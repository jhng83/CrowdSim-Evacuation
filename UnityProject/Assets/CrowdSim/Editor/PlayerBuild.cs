using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrowdSim.Distribution;
using CrowdSim.Migration;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
using UnityEngine;
using PackageInfo = global::UnityEditor.PackageManager.PackageInfo;

namespace CrowdSim.Unity.Editor
{
    /// <summary>Explicit Windows x64/Mono release build. Never changes Editor optimization mode,
    /// switches the active target, overwrites a previous build or touches authored saves.</summary>
    public static class PlayerBuild
    {
        private const string Menu = "CrowdSim/Build/Windows x64 Player and ZIP";
        private const string PackageMenu = "CrowdSim/Build/Check Package Readiness";

        [MenuItem(PackageMenu)]
        private static void CheckPackagesFromMenu()
        {
            try
            {
                RequirePackagesReady();
                const string message = "Registered package versions, package errors and package.json files passed preflight. " +
                    "This does not test Hub authentication, every DLL dependency, shader import or the Windows build.";
                Debug.Log(message); EditorUtility.DisplayDialog("CrowdSim package preflight", message, "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("CrowdSim packages need attention", exception.Message, "OK");
            }
        }

        private static void RequirePackagesReady()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            IReadOnlyList<string> issues = PackageReadiness.Inspect(File.ReadAllText(Path.Combine(project, "Packages", "packages-lock.json")),
                PackageInfo.GetAllRegisteredPackages().Select(p => new PackageReadiness.ResolvedPackage(
                    p.name, p.version, p.resolvedPath, p.errors.Select(e => e.message))));
            if (issues.Count > 0) throw new BuildFailedException("Package preflight failed; player build was not started.\n\n" +
                string.Join("\n", issues) + "\n\n" + PackageReadiness.Recovery);
        }

        [MenuItem(Menu)]
        private static void FromMenu()
        {
            try
            {
                string output = BuildWindows64();
                EditorUtility.RevealInFinder(output);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("CrowdSim build could not complete", exception.Message, "OK");
            }
        }

        [MenuItem(Menu, true)]
        private static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode &&
            !EditorApplication.isCompiling && !BuildPipeline.isBuildingPlayer;

        /// <summary>Batch entry point: -buildTarget Win64 -executeMethod
        /// CrowdSim.Unity.Editor.PlayerBuild.BuildFromCommandLine [-crowdsimOutput fresh-directory].</summary>
        public static void BuildFromCommandLine() => BuildWindows64();

