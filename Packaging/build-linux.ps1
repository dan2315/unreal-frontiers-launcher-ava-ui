[CmdletBinding()]
param(
    [string]$Version,
    # DEV launcher: UnrealFrontiersLauncherDev, test mode, orange icon, takes its backend from server-url.txt.
    [switch]$Dev,
    [string]$ServerUrl,
    # Backend to take the previous release from, so Velopack can make delta packages.
    [string]$FetchPrevious,
    # CI: PUT the files to a backend with its LAUNCHER_PUBLISH_KEY (the same way release.ps1 does for Windows).
    [string]$PublishUrl,
    [string]$PublishKey = $env:LAUNCHER_PUBLISH_KEY,
    [string]$OutputDir
)

# Builds the Linux launcher (win-x64 is release.ps1 / build-dev.ps1): a self-contained linux-x64 build packed by Velopack into
#   <App>.AppImage                          what the user runs (it updates itself, channel "linux")
#   <App>-<version>-linux-full.nupkg (+ delta)   the update packages
#   releases.linux.json, assets.linux.json, RELEASES-linux   the update feed (published last)
#   install.sh                              downloads the AppImage, adds the menu entry and the uflauncher:// handler
# Run it on Linux (Velopack makes AppImages only there; the Ubuntu runner also needs squashfs-tools).

$ErrorActionPreference = 'Stop'

$root = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$repo = Split-Path -Parent $root
$project = Join-Path $repo 'unreal-frontiers-launcher.csproj'

$releaseVersion = if ($Version) { $Version } else { ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1 }
if (-not $releaseVersion) { throw "No <Version> found in $project" }

$packId = if ($Dev) { 'UnrealFrontiersLauncherDev' } else { 'UnrealFrontiersLauncher' }
$packTitle = if ($Dev) { 'Unreal Frontiers DEV' } else { 'Unreal Frontiers' }
$icon = Join-Path $repo (if ($Dev) { 'Assets/Images/logo-dev.png' } else { 'Assets/Images/logo_app.png' })
$publishDir = Join-Path $root 'publish/linux'
$releasesDir = if ($OutputDir) { $OutputDir } else { Join-Path $root 'Releases-linux' }
Write-Host "Building $packId $releaseVersion for linux-x64"

function Get-LauncherFileUrl([string]$base, [string]$name) {
    "$($base.TrimEnd('/'))/updateclient/download_file/launcher?file=$([uri]::EscapeDataString($name))"
}

New-Item -ItemType Directory -Force $releasesDir | Out-Null
if ($FetchPrevious) {
    $feedFile = Join-Path $releasesDir 'releases.linux.json'
    try {
        Invoke-WebRequest (Get-LauncherFileUrl $FetchPrevious 'releases.linux.json') -OutFile $feedFile -UseBasicParsing
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
        Write-Host "No previous Linux release to build deltas from ($($_.Exception.Message)): full packages only."
        Remove-Item $feedFile -ErrorAction SilentlyContinue
    }
}

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
$devArgs = if ($Dev) { @('-p:DevBuild=true') } else { @() }
dotnet publish $project -c Release -r linux-x64 --self-contained @devArgs `
    -p:PublishReadyToRun=true -p:DebugType=None -p:DebugSymbols=false -p:Version=$releaseVersion -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

if ($Dev -and $ServerUrl) { Set-Content -Path (Join-Path $publishDir 'server-url.txt') -Value $ServerUrl -NoNewline -Encoding ascii }

# vpk names the AppImage after the pack id; the previous full packages in $releasesDir are used for the deltas.
vpk '[linux]' pack --packId $packId --packVersion $releaseVersion --packDir $publishDir --mainExe $packId `
    --packTitle $packTitle --icon $icon --categories Game --outputDir $releasesDir
if ($LASTEXITCODE -ne 0) { throw 'vpk pack failed' }

# install.sh for this build: where to download from and which AppImage.
$base = if ($Dev -and $ServerUrl) { $ServerUrl } elseif ($PublishUrl) { $PublishUrl } else { 'https://unrealfrontiers.com' }
(Get-Content (Join-Path $root 'linux/install.sh') -Raw).Replace('@BASE_URL@', $base.TrimEnd('/')).Replace('@APP@', $packId).Replace('@TITLE@', $packTitle) |
    Set-Content (Join-Path $releasesDir 'install.sh') -NoNewline -Encoding utf8NoBOM

$new = Get-ChildItem $releasesDir | Where-Object { $_.Name -like "$packId-$releaseVersion-*" }
$appImage = Get-ChildItem $releasesDir -Filter "$packId.AppImage"
$feeds = Get-ChildItem $releasesDir | Where-Object { $_.Name -in 'releases.linux.json', 'assets.linux.json', 'RELEASES-linux' }
$installScript = Get-Item (Join-Path $releasesDir 'install.sh')
# Packages first, the feed last, so clients never see a feed that points at a missing file.
$ordered = @($new) + @($appImage) + @($installScript) + @($feeds)
Write-Host 'Files:'; $ordered | ForEach-Object { Write-Host "  $($_.Name) ($([math]::Round($_.Length / 1MB, 2)) MB)" }

if (-not $PublishUrl) {
    Write-Host "Done. Files are in $releasesDir (add -PublishUrl and -PublishKey to publish)."
    return
}

if (-not $PublishKey) { throw 'PublishKey (LAUNCHER_PUBLISH_KEY) is not set' }
foreach ($file in $ordered) {
    Write-Host "Publishing $($file.Name)"
    # The key goes in a header file so it never shows up in a process list or a log.
    $headers = New-TemporaryFile
    try {
        Set-Content -Path $headers -Value "x-api-key: $PublishKey" -Encoding ascii
        curl --silent --show-error --fail --retry 3 --retry-delay 5 -X PUT -H "@$headers" -T $file.FullName "$($PublishUrl.TrimEnd('/'))/updateclient/launcher/$($file.Name)"
        if ($LASTEXITCODE -ne 0) { throw "publishing failed for $($file.Name)" }
    }
    finally { Remove-Item $headers -ErrorAction SilentlyContinue }
    Write-Host ''
}
Write-Host 'Publish complete.'
