# RESULT — WO-1108 Echo simplification: auto-harvest, count-driven passive repair, escort-then-vanish

**Date:** 2026-08-16  **Seat:** CLI (Lane A `c72d276db`, Lane B `7fcb49a1b`, WO-1108b `38ed0d881`)
**Status:** DONE — pending PO felt-verify

## Lane A — repair rides the ROSTER COUNT, not an assignment (`c72d276db`)

Owner: *"they automatically just harvests. You choose what it chooses to harvest, and the number of pets
that we have just passively takes towards healing"* → *"Get it done."*

- **Read at source first, and it changed the job:** auto-harvest with a player-picked resource was
  **ALREADY BUILT end to end** (accrual every frame in `EchoService.Update`, 5-resource picker, per-Echo
  weights). Only repair was new, and it is one function — `RepairFractionsPerSecond()` loses the
  `lane == Repair` filter and sums every owned Echo. `EchoRepairService` needed no logic change.
- **D3 — the silent 6×, caught and priced.** `repairFractionPerHour` was a CODE DEFAULT (`2f`) absent
  from `echoes-balance.json`. Passive across a 6-Echo roster = 6 × 2.04 = 12.24/h. Anchored at
  full-roster parity instead: 2.04/6 = 0.34 → authored **0.35**, giving 2.14/h (+5% vs the felt rate).
  Now in BOTH json copies with the arithmetic in `_authoringNotes`; the code default moved 2f → 0.35f so
  an absent file cannot reintroduce the 6×. Passive repair is now FREE (no Echo gives up its harvest
  assignment for it); early game is deliberately slower per-Echo. **Tuning handle: each +0.1 is about
  +0.61/h at full roster.**
- **Read-migration, no schema bump:** a stored `repair:N` normalizes to the Harvest lane and resolves to
  that Echo's AFFINITY resource at the same level — never idle (which would have silently zeroed that
  Echo's yield) and never a crash. `AssignRepair` survives as a LOUD always-false refusal.
  `EchoRepairStatus.NoneAssigned` was unreachable once every Echo repairs; replaced with `NoEchoes`.
- **CANON CORRECTION shipped in the same commit (§15):** `CLAUDE.md` §7 claimed a matching affinity
  **"DOUBLES the yield"**. FALSE against live tuning — the match bonus is **ADDITIVE** (+0.03 on a +0.02
  base), ~+3% absolute, exactly the owner's own "+5% not 55%" ruling in `echoes-balance.json`'s
  `_authoringNotes`. A seat implementing the canon sentence literally would have shipped a ~20× buff.
  "Never a lock" is intact.

## Lane B — the Echo walks you to the gate, then it is GONE (`7fcb49a1b`)

Owner: *"it takes you to the gate, gives you your dialogue, then it disappears. The only time it
reappears is after your battle."*

- The escort already existed (tutorial beat 2/8 + `PetHeroLeash.SetLeadTarget`). What did not exist
  ANYWHERE was a **despawn path** — no pet in this game had ever been removed from the world.
  `PetDeployer` gains `DespawnEcho` / `DespawnAllEchoBodies`.
- **Teardown ORDER is the whole point:** leash disabled first (so the static enabled-leash census
  decrements exactly once), then harvester, then `ClearLeadTarget`. A body dying mid-lead would otherwise
  strand a static anchor and make every later `SetLeadTarget` warn "ZERO enabled PetHeroLeash" forever.
  A torn-id set guards `Destroy` being deferred to end-of-frame.
