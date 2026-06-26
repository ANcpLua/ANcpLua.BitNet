# ANcpLua.Agents.BitNet.Core

The **dependency-free client core** for the BitNet (`bitnet.cpp` / `llama-server`) stack — the
shared base that both [`ANcpLua.Agents.Hosting.BitNet`](https://www.nuget.org/packages/ANcpLua.Agents.Hosting.BitNet)
and [`ANcpLua.Agents.Testing.BitNet`](https://www.nuget.org/packages/ANcpLua.Agents.Testing.BitNet)
build on, with **no hosting / ASP.NET Core dependency**.

| Type | What it does |
|---|---|
| `BitNetClientOptions` | Endpoint / API-path / model config, with `BITNET_URL` · `BITNET_API_PATH` · `BITNET_MODEL` environment overrides. |
| `BitNetChatClientFactory` | `Create(options)` → an OpenAI-compatible `IChatClient` bound to the `llama-server` endpoint. |
| `LegacyMaxTokensPolicy` | Mirrors `max_completion_tokens` → `max_tokens` for `llama-server` builds before ggml-org/llama.cpp PR #19831. Self-deleting once the server honors the new field. |

```csharp
using ANcpLua.Agents.Hosting.BitNet;

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
