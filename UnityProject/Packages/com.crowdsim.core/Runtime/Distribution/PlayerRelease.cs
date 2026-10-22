#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CrowdSim.Migration;
using Newtonsoft.Json;

namespace CrowdSim.Distribution
{
    /// <summary>Receipt written only after Unity's real BuildPipeline reports success.</summary>
    public sealed class PlayerBuildReceipt
    {
        public int SchemaVersion { get; set; } = 1;
        public string Result { get; set; } = "";
        public string EditorVersion { get; set; } = "";
        public string ApplicationVersion { get; set; } = "";
        public string Target { get; set; } = "";
        public string Backend { get; set; } = "";
        public bool Development { get; set; }
        public string[] Scenes { get; set; } = Array.Empty<string>();
        public string BuildGuid { get; set; } = "";
        public string BuiltUtc { get; set; } = "";
        public int Errors { get; set; }
        public int Warnings { get; set; }
        public string Serialize() => JsonConvert.SerializeObject(this, Formatting.Indented);
    }

    /// <summary>Portable validation/ZIP packaging used by the Editor build command and cloud regressions.
    /// File-layout checks are not a substitute for executing the real Unity player.</summary>
    public static class PlayerRelease
    {
        public const string Executable = "CrowdSim.exe";
        public const string ReceiptName = "build-receipt.json";
        public const string ChecksumsName = "release-files.sha256";
        public static IReadOnlyList<string> RequiredFiles { get; } = Array.AsReadOnly(new[] {
            Executable, "UnityPlayer.dll", "MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll",
            "CrowdSim_Data/globalgamemanagers", "CrowdSim_Data/Managed/CrowdSim.Portable.dll",
            "CrowdSim_Data/Managed/CrowdSim.Foundation.dll", "CrowdSim_Data/Managed/CrowdSim.Workspace.Unity.dll",
            "CrowdSim_Data/Managed/Newtonsoft.Json.dll", "README.txt", ReceiptName, "Notices/package-inventory.json",
            "CrowdSim_Data/StreamingAssets/Scenarios/legacy_v1.json", "CrowdSim_Data/StreamingAssets/Scenarios/legacy_v2.json",
            "CrowdSim_Data/StreamingAssets/Scenarios/legacy_v3.json", "CrowdSim_Data/StreamingAssets/Scenarios/toxic_fume_205.json",
            "CrowdSim_Data/StreamingAssets/Documentation/USER_GUIDE.txt",
            "CrowdSim_Data/StreamingAssets/Documentation/THIRD_PARTY_NOTICES.txt",
            "CrowdSim_Data/StreamingAssets/Documentation/Notices/AtkinsonHyperlegible-OFL.txt",
            "CrowdSim_Data/StreamingAssets/Documentation/Notices/Newtonsoft.Json-LICENSE.txt"
        });

