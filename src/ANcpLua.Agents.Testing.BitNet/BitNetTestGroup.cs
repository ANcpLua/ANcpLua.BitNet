using Xunit;

namespace ANcpLua.Agents.Testing.BitNet;

/// <summary>
///     Reference xUnit v3 collection definition for the shared BitNet server fixture.
/// </summary>
/// <remarks>
///     xUnit only discovers <c>[CollectionDefinition]</c> classes inside the <em>test</em> assembly,
///     not in a referenced library — so this type is a template, not something the runner picks up on
///     your behalf. With <c>[BitNet]</c> you do not need it: the bundled generator emits an equivalent
///     <c>[CollectionDefinition("BitNet")] : ICollectionFixture&lt;BitNetFixture&gt;</c> straight into
///     your test assembly. Define your own copy only if you wire <c>[Collection("BitNet")]</c> by hand
///     without the generator.
/// </remarks>
[CollectionDefinition("BitNet")]
public sealed class BitNetTestGroup : ICollectionFixture<BitNetFixture>;
