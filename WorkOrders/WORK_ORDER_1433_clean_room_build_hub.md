# WORK ORDER 1433 — Clean-Room PROD BUILD HUB

**Status:** DRAFT — SPEC ONLY, NOT READY TO IMPLEMENT (four owner questions open, §11; Phase 0 gates everything)
**Revised:** 2026-09-06 — owner re-ruled hub contents to **MINIMUM THAT BUILDS**, superseding "full copy" (§0)
**Minted:** 2026-09-06 (CLI main line; banner `CLI_LANES_WO_NUMBERS.md:190` said next free = 1433)
**Lane:** Build/infrastructure. Disjoint from every gameplay lane (§9). Touches no `Assets/` code.
**Silo:** Ship chain / CI
**Author:** spec-writing lane. Design only — nothing in this document was built.

> ⚠ **NUMBER COLLISION AVOIDED.** This work order was commissioned as **WO-1432**. That number was
> already on disk as `WorkOrders/WORK_ORDER_1432_honest_review_thank_you_grant.md`, minted by the CLI
> seat on 2026-09-06 (`CLI_LANES_WO_NUMBERS.md:191`). Per §2 (`first-on-disk-and-referenced-wins`) the
> existing 1432 keeps the number and this ticket took **1433** off the banner. Anything that already
> refers to "WO-1432 clean-room build hub" means **this file**.

---

## 0. THE OWNER'S REQUEST, verbatim

> *"is it possible to move the build to a specific structure in GitHub where EVERYTHING needed lives
> as a copy, that I can tell another machine to download and build APK, AAB or web UI so we don't eat
> the time and we have a share to that device so you can access?"*

> *"full PROD HUB — replace everything with the new data, wipe every time and pull down then build."*

### ⛔ THE CONTENTS RULING — **MINIMUM THAT BUILDS** (owner, 2026-09-06, BINDING)

> **The hub carries only what a build provably loads.** Where a pack is an *upgrade over a tracked
> fallback* (`tools/art/REQUIRED_PACKS.md` §1), **the hub takes the tracked fallback and does NOT carry
> the pack.**

⚠ **THIS SUPERSEDES THE EARLIER "FULL COPY" RULING** — *"the PROD HUB repo must be a FULL COPY —
everything needed to build lives in it, wiped and replaced on every push, so a clean machine pulls it
and builds with no second source."* That ruling was made on the premise that the gitignored set was
**two packs**. The measurements in §1 showed it is **17.71 GiB across 124,465 files**, and the owner
**re-ruled on the real numbers**. Do not restore the full-copy reading from any earlier draft, summary
or conversation: it was retired by the person who made it, on evidence, the same day.

**What this changes, and what it does NOT change.** It changes the **decision rule applied to the
dependency walk's output** — not whether the walk happens. **P0-2 is now MORE necessary, not less:
"minimum" is undefined until the walk produces the set.** An implementation lane that reads
"minimum that builds" as licence to skip the measurement and hand-pick some packs has inverted the
ruling.

**And it buys a named risk the owner explicitly accepted — see R-11 and §7.4.** Hub builds may look
different from this machine's builds wherever a tracked fallback substitutes for an absent pack. She
was told that, and told it might not surface until she sees it on the phone. §7.4 specifies the cheap
detector so that it surfaces in a **diff**, not in her eyes.

**The motivation, stated plainly:** a build occupies the owner's only Unity machine for ~10 minutes
and holds the project lock, which blocks every other gate. `run-unity-method.ps1:130-133` refuses to
launch while a `Unity` process exists, and `morning-ship-chain.ps1:74-76` refuses the whole chain for
the same reason. The hub's value is **giving that machine back**, not making a build finish faster.

---

## 1. ⛔ READ THIS BEFORE ANYTHING ELSE — four measured facts that change the brief

Every number below was measured on `D:\eoa` on **2026-09-06**. Nothing here is recalled or inferred.

### 1.1 `Assets/Quaternius` DOES NOT EXIST ON THE WORKING MACHINE — and the code that names it is EDITOR-ONLY

```
$ ls -d Assets/Quaternius
ls: cannot access 'Assets/Quaternius': No such file or directory
$ ls Assets/Quaternius.meta
ls: cannot access 'Assets/Quaternius.meta': No such file or directory
```

**The pack is absent, and so is its folder `.meta`** — so there is no dangling folder GUID either.
`.gitignore:508-509` ignores it. **The hub cannot be seeded with Quaternius from `D:\eoa`.**

**Under a minimum-that-builds rule this is load-bearing, so it was investigated rather than noted.
Determination: every `Assets/Quaternius/` PATH STRING in the codebase lives in the editor-only
assembly, and no runtime asset references the pack.** Evidence, all read at source this session:

**(a) Every path-string reference is under `Assets/Editor/`** — `DeNelle.Editor.asmdef` (`:26`
`includePlatforms`), which is editor-only and ships in no player:

| File:line | What it does |
|---|---|
| `Assets/Editor/AssetImportPostprocessor.cs:95` | `"Assets/Quaternius/"` in a path list |
| `Assets/Editor/CastleHubBuilder.cs:136` | prefab root const; **`:969` `Debug.LogWarning("Missing Quaternius prefab: …")`** — the §4 warn-not-error pattern |
| `Assets/Editor/EnemyProvingHarness.cs:92` | path list |
| `Assets/Editor/MeshCompressionPass.cs:74` | path list, and **`:65` says in-code "polyperfect / Quaternius are gitignored and absent on some clones"** |
| `Assets/Editor/TextureBatchOptimizer.cs:78` | path list; comment: **"missing root is skipped with a warning"** |
| `Assets/Editor/Village2Build.cs:30,36-38` | harvest source paths for a tool its own header calls **"EXPERIMENT / build-tooling only. Editor-only."** |
| `Assets/Editor/Regression/TerrainLayerRegression.cs:162` | ⚠ **`string[] banned = { "Assets/Blink/", "Assets/polyperfect/", "Assets/Quaternius/" }`** |
| `Assets/Editor/Regression/VfxResourceSelfContainmentRegression.cs:90` | same — an exclusion list |
| `Assets/Editor/Regression/TowerEmpowermentReachabilityRegression.cs:456` | name filter excluding `/Quaternius/` |

The last three are the strongest evidence available: **two regressions hold the pack in a `banned` /
self-containment exclusion list, i.e. the suites actively assert that runtime content does NOT
reference it.** Two more tools state in their own code that the pack may be absent and handle it.

**(b) Every non-Editor mention is a COMMENT, with exactly one apparent exception, and that exception
is a stub.** `ClaimableCamp.cs:130` really does call `SpawnQuaterniusEnemyCamp()` at runtime — but the
method (`:135-153`) spawns **`GameObject.CreatePrimitive(PrimitiveType.Cube)`** three times and names
them `"QuaterniusEnemyProp"`. Its own comment at `:138` reads *"Demo spawn; real load Quaternius
enemy/medieval props."* **It loads nothing from the pack; the name is an artifact of an unbuilt
intention.** The remaining hits (`MagentaGuard.cs`, `RotationCorrectionRegistry.cs`,
`HeroBodySwapper.cs`, `HubStructureVisualInjector.cs`, `VFXManager.cs`, `Village2Generator.cs`) are
comments.

**(c) No data asset references it.** `grep -rl "Quaternius"` across `*.json`, `*.prefab`, `*.unity`,
`*.asset` under `Assets/` returns exactly one file — `Assets/README.md` — and **nothing** under
`Assets/AddressableAssetsData/`.

**So: not cruft, but not a build dependency either.** The references are editor scene-building tooling
and deliberate exclusion lists. Under minimum-that-builds, **the hub does not carry Quaternius**, and
that is a conclusion from evidence rather than from its absence being convenient.

⚠ **THE ONE THING STATIC READING CANNOT SETTLE, stated plainly rather than inferred.**
`HubStructureVisualInjector.cs:7` records that *"MainCastle_Hall bakes its 8 structures from
polyperfect/Quaternius prefabs via CastleHubBuilder."* **A committed scene that was baked while the
pack was present could still hold mesh/material references to Quaternius GUIDs that now resolve to
nothing.** Grep cannot see that — a GUID reference in a `.unity` file is an opaque hash, not the string
"Quaternius". Only Unity resolving the scene can answer it. **I have NOT proven either way, and I am
not inferring from the fact that the machine appears to build.** **P0-1 settles it** by opening the hub
scene in the editor and reporting unresolved references — and the §7.4 capture diff would catch the
visible consequence regardless.

### 1.2 THE GITIGNORED SET IS 17.71 GiB / 124,465 FILES — NOT "TWO PACKS"

Measured with `git ls-files --others --ignored --exclude-standard -- Assets/`:

| | Files | Size |
|---|---:|---:|
| **Gitignored files under `Assets/`** | **124,465** | **17.71 GiB** |
| Whole `Assets/` tree on disk | 138,854 | 19.97 GiB |
| Tracked git pack (`git count-objects -vH`) | — | 343.58 MiB |
| Git LFS objects already tracked (`.git/lfs`) | 3,538 | 3.6 GB |

Where the ignored bytes are (top of the list, by second/third path segment):

| Path | Files | MiB |
|---|---:|---:|
| `Assets/Blink/Art` | 9,092 | **12,865.4** |
| `Assets/Models/KayKit` | 61,318 | 1,464.6 |
| `Assets/Mirza Beig/Particle Systems` | 3,248 | 1,147.1 |
| `Assets/polyperfect/Low Poly Ultimate Pack` | 25,661 | **438.4** |
| `Assets/Synty/PolygonFantasyKingdom` | 13,343 | 285.6 |
| `Assets/Spells Pack/{Demo,Particles}` | 2,029 | 531.4 |
| `Assets/Supercyan/{Textures,Animations}` | 834 | 312.5 |
| `Assets/UnityTechnologies/ParticlePack` | 885 | 189.7 |
| `Assets/Synty/PolygonGeneric` | 2,567 | 151.4 |
| `Assets/Tech hud elements/*` | 717 | 165.5 |
| `Assets/Models/{KayKit Adventurers 2.0, Cathedral, People}` | 782 | 139.8 |

**Exact figure for the pack the ruling names:** `Assets/polyperfect` = **459,660,257 bytes**
(438.4 MiB apparent, 476 MB allocated on disk) across **25,671 files**.
`Assets/Quaternius` = **0 bytes, absent** (§1.1).

**"Everything NEEDED to build" is not the same set as "everything gitignored on disk," and the
difference is roughly 17 GiB.** `tools/art/REQUIRED_PACKS.md` §1 is explicit that the big packs are
*upgrades over tracked fallbacks*, not boot requirements — "a bare clone with no extra packs copied in
should still boot and show distinct enemies/NPCs via the tracked `Resources/` fallback." `.gitignore`
lines 713-723 say the same for Synty: "Only the specific prefabs/meshes the game references are copied
into tracked `Resources/` + Addressable paths." **`Assets/Blink/Art` alone is 12.8 GiB and the
`.gitignore` negations at lines 529-539 track only `MegaWeaponPack1/Meshes_MWP1/*.fbx.meta` out of
it.** Committing that whole tree would be tracking 12.8 GiB to obtain a handful of weapon meshes —
which is the measurement that retired the full-copy ruling (§0).

The honest bound for an implementer: **floor** = what is tracked today (343.58 MiB pack + 3.6 GB LFS);
**ceiling** = floor + 17.71 GiB. The real number is between them and **P0-2 measures it** rather than
guessing.

`tools/art/REQUIRED_PACKS.md` names exactly one pack with **no runtime fallback**:
`Assets/UnityTechnologies/ParticlePack` (189.7 MiB) — 54 owner-tagged VFX keys in
`Assets/Editor/VfxManualPicks.json` point into it, and `Enemy.cs` / `PoiCalloutSystem.cs` consume it.
That one is a hard hub requirement regardless of what P0-2 finds.

### 1.3 "CAN A NORMAL GIT REMOTE HOLD IT?" — LFS ALREADY DECIDES THIS, AND IT IS METERED

`.gitattributes:1-10` already routes `*.mp3 *.wav *.fbx *.png *.jpg *.jpeg *.JPEG *.tga *.mp4 *.psd`
through LFS. Splitting the ignored set by that rule:

| Route | Files | Size |
|---|---:|---:|
| **Would land in LFS** (matches `.gitattributes`) | 24,584 | **15.20 GiB** |
| **Would land as plain git objects** | 99,881 | **2.51 GiB** |
| Individual files over 100 MiB | **0** | — |

So the question is not "can a git remote hold it" — `git add` of the packs sends 15.20 GiB to **LFS
storage**, which is billed by quota, and **every fresh clone re-downloads it**, which is billed by
bandwidth. Since the hub is specified to **wipe and re-pull every time**, the hub pays that bandwidth
*on every single build*.

⚠ **NOT VERIFIED (do not let an implementer assume these):** GitHub's current per-file size limit, LFS
per-file limit, LFS storage and bandwidth quota per plan, and hosted-runner disk size. This document
deliberately does not state them from memory. **P0-3 requires reading them off GitHub's own docs and
recording the figures with the date read.** The design decision in §4 hinges on those numbers.

### 1.4 THE ~10 MINUTES SHE WANTS BACK AND "WIPE EVERY TIME" ARE IN TENSION

A wiped tree means a wiped `Library/`, and Unity must reimport ~20 GiB of assets before it compiles a
line. The ~10-minute figure is a **warm-`Library`** build on `D:\eoa`. A genuine clean-room first
build is materially longer — **NOT VERIFIED how much longer; P0-4 measures it once.**

This does not sink the design: the win the owner actually asked for is *"we don't eat the time"* on
**her** machine, and the hub delivers that whether the hub itself takes 10 minutes or 50. But an
implementer must not promise a faster build. **Owner Question OQ-1 (§11) puts the `Library/` cache
question to her rather than deviating silently (§11B.B).**

---

## 2. THE EXISTING SHIP CHAIN, AS IT ACTUALLY IS

Everything in this section was read at source this session. Scripts live at the **repo root**, not
under `tools/`, except the four noted.

### 2.1 Script-by-script

| Script | What it does | Marker(s) it judges | Blocks? |
|---|---|---|---|
| **`run-unity-method.ps1`** (root, 249 ln) | The one wrapper every batchmode gate goes through. Locates the editor under `C:\Program Files\Unity\Hub\Editor` (`:97`), pins `6000.4.8f1` (`:98`) via `tools/assert-unity-editor-pin.ps1` (`:125`), **refuses if a Unity process is running** (`:130-133`), launches `-batchmode -quit -executeMethod`, polls until no `Unity` process remains (`:156-162`) because Unity forks on launch, then judges the log. | `-ExpectMarker` (caller declares it). Fails closed on `LOG_MISSING` / `LOG_STALE_FROM_EARLIER_RUN` / `LOG_TRUNCATED` / `MARKER_ABSENT` (`:205-213`) → **exit 8**; licence error → **exit 7** (`:222`). Emits `VERDICT=PASS` / `PASS-UNASSERTED` / `FAIL` through `Write-Verdict` (`:77-92`), which also posts to Discord via `tools/status-post.mjs` when a webhook exists. | yes |
| **`overnight-apk-build.ps1`** (root, 132 ln) | Detached Seeker APK. (1) `node tools/schema-parity.mjs` and **refuses the build** without a fresh `SCHEMA_PARITY_OK` (`:62-68`, exit 4). (2) `run-unity-method -Method DeNelle.Editor.AndroidBuild.BuildSeekerApk -BuildTarget Android -ExpectMarker '[AndroidBuild] SUCCEEDED'` (`:72`). (3) **Freshness check** — an APK older than `$startedAt` is `APK_STALE`, exit 1 (`:86-91`). (4) `tools/r2-ship.ps1` (`:106`). (5) judges `R2_PARITY_OK` **on the log, not the exit code** (`:113`) and **exits 3** if absent (`:129`). | `SCHEMA_PARITY_OK`, `[AndroidBuild] SUCCEEDED`, `APK_OK`/`APK_STALE`, `R2_PARITY_OK`, `APK_DONE` → `Builds\overnight-apk-status.txt` | yes |
| **`google-play-aab-build.ps1`** (root, 350 ln) | The one sanctioned AAB path (WO-1365). **Signing preflight before the build** (`:103-132`): reads `keystore.properties`, requires `keystore.path/alias/storepass/keypass` all present and the keystore file to exist, else `AAB_SIGNING_FAIL` (exit 5). Then build → R2 ship (blocks) → bundletool size guard. | `AAB_SIGNING_PREFLIGHT_OK`, `AAB_SIGNING_OK`, `AAB_STALE`, `AAB_SIZE_OK/FAIL/UNMEASURED`, `AAB_DONE`. Exits: 1 no fresh AAB, 3 parity, 5 signing, 6 size. **`AAB_SIZE_UNMEASURED` fails closed.** | yes |
| **`distribute-android.ps1`** (root, 73 ln) | Firebase App Distribution. Runs schema parity **even with no `-Build`** (`:29-34`, exit 4) because "a green build from yesterday cannot prove today's DB". Resolves App ID from `-AppId` → `$env:FIREBASE_APP_ID` → gitignored `firebase-appid.txt` (`:38`). Requires the `firebase` CLI on PATH (`:44`). | `SCHEMA_PARITY_OK`; then plain `firebase appdistribution:distribute` exit code (`:72`) | yes |
| **`install-apk-to-seeker.ps1`** (root, 148 ln) | Build + **sideload over USB**. `#requires -Version 5.1`. Hardcodes `C:\Program Files\Unity\Hub\Editor\6000.4.8f1` (`:26`) and gets `adb.exe` from the Unity Android module (`:30`). **Requires a device in `device` state** (`:112-121`). Calls `tools/r2-ship.ps1 **-WarnOnly**` (`:136`) — the one legitimate non-blocking call site. | `[AndroidBuild] SUCCEEDED`; R2 parity **warn-only** | no (by design) |
| **`morning-ship-chain.ps1`** (root, 192 ln) | EXE → APK → Firebase → WebGL. Schema gate first (`:42-46`). **Commit-charge memory gate** (`:54-71`, `Get-Counter`, default 60 GB headroom, exit 90) for the ~88 GB kernel leak. Refuses if Unity is open (`:74-76`). Compares artifact mtimes against pre-run stamps — *"Existence proves nothing. Freshness does."* (`:82-90`). Asserts `RELEASE signing` and rejects `DEBUG signing` in `apk-build.log` (`:121-129`, exit 13). Calls `tools/r2-ship.ps1` and dies on non-zero (`:158-161`). | `SCHEMA_PARITY_OK`, `[AndroidBuild] SUCCEEDED`, `R2_PARITY_OK`; freshness by mtime | yes |
| **`build-webgl.ps1`** (root, 165 ln) | WebGL player → `Builds/WebGL/index.html`, log `Builds/webgl-build.log`. Same editor pin (`:36-63`). Wipes `Builds\WebGL` first (`:74-78`). `-NoBrotli`, `-DevBuild`. | ⚠ **NONE from Unity.** `:6` — *"Success = index.html."* `WEBGL_BUILD_OK` at `tools/command-centre.ps1:366` is **synthesised** by the caller from index.html existence + log freshness. | n/a |
| **`tools/r2-ship.ps1`** (240 ln) | **The one file that ships content.** `python tools/r2_sync.py --ensure-cors`, then `--push ServerData` (**the PARENT** — `:116-118`), then **enumerates the actual subdirectories of `ServerData/`** that hold `catalog_*` files (`:157-159`) and runs `--verify-catalog ServerData/<name>` per target (`:182`). Rewrites each per-target `R2_PARITY_OK` to `R2_PARITY_TARGET_OK` (`:185`) so the aggregate marker can only be written when **every** target passes (`:212-219`). Log is **UTF-16LE** (`:217`). | `R2_PUSH_OK` → `Builds/r2-push.log`; **`R2_PARITY_OK targets=… objects=…`** → `Builds/r2-parity.log`. **Exit 16** on failure; `-WarnOnly` exits 0 anyway; `-VerifyOnly` uploads nothing. | yes (except `-WarnOnly`) |
| **`tools/web-ship.ps1`** (522 ln) | Knows all **four** Vercel surfaces and proves the public production domains serve identical bytes. Registry hardcoded once (`:133-161`). **No `-Force`, no `-WarnOnly`, deliberately** (`:59-62`). | `WEB_SURFACES_OK`, `WEB_STAGE_OK`, `WEB_DEPLOY_OK`, `WEB_SHIP_PUSH_OK`, `WEB_LEGAL_OK`, `WEB_PARITY_OK` → `Builds/web-parity.log`. Exit 16 fail, 20 refused. | yes |
| **`tools/command-centre.ps1`** | Gate → preview → promote → prove → roll back production. Its `Assert-FreshMarker` (`:121-146`) is the reference implementation of the repo's evidence rule: `LOG_MISSING` / `LOG_STALE_FROM_EARLIER_RUN` / `MARKER_ABSENT` → `COMMAND_CENTRE_REFUSED`. **Reads the R2 log with `-Utf16`** (`:305`). | Step 1 `COMPILE_GATE_OK` (`:287`) then **`REGRESSION_OK \d+/\d+ suites`** — the *shaped* form (`:297`); step 2 `R2_PARITY_OK`; step 3 `SCHEMA_PARITY_OK` + `TREASURY_VERIFY_OK`; step 4 `ROLLBACK_ID_CAPTURED`; step 5 `WEBGL_BUILD_OK` + `CANDIDATE_CONTENT_MATCH`. Requires `VERCEL_TOKEN` (`:266`) and `DATABASE_URL` (`:270`). | yes |

### 2.2 The gates and their markers (CLAUDE.md §8, binding)

| Gate | Entry point | Marker | Read at source |
|---|---|---|---|
| Compile | `DeNelle.Editor.CompileGate.Run` | `COMPILE_GATE_OK :: scripts compiled clean` | `Assets/Editor/CompileGate.cs:57` |
| Data regression | `DeNelle.Editor.Regression.DataRegression.RunAll` | **`REGRESSION_OK <n>/<n> suites`** | `Assets/Editor/Regression/DataRegression.cs:1674` |
| Legacy smoke | `RegressionSuite.RunAll` | `CHECKIN_SUITE_OK <p>/<n> cases` | `DataRegression.cs:23` |
| Session guards | `SessionRegression.RunAll` | `SESSION_GUARDS_OK` | CLAUDE.md §8 |
| UI capture | `UICaptureLaunch.RunCaptureHeadless` | `UI_CAPTURE_OK <n>` | referenced `CaptureProvenanceRegression.cs:38,526` |
| R2 content | `tools/r2-ship.ps1` | `R2_PARITY_OK` | `tools/r2-ship.ps1:215` |
| DB schema | `node tools/schema-parity.mjs` | `SCHEMA_PARITY_OK <n> table(s) verified` | `tools/schema-parity.mjs:243` |

⛔ **JUDGE BY THE MARKER ON A FRESH LOG, NEVER THE EXIT CODE.** This is not style advice. The runners
in this repo exit 0 on refusals and FAILs — stated at `r2-ship.ps1:76-78`, `web-ship.ps1:47-56`,
`overnight-apk-build.ps1:111-112`, and CLAUDE.md §8. **Marker absence on a fresh log is a FAILURE, not
an unknown.** The hub's job runner must implement the same three-part freshness check
`command-centre.ps1:121-146` already implements: log exists, log mtime postdates the step start, marker
present — and for the regression the **shaped** `REGRESSION_OK \d+/\d+ suites`, because the three entry
points used to emit identical bare `REGRESSION_OK` markers and a 22-case suite's pass read as the full
suite's pass (CLAUDE.md §8).

### 2.3 Signing, verified at source (not from a script comment)

`Assets/Editor/AndroidBuild.cs:367-403` — `ApplyReleaseSigning()`:

- `:369` reads `keystore.properties` from `Directory.GetCurrentDirectory()`.
- `:372-373` **file absent → `useCustomKeystore = false` + `Debug.LogWarning` — the build CONTINUES and produces a DEBUG-SIGNED artifact.**
- `:393-394` incomplete or keystore file missing → same silent DEBUG fallback.
- `:398,403` success → `useCustomKeystore = true` and logs `[AndroidBuild] RELEASE signing: keystore='…' alias='…'`.

`AndroidBuild.cs:176` — `BuildSeekerApk` calls `AddressablesContentBuild.EnsureBuilt("AndroidBuild", BuildTarget.Android)` **before** `BuildPipeline.BuildPlayer`; `:201` logs `[AndroidBuild] SUCCEEDED`. This is why `overnight-apk-build.ps1:72` passes `-BuildTarget Android` explicitly (Addressables builds for the **active** target, and on 2026-08-19 that was `StandaloneWindows64`).

### 2.4 `r2_sync.py` push semantics

`tools/r2_sync.py:184-230` — `--push` **uploads and skips objects whose content already matches**, comparing **md5 against the object ETag**, not size (`:190-196`: a size-equal skip once left an OLD catalog hosted). It prints `R2_PUSH_OK <sent> uploaded (<MB>), <skipped> unchanged`. **It is additive — no delete, no mirror** (the only `delete_object` is the healthcheck probe cleanup at `:161`). So two builders sharing the bucket cannot clobber each other's bundles; they accumulate. That is good for the hub, and it means **bucket growth is an unbounded cost nobody is currently watching** — noted as risk R-7.

Credentials come from **`.env.r2` at the repo root** (`:50`), gitignored via `.gitignore:585` (`.env*`), requiring `R2_S3_ENDPOINT`, `R2_BUCKET`, `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY`, `R2_PUBLIC_URL` (`:66-67`).

---

## 3. ⛔ CLAUDE.md §16 AND HOW A RUNNER SATISFIES IT

**The constraint.** Enemy and structure art is served from
`https://pub-ab6dfaf1b3d74ca78891876611ccb832.r2.dev/[BuildTarget]`
(`Assets/AddressableAssetsData/AddressableAssetSettings.asset`). There is **no local fallback** —
`Assets/Resources/Enemies` and `Assets/Resources/Structures` no longer exist as the art source.
**Bundle names are content-hashed, so every content build needs its own push, and a missing push fails
SILENTLY**: the game installs, launches, plays, and shows tinted capsules with no error on screen. This
has happened **four** times (`tools/r2-ship.ps1:30-53`).

**The hook does not travel.** `.githooks/pre-push` is tracked, but it only runs when a clone has run
`git config core.hooksPath .githooks` — **local config, per clone**. Its invariant
(`.githooks/pre-push:15-18,107-125`) is *the parity proof must be newer than the newest file under
`ServerData/`*, and it also checks the Unity editor pin (`:56-69`) and `api/schema.sql` changes
(`:36-51`). **It has deliberately no override flag** (`:20-25`).

**How the hub satisfies §16 — and it is not by installing the hook.**

The hook guards `git push`. **The hub pushes nothing to git.** Installing the hook on the hub would
guard an operation the hub never performs. What the hub must do instead:

1. **The hub runs `tools/r2-ship.ps1` as a mandatory, blocking job step** — never `-WarnOnly`, never
   `-VerifyOnly` on a build that produced content, never a re-inlined copy of push+verify (§16
   forbids re-inlining; `r2-ship.ps1:55-61` records why).
2. **The step is judged by `R2_PARITY_OK` on a fresh `Builds/r2-parity.log`, decoded as UTF-16LE**
   (`r2-ship.ps1:217`; `command-centre.ps1:305` uses `-Utf16`; `.githooks/pre-push:109-110` strips NULs
   with `tr -d '\000'`). A job step that greps a UTF-16 file as UTF-8 will silently never match —
   **that is a marker-absent FAILURE and must fail the job**, per §2.2.
3. **The job fails, and publishes no artifact, when that marker is absent.** Freshness is checked the
   same three ways `command-centre.ps1:121-146` checks it. This reproduces the hook's invariant inside
   the job, where it belongs, because the hub *is* the builder.
4. ⚠ **THE INVERSION THIS CREATES, AND IT IS THE MOST DANGEROUS THING IN THIS DOCUMENT.** Once the hub
   builds content, `D:\eoa`'s `ServerData/` is no longer the bytes that shipped. The working machine's
   `pre-push` hook will still happily pass — it is proving its own stale local tree against its own
   stale local proof. **The hook stops being a ship gate the moment a second machine builds content.**
   Two mitigations, and OQ-3 asks the owner to pick:
   - **(a) The hub is the ONLY seat that builds and pushes content.** `D:\eoa` never runs a content
     build, so its `ServerData/` never moves and its hook stays honest by not being exercised.
   - **(b) `D:\eoa` pulls the hub's `ServerData/` artifact down before it pushes**, so the local proof
     describes the shipped bytes. More moving parts, more chances to skip a step — which §16 says is
     exactly how this fails.
   This spec **recommends (a)**, and recommends that the implementation lane raise a follow-up ticket
   to teach `pre-push` to recognise a hub-issued parity proof. **That follow-up is out of scope here
   and must not be improvised** — `.githooks/pre-push` has no override flag on purpose.
5. **Same failure class, already live, at `install-apk-to-seeker.ps1:136`.** It runs
   `r2-ship.ps1 -WarnOnly` against the **local** `ServerData/`. If the CLI seat downloads a hub-built
   APK and installs it with `-Build:$false`, that warn-only parity check is about **different bytes
   than the APK's catalog** — it proves nothing about the artifact being installed. **The hub must
   therefore publish `ServerData/<target>/catalog_*` alongside every artifact**, and the sideload step
   must either verify against those or be explicitly told parity was proven upstream, with the hub's
   run id recorded. Do not leave this implicit; it is precisely the 2026-08-19 shape.

**Also binding:** push the **PARENT** (`--push ServerData`), verify the **EXPLICIT** target. Both forms
are hardcoded exactly once inside `r2-ship.ps1` (`:63-74`). The hub calls the file. It never retypes
either command.

---

## 4. WHAT A FRESH CLONE IS MISSING

Beyond §1.2's 17.71 GiB of art, a clean-room checkout of this repo lacks:

**Secrets and machine-local config (all gitignored):**

| Item | Path | Ignored at | Needed by |
|---|---|---|---|
| R2 credentials | `.env.r2` | `.gitignore:585` (`.env*`) | `tools/r2_sync.py:50,66-67` |
| Android release keystore | `*.keystore` + `keystore.properties` | `.gitignore:391,393` | `AndroidBuild.cs:369`, `google-play-aab-build.ps1:118` |
| Production DB URL | `DATABASE_URL` (env or `.env.local`) | `.gitignore:388` | `tools/schema-parity.mjs:205-224` |
| Firebase App ID | `firebase-appid.txt` | `.gitignore:598` | `distribute-android.ps1:38` |
| Vercel token | `.vercel-token` | `.gitignore:101` | `command-centre.ps1:266`, `web-ship.ps1` |
| Google services (desktop) | `Assets/StreamingAssets/google-services-desktop.json` | `.gitignore` §check-in sweep | Firebase runtime |
| Firebase SDK trees | `Assets/Firebase/`, `Assets/ExternalDependencyManager/`, `Assets/GoogleSignIn/`, `Assets/GeneratedLocalRepo/` (22.9 MiB) | `.gitignore:608-613,620` | Firebase Auth / Analytics compile |

⚠ **`Assets/Firebase/` and `Assets/ExternalDependencyManager/` are gitignored and are Unity plugin
trees, not art.** A fresh clone almost certainly does not compile without them. **NOT VERIFIED** —
P0-2 must classify them alongside the art.

**Also absent from a fresh clone:** `Library/` (`.gitignore:4`) — the full-reimport cost of §1.4;
`Builds/` (`.gitignore:8`) — which is why evidence must be uploaded as run artifacts, §7;
`ServerData/` — Addressables remote output, explicitly ignored because "the CDN — not git — is where
the player fetches them from"; `Assets/StreamingAssets/aa/*` (`.gitignore:88-89`) — local Addressables
output; `Assets/Models/*` except `People/`, `Assets/Art/TripoStructures/`, most of
`Assets/Resources/Structures/` — the trio `export-assets.ps1` / `import-assets.ps1` exists to hand-carry
by zip.

**The re-import path canon names is not an importer.** CLAUDE.md §4 says Polyperfect is "re-imported on
fresh clone via `Defenders/Art/Fix Polyperfect URP Materials`." Read as written that is a **material
fixer** — it repairs shaders on a pack that is already on disk. It does not obtain the 438.4 MiB of
FBX/PNG. `tools/art/REQUIRED_PACKS.md` §4 step 3 is the accurate procedure: *"copy the pack in from the
owner's zip / source folder … Do not `git pull` it — it isn't in git."* **Flagging this as a canon
correction for a future §15 pass; this spec does not edit CLAUDE.md.**

**The one machine-readable check that already exists:** `tools/art/verify-runtime-art.ps1` classifies
paths as CRITICAL / COMMITTED / PACK, hard-fails on the first two, warns on PACK, and has a **`-Strict`
switch that turns pack warnings into failures — described in its own header as "strict CI mode."**
**The hub runs it with `-Strict` as a pre-build step.** It is the closest thing the repo has to a
clean-room readiness gate and it was written for exactly this.

---

## 5. UNITY VERSION AND LICENSING

**Pinned version: `6000.4.8f1` (revision `f8b72d3d7343`)** — read from
`ProjectSettings/ProjectVersion.txt`. The pin is enforced in four places: `run-unity-method.ps1:98`,
`build-webgl.ps1:37`, `install-apk-to-seeker.ps1:25`, and `.githooks/pre-push:32,60-69`
(`UNITY_EDITOR_PIN_DOWNGRADE` blocks the push). `tools/assert-unity-editor-pin.ps1` is the shared
assert.

**Modules required on the runner:** Android Build Support with OpenJDK + SDK + NDK
(`install-apk-to-seeker.ps1:37-45` aborts without it) and WebGL Build Support
(`build-webgl.ps1:68-71` warns and the build then fails).

**Licensing.** `run-unity-method.ps1:175` already detects `HandshakeResponse reported an error`,
`No valid Unity Editor license`, `ResponseCode: 505`, `Unsupported protocol version` and exits **7**
with a named remedy: *retry once (often transient); if still failing, close Unity Hub* (owner remedy
2026-08-25). Because the runner is a real Windows machine (§6), the licence is activated on it through
the Hub exactly like any other seat.

⚠ **NOT VERIFIED:** whether the owner's Unity licence tier permits unattended/build-server activation,
and whether a second concurrent activation is within seat entitlement. **This is a licence-terms
question, not a technical one, and it must be answered before a second machine is activated** — P0-5.
Do not have an implementation lane resolve it by trying it.

---

## 6. WHAT CANNOT MOVE TO CI — AND WHY THE RUNNER MUST BE SELF-HOSTED WINDOWS

### 6.1 The runner is forced

This is a **finding, not a preference**:

- `run-unity-method.ps1:97` and `build-webgl.ps1:36` hardcode `C:\Program Files\Unity\Hub\Editor`.
- `install-apk-to-seeker.ps1:1` is `#requires -Version 5.1` — **Windows** PowerShell.
- `morning-ship-chain.ps1:54` uses `Get-Counter '\Memory\Committed Bytes'` — a Windows perf counter.
- Every one of these scripts is ASCII-only *because PS 5.1 reads BOM-less files as ANSI*
  (`run-unity-method.ps1:11-12`, `morning-ship-chain.ps1:18-19`, and others).
- `Assets/` is **19.97 GiB on disk** before `Library/` exists.

The owner said *"another machine."* **That machine is the runner.** GitHub Actions is the
**orchestrator** — trigger, secrets, artifact store, log surface — and a **self-hosted Windows runner**
registered on it is the executor. Nothing in the existing chain runs on a hosted Linux runner without
rewriting all of it, and rewriting the chain is exactly what §16 forbids (one file, one seam).

⚠ **NOT VERIFIED:** hosted-runner disk capacity and whether any hosted Windows image could hold ~20 GiB
of assets plus `Library/` plus IL2CPP output. **P0-3 records the figure.** Do not assume it fits.

### 6.2 Steps that cannot move, ever

| Step | Why it is pinned to a human + hardware |
|---|---|
| `install-apk-to-seeker.ps1` | Needs a USB device in `device` state (`:112-121`) and `adb install` (`:139`). No device, no install. |
| Capturing the **live release** signing certificate | Requires reading the certificate off the copy installed on the Seeker. One-time, working-machine, device step. See §9.3. |
| **The felt-test** | CLAUDE.md §13: the PO felt-verifies and closes; headless cannot judge feel. **A green hub run is not a felt-test.** This is an acceptance criterion, §10. |
| Owner creative decisions | §2 — CLI does not classify-triage, PO closes. |

---

## 7. THE SPEC

### 7.1 Repository shape

**Two repositories, not one.**

- **`eoa` (existing)** — source of truth. Unchanged. The seats work here.
- **`eoa-prod-hub` (new, PRIVATE)** — the build repo. It carries the tracked source tree **plus only
  the required-and-untracked set P0-2 proves a build loads**, and it is **replaced wholesale on every
  publish**.

**⛔ THE CONTENTS RULE — MINIMUM THAT BUILDS (owner ruling, §0).** For each item the P0-2 walk returns:

| P0-2 says | Hub carries |
|---|---|
| Referenced by a build scene / Addressables group / `Resources/`, **and untracked** | **YES** — it is the minimum |
| A pack that is an **upgrade over a tracked fallback** (`REQUIRED_PACKS.md` §1) | **NO** — the hub takes the fallback |
| Referenced with **no fallback** (today: `Assets/UnityTechnologies/ParticlePack`, 189.7 MiB — 54 keys in `VfxManualPicks.json`, consumed by `Enemy.cs` / `PoiCalloutSystem.cs`) | **YES** — mandatory, no substitute exists |
| Editor-only tooling input (Quaternius per §1.1; `Village2Build` harvest sources) | **NO** |
| Unresolved / ambiguous | **STOP and ask.** Do not pick a side to keep the lane moving. |

⚠ **"Minimum that builds" is not licence to skip the measurement and hand-pick packs.** The walk
produces the set; the table above only decides what to do with each row. A lane that eyeballs the
§1.2 table and guesses has inverted the ruling — and §11B forbids exactly that.

⛔ **"UPGRADE OVER A TRACKED FALLBACK" IS A DOCUMENTED PROPERTY OF FOUR PACKS, NOT A GENERAL TRUTH —
AND POLYPERFECT IS THE LIKELY SURPRISE.** `REQUIRED_PACKS.md` §2's pack table documents a fallback for
**KayKit, People, the AccuRig `Resources/Enemies` family, and ParticlePack** (the last one explicitly
having **none**). **`polyperfect`, `Synty`, `Blink`, `Mirza Beig`, `Spells Pack`, `Hovl Studio`,
`Supercyan` and `Tech hud elements` are NOT in that table** — §3 merely lists them as zip-travel. **They
have no documented fallback, and this spec does not assert they have one.**

Polyperfect specifically: `CastleHubBuilder.cs:156` bakes the hub *"from Quaternius + polyperfect
packs"*, and a baked scene holds **mesh GUIDs**, which no warn-on-missing code path can substitute for.
`HubStructureVisualInjector` swaps only **8 named structures** to `Resources/Structures` models. So the
plausible outcome is that **polyperfect is minimum-that-builds** — 438.4 MiB the hub must carry.
**P0-2 decides; do not pre-classify any untabled pack as an "upgrade" to make the hub smaller.**

⚠ **`Assets/Firebase/`, `Assets/ExternalDependencyManager/`, `Assets/GoogleSignIn/`,
`Assets/GeneratedLocalRepo/` are gitignored plugin trees, not art, and have no "fallback" concept.**
If the player build does not compile without them they are minimum-that-builds by definition. P0-2
must classify them explicitly rather than letting an art-shaped rule swallow a compile dependency.

⛔ **The hub repository MUST be PRIVATE.** It carries commercial Asset-Store licensed packs
(Polyperfect, Synty, Mirza Beig, Hovl Studio, Spells Pack, Supercyan, Blink, KayKit).
⚠ **NOT VERIFIED, both halves — and no EULA was read this session:** whether a public mirror would
breach those licences (the default working assumption for commercial packs, and the reason "private"
is the floor here), and whether the EULA permits mirroring them into a **private** second repository
at all. **P0-6 confirms both before a single pack byte is published.** Do not let an implementation
lane settle a licence question by proceeding.

### 7.2 The publish step (working machine → hub)

A tracked script — proposed `tools/prod-hub-publish.ps1`, **to be written by the implementation lane,
not by this spec** — that:

1. Refuses if a Unity process is running (same guard as `run-unity-method.ps1:130-133`).
2. Asserts `D:\eoa` is clean enough to publish (no unstaged tracked changes it would silently carry).
3. Mirrors the working tree into the hub checkout: every tracked file **plus** the required-and-
   untracked manifest from P0-2, and **nothing else** — no `Library/`, no `Builds/`, no `Temp/`, no
   `ServerData/`, no `.env*`, no `*.keystore`, no `keystore.properties`, no `key.json`/`*.jwk`.
4. **Emits a secret-scan refusal** if any of those secret patterns would be published. Fail closed.
5. Commits and force-pushes the hub as a single replacement commit (the owner's *"wipe every time"*).
6. Prints `PROD_HUB_PUBLISH_OK <sha> files=<n> bytes=<n>` — **its own distinct marker**, per §8's
   distinct-markers rule.

⛔ **THE PUBLISH SCRIPT MUST NEVER RUN `git clean -xdf` OR ANY WIPE INSIDE `D:\eoa`.** That command in
the working tree would delete **17.71 GiB of untracked art that exists on no other machine** — §1.1
already shows one pack has gone missing once. The wipe belongs on the hub runner and nowhere else.

### 7.3 The hub job

**Trigger: `workflow_dispatch` (manual), with a target selector.** Recommended, with the reasons:

- **On-merge is wrong here.** Every content build re-hashes every bundle (§16), so every merge would
  push to R2 with production credentials. `r2_sync.py` is additive (§2.4), so the bucket grows
  unbounded and nobody is watching it (R-7). And the owner felt-tests every build anyway — a build she
  did not ask for has no consumer.
- **Tag-triggered is right later, for the AAB only** (Phase 3), where a tag is a genuine release event.
- Manual dispatch also lets her pick the target, which is exactly what she asked for
  (*"APK, AAB or web UI"*).

**Job steps, in order.** Every step judged by **marker + fresh log**; **marker absent = job fails**.

| # | Step | Marker asserted | On absence |
|---|---|---|---|
| 1 | **Wipe.** `git fetch --all` → `git reset --hard <sha>` → `git clean -xdf` (hub checkout only) → `git lfs pull`. ⚠ **If OQ-1 lands on "keep `Library/` warm", this needs `-e Library` or the clean deletes the very cache the answer preserves.** | clean tree + LFS hydrated | fail |
| 2 | `powershell -File tools\art\verify-runtime-art.ps1 **-Strict**`, **with the P0-2-derived allow-list of packs the hub is legitimately without** (§7.4) | non-zero exit = fail. ⚠ Without the allow-list `-Strict` fails every hub build by design — scope it, never drop it | fail |
| 3 | `run-unity-method.ps1 -Method DeNelle.Editor.CompileGate.Run -ExpectMarker COMPILE_GATE_OK` | `COMPILE_GATE_OK` | fail |
| 4 | `run-unity-method.ps1 -Method …DataRegression.RunAll -ExpectMarker REGRESSION_OK` | **`REGRESSION_OK \d+/\d+ suites`** (shaped, per `command-centre.ps1:297`) | fail |
| 5 | `run-unity-method.ps1 -Method …UICaptureLaunch.RunCaptureHeadless -ExpectMarker UI_CAPTURE_OK` | `UI_CAPTURE_OK <n>` + PNGs uploaded | fail |
| 5b | **Substitution detector (§7.4)** — diff this run's captures against the reference set for the same `SOURCE_SHA`, minus the accepted-diff ledger | **`ART_SUBSTITUTION_OK <n> captures clear`**, else `ART_SUBSTITUTION_DIFF <n>` + the differing PNG pairs published | **fail on any diff NOT in the ledger** (§7.4.4) |
| 6 | **Target build** — WebGL: `build-webgl.ps1`; APK: `overnight-apk-build.ps1`; AAB: `google-play-aab-build.ps1` | `[AndroidBuild] SUCCEEDED` / `AAB_*`; **WebGL has no Unity marker (§2.1) — synthesize from `index.html` + fresh `webgl-build.log`, exactly as `command-centre.ps1:366-374` does, and label it synthesized** | fail |
| 7 | **Signature gate** (APK/AAB only) — `apksigner verify --print-certs`, fingerprint must equal the recorded live fingerprint (§9.3) | **`SIGNING_CERT_MATCH_OK <sha256>`** (new marker) | fail |
| 8 | **Content ship** — `tools\r2-ship.ps1` (blocking; never `-WarnOnly`) | `R2_PARITY_OK` on a fresh **UTF-16LE** `Builds/r2-parity.log` | fail |
| 9 | **Publish artifacts** — the player artifact, `ServerData/<target>/catalog_*`, every `Builds/*.log`, every status `.txt`, the UI-capture PNGs | `PROD_HUB_ARTIFACT_OK` | fail |

**Deliberately NOT in the hub job:** `distribute-android.ps1` (Firebase), `tools/web-ship.ps1` (the
four-surface Vercel registry), `install-apk-to-seeker.ps1` (needs a device), and anything under
`tools/command-centre.ps1` that promotes production. **The hub builds and proves. It does not
distribute.** Distribution stays a deliberate act on the working machine, where the owner is.

`SCHEMA_PARITY_OK` is a **precondition of the APK and AAB builds** (`overnight-apk-build.ps1:62-68`,
`google-play-aab-build.ps1`), so the hub must hold `DATABASE_URL` **just to build an APK**. See R-3 —
this is a real objection to the whole design, not a footnote.

### 7.4 ⛔ THE SUBSTITUTION DETECTOR — the owner must never be the one who spots this

**The accepted risk, stated where nobody can miss it (R-11).** Minimum-that-builds means the hub
builds against **tracked fallbacks** wherever a pack is an upgrade. `REQUIRED_PACKS.md` §1 is explicit
about what that looks like: *"generic silhouettes / capsules ('Bryn is a pill'), untextured bodies, and
identical unarmed enemies … skeletons instead of per-type-armed Hollow Ones, box doorway instead of
KayKit dungeon geometry."* **The game still runs. Nothing errors. It just looks less rich.** The owner
was told this and accepted it.

**That is the same silent-failure shape as §16, and this project has a standing rule about it:** the
owner is NEVER the bug detector (CLAUDE.md §14; memory `never-dragdrop-or-manual-playtest`). "We'll
notice on the phone" is not a plan — it is the exact failure §14 exists to abolish. So the risk is
**detected mechanically**, not accepted bare.

**The detector, built from parts this repo already has:**

1. **The hub already runs `UICaptureLaunch.RunCaptureHeadless`** as gate step 5 (§7.3) and asserts
   `UI_CAPTURE_OK <n>`. Those PNGs are already being produced and uploaded.
2. **Establish a baseline.** The working machine — which HAS the packs — runs the same capture at a
   known-good commit. That PNG set is the **reference**, stored with the commit sha that produced it.
3. **The hub diffs its capture set against the reference** and emits its own marker:
   **`ART_SUBSTITUTION_OK <n> captures clear`**, or `ART_SUBSTITUTION_DIFF <n>` naming each differing
   capture. *(Named to avoid `-Pattern 'PARITY_OK'` colliding with `R2_`/`WEB_`/`SCHEMA_PARITY_OK`.)*
4. ⛔ **AN ACCEPTED-DIFF LEDGER, NOT A BLANKET PASS OR A BLANKET FAIL. Get this shape right — the
   obvious designs both fail.**
   - *Fail on every diff* is a **veto on the ruling**, not a mitigation. Under minimum-that-builds the
     hub renders fallbacks by design, so its captures differ from the packed reference on **every build,
     forever**. A permanently-red gate gets switched off, and then R-11 has no detector at all.
   - *Warn on every diff* is "we'll notice," which §14 abolishes.
   - **The right shape already exists in this repo: `proof/owner-validations.json`** (see
     `tools/owner_validations.py`) — a committed, human-readable, diffable ledger of the owner's
     sign-offs, which `board_build.py` **reads and never rewrites** (this session's rebuild printed
     `VALIDATIONS_OK 228 recorded, 226 validated, preserved across rebuild`). **Model the accepted-diff
     ledger on it, in `proof/`, for the same reason: a substitution the owner has looked at and accepted
     is evidence, and it must survive every rebuild.**
   - **Therefore:** a diff **not** in the ledger **FAILS the build** and publishes the PNG pair. The
     owner (or a seat, on her word) looks at the pair and records acceptance, keyed by **capture id +
     the reference image hash**. The detector then passes that known substitution and **fails only NEW
     ones.** Fail closed; PO reviews and accepts (CLAUDE.md §13 — the PO closes, never the CLI). The
     gate then guards **drift**, which is the thing that can actually hurt her.
5. ⚠ **KEY THE REFERENCE ON THE SOURCE SHA, NOT THE HUB'S.** The hub's history is replaced on every
   publish (§7.2), so its commit sha never equals `eoa`'s and "same commit" is meaningless there. The
   publish script must stamp the **source** sha into the hub as a tracked `SOURCE_SHA` file, and the
   reference capture set keys on that.
5. **Screenshots are primary evidence for visual defects** (memory
   `screenshots-are-primary-evidence-for-visual-defects`): FlowTrace shows what the code believes, the
   screenshot shows what the player sees. Substitution is invisible to every marker in §2.2 and visible
   in a PNG. This is the only gate shape that can catch it.

**Cheaper first line, run before the captures:** `tools/art/verify-runtime-art.ps1 **-Strict**` is
already gate step 2 and **already warns per-pack on absence** (its PACK tier). Under minimum-that-builds
those warnings are now *expected*, so `-Strict` alone would fail every hub build. **The implementation
lane must therefore give the hub an explicit allow-list of packs it is legitimately without** — derived
from P0-2, not hand-written — and fail on any absence outside that list. **Do not "solve" this by
dropping `-Strict`:** that would discard the one existing check that notices a missing CRITICAL or
COMMITTED tracked asset, which is a real break, not an accepted substitution.

⚠ **TWO THINGS NOT VERIFIED, AND THE FIRST COULD MAKE THIS DETECTOR CHECK NOTHING.**

1. **Does `RunCaptureHeadless` actually frame WORLD GEOMETRY AND CHARACTER BODIES, or only UI panels?**
   Its marker is `UI_CAPTURE_OK <n>` and the sample value seen in-tree is `UI_CAPTURE_OK 51`
   (`CaptureProvenanceRegression.cs:526`) — **51 reads like a panel count, and the name says UI.**
   `HubSceneLiteralRegression.cs:9` shows it boots a scene, so it is not purely synthetic, but **nothing
   I read this session proves it photographs the things substitution actually degrades** — "Bryn is a
   pill", untextured bodies, box doorways instead of KayKit walls. **If it captures UI, §7.4 measures
   the wrong thing entirely** (§11B: measuring something is not measuring the right thing) and the
   detector is decorative. **P0-9 must open the capture output and look before a line of diff code is
   written.** If it is UI-only, the hub needs a **world-view capture**, and
   `Assets/Editor/DungeonSceneCapture.cs` (marker `DUNGEON_CAPTURE_OK`) is the existing in-repo pattern
   to model it on — it was written precisely because *"nothing in the project could do that."*
2. **Is the output deterministic enough to diff?** Lighting, time-of-day, random dressing and font
   rasterisation all routinely defeat byte-exact PNG comparison. Run the capture twice on the working
   machine with an unchanged tree and compare. Not byte-stable → the diff must be perceptual (a
   tolerance) or scoped to a fixed capture scene. **Do not assume byte-equality: a flapping gate gets
   switched off, and then the R-11 detector is gone.**

### 7.5 Where artifacts land and how they are reached

- **GitHub Actions run artifacts**, attached to the run. She reaches them from the run page in a
  browser, on any device — which is the *"share to that device so you can access"* half of her request.
- **The CLI seat reaches them with `gh run download <run-id>`.** `gh` is already used in this repo.
  ⚠ **NOT VERIFIED:** artifact retention default and size cap on the owner's plan — P0-3.
- **The parity/gate evidence is the point**, not a nicety: `Builds/` is gitignored (`.gitignore:8`), so
  logs do not travel by git. Uploading them is the **only** way the CLI seat gets marker evidence
  without shell access to the runner, and §11B forbids claiming a gate passed without reading it.
- **Discord, for free.** `run-unity-method.ps1:77-92` already routes every `VERDICT=` line through
  `tools/status-post.mjs`, which is a silent no-op when the webhook is absent. Give the hub that one
  webhook secret and its pass/fail lands in the channel the seats already watch, with no new plumbing.

---

## 8. THE CREDENTIAL SURFACE — NAMED IN FULL

**Secrets the hub needs, by phase.** Nothing here is designed around silently.

| Secret | Consumer | Needed from |
|---|---|---|
| `R2_S3_ENDPOINT`, `R2_BUCKET`, `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY`, `R2_PUBLIC_URL` (materialised as `.env.r2`) | `tools/r2_sync.py:66-67` | Phase 1 |
| `DATABASE_URL` (production Postgres) | `tools/schema-parity.mjs:205` | Phase 2 (APK precondition) |
| Android keystore **bytes** + `keystore.path/alias/storepass/keypass` (materialised as `keystore.properties`) | `AndroidBuild.cs:369-403` | Phase 2 |
| Firebase auth (non-interactive) | `distribute-android.ps1:71` | **not in the hub job** — kept on the working machine |
| `VERCEL_TOKEN` | `command-centre.ps1:266`, `web-ship.ps1` | **not in the hub job** |
| Discord webhook | `tools/status-post.mjs` | optional, Phase 1 |

**Toolchain the runner must carry:** Unity `6000.4.8f1` + Android Build Support (OpenJDK/SDK/NDK) +
WebGL Build Support; Windows PowerShell 5.1; `python` with `boto3`; `node`; `java` + `bundletool` (AAB
size guard, which **fails closed** as `AAB_SIZE_UNMEASURED`); `git` + `git-lfs`; `gh`.
`firebase` and `vercel` CLIs are **not** needed given §7.3's exclusions.

⚠ **NOT VERIFIED:** how `distribute-android.ps1:71` authenticates non-interactively (it assumes
`firebase login` was run by a human — `:3-8`). Since Firebase distribution stays off the hub, this does
not block the design, but do not let a later lane "just add it."

---

## 9. RISKS — stated honestly

**R-1 — Secrets multiply, and one of them is production.** Every secret above becomes a second copy on
a second machine. `DATABASE_URL` is the sharpest: it is the **production** database, and the hub needs
it *merely to build an APK*, because `overnight-apk-build.ps1:62-68` refuses without
`SCHEMA_PARITY_OK`. **A build server holding production database credentials is a real expansion of
blast radius.** OQ-4 puts three options to the owner: (a) hub holds a **read-only** `DATABASE_URL`;
(b) schema parity is proven on the working machine and the hub is handed a signed fresh proof;
(c) accept it. This spec recommends (a) and **NOT VERIFIED** whether `schema-parity.mjs` works on a
read-only role — trivially testable, and P0-7 tests it.

**R-2 — ⚠ THE SIGNING CERTIFICATE IS THE ONE THAT CAN BRICK UPDATES.** It is already recorded as
**never captured against the live release** (CLAUDE.md §11B.A). `AndroidBuild.cs:372-373,393-394`
**silently falls back to DEBUG signing** with only a `LogWarning` when `keystore.properties` is absent
or incomplete. `morning-ship-chain.ps1:121-128` greps for the string `RELEASE signing` — which proves
**a** release key was used, **not that it is THE key the live release carries**. A hub with a different
keystore produces an APK that installs fine on a clean device and **cannot update an installed copy**;
testers hit a signature-mismatch days later, exactly as `morning-ship-chain.ps1:117-120` describes.
**Mitigation, and it is a hard gate:** P0-8 captures the live release's certificate fingerprint from the
Seeker, records it in a tracked file, and step 7 of §7.3 refuses any APK/AAB whose
`apksigner verify --print-certs` fingerprint differs. **Nothing ships to a device from the hub until
that fingerprint is recorded.**

**R-3 — LFS storage and bandwidth, paid, per build — and it RATCHETS.** §1.3: 15.20 GiB of the ignored
set routes to LFS and the hub re-clones on every run by design. Worse, §7.2's *"force-push a single
replacement commit"* rewrites git history but **LFS objects are content-addressed and are not reclaimed
by a history rewrite** — every changed PNG/FBX adds a new object while the old one keeps counting
against storage. So the "wipe every time" model makes LFS storage grow monotonically, not stay flat.
**NOT VERIFIED** what that costs on the owner's plan, or whether GitHub garbage-collects orphaned LFS
objects at all — **P0-3 must read both off GitHub's own docs.**

⚠ **The minimum-that-builds ruling (§0) substantially DEFUSES this risk, and that is a large part of
why it is the better ruling** — the hub no longer carries 15.20 GiB of packs, only the P0-2 set. But
it does not eliminate it: the tracked tree already carries **3.6 GB across 3,538 LFS objects**, and the
hub re-clones that on every wiped build. **Size the real figure off P0-2, not off this paragraph.**
If it is still prohibitive, the fallback is a **pre-seeded runner** (packs live on the runner's disk
outside the checkout; the wipe preserves them) — a **deliberate, named deviation** needing the owner's
word (OQ-2), because §11B.B forbids taking it silently.

**R-4 — The hook inversion.** §3.4. The `pre-push` gate quietly stops meaning anything the moment two
machines build content. Rated the **highest-severity design risk** here because it is silent, and §16
records four separate incidents of exactly this failure mode.

**R-5 — Stale-parity sideload.** §3.5. `install-apk-to-seeker.ps1:136` proves the *local* tree, not the
downloaded artifact.

**R-6 — WebGL has no Unity-emitted success marker.** `build-webgl.ps1:6`. The hub inherits a synthesized
marker at exactly the step Phase 1 is meant to prove. A follow-up ticket should make `WebGLBuild.BuildWebGL`
emit a real one. Out of scope here.

**R-7 — Unbounded R2 growth.** `r2_sync.py` never deletes (§2.4). More builders means faster growth,
uncosted and unwatched.

**R-8 — Licence terms.** §5 and §7.1: Unity build-server activation and Asset-Store redistribution into
a second repo are both **NOT VERIFIED** and both are legal questions, not technical ones.

**R-9 — A green hub run is not a felt-test.** CLAUDE.md §13: the PO felt-verifies and closes. Every
marker in the chain was green on 2026-08-19 and on 2026-08-20, and both builds were broken. **The owner
still plays it.** This is an acceptance criterion, not a caveat.

**R-11 — ⚠ THE RISK THE OWNER EXPLICITLY ACCEPTED: silent art substitution.** Minimum-that-builds
(§0) means the hub renders **tracked fallbacks** wherever a pack is an upgrade, so a hub build can look
different from a working-machine build of the same commit — *"generic silhouettes / capsules … skeletons
instead of per-type-armed Hollow Ones"* (`REQUIRED_PACKS.md` §1). **Nothing errors; the game runs.** She
was told this, told it might not surface until she saw it on the phone, and accepted it. **It is
recorded here as an accepted risk, not a resolved one.** Mitigation is **§7.4's capture diff plus the
scoped `-Strict` pack allow-list** — the risk is accepted on the condition that a **machine**, not the
owner, is the thing that notices. If the implementation lane cannot make §7.4 work (see P0-9), it must
say so and get a fresh ruling rather than shipping the acceptance without the detector.

**R-10 — Runner minutes and machine cost.** A self-hosted runner is a physical Windows machine that
must exist, stay powered, hold ~40 GiB of checkout + `Library/` + build output, and stay pinned to
`6000.4.8f1`. **NOT VERIFIED** whether the owner has such a machine available; if the plan is a cloud
Windows VM, that is a recurring bill and its own ticket.

---

## 10. PHASING — recommended, with the caveat the evidence forces

**Phase 0 — prove the clean room is even possible. No CI, no runner, no secrets.**

Runs on the working machine. **The whole design is gated on this and it must not be skipped.**

- **P0-1** — `Assets/Quaternius`: §1.1 already settles the code question with evidence (editor-only; two regressions ban it from runtime; the one runtime call is a `CreatePrimitive` stub). **What remains is the one thing grep cannot see:** open **every scene in the build list** in the editor and report whether any carries **unresolved GUID references** to the absent pack. `Main_Castle_Overworld` is the obvious one (`HubStructureVisualInjector.cs:7`), **but Village2 matters just as much** — it is the live raid target (CLAUDE.md §8) and `Village2Generator.cs:4` describes it as *"modular Quaternius walls + balcony ramparts"*, with `Village2Build.cs:30` harvesting from the pack's own sample scene. Report the finding either way; do not infer it from the build appearing to work.
- **P0-2** — **The dependency walk. The keystone of the whole ticket — "minimum" is undefined until this runs.** An Editor method that walks `AssetDatabase.GetDependencies` over the build scenes + every Addressables group + `Resources/`, and emits the **referenced-but-untracked** set with per-file bytes and its own marker. Classify every row against the §7.1 contents table, and **explicitly classify the gitignored plugin trees** (`Assets/Firebase/`, `Assets/ExternalDependencyManager/`, `Assets/GoogleSignIn/`, `Assets/GeneratedLocalRepo/`) rather than letting an art-shaped rule swallow a compile dependency. **This produces the real hub size, between the 343.58 MiB floor and the +17.71 GiB ceiling.**
- **P0-3** — Read GitHub's own current docs and record, with the date read: per-file size limit, LFS per-file limit, LFS storage + bandwidth quota on the owner's plan, **whether orphaned LFS objects are garbage-collected after a history rewrite (R-3)**, hosted-runner disk, artifact retention and size caps. **Record NOT VERIFIED for anything the docs do not state.**
- **P0-4** — Time one cold build with a wiped `Library/` on the working machine, to give OQ-1 a real number.
- **P0-5 / P0-6** — Unity build-server licensing; Asset-Store redistribution into a private mirror. Both are owner/legal calls.
- **P0-7** — Does `schema-parity.mjs` pass against a read-only DB role?
- **P0-8** — ⚠ **Capture the live release certificate fingerprint from the Seeker and record it in a tracked file.** Device step. Gates every later phase that produces an installable artifact.
- **P0-9** — ⚠ **Two questions about `RunCaptureHeadless`, and the FIRST decides whether §7.4 is worth building at all.** (a) **Open the capture PNGs and look: does it frame world geometry and character bodies, or only UI panels?** The marker is `UI_CAPTURE_OK`. If UI-only, the detector must be rebuilt on a world-view capture (`DungeonSceneCapture.cs` is the in-repo pattern) or it measures nothing. (b) Is the output byte-stable across two runs on an unchanged tree? If not, the diff needs a perceptual tolerance or a fixed scene. **Both answered before a line of diff code is written.**
- **P0-10** — Produce the **first reference capture set** on the working machine (which has the packs) at a known-good commit, and record the sha alongside it. §7.4 has nothing to diff against until this exists.

**Phase 1 — WebGL.** Prove the clean-room checkout, the wipe, and steps 1-5 + 8-9 of §7.3.
**No keystore, no device, and if it stops at "artifact uploaded" rather than "deployed", no `VERCEL_TOKEN`.**
It still exercises §16 fully — **the web build pulls remote content too**, and the evidence is stronger
than the CORS print string at `r2_sync.py:179`: `tools/r2-ship.ps1:88` carries `WebGL` in its
`ValidateSet` of shippable targets, and the PROD-022 banner entry (`CLI_LANES_WO_NUMBERS.md:4-12`)
records structure bundles retry-storming with *"model not found ... Check ... its bundle is uploaded to
the CDN"* on the **Pi web build**. A web build with an unpushed bucket is broken the same way an APK is.
**Phase 1 explicitly ends at an uploaded artifact.** `tools/web-ship.ps1`'s
four-surface registry is its own trap (`:12-33`: a `--prod` deploy updates one project and the domain
named after the game keeps serving stale bytes) and must stay out of the first proof.

**Phase 2 — APK.** Adds `DATABASE_URL`, the keystore, and the §7.3 step-7 signature gate. **This is the
payoff** — the APK is the vision (owner ruling 2026-09-02) and the ~10 minutes she wants back.

**Phase 3 — AAB.** Adds store signing and the bundletool size guard on top of a proven signature gate.
Last, because it is the one that touches the store.

**Where I agree with the coordinator, and the one caveat.** The WebGL-first ordering is right for the
reason given — fewest secrets, no keystore, no device. **The caveat: Phase 1 delivers her nothing she
asked for.** She asked for APK time back. So **Phase 1 must be short**, and if it starts sprawling
(the four-surface Vercel registry is where that happens), cut it to "clean checkout + gates + artifact
upload, WebGL binary optional" and go straight to Phase 2. Do not let the safest phase become the
longest one.

---

## 11. OPEN OWNER QUESTIONS — the spec cannot proceed past Phase 0 without these

**OQ-1 — Wipe vs. warm cache.** *"Wipe every time"* means a full `Library/` reimport of ~20 GiB on
every build. Recommendation: **wipe the source tree** (`fetch` + `reset --hard` + `clean -xdf`) and
**keep `Library/` warm** (or run a Unity Accelerator). Same clean-room guarantee for source, without
paying the reimport hourly. Deviating from her literal words needs her word first (§11B.B).

**OQ-2 — If LFS cost is prohibitive (P0-3), is a pre-seeded runner acceptable?** Packs live on the
runner's disk, preserved across wipes. It is a deliberate departure from "no second source."

**OQ-3 — After the hub exists, does `D:\eoa` still build content?** Recommendation: **no** — the hub is
the only content builder, which keeps the `pre-push` hook honest (§3.4a).

**OQ-4 — Does the hub hold a production `DATABASE_URL`?** Recommendation: a **read-only** role
(pending P0-7).

---

## 12. ACCEPTANCE CRITERIA

1. Phase 0 is complete and **every P0 item is answered or explicitly recorded NOT VERIFIED with the
   reason** — no item silently dropped.
2. A hub run on a **wiped** checkout emits, on **fresh** logs: `COMPILE_GATE_OK`,
   `REGRESSION_OK <n>/<n> suites` **in its shaped form**, `UI_CAPTURE_OK <n>`, the target's build
   marker, and `R2_PARITY_OK` **decoded as UTF-16LE**.
3. **Deleting the marker from any one log fails the job.** Demonstrated, not asserted — the same way
   `run-unity-method.ps1:38-42`'s `-JudgeExistingLog` demonstrates the stale-log path without staging a
   crash. **A gate that has only ever been seen passing is not a proven gate**
   (memory: `prove-the-success-path-not-just-the-refusal`).
4. Artifacts include the player binary, `ServerData/<target>/catalog_*`, every `Builds/*.log`, every
   status `.txt`, and the UI-capture PNGs; `gh run download` retrieves them.
5. No secret and no `Library/`, `Builds/`, `Temp/`, `ServerData/`, `.env*`, `*.keystore`,
   `keystore.properties`, `key.json`, `*.jwk` appears in the hub repository. Verified by scan.
6. The hub repository is **private**.
6b. **The hub carries the P0-2 set and nothing beyond it** (§7.1 contents table). Every excluded pack
   is excluded because P0-2 classified it, not because someone judged it unnecessary.
6c. **`ART_SUBSTITUTION_OK` is emitted, and the detector is PROVEN to fire** — introduce a deliberate art
   substitution and show `ART_SUBSTITUTION_DIFF` naming it. Per criterion 3, a detector only ever seen
   passing is not a proven detector, and this one is the sole mitigation for the risk the owner
   accepted (R-11).
7. **(APK/AAB only)** `SIGNING_CERT_MATCH_OK` proves the artifact's certificate fingerprint equals the
   fingerprint recorded in P0-8. **Absent that recorded fingerprint, the hub ships nothing to a device.**
8. ⛔ **A green hub run is NOT a felt-test.** The PO plays the build and closes the ticket
   (CLAUDE.md §13). No lane may close this on markers alone.

## 13. WHAT NOT TO TOUCH

- ⛔ **Do not re-inline the R2 push or verify** into the workflow, a doc, or a second script.
  Call `tools/r2-ship.ps1`. CLAUDE.md §16 and `r2-ship.ps1:55-74` record what happened last time.
- ⛔ **Do not add an override or `-WarnOnly` to the hub's parity step.** `install-apk-to-seeker.ps1:136`
  is the one legitimate warn-only call site in the repo, and it is legitimate because it sideloads
  rather than distributes.
- ⛔ **Do not add an override flag to `.githooks/pre-push`.** It has none on purpose (`:20-25`).
- ⛔ **Do not run any wipe inside `D:\eoa`.** §7.2.
- ⛔ **Do not modify `Assets/`, `.gitignore`, or any existing ship script** as part of this ticket. If
  one of them must change, that is a separate WO with its own gate.
- ⛔ **Do not restate a marker, a Unity version, a WO number or a face count into a second document.**
  Point at the source. Every expensive bug in this repo is copied state that went stale
  (CLAUDE.md §2, §5, §16).

---

## 14. PROVENANCE

Every claim above cites a file:line read on **2026-09-06** in `D:\eoa`, or is marked **NOT VERIFIED**.
Sizes were measured this session with `git ls-files --others --ignored --exclude-standard`,
`Get-ChildItem -Recurse -File -Force | Measure-Object -Sum Length`, `git count-objects -vH`, and
`git lfs ls-files`. Nothing was taken from a doc's summary of the code, and nothing was recalled.
Where a script's own comment asserts a fact about `Assets/Editor/AndroidBuild.cs`, that file was opened
and the lines re-read at source (§2.3) rather than quoted from the comment.
