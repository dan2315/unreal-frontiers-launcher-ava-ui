[CmdletBinding()]
param(
    [string]$Output,
    [string]$ServerUrl,
    [switch]$Zip,
    [switch]$Upload,
    [string]$DevServerContainer = 'uf-dev-server-1',
    [string]$RemotePath = '/app/modpack_data/launcher',
    [switch]$Run
)

# Builds the DEV launcher: UnrealFrontiersLauncherDev.exe, orange DEV icon and title, test mode by default
# (data in %LOCALAPPDATA%\UnrealFrontiers.Test). It never auto-updates and can run next to the production launcher.
#
#   -ServerUrl <url>  the dev backend to use, e.g. http://100.64.0.5:3200 (default http://localhost:3200).
#                     The DEV build takes it as "--server-url <url>"; Start-Dev-Launcher.bat in the output passes it.
#   -Zip              also makes UnrealFrontiersLauncherDev.zip next to the output folder.
#   -Upload           copies that zip into the dev backend (container $DevServerContainer, $RemotePath), so it is
#                     downloadable from /updateclient/download_file/launcher?file=UnrealFrontiersLauncherDev.zip
#   -Run              starts the build.

$ErrorActionPreference = 'Stop'

$root = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$project = Join-Path (Split-Path -Parent $root) 'unreal-frontiers-launcher.csproj'
if (-not $Output) { $Output = Join-Path $root 'publish\dev' }
if ($Upload) { $Zip = $true }

if (Test-Path $Output) { Remove-Item $Output -Recurse -Force }
dotnet publish $project -c Release -r win-x64 --self-contained -p:DevBuild=true `
    -p:PublishReadyToRun=true -p:DebugType=None -p:DebugSymbols=false -o $Output
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

$exe = Join-Path $Output 'UnrealFrontiersLauncherDev.exe'
$arguments = if ($ServerUrl) { "--server-url $ServerUrl" } else { '' }
Set-Content -Path (Join-Path $Output 'Start-Dev-Launcher.bat') -Encoding ascii -Value @(
    '@echo off',
    "start """" ""%~dp0UnrealFrontiersLauncherDev.exe"" $arguments".TrimEnd()
)
Write-Host "Dev launcher: $exe"
if ($ServerUrl) { Write-Host "Backend: $ServerUrl (via Start-Dev-Launcher.bat)" }

if ($Zip) {
    $zipPath = Join-Path (Split-Path -Parent $Output) 'UnrealFrontiersLauncherDev.zip'
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path (Join-Path $Output '*') -DestinationPath $zipPath
    Write-Host "Zip: $zipPath"
}

if ($Upload) {
    $name = 'UnrealFrontiersLauncherDev.zip'
    docker exec $DevServerContainer mkdir -p $RemotePath
    if ($LASTEXITCODE -ne 0) { throw 'could not create the launcher folder in the dev backend' }
    docker cp $zipPath "${DevServerContainer}:${RemotePath}/$name.part"
    if ($LASTEXITCODE -ne 0) { throw 'docker cp failed' }
    docker exec $DevServerContainer mv -f "$RemotePath/$name.part" "$RemotePath/$name"
    if ($LASTEXITCODE -ne 0) { throw 'rename in the dev backend failed' }
    Write-Host "Uploaded to the dev backend: /updateclient/download_file/launcher?file=$name"
}

if ($Run) {
    if ($arguments) { Start-Process $exe -ArgumentList $arguments } else { Start-Process $exe }
}
