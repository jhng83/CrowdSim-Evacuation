using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using CrowdSim.Distribution;
using CrowdSim.Migration;
using Xunit;

namespace CrowdSim.Migration.Tests;

// Synthetic PE headers/layouts test the real portable packager, not Unity compilation
// or executability. Only the Unity BuildPipeline can produce the actual player.
public sealed class StandaloneReleaseTests : IDisposable
{
    private readonly string _temporary = Path.Combine(Path.GetTempPath(), "crowdsim-release-" + Guid.NewGuid().ToString("N"));
    private string Player => Path.Combine(_temporary, "Player");

    private PlayerBuildReceipt Fixture()
    {
        foreach (string path in PlayerRelease.RequiredFiles)
        {
            string file = Path.Combine(Player, path); Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "synthetic fixture, not a player");
        }
        foreach (string path in new[] { "CrowdSim.exe", "UnityPlayer.dll", "MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll" })
        {
            using FileStream stream = File.Create(Path.Combine(Player, path));
            stream.SetLength(256); using BinaryWriter writer = new(stream);
            writer.Write((ushort)0x5a4d); stream.Position = 0x3c; writer.Write(64);
            stream.Position = 64; writer.Write(0x00004550u); writer.Write((ushort)0x8664);
            stream.Position = 88; writer.Write((ushort)0x20b);
        }
        PlayerBuildReceipt receipt = new() {
            Result = "Succeeded", EditorVersion = MigrationContract.EditorVersion,
            ApplicationVersion = MigrationContract.ApplicationVersion, Target = "StandaloneWindows64", Backend = "Mono",
            Scenes = new[] { MigrationContract.BootstrapScene, MigrationContract.ParityScene, MigrationContract.HumanoidScene }, BuildGuid = "synthetic-test-only",
            BuiltUtc = DateTime.UtcNow.ToString("O")
        };
        Write(receipt); return receipt;
    }
    private void Write(PlayerBuildReceipt receipt) => File.WriteAllText(Path.Combine(Player, PlayerRelease.ReceiptName), receipt.Serialize());

    [Fact]
    public void PackagingIncludesRequiredRuntimeDocsExamplesAndVerifiableHashesButOmitsSymbolsAndDebugBackups()
    {
        Fixture(); File.WriteAllText(Path.Combine(Player, "CrowdSim.pdb"), "symbols");
        string debug = Path.Combine(Player, "CrowdSim_BurstDebugInformation_DoNotShip"); Directory.CreateDirectory(debug);
        File.WriteAllText(Path.Combine(debug, "private.txt"), "not for player");
        string archive = Path.Combine(_temporary, "release.zip"); PlayerRelease.CreateArchive(Player, archive);
        using ZipArchive zip = ZipFile.OpenRead(archive);
        Assert.All(PlayerRelease.RequiredFiles, path => Assert.NotNull(zip.GetEntry(path)));
        Assert.DoesNotContain(zip.Entries, e => e.FullName.EndsWith(".pdb") || e.FullName.Contains("DoNotShip"));
        using StreamReader sums = new(zip.GetEntry(PlayerRelease.ChecksumsName)!.Open());
        foreach (string line in sums.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string path = line[66..]; using Stream content = zip.GetEntry(path)!.Open();
            Assert.Equal(line[..64], Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant());
        }
        Assert.Throws<IOException>(() => PlayerRelease.CreateArchive(Player, archive));
    }

    [Theory]
    [InlineData("CrowdSim.exe")] [InlineData("UnityPlayer.dll")]
    [InlineData("MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll")]
    [InlineData("CrowdSim_Data/Managed/CrowdSim.Portable.dll")]
    [InlineData("CrowdSim_Data/StreamingAssets/Scenarios/toxic_fume_205.json")]
    [InlineData("CrowdSim_Data/StreamingAssets/Documentation/Notices/AtkinsonHyperlegible-OFL.txt")]
    [InlineData("Notices/package-inventory.json")]
    public void IncompleteBuildsCannotBecomeDownloadableReleaseArchives(string missing)
    {
        Fixture(); File.Delete(Path.Combine(Player, missing));
        Assert.Throws<InvalidDataException>(() => PlayerRelease.CreateArchive(Player, Path.Combine(_temporary, "bad.zip")));
        Assert.False(File.Exists(Path.Combine(_temporary, "bad.zip")));
    }

    [Theory]
    [InlineData("editor")] [InlineData("backend")] [InlineData("development")] [InlineData("failed")]
    [InlineData("errors")] [InlineData("entry")] [InlineData("version")] [InlineData("diagnostics")]
    public void BuildReceiptMustMatchTheSupportedSuccessfulRelease(string change)
    {
        PlayerBuildReceipt r = Fixture();
        switch (change)
        {
            case "editor": r.EditorVersion = "wrong"; break;
            case "backend": r.Backend = "IL2CPP"; break;
            case "development": r.Development = true; break;
            case "failed": r.Result = "Failed"; break;
            case "errors": r.Errors = 1; break;
            case "entry": r.Scenes = new[] { MigrationContract.ParityScene }; break;
            case "version": r.ApplicationVersion = "wrong"; break;
            case "diagnostics": r.Scenes = new[] { MigrationContract.BootstrapScene }; break;
        }
        Write(r); Assert.Throws<InvalidDataException>(() => PlayerRelease.Validate(Player));
    }

    [Theory]
    [InlineData("UnityEditor.dll")] [InlineData("CrowdSim.Editor.dll")]
    [InlineData("CrowdSim.Foundation.PlayModeTests.dll")]
    public void EditorAndTestAssembliesCannotShip(string name)
    {
        Fixture(); File.WriteAllText(Path.Combine(Player, "CrowdSim_Data/Managed", name), "not for player");
        Assert.Throws<InvalidDataException>(() => PlayerRelease.Validate(Player));
    }

    [Fact]
    public void ArchiveNeverOverwritesExistingFilesOrPackagesItself()
    {
        Fixture(); string existing = Path.Combine(_temporary, "existing.zip"); File.WriteAllText(existing, "retain me");
        Assert.Throws<IOException>(() => PlayerRelease.CreateArchive(Player, existing));
        Assert.Equal("retain me", File.ReadAllText(existing));
        Assert.Throws<ArgumentException>(() => PlayerRelease.CreateArchive(Player, Path.Combine(Player, "self.zip")));
        Assert.False(File.Exists(Path.Combine(Player, PlayerRelease.ChecksumsName)));
    }

    [Fact]
    public void RejectsWrongArchitectureAndExternalSymlinks()
    {
        Fixture(); string exe = Path.Combine(Player, "CrowdSim.exe"); byte[] file = File.ReadAllBytes(exe);
        file[68] = 0x4c; file[69] = 0x01; File.WriteAllBytes(exe, file);
        Assert.Throws<InvalidDataException>(() => PlayerRelease.Validate(Player));
        Fixture(); string external = Path.Combine(_temporary, "outside"); Directory.CreateDirectory(external);
        Directory.CreateSymbolicLink(Path.Combine(Player, "external-link"), external);
        Assert.Throws<InvalidDataException>(() => PlayerRelease.Validate(Player));
    }

    public void Dispose() { if (Directory.Exists(_temporary)) Directory.Delete(_temporary, true); }
}
