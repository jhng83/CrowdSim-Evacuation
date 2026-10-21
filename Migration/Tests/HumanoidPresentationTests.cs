using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using CrowdSim.Diagnostics;
using CrowdSim.Workspace;
using Xunit;

namespace CrowdSim.Migration.Tests;

public sealed class HumanoidPresentationTests
{
    private static AgentVisualFrame Sample(AgentState state = AgentState.Evacuating, SeatingPosture posture = SeatingPosture.Walking,
        float rise = 0, float speed = 1, int? target = null, bool attending = false)
        => new(7, new Vector2(20, 14), Vector2.UnitY, speed, state, posture, rise, .7f, target, attending);

    [Theory]
    [InlineData(AgentState.Wandering, HumanPose.Standing)]
    [InlineData(AgentState.WaitingToReact, HumanPose.Standing)]
    [InlineData(AgentState.Evacuating, HumanPose.Standing)]
    [InlineData(AgentState.Injured, HumanPose.Standing)]
    [InlineData(AgentState.Crawling, HumanPose.Crawling)]
    [InlineData(AgentState.Fallen, HumanPose.Prone)]
    [InlineData(AgentState.TempIncap, HumanPose.Prone)]
    [InlineData(AgentState.Incapacitated, HumanPose.Prone)]
    [InlineData(AgentState.Dead, HumanPose.Prone)]
    public void AllActiveStatesHaveSolidNearAndFarPoses(AgentState state, HumanPose expected)
    {
        AgentVisualFrame agent = Sample(state);
        Assert.Equal(expected, agent.Pose);
        List<HumanPart> parts = new();
        foreach (bool detail in new[] { false, true })
        {
            HumanGeometry.Build(agent, detail, 1.2f, parts);
            Assert.InRange(parts.Count, 6, 16);
            Assert.All(parts, p =>
            {
                Assert.True(p.Size.X > 0 && p.Size.Y > 0 && p.Size.Z > 0);
                Assert.True(float.IsFinite(p.Center.X + p.Center.Y + p.Center.Z));
                Assert.InRange(p.Rotation.Length(), .99999f, 1.00001f);
            });
            float top = parts.Max(p => BoundsY(p).Max);
            Assert.InRange(top, expected == HumanPose.Standing ? 1.69f : .2f, expected == HumanPose.Standing ? 1.75f : .56f);
        }
    }
    private static (float Min, float Max) BoundsY(HumanPart part)
    {
        List<float> ys = new();
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
            ys.Add((part.Center + Vector3.Transform(part.Size * new Vector3(x, y, z) * .5f, part.Rotation)).Y);
        return (ys.Min(), ys.Max());
    }
    [Fact]
    public void SeatRiseAndAssistanceHaveDistinctGeometryAndCasualtyOverridesSeatOrHelp()
    {
        List<HumanPart> parts = new();
        float previousTop = 0;
        foreach (float rise in new[] { 0f, .5f, 1f })
        {
            AgentVisualFrame agent = Sample(posture: SeatingPosture.StandingUp, rise: rise);
            Assert.Equal(HumanPose.Rising, agent.Pose);
            HumanGeometry.Build(agent, true, 0, parts);
            float top = parts.Max(p => BoundsY(p).Max);
            Assert.True(top > previousTop); previousTop = top;
        }
        Assert.Equal(HumanPose.Seated, Sample(posture: SeatingPosture.Seated).Pose);
        AgentVisualFrame helper = Sample(target: 9, attending: true);
        Assert.Equal(HumanPose.Assisting, helper.Pose);
        HumanGeometry.Build(helper, true, 0, parts);
        Assert.InRange(parts.Max(p => BoundsY(p).Max), 1.2f, 1.4f);
        Assert.Equal(HumanPose.Prone, Sample(AgentState.Dead, SeatingPosture.Seated, target: 9, attending: true).Pose);
    }
    [Fact]
    public void GaitVariesWithPhaseAndRunningButStationaryPoseDoesNotWalk()
    {
        List<HumanPart> parts = new();
        HumanGeometry.Build(Sample(), false, 0, parts); var neutral = parts.ToArray();
        HumanGeometry.Build(Sample(), false, MathF.PI / 2, parts); var walking = parts.ToArray();
        Assert.NotEqual(neutral[2].Center, walking[2].Center);
        HumanGeometry.Build(Sample(speed: 2.4f), false, MathF.PI / 2, parts);
        Assert.True(Math.Abs(parts[2].Center.X) > Math.Abs(walking[2].Center.X));
        HumanGeometry.Build(Sample(speed: 0), false, 0, parts); var still = parts.ToArray();
        HumanGeometry.Build(Sample(speed: 0), false, 2, parts);
        Assert.Equal(still.Select(p => p.Center), parts.Select(p => p.Center));
        Assert.True(HumanGeometry.Cadence(2.4f) > HumanGeometry.Cadence(1));
    }
    [Fact]
    public void StatePaletteAndIndicatorsSeparatePanicFromPhysicalHealth()
    {
        Assert.NotEqual(AgentPalette.Color(AgentState.Evacuating), AgentPalette.Color(AgentState.Injured));
        Assert.Equal(AgentPalette.Color(AgentState.TempIncap), AgentPalette.Color(AgentState.Incapacitated));
        Assert.True(Sample().ShowPanic); Assert.False(Sample(AgentState.Dead).ShowPanic);
        Assert.Equal("! ALARM", Sample(AgentState.WaitingToReact).Indicator);
        Assert.False(Sample().Alarm);
        Assert.Equal("GOING TO HELP 9", Sample(target: 9).Indicator);
        Assert.Equal("+ HELPING 9", Sample(target: 9, attending: true).Indicator);
        Assert.Equal("GOING TO HELP 9 · ! ALARM", Sample(AgentState.WaitingToReact, target: 9).Indicator);
    }
    [Fact]
    public void MotionInterpolatesWithoutOvershootFreezesWhenPausedAndResetsPositionOnReplay()
    {
        HumanMotion motion = new(7);
        motion.Observe(0, Vector2.Zero); motion.Observe(1, new Vector2(2, 4), 1);
        motion.Advance(.075f, true, 1, .15f, 1);
        Assert.Equal(new Vector2(1, 2), motion.Position);
        Assert.Equal(.5f, motion.Rise);
        for (int i = 0; i < 20; i++) motion.Advance(.1f, true, 1, .15f, 1);
        Assert.Equal(new Vector2(2, 4), motion.Position);
        float phase = motion.Phase; motion.Advance(100, false, 4, .15f, 2);
        Assert.Equal(phase, motion.Phase);
        Assert.Equal(1, motion.Rise);
        motion.Observe(0, new Vector2(8, 6)); Assert.Equal(new Vector2(8, 6), motion.Position);
        Assert.Equal(0, motion.Rise); Assert.Equal(new HumanMotion(7).Phase, motion.Phase);
    }
    [Theory]
    [InlineData(.45f)] [InlineData(1f)] [InlineData(2.5f)]
    public void CameraFitsOverviewCentersFollowAndUsesReferenceHeadHeightAndHeading(float zoom)
    {
        AgentVisualFrame selected = Sample();
        AgentCameraFrame overview = AgentCameraRig.Calculate(60, 40, 1.6f, -35, 34, zoom, AgentCameraMode.Overview, selected);
        Assert.True(overview.Orthographic); Assert.Equal(new Vector3(30, 0, 20), overview.Target);
        AgentCameraFrame follow = AgentCameraRig.Calculate(60, 40, 1.6f, -35, 34, zoom, AgentCameraMode.Follow, selected);
        Assert.Equal(new Vector3(20, 0, 14), follow.Target); Assert.Equal(overview.Size / 1.35f, follow.Size, 5);
        AgentCameraFrame pov = AgentCameraRig.Calculate(60, 40, 1.6f, -35, 34, zoom, AgentCameraMode.Pov, selected);
        Assert.False(pov.Orthographic); Assert.Equal(new Vector3(20, 1.496f, 14), pov.Eye);
        Assert.True(pov.Target.Z > pov.Eye.Z); Assert.True(pov.Target.Y < pov.Eye.Y);
        Assert.Equal(overview.Target, AgentCameraRig.Calculate(60, 40, 1.6f, -35, 34, zoom, AgentCameraMode.Pov, null).Target);
    }
    [Fact]
    public void PovProjectionMatchesTheActualUnmodifiedReferenceCamera()
    {
        AgentVisualFrame agent = Sample();
        AgentCameraFrame frame = AgentCameraRig.Calculate(60, 40, 1.6f, -35, 34, 1, AgentCameraMode.Pov, agent);
        CrowdSim.OpenGL.Rendering.Camera3D native = new();
        native.ConfigurePov(Vector2.Zero, new Vector2(1000, 600), agent.Position, agent.Heading, 1.496f);
        Vector3 forward = Vector3.Normalize(frame.Target - frame.Eye);
        Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, forward));
        Vector3 up = Vector3.Cross(forward, right);
        foreach (Vector3 nativePoint in new[] { new Vector3(20, 18, 1), new Vector3(21, 20, 0), new Vector3(19, 25, 1.7f) })
        {
            Assert.True(native.TryProject(nativePoint, out Vector2 expected));
            Vector3 point = PresentationFrame.ToPresentation(new Vector2(nativePoint.X, nativePoint.Y), nativePoint.Z);
            Vector3 relative = point - frame.Eye;
            float focal = 300 / MathF.Tan(AgentCameraFrame.FieldOfView * MathF.PI / 360);
            float depth = Vector3.Dot(relative, forward);
            Vector2 actual = new(500 + Vector3.Dot(relative, right) / depth * focal, 300 - Vector3.Dot(relative, up) / depth * focal);
            Assert.InRange(Vector2.Distance(expected, actual), 0, .001f);
        }
        Assert.False(native.TryProject(new Vector3(20, 14.02f, 1.496f), out _));
        Assert.Equal(.04f, AgentCameraFrame.NearPlane);
    }
    [Fact]
    public void HiddenSelectionFollowPovClearAndReplayRetainInspectorChoiceAndNeverDirtyAuthoredScene()
    {
        WorkspaceSession session = Ready(); session.MarkSaved("M8"); string json = session.ExportAuthoredJson("M8");
        session.ToggleInspector(); session.ShowGrid = true;
        Assert.False(session.SetCameraMode(AgentCameraMode.Pov));
        session.SelectAgentById(session.Frame.Agents[0].Id);
        Assert.False(session.InspectorVisible); Assert.Equal(2, session.InspectorTab);
        Assert.True(session.SetCameraMode(AgentCameraMode.Follow)); Assert.True(session.SetCameraMode(AgentCameraMode.Pov));
        session.SetCameraMode(AgentCameraMode.Follow); Assert.Equal(AgentCameraMode.Follow, session.CameraMode);
        Assert.Equal(json, session.ExportAuthoredJson("M8")); Assert.False(session.IsDirty);
        session.SelectAgentById(null); Assert.Equal(AgentCameraMode.Overview, session.CameraMode);
        session.SelectAgentById(0); session.SetCameraMode(AgentCameraMode.Pov); Assert.True(session.Replay());
        Assert.Null(session.SelectedAgentId); Assert.Equal(AgentCameraMode.Overview, session.CameraMode);
        Assert.False(session.InspectorVisible); Assert.True(session.ShowGrid);
    }
    [Fact]
    public void ExitingAgentEndsSelectionAndBothCameraModesAutomatically()
    {
        WorkspaceSession session = new();
        Assert.True(session.Place(PlacementTool.Exit, new Vector2(0, 17), 5));
        Assert.True(session.Place(PlacementTool.Agents, new Vector2(3, 17), 1)); Assert.True(session.Finalise());
        session.SelectAgentById(0); session.SetCameraMode(AgentCameraMode.Pov); session.ToggleRunning();
        Assert.True(session.TriggerExplosion(new Vector2(12, 17)));
        for (int i = 0; i < 1600 && session.SelectedAgentId.HasValue; i++) session.Advance(session.StepSeconds);
        Assert.Equal(1, session.Frame.Exited); Assert.Null(session.SelectedAgentId);
        Assert.Equal(AgentCameraMode.Overview, session.CameraMode); Assert.Empty(session.Frame.Agents);
        Assert.Contains("ended automatically", session.Message);
    }
    private static WorkspaceSession Ready(ScenarioType scenario = ScenarioType.Explosion, bool hybrid = false)
    {
        WorkspaceSession session = new(scenario); session.SetHybrid(hybrid);
        Assert.True(session.Place(PlacementTool.Exit, new Vector2(0, 17), 5));
        Assert.True(session.Place(PlacementTool.Agents, new Vector2(24, 17), 15));
        if (scenario == ScenarioType.ToxicFume) { Assert.True(session.PlaceFumeSource(new Vector2(26, 17))); Assert.True(session.ComputeFume()); }
        Assert.True(session.Finalise()); return session;
    }
    [Theory]
    [InlineData(ScenarioType.Explosion, false)] [InlineData(ScenarioType.Explosion, true)]
    [InlineData(ScenarioType.ToxicFume, false)] [InlineData(ScenarioType.ToxicFume, true)]
    public void RepeatedPresentationAndCameraCommandsDoNotChangePhysicsExposureFurnitureOrSeededOutcome(ScenarioType scenario, bool hybrid)
    {
        WorkspaceSession viewed = Ready(scenario, hybrid), control = Ready(scenario, hybrid);
        foreach (WorkspaceSession session in new[] { viewed, control })
        {
            session.ToggleRunning();
            if (scenario == ScenarioType.Explosion) session.TriggerExplosion(new Vector2(28, 17)); else session.ReleaseFume();
        }
        List<HumanPart> parts = new();
        for (int tick = 0; tick < 80; tick++)
        {
            foreach (AgentVisualFrame agent in viewed.Frame.Agents)
            {
                HumanGeometry.Build(agent, tick % 2 == 0, tick * .1f, parts);
                AgentCameraRig.Calculate(60, 40, 1.6f, tick, 34, 1, AgentCameraMode.Pov, agent);
            }
            viewed.SelectAgentById(viewed.Frame.Agents.FirstOrDefault()?.Id);
            viewed.SetCameraMode((AgentCameraMode)(tick % 3));
            viewed.Advance(viewed.StepSeconds); control.Advance(control.StepSeconds);
        }
        Assert.Equal(control.Frame.Bodies.Select(b => (b.Position, b.State, b.MotionState, b.Energy)), viewed.Frame.Bodies.Select(b => (b.Position, b.State, b.MotionState, b.Energy)));
        Assert.Equal(control.Frame.MaximumExposure, viewed.Frame.MaximumExposure);
        Assert.Equal(control.Frame.Exited, viewed.Frame.Exited); Assert.Equal(control.ExportAuthoredJson("same"), viewed.ExportAuthoredJson("same"));
    }
    [Fact]
    public void ActualSnapshotFacesHelpTargetAndFreezesValuesWhenModelChanges()
    {
        Simulation model = new(randomSeed: 42); model.SpawnAgentsAtPositions(new[] { new Vector2(10, 10), new Vector2(13, 14) });
        Agent helper = model.Agents[0]; helper.Intent = BehaviorIntent.AssistCasualty; helper.AssistTargetAgentId = 1; helper.AssistPauseUntilFrame = 10;
        AgentVisualFrame saved = PresentationFrame.Capture(model).Agents[0];
        Assert.Equal(new Vector2(.6f, .8f), saved.Heading); Assert.Equal(HumanPose.Assisting, saved.Pose);
        helper.Position = new Vector2(30, 30); helper.State = AgentState.Dead; helper.AssistTargetAgentId = null;
        Assert.Equal(new Vector2(10, 10), saved.Position); Assert.Equal(1, saved.HelpTarget); Assert.True(saved.Attending);
        model.Agents[1].State = AgentState.Exited;
        Assert.Single(PresentationFrame.Capture(model).Agents);
    }
}
