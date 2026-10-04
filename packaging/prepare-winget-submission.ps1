<#
.SYNOPSIS
  Download a published Scout release and prepare its WinGet community manifests.
.DESCRIPTION
  Uses the released ZIP bytes, generates both architecture entries, and runs
  winget validate. It does not create a PR in microsoft/winget-pkgs.
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)] [ValidatePattern('^\d+\.\d+\.\d+$')] [string]$Version,
  [string]$AssetDirectory = (Join-Path $PSScriptRoot "release-assets/$Version"),
  [string]$OutputDirectory = (Join-Path $PSScriptRoot 'winget-manifests')
)

$ErrorActionPreference = 'Stop'
foreach ($tool in @('gh', 'winget')) {
  if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
    throw "$tool is required to prepare and validate the WinGet submission"
  }
}

New-Item -ItemType Directory -Path $AssetDirectory -Force | Out-Null
& gh release download "v$Version" --repo harder/wingetscout `
  --pattern "wingetscout-$Version-win-*.zip" --dir $AssetDirectory --clobber
if ($LASTEXITCODE -ne 0) { throw "Could not download the published v$Version ZIPs" }

& (Join-Path $PSScriptRoot 'new-winget-manifest.ps1') `
  -Version $Version -AssetDirectory $AssetDirectory -OutputDirectory $OutputDirectory
if (-not $?) { throw 'WinGet manifest generation failed' }

$manifestDirectory = Join-Path $OutputDirectory "manifests/h/Harder/WinGetScout/$Version"
& winget validate --manifest $manifestDirectory
if ($LASTEXITCODE -ne 0) { throw "WinGet validation failed for $manifestDirectory" }

Write-Host "Validated WinGet submission: $manifestDirectory"
Write-Host 'Test install and uninstall in Windows Sandbox, then copy only the three YAML files into microsoft/winget-pkgs.'
