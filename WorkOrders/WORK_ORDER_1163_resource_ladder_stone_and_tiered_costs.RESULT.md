# WORK ORDER 1163 - RESULT: resource ladder and Food-to-Stone conversion

**Status:** DONE - owner-closed 2026-08-25 on a Seeker felt-test of build `2026.08.25.341262` ("stone yes"). Implementation was integrated through `45907e7e`; the owner-held device acceptance in section 7 is now satisfied.

## Final integrated result (2026-08-25)

The broad implementation landed as `a11899d58`, the coordinated save-schema/version update as
`dbedc701`, and the Quarry portrait resolution as `45907e7e`. Together with the earlier paid-pack
slice `5625f9af8`, current main now ships one Stone identity over the frozen internal Food save slot,
the L1 Wood+Gold / L2 Stone+Gold / L3 Iron+Gold building ladder, and Gold-only troop training.

Queued Gold is recorded in the v39 paid basket and cancellation refunds the complete material+Gold
basket before one authoritative save and resource notification. Legacy troop costs preserve the old
Wood+Iron+Food total as Gold; legacy pack `food` keys remain accepted as the internal Stone slot.
Canonical mirrors and generated catalog/build-category fallbacks move together. Quests retain their
wire fields while live presentation says Stone.

## Integrated evidence

- `COMPILE_GATE_OK` with zero C# errors.
- `REGRESSION_OK 279/279` on integrated main.
- Backend Node fleet `57/57` green.
- Paid-pack, battle-monthly, impulse-pack, town-bank, core-save v39, Obsidian queue/refund,
  training-door, and upgrade-authority suites green.
- Quarry portrait follow-up integrated at `45907e7e`.

## Acceptance still owner-held

The implementation/code/data scope is complete. Final closure still requires the section 7 headed
Seeker felt-test: confirm Stone/Quarry vocabulary and icons in play and observe representative L1,
L2, and troop-training charges. Do not describe missing owner/device evidence as missing conversion
implementation.

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

## Superseded partial-state note

The paragraph below described the first paid-pack-only landing. Those implementation items are now
integrated as recorded above; it is retained only as history.

The final felt-test is owner-held. Any live/device capture and final integrated Unity regression required by the remaining implementation are lead/ops-held. Player-facing names and feel remain owner authority. `blue_mine` art remains explicitly outside WO-1163.
