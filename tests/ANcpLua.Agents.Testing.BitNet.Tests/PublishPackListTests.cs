using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ANcpLua.Agents.Testing.BitNet.Tests;

/// <summary>
///     Keeps the two hand-maintained pack lists — the publish workflow's Pack step and the
///     Makefile's pack target — honest against the packable projects that actually exist.
/// </summary>
/// <remarks>
///     A per-project pack list is deliberate (a new project must not reach nuget.org just by
///     being added to the solution), but its failure mode is silent: a packable project nobody
///     added to the list builds and tests forever without ever publishing. That happened to
///     ANcpLua.Agents.Evaluation in the sibling ANcpLua.Agents repo (v1.13.9 shipped seven of
///     eight packages); these tests are that incident's guard, applied here — where there are
///     TWO lists that can drift.
/// </remarks>
public sealed partial class PublishPackListTests
{
    private static readonly string s_repoRoot = LocateRepoRoot();

    [Fact]
    public void Workflow_pack_step_covers_every_packable_source_project()
    {
        var packable = PackableProjectPaths();
        var packed = PackLines(File.ReadAllText(Path.Combine(s_repoRoot, ".github", "workflows", "nuget-publish.yml")));

        Assert.Equal(packable, packed);
    }

    [Fact]
    public void Makefile_pack_target_covers_every_packable_source_project()
    {
        var packable = PackableProjectPaths();
        var packed = PackLines(File.ReadAllText(Path.Combine(s_repoRoot, "Makefile")));

        Assert.Equal(packable, packed);
    }

    /// <summary>Every src/**.csproj that declares a PackageId and is not opted out of packing.</summary>
    private static string[] PackableProjectPaths() =>
        [.. Directory.EnumerateFiles(Path.Combine(s_repoRoot, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(static path =>
            {
                var project = XDocument.Load(path).Root!;
                bool hasPackageId = project.Descendants("PackageId").Any();
                bool optedOut = string.Equals(
                    project.Descendants("IsPackable").FirstOrDefault()?.Value,
                    "false",
                    StringComparison.OrdinalIgnoreCase);
                return hasPackageId && !optedOut;
            })
            .Select(static path => Path.GetRelativePath(s_repoRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)];

    private static string[] PackLines(string text) =>
        [.. PackCommandRegex().Matches(text)
            .Select(static match => match.Groups["path"].Value)
            .Order(StringComparer.Ordinal)];

    [GeneratedRegex(@"dotnet pack\s+(?<path>src/[^\s]+\.csproj)")]
    private static partial Regex PackCommandRegex();

    private static string LocateRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ANcpLua.BitNet.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the ANcpLua.BitNet repository root.");
    }
}
