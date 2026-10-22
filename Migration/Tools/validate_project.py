#!/usr/bin/env python3
"""Static download/asset contract checks. This does not run a Unity importer."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / "UnityProject"
EXTERNAL_GUIDS = {
    "bf2edee5c58d82540a51f03df9d42094",  # URP 17.3.0 UniversalRenderPipelineAsset
    "de640fe3d0db1804a85f9fc8f5cadab6",  # URP 17.3.0 UniversalRendererData
}


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def validate() -> None:
    require("m_EditorVersion: 6000.3.26f1" in (PROJECT / "ProjectSettings/ProjectVersion.txt").read_text(), "Editor pin changed")
    manifest = json.loads((PROJECT / "Packages/manifest.json").read_text())["dependencies"]
    lock = json.loads((PROJECT / "Packages/packages-lock.json").read_text())["dependencies"]
    require(manifest["com.unity.render-pipelines.universal"] == "17.3.0", "URP pin changed")
    for name, version in manifest.items():
        require(lock[name]["version"] == version, f"Manifest/lock mismatch: {name}")
    for name, package in lock.items():
        for dependency in package["dependencies"]:
            require(dependency in lock, f"Missing locked dependency: {name} -> {dependency}")

    asset_roots = [PROJECT / "Assets", PROJECT / "Packages/com.crowdsim.core"]
    assets = [p for directory in asset_roots for p in directory.rglob("*") if not p.name.endswith(".meta")]
    guid_map: dict[str, Path] = {}
    for asset in assets:
        meta = Path(str(asset) + ".meta")
        require(meta.is_file(), f"Missing metadata: {asset.relative_to(ROOT)}")
        matches = re.findall(r"^guid: ([0-9a-f]{32})$", meta.read_text(), re.M)
        require(len(matches) == 1, f"Invalid metadata GUID: {meta}")
        require(matches[0] not in guid_map, f"Duplicate asset GUID: {meta}")
        guid_map[matches[0]] = asset
    for directory in asset_roots:
        for meta in directory.rglob("*.meta"):
            require(Path(str(meta)[:-5]).exists(), f"Orphan metadata: {meta}")
    for asset in assets + list((PROJECT / "ProjectSettings").glob("*.asset")):
        if asset.is_file() and asset.suffix in {".unity", ".asset"}:
            text = asset.read_text()
            for reference in re.findall(r"guid: ([0-9a-f]{32})", text):
                require(reference in guid_map or reference in EXTERNAL_GUIDS or reference == "0" * 32,
                        f"Unresolved serialized GUID in {asset}: {reference}")
            anchors = re.findall(r"^--- !u!\d+ &(\d+)$", text, re.M)
            require(len(anchors) == len(set(anchors)), f"Duplicate serialized object ID in {asset}")
            for reference in re.findall(r"\{fileID: (\d+)\}", text):
                require(reference == "0" or reference in anchors, f"Broken local object reference in {asset}: {reference}")

    scene = PROJECT / "Assets/CrowdSim/Scenes/Bootstrap.unity"
    require(scene.is_file(), "Bootstrap scene missing")
    require("m_Name: CrowdSim Bootstrap" in scene.read_text(), "Scene has no bootstrap host")
    require("m_Name: Main Camera" in scene.read_text(), "Scene has no camera")
    require("Assets/CrowdSim/Scenes/Bootstrap.unity" in (PROJECT / "ProjectSettings/EditorBuildSettings.asset").read_text(),
            "Bootstrap scene is not in build settings")

    uxml = PROJECT / "Assets/CrowdSim/Resources/CrowdSim/Startup.uxml"
    tree = ET.parse(uxml)
    names = [e.attrib["name"] for e in tree.iter() if "name" in e.attrib]
    require(len(names) == len(set(names)), "Duplicate UXML element names")
    controller = PROJECT / "Assets/CrowdSim/Runtime/App/BootstrapController.cs"
    for name in re.findall(r'\.Q<\w+>\("([^"]+)"\)', controller.read_text()):
        require(name in names, f"Controller binds a nonexistent UXML element: {name}")
    for style in tree.getroot().findall("Style"):
        require((uxml.parent / style.attrib["src"]).is_file(), "Missing startup stylesheet")

    for name, original in [("legacy_v2.json", "scenarios/scenario_1.json"),
                           ("toxic_fume_205.json", "tests/CrowdSim.Tests/Fixtures/toxic_fume_205.json")]:
        require((PROJECT / "Assets/StreamingAssets/Scenarios" / name).read_bytes() == (ROOT / original).read_bytes(),
                f"Bundled scenario differs from reference: {name}")

    assemblies = {}
    for file in PROJECT.rglob("*.asmdef"):
        data = json.loads(file.read_text())
        require(data["name"] not in assemblies, "Duplicate assembly name")
        assemblies[data["name"]] = data
    require(assemblies["CrowdSim.Portable"]["noEngineReferences"], "Portable core permits UnityEngine references")
    core_assembly = assemblies["CrowdSim.Portable"]
    require(core_assembly.get("overrideReferences") and
            "Newtonsoft.Json.dll" in core_assembly.get("precompiledReferences", []),
            "Core must reference the JSON package's precompiled DLL")
    require("Unity.Newtonsoft.Json" not in core_assembly.get("references", []),
            "JSON package 3.2.2 supplies DLLs, not an assembly definition")
    for file in (PROJECT / "Packages/com.crowdsim.core/Runtime").rglob("*.cs"):
        require("using Unity" not in file.read_text(), f"Engine dependency in portable source: {file}")

    require((PROJECT / "Assets/CrowdSim/Resources/CrowdSim/Workspace.uss").is_file(), "M3 stylesheet missing")
    require("CrowdSim.Workspace.Unity" in assemblies["CrowdSim.Foundation"]["references"], "Startup cannot reference M3 workspace")
    require("_workspace.transform.SetParent" not in controller.read_text(), "Workspace UIDocument must be independent of hidden startup UI")
    require("open-explosion-workspace" in names and "open-fume-workspace" in names, "M3 startup entries missing")

    require("open-furnished-workspace" in names, "M4 example entry missing")
    workspace = PROJECT / "Assets/CrowdSim/Runtime/Workspace/WorkspaceController.cs"
    require('name = "asset-picker"' in workspace.read_text() and 'name = "asset-angle-exact"' in workspace.read_text(), "M4 picker/rotation bindings missing")

    require((PROJECT / "Assets/CrowdSim/Runtime/Workspace/WorkspaceController.Files.cs").is_file(), "M5 file adapter missing")
    for name in ["legacy_v1.json", "legacy_v3.json"]:
        require((PROJECT / "Assets/StreamingAssets/Scenarios" / name).is_file(), "M5 legacy fixture missing: " + name)

    for name in ["WorkspaceController.Explosion.cs", "WorkspaceExplosionView.cs"]:
        require((PROJECT / "Assets/CrowdSim/Runtime/Workspace" / name).is_file(), "M6 Explosion adapter missing: " + name)

    require("open-fume-example" in names, "M7 ready-to-load Fume example missing")
    for name in ["WorkspaceController.Fume.cs", "WorkspaceFumeView.cs", "GroundOverlayLayer.cs", "WorkspaceSurfaceLibrary.cs"]:
        require((PROJECT / "Assets/CrowdSim/Runtime/Workspace" / name).is_file(), "M7 adapter/asset library missing: " + name)

    for name in ["WorkspaceHumanoidView.cs", "WorkspaceController.Agents.cs", "HumanoidPresentationGallery.cs"]:
        require((PROJECT / "Assets/CrowdSim/Runtime/Workspace" / name).is_file(), "M8 presentation adapter missing: " + name)
    human_scene = "Assets/CrowdSim/Scenes/Validation/HumanoidPresentation.unity"
    require((PROJECT / human_scene).is_file() and human_scene in (PROJECT / "ProjectSettings/EditorBuildSettings.asset").read_text(), "M8 pose reference scene missing from build settings")
    font = PROJECT / "Assets/CrowdSim/Resources/CrowdSim/Fonts/AtkinsonHyperlegible-Regular.ttf"
    require(font.read_bytes()[:4] == b"\x00\x01\x00\x00", "Bundled font is not a TrueType font")
    require("SIL OPEN FONT LICENSE" in font.with_name("OFL.txt").read_text(), "Bundled font license missing")
    require((PROJECT / "Assets/CrowdSim/Resources/CrowdSim/HumanOutline.shader").is_file(), "M8 outline shader must be bundled for player inclusion")

    # Check actual authored solid text/background colours (not rendered/material contrast).
    css = (PROJECT / "Assets/CrowdSim/Resources/CrowdSim/Workspace.uss").read_text()
    def style(selector: str, prop: str) -> tuple[float, ...]:
        values = []
        for selectors, declarations in re.findall(r"([^{}]+)\{([^{}]+)\}", re.sub(r"/\*.*?\*/", "", css, flags=re.S)):
            if selector in [s.strip() for s in selectors.split(",")]:
                values += re.findall(r"(?<![\w-])" + re.escape(prop) + r"\s*:\s*rgb\(([^)]+)\)", declarations)
        require(bool(values), "Missing design colour: " + selector + "/" + prop)
        return tuple(float(v.strip()) / 255 for v in values[-1].split(","))
    def luminance(rgb: tuple[float, ...]) -> float:
        linear = [v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in rgb]
        return sum(a * b for a, b in zip(linear, (.2126, .7152, .0722)))
    def contrast(ink: tuple[float, ...], paper: tuple[float, ...]) -> float:
        a, b = sorted([luminance(ink), luminance(paper)])
        return (b + .05) / (a + .05)
    for ink, ink_prop, paper in [(".workspace", "color", ".controls"), (".muted", "color", ".controls"),
                                  ("Button", "color", "Button.primary-button"), ("Button", "color", ".scenario-fume Button.primary-button"),
                                  (".agent-legend-text", "color", ".inspector"),
                                  ("Button", "color", "Button.primary-button:hover"), ("Button", "color", "Button.primary-button:focus"),
                                  ("Button", "color", ".scenario-fume Button.primary-button:hover"), ("Button", "color", ".scenario-fume Button.primary-button:focus")]:
        ratio = contrast(style(ink, ink_prop), style(paper, "background-color"))
        require(ratio >= 4.5, f"Small UI text contrast below 4.5:1: {ink} on {paper}: {ratio:.2f}")

    for name in ["WorkspaceController.Diagnostics.cs", "WorkspaceDiagnosticView.cs", "WorkspacePillarOutline.cs"]:
        require((PROJECT / "Assets/CrowdSim/Runtime/Workspace" / name).is_file(), "M9 adapter missing: " + name)
    for name in ["Workspace", "Startup", "Parity"]:
        sheet = (PROJECT / "Assets/CrowdSim/Resources/CrowdSim" / (name + ".uss")).read_text()
        for value in re.findall(r"(?<![\w-])color:\s*rgb\(([^)]+)\)", sheet):
            require(tuple(int(v.strip()) for v in value.split(",")) == (0, 0, 0), "M9 requires black authored text: " + name)
    require(".workspace { background-color: rgba(0, 0, 0, 0);" in css, "Opaque workspace UI would obscure the camera viewport")
    require((ROOT / "Migration/Benchmarks/CrowdSim.Scale.csproj").is_file() and
            (ROOT / "Migration/Benchmarks/packages.lock.json").is_file(), "M9 benchmark adapter/lock missing")

    for module in ["imageconversion", "screencapture"]:
        require(manifest["com.unity.modules." + module] == "1.0.0", "M10 runtime capture module missing: " + module)
    recorder = (PROJECT / "Assets/CrowdSim/Runtime/Workspace/WorkspaceController.Recording.cs").read_text()
    require("WaitForEndOfFrame" in recorder and "CaptureScreenshotIntoRenderTexture" in recorder,
            "M10 must capture the complete composed window at end of frame")
    require("EncodeArrayToJPG" in recorder and "UnityEditor" not in re.sub(r"//[^\n]*", "", recorder),
            "M10 encoder must work independently of Editor tools")
    for name in ["Recording/MjpegAviWriter.cs", "Recording/VideoRecording.cs", "Workspace/WorkspaceSession.Stage.cs"]:
        require((PROJECT / "Packages/com.crowdsim.core/Runtime" / name).is_file(), "M10 runtime contract missing: " + name)
    require((PROJECT / "Assets/CrowdSim/Runtime/Workspace/WorkspaceController.Stage.cs").is_file(), "M10 stage controls missing")

    gpu_shader = PROJECT / "Assets/CrowdSim/Resources/CrowdSim/CrowdInstanced.shader"
    gpu_batch = PROJECT / "Assets/CrowdSim/Runtime/Workspace/CrowdInstanceBatch.cs"
    require(gpu_shader.is_file() and "#pragma multi_compile_instancing" in gpu_shader.read_text(), "GPU crowd instancing shader missing")
    require("Graphics.RenderMeshInstanced" in gpu_batch.read_text(), "GPU crowd must submit instanced volumes")
    graphics = (PROJECT / "ProjectSettings/GraphicsSettings.asset").read_text()
    require("m_InstancingStripping: 2" in graphics, "Runtime instancing variants must use Keep All")
    for shader in [gpu_shader, PROJECT / "Assets/CrowdSim/Resources/CrowdSim/HumanOutline.shader"]:
        guid = re.search(r"^guid: ([0-9a-f]{32})$", Path(str(shader) + ".meta").read_text(), re.M).group(1)
        require(guid in graphics, "Runtime-instanced shader must be retained for builds: " + shader.name)

    parity_scene = "Assets/CrowdSim/Scenes/Validation/SimulationParity.unity"
    require((PROJECT / parity_scene).is_file(), "M2 diagnostic scene missing")
    require(parity_scene in (PROJECT / "ProjectSettings/EditorBuildSettings.asset").read_text(), "Diagnostic scene is absent from build settings")
    for reference in (ROOT / "Migration/Parity/Reference").glob("*.json"):
        if reference.name == "source-manifest.json":
            continue
        bundled = PROJECT / "Assets/StreamingAssets/Parity" / reference.name
        require(bundled.read_bytes() == reference.read_bytes(), f"Unity parity data differs: {reference.name}")

    source_manifest = json.loads((ROOT / "Migration/Parity/Reference/source-manifest.json").read_text())
    for name, expected in source_manifest["files"].items():
        require(hashlib.sha256((ROOT / name).read_bytes()).hexdigest() == expected, f"Reference source changed: {name}")
    require(source_manifest["referenceCommit"] == "01b435568f211f5c8ddf99b32b05e7ceb1ad3a24", "Reference commit changed")
    print(f"PASS: {len(guid_map)} unique asset GUIDs, complete package graph, startup bindings, "
          f"two exact fixture copies and {len(source_manifest['files'])} unchanged reference files.")
    print("Unity import, package resolution, C# frontend compilation and rendering are NOT established by this check.")


if __name__ == "__main__":
    try:
        validate()
    except (ValueError, KeyError, OSError, ET.ParseError) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        raise SystemExit(1)
