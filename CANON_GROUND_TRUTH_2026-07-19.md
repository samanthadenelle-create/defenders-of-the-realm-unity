# CANON GROUND TRUTH — 2026-07-19

> ## ⚠ EVENING ADDENDUM (2026-07-19 evening — supersedes the stale morning lines below)
> The body below is the 07-19 MORNING snapshot. Current reality after the evening FELT-TEST FIX WAVE:
> **WO next-free = 754** (739-753 consumed — the morning "750" line is stale). HEAD has advanced past
> `98ff1135` (the felt-test fixes + docs). **Still `REGRESSION_OK` (0 reds).** Landed this evening: pet-screen
> sort-order, HUD de-overlap, **WO-751 Y-height normalization** (default 4m/tower 7m/siege 3m + audit tool),
> Echo modal single-arbiter (PanelManager), upgrade-panel visuals (event-driven + text-fit + hotkeys removed),
> flag-screenshot save-on-release, **upgrade no-op blocker FIXED** (wrong-wallet: city path now spends the
> GameState/ResourceLedger wallet), white-ballista/magenta-weapon materials, **WO-753 Destructible**
> (no-rebuild + full-cost + VFX cleanup). **New WOs:** 750 (Right ActionBar naming + Warden's Grace, SPEC),
> 751 (DONE), 752 (Echo founding-card + interjection, SPEC+creative sign-off), 753 (Destructible),
> 754 (rewarded ads SPEC), 755 (pack catalog SPEC), 756 (sales banner + campaign tool SPEC). **New rulings:**
> Right ActionBar = Attack + Q/W/E/R named skills (no mobile key-letters); Y-height normalization; Echo =
> essence of a guarded person (Aldwin/Elowen/Corvin/Bran/Doran/Maren); destroyed = no-rebuild + Destructible;
> headless UI-screenshot pass runs before builds. For the fullest current state read `KEY_FACTS.md` +
> `docs/HANDOVER.md` newest block (both current).

> **Purpose:** the single anchor of *current reality*, verified from the working tree, HEAD, the gates,
> and owner rulings given live. **Supersedes `CANON_GROUND_TRUTH_2026-07-18.md`** (bannered). If a doc
> contradicts a line here, the doc is STALE. Read this -> `KEY_FACTS.md` -> `SESSION_CANON_LOADER.md`
> -> `SAMANTHA.md` -> `docs/HANDOVER.md` -> `docs/MASTER_CATALOG.md`.

## Star North (unchanged)
- **Pi Hackathon WON** (2026-07-17). The "July-31 deadline / build mode IS the demo" framing is RETIRED;
  roadmap is OPEN. Product unchanged: "Echoes of Elarion" in "Defenders of the Realm", mobile web (Pi
  Browser), V1 = one controllable Knight + player-built city; HP-B2B architecture; ten-year-old feel bar.

## Repo / git
- **Branch `wip/village2-and-f8-tickets`.** HEAD = **`98ff1135`**, **local AHEAD of origin by 7**, **PUSH
  HELD** (owner authorizes push + prod promotion). Prod UNTOUCHED (still the 07-16 `q2v5vj86g`).
- **Save schema v34** (07-19: persist Tribes/Wards/Arena + pet active-slot). Every 21->34 bump has a
  SaveMigrator step; v34 is additive/default-on-read.
- **WO next-free = 750** (banner `CLI_LANES_WO_NUMBERS.md`; 739-749 consumed). 748/749 now DONE.

## Gates (as of 2026-07-19, headless-verified this session)
- `COMPILE_GATE_OK` (compile + brace + NUL). **`DataRegression.RunAll` = `REGRESSION_OK` — ZERO reds**
  (first time). The 5 long-standing FAIL-BY-DESIGN reds were all fixed today (see below). The old
  "8/6/5 known-red baseline" lines in other docs are STALE -> the baseline is now **0**.
- Ratchets `[ui-mvvm]` (report-only, ~16 debt), `[ui-obsidian]` (4 new hand-rolled UI warns, report-only),
  `[room-forge]` at 0. `GEAR_CURATION_OK` green (WO-747 curated projection).

## What landed this session (2026-07-19)
- **WO-748 Default Town founding choice** (`f5fcbde2`) — FoundingChoiceController after PetSelect; Default
  Town re-triggers StrategicPlacementMigration -> movable ring at live grid cells. RESULT filed. Owner
  felt-verify R1/R2/R3.
- **WO-749 dungeons as crafting-ingredient source** (`0c64daaa`) — DungeonLootGrant (chest rewardKey +
  ATB-victory roll + larder deposit), DungeonChestInteract, 4 new loot tables incl 5 legendaries, 10 floor
  scatter, + the 7 gear-component MaterialDefs. RESULT filed. Echo-Exploration multiplier deferred (WO-750+).
- **Corrupt d4 scene PURGED** (`c5b3461c`) — `d4_sunken_crypt.unity` (58% NUL) removed + build-settings
  entry; stale merged branch `feat/room-forge-dungeon-baker` deleted; the dungeon session's broken
  uncommitted rooms-catalog/EntryHall socket rework restored out of the tree. dg_starter_loop is the
  playable dungeon.
- **All 5 regression reds FIXED** (owner's Prioritized Resolution Plan):
  - R1 arena ground texture (`00568728`) — bound grass to ArenaGround.mat -> `[arena-prefab]` OK.
  - R2 dual-wallet (`ef6f097b`) — EconomyService.Grant mirrors Wood/Iron to GameState -> `[village-econ]` OK.
  - R5 orc-raider SSOT (`6ac98fa3`) — enemies.json Hp 130 + WildlandsRoster; 5 spawners unified -> `[combat-atb]` OK.
  - R3+R4 persistence (`98ff1135`) — save v33->v34 persists pet-slot + Tribes/Wards/Arena -> `[glimmer]`/`[core-save]` OK.
- **Process canon:** `SUNDAY_HOUSEKEEPING.md` (weekly full-sweep ritual) + the KNOWN DICTIONARIES registry
  (memories `sunday-housekeeping-ritual`, `audit-outputs-as-known-dictionaries`).
- **Notion setup kit staged** (`docs/notion/` — runbook + 4-DB schema + 665-row seed CSV). Blocked only on
  owner `/mcp` auth of the new Notion instance.

## Known dictionaries (stored registries — see SUNDAY_HOUSEKEEPING.md)
- `docs/reference/HERO_ANIMATION_DICTIONARY.md` — hero anim->action + Right ActionBar (Attack + 3 skills) +
  Hot-Swap bar mappings. *(landing from the hero-animation-audit fleet)*
- `docs/reference/REGRESSION_COVERAGE_MATRIX.md` — every silo-audit finding -> covering regression. *(landing
  from the silo-audit fleet)*

## Owner rulings still in force (from 07-17/18)
- One-free-total build placement + FoundingKit exemption; founding Echo fires the full EchoUnlockDialogue
  card; gear drop 2%/slot; Echo lanes: only Harvest host-wired (Crafting/Defense/Exploration stubbed).

## Open / owner's
- **Felt-verify:** the 5 red fixes on mobile (arena look/perf; orc-raider wave balance; multi-slot pet +
  tribe/ward/arena survive reload; dual-wallet upgrade income) + WO-748/749 screens + the converted MVVM
  screens (still pending from 07-18).
- **Push** authorization (7 commits held) + prod promotion (owner's).
- **Notion:** run `/mcp` to auth the new instance -> the staged runbook executes.
