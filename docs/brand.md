# Brand

The product is **Sluice**. The command line tool is `Sluice.exe`, the window is
`Sluice.UI.exe`. There is no short form: not in the window title, not in the docs, not in
the release assets.

## Where the identity lives

| Surface | Value |
| --- | --- |
| Window title | `Sluice` — the caption bar, no wordmark repeated inside the window |
| Version | `0.x.y`, Cascadia Mono at 45% opacity, right of the machine chips |
| Accent | `#60CDFF` on dark, ink `#04121A` on the accent |
| Plane | one flat semi transparent black, `#85000000` (52%), no gradient and no tint |
| Components | opaque: panels `#1E1E1E`, inputs `#151515`, raised `#2A2A2A`, hairlines white at 12% |
| Backdrop | Windows acrylic (`DWMSBT_TRANSIENTWINDOW`) only in the gaps and at the window edge; a solid canvas when the compositor refuses |

## The mark

`assets/logo.png` is the artwork. Everything else is derived from it, and repository artwork lives
in an `assets` folder rather than next to the documentation:

| File | Used by |
| --- | --- |
| `assets/logo.png` | the readme, at 140 px wide |
| `src/Sluice.Desktop/Assets/app.ico` | the window title bar, the taskbar and the exe itself |
| `src/Sluice.Desktop/Assets/app.png` | the window icon, loaded as a pack resource in `MainWindow` |

The ICO carries 16, 24, 32, 48, 64, 128 and 256 pixel frames, PNG compressed, with the white
background keyed out so the mark reads on a dark taskbar.

Replacing the artwork means regenerating those three files from the new PNG. The project picks them up
through `<ApplicationIcon>Assets\app.ico</ApplicationIcon>` for the executable and through
`Icon = new BitmapImage(pack://application:,,,/Assets/app.png)` in `MainWindow`; the process also claims
the AppUserModelID `MCMR-JIM.Sluice` so the shell keeps the mark on the taskbar instead of caching one
per executable path.

## Naming rules

* Product name in prose and UI: `Sluice`.
* Never `sluice`, `SDL`, or any other abbreviation, including in commit subjects about the
  surface (say "the window" or "the command line tool" instead).
* File and folder names keep the same casing as the assemblies: `Sluice.exe`,
  `Sluice.UI.exe`, `Sluice-manifest.json`.