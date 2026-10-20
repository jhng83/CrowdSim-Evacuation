using System;
using System.IO;
using System.Linq;
using CrowdSim.Core.Model;
using CrowdSim.Diagnostics;
using CrowdSim.Workspace;
using NUnit.Framework;
using UnityEngine;

namespace CrowdSim.Unity.Tests
{
    public sealed class ScenarioFileContractTests
    {
        [TestCase("legacy_v1.json", 4)]
        [TestCase("legacy_v2.json", 0)]
        [TestCase("legacy_v3.json", 205)]
        [TestCase("toxic_fume_205.json", 205)]
        public void AllBundledSchemaVersionsLoadIntoAPausedBuilderAndExportV4(string name, int population)
        {
            WorkspaceSession session = WorkspaceSession.FromJson(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Scenarios", name)));
            Assert.That(session.IsBuilding && !session.Running, Is.True);
            Assert.That(session.PlannedCount, Is.EqualTo(population));
            Assert.That(session.IsDirty, Is.False);
            Assert.That(session.ExportAuthoredJson("copy"), Does.Contain("\"version\": 4"));
        }
        [Test]
        public void HostFilesystemSaveLoadAndReplayKeepTheAuthoredStarts()
        {
            string directory = Path.Combine(Path.GetTempPath(), "crowdsim-unity-files-" + Guid.NewGuid().ToString("N"));
            try
            {
                WorkspaceFiles files = new WorkspaceFiles(WorkspaceExamples.Furniture(), new ScenarioLibrary(directory));
                Assert.That(files.Save("editor<> name"), Is.True);
                Assert.That(files.CurrentEntry.Name, Is.EqualTo("editor_name"));
                WorkspaceFiles reloaded = new WorkspaceFiles(new WorkspaceSession(), new ScenarioLibrary(directory));
                Assert.That(reloaded.Load(reloaded.Entries.Single()), Is.True);
                Assert.That(reloaded.Finalise(), Is.True);
                var starts = reloaded.Session.Frame.Bodies.Where(b => b.Kind == ViewKind.Agent).Select(b => b.Position).ToArray();
                reloaded.Session.ToggleRunning(); reloaded.Session.TriggerExplosion(new System.Numerics.Vector2(18, 12));
                for (int i = 0; i < 120; i++) reloaded.Session.Advance(reloaded.Session.StepSeconds);
                Assert.That(reloaded.Replay(), Is.True);
                Assert.That(reloaded.Session.Tick, Is.Zero);
                Assert.That(reloaded.Session.Frame.SeatedAgents, Is.EqualTo(11));
                Assert.That(reloaded.Session.Frame.Bodies.Where(b => b.Kind == ViewKind.Agent).Select(b => b.Position), Is.EqualTo(starts));
                Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
        [Test]
        public void Loaded205PersonFumeSceneReplaysWithARegeneratedUnreleasedCache()
        {
            WorkspaceSession session = WorkspaceSession.FromJson(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Scenarios", "toxic_fume_205.json")));
            Assert.That(session.Finalise(), Is.True);
            Assert.That(session.Frame.Population, Is.EqualTo(205));
            session.ToggleRunning(); Assert.That(session.ReleaseSavedFume(), Is.True);
            for (int i = 0; i < 120; i++) session.Advance(session.StepSeconds);
            Assert.That(session.Replay(), Is.True);
            Assert.That(session.HasFumeCache && !session.FumeReleased, Is.True);
            Assert.That(session.Frame.SeatedAgents, Is.EqualTo(80));
            Assert.That(session.Frame.FurnitureBodies.All(b => b.State == ChairState.Upright), Is.True);
            Assert.That(session.Tick, Is.Zero);
        }
        [Test]
        public void DirtyAndOverwriteDecisionsKeepTheCurrentSessionUntilConfirmed()
        {
            string directory = Path.Combine(Path.GetTempPath(), "crowdsim-unity-dirty-" + Guid.NewGuid().ToString("N"));
            try
            {
                WorkspaceFiles files = new WorkspaceFiles(WorkspaceExamples.Furniture(), new ScenarioLibrary(directory));
                Assert.That(files.Save("keep"), Is.True);
                Assert.That(files.Save("keep", saveAs: true), Is.False);
                Assert.That(files.OverwriteName, Is.EqualTo("keep"));
                files.CancelOverwrite();
                files.Session.SetBehaviour(0, .2f);
                WorkspaceSession old = files.Session;
                Assert.That(files.New(ScenarioType.Explosion), Is.False);
                files.Cancel(); Assert.That(files.Session, Is.SameAs(old));
                Assert.That(files.Session.IsDirty, Is.True);
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
        [Test]
        public void LegacyRandomStartsRepeatAtFrameZero()
        {
            WorkspaceSession session = WorkspaceSession.FromJson(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Scenarios", "legacy_v1.json")));
            Assert.That(session.UsesRandomStarts, Is.True);
            session.Finalise();
            var positions = session.Frame.Bodies.Where(b => b.Kind == ViewKind.Agent).Select(b => b.Position).ToArray();
            session.ToggleRunning(); for (int i = 0; i < 10; i++) session.Advance(session.StepSeconds);
            session.Replay();
            Assert.That(session.Frame.Bodies.Where(b => b.Kind == ViewKind.Agent).Select(b => b.Position), Is.EqualTo(positions));
        }
    }
}
