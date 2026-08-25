# WORK ORDER 1195 - RESULT: partial cost-format authority

**Status:** PARTIAL - source architecture landed; required art/mapping and headed acceptance remain open. Do not close.
**Implementation commit:** `0c65af9b0`
**Registration commit:** `905fe686b`

## Landed slice

- `CostFormat` is the shared structured cost authority; migrated cost surfaces consume concept, full-word fallback, amount, and compact amount text rather than authoring letter suffixes.
- The uGUI and UI Toolkit renderers resolve icons through the existing concept-icon authority and fall back to the full ASCII resource word when art is unavailable.
- The build-palette unaffordable state keeps `NEED` as a separate word, and the retired `NEED 80W 30I` canon example was corrected.
- The source oracle scans the player-facing C# surface for retired resource-letter grammar and second icon authority, and is registered in `DataRegression`.

## Fresh integrated evidence

- `COMPILE_GATE_OK`
- `REGRESSION_OK 281/281 suites`
- `COST_FORMAT_SOURCE_OK`

## Still open - this ticket is not complete

- Approved icon art and canonical mapping are still required for `stone`, `magic`, and `wisdom`; do not invent or silently substitute these assets.
- Capture and open the shipped build-screen cost band, including an unaffordable card whose `NEED` word remains legible.
- Run and inspect the required greyscale capture so each resource remains identifiable by silhouette rather than hue.
- Confirm the existing price-band geometry and neighboring card content did not regress.

The green headless gates prove the shared source authority, not the missing art or headed visual acceptance.
