using System.Diagnostics;
using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Geometry;
using CrowdSim.Core.Model;
using CrowdSim.Core.Systems;
using CrowdSim.OpenGL.UI;
using ImGuiNET;

namespace CrowdSim.OpenGL.Rendering;

/// <summary>
/// 3D isometric crowd renderer. Simulation physics stay on the 2D ground plane, but the world is
/// rendered as projected 3D geometry: raised walls, debris and simple humanoids with separate head,
/// torso, arms and legs. Dear ImGui submits the projected primitives through Silk.NET's OpenGL
/// backend, avoiding a second custom matrix/render pipeline.
/// </summary>
public sealed partial class WorldRenderer
{
    private const float WallHeight = 1.82f;
    private const float HumanoidHeight = 1.70f;
    private const float HumanoidWidth = 0.48f;
    private static readonly Vector4 FloorColor = new(0.80f, 0.80f, 0.80f, 1f);

    // The original baseline used a 42-world-unit-tall humanoid. Render-only overlays that
    // were authored in those legacy world units must be scaled to metres as well; otherwise
    // panic rings and debug vectors become ~25x too large after the metric conversion.
    private const float LegacyRenderUnitMeters = HumanoidHeight / 42f;
    private const float MinimumAnimationBaseSpeedMetersPerStep = 0.25f * LegacyRenderUnitMeters;
    private const float StandingMotionThresholdMetersPerStep = 0.08f * LegacyRenderUnitMeters;
    private const float CrawlMotionStartMetersPerStep = 0.03f * LegacyRenderUnitMeters;
    private const float CrawlMotionFullMetersPerStep = 0.20f * LegacyRenderUnitMeters;
    private const float HeadingMotionThresholdSquared =
        1e-5f * LegacyRenderUnitMeters * LegacyRenderUnitMeters;
    private const float PanicRingMaxExpansionMeters = 5f * LegacyRenderUnitMeters;
    private const float DesiredVectorLengthMeters = 32f * LegacyRenderUnitMeters;
    private const float AvoidanceVectorLengthMeters = 30f * LegacyRenderUnitMeters;
    private const float HerdVectorLengthMeters = 28f * LegacyRenderUnitMeters;
    private const float BlastVectorLengthMeters = 26f * LegacyRenderUnitMeters;
    private const float SfmVectorMaxLengthMeters = 45f * LegacyRenderUnitMeters;
    private readonly Camera3D _camera = new();
    private readonly Dictionary<int, Vector2> _renderPositions = new();
    private readonly Dictionary<int, Agent> _agentById = new();
    private readonly Dictionary<int, Queue<Vector2>> _trails = new();
    private readonly Dictionary<int, float> _gaitPhases = new();
    private readonly HashSet<int> _activeAgentIds = new();
    private readonly HashSet<int> _visibleAgentIds = new();
    private readonly List<int> _staleAgentIds = new();
    private readonly List<RenderEntity> _renderEntities = new(768);
    private readonly List<CachedWallChunk> _cachedWallChunks = new(128);
    private readonly CachedBlastBand[] _cachedBlastBands =
    {
        new(), new(), new(), new(), new(),
    };
    private int _cachedWallLayoutVersion = -1;
    private int _cachedBlastLayoutHash = int.MinValue;
    private int _lastTrailFrame = -1;
    private Vector2 _canvasMin;
    private Vector2 _canvasMax;
    private int? _hiddenAgentId;
    private bool _followAgent;
    private int? _selectedAgentId;
    private int _currentFrameIndex;
    private float _currentSimulationSpeed = 1f;
    private float _orthographicHumanoidScreenHeight;
    private bool _adaptiveFastHumanoids;

    /// <summary>Whether the pointer is currently inside the 3D simulation canvas.</summary>
    public bool IsHovered { get; private set; }

    /// <summary>Camera used by the world and exposed to the sidebar controls.</summary>
    public Camera3D Camera => _camera;

    /// <summary>Approximate visual width of a standing humanoid in world units.</summary>
    public float AgentVisualWidth => HumanoidWidth;

    /// <summary>CPU time used by the projected world draw on the latest frame.</summary>
    public double LastDrawMilliseconds { get; private set; }

    /// <summary>Number of active agents that passed the renderer's viewport culling.</summary>
    public int LastVisibleAgentCount { get; private set; }

    /// <summary>Clear render-only caches such as position smoothing and trails.</summary>
    public void ResetVisualState()
    {
        _renderPositions.Clear();
        _agentById.Clear();
        _trails.Clear();
        _gaitPhases.Clear();
        _activeAgentIds.Clear();
        _visibleAgentIds.Clear();
        _staleAgentIds.Clear();
        _renderEntities.Clear();
        _cachedWallChunks.Clear();
        _cachedFixedFurnitureChunks.Clear();
        foreach (CachedBlastBand band in _cachedBlastBands)
        {
            band.Clear();
        }
        _cachedWallLayoutVersion = -1;
        _cachedBlastLayoutHash = int.MinValue;
        _lastTrailFrame = -1;
        LastVisibleAgentCount = 0;
    }

    /// <summary>Draw the complete world into the currently active ImGui child window.</summary>
    internal void Draw(
        Simulation simulation,
        bool builderMode,
        BuilderTool builderTool,
        int blockageCells,
        int exitPersonsWide,
        int agentPlacementCount,
        int? selectedAgentId,
        Agent? cameraAgent,
        bool followAgent,
        bool povEnabled,
        float simulationSpeed,
        RenderDebugOptions debug,
        float furnitureRotation = 0f,
        bool snapBarricadeEnds = true)
    {
        long drawStartTimestamp = Stopwatch.GetTimestamp();
        Vector2 available = ImGui.GetContentRegionAvail();
        available.X = Math.Max(100f, available.X);
        available.Y = Math.Max(100f, available.Y);

        ImGui.InvisibleButton("##crowd-world-canvas", available);
        _canvasMin = ImGui.GetItemRectMin();
        _canvasMax = ImGui.GetItemRectMax();
        IsHovered = ImGui.IsItemHovered();

        _hiddenAgentId = povEnabled ? cameraAgent?.AgentId : null;
        _followAgent = followAgent && cameraAgent is not null && !povEnabled;
        _selectedAgentId = selectedAgentId;
        _currentFrameIndex = simulation.FrameIndex;
        _currentSimulationSpeed = Math.Clamp(simulationSpeed, 0.05f, 32f);

        int activeCount = 0;
        _agentById.Clear();
        foreach (Agent candidate in simulation.Agents)
        {
            _agentById[candidate.AgentId] = candidate;
            if (candidate.IsActive)
            {
                activeCount++;
            }
        }

        // Adaptive presentation only: physics remains untouched. All agent LODs use
        // world-space 3D body parts. Under load, reduced figures omit small details while
        // retaining depth, shading and animation. Hysteresis prevents repeated load switches.
        if (_currentSimulationSpeed >= 4f || activeCount >= 220 || LastDrawMilliseconds >= 20.0)
        {
            _adaptiveFastHumanoids = true;
        }
        else if (_currentSimulationSpeed <= 2f && activeCount <= 170 && LastDrawMilliseconds <= 11.0)
        {
            _adaptiveFastHumanoids = false;
        }

        if (povEnabled && cameraAgent is not null)
        {
            _camera.ConfigurePov(
                _canvasMin,
                _canvasMax,
                cameraAgent.Position,
                GetHeading(cameraAgent),
                eyeHeight: HumanoidHeight * 0.88f);
        }
        else
        {
            _camera.Configure(
                _canvasMin,
                _canvasMax,
                simulation.Config.World.WidthMeters,
                simulation.Config.World.LengthMeters,
                maximumHeight: 3.24f,
                focusWorld: _followAgent ? cameraAgent?.Position : null);
        }

        if (IsHovered && MathF.Abs(ImGui.GetIO().MouseWheel) > 0.001f && !_camera.IsPov)
        {
            _camera.AdjustZoom(ImGui.GetIO().MouseWheel);
            _camera.Configure(
                _canvasMin,
                _canvasMax,
                simulation.Config.World.WidthMeters,
                simulation.Config.World.LengthMeters,
                maximumHeight: 3.24f,
                focusWorld: _followAgent ? cameraAgent?.Position : null);
        }

        // In the orthographic overview every standing humanoid has the same projected height.
        // Measure it once instead of projecting foot/head again for every agent. POV remains
        // perspective-correct and computes size per agent because distance matters there.
        _orthographicHumanoidScreenHeight = _camera.IsPov
            ? 0f
            : EstimateHumanoidScreenHeight(Vector2.Zero);

        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        draw.PushClipRect(_canvasMin, _canvasMax, true);
        draw.AddRectFilled(_canvasMin, _canvasMax, Color(0.045f, 0.052f, 0.065f, 1f));

        DrawFloor(draw, simulation);
        DrawGrid(draw, simulation);
        DrawExitMarkers(draw, simulation);
        DrawExplosionGroundEffects(draw, simulation, debug);
        DrawToxicFumeGroundEffects(draw, simulation, builderMode, debug);

        if (builderMode)
        {
            DrawPlannedAgentPlacements(draw, simulation);
        }

        if (debug.ShowCongestion)
        {
            DrawCongestion(draw, simulation);
        }

        UpdateRenderPositions(simulation);
        UpdateGaitPhases(simulation, simulationSpeed);
        UpdateTrails(simulation, debug.ShowTrails);
        DrawDepthSortedEntities(draw, simulation);
        DrawAgentOverlays(draw, simulation, selectedAgentId, debug);

        if (builderMode)
        {
            DrawBuilderPreview(draw, simulation, builderTool, blockageCells, exitPersonsWide, agentPlacementCount, furnitureRotation, snapBarricadeEnds);
        }

        DrawWorldLegend(draw, simulation, builderMode);
        draw.PopClipRect();
        LastDrawMilliseconds =
            (Stopwatch.GetTimestamp() - drawStartTimestamp) * 1000.0 / Stopwatch.Frequency;
    }

    /// <summary>Convert the current mouse position to the Z=0 simulation ground plane.</summary>
    public bool TryMouseToWorld(out Vector2 world)
    {
        world = default;
        if (!IsHovered || !_camera.TryScreenToGround(ImGui.GetMousePos(), out Vector2 candidate))
        {
            return false;
        }

        world = candidate;
        return true;
    }

    private void DrawFloor(ImDrawListPtr draw, Simulation simulation)
    {
        if (_camera.IsPov)
        {
            // First-person view uses a simple horizon/floor backdrop. The actual room geometry
            // is still projected in front of it, but we avoid projecting a giant floor quad
            // through the near plane behind the agent's head.
            float horizonY = _canvasMin.Y + (_canvasMax.Y - _canvasMin.Y) * 0.43f;
            draw.AddRectFilled(
                _canvasMin,
                new Vector2(_canvasMax.X, horizonY),
                Color(0.035f, 0.043f, 0.055f, 1f));
            draw.AddRectFilled(
                new Vector2(_canvasMin.X, horizonY),
                _canvasMax,
                Color(FloorColor));
            return;
        }

        float w = simulation.Config.World.WidthMeters;
        float h = simulation.Config.World.LengthMeters;
        DrawFlatQuad(
            draw,
            new Vector3(0f, 0f, 0f),
            new Vector3(w, 0f, 0f),
            new Vector3(w, h, 0f),
            new Vector3(0f, h, 0f),
            Color(FloorColor));
    }

    private void DrawGrid(ImDrawListPtr draw, Simulation simulation)
    {
        if (!simulation.Config.Grid.Enabled)
        {
            return;
        }

        float width = simulation.Config.World.WidthMeters;
        float height = simulation.Config.World.LengthMeters;
        float step = Math.Max(0.20f, simulation.Config.Grid.CellSizeMeters);
        uint color = Color(0.35f, 0.35f, 0.35f, 0.25f);

        for (float x = 0f; x <= width; x += step)
        {
            DrawWorldLine(
                draw,
                new Vector3(x, 0f, 0.05f),
                new Vector3(x, height, 0.05f),
                color,
                1f);
        }

        for (float y = 0f; y <= height; y += step)
        {
            DrawWorldLine(
                draw,
                new Vector3(0f, y, 0.05f),
                new Vector3(width, y, 0.05f),
                color,
                1f);
        }
    }

