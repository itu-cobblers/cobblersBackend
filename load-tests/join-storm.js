// Join storm — 80 students join a room at once, the way 09:00 on day 1 actually
// looks. This is a *certainty* on a camp morning, unlike a synchronised Run burst
// which is only a possibility.
//
// It is not a CPU test. Each JoinSession runs AttendanceService.RecordAttendanceAsync
// = 3 SELECTs (session, student, attendance-exists) + a SaveChanges transaction,
// then TWO group broadcasts (StudentJoined, RosterUpdated). Eighty of those at once
// land on Postgres, whose max_connections is 100 — the same number Npgsql's pool
// defaults to, i.e. zero headroom. There is no EnableRetryOnFailure, so exhaustion
// surfaces as a HubException, and the browser client's withAutomaticReconnect turns
// that into a retry storm. That is the failure mode this measures.
//
// Unlike hub-broadcast-stress.js, this does REAL SignalR negotiation
// (POST /hub/negotiate then ws?id=<token>), which is what the browser client does
// via `new HubConnectionBuilder().withUrl('/hub')`. Connecting raw to /hub without
// an id leaves the negotiate leg — a REST round trip per student, hitting the same
// Kestrel — completely untested.
//
// WRITES TO THE DATABASE: one student row and one attendance row per VU. Every id
// is prefixed `loadtest-` so cleanup is an ordered delete. Point it at a throwaway
// session, never a room a real class is in.
//
//   BASE_URL=https://cobblerscoders.tech SESSION_CODE=ABCD VUS=80 k6 run join-storm.js
//
//   BASE_URL      default http://localhost:5046
//   SESSION_CODE  required — a throwaway room code from POST /api/sessions
//   VUS           students joining at once. Default 80
//   HOLD          seconds to hold the socket open after joining. Default 20
//   RAMP          seconds to spread the joins over. Default 5 (0 = all at once)

import http from 'k6/http'
import ws from 'k6/ws'
import { check } from 'k6'
import { Counter, Rate, Trend } from 'k6/metrics'

const BASE_URL = (__ENV.BASE_URL || 'http://localhost:5046').replace(/\/$/, '')
const SESSION_CODE = __ENV.SESSION_CODE
const VUS = parseInt(__ENV.VUS || '80', 10)
const HOLD = parseInt(__ENV.HOLD || '20', 10)
const RAMP = parseInt(__ENV.RAMP || '5', 10)

if (!SESSION_CODE) {
  throw new Error('SESSION_CODE is required — create a throwaway room with POST /api/sessions first')
}

const RS = '\x1e' // SignalR's record separator; every frame is terminated by it

const registerDuration = new Trend('register_duration', true)
const negotiateDuration = new Trend('negotiate_duration', true)
const joinDuration = new Trend('join_invoke_duration', true)
const wsHandshake = new Trend('ws_handshake_duration', true)

const registerOk = new Rate('register_ok')
const negotiateOk = new Rate('negotiate_ok')
const connectOk = new Rate('ws_connect_ok')
const joinOk = new Rate('join_ok')

const joinErrors = new Counter('join_errors')
const connectFailures = new Counter('ws_connect_failures')
const rosterFrames = new Counter('roster_updated_frames')
const studentJoinedFrames = new Counter('student_joined_frames')
const rosterBytes = new Counter('roster_updated_bytes')

export const options = {
  scenarios: {
    storm: RAMP > 0
      ? {
          // Spread over RAMP seconds: 80 humans opening a laptop lid do not arrive
          // in the same millisecond, and the arrival shape is what decides whether
          // the connection pool is hit all at once.
          executor: 'ramping-arrival-rate',
          startRate: 0,
          timeUnit: '1s',
          preAllocatedVUs: VUS,
          maxVUs: VUS * 2,
          stages: [
            { duration: `${RAMP}s`, target: Math.ceil(VUS / RAMP) },
            { duration: '1s', target: 0 },
          ],
        }
      : { executor: 'per-vu-iterations', vus: VUS, iterations: 1, maxDuration: '5m' },
  },
  thresholds: {
    // Every one of these is an absolute requirement, and every one is counted in a
    // way that cannot pass vacuously — a Rate with zero samples fails, unlike the
    // zero-sample Trend threshold in hub-broadcast-stress.js which passes silently.
    register_ok: ['rate==1'],
    negotiate_ok: ['rate==1'],
    ws_connect_ok: ['rate==1'],
    join_ok: ['rate==1'],
    ws_connect_failures: ['count==0'],
    join_errors: ['count==0'],
    'join_invoke_duration': ['p(95)<2000', 'max<10000'],
  },
}

