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
        public Camera ViewCamera { get; private set; }
        public float Azimuth { get; set; } = -35f;
        public float Elevation { get; set; } = 34f;
        public float Zoom { get; set; } = 1f;
        public void Initialise()
        {
            GameObject host = new GameObject("Workspace camera");
            host.transform.SetParent(transform, false);
            ViewCamera = host.AddComponent<Camera>();
            ViewCamera.depth = 10;
            ViewCamera.orthographic = true;
            ViewCamera.clearFlags = CameraClearFlags.SolidColor;
            ViewCamera.backgroundColor = new Color(.045f, .07f, .10f);
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
                view.transform.localPosition = new Vector3(body.Position.X, body.Position.Y, body.Position.Z);
                Vector3 scale = new Vector3(body.Size.X, body.Size.Y, body.Size.Z);
                if (body.Shape == ViewShape.Capsule || body.Shape == ViewShape.Cylinder) scale.y *= 0.5f;
                view.transform.localScale = scale;
                // The XY -> XZ mapping reverses positive ground yaw around Unity's Y axis.
                view.transform.localRotation = Quaternion.Euler(0f, -body.GroundAngle * Mathf.Rad2Deg, 0f);
                Color color = overrideColor ?? BodyColor(body);
                string key = color.ToString("F4");
                if (!_materials.TryGetValue(key, out Material material))
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (shader == null) throw new InvalidOperationException("URP Unlit shader is unavailable.");
                    material = new Material(shader) { color = color };
                    _materials.Add(key, material);
                }
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
                case ViewKind.Floor: return new Color(0.3f, 0.36f, 0.4f);
                case ViewKind.Exit: return Color.cyan;
                case ViewKind.Barricade:
                case ViewKind.Source: return new Color(1f, 0.45f, 0.1f);
                case ViewKind.Table: return new Color(0.57f, 0.35f, 0.2f);
                case ViewKind.Chair: return new Color(0.7f, 0.55f, 0.3f);
                case ViewKind.Stage: return new Color(0.4f, 0.42f, 0.5f);
                case ViewKind.Debris: return new Color(1f, 0.75f, 0.25f);
                default: return new Color(0.2f, 0.5f, 0.7f);
            }
        }

        private void OnDestroy()
        {
            foreach (Material material in _materials.Values) Destroy(material);
        }
    }
}
