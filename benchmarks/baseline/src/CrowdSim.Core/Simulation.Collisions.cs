using System.Numerics;
using CrowdSim.Core.Geometry;
using CrowdSim.Core.Model;
using CrowdSim.Core.Systems;

namespace CrowdSim.Core;

public sealed partial class Simulation
{
    /// <summary>
    /// Hard collision resolution against walls/blockages and crawling/fallen casualties.
    /// Includes the validated wall-follow, reversal, cooldown, post-blast release and
    /// guaranteed-escape guardrails.
    /// </summary>
    public void ResolveSolidCollisions(Agent agent)
    {
        if (agent.State is AgentState.Fallen or AgentState.Incapacitated or AgentState.Exited or
            AgentState.TempIncap or AgentState.Dead)
        {
            return;
        }

        // Guard before any spatial-hash lookup so NaN/Infinity never participates in
        // float-to-cell conversion. This is equivalent to the source's recovery intent
        // but applies it at the safest possible boundary.
        if (!VectorMath.IsFinite(agent.Position))
        {
            agent.Position = new Vector2(Config.World.WidthMeters / 2f, Config.World.LengthMeters / 2f);
        }

        if (!VectorMath.IsFinite(agent.Velocity))
        {
            agent.Velocity = Vector2.Zero;
        }

        float radius = Config.Agent.Radius;
        float casualtyRadius = Config.Agent.Radius * 1.25f;
        float speed = agent.Velocity.Length();

        CollectNearbyWalls(
            agent.Position,
            Config.Agent.Radius * 1.2f,
            _nearbyWallScratch,
            _nearbyWallSeenScratch);
        bool isTouchingWall = false;
        foreach (Wall wall in _nearbyWallScratch)
        {
            if (wall.X1 - Config.Agent.Radius <= agent.Position.X &&
                agent.Position.X <= wall.X2 + Config.Agent.Radius &&
                wall.Y1 - Config.Agent.Radius <= agent.Position.Y &&
                agent.Position.Y <= wall.Y2 + Config.Agent.Radius)
            {
                isTouchingWall = true;
                break;
            }
        }

        if (!isTouchingWall)
        {
            isTouchingWall = IsNearCircularBlockage(agent.Position, Config.Agent.Radius);
        }

        if (speed < 0.05f * agent.BaseSpeed && agent.WallFollowDir is null && !isTouchingWall)
        {
            return;
        }

        const int maxIterations = 8;
        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            bool moved = false;
            CollectNearbyWalls(
                agent.Position,
                agent.WallFollowDir is not null ? WallFollowRadius : InteractionRadius,
                _nearbyWallScratch,
                _nearbyWallSeenScratch);

            foreach (Wall wall in _nearbyWallScratch)
            {
                if (!(wall.X1 - radius <= agent.Position.X && agent.Position.X <= wall.X2 + radius &&
                      wall.Y1 - radius <= agent.Position.Y && agent.Position.Y <= wall.Y2 + radius))
                {
                    continue;
                }

                float dxLeft = wall.X1 - (agent.Position.X - radius);
                float dxRight = (agent.Position.X + radius) - wall.X2;
                float dyTop = wall.Y1 - (agent.Position.Y - radius);
                float dyBottom = (agent.Position.Y + radius) - wall.Y2;

                Vector2 correction = new(-dxLeft, 0f);
                float correctionLengthSquared = correction.LengthSquared();
                Vector2 candidateCorrection = new(dxRight, 0f);
                float candidateLengthSquared = candidateCorrection.LengthSquared();
                if (candidateLengthSquared < correctionLengthSquared)
                {
                    correction = candidateCorrection;
                    correctionLengthSquared = candidateLengthSquared;
                }

                candidateCorrection = new Vector2(0f, -dyTop);
                candidateLengthSquared = candidateCorrection.LengthSquared();
                if (candidateLengthSquared < correctionLengthSquared)
                {
                    correction = candidateCorrection;
                    correctionLengthSquared = candidateLengthSquared;
                }

                candidateCorrection = new Vector2(0f, dyBottom);
                candidateLengthSquared = candidateCorrection.LengthSquared();
                if (candidateLengthSquared < correctionLengthSquared)
                {
                    correction = candidateCorrection;
                }
                float correctionLength = correction.Length();
                if (correctionLength <= 0f || !float.IsFinite(correctionLength))
                {
                    continue;
                }

                agent.Position += correction;
                moved = true;

                Vector2 normal = correction / (correctionLength + 1e-6f);
                float normalVelocity = Vector2.Dot(agent.Velocity, normal);
                if (normalVelocity < 0f)
                {
                    agent.Velocity -= normalVelocity * normal;
                }

                Vector2 tangent = VectorMath.Perpendicular(normal);

                // Finite-blockage termination.
                if (agent.ExitTarget is not null)
                {
                    bool isBlockage =
                        wall.X2 - wall.X1 < 0.9f * Environment.Width &&
                        wall.Y2 - wall.Y1 < 0.9f * Environment.Height;

                    if (isBlockage && agent.WallFollowTangent is not null)
                    {
                        Vector2 toExit = VectorMath.SafeNormalize(agent.ExitTarget.Position - agent.Position);
                        Vector2 followTangent = VectorMath.SafeNormalize(agent.WallFollowTangent.Value);
                        float progress = Vector2.Dot(followTangent, toExit);
                        if (progress < 0.1f)
                        {
                            DetachFromWallFollow(agent);
                            agent.Velocity += toExit * (0.6f * agent.BaseSpeed);
                            break;
                        }
                    }
                }

                speed = agent.Velocity.Length();
                bool changedNormal = agent.WallFollowNormal is null ||
                    Vector2.Dot(normal, agent.WallFollowNormal.Value) < 0.95f;

                bool allowLegacyWallFollow =
                    agent.State == AgentState.Wandering || agent.Exploring;
                if (allowLegacyWallFollow &&
                    (normalVelocity < -0.006f || speed < 0.25f * agent.BaseSpeed) &&
                    FrameIndex >= agent.WallFollowCooldownUntil &&
                    changedNormal &&
                    agent.WallFollowUntil < FrameIndex)
                {
                    if (agent.ExitTarget is not null)
                    {
                        Vector2 left = VectorMath.Perpendicular(normal);
                        Vector2 right = -left;
                        const float probeDistance = 0.81f;
                        Vector2 leftProbe = VectorMath.ClampToBounds(
                            agent.Position + left * probeDistance,
                            Config.World.WidthMeters,
                            Config.World.LengthMeters);
                        Vector2 rightProbe = VectorMath.ClampToBounds(
                            agent.Position + right * probeDistance,
                            Config.World.WidthMeters,
                            Config.World.LengthMeters);
                        float leftDistance = Vector2.Distance(leftProbe, agent.ExitTarget.Position);
                        float rightDistance = Vector2.Distance(rightProbe, agent.ExitTarget.Position);
                        agent.WallFollowDir = leftDistance < rightDistance ? 1 : -1;
                    }
                    else
                    {
                        agent.WallFollowDir = Random.NextDouble() < 0.5 ? 1 : -1;
                    }

                    agent.WallFollowUntil = FrameIndex + 9999;
                    agent.WallFollowNormal = normal;
                    agent.WallFollowTangent = tangent;
                    agent.WallFollowDistance = 0f;
                    agent.WallFollowReversed = false;
                    agent.WallFollowStartFrame = FrameIndex;
                }

                if (agent.WallFollowDir is not null && agent.WallFollowUntil > FrameIndex)
                {
                    agent.Position += agent.WallFollowDir.Value * tangent * 0.01f;
                }
            }

            foreach (CircleBlockage circle in Environment.CircularBlockages)
            {
                Vector2 offset = agent.Position - circle.Center;
                float distance = offset.Length();
                float minimumDistance = circle.Radius + radius;
                if (!float.IsFinite(distance) || distance >= minimumDistance)
                {
                    continue;
                }

                Vector2 normal = distance > 1e-6f ? offset / distance : Vector2.UnitX;
                float penetration = minimumDistance - Math.Max(0f, distance);
                agent.Position += normal * (penetration + 0.001f);
                moved = true;

                float inwardVelocity = Vector2.Dot(agent.Velocity, normal);
                if (inwardVelocity < 0f)
                {
                    agent.Velocity -= inwardVelocity * normal;
                }
            }

            CollectNearbyAgents(agent, CollisionRadius, _collisionAgentScratch);
            foreach (Agent other in _collisionAgentScratch)
            {
                if (!other.IsCasualtyObstacle)
                {
                    continue;
                }

                if (agent.TimeStuck > 2.5f)
                {
                    continue;
                }

                Vector2 difference = agent.Position - other.Position;
                if (!VectorMath.IsFinite(difference))
                {
                    continue;
                }

                float distance = difference.Length();
                if (!float.IsFinite(distance) || distance < 1e-6f)
                {
                    continue;
                }

                float minSeparation = radius + casualtyRadius;
                if (distance >= minSeparation)
                {
                    continue;
                }

                Vector2 push = difference / distance * (minSeparation - distance);
                agent.Position += push;
                moved = true;

                Vector2 normal = VectorMath.SafeNormalize(push);
                float normalVelocity = Vector2.Dot(agent.Velocity, normal);
                if (normalVelocity < 0f)
                {
                    agent.Velocity -= normalVelocity * normal;
                }

                if (agent.WallFollowUntil > FrameIndex)
                {
                    float inward = Vector2.Dot(agent.Velocity, normal);
                    if (inward < 0f)
                    {
                        agent.Velocity -= inward * normal;
                    }
                }

                float yieldFactor = 0.85f;
                if (agent.ExitTarget is not null && other.ExitTarget is not null)
                {
                    Vector2 toExit = VectorMath.SafeNormalize(agent.ExitTarget.Position - agent.Position);
                    if (toExit.LengthSquared() > 0f)
                    {
                        float agentAlignment = Vector2.Dot(agent.Velocity, toExit);
                        float otherAlignment = Vector2.Dot(other.Velocity, toExit);
                        if (agentAlignment > otherAlignment + 0.05f)
                        {
                            yieldFactor = 0.95f;
                        }
                    }
                }

                if (agent.ExitTarget is not null)
                {
                    Vector2 toExit = VectorMath.SafeNormalize(agent.ExitTarget.Position - agent.Position);
                    float alignment = Vector2.Dot(agent.Velocity, toExit);
                    if (agent.WallFollowDir is not null && alignment > 0.3f)
                    {
                        yieldFactor = Math.Max(yieldFactor, 0.95f);
                    }
                }

                agent.Velocity *= yieldFactor;
            }

            if (!moved)
            {
                break;
            }

            if (agent.WallFollowUntil > FrameIndex && agent.WallFollowDir is not null)
            {
                // Commit-phase fast path.
                if (agent.WallFollowDistance < 6.48f)
                {
                    if (agent.WallFollowTangent is null)
                    {
                        break;
                    }

                    Vector2 tangent = VectorMath.SafeNormalize(agent.WallFollowTangent.Value);
                    float tangentialVelocity = Vector2.Dot(agent.Velocity, tangent);
                    agent.Velocity = tangentialVelocity * tangent;
                    continue;
                }

                if (agent.WallFollowTangent is null)
                {
                    break;
                }

                Vector2 wallTangent = agent.WallFollowTangent.Value;
                int stuckTime = FrameIndex - agent.WallFollowStartFrame;
                float exitDistance = agent.ExitTarget is not null
                    ? Vector2.Distance(agent.Position, agent.ExitTarget.Position)
                    : float.PositiveInfinity;
                float exitClearDistance = 4f * Config.Agent.Radius;
                float tangentialSpeed = MathF.Abs(Vector2.Dot(agent.Velocity, wallTangent));
                const float edgeCommitBonus = 0.6f;
                int delay = (int)(1.5f / Config.Agent.Dt * edgeCommitBonus);
                int rampTime = Math.Max(1, (int)(4f / Config.Agent.Dt * edgeCommitBonus));

                if (stuckTime > delay && exitDistance > exitClearDistance &&
                    tangentialSpeed > 0.15f * agent.BaseSpeed)
                {
                    float factor = Math.Min(1f, stuckTime / (float)rampTime);
                    float jitter = ((float)Random.NextDouble() - 0.5f) * 0.4f * factor * agent.BaseSpeed;
                    agent.Velocity += jitter * agent.WallFollowDir.Value * wallTangent;
                }

                if (agent.ExitTarget is not null && agent.WallFollowDir is not null)
                {
                    CollectNearbyWalls(
                        agent.Position,
                        WallFollowRadius,
                        _nearbyWallScratch,
                        _nearbyWallSeenScratch);
                    if (FrameIndex - agent.LosLastCheckFrame >= Math.Max(1, (int)(0.5f / Config.Agent.Dt)))
                    {
                        agent.LosCached = EvacuationSystem.HasLineOfSight(
                            agent.Position,
                            agent.ExitTarget.Position,
                            _nearbyWallScratch);
                        agent.LosLastCheckFrame = FrameIndex;
                    }

                    if (agent.LosCached)
                    {
                        Vector2? previousNormal = agent.WallFollowNormal;
                        DetachFromWallFollow(agent);
                        if (previousNormal is not null)
                        {
                            agent.Position += previousNormal.Value * (1.2f * Config.Agent.Radius);
                            agent.Position = VectorMath.ClampToBounds(
                                agent.Position,
                                Config.World.WidthMeters,
                                Config.World.LengthMeters);
                        }

                        return;
                    }
                }
            }
        }

