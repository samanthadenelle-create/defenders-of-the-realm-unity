# WORK ORDER 408 — Texture ship-vs-source audit + proposed cap plan

**Status: SPEC / OWNER EYEBALL** · Lane 10 Build/Perf · P0 · 2026-06-13 · By: CLI
**Goal:** WebGL build payload 223 MB → **< 60 MB**, without degrading hero/world/UI fidelity.
**Method:** read-only static audit (dimensions parsed from file headers) — to be cross-checked
against the in-flight build's BuildReport for ground truth on the CHECK rows.

> **Teaching note — file size ≠ build size.** The MB below are raw source-file sizes on disk. In
> the build each texture is imported+compressed (ASTC/DXT/Crunch) at its `maxTextureSize`. The
> lever is the **maxTextureSize cap**: 4096→1024 = **16× fewer pixels** = ~16× smaller in-build.
> So this table ranks *where the cap pays off*, not literal build bytes. An 8192² sheet is ~21 MB
> even ASTC-compressed — those are the scary ones.

## Totals (files ≥ 0.5 MB on disk)
| Class | Size | Files | Meaning |
|---|---|---|---|
| **SHIP** (Resources/ + StreamingAssets) | **175 MB** | 30 | ALWAYS in build — guaranteed lever |
| **CHECK** (scene-ref possible) | **1193 MB** | 333 | ships *iff* referenced by a built scene/prefab — verify vs build report |
| **SOURCE** (PSD / demo / sample) | 379 MB | 126 | editor-only, does NOT ship — repo bloat, not payload (defer) |

---

## A. SHIP — guaranteed in build (do these first; all safe)
| Cur | Dim | File | Proposed cap | Note |
|---|---|---|---|---|
| 24.8 | 4096² | `Resources/Textures/Cathedral.png` | **1024** | structure base-color |
| 23.3 | 4096² | `Resources/Heroes/Textures/fantasywizard…basecolor` | **1024** | hero skin |
| 21.3 | 4096² | `Resources/Textures/Knight.png` | **DEDUP** | == knight basecolor below (same 4096²/21.3MB) |
| 21.3 | 4096² | `Resources/Heroes/Textures/medievalknight…basecolor` | **1024** | hero skin (canonical copy) |
| 16.9 | 4096² | `Resources/Heroes/Textures/Archer_basecolor` | **DEDUP** | == Ranger.fbm/archer_basecolor (same) |
| 16.9 | 4096² | `Resources/Heroes/Ranger.fbm/archer_basecolor` | **1024** | hero skin (keep one) |
| 5.2 | 1024² | `Heroes/Textures/remesh_12…Normal` + `Knight.fbm/…Normal` | **DEDUP→1024** | doubled across Textures/ & .fbm/ |
| 4.8 | 4096² | `Resources/Enemies/Materials/Dragon_Bump_Col2.jpg` | **1024** | dragon base |
| 3.5 | 4096² | `Resources/Enemies/Materials/Dragon_Nor_mirror2.jpg` | **1024** | dragon normal |
| 3.1×4 | 4096² | witch/knight `.JPEG` basecolors (Textures/ **＋** .fbm/ dup) | **DEDUP→1024** | each exists twice |
| 1.9 | 1254² | `Resources/HudIcons/hud_quest.png`, `Elarion.png` | **512** | HUD icon — 1254² is overkill |
| 1.1 | 2048² | `Structures/Portal.fbm/Portal_…basecolor` | **1024** | portal |

**Sub-result:** the 6 hero/structure 4096² base-colors (≈124 MB raw) → 1024 + dedup of the doubled
hero textures and redundant `.fbm/` extractions is the single biggest *guaranteed* win. The `.fbm`
folders are FBX-embedded texture dumps; if the material binds the `Textures/` copy, the `.fbm` copy
is dead ship-weight (confirm per material).

## B. CHECK — ships only if referenced (verify vs build report — likely HUGE)
The **Mirza Beig Ultimate VFX** spritesheets are **8192×8192** — absurd for mobile/WebGL. If any
shipped spell/explosion prefab references them, they dominate the payload (one 8192² ASTC ≈ 21 MB):
| Cur | Dim | File group | Proposed cap |
|---|---|---|---|
| 63 / 42 / 27 / 26 / 25 / 22 / 22… | **8192²** | `Mirza Beig/…/Spritesheets/*explosion*, *smokeWisps*, *solarFlare*, *blob*, *liquid*` (≈15 files) | **2048** (or 1024) — 16–64× |
| 19 / 19 / 12 / 12 / 11… | 4096² | `Mirza Beig/…/*realisticFire*, *swirl*, *fire*, *Explosion*` | **2048** |
| 19.4 / 15.3 / 14.2 / 13.3 | 2048² | `Art/Towers/VikingWatchTower/textures/*` (tower base+normal) | **1024** (ships — towers in scenes) |
| 12.5 ×2 | 4096² | `Quaternius/Medieval Village MegaKit/…/T_BrushedNoise` **＋** `Medieval Village/Textures/T_BrushedNoise` (DUP across 2 folders) | **1024 + dedup** |
| 12.0 | 2048² | `Spells Pack/Particles/Models/Rock 3_Normal` | **1024** |

