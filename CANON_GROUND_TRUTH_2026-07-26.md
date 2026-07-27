# CANON GROUND TRUTH — 2026-07-26 (Sunday housekeeping: dungeon+raid felt-test wave)

> **LIVE ANCHOR (2026-07-26).** Records the reality after a large felt-test fix wave landed on
> `wip/village2-and-f8-tickets` (dungeon functional-loop fixes, dungeon movement/camera, enemies-in-castle
> lockdown, towers-through-walls, MagentaGuard Android, loading overlay, vendor NPCs, gate-traversal) **plus**
> the firming of the dungeon/raid/enemy/queue work-order set (WO-770/771/772/773).
> **Supersedes `CANON_GROUND_TRUTH_2026-07-22.md`** (bannered). If a doc contradicts a line here, the doc is stale.
>
> **This anchor is a DELTA over the 07-22 anchor.** The 07-22 anchor's §5 code-verified module digests (Core/Save,
> Hero, Village systems, Echo, Enemies/World, Combat, HUD, Data, Economy, Dungeons, Editor tools, Web/Dialogue/Audio),
> its §6 catalog-drift ledger, §7 comment-lie registry, and §8 landmines **remain the deep reference** except where a
> line below changes them. Read order: this → `CANON_GROUND_TRUTH_2026-07-22.md` (deep module state) → `KEY_FACTS.md`
> → `SESSION_CANON_LOADER.md` → `docs/HANDOVER.md` → `docs/MASTER_CATALOG.md`.

---

## 0. STAR NORTH (unchanged from 07-22)
- **Pi Hackathon WON** (2026-07-17). The "July-31 deadline / build mode IS the demo" framing is **RETIRED**;
  roadmap is OPEN. Product: **"Echoes of Elarion"** (Chapter One) in **"Defenders of the Realm"**; tagline
  **"Hold the last light."** Mobile-web first (Pi Browser), portrait; desktop = dev proxy.
- V1 = **one controllable Knight "Grom"** + isolated real-time `BattleArena` combat + player-built city.
- Bar = the **ten-year-old test** on a phone. Headless proves binding; only the owner's hands prove feel.
- Economy: **V1 ships ZERO crypto**; monetization = sell time, never power.
- Spine (the Rosetta stone): **you control exactly ONE thing — the hero; everything else is AUTONOMOUS.**

---

## 1. REPO / GIT / GATES (verified this session)
- **Branch `wip/village2-and-f8-tickets`. HEAD = `7dec0e07`** (2026-07-26 18:01), **local == origin (0/0) —
  the felt-test wave IS pushed.** This is a change from 07-22's "push HELD": the dungeon+raid felt-test wave
  was committed AND pushed to `origin/wip`. **Prod still UNTOUCHED** (owner promotes separately).
