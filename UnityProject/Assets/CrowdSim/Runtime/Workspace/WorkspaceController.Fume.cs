using System;
using CrowdSim.Core.Model;
using CrowdSim.Diagnostics;
using CrowdSim.Workspace;
using UnityEngine;
using UnityEngine.UIElements;

namespace CrowdSim.Unity.Workspace
{
    public sealed partial class WorkspaceController
    {
        private WorkspaceFumeView _fumeView;
        private readonly Slider[] _fumeFields = new Slider[10];
        private Slider _fumeTimeline;
        private Label _fumeState, _fumeCacheMetrics, _fumeExposureMetrics, _fumeQuickStatus;
        private VisualElement _fumeSettings;
        private Button _fumeSourceButton, _computeFume, _resetFume;
        private bool _syncFume;
        private void OpenFumeSetup()
        {
            Session.SelectInspectorTab(1); if (!Session.InspectorVisible) Session.ToggleInspector(); Refresh();
        }
        private void BuildFumeShortcut()
        {
            VisualElement card = Box(_page, "workflow-card");
            Label(card, "TOXIC FUME / SETUP", "eyebrow");
            _fumeQuickStatus = Label(card, Session.FumeReadiness, "muted");
            Button(card, "Set release point", () => { SetTool(PlacementTool.FumeSource); OpenFumeSetup(); }).name = "fume-source-shortcut";
            Button(card, "Fume setup →", OpenFumeSetup).AddToClassList("primary-button");
        }
        private void BuildFumeInspector()
        {
            Label(_inspectorPage, "Toxic Fume", "heading");
            Label(_inspectorPage, "01 Source   /   02 Compute   /   03 Preview   /   04 Release", "workflow-steps");
            _fumeState = Label(_inspectorPage, "", "state-card"); _fumeState.name = "fume-readiness";
            _fumeSourceButton = Button(_inspectorPage, "Set / move release point", () => SetTool(PlacementTool.FumeSource));
            _fumeSourceButton.name = "place-fume-source";
            _fumeSettings = Box(_inspectorPage, "fume-settings");
            Label(_fumeSettings, "Dispersion settings", "section-title");
            Foldout advanced = new Foldout { text = "Advanced • drift and exposure", value = false };
            for (int i = 0; i < 10; i++)
            {
                int index = i; FumeControl control = FumeControls.All[i];
                Slider slider = new Slider(control.Label, control.Minimum, control.Maximum)
                { value = Session.FumeValues[i], showInputField = true, name = "fume-setting-" + i, tooltip = control.Help };
                slider.RegisterValueChangedCallback(evt =>
                {
                    if (_syncFume) return;
                    Session.SetFumeValue(index, evt.newValue); _operationStatus = ""; Refresh(); Draw();
                });
                (i < 4 ? _fumeSettings : advanced).Add(slider); _fumeFields[i] = slider;
            }
            _fumeSettings.Add(advanced);
            _resetFume = Button(_fumeSettings, "Reset Fume Defaults", () => { Session.ResetFumeDefaults(); _operationStatus = ""; Refresh(); Draw(); });
            _computeFume = Button(_inspectorPage, "Compute dispersion", () => { Session.ComputeFume(); _operationStatus = ""; ClearPreview(); Refresh(); Draw(); });
            _computeFume.name = "compute-fume"; _computeFume.AddToClassList("primary-button");
            _fumeCacheMetrics = Label(_inspectorPage, "", "data-card"); _fumeCacheMetrics.name = "fume-cache-metrics";
            Label(_inspectorPage, "Preview timeline", "section-title");
            _fumeTimeline = new Slider("Seconds after release", 0, Math.Max(10, Session.Fume.PreviewMaximum))
            { value = Session.Fume.DisplaySeconds, showInputField = true, name = "fume-preview" };
            _fumeTimeline.RegisterValueChangedCallback(evt =>
            {
                if (_syncFume) return;
                Session.SetFumePreview(evt.newValue); _operationStatus = ""; Refresh(); Draw();
            }); _inspectorPage.Add(_fumeTimeline);
            Button(_inspectorPage, "Reset preview", () => { Session.SetFumePreview(0); _operationStatus = ""; Refresh(); Draw(); }).name = "reset-fume-preview";
            Label(_inspectorPage, "Preview changes the view only. People remain at their starts and accumulate no exposure. Finalise locks setup; Start then Release Toxic Fume begins the event.", "muted");
            Toggle plume = new Toggle("Show concentration plume") { value = Session.ShowFume, name = "show-fume" };
            plume.RegisterValueChangedCallback(evt => { Session.ShowFume = evt.newValue; _fumeView.Draw(Session); }); _inspectorPage.Add(plume);
            Toggle grid = new Toggle("Show fume grid cells") { value = Session.ShowFumeGrid, name = "show-fume-grid" };
            grid.RegisterValueChangedCallback(evt => { Session.ShowFumeGrid = evt.newValue; _fumeView.Draw(Session); }); _inspectorPage.Add(grid);
            Label(_inspectorPage, "CONCENTRATION  0 → 1", "eyebrow");
            VisualElement legend = Box(_inspectorPage, "concentration-legend");
            for (int i = 0; i < 8; i++)
            {
                float c = i / 7f; VisualElement swatch = new VisualElement(); swatch.AddToClassList("legend-swatch");
                swatch.style.backgroundColor = new Color(.18f + .58f * c, .82f - .18f * c, .30f - .12f * c); legend.Add(swatch);
            }
            Label(_inspectorPage, "Exposure and response", "section-title");
            _fumeExposureMetrics = Label(_inspectorPage, "", "data-card"); _fumeExposureMetrics.name = "fume-exposure-metrics";
            Foldout notes = new Foldout { text = "About this model" };
            Label(notes, "An obstacle-aware, normalized field on a 0.50 m grid. Fixed post-release decay: 24 s. Concentration and exposure are generic simulator values, not chemical limits or CFD. Incapacitation is permanent until replay.", "muted"); _inspectorPage.Add(notes);
            UpdateFumeInspector();
        }
        private void UpdateFumeInspector()
        {
            if (_fumeQuickStatus != null && _activePage == "Build / Run" && Session.Scenario == ScenarioType.ToxicFume)
                _fumeQuickStatus.text = Session.FumeReadiness;
            if (Session.InspectorTab != 1 || Session.Scenario != ScenarioType.ToxicFume || _fumeState == null) return;
            FumeFrame frame = Session.Fume;
            _syncFume = true;
            _fumeSettings.SetEnabled(Session.CanEditFume); _fumeSourceButton.SetEnabled(Session.CanEditFume);
            _fumeSourceButton.EnableInClassList("tool-active", _tool == PlacementTool.FumeSource);
            _computeFume.SetEnabled(Session.CanComputeFume); _resetFume.SetEnabled(Session.CanEditFume);
            for (int i = 0; i < 10; i++) _fumeFields[i].SetValueWithoutNotify(Session.FumeValues[i]);
            _fumeFields[2].lowValue = Math.Max(2, Session.FumeValues[0]);
            _fumeFields[7].lowValue = Math.Min(.95f, Session.FumeValues[6] + .05f);
            _fumeFields[9].lowValue = Session.FumeValues[8] + 1;
            _fumeTimeline.highValue = Math.Max(10, frame.PreviewMaximum);
            _fumeTimeline.SetValueWithoutNotify(frame.DisplaySeconds); _fumeTimeline.SetEnabled(Session.CanEditFume && frame.CacheReady);
            _root.Q<Button>("reset-fume-preview")?.SetEnabled(Session.CanEditFume && frame.CacheReady);
            _fumeState.text = (frame.Released ? "RELEASE ACTIVE" : frame.CacheReady ? "FIELD READY" : "SETUP REQUIRED") + "\n" + Session.FumeReadiness +
                (frame.Source.HasValue ? "\nSource " + frame.Source.Value.X.ToString("F1") + ", " + frame.Source.Value.Y.ToString("F1") + " m" : "");
            _fumeState.EnableInClassList("is-ready", frame.CacheReady);
            _fumeCacheMetrics.text = frame.CacheReady ? frame.ReachableCells.ToString("N0") + " reachable / " + frame.GridCells.ToString("N0") + " grid cells\n" +
                frame.GridResolution.ToString("F2") + " m resolution · " + (frame.CacheBytes / 1024f).ToString("F1") + " KiB\n" +
                frame.ComputeMilliseconds.ToString("F1") + " ms compute · max arrival " + frame.MaximumArrival.ToString("F1") + " s" : "No valid field. Compute after source or layout changes.";
            _fumeExposureMetrics.text = (frame.Preview ? "PREVIEW • no physical exposure" : frame.Released ? "LIVE • " + frame.ElapsedSeconds.ToString("F1") + " s after release" : "NOT RELEASED • initial field shown") +
                "\nExposed (incl. impaired) " + frame.Exposed + " · impaired " + frame.Impaired +
                "\nIncapacitated " + frame.Incapacitated + " · high concentration " + frame.HighConcentration +
                "\nMean cumulative exposure " + frame.AverageExposure.ToString("F2") + "\nPeak concentration " + frame.PeakConcentration.ToString("F3");
            _syncFume = false;
        }
        /// <summary>Startup examples use the same transactional import workflow as Files.</summary>
        public void OpenScenarioFile(string path) { Files.Import(path); SyncFiles(); }
    }
}
