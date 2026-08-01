# KEY FACTS — the living fact sheet (update IN PLACE, never snapshot)

> **Rule (owner directive 2026-07-12):** this file is LIVING — when a fact changes, edit the line
> in the same commit as the change and re-stamp its date. Facts here are code-verified, never
> assumed. If a doc contradicts a line here, the doc is stale. Dated anchors
> (`CANON_GROUND_TRUTH_*`) remain the session snapshots; THIS file is the always-current card.

## ⭐ NORTH STAR — the state we are building toward
- **The product:** "Echoes of Elarion" (chapter) in the "Defenders of the Realm" series — "Echoes of
  a Forgotten Civilization" (retired tagline "Hold the last light" noted in canon-strings.json).
  **V1 = ONE controllable Knight ("Grom")** in an overworld with isolated
  real-time BattleArena combat; **the player builds their own city** (player-defined map pivot
  07-11: Build → place/move/rotate functional structures). *(The "build mode IS the demo" framing is
  RETIRED — see the platform line.)*
- **The platform:** **mobile web in Pi Browser**. **Pi Hackathon: WON (owner, 2026-07-17)** — the
  July-31 deadline + "build mode IS the demo" framing is RETIRED/STALE: there is NO upcoming demo, the
  roadmap is OPEN for the next phase (owner sets the new north star). Any doc still leaning on the
  hackathon deadline is STALE. Desktop is the dev proxy, never the verdict.
- **The bar:** the **ten-year-old test** — "wow, this feels good" on a phone, or it isn't done.
  Feel-first; headless proves binding, only the owner's hands prove feel.
- **The player never sees a failure** — errors are captured loudly in the db, invisible on screen.
- **The economy direction:** V1 ships ZERO crypto; soft currency client-owned now, flips
  server-authoritative (auth scaffolding already built) when currency carries real value; SKR is a
  later, separate arc. Monetization = rewarded-ad income paths, never a wall.
- **The architecture:** HP B2B — bounded contexts, presentation never touches objects, the One
  Model (entries + capabilities), data-only content ("data only always"), pooled by default.
  Deep-dives: `docs/NORTH_STAR.md` (vision/GTM) · `docs/COMBAT_PIVOT_NORTHSTAR.md` (combat) ·
  `docs/ARCHITECTURE_NORTH_STAR.md` (does the foundation grow into the dream).
- **The operating dream:** the owner plays and rules; agents build in parallel lanes; every bug is
  a captured line; every system self-reports; the fleet + web bots verify before she ever has to.

## Latest (2026-08-01) — post-reboot ship wave: Realm Map + KayKit NPCs + Queues ruling + release train
- **Anchor = `CANON_GROUND_TRUTH_2026-08-01.md`** (supersedes 07-26, bannered). **HEAD `ac0a52e3`, pushed,
  local==origin.** Gates: `COMPILE_GATE_OK` + `REGRESSION_OK` + `UI_CAPTURE_OK 23` (pixels eyeballed).
- **WO-818 ALL PHASES SHIPPED:** 12 KayKit NPC bodies tracked at `Assets/Resources/NPCs/KayKit/`
  (`KAYKIT_STAGE_OK 12/12`, Humanoid); `structures-catalog.json` **v6** dual-copy carries `repo.npcModel`
  on exactly 12 owner rows; `KayKitNpcBody` resolver = KayKit-first → People chain → capsule (one Warn,
  never blank); `NPC_MODELS` oracle pins the 12 verbatim. Body swap = one-word owner JSON retag.
- **WO-826 Realm Map SHIPPED:** parchment panel (Elarion gilt home + 5 fog regions from dual-copy
  `realm-map.json`), strict MVVM, HUD **Map** button (hidden until Onboarded, WO-825 R4), DevPanel entry,
  `REALM_MAP` oracle + 8 EditMode tests. Travel stubbed → WO-827. 825 program IN FLIGHT (827/828/829 next).
- **OWNER RULING: bar Queues button RETIRED** — the right-column **Builders chip** (QueueStatus band,
  above the resources dock) is the ONE Queues entry; calm(town) bar = **6 faces**
  (Build/Talk/Bag/Raids/Map/Quests⇄Upgrade). `ObsidianQueueRegression` 7c enforces the retirement.
- **ProjectSettings dynamic-batching RCA CLOSED** (`ac0a52e3`): reverter proven (twice-captured) to run
  INSIDE `BuildPlayer` after the pre-build set; `DesktopBuild` now re-asserts static=0/dynamic=1
  post-build (the WebGL exceptionSupport-restore pattern). Owner keeps dynamic=1.
