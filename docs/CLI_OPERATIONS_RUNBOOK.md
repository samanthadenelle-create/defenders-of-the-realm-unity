# CLI OPERATIONS RUNBOOK — everything a CLI seat needs, in one file

**Status:** LIVE canon. Written 2026-08-27 by the CLI lead seat.
**Audience:** any CLI seat (Claude Code, Codex, or a human) picking up this repo.
**Why this exists:** the owner's session budget is finite. When she is not available to correct a
seat mid-flight, the seat must be able to boot, be an SME, gate, ship and deploy without asking.

> ⚠ **This runbook does not replace `CLAUDE.md`.** `CLAUDE.md` is the LAW (what you may and may not
> do). This is the PROCEDURE (how to actually run the machine). Where they disagree, `CLAUDE.md`
> wins and this file is the thing that is stale — fix it in the same commit.

---

## 0. THE ONE RULE THAT PREVENTS MOST DAMAGE

> ## ⛔ JUDGE EVERY GATE BY ITS MARKER ON A FRESH LOG. NEVER BY THE EXIT CODE.

This repo's runners **exit 0 on refusals, on FAILs, and on licence errors**. `wrapperExit=0` and
`succeeded=True` mean the *process* ran, not that the *work* passed. A marker's **absence on a fresh
log is a FAILURE, not an unknown.**

"Fresh" means the log's mtime postdates the run's start. `run-unity-method.ps1 -ExpectMarker <MARKER>`
checks freshness for you and prints `VERDICT=PASS`/`FAIL` — **always pass `-ExpectMarker`.** Without
it you get `VERDICT=PASS-UNASSERTED`, which proves nothing about which run produced the text.

---

## 1. SESSION STARTUP — the SME knowledge you must have BEFORE touching code

**Do this unprompted, every session, in this order.** The owner should never have to tell a seat to
read canon.

| # | Read | Why |
|---|---|---|
| 1 | `PREFLIGHT_GATE.md` | Answer YES + a one-line proof to every applicable item. One NO or "I think so" = STOP. |
| 2 | `SESSION_CANON_LOADER.md` | The at-a-glance SME primer: core rules + current state + key files. |
| 3 | `CLAUDE.md` | The binding law. Non-negotiable. |
| 4 | `docs/MASTER_CATALOG.md`, then the `docs/MASTER_CATALOG/<area>.md` for what you will touch | File-by-file truth, verified from CODE not comments. |
| 5 | The newest `CANON_GROUND_TRUTH_<date>.md` at repo root | The single live anchor for current reality. |
| 6 | `docs/HANDOVER.md` | How we work + resume points. |
| 7 | `docs/ARCHITECTURE.md` | The authoritative hub; read before any per-area `*_ARCHITECTURE.md`. |

Then regenerate and read the board (§3).

**⚠ COMMENTS LIE. THE CATALOG IS BUILT FROM CODE FOR THIS REASON.** The canonical example: a class
header saying "pure transform" over something that is actually a `NavMeshAgent`. Trusting the comment
misdiagnoses every movement bug in that system.

**⚠ JUDGE A DOC BY ITS DATE.** An older ticket asserting current state is presumed stale where a
newer doc or the owner's own words conflict. An UNDATED work order claiming "#1 priority now" is
stale by definition.

### Facts that are true today and cost people days when assumed wrong

- **Repo root is machine-dependent** (`C:\eoa` on one machine, `D:\eoa` on another). Never hardcode
  it. Write paths relative to the root.
- **Home hub scene = `Main_Castle_Overworld`.** `MainCastle_Hall.unity` still exists on disk and is
  **legacy, not the hub**. `Village.unity` and `OuterWorld.unity` are **deleted**.
- **There are 19+ `.asmdef` files. READ THE ASMDEF** — it is the authority on what may reference
  what. The only *enforced* invariant is: **`DeNelle.HUD` never references `DeNelle.Village`**, in
  either direction. Reflection in `AdminOverlay.cs` exists *because* of that rule, not in violation
  of it.
