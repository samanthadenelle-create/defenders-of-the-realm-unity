<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 191 — WebGL Build Size Optimization

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Lane:** Build/Assets — CLI (asset import settings + build config). Mostly editor/import work; some needs a rebuild.
**Source:** owner — "optimize and make lighter." Architect assessment 2026-05-31.
**Priority:** P1 — 186 MB is too heavy for fast web loads / Vercel.

## The picture
Build = 186 MB total, **181 MB is `WebGL.data.br`**. Only `Title.unity` ships, so the .data is almost
entirely **`Assets/Resources/**`** (537 MB raw) — Unity force-includes all Resources whole. Two root causes:
everything lives in Resources (nothing can be deferred) + textures use ASTC but **crunch compression is OFF**
so they barely shrink under Brotli.

## GUIDING PRINCIPLE (owner 2026-05-31): SHIP ONLY ASSETS THE GAME ACTUALLY USES
The fastest, safest, biggest win is to **stop shipping unused assets**. Anything in `Resources/` with no
reference path ships dead weight. Phase 0 below makes "only used assets get uploaded" a hard rule before any
re-compression. Long-term, Addressables (Phase 2) enforces this structurally — only referenced/loaded assets
bundle.

## PHASE 0 — Unused-asset audit (DO FIRST — ship only what's referenced)
1. **Read the Unity Build Report** (Editor.log / `Builds/webgl-build.log` build-report section) — it
   enumerates every asset actually included in the .data with its size. This is the authoritative "what
   shipped" list. Sort by size.
2. For each large included asset, check it's actually reachable: grep code for `Resources.Load("<path>")`
   and check prefab/scene references (AssetDatabase dependency / "Find References In Scene"). Flag anything
   with **zero references** (the 92 MB `pet-aether-twilight.fbx` is the known one — find the rest).
