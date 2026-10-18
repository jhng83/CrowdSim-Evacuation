using System;
using System.Collections;
using System.IO;
using CrowdSim.Migration;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace CrowdSim.Unity.Foundation
{
    /// <summary>Downloadable M1 startup screen. Simulation workflows are introduced in M2 onward.</summary>
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
                Debug.LogError("CrowdSim M1: startup layout or runtime theme is missing.");
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

            _showExplosion = () => ShowOverview("Explosion",
                "Shock, debris, shielding and crowd response",
                "The original fixed-step model is preserved as the reference. " +
                "The portable engine and executable presets arrive in M2; full Explosion controls and analysis arrive in M6.");
            _showFume = () => ShowOverview("Toxic Fume",
                "Precomputed plume, exposure and evacuation",
                "The original obstacle-aware normalized fume model is the reference. " +
                "The portable engine arrives in M2; source placement, precomputation, preview and release arrive in M7.");
            _reset = ResetOverview;
            _check = RunStartupChecks;
            _root.Q<Button>("explosion-overview").clicked += _showExplosion;
            _root.Q<Button>("fume-overview").clicked += _showFume;
            _root.Q<Button>("reset-overview").clicked += _reset;
            _root.Q<Button>("run-checks").clicked += _check;
            _root.Q<Label>("editor-version").text = "EDITOR  " + Application.unityVersion;
            _root.Q<Label>("milestone-label").text = MigrationContract.Milestone;
            ResetOverview();
            StartCoroutine(CheckAfterFirstFrame());
        }

        private IEnumerator CheckAfterFirstFrame()
        {
            yield return null;
            RunStartupChecks();
        }

        private void ResetOverview()
        {
            ShowOverview("Welcome", "A new home for the same simulation",
                "Explore the two scenario overviews, check the project setup, then follow the M1 local test checklist. " +
                "This milestone provides the Unity foundation; no crowd simulation runs yet.");
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
                File.Exists(Path.Combine(Application.streamingAssetsPath, "Scenarios", "toxic_fume_205.json"));
            _root.Q<Label>("editor-check").text = (editorOk ? "PASS" : "CHECK") + "  Editor " + MigrationContract.EditorVersion;
            _root.Q<Label>("pipeline-check").text = (pipelineOk ? "PASS" : "CHECK") + "  Universal Render Pipeline";
            _root.Q<Label>("samples-check").text = (samplesOk ? "PASS" : "CHECK") + "  Two bundled reference examples";
            _root.Q<Label>("setup-status").text = editorOk && pipelineOk && samplesOk
                ? "Startup checks passed. Ready for your M1 Editor tests."
                : "Check the setup items above before accepting M1.";
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (_root != null)
            {
                _root.Q<Button>("explosion-overview").clicked -= _showExplosion;
                _root.Q<Button>("fume-overview").clicked -= _showFume;
                _root.Q<Button>("reset-overview").clicked -= _reset;
                _root.Q<Button>("run-checks").clicked -= _check;
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
