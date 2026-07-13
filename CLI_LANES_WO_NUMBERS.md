# Lanes — Work-Order Numbers Only (for CLI)  ·  reconciled 2026-06-12 (nightly refill)

> ## ⚠ REFRESHED 2026-07-13: next free WO = **708** (**707** = town catalog grooming, one-building-per-trade + pallet stores · **706** = build-palette portraits complete set, UI-seat art · **705** = onboarding duplicate-UIDocument RCA, fleet-captured) (**704** = trailer fullscreen/aspect, ticket VID-1) (**703** = blank-start residual standdown, ticket BLANK-1) (**699** = hero-select ability chips SEL-1 · **700** = Android APK/Seeker test [UI-seat mint, correctly took next-free] · **701** = Mending Echoes offline repair [renumbered from colliding fresh 686] · **702** = Founding of Elarion FTUE [renumbered from colliding fresh 699]) (688–695 consumed by the collision renumber below; **696** = repair-before-upgrade context, renumbered from a colliding fresh 684 mint on 07-13; **697** = currency compact-format + icon chips, ticket RES-1; **698** = encounter budget + scouting, renumbered from a colliding fresh 685 mint). Prior refresh 07-12: 685/686/687 = web-trace lifecycle trio (685 retention/TTL cron, 686 ingestion hardening, 687 read/triage surface); 684 = outstanding-items board. Disk max = **698**.
> ⚠ **UI-SEAT SYNC NOTE (2026-07-13):** the spec-writer seat is minting in the pre-renumber 674–685 space — every fresh mint there collides. UI-seat numbers translate: its 682=695 (strategic placement) · its 683=693 (jeweler) · its 684=696 (repair context) · its 685=698 (encounter budget). Point the UI seat at THIS banner before its next mint.
> The per-lane detail below is **FROZEN HISTORY** (pre-430 era — ~270 numbers stale); do **NOT** mint from it.
> **Collisions RESOLVED 2026-07-13** (677–681 each had two specs on disk; the 07-12 evening-arc/ticket-board side kept its number, the other spec was renumbered — files renamed + headers updated):
> - 677 kept `mobile_buildmode_move_unreachable` (spec + RESULT) · `asset_caster_toolkit_family` → **688**
> - 678 kept `pi_sdk_timeout_clean_wrap` (spec + RESULT) · `hovl_vfx_fidelity` (spec + RESULT) → **689**
> - 679 kept `crystal_economy_faucets` (owner ask 07-12) · `swordshield_full_wiring` (Grok package) → **690**
> - 680 kept `enhancement_tier_gate_legibility` (ticket UPG-1) · `blink_orcs_activation` (Grok package) → **691**
> - 681 kept `echo_select_intro_and_assign` (ticket ECHO-1) · `blink_icons_import` (Grok package) → **692**
> - 683 kept `build_screen_dpad` (spec + RESULT, ticket closed by PO 07-13) · `jeweler_crafting_mobile_readability` → **693** (untracked — stage when claimed)
> - 685 kept `webtrace_retention_ttl_cron` (banner-canonical, board row Done 07-13) · `webtrace_lifecycle` → **694** (untracked — stage when claimed)
> - 682 kept `web_quiet_error_surface` (spec + RESULT, board row Done 07-13) · `strategic_placement_lock_on` → **695** (untracked — stage when claimed; implementation agent pre-briefed on the rename)
> Rule stands: **mint from THIS banner's next-free, bump it in the same edit.**

Branch **feat/tower-core-loop**. Numbers only, run order. `→` = serial (same lane, in order);
commas = parallel-safe. Detail in `MASTER_PIPELINES_BACKLOG_2026-06-06.md`. New WOs ≥290 are spec'd by
this session's design docs (see "Newly minted" below) — full WO files on request.
**Numbering authority = the master doc + this file, NOT the filesystem max. Next free WO = 430**
(287/288/306–343 used, 289 free, 290–305 minted, 339–343 refill; **352–390 minted out-of-band by the
2026-06-08/09 sessions** and slotted on 2026-06-10; **344–351 skipped — treat as used/reserved,
do NOT mint** per CLAUDE.md; **391–411 minted on-board (Notion) by the 2026-06-10/11 owner/CLI
sessions** — specs live in the Notion rows, only WO-405 has a repo spec file — slotted on 2026-06-11;
**412–428 minted on-board (Notion) by the 2026-06-11/12 owner sessions** — playtest bug sweep, slotted
below on 2026-06-12; **429** = the repo "store stock from DB" spec renumbered from a colliding WO-414).
⚠ **Number collisions (clean up via Lane 0 dedupe item):** repo has duplicate WO files for 329/330/331/333/334
(and legacy 43/46/106–111/129/136–138/152/159/179/181/253–257/279/280/282/301); Notion board also carries a
*different* 328–339 P0-bug block (06-08 session) vs this file's 328–339. Do not reuse any of these numbers.
**Live board (Notion mirror):** https://app.notion.com/p/f3115f05ecf940cf8968bd82bbbdff9f — see `NOTION_SOURCE_OF_TRUTH.md`.