        // Post-blast wall-follow decay.
        if (agent.WallFollowDir is not null &&
            (agent.State is AgentState.Evacuating or AgentState.Injured) &&
            BlastOver() &&
            agent.Velocity.Length() < 0.15f * agent.BaseSpeed)
        {
            DetachFromWallFollow(agent);
            if (agent.ExitTarget is not null)
            {
                agent.Velocity = VectorMath.SafeNormalize(agent.ExitTarget.Position - agent.Position) *
                    (0.8f * agent.BaseSpeed);
            }
        }

        // Hard failsafe: guaranteed release after six simulated seconds.
        int maxWallFollowFrames = Math.Max(1, (int)(6f / Config.Agent.Dt));
        if (agent.WallFollowDir is not null &&
            (agent.State is AgentState.Evacuating or AgentState.Injured) &&
            FrameIndex - agent.WallFollowStartFrame > maxWallFollowFrames)
        {
            DetachFromWallFollow(agent);
            if (agent.ExitTarget is not null)
            {
                agent.Velocity = VectorMath.SafeNormalize(agent.ExitTarget.Position - agent.Position) *
                    (0.9f * agent.BaseSpeed);
            }

            agent.WallFollowCooldownUntil = FrameIndex + (int)(20f / Config.Agent.Dt);
        }