export default function () {
  const studentId = `loadtest-${__VU}-${Date.now()}`
  const displayName = `LOADTEST-${__VU}`

  // 1. Register. JoinSession throws HubException("No student '<id>'") if the row
  //    does not exist, so a failure here would surface as a misleading join error.
  const reg = http.put(
    `${BASE_URL}/api/students/${studentId}`,
    JSON.stringify({ displayName }),
    { headers: { 'Content-Type': 'application/json' }, tags: { name: 'register' }, timeout: '60s' }
  )
  registerDuration.add(reg.timings.duration)
  const registered = reg.status === 204 || reg.status === 200
  registerOk.add(registered)
  check(reg, { 'student registered': () => registered })
  if (!registered) return

  // 2. Negotiate, exactly as the browser client does.
  const neg = http.post(`${BASE_URL}/hub/negotiate?negotiateVersion=1`, null, {
    tags: { name: 'negotiate' },
    timeout: '60s',
  })
  negotiateDuration.add(neg.timings.duration)
  let token = null
  if (neg.status === 200) {
    try {
      const body = neg.json()
      token = body.connectionToken || body.connectionId
    } catch (err) {
      token = null
    }
  }
  negotiateOk.add(token !== null)
  check(neg, { 'negotiate returned a token': () => token !== null })
  if (!token) return

  // 3. Open the socket with the negotiated id.
  const wsUrl =
    `${BASE_URL.replace(/^http/, 'ws')}/hub?id=${encodeURIComponent(token)}`

  let joinSentAt = 0
  let joined = false
  let connectedAt = 0

  const res = ws.connect(wsUrl, { tags: { name: 'hub' } }, function (socket) {
    socket.on('open', function () {
      connectedAt = Date.now()
      socket.send(`{"protocol":"json","version":1}${RS}`)
    })

    let buffer = ''
    socket.on('message', function (data) {
      buffer += data
      // Frames are RS-terminated and may arrive coalesced or split.
      const parts = buffer.split(RS)
      buffer = parts.pop()

      for (const part of parts) {
        if (!part) continue
        let msg
        try {
          msg = JSON.parse(part)
        } catch (err) {
          continue
        }

        // The handshake reply is `{}` (or `{"error":...}`) with no `type`.
        if (msg.type === undefined) {
          if (msg.error) {
            joinErrors.add(1)
            socket.close()
            return
          }
          wsHandshake.add(Date.now() - connectedAt)
          joinSentAt = Date.now()
          socket.send(
            JSON.stringify({
              type: 1,
              target: 'JoinSession',
              arguments: [{ code: SESSION_CODE, studentId, displayName }],
              invocationId: '1',
            }) + RS
          )
          continue
        }

        // type 3 = completion of our JoinSession invocation.
        if (msg.type === 3 && msg.invocationId === '1') {
          joinDuration.add(Date.now() - joinSentAt)
          if (msg.error) {
            joinErrors.add(1)
            joined = false
          } else {
            joined = true
          }
          continue
        }

        // type 1 = a server-to-client broadcast. Students never handle these three
        // (see sessionHub.ts — they are teacher-only callbacks), yet the server
        // sends them to the whole group, so every student pays to receive them.
        // Counting the bytes is how you size that waste.
        if (msg.type === 1) {
          if (msg.target === 'RosterUpdated') {
            rosterFrames.add(1)
            rosterBytes.add(part.length)
          } else if (msg.target === 'StudentJoined') {
            studentJoinedFrames.add(1)
          }
        }
      }
    })

    socket.on('error', function () {
      joinErrors.add(1)
    })

    // Hold the connection so the peak of concurrent sockets and Postgres
    // connections is observable, then leave cleanly.
    socket.setTimeout(function () {
      socket.close()
    }, HOLD * 1000)
  })

  // ws.connect returns after the socket closes. A failed upgrade has no 101, and
  // must be counted here — incrementing only inside an error handler is how the
  // old script could report zero failures having connected nothing.
  const upgraded = res && res.status === 101
  connectOk.add(upgraded)
  if (!upgraded) connectFailures.add(1)
  joinOk.add(joined)

  check(res, { 'ws upgraded (101)': () => upgraded })
}

export function handleSummary(data) {
  const m = data.metrics
  const n = (metric, key) => {
    const e = m[metric]
    if (!e || !e.values || e.values[key] === undefined) return 0
    return e.values[key]
  }
  const ms = (v) => `${Math.round(v)}ms`
  const pctOf = (metric) => `${(n(metric, 'rate') * 100).toFixed(1)}%`

  const roster = n('roster_updated_bytes', 'count')
  const lines = [
    '='.repeat(100),
    `JOIN STORM  vus=${VUS} ramp=${RAMP}s hold=${HOLD}s room=${SESSION_CODE}`,
    `  register     ${pctOf('register_ok').padEnd(8)} p95 ${ms(n('register_duration', 'p(95)'))}`,
    `  negotiate    ${pctOf('negotiate_ok').padEnd(8)} p95 ${ms(n('negotiate_duration', 'p(95)'))}`,
    `  ws upgrade   ${pctOf('ws_connect_ok').padEnd(8)} p95 ${ms(n('ws_handshake_duration', 'p(95)'))}   failures=${n('ws_connect_failures', 'count')}`,
    `  JoinSession  ${pctOf('join_ok').padEnd(8)} p50 ${ms(n('join_invoke_duration', 'med'))}  p95 ${ms(n('join_invoke_duration', 'p(95)'))}  max ${ms(n('join_invoke_duration', 'max'))}`,
    `  join errors  ${n('join_errors', 'count')}   <- HubException, incl. pool exhaustion`,
    '',
    `  broadcast waste (students receive but never handle these):`,
    `    StudentJoined frames  ${n('student_joined_frames', 'count')}`,
    `    RosterUpdated frames  ${n('roster_updated_frames', 'count')}`,
    `    RosterUpdated bytes   ${(roster / 1048576).toFixed(2)} MB`,
    '='.repeat(100),
  ]
  return { stdout: `\n${lines.join('\n')}\n\n` }
}
