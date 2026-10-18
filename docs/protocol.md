# How a Store link becomes a package

The part no link generator explains, and the part that breaks when Microsoft changes something.
Implemented in `src/StoreDownloader.Core/Store`, `Fe3` and `Packages`.

## 1. A link is not a package

`https://apps.microsoft.com/detail/9nksqgp7f2nh` contains a **product id** (`9NKSQGP7F2NH`).
`www.microsoft.com/store/apps/<id>`, `ms-windows-store://pdp/?ProductId=<id>` and bare ids all
work too. `StoreUrlParser` turns any of them into `StoreRequest(ProductId, Market, Locale)`.

## 2. The catalog gives a category, not files

| Endpoint | Notes |
| --- | --- |
| `displaycatalog.mp.microsoft.com/v7.0/products/{id}` | Richest answer. **Requires a `languages` parameter**, otherwise it returns `400 RequiredParameterMissing`. |
| `storeedgefd.dsx.mp.microsoft.com/v9.0/products/{id}` | Fallback. `Skus[].FulfillmentData` is a JSON **string**, not an object. |

What matters is `FulfillmentData.WuCategoryId`: a GUID that identifies the product's payload in
the Windows Update world, plus `PackageFamilyName` and the advertised architectures.

## 3. Three SOAP calls turn a category into packages

Endpoint: `https://fe3.delivery.mp.microsoft.com/ClientWebService/client.asmx`.

1. **GetCookie**: empty old cookie in, `<EncryptedData>` session cookie out. Every later call needs it.
2. **SyncUpdates**: expands the category. The request must look like a retail Windows Update client:
   * `TicketType` = `Retail` (not `<User/>`),
   * the `InstalledNonLeafUpdateIDs` / `OtherCachedUpdateIDs` lists a real client reports,
   * `FilterAppCategoryIds` set to the `WuCategoryId` from step 2,
   * `DeviceAttributes` reporting the legacy build `10.0.16184.1001`.

   Get one of those wrong and the service still answers `200`, but with a package *tree* that has
   no `Files` and no `SecuredFragment` entries: metadata only, no payload handles. It looks like
   "this app has no packages" and costs hours if you do not know it.
3. **GetExtendedUpdateInfo2** on `client.asmx/secured`: one `<UpdateIdentity>` per update,
   asking for `FileUrl` and `FileDecryption`. The answer maps a base64 **SHA-1 digest** to a
   signed CDN URL on `tlu.dl.delivery.mp.microsoft.com`.

The `SyncUpdates` answer is two halves joined on the update's numeric id:

`@
NewUpdates/UpdateInfo
  <ID>334413813</ID>
  <Xml> <UpdateIdentity UpdateID="..." RevisionNumber="1"/>
        <Properties> <SecuredFragment>FileUrl</SecuredFragment> </Properties>    <- has a url
  </Xml>

ExtendedUpdateInfo/Updates/Update
  <ID>334413813</ID>
  <Xml> <ExtendedProperties PackageIdentityName="5319275A.WhatsAppDesktop"/>
        <Files>
          <File FileName="f6cf9546-....msixbundle" Size="311928961" Digest="..."
                InstallerSpecificIdentifier="5319275A.WhatsAppDesktop_2.2640.100.0_neutral_~_cv1g1gvanyjgm">
            <AdditionalDigest Algorithm="SHA256">...</AdditionalDigest>
          </File>
          <File FileName="Abm_....cab" PatchingType="DynamicMetadata"/>          <- block map, not a package
        </Files>
  </Xml>
`@

`InstallerSpecificIdentifier` is the package full name without the publisher hash suffix:
`Name_Version_Architecture_ResourceId_PublisherId`, with `~` for "no resource id" and empty
segments collapsed. Package names contain dots and underscores, so the version is found by pattern,
never by counting separators.

## 4. URLs are signed and short lived

CDN URLs carry `P1...P4` query parameters that expire within hours, and the file names on the CDN
are opaque GUIDs. Resolve immediately before downloading, rebuild readable names
(`Name_Version_Arch.ext`) and verify each file against the published SHA-1 before using it.

## 5. A bundle is not one package

`.msixbundle` / `.appxbundle` are ZIP files with `AppxMetadata/AppxBundleManifest.xml` plus one
application package per architecture and the resource packages. Windows performs architecture and
language selection itself when a *bundle* is deployed; it does not when individual child files are
installed, which is how hand-unpacked bundles end up with the wrong package or a broken
registration. `BundlePackageSelector` mirrors Windows' choice: exact architecture first (an x64
machine also accepts x86, an arm64 machine accepts x86/x64), then resources for the requested
language.

The dependencies that matter live in the *inner* manifest, so `PackageAnalyzer.AnalyzeChild` reads
the child package straight out of the bundle without extracting anything.

## 6. Installing is where most guides go wrong

Handing a downloaded file to **App Installer** (the shell default for `.msix`/`.appx`) means the
missing frameworks are not installed, the wrong architecture may be chosen, the UI cannot tell you
what `0x80073CF3` means, and nothing verifies the result.

`Add-AppxPackage` with an explicit dependency list is the supported path:

`@powershell
$deps = @('.\VCLibs.appx', '.\WindowsAppRuntime.msix')
Add-AppxPackage -Path .\App.msixbundle -DependencyPath $deps -ForceApplicationShutdown
Get-AppxPackage -Name 5319275A.WhatsAppDesktop   # read the registration back
`@

The tool builds exactly that command per plan step, escalates on the HRESULT that comes back, and
finally compares the registered identity and version with the plan.
