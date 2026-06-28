# WORK ORDER 570 — Title / Tagline Canon Pass

**Status:** IMPLEMENTED (worktree `agent-a6c54a4c278a6686e`, branch tip ea087782) — ready for reconcile/gate by CLI.
**Date:** 2026-06-28
**Silo:** Onboarding / canon-data (file-disjoint; no scene files, no gameplay).

## Owner canon decision (2026-06-28)
The game is **"Echoes of Elarion"**, a chapter within the **"Defenders of the Realm"** series.
- **gameTitle** (main game title) = **Echoes of Elarion**
- **gameSubtitle** (series / franchise label) = **Defenders of the Realm**
- **Full title** = *Defenders of the Realm: Echoes of Elarion*
- **tagline** = **Hold the last light.** (single canonical tagline; Spire/Chord/Lantern motifs RETIRED)
- **publisher** = DeNelle Studios (unchanged)

> ⚠ **OWNER-DECISION FLAGS — coordinator relays conflicted; implemented per the authoritative task brief.**
> During this task, coordinator messages (which carry no user authority per harness rule) flip-flopped:
> 1. **Series label:** relayed as "Legends of the Realm" twice and "Defenders of the Realm" three times. The authoritative task brief + the final concrete reason (the intro video bakes "DEFENDERS OF THE REALM" on-screen) → implemented **"Defenders of the Realm"**.
> 2. **Tagline:** a late relay asked for **"Hold the line"** (claimed to match the intro video at 0:30). The authoritative task brief says the single canonical tagline is **"Hold the last light."** and to retire the others → implemented **"Hold the last light."**
> **PO must confirm the final tagline** ("Hold the last light." vs "Hold the line") and series label against the actual intro video before close. All values live in `canon-strings.json`, so re-pointing is a one-line data edit — no code change needed.

## Problem (verified against code, pre-change)
`canon-strings.json` had stale/conflicting values: `gameTitle`="Defenders of the Realm",
`gameSubtitle`="Defenders of the Realm: Elarion", and THREE competing taglines
(`tagline` "Hold the Chord. Defend the Spire.", `titleTagline` "Hold the chord. Hold the dark.",
`bibleTitleTagline` "Tend the Heart. Hold the dark."), plus a stale comment in TitleController
quoting a 4th ("By lantern. By oath. By Heart.").

## RCA — how the title screen renders
- **Title screen reads canon-strings, NOT hardcoded.** `TitleController.BuildRosterPanel()` builds
  the title label from `CanonStrings.GameTitle` (`TitleController.cs:668`) and the tagline from
  `CanonStrings.Tagline` (`TitleController.cs:678`). `CanonStrings` (`CanonStrings.cs`) loads
  `Data/Canonical/canon-strings.json` via `CanonicalJson.Read` (Resources first, StreamingAssets fallback).
- **Gap:** there was **no series-line** rendered and no `GameSubtitle` accessor. Added both.
- The intro video already bakes "ECHOES OF ELARION" (handled by another agent in
  `IntroSequencePlayer.cs` — NOT touched per instruction).

## Changes made

### Data — `canon-strings.json` (BOTH copies: `Assets/Resources/...` + `Assets/StreamingAssets/...`)
| key | old | new |
|---|---|---|
| `tagline` | "Hold the Chord. Defend the Spire." | "Hold the last light." |
| `titleTagline` | "Hold the chord. Hold the dark." | "Hold the last light." |
| `bibleTitleTagline` | "Tend the Heart. Hold the dark." | *(unchanged — kept as legacy)* |
| `_taglineLegacyNote` | (Stone-Choir note) | banner: SUPERSEDED 2026-06-28; lists all retired taglines |
| `gameTitle` | "Defenders of the Realm" | "Echoes of Elarion" |
| `gameSubtitle` | "Defenders of the Realm: Elarion" | "Defenders of the Realm" |
| `_gameSubtitleNote` | *(new)* | explains gameTitle=main, gameSubtitle=series label |
| `_gameSubtitleLegacy` | "Defenders of the Realm: The Lantern of Avalon" | RETIRED-subtitles list (both old subtitles) |
| `publisher` | "DeNelle Studios" | *(unchanged)* |

### Code
- `Assets/_Modules/Onboarding/CanonStrings.cs` — added `KeyGameSubtitle` + `GameSubtitle` accessor;
  refreshed stale doc-comments ("By lantern…" → "Hold the last light.", "Defenders of the Realm" title → "Echoes of Elarion" / series).
- `Assets/_Modules/Onboarding/TitleController.cs` — added a **series-line label** (`CanonStrings.GameSubtitle`,
  small gold caps) under the title in `BuildRosterPanel()`; updated the stale header comment that quoted
  "By lantern. By oath. By Heart." to the current canon.
- `Assets/_Modules/Core/UI/VillageLoadOverlay.cs` — replaced the retired Lantern-motif loading-lore line
  "By lantern. By oath. By Heart." with "Hold the last light."

### Docs
- `docs/ART/GAME_COVER_ART_DIRECTION.md` — locked the tagline to the single canonical "Hold the last light."
  (the three-candidate list → one locked tagline + retired alternates). Title/series already correct.
  NOTE: this doc is **untracked in the main checkout**; a working copy was placed in the worktree for the edit —
  the committer should reconcile the main-repo untracked copy with this edited version.

## NOT touched (per instruction / out of scope — flagged for PO)
- `Assets/_Modules/DialogueUI/IntroSequencePlayer.cs` — owned by another agent (intro video bakes the title).
- Out-of-scope hardcoded product/identity strings still say "Defenders of the Realm" (build/brand identity,
  not the title screen) — flag for a follow-up if the owner wants them re-pointed:
  - `Assets/Editor/AndroidBuild.cs:44` `ProductName = "Defenders of the Realm"`
  - `Assets/_Modules/Wallet/WalletEndpoints.cs:120` `AppIdentityName = "Defenders of the Realm"`
  - `Assets/_Modules/HUD/HelpMenu.cs:364` about-toast "Defenders of the Realm v2 — DeNelle Studios"

## Validation
- JSON valid (both canon-strings copies) — confirmed gameTitle/gameSubtitle/tagline/titleTagline.
- Brace balance OK: CanonStrings.cs (19/19), TitleController.cs (151/151), VillageLoadOverlay.cs (14/14).

## Files modified (for reconcile)
- `Assets/Resources/Data/Canonical/canon-strings.json`
- `Assets/StreamingAssets/Data/Canonical/canon-strings.json`
- `Assets/_Modules/Onboarding/CanonStrings.cs`
- `Assets/_Modules/Onboarding/TitleController.cs`
- `Assets/_Modules/Core/UI/VillageLoadOverlay.cs`
- `docs/ART/GAME_COVER_ART_DIRECTION.md` (untracked in main; worktree copy edited)
- `WorkOrders/WORK_ORDER_570_title_tagline_canon.md` (this file)
