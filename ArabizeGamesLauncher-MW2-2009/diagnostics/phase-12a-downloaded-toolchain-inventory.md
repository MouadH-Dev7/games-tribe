# Phase 12A — Downloaded Toolchain Inventory

**Audit date:** 2026-11-03  
**Mode:** Read-only local metadata, documentation, and source review. No downloaded tool was launched or built; no network, registry, game directory, or CASC game payload was accessed. The report is saved outside the game installation.

## 1. Executive Summary

- `CASCExplorer` is a prebuilt, unsigned application bundle with no source or Git metadata in the inspected folder. Its config explicitly maps product `lazr` to “Call of Duty: MW2 Campaign Remastered”, but this proves only that the UI config recognizes the product code, not that the bundled parser supports build `1.1.2.1279292` or its manifests. **Verdict: `UNVERIFIED BINARY`.**
- `CascLib-master` is a C/C++ source snapshot whose README names upstream `https://github.com/ladislav-zezula/CascLib.git`. It implements an INSTALL-manifest root handler and supports several CASC index layouts, but has no `.git` commit available, contains no `lazr`/MW2-specific source reference, supports only INSTALL manifest version 1 with CKey size at most 16 bytes, and its install-root handler skips tag data rather than exposing per-file locale associations. It is a **candidate for further static review**, not yet qualified to answer the Arabic inventory question or exact-build compatibility.
- `Release-win-x64` is a separate `wow.tools.local` ASP.NET Core application bundle, not CASCExplorer or CascLib. Its source/provenance is absent and its main binary is unsigned. It is not a qualified MW2CR tool. **Verdict: `UNVERIFIED BINARY` / `NOT SUITABLE` for this task.**
- Local installers are present for OmegaT 6.1.1 and Poedit 3.10; both have valid Authenticode signatures and metadata consistent with their product names. Neither was launched. Installed status and bundled help were not checked. **Recommendation:** OmegaT as the main translation workspace after resources have been converted to a supported exchange format; Poedit only as a secondary editor if the extracted format is gettext `.po`.
- **No tool is approved by this audit for opening the target game storage.** Next safe step: static review of the exact CascLib source commit represented by CASCExplorer’s bundled CascLib DLL, then a limited, separately authorized, offline synthetic-manifest test of tag reporting. Exact MW2CR storage compatibility remains unverified.

## 2. CASCExplorer Inventory

**Local folder:** `C:\Users\mouad\Downloads\Compressed\CASCExplorer`

The folder contains 22 files: the application, bundled libraries and PDBs, an application config, and a 140,537,894-byte `listfile.csv`. No README, source tree, `.git`, release notes, or provenance manifest is present.

### Identity and metadata

| File | Evidence |
|---|---|
| `CASCExplorer.exe` | Product `CASCExplorer`; file/product version `1.0.0.0`; no company or original filename field; SHA-256 `E74472C0BAA1B878F3A2DF032CD5FB14E37A19B76B97D4B38133AC6DC24F6F79`; Authenticode `NotSigned`. |
| `CascLib.dll` | Product `CascLib`; product version `1.0.0+79abf98f17ac9bfc5b96922f4ccde9338a45a924`; company `TOM_RUS`; SHA-256 `F977B48A957AE08ED708951EDF802A2653C68FC7C67A381ED287C449B94730BC`; `NotSigned`. The embedded commit-like suffix is useful identity metadata, but does not authenticate the binary or establish which source snapshot was used. |
| `CASCExplorer.exe.config` | .NET Framework 4.7.2 target. Contains an `OnlineStorageList` with `lazr = Call of Duty: MW2 Campaign Remastered` (line 88) and other products. Contains user settings including `RecentStorages`, `LocaleFlags` and `ListFilePath`. |
| Other bundled DLLs | File versions and hashes are listed in §8. No trust inference is made from filenames alone. |

Exact config evidence: `C:\Users\mouad\Downloads\Compressed\CASCExplorer\CASCExplorer.exe.config`, `OnlineStorageList` entry `lazr`; the same file has a `userSettings` section with `RecentStorages` and other preferences. This config suggests product selection and user settings; it does not prove the executable’s code path or compatibility with the installed build.

### Capabilities and limitations

