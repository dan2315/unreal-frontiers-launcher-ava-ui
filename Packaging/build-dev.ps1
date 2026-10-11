[CmdletBinding()]
param(
    [string]$Output,
    [switch]$Run
)

# Builds the DEV launcher: UnrealFrontiersLauncherDev.exe, orange DEV icon and title, test mode by default
# (dev backend http://localhost:3200, data in %LOCALAPPDATA%\UnrealFrontiers.Test). It never auto-updates and
# can run next to the production launcher.

$ErrorActionPreference = 'Stop'

$root = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$project = Join-Path (Split-Path -Parent $root) 'unreal-frontiers-launcher.csproj'
if (-not $Output) { $Output = Join-Path $root 'publish\dev' }

if (Test-Path $Output) { Remove-Item $Output -Recurse -Force }
dotnet publish $project -c Release -r win-x64 --self-contained -p:DevBuild=true `
    -p:PublishReadyToRun=true -p:DebugType=None -p:DebugSymbols=false -o $Output
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

$exe = Join-Path $Output 'UnrealFrontiersLauncherDev.exe'
Write-Host "Dev launcher: $exe"
if ($Run) { Start-Process $exe }
