# Claude Code Work Order — Village Recovery

**Handoff from:** Cowork diagnosis pass on 2026-05-22
**Owner:** Samantha (samanthadenelle@gmail.com)
**Repo:** `C:\Users\Kayden-Laptop\Documents\defenders-unity` (branch `master`, in sync with `origin/master` at `ba393bb`)
**Unity version:** `6000.4.7f1` (per `SESSION-RESUME-AFTER-REBOOT.md`)

## Read these first

1. `docs/diagnosis-report.md` — full root-cause analysis. Don't skip this; it explains *why* the repair steps below are what they are.
2. `docs/recovery-work-orders.md` — the original 7-agent work plan Samantha drafted. Use as scope context.
3. `memory/unity-license-kill-caution.md` — DO NOT broad-kill Unity processes.
4. `memory/unity-build-exe-stub-quirk.md` — ALWAYS clean-delete `Builds/Windows` before player builds.
5. `memory/owner-working-style.md` — autonomous, parallel, terse direction.
6. `SESSION-RESUME-AFTER-REBOOT.md` — the build-chain commands and license-channel caveat.

## State at handoff

- Local `master` at `ba393bb` "Lock untracked Tripo .meta GUIDs before further work" — pushed to origin.
- **57 files modified, uncommitted** (Tripo FBXs + their `Resources/Textures/*.png` companions, plus 5 audio mp3s, link.xml, several onboarding image assets, and a handful of `docs/*.md`). These changes are expected — they are evidence of the broken Tripo import. They get committed as part of Phase A Step 5 below.
- Unity Editor is **not** running on the sandbox. All Editor-driven steps must be run on Samantha's local machine.

---

# Phase A — Tripo + Village repair (P0)

This is the actual fix for the "broken village" symptom. Each menu action is idempotent. Steps 2–4 must be run in Unity Editor; nothing here can be done in batchmode reliably (the postprocessor has known batchmode crash modes — comments at `Assets/Editor/TripoAssetPostprocessor.cs:103-110, 152-159`).

## Step 2 — Force re-extract Tripo textures

**Why:** No `.tripo-extracted` markers exist anywhere in `Assets/`, and no sibling `Textures/` folders exist next to `Cathedral.fbx`, `castle+ballast+Tower.fbx`, `Resources/Heroes/*.fbx`, or `Resources/Pets/*.fbx`. The extraction step never landed. Source materials are still legacy `FbxSurfacePhong` with null `_MainTex`, which is why `TripoMaterialFixer` is masking them as solid-color blobs.

**Action:** In Unity Editor: **Defenders ▸ Tripo ▸ Force re-extract all textures**
(menu defined at `Assets/Editor/TripoAssetPostprocessor.cs:236`).

**Verify Console shows** lines like:
```
[TripoAssetPostprocessor] Extracted embedded textures from Assets/Models/Cathedral/Cathedral.fbx
[TripoAssetPostprocessor] Extracted embedded textures from Assets/Models/CastleGate/castle+ballast+Tower.fbx
[TripoAssetPostprocessor] Extracted embedded textures from Assets/Resources/Heroes/Knight.fbx
... (Mage, Ranger, aether-sprite, flame-pup, ice-wolf, pet-aether-twilight)
```

**Verify on disk** these folders now exist and are populated:

```
Assets/Models/Cathedral/Textures/
Assets/Models/CastleGate/castle+ballast+Tower.fbm/Textures/   (or sibling)
Assets/Resources/Heroes/Textures/
Assets/Resources/Pets/Textures/
```

…each with a `.tripo-extracted` marker file.

**If the postprocessor doesn't fire:** check `git log --oneline -- Assets/Editor/TripoAssetPostprocessor.cs` to confirm the script is current. Look for the `[InitializeOnLoad]` attribute and the `ForceReextractAll` method. If `.tripo-extracted` markers exist but are stale (no actual extraction happened), delete them and re-run. They live alongside the FBXs.

## Step 3 — Rebuild village scene

**Action:** In Unity Editor: **Defenders ▸ Week 3 ▸ Build Village Scene**
(method `VillageSceneBuilder.BuildVillage` — entry at `Assets/Editor/VillageSceneBuilder.cs:24-27`).

**Why this works:** the builder uses `AssetDatabase.LoadAssetAtPath` (path-based, not GUID-based) — it will pick up the now-correctly-imported materials automatically. Builder is idempotent.

**Heads-up:** this also calls `ExteriorTerrainBuilder.BuildExterior()` at line 391, which takes ~2–3 minutes for the 513×513 heightmap + tree scatter. If iterating fast, see Phase C improvement #2 for a way to skip it.

