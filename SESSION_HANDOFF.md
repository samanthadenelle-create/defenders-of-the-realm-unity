> ⚠ **STALE — pre-pivot process/state doc** (stale branch `feat/tower-core-loop`, Linear board, or Solana/tower-defense framing). Board = Notion; branch = `wip/village2-and-f8-tickets`. Live reality: `CANON_GROUND_TRUTH_2026-06-26.md`.

# Session Handoff — 2026-05-25

Read this first if you're a fresh Claude session (e.g. picking up on the laptop).
The previous session ran on the owner's desktop PC; this file carries the state across.

## Where we are right now
- Repo is at commit **`30ff18b`** (master). The laptop is synced to it and **building a
  Development player** to playtest.
- The big art (KayKit `Models/`, `Art/TripoStructures/`, `Resources/Structures/`) is
  **gitignored** and travels by zip, NOT git. It was transferred via `export-assets.ps1`
  (desktop) → zip → `import-assets.ps1 -ExportDir <unzipped>` (laptop). It's already in place.
- Hero FBX (`Assets/Resources/Heroes/*.fbx`) travel via **git LFS**. Run `git lfs install`
  then `git lfs pull` after cloning or the heroes come down as pointers. (Knight.fbx should be
  ~2.49 MB / 2,615,964 bytes — if it's ~130 bytes it's still a pointer.)

## What this session fixed (all committed + in the build)
- **Crash on Village load ("level3 corrupted / Position out of bounds")** — root cause was an
  over-enlarged village (~5000+ scene tiles) corrupting scene serialization. Fix: reverted the
  village to the original ring size (`WallHalfX=28, WallHalfZ=21`) and original building
  positions. **DO NOT re-enlarge the village** — that's what crashes it. Static batching is also
  disabled in the build (`DesktopBuild.cs`) as belt-and-suspenders.
- **Build HUD** (`30ff18b`) — clicking HUD "Build" did nothing because `BuildMenu`'s UIDocument
  had no PanelSettings, so it opened invisibly. `BuildMenu.Awake` now borrows the scene's
  PanelSettings (the HUD's) and sorts above it. Runtime fix, no re-bake.
- **Dev gear / F1 menu** — `DesktopBuild.cs` now builds with `BuildOptions.Development` so the
  DevTools QA panel (force-wave, grant-materials) compiles in (it's `#if DEVELOPMENT_BUILD`).
  **You must tick "Development Build" in Build Settings** (or use `build-windows.ps1`) to get it.
- **Tripo owner models** — 5 village buildings + 3 hero bodies + dungeon Portal. Tripo FBX import
  tipped 90° and their extracted materials render rainbow; fix is rotate `-90°X` + force a URP/Lit
  material from the basecolor texture (single-mesh buildings) or `TripoMaterialFixer.ForceRebuildAll`
  (multi-part, e.g. PetHome). PetHome colors still need an in-game eyeball.
- **Enemy spawn → march → breach loop** (WO-27) — exterior approach corridors + spawn points;
  gates excluded from the NavMesh bake so enemies/hero pass through (verified PathComplete 4/4).

## WORK ORDER 29 — status
- **§5 Build HUD** ✅ done (above).
- **§6 stability** ✅ done (crash, loop, dev gear, Tripo).
- **§1 Explorability** — exterior terrain + 40 m approach corridors exist and gates are passable.
  NEEDS PLAYTEST: walk out a gate and report the *specific* blocker (invisible wall / fall off
  edge / can't exit). Fix that, don't rebuild blind. NOTE: explorability must use the exterior
  **Terrain** + NavMesh (one object), NOT thousands of tiles — tiles are the crash cause.
- **§4 Hero walk animation** — BLOCKED: the hero models are static (Humanoid avatar but
  `clipAnimations: []`, no clips). Needs free **Mixamo** walk/idle/run dropped into the project;
  then wire an Animator + retarget. Owner was asked to grab these. (Procedural walk-bob offered as
  a stopgap if they say so.)
- **§2 hidden dungeon Portals** — NOT STARTED (next). Place 2–3 `Portal` entrances in the exterior;
  `DungeonEntranceBootstrap` already instantiates the Portal at runtime.
- **§3 gathering nodes** — NOT STARTED (next, new system).

## Key gotchas / owner preferences (from memory)
- **Use PowerShell, not Bash** on Windows (Bash mangles paths/encoding here).
- **Don't auto-launch the built game** to "verify" — the owner playtests builds himself. Verify
  via build artifacts or headless logs only:
  `DefendersOfTheRealm.exe -batchmode -nographics -bootScene Village -logFile <log>` (loads the
  Village without the game window; "Loop armed" + no "corrupted" = good).
- **KayKit art loads by hardcoded path** `Assets/Models/KayKit/<pack>/` in the village builder.
  If those 6 packs aren't there, the bake fills with placeholders. (Editor/player resolve by GUID
  so they look fine, but the *builder* needs the path.)
- **Don't hand-edit curated `.unity` scenes autonomously** — file scene fixes as follow-up work
  orders with acceptance criteria.

## Useful commands
- Build a player: `.\build-windows.ps1` (tick Development for the dev gear).
- Re-bake the village scene (editor): menu `Defenders/Week 3/Build Village Scene`.
- Headless load-check (no game window): see the `-bootScene Village` line above.
- Asset transfer: `export-assets.ps1` (has art) → zip → `import-assets.ps1 -ExportDir <unzipped>`.

## Immediate next step
Owner is building + playtesting on the laptop. Wait for their report on: Build HUD, F1 dev menu,
and walking out a gate (§1). Then: fix the specific §1 blocker, do §2 (dungeon Portals) + §3
(gathering), and wire §4 once Mixamo clips arrive.
