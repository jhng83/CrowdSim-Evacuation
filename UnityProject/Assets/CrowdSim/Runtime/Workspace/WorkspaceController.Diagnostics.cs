using System;
using System.Linq;
using System.Text;
using CrowdSim.Diagnostics;
using UnityEngine;
using UnityEngine.UIElements;

namespace CrowdSim.Unity.Workspace
{
    public sealed partial class WorkspaceController
    {
        private WorkspaceDiagnosticView _diagnosticView;
        private Label _performanceText, _modelText;
        private Toggle _runtimeHybrid;
        private readonly FrameStatistics _performance = new FrameStatistics();
        private long _previousAllocated;
        private float _performanceRefresh;
        private void ResetPerformance()
        { _performance.Clear(); _previousAllocated = 0; _performanceRefresh = 0; }

        private void BuildDiagnosticControls()
        {
            _runtimeHybrid = new Toggle("Hybrid crowd mode (pause to change)") { value = Session.Hybrid, name = "runtime-hybrid" };
            _runtimeHybrid.RegisterValueChangedCallback(evt => { Session.SetPausedHybrid(evt.newValue); Refresh(); });
            _inspectorPage.Add(_runtimeHybrid);
            Toggle gpu = new Toggle("GPU instanced crowd rendering") { value = _world.Humans.PreferGpuInstancing, name = "gpu-instancing" };
            gpu.RegisterValueChangedCallback(evt => _world.Humans.PreferGpuInstancing = evt.newValue); _inspectorPage.Add(gpu);
            Label(_inspectorPage, "Batches volumetric bodies, outlines and rings on supported GPUs. Turn off for a comparison. Simulation stays on the CPU with the same rules.", "muted");
            _performanceText = Label(_inspectorPage, "Collecting frame measurements…", "diagnostic-card"); _performanceText.name = "performance-metrics";
            Foldout overlays = new Foldout { text = "Diagnostic overlays", value = true }; _inspectorPage.Add(overlays);
            DiagnosticOverlay[] flags = { DiagnosticOverlay.Desired, DiagnosticOverlay.Avoidance, DiagnosticOverlay.Herd, DiagnosticOverlay.Sfm,
                DiagnosticOverlay.Blast, DiagnosticOverlay.Vision, DiagnosticOverlay.Congestion, DiagnosticOverlay.Trails };
            string[] labels = { "Desired direction · green", "Avoidance direction · amber", "Herd / lane direction · teal", "Total SFM force · magenta",
                "Blast direction · orange", "Vision cone · blue", "Congestion heat · purple", "Trajectory trails · charcoal" };
            for (int i = 0; i < flags.Length; i++)
            {
                DiagnosticOverlay flag = flags[i];
                Toggle toggle = new Toggle(labels[i]) { value = (Session.Overlays & flag) != 0, name = "overlay-" + flag };
                toggle.AddToClassList("overlay-row");
                toggle.RegisterValueChangedCallback(evt => { if (evt.newValue) Session.Overlays |= flag; else Session.Overlays &= ~flag; });
                if (flag == DiagnosticOverlay.Blast) toggle.SetEnabled(Session.Scenario == CrowdSim.Core.Model.ScenarioType.Explosion);
                overlays.Add(toggle);
            }
            Label(overlays, "Direction arrows are normalized display lengths; force length is capped. Cones use configured vision range/angle, without occlusion clipping. Purple is local congestion, distinct from orange panic. Trails retain up to 40 samples at one sample per 3 ticks. All overlays survive inspector hiding.", "muted");
            Foldout scale = new Foldout { text = "Reproducible crowd comparisons" }; _inspectorPage.Add(scale);
            DropdownField count = new DropdownField("Population", new System.Collections.Generic.List<string> { "220", "500", "1000" }, 0);
            Toggle furniture = new Toggle("Furnished comparison (80 seated)"); scale.Add(count); scale.Add(furniture);
            Button(scale, "New comparison layout", () => { Files.NewScale(int.Parse(count.value), furniture.value, Session.Hybrid); SyncFiles(); });
            Button(scale, "New dense 800-person layout (32 tables)", () => { Files.NewDense(); SyncFiles(); });
            Label(scale, "Creates an editable layout with seed 731 and four exits. Save/discard prompts protect the current scene. Finalise and trigger the hazard, then replay between Hybrid comparisons. Compare identical layouts/speed/zoom and warmed runs; results depend on hardware. CPU timings are not application FPS or realism validation.", "muted");
            Foldout model = new Foldout { text = "Simulation model and parameters" }; _inspectorPage.Add(model);
            _modelText = Label(model, "", "diagnostic-card");
            Label(model, "Acute stress is one bounded modifier, not the evacuation switch. Purposeful evacuation can remain brisk at low panic. Doorway density, delayed progress, urgency and assertiveness produce yielding, gap seeking or bounded pressing. Navigation uses body-clear routes; individual SFM/contact guards remain authoritative with Hybrid off or on. Fume is an abstract normalized field, not chemical CFD.", "muted");
            BuildAgentLegend(_inspectorPage);
        }
        private string OverviewDiagnostics()
        {
            DiagnosticFrame d = Session.Diagnostics; PopulationMetrics p = d.Population;
            StringBuilder b = new StringBuilder();
            b.AppendLine("Tick " + d.Tick + " · " + p.ElapsedSeconds.ToString("F1") + " s · seed " + d.Seed);
            b.AppendLine("Room " + Session.Frame.Width.ToString("F1") + " × " + Session.Frame.Length.ToString("F1") + " m");
            b.AppendLine("Population " + (Session.IsBuilding ? Session.PlannedCount : Session.Frame.Population) + " · in room " + p.InRoom + " · mobile/responsive " + p.Mobile);
            b.AppendLine("Evacuated " + p.Exited + " · injured " + p.Injured + " · crawling " + p.Crawling + " · fallen " + p.Fallen);
            b.AppendLine("Temporary incapacity " + p.TempIncap + " · incapacitated " + p.Incapacitated + " · fatal " + p.Dead);
            b.AppendLine("Going to assist " + p.GoingToAssist + " · helping " + p.CurrentlyHelping);
            b.AppendLine("Panic mean/max " + p.AveragePanic.ToString("F2") + " / " + p.MaximumPanic.ToString("F2"));
            b.AppendLine("Congestion mean " + p.AverageCongestion.ToString("F2") + " · visibility mean " + p.AverageVisibility.ToString("F2"));
            b.AppendLine("Run contagion " + p.RunningContagion + " · threat escape " + p.ThreatEscape + " · crowd following " + p.FollowingCrowd);
            b.AppendLine("Seated " + Session.Frame.SeatedAgents + " · rising " + Session.Frame.RisingAgents);
            b.AppendLine("Furniture airborne/fallen/destroyed " + Session.Frame.FurnitureBodies.Count(f => f.State == CrowdSim.Core.Model.ChairState.Airborne) + " / " + Session.Frame.FurnitureBodies.Count(f => f.State == CrowdSim.Core.Model.ChairState.Fallen) + " / " + Session.Frame.FurnitureBodies.Count(f => f.State == CrowdSim.Core.Model.ChairState.Destroyed));
            b.AppendLine("Square pillars " + d.Squares + " · circular pillars " + d.Circles + " · internal blue walls " + d.BlueWalls);
            b.AppendLine("Hybrid " + Session.Hybrid + " · run state " + Session.Phase);
            b.AppendLine("Hazard " + (Session.Frame.HazardReleased ? "released" : "not released"));
            if (Session.Scenario == CrowdSim.Core.Model.ScenarioType.ToxicFume)
                b.AppendLine("Fume exposed/impaired/incapacitated " + p.ToxicExposed + " / " + p.ToxicImpaired + " / " + p.ToxicIncapacitated + "\nHigh concentration " + p.ToxicHighConcentration + " · peak " + p.PeakToxicConcentration.ToString("F3") + "\nMean cumulative exposure " + p.AverageToxicExposure.ToString("F2"));
            else b.AppendLine("Debris hits/injuries/incapacitations/fatalities " + Session.Blast.DebrisHits + " / " + Session.Blast.DebrisInjuries + " / " + Session.Blast.DebrisIncapacitations + " / " + Session.Blast.DebrisFatalities);
            b.AppendLine("\nExit flow (native density count and persons/s)");
            foreach (ExitDiagnostic exit in d.Exits) b.AppendLine(exit.Name + " · density " + exit.Density.ToString("F0") + " · flow " + exit.Flow.ToString("F2") + "/s");
            return b.ToString();
        }
        private void UpdateDiagnosticText()
        {
            if (Session.InspectorTab != 3) return;
            _runtimeHybrid?.SetValueWithoutNotify(Session.Hybrid); _runtimeHybrid?.SetEnabled(Session.CanChangeHybrid);
            if (_modelText != null) _modelText.text = string.Join("\n", Session.Diagnostics.Model.Select(r => r.Label + ": " + r.Value));
        }
        private void UpdatePerformanceText()
        {
            if (Session.InspectorTab != 3 || _performanceText == null || Time.unscaledTime < _performanceRefresh) return;
            _performanceRefresh = Time.unscaledTime + .25f;
            DiagnosticFrame d = Session.Diagnostics;
            _performanceText.text = "Observed FPS " + _performance.FramesPerSecond.ToString("F1") + " · frame " + _performance.FrameMilliseconds.ToString("F2") + " ms\n" +
                "Frame p50/p95 " + _performance.Percentile(.5).ToString("F2") + " / " + _performance.Percentile(.95).ToString("F2") + " ms (last ≤120 frames)\n" +
                "Physics commands CPU " + _performance.PhysicsCommandsMilliseconds.ToString("F2") + " ms\n" +
                "Last engine tick CPU " + d.PhysicsMilliseconds.ToString("F2") + " ms · furniture " + d.FurnitureMilliseconds.ToString("F2") + " ms\n" +
                "Phases ms · grid " + d.GridMilliseconds.ToString("F2") + " · hazard " + d.HazardMilliseconds.ToString("F2") +
                " · response " + d.ResponseMilliseconds.ToString("F2") + " · preparation " + d.PreparationMilliseconds.ToString("F2") +
                "\nMovement " + d.MovementMilliseconds.ToString("F2") + " · contacts " + d.ContactsMilliseconds.ToString("F2") + " · exits " + d.ExitMilliseconds.ToString("F2") +
                "\nMovement subsets · SFM " + d.SfmMilliseconds.ToString("F2") + " · move/collision " + d.CollisionMilliseconds.ToString("F2") + " ms (do not add to phases)\n" +
                "Route CPU " + d.RouteMilliseconds.ToString("F2") + " ms · " + d.RouteRequests + " requests\n" +
                "World commands CPU " + _performance.WorldCommandsMilliseconds.ToString("F2") + " ms\nUI commands CPU " + _performance.UiCommandsMilliseconds.ToString("F2") + " ms (toolkit paint excluded)\n" +
                "Managed main-thread bytes/frame " + _performance.ManagedBytes.ToString("N0") + "\nVisible / active " + _world.Humans.VisibleCount + " / " + _world.Humans.ActiveCount +
                "\nHybrid open/detailed " + d.Open + " / " + d.Detailed + " · grouped " + d.Grouped + " in " + d.Groups + " groups\nShared field builds " + d.FieldBuilds + " · geometry refreshes " + d.GeometryRefreshes +
                "\nCrowd rendering: " + _world.Humans.RenderBackend + " · GPU instances " + _world.Humans.GpuInstances + " · submissions " + _world.Humans.GpuDraws +
                "\nGPU time: not measured · recording " + (IsRecording ? "active" : "idle") +
                "\nCapture CPU " + _captureMilliseconds.ToString("F2") + " ms · encode/write " + (_recording?.LastEncodingMilliseconds ?? _encodeMilliseconds).ToString("F2") +
                " ms · queue " + (_recording?.QueueCount ?? 0) + "/3 · dropped " + (_recording?.DroppedFrames ?? _lastDropped);
        }
    }
}
