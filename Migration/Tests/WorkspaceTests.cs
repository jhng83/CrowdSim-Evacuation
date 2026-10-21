using System.Numerics;
using CrowdSim.Core.Model;
using CrowdSim.Diagnostics;
using CrowdSim.Workspace;
using Xunit;

namespace CrowdSim.Migration.Tests;

public sealed class WorkspaceTests
{
    private static WorkspaceSession Ready(bool hybrid = false)
    {
        WorkspaceSession session = new();
        Assert.True(session.SetHybrid(hybrid));
        Assert.True(session.Place(PlacementTool.Exit, new Vector2(0, 17), 3));
        Assert.True(session.Place(PlacementTool.Agents, new Vector2(24, 17), 15));
        Assert.True(session.Finalise());
        return session;
    }

    [Fact]
    public void InvalidCommandsAndNonfiniteCoordinatesCannotMutateTheBuilder()
    {
        WorkspaceSession session = new();
        int revision = session.Revision;
        Assert.False(session.Place(PlacementTool.Exit, new Vector2(20, 20), 1));
        Assert.False(session.Place(PlacementTool.Agents, new Vector2(float.NaN, 2), 5));
        Assert.False(session.Place(PlacementTool.Square, new Vector2(10, 10), 999));
        Assert.False(session.Resize(float.NaN, 20));
        Assert.False(session.Finalise());
        Assert.False(session.ToggleRunning());
        Assert.False(session.Undo());
        Assert.Equal(revision, session.Revision);
        Assert.Equal(0, session.Tick);
        Assert.Contains("exit", session.Readiness);
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(5)]
    public void ExitCapacityUsesReferenceWidthsAndOverlapRejectsAtomically(int capacity)
    {
        WorkspaceSession session = new();
        Vector2 point = new(0, 17);
        Assert.True(session.Preview(PlacementTool.Exit, point, capacity).Valid);
        Assert.Equal(0, session.ExitCount);
        Assert.True(session.Place(PlacementTool.Exit, point, capacity));
        ViewBody exit = Assert.Single(session.Frame.Bodies, b => b.Kind == ViewKind.Exit);
        Assert.Equal(capacity == 1 ? .48f : capacity * .4f, exit.Size.Z, 4);
        Assert.False(session.Preview(PlacementTool.Exit, point, capacity).Valid);
        Assert.False(session.Place(PlacementTool.Exit, point, capacity));
        Assert.Equal(1, session.ExitCount);
        Assert.False(session.CanResize);
        Assert.True(session.Undo());
        Assert.True(session.CanResize);
    }

    [Theory]
    [InlineData(1)] [InlineData(5)] [InlineData(15)] [InlineData(50)]
    public void PreviewIsDetachedAndRepeatedPreviewDoesNotChangeExactStartsOrSeededTraits(int count)
    {
        WorkspaceSession session = new(), control = new();
        Vector2 point = new(25, 15);
        for (int i = 0; i < 3; i++)
        {
            PlacementPreview preview = session.Preview(PlacementTool.Agents, point, count);
            Assert.True(preview.Valid);
            Assert.Equal(count, preview.Bodies.Count);
        }
        Assert.Equal(0, session.PlannedCount);
        Assert.Equal(0, session.Revision);
        foreach (WorkspaceSession item in new[] { session, control })
        {
            Assert.True(item.Place(PlacementTool.Exit, new Vector2(0, 17), 1));
            Assert.True(item.Place(PlacementTool.Agents, point, count));
        }
        var starts = session.Frame.Bodies.Where(b => b.Kind == ViewKind.Agent).Select(b => b.Position).ToArray();
        Assert.True(session.Finalise()); Assert.True(control.Finalise());
        Assert.Equal(starts, session.Frame.Bodies.Where(b => b.Kind == ViewKind.Agent).Select(b => b.Position));
        Assert.Equal(control.BehaviourValues, session.BehaviourValues);
        Assert.True(session.ToggleRunning()); Assert.True(control.ToggleRunning());
        for (int i = 0; i < 10; i++) { session.Advance(session.StepSeconds); control.Advance(control.StepSeconds); }
        Assert.Equal(control.Frame.Bodies.Select(b => b.Position), session.Frame.Bodies.Select(b => b.Position));
    }

