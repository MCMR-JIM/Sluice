# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

* The manual describes what the tool actually does now. `--dry-run` still claimed to print the plan and
  touch nothing, although a dry run from a Store link reports the package selection (a plan needs files
  on disk) and only the `--dir` form prints a plan; the install page also never mentioned that a folder
  is narrowed down by its download manifest, or that a failed run lists the steps it never reached.

## [0.4.23] - 2026-10-27

### Changed

* Planning reads the machine once. It asked about the installed frameworks and then about the
  application, and each question started its own PowerShell process - half a second each, and two
  slightly different views of the machine. A snapshot answers both, and the installer keeps asking the
  live inventory because it is the thing changing the machine.
* Download progress is reported once per mebibyte instead of once per 128 KiB buffer, with the final
  update always sent. A 300 MiB package used to push more than two thousand callbacks at the window,
  each of them a trip through its dispatcher.

## [0.4.22] - 2026-10-27

### Fixed

* `install --dir` and `plan` follow the download manifest instead of planning against every package in
  the folder. A folder reused for several products offered the planner all of them, and the main
  package was chosen by architecture and size - so the wrong application could be installed. Verified
  with two products in one folder: without a manifest the plan picked the wrong one, with the manifest
  it picks the product the folder was downloaded for. Packages the manifest names but that are gone are
  reported instead of silently ignored.

## [0.4.21] - 2026-10-27

### Fixed

* A failed install says which steps it never reached. It stops at the first failure, leaving the rest in
  the plan list as "waiting" for ever - which reads like the tool is still working - and the report said
  nothing about them. The window marks them "not run", the command line lists them, and the JSON output
  carries them.
* `install --json` no longer claims `verified: true` for a run that failed. The flag described "no
  verification problem", which is true and useless when the install itself did not finish.

## [0.4.20] - 2026-10-27

### Fixed

* The note about a resource package that belongs to another family follows the language. It was the last
  diagnostic still written as a finished English sentence, so the window could not translate it while
  every other note followed the switch. A new test now proves that every diagnostic the core can emit
  has a translation in the window, so the next one cannot be forgotten.

## [0.4.19] - 2026-10-27

### Fixed

* A batch never downloads the same destination twice. The delivery service can list one file under two
  category entries, and names that differ only in case are one file on Windows; two writers on one
  `.part` file made the second one fail with a sharing violation that looked like a network error.
  Names are deduplicated case-insensitively before the first byte is fetched.
* The activity log opens on the newest line. While the pane was collapsed its scroll viewer had no
  extent, so the scroll-to-end that ran with each new line did nothing and the tab opened on the oldest
  entry, with the reason somebody opened it off screen.

## [0.4.18] - 2026-10-27

### Fixed

* A download that cannot fit on the drive is refused before the first byte is fetched, with the numbers
  in the message. It used to run until the disk filled up and fail at the last write, minutes later.
* A leftover file of the wrong length is downloaded again instead of being reused. With verification
  turned off, an interrupted run left a truncated package behind and the next run reported it as already
  downloaded, so the install failed later on a package that could not be opened.

## [0.4.17] - 2026-10-27

### Added

* The window remembers where it was. Size and position are kept in
  `%LOCALAPPDATA%\sluice\window.json` and restored through a clamp against the work area, so a window
  remembered on a bigger screen - or on a monitor that is no longer attached - opens fully visible
  instead of with its status row below the bottom edge. A corrupt or missing file is simply no memory.

### Fixed

* Closing the window no longer disposes the cancellation source underneath a running stage. The pipeline
  still holds that token while it unwinds, and a disposed source makes the next registration throw
  `ObjectDisposedException` in a window that is already closing; the source is cancelled and left to the
  run's own cleanup.

## [0.4.16] - 2026-10-27

### Fixed

* A new run forgets the previous verdict completely. Only the two visible strings were cleared, so the
  headline key, the detail recipe and the install flag survived: after an install, a later resolve and
  a language flip showed "Resolved" in the headline with "<app> is installed" underneath. The failure
  branch now goes through the same recipe mechanism instead of writing the strings directly.
* The error code panel follows the language. The body was re-rendered on a flip but the title was not,
  so "that is not a code" stayed in the old language above a correctly translated sentence.

## [0.4.15] - 2026-10-27

### Fixed

* A command that fails while `--json` was requested now says so in a document on standard output.
  It used to write only to standard error and leave stdout empty, so a reader piping into a JSON parser
  failed on an empty stream and lost the real reason. Failures from the option guard, from a bad
  argument, from the network, from a cancellation and from a command's own checks all carry a
  `{ succeeded: false, error, kind }` document now.
* A package whose manifest is not valid XML is reported as that, instead of escaping as an
  `XmlException` that ended the whole planning run with a stack trace. A bundle with one unreadable
  child now plans around it and says which child it was.

## [0.4.14] - 2026-10-27

### Fixed

* Tables line up when a cell holds Chinese. Column widths were measured in UTF-16 units, so a path such
  as `D:\下载\…` counted as half its visible width and pushed every following column out of place; the
  printer measures terminal cells now, counting East Asian characters and emoji as two.
* `--arch x46` is rejected as a typing error (exit code 2, naming the values that exist) instead of
  travelling into a selection that matched nothing and being reported as a product problem.

## [0.4.13] - 2026-10-27

### Fixed

* An install started by a script no longer installs without being asked to. The confirmation step was
  skipped whenever standard input was not a terminal, so a piped run deployed packages nobody agreed
  to; without `--yes` it now refuses with exit code 2 and says which switch to pass.
