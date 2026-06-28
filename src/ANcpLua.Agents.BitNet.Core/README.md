# ANcpLua.Agents.BitNet.Core

The **client core** for the BitNet (`bitnet.cpp` / `llama-server`) stack — the
shared base that both [`ANcpLua.Agents.Hosting.BitNet`](https://www.nuget.org/packages/ANcpLua.Agents.Hosting.BitNet)
and [`ANcpLua.Agents.Testing.BitNet`](https://www.nuget.org/packages/ANcpLua.Agents.Testing.BitNet)
build on, with **no hosting stack and no provider SDK** — it speaks the OpenAI-compatible wire
protocol directly over `HttpClient` + `System.Text.Json`, exposing only the vendor-neutral
`IChatClient`.

| Type | What it does |
|---|---|
| `BitNetClientOptions` | Endpoint / API-path / model config, with `BITNET_URL` · `BITNET_API_PATH` · `BITNET_MODEL` environment overrides. |
| `BitNetChatClient` | A dependency-free `IChatClient` that POSTs straight to the OpenAI-compatible `/chat/completions` endpoint. No provider SDK; emits `max_tokens` natively. |
| `BitNetChatClientFactory` | `Create(options)` → a configured `BitNetChatClient` as `IChatClient`. |
| `LegacyMaxTokensPolicy` | **Obsolete.** Was an OpenAI-SDK pipeline shim mirroring `max_completion_tokens` → `max_tokens`; the agnostic client emits `max_tokens` natively, so it is unused. Kept for compatibility. |

```csharp
using ANcpLua.Agents.Hosting.BitNet;
using Microsoft.Extensions.AI;

IChatClient client = BitNetChatClientFactory.Create(new BitNetClientOptions
{
    Endpoint = new Uri("http://localhost:8080"),
    Model    = "bitnet-b1.58-2B-4T",
});
```

> The types live in the `ANcpLua.Agents.Hosting.BitNet` namespace for API stability (shipped that
> way in `4.0.0-preview.1`); this package is the assembly split-out so the client core can be taken
> without the hosting stack.

**BitNet is a local test-double / system-under-test — not an authoritative judge.** See
[`docs/bitnet-not-a-judge.md`](https://github.com/ANcpLua/ANcpLua.BitNet/blob/main/docs/bitnet-not-a-judge.md).