- **Dungeon verified from a captured run** (owner-ordered log test): all 7 proving lines + the R-A1
  arena CharacterController guard green. Open: vitals 120/60 placeholder (770.10), placeholder props
  (770.8), `EnvTreeFix VERIFY FAILED 'Skeleton_Mage_Hat'` (minor, unticketed).
- **Release train:** fresh desktop exe (15:17) · Seeker APK built + Firebase App Distribution (testers
  group) + adb install · WebGL→Vercel PREVIEW queued (promotion = owner). Screenshot archive:
  `Builds\ui-capture-archive\2026-08-01\` (23 PNGs).
- **UI seat reconciled:** WO-830 (Echo harvest affinity+synergy: 6 unique affinities
  Wood/Iron/Food/Gold/Crystals/Repairs, 3 disclosed pairs, 1 HIDDEN tri-synergy) + WO-831 (2D emergence
  sprite beat) minted; `docs/qa/UI_REVIEW_2026-08-01.md` (20-panel real-pixel review) banked.
  **WO next-free = 832** (banner is sole authority — never copy the number into docs, point at it).
- **Verified inventories (cite these):** regression gate = **103 checks (26 inline + 77 suites)**;
  FeatureFlags = 62 (⚠ XML summaries LIE on 12 defaults — trailing `//` comment is truth); save **v35**;
  EditMode reds live in `Assets/Data/Tests/WaveDataTest.cs` (wave-1 ruling open), not Tests/EditMode.
- **Queue ahead:** 822 → 817 ph1-2 → 821 → 827/828/829 (+830/831 owner-sequenced). Felt-verify list:
  Realm Map, 6-face bar + Builders chip, KayKit NPCs, 819/820/810/808/812/813, WO-825 R1-R4.

## Latest (2026-07-30) — 12-agent SME fan-out + check-in sweep + WO-783 fix wave
- **Operating model (owner directive):** CLI = **GATEKEEPER**. Dedicated agents write requirements, write
  the tests, and produce **read-only implementation proposals**; the CLI verifies every proposal **against the
  tree**, runs the tests, gates, commits by explicit path, and **screenshot-verifies before anything reaches
  the owner**. Agent output is a proposal, never truth — two claims were REFUTED on verification this session.
  Memory: `cli-gatekeeper-agent-role-model`. *(2026-07-30)*
- **Check-in sweep:** 9 commits, tree clean. Found + fixed **5 folders with tracked contents but an UNTRACKED
  folder `.meta`** (Core/Enemies, Core/Jobs, Village/Troops/Data, both dungeon-graphs) — a GUID-regeneration
  hazard on the second machine. `Assets/UnityTechnologies/` (191 MB Particle Pack) gitignored per big-pack
  policy + logged in `tools/art/REQUIRED_PACKS.md`. *(2026-07-30)*
- **WO-783 fix wave — IMPLEMENTED, all three gates green** (`COMPILE_GATE_OK` + `REGRESSION_OK` +
  `UI_CAPTURE_OK`, pixels opened not just markers):
  - **Raid VICTORY now settles the army.** `ReconcileAfterRaid` had ONE caller (retreat) and `AddVeterancy`
    had **ZERO repo-wide** — *winning a raid was free*. One latched `RaidDeployController.ReconcileRaidEnd(stars)`
    now serves both exits; 3-star clears pay veterancy.
  - **Healer's Cottage is REACHABLE again.** It lost its `AuthoredPortal` row when the east portal was
    rerouted to `dg_starter_loop`, so the richest dungeon in the game (lore/mini-boss/chests/crafting) was
    dev-overlay-only. Third row added, SOUTH `(20,0,-140)` yaw 352 — the only one of the three seats whose
    ground is provably flat (the WO-468 cave corridor pins Y=0). ⚠ navmesh seating still needs a runtime line.
  - **`[ui-obsidian]` ratchet ARMED** (`HardFailOnNew=true`, 0 NEW) + a **namespace-qualified regex blind spot**
    closed that had been hiding `OutpostHub.cs` as a false "resolved".
  - **waves.json authored schedule is DEAD and now says so** — see the standing-truth line below.
  - Echoes-button safe-area inset 16 -> 54 ref px (~7 dp -> ~24 dp on the Seeker).
- **FPV camera: owner RE-AFFIRMED default-ON** (2026-07-30). `ff.dungeonfpv` stays `defaultOn:true`; the
  `PAIN_POINTS` §4 "keep only if felt-tested" gate is CLOSED. Two `DungeonCameraRig` headers that called FPV
  "a STUB with no independent look" and named over-the-shoulder the default were corrected.