**Verify Console shows** no `[VillageSceneBuilder] KayKit asset missing` warnings and no `ForceHexMaterial` errors. The line to grep for is `[VillageSceneBuilder]`. Any warning there names the specific asset that didn't resolve — that's the next thing to debug.

## Step 4 — KayKit material repair (belt-and-braces)

**Action:** **Tools ▸ DeNelle ▸ Fix KayKit Materials**
(`Assets/Editor/KayKitMaterials.cs`, scans all of `Assets/Models/KayKit/` recursively).

**Why:** re-creates one URP/Lit material per KayKit subfolder against the local atlas and remaps the folder's FBX importer references. Cheap. Documented prerequisite at `VillageSceneBuilder.cs:2430`. Run even if Step 3 succeeded — it heals any per-folder material drift introduced by recent Tripo re-imports.

## Step 5 — Commit the repair

```powershell
cd C:\Users\Kayden-Laptop\Documents\defenders-unity

git add `
    Assets/Models/Cathedral/Textures `
    Assets/Resources/Heroes/Textures `
    Assets/Resources/Pets/Textures `
    "Assets/Models/CastleGate/castle+ballast+Tower.fbm/Textures" `
    Assets/**/*.tripo-extracted `
    Assets/Scenes/Village.unity `
    Assets/Resources/Textures/*.png `
    "Assets/Black Dragon/Dragon_Baked_Actions_fbx_7.4_binary.fbx" `
    "Assets/Black Dragon/Materials/Dragon_Bump_Col2.jpg" `
    "Assets/Black Dragon/Materials/Dragon_Nor_mirror2.jpg" `
    "Assets/Models/CastleGate/castle+ballast+Tower.fbx" `
    "Assets/Models/CastleGate/castle+ballast+Tower.fbm/castle+ballast+Tower_basecolor.jpg" `
    Assets/Models/Cathedral/Cathedral.fbx `
    Assets/Resources/Heroes/Knight.fbx `
    Assets/Resources/Heroes/Mage.fbx `
    Assets/Resources/Heroes/Ranger.fbx `
    Assets/Resources/Pets/aether-sprite.fbx `
    Assets/Resources/Pets/flame-pup.fbx `
    Assets/Resources/Pets/ice-wolf.fbx `
    Assets/Resources/Cosmetics/Pets/pet-aether-twilight.fbx

git commit -m "Repair Tripo materials + rebuild village scene

Extracted embedded textures on Tripo FBXs (Cathedral, CastleGate,
Heroes, Pets), repaired KayKit URP materials, rebuilt Village scene
against the now-correct material references.

Closes Phase A of docs/claude-code-work-order.md.
See docs/diagnosis-report.md for root cause."

git push origin master
```

After the LFS warning prints, push. The unstaged residue should be:
- `.gitattributes` (let Samantha decide whether to land the LFS attribute changes — these are separate)
- `Assets/AddressableAssetsData/link.xml` (autogenerated by Unity — usually fine to commit)
- `Assets/Audio/Resources/*.mp3` (5 audio files — confirm with Samantha whether these are the new authored versions before committing)
- `Assets/_Modules/Onboarding/Art/heart-wing.jpg` + `Resources/HeroPortraits/*.jpg|png` + `Resources/Intro/*.png` + `Resources/PetPortraits/*.jpg` + `Video/studio-bumper.mp4` (onboarding art changes — likely fine to commit, confirm)
- `docs/*.md` (work-in-progress design docs — separate commit)
- `docs/screenshot-*.png` (debugging screenshots — separate commit)

Don't bundle those into the repair commit. Keep the repair scope tight.

## Step 6 — Visual verification

Hit Play in the Village scene. Walk the village. Confirm:

- **Cathedral** renders with stone texture (not solid grey). Tripo-extracted `Cathedral_basecolor.*` should be the `_BaseMap`.
- **CastleGate / ballast tower** renders with its basecolor (not solid white).
- **Knight, Mage, Ranger** heroes render textured (not solid colors). Select hero at HeroSelect and confirm each.
- **Pets** (aether-sprite, flame-pup, ice-wolf) render textured.
- **Hex buildings** render with the medieval color palette (not pink/magenta error).

**Capture screenshots** of the village from a few angles. Stash them in `docs/screenshot-village-repair-YYYYMMDD.png` for posterity.

**If hex buildings render pink/magenta after all four steps:** the issue is `ForceHexMaterial` at `VillageSceneBuilder.cs:1965` failing to load `Assets/Models/KayKit/KayKit Medieval Hexagon Pack 1.0.1/Assets/fbx(unity)/decoration/nature/hexagons_medieval_URP.mat`. Open the Console, look for `[VillageSceneBuilder] ForceHexMaterial` warnings. Likely cause is the shared atlas `.mat` is stale — try `Reimport` on just that one `.mat` file, then re-run Step 3.

---

# Phase B — Remaining UI repair work (P1)

These are Agents 3–6 from `docs/recovery-work-orders.md`. Each is a small, focused repair. Do them sequentially (not parallel) because they share the village scene and you want to test each in isolation.

## Phase B-1 (formerly Agent 4) — HUD top-left status repair

**Symptom:** Only HP shows in the top-left HUD. Mana and Spire Health are missing.

**Likely starting points:**
- `Assets/_Modules/HUD/VillageHudController.cs` — main HUD controller.
- Recent relevant commit: `d0e7cb0` "Mana panel repositioned to top-left (under heart-hp)" — that commit moved the mana panel. Diff it: `git show d0e7cb0`. Compare to what's actually in the scene now.

**Investigation:**
1. Read `VillageHudController.cs` end to end. Find HP, Mana, Spire Health UXML element IDs.
2. Open the village UXML (probably `Assets/_Modules/HUD/Village/*.uxml`) and confirm the elements exist with those IDs.
3. Hit Play. Open UI Toolkit Debugger (Window ▸ UI Toolkit ▸ Debugger). Are Mana / Spire elements present but `display: none`? Or missing from the tree entirely? Or off-screen?
4. Check for runtime code that hides them — grep `.style.display = DisplayStyle.None` on Mana / Spire across the HUD asmdef.

**Fix shape:** likely a USS class missing, an element ID mismatch after a UXML refactor, or a runtime guard that incorrectly hides Mana when (e.g.) the wizard isn't equipped.

**Done when:** HP, Mana, Spire Health all visible top-left, values update in real time during Play.

## Phase B-2 (formerly Agent 5) — Build button UI flow repair

**Symptom:** Build button on HUD exists but clicking it doesn't open the left-side build menu (towers / walls / repair).

**Likely starting points:**
- `Assets/_Modules/HUD/VillageHudController.cs` — Build button is likely registered here.
- `Assets/_Modules/Village/BuildMenu/` — assume a BuildMenu controller lives here.
- Recent relevant commit: `b8cef75` "Pet basecolor PNGs + BuildMenu Tower/Repair simplification" — the BuildMenu was simplified. Diff it.
- `888de07` "Wave timer single + Build click reflection fallback" — `Build click reflection fallback` is a clue: the Build button is wired by reflection, which means a string-named target method. If that method got renamed, the reflection silently fails.

**Investigation:**
1. Grep `Build click` and `reflection` across `Assets/_Modules/`. Find the reflection callsite.
2. Confirm the target method name still exists on whichever controller it expects to find.
3. If the method was renamed, either restore the old name or update the reflection callsite. Prefer updating the reflection — renames usually have a reason.
4. Hit Play, click Build, watch Console for any `[VillageHud]` or `[BuildMenu]` log lines.

**Fix shape:** rebind the button to its target controller, or restore the menu panel if it was disabled in the scene.

**Done when:** clicking Build opens the left-side menu with Towers / Walls / Repair options. Each option leads to its dialog. Repair flow is reachable if damaged objects exist.

## Phase B-3 (formerly Agent 3) — Dungeon portal destination

**Symptom:** Portal works as a bridge but loads a placeholder (empty square room with two pills, or vague 3D stub).

**Likely starting points:**
- `Assets/_Modules/Dungeons/` — dungeon module.
- Recent relevant commits:
  - `f35aff1` "Both portals → Healer's Cottage (temporary)" — **both portals point at Healer's Cottage right now**. This is the placeholder Samantha is seeing.
  - `3ff2160` "Fix double-prefix dungeonId — portals route to real scenes now"
  - `5e9da46` "DungeonPortal uses plain LoadScene — no more fade-hang"
  - `ad539cc` "Folk's Granary movement" — the second dungeon (Folk's Granary) was being built out before this work order.

**Investigation:**
1. Read `DungeonPortal.cs` (likely under `Assets/_Modules/Dungeons/`). Find the destination resolution logic.
2. Confirm scenes `Dungeon_HealersCottage.unity` and `Dungeon_FolksGranary.unity` both exist under `Assets/Scenes/`.
3. Check Build Settings (`File ▸ Build Settings`) — are both dungeon scenes in the build list? If not, `SceneManager.LoadScene` will fail silently.
4. In Village.unity, select each portal trigger and confirm the destination dungeonId is correct (east portal → Folk's Granary, west portal → Healer's Cottage).

**Fix shape:** undo the temporary `f35aff1` mapping (both portals → Healer's Cottage). Restore the east portal → Folk's Granary mapping. Verify Folk's Granary scene is built out enough to be an acceptable destination — if not, this becomes a content task, not just wiring.

**Done when:** west portal loads Healer's Cottage with real dungeon geometry; east portal loads Folk's Granary with real dungeon geometry. Player spawn position is sensible in each.

## Phase B-4 (formerly Agent 6) — Master volume toggle restoration

**Symptom:** Master volume toggle missing from HUD.

**Likely starting points:**
- `Assets/_Modules/Core/Audio/AudioService.cs` (or similar) — the master audio service.
- HUD UXML for Village + Title scenes.

**Investigation:**
1. `git log --oneline -- Assets/_Modules/HUD/` and grep commit messages for "volume", "mute", "sound toggle". Find the commit that added or removed it.
2. If it was added and then disconnected, restore the binding. If it was never built, add a small toggle button to the Village HUD top-right corner (next to the existing `?` Help button).
3. Wire to whatever master audio control exists. If using a Unity AudioMixer, set the master group's volume to -80 dB (mute) / 0 dB (unmute).
4. Persist via `PlayerPrefs` (key `"DeNelle.Audio.MasterMute"`) — there's an existing settings system per `OVERNIGHT_REPORT.md`.

**Fix shape:** new ~30-line component plus a HUD UXML hook. Scope-creep risk: don't expand this into a full settings overhaul.

**Done when:** HUD has a working volume on/off toggle that persists across sessions.

---

# Phase C — Optional architecture improvements

Lift these when convenient. None are blocking.

## C-1 — Extract WallLayout half-extents (low effort, low risk)

`VillageSceneBuilder.cs` and `ExteriorTerrainBuilder.cs` both hard-code village half-extents as `(150, 120)`. Extract to a shared `WallLayout` constant (probably in `Assets/_Modules/Village/WallLayout.cs` — confirm whether that class already exists). Both builders reference it. Prevents drift if the village ever resizes.

## C-2 — Gate ExteriorTerrainBuilder behind a bool param

Add a `buildExterior = true` param to `VillageSceneBuilder.BuildVillage`. When `false`, skip the 2–3 minute terrain pass. Add a sibling menu `Defenders ▸ Week 3 ▸ Build Village Scene (no exterior)` for fast iteration.

## C-3 — Long-term: split exterior into additive scene

Move terrain + tree scatter + landmarks into `Assets/Scenes/VillageExterior.unity`. Load additively from `VillageController` or a small `WorldLoader`. Unload on dungeon entry to free ~10–30MB on mobile.

**Don't do this yet** — defer until either (a) the wilderness expands materially, (b) mobile profiler captures show terrain memory pressure, or (c) merge conflicts on `Village.unity` become painful.

## C-4 — Delete orphan KayKit folders (~600MB cleanup)

After Phase A is verified working:

```powershell
# Back up first — .gitignore excludes /Assets/Models/, so this is unrecoverable from git
cd C:\Users\Kayden-Laptop\Documents\defenders-unity
Compress-Archive -Path "Assets\Models\KayKit\dungeon" -DestinationPath "..\defenders-unity-kaykit-orphan-dungeon-backup.zip"
Compress-Archive -Path "Assets\Models\KayKit\medieval" -DestinationPath "..\defenders-unity-kaykit-orphan-medieval-backup.zip"

# Then delete in Unity (Project window → right-click → Delete) so the .meta files get cleaned up too:
#   Assets/Models/KayKit/dungeon/
#   Assets/Models/KayKit/medieval/
```

Both folders are unreferenced anywhere in `Assets/` (`grep -rlF` of all GUIDs in those folders against `Assets/Scenes`, `Assets/Prefabs`, `Assets/Resources`, `Assets/Editor` returns zero matches). Only `docs/dungeons-3d-unity-layout-spec.md` mentions the bare `dungeon/` path as historical text — that's fine.

---

# Do-not-touch list

Carried over from the diagnosis report. **Do not modify these without an explicit re-scope from Samantha:**

- `Assets/_Modules/Village/VillageController.cs` — skeleton class, wired via reflection by the Editor builder. Rewriting it silently breaks every `BuildVillage` run.
- `WallLayout.Segments` / `Gates` — both builders hard-code half-extents (150 × 120). Change one only with the other.
- `ExteriorRoot` GameObject in `Village.unity` — `BuildExterior` expects to be the sole author of that subtree. Don't manually edit.
- `TerrainBaseDepth = 0.5` (in `ExteriorTerrainBuilder.cs:105`) — documented to prevent hex-tile Z-fighting at Y≈0.015.
- Per-instance color recoloring on dressing materials — breaks instanced batches (comment at `VillageSceneBuilder.cs:3476`).
- Migrating village content to Addressables — defer. Addressables is currently Localization-only. No perf payoff yet.
- The three `.meta` files just locked in by `ba393bb` — these are now canonical. **Never delete and let Unity regenerate them.**

---

# Build verification (after major changes)

If Phase A or any of Phase B touches scenes or code, run the full build chain from `SESSION-RESUME-AFTER-REBOOT.md` to validate the player still builds clean:

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe'
$proj  = 'C:\Users\Kayden-Laptop\Documents\defenders-unity'

# Force Tripo texture re-extract
& $unity -batchmode -quit -projectPath $proj `
    -executeMethod DeNelle.Editor.TripoAssetPostprocessor.ForceReextractAll `
    -logFile (Join-Path $proj 'Builds\tripo-extract.log')

# Wait for full Unity exit + clear stale lockfile
while (Get-Process -Name 'Unity' -ErrorAction SilentlyContinue |
       Where-Object { $_.MainWindowTitle -eq '' }) { Start-Sleep -Milliseconds 500 }
if (Test-Path 'Temp\UnityLockfile') { Remove-Item 'Temp\UnityLockfile' -Force }

# Grant-polish rebuild
& $unity -batchmode -quit -projectPath $proj `
    -executeMethod DeNelle.Editor.GrantPolishBuilder.BuildAll `
    -logFile (Join-Path $proj 'Builds\grant-polish.log')

while (Get-Process -Name 'Unity' -ErrorAction SilentlyContinue |
       Where-Object { $_.MainWindowTitle -eq '' }) { Start-Sleep -Milliseconds 500 }
if (Test-Path 'Temp\UnityLockfile') { Remove-Item 'Temp\UnityLockfile' -Force }

# CLEAN player build — must delete Builds\Windows or exe stub stays stale
if (Test-Path 'Builds\Windows') {
    [System.IO.Directory]::Delete('Builds\Windows', $true)
}
& $unity -batchmode -quit -buildTarget Win64 -projectPath $proj `
    -executeMethod DeNelle.Editor.DesktopBuild.BuildWindows `
    -logFile (Join-Path $proj 'Builds\build-tripo.log')
```

**Verify timestamps match** (per `memory/unity-build-exe-stub-quirk.md`):

```powershell
Get-Item 'Builds\Windows\DefendersOfTheRealm.exe',
         'Builds\Windows\UnityPlayer.dll',
         'Builds\Windows\DefendersOfTheRealm_Data\level3',
         'Builds\Windows\DefendersOfTheRealm_Data\Managed\DeNelle.Village.dll'
```

If `.exe` and `level3` differ by more than build duration, it's the stub-quirk — delete `Builds\Windows` and re-run.

**If batchmode crashes with the license error** (`HandshakeResponse reported an error: ResponseCode: 505 "Unsupported protocol version '1.18.1'"`):
- DO NOT broad-kill Unity processes.
- Open Unity Hub once interactively to refresh the license token, OR
- Restart Windows (full reboot).
- Per `memory/unity-license-kill-caution.md`.

---

# What's already done

- ✅ Pulled `origin/master` (was already up to date).
- ✅ Three investigation agents ran (GUID/.meta, KKit reimport strategy, architecture review).
- ✅ Diagnosis report written: `docs/diagnosis-report.md`.
- ✅ Three untracked `.meta` files locked in by commit `ba393bb` (pushed to origin).
- ⏳ **Phase A Steps 2–6 are next.** Start there.

---

# Reporting back

When Phase A is verified, write a one-paragraph summary into `OVERNIGHT_REPORT.md` (Samantha's preferred briefing file) noting:
- Which symptoms cleared after the repair.
- Any new Console warnings that surfaced.
- Phase B priorities (recommend HUD → Build → Portal → Volume, smallest-risk first).
- Whether Phase C-4 cleanup happened.

Then commit `OVERNIGHT_REPORT.md` and push.
