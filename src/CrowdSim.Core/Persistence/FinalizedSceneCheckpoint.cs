using CrowdSim.Core.Configuration;

namespace CrowdSim.Core.Persistence;

/// <summary>
/// Immutable-in-practice configuration checkpoint captured when the builder finalises a scene.
/// Runtime positions, hazard effects, injuries, chair damage and elapsed time are intentionally
/// excluded, so every replay returns to the ready-but-not-started state.
/// </summary>
public sealed class FinalizedSceneCheckpoint
{
    private FinalizedSceneCheckpoint(ScenarioDefinition definition)
    {
        Definition = definition;
    }

    public ScenarioDefinition Definition { get; }

    /// <summary>Capture the exact authored scene that is about to become a simulation run.</summary>
    public static FinalizedSceneCheckpoint Capture(
        Simulation simulation,
        int requestedAgentCount,
        string scenarioName)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ScenarioDefinition definition = ScenarioDefinition.Capture(
            simulation,
            requestedAgentCount,
            scenarioName);
        definition.ValidateBasic();
        return new FinalizedSceneCheckpoint(definition);
    }

    /// <summary>
    /// Recreate frame zero with the same seed and authored population. Toxic-fume fields are
    /// recomputed because their cache is runtime data derived from the restored static layout.
    /// </summary>
    public Simulation CreateReadySimulation()
    {
        SimulationConfig config = Definition.CreateConfig();
        Simulation simulation = new(0, config, Definition.RandomSeed);
        Definition.ApplyLayout(simulation);

        if (simulation.ScenarioType == CrowdSim.Core.Model.ScenarioType.ToxicFume &&
            simulation.ToxicFumeSystem.SourcePosition is not null)
        {
            simulation.PrecomputeToxicFume();
        }

        if (simulation.Environment.PlannedAgentCount > 0)
        {
            simulation.SpawnAgentsAtPositions(simulation.Environment.GetPlannedAgentPositions());
        }
        else
        {
            simulation.SpawnAgents(Definition.AgentCount);
        }

        simulation.SetRunning(false);
        return simulation;
    }
}