- **STANDING TRUTH — `waves.json` `enemies[]` batches are INERT.** `_smartComposition:1` in both live hubs
  means `WaveManager` GENERATES every wave's roster; only `countdownSeconds`, `boss`, `apexBoss` survive.
  **19 waves / 55 batches / 148 authored enemies are discarded every session.** Not a code regression (WO-362
  supersession was deliberate) — the *data* was authored 2026-07-11, ~4 weeks AFTER the batches went inert.
  A once-per-session `FlowTrace.Warn` now names it. **OPEN owner ruling (WO-783 D1):** which authority wins.
- **New WOs:** **783** (this wave) · **784** Echo lanes — canon's "3 of 4 stub" is wrong, **all four** are
  write-only, even Harvest bypasses the Core contract · **785** VFX survivability — **117 of 121** owner-tagged
  VFX rows point into gitignored packs with **no runtime fallback**. **782 RESERVED** (night-wrap capsule
  standee). **WO next-free = 786** *(superseded → banner, 832)*. *(2026-07-30)*

## Latest (2026-07-26) — dungeon+raid felt-test wave + Sunday housekeeping
- **Live anchor = `CANON_GROUND_TRUTH_2026-07-26.md`** (delta over 07-22, which stays the deep module
  reference). Branch `wip/village2-and-f8-tickets`, HEAD `7dec0e07`, **local==origin — this wave IS pushed**
  (change from 07-22 push-HELD). Prod untouched. Save still **v34** (no new persisted fields this wave).
- **Dungeons = functional end-to-end loop** (enter → explore → read lore → fight with REAL win/loss → settle
  → leave → Village). Shipped: WO-770.1 (exit + boss back-door), .2 (correct-dungeon return), .3 (real
  victory/defeat carrier via `SceneRouter.PendingBattle.LastOutcome` — a lost fight ends the run), .3b
  (real-time `BattleArena.OnBattleEnded` → shared `SettleEncounter`; fixes the never-released combat lock),
  .4 (readable lore stones + code-built modal), .7 (toast layer + live Bryn dialogue), .9 (stale-read
  `OnEnable` clear). Plus DungeonHero sole-mover + taller camera + Bryn pill-hide. *(2026-07-26)*
- **Raid loop LOCKED to Teleport/Deploy** (COC model, owner 2026-07-26); walk-to retired as the raid loop
  (its `EnemyOutpost`s may return as a light overworld patrol side-activity). When raid work starts, set
  `ff.overworldencounter=0` + `ff.raidwalk` OFF first. WO-771 v2 is the build plan; **nothing built yet.**
  Reuse `RaidBaseGenerator` + `EnemyFactory→Enemy→TargetManager` combat; `IDamageableStructure` must move
  Village→Core; tower-fire is greenfield. V1 = PvE generated bases (skip the deterministic sim). *(2026-07-26)*
- **Firmed WO set (`docs/qa/`):** 770 (dungeon), 771 (raid v2), 772 (shared enemy system — classes/families/
  armor/weapons + `EnemyResolver`, fixes generic-skeleton bug), 773 (common Obsidian job queue). Validation:
  `docs/qa/dungeon-raid-validation-2026-07-26.md`. **772 is BLOCKED on owner ratifying `docs/enemy-codex.md`**
  (review-and-approve gate) — it blocks 770.11 + 771.13. *(2026-07-26)*
- **Non-dungeon felt fixes shipped:** enemies-out-of-castle + battle-mode BattleLock (`e05f92f7`), towers no
  longer shoot through walls (Structure layer + LoS, `2cb3c40d`), MagentaGuard catches Android compile-failed
  shaders (`386a932f`), loading overlay + standard bar (`4edf8dcc`/`7dec0e07`), gate-traversal teleport off —
  walk through the arch (`8c35332f`), collector buildings get vendor NPCs (`804a02a2`, Lever 1 in progress),
  Alchemy recipe scroll-fix (`8ca95735`). *(2026-07-26)*
- **WO next-free = 774** *(superseded → banner, 832)* (761–773 consumed; 770–773 are decimal-sub-order specs in `docs/qa/`). Ticket table:
  `docs/qa/SUNDAY_STATUS_2026-07-26.md`. §6/§7 catalog-drift housekeeping WO + CS-1 ring/amulet non-persist
  ticket still open. *(2026-07-26)*

