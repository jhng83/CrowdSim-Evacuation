using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using CrowdSim.Core.Persistence;
using CrowdSim.Core.Model;
using CrowdSim.Diagnostics;
using CrowdSim.OpenGL.UI;
using CrowdSim.Workspace;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using GroundPoint = System.Numerics.Vector2;
using ViewVector = System.Numerics.Vector3;

namespace CrowdSim.Unity.Workspace
{
    /// <summary>Common build/run and furniture workspace. All model changes pass through the testable session commands.</summary>
    public sealed partial class WorkspaceController : MonoBehaviour
    {
        public WorkspaceFiles Files { get; private set; }
        public WorkspaceSession Session => Files == null ? null : Files.Session;
        private WorkspaceWorldView _world;
        private UIDocument _document;
        private PanelSettings _settings;
        private Action _exit;
        private VisualElement _root, _left, _right, _viewport, _page, _inspectorPage, _builder, _footer;
        private Label _status, _metrics, _readiness, _selection, _inspectorText;
        private Button _finalise, _transport, _undo, _arm, _toggleInspector;
        private FloatField _width, _length;
        private Slider _speed;
        private Toggle _hybrid;
        private readonly List<Slider> _behaviour = new List<Slider>();
        private PlacementTool _tool = PlacementTool.Exit;
        private int _exitWidth = 1, _people = 1;
        private VisualElement _assetControls;
        private Label _assetHint;
        private Button _placeAsset;
        private Slider _angleSlider;
        private FloatField _exactAngle;
        private bool _overViewport, _hasPointer;
        private Vector3 _pointerPosition;
        private int _drawnRevision = -1;
        private bool _drawnGrid;
        private string _activePage = "Build / Run";
        private float _lastPreviewTime;
        private readonly List<ViewBody> _grid = new List<ViewBody>();

        public void Open(ScenarioType scenario, Action exit = null, bool furnished = false, string storageDirectory = null)
        {
            _exit = exit;
            Files = new WorkspaceFiles(furnished ? WorkspaceExamples.Furniture() : new WorkspaceSession(scenario),
                new ScenarioLibrary(storageDirectory ?? Path.Combine(Application.persistentDataPath, "scenarios"),
                    Path.Combine(Application.streamingAssetsPath, "Scenarios"), ScenarioFileStore.DefaultDirectory, ScenarioFileStore.LegacyDirectory));
            _shownSession = Session;
            _saveName = Session.Name;
            _document = gameObject.AddComponent<UIDocument>();
            _document.sortingOrder = 10;
            _settings = ScriptableObject.CreateInstance<PanelSettings>();
            _settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("CrowdSim/FoundationTheme");
            _settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            _document.panelSettings = _settings;
            _root = _document.rootVisualElement;
            _root.styleSheets.Add(Resources.Load<StyleSheet>("CrowdSim/Workspace"));
            _root.AddToClassList("workspace");
            _root.RegisterCallback<KeyDownEvent>(OnKey);
            GameObject view = new GameObject("Workspace detached world");
            view.transform.SetParent(transform, false);
            _world = view.AddComponent<WorkspaceWorldView>();
            _world.Initialise();
            BuildInterface();
            Draw(); Refresh();
        }

