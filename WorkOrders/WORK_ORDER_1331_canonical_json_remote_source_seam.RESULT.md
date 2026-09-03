# WORK ORDER 1331 - RESULT

**Status:** FIXED (code landed, flag-gated OFF) - 2026-09-02
**Gate:** NOT RUN. This ticket's instructions forbade running a Unity gate, a content build, an R2
push, a deploy or a commit. `COMPILE_GATE_OK` / `REGRESSION_OK` / the oracle's RED-then-GREEN mutation
are therefore **PENDING** and are the orchestrator's to run. Nothing below claims to have been proven
by a run that did not happen (CLAUDE.md section 12).

---

## 1. What landed

`CanonicalJson.Source` - a settable `ICatalogSource` documented since 2026-06-27 as a one-line swap and
**assigned nowhere in the tree** - is now assigned in exactly ONE place, behind a flag that ships OFF.

| Layer | File | New? |
|---|---|---|
| State, parse, **validation**, allowlist + deny list | `Assets/_Modules/Core/Data/RemoteCatalogOverrides.cs` | NEW |
| The `ICatalogSource` decorator (serve override, else delegate) | `Assets/_Modules/Core/Data/RemoteCatalogSource.cs` | NEW |
| Transport, poll, device cache, **the one install** | `Assets/_Modules/Core/Data/RemoteCatalogService.cs` | NEW |
| Local arm, default OFF | `Assets/_Modules/Core/FeatureFlags.cs` - `RemoteCatalogs` (`ff.catalogremote`) | edited |
| Oracle | `Assets/Editor/Regression/RemoteCatalogSeamRegression.cs` - `[catalog-seam]` | NEW |
| Oracle registration | `Assets/Editor/Regression/DataRegression.cs` | edited (one line + a comment) |
| Dead-mechanism banner | `Assets/_Modules/Core/State/ServerConfig.cs` | edited (comment only) |
| Five misleading authoring notes | 5 canonical JSONs x 2 trees (Resources + StreamingAssets) | edited |
| Canon | `docs/reference/TUNABLE_LEVER_INVENTORY.md` new section 2.2 | edited |

**No call site changed anywhere in the game.** That was the whole point of the seam.

### Catalogs connected - FIVE, deliberately, not 71

`RemoteCatalogOverrides.Allowlist`:

```
Data/Canonical/enemies.json          - enemy stats, the biggest difficulty surface with a real reader
Data/Canonical/waves.json            - wave pacing / composition inputs
Data/Canonical/echoes-balance.json   - Echo income (inventory row 14)
Data/Canonical/kill-rewards.json     - the grind-to-repair economy (owner ruling 2026-08-26)
Data/Canonical/siege-stakes.json     - siege stakes, whose own note calls its numbers provisional
```

Widening is a **data edit** (`Allowlist` + the matching literal in the oracle), which is exactly the
"prove the mechanism, then widen" shape the WO asked for.

⚠ Note recorded in-code for whoever widens: the `[wave-authoring]` regression fails the gate if
`enemies[]` batches reappear in **the file**; a remote payload does not pass through that gate. It is
safe today only because those batches are inert (`_smartComposition:1`, CLAUDE.md section 8).

### Arming

- **Local (a human at the device):** PlayerPrefs `ff.catalogremote` = 1. Default OFF.
- **Remote (the owner at the console):** the tunables-rail knob `catalog.remoteEnabled`, read **only
  if it is registered** in `RemoteTunables.Registry`, so the code emits no "UNREGISTERED tunable key"
  noise today and needs **no edit** the day the knob is added. **That registry edit was deliberately
  NOT done - see section 7, coordination.**

Precedence is the rail's own and unchanged: LOCAL PlayerPrefs beats REMOTE beats COMPILED DEFAULT.

---

## 2. How the flag-OFF path was PROVEN byte-identical

Two independent proofs, one structural and one measured. The structural one is the stronger, and it is
the reason the design puts the flag check *before* the assignment rather than inside the decorator.

**(a) STRUCTURAL - the flag-off path is not "equivalent code", it is THE SAME CODE.**
`RemoteCatalogService.Install()` returns **before** touching `CanonicalJson.Source` when disarmed, and
`Bootstrap()` returns **before** starting any fetch. So with the flag off:

