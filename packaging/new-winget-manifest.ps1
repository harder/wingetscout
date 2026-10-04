<#
.SYNOPSIS
  Generate the three community-repository manifests for a published Scout release.
.DESCRIPTION
  Uses the complete portable ZIPs, checks their inner COM layout, and hashes the
  exact files that the immutable version-tag URLs will serve. Submit the output
  separately to microsoft/winget-pkgs after testing installation on Windows.
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)] [ValidatePattern('^\d+\.\d+\.\d+$')] [string]$Version,
  [Parameter(Mandatory)] [string]$AssetDirectory,
  [string]$OutputDirectory = (Join-Path $PSScriptRoot 'winget-manifests')
)

$ErrorActionPreference = 'Stop'
$packageId = 'Harder.WinGetScout'
$assetRoot = (Resolve-Path -LiteralPath $AssetDirectory).Path
$destination = Join-Path $OutputDirectory "manifests/h/Harder/WinGetScout/$Version"
New-Item -ItemType Directory -Path $destination -Force | Out-Null

$installers = foreach ($arch in @('x64', 'arm64')) {
  $bundle = "wingetscout-$Version-win-$arch"
  $zip = Join-Path $assetRoot "$bundle.zip"
  if (-not (Test-Path -LiteralPath $zip)) { throw "Missing release ZIP: $zip" }

  Add-Type -AssemblyName System.IO.Compression
  $archive = [IO.Compression.ZipFile]::OpenRead($zip)
  try {
    $entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
    foreach ($required in @('wingetscout.exe', 'WindowsPackageManager.dll',
                            'Microsoft.Management.Deployment.InProc.dll')) {
      if ($entries -cnotcontains "$bundle/$required") {
        throw "$zip is missing $bundle/$required; it cannot be submitted as the COM package"
      }
    }
  }
  finally { $archive.Dispose() }

  $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
  [pscustomobject]@{ Arch = $arch; Bundle = $bundle; Hash = $hash }
}

$versionManifest = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.1.12.0.schema.json
PackageIdentifier: $packageId
PackageVersion: $Version
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.12.0
"@

$localeManifest = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.1.12.0.schema.json
PackageIdentifier: $packageId
PackageVersion: $Version
PackageLocale: en-US
Publisher: Kevin Harder
PublisherUrl: https://github.com/harder
PackageName: Scout for WinGet
PackageUrl: https://github.com/harder/wingetscout
License: MIT
LicenseUrl: https://github.com/harder/wingetscout/blob/v$Version/LICENSE
ShortDescription: Terminal app for finding, installing, and updating Windows packages
Description: Scout for WinGet provides search, package details, installs, upgrades, pins, and update checks through the WinGet COM and CLI backends.
ReleaseNotesUrl: https://github.com/harder/wingetscout/releases/tag/v$Version
Tags:
- winget
- package-manager
- terminal
ManifestType: defaultLocale
ManifestVersion: 1.12.0
"@

$installerManifest = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.1.12.0.schema.json
PackageIdentifier: $packageId
PackageVersion: $Version
InstallerType: zip
NestedInstallerType: portable
ArchiveBinariesDependOnPath: true
Commands:
- wingetscout
Installers:
"@
foreach ($item in $installers) {
  $installerManifest += @"

- Architecture: $($item.Arch)
  NestedInstallerFiles:
  - RelativeFilePath: $($item.Bundle)/wingetscout.exe
    PortableCommandAlias: wingetscout
  InstallerUrl: https://github.com/harder/wingetscout/releases/download/v$Version/$($item.Bundle).zip
  InstallerSha256: $($item.Hash)
"@
}
$installerManifest += "`nManifestType: installer`nManifestVersion: 1.12.0`n"

Set-Content -LiteralPath (Join-Path $destination "$packageId.yaml") -Value $versionManifest -Encoding utf8
Set-Content -LiteralPath (Join-Path $destination "$packageId.locale.en-US.yaml") -Value $localeManifest -Encoding utf8
Set-Content -LiteralPath (Join-Path $destination "$packageId.installer.yaml") -Value $installerManifest -Encoding utf8
Write-Host "WinGet manifests ready in $destination"
