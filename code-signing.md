# Signing and distribution

## Current release plan

The first WinGet community package uses the complete **portable ZIP**. The release workflow publishes x64 and ARM64 ZIPs, SHA-256 checksums, and build attestations. It generates the WinGet manifests from the ZIPs downloaded back from the published GitHub Release. The ZIPs and executables are currently **unsigned**.

WinGet verifies the ZIP against `InstallerSha256`, extracts it into a managed portable package directory, and makes the `wingetscout` command available on `PATH`. That provides installation, upgrade, and uninstall behavior; **WinGet does not sign the executable or grant it a trusted publisher identity**. A first run may still encounter Windows reputation warnings. A published GitHub checksum or attestation proves different things from a Windows code signature.

Signed **MSIX** is a later distribution option. It supplies Windows package identity for the out-of-process WinGet COM backend and needs a certificate trusted by the target PC. The release workflow builds and signs MSIX only when all signing settings below exist. At present it signs the MSIX package, **not** the portable ZIP's executable. For the COM activation details, see [com-activation.md](com-activation.md).

## Get a publicly trusted signature for MSIX

The workflow is wired to [Azure Artifact Signing](https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart), previously called Azure Trusted Signing. Its Basic tier currently costs about US$10 per month; check [Microsoft's current pricing](https://learn.microsoft.com/en-us/azure/artifact-signing/how-to-change-sku) before creating an account. Public Trust is available to individual developers in the US and Canada, and to organizations in Microsoft's listed regions. For individual validation, the Azure billing account must be **Individual**, and its legal name and address must match the government ID used for verification.

1. **Prepare Azure.** Sign in to an Azure subscription. In **Subscriptions → Resource providers**, register `Microsoft.CodeSigning`. Check the subscription's billing account type, legal name, and sold-to address before starting individual validation.
2. **Create an Artifact Signing account.** In the Azure portal, open **Artifact Signing Accounts → Create**, select the subscription and a supported region, and choose the Basic tier unless you need more capacity. Record the account name and its **Account URI** endpoint.
3. **Validate your identity.** Give your Azure user the **Artifact Signing Identity Verifier** role if needed. On the account's **Identity validations** page, create an **Individual → Public** request (or the matching organization request), then complete Microsoft's verification steps. Wait until its status is **Completed**. [Microsoft's quickstart](https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart) has the current portal screens and document requirements.
4. **Create a Public Trust certificate profile.** On **Certificate profiles**, create a profile using the completed validation. Record its name and copy the **Certificate Subject Preview** exactly. The MSIX manifest publisher must match that subject; put the exact value in `MSIX_PUBLISHER`.
5. **Create a signing identity for CI.** Register a Microsoft Entra application and create a service principal plus client secret. Assign that service principal the **Artifact Signing Certificate Profile Signer** role, preferably at the certificate-profile scope. Record its tenant ID, application/client ID, and client-secret **value**. Follow [Microsoft's role assignment guide](https://learn.microsoft.com/en-us/azure/artifact-signing/tutorial-assign-roles) and [service-principal setup](https://learn.microsoft.com/en-us/azure/developer/github/connect-from-azure-secret). Keep the secret outside Git and rotate it before expiry.
6. **Configure this repository.** In GitHub **Settings → Secrets and variables → Actions**, add these repository **secrets**:

   | Secret | Value |
   | --- | --- |
   | `AZURE_TENANT_ID` | Entra directory/tenant ID |
   | `AZURE_CLIENT_ID` | Application/client ID of the signing service principal |
   | `AZURE_CLIENT_SECRET` | Its client-secret value |

   Add these repository **variables**:

   | Variable | Value |
   | --- | --- |
   | `AZURE_SIGNING_ENDPOINT` | Account URI shown in Azure, for example `https://eus.codesigning.azure.net/` |
   | `AZURE_SIGNING_ACCOUNT` | Artifact Signing account name |
   | `AZURE_SIGNING_PROFILE` | Public Trust certificate-profile name |
   | `MSIX_PUBLISHER` | Exact certificate subject from the profile preview |

7. **Validate with a new release.** The workflow rejects partial configuration, signs both MSIX architectures with `azure/trusted-signing-action`, and runs `signtool verify /pa` before publishing. Inspect the release's signed MSIX and install it on a clean test PC **without importing a development certificate**. Confirm the displayed publisher and run the COM diagnostics. A self-signed MSIX from `packaging/build-msix.ps1 -SelfSigned` is only for local testing and requires explicit trust on test devices.

Microsoft's [MSIX signing guide](https://learn.microsoft.com/en-us/windows/msix/package/sign-msix-package-guide) explains the distinction between development and production signatures. A valid public signature identifies the publisher and enables reputation to build; it does **not** promise an immediate SmartScreen pass for a new app.

## Portable ZIP signing later

If Windows reputation warnings for ZIP users become a priority, sign each `wingetscout.exe` **before** compressing and hashing the ZIPs. That needs an additional, separately tested workflow change. Signing the MSIX alone does not sign the ZIP executable, and submitting a ZIP manifest to WinGet does not make it signed. Any signed ZIP must get a new release version and new manifest hashes; published release assets are immutable.
