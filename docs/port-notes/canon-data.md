# Canon Data Extraction — Week 1

**Date:** 2026-05-18
**Purpose:** Extract canonical game text into shared JSON so the React v1 engine and the Unity v2 port draw from one source of truth.
**Status:** Complete, with caveats (see "Could not locate" below).

## Files produced

- `docs/port-notes/data/canon-strings.json` — canonical proper nouns (names, titles, brand terms).
- `docs/port-notes/data/en.json` — localizable user-facing strings for the Unity Localization package string table.

Both files are valid JSON and include `_comment` / `_sources` metadata keys (prefixed `_`) describing provenance. Strip or ignore `_`-prefixed keys when ingesting into a string table.

## Sources read

| File | Result |
| ---- | ------ |
| `defenders-unity/docs/narrative-bible.md` | Read in full (388 lines). Canonical lore authority. §7 is the in-game text library. |
| `defenders-of-the-realm/src/content/story.ts` | Read in full (644 lines). Hero data, opening cinematic, narrative beats, runtime cues. |
| `defenders-of-the-realm/src/content/tooltips.ts` | Read in full (234 lines). HUD/panel tooltip copy, ability tooltips, gate/movement hints. |
| `defenders-of-the-realm/src/lib/themes.ts` | Read in full (395 lines). UI color/theme presets only — **no game text**, not used. |
| `defenders-of-the-realm/src/lib/constants.ts` | Read in full (5 lines). Contains only Solana/crypto wallet addresses — **no game content**. See below. |

The React v1 project was treated strictly read-only; nothing was written into it.

## IMPORTANT — naming-layer discrepancy

The two canonical sources use **two parallel naming layers** and do not fully agree. Both were captured rather than picking one:

- **narrative-bible.md** (newer canon-lore authority): world-tree = **Elarion** / "the Heart"; player = **the Keeper**; enemies = **the Hollow Ones**; tagline = "Tend the Heart. Hold the dark."
- **story.ts** (v1 implementation): village = **Avalon**; light source = **the Lantern of Avalon**; player = **Guardian** / **Realm Guardian**, hero name **Blaise** (mage); enemies = **the Hollowed**; tagline = "Tend the Lantern. Hold the dark."

These are not contradictions to resolve blindly — the bible §2 says Elarion is the World Tree and §6 lists places; story.ts WORLD says the village is Avalon. So **Avalon (village) + Elarion (the Heart-Tree at its center)** coexist. But "Keeper vs Guardian" and "Hollow Ones vs Hollowed" and the two taglines are genuine v1-vs-bible drift. Both spellings are kept in the JSON with distinct keys (e.g. `hollowOnes` and `hollowed`; `title.tagline.bible` and `title.tagline.story`; `heartVoice.*` from the bible and `heartVoice.alt.*` from story.ts). **A design decision is needed before ship on which layer is authoritative for the Unity port.** Recommend the narrative bible, per the task brief calling it "canonical lore."

## canon-strings.json — key list

Town/world: `avalon`, `avalonEpithet`, `elarion`, `elarionAlias`, `elarionAlsoCalled`, `theHeart`, `theHeartTree`, `theHeartTagline`, `worldTree`
Hero/player: `blaise`, `blaiseEpithet`, `blaiseMentor`, `theKeeper`, `keeper`, `theKeeperKeyword`, `guardianTitle`, `guardianOfTheLantern`
Antagonist: `alduin`, `alduinTitle`
Brand: `heartWing`, `heartWingDescription`, `lantern`, `lanternShort`, `tagline`, `titleTagline`, `bibleTitleTagline`, `publisher`, `gameTitle`, `gameSubtitle`
Factions/forces: `hollowOnes`, `hollowed`, `wardens`, `theFolk`, `firstLight`, `theKeepersSong`, `theWound`, `theWithering`, `firstLightEvent`
NPCs: `wardenAelwyn`, `sirBram`, `selaThornquiver`, `garran`, `garranMentor`, `wrenThornquiver`, `wrenMentor`
Requested-but-unverified names: `bryn`, `mara`, `tovin`, `eira`, `aelf`, `mira`
Pets: `aetherSprite`, `aetherSpriteEpithet`, `flamePup`, `flamePupEpithet`, `iceWolf`, `iceWolfEpithet`
Buildings: `crystalMines`, `crystalMine`, `petHouse`, `arcaneTower`, `workshop`, `farm`
Zones: `zoneHearth`, `zoneWintermere`, `zoneEmberfall`, `zoneSkyloom`, `zoneGreenrows`

## en.json — key groups