- **The `SpawnPoint` tag DOES NOT EXIST.** Only four tags are declared: `Tower`, `Building`,
  `HeartTarget`, `Player`. `FindGameObjectsWithTag` **throws** on an undeclared tag. Enemy spawns
  resolve by the `WaveSpawnPoint` **component**.
- **`RaidHeroSpawner` never existed.** Do not go looking for it.
- **UXML does not work in builds.** Always code-built UI.
- **Save schema version:** read it off `SaveSchema.CurrentVersion`, never off a doc. Never write
  `CurrentVersion - 1` in a test — it silently follows the next bump and skips the case you meant.
- **The owner is red/green colourblind.** Never carry meaning by hue alone; never ask her to pick
  hues. State goes in WORDS.
- **Money is REAL** (mainnet sales + SKR live as of 2026-08-27). "No purchase has ever completed" is
  retired and must not be used as a safety argument for any migration, reset, or economy edit.

---

## 2. THE SEAT MODEL — who does what

| Seat | Does | Never does |
|---|---|---|
| **Owner (Samantha)** | PM. Final creative decisions. Felt-tests and **closes** tickets. | Is never the bug detector. |
| **UI seat** (Claude app) | RCA, work orders, specs, narrative, mockups, board grooming. | **Never writes or edits `.cs`.** |
| **CLI seat** (this) | Writes + build-verifies ALL code. **Sole git committer.** Owns batchmode. | Does not classify-triage; does not close tickets. |
| **Agents** | ONE focused task each, in parallel, on **file-disjoint** lanes. | Do not gate, do not commit. |

**Orchestrate, do not solo.** Route focused tasks to agents on disjoint lanes; you keep your hands on
**GATE** and **COMMIT**. Read-only diagnosis agents are gate-free — fan out many.

**⛔ ONE COMMITTER.** Two seats committing duel over `.git/index.lock` and produce stale locks and
false "pushed". Other seats write and signal ready; the committer reconciles **by explicit path**.
Never `git add -A`. Review every other seat's diff against the whole tree, not its stated scope.

**Verify agent claims — do not take them at face value.** They are usually right and occasionally
confidently wrong. Re-run their tests. Spot-check their key mutation yourself. Twice this session an
agent's report differed from the tree (a claimed file edit that was real but invisible to a bad grep;
a marker name that differed from what actually reached the log).

---

## 3. THE BOARD — how it works

```powershell
python tools/board_build.py     # ~2s. Regenerate at session boot and before any board read.
```

- **`BOARD.html` (repo root) is GENERATED from the repo.** The repo IS the source of truth; the board
  is a derived view and **cannot drift**.
- It parses `WorkOrders/*.md` `**Status:**` lines, RESULT markers, and the numbering banner.
- **Flip a WO's own `**Status:**` line in the SAME COMMIT as the work.** There is no second system to
  update.
- **`PROOF.html`** is the screenshot-proof page, generated by `python tools/proof_build.py` from
  `proof/manifest.json`. Images live in `proof/img/`. A missing image renders as `PROOF MISSING`.
- Board self-check prints `BOARD_CHECK_OK <n> unlabeled, <n> status contradictions`.

**⛔ Linear, Notion and the Task list are ALL RETIRED.** Do not mark, mirror, or read any of them.

### Work order numbering

- **The `CLI_LANES_WO_NUMBERS.md` banner is the SOLE authority.** Not the filesystem max, not a
  number copied into any other doc — every copy goes stale.
- Two **disjoint** blocks: a main line (CLI) and a reserved block (UI seat). Read the ranges off the
  banner; they are deliberately not written elsewhere.
- **Bump your own banner row in the SAME EDIT as the mint.** A mint written to disk without bumping
  the banner *is* the collision. `board_build.py` prints `BANNER_OK next mint - CLI: N, UI seat: M`.

---

## 4. GATES — the commands and their markers

Run from the repo root. **Unity must be closed** (project lock) for any batchmode run.

