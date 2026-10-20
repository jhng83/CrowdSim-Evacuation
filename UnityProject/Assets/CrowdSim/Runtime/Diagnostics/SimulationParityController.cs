using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrowdSim.Core.Model;
using CrowdSim.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace CrowdSim.Unity.Diagnostics
{
    /// <summary>M2's bounded diagnostic viewer. It schedules the portable engine and renders detached snapshots.</summary>
    public sealed class SimulationParityController : MonoBehaviour
    {
        private static readonly string[] Presets = { "explosion-open", "explosion-furnished", "toxic-205" };
        private readonly Dictionary<string, GameObject> _views = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
        private readonly FixedStepScheduler _scheduler = new FixedStepScheduler();
        private DiagnosticSession _session;
        private UIDocument _document;
        private PanelSettings _panelSettings;
        private GameObject _world;
        private Camera _camera;
        private VisualElement _panel;
        private DropdownField _preset;
        private Toggle _hybrid;
        private Label _caseLabel;
        private Label _metrics;
        private Label _result;
        private Button _load;
        private Button _play;
        private Button _step;
        private Button _check;
        private bool _playing;
        private bool _checking;
        private bool _ready;

        public int Tick => _session == null ? 0 : _session.Tick;
        public bool CheckPassed => _session != null && _session.Passed;

        private void OnEnable()
        {
            try
            {
                BuildInterface();
                _world = new GameObject("Detached diagnostic views");
                _world.transform.SetParent(transform, false);
                GameObject cameraObject = new GameObject("Diagnostic camera");
                cameraObject.transform.SetParent(_world.transform, false);
                _camera = cameraObject.AddComponent<Camera>();
                _camera.orthographic = true;
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(0.045f, 0.07f, 0.10f);
                _ready = true;
                LoadSelected();
            }
            catch (Exception exception)
            {
                _ready = false;
                Debug.LogException(exception);
                if (_result != null) _result.text = "SETUP FAILED: " + exception.Message;
            }
        }

        private void BuildInterface()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null) _document = gameObject.AddComponent<UIDocument>();
            ThemeStyleSheet theme = Resources.Load<ThemeStyleSheet>("CrowdSim/FoundationTheme");
            StyleSheet style = Resources.Load<StyleSheet>("CrowdSim/Parity");
            if (theme == null || style == null) throw new InvalidOperationException("Diagnostic UI resources are missing.");
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.themeStyleSheet = theme;
            _panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            _document.panelSettings = _panelSettings;
            _document.visualTreeAsset = null;
            VisualElement root = _document.rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            root.styleSheets.Add(style);
            _panel = new VisualElement();
            _panel.AddToClassList("diagnostic-panel");
            root.Add(_panel);
            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
            _panel.Add(scroll);
            AddLabel(scroll, "CROWD / SIM", "brand");
            AddLabel(scroll, "M2 · portable engine", "title");
            AddLabel(scroll, "Preset diagnostics · 0.15 seconds per tick", "muted");
            _preset = new DropdownField("Preset", new List<string> { "Open Explosion · 50", "Furnished Explosion · 61", "Toxic Fume · 205" }, 0);
            scroll.Add(_preset);
            _hybrid = new Toggle("Hybrid mode") { value = false };
            scroll.Add(_hybrid);
            _load = AddButton(scroll, "Load / reset selected", LoadSelected);
            _caseLabel = AddLabel(scroll, "", "case-label");
            _play = AddButton(scroll, "Start 1x", () =>
            {
                _playing = !_playing;
                _checking = false;
                _scheduler.Reset();
                RefreshInterface();
            });
            _step = AddButton(scroll, "Step one tick", () =>
            {
                if (_session != null) _session.StepOnce();
                _scheduler.Reset();
                ApplyFrame();
                RefreshInterface();
            });
            _check = AddButton(scroll, "Run full 120-tick check", () =>
            {
                LoadSelected();
                _checking = _ready;
                RefreshInterface();
            });
            AddButton(scroll, "Pause", () => { _playing = false; _checking = false; _scheduler.Reset(); RefreshInterface(); });
            _metrics = AddLabel(scroll, "", "metrics");
            _result = AddLabel(scroll, "", "result");
            AddLabel(scroll, "Event at tick 10; seven reference checkpoints and replay. Floats use documented tolerances; states/counts are exact.", "muted");
            AddLabel(scroll, "Capsules and blocks are diagnostic views. Full builder, humanoids, plume graphics and analysis arrive in later milestones.", "muted");
            AddButton(scroll, "Return to startup", () => SceneManager.LoadScene("Bootstrap"));
        }

        public void LoadSelected()
        {
            _playing = false;
            _checking = false;
            _scheduler.Reset();
            string preset = Presets[_preset.index];
            string directory = Path.Combine(Application.streamingAssetsPath, "Parity");
            string name = preset + (_hybrid.value ? "-hybrid" : "-individual");
            string baseline = File.ReadAllText(Path.Combine(directory, name + ".json"));
            string fixture = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Scenarios", "toxic_fume_205.json"));
            _session = new DiagnosticSession(preset, _hybrid.value, baseline, fixture);
            foreach (GameObject view in _views.Values) Destroy(view);
            _views.Clear();
            ApplyFrame();
            RefreshInterface();
        }

        private void Update()
        {
            if (!_ready || _session == null) return;
            try
            {
                int before = _session.Tick;
                if (_checking)
                {
                    for (int i = 0; i < 4 && !_session.Completed; i++) _session.StepOnce();
                }
                else if (_playing)
                    _scheduler.Advance(Time.unscaledDeltaTime, _session.StepSeconds, 1d,
                        () => _session.StepOnce(), () => _session.Completed);
                if (_session.Completed) { _playing = false; _checking = false; }
                if (before != _session.Tick) { ApplyFrame(); RefreshInterface(); }
                FitCamera();
            }
            catch (Exception exception)
            {
                _playing = false;
                _checking = false;
                _ready = false;
                _result.text = "RUN FAILED: " + exception.Message;
                Debug.LogException(exception);
            }
        }

        private void RefreshInterface()
        {
            if (_session == null) return;
            PresentationFrame frame = _session.Frame;
            _caseLabel.text = _session.Name;
            _metrics.text = "Tick " + frame.Tick + " / 120  ·  " + (frame.Tick * _session.StepSeconds).ToString("F2") + " s\n" +
                "Population " + frame.Population + " · exited " + frame.Exited + "\n" +
                "Injured " + frame.Injured + " · dead " + frame.Dead + "\n" +
                "Maximum exposure " + frame.MaximumExposure.ToString("F4") + "\n" +
                "Hybrid open-space agents " + frame.OpenSpaceAgents + "\n" +
                "Hazard " + (frame.HazardReleased ? "triggered" : "waiting for tick 10");
            _result.text = _session.Failures.Count > 0 ? "FAIL\n" + string.Join("\n", _session.Failures)
                : _session.Passed ? "PASS · 7/7 checkpoints\nReplay restored frame zero."
                : "Pending · " + _session.ComparedCheckpoints + "/7 checkpoints\n" + (_playing ? "Running at 1x" : _checking ? "Checking" : "Scheduling paused");
            bool busy = _playing || _checking;
            _preset.SetEnabled(!busy); _hybrid.SetEnabled(!busy); _load.SetEnabled(!busy);
            _check.SetEnabled(!busy); _step.SetEnabled(!busy && !_session.Completed);
            _play.SetEnabled(!_checking && !_session.Completed);
            _play.text = _playing ? "Pause 1x" : "Start 1x";
        }

        private void ApplyFrame()
        {
            if (_session == null || _world == null) return;
            HashSet<string> active = new HashSet<string>();
            foreach (ViewBody body in _session.Frame.Bodies)
            {
                active.Add(body.Id);
                if (!_views.TryGetValue(body.Id, out GameObject view))
                {
                    PrimitiveType primitive = body.Shape == ViewShape.Capsule ? PrimitiveType.Capsule
                        : body.Shape == ViewShape.Cylinder ? PrimitiveType.Cylinder
                        : body.Shape == ViewShape.Sphere ? PrimitiveType.Sphere : PrimitiveType.Cube;
                    view = GameObject.CreatePrimitive(primitive);
                    view.name = body.Id;
                    view.transform.SetParent(_world.transform, false);
                    Collider collider = view.GetComponent<Collider>();
                    if (collider != null) Destroy(collider);
                    _views.Add(body.Id, view);
                }
                view.transform.localPosition = new Vector3(body.Position.X, body.Position.Y, body.Position.Z);
                Vector3 scale = new Vector3(body.Size.X, body.Size.Y, body.Size.Z);
                if (body.Shape == ViewShape.Capsule || body.Shape == ViewShape.Cylinder) scale.y *= 0.5f;
                view.transform.localScale = scale;
                // The XY -> XZ mapping reverses positive ground yaw around Unity's Y axis.
                view.transform.localRotation = Quaternion.Euler(0f, -body.GroundAngle * Mathf.Rad2Deg, 0f);
                string key = body.Kind + ":" + body.State;
                if (!_materials.TryGetValue(key, out Material material))
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (shader == null) throw new InvalidOperationException("URP Unlit shader is unavailable.");
                    material = new Material(shader) { color = BodyColor(body) };
                    _materials.Add(key, material);
                }
                view.GetComponent<Renderer>().sharedMaterial = material;
            }
            foreach (string id in _views.Keys.Where(id => !active.Contains(id)).ToArray())
            {
                Destroy(_views[id]);
                _views.Remove(id);
            }
            FitCamera();
        }

        private void FitCamera()
        {
            if (_camera == null || _session == null) return;
            float sidebar = Mathf.Min(390f, Screen.width * 0.43f);
            _panel.style.width = Mathf.Max(180f, sidebar - 24f);
            _camera.rect = new Rect(sidebar / Mathf.Max(1, Screen.width), 0f, 1f - sidebar / Mathf.Max(1, Screen.width), 1f);
            Vector3 target = new Vector3(_session.Frame.Width / 2f, 0f, _session.Frame.Length / 2f);
            float elevation = 34f * Mathf.Deg2Rad, azimuth = -35f * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(elevation) * Mathf.Cos(azimuth), Mathf.Sin(elevation), Mathf.Cos(elevation) * Mathf.Sin(azimuth));
            _camera.transform.position = target + direction * 100f;
            _camera.transform.LookAt(target, Vector3.up);
            float horizontal = 0f, vertical = 0f;
            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = new Vector3(x * _session.Frame.Width / 2f, 0f, z * _session.Frame.Length / 2f);
                horizontal = Mathf.Max(horizontal, Mathf.Abs(Vector3.Dot(corner, _camera.transform.right)));
                vertical = Mathf.Max(vertical, Mathf.Abs(Vector3.Dot(corner, _camera.transform.up)));
            }
            float aspect = Mathf.Max(0.2f, (Screen.width - sidebar) / Mathf.Max(1f, Screen.height));
            _camera.orthographicSize = Mathf.Max(vertical + 3f, (horizontal + 3f) / aspect) * 1.08f;
        }

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

        private static Label AddLabel(VisualElement parent, string text, string className)
        {
            Label label = new Label(text); label.AddToClassList(className); parent.Add(label); return label;
        }
        private static Button AddButton(VisualElement parent, string text, Action action)
        {
            Button button = new Button(action) { text = text }; parent.Add(button); return button;
        }

        private void OnDisable()
        {
            _ready = false; _playing = false; _checking = false;
            if (_document != null) _document.panelSettings = null;
            if (_panelSettings != null) Destroy(_panelSettings);
            if (_world != null) Destroy(_world);
            foreach (Material material in _materials.Values) Destroy(material);
            _materials.Clear(); _views.Clear();
        }
    }
}
