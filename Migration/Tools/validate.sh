#!/usr/bin/env bash
set -euo pipefail
crowdsim_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
crowdsim_dotnet="${DOTNET:-dotnet}"
cd "$crowdsim_root"
python3 Migration/Tools/validate_project.py
cd "$crowdsim_root/Migration"
# Resolve the pinned SDK for reference commands too, without changing the original project.
"$crowdsim_dotnet" restore ../CrowdSim.sln
"$crowdsim_dotnet" build ../CrowdSim.sln -c Release --no-restore -m:2
"$crowdsim_dotnet" test ../tests/CrowdSim.Tests/CrowdSim.Tests.csproj -c Release --no-build --no-restore \
    --logger 'trx;LogFileName=reference.trx' --results-directory "$crowdsim_root/Migration/artifacts/tests"
"$crowdsim_dotnet" restore Tests/CrowdSim.Migration.Tests.csproj --locked-mode
"$crowdsim_dotnet" test Tests/CrowdSim.Migration.Tests.csproj -c Release --no-restore \
    --logger 'trx;LogFileName=foundation.trx' --results-directory "$crowdsim_root/Migration/artifacts/tests"
"$crowdsim_dotnet" restore PortableTests/CrowdSim.Portable.Tests.csproj --locked-mode
"$crowdsim_dotnet" test PortableTests/CrowdSim.Portable.Tests.csproj -c Release --no-restore \
    --logger 'trx;LogFileName=portable-core.trx' --results-directory "$crowdsim_root/Migration/artifacts/tests"
"$crowdsim_dotnet" restore Parity/ReferenceCapture/ReferenceCapture.csproj --locked-mode
"$crowdsim_dotnet" run --project Parity/ReferenceCapture/ReferenceCapture.csproj -c Release --no-restore -- --verify "$crowdsim_root"
"$crowdsim_dotnet" restore Parity/PortableCapture/PortableCapture.csproj --locked-mode
"$crowdsim_dotnet" run --project Parity/PortableCapture/PortableCapture.csproj -c Release --no-restore -- --verify "$crowdsim_root"

"$crowdsim_dotnet" restore Parity/FileInterop/FileInterop.csproj --locked-mode
"$crowdsim_dotnet" run --project Parity/FileInterop/FileInterop.csproj -c Release --no-restore -- "$crowdsim_root"

# Build the CPU comparison adapter without imposing a wall-clock benchmark on routine validation.
"$crowdsim_dotnet" restore Benchmarks/CrowdSim.Scale.csproj --locked-mode
"$crowdsim_dotnet" build Benchmarks/CrowdSim.Scale.csproj -c Release --no-restore
