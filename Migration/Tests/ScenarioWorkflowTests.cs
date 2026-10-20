using System.Numerics;
using CrowdSim.Core.Model;
using CrowdSim.Diagnostics;
using CrowdSim.OpenGL.UI;
using CrowdSim.Workspace;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CrowdSim.Migration.Tests;

public sealed class ScenarioWorkflowTests : IDisposable
{
    private readonly string _temporary = Path.Combine(Path.GetTempPath(), "crowdsim-m5-test-" + Guid.NewGuid().ToString("N"));
    private string UserDirectory => Path.Combine(_temporary, "user");
    private string ReadOnlyDirectory => Path.Combine(_temporary, "examples");
    private static string Root
    {
        get
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CrowdSim.sln"))) directory = directory.Parent;
            return directory?.FullName ?? throw new DirectoryNotFoundException();
        }
    }
    public ScenarioWorkflowTests() { Directory.CreateDirectory(ReadOnlyDirectory); }
    private WorkspaceFiles Files(bool furnished = true) => new(furnished ? WorkspaceExamples.Furniture() : new WorkspaceSession(), new ScenarioLibrary(UserDirectory, ReadOnlyDirectory));
    private string Source(string name = "example")
    {
        string path = Path.Combine(ReadOnlyDirectory, name + ".json");
        File.WriteAllText(path, WorkspaceExamples.Furniture().ExportAuthoredJson(name));
        return path;
    }

    [Theory]
    [InlineData("../bad/name:*", "bad_name")]
    [InlineData("CON", "scenario_CON")]
    [InlineData("LPT9.txt", "scenario_LPT9.txt")]
    [InlineData("  ..  ", "scenario")]
    [InlineData("Case one", "Case_one")]
    [InlineData("", "scenario")]
    public void NamesRemainPortableAndCannotEscapeTheWriteDirectory(string input, string expected)
    {
        WorkspaceFiles files = Files();
        Assert.Equal(expected, ScenarioLibrary.SafeName(input));
        Assert.True(files.Save(input));
        Assert.Equal(Path.Combine(UserDirectory, expected + ".json"), files.CurrentEntry!.Path);
        Assert.False(files.Session.IsDirty);
        Assert.Equal(expected, files.Session.Name);
        Assert.True(File.Exists(files.CurrentEntry.Path));
    }

    [Fact]
    public void RuntimeSaveKeepsAuthoredStartsAndReplayRestoresIntactFurnitureAfterDamage()
    {
        WorkspaceFiles files = Files();
        Assert.True(files.Save("before"));
        Assert.True(files.Finalise());
        var initial = files.Session.Frame;
        string authored = files.Session.ExportAuthoredJson("canonical");
        files.Session.ToggleRunning(); files.Session.TriggerExplosion(new Vector2(18, 12));
        for (int i = 0; i < 120; i++) files.Session.Advance(files.Session.StepSeconds);
        Assert.Contains(files.Session.Frame.FurnitureBodies, b => b.State != ChairState.Upright);
        Assert.False(files.Session.IsDirty);
        Assert.Equal(authored, files.Session.ExportAuthoredJson("canonical"));
        Assert.True(files.Save("after", saveAs: true));
        WorkspaceSession loaded = WorkspaceSession.FromJson(File.ReadAllText(files.CurrentEntry!.Path));
        Assert.True(loaded.Finalise());
        Assert.Equal(Positions(initial), Positions(loaded.Frame));
        Assert.Equal(11, loaded.Frame.SeatedAgents);
        Assert.True(files.Replay());
        Assert.Equal(0, files.Session.Tick); Assert.False(files.Session.Running);
        Assert.False(files.Session.Frame.HazardReleased);
        Assert.Equal(Positions(initial), Positions(files.Session.Frame));
        Assert.All(files.Session.Frame.FurnitureBodies, b => Assert.Equal(ChairState.Upright, b.State));
        Assert.Equal(11, files.Session.Frame.SeatedAgents);
        Assert.Empty(Directory.GetFiles(UserDirectory, "*.tmp"));
    }
    private static Vector3[] Positions(PresentationFrame frame) => frame.Bodies.Where(b => b.Kind == ViewKind.Agent).Select(b => b.Position).ToArray();

    [Fact]
    public void LoadRequiresPauseAndDirtyDecisionAndValidatedCandidateSurvivesCancelOrDiscard()
    {
        WorkspaceFiles files = Files();
        string source = Source(); files.Refresh();
        Assert.True(files.Finalise()); files.Session.ToggleRunning();
        WorkspaceSession original = files.Session;
        Assert.False(files.Import(source)); Assert.Equal(FileTransition.None, files.Pending);
        Assert.Same(original, files.Session);
        files.Session.ToggleRunning();
        Assert.False(files.Import(source)); Assert.Equal(FileTransition.Load, files.Pending);
        files.Cancel(); Assert.Same(original, files.Session); Assert.True(files.Session.IsDirty);
        Assert.False(files.Import(source));
        File.WriteAllText(source, "broken after preparation");
        Assert.True(files.DiscardAndContinue());
        Assert.NotSame(original, files.Session);
        Assert.True(files.CurrentEntry!.ReadOnly);
        Assert.True(files.Session.IsBuilding); Assert.False(files.Session.Running);
        Assert.False(files.Session.IsDirty); Assert.False(files.Session.CanReplay);
        Assert.Equal(26, files.Session.PlannedCount);
    }

    [Theory]
    [InlineData("{not json}")]
    [InlineData("{\"version\":99}")]
    [InlineData("{\"version\":4,\"agentCount\":\"five\"}")]
    public void FailedLoadKeepsCurrentSceneAndDirtyState(string json)
    {
        WorkspaceFiles files = Files();
        string path = Path.Combine(ReadOnlyDirectory, "bad.json"); File.WriteAllText(path, json);
        WorkspaceSession original = files.Session;
        Assert.False(files.Import(path));
        Assert.Same(original, files.Session); Assert.True(original.IsDirty);
        Assert.Equal(FileTransition.None, files.Pending); Assert.Contains("failed", files.Message);
    }

    [Fact]
    public void PhysicalValidationFailureIsAlsoTransactional()
    {
        WorkspaceFiles files = Files();
        JObject invalid = JObject.Parse(files.Session.ExportAuthoredJson("invalid"));
        ((JArray)invalid["furniture"]!).Add(invalid["furniture"]![0]!.DeepClone());
        string path = Path.Combine(ReadOnlyDirectory, "overlap.json"); File.WriteAllText(path, invalid.ToString());
        WorkspaceSession old = files.Session;
        Assert.False(files.Import(path)); Assert.Same(old, files.Session);
        Assert.Equal(5, files.Session.FurnitureCount); Assert.True(old.IsDirty);
    }

    [Fact]
    public void ReadOnlySourceAndSameNamedWritableSaveStayDistinctAndCopyNeverModifiesSource()
    {
        WorkspaceFiles files = Files(false);
        string source = Source("same"); string bytes = File.ReadAllText(source);
        Assert.True(files.Import(source)); Assert.True(files.CurrentEntry!.ReadOnly);
        Assert.True(files.Save("same")); Assert.False(files.CurrentEntry!.ReadOnly);
        files.Refresh();
        Assert.Equal(2, files.Entries.Count(e => e.Name == "same"));
        Assert.False(files.Entries[0].ReadOnly);
        Assert.Equal(bytes, File.ReadAllText(source));
        Assert.True(files.Save("same")); // Ordinary Save to the current writable entry.
        Assert.False(files.Save("same", saveAs: true));
        Assert.Equal("same", files.OverwriteName);
        files.CancelOverwrite();
        Assert.Equal(bytes, File.ReadAllText(source));
    }

    [Fact]
    public void DirtySaveAndContinuePausesAndClearsTheOldCheckpointOnlyAfterSuccess()
    {
        WorkspaceFiles files = Files();
        files.Finalise(); files.Session.ToggleRunning();
        WorkspaceSession old = files.Session;
        Assert.False(files.New(ScenarioType.ToxicFume)); Assert.False(old.Running);
        Assert.True(files.Save("retained", continuePending: true));
        Assert.NotSame(old, files.Session); Assert.False(files.Session.CanReplay);
        Assert.False(files.Session.IsDirty); Assert.Equal(0, files.Session.PlannedCount);
        Assert.Equal(ScenarioType.ToxicFume, files.Session.Scenario);
        Assert.True(File.Exists(Path.Combine(UserDirectory, "retained.json")));
        Assert.Null(files.CurrentEntry);
    }

    [Fact]
    public void OverwriteDecisionCanBackOutOrContinueThePendingNewScenario()
    {
        WorkspaceFiles files = Files(); Assert.True(files.Save("existing"));
        files.Session.SelectAsset(BuilderAsset.SingleChair);
        Assert.True(files.Session.Place(PlacementTool.Asset, new Vector2(24, 28), 0));
        string oldBytes = File.ReadAllText(Path.Combine(UserDirectory, "existing.json"));
        Assert.False(files.New(ScenarioType.Explosion));
        Assert.False(files.Save("existing", saveAs: true, continuePending: true));
        Assert.Equal(oldBytes, File.ReadAllText(Path.Combine(UserDirectory, "existing.json")));
        files.CancelOverwrite(); Assert.Equal(FileTransition.New, files.Pending);
        Assert.False(files.Save("existing", saveAs: true, continuePending: true));
        Assert.True(files.ConfirmOverwrite());
        Assert.Equal(0, files.Session.PlannedCount);
        Assert.NotEqual(oldBytes, File.ReadAllText(Path.Combine(UserDirectory, "existing.json")));
    }

    [Fact]
    public void SaveFailureCannotLoseDirtyChangesOrCompletePendingTransition()
    {
        string blocked = Path.Combine(_temporary, "not-a-directory"); File.WriteAllText(blocked, "keep");
        WorkspaceFiles files = new(WorkspaceExamples.Furniture(), new ScenarioLibrary(blocked));
        WorkspaceSession original = files.Session;
        Assert.False(files.New(ScenarioType.Explosion));
        Assert.False(files.Save("blocked", continuePending: true));
        Assert.Same(original, files.Session); Assert.True(files.Session.IsDirty);
        Assert.Equal(FileTransition.New, files.Pending);
        Assert.Equal("keep", File.ReadAllText(blocked)); Assert.Contains("failed", files.Message);
    }

    [Fact]
    public void FailedAtomicDestinationLeavesNoTemporaryFileAndPreservesPreviouslySavedData()
    {
        WorkspaceFiles files = Files(); Assert.True(files.Save("keep"));
        string bytes = File.ReadAllText(Path.Combine(UserDirectory, "keep.json"));
        Directory.CreateDirectory(Path.Combine(UserDirectory, "directory.json"));
        files.Session.SetBehaviour(0, .2f);
        Assert.False(files.Save("directory", saveAs: true));
        Assert.True(files.Session.IsDirty);
        Assert.Equal(bytes, File.ReadAllText(Path.Combine(UserDirectory, "keep.json")));
        Assert.Empty(Directory.GetFiles(UserDirectory, "*.tmp"));
    }

    [Fact]
    public void LegacyV1RandomPopulationAndBlueWallsReplayAtTheSameSeededStarts()
    {
        string json = File.ReadAllText(Path.Combine(Root, "UnityProject/Assets/StreamingAssets/Scenarios/legacy_v1.json"));
        WorkspaceSession session = WorkspaceSession.FromJson(json);
        Assert.True(session.UsesRandomStarts); Assert.Equal(4, session.PlannedCount);
        Assert.True(session.Finalise()); Assert.Equal(4, session.Frame.Population);
        Assert.Contains(session.Frame.Bodies, b => b.Kind == ViewKind.Wall);
        var initial = Positions(session.Frame);
        session.ToggleRunning(); for (int i = 0; i < 10; i++) session.Advance(session.StepSeconds);
        Assert.True(session.Replay()); Assert.Equal(initial, Positions(session.Frame));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Fume205ReplayRegeneratesAReadyUnreleasedCacheAndRestoresFurnitureAndStarts(bool hybrid)
    {
        JObject json = JObject.Parse(File.ReadAllText(Path.Combine(Root, "tests/CrowdSim.Tests/Fixtures/toxic_fume_205.json")));
        json["hybridEnabled"] = hybrid;
        WorkspaceSession session = WorkspaceSession.FromJson(json.ToString());
        Assert.False(session.HasFumeCache); Assert.True(session.CanFinalise);
        Assert.True(session.Finalise()); Assert.True(session.HasFumeCache);
        Assert.Equal(205, session.Frame.Population); Assert.Equal(80, session.Frame.SeatedAgents);
        var initial = Positions(session.Frame);
        string authored = session.ExportAuthoredJson("canonical");
        session.ToggleRunning(); Assert.True(session.ReleaseSavedFume()); Assert.False(session.ReleaseSavedFume());
        for (int i = 0; i < 120; i++) session.Advance(session.StepSeconds);
        Assert.True(session.FumeReleased); Assert.True(session.Frame.SeatedAgents < 80);
        Assert.Equal(authored, session.ExportAuthoredJson("canonical"));
        Assert.True(session.Replay());
        Assert.True(session.HasFumeCache); Assert.False(session.FumeReleased);
        Assert.False(session.Running); Assert.Equal(0, session.Tick);
        Assert.Equal(80, session.Frame.SeatedAgents); Assert.Equal(initial, Positions(session.Frame));
        Assert.All(session.Frame.FurnitureBodies, b => Assert.Equal(ChairState.Upright, b.State));
        Assert.Equal(0, session.Frame.MaximumExposure);
    }

    [Fact]
    public void NewAndLeaveHaveDirtyPromptsButRuntimeMotionAloneDoesNotDirtyASavedScene()
    {
        WorkspaceFiles files = Files(false);
        Assert.False(files.Session.IsDirty);
        files.Session.SelectAsset(BuilderAsset.SingleChair); files.Session.SetAssetRotation(37.5f);
        Assert.False(files.Session.IsDirty);
        Assert.True(files.Session.Place(PlacementTool.Asset, new Vector2(20, 17), 0));
        Assert.False(files.Leave(false)); Assert.Equal(FileTransition.Startup, files.Pending);
        files.Cancel(); Assert.True(files.Session.IsDirty);
        Assert.True(files.Save("authored")); Assert.True(files.Leave(true));
        Assert.Equal(FileTransition.Diagnostics, files.Completed);
        Assert.True(files.New(ScenarioType.Explosion)); Assert.False(files.Session.CanReplay);
    }
    public void Dispose() => Directory.Delete(_temporary, true);
}
