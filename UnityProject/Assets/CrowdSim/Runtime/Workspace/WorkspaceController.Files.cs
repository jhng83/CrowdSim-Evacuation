using System;
using System.Linq;
using CrowdSim.Core.Model;
using CrowdSim.Workspace;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace CrowdSim.Unity.Workspace
{
    public sealed partial class WorkspaceController
    {
        private WorkspaceSession _shownSession;
        private VisualElement _modal;
        private Label _brand, _fileResult, _dirtyLabel;
        private TextField _saveField;
        private Button _loadFile, _importFile, _replay, _releaseFume;
        private string _saveName = "Untitled", _importPath = "", _entryPath = "", _operationStatus = "";
        private Action _approvedQuit;

        /// <summary>Request normal application exit. The owner exits only after authored-data decisions
        /// and synchronous recording finalization. Also serves the OS window-close path.</summary>
        public void RequestQuit(Action approved)
        {
            if (approved == null) throw new ArgumentNullException(nameof(approved));
            if (Files.Pending != FileTransition.None || Files.OverwriteName != null) return;
            _approvedQuit = approved;
            Files.Quit(); SyncFiles();
        }

        private void BuildFilesPage()
        {
            Label(_page, "Scenario files", "heading");
            Label(_page, "Saves contain authored configuration and starting placement. Runtime damage and motion are excluded.", "muted");
            Label(_page, "Save directory:\n" + Files.Library.DirectoryPath, "file-path");
            Button(_page, "Open save folder", () =>
            {
                try
                {
                    System.IO.Directory.CreateDirectory(Files.Library.DirectoryPath);
                    Application.OpenURL(new Uri(Files.Library.DirectoryPath + System.IO.Path.DirectorySeparatorChar).AbsoluteUri);
                }
                catch (Exception exception) { _operationStatus = "Cannot open save folder: " + exception.Message; Refresh(); }
            });
            _dirtyLabel = Label(_page, "", "metrics");
            _saveField = new TextField("Save name") { value = _saveName, name = "save-name" };
            _saveField.RegisterValueChangedCallback(evt => _saveName = evt.newValue); _page.Add(_saveField);
            Label hint = Label(_page, "Target: " + Files.Library.TargetPath(_saveName), "file-path");
            _saveField.RegisterValueChangedCallback(evt => hint.text = "Target: " + Files.Library.TargetPath(evt.newValue));
            Button(_page, "Save", () => SaveFromPage(false));
            Button(_page, "Save As", () => SaveFromPage(true));
            var entries = Files.Entries;
            var choices = entries.Select(e => e.Label).ToList();
            int selected = Math.Max(0, entries.ToList().FindIndex(e => e.Path == _entryPath));
            DropdownField saved = new DropdownField("Saved / read-only scenarios", choices, choices.Count == 0 ? -1 : selected) { name = "scenario-list" };
            if (entries.Count > 0) _entryPath = entries[selected].Path;
            saved.SetEnabled(entries.Count > 0);
            saved.RegisterValueChangedCallback(evt => { if (saved.index >= 0 && saved.index < entries.Count) _entryPath = entries[saved.index].Path; });
            _page.Add(saved);
            _loadFile = Button(_page, "Load selected (paused)", () =>
            {
                ScenarioEntry entry = Files.Entries.FirstOrDefault(e => e.Path == _entryPath);
                if (entry != null) Files.Load(entry);
                SyncFiles();
            });
            Button(_page, "Refresh file list", () => { Files.Refresh(); SyncFiles(); });
            TextField path = new TextField("Import JSON path (read-only)") { value = _importPath, name = "import-path" };
            path.RegisterValueChangedCallback(evt => _importPath = evt.newValue); _page.Add(path);
            _importFile = Button(_page, "Import path (paused)", () => { Files.Import(_importPath); SyncFiles(); });
            Label(_page, "Bundled examples, legacy directories and imported paths are read-only sources. Save copies to the directory above. Loaded scenes open paused in the builder.", "muted");
            DropdownField scenario = new DropdownField("New scenario type", new System.Collections.Generic.List<string> { "Explosion", "Toxic Fume" }, Session.Scenario == ScenarioType.Explosion ? 0 : 1);
            _page.Add(scenario);
            Button(_page, "New Scenario", () => { Files.New(scenario.index == 0 ? ScenarioType.Explosion : ScenarioType.ToxicFume); SyncFiles(); });
            Button(_page, "Return to startup", RequestExit);
            Button(_page, "Open M2 diagnostics", () => ShowExitPrompt(true));
            _fileResult = Label(_page, Files.Message, "status");
            UpdateFilesState();
        }
        private void SaveFromPage(bool saveAs, bool continuePending = false)
        {
            if (Files.Save(_saveName, saveAs, continuePending)) _entryPath = Files.CurrentEntry?.Path ?? "";
            SyncFiles();
        }
        private void UpdateFilesState()
        {
            if (_activePage != "Files" || _dirtyLabel == null) return;
            _dirtyLabel.text = "Scenario: " + Session.Name + "\nUnsaved authored changes: " + (Session.IsDirty ? "Yes" : "No") +
                (Files.CurrentEntry == null ? "" : "\nSource: " + Files.CurrentEntry.Label);
            _loadFile.SetEnabled(!Session.Running && Files.Entries.Count > 0);
            _importFile.SetEnabled(!Session.Running);
        }
        private void SyncFiles()
        {
            CloseModal();
            _operationStatus = Files.Message;
            if (Files.Completed == FileTransition.Quit)
            {
                StopRecording("Saved before application exit."); Files.AcknowledgeTransition();
                Action approved = _approvedQuit; _approvedQuit = null; approved?.Invoke(); return;
            }
            if (Files.Pending != FileTransition.Quit) _approvedQuit = null;
            if (Files.Completed == FileTransition.Startup)
            { StopRecording("Saved before leaving workspace."); Files.AcknowledgeTransition(); _exit?.Invoke(); Destroy(gameObject); return; }
            if (Files.Completed == FileTransition.Diagnostics)
            { StopRecording("Saved before opening diagnostics."); Files.AcknowledgeTransition(); SceneManager.LoadScene("SimulationParity"); return; }
            if (!ReferenceEquals(_shownSession, Session))
            {
                StopRecording("Saved before scenario replacement.");
                ResetPerformance();
                _shownSession = Session;
                _tool = PlacementTool.Exit; _exitWidth = 1; _people = 1;
                _saveName = Files.CurrentEntry == null ? Session.Name : Files.CurrentEntry.Name + (Files.CurrentEntry.ReadOnly ? "_copy" : "");
                _speed.SetValueWithoutNotify((float)Session.Speed);
                _world.ResetCamera(); ClearPreview(); _hasPointer = false;
                _drawnRevision = -1; _inspectorPage.name = "";
                _activePage = "Build / Run"; BuildPage(); Draw();
            }
            else if (_activePage == "Files")
            {
                if (!Session.IsDirty && Files.CurrentEntry != null && !Files.CurrentEntry.ReadOnly) _saveName = Session.Name;
                BuildPage();
            }
            Files.AcknowledgeTransition();
            Refresh();
            ShowFileDecision();
        }
        private void CloseModal()
        {
            if (_modal != null) { _modal.RemoveFromHierarchy(); _modal = null; }
            foreach (VisualElement element in _root.Children()) element.SetEnabled(true);
        }
        private void ShowFileDecision()
        {
            if (Files.Pending == FileTransition.None && Files.OverwriteName == null) return;
            if (Session.Running) { Session.ToggleRunning(); Refresh(); }
            foreach (VisualElement element in _root.Children()) element.SetEnabled(false);
            _modal = Box(_root, "modal-shade"); _modal.name = "file-decision";
            VisualElement frame = Box(_modal, "modal-card");
            ScrollView content = new ScrollView(ScrollViewMode.Vertical); content.AddToClassList("page-scroll"); frame.Add(content);
            VisualElement card = content;
            if (Files.OverwriteName != null)
            {
                Label(card, "Replace existing save?", "heading");
                Label(card, Files.Library.TargetPath(Files.OverwriteName), "file-path");
                Label(card, "The existing save is retained until you confirm replacement.", "muted");
                Button keep = Button(card, "Back / keep existing save", () => { Files.CancelOverwrite(); SyncFiles(); });
                Button(card, "Replace save", () => { if (Files.ConfirmOverwrite()) _entryPath = Files.CurrentEntry?.Path ?? ""; SyncFiles(); });
                keep.Focus();
            }
            else
            {
                Label(card, "Unsaved authored changes", "heading");
                Label(card, "Current: " + Session.Name + "\nNext action: " + Files.PendingDescription, "metrics");
                TextField name = new TextField("Save name before continuing") { value = _saveName, name = "decision-save-name" };
                name.RegisterValueChangedCallback(evt => _saveName = evt.newValue); card.Add(name);
                Label target = Label(card, "Target: " + Files.Library.TargetPath(_saveName), "file-path");
                name.RegisterValueChangedCallback(evt => target.text = "Target: " + Files.Library.TargetPath(evt.newValue));
                Button keep = Button(card, "Keep working", () => { Files.Cancel(); SyncFiles(); });
                Button(card, "Save and continue", () => SaveFromPage(false, true));
                Button(card, "Discard session and continue", () => { Files.DiscardAndContinue(); SyncFiles(); });
                keep.Focus();
            }
            Label(card, Files.Message, "status");
        }
    }
}
