#!/usr/bin/env bash
# One-shot local dev entrypoint: brings up Postgres + Piston in Docker,
# points the API at them, and runs the backend. Stops the containers again
# (without removing them, so data survives) when the backend exits.
#
#   ./scripts/run-dev.sh
set -euo pipefail

cd "$(dirname "$0")/.."

export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=bootit;Username=postgres;Password=postgres"
export Piston__BaseUrl="http://localhost:2000/"

trap 'echo "==> Stopping local Postgres + Piston containers"; docker compose stop' EXIT

echo "==> Starting local Postgres + Piston containers"
docker compose up -d

echo "==> Running backend (ConnectionStrings__DefaultConnection -> local Postgres, Piston__BaseUrl -> $Piston__BaseUrl)"
dotnet run --project cobblersBackend