## Latest (2026-07-22) — SME fan-out + canon refresh + branch hygiene
- **Live anchor = `CANON_GROUND_TRUTH_2026-07-22.md`** (supersedes 07-19). A 17-agent read-only SME fan-out
  (code-verified) confirmed: **code healthy, gates green** (HEAD `148ab637`, local==origin, `REGRESSION_OK`
  16 suites/0 reds, save v34) — **the real debt is DOCUMENTATION DRIFT.** The `MASTER_CATALOG/<area>` sections
  are weeks stale; the 07-22 anchor carries the **§6 catalog-drift ledger** + **§7 comment-vs-code lies
  registry**. Key corrections: home hub = `Main_Castle_Overworld` (MergedWorld ON, one navmesh) not
  MainCastle_Hall; `ff.atbdungeon` doesn't exist (real gate `ff.dungeonrealtime`, dungeons route into
  BattleArena); 23 build scenes; ~70 catalogs; audio 5-group mixer never built (AudioSource-direct fallback only);
  HeroPortraits folder absent; deploy chain writes `CHAIN_DONE` on failure.
- **Branch hygiene:** 2 stale agent worktrees + local branches removed (dungeon work verified already-merged);
  2 stale remotes purged — `feat/tower-core-loop` (`cea673e4`) + `samantha-village-progress-2025-05-23`
  (`40a570a6`). Remotes now `master` + `wip` only.
- **Real bug surfaced (CS-1):** equipped ring/amulet (`equippedRingId`/`AmuletId`) declared + migrator-seeded
  (v26) but no GameState field / no Snapshot-Apply → **reset on reload.** Needs a ticket.
- **Still queued:** §6 catalog-drift + §7 comment-lie fixes as a housekeeping WO; CS-1 ticket. Push HELD.

## Latest (2026-07-20 overnight autonomous loop) — see `OVERNIGHT_RESULT_2026-07-20.md`
- **Regression baseline = REGRESSION_OK, ALL 16 SME P1 suites GREEN, ZERO reds** (2026-07-20). Added
  WAVES_SCHEMA (EW-3) + PACK_COSMETIC_INTEGRITY (ECON-1) + flipped DUNGEON_DRESSING green (real
  DungeonDresser prop pass). **All 5 audit P1s cleared + guarded.** Full green set: WAVE_SCALING /
  ENEMY_REWARDS / WALL_MITIGATION / UPGRADE_AUTHORITY / PACK_GRANT / SFX_RESOLVE / DUNGEON_EXIT /
  FOUNDING_REACH / FTUE_HONESTY / ECHO_CARD_COPY / SHADER_PIN / MODAL_REGISTRATION / CRYSTAL_PRODUCTION /
  WAVES_SCHEMA / PACK_COSMETIC_INTEGRITY / DUNGEON_DRESSING. *(pushed to wip, origin 1d7512b0)*
- **Composed dungeons now DRESSED:** `DungeonDresser.DressRoom` (Assets/Editor/RoomForge) seats ~8 real
  KayKit props (corner torches + floor barrels/crates) per composed room, wired into `DungeonBaker`
  pre-NavMesh (colliders stripped, doorway clearance). Broader dungeon VFX/lighting/battle dressing = next
  pillar follow, NOT built yet.
- **NEW TOOL — headless UI screenshot capture:** `DeNelle.Editor.UICaptureLaunch.RunCaptureHeadless`
  (edit-mode synchronous render; the old Play-mode path never worked under `-batchmode -quit`). Writes
  `Builds\ui-capture\*.png` + `UI_CAPTURE_OK`. **Run it before shipping any UI change** (owner rule: never
  be first to see a broken panel). It already caught 2 real Echo-card bugs pre-build tonight.
- **Newly data-driven (SSOT):** `buildings.json` crystalsPerWave (v2, CrystalMine yield), `enemies.json`
  xp/coinReward (v4), `walls.json` heartDamageMultiplier. All dual-copy identical + version-bumped.
- **Echo card = 6 NAMED SOULS** (Aldwin/Elowen/Corvin/Bran/Doran/Maren) in `EchoRosterCatalog` — each the
  awakened essence of a soul the Heart guards; founding header "An Echo Awakens" (not "Leveled Up to 1").
  Founding card layout screenshot-verified (full copy fits, 3-across buttons, one dismiss).
- **15 top-band modals** now register with PanelManager (back-button + battle-lock arbiter).
- **Builds:** Seeker APK -> Windows -> WebGL launched detached ~06:28; WebGL DEPLOY pending owner `vercel` CLI.

