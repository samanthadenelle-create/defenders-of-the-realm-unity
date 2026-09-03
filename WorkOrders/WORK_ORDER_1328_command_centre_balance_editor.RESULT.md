# WORK ORDER 1328 - RESULT

**Status:** FIXED (code + oracle + canon). **NOT deployed, NOT committed** - the lead owns deploy,
gate and commit. **Not closed:** closing needs a phone-width screenshot a human opened, then PO
felt-verify. Headless cannot see a console.

**Lane:** Ops / Command Center console + the remote tunables rail. Deliberately disjoint from the
Unity client: **no `.cs` file was touched**, so nothing here can collide with the owner's Android
felt-test or with the VFX/combat lanes running in the same tree tonight.

---

## What shipped

A **Balance** tab in the Command Center, in the **primary nav** (not behind "More tools"), driven by
a JSON manifest.

Per knob, on one card: the plain-English name, what moving it actually does, the **current** value,
the value **the installed build ships with**, and the state as a **WORD** - `OVERRIDDEN (the
installed game ships with 100)` or `Shipped default - nothing is overriding it`. Ints get a big
number field with `-` / `+` bumpers and **Save this value**; bools get **Turn ON** / **Turn OFF**.
Every card carries **Reset to shipped (N)**.

**Reset is not zero, and the page says so three times:** at the top of the tab, in the note under
every control, and in the confirm dialog the finger actually triggers - *"This REMOVES the override,
so the knob answers whatever the installed game says: 20. It is NOT the same as saving 0."*

Touch targets are **112 px** (`--bigtap`). Nothing on the surface carries meaning by hue: the accent
colour only ever repeats a word that is already written out.

The out-of-scope boundary is **printed on the page**: *"Prices, purchase amounts, entitlements and
grants are NEVER editable here and never will be... the game takes real money; a value the phone
could override would be an exploit, not a feature."*

## Files changed

| File | What |
|---|---|
| `api/admin/console.js` | **modified.** Balance tab: nav entry, `renderBalance`, `renderKnob`, `knobNow`, `knobSpec`, `writeKnob`, `loadTunables`, the click wiring, and the `--bigtap` CSS block. The template is now `PAGE_TEMPLATE` and `PAGE` is it with the manifest substituted in, ASCII-checked at the seam. |
| `api/_lib/tunable-manifest.js` | **new.** The JOIN: owner areas, labels, plain English, safe ranges + `mismatches()` and `build()`. |
| `api/_lib/tunable-manifest.generated.json` | **new, GENERATED.** key + kind + default, derived from `RemoteTunables.Registry`. Do not hand-edit. |
| `tools/gen-tunable-manifest.mjs` | **new.** The generator + the parser the oracle reuses. Markers: `TUNABLE_MANIFEST_GEN_OK` / `TUNABLE_MANIFEST_DRIFT` / `TUNABLE_MANIFEST_GEN_FAIL`. |
| `test/tunables-manifest.test.js` | **new.** The oracle, 23 cases. |
| `test/command-center.test.js` | **modified.** `OPS_ACTIONS` pin updated 5 -> 7. See "pre-existing red" below. |
| `docs/PROD022_TUNABLE_FLAGS.md` | **modified.** The "From the phone" section said the console had **not** been extended - that sentence *was* this ticket. Retired with a banner (never rewritten silently), replaced with the phone procedure, the manifest ownership table, and the money boundary. Where-this-lives table extended. |
| `docs/CLI_OPERATIONS_RUNBOOK.md` | **modified.** Section 8, one bullet under "The command center". |
| `WorkOrders/WORK_ORDER_1328_*.md` | Status -> FIXED. |

**Nothing else was touched.** `api/admin/db.js` and `api/admin/stats.js` are unmodified and remain
SELECT-only by construction - the oracle asserts they contain no mention of tunables at all, so
WO-1328 cannot have opened a second door there. No new table, no new write path, no second
configuration mechanism: writes go to the pre-existing `tunable.set` / `tunable.clear` on
`POST /api/admin/ops`, behind the same two keys.

