using System.IO;
using CrowdSim.Diagnostics;
using NUnit.Framework;
using UnityEngine;

namespace CrowdSim.Unity.Tests
{
    public sealed class PortableParityTests
    {
        [TestCase("explosion-open", false)]
        [TestCase("explosion-open", true)]
        [TestCase("explosion-furnished", false)]
        [TestCase("explosion-furnished", true)]
        [TestCase("toxic-205", false)]
        [TestCase("toxic-205", true)]
        public void HostRuntimeMatchesSevenReferenceCheckpointsAndReplay(string preset, bool hybrid)
        {
            string name = preset + (hybrid ? "-hybrid" : "-individual");
            string baseline = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Parity", name + ".json"));
            string fixture = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Scenarios", "toxic_fume_205.json"));
            DiagnosticSession session = new DiagnosticSession(preset, hybrid, baseline, fixture);
            while (session.StepOnce()) { }
            Assert.That(session.Tick, Is.EqualTo(120));
            Assert.That(session.ComparedCheckpoints, Is.EqualTo(7));
            Assert.That(session.Failures, Is.Empty, string.Join("\n", session.Failures));
            Assert.That(session.Passed && session.ReplayVerified, Is.True);
        }
    }
}
