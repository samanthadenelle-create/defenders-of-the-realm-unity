# Overnight Report — 2026-06-27 → 06-28

**Operator:** CLI (sole committer). **Branch:** `wip/village2-and-f8-tickets`.
**Posture:** safe high-value progress; everything committed **LOCAL only — NOTHING pushed**
(awaiting owner felt-verify per §11). Editor was closed the whole session (headless-safe).

> Owner directive (recap): "1. full regression (village→raid→combat→return). 2. verify Knight
> talents + Obsidian sprites. 3. smoke-test crafting. 4. asset sweep for broken refs. 5. if time,
> +4-6 recipes. Prioritize regression+verification first. Then attempt headless web runs + log to DB."

---

## TL;DR (read this first)
- ✅ Crafting system + Apothecary opener **committed** (`2cd8e142`) — gate green.
- ✅ Talent data **verified PASS 8/8**; ranger/mage correctly inert; **1 real break found + fixed**
  (mage icons pointed at a non-existent folder — `6e6a4db9`).
- ✅ Asset sweep: **20 broken refs found → all 20 fixed** (the mage-icon repoint). Everything else clean.
- ✅ Fresh Windows exe built (`22:27`, includes crafting+talents+accessories) — morning felt-test ready.
- ✅ Full-loop AutoPilot regression: **12/12 instances exited cleanly** (no hang/softlock); all
  my-relevant steps PASS (incl. save round-trip). **One new issue found & fixed** (magenta drop mote,
  `4f7d2fed`); the rest are pre-existing/known (headline: OuterWorld seam crossing — known V2).
- ✅ Crafting smoke: permanent `CheckCraftingChain` oracle → **16/16 recipes craftable drops→craft→consumable**.
- ✅ Stretch: **+4 functional recipes/consumables** (heal/rest, art-backed ingredients) — **committed** (`4f7d2fed`).
- 🔎 Web self-healing loop: backend telemetry pipeline **already exists** (`api/events/track.js` →
  `analytics_events`); loop needs only client wiring + WebGL autopilot loader + cron — **deploy is a
  morning GO** (outward-facing). Plan below.

---

