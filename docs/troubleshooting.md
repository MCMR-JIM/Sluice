# Troubleshooting

Start with `storedl doctor`: it checks the four things that decide most sideload failures
(sideloading policy, Developer Mode, the deployment services, free space).

`storedl explain <hresult>` prints the catalogue entry for any code below, including the manual fix.

## Error codes

| Code | Meaning | Automatic remedy | What to do when it still fails |
| --- | --- | --- | --- |
| `0x80073CF3` | Package conflicts with something already installed | retries with `-DependencyPath` | A framework is missing or too old: `storedl download <id> --deps-only` into the same folder, install again |
| `0x80073CF6` | Staged but could not be registered | extracts and registers loose files | Enable Developer Mode, or remove the stale registration and install again |
| `0x80073CF9` | Deployment service failed | none | Start `AppXSvc`/`StateRepository`, then `wsreset.exe` or a DISM repair |
| `0x80073D02` | Package in use | retries with `-ForceApplicationShutdown` | Close the app and any process holding it |
| `0x80073CFB` | Already installed | retries with `-ForceUpdateFromAnyVersion` | Uninstall the existing package, or ignore it |
| `0x80073CFF` | Deployment policy blocked it | none | Settings > System > For developers > Developer Mode, or set `AllowAllTrustedApps=1` |
| `0x800B0100` | Package not signed | none | Use the original signed package; the tool only downloads those |
| `0x800B0109` | Untrusted root certificate | none | Trust the signing certificate, or use the Microsoft-signed build |
| `0x80070005` | Access denied | none | Elevate, or install per user instead of `--all-users` |
| `0x80073CF0` | Package could not be opened | none | Truncated or wrong file: check the digest reported by the download |
| `0x80073CFD` | Not applicable to this device | none | Wrong architecture or device family; update Windows |
| `0x80070490` | Component not found | none | Corrupt deployment state: DISM repair, then reinstall |

## Scenarios people actually hit

### "App Installer opens and skips the dependencies"

That is App Installer's behaviour: it deploys the single file you clicked. Use
`storedl install` (or `Add-AppxPackage -Path ... -DependencyPath ...`) so the frameworks go in
first. That is the entire reason this tool exists.

### "It installed, but there is no Start menu entry, or it crashes immediately"

Usually one of: a child package of a bundle was registered instead of the bundle; a required
framework is missing; or loose files were registered without their assets. Run
`storedl analyze <file>` to see identity and dependencies, then `storedl plan <dir>` to see what
should be installed, and reinstall with `storedl install --dir <dir> --force`.

### "0x80073CF3 even though the dependencies are right there"

Two common causes:

1. the dependency is for the wrong architecture (x86 package on an x64-only manifest, or the
   reverse): `storedl download --arch x64` picks the right one;
2. an older version of the dependency is installed and the manifest requires a newer one: the plan
   compares the installed version and decides.

### "The link from the generator expired"

CDN URLs are signed and expire within hours. Re-run `storedl download` (resolution is cheap), or
keep the files from the first run and use `storedl install --dir <folder>` offline.

### "Encrypted package" / "this app needs a licence"

`.eappx` / `.emsix` payloads are encrypted for entitled accounts. `Add-AppxPackage` cannot
install them without the licence the Store issues; the tool detects them and refuses with an
explanation instead of failing later with a cryptic HRESULT.

### "doctor says sideloading is disabled"

* Windows 10: Settings > Update & Security > For developers > Sideload apps.
* Windows 11: Settings > System > For developers > Developer Mode.
* Scripted: `Set-ItemProperty HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock -Name AllowAllTrustedApps -Value 1`
  (add `AllowDevelopmentWithoutDevLicense` if you also want the `-Register` fallback), then reboot.

### "Nothing is registered after a successful install"

The tool compares `Get-AppxPackage -Name <identity>` with the plan. If it comes back empty, the
deployment went to a different user (try `--all-users` with elevation) or it was rolled back. Check
Event Viewer > Applications and Services Logs > Microsoft > Windows > AppXDeployment-Server for the
activity id printed in the error detail.

## Reporting a resolution problem

`storedl resolve <url> --json` captures everything the tool saw: product, category id, package
identities, architectures and URLs. It contains no personal data, which makes it a good thing to
attach to an issue.
