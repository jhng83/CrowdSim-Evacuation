using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Geometry;
using CrowdSim.Core.Model;
using CrowdSim.Core.Persistence;
using CrowdSim.Core.Systems;
using Xunit;

namespace CrowdSim.Tests;

public sealed class MetricWorldAndCircularBlockageTests
{
    [Fact]
    public void DefaultWorld_UsesMetresAndFixedOnePointSevenMetreAgents()
    {
        Simulation simulation = new(agentCount: 0, randomSeed: 901);

        Assert.Equal(56.0f, simulation.Config.World.WidthMeters, 3);
        Assert.Equal(34.0f, simulation.Config.World.LengthMeters, 3);
        Assert.Equal(1.70f, simulation.Config.Agent.Height, 3);
        Assert.Equal(0.20f, simulation.Config.Agent.Radius, 3);
        Assert.Equal(1.20f, simulation.Config.Grid.CellSizeMeters, 3);
        Assert.Equal(0.60f, simulation.Environment.BoundaryThickness, 3);
    }

    [Fact]
    public void ClampToBounds_PreservesMetricPrecisionAndUsesFullRoomExtent()
    {
        Vector2 inside = VectorMath.ClampToBounds(new Vector2(55.75f, 33.625f), 56.0f, 34.0f);
        Assert.Equal(55.75f, inside.X, 3);
        Assert.Equal(33.625f, inside.Y, 3);

        Vector2 outside = VectorMath.ClampToBounds(new Vector2(56.8f, -0.4f), 56.0f, 34.0f);
        Assert.Equal(56.0f, outside.X, 3);
        Assert.Equal(0.0f, outside.Y, 3);
    }

    [Fact]
    public void CircularBlockage_IsTrueTwoPointFiveMetreDiameterSolidGeometry()
    {
        Simulation simulation = new(agentCount: 0, randomSeed: 902);

        Assert.True(simulation.Environment.AddCircularBlockageFromClick(28f, 17f));
        CircleBlockage circle = Assert.Single(simulation.Environment.CircularBlockages);

        Assert.Equal(2.50f, circle.Diameter, 3);
        Assert.Equal(1.25f, circle.Radius, 3);
        Assert.True(simulation.Environment.IsPointBlocked(circle.Center));

        Vector2 start = circle.Center - new Vector2(3f, 0f);
        Vector2 goal = circle.Center + new Vector2(3f, 0f);
        Assert.False(simulation.HasTraversablePath(start, goal, simulation.Config.Agent.Radius));

        VisibilityGraphPlanner planner = new(simulation.Config.Agent);
        IReadOnlyList<Vector2> route = planner.Plan(simulation.Environment, start, goal);
        Assert.NotEmpty(route);
        Assert.Equal(goal, route[^1]);
        Assert.True(route.Count > 1, "A circular solid blockage should force an intermediate waypoint.");
    }

    [Fact]
    public void CircularBlockage_UndoRemovesLastPlacedCircle()
    {
        Simulation simulation = new(agentCount: 0, randomSeed: 903);
        Assert.True(simulation.Environment.AddCircularBlockageFromClick(28f, 17f));

        Assert.True(simulation.Environment.UndoLast());
        Assert.Empty(simulation.Environment.CircularBlockages);
    }

    [Fact]
    public void ScenarioRoundTrip_PreservesMetricCircularBlockage()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"crowdsim-metric-{Guid.NewGuid():N}");
        try
        {
            Simulation source = new(agentCount: 0, randomSeed: 904);
            Assert.True(source.Environment.AddCircularBlockageFromClick(28f, 17f));
            CircleBlockage expected = Assert.Single(source.Environment.CircularBlockages);

            ScenarioDefinition captured = ScenarioDefinition.Capture(source, 0, "metric_circle");
            ScenarioFileStore.Save(captured, directory);
            ScenarioDefinition loaded = ScenarioFileStore.Load("metric_circle", directory);

            Assert.Equal(ScenarioDefinition.CurrentVersion, loaded.Version);
            Simulation restored = new(0, loaded.CreateConfig(), loaded.RandomSeed);
            loaded.ApplyLayout(restored);
            CircleBlockage actual = Assert.Single(restored.Environment.CircularBlockages);

            Assert.Equal(expected.Center.X, actual.Center.X, 3);
            Assert.Equal(expected.Center.Y, actual.Center.Y, 3);
            Assert.Equal(CircleBlockage.DefaultRadiusMeters, actual.Radius, 3);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void LegacyScenarioV1_ConvertsWorldCoordinatesToMetres()
    {
        ScenarioDefinition legacy = new()
        {
            Version = 1,
            Name = "legacy",
            RoomWidth = 1380f,
            RoomHeight = 840f,
            BlastZones = new BlastZoneScenario
            {
                Kill = 39f,
                Incapacitation = 78f,
                Fallen = 156f,
                Injury = 234f,
                Panic = 450f,
            },
        };

        ScenarioDefinition metric = ScenarioDefinition.UpgradeLegacyV1(legacy);

        Assert.Equal(2, metric.Version);
        Assert.Equal(1380f * ScenarioDefinition.LegacyMetersPerUnit, metric.RoomWidth, 3);
        Assert.Equal(840f * ScenarioDefinition.LegacyMetersPerUnit, metric.RoomHeight, 3);
        Assert.Equal(39f * ScenarioDefinition.LegacyMetersPerUnit, metric.BlastZones.Kill, 3);
    }
}
