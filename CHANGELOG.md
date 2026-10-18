# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

* An `install-test` workflow that sideloads a Store app on a disposable hosted Windows runner:
  build from the commit under test, enable sideloading, doctor, resolve, plan, install, read the
  registration back, remove it again, upload the log.

### Fixed

* `doctor` reported the sideloading policy and Developer Mode as `unknown` on every machine
  where they were actually configured, because the registry returns DWORDs and the probe only
  understood JSON booleans. A disabled sideloading policy therefore produced no warning at all.
* The install-test workflow no longer treats doctor's warning exit code as a step failure.

## [0.2.0] - 2026-10-18

### Added

* Built-in manual: `storedl help <command>` and `storedl <command> --help` print synopsis,
  arguments, every option with its default, examples and notes, all shipped inside the binary.
* `storedl --version` prints the version, runtime and OS it would deploy into.
* Dependency-aware downloads: `install` now fetches the application first, reads the
  dependencies out of its manifest and downloads only those. `--all-deps` restores the old
  behaviour for offline archives.
* Cross-platform CI: build and test on Linux and Windows, plus a publish check for every target
  runtime identifier.
* Release pipeline: tagging `v*` publishes self-contained single-file Windows binaries
  (x64, x86, arm64) with `SHA256SUMS.txt`. Only Windows is packaged: deployment needs
  `Add-AppxPackage` and PowerShell, so a binary for another platform would only half work.
* Exit code 3 from `doctor` when the machine cannot deploy packages yet.

### Changed

* The locale defaults to the Windows display language instead of `en-US`.
* `install` reports why a dependency is skipped ("already installed", "not declared by the
  manifest") instead of silently ignoring it.
* Documentation rewritten: one H1 per page, strict heading hierarchy, every code block fenced
  and labelled, contents tables, and a troubleshooting page organised by symptom.

### Fixed

* Options such as `--json` no longer consume the following positional argument.
* The deployment HRESULT is read from the message text where Windows puts it, instead of the
  generic wrapper code from the exception.
* PowerShell output is pinned to UTF-8 and CLIXML noise is stripped, so localized Windows
  messages are readable.
* Bundle child packages are read without extracting the bundle, so the dependency graph comes
  from the real inner manifest.

## [0.1.0] - 2026-10-18

### Added

* Store link parsing for `apps.microsoft.com`, legacy `microsoft.com/store` links,
  `ms-windows-store://` deep links and bare product ids.
* Product resolution through the display catalog with a Store Edge fallback.
* Delivery service client: `GetCookie`, `SyncUpdates` and `GetExtendedUpdateInfo2`, including
  the retail device profile that makes the service return payload fragments.
* Package analysis for `.msix`, `.appx`, `.msixbundle` and `.appxbundle`: identity,
  dependencies, resource ids and bundle children.
* Resumable, parallel downloads verified against the published SHA-1, with
  `storedl-manifest.json` written next to the files.
* Framework-first install planning against the installed package inventory.
* Deployment through `Add-AppxPackage` with automatic remedies per HRESULT, plus registration
  verification.
* Deployment error catalogue covering fifteen HRESULTs with causes and fixes.
* Environment probe covering sideloading policy, Developer Mode, deployment services and disk
  space.
* Commands: `resolve`, `download`, `analyze`, `plan`, `install`, `doctor`, `explain`.
