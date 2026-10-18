# Brand

The product is **StoreDownloader**. The command line tool is `StoreDownloader.exe`, the window is
`StoreDownloader.UI.exe`. There is no short form: not in the window title, not in the docs, not in
the release assets.

## Where the identity lives

| Surface | Value |
| --- | --- |
| Window title | `StoreDownloader` |
| Header wordmark | `StoreDownloader`, Segoe UI Variable Display, SemiBold |
| Version chip | `0.x.y`, Cascadia Mono, 45% opacity |
| Accent | `#60CDFF` on dark, ink `#04121A` on the accent |
| Surfaces | translucent black at 17% and 22% over the acrylic, hairlines white at 12% |
| Backdrop | Windows acrylic, always; a solid canvas only as the fallback when the compositor refuses |

## The current mark is a placeholder

Two placeholders exist, both drawn in vectors, both meant to be replaced:

* `docs/logo.png` — the 512 px mark used at the top of the readme (a parcel with a download arrow
  on an accent tile);
* the `Viewbox` in `MainWindow.xaml` — the same idea as line art in the window header.

Replace them like this:

1. save the artwork as `docs/logo.png` (512 px or larger, transparent or tiled — the readme scales
   it to 140 px);
2. save the same art as `src/StoreDownloader.Desktop/Assets/app.ico` (16, 20, 24, 32, 40, 48, 64,
   128 and 256 pixel frames in one file);
3. add `<ApplicationIcon>Assets\app.ico</ApplicationIcon>` to
   `src/StoreDownloader.Desktop/StoreDownloader.Desktop.csproj` and `Icon="Assets/app.ico"` on the
   `Window` element, and optionally swap the header `Viewbox` for
   `<Image Source="Assets/app.png" Width="18" Height="18" />`.

## Prompt for generating the icon

Hand this to any image model; it asks for a flat vector mark rather than a rendered illustration,
which is what survives being shrunk to 16 pixels in a taskbar.

> Design a flat vector app icon for a Windows developer tool called StoreDownloader. Subject: a
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

* Product name in prose and UI: `StoreDownloader`.
* Never `storedl`, `SDL`, or any other abbreviation, including in commit subjects about the
  surface (say "the window" or "the command line tool" instead).
* File and folder names keep the same casing as the assemblies: `StoreDownloader.exe`,
  `StoreDownloader.UI.exe`, `StoreDownloader-manifest.json`.
