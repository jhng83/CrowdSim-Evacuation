using System.Numerics;

namespace CrowdSim.OpenGL.Rendering;

/// <summary>
/// Camera used by the projected 3D renderer. The default view is an elevated orthographic
/// isometric camera. A selected agent can also be followed from an agent-centred isometric
/// view or observed from a head-height first-person perspective camera.
/// </summary>
public sealed class Camera3D
{
    private const float PovNearPlane = 0.04f;
    private const float PovFieldOfViewDegrees = 72f;

    private Vector3 _forward = new(-0.70f, 0.50f, -0.50f);
    private Vector3 _right = Vector3.UnitX;
    private Vector3 _up = Vector3.UnitZ;
    private Vector3 _target;
    private Vector3 _eye;
    private Vector2 _canvasCenter;
    private Vector2 _canvasSize = Vector2.One;
    private float _cameraCenterX;
    private float _cameraCenterY;
    private float _scale = 1f;
    private float _povFocalPixels = 1f;

    /// <summary>Camera azimuth around the world Z axis, in degrees.</summary>
    public float AzimuthDegrees { get; private set; } = -35f;

    /// <summary>Camera elevation above the ground plane, in degrees.</summary>
    public float ElevationDegrees { get; private set; } = 34f;

    /// <summary>Orthographic zoom multiplier.</summary>
    public float Zoom { get; private set; } = 1f;

    /// <summary>True while the camera is configured as the selected agent's first-person view.</summary>
    public bool IsPov { get; private set; }

    /// <summary>Set azimuth while keeping a practical three-dimensional viewing range.</summary>
    public void SetAzimuth(float degrees) => AzimuthDegrees = Math.Clamp(degrees, -180f, 180f);

    /// <summary>Set elevation while preventing a near-horizontal or top-down singular view.</summary>
    public void SetElevation(float degrees) => ElevationDegrees = Math.Clamp(degrees, 18f, 70f);

    /// <summary>Set the orthographic zoom multiplier.</summary>
    public void SetZoom(float zoom) => Zoom = Math.Clamp(zoom, 0.45f, 2.5f);

    /// <summary>Adjust zoom from the mouse wheel when the world canvas is hovered.</summary>
    public void AdjustZoom(float wheelDelta)
    {
        if (!IsPov)
        {
            SetZoom(Zoom * MathF.Exp(wheelDelta * 0.10f));
        }
    }

    /// <summary>Restore the validated default 3D presentation camera.</summary>
    public void Reset()
    {
        AzimuthDegrees = -35f;
        ElevationDegrees = 34f;
        Zoom = 1f;
        IsPov = false;
    }

