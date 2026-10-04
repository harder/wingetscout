# Publish Scout in WinGet

The community package ID is `Harder.WinGetScout`. Submit the **portable ZIP** for each architecture. Each ZIP contains `wingetscout.exe` and the WinGet in-process COM DLLs in a stable `wingetscout/` folder. This path matters because scheduled update checks record the executable's absolute path. The standalone EXE lacks the COM DLLs, and the MSIX requires a trusted signing certificate, so neither is the initial community manifest installer.

1. Merge and release a new Scout version with a matching `WinGetScout.csproj` `<Version>` and `v<version>` tag. The release workflow builds and attests the ZIPs, then uploads a `winget-manifests` workflow artifact containing the generated three-file manifest set.
2. Download the published ZIPs and confirm their version-specific URLs and hashes match the generated installer manifest. The generator can also be run locally:

   ```powershell
   pwsh ./packaging/new-winget-manifest.ps1 -Version 0.1.4 -AssetDirectory ./release-assets
   ```

3. On Windows, validate the generated directory with `winget validate --manifest <manifest-directory>`. Enable local manifests with `winget settings --enable LocalManifestFiles`, then test `winget install --manifest <manifest-directory>` in Windows Sandbox if available. Confirm `wingetscout` starts, reports the COM backend, and that `winget uninstall Harder.WinGetScout` removes the portable package. Do not run install tests against an existing Scout data folder without backing it up.
4. Submit **only the three manifest YAML files** for one version in a PR to [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs). Use the generated path `manifests/h/Harder/WinGetScout/<version>/`. Follow the repository CLA and bot validation. Do not add the manifest files directly to the Scout release or advertise the WinGet command until that PR is accepted.
5. For later releases, generate the next version's manifests and submit them to the same package ID. The `Harder.WinGetScout` package can later add signed MSIX installers only after their certificate subject, signature hash, identity, and upgrade behavior have been validated.

The GitHub release must remain immutable after generating checksums. Replacing ZIP assets at an existing tag invalidates the community manifest. See [Microsoft's authoring and validation guidance](https://github.com/microsoft/winget-pkgs/blob/master/doc/Authoring.md).
