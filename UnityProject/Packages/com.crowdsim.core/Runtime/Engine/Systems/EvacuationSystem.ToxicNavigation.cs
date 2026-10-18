#nullable enable
// M2 import of reference 01b4355; see docs/unity-migration/COMPATIBILITY.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrowdSim.Core.Compatibility;
using System.Numerics;
using CrowdSim.Core.Geometry;
using CrowdSim.Core.Model;

namespace CrowdSim.Core.Systems
{
    public sealed partial class EvacuationSystem
    {
        /// <summary>A bounded observation of the current plume, never a future-arrival prediction.</summary>
        internal readonly struct NaturalFumeRisk : IEquatable<NaturalFumeRisk>
        {
            public float Start { get; }
            public float Mean { get; }
            public float Peak { get; }
            public float End { get; }
            public float ObservedDistance { get; }
            public NaturalFumeRisk(
                float Start, float Mean, float Peak, float End, float ObservedDistance)
            {
                this.Start = Start;
                this.Mean = Mean;
                this.Peak = Peak;
                this.End = End;
                this.ObservedDistance = ObservedDistance;
            }
            public void Deconstruct(out float Start, out float Mean, out float Peak, out float End, out float ObservedDistance) { Start = this.Start; Mean = this.Mean; Peak = this.Peak; End = this.End; ObservedDistance = this.ObservedDistance; }
            public bool Equals(NaturalFumeRisk other) => EqualityComparer<float>.Default.Equals(Start, other.Start) && EqualityComparer<float>.Default.Equals(Mean, other.Mean) && EqualityComparer<float>.Default.Equals(Peak, other.Peak) && EqualityComparer<float>.Default.Equals(End, other.End) && EqualityComparer<float>.Default.Equals(ObservedDistance, other.ObservedDistance);
            public override bool Equals(object? other) => other is NaturalFumeRisk value && Equals(value);
            public override int GetHashCode()
            {
                int hash = 0;
                hash = unchecked(hash * -1521134295 + EqualityComparer<float>.Default.GetHashCode(Start));
                hash = unchecked(hash * -1521134295 + EqualityComparer<float>.Default.GetHashCode(Mean));
                hash = unchecked(hash * -1521134295 + EqualityComparer<float>.Default.GetHashCode(Peak));
                hash = unchecked(hash * -1521134295 + EqualityComparer<float>.Default.GetHashCode(End));
                hash = unchecked(hash * -1521134295 + EqualityComparer<float>.Default.GetHashCode(ObservedDistance));
                return hash;
            }
            public static bool operator ==(NaturalFumeRisk left, NaturalFumeRisk right) => left.Equals(right);
            public static bool operator !=(NaturalFumeRisk left, NaturalFumeRisk right) => !left.Equals(right);
            internal float Severity =>
                (0.60f + 0.15f * ObservedDistance) * Mean + 0.40f * Peak +
                0.50f * End + 0.75f * Math.Max(0f, End - Start);
        }

        private static bool HasActiveNaturalFume(Simulation simulation) =>
            simulation.ScenarioType == ScenarioType.ToxicFume &&
            simulation.ToxicFumeSystem.IsReleased &&
            simulation.ToxicFumeSystem.IsCacheValidFor(simulation);

        /// <summary>
        /// At most five cached concentration lookups. The observation stops at the perception
        /// horizon or an occluding wall. Furniture does not occlude the existing abstract plume.
        /// A straight local sample is a perceived risk cue, not an entire route exposure forecast.
        /// </summary>
        internal NaturalFumeRisk SampleNaturalFumeRisk(Simulation simulation, Agent agent, Vector2 goal)
        {
            if (!UsesNaturalNavigation(simulation) || !HasActiveNaturalFume(simulation)) return default;
            ToxicFumeSystem fume = simulation.ToxicFumeSystem;
            float elapsed = fume.GetElapsedSeconds(simulation);
            float start = fume.GetConcentration(agent.Position, elapsed);
            Vector2 displacement = goal - agent.Position;
            float range = Math.Max(0f, _config.NaturalPerceptionRadiusMeters) *
                Math.Clamp(agent.VisibilityFactor, 0.35f, 1f);
            float length = Math.Min(displacement.Length(), range);
            if (length <= 0.0001f) return new NaturalFumeRisk(start, start, start, start, 0f);
            Vector2 delta = VectorMath.SafeNormalize(displacement) * length;
            // Most local intentions are wholly visible. One ray is enough for that common
            // case; only an occluded intention needs progressive prefix visibility checks.
            bool fullyVisible = simulation.HasLineOfSight(agent.Position, agent.Position + delta);
            float sum = start, peak = start, end = start, observed = 0f;
            int count = 1;
            for (int sample = 1; sample <= 4; sample++)
            {
                float fraction = sample / 4f;
                Vector2 position = agent.Position + delta * fraction;
                if (!fullyVisible && !simulation.HasLineOfSight(agent.Position, position)) break;
                end = fume.GetConcentration(position, elapsed);
                sum += end;
                peak = Math.Max(peak, end);
                observed = length * fraction;
                count++;
            }
            return new NaturalFumeRisk(start, sum / count, peak, end, observed);
        }