* `explain --json` produces a JSON document for the whole catalogue. The listing and the no-argument
  form were handled before the JSON branch, so a caller asking for JSON got a text table.

## [0.4.12] - 2026-10-27

### Fixed

* The crash dialog works again. The template it renders asked for three placeholders while the handler
  passed two, so the handler threw a `FormatException` of its own: any unhandled exception made the
  window disappear with no message and no log file. The text is now assembled from a template without
  placeholders and every step of the handler is guarded.
* Copying the log, or pasting a link, no longer ends the window when another process holds the
  clipboard open (remote sessions and clipboard managers do that constantly); the failure shows in the
  status line instead.
* The environment check no longer overrides a running stage. The probe is fire and forget from the
  constructor, so a slow one could replace "Resolving…" with "Ready" a moment after a run started.

## [0.4.11] - 2026-10-27

### Fixed

* `install --dry-run` no longer downloads. The manual promises it touches nothing, but resolve,
  download and the manifest write all ran before the dry run gate, so inspecting a product fetched
  every package and left `Sluice-manifest.json` behind. A dry run from a Store reference now reports
  the selection it would fetch, without resolving file URLs or writing anything.
* `doctor --json` returns the same exit code as the text output. The JSON branch returned 0
  unconditionally, so a scripted caller saw success on a machine the text output called unfit (3).
* An option a command does not take is now an error instead of a silent no-op: `sluice download <url>
  --al-arch` used to run with its defaults while the caller believed the typo had been applied. The
  accepted set is derived from the same manual `Sluice help <command>` prints, and
  `--no-force-shutdown`, which install already honoured, is documented at last.
* A cancelled or timed out run and an IO failure now end with a one line message instead of a stack
  trace.

## [0.4.10] - 2026-10-27

### Fixed

* A package the plan could not use now says why. An encrypted Store package and a truncated download
  were both reported as "not a readable appx/msix package", which sends the operator after the wrong
  problem; the analyzer's own explanation is carried into the plan instead.
* The activity log is capped at a thousand lines and drops the oldest. It used to keep every line for
  the life of the process, and since the log is a plain (non-virtualised) list, each line also held a
  visual. Copying it now prints the severity in the language the window speaks.

## [0.4.9] - 2026-10-27

### Fixed

* The loose file fallback (`-Register`, used when a package cannot be staged) unpacks into
  `%LOCALAPPDATA%\sluice\registered` instead of the temp folder, because the registration points into
  that folder and a temp cleaner deleting it breaks an app Windows still lists as installed. A failed
  attempt now removes its own files, where before every retry left a few hundred megabytes behind.
* A download only retries failures that can clear up by themselves. A full disk, a path that cannot
  exist or a file the process may not write used to be retried three times with six seconds of
  sleeping before failing with "after 3 attempts"; they are now reported at once, with the reason the
  operating system gave in the message instead of buried in an inner exception.

## [0.4.8] - 2026-10-27

### Fixed

* A bundle without a package for the target architecture is now reported as such instead of having
  the biggest foreign build selected. The planner's "bundle has no application package for this
  architecture" error could never fire before, so the install failed later with 0x80073CFD.
* An install whose registration could not be read back is reported as unverified instead of failed.
  `Get-AppxPackage` failing on its own made the verify step say "Nothing is registered for …
  after the install" and the run end as a failure, sending people after a package Windows had just
  installed; the reason is now carried in `InstallOutcome.VerificationProblem`, printed by the
  command line and shown in the window.

## [0.4.7] - 2026-10-27

### Fixed

* The delivery service session is renewed instead of trusted for the life of the process. The cookie
  expires while the window is open, the service answers such a request with a fault that says nothing
  about the cause, and every later call failed - reading exactly like an outage - until the app was
  restarted. A fault now drops the cookie, fetches a fresh one and repeats the call once.
* The install plan picks the application package built for the target architecture, not the largest
  file in the folder. An offline folder with several builds (`--all-arch`) used to have the arm64
  build staged on an x64 machine, which Windows rejects with 0x80073CFD. When nothing in the folder
  fits, the plan still proceeds but warns that Windows will probably refuse it.

## [0.4.6] - 2026-10-27

### Fixed

* Resolution now falls back to the second catalog endpoint when the first one fails in any way. Only
  `StoreResolutionException` was caught, so an HTML body where JSON was expected, or a socket that
  gave up after its retries, ended the resolution with a raw parser message instead of trying the next
  endpoint.
* An answer that carries no package categories is no longer cached. A struggling endpoint returns
  metadata only, and remembering that for the rest of the process kept telling the user the listing
  was probably a Win32 app long after the endpoint had recovered. The cache is also a
  `ConcurrentDictionary` now, because the window and the command line can resolve at the same time.

## [0.4.5] - 2026-10-27

### Fixed

* The planner only skips a framework when the installed copy is built for the target architecture.
  An x86 framework sitting on an x64 machine used to count as "already installed", so the plan
  omitted the x64 package and the main install failed with 0x80073CF3 - a code whose own explanation
  blames a missing framework.
* Eight digit HRESULTs written without the `0x` prefix are read as hex. `80070005` used to be taken
  as the decimal 80,070,005, so `explain 80070005` answered "0x04C5C575 is not in the catalogue"
  while `explain 0x80070005` explained access denied. A value with the top bit set cannot be a
  decimal HRESULT, which is what makes the two forms distinguishable.

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