        /// <summary>Reject incomplete, unsupported, development or editor-contaminated player directories.</summary>
        public static PlayerBuildReceipt Validate(string directory)
        {
            if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("Player directory does not exist.");
            List<string> files = Files(directory);
            foreach (string relative in RequiredFiles)
                if (!files.Contains(relative) || new FileInfo(Path.Combine(directory, relative)).Length == 0)
                    throw new InvalidDataException("Incomplete player: " + relative);
            PlayerBuildReceipt? receipt = JsonConvert.DeserializeObject<PlayerBuildReceipt>(File.ReadAllText(Path.Combine(directory, ReceiptName)));
            if (receipt == null || receipt.SchemaVersion != 1 || receipt.Result != "Succeeded" || receipt.Errors != 0 || receipt.Development ||
                receipt.EditorVersion != MigrationContract.EditorVersion || receipt.ApplicationVersion != MigrationContract.ApplicationVersion ||
                receipt.Target != "StandaloneWindows64" || receipt.Backend != "Mono" || string.IsNullOrWhiteSpace(receipt.BuildGuid) ||
                receipt.Scenes == null || receipt.Scenes.Length == 0 || receipt.Scenes[0] != MigrationContract.BootstrapScene ||
                !receipt.Scenes.Contains(MigrationContract.ParityScene) || !receipt.Scenes.Contains(MigrationContract.HumanoidScene))
                throw new InvalidDataException("Player receipt does not describe a successful supported release build.");
            foreach (string relative in files)
            {
                string name = Path.GetFileName(relative);
                if (relative.StartsWith("CrowdSim_Data/Managed/", StringComparison.Ordinal) &&
                    (name.StartsWith("UnityEditor", StringComparison.OrdinalIgnoreCase) ||
                     name.StartsWith("CrowdSim.Editor", StringComparison.OrdinalIgnoreCase) ||
                     name.Contains("Tests", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("Editor/test assembly found in player: " + relative);
            }
            foreach (string relative in new[] { Executable, "UnityPlayer.dll", "MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll" })
                ValidatePe64(Path.Combine(directory, relative));
            return receipt;
        }

        /// <summary>Package a validated build without shipping symbol/debug backup directories.
        /// Never overwrite an existing archive or include links/paths outside the player root.</summary>
        public static void CreateArchive(string directory, string archivePath)
        {
            Validate(directory);
            string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string archive = Path.GetFullPath(archivePath);
            if (archive.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Put the ZIP outside the player directory.");
            if (File.Exists(archive)) throw new IOException("Archive already exists; use a new output path.");
            List<string> files = Files(directory).Where(p => !Excluded(p) && p != ChecksumsName).ToList();
            string sums = string.Join("\n", files.Select(p => Hash(Path.Combine(directory, p)) + "  " + p)) + "\n";
            string checksums = Path.Combine(directory, ChecksumsName);
            if (File.Exists(checksums)) throw new IOException("Checksums already exist; package a fresh player build.");
            File.WriteAllText(checksums, sums, new UTF8Encoding(false));
            files.Add(ChecksumsName); files.Sort(StringComparer.Ordinal);
            using (FileStream stream = new FileStream(archive, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (ZipArchive zip = new ZipArchive(stream, ZipArchiveMode.Create))
                foreach (string relative in files)
                {
                    ZipArchiveEntry entry = zip.CreateEntry(relative, CompressionLevel.Optimal);
                    using (Stream target = entry.Open())
                    using (FileStream source = File.OpenRead(Path.Combine(directory, relative))) source.CopyTo(target);
                }
        }

        private static bool Excluded(string path) => path.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".mdb", StringComparison.OrdinalIgnoreCase) ||
            path.Split('/').Any(p => p.Contains("DoNotShip", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("DontShip", StringComparison.OrdinalIgnoreCase));

        private static List<string> Files(string root)
        {
            List<string> files = new List<string>();
            void Visit(string directory, string prefix)
            {
                foreach (string path in Directory.GetFileSystemEntries(directory))
                {
                    FileAttributes attributes = File.GetAttributes(path);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Player package cannot contain symbolic links.");
                    string relative = prefix + Path.GetFileName(path);
                    if (relative.IndexOfAny(new[] { '\r', '\n', '\\' }) >= 0) throw new InvalidDataException("Invalid package path.");
                    if ((attributes & FileAttributes.Directory) != 0) Visit(path, relative + "/"); else files.Add(relative);
                }
            }
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Player root cannot be a symbolic link.");
            Visit(root, ""); files.Sort(StringComparer.Ordinal); return files;
        }

        private static void ValidatePe64(string path)
        {
            using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
            {
                if (reader.BaseStream.Length < 128 || reader.ReadUInt16() != 0x5a4d) throw new InvalidDataException("Not a Windows PE file: " + Path.GetFileName(path));
                reader.BaseStream.Position = 0x3c;
                int offset = reader.ReadInt32();
                if (offset < 64 || offset > reader.BaseStream.Length - 26) throw new InvalidDataException("Invalid PE header.");
                reader.BaseStream.Position = offset;
                if (reader.ReadUInt32() != 0x00004550 || reader.ReadUInt16() != 0x8664) throw new InvalidDataException("Expected Windows x64 PE machine type.");
                reader.BaseStream.Position = offset + 24;
                if (reader.ReadUInt16() != 0x20b) throw new InvalidDataException("Expected PE32+ header.");
            }
        }

        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream input = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
        }
    }
}
