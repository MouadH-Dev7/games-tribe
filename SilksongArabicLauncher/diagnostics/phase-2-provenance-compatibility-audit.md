# Phase 2 — Silksong Translation Package Provenance and Compatibility Audit

## Executive result

**Recommendation: Blocked pending source/provenance verification.** The local game candidate is structurally consistent with a Unity/Mono Silksong installation, and the supplied package contains Arabic localization files plus a Doorstop/BepInEx/Patchwork/HeshamRTL runtime setup. However, neither the exact game content build nor the provenance of the unsigned runtime DLLs is established. Static assembly references also show version differences between several plugins and the loader/dependencies supplied beside them. The package should not be installed automatically yet.

This phase was read-only. No game, launcher, or supplied DLL was run or loaded; no archive was extracted; no Registry was accessed; and no game, package, or MW2CR launcher file was changed.

## 1. Exact local game build

Candidate: `C:\Games\Hollow Knight - Silksong`

| Evidence | Local observation | What it establishes |
|---|---|---|
| `Hollow Knight Silksong.exe` | 672,256 bytes; SHA-256 `72FAA5AC3F3D8CD781B962EE1D842F435300A270CE8AC76D9F6DC9283002E074`; file version `6000.0.50.3512832`; product version `6000.0.50f1-uum-100966-branch1 (359a00488d30)` | Filename and game-data pairing are strong Silksong evidence. Version fields identify Unity, not the Silksong release. Product/company metadata is blank. |
| `UnityPlayer.dll` | 33,677,168 bytes; same Unity file version `6000.0.50.3512832`; SHA-256 `A4038CD4B94A819CE646288EB8FC0AA2FEC362C86717439F8AB98E08538274D2` | Executable and Unity player agree on engine build. |
| `Hollow Knight Silksong_Data\globalgamemanagers` | 303,060 bytes; SHA-256 `3CE206B562680A41DE8271D2546C2462414CC7A3FAD3EED7139BC32328B45BBD`; embedded Unity string `6000.0.50f1-uum-100966-branch1` | Confirms the same Unity engine branch, not the game's patch/build number. |
| `Hollow Knight Silksong_Data\boot.config` | Contains `build-guid=f7955483e67143d8806ee130b1fce917`; SHA-256 `77124F10AE28BAE3772E1BB2D2B90ACAFD0F250E36CA083A50C10E1CD288D46F` | A local build marker exists, but no mapping to a public game release was found. |
| `Hollow Knight Silksong_Data\Managed\Assembly-CSharp.dll` | 6,944,256 bytes; managed assembly metadata version `0.0.0.0`; SHA-256 `C1A97ACD7F38A84450CFB43C09AB128325B0CAADC266EC407E52A56A9439B40C` | Confirms a managed game assembly is present; it does not encode an identifiable game release. |

The game folder also contains `MonoBleedingEdge`, Team Cherry managed assemblies, and Steamworks components. This is consistent with a Unity/Mono PC build. Steamworks files do not prove this copy was installed by Steam or that it corresponds to App ID `1030300`.

No `appmanifest_1030300.acf` or Steam library configuration was found in the common Steam locations checked. The expected Steam folders checked were absent. Registry-based launcher discovery was not performed. Consequently, association with Steam App ID `1030300` and the exact Silksong content/build version remain **UNVERIFIED**. The installed files appear internally consistent at the Unity-engine level, but that does not establish game-release consistency.

## 2. Supplied package identity, source, and integrity

Source: `C:\Users\mouad\Desktop\New folder (3)`

The included Arabic readme names the package “Hollow Knight: Silksong — تعريب هشام”. The package has 65 files totaling 26,266,619 bytes. Its initial SHA-256 inventory is [translation-package-sha256.csv](C:/Users/mouad/Desktop/Call%20of%20Duty%20-%20Modern%20Warfare%202%20CR%20trib/SilksongArabicLauncher/diagnostics/translation-package-sha256.csv). Rechecking the package against that inventory found 65/65 files present with no hash changes.

No project source, `.csproj`, PDB symbols, package manifest, publisher signature, or verifiable release metadata for the supplied plugins was found in the inspected package or the relevant local downloaded-source directory. The file metadata below is locally observed only; version strings and commit-looking product-version suffixes do not authenticate upstream provenance.

