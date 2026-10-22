using System;
using System.Collections;
using System.IO;
using CrowdSim.Migration;
using CrowdSim.Core.Model;
using CrowdSim.Unity.Workspace;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace CrowdSim.Unity.Foundation
{
    /// <summary>Startup and scenario entry for the accepted diagnostics and common workspace.</summary>
    [DisallowMultipleComponent]
    public sealed class BootstrapController : MonoBehaviour
    {
        private UIDocument _document;
        private PanelSettings _panel;
        private VisualElement _root;
        private Action _showExplosion;
        private Action _showFume;
        private Action _reset;
        private Action _check;
        private Action _openParity;
        private Action _openExplosion;
        private Action _openFurnished;
        private Action _openFume;
        private Action _openFumeExample;
        private Action _quit, _guide, _notices;
        private bool _quitApproved;
        private GameObject _workspace;

        /// <summary>The currently displayed scenario overview, for integration tests.</summary>
        public string SelectedOverview { get; private set; } = "Welcome";

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null)
            {
                _document = gameObject.AddComponent<UIDocument>();
            }

            ThemeStyleSheet theme = Resources.Load<ThemeStyleSheet>("CrowdSim/FoundationTheme");
            VisualTreeAsset layout = Resources.Load<VisualTreeAsset>("CrowdSim/Startup");
            if (theme == null || layout == null)
            {
                Debug.LogError("CrowdSim: startup layout or runtime theme is missing.");
                enabled = false;
                return;
            }

            _panel = ScriptableObject.CreateInstance<PanelSettings>();
            _panel.name = "CrowdSim runtime panel";
            _panel.themeStyleSheet = theme;
            _panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _panel.referenceResolution = new Vector2Int(1440, 900);
            _panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            _panel.match = 0.5f;
            _document.panelSettings = _panel;
            _document.visualTreeAsset = layout;
            _root = _document.rootVisualElement;
            _root.style.unityFontDefinition = FontDefinition.FromFont(Resources.Load<Font>("CrowdSim/Fonts/AtkinsonHyperlegible-Regular"));
            _root.style.display = DisplayStyle.Flex;

            _showExplosion = () => ShowOverview("Explosion",
                "Shock, debris, shielding and crowd response",
                "Tune five zone radii, run a one-shot event, and inspect shock, debris, shielding and evacuation. Adjust overlays without changing physical outcomes.");
            _showFume = () => ShowOverview("Toxic Fume",
                "Precomputed plume, exposure and evacuation",
                "Place a source, compute dispersion, preview the timeline, then start and release. Inspect concentration and cumulative exposure as the crowd responds.");
            _reset = ResetOverview;
            _check = RunStartupChecks;
            _openParity = () => SceneManager.LoadScene("SimulationParity");
            _root.Q<Button>("explosion-overview").clicked += _showExplosion;
            _root.Q<Button>("fume-overview").clicked += _showFume;
            _root.Q<Button>("reset-overview").clicked += _reset;
            _root.Q<Button>("run-checks").clicked += _check;
            _root.Q<Button>("open-parity").clicked += _openParity;
            _openExplosion = () => OpenWorkspace(ScenarioType.Explosion);
            _openFurnished = () => OpenWorkspace(ScenarioType.Explosion, true);
            _openFume = () => OpenWorkspace(ScenarioType.ToxicFume);
            _root.Q<Button>("open-explosion-workspace").clicked += _openExplosion;
            _root.Q<Button>("open-furnished-workspace").clicked += _openFurnished;
            _root.Q<Button>("open-fume-workspace").clicked += _openFume;
            _openFumeExample = () =>
            {
                OpenWorkspace(ScenarioType.ToxicFume);
                _workspace.GetComponent<WorkspaceController>().OpenScenarioFile(Path.Combine(Application.streamingAssetsPath, "Scenarios", "toxic_fume_205.json"));
            };
            _root.Q<Button>("open-fume-example").clicked += _openFumeExample;
            _quit = RequestQuit; _guide = () => OpenDocumentation("USER_GUIDE.txt");
            _notices = () => OpenDocumentation("THIRD_PARTY_NOTICES.txt");
            _root.Q<Button>("quit-application").clicked += _quit;
            _root.Q<Button>("open-guide").clicked += _guide;
            _root.Q<Button>("open-notices").clicked += _notices;
            _root.Q<Label>("editor-version").text = (Application.isEditor ? "EDITOR  " : "PLAYER  ") + Application.unityVersion + " · v" + Application.version;
            _root.Q<Label>("milestone-label").text = MigrationContract.Milestone;
            _quitApproved = false;
            Application.wantsToQuit += WantsToQuit;
            ResetOverview();
            StartCoroutine(CheckAfterFirstFrame());
        }

        private void OpenWorkspace(ScenarioType scenario, bool furnished = false)
        {
            if (_workspace != null) return;
            _root.style.display = DisplayStyle.None;
            _workspace = new GameObject("CrowdSim workspace");
            // Keep this a scene root: nested UIDocuments inherit their parent's panel
            // and would disappear when the startup document is hidden. OnDisable owns cleanup.
            _workspace.AddComponent<WorkspaceController>().Open(scenario, () =>
            {
                if (_root != null) _root.style.display = DisplayStyle.Flex;
            }, furnished, quit: RequestQuit);
        }

        private void OpenDocumentation(string name)
        {
            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, "Documentation", name);
                if (!File.Exists(path)) throw new FileNotFoundException("Bundled document is missing.", name);
                Application.OpenURL(new Uri(path).AbsoluteUri);
            }
            catch (Exception exception) { _root.Q<Label>("setup-status").text = "Cannot open document: " + exception.Message; }
        }

        private bool WantsToQuit()
        {
            if (_quitApproved) return true;
            RequestQuit(); return false;
        }

        private void RequestQuit()
        {
            if (_quitApproved) return;
            if (_workspace != null) _workspace.GetComponent<WorkspaceController>().RequestQuit(ApproveQuit);
            else ApproveQuit();
        }

        private void ApproveQuit()
        {
            if (_quitApproved) return;
            _quitApproved = true;
            // Exit on the next frame, outside an OS wantsToQuit callback or UI dispatch.
            StartCoroutine(ExitAfterDecision());
        }

        private IEnumerator ExitAfterDecision()
        {
            yield return null;
#if UNITY_EDITOR
            global::UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private IEnumerator CheckAfterFirstFrame()
        {
            yield return null;
            RunStartupChecks();
        }

        private void ResetOverview()
        {
            ShowOverview("Welcome", "A new home for the same simulation",
                "Choose a scenario or open a furnished example. Build your layout, configure an event, then run and inspect individual responses. Save your experiment and replay from the original starts.");
        }

        private void ShowOverview(string scenario, string title, string description)
        {
            SelectedOverview = scenario;
            _root.Q<Label>("overview-kicker").text = scenario.ToUpperInvariant();
            _root.Q<Label>("overview-title").text = title;
            _root.Q<Label>("overview-description").text = description;
            _root.Q<Button>("explosion-overview").EnableInClassList("selected", scenario == "Explosion");
            _root.Q<Button>("fume-overview").EnableInClassList("selected", scenario == "Toxic Fume");
        }

        private void RunStartupChecks()
        {
            bool editorOk = MigrationContract.IsExpectedEditor(Application.unityVersion);
            bool pipelineOk = GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;
            bool samplesOk = File.Exists(Path.Combine(Application.streamingAssetsPath, "Scenarios", "legacy_v2.json")) &&
                File.Exists(Path.Combine(Application.streamingAssetsPath, "Scenarios", "toxic_fume_205.json")) &&
                File.Exists(Path.Combine(Application.streamingAssetsPath, "Documentation", "USER_GUIDE.txt")) &&
                File.Exists(Path.Combine(Application.streamingAssetsPath, "Documentation", "THIRD_PARTY_NOTICES.txt"));
            _root.Q<Label>("editor-check").text = (editorOk ? "PASS" : "CHECK") + "  Editor " + MigrationContract.EditorVersion;
            _root.Q<Label>("pipeline-check").text = (pipelineOk ? "PASS" : "CHECK") + "  Universal Render Pipeline";
            _root.Q<Label>("samples-check").text = (samplesOk ? "PASS" : "CHECK") + "  Bundled examples, guide and notices";
            _root.Q<Label>("setup-status").text = editorOk && pipelineOk && samplesOk
                ? "Project ready. Choose a scenario or an example to begin."
                : "Check the setup items above before running diagnostics.";
        }

        private void OnDisable()
        {
            Application.wantsToQuit -= WantsToQuit;
            StopAllCoroutines();
            if (_workspace != null) Destroy(_workspace);
            if (_root != null)
            {
                _root.Q<Button>("explosion-overview").clicked -= _showExplosion;
                _root.Q<Button>("fume-overview").clicked -= _showFume;
                _root.Q<Button>("reset-overview").clicked -= _reset;
                _root.Q<Button>("run-checks").clicked -= _check;
                _root.Q<Button>("open-parity").clicked -= _openParity;
                _root.Q<Button>("open-explosion-workspace").clicked -= _openExplosion;
                _root.Q<Button>("open-furnished-workspace").clicked -= _openFurnished;
                _root.Q<Button>("open-fume-workspace").clicked -= _openFume;
                _root.Q<Button>("open-fume-example").clicked -= _openFumeExample;
                _root.Q<Button>("quit-application").clicked -= _quit;
                _root.Q<Button>("open-guide").clicked -= _guide;
                _root.Q<Button>("open-notices").clicked -= _notices;
            }
            if (_document != null)
            {
                _document.panelSettings = null;
            }
            if (_panel != null)
            {
                Destroy(_panel);
            }
            _root = null;
        }
    }
}
