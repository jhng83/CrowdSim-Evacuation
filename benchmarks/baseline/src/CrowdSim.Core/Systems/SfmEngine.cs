using System.Numerics;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Geometry;
using CrowdSim.Core.Model;

namespace CrowdSim.Core.Systems;

/// <summary>
/// Panic-influenced Helbing-style Social Force Model. The implementation keeps the validated
/// force caps, vision-cone gate, interaction cutoff, pressure permeability, wall-follow wall-force
/// suppression and final non-finite guard from physics_sfm.py.
/// </summary>
public sealed class SfmEngine
{
    public const float MaxRepulsion = 6000f;

    private readonly AgentConfig _config;
    private readonly List<Agent> _nearbyScratch = new(96);
    private readonly List<Wall> _nearbyWallScratch = new(24);
    private readonly HashSet<Wall> _nearbyWallSeenScratch = new();

    public SfmEngine(AgentConfig config)
    {
        _config = config;
    }

    public bool InVisionCone(
        Vector2 agentPosition,
        Vector2 agentHeading,
        Vector2 otherPosition,
        float maxRange,
        float cosHalfAngle)
    {
        Vector2 difference = otherPosition - agentPosition;
        float distance = difference.Length();
        if (!float.IsFinite(distance) || distance > maxRange || distance < VectorMath.Epsilon)
        {
            return false;
        }

        Vector2 normalized = difference / distance;
        return Vector2.Dot(agentHeading, normalized) >= cosHalfAngle;
    }

