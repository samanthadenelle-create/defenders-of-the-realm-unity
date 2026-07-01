# CANON GROUND TRUTH — 2026-07-01

> **Purpose:** the single anchor of *current reality*, derived ONLY from verified sources
> (HEAD commit arc, working tree, save schema, the auto-memory index, and the combat/region canon)
> — NOT from assumptions. Every doc-audit agent checks files against THIS. If a doc contradicts a
> line here, the doc is STALE (unless it is a correctly-dated historical ledger, which is frozen-OK).
> If you believe a line *here* is wrong, FLAG it — do not silently trust it.
>
> Sourced 2026-07-01 from: `git rev-parse HEAD` (`36c901f2`) + `git log` (the Pi-web arc `8b79a494`→`36c901f2`),
> `git status -sb` (ahead of `origin/wip` by 19), `Assets/_Modules/Core/State/SaveSchema.cs`
> (`CurrentVersion = 28`), `Assets/_Modules/Core/FeatureFlags.cs`, `git stash list`, `RESUME_2026-06-30_seam-unstack.md`,
> and the 07-01 memory index (Pi-auth-resolved, vercel-100mb, release-process, localhost-webgl-bots, hackathon-july).
> **Supersedes `CANON_GROUND_TRUTH_2026-06-28.md` (now frozen/superseded).**

## Repo / git
- **Branch:** `wip/village2-and-f8-tickets`.
- **HEAD:** `36c901f2` (2026-07-01). **Upstream now exists** (`origin/wip/village2-and-f8-tickets`); local is
  **19 commits AHEAD** — i.e. the earlier Pi-web arc partially pushed, the latest 19 are local-only.
- **Sole committer = CLI.** Commit by EXPLICIT PATH; push only on explicit owner OK after felt/regression verify.
- **Working tree is dirty** (canonical data JSON, several `.cs`, plus untracked `.meta`/OffsetForge files) — the
  07-01 web + dev-tools + data work in progress. Reconcile by path; never `git add -A`.

## Live thread (2026-07-01) — WEB / PI STABILIZATION (this supersedes the 06-30 seam thread as *current focus*)
- Per the **release-process** model: we are **PRE-RELEASE** → goal = stabilize + make the web build playable
  (4-point DoD: deployment consolidated, sign-in works, core loop playable start→finish, zero P0s); THEN
  code-lock + release train, P0-only hotfixes.
- **Pi sign-in = RESOLVED (2026-07-01), registration verified.** Root cause was `vercel.json`
  `COOP/COEP:require-corp` silently blocking the cross-origin `pi-sdk.js` (no CORP) → `window.Pi` undefined.
  Fix = REMOVE COOP/COEP (safe: `webGLThreadsSupport:0`) + `no-store` on index.html (bust stale COEP cache) +
  CORS on the API endpoints (published app runs cross-origin under `pinet.com`). Commits `d5fcb48d` (root),
  `ca24bda3`, `4346b893`, `150becf8` (sign-in timeout/double-trigger guard). My earlier "size/preview won't
  boot" theory was WRONG — instrument→prove won it (memory `pi-auth-codecomplete-blocker-is-preview-load`).
- **Web observability SHIPPED:** `WebTrace` (`?trace=1`) enables `FlowTrace.Enabled=true`; `api/trace` echoes each
  Pi/error signal line to Vercel runtime logs (readable without the sensitive `DATABASE_URL`) → Neon. Commits
  `9901240e`, `22b0f7bb`, `0e155efd` (release build was trace-silent until WebTrace enables FlowTrace).
- **Localhost dev WebGL bots (the web arm of the bot engine):** `build-webgl.ps1 -DevBuild` compiles AutoPilot in;
  `?autopilot=1&seed=N` spawns an in-browser chaos bot; `serve-webgl.ps1` hosts localhost (no 100MB cap). Commit
  `b41469d4`. **UNVERIFIED:** AutoPilot has never actually driven in-browser — first run must confirm it drives.
- **Dev tools owner-gated + dungeons flagged OFF + perf self-report** (`979485c1`); dev-panel auto-close +
  tower-placement FlowTrace (`36c901f2`); duplicate-hero-on-town-return dedupe + WebGL intro streaming (`420eea33`).
- **Vercel still blocked by the 100 MB per-file limit** — monolithic `WebGL.data` (113.7 MB) fails upload; the real
  fix = WO-545 Addressables (heroes out of Resources → stream), NOT a compression hack. itch stays the web home.
- **Hackathon target = JULY 31, not June** (monthly; web not yet stable → no rush). Banked: size 250→85 MB, deploy
  pipeline proven, Pi verified rendering in Pi Desktop.