## Persistence / save
- Save schema **v35** (v29 heroLevel/heroXp/heroLifetimeXp; v30 strategicPlacementMigrated WO-673; v31 echoLanes; v32 freeBuildsUsed; v33 echoLanes `lane:level` token WO-738 — deliberate pass-through; v34 persists Tribes/Wards/Arena + pet active-slot; **v35** `obsidianQueue` — WO-773 multi-channel Builder/Train/Research queue, `MigrateToV35` folds legacy buildJobs/pendingBuilds/buildingCooldowns into the Builder channel, idempotent). Every bump carries a `SaveMigrator` step so the CORE_SAVE version-triple oracle stays green. *(verified from SaveSchema.cs:35/SaveMigrator.cs 2026-08-01)*
- **Persisted:** BaseLayout, Zones, PartyMemberIds, ArenaDefense, PetName, Settlements. **NOT persisted (truthful red oracles):** Tribes, Wards, Arena W-L record, pet active-slot map, broken-tower state. *(2026-07-12)*
- Local save = PlayerPrefs `dotr-save`, signed (LB-3 HMAC, tamper-rejected); server save/load nonce-auth is built but `BackendAuthConfig.Enforced` = **OFF**. *(2026-07-12)*

## Data catalogs
- **Dual-copy rule: `Resources/Data/Canonical` WINS at runtime** over StreamingAssets. `DATAWEB` oracle enforces content sync. *(2026-07-12)*
- **Gear ruling:** the SMALL curated set is deliberate ("only a few prefabs — nothing decent to use yet") → **Resources is truth for weapons/armor**; sync Resources → StreamingAssets. The 433-weapon StreamingAssets copy is the stale side. *(owner 2026-07-12)*
- Drifted pairs found (sync pending): weapons, armor, daily-quests, skin, stake-rewards, tower-perks. *(2026-07-12)*
- The "six StreamingAssets-only WebGL-broken catalogs" are **already mirrored** (that risk-ledger line is stale). *(2026-07-12)*
- **Echo model (WO-738, owner Path-B ruling):** 6 collectible spirits (identity in the `EchoRosterCatalog` CODE TABLE — no ScriptableObjects, WebGL-safety ruling), balance in `echoes-balance.json` (dual-copy). Each echo has element + level (max 8) + one assigned functional lane (Harvest/Crafting/Defense/Exploration). `EchoBonusCalculator` is the single math source (economy + UI + `EchoSpecializationRegression` oracle all read it). Echoes NEVER fight: Defense = passive offline city-raid bonus, Exploration = dungeons-only — both STUBBED (write to Core `EchoLaneBonuses`, hosts read when they land); **Harvest + Crafting are the felt-now lanes.** Picker reachable via roster-card tap (the wisp-injector path is dead). *(2026-07-17)*

## Backend / web
- **`api/` lives IN THIS REPO and is git-TRACKED** (not gitignored, not a separate React repo). Deploys ride any `vercel deploy` from C:\EOA. *(2026-07-12)*
- WebTrace: `?trace=1` → `POST /api/trace` → Neon **`analytics_events`** (`event_name='web_trace'`; no separate web_traces table). **CLI read path = the `[sig]` echo in Vercel runtime logs** (`DATABASE_URL` is sensitive/unpullable). *(proven 2026-07-12)*
- **No TTL cron exists** for trace rows (security H1 — fix pending). Open POSTs (trace/track/bug-report) have **no rate limit**. *(audit 2026-07-12)*
- db-viewer: `tools/db-viewer/index.html` + `api/admin/db.js`, key = `ADMIN_DASH_KEY` (Vercel env, set + redeploy to activate). *(2026-07-12)*

## Web triage — the read path (LIVE as of 2026-07-15; it never was before)
- **`ADMIN_DASH_KEY` is now SET** (Vercel env, preview+production; value in gitignored `.admin-dash-key`).
  It had NEVER been set, so `tools/db-viewer` + the `/triage-web-issue` skill were **dark since written**
  — 70,053 `analytics_events` rows accumulated unread. Endpoint verified live. *(2026-07-15)*
- ⚠ **`vercel logs` CANNOT give you the `[sig]` lines** — proven: even `--json` returns exactly ONE
  message per request (the summary `[web_trace] sess=… lines=N signal=N`); the per-line
  `[sig]` echoes from `api/trace.js:67` are never surfaced. The canon read-path "the `[sig]` echo in
  Vercel runtime logs" gets you `signal=18` but **not the 18 lines**. **Real read path = the admin
  endpoint** → `api/admin/db.js?view=traces` (sessions) → `&session=<id>&order=asc&limit=50` (the
  scene-load HEAD, where TERRAINDIAG / MagentaGuard / catalog resolution live). Header
  `x-admin-key`; base rotates per deploy → `Builds\admin-preview-url.txt`. *(2026-07-15)*
- **`order=asc` + `offset` + `total_batches`/`has_more` added** to the traces view. It was
  `DESC LIMIT 20` with no offset, so long sessions (one ran 2840 batches / 153k lines) were readable
  only from the TAIL = gameplay spam; the diagnostic head was structurally unreachable. *(2026-07-15)*
