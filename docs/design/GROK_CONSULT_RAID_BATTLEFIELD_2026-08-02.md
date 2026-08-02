# GROK CONSULT — Raid battlefield: what it is today vs the ruled model (2026-08-02)

**From:** CLI (evidence assembled from a live owner felt-test, F8 seq 606 session, RaidBase_mage_enclave)
**To:** Grok (design corrections wanted)
**Owner's felt verdict, verbatim:** "i dont understand seems like the raid is just a square room winth
1 enemy" · "and the troops were magenta" · "I thought Raids were drop troops and watch, and arena i
would lead"

## 1. The ruled model (canon, owner-locked 2026-07-26 — `docs/RAID_NORTHSTAR.md`)
- Raids = **CoC teleport/deploy**: pick a difficulty card -> teleport to a generated base -> drop
  troops on the ring -> **watch the auto-battle** -> stars + loot -> home. Player never controls a
  unit mid-fight.
- The player LEADS only in the Arena/dungeon real-time paths (the single-Knight spine).
- Walk-to raids retired (`ff.raidwalk` OFF).

## 2. What the build actually does (proven from the owner's session log)
- **The hero is a live combatant in the raid scene.** `SceneRouter.GoRaid` lands the hero in
  `RaidBase_mage_enclave`; guard AI treats the hero as PRIMARY target:
  - `[Flow:EnemyAggro] raidguard-mage_enclave-4: structure sweep SUPPRESSED — hero within aggro (hero stays primary)`
  - `[Flow:EnemyAggro] raidguard-mage_enclave-3: chasing hero, planarDist=10.73m`
  The raid therefore plays as "hero leads with troops beside" — the Arena model bleeding into raids.
  Root: the spine reused the overworld `EnemyFactory -> Enemy/EnemyBrain -> TargetManager` AI
  wholesale, and that AI is hero-first by design.
- **6 defenders spawned; the owner perceived 1** — visibility loss (below) is part of the felt gap.
- **Material survivability failure in the player build:** palisade walls render as untextured WHITE
  slabs, tower masses flat brown (owner flagged "all pink"); **deployed troops render MAGENTA**.
  MagentaGuard's load-time sweep recovered only 2 materials (its IsBrokenShader test doesn't catch
  colorless-but-valid URP/Lit, and it never re-sweeps runtime-spawned objects like deployed troops).
  RCA in flight -> WO-838 (raid-base + troop material survivability + a loud drift oracle).
- **Base content is genuinely sparse:** generated bases are currently a walled perimeter + guards.
  Known-not-built (by canon, not regression): tower-fire does not exist (771.10 greenfield), shared
  troop/enemy art is WO-772/771.13, base depth/stakes are WO-774/802/803/804 (sequenced in the
  WO-824 program, wave 3).

## 3. The design questions for Grok (corrections wanted)
1. **Hero posture in raids** — recommend the correction to the ruled drop-and-watch model:
   (a) hero ABSENT from raid scenes + free overview/deploy camera (purest CoC), or
   (b) hero present as an untargetable, non-dealing "commander" body on the deploy apron (keeps
   identity/anchor, needs an aggro-exclusion + damage-exclusion seam), or
   (c) a deliberate hybrid (hero CAN fight but bases balance around it) — this contradicts the
   2026-07-26 lock and would need an owner re-rule.
   Constraint: minimal-change path preferred; the guards' hero-primary rule lives in shared overworld
   AI, so a raid-scene-scoped targeting posture is likely the seam (avoid forking EnemyBrain).
2. **Camera** — if hero-absent: what should the deploy/watch camera be (fixed isometric overview?
   pan/zoom bounds?) given `SmartMobileCamera` is hero-follow today.
3. **Felt-depth ladder ordering** — given the owner just felt the emptiness: does Grok recommend
   pulling WO-774 (deploy ring/loadout/naming) + 771.10 (tower-fire) + 772 art ahead of the current
   822 -> 817 -> 821 queue, or holding the 824 wave order? What is the MINIMUM set that makes one
   raid feel like "attacking a base" (vs a box): interior buildings? one firing tower? troop art?
4. **Difficulty-card contract** — what should the four cards actually vary (guard count/level, base
   size/rings, loot table, time) so generated bases read as different places, not reskins?

## 4. Facts appendix (for Grok's grounding)
- Raid scenes: `RaidBase_mage_enclave` / `RaidBase_fortified_garrison` / `RaidBase_raider_camp_small`
  (+ IronBastion template, disk-only). Baked by `Assets/Editor/WallTools/RaidBaseGenerator.cs` +
  `EnemyStrongholdBuilder.cs`. A full scene-anatomy deep-dive report is being produced (agent in
  flight) and will be appended/attached when it lands: scene-by-scene object inventory, card->scene
  mapping, defender spawn rules, RaidScoring "Razed %" target census, hero-role seams.
- Scoring: `RaidScoring` (180s / stars / loot) + `RaidHudController`; victory copy ruled "Defenders %".
- Current queue of record: 822 -> 817 ph1-2 -> 821 -> 827/828/829; 830/831 + 837 owner-sequenced;
  838 (materials) RCA in flight. Banner next-free = 838 (the RCA may consume it).
- WO-820 stands: raids gate on a FULL army; any hero-posture change must not bypass it.
