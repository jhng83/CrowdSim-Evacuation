using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Compatibility;
using CrowdSim.Core.Persistence;
using CrowdSim.Diagnostics;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CrowdSim.Migration.Tests;

public sealed class PortableDiagnosticsTests
{
    private static string Root
    {
        get
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CrowdSim.sln"))) directory = directory.Parent;
            return directory?.FullName ?? throw new DirectoryNotFoundException();
        }
    }

    [Theory]
    [InlineData("explosion-open", false)]
    [InlineData("explosion-open", true)]
    [InlineData("explosion-furnished", false)]
    [InlineData("explosion-furnished", true)]
    [InlineData("toxic-205", false)]
    [InlineData("toxic-205", true)]
    public void UnityDiagnosticProductionRunnerMatchesReferenceAndRestoresReplay(string preset, bool hybrid)
    {
        string baseline = File.ReadAllText(Path.Combine(Root, "Migration/Parity/Reference", preset + (hybrid ? "-hybrid.json" : "-individual.json")));
        string fixture = File.ReadAllText(Path.Combine(Root, "tests/CrowdSim.Tests/Fixtures/toxic_fume_205.json"));
        DiagnosticSession session = new(preset, hybrid, baseline, fixture);
        Assert.Equal(0, session.Tick);
        while (session.StepOnce()) { }
        Assert.Equal(120, session.Tick);
        Assert.Equal(7, session.ComparedCheckpoints);
        Assert.Empty(session.Failures);
        Assert.True(session.Passed);
        Assert.True(session.ReplayVerified);
        Assert.False(session.StepOnce());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-12345)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void CompatibilityRandomMatchesTheReferenceAcrossMixedCallsAndLargeRanges(int seed)
    {
        Random reference = new(seed);
        ReferenceRandom portable = new(seed);
        for (int i = 0; i < 1000; i++)
        {
            Assert.Equal(reference.Next(), portable.Next());
            Assert.Equal(reference.Next(150), portable.Next(150));
            Assert.Equal(reference.Next(-100, 200), portable.Next(-100, 200));
            Assert.Equal(reference.Next(int.MinValue, int.MaxValue), portable.Next(int.MinValue, int.MaxValue));
            Assert.Equal(reference.NextDouble(), portable.NextDouble());
        }
        byte[] expected = new byte[257], actual = new byte[257];
        reference.NextBytes(expected); portable.NextBytes(actual);
        Assert.Equal(expected, actual);
        reference.NextBytes(expected.AsSpan()); portable.NextBytes(actual.AsSpan());
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CompatibilityHeapMatchesInterleavedEqualPriorityOperationsAndClear()
    {
        PriorityQueue<int, int> reference = new();
        ReferencePriorityQueue<int, int> portable = new();
        Random random = new(42);
        for (int i = 0; i < 4000; i++)
        {
            if (i % 5 != 0 || reference.Count == 0)
            {
                int priority = random.Next(8);
                reference.Enqueue(i, priority); portable.Enqueue(i, priority);
            }
            else Assert.Equal(reference.Dequeue(), portable.Dequeue());
        }
        while (reference.Count > 0) Assert.Equal(reference.Dequeue(), portable.Dequeue());
        Assert.False(portable.TryDequeue(out _, out _));
        portable.Enqueue(1, 1); portable.Clear();
        Assert.Equal(0, portable.Count);
        portable.Enqueue(2, 0); Assert.Equal(2, portable.Dequeue());
    }

    [Fact]
    public void PresentationValuesRemainDetachedWhenPhysicalStateMoves()
    {
        Simulation simulation = DiagnosticPresets.CreatePreset("explosion-open", false, null);
        PresentationFrame frame = PresentationFrame.Capture(simulation);
        ViewBody body = frame.Bodies.Single(b => b.Id == "agent-0");
        Vector3 before = body.Position;
        simulation.Agents[0].Position += new Vector2(3f, 2f);
        Assert.Equal(before, body.Position);
        Assert.NotEqual(body.Position, PresentationFrame.Capture(simulation).Bodies.Single(b => b.Id == "agent-0").Position);
        Assert.Throws<NotSupportedException>(() => ((IList<ViewBody>)frame.Bodies).Clear());
        Assert.Equal(new Vector3(2f, 7f, 3f), PresentationFrame.ToPresentation(new Vector2(2f, 3f), 7f));
    }

    [Theory]
    [InlineData("{version:4}")]
    [InlineData("{\"version\":\"4\"}")]
    [InlineData("{\"version\":4,}")]
    [InlineData("{/*comment*/\"version\":4}")]
    [InlineData("{\"version\":+4}")]
    [InlineData("{\"unknown\":NaN}")]
    [InlineData("{\"agentCount\":04}")]
    [InlineData("{\"scenarioType\":4294967296}")]
    [InlineData("{\"name\":\"raw\nline\"}")]
    public void ScenarioCodecRejectsNonstandardJsonAndPrimitiveCoercion(string json)
        => Assert.ThrowsAny<Exception>(() => ScenarioJson.Deserialize(json));

    [Fact]
    public void FloatToleranceNeverRelaxesDiscreteStateAgreement()
    {
        List<string> failures = new();
        ParityComparison.Compare(JObject.Parse("{\"state\":2,\"x\":1.0}"), JObject.Parse("{\"state\":3,\"x\":1.00001}"), failures);
        Assert.Single(failures);
        Assert.Contains("state", failures[0]);
    }

    [Fact]
    public void FixedStepSchedulerRejectsVariablePhysicsAndBoundsStallCatchup()
    {
        FixedStepScheduler scheduler = new();
        int ticks = 0;
        Assert.Equal(0, scheduler.Advance(0.10, 0.15, 1, () => ticks++, () => false));
        Assert.Equal(1, scheduler.Advance(0.05, 0.15, 1, () => ticks++, () => false));
        Assert.Equal(1, ticks);
        scheduler.Reset();
        scheduler.Advance(999, 0.15, 1, () => ticks++, () => false);
        Assert.InRange(ticks, 2, 3);
        Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Advance(1, 0, 1, () => { }, () => false));
    }
}
