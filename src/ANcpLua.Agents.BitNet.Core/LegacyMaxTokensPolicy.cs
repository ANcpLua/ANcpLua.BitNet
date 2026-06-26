using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json.Nodes;
using ANcpLua.Roslyn.Utilities;

namespace ANcpLua.Agents.Hosting.BitNet;

/// <summary>
///     Mirrors <c>max_completion_tokens</c> → <c>max_tokens</c> in the outbound
///     chat-completion JSON body. Older OpenAI-compatible servers (anything before
///     ggml-org/llama.cpp PR #19831, merged 2026-02-23) only honor the legacy
///     <c>max_tokens</c> field; the .NET SDK only emits the new one. Without this
///     mirror the server ignores the cap and generates until the context fills.
/// </summary>
/// <remarks>
///     Registered on <c>OpenAIClientOptions</c> via the inherited
///     <c>ClientPipelineOptions.AddPolicy</c> hook so the policy runs after the
///     SDK has serialized the <c>PipelineRequest.Content</c> — we parse the final
///     JSON body, copy the field, and rewrite the content. The async path uses
///     <c>WriteToAsync</c> end-to-end so the pipeline never blocks on sync I/O.
///     Self-deleting: once the target server accepts <c>max_completion_tokens</c>
///     natively, this becomes a no-op.
/// </remarks>
public sealed class LegacyMaxTokensPolicy : PipelinePolicy
{
    /// <inheritdoc />
    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        Guard.NotNull(message);
        Guard.NotNull(pipeline);

        Mirror(message);
        ProcessNext(message, pipeline, currentIndex);
    }

    /// <inheritdoc />
    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        Guard.NotNull(message);
        Guard.NotNull(pipeline);

        await MirrorAsync(message).ConfigureAwait(false);
        await ProcessNextAsync(message, pipeline, currentIndex).ConfigureAwait(false);
    }

    private static void Mirror(PipelineMessage message)
    {
        if (!ShouldMirror(message, out var content)) return;
        using var buffer = new MemoryStream();
        content.WriteTo(buffer, default);
        Apply(message, buffer);
    }

    private static async ValueTask MirrorAsync(PipelineMessage message)
    {
        if (!ShouldMirror(message, out var content)) return;
        await using var buffer = new MemoryStream();
        await content.WriteToAsync(buffer, message.CancellationToken).ConfigureAwait(false);
        Apply(message, buffer);
    }

    private static bool ShouldMirror(PipelineMessage message, out BinaryContent content)
    {
        content = null!;
        if (message.Request?.Content is not { } c) return false;
        // OpenAI's spec defines the path as `/v1/chat/completions` (lowercase),
        // and llama-server preserves casing; an ordinal compare is sufficient.
        if (message.Request.Uri?.AbsolutePath?.EndsWithOrdinal("/chat/completions") is not true) return false;
        content = c;
        return true;
    }

    private static void Apply(PipelineMessage message, MemoryStream buffer)
    {
        buffer.Position = 0;
        if (JsonNode.Parse(buffer) is not JsonObject body) return;
        if (body["max_tokens"] is null && body["max_completion_tokens"] is { } mct)
        {
            body["max_tokens"] = mct.DeepClone();
            message.Request.Content = BinaryContent.Create(BinaryData.FromString(body.ToJsonString()));
        }
    }
}
