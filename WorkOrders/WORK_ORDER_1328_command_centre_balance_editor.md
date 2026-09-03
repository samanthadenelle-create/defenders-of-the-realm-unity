# WORK ORDER 1328 - Balance editing belongs in the Command Center, behind a simple UI driven by JSON

**Status:** FIXED - Balance tab shipped in the Command Center, driven by a manifest whose spine is GENERATED from RemoteTunables.Registry and pinned by test/tunables-manifest.test.js (23 cases). Needs a phone-width screenshot a human opened, then PO close.
**Silo / Lane:** Ops / Command Center console (`api/admin/console`) + the remote tunables rail
**Type:** EXISTING rail, MISSING surface
**Minted:** 2026-09-02 (CLI) from a direct owner ruling.
**Severity:** P2 - it is the difference between a balance pass she can actually do and one she cannot.

## The owner's ruling, verbatim

> *"and think about it, should be in command center so you dont need to be a rocket scientist. a area
> for skills, and tiers of skills or spells or almost anything (misc) and they can have a simple UI
> that rives a json"*

Preceded, in the same breath, by: *"i have been screaming this for months."*

**Read that second sentence as the requirement.** The point is not "add a page". It is that a balance
change must stop costing a specialist. Today changing one number is either a 10-30 minute rebuild or
a PowerShell command with an exact key name - and the person who needs to make that change is the
only person who can judge feel, and she is on a phone.

## What already exists - BUILD ON IT, DO NOT GREENFIELD

- **The rail:** `docs/PROD022_TUNABLE_FLAGS.md` is the contract. Registry
  `Assets/_Modules/Core/Ops/RemoteTunables.cs`, transport `RemoteTunablesService.cs`, table
  `client_tunables` in `api/schema.sql`, allowlist `TUNABLE_KEYS` in `api/_lib/tunables.js`.
- **The writes ALREADY EXIST** as `tunable.set` / `tunable.clear` on `POST /api/admin/ops`, behind
  `ADMIN_DASH_KEY` + `ADMIN_OPS_KEY`. That doc says in its own words: *"The Command Center console
  HTML has not been extended with buttons for them - the PowerShell surface above is primary."*
  **That sentence is this ticket.**
- **The console:** `api/admin/console`, already phone-first, already key-gated, keys held in memory
  only (never localStorage, never a cookie, never the URL). Keep every one of those properties.

The work is a SURFACE over an existing rail. No new configuration mechanism, no second write path,
no new table.

## What to build

**A grouped editor, driven by a JSON manifest** (her words: *"a simple UI that rives a json"*).

1. **The manifest describes the knobs**, so adding a lever later is a data edit, not a UI edit. Group
   into the AREAS she named: **Skills - Tiers - Spells - Misc**. Each entry: key, area, human label,
   plain-English description, kind, default, safe min/max.
   - CRITICAL: the manifest must be DERIVED FROM or PINNED AGAINST `RemoteTunables.Registry`. It must
     not become a fifth hand-maintained copy of the knob list - that is this repo's most expensive
     recurring bug class (one fact written twice, then it rots). An oracle must fail if they disagree.
2. **Show, for every knob: CURRENT value, DEFAULT, and whether it is overridden** - in WORDS, never
   by colour alone (the owner is red/green colourblind). "OVERRIDDEN (default 100)" beats a dot.
3. **A one-tap way back to shipped behaviour per knob.**
   - `Clear` is NOT `set 0`. Clearing removes the override so the knob answers the build default;
     setting 0 may mean something entirely different (`pi.requestTimeoutSeconds` defaults to 20, not
     0). The UI must make that unmistakable - it is the easiest way to break a live game from here.
4. **Phone-first.** Landscape and portrait, touch targets >= 112px. She will use this on a phone
   beside the device running the build.
5. Judge every write by its marker, never an exit code.

## HARD CONSTRAINTS

- **SERVER-AUTHORITATIVE VALUES ARE OUT OF SCOPE, PERMANENTLY.** Prices, entitlements, grants,
  purchase amounts (`api/_lib/purchase-catalog.js`) must NEVER appear on this page. The game takes
  real money on mainnet; a client-side override there is an exploit, not a feature. State the
  boundary in the page itself so no future seat widens it by accident.
- `api/admin/db.js` and `api/admin/stats.js` are SELECT-only by construction. Do not add a write path
  to them. Writes go through `api/admin/ops.js` and nowhere else.
- Preserve the invariant that outranks the feature: *no row, no network, no parse => TODAY'S
  BEHAVIOUR, EXACTLY.* This page must never put the client in a state it cannot get back from, and an
  empty `client_tunables` table stays the correct resting state.
- The two admin keys must stay DIFFERENT values - a second key equal to the first is one key.

## Acceptance

- [ ] The owner changes a balance value from her phone, in under a minute, without typing a key name
      or knowing PowerShell.
- [ ] Every knob shows current + default + overridden-state in words.
- [ ] Clear-vs-zero is unmistakable.
- [ ] An oracle pins the manifest against `RemoteTunables.Registry` and the server allowlist, and goes
      RED naming which two disagree. Prove it RED first and report the mutation.
- [ ] A phone-width screenshot a human opened. Headless cannot see a console.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n>` + backend `node --test` on FRESH logs.
- [ ] PO felt-verifies and closes.