    /// <summary>
    /// Configure the standard elevated 3D view. When <paramref name="focusWorld"/> is supplied,
    /// the selected agent is kept near the centre of the canvas while retaining the same
    /// isometric orientation.
    /// </summary>
    public void Configure(
        Vector2 canvasMin,
        Vector2 canvasMax,
        float worldWidth,
        float worldHeight,
        float maximumHeight,
        Vector2? focusWorld = null)
    {
        IsPov = false;
        _canvasSize = Vector2.Max(canvasMax - canvasMin, new Vector2(1f));
        _canvasCenter = (canvasMin + canvasMax) * 0.5f;

        Vector2 groundTarget = focusWorld ?? new Vector2(worldWidth * 0.5f, worldHeight * 0.5f);
        float targetHeight = focusWorld is null ? maximumHeight * 0.25f : 0f;
        _target = new Vector3(groundTarget.X, groundTarget.Y, targetHeight);

        float azimuth = DegreesToRadians(AzimuthDegrees);
        float elevation = DegreesToRadians(ElevationDegrees);

        // Vector from the target towards the camera.
        Vector3 cameraDirection = Vector3.Normalize(new Vector3(
            MathF.Cos(elevation) * MathF.Cos(azimuth),
            MathF.Cos(elevation) * MathF.Sin(azimuth),
            MathF.Sin(elevation)));

        _forward = -cameraDirection;
        _right = Vector3.Normalize(Vector3.Cross(_forward, Vector3.UnitZ));
        _up = Vector3.Normalize(Vector3.Cross(_right, _forward));

        float marginX = Math.Max(0.80f, worldWidth * 0.035f);
        float marginY = Math.Max(0.80f, worldHeight * 0.035f);
        float minX = -marginX;
        float maxX = worldWidth + marginX;
        float minY = -marginY;
        float maxY = worldHeight + marginY;
        float minZ = 0f;
        float maxZ = maximumHeight;

        float minCameraX = float.PositiveInfinity;
        float maxCameraX = float.NegativeInfinity;
        float minCameraY = float.PositiveInfinity;
        float maxCameraY = float.NegativeInfinity;

        // Evaluate the eight AABB corners without allocating temporary arrays every frame.
        for (int xi = 0; xi < 2; xi++)
        {
            float x = xi == 0 ? minX : maxX;
            for (int yi = 0; yi < 2; yi++)
            {
                float y = yi == 0 ? minY : maxY;
                for (int zi = 0; zi < 2; zi++)
                {
                    float z = zi == 0 ? minZ : maxZ;
                    Vector3 relative = new Vector3(x, y, z) - _target;
                    float cameraX = Vector3.Dot(relative, _right);
                    float cameraY = Vector3.Dot(relative, _up);
                    minCameraX = Math.Min(minCameraX, cameraX);
                    maxCameraX = Math.Max(maxCameraX, cameraX);
                    minCameraY = Math.Min(minCameraY, cameraY);
                    maxCameraY = Math.Max(maxCameraY, cameraY);
                }
            }
        }

        if (focusWorld is null)
        {
            _cameraCenterX = (minCameraX + maxCameraX) * 0.5f;
            _cameraCenterY = (minCameraY + maxCameraY) * 0.5f;
        }
        else
        {
            // Because _target is the followed agent, zero camera-space X/Y centres that agent.
            _cameraCenterX = 0f;
            _cameraCenterY = 0f;
        }

        float projectedWidth = Math.Max(0.001f, maxCameraX - minCameraX);
        float projectedHeight = Math.Max(0.001f, maxCameraY - minCameraY);
        const float paddingFraction = 0.90f;
        float followMultiplier = focusWorld is null ? 1f : 1.35f;
        _scale = paddingFraction * Math.Min(
            _canvasSize.X / projectedWidth,
            _canvasSize.Y / projectedHeight) * Zoom * followMultiplier;
    }

    /// <summary>
    /// Configure a head-height first-person camera that follows one agent's movement heading.
    /// </summary>
    public void ConfigurePov(
        Vector2 canvasMin,
        Vector2 canvasMax,
        Vector2 agentPosition,
        Vector2 heading,
        float eyeHeight)
    {
        IsPov = true;
        _canvasSize = Vector2.Max(canvasMax - canvasMin, new Vector2(1f));
        _canvasCenter = (canvasMin + canvasMax) * 0.5f;

        Vector2 groundDirection = heading.LengthSquared() > 1e-6f
            ? Vector2.Normalize(heading)
            : Vector2.UnitX;

        _eye = new Vector3(agentPosition.X, agentPosition.Y, Math.Max(0.10f, eyeHeight));

        // A slight downward pitch keeps nearby floor geometry visible without turning the
        // camera into a third-person view.
        _forward = Vector3.Normalize(new Vector3(groundDirection.X, groundDirection.Y, -0.055f));
        _right = Vector3.Normalize(Vector3.Cross(_forward, Vector3.UnitZ));
        if (_right.LengthSquared() < 1e-6f)
        {
            _right = Vector3.UnitX;
        }

        _up = Vector3.Normalize(Vector3.Cross(_right, _forward));
        float fovRadians = DegreesToRadians(PovFieldOfViewDegrees);
        _povFocalPixels = (_canvasSize.Y * 0.5f) / MathF.Tan(fovRadians * 0.5f);
    }

    /// <summary>Project one world-space 3D point into ImGui screen coordinates.</summary>
    public Vector2 Project(Vector3 world)
    {
        if (TryProject(world, out Vector2 screen))
        {
            return screen;
        }

        // Keep invalid/behind-camera geometry safely outside the clip rectangle.
        return new Vector2(-100_000f, -100_000f);
    }

