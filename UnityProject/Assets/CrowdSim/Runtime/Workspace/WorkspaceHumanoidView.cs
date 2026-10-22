using System;
using System.Collections.Generic;
using CrowdSim.Diagnostics;
using CrowdSim.Core.Model;
using UnityEngine.Rendering;
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
            public readonly Matrix4x4[] LocalMatrices = new Matrix4x4[16];
            public readonly List<Transform> Parts = new List<Transform>();
            public readonly List<MeshRenderer> Renderers = new List<MeshRenderer>();
            public float LastPhase = float.NaN, LastRise = float.NaN, LastPanic = float.NaN;
            public bool LastDetail; public AgentState LastState; public HumanPose LastPose;
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
        private readonly Dictionary<(AgentState, int), Material> _materials = new Dictionary<(AgentState, int), Material>();
        private Camera _camera;
        private Mesh _box;
        private Material _outline, _white, _orange;
        private Shader _instancedShader;
        private Mesh _ringMesh;
        private readonly CrowdInstanceBatch _gpu = new CrowdInstanceBatch();
        private bool _gpuActive;
        public bool PreferGpuInstancing { get; set; } = true;
        public bool UsingGpuInstancing => _gpuActive;
        public int GpuInstances => _gpuActive ? _gpu.InstanceCount : 0;
        public int GpuDraws => _gpuActive ? _gpu.DrawCount : 0;
        public string RenderBackend => _gpuActive ? "GPU instancing" : "Compatibility renderers";
        private WorkspaceSession _session;
        private GUIStyle _label;
        private int? _selected;
        private readonly Plane[] _frustum = new Plane[6];
        public int VisibleCount { get; private set; }
        public int ActiveCount => _humans.Count;
        public bool IndicatorsVisible { get; set; } = true;

        public void Initialise(Camera camera, Mesh box)
        {
            _camera = camera; _box = box;
            Shader outline = Resources.Load<Shader>("CrowdSim/HumanOutline");
            if (outline == null) throw new InvalidOperationException("Human outline shader is missing.");
            _outline = new Material(outline) { enableInstancing = true };
            _instancedShader = Resources.Load<Shader>("CrowdSim/CrowdInstanced");
            _ringMesh = CreateRingMesh();
            _white = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.black };
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
            bool gpu = PreferGpuInstancing && SystemInfo.supportsInstancing && _instancedShader != null && _instancedShader.isSupported;
            if (gpu != _gpuActive) ChangeBackend(gpu);
            _gpu.Reset();
            _selected = selected; VisibleCount = 0;
            GeometryUtility.CalculateFrustumPlanes(_camera, _frustum);
            _active.Clear();
            foreach (AgentVisualFrame agent in samples)
            {
                _active.Add(agent.Id);
                if (!_humans.TryGetValue(agent.Id, out Human human))
                {
                    human = _pool.Count > 0 ? _pool.Pop() : Create();
                    human.Motion = new HumanMotion(agent.Id);
                    human.LastPhase = float.NaN; human.LastPanic = float.NaN;
                    human.Root.name = "Human " + agent.Id;
                    _humans.Add(agent.Id, human);
                }
                human.Snapshot = agent;
                human.Motion.Observe(tick, agent.Position, agent.Rise);
                human.Motion.Advance(elapsed, animate, speed, stepSeconds, agent.Attending ? .3f : agent.SpeedRatio);
                Vector3 anchor = new Vector3(human.Motion.Position.X, .85f, human.Motion.Position.Y);
                bool inView = _camera.transform.position.y < 2 || GeometryUtility.TestPlanesAABB(_frustum, new Bounds(anchor, new Vector3(2, 2, 2)));
                human.Visible = inView && !(mode == AgentCameraMode.Pov && agent.Id == _selected);
                if (human.Root.activeSelf != human.Visible) human.Root.SetActive(human.Visible);
                if (!human.Visible) continue;
                VisibleCount++;
                Vector2 ground = new Vector2(human.Motion.Position.X, human.Motion.Position.Y);
                human.Root.transform.localPosition = new Vector3(ground.x, 0, ground.y);
                human.Root.transform.localRotation = Quaternion.Euler(0, -Mathf.Atan2(agent.Heading.Y, agent.Heading.X) * Mathf.Rad2Deg, 0);
                Vector3 foot = _camera.WorldToScreenPoint(human.Root.transform.position);
                Vector3 head = _camera.WorldToScreenPoint(human.Root.transform.position + Vector3.up * 1.7f);
                bool detail = agent.Id == _selected || Vector2.Distance(foot, head) >= 48;
                bool rebuild = human.Geometry.Count == 0 || human.LastPhase != human.Motion.Phase || human.LastRise != human.Motion.Rise ||
                    human.LastDetail != detail || human.LastState != agent.State || human.LastPose != agent.Pose;
                if (rebuild)
                {
                    HumanGeometry.Build(agent, detail, human.Motion.Phase, human.Geometry, human.Motion.Rise);
                    for (int i = 0; i < human.Geometry.Count; i++)
                    {
                        HumanPart geometry = human.Geometry[i];
                        human.LocalMatrices[i] = Matrix4x4.TRS(Vector(geometry.Center),
                            new Quaternion(geometry.Rotation.X, geometry.Rotation.Y, geometry.Rotation.Z, geometry.Rotation.W), Vector(geometry.Size));
                        if (_gpuActive) continue;
                        if (i == human.Parts.Count)
                        {
                            Transform created = CreatePart(human.Root.transform); human.Parts.Add(created);
                            human.Renderers.Add(created.GetComponent<MeshRenderer>());
                        }
                        Transform part = human.Parts[i];
                        if (!part.gameObject.activeSelf) part.gameObject.SetActive(true);
                        part.localPosition = Vector(geometry.Center); part.localScale = Vector(geometry.Size);
                        part.localRotation = new Quaternion(geometry.Rotation.X, geometry.Rotation.Y, geometry.Rotation.Z, geometry.Rotation.W);
                        if (human.LastState != agent.State || human.Renderers[i].sharedMaterial == null)
                            human.Renderers[i].sharedMaterial = Material(agent, geometry.Shade);
                    }
                    for (int i = human.Geometry.Count; i < human.Parts.Count; i++)
                        if (human.Parts[i].gameObject.activeSelf) human.Parts[i].gameObject.SetActive(false);
                    // Part indices can change meaning/shade when a pose/detail level changes.
                    if (!_gpuActive && (human.LastDetail != detail || human.LastPose != agent.Pose))
                        for (int i = 0; i < human.Geometry.Count; i++) human.Renderers[i].sharedMaterial = Material(agent, human.Geometry[i].Shade);
                    human.LastPhase = human.Motion.Phase; human.LastRise = human.Motion.Rise;
                    human.LastDetail = detail; human.LastState = agent.State; human.LastPose = agent.Pose;
                }
                if (_gpuActive)
                {
                    Matrix4x4 rootMatrix = human.Root.transform.localToWorldMatrix;
                    for (int i = 0; i < human.Geometry.Count; i++)
                    {
                        Matrix4x4 matrix = rootMatrix * human.LocalMatrices[i];
                        _gpu.Add(Material(agent, human.Geometry[i].Shade), _box, matrix);
                        _gpu.Add(_outline, _box, matrix * Matrix4x4.Scale(Vector3.one * 1.075f));
                    }
                    if (agent.Id == _selected) AddGpuRing(human, _white, .57f);
                    if (agent.ShowPanic) AddGpuRing(human, _orange, .32f + agent.Panic * .52f);
                    continue;
                }
                human.Selection.enabled = agent.Id == _selected;
                human.Panic.enabled = agent.ShowPanic;
                if (human.LastPanic != agent.Panic)
                {
                    if (agent.ShowPanic) Ring(human.Panic, .32f + agent.Panic * .52f, .018f);
                    human.LastPanic = agent.Panic;
                }
            }
            _stale.Clear();
            foreach (int id in _humans.Keys) if (!_active.Contains(id)) _stale.Add(id);
            foreach (int id in _stale)
            {
                Human human = _humans[id]; human.Root.SetActive(false); human.Snapshot = null;
                _pool.Push(human); _humans.Remove(id);
            }
            if (_gpuActive) _gpu.Draw(_camera);
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
                Matrix4x4 rootMatrix = human.Root.transform.localToWorldMatrix;
                for (int i = 0; i < human.Geometry.Count; i++)
                {
                    Matrix4x4 matrix = rootMatrix * human.LocalMatrices[i];
                    Matrix4x4 inverse = matrix.inverse;
                    Vector3 origin = inverse.MultiplyPoint3x4(ray.origin);
                    Vector3 direction = inverse.MultiplyVector(ray.direction).normalized;
                    if (new Bounds(Vector3.zero, Vector3.one).IntersectRay(new Ray(origin, direction), out float localDistance))
                    {
                        float distance = Vector3.Distance(ray.origin, matrix.MultiplyPoint3x4(origin + direction * localDistance));
                        if (distance < nearest) { nearest = distance; selected = pair.Key; }
                    }
                }
            }
            return selected;
        }
        private Human Create()
        {
            GameObject root = new GameObject("Human"); root.transform.SetParent(transform, false);
            Human human = new Human { Root = root };
            if (!_gpuActive)
            {
                human.Selection = Line(root.transform, "Selection ring", _white); human.Panic = Line(root.transform, "Panic ring", _orange);
                Ring(human.Selection, .57f, .035f);
            }
            return human;
        }
        private void ChangeBackend(bool gpu)
        {
            foreach (Human human in _humans.Values) { human.Root.SetActive(false); Destroy(human.Root); }
            foreach (Human human in _pool) { human.Root.SetActive(false); Destroy(human.Root); }
            _humans.Clear(); _pool.Clear(); _gpu.Clear();
            foreach (Material material in _materials.Values) Destroy(material);
            _materials.Clear(); _gpuActive = gpu;
            if (_white != null) Destroy(_white); if (_orange != null) Destroy(_orange);
            _white = new Material(gpu ? _instancedShader : Shader.Find("Universal Render Pipeline/Unlit"));
            _orange = new Material(gpu ? _instancedShader : Shader.Find("Universal Render Pipeline/Unlit"));
            if (gpu)
            {
                _white.SetColor("_BaseColor", Color.black); _orange.SetColor("_BaseColor", new Color(1, .37f, .10f));
                _white.SetFloat("_Unlit", 1); _orange.SetFloat("_Unlit", 1);
            }
            else { _white.color = Color.black; _orange.color = new Color(1, .37f, .10f); }
        }
        private void AddGpuRing(Human human, Material material, float radius)
        {
            Vector3 p = human.Root.transform.position; p.y = material == _white ? .035f : .018f;
            _gpu.Add(material, _ringMesh, Matrix4x4.TRS(p, Quaternion.identity, new Vector3(radius, 1, radius)));
        }
        private static Mesh CreateRingMesh()
        {
            const int segments = 32;
            Vector3[] vertices = new Vector3[segments * 2]; Vector3[] normals = new Vector3[vertices.Length];
            int[] triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                Vector3 p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                vertices[i * 2] = p * .975f; vertices[i * 2 + 1] = p * 1.025f;
                normals[i * 2] = normals[i * 2 + 1] = Vector3.up;
                int n = ((i + 1) % segments) * 2, at = i * 6;
                triangles[at] = i * 2; triangles[at + 1] = n; triangles[at + 2] = i * 2 + 1;
                triangles[at + 3] = i * 2 + 1; triangles[at + 4] = n; triangles[at + 5] = n + 1;
            }
            Mesh mesh = new Mesh { name = "Crowd / instanced indicator ring" };
            mesh.vertices = vertices; mesh.normals = normals; mesh.triangles = triangles; mesh.RecalculateBounds(); return mesh;
        }
        private Transform CreatePart(Transform parent)
        {
            GameObject part = new GameObject("Body part"); part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>().sharedMesh = _box; part.AddComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            GameObject hull = new GameObject("Dark silhouette"); hull.transform.SetParent(part.transform, false);
            hull.transform.localScale = Vector3.one * 1.075f;
            hull.AddComponent<MeshFilter>().sharedMesh = _box; hull.AddComponent<MeshRenderer>().sharedMaterial = _outline;
            return part.transform;
        }
        private Material Material(AgentVisualFrame agent, float shade)
        {
            (AgentState, int) key = (agent.State, Mathf.RoundToInt(shade * 100));
            if (_materials.TryGetValue(key, out Material material)) return material;
            System.Numerics.Vector3 rgb = AgentPalette.Color(agent.State) * shade;
            material = new Material(_gpuActive ? _instancedShader : Shader.Find("Universal Render Pipeline/Lit"));
            if (_gpuActive) material.SetColor("_BaseColor", new Color(rgb.X, rgb.Y, rgb.Z));
            else material.color = new Color(rgb.X, rgb.Y, rgb.Z);
            if (!_gpuActive) material.SetFloat("_Smoothness", .2f);
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
            _label.normal.textColor = Color.black;
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
                Color saved = GUI.color; GUI.color = new Color(.98f, .975f, .95f, .96f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = saved; GUI.Label(rect, text, _label);
            }
            GUI.EndGroup();
        }
        private void OnDestroy()
        {
            foreach (Material material in _materials.Values) Destroy(material);
            if (_ringMesh != null) Destroy(_ringMesh);
            _gpu.Clear();
            if (_outline != null) Destroy(_outline); if (_white != null) Destroy(_white); if (_orange != null) Destroy(_orange);
        }
    }
}
