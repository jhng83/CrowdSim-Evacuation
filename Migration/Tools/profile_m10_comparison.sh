#!/usr/bin/env bash
# CPU-only comparison. Never edits original reference source or regenerates goldens.
# Builds create ignored bin/obj outputs; instrumentation edits only a temporary archive.
set -euo pipefail
crowdsim_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
crowdsim_dotnet="${DOTNET:-dotnet}"
crowdsim_output="${1:?Provide an output directory outside the checkout}"
crowdsim_frames="${2:-120}"
crowdsim_trials="${3:-3}"
crowdsim_temporary="$(mktemp -d)"
trap 'rm -rf "$crowdsim_temporary"' EXIT
mkdir -p "$crowdsim_output" "$crowdsim_temporary/source" "$crowdsim_temporary/baseline"
cd "$crowdsim_root"
# The baseline has identical model code to M10 plus diagnostic clocks only. The
# current dense fixture is a shared input, not a modification of M10's simulation.
git archive 6343f9d52ceb5a7e980e97bd1c7e04310ce37808 | tar -x -C "$crowdsim_temporary/source"
python3 Migration/Tools/instrument_m10_baseline.py "$crowdsim_temporary/source"
cp UnityProject/Packages/com.crowdsim.core/Runtime/Diagnostics/DenseCrowdLayout.cs "$crowdsim_temporary/source/UnityProject/Packages/com.crowdsim.core/Runtime/Diagnostics/"
"$crowdsim_dotnet" restore "$crowdsim_temporary/source/Migration/Build/CrowdSim.Portable.csproj" --locked-mode
"$crowdsim_dotnet" build "$crowdsim_temporary/source/Migration/Build/CrowdSim.Portable.csproj" -c Release --no-restore
"$crowdsim_dotnet" restore Migration/Benchmarks/CrowdSim.Scale.csproj --locked-mode
"$crowdsim_dotnet" build Migration/Benchmarks/CrowdSim.Scale.csproj -c Release --no-restore
cp -a Migration/Benchmarks/bin/Release/net8.0/. "$crowdsim_temporary/baseline/"
cp "$crowdsim_temporary/source/Migration/Build/bin/Release/netstandard2.1/CrowdSim.Portable.dll" "$crowdsim_temporary/baseline/"
"$crowdsim_dotnet" Migration/Benchmarks/bin/Release/net8.0/CrowdSim.Scale.dll --write-dense-fixtures "$crowdsim_output/fixtures"
sha256sum "$crowdsim_temporary/baseline/CrowdSim.Portable.dll" Migration/Benchmarks/bin/Release/net8.0/CrowdSim.Portable.dll > "$crowdsim_output/assembly-hashes.txt"
for ((crowdsim_trial=1; crowdsim_trial<=crowdsim_trials; crowdsim_trial++)); do
    DOTNET_TieredCompilation=0 "$crowdsim_dotnet" "$crowdsim_temporary/baseline/CrowdSim.Scale.dll" --profile "$crowdsim_output/base-$crowdsim_trial" "$crowdsim_frames" "$crowdsim_output/fixtures"
    DOTNET_TieredCompilation=0 "$crowdsim_dotnet" Migration/Benchmarks/bin/Release/net8.0/CrowdSim.Scale.dll --profile "$crowdsim_output/opt-$crowdsim_trial" "$crowdsim_frames" "$crowdsim_output/fixtures"
done
python3 Migration/Tools/summarize_performance.py "$crowdsim_output" "$crowdsim_trials"