- **Product selection:** `lazr` is explicitly configured. **CONFIRMED from config.**
- **Supported CASC formats/games from source:** **NOT VERIFIED** because no source is present. The config lists many Blizzard product codes; a product-list entry is not proof of each storage layout.
- **INSTALL manifest listing:** **NOT VERIFIED for this application.** The bundled CascLib product is present, and the separate source snapshot has an INSTALL handler, but the bundle’s code/source was not matched to that snapshot.
- **Listing without payload export:** **NOT VERIFIED for the GUI.** A separate CascLib API can enumerate root names, but no CASCExplorer source was available to confirm which calls the application makes.
- **Network:** The config’s `OnlineStorageList` implies online-storage support is intended (**INFERRED**); exact startup behavior/network calls are **NOT VERIFIED**. CascLib source does contain online/download features (see §3).
- **Writes:** The config has user-persisted settings, and this is an explorer with a large list file, but logs/cache/output behavior cannot be established from the config alone. GUI export or other writes are not ruled out. No app was launched.
- **Read-only mode:** No documented or source-verified enforcement exists in this folder. Do not use it against the game until its exact code/version, offline mode, output behavior, and parser compatibility are reviewed.

**CASC verdict: `UNVERIFIED BINARY`.**

## 3. CascLib Inventory

**Local folder:** `C:\Users\mouad\Downloads\Compressed\CascLib-master`

The folder is a C/C++ source snapshot with Visual Studio project/solution files, CMake configuration, tests, source, and bundled zlib implementation. No `.git` directory is present, so commit, branch, and tags are unavailable. `README.md` explicitly directs users to `https://github.com/ladislav-zezula/CascLib.git`. `src\CascLib.h` declares version `3.0` (`CASCLIB_VERSION_STRING "3.0"`). The Debian packaging changelog says `libcasc (3.2)` from 2021, but that package metadata does not override the source header’s declared library version or establish the snapshot’s upstream commit.

### Manifest, index, and payload support

- `src\CascRootFile_Install.cpp` documents an INSTALL-manifest root handler. `CaptureInstallHeader` accepts magic `DL`, version `1`, and rejects EKey/CKey length greater than 16 bytes (lines 70–90). `RootHandler_CreateInstall` invokes the parser; the root advertises file names and root CKeys (lines 18–25, 93–113).
- In `TRootHandler_Install::Load`, tag records are skipped using `GetTagBitmapLength` (lines 38–44), then filenames and keys are inserted into a tree (46–60). The reviewed handler does not retain or expose which manifest tag bitmap applies to each filename. It can therefore help build a filename/root listing, but by itself does not answer “which entries are `arSA text` or `arSA speech`?”
- `src\CascIndexFiles.cpp` contains index format detection for legacy and newer filename forms; `src\CascCommon.h` documents index revisions 5 and 7. This is evidence of general index support, not proof of the exact `lazr` index variant/build.
- Source search found no `lazr`, “MW2 Campaign”, or `LAZR` game-specific references under `src`. Exact product/build support is **NOT VERIFIED**.
- `src\CascReadFile.cpp` opens local `data.NNN` streams read-only, but the library supports download behavior when its online features are enabled. `src\CascLib.h` defines `CASC_FEATURE_ONLINE` and `CASC_FEATURE_FORCE_DOWNLOAD`; `src\CascOpenStorage.cpp` contains online manifest retrieval logic. An offline-only caller must explicitly avoid online features.
- This is a general-purpose library, not a read-only sandbox. The source also contains dump/output helpers (`CascDumpData.cpp`) and optional filename-output code (`CascCommon.h`). No archive-rewrite API was established in this limited review, but output-writing functionality exists and no global read-only mode was identified.

### Preliminary parser-risk observations

`CascRootFile_Install.cpp` uses `strlen` on manifest strings and advances pointers based on parsed string/bitmap lengths. The header validates some basic fields, but this short review did not establish comprehensive bounds validation for every tag/name/entry or robustness against malformed content. This is a review lead, not a demonstrated exploit. Do not point it at untrusted data until the parser and allocations are reviewed and hardened. The full parser, index, encoding, BLTE, and allocation paths have not received a complete security audit here.

### Relationship to bundled CascLib DLL

The separate CascLib source snapshot and `CASCExplorer\CascLib.dll` share the same named upstream project lineage, but cannot be proven identical. The DLL has version text ending in `79abf98f17ac9bfc5b96922f4ccde9338a45a924`; the source folder lacks Git metadata and its exact commit cannot be compared. Therefore it is **not confirmed** that `CascLib-master` is the submodule/source used to build the explorer’s DLL.

