# COM activation under Native AOT

How `wingetscout` reaches the WinGet COM API in its current portable ZIP, and how the optional
MSIX identity build differs. For release and signing status, see [code-signing.md](code-signing.md).

## The problem in one paragraph

The WinGet COM API (`Microsoft.Management.Deployment.PackageManager`) is served by an
**out-of-process server** (`WindowsPackageManagerServer.exe`) that ships in-box with **App
Installer** — so the engine is already on every Windows 10/11 machine. The catch: `new
PackageManager()` is a **WinRT activation**, which requires the calling process to have **package
identity**. An unpackaged process has none, so activation throws `0x80073D54`
(`APPMODEL_ERROR_NO_PACKAGE`). Under JIT this is normally bridged (the old `winrtact.dll`
manual-activation shim, or CsWinRT's runtime fallback); under **Native AOT** none of those bridges
exist, so the shipped AOT build can't reach the out-of-process server at all.

## What the activation investigation measured

All rows tested on a Windows 11 ARM64 host against the real installed winget server.

| Build | Package identity | Ships beyond the exe | `new PackageManager()` | classic `CoCreateInstance` (OOP) |
|---|---|---|---|---|
| **JIT**, portable | none | nothing | — | ✅ works (this is the UniGetUI path) |
| **AOT**, portable | none | nothing | ❌ `0x80073D54` | ❌ `0x80073D54` |
| **AOT**, portable | none | bare `app.manifest` (supportedOS only) | — | ❌ `0x80073D54` |
| **AOT**, packaged | ✅ | nothing | ❌ `0x8000000F` | ❌ `0x8000000F` |
| **AOT**, packaged | ✅ | **+ 61 KB `Microsoft.Management.Deployment.winmd`** | ✅ works | ✅ works |
| **AOT**, portable (current) | none | **+ ~7 MB in-process engine** | ✅ (in-proc) | n/a |

Two non-obvious conclusions:

1. **The "single portable exe + out-of-process COM, ship nothing" idea is impossible under AOT.**
   Even the classic `CoCreateInstance` trick that UniGetUI uses (`WindowsPackageManagerStandardFactory`
   with `CLSCTX_ALLOW_LOWER_TRUST_REGISTRATION`) fails with the *same* `0x80073D54` — because
   UniGetUI is JIT/ReadyToRun, and **no shipping app has ever done winget-COM-under-AOT**. So
   shipping the in-process engine in the portable build is genuinely required, not an oversight.

2. **Package identity rescues AOT — cheaply.** Identity clears `0x80073D54`, surfaces a second gate
   (`0x8000000F = RO_E_METADATA_NAME_NOT_FOUND` — AOT carries no WinRT type metadata), and dropping
   the **61 KB `.winmd`** into the package clears that too. A packaged AOT exe then talks to the
   already-installed server, shipping ~61 KB of metadata instead of the ~7 MB engine.

## The two viable activation strategies (and the dead ends)

**Viable**

- **In-process server** (current portable build). Bundle `Microsoft.WindowsPackageManager.InProcCom`
  (native `WindowsPackageManager.dll` ~7 MB + `Microsoft.Management.Deployment.InProc.dll`) and a
  registration-free WinRT `app.manifest` that routes the activatable classes in-process. No identity,
  no out-of-process server, no signing required → a true portable exe (+ engine). Also sidesteps the
  out-of-process server-wedge failure mode entirely. Cost: ~7 MB and you own keeping the engine current.
- **Package identity** (MSIX / sparse package). Give the app identity and ship the 61 KB `.winmd`;
  `new PackageManager()` (or `CoCreateInstance`) then reaches the in-box out-of-process server. Tiny
  payload, but requires an MSIX + a signing cert, and reintroduces the out-of-process dependency
  (server health, cross-process progress-callback marshaling — which does work; the download path
  exercised it).

**Dead ends (don't retry)**

- Classic `CoCreateInstance` from an unpackaged AOT process — `0x80073D54`.
- A bare `app.manifest` (supportedOS/longPathAware) without in-proc routing or identity — no effect.
- Identity without the `.winmd` — `0x8000000F`.
- `Microsoft.Windows.CsWinRT 3.x` AOT-first projection — breaks at its own codegen.

## How this maps to distribution

- **Portable ZIP (recommended):** the Native AOT executable and in-process COM companion files.
  It uses COM where activation succeeds and falls back to the WinGet CLI. This is the initial WinGet
  community-package format; keep the extracted folder together.
- **Standalone `.exe`:** a smaller release download without the COM companion files. It uses the
  WinGet CLI backend.
- **MSIX (not yet released):** the identity build uses a `.winmd` file and package identity to reach
  the out-of-process WinGet server. It still needs public-trust signing and installation tests before
  it can be offered. The release workflow only publishes it when signing is fully configured.

### The `WingetComMode` build switch

The project selects the activation strategy at build time (Windows TFM only):

- `WingetComMode=InProc` (default) — references `InProcCom` + the in-proc `app.manifest`. This is the
  full ZIP build. A single EXE download omits the companion files and uses CLI fallback.
- `WingetComMode=Identity` — drops `InProcCom` and the in-proc manifest routing, copies the `.winmd`
  next to the exe, and relies on package identity. This is the unreleased MSIX build.

```powershell
# portable / in-proc (default)
dotnet publish -c Release -f net10.0-windows10.0.26100.0 -r win-x64

# identity build for the MSIX
dotnet publish -c Release -f net10.0-windows10.0.26100.0 -r win-x64 -p:WingetComMode=Identity
```

The public distribution steps are in [README.md](README.md#install). The future MSIX signing steps are
in [code-signing.md](code-signing.md); the build script and manifest live under [`packaging/`](packaging/).
