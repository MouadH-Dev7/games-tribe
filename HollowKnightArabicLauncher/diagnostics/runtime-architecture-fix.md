# Runtime architecture correction

- Target executable: `hollow_knight.exe`, PE machine x86 (32-bit).
- Previously bundled `winhttp.dll`: PE machine x64 (64-bit), so Doorstop could not load into the target process.
- Evidence after the install: no game-local `BepInEx/LogOutput.log` or generated `BepInEx/config/BepInEx.cfg`; the game's Unity log contained no Doorstop/BepInEx/SnakeArabic initialization messages.
- Game log also recorded the selected language as `FR`. The supplied translation package's own BepInEx log says to select English for the translation.
- Replaced only the launcher's bundled `TranslationPackage/winhttp.dll` with the root `winhttp.dll` from official BepInEx `BepInEx_win_x86_5.4.23.5.zip`.
- Official archive URL: `https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x86_5.4.23.5.zip`
- Official archive SHA-256: `37651C79E40D6F909572A4F461AC25350BB3EF8FE7FBD29F1AA8791A33B84C82`
- Corrected `winhttp.dll`: 22,016 bytes; PE machine x86; SHA-256 `CC643B54484F694A8E0E6641CAC79D74141009AFC9E24D826F6FD7FD48FD182A`.
- The game directory was not modified during this correction. The earlier installed transaction remains registered; use the launcher to uninstall the old package, install the corrected package, and choose English in the game settings.
- BepInEx startup and translation display have not yet been verified after this correction. Unity/plugin compatibility remains unconfirmed.
