<#
.SYNOPSIS
    Prints the data revision of a commit: how many commits changed Resources\ since revisions were reset.

.DESCRIPTION
    Used by the Release and Publish data workflows, so the bundled data and published packs are numbered
    the same way. Apps download a pack only when its revision is higher than the data they already have.

    Needs the full git history (in workflows: actions/checkout with fetch-depth: 0).

.EXAMPLE
    ./scripts/get-data-revision.ps1          # for HEAD
#>
param([string]$Commit = 'HEAD')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Revisions count the commits that changed Resources\ after this one, so the first is r1 (reset on
# 2026-11-08, at the merge of data packs). Never change it: apps compare revisions, and a lower number
# would make them ignore new packs.
$resetAfter = '2c8c9642bb79db746fdd0784f4f5853bffdf6b5f'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$count = git -C $root rev-list --count "$resetAfter..$Commit" -- Resources
if ($LASTEXITCODE -ne 0) { throw "git rev-list failed. Is the full history available (fetch-depth: 0)?" }
[int]$count
