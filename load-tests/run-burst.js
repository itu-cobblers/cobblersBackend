// The 80-way Run burst — "the teacher says everyone press Run now".
//
// Hits POST /api/execute through the full production path (TLS -> nginx -> backend
// -> Piston). That endpoint touches NO database, so the faithful workload and the
// zero-pollution workload are the same workload: this writes nothing and is safe
// against production.
//
// Safe to run from your laptop. The quantity of interest is a 30-60s queueing
// tail, so 30ms of RTT is noise. Raise the file-descriptor limit first:
//
//   ulimit -n 4096
//   BASE_URL=https://cobblerscoders.tech VUS=80 k6 run run-burst.js
//
// Environment variables:
//   BASE_URL  default http://localhost:5046
//   VUS       simultaneous students. Default 80. Use 20 to model a staggered class.
//   PAYLOAD   hello | typical | spin. Default hello
//
// What to read off the result:
//   * wall     — the drain time. Compare against (VUS x CPU-s-per-job) / (cores x 0.85).
//   * max      — THE number. The product requirement is the last student, not p95.
//   * bodyOK   — must be >= 99.5%. A payload that works at VUS=1 must work at VUS=80.
//   * http_504 — must be 0. Any 504 means the timeout ladder is still inverted:
//                nginx gives up at its stock 60s while the backend waits out
//                HttpClient's 100s default, so a student sees an error for a job
//                that actually succeeded, and the backend holds the Piston slot
//                for another 40s afterwards.

import http from 'k6/http'
import { check } from 'k6'
import { Counter, Rate, Trend } from 'k6/metrics'
import { buildPayload } from './lib/payloads.js'

const BASE_URL = (__ENV.BASE_URL || 'http://localhost:5046').replace(/\/$/, '')
const VUS = parseInt(__ENV.VUS || '80', 10)
const PAYLOAD = __ENV.PAYLOAD || 'hello'

const runDuration = new Trend('run_duration', true)
const bodyOk = new Rate('run_body_ok')
const status502 = new Counter('http_502')
const status503 = new Counter('http_503')
const status504 = new Counter('http_504')
const statusOther = new Counter('http_other_error')
const wrongVerdict = new Counter('run_wrong_verdict')

// The CDF at the points the decision actually turns on, rather than a percentile
// that hides the tail.
const under10s = new Counter('run_completed_under_10s')
const under30s = new Counter('run_completed_under_30s')
const under60s = new Counter('run_completed_under_60s')

export const options = {
  scenarios: {
    // per-vu-iterations with one iteration each: a fixed batch of VUS requests,
    // every one from an agent with exactly one outstanding request. That is
    // faithful — the UI disables Run while a run is in flight, so a real student
    // cannot have two.
    burst: {
      executor: 'per-vu-iterations',
      vus: VUS,
      iterations: 1,
      maxDuration: '10m',
    },
  },
  thresholds: {
    // Tagged, so this is the Run request's own p95 and not an average of it with
    // something else. Untagged thresholds are why the existing scripts' "p(95)"
    // is really a p90 of the slow call mixed with a fast one.
    //
    // The numbers are the stated classroom tolerance for the worst case where the
    // whole class happens to press Run at the same moment: up to ~30s for the last
    // student is acceptable, provided nothing errors and nothing is killed. The
    // rows below the latency ones are the real requirement and are absolute.
    'run_duration{name:execute}': ['p(95)<25000', 'max<30000'],
    run_body_ok: ['rate>0.995'],
    http_504: ['count==0'],
    http_502: ['count==0'],
  },
}

export default function () {
  const payload = buildPayload(PAYLOAD, `${__VU}`)

  const res = http.post(
    `${BASE_URL}/api/execute`,
    JSON.stringify({ code: payload.source }),
    {
      headers: { 'Content-Type': 'application/json' },
      // k6's default per-request timeout is 60s, which would land exactly on
      // nginx's default and make the two indistinguishable in the results.
      timeout: '180s',
      tags: { name: 'execute' },
    }
  )

  runDuration.add(res.timings.duration, { name: 'execute' })

  const seconds = res.timings.duration / 1000
  if (seconds < 10) under10s.add(1)
  if (seconds < 30) under30s.add(1)
  if (seconds < 60) under60s.add(1)

  if (res.status !== 200) {
    if (res.status === 502) status502.add(1)
    else if (res.status === 503) status503.add(1)
    else if (res.status === 504) status504.add(1)
    else statusOther.add(1)
    bodyOk.add(false)
    check(res, { 'execute answered 200': () => false })
    return
  }

  let body = null
  try {
    body = res.json()
  } catch (err) {
    statusOther.add(1)
    bodyOk.add(false)
    return
  }

  const verdict = body && body.status
  const stdout = (body && body.stdout) || ''
  const markerPresent = stdout.indexOf(payload.marker) !== -1

  // The whole point of asserting the body: a Piston SIGKILL comes back as HTTP 200
  // with status "runtime_error", indistinguishable from a student's own exception.
  // Only the marker proves the program actually ran to completion.
  const ok = verdict === 'success' && markerPresent
  bodyOk.add(ok)
  if (!ok) wrongVerdict.add(1)

  check(res, {
    'status is success': () => verdict === 'success',
    'stdout has marker': () => markerPresent,
  })
}

export function handleSummary(data) {
  const m = data.metrics
  const wallSeconds = data.state.testRunDurationMs / 1000
  const n = (metric, key) => {
    const entry = m[metric]
    if (!entry || !entry.values || entry.values[key] === undefined) return 0
    return entry.values[key]
  }
  const s = (v) => `${(v / 1000).toFixed(1)}s`

  const okRate = n('run_body_ok', 'rate') * 100
  const pct = (c) => `${((c / VUS) * 100).toFixed(0)}%`

  const lines = [
    '='.repeat(100),
    `BURST  vus=${VUS} payload=${PAYLOAD} target=${BASE_URL}`,
    `  drain (wall)   ${wallSeconds.toFixed(1)}s`,
    `  p50 / p95      ${s(n('run_duration', 'med'))} / ${s(n('run_duration', 'p(95)'))}`,
    `  MAX            ${s(n('run_duration', 'max'))}   <- the last student`,
    `  CDF            <10s ${pct(n('run_completed_under_10s', 'count'))}` +
      `  <30s ${pct(n('run_completed_under_30s', 'count'))}` +
      `  <60s ${pct(n('run_completed_under_60s', 'count'))}`,
    `  bodyOK         ${okRate.toFixed(1)}%   (must be >= 99.5%)`,
    `  wrong verdict  ${n('run_wrong_verdict', 'count')}   <- correct code called wrong`,
    `  502 / 503 / 504  ${n('http_502', 'count')} / ${n('http_503', 'count')} / ${n('http_504', 'count')}` +
      `   (504 > 0 means the timeout ladder is inverted)`,
    '='.repeat(100),
  ]

  return { stdout: `\n${lines.join('\n')}\n\n` }
}
