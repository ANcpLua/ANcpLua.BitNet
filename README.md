# ANcpLua.BitNet

A clean, publishable home for the **BitNet** (`bitnet.cpp` / `llama-server`) integration
stack — hosting facade, Roslyn source generator, and an xUnit test fixture — re-homed out
of `ANcpLua.Agents` (which deliberately purged provider facades to stay an instrumentation
core) so the already-published packages stop being orphaned.

| Package | What it is |
|---|---|
| `ANcpLua.Agents.Hosting.BitNet` | OpenAI-compatible `IChatClient` over `bitnet.cpp` `llama-server`: keyed DI registration, health check, and a `LegacyMaxTokensPolicy` shim. Bundles a Roslyn generator that wires `[assembly: BitNetEndpoint(...)]` into `AddDiscoveredBitNetClients`. |
| `ANcpLua.Agents.Hosting.BitNet.Generators` | The incremental generator (ships inside the hosting package's `analyzers/`). |
| `ANcpLua.Agents.Testing.BitNet` | xUnit v3 `BitNetFixture` that auto-manages a **digest-pinned**, idempotent (inspect-before-pull) BitNet Docker container and exposes an `IChatClient`. |

## The honest part (why this is publishable with a clear conscience)

**BitNet is a local test-double / system-under-test — not an authoritative judge.** A 1.58-bit
2B model is below the reliable LLM-as-judge threshold (~≥14B), and on emulated CPU it is
impractically slow for per-evaluation judging. We measured this directly — see
[`docs/bitnet-not-a-judge.md`](docs/bitnet-not-a-judge.md). This stack exists to make BitNet
*easy to stand up as a cheap local model under test*, never to force it into a role it can't fill.

## Running BitNet locally

The `BitNetFixture` auto-manages the digest-pinned container for you, but you can also drive it by
hand. Three equivalent, **idempotent** entry points (each a no-op if the server is already up):

```bash
make bitnet-up        # start (or scripts/bitnet-docker.sh up)
make bitnet-status
make bitnet-down
make test             # tests, Docker-gated (skip cleanly without a server)
make test-live        # bring BitNet up, then run the auto-Docker round-trip smoke test
```

Consumers of `ANcpLua.Agents.Testing.BitNet` get the same via auto-imported MSBuild targets — no
shell script needed:

```bash
dotnet build -t:BitNetUp      # also BitNetStatus / BitNetDown
```

The pinned image digest lives in exactly one logical place (mirrored across `BitNetFixture`,
`scripts/bitnet-docker.sh`, and the package's `build/*.props`); CI fails if the three ever drift.

## Status

Published preview on nuget.org (`4.0.0-preview.*`, keyless trusted publishing). Done: the four
packages, the declarative `[BitNet]` fixture generator, smoke tests, and the idempotent
Makefile / MSBuild entry points. Remaining (see [`TODO.md`](TODO.md)): per-class
`[BitNet(Port=…, Model=…)]` config (v2) and re-homing the `ANcpLua.NET.Sdk.BitNet` meta-package.