- Save schema **v35** (updated — WO-773 landed after this doc's initial HEAD: `SaveSchema.CurrentVersion=35`,
  `SaveMigrator.MigrateToV35` appends the `ObsidianQueue` multi-channel job state and folds the legacy
  `BuildJobs`/`PendingBuilds`/`BuildingCooldowns` timers into the Builder channel. Earlier this doc said
  **v34/no-new-fields** — STALE, corrected here. WO-771.1b's raid-schema migration remains SPEC; CLI
  reconciles it onto v35 when 771.1b is implemented, not the read-only-tree's "v10/v11" numbering).
- **Gates:** compile-green across the wave (each felt-test commit built). Full `DataRegression.RunAll`
  re-baseline was **not re-run as part of this housekeeping doc pass** — the 07-22 baseline (`REGRESSION_OK`,
  16 P1 suites, 0 reds) is the last certified full sweep; re-run before the next build ship.
- Working tree at housekeeping start: churn only (`.meta`/`.mat` re-import + gitignored art packs); no `.cs`
  staged by this doc-only session.

---

## 2. DUNGEONS — now a FUNCTIONAL END-TO-END LOOP (the headline change)

The dungeon subsystem — long "always overlooked" — is now a real loop:
**Village → door → dungeon → explore / read lore / fight (real win OR loss) → settle → leave → Village.**

**Shipped this session (WO-770 sub-orders + felt-test fixes):**
- **770.1 — always-open exit + boss-gated back-door** (`8ccacd9d`): `DungeonExit` interactable placed;
  `ExitToVillage()` now has real callers (was 0). Fixes the roach-motel (D1).
- **770.2 — return to the CURRENT dungeon** (`4f4545c8`): `EncounterTrigger.ReturnScene` no longer hardcoded
  to the Cottage; a fight in dungeon X returns to X (D3).
- **770.3 — real victory/defeat carrier** (`e51628e0`): `SceneRouter.PendingBattle.LastOutcome` carries the
  ATB result; a LOST dungeon fight ends the run and returns to the Village (no free retry). Kills the
  "always victory" assumption (D4).
- **770.3b — real-time encounter settlement + defeat parity** (`53e1b9e4`): the default dungeon path is the
  real-time `BattleArena` (`ff.dungeonrealtime` ON) which fires fire-and-forget with no scene reload — so the
  old scene-reload-coupled settle never ran. Now the Dungeons module subscribes to `BattleArena.OnBattleEnded`
  and routes BOTH paths through one shared `SettleEncounter(victory, wasBoss)` (`_inCombat` clears,
  `MarkBossDefeated` fires on a real-time boss win, loot grants, a loss ends the run). Fixes the "combat lock
  never releases / dungeon goes permanently quiet after the first fight" seam.
- **770.4 — readable lore stones** (`15fb8ca1`): `LoreStone.Read()` wired to proximity+interact; a code-built
  (NOT uxml) lore reading modal shows the authored `lore-fragments.json` copy (D6).
- **770.7 — player-feedback toast layer + live Bryn dialogue** (`101fa983`): checkpoint/crafting toasts and
  Bryn's `FirstMeet[]`/`Idle[]` lines now surface (were 0 subscribers). Fixes D13/D14.
- **770.9 — stale-read fix** (`fd45e2f0`): `DungeonRuntimeState.OnEnable` clears run identity + progress lists,
  removing the stale-read window (D11).
- **Dungeon movement + camera + Bryn** (`82e1f3a4`, `f42e6f7e`): `DungeonHero` is the **sole mover** (no
  competing controller); dungeon camera raised to a taller framing; exit interaction restored; **Bryn's
  pill-hide** now covers a skinned baked body (the green capsule no longer shows through).
- **Dungeon camera = FIRST-PERSON by default** (`ff.dungeonfpv` **default-ON**, owner 2026-07-26):
  `DeNelle.Dungeons.DungeonCameraRig` is a full FPV (independent yaw+pitch look layer, pitch-clamped ±70;
  temporary over-the-shoulder via `SetCombatFraming`). An architect chose FPV traversal over raising the
  ~4u ceiling. Set PlayerPrefs `ff.dungeonfpv=0` to revert to the fixed over-the-shoulder rig. **Open
  follow-up (PAIN_POINTS §4):** keep FPV only if felt-tested — else default third-person until motion-sick
  risk is closed; placeholder hero vitals (`_heroBaselineHp=120`/`mana=60`) still to be wired (WO-770.10).

**Dungeon backlog (NOT built — remain in WO-770):**
- **770.5** — consolidate the two Village→Dungeon entry systems (retire `DungeonPortal`, keep the
  data-driven `DungeonEntrance` ring). OWNER-GATED (village re-bake). Canonical-verify the seam first.
- **770.6** — Folk's Granary = the first-torch tutorial dungeon (gather → craft first torch → light the dark →
  exit → leads into Healer's Cottage). Reuses existing Lantern+crafting; CODE + OWNER-GATED.
- **770.8** — content assets: KayKit Dungeon geometry (zip in Downloads), `echoes-beneath-elarion.mp3`,
  Newtonsoft in `DeNelle.Dungeons.asmdef`. CONTENT/OWNER-gated (asmdef item is code-only).
- **770.10** — hero integration: real vitals (replace `_heroBaselineHp=120`/`mana=60`) + walk animation on
  the shared WO-772/771.13 rig. CODE + CONTENT.
- **770.11** — dungeon enemy-placement system (visible stationed/roaming enemies per room, data-driven).
  Depends on WO-772 + WO-771.13. CODE + OWNER-GATED.

**Combat routing (unchanged truth, reconfirmed):** dungeons route into the real-time `BattleArena`
(`ff.dungeonrealtime` default TRUE). **`ff.atbdungeon` does not exist** — the 07-22 §7 comment-lie stands.
ATB remains built-but-dormant.

---

## 3. RAID LOOP — LOCKED to Teleport / Deploy (owner ruling 2026-07-26)

The owner picked the **teleport/deploy** raid loop (the COC model). **Walk-to is RETIRED as the raid loop**
(its `EnemyOutpost`s may return later as a light overworld "patrol" side-activity — not the raid loop).

> **raid canon = `docs/RAID_NORTHSTAR.md`** (the single source of truth for the raid loop + the V1/V2 split;
> supersedes the parallel fantasies in `RAID_PILLAR_VISION.md` / `ARENA_SOLUTION.md`).

- **Do first, unconditionally (when raid work starts):** set `ff.overworldencounter=0` (a leftover preview
  default) and `ff.raidwalk` OFF — otherwise neither loop spawns and raids look broken out of the box.
- **WO-771 (v2) is the build plan** — rebuilt after an adversarial implementability review. Verified reuse
  vs new is pinned in the WO: `IDamageableStructure` lives in **`DeNelle.Village`** (must move to Core,
  771.0); **no tower-fire code exists** (greenfield, 771.10); troops reuse `Pet.cs` shape with physics
  acquisition DISABLED (interpolate to sim ticks). **Reuse `RaidBaseGenerator` + `EnemyFactory→Enemy→
  TargetManager` real-time combat — do NOT rebuild the base authoring or the auto-battle.**
- **V1 vs V2 split (owner-recommended):** V1 = PvE against **generated** bases, reuse the existing real-time
  combat, "replay" = re-watch from the recorded deploy log; **skip the deterministic sim.** V2 = rewarded PvP
  adds the deterministic fixed-point sim (771.3), async player-base snapshots + matchmaking (771.2/771.7),
  server byte-exact anti-cheat.
- **V1 spine — CORRECTED 2026-07-26: the spine ALREADY EXISTS end-to-end and is REACHABLE** (flags flipped:
  `ff.raidwalk` OFF, `ff.barracks` ON, `ff.buildtimers` ON). The earlier "nothing built yet — all SPEC"
  line was STALE — code is ahead of the docs. Verified in-tree (paths confirmed at implement time):
  train (`TroopTrainingVM`/`TroopTrainingPanel`, `Village/Hero/`) → `ArmyStorage` (`Core/State/`) →
  `RaidSelectionScreen`/`RaidSelectionVM` → `RaidDeployScreen`/`RaidDeployVM` (`Village/Hero/`) →
  `SceneRouter.GoRaid(scene)` → the four `RaidBase_*` scenes (IronBastion / fortified_garrison /
  mage_enclave / raider_camp_small) → `RaidDeployController` tray + `TroopDeployer.SpawnFromArmy`
  (`Village/Troops/`) → `TroopController` auto-fight → `RaidScoring` (180s/stars/loot) + `RaidHudController`
  → claim. **Full beat→class map + the P0/P1/P1.5/P2 ladder = `docs/RAID_NORTHSTAR.md` §2A.**
- **Raid V1 UX polish is IN FLIGHT (WO-774, P0):** loadout handoff (`SceneRouter.GoRaid` still takes only a
  scene name — no `RaidParams`/pending loadout bag, so the field tray arms the FULL roster), Army/Deploy
  naming split, deploy ring, victory/star copy = "Defenders %" not "base destroyed", Train-channel queue UI.
  This is polish + clarity, **not a rebuild.** Sub-orders that remain SPEC on top of the shipped spine:
  771.1b (raid-schema migration onto v35), 771.10 (towers, if the generated base's don't already fire),
  771.13 + WO-772 (shared troop/enemy art). 771.9 (barracks/train-channel integration) is in flight.

---

## 4. CROSS-CUTTING WORK-ORDERS FIRMED THIS SESSION

- **WO-772 — shared enemy system** (classes / families / equippable armor+weapons). One `EnemyResolver`
  feeds BOTH dungeon placement (770.11) and raid rosters/art (771.13). Also fixes the **generic-skeleton
  spawn bug** (enemy ids don't resolve to distinct defs+models). **BLOCKED on owner ratification** — the WO
  operationalizes `docs/enemy-codex.md`, which is a "review-and-approve before implementation" gate; the owner
  ratifies the roster (or a subset) first. Families: **The Hollow Ones** (undead/skeletons, KayKit Skeletons
  1.1) + **The Wildlands** (living) + 8 set-piece bosses (2 canon-locked). **UPDATE 2026-07-26: UNBLOCKED —
  Hollow Ones + mini-boss names RATIFIED, Wildlands DEFERRED (`docs/PAIN_POINTS_2026-07-26.md` §1.1).
  `EnemyResolver` (`Core/Enemies/EnemyResolver.cs`, + `Editor/Regression/EnemyResolverRegression.cs` +
  `Tests/PlayMode/EnemyResolverSpawnTests.cs`) is present and IN FLIGHT** — do NOT assert it done; the
  barracks-catalog-structure (Barracks as an upgradable placeable building, PAIN_POINTS §3.3) is also
  in flight, not shipped.
- **WO-773 — common "Obsidian" job queue — SHIPPED (2026-07-26, schema v35).** Landed as a **multi-channel**
  queue (Builder / Train / Research channels — per the PAIN_POINTS ruling, NOT the old single-FIFO), so
  training a troop no longer competes with a wall upgrade. Files: `Core/Jobs/ObsidianQueueState.cs`,
  `ObsidianQueueEngine.cs`, `JobKind.cs`, `IJobEffect.cs`; `SaveMigrator.MigrateToV35` appends
  `ObsidianQueue` and folds the legacy `BuildJobs`/`PendingBuilds`/`BuildingCooldowns` timers into the
  Builder channel (idempotent, no-loss); the on-screen `ObsidianQueueHud` + `BuildTimerService` (now
  "the common Obsidian multi-channel work queue" per its own header) surface it. Player copy = "Builders" /
  "Training" / "Research" — never "Obsidian queue".
- **Validation sign-off:** `docs/qa/dungeon-raid-validation-2026-07-26.md` certifies the WO-770/771/772/773
  set as handoff-ready — all D1–D16 findings owned, COC loop coverage complete, determinism model sound,
  every code citation re-verified. The only waits are owner-side asset staging + balancing constants.

---

## 5. OTHER FELT-TEST FIXES SHIPPED THIS SESSION (non-dungeon)

- **Enemies stay out of the castle during tutorial + battle mode triggers** (`e05f92f7`): `ZoneManager`
  classification (owner cites 52/52) + a battle-mode `BattleLock` so wave enemies don't wander the hub
  during the tutorial/onboarding window.
- **Towers no longer shoot through walls** (`2cb3c40d`): a **Structure layer** + line-of-sight check gates
  tower fire; a wall between tower and target blocks the shot.
- **MagentaGuard catches Android compile-failed shaders** (`386a932f`): an `isSupported` check catches the
  purple/white slab family that appears when a shader fails to compile on Android (not just the URP/Standard
  swap family already guarded).
- **Loading overlay + standard loading bar** (`4edf8dcc`, `7dec0e07`): a loading overlay covers the
  founding → hub load, using a standard loading bar (owner felt-test).
- **Gate-traversal teleport disabled** (`8c35332f`): you walk through the arch now — no teleport on gate
  touch.
- **Collector buildings get vendor NPCs** (`804a02a2`): Lumbermill / Farm / Forge collector buildings now
  spawn a vendor NPC (Lever 1 in progress per the owner).
- **Alchemy recipe list scroll-fix** (`8ca95735`): recipe rows no longer overlap/clip.

---

## 6. WO NUMBERING (reconciled this session)

- **Next-free WO = 774.** The `CLI_LANES_WO_NUMBERS.md` banner said next-free = 761 (2026-07-24), but
  **761–773 are all now consumed:** 761 (structure fire), 762 (builder queue), 763 (Wisdom earned),
  764 (hub Y-height), 765 (capture Default Town), 766 (Seeker wallet), 767 (texture caps),
  768 (thin-client migration), 769 (Firebase auth) all have `WorkOrders/WORK_ORDER_76*.md` files;
  **770 (dungeon functional), 771 (raid), 772 (enemy system), 773 (Obsidian queue)** live as firmed specs in
  `docs/qa/`. Mint the next new WO as **774** and bump the banner in the same edit.
- The dungeon/raid WOs use **decimal sub-orders** (770.1–770.11, 771.0–771.14) — those are sub-tasks of the
  parent number, not new WO numbers.

---

## 7. STILL-STALE / FOLLOW-UPS (carried from 07-22 + new)

- **`docs/MASTER_CATALOG/<area>.md` sections remain dated 2026-06-12** — the 07-22 §6 catalog-drift ledger +
  §7 comment-lie registry are the fix-list; still queued as a housekeeping WO (mint 774+). The
  `MASTER_CATALOG/misc-modules.md` dungeon section is now **doubly stale** (predates the whole RoomForge +
  770-wave functional loop).
- **CS-1 (real bug, still open):** equipped ring/amulet (`equippedRingId`/`AmuletId`) declared + migrator-
  seeded (v26) but no `GameState` field / no Snapshot-Apply → reset on reload. Needs a ticket.
- **07-22 §8 landmines unchanged** (Echo lanes 3-of-4 stub, dual-wallet spend asymmetry, orc-raider SSOT
  incomplete, IronScrap faucet-no-drain, HeroPortraits folder absent, audio 5-group mixer stub, deploy-chain
  `CHAIN_DONE`-on-failure). None were addressed this session.
- **Full DataRegression re-baseline** not re-run this housekeeping pass — re-run `DataRegression.RunAll`
  before the next build ship to confirm the felt-test wave held `REGRESSION_OK`.
- **PM pain-points + Tier-0 rulings (2026-07-26):** `docs/PAIN_POINTS_2026-07-26.md` — enemy-codex
  **Hollow Ones APPROVED / Wildlands DEFERRED**; KayKit pack-travel policy; raid V1 stakes (casualties +
  stars, no sim-first); multi-channel job queue (Builder/Train/Research); player copy never says
  “Obsidian queue.” Unblocks WO-772 Phase 1 without full Mystery Monthly packs.
- **WO-772 Phase 1 UNBLOCKED** (Hollow Ones only — see PAIN_POINTS §1.1). Wildlands / Mystery Monthly
  remain Phase 2 content; do not block dungeon placement or Hollow art on deferred packs.

---
*Live anchor 2026-07-26. Dungeon loop functional (FPV default-on); raid V1 spine EXISTS end-to-end and is
REACHABLE (flags flipped) — polish/UX in flight (WO-774), not a rebuild; WO-773 multi-channel job queue
SHIPPED (schema v35); EnemyResolver + barracks-catalog-structure IN FLIGHT. Raid product-truth =
`docs/RAID_NORTHSTAR.md` §2A. Deep module state = the 07-22 anchor. 07-22 SUPERSEDED (bannered).
Load-bearing set refreshed same-breath (§15). See `docs/qa/SUNDAY_STATUS_2026-07-26.md` for the ticket table.*
