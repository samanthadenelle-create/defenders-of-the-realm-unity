# WORK ORDER 1206 - A retired resource word must never reach a player surface again

**Status:** CLOSED 2026-08-27 — owner felt-tested PASS on APK 2026.08.27.343739.
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

---

## RESULT - 2026-08-26 (edit-only agent lane; NOT gated, NOT committed)

The suite itself landed on 08-25. Reading it against its own acceptance left three real gaps,
all of them the shapes this repo hunts rather than cosmetic ones.

### 1. Acceptance 4 was a ONE-OFF proof, and a one-off proof expires

The ticket asked for "reintroduce a retired word on a display surface, watch the suite name it,
then remove it". That was done by hand on 08-25 - and it stopped being evidence the moment
anyone touched a predicate. **A detector that has silently stopped matching reports exactly the
same clean green as a clean tree**, and a failure-only oracle is not acceptance; this repo has
already shipped a guard that aborted every good run while exiting 0.

`SelfTest` now runs on EVERY execution, driving **the same** `ScanJsonDocument` /
`ScanSourceLines` the real sweep uses (split out for that purpose - never a second
implementation, which would only prove the copy agrees with itself). **15 cases, both
directions:**

- 5 RED cases that MUST be named: a whitelisted display field, copy inside a `tips[]` array, a
  dotted locale key in `en.json`, an assignment to a label, toast copy.
- 10 GREEN cases that MUST stay silent: `JsonProperty` wire names, `const string` keys, a
  `case "food":` switch label, a `PlayerPrefs` key, a `FlowTrace` argument, the retired word as
  a substring (`Seafood`, `seafood stew`), an unquoted identifier, already-converted copy, and
  raw wire keys (`food_store`, `paidFood`, `resourceKey`).

A silent RED case fails as **BLIND**; a noisy GREEN case fails as **OVER-BROAD** and says
"narrow the predicate, do not baseline the surface" - because an oracle that flags
`EconomyService.Food` is switched off within a week, which the ticket itself calls worse than no
oracle.

### 2. The ratchet was a HashSet, and a HashSet cannot ratchet

`KnownCopyDebt2026_08_25` was a set of `file:surface` keys, so it forgave **any number** of
leaks in a surface it had ever forgiven. A third retired word authored into
`guide-content.json`'s `tips[]` would have been waved straight through by a row minted for two
OTHER tips - which is precisely the "a new leak cannot hide behind the old ones" claim the
comment above it makes.

It is now `Dictionary<string,int>` with an **exact count**:

- the **(count+1)th** hit in a baselined surface FAILS like any other leak;
- a row matching **fewer** than its count fails as **DRIFT** - lower it, the debt shrank;
- a row matching **nothing** fails as **STALE** - the copy was fixed, delete the row.

That last one is what actually enforces "this list may only ever shrink". Measured against the
canonical tree on 08-26: `guide-content.json:tips[]` **2**, `quests.json:objectiveText` **1**,
`structures-catalog.json:description` **1** - the same four owner-owed prose rows the 08-25 run
left, now pinned by count rather than by name.

### 3. Two hollow passes in the JSON sweep

```
try { root = JToken.Parse(File.ReadAllText(file)) as JContainer; }
catch { continue; } // syntax belongs to the canonical-data gate
if (root == null) continue;
```

Both `continue`s certified a file the suite **never read**, and reported the same green as if it
had. Delegating the syntax error to another gate is fine; reporting *this* suite clean over a
file it could not open is not. Both now FAIL naming the path and saying it was NEVER SCANNED.
Neither fires today - all **94** canonical JSON files parse and all have container roots -
so this costs no red and closes the blind spot.

`LineOf` also returned **1** when the value could not be located verbatim, pointing the reader
at the opening brace as though it were the leak. It is now `LineLabel`, returning `line?` - a
confidently wrong coordinate is worse than an honest unknown.

### How RED was proven

- **The self-test IS the standing red proof**, and every one of its 15 expectations was verified
  against faithful simulations of the real predicates (`FrozenSourceSyntax`, `VisibleSourceHint`,
  `QuotedWholeWord`, `WholeWord`, `IsVisibleJsonValue`, `SurfaceName`) before it was written -
  9/9 source cases and 6/6 JSON cases agreed with the expectations authored into the suite.
- **The ledger counts were MEASURED, not assumed**: a sweep of all 94 canonical JSON files using
  the suite's own visibility rules returned exactly the four rows above, with the counts 2/1/1.
- The two new `[unreadable]` failures were checked against the same sweep: **zero** unparseable
  files and **zero** scalar roots, so they are latent guards, not new debt.

### Files

- **Edit** `Assets/Editor/Regression/RetiredVocabularyRegression.cs` - braces **72/72**, NUL
  bytes **0**, all additions pure ASCII (the 29 non-ASCII bytes in the file are pre-existing box
  and stop glyphs, untouched).

### DataRegression

**No new line needed.** The suite was registered on 08-25 at
`Assets/Editor/Regression/DataRegression.cs:689` and the entry point (`Run(out reason)`) is
unchanged.

### What was NOT touched

- PROD-016's own fix, and the frozen internal slot names (`EconomyService.Food`,
  `PackEconomy.Food`, `BuildJobData.paidFood`, `legacySkus`, quest wire fields).
- `Assets/Resources/Data/Canonical/*` - reserved by another lane this batch. The four prose rows
  are the owner's copy call and are held by the ledger, not edited.
