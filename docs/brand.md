# Brand

The product is **Sluice**. The command line tool is `Sluice.exe`, the window is
`Sluice.UI.exe`. There is no short form: not in the window title, not in the docs, not in
the release assets.

## Where the identity lives

| Surface | Value |
| --- | --- |
| Window title | `Sluice` |
| Header wordmark | `Sluice`, Segoe UI Variable Display, SemiBold |
| Version chip | `0.x.y`, Cascadia Mono, 45% opacity |
| Accent | `#60CDFF` on dark, ink `#04121A` on the accent |
| Surfaces | translucent black at 17% and 22% over the acrylic, hairlines white at 12% |
| Backdrop | Windows acrylic, always; a solid canvas only as the fallback when the compositor refuses |

## The mark

`assets/logo.png` is the artwork. Everything else is derived from it, and repository artwork lives
in an `assets` folder rather than next to the documentation:

| File | Used by |
| --- | --- |
| `assets/logo.png` | the readme, at 140 px wide |
| `src/Sluice.Desktop/Assets/app.ico` | the window title bar, the taskbar and the exe itself |
| `src/Sluice.Desktop/Assets/app.png` | the 16 px mark in the window header |

The ICO carries 16, 24, 32, 48, 64, 128 and 256 pixel frames, PNG compressed, with the white
background keyed out so the mark reads on a dark taskbar.

Replacing the artwork means regenerating those three files from the new PNG; the project picks them
up through `<ApplicationIcon>Assets\app.ico</ApplicationIcon>`, `Icon="Assets/app.ico"` on the window
and `<Image Source="Assets/app.png">` in the header.

## Prompt for generating the icon

Hand this to any image model; it asks for a flat vector mark rather than a rendered illustration,
which is what survives being shrunk to 16 pixels in a taskbar.

> Design a flat vector app icon for a Windows developer tool called Sluice. Subject: a
> shipping parcel seen in three-quarter view, its lid open, with a downward arrow sliding into it;
> the arrow and the parcel share one continuous stroke so the shape reads at 16 px. Style: bold
> geometric line art, uniform 2.5 px strokes, rounded joints, no gradients, no texture, no drop
> shadow, no text. Composition: centred, optically balanced, 14% padding inside a 1024 by 1024
> canvas, flat single-tone mark on a transparent background. Colour: mark in near black
> `#04121A` for light mode and in Windows accent blue `#60CDFF` for dark mode; provide both
> variants. Deliver 1024 by 1024 PNG, transparent, plus the same art inside a rounded square
> (`22%` corner radius) filled with `#1F1F1F` for contexts that need a tile.

If you would rather iterate on something more distinctive, ask for an alternative brief: *"a
staircase of three stacked parcels, the top one dissolving into a pixel grid, same constraints"*.

## Naming rules

* Product name in prose and UI: `Sluice`.
* Never `sluice`, `SDL`, or any other abbreviation, including in commit subjects about the
  surface (say "the window" or "the command line tool" instead).
* File and folder names keep the same casing as the assemblies: `Sluice.exe`,
  `Sluice.UI.exe`, `Sluice-manifest.json`.
