# ANcpLua.Agents.Hosting.BitNet

Consumer toolkit for Microsoft Agent Framework — local-LLM hosting via Microsoft's [BitNet b1.58](https://github.com/microsoft/BitNet) over the OpenAI-compatible `/v1` surface.

Alpha-channel package. Keep isolated from stable/preview consumers unless explicitly intended.

- Compatible with: Microsoft.Agents.AI 1.8.x
- Tested against: Microsoft.Agents.AI 1.8.0 + Microsoft.Extensions.AI 10.6.x
- Capability tested against: BitNet b1.58 2B-4T weights served by Microsoft's prebuilt `bitnet.cpp` Docker image

> **Naming:** `*` = consumer-facing facade / entry-point, bare = primitive consumers may compose with. See [the convention in ANcpLua.Agents](../ANcpLua.Agents/README.md#naming-convention).

## Install

```sh
dotnet add package ANcpLua.Agents.Hosting.BitNet
```

Or add the `PackageReference` by hand:

```xml
<PackageReference Include="ANcpLua.Agents.Hosting.BitNet" />
```

Under Central Package Management (recommended; required by `ANcpLua.NET.Sdk`), pin the version in your `Directory.Packages.props` and leave the `Version=` off the `PackageReference` itself.

## Standing up a BitNet server

The hosting package only speaks HTTP to an OpenAI-compatible endpoint — it never builds, downloads, or spawns the binary. You can satisfy that contract any way you like; Microsoft's prebuilt Docker image is the easiest path:

```sh
make bitnet-up                    # idempotent — stops any prior container first
export BITNET_URL=http://localhost:11434
```

`make bitnet-up` wraps `scripts/bitnet-docker.sh start`. The script pins the image by digest (`sha256:9d5f7f4e...cd243a` as of 2026-05-12), so byte-identical runs are guaranteed until you intentionally re-resolve.

What it bundles: `bitnet.cpp`, the `b1.58-2B-4T` GGUF weights, the patched `llama-server`, all under `/v1/chat/completions` on port 11434. No Python, cmake, LUT codegen, or `git clone` involved.

If your environment cannot pull this image (air-gapped hosts, vendor mirrors, custom builds), point `BITNET_URL` at any other OpenAI-compatible `/v1/chat/completions` endpoint — LM Studio, vLLM with a BitNet build, your own `llama-server` build, a private inference gateway. The hosting package does not care how the server got there. See *Other OpenAI-compatible servers* below.

Health check:

```sh
curl -fsS http://localhost:11434/health && echo ok
```

Stop:

```sh
make bitnet-down
```

### Pinning by digest (production / supply-chain)

`make bitnet-up` already runs digest-pinned. If you want to re-resolve to a newer Microsoft build, the recipe is:

```sh
docker buildx imagetools inspect \
  mcr.microsoft.com/appsvc/docs/sidecars/sample-experiment:bitnet-b1.58-2b-4t-gguf \
  --format '{{json .Manifest.Digest}}'
```

Edit the `IMAGE=` constant at the top of `scripts/bitnet-docker.sh` with the new digest, commit, done.

### Other OpenAI-compatible servers

LM Studio, vLLM with a BitNet build, a private inference gateway, your own fork — all work. Point `BITNET_URL` at them.

**Note on Ollama**: as of mid-2026 Ollama does not run BitNet b1.58 (upstream `llama.cpp` lacks the `i2_s` tensor type that BitNet requires; Ollama inherits that gap — see ollama/ollama#10334).

## Why a dedicated hosting package

Stock `Microsoft.Extensions.AI.OpenAI` works against any OpenAI-compatible endpoint — but `llama-server` builds older than [ggml-org/llama.cpp#19831](https://github.com/ggml-org/llama.cpp/pull/19831) (merged 2026-02-23) silently ignore the SDK-emitted `max_completion_tokens` field and generate to the context limit. This package promotes the `LegacyMaxTokensPolicy` shim out of test-only territory and ships it as part of the runtime path. Once Microsoft's BitNet fork picks up the upstream merge, the policy becomes a no-op self-deleting decorator.

## Usage — four modes

### Mode 0 — zero config (defaults to `BITNET_URL`)

```csharp
builder.AddBitNetChatClient();   // connection name defaults to "bitnet"
```

Resolves:

```csharp
public sealed class MyAgent([FromKeyedServices("bitnet")] IChatClient chat) { ... }
```

### Mode 1 — Aspire-style named connection

```csharp
builder.AddBitNetChatClient("bitnet");
// reads ConnectionStrings:bitnet = "http://localhost:11434"
```

### Mode 2 — programmatic configuration (keyed multi-endpoint)

```csharp
builder.AddBitNetChatClient("local", o =>
{
    o.Endpoint = new Uri("http://localhost:11434");
    o.Model    = "bitnet-b1.58-2B-4T";
    o.ApiPath  = "/v1";
});
```

### Mode 3 — assembly attribute + bundled source generator

```csharp
[assembly: BitNetEndpoint("bitnet", "http://localhost:11434", Model = "bitnet-b1.58-2B-4T")]

// Program.cs — one call wires every declared endpoint
builder.AddDiscoveredBitNetClients();
```

## What gets registered

- `IChatClient` keyed by the connection name (singleton)
- `IOptions<BitNetClientOptions>` bound to `BitNet:<name>:*` config section
- Health check named `bitnet:<name>` against `<endpoint>/health`
- OpenTelemetry decoration via `Microsoft.Extensions.AI.UseOpenTelemetry()` — emits standard `gen_ai.*` spans/meters

## Environment overrides

For test scenarios and the `BitNetFixture` contract (`ANcpLua.Agents.Testing.BitNet.BitNetFixture`):

- `BITNET_URL` — overrides `Endpoint`. When unset, the fixture auto-starts the Docker image itself (unless `BITNET_FIXTURE_NO_DOCKER` is truthy).
- `BITNET_API_PATH` — overrides `ApiPath` (default `/v1`)
- `BITNET_MODEL` — overrides `Model` (default `bitnet-b1.58-2B-4T`)
- `BITNET_FIXTURE_NO_DOCKER` — set truthy to opt the fixture out of auto-Docker

Env vars win over config so existing fixture consumers keep working.

Smoke-test the auto-Docker path with `make smoke` (sets `BITNET_SMOKE_TEST=1` and uses the Microsoft.Testing.Platform-correct `--filter-method` flag — `global.json` pins MTP as the runner, so legacy VSTest flags like `--filter` / `--logger` are rejected).

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `BitNet endpoint is not configured` at startup | `BITNET_URL` not set and no `ConnectionStrings:<name>` / `BitNet:<name>:Endpoint` bound | Run `make bitnet-up` then `export BITNET_URL=http://localhost:11434`, or pass `configure: o => o.Endpoint = ...` |
| Health check `bitnet:<name>` is Unhealthy | server not listening, or wrong port | `curl $BITNET_URL/health` to confirm; check container with `make bitnet-status` |
| Model generates until context fills, ignores token cap | running against a `llama-server` older than llama.cpp PR #19831 without our shim | confirm `Microsoft.Extensions.AI.OpenAI` is going through `BitNetChatClientFactory.Create` — the `LegacyMaxTokensPolicy` is applied there automatically |
| `Unexpected end of input` / GGUF errors on stock Ollama | Ollama uses upstream llama.cpp which lacks `i2_s` | Ollama is not a target — use the Docker image |