| Gate | Command | Marker |
|---|---|---|
| Compile | `.\run-unity-method.ps1 -Method DeNelle.Editor.CompileGate.Run -LogName c1 -ExpectMarker COMPILE_GATE_OK` | `COMPILE_GATE_OK` |
| Full regression | `.\run-unity-method.ps1 -Method DeNelle.Editor.DataRegression.RunAll -LogName r1` | `REGRESSION_OK <n>/<n> suites` |
| UI capture | `.\run-unity-method.ps1 -Method DeNelle.Editor.UICaptureLaunch.RunCaptureHeadless -LogName cap1 -ExpectMarker UI_CAPTURE_OK` | `UI_CAPTURE_OK <count>` |
| Schema parity | `node tools/schema-parity.mjs` | `SCHEMA_PARITY_OK <n> table(s)` |
| JS tests | `node --test "test/*.test.js"` | `pass N / fail 0` |
| R2 content | `.\tools\r2-ship.ps1` (Android) / `.\tools\r2-ship.ps1 -Target WebGL` (Pi) | `R2_PUSH_OK`, `R2_PARITY_OK` |

**Never restate the suite count in a doc — read it off the marker.** (It was 308, then 309, within one
morning.)

**The markers are DISTINCT per entry point.** `DataRegression.RunAll` → `REGRESSION_OK`;
`RegressionSuite.RunAll` → `CHECKIN_SUITE_OK`; `SessionRegression.RunAll` → `SESSION_GUARDS_OK`. They
once all printed `REGRESSION_OK`, so a 22-case suite's pass read as the full suite's pass.

### Reading a failure

`REGRESSION_FAIL: N failure(s) (X/Y registered suites green)` — the detail lines follow it:

```bash
grep -A6 "REGRESSION_FAIL" Builds/<log> | grep -E "^\s+- "
```

### C# file quality gate (before reporting done on ANY `.cs` edit)

```bash
python -c "
import io
c=io.open('Assets/path/File.cs',encoding='utf-8').read()
print('BALANCED' if c.count('{')==c.count('}') else 'MISMATCH', 'NUL!' if chr(0) in c else '')
"
```

**ASCII-ONLY in player-facing strings.** A tofu oracle FAILS the regression on characters the UI font
cannot render. CJK brackets (`〔〕`) cost a full gate run on 2026-08-27. No em-dashes, no ellipsis
character, no smart quotes.

### Two failure classes that look like passes

1. **HOLLOW PASS** — a guard that returns early on a missing dependency without asserting or emitting
   `RegressionOutcome.Skip`/`PartialSkip` lands GREEN. It is a **P1 defect**. The detector reports
   **one site per run**, so sweep whole files.
2. **A test that has never failed proves nothing (WO-1138).** Prove RED first: break the thing, watch
   the test fail, restore, watch it pass. Report the mutation. Prove the **success path too** — a
   failure-only oracle shipped a guard here that aborted every good run while exiting 0.

---

## 5. BUILDS

**Close Unity first.** Every batchmode run takes the project lock.

| Target | Script |
|---|---|
| Windows player | `.\build-windows.ps1` |
| Android APK | `.\overnight-apk-build.ps1` (full chain, **blocks** on R2) |
| **Google Play AAB** | `.\google-play-aab-build.ps1` (full chain, **blocks** on R2 **and on size**) |
| AAB size check only | `.\google-play-aab-build.ps1 -MeasureOnly` (bundletool, no build, no push) |
| APK install to device | `.\install-apk-to-seeker.ps1 -Build:$false -Install:$true` |
| WebGL | `.\build-webgl.ps1` / `.\build-webgl-isolated.ps1` |
| WebGL serve locally | `.\serve-webgl.ps1` |
| Morning ship chain | `.\morning-ship-chain.ps1` (**blocks** on R2) |

