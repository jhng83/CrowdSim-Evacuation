using System;
using CrowdSim.Core;
using CrowdSim.Core.Model;

namespace CrowdSim.Diagnostics
{
    /// <summary>M9 translation of native UiMetrics.Capture. Denominators and casualty exclusions are preserved.</summary>
    public sealed class PopulationMetrics
    {
        public int InRoom { get; }
        public int Mobile { get; }
        public int Exited { get; }
        public int Injured { get; }
        public int Crawling { get; }
        public int Fallen { get; }
        public int TempIncap { get; }
        public int Incapacitated { get; }
        public int Dead { get; }
        public float AveragePanic { get; }
        public float MaximumPanic { get; }
        public float AverageVisibility { get; }
        public float AverageCongestion { get; }
        public int RunningContagion { get; }
        public int ThreatEscape { get; }
        public int FollowingCrowd { get; }
        public int GoingToAssist { get; }
        public int CurrentlyHelping { get; }
        public int ToxicExposed { get; }
        public int ToxicImpaired { get; }
        public int ToxicIncapacitated { get; }
        public int ToxicHighConcentration { get; }
        public float AverageToxicExposure { get; }
        public float PeakToxicConcentration { get; }
        public float ElapsedSeconds { get; }
        public PopulationMetrics(
            int InRoom,
            int Mobile,
            int Exited,
            int Injured,
            int Crawling,
            int Fallen,
            int TempIncap,
            int Incapacitated,
            int Dead,
            float AveragePanic,
            float MaximumPanic,
            float AverageVisibility,
            float AverageCongestion,
            int RunningContagion,
            int ThreatEscape,
            int FollowingCrowd,
            int GoingToAssist,
            int CurrentlyHelping,
            int ToxicExposed,
            int ToxicImpaired,
            int ToxicIncapacitated,
            int ToxicHighConcentration,
            float AverageToxicExposure,
            float PeakToxicConcentration,
            float ElapsedSeconds)
        {
            this.InRoom = InRoom;
            this.Mobile = Mobile;
            this.Exited = Exited;
            this.Injured = Injured;
            this.Crawling = Crawling;
            this.Fallen = Fallen;
            this.TempIncap = TempIncap;
            this.Incapacitated = Incapacitated;
            this.Dead = Dead;
            this.AveragePanic = AveragePanic;
            this.MaximumPanic = MaximumPanic;
            this.AverageVisibility = AverageVisibility;
            this.AverageCongestion = AverageCongestion;
            this.RunningContagion = RunningContagion;
            this.ThreatEscape = ThreatEscape;
            this.FollowingCrowd = FollowingCrowd;
            this.GoingToAssist = GoingToAssist;
            this.CurrentlyHelping = CurrentlyHelping;
            this.ToxicExposed = ToxicExposed;
            this.ToxicImpaired = ToxicImpaired;
            this.ToxicIncapacitated = ToxicIncapacitated;
            this.ToxicHighConcentration = ToxicHighConcentration;
            this.AverageToxicExposure = AverageToxicExposure;
            this.PeakToxicConcentration = PeakToxicConcentration;
            this.ElapsedSeconds = ElapsedSeconds;
        }
        public static PopulationMetrics Capture(Simulation simulation)
        {
            int inRoom = 0;
            int mobile = 0;
            int exited = 0;
            int injured = 0;
            int crawling = 0;
            int fallen = 0;
            int tempIncap = 0;
            int incapacitated = 0;
            int dead = 0;
            int runningContagion = 0;
            int threatEscape = 0;
            int followingCrowd = 0;
            int goingToAssist = 0;
            int currentlyHelping = 0;
            int toxicExposed = 0;
            int toxicImpaired = 0;
            int toxicIncapacitated = 0;
            int toxicHighConcentration = 0;
            float toxicExposureSum = 0f;
            float peakToxicConcentration = 0f;
            int toxicExposureSamples = 0;
            float panicSum = 0f;
            float maximumPanic = 0f;
            float visibilitySum = 0f;
            float congestionSum = 0f;

            foreach (Agent agent in simulation.Agents)
            {
                switch (agent.State)
                {
                    case AgentState.Exited: exited++; break;
                    case AgentState.Injured: injured++; break;
                    case AgentState.Crawling: crawling++; break;
                    case AgentState.Fallen: fallen++; break;
                    case AgentState.TempIncap: tempIncap++; break;
                    case AgentState.Incapacitated: incapacitated++; break;
                    case AgentState.Dead: dead++; break;
                }

                if (simulation.ScenarioType == ScenarioType.ToxicFume)
                {
                    switch (agent.ToxicExposureStatus)
                    {
                        case ToxicExposureStatus.Exposed:
                            toxicExposed++;
                            break;
                        case ToxicExposureStatus.Impaired:
                            toxicExposed++;
                            toxicImpaired++;
                            break;
                        case ToxicExposureStatus.Incapacitated:
                            toxicExposed++;
                            toxicIncapacitated++;
                            break;
                    }

                    if (agent.IsActive &&
                        agent.CurrentToxicConcentration >= simulation.ToxicFumeSystem.Configuration.DangerConcentration)
                    {
                        toxicHighConcentration++;
                    }

                    toxicExposureSum += agent.CumulativeToxicExposure;
                    peakToxicConcentration = Math.Max(peakToxicConcentration, agent.PeakToxicConcentration);
                    toxicExposureSamples++;
                }

                if (!agent.IsActive)
                {
                    continue;
                }

                inRoom++;
                bool isMobile = agent.State is not
                    (AgentState.Dead or AgentState.Incapacitated or AgentState.TempIncap or AgentState.Fallen);
                if (!isMobile)
                {
                    continue;
                }

                mobile++;
                float panic = agent.PanicLevel;
                panicSum += panic;
                maximumPanic = Math.Max(maximumPanic, panic);
                visibilitySum += agent.VisibilityFactor;
                congestionSum += agent.LocalCongestion;
                if (agent.LastRunningPanicStimulus > 0.05f) runningContagion++;

                switch (agent.Intent)
                {
                    case BehaviorIntent.ThreatEscape:
                        threatEscape++;
                        break;
                    case BehaviorIntent.FollowCrowd:
                        followingCrowd++;
                        break;
                    case BehaviorIntent.AssistCasualty:
                        if (agent.AssistPauseUntilFrame >= 0 && simulation.FrameIndex < agent.AssistPauseUntilFrame)
                            currentlyHelping++;
                        else
                            goingToAssist++;
                        break;
                }
            }

            float inverseMobile = mobile > 0 ? 1f / mobile : 0f;
            return new PopulationMetrics(
                InRoom: inRoom,
                Mobile: mobile,
                Exited: exited,
                Injured: injured,
                Crawling: crawling,
                Fallen: fallen,
                TempIncap: tempIncap,
                Incapacitated: incapacitated,
                Dead: dead,
                AveragePanic: panicSum * inverseMobile,
                MaximumPanic: maximumPanic,
                AverageVisibility: mobile == 0 ? 1f : visibilitySum * inverseMobile,
                AverageCongestion: congestionSum * inverseMobile,
                RunningContagion: runningContagion,
                ThreatEscape: threatEscape,
                FollowingCrowd: followingCrowd,
                GoingToAssist: goingToAssist,
                CurrentlyHelping: currentlyHelping,
                ToxicExposed: toxicExposed,
                ToxicImpaired: toxicImpaired,
                ToxicIncapacitated: toxicIncapacitated,
                ToxicHighConcentration: toxicHighConcentration,
                AverageToxicExposure: toxicExposureSamples > 0 ? toxicExposureSum / toxicExposureSamples : 0f,
                PeakToxicConcentration: peakToxicConcentration,
                ElapsedSeconds: simulation.FrameIndex * simulation.Config.Agent.Dt);
        }
    }
}