        if (agent.WallFollowDir is not null && agent.ExitTarget is not null)
        {
            float exitDistance = Vector2.Distance(agent.Position, agent.ExitTarget.Position);
            if (!float.IsFinite(agent.LastExitDist))
            {
                agent.LastExitDist = exitDistance;
            }

            if (exitDistance < agent.LastExitDist - 0.08f)
            {
                agent.LastExitDist = exitDistance;
            }
            else if (exitDistance > agent.LastExitDist + 0.24f &&
                     FrameIndex - agent.WallFollowStartFrame > (int)(2f / Config.Agent.Dt))
            {
                DetachFromWallFollow(agent);
                agent.LastExitDist = float.PositiveInfinity;
                agent.WallFollowCooldownUntil = FrameIndex + (int)(10f / Config.Agent.Dt);
            }

            if (agent.WallFollowDir is not null)
            {
                float commitDistance = 8.10f * (1f + 0.8f * agent.PanicLevel);
                int maxTimeFrames = Math.Max(1, (int)(3f / Config.Agent.Dt));
                int timeElapsed = FrameIndex - agent.WallFollowStartFrame;
                if (agent.WallFollowDistance >= commitDistance || timeElapsed >= maxTimeFrames)
                {
                    if (!agent.WallFollowReversed)
                    {
                        agent.WallFollowDir *= -1;
                        agent.WallFollowDistance = 0f;
                        agent.WallFollowReversed = true;
                        agent.WallFollowStartFrame = FrameIndex;
                    }
                    else
                    {
                        DetachFromWallFollow(agent);
                    }
                }
            }
        }

