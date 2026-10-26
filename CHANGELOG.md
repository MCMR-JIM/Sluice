# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.4.4] - 2026-10-27

### Fixed

* A download whose digest did not match used to leave the bad `.part` file on disk, so every later
  attempt resumed the same prefix and failed the same way. The file is thrown away and the retry
  starts from zero, which is what makes a cancelled or crashed run recoverable.
* A Store link now resolves the product its path names, not whatever twelve-character value a
  tracking parameter happens to carry. Dedicated id parameters are read first, then the path, and the
  remaining query text only when neither holds an id.

## [0.4.3] - 2026-10-27

### Fixed

* Cancelling a run now ends the PowerShell child instead of only abandoning the wait. Waiting with a
  token left `Add-AppxPackage` running in a process nobody was watching, so the window could say
  "cancelled" while a deployment was still going; the child is killed as a process tree and reaped,
  and a test asserts that the process it started is gone after cancellation.

### Changed

* The window no longer asks for an architecture it already knows: the left pane shows the machine's
  own architecture as a read-only line and the x64/x86/arm64 picker only appears when "For another
  machine" is ticked, which is the case an offline archive for another device needs. The command line
  keeps `--arch` for the same job.

## [0.4.2] - 2026-10-19

### Changed

* Scrollbars look like the ones in Task Manager: no track, no arrows, a thin rounded grip that rests
  almost invisible and comes up when the pointer reaches the gutter. The bar hangs on the pane's
  right edge and the pane keeps the width a scrollbar column used to take: on a 1500 pixel wide
  window the content's right edge moved from 406 to 423 while the pane border stayed at 438.
* The readme downloads a zip and expands it instead of quoting one old release, the publish commands
  match the release workflow, the project layout lists the window, and the brand page describes the
  flat 52% plane and the icon wiring the code actually uses.

## [0.4.1] - 2026-10-19

### Fixed

* The window keeps the chosen language everywhere. Machine chips, the product subtitle, the status
  line and the result headline were rendered once and never refreshed, so switching to EN left
  Chinese behind; they now re-render on the switch. The chips are updated in place, because clearing
  and repopulating the collection in one pass made WPF drop the first containers - the machine name
  and the architecture used to disappear from the header after a flip.
* The activity log follows the language too: every line keeps the recipe it was built from, so a
  switch re-renders the whole transcript, including the severity gutter (命令/信息/完成 and
  cmd/info/done instead of raw enum names).
* The shell keeps the mark. The process now claims the AppUserModelID `MCMR-JIM.Sluice` and the
  exe's version resource says Sluice, so the taskbar no longer falls back to the generic window
  glyph after the binary is replaced.
* The crash dialog is bilingual instead of always Chinese.

### Changed

* The readme badges sit directly under the tagline.

### Added

* Diagnostics carry a code plus the values they format (`DiagnosticMessage`): environment warnings,
  bundle and preparation notes, plan issues and step reasons are translated by the window while the
  command line and the JSON output keep printing the same English text.
* The error-code lookup explains every catalogued deployment error in Chinese as well, and the panel
  re-renders when the language changes.

## [0.4.0] - 2026-10-19

### Changed

* **Renamed to Sluice.** Product, assemblies (`Sluice.exe`, `Sluice.UI.exe`), namespaces, project
  folders, the download manifest and the release archives all carry the new name.
* The real mark is in: `assets/logo.png` at the top of the readme, with `app.ico` and `app.png`
  generated from it for the taskbar and the exe. Repository artwork lives under `assets/`, not
  `docs/`.
* The window no longer repeats the mark and the name inside its content: the caption bar already
  shows both, so the row starts with the machine chips and the version sits next to the language
  switch on the right.
* The readme badges sit centred under the pitch, and there are more of them: CI, latest release,
  platform, .NET and licence.
* Header chips are rebuilt when the language changes, so a switch cannot leave half the header in
  the previous language.

### Added

* The window speaks Chinese and English, switchable in the header (中文 / EN). One dictionary feeds
  the XAML through an indexer binding, so every label, the plan rows, the package roles, the stage
  and the status line flip at once.

### Changed

* The readme opens with the mark centred in HTML instead of a screenshot; screenshots are gone from
  the documentation entirely, the steps and the output are written out instead.
* `docs/logo.png` is a drawn placeholder until real artwork lands; [docs/brand.md](docs/brand.md)
  lists the palette, the swap in procedure and a prompt for generating an icon.

## [0.3.0] - 2026-10-19

### Changed

* The product is consistently called `Sluice`: window title, header, help text, binaries
  (`Sluice.exe` and `Sluice.UI.exe`), release archives and the download
  manifest file name. No short form anywhere.
* The header no longer shows a letter in a rounded square. A vector parcel mark stands in until a
  real icon exists; [docs/brand.md](docs/brand.md) documents the palette, the swap in procedure and
  a prompt for generating the artwork.
* The window sits on the Windows acrylic backdrop (`DWMSBT_TRANSIENTWINDOW`) under one flat semi
  transparent black plane (52% black, no gradient, no tint layers); every component inside is
  opaque, so panels read as solid surfaces and only the gaps and the window edge show the blur.
  Rows highlight on hover, separators are hairline.

### Added

* A desktop window, `Sluice.UI.exe`: the same resolve, download, plan, install and verify pipeline
  driven from a WPF interface with the Windows 11 Mica backdrop, a dark title bar, dense
  Task Manager style layout, a live package table, per step install progress, a colour coded
  activity log, `doctor` chips and an HRESULT lookup.
* `AppxInstaller` reports every finished install step through an `IProgress<InstallStepResult>`,
  which is what lets a user interface show the plan turning green step by step.
* Release archives now carry both binaries; `ci` cross publishes the window for every Windows
  architecture as well.

## [0.2.1] - 2026-10-18

### Added

* An `install-test` workflow that sideloads a Store app on a disposable hosted Windows runner:
  build from the commit under test, enable sideloading, doctor, resolve, plan, install, read the
  registration back, remove it again, upload the log.

### Fixed

* `doctor` reported the sideloading policy and Developer Mode as `unknown` on every machine
  where they were actually configured, because the registry returns DWORDs and the probe only
  understood JSON booleans. A disabled sideloading policy therefore produced no warning at all.
* The install-test workflow no longer treats doctor's warning exit code as a step failure.

### Verified

* A real sideload ran on a disposable hosted Windows runner (Windows Server 2025, build 26100)
  with the `install-test` workflow: the three framework packages the app was missing were
  installed first, then the application bundle, the registration was read back as `status Ok`,
  the executable path was confirmed in `C:\Program Files\WindowsApps`, and the package was
  removed again. This is the case App Installer fails on with `0x80073CF3`.

## [0.2.0] - 2026-10-18

### Added

* Built-in manual: `Sluice help <command>` and `Sluice <command> --help` print synopsis,
  arguments, every option with its default, examples and notes, all shipped inside the binary.
* `Sluice --version` prints the version, runtime and OS it would deploy into.
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
  `Sluice-manifest.json` written next to the files.
* Framework-first install planning against the installed package inventory.
* Deployment through `Add-AppxPackage` with automatic remedies per HRESULT, plus registration
  verification.
* Deployment error catalogue covering fifteen HRESULTs with causes and fixes.
* Environment probe covering sideloading policy, Developer Mode, deployment services and disk
  space.
* Commands: `resolve`, `download`, `analyze`, `plan`, `install`, `doctor`, `explain`.
