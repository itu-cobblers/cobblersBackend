#!/usr/bin/env bash
#
# Concurrency knee sweep — runs piston-sweep.js at several concurrencies and
# collects one result line per point, so the six runs transcribe straight into a
# table.
#
# Run this ON VM-1: it already reaches VM-2:2000, so there is no firewall change
# and no network noise, and the backend is idle during this stage so the load
# generator contends with nothing under test.
#
# Usage, from your laptop:
#   scp -r load-tests <vm-1>:/root/
#   ssh <vm-1> 'cd /root/load-tests && PISTON_URL=http://<vm-2>:2000 bash sweep.sh'
#
#   PISTON_URL   required, e.g. http://10.0.0.3:2000
#   KS           concurrencies to sweep (default "1 2 4 8 16 32")
#   JOBS         jobs per point (default 32) — identical work at every point
#   PAYLOAD      hello | typical (default hello)
#
# NOTE: if k6 is not installed the script falls back to Docker with a VOLUME MOUNT.
# The `docker run -i grafana/k6 run -` stdin form does NOT work here, because the
# script imports ./lib/payloads.js and stdin mode cannot resolve relative imports.

set -uo pipefail

: "${PISTON_URL:?set PISTON_URL, e.g. PISTON_URL=http://10.0.0.3:2000}"
KS="${KS:-1 2 4 8 16 32}"
JOBS="${JOBS:-32}"
PAYLOAD="${PAYLOAD:-hello}"

STAMP="$(date +%Y%m%d-%H%M)"
OUT="sweep-${STAMP}.txt"

if command -v k6 >/dev/null 2>&1; then
  RUNNER="native"
elif command -v docker >/dev/null 2>&1; then
  RUNNER="docker"
else
  echo "ERROR: neither k6 nor docker is available." >&2
  exit 1
fi

run_point() {
  local k="$1"
  if [ "$RUNNER" = "native" ]; then
    PISTON_URL="$PISTON_URL" K="$k" JOBS="$JOBS" PAYLOAD="$PAYLOAD" \
      k6 run --no-usage-report --quiet piston-sweep.js 2>&1
  else
    docker run --rm \
      -v "$PWD:/scripts" -w /scripts \
      -e PISTON_URL="$PISTON_URL" -e K="$k" -e JOBS="$JOBS" -e PAYLOAD="$PAYLOAD" \
      grafana/k6 run --no-usage-report --quiet piston-sweep.js 2>&1
  fi
}

{
  echo "# piston concurrency sweep  ${STAMP}"
  echo "# target=${PISTON_URL} jobs/point=${JOBS} payload=${PAYLOAD} runner=${RUNNER}"
  echo
} | tee "$OUT"

for k in $KS; do
  echo ">>> K=$k ..." >&2
  LINE="$(run_point "$k" | grep -E '^K=' | tail -1)"
  if [ -z "$LINE" ]; then
    LINE="K=$k FAILED — no result line (rerun this point alone to see the error)"
  fi
  echo "$LINE" | tee -a "$OUT"
done

cat <<EOF | tee -a "$OUT"

# How to read this:
#   throughput should RISE with K, then FLATTEN. The K where it flattens is the
#   knee — that is the right value for PISTON_MAX_CONCURRENT_JOBS. Expect 4-8:
#   for a CPU-bound job the optimum is near the core count, and concurrency
#   between the knee and Piston's stock ~64 buys nothing but latency and memory.
#
#   If throughput flattens at roughly (cores / C), more cores convert directly
#   into throughput and a VM upgrade will help. If it flattens well below that,
#   something other than CPU is the limit and an upgrade will not help.
#
#   sigkill must stay 0. The first K where it goes non-zero is where the
#   configured run_timeout starts killing correct student code under load —
#   and because Piston answers HTTP 200 for a kill, that is invisible to any
#   HTTP-status-based check and to every server-side metric.
EOF

echo
echo "saved: $OUT"
