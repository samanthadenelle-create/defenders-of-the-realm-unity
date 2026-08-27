# WORK ORDER 1223 - The portal gate's COVERAGE is never asserted: a reachable dungeon with no row is silently open

**Status:** CLOSED 2026-08-27 — owner felt-tested PASS on APK 2026.08.27.343739 (dungeon review).
**Silo:** Gates / oracles (+ one server-side row)
**Origin:** Owner, 2026-08-26, on being shown that the dungeon she had just black-screened in was
absent from the door table. Owner verbatim: ***"so the regression needs to confirm that table has
mapping for every dungeon"***.

---

## PROOF

**Live door state**, fetched from `GET https://defenders-of-the-realm-v2.vercel.app/api/dungeon-status`
on 2026-08-26 (the endpoint is public read by design):
```
dg_starter_loop   open
dg_sunken_vault   open
dg_bonecrypt      open
dg_ember_deep     open
```

**The dungeon the owner was standing in is not there.** From her device the same morning:
```
[Flow:HeroOwner] scene='dg_healers_cottage' ...
[Flow:Perf] fps=60 ... scene=dg_healers_cottage towers=0 enemies=7
```
`dg_healers_cottage` is reachable, loaded, populated — and has **no row**.

**And it is in neither list:**
- `DungeonStatusCatalog.PortalDungeonIds` (`:126-132`) = the four ids above. Not there.
- `DungeonStatusRegression.MustNotBeGated` (`:115-118`) = `dg_hollow_roads`, `dg_descent_probe`,
  `dg_stair_rig`, `dg_stairwell_probe` — fixtures, probes and one crossroads. Not there either.

## THE DEFECT — a gate that validates CONTENTS but never COMPLETENESS

`DungeonStatusRegression` case 4 `[door-ids]` asserts *"the status domain is EXACTLY the four
AuthoredPortal ids."* That reads strict, and it is — **about the four it already knows**. It
iterates `PortalDungeonIds` and checks those. **It can never notice a fifth reachable dungeon,
because a fifth dungeon is not in the list it iterates.**

⭐ This is the hollow-coverage shape, and it is the most expensive class in this repo: *a gate that
reports success while the thing it exists to protect is untested.* It is not a hollow PASS (nothing
is skipped); it is a hollow SCOPE.

> ### ⚠ SUPERSEDED IN PART — OWNER RULING 2026-08-26
> The owner ruled on this WO, verbatim: ***"not acesable if not in table, if in table and works
> then yes"***. That **REVERSES** the paragraph immediately below and the first bullet of *What NOT
> to touch*. The client now **FAILS CLOSED**: an absent row, an absent table (no cache / server
> unreachable / timed out), a rejected or empty payload, and a row whose status does not parse all
> resolve **Sealed**. Two named escapes survive — the kill switch (`FeatureFlags.DungeonStatus = 0`)
> and `DungeonStatusCatalog.UngatedIds` (the Rootways crossroads + the fixtures/probes, which have
> no door and can never have a row).
> She also ruled `dg_folks_granary` and `dg_healers_cottage` **GATABLE**: both are in
> `DungeonStatusCatalog.PortalDungeonIds`, **not** in the ungated allowlist.
> The paragraph below is preserved unrewritten (CLAUDE.md §15 — frozen ledgers get a banner, not
> a rewrite); read it as the *pre-ruling* rationale.

**The client's fail-open default is CORRECT and must not change.** `api/dungeon-status.js` records
why: the status resolves before sign-in, and auth *"would INVERT the safety property — an
auth-gated status call fails for offline and guest players, and a fail-closed reading of that
failure locks them out."* A missing id, an unknown status and an unreachable server all read OPEN
**on purpose**. ⛔ Do not "fix" this by failing closed. The defect is that nothing tells us a
dungeon is uncovered — not that it defaults open.

**Drift is already detected in ONE direction only.** `DungeonStatusCatalog.cs:238`:
```csharp
if (Array.IndexOf(PortalDungeonIds, id) < 0)
    FlowTrace.Step(Sys, "payload carries unshipped id '" + id + "' - kept, nothing queries it.");
```
A **row with no dungeon** is noticed. A **dungeon with no row** — the direction that cost the owner a
black screen she could not gate — is silent.

