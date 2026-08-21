# RULES — the one page. Read this, then read what it points at.

**This file is an INDEX of the binding rules. It is NOT their source.**
Every rule below is one checkable line plus a pointer to the doc that owns it. The pointer is the
authority; this page is the map. **Nothing here may be quoted as the reason a rule exists** — open
the source. If this page and a source doc ever disagree, **the source doc wins and this page is the
bug** (fix it in the same breath, §15).

Deliberately no deep content is copied here. A copied rule is a future contradiction: the copy drifts,
and then nobody knows which one is binding. That is exactly why "read the rules" needed a single target
instead of an eighth long doc.

**Scope:** binding on every seat — CLI, UI, every spawned agent, every session, forever.
*(Implements WO-938; the numbering banner is `CLI_LANES_WO_NUMBERS.md`.)*

---

## Precedence — when two sources disagree

Not invented here; each line is stated by the doc named.

1. **The owner's live ruling** beats every document.
2. **The newest `CANON_GROUND_TRUTH_<date>.md`** beats any other doc on current state — *(SESSION_CANON_LOADER.md, top banner; CLAUDE.md §15)*.
3. **The CODE beats the comments, and beats every doc, on what the software does** — *(CLAUDE.md, mandatory-first-step)*.
4. **The MARKER FILE beats any doc** on gate/suite/test results; never restate a count from prose — *(CLAUDE.md §8; docs/HANDOVER.md)*.
5. **The `CLI_LANES_WO_NUMBERS.md` banner is the SOLE work-order numbering authority** — no other file, ever — *(CLAUDE.md §2)*.
6. **`BOARD.html` is derived from `WorkOrders/*.md`**; the WO files are the data — *(docs/BOARD.md §1)*.
7. Anything else: **the source doc over this page.**

---

## ★ THE FIVE THAT GET VIOLATED MOST ★

These are not the five most important. They are the five the docs themselves record as **repeatedly
broken**. Answer yes to all five before anything else.

1. **INSTRUMENT FIRST — never inference-fix.** No code edit on a non-trivial bug until you can cite a
   CAPTURED LINE that proves the cause. Static reading locates candidates; it never concludes.
   *(CLAUDE.md §12 — the HARD GATE; memory `never-inference-fix`. Forged after 3 wasted cycles on the
   "pink floor"; one headless dump named it in a single read.)*
2. **MINT WO NUMBERS ONLY FROM THE BANNER, AND BUMP YOUR OWN ROW IN THE SAME EDIT.** Never copy a
   number out of any doc. *(CLAUDE.md §2 — five two-seat collisions in one day on 2026-08-02, including
   by the CLI. The mint written without the banner bump IS the collision.)*
