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
- ⚠ **The instruments are OFF in ship builds:** `FlowTrace.cs:28` = `Application.isEditor ||
  Debug.isDebugBuild`, and ship WebGL/desktop = `BuildOptions.None` → every `[Flow:*]` line (FloorDiag,
  MagentaGuard, Guard) is **suppressed in prod**. The guards still ACT; they just report nothing. This is why
  a live magenta had to be diagnosed from a *desktop* Player.log. **Open gap — needs a WO.** *(2026-07-15)*
- ⚠ **`/Assets/Resources/Structures/` is gitignored** (`.gitignore:121`) — only **4** models are tracked
  (ArcaneSpire_1/2/3, WizardTower_1); the other ~37 arrive ONLY by manual LAN copy from the laptop. Any
  build cut on a machine/CI without that copy ships **placeholder buildings**. Deliberate per the big-art
  policy, but it silently broke the 07-14 22:00 exe (art landed 22:45, 45 min AFTER the build). *(2026-07-15)*

## Builds
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
