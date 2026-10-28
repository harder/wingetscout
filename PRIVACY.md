# Scout for WinGet Privacy Policy

Effective October 27, 2026. This policy covers the Scout for WinGet application maintained by Kevin Harder.

## Data on your device

Scout reads installed package names and versions, configured package sources, and package metadata to provide package management and update checks. It saves settings, saved package sets, the latest update-check results, and up to 20 recent operation records in `%LOCALAPPDATA%\WinGetScout`. Mock sessions use the `Mock` subfolder.

Operation records can include package identifiers, versions, timestamps, install options, file paths, results, and diagnostic messages. These may reveal your Windows username, device details, or private package-source information. Local files are not encrypted by Scout and are protected by your Windows account permissions.

## Network access and sharing

Scout has no analytics, advertising, or automatic diagnostic uploads to the developer. It uses Microsoft's WinGet components and your configured sources to search for packages, check updates, and download or manage software. These requests may disclose your IP address, search terms, package identifiers, and information required by the source. Microsoft, source operators, package publishers, and websites you open have their own privacy policies.

Scheduled update checks run only when you enable them and query your package sources. Reports and CSV files are saved or copied to the clipboard only when you request it. Review them for sensitive information before sharing; Scout does not automatically send them to anyone.

## Retention and deletion

Local data stays on your device until replaced or deleted; operation history retains the most recent 20 runs. To remove Scout's saved data, disable scheduled checks in Scout, close it, and delete `%LOCALAPPDATA%\WinGetScout`. Delete any exported files and downloaded installers separately. Uninstalling the application may leave this user data behind. WinGet and installed packages may retain their own data separately.

## Contact

For privacy questions, use [the project's GitHub issues](https://github.com/harder/wingetscout/issues). Issues are public; do not include sensitive information.
