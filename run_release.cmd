@echo off
cd /d "%~dp0"
dotnet run -c Release --project src\CrowdSim.OpenGL\CrowdSim.OpenGL.csproj
if errorlevel 1 pause
