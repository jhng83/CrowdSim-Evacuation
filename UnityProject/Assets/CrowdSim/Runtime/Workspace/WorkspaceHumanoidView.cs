using System;
using System.Collections.Generic;
using CrowdSim.Diagnostics;
using CrowdSim.Workspace;
using UnityEngine;

namespace CrowdSim.Unity.Workspace
{
    /// <summary>Pooled low-poly humans, outlines and read-only indicators. No Unity physics or model references.</summary>
    public sealed class WorkspaceHumanoidView : MonoBehaviour
    {
        private sealed class Human
        {
            public GameObject Root;
            public readonly List<Transform> Parts = new List<Transform>();
            public readonly List<HumanPart> Geometry = new List<HumanPart>(16);
            public LineRenderer Selection, Panic;
            public HumanMotion Motion;
            public AgentVisualFrame Snapshot;
            public bool Visible;
        }
        private readonly Dictionary<int, Human> _humans = new Dictionary<int, Human>();
        private readonly Stack<Human> _pool = new Stack<Human>();
        private readonly List<int> _stale = new List<int>();
        private readonly HashSet<int> _active = new HashSet<int>();
        private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
        private Camera _camera;
        private Mesh _box;
        private Material _outline, _white, _orange;
        private WorkspaceSession _session;
        private GUIStyle _label;
        private int? _selected;
        public int ActiveCount => _humans.Count;
        public bool IndicatorsVisible { get; set; } = true;

