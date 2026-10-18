# M1 validation evidence (delivery snapshot)

Executed in Codex cloud on 2026-10-18, Linux x64, SDK 8.0.415/runtime 8.0.21.
This file distinguishes commands executed here from tests supplied for local Unity.

| Check | Actual result |
| --- | --- |
| Static project/asset/package graph/UXML checks | Passed: 30 unique GUIDs, resolved serialized references, startup bindings and two exact fixture copies |
| Reference preservation | Passed: SHA-256 manifest for 106 original files; no changes to src/, tests/, scenarios/, solution or reference dependency declarations |
| Original Release solution build | Passed |
| Original xUnit regression suite | 404 passed, 0 failed, 0 skipped |
| Foundation/recording-dependency xUnit suite | 8 passed, 0 failed, 0 skipped |
| Portable package source compilation | Passed: same production shell source, C# 9 / netstandard2.1, no UnityEngine or reference-engine assembly dependency |
| Reference capture replay | Passed: six cases, 121 tick observations each, repeat-generated hashes and compatibility oracle matched checked-in baselines |
| Encoder framework assets | Resolved SharpAvi/SharpAvi.ImageSharp netstandard2.0 and ImageSharp netstandard2.1 assets; encoded two-frame probe |
| Independent AVI inspection/decode | ffprobe: MJPEG, 64x64, 20/1 fps, 2 frames; ffmpeg decoded both, average RGB red [254,0,0] then blue [0,0,254] |
| Shell syntax and Git whitespace checks | Passed |
| GitHub Actions workflow | Added; remote execution is not established by these cloud results |
| Unity package resolution/first import | **Not executed — no Unity Editor installed** |
| Unity frontend C# compilation | **Not executed** |
| Unity EditMode tests | **3 supplied, not executed** |
| Unity PlayMode test | **1 supplied, not executed** |
| Actual Unity appearance, resizing and native input | **Not executed** |
| Desktop player build/run | **Not executed; final player acceptance belongs to M11** |

The original reference can emit its existing xUnit2031 advisory on a clean build;
its tests and assertion behavior were not changed. No tests were disabled or skipped.

## Reproduce available cloud checks

From the repository root, with an installed .NET SDK 8.0.415:

```bash
bash Migration/Tools/validate.sh
```

If dotnet is installed privately, set DOTNET to its executable before invoking:

```bash
DOTNET=/workspace/crowdsim-tools/dotnet/dotnet bash Migration/Tools/validate.sh
```

The script enters Migration/ to activate its SDK pin even for the original solution,
and restores the tooling using committed NuGet lockfiles. The reference capture
also pins runtime 8.0.21 to avoid silently using another installed runtime patch.
It verifies baselines without rewriting them. TRX outputs are under the ignored
`Migration/artifacts/tests/` directory. The workflow uploads actual TRX results.

To preserve the encoder probe for independent inspection:

```bash
CROWDSIM_PROBE_ARTIFACT="$PWD/Migration/artifacts/recording-probe.avi" bash Migration/Tools/validate.sh
ffprobe -v error -select_streams v:0 -show_entries stream=codec_name,width,height,r_frame_rate,nb_frames -of json Migration/artifacts/recording-probe.avi
```

The 64x64 probe is a dependency check, not the final 1280x720 Unity recorder.
No Unity codec binary or video capture implementation is delivered by M1.

## Required local acceptance

Follow [M1_LOCAL_TESTING.md](M1_LOCAL_TESTING.md) in Unity 6000.3.26f1. Actual Editor
results must be reported before M1 becomes accepted. A static package-lock check
cannot prove Unity registry resolution. First import may normalize the lock and
serialize generated default settings; review any such changes rather than assuming
they are parity failures or claiming they were tested here.

The user subsequently reported M1 tested successfully and authorized M2. No local
Editor logs were supplied; this snapshot retains the original cloud/local distinction.
