# StoreDownloader

One-stop resolver, downloader and sideload installer for Microsoft Store (MSIX/APPX) packages.

Give it a Microsoft Store URL or product ID and it will:

1. resolve the real package URLs from Microsoft's delivery services,
2. download the app **and** its framework dependencies, verifying every file,
3. pick the right architecture and resource packages out of the bundle,
4. install everything in dependency order with `Add-AppxPackage`,
5. verify the resulting registration instead of trusting the App Installer UI.

Status: under construction.
