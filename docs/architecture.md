# Architecture

Five layers, each usable on its own, so the tool can be scripted in pieces.

`@
Store URL  ->  Store        product metadata, WuCategoryId
               Fe3          packages + signed urls
               Packages     manifests, architecture/resource choice
               Download     bytes on disk
               Install      plan, deploy, verify
               Diagnostics  what Windows actually said
`@

## Design decisions worth knowing

**No runtime dependencies.** Everything is BCL: `System.Text.Json`, `System.IO.Compression`
(an appx *is* a zip), `XDocument` for manifests and SOAP, `HttpClient` for everything else. The
result is a single small assembly that can be dropped anywhere.

**Windows APIs are driven, not reimplemented.** Deployment goes through PowerShell and
`Add-AppxPackage`, never through undocumented COM. Consequences: real HRESULTs, real behaviour,
and the exact command is printed in the plan so an operator can reproduce or amend it by hand.
Scripts are passed with `-EncodedCommand` to avoid quoting games, child output is pinned to UTF-8
so localized Windows messages survive, and PowerShell's CLIXML noise is cleaned before it reaches
the user.

**Everything that touches the machine is behind an interface.**
`IInstalledPackageInventory` is implemented by `AppxInventory` (PowerShell) and by a fake in the
tests, so planning logic is tested without a Windows machine.

**Protocol behaviour is data, not code.** The device profile the delivery service expects lives in
`WuDeviceProfile` with a comment on why each part matters; the deployment error catalogue is a
table. When Microsoft changes something there is one obvious place to edit.

**URLs are treated as short lived.** A resolution is valid for minutes, so the CLI resolves,
selects, downloads and installs in one process, and writes `storedl-manifest.json` next to the
files so a later offline install knows what it is looking at.

## Testing

`dotnet test` runs entirely offline:

* URL and deep link parsing,
* the delivery service parser against a checked-in fixture of the real answer shape (including
  block map side files and duplicate update entries),
* package identifier edge cases (`~` resource ids, underscores and dots in names, versions),
* manifest reading and bundle child selection, using appx/msix/bundle containers built by the tests,
* dependency planning against a fake inventory: ordering, missing dependencies, already installed,
  unreadable inventory,
* HRESULT parsing and the error catalogue,
* download file naming and manifest round-trips.

## Repository conventions

* Conventional commits, one intent per commit, English.
* `main` only receives merge commits from feature branches.
* `feat/*` per capability, `test/*` and `fix/*` for their own work.
* Analyzers run as part of the build; warnings are review items, not noise.