### Static DLL audit

All 21 DLL files were read as PE/CLR metadata, hashed, and checked for Authenticode status. No supplied DLL was loaded. The full per-file record—including relative path, size, SHA-256, PE machine, CLR presence/flags, assembly identity/version, referenced assembly identities, file/product versions, and signature status—is embedded in each file entry of [phase-2-installation-manifest-proposal.json](C:/Users/mouad/Desktop/Call%20of%20Duty%20-%20Modern%20Warfare%202%20CR%20trib/SilksongArabicLauncher/diagnostics/phase-2-installation-manifest-proposal.json).

- The 20 managed DLLs are PE machine `I386`, CLR `ILOnly`, and do not set `Requires32Bit`; this is AnyCPU-style IL metadata, not proof that a particular Mono/BepInEx runtime can load them.
- `winhttp.dll` is native AMD64, 25,088 bytes, version `3.4.0.0`, SHA-256 `CF9DD372CA0DDBE01153502C49F8F756197BB260001792FE766F6C0242DC7FC0`. Its static import table names `KERNEL32.dll`, `USER32.dll`, and `SHELL32.dll`.
- Every supplied DLL returned Authenticode status `NotSigned`. This does not prove maliciousness; it means Windows publisher-signature verification cannot establish who produced these binaries.
- `Patchwork.dll` metadata identifies assembly version `2.5.0.0`, with product-version suffix `a6a806ede1354aafdd5f4c19319739085d2d89bc`. It references `TeamCherry.Localization`, `TeamCherry.Cinematics`, `TeamCherry.TK2D`, `Assembly-CSharp`, Unity modules, BepInEx, Harmony, and MonoMod. The corresponding Team Cherry, game, Unity, and Mathematics assemblies referenced by name are present in the candidate game’s `Managed` folder. That is evidence of a game-specific dependency set, not proof of exact-build compatibility.
- `SilksongPrepatcher.dll` metadata reports version `1.3.0.0` and product suffix `6e1b719decc82f6423cc22ccd6566267846033fc`. `HeshamRTL.Runtime.dll` reports `1.0.0.0`. Those suffixes and names are unverified claims until matched to trustworthy source/releases.

### Dependency-version risks

Static assembly references expose several potential conflicts that need trusted source/binding documentation or an isolated authorized compatibility test:

- Package `BepInEx.dll` and `BepInEx.Preloader.dll` are `5.4.22.0`. `HeshamRTL.Runtime.dll` requests BepInEx `5.4.23.5`.
- `Patchwork.dll` requests BepInEx `5.4.20.0`, Harmony `2.7.0.0`, and MonoMod.Utils `21.12.13.1`; package core supplies BepInEx `5.4.22.0`, Harmony `2.9.0.0`, and MonoMod.Utils `22.1.29.1`.
- `SilksongPrepatcher.dll` and the MonoDetour integration reference Mono.Cecil `0.11.4.0` and MonoMod.Utils `21.12.13.1`; package core contains Mono.Cecil `0.10.4.0` and MonoMod.Utils `22.1.29.1`.

These may or may not be tolerated by the relevant loader/binding policy. Static metadata alone cannot resolve that. The supplied package has no authenticated version manifest or documented binding redirects that settle these differences.

## 3. Doorstop and BepInEx loading chain

Observed package configuration:

- `.doorstop_version`: `4.4.1`.
- Root `doorstop_config.ini`: `[UnityDoorstop]`, `enabled=true`, `targetAssembly=BepInEx\core\BepInEx.Preloader.dll`, `redirectOutputLog=false`, `ignoreDisableSwitch=false`, and an empty `dllSearchPathOverride`.
- The root `winhttp.dll` is AMD64 and matches the game's 64-bit process architecture.
- The package places BepInEx `5.4.22.0` files under `BepInEx\core`, MonoDetour and Silksong patchers under `BepInEx\patchers`, and Patchwork/HeshamRTL plugins under `BepInEx\plugins`.
- Only one BepInEx configuration file is present: `BepInEx\config\com.hesham.rtl.runtime.cfg`. No separate BepInEx preloader config or `BepInEx.cfg` was found in the package.