        if (!VectorMath.IsFinite(agent.Position))
        {
            agent.Position = new Vector2(Config.World.WidthMeters / 2f, Config.World.LengthMeters / 2f);
        }

        if (!VectorMath.IsFinite(agent.Velocity))
        {
            agent.Velocity = Vector2.Zero;
        }

        if (agent.WallFollowDir is not null && agent.WallFollowUntil > FrameIndex &&
            agent.WallFollowNormal is not null && agent.WallFollowTangent is not null)
        {
            Vector2 tangent = VectorMath.SafeNormalize(agent.WallFollowTangent.Value);
            float tangentialVelocity = Vector2.Dot(agent.Velocity, tangent);
            agent.Velocity = tangentialVelocity * tangent;
        }

        // Collision energy dissipation.
        if (agent.Velocity.Length() > VectorMath.Epsilon)
        {
            agent.Velocity *= 0.75f;
        }

        if (agent.WallFollowDir is null)
        {
            agent.WallFollowNormal = null;
            agent.WallFollowTangent = null;
            agent.LastExitDist = float.PositiveInfinity;
            agent.WallFollowCooldownUntil = FrameIndex + (int)(10f / Config.Agent.Dt);
        }
    }

    /// <summary>Move one agent with sub-step tunnelling protection and final blockage containment.</summary>
    public void MoveAgent(Agent agent)
    {
        Vector2 newPosition = agent.Position + agent.Velocity;
        float speed = agent.Velocity.Length();
        CollectNearbyWalls(
            agent.Position,
            Config.Agent.Radius * 2f,
            _nearbyWallScratch,
            _nearbyWallSeenScratch);
        bool nearStaticObstacle = _nearbyWallScratch.Count > 0 ||
            IsNearCircularBlockage(agent.Position, Config.Agent.Radius * 2f);
        int substeps = nearStaticObstacle && speed > 0f ? 2 : 1;

        if (substeps > 1)
        {
            Vector2 stepVelocity = agent.Velocity / substeps;
            for (int step = 0; step < substeps; step++)
            {
                Vector2 oldPosition = agent.Position;
                newPosition = VectorMath.ClampToBounds(
                    agent.Position + stepVelocity,
                    Config.World.WidthMeters,
                    Config.World.LengthMeters);
                if (agent.WallFollowDir is not null)
                {
                    agent.WallFollowDistance += Vector2.Distance(oldPosition, newPosition);
                }

                agent.Position = newPosition;
                ResolveSolidCollisions(agent);
            }

            StopExplorationIfGoalReached(agent);
            FinalBlockageContainment(agent);
            return;
        }

        newPosition = VectorMath.ClampToBounds(newPosition, Config.World.WidthMeters, Config.World.LengthMeters);
        if (agent.WallFollowDir is not null)
        {
            agent.WallFollowDistance += Vector2.Distance(agent.Position, newPosition);
        }

        agent.Position = newPosition;
        StopExplorationIfGoalReached(agent);

        speed = agent.Velocity.Length();
        bool needsPreciseCollision = agent.WallFollowDir is not null;
        if (!needsPreciseCollision)
        {
            CollectNearbyWalls(
                agent.Position,
                Config.Agent.Radius * 1.5f,
                _nearbyWallScratch,
                _nearbyWallSeenScratch);
            if (_nearbyWallScratch.Count > 0 ||
                IsNearCircularBlockage(agent.Position, Config.Agent.Radius * 1.5f))
            {
                needsPreciseCollision = true;
            }
            else
            {
                CollectNearbyAgents(agent, CollisionRadius, _collisionAgentScratch);
                foreach (Agent other in _collisionAgentScratch)
                {
                    if (other.IsCasualtyObstacle)
                    {
                        needsPreciseCollision = true;
                        break;
                    }
                }
            }
        }

        if (needsPreciseCollision)
        {
            ResolveSolidCollisions(agent);
        }
        else if (speed > 0.35f * agent.BaseSpeed && FrameIndex % 2 == 0)
        {
            ResolveSolidCollisions(agent);
        }

        FinalBlockageContainment(agent);
    }

    private void StopExplorationIfGoalReached(Agent agent)
    {
        if (agent.Exploring && agent.TempGoal is not null &&
            Vector2.Distance(agent.Position, agent.TempGoal.Value) < 0.20f)
        {
            agent.Exploring = false;
            agent.TempGoal = null;
            agent.ExplorationReplanAfterFrame = 0;
        }
    }

    private void FinalBlockageContainment(Agent agent)
    {
        float radius = Config.Agent.Radius;
        foreach (Wall wall in Environment.Walls)
        {
            bool isBlockage =
                wall.X2 - wall.X1 < 0.9f * Environment.Width &&
                wall.Y2 - wall.Y1 < 0.9f * Environment.Height;
            if (!isBlockage)
            {
                continue;
            }

            if (!(wall.X1 - radius < agent.Position.X && agent.Position.X < wall.X2 + radius &&
                  wall.Y1 - radius < agent.Position.Y && agent.Position.Y < wall.Y2 + radius))
            {
                continue;
            }

            float leftDistance = MathF.Abs(agent.Position.X - (wall.X1 - radius));
            float rightDistance = MathF.Abs(agent.Position.X - (wall.X2 + radius));
            float topDistance = MathF.Abs(agent.Position.Y - (wall.Y1 - radius));
            float bottomDistance = MathF.Abs(agent.Position.Y - (wall.Y2 + radius));
            float dx = Math.Min(leftDistance, rightDistance);
            float dy = Math.Min(topDistance, bottomDistance);

            if (dx < dy)
            {
                float x = agent.Position.X < (wall.X1 + wall.X2) * 0.5f
                    ? wall.X1 - radius
                    : wall.X2 + radius;
                agent.Position = new Vector2(x, agent.Position.Y);
                agent.Velocity = new Vector2(0f, agent.Velocity.Y);
            }
            else
            {
                float y = agent.Position.Y < (wall.Y1 + wall.Y2) * 0.5f
                    ? wall.Y1 - radius
                    : wall.Y2 + radius;
                agent.Position = new Vector2(agent.Position.X, y);
                agent.Velocity = new Vector2(agent.Velocity.X, 0f);
            }
        }

        foreach (CircleBlockage circle in Environment.CircularBlockages)
        {
            Vector2 offset = agent.Position - circle.Center;
            float distance = offset.Length();
            float minimumDistance = circle.Radius + radius;
            if (!float.IsFinite(distance) || distance >= minimumDistance)
            {
                continue;
            }

            Vector2 normal = distance > 1e-6f ? offset / distance : Vector2.UnitX;
            agent.Position = circle.Center + normal * (minimumDistance + 0.001f);
            float inwardVelocity = Vector2.Dot(agent.Velocity, normal);
            if (inwardVelocity < 0f)
            {
                agent.Velocity -= inwardVelocity * normal;
            }
        }
    }

    private bool IsNearCircularBlockage(Vector2 position, float margin)
    {
        foreach (CircleBlockage circle in Environment.CircularBlockages)
        {
            float reach = circle.Radius + Math.Max(0f, margin);
            if (Vector2.DistanceSquared(position, circle.Center) <= reach * reach)
            {
                return true;
            }
        }

        return false;
    }

    private void DetachFromWallFollow(Agent agent)
    {
        agent.WallFollowDir = null;
        agent.WallFollowUntil = FrameIndex;
        agent.WallFollowNormal = null;
        agent.WallFollowTangent = null;
    }
}
