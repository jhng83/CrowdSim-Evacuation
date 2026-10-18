#!/usr/bin/env python3
"""One-time M2 source import. Writes only to an explicit, empty output directory.

This is a mechanical starting point, not a future synchronization command. The
portable implementation is reviewed/maintained source; never regenerate over it.
"""
import argparse
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]


def positional_struct(match):
    access, name, arguments, ending = match.groups()
    fields = [tuple(p.strip().rsplit(" ", 1)) for p in arguments.split(",")]
    members = [f"{access} readonly struct {name} : IEquatable<{name}>", "{"]
    members += [f"    public {kind} {prop} {{ get; }}" for kind, prop in fields]
    members += [f"    public {name}({arguments})", "    {"]
    members += [f"        this.{prop} = {prop};" for _, prop in fields]
    members += ["    }"]
    comparisons = " && ".join(f"EqualityComparer<{kind}>.Default.Equals({prop}, other.{prop})" for kind, prop in fields)
    members += [f"    public bool Equals({name} other) => {comparisons};",
                f"    public override bool Equals(object other) => other is {name} value && Equals(value);",
                "    public override int GetHashCode()", "    {", "        int hash = 0;"]
    members += [f"        hash = unchecked(hash * -1521134295 + EqualityComparer<{kind}>.Default.GetHashCode({prop}));" for kind, prop in fields]
    members += ["        return hash;", "    }",
                f"    public static bool operator ==({name} left, {name} right) => left.Equals(right);",
                f"    public static bool operator !=({name} left, {name} right) => !left.Equals(right);"]
    if ending == ";":
        members += ["}"]
    return "\n".join(members)


def transform(text):
    text = re.sub(r"\b(public|private|internal) readonly record struct (\w+)\s*\(([^)]*)\)(;|\s*\{)", positional_struct, text)
    text = text.replace("readonly record struct OrientedRectangle", "readonly struct OrientedRectangle")
    text = re.sub(r"ArgumentNullException\.ThrowIfNull\((\w+)\);", r"if (\1 is null) throw new ArgumentNullException(nameof(\1));", text)
    text = text.replace("MathF.Tau", "ReferenceMath.Tau")
    text = text.replace("PriorityQueue<", "ReferencePriorityQueue<")
    text = text.replace("Random.Shared.Next()", "ReferenceRandom.CreateSeed()")
    text = text.replace("Random = new Random(RandomSeed)", "Random = new ReferenceRandom(RandomSeed)")
    text = re.sub(r"(?:System\.Diagnostics\.)?Stopwatch.GetElapsedTime\((\w+)\).TotalMilliseconds", r"ReferenceClock.ElapsedMilliseconds(\1)", text)
    text = re.sub(r"Array.Clear\((\w+)\)", r"Array.Clear(\1, 0, \1.Length)", text)
    text = text.replace("Enum.IsDefined(kind)", "Enum.IsDefined(typeof(FurnitureKind), kind)")
    text = text.replace("Enum.IsDefined(set.Kind)", "Enum.IsDefined(typeof(FurnitureKind), set.Kind)")
    text = text.replace("OperatingSystem.IsWindows()", "System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)")
    text = text.replace("File.Move(temporaryPath, path, overwrite: true);", "if (File.Exists(path)) File.Replace(temporaryPath, path, null);\n            else File.Move(temporaryPath, path);")
    found = re.search(r"^namespace ([\w.]+);", text, re.M)
    if found:
        before, after = text[:found.start()], text[found.end():].lstrip("\n")
        imports = ["System", "System.Collections.Generic", "System.IO", "System.Linq", "System.Threading", "System.Threading.Tasks", "CrowdSim.Core.Compatibility"]
        before = "".join("using " + name + ";\n" for name in imports if "using " + name + ";" not in before) + before
        text = before + "namespace " + found.group(1) + "\n{\n" + "\n".join("    " + line if line else "" for line in after.splitlines()) + "\n}\n"
    return text


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    output = Path(parser.parse_args().output)
    if output.exists() and any(output.iterdir()):
        raise SystemExit("Output directory must be empty; existing portable files are never overwritten.")
    for source in sorted((ROOT / "src/CrowdSim.Core").rglob("*.cs")):
        relative = source.relative_to(ROOT / "src/CrowdSim.Core")
        if "obj" in relative.parts or "bin" in relative.parts:
            continue
        target = output / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text("// M2 import of reference 01b4355; see docs/unity-migration/COMPATIBILITY.md.\n" + transform(source.read_text()))


if __name__ == "__main__":
    main()
