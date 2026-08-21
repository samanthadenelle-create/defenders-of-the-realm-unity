<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-28
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-28) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 545 — Fix 8 pre-existing EditMode test failures

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Lane:** QA / data-and-tests (no scene files, no art)
**Opened:** 2026-06-28 (surfaced during Gear Preview headless verification — these are NOT
from that change; EditMode was 361/369 with the 8 below already red)

## Context
A full EditMode run (`run-tests.ps1 -Platform EditMode`, total=369) shows **8 failures**
across unrelated subsystems. Two are confirmed pre-existing (present in the prior
`Builds/editmode-results.xml`). They block a clean "all green" gate, so triage + fix.

## The 8 failures (from `Builds/test-results-EditMode.xml`)

### A. BuildingCatalog (×3) — data drift: catalog now has 10 buildings, tests expect 9
- `BuildingCatalogTest.buildings_json_loads_exactly_nine_buildings` — Expected 9, was 10.
- `BuildingCatalogTest.building_capabilities_are_data_on_the_entry` — "armorer must NOT be
  shoppable" Expected False, was True.
- `BuildingCatalogTest.find_by_type_resolves_each_building_type` — `Find(Armorer)` Expected
  not null, was null.
- **Decide:** is the 10th building canonical (then update the test's expected count + the
  armorer shoppable/find expectations to the new truth) OR is `buildings.json` wrong (then
  fix the data)? This is a data-vs-test reconciliation — confirm intended building set first.

### B. Wallet log-assert (×3) — tests log an `[Error]` without `LogAssert.Expect`
- `WalletServiceTest.a_provider_payment_failure_surfaces_through_pay`
- `WalletServiceTest.pay_for_a_rail_with_no_price_fails`
- `WalletServiceTest.pay_with_a_null_pack_fails_cleanly`
- Each fails on "Unhandled log message: '[Error] [Flow:Wallet] …'". The code correctly logs
  the failure; the TEST must declare it with `LogAssert.Expect(LogType.Error, <regex>)` before
  the act. Fix = add the expects (do not silence the production log).

### C. ModalPanelDisciplineTests.OpeningSecondPanel_ClosesTheFirst — unhandled error log
- Fails on "[Error] [Flow:UI] PanelManager: 'Second' recorded as OPEN but its IsOpen probe
  reports NOT open …". Either the test panel's `IsOpen` probe is wrong for the test double, or
  the test needs a `LogAssert.Expect`. Investigate which (real discipline bug vs test-double gap).

### D. UnityObjectNullCoalesceLintTests.NoNullCoalesceOnGetComponent — a real lint hit
- The lint scans `Assets/` for `??` / `?.` used on a `UnityEngine.Object` lookup
  (`GetComponent`/`FindObjectOfType`), which mis-handles Unity fake-null. Find the offending
  site(s) and rewrite to an explicit `== null` check. (Not in the Gear Preview files — those
  were checked clean.)

## Acceptance criteria
- `run-tests.ps1 -Platform EditMode` → **0 failures** (or each remaining failure explicitly
  justified + waived by the owner).
- No production logging silenced to make a test pass (use `LogAssert.Expect`).
- Building-set reconciliation decision recorded in the test + `buildings.json` as the agreed truth.

## What NOT to touch
- Gear Preview / talent files (EquipmentPanel, EquipVM, IEquipTarget, HeroSkillTree*).
- No scene files; no art.

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `edf1d14f7` — stale expectations reconciled. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
