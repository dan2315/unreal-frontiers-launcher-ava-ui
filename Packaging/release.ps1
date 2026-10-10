[CmdletBinding()]
param(
    [string]$Version,
    [switch]$Upload,
    [string]$Server = 'root@192.168.100.148',
    [string]$RemotePath = '/var/lib/docker/volumes/compose_compose_modpack_data/_data/launcher'
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo 'unreal-frontiers-launcher.csproj'
$publishDir = Join-Path $PSScriptRoot 'publish\win-x64'
$releasesDir = Join-Path $PSScriptRoot 'Releases'
$packId = 'UnrealFrontiersLauncher'
$packTitle = 'Unreal Frontiers'
$mainExe = 'UnrealFrontiersLauncher.exe'

$releaseVersion = if ($Version) { $Version } else { ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1 }
if (-not $releaseVersion) { throw "No <Version> found in $project" }
Write-Host "Releasing $packId $releaseVersion"

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $project -c Release -r win-x64 --self-contained `
    -p:PublishReadyToRun=true -p:DebugType=None -p:DebugSymbols=false -p:Version=$releaseVersion -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

# Previous full packages in $releasesDir are used to generate delta updates.
vpk pack --packId $packId --packVersion $releaseVersion --packDir $publishDir --mainExe $mainExe `
    --runtime win-x64 --packTitle $packTitle --icon (Join-Path $repo 'Assets\Images\logo.ico') --outputDir $releasesDir
if ($LASTEXITCODE -ne 0) { throw 'vpk pack failed' }

# Launchers installed before Velopack look for "version" and run UFLauncher_installer.exe.
$setup = Get-ChildItem $releasesDir -Filter '*-Setup.exe' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Copy-Item $setup.FullName (Join-Path $releasesDir 'UFLauncher_installer.exe') -Force
Set-Content -Path (Join-Path $releasesDir 'version') -Value $releaseVersion -NoNewline -Encoding ascii

if (-not $Upload) {
    Write-Host "Done. Files are in $releasesDir (run with -Upload to publish)."
    return
}

$packages = Get-ChildItem $releasesDir -Filter '*.nupkg'
$feeds = Get-ChildItem $releasesDir | Where-Object { $_.Name -like 'releases.*.json' -or $_.Name -eq 'RELEASES' }
$installers = Get-ChildItem $releasesDir | Where-Object { $_.Name -like '*-Setup.exe' -or $_.Name -eq 'UFLauncher_installer.exe' }

# Packages first, feeds and the version marker last, so clients never see a feed that points at a missing file.
$ordered = @($packages) + @($installers) + @($feeds) + @(Get-Item (Join-Path $releasesDir 'version'))
foreach ($file in $ordered) {
    Write-Host "Uploading $($file.Name)"
    scp $file.FullName "${Server}:${RemotePath}/$($file.Name).part"
    if ($LASTEXITCODE -ne 0) { throw "scp failed for $($file.Name)" }
    ssh $Server "mv -f '${RemotePath}/$($file.Name).part' '${RemotePath}/$($file.Name)'"
    if ($LASTEXITCODE -ne 0) { throw "remote rename failed for $($file.Name)" }
}

Write-Host 'Upload complete.'
