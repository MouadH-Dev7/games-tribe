# Phase 16A — Original Locale Backup Evidence

## Scope and handling

Inspected only this requested file as plain text:

`C:\Users\mouad\Desktop\lazr_locale_backup.reg`

No Registry API was used. The file was not imported, executed, changed, or copied. Only the target section and the two requested value lines are reported here; unrelated Registry content is omitted.

## Target section findings

Target key:

`HKEY_CURRENT_USER\Software\Blizzard Entertainment\Battle.net\Launch Options\LAZR`

| Value name | Snapshot result | Literal data | Type represented by `.reg` syntax |
| --- | --- | --- | --- |
| `LOCALE` | Present | `enUS` | `REG_SZ` (`"name"="text"` form) |
| `LOCALE_AUDIO` | Present | `enUS` | `REG_SZ` (`"name"="text"` form) |

Both values were present in this exported snapshot. This establishes only what the backup file records at its export time. It does not establish the current live Registry state, the game's currently selected language, or whether Battle.net accepts these values for the installed build.

## Safety confirmation

- Live Registry was not read or modified.
- The `.reg` file was not imported or executed.
- No game files were accessed or modified.
- No launcher or game was started.
