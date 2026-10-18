using System.Reflection;
using System.Text.Json;
using CrowdSim.Migration;
using Xunit;

namespace CrowdSim.Migration.Tests;

public sealed class FoundationContractTests
{
    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrowdSim.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    [Theory]
    [InlineData("6000.3.26f1", true)]
    [InlineData("6000.3.25f1", false)]
    [InlineData("6000.3.26f1-extra", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void EditorCompatibilityRequiresTheApprovedPatch(string? version, bool expected)
        => Assert.Equal(expected, MigrationContract.IsExpectedEditor(version!));

    [Fact]
    public void PortableContractTargetsStandardAndDoesNotReferenceTheReferenceEngineOrUnity()
    {
        Assembly assembly = typeof(MigrationContract).Assembly;
        Assert.Contains(assembly.GetCustomAttributesData(), a =>
            a.AttributeType.Name == "TargetFrameworkAttribute" &&
            (string)a.ConstructorArguments[0].Value! == ".NETStandard,Version=v2.1");
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a =>
            a.Name!.StartsWith("Unity", StringComparison.Ordinal) || a.Name == "CrowdSim.Core");
    }

    [Fact]
    public void DownloadedEditorAndPipelinePinsAgreeWithRuntimeContract()
    {
        string project = Path.Combine(RepositoryRoot(), "UnityProject");
        Assert.Contains("m_EditorVersion: " + MigrationContract.EditorVersion,
            File.ReadAllText(Path.Combine(project, "ProjectSettings", "ProjectVersion.txt")));
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(project, "Packages", "manifest.json")));
        Assert.Equal(MigrationContract.UrpVersion,
            manifest.RootElement.GetProperty("dependencies").GetProperty("com.unity.render-pipelines.universal").GetString());
        Assert.True(File.Exists(Path.Combine(project, MigrationContract.BootstrapScene)));
    }
}