**Action:** the running build's BuildReport ("Used Assets, sorted by uncompressed size") is the
authoritative list — I'll diff this CHECK set against it the moment the build lands, so we cap only
what actually ships and don't touch unreferenced VFX (keeps import times sane).

## C. SOURCE — does NOT ship (defer to repo-hygiene, NOT this P0)
`Tech hud elements/Psd/Rpg kit.psd` (93 MB), `Action/simple_walk/*` demo CC normals (25 MB each),
`*/Demo Scene.unity`. Repo/clone bloat only — gitignore later; zero build-payload impact.

> **Pack-preservation guard (from WO-437/438):** the `Tech hud elements/Sprites/` UI sprites are
> load-bearing for the combat+global HUD reskin (RpgUiCatalog → Resources/RpgUi/<role>/). The
> optimizer must cap UI sprites **conservatively** (≤512 only where safe) — never blanket-cap them
> with the 3D textures, or the HUD skin goes blurry.

---

## Execution plan (once the WebGL build frees Unity)
1. Diff CHECK rows vs BuildReport → final ship list.
2. Run `TextureBatchOptimizer` (a87c4a6 fixed its WebGL/"Web" platform-name bug) with per-role caps:
   3D base-color/normal → 1024; VFX sheets → 2048; HUD icons → 512; **UI sprites untouched/≤512 safe**.
3. Dedup the doubled hero textures + redundant `.fbm/` extractions (rebind materials to one copy).
4. Rebuild WebGL → measure payload. Target < 60 MB.
5. **Owner verifies** hero/world/VFX/HUD look right in the editor (you modify ← CLI; I verify ← owner).

**Reusable tool:** `tex_audit.py` (repo root) regenerates this audit headlessly anytime.

---

## ⚠️ BUILD REPORT GROUND TRUTH (2026-06-13) — SUPERSEDES the disk audit above

The 200 MB WebGL BuildReport ("Used Assets, sorted by uncompressed size") **overturned the
static disk audit.** Lesson: *source file size ≠ build size.* The disk audit's top suspects were
already-solved or non-shipping:
- **Hero/enemy base-colors (4096², 17–24 MB on disk) ship at ~170 KB** — `TripoTextureImportCap`
  already caps them. **Red herring.**
- **8192² Mirza Beig VFX sheets do NOT ship** (not referenced by built prefabs). **Ignore.**

**Actual build (347 MB uncompressed → 200 MB):**
| Category | In-build | Real contents |
|---|---|---|
| **Meshes** | **115 MB (33%)** | Tripo/AccuRIG FBXs ship **uncompressed** — OgreMage 24.9 · Troll 24.8 · Knight 22.5 · Mage 5.2 · Cleric 4.8 · Ranger 4.7 · Dragon/Orcs 1–2 ea |
| **Textures** | **169 MB (48.7%)** | ~100 MB is **Quaternius MegaKit** material maps (8 normals @5.3 + ~20 base/rough/ORM @2.7, all 2048²); ItemIcons 8×2.6; heart-wing 5.3; Yarn dialogue sprites 3.4×2 |
| **Sounds** | 47 MB (13.7%) | battle/world mp3s — lower-priority lever |

**Executed (safe pass, owner-approved 2026-06-13) — `Assets/Editor/WebGLSizePass408.cs`:**
1. Mesh `Compression.Medium` + `OptimizationFlags.Everything` + `isReadable=false` on shipped FBXs
   (`Resources/Enemies|Heroes|Structures|Walls`). Reversible; owner verifies skinned characters.
2. Quaternius MegaKit + ItemIcons/ProjectileIcons textures 2048→1024 (default cap = all platforms;
   sidesteps the "WebGL"/"Web" platform-name trap).
**Projected:** 200 → ~90–110 MB. Sub-60 likely needs Tripo **mesh decimation** (separate task) +
audio bitrate pass. Run → rebuild → measure → owner visual verify.
