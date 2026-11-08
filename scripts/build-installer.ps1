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

# WiX's incremental build doesn't notice a version-only change, and it hard-links its obj\ output
# into the output folder. Reusing either would ship a stale MSI, so always build from scratch.
foreach ($dir in 'obj', 'bin') {
    $path = Join-Path $root "Mapee.Installer\$dir"
    if (Test-Path $path) { Remove-Item $path -Recurse -Force }
}

Invoke-Step "Build installer" {
    dotnet build "$root\Mapee.Installer\Mapee.Installer.wixproj" `
        --configuration Release `
        --no-incremental `
        -p:Version=$Version `
        -p:AppPublishDir=$publishDir `
        --output $installerBuildDir
}

function Invoke-Com($object, [string]$member, [Reflection.BindingFlags]$kind, [object[]]$arguments) {
    $object.GetType().InvokeMember($member, $kind, $null, $object, $arguments)
}

# Reads what matters about a built MSI, the way Windows Installer will see it.
function Get-MsiFacts([string]$path) {
    $installer = New-Object -ComObject WindowsInstaller.Installer
    $com = [System.Collections.Generic.List[object]]::new()
    try {
        $summary = Invoke-Com $installer 'SummaryInformation' GetProperty @($path, 0); $com.Add($summary)
        $wordCount = Invoke-Com $summary 'Property' GetProperty @(15)

        $db = Invoke-Com $installer 'OpenDatabase' InvokeMethod @($path, 0); $com.Add($db)
        $files = Invoke-Com $db 'OpenView' InvokeMethod @('SELECT `File` FROM `File`'); $com.Add($files)
        Invoke-Com $files 'Execute' InvokeMethod $null | Out-Null
        $fileCount = 0
        while ($record = Invoke-Com $files 'Fetch' InvokeMethod $null) { $fileCount++; $com.Add($record) }

        $props = Invoke-Com $db 'OpenView' InvokeMethod @("SELECT ``Value`` FROM ``Property`` WHERE ``Property`` = 'ProductVersion'"); $com.Add($props)
        Invoke-Com $props 'Execute' InvokeMethod $null | Out-Null
        $record = Invoke-Com $props 'Fetch' InvokeMethod $null; $com.Add($record)
        $productVersion = Invoke-Com $record 'StringData' GetProperty @(1)

        # Load the package like msiexec does, without installing anything (1 = ignore machine state).
        $opens = $true
        try { $com.Add((Invoke-Com $installer 'OpenPackage' InvokeMethod @($path, 1))) } catch { $opens = $false }

        [pscustomobject]@{ Opens = $opens; ProductVersion = $productVersion; WordCount = $wordCount; FileCount = $fileCount }
    }
    finally {
        # Release everything now, or the MSI stays locked and the next build can't replace it.
        foreach ($o in @($com) + @($installer)) { if ($o) { [Runtime.InteropServices.Marshal]::ReleaseComObject($o) | Out-Null } }
        [GC]::Collect()
        [GC]::WaitForPendingFinalizers()
    }
}

$msi = Join-Path $OutputDir "$baseName.msi"
$zip = Join-Path $OutputDir "$baseName.zip"
Copy-Item (Join-Path $installerBuildDir 'Mapee.msi') $msi -Force

# Check the MSI that ships before packaging it.
$facts = Get-MsiFacts $msi
$expectedFiles = (Get-ChildItem $publishDir -Recurse -File).Count + 1   # every published file plus Icon.ico
$problems = @(
    if (-not $facts.Opens) { 'Windows Installer cannot open it.' }
    if ($facts.ProductVersion -ne $Version) { "It says version '$($facts.ProductVersion)' instead of $Version (a stale build?)." }
    if ($facts.WordCount -ne 2) { "Its Word Count is '$($facts.WordCount)' instead of 2 (compressed, needs elevation)." }
    if ($facts.FileCount -ne $expectedFiles) { "It contains $($facts.FileCount) files instead of $expectedFiles. Check the Files element in Mapee.Installer\Package.wxs." }
)
if ($problems) { throw "The built installer is broken:`n  " + ($problems -join "`n  ") }
Write-Host "Installer checks passed: opens in Windows Installer, version $($facts.ProductVersion), $($facts.FileCount) files, requires elevation."

Compress-Archive -Path $msi -DestinationPath $zip -CompressionLevel Optimal -Force

Write-Host ''
Write-Host 'Release files:' -ForegroundColor Green
Get-Item $msi, $zip | ForEach-Object { '  {0}  ({1:N1} MB)' -f $_.FullName, ($_.Length / 1MB) }
