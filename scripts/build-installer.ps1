<#
.SYNOPSIS
    Publishes Mapee and builds the installer, exactly like the Release workflow does.

.DESCRIPTION
    1. dotnet publish (self-contained, single-file, ReadyToRun, win-x64) with the given version.
       The publish folder is also the portable app: copy it anywhere and run Mapee.exe.
    2. Builds the WiX MSI from the publish folder
    3. Writes the release files to the output folder:
         MapeeInstaller.<version>-self-win64.msi
         MapeeInstaller.<version>-self-win64.zip   (the MSI, zipped, for the website)

.EXAMPLE
    ./scripts/build-installer.ps1 -Version 1.4.0
#>
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [string]$OutputDir = (Join-Path $PSScriptRoot '..\artifacts')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$OutputDir = [IO.Path]::GetFullPath($OutputDir)
$publishDir = Join-Path $OutputDir 'publish'
$installerBuildDir = Join-Path $OutputDir 'installer'
$baseName = "MapeeInstaller.$Version-self-win64"

# Windows Installer only supports major.minor.build up to 255.255.65535.
[int[]]$parts = $Version.Split('.')
if ($parts[0] -gt 255 -or $parts[1] -gt 255 -or $parts[2] -gt 65535) {
    throw "Version $Version is outside what an MSI supports (max 255.255.65535)."
}

foreach ($dir in $publishDir, $installerBuildDir) {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
}
New-Item -ItemType Directory -Force $OutputDir | Out-Null

function Invoke-Step([string]$name, [scriptblock]$command) {
    Write-Host "==> $name" -ForegroundColor Cyan
    & $command
    if ($LASTEXITCODE -ne 0) { throw "$name failed (exit code $LASTEXITCODE)." }
}

# Mapper.Gui.csproj puts build output under $(SolutionDir)Build\, so give it the repo root
# (via the environment, which avoids command-line quoting problems with trailing backslashes).
$previousSolutionDir = $env:SolutionDir
$env:SolutionDir = "$root\"
try {
    # Self-contained single-file exe (users don't need .NET installed), precompiled with
    # ReadyToRun for faster startup. Native WPF DLLs and Resources\ stay next to the exe.
    Invoke-Step "Publish Mapee $Version" {
        dotnet publish "$root\Mapper.Gui\Mapper.Gui.csproj" `
            --configuration Release `
            --runtime win-x64 `
            --self-contained true `
            -p:PublishSingleFile=true `
            -p:PublishReadyToRun=true `
            -p:Version=$Version `
            --output $publishDir
    }
}
finally {
    $env:SolutionDir = $previousSolutionDir
}

Invoke-Step "Build installer" {
    dotnet build "$root\Mapee.Installer\Mapee.Installer.wixproj" `
        --configuration Release `
        -p:Version=$Version `
        -p:AppPublishDir=$publishDir `
        --output $installerBuildDir
}

function Get-MsiFileCount([string]$path) {
    $installer = New-Object -ComObject WindowsInstaller.Installer
    $db = $installer.GetType().InvokeMember('OpenDatabase', 'InvokeMethod', $null, $installer, @($path, 0))
    $view = $db.GetType().InvokeMember('OpenView', 'InvokeMethod', $null, $db, @('SELECT `File` FROM `File`'))
    $view.GetType().InvokeMember('Execute', 'InvokeMethod', $null, $view, $null) | Out-Null
    $count = 0
    while ($view.GetType().InvokeMember('Fetch', 'InvokeMethod', $null, $view, $null)) { $count++ }
    $view.GetType().InvokeMember('Close', 'InvokeMethod', $null, $view, $null) | Out-Null
    # Release the database now, or the MSI stays locked and the next build can't replace it.
    foreach ($com in $view, $db, $installer) { [Runtime.InteropServices.Marshal]::ReleaseComObject($com) | Out-Null }
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
    $count
}

# Guard against shipping an installer that's missing files (e.g. if the publish path in
# Package.wxs ever stops matching). The MSI holds every published file plus Icon.ico.
$builtMsi = Join-Path $installerBuildDir 'Mapee.msi'
$expected = (Get-ChildItem $publishDir -Recurse -File).Count + 1
$actual = Get-MsiFileCount $builtMsi
if ($actual -ne $expected) {
    throw "The installer contains $actual files but $expected were expected. Check the Files element in Mapee.Installer\Package.wxs."
}
Write-Host "Installer contains all $actual files."

$msi = Join-Path $OutputDir "$baseName.msi"
$zip = Join-Path $OutputDir "$baseName.zip"
Copy-Item $builtMsi $msi -Force
Compress-Archive -Path $msi -DestinationPath $zip -CompressionLevel Optimal -Force

Write-Host ''
Write-Host 'Release files:' -ForegroundColor Green
Get-Item $msi, $zip | ForEach-Object { '  {0}  ({1:N1} MB)' -f $_.FullName, ($_.Length / 1MB) }
