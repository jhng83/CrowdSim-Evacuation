using System.Collections;
using System.Linq;
using CrowdSim.Core.Model;
using CrowdSim.Diagnostics;
using CrowdSim.Unity.Workspace;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Ground = System.Numerics.Vector2;

namespace CrowdSim.Unity.Tests
{
    public sealed class InstancedCrowdInteractionTests
    {
        [UnityTest]
        public IEnumerator GpuVolumesRemainPickableAndPovHiddenWithoutBodyRenderersAndFallbackRetainsPoses()
        {
            if (!SystemInfo.supportsInstancing) Assert.Ignore("This graphics device cannot execute GPU instancing.");
            yield return SceneManager.LoadSceneAsync("Bootstrap"); yield return null;
            GameObject host = new GameObject("Instancing acceptance");
            WorkspaceWorldView world = host.AddComponent<WorkspaceWorldView>(); world.Initialise();
            world.Fit(30, 20, new Rect(.1f, .1f, .8f, .8f));
            AgentVisualFrame[] samples = {
                new AgentVisualFrame(0, new Ground(5, 10), Ground.UnitX, 0, AgentState.Evacuating, panic: .7f),
                new AgentVisualFrame(1, new Ground(12, 10), Ground.UnitX, 0, AgentState.Wandering, SeatingPosture.Seated),
                new AgentVisualFrame(2, new Ground(19, 10), Ground.UnitX, 0, AgentState.Crawling),
                new AgentVisualFrame(3, new Ground(26, 10), Ground.UnitX, 0, AgentState.Dead) };
            world.Humans.RenderSamples(samples, 0, 0, false, 1, .15f, 0, AgentCameraMode.Overview); yield return null;
            Assert.That(world.Humans.UsingGpuInstancing, Is.True, "The bundled instancing shader must compile on this supported device.");
            Assert.That(world.Humans.GpuInstances, Is.GreaterThan(48)); Assert.That(world.Humans.GpuDraws, Is.GreaterThan(0));
            Assert.That(world.Humans.GpuDraws, Is.LessThan(world.Humans.GpuInstances));
            Assert.That(world.Humans.GetComponentsInChildren<MeshRenderer>(true).Length, Is.Zero);
            Assert.That(world.Humans.GetComponentsInChildren<LineRenderer>(true).Length, Is.Zero);
            Assert.That(world.Humans.GetComponentsInChildren<Collider>(true).Length, Is.Zero);
            foreach (AgentVisualFrame a in samples)
            {
                float height = a.Id == 3 ? .17f : a.Id == 2 ? .34f : a.Id == 1 ? .8f : 1.1f;
                Vector3 screen = world.ViewCamera.WorldToScreenPoint(new Vector3(a.Position.X, height, a.Position.Y));
                Assert.That(world.Humans.Pick(screen), Is.EqualTo(a.Id));
            }
            world.Humans.RenderSamples(samples, 0, 0, false, 1, .15f, 0, AgentCameraMode.Pov);
            Assert.That(world.transform.Find("Humanoid presentation/Human 0").gameObject.activeSelf, Is.False);
            world.Humans.PreferGpuInstancing = false;
            world.Humans.RenderSamples(samples, 0, 0, false, 1, .15f, null, AgentCameraMode.Overview); yield return null;
            Assert.That(world.Humans.UsingGpuInstancing, Is.False);
            Assert.That(world.Humans.GetComponentsInChildren<MeshRenderer>().Length, Is.GreaterThan(0));
            foreach (AgentVisualFrame a in samples) Assert.That(world.Humans.RenderedPosition(a.Id), Is.EqualTo(a.Position));
            world.Humans.PreferGpuInstancing = true;
            world.Humans.RenderSamples(samples, 0, 0, false, 1, .15f, null, AgentCameraMode.Overview); yield return null;
            Assert.That(world.Humans.GetComponentsInChildren<MeshRenderer>().Length, Is.Zero);
            world.Humans.RenderSamples(new AgentVisualFrame[0], 1, 0, false, 1, .15f, null, AgentCameraMode.Overview);
            Assert.That(world.Humans.GpuInstances, Is.Zero); Assert.That(world.Humans.GpuDraws, Is.Zero);
            Object.Destroy(host); yield return null; LogAssert.NoUnexpectedReceived();
        }
    }
}
