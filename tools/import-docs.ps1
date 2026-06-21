<#
.SYNOPSIS
  Import Isaac modding documentation from a local source into embedded resources.

.DESCRIPTION
  Copies markdown documentation from a user-supplied source directory into
  src/IsaacAgent.Rag/Resources/docs/ so it gets embedded into the assembly
  at build time. The source directory is expected to contain the Isaac
  modding community documentation (MkDocs Material format) with the
  following layout:
    <SourceDir>\IsaacDocs\docs\        — vanilla API docs
    <SourceDir>\REPENTOGON\docs\docs\  — REPENTOGON extension docs

  This script is build-time only and does NOT ship in the distributed binary.
  It takes the source path as a parameter — no paths are hardcoded.

.PARAMETER SourceDir
  Root directory containing the documentation to import. Expected layout:
    <SourceDir>\IsaacDocs\docs\        — vanilla API docs (MkDocs)
    <SourceDir>\REPENTOGON\docs\docs\  — REPENTOGON extension docs (MkDocs)

.PARAMETER Force
  Overwrite existing files in the target directory.

.EXAMPLE
  .\tools\import-docs.ps1 -SourceDir "C:\path\to\docs\root"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourceDir,

    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$targetRoot = Join-Path $scriptRoot '..\src\IsaacAgent.Rag\Resources\docs'

$sources = @(
    @{ Name = 'vanilla';    Src = Join-Path $SourceDir 'IsaacDocs\docs';       Dst = Join-Path $targetRoot 'vanilla' }
    @{ Name = 'repentogon'; Src = Join-Path $SourceDir 'REPENTOGON\docs\docs'; Dst = Join-Path $targetRoot 'repentogon' }
)

$totalFiles = 0

foreach ($src in $sources) {
    if (-not (Test-Path $src.Src)) {
        Write-Warning "Source not found, skipping $($src.Name): $($src.Src)"
        continue
    }

    if ((Test-Path $src.Dst) -and -not $Force) {
        Write-Warning "Target exists, skipping (use -Force to overwrite): $($src.Dst)"
        continue
    }

    if (Test-Path $src.Dst) {
        Remove-Item $src.Dst -Recurse -Force
    }
    New-Item -ItemType Directory -Path $src.Dst -Force | Out-Null

    $files = Get-ChildItem $src.Src -Recurse -File -Filter '*.md'
    $count = 0

    foreach ($file in $files) {
        $relative = $file.FullName.Substring($src.Src.Length).TrimStart('\', '/')
        $destFile = Join-Path $src.Dst $relative
        $destDir = Split-Path -Parent $destFile
        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }
        Copy-Item $file.FullName -Destination $destFile -Force
        $count++
    }

    Write-Host "Imported $count files for $($src.Name) -> $($src.Dst)"
    $totalFiles += $count
}

Write-Host "`nTotal imported: $totalFiles markdown files"
Write-Host "Target: $targetRoot"