        /// <summary>Build, validate, inventory and package a real supported Unity player.
        /// Returns the ZIP path; failures propagate to batch mode as a failed build.</summary>
        public static string BuildWindows64()
        {
            if (!CanBuild()) throw new BuildFailedException("Stop Play Mode and wait for compilation before building.");
            if (Application.unityVersion != MigrationContract.EditorVersion)
                throw new BuildFailedException("Use Unity " + MigrationContract.EditorVersion + ".");
            RequirePackagesReady();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                throw new BuildFailedException("Select Windows x64 in File > Build Profiles first, then retry. The command does not switch targets automatically.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new BuildFailedException("Install Windows Build Support for this Editor through Unity Hub.");
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) != ScriptingImplementation.Mono2x ||
                PlayerSettings.GetApiCompatibilityLevel(NamedBuildTarget.Standalone) != ApiCompatibilityLevel.NET_Standard ||
                PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Standalone) != ManagedStrippingLevel.Disabled)
                throw new BuildFailedException("Use the committed Mono / .NET Standard 2.1 / Disabled managed-stripping settings.");
            if (PlayerSettings.bundleVersion != MigrationContract.ApplicationVersion ||
                PlayerSettings.companyName != "CrowdSim" || PlayerSettings.productName != "CrowdSim Unity")
                throw new BuildFailedException("Restore the committed product/version settings; changing identity also changes the save location.");
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0 || scenes[0] != MigrationContract.BootstrapScene ||
                !scenes.Contains(MigrationContract.ParityScene) || !scenes.Contains(MigrationContract.HumanoidScene) || scenes.Any(s => !File.Exists(s)))
                throw new BuildFailedException("Restore the committed build scene list (Bootstrap first, diagnostic scenes included).");

            string project = Directory.GetParent(Application.dataPath).FullName;
            string output = Path.GetFullPath(OutputArgument() ?? Path.Combine(project, "Builds", "Windows-x64",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8)));
            string projectPrefix = project.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string buildsPrefix = Path.Combine(project, "Builds") + Path.DirectorySeparatorChar;
            if (output == project || (output.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase) &&
                !output.StartsWith(buildsPrefix, StringComparison.OrdinalIgnoreCase)))
                throw new BuildFailedException("Inside the project, build only under Builds; never into Assets, Packages or Library.");
            if (File.Exists(output) || (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()))
                throw new BuildFailedException("Output must be new or empty; previous builds are never overwritten: " + output);
            Directory.CreateDirectory(output);
            string player = Path.Combine(output, "Player"); Directory.CreateDirectory(player);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = scenes, locationPathName = Path.Combine(player, PlayerRelease.Executable),
                target = BuildTarget.StandaloneWindows64, targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            });
            if (report == null || report.summary.result != BuildResult.Succeeded || report.summary.totalErrors != 0)
                throw new BuildFailedException("Unity player build failed; no release ZIP was created. " +
                    "See the first failing build step in Console/Editor.log. " +
                    "For authentication, missing Unsafe/Burst dependencies or URP import errors, " + PackageReadiness.Recovery);

            string docs = Path.Combine(player, "CrowdSim_Data", "StreamingAssets", "Documentation");
            File.Copy(Path.Combine(docs, "USER_GUIDE.txt"), Path.Combine(player, "README.txt"));
            CopyPackageNotices(player);
            PlayerBuildReceipt receipt = new PlayerBuildReceipt {
                Result = report.summary.result.ToString(), EditorVersion = Application.unityVersion,
                ApplicationVersion = PlayerSettings.bundleVersion, Target = report.summary.platform.ToString(), Backend = "Mono",
                Development = (report.summary.options & BuildOptions.Development) != 0, Scenes = scenes, BuildGuid = report.summary.guid.ToString(),
                BuiltUtc = DateTime.UtcNow.ToString("O"), Errors = report.summary.totalErrors, Warnings = report.summary.totalWarnings
            };
            File.WriteAllText(Path.Combine(player, PlayerRelease.ReceiptName), receipt.Serialize());
            string archive = Path.Combine(output, "CrowdSim-Windows-x64-" + MigrationContract.ApplicationVersion + ".zip");
            PlayerRelease.CreateArchive(player, archive);
            Debug.Log("CrowdSim Windows player and ZIP built: " + archive + ". Run the standalone local acceptance checklist before distribution.");
            return archive;
        }

        private static string OutputArgument()
        {
            string[] args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-crowdsimOutput");
            if (at < 0) return null;
            if (at + 1 == args.Length || args[at + 1].StartsWith("-", StringComparison.Ordinal))
                throw new BuildFailedException("-crowdsimOutput requires a fresh output directory.");
            return args[at + 1];
        }

        [Serializable]
        private sealed class PackageEntry
        {
            public string name, version;
            public string[] legalFiles;
        }
        [Serializable]
        private sealed class PackageInventory
        {
            public string scope = "Resolved Unity packages, including build/editor-only entries; legal texts copied verbatim where supplied.";
            public PackageEntry[] packages;
        }
        private static void CopyPackageNotices(string player)
        {
            List<PackageEntry> entries = new List<PackageEntry>();
            foreach (PackageInfo package in PackageInfo.GetAllRegisteredPackages().OrderBy(p => p.name, StringComparer.Ordinal))
            {
                List<string> copied = new List<string>();
                if (Directory.Exists(package.resolvedPath))
                    foreach (string file in Directory.GetFiles(package.resolvedPath).OrderBy(p => p, StringComparer.Ordinal))
                    {
                        string name = Path.GetFileName(file);
                        if (name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                        if (!name.Contains("license", StringComparison.OrdinalIgnoreCase) &&
                            !name.Contains("notice", StringComparison.OrdinalIgnoreCase) &&
                            !name.StartsWith("copying", StringComparison.OrdinalIgnoreCase)) continue;
                        string relative = "Notices/Packages/" + package.name + "/" + name;
                        string target = Path.Combine(player, relative); Directory.CreateDirectory(Path.GetDirectoryName(target));
                        File.Copy(file, target); copied.Add(relative);
                    }
                entries.Add(new PackageEntry { name = package.name, version = package.version, legalFiles = copied.ToArray() });
            }
            string inventory = Path.Combine(player, "Notices", "package-inventory.json");
            Directory.CreateDirectory(Path.GetDirectoryName(inventory));
            File.WriteAllText(inventory, JsonUtility.ToJson(new PackageInventory { packages = entries.ToArray() }, true));
        }
    }
}
