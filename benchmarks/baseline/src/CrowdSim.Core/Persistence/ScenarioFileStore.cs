using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrowdSim.Core.Persistence;

/// <summary>JSON file persistence for configuration-only scenario definitions.</summary>
public static class ScenarioFileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.Strict,
    };

    public static string DefaultDirectory => Path.Combine(System.Environment.CurrentDirectory, "scenarios");

    public static IReadOnlyList<string> ListScenarioNames(string? directory = null)
    {
        string targetDirectory = directory ?? DefaultDirectory;
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
        File.WriteAllText(path, json);
        return path;
    }

    public static ScenarioDefinition Load(
        string scenarioName,
        string? directory = null)
    {
        string targetDirectory = directory ?? DefaultDirectory;
        string safeName = SanitizeName(scenarioName);
        string path = Path.Combine(targetDirectory, safeName + ".json");
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

        scenario.ValidateBasic();
        return scenario;
    }

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