- `RemoteCatalogSource` is **never constructed**;
- `CanonicalJson.Source` still holds the `LocalJsonCatalogSource` its own **field initializer** set;
- no `UnityWebRequest` is created, no PlayerPrefs cache is read, no poll runs;
- `CanonicalJson.Read`'s own trace line still prints `via LocalJsonCatalogSource` - even the log text
  is unchanged, which a decorator that always installed would have silently altered.

Verified by grep at HEAD+working tree: `CanonicalJson.Source =` appears **four** times in
`Assets/**/*.cs` and **three of them are doc comments** (`CanonicalJson.cs:29`, `ICatalogSource.cs:8`,
and the `<c>` sample). The single executable assignment is inside the flag-gated `Install()`. The
oracle re-counts this on every run (case `[never-blocks]`) so a second assignment cannot appear
unnoticed and quietly invalidate the claim.

**(b) MEASURED - resolved both ways and compared ordinal, full string.**
Oracle case `[flag-off]` reads each of the five allowlisted catalogs through a bare
`LocalJsonCatalogSource` and through `new RemoteCatalogSource(local)` with no overrides standing, and
fails on any ordinal difference or any empty result. It also asserts `FeatureFlags.RemoteCatalogs` is
false, `RemoteCatalogService.Enabled` is false, `Installed` is false, `CanonicalJson.Source is
LocalJsonCatalogSource`, `RowCount == 0` and `TableProvenance == "default"`.

---

## 3. Every failure mode driven, and what it fell through to

All thirteen are driven in oracle case `[failure-modes]`, and **after each one all five catalogs are
re-compared ordinal against their compiled text**. Zero network, zero database - the whole state half
is transport-free by design, so a failure mode is driven by handing it a string.

| # | Failure driven | Where it is caught | Falls through to |
|---|---|---|---|
| 1 | null body | `ApplyPayload` empty check | compiled copy, `Warn` |
| 2 | empty body | same | compiled copy, `Warn` |
| 3 | whitespace-only body | same | compiled copy, `Warn` |
| 4 | malformed JSON | Guarded `DeserializeObject` -> null | compiled copy, `Fail` |
| 5 | **truncated payload** (cut at 60%) | same | compiled copy, `Fail` |
| 6 | **truncated CATALOG inside a well-formed payload** | `Validate` -> `JToken.Parse` fails | compiled copy, `Fail`, **whole payload rejected** |
| 7 | oversized catalog body (> `MaxCatalogBytes`) | `Validate` size cap | compiled copy, `Fail`, whole payload rejected |
| 8 | server `readOk:false` | explicit branch | table CLEARED to compiled copies, `Warn` |
| 9 | empty `catalogs` map | accepted as "nothing overridden" | compiled copies, `Step` |
| 10 | good JSON, **wrong root kind** (array vs object) | `Validate` root-kind match | compiled copy, `Fail`, whole payload rejected |
| 11 | good JSON **missing a top-level key** the compiled copy has | `Validate` key-coverage | compiled copy, `Fail`, whole payload rejected |
| 12 | a **MONEY** path (`packs.json`) | `IsDenied`, checked FIRST | compiled copies, `Fail`, whole payload rejected |
| 13 | a catalog this build never heard of | not on allowlist | skipped, `Step` (forward-compat) |
| 14 | **corrupt device cache** | `RemoteCatalogService.ApplyCachedPayload` -> same validating parse | compiled copies; the cache is DISCARDED so it cannot be re-rejected every launch |
| 15 | **garbage arriving after a good payload** | rejection returns before the atomic swap | the PREVIOUS good table survives intact - asserted on both row count and content |

Transport-side failures (not drivable headlessly, so they are proven by construction and by matching
the shipped `RemoteTunablesService` line for line):

- **unreachable / offline / DNS / captive portal** -> `req.result != Success` -> `LogFetchFailure`,
  compiled copies. `req.timeout` is set, so a captive-portal socket cannot hang the session.
- **timeout** -> surfaces as `Result.ConnectionError`, same path.
- **non-2xx** -> the UniTask awaiter THROWS (WO-769); caught, **and** re-checked after the await,
  because checking only one is the historical bug.