        /// <summary>
        /// Soft preference rather than an impassable gas wall: if all feasible directions are
        /// contaminated, recovery can still select the least costly reachable alternative.
        /// </summary>
        internal float NaturalToxicPathPenalty(Simulation simulation, Agent agent, Vector2 goal)
            => Math.Max(0f, _config.NaturalToxicRiskWeight) * SampleNaturalFumeRisk(simulation, agent, goal).Severity;

        /// <summary>
        /// Abandon a detour only when a newly worsened patch lies ahead. Rising concentration
        /// at the pedestrian's own position must not cancel a still-improving retreat.
        /// </summary>
        internal bool NaturalToxicDetourWorsened(Simulation simulation, Agent agent, Vector2 goal)
        {
            if (!HasActiveNaturalFume(simulation)) return false;
            NaturalFumeRisk risk = SampleNaturalFumeRisk(simulation, agent, goal);
            NaturalNavigationMemory memory = agent.NavigationMemory;
            float awareness = simulation.ToxicFumeSystem.Configuration.AwarenessConcentration;
            const float change = 0.08f;
            return risk.Peak >= awareness &&
                (risk.Peak > Math.Max(risk.Start, memory.DetourToxicPeak) + change ||
                 risk.End > Math.Max(risk.Start, memory.DetourToxicEnd) + change);
        }

        /// <summary>
        /// Staggered local hazard steering while a bounded recovery request waits. An active
        /// recovery goal already has fume scoring/revalidation; never fight it with a second goal.
        /// Existing circle/casualty avoidance and collision resolution run after this direction.
        /// </summary>
        internal Vector2 ApplyNaturalToxicAvoidance(
            Simulation simulation, Agent agent, Vector2 direction, bool exitCapture)
        {
            if (!UsesNaturalNavigation(simulation) || !IsNaturalMover(agent) ||
                !HasActiveNaturalFume(simulation)) return direction;
            NaturalNavigationMemory memory = agent.NavigationMemory;
            if (memory.DetourGoal is not null || exitCapture || direction.LengthSquared() < 0.000001f)
            {
                memory.ToxicSteeringActive = false;
                memory.NextToxicSteeringFrame = simulation.FrameIndex;
                return direction;
            }
            direction = VectorMath.SafeNormalize(direction);
            if (simulation.FrameIndex < memory.NextToxicSteeringFrame &&
                Vector2.Dot(direction, memory.ToxicSteeringInput) > 0.80f)
                return memory.ToxicSteeringActive ? memory.ToxicSteeringDirection : direction;

            memory.NextToxicSteeringFrame = simulation.FrameIndex + SecondsToFrames(
                _config.NaturalDecisionSeconds + (agent.AgentId % 3) * 0.035f);
            memory.ToxicSteeringInput = direction;
            memory.ToxicSteeringActive = false;
            const float lookAhead = 1.2f;
            NaturalFumeRisk forward = SampleNaturalFumeRisk(simulation, agent, agent.Position + direction * lookAhead);
            float awareness = simulation.ToxicFumeSystem.Configuration.AwarenessConcentration;
            // Ordinary evacuation already moves us out of a uniformly contaminated patch.
            // Intervene for an observed increase ahead, not merely because gas is present.
            if (forward.Peak < awareness || forward.Peak <= forward.Start + 0.04f) return direction;

            RequestNaturalRecovery(simulation, agent, force: false);
            Vector2 safer = simulation.ToxicFumeSystem.GetSaferDirection(simulation, agent.Position);
            if (safer.LengthSquared() < 0.000001f) return direction;
            Vector2 saferGoal = agent.Position + safer * lookAhead;
            if (!simulation.HasTraversablePath(agent.Position, saferGoal, NavigationClearance())) return direction;
            NaturalFumeRisk alternative = SampleNaturalFumeRisk(simulation, agent, saferGoal);
            if (alternative.Severity + 0.03f >= forward.Severity) return direction;
            memory.ToxicSteeringDirection = VectorMath.SafeNormalize(safer);
            memory.ToxicSteeringActive = true;
            return memory.ToxicSteeringDirection;
        }

