# CANON GROUND TRUTH — 2026-06-26

> **Purpose:** the single anchor of *current reality*, derived ONLY from verified sources
> (HEAD commit arc, working tree, the auto-memory index, the 06-25 overnight summary, and the
> combat-pivot canon) — NOT from assumptions. Every doc-audit agent checks files against THIS.
> If a doc contradicts a line here, the doc is STALE (unless it is a correctly-dated historical
> ledger, which is frozen-OK). If you believe a line *here* is wrong, FLAG it — do not silently
> trust it.
>
> Sourced 2026-06-26 from: `git log` (commits through `8aa24c32`), `git status`, `MEMORY.md`,
> `OVERNIGHT_SUMMARY_2026-06-25.md`, and the combat-pivot / region-architecture memories.

## Repo / git
- **Branch:** `wip/village2-and-f8-tickets` (NOT `feat/tower-core-loop` — that name in older docs is stale).
- **HEAD:** `8aa24c32` (2026-06-26). **Nothing pushed** this arc (no upstream tracking); all local.
- **Sole committer = CLI.** Push only on explicit owner OK after felt/regression verify.

## In-flight / unverified (do NOT treat as done)
- **HEAD `8aa24c32` = UNVERIFIED hero-priority structure sweep** (`ff.enemystructureaware`, default ON).
  Verify-capture showed **0 sweep acquires** (sweep stayed inert; hero-in-aggro gate suppressed it).
  NEXT (not done): skip-reason FlowTrace (hero-in-aggro vs no-structure-in-range) + headless capture of
  the no-hero-near-structure case BEFORE claiming fixed. **Do NOT push until proven.**
- Two **untracked `.cs`** awaiting triage: `Assets/Editor/CastlePlaceCrossing.cs`,
  `Assets/_Modules/Village/Hero/RumorBoardPanelBootstrap.cs`.
- Working tree also holds batchmode artifacts (terrain, NavMesh, Garrison_* scenes, prefabs, .meta) — generated.

## Combat / hero north star (CURRENT — supersedes all "Blink" canon)
- **ONE controllable hero (Knight, "Grom").** Everything else autonomous. Single-hero / knight-only flags ON.
- **Blink full-body rig is JUNKED** (owner hated it + bone-map spam). Player hero = a **single Tripo
  self-rigged model**, static armor, NO mesh-swap. *Any doc that says "Hero Rig = Blink full-body" is STALE.*
- **Animated real-time combat lives in the OVERWORLD** (isolated BattleArena): walk → engage a roaming
  rep → pop into an open-kite arena → Knight vs Tripo orc family → win returns home. **ATB is separate**
  (flat/static, single hero vs static enemies — NOT the animated battle).
- **Arena trio is OFF / gated** (descoped this arc). Lock-on combat = WO-512 (manual pick, camera framing,
  face+strafe). **9-zone battle HUD** is the owner's exact-vision layout.
- Roster = Tripo only (Heroes Knight/Ranger/Wizard; Enemies Orcs/Skeletons/Trolls × Mage/Tank/Warrior).
  **V1 = Knight + ORCS only.**

## World / scene
- **Home hub = `MainCastle_Hall`**; `OuterWorld` streams additively (`WorldSceneLoader`).
- **`Village2` = raid-target stronghold.** **`Village.unity` = ABANDONED** (corruption-cursed; never hand-edit).
- **Castle↔OuterWorld = four-side warp gates** (`f82e2b00`, rotation-generalized `RuntimeRegionGate`) —
  WARP by design (stacked navmeshes don't auto-connect). **Castle moat + 4 drawbridges** first-pass
  (`ff.castlemoat`). **Tree aura + tower glow** (`ff.hubambientvfx`).
- Region architecture north star = **hybrid gated regions** (navmesh-stitched low-poly scenes, natural/
  diegetic playable connectors, danger gradient, Elden-Ring drop-&-recover death). WO-453 was the original
  spec id; confirm against the region-architecture memory before citing.
- Reps spawn OuterWorld-side only (castle safe to shop). Roaming reps 8→6 + respawn.

## V1 economy / progression (wired this arc)
- **Echo workforce** wired: 1–4 echoes, silo+dump, wave-unlock, offline via real clock, **save v25**.
- **Village-tier upgrade wired** → unlocks the previously-frozen **WO-432 building upgrade tree**.
- Offline-farmed Wood/Iron persists + reaches upgrades (`BankYield`→`GrantSpendable`).
- **Store redesign** (WO-501: type filter, slim list, 3D preview, buy/sell+equip); **gear balance** (WO-500:
  27 weapons + 12 armor graded). **Offset Forge** offsets applied on weapon attach (WO-490/510).

## Monetization / distribution
- **LIVE on itch:** `denellestudios/defenders-of-the-realm-defend-the-tower` (channel `html5`).
- **Vercel = parked** — payload ~119 MB Brotli; needs a real art shrink, not a setting. itch stays web home.
- Honest market research in (`docs/PRODUCTS.md` + `docs/MARKET_RESEARCH.md`): 3 of 4 tool ideas = SKIP;
  the one real-margin play = packaging the AI-fleet methodology as a teardown/cohort, not a template pack.
- **Pi integration** spec'd: payment backend = **Cloudflare Worker** (`pi-backend`).

## Queued / captured (NOT built — deliberate specs)
- **WO-509** functional N/E/W moat seams + footprint-shrink (slider-tunable).
- **WO-513** coordinated orc family (gang/flank/surround).
- **WO-514** tower cap (perf + anti-boxing) + Population→Saved Echoes→SP + siege-AI (mobs target towers).
- WO-430 offline-garrison (reconciled ~65% new, queued). HUD glyph clarity.

## Process canon (unchanged, BINDING)
- Read-first every session: `SESSION_CANON_LOADER.md` + `docs/MASTER_CATALOG.md` + `docs/*ARCHITECTURE*`.
- **Instrument, don't guess** (§12 hard gate). **Ticket pipeline** QA→CLI→PO. **UI never writes code;
  CLI is sole committer.** Never hand-edit `.unity`. Stage by explicit path, never `git add -A`.
- **Notion "Work Orders" DB = live board** (data source `5f66b263-c732-4075-b94a-f5f4de9f8087`); WO spec
  files stay in repo. WO-numbering authority = `MASTER_PIPELINES_BACKLOG_2026-06-06.md`, not filesystem max.

## Known-stale reference docs (confirmed before the audit, fix in reconcile)
- `SESSION_CANON_LOADER.md` "Current State" — still says Hero Rig = Blink. **STALE.**
- `docs/HANDOVER.md` SESSION block — dated **06-19** (DO-NOT-PUSH-6-commits / WO-453 / Blink migration). **STALE.**
- `PIPELINE_STATE.md` "CURRENT STATE" block — dated **06-09** (WO-383, `feat/tower-core-loop`). **STALE.**