> ### ⛔ THE AAB GOES THROUGH `google-play-aab-build.ps1`, NEVER A RAW `Unity.exe` LINE (WO-1365)
> Until 2026-09-04 **no script invoked `BuildGooglePlayAab` at all** — the 2026-09-01 AAB came from a
> hand-assembled command line with **no `-ExpectMarker`**, the AAB lane **never pushed R2** (it
> resolves the same `.../Android/` remote catalog as the APK, and every build stamps a new version so
> it asks for a **new content-hashed catalog** no previous push can cover — §16 occurrence five
> waiting), and **nothing measured size** (31 MB appeared in two days with every marker green).
> The wrapper closes all three. Markers, judged on a fresh log, never on the exit code:
> `AAB_SIGNING_PREFLIGHT_OK` / `AAB_SIGNING_OK` / `AAB_SIGNING_FAIL` (exit 5 — a missing or incomplete
> gitignored `keystore.properties` makes `ApplyReleaseSigning` fall back **silently to DEBUG
> signing**, which Play rejects), `[AndroidBuild] SUCCEEDED` (asserted via `-ExpectMarker`),
> `AAB_OK` / `AAB_STALE` / `AAB_FAILED_NO_AAB` (exit 1 — freshness, not existence),
> `R2_PARITY_OK` / `R2_PARITY_FAILED` (exit 3 — delegated to `tools\r2-ship.ps1`, the one file; never
> re-inline the push or the verify), `AAB_SIZE_OK <bytes> (<margin> under <ceiling>)` /
> `AAB_SIZE_FAIL` / `AAB_SIZE_UNMEASURED` (exit 6), `AAB_DONE`. Status file: `Builds\aab-status.txt`.
> Ceiling defaults to **500,000,000 bytes** — Play's base-module compressed-download limit on the
> strict decimal reading. Google documents neither MB nor MiB and the difference is ~14 MB; do not
> raise the default to the generous reading to make a build pass. Measured with Unity's own bundled
> `bundletool-all-*.jar` + OpenJDK (`build-apks --mode=default` then `get-size total --modules=base`,
> **MAX** binding), both located by searching the Hub — no install, no hardcoded editor version. The
> ~1 GB `.apks` intermediate is deleted in a `finally`.

### ⛔ THE TESTER BUILD MUST BEHAVE LIKE PRODUCTION (owner ruling 2026-08-27)

> *"i need it to act as it would in prod as its the only way to really test it"*

`TESTER_BUILD` is for **tooling the owner deliberately invokes** — the AdminOverlay, the resource
grant, the F8 flag chip. It is **NOT** a licence to change what the game does on its own.

Never fix a testing inconvenience by making the tester build diverge in BEHAVIOUR. A build that
behaves differently from production cannot validate production — a bypass makes each felt-test
cheaper and every one of them less meaningful, and first-run flows are exactly what only ever break
for real players. If a flow is painful, either it is correct and you walk through it, or it is a
defect and you fix it **for everyone**.

(This ruling overturned a recommendation on WO-1249 to skip wallet validation for testers. The
distinction that survives: a tool you press ≠ the app acting differently.)

**⚠ After an Android build the active target stays Android**, which breaks SBP/Addressables for a
desktop build. Pass `-buildTarget Win64` for the next desktop build. Licensing-error lines in that
log are benign noise.

**⚠ Long batchmode nights leak ~90 GB of commit charge that no process owns.** Player builds then OOM
with RAM apparently free and nothing to kill. **A reboot is the only fix** — ask the owner.

**⚠ `install-apk-to-seeker.ps1` deletes your APK by default.** Pass `-Build:$false` to install an
existing one, and call it directly (not via `-File`).

---

## 6. ⛔ R2 CONTENT — the trap that has bitten THREE times

> ## ENEMY AND STRUCTURE **ART** IS SERVED FROM THE R2 CDN. THERE IS NO LOCAL FALLBACK.

A build whose bundles were never uploaded **installs, launches and plays** — with tinted **capsule**
enemies and placeholder buildings, and **no error on screen**. The only detector is the owner's eyes,
which is exactly what we must never rely on.

> ### ⭐ BUNDLE NAMES ARE CONTENT-HASHED. EVERY CONTENT BUILD NEEDS ITS OWN PUSH.
> A push from a previous build can **never** cover this one. The bucket looking full proves nothing.
> "I pushed yesterday" is never an answer.

**The sanctioned path is ONE file:** `tools\r2-ship.ps1` (push + verify, marker-judged, exit 16 on
failure). `morning-ship-chain.ps1` and `overnight-apk-build.ps1` call it and **block**;
`install-apk-to-seeker.ps1` calls it with `-WarnOnly` (a deliberately offline sideload is legitimate
*there and only there*).