## How the manifest is pinned - the specific mechanism

Three facts about a knob, three owners, **none written twice**:

| Fact | Owner | How it gets to the page |
|---|---|---|
| key + kind + **default** | `Assets/_Modules/Core/Ops/RemoteTunables.cs` `Registry` | **PARSED** by `tools/gen-tunable-manifest.mjs` into `api/_lib/tunable-manifest.generated.json`. Resolves the `const string Key*` names and the named int defaults (`VerbosityVerbose`, `DrainReturnPctDefault`), and **throws** on any entry it cannot resolve rather than dropping it. |
| may this key be written | `TUNABLE_KEYS` in `api/_lib/tunables.js` | required directly; a key not on it is **not offered** by `build()` |
| area / label / plain English / safe range | `PRESENTATION` in `api/_lib/tunable-manifest.js` | hand-authored - genuinely new information, not a copy of anything |

`build()` **joins** them and refuses to invent. `mismatches()` returns plain-English defects that
**name the two sources**, e.g. *"BUILD REGISTRY vs SERVER ALLOWLIST: RemoteTunables.Registry has
`combat.drainReturnPct` but TUNABLE_KEYS in api/_lib/tunables.js does not - the server would REFUSE
every write to it."* Defects also render **on the page**, so a drift is visible to the owner and not
only to a test run.

`test/tunables-manifest.test.js` re-parses `RemoteTunables.cs` **from disk on every run** and asserts
the checked-in JSON is byte-identical to a fresh derivation. It also pins the doc
(`docs/PROD022_TUNABLE_FLAGS.md` must name every registered key) and the served page.

**Adding a lever later is a data edit, not a UI edit:** register the knob, allowlist it, run
`node tools/gen-tunable-manifest.mjs`, add one `PRESENTATION` entry. The card appears on its own.

## The oracle proved RED - four mutations, each restored

| # | Mutation | Result |
|---|---|---|
| 1 | Generated JSON default `100` -> `55` (i.e. the registry moved and the JSON did not) | RED: *"BUILD REGISTRY vs GENERATED MANIFEST: Assets/_Modules/Core/Ops/RemoteTunables.cs has moved and api/_lib/tunable-manifest.generated.json has not. Run: node tools/gen-tunable-manifest.mjs"* |
| 2 | `PRESENTATION` key renamed `combat.drainReturnPct` -> `...PctXX` | RED x4, naming both directions: *"BUILD REGISTRY vs CONSOLE MANIFEST: ...Registry has combat.drainReturnPct but PRESENTATION does not - the knob would be INVISIBLE in the Command Center"* and *"CONSOLE MANIFEST vs BUILD REGISTRY: PRESENTATION has combat.drainReturnPctXX but the Registry does not - the page would show a lever that moves nothing"* |
| 3 | `combat.drainReturnPct` removed from the `TUNABLE_KEYS` allowlist | RED x3: *"BUILD REGISTRY vs SERVER ALLOWLIST: ...the server would REFUSE every write to it"* |
| 4 | `knobNow` made an unreadable table fall back to the default | RED: *"RENDERED: an unreadable table is NEVER drawn as 'everything is at its default'"* |

Every mutation was reverted and the suite re-run green afterwards.

**And it caught two REAL drifts, unprompted, on the night it was written.** Parallel lanes landed
`vfx.particleBouncePct` + `vfx.maxParticleLights` (WO-1327) and then three `combat.overTime*` knobs
(WO-1330) into the shared tree while this ticket was in flight. The oracle went red naming each
missing knob within the minute. That is the whole argument for deriving the spine instead of typing
it, and it happened by accident on day one. All five are now on the page: **Spells** holds 6 knobs,
**Misc** 8.

## The steps the owner takes on her phone

