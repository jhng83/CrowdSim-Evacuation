using System.Numerics;

namespace CrowdSim.Core.Model;

/// <summary>Navigation decisions, independent of injury, panic and physical body state.</summary>
public enum NavigationRecoveryStage
{
    FollowingRoute,
    AssessingBlockage,
    LocalDetour,
    WiderDetour,
    ReconsideringExit,
    WaitingForOpening,
}

/// <summary>
/// Small, fixed-capacity movement/failure history. Route replacement deliberately does not
/// clear this memory: repeatedly issuing the same unsuccessful plan is not actual progress.
/// </summary>
public sealed class NaturalNavigationMemory
{
    private readonly PositionSample[] _history = new PositionSample[24];
    private readonly FailedLocation[] _failures = new FailedLocation[6];
    private int _historyCount;
    private int _historyHead;
    private int _failureHead;
    private int _lastSampleFrame = -1;
    private int _lastProgressFrame = -1;
    private int _loopSuppressedUntilFrame = -1;
    private Vector2 _lastPosition;
    private Vector2 _lastTarget;
    private float _bestTargetDistance;
    private float _travelled;
    private float _bestFieldCost = float.PositiveInfinity;
    private int _fieldVersion = -1;

    public NavigationRecoveryStage Stage { get; internal set; }
    public float NoProgressSeconds { get; private set; }
    public float LongestNoProgressSeconds { get; private set; }
    public bool LoopDetected { get; private set; }
    public Vector2? DetourGoal { get; internal set; }
    public int RecoveryAttempts { get; internal set; }
    public int TotalRecoveryAttempts { get; internal set; }
    public int SuccessfulDetours { get; internal set; }
    public int ExitChanges { get; internal set; }
    public int RoutePlans { get; internal set; }
    public bool RecoveryRequested { get; internal set; }

