import http from 'k6/http'
import { check } from 'k6'
import { uuidv4 } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js'

// Ramp from 1 to 65 VUs to match the final expected class size.
//
// Required environment variable:
//   ASSIGNMENT_ID
//
// Optional:
//   BASE_URL    - defaults to http://localhost:5046
//   SESSION_ID  - room code; omit for solo submissions

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5046'
const ASSIGNMENT_ID = __ENV.ASSIGNMENT_ID
const SESSION_ID = __ENV.SESSION_ID

if (!ASSIGNMENT_ID) {
  throw new Error('ASSIGNMENT_ID environment variable is required')
}

export const options = {
  scenarios: {
    ramp: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { duration: '8m', target: 65 }, // 1 -> 65 VUs gradually
        { duration: '2m', target: 65 }, // hold at 65
        { duration: '1m', target: 0 },  // ramp down
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
  const displayName = `ramp-${__VU}-${__ITER}`

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
