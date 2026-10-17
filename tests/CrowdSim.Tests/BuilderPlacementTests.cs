using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Persistence;
using CrowdSim.Core.Systems;
using Xunit;

namespace CrowdSim.Tests;

public sealed class BuilderPlacementTests
{
    [Fact]
    public void BlueWall_IsTwoGridCellsAndActsAsSolidGeometry()
    {
        Simulation simulation = new(agentCount: 0, randomSeed: 101);

        Assert.True(simulation.Environment.AddBlueWallFromClick(28f, 17f));
        Assert.Single(simulation.Environment.BlueWalls);

        var wall = simulation.Environment.BlueWalls[0];
        float expectedSide = 2f * simulation.Config.Grid.CellSizeMeters;
        Assert.Equal(expectedSide, wall.X2 - wall.X1, 3);
        Assert.Equal(expectedSide, wall.Y2 - wall.Y1, 3);
        Assert.True(simulation.Environment.IsPointBlocked(
            new Vector2((wall.X1 + wall.X2) * 0.5f, (wall.Y1 + wall.Y2) * 0.5f)));
    }


    [Fact]
    public void Blockage_CanBeOneOrTwoGridCells()
    {
        Simulation oneCellSimulation = new(agentCount: 0, randomSeed: 151);
        Assert.True(oneCellSimulation.Environment.AddBlockageFromClick(20f, 14f, cells: 1));
        var oneCell = oneCellSimulation.Environment.Blockages.Single();
        Assert.Equal(oneCellSimulation.Config.Grid.CellSizeMeters, oneCell.X2 - oneCell.X1, 3);
        Assert.Equal(oneCellSimulation.Config.Grid.CellSizeMeters, oneCell.Y2 - oneCell.Y1, 3);

        Simulation twoCellSimulation = new(agentCount: 0, randomSeed: 152);
        Assert.True(twoCellSimulation.Environment.AddBlockageFromClick(20f, 14f, cells: 2));
        var twoCell = twoCellSimulation.Environment.Blockages.Single();
        Assert.Equal(2f * twoCellSimulation.Config.Grid.CellSizeMeters, twoCell.X2 - twoCell.X1, 3);
        Assert.Equal(2f * twoCellSimulation.Config.Grid.CellSizeMeters, twoCell.Y2 - twoCell.Y1, 3);
    }

    [Fact]
    public void Exit_CanBeFivePersonsWide()
    {
        Simulation simulation = new(agentCount: 0, randomSeed: 153);
        float clickX = simulation.Environment.Width / 2f;
        float clickY = simulation.Environment.BoundaryThickness / 2f;

        Assert.True(simulation.Environment.AddExitFromClick(clickX, clickY, personsWide: 5));
        var exit = simulation.Environment.Exits.Single();
        float expectedWidth = 5f * 2f * simulation.Config.Agent.Radius;
        Assert.Equal(expectedWidth, exit.Size * 2);
    }

    [Fact]
    public void AgentPlacement_FiftyAgentsSpreadsAroundBlockedFloor()
    {
        Simulation simulation = new(agentCount: 0, randomSeed: 202);
        Assert.True(simulation.Environment.AddBlockageFromClick(28f, 17f));

        Assert.True(simulation.Environment.AddAgentSpawnGroupFromClick(28f, 17f, 50));
        Assert.Equal(50, simulation.Environment.PlannedAgentCount);

        List<Vector2> positions = simulation.Environment.GetPlannedAgentPositions();
        Assert.Equal(50, positions.Count);
        Assert.All(
            positions,
            position => Assert.False(
                simulation.Environment.IsPointBlocked(position, simulation.Config.Agent.Radius)));

        float minimumDistance = simulation.Config.Agent.Radius * 2f;
        for (int i = 0; i < positions.Count; i++)
        {
            for (int j = i + 1; j < positions.Count; j++)
            {
                Assert.True(
                    Vector2.Distance(positions[i], positions[j]) >= minimumDistance,
                    $"Planned agents {i} and {j} overlap.");
            }
        }
    }

