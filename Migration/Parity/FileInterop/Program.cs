extern alias reference;
extern alias portable;
using System.Text.Json;
using System.Text.Json.Nodes;
using RefSimulation = reference::CrowdSim.Core.Simulation;
using RefDefinition = reference::CrowdSim.Core.Persistence.ScenarioDefinition;
using RefStore = reference::CrowdSim.Core.Persistence.ScenarioFileStore;
using Session = portable::CrowdSim.Workspace.WorkspaceSession;
using Examples = portable::CrowdSim.Workspace.WorkspaceExamples;
using Library = portable::CrowdSim.Workspace.ScenarioLibrary;

string root = Path.GetFullPath(args[0]);
JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
string temporary = Path.Combine(Path.GetTempPath(), "crowdsim-file-interop-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporary);
try
{
    RefSimulation legacyModel = new(randomSeed: 42);
    if (!legacyModel.Environment.AddExitFromClick(.3f, 17, 3) || !legacyModel.Environment.AddBlueWallFromClick(30, 20))
        throw new InvalidOperationException("Legacy fixture setup failed.");
    JsonObject v1 = JsonNode.Parse(JsonSerializer.Serialize(RefDefinition.Capture(legacyModel, 4, "legacy"), options))!.AsObject();
    v1["version"] = 1;
    float scale = RefDefinition.LegacyMetersPerUnit;
    foreach (string field in new[] { "roomWidth", "roomHeight" }) v1[field] = v1[field]!.GetValue<float>() / scale;
    foreach (JsonNode? exit in v1["exits"]!.AsArray())
        foreach (string field in new[] { "x", "y", "size" }) exit![field] = exit[field]!.GetValue<float>() / scale;
    foreach (JsonNode? wall in v1["blueWalls"]!.AsArray())
        foreach (string field in new[] { "x1", "y1", "x2", "y2" }) wall![field] = wall[field]!.GetValue<float>() / scale;
    foreach (string field in new[] { "kill", "incapacitation", "fallen", "injury", "panic" })
        v1["blastZones"]![field] = v1["blastZones"]![field]!.GetValue<float>() / scale;
    string fume = File.ReadAllText(Path.Combine(root, "tests/CrowdSim.Tests/Fixtures/toxic_fume_205.json"));
    JsonObject v3 = JsonNode.Parse(fume)!.AsObject(); v3["version"] = 3;
    v3["behavior"]!.AsObject().Remove("maxRunningSpeedMetersPerSecond");
    var bundledLegacy = new Dictionary<string, string>
    {
        ["legacy_v1.json"] = v1.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n",
        ["legacy_v3.json"] = v3.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n"
    };
    foreach (var fixture in bundledLegacy)
    {
        string path = Path.Combine(root, "UnityProject/Assets/StreamingAssets/Scenarios", fixture.Key);
        if (args.Contains("--write-fixtures")) File.WriteAllText(path, fixture.Value);
        else if (File.ReadAllText(path) != fixture.Value) throw new InvalidOperationException("Bundled legacy fixture drift: " + fixture.Key);
    }
    var cases = new Dictionary<string, string>
    {
        ["v1-units-blue-walls-random-starts"] = v1.ToJsonString(),
        ["v2-authored-example"] = File.ReadAllText(Path.Combine(root, "scenarios/scenario_1.json")),
        ["v3-default-upgrade"] = v3.ToJsonString(),
        ["v4-furnished-free-angle"] = Examples.Furniture().ExportAuthoredJson("furnished"),
        ["v4-toxic-205"] = fume
    };
    foreach (var item in cases)
    {
        string nativeDirectory = Path.Combine(temporary, item.Key, "native");
        string portableDirectory = Path.Combine(temporary, item.Key, "portable");
        Directory.CreateDirectory(nativeDirectory);
        File.WriteAllText(Path.Combine(nativeDirectory, "input.json"), item.Value);
        RefDefinition native = RefStore.Load("input", nativeDirectory);
        RefSimulation nativeModel = new(config: native.CreateConfig(), randomSeed: native.RandomSeed);
        native.ApplyLayout(nativeModel);
        string expected = JsonSerializer.Serialize(RefDefinition.Capture(nativeModel, native.AgentCount, "canonical"), options);
        Session session = Session.FromJson(item.Value);
        Check(expected, session.ExportAuthoredJson("canonical"));
        Library library = new(portableDirectory);
        library.Save(session.ExportAuthoredJson("copy"), "copy");
        library.Save(session.ExportAuthoredJson("copy"), "copy"); // Atomic existing-file replacement.
        RefDefinition copied = RefStore.Load("copy", portableDirectory);
        RefSimulation copiedModel = new(config: copied.CreateConfig(), randomSeed: copied.RandomSeed);
        copied.ApplyLayout(copiedModel);
        Check(expected, JsonSerializer.Serialize(RefDefinition.Capture(copiedModel, copied.AgentCount, "canonical"), options));
        string nativeFile = RefStore.Save(native, nativeDirectory);
        Check(expected, Session.FromJson(File.ReadAllText(nativeFile)).ExportAuthoredJson("canonical"));
        if (Directory.EnumerateFiles(portableDirectory, "*.tmp").Any()) throw new InvalidOperationException("Temporary save leaked.");
        Console.WriteLine("PASS native/portable file round trips: " + item.Key);
    }
    Session sized = new(); sized.SelectAsset(portable::CrowdSim.OpenGL.UI.BuilderAsset.Stage);
    if (!sized.SetStageSize(8, 4) || !sized.Place(portable::CrowdSim.Workspace.PlacementTool.Asset, new System.Numerics.Vector2(20, 15), 0))
        throw new InvalidOperationException("Sized stage fixture setup failed.");
    string customDirectory = Path.Combine(temporary, "sized-stage");
    Library customLibrary = new(customDirectory); customLibrary.Save(sized.ExportAuthoredJson("custom"), "custom");
    bool nativeRejected = false;
    try { RefStore.Load("custom", customDirectory); }
    catch (InvalidDataException) { nativeRejected = true; }
    if (!nativeRejected) throw new InvalidOperationException("Native must explicitly reject v5, never silently use a default-sized stage.");
    Session loaded = Session.FromJson(File.ReadAllText(Path.Combine(customDirectory, "custom.json")));
    if (loaded.StageLength != 8 || loaded.StageWidth != 4) throw new InvalidOperationException("Unity v5 stage dimensions were lost.");
    Console.WriteLine("PASS sized-stage v5 Unity round trip and explicit unchanged-native rejection.");
    Console.WriteLine("All five schema/fixture interop cases pass against the unchanged native codec.");
}
finally { Directory.Delete(temporary, true); }

void Check(string expected, string actual)
{
    // Deserialize both with the unchanged reference type before canonical serialization.
    // This compares float values/schema semantics rather than JSON whitespace/float spelling.
    string normalExpected = JsonSerializer.Serialize(JsonSerializer.Deserialize<RefDefinition>(expected, options), options);
    string normalActual = JsonSerializer.Serialize(JsonSerializer.Deserialize<RefDefinition>(actual, options), options);
    if (normalExpected != normalActual) throw new InvalidOperationException("Native/portable authored definitions differ.");
}