- **WEB F8 WATCHER LIVE:** `.claude/skills/run-defenders/websig-watch-start.ps1` polls the trace DB and
  emits into the SAME `logs/f8-inbox` with the same PING seq contract, so `f8-check-inbox.ps1` covers
  desktop AND web. Start it alongside `f8-watch-start.ps1`. Proven against the known-bad 07-15 session:
  29 signal hits, fires on the MAGENTA line. *(2026-07-15)*
- **Sessions are attributable from 2026-07-15:** `WebTrace._buildId` = `<version>@<host>` (was
  `Application.version` = **"1.0" for every build**, so a magenta preview and healthy prod were
  indistinguishable). Needs a WebGL rebuild to reach players. *(2026-07-15)*

## Ground / terrain (RCA 2026-07-15 — the magenta ground)
- **The visible ground of `Main_Castle_Overworld` is the `ExteriorTerrain` Terrain**, NOT the courtyard
  tiles (those are dropped to Y=-0.5 + hidden by GroundZFightFixer). It binds its material BY GUID at
  `Main_Castle_Overworld.unity:16016` -> `0eb083914b7ffae4eaf721e2353fea0b` =
  `Assets/Generated/Terrain/ExteriorTerrainMaterial.mat`. *(2026-07-15)*
- **That .mat was NEVER IN GIT** (`git log --all -- <it>` = empty) — `Assets/Generated/` was ignored, so it
  only ever existed on the machine that ran the terrain bake (laptop, baked 05-31). Its siblings
  (ExteriorTerrainData + the 5 .terrainlayers) WERE tracked, committed before the ignore rule → the ground
  was **walkable but MAGENTA** on every other machine. **FIXED:** original recovered from the laptop share +
  `Assets/Generated/Terrain/` is now TRACKED (the whole bake folder). *(2026-07-15)*
- **MagentaGuard could not save it:** `MagentaGuard.cs` gated terrain recovery on `tm != null &&
  IsBrokenShader(...)` — a NULL material short-circuited it, so the fix never fired (the FloorDiag line
  reads `mat='<NULL>' ... broken=False`). Now treats a null materialTemplate AS the break. *(2026-07-15)*
- **The web build is NOT blind — read the live trace, don't hunt a desktop log** (a CLI got this wrong
  2026-07-15 and wrote a bogus WO; the answer was already in START_HERE §3). `FlowTrace.cs:28` defaults
  `Enabled = isEditor || isDebugBuild` **on purpose** (PII: hot-path lines carry wallet ids / save-blob
  lengths / roster), but `WebTrace.cs:162` sets `FlowTrace.Enabled = true` when web tracing activates, and
  BOTH its gates are already open: `FeatureFlags.cs:117` `WebTrace => Get("webtrace", defaultOn: true)` and
  `WebTrace.cs:63` `TraceEndpoint = https://defenders-of-the-realm-v2.vercel.app/api/trace`. So a live web
  session streams `[Flow:*]` to Neon `analytics_events`; **CLI read path = the `[sig]` echo in Vercel runtime
  logs**. ⚠ `WebTrace.cs:11-15`'s header still says "DORMANT BY DEFAULT / default OFF" — **the comment LIES**
  vs `defaultOn: true` (classic: verify from code). *(2026-07-15)*
- Minor, unfixed: `FloorDeepDiag.cs:32` is hard-scoped to `TargetScene = "MainCastle_Hall"`, so it never runs
  in the live merged world `Main_Castle_Overworld`. MagentaGuard's own FloorDiag dump is what actually fires. *(2026-07-15)*
- ⚠ **`/Assets/Resources/Structures/` is gitignored** (`.gitignore:121`) — only **4** models are tracked
  (ArcaneSpire_1/2/3, WizardTower_1); the other ~37 arrive ONLY by manual LAN copy from the laptop.
  **This is DELIBERATE and stays** (owner ruling 2026-07-15): there are exactly **two machines** (this
  desktop + `Kayden-Laptop`, share `\\<ip>\EoA`, user `Kayden-Laptop`) — no CI, no fresh clones, so the
  big-art-out-of-git policy holds and LFS is not worth it.
  **The real risk is TWO-MACHINE DRIFT, and it is not theoretical — it caused BOTH 07-15 bugs:** the
  terrain material existed only on the laptop (magenta ground), and the 22:00 exe was cut 45 min BEFORE
  the 22:45 art copy, shipping 4 of 41 models as placeholders while the build reported SUCCESS.
  **Mitigation = make drift LOUD, not tracked:** a pre-build oracle that fails when
  `structures-catalog.json` `visualPrefabPath` keys do not resolve on disk. Proposed 07-15, owner's call. *(2026-07-15)*