3. **READ THE ALREADY-HARVESTED CAPTURE BEFORE YOU THEORISE.** F8 inbox / break-log / screenshots first
   — spawning a code-reader before reading the harvest is the banned failure. *(CLAUDE.md §14: "you have
   the answers yet choose not to look".)*
4. **COMMIT BY EXPLICIT PATH, ONE COMMITTER, NEVER `git add -A`.** The tree is shared by multiple
   sessions and agents. *(CLAUDE.md §11; memory `sole-git-committer` — two committers duel on
   `.git/index.lock` and produce false "pushed".)*
5. **UPDATE CANON IN THE SAME BREATH AS THE CHANGE.** A state change with no canon update is an
   incomplete change; if deferred, leave a dated `STALE:` flag. *(CLAUDE.md §15 — the rule that exists
   because one fleet-scale audit of 1090 files already had to happen once.)*

---

# ⚡ QUICK REFERENCE — "…and where do I start / how do I debug / how do I build?"

Orientation, not prohibition. **These lines are NOT numbered rules** — rules 1–102 below are the binding
index and are untouched. Same law applies: **each line is a pointer, not a copy.** Every fact here was
read at source on 2026-08-09; where a source doc disagreed with the disk, the disk is written here and
the disagreement is filed in CONFLICTS at the bottom. Cite the source, never this page.

---

## QR-1 · KEY ARCHITECTURE — what you must know before touching code

**Assemblies** *(the authority is the `.asmdef` files themselves — read them, not a table)*
- **QR-1.1** 19 `.asmdef` under `Assets/_Modules/` + `Assets/Data/DeNelle.Data.asmdef` + the editor asmdefs.
  → `Assets/_Modules/README.md` (per-module map) · `docs/ARCHITECTURE.md` (the hub — read before any
  single `*_ARCHITECTURE.md`). **CLAUDE.md §5's six-row table is a subset, not the map — see C-6.**
- **QR-1.2** **The load-bearing invariant is HUD ⇸ Village: `DeNelle.HUD` never references
  `DeNelle.Village`, in either direction.** Verified at source in `Assets/_Modules/HUD/DeNelle.HUD.asmdef`
  (references `DeNelle.Core` + `DeNelle.Data` only) — `Assets/_Modules/HUD/AdminOverlay.cs` reaches a
  Village type by reflection *precisely because* the asmdef forbids the reference. → CLAUDE.md §5 · rule 30
- **QR-1.3** **Cross-module calls go through `CoreServices`** — `Assets/_Modules/Core/CoreServices.cs`
  (`CoreServices.Hud`, `CoreServices.Audio`), always with `?.` (rule 32). → CLAUDE.md §5, §6
- **QR-1.4** Regression/editor code lives in the nested editor asmdef, never a runtime assembly. → rule 40

**Key interfaces** *(all `DeNelle.Core.*`; open the file, don't infer the contract)*
- **QR-1.5** `IDamageableStructure` → `Assets/_Modules/Core/Combat/IDamageableStructure.cs` — implementers
  need `using DeNelle.Core.Combat;` (rule 31). → CLAUDE.md §6
- **QR-1.6** `IVillageHud` → `Assets/_Modules/Core/HUD/IVillageHud.cs`, resolved via `CoreServices.Hud`.
- **QR-1.7** `IAudioService` → `Assets/_Modules/Core/Audio/IAudioService.cs`, resolved via `CoreServices.Audio`.

**Live scenes** *(all under `Assets/Scenes/`; verified present/absent on disk)*
- **QR-1.8** **Home hub = `Assets/Scenes/Main_Castle_Overworld.unity`** (merged world, one navmesh).
  → CLAUDE.md §7, §8
- **QR-1.9** **`Assets/Scenes/Village2.unity` = the raid target.** Raid/garrison/dungeon scenes sit
  beside it (`RaidBase_*`, `Garrison_*`, `DungeonCompose/*`). → CLAUDE.md §8
- **QR-1.10** **`Village.unity` and `OuterWorld.unity` are DELETED** — confirmed absent from the tree. A
  doc naming either is stale. → CLAUDE.md §7 · **C-4**
- **QR-1.11** ⚠ **`Assets/Scenes/MainCastle_Hall.unity` still EXISTS on disk and is NOT the hub** — a
  LEGACY file. That ambiguity is what keeps re-seeding stale docs. → CLAUDE.md §7
- **QR-1.12** Never hand-edit a curated `.unity`; rebuild through its builder (rule 56).

**Naming canon that bites**
- **QR-1.13** **Elarion**, never "Avalon". → CLAUDE.md §7 · `DESIGN-DECISIONS.md` #1
- **QR-1.14** **Hero tag = `Player`**, one tag per GameObject; set in `HeroControlEnsurer.Ensure`. → CLAUDE.md §7
- **QR-1.15** **Enemy AI finds the hero by COMPONENT** (`FindFirstObjectByType<HeroLocomotion>()`), **never
  by a tag** — the old `HeroTarget` tag was never declared. → CLAUDE.md §7 · rule 61
- **QR-1.16** Player-facing strings come from `canon-strings.json`, and **a display name is never an id**
  — see QR-5.7, the single most expensive naming trap in the repo.

**Shape of the code**
- **QR-1.17** **UXML does not work in player builds — UI is code-built uGUI.** `.uxml` assets DO still
  exist in-tree (e.g. `Assets/_Modules/HUD/VillageHud.uxml`); their presence is legacy, not permission.
  → CLAUDE.md §8 · rule 34
- **QR-1.18** **Presentation is a separate layer that NEVER touches the objects** — the doc's own
  most-violated principle. → `docs/ARCHITECTURE_PRINCIPLES.md` §2 · rule 19
- **QR-1.19** **One Model:** capability is a property on the entry; every system is a READER of the
  collection — never hard-code per type/tag. → `docs/ARCHITECTURE_PRINCIPLES.md` §2b · rule 20
- **QR-1.20** Be the SME from `docs/MASTER_CATALOG.md` + the `docs/MASTER_CATALOG/<area>.md` for what you
  are about to touch — **verified from CODE, because the comments lie**. → CLAUDE.md mandatory-first-step

---

## QR-2 · DEBUGGING — FlowTrace, Guard, and the F8 loop

- **QR-2.1** ★ **THE HARD GATE: no code edit on a non-trivial bug until you can cite CAPTURED DATA that
  proves the cause.** Static reading LOCATES candidates; it never CONCLUDES. → CLAUDE.md §12 · rules 44–48

**FlowTrace — `Assets/_Modules/Core/Diagnostics/FlowTrace.cs` (`DeNelle.Core.Diagnostics`)**
*API surface read at source; do not add a call you have not confirmed exists.*
- **QR-2.2** Point calls: **`Step`** (reached it) · **`Warn`** (fallback/anomaly) · **`Fail`** (error level
  → break-log). Never downgrade a real failure to `Warn` (rule 38).
- **QR-2.3** Hot paths: **`Throttle(system, key, everySeconds, msg)`** · **`Once(system, key, msg)`**;
  `ResetSession()` clears both. → rule 39
- **QR-2.4** Scoped: **`Measure(system, what, warnAboveMs)`** returns a `FlowTrace.Scope`;
  **`Enter(system, what)`** returns a `FlowTrace.FlowScope` (`using var _ = …`) that indents by call depth.
  **There is NO `FlowTrace.Exit` — the exit side is `FlowScope.Dispose()`,** which fires on scope end.
- **QR-2.5** `FlowTrace.Try(system, what, action)` / `Try<T>(…, fn, fallback)` log an exception at error
  level **independently of `Enabled`**, so a real throw can never be silenced.
- **QR-2.6** Toggles: `Enabled` (**defaults to `Application.isEditor || Debug.isDebugBuild`** — a release
  player ships tracing OFF), plus `Only(…)` / `Mute(…)` / `AllOn()` category filters and a swappable
  `Sink` (`ITraceSink`) reconfigured through `Configure(TraceConfig)`.
- **QR-2.7** ⛔ **NEVER STRIP FLOWTRACE (owner ruling 2026-08-09, BINDING).** Flagging OFF is allowed
  (`FlowTrace.Enabled=false`); **the calls STAY IN THE CODE.** A stripped `Warn`/`Fail`/`Guard` turns a
  logged failure back into a silent one. → CLAUDE.md §12 (the ⛔ block) · `docs/INSTRUMENTATION_STANDARD.md` §1.4

**Guard — `Assets/_Modules/Core/Diagnostics/Guard.cs`**
- **QR-2.8** `Guard.Try(system, what, action)` → `bool`; `Guard.Try<T>(…, func, fallback)` → `T`;
  **`Guard.TryEach(…)` → `(int built, int failed)`** — any loop building a list/grid/screen from N objects
  uses `TryEach`, so one bad object never blanks a screen. → rule 37
- **QR-2.9** **A `catch` that swallows without logging is forbidden.** No silent failures; every fallback
  is a `Warn`, every empty/skip/early-return is traced. → `docs/INSTRUMENTATION_STANDARD.md` §2 · rule 36
- **QR-2.10** Write the instrumentation IN as you author the method, not after a bug. → rule 35 · §2

**The F8 live-triage loop** *(watcher scripts in `.claude/skills/run-defenders/`)*
- **QR-2.11** **Start once:** `f8-watch-start.ps1` (idempotent; runs `f8-watch-daemon.ps1` hidden).
  **Poll:** `f8-check-inbox.ps1` FIRST every turn. **After triage:** `f8-ack.ps1`. **Stop:**
  `f8-watch-stop.ps1`. Legacy one-shot fallback: `f8-watch.sh`. → CLAUDE.md §14
- **QR-2.12** ★ **TRIAGE FROM THE HARVESTED LINES FIRST** — read `logs/f8-inbox/LATEST_CAPTURE.md` before
  any code-read, any agent, any theory. Spawning a code-reader before reading the harvest is the banned
  failure. → CLAUDE.md §14 · rule 47 · memory `never-inference-fix`
- **QR-2.13** The owner is NEVER the bug detector. → CLAUDE.md §14 · rule 53
- **QR-2.14** Prefer HEADLESS capture to self-serve before asking the owner to retest. → rule 50

**Where logs land** — *root-relative only; **the repo root is MACHINE-DEPENDENT — never hardcode it**
(CLAUDE.md §0 and `PREFLIGHT_GATE.md` B11 both name stale absolute paths, see **C-3**).*
- **QR-2.15** `logs/f8-inbox/` — daemon inbox: `LATEST_CAPTURE.md` + `PING.json`.
- **QR-2.16** `logs/debug/` — escalation logs. `logs/` — batchmode run logs.
- **QR-2.17** `Builds/<LogName>` — every `run-unity-method.ps1` run; `Builds/build.log` — the player build.
- **QR-2.18** **`break-log.jsonl` is written to the PLAYER's `Application.persistentDataPath`, not the
  repo** (+ a PNG per capture) — `Assets/_Modules/Core/Diagnostics/BreakCaptureHarness.cs`. The daemon is
  what brings it into `logs/f8-inbox/`.

---

## QR-3 · BUILD — the commands that actually work

- **QR-3.1** ★ **THE MARKER IS THE EVIDENCE — NEVER THE EXIT CODE.** `run-unity-method.ps1` judges from
  *log text* (`Exiting batchmode successfully`), not from a marker, so it **can exit 0 on a run that
  refused or FAILED**. Verify the marker, the log's freshness, and its size. → rule 79 · CLAUDE.md §8 ·
  memory `gates-report-success-without-proving-it`
- **QR-3.2** **Unity must be CLOSED for batchmode** (project lock). Both `run-unity-method.ps1` and
  `build-windows.ps1` refuse with **exit 3** if a `Unity` process is running. → CLAUDE.md §3 · rule 57

**The gates** *(entry points verified at source — all four classes are namespace **`DeNelle.Editor`**)*
- **QR-3.3** `DeNelle.Editor.CompileGate.Run` → **`COMPILE_GATE_OK`** — `Assets/Editor/CompileGate.cs`.
  Also scans every `.cs` for NUL bytes and withholds the marker on a hit (rule 29).
- **QR-3.4** `DeNelle.Editor.DataRegression.RunAll` → **`REGRESSION_OK <n>/<n> suites`** — **THE** gate.
  `Assets/Editor/Regression/DataRegression.cs`. ⚠ **Namespace is `DeNelle.Editor`, NOT
  `DeNelle.Editor.Regression`** — the folder is `Regression/`, the namespace is not. This has bitten before.
- **QR-3.5** `DeNelle.Editor.RegressionSuite.RunAll` → **`CHECKIN_SUITE_OK <p>/<n> cases`** (legacy smoke
  battery) — `Assets/Editor/RegressionSuite.cs`.
- **QR-3.6** `DeNelle.Editor.SessionRegression.RunAll` → **`SESSION_GUARDS_OK <n>/<n> checks`** —
  `Assets/Editor/Regression/SessionRegression.cs`.
- **QR-3.7** `DeNelle.Editor.UICaptureLaunch.RunCaptureHeadless` → **`UI_CAPTURE_OK <count>`** —
  `Assets/Editor/UICaptureLaunch.cs`. **Green marker ≠ correct screen: OPEN THE PNGs.** → rules 78, 83
- **QR-3.8** **These markers are DISTINCT on purpose** (2026-08-02). Until then all three regression entry
  points printed a bare `REGRESSION_OK`, so a 22-case battery read as the full gate. Never read one as
  another; never restate a count from a doc — read it off the marker. → rules 80, 81
- **QR-3.9** Runner: `run-unity-method.ps1 -Method <FullyQualified.Method> -LogName <name>.log`
  (`-TimeoutMin`, `-BuildTarget`). Worked examples + the marker table: **`.claude/skills/run-defenders/SKILL.md`**.

**The Windows player build**
- **QR-3.10** `build-windows.ps1` → **`Builds/Windows/DefendersOfTheRealm.exe`**, log `Builds/build.log`.
  It wipes `Builds/Windows` first (a stale exe stub against fresh scenes = native crash) and stops a
  running player by name. Method: `DeNelle.Editor.DesktopBuild.BuildWindows`, target pinned `Win64`.
- **QR-3.11** ⚠ **After ANY Android/APK build, pass `-BuildTarget Win64`** — the APK leaves the project's
  active target on Android and the next desktop build dies in SBP/Addressables ("Native extension for
  Android target not found"), which the runner reports as a *generic* failure. → `run-unity-method.ps1`
  header · memory `desktop-build-after-android-target`
- **QR-3.12** Wipe + rebuild the exe unprompted after a gate-green commit wave. → memory `wipe-rebuild-exe-on-ready`
- **QR-3.13** A **"LICENSE ERROR"** line is usually a MISDIAGNOSIS — see QR-5.9.
- **QR-3.14** ⛔ **A build is not shipped until its content is pushed:** enemy/structure art is served from
  R2 with **no local fallback** and **content-hashed** bundle names, so every build needs its own push —
  `tools\r2-ship.ps1` → **`R2_PARITY_OK`**. Never raw `adb install`. → **CLAUDE.md §16** (full rule)

---

## QR-4 · SERVICES / INFRA — what runs where

Resolved from the tree on 2026-08-09, not from assumption. Where the tree could not settle it, it says so.

- **QR-4.1** **Vercel — the `api/` serverless backend IS IN THIS REPO**, git-tracked; it is *not* a separate
  project. ~21 routes under `api/` (`api/bug-report.js`, `api/trace.js`, `api/admin/db.js`,
  `api/game/save.js`, `api/game/load.js`, `api/auth/nonce.js`, `api/events/track.js`, `api/leaderboard/*`,
  `api/profile/*`, `api/promo/*`, `api/referral/*`, `api/pi/verify.js`, `api/tower-swap/log.js`, …). ⚠
  There is **no `api/wallet-auth` endpoint** — wallet auth is the shared lib `api/_lib/wallet-auth.js`
  and the challenge route is `api/auth/nonce.js`. Config: `vercel.json`, `.vercelignore`,
  `.vercel/project.json` (project `defenders-of-the-realm-v2`). → `api/DEPLOY.md` · memory `api-backend-in-repo`
- **QR-4.2** **Vercel also serves the WebGL build** — `vercel.json` sets `outputDirectory: Builds/WebGL`,
  `git.deploymentEnabled: false` (CLI-only). Deploy scripts `overnight-webgl-deploy.ps1` /
  `webgl-vercel-overnight.ps1` are **PREVIEW-ONLY by design — never `--prod`.** A second, separate Vercel
  project holds the marketing/legal one-pager: `site/` (`site/.vercel/project.json` → `echoes-of-elarion`).
  - ⚠ **READ THE SCOPE OF THAT RULE (correction 2026-08-10, verified against the live Vercel deployment
    record).** "PREVIEW-ONLY, never `--prod`" is **still true and still correct — it is a statement about
    the SCRIPTS, not about the state of production.** Production has been promoted **by hand**, repeatedly:
    `vercel deploy --prod` on **2026-08-03T22:50Z**, **2026-08-04T19:33Z** and **2026-08-05T23:37Z**
    (`dpl_9vGadbKyPrQ55HR3PaUT53i9CNUh`, commit `8fdb29a5`), and the 2026-07-16 build went live by
    `vercel promote`. **The scripts never touched prod; a human did.**
  - **This distinction IS the bug it caused.** Multiple docs read this tooling guarantee as a guarantee
    about state and concluded *"`api/` cannot have reached production"* — which is how
    `docs/HANDOVER.md`, `CANON_GROUND_TRUTH_2026-08-09.md` and `docs/reference/AUDIT_2026-08-09.md` §5 all
    carried "prod runs OLD `api/` code" for a week after it stopped being true (the AUDIT built a
    **security** argument on it: *"prod running old code is what's protecting you"* — the mitigation never
    existed). **A rule about what a script does can never be evidence about what production is running.
    Check the deployment record.**
  - **There is no WebGL-only promotion from this repo.** `.vercelignore:17` (`!/api`) re-includes `/api`,
    so **every `--prod` from the repo root re-ships the serverless backend to production alongside the
    static payload.** Any plan of the form "ship the game build but hold `api/` back" is unimplementable
    as the tree stands.
  - **PROMOTION TECHNIQUE (owner-ruled 2026-08-10, and the one to reach for at 2am):** build →
    **deploy to PREVIEW** → **verify the preview URL actually serves the new build** →
    **`vercel promote <that exact preview url>`**. Deliberately **NOT `--prod`**: promoting a verified
    preview ships **the artifact you inspected**, whereas `--prod` re-uploads a fresh, uninspected one.
    Run it from the **repo root** (see the `Builds/WebGL` stray-project trap in
    `docs/webgl-hosting-notes.md`), and **overwrite `Builds/PROD_ROLLBACK.txt` with the OUTGOING prod
    deployment id BEFORE promoting** — recorded afterwards it points at the thing you are escaping.
- **QR-4.3** **Neon Postgres — ONE database. There is NO prod/dev split.** The only env var is
  **`DATABASE_URL`** (22 call sites in `api/`; no `POSTGRES_URL`, no `NEON_*`, no Neon branch anywhere in
  the tree). Preview and production therefore read and write the SAME database. Schema: `api/schema.sql`
  (`player_data`, `analytics_events`, `bug_reports`, `auth_nonces`, `leaderboard_scores`, …). Saves:
  `api/game/save.js` + `api/game/load.js`. **The secret is not in the repo** — it is a Vercel dashboard
  env var. → `api/DB_SETUP.md` §1.3, §6.1
- **QR-4.4** **Firebase — Auth and App Distribution are REAL; Firestore is NOT used.** Auth (email/password
  + Google) is live C#: `Assets/_Modules/Core/Auth/FirebaseAuthService.cs`, consumed by
  `Assets/_Modules/Onboarding/LoginViewModel.cs`; the Unity SDK **is** imported
  (`Assets/Firebase/Plugins/*.dll`). App Distribution ships the Seeker APK via `distribute-android.ps1`
  (chained by `morning-ship-chain.ps1`). **Zero Firestore references in `Assets/_Modules`; no
  `firebase.json`, no `.firebaserc`.** Firebase is **ACCESS ONLY** — it binds no save key; the save key is
  the wallet address and backend auth is the `X-Wallet`/`X-Nonce`/`X-Signature` scheme in
  `api/_lib/wallet-auth.js`. The SDK has no WebGL support, so a stub compiles under `UNITY_WEBGL`.
  Config: `Assets/google-services.json` + `firebase-appid.txt` (**both gitignored, local-only**).
  → memory `firebase-app-distribution`
- **QR-4.5** **itch.io — USED, and it is the live public web host.** Not merely referenced: `ship-webgl.ps1`
  pushes `Builds/WebGL` with `butler` to `denellestudios/defenders-of-the-realm-defend-the-tower:html5`
  (also `-Ship` in `build-webgl-isolated.ps1`, an itch packaging path in `build-webgl.ps1`). itch rejects
  the uncompressed build (per-file limit), so it ships **Brotli + `decompressionFallback=true`**. Credential
  is butler's own machine-local login — nothing in-repo. → `DEPLOY_WEBGL_ITCH_GUIDE.md`
- **QR-4.6** **Solana — DEVNET only, and hard-blocked off mainnet.** Network default:
  `Assets/_Modules/Wallet/WalletService.cs` (`DefaultNetwork = WalletNetwork.Devnet`); endpoints/mints:
  `Assets/_Modules/Wallet/WalletEndpoints.cs` (the SKR mints are **deliberately empty strings** so a
  transfer fails loudly); `Assets/_Modules/Wallet/SolanaWalletProvider.cs` **hard-blocks** Mainnet in
  `SendPayment`. `SOLANA_SDK` is defined for **Android only** (`ProjectSettings/ProjectSettings.asset`), so
  desktop/WebGL fall to the stub provider. Wallet = identity + payments; non-custodial, no key in the tree.
  → memory `android-seeker-distribution-and-wallet-strategy`
- **QR-4.7** **Monetization is DELIBERATELY OFF.** The gate is
  `FeatureFlags.RealmStorePurchase` (`defaultOn: false`) in `Assets/_Modules/Core/FeatureFlags.cs`,
  re-gated OFF and locked on 2026-08-08. **Do not restate the ladder — read it:**
  `docs/reference/MONETIZATION_ACTIVATION_LADDER.md`. ⚠ `Get()` reads **PlayerPrefs first**, so a stored
  `ff.realmstorepurchase=1` beats the default (QR-5.10).
- **QR-4.8** ⚠ **AMBIGUOUS — needs an owner ruling: is the `site/` project actually deployed?**
  `site/README.md` says in bold **"⛔ NOT DEPLOYED"** with a pre-publication banner and `noindex`, while
  `KEY_FACTS.md` and `docs/HANDOVER.md` claim `echoes-of-elarion.vercel.app/terms` is live and
  HTTP-200-verified. The tree cannot settle it. **Do not paper over this — ask.**
- **QR-4.9** ⚠ **AMBIGUOUS — needs an owner ruling: should `defenders-of-the-realm-v2` PRODUCTION be
  promoted?** Canon says `api/` is preview-only, yet the client hardcodes the prod domain in 11 places
  (e.g. `Assets/_Modules/Core/State/GameStateService.cs`, `Assets/_Modules/Core/Diagnostics/WebTrace.cs`).
  The newest `CANON_GROUND_TRUTH_<date>.md` marks it explicitly as an owner call.

---

## QR-5 · TIPS & TRICKS — the scar tissue

> **THE INCLUSION BAR: only failures this repo has hit ~10+ times.** Judge frequency from the repo's own
> evidence — how many `WorkOrders/*.md`, `*.RESULT.md`, `docs/reference/AUDIT_*`, RCA notes and code
> comments name the same failure. **One occurrence is not a tip.** A list that accepts everything stops
> being read, which is the exact failure this bar prevents. Adding an entry? Cite the recurrence.

*The family resemblance across QR-5.1, QR-5.2 and QR-5.3: **a thing that looks wrong in the world is
almost always an IMPORT / CONVENTION / MATERIAL fault — not a mesh fault and not an animation fault.***

- **QR-5.1 · MAGENTA / PINK RENDER** *(the archetype — the single most-referenced failure in the repo)*
  **SYMPTOM:** an object or the ground renders pink/magenta **in the built player**, fine in the editor.
  **CAUSE:** the material sits on a Built-in/Standard/Legacy/Phong shader (or an unreferenced Shader
  Graph) → **stripped from the URP build** → resolves to `Hidden/InternalErrorShader`. Never a mesh fault.
  **FIX:** the global net is `Assets/_Modules/Core/MagentaGuard.cs` (`IsBrokenShader` / `ResolveUrpLitShader`
  are the single authority for "would this render magenta"); targeted fixers
  `Assets/_Modules/Core/TripoMaterialFixer.cs` and the editor menu **`Defenders/Art/Fix Polyperfect URP
  Materials`** (`Assets/Editor/PolyperfectUrpFix.cs`). ⚠ **An editor-only scan finds nothing** — the strip
  only happens in a build. The "pink floor" is the §12 origin story (3 wrong cycles guessed; one headless
  `FloorDiag` dump named it in one read). → CLAUDE.md §12
- **QR-5.2 · THE TRIPO ROTATION** *(named repeat offender; RCA is verbatim in the source)*
  **SYMPTOM:** a Tripo/AccuRig/CC enemy faces the wrong way — or is **lying on its back**.
  **CAUSE:** export-convention axis mismatch. Vendor rigs author **+X-forward**; the KayKit rigs already
  face **+Z** and must NOT be rotated. **Applying a PITCH instead of a YAW tips the model over.**
  **FIX:** a **-90 YAW ONLY** — `Quaternion.Euler(0f, -90f, 0f)` — applied to the **visual child** via
  `skinOpts.LocalRotation`, in `Assets/_Modules/Village/Enemies/EnemyFactory.cs`. Opt in by **NAME**: add
  to the `AccuRigIntake` set (`Troll`, `Troll_Mage`, `Troll_Overlord`, `Skeleton_Golem_NEW`,
  `Necromancer_NEW`) — **rig CLASS does not imply export convention, so nothing blanket-rotates.**
  ⚠ **The RCA, in that file's comments:** the old `Euler(-90,-90,0)` **X-pitch laid the troll ON ITS BACK**
  — *proven by captured data* (`DiagGarrisonRoster`: `worldUp=(1,0,0)`, `localEuler (270,270,0)`,
  `tipped=True`), not guessed. Oracle: `Assets/_Modules/DevTools/AutoPilotDriver.cs` (`DiagGarrisonRoster`).
- **QR-5.3 · T-POSE + SLIDING**
  **SYMPTOM:** the character holds a bind/T-pose while the NavMeshAgent slides it — "the sliding statue".
  **CAUSE:** a **RIG/IMPORT** fault, not an animation fault — no valid Humanoid avatar, or a **Generic**
  clip against a Humanoid rig (or the inverse). A Generic clip cannot retarget onto a Humanoid avatar.
  **FIX:** guard in `Assets/_Modules/Village/Enemies/EnemyAnimatorFactory.cs` (checks BOTH directions —
  the original guard was gated on `isHuman` and let Generic rigs straight through); hero side
  `Assets/_Modules/Village/Hero/HeroBodySwapper.cs`; repair via the editor menu **`Defenders/Maintenance/
  Fix AccuRig Enemy Rigs (Generic -> Humanoid)`** (`Assets/Editor/Maintenance/HumanoidRigFixup.cs`) —
  which then *verifies*, because "a flipped flag is not an avatar".
- **QR-5.4 · A CARD OR ICON SHOWING A BARE LETTER**
  **SYMPTOM:** a build card renders a single gilt letter on a dark plate.
  **CAUSE:** **the art exists and the RESOLVER missed it** — the letter is a fallback, not missing art.
  Usually a portrait filename that doesn't match the catalog id, or art hung off a *display-name slug*
  creative later renamed. **FIX:** add the alias to `PortraitAliases` in
  `Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs` (the fallback lives in the same file); ratcheted by
  `Assets/Editor/Regression/BuildCardArtRegression.cs`. Law in the source: *a portrait must not hang off a
  label creative can change at any time.*
- **QR-5.5 · BLACK-ON-BLACK UI**
  **SYMPTOM:** a panel floating over the world is invisible or has no discernible edge.
  **CAUSE:** `ObsidianFill` is `(0.02, 0.02, 0.025, 0.98)` — **near-black** (~#050506).
  **FIX:** it is only legible because it ships with **its own gold edge** (`ObsidianTrim` +
  `ObsidianTrimPx`) — build through `ElarionUiKit.BuildObsidianPanel`, in
  `Assets/_Modules/Core/UI/ElarionUiKit.cs`. Anything bypassing it inherits the near-black with no edge.
- **QR-5.6 · DUAL-COPY JSON — `Resources` WINS**
  **SYMPTOM:** you edited a catalog and nothing changed in-game.
  **CAUSE:** `Assets/Resources/Data/Canonical/` is read FIRST on every platform; the `StreamingAssets`
  copy is only a desktop fallback — so a StreamingAssets-only edit is invisible. **The two copies must
  stay BYTE-IDENTICAL. FIX/authority:** `Assets/_Modules/Core/Data/CanonicalJson.cs` →
  `Assets/_Modules/Core/Data/LocalJsonCatalogSource.cs`; enforced by cases in
  `Assets/Editor/Regression/DataRegression.cs` (`catalog-byte-equal`).
- **QR-5.7 · DISPLAY NAMES ARE NOT IDS** *(the WO-840 "naming inversion")*
  **SYMPTOM:** you edit the building you think you named, and a different one changes.
  **CAUSE:** the names are **inverted** relative to intuition. Verified in
  `Assets/Resources/Data/Canonical/buildings.json` + `canon-strings.json`: id **`workshop`** renders
  **"Forge"** (the WEAPONS shop) · id **`forge`** renders **"Armorer"** (the ARMOR shop) · id **`armorer`**
  renders **"Blacksmith"** — *it is NOT the Armorer* · **"Lumber Mill" is TWO ids** (`lumbermill` and
  `collector_lumbermill`). **FIX:** always route through the id. Related: a `displayName` key missing from
  `canon-strings.json` renders the literal `[[missing:<key>]]` on the nameplate, and a *shared* key gives
  two buildings one name — both have shipped.
- **QR-5.8 · THE MARKER, NEVER THE EXIT CODE** — a gate "passed" (exit 0) but nothing built or ran.
  Unity forks, so the wrapper's exit code is meaningless. **FIX:** QR-3.1 + rules 79–81. *Corollary from
  the same family: **two artifacts that should differ but have IDENTICAL FILE SIZES are a defect signal**
  — the tell is visible in the listing before the wrong picture is (`WORK_ORDER_1010…RESULT.md`).*
- **QR-5.9 · "LICENSE ERROR" IS USUALLY A MISDIAGNOSIS**
  **SYMPTOM:** a batchmode log carries `ResponseCode: 505` / `HandshakeResponse reported an error`.
  **CAUSE:** Unity 6 routinely fails the FIRST license handshake and auto-recovers; the line prints on
  fully successful runs. `build-windows.ps1` only flags it when there is **no** subsequent
  "Successfully updated license" recovery line. **FIX:** judge by the success marker.
  ⛔ **NEVER `Stop-Process` Unity Hub or `Licensing.Client`** — that breaks the license channel and costs a
  reboot. → `CLI_GATEKEEPER_PLAYBOOK.md`
- **QR-5.10 · A FLAG DEFAULT CHANGE DOES NOTHING ON A USED MACHINE**
  **SYMPTOM:** you flip a `FeatureFlags` default, rebuild, and behaviour is unchanged — on your machine.
  **CAUSE:** `FeatureFlags.Get` reads **PlayerPrefs FIRST**, so a previously-set local value shadows the
  new default — and the machine that "verified" it is the one holding the stale pref.
  **FIX:** clear the pref or test a clean profile — `Assets/_Modules/Core/FeatureFlags.cs`; verify a flag's
  SCOPE too, before assuming its reach. → `KEY_FACTS.md` (⚠ TRAP entries)
- **QR-5.11 · TOFU BOXES, AND MEANING CARRIED BY COLOUR ALONE**
  **SYMPTOM:** UI text renders as `□□□` on device; or two states are genuinely indistinguishable.
  **CAUSE:** a non-ASCII glyph missing from the TMP atlas; and **the owner is red/green colourblind**, so
  an affordable-vs-unaffordable TINT carries zero information. **FIX:** keep TMP strings ASCII, and give
  every state a **word + shape**, never a colour alone (e.g. `NEED 80W 30I`). → `KEY_FACTS.md` · CLAUDE.md §7
- **QR-5.12 · NEVER INFERENCE-FIX** — the meta-tip the other eleven keep proving. A plausible fix *feels*
  like progress and costs N blind cycles; instrumenting *feels* slow and costs one read. **If you cannot
  point at the data line, you have not earned the edit.** → CLAUDE.md §12 · rules 44–48

---

# A. Before you touch anything

6. **Answer PREFLIGHT GATE A out loud, unprompted, before your first edit of a session.** One NO or one
   unproven YES = stop. → `PREFLIGHT_GATE.md` Gate A · CLAUDE.md preflight banner
7. **Load `SESSION_CANON_LOADER.md` every session, before anything else.** → CLAUDE.md read-first
8. **Read `docs/MASTER_CATALOG.md` + the `docs/MASTER_CATALOG/<area>.md` for what you are about to
   touch.** Mandatory first step, every session. → CLAUDE.md mandatory-first-step
9. **Read the newest `CANON_GROUND_TRUTH_<date>.md` and confirm nothing there contradicts your plan.**
   → `PREFLIGHT_GATE.md` A2 · CLAUDE.md §15
10. **Be the SME BEFORE you change anything — verified from the CODE, not the comments.** No fixing,
    building, or claiming-fixed on assumptions. → CLAUDE.md mandatory-first-step
11. **Never assert a fact you have not opened at source this session.** Read-before-assert applies to
    code AND docs. → SESSION_CANON_LOADER.md (Day-1 boot) · memory `assert-only-what-you-read-at-source`
12. **Check the README index system before grepping or exploring** — `PROJECT_INDEX.md`,
    `Assets/README.md`, `Assets/_Modules/README.md`, `docs/README.md`. → CLAUDE.md Navigation
13. **Confirm the system does not already exist. Extend it; never greenfield a duplicate.**
    → `PREFLIGHT_GATE.md` A3 · memory `dont-greenfield`
14. **Delegate the deep dig to agents; your hands stay on gates and commits.** Do not solo-charge what
    an agent should go deep on. → CLAUDE.md §11 · `PREFLIGHT_GATE.md` A4
15. **Run file-disjoint lanes in parallel; one agent per shared file.** `VillageSceneBuilder.cs` is a
    serialization bottleneck — one toucher at a time. → CLAUDE.md §9
16. **Ambiguous ticket (no repro / screen / stack) bounces back for detail. Never work blind.**
    → CLAUDE.md §11

# B. Architecture law — the shape the code must take

17. **Decision lens: what is RIGHT, not what is easy. When they diverge, name the divergence out loud.**
    → `docs/ARCHITECTURE_PRINCIPLES.md` §0
18. **Bounded context per component — one job, deliberately limited scope, never reaches outside its
    lane.** → `docs/ARCHITECTURE_PRINCIPLES.md` §1
19. **Presentation is a separate layer that NEVER touches the objects.** Nothing about how a thing looks
    lives on the thing. → `docs/ARCHITECTURE_PRINCIPLES.md` §2 (the most-violated principle, per the doc)
20. **One Model: capability is a property on the entry; never hard-code per type/tag. Every system is a
    READER of the collection.** → `docs/ARCHITECTURE_PRINCIPLES.md` §2b
21. **POOL by default, and ONE pool/owner per concern.** Anything spawned more than once comes from a
    pool. → `docs/ARCHITECTURE_PRINCIPLES.md` §2b.1, §2b.2
22. **Structural work ships with the tests that prove behavior was preserved** — tests are the permission
    gate. → `docs/ARCHITECTURE_PRINCIPLES.md` §2c
23. **Queue by leverage: player-felt vs holistic. NEVER smuggle a structural refactor into player-facing
    work.** → `docs/ARCHITECTURE_PRINCIPLES.md` §3 · CLAUDE.md architecture banner
24. **Derive orientation/grip/seat/scale from mesh bounds + asset name — never a guessed Euler, never
    identity. A `manual=true` correction is canon and is never overwritten.**
    → `docs/ARCHITECTURE_PRINCIPLES.md` §4 · `docs/WEAPON_ARMOR_ORIENT_LOGIC.md`
25. **MVVM strict: the VM holds all logic/state; the View is a dumb skin that reads no game state.**
    → SESSION_CANON_LOADER.md Core Rules · `docs/UI_MVVM_BINDING_MAP.md`
26. **Behaviour changes are flag-gated.** → SESSION_CANON_LOADER.md Core Rules

# C. Writing code

27. **Write/Edit on the Windows path ONLY. Never `cat >`, `echo >>`, or any bash redirect into a `.cs`.**
    The Linux mount does not sync reliably and silently garbles files. → CLAUDE.md §0
28. **Brace-balance check every `.cs` you touched, before reporting done.** → CLAUDE.md §1
29. **No NUL bytes in any `.cs`** — the compile gate scans for them and withholds the marker.
    → CLAUDE.md §1 (WO-434)
30. **Respect the assembly boundaries: Village → Core only, HUD → Core only, never Village ↔ HUD.**
    Cross-module calls go through `CoreServices`. → CLAUDE.md §5
31. **`using DeNelle.Core.Combat;` in any file implementing `IDamageableStructure`.** → CLAUDE.md §6, §10
32. **Null-conditional (`?.`) on every cross-module service call.** → CLAUDE.md §10
33. **No new `System.Reflection` in bridge scripts.** → CLAUDE.md §10
34. **Never use UXML — it does not work in builds. UI is code-built.** → CLAUDE.md §8
35. **Write the instrumentation IN as you author the method, not after a bug** — flow entry, every branch
    taken, every fallback, every resolve, the render/commit seam.
    → `docs/INSTRUMENTATION_STANDARD.md` §2
36. **No silent failures. Every `catch` logs; every fallback is a `Warn`; every empty/skip/early-return is
    traced.** "Shows nothing, no error" must be impossible. → `docs/INSTRUMENTATION_STANDARD.md` §2 ·
    CLAUDE.md §12.2
37. **Any loop that builds a list/grid/screen from N objects uses `Guard.TryEach`** — one bad object never
    blanks a screen. → `docs/INSTRUMENTATION_STANDARD.md` §3
38. **Real failures use `Fail` (error level → break-log). Never downgrade one to `Warn` to keep the log
    clean.** → `docs/INSTRUMENTATION_STANDARD.md` §5
39. **Hot-path logs use `Throttle`/`Once` or a guarded call.** → `docs/INSTRUMENTATION_STANDARD.md` §1.3
40. **Regression code lives in the nested editor asmdef, never in a runtime assembly.**
    → `docs/INSTRUMENTATION_STANDARD.md` §4
41. **Instrument existing code ON TOUCH, never as a big-bang sweep.**
    → `docs/INSTRUMENTATION_STANDARD.md` §6
42. **One thing at a time, fully verified before the next. Deliver complete — no piecemeal.**
    → SESSION_CANON_LOADER.md Core Rules

# D. Debugging — the hard gate (most-violated; give it weight)

43. **Answer PREFLIGHT GATE B the moment a bug appears.** → `PREFLIGHT_GATE.md` Gate B
44. **No code edit until CAPTURED DATA proves the cause.** Instrument → run (prefer headless) → read the
    trace → fix the step the data names. → CLAUDE.md §12 ★
45. **Static code-reading LOCATES candidates; it NEVER CONCLUDES the cause.** An inferred root is a guess.
    → CLAUDE.md §12
46. **Instrumenting is the OPENING move, unprompted — not a fallback after a guess fails.**
    → CLAUDE.md §12
47. **Read the already-harvested F8 capture FIRST** — before any code-read, any agent, any theory.
    → CLAUDE.md §14
48. **Cite the exact proving line.** If you cannot point at it, you have not earned the edit.
    → `PREFLIGHT_GATE.md` B9 · memory `never-inference-fix`
49. **Split every "shows nothing" into data-empty vs built-but-invisible vs threw-and-skipped from the
    trace, before touching code.** → CLAUDE.md §12.3 · `PREFLIGHT_GATE.md` B10
50. **Prefer headless capture to self-serve** — AutoPilot fleet, `break-log.jsonl`, on-load dumps —
    before asking the owner to retest. → CLAUDE.md §12.4
51. **Two failed fix attempts on the same issue → STOP and escalate with logs. Do not solo-iterate a
    third time.** → `PREFLIGHT_GATE.md` B11 · memory `two-failure-escalate-to-grok`
52. **Every RCA hand-off carries a PROOF section: the verbatim captured line, its source, and what it
    proves.** Never a narrative-only RCA. → `docs/TICKET_PIPELINE.md` §0
53. **Keep the F8 watcher running and poll its inbox every turn; ack after triage.** The owner is NEVER
    the bug detector. → CLAUDE.md §14
54. **"Works on my machine" / "it doesn't stop the demo" / "probably just noise" are BANNED answers.**
    → `docs/ARCHITECTURE_PRINCIPLES.md` §5
55. **Verify proactively — audit the flow before you are asked and before you call it done.**
    → `docs/ARCHITECTURE_PRINCIPLES.md` §5

# E. Scenes, assets + naming canon

56. **NEVER hand-edit a curated `.unity` scene.** Rebuild through its builder. → CLAUDE.md §3 ·
    memory `owner-prefs-scenes`, `dungeon-scene-shared-tree-corruption`
57. **Never run a bake while the Unity editor is open** (project lock). → CLAUDE.md §3
58. **UI does not fire batchmode. Bake/build commands go to CLI in a work order.** → CLAUDE.md §3
59. **Polyperfect: use the `_M` quality tier only, check `docs/polyperfect-asset-catalog.md` before
    naming a prefab, and `LogWarning` (never error) on a missing one** — the pack is gitignored.
    → CLAUDE.md §4
60. **Use the canon names.** Elarion (never "Avalon"); hero tag `Player`; home hub
    `Main_Castle_Overworld`; player-facing strings from `canon-strings.json`. → CLAUDE.md §7
61. **Enemy AI finds the hero by component, not by a tag.** → CLAUDE.md §7

# F. Work orders + the board

62. **Take every new WO number from the `CLI_LANES_WO_NUMBERS.md` banner — the SOLE authority — and bump
    your seat's own banner row IN THE SAME EDIT as the mint.** → CLAUDE.md §2 ★
63. **Never copy a WO number out of any other doc** (not the filesystem max, not a backlog doc, not a
    handover). Every copy goes stale. → CLAUDE.md §2 · docs/HANDOVER.md
64. **The two seats mint from DISJOINT blocks; collisions resolve first-on-disk-and-referenced-wins.**
    → CLAUDE.md §2 · the banner's block table (**read the block ranges off the banner — see Conflict C-1**)
65. **Save work orders to `WorkOrders/WORK_ORDER_NNN_short_name.md` with files-to-edit, acceptance
    criteria, and what NOT to touch; mark `Status: READY TO IMPLEMENT` when the spec is complete.**
    → CLAUDE.md §2
66. **The `**Status:**` line must contain one canonical keyword.** `Unlabeled` is a defect in the WO file,
    not a category. → `docs/BOARD.md` §3
67. **Flip the status line AND write the `.RESULT.md` in the SAME COMMIT as the work.** A deferred flip is
    a board that lies until later. → `docs/BOARD.md` §2 · CLAUDE.md §2
68. **A RESULT file is written by the seat that verified the work — never fabricated to clear debt.**
    → docs/HANDOVER.md
69. **Regenerate `BOARD.html` (`python tools/board_build.py`) at session boot and before any board read.
    Never hand-edit it** — it is generated output. → `docs/BOARD.md` §2
70. **Never mirror to Notion — no writes, no reads — whatever older docs say.** → `docs/BOARD.md` §2 ·
    CLAUDE.md §2 · memory `notion-retired-board-is-derived`
71. **A new status keyword requires editing `tools/board_build.py` AND `docs/BOARD.md` §3 in the same
    commit.** → `docs/BOARD.md` §4

# G. Committing

72. **There is exactly ONE committer (the CLI/lead seat). No second session or agent commits or pushes.**
    → CLAUDE.md §11 · `docs/TICKET_PIPELINE.md` §7
73. **Stage by EXPLICIT PATH. Never `git add -A`. Never blind-replace a file.** → CLAUDE.md §11 ★
74. **Review every diff before staging — the tree is shared, and mount-garble is a live risk (§0).**
    → CLAUDE.md §11
75. **Commit local; PUSH ONLY after the owner felt-verifies, or a regression proves it.**
    → CLAUDE.md §11 · `PREFLIGHT_GATE.md` C15 · `docs/TICKET_PIPELINE.md` §7
76. **Other sessions write and signal "ready"; the one committer reconciles by path.** → CLAUDE.md §11

# H. Gates

77. **Answer PREFLIGHT GATE C before you say DONE.** → `PREFLIGHT_GATE.md` Gate C
78. **Pre-ship gates are `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` + `UI_CAPTURE_OK` — and you
    OPEN the PNGs — plus `R2_PARITY_OK` on any build that reaches a device or a store: enemy/structure ART
    is remote and content-hashed, so every build needs its own push (`tools\r2-ship.ps1`, never raw
    `adb install`). → CLAUDE.md §8 · §16 (full rule)**
79. **The MARKER is the evidence, not the exit code.** A batchmode run can exit 0 on a refusal or a FAIL —
    verify the marker, the log's freshness, and its size. → CLAUDE.md §8 · docs/HANDOVER.md ·
    memory `gates-report-success-without-proving-it`
80. **The three entry points emit DISTINCT markers** — `DataRegression.RunAll` → `REGRESSION_OK`,
    `RegressionSuite.RunAll` → `CHECKIN_SUITE_OK`, `SessionRegression.RunAll` → `SESSION_GUARDS_OK`.
    Do not read one as another. → CLAUDE.md §8
81. **NEVER restate a suite/test count from a doc — read it off the marker file, and check its date.**
    → CLAUDE.md §8 · docs/HANDOVER.md
82. **Never claim fixed on faith. Prove it with captured data, a headless run, or a regression.**
    → `PREFLIGHT_GATE.md` C13 · `docs/TICKET_PIPELINE.md` §5
83. **Headless markers cannot see geometry, orientation, or feel — that class of defect needs eyes**
    (UI capture, device screencap, or the owner). → docs/HANDOVER.md 2026-08-09 ·
    memory `headless-screenshot-verify-ui-before-build`

# I. Roles

84. **UI NEVER writes or edits `.cs` — no exceptions.** It does RCA, specs/work orders, narrative,
    mockups, board grooming. Code it wants goes to CLI as a spec. → CLAUDE.md §2
85. **CLI writes and build-verifies ALL code, owns batchmode, and is the sole git committer.**
    → CLAUDE.md §2 · `docs/TICKET_PIPELINE.md` §3
86. **The owner is the PO: she routes, felt-verifies after deploy, and CLOSES. CLI does not close.**
    → `docs/TICKET_PIPELINE.md` §1, §6
87. **QA triage is READ-ONLY: never edits, never gates, never commits.** → `docs/TICKET_PIPELINE.md` §2
88. **QA classifies NEW FEATURE vs EXISTING before any fix.** A not-yet-built function goes back to PO as
    a spec — never RCA-"fixed". → `docs/TICKET_PIPELINE.md` §2, §3
89. **Role separation is non-negotiable: QA doesn't write, CLI doesn't classify-triage, PO closes.**
    → CLAUDE.md §13 · `docs/TICKET_PIPELINE.md` §1
90. **Log every hand-off** (who → who, why) on the ticket. → `docs/TICKET_PIPELINE.md` §4
91. **The lead session is the ORCHESTRATOR: triage flow-first, fan out focused single-task agents in
    parallel, batch-gate once, commit by lane.** → CLAUDE.md §11
92. **Propagate this methodology to every agent you spawn, every session.** → CLAUDE.md §12
93. **Give the real architectural read — the why, the tradeoff, the failure mode — and let the owner
    decide. Never quietly pick easy and present it as the answer.**
    → `docs/ARCHITECTURE_PRINCIPLES.md` §0

# J. Canon maintenance

94. **Any change to architecture/state/canon updates the load-bearing doc in the SAME commit — or gets a
    one-line dated `STALE:` flag naming what is now wrong.** → CLAUDE.md §15 ★
95. **Keep exactly ONE current `CANON_GROUND_TRUTH_<date>.md`; supersede the old one by date.**
    → CLAUDE.md §15
96. **Keep the load-bearing set green:** `SESSION_CANON_LOADER.md`, `docs/HANDOVER.md`,
    `PIPELINE_STATE.md`, `docs/MASTER_CATALOG.md`, `PROJECT_INDEX.md`, the relevant `docs/*ARCHITECTURE*`,
    `CLAUDE.md`. → CLAUDE.md §15
97. **Dated point-in-time ledgers are FROZEN: banner them `⚠ SUPERSEDED <date>`, never rewrite the body.**
    → CLAUDE.md §15
98. **An undated WO asserting current state is STALE — date it or banner it.** → CLAUDE.md §15
99. **Weekly 5-minute audit: skim the load-bearing set against the anchor; fix or flag.**
    → CLAUDE.md §15 · `SUNDAY_HOUSEKEEPING.md`
100. **Never guess in a doc.** Canon updates are sourced from HEAD / the working tree / verified captures —
     §12 discipline applies to documentation too. → CLAUDE.md §15
101. **Keep the README index system and `docs/MASTER_CATALOG.md` current when you add, move, or change
     systems.** → CLAUDE.md Navigation, mandatory-first-step
102. **Never say "I'll mark it" — write the memory AND the doc in the moment.**
     → SESSION_CANON_LOADER.md Day-1 boot

---

## ⚠ CONFLICTS BETWEEN SOURCES — open, needing an owner ruling

Found while indexing. **No winner is picked here.** Each is a place where two binding docs say different
things, so a seat following one is provably breaking the other.

**C-1 — WO numbering: which block belongs to the UI seat.**
`CLAUDE.md` §2 states two blocks: "main line → CLI" and "**860–899** reserved → the UI seat."
`CLI_LANES_WO_NUMBERS.md` records an **owner ruling dated 2026-08-07** that **860–899 is CLOSED (full at
899)** and the UI seat moved to **1000–1099** (banner table: UI seat next free 1012). Both docs are
binding; they name different ranges.
*Note:* CLAUDE.md itself names the banner the SOLE authority, which arguably self-resolves the operative
number — but §2's text is stale and is exactly the kind of copied number that caused the 08-02 collisions.
**Owner call: correct CLAUDE.md §2 to point at the banner without restating a range.**

**C-2 — Which board is "the shared board".** Three answers live in binding docs:
`CLAUDE.md` §2 (Completing work orders) says "**UI marks the matching Linear issue as Done**";
`CLAUDE.md` §2 (board paragraph) + `docs/BOARD.md` say the board is **`BOARD.html`, derived from
`WorkOrders/*.md`**, with Notion retired; `CLAUDE.md` §13 + `docs/TICKET_PIPELINE.md` say the shared board
is **the Task list**. Linear predates both retirements (history: Linear → Notion → derived board), so the
Linear line reads as dead text — but it is still in a binding section. Whether the *ticket* board (Task
list) and the *work-order* board (BOARD.html) are one board or two is never stated.
**Owner call: delete the Linear line, and state whether tickets and WOs share one board.**

**C-3 — The project's Windows path.** `CLAUDE.md` §0 says the project's home is **`C:\EoA\`**;
`PREFLIGHT_GATE.md` B11 says to write escalation logs to **`C:\eoa\logs\debug\`**. On this machine
`C:\EoA` **does not exist** — the repo is **`D:\eoa`** (and `D:\eoa\logs\debug` exists). Both doc paths are
stale, which makes §0's mount-vs-Windows rule read against a path that isn't there.
**Owner call: repoint both to `D:\eoa`.**

**C-4 — CLAUDE.md §3 names a scene CLAUDE.md §7 says is deleted.** §3: "NEVER hand-edit `Village.unity` —
always rebuild via `Defenders > Week 3 > Build Village Scene` / `VillageSceneBuilder.BuildVillage`." §7:
"`Village.unity` + `OuterWorld.unity` are **DELETED** from the tree." The general rule (never hand-edit a
`.unity`; rebuild through its builder) is unaffected, but the named scene and menu path are stale and there
is no stated builder for the scenes that replaced it.
**Owner call: restate §3 generically and name the current builders.**

**C-5 — What to strip when a system stabilises.** `CLAUDE.md` §12 closes with "Set
`FlowTrace.Enabled=false` (or strip calls) once a system is proven stable."
`docs/INSTRUMENTATION_STANDARD.md` §1.4 is narrower: on graduation, mute/strip the **`Step` breadcrumbs
only** and **KEEP every `Warn`/`Fail` and every `Guard`** — those are the permanent no-silent-failure net,
not scaffolding. A seat reading §12 alone can legitimately strip the net.
**Owner call: point §12's closing line at §1.4 instead of restating it.**

**C-6 — CLAUDE.md §5's assembly table does not match the `.asmdef` files on disk.** §5 lists six
assemblies and states the cross-assembly rule as "**Village → Core only. HUD → Core only.**" On disk there
are **19 `.asmdef` under `Assets/_Modules/`** plus `Assets/Data/DeNelle.Data.asmdef`, and
`Assets/_Modules/Village/DeNelle.Village.asmdef` references **`DeNelle.BattleATB`, `DeNelle.AI`,
`DeNelle.Cosmetics`, `DeNelle.Data`, `DeNelle.Pets`, `DeNelle.Wallet`, `DeNelle.Audio`** in addition to
`DeNelle.Core` — so "Village → Core only" is not what the project builds. The invariant that IS true and
IS load-bearing is **HUD ⇸ Village** (`DeNelle.HUD.asmdef` → `DeNelle.Core` + `DeNelle.Data` only; see
QR-1.2). A seat reading §5 literally would reject legitimate existing references as violations.
**✅ RULED 2026-08-09 (owner agreed): APPLIED to CLAUDE.md §5.** The false "Village → Core only" line
is retired; §5 now states the ONE enforced invariant (HUD ⇸ Village, with `AdminOverlay.cs`'s reflection
cited as evidence OF the rule) and points at the `.asmdef` files as the authority. The six-row table is
kept but explicitly labelled a convenience SUBSET, with a standing instruction not to restore a
hand-maintained dependency table.

**C-7 — Is the `site/` (marketing/legal) Vercel project deployed?** `site/README.md` says in bold
"⛔ **NOT DEPLOYED.** Do not deploy until this checklist is clear", with a pre-publication banner and
`noindex`. `KEY_FACTS.md` and `docs/HANDOVER.md` state the Terms page is live at
`echoes-of-elarion.vercel.app/terms` and "verified HTTP 200". Both are binding; the tree cannot settle
which is current, and this is a **legal-surface** page. See QR-4.8.
**✅ RULED 2026-08-09 (owner): the site IS DEPLOYED.** Corroborated in-tree — `site/.vercel/project.json`
links to Vercel project `echoes-of-elarion` (that file is written by `vercel link`/`deploy`, not by
hand), `site/terms.html` is present, and commit `c8320434` "host a Terms of Use" shipped it.
`KEY_FACTS.md` + `docs/HANDOVER.md` were correct; `site/README.md`'s "NOT DEPLOYED" banner was the
stale loser and has been corrected, with its checklist re-framed as POST-publication maintenance —
on a live legal surface an unticked item is a defect, not a pending task.

---

*Maintained per CLAUDE.md §15: if you change a rule at its source, fix the one line here in the same
commit. If you find yourself pasting explanation into this file, stop — that content belongs in the
source doc, and the copy is the drift.*
