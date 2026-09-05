# RESULT - WORK ORDER 1333 - Em dashes in DISPLAYED titles render as tofu boxes

**Filed:** 2026-09-04 (board agent, from the lead's measured facts + a same-session read of the tree)
**WO status:** FIXED - in build 2026.09.05.355872, awaiting owner felt-test. PO closes.

## What shipped (measured this session, at source)

| Site named by the WO | State on the 2026.09.05.355872 tree |
|---|---|
| `Assets/Resources/Data/Canonical/lore-fragments.json` | **0** U+2014 (UTF-8 scan) |
| `Assets/StreamingAssets/Data/Canonical/lore-fragments.json` | **0** U+2014 - twin matches |
| `Assets/Resources/Data/Canonical/dungeons/healers-cottage.json` (the WO's path omitted the `dungeons/` folder) | **0** U+2014 |
| `Assets/StreamingAssets/Data/Canonical/dungeons/healers-cottage.json` | **0** U+2014 - twin matches |
| `Assets/Editor/DungeonSceneBuilder.cs:995-1028` (journal entries 1-3 + the entry-4 title) | clean - plain ASCII hyphens |

Also in this build: twelve canonical JSONs were de-BOMed tonight (lead's fact list), which is the
same "bytes the font never sees" class this ticket is about.

## RESIDUAL - measured, not fixed, and the RESULT says so loudly

A UTF-8 scan of `Assets/Editor/DungeonSceneBuilder.cs` for U+2014 on NON-comment lines finds
**27** hits. Most are log/`Debug.LogError` strings (never rendered by TMP). **Three are DISPLAYED
strings and still carry the em dash:**

(the em dash is written `<U+2014>` below so this file stays ASCII)
- `DungeonSceneBuilder.cs:183` - `"The path opens easy, Keeper. But mind the rocks <U+2014> they remember you. "`
- `DungeonSceneBuilder.cs:1031` - journal-4 body: `"in the cellar for whoever is next <U+2014> a seed. Plant it at the Folk's "` (the line immediately AFTER the WO's `995-1028` range)
- `DungeonSceneBuilder.cs:1382` - `"Special: Tincture <U+2014> shrinks the Keeper's lantern reach 50% for 6s"`

So the two JSON sites are done and the third site is done for the range the WO named, but the
same class survives three lines away. The acceptance line "every non-ASCII character in a
player-facing string is gone" is **not** met for `DungeonSceneBuilder.cs`. The owner will not see
these on the Seeker unless she opens the Healer's Cottage dungeon and reads those specific stones
/ items - they do not touch the raid loop this build was cut for.

## Which suite proves it

- `[dungeon-lore]` (`DungeonLoreReadableRegression`, registered `DataRegression.cs:529`) reads both
  `lore-fragments.json` twins and pins the Alduin/Aldwin copy source; it was GREEN on pass 2.
- **No suite in `Assets/Editor/Regression/` references WO-1333** (`grep 1333` -> 0 files), so the
  WO's own acceptance item "the tofu oracle is WIDENED to cover these files, proven RED first" is
  **not evidenced** on this tree. The `[hud-ui-sme]` tofu oracle scans UI files (251 scanned, 0
  tofu on pass 2), not the dungeon builder's string table.

## Build + install evidence (lead's measurements, 2026-09-04)

- Build `2026.09.05.355872` (versionCode 355872) installed on the owner's Seeker 2026-09-04 22:22:13
  via `install-apk-to-seeker.ps1` (`Success`; `adb shell dumpsys package` versionName=2026.09.05.355872).
- Chain markers: `SCHEMA_PARITY_OK`, `APK_OK` (461 MB), `R2_PUSH_OK` (catalog_2026.09.05.355872.bin/.hash
  uploaded), `R2_PARITY_OK targets=Android,StandaloneWindows64,WebGL objects=271`, `APK_DONE`.
- Regression on the same tree: pass 2 = 375/377 (the two reds were in TEST code and are fixed);
  **final pass pending; see `Builds/regression.log` marker.**

## What the owner should felt-test

1. Open the Journey / lore surface that shows lore fragments and read two fragment titles - no
   hollow rectangle where a dash should be.
2. Enter the Healer's Cottage dungeon and read journal entries 1, 2 and 3 on the tables - the
   titles read "Alduin's journal - entry N" with a plain dash.
3. Read journal entry 4 (workshop) - **expect a tofu box** in the body ("for whoever is next [] a
   seed"); that is the residual above, not a regression. Mark the ticket Needs Work with that note
   if she wants it in this pass.
4. Tap any dungeon item card that shows a "Special:" line (Tincture) - same check.

## Residual closed 2026-09-04 22:40 (CLI)
The three display-string em dashes the board pass found in `Assets/Editor/DungeonSceneBuilder.cs`
(:183 lore line, :1031 journal-4 body, :1382 "Special: Tincture") are replaced with ASCII " - ".
They are NOT in build 2026.09.05.355872 on the Seeker - expect the tofu box at journal 4 on that
build; the next APK carries the fix. Coverage note: no regression sweeps `Assets/Editor/*Builder.cs`
for non-ASCII in baked strings (the tofu checks are per-file lints on `_Modules` sources), which is
why 377/377 green did not see these. Candidate oracle: a repo-wide non-ASCII scan of every string
literal in Editor scene builders (mint from the banner).
