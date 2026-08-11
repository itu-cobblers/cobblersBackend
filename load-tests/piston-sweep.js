// Piston concurrency sweep — hits Piston DIRECTLY, bypassing the backend and the
// database entirely. Zero rows written, so this is safe to run against production.
//
// It answers two questions:
//   1. What does one Java job cost in CPU-seconds?  (run at K=1, then read the
//      cgroup counter delta on the Piston host — see RUNBOOK-capacity.md)
//   2. Where is the concurrency knee?  (sweep K and watch throughput flatten)
//
// Those two numbers are the entire input to the VM-sizing decision:
//   cores needed = (jobs in burst x CPU-seconds per job) / (acceptable wait x 0.85)
//
// Run this from VM-1, not your laptop: VM-1 already reaches VM-2:2000, so there is
// no firewall change and no network noise, and the backend is idle during this
// stage so the load generator contends with nothing under test.
//
//   PISTON_URL=http://<vm-2>:2000 K=1 JOBS=32 k6 run piston-sweep.js
//
// Environment variables:
//   PISTON_URL  Piston base URL, no trailing slash. Default http://localhost:2000
//   K           concurrency — how many jobs run at once. Default 1
//   JOBS        total jobs in the batch, shared across the K workers. Default 32
//   PAYLOAD     hello | typical | spin. Default hello
//
// The request body mirrors ExecutorService.ExecuteAsync exactly, including the
// file name "Main" with NO .java suffix — Piston's Java 15.0.2 run script is
// `mv $1 $1.java && java $1.java`, so it appends the suffix itself.

import http from 'k6/http'
import { check } from 'k6'
import { Counter, Rate, Trend } from 'k6/metrics'
import { buildPayload } from './lib/payloads.js'

const PISTON_URL = (__ENV.PISTON_URL || 'http://localhost:2000').replace(/\/$/, '')
const K = parseInt(__ENV.K || '1', 10)
const JOBS = parseInt(__ENV.JOBS || '32', 10)
const PAYLOAD = __ENV.PAYLOAD || 'hello'

const jobDuration = new Trend('piston_job_duration', true)
const jobsSigkilled = new Counter('piston_jobs_sigkilled')
const jobsNonZeroExit = new Counter('piston_jobs_nonzero_exit')
const jobsHttpError = new Counter('piston_jobs_http_error')
const bodyOk = new Rate('piston_body_ok')

export const options = {
  scenarios: {
    // shared-iterations gives exactly JOBS total jobs spread across K workers, so
    // every point in the sweep does identical work at a different concurrency.
    // per-vu-iterations would need JOBS to divide evenly by K.
    sweep: {
      executor: 'shared-iterations',
      vus: K,
      iterations: JOBS,
      maxDuration: '20m',
    },
  },
  // No thresholds. This script is a measurement instrument, not a pass/fail gate —
  // a "failing" run at K=32 is the finding, not an error.
  discardResponseBodies: false,
}

export default function () {
  const payload = buildPayload(PAYLOAD, `${__VU}-${__ITER}`)

  const res = http.post(
    `${PISTON_URL}/api/v2/execute`,
    JSON.stringify({
      language: 'java',
      version: '*',
      files: [{ name: 'Main', content: payload.source }],
    }),
    {
      headers: { 'Content-Type': 'application/json' },
      // Well above any plausible run_timeout, so k6's own abort is never mistaken
      // for a server failure.
      timeout: '180s',
      tags: { name: 'piston_execute' },
    }
  )

  jobDuration.add(res.timings.duration)

  if (res.status !== 200) {
    jobsHttpError.add(1)
    bodyOk.add(false)
    check(res, { 'piston answered 200': () => false })
    return
  }

  let run = null
  try {
    run = res.json('run')
  } catch (err) {
    jobsHttpError.add(1)
    bodyOk.add(false)
    return
  }

  const stdout = (run && run.stdout) || ''
  const signal = run ? run.signal : null
  const code = run ? run.code : null

  // A run-timeout kill: code null, signal SIGKILL, marker never printed. Piston
  // still answers HTTP 200 for this, which is why it needs its own counter.
  if (signal) jobsSigkilled.add(1)
  if (code !== 0) jobsNonZeroExit.add(1)

  const markerPresent = stdout.indexOf(payload.marker) !== -1
  bodyOk.add(markerPresent)

  check(res, {
    'run.code is 0': () => code === 0,
    'not killed': () => !signal,
    'stdout has marker': () => markerPresent,
  })
}

// One compact line per run, so the six sweep points transcribe straight into the
// results table. Deliberately no jslib import — nothing here should depend on the
// network being up the night before the camp.
export function handleSummary(data) {
  const m = data.metrics
  const wallSeconds = data.state.testRunDurationMs / 1000
  const n = (metric, key) => {
    const entry = m[metric]
    if (!entry || !entry.values || entry.values[key] === undefined) return 0
    return entry.values[key]
  }
  const ms = (v) => `${Math.round(v)}ms`

  const throughput = wallSeconds > 0 ? JOBS / wallSeconds : 0
  const okRate = n('piston_body_ok', 'rate') * 100

  const line =
    `K=${K} payload=${PAYLOAD} jobs=${JOBS} ` +
    `wall=${wallSeconds.toFixed(1)}s throughput=${throughput.toFixed(2)}job/s ` +
    `p50=${ms(n('piston_job_duration', 'med'))} ` +
    `p95=${ms(n('piston_job_duration', 'p(95)'))} ` +
    `max=${ms(n('piston_job_duration', 'max'))} ` +
    `sigkill=${n('piston_jobs_sigkilled', 'count')} ` +
    `nonzero=${n('piston_jobs_nonzero_exit', 'count')} ` +
    `httperr=${n('piston_jobs_http_error', 'count')} ` +
    `bodyOK=${okRate.toFixed(1)}%`

  return {
    stdout: `\n${'='.repeat(100)}\n${line}\n${'='.repeat(100)}\n\n`,
    'piston-sweep-last.txt': `${line}\n`,
  }
}