- **404 (endpoint not deployed - which is TODAY'S state)** -> treated as "the feature is absent", not
  as unreachability: the standing table is cleared **and the device cache dropped**, so an override
  can never outlive the system that set it. This is the resting state that ships.

**There is no path that can blank a catalog, hang boot, or half-apply one.** A payload is accepted
whole or rejected whole; the swap is a single assignment of a fully-validated dictionary.

**Non-blocking is structural, not a comment:** `Bootstrap()` calls `PollForeverAsync().Forget()` with
no `await` at the call site; there is no barrier, no `WaitForCompletion`, no `.Result`, no
`Thread.Sleep` - and case `[never-blocks]` greps the file for each of those.

---

## 4. The oracle, and the mutation

`Assets/Editor/Regression/RemoteCatalogSeamRegression.cs` - marker `CATALOG_SEAM_OK` /
`CATALOG_SEAM_FAIL`, registered in `DataRegression.RunAll` as `[catalog-seam]`. Six cases:
`[flag-off]`, `[failure-modes]`, `[override-applies]`, `[money-boundary]`, `[allowlist-shape]`,
`[never-blocks]`. It snapshots, clears and restores `ff.catalogremote` so an armed developer machine
reds nothing.

It deliberately also proves the mechanism **works** (`[override-applies]`: a valid payload IS served,
only for its own path, and `Clear()` restores the compiled text). An oracle that only ever proves
"nothing happened" goes green just as happily when the feature is dead code.

### THE MUTATION (defined, NOT executed)

> In `RemoteCatalogOverrides.ApplyPayload`, at the failed-`Validate` branch, change
> `return false;` to `continue;`.

That is precisely the **partial merge** this ticket forbids: a payload with one bad catalog would land
its other rows. It must red `[failure-modes]` on modes 6, 7, 10 and 11 (the four that reach `Validate`
with a good payload wrapper), because the standing table would then hold a row where the oracle
expects the compiled text.

**⛔ IT WAS NOT RUN.** Proving it red requires the Unity gate, which this ticket's instructions
explicitly forbade me to run. Handing over a mutation I have not executed is the honest state; a green
oracle nobody has seen go red is not yet evidence (CLAUDE.md section 12). **Orchestrator: run
`DataRegression.RunAll` (or the standalone
`DeNelle.Editor.Regression.RemoteCatalogSeamRegression.RunAll`) green, apply the mutation, confirm RED,
revert.**

---

## 5. The two "free wins"

### 5a. `heart.json` / `towers.json` - NUMBERS FOR THE OWNER, NOTHING APPLIED

Confirmed at source: a grep of `Assets/**/*.cs` for `heart.json` / `towers.json` returns **only**
`Assets/Editor/Regression/DataWebRegression.cs` (which asserts they are *served*) and one comment in
`ArcaneCrownAura.cs`. **Neither file has a runtime reader.** ⛔ **NOTHING WAS WIRED AND NO BALANCE WAS
CHANGED** - the WO is explicit that this is an owner ruling, not a cleanup.

**The Heart - a 60% HP increase and the loss of all regen, in one step:**

| | Shipped today | `heart.json` says |
|---|---|---|
| Max HP | **100** (`HeartController.cs:97`, `[SerializeField, Range(0,100)] _hp = 100f`) | **160** |
| Regen out of combat | **2 HP/s** (`HeartRegen.cs:61`, `_regenPerSecond = 2f`, ticked every 0.5 s) | **0** |
| Ring radius | (scene geometry) | 4.4 |
| Phase thresholds | intact / wounded 0.6 / critical 0.25 - the HUD already swaps at these | same |

This is **the loss condition**, and the two changes pull in opposite directions: +60% HP makes it
tankier, losing 2 HP/s regen makes every point of chip damage permanent. Today a Heart at 10 HP is
back to full in 45 seconds of calm; under the authored file it never heals at all. ⚠ Note also that
`_hp` is `[Range(0f, 100f)]` and scene-serialized, so raising max HP to 160 is **not** a one-line data
change - the range attribute and any scene value have to move with it.

**The towers - authored file vs what three seeded ScriptableObjects actually ship**
(`Assets/Editor/TowerDataSeeder.cs:84-85, 98-99, 113-114`; `Tower.cs` reads `TowerData.upgrades[].range/damage`):

| Level | `towers.json` range / damage / cooldown | Seeded archetype ranges | Seeded archetype damage |
|---|---|---|---|
| 1 | 14 / 12 / 1.1 s | 18, 14, 12 | 22, 18, 8 |
| 2 | 17 / 22 / 0.9 s | 20, 16, 14 | 35, 28, 14 |
| 3 | 21 / 40 / 0.7 s | 22, 18, 16 | 50, 40, 22 |

`towers.json` also authors zone geometry (`sectorAngleRadians`, `slotsPerZone` 3, `slotFanAngleRadians`
0.34, `slotRadiusOffset` 4) that nothing reads either. Note the shapes genuinely differ: the JSON has
ONE tower line with a cooldown, the build has THREE archetypes with none - so this is not a
"switch the reader on" job, it is a design question about which model is wanted.

**Recommendation:** ticket the Heart pair as a single owner ruling (it is one felt-test: "does the
Heart stop healing?"), and treat `towers.json` as **stale authored data** to either retire or
re-author against the three-archetype model before anyone wires it.

### 5b. `ServerConfig.cs` - RECOMMENDATION: RETIRE IT

Verified: `resp.Config` is absorbed at `GameStateService.cs:1794` and consumed at
`WaveManager.cs:3571-3597`, but `api/game/load.js` **never emits a `config` key**, and a grep of the
whole `api/` tree for `bossWaveCrystalDropChance` / `BOSS_CRYSTAL_DROP_CHANCE` returns nothing. So all
eleven fields have been permanently `ServerConfig.Default` for the life of the file.

**I did NOT build the missing server half** (the WO forbids it, and it would be the second
configuration mechanism this repo keeps paying for). What I did do is put a `⛔ DORMANT` banner at the
top of `ServerConfig.cs` and **retire the false sentence that was living in its header** - *"All values
are set via Vercel environment variables in the backend dashboard - no Unity rebuild or code change
needed to adjust live game parameters."* That line was false for the entire life of the file and is
exactly the kind of doc that let everyone believe balance was already remotely tunable. Comment-only;
zero behaviour change.

**Recommendation (owner decision):** RETIRE the record. Its `MaintenanceMode`/`MaintenanceMessage` are
already superseded by the live `maintenance_toggles` rail. If the four boss-drop keys are still wanted
(`bossWaveCrystalDropChance` 0.45, `bossWaveCrystalMin` 1, `bossWaveCrystalMax` 3, `bossWaveInterval`
5), add them to `RemoteTunables.Registry` as ordinary int knobs - one mechanism, one precedence, one
oracle. Retiring changes no behaviour: every reader already gets `Default`.

### 5c. The five misleading authoring notes - CORRECTED

`dungeon-balance.json`, `echoes-balance.json`, `kill-rewards.json`, `siege-stakes.json`,
`vendors.json`, in **both** trees (Resources + StreamingAssets), all ten files re-verified to parse and
to remain byte-identical twins. Each now carries, immediately after its "NO recompile" phrase, a
correction stating that `Assets/Resources` is compiled into the player, that the edit still costs a
full build (~10 min APK / ~30 min WebGL), that editing the StreamingAssets twin alone does nothing,
and whether that file is on the WO-1331 allowlist yet.

---

## 6. Brace / NUL check - every file touched

Checked with a byte-level reader (`{` vs `}` count, embedded NULs, non-ASCII position). No file
touched contains a NUL byte. All non-ASCII characters in every file are inside **comments** (house
style); every player-facing and log string is plain ASCII.

| File | `{` / `}` | NUL |
|---|---|---|
| `Assets/_Modules/Core/Data/RemoteCatalogOverrides.cs` | 43 / 43 OK | 0 |
| `Assets/_Modules/Core/Data/RemoteCatalogSource.cs` | 5 / 5 OK | 0 |
| `Assets/_Modules/Core/Data/RemoteCatalogService.cs` | 31 / 31 OK | 0 |
| `Assets/_Modules/Core/FeatureFlags.cs` | 33 / 33 OK | 0 |
| `Assets/_Modules/Core/State/ServerConfig.cs` | 23 / 23 OK | 0 |
| `Assets/Editor/Regression/RemoteCatalogSeamRegression.cs` | 73 / 73 OK | 0 |
| `Assets/Editor/Regression/DataRegression.cs` | 1022 / 1022 OK | 0 |

*(The oracle's JSON fixture literals were deliberately written brace-balanced - e.g. the malformed
body is `{ this is not json ,,, }` rather than an unclosed brace - so the naive CLAUDE.md section 1
counter is not tripped by a string. They are still unparseable, which is what the case needs.)*

The ten canonical JSONs were re-parsed with a JSON parser and their Resources/StreamingAssets twins
re-compared byte for byte.

---

## 7. Coordination - what I did NOT touch, and one defect found in another lane

**Live lanes respected.** WO-1330 (`RemoteTunables.cs`, `RemoteTunablesDefaultsRegression.cs`,
`api/_lib/tunables.js`, `docs/PROD022_TUNABLE_FLAGS.md`) and WO-1332 (Alduin spelling in canonical
JSON + `.yarn`) both have files open in this working tree. **I edited none of them.**

**That is why the rail knob `catalog.remoteEnabled` is NOT registered.** Adding a knob is a four-file
edit and **all four of those files are WO-1330's**. Rather than mint into another lane's open file, the
seam reads the knob *only if it is registered*, so:

> **FOLLOW-UP (cheap, after WO-1330 lands):** add
> `new TunableSpec("catalog.remoteEnabled", TunableKind.Bool, 0, ...)` to `RemoteTunables.Registry`,
> the key to `TUNABLE_KEYS` in `api/_lib/tunables.js`, a row to `docs/PROD022_TUNABLE_FLAGS.md`, and
> the pair to `ExpectedDefaults` + `ExpectedKnobCount` in `RemoteTunablesDefaultsRegression.cs`.
> **No change to any WO-1331 file is needed** - the seam picks it up the moment the spec exists.

### ⛔ COMPILE-BLOCKING DEFECT IN WO-1330's WORKING-TREE EDIT (not mine, not fixed)

`Assets/Editor/Regression/RemoteTunablesDefaultsRegression.cs:261-262` is missing a string-concatenation
`+`:

```csharp
reason = "TUNABLE DEFAULTS OK - all " + ExpectedKnobCount + " knobs (... + the three "
         "WO-1330 over-time levers + the two WO-1327 VFX feel/perf clamps) resolve to " +
```

Two adjacent string literals with no operator - this will not compile, so `COMPILE_GATE_OK` cannot be
reached on the current tree by anybody. **Reported, not fixed:** it is WO-1330's file and the
coordination rule says report. It is a one-character fix for that lane.

---

## 8. Server half - deliberately NOT built, and what it would be

`GET /api/client-catalogs` does not exist, so today the seam - even if armed - takes the **404 path**:
table cleared, cache dropped, every catalog resolves its compiled copy, one loud `Warn`. That is the
correct resting state and it is what ships.

Building the endpoint was **out of scope** for this ticket (which asked to land and prove the client
seam) and is untestable from here - no database, and deploying is forbidden. When it is wanted it is a
direct mirror of the tunables pattern, and it is the thing that turns this from a proven mechanism into
a thing the owner can actually use:

- `client_catalogs` table: `path TEXT PRIMARY KEY, body TEXT, updated_at`;
- `api/_lib/catalogs.js` - read + a `CATALOG_PATHS` allowlist mirroring
  `RemoteCatalogOverrides.Allowlist` (and never the deny list);
- `api/client-catalogs.js` - public unauthenticated GET, 10 s edge cache, returning
  `{version, readOk, reason, catalogs:{path: body}}`;
- write actions on the existing two-key admin endpoint, and a **Balance**-tab-style card;
- a `test/catalogs-manifest.test.js` re-deriving the allowlist from `RemoteCatalogOverrides.cs` so the
  two lists cannot drift (the pattern `test/tunables-manifest.test.js` already proves catches drift).

---

## 9. Acceptance vs the WO

- [x] Flag OFF ⇒ byte-identical. Proven structurally (the assignment never executes; one assignment in
      the tree, oracle-counted) **and** measured ordinal on all five catalogs.
- [x] Flag ON with unreachable / 404 / malformed / truncated ⇒ compiled catalog, logged loudly. All
      thirteen headless modes driven; the four transport modes proven by construction against the
      shipped `RemoteTunablesService`.
- [~] An oracle pins the fall-through for every failure mode. **Written and registered; the RED-first
      mutation is defined but NOT EXECUTED** - running the gate was out of scope. See section 4.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n>` on fresh logs. **PENDING - orchestrator.** Note the
      WO-1330 compile defect in section 7 blocks the gate until that lane fixes it.
- [x] The five misleading "no recompile" authoring notes are corrected (ten files, both trees).
- [ ] PO felt-verifies and closes. **PENDING** - and note the seam is inert until the endpoint exists,
      so the felt-verify that matters today is "the game is unchanged with the flag off".
