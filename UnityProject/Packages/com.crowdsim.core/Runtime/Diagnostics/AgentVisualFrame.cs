#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using CrowdSim.Core.Model;

namespace CrowdSim.Diagnostics
{
    public enum HumanPose { Standing, Seated, Rising, Crawling, Prone, Assisting }

    /// <summary>Detached presentation input. Velocity retains metres per reference step, not metres per second.</summary>
    public sealed class AgentVisualFrame
    {
        private const float LegacyRenderUnitMeters = 1.70f / 42f;
        public int Id { get; }
        public Vector2 Position { get; }
        public Vector2 Heading { get; }
        public float SpeedRatio { get; }
        public AgentState State { get; }
        public SeatingPosture Posture { get; }
        public float Rise { get; }
        public float Panic { get; }
        public bool Alarm => State == AgentState.WaitingToReact;
        public int? HelpTarget { get; }
        public bool Attending { get; }
        public HumanPose Pose => IsProne(State) ? HumanPose.Prone : State == AgentState.Crawling ? HumanPose.Crawling
            : Posture == SeatingPosture.Seated ? HumanPose.Seated : Posture == SeatingPosture.StandingUp ? HumanPose.Rising
            : Attending ? HumanPose.Assisting : HumanPose.Standing;
        public bool ShowPanic => Panic >= .2f && !IsProne(State);
        public string Indicator
        {
            get
            {
                string help = HelpTarget.HasValue ? (Attending ? "+ HELPING " : "GOING TO HELP ") + HelpTarget : "";
                return help + (Alarm ? (help.Length > 0 ? " · ! ALARM" : "! ALARM") : "");
            }
        }
        public string StateLabel => State == AgentState.TempIncap ? "Temporary incapacity" : State == AgentState.WaitingToReact ? "Waiting / reaction freeze" : State.ToString();

        public AgentVisualFrame(int id, Vector2 position, Vector2 heading, float speedRatio, AgentState state,
            SeatingPosture posture = SeatingPosture.Walking, float rise = 0, float panic = 0, int? helpTarget = null, bool attending = false)
        {
            Id = id; Position = position;
            Heading = Finite(heading) && heading.LengthSquared() > 1e-8f ? Vector2.Normalize(heading) : Vector2.UnitX;
            SpeedRatio = float.IsFinite(speedRatio) ? Math.Max(0, speedRatio) : 0;
            State = state; Posture = posture; Rise = Math.Clamp(rise, 0, 1); Panic = Math.Clamp(panic, 0, 1);
            HelpTarget = helpTarget; Attending = attending;
        }
        public static bool IsProne(AgentState state) => state == AgentState.Fallen || state == AgentState.TempIncap || state == AgentState.Incapacitated || state == AgentState.Dead;
        private static bool Finite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);

        internal static AgentVisualFrame Capture(Agent agent, int tick, IDictionary<int, Agent> agents)
        {
            Vector2 heading = Finite(agent.Velocity) && agent.Velocity.LengthSquared() > 1e-5f * LegacyRenderUnitMeters * LegacyRenderUnitMeters ? agent.Velocity
                : agent.LastDesiredDirection.LengthSquared() > 1e-5f ? agent.LastDesiredDirection
                : agent.ExitTarget != null ? agent.ExitTarget.Position - agent.Position : Vector2.UnitX;
            int? target = agent.Intent == BehaviorIntent.AssistCasualty ? agent.AssistTargetAgentId : null;
            if (target.HasValue && agents.TryGetValue(target.Value, out Agent? casualty) && casualty.IsActive)
                heading = casualty.Position - agent.Position;
            // Chair facing is authoritative until the native stand-up transition completes.
            if (agent.Posture != SeatingPosture.Walking && agent.Chair != null)
                heading = new Vector2(MathF.Cos(agent.Chair.StartHeading), MathF.Sin(agent.Chair.StartHeading));
            float speed = Finite(agent.Velocity) ? agent.Velocity.Length() : 0;
            if (agent.State == AgentState.WaitingToReact || speed < .08f * LegacyRenderUnitMeters) speed = 0;
            return new AgentVisualFrame(agent.AgentId, agent.Position, heading, speed / Math.Max(.25f * LegacyRenderUnitMeters, agent.BaseSpeed),
                agent.State, agent.Posture, agent.StandUpProgress, agent.PanicLevel, target,
                target.HasValue && agent.AssistPauseUntilFrame >= 0 && tick < agent.AssistPauseUntilFrame);
        }
    }

    /// <summary>Stable state colours. Panic never changes a body's state colour. All legend text uses normal high-contrast ink.</summary>
    public static class AgentPalette
    {
        public static Vector3 Color(AgentState state)
        {
            switch (state)
            {
                case AgentState.Evacuating: return new Vector3(.20f, .81f, .46f);
                case AgentState.WaitingToReact: return new Vector3(.98f, .67f, .22f);
                case AgentState.Injured: return new Vector3(.26f, .57f, 1f);
                case AgentState.Crawling: return new Vector3(.98f, .88f, .31f);
                case AgentState.Fallen: return new Vector3(.96f, .29f, .25f);
                case AgentState.Incapacitated:
                case AgentState.TempIncap: return new Vector3(.55f, .16f, .23f);
                case AgentState.Dead: return new Vector3(.28f, .30f, .34f);
                default: return new Vector3(.91f, .92f, .88f);
            }
        }
    }
}
