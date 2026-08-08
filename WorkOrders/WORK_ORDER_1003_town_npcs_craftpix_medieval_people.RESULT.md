# WORK ORDER 1003 — RESULT: Town NPCs replaced with the CraftPix medieval people pack

**Status:** DONE (reconciled 2026-08-08 from the tree — NOT felt-verified)
**Date:** 2026-08-08 · **Seat:** CLI · **Silo:** World/characters/art

## Shipping commit
`500e5b84` — CraftPix people imported and wired into the town NPC pool.

## Decisive artifact
- `Assets/Art/People/CraftPix/` holds the **14 FBX** characters from the pack.
- `CastleTownsfolkInjector.cs:85` — the townsfolk body pool now reads `NPCs/CraftPixPeople/NPC_*`,
  i.e. the injector is pointed at the new people, not the old KayKit bodies.
- Guarded by `TownsfolkBodyPoolRegression.cs`, so a regression to the old pool now fails the suite.

## Why this RESULT exists
The WO carried `Status: READY TO IMPLEMENT` until 2026-08-08 while the work was already in HEAD.
See `docs/reference/WO_TRUE_STATUS_2026-08-08.md`.

## Outstanding
**Owner felt-verification is outstanding.** The pool swap and its regression guard are proven from the
tree; how the retargeted people read in motion in town is not. PO closes after a felt-test (§13).
