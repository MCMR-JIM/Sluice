# Protocol notes

How a Store link becomes a package, end to end. This is the part link generators do not explain
and the part that breaks when Microsoft changes something.

Relevant code: `src/Sluice.Core/Store`, `src/Sluice.Core/Fe3` and
`src/Sluice.Core/Packages`.

## Contents

1. [From link to product id](#1-from-link-to-product-id)
2. [From product to Windows Update category](#2-from-product-to-windows-update-category)
3. [From category to packages](#3-from-category-to-packages)
4. [From package to download URL](#4-from-package-to-download-url)
5. [From bundle to installable packages](#5-from-bundle-to-installable-packages)
6. [From packages to a working install](#6-from-packages-to-a-working-install)

## 1. From link to product id

Every Store link carries a **product id** such as `9NKSQGP7F2NH`. These forms are equivalent:

| Input | Notes |
| --- | --- |
| `https://apps.microsoft.com/detail/9NKSQGP7F2NH` | Current web link; may carry a slug before the id |
| `https://www.microsoft.com/store/apps/9NKSQGP7F2NH` | Legacy web link |
| `ms-windows-store://pdp/?ProductId=9NKSQGP7F2NH` | Shell deep link |
| `9NKSQGP7F2NH` | Bare id, including legacy 13 character ids |

`StoreUrlParser` normalises all of them into `StoreRequest(ProductId, Market, Locale)` and keeps
any `hl` or `gl` hint the link carries.

## 2. From product to Windows Update category

Two catalog endpoints are queried, in order:

| Endpoint | Notes |
| --- | --- |
| `displaycatalog.mp.microsoft.com/v7.0/products/{id}` | Richest answer. Requires a `languages` parameter, otherwise it answers `400 RequiredParameterMissing` |
| `storeedgefd.dsx.mp.microsoft.com/v9.0/products/{id}` | Fallback. Its `Skus[].FulfillmentData` is a JSON string, not an object |

The field that matters is `FulfillmentData.WuCategoryId`, a GUID identifying the product's payload
in the Windows Update world:

```json
{
  "WuCategoryId": "3dadc9b1-3603-496c-a6d1-bf2fda81df89",
  "WuBundleId": "e00862b1-718d-4c41-969f-29b256de5048",
  "PackageFamilyName": "5319275A.WhatsAppDesktop_cv1g1gvanyjgm",
  "SkuId": "0010"
}
```

The package family name is what later identifies "the app" among its own dependencies.

## 3. From category to packages

Three SOAP calls against `https://fe3.delivery.mp.microsoft.com/ClientWebService/client.asmx`.

### 3.1 GetCookie

An empty old cookie goes in, a session cookie comes out. Every later call needs it.

```xml
<GetCookie xmlns="http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService">
  <oldCookie></oldCookie>
  <lastChange>2024-11-20T10:00:00.0000000Z</lastChange>
  <currentTime>2026-01-01T09:00:00.000Z</currentTime>
  <protocolVersion>1.40</protocolVersion>
</GetCookie>
```

### 3.2 SyncUpdates

Expands the category into an update tree. The request has to look like a retail Windows Update
client, and four parts of it are load bearing:

| Field | Value | Why |
| --- | --- | --- |
| `TicketType` | `Retail` (not `<User/>`) | Without it the service answers with metadata only |
| `InstalledNonLeafUpdateIDs`, `OtherCachedUpdateIDs` | The ids a real client reports | Defines which parts of the tree get expanded |
| `FilterAppCategoryIds` | The `WuCategoryId` from step 2 | Selects the product |
| `DeviceAttributes` | Includes the legacy build `10.0.16184.1001` | A current build string yields a metadata-only answer |

Both wrong-value cases return `200 OK` with a **package tree that contains no `Files` and no
`SecuredFragment` entries**. It looks like "this app has no packages" and costs hours to diagnose.
`WuDeviceProfile` in the code holds the profile; the comment there explains each field.

### 3.3 Join the two halves of the answer

`SyncUpdates` returns the update identities in `NewUpdates` and the payload descriptions in
`ExtendedUpdateInfo`, keyed by the same numeric id:

```xml
<!-- NewUpdates/UpdateInfo -->
<ID>334413813</ID>
<Xml>
  <UpdateIdentity UpdateID="27d58f36-de5d-4af8-99fa-a95b9e597a50" RevisionNumber="1" />
  <Properties>
    <SecuredFragment>FileUrl</SecuredFragment>
  </Properties>
</Xml>

<!-- ExtendedUpdateInfo/Updates/Update -->
<ID>334413813</ID>
<Xml>
  <ExtendedProperties PackageIdentityName="5319275A.WhatsAppDesktop" />
  <Files>
    <File FileName="f6cf9546-2f05-4c34-a71f-6f86602f6652.msixbundle"
          Size="311928961"
          Digest="G3uEugLNwE0uzlVYbfrbSPcNhzs="
          InstallerSpecificIdentifier="5319275A.WhatsAppDesktop_2.2640.100.0_neutral_~_cv1g1gvanyjgm">
      <AdditionalDigest Algorithm="SHA256">LjsrJ21j6HfHNqM2NLJnH6cRF7AWyF+2vXk3E8FmZ5E=</AdditionalDigest>
    </File>
    <File FileName="Abm_f6cf9546-2f05-4c34-a71f-6f86602f6652.cab"
          PatchingType="DynamicMetadata" />
  </Files>
</Xml>
```

Rules that follow from this shape:

* a `File` with `PatchingType="DynamicMetadata"` is a block map side file, not a package;
* a `File` without `InstallerSpecificIdentifier` is never a package;
* `InstallerSpecificIdentifier` is the package full name without the publisher hash:
  `Name_Version_Architecture_ResourceId_PublisherId`, with `~` meaning "no resource id" and empty
  segments collapsed;
* package names contain dots and underscores, so the version is located by pattern, never by
  counting separators. `PackageIdentifier.TryParse` does this and is covered by tests.

Identical entries can appear under several update ids. The list is de-duplicated in favour of the
copy that carries a URL.

## 4. From package to download URL

`GetExtendedUpdateInfo2` on `client.asmx/secured` exchanges an update identity for a signed CDN
URL. Requests are batched ten at a time:

```xml
<GetExtendedUpdateInfo2 xmlns="http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService">
  <updateIDs>
    <UpdateIdentity>
      <UpdateID>27d58f36-de5d-4af8-99fa-a95b9e597a50</UpdateID>
      <RevisionNumber>1</RevisionNumber>
    </UpdateIdentity>
  </updateIDs>
  <infoTypes>
    <XmlUpdateFragmentType>FileUrl</XmlUpdateFragmentType>
    <XmlUpdateFragmentType>FileDecryption</XmlUpdateFragmentType>
  </infoTypes>
  <deviceAttributes>...</deviceAttributes>
</GetExtendedUpdateInfo2>
```

The answer maps a base64 **SHA-1 digest** to a URL:

```xml
<FileLocations>
  <FileLocation>
    <FileDigest>G3uEugLNwE0uzlVYbfrbSPcNhzs=</FileDigest>
    <Url>http://tlu.dl.delivery.mp.microsoft.com/filestreamingservice/files/...</Url>
  </FileLocation>
</FileLocations>
```

That digest is the key back to the package: it is why resolution matches on SHA-1 rather than on
file names, and why downloads are verified against the same digest before use.

URLs carry `P1` to `P4` signature parameters and expire within hours. File names on the CDN are
opaque GUIDs, so the tool rebuilds readable names (`Name_Version_Arch.ext`) from the installer
specific identifier.

## 5. From bundle to installable packages

`.msixbundle` and `.appxbundle` are ZIP containers: `AppxMetadata/AppxBundleManifest.xml` plus
one application package per architecture and the resource packages.

```xml
<Bundle xmlns="http://schemas.microsoft.com/appx/2013/bundle">
  <Identity Name="5319275A.WhatsAppDesktop" Publisher="CN=..." Version="2.2640.100.0" />
  <Packages>
    <Package Type="application" Version="2.2640.100.0" Architecture="x64"
             FileName="5319275A.WhatsAppDesktop_2.2640.100.0_x64.msix" Offset="0" Size="..." />
    <Package Type="resource" Version="2.2640.100.0" Architecture="neutral" ResourceId="zh-CN"
             FileName="5319275A.WhatsAppDesktop_2.2640.100.0_neutral_zh-CN.msix" Offset="..." Size="..." />
  </Packages>
</Bundle>
```

Windows performs architecture and language selection itself when a **bundle** is deployed. It does
not when individual child files are installed, which is how hand-unpacked bundles end up with the
wrong package or a broken registration. `BundlePackageSelector` mirrors Windows' choice:

1. an exact architecture match wins;
2. a compatible fallback is accepted (an x64 machine also runs x86, an arm64 machine runs x86 and x64);
3. `neutral` is the last resort;
4. resource packages are kept for the requested language, plus scale and contrast qualifiers.

The dependencies that matter live in the *inner* application manifest, so
`PackageAnalyzer.AnalyzeChild` reads the child package straight out of the bundle without
extracting anything.

## 6. From packages to a working install

Handing a downloaded file to **App Installer**, the shell default for `.msix` and `.appx`, means
missing frameworks are not installed, the wrong architecture may be chosen, the error cannot be
explained, and nothing verifies the result.

`Add-AppxPackage` with an explicit dependency list is the supported path:

```powershell
$deps = @('.\Microsoft.VCLibs.140.00_14.0.33728.0_x64.appx', '.\Microsoft.WindowsAppRuntime.2_2.5.1.0_x64.msix')
Add-AppxPackage -Path .\5319275A.WhatsAppDesktop_2.2640.100.0_neutral.msixbundle -DependencyPath $deps -ForceApplicationShutdown
Get-AppxPackage -Name 5319275A.WhatsAppDesktop
```

The tool issues exactly that command per plan step, escalates on the HRESULT that comes back (see
[docs/troubleshooting.md](troubleshooting.md)), and finally compares the registered identity and
version with the plan. A deployment that reports success but registers nothing is treated as a
failure.