- `title.tagline.*` — title-screen taglines (bible + story variants)
- `intro.coldOpen.line1..3` — the 3-line first-launch cold-open (narrative bible §7.1)
- `tutorial.first*` — FTUE pop-up lines (bible §7.2 / story.ts TUTORIAL_LINES)
- `tutorial.steps.1..7` — the 7-step onboarding sequence (story.ts TUTORIAL_STEPS)
- `gate.*` — force-field gate one-shot tutorial toasts (tooltips.ts)
- `wave.warning.{ice,fire,aether,mixed,boss}.{1,2}` — pre-wave callouts (bible §7.3)
- `heartVoice.*` — Heart's-voice state-change lines, serene/vigilant/warning/danger/critical/boss/victorious (bible §7.4); `heartVoice.alt.*` are the story.ts LANTERN_VOICE variants
- `heartDamage.threshold{75,50,25,10,0}` — HP-threshold flavor (bible §7.5); `heartDamage.alt.*` from story.ts
- `victory.wave.*`, `victory.boss.*`, `victory.personalBest`, `victory.waveCleared.*` — victory lines (bible §7.6 + story.ts WAVE_CLEARED_LINES)
- `defeat.1..3`, `defeat.alt.*` — defeat lines (bible §7.7 + story.ts)
- `petCaption.*` — pet vocalization captions for accessibility (bible §7.9)
- `petAmbient.*`, `keeperAmbient.1..8` — ambient flavor (story.ts PET_AMBIENT + bible §7.8)
- `milestone.*` — achievement lines (bible §7.10)
- `returningPlayer` — returning-player welcome line (story.ts)
- `elementBlurb.*`, `resourceBlurb.*`, `buildingDesc.*` — badge/resource/building flavor (bible §7.11–7.13)
- `tooltip.*` — all static HUD tooltips, ability tooltips (mage/knight/ranger), wave UI (tooltips.ts)
- `movementHint.*`, `realmMap.revealHint` — touch hints (emoji prefixes stripped for string-table cleanliness)
- `shopkeeperPanic.1..6` — shopkeeper barks (story.ts)

## Text I could NOT locate (fill later)

1. **Tagline "By lantern. By oath. By Heart."** — The task brief specifies this as `tagline`. It does **not** appear in any source read. The narrative bible's title tagline is "Tend the Heart. Hold the dark." and story.ts uses "Tend the Lantern. Hold the dark." The brief's tagline was used verbatim for `canon-strings.json.tagline` as instructed, but it is **not sourced from canon files** — confirm with the writer/brand owner.
2. **"DeNelle Studios" (publisher)** — Not found in any source file. Used verbatim from the task brief for `publisher`. No publisher/studio name appears anywhere in the React project files read. Confirm.
3. **Names Bryn, Mara, Tovin, Eira, Aelf, Mira** — Requested by the brief but **absent from narrative-bible.md and story.ts**. The bible's canon registry (§9) lists no such names; story.ts NPCs are Warden Aelwyn, Sir Bram, Sela Thornquiver (plus heroes Blaise, Garran, Wren Thornquiver). Placeholder entries were added to `canon-strings.json` (keys `bryn`/`mara`/`tovin`/`eira`/`aelf`/`mira`) mirroring the requested spelling, and flagged via `_namesNotInSources`. **These need canon definition** — either the writer adds them to the bible registry, or they are dropped.
4. **"First Light" as a standalone event/term** — The bible uses "the First Light" only as the being(s) who planted Elarion (§1, §9). No "First Light" event (e.g. a founding moment) was found. `canon-strings.json` has both `firstLight` ("the First Light", verified) and `firstLightEvent` ("First Light", unverified, flagged via `_firstLightNote`). Confirm whether a separate event term is intended.
5. **"The Keeper" — name vs title** — The bible uses "the Keeper" as the player role; story.ts names the mage hero **Blaise** and uses **Guardian**. No personal name for the bible's Keeper exists (bible §10 Q2 leaves the player un-named with default "Keeper"). Captured as a title, not a name.
6. **`constants.ts` has no game data** — `src/lib/constants.ts` contains only Solana blockchain wallet addresses (ADMIN_ADDRESS, PROJECT_VAULT_ADDRESS, SOL, USDC). It appears to be a stray/placeholder or web3-integration file unrelated to game content. No canonical strings extracted from it.

## Notes for the Unity port

- `heartDamage.threshold0` is "…" (an ellipsis) — the bible §7.5 specifies silence/no text at 0% HP before the defeat screen. Kept as the literal ellipsis character; the Unity HUD may prefer to treat this key as "show nothing."
- Movement-hint emoji (joystick/finger/map icons) present in tooltips.ts were stripped from `en.json` values for string-table portability. Re-add via sprite tags in Unity if desired.
- Multi-variant lines use numeric suffixes (`.1`, `.2`, …). The Unity wave/HUD systems should pick one at random per trigger, matching the React `@/lib/narrative` helper behavior.
