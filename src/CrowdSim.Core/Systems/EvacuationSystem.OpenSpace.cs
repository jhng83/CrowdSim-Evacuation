using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Geometry;
using CrowdSim.Core.Model;

namespace CrowdSim.Core.Systems;

public sealed partial class EvacuationSystem
{
    private readonly List<Agent> _openSpacePeople = new(64);

    /// <summary>Find a reachable local waypoint, including sideways/backward escape from a bottleneck.</summary>
    internal bool TryChooseEarlyOpenSpaceGoal(Simulation simulation, Agent agent, Vector2 forward,
        bool threatEscape, out Vector2 bestGoal)
    {
        bestGoal = default;
        if (simulation.FrameIndex < agent.NextOpenSpaceProbeFrame || forward.LengthSquared() < 1e-8f)
            return false;
        agent.NextOpenSpaceProbeFrame = simulation.FrameIndex + SecondsToFrames(0.30f);
        forward = VectorMath.SafeNormalize(forward);
        simulation.CollectAgentsNearPoint(agent.Position, 3.2f, _openSpacePeople, agent.AgentId);
        float bestScore = float.NegativeInfinity;
        bool found = false;
        // Short options fit around furniture; longer options can escape a cluster.
        ReadOnlySpan<float> lengths = stackalloc float[] { 0.75f, 1.35f, 2.1f };
        foreach (float length in lengths)
        for (int sample = 0; sample < 12; sample++)
        {
            float angle = sample * MathF.Tau / 12f;
            Vector2 direction = Rotate(forward, angle);
            Vector2 candidate = agent.Position + direction * length;
            if (!simulation.HasTraversablePath(agent.Position, candidate, NavigationClearance()) ||
                !OpenSpacePathAvoidsPeople(agent, candidate)) continue;
            if (threatEscape && simulation.Environment.Explosion is Explosion blast &&
                Vector2.Distance(candidate, blast.Position) < Vector2.Distance(agent.Position, blast.Position) -
                    _config.ThreatEscapeCasualtyBypassMaxBacktrackMeters) continue;
            float crowding = 0f;
            foreach (Agent other in _openSpacePeople)
                if (other.IsActive) crowding += Math.Max(0f, 1f - Vector2.Distance(candidate, other.Position) / 0.9f);
            float clearance = Math.Clamp(EndpointClearance(simulation, candidate) / 0.8f, 0f, 1f);
            float progress = Vector2.Dot(direction, forward);
            float sidePreference = ((agent.AgentId & 1) == 0 ? 1f : -1f) * MathF.Sin(angle) * 0.015f;
            // Clear space dominates immediate exit progress; a temporary backward step is legitimate.
            float score = 1.4f * clearance - 2f * crowding + 0.3f * progress + 0.12f * length + sidePreference;
            if (score > bestScore)
            {
                bestScore = score;
                bestGoal = candidate;
                found = true;
            }
        }
        return found;
    }

    private bool OpenSpacePathAvoidsPeople(Simulation simulation, Agent agent, Vector2 goal)
    {
        simulation.CollectAgentsNearPoint(agent.Position, Vector2.Distance(agent.Position, goal) + 1f,
            _openSpacePeople, agent.AgentId);
        return OpenSpacePathAvoidsPeople(agent, goal);
    }

    /// <summary>Preserve physical body space, while allowing escape from pre-existing close contact.</summary>
    private bool OpenSpacePathAvoidsPeople(Agent agent, Vector2 goal)
    {
        int steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(agent.Position, goal) / 0.06f));
        foreach (Agent other in _openSpacePeople)
        {
            if (!other.IsActive) continue;
            float hardRadius = _config.Radius * (other.IsCasualtyObstacle ? 2.25f : 2f);
            float clearance = hardRadius + 0.025f;
            float previous = Vector2.Distance(agent.Position, other.Position);
            for (int step = 1; step <= steps; step++)
            {
                float distance = Vector2.Distance(Vector2.Lerp(agent.Position, goal, step / (float)steps), other.Position);
                if (distance < clearance && distance <= previous + 1e-6f) return false;
                previous = distance;
            }
            if (previous < clearance) return false;
        }
        return true;
    }
}
