using System.Collections;
using System.Collections.Generic;
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
        public IEnumerator WorldReuseRetainsStationaryViewsButUpdatesMovementColourShapeAndScopedCleanup()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap"); yield return null;
            GameObject host = new GameObject("Retained world acceptance");
            WorkspaceWorldView world = host.AddComponent<WorkspaceWorldView>(); world.Initialise();
            ViewBody Table(float x = 4) => new ViewBody("retained-table", ViewShape.Box, ViewKind.Table,
                new System.Numerics.Vector3(x, .5f, 5), new System.Numerics.Vector3(2, 1, 2));
            world.Draw(new[] { Table() });
            Transform table = host.transform.Find("retained-table"); Material original = table.GetComponent<Renderer>().sharedMaterial;
            Assert.That(world.LastSceneBodyUpdates, Is.EqualTo(1));
            world.Draw(new[] { Table() });
            Assert.That(world.LastSceneBodyUpdates, Is.Zero); Assert.That(world.LastSceneBodyReuses, Is.EqualTo(1));
            Assert.That(host.transform.Find("retained-table"), Is.SameAs(table));
            Assert.That(table.GetComponent<Renderer>().sharedMaterial, Is.SameAs(original));
            world.Draw(new[] { Table(6) });
            Assert.That(table.localPosition.x, Is.EqualTo(6)); Assert.That(world.LastSceneBodyUpdates, Is.EqualTo(1));
            world.Draw(new[] { Table(6) }, "preview-", Color.red);
            Transform preview = host.transform.Find("preview-retained-table");
            world.Draw(new[] { Table(6) }, "preview-", Color.green);
            Assert.That(preview.GetComponent<Renderer>().sharedMaterial.color, Is.EqualTo(Color.green));
            world.Draw(new[] { Table(6) }, "grid-", Color.black);
            world.Draw(new ViewBody[0]); yield return null;
            Assert.That(table == null, Is.True); Assert.That(preview != null, Is.True);
            Assert.That(host.transform.Find("grid-retained-table"), Is.Not.Null);
            world.Draw(new[] { Table() });
            world.Draw(new[] { new ViewBody("retained-table", ViewShape.Cylinder, ViewKind.Block,
                new System.Numerics.Vector3(4, 1, 5), new System.Numerics.Vector3(2, 2, 2), isPillar: true) });
            yield return null;
            Assert.That(host.transform.Find("retained-table").GetComponent<WorkspacePillarOutline>(), Is.Not.Null);
            world.Draw(new[] { Table() }); yield return null;
            Assert.That(host.transform.Find("retained-table").GetComponent<WorkspacePillarOutline>(), Is.Null);
            world.Draw(new ViewBody[0], "preview-"); yield return null;
            Assert.That(preview == null, Is.True); Assert.That(host.transform.Find("retained-table"), Is.Not.Null);
            Object.Destroy(host); yield return null; LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CachedBodyMaterialsKeepExactStateShadesAcrossPoseAndZoomChanges()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap"); yield return null;
            GameObject host = new GameObject("Material cache acceptance");
            WorkspaceWorldView world = host.AddComponent<WorkspaceWorldView>(); world.Initialise();
            world.Humans.PreferGpuInstancing = false;
            Ground position = new Ground(15, 10);
            AgentVisualFrame[] samples = {
                new AgentVisualFrame(0, position, Ground.UnitX, 0, AgentState.Wandering),
                new AgentVisualFrame(0, position, Ground.UnitX, 0, AgentState.Injured),
                new AgentVisualFrame(0, position, Ground.UnitX, 0, AgentState.Wandering, SeatingPosture.Seated),
                new AgentVisualFrame(0, position, Ground.UnitX, 0, AgentState.Wandering, SeatingPosture.StandingUp, .4f),
                new AgentVisualFrame(0, position, Ground.UnitX, 0, AgentState.Crawling),
                new AgentVisualFrame(0, position, Ground.UnitX, 0, AgentState.Dead),
                new AgentVisualFrame(0, position, Ground.UnitX, 0, AgentState.Evacuating, helpTarget: 1, attending: true) };
            List<HumanPart> expected = new List<HumanPart>(); int tick = 0;
            foreach (float zoom in new[] { .45f, 2.5f })
            {
                world.Zoom = zoom; world.Fit(30, 20, new Rect(.1f, .1f, .8f, .8f));
                foreach (AgentVisualFrame sample in samples)
                {
                    world.Humans.RenderSamples(new[] { sample }, tick++, 0, false, 1, .15f, null, AgentCameraMode.Overview);
                    bool detail = Vector2.Distance(world.ViewCamera.WorldToScreenPoint(new Vector3(15, 0, 10)),
                        world.ViewCamera.WorldToScreenPoint(new Vector3(15, 1.7f, 10))) >= 48;
                    HumanGeometry.Build(sample, detail, 0, expected, sample.Rise);
                    MeshRenderer[] renderers = world.Humans.GetComponentsInChildren<MeshRenderer>().Where(r => r.name == "Body part").ToArray();
                    Assert.That(renderers.Length, Is.EqualTo(expected.Count));
                    for (int i = 0; i < expected.Count; i++)
                    {
                        System.Numerics.Vector3 rgb = AgentPalette.Color(sample.State) * expected[i].Shade;
                        Color actual = renderers[i].sharedMaterial.color;
                        Assert.That(actual.r, Is.EqualTo(rgb.X).Within(.0001f)); Assert.That(actual.g, Is.EqualTo(rgb.Y).Within(.0001f));
                        Assert.That(actual.b, Is.EqualTo(rgb.Z).Within(.0001f));
                    }
                }
            }
            Object.Destroy(host); yield return null; LogAssert.NoUnexpectedReceived();
        }

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