        public void Initialise(Camera camera, Mesh box)
        {
            _camera = camera; _box = box;
            Shader outline = Resources.Load<Shader>("CrowdSim/HumanOutline");
            if (outline == null) throw new InvalidOperationException("Human outline shader is missing.");
            _outline = new Material(outline);
            _white = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(.88f, .96f, 1) };
            _orange = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(1, .37f, .10f) };
        }

        public void Render(WorkspaceSession session, float elapsed, bool animate)
        {
            if (_session != session)
            {
                foreach (Human old in _humans.Values) { old.Root.SetActive(false); _pool.Push(old); }
                _humans.Clear(); _session = session;
            }
            RenderSamples(session.Frame.Agents, session.Tick, elapsed, animate && session.Running,
                (float)session.Speed, session.StepSeconds, session.SelectedAgentId, session.CameraMode);
        }

        /// <summary>The production renderer's detached input seam, also used by the deterministic pose gallery.</summary>
        public void RenderSamples(IReadOnlyList<AgentVisualFrame> samples, int tick, float elapsed, bool animate,
            float speed, float stepSeconds, int? selected, AgentCameraMode mode)
        {
            _selected = selected;
            _active.Clear();
            foreach (AgentVisualFrame agent in samples)
            {
                _active.Add(agent.Id);
                if (!_humans.TryGetValue(agent.Id, out Human human))
                {
                    human = _pool.Count > 0 ? _pool.Pop() : Create();
                    human.Motion = new HumanMotion(agent.Id);
                    human.Root.name = "Human " + agent.Id;
                    _humans.Add(agent.Id, human);
                }
                human.Snapshot = agent;
                human.Motion.Observe(tick, agent.Position, agent.Rise);
                human.Motion.Advance(elapsed, animate, speed, stepSeconds, agent.Attending ? .3f : agent.SpeedRatio);
                human.Visible = !(mode == AgentCameraMode.Pov && agent.Id == _selected);
                human.Root.SetActive(human.Visible);
                Vector2 ground = new Vector2(human.Motion.Position.X, human.Motion.Position.Y);
                human.Root.transform.localPosition = new Vector3(ground.x, 0, ground.y);
                human.Root.transform.localRotation = Quaternion.Euler(0, -Mathf.Atan2(agent.Heading.Y, agent.Heading.X) * Mathf.Rad2Deg, 0);
                Vector3 foot = _camera.WorldToScreenPoint(human.Root.transform.position);
                Vector3 head = _camera.WorldToScreenPoint(human.Root.transform.position + Vector3.up * 1.7f);
                bool detail = agent.Id == _selected || Vector2.Distance(foot, head) >= 48;
                HumanGeometry.Build(agent, detail, human.Motion.Phase, human.Geometry, human.Motion.Rise);
                for (int i = 0; i < human.Geometry.Count; i++)
                {
                    if (i == human.Parts.Count) human.Parts.Add(CreatePart(human.Root.transform));
                    Transform part = human.Parts[i]; HumanPart geometry = human.Geometry[i];
                    part.gameObject.SetActive(true);
                    part.localPosition = Vector(geometry.Center); part.localScale = Vector(geometry.Size);
                    part.localRotation = new Quaternion(geometry.Rotation.X, geometry.Rotation.Y, geometry.Rotation.Z, geometry.Rotation.W);
                    part.GetComponent<MeshRenderer>().sharedMaterial = Material(agent, geometry.Shade);
                }
                for (int i = human.Geometry.Count; i < human.Parts.Count; i++) human.Parts[i].gameObject.SetActive(false);
                human.Selection.enabled = agent.Id == _selected;
                Ring(human.Selection, .57f, .035f);
                human.Panic.enabled = agent.ShowPanic;
                if (agent.ShowPanic) Ring(human.Panic, .32f + agent.Panic * .52f, .018f);
            }
            _stale.Clear();
            foreach (int id in _humans.Keys) if (!_active.Contains(id)) _stale.Add(id);
            foreach (int id in _stale)
            {
                Human human = _humans[id]; human.Root.SetActive(false); human.Snapshot = null;
                _pool.Push(human); _humans.Remove(id);
            }
        }
        public System.Numerics.Vector2? RenderedPosition(int? id)
            => id.HasValue && _humans.TryGetValue(id.Value, out Human human) ? human.Motion.Position : (System.Numerics.Vector2?)null;

        /// <summary>Nearest visible body-part volume under the pointer, including low and seated poses. No engine hitboxes.</summary>
        public int? Pick(Vector2 screen)
        {
            if (!_camera.pixelRect.Contains(screen) || !_camera.orthographic) return null;
            Ray ray = _camera.ScreenPointToRay(screen);
            float nearest = float.PositiveInfinity; int? selected = null;
            foreach (KeyValuePair<int, Human> pair in _humans)
            {
                Human human = pair.Value;
                if (!human.Visible) continue;
                foreach (Transform part in human.Parts)
                {
                    if (!part.gameObject.activeSelf) continue;
                    // Transform the ray into the oriented box, rather than using a broad world AABB.
                    Vector3 origin = part.InverseTransformPoint(ray.origin);
                    Vector3 direction = part.InverseTransformVector(ray.direction);
                    if (new Bounds(Vector3.zero, Vector3.one).IntersectRay(new Ray(origin, direction.normalized), out float localDistance))
                    {
                        float distance = Vector3.Distance(ray.origin, part.TransformPoint(origin + direction.normalized * localDistance));
                        if (distance < nearest) { nearest = distance; selected = pair.Key; }
                    }
                }
            }
            return selected;
        }
        private Human Create()
        {
            GameObject root = new GameObject("Human"); root.transform.SetParent(transform, false);
            return new Human { Root = root, Selection = Line(root.transform, "Selection ring", _white), Panic = Line(root.transform, "Panic ring", _orange) };
        }
        private Transform CreatePart(Transform parent)
        {
            GameObject part = new GameObject("Body part"); part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>().sharedMesh = _box; part.AddComponent<MeshRenderer>();
            GameObject hull = new GameObject("Dark silhouette"); hull.transform.SetParent(part.transform, false);
            hull.transform.localScale = Vector3.one * 1.075f;
            hull.AddComponent<MeshFilter>().sharedMesh = _box; hull.AddComponent<MeshRenderer>().sharedMaterial = _outline;
            return part.transform;
        }
        private Material Material(AgentVisualFrame agent, float shade)
        {
            string key = agent.State + "/" + shade.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            if (_materials.TryGetValue(key, out Material material)) return material;
            System.Numerics.Vector3 rgb = AgentPalette.Color(agent.State) * shade;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(rgb.X, rgb.Y, rgb.Z) };
            material.SetFloat("_Smoothness", .2f);
            _materials.Add(key, material); return material;
        }
        private static LineRenderer Line(Transform parent, string name, Material material)
        {
            GameObject host = new GameObject(name); host.transform.SetParent(parent, false);
            LineRenderer line = host.AddComponent<LineRenderer>(); line.sharedMaterial = material;
            line.useWorldSpace = false; line.loop = true; line.positionCount = 32; line.widthMultiplier = .035f;
            return line;
        }
        private static void Ring(LineRenderer line, float radius, float height)
        {
            for (int i = 0; i < line.positionCount; i++)
            { float angle = i * Mathf.PI * 2 / line.positionCount; line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius)); }
        }
        private static Vector3 Vector(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);

        private void OnGUI()
        {
            if (!IndicatorsVisible || _camera == null || Event.current.type != EventType.Repaint) return;
            if (_label == null) _label = new GUIStyle(GUI.skin.label)
            { font = Resources.Load<Font>("CrowdSim/Fonts/AtkinsonHyperlegible-Regular"), fontSize = 14, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(6, 6, 3, 3) };
            _label.normal.textColor = new Color(.93f, .96f, 1);
            Rect pixel = _camera.pixelRect;
            Rect clip = new Rect(pixel.x, Screen.height - pixel.yMax, pixel.width, pixel.height);
            GUI.BeginGroup(clip);
            foreach (Human human in _humans.Values)
            {
                if (!human.Visible) continue;
                AgentVisualFrame agent = human.Snapshot;
                bool selected = agent.Id == _selected;
                string text = agent.Indicator;
                if (selected) text = "#" + agent.Id + "  " + agent.StateLabel + "  | Panic " + agent.Panic.ToString("P0") + (text.Length > 0 ? "\n" + text : "");
                if (text.Length == 0) continue;
                Vector3 point = _camera.WorldToScreenPoint(human.Root.transform.position + Vector3.up * (agent.Pose == HumanPose.Prone || agent.Pose == HumanPose.Crawling ? .75f : agent.Pose == HumanPose.Assisting ? 1.5f : 2.03f));
                if (point.z <= 0 || !_camera.pixelRect.Contains(point)) continue;
                Vector2 size = _label.CalcSize(new GUIContent(text));
                Rect rect = new Rect(point.x - clip.x - size.x / 2, Screen.height - point.y - clip.y - size.y, size.x, size.y);
                Color saved = GUI.color; GUI.color = new Color(.035f, .06f, .09f, .94f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = saved; GUI.Label(rect, text, _label);
            }
            GUI.EndGroup();
        }
        private void OnDestroy()
        {
            foreach (Material material in _materials.Values) Destroy(material);
            if (_outline != null) Destroy(_outline); if (_white != null) Destroy(_white); if (_orange != null) Destroy(_orange);
        }
    }
}