**CascLib verdict: `SAFE CANDIDATE FOR FURTHER STATIC REVIEW` only.** This is not approval to compile or run it against MW2CR.

## 4. Release-win-x64 Inventory

**Local folder:** `C:\Users\mouad\Downloads\Compressed\Release-win-x64`

There are 190 files. Extension counts: 49 `.br`, 49 `.gz`, 25 `.html`, 19 `.js`, 13 `.webp`, 10 `.css`, 4 `.map`, 3 `.json`, 3 `.dll`, and one each `.config`, `.data`, `.eot`, `.exe`, `.ico`, `.otf`, `.png`, `.svg`, `.symbols`, `.ttf`, `.wasm`, `.woff`, and `.woff2`. The files include a `wwwroot` web asset tree, application settings, `web.config`, and a large self-contained application executable. No README/release notes/source or `.git` metadata was found in the folder.

The executable’s embedded metadata identifies **`wow.tools.local`**, file version `0.9.10.0`, product version `0.9.10+dd0f7a03355e3178533fed7f42d3b635cb9bf8f5`, company `wow.tools.local`, original filename `wow.tools.local.dll`. SHA-256 `FD5410FD11AA868DE95F2D08644CA906AC302014208B361053B919A52E6112A1`; Authenticode `NotSigned`.

The appsettings files are generic ASP.NET Core logging/host settings; `web.config` and `wow.tools.local.staticwebassets.endpoints.json` further identify a web application deployment. No CASCExplorer or CascLib attribution was found. The bundle’s actual CASC handling, network behavior, and outputs cannot be determined without source or running it, both outside scope.

### Executable and library hashes

| File | Version/product metadata | Signature | SHA-256 |
|---|---|---|---|
| `aspnetcorev2_inprocess.dll` | ASP.NET Core Module Request Handler, 20.0.26296.14 | Valid, Microsoft .NET | `6D67D9B52A02CDCE5B6EE3F6E9E022891E474EE554B168943EE9C14512B1FEAF` |
| `e_sqlite3.dll` | No version/company metadata | NotSigned | `B082E8A72AD4CE908234EAC970B09B7D328D16157CEC34A3EBCCFD9DFE2221C6` |
| `libvips-42.dll` | No version/company metadata | NotSigned | `06DAB07CC386748513337A31672B1D5269FBC770A14FF403080575665BF0813F` |
| `wow.tools.local.exe` | 0.9.10.0; `wow.tools.local` | NotSigned | `FD5410FD11AA868DE95F2D08644CA906AC302014208B361053B919A52E6112A1` |

**Verdicts: `UNVERIFIED BINARY`; `NOT SUITABLE` as a qualified MW2CR CASC inspection component.**

## 5. Relationship Between the CASC Downloads

- CASCExplorer is a bundled binary application that includes a `CascLib.dll`; no source was downloaded into its folder.
- `CascLib-master` is a separate source snapshot claiming the upstream CascLib repository in its README. Same project lineage is plausible, but matching commit/build relationship to the bundled DLL is **NOT CONFIRMED**.
- `Release-win-x64` is identified as a separate `wow.tools.local` web application and is **not** the CASCExplorer release or CascLib source.
- None of these local artifacts proves exact compatibility with MW2CR build `1.1.2.1279292`. The configured `lazr` selector in the GUI and generic manifest support in library source are useful leads, not build-specific validation.

## 6. OmegaT Inventory

**Installer:** `C:\Users\mouad\Downloads\Programs\OmegaT_6.1.1_Windows_64_signed.exe`  
**Size:** 418,235,776 bytes. **Embedded product/version/company:** OmegaT / 6.1.1 / OmegaT. **Authenticode:** Valid; signer `Open Source Developer HIROSHI MIURA` (certificate subject as reported locally). **SHA-256:** `08C99736CF55564BD5089AF6646C2CED16A606C4095A3B9DF5CA192CE12772A2`.

The file is an installer executable by its filename and PE metadata. It was not launched. Its presence does not establish that OmegaT is installed. No local OmegaT help or translation-format documentation was found in the inspected download locations.

