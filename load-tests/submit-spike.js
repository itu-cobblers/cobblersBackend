import http from 'k6/http'
import { check } from 'k6'
import { uuidv4 } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js'

// Load test: 65 students press Submit as close to simultaneously as k6 can start them.
//
// Each virtual user:
//   1. Registers a unique student identity (PUT /api/students/{studentId})
//   2. Submits a simple Java program to one assignment
//      (POST /api/assignments/{assignmentId}/submissions)
//
// Required environment variable:
//   ASSIGNMENT_ID  - the assignment to submit to (e.g. 1)
//
// Optional environment variables:
//   BASE_URL       - defaults to http://localhost:5046
//   SESSION_ID     - room code to attach to the submission; omit for solo submissions
//   VUS            - number of virtual users, defaults to 65
//   ITERATIONS     - submissions per virtual user, defaults to 1

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5046'
const ASSIGNMENT_ID = __ENV.ASSIGNMENT_ID
const SESSION_ID = __ENV.SESSION_ID
const VUS = parseInt(__ENV.VUS || '65', 10)
const ITERATIONS = parseInt(__ENV.ITERATIONS || '1', 10)

if (!ASSIGNMENT_ID) {
  throw new Error('ASSIGNMENT_ID environment variable is required')
}

export const options = {
  scenarios: {
    spike: {
      executor: 'per-vu-iterations',
      vus: VUS,
      iterations: ITERATIONS,
      maxDuration: '10m',
    },
  },
  thresholds: {
    // Fewer than 5% of requests may fail.
    http_req_failed: ['rate<0.05'],
    // 95% of submissions should finish in under 15s (Piston Java runs take time).
    http_req_duration: ['p(95)<15000'],
  },
}

export default function () {
  const studentId = uuidv4()
  const displayName = `load-${__VU}-${__ITER}`

  const registerRes = http.put(
    `${BASE_URL}/api/students/${studentId}`,
    JSON.stringify({ displayName }),
    {
      headers: { 'Content-Type': 'application/json' },
      tags: { name: 'register_student' },
    }
  )

  check(registerRes, {
    'student registered (204)': (r) => r.status === 204,
  })

  const payload = {
    studentId,
    content: `public class Main {
  public static void main(String[] args) {
    System.out.println("Hello from ${displayName}");
  }
}`,
  }

  if (SESSION_ID) {
    payload.sessionId = SESSION_ID
  }

  const submitRes = http.post(
    `${BASE_URL}/api/assignments/${ASSIGNMENT_ID}/submissions`,
    JSON.stringify(payload),
    {
      headers: { 'Content-Type': 'application/json' },
      tags: { name: 'submit_assignment' },
    }
  )

  check(submitRes, {
    'submit returned 200': (r) => r.status === 200,
    'submit returned a result': (r) => r.status === 200 && r.json('subId') !== undefined,
  })
}
