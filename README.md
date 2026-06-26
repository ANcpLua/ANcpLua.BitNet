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

## Status

Early — see [`TODO.md`](TODO.md). The hosting + generator packages build and pack; the testing
fixture builds. Declarative `[BitNet]`-driven fixture generation, a fully idempotent Makefile/MSBuild
entry, smoke tests, and NuGet trusted publishing are the next increments.