**Workflow assessment:** **SUITABLE FOR MAIN TRANSLATION WORKFLOW**, conditionally. OmegaT is the better general translation-memory/glossary workspace once extracted game text is converted to a format OmegaT can import/export. This audit did not verify this installer’s bundled format filters, Arabic bidi display/editing behavior, markup preservation, or template behavior. Before large-scale translation, perform a harmless synthetic Arabic/Latin and placeholder round-trip test in an authorized phase. It is not an extractor or game resource packer.

## 7. Poedit Inventory

**Installer:** `C:\Users\mouad\Downloads\Poedit-3.10-setup.exe`  
**Size:** 19,743,568 bytes. **Embedded product/version/company:** Poedit / 3.10 / Vaclav Slavik. **Authenticode:** Valid; signer `Václav Slavík`. **SHA-256:** `BC14CBB57EECAEDD358FB61EFC6464AB9F54B9E63A745C4B6CDDCE605E2B4E5A`.

The file is an installer executable by name and metadata. It was not launched. Installed status and bundled documentation were not checked. Its metadata is consistent with a signed Poedit installer; it does not establish features beyond its product identity.

**Workflow assessment:** **SUITABLE AS SECONDARY TOOL**, conditionally. Poedit is useful if a later extractor produces gettext `.po` catalogs and its Arabic/Unicode and validation behavior is verified. For an unknown game-specific table or raw format it is not a substitute for a parser or custom conversion pipeline; likely redundant if text is already managed in OmegaT using another supported interchange format.

## 8. Security and Provenance Findings

### SHA-256 of CASCExplorer executable and libraries

| File | SHA-256 | Authenticode |
|---|---|---|
| `Be.Windows.Forms.HexBox.dll` | `4938A4DBB51FD8D35DFDF2C5D42E9A127B9365D495461864E6BB9EC7FC9A3CB7` | NotSigned |
| `CASCExplorer.exe` | `E74472C0BAA1B878F3A2DF032CD5FB14E37A19B76B97D4B38133AC6DC24F6F79` | NotSigned |
| `CASCExplorer.ViewPlugin.dll` | `75B0FBDABE16DEF005612E9804334414DE09588A2F0FC70D7C3B24439FF9B518` | NotSigned |
| `CascLib.dll` | `F977B48A957AE08ED708951EDF802A2653C68FC7C67A381ED287C449B94730BC` | NotSigned |
| `MimeKitLite.dll` | `88302A5438B34148439734163BA2F6337CDC0EB9295E4340AD6B7D37862D476B` | NotSigned |
| `Newtonsoft.Json.dll` | `E1E27AF7B07EEEDF5CE71A9255F0422816A6FC5849A483C6714E1B472044FA9D` | Valid, Microsoft Windows signer |
| `SereniaBLPLib.dll` | `E06D2A8067DADDDE5A3CF2312A8434553F9DE88682C388BB9FF4C529F5EFA20B` | NotSigned |
| `SixLabors.ImageSharp.dll` | `B39C8B9CE64D323D9D3EE8B94B7C84C66AE4727DD1BD77A5554C075764813C1A` | NotSigned |
| `System.Buffers.dll` | `ACCCCFBE45D9F08FFEED9916E37B33E98C65BE012CFFF6E7FA7B67210CE1FEFB` | Valid, Microsoft Windows signer |
| `System.Drawing.Common.dll` | `82536A746C4AC8B2372E2E5C9E28B5A573A213F98E9400C4D959BAB415FB6B77` | Valid, Microsoft .NET signer |
| `System.Memory.dll` | `BF3FB84664F4097F1A8A9BC71A51DCF8CF1A905D4080A4D290DA1730866E856F` | Valid, Microsoft Windows signer |
| `System.Numerics.Vectors.dll` | `1D3EF8698281E7CF7371D1554AFEF5872B39F96C26DA772210A33DA041BA1183` | Valid, Microsoft Corporation signer |
| `System.Runtime.CompilerServices.Unsafe.dll` | `37768488E8EF45729BC7D9A2677633C6450042975BB96516E186DA6CB9CD0DCF` | Valid, Microsoft Windows signer |
| `System.Text.Encoding.CodePages.dll` | `93DA6A111239A3804DA2EFD6A6FAA92BF5CBFE3B2B079AD3C04BE643179F4088` | Valid, Microsoft Corporation signer |

