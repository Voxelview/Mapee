<#
.SYNOPSIS
    Checks that Mapee can load a Resources folder (the repository's, or an extracted data pack).

.DESCRIPTION
    Runs `Mapee.exe --check-data`, which loads the folder with the same code the app uses at startup:
    every JSON file, style, translator and colormap, plus a check that its format matches the build.
    Fails if anything is missing or invalid.

    Without -AppExe, builds Mapee first (Debug).

.EXAMPLE
    ./scripts/check-data.ps1                                   # the repository's Resources\
.EXAMPLE
    ./scripts/check-data.ps1 -ResourcesDir artifacts\data\pack -AppExe artifacts\publish\Mapee.exe
#>
param(
    [string]$ResourcesDir = (Join-Path $PSScriptRoot '..\Resources'),
    [string]$AppExe
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$ResourcesDir = (Resolve-Path $ResourcesDir).Path

if (-not $AppExe) {
    Write-Host '==> Build Mapee' -ForegroundColor Cyan
    $previousSolutionDir = $env:SolutionDir
    $env:SolutionDir = "$root\"
    try {
        dotnet build "$root\Mapper.Gui\Mapper.Gui.csproj" --configuration Debug -v:q -nologo
        if ($LASTEXITCODE -ne 0) { throw "Building Mapee failed (exit code $LASTEXITCODE)." }
    }
    finally {
        $env:SolutionDir = $previousSolutionDir
    }
    $AppExe = Join-Path $root 'Build\Debug\Mapee.exe'
}
$AppExe = (Resolve-Path $AppExe).Path

Write-Host "==> Check $ResourcesDir" -ForegroundColor Cyan
$report = [IO.Path]::GetTempFileName()
try {
    # Mapee.exe is a Windows app, so wait for it explicitly and read its report from a file.
    $process = Start-Process -FilePath $AppExe -ArgumentList '--check-data', "`"$ResourcesDir`"", '--output', "`"$report`"" -Wait -PassThru -NoNewWindow
    if (Test-Path $report) { Get-Content $report | Write-Host }
    if ($process.ExitCode -ne 0) { throw "Mapee can't load this data (exit code $($process.ExitCode))." }
}
finally {
    Remove-Item $report -ErrorAction SilentlyContinue
}
