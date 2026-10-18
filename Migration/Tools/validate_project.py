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
