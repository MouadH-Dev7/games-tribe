# Arabic rendering feasibility — Campaign Remastered

## Assessment

**BLOCKED / NOT VERIFIED.** No archive resources could be inspected because no parser was verified for this exact `lazr` build. The installed executable and opaque archive inventory do not provide enough evidence to evaluate rendering behavior.

| Capability | Status | Evidence / limitation |
|---|---|---|
| Arabic glyph coverage | **NOT VERIFIED** | No game font resource was extracted or identified. Locale tags are not font evidence. |
| Arabic shaping (joining forms/ligatures) | **NOT VERIFIED** | No text renderer, font atlas, source code, or runtime output was inspected. |
| Right-to-left ordering | **NOT VERIFIED** | No Arabic UI string was observed in game. The launcher itself supports RTL, but that is unrelated to the game's renderer. |
| Mixed Arabic/Latin and numerals | **NOT VERIFIED** | No in-game mixed-direction sample could be tested. |
| UI layout, alignment, clipping, menus/subtitles | **NOT VERIFIED** | No localized UI resources or runtime screenshots exist. |
| Arabic locale selection | **INFERRED** | Local build metadata lists `arSA text?` and `arSA speech?`, and a bundled text `.reg` snippet names `arSA`. This does not establish functional Arabic rendering in the installed build. |

## What would be needed

First, establish exact-build read-only archive compatibility and inventory locale resources. Then inspect actual font/string formats and locale-specific groups using a validated decoder. Even that static evidence may not prove shaping and bidirectional behavior; a runtime test would require a verified compatible Arabic package and controlled, non-destructive testing. No game modification or runtime test was performed here.

**Conclusion:** Arabic rendering compatibility is not established. Install and Restore must remain disabled; no font, UI, or text replacement destination is known.
