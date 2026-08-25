# WORK ORDER 1194 - RESULT: ambient harvest and bank-cap clarity

**Status:** IMPLEMENTED AND INTEGRATED - awaiting headed/device felt-test to close.

## Landed

Commit `8c03413ab` implements the owner-ruled ambient treatment after WO-1163 established Stone:

- three always-visible capped-resource lines for Wood, Iron, and Stone;
- each line reports `current of capacity` using the authoritative town-bank model;
- the vague collector fraction becomes the action word `Harvest`;
- exactly-at-cap retains the existing full meaning, while purchased above-cap state says the value is
  owned and spendable rather than implying purchase loss;
- Gold and uncapped Crystals remain outside the capped-resource capacity treatment;
- text and shape carry state without relying on colour alone.

## Integrated evidence

- Current integrated main through `45907e7e`.
- `COMPILE_GATE_OK`, zero C# errors.
- `REGRESSION_OK 279/279`.
- Backend Node fleet `57/57` green.
- WO-1163 Stone identity and TownBankCapacity aliases are integrated ahead of this surface.

## Acceptance still open

The code/data implementation is complete. Final closure remains owner/ops-held: capture the headed
town HUD and verify the three thin lines, Harvest control, exact-cap and above-cap wording, touch
comfort, greyscale readability, and Seeker device feel. No further implementation scope is implied
by this remaining visual acceptance.