**⛔ Do NOT re-inline the push or the verify into any chain, script, doc or work order.** They were
copy-pasted into two chains once and had already drifted.

**The asymmetry is real and it is the trap:** push the **PARENT** (`ServerData`), verify the
**EXPLICIT** target (`ServerData/Android`). Pushing `ServerData/Android` **flattens the keys to the
bucket root** where the game never looks — and reports `R2_PUSH_OK` while uploading 103 unreadable
objects. Both forms are hardcoded exactly once inside `r2-ship.ps1`.

**⛔ A raw `adb install` of a hand-built APK bypasses all of it.** Installing or distributing goes
through the scripts, never through raw `adb`.

**The `pre-push` hook** refuses `git push` whenever anything under `ServerData/` is newer than
`Builds/r2-parity.log`, or that log lacks `R2_PARITY_OK`. The invariant is *the proof must postdate
the bytes it claims to prove*. **There is deliberately no override flag** — to clear a real block, run
`tools\r2-ship.ps1`.

Wire the hook once per clone (it is local config and does not travel):

```powershell
git config core.hooksPath .githooks
```

---

## 7. FIREBASE — pushing the Android build to testers

One-time setup:

```powershell
npm install -g firebase-tools
firebase login
```

Distribute:

```powershell
.\distribute-android.ps1 -Notes "what changed"
```

- Default APK path: `Builds/Android/DefendersOfTheRealm.apk`.
- **App ID resolution order:** `-AppId` parameter → `$env:FIREBASE_APP_ID` → `firebase-appid.txt`
  (gitignored). If none is present the script errors rather than guessing.
- Optional `-Groups <tester-group>`.
- Under the hood: `firebase appdistribution:distribute <apk> --app <id> --release-notes <notes>`.

**⚠ Distribute only what R2 already has.** An APK distributed before its bundles are pushed is the
capsule-enemy failure, delivered to testers.

**⚠ Check the build you are distributing is newer than the fix you think is in it.** Compare the APK's
timestamp against the commit. A fix committed after the build is not in the build — this exact mistake
shipped once this month.

---

## 8. VERCEL — the backend (`api/`) and the web UI

> ### ⭐ `vercel.json` sets `git.deploymentEnabled: false`. **PUSHING DOES NOT DEPLOY.**
> A green push and a green gate tell you nothing about what is live. Deploy is an explicit act.

The backend lives **in this repo** at `api/` (Vercel serverless) — it is not a separate project.

**Deploy:**

```powershell
vercel deploy          # preview
vercel deploy --prod   # production
```

`webgl-vercel-overnight.ps1` wraps this for the WebGL build and captures the deployment URL into
`Builds\vercel-deploy.txt`.

**Live API base — NOT a secret, it is compiled into the public APK:**
`https://defenders-of-the-realm-v2.vercel.app` (pinned as a `const` in `EventTracker.cs:52`,
`WebTrace.cs:76`, `MaintenanceService.cs:76`, `BenefactorsService.cs:59`).

> ⭐ **If you think you cannot access prod, read `docs/ACCESS_AND_SECRETS.md` before saying so.** The
> URL, the endpoints and the project ids are public; the credentials are already on this machine in
> gitignored `.env.local`, and every tool resolves them the same way. Never ask the owner to paste a
> credential into a prompt.

**Environment variables** (Vercel → Settings → Environment Variables). None of these may ever be
printed, logged, or echoed — names and lengths only:

| Var | Used by |
|---|---|
| `DATABASE_URL` | every `api/` route that touches Neon (`?sslmode=require`) |
| `ADMIN_DASH_KEY` | the READ half: `api/admin/db.js`, `api/admin/stats.js`, the console gate |
| `ADMIN_OPS_KEY` | the WRITE half: `api/admin/ops.js` ONLY. **Must be a different value** — a second key equal to the first is one key. |

**Smoke test after deploy** (a 200 with JSON = live; empty arrays are fine):

```
GET https://<app>.vercel.app/api/leaderboard?metric=best_wave&period=all
GET https://<app>.vercel.app/api/auth/nonce?wallet=test
```

