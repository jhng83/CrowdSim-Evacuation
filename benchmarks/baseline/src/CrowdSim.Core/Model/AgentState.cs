namespace CrowdSim.Core.Model;

/// <summary>Finite physical/operational states of an agent in the evacuation scenario.</summary>
public enum AgentState
{
    Wandering,
    WaitingToReact,
    Evacuating,
    Exited,
    Injured,
    Fallen,
    Crawling,
    Incapacitated,
    TempIncap,
    Dead
}

/// <summary>
/// Short-lived behavioural intent layered on top of AgentState. This keeps body-state colours
/// stable while allowing heterogeneous emergency responses such as orienting, moving away from
/// the immediate threat, following an obvious evacuation flow, or briefly attending a casualty.
/// </summary>
public enum BehaviorIntent
{
    Unaware,
    Orienting,
    ThreatEscape,
    Evacuate,
    FollowCrowd,
    AssistCasualty
}

/// <summary>
/// Short-lived response to pressure near a congested doorway. This is separate from
/// panic so a calm pedestrian may still yield, seek a gap, or press after prolonged delay.
/// </summary>
public enum BottleneckResponseMode
{
    Free,
    Yield,
    SeekGap,
    Press
}

/// <summary>Strategy used to choose an exit.</summary>
public enum DecisionStrategy
{
    Nearest,
    LeastCrowded
}