    private void DrawExitMarkers(ImDrawListPtr draw, Simulation simulation)
    {
        foreach (Exit exit in simulation.Environment.Exits)
        {
            float half = Math.Max(0.08f, exit.Size);
            Vector4 glow = new(0.08f, 0.72f, 0.95f, 0.78f);
            Vector4 post = new(0.12f, 0.82f, 1.00f, 1f);
            const float depth = 0.36f;

            if (exit.Side is "N" or "S")
            {
                DrawFlatQuad(
                    draw,
                    new Vector3(exit.Position.X - half, exit.Position.Y - depth, 0.15f),
                    new Vector3(exit.Position.X + half, exit.Position.Y - depth, 0.15f),
                    new Vector3(exit.Position.X + half, exit.Position.Y + depth, 0.15f),
                    new Vector3(exit.Position.X - half, exit.Position.Y + depth, 0.15f),
                    Color(glow));

                DrawOrientedPrism(draw, new Vector2(exit.Position.X - half, exit.Position.Y),
                    -0.08f, 0.08f, -0.08f, 0.08f, 0f, 1.38f, 0f, post);
                DrawOrientedPrism(draw, new Vector2(exit.Position.X + half, exit.Position.Y),
                    -0.08f, 0.08f, -0.08f, 0.08f, 0f, 1.38f, 0f, post);
            }
            else
            {
                DrawFlatQuad(
                    draw,
                    new Vector3(exit.Position.X - depth, exit.Position.Y - half, 0.15f),
                    new Vector3(exit.Position.X + depth, exit.Position.Y - half, 0.15f),
                    new Vector3(exit.Position.X + depth, exit.Position.Y + half, 0.15f),
                    new Vector3(exit.Position.X - depth, exit.Position.Y + half, 0.15f),
                    Color(glow));

                DrawOrientedPrism(draw, new Vector2(exit.Position.X, exit.Position.Y - half),
                    -0.08f, 0.08f, -0.08f, 0.08f, 0f, 1.38f, 0f, post);
                DrawOrientedPrism(draw, new Vector2(exit.Position.X, exit.Position.Y + half),
                    -0.08f, 0.08f, -0.08f, 0.08f, 0f, 1.38f, 0f, post);
            }
        }
    }

    private void DrawToxicFumeGroundEffects(
        ImDrawListPtr draw,
        Simulation simulation,
        bool builderMode,
        RenderDebugOptions debug)
    {
        if (simulation.ScenarioType != ScenarioType.ToxicFume ||
            simulation.ToxicFumeSystem.SourcePosition is not Vector2 source)
        {
            return;
        }

        // The source marker remains visible even when the concentration overlay is hidden.
        DrawGroundCircle(
            draw,
            source,
            Math.Max(0.18f, simulation.ToxicFumeSystem.Configuration.InitialRadiusMeters),
            0.055f,
            new Vector4(0.18f, 0.92f, 0.48f, 0.55f),
            36);

        if (!debug.ShowToxicFume || !simulation.ToxicFumeSystem.IsPrecomputed)
        {
            return;
        }

        float elapsed = simulation.ToxicFumeSystem.IsReleased
            ? simulation.ToxicFumeSystem.GetElapsedSeconds(simulation)
            : builderMode
                ? simulation.ToxicFumeSystem.PreviewTimeSeconds
                : 0f;

        int cells = simulation.ToxicFumeSystem.GridCellCount;
        int stride = cells > 30000 ? 3 : cells > 6000 ? 2 : 1;
        foreach (ToxicFumeCellSample sample in simulation.ToxicFumeSystem.EnumerateCells(elapsed, 0.025f, stride))
        {
            float c = Math.Clamp(sample.Concentration, 0f, 1f);
            float half = sample.CellSizeMeters * 0.50f;
            // Green/yellow normalized field: opacity conveys concentration while remaining
            // visually distinct from the explosion's red/orange blast overlays.
            Vector4 tint = new(
                0.18f + 0.58f * c,
                0.82f - 0.18f * c,
                0.30f - 0.12f * c,
                0.07f + 0.30f * c);
            DrawFlatQuad(
                draw,
                new Vector3(sample.Center.X - half, sample.Center.Y - half, 0.065f),
                new Vector3(sample.Center.X + half, sample.Center.Y - half, 0.065f),
                new Vector3(sample.Center.X + half, sample.Center.Y + half, 0.065f),
                new Vector3(sample.Center.X - half, sample.Center.Y + half, 0.065f),
                Color(tint));

            if (debug.ShowToxicGrid)
            {
                uint gridColor = Color(new Vector4(0.55f, 1.0f, 0.66f, 0.18f));
                Vector3 a = new(sample.Center.X - half, sample.Center.Y - half, 0.07f);
                Vector3 b = new(sample.Center.X + half, sample.Center.Y - half, 0.07f);
                Vector3 c1 = new(sample.Center.X + half, sample.Center.Y + half, 0.07f);
                Vector3 d = new(sample.Center.X - half, sample.Center.Y + half, 0.07f);
                DrawWorldLine(draw, a, b, gridColor, 1f);
                DrawWorldLine(draw, b, c1, gridColor, 1f);
                DrawWorldLine(draw, c1, d, gridColor, 1f);
                DrawWorldLine(draw, d, a, gridColor, 1f);
            }
        }
    }

    private void DrawExplosionGroundEffects(
        ImDrawListPtr draw,
        Simulation simulation,
        RenderDebugOptions debug)
    {
        Explosion? explosion = simulation.Environment.Explosion;
        if (explosion is null)
        {
            return;
        }

        // Persistent analysis zones. The five annuli use the same configured radii as the
        // event physics, are clipped by room boundaries/walls, and are rendering-only after
        // the one-time shock outcome has been applied.
        if (debug.ShowBlastZones)
        {
            DrawPersistentBlastZones(draw, simulation, explosion.Position, debug);
        }

        float elapsed = Math.Max(0, simulation.FrameIndex - explosion.TriggeredFrame) * simulation.Config.Agent.StepScale;
        float shockRadius = Math.Min(
            simulation.Config.Agent.HeatRadius,
            elapsed * simulation.Config.Agent.ShockwaveSpeedMetersPerFrame);
        int frontEndFrame = (int)MathF.Ceiling(
            simulation.Config.Agent.HeatRadius /
            Math.Max(simulation.Config.Agent.ShockwaveSpeedMetersPerFrame, 1e-5f)) + 3;

        // Active event presentation remains a thin, fast, translucent shock front.
        if (elapsed <= frontEndFrame && shockRadius > 1f)
        {
            DrawShieldedGroundRing(
                draw,
                simulation,
                explosion.Position,
                Math.Max(0.04f, shockRadius - 0.28f),
                shockRadius + 0.20f,
                0.82f,
                new Vector4(0.82f, 0.92f, 1f, 0.16f),
                _adaptiveFastHumanoids ? 56 : 96);
            DrawShieldedGroundCircle(
                draw,
                simulation,
                explosion.Position,
                shockRadius,
                0.90f,
                new Vector4(0.96f, 0.99f, 1f, 0.74f),
                _adaptiveFastHumanoids ? 64 : 112);
        }
    }

    private void DrawPersistentBlastZones(
        ImDrawListPtr draw,
        Simulation simulation,
        Vector2 origin,
        RenderDebugOptions debug)
    {
        EnsurePersistentBlastCache(simulation, origin);

        // Draw outer-to-inner non-overlapping bands so the display remains readable.
        if (debug.ShowPanicZone)
        {
            DrawCachedBlastBand(draw, _cachedBlastBands[4]);
        }

        if (debug.ShowInjuryZone)
        {
            DrawCachedBlastBand(draw, _cachedBlastBands[3]);
        }

        if (debug.ShowFallenZone)
        {
            DrawCachedBlastBand(draw, _cachedBlastBands[2]);
        }

        if (debug.ShowIncapacitationZone)
        {
            DrawCachedBlastBand(draw, _cachedBlastBands[1]);
        }

        if (debug.ShowKillZone)
        {
            DrawCachedBlastBand(draw, _cachedBlastBands[0]);
        }
    }

    private void EnsurePersistentBlastCache(Simulation simulation, Vector2 origin)
    {
        var config = simulation.Config.Agent;
        HashCode hash = new();
        hash.Add(simulation.Environment.WallLayoutVersion);
        hash.Add(origin.X);
        hash.Add(origin.Y);
        hash.Add(config.HardKillRadius);
        hash.Add(config.HeatIncapacityRadius);
        hash.Add(config.FallenRadius);
        hash.Add(config.InjuryRadius);
        hash.Add(config.HeatRadius);
        int layoutHash = hash.ToHashCode();
        if (layoutHash == _cachedBlastLayoutHash)
        {
            return;
        }

        _cachedBlastLayoutHash = layoutHash;
        BuildCachedBlastBand(
            _cachedBlastBands[0], simulation, origin, 0f, config.HardKillRadius, BlastZoneStyle.Kill);
        BuildCachedBlastBand(
            _cachedBlastBands[1], simulation, origin, config.HardKillRadius, config.HeatIncapacityRadius,
            BlastZoneStyle.Incapacitation);
        BuildCachedBlastBand(
            _cachedBlastBands[2], simulation, origin, config.HeatIncapacityRadius, config.FallenRadius,
            BlastZoneStyle.Fallen);
        BuildCachedBlastBand(
            _cachedBlastBands[3], simulation, origin, config.FallenRadius, config.InjuryRadius,
            BlastZoneStyle.Injury);
        BuildCachedBlastBand(
            _cachedBlastBands[4], simulation, origin, config.InjuryRadius, config.HeatRadius,
            BlastZoneStyle.Panic);
    }

    private void BuildCachedBlastBand(
        CachedBlastBand cache,
        Simulation simulation,
        Vector2 origin,
        float innerRadius,
        float outerRadius,
        Vector4 baseColor)
    {
        cache.Clear();
        cache.FillColor = Color(new Vector4(baseColor.X, baseColor.Y, baseColor.Z, 0.075f));
        cache.OutlineColor = Color(new Vector4(baseColor.X, baseColor.Y, baseColor.Z, 0.68f));

        const int bandSegments = 128;
        float safeInner = Math.Max(0f, innerRadius);
        float safeOuter = Math.Max(safeInner + 0.02f, outerRadius);
        for (int i = 0; i < bandSegments; i++)
        {
            float angleA = MathF.Tau * i / bandSegments;
            float angleB = MathF.Tau * (i + 1) / bandSegments;
            Vector2 directionA = new(MathF.Cos(angleA), MathF.Sin(angleA));
            Vector2 directionB = new(MathF.Cos(angleB), MathF.Sin(angleB));
            Vector2 innerA = origin + directionA * safeInner;
            Vector2 outerA = origin + directionA * safeOuter;
            Vector2 innerB = origin + directionB * safeInner;
            Vector2 outerB = origin + directionB * safeOuter;
            Vector2 midpoint = (innerA + outerA + innerB + outerB) * 0.25f;

            if (!BlastZonePointIsVisible(simulation, origin, innerA) ||
                !BlastZonePointIsVisible(simulation, origin, outerA) ||
                !BlastZonePointIsVisible(simulation, origin, innerB) ||
                !BlastZonePointIsVisible(simulation, origin, outerB) ||
                !BlastZonePointIsVisible(simulation, origin, midpoint))
            {
                continue;
            }

            cache.Quads.Add(new CachedWorldQuad(
                new Vector3(innerA, 0.22f),
                new Vector3(innerB, 0.22f),
                new Vector3(outerB, 0.22f),
                new Vector3(outerA, 0.22f)));
        }

        const int outlineSegments = 144;
        Vector2 previous = origin + new Vector2(safeOuter, 0f);
        for (int i = 1; i <= outlineSegments; i++)
        {
            float angle = MathF.Tau * i / outlineSegments;
            Vector2 next = origin + new Vector2(
                safeOuter * MathF.Cos(angle),
                safeOuter * MathF.Sin(angle));
            Vector2 midpoint = (previous + next) * 0.5f;
            if (BlastZonePointIsVisible(simulation, origin, previous) &&
                BlastZonePointIsVisible(simulation, origin, midpoint) &&
                BlastZonePointIsVisible(simulation, origin, next))
            {
                cache.Outline.Add(new CachedWorldLine(
                    new Vector3(previous, 0.28f),
                    new Vector3(next, 0.28f)));
            }

            previous = next;
        }
    }

    private void DrawCachedBlastBand(ImDrawListPtr draw, CachedBlastBand cache)
    {
        foreach (CachedWorldQuad quad in cache.Quads)
        {
            DrawFlatQuad(draw, quad.A, quad.B, quad.C, quad.D, cache.FillColor);
        }

        foreach (CachedWorldLine line in cache.Outline)
        {
            DrawWorldLine(draw, line.A, line.B, cache.OutlineColor, 1.4f);
        }
    }

