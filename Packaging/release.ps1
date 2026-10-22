[CmdletBinding()]
param(
    [string]$Version,
    # Old way: copy the files to the PC that runs the backend over scp/ssh.
    [switch]$Upload,
    [string]$Server = 'root@192.168.100.148',
    [string]$RemotePath = '/var/lib/docker/volumes/compose_compose_modpack_data/_data/launcher',
    # New way (used by the GitHub workflow): PUT every file to the backend, e.g. https://unrealfrontiers.com.
    # The key is the backend's LAUNCHER_PUBLISH_KEY (it can publish launcher files and nothing else).
    [string]$PublishUrl,
    [string]$PublishKey = $env:LAUNCHER_PUBLISH_KEY,
    # Backend to take the previous release from, so Velopack can make delta packages (a fresh runner has no Releases folder).
    [string]$FetchPrevious
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

function Get-LauncherFileUrl([string]$base, [string]$name) {
    "$($base.TrimEnd('/'))/updateclient/download_file/launcher?file=$([uri]::EscapeDataString($name))"
}

New-Item -ItemType Directory -Force $releasesDir | Out-Null
if ($FetchPrevious) {
    $feedFile = Join-Path $releasesDir 'releases.win.json'
    try {
        Invoke-WebRequest (Get-LauncherFileUrl $FetchPrevious 'releases.win.json') -OutFile $feedFile -UseBasicParsing
        $previous = Get-Content $feedFile -Raw | ConvertFrom-Json
        foreach ($asset in $previous.Assets | Where-Object { $_.Type -eq 'Full' }) {
            $target = Join-Path $releasesDir $asset.FileName
            if (-not (Test-Path $target)) {
                Write-Host "Fetching previous package $($asset.FileName)"
                Invoke-WebRequest (Get-LauncherFileUrl $FetchPrevious $asset.FileName) -OutFile $target -UseBasicParsing
            }
        }
    }
    catch {
        Write-Host "No previous release to build deltas from ($($_.Exception.Message)): full packages only."
        Remove-Item $feedFile -ErrorAction SilentlyContinue
    }
}

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

if (-not $Upload -and -not $PublishUrl) {
    Write-Host "Done. Files are in $releasesDir (run with -Upload, or -PublishUrl and -PublishKey, to publish)."
    return
}

$packages = Get-ChildItem $releasesDir -Filter '*.nupkg'
$feeds = Get-ChildItem $releasesDir | Where-Object { $_.Name -like 'releases.*.json' -or $_.Name -eq 'RELEASES' }
$installers = Get-ChildItem $releasesDir | Where-Object { $_.Name -like '*-Setup.exe' -or $_.Name -eq 'UFLauncher_installer.exe' }

# Packages first, feeds and the version marker last, so clients never see a feed that points at a missing file.
$ordered = @($packages) + @($installers) + @($feeds) + @(Get-Item (Join-Path $releasesDir 'version'))

if ($PublishUrl) {
    if (-not $PublishKey) { throw 'PublishKey (LAUNCHER_PUBLISH_KEY) is not set' }
    foreach ($file in $ordered) {
        Write-Host "Publishing $($file.Name) ($([math]::Round($file.Length / 1MB, 1)) MB)"
        # curl streams the file; the key goes in a header file so it never shows up in a process list or a log.
        $headers = New-TemporaryFile
        try {
            Set-Content -Path $headers -Value "x-api-key: $PublishKey" -Encoding ascii
            curl.exe --silent --show-error --fail --retry 3 --retry-delay 5 -X PUT -H "@$headers" -T $file.FullName "$($PublishUrl.TrimEnd('/'))/updateclient/launcher/$($file.Name)"
            if ($LASTEXITCODE -ne 0) { throw "publishing failed for $($file.Name)" }
        }
        finally { Remove-Item $headers -ErrorAction SilentlyContinue }
        Write-Host ''
    }
    Write-Host 'Publish complete.'
    return
}

foreach ($file in $ordered) {
    Write-Host "Uploading $($file.Name)"
    scp $file.FullName "${Server}:${RemotePath}/$($file.Name).part"
    if ($LASTEXITCODE -ne 0) { throw "scp failed for $($file.Name)" }
    ssh $Server "mv -f '${RemotePath}/$($file.Name).part' '${RemotePath}/$($file.Name)'"
    if ($LASTEXITCODE -ne 0) { throw "remote rename failed for $($file.Name)" }
}

Write-Host 'Upload complete.'
