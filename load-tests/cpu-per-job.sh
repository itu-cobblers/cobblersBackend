#!/usr/bin/env bash
#
# THE measurement. Everything about VM sizing follows from its output.
#
# Run this ON VM-2 (the Piston host). It fires N sequential Java jobs at Piston on
# localhost and reads the Piston container's own CPU accounting counter before and
# after, giving CPU-seconds per job:
#
#   cores needed = (jobs in burst x CPU-seconds per job) / (acceptable wait x 0.85)
#
# Sequential on purpose: at concurrency 1 there is no queueing, so the number is
# pure service cost. The curl loop runs outside the Piston container, so it does
# not contaminate the container cgroup being measured.
#
# Usage, from your laptop:
#   scp cpu-per-job.sh <vm-2>:/root/
#   ssh <vm-2> 'bash /root/cpu-per-job.sh'
#
#   N=30          jobs to run (default 30)
#   CONTAINER     container name or id (default: auto-detect by name ~ piston)
#   PORT          Piston port (default 2000)

set -uo pipefail

N="${N:-30}"
PORT="${PORT:-2000}"
CONTAINER="${CONTAINER:-$(docker ps --format '{{.Names}}' | grep -i piston | head -1)}"

if [ -z "$CONTAINER" ]; then
  echo "ERROR: no running container matching 'piston'. Set CONTAINER=<name>." >&2
  exit 1
fi

FULL_ID="$(docker inspect --format '{{.Id}}' "$CONTAINER")" || exit 1

# Read the container's cumulative CPU time in microseconds. Several layouts exist
# depending on cgroup version and Docker's cgroup driver, so try each and report
# which one worked — a silently wrong path would produce a plausible wrong answer.
#
# Prints "<usec> <source-label>" on one line. The label has to travel out with the
# value rather than via a variable, because every call site uses $( ), which is a
# subshell — an assignment inside the function would never reach the caller.
read_cpu_usec() {
  local v
  # cgroup v2, private cgroup namespace (Docker 20.10+ default): the container
  # sees its own cgroup at the root. Most reliable when it works.
  v=$(docker exec "$CONTAINER" cat /sys/fs/cgroup/cpu.stat 2>/dev/null | awk '/^usage_usec/{print $2}')
  if [ -n "${v:-}" ]; then echo "$v container-cgroup-v2"; return 0; fi

  # cgroup v2 on the host, systemd driver.
  v=$(awk '/^usage_usec/{print $2}' "/sys/fs/cgroup/system.slice/docker-${FULL_ID}.scope/cpu.stat" 2>/dev/null)
  if [ -n "${v:-}" ]; then echo "$v host-cgroup-v2-systemd"; return 0; fi

  # cgroup v2 on the host, cgroupfs driver.
  v=$(awk '/^usage_usec/{print $2}' "/sys/fs/cgroup/docker/${FULL_ID}/cpu.stat" 2>/dev/null)
  if [ -n "${v:-}" ]; then echo "$v host-cgroup-v2-cgroupfs"; return 0; fi

  # cgroup v1: nanoseconds, convert to microseconds.
  local ns
  ns=$(cat "/sys/fs/cgroup/cpuacct/docker/${FULL_ID}/cpuacct.usage" 2>/dev/null)
  if [ -n "${ns:-}" ]; then echo "$((ns / 1000)) host-cgroup-v1"; return 0; fi

  return 1
}

BEFORE_RAW=$(read_cpu_usec)
BEFORE=${BEFORE_RAW%% *}
CPU_SOURCE=${BEFORE_RAW#* }
if [ -z "${BEFORE:-}" ]; then
  echo "ERROR: could not read CPU accounting for container $CONTAINER." >&2
  echo "Fallback: run 'docker stats $CONTAINER' in another terminal and eyeball" >&2
  echo "average CPU% during the run, then CPU-s/job = (avg% / 100) * wall / N." >&2
  exit 1
fi

echo "container   : $CONTAINER (${FULL_ID:0:12})"
echo "cpu source  : $CPU_SOURCE"
echo "jobs        : $N sequential"
echo

REQ='{"language":"java","version":"*","files":[{"name":"Main","content":"public class Main { public static void main(String[] a) { System.out.println(\"OK-cpuprobe\"); } }"}]}'

OK=0
KILLED=0
START=$(date +%s.%N)

for i in $(seq 1 "$N"); do
  RESP=$(curl -s -m 120 -X POST "http://localhost:${PORT}/api/v2/execute" \
    -H 'Content-Type: application/json' -d "$REQ")
  if printf '%s' "$RESP" | grep -q 'OK-cpuprobe'; then
    OK=$((OK + 1))
  else
    KILLED=$((KILLED + 1))
    # First failure is worth seeing in full: a stock 3s run_timeout that cannot
    # even fit an unloaded hello-world changes the whole picture.
    if [ "$KILLED" -eq 1 ]; then
      echo "  !! job $i did not print its marker. Raw response:" >&2
      printf '     %s\n' "$RESP" >&2
    fi
  fi
  printf '\r  progress: %d/%d  ok=%d killed=%d' "$i" "$N" "$OK" "$KILLED"
done

END=$(date +%s.%N)
AFTER_RAW=$(read_cpu_usec)
AFTER=${AFTER_RAW%% *}
echo; echo

awk -v before="$BEFORE" -v after="$AFTER" -v n="$N" -v start="$START" -v end="$END" \
    -v ok="$OK" -v killed="$KILLED" '
BEGIN {
  cpu = (after - before) / 1000000.0
  wall = end - start
  per = cpu / n
  printf "  total CPU consumed : %.2f s\n", cpu
  printf "  total wall time    : %.2f s\n", wall
  printf "  jobs ok / killed   : %d / %d\n", ok, killed
  printf "\n  >>> C = CPU-SECONDS PER JOB : %.2f <<<\n\n", per
  printf "  sequential latency : %.2f s per job\n", wall / n
  printf "  parallel efficiency: %.0f%% (CPU-s / wall-s at concurrency 1)\n\n", (cpu / wall) * 100

  printf "  Cores needed on this host, from C = %.2f:\n", per
  printf "    %-34s %.1f\n", "burst of 80, drain <= 30s :", (80 * per) / (30 * 0.85)
  printf "    %-34s %.1f\n", "burst of 80, drain <= 60s :", (80 * per) / (60 * 0.85)
  printf "    %-34s %.1f\n", "burst of 20 (staggered), <=30s :", (20 * per) / (30 * 0.85)
  printf "    %-34s %.1f\n", "steady state at rho <= 0.5 :", 2 * 1.07 * per / 0.85
  printf "\n  Verdict = MAX of the burst row you accept and the steady row.\n"
  printf "  Compare against the core count this VM actually has.\n"
}'
