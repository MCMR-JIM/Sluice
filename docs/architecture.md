# Architecture

Five layers, each usable on its own, so the tool can be scripted in pieces.

## Contents

* [Layers](#layers)
* [Design decisions](#design-decisions)
* [Testing](#testing)
* [Repository conventions](#repository-conventions)

## Layers

| Layer | Namespace | Responsibility |
| --- | --- | --- |
| Store | `StoreDownloader.Store` | Parse input, resolve product metadata and `WuCategoryId` |
| Delivery | `StoreDownloader.Fe3` | Expand a category into packages with signed URLs |
| Packages | `StoreDownloader.Packages` | Read manifests, choose architecture and resources |
| Download | `StoreDownloader.Download` | Get bytes on disk, verify them, record a manifest |
| Install | `StoreDownloader.Install` | Plan, deploy, escalate remedies, verify registration |
| Windows | `StoreDownloader.Windows` | PowerShell host, installed inventory, environment probe |
| Diagnostics | `StoreDownloader.Diagnostics` | The deployment HRESULT catalogue |

`StoreDownloader.Cli` only wires them together: it contains command parsing, table rendering and
JSON output, no protocol knowledge.

## Design decisions

### No runtime dependencies

Everything comes from the BCL. `System.Text.Json` for the catalog, `System.IO.Compression` for
package containers (an appx is a zip), `XDocument` for manifests and SOAP, `HttpClient` for the
rest. The result is one small assembly that can be dropped anywhere and cannot break because a
third-party package changed.

### Windows APIs are driven, not reimplemented

Deployment goes through PowerShell and `Add-AppxPackage`, never through undocumented COM.
Consequences:

* failures are real HRESULTs, not guesses;
* the exact command is printed in the plan, so an operator can reproduce or amend it by hand;
* scripts are passed with `-EncodedCommand` to avoid quoting problems;
* child output is pinned to UTF-8, so localized Windows messages survive;
* PowerShell's CLIXML and ANSI noise is cleaned before it reaches the user.

One subtlety worth keeping: `Add-AppxPackage` reports the interesting deployment HRESULT inside the
message text, while `$_.Exception.HResult` carries a generic wrapper code. The installer parses the
message first and only falls back to the exception.

### Everything that touches the machine is behind an interface

`IInstalledPackageInventory` is implemented by `AppxInventory` (PowerShell) and by a fake in the
tests, so planning logic is tested without a Windows machine. The planner, the installer and the
probe all fail softly: a missing PowerShell or a broken cmdlet becomes a warning in the plan, not an
exception in the user's face.

### Protocol behaviour is data, not code

The device profile the delivery service expects lives in `WuDeviceProfile`, with a comment on why
each part matters. The deployment error catalogue is a table. When Microsoft changes something there
is exactly one obvious place to edit, and a fixture test that says whether the parser still
understands the shape.

### Selection follows the manifest, not the catalog

A Store category advertises framework packages for every architecture and runtime generation; a
20 MiB app can be surrounded by hundreds of megabytes of them. `install` therefore downloads the
application first, reads the dependencies out of its (possibly bundled) manifest, and fetches only
those. `--all-deps` restores the "download everything" behaviour for offline archives.

## Testing

`dotnet test` runs offline and needs neither network nor a deployment stack:

| Area | What is covered |
| --- | --- |
| Input | Store URLs, deep links, bare ids, market and locale hints |
| Delivery parser | A checked-in `SyncUpdates` fixture: numeric join, block map filtering, duplicate collapsing, digest and flag mapping |
| Identifiers | `~` resource ids, dots and underscores in names, version parsing, comparison |
| Manifests | Identity, dependencies, device family, resource packages, bundle children, encrypted rejection |
| Selection | Architecture scoring, resource language matching, main versus dependency choice, skipped-file explanations |
| Planning | Framework ordering, missing dependencies, already installed, unreadable inventory, bundle inner manifests |
| Downloads | File name sanitising, digest verification paths, manifest round-trips |
| Errors | HRESULT parsing in hex, decimal and signed form; catalogue completeness |
| CLI | Argument parsing, value-less switches, help completeness, version reporting |

Fixtures build real appx, msix and bundle containers into a temporary folder at test time, so
manifest reading is exercised against actual ZIP archives rather than mocks.

## Repository conventions

* Conventional commits, one intent per commit, English.
* Work happens on `feat/*`, `fix/*`, `ci/*` and `docs/*` branches and lands through a pull
  request; `main` only ever moves by merge commit.
* Merged head branches are deleted automatically (`delete_branch_on_merge`). Branches that were
  merged before that setting existed are swept by the `cleanup` workflow:
  `gh workflow run cleanup.yml`.
* CI runs on Linux and Windows for every pull request and cross publishes all seven runtime
  identifiers, so a platform assumption cannot land unnoticed.
* Releases are tagged on `main`: `git tag -a vX.Y.Z -m "..." && git push origin vX.Y.Z`. The
  `release` workflow publishes the binaries, updates a release when it is re-run, and appends
  generated notes to the preamble it carries.
* The changelog is updated in the same pull request that adds the behaviour.
* The build runs analyzers at `latest-recommended`; warnings are review items, not noise.