    [Fact]
    public void PreviewOnlyHighlightsNewGeometryAndUndoRestoresRoomLock()
    {
        WorkspaceSession session = new();
        Assert.True(session.Resize(60, 40));
        Assert.True(session.Place(PlacementTool.Circle, new Vector2(35, 20), 1));
        PlacementPreview preview = session.Preview(PlacementTool.Square, new Vector2(20, 20), 2);
        Assert.True(preview.Valid);
        Assert.Single(preview.Bodies);
        Assert.True(session.Place(PlacementTool.Square, new Vector2(20, 20), 2));
        Assert.False(session.Place(PlacementTool.Circle, new Vector2(20, 20), 1));
        Assert.False(session.Resize(80, 50));
        Assert.True(session.Undo()); Assert.True(session.Undo());
        Assert.True(session.CanResize);
        Assert.True(session.Resize(80, 50));
        Assert.Equal(80, session.Frame.Width);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FinalisationTransportAndSingleEventKeepGeometryLockedAndPauseStopsTicks(bool hybrid)
    {
        WorkspaceSession session = Ready(hybrid);
        Assert.Equal(WorkspacePhase.Ready, session.Phase);
        Assert.Equal(0, session.Advance(50));
        Assert.False(session.Resize(10, 10));
        Assert.False(session.Place(PlacementTool.Agents, new Vector2(10, 10), 5));
        Assert.False(session.Undo());
        Assert.False(session.SetBehaviour(0, .1f));
        Assert.False(session.ResetBehaviour());
        Assert.False(session.SetHybrid(!hybrid));
        Assert.False(session.TriggerExplosion(new Vector2(28, 17)));
        Assert.True(session.ToggleRunning());
        Assert.True(session.TriggerExplosion(new Vector2(28, 17)));
        Assert.False(session.TriggerExplosion(new Vector2(29, 17)));
        session.Advance(session.StepSeconds);
        Assert.Equal(1, session.Tick);
        Assert.True(session.Frame.HazardReleased);
        Assert.True(session.ToggleRunning());
        int paused = session.Tick;
        Assert.Equal(0, session.Advance(100));
        Assert.Equal(paused, session.Tick);
        Assert.True(session.ToggleRunning());
        session.Advance(session.StepSeconds / 2d);
        Assert.Equal(paused, session.Tick);
        session.Advance(session.StepSeconds / 2d);
        Assert.Equal(paused + 1, session.Tick);
    }

    [Theory]
    [InlineData(.25, 0)] [InlineData(1, 1)] [InlineData(4, 4)]
    public void SpeedMultipliesSchedulingAndNotTheIntegrationStep(double speed, int expectedTicks)
    {
        WorkspaceSession session = Ready();
        session.SetSpeed(speed); session.ToggleRunning();
        // Four short frames avoid conflating the CPU catch-up budget with speed multiplication.
        for (int i = 0; i < 4; i++) session.Advance(session.StepSeconds / 4d);
        Assert.Equal(expectedTicks, session.Tick);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.SetSpeed(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.SetSpeed(5));
    }

    [Fact]
    public void AllFourteenControlsUseReferenceBoundsAndResetBeforeFinalisation()
    {
        WorkspaceSession session = new();
        var defaults = session.BehaviourValues.ToArray();
        Assert.Equal(14, BehaviourControls.All.Count);
        for (int i = 0; i < 14; i++)
        {
            Assert.True(session.SetBehaviour(i, -100));
            Assert.Equal(BehaviourControls.All[i].Minimum, session.BehaviourValues[i]);
            Assert.True(session.SetBehaviour(i, 100));
            Assert.Equal(BehaviourControls.All[i].Maximum, session.BehaviourValues[i]);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => session.SetBehaviour(0, float.NaN));
        Assert.True(session.ResetBehaviour());
        Assert.Equal(defaults, session.BehaviourValues);
    }

    [Fact]
    public void HiddenInspectorRetainsTabGridAndSelectionWithoutForcingItOpen()
    {
        WorkspaceSession session = Ready();
        session.SelectInspectorTab(3); session.ShowGrid = true;
        session.ToggleInspector(); session.ToggleInspector();
        Assert.Equal(3, session.InspectorTab);
        session.ToggleInspector();
        ViewBody agent = session.Frame.Bodies.First(b => b.Kind == ViewKind.Agent);
        session.SelectAgent(new Vector2(agent.Position.X, agent.Position.Z));
        Assert.False(session.InspectorVisible);
        Assert.Equal(2, session.InspectorTab);
        Assert.NotNull(session.SelectedAgentId);
        Assert.True(session.ShowGrid);
        session.ShowAgentInspector();
        Assert.True(session.InspectorVisible);
    }

    [Fact]
    public void FumeCommonBuilderKeepsReadinessGuardUntilAReleasePointExists()
    {
        WorkspaceSession session = new(ScenarioType.ToxicFume);
        Assert.True(session.Place(PlacementTool.Exit, new Vector2(0, 17), 1));
        Assert.True(session.Place(PlacementTool.Agents, new Vector2(25, 15), 5));
        Assert.False(session.CanFinalise);
        Assert.False(session.Finalise());
        Assert.Contains("release point", session.Readiness);
    }
    [Theory]
    [InlineData("F4", false, false, false, WorkspaceShortcut.ToggleInspector)]
    [InlineData("F4", false, true, false, WorkspaceShortcut.None)]
    [InlineData("F4", false, false, true, WorkspaceShortcut.None)]
    [InlineData("Z", true, false, false, WorkspaceShortcut.Undo)]
    [InlineData("Z", true, false, true, WorkspaceShortcut.None)]
    [InlineData("Space", false, false, false, WorkspaceShortcut.ToggleRunning)]
    [InlineData("Space", false, false, true, WorkspaceShortcut.None)]
    [InlineData("Escape", false, false, false, WorkspaceShortcut.CancelTool)]
    public void ShortcutsRespectFocusAndNeverInterceptAltF4(string key, bool control, bool alt, bool editing, WorkspaceShortcut expected)
        => Assert.Equal(expected, WorkspaceShortcuts.Resolve(key, control, alt, editing));

}
