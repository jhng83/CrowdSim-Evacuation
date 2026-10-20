#nullable enable
using System;
using System.Collections.Generic;
using CrowdSim.Core.Model;

namespace CrowdSim.Workspace
{
    public enum FileTransition { None, Load, New, Startup, Diagnostics }
    /// <summary>Transactional file/session lifecycle, including dirty and overwrite decisions. No Unity dependency.</summary>
    public sealed class WorkspaceFiles
    {
        public WorkspaceSession Session { get; private set; }
        public ScenarioLibrary Library { get; }
        public ScenarioEntry? CurrentEntry { get; private set; }
        public IReadOnlyList<ScenarioEntry> Entries { get; private set; } = Array.Empty<ScenarioEntry>();
        public FileTransition Pending { get; private set; }
        public FileTransition Completed { get; private set; }
        public string PendingDescription => Pending == FileTransition.Load && _preparedEntry != null ? "Load " + _preparedEntry.Label
            : Pending == FileTransition.New && _prepared != null ? "New " + _prepared.Scenario + " scenario" : Pending.ToString();
        public string Message { get; private set; } = "";
        public string? OverwriteName { get; private set; }
        private WorkspaceSession? _prepared;
        private ScenarioEntry? _preparedEntry;
        private bool _continueAfterSave;

        public WorkspaceFiles(WorkspaceSession initial, ScenarioLibrary library)
        { Session = initial; Library = library; Refresh(); }
        public void Refresh()
        {
            try
            {
                Entries = Library.List();
                Message = Entries.Count + " scenario files. " + string.Join("\n", Library.ListingErrors);
            }
            catch (Exception exception) { Message = "Refresh failed: " + exception.Message; }
        }
        public bool Load(ScenarioEntry entry)
        {
            if (Session.Running) { Message = "Pause before loading a scenario."; return false; }
            if (Pending != FileTransition.None || OverwriteName != null) return false;
            try
            {
                WorkspaceSession prepared = WorkspaceSession.FromJson(Library.Read(entry));
                return Request(FileTransition.Load, prepared, entry);
            }
            catch (Exception exception) { Message = "Load failed: " + exception.Message; return false; }
        }
        public bool Import(string path)
        {
            try { return Load(Library.ImportPath(path)); }
            catch (Exception exception) { Message = "Import failed: " + exception.Message; return false; }
        }
        public bool New(ScenarioType scenario) => Request(FileTransition.New, new WorkspaceSession(scenario), null);
        public bool Leave(bool diagnostics) => Request(diagnostics ? FileTransition.Diagnostics : FileTransition.Startup, null, null);
        private bool Request(FileTransition transition, WorkspaceSession? prepared, ScenarioEntry? entry)
        {
            if (Pending != FileTransition.None || OverwriteName != null) return false;
            Completed = FileTransition.None;
            if (Session.Running) Session.ToggleRunning();
            Pending = transition; _prepared = prepared; _preparedEntry = entry;
            if (Session.IsDirty)
            { Message = "Unsaved authored changes. Save and continue, discard, or keep working."; return false; }
            Complete();
            return true;
        }
        private void Complete()
        {
            FileTransition transition = Pending;
            if (_prepared != null) { Session = _prepared; CurrentEntry = _preparedEntry; }
            Pending = FileTransition.None; _prepared = null; _preparedEntry = null;
            Completed = transition;
            Message = transition == FileTransition.Load ? "Loaded paused into the builder; review and finalise."
                : transition == FileTransition.New ? "New empty scenario."
                : "Ready to leave the workspace.";
        }
        public bool DiscardAndContinue()
        {
            if (Pending == FileTransition.None || OverwriteName != null) return false;
            Complete(); return true;
        }
        public void Cancel()
        {
            Pending = FileTransition.None; Completed = FileTransition.None; OverwriteName = null;
            _prepared = null; _preparedEntry = null; _continueAfterSave = false;
            Message = "Cancelled. Current scene and unsaved authored changes retained.";
        }
        public bool Save(string name, bool saveAs = false, bool continuePending = false)
        {
            if (OverwriteName != null) return false;
            try
            {
                string safe = ScenarioLibrary.SafeName(name);
                _continueAfterSave = continuePending;
                if (Library.Exists(safe) && (saveAs || CurrentEntry == null || !Library.IsCurrentTarget(CurrentEntry, safe)))
                {
                    OverwriteName = safe;
                    if (Session.Running) Session.ToggleRunning();
                    Message = "Replace existing save '" + safe + "'?";
                    return false;
                }
                return Write(safe);
            }
            catch (Exception exception) { Message = "Save failed: " + exception.Message; return false; }
        }
        private bool Write(string safe)
        {
            try
            {
                ScenarioEntry entry = Library.Save(Session.ExportAuthoredJson(safe), safe);
                CurrentEntry = entry;
                Session.MarkSaved(safe);
                Refresh();
                Message = "Saved authored starts to " + entry.Path + ". Runtime damage, positions and elapsed time are excluded.";
                if (_continueAfterSave && Pending != FileTransition.None) Complete();
                _continueAfterSave = false;
                return true;
            }
            catch (Exception exception) { Message = "Save failed: " + exception.Message; return false; }
        }
        public bool ConfirmOverwrite()
        {
            if (OverwriteName == null) return false;
            string name = OverwriteName; OverwriteName = null;
            return Write(name);
        }
        public void CancelOverwrite()
        { OverwriteName = null; _continueAfterSave = false; Message = "Overwrite cancelled; current scene retained."; }
        public bool Finalise()
        {
            try
            {
                bool success = Session.Finalise(); Message = Session.Message;
                return success;
            }
            catch (Exception exception) { Message = "Finalise failed: " + exception.Message; return false; }
        }
        public bool Replay()
        {
            if (Pending != FileTransition.None || OverwriteName != null) return false;
            try { bool success = Session.Replay(); Message = Session.Message; return success; }
            catch (Exception exception) { Message = "Replay failed: " + exception.Message; return false; }
        }
        public void AcknowledgeTransition() => Completed = FileTransition.None;
    }
}
