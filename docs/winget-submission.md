# Publish Scout in WinGet

The community package ID is `Harder.WinGetScout`. Submit the **portable ZIP** for each architecture. Each ZIP contains `wingetscout.exe` and the WinGet in-process COM DLLs in a stable `wingetscout/` folder. This path matters because scheduled update checks record the executable's absolute path. The standalone EXE lacks the COM DLLs; signed MSIX can be added later after testing.

The installer manifest has `InstallerType: zip`, `NestedInstallerType: portable`, `PortableCommandAlias: wingetscout`, and `ArchiveBinariesDependOnPath: true`. WinGet installs the files in its managed per-user portable package directory and adds the executable's directory to the user `PATH`, so a new terminal can run `wingetscout` from anywhere. WinGet verifies the ZIP's SHA-256 hash; it does **not** provide a publisher signature or remove all Windows reputation warnings. No separate installer is needed.

1. Merge and release a new Scout version with a matching `WinGetScout.csproj` `<Version>` and `v<version>` tag. The release workflow builds and attests the ZIPs, publishes an immutable GitHub Release, downloads the published ZIPs back, and uploads a `winget-manifests-<version>` workflow artifact containing the three-file manifest set.
2. Download that artifact from the successful release workflow run. The files are under `manifests/h/Harder/WinGetScout/<version>/`. To regenerate directly from the published ZIPs and validate in one command on Windows, run:

   ```powershell
   $version = '1.0.1' # replace with the published release version
   pwsh ./packaging/prepare-winget-submission.ps1 -Version $version
   ```

   The helper needs `gh`, `winget`, and a GitHub login with access to the public release. It downloads the exact published assets and uses [new-winget-manifest.ps1](../packaging/new-winget-manifest.ps1) to check the archive layout and compute SHA-256 values. The release workflow also verifies its published ZIPs are byte-for-byte identical to its build artifacts. Before submitting, inspect the generated URLs and hashes against the GitHub Release.
3. Test in Windows Sandbox. A clean Sandbox does not include WinGet, so first install it in a Sandbox PowerShell window using [Microsoft's documented bootstrap](https://learn.microsoft.com/en-us/windows/package-manager/winget/#install-winget-on-windows-sandbox):

   ```powershell
   $ProgressPreference = 'SilentlyContinue'
   Install-PackageProvider -Name NuGet -Force | Out-Null
   Install-Module -Name Microsoft.WinGet.Client -Force -Repository PSGallery | Out-Null
   Repair-WinGetPackageManager -AllUsers
   winget --version
   ```

   Make the three YAML files available inside Sandbox, then run:

   ```powershell
   $manifest = 'C:\ScoutInput\manifests' # change to the mapped manifest directory
   winget validate --manifest $manifest
   winget settings --enable LocalManifestFiles
   winget install --manifest $manifest --accept-package-agreements --accept-source-agreements --disable-interactivity
   ```

   Open a **new terminal** and run `Get-Command wingetscout`, `wingetscout --smoke`, and `wingetscout --comdiag`. Before the package enters the community source, a local-manifest install is registered under WinGet's temporary `DefaultSource`. In the new terminal, set `$manifest` to the mapped directory again, then run `winget list --name 'Scout for WinGet' --exact` and `winget uninstall --manifest $manifest --disable-interactivity`. Confirm that `Get-Command wingetscout` no longer finds the alias. The final community package ID, `Harder.WinGetScout`, becomes usable with `winget list -e --id Harder.WinGetScout` and `winget uninstall -e --id Harder.WinGetScout` **after** the package is published in WinGet. Do not run install tests against an existing Scout data folder without backing it up.
4. Submit **only the three manifest YAML files** for one version in a PR to [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs). Use the generated path `manifests/h/Harder/WinGetScout/<version>/`. Fork the repository, create a branch based on its current `master`, copy the three files into that path, and open a PR with the title `New package: Harder.WinGetScout version <version>`. Check for an existing PR before opening another. Follow the repository CLA and [validation checks](https://github.com/microsoft/winget-pkgs/blob/master/doc/Validation.md); review the PR's Checks tab and respond to any bot feedback. Do not add the manifest files directly to the Scout release or advertise the WinGet command until that PR is accepted.

5. For later releases, generate the next version's manifests and submit them to the same package ID. The `Harder.WinGetScout` package can later add signed MSIX installers only after their certificate subject, signature hash, identity, and upgrade behavior have been validated.

The GitHub release must remain immutable after generating checksums. The release workflow stages all assets as a draft, checks their SHA-256 digests, and then publishes the complete release. It rejects reruns whenever a release for the tag already exists, including a draft left by a failed run. Inspect and remove an incomplete draft before retrying the tag; never replace assets on a published release, since that invalidates the community manifest. Publish a new version instead. See [Microsoft's authoring and validation guidance](https://github.com/microsoft/winget-pkgs/blob/master/doc/Authoring.md).