## REQUIRED

**1. A completeness oracle.** Enumerate every dungeon id the player can actually REACH (from the
shipped content — the composed layouts under `Resources/dungeon-layouts/`, the build-scene list, and
whatever `DungeonWorldPortalSpawner` can spawn a portal for — establish the authority by reading,
and state in the RESULT which source you used and why). Assert every reached id is EITHER in
`PortalDungeonIds` OR explicitly in `MustNotBeGated`. Anything in neither is a FAILURE naming the id.

⛔ **The implementer MAY NOT extend either list to make the suite pass.** An unaccounted dungeon is
a finding for the owner, not a row to quietly add. `MustNotBeGated` entries carry a stated reason;
adding one without a reason is exactly the softening this repo forbids. **Report `dg_healers_cottage`
and let her rule which list it belongs in.**

**2. Reverse-direction coverage.** Extend the check so a dungeon-with-no-row is as loud as the
existing row-with-no-dungeon. Both directions, one oracle.

**3. ⚠ Decide where the DB half lives, and say so.** The shipped-dungeon list is CLIENT-side; the
rows are in **Neon**. A Unity EditMode oracle cannot reach the database. Options — pick one and
justify it in the RESULT:
   - (a) Unity asserts client ids against a tracked manifest; a backend test (`test/*.test.js`,
     alongside `purchases.quote.test.js`) asserts every manifest id has a row. The manifest is the
     contract and neither side can drift silently. **Recommended.**
   - (b) Unity-only, asserting against `PortalDungeonIds` and treating the DB as out of scope —
     cheaper, but it cannot catch a row deleted server-side.
   ⛔ Do NOT have the Unity oracle hit the network. Batchmode network calls are exactly the
   flakiness that makes a gate untrustworthy.

## Owner action, available TODAY with no build

`dg_healers_cottage` can be sealed by inserting one row into `dungeon_status` with a closed status
plus authored `headline`/`body`. ⛔ **The copy rule travels with the data** (`api/dungeon-status.js`
header): a closed dungeon reads as **WORLD**, never as build status — never "under maintenance",
"coming soon", or "under construction". `DungeonStatusRegression` lints for exactly those words.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts off the marker.
2. ⭐ The new case **FAILS on today's tree**, naming `dg_healers_cottage`. Prove it RED first — a
   completeness check that is green on a tree with a known uncovered dungeon is decoration (WO-1138).
3. The RESULT names the reachability authority used, and lists every dungeon id it found.
4. `DUNGEON_STATUS_OK` still emitted; the existing four cases still pass unmodified.
5. Owner rules where `dg_healers_cottage` belongs; CLI does not decide it.

## What NOT to touch

- ⛔ The fail-open default, in any direction.
- ⛔ The no-auth decision on `/api/dungeon-status`.
- ⛔ `PortalDungeonIds` / `MustNotBeGated` membership, pending the owner's ruling.
- ⛔ The banned-word lint (`construction`, `coming soon`, `wip`, `dev`, …) — that is the copy rule
  and it is load-bearing.
## LANDED-WORK AUDIT (2026-08-26)

The fail-closed client half landed in `b303c4fbf`; the backend manifest enforcement landed in
`4efbbfde` (`api/dungeon-status.js`, `api/_lib/dungeon-manifest.json`,
`test/dungeon-status.manifest.test.js`). Fresh evidence: `Builds/batch0-compile-2.log:1966`
`COMPILE_GATE_OK`; `Builds/batch0-regression-2.log:83628` `DUNGEON STATUS OK` lists all seven
reachable ids with zero unaccounted; `:83814` `REGRESSION_OK 291/291`. Node is GREEN 4/4; deliberate
removal of `dg_folks_granary` produced the named missing-row RED, the row was restored, and Node
reran GREEN 4/4. `api/schema.sql` is unchanged. The owner's placement ruling is recorded above:
`dg_folks_granary` and `dg_healers_cottage` are both portal-gated. All ticket acceptance items are proven.
