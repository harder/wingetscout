# Publish Scout in WinGet

The community package ID is `Harder.WinGetScout`. Submit the **portable ZIP** for each architecture. Each ZIP contains `wingetscout.exe` and the WinGet in-process COM DLLs in a stable `wingetscout/` folder. This path matters because scheduled update checks record the executable's absolute path. The standalone EXE lacks the COM DLLs; signed MSIX can be added later after testing.

The installer manifest has `InstallerType: zip`, `NestedInstallerType: portable`, `PortableCommandAlias: wingetscout`, and `ArchiveBinariesDependOnPath: true`. WinGet installs the files in its managed per-user portable package directory and adds the executable's directory to the user `PATH`, so a new terminal can run `wingetscout` from anywhere. WinGet verifies the ZIP's SHA-256 hash; it does **not** provide a publisher signature or remove all Windows reputation warnings. No separate installer is needed.

1. Merge and release a new Scout version with a matching `WinGetScout.csproj` `<Version>` and `v<version>` tag. The release workflow builds and attests the ZIPs, publishes an immutable GitHub Release, downloads the published ZIPs back, and uploads a `winget-manifests-<version>` workflow artifact containing the three-file manifest set.
2. Download that artifact from the successful release workflow run. The files are under `manifests/h/Harder/WinGetScout/<version>/`. To regenerate directly from the published ZIPs and validate in one command on Windows, run:

   ```powershell
   pwsh ./packaging/prepare-winget-submission.ps1 -Version 0.1.4
   ```

   The helper needs `gh`, `winget`, and a GitHub login with access to the public release. It downloads the exact published assets and uses [new-winget-manifest.ps1](new-winget-manifest.ps1) to check the archive layout and compute SHA-256 values. The release workflow also verifies its published ZIPs are byte-for-byte identical to its build artifacts. Before submitting, inspect the generated URLs and hashes against the GitHub Release.
3. On Windows, run `winget validate --manifest <manifest-directory>` if using the workflow artifact (the helper runs this for you). Enable local manifests with `winget settings --enable LocalManifestFiles`, then test `winget install --manifest <manifest-directory>` in Windows Sandbox if available. Open a **new terminal** and run `Get-Command wingetscout` and `wingetscout --smoke`; confirm the COM backend on a real interactive run. Check `winget list -e --id Harder.WinGetScout`, then `winget uninstall -e --id Harder.WinGetScout`. Test an upgrade between two released versions once a second version exists. Do not run install tests against an existing Scout data folder without backing it up.
4. Submit **only the three manifest YAML files** for one version in a PR to [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs). Use the generated path `manifests/h/Harder/WinGetScout/<version>/`. Follow the repository CLA and bot validation. Do not add the manifest files directly to the Scout release or advertise the WinGet command until that PR is accepted.
5. For later releases, generate the next version's manifests and submit them to the same package ID. The `Harder.WinGetScout` package can later add signed MSIX installers only after their certificate subject, signature hash, identity, and upgrade behavior have been validated.

The GitHub release must remain immutable after generating checksums. The release workflow rejects published-tag reruns and will not overwrite release assets. Replacing ZIP assets at an existing tag invalidates the community manifest; publish a new version instead. See [Microsoft's authoring and validation guidance](https://github.com/microsoft/winget-pkgs/blob/master/doc/Authoring.md).
