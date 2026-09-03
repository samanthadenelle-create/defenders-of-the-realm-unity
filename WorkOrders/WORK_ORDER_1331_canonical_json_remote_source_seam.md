# WORK ORDER 1331 - Connect the remote catalog seam that already exists, so canonical data stops needing a rebuild

**Status:** FIXED (2026-09-02) - see WORK_ORDER_1331_canonical_json_remote_source_seam.RESULT.md. Awaiting the gate + PO felt-verify.
**Silo / Lane:** Core / catalog loading + the remote tunables rail
**Type:** EXISTING seam, NEVER CONNECTED
**Minted:** 2026-09-02 (CLI) from the WO-1328 lever inventory.
**Severity:** P1 leverage - this is the root cause of a months-old owner complaint.

## Why this exists

Owner, 2026-09-02: *"be smart, dont make it need a code change, make it tweakable from a db call"* -
followed by ***"i have been screaming this for months."***

`docs/reference/TUNABLE_LEVER_INVENTORY.md` found why the screaming never worked:

> **"Data-driven" in this repo does not mean "tunable without a rebuild."**
> `LocalJsonCatalogSource.cs:30-40` resolves `Resources.Load<TextAsset>` **first, on every platform**,
> and `Assets/Resources/` is **compiled into the player**. Editing any of the 71 canonical JSONs costs
> a full build (~10 min APK / ~30 min WebGL). Editing the `StreamingAssets` twin changes nothing.

⚠ **Five canonical files advertise in their own authoring notes that the owner "retunes with NO
recompile."** That is literally true (no C# recompiles) and false in the only sense she cares about
(she still waits for a build). Every past attempt to solve this by moving numbers into JSON was
therefore working on the wrong axis. **Fix those five notes in the same change** - a doc that misleads
about the one thing being fixed is worse than silent.

## The seam ALREADY EXISTS and is assigned NOWHERE

`CanonicalJson.Source` is a settable `ICatalogSource`, documented in its own comments as a one-line
swap to a remote source. Nothing ever assigns it. Connecting it converts **5,224 numeric leaves** of
canonical data (excluding `widget-params.json`) into remotely updatable content **with no call-site
change anywhere in the game**.

That is the wholesale fix. It is not a new mechanism, and building one instead would be the mistake.

## ⛔ SHIP IT FLAG-GATED AND OFF. THIS IS NOT OPTIONAL.

This changes how EVERY catalog in the game loads. The blast radius is the whole product, and the game
is live on a store and takes real money.

- Default OFF. With the flag off, `CanonicalJson` resolves EXACTLY as it does today, byte for byte.
- The invariant is the rail's own, and it outranks the feature:
  **no row, no network, no server, no parse => TODAY'S BEHAVIOUR, EXACTLY.**
  A remote catalog that is unreachable, stale, truncated or malformed must fall through to the
  compiled copy. Never a blank catalog, never a hang, never a partial merge.
- A remote payload must be validated BEFORE it replaces anything. A half-parsed catalog that
  overwrites a good one is strictly worse than no feature. `Guard.Try` every parse; a rejected payload
  logs loudly (`FlowTrace.Fail`) and changes nothing.
- The fetch must never block or delay boot.
- Follow the precedence the rail already established:
  `LOCAL PlayerPrefs` beats `REMOTE` beats `COMPILED DEFAULT`.

## Scope for THIS ticket

Land the seam and prove it, on a SMALL number of catalogs - not all 71 at once. Recommend starting
with the ones the inventory ranks highest for felt-testing. Widening later is a data decision once the
mechanism is proven.

⛔ **SERVER-AUTHORITATIVE DATA IS PERMANENTLY OUT OF SCOPE.** Prices, entitlements, grants, base-unit
amounts, token decimals, quote TTL (`api/_lib/purchase-catalog.js`). The client does no pricing
arithmetic by design and `/verify` runs AFTER settlement - a client-side override there is money gone
with nothing granted. State the boundary in code, not just here.

## Two free wins found in the same sweep - fold in if cheap, else ticket them

1. **`heart.json` and `towers.json` have NO RUNTIME READER AT ALL** - only a regression asserting they
   are *served*. The shipped Heart is **100 HP with 2 HP/s regen**; the authored files say **160 HP
   with zero regen**. The game has been ignoring reviewed, authored balance data. Wiring these two
   collapses ~10 constants into data that already exists.
   ⚠ This CHANGES LIVE BALANCE (a 60% HP increase and the loss of regen). It is an owner ruling, not a
   cleanup - surface the numbers and let her decide.
2. **`Core/State/ServerConfig.cs` is a DEAD SECOND MECHANISM** - a fully wired 11-field live-ops rail
   (boss crystal drops, sales, events, refund rate), absorbed at `GameStateService.cs:1792-1796` and
   consumed at `WaveManager.cs:3571-3597`. But `api/game/load.js` **never emits a `config` key**, so
   none of it has ever been settable. **Retire it or fold its four useful keys onto the tunables rail.
   Do NOT build the missing server half** - that would be a second configuration mechanism, which is
   the exact disease this repo keeps paying for.

## Acceptance

- [ ] With the flag OFF, catalog loading is byte-identical to today. Prove it, do not assert it.
- [ ] With the flag ON and the endpoint unreachable / 404 / malformed / truncated, the game resolves
      the COMPILED catalog and logs loudly. Drive each failure path; a fall-through that has never
      been exercised is not evidence.
- [ ] An oracle pins the fall-through for every failure mode. Prove it RED first; report the mutation.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on FRESH logs, markers asserted.
- [ ] The five misleading "no recompile" authoring notes are corrected.
- [ ] PO felt-verifies and closes.
