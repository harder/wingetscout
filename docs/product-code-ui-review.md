# Product codes in WinGet interfaces

Reviewed on October 24, 2026, to decide whether Scout should display the list of
installer product codes in its ordinary Package Details pane.

## Comparison

| Interface | Evidence inspected | Product-code presentation and use |
| --- | --- | --- |
| winstall (web) | [7-Zip package page](https://winstall.app/apps/7zip.7zip) | Shows the package ID, publisher, version, license, copyright, description, tags, and install command. No separate product-code field appears on the page. Installs use `winget install -e --id 7zip.7zip`. |
| winget-tui (terminal) | [Detail model](https://github.com/shanselman/winget-tui/blob/main/src/models.rs#L183) and [detail renderer](https://github.com/shanselman/winget-tui/blob/main/src/ui.rs#L436) | The model and detail renderer omit product codes. They expose package identity, versions, publisher, source, license, links, description, and package actions. |
| UniGetUI (desktop) | [Details view model](https://github.com/Devolutions/UniGetUI/blob/main/src/UniGetUI.Avalonia/ViewModels/DialogPages/PackageDetailsViewModel.cs), [detail contract](https://github.com/Devolutions/UniGetUI/blob/main/src/UniGetUI.PackageEngine.Interfaces/IPackageDetails.cs), and [operation selector](https://github.com/Devolutions/UniGetUI/blob/main/src/UniGetUI.PackageEngine.Managers.WinGet/Helpers/WinGetPkgOperationHelper.cs#L18) | The details view model and contract omit a separate product-code list, including in extended details. Extended details include package ID, manifest, installer hash/type/URL, and update date. Operations normally select by package ID; an internal installed-app identifier supports local-identifier fallback. |

The web page was inspected as rendered page text. TUI and desktop findings are
from current upstream source inspection, not interactive runs of those programs.
The comparison covers their package-details surfaces; it does not claim these
identifiers can never appear in logs, raw manifests, or an unmatched installed
application's package ID.

## What the codes support

Windows Installer's [ProductCode](https://learn.microsoft.com/en-us/windows/win32/msi/productcode)
identifies a particular product release and its uninstall registry key.
[Maintenance commands](https://learn.microsoft.com/en-us/windows/win32/msi/command-line-options)
can identify an installed MSI product by its code. Product codes therefore have
administrative and diagnostic value.

UniGetUI's [native helper](https://github.com/Devolutions/UniGetUI/blob/main/src/UniGetUI.PackageEngine.Managers.WinGet/ClientHelpers/NativeWinGetHelper.cs#L384)
stores `InstalledVersion.Id`; its operation helper can use that local identifier
if ordinary catalog identification fails. That is an internal operation target,
not a displayed list of every installer variant's codes.

Scout's COM detail loader prefers the available version's metadata. The list can
therefore include product codes for installer architectures other than the one
installed locally. For example, the [7-Zip 26.05 manifest](https://github.com/microsoft/winget-pkgs/blob/master/manifests/7/7zip/7zip/26.05/7zip.7zip.installer.yaml)
contains `7-Zip` for EXE installers and distinct x86/x64 MSI GUIDs. Showing them
together beneath the installed version does not help the user choose a routine
action and can suggest that several variants are installed.

## Decision for Scout

Remove the Product code row from Package Details. No Scout action uses that
displayed row, and the interfaces inspected provide no compelling reason for
displaying the full catalog list in ordinary package browsing. The package ID,
versions, source, publisher, links, and description provide the useful context
for install and upgrade decisions.
