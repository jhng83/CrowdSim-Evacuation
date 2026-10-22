#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CrowdSim.Unity.Editor
{
    /// <summary>Read-only package metadata/payload checks before a player build. These do not
    /// authenticate, repair packages or prove that all native/compiler dependencies can load.</summary>
    public static class PackageReadiness
    {
        public const string Recovery = "Close Unity, sign in to Unity Hub and reopen through Hub. " +
            "Allow package resolution/import to finish. Use a fresh short project path without old Library/Temp caches " +
            "if package files are missing. Keep committed package versions; do not add replacement DLLs. " +
            "See docs/unity-migration/M11_BUILD_RECOVERY.md in the source download.";

        /// <summary>Detached snapshot of Unity's registered package information.</summary>
        public sealed class ResolvedPackage
        {
            public string Name { get; }
            public string Version { get; }
            public string Path { get; }
            public IReadOnlyList<string> Errors { get; }
            public ResolvedPackage(string name, string version, string path, IEnumerable<string> errors)
            { Name = name; Version = version; Path = path; Errors = errors.ToArray(); }
        }

        /// <summary>Compare the committed lock graph with registered metadata and package.json
        /// files. Built-in engine modules can be virtual, so do not require their payload paths.</summary>
        public static IReadOnlyList<string> Inspect(string lockJson, IEnumerable<ResolvedPackage> packages)
        {
            JObject expected = JObject.Parse(lockJson)["dependencies"] as JObject ??
                throw new InvalidDataException("Package lock has no dependencies object.");
            Dictionary<string, ResolvedPackage> registered = packages.ToDictionary(p => p.Name, StringComparer.Ordinal);
            List<string> issues = new List<string>();
            foreach (JProperty item in expected.Properties().OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                string name = item.Name;
                if (!registered.TryGetValue(name, out ResolvedPackage? package))
                { issues.Add(name + ": not registered; package resolution must complete before building."); continue; }
                foreach (string error in package.Errors) issues.Add(name + ": " + error);
                string version = (string?)item.Value["version"] ?? "";
                if (!version.StartsWith("file:", StringComparison.Ordinal) && package.Version != version)
                    issues.Add(name + ": resolved " + package.Version + ", expected locked " + version + ".");
                if (name.StartsWith("com.unity.modules.", StringComparison.Ordinal)) continue;
                string manifest = System.IO.Path.Combine(package.Path, "package.json");
                if (string.IsNullOrWhiteSpace(package.Path) || !File.Exists(manifest))
                { issues.Add(name + ": package.json/payload directory is missing or unreadable."); continue; }
                try
                {
                    JObject actual = JObject.Parse(File.ReadAllText(manifest));
                    if ((string?)actual["name"] != name || (string?)actual["version"] != package.Version)
                        issues.Add(name + ": installed package.json disagrees with registered name/version.");
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                    exception is Newtonsoft.Json.JsonException)
                { issues.Add(name + ": cannot read package.json (" + exception.GetType().Name + ")."); }
            }
            return issues.AsReadOnly();
        }
    }
}