- **ONE APPEARANCE OWNER, by RE-POINTING not adding.** `EchoAutoDeployTrigger` was a SECOND owner and
  contradicted the ruling twice (it summoned AS the battle began, and its own header said the Echo "is
  never despawned here"). Its `Fire()` no longer summons — it only marks that the player entered the
  fight, so the ordering (entered → resolved → REAPPEAR) is provable. The WO-360 golden flourish MOVED to
  the reappearance. New `EchoWorldPresence` owns all three transitions; `EchoPresenceWatcher` fires the
  return on the `BattleLock` true→false edge, so no battle system needed a new callback and it does not
  care whether ATB or Arena resolved.
- **Deleted `TutorialFlow.EnsureGuidePetDeployer`** — a private FOURTH spelling of the same self-heal,
  which its own comment had already flagged.
- **`PetTaskController` husk RETIRED:** its rival `WallRepairController.RepairAll()` loop raced Lane A's
  `EchoRepairService` over the same walls and the same construction wallet on its own cadence.
  ⚠ It was DEADER than the spec said — `SetTask` has ZERO callers in the tree; only `PetTaskInstaller`
  bolting the component onto every pet each second kept it alive. Installer deleted; `SetTask(Repair)`
  now refuses loudly. **The TYPE stays** because `EchoEngageDialogueRegression` pins it by reflection —
  deleting it would have broken a suite.

## WO-1108b — containers climb to SIX levels, 1k doubling to 32k (`38ed0d881`)

Owner: *"set 6 levels and each level adds1k then next add 2k next 4k next 8k 16k 32k"*, reading confirmed.

- Data, not a new system: `storageCapacity` 500 → 1000, multipliers `[1,2,3]` → `[1,2,4,8,16,32]`,
  `maxLevel` 3 → 6. A maxed container takes that resource's store 2500 → 34000. Both JSON copies
  byte-identical.
- Costs: each step costs 2× the previous and grants 2× the capacity, seeded at 3× the row's build cost.
  **Wood + iron only** (WO-947 — containers are regular structures).
- **TIME DELIBERATELY NOT AUTHORED:** `StartUpgrade` derives tier as `targetLevel-2`, so the existing
  curve yields 40 s / 2 m / 6 m / 18 m / 55 m by itself. Authoring it would have created a second source
  of truth.
- ⚠ **A regression would have rejected this outright.** `BuildEconomyRegression` hard-failed any row with
  `maxLevel > 3`, and that ceiling was hardcoded in **EIGHT** places (`BuildModeController`,
  `StructureCardVM`, three suites, an EditMode test, and `StorageCapsCatalog`'s fallback array — a
  deleted file would have silently capped at 3×). All now read one constant, `RepoProps.MaxStructureLevel`.
  **Deliberately NOT raised:** the tower/wall tier STAT and ACCENT tables, which have 3 rungs by design.
- **Save-compat, no bump:** `BaseLayoutLoader` floors level at 1 with NO upper clamp, and capacity is
  never persisted — it is derived from `BaseLayout` level on every read.

## Owner decisions left open

- ⚠ **TWO OWNER RULINGS NOW CONFLICT, flagged not buried.** 2026-08-04 ruled the base store fills first
  so pallets drain LAST; this ladder makes a container outgrow the base store from **LEVEL 2**. The
  order-intent case now hard-fails only if that happens at level 1 (an inversion visible on build day)
  and NOTES the flip above. Restoring the old form would fail the build on her own numbers; preserving it
  needs `baseCap > 32000`, which makes containers pointless. **The real fix, if she wants it, is a
  PRESENTATION rule** (base fills last regardless of capacity).
- ⚠ **Tuning note, intentional but not instructed:** foundry/silo's final step costs 2880 wood, ABOVE the
  2000 base cap — so those top upgrades require a level-2+ lumberyard first. CoC-style interdependency.
- **Repair rate is hers to tune** from the 0.35 anchor (see Lane A).

## Oracles

`EchoWorldPresenceRegression` → `ECHO_WORLD_PRESENCE_OK` drives the REAL state machine in-process (body
present during escort, gone after, back exactly once, second battle-resolve changes nothing), plus
`[one-owner]` which scans every `.cs` for a `SummonAt` call and fails if a second owner grows back.
`EchoResourcePickerRegression` group 6 INVERTS — the repair chip must be gone, with a reflection guard on
`RepairTaskChip` / `AssignRepair`. `EchoSpecializationRegression` pins 0.35 **and** that the key is
physically present in the json, plus a new case: EchoCount 5 → 6 with byte-identical assignments must
RAISE the rate. New `[storage-ladder-6]` case pins the curve per container through the real
`CapacityAtLevel`, the 34000 total, and that all five steps are priced, escalating and crystal/food-free.
