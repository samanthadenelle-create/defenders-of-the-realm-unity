# WORK ORDER 703 — Blank start: residual structures/NPCs beyond the ruled set (BLANK-1)

**Status: READY TO IMPLEMENT — verification step FIRST** (owner screenshots ×3, 2026-07-13,
new build 10:37, strategic placement locked on).
**Lane:** World/injectors. **Type:** EXISTING (WO-695 standdown incomplete — the world has more
spawners than WO-673's list knew about).

## THE RULING (owner 2026-07-13, CANON — supersedes prior carve-outs)

**A fresh start is the TREE, the WELL, and the WALLS — nothing else.** Owner clarification
(2026-07-13, same session): **"the walls" INCLUDES the GATES** — the wall ring stays complete
with its gatehouses/gates; do not strip gates as "extra structures". This supersedes the
07-13 anchor's "apothecary/jewelers-bench stations stay injector-owned until catalog rows land"
carve-out: those stations (and their vendor NPCs) stand down too. Acceptance is crisp: stand at
spawn on a fresh save and count exactly three things — the tree, the well, and the complete
walled ring (gates included).

## Symptom inventory (screenshot-evidenced)

(a) a small house + field NPCs + one NPC ON TOP of the gatehouse wall; (b) the COLOSSEUM/arena
entrance — owner: "should be completely flagged off for now" (the 07-10 Colosseum_ArenaEntrance
that already drifted the tutorial-tower probe); (c) a large tavern/inn + a market stall + a crowd
of townsfolk near the tree — "should not exist". Green particle quads visible again beside the
colosseum — linked to ticket VFX-1 (same emitter class, fresh evidence for its census).

## VERIFY FIRST (§12 — before classifying anything a defect)

0. **Save state:** the Knight is Lv 10 in the shots — confirm whether that session was a FRESH
   save or the owner's MIGRATED main (migrated records legitimately keep their structures). If
   it's the main save, the true defects narrow to the NPCs + the wall-percher; the building
   inventory re-verifies on a genuinely fresh save.
1. **Fresh-save scene census:** headless/instrumented pass over the merged scene on a fresh save
   naming EVERY structure visual + NPC and the spawner that made it ([Flow:*] on each injector).
   The census makes the spawner list exhaustive ONCE — no whack-a-mole per screenshot.

## RCA candidates (census names the real ones)

1. Station/vendor injectors spawn NPCs WITHOUT building markers (CastleVendorNpcInjector
   deferred polls ~:233/:280) — now stood down per the ruling.
2. Baked storefront visuals missed by the standdown sweep (tavern + stall + house — census
   names each).
3. Colosseum injector/visual needs its own default-OFF flag (ff.colosseum or the WO-695
   standdown list).
4. Townsfolk/ambient NPC injectors (AmbientNPC/VillageNpcInjector class) spawn regardless of
   buildings — gate each NPC on its home building existing.
5. NPC-on-wall = spawn sampling ANY walkable navmesh (wall-top) — sample the ground ring only.

## Acceptance
- [ ] Fresh save: census log lists tree + well + walls and NOTHING else (no structure visuals,
      no NPCs, no colosseum, no stations); the same census re-run proves it post-fix.
- [ ] Colosseum behind a default-OFF flag; reversible.
- [ ] Migrated save: every legitimate BaseLayout record still spawns; nothing double-spawns.
- [ ] NPC spawns sample the ground ring only (no wall-top placement anywhere).
- [ ] 07-13 anchor's residual carve-out line updated IN THE SAME COMMIT (§15).
- [ ] COMPILE_GATE_OK + fleet (tutorial + vendor probes on fresh save) + owner felt-pass
      standing at spawn (PO closes).

## What NOT to touch
BaseLayout replay of real records · the WO-695 migration marker semantics · the authored shell
(tree/well/walls) · VFX-1's emitter fix (separate ticket; this WO only feeds it evidence).

*Cross-refs:* ticket BLANK-1 (owner screenshots) · WO-695 (the standdown this completes) ·
WO-702 founding-FTUE (builds on the true blank start) · ticket VFX-1 (green quads evidence) ·
CANON_GROUND_TRUTH_2026-07-13 (residual line to supersede).
