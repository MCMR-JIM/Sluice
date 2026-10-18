# StoreDownloader

**One command that turns a Microsoft Store link into a working install.**

Store link in, running app out. The tool resolves the real package URLs, downloads the app and
the dependency packages its manifest actually declares, installs the frameworks first with
`Add-AppxPackage`, then reads the registration back to prove it worked.

[![ci](https://github.com/MCMR-JIM/StoreDownloader/actions/workflows/ci.yml/badge.svg)](https://github.com/MCMR-JIM/StoreDownloader/actions/workflows/ci.yml)

## Contents

| Section | What you get |
| --- | --- |
| [Quick start](#quick-start) | First install in three commands |
| [Why this exists](#why-this-exists) | The problems link generators leave behind |
| [Commands](#commands) | Every command and the options that matter |
| [How it works](#how-it-works) | The pipeline in one picture |
| [Requirements](#requirements) | Windows version, PowerShell, policy |
| [Build and test](#build-and-test) | From clone to single-file binary |
| [Limitations](#limitations) | What it deliberately does not do |
| [Documents](#documents) | Protocol notes, troubleshooting, architecture |

## Quick start

```console
dotnet publish src/StoreDownloader.Cli -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o out

.\out\storedl.exe doctor
.\out\storedl.exe install https://apps.microsoft.com/detail/9nksqgp7f2nh --dry-run
.\out\storedl.exe install https://apps.microsoft.com/detail/9nksqgp7f2nh
```

A real run looks like this:

```text
Product : WhatsApp (9NKSQGP7F2NH)
Target  : x64, locale en-US
Folder  : ./downloads
App     : 1 package(s) plus the dependencies its manifest asks for

Plan for 5319275A.WhatsAppDesktop 2.2640.100.0 (x64)
#  KIND              FILE
-  ----------------  -------------------------------------------------------------
1  InstallFramework  Microsoft.VCLibs.140.00.UWPDesktop_14.0.33728.0_x64.appx
2  InstallFramework  Microsoft.VCLibs.140.00_14.0.33519.0_x64.appx
3  InstallFramework  Microsoft.WindowsAppRuntime.2_2.5.1.0_x64.msix
4  InstallMain       5319275A.WhatsAppDesktop_2.2640.100.0_neutral.msixbundle
5  Verify            5319275A.WhatsAppDesktop_2.2640.100.0_neutral.msixbundle

KIND              FILE                                                     RESULT
----------------  -------------------------------------------------------  ------
InstallFramework  Microsoft.VCLibs.140.00.UWPDesktop_14.0.33728.0_x64.appx  ok
InstallFramework  Microsoft.VCLibs.140.00_14.0.33519.0_x64.appx           ok
InstallFramework  Microsoft.WindowsAppRuntime.2_2.5.1.0_x64.msix         ok
InstallMain       5319275A.WhatsAppDesktop_2.2640.100.0_neutral.msixbundle ok
Verify            5319275A.WhatsAppDesktop_2.2640.100.0_neutral.msixbundle ok

Registered: 5319275A.WhatsAppDesktop_2.2640.100.0_x64__cv1g1gvanyjgm (status Ok)
Install finished successfully.
```

## Why this exists

Store link generators get you *links*. Everything that goes wrong afterwards is left to you:

| Problem | What this tool does instead |
| --- | --- |
| Dependency packages are listed for every architecture and runtime generation, and nobody knows which ones the app needs | Reads the `PackageDependency` entries out of the package manifest and downloads only those |
| The browser hands the file to **App Installer**, which deploys one file and silently skips dependencies | Never uses App Installer: plans frameworks first, then `Add-AppxPackage` with an explicit dependency list |
| A bundle gets unzipped by hand, one child package is deployed, registration breaks with `0x80073CF6` | Deploys the bundle as a bundle, or registers loose files deliberately as a fallback |
| "It installed, but there is no Start menu entry" or it crashes on launch | Reads the registration back and compares identity, version and status with the plan |
| The generated URL stopped working, because the links are signed and expire within hours | Resolves and downloads in one run, and can replay a folder offline |
| `0x80073CF3`, `0x80073D02`, `0x80073CFF` and no way to find out what they mean | Catalogues the deployment HRESULTs, retries the known remedies, and prints the manual fix |

## Commands

| Command | Accepts | What it does |
| --- | --- | --- |
| `install` | Store URL, product id, or a folder of packages | The whole pipeline: resolve, download, plan, install, verify |
| `resolve` | Store URL or product id | Show every package behind a link, with architecture and size |
| `download` | Store URL or product id | Download the app, its dependencies and its resource packages |
| `analyze` | Package file or folder | Show identity, dependencies and bundle contents without installing |
| `plan` | Folder of packages | Show install order and blocking problems without touching the machine |
| `doctor` | - | Check sideloading policy, Developer Mode, deployment services, free space |
| `explain` | HRESULT such as `0x80073CF3` | Explain a deployment error and how to get past it |

Every command has its own manual page inside the binary: `storedl help install` (or
`storedl install --help`) prints the synopsis, arguments, every option with its default, examples
and notes. `storedl --help` is the index, `storedl --version` the runtime it would deploy into.

### Recipes

Install straight from the Store:

```console
storedl install https://apps.microsoft.com/detail/9nksqgp7f2nh
```

Inspect before touching anything:

```console
storedl install 9NKSQGP7F2NH --dry-run
storedl analyze .\downloads
storedl plan .\downloads --arch x64
```

Build an offline archive for another machine, then replay it with no network:

```console
storedl download 9NKSQGP7F2NH --all-arch --locale zh-CN --dir D:\StoreCache
storedl install --dir D:\StoreCache
```

Fetch framework packages only, for an app you already have:

```console
storedl download <product-id> --deps-only --dir .\downloads
```

Diagnose a machine that refuses to install anything:

```console
storedl doctor
storedl explain 0x80073CF3 --json
```

### Options worth knowing

| Option | Applies to | Effect |
| --- | --- | --- |
| `--locale \<tag\>` | resolve, download, install, plan | Language of the resource packages (default: Windows display language) |
| `--arch \<arch\>` | download, install, plan | Target architecture (default: this machine) |
| `--all-arch` | download, install | Keep every architecture, for offline archives |
| `--all-deps` | install | Download every candidate dependency instead of only the declared ones |
| `--dir \<path\>` | download, install | Download folder (default `./downloads`) |
| `--dry-run` | install | Print the plan and stop |
| `--skip-download` | install | Install from `--dir` without asking the Store again |
| `--force` | install | Allow installing over a higher version |
| `--all-users` | install | Install for every user; needs elevation |
| `--json` | resolve, download, plan, install, analyze, explain, doctor | Machine readable output |

## How it works

```text
Store URL
   |
   v
product id  ->  catalog  ->  WuCategoryId + package family
   |
   v
FE3 delivery service (SOAP)
   GetCookie  ->  SyncUpdates  ->  GetExtendedUpdateInfo2
   |
   v
signed CDN urls  ->  download (resume, SHA-1 verify)
   |
   v
manifest analysis  ->  dependency graph  ->  ordered plan
   |
   v
Add-AppxPackage (+ remedies on failure)  ->  registration check
```

The fine detail, including why the delivery service needs a retail ticket and a legacy build
string before it returns any payload at all, is in [docs/protocol.md](docs/protocol.md).

## Requirements

* **Windows 10 1809 or newer** to install: deployment is driven through PowerShell 5.1 or 7.
* Resolving, downloading, analysing and planning work anywhere .NET 8 runs.
* No administrator rights for a per-user install; `--all-users` needs elevation.
* Sideloading must be allowed. `storedl doctor` reports whether it is, and what to change.

## Build and test

```console
dotnet build
dotnet test
dotnet run --project src/StoreDownloader.Cli -- doctor
```

Single-file build for distribution:

```console
dotnet publish src/StoreDownloader.Cli -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o out
```

The test suite runs offline. It reads the delivery service parser through a checked-in fixture,
builds appx/msix/bundle containers at test time, and drives the planner with a fake inventory, so
no network and no Windows deployment stack are required.

## Limitations

* **Encrypted packages** (`.eappx`, `.emsix`) cannot be installed: they need a licence issued to
  an entitled account. The tool detects them and explains why instead of failing later.
* **Paid apps** need an entitlement. Free apps, which are what people actually sideload, resolve fully.
* **Signed URLs expire** within hours. Resolve and download in the same run, or keep the files.
* The delivery service is Microsoft's internal protocol and can change. Everything that depends on
  its behaviour sits behind `src/StoreDownloader.Core/Fe3` and is covered by fixture tests.

## Project layout

```text
src/StoreDownloader.Core
  Store/        url parsing, display catalog and Store Edge clients
  Fe3/          delivery service: SOAP envelopes, response parsing, package model
  Packages/     appx, msix and bundle manifests; architecture and resource selection
  Download/     resumable verified downloads, download manifests
  Install/      plans, Add-AppxPackage execution, remedies, verification
  Diagnostics/  deployment HRESULT catalogue
  Windows/      PowerShell host, installed package inventory, environment probe
src/StoreDownloader.Cli        the storedl command line
tests/StoreDownloader.Tests    offline tests with synthetic package fixtures
docs/                          protocol notes, troubleshooting, architecture
```

## Documents

| Document | Contents |
| --- | --- |
| [docs/protocol.md](docs/protocol.md) | Link to package: catalog, delivery service, bundles, installing |
| [docs/troubleshooting.md](docs/troubleshooting.md) | Error codes, symptom to fix, how to collect a report |
| [docs/architecture.md](docs/architecture.md) | Layering, design decisions, testing, conventions |

## License

MIT. See [LICENSE](LICENSE).