---

## Newly minted this session (≥290 — keeps lanes full)

- **290** QuestService + quest tracker UI (backbone for all questlines) — *foundational, do early*
- **291** Vendor Yarn pack (9 NPCs) + NPCCommandBridge quest verbs (StartQuest/Advance/Complete/GiveKeystone)
- **292** Keystone → Spire finale wiring (≥6 Keystones → Spire Defense → Necromancer)
- **293** Crafting tiers (Common/Fine/Master/Legendary) + legendary recipe system
- **294** Forgemasters' Saga: 4 deep crafter Yarn files + 3 reconciliation scenes
- **295** Legendary set "Aegis of Elarion" items + Oathweld ward effect
- **296** Reforge choice (Heart vs cleansed regions) → finale/ending wiring
- **297** Pet acquisition + slots (tame / egg-hatch / rescue)
- **298** Pet skill catalog content + balance (4 branches + signatures)
- **299** Pet bond questlines (Fenn "Wild Hearts" + per-species)
- **300** Elarion weaponsmithing lore integration (item flavor, maker's marks, appraisal)
- **301** Party persistence — wallet-keyed roster in GameState (+ migrate pet PlayerPrefs blob)
- **302** Floating health-bar oversize fix (green-pill host-scale)
- **303** Combat party HUD wire-to-live-data (HUDManager)
- **304** Brom's rumor board (quest-board UI; can fold into 290)
- **305** Relic-recovery quests (Dawnedge / garrison blades / pattern-blade)
- **339** SaveSchema: add quest state versioning + migration stub (anchor for all quest WOs)
- **340** PlayerPrefs migration: legacy pet/party data → GameState on load
- **341** Backend: auth token refresh + expiry handling
- **342** WebGL: memory optimization + GC pressure reduction
- **343** Analytics: event batching + periodic backend flush

## Out-of-band block 352–390 (minted 2026-06-08/09 sessions; slotted 2026-06-10)

- **352–353, 355–357** build-mode/UI panels (preview, palette filters, portrait layout, placement validation, touch) — L4
- **354** upgrade tier display + synergy — L11
- **358** ✓DONE Yarn welcome · **359** combat feedback — L9 · **360** companion echo outpost — L12
- **361** wave rewards/passive XP — L6 · **362** enemy wave composition — L2 · **363** orientation validation gate — L0
- **364** companion gear — L12 · **365–367** idle poses/routines + town camera — L10 · **368** ✓DONE camera regression
- **369** arena monument — L1 · **370–372** monument VFX, combat SFX, battle music — L9
- **373** critical regression gates — L0 · **374–378** UI fixes (char select, Yarn threading=375, hero pose, dialogue block, town HUD) — L4
- **379** echo auto-summon — L12 · **380** ✓DONE gear icon · **381** ATB arena cleanup — L2 · **382** ✓DONE hero HP
- **383** castle↔outerworld seam — L5 · **384** castle stairs — L1 (CastleHubBuilder single-writer) · **385** castle camera (fade landed, pending playtest) — L4
- **386** battle visualization (understand-phase done) — L2/L4 · **387** ✓DONE camera-relative movement
- **388** player castle as arena defender (SPEC) — L2 · **389** arena mode attack/defend (partial built) — L2/L6/L4 · **390** battle potion loadout (SPEC, after 389) — L6/L2

## Out-of-band block 391–411 (minted on-board 2026-06-10/11 owner/CLI sessions; slotted 2026-06-11)

Newly minted: specs live in the Notion rows (only **405** has a repo `WORK_ORDER_*.md` file — backfill the rest when claimed).
⚠ **HUD/UI gate:** 400/403/404/411 are **Blocked on WO-405** (UGUI design-system owner-approval gate) — do not pick up until 405 is Done.

- **392** Warcraft-style tiered building upgrades (Lumbermill/Forge/Armorer) — L11 · **407** Arcane Tower tiers (extends 392) — L11
- **393** low-contrast yellow text on building-upgrade UI — L4 · **394** build click gives no feedback (surface block reason) — L11
- **395** resource node / mine interaction visual replacement (asset-library audit first) — L5
- **398** Knight still dealing ranged damage (melee-only) — L2 · **399** Knight melee weapon skill set — L3
- **400** Inventory rework to mockup (after 403) — L4 · **401** Blacksmith vendor presentation (signboard + Yarn-only) — L12
- **403** UNIFIED context HUD shell + TOWN mode (RESPEC; rebuild, don't patch; needs 405✓kit) — L4
- **404** combat HUD group (same canvas as 403, waits on 403) — L4
- **405** ✓kit-approved — UGUI design system `ElarionUiKit` (P0, blocks all HUD work; repo file exists) — L4
- **406** shops empty — vendor inventories not populated — L6 · **408** WebGL texture optimization 223→<60 MB (scripts committed, NOT run) — L10
- **409** magenta tower materials (Standard→URP swap, NOT broad fixer) + UI sprite `*`/`#` glyphs — L0
- **411** Town HUD ≠ `hud_mobile_town.png` mockup (10 deviations; blocked on 405, folds into 403 path) — L4
- **391 / 396 / 397 / 402** — used on-board (rows not mirrored here yet — see the Notion board for titles) → do NOT mint
- **410** ★P0 — 0.1 fps in MainCastle_Hall: main-thread GC storm + combat-object leak — L10 (title mirrored 2026-06-12)

## Out-of-band block 412–429 (minted on-board 2026-06-11/12 owner sessions; slotted 2026-06-12)

Owner playtest bug sweep + vendor/storefront chain. Specs live in the Notion rows (repo spec files exist
only for **413** and **429**; backfill others when claimed).
⚠ **Collision resolved 2026-06-12:** the repo file `WORK_ORDER_414_store_stock_from_db.md` collided with
Notion's WO-414 (TALK-glow black disc). Repo spec **renumbered → WO-429** (`WORK_ORDER_429_store_stock_from_db.md`);
the old 414 file is marked SUPERSEDED. Notion's 414 stands.

- **412** ◐ Vendor Wares BUY tab empty (layout fix `ca89d9b` landed; build-test + catalog-load open) — L6
- **413** upgradable vs shoppable building menus (data-driven; repo spec exists) — L6
- **414** black circle under TALK button (AttentionGlowUi first-frame) — L4
- **415** vendor storefront UI from Tech hud elements pack (after 412) — L4 · **416** hide "Talk:" world prompt (supersedes 411 #9) — L4
- **417** ★DO FIRST: Settings/Dev Tools rows blank (owner's test harness) — L4 · **421** battle HUD skill bar empty — L4 · **428** hero damage not shown on HUD — L4
- **418** castle→OuterWorld hard-pop blend — L5 · **426** enable node/outpost claim loop — L5
- **419** enemies don't attack after castle→OuterWorld — L2 · **423** hero attacks without facing target — L2
- **422** Echo Warden pet-selection gate + unlock quest — L12
- **424** harvested resources not in HUD count — L6 · **425** hero spawns unarmed (default weapon) — L6
- **420 / 427** — used on-board (titles not mirrored — see the Notion board) → do NOT mint
- **429** store stock served from Neon DB (StoreService + offline-first fallback; repo spec exists) — L7

---

## Lanes (topped up)

**Lane 0 — Verify/build now:** 283✓, 284✓, 285✓, 286✓, 107, 108✓, 109, 110, 111, 329(regression suite), 302, 303, 363(orientation gate), 373(regression gates), 409(magenta towers + UI glyphs)  ~~328 CLOSED (ambiguous/no repro)~~
**Lane 1 — World/Env (VillageSceneBuilder = SOLE WRITER, serial):** 253 → 166✓ → 167 → 168 → 157 → 137, then 173, 245, 246, 247, 263, 311, 312, 313, 321, 323, 369(arena monument), 384(castle stairs — CastleHubBuilder)
**Lane 2 — Combat/AI (parallel):** 254, 255, 135, 145, 146, 147, 155, 128, 287(SPEC), 310, 315, 316, 317, 318, 320, 326, 327, 330(DTT cyan hero), 331(DTT hotkeys), 332(DTT aim sensitivity), 333(village death→DTT/ATB HIGH), 335(ATB purple capsule bug HIGH) → 336(ATB village wall environment), 362(wave composition), 381(ATB arena cleanup), 386(battle viz, w/ L4), 388(arena defender base SPEC), 389(arena mode, partial), 398(Knight melee-only), 419(enemies passive after transition), 423(rotate-to-target)
**Lane 3 — Combat Feel (serial):** 288(in-progress) → 213 → 217 → 218 → 219 → 220, then 295 (legendary set feel), 319 (DTT parity/anim), 399 (Knight melee skill set)
**Lane 4 — UI/HUD (parallel):** 307 → 308, 309; 303, 302, 110, 124, 156, 178✓, 237, 257, 304, 322, 337(Echo Hollow dialogue overlap HIGH), 338(Echo Hollow rebrand — UI strings), 352, 353, 355, 356, 357, 374, 375, 376, 377, 378, 385(castle camera, pending playtest), 380✓, 382✓; **405✓kit → 403 → 404, 400, 411** (unified HUD path — P0 chain, 400/403/404/411 blocked on 405 Done), 393, 414(TALK-glow disc), 416(hide Talk prompt), 417(★DO FIRST: Settings/DevTools rows), 421(battle HUD skill bar), 428(hero damage HUD); 415(vendor storefront skin, after 412)
**Lane 5 — World/Exploration:** 164 → 153✓, 159, 160, 165, 142, 143, 144, 154, 305, 324, 383(castle↔outerworld seam), 395(node/mine visual replacement), 418(castle→OW blend), 426(claim loop)
**Lane 6 — Economy/Progression:** 228 → 229, 151, 115, 117, 119, 194, 293, 297, 298, 325, 361(wave rewards), 390(potion loadout, after 389), 406(empty vendor shops), 412◐(Vendor Wares BUY empty), 413(upgradable vs shoppable menus), 424(harvest→HUD count), 425(default weapon)
**Lane 7 — Persistence/Backend:** 301 → 339 → 340, 341; 120, 80, 129, 121, 118, 429(store stock from DB — needs React-repo GET endpoint)
**Lane 8 — Monetization/Store:** 72, 73✓, 74, 75, 76, 77, 78, 79, 80, 236
**Lane 9 — VFX/Audio (parallel):** 256, 264, 272, 195, 170, 171, 66✓, 111, 243, 359(combat feedback), 370(monument VFX), 371(combat SFX), 372(battle music)
**Lane 10 — Build/Deploy/Perf:** 196 → 211 → 342, 343; 191, 51, 53, 54, 57, 282(HELD), 365, 366, 367, 368✓, 408(texture opt — scripts committed, not run), 410(★P0 GC storm 0.1fps MainCastle_Hall)
**Lane 11 — Build Mode / Player Base:** 108✓ → 215, 282, 113, 114, 181, 104, 239, 292, 314, 334(tower placement rotate menu), 354(upgrade synergy), 392(building upgrade tiers) → 407(arcane tower tiers), 394(build-block feedback)
**Lane 12 — Narrative/Onboarding/Quests:** 290 → 291 → 304, 230, 222 → 227, 238, 277, 116, 235, 133, 294, 296, 299, 300, 338(Echo Hollow rebrand — Yarn + DESIGN-DECISIONS), 358✓, 360(echo outpost), 364(companion gear), 379(echo auto-summon), 401(blacksmith presentation), 422(Echo Warden pet gate + unlock quest)

**Hard rules:** ONE agent in Lane 1. `GameState.cs`/`SaveSchema` field-adds (Lanes 5/6/7/11/301/339) additive,
one-at-a-time. **Do early:** 164 (zone), wallet/economy merge, 290 (QuestService), 339 (SaveSchema anchor) — many lanes depend on them.
Overlaps: 108 (5/11), 282 (10/11), 80 (7/8), 111 (0/9), 295 (3/6), 340 (7/301).