    private void DrawAnalysisBand(
        ImDrawListPtr draw,
        Simulation simulation,
        Vector2 origin,
        float innerRadius,
        float outerRadius,
        Vector4 baseColor)
    {
        Vector4 fill = new(baseColor.X, baseColor.Y, baseColor.Z, 0.075f);
        Vector4 outline = new(baseColor.X, baseColor.Y, baseColor.Z, 0.68f);

        DrawShieldedGroundRing(
            draw,
            simulation,
            origin,
            innerRadius,
            outerRadius,
            0.22f,
            fill,
            128);
        DrawShieldedGroundCircle(
            draw,
            simulation,
            origin,
            outerRadius,
            0.28f,
            outline,
            144);
    }

    private void DrawCongestion(ImDrawListPtr draw, Simulation simulation)
    {
        // LocalCongestion is already computed by the behaviour layer from the spatial hash.
        // Reusing it avoids a second O(N^2) density scan purely for presentation.
        foreach (Agent agent in simulation.Agents)
        {
            if (!agent.IsActive || !IsAgentOnScreen(agent.Position, 24f))
            {
                continue;
            }

            float intensity = Math.Clamp(agent.LocalCongestion, 0f, 1f);
            if (intensity <= 0.08f)
            {
                continue;
            }

            if (!_camera.TryProject(new Vector3(agent.Position, 0.08f), out Vector2 screen))
            {
                continue;
            }

            float screenRadius = 5f + 11f * intensity;
            // Purple congestion heat keeps density diagnostics visually distinct from
            // the red/orange panic ring rendered under individual agents.
            draw.AddCircleFilled(
                screen,
                screenRadius,
                Color(
                    0.48f + 0.32f * intensity,
                    0.10f + 0.08f * (1f - intensity),
                    0.82f + 0.15f * intensity,
                    0.08f + 0.16f * intensity),
                16);
        }
    }

    private void UpdateRenderPositions(Simulation simulation)
    {
        _activeAgentIds.Clear();
        float renderDt = Math.Clamp(ImGui.GetIO().DeltaTime, 0f, 0.10f);
        float responsiveness = 18f * MathF.Sqrt(Math.Clamp(_currentSimulationSpeed, 0.25f, 8f));
        float blend = 1f - MathF.Exp(-responsiveness * renderDt);
        blend = Math.Clamp(blend, 0.12f, 0.85f);

        foreach (Agent agent in simulation.Agents)
        {
            if (!agent.IsActive)
            {
                continue;
            }

            _activeAgentIds.Add(agent.AgentId);
            Vector2 target = agent.Position;
            if (!_renderPositions.TryGetValue(agent.AgentId, out Vector2 current) ||
                !VectorMath.IsFinite(current) || !VectorMath.IsFinite(target))
            {
                current = target;
            }
            else
            {
                // Presentation smoothing only; never writes back into simulation physics.
                // Time-based blending avoids the apparent input/motion delay that the old fixed
                // 0.15 blend produced whenever FPS dropped or simulation speed increased.
                current += (target - current) * blend;
            }

            _renderPositions[agent.AgentId] = current;
        }

        _staleAgentIds.Clear();
        foreach (int id in _renderPositions.Keys)
        {
            if (!_activeAgentIds.Contains(id))
            {
                _staleAgentIds.Add(id);
            }
        }

        foreach (int staleId in _staleAgentIds)
        {
            _renderPositions.Remove(staleId);
            _trails.Remove(staleId);
            _gaitPhases.Remove(staleId);
        }
    }

    private void UpdateGaitPhases(Simulation simulation, float simulationSpeed)
    {
        float renderDt = Math.Clamp(ImGui.GetIO().DeltaTime, 0f, 0.10f);
        float timeScale = simulation.Running ? Math.Clamp(simulationSpeed, 0.05f, 32f) : 0f;

        foreach (Agent agent in simulation.Agents)
        {
            if (!agent.IsActive)
            {
                continue;
            }

            if (!_gaitPhases.TryGetValue(agent.AgentId, out float phase))
            {
                // Stable phase offset avoids a whole crowd stepping in sync while remaining
                // deterministic for the same agent id. This is render-only state.
                phase = (agent.AgentId * 2.39996323f) % MathF.Tau;
            }

            if (timeScale > 0f)
            {
                float speed = VectorMath.IsFinite(agent.Velocity)
                    ? agent.Velocity.Length()
                    : 0f;
                float baseSpeed = Math.Max(MinimumAnimationBaseSpeedMetersPerStep, agent.BaseSpeed);
                float speedRatio = speed / baseSpeed;

                // Render-time integration keeps limbs visually smooth even though the crowd
                // physics uses a coarser fixed timestep. Simulation-speed scaling keeps gait
                // synchronized when the user accelerates or slows the whole simulation.
                float locomotion = SmoothStep(0.08f, 0.70f, speedRatio);
                float runBlend = SmoothStep(1.25f, 2.20f, speedRatio);
                float cadenceHz = Lerp(1.35f, 2.55f, runBlend) * locomotion;
                phase = WrapAngle(phase + MathF.Tau * cadenceHz * renderDt * timeScale);
            }

            _gaitPhases[agent.AgentId] = phase;
        }

        // Stale gait entries are removed together with render-position entries in
        // UpdateRenderPositions(), avoiding another hash-set and key-array allocation.
    }

    private void UpdateTrails(Simulation simulation, bool enabled)
    {
        if (!enabled || _lastTrailFrame == simulation.FrameIndex || simulation.FrameIndex % 3 != 0)
        {
            return;
        }

        _lastTrailFrame = simulation.FrameIndex;
        foreach (Agent agent in simulation.Agents.Where(agent => agent.IsActive))
        {
            if (!_trails.TryGetValue(agent.AgentId, out Queue<Vector2>? trail))
            {
                trail = new Queue<Vector2>();
                _trails[agent.AgentId] = trail;
            }

            trail.Enqueue(agent.Position);
            while (trail.Count > 40)
            {
                trail.Dequeue();
            }
        }
    }

    private void DrawDepthSortedEntities(ImDrawListPtr draw, Simulation simulation)
    {
        _renderEntities.Clear();
        _visibleAgentIds.Clear();
        IndexFurniture(simulation);
        EnsureWallChunkCache(simulation);

        foreach (CachedWallChunk cached in _cachedWallChunks)
        {
            Wall wall = cached.Wall;
            Vector2 midpoint = new((wall.X1 + wall.X2) * 0.5f, (wall.Y1 + wall.Y2) * 0.5f);
            _renderEntities.Add(new RenderEntity(
                _camera.Depth(new Vector3(midpoint, 0f)),
                RenderEntityKind.Wall,
                null,
                wall,
                null,
                cached.IsBlockage,
                cached.IsBlueWall));
        }

        foreach ((FurnitureSet set, Wall bounds) in _cachedFixedFurnitureChunks)
        {
            Vector2 midpoint = new((bounds.X1 + bounds.X2) * 0.5f, (bounds.Y1 + bounds.Y2) * 0.5f);
            _renderEntities.Add(new RenderEntity(
                _camera.Depth(new Vector3(midpoint, 0f)), RenderEntityKind.FixedFurniture,
                null, bounds, null, false, false, Furniture: set));
        }

        foreach (FurnitureSet set in simulation.Environment.Furniture)
        {
            if (set.Table is not RectangularFurnitureBody table ||
                table.RectangleObstacle is not Wall bounds || table.State == ChairState.Destroyed) continue;
            _renderEntities.Add(new RenderEntity(
                _camera.Depth(new Vector3(table.Position, table.Height)), RenderEntityKind.FixedFurniture,
                null, bounds, null, false, false, Furniture: set));
        }

        foreach (CircleBlockage circle in simulation.Environment.CircularBlockages)
        {
            _renderEntities.Add(new RenderEntity(
                _camera.Depth(new Vector3(circle.Center, 0f)),
                RenderEntityKind.CircleBlockage,
                null,
                null,
                null,
                true,
                false,
                circle));
        }

        foreach (Agent agent in simulation.Agents)
        {
            if (!agent.IsActive || _hiddenAgentId == agent.AgentId)
            {
                continue;
            }

            Vector2 position = _renderPositions.TryGetValue(agent.AgentId, out Vector2 cached)
                ? cached
                : agent.Position;
            if (!IsAgentOnScreen(position, 42f))
            {
                continue;
            }

            _visibleAgentIds.Add(agent.AgentId);
            _renderEntities.Add(new RenderEntity(
                _camera.Depth(new Vector3(position, 0f)),
                RenderEntityKind.Agent,
                agent,
                null,
                null,
                false,
                false));
        }

        foreach (Debris debris in simulation.Debris)
        {
            if (!IsAgentOnScreen(debris.Position, 24f))
            {
                continue;
            }

            _renderEntities.Add(new RenderEntity(
                _camera.Depth(new Vector3(debris.Position, 0.16f)),
                RenderEntityKind.Debris,
                null,
                null,
                debris,
                false,
                false));
        }

        LastVisibleAgentCount = _visibleAgentIds.Count;
        _renderEntities.Sort(static (left, right) => right.Depth.CompareTo(left.Depth));
        foreach (RenderEntity entity in _renderEntities)
        {
            switch (entity.Kind)
            {
                case RenderEntityKind.Wall when entity.Wall is Wall wall:
                    DrawWall(draw, wall, entity.IsBlockage, entity.IsBlueWall);
                    break;
                case RenderEntityKind.CircleBlockage when entity.Circle is CircleBlockage circle:
                    if (circle.IsFurniture) DrawFurnitureProxy(draw, simulation, circle);
                    else DrawCircleBlockage(draw, circle);
                    break;
                case RenderEntityKind.FixedFurniture when entity.Furniture is FurnitureSet set && entity.Wall is Wall bounds:
                    DrawFixedFurniture(draw, set, bounds, FixedFurnitureColor(set.Kind));
                    break;
                case RenderEntityKind.Agent when entity.Agent is Agent agent:
                    DrawAgent(draw, agent);
                    break;
                case RenderEntityKind.Debris when entity.Debris is Debris debris:
                    DrawDebris(draw, debris);
                    break;
            }
        }
    }

    private void EnsureWallChunkCache(Simulation simulation)
    {
        int layoutVersion = simulation.Environment.WallLayoutVersion;
        if (layoutVersion == _cachedWallLayoutVersion)
        {
            return;
        }

        _cachedWallLayoutVersion = layoutVersion;
        _cachedWallChunks.Clear();
        _cachedFixedFurnitureChunks.Clear();
        HashSet<Wall> blockageSet = new(simulation.Environment.Blockages);
        HashSet<Wall> blueWallSet = new(simulation.Environment.BlueWalls);
        foreach (Wall wall in simulation.Environment.Walls)
        {
            if (wall.IsFurniture) continue; // rendered at its real table/stage height instead
            AddWallChunksToCache(
                wall,
                blockageSet.Contains(wall),
                blueWallSet.Contains(wall));
        }

        CacheFixedFurnitureChunks(simulation);

        // Persistent blast geometry depends on the same walls. Force a rebuild only when
        // static geometry actually changes instead of hashing every wall on every render frame.
        _cachedBlastLayoutHash = int.MinValue;
    }

    private void AddWallChunksToCache(Wall wall, bool blockage, bool blueWall)
    {
        float width = wall.X2 - wall.X1;
        float height = wall.Y2 - wall.Y1;
        const float maxChunk = 2.23f;
        int chunks = Math.Max(1, (int)MathF.Ceiling(Math.Max(width, height) / maxChunk));

        for (int i = 0; i < chunks; i++)
        {
            float t0 = i / (float)chunks;
            float t1 = (i + 1) / (float)chunks;
            Wall chunk = width >= height
                ? new Wall(
                    wall.X1 + (wall.X2 - wall.X1) * t0,
                    wall.Y1,
                    wall.X1 + (wall.X2 - wall.X1) * t1,
                    wall.Y2)
                : new Wall(
                    wall.X1,
                    wall.Y1 + (wall.Y2 - wall.Y1) * t0,
                    wall.X2,
                    wall.Y1 + (wall.Y2 - wall.Y1) * t1);
            _cachedWallChunks.Add(new CachedWallChunk(chunk, blockage, blueWall));
        }
    }

    private bool IsAgentOnScreen(Vector2 position, float margin)
    {
        if (!_camera.TryProject(new Vector3(position, HumanoidHeight * 0.5f), out Vector2 screen))
        {
            return false;
        }

        return screen.X >= _canvasMin.X - margin && screen.X <= _canvasMax.X + margin &&
               screen.Y >= _canvasMin.Y - margin && screen.Y <= _canvasMax.Y + margin;
    }