    internal float RemainingFieldCost { get; set; } = float.PositiveInfinity;
    internal int FieldGeometryVersion { get; set; } = -1;
    internal int FieldCostFrame { get; set; } = -9999;
    internal int LastRecoveryFrame { get; set; } = -9999;
    internal int RecoveryEpisodeStartedFrame { get; set; } = -1;
    internal bool ReplanAfterDetour { get; set; }
    internal Vector2 RecoveryRequestPosition { get; set; }
    internal int NextObservationFrame { get; set; }
    internal int NextRecoveryFrame { get; set; }
    internal int DetourStartedFrame { get; set; }
    internal int DetourUntilFrame { get; set; }
    internal int DetourLastProgressFrame { get; set; }
    internal int DetourNextCheckFrame { get; set; }
    internal float DetourBestDistance { get; set; }
    internal bool RouteQueued { get; set; }
    internal Vector2 RequestedRouteGoal { get; set; }
    internal string? RequestedRouteKey { get; set; }
    internal int NextRouteRequestFrame { get; set; }
    internal int NextRouteCheckFrame { get; set; }
    internal int IgnoreSharedFieldUntilFrame { get; set; }
    internal Vector2? GuidancePoint { get; set; }
    internal string? GuidanceKey { get; set; }
    internal int GuidanceRefreshFrame { get; set; }
    internal int LastExitChangeFrame { get; set; } = -9999;
    internal string? RecentlyFailedExit { get; set; }
    internal int FailedExitUntilFrame { get; set; }
    internal int NextHazardCheckFrame { get; set; }
    internal Vector2 FurnitureAvoidance { get; set; }
    internal int NextToxicExitCheckFrame { get; set; }
    internal int NextToxicSteeringFrame { get; set; }
    internal Vector2 ToxicSteeringInput { get; set; }
    internal Vector2 ToxicSteeringDirection { get; set; }
    internal bool ToxicSteeringActive { get; set; }
    internal float DetourToxicPeak { get; set; }
    internal float DetourToxicEnd { get; set; }
    internal Dictionary<string, (float Cost, int Frame)> ExitObservations { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Observe real motion toward a stable waypoint. Changing the waypoint does not itself
    /// reset the clock. A returning, sufficiently long trajectory is a loop even if intermediate
    /// samples briefly got closer to the destination. All distances here are metres.
    /// </summary>
    internal void Observe(Vector2 position, Vector2 target, int frame, float dt,
        float remainingFieldCost = float.PositiveInfinity, int fieldVersion = -1)
    {
        if (_lastSampleFrame == frame) return;
        float distance = Vector2.Distance(position, target);
        if (_lastSampleFrame < 0 || frame < _lastSampleFrame)
        {
            _lastPosition = position;
            _lastTarget = target;
            _bestTargetDistance = distance;
            _lastProgressFrame = frame;
            _lastSampleFrame = frame;
            Record(position, frame);
            return;
        }

        float moved = Vector2.Distance(position, _lastPosition);
        _travelled += moved;
        bool sameTarget = Vector2.DistanceSquared(target, _lastTarget) < 0.04f;
        bool reachedPrevious = Vector2.Distance(position, _lastTarget) <= 0.32f && moved >= 0.025f;
        bool usefulProgress = sameTarget && _bestTargetDistance - distance >= 0.10f;
        bool fieldProgress = false;
        if (float.IsFinite(remainingFieldCost))
        {
            if (_fieldVersion != fieldVersion)
            {
                _fieldVersion = fieldVersion;
                _bestFieldCost = remainingFieldCost;
            }
            else if (_bestFieldCost - remainingFieldCost >= 0.10f && moved >= 0.025f)
            {
                fieldProgress = true;
                _bestFieldCost = remainingFieldCost;
            }
        }
        if (reachedPrevious || usefulProgress || fieldProgress)
        {
            _lastProgressFrame = frame;
            _bestTargetDistance = distance;
        }
        else if (!sameTarget)
        {
            _bestTargetDistance = distance;
        }

        if (frame >= _loopSuppressedUntilFrame)
        {
            for (int i = 0; i < _historyCount; i++)
            {
                PositionSample previous = _history[i];
                if ((frame - previous.Frame) * dt < 1.8f ||
                    _travelled - previous.Travelled < 2.2f) continue;
                if (Vector2.DistanceSquared(position, previous.Position) < 0.20f)
                {
                    LoopDetected = true;
                    break;
                }
            }
        }

        NoProgressSeconds = Math.Max(0f, (frame - _lastProgressFrame) * dt);
        LongestNoProgressSeconds = Math.Max(LongestNoProgressSeconds, NoProgressSeconds);
        _lastPosition = position;
        _lastTarget = target;
        _lastSampleFrame = frame;
        Record(position, frame);
    }

    /// <summary>A new exit changes the cost scale, not the physical progress clock.</summary>
    internal void ResetRouteCostObservation()
    {
        _fieldVersion = -1;
        _bestFieldCost = float.PositiveInfinity;
        RemainingFieldCost = float.PositiveInfinity;
        FieldCostFrame = -9999;
    }

    private void Record(Vector2 position, int frame)
    {
        _history[_historyHead] = new PositionSample(position, frame, _travelled);
        _historyHead = (_historyHead + 1) % _history.Length;
        _historyCount = Math.Min(_history.Length, _historyCount + 1);
    }

    internal void AcknowledgeLoop(int frame, int suppressionFrames)
    {
        LoopDetected = false;
        _loopSuppressedUntilFrame = frame + suppressionFrames;
        // Keep breadcrumbs for candidate scoring. Suppression alone prevents rediscovery
        // of the same old loop while the newly selected escape is just starting.
    }

    internal void RememberFailure(Vector2 point, int untilFrame)
    {
        _failures[_failureHead] = new FailedLocation(point, untilFrame);
        _failureHead = (_failureHead + 1) % _failures.Length;
    }

    internal float FailurePenalty(Vector2 point, int frame)
    {
        float penalty = 0f;
        foreach (FailedLocation failure in _failures)
        {
            if (failure.UntilFrame <= frame) continue;
            float distance = Vector2.Distance(point, failure.Position);
            penalty += Math.Max(0f, 1f - distance / 1.35f);
        }
        return penalty;
    }

    internal float RecentVisitPenalty(Vector2 point, int frame, float dt)
    {
        float penalty = 0f;
        for (int i = 0; i < _historyCount; i++)
        {
            PositionSample sample = _history[i];
            if ((frame - sample.Frame) * dt < 1.2f) continue;
            penalty = Math.Max(penalty, Math.Max(0f, 1f - Vector2.Distance(point, sample.Position) / 0.85f));
        }
        return penalty;
    }

    private readonly record struct PositionSample(Vector2 Position, int Frame, float Travelled);
    private readonly record struct FailedLocation(Vector2 Position, int UntilFrame);
}