    [Fact]
    public void SpawnAgentsAtPositions_UsesMousePlannedStartPositionsExactly()
    {
        Simulation simulation = new(agentCount: 0, randomSeed: 303);
        Assert.True(simulation.Environment.AddAgentSpawnGroupFromClick(24f, 15f, 15));
        List<Vector2> starts = simulation.Environment.GetPlannedAgentPositions();

        simulation.SpawnAgentsAtPositions(starts);

        Assert.Equal(starts.Count, simulation.Agents.Count);
        for (int i = 0; i < starts.Count; i++)
        {
            Assert.Equal(starts[i], simulation.Agents[i].Position);
        }
    }

    [Fact]
    public void VisibilityPlanner_RoutesAroundBlueWallLikeAnyOtherSolidObstacle()
    {
        Simulation simulation = new(agentCount: 0, randomSeed: 353);
        Assert.True(simulation.Environment.AddBlueWallFromClick(28f, 17f));
        var wall = simulation.Environment.BlueWalls.Single();
        Vector2 start = new(wall.X1 - 3.24f, (wall.Y1 + wall.Y2) * 0.5f);
        Vector2 goal = new(wall.X2 + 3.24f, (wall.Y1 + wall.Y2) * 0.5f);

        VisibilityGraphPlanner planner = new(simulation.Config.Agent);
        IReadOnlyList<Vector2> route = planner.Plan(simulation.Environment, start, goal);

        Assert.NotEmpty(route);
        Assert.Equal(goal, route[^1]);
        Assert.True(route.Count > 1, "A blue wall should force at least one intermediate waypoint.");
    }

    [Fact]
    public void RoomResize_WorksBeforePlacementAndLocksAfterSceneConstructionStarts()
    {
        Simulation simulation = new(agentCount: 0, randomSeed: 404);

        Assert.True(simulation.Environment.TryResizeRoom(40f, 28f));
        Assert.Equal(40f, simulation.Environment.Width, 3);
        Assert.Equal(28f, simulation.Environment.Height, 3);
        Assert.Equal(40f, simulation.Config.World.WidthMeters, 3);
        Assert.Equal(28f, simulation.Config.World.LengthMeters, 3);

        Assert.True(simulation.Environment.AddExitFromClick(
            simulation.Environment.Width / 2,
            simulation.Environment.BoundaryThickness / 2,
            personsWide: 3));
        Assert.False(simulation.Environment.TryResizeRoom(48f, 32f));
        Assert.Equal(40f, simulation.Environment.Width, 3);
        Assert.Equal(28f, simulation.Environment.Height, 3);
    }

    [Fact]
    public void ScenarioRoundTrip_PreservesRoomBlueWallsAndMouseSpawnGroups()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"crowdsim-builder-{Guid.NewGuid():N}");
        try
        {
            Simulation source = new(agentCount: 0, randomSeed: 505);
            Assert.True(source.Environment.TryResizeRoom(44f, 29f));
            Assert.True(source.Environment.AddExitFromClick(
                source.Environment.Width / 2,
                source.Environment.BoundaryThickness / 2,
                personsWide: 3));
            Assert.True(source.Environment.AddBlueWallFromClick(12f, 12f));
            Assert.True(source.Environment.AddAgentSpawnGroupFromClick(30f, 18f, 15));

            ScenarioDefinition captured = ScenarioDefinition.Capture(
                source,
                requestedAgentCount: 0,
                name: "builder_roundtrip");
            ScenarioFileStore.Save(captured, directory);
            ScenarioDefinition loaded = ScenarioFileStore.Load("builder_roundtrip", directory);

            Simulation restored = new(0, loaded.CreateConfig(), loaded.RandomSeed);
            loaded.ApplyLayout(restored);

            Assert.Equal(44f, restored.Environment.Width, 3);
            Assert.Equal(29f, restored.Environment.Height, 3);
            Assert.Single(restored.Environment.BlueWalls);
            Assert.Equal(15, restored.Environment.PlannedAgentCount);
            List<Vector2> expectedStarts = source.Environment.GetPlannedAgentPositions();
            List<Vector2> restoredStarts = restored.Environment.GetPlannedAgentPositions();
            Assert.Equal(expectedStarts.Count, restoredStarts.Count);
            for (int index = 0; index < expectedStarts.Count; index++)
            {
                Assert.Equal(expectedStarts[index], restoredStarts[index]);
            }
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
