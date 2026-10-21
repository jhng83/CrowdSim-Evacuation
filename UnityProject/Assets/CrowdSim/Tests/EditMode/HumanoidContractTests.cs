using System.Collections.Generic;
using System.Linq;
using CrowdSim.Core.Model;
using CrowdSim.Diagnostics;
using CrowdSim.Workspace;
using NUnit.Framework;
using Ground = System.Numerics.Vector2;

namespace CrowdSim.Unity.Tests
{
    public sealed class HumanoidContractTests
    {
        [Test]
        public void EveryActiveStateHasPositiveVolumeGeometryAtBothDetailLevels()
        {
            List<HumanPart> parts = new List<HumanPart>();
            foreach (AgentState state in System.Enum.GetValues(typeof(AgentState)))
            {
                if (state == AgentState.Exited) continue;
                AgentVisualFrame agent = new AgentVisualFrame(0, Ground.Zero, Ground.UnitX, 1, state);
                foreach (bool detail in new[] { false, true })
                {
                    HumanGeometry.Build(agent, detail, 1, parts);
                    Assert.That(parts.Count, Is.GreaterThanOrEqualTo(6));
                    Assert.That(parts.All(p => p.Size.X > 0 && p.Size.Y > 0 && p.Size.Z > 0), Is.True);
                }
            }
        }
        [Test]
        public void AssistanceAlarmAndPanicRemainSeparateFromBodyColourAndHealth()
        {
            AgentVisualFrame helper = new AgentVisualFrame(7, Ground.Zero, Ground.UnitX, 0, AgentState.Evacuating,
                panic: .8f, helpTarget: 2, attending: true);
            Assert.That(helper.Pose, Is.EqualTo(HumanPose.Assisting)); Assert.That(helper.Indicator, Is.EqualTo("+ HELPING 2"));
            Assert.That(helper.ShowPanic, Is.True); Assert.That(helper.Alarm, Is.False);
            Assert.That(AgentPalette.Color(helper.State), Is.EqualTo(AgentPalette.Color(AgentState.Evacuating)));
        }
        [TestCase(.45f)] [TestCase(1f)] [TestCase(2.5f)]
        public void CameraUsesHeadHeightAndCorrectGroundToUnityAxes(float zoom)
        {
            AgentVisualFrame agent = new AgentVisualFrame(0, new Ground(12, 8), Ground.UnitY, 1, AgentState.Evacuating);
            AgentCameraFrame pov = AgentCameraRig.Calculate(60, 40, 1.6f, -35, 34, zoom, AgentCameraMode.Pov, agent);
            Assert.That(pov.Eye, Is.EqualTo(new System.Numerics.Vector3(12, 1.496f, 8)));
            Assert.That(pov.Target.Z, Is.GreaterThan(pov.Eye.Z)); Assert.That(pov.Orthographic, Is.False);
            Assert.That(AgentCameraFrame.FieldOfView, Is.EqualTo(72)); Assert.That(AgentCameraFrame.NearPlane, Is.EqualTo(.04f));
        }
        [Test]
        public void SelectionAndReplayDoNotOpenAHiddenInspectorAndResetCamera()
        {
            WorkspaceSession session = new WorkspaceSession();
            session.Place(PlacementTool.Exit, new Ground(0, 17), 3); session.Place(PlacementTool.Agents, new Ground(24, 17), 1); session.Finalise();
            session.ToggleInspector(); session.SelectAgentById(0); session.SetCameraMode(AgentCameraMode.Pov);
            Assert.That(session.InspectorVisible, Is.False); session.Replay();
            Assert.That(session.SelectedAgentId, Is.Null); Assert.That(session.CameraMode, Is.EqualTo(AgentCameraMode.Overview));
            Assert.That(session.InspectorVisible, Is.False);
        }
        [Test]
        public void InterpolationDoesNotExtrapolateOrAnimateDuringPause()
        {
            HumanMotion motion = new HumanMotion(4); motion.Observe(0, Ground.Zero); motion.Observe(1, Ground.One);
            motion.Advance(.075f, true, 1, .15f, 1); Assert.That(motion.Position, Is.EqualTo(Ground.One * .5f));
            float phase = motion.Phase; motion.Advance(10, false, 4, .15f, 2);
            Assert.That(motion.Position, Is.EqualTo(Ground.One)); Assert.That(motion.Phase, Is.EqualTo(phase));
        }
    }
}