    private void DrawWall(ImDrawListPtr draw, Wall wall, bool blockage, bool blueWall)
    {
        // Room perimeter walls are visually transparent. Keep their simulation geometry
        // for collision, navigation, exit placement and hazard interactions; only skip drawing.
        // Authored blockages/internal blue walls retain their existing appearance.
        if (!blockage && !blueWall)
        {
            return;
        }

        Vector4 color = blueWall
            ? new Vector4(0.10f, 0.34f, 0.88f, 1f)
            : blockage
                ? new Vector4(0.52f, 0.39f, 0.29f, 1f)
                : new Vector4(0.62f, 0.66f, 0.72f, 1f);
        DrawAxisAlignedPrism(draw, wall.X1, wall.X2, wall.Y1, wall.Y2, 0f, WallHeight, color);
    }

    private void DrawCircleBlockage(ImDrawListPtr draw, CircleBlockage circle)
    {
        const int segments = 28;
        const float height = 1.20f;
        Vector4 baseColor = new(0.52f, 0.39f, 0.29f, 1f);
        uint sideColor = Color(Shade(baseColor, 0.88f));
        uint topColor = Color(Shade(baseColor, 1.05f));

        Vector3 topCenter = new(circle.Center, height);
        if (!_camera.TryProject(topCenter, out Vector2 topCenterScreen))
        {
            return;
        }

        for (int i = 0; i < segments; i++)
        {
            float angleA = MathF.Tau * i / segments;
            float angleB = MathF.Tau * (i + 1) / segments;
            Vector2 a = circle.Center + new Vector2(MathF.Cos(angleA), MathF.Sin(angleA)) * circle.Radius;
            Vector2 b = circle.Center + new Vector2(MathF.Cos(angleB), MathF.Sin(angleB)) * circle.Radius;
            DrawFlatQuad(
                draw,
                new Vector3(a, 0f),
                new Vector3(b, 0f),
                new Vector3(b, height),
                new Vector3(a, height),
                sideColor);

            if (_camera.TryProject(new Vector3(a, height), out Vector2 topA) &&
                _camera.TryProject(new Vector3(b, height), out Vector2 topB))
            {
                draw.AddTriangleFilled(topCenterScreen, topA, topB, topColor);
            }
        }
    }

    private void DrawAgent(ImDrawListPtr draw, Agent agent)
    {
        bool previous = _outlineAgentGeometry;
        _outlineAgentGeometry = true;
        try { DrawAgentGeometry(draw, agent); }
        finally { _outlineAgentGeometry = previous; }
    }

    private void DrawAgentGeometry(ImDrawListPtr draw, Agent agent)
    {
        Vector2 position = _renderPositions.TryGetValue(agent.AgentId, out Vector2 cached)
            ? cached
            : agent.Position;
        Vector2 heading = GetHeading(agent);
        if (agent.Intent == BehaviorIntent.AssistCasualty &&
            agent.AssistTargetAgentId is int targetId &&
            _agentById.TryGetValue(targetId, out Agent? assistTarget) &&
            assistTarget.IsActive)
        {
            heading = VectorMath.SafeNormalize(assistTarget.Position - agent.Position, heading);
        }

        float angle = MathF.Atan2(heading.Y, heading.X);
        Vector4 torso = AgentColor(agent);
        Vector4 limb = Shade(torso, 0.82f);
        Vector4 head = Shade(torso, 1.08f);
        Vector4 pelvis = Shade(torso, 0.92f);
        float scale = HumanoidHeight / 1.70f;

        // All casualty/helper poses use the same FIX11 blocky low-poly design language.
        if (agent.State is AgentState.Fallen or AgentState.TempIncap or AgentState.Incapacitated or AgentState.Dead)
        {
            DrawProneHumanoid(draw, position, angle, scale, torso, limb, head, 0.08f);
            return;
        }

        if (agent.State == AgentState.Crawling)
        {
            DrawCrawlingHumanoid(draw, agent, position, angle, scale * 0.92f, torso, limb, head);
            return;
        }

        if (agent.Posture != SeatingPosture.Walking && agent.Chair is FurnitureChair seat)
        {
            DrawSeatedPerson(draw, position, seat.StartHeading, torso,
                agent.Posture == SeatingPosture.StandingUp ? agent.StandUpProgress : 0f);
            return;
        }

        bool activelyAttending = agent.Intent == BehaviorIntent.AssistCasualty &&
                                 agent.AssistPauseUntilFrame >= 0 &&
                                 _currentFrameIndex < agent.AssistPauseUntilFrame;
        if (activelyAttending)
        {
            DrawAssistingHumanoid(draw, agent, position, angle, scale, torso, limb, head);
            return;
        }

        float speed = VectorMath.IsFinite(agent.Velocity) ? agent.Velocity.Length() : 0f;
        float baseSpeed = Math.Max(MinimumAnimationBaseSpeedMetersPerStep, agent.BaseSpeed);
        float speedRatio = speed / baseSpeed;
        float moveBlend = SmoothStep(0.08f, 0.70f, speedRatio);
        float runBlend = SmoothStep(1.25f, 2.20f, speedRatio);
        float phase = _gaitPhases.TryGetValue(agent.AgentId, out float gaitPhase) ? gaitPhase : 0f;

        float projectedHeight = _camera.IsPov
            ? EstimateHumanoidScreenHeight(position)
            : _orthographicHumanoidScreenHeight;
        float fullDetailThreshold = _currentSimulationSpeed >= 8f
            ? 72f
            : _currentSimulationSpeed >= 4f
                ? 58f
                : 48f;
        float farDetailThreshold = _currentSimulationSpeed >= 8f ? 28f : 22f;
        bool preserveDetailedFigure = _selectedAgentId == agent.AgentId ||
                                      (_followAgent && _selectedAgentId == agent.AgentId);
        float farLodThreshold = _adaptiveFastHumanoids ? 34f : 18f;
        bool useFarLod = !preserveDetailedFigure && projectedHeight > 0f && projectedHeight < farLodThreshold;

        // Distant agents stay volumetric: use the existing six-part 3D figure at every
        // overview scale instead of switching to flat screen-space quads and lines.
        if (useFarLod || projectedHeight < fullDetailThreshold)
        {
            DrawReducedHumanoid(
                draw,
                agent,
                position,
                angle,
                scale,
                torso,
                limb,
                head,
                speed,
                speedRatio,
                phase,
                useFarLod || projectedHeight < farDetailThreshold);
            return;
        }

        if (agent.State == AgentState.WaitingToReact || speed < StandingMotionThresholdMetersPerStep)
        {
            moveBlend = 0f;
            runBlend = 0f;
        }

        // Option-A model: intentionally simple checkpoint-style low-poly human.
        // The body is a clean rectangular torso + block head + long slim straight limbs.
        // Keeping the limb count low also makes this cheaper than the previous elbow/knee model.
        float stride = Lerp(0.10f, 0.27f, runBlend) * scale * moveBlend;
        float stepLift = Lerp(0.018f, 0.085f, runBlend) * scale * moveBlend;
        float bob = (0.004f + 0.013f * runBlend) * scale * moveBlend *
                    (0.5f + 0.5f * MathF.Cos(phase * 2f));
        float forwardLean = Lerp(0.0f, 0.055f, runBlend) * scale * moveBlend;
        float armSwingRadians = Lerp(0.34f, 0.70f, runBlend) * moveBlend;
        Vector3 lateralAxis = new(-MathF.Sin(angle), MathF.Cos(angle), 0f);

        Vector2 pelvisCenter = TransformLocal(position, forwardLean * 0.16f, 0f, angle);
        DrawOrientedPrism(
            draw,
            pelvisCenter,
            -0.10f * scale,
            0.10f * scale,
            -0.13f * scale,
            0.13f * scale,
            0.76f * scale + bob,
            0.86f * scale + bob,
            angle,
            pelvis);

        Vector2 torsoCenter = TransformLocal(position, forwardLean * 0.58f, 0f, angle);
        DrawOrientedPrism(
            draw,
            torsoCenter,
            -0.13f * scale,
            0.13f * scale,
            -0.19f * scale,
            0.19f * scale,
            0.84f * scale + bob,
            1.38f * scale + bob,
            angle,
            torso);

        Vector2 neckCenter = TransformLocal(position, forwardLean * 0.82f, 0f, angle);
        DrawOrientedPrism(
            draw,
            neckCenter,
            -0.058f * scale,
            0.058f * scale,
            -0.055f * scale,
            0.055f * scale,
            1.38f * scale + bob,
            1.47f * scale + bob,
            angle,
            Shade(torso, 0.98f));

        Vector2 headCenter = TransformLocal(position, forwardLean, 0f, angle);
        DrawOrientedPrism(
            draw,
            headCenter,
            -0.105f * scale,
            0.105f * scale,
            -0.105f * scale,
            0.105f * scale,
            1.47f * scale + bob,
            1.70f * scale + bob,
            angle,
            head);

        DrawAnimatedLeg(draw, position, angle, lateralAxis, -1f, phase, stride, stepLift, scale, bob, runBlend, limb);
        DrawAnimatedLeg(draw, position, angle, lateralAxis, 1f, phase + MathF.PI, stride, stepLift, scale, bob, runBlend, limb);
        DrawAnimatedArm(draw, position, angle, lateralAxis, -1f, phase + MathF.PI, armSwingRadians, scale, bob, forwardLean, runBlend, limb);
        DrawAnimatedArm(draw, position, angle, lateralAxis, 1f, phase, armSwingRadians, scale, bob, forwardLean, runBlend, limb);
    }

    private float EstimateHumanoidScreenHeight(Vector2 position)
    {
        if (!_camera.TryProject(new Vector3(position, 0f), out Vector2 foot) ||
            !_camera.TryProject(new Vector3(position, HumanoidHeight), out Vector2 head))
        {
            return 0f;
        }

        return Vector2.Distance(foot, head);
    }

