# KEY FACTS — the living fact sheet (update IN PLACE, never snapshot)

> **Rule (owner directive 2026-07-12):** this file is LIVING — when a fact changes, edit the line
> in the same commit as the change and re-stamp its date. Facts here are code-verified, never
> assumed. If a doc contradicts a line here, the doc is stale. Dated anchors
> (`CANON_GROUND_TRUTH_*`) remain the session snapshots; THIS file is the always-current card.

## ⭐ NORTH STAR — the state we are building toward
- **The product:** "Echoes of Elarion" (chapter) in the "Defenders of the Realm" series — "Hold
  the last light." **V1 = ONE controllable Knight ("Grom")** in an overworld with isolated
  real-time BattleArena combat; **the player builds their own city** (player-defined map pivot
  07-11: Build → place/move/rotate functional structures — **build mode IS the demo**).
- **The platform:** **mobile web in Pi Browser** — Pi Hackathon deadline **July 31, 2026**.
  Desktop is the dev proxy, never the verdict.
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

## Persistence / save
- Save schema **v30** (v29 heroLevel/heroXp/heroLifetimeXp; v30 strategicPlacementMigrated WO-673). *(verified from SaveSchema.cs 2026-07-12)*
- **Persisted:** BaseLayout, Zones, PartyMemberIds, ArenaDefense, PetName, Settlements. **NOT persisted (truthful red oracles):** Tribes, Wards, Arena W-L record, pet active-slot map, broken-tower state. *(2026-07-12)*
- Local save = PlayerPrefs `dotr-save`, signed (LB-3 HMAC, tamper-rejected); server save/load nonce-auth is built but `BackendAuthConfig.Enforced` = **OFF**. *(2026-07-12)*

## Data catalogs
- **Dual-copy rule: `Resources/Data/Canonical` WINS at runtime** over StreamingAssets. `DATAWEB` oracle enforces content sync. *(2026-07-12)*
- **Gear ruling:** the SMALL curated set is deliberate ("only a few prefabs — nothing decent to use yet") → **Resources is truth for weapons/armor**; sync Resources → StreamingAssets. The 433-weapon StreamingAssets copy is the stale side. *(owner 2026-07-12)*
- Drifted pairs found (sync pending): weapons, armor, daily-quests, skin, stake-rewards, tower-perks. *(2026-07-12)*
- The "six StreamingAssets-only WebGL-broken catalogs" are **already mirrored** (that risk-ledger line is stale). *(2026-07-12)*

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
- Fleet baseline: DataRegression = 3 known pre-existers (arena ground, B2 dual-wallet, pet-slot) **+ 3 expected CoreSave fail-by-design reds** (Tribes/Wards/Arena). *(2026-07-12)*

## UI / input
- ASCII-only TMP strings (non-ASCII glyphs = tofu □ on device); never meaning by color alone (owner red/green colorblind). HUDUI oracle locks the tofu class. *(2026-07-12)*
- Build-mode touch: uGUI verb bar + PLACE + kit d-pad (publishes `HudMoveInput` → merged with arrow-key read). GhostPreview moves its CHILD visual — probe via `GhostPreview.CurrentPosition`, never the host transform. *(2026-07-12)*

## Process
- Boot: **START_HERE.md** routes everything; SAMANTHA.md = the confirmation gate; PREFLIGHT_GATE A/B/C.
- Phone/async triage: `/triage-web-issue` skill — pull the web-trace from the db (`api/admin/db.js`, `X-Admin-Key`=`ADMIN_DASH_KEY`), RCA from the proving line, write the WO left READY for the Windows machine. *(2026-07-12)*
- WO numbering: mint from the `CLI_LANES_WO_NUMBERS.md` banner (**next free = 723**; Grok-03 here→there = **716–722** + **715** VFX; see `docs/UI/Grok-03-here-to-there-WO-program.md`), bump in the same edit. ⚠ UI-seat mints in the old 674–685 space collide — translation table in the banner; owner syncing the UI seat 07-13. Collisions resolved 2026-07-13: 677–681 duplicate specs renumbered to 688–692, 682/683/685 dupes to 695/693/694; a fresh 07-13 mint colliding with the 684 board renumbered to **696** (repair-before-upgrade context). *(2026-07-13)*
- Outstanding board: `WorkOrders/WORK_ORDER_684_outstanding_items_board.md` (exact asks + steps).
- ⛔ Apex dragon model = CC BY-NC — license/replace before commercial release.
