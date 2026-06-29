# CANON GROUND TRUTH — 2026-06-28

> **Purpose:** the single anchor of *current reality*, derived ONLY from verified sources
> (HEAD commit arc, working tree, the auto-memory index, WO specs, and the combat-pivot canon)
> — NOT from assumptions. Every doc-audit agent checks files against THIS. If a doc contradicts a
> line here, the doc is STALE (unless it is a correctly-dated historical ledger, which is frozen-OK).
> If you believe a line *here* is wrong, FLAG it — do not silently trust it.
>
> Sourced 2026-06-28 from: `git rev-parse HEAD` (`7c05cd1b`), `Assets/_Modules/Core/State/SaveSchema.cs`
> (`CurrentVersion = 27`), the WO-560→584 spec arc, `docs/UI_BLINK_TEMPLATE_CANON.md`, and the
> combat-pivot / region-architecture / dungeon-consolidation memories.
> **Supersedes `CANON_GROUND_TRUTH_2026-06-26.md` (frozen snapshot).**

## Repo / git
- **Branch:** `wip/village2-and-f8-tickets` (NOT `feat/tower-core-loop` — that name in older docs is stale).
- **HEAD:** `7c05cd1b` (2026-06-28). **Nothing pushed** this arc (no upstream tracking); all local.
- **Sole committer = CLI.** Push only on explicit owner OK after felt/regression verify.

## Title / brand canon (WO-570, owner-ratified 2026-06-28)
- **gameTitle = "Echoes of Elarion"** — a chapter within the **"Defenders of the Realm"** series (subtitle).
- **Full title** = *Defenders of the Realm: Echoes of Elarion*. **Publisher** = DeNelle Studios.
- **Tagline = "Hold the last light."** (single canonical tagline; Spire/Chord/Lantern motifs RETIRED).

## Combat / hero north star (CURRENT — supersedes all "Blink" canon)
- **ONE controllable hero (Knight, "Grom").** Everything else autonomous. Single-hero / knight-only flags ON.
- **Blink full-body rig is JUNKED** (owner hated it + bone-map spam). Player hero = a **single Tripo
  self-rigged model**, static armor, NO mesh-swap. *Any doc that says "Hero Rig = Blink full-body" is STALE.*
- **Animated real-time combat lives in the OVERWORLD** (isolated BattleArena): walk → engage a roaming
  rep → pop into an open-kite arena → Knight vs Tripo orc family → win returns home. **ATB is separate**
  (flat/static, single hero vs static enemies — NOT the animated battle).
- **WO-584 dungeon/outpost/arena consolidation (READY, owner-ratified 2026-06-28)** = the current
  combat-space direction: **one space primitive, three skins, one warp entrance.** A WORLD prefab
  (cave / enemy-encampment) → **RegionGate WARP** (placeable anywhere, ~0 cost) → **RESOLVER**
  (`spaceType` → DungeonResolver / OutpostResolver) → **Arena-skinned space** (skin + spawn-set +
  ownership flag) → clear it via the verified real-time Arena loop → **ownership flips Enemy → PlayerCamp**
  (same space re-dresses in place). Replaces the flat ATB dungeon fight and closes the WO-453 zoning gap
  with **no cross-region navmesh/seam work** (each space is isolated; you *port* in). **`ff.atbdungeon` OFF.**
- Roster = Tripo only (Heroes Knight/Ranger/Wizard; Enemies Orcs/Skeletons/Trolls × Mage/Tank/Warrior).
  **V1 = Knight + ORCS only.** Lock-on combat = WO-512; **9-zone battle HUD** is the owner's exact layout.

## UI canon (WO-560 arc)
- **`docs/UI_BLINK_TEMPLATE_CANON.md` (BINDING)** — code-built uGUI; one master factory
  `BuildObsidianPanel(frameName)` renders the real Blink frame + returns header/body/medallion/footer
  drop-zones; screens DROP chrome-less content into zones + bind the model, never restyle.
  Blink survives ONLY as a UI re-skin kit (`BlinkChrome` flag), not the hero body.

## World / scene
- **Home hub = `MainCastle_Hall`**; `OuterWorld` streams additively (`WorldSceneLoader`).
- **`Village2` = raid-target stronghold.** **`Village.unity` = ABANDONED** (corruption-cursed; never hand-edit).
- **Castle↔OuterWorld = four-side warp gates** (rotation-generalized `RuntimeRegionGate`) — WARP by design
  (stacked navmeshes don't auto-connect). **Castle moat + 4 drawbridges** (`ff.castlemoat`). **Tree aura +
  tower glow** (`ff.hubambientvfx`).
- Region architecture north star = **hybrid gated regions** (navmesh-stitched low-poly scenes, natural/
  diegetic playable connectors, danger gradient, Elden-Ring drop-&-recover death).
- Reps spawn OuterWorld-side only (castle safe to shop). Roaming reps respawn.

## V1 economy / progression (wired)
- **Echo workforce** wired: silo+dump, wave-unlock, offline via real clock. **Save schema = v27.**
  *(Code today = 1–4 echoes (cap ≤4); the memory `echo-workforce-drag-drop` cap-5 (3+2) design is NOT
  built — flag for the next echo WO; code is authoritative.)*
- **Village-tier upgrade wired** → unlocks the **WO-432 building upgrade tree** (WC3-style perks, Heart gate).
- Offline-farmed Wood/Iron persists + reaches upgrades (`BankYield`→`GrantSpendable`).
- **Store redesign** (WO-501); **gear balance** (WO-500: 27 weapons + 12 armor). **Offset Forge** offsets
  applied on weapon attach (WO-490/510). Accessory equip persistence (v26: ring/amulet). Wall-mount
  defense seating (v27: `worldY` + `wallMounted`).

## Monetization / distribution
- **LIVE on itch:** `denellestudios/defenders-of-the-realm-defend-the-tower` (channel `html5`).
- **Vercel = parked** — payload too large; needs a real art shrink, not a setting. itch stays web home.
- **Pi integration** spec'd: payment backend = **Cloudflare Worker** (`pi-backend`).

## Process canon (unchanged, BINDING)
- Read-first every session: `SESSION_CANON_LOADER.md` + `docs/MASTER_CATALOG.md` + `docs/*ARCHITECTURE*`.
- **Instrument, don't guess** (§12 hard gate). **Ticket pipeline** QA→CLI→PO. **UI never writes code;
  CLI is sole committer.** Never hand-edit `.unity`. Stage by explicit path, never `git add -A`.
- **Notion "Work Orders" DB = live board** (data source `5f66b263-c732-4075-b94a-f5f4de9f8087`); WO spec
  files stay in repo. **WO specs now run through 584** (WO-numbering authority =
  `MASTER_PIPELINES_BACKLOG_2026-06-06.md` + `CLI_LANES_WO_NUMBERS.md`, not the filesystem max).