## Seam un-stack (WO-453) — PARKED, not landed
- The **un-stack is NOT applied on this machine.** `Assets/_Modules/Core/World/WorldGeometry.cs` does **not exist**
  in the working tree. The office-session un-stack (+carve +overnight features) is parked in **`stash@{0}`**
  ("WIP 2026-06-30: overnight features + world-carve + unstack — parked for selective re-apply").
- What DID land & felt-verify (06-30 afternoon): terrain depression fix (`37ae7cb1`, CastleDepressionDepth −3→0)
  and the TRUE-ROOT `.gitattributes` binary-asset EOL fix (`825f6af2`, force `*TerrainData/NavMesh-*/LightingData
  .asset binary`; memory `gitattributes-binary-asset-eol-corruption`). The built exe still ping-pongs the seam
  (un-stack not applied) — don't re-test the seam on it.
- Resume for the un-stack when we return to it: `RESUME_2026-06-30_seam-unstack.md` (frozen, still valid).

## Combat / hero north star (unchanged from 06-28)
- **ONE controllable hero (Knight, "Grom").** Everything else autonomous. Single-hero / knight-only flags ON.
- **Blink full-body rig is JUNKED** — hero = a single Tripo self-rigged model, static armor, NO mesh-swap. Blink
  survives ONLY as a UI re-skin kit (`BlinkChrome`).
- **Animated real-time combat lives in the OVERWORLD** isolated BattleArena (lock-on WO-512, 9-zone HUD). **ATB is
  separate** (flat/static). **WO-584 dungeon/outpost/arena consolidation** = one warp-in space primitive, 3 skins,
  resolver + ownership flip; `ff.atbdungeon` OFF. **Dungeons are currently flagged OFF** (979485c1) pending the
  resolver slice.
- **`ff.noautoheal` defaultOn = true** (no post-combat auto-heal-to-full; reversible via PlayerPrefs).
- Roster = Tripo only. **V1 = Knight + ORCS only.**

## UI canon (unchanged)
- **`docs/UI_BLINK_TEMPLATE_CANON.md` (BINDING)** — code-built uGUI; one master factory `BuildObsidianPanel(frameName)`
  renders the real Blink frame + returns header/body/medallion/footer drop-zones; screens DROP content + bind, never restyle.
- **New title key-art** shipped (`8b79a494`, "Echoes of Elarion", Grok fit-to-frame portrait). Full title =
  *Defenders of the Realm: Echoes of Elarion*; tagline "Hold the last light."

## World / scene (unchanged)
- Home hub = `MainCastle_Hall`; `OuterWorld` streams additively; `Village2` = raid-target; `Village.unity` = ABANDONED.
- Castle↔OuterWorld = four-side warp gates (`RuntimeRegionGate`), WARP by design (stacked navmeshes don't auto-connect
  — the root the parked un-stack addresses). Castle moat + 4 drawbridges (`ff.castlemoat`); tree aura + tower glow
  (`ff.hubambientvfx`).

## V1 economy / progression (wired)
- **Echo workforce** wired; offline via real clock. **Save schema = v28** (WO-587 Population & Echo growth:
  populationXp + populationQuests + populationOutposts + populationEchoSlots survive save/load — milestone-driven
  Echo slot unlocks, all additive default-on-read). v27 = wall-mount seating; v26 = ring/amulet equip persistence.
- Village-tier upgrade → WO-432 building upgrade tree (WC3-style perks, Heart gate). Store redesign (WO-501); gear
  balance (WO-500); Offset Forge offsets on weapon attach (WO-490/510).

## Monetization / distribution
- **LIVE on itch:** `denellestudios/defenders-of-the-realm-defend-the-tower` (channel `html5`).
- **Pi:** sign-in resolved & verified (see live thread). **Vercel = blocked by 100 MB per-file limit** until WO-545.
- **SKR = the REAL Solana/Seeker token, used NON-CUSTODIALLY** (no mint, no game-held withdrawable balance). V1 ships
  with zero crypto (memory `skr-separate-ingame-currency-real-token-readonly`).

## Process canon (unchanged, BINDING)
- Read-first every session: `SESSION_CANON_LOADER.md` + this anchor + newest `RESUME_*.md` + `docs/MASTER_CATALOG.md`
  + `docs/*ARCHITECTURE*`. **Instrument, don't guess** (§12 hard gate). **Ticket pipeline** QA→CLI→PO. **UI never
  writes code; CLI is sole committer.** Never hand-edit `.unity`. Stage by explicit path, never `git add -A`.
- **Notion "Work Orders" DB = live board** (data source `5f66b263-c732-4075-b94a-f5f4de9f8087`); WO spec files stay
  in repo. WO-numbering authority = `MASTER_PIPELINES_BACKLOG_2026-06-06.md` + `CLI_LANES_WO_NUMBERS.md`.
