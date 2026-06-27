namespace ANcpLua.Agents.Testing.BitNet;

/// <summary>
///     Marks a test class as requiring the shared <see cref="BitNetFixture" />. The bundled generator
///     (<c>ANcpLua.Agents.Testing.BitNet.Generators</c>) emits the xUnit v3 wiring for the class: the
///     <c>[Collection]</c> attribute, the fixture-injecting constructor, a <c>BitNet</c> accessor, and a
///     <c>SkipUnlessBitNetAvailable()</c> helper — so the class itself stays boilerplate-free.
/// </summary>
/// <remarks>
///     <para>The target must be a non-generic, top-level <c>partial class</c> with no hand-written
///     constructor (the generator owns it; <c>BITNET001</c>–<c>BITNET004</c> diagnose violations).
///     Example:</para>
///     <code>
///     [BitNet]
///     public partial class ChatTests
///     {
///         [Fact]
///         public async Task Answers()
///         {
///             SkipUnlessBitNetAvailable();
///             var reply = await BitNet.ChatClient!.GetResponseAsync("ping");
///             Assert.NotNull(reply);
///         }
///     }
///     </code>
///     <para>All <c>[BitNet]</c> classes share one container by default (the <c>"BitNet"</c> collection,
///     defined by <see cref="BitNetTestGroup" />) — BitNet is a single-slot, slow server, so one shared
///     instance per run is the sane default. Set <see cref="Collection" /> to route a class to a
///     different <c>ICollectionFixture&lt;BitNetFixture&gt;</c> collection you have defined.</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
public sealed class BitNetAttribute : Attribute
{
    /// <summary>
    ///     Name of the xUnit collection the generated wiring joins. Defaults to <c>"BitNet"</c>. The
    ///     generator also emits the matching
    ///     <c>[CollectionDefinition(name)] : ICollectionFixture&lt;BitNetFixture&gt;</c> for whatever
    ///     name you set, so you never hand-write one. Use a custom name to give a subset of classes
    ///     their own shared <see cref="BitNetFixture" /> instance instead of the default single one.
    /// </summary>
    public string Collection { get; set; } = "BitNet";
}