    private void DrawReducedHumanoid(
        ImDrawListPtr draw,
        Agent agent,
        Vector2 position,
        float angle,
        float scale,
        Vector4 torso,
        Vector4 limb,
        Vector4 head,
        float speed,
        float speedRatio,
        float phase,
        bool farLod)
    {
        float moveBlend = agent.State == AgentState.WaitingToReact || speed < StandingMotionThresholdMetersPerStep
            ? 0f
            : SmoothStep(0.08f, 0.70f, speedRatio);
        float runBlend = SmoothStep(1.25f, 2.20f, speedRatio);
        float wave = MathF.Sin(phase) * moveBlend;
        float bob = (0.003f + 0.010f * runBlend) * scale * moveBlend *
                    (0.5f + 0.5f * MathF.Cos(phase * 2f));
        float lean = Lerp(0f, 0.048f, runBlend) * scale * moveBlend;
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        Vector3 lateralAxis = new(-sin, cos, 0f);

        Vector2 torsoCenter = TransformLocal(position, lean * 0.55f, 0f, cos, sin);
        DrawOrientedPrism(
            draw,
            torsoCenter,
            -(farLod ? 0.11f : 0.13f) * scale,
            (farLod ? 0.11f : 0.13f) * scale,
            -(farLod ? 0.17f : 0.19f) * scale,
            (farLod ? 0.17f : 0.19f) * scale,
            0.84f * scale + bob,
            1.38f * scale + bob,
            angle,
            torso);

        Vector2 headCenter = TransformLocal(position, lean * 0.95f, 0f, cos, sin);
        float headHalf = (farLod ? 0.095f : 0.105f) * scale;
        DrawOrientedPrism(
            draw, headCenter, -headHalf, headHalf, -headHalf, headHalf,
            1.47f * scale + bob, 1.69f * scale + bob, angle, head);

        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            float legWave = i == 0 ? wave : -wave;
            float sideOffset = side * 0.082f * scale;
            Vector3 hip = new(TransformLocal(position, 0f, sideOffset, cos, sin), 0.80f * scale + bob);
            float ankleForward = legWave * Lerp(0.09f, 0.24f, runBlend) * scale;
            float ankleLift = Math.Max(0f, legWave) * Lerp(0.018f, 0.070f, runBlend) * scale;
            Vector3 ankle = new(
                TransformLocal(position, ankleForward, sideOffset, cos, sin),
                0.085f * scale + ankleLift);
            DrawSegmentPrism(
                draw,
                hip,
                ankle,
                lateralAxis,
                (farLod ? 0.034f : 0.043f) * scale,
                (farLod ? 0.030f : 0.036f) * scale,
                limb);

            float armWave = -legWave;
            Vector3 shoulder = new(
                TransformLocal(position, lean * 0.60f, side * 0.215f * scale, cos, sin),
                1.30f * scale + bob);
            Vector3 hand = new(
                TransformLocal(
                    position,
                    lean * 0.62f + armWave * Lerp(0.07f, 0.17f, runBlend) * scale,
                    side * 0.225f * scale,
                    cos,
                    sin),
                (0.82f + 0.045f * runBlend * MathF.Abs(armWave)) * scale + bob);
            DrawSegmentPrism(
                draw,
                shoulder,
                hand,
                lateralAxis,
                (farLod ? 0.026f : 0.033f) * scale,
                (farLod ? 0.023f : 0.029f) * scale,
                limb);
        }
    }

    private void DrawAnimatedLeg(
        ImDrawListPtr draw,
        Vector2 position,
        float angle,
        Vector3 lateralAxis,
        float sideSign,
        float legPhase,
        float stride,
        float stepLift,
        float scale,
        float bob,
        float runBlend,
        Vector4 color)
    {
        float wave = MathF.Sin(legPhase);
        float swingLift = Math.Max(0f, wave) * stepLift;
        float hipSide = sideSign * 0.082f * scale;
        float ankleSide = sideSign * (0.084f + 0.008f * runBlend) * scale;
        float ankleForward = wave * stride;
        float ankleZ = 0.085f * scale + swingLift;

        Vector3 hip = LocalPoint(position, angle, 0f, hipSide, 0.80f * scale + bob);
        Vector3 ankle = LocalPoint(position, angle, ankleForward, ankleSide, ankleZ);

        // Option-A style deliberately keeps each leg as one long straight slim block.
        DrawSegmentPrism(
            draw,
            hip,
            ankle,
            lateralAxis,
            0.045f * scale,
            0.037f * scale,
            color);

        Vector2 footCenter = TransformLocal(
            position,
            ankleForward + 0.050f * scale,
            ankleSide,
            angle);
        float footBase = Math.Max(0.012f * scale, ankleZ - 0.040f * scale);
        DrawOrientedPrism(
            draw,
            footCenter,
            -0.050f * scale,
            0.125f * scale,
            -0.052f * scale,
            0.052f * scale,
            footBase,
            footBase + 0.070f * scale,
            angle,
            Shade(color, 0.86f));
    }

    private void DrawAnimatedArm(
        ImDrawListPtr draw,
        Vector2 position,
        float angle,
        Vector3 lateralAxis,
        float sideSign,
        float armPhase,
        float swingRadians,
        float scale,
        float bob,
        float forwardLean,
        float runBlend,
        Vector4 color)
    {
        float wave = MathF.Sin(armPhase);
        float swingStrength = Math.Clamp(swingRadians / 0.70f, 0f, 1f);
        float handForward = wave * Lerp(0.10f, 0.22f, runBlend) * scale * swingStrength;
        float shoulderSide = sideSign * 0.222f * scale;
        float handSide = sideSign * 0.226f * scale;

        Vector3 shoulder = LocalPoint(
            position,
            angle,
            forwardLean * 0.58f,
            shoulderSide,
            1.31f * scale + bob);
        Vector3 hand = LocalPoint(
            position,
            angle,
            forwardLean * 0.62f + handForward,
            handSide,
            (0.82f + 0.045f * runBlend * MathF.Abs(wave)) * scale + bob);

        // Reference-style straight arm: a single narrow block swinging from the shoulder.
        DrawSegmentPrism(
            draw,
            shoulder,
            hand,
            lateralAxis,
            0.033f * scale,
            0.029f * scale,
            color);
    }

    private void DrawAssistingHumanoid(
        ImDrawListPtr draw,
        Agent agent,
        Vector2 position,
        float angle,
        float scale,
        Vector4 torso,
        Vector4 limb,
        Vector4 head)
    {
        // Same rectangular torso/block-head style as the upright model, rearranged into
        // a cheap kneeling pose during the bounded 2-second attendance phase.
        float assistPhase = (_currentFrameIndex * 0.18f + agent.AgentId * 0.73f) % MathF.Tau;
        float leanPulse = 0.022f * scale * (0.5f + 0.5f * MathF.Sin(assistPhase));
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        Vector3 lateralAxis = new(-sin, cos, 0f);

        Vector2 torsoCenter = TransformLocal(position, 0.10f * scale + leanPulse, 0f, cos, sin);
        DrawOrientedPrism(
            draw,
            torsoCenter,
            -0.13f * scale,
            0.13f * scale,
            -0.19f * scale,
            0.19f * scale,
            0.45f * scale,
            0.96f * scale,
            angle,
            torso);

        Vector2 neckCenter = TransformLocal(position, 0.14f * scale + leanPulse, 0f, cos, sin);
        DrawOrientedPrism(
            draw, neckCenter, -0.052f * scale, 0.052f * scale, -0.050f * scale, 0.050f * scale,
            0.94f * scale, 1.01f * scale, angle, Shade(torso, 0.98f));

        Vector2 headCenter = TransformLocal(position, 0.17f * scale + leanPulse, 0f, cos, sin);
        DrawOrientedPrism(
            draw,
            headCenter,
            -0.105f * scale,
            0.105f * scale,
            -0.105f * scale,
            0.105f * scale,
            0.99f * scale,
            1.22f * scale,
            angle,
            head);

        Vector3 leftHip = LocalPoint(position, angle, -0.02f * scale, -0.082f * scale, 0.48f * scale);
        Vector3 leftKnee = LocalPoint(position, angle, 0.14f * scale, -0.10f * scale, 0.18f * scale);
        Vector3 leftFoot = LocalPoint(position, angle, 0.34f * scale, -0.10f * scale, 0.065f * scale);
        DrawSegmentPrism(draw, leftHip, leftKnee, lateralAxis, 0.045f * scale, 0.037f * scale, limb);
        DrawSegmentPrism(draw, leftKnee, leftFoot, lateralAxis, 0.041f * scale, 0.034f * scale, Shade(limb, 0.94f));

        Vector3 rightHip = LocalPoint(position, angle, -0.02f * scale, 0.082f * scale, 0.48f * scale);
        Vector3 rightKnee = LocalPoint(position, angle, -0.20f * scale, 0.10f * scale, 0.13f * scale);
        Vector3 rightFoot = LocalPoint(position, angle, -0.43f * scale, 0.10f * scale, 0.060f * scale);
        DrawSegmentPrism(draw, rightHip, rightKnee, lateralAxis, 0.045f * scale, 0.037f * scale, limb);
        DrawSegmentPrism(draw, rightKnee, rightFoot, lateralAxis, 0.041f * scale, 0.034f * scale, Shade(limb, 0.94f));

        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            Vector3 shoulder = LocalPoint(position, angle, 0.11f * scale + leanPulse, side * 0.22f * scale, 0.88f * scale);
            Vector3 hand = LocalPoint(position, angle, 0.41f * scale + leanPulse, side * 0.14f * scale, 0.48f * scale);
            DrawSegmentPrism(draw, shoulder, hand, lateralAxis, 0.033f * scale, 0.029f * scale, limb);
        }
    }

    private void DrawCrawlingHumanoid(
        ImDrawListPtr draw,
        Agent agent,
        Vector2 position,
        float angle,
        float scale,
        Vector4 torso,
        Vector4 limb,
        Vector4 head)
    {
        float speed = VectorMath.IsFinite(agent.Velocity) ? agent.Velocity.Length() : 0f;
        float phase = _gaitPhases.TryGetValue(agent.AgentId, out float gaitPhase) ? gaitPhase : 0f;
        float motion = SmoothStep(
            CrawlMotionStartMetersPerStep,
            Math.Max(CrawlMotionFullMetersPerStep, agent.BaseSpeed * 0.35f),
            speed);
        float crawlSwing = MathF.Sin(phase) * 0.12f * scale * motion;
        Vector3 lateralAxis = new(-MathF.Sin(angle), MathF.Cos(angle), 0f);

        float z0 = 5.5f * LegacyRenderUnitMeters;
        Vector3 hip = LocalPoint(position, angle, -0.12f * scale, 0f, z0 + 0.10f * scale);
        Vector3 shoulder = LocalPoint(position, angle, 0.27f * scale, 0f, z0 + 0.16f * scale);

        // Horizontal block torso: same chunky rectangular body language as standing agents.
        DrawSegmentPrism(draw, hip, shoulder, lateralAxis, 0.18f * scale, 0.12f * scale, torso);

        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            float limbWave = i == 0 ? crawlSwing : -crawlSwing;
            Vector3 shoulderJoint = LocalPoint(position, angle, 0.22f * scale, side * 0.19f * scale, z0 + 0.14f * scale);
            Vector3 elbow = LocalPoint(position, angle, 0.35f * scale + limbWave * 0.45f, side * 0.21f * scale, z0 + 0.09f * scale);
            Vector3 hand = LocalPoint(position, angle, 0.49f * scale + limbWave, side * 0.22f * scale, z0 + 0.03f * scale);
            DrawSegmentPrism(draw, shoulderJoint, elbow, lateralAxis, 0.033f * scale, 0.029f * scale, limb);
            DrawSegmentPrism(draw, elbow, hand, lateralAxis, 0.030f * scale, 0.026f * scale, Shade(limb, 0.96f));

            Vector3 hipJoint = LocalPoint(position, angle, -0.12f * scale, side * 0.09f * scale, z0 + 0.09f * scale);
            Vector3 knee = LocalPoint(position, angle, -0.37f * scale - limbWave, side * 0.15f * scale, z0 + 0.07f * scale);
            Vector3 foot = LocalPoint(position, angle, -0.56f * scale - limbWave * 0.45f, side * 0.12f * scale, z0 + 0.025f * scale);
            DrawSegmentPrism(draw, hipJoint, knee, lateralAxis, 0.043f * scale, 0.035f * scale, limb);
            DrawSegmentPrism(draw, knee, foot, lateralAxis, 0.039f * scale, 0.032f * scale, Shade(limb, 0.92f));
        }

        Vector2 headCenter = TransformLocal(position, 0.49f * scale, 0f, angle);
        DrawOrientedPrism(
            draw,
            headCenter,
            -0.105f * scale,
            0.105f * scale,
            -0.105f * scale,
            0.105f * scale,
            z0 + 0.07f * scale,
            z0 + 0.30f * scale,
            angle,
            head);
    }

    private void DrawProneHumanoid(
        ImDrawListPtr draw,
        Vector2 position,
        float angle,
        float scale,
        Vector4 torso,
        Vector4 limb,
        Vector4 head,
        float baseHeight)
    {
        float z0 = baseHeight;

        // Lying version of the same blocky body: long separated legs, rectangular torso,
        // slim arms and a square head. No extra mesh/skeleton is required.
        DrawOrientedPrism(draw, position, -0.79f * scale, -0.17f * scale,
            -0.105f * scale, -0.025f * scale, z0, z0 + 0.10f * scale, angle, limb);
        DrawOrientedPrism(draw, position, -0.79f * scale, -0.17f * scale,
            0.025f * scale, 0.105f * scale, z0, z0 + 0.10f * scale, angle, limb);
        DrawOrientedPrism(draw, position, -0.23f * scale, 0.41f * scale,
            -0.19f * scale, 0.19f * scale, z0, z0 + 0.20f * scale, angle, torso);
        DrawOrientedPrism(draw, position, -0.10f * scale, 0.35f * scale,
            -0.255f * scale, -0.19f * scale, z0, z0 + 0.075f * scale, angle, limb);
        DrawOrientedPrism(draw, position, -0.10f * scale, 0.35f * scale,
            0.19f * scale, 0.255f * scale, z0, z0 + 0.075f * scale, angle, limb);
        DrawOrientedPrism(draw, position, 0.44f * scale, 0.67f * scale,
            -0.105f * scale, 0.105f * scale, z0, z0 + 0.23f * scale, angle, head);
    }

    private void DrawDebris(ImDrawListPtr draw, Debris debris)
    {
        float energy = Math.Clamp(debris.Energy, 0f, 1f);
        float halfWidth = Math.Max(0.036f, debris.Radius * (0.18f + 0.12f * energy));
        float halfLength = Math.Max(0.073f, debris.Radius * (0.50f + 0.35f * energy));
        Vector4 color = Vector4.Lerp(
            new Vector4(0.43f, 0.29f, 0.18f, 0.88f),
            new Vector4(1f, 0.58f, 0.16f, 1f),
            energy);
        float angle = debris.Velocity.LengthSquared() > 1e-6f
            ? MathF.Atan2(debris.Velocity.Y, debris.Velocity.X)
            : 0f;
        DrawOrientedPrism(
            draw,
            debris.Position,
            -halfLength,
            halfLength,
            -halfWidth,
            halfWidth,
            0.02f,
            0.061f + halfWidth * 2f,
            angle,
            color);
    }

    private void DrawAgentOverlays(
        ImDrawListPtr draw,
        Simulation simulation,
        int? selectedAgentId,
        RenderDebugOptions debug)
    {
        foreach (Agent agent in simulation.Agents)
        {
            if (!agent.IsActive || _hiddenAgentId == agent.AgentId ||
                !_visibleAgentIds.Contains(agent.AgentId))
            {
                continue;
            }

            Vector2 position = _renderPositions.TryGetValue(agent.AgentId, out Vector2 cached)
                ? cached
                : agent.Position;

            if (selectedAgentId == agent.AgentId)
            {
                DrawAgentScreenRing(
                    draw, position, HumanoidWidth * 1.15f, 0.8f * LegacyRenderUnitMeters,
                    new Vector4(1f, 1f, 1f, 0.95f), selected: true);
            }

            // A short-lived visual alarm cue. WaitingToReact is the existing bounded
            // startle/freeze state used by direct shock, social panic and running-crowd
            // contagion. As soon as the state changes to Evacuating/Injured (or any
            // casualty state), this indicator disappears automatically.
            if (agent.State == AgentState.WaitingToReact)
            {
                DrawAlarmIndicator(draw, position);
            }

            if (agent.Intent == BehaviorIntent.AssistCasualty && agent.AssistTargetAgentId is int assistTargetId)
            {
                DrawAssistanceIndicator(draw, agent, position, assistTargetId);
            }

            float panic = Math.Clamp(agent.PanicLevel, 0f, 1f);
            bool casualty = agent.State is AgentState.Fallen or AgentState.TempIncap or
                AgentState.Incapacitated or AgentState.Dead;
            if (panic >= 0.20f && !casualty)
            {
                DrawAgentScreenRing(
                    draw,
                    position,
                    HumanoidWidth * 0.65f + panic * PanicRingMaxExpansionMeters,
                    0.65f * LegacyRenderUnitMeters,
                    new Vector4(1f, 0.35f * (1f - panic), 0.02f, 0.72f),
                    selected: false);
            }

            if (debug.ShowTrails)
            {
                DrawTrail(draw, agent.AgentId);
            }

            if (debug.ShowDesiredDirection)
            {
                DrawWorldVector(draw, position, agent.LastDesiredDirection, DesiredVectorLengthMeters,
                    new Vector4(0.25f, 0.95f, 0.35f, 0.95f));
            }

            if (debug.ShowAvoidanceDirection)
            {
                DrawWorldVector(draw, position, agent.LastAvoidanceDirection, AvoidanceVectorLengthMeters,
                    new Vector4(1f, 0.72f, 0.12f, 0.95f));
            }

            if (debug.ShowHerdDirection)
            {
                DrawWorldVector(draw, position, agent.LastHerdDirection, HerdVectorLengthMeters,
                    new Vector4(0.12f, 0.88f, 0.95f, 0.95f));
            }

            if (debug.ShowSfmForce)
            {
                Vector2 forceDirection = VectorMath.SafeNormalize(agent.LastSfmForce);
                float magnitude = Math.Min(
                    SfmVectorMaxLengthMeters,
                    agent.LastSfmForce.Length() / 160f * LegacyRenderUnitMeters);
                DrawWorldVector(draw, position, forceDirection, magnitude,
                    new Vector4(0.95f, 0.22f, 0.95f, 0.95f));
            }

            if (debug.ShowBlastDirection && simulation.Environment.Explosion is Explosion explosion && !simulation.BlastOver())
            {
                Vector2 direction = VectorMath.SafeNormalize(agent.Position - explosion.Position);
                DrawWorldVector(draw, position, direction, BlastVectorLengthMeters,
                    new Vector4(1f, 0.35f, 0.08f, 0.92f));
            }

            if (debug.ShowVisionCone)
            {
                DrawVisionCone(draw, simulation, agent, position);
            }
        }
    }

    private void DrawAssistanceIndicator(
        ImDrawListPtr draw,
        Agent helper,
        Vector2 helperPosition,
        int targetAgentId)
    {
        if (!_agentById.TryGetValue(targetAgentId, out Agent? target) || !target.IsActive)
        {
            return;
        }

        // One subtle helper-to-casualty link makes the assignment readable without adding
        // route/debug clutter. This is drawn during both approach and actual attendance.
        DrawWorldLine(
            draw,
            new Vector3(helperPosition, 0.32f),
            new Vector3(target.Position, 0.32f),
            Color(0.94f, 0.98f, 1f, 0.34f),
            1.15f);

        bool attending = helper.AssistPauseUntilFrame >= 0 &&
                         _currentFrameIndex < helper.AssistPauseUntilFrame;
        float markerHeight = attending ? 1.38f : HumanoidHeight + 0.34f;
        if (!_camera.TryProject(
                new Vector3(helperPosition.X, helperPosition.Y, markerHeight),
                out Vector2 marker))
        {
            return;
        }

        uint white = Color(0.98f, 1f, 1f, 0.98f);
        uint shadow = Color(0.02f, 0.03f, 0.04f, 0.88f);
        Vector2 shadowOffset = new(1f, 1f);

        // Reserve the medical cross for the bounded attendance phase. During approach,
        // the text alone says GOING TO HELP so a long blocked approach cannot be mistaken
        // for a 30-second assistance action.
        if (attending)
        {
            const float crossHalf = 4.5f;
            const float crossThickness = 1.8f;
            draw.AddRectFilled(
                marker + new Vector2(-crossThickness, -crossHalf) + shadowOffset,
                marker + new Vector2(crossThickness, crossHalf) + shadowOffset,
                shadow,
                0.8f);
            draw.AddRectFilled(
                marker + new Vector2(-crossHalf, -crossThickness) + shadowOffset,
                marker + new Vector2(crossHalf, crossThickness) + shadowOffset,
                shadow,
                0.8f);
            draw.AddRectFilled(
                marker + new Vector2(-crossThickness, -crossHalf),
                marker + new Vector2(crossThickness, crossHalf),
                white,
                0.8f);
            draw.AddRectFilled(
                marker + new Vector2(-crossHalf, -crossThickness),
                marker + new Vector2(crossHalf, crossThickness),
                white,
                0.8f);
        }

        string label = attending ? "HELPING" : "GOING TO HELP";
        Vector2 textSize = ImGui.CalcTextSize(label);
        Vector2 textPosition = marker + new Vector2(-textSize.X * 0.5f, attending ? 7f : -4f);
        draw.AddText(textPosition + shadowOffset, shadow, label);
        draw.AddText(textPosition, white, label);
    }

    private void DrawAlarmIndicator(ImDrawListPtr draw, Vector2 position)
    {
        // Project a point just above the standing humanoid's head. This is a render-only
        // billboard-style marker and never feeds back into panic, reaction timing or SFM.
        Vector3 markerWorld = new(position.X, position.Y, HumanoidHeight + 0.40f);
        if (!_camera.TryProject(markerWorld, out Vector2 markerScreen))
        {
            return;
        }

        // Draw a custom exclamation mark instead of relying on the font glyph. The stem
        // is about twice the previous visual stroke width while keeping the same overall
        // height and head clearance. A small dark offset keeps it readable on light faces.
        const float stemHalfWidth = 2.0f;
        const float stemTopOffset = -8.0f;
        const float stemBottomOffset = -1.0f;
        const float dotOffsetY = 3.0f;
        const float dotRadius = 2.2f;
        Vector2 shadow = new(1.2f, 1.2f);
        uint shadowColor = Color(0.02f, 0.02f, 0.02f, 0.95f);
        uint alarmColor = Color(1f, 0.05f, 0.04f, 1f);

        Vector2 stemMin = markerScreen + new Vector2(-stemHalfWidth, stemTopOffset);
        Vector2 stemMax = markerScreen + new Vector2(stemHalfWidth, stemBottomOffset);
        Vector2 dotCenter = markerScreen + new Vector2(0f, dotOffsetY);

        draw.AddRectFilled(stemMin + shadow, stemMax + shadow, shadowColor, 1.0f);
        draw.AddCircleFilled(dotCenter + shadow, dotRadius, shadowColor, 12);
        draw.AddRectFilled(stemMin, stemMax, alarmColor, 1.0f);
        draw.AddCircleFilled(dotCenter, dotRadius, alarmColor, 12);
    }

    private void DrawTrail(ImDrawListPtr draw, int agentId)
    {
        if (!_trails.TryGetValue(agentId, out Queue<Vector2>? trail) || trail.Count < 2)
        {
            return;
        }

        int i = 0;
        Vector2 previous = default;
        foreach (Vector2 point in trail)
        {
            if (i > 0)
            {
                float alpha = 0.12f + 0.48f * i / trail.Count;
                DrawWorldLine(draw, new Vector3(previous, 0.35f), new Vector3(point, 0.35f),
                    Color(0.45f, 0.78f, 1f, alpha), 1.2f);
            }
            previous = point;
            i++;
        }
    }

    private void DrawVisionCone(ImDrawListPtr draw, Simulation simulation, Agent agent, Vector2 position)
    {
        Vector2 heading = GetHeading(agent);
        if (heading.LengthSquared() < 1e-6f)
        {
            return;
        }

        float range = simulation.Config.Agent.VisionRange;
        float halfAngle = simulation.Config.Agent.VisionHalfAngleDeg * MathF.PI / 180f;
        float baseAngle = MathF.Atan2(heading.Y, heading.X);
        Vector2 left = position + new Vector2(MathF.Cos(baseAngle - halfAngle), MathF.Sin(baseAngle - halfAngle)) * range;
        Vector2 right = position + new Vector2(MathF.Cos(baseAngle + halfAngle), MathF.Sin(baseAngle + halfAngle)) * range;
        uint color = Color(0.65f, 0.82f, 1f, 0.34f);
        DrawWorldLine(draw, new Vector3(position, 0.3f), new Vector3(left, 0.3f), color, 1f);
        DrawWorldLine(draw, new Vector3(position, 0.3f), new Vector3(right, 0.3f), color, 1f);

        const int segments = 18;
        Vector2 previous = left;
        for (int i = 1; i <= segments; i++)
        {
            float angle = baseAngle - halfAngle + 2f * halfAngle * i / segments;
            Vector2 next = position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * range;
            DrawWorldLine(draw, new Vector3(previous, 0.3f), new Vector3(next, 0.3f), color, 1f);
            previous = next;
        }
    }

    private void DrawWorldVector(ImDrawListPtr draw, Vector2 origin, Vector2 direction, float length, Vector4 color)
    {
        Vector2 normalized = VectorMath.SafeNormalize(direction);
        if (normalized.LengthSquared() < 1e-6f || length <= 0.1f)
        {
            return;
        }

        Vector2 end = origin + normalized * length;
        uint packed = Color(color);
        if (!_camera.TryProjectSegment(
                new Vector3(origin, 1.78f),
                new Vector3(end, 1.78f),
                out Vector2 a,
                out Vector2 b))
        {
            return;
        }

        draw.AddLine(a, b, packed, 1.5f);

        Vector2 screenDelta = b - a;
        if (screenDelta.LengthSquared() <= 1e-4f)
        {
            return;
        }

        Vector2 screenDir = Vector2.Normalize(screenDelta);
        Vector2 side = new(-screenDir.Y, screenDir.X);
        draw.AddLine(b, b - screenDir * 6f + side * 3f, packed, 1.5f);
        draw.AddLine(b, b - screenDir * 6f - side * 3f, packed, 1.5f);
    }

    private void DrawBuilderPreview(
        ImDrawListPtr draw,
        Simulation simulation,
        BuilderTool builderTool,
        int blockageCells,
        int exitPersonsWide,
        int agentPlacementCount,
        float furnitureRotation,
        bool snapBarricadeEnds)
    {
        if (!TryMouseToWorld(out Vector2 world))
        {
            return;
        }
        if (builderTool is BuilderTool.RoundTableSet or BuilderTool.SingleChair or BuilderTool.RectangularTable or BuilderTool.Stage or BuilderTool.Barricade)
        {
            FurnitureKind kind = builderTool switch
            {
                BuilderTool.RoundTableSet => FurnitureKind.RoundTableSet,
                BuilderTool.SingleChair => FurnitureKind.SingleChair,
                BuilderTool.RectangularTable => FurnitureKind.RectangularTable,
                BuilderTool.Stage => FurnitureKind.Stage,
                _ => FurnitureKind.Barricade,
            };
            if (kind == FurnitureKind.Barricade && snapBarricadeEnds)
                world = simulation.Environment.SnapBarricadePlacement(world, furnitureRotation);
            FurnitureSet preview = new(kind, world, furnitureRotation);
            bool fits = simulation.Environment.CanPlaceFurniture(kind, world, furnitureRotation);
            Vector4 color = fits ? new(0.2f, 0.9f, 0.5f, 0.65f) : new(1f, 0.2f, 0.2f, 0.65f);
            if (preview.StaticObstacle is Wall footprint)
            {
                DrawFixedFurniture(draw, preview, footprint, color);
                DrawFurnitureFootprint(draw, footprint, color);
                return;
            }
            DrawGroundCircle(draw, world, preview.PlacementRadius, 0.04f, color, 40);
            foreach (FurnitureChair chair in preview.Chairs)
                DrawChair(draw, chair, color);
            if (preview.TableObstacle is not null) DrawTable(draw, preview.Table!, color);
            return;
        }

        if (builderTool == BuilderTool.Blockage)
        {
            Wall candidate = simulation.Environment.GetBlockagePlacementBounds(world.X, world.Y, blockageCells);
            bool valid = simulation.Environment.CanPlaceBlockageFromClick(world.X, world.Y, blockageCells);
            Vector4 color = valid
                ? new Vector4(0.15f, 0.90f, 0.55f, 0.38f)
                : new Vector4(1f, 0.20f, 0.18f, 0.38f);
            DrawAxisAlignedPrism(draw, candidate.X1, candidate.X2, candidate.Y1, candidate.Y2, 0f, 0.81f, color);
            return;
        }

        if (builderTool == BuilderTool.CircularBlockage)
        {
            Vector2 snapped = simulation.Environment.SnapPointToGrid(world.X, world.Y);
            const float radius = 1.25f;
            CircleBlockage candidate = new(snapped, radius);
            float margin = simulation.Environment.BoundaryThickness + radius + 0.05f;
            bool inside = snapped.X >= margin && snapped.X <= simulation.Environment.Width - margin &&
                          snapped.Y >= margin && snapped.Y <= simulation.Environment.Height - margin;
            bool valid = inside && !simulation.Environment.CircleOverlapsAny(candidate);
            Vector4 color = valid
                ? new Vector4(0.15f, 0.90f, 0.55f, 0.50f)
                : new Vector4(1f, 0.20f, 0.18f, 0.50f);
            DrawGroundCircle(draw, snapped, radius, 0.04f, color, 40);
            return;
        }

        if (builderTool == BuilderTool.ToxicFumeSource)
        {
            float margin = Math.Max(0.02f, 0.25f * simulation.Config.Agent.Radius);
            bool valid = simulation.ScenarioType == ScenarioType.ToxicFume &&
                         world.X > simulation.Environment.BoundaryThickness + margin &&
                         world.Y > simulation.Environment.BoundaryThickness + margin &&
                         world.X < simulation.Environment.Width - simulation.Environment.BoundaryThickness - margin &&
                         world.Y < simulation.Environment.Height - simulation.Environment.BoundaryThickness - margin &&
                         !simulation.Environment.IsPointBlocked(world, margin);
            Vector4 color = valid
                ? new Vector4(0.18f, 0.92f, 0.48f, 0.72f)
                : new Vector4(1f, 0.24f, 0.18f, 0.72f);
            DrawGroundCircle(
                draw,
                world,
                Math.Max(0.18f, simulation.ToxicFumeSystem.Configuration.InitialRadiusMeters),
                0.045f,
                color,
                36);
            if (_camera.TryProject(new Vector3(world, 0.02f), out Vector2 screen))
            {
                draw.AddText(screen + new Vector2(7f, -8f), Color(color), "FUME SOURCE");
            }

            return;
        }

        if (builderTool == BuilderTool.Agents)
        {
            float radius = Math.Max(
                0.48f,
                MathF.Sqrt(Math.Max(1, agentPlacementCount)) * simulation.Config.Agent.Radius * 1.8f);
            bool inside = world.X >= simulation.Environment.BoundaryThickness &&
                          world.X <= simulation.Environment.Width - simulation.Environment.BoundaryThickness &&
                          world.Y >= simulation.Environment.BoundaryThickness &&
                          world.Y <= simulation.Environment.Height - simulation.Environment.BoundaryThickness;
            Vector4 color = inside
                ? new Vector4(0.18f, 0.92f, 0.92f, 0.72f)
                : new Vector4(1f, 0.24f, 0.18f, 0.72f);
            DrawGroundCircle(draw, world, radius, 0.04f, color, 32);
            if (_camera.TryProject(new Vector3(world, 0.02f), out Vector2 screen))
            {
                draw.AddText(screen + new Vector2(7f, -8f), Color(color), $"x{agentPlacementCount}");
            }

            return;
        }

        string? sideName = simulation.Environment.DetectBoundarySide(world.X, world.Y);
        if (sideName is null)
        {
            return;
        }

        float agentDiameter = 2f * simulation.Config.Agent.Radius;
        float exitWidth = exitPersonsWide == 1
            ? agentDiameter * 1.2f
            : Math.Max(1, exitPersonsWide) * agentDiameter;
        float halfSize = Math.Max(0.12f, exitWidth / 2f);
        Vector2 marker = sideName switch
        {
            "N" => new Vector2(Math.Clamp(world.X, halfSize, simulation.Environment.Width - halfSize),
                simulation.Environment.BoundaryThickness * 0.5f),
            "S" => new Vector2(Math.Clamp(world.X, halfSize, simulation.Environment.Width - halfSize),
                simulation.Environment.Height - simulation.Environment.BoundaryThickness * 0.5f),
            "W" => new Vector2(simulation.Environment.BoundaryThickness * 0.5f,
                Math.Clamp(world.Y, halfSize, simulation.Environment.Height - halfSize)),
            _ => new Vector2(simulation.Environment.Width - simulation.Environment.BoundaryThickness * 0.5f,
                Math.Clamp(world.Y, halfSize, simulation.Environment.Height - halfSize)),
        };

        DrawGroundCircle(draw, marker, halfSize, 0.04f, new Vector4(0.15f, 0.90f, 1f, 0.85f), 28);
    }

    private void DrawPlannedAgentPlacements(ImDrawListPtr draw, Simulation simulation)
    {
        uint pointColor = Color(0.18f, 0.92f, 0.88f, 0.82f);
        uint textColor = Color(0.04f, 0.18f, 0.17f, 1f);
        foreach (AgentSpawnGroup group in simulation.Environment.AgentSpawnGroups)
        {
            foreach (Vector2 position in group.Positions)
            {
                if (_camera.TryProject(new Vector3(position, 0.024f), out Vector2 screen) &&
                    screen.X >= _canvasMin.X && screen.X <= _canvasMax.X &&
                    screen.Y >= _canvasMin.Y && screen.Y <= _canvasMax.Y)
                {
                    draw.AddCircleFilled(screen, 4.4f, AgentOutlineColor, 8);
                    draw.AddCircleFilled(screen, 3.2f, pointColor, 8);
                }
            }

            if (_camera.TryProject(new Vector3(group.Center, 0.04f), out Vector2 centerScreen))
            {
                draw.AddText(centerScreen + new Vector2(6f, -8f), textColor, $"x{group.Count}");
            }
        }
    }

    private void DrawWorldLegend(ImDrawListPtr draw, Simulation simulation, bool builderMode)
    {
        Vector2 topLeft = _canvasMin + new Vector2(14f, 12f);
        uint text = Color(0.92f, 0.94f, 0.97f, 1f);
        string povName = _hiddenAgentId is int povAgentId
            ? $"AGENT {povAgentId}"
            : "SELECTED AGENT";
        string title = builderMode
            ? "Place scene items with the selected tool"
            : _camera.IsPov
                ? $"AGENT POV - {povName}"
                : _followAgent
                    ? "3D CROWD SIMULATION - FOLLOWING SELECTED AGENT"
                    : simulation.Running
                        ? "3D CROWD SIMULATION - RUNNING"
                        : "3D CROWD SIMULATION - PAUSED";

        draw.AddText(topLeft, text, title);
        draw.AddText(
            topLeft + new Vector2(0f, 19f),
            Color(0.65f, 0.70f, 0.77f, 1f),
            _camera.IsPov
                ? "Open the Agent inspector to leave POV."
                : builderMode
                    ? "Scroll to zoom  |  Adjust perspective on the Camera page"
                    : "Click a person to inspect  |  Scroll to zoom");
    }

    private Vector2 GetHeading(Agent agent)
    {
        if (agent.Velocity.LengthSquared() > HeadingMotionThresholdSquared)
        {
            return VectorMath.SafeNormalize(agent.Velocity, Vector2.UnitX);
        }

        if (agent.LastDesiredDirection.LengthSquared() > 1e-5f)
        {
            return VectorMath.SafeNormalize(agent.LastDesiredDirection, Vector2.UnitX);
        }

        if (agent.ExitTarget is not null)
        {
            return VectorMath.SafeNormalize(agent.ExitTarget.Position - agent.Position, Vector2.UnitX);
        }

        return Vector2.UnitX;
    }

    private static Vector4 AgentColor(Agent agent)
    {
        // Body colour encodes state only. Panic is deliberately shown with the red/orange
        // ground ring instead of tinting the humanoid, so an evacuating green agent does not
        // become visually ambiguous as its panic level increases.
        return agent.State switch
        {
            AgentState.Dead => new Vector4(0.18f, 0.18f, 0.18f, 1f),
            AgentState.Incapacitated or AgentState.TempIncap => new Vector4(0.42f, 0.03f, 0.03f, 1f),
            AgentState.Fallen => new Vector4(0.96f, 0.05f, 0.04f, 1f),
            AgentState.Crawling => new Vector4(0.96f, 0.90f, 0.08f, 1f),
            AgentState.Injured => new Vector4(0.08f, 0.30f, 0.95f, 1f),
            AgentState.Evacuating => new Vector4(0.12f, 0.88f, 0.22f, 1f),
            AgentState.WaitingToReact => new Vector4(0.96f, 0.65f, 0.10f, 1f),
            _ => new Vector4(0.90f, 0.90f, 0.86f, 1f),
        };
    }

    private void DrawAgentScreenRing(
        ImDrawListPtr draw,
        Vector2 center,
        float worldRadius,
        float z,
        Vector4 color,
        bool selected)
    {
        if (!_camera.TryProject(new Vector3(center, z), out Vector2 screen) ||
            !_camera.TryProject(new Vector3(center.X + worldRadius, center.Y, z), out Vector2 xEdge) ||
            !_camera.TryProject(new Vector3(center.X, center.Y + worldRadius, z), out Vector2 yEdge))
        {
            return;
        }

        float radius = 0.5f * (Vector2.Distance(screen, xEdge) + Vector2.Distance(screen, yEdge));
        if (!float.IsFinite(radius) || radius < 1f)
        {
            return;
        }

        int segments = selected ? 18 : (_adaptiveFastHumanoids ? 10 : 14);
        draw.AddCircle(screen, radius, Color(color), segments, selected ? 1.8f : 1.4f);
    }

    private void DrawGroundCircle(
        ImDrawListPtr draw,
        Vector2 center,
        float radius,
        float z,
        Vector4 color,
        int segments)
    {
        uint packed = Color(color);
        Vector3 previous = new(center.X + radius, center.Y, z);
        for (int i = 1; i <= segments; i++)
        {
            float angle = MathF.Tau * i / segments;
            Vector3 next = new(
                center.X + radius * MathF.Cos(angle),
                center.Y + radius * MathF.Sin(angle),
                z);
            DrawWorldLine(draw, previous, next, packed, 1.4f);
            previous = next;
        }
    }

    private void DrawShieldedGroundRing(
        ImDrawListPtr draw,
        Simulation simulation,
        Vector2 blastOrigin,
        float innerRadius,
        float outerRadius,
        float z,
        Vector4 color,
        int segments)
    {
        uint packed = Color(color);
        float safeInner = Math.Max(0f, innerRadius);
        float safeOuter = Math.Max(safeInner + 0.02f, outerRadius);

        for (int i = 0; i < segments; i++)
        {
            float angleA = MathF.Tau * i / segments;
            float angleB = MathF.Tau * (i + 1) / segments;
            Vector2 directionA = new(MathF.Cos(angleA), MathF.Sin(angleA));
            Vector2 directionB = new(MathF.Cos(angleB), MathF.Sin(angleB));
            Vector2 innerA = blastOrigin + directionA * safeInner;
            Vector2 outerA = blastOrigin + directionA * safeOuter;
            Vector2 innerB = blastOrigin + directionB * safeInner;
            Vector2 outerB = blastOrigin + directionB * safeOuter;
            Vector2 midpoint = (innerA + outerA + innerB + outerB) * 0.25f;

            if (!BlastZonePointIsVisible(simulation, blastOrigin, innerA) ||
                !BlastZonePointIsVisible(simulation, blastOrigin, outerA) ||
                !BlastZonePointIsVisible(simulation, blastOrigin, innerB) ||
                !BlastZonePointIsVisible(simulation, blastOrigin, outerB) ||
                !BlastZonePointIsVisible(simulation, blastOrigin, midpoint))
            {
                continue;
            }

            DrawFlatQuad(
                draw,
                new Vector3(innerA, z),
                new Vector3(innerB, z),
                new Vector3(outerB, z),
                new Vector3(outerA, z),
                packed);
        }
    }

    private void DrawShieldedGroundCircle(
        ImDrawListPtr draw,
        Simulation simulation,
        Vector2 blastOrigin,
        float radius,
        float z,
        Vector4 color,
        int segments)
    {
        uint packed = Color(color);
        Vector2 previous = blastOrigin + new Vector2(radius, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angle = MathF.Tau * i / segments;
            Vector2 next = blastOrigin + new Vector2(
                radius * MathF.Cos(angle),
                radius * MathF.Sin(angle));
            Vector2 midpoint = (previous + next) * 0.5f;

            if (BlastZonePointIsVisible(simulation, blastOrigin, previous) &&
                BlastZonePointIsVisible(simulation, blastOrigin, midpoint) &&
                BlastZonePointIsVisible(simulation, blastOrigin, next))
            {
                DrawWorldLine(
                    draw,
                    new Vector3(previous, z),
                    new Vector3(next, z),
                    packed,
                    1.4f);
            }

            previous = next;
        }
    }

    private static bool BlastZonePointIsVisible(
        Simulation simulation,
        Vector2 blastOrigin,
        Vector2 point)
    {
        float t = simulation.Environment.BoundaryThickness;
        if (point.X < t || point.X > simulation.Environment.Width - t ||
            point.Y < t || point.Y > simulation.Environment.Height - t)
        {
            return false;
        }

        return simulation.HasLineOfSight(blastOrigin, point);
    }

    private void DrawAxisAlignedPrism(
        ImDrawListPtr draw,
        float minX,
        float maxX,
        float minY,
        float maxY,
        float minZ,
        float maxZ,
        Vector4 color)
        => DrawOrientedPrism(
            draw,
            new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f),
            minX - (minX + maxX) * 0.5f,
            maxX - (minX + maxX) * 0.5f,
            minY - (minY + maxY) * 0.5f,
            maxY - (minY + maxY) * 0.5f,
            minZ,
            maxZ,
            0f,
            color);

    private void DrawOrientedPrism(
        ImDrawListPtr draw,
        Vector2 center,
        float minForward,
        float maxForward,
        float minSide,
        float maxSide,
        float minZ,
        float maxZ,
        float angle,
        Vector4 color)
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        Vector2 b0 = TransformLocal(center, minForward, minSide, cos, sin);
        Vector2 b1 = TransformLocal(center, maxForward, minSide, cos, sin);
        Vector2 b2 = TransformLocal(center, maxForward, maxSide, cos, sin);
        Vector2 b3 = TransformLocal(center, minForward, maxSide, cos, sin);

        Vector3 p000 = new(b0, minZ);
        Vector3 p100 = new(b1, minZ);
        Vector3 p110 = new(b2, minZ);
        Vector3 p010 = new(b3, minZ);
        Vector3 p001 = new(b0, maxZ);
        Vector3 p101 = new(b1, maxZ);
        Vector3 p111 = new(b2, maxZ);
        Vector3 p011 = new(b3, maxZ);

        Span<Vector3> vertices = stackalloc Vector3[8]
        { p000, p100, p110, p010, p001, p101, p111, p011 };
        DrawPrismFaces(draw, vertices, color, oriented: true);
    }

    private void DrawSegmentPrism(
        ImDrawListPtr draw,
        Vector3 start,
        Vector3 end,
        Vector3 preferredSideAxis,
        float halfWidth,
        float halfDepth,
        Vector4 color)
    {
        Vector3 segment = end - start;
        float length = segment.Length();
        if (!float.IsFinite(length) || length < 1e-4f)
        {
            return;
        }

        Vector3 along = segment / length;
        Vector3 side = preferredSideAxis - Vector3.Dot(preferredSideAxis, along) * along;
        if (side.LengthSquared() < 1e-6f)
        {
            side = Vector3.Cross(along, Vector3.UnitZ);
        }
        if (side.LengthSquared() < 1e-6f)
        {
            side = Vector3.UnitY;
        }
        side = Vector3.Normalize(side);

        Vector3 depth = Vector3.Cross(along, side);
        if (depth.LengthSquared() < 1e-6f)
        {
            depth = Vector3.UnitZ;
        }
        else
        {
            depth = Vector3.Normalize(depth);
        }

        // Metric conversion: 0.2 was the original renderer's legacy-unit safety floor.
        // Keeping it as 0.20 metres made every nominally slim arm/leg at least 0.40 m thick.
        // Convert only that safety floor; requested metre-scale limb dimensions remain untouched.
        float minimumHalfThickness = 0.2f * LegacyRenderUnitMeters;
        Vector3 sw = side * Math.Max(minimumHalfThickness, halfWidth);
        Vector3 sd = depth * Math.Max(minimumHalfThickness, halfDepth);

        Vector3 s00 = start - sw - sd;
        Vector3 s10 = start + sw - sd;
        Vector3 s11 = start + sw + sd;
        Vector3 s01 = start - sw + sd;
        Vector3 e00 = end - sw - sd;
        Vector3 e10 = end + sw - sd;
        Vector3 e11 = end + sw + sd;
        Vector3 e01 = end - sw + sd;

        Span<Vector3> vertices = stackalloc Vector3[8]
        { s00, s10, s11, s01, e00, e10, e11, e01 };
        DrawPrismFaces(draw, vertices, color, oriented: false);
    }

    // Both prism types share these vertex indices. Projection is done eight times
    // per prism, rather than again for every vertex of every face (20 or 24 times).
    private static readonly int[] PrismCorners =
    { 0, 1, 5, 4, 1, 2, 6, 5, 2, 3, 7, 6, 3, 0, 4, 7, 0, 3, 2, 1, 4, 5, 6, 7 };
    private static readonly float[] OrientedShades = { 0.74f, 0.86f, 0.80f, 0.92f, 0f, 1.08f };
    private static readonly float[] SegmentShades = { 0.75f, 0.88f, 0.82f, 0.94f, 0.80f, 1.06f };

    private void DrawPrismFaces(ImDrawListPtr draw, ReadOnlySpan<Vector3> vertices,
        Vector4 color, bool oriented)
    {
        Span<Vector2> projected = stackalloc Vector2[8];
        Span<bool> visible = stackalloc bool[8];
        for (int i = 0; i < 8; i++)
        {
            visible[i] = _camera.TryProject(vertices[i], out projected[i]);
        }
        Span<int> order = stackalloc int[6];
        Span<float> depths = stackalloc float[6];
        int sortedCount = oriented ? 4 : 6;
        for (int face = 0; face < sortedCount; face++)
        {
            int k = face * 4;
            // Keep the previous centre-depth expression and stable face ordering.
            Vector3 center = (vertices[PrismCorners[k]] + vertices[PrismCorners[k + 1]] +
                vertices[PrismCorners[k + 2]] + vertices[PrismCorners[k + 3]]) * 0.25f;
            depths[face] = _camera.Depth(center);
            int j = face - 1;
            while (j >= 0 && depths[order[j]] < depths[face])
            {
                order[j + 1] = order[j];
                j--;
            }
            order[j + 1] = face;
        }
        if (oriented)
        {
            order[4] = 5; // upright top remains last, exactly as before
        }
        float[] shades = oriented ? OrientedShades : SegmentShades;
        int count = oriented ? 5 : 6;
        for (int i = 0; i < count; i++)
        {
            int face = order[i];
            int k = face * 4;
            int a = PrismCorners[k], b = PrismCorners[k + 1];
            int c = PrismCorners[k + 2], d = PrismCorners[k + 3];
            if (visible[a] && visible[b] && visible[c] && visible[d])
            {
                draw.AddQuadFilled(projected[a], projected[b], projected[c], projected[d],
                    Color(Shade(color, shades[face])));
            }
        }
        if (_outlineAgentGeometry)
            DrawProjectedBodyOutline(draw, projected, visible);
    }

    private static Vector3 LocalPoint(
        Vector2 center,
        float angle,
        float forward,
        float side,
        float z)
    {
        Vector2 ground = TransformLocal(center, forward, side, angle);
        return new Vector3(ground, z);
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        if (edge1 <= edge0)
        {
            return value >= edge1 ? 1f : 0f;
        }

        float t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float Lerp(float a, float b, float t)
        => a + (b - a) * Math.Clamp(t, 0f, 1f);

    private static float WrapAngle(float angle)
    {
        angle %= MathF.Tau;
        return angle < 0f ? angle + MathF.Tau : angle;
    }

    private static Vector2 TransformLocal(Vector2 center, float forward, float side, float angle)
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        return TransformLocal(center, forward, side, cos, sin);
    }

    private static Vector2 TransformLocal(
        Vector2 center,
        float forward,
        float side,
        float cos,
        float sin)
        => center + new Vector2(
            forward * cos - side * sin,
            forward * sin + side * cos);

    private void DrawFlatQuad(ImDrawListPtr draw, Vector3 a, Vector3 b, Vector3 c, Vector3 d, uint color)
        => DrawProjectedQuad(draw, a, b, c, d, color);

    private void DrawProjectedQuad(ImDrawListPtr draw, Vector3 a, Vector3 b, Vector3 c, Vector3 d, uint color)
    {
        if (!_camera.TryProject(a, out Vector2 pa) ||
            !_camera.TryProject(b, out Vector2 pb) ||
            !_camera.TryProject(c, out Vector2 pc) ||
            !_camera.TryProject(d, out Vector2 pd))
        {
            return;
        }

        draw.AddQuadFilled(pa, pb, pc, pd, color);
    }

    private void DrawWorldLine(
        ImDrawListPtr draw,
        Vector3 a,
        Vector3 b,
        uint color,
        float thickness)
    {
        if (_camera.TryProjectSegment(a, b, out Vector2 screenA, out Vector2 screenB))
        {
            draw.AddLine(screenA, screenB, color, thickness);
        }
    }

    private static Vector4 Shade(Vector4 color, float factor) => new(
        Math.Clamp(color.X * factor, 0f, 1f),
        Math.Clamp(color.Y * factor, 0f, 1f),
        Math.Clamp(color.Z * factor, 0f, 1f),
        color.W);

    private static uint Color(Vector4 color) => ImGui.ColorConvertFloat4ToU32(color);

    private static uint Color(float r, float g, float b, float a)
        => ImGui.ColorConvertFloat4ToU32(new Vector4(r, g, b, a));

    private sealed class CachedBlastBand
    {
        public List<CachedWorldQuad> Quads { get; } = new(128);
        public List<CachedWorldLine> Outline { get; } = new(144);
        public uint FillColor { get; set; }
        public uint OutlineColor { get; set; }

        public void Clear()
        {
            Quads.Clear();
            Outline.Clear();
        }
    }

    private readonly record struct CachedWorldQuad(Vector3 A, Vector3 B, Vector3 C, Vector3 D);
    private readonly record struct CachedWorldLine(Vector3 A, Vector3 B);
    private readonly record struct CachedWallChunk(Wall Wall, bool IsBlockage, bool IsBlueWall);

    private enum RenderEntityKind
    {
        Wall,
        CircleBlockage,
        Agent,
        Debris,
        FixedFurniture,
    }

    private readonly record struct RenderEntity(
        float Depth,
        RenderEntityKind Kind,
        Agent? Agent,
        Wall? Wall,
        Debris? Debris,
        bool IsBlockage,
        bool IsBlueWall,
        CircleBlockage? Circle = null,
        FurnitureSet? Furniture = null);

}
