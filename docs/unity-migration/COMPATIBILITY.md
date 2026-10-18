# Compatibility decisions and evidence

## Approved baseline

- Unity **6000.3.26f1**; URP **17.3.0**; API compatibility **.NET Standard 2.1**.
- UI Toolkit runtime UI; Input System **1.14.2** installed; both input backends
  enabled at M1. Input actions and simulator shortcuts arrive with M3.
- Unity Test Framework **1.4.6**; Newtonsoft JSON package **3.2.2** reserved for
  the persistence adapter. These are declarations, not a claim of successful UPM import.
- Source in the portable package uses C# 9, block namespaces and explicit imports.
  `Migration/Build/CrowdSim.Portable.csproj` compiles those same files against
  netstandard2.1; it does not compile replacement copies.
- The cloud harness has separate Directory.Build.props. Existing .NET 8/C# 12
  reference settings and package pins remain untouched.

Editor/URP pins were checked against Unity's official repositories:
[6000.3.26f1](https://github.com/Unity-Technologies/UnityCsReference/tree/6000.3.26f1),
[URP package metadata](https://github.com/Unity-Technologies/Graphics/blob/6000.3/staging/Packages/com.unity.render-pipelines.universal/package.json).
Supporting package dependency declarations informed the committed lock graph.
The lock was prepared without a Unity Editor; UPM may normalize source/depth data
on first import. Such changes must be reviewed, not presented as already resolved.

## M2 engine work identified by source inspection

The original uses file-scoped namespaces, record structs, init accessors, implicit
usings, PriorityQueue, Random.Shared, Stopwatch.GetElapsedTime, MathF.Tau, generic
enum helpers, argument ThrowIf helpers, OperatingSystem helpers and System.Text.Json.
They need source-compatible equivalents or adapters; a net8.0 DLL is not a Unity plugin.

The seeded-random and queue-dequeue oracle is in Parity/Reference/compatibility.json
under Migration/. Queue tie behavior must be retained; adding a stable tie-breaker
is a behavior change. Random sequences need comparison on Mono, not replacement
with UnityEngine.Random. Floating-point agreement across runtimes remains an M2 gate.

M1's portable assembly contains only version/contract metadata. It proves the
compilation boundary, not simulation portability. Physics, navigation, hazard and
furniture decisions will remain in the engine. Unity will map XY ground coordinates
to XZ and consume presentation data without writing physical state.

## Recording probe

The unchanged reference pins SharpAvi 3.0.1, SharpAvi.ImageSharp 3.0.0 and transitive
ImageSharp 1.0.4. A separate netstandard2.1 probe resolves their Standard-compatible
assets and encodes two 64x64 frames to indexed MJPEG AVI at 20 fps.

The Standard SharpAvi API requires `WriteFrame(bool, byte[], int, int)`; the
reference's shorter overload cannot be copied unchanged. The probe uses the
compatible overload and checks the AVI format, rate, dimensions, frame count and
index. No codec binary is installed in UnityProject at M1. Mono execution,
end-of-frame whole-UI capture, asynchronous queues, licensing notices for distributed
binaries and standalone behavior remain M10 work. The original recorder is unchanged.

## Validation limits

There is no Unity Editor installed on the current cloud worker. Static GUID,
XML/JSON, scene structure and dependency checks do not establish Unity C# frontend
compilation, package resolution, graphics initialization, UI layout or input.
Those checks are supplied in the project and local checklist, and remain unrun
until a real Editor executes them. No Windows-specific or IL2CPP validation is claimed.