Full checklist: `api/DEPLOY.md`. Table/migration detail: `api/DB_SETUP.md`.

### The command center

- `https://<app>.vercel.app/api/admin/console` — phone-first operator console. Type `ADMIN_DASH_KEY`
  into the gate; writes prompt for `ADMIN_OPS_KEY` once per tab. Keys are held **in memory only** —
  never localStorage, never a cookie, never the URL.
- **Players is the default view:** 24-hour, 7-day and 30-day active players, UTC daily trend,
  sessions, new players and telemetry identity coverage. Active means a distinct non-anonymous
  player that emitted `session_start`; anonymous volume is shown separately and never miscounted
  as one player.
- **The read/write boundary is at the ENDPOINT, not in the UI.** `api/admin/db.js` and
  `api/admin/stats.js` are **SELECT-only by construction**. Never add a write path to them.
- **Balance is a tab (WO-1328, 2026-09-02).** The **Balance** tab edits the remote tunables
  (`docs/PROD022_TUNABLE_FLAGS.md`) from a phone: Save writes an override, Reset deletes the row so
  the knob answers the build default. **Reset is not "set 0"** — the art timeout resets to 20, not 0.
  Writes go to `POST /api/admin/ops` (`tunable.set` / `tunable.clear`) like every other ops write.
  The page is driven by a JSON manifest whose spine is **generated** from `RemoteTunables.Registry`;
  `test/tunables-manifest.test.js` goes red naming which two sources disagree. **Prices,
  entitlements, grants and purchase amounts are permanently out of scope** — real money, server
  authority, `api/_lib/purchase-catalog.js`.
- `tools/command-centre.ps1` is a **different thing that shares the name** — it is the guarded deploy
  chain (WO-1199), not the console.

---

## 9. THE DATABASE (Neon Postgres)

- `api/schema.sql` is the declaration. `tools/schema-parity.mjs` proves the live DB matches it and
  **parses only `CREATE TABLE` bodies**.
- **There is NO migration runner.** A column declared in a `CREATE TABLE` body but not yet run in
  production reads as DRIFT and **blocks every deploy** until a human runs SQL. Add such columns as a
  documented `ALTER` comment instead.
- **⛔ `CREATE TABLE IF NOT EXISTS` skips the table AND its CHECK constraints together.** It reports
  success and does nothing. On a repair, drop the guard and **verify by shape query**, never by the
  absence of an exception.
- **⛔ `ON CONFLICT DO NOTHING` does not back-fill an already-provisioned database.** That trap shut
  two dungeons in production. If rows must exist live, say so explicitly — the lead writes them.
- The `neon()` HTTP driver is **tagged-template only** (no `.query()`). For raw DDL use `Client`.
- **Do not split a migration file on semicolons** — it destroys `DO $$ ... $$` blocks (error 42601).
  Send the whole file as one batch.

---

## 10. F8 LIVE TRIAGE — the owner is never the bug detector

Hook-enforced via `.claude/settings.json`: SessionStart starts the daemon, UserPromptSubmit injects
un-acked captures, a Stop-hook poller rewakes an idle seat when a capture lands.

```powershell
powershell -File .\.claude\skills\run-defenders\f8-watch-start.ps1   # idempotent
.\.claude\skills\run-defenders\f8-check-inbox.ps1                    # OLDEST un-acked + pending=N
.\.claude\skills\run-defenders\f8-ack.ps1                            # acks exactly ONE
```

> **⛔ THE INBOX IS A QUEUE, NOT A SLOT.** `LATEST_CAPTURE.md` and `PING.json` hold only the NEWEST
> capture. The record is `logs/f8-inbox/QUEUE.jsonl`. **Never ack "the latest"** — keep triaging until
> `f8-check-inbox.ps1` reports `NO_CAPTURE`. Acking the newest once silently closed two of the owner's
> captures that no seat ever saw.

**On a fire, read the harvested `[Flow:*]` lines in `LATEST_CAPTURE.md` FIRST** — before any code
read, any agent, any theory. Spawning a code-reading agent before reading the captured trace is the
banned failure.

---

## 11. DEBUGGING — instrument, never guess (§12, binding)

