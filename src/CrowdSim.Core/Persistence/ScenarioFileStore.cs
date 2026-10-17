using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrowdSim.Core.Persistence;

/// <summary>JSON file persistence for configuration-only scenario definitions.</summary>
public static class ScenarioFileStore
{
    private const string ApplicationFolderName = "CrowdSimCs";
    private const string ScenarioFolderName = "scenarios";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.Strict,
    };

    /// <summary>
    /// Stable, user-writable scenario directory. A packaged desktop application must not depend
    /// on its process working directory, which can be read-only or change between launch methods.
    /// </summary>
    public static string DefaultDirectory
    {
        get
        {
            string localData = System.Environment.GetFolderPath(
                System.Environment.SpecialFolder.LocalApplicationData,
                System.Environment.SpecialFolderOption.Create);
            return !string.IsNullOrWhiteSpace(localData)
                ? Path.Combine(localData, ApplicationFolderName, ScenarioFolderName)
                : Path.Combine(AppContext.BaseDirectory, ScenarioFolderName);
        }
    }

    /// <summary>Former working-directory location retained as a read-only compatibility source.</summary>
    public static string LegacyDirectory
        => Path.Combine(System.Environment.CurrentDirectory, ScenarioFolderName);

    public static IReadOnlyList<string> ListScenarioNames(string? directory = null)
    {
        if (directory is not null)
        {
            return ListNamesInDirectory(directory);
        }

        // Show previous working-directory saves alongside new per-user saves without moving or
        // overwriting user files. The per-user copy wins if names collide.
        return ListNamesInDirectory(DefaultDirectory)
            .Concat(PathsEqual(DefaultDirectory, LegacyDirectory)
                ? Array.Empty<string>()
                : ListNamesInDirectory(LegacyDirectory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ListNamesInDirectory(string targetDirectory)
    {
        try
        {
            if (!Directory.Exists(targetDirectory))
            {
                return Array.Empty<string>();
            }

            return Directory.EnumerateFiles(targetDirectory, "*.json", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            // One inaccessible compatibility directory must not hide valid per-user saves.
            return Array.Empty<string>();
        }
    }

    public static string Save(
        ScenarioDefinition scenario,
        string? directory = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.ValidateBasic();

        string targetDirectory = directory ?? DefaultDirectory;
        Directory.CreateDirectory(targetDirectory);
        string safeName = SanitizeName(scenario.Name);
        string path = Path.Combine(targetDirectory, safeName + ".json");
        string json = JsonSerializer.Serialize(scenario, JsonOptions);
        string temporaryPath = Path.Combine(
            targetDirectory,
            $".{safeName}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

        return path;
    }

    public static ScenarioDefinition Load(
        string scenarioName,
        string? directory = null)
    {
        string safeName = SanitizeName(scenarioName);
        string path = ResolveLoadPath(safeName, directory);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Scenario '{safeName}' was not found.", path);
        }

        string json = File.ReadAllText(path);
        ScenarioDefinition? scenario = JsonSerializer.Deserialize<ScenarioDefinition>(json, JsonOptions);
        if (scenario is null)
        {
            throw new InvalidDataException("Scenario JSON did not contain a valid scenario object.");
        }

        if (scenario.Version == 1)
        {
            scenario = ScenarioDefinition.UpgradeLegacyV1(scenario);
        }

        if (scenario.Version == 2)
        {
            scenario = ScenarioDefinition.UpgradeLegacyV2(scenario);
        }

        if (scenario.Version == 3)
        {
            scenario = ScenarioDefinition.UpgradeLegacyV3(scenario);
        }
        scenario.ValidateBasic();
        return scenario;
    }

    private static string ResolveLoadPath(string safeName, string? directory)
    {
        if (directory is not null)
        {
            return Path.Combine(directory, safeName + ".json");
        }

        string primary = Path.Combine(DefaultDirectory, safeName + ".json");
        if (File.Exists(primary) || PathsEqual(DefaultDirectory, LegacyDirectory))
        {
            return primary;
        }

        return Path.Combine(LegacyDirectory, safeName + ".json");
    }

    private static bool PathsEqual(string left, string right)
        => string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    public static string SanitizeName(string? name)
    {
        string candidate = string.IsNullOrWhiteSpace(name) ? "scenario" : name.Trim();
        char[] invalid = Path.GetInvalidFileNameChars();
        string safe = new(candidate.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        safe = safe.Replace(' ', '_');
        while (safe.Contains("__", StringComparison.Ordinal))
        {
            safe = safe.Replace("__", "_", StringComparison.Ordinal);
        }

        safe = safe.Trim('_', '.');
        return string.IsNullOrWhiteSpace(safe) ? "scenario" : safe;
    }
}
