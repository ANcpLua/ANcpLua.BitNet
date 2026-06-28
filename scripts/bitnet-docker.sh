#!/usr/bin/env bash
# scripts/bitnet-docker.sh
#
# Start (or stop) the Microsoft-published BitNet b1.58-2B-4T inference server via Docker,
# idempotently. Pinned by digest for reproducibility — re-resolve with
# `docker buildx imagetools inspect ...` if you intentionally want a newer build.
#
# The OpenAI-compatible API lands at http://localhost:11434/v1/chat/completions — exactly
# what ANcpLua.Agents.Hosting.BitNet's IChatClient (and the BitNetFixture) probe.
#
# Usage:
#   scripts/bitnet-docker.sh up        # idempotent: no-op if already running + healthy
#   scripts/bitnet-docker.sh down      # idempotent: no-op if nothing is running
#   scripts/bitnet-docker.sh status
#   (start|stop are accepted as aliases for up|down)
#
# Env overrides:
#   BITNET_PORT             (default 11434) — host port to bind
#   BITNET_CONTAINER        (default bitnet) — container name
#   BITNET_HEALTH_DEADLINE  (default 60) — seconds to wait for /health
#   BITNET_IMAGE            — override the pinned image (e.g. a newer rebuild)
set -euo pipefail

# Pinned digest captured 2026-05-12 from the public mcr.microsoft.com registry. The tag
# `:bitnet-b1.58-2b-4t-gguf` is mutable and Microsoft rebuilds on its own cadence; pin here
# so this script reproduces byte-identical runs. Mirrors BitNetFixture.DockerImage and
# build/ANcpLua.Agents.Testing.BitNet.props (CI asserts the three stay in sync).
IMAGE="${BITNET_IMAGE:-mcr.microsoft.com/appsvc/docs/sidecars/sample-experiment@sha256:9d5f7f4e6e5a456b40582f7b00a70a5e2a4637c37f0976bfcffd1ed252cd243a}"
TAG_FOR_LOGS="mcr.microsoft.com/appsvc/docs/sidecars/sample-experiment:bitnet-b1.58-2b-4t-gguf"

PORT="${BITNET_PORT:-11434}"
NAME="${BITNET_CONTAINER:-bitnet}"
HEALTH_DEADLINE="${BITNET_HEALTH_DEADLINE:-60}"

ACTION="${1:-up}"
case "$ACTION" in
  start) ACTION=up ;;
  stop)  ACTION=down ;;
esac

command -v docker >/dev/null 2>&1 || {
  echo "[bitnet-docker] docker not found on PATH" >&2
  exit 2
}

# `up` polls /health with curl; without it, every loop iteration silently fails inside the `if`
# (set -e ignores conditional-context failures) and the script reports a misleading timeout.
if [ "$ACTION" = "up" ] && ! command -v curl >/dev/null 2>&1; then
  echo "[bitnet-docker] curl not found on PATH — required for /health readiness checks" >&2
  exit 2
fi

is_running() { [ "$(docker inspect -f '{{.State.Running}}' "$NAME" 2>/dev/null)" = "true" ]; }
health_ok()  { curl -fsS "http://localhost:$PORT/health" >/dev/null 2>&1; }

case "$ACTION" in
  up)
    # Idempotent: already running AND answering /health -> leave it alone.
    if is_running && health_ok; then
      echo "[bitnet-docker] '$NAME' already up + healthy on :$PORT — nothing to do"
      echo "  export BITNET_URL=http://localhost:$PORT"
      exit 0
    fi

    # A container by that name exists but isn't healthy (stopped, crashed, mid-boot from a
    # previous aborted run) — clear it so the fresh run owns the name and the port.
    if docker inspect "$NAME" >/dev/null 2>&1; then
      echo "[bitnet-docker] clearing stale/unhealthy container '$NAME'"
      docker rm -f "$NAME" >/dev/null
    fi

    # Separate the (large, slow) image transfer from the /health budget below — a cold pull
    # alone can exceed the deadline. `docker image inspect` is a fast local lookup.
    if ! docker image inspect "$IMAGE" >/dev/null 2>&1; then
      echo "[bitnet-docker] pulling $TAG_FOR_LOGS (digest-pinned)..."
      docker pull "$IMAGE" >/dev/null
    fi

    echo "[bitnet-docker] starting $TAG_FOR_LOGS on :$PORT"
    docker run -d --rm --name "$NAME" -p "$PORT:11434" "$IMAGE" >/dev/null

    echo "[bitnet-docker] waiting for /health (${HEALTH_DEADLINE}s deadline)..."
    for i in $(seq 1 "$HEALTH_DEADLINE"); do
      if health_ok; then
        echo "[bitnet-docker] /health OK after ${i}s"
        echo "  export BITNET_URL=http://localhost:$PORT"
        exit 0
      fi
      sleep 1
    done

    echo "[bitnet-docker] FAIL: /health never returned 200 in ${HEALTH_DEADLINE}s — container logs:" >&2
    docker logs "$NAME" 2>&1 | tail -40 >&2
    exit 3
    ;;

  down)
    if docker inspect "$NAME" >/dev/null 2>&1; then
      echo "[bitnet-docker] removing container '$NAME'"
      docker rm -f "$NAME" >/dev/null
    else
      echo "[bitnet-docker] no container '$NAME' — nothing to do"
    fi
    ;;

  status)
    if is_running; then
      docker inspect "$NAME" --format '{{.State.Status}} on :{{(index (index .NetworkSettings.Ports "11434/tcp") 0).HostPort}} ({{.Image}})'
    else
      echo "not running"
      exit 1
    fi
    ;;

  *)
    echo "[bitnet-docker] unknown action: $ACTION (use up|down|status)" >&2
    exit 2
    ;;
esac