> **No code edit on a non-trivial bug until you can cite CAPTURED DATA that proves the cause.**

Static code-reading **locates** candidates; it **never concludes** the cause. An inferred root is a
guess. Instrument with `FlowTrace.Enter/Step/Warn/Fail` and `Guard.Try`, run it (prefer headless),
read the trace, let the data pinpoint the dead step, fix THAT.

Split every "shows nothing" into *data-empty* vs *built-but-invisible* vs *threw-and-skipped* using
the trace, before touching code.

**⛔ NEVER STRIP FLOWTRACE.** Instrumentation is permanent. You may eventually flag it off
(`FlowTrace.Enabled=false`); the calls stay. Removing it is never "cleanup" — it discards the only
asset that makes the next bug cheap.

**Screenshots are primary evidence for visual defects.** FlowTrace shows what the code *believes*; the
screenshot shows what the player *gets*. On 2026-08-27 a banner passed `COMPILE_GATE_OK`,
`REGRESSION_OK 308/308` and its own dedicated regression — and the first screenshot showed the
operator's message truncated to `- Rai...`, plus a second producer the tests were aimed at that no
player ever reads.

**And verify your tooling before blaming the code.** A 390px "responsive bug" in that same session was
an artifact: headless Edge enforces a ~500px minimum window, so the layout viewport never changed. The
tell was that the element's offset was identical at 360 and 390. Filing it would have cost someone a
morning chasing correct CSS.

---

## 12. COMMIT AND PUSH DISCIPLINE

1. Gate the combined tree **once** (`COMPILE_GATE_OK` + `REGRESSION_OK`).
2. Stage **by explicit path**, one lane per commit. Never `git add -A`.
3. Flip the WO `**Status:**` line in the same commit as the work.
4. Commit messages: use `git commit -F <file>`. **PowerShell here-strings and
   `Set-Content -Encoding utf8` both corrupt messages** — write the file with .NET `WriteAllText`
   (no BOM) or a bash heredoc.
5. End messages with `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`.
6. **Push only after the owner retests/confirms** (felt/gameplay) or a regression passes (data/logic).
7. If the push is rejected for LFS: `git lfs push --all origin`, then push again.
8. If it is rejected by `pre-push` for R2 or schema parity, **fix the cause** — run `tools\r2-ship.ps1`
   or apply the migration. There is no override.

**Ambiguous tickets bounce back for detail.** No repro, no screen, no stack = never work blind.

---

## 13. WHAT TO DO WHEN YOU ARE UNSURE

- **State assumptions and keep building.** Deliver the whole scope; flag what is uncertain.
- **Ask only when different readings produce materially different work** — and ask with
  `AskUserQuestion`, with a recommendation, in the same turn you raise it. Never bury a decision in
  prose.
- **Never fabricate a fact you have not opened at source this session.** Speed is fine for lookups;
  judgement gets delegated or verified.
- **Report outcomes faithfully.** If tests fail, say so with the output. If a step was skipped, say
  that. "Done and verified" is a claim you must be able to point at a marker for.
- **The owner's statements are ground truth.** Act on them; do not re-derive or propose undoing her
  deliberate work.

---

## 14. THE RECURRING DISEASE — duplicated state

Nearly every expensive bug in this repo's history is **one fact written in a second place, which then
rotted.** Documented instances: the stale WO number block, the retired asmdef dependency table, the
hardcoded repo root, a schema version restated in a doc, `CurrentVersion - 1` in a test, a push+verify
pair copy-pasted into two chains, two producers of the same banner string, a denylist nearly copied
into a second file.

**When you find yourself writing a fact down a second time, point at the first instead.** When you
find two copies, delete one — do not "keep it for the tests", because that recreates the split
immediately.

---

## 15. KEEPING THIS FILE HONEST

Per `CLAUDE.md` §15: any commit that changes architecture, state, or procedure updates the relevant
load-bearing doc **in the same commit** — or adds a one-line `STALE:` flag at the top naming what is
now wrong. A state change with no canon update is an incomplete change.

If something in this runbook proved wrong while you were following it, **fix it before you move on.**
The next seat has no other way to know.
