# WORK ORDER 1206 - A retired resource word must never reach a player surface again

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-25 (CLI lead, main line; banner bumped 1206 -> 1207 in the same edit)
**Silo:** Tooling / gates (the oracle) + HUD (whatever the oracle surfaces)

---

## Why this exists - two leaks in one hour, both found by the owner, not by us

WO-1163 retired Food. It converted the economy, the save contract, the costs and the town strip,
and the owner CLOSED it on a device. Within the hour, felt-testing the same build, she hit **two
surfaces it never reached**:

1. **The build menu** - `LiveWalletSource.cs:88` hardcoded `new WalletVM.Entry("food", ..., "F", food)`
   and shipped **"F 130"** to a live build. Owner: *"build menu still has W I F (food)"*.
   **Fixed 2026-08-25** in the same session; captured at `tmp/felt2/shot-191315.png`.
2. **The Echo job and the world node** - `EchoAssignments.cs:99` still publishes `ResFood` in
   `PickableResources`, `HarvestSite.cs:368` still maps `MineResource.Food -> "Harvest/food"`.
   Owner: *"assigned to food node"*. **Still open - PROD-016 is the live remainder.**

⛔ **The pattern, and it is the one this repo keeps paying for:** the conversion was applied
**per-surface** instead of at one seam, and **nothing asserted the retirement**. A ruling with no
oracle behind it is not retired - it is merely *mostly* renamed, and the remainder surfaces in front
of the owner one screen at a time. Same shape as the WO number block (CLAUDE.md sec.2), the assembly
table (sec.5), the R2 push (sec.16) and WO-1137's 3-of-28 fallback catalog.

⚠ **It also nearly cost a ticket.** The 2026-08-25 Ready-board RCA classified PROD-016 as *"stale,
duplicate or not assignable"* on the reasoning that WO-1163 would close it. WO-1163 closed; the
defect did not. Acting on that classification would have deleted a live, reproducible defect from a
build that takes real money.

## What to build

A registered regression - working name `RetiredVocabularyRegression` - that **fails when a retired
resource word can reach a player-visible surface.**

1. **Author the retirement list as DATA, not as a C# list** (WO-1161's rule: a list in code is one
   fact written twice). One canonical row per retirement: the retired word, the word that replaced
   it, and the date/ticket that retired it. `food -> stone`, WO-1163, 2026-08-25 is the first row.
2. **Sweep the player-visible channels only** - display strings, badge letters, picker option lists,
   catalog display names, toast/label copy. ⛔ **Do NOT flag persistence or wire vocabulary:**
   `EconomyService.Food`, `PackEconomy.Food`, `BuildJobData.paidFood`, the `legacySkus` aliases and
   the quest wire fields are all DELIBERATELY frozen by WO-1163 - the internal slot keeps its name on
   purpose. An oracle that cannot tell those apart will be turned off within a week, which is worse
   than no oracle.
3. **Fail with the file:line and the surface**, so the fix is mechanical for whoever gets the red.
4. **Prove it RED before green** - reintroduce a retired word on a display surface, watch the suite
   name it, then remove it. A pin that has never been seen red is not evidence (2026-08-23 lesson).

## Acceptance criteria

- The suite is registered in `DataRegression.RunAll` (⛔ committer-fenced - the lead adds that line).
- Red proven and quoted in the RESULT, then green inside `REGRESSION_OK <n>/<n>` on a fresh log.
- The retirement list is data, dual-copy, version-bumped.
- Running it today surfaces the PROD-016 surfaces and **nothing frozen** - if it flags
  `EconomyService.Food`, the scoping is wrong and the ticket is not done.

## What NOT to touch

- ⛔ PROD-016's own fix. That ticket owns the Echo/node conversion **including the read-migration for
  persisted `food:N` assignment tokens**; this ticket only has to make its absence LOUD. Two seats in
  `EchoAssignments.cs` is the duplicate-work failure this batch already refused once.
- ⛔ The frozen internal slot names listed above.
