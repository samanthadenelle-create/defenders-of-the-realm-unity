# CANON GROUND TRUTH — 2026-07-13 (midday sync handoff)

> **Purpose:** the single anchor of *current reality*, verified from the working tree, HEAD, the
> boards, and owner rulings given live this session. **Supersedes `CANON_GROUND_TRUTH_2026-07-12.md`**
> (bannered). If a doc contradicts a line here, the doc is STALE.
> Written as the sync handoff for the UI/triage seat (its 5-point request, 2026-07-13).

> ## ⚡ AFTERNOON UPDATE (2026-07-13 PM — the FOUNDING arc; supersedes the midday lines below where they conflict)
> Owner felt-passed the midday wave, then ruled + shipped the founding redesign in one arc (all committed local, push HELD):
> - **WO-703 BLANK-1 CLOSED-pending-felt:** fresh save = tree + well + walls/gates, `BLANK_START_OK` oracle standing.
> - **WO-707 (one building per trade, CoC-modeled):** Town tab = Echo Hollow · Store (ex-Market, Buy Packs/PackStore front) · Forge · Armorer · Arcane Tower · Jeweler · **Farm** (mill RETIRED, "farm is cleaner") · Lumbermill · **Lumberyard/Foundry/Silo** (NEW storage containers, shared GenericContainer Tripo body, `storageCapacity` data field stubbed, NOT singleton, CoC visible-fill = pass 2 with the WoodBox pallet props). Retired from palette (locked, rows loadable): mine_crystal ("that's a node"), mill, lumbermill(Sawmill), armorer(Blacksmith), collector_forge. **All 8 trade buildings singleton.** Enemy targeting = CONTAINERS ONLY (seam stubbed, WO-672 wires it). Destroyed building = pay full cost to REBUILD, re-placeable anywhere. Vendor anchors: Windmill→collector_farm, Lumbermill→collector_lumbermill.
> - **Founding seed = 650w/385i** (affords exactly one of each + the 3 containers; StartingBudget constants). **The WO-682/695 FTUE grace forge is KILLED** ("should be placed by player") — ResetToNewGame BaseLayout = EMPTY; census + core-save oracles updated (mandatory chain = exactly 10).
> - **WO-702 The Founding of Elarion SHIPPED (awaiting owner felt-pass):** Sylas IS the Steward (Ranger body, spawned by new SylasStewardInjector, unloads on Onboarded); 7 founding beats (greet → Hollow, pet ice-wolf SPAWNS OUT of it → stores lesson on the Lumberyard → free build → Echo gather offer → a defense or two → teaching wave); per-item signal `build.structure_placed:<id>`; peace window = the existing `!Onboarded` gates; PetSelect stays bypassed (Hollow IS the bonding). Founder's-Plan ghost NOT shipped (pin was never ruled; fast-follow if wanted).
> - **WO-705** (onboarding duplicate UIDocument, fleet-captured) READY · **WO-706** (10 palette portraits incl. the 3 containers, UI-seat art) READY · **WO-708** (wall builder drag-lines — the owner's "complete any base" vision) **PARKED POST-V1**.
> - **PUSHED 2026-07-13 evening (owner: "can be checked in — we've been testing and refining all day"):** origin `1ee7b6af..fd33aeea`, 66 commits — the full founding arc + earned economy (free-first-builds v32, zero seed, level-1 producers, WO-709 quadratic echo multiplier cap-5-by-waves, two-step placement, CoC raid canon). The "push HELD" lines above are SUPERSEDED by this push; prod/preview deploys remain separate owner calls.
> - Fleet-aggregation Remove-Item race fixed in run-unity-method.ps1. Latest exe = **13:59** (the founding build). Two-committer incident resolved (the crashed session auto-resumed and committed `cac9d0ca`/`7df41dee` ungated — content post-hoc gate-verified green; memory `resumed-session-ghost-committer`).

## Repo / git
- **Branch `wip/village2-and-f8-tickets`, HEAD `2de11256`** (docs: morning brief 2026-07-13).
  **Ahead 22 of origin** (origin sits at `1ee7b6af`, 07-12 09:50 — a push happened 07-12 morning;
  the "95+ ahead" line in older canon is stale). **Push HELD** for the owner's word.
- **Save schema = v30 on disk; v31 PENDING** — the echo lane added `echoLanes` (additive,
  default-on-read); the orchestrator applies the v30→v31 bump at the reconcile pass per the
  additive-field precedent. Claims of "v29" are doubly stale.
- **DIRTY TREE — this session's implemented-but-UNGATED wave** (do NOT treat as shipped):
  WO-680 upgrade-panel legibility (UPG-1) · WO-602 home-return portals · WO-681 echo card
  (ECHO-1) · WO-693 jeweler/crafting readability · WO-695 strategic placement flag-removal
  (21 files) — all edit-complete, brace/NUL-clean, awaiting ONE batch CompileGate + DataRegression
  + build + fleet, then per-lane commits. Still IN FLIGHT: WO-697 currency chips (RES-1) and the
  REP-1 fix lane (instrumentation + REPAIR_PROBE + RepairFull). Plus the WO renumber renames
  (688–695 staged as git mv; 693/694/695/696/698 untracked renames) and canon/board doc edits.

## Build / deploy state
- **Windows exe:** `Builds/Windows/DefendersOfTheRealm.exe` stamped **2026-07-12 19:07** —
  PRE-DATES everything in today's dirty tree.
- **WebGL PREVIEW (current):** https://defenders-of-the-realm-v2-9ncz1sks9.vercel.app
  (ship build, `BuildOptions.None`, deployed 07-12 21:26; the Seeker share-bypass link in
  `MORNING_BRIEF_2026-07-13.md` expires ~21:26 tonight). **Owner felt-passed it 07-13 morning** —
  that closed WO-677/678/682/683/685. **Prod UNTOUCHED** (07-04 Pi build).

## World / flags (the load-bearing corrections)
- **⚠ WORLD CORRECTION: `MergedWorld` defaults ON** (`FeatureFlags.cs:315`) — the live world is
  the ONE merged scene **`Main_Castle_Overworld`** (`SceneRouter.Castle` resolves to it;
  `HubScenes.IsOverworld` matches it). The 07-12 anchor's "hub MainCastle_Hall + additive
  OuterWorld" line was STALE vs code. Castle↔overworld is an in-scene walk; portals warp.
- **`ff.strategicplacement` is REMOVED (WO-695, ex-682)** — strategic placement is **locked ON**:
  blank-template new game (authored shell + zero storefronts + core-kit budget 260w/210i +
  Town/Defenses/Walls palette), v30 one-shot migration for existing saves (marker-latched, proven
  not re-triggerable), FTUE guard = grace-default movable Forge record so vendor talk-routes
  survive a fresh save. In the ungated tree.
- **`ff.homereturnportal` NEW, default ON** (WO-602, ungated tree): four runtime-injected
  "Enter Elarion" return portals at the bridge mouths → fade-warp to the courtyard. No bake, no
  scene edit.
- **`ff.buildingupgradepanel`** (WO-675 Obsidian redesign) — live on the current preview.
- ~~Residual on fresh saves: apothecary + jeweler's-bench STATIONS remain injector-owned until
  their catalog rows land (deliberate "never lost" rule).~~ **SUPERSEDED (WO-703 / BLANK-1, owner
  ruling 2026-07-13):** fresh start = TREE + WELL + WALLS (gates included), nothing else — the
  station injectors now stand down on ANY marker-set save (`StanddownActiveForStation` =
  `StanddownActive`); their vendor NPCs follow (no Building, no NPC). Colosseum behind new
  default-OFF `ff.colosseum`. Census oracle = `BlankStartCensusRegression` (BLANK_START_OK).

## WO numbering (authority = CLI_LANES_WO_NUMBERS.md banner)
- **Next free = 699.** All disk collisions RESOLVED 2026-07-13: dupes renumbered → 688
  (asset-caster) · 689 (hovl +RESULT) · 690 (swordshield) · 691 (blink-orcs) · 692 (blink-icons) ·
  693 (jeweler readability) · 694 (webtrace lifecycle) · 695 (strategic placement, ex-682).
  Fresh UI-seat mints renumbered: 696 (repair-before-upgrade context, ex-684) · 697 (currency
  compact/chips, RES-1) · 698 (encounter budget + scouting, ex-685).
- **⚠ UI-SEAT TRANSLATION:** the spec seat minted in the pre-renumber space — its 682=695 ·
  683=693 · 684=696 · 685=698. Owner syncing that seat 07-13; it must mint from the banner.

## Ticket dispositions (the board tickets, honest state)
- **MOB-1** (mobile build-mode Move/Sell unreachable) = WO-677 — **FIXED + PO-CLOSED 07-13**
  (RESULT on disk; verb bar device-confirmed).
- **MOB-2** (build-screen d-pad + text labels) = WO-683 — **FIXED + PO-CLOSED 07-13** (RESULT on
  disk; d-pad felt-passed; DPAD-specific fleet probe rides the next fleet run as regression).
- **UPG-1** (Tier-2 dead-end + "Unlock Maxed") = WO-680 — **IMPLEMENTED, ungated tree**; RCA
  proving cites `BuildingUpgradeVM.cs:385`, `BuildingUpgradePanelMvvm.cs:579/:383`. Spec
  amendment A1–A4 (footer clipping / tile anatomy) deliberately parked — needs a factory-level pass.
- **ECHO-1** (echo select intro + assign) = WO-681 — **IMPLEMENTED, ungated tree**; hosts the
  WO-658 picker; finding: no world Echo body existed (placeholder wisps injected; embodiment=WO-659).
- **VFX-1 / VFX-2** — **no tickets by these IDs exist in the repo.** Best mapping, stated
  honestly: Hovl VFX fidelity = WO-689 (ex-678) — RESULT on disk (done); the VFX Caster
  tag-to-catalog extension — in the tree UNGATED since the 07-12 handoff (WO-684 §A.2). If
  VFX-1/2 mean something else, the UI seat should name the symptoms.
- **REP-1** (repair paid, still looks broken) — **ROOT LOCATED, fix IN FLIGHT**: hardcoded
  `Repair(100f)` (`WallRepairController.cs:813,:924`) vs `Building.Repair` additive clamp
  (`Building.cs:224`) with MaxHp 120–240 → full-price spend, partial restore; walls/gates
  (MaxHp≤100) mask it. No captured line existed (no session ever exercised repair) — the lane
  ships permanent `[Flow:Repair]` traces + a REPAIR_PROBE whose one run yields both the §12
  proof line and the post-fix verification, + `RepairTarget.RepairFull()` at both call sites.
- **RES-1** (six-digit currency clips) = WO-697 — **IN FLIGHT** (CompactNumber in the kit once,
  icon-first CurrencyChip everywhere, content-fit; kit rule: ellipsis on currency forbidden).

## PO closures / board state today
- **Done (PO-closed 07-13):** WO-677, 678, 682 (web errors), 683, 685 (TTL cron). **Dropped:**
  604, 605 (deprecated 07-03-era tickets).
- **Notion rows exist for:** 674 (11 Build Mode) · 675 (11 Build Mode, Verify-Close) · 676
  (3 Combat Feel) · 677/678/682/683/685 (Done) · 679 (6 Economy) · 680 (11 Build Mode, In
  progress) · 681 (6 Economy, In progress) · 684 board · 686/687 (10 Build/Perf, Ready) · 695
  (11 Build Mode, In progress) · 696 (11 Build Mode, Ready, dep REP-1) · 697 (11 Build Mode,
  Ready) · 698 (3 Combat Feel, Ready). **No Notion rows:** 688–694 (renumbered
  losers/backlog — mint rows only if/when claimed).
- Task board (this session): UPG-1 · ECHO-1 · renumber (done) · WO-602 · REP-1 · WO-696 ·
  RES-1/697 · WO-698 — all with handoffLog metadata.

## Next mechanical steps (the standing plan)
1. WO-697 + REP-1 lanes land → reconcile pass (v31 bump; GameStateService/CoreSaveRegression
   overlap review between echo + strategic lanes).
2. ONE batch cycle: CompileGate → DataRegression → REPAIR_PROBE → fresh Windows build → fleet
   (HOME_RETURN, tutorial, panel probes = the verdicts) → commit each lane by explicit path.
3. Owner felt-pass on the new build → next wave from the READY queue (674, 676, 679, 696, 698)
   in PM-audit order. Push on the owner's word only.

## Read order for a cold start
This file → `SESSION_CANON_LOADER.md` → `SAMANTHA.md` → `docs/HANDOVER.md` (07-13 block) →
`docs/MASTER_CATALOG.md` (area) → `CLAUDE.md` + `PREFLIGHT_GATE.md`.
