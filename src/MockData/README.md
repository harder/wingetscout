# Mock catalog snapshot

`catalog.json` is a frozen, portable fixture captured on October 24, 2026, from
read-only WinGet queries on a Windows ARM64 machine. It contains 23 unique
catalog entries: 18 were installed and 14 had an available upgrade. Five extra
catalog-only entries exercise discovery and install-review paths.

Installed versions came from `winget export --include-versions`, separately for
the `winget` and `msstore` sources. Upgrade candidates came from `winget upgrade`
with no package or operation flags. Public package metadata came from
`winget show --id <id> --exact --source <source>`. Collection used WinGet CLI
`1.30.140-preview`; the app's live COM checks used its bundled `1.29.380` engine.

The snapshot includes long names/IDs, long version strings, descriptions, many
tags, documentation links, stable packages, upgrade candidates, and a Store
package. It intentionally preserves observations such as an installed Discord
version newer than the Store catalog's advertised version. `Version` is the
catalog version; `InstalledVersion` and `AvailableVersion` are separate recorded
observations. A string such as Unity Hub's `< 3.21.0.65535` is kept as observed,
but is not offered as an exact version in the mock version picker.

No usernames, installation paths, registry paths, machine identifiers, or live
package-state queries are included in the fixture. Names and descriptions are
public package metadata. Mock operations remain simulated and session-local.
The JSON is embedded and read through a source-generated serialization context
so the same fixture works in Native AOT builds and on non-Windows hosts.

To refresh it, take new read-only exports and upgrade listings, choose a varied
set of package IDs, and collect their `show` metadata. Record the observation
date here. Keep each source/ID unique and avoid synthesizing upgrade candidates
from string inequality: version formatting and catalog correlation can differ.
Re-run the mock catalog, batch/retry, layout, and rendered-screen regressions.
