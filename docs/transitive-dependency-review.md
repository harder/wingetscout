# Transitive dependency review

Reviewed November 1, 2026, after the Terminal.Gui `.183` update reached main.
The direct package references, SDK, and GitHub Actions were current, but their
transitive minimum versions left 22 compatible package updates available.

## Selected updates

| Package family | Before | Selected | Reason and compatibility review |
| --- | --- | --- | --- |
| Markdig | 1.4.2 | 1.5.1 | [1.5.0](https://github.com/xoofx/markdig/releases/tag/1.5.0) fixes Unicode whitespace and escaping; [1.5.1](https://github.com/xoofx/markdig/releases/tag/1.5.1) fixes quote/fence indentation, list/thematic-break parsing, disabled HTML output, and autolink schemes. Its breaking rename of `ParseSetexHeadings` is not used by Scout or Terminal.Gui at source commit `84ea67cb4b43c3e711b54dfb46d3facce078f292`; all five Terminal.Gui source files using Markdig were inspected. Existing rendered Details-link tests cover the actual integration. |
| Microsoft.Extensions.* | 10.0.13; Logging.Abstractions 10.0.7 | 10.0.14 | Align configuration, binder, JSON, logging, options, and their dependent libraries with the serviced .NET 10 family already used by SDK 10.0.403. Five root references promote all 12 affected Extensions packages together. No application API migration is needed. |
| System.IO.Abstractions and TestableIO wrappers | 22.2.0 | 22.3.0 | The [release](https://github.com/TestableIO/System.IO.Abstractions/releases/tag/v22.3.0) promotes Testably.Abstractions.FileSystem.Interface from 10.3.0 to 10.4.0 and includes upstream maintenance updates. Keep the abstraction and both wrapper packages aligned; Scout's real diagnostics SaveDialog and cancellation tests exercise the integration. |
| Microsoft.Testing.Platform and extensions | 2.5.1 | 2.5.2 | The [source changelog](https://github.com/microsoft/testfx/blob/933377f07ac4bded77e820b0beef7d27e805cd89/docs/Changelog-Platform.md) fixes invocation working-directory preservation for controller-launched ordinary test hosts and relative settings paths. NuGet publishes stable 2.5.2 even though this source changelog still labels it unreleased. Keep Platform, MSBuild, Telemetry, and TrxReport.Abstractions on the same patch. |
| Microsoft.Bcl.AsyncInterfaces (tests only) | 6.0.0 | 10.0.14 | Update the xUnit common-library dependency for this net10.0 test project. Keep the reference private to the test project; it is not an application dependency. |

The application promotes only the parent packages needed to select the newer
graph: Markdig, System.IO.Abstractions, and five Microsoft.Extensions roots.
The test project promotes the MTP MSBuild/Telemetry/TRX roots and async-interface
package. Explicit references make these overrides visible to Dependabot;
regenerating a lockfile alone would select the upstream minimum versions again.
Regenerate portable, Identity, and test lockfiles together. Remove overrides
when the parent packages raise their minimums to compatible reviewed versions.

Dependabot groups Extensions and file-system packages, and includes MTP in the
existing xUnit group so related components can be reviewed together.

## Application Insights 3.2.0 is incompatible with the current test telemetry

The remaining major update is Microsoft.ApplicationInsights `2.23.0` to `3.2.0`.
This is not a compatible transitive substitution for the current runner:

- MTP 2.5.2's
  [AppInsightTelemetryClient](https://github.com/microsoft/testfx/blob/933377f07ac4bded77e820b0beef7d27e805cd89/src/Platform/Microsoft.Testing.Extensions.Telemetry/AppInsightTelemetryClient.cs)
  calls `TelemetryClient.TrackEvent(name, properties, metrics)`.
- Application Insights 3.x
  [removes that three-argument overload](https://github.com/microsoft/ApplicationInsights-dotnet/blob/v3.2.0/BreakingChanges.md#trackevent).
  The 3.2.0 package API documentation and source expose the two-argument
  `TrackEvent(name, properties)` instead. Its broader
  [migration](https://github.com/microsoft/ApplicationInsights-dotnet/blob/v3.2.0/MigrationGuidance.md)
  also replaces telemetry infrastructure with OpenTelemetry/Azure Monitor.

The precompiled MTP telemetry extension would call a missing method if forced
onto 3.x. Declare a private `[2.23.0,3.0.0)` test-package compatibility range.
Wait for an MTP extension that migrates this call before updating to 3.x;
ordinary test success alone would not prove that telemetry still works.

## Other audit results

- Terminal.Gui `.183` remains the latest develop publication by NuGet timestamp.
  The numerically higher `2.6.0-develop.61` is older.
- Both WinGet COM packages remain at latest stable `1.29.380`. Newer packages
  are previews; their latest versions differ between projection and engine.
- xUnit `4.0.2` and .NET 10 SDK `10.0.403` are current.
- Every GitHub Actions pin matches its latest action release. For CodeQL,
  compare `v4.*` action releases rather than `codeql-bundle-*` releases; the
  generic GitHub latest-release endpoint returns a bundle tag. The pinned
  action remains latest `v4.39.1`.
- Use the signing action's canonical `azure/artifact-signing-action` repository
  name, as documented in its pinned README. The former
  `azure/trusted-signing-action` name redirects there. Keep the identical
  `c7ab2a863ab5f9a846ddb8265964877ef296ee82` v2.0.0 commit and existing inputs.

## Validation

The updated graph passes all 757 tests on Linux x64 with SDK 10.0.403,
including rendered links, file-dialog cancellation, workflow reviews, UI
dispatch, shutdown, and list resizing. Adding the Application Insights range
keeps its resolved version at 2.23.0. Both Windows targets compile with zero
warnings/errors. All four architecture/mode locked restore graphs, graceful
mock startup with empty stderr, monitor tests, site/link build, and whitespace
checks pass. The final stable audit finds no application updates and only the
incompatible Application Insights major update in tests. PR CI supplies
Windows x64/ARM64 portable and Identity Native
AOT publishes and published mock startup; these do not establish live COM
activation or interactive terminal parity.

The signing action's actual Azure-backed execution requires configured signing
credentials; the canonical-name change keeps the existing action code and
configuration.

Recheck the stable update inventory with:

```sh
dotnet package list --project WinGetScout.csproj --outdated --include-transitive --format json
dotnet package list --project tests/WinGetScout.Tests.csproj --outdated --include-transitive --format json
```

Only the deliberately constrained Application Insights 3.x update should
remain in the stable package inventory.
