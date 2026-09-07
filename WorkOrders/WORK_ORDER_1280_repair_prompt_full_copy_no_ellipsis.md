# WORK ORDER 1280 - Repair prompt full copy, no ellipsis

**Status:** CLOSED 2026-09-06 - owner felt-test PASS (validated 2026-09-04T17:26:32, build 2026.09.04.354315). PRIOR STATUS: FIXED 2026-08-29 - complete repair copy is present in Seeker tester APK 2026.08.29.346849; awaiting owner device test.

**Minted:** 2026-08-29 from authoritative main-line banner 1280; banner bumped to 1281 in this same edit.

## Player outcome

Every selected damaged structure shows its complete name, health and damage percentages, repair or rebuild cost, exact material shortfall, and full action label. The responsive phone card wraps and best-fits readable text; no repair runtime label, card, status, or button uses ellipsis or truncation.

## Acceptance evidence

- `WallRepairController.ComposePromptDetails` is the full-copy authority.
- `HudKitController.ShowRepairPrompt` reserves a four-line detail pane and separate phone-safe action column.
- `RepairPromptReadabilityRegression` pins wrap/overflow settings and a long-name, long-cost, unaffordable full-copy path.
- Static and compile/regression gates must be green before APK handoff.
