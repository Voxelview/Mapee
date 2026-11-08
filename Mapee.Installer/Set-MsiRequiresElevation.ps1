# Clears the "elevated privileges are not required" bit (8) of an MSI's Word Count summary
# property. See the RequireElevation target in Mapee.Installer.wixproj for why.
param([Parameter(Mandatory)][string]$Path)
$ErrorActionPreference = 'Stop'

$PID_WORDCOUNT = 15
$NO_ELEVATION_BIT = 8

$Path = (Resolve-Path $Path).Path
$installer = New-Object -ComObject WindowsInstaller.Installer

function Open-Summary([int]$updateCount) {
    $installer.GetType().InvokeMember('SummaryInformation', 'GetProperty', $null, $installer, @($Path, $updateCount))
}
function Get-WordCount {
    $summary = Open-Summary 0
    try { [int]$summary.GetType().InvokeMember('Property', 'GetProperty', $null, $summary, @($PID_WORDCOUNT)) }
    finally { [Runtime.InteropServices.Marshal]::ReleaseComObject($summary) | Out-Null }
}

$wordCount = Get-WordCount
$newWordCount = $wordCount -band (-bnot $NO_ELEVATION_BIT)

if ($newWordCount -ne $wordCount) {
    # Only open the summary for writing when it will be saved: releasing a writable summary
    # without Persist() empties it, and Windows Installer then refuses to open the package.
    $summary = Open-Summary 1
    try {
        $summary.GetType().InvokeMember('Property', 'SetProperty', $null, $summary, @($PID_WORDCOUNT, $newWordCount))
        $summary.GetType().InvokeMember('Persist', 'InvokeMethod', $null, $summary, $null) | Out-Null
    }
    finally { [Runtime.InteropServices.Marshal]::ReleaseComObject($summary) | Out-Null }
}

$saved = Get-WordCount
if ($saved -ne $newWordCount) { throw "Failed to update the MSI Word Count (expected $newWordCount, found '$saved')." }
Write-Host "MSI Word Count: $wordCount -> $saved (elevation required)"