The configured startup chain is therefore: game process loads the root Doorstop proxy; enabled Doorstop configuration points to the relative BepInEx preloader path; the preloader is intended to initialize BepInEx and the placed patchers/plugins. This is an interpretation of configuration and directory layout, not runtime verification. The game contains Unity's Mono runtime; the package supplies a loader stack, but exact compatible runtime/loader versions remain unresolved. In the candidate game folder, `BepInEx`, `winhttp.dll`, and `doorstop_config.ini` were absent, so no pre-existing BepInEx files were found to conflict. The English font bundle is the one existing destination collision.

## 4. Patchwork YAML and Arabic font/RTL audit

- Python 3.11.9 and PyYAML were already available; no package was installed. All 36 YAML files parsed using a safe YAML loader. All decode as strict UTF-8; there are no replacement characters or detected `Ã`/`Ø`/`Ù` mojibake markers.
- 5,946 YAML key instances were found. No duplicate keys occur inside an individual YAML mapping. Seven keys occur in more than one file and have different values: `BS_SIT_QUEST2_OFFER`, `BS_SIT_QUEST2_REPEAT`, `BS_SIT_QUEST2_REOFFER`, `BS_SIT_QUEST2_DECLINE`, and `BS_SIT_QUEST2_ACCEPT` occur in `Crawl` and blank-valued `Deprecated`; `CARETAKER_REPEAT` occurs in `Enclave` and blank-valued `Deprecated`; `SAVE_WARNING` occurs in `General` and `MainMenu` with different text. This may be intentional sheet scoping, but the exact loader merge/override behavior is not proven. The six blank `Deprecated` values could matter if sheets are merged globally.
- 35 YAML files contain Arabic characters; `Deprecated\EN.yml` is blank-valued and has none. This is not a translation-completeness measure.
- Static strings in Patchwork include `LoadTextSheet`, `TextLoadPath`, `CurrentLanguage`, `LanguageCode`, `SupportedLanguages`, and “Dumping text for language code”. These support that the plugin contains text-sheet/language handling. Its runtime discovery and merge rules are **UNKNOWN** because source is absent and the DLL was not decompiled or executed. The package README and directory layout indicate `EN` is the intended locale override.
- Static strings in HeshamRTL describe Arabic TextMeshPro fallback-font use, legacy Unity text support, text-direction/alignment handling, and an `Arabic.ttf` path in the plugin folder or a full path. The package places `Arabic.ttf` beside `HeshamRTL.Runtime.dll`, which is consistent with those strings. The only RTL config sets `Verbose = false`; it does not explicitly set a font path.
- The font is 180,460 bytes, SHA-256 `DC09ECFDA83A30F5CE055A765FEFB5CE2802311AB202AB2B0372664346FECE97`; its `OFL.txt` states SIL Open Font License 1.1. No runtime font loading was tested. Glyph coverage, shaping, right-to-left rendering, mixed Arabic/Latin ordering, and UI layout remain unverified.

## 5. Unity bundle header comparison

No Unity bundle parser was available in the inspected local environment (`UnityPy` was absent); no tool or dependency was installed. Only the first 128 bytes of each relevant bundle were read to parse bounded UnityFS header fields. No block table was decompressed, and no asset was extracted.

| Bundle | UnityFS format | Header Unity version / revision | Declared size vs file size | Compressed / uncompressed block-info bytes | Flags |
|---|---:|---|---:|---:|---:|
| Package `...\fonts_assets_english.bundle` | 8 | `5.x.x` / `0.0.0` | 23,349,233 / 23,349,233 | 153 / 153 | `0x00000040` |
| Installed `...\fonts_assets_english.bundle` | 8 | `5.x.x` / `0.0.0` | 8,517,424 / 8,517,424 | 86 / 153 | `0x00000243` |
| Package `patchwork.assetbundle` | 8 | `5.x.x` / `6000.0.50f1` | 7,094 / 7,094 | 65 / 91 | `0x00000243` |

The font bundle pair has matching UnityFS format and header Unity version/revision strings, and each declared total size matches its actual size. The block-info sizes and flags differ. This is **partial header compatibility evidence only**; bundle internals, asset names/types, font assets, compression correctness, and replacement compatibility are **UNVERIFIED**.

