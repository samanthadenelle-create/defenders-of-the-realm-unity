# WORK ORDER 949 — Death UX: respawn IN TOWN, starter potions, and teach the cost of dying

**Status:** READY TO IMPLEMENT
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
