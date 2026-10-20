#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CrowdSim.Core.Persistence;

namespace CrowdSim.Workspace
{
    /// <summary>Immutable file identity; sources outside the designated save directory are always read-only.</summary>
    public sealed class ScenarioEntry
    {
        public string Path { get; }
        public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);
        public bool ReadOnly { get; }
        public string Label => Name + (ReadOnly ? " [read-only] " : " [saved] ") + System.IO.Path.GetDirectoryName(Path);
        internal ScenarioEntry(string path, bool readOnly) { Path = path; ReadOnly = readOnly; }
    }

    /// <summary>Platform path injection with one write root and any number of explicitly read-only sources.</summary>
    public sealed class ScenarioLibrary
    {
        public string DirectoryPath { get; }
        private readonly string[] _sources;
        public IReadOnlyList<string> ListingErrors { get; private set; } = Array.Empty<string>();
        public ScenarioLibrary(string writableDirectory, params string[] readOnlyDirectories)
        {
            DirectoryPath = NormalDirectory(writableDirectory);
            _sources = new[] { DirectoryPath }.Concat(readOnlyDirectories.Select(NormalDirectory)).Distinct(PathComparer).ToArray();
        }
        private static string NormalDirectory(string path)
        {
            string full = Path.GetFullPath(path);
            return full.Length > (Path.GetPathRoot(full) ?? "").Length
                ? full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : full;
        }
        public bool IsCurrentTarget(ScenarioEntry entry, string name) => !entry.ReadOnly && PathComparer.Equals(entry.Path, TargetPath(name));
        private static StringComparer PathComparer => System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        private bool Writable(string path) => PathComparer.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), DirectoryPath);
        public IReadOnlyList<ScenarioEntry> List()
        {
            List<ScenarioEntry> entries = new List<ScenarioEntry>();
            List<string> errors = new List<string>();
            foreach (string directory in _sources)
            {
                try
                {
                    if (Directory.Exists(directory))
                        entries.AddRange(System.IO.Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                            .Select(path => new ScenarioEntry(Path.GetFullPath(path), !Writable(path))));
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                { errors.Add(directory + ": " + exception.Message); }
            }
            ListingErrors = errors.AsReadOnly();
            return entries.OrderBy(e => e.ReadOnly).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Path, PathComparer).ToList().AsReadOnly();
        }
        public ScenarioEntry ImportPath(string path)
        {
            string full = Path.GetFullPath(path);
            if (!string.Equals(Path.GetExtension(full), ".json", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Choose a .json scenario file.");
            if (!File.Exists(full)) throw new FileNotFoundException("Scenario file was not found.", full);
            return new ScenarioEntry(full, !Writable(full));
        }
        public string Read(ScenarioEntry entry) => File.ReadAllText(entry.Path);
        public string TargetPath(string name) => Path.Combine(DirectoryPath, SafeName(name) + ".json");
        public bool Exists(string name) => File.Exists(TargetPath(name));
        public ScenarioEntry Save(string json, string name)
        {
            // Export's name is already normalised by the coordinator. Decode/validate before writing.
            string safe = SafeName(name);
            ScenarioDefinition definition = ScenarioJson.Deserialize(json);
            if (definition.Name != safe) throw new InvalidDataException("Scenario name must match the reviewed save name.");
            string path = ScenarioFileStore.Save(definition, DirectoryPath);
            return new ScenarioEntry(Path.GetFullPath(path), false);
        }
        /// <summary>Portable desktop filename policy; no path separators, Windows device names or trailing dots.</summary>
        public static string SafeName(string name)
        {
            StringBuilder result = new StringBuilder();
            foreach (char ch in (name ?? "").Trim())
                result.Append(char.IsControl(ch) || "/\\<>:\"|?*".IndexOf(ch) >= 0 || char.IsWhiteSpace(ch) ? '_' : ch);
            string safe = result.ToString().Trim('_', '.');
            while (safe.Contains("__")) safe = safe.Replace("__", "_");
            if (safe.Length == 0) safe = "scenario";
            if (safe.Length > 100) safe = safe.Substring(0, 100).TrimEnd('.', '_');
            string stem = safe.Split('.')[0].ToUpperInvariant();
            if (new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(stem)) safe = "scenario_" + safe;
            return safe;
        }
    }
}
