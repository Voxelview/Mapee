<#
.SYNOPSIS
    Builds a data pack from Resources\, exactly like the "Publish data" workflow does.

.DESCRIPTION
    Writes to the output folder:
      mapee-data-r<revision>.zip   Resources\ with manifest.json stamped with format, revision, commit and date
      latest.json                  What the app reads to find the pack: format, revision, url, sha256, size
      pack\                        The zip's contents, extracted, for scripts/check-data.ps1

    The workflow uploads the zip and latest.json to
    gs://mapee_update_bucket_goldentadpole/<data|data-test>/format-<format>/.

.EXAMPLE
    ./scripts/build-data-pack.ps1 -Revision (./scripts/get-data-revision.ps1)
#>
param(
    # The data revision (scripts/get-data-revision.ps1).
    [Parameter(Mandatory)]
    [ValidateRange(1, [int]::MaxValue)]
    [int]$Revision,

    # live: every user. test: only apps started with MAPEE_DATA_CHANNEL=test.
    [ValidateSet('live', 'test')]
    [string]$Channel = 'live',

    [string]$Commit = '',

    # The folder to pack. Defaults to the repository's Resources\; point it at a copy to try out
    # a change on the test channel without committing it.
    [string]$ResourcesDir = (Join-Path $PSScriptRoot '..\Resources'),

    [string]$OutputDir = (Join-Path $PSScriptRoot '..\artifacts\data')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem

# Must match DataPackUpdater.BucketUrl in the app.
$bucketUrl = 'https://storage.googleapis.com/mapee_update_bucket_goldentadpole'

$ResourcesDir = (Resolve-Path $ResourcesDir).Path
$OutputDir = [IO.Path]::GetFullPath($OutputDir)
$packDir = Join-Path $OutputDir 'pack'
$zipPath = Join-Path $OutputDir "mapee-data-r$Revision.zip"

if (Test-Path $OutputDir) { Remove-Item $OutputDir -Recurse -Force }
New-Item -ItemType Directory -Force $packDir | Out-Null

function Write-Json([string]$path, $value) {
    [IO.File]::WriteAllText($path, ($value | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
}

# Copy Resources\ and stamp its manifest, keeping what the repository's manifest says
# (format, minecraftVersion, ...).
Copy-Item (Join-Path $ResourcesDir '*') $packDir -Recurse
$manifestPath = Join-Path $packDir 'manifest.json'
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$format = [int]$manifest.format
$minecraftVersion = [string]$manifest.minecraftVersion
$created = [DateTimeOffset]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
$stamps = [ordered]@{ revision = $Revision; commit = $Commit; created = $created }
foreach ($name in $stamps.Keys) {
    $manifest | Add-Member -NotePropertyName $name -NotePropertyValue $stamps[$name] -Force
}
Write-Json $manifestPath $manifest

# ZipFile writes '/' separators on every PowerShell version (Compress-Archive in Windows PowerShell doesn't).
[IO.Compression.ZipFile]::CreateFromDirectory($packDir, $zipPath, [IO.Compression.CompressionLevel]::Optimal, $false)
$zip = Get-Item $zipPath

$folder = if ($Channel -eq 'test') { 'data-test' } else { 'data' }
Write-Json (Join-Path $OutputDir 'latest.json') ([ordered]@{
    format           = $format
    revision         = $Revision
    minecraftVersion = $minecraftVersion
    url              = "$bucketUrl/$folder/format-$format/$($zip.Name)"
    sha256           = (Get-FileHash $zip.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    size             = $zip.Length
    commit           = $Commit
    created          = $created
})

Write-Host ("Data pack r{0} (format {1}, Minecraft {2}, {3} channel): {4}  ({5:N2} MB)" -f $Revision, $format, $minecraftVersion, $Channel, $zip.FullName, ($zip.Length / 1MB))
Get-Content (Join-Path $OutputDir 'latest.json')
