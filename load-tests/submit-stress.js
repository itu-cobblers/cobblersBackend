import http from 'k6/http'
import { check } from 'k6'
import { uuidv4 } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js'

// Stress test: ramp up virtual users until response times degrade or errors appear.
// Defaults to the final class size (65). Use MAX_VUS to test beyond that if needed.
//
// Required environment variable:
//   ASSIGNMENT_ID  - the assignment to submit to (e.g. 1)
//
// Optional environment variables:
//   BASE_URL       - defaults to http://localhost:5046
//   SESSION_ID     - room code to attach to the submission; omit for solo submissions
//   MAX_VUS        - peak virtual users, defaults to 65

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5046'
const ASSIGNMENT_ID = __ENV.ASSIGNMENT_ID
const SESSION_ID = __ENV.SESSION_ID
const MAX_VUS = parseInt(__ENV.MAX_VUS || '65', 10)

if (!ASSIGNMENT_ID) {
  throw new Error('ASSIGNMENT_ID environment variable is required')
}

export const options = {
  scenarios: {
    stress: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '2m', target: MAX_VUS }, // ramp up
        { duration: '3m', target: MAX_VUS }, // hold
        { duration: '1m', target: 0 },       // ramp down
      ],
      gracefulRampDown: '30s',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.05'],
    http_req_duration: ['p(95)<15000'],
  },
}

export default function () {
  const studentId = uuidv4()
  const displayName = `stress-${__VU}-${__ITER}`

  http.put(
    `${BASE_URL}/api/students/${studentId}`,
    JSON.stringify({ displayName }),
    {
      headers: { 'Content-Type': 'application/json' },
      tags: { name: 'register_student' },
    }
  )

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
