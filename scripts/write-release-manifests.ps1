<#
.SYNOPSIS
    Writes the two files that announce a release, from the output of build-installer.ps1.

.DESCRIPTION
    MapeeVersionInfo-self-win64.xml
        Read by the app's updater (AutoUpdater.NET) on every start.
        Points at the MSI on the GitHub release and carries its SHA256 checksum.
    installer-info.xml
        Read by the website (mapee.net) for its download button.
        Points at the zip on the GitHub release.

    The Release workflow uploads both to the Google Cloud Storage bucket as its last step.

.EXAMPLE
    ./scripts/write-release-manifests.ps1 -Version 1.4.0
#>
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [string]$Repository = 'Voxelview/Mapee',

    [string]$ArtifactsDir = (Join-Path $PSScriptRoot '..\artifacts'),

    # Release date shown on the website. Defaults to today (UTC).
    [datetime]$ReleaseDate = [datetime]::UtcNow
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$ArtifactsDir = [IO.Path]::GetFullPath($ArtifactsDir)
$baseName = "MapeeInstaller.$Version-self-win64"
$msi = Get-Item (Join-Path $ArtifactsDir "$baseName.msi")
$zip = Get-Item (Join-Path $ArtifactsDir "$baseName.zip")
$downloadBase = "https://github.com/$Repository/releases/download/$Version"
$outDir = Join-Path $ArtifactsDir 'manifests'
New-Item -ItemType Directory -Force $outDir | Out-Null

function Write-Utf8NoBom([string]$path, [string]$content) {
    [IO.File]::WriteAllText($path, $content, [Text.UTF8Encoding]::new($false))
}
function X([string]$value) { [Security.SecurityElement]::Escape($value) }

$sha256 = (Get-FileHash $msi.FullName -Algorithm SHA256).Hash

Write-Utf8NoBom (Join-Path $outDir 'MapeeVersionInfo-self-win64.xml') @"
<?xml version="1.0" encoding="UTF-8"?>
<item>
    <version>$(X "$Version.0")</version>
    <url>$(X "$downloadBase/$($msi.Name)")</url>
    <mandatory>false</mandatory>
    <checksum algorithm="SHA256">$sha256</checksum>
</item>

"@

Write-Utf8NoBom (Join-Path $outDir 'installer-info.xml') @"
<info>
    <version>$(X $Version)</version>
    <url>$(X "$downloadBase/$($zip.Name)")</url>
    <download-button-upper-text>Installer for Windows 10/11 x64</download-button-upper-text>
    <file-size>$($zip.Length)</file-size>
    <release-date>$($ReleaseDate.ToString('yyyy-M-d'))</release-date>
</info>

"@

Get-ChildItem $outDir | ForEach-Object {
    Write-Host "--- $($_.Name)" -ForegroundColor Cyan
    Get-Content $_.FullName
}
