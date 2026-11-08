# Clears the "elevated privileges are not required" bit (8) of an MSI's Word Count summary
# property. See the RequireElevation target in Mapee.Installer.wixproj for why.
param([Parameter(Mandatory)][string]$Path)
$ErrorActionPreference = 'Stop'

$PID_WORDCOUNT = 15
$NO_ELEVATION_BIT = 8

$installer = New-Object -ComObject WindowsInstaller.Installer
$summary = $installer.GetType().InvokeMember('SummaryInformation', 'GetProperty', $null, $installer, @((Resolve-Path $Path).Path, 1))
$wordCount = [int]$summary.GetType().InvokeMember('Property', 'GetProperty', $null, $summary, @($PID_WORDCOUNT))
$newWordCount = $wordCount -band (-bnot $NO_ELEVATION_BIT)

if ($newWordCount -ne $wordCount) {
    $summary.GetType().InvokeMember('Property', 'SetProperty', $null, $summary, @($PID_WORDCOUNT, $newWordCount))
    $summary.GetType().InvokeMember('Persist', 'InvokeMethod', $null, $summary, $null) | Out-Null
}
[System.Runtime.InteropServices.Marshal]::ReleaseComObject($summary) | Out-Null
Write-Host "MSI Word Count: $wordCount -> $newWordCount (elevation required)"
