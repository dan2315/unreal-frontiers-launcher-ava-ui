@echo off
rem Starts the launcher in TEST mode: dev backend (http://localhost:3200), its own data folder
rem (%LOCALAPPDATA%\UnrealFrontiers.Test) and its own logins. Safe to run next to the real launcher.
rem
rem   run-test-launcher.bat                       built Debug exe, or "dotnet run" if it is not built
rem   run-test-launcher.bat "C:\path\to\UnrealFrontiersLauncher.exe"   any other build
rem
rem Optional: set UF_SERVER_URL to another test backend first. The production backend is refused.
set UF_MODE=test
set "EXE=%~1"
if "%EXE%"=="" set "EXE=%~dp0..\bin\Debug\net8.0\UnrealFrontiersLauncher.exe"
if exist "%EXE%" (
    start "" "%EXE%" --test
) else (
    dotnet run --project "%~dp0..\unreal-frontiers-launcher.csproj" -- --test
)