Valid signatures on individual dependencies do not authenticate the application bundle as a whole. Unsigned CASCExplorer and CascLib binaries have no verified origin in this local evidence. Hashes make artifacts identifiable but do not establish safety.

Other limitations:

- No Git history/commit exists in the two source/binary directories, so exact snapshots cannot be tied to a trusted commit solely from local metadata.
- CASCExplorer config includes online products and user settings. The app may contact remote CDN endpoints and persist settings, but startup network and write behavior cannot be established without source or execution; both remain unverified.
- CascLib supports online features and output/reporting code; it is not intrinsically sandboxed or guaranteed read-only.
- No installer was run; no files were modified or installed by this audit.

## 9. MW2CR Compatibility Assessment

| Need | Finding | Confidence |
|---|---|---|
| Recognize product `lazr` | Present in CASCExplorer config as an online storage label. | **High** for config label; **low** for parser compatibility. |
| Parse local MW2CR build `1.1.2.1279292` | No exact-build test/source evidence. | **NOT VERIFIED.** |
| Parse local CDN/install metadata | CascLib source has INSTALL manifest support and index readers in general. | **Medium** for generic feature; **low** for exact layout/build. |
| Identify which entries carry `arSA text`/`arSA speech` tags | CascLib install handler skips tag records and does not attach tag membership to file entries. | **High** from reviewed function; this source snapshot alone is insufficient for locale inventory. |
| List files without exporting their payloads | Feasible in principle through a root-list API, but exact caller behavior/build compatibility are unverified. | **Medium/low.** |
| Stay offline/read-only | Requires caller configuration and controlled output paths; library and GUI include online/output features and no global guard was established. | **Low without further review.** |

No CASC game data was accessed in this phase.

## 10. Recommended Toolchain

- **CASC metadata:** Keep CascLib as a source-review candidate only. First verify the source commit matching bundled DLL commit-like metadata, inspect its complete INSTALL/tag/index/encoding and read paths, and plan a minimal offline parser that reports manifest tag membership without opening ordinary asset payloads. Do not use the unsigned CASCExplorer executable on the game yet.
- **Arabic translation:** Use OmegaT as the likely main translation workspace once extracted text has been converted to a tested Unicode interchange format. Use Poedit only if `.po` output is selected. Neither tool extracts or rebuilds game assets.
- **Resource parsing/reintegration:** No component here is qualified to identify H2 resource types, export text/font structures, handle Arabic shaping/RTL, or rebuild MW2CR resources. Those are separate research and compatibility gates.

## 11. Rejected or Unverified Components

- **CASCExplorer executable — `UNVERIFIED BINARY`:** no local source/commit/signature; product config says `lazr`, but no proof for the exact build, tag-listing fidelity, or safe offline/read-only execution.
- **CascLib-master source — `SAFE CANDIDATE FOR FURTHER STATIC REVIEW`:** relevant manifest implementation exists, but exact commit/build and locale-tag semantics remain unresolved; parser hardening review is incomplete.
- **Release-win-x64 — `UNVERIFIED BINARY` / `NOT SUITABLE`:** product metadata identifies an unrelated `wow.tools.local` web app; no verified MW2CR capability or provenance.
- **OmegaT 6.1.1 — `SUITABLE FOR MAIN TRANSLATION WORKFLOW` (conditional):** installer identity/signature verified; Arabic bidi/format round-trip and installation state not verified.
- **Poedit 3.10 — `SUITABLE AS SECONDARY TOOL` (conditional):** installer identity/signature verified; usefulness depends on `.po` output and Arabic/placeholder validation.

## 12. Recommended Next Phase

**Phase 12B: CascLib provenance and tag-aware manifest parser static review.** Compare the exact source corresponding to the bundled CascLib DLL’s embedded commit suffix with this local source snapshot (only from an already trusted local source if available); audit `GetTagBitmapLength`, the INSTALL handler, storage-open path, index/encoding resolution, online switches, and write-capable calls. Specify a synthetic manifest test that confirms per-entry `arSA text`/`arSA speech` associations without accessing the game. Keep the game untouched and do not execute/compile anything until an independently reviewed offline test design and a separately authorized phase.

For translation tools, a later isolated synthetic-file test should cover Arabic Unicode, mixed Arabic/Latin strings, diacritics, placeholders, punctuation, and export round-trip after the extraction format is known.