        /// <summary>
        /// A progressing agent may still notice that its exit intention is becoming contaminated.
        /// Recheck at a staggered sub-second cadence, require a materially better/cleaner alternative,
        /// and apply a switch cooldown. No per-agent A* or full-grid plume rebuild occurs here.
        /// </summary>
        private void MaybeReselectNaturalToxicExit(Simulation simulation, Agent agent)
        {
            if (!HasActiveNaturalFume(simulation) || agent.ExitTarget is null ||
                simulation.Environment.Exits.Count < 2) return;
            NaturalNavigationMemory memory = agent.NavigationMemory;
            if (simulation.FrameIndex < memory.NextToxicExitCheckFrame) return;
            memory.NextToxicExitCheckFrame = simulation.FrameIndex + SecondsToFrames(
                0.60f + (agent.AgentId % 3) * 0.06f);
            if ((simulation.FrameIndex - memory.LastExitChangeFrame) * _config.Dt < 2.0f) return;

            Exit current = agent.ExitTarget;
            Vector2 intendedPoint = memory.DetourGoal ?? agent.CurrentRouteWaypoint ?? current.Position;
            NaturalFumeRisk risk = SampleNaturalFumeRisk(simulation, agent, intendedPoint);
            float awareness = simulation.ToxicFumeSystem.Configuration.AwarenessConcentration;
            float danger = simulation.ToxicFumeSystem.Configuration.DangerConcentration;
            if (risk.Peak < awareness) return;
            bool worseningAhead = risk.Peak > risk.Start + 0.06f;
            bool dangerousWithoutImprovement = risk.Start >= danger && risk.End >= risk.Start - 0.04f;
            if (!worseningAhead && !dangerousWithoutImprovement) return;

            float currentExitPenalty = NaturalToxicPathPenalty(simulation, agent, current.Position);
            float currentPenalty = Math.Max(currentExitPenalty,
                Math.Max(0f, _config.NaturalToxicRiskWeight) * risk.Severity);
            float currentCost = NaturalPerceivedExitCost(simulation, agent, current, currentExitPenalty);
            Exit? best = null;
            float bestCost = float.PositiveInfinity;
            foreach (Exit exit in simulation.Environment.Exits)
            {
                if (ReferenceEquals(exit, current)) continue;
                float hazardCost = NaturalToxicPathPenalty(simulation, agent, exit.Position);
                if (hazardCost + 0.25f >= currentPenalty) continue;
                float cost = NaturalPerceivedExitCost(simulation, agent, exit, hazardCost);
                if (memory.RecentlyFailedExit == exit.Name && simulation.FrameIndex < memory.FailedExitUntilFrame)
                    cost += 3f; // a hazard can justify revisiting a previously crowded exit
                if (cost >= bestCost) continue;
                bestCost = cost;
                best = exit;
            }
            if (best is null || bestCost > currentCost * _config.ExitSwitchImprovementRatio) return;

            memory.RecentlyFailedExit = current.Name;
            memory.FailedExitUntilFrame = simulation.FrameIndex + SecondsToFrames(_config.NaturalFailureMemorySeconds);
            memory.LastExitChangeFrame = simulation.FrameIndex;
            memory.ExitChanges++;
            memory.Stage = NavigationRecoveryStage.ReconsideringExit;
            memory.DetourGoal = null;
            memory.ReplanAfterDetour = false;
            memory.GuidancePoint = null;
            memory.GuidanceKey = null;
            memory.ResetRouteCostObservation();
            memory.NextRouteRequestFrame = simulation.FrameIndex;
            memory.NextToxicSteeringFrame = simulation.FrameIndex;
            memory.ToxicSteeringActive = false;
            // Invalidate an old queued goal without removing/reordering other agents' work.
            memory.RequestedRouteKey = null;
            agent.ExitTarget = best;
            agent.ExitCommitUntilFrame = simulation.FrameIndex + SecondsToFrames(5f);
            agent.ResetExitCongestion();
            agent.ClearRoute();
            ClearCircleFollow(agent);
            ClearSlowObstacleBypass(agent);
            agent.Exploring = false;
            agent.TempGoal = null;
        }
    }
}
