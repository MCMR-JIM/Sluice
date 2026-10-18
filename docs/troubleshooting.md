# Troubleshooting

Most sideload failures come down to four things: the policy that allows sideloading, Developer
Mode, the deployment services, and disk space. `Sluice doctor` checks all four and tells you what
to change.

```console
Sluice doctor
Sluice explain 0x80073CF3
```

## Contents

* [Error codes](#error-codes)
* [Symptoms](#symptoms)
* [Diagnostics worth running](#diagnostics-worth-running)
* [Reporting a problem](#reporting-a-problem)

## Error codes

Deployment failures report an HRESULT. `Sluice explain <code>` prints the entry below, including
whether the tool retries automatically.

| Code | Name | Meaning | Automatic remedy | Manual fix |
| --- | --- | --- | --- | --- |
| `0x80073CF3` | ERROR_PACKAGE_UPDATE_POLICY_VIOLATION | Conflicts with something already installed | retries with `-DependencyPath` | A framework is missing or too old: download it with `--deps-only` into the same folder, then install again |
| `0x80073CF6` | ERROR_PACKAGE_COULD_NOT_BE_REGISTERED | Staged, but registration failed | extracts and registers loose files | Enable Developer Mode, or remove the stale registration and install again |
| `0x80073CF9` | ERROR_DEPLOYMENT_FAILED | The deployment service could not complete | none | Start `AppXSvc` and `StateRepository`, then `wsreset.exe` or a DISM repair |
| `0x80073D02` | ERROR_PACKAGES_IN_USE | A package being replaced is in use | retries with `-ForceApplicationShutdown` | Close the app and any process holding it |
| `0x80073CFB` | ERROR_PACKAGE_ALREADY_INSTALLED | Same or newer version already present | retries with `-ForceUpdateFromAnyVersion` | Uninstall the existing package, or accept it |
| `0x80073CFF` | ERROR_INSTALL_POLICY_FAILURE | Policy blocked the deployment | none | Settings > System > For developers > Developer Mode, or set `AllowAllTrustedApps` to 1 |
| `0x800B0100` | TRUST_E_NOSIGNATURE | The package is not signed | none | Use the original signed package; the tool only downloads those |
| `0x800B0109` | CERT_E_UNTRUSTEDROOT | Signature chains to an untrusted root | none | Trust the signing certificate, or use the Microsoft-signed build |
| `0x800B010A` | CERT_E_CHAINING | Invalid certificate chain | none | Verify the digest, then download again |
| `0x80070005` | E_ACCESSDENIED | Access denied during deployment | none | Elevate, or install per user instead of `--all-users` |
| `0x80073CF0` | ERROR_PACKAGE_OPEN_FAILED | The package could not be opened | none | Truncated or wrong file: check the digest the download reported |
| `0x80073CF1` | ERROR_PACKAGE_NOT_FOUND | The file was not where it was expected | none | Re-run the install with the same `--dir` used for the download |
| `0x80073CFD` | ERROR_PACKAGE_NOT_SUPPORTED_ON_THIS_DEVICE | Not applicable to this machine | none | Wrong architecture or device family; update Windows |
| `0x80070490` | ERROR_NOT_FOUND | A component deployment needs is missing | none | Corrupt deployment state: DISM repair, then reinstall |
| `0x8007000B` | ERROR_BAD_FORMAT | The payload is not a valid image | none | Install the architecture this machine runs; let the digest check pass |

## Symptoms

### Symptom: App Installer opens and skips the dependencies

That is App Installer's behaviour: it deploys the single file you clicked. Use `Sluice install`,
or `Add-AppxPackage -Path ... -DependencyPath ...` by hand, so the frameworks go in first. This is
the reason the tool exists.

### Symptom: it installed, but there is no Start menu entry, or it crashes on launch

Usual causes: a child package of a bundle was registered instead of the bundle, a required framework
is missing, or loose files were registered without their assets.

```console
Sluice analyze .\downloads
Sluice plan .\downloads --arch x64
Sluice install --dir .\downloads --force --yes
```

`Sluice analyze` shows the identity and dependencies of each file, `Sluice plan` shows what the
tool thinks should be installed, and the install then verifies the registration.

### Symptom: 0x80073CF3 even though the dependencies are right there

Two common causes:

1. **Wrong architecture.** An x86 framework cannot satisfy an x64 manifest, and the reverse. Let the
   tool pick with `--arch`, or use `--all-arch` for an archive.
2. **Installed version too old.** The manifest requires a minimum version. `Sluice plan` compares
   the installed version and decides whether to install or skip; if it says "already installed", the
   requirement is satisfied by a newer version and the error comes from somewhere else.

### Symptom: the generated link stopped working

CDN URLs are signed and expire within hours. Re-run `Sluice download` (resolution is cheap), or
keep the files from the first run and install offline with `--skip-download`.

### Symptom: "encrypted package", or the app demands a licence

`.eappx` and `.emsix` payloads are encrypted for entitled accounts. `Add-AppxPackage` cannot
install them without the licence the Store issues for that account. The tool detects them and
refuses with an explanation instead of failing later with a cryptic HRESULT.

### Symptom: doctor says sideloading is disabled

* Windows 11: Settings > System > For developers > Developer Mode.
* Windows 10: Settings > Update & Security > For developers > Sideload apps.
* Scripted, in an elevated shell:

```powershell
Set-ItemProperty HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock -Name AllowAllTrustedApps -Value 1
# only needed for the -Register fallback:
Set-ItemProperty HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock -Name AllowDevelopmentWithoutDevLicense -Value 1
```

Reboot afterwards, then re-run `Sluice doctor`.

### Symptom: nothing is registered after a successful install

The tool compares `Get-AppxPackage -Name <identity>` with the plan and treats an empty answer as a
failure. If it happens anyway, the deployment went to a different user (try `--all-users` with
elevation) or was rolled back. Event Viewer > Applications and Services Logs > Microsoft > Windows >
AppXDeployment-Server holds the activity id printed in the error detail.

## Diagnostics worth running

| Command | What it answers |
| --- | --- |
| `Sluice doctor` | Can this machine install sideloaded packages at all? |
| `Sluice doctor --json` | The same, for scripting |
| `Sluice resolve <url>` | What does the Store actually offer for this id? |
| `Sluice analyze <file>` | What identity, architecture and dependencies does this file carry? |
| `Sluice plan <dir>` | What would be installed, in what order, and what is blocking it? |
| `Sluice explain <code>` | What does this HRESULT mean and what fixes it? |

PowerShell equivalents, when you want to see the raw state:

```powershell
Get-AppxPackage -Name <name> | Select-Object Name, Version, Status, PackageFullName
Get-AppxPackage -PackageTypeFilter Framework | Select-Object Name, Version, Architecture
Get-AppxPackageLog -ActivityID <activity id from the error>
Get-Service AppXSvc, StateRepository, ClipSVC
```

## Reporting a problem

`Sluice resolve <url> --json` and `Sluice plan <dir> --json` capture what the tool saw: product,
category id, package identities, architectures, URLs and the plan. Neither contains personal data,
which makes them safe to attach to an issue.