## Builds
- **PROD (current) = the 07-16 six-fix build** — `q2v5vj86g`, promoted 2026-07-16, public, on
  `defenders-of-the-realm-v2.vercel.app`. Rollback target recorded in `Builds/PROD_ROLLBACK.txt`
  (prior prod `44dellx2j`). Commit `77e927be` (pushed to origin). *(2026-07-16)*
- **2026-08-01 release train:** fresh desktop exe · Seeker APK v-wave installed on-device + Firebase App
  Distribution (testers) · WebGL→Vercel preview refreshed. Screenshot archive
  `Builds\ui-capture-archive\2026-08-01\`.
- **Web-build self-test = `tools/webbot/`** (Playwright): `webbot.js` drives the DEPLOYED build for
  screenshots + live browser-console `[Flow:*]` capture + a drag-pan engage check; `introtest.js`
  clicks Play Intro. CAVEAT: synthetic clicks do NOT reliably fire Unity uGUI buttons in WebGL — the
  bot verifies boot/render/console + asset serving, but button-driven flows (Play Intro, into Build
  mode) need owner felt-test or the codec/HTTP determinants. Pass the SSO bypass param in the URL. *(2026-07-16)*
- **Intro video plays on web** (owner Q 2026-07-16): determinants all green — `StreamingAssets/Video/
  Defenders.mp4` serves 200 (`video/mp4`, 4MB), codec = **H.264 avc1 + AAC mp4a** (browser-compatible;
  Unity copies StreamingAssets raw so codec IS the web risk), `IntroSequencePlayer` uses VideoPlayer
  **URL source** (not VideoClip) with the WebGL `audioOutputMode=Direct` fix. *(2026-07-16)*
- **Ship WebGL = `BuildOptions.None`** (Development is opt-in `-DevBuild` — NEVER deploy a DevBuild: Development players paint the full-screen error overlay). Desktop release still ships Development (open item). *(verified WebGLBuild.cs:124 / DesktopBuild.cs:178, 2026-07-12)*
- Deploy chain: `webgl-vercel-overnight.ps1` detached; markers + `DEPLOY_URL` in `Builds/webgl-chain-status.txt`. Preview only; promotion + push are the owner's.
- Fleet baseline: DataRegression = **REGRESSION_OK, 0 reds** — all 5 long-standing reds fixed 2026-07-19 (R1 arena ground texture, R2 dual-wallet Grant->GameState, R3 pet active-slot persist, R4 core-save Tribes/Wards/Arena persist, R5 orc-raider SSOT enemies.json Hp 130). *(2026-07-19)*; re-certified 2026-08-01 with UI_CAPTURE_OK 23 (103 checks).

## UI / MVVM (WO-744 — DONE 2026-07-18)
- **Strict MVVM across the whole game:** every panel View binds an `IPanelViewModel` and reads NO
  game state at runtime; all state/logic lives in the VM (`CreateDefault` is the sole resolution
  site). All 36 audit panel Views migrated (silos B/C/D/E/F/G) + the landmines: BattleHudUgui behind
  `ff.battlehudvm` (default OFF; ATB feel-sim untouched), DialogueView with the WO-702 truce relocated.
  Spec: `docs/UI_MVVM_MIGRATION_PLAN.md`. *(2026-07-18)*
- **The ratchet is ARMED:** `UiMvvmConformanceRegression` runs in `DataRegression` as `[ui-mvvm]`
  with `HardFailOnNew=true` + an EMPTY baseline — any NEW View that reads game state (EconomyService/
  GameStateService/Find*Type/gameplay catalogs) HARD-FAILS the gate. Non-panel offenders (flow
  controllers, spawners, benign EventSystem/sibling finds, HUD wiring) are allowlisted with reasons. *(2026-07-18)*
- Shared VM seams: `Core.UI.Mvvm.WalletVM` (DTO) + `LiveWalletSource`, `GearIconCatalog` (icon leak),
  promoted `Core.UI.Mvvm.CraftRecipeVM`, `ArenaPaletteVM`, `StructureCardVM`/`PlacedTowerListVM`. *(2026-07-18)*

## Room Forge (WO-740–745 — DONE 2026-07-18)
- Socketed-room dungeon pipeline merged to mainline: 17 default room prefabs + shared KayKit
  materials; JSON compose layouts (`Assets/**/dungeon-layouts/`, dual-copy + `version`); the demo
  bakes clean (`matesOk=2 matesFail=0`, NavMesh `PathComplete`); `RoomForgeRegression` (`[room-forge]`,
  10 cases) + `[Flow:DungeonBake]` + baker hard-gate/re-verify fixes. Editor menus under
  `Defenders/Dungeon/*`. KayKit atlas stays machine-local (big-art-out-of-git). *(2026-07-18)*

## UI / input
- ASCII-only TMP strings (non-ASCII glyphs = tofu □ on device); never meaning by color alone (owner red/green colorblind). HUDUI oracle locks the tofu class. *(2026-07-12)*
- Build-mode touch: uGUI verb bar + PLACE + kit d-pad (publishes `HudMoveInput` → merged with arrow-key read). GhostPreview moves its CHILD visual — probe via `GhostPreview.CurrentPosition`, never the host transform. *(2026-07-12)*
- **Right ActionBar = Attack + Q/W/E/R named skills:** Sword Wielding / Sword Heroic / Shield Charge / Warden's Grace / Radiant Strike. **Mobile HUD shows NO key-letters** (WO-750 SPEC). *(2026-07-19)*
- **All placed items normalized by Y-height:** default **4m**, tower override **7m**, siege override **3m**, + a Y-height audit tool (WO-751 IMPLEMENTED). *(2026-07-19)*
- **Destroyed items = NO rebuild + full-cost + VFX cleanup** via a new `Destructible` component (WO-753 in progress). *(2026-07-19)*
- **Headless UI-screenshot pass must run before builds** (felt-test-wave standing rule). *(2026-07-19)*

## Echo canon
- **Echo = the essence of a person the tree of Elarion guards** — 6 named people: Aldwin, Elowen, Corvin, Bran, Doran, Maren. Feeds the WO-752 founding-card overhaul + post-tutorial interjection (SPEC + creative sign-off, awaiting copy). Balance/lane model unchanged (see the Echo model line under Data catalogs). *(2026-07-19)*

## Process
- Boot: **START_HERE.md** routes everything; SAMANTHA.md = the confirmation gate; PREFLIGHT_GATE A/B/C.
- Phone/async triage: `/triage-web-issue` skill — pull the web-trace from the db (`api/admin/db.js`, `X-Admin-Key`=`ADMIN_DASH_KEY`), RCA from the proving line, write the WO left READY for the Windows machine. *(2026-07-12)*
- WO numbering: mint from the `CLI_LANES_WO_NUMBERS.md` banner (**832** as of 2026-08-01; NEVER copy the number — the banner is the only authority; historical: 761–773 consumed — 762 builder-queue, 763 Wisdom, 764 hub-Y-height, 765 capture-Default-Town, 766 Seeker wallet, 767 texture caps, 768 thin-client, 769 Firebase auth, 770 dungeon, 771 raid, 772 enemy, 773 Obsidian queue; earlier: 739-753 consumed — 750 Right-ActionBar naming SPEC, 751 Y-height normalization DONE, 752 Echo founding-card SPEC, 753 Destructible IN PROGRESS; Grok-03 here→there = **716–722** + **715** VFX; see `docs/UI/Grok-03-here-to-there-WO-program.md`), bump in the same edit. ⚠ UI-seat mints in the old 674–685 space collide — translation table in the banner; owner syncing the UI seat 07-13. Collisions resolved 2026-07-13: 677–681 duplicate specs renumbered to 688–692, 682/683/685 dupes to 695/693/694; a fresh 07-13 mint colliding with the 684 board renumbered to **696** (repair-before-upgrade context). *(2026-07-13)*
- Outstanding board: `WorkOrders/WORK_ORDER_684_outstanding_items_board.md` (exact asks + steps).
- ✅ Apex dragon model = **SWAP LANDED 2026-07-24 (WO-760)** — the licensed Asset-Store dragon (product 71047 "Dragon Animated", WDallgraphics; source `Assets/Dragon/`, now git-tracked, not gitignored) ships as `Resources/Enemies/Boss_Dragon.prefab`, built by `DragonAnimatorSetup` + force-tracked `Assets/Generated/Animators/SyndrathDragon.controller`. Old CC-BY-NC 3DHaupt `Dragon.fbx`/2 controllers/materials + the orphan `Prefabs/Village/Generated/Boss_Dragon.prefab` git-rm'd; unlicensed `RedDragon 1.2` stray deleted; `EnemyFactory` dragon keys repointed to `Boss_Dragon`. ⚠ **The earlier "RESOLVED 2026-07-23" claim was PREMATURE** — that commit only repointed comments; the CC-BY-NC model still SHIPPED (Resources includes unused assets) until the 07-24 builder-run + git-rm. Commercial-ship blocker now ACTUALLY cleared; boss "Syndrath the Devourer" retained; fly-in->land->burn-towers->retarget-Tree behavior built (WO-760, felt-verify pending).