3. **Remove unreferenced assets from `Resources/`** (delete or relocate out of any `Resources/` folder so
   they're not force-included). Keep a list in the RESULT of what was pulled + why.
4. Watch the **reflection-based Cosmetics path** — some assets may be loaded by constructed string paths
   (not literal `Resources.Load("x")`); confirm those before removing, so you don't strip a live asset.
5. Re-measure. Expect the orphans alone to drop tens of MB.

### Phase 0 — CONFIRMED VERDICTS (CLI reference audit 2026-05-31, owner-approved)

**REMOVE NOW (zero/no live references — execute, gate on green build):** ~140 MB raw
- `Resources/Cosmetics/Pets/pet-aether-twilight.fbx` (91 MB) — 0 references anywhere (code/prefab/scene). Pure orphan.
- `Resources/Enemy/` (~28 MB, the orc/necromancer hero-test) — code loads `"Enemies/"` (the real folder: EnemyAnimatorFactory, WaveManager → `Enemies/Boss_Dragon`). `Enemy/` is stale leftover, loaded by nothing.
- `Resources/CC5Hero/` (21 MB) — only ref is `Editor/CC5ExtractTex.cs`, a scratch texture-extract script (itself clutter). Nothing in-game uses it. **Also delete `Editor/CC5ExtractTex.cs`.**

**REMOVE + CLEAN STALE REFS (owner 2026-05-31: "cathedral removed long ago"):**
- `Resources/Textures/Cathedral.png` (25 MB) — the cathedral was **replaced by the Tree of Life** as the centerpiece (owner 2026-05-31). The live center structure is `Resources/Structures/tree_of_life.fbx` (KEEP — has its own mesh/material, does NOT use Cathedral.png). The remaining **23 Cathedral refs** (incl. structure builder) are **vestigial from the cathedral→Tree-of-Life swap**. **Do NOT blind-delete the PNG** — trace the refs, confirm the structure builder now places `tree_of_life` (not the cathedral), strip the dead cathedral path + the texture together, then green-build and **verify the Tree of Life still renders at center**. Net ~25 MB.
  - *Canon note:* the Tree of Life = the Heart of Elarion = the upgradeable centerpiece ("Town Hall" seat) from `DESIGN_CORE_LOOP_AND_STRUCTURE.md`. The thing you grow/defend is the world tree.

**DEDUPE (careful, not blind-delete):**
- The `.fbm/` folders are Unity auto-extracts on FBX import — **regenerable**. The copy the code actually loads is `Textures/<name>` (`ApplyExtractedTexture` → `"Textures/Ranger"`, `"Textures/Knight"`). → Dedupe to the `Textures/` copy and **gitignore the `.fbm` folders** (they rebuild on import).
- Top-level `Textures/` (85 MB): `Knight.png` + `Ranger.png` are USED by heroes → KEEP. `Cathedral/flame-pup/ice-wolf/aether` copies → per-file check (likely dups of `Pets/.fbm`).

**KEEP + COMPRESS only (all referenced — Phase 1, not removal):**
- All 3 pets (flame-pup/aether-sprite/ice-wolf = 16/11/14 refs) and all 3 heroes (wizard/knight/archer) are wired. No removal — crunch textures + downsize per Phase 1.

**Gate the three removals:** after deleting, compile + build green to prove nothing breaks; if green, commit `chore: remove orphaned/unused Resources (~140 MB raw — twilight cosmetic, Enemy test, CC5Hero)`. Then re-measure `.data.br`.

## PHASE 1 — Quick wins (do first; ~90–130 MB compressed savings; hours)
Do in this order, re-measure after each:

1. **Delete the orphaned cosmetic FBX — biggest single win (~25–35 MB).**
   `Assets/Resources/Cosmetics/Pets/pet-aether-twilight.fbx` is **92 MB raw with ZERO references** in code
   or prefabs (force-shipped dead weight). **Verify no `Resources.Load("Cosmetics/...")` path loads it**
   (architect found none; double-check the reflection-based Cosmetics path), then remove it from Resources
   (delete or move out of any `Resources/` folder).
2. **Audio reimport (~12–18 MB).** All `Assets/Audio/Resources/` MP3s are quality=100%, stereo. Set:
   Vorbis, quality ~40–50%, **Force To Mono** (music can stay stereo if it matters — test), **Load In
   Background**, no preload. Apply via `.mp3.meta` import settings.
3. **Texture crunch compression (~30–50 MB).** Enable `crunchedCompression: 1` (quality ~50) on the big
   Resources hero/pet/structure basecolor textures (wizard 24M, Cathedral 25M, knight 22M, etc.).
4. **Max texture size 2048 → 1024 (~20–30 MB)** on character basecolors — 2048 is overkill for web.
5. **Managed stripping Minimal → High** in `Assets/Editor/WebGLBuild.cs` (~line 57) — **verify `link.xml`
   covers the Cosmetics reflection path** so stripping doesn't break runtime reflection. Test boot after.
6. **Remove vendor demo Resources** (TMP Examples & Extras/Resources, any pack demo Resources) (~2–5 MB).

**Phase 1 target: total ~70–95 MB** (≈50% off), low risk. Rebuild (WO-190 path) + re-measure + smoke-test.

## PHASE 2 — Structural: Addressables streaming (days; the real "light" win)
`docs/addressables-implementation-plan.md` is implementation-ready and the AddressableAssetSettings groups
exist, but runtime loading is **not wired** (no `Addressables.LoadAssetAsync`/`AssetReference` in `_Modules`).
- Move Pets, Heroes, Towers, VFX, Dungeons, Music **out of Resources into Remote on-demand bundles** (per the
  plan's group table), served from CDN. Convert `Resources.Load` call sites to Addressables handles; test
  memory release on unload.
- **Initial download drops to ~15–25 MB** (Core-Essential + UI-Core + Title + wasm); the rest streams on
  village/battle/dungeon entry and is CDN-cached. New cosmetics ship via catalog update, no rebuild.
- Effort: ~3–5 days.

## Targets
- Phase 1 only: **~70–95 MB total**.
- Phase 1 + 2: **initial download ~15–25 MB**, remainder streamed on demand.

## Acceptance
- Each Phase 1 step's saving logged (before/after .data.br size).
- Game still boots green + plays after stripping/import changes (smoke-test: title → village, pets/heroes
  render correctly, audio plays, no missing-asset errors).
- No visual regression beyond acceptable compression (spot-check hero/pet/structure textures).
- Phase 2 (if done): initial download measured; on-demand content loads on scene entry without errors.

## Gate
Build green; smoke-test; commit per step where sensible; `WORK_ORDER_191_webgl_size_optimization.RESULT.md`
with before/after sizes per step. Coordinate with WO-190 (the rebuild produces the measurement build).

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `AddressableAssetsData/AssetGroups; WebGLPlayerSettingsConfigurator.cs` — addressables split. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
