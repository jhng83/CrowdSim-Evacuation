using System.Numerics;
using CrowdSim.Core.Model;
using CrowdSim.Workspace;
using Xunit;

namespace CrowdSim.Migration.Tests;

public sealed class QuitWorkflowTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "crowdsim-quit-" + Guid.NewGuid().ToString("N"));
    private WorkspaceFiles Files(bool dirty = true)
    {
        WorkspaceSession s = new();
        Assert.True(s.Place(PlacementTool.Exit, new Vector2(.3f, 17), 3));
        Assert.True(s.Place(PlacementTool.Agents, new Vector2(8, 8), 5));
        WorkspaceFiles f = new(s, new ScenarioLibrary(Path.Combine(_root, "saves")));
        if (!dirty) Assert.True(f.Save("saved"));
        return f;
    }

    [Fact]
    public void DirtyQuitPausesAndKeepWorkingRetainsAuthoredDataAndReplayState()
    {
        WorkspaceFiles f = Files(); Assert.True(f.Finalise()); f.Session.ToggleRunning();
        string authored = f.Session.ExportAuthoredJson("original");
        Assert.False(f.Quit()); Assert.Equal(FileTransition.Quit, f.Pending); Assert.False(f.Session.Running);
        Assert.Equal(FileTransition.None, f.Completed); f.Cancel();
        Assert.Equal(FileTransition.None, f.Pending); Assert.True(f.Session.IsDirty);
        Assert.Equal(authored, f.Session.ExportAuthoredJson("original")); Assert.True(f.Session.CanReplay);
        Assert.False(f.Quit()); Assert.True(f.DiscardAndContinue()); Assert.Equal(FileTransition.Quit, f.Completed);
    }

    [Fact]
    public void QuitAfterSaveRequiresOverwriteConfirmationAndDoesNotProceedAfterSaveFailure()
    {
        WorkspaceFiles f = Files(); Assert.True(f.Save("existing"));
        Assert.True(f.Session.Place(PlacementTool.Agents, new Vector2(12, 8), 1));
        Assert.False(f.Quit());
        Assert.False(f.Save("existing", saveAs: true, continuePending: true));
        Assert.Equal(FileTransition.None, f.Completed); Assert.Equal(FileTransition.Quit, f.Pending);
        f.CancelOverwrite(); Assert.Equal(FileTransition.Quit, f.Pending);
        Assert.True(f.Save("new", continuePending: true)); Assert.Equal(FileTransition.Quit, f.Completed);
        Assert.True(File.Exists(Path.Combine(_root, "saves/new.json")));

        WorkspaceFiles failed = Files();
        string saveDirectory = failed.Library.DirectoryPath; Directory.Delete(saveDirectory, true); File.WriteAllText(saveDirectory, "not a directory");
        Assert.False(failed.Quit()); Assert.False(failed.Save("cannot-write", continuePending: true));
        Assert.Equal(FileTransition.Quit, failed.Pending); Assert.Equal(FileTransition.None, failed.Completed);
        Assert.True(failed.Session.IsDirty); failed.Cancel(); Assert.Equal(FileTransition.None, failed.Pending);
    }

    [Fact]
    public void CleanQuitCompletesWithoutChangingSimulationAndExistingDecisionsCannotBeReplaced()
    {
        WorkspaceFiles f = Files(false); Assert.True(f.Finalise()); f.Session.ToggleRunning();
        int tick = f.Session.Tick; Assert.True(f.Quit()); Assert.Equal(FileTransition.Quit, f.Completed);
        Assert.Equal(tick, f.Session.Tick); Assert.False(f.Session.Running); f.AcknowledgeTransition();
        Assert.True(f.Session.IsDirty == false);
        WorkspaceFiles dirty = Files(); Assert.False(dirty.Leave(false));
        Assert.False(dirty.Quit()); Assert.Equal(FileTransition.Startup, dirty.Pending); dirty.Cancel();
        Assert.False(dirty.Quit()); Assert.False(dirty.New(ScenarioType.ToxicFume)); Assert.Equal(FileTransition.Quit, dirty.Pending);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
