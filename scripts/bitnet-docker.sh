#!/usr/bin/env bash
# scripts/bitnet-docker.sh
#
# Start (or stop) the Microsoft-published BitNet b1.58-2B-4T inference server
# via Docker. Pinned by digest for reproducibility — re-resolve with
# `docker buildx imagetools inspect ...` if you intentionally want a newer build.
#
# The OpenAI-compatible API lands at http://localhost:11434/v1/chat/completions
# — exactly what ANcpLua.Agents.Hosting.BitNet's IChatClient probes.
#
# Usage:
#   scripts/bitnet-docker.sh start      # default
#   scripts/bitnet-docker.sh stop
#   scripts/bitnet-docker.sh status
#
# Env vars:
#   BITNET_PORT (default 11434) — host port to bind
#   BITNET_CONTAINER (default bitnet) — container name
set -euo pipefail

# Pinned digest captured 2026-05-12 from the public mcr.microsoft.com registry.
# The tag `:bitnet-b1.58-2b-4t-gguf` is mutable and Microsoft rebuilds on its own
# cadence; pin here so this script reproduces byte-identical runs over time.
IMAGE="mcr.microsoft.com/appsvc/docs/sidecars/sample-experiment@sha256:9d5f7f4e6e5a456b40582f7b00a70a5e2a4637c37f0976bfcffd1ed252cd243a"
TAG_FOR_LOGS="mcr.microsoft.com/appsvc/docs/sidecars/sample-experiment:bitnet-b1.58-2b-4t-gguf"

PORT="${BITNET_PORT:-11434}"
NAME="${BITNET_CONTAINER:-bitnet}"
ACTION="${1:-start}"

command -v docker >/dev/null 2>&1 || {
  echo "[bitnet-docker] docker not found on PATH" >&2
  exit 2
}

# `start` polls /health with curl; without it, every loop iteration silently fails inside the `if`
# (set -e ignores conditional-context failures), then the script reports a misleading 60-second
# /health timeout. Fail fast instead so the operator sees the real cause.
if [ "${1:-start}" = "start" ] && ! command -v curl >/dev/null 2>&1; then
  echo "[bitnet-docker] curl not found on PATH — required for /health readiness checks" >&2
  exit 2
fi

case "$ACTION" in
  start)
    if docker inspect "$NAME" >/dev/null 2>&1; then
      echo "[bitnet-docker] container '$NAME' exists — removing first"
      docker rm -f "$NAME" >/dev/null
    fi
    echo "[bitnet-docker] starting $TAG_FOR_LOGS (digest-pinned) on :$PORT"
    docker run -d --rm --name "$NAME" -p "$PORT:11434" "$IMAGE" >/dev/null

    echo "[bitnet-docker] waiting for /health (60s deadline)..."
    for i in $(seq 1 60); do
      if curl -fsS "http://localhost:$PORT/health" >/dev/null 2>&1; then
        echo "[bitnet-docker] /health OK after ${i}s"
        echo
        echo "[bitnet-docker] export this for the fixture / SDK consumers:"
        echo "  export BITNET_URL=http://localhost:$PORT"
        exit 0
      fi
      sleep 1
    done
    echo "[bitnet-docker] FAIL: /health never returned 200 in 60s — container logs:" >&2
    docker logs "$NAME" 2>&1 | tail -40 >&2
    exit 3
    ;;

  stop)
    if docker inspect "$NAME" >/dev/null 2>&1; then
      echo "[bitnet-docker] stopping container '$NAME'"
      docker stop "$NAME" >/dev/null
    else
      echo "[bitnet-docker] no container '$NAME' running"
    fi
    ;;

  status)
    if docker inspect "$NAME" >/dev/null 2>&1; then
      docker inspect "$NAME" --format '{{.State.Status}} on :{{(index (index .NetworkSettings.Ports "11434/tcp") 0).HostPort}} ({{.Image}})'
    else
      echo "not running"
      exit 1
    fi
    ;;

  *)
    echo "[bitnet-docker] unknown action: $ACTION (use start|stop|status)" >&2
    exit 2
    ;;
esac
