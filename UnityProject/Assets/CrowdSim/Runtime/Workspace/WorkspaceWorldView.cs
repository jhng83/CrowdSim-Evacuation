using System;
using System.Collections.Generic;
using System.Linq;
using CrowdSim.Core.Model;
using CrowdSim.Diagnostics;
using UnityEngine;

namespace CrowdSim.Unity.Workspace
{
    /// <summary>Detached pooled primitives; Unity physics never moves the crowd.</summary>
    public sealed class WorkspaceWorldView : MonoBehaviour
    {
        private readonly Dictionary<string, GameObject> _views = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, ViewShape> _shapes = new Dictionary<string, ViewShape>();
        private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
        private WorkspaceSurfaceLibrary _surfaces;
        public Camera ViewCamera { get; private set; }
        public float Azimuth { get; set; } = -35f;
        public float Elevation { get; set; } = 34f;
        public float Zoom { get; set; } = 1f;
        public void Initialise()
        {
            _surfaces = new WorkspaceSurfaceLibrary();
            GameObject lighting = new GameObject("Studio key light"); lighting.transform.SetParent(transform, false);
            Light key = lighting.AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 1.1f;
            key.color = new Color(1, .97f, .92f); key.shadows = LightShadows.Soft; lighting.transform.rotation = Quaternion.Euler(48, -30, 0);
            GameObject fillHost = new GameObject("Studio fill light"); fillHost.transform.SetParent(transform, false);
            Light fill = fillHost.AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .35f;
            fill.color = new Color(.65f, .78f, 1); fillHost.transform.rotation = Quaternion.Euler(25, 145, 0);
            GameObject host = new GameObject("Workspace camera");
            host.transform.SetParent(transform, false);
            ViewCamera = host.AddComponent<Camera>();
            ViewCamera.depth = 10;
            ViewCamera.orthographic = true;
            ViewCamera.clearFlags = CameraClearFlags.SolidColor;
            ViewCamera.backgroundColor = new Color(.055f, .075f, .10f);
        }
        public void Draw(IEnumerable<ViewBody> bodies, string prefix = "", Color? overrideColor = null)
        {
            HashSet<string> active = new HashSet<string>();
            foreach (ViewBody body in bodies)
            {
                string id = prefix + body.Id;
                active.Add(id);
                if (_views.TryGetValue(id, out GameObject previous) && _shapes[id] != body.Shape)
                {
                    Destroy(previous); _views.Remove(id); _shapes.Remove(id);
                }
                if (!_views.TryGetValue(id, out GameObject view))
                {
                    PrimitiveType primitive = body.Shape == ViewShape.Capsule ? PrimitiveType.Capsule
                        : body.Shape == ViewShape.Cylinder ? PrimitiveType.Cylinder
                        : body.Shape == ViewShape.Sphere ? PrimitiveType.Sphere : PrimitiveType.Cube;
                    view = GameObject.CreatePrimitive(primitive);
                    view.name = id;
                    view.transform.SetParent(transform, false);
                    Collider collider = view.GetComponent<Collider>();
                    if (collider != null) Destroy(collider);
                    _views.Add(id, view); _shapes.Add(id, body.Shape);
                }
                if (body.Shape == ViewShape.Box && (body.Kind == ViewKind.Table || body.Kind == ViewKind.Chair || body.Kind == ViewKind.Barricade))
                    view.GetComponent<MeshFilter>().sharedMesh = _surfaces.Box;
                view.transform.localPosition = new Vector3(body.Position.X, body.Position.Y, body.Position.Z);
                Vector3 scale = new Vector3(body.Size.X, body.Size.Y, body.Size.Z);
                if (body.Shape == ViewShape.Capsule || body.Shape == ViewShape.Cylinder) scale.y *= 0.5f;
                view.transform.localScale = scale;
                // The XY -> XZ mapping reverses positive ground yaw around Unity's Y axis.
                view.transform.localRotation = Quaternion.Euler(0f, -body.GroundAngle * Mathf.Rad2Deg, 0f);
                Color color = overrideColor ?? BodyColor(body);
                bool flat = overrideColor.HasValue || body.Kind == ViewKind.Source || body.Kind == ViewKind.Exit;
                bool metal = body.Id.Contains("-leg-") || body.Id.EndsWith("-pedestal", StringComparison.Ordinal);
                string surface = !flat && body.Kind == ViewKind.Floor ? "floor" : !flat && body.Kind == ViewKind.Barricade ? "barrier"
                    : !flat && body.Kind == ViewKind.Table && !metal ? "wood" : "plain";
                if (!overrideColor.HasValue && metal && (body.Kind == ViewKind.Table || body.Kind == ViewKind.Chair)) color = new Color(.22f, .29f, .34f);
                string key = (flat ? "flat/" : "lit/") + surface + color.ToString("F4");
                if (!_materials.TryGetValue(key, out Material material))
                {
                    Shader shader = Shader.Find(flat ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
                    if (shader == null) throw new InvalidOperationException("The configured URP surface shader is unavailable.");
                    material = new Material(shader) { color = color };
                    if (!flat)
                    {
                        material.SetFloat("_Smoothness", metal ? .32f : .18f); material.SetFloat("_Metallic", metal ? .35f : 0);
                        material.SetColor("_EmissionColor", color * .08f); material.EnableKeyword("_EMISSION");
                    }
                    if (surface == "wood") material.mainTexture = _surfaces.Wood;
                    if (surface == "barrier") { material.mainTexture = _surfaces.Barrier; material.color = Color.white; }
                    if (surface == "floor") material.mainTexture = _surfaces.Floor;
                    _materials.Add(key, material);
                }
                if (surface == "floor") material.mainTextureScale = new Vector2(body.Size.X / 2, body.Size.Z / 2);
                view.GetComponent<Renderer>().sharedMaterial = material;
            }
            foreach (string id in _views.Keys.Where(id => id.StartsWith(prefix, StringComparison.Ordinal) && (prefix.Length > 0 || (!id.StartsWith("preview-") && !id.StartsWith("grid-") && !id.StartsWith("selection-"))) && !active.Contains(id)).ToArray())
            {
                Destroy(_views[id]);
                _views.Remove(id); _shapes.Remove(id);
            }
        }

        public void Fit(float width, float length, Rect viewport)
        {
            ViewCamera.rect = viewport;
            Vector3 target = new Vector3(width / 2, 0, length / 2);
            float elevation = Elevation * Mathf.Deg2Rad, azimuth = Azimuth * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(elevation) * Mathf.Cos(azimuth), Mathf.Sin(elevation), Mathf.Cos(elevation) * Mathf.Sin(azimuth));
            ViewCamera.transform.position = target + direction * 200;
            ViewCamera.transform.LookAt(target, Vector3.up);
            float horizontal = 0, vertical = 0;
            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = new Vector3(x * width / 2, 0, z * length / 2);
                horizontal = Mathf.Max(horizontal, Mathf.Abs(Vector3.Dot(corner, ViewCamera.transform.right)));
                vertical = Mathf.Max(vertical, Mathf.Abs(Vector3.Dot(corner, ViewCamera.transform.up)));
            }
            float aspect = Mathf.Max(.1f, Screen.width * viewport.width / Mathf.Max(1, Screen.height * viewport.height));
            ViewCamera.orthographicSize = Mathf.Max(vertical + 3, (horizontal + 3) / aspect) * 1.08f / Mathf.Clamp(Zoom, .45f, 2.5f);
        }
        public bool GroundPoint(Vector2 screenPoint, out System.Numerics.Vector2 point, float height = 0)
        {
            point = default;
            if (!ViewCamera.pixelRect.Contains(screenPoint)) return false;
            Ray ray = ViewCamera.ScreenPointToRay(screenPoint);
            if (!new Plane(Vector3.up, new Vector3(0, height, 0)).Raycast(ray, out float distance)) return false;
            Vector3 hit = ray.GetPoint(distance);
            point = new System.Numerics.Vector2(hit.x, hit.z);
            return true;
        }
        public void ResetCamera() { Azimuth = -35; Elevation = 34; Zoom = 1; }
        private static Color BodyColor(ViewBody body)
        {
            if (body.Kind == ViewKind.Agent || body.Kind == ViewKind.AgentDetail)
            {
                switch (body.State)
                {
                    case AgentState.Evacuating: return new Color(0.15f, 0.85f, 0.35f);
                    case AgentState.WaitingToReact: return new Color(1f, 0.7f, 0.15f);
                    case AgentState.Injured: return new Color(0.15f, 0.4f, 1f);
                    case AgentState.Dead: return Color.gray;
                    case AgentState.Crawling: return Color.yellow;
                    case AgentState.Fallen: return Color.red;
                    case AgentState.Incapacitated:
                    case AgentState.TempIncap: return new Color(0.5f, 0.1f, 0.1f);
                    default: return new Color(0.92f, 0.94f, 0.96f);
                }
            }
            switch (body.Kind)
            {
                case ViewKind.Floor: return new Color(.36f, .41f, .45f);
                case ViewKind.Exit: return new Color(.25f, .90f, .66f);
                case ViewKind.Barricade: return new Color(.85f, .68f, .25f);
                case ViewKind.Source: if (body.Id == "source") return new Color(.2f, .95f, .65f); return new Color(1f, 0.45f, 0.1f);
                case ViewKind.Table: return new Color(.66f, .49f, .31f);
                case ViewKind.Chair: return new Color(.24f, .47f, .49f);
                case ViewKind.Stage: return new Color(.31f, .37f, .44f);
                case ViewKind.Debris: return Color.Lerp(new Color(.43f, .29f, .18f), new Color(1, .58f, .16f), Mathf.Round(Mathf.Clamp01(body.Energy) * 8) / 8);
                default: return new Color(0.2f, 0.5f, 0.7f);
            }
        }

        private void OnDestroy()
        {
            foreach (Material material in _materials.Values) Destroy(material);
            _surfaces?.Dispose();
        }
    }
}