        private void BuildInterface()
        {
            VisualElement top = Box(_root, "topbar");
            _brand = Label(top, "", "brand");
            _toggleInspector = Button(top, "Hide inspector (F4)", () => { Session.ToggleInspector(); Refresh(); });
            Button(top, "Return to startup", RequestExit);
            VisualElement body = Box(_root, "workspace-body");
            _left = Box(body, "controls");
            VisualElement tabs = Box(_left, "tabs");
            foreach (string tab in new[] { "Build / Run", "Camera", "Files" })
            {
                string name = tab;
                Button(tabs, name, () => { _activePage = name; BuildPage(); });
            }
            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical); scroll.AddToClassList("page-scroll"); _left.Add(scroll);
            _page = scroll;
            _viewport = Box(body, "viewport");
            _viewport.focusable = true;
            _selection = Label(_viewport, "Click the floor to place the active tool.", "viewport-label");
            _selection.pickingMode = PickingMode.Ignore;
            Button(_viewport, "Show selected agent", () => { Session.ShowAgentInspector(); Refresh(); }).AddToClassList("agent-open");
            _viewport.RegisterCallback<PointerMoveEvent>(evt => Preview(evt.position));
            _viewport.RegisterCallback<PointerEnterEvent>(evt => { _overViewport = true; });
            _viewport.RegisterCallback<PointerLeaveEvent>(evt => { _overViewport = false; _hasPointer = false; ClearPreview(); });
            _viewport.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || evt.target != _viewport) return;
                _viewport.Focus();
                if (WorldPoint(evt.position, out GroundPoint point, !Session.IsBuilding && _tool != PlacementTool.Explosion ? .85f : 0))
                    HandleWorldClick(point);
                evt.StopPropagation();
            });
            _right = Box(body, "inspector");
            VisualElement inspectorTabs = Box(_right, "tabs");
            string[] inspectorNames = { "Overview", "Hazard", "Agent", "Debug" };
            for (int i = 0; i < inspectorNames.Length; i++)
            {
                int index = i;
                Button(inspectorTabs, inspectorNames[i], () => { Session.SelectInspectorTab(index); Refresh(); });
            }
            ScrollView inspectorScroll = new ScrollView(ScrollViewMode.Vertical); inspectorScroll.AddToClassList("page-scroll"); _right.Add(inspectorScroll);
            _inspectorPage = inspectorScroll;
            _footer = Box(_root, "transport");
            VisualElement actions = Box(_footer, "actions");
            _finalise = Button(actions, "Finalise scene", () => { if (Files.Finalise()) _tool = PlacementTool.Select; _operationStatus = Files.Message; ClearPreview(); Refresh(); });
            _transport = Button(actions, "Start", () => { _operationStatus = ""; Session.ToggleRunning(); ClearPreview(); Refresh(); });
            _arm = Button(actions, "Arm Explosion", () => { _operationStatus = ""; _tool = PlacementTool.Explosion; Refresh(); });
            _replay = Button(actions, "Replay from start", () =>
            {
                if (Files.Replay()) _tool = PlacementTool.Select;
                _operationStatus = Files.Message; ClearPreview(); Draw(); Refresh();
            });
            _releaseFume = Button(actions, "Release saved Fume", () => { Session.ReleaseSavedFume(); _operationStatus = ""; Refresh(); });
            _speed = new Slider("Playback speed", .25f, 4f) { value = 1, showInputField = true, name = "playback-speed" };
            _speed.RegisterValueChangedCallback(evt =>
            {
                if (float.IsFinite(evt.newValue)) Session.SetSpeed(Mathf.Clamp(evt.newValue, .25f, 4));
                _speed.SetValueWithoutNotify((float)Session.Speed);
            });
            actions.Add(_speed);
            _readiness = Label(_footer, "", "readiness");
            _status = Label(_footer, "", "status");
            BuildPage();
        }

        private void BuildPage()
        {
            _page.Clear(); _behaviour.Clear(); _builder = null;
            if (_activePage == "Camera")
            {
                Label(_page, "Camera", "heading");
                CameraSlider("Azimuth", -180, 180, _world.Azimuth, value => _world.Azimuth = value);
                CameraSlider("Elevation", 18, 70, _world.Elevation, value => _world.Elevation = value);
                CameraSlider("Zoom", .45f, 2.5f, _world.Zoom, value => _world.Zoom = value);
                Button(_page, "Reset camera", () => { _world.ResetCamera(); BuildPage(); });
                Label(_page, "Follow and head-height POV arrive in M8.", "muted");
                return;
            }
            if (_activePage == "Files")
            {
                BuildFilesPage();
                return;
            }
            Label(_page, "Build / Run", "heading");
            _metrics = Label(_page, "", "metrics");
            _builder = Box(_page, "builder");
            _width = new FloatField("Room width (m)") { value = Session.Frame.Width, isDelayed = true, name = "room-width" };
            _length = new FloatField("Room length (m)") { value = Session.Frame.Length, isDelayed = true, name = "room-length" };
            _builder.Add(_width); _builder.Add(_length);
            Button(_builder, "Apply room size", () => { Session.Resize(_width.value, _length.value); Refresh(); });
            Label(_builder, "10–160 m wide / 10–120 m long. Size locks after the first placement; undo all placements to unlock.", "muted");
            DropdownField exits = new DropdownField("Exit capacity", new List<string> { "1 person", "3 people", "5 people" }, _exitWidth == 1 ? 0 : _exitWidth == 3 ? 1 : 2);
            exits.RegisterValueChangedCallback(evt => { _exitWidth = new[] { 1, 3, 5 }[exits.index]; _tool = PlacementTool.Exit; ClearPreview(); Refresh(); }); _builder.Add(exits);
            Button(_builder, "Place exit", () => SetTool(PlacementTool.Exit));
            List<string> assetNames = BuilderAssetSelection.Options.Select(asset => BuilderAssetSelection.Label(asset, 1.2f)).ToList();
            DropdownField assets = new DropdownField("Asset", assetNames, (int)Session.SelectedAsset) { name = "asset-picker" };
            assets.RegisterValueChangedCallback(evt =>
            {
                Session.SelectAsset((BuilderAsset)assets.index);
                _tool = PlacementTool.Asset;
                BuildAssetControls(); ClearPreview(); Refresh(); Repreview();
            });
            _builder.Add(assets);
            _assetHint = Label(_builder, Session.AssetHint, "muted");
            _assetControls = Box(_builder, "asset-controls");
            _placeAsset = Button(_builder, "Place selected asset", () => { SetTool(PlacementTool.Asset); Repreview(); });
            _placeAsset.name = "place-asset";
            BuildAssetControls();
            DropdownField people = new DropdownField("People per click", new List<string> { "1", "5", "15", "50" }, Array.IndexOf(new[] { 1, 5, 15, 50 }, _people));
            people.RegisterValueChangedCallback(evt => { _people = new[] { 1, 5, 15, 50 }[people.index]; SetTool(PlacementTool.Agents); }); _builder.Add(people);
            Button(_builder, "Place people", () => SetTool(PlacementTool.Agents));
            _undo = Button(_builder, "Undo last placement (Ctrl+Z)", () => { Session.Undo(); ClearPreview(); Refresh(); });
            _hybrid = new Toggle("Hybrid crowd mode") { value = Session.Hybrid };
            _hybrid.RegisterValueChangedCallback(evt => { Session.SetHybrid(evt.newValue); Refresh(); }); _builder.Add(_hybrid);
            Foldout tuning = new Foldout { text = "Behaviour tuning · 14 controls", value = false }; _builder.Add(tuning);
            for (int i = 0; i < BehaviourControls.All.Count; i++)
            {
                int index = i;
                BehaviourControl control = BehaviourControls.All[i];
                Slider slider = new Slider(control.Label, control.Minimum, control.Maximum)
                { value = Session.BehaviourValues[i], showInputField = true, name = "behaviour-" + i };
                slider.RegisterValueChangedCallback(evt =>
                {
                    if (float.IsFinite(evt.newValue)) Session.SetBehaviour(index, evt.newValue);
                    slider.SetValueWithoutNotify(Session.BehaviourValues[index]);
                    Refresh();
                });
                tuning.Add(slider); _behaviour.Add(slider);
            }
            Button(tuning, "Reset behaviour defaults", () =>
            {
                Session.ResetBehaviour();
                for (int i = 0; i < _behaviour.Count; i++) _behaviour[i].SetValueWithoutNotify(Session.BehaviourValues[i]);
                Refresh();
            });
            Label(tuning, "Higher stress persistence slows recovery. Compression affects social spacing; physical guards remain active. Running speed is a ceiling, not a target.", "muted");
            Label(_page, "Occupied furniture contributes to the planned population. Full hazard controls arrive in M6/M7. Select runtime agents by clicking their capsules.", "muted");
            Refresh();
        }

        private void BuildAssetControls()
        {
            _assetControls.Clear(); _angleSlider = null; _exactAngle = null;
            if (Session.AssetSupportsRotation)
            {
                _angleSlider = new Slider(Session.AssetUsesQuarterTurns ? "Facing (quarter turns)" : "Facing (degrees)", 0, 359.9f)
                { value = Session.AssetRotationDegrees, name = "asset-angle-slider" };
                _angleSlider.RegisterValueChangedCallback(evt => SetRotation(evt.newValue)); _assetControls.Add(_angleSlider);
                _exactAngle = new FloatField("Exact angle (degrees)")
                { value = Session.AssetRotationDegrees, isDelayed = true, name = "asset-angle-exact" };
                _exactAngle.RegisterValueChangedCallback(evt => SetRotation(evt.newValue)); _assetControls.Add(_exactAngle);
                if (Session.SelectedAsset == BuilderAsset.Barricade)
                {
                    Button(_assetControls, "-15 degrees", () => RotateSelectedAsset(true));
                    Button(_assetControls, "+15 degrees", () => RotateSelectedAsset());
                    Label(_assetControls, "R: +15° / Shift+R: -15° over the viewport. 0° along X, 90° along Y.", "muted");
                }
                else
                {
                    Button(_assetControls, "Rotate 90 degrees (R)", () => RotateSelectedAsset());
                    Label(_assetControls, Session.AssetUsesQuarterTurns ? "Table/stage angles snap to quarter turns." : "Occupied sets/chairs retain exact facing; R rotates 90°.", "muted");
                }
            }
            if (Session.SelectedAsset == BuilderAsset.Barricade)
            {
                Toggle snap = new Toggle("Snap barricades end-to-end") { value = Session.SnapBarricadeEnds, name = "barricade-snap" };
                snap.RegisterValueChangedCallback(evt => { Session.SetBarricadeSnap(evt.newValue); ClearPreview(); Repreview(); }); _assetControls.Add(snap);
            }
        }
        private void RotateSelectedAsset(bool reverse = false)
        {
            if (Session.RotateAsset(reverse)) _tool = PlacementTool.Asset;
            Refresh(); ClearPreview(); Repreview();
        }
        private void SetRotation(float degrees)
        {
            if (float.IsFinite(degrees)) { Session.SetAssetRotation(degrees); _tool = PlacementTool.Asset; }
            Refresh(); ClearPreview(); Repreview();
        }
        private void Repreview()
        {
            if (!_hasPointer || !_overViewport) return;
            _lastPreviewTime = -100; Preview(_pointerPosition);
        }

        private void CameraSlider(string name, float min, float max, float initial, Action<float> changed)
        {
            Slider slider = new Slider(name, min, max) { value = initial, showInputField = true };
            slider.RegisterValueChangedCallback(evt =>
            {
                if (float.IsFinite(evt.newValue)) changed(Mathf.Clamp(evt.newValue, min, max));
                else slider.SetValueWithoutNotify(initial);
            }); _page.Add(slider);
        }
        private void SetTool(PlacementTool tool) { _operationStatus = ""; _tool = tool; ClearPreview(); Refresh(); }
        private int Option => _tool == PlacementTool.Exit ? _exitWidth : _people;
        public void HandleWorldClick(GroundPoint point)
        {
            if (_modal != null) return;
            _operationStatus = "";
            if (Session.IsBuilding)
            {
                if (_tool != PlacementTool.Select) Session.Place(_tool, point, Option);
            }
            else if (_tool == PlacementTool.Explosion)
            {
                if (Session.TriggerExplosion(point)) _tool = PlacementTool.Select;
            }
            else Session.SelectAgent(point);
            ClearPreview(); Draw(); Refresh();
        }

        private bool WorldPoint(Vector3 panelPoint, out GroundPoint ground, float height = 0)
        {
            float panelHeight = Mathf.Max(1, _root.resolvedStyle.height), panelWidth = Mathf.Max(1, _root.resolvedStyle.width);
            Vector2 screen = new Vector2(panelPoint.x * Screen.width / panelWidth, (panelHeight - panelPoint.y) * Screen.height / panelHeight);
            return _world.GroundPoint(screen, out ground, height);
        }
        private void Preview(Vector3 point)
        {
            _pointerPosition = point; _hasPointer = true;
            if (_tool == PlacementTool.Select) { ClearPreview(); return; }
            if (!Session.IsBuilding || Time.unscaledTime - _lastPreviewTime < .08f) return;
            _lastPreviewTime = Time.unscaledTime;
            if (!WorldPoint(point, out GroundPoint ground)) { ClearPreview(); return; }
            PlacementPreview preview = Session.Preview(_tool, ground, Option);
            if (preview.Bodies.Count > 0) _world.Draw(preview.Bodies, "preview-", preview.Valid ? new Color(.2f, 1, .7f) : Color.red);
            else _world.Draw(new[] { new ViewBody("invalid", ViewShape.Box, ViewKind.Block,
                new ViewVector(ground.X, .06f, ground.Y), new ViewVector(.7f, .12f, .7f)) }, "preview-", Color.red);
            _selection.text = (_tool == PlacementTool.Asset ? Session.SelectedAsset + " " + Session.AssetRotationDegrees.ToString("F1") + "°" : _tool.ToString()) + " · " + ground.X.ToString("F1") + ", " + ground.Y.ToString("F1") + " m · " + (preview.Valid ? "valid preview" : "placement rejected");
        }
        private void ClearPreview() { if (_world != null) _world.Draw(Array.Empty<ViewBody>(), "preview-"); }

        private void Update()
        {
            if (Session == null) return;
            try
            {
                if (_modal == null && Session.Advance(Time.unscaledDeltaTime) > 0) Refresh();
                if (_drawnRevision != Session.Revision || _drawnGrid != Session.ShowGrid) Draw();
                Layout();
            }
            catch (Exception exception)
            {
                if (Session.Running) Session.ToggleRunning();
                _status.text = "Run failed: " + exception.Message;
                Debug.LogException(exception);
                enabled = false;
            }
        }
        private void Layout()
        {
            float width = _root.resolvedStyle.width, height = _root.resolvedStyle.height;
            if (!float.IsFinite(width) || !float.IsFinite(height) || width <= 0 || height <= 0) return;
            DashboardLayout layout = DashboardLayout.Calculate(width, Session.InspectorVisible, 8);
            _left.style.width = layout.ControlsWidth;
            _viewport.style.marginLeft = layout.Gap;
            _viewport.style.marginRight = Session.InspectorVisible ? layout.Gap : 0;
            _right.style.width = layout.InspectorWidth;
            _right.style.display = Session.InspectorVisible ? DisplayStyle.Flex : DisplayStyle.None;
            Rect rect = _viewport.worldBound;
            if (rect.width <= 0 || rect.height <= 0) return;
            _world.Fit(Session.Frame.Width, Session.Frame.Length,
                new Rect(rect.x / width, 1 - rect.yMax / height, rect.width / width, rect.height / height));
        }
        private void Draw()
        {
            _world.Draw(Session.Frame.Bodies);
            if (_drawnGrid != Session.ShowGrid || _drawnRevision < 0 || Session.IsBuilding)
            {
                _grid.Clear();
                if (Session.ShowGrid)
                {
                    for (float x = 0; x <= Session.Frame.Width; x += 1.2f)
                        _grid.Add(new ViewBody("x-" + x, ViewShape.Box, ViewKind.Block,
                            new ViewVector(x, .01f, Session.Frame.Length / 2), new ViewVector(.025f, .015f, Session.Frame.Length)));
                    for (float z = 0; z <= Session.Frame.Length; z += 1.2f)
                        _grid.Add(new ViewBody("z-" + z, ViewShape.Box, ViewKind.Block,
                            new ViewVector(Session.Frame.Width / 2, .01f, z), new ViewVector(Session.Frame.Width, .015f, .025f)));
                }
                _world.Draw(_grid, "grid-", new Color(.42f, .5f, .56f));
            }
            ViewBody selected = Session.Frame.Bodies.FirstOrDefault(b => b.Id == "agent-" + Session.SelectedAgentId);
            _world.Draw(selected == null ? Array.Empty<ViewBody>() : new[] {
                new ViewBody("agent", ViewShape.Cylinder, ViewKind.Agent, new ViewVector(selected.Position.X, .04f, selected.Position.Z), new ViewVector(.9f, .06f, .9f)) }, "selection-", Color.cyan);
            _drawnRevision = Session.Revision; _drawnGrid = Session.ShowGrid;
        }
        private void Refresh()
        {
            if (_status == null) return;
            _status.text = string.IsNullOrEmpty(_operationStatus) ? Session.Message : _operationStatus;
            string displayName = Session.Name.Length > 32 ? Session.Name.Substring(0, 32) + "…" : Session.Name;
            _brand.text = "CROWD / SIM · " + Session.Scenario + " · " + displayName + (Session.IsDirty ? " *" : "") + " · M5";
            _replay.SetEnabled(Session.CanReplay);
            _releaseFume.style.display = Session.Scenario == ScenarioType.ToxicFume ? DisplayStyle.Flex : DisplayStyle.None;
            _releaseFume.SetEnabled(Session.Running && Session.HasFumeCache && !Session.FumeReleased);
            UpdateFilesState();
            _readiness.text = Session.Readiness;
            _finalise.SetEnabled(Session.CanFinalise);
            _transport.SetEnabled(!Session.IsBuilding);
            _transport.text = Session.Running ? "Pause" : Session.Phase == WorkspacePhase.Ready ? "Start" : "Resume";
            _arm.SetEnabled(Session.Running && Session.Scenario == ScenarioType.Explosion && !Session.Frame.HazardReleased);
            _toggleInspector.text = Session.InspectorVisible ? "Hide inspector (F4)" : "Show inspector (F4)";
            _selection.text = Session.IsBuilding ? "Tool: " + _tool + " · green preview / red rejection · click floor" : _tool == PlacementTool.Explosion ? "Explosion armed · click inside the room once" : "Click a capsule to select · Show selected agent opens details";
            if (_activePage == "Build / Run")
            {
                _metrics.text = (Session.UsesRandomStarts && Session.IsBuilding ? "Legacy random starts: " + Session.PlannedCount + " on finalisation\n" : "") + Session.Phase + " · tick " + Session.Tick + " · " + (Session.Tick * Session.StepSeconds).ToString("F2") + " s\n" +
                    Session.ExitCount + " exits · " + Session.PlannedCount + " planned · " + Session.Frame.Exited + " evacuated\n" +
                    Session.FurnitureCount + " furniture sets · seated " + Session.Frame.SeatedAgents + " · rising " + Session.Frame.RisingAgents;
                _builder.SetEnabled(Session.IsBuilding);
                _width.SetEnabled(Session.CanResize); _length.SetEnabled(Session.CanResize);
                _placeAsset.SetEnabled(Session.IsBuilding && !Session.AssetUnavailable);
                _assetControls.SetEnabled(Session.IsBuilding && !Session.AssetUnavailable);
                _assetHint.text = Session.AssetUnavailable ? "Stage already placed (1/1). Undo its placement before replacing it." : Session.AssetHint;
                _angleSlider?.SetValueWithoutNotify(Session.AssetRotationDegrees);
                _exactAngle?.SetValueWithoutNotify(Session.AssetRotationDegrees);
            }
            BuildInspector();
            Layout();
        }
        private void BuildInspector()
        {
            // Keep the controls alive while running; rebuild only on destination changes.
            int tab = Session.InspectorTab;
            string tag = "inspector-" + tab;
            if (_inspectorPage.name != tag)
            {
                _inspectorPage.Clear(); _inspectorPage.name = tag;
                Label(_inspectorPage, new[] { "Overview", "Hazard", "Selected agent", "Debug" }[tab], "heading");
                _inspectorText = Label(_inspectorPage, "", "metrics");
                if (tab == 3)
                {
                    Toggle grid = new Toggle("Show room grid") { value = Session.ShowGrid };
                    grid.RegisterValueChangedCallback(evt => { Session.ShowGrid = evt.newValue; }); _inspectorPage.Add(grid);
                    Label(_inspectorPage, "Overlay flags persist while the inspector is hidden. Force/vision/trail overlays arrive in M9.", "muted");
                }
            }
            _inspectorText.text = tab == 0 ? "Tick " + Session.Tick + "\nPopulation " + (Session.IsBuilding ? Session.PlannedCount : Session.Frame.Population) +
                "\nSeated " + Session.Frame.SeatedAgents + " · rising " + Session.Frame.RisingAgents +
                "\nFurniture airborne " + Session.Frame.FurnitureBodies.Count(b => b.State == ChairState.Airborne) +
                " · fallen " + Session.Frame.FurnitureBodies.Count(b => b.State == ChairState.Fallen) +
                " · destroyed " + Session.Frame.FurnitureBodies.Count(b => b.State == ChairState.Destroyed) +
                "\nEvacuated " + Session.Frame.Exited + "\nInjured " + Session.Frame.Injured + "\nDead " + Session.Frame.Dead + "\nHybrid " + Session.Hybrid
                : tab == 1 ? "Hazard: " + Session.Scenario + "\n" + (Session.Frame.HazardReleased ? "Triggered" : "Waiting") +
                    (Session.Scenario == ScenarioType.Explosion ? "\nExplosion: start, arm, then click the room. Detailed controls arrive in M6."
                        : "\nSaved Fume: finalise to precompute, start, then Release saved Fume. Cache ready: " + Session.HasFumeCache + ". Full authoring arrives in M7.")
                : tab == 2 ? Session.SelectedAgentSummary : "Grid spacing: 1.2 m\nFixed simulation step: " + Session.StepSeconds.ToString("G") + " s\nSelection: " + (Session.SelectedAgentId?.ToString() ?? "none");
        }
        private void OnKey(KeyDownEvent evt)
        {
            if (_modal != null) return;
            _operationStatus = "";
            bool editing = false;
            VisualElement focus = _root.focusController?.focusedElement as VisualElement;
            while (focus != null)
            {
                if (focus is TextField || focus is FloatField || focus is IntegerField || focus is DoubleField ||
                    focus.ClassListContains("unity-base-text-field__input")) editing = true;
                focus = focus.parent;
            }
            WorkspaceShortcut command = WorkspaceShortcuts.Resolve(evt.keyCode.ToString(), evt.ctrlKey || evt.commandKey, evt.altKey, editing);
            switch (command)
            {
                case WorkspaceShortcut.ToggleInspector: Session.ToggleInspector(); break;
                case WorkspaceShortcut.Undo: Session.Undo(); break;
                case WorkspaceShortcut.ToggleRunning: Session.ToggleRunning(); break;
                case WorkspaceShortcut.CancelTool: _tool = PlacementTool.Select; break;
                case WorkspaceShortcut.RotateAsset:
                    if (_tool != PlacementTool.Asset || !Session.IsBuilding || !_overViewport) return;
                    Session.RotateAsset(evt.shiftKey); break;
                default: return;
            }
            evt.StopPropagation(); evt.PreventDefault(); ClearPreview(); Refresh(); Repreview();
        }
        private void RequestExit() { Files.Leave(false); SyncFiles(); }
        private void ShowExitPrompt(bool diagnostics) { Files.Leave(diagnostics); SyncFiles(); }
        private static VisualElement Box(VisualElement parent, string css)
        { VisualElement box = new VisualElement(); box.AddToClassList(css); parent.Add(box); return box; }
        private static Label Label(VisualElement parent, string text, string css)
        { Label label = new Label(text); label.AddToClassList(css); parent.Add(label); return label; }
        private static Button Button(VisualElement parent, string text, Action action)
        { Button button = new Button(action) { text = text }; parent.Add(button); return button; }
        private void OnDestroy()
        {
            if (_document != null) _document.panelSettings = null;
            if (_settings != null) Destroy(_settings);
        }
    }
}
