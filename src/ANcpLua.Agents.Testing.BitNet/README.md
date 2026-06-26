# ANcpLua.Agents.Testing.BitNet

xUnit v3 fixture that auto-manages a local **BitNet** (`bitnet.cpp` `llama-server`) Docker
container and exposes an `IChatClient` for tests.

```csharp
[Collection("BitNet")]
public sealed class MyTests(BitNetFixture bitnet)
{
    [Fact]
    public async Task Generates()
    {
        Assert.SkipUnless(bitnet.IsAvailable, "BitNet not running");
        var response = await bitnet.ChatClient!.GetResponseAsync("hello");
        Assert.NotNull(response.Text);
    }
}
```

Endpoint discovery: `BITNET_URL` → auto-managed digest-pinned Docker container
(inspect-before-pull, `/health` wait) → legacy `http://localhost:8080`.

> **BitNet is a local test-double / system-under-test — not an authoritative judge.**
> A 1.58-bit 2B model is below the reliable LLM-as-judge threshold (~≥14B) and, on
> emulated CPU, impractically slow. See the repo's `docs/bitnet-not-a-judge.md`.
