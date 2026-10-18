# StoreDownloader

**One command that turns a Microsoft Store link into a working install.**

Store link in, running app out: resolve the real package URLs, download the app *and* the
dependency packages it actually needs, install them in the right order with
`Add-AppxPackage`, then read the registration back and prove it worked.

`@console
$ storedl install https://apps.microsoft.com/detail/9nksqgp7f2nh
Product : WhatsApp (9NKSQGP7F2NH)
Target  : x64, locale en-US
Folder  : ./downloads
Files   : 4 package(s), 351.9 MiB

Plan for 5319275A.WhatsAppDesktop 2.2640.100.0 (x64)
#  KIND              FILE
-  ----------------  ------------------------------------------------------------------
1  InstallFramework  Microsoft.VCLibs.140.00.UWPDesktop_14.0.33728.0_x64.appx
2  InstallFramework  Microsoft.VCLibs.140.00_14.0.33519.0_x64.appx
3  InstallFramework  Microsoft.WindowsAppRuntime.2_2.5.1.0_x64.msix
4  InstallMain       5319275A.WhatsAppDesktop_2.2640.100.0_neutral.msixbundle
5  Verify            5319275A.WhatsAppDesktop_2.2640.100.0_neutral.msixbundle

KIND              FILE                                                     RESULT
----------------  -------------------------------------------------------  ------
InstallFramework  Microsoft.VCLibs.140.00.UWPDesktop_14.0.33728.0_x64.appx  ok
...
Registered: 5319275A.WhatsAppDesktop_2.2640.100.0_x64__cv1g1gvanyjgm (status Ok)
Install finished successfully.
`@

## Why this exists

Web generators get you *links*. That is where the trouble starts:

| What goes wrong | What this tool does instead |
| --- | --- |
| Generators list dependencies for every architecture, nobody knows which the app needs | Reads the `PackageDependency` entries out of the real manifest and keeps only compatible packages |
| The browser hands the file to **App Installer**, which silently skips dependencies | Never uses App Installer: `Add-AppxPackage` with `-DependencyPath`, frameworks first |
| The bundle is unzipped by hand, a child package is deployed, registration breaks (`0x80073CF6`) | Installs the bundle as a bundle, or registers loose files deliberately as a fallback |
| "It installed but there is no Start menu entry" or it crashes on launch | Reads the registration back and compares identity, version and status |
| The URL stopped working (they are signed and expire) | Resolves and downloads in one run, and can replay a folder offline |
| `0x80073CF3`, `0x80073D02`, `0x80073CFF` ... and no idea what they mean | Catalogues the HRESULTs, retries the remedies automatically, prints the manual fix |

## Commands

| Command | What it does |
| --- | --- |
| `install <url\|product-id\|dir>` | The whole pipeline: resolve, download, plan, install, verify |
| `resolve <url\|product-id>` | Show every package behind a link, with architecture and size |
| `download <url\|product-id>` | Download the app, its dependencies and its resource packages |
| `analyze <file\|dir>` | Show what is inside appx/msix/bundles: identity, dependencies, children |
| `plan <dir>` | Show the install order and any blocking problem, without touching the machine |
| `doctor` | Check sideloading policy, Developer Mode, deployment services and disk space |
| `explain <hresult>` | Explain a deployment error and how to get past it (`explain --list` lists them all) |

Run `storedl --help` for every option.

### Typical uses

`@console
# install straight from the Store
storedl install https://apps.microsoft.com/detail/9nksqgp7f2nh

# see what would happen first
storedl install 9NKSQGP7F2NH --dry-run

# fetch an offline archive for another machine (all architectures, two locales)
storedl download 9NKSQGP7F2NH --all-arch --locale zh-cn --dir D:\StoreCache

# install from that archive, no network needed
storedl install --dir D:\StoreCache

# the app is installed but keeps failing: what is wrong with this machine?
storedl doctor
storedl explain 0x80073CF3
`@

## Requirements

* **Windows 10 1809 or newer** to *install*: it drives `Add-AppxPackage` through PowerShell 5.1 or 7.
* Resolving, downloading, analysing and planning work anywhere .NET 8 runs.
* No administrator rights for a normal per-user install; `--all-users` needs elevation.
* Sideloading has to be allowed. `storedl doctor` tells you whether it is.

## How it works

`@
Store URL -> product id -> catalog (WuCategoryId + package family)
                                    |
                                    v
                      FE3 delivery service (SOAP)
                      GetCookie -> SyncUpdates -> GetExtendedUpdateInfo2
                                    |
                                    v
               signed CDN urls -> download (resume, SHA-1 verify)
                                    |
                                    v
               manifest analysis -> dependency graph -> ordered plan
                                    |
                                    v
                Add-AppxPackage (+ remedies on failure) -> registration check
`@

Details, including why the delivery service needs a retail ticket and a legacy build string,
are in [docs/protocol.md](docs/protocol.md). Failure modes and what actually fixes them are in
[docs/troubleshooting.md](docs/troubleshooting.md).

## Building

`@console
dotnet build
dotnet test
dotnet run --project src/StoreDownloader.Cli -- doctor
`@

Publish a single-file binary:

`@console
dotnet publish src/StoreDownloader.Cli -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o out
`@

## Limitations, stated honestly

* **Encrypted Store packages** (`.eappx` / `.emsix`) cannot be installed: they need a licence issued to
  an entitled account. The tool detects them and says so instead of failing mysteriously.
* **Paid apps** need an entitlement. Free apps, which are what people actually sideload, resolve fully.
* **Signed URLs expire** within hours. Resolve and download in the same run, or keep the files.
* The delivery service is Microsoft's internal protocol and can change. Everything that depends on
  its behaviour sits behind `src/StoreDownloader.Core/Fe3` and is covered by fixture tests.

## Layout

`@
src/StoreDownloader.Core
  Store/       url parsing, display catalog + Store Edge clients
  Fe3/         delivery service: SOAP envelopes, response parsing, package model
  Packages/    appx/msix/bundle manifest reading, architecture + resource selection
  Download/    resumable verified downloads, download manifests
  Install/     plans, Add-AppxPackage execution, remedies, verification
  Diagnostics/ deployment HRESULT catalogue
  Windows/     PowerShell host, installed package inventory, environment probe
src/StoreDownloader.Cli        the storedl command line
tests/StoreDownloader.Tests    offline tests, including synthetic package fixtures
docs/                          protocol notes, troubleshooting, architecture
`@

## License

MIT.