**Correction to Phase 1 wording:** the `6000.0.50f1` revision is in the package's `patchwork.assetbundle`; the two `fonts_assets_english.bundle` headers identify `5.x.x` / `0.0.0`. The earlier report conflated these bundle version strings.

## 6. Proposed installation manifest

The machine-readable proposal is [phase-2-installation-manifest-proposal.json](C:/Users/mouad/Desktop/Call%20of%20Duty%20-%20Modern%20Warfare%202%20CR%20trib/SilksongArabicLauncher/diagnostics/phase-2-installation-manifest-proposal.json). It includes all 65 source files; the readme is explicitly excluded from installation per its own instructions, leaving 64 proposed payload paths. For each file it records source/destination relative path, source size and SHA-256, category, code/data classification, existing destination state and hash, proposed add/replace action, per-file concerns, and PE/CLR/signature/reference metadata for DLLs.

The only destination collision found is `Hollow Knight Silksong_Data\StreamingAssets\aa\StandaloneWindows64\fonts_assets_english.bundle`: the package file is 23,349,233 bytes (`52F2D2BBF6D5EA90976718021EAE5985F846C5010B145BB286E062F7574BAE2E`); the installed file is 8,517,424 bytes (`125993E4121D83F5D7F72F3193E820375A1FE86BF63B6061BC48B479675BBC9A`). It is explicitly classified as a replacement. The other 63 intended payload paths did not exist at the time of inspection. The candidate game had no existing BepInEx tree or Doorstop root config/proxy, so no unrelated mod files were observed at those planned paths. Any installer must still recheck these conditions immediately before use and refuse to overwrite unexpected files without verified backup and explicit user authorization.

The JSON was parsed with Python's standard JSON parser after generation. It contains 65 entries, 64 included payload entries, and one excluded instructions entry.

## 7. Compatibility verdicts

| Area | Verdict | Basis |
|---|---|---|
| Game identity confidence | PARTIALLY VERIFIED | Executable, matching data directory, Unity/Mono runtime, and Team Cherry assemblies are present; store identity is not established. |
| Exact game build confidence | UNVERIFIED | Unity engine version and local build GUID exist, but no authoritative mapping to a Silksong release/build was found. |
| Translation package integrity | PARTIALLY VERIFIED | All 65 local hashes are stable against the Phase 1 inventory; YAML parses. No trusted publisher hash/signature exists. |
| DLL provenance | UNVERIFIED | DLLs are unsigned; no local source/release records authenticate them. |
| Mod-loader compatibility | UNVERIFIED | Doorstop/BepInEx chain is configured, but multiple referenced versions differ from supplied versions. |
| Patchwork compatibility | PARTIALLY VERIFIED | Patchwork references Team Cherry/game assemblies present locally and contains text/language-related metadata strings; exact build and YAML loader behavior are unknown. |
| Arabic font compatibility | PARTIALLY VERIFIED | Arabic TTF is present beside RTL plugin; strings describe fallback use, but actual font loading/rendering was not tested. |
| Unity bundle compatibility | PARTIALLY VERIFIED | UnityFS v8 and Unity version/revision header strings match between package and installed font bundles; flags and block-info sizes differ and internals were not examined. |
| Overall installation readiness | BLOCKED | Exact build, DLL provenance, dependency binding, runtime behavior, and bundle contents are not established. |

## 8. Actions and recommended next step

No game or package file was modified, copied, installed, renamed, deleted, or extracted. No supplied DLL or game was launched or loaded. No Registry access, network access, dependency installation, or MW2CR launcher modification occurred. The only project outputs are this report and the proposed JSON manifest; the Phase 1 SHA-256 inventory remains unchanged.

**Recommendation: Blocked pending source/provenance verification.** Before designing an installer, establish the authoritative local game build/store identity and obtain trustworthy source/release evidence for the unsigned loader/plugins. Resolve the explicit BepInEx/MonoMod/Mono.Cecil version differences, verify Patchwork's locale-sheet discovery semantics, and use an already available safe Unity parser to inspect both font bundles. Only then can a reversible install plan be considered. No installer was implemented and Phase 3 has not started.
