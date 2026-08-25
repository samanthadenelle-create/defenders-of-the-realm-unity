# WORK ORDER 1163 - RESULT: paid-pack food-to-stone correction

**Status:** PARTIAL - money-path slice landed; WO-1163 remains open and assignable.

## Landed

Commit `5625f9af819384e8933d8cdecd63264a4c89d950` (`feat(economy): replace impulse food grants with stone`) moves the paid-pack rename across all five required surfaces:

- server `USD_ANCHORS`;
- both byte-identical canonical `packs.json` copies;
- the Node resource-key oracle, now derived from canonical grants so it fails closed;
- the Unity `PackEconomy` JSON binding, impulse amount lookup, and shortfall route;
- the registered Unity impulse regression's resource family and raw-authored-key-versus-DTO oracle.

The three renamed `impulse-stone-*` rows retain their retired `impulse-food-*` ids through `legacySkus`. The internal `PackEconomy.Food` field remains the reused persistence/economy slot and binds authored `stone`; there is no save-schema migration, conversion haircut, amount change, rate change, rounding change, or settlement change.

## Evidence

- Fresh lead gate: `COMPILE_GATE_OK` before commit.
- Quote suite: `31/31` green.
- Complete backend Node fleet: `57/57` green.
- Canonical pack mirrors: byte-identical, MD5 `A711238D20A51A29E294236AB25B3D3D`.
- Source safety: `git diff --check` clean; `SaveSchema.CurrentVersion` remains `38`; no quest path changed.

## Still open - do not close WO-1163

This result covers only the bounced money-path correction. Section 7 still requires the broader food-to-stone game conversion, L1 wood+gold / L2 stone+gold / L3 iron+gold repricing, troop training to straight gold, regenerated WO-1137 fallback, and captured proof that the three representative actions charge those baskets.

The final felt-test is owner-held. Any live/device capture and final integrated Unity regression required by the remaining implementation are lead/ops-held. Player-facing names and feel remain owner authority. `blue_mine` art remains explicitly outside WO-1163.