## 1. Regression — village → raid → combat → return (OWNER PRIORITY #1)
- **Method:** `run-autopilot-fleet.ps1 -Count 12 -SeedStart 1 -TimeoutMin 8` against the fresh
  `Builds/Windows/DefendersOfTheRealm.exe` (built 22:27, incl. all of today's commits up to crafting).
- **Headline:** all 12 seeded instances **exited on their own** (none killed by the timeout) →
  no softlock / infinite-loop / hang in the core loop across 12 distinct chaos seeds.
- **What PASSED (all 12/12):** BootToGameplay, ResolveHero, WalkToEachGate (4/5 gates),
  OpenEachVendor (1/1), AssertVendorContracts (0 violations), AssertVendorTalkRoute (0),
  AssertEconomyDeduct, AssertEquip, **AssertSaveRoundTrip (PASS — the new ISaveProvider save seam
  round-trips wallet+roster+quest)**, DiagGarrisonRoster (6/6, magenta=0), OpenEachHUDPanel (10/10),
  TriggerWave, AssertCombatInvariants (PASS). → **crafting/talents/save introduced no regression in
  covered flow.** Combat in the encounter actually resolves (`droppedToBattle=True spawned=3 skinned=3
  orcRig=3 resolved=True`).
- **Ranked tickets (Builds/autopilot-tickets.md), triaged:**
  | Ticket | Repro | Mine? | Disposition |
  |---|---|---|---|
  | Seam crossing unreachable `rgate_castle_to_outerworld_east` / `RuntimeSeam_Trigger` (~7067m); `heroReturn=7018m` | 12/12 | **No** | Known V2 navmesh-seam issue (memory `v2-enemy-seam-navmesh-traversal`). Pre-existing. Top owner-priority known gap, but not a tonight regression and a known-hard V2 item — left for owner direction. |
  | MAGENTA `ItemDropMote` (InternalErrorShader) | 6/12 | **Yes** | **FIXED** — enabling drops surfaced the mote using the built-in Standard shader (URP can't render it). Now builds a URP/Lit material. See §4/commits. |
  | Duplicate ENABLED UIDocument (MusicSelectionPanel + [DEV] QA Dev Console share DevRuntimePanelSettings) | 7/12 + 5/12 | No | Pre-existing **dev-only** panel collision; not in player builds. Logged for a follow-up WO. |
  | Yarn `DialogueException: No node has been selected` | 5/12 | No | Recurring known Yarn issue (memory `yarn-no-node-stop-after-panel-command` — fix is `<<stop>>`). Pre-existing; Yarn is being retired (WO-455). |
  | PanelRouter RumorBoard / BuildingUpgrade "open but no panel visible" (WO-465 invisible-scrim) | 4/12 | No | Pre-existing known scrim issue. Logged. |
- **Verdict:** **core loop is stable and regression-free for tonight's work.** The one new issue my
  changes introduced (magenta drop mote) is fixed. The headline pre-existing gap is the OuterWorld
  **seam crossing/return** (known V2). Combat itself works end-to-end.

## 2. Knight talents + Obsidian sprites (verify end-to-end)
- **Data verify: PASS 8/8** (read-only agent, cited evidence):
  68 nodes (20 knight / 20 ranger / 20 mage / 8 shared); tier costs 1/2/3/5 + shared 2; vertical
  same-slot prereqs all resolve; **capstone exclusivity enforced in catalog** (`HeroTalentCatalog.CanUnlock:177`
  + `WisdomCurrencyService.Unlock:113`, UI reason `HeroSkillTreeVM:269`); 6/20 Knight nodes have live
  V1 consumers (Iron Resolve / Spear Thrust / Guardian Stance / Mending Oath / Legendary Vanguard /
  Knight Eternal), the other 14 inert **by design** (every one JSON-tagged `(allies — V2)` / `(… — V-later)`).
- **Ranger/mage:** correctly inert (data+icons only; `HeroSkillTreeVM.HeroSlug="knight"` hardcoded).
- **Sprite render:** headless can't confirm visuals (`-nographics`) → **owner felt-test confirms** the
  panel art in the morning. Data/path side is now clean (see fix in §4).

### Owner decisions still open (flagged, not blocking)
- **Wisdom soft-cap:** 3 repeatable income sources (per-wave +2 `WaveFeedbackDirector:109`, arena win
  `BattleArena:1289`, daily `DailyQuestTowerBridge:131`) → total Wisdom is effectively **uncapped**, so a
  player can eventually buy the whole tree. Decide if a hard cap is wanted for V1.
- **14 inert Knight nodes are takeable + cost Wisdom with no in-panel "(V2)" warning** — incl. the
  stat-styled *Aegis Reinforcement (shieldStrength)* and both **taunt** nodes (taunt deferred to V2 per
  your Q3). Recommend surfacing the JSON `note`/a "(V2)" suffix in the panel before felt-test (small
  CLI panel tweak) so players don't sink Wisdom into dead nodes. Held for your call on wording.

## 3. Crafting smoke test (drops → craft → inventory)
- **Added a permanent headless oracle** `CheckCraftingChain` in `DataRegression` (re-runs every gate):
  proves the full data chain — every recipe **output** is a real consumable, every **ingredient** is a
  real material **and droppable in ≥1 loot table** (so it's obtainable → the recipe is craftable), no
  phantom drops, no unknown ingredients. Legacy scaffolding ids excepted.
- **Result:** `[crafting] chain checked: 16 recipe(s), 12 material(s), 17 droppable id(s); 16 recipe(s)
  fully craftable drops->craft->consumable` → **REGRESSION_OK**. All 16 recipes (8 original + 4 stretch +
  4 legacy) are end-to-end craftable from drops.
- The **runtime transaction** (`ItemCraftingService.TryCraft` consume→deposit) reuses the already-shipping
  `VillageInventory` larder (same store the Workshop crafts from), so storage is proven. A play-mode
  craft oracle (an AutoPilot crafting step — `VillageInventory.Instance` only bootstraps at runtime) is a
  recommended follow-up WO for full in-engine coverage.

## 4. Asset sweep — broken references
- **20 broken refs found, all 20 fixed.** `hero-talents.json` pointed the 20 mage node icons at
  `Talents/mage/mage_NN`, but the imported art lives in `Talents/wizard/wizard_NN` (no `mage/` folder).
  Invisible in V1 (Knight-only) but broke the stored mage data. **Fix `6e6a4db9`:** repointed the 20
  `iconPath`s to the wizard folder (node ids keep the `mage.*` slug per your convention); both
  Resources + StreamingAssets; JSON re-validated.
- Everything else clean: 10 accessory icons, 8 consumable + 12 ingredient icons all resolve; recipe
  inputs/outputs all resolve; dual-copy (Resources == StreamingAssets) byte-identical for all 6 catalogs.
- Informational (not a break): 5 legacy scaffolding mat ids (`wild-herb`/`rare-essence`/`monster-hide`/
  `tattered-cloth`/`ember-resin`) referenced by the 4 pre-existing recipes + default loot tables but not
  in `materials.json` → documented-intentional glyph fallback.

## 5. Stretch — +recipes & drop tables
- **+4 functional consumables + 4 recipes** authored (16 total each now):
  Field Poultice (heal 60), Hearthfire Stew (food, heal 80), Warden's Campfire (tent/rest), Purifying
  Draught (heal 50). All use **heal/rest** (the only effects V1's `ConsumableUseService` actually applies
  → functional now, not stored-inert) and only the **12 art-backed ingredients** (already supplied by
  existing drop tables, so no invented enemy ids). Glyph fallback for art (no new sprites) — **names/balance
  are owner-review.** Referential integrity + R==SA parity validated.
- **Status: committed** (`4f7d2fed`) — gate + DataRegression green; crafting-chain oracle confirms all
  4 are craftable from existing drops.

## 6. Web self-healing loop (headless web runs → DB → fix → redeploy)
- **Recon verdict: ~60% already built.** `WebTrace.cs` (WebGL FlowTrace→HTTP sink, batched) is complete
  but dormant (`TraceEndpoint=""`). And the **telemetry backend already exists**: `api/events/track.js`
  (Vercel serverless) → Neon `analytics_events`, plus `api/bug-report.js`, with client `EventTracker.cs`.
  `vercel.json` present (`deploymentEnabled:false`, outputs `Builds/WebGL`).
- **What's actually needed (small):**
  1. Wire `WebTrace.cs` `TraceEndpoint` → existing `/api/events/track` (eventName `flow_trace`, batch in
     properties) — **no new endpoint/table needed.** (Pure client constant; 1 line.)
  2. WebGL AutoPilot loader: a `?autopilot=1` URL hook + WebGL-template JS `SendMessage` + a runtime entry
     so bots run in a **dev** WebGL build (AutoPilot is `#if DEVELOPMENT_BUILD||UNITY_EDITOR` — release
     can't auto-run it).
  3. Headless-Chrome driver (I have claude-in-chrome tools) to launch N tabs against the deployed URL.
  4. Cron orchestration: deploy → bot-run → pull `analytics_events` → triage/fix → redeploy.
- **Why I did NOT deploy overnight:** steps 3–4 are **outward-facing** (deploy to Vercel + read prod DB).
  Per the confirm-before-outward rule + the itch→Vercel move itself still being your pending call, I staged
  this to a **one-command morning GO** rather than push it live autonomously. Buildable code pieces can be
  authored + gated overnight if you want; the deploy + cron-arm is your greenlight.
- **Full executable plan written:** `docs/WEB_SELF_HEAL_LOOP_PLAN.md` — current state, the 4 build steps
  with exact files, deploy + cron steps, risks, and a localhost-first milestone before any production
  exposure. Tasks #21/#23 are **planned + de-risked + staged** (loop not yet running — deploy is your GO).

---

## Commits this session (LOCAL — not pushed)
- `2cd8e142` feat(crafting): Apothecary consumable crafting — 8 recipes, drops, Obsidian panel, opener
- `6e6a4db9` fix(talents): repoint 20 mage iconPaths Talents/mage → Talents/wizard
- `4f7d2fed` fix(items)+feat(crafting): URP drop-mote material; +4 recipes; crafting-chain regression oracle
- _(plus earlier today, pre-overnight: save seam `16f10308`, Addressables seam `ae7330d4`, talent icons
  `ae9095c8`, Knight talents `9f926d76`, talent decisions `28e2735c`, accessory icons `5fe8ba4d`,
  crafting icons `d044aaa6` — all LOCAL, none pushed)_

**Final morning exe:** rebuilt **22:47** after all fixes (incl. the URP mote fix) so the felt-test shows
gold loot motes, not magenta. → `Builds/Windows/DefendersOfTheRealm.exe` (+ `_Data`). Build SUCCESS,
gate-clean.

## Morning checklist for owner
1. Felt-test the fresh exe: Knight talent panel art + crafting at the Apothecary (drops → craft → use).
2. Decide: Wisdom hard-cap? + "(V2)" tagging on the 14 inert Knight nodes (wording).
3. Decide: canonical Apothecary placement (currently a runtime-injected station; `CastleHubBuilder`
   rebuild deferred to avoid a guessy overnight scene change).
4. GO/NO-GO on the web loop deploy (Vercel + cron) — everything else is ready to wire.
5. If all felt-good → I push the local commits.