1. Open `https://<app>.vercel.app/api/admin/console`.
2. Type the read key, tap **Open**.
3. Tap **Balance** (second tab, always visible).
4. Scroll to **Spells** -> *Drain healing return*. It reads `Now 100 / Shipped default - nothing is
   overriding it`.
5. Tap `-` a few times, or type in the big field. Tap **Save this value**.
6. Confirm the dialog ("Set Drain healing return to 50? The installed game ships with 100.").
7. First write of the tab asks for the write key, once. It is never saved.
8. The card re-reads and now says `Now 50 / OVERRIDDEN (the installed game ships with 100)`.
9. Felt-test. **About 40 seconds** to a running game (10 s edge cache + 30 s client poll).
10. To put it back: **Reset to shipped (100)**. Not "type 100" - Reset removes the row, so the knob
    answers whatever build she happens to be running.

No key name typed. No PowerShell. No rebuild.

## Verification - markers on fresh logs

- `node --test "test/*.test.js"` -> **328 tests, 328 pass, 0 fail.**
  Baseline before this lane was **305 / 302 pass / 3 fail**; all three pre-existing failures were the
  same stale `OPS_ACTIONS` pin (`tunable.set` / `tunable.clear` were added to the endpoint by PROD-022
  and the test still expected five actions). Two were fixed by updating the pin; the third
  (*"pushing a SKU is declared NOT INSTRUMENTED"*, which asserts the page's postable actions equal
  `OPS_ACTIONS` exactly) was fixed **by this work**, because the page now posts both knob actions.
- `node tools/gen-tunable-manifest.mjs --check` -> `TUNABLE_MANIFEST_GEN_OK knobs=14 (checked, no drift)`.
- Page script parses standalone (`node --check`) -> `PAGE_SCRIPT_PARSE_OK`.
- The served page is still **7-bit ASCII end to end** and still stores **no key** anywhere
  (`test/command-center.test.js`, unchanged assertions, green).

**Not run:** any Unity batchmode. `COMPILE_GATE_OK` / `REGRESSION_OK` are the lead's to take, and
this lane changed no `.cs`, so neither marker is affected by it - but the two parallel lanes in the
same tree did change `.cs` and the lead should gate at HEAD before trusting any of it.

## What was deliberately NOT touched

- **No `.cs` file.** Not `RemoteTunables.cs`, not the C# `[tunable-defaults]` regression. The manifest
  oracle is a Node test on purpose: it runs with no Unity lock, so it cannot collide with a build.
- **`api/admin/db.js` / `api/admin/stats.js`** - untouched, still SELECT-only by construction.
- **`api/_lib/purchase-catalog.js` and everything it decides** - prices, entitlements, grants,
  purchase amounts. Permanently out of scope, stated on the page and asserted on the *shape* of the
  manifest (a knob key matching `price|sku|entitle|grant|usd|payout|refund|cost|purchase|wallet`
  fails the suite), so a future seat adding "just one more knob" trips it rather than shipping it.
- **The PowerShell surface** (`tools/command-centre.ps1 -Tunables`, `tools/client-tunables.mjs`) -
  unchanged and still works.
- **The invariant** - no row / no network / no parse => today's behaviour, exactly. An empty
  `client_tunables` table is still the correct resting state; this page only ever writes overrides
  and deletes them.
- **Other lanes' uncommitted work in the shared tree** (WO-1327 VFX, WO-1330 over-time,
  `PetDeployer.cs`, `ProjectSettings.asset`, `docs/reference/TUNABLE_LEVER_INVENTORY.md`). Read, not
  edited, except that their new knobs are now presented on the page. **The lead commits by explicit
  path.**

## Left for the human

- A **phone-width screenshot, opened by a person**, of the Balance tab. Headless cannot see a
  console, and this ticket is about whether it is usable one-handed.
- Deploy (`vercel deploy --prod`) - the console is dead until then, and `vercel.json` sets
  `git.deploymentEnabled:false`, so pushing does not deploy.
- PO felt-verify and close.
