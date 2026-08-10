import ws from 'k6/ws'
import http from 'k6/http'
import { check } from 'k6'
import { Trend, Counter } from 'k6/metrics'
import { uuidv4 } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js'

// Stress-tests the SignalR broadcast fan-out path (SessionHub's SubmissionRecorded
// event), which submit-stress.js / submit-spike.js do NOT cover — those only exercise
// the REST submission endpoint. Every VU here: registers a student, opens a SignalR
// WebSocket to /hub, joins SESSION_ID's room, submits one assignment over REST, then
// measures how long its own SubmissionRecorded broadcast takes to come back over the
// socket (own_broadcast_latency_ms) and how many broadcasts it sees overall
// (submission_broadcasts_received, expected to approach VUS per VU if nothing is
// dropped under load).
//
// Point BASE_URL at whatever sits in front of /hub in the topology you're testing —
// e.g. https://cobblerscoders.tech, which routes through the frontend (nginx)
// container's WebSocket proxy, not http://backend:5046 directly — so the test
// actually exercises the same path production traffic takes.
//
// Required environment variables:
//   ASSIGNMENT_ID  - the assignment to submit to (e.g. 1)
//   SESSION_ID     - room code from an already-created, active session
//                    (POST /api/sessions); JoinSession fails for an unknown code.
//
// Optional environment variables:
//   BASE_URL       - defaults to http://localhost:5046
//   VUS            - concurrent students, defaults to 80
//   LISTEN_WINDOW  - seconds each VU keeps listening after submitting, defaults to 15

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5046'
const WS_URL = BASE_URL.replace(/^http/, 'ws') + '/hub'
const ASSIGNMENT_ID = __ENV.ASSIGNMENT_ID
const SESSION_ID = __ENV.SESSION_ID
const VUS = parseInt(__ENV.VUS || '80', 10)
const LISTEN_WINDOW = parseInt(__ENV.LISTEN_WINDOW || '15', 10)

if (!ASSIGNMENT_ID) {
  throw new Error('ASSIGNMENT_ID environment variable is required')
}
if (!SESSION_ID) {
  throw new Error('SESSION_ID environment variable is required (create an active session first)')
}

// SignalR's JSON hub protocol terminates every message with a record separator.
const RECORD_SEPARATOR = '\x1e'

const ownBroadcastLatency = new Trend('own_broadcast_latency_ms')
const broadcastsReceived = new Counter('submission_broadcasts_received')
const joinFailures = new Counter('join_failures')

export const options = {
  scenarios: {
    broadcast_fanout: {
      executor: 'per-vu-iterations',
      vus: VUS,
      iterations: 1,
      maxDuration: `${LISTEN_WINDOW + 30}s`,
    },
  },
  thresholds: {
    join_failures: ['count==0'],
    own_broadcast_latency_ms: ['p(95)<5000'],
  },
}

export default function () {
  const studentId = uuidv4()
  const displayName = `hub-${__VU}-${__ITER}`

  http.put(
    `${BASE_URL}/api/students/${studentId}`,
    JSON.stringify({ displayName }),
    { headers: { 'Content-Type': 'application/json' }, tags: { name: 'register_student' } }
  )

  let handshakeDone = false
  let submitSentAt = null

  const res = ws.connect(WS_URL, {}, function (socket) {
    let buffer = ''

    socket.on('open', () => {
      socket.send(JSON.stringify({ protocol: 'json', version: 1 }) + RECORD_SEPARATOR)
    })

    socket.on('message', (data) => {
      buffer += data
      const parts = buffer.split(RECORD_SEPARATOR)
      buffer = parts.pop() // keep any incomplete trailing chunk for the next frame

      for (const part of parts) {
        if (!part) continue
        const msg = JSON.parse(part)

        // Handshake response is `{}` (or `{"error": "..."}`) — it has no `type` field.
        if (!handshakeDone) {
          handshakeDone = true
          if (msg.error) {
            joinFailures.add(1)
            socket.close()
            continue
          }
          socket.send(JSON.stringify({
            type: 1,
            target: 'JoinSession',
            arguments: [{ code: SESSION_ID, studentId, displayName }],
            invocationId: '1',
          }) + RECORD_SEPARATOR)
          continue
        }

        // Completion of our JoinSession invocation.
        if (msg.type === 3 && msg.invocationId === '1') {
          if (msg.error) {
            joinFailures.add(1)
            socket.close()
            continue
          }
          submitSentAt = Date.now()
          const payload = {
            studentId,
            sessionId: SESSION_ID,
            content: `public class Main {
  public static void main(String[] args) {
    System.out.println("Hello from ${displayName}");
  }
}`,
          }
          http.post(
            `${BASE_URL}/api/assignments/${ASSIGNMENT_ID}/submissions`,
            JSON.stringify(payload),
            { headers: { 'Content-Type': 'application/json' }, tags: { name: 'submit_assignment' } }
          )
          continue
        }

        // Group broadcast: SubmissionRecorded fires for every submission in the room.
        if (msg.type === 1 && msg.target === 'SubmissionRecorded') {
          broadcastsReceived.add(1)
          const body = msg.arguments && msg.arguments[0]
          if (submitSentAt && body && body.studentId === studentId) {
            ownBroadcastLatency.add(Date.now() - submitSentAt)
          }
        }
        // type 6 = server ping, type 7 = close — nothing to do for either here.
      }
    })

    socket.setTimeout(() => socket.close(), LISTEN_WINDOW * 1000)
  })

  check(res, { 'ws connected': (r) => r && r.status === 101 })
}
