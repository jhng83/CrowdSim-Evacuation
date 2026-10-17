# FIX10 QA Notes

Scope: thicker alarm indicator plus exit congestion anti-banging behavior only.

Static checks completed in the generation environment:
- all .csproj / props XML parsed;
- VS Code JSON parsed;
- C# brace/parenthesis/bracket smoke check passed;
- modified files contain no placeholder/NotImplemented markers;
- two xUnit regression tests were added for moderate-panic yielding and high-panic no-repeat-setback behavior.

The generation environment does not contain the .NET SDK, so the authoritative validation remains:

```powershell
dotnet clean
dotnet build .\CrowdSim.sln -c Debug
dotnet test .\CrowdSim.sln -c Debug
```
