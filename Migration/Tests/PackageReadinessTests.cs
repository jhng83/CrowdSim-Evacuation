using CrowdSim.Unity.Editor;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CrowdSim.Migration.Tests;

public sealed class PackageReadinessTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "crowdsim-package-check-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void CompletePinnedAndEmbeddedMetadataPassWithoutChangingFiles()
    {
        var input = Installed("com.unity.inputsystem", "1.14.2");
        var core = Installed("com.crowdsim.core", "0.11.1");
        string before = File.ReadAllText(Path.Combine(input.Path, "package.json"));
        Assert.Empty(PackageReadiness.Inspect(Lock((input.Name, "1.14.2"), (core.Name, "file:com.crowdsim.core")), new[] { input, core }));
        Assert.Equal(before, File.ReadAllText(Path.Combine(input.Path, "package.json")));
        Assert.Equal(2, Directory.GetFiles(_root, "*", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public void MissingInputSystemRegistrationStopsReadinessAndNamesTheBlocker()
    {
        var issues = PackageReadiness.Inspect(Lock(("com.unity.inputsystem", "1.14.2")), Array.Empty<PackageReadiness.ResolvedPackage>());
        Assert.Contains(issues, issue => issue.Contains("com.unity.inputsystem") && issue.Contains("not registered"));
        Assert.Contains("sign in to Unity Hub", PackageReadiness.Recovery);
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public void RegisteredPackageErrorsAreNotHiddenByExistingMetadata()
    {
        var input = Installed("com.unity.inputsystem", "1.14.2", "Download failed: not signed in");
        var issues = PackageReadiness.Inspect(Lock((input.Name, input.Version)), new[] { input });
        Assert.Contains(issues, issue => issue.Contains("not signed in"));
    }

    [Fact]
    public void DifferentResolvedVersionCannotSilentlyReplaceThePinnedPackage()
    {
        var burst = Installed("com.unity.burst", "1.8.13");
        Assert.Contains(PackageReadiness.Inspect(Lock((burst.Name, "1.8.14")), new[] { burst }),
            issue => issue.Contains("resolved 1.8.13, expected locked 1.8.14"));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MissingOrMalformedPackagePayloadIsReported(bool malformed)
    {
        var burst = Installed("com.unity.burst", "1.8.14");
        string manifest = Path.Combine(burst.Path, "package.json");
        if (malformed) File.WriteAllText(manifest, "{"); else File.Delete(manifest);
        Assert.NotEmpty(PackageReadiness.Inspect(Lock((burst.Name, burst.Version)), new[] { burst }));
    }

    [Fact]
    public void RegisteredNameVersionMustMatchTheInstalledManifest()
    {
        var core = Installed("com.crowdsim.core", "0.11.1");
        File.WriteAllText(Path.Combine(core.Path, "package.json"), "{\"name\":\"wrong\",\"version\":\"0.11.0\"}");
        Assert.Contains(PackageReadiness.Inspect(Lock((core.Name, "file:com.crowdsim.core")), new[] { core }),
            issue => issue.Contains("disagrees"));
    }

    [Fact]
    public void VirtualBuiltinModulesDoNotNeedARealPayloadButTheirErrorsStillMatter()
    {
        string name = "com.unity.modules.uielements";
        var clean = new PackageReadiness.ResolvedPackage(name, "1.0.0", "", Array.Empty<string>());
        Assert.Empty(PackageReadiness.Inspect(Lock((name, "1.0.0")), new[] { clean }));
        var broken = new PackageReadiness.ResolvedPackage(name, "1.0.0", "", new[] { "module error" });
        Assert.Contains(PackageReadiness.Inspect(Lock((name, "1.0.0")), new[] { broken }), issue => issue.Contains("module error"));
    }

    private PackageReadiness.ResolvedPackage Installed(string name, string version, params string[] errors)
    {
        string directory = Path.Combine(_root, name); Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "package.json"), new JObject { ["name"] = name, ["version"] = version }.ToString());
        return new PackageReadiness.ResolvedPackage(name, version, directory, errors);
    }

    private static string Lock(params (string Name, string Version)[] packages) => new JObject {
        ["dependencies"] = new JObject(packages.Select(p => new JProperty(p.Name, new JObject { ["version"] = p.Version })))
    }.ToString();

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
