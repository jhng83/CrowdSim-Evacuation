# Codex cloud setup for CrowdSim-Evacuation

## Files and activation

The instruction filename is **`AGENTS.md`**, plural and uppercase, at the repository
root beside `CrowdSim.sln`. Codex discovers it as project guidance. This guide and
`DEVELOPMENT_GUIDE.md` are supporting documents referenced by that file; arbitrary
Markdown files are not automatically equivalent instruction files.

Commit these files to the branch used by the task. After the documentation PR is
merged, select that updated branch for a new cloud task. These files guide work;
they do not by themselves connect GitHub, install an SDK, configure an environment,
or create CI jobs.

## Environment preparation

Connect/select `jhng83/CrowdSim-Evacuation` in the Codex cloud environment available
to your account, then select the intended branch. In its setup configuration,
prepare a .NET 8 SDK and restore NuGet dependencies. Exact UI labels can differ
between Codex surfaces. No application API key is required to build or test this
simulator.

An existing .NET 8 SDK is sufficient. SDK 8.0.415 was used for the delivered source;
the repository currently does not enforce a `global.json`. Do not silently change
the framework or package versions when preparing the environment.

The following Bash setup block runs from the checked-out repository root. It
uses an existing SDK 8 installation when available and otherwise installs the
known SDK privately under `$HOME/.dotnet`. It does not need administrator rights:

```bash
set -euo pipefail
test -f CrowdSim.sln

if command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks | grep -q '^8\.'; then
    crowdsim_dotnet="$(command -v dotnet)"
elif test -x "$HOME/.dotnet/dotnet" && "$HOME/.dotnet/dotnet" --list-sdks | grep -q '^8\.'; then
    crowdsim_dotnet="$HOME/.dotnet/dotnet"
else
    crowdsim_install_script="$(mktemp)"
    trap 'rm -f "$crowdsim_install_script"' EXIT
    curl --fail --silent --show-error --location \
        https://dot.net/v1/dotnet-install.sh --output "$crowdsim_install_script"
    bash "$crowdsim_install_script" --version 8.0.415 \
        --install-dir "$HOME/.dotnet" --no-path
    crowdsim_dotnet="$HOME/.dotnet/dotnet"
fi

"$crowdsim_dotnet" --info
"$crowdsim_dotnet" restore CrowdSim.sln
```

The fallback needs `bash`, `curl`, certificate trust, archive extraction tools and
the native prerequisites of .NET on the Linux image. Provision missing system
dependencies through the environment's normal setup process. Do not download SDKs
into the repository or commit them.

Setup must have access to the Microsoft installer/SDK download hosts and NuGet
package feeds. If restore fails, preserve its exact error: blocked network, TLS,
an unavailable package version and an incompatible SDK need different fixes.
Do not automatically substitute package versions. A default simulator build has
no requirement for FDS, Python packages, Node.js or a database.

## Running commands after setup

A setup session's shell variables and `export` statements may not survive into
the agent session. If `dotnet` is already on PATH, use the commands in `AGENTS.md`.
If the private installer was used and it is not on PATH, use its explicit path:

```bash
"$HOME/.dotnet/dotnet" build CrowdSim.sln -c Release --no-restore
"$HOME/.dotnet/dotnet" test tests/CrowdSim.Tests/CrowdSim.Tests.csproj -c Release --no-build --no-restore
```

Alternatively configure PATH in the environment settings with the SDK directory
prepended, preserving the environment's existing entries. Do not rely solely on
editing a shell startup file that the agent shell might not read.

For cached environments with a maintenance hook, refresh restore after checkout
using the same executable selection as setup. If packages change during a task,
restore again with network access before `--no-restore` commands. A cached restore
from an older revision is not proof that a new revision's dependencies are present.

## Headless validation versus GUI validation

The core test project can run on a Linux cloud worker without a display. It links
pure UI state/layout helpers and does not reference the windowed application.
Building the solution also does not require opening a window.

Launching the native app requires GLFW/native libraries, a display server and a
usable OpenGL context. On a worker where those are provisioned, software OpenGL
with Xvfb may allow a smoke test. This is optional environment-specific setup,
not a substitute for testing the target Windows desktop. Do not add a browser UI
or change the renderer just to make a headless worker launch the application.

Use this reporting distinction:

| Evidence | What it establishes |
| --- | --- |
| Successful restore | Dependencies resolved in that environment/revision |
| Release build | The selected code/configuration compiles |
| xUnit run | The reported tests executed with the stated outcomes |
| Pure layout/state tests | Those helpers behaved correctly without a window |
| Native GUI interaction | The observed controls/input/rendering worked on that graphics stack |
| Existing checked-in screenshots/results | Historical evidence, not a new test run |

## Example first task

```text
Read AGENTS.md, docs/DEVELOPMENT_GUIDE.md and docs/CODEX_CLOUD.md.
Inspect the current implementation and run the Release build and test commands.
Report the actual outcomes and any environment limitation. Do not change code
just to complete this initial verification.
```

For subsequent work, state the visible symptom and desired behavior, then ask for
an implementation with appropriate tests. The root instructions carry the stable
architecture and preservation rules; the task prompt supplies the specific change.

## References

- [Official Codex AGENTS.md guidance](https://learn.chatgpt.com/docs/agent-configuration/agents-md)
- [Official Codex cloud environment guidance](https://learn.chatgpt.com/docs/environments/cloud-environment)
- [Microsoft .NET install script](https://learn.microsoft.com/dotnet/core/tools/dotnet-install-script)

Cloud configuration is managed in the Codex environment, not by a Markdown file
or a GitHub repository setting alone. This documentation does not add a GitHub
Actions workflow or change any simulation code.
