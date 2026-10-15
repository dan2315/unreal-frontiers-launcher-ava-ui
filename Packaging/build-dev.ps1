[CmdletBinding()]
param(
    [string]$Output,
    [string]$ServerUrl,
    [switch]$Zip,
    [switch]$Installer,
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
#   -Installer        also makes a Velopack installer (UFLauncher_installer.exe + version, like production). The DEV
#                     install does not register uflauncher://, so it never touches the production launcher's link.
#   -Upload           copies the zip and the installer into the dev backend (container $DevServerContainer, $RemotePath):
#                     /updateclient/download_file/launcher?file=UnrealFrontiersLauncherDev.zip | UFLauncher_installer.exe
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
if ($ServerUrl) { Set-Content -Path (Join-Path $Output 'server-url.txt') -Value $ServerUrl -NoNewline -Encoding ascii }
Write-Host "Dev launcher: $exe"
if ($ServerUrl) { Write-Host "Backend: $ServerUrl (via Start-Dev-Launcher.bat)" }

if ($Zip) {
    $zipPath = Join-Path (Split-Path -Parent $Output) 'UnrealFrontiersLauncherDev.zip'
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path (Join-Path $Output '*') -DestinationPath $zipPath
    Write-Host "Zip: $zipPath"
}

$uploads = @()
if ($Zip) { $uploads += $zipPath }

if ($Installer) {
    # Same file names as production, so /updateclient/download_file/launcher?file=UFLauncher_installer.exe works on the dev backend.
    $version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    $installerDir = Join-Path (Split-Path -Parent $Output) 'installer'
    if (Test-Path $installerDir) { Remove-Item $installerDir -Recurse -Force }
    vpk pack --packId UnrealFrontiersLauncherDev --packVersion $version --packDir $Output --mainExe UnrealFrontiersLauncherDev.exe `
        --runtime win-x64 --packTitle 'Unreal Frontiers DEV' --icon (Join-Path (Split-Path -Parent $root) 'Assets\Images\logo-dev.ico') --outputDir $installerDir
    if ($LASTEXITCODE -ne 0) { throw 'vpk pack failed' }

    $setup = Get-ChildItem $installerDir -Filter '*-Setup.exe' | Select-Object -First 1
    Copy-Item $setup.FullName (Join-Path $installerDir 'UFLauncher_installer.exe') -Force
    Set-Content -Path (Join-Path $installerDir 'version') -Value $version -NoNewline -Encoding ascii
    $uploads += (Join-Path $installerDir 'UFLauncher_installer.exe'), (Join-Path $installerDir 'version')
    Write-Host "Installer: $(Join-Path $installerDir 'UFLauncher_installer.exe')"
}

if ($Upload) {
    docker exec $DevServerContainer mkdir -p $RemotePath
    if ($LASTEXITCODE -ne 0) { throw 'could not create the launcher folder in the dev backend' }
    foreach ($file in $uploads) {
        $name = Split-Path -Leaf $file
        docker cp $file "${DevServerContainer}:${RemotePath}/$name.part"
        if ($LASTEXITCODE -ne 0) { throw "docker cp failed for $name" }
        docker exec $DevServerContainer mv -f "$RemotePath/$name.part" "$RemotePath/$name"
        if ($LASTEXITCODE -ne 0) { throw "rename in the dev backend failed for $name" }
        Write-Host "Uploaded to the dev backend: /updateclient/download_file/launcher?file=$name"
    }
}

if ($Run) {
    if ($arguments) { Start-Process $exe -ArgumentList $arguments } else { Start-Process $exe }
}