    /// <summary>Try to project a world point. POV points behind the near plane are rejected.</summary>
    public bool TryProject(Vector3 world, out Vector2 screen)
    {
        if (!IsPov)
        {
            Vector3 relative = world - _target;
            float orthoCameraX = Vector3.Dot(relative, _right);
            float orthoCameraY = Vector3.Dot(relative, _up);
            screen = new Vector2(
                _canvasCenter.X + (orthoCameraX - _cameraCenterX) * _scale,
                _canvasCenter.Y - (orthoCameraY - _cameraCenterY) * _scale);
            return float.IsFinite(screen.X) && float.IsFinite(screen.Y);
        }

        Vector3 povRelative = world - _eye;
        float depth = Vector3.Dot(povRelative, _forward);
        if (!float.IsFinite(depth) || depth <= PovNearPlane)
        {
            screen = default;
            return false;
        }

        float cameraX = Vector3.Dot(povRelative, _right);
        float cameraY = Vector3.Dot(povRelative, _up);
        screen = new Vector2(
            _canvasCenter.X + cameraX / depth * _povFocalPixels,
            _canvasCenter.Y - cameraY / depth * _povFocalPixels);
        return float.IsFinite(screen.X) && float.IsFinite(screen.Y);
    }

    /// <summary>
    /// Project a line segment, clipping it against the POV near plane when necessary.
    /// </summary>
    public bool TryProjectSegment(Vector3 a, Vector3 b, out Vector2 screenA, out Vector2 screenB)
    {
        if (!IsPov)
        {
            bool projectedA = TryProject(a, out screenA);
            bool projectedB = TryProject(b, out screenB);
            return projectedA && projectedB;
        }

        float depthA = Vector3.Dot(a - _eye, _forward);
        float depthB = Vector3.Dot(b - _eye, _forward);
        if (depthA <= PovNearPlane && depthB <= PovNearPlane)
        {
            screenA = default;
            screenB = default;
            return false;
        }

        if (depthA <= PovNearPlane)
        {
            float t = (PovNearPlane + 0.01f - depthA) / Math.Max(1e-6f, depthB - depthA);
            a = Vector3.Lerp(a, b, Math.Clamp(t, 0f, 1f));
        }
        else if (depthB <= PovNearPlane)
        {
            float t = (PovNearPlane + 0.01f - depthB) / Math.Max(1e-6f, depthA - depthB);
            b = Vector3.Lerp(b, a, Math.Clamp(t, 0f, 1f));
        }

        bool clippedProjectedA = TryProject(a, out screenA);
        bool clippedProjectedB = TryProject(b, out screenB);
        return clippedProjectedA && clippedProjectedB;
    }

    /// <summary>
    /// Return camera depth. Larger values are farther from the eye and therefore should be
    /// painted first by the renderer's painter ordering.
    /// </summary>
    public float Depth(Vector3 world)
        => IsPov
            ? Vector3.Dot(world - _eye, _forward)
            : Vector3.Dot(world - _target, _forward);

    /// <summary>Project a screen position onto the simulation's Z=0 ground plane.</summary>
    public bool TryScreenToGround(Vector2 screen, out Vector2 ground)
    {
        ground = default;
        if (IsPov || _scale <= 0.0001f || MathF.Abs(_forward.Z) <= 0.0001f)
        {
            return false;
        }

        float cameraX = _cameraCenterX + (screen.X - _canvasCenter.X) / _scale;
        float cameraY = _cameraCenterY - (screen.Y - _canvasCenter.Y) / _scale;
        Vector3 planePoint = _target + _right * cameraX + _up * cameraY;
        float t = -planePoint.Z / _forward.Z;
        Vector3 worldPoint = planePoint + _forward * t;

        if (!float.IsFinite(worldPoint.X) || !float.IsFinite(worldPoint.Y))
        {
            return false;
        }

        ground = new Vector2(worldPoint.X, worldPoint.Y);
        return true;
    }

    private static float DegreesToRadians(float degrees) => degrees * MathF.PI / 180f;
}
