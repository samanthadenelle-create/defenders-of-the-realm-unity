# WORK ORDER 949 — Death UX: respawn IN TOWN, starter potions, and teach the cost of dying

**Status:** READY TO IMPLEMENT (PARTIAL - deliverables 1 and 2 LANDED + gated 2026-08-10; deliverable 3 (teach the cost of dying) is NOT built, see the 2026-08-10 note at the bottom)
**Minted:** 2026-08-10 (CLI seat, main line — banner bumped 949 → 950 in the same edit)
**Silo:** Village/Hero (respawn flow) + founding kit data + one FTUE one-shot (composes with WO-1012)
**Origin:** owner F8s 2026-08-10, verbatim:
- seq (10:20:12): *"On Death I should respawn in town not where I died."*
- seq (10:22:11): *"here is where I respawn. Can we start the user with some potions, and explain to
  them consequences of dying with resources"*
Adjacent felt evidence, same session: her death happened in the BattleArena warp-space; the shake at
death is the death-pin fight (fix landed, unbuilt at capture time); "here is where I shake" /
"here is where I respawn" markers bracket the felt sequence.

---

## 1. Three deliverables

1. **RULING — respawn location = TOWN.** On hero death (any context: town wave, arena, outpost),
   respawn places the hero at the town anchor (the same hub spawn the arena's return pose targets),
   never at the death spot. Verify against the shipped "respawn now MOVES you" behavior (2026-08-02)
   and the arena defeat path (`BattleArena` return + `HeroHealth.Respawn`) — the felt report says the
   current result reads as "where I died," so capture a run first and cite the line that shows where
   respawn actually lands before changing it (§12).
2. **Starter potions.** The founding kit grants a small number of healing potions (count/type =
   owner-tunable data; propose 2-3 minor healing potions in the founding grant, same grant seam the
   FTUE already uses). ⚠ Known adjacent defect (2026-08-06 session): the potion button self-disables
   at zero — with starter potions granted, verify the button lives. ⚠ THERE IS NO APOTHECARY
   reachable (no catalog row) — starter potions must not point the player at an unreachable refill;
   the nudge chain (WO-1012 2c-bis) owns "where to get more" once the shop path exists.
3. **Teach the cost of dying.** One FTUE one-shot (WO-1012 kit/schema — do not fork) at the first
   death/respawn: explain what dying costs with resources on hand. ⚠ DISCOVERY FIRST: verify at
   source what dying actually costs today (resource drop? nothing?). If death currently has NO
   resource consequence, this is a DESIGN GAP — report it and propose the consequence for the owner
   to pin rather than inventing one (creative/balance pick is hers).

## 2. What NOT to touch

The death-pin rebase fix (in tree, uncommitted — Hero lane); the arena's return-pose nets; the
Onboarded gate; potion crafting/apothecary scope (parked); no UXML.

---

## 2026-08-10 - PARTIAL LANDING (CLI seat, gated)

**2 of 3 deliverables landed and are gate-green.**

**1. Respawn location = TOWN - LANDED.** The gap was the ARENA loss path, not `HeroHealth`:
`HandleDeath`'s hub branch already resolved the town anchor (`HeroHealth.cs:878`), but BattleArena's
defeat return warped to `SafeLossReturnPosition` - a pull-back anchor out in the field - and revived
the hero in place there. `ResolveTownSpawn()` is now public (`HeroHealth.cs:967`) and BattleArena
overrides the loss anchor with it when the hero actually DIED in a hub scene
(`BattleArena.cs:2313-2327`). Deliberately narrow: a loss with the hero ALIVE (flee/regroup) keeps the
safe pull-back, and dungeon defeats are excluded. The revive trace now names the anchor it landed on
(`:2620`), so a capture proves which branch ran.

**2. Starter potions - LANDED.** `StartingBudget.FoundingHealPotions = 3` (`NestedTypes.cs:90`),
seeded into the persisted larder by `ResetToNewGame` - the ONE founding-grant seam
(`GameStateService.cs:968-971`). Existing saves untouched, no schema bump. **An adjacent defect was
found and fixed in the same seam:** `VillageInventory`'s `_loaded` latch pulled `GearInventory`
exactly once per app run, so a New Game left the DDOL singleton holding the PREVIOUS session's counts and
its next `SyncToState` would have clobbered the fresh grant; it now re-pulls on `StateReplaced` and
releases the subscription in `OnDestroy` (`VillageInventory.cs:53-77`). Covered by
`CoreSaveRegression.cs:656-663` and `ResetCarveOutTest.cs:163-171`.

**3. Teach the cost of dying - NOT DONE, and this is why the WO stays READY.** No FTUE one-shot was
written, and the DISCOVERY this WO required first - what dying actually costs today - was never run or
recorded. If death has no resource consequence, that is a DESIGN GAP needing the owner's pin, not an
invention. Also untouched: this WO's own flagged risk that the potion button self-disables at zero (with
a grant in place it should live, but nothing verifies it).

**Gate:** `Builds/gate-settle4.log` -> `COMPILE_GATE_OK` · `Builds/regression-settle3.log` ->
`REGRESSION_OK 143/143 suites`.

**Owner felt-verify:** New Game -> 3 potions on the belt, button live. Die in a town wave AND lose an
arena fight -> both wake at the town anchor, never on the corpse.