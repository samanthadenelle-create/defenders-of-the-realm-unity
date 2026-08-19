> ## ▶ REFRESHED 2026-08-18 — the live anchor is `CANON_GROUND_TRUTH_2026-08-18.md`
>
> The **08-18 LIVE THREAD** below is current; every thread under it is SUPERSEDED and kept as history.
> *(This banner read "REFRESHED 2026-08-09 / anchor 08-09" until tonight — nine days stale, and it
> disagreed with the LIVE THREAD directly beneath it, which had been re-anchored to 08-16. Re-stamp
> both in the same change as any new anchor.)*
>
> ## ⚠ (previous) REFRESHED 2026-08-09 — SUPERSEDED 2026-08-18
>
> The **08-09 LIVE THREAD** below is current; every thread under it is SUPERSEDED and kept as history.
> The 08-08 anchor is bannered and is **INVERTED** on its two headline sections (the machine block is
> resolved; the dungeon-stair hunt is closed) — do not act on it.
>
> Per CLAUDE.md §15 the newest `CANON_GROUND_TRUTH_<date>.md` wins on any conflict with this file.
# DeNelle Studios — Project Canon Loader

> ## ▶ LIVE THREAD (2026-08-18) — READ BEFORE WORKING
> **Reality anchor = `CANON_GROUND_TRUTH_2026-08-18.md`** (minted tonight; the 08-16 anchor is now
> bannered/frozen). Branch `wip/village2-and-f8-tickets`. **This block records NO HEAD sha, NO
> commits-ahead, NO suite count and NO next-free WO number** — read them off `git status` /
> `git rev-list origin/..HEAD`, the MARKER files under `Builds/`, and the `CLI_LANES_WO_NUMBERS.md`
> banner (sole authority, two disjoint blocks). Save schema version → read
> `SaveSchema.CurrentVersion` at `Assets/_Modules/Core/State/SaveSchema.cs:41`. One committer, staged
> by explicit path, never `git add -A`.
>
> **Current state, in one breath.** This is a LIVE game on the Solana dApp Store, so the blast radius
> of a content change lands on installed devices, not on this tree. Tonight's overnight loop turned on
> one lesson: **a correction pass corrected the wrong file, twice.** `f995c4706` baked ten structure
> FBXs upright and zeroed ten rows in `Assets/OffsetForge/offsets.json` — **but those rows are INERT
> for structures** (`AttachmentOffsetRegistry` is keyed by hero/enemy attachment mesh ids). The live
> orientation channels are `entry.orientation` in `structures-catalog.json`
> (`Assets/_Modules/Village/Catalog/StructureFactory.cs:151-158`, applied only when `manual == true`)
> and hardcoded `pitchDeg` in `Assets/_Modules/Village/HubStructureVisualInjector.cs` (~:81-91). Both
> still carried the legacy `-90`, so bake **and** correction both applied and the models lay down. The
> catalog channel was fixed for five ids (catalog **version 22 → 23**); **the hub-injector channel was
> IN FLIGHT at time of writing.** **No headless gate can see orientation** — the instrument for it
> exists unused at `Assets/Editor/WoodenWatchtowerBuilder.cs:277` (`UprightAspectMin = 1.2f`; 1.70–1.92
> upright vs 0.52–0.59 flat), filed **PROD-008**.
>
> **⛔ THE ONE LINE TO CARRY OUT OF THIS BLOCK: eight `-90`s are CORRECT AND MUST STAY** —
> `pet-house`, `market`, `arcane-tower`, `collector_farm`, `collector_lumbermill`, `lumberyard`,
> `foundry`, `silo`. Their FBX metas read `bakeAxisConversion: 0`, so the `-90` is what stands them
> up. A "tidy up the remaining -90s" pass lays all eight down, **including `collector_lumbermill`,
> the FTUE's first building.** The rule is **"-90 is legacy IFF that FBX's meta says
> `bakeAxisConversion: 1`"** — check the meta, per asset, every time.
>
> **Also settled tonight (see the anchor for file:line):** the **sign-in gate (PROD-006)** would have
> shown SIGN IN on every launch **forever** for a wallet-only player, because it read only
> `FirebaseAuthService.IsSignedIn` while the identity law says only the wallet path binds — fixed with
> a pure `ShouldContinueWithoutLogin(...)` + `GameStateService.HasAttestedWalletIdentity`, **no timing
> constant**, pinned `[login-gate]`; **MWA session sealing WORKS** (`6e9f86cc3`) and only the gate was
> ignoring it; the **Realm Store (PROD-003)** stands via an owner-authored
> `Quaternion.Euler(0, 180, 90)` and is **deliberately NOT in Offset Forge**
> (`Assets/Editor/TripoAxisBake.cs:147-154` auto-rewrites baked rows); the **CDN is ~84.26 MiB across
> two unlabelled remote groups** pulled through a **synchronous main-thread `WaitForCompletion`** with
> **no prewarm** (PROD-009/010), and **keeping the CDN was RIGHT** because
> `m_DisableCatalogUpdateOnStart: 0` means going local = invisible buildings for installed players;
> **every APK build REQUIRES `python tools/r2_sync.py --push ServerData`** (NOT `ServerData/Android`;
> the docstring at `tools/r2_sync.py:22` is wrong) with **no gate to catch a mismatch** (PROD-011);
> **monetization stays OFF** behind five independent refusals plus two hard blockers (no
> server-authoritative economy, no payment verification); and an **api/ security fix moving
> grant-bearing endpoints to the signed wallet rail is UNCOMMITTED and NOT DEPLOYED** — ⚠ it
> **BREAKS guest promo redemption and referral claiming**, so deploying is the owner's call.
>
> **Known-red baseline: 4** (`CaravanStatusChip`, `vfx-self-contained`, `vfx-null-slot` — awaiting
> owner ruling, `WANDERER BUBBLE x4` — needs a dungeon re-bake in an **isolated worktree**). Two new
> reds tonight were **fixed at source, not baselined**. **Never restate a suite count from a doc** —
> the three entry points emit DISTINCT markers (`REGRESSION_OK` / `CHECKIN_SUITE_OK` /
> `SESSION_GUARDS_OK`).
>
> **IN FLIGHT when written:** the hub-injector orientation lane; the gear seating lane (a prop
> measured `worldBounds=(0,0,0)` and a `parent-scale compensate` firing **every frame**). Re-verify
> both at source before acting.
>
> **Open owner rulings — do NOT answer these:** PROD-012 is-internet-required · pack pricing (five
> SKUs above the $5 early-access cap, up to $49.99) · mainnet · the Realm Store vendor NPC body ·
> storefront height 4 m vs the 1.25 landmark tier · `vfx-null-slot` retag-or-repair.

> ## ▶ LIVE THREAD (2026-08-09) — SUPERSEDED (see 08-18 above) *(header re-anchored 2026-08-10 morning)*
> **Reality anchor = `CANON_GROUND_TRUTH_2026-08-16.md`** (minted 2026-08-16; 08-09 and 08-07 are now
> bannered/frozen). ⚠ The HEAD/gate/tree lines in the rest of THIS block are the 08-09/08-10 snapshot —
> read the 08-16 anchor and `git status` for current state. Branch
> `wip/village2-and-f8-tickets`, **HEAD `07b756b6` (2026-08-09 23:00), PUSHED 2026-08-10 ~10:12 —
> local == origin** (the 68-commit 08-09 wave). ⚠ The tree carries the 2026-08-10 morning fix wave
> UNCOMMITTED while it gates: WO-931 wallet refusal · hero death-pin rebase · battle-music countdown
> gate · WO-945 onboarding build grace (+ agent lanes in flight). Save schema **v38**
> (`SaveSchema.cs:41`, WO-934). **WO-931 is IMPLEMENTED (option b)** — its "READY TO IMPLEMENT"
> lines below are history. One committer, staged by explicit path, never `git add -A`.
> Gates last emitted, **read off the marker files**: `Builds/gate-ship3.log` → `COMPILE_GATE_OK` ·
> `Builds/regression-ship3.log` → `REGRESSION_OK 130/130 suites` · `Builds/ui-capture-ship.log` →
> `UI_CAPTURE_OK 44`. ⚠ `Builds/test-results-EditMode.xml` is 930/930 green but **stamped 2026-08-04 —
> five days stale; not current evidence.**
> **Never restate a suite count from a doc.** The three entry points emit DISTINCT markers
> (`REGRESSION_OK` / `CHECKIN_SUITE_OK` / `SESSION_GUARDS_OK`).
>
> **✅ THE 08-08 MACHINE BLOCK IS RESOLVED.** Rebooted 08-08 08:07:21; commit charge **45.7 GB of
> 127.8 GB**, 11.9 GB free, no Unity running. **Windows EXE 08-08 14:33 · Android APK 08-08 20:00
> (572,202,338 bytes) · Firebase ran.** ⚠ **Only the WebGL / web-deploy step never happened** —
> `Builds/WebGL` is still 2026-08-05 and there is **no `Builds/webgl-chain-status.txt`.**
>
> **★ THE DUNGEON STAIRS ARE SOLVED — the PathPartial hunt is CLOSED.** WO-930's one-room stairwell:
> `3ab1bfb6` (**the first floor-to-floor `PathComplete` in project history**) → `e7163c9c` (skinned,
> 0 bad surfaces) → `5f0e23aa` (candle lights + **a caught RED gate: `dg_sunken_vault.json` dual-copy
> drift — Resources held the OLD 17-room layout, and Resources WINS at runtime**) → `cb092b7f`
> (**all 4 content dungeons PathComplete, 12 descents, 0 mate failures, 14/14 dual-copy parity**;
> `dg_descent_probe`/`dg_stair_rig` left on the old model as controls) → `51a89364` (`RoomPrefabMeta` on
> `StairwellRoom` — the overlap gate had been measuring a **20x10 m room as one 10 m cell**).
> **ROOT CAUSE = STAIR YAW:** `GraphDungeonComposer.SolveMate` hardcoded `yaw = 0f` on vertical sockets,
> so only a Delta of 180 landed the flight in the floor hole. **It was never a property of the stair** —
> which is why four rounds of bucketing the stair's scalars all returned nothing. The 08-08 anchor's
> "dump navmesh triangles next" move is DEAD; keep its killed-hypotheses table as history only.
>
> **⚠ HEADLESS GATES CANNOT SEE ORIENTATION — the lesson of the day.** `70a86c17` **reverts** `bb6dc010`:
> `SkinOptions.PreservePrefabRotation` on ALL structures **laid the whole town on its side** (13 catalog
> rows carry a manual -90 that composes to 180), reproducing only on the **dungeon → town return path**
> via `BaseLayoutLoader`, with every marker green throughout. This class needs **eyes**, not markers.
> Narrow fix `439e03ee`: per-catalog-row `RepoProps.preservePrefabRotation` (default false, **exactly one
> opt-in: `tower_ground_archer`**), `StructureFactory.OptsFor` the single reader. ⚠ Still live:
> `Resources/Structures` holds a `.fbx` AND a same-stem `.prefab` — `Resources.Load` is **ambiguous**.
>
> **⚠ STORE PURCHASES ARE RE-GATED OFF AND LOCKED** (`576601e3`). `StubWalletProvider` has **no
> `#if UNITY_EDITOR`/`DEVELOPMENT_BUILD` guard**, ships in every player, fabricates a wallet + a **2000 SKR
> mock balance** + a base58 signature, and `ApplyPackContents` **grants the pack for ZERO payment** while
> firing `purchase_completed` with the fake txSig. **The submitted store build had a tappable Buy button.**
> = **WO-931 READY TO IMPLEMENT**, precondition **3 of 3** before `FeatureFlags.RealmStorePurchase` may
> ever flip on.
>
> **Legal / publishing:** app installs as **"Echoes of Elarion"** (`640bfc1c`); Terms of Use authored and
> hosted verbatim at `site/terms.html`, live at `https://echoes-of-elarion.vercel.app/terms` (verified 200),
> governing law Texas, ⚠ **no arbitration / class-action / jury-trial waiver — left for the attorney**;
> publishing scaffold under `publishing/`. ⚠ **TWO OPEN FLAGS:** `PRIVACY_POLICY.md:87-89` carries **one
> false sentence on a LIVE page** (it describes an Ad button that is now **absent from the UI**; the core
> no-ads claim is TRUE) — **do not edit it, live legal copy is the owner's/attorney's call**; and
> `docs/PUBLISHING_STEPS.md` Rail 1 is **obsolete** (bannered) — `dapp-store-cli@1.0.0` has no
> init/create/validate/publish, the app must already exist in the portal with an App NFT, and publisher +
> app are created **in the web portal with a browser wallet**.
>
> **⚠ `tools/webbot/` WAS DELETED OUTSIDE GIT** — all four files present at HEAD, **no commit ever deleted
> them**, not gitignored, absent on disk. That is the Playwright web-build self-test rig (the eyes on the
> deployed build). `git checkout -- tools/webbot/` restores it — **NOT run; open decision for the owner.**
>
> **⚠ F8 — ONE UNACKNOWLEDGED capture, seq 2248** (08-08 13:17:10, `Main_Castle_Overworld`):
> `Cannot set the parent of the GameObject '[VFX_Harvest_Wood]' while activating or deactivating the parent
> GameObject 'Lumbermill'.` This is the **WO-929** class, but **every proving line in WO-929 is
> `OutpostEnemy (...)`, a POOLED ENEMY** — so a fix scoped to the pooled-enemy path is **incomplete**.
>
> **★ FOUR CANON CLAIMS REFUTED AT SOURCE 2026-08-09 — CLOSED, stop carrying them** (anchor §9):
> **"THE SEAM" IS CLOSED** — WO-853 dual-implemented `IDamageable`+`IDamageableStructure` on
> `WallSegment.cs:53`/`Gate.cs:67`/`DefenseTower.cs:57`/`RaidSpire.cs:61` and widened the troop mask on
> both entry points (`TroopController.cs:189`, `:201-202`, `:394`, buffer 48 → 128 at `:104`); ⚠ **the
> raid-roadmap prerequisite is SATISFIED, so WO-774.0's posture ruling is NO LONGER FREE TO DEFER** ·
> the **"orphan third copy"** of the gear catalogs is GONE (`Assets/Data/Canonical` does not exist,
> deleted in `c55a5561`; `LocalJsonCatalogSource.Read` probes only Resources then StreamingAssets) ·
> **`CatalogBootstrap.RegisterFallback` drift is FIXED** — all three rows field-equal, the pure-white
> `visualTexturePath` defect CLOSED, guarded by `[fallback-parity]` · **dual-copy is HEALTHY** — 80 files
> per side, 77 paired, **only `weapons.json`+`armor.json` drift and both are the deliberate owner gear
> ruling**; the 08-08 `dg_sunken_vault.json` drift is FIXED (v1 / 14 rooms both sides).
>
> **⚠ AND FIVE NEW GAPS, all OPEN and UNCOVERED** (anchor §10): **three of the five difficulty multipliers
> are computed with ZERO gameplay consumers** — `EnemyCountMultiplier`/`BossHpMultiplier`/
> `BossDamageMultiplier` (`Core/Difficulty/DynamicDifficulty.cs:119,122,125`), so **every boss wave ignores
> the softer boss curve**; *(canon's "adaptive difficulty is INERT" is HALF-FALSE — all six
> `EncounterSample` fields ARE recorded, `WaveManager.cs:2471-2484`/`:2341`/`:1761-1762`/`:1876-1877`.
> ⚠ folder is `Core/Difficulty/`, namespace is `DeNelle.Core.Adaptive` — both right, fix neither)* ·
> **`DataWebRegression` iterates the StreamingAssets root only** (`:208`, `:356`), so a **Resources-only
> file — the copy that WINS at runtime — is never drift- or version-checked** (`widget-params.json` has no
> `version` field at all) · the version check **never asserts that a change bumps it** — 24 catalogs
> changed content with no bump on their latest commit · **`RoomForgeRegression.cs:162` is a hardcoded
> 3-file dual-copy list with NO `dg_*` layout** in it, including the one that actually drifted ·
> **`DungeonBaker` probes ONE path and is log-only** — `SaveScene` runs unconditionally after a
> `PathPartial` (`:432-445`, `:457-479`, `:490-494`).
>
> **⚠ WO-930 did NOT delete `StairUp`/`StairDown`/`IsVertical`/`SEALED_VERTICAL`/floor holes/ceiling
> shafts — BY DESIGN.** They are a **quarantined, gated CONTROL GROUP**
> (`DungeonMultiLevelRegression.cs:41-63`, "⚠ DO NOT DELETE"). **`dg_stair_rig` and `dg_descent_probe` are
> TEST FIXTURES, not regressions or stale content.** Converted layouts, verified: `dg_bonecrypt`,
> `dg_ember_deep`, `dg_sunken_vault`, `dg_stairwell_probe`.
>
> **CARRIED FORWARD, still open** (the 08-08 anchor dropped these): the VFX **ONESHOT pool saturates
> 40/40** — different pool, different reclaim path, **NOT closed** by the 08-06 loop-cap fix · the
> **absence** of `SKIPPED - active loops 20/20` across a full wave has **never been proven** (owed a fleet
> run) · **`VFXType` serialises by ORDINAL — appends only**, and `Build()` does
> `entries.arraySize = rows.Count` so a builder-only row is silently dropped · **✅ WO-910 RESOLVED
> 2026-08-16** (trees re-authored to 3 bases branching wider — knight 3/7/8/7/7, ranger 3/5/6/6, mage
> 3/6/6/5; ranger and mage had had **no authored x/y at all**, so the old "31 dead nodes" line described
> a missing layout) · **hero select SELF-SKIPS** when the
> save records a class — use New Game / Play Intro, never Continue · **`api/` is PREVIEW-only** and prod's
> nonce endpoint has **no CORS** · still colour-only and OPEN: the build placement ghost + the hero health
> bar · height cadence 1.25/1.2/1.0/0.75/0.35 with **walls DELIBERATELY excluded** (a uniform fit narrows a
> wall and **opens PATHABLE GAPS** in saved runs; `collector_farm` 1.4 is a COMPENSATION, not an outlier) ·
> the bar is **6 visible faces** with `Upgrade` re-pointed to Manage/Queues, `ButtonCount` stays **7**,
> `MaxVisibleFaces` is what went 7 → 6, and **`Map` stays dormant at ordinal 4 — never renumber it.**
>
> **The 08-06 thread below is SUPERSEDED.**
>
> ## ▶ LIVE THREAD (2026-08-06) — SUPERSEDED (see 08-09 above)
> **Reality anchor = `CANON_GROUND_TRUTH_2026-08-06.md`** (supersedes 08-05, which is bannered; note the
> 08-05 anchor itself was never threaded here, so this line jumps two days). Branch
> `wip/village2-and-f8-tickets`, **HEAD `1534dffb`, local is 43 commits AHEAD of origin — NOT PUSHED.**
> ⚠ Working tree NOT clean, **and it is a SHARED tree (CLAUDE.md §11)**: `ProjectSettings.asset` carries a
> newer APK stamp (`312459` vs the committed `312348`); `WorkOrders/WORK_ORDER_885`–`894` are untracked;
> and **a concurrent implementation lane of ~32 modified `.cs` files + the dual-copy
> `structures-catalog.json` / `damage-states.json`** was in the tree **as measured 2026-08-05**
> (consistent with WO-889–893 in flight *at that date*). ⚠ This is a dated snapshot of a working tree,
> not a standing fact — **run `git status` for the current tree**; do not act on it.
> **One committer, staged by explicit path, never `git add -A`.**
> Gates last emitted: `COMPILE_GATE_OK` + **`REGRESSION_OK 120/120 suites`** + `VFX_LOOPFLAG_OK` +
> `VFX_ART_MIRROR_OK` + `PARTICLE_PACK_VFX_BUILD_OK` + `BOSS_FIREBREATH_BUILD_OK`. Save **v36** unchanged.
> **Never restate a suite count from a doc — read it off the marker.** It moved 117 → 118 → 119 → 120 in
> eight hours, and the three entry points now emit DISTINCT markers (`REGRESSION_OK` /
> `CHECKIN_SUITE_OK` / `SESSION_GUARDS_OK`).
>
> **THE PATTERN OF THE NIGHT — carry this forward:** *a flag authored BY HAND instead of DERIVED from the
> thing it describes.* `IsLoop` (53 of 122 picks wrong) · the "self-contained" tracked VFX prefab
> (`CopyAsset` copies the prefab only — **183 pack references**) · `HeroTalentNodeDef.Hidden` (zero runtime
> readers, its own comment lied) · `TalentStrategyRegression.HiddenTrees` (40 nodes never audited) · the UI
> capture harness resolution (a **label, not a layout**) · `CatalogBootstrap.RegisterFallback` (all three
> rows drifted). **Derive it, and PIN the owner's standing rulings above the derivation with their reason.**
>
> **⚠ VFX LOOP-CAP P0 — FIXED, six captured proving sessions.** `IsLoop` was a sticky checkbox force-set
> true for role Projectile/Aura; a loop played fire-and-forget **permanently consumes one of the 20 global
> slots**. The archer and ballista fire `PP_MuzzleFlash` and discard the handle, so after ~20 shots a tower
> renders no projectile AND starves the Tree of Life aura and every POI marker. `break-log` shows
> `SKIPPED - active loops 20/20` naming five victims that were themselves the mis-flagged culprits.
> ⚠ **Not yet proven: the ABSENCE of the cap message across a full wave — that needs a fleet run.**
> ⚠ **A SECOND signature, NOT closed by this fix: the ONESHOT pool saturates 40/40** in three captures.
>
> **⚠ RANGER + MAGE ARE UNLOCKED** (`ff.knightonly` defaults OFF; roster Knight/Ranger/Mage via
> `PlayableHeroes`; **Cleric deliberately out** — no authored kit). **✅ WO-910 IS RESOLVED (2026-08-16)
> — the "trees are empty / READY FOR OWNER RULING" framing is HISTORY.** All three trees were
> re-authored to **3 bases branching wider**: knight **3/7/8/7/7** (32 nodes) · ranger **3/5/6/6** (20)
> · mage **3/6/6/5** (20), verified in `hero-talents.json`. ⚠ Ranger and mage previously had **no
> authored x/y at all**, so the "31 dead nodes" figure described a missing layout, not a design
> deficit. Also fixed: **one focus plate per BOARD** (was one per track — the view read
> `HeroSkillTreeVM`'s per-TRACK `nextTaken` as board-level). ⚠ **Hero select SELF-SKIPS when the save already
> records a class** (`HeroSelectController.OnEnable` → `SceneRouter.GoCastle()`), so **testing a class
> change requires New Game / Play Intro**, not Continue.
>
> **⚠ ACCESSIBILITY:** the low-health tell is **no longer a red vignette**. It reads by **pulse rate
> (0.85 → 3.2 Hz)**, **guttering depth** (trough to a tenth of density) and a **recipe swap to a candle
> gutter below a quarter health** — shape and timing, never hue. The vignette stays only as a redundant cue.
> Still colour-only and OPEN: the build placement ghost (valid/invalid on the red/green axis) and the hero
> health bar.
>
> **⚠ HEIGHT CADENCE (owner ruling):** 1.25 landmark / **1.2 towers** / 1.0 building base / 0.75 siege /
> 0.35 decoration, recorded in the data as `_heightCadence`, **catalog v8** (6→7 archer, 7→8 cadence). **WALLS DELIBERATELY EXCLUDED** —
> the fit is uniform, so narrowing a wall **opens PATHABLE GAPS in saved wall runs**. `collector_farm` at
> 1.4 is a **compensation, not an outlier** (windmill blades inflate the Y bounds) — do not "fix" it.
>
> **⚠ THE UI CAPTURE HARNESS WAS GEOMETRY-BLIND until `7e05e6d3`** — the resolution in a PNG filename was a
> LABEL, not a layout. **2670x1200, the Seeker's real surface, had never been rendered in this repo.**
> Several of tonight's UI commits are explicitly **not** geometry-verified and need a device check.
> ✖ **`ClampMinTouch` was CHECKED AND RULED OUT** at three sites tonight (bands resolved 117 / 116.7-130.6 /
> exactly 112.0 px). It is a real class, but check the band arithmetic before naming it.
>
> **Full detail + the refuted-beliefs ledger:** `docs/reference/SESSION_INDEX_2026-08-06.md`
> (and `docs/reference/DEFECT_INDEX_2026-08-05.md` for the earlier half of the same day, frozen).
> **The 08-03 thread below is SUPERSEDED.**
>
> ## ▶ LIVE THREAD (2026-08-03) — SUPERSEDED (see 08-06 above)
> **Reality anchor = `CANON_GROUND_TRUTH_2026-08-03.md`** (supersedes 08-02, bannered). Branch
> `wip/village2-and-f8-tickets`, **HEAD `56be3ae2`, local==origin, PUSHED**. **Working tree CLEAN.**
> Gates `COMPILE_GATE_OK` + **`REGRESSION_OK 104/104 suites`** + **`TESTS_OK 912/912`, zero reds** +
> **`UI_CAPTURE_OK 28`**. Save **v36**. WO blocks unchanged (main **853** / UI-seat **863**).
>
> **The overnight wave (15 commits) is the thing to know:** enemies actually reach you now; raids went from
> ~2.4% of the floor to 20/49/60% with a **spire objective** instead of a corpse count (raid walls had no
> colliders and no raid scene had a hero spawn point); raid troops animate and aren't magenta; the tutorial's
> Hollow step can be completed; **the check-in gate had never run at all** — it did not parse under PS 5.1.
>
> **⚠ SERVER — verified live 2026-08-03, corrects 08-02:** **`auth_nonces` EXISTS** and returns HTTP 200 on
> production (the 08-02 "table does not exist" line is dead). But **`api/` is deployed to PREVIEW only** and
> the game hardcodes the prod domain, so none of the overnight server work is reachable — and **prod's nonce
> endpoint has no CORS**, so a browser blocks the WebGL wallet rail regardless. `player_data` = 2 test rows
> from May; `bug_reports` = 0. **Promoting `api/` to prod is the single highest-value action on the board**
> and is the owner's call.
>
> **⚠ THE SEAM:** nothing in the game can damage a wall, gate or enemy tower — `WallSegment.cs:28` and
> `Gate.cs:45` implement `IDamageableStructure`, `TroopController.cs:449-459` sweeps for `IDamageable`, and
> the two are disjoint. Prerequisite under both raid roadmaps; makes the WO-774.0 posture ruling deferrable.
>
> **⚠ CANON HEALTH:** `docs/MASTER_CATALOG.md` (the INDEX) was NOT refreshed by WO-836 — only the 19 area
> files were; treat the index as a filename list. The area files are code-true as of `b77a178e`, not HEAD.
> `docs/reference/REGRESSION_COVERAGE_MATRIX.md` is two Sundays stale — never quote its counts.
> **The 08-02 thread below is SUPERSEDED.**
>
> ## ▶ LIVE THREAD (2026-08-02) — SUPERSEDED (see 08-03 above)
> **Reality anchor = `CANON_GROUND_TRUTH_2026-08-02.md`** (supersedes 08-01, bannered). Branch
> `wip/village2-and-f8-tickets`, **HEAD `e60b19e5`, local==origin, PUSHED** (21 commits dated 08-02).
> ⚠ working tree NOT clean — an in-flight item-identity lane is uncommitted.
> Gates `COMPILE_GATE_OK` + `REGRESSION_OK` + **EditMode 884/884, zero reds** + **`UI_CAPTURE_OK 28`**.
> **Save schema = `v36`** (`everBuiltStructureIds`; Echo lane tokens read-migrated to `<resource>:<level>`).
>
> **WO numbering — NEVER copy a number from any doc; read the `CLI_LANES_WO_NUMBERS.md` banner.**
> **TWO DISJOINT BLOCKS are in use** (the fix for 5 two-seat collisions on 08-02 alone):
> **main line (CLI) next free 853** · **reserved 860–899 (UI seat) next free 863**. Each seat bumps ITS
> OWN banner row in the SAME edit as the mint — that is the rule that keeps getting broken.
>
> **Shipped 08-02:** WO-830/831 **Echo harvest program** (affinity is a **MATCH BONUS, never a lock** —
> the player picks each Echo's resource, a match doubles yield; **Maren harvests Crystals**) · WO-835
> action bar (holes impossible by construction) · WO-839 raid deploy · WO-840 armorer · WO-841 countdown ·
> WO-842/843/844 felt fixes (single Wood/Iron authority; singleton rebuild; potions really apply) ·
> WO-797/849 dungeon room-ownership + pursuit bound + exit beacon · WO-850 deepest-room treasure ·
> WO-766 **real Solana wallet + the tester program** · **WO-836 MASTER_CATALOG all 19 areas rewritten
> from code** · WO-852 Echo card bands · WO-860 starter loadout = **sword+shield** · WO-861 Phase 0 ·
> tower research ladder restored · shields actually defend · respawn now MOVES you.
> **Ten new oracles** (incl. dungeon-treasure, echo-card-layout, starter-loadout, shield-defense).
>
> **⚠ APK precondition:** the Solana SDK is a **git-URL** package (re-resolves into `Library/PackageCache`)
> — run `tools/android/patch-solana-sdk.ps1` before ANY APK build. Android stripping is at **Low**;
> **WO-848 open** to restore Medium.
>
> **`WaveDataTest` has NO open ruling** — the owner closed it 07-30 (smart composition); both tests now
> assert EMPTY batches and a re-add FAILS. Any doc calling it "an open owner ruling" is stale.
> **The 08-01 thread below is SUPERSEDED.**
>
> ## ▶ LIVE THREAD (2026-08-01) — SUPERSEDED (see 08-02 above)
> **Reality anchor = `CANON_GROUND_TRUTH_2026-08-01.md`** (supersedes 07-26, bannered). HEAD `ac0a52e3`+,
> local==origin, PUSHED. Gates `COMPILE_GATE_OK` + `REGRESSION_OK` (103 checks) + `UI_CAPTURE_OK 23`.
> Save **v35**. Shipped today: **WO-818 all phases** (12 KayKit NPC bodies + `repo.npcModel` catalog v6 +
> KayKit-first injectors + NPC_MODELS oracle), **WO-826 Realm Map** (parchment panel + HUD Map button +
> REALM_MAP oracle; travel stubbed to 827), **owner ruling: bar Queues button RETIRED** (right-column
> Builders chip = the one Queues entry, 6-face bar, oracle-enforced), **ProjectSettings batching RCA
> CLOSED** (DesktopBuild post-build re-assert), dungeon log test all-7-proving-lines green. Release
> train: desktop exe + Seeker APK (installed + Firebase testers) + WebGL→Vercel preview. **WO next-free:
> read the `CLI_LANES_WO_NUMBERS.md` banner — NEVER trust a copied number here** (by 2026-08-02 midday it
> had already moved 832→838: 832 one-true-button · 833 KayKit idle · 834 blank-town save v36 · 835
> action-bar repack · 836 catalog SME fleet · 837 stockpile capacity caps). Save is **v36** as of
> 2026-08-02 (WO-834). Queue: 822 → 817 ph1-2 → 821 → 827/828/829 (+830/831 Echo affinity program,
> owner-sequenced; 837 stockpiles). **The 07-26 thread below is SUPERSEDED.**
>
> ## ▶ LIVE THREAD (2026-07-26) — READ BEFORE WORKING — SUPERSEDED (see 08-01 above)
> **Current reality anchor = `CANON_GROUND_TRUTH_2026-07-26.md`** (supersedes 07-22). A large **dungeon+raid
> felt-test wave** landed on `wip/village2-and-f8-tickets` and **is PUSHED** (HEAD `7dec0e07`, local==origin —
> a change from 07-22's push-HELD). **Dungeons are now a functional end-to-end loop** (enter → explore → read
> lore → fight with a REAL win/loss → settle → leave → Village): WO-770.1/.2/.3/.3b/.4/.7/.9 shipped, plus
> DungeonHero sole-mover / taller camera / Bryn pill-hide. **The raid loop is LOCKED to Teleport/Deploy** (COC
> model, owner 2026-07-26); walk-to retired as the raid loop. **WO-770 (dungeon), 771 (raid v2), 772 (shared
> enemy system), 773 (Obsidian job queue)** are firmed + validation-signed-off (`docs/qa/`), but only 770 is
> partly built — 770.5/.6/.8/.10/.11 + all of 771 + 773 are BACKLOG *(superseded: 773 SHIPPED v35; raid
> V1 spine EXISTS end-to-end — WO-774 is UX polish)*; **772 Phase 1 UNBLOCKED** (Hollow Ones
> APPROVED / Wildlands DEFERRED — `docs/PAIN_POINTS_2026-07-26.md`). Non-dungeon felt fixes shipped: enemies-out-of-castle + battle-lock, towers-no-longer-through-
> walls, MagentaGuard Android, loading overlay+bar, gate-traversal-teleport off, collector vendor NPCs, Alchemy
> scroll-fix. WO next-free = **832** — mint ONLY from the `CLI_LANES_WO_NUMBERS.md` banner. Ticket table: `docs/qa/SUNDAY_STATUS_2026-07-26.md`.
> **Save schema = v35** — code-verified (`SaveSchema.CurrentVersion = 35`): **WO-773's Obsidian
> multi-channel work queue (`obsidianQueue`) HAS shipped** (the v34→v35 migrator folds legacy timed state
> into the Builder channel). Treat WO-773 as landed, not backlog — the "773 BACKLOG" line above reflects the
> Sunday doc-pass, before the queue landed. **The 07-22 thread below is SUPERSEDED** (its §5/§6/§7
> module digests remain the deep reference).
>
> ## ▶ LIVE THREAD (2026-07-22) — SUPERSEDED (see 07-26 above; deep module state still valid)
> **Reality anchor = `CANON_GROUND_TRUTH_2026-07-22.md`** (supersedes 07-19). A **17-agent read-only
> SME fan-out** (12 module + 5 high-level, verified from code) produced that anchor: the code is HEALTHY and
> gates are GREEN (`COMPILE_GATE_OK` + `REGRESSION_OK`, 16 P1 suites, 0 reds, save v34, HEAD `148ab637`,
> local==origin) — **the debt is DOCUMENTATION DRIFT.** The `MASTER_CATALOG/<area>` sections (dated 2026-06-12
> on the stale `feat/tower-core-loop` label) have drifted weeks behind: see the 07-22 anchor's **§6
> catalog-drift ledger** + **§7 comment-vs-code lies registry** (e.g. `ff.atbdungeon` doesn't exist — real
> gate is `ff.dungeonrealtime`; home hub is `Main_Castle_Overworld` not `MainCastle_Hall`; save v34 not v33;
> 23 build scenes not 13; audio 5-group mixer never built). **Branch hygiene done 07-22:** 2 stale agent
> worktrees + 4 stale branches (2 local, 2 remote: `feat/tower-core-loop`, `samantha-village-progress-2025`)
> purged; remotes now `master` + `wip` only. WO next-free = **754**. Push still HELD. **The 07-19 threads
> below are SUPERSEDED.**
>
> ## ▶ LIVE THREAD (2026-07-19 EVENING) — READ BEFORE WORKING
> **Current reality anchor = `CANON_GROUND_TRUTH_2026-07-19.md` (still current).** On top of the 07-19
> morning arc below: a **FELT-TEST FIX WAVE** (CLI committing) — pet-screen sort-order, HUD de-overlap,
> **WO-751 Y-height normalization** (default 4m / tower 7m / siege 3m + audit tool), Echo modal single-
> arbiter via `PanelManager`, upgrade-panel visuals (event-driven rebuild, text-fit, hotkeys removed),
> flag-screenshot save-on-release; **in-flight:** upgrade no-op blocker, white-ballista/magenta-weapon
> materials, **WO-753 Destructible** (no-rebuild + full-cost + VFX cleanup). **New WOs:** 750 (Right
> ActionBar naming + Warden's Grace redesign, SPEC), 751 (Y-normalization, DONE), 752 (Echo founding-card
> overhaul + post-tutorial interjection, SPEC + creative sign-off), 753 (Destructible, IN PROGRESS).
> **New rulings:** Right ActionBar = Attack + Q/W/E/R named skills (Sword Wielding/Sword Heroic/Shield
> Charge/Warden's Grace/Radiant Strike), mobile HUD shows NO key-letters; all items normalized by
> Y-height; Echo = essence of a person the tree guards (Aldwin/Elowen/Corvin/Bran/Doran/Maren); destroyed
> items never rebuild (full-cost + VFX cleanup); headless UI-screenshot pass runs before builds.
> **WO next-free = 754** (750-753 consumed). **The morning 07-19 line below is still valid history.**
>
> ## ▶ LIVE THREAD (2026-07-19) — READ BEFORE WORKING
> **Current reality anchor = `CANON_GROUND_TRUTH_2026-07-19.md` (read it first).** Since 07-18: HEAD
> `98ff1135`, **local ahead of origin by 7, PUSH HELD**. **DataRegression is `REGRESSION_OK` — ZERO reds**
> (all 5 long-standing FAIL-BY-DESIGN reds fixed 07-19 per the owner's plan: arena texture, dual-wallet,
> pet-slot persist, Tribes/Wards/Arena persist, orc-raider SSOT). **Save v34** (persist Tribes/Wards/Arena +
> pet active-slot). **WO-748 (Default Town) + WO-749 (dungeon ingredients) DONE + RESULT-filed.** Corrupt
> `d4_sunken_crypt` scene PURGED + stale branch junked. New: `SUNDAY_HOUSEKEEPING.md` weekly ritual +
> known-dictionaries; Notion setup kit staged (`docs/notion/`, awaiting owner `/mcp`). WO next-free = **750**.
> **The 07-18 thread below is SUPERSEDED.**
>
> ## ▶ LIVE THREAD (2026-07-18) — READ BEFORE WORKING
> **Current reality anchor = `CANON_GROUND_TRUTH_2026-07-18.md` (read it first).** Since 07-13:
> **Pi Hackathon WON** (the "July-31 deadline / build mode IS the demo" framing is RETIRED); the
> **whole-game MVVM migration is DONE** (WO-744 — every panel View binds an `IPanelViewModel`, the
> `[ui-mvvm]` conformance oracle is armed HARD-FAIL); **Room Forge merged to mainline** (WO-740–745,
> green); **save v33**; `wip` pushed to origin. Two-session shared-tree hazard is live (dungeon
> session should use its own worktree). WO banner next-free = **750**. **The 07-12 thread below is
> SUPERSEDED — do not act on its "demo readiness" framing.**
>
> ## ▶ LIVE THREAD (2026-07-12 evening) — SUPERSEDED (see the 07-18 thread above)
> Current focus = **MOBILE-WEB DEMO READINESS** (Pi hackathon **July 31**; **build mode IS the demo**
> per the player-defined-map pivot, 07-11). Tonight's arc: **WO-677/678/682/683 committed local**
> (`66b3272f`, `c963a553`, `33799026`, `965309a6`, `683b917b`); gates = `COMPILE_GATE_OK` +
> DataRegression at the 3-known-pre-exister baseline; new **SFX_WEBGL_OK oracle** swept 13 broken
> clip metas (db-proven `Loading FSB failed` SwordSwing root). **WebTrace web-debug loop PROVEN**
> end-to-end: `?trace=1` → `POST /api/trace` → Neon `analytics_events`; the CLI read path = the
> `[sig]` echo in Vercel runtime logs (`DATABASE_URL` is sensitive/unpullable). New **WebGL ship
> preview deploying tonight** (non-dev build — the giant-error-overlay class dies); prior preview
> `mexharnff` is superseded when the new URL lands; **prod UNTOUCHED**; **push HELD**. Live anchor =
> **`CANON_GROUND_TRUTH_2026-07-13.md`** (07-12 + 07-08 anchors bannered SUPERSEDED). Notes: `api/`
> lives **IN-REPO (gitignored)** — the "separate React repo" line is dead; save schema **v30**;
> **`SAMANTHA.md` + the new `START_HERE.md` are the boot gate**; WO numbering **next-free = 684**
> (677/678 collisions flagged). **BINDING: read-before-assert applies to EVERYTHING (code + non-code).**

**READ THIS FIRST on any new session (owner directive 2026-06-20).** Every CLI/agent
loads this before doing anything, to stay an SME. It is the fast-path summary; the
binding depth lives in `CLAUDE.md`, `docs/ARCHITECTURE_PRINCIPLES.md`, `docs/HANDOVER.md`,
`docs/MASTER_CATALOG.md`, and `docs/INSTRUMENTATION_STANDARD.md`.

> ## 🟥 DAY-1 BOOT — the owner should NEVER have to remind you of this
> The #1 recurring waste (owner, EVERY day, 2026-06-23): she has to re-teach a fresh CLI to read docs / canons /
> absorb memories / orchestrate / stop guessing — even though it's all written here and in memory. **Reading this
> is not doing it.** So turn ONE, unprompted, BEFORE your first task reply:
> 1. **Read + be SME:** this file + `docs/MASTER_CATALOG.md` (relevant area) + the `docs/*ARCHITECTURE*` for what you'll touch. Reuse built systems; never reinvent.
> 2. **Boot posture = VERIFY + DELEGATE + INSTRUMENT-FIRST:** delegate deep work to agents (your hands = gates + commits); on ANY non-trivial bug, READ the captured data (F8 break-log / Editor.log / FlowTrace) and cite the line BEFORE any edit. Never guess / inference-fix.
> 3. **Hold the line** (pleasing ≠ right): park off-focus shiny things into a WO; bank wins before building more.
> 4. **Never say "I'll mark it" — write the memory AND the doc in the moment** (persist in both places).
> If you catch yourself about to guess, solo-dig, or please-and-slide → STOP and do the above. The reminder being needed AT ALL is the failure to eliminate.

## Core Rules (always follow)
- **One Model:** Capability is a property on the entry. Never hard-code per type/tag.
- **Presentation never touches objects** — HUD → Core only, Village → Core only.
- **MVVM strict:** the VM holds all logic/state; the View is a dumb skin (no game-state reads).
- **Flag-gated changes only** (BlinkChrome, BuildingUpgradePanel, etc.).
- **Instrument, don't guess — THE HARD GATE (BINDING, CLAUDE.md §12):** NO code edit on a real bug
  until CAPTURED DATA proves the cause. Loggers step IN/OUT → run HEADLESS → data pinpoints → fix THAT.
  Static reading locates candidates, never concludes. Never inference-fix; it's the OPENING move, unprompted.
- **One thing at a time, fully verified before the next.**
- **Deliver complete + felt-verified. No piecemeal.**
- **Ticket pipeline (BINDING):** QA (read-only RCA, classify NEW-feature vs EXISTING) → CLI
  (implement + headless-verify) → PO (felt-verify + close). Shared board = the Task list; log
  every hand-off. Full spec: `docs/TICKET_PIPELINE.md`.

## Current State (anchored to `CANON_GROUND_TRUTH_2026-08-09.md`; the bullets below carry earlier detail — where they disagree with the LIVE THREAD above, the thread wins)

> **Fast reconciliation (corrected 2026-08-09 — trust these over the older bullets):** home hub =
> `Main_Castle_Overworld` (MergedWorld ON, one navmesh; `Village.unity` and `OuterWorld.unity` are
> DELETED — the "MainCastle_Hall + OuterWorld streams additively" bullet below is stale). Save schema =
> **v37** (`SaveSchema.cs:36`; v35 = WO-773 Obsidian queue, v36 = WO-834 `everBuiltStructureIds`,
> **v37 = WO-911 the per-job PAID BASKET** — cancel refunds 100% of what was paid, flat; a pre-v37 job
> refunds ZERO and says so). **HEAD = `c8320434`, PUSHED, local == origin** (the `e60b19e5` bullet below
> is stale). Dungeon real-time gate is `ff.dungeonrealtime` (there is no `ff.atbdungeon`). Raid loop =
> COC Teleport/Deploy (WO-771). Dungeons are a functional end-to-end loop — and as of 2026-08-08 the
> **multi-level stairs work**: all 4 content dungeons bake `PathComplete` (WO-930).
- **Strategic placement = ALWAYS ON (2026-07-13, WO-695 ex-682):** `ff.strategicplacement` is REMOVED —
  Build → Town/Defenses/Walls tabs, movable functional storefronts and the 260w/210i core-kit seed are
  the unconditional path; New Game = the BLANK template (+ one FTUE grace-default Forge record);
  existing saves migrate once via the v30 one-shot writer.
- **Branch:** `wip/village2-and-f8-tickets` (NOT `feat/tower-core-loop` — stale). **HEAD = `e60b19e5` (the 2026-08-02 marathon-day-2 arc: Echo program + tester wallet + dungeon/gear evening), pushed, local==origin.** Save schema **v36** *(FROZEN HISTORY — true on 2026-08-02 only; the live schema is
  **v38**, read `SaveSchema.CurrentVersion` at `SaveSchema.cs:41`)*. Fresh headless gates = `COMPILE_GATE_OK` + DataRegression **`REGRESSION_OK` — 0 reds** (all 5 long-standing baseline reds fixed 2026-07-19: arena texture, dual-wallet, pet-slot persist, Tribes/Wards/Arena persist, orc-raider SSOT) + the `[ui-mvvm]`/`[room-forge]` ratchets at 0 NEW.
- **Pi Hackathon WON (2026-07-17)** — the "July-31 deadline / build mode IS the demo" framing is **RETIRED**; there is NO upcoming demo and the roadmap is OPEN. The quality bar (feel-arc/F8, ten-year-old test) still governs. **Prod untouched** (promotion stays the owner's separate call at `defenders-of-the-realm-v2.vercel.app`). **Highest-leverage open lane = the CoC offense loop (WO-724→726, Path A convergence)** now the MVVM + Room Forge foundations have landed; WO-739 generic upgrade panel is the parallel-safe start.
- **Title:** **"Echoes of Elarion"** (chapter) within the **"Defenders of the Realm"** series; tagline **"Echoes of a Forgotten Civilization"** (owner 2026-07-24; "Hold the last light" retired).
- **Combat space:** WO-584 consolidation (READY) — one warp-in space primitive, 3 skins (dungeon/outpost/arena), ownership flip; replaces flat ATB dungeon.
- **Game:** Echoes of Elarion / Defenders of the Realm (Unity 6 / URP). **V1 = ONE controllable hero
  (Knight "Grom") in an overworld with isolated real-time BattleArena combat.** Base-defense/tower-defense
  is V2-gated behind `ff.basebuilding`. (itch web build LIVE; Solana→Pi/Cloudflare backend; Vercel LIVE — prod = the 07-16 six-fix build `q2v5vj86g`; fresh WebGL preview 2026-08-01.)
- **Hero Rig:** a **single Tripo self-rigged model**, static armor, **NO mesh-swap**. *Blink full-body rig
  is JUNKED (06-22)* — Blink survives only as a **UI re-skin kit** (`BlinkChrome` flag), not the hero body.
- **Combat:** animated real-time battle = the **OVERWORLD BattleArena** (lock-on WO-512, 9-zone HUD).
  **ATB is separate** (flat/static, single hero vs static enemies). Arena trio = OFF/gated.
- **Tech Tree:** BuildingUpgradeVM + PanelMvvm (Warcraft 3-style perks, tier gate at the Heart of Elarion);
  unlocked this arc by the wired village-tier upgrade (WO-432).
- **World:** home hub **`Main_Castle_Overworld`** (MergedWorld ON, one navmesh; `Village.unity` +
  `OuterWorld.unity` DELETED — `MainCastle_Hall.unity` exists on disk but is NOT the hub); `Village2` =
  raid target. Castle↔outer world is one merged scene; moat + drawbridges (`ff.castlemoat`); tree aura +
  tower glow (`ff.hubambientvfx`).
- **Economy:** Echo workforce wired (offline real-clock, WO-587 Population & Echo growth; save **v36**); gold on kills; research costs. **Echo harvest affinity is a MATCH BONUS, never a lock** (WO-830: the player picks each Echo's resource; matching its affinity doubles the yield; token grammar `<resource>:<level>`; Maren = Crystals).
## Key Files to Remember
- `CANON_GROUND_TRUTH_2026-08-09.md` (the single live anchor of current reality — read FIRST; a delta over 08-08 → 08-07 → 08-06 → 08-05 → 08-03 → 08-02 → 08-01 → 07-26 → the deep `CANON_GROUND_TRUTH_2026-07-22.md` module anchor. All earlier anchors are SUPERSEDED/frozen — and the **08-08 one is INVERTED** on its machine-blocked and dungeon-stair sections, so do not act on it)
- `docs/reference/WO_TRUE_STATUS_2026-08-08.md` (the WO audit that found **52 of ~91 statuses wrong**; frozen, dated)
- `docs/reference/SESSION_INDEX_2026-08-06.md` (tonight as a known dictionary: every defect with its proving line, every REFUTED belief with the evidence that killed it, the owner rulings, the open items) · `docs/reference/DEFECT_INDEX_2026-08-05.md` (the same for the earlier half of 08-05; frozen)
- `docs/qa/UI_REVIEW_2026-08-01.md` (frozen 20-panel real-pixel readability review) · `docs/GROK_MEMORY.md` (Grok fast path)
- `docs/qa/SUNDAY_STATUS_2026-07-26.md` (current WO/ticket status table) + `docs/qa/dungeon-raid-validation-2026-07-26.md` (dungeon/raid sign-off)
- `docs/COMBAT_PIVOT_NORTHSTAR.md` (single-Knight pivot — supersedes all "Blink/party-of-4" canon)
- `docs/ARCHITECTURE_PRINCIPLES.md` · `docs/ARCHITECTURE.md` (hub)
- `docs/TICKET_PIPELINE.md` (QA→CLI→PO ticket lifecycle, BINDING)
- `docs/PATH_TO_V1.md` · `V1_ASSEMBLY_MAP.md` · `ECHO_WORKFORCE_SPEC.md`
- `docs/UI_MVVM_BINDING_MAP.md` · `docs/UI_BLINK_TEMPLATE_CANON.md` (BINDING — master-frame UI formula) · `docs/BLINK_UI.md` (UI re-skin only)
- `WORK_ORDER_432` / `WORK_ORDER_433`

> **Maintenance (WO-520):** after any commit that changes architecture/state, update the relevant canon
> doc in the same breath, and keep `CANON_GROUND_TRUTH_<date>.md` current. See CLAUDE.md §15.

---
*Maintained by the owner. Keep it current; it is the at-a-glance SME primer pasted at the
start of every session.*