    /// <summary>Compute desired, social and wall forces for one agent.</summary>
    public Vector2 ComputeForces(Simulation simulation, Agent agent, Vector2 desiredDirection)
    {
        Vector2 velocity = VectorMath.IsFinite(agent.Velocity) ? agent.Velocity : Vector2.Zero;
        Vector2 desiredDir = VectorMath.SafeNormalize(desiredDirection);

        float desiredMagnitude = agent.CurrentDesiredSpeed;
        Vector2 desiredVelocity = desiredDir * desiredMagnitude;
        float bottleneckModeFactor = agent.BottleneckMode switch
        {
            BottleneckResponseMode.SeekGap => 0.35f,
            BottleneckResponseMode.Press => 1.00f,
            _ => 0f,
        };
        float relaxationScale = 1f -
            _config.BottleneckPressRelaxationReduction *
            Math.Clamp(agent.BottleneckPressure, 0f, 1f) *
            bottleneckModeFactor;
        float relaxationTime = Math.Max(
            0.20f,
            _config.RelaxationTime * Math.Clamp(relaxationScale, 0.55f, 1f));
        Vector2 desiredForce = (desiredVelocity - velocity) / relaxationTime;

        Vector2 socialForce = Vector2.Zero;
        float interactionRadius = Math.Max(0f, _config.SfmInteractionRadius);
        float interactionRadiusSquared = interactionRadius * interactionRadius;

        Vector2 heading;
        float velocityMagnitude = velocity.Length();
        if (velocityMagnitude > VectorMath.Epsilon)
        {
            heading = velocity / velocityMagnitude;
        }
        else if (agent.ExitTarget is not null)
        {
            heading = VectorMath.SafeNormalize(agent.ExitTarget.Position - agent.Position);
        }
        else
        {
            heading = Vector2.Zero;
        }

        float cosHalfAngle = MathF.Cos(_config.VisionHalfAngleDeg * MathF.PI / 180f);

        // The old implementation scanned the entire population for every pedestrian and then
        // rejected almost everyone by vision/cutoff distance. Query the simulation's existing
        // spatial hash first. Sorting the small candidate set by AgentId preserves deterministic
        // pair-force accumulation order from the former global-list traversal.
        float neighbourQueryRadius = interactionRadius > 0f
            ? Math.Min(_config.VisionRange, interactionRadius)
            : _config.VisionRange;
        simulation.CollectNearbyAgents(agent, neighbourQueryRadius, _nearbyScratch);
        _nearbyScratch.Sort(static (left, right) => left.AgentId.CompareTo(right.AgentId));

        foreach (Agent other in _nearbyScratch)
        {
            if (!other.IsActive)
            {
                continue;
            }

            if (!InVisionCone(
                    agent.Position,
                    heading,
                    other.Position,
                    _config.VisionRange,
                    cosHalfAngle))
            {
                continue;
            }

            Vector2 difference = agent.Position - other.Position;
            if (!VectorMath.IsFinite(difference))
            {
                continue;
            }

            float distanceSquared = difference.LengthSquared();
            if (interactionRadiusSquared > 0f && distanceSquared > interactionRadiusSquared)
            {
                continue;
            }

            if (distanceSquared < 1e-10f || !float.IsFinite(distanceSquared))
            {
                continue;
            }

            float distance = MathF.Sqrt(distanceSquared);
            if (distance < VectorMath.Epsilon)
            {
                continue;
            }

            Vector2 normal = difference / distance;
            float bodyContactRadius = 2f * _config.Radius;

            if (other.State is AgentState.Crawling or AgentState.Fallen or AgentState.TempIncap or
                AgentState.Incapacitated or AgentState.Dead)
            {
                float pressure = Math.Min(1f, agent.TimeStuck / 3f);
                bodyContactRadius *= 1f - 0.4f * pressure;
            }

            // Personal-space compression changes anticipatory social repulsion, not the hard
            // physical-contact radius. Highly stressed pedestrians may accept closer spacing,
            // but body/contact and collision guardrails remain fully active.
            float panic = Math.Clamp(agent.PanicLevel, 0f, 1f);
            float panicCompression = _config.Behavior.PanicSpaceCompression * panic;
            float localPressure = Math.Clamp(agent.BottleneckPressure, 0f, 1f);
            float bottleneckCompression =
                _config.Behavior.BottleneckSpaceCompression *
                localPressure *
                bottleneckModeFactor;
            float totalCompression = Math.Clamp(
                panicCompression + bottleneckCompression,
                0f,
                0.45f);
            float personalSpaceScale =
                _config.Behavior.PersonalSpace *
                Math.Clamp(agent.PersonalSpaceBias, 0.80f, 1.20f) *
                (1f - totalCompression);
            personalSpaceScale = Math.Clamp(personalSpaceScale, 0.70f, 1.50f);
            float socialRadius = bodyContactRadius * personalSpaceScale;

            float exponent = (socialRadius - distance) / Math.Max(_config.SfmB, 1e-5f);
            exponent = Math.Clamp(exponent, -60f, 20f); // numerical guard; does not change nominal range
            Vector2 pairForce = _config.SfmA * MathF.Exp(exponent) * normal;

            if (distance < bodyContactRadius)
            {
                float overlap = bodyContactRadius - distance;
                Vector2 tangent = VectorMath.Perpendicular(normal);
                Vector2 deltaVelocity = other.Velocity - velocity;
                Vector2 bodyForce = _config.SfmK * overlap * normal;
                Vector2 slideForce =
                    _config.SfmKappa * overlap * Vector2.Dot(deltaVelocity, tangent) * tangent;
                pairForce += bodyForce + slideForce;
            }

            pairForce = SoftCap(pairForce, MaxRepulsion);
            if (VectorMath.IsFinite(pairForce))
            {
                socialForce += pairForce;
            }
        }

        Vector2 wallForce = Vector2.Zero;
        bool wallFollowing = agent.WallFollowDir is not null && agent.WallFollowUntil > simulation.FrameIndex;
        if (!wallFollowing)
        {
            // Static wall force falls off exponentially over SfmB (0.08 by default), so walls
            // more than a small margin outside body contact contribute effectively zero force.
            // Preserve the exact full-wall scan for simple layouts; builder-heavy scenes use the
            // existing wall spatial grid and avoid O(agent x wall) work every physics step.
            IReadOnlyList<Wall> walls;
            if (simulation.Environment.Walls.Count <= 12)
            {
                walls = simulation.Environment.Walls;
            }
            else
            {
                float wallQueryRadius = _config.Radius + Math.Max(0.08f, 25f * _config.SfmB);
                simulation.CollectNearbyWalls(
                    agent.Position,
                    wallQueryRadius,
                    _nearbyWallScratch,
                    _nearbyWallSeenScratch);
                walls = _nearbyWallScratch;
            }

            foreach (Wall wall in walls)
            {
                Vector2 closest = wall.ClosestPoint(agent.Position);
                Vector2 difference = agent.Position - closest;
                if (!VectorMath.IsFinite(difference))
                {
                    continue;
                }

                float distance = difference.Length();
                if (distance < VectorMath.Epsilon || !float.IsFinite(distance))
                {
                    continue;
                }

                Vector2 normal = difference / distance;
                float radius = _config.Radius;
                float exponent = (radius - distance) / Math.Max(_config.SfmB, 1e-5f);
                exponent = Math.Clamp(exponent, -60f, 20f);
                Vector2 force = _config.SfmA * MathF.Exp(exponent) * normal;

                if (distance < radius)
                {
                    float overlap = radius - distance;
                    Vector2 tangent = VectorMath.Perpendicular(normal);
                    Vector2 bodyForce = _config.SfmK * overlap * normal;
                    Vector2 slideForce =
                        _config.SfmKappa * overlap * Vector2.Dot(-velocity, tangent) * tangent;
                    force += bodyForce + slideForce;
                }

                force = SoftCap(force, MaxRepulsion);
                if (VectorMath.IsFinite(force))
                {
                    wallForce += force;
                }
            }

            foreach (CircleBlockage circle in simulation.Environment.CircularBlockages)
            {
                Vector2 difference = agent.Position - circle.Center;
                float centerDistance = difference.Length();
                if (!float.IsFinite(centerDistance))
                {
                    continue;
                }

                Vector2 normal = centerDistance > VectorMath.Epsilon
                    ? difference / centerDistance
                    : Vector2.UnitX;
                float surfaceDistance = centerDistance - circle.Radius;
                float bodyRadius = _config.Radius;
                float exponent = (bodyRadius - surfaceDistance) / Math.Max(_config.SfmB, 1e-5f);
                exponent = Math.Clamp(exponent, -60f, 20f);
                Vector2 force = _config.SfmA * MathF.Exp(exponent) * normal;

                if (surfaceDistance < bodyRadius)
                {
                    float overlap = bodyRadius - surfaceDistance;
                    Vector2 tangent = VectorMath.Perpendicular(normal);
                    force += _config.SfmK * overlap * normal +
                        _config.SfmKappa * overlap * Vector2.Dot(-velocity, tangent) * tangent;
                }

                force = SoftCap(force, MaxRepulsion);
                if (VectorMath.IsFinite(force))
                {
                    wallForce += force;
                }
            }
        }

        Vector2 total = desiredForce + socialForce + wallForce;
        if (!VectorMath.IsFinite(total))
        {
            total = Vector2.Zero;
        }

        agent.LastDesiredDirection = desiredDir;
        agent.LastSfmForce = total;
        return total;
    }

    private static Vector2 SoftCap(Vector2 force, float cap)
    {
        if (!VectorMath.IsFinite(force))
        {
            return Vector2.Zero;
        }

        float magnitude = force.Length();
        if (magnitude > cap && magnitude > VectorMath.Epsilon)
        {
            return force / magnitude * cap;
        }

        return force;
    }
}
