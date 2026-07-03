# WebGL Delivery Plan — 2026-07-03

**Author:** WebGL & Asset Delivery audit session (read-only audit night, 2026-07-02 → 03).
**Mode:** AUDIT + PLAN. No structural Addressables edits were made (tree held a full uncommitted day).
Everything below is **measured from the tree/log/git/live-site**, not guessed.

**GOVERNING LENS (owner): "What CAN stream, SHOULD stream."** Full Addressables is the
**destination architecture**, not a size workaround. The project was deliberately built as thin
JSON catalogs + runtime interpreters (dialogue, vendors, tutorial-steps, building-tiers,
dungeon-kit, `CanonicalJson`/`LocalJsonCatalogSource`) precisely so the boot payload is
**logic + text**, and every binary the catalogs reference **streams**. The plan below therefore
defaults **every binary family to REMOTE STREAMING**; the load-bearing table is the
**JUSTIFIED-LOCAL list** (§3.2) — each local entry must argue its seat in the boot `.data`.
The Vercel 100 MB comfort margin is a **milestone on the way**, not the finish line.

**Companion canon:** `docs/DATA_ARCHITECTURE_DECISION_2026-06-27.md` (3-tier, owner-ratified),
`Assets/Editor/HeroAddressablesGrouper.cs` (WO-545 Tier-1 tooling, already written),
`Assets/_Modules/Core/Addressables/HeroAssetLoader.cs` + `HeroTextureLoader.cs` (code seam, shipped).

---

## 0. LIVE BASELINE (verified via HEAD requests, 2026-07-02 night)

The game **is live on Vercel** (`defenders-of-the-realm-v2.vercel.app`, deployed 07-01 post-audio-slim):

| Payload | Live size | Note |
|---|---|---|
| `*.data.unityweb` | **85.21 MB** | Brotli-compressed monolith — under the 100 MB/file wall but only **~15 MB headroom**; every new hero/enemy/atlas eats into it |
| `*.wasm.unityweb` | 12.91 MB | release IL2CPP, healthy |
| `*.framework.js.unityweb` | 0.08 MB | — |
| `*.loader.js` | 0.11 MB | — |
| `StreamingAssets/aa/WebGL/gear_assets_all_*.bundle` | **14.36 MB** | Addressables streaming **already works in production** |

**Config gap found:** every payload — including content-hash-named files — is served
`Cache-Control: public, max-age=0, must-revalidate`. Hashed immutable files re-validate on every
load. Fix via `vercel.json` headers (§4 quick-win Q1).

A fresh release build was compiling during this audit (`Builds/webgl-build.log`, wasm link stage
837/849 at last poll). Read its final build report + measure the new `.unityweb` payloads when done;
hero textures went back to 2048@q90 on 07-01 (~+2–3 MB expected vs the live 85.21).

---

## 1. SIZE LEDGER (measured from the working tree)

`WebGL.data` is force-fed by **two** Resources roots plus StreamingAssets:

| Source | Size on disk | Files | Ships in |
|---|---|---|---|
| `Assets/Resources/**` | **222.4 MB** | 1,120 | WebGL.data (force-included) |
| `Assets/Audio/Resources/**` (music) | **47.9 MB** | 16 | WebGL.data (Vorbis q0.3 WebGL override → far smaller in-build) |
| `Assets/StreamingAssets/**` | 4.7 MB | 51 | Copied verbatim (canonical JSON + Defenders.mp4) |
| Addressables local bundles (already OUT of .data) | 14.5 MB | 9 | `StreamingAssets/aa/WebGL/` |

### Resources by family (source MB / files) — streaming disposition

| # | Family | MB | Files | Disposition |
|---|---|---|---|---|
| 1 | `Enemies/` | 57.6 | 46 | **STREAM** (Orc_Shaman.fbx 14.5, Orc_Berserker 11.5, Orc_Necromancer 8.4, Dragon 6.1 + Dragon jpgs 8.4) |
| 2 | `Heroes/` | 55.1 | 86 | **STREAM** — tooling already exists (`HeroAddressablesGrouper`). Top: `medievalknight3dmodel_basecolor.PNG` 21.3 source |
| 3 | Music (both Resources roots) | ~51 | 18 | **STREAM** (all but the title/boot loop) |
| 4 | `RpgUi/` | 22.3 | 90 | **SPLIT** — master-frame chrome local (boot UI), per-panel art (talent/quest/crafting frames) streams |
| 5 | `HudIcons/` | 20.9 | 63 | **STREAM eventually** (already WebGL-capped to 128 so in-build cost is small; lowest priority) |
| 6 | `ItemIcons/` | 15.0 | 492 | **STREAM** (pairs with the already-streaming Gear bundle) |
| 7 | `Pets/` | 10.6 | 25 | **STREAM** |
| 8 | `Structures/` | 9.9 | 76 | **STREAM** (Portal.fbx 5.05 + Portal.fbm 1.07) |
| 9 | `Arena/` + `VFX/` + `Walls/` | 13.3 | 54 | **STREAM** |
| 10 | `Talents/` + `Portraits/` + `PetPortraits/` | 8.7 | 82 | **STREAM** (panel-open time, not boot) |

**Oddities flagged:**
- **Double-ship:** `Heroes/Knight.fbm/remesh_12_combined_Bake_Normal.png` (5.22 MB) AND
  `Heroes/Textures/remesh_12_combined_Bake_Normal.png` (5.22) both live under Resources — same for
  `knight_basecolor.JPEG` (3.03 ×2) and Diffuse (1.7 ×2). ~10 MB source duplicated. The grouper
  already handles this correctly (fbm textures become implicit bundle deps).
- `StreamingAssets/Video/Defenders.mp4` = 3.96 MB raw — verify still referenced; else drop.

---

## 2. link.xml VERDICT — LOAD-BEARING, and AUTO-REGENERATED (deletion was benign)

**Evidence:**
1. `git show HEAD:Assets/AddressableAssetsData/link.xml` → preserves
   `UnityEngine.AddressableAssets.Addressables`, the `Unity.ResourceManager` providers
   (`AssetBundleProvider`, `BundledAssetProvider`, `InstanceProvider`, `SceneProvider`),
   Localization table types, and engine types (AnimationClip/Animator/Avatar/
   RuntimeAnimatorController, GameObject/Material/Mesh/Shader/SkinnedMeshRenderer/Texture2D…).
2. `ProjectSettings.asset`: `stripEngineCode: 1` and `managedStrippingLevel: { WebGL: 4 }` = **High**.
   Addressables resolves providers **by serialized class-name strings in `catalog.bin`** — invisible
   to the IL2CPP linker, so without these preserves High stripping strips them → every Addressables
   load (the live 14.36 MB Gear bundle, Localization) dies at runtime. In a full-streaming
   architecture this file is **critical infrastructure**.
3. The "deletion" resolved itself: the file is **back in the working tree, byte-identical to HEAD**
   (only `link.xml.meta` modified). The Addressables content build inside tonight's player build
   **regenerates it** — standard Addressables behavior. The deletion was transient churn, not a
   strip experiment; tonight's build ran WITH the file present.

**Ruling:** keep it tracked; never commit its deletion. If a streamed family loses behavior in a
build, suspect stripping first and add the specific type here — do NOT lower the stripping level.

---

## 3. THE FULL-ADDRESSABLES PLAN (WO-545 → destination architecture)

### 3.0 What already exists (do NOT greenfield)
- **Code seam (shipped):** `HeroAssetLoader.cs` / `HeroTextureLoader.cs` — Addressables-first with
  Resources fallback; **address == Resources key**, so each family flips independently and
  un-migrated content keeps working. Call sites already flipped:
  `HeroBodySwapper.cs:191,272,1021,1093`; Blink base body is pure-Addressables (`HeroBodySwapper.cs:92`).
- **Grouping/migration tool (written):** `Assets/Editor/HeroAddressablesGrouper.cs` — per-hero
  groups + shared `Hero_Textures`, migration target `Assets/HeroContent/` (out of Resources),
  GUID-preserving moves. Headless: `-executeMethod DeNelle.Editor.HeroAddressablesGrouper.GroupAndMigrateHeroes`.
- **Production proof:** the `Gear` bundle streams 14.36 MB in the live deploy today.

### 3.1 Group design — DEFAULT = REMOTE STREAMING

Every binary family becomes an Addressables group whose end-state is the **Remote** path.
"Local" during rollout is a staging posture (same-origin `StreamingAssets/aa/`), flipped to remote
by profile, not by re-authoring:

| Group | Contents | Label | End state |
|---|---|---|---|
| `Hero_<slug>` (×3) + `Hero_Textures` | hero FBX/controllers + shared atlases | `hero` | REMOTE |
| `Enemies_Orcs`, `Enemies_Boss` | Orc_*.fbx, Dragon/Demon + materials + controllers | `enemy` | REMOTE |
| `Pets` | pet FBX/fbm/controllers | `pet` | REMOTE |
| `Music` | all music beyond the title loop | `audio` | REMOTE |
| `Structures`, `Arena_VFX` | Portal, walls, arena kit, VFX packs | `world` | REMOTE |
| `Icons_Items`, `Portraits`, `Talents`, `PanelArt` | item icons (492), portraits, talent art, per-panel RpgUi frames | `ui` | REMOTE |
| `Gear` (live) | gear prefabs | `gear` | REMOTE (flip from local) |
| Dungeon/outpost kit scenes (WO-595) | scene bundles incl. baked lighting | `scene` | REMOTE (born Addressable — never enters Resources) |

### 3.2 THE JUSTIFIED-LOCAL LIST (the key table — everything else streams)

What ships in the boot `.data`, and why it earned the seat:

| Local item | ~Size (in-build) | Justification |
|---|---|---|
| Code (wasm) + interpreters | 12.9 MB wasm | The runtime itself — dialogue runner, vendor/tutorial/building-tier/dungeon-kit interpreters. This IS the product's local half |
| `Resources/Data/**` + `StreamingAssets/Data/Canonical/**` thin catalogs | ~1 MB | The **local catalog seam** (owner-ratified): `CanonicalJson`/`LocalJsonCatalogSource` read synchronously at boot on every platform; catalogs are the map that names every streamed binary — the map must precede the territory |
| Title screen: `Title/Title_L`/`Title_H`, title music loop, `Sfx/UiClick` + mixer | ~2–3 MB | First-frame/first-input feel — must render before any network fetch completes; a blank title while bundles download is a felt-quality failure |
| Blink master-frame UI chrome (`RpgUi` shared frame set) | ~2–4 MB | `BuildObsidianPanel` boot UI — panels must open offline-of-CDN; per-panel ART streams, the frame formula stays |
| Loading/progress + error iconography | <1 MB | The screen that fronts all streaming needs no streaming |
| Procedural SFX fallbacks (`GameSfx` generators) | ~0 (code) | Already the fallback when a clip is absent — the degrade path for `audio` label failures |
| Addressables `catalog.bin` + `settings.json` + `link.xml` preserves | <0.1 MB | The streaming bootstrap itself |
| `DevPanelSettings`, FeatureFlags defaults | trivial | Boot wiring |

Target boot `.data` end-state: **~15–25 MB** (from 85.21 live). Everything not in this table needs
no argument to stream — it needs an argument to stay.

### 3.3 ⚠ The one real blocker: WebGL sync/async gate
`HeroAssetLoader`/`HeroTextureLoader` use `WaitForCompletion`, which **WebGL does not support for a
bundle that still has to download** (documented in the grouper header). Two resolutions — pick ONE
before the first migration:
- **(a) Async-first loaders:** add `LoadHeroPrefabAsync` etc., convert call sites to coroutine/Task flow. Correct end-state.
- **(b) Warm-up manifest:** during the title/loading screen, async-prefetch the addresses the next
  scene's sync path will hit (label-driven: `Addressables.DownloadDependenciesAsync(label)`); sync
  `WaitForCompletion` on an already-cached bundle is safe. Lower churn — recommended for Phase 1,
  with (a) as the Phase 4+ cleanup. In a fully-remote world the warm-up UI (progress per label)
  becomes a permanent, ownable loading experience.

### 3.4 Profiles / load paths (3 distribution targets)

Current: `Local.LoadPath = {Addressables.RuntimePath}/[BuildTarget]`; `Remote.LoadPath = <undefined>`.

| Target | End-state profile | Notes |
|---|---|---|
| Vercel / Pi Browser (primary) | `Remote.LoadPath = https://<cdn>/aa/[BuildTarget]/v<N>` | COEP deliberately OFF on the Pi deploy → cross-origin fetches allowed; CDN must send `Access-Control-Allow-Origin: *` (§5). `BuildRemoteCatalog = true` |
| itch.io (html5 zip) | Local profile build variant | itch cannot reach out reliably from its sandbox unless CORS is right; simplest: itch build keeps groups LOCAL (same content, profile flip). Zip total stays fine |
| Future mobile | Remote (same CDN) | Content updates without store re-submission — the payoff of the remote catalog |

### 3.5 Code seams still on raw `Resources.Load` (flip per family, file:line)

Pattern for every one: clone the proven `HeroAssetLoader` shape (Addressables-first, Resources
fallback, `Guard.Try` + `FlowTrace` per §12).

| Family | Call sites |
|---|---|
| Enemies | `Assets/_Modules/BattleATB/AtbCombatantSwapper.cs:300,419,574`; `Assets/_Modules/Village/VisualFactory.cs:74` (generic prefab resolver — flipping this one seam covers every VisualFactory consumer) |
| Pets | `Assets/_Modules/Pets/PetDeployer.cs:605,616,690–692,754,893–896` (prefab, cosmetic skin, controllers, `LoadAll` clips, portraits) |
| Portraits | `Assets/_Modules/DialogueUI/PortraitCache.cs:53–56`; `Onboarding/HeroSelectController.cs:834,841`; `PetSelectController.cs:490`; `TitleController.cs:914,922` |
| Music | `Assets/_Modules/Audio/AudioBootstrap.cs:69,88,177,197`; `Village/Audio/BattleMusicManager.cs:436,568` |
| Intro/story art | `Onboarding/StoryIntroController.cs:178`; `TitleController.cs:659`; `DialogueUI/IntroSequencePlayer.cs:363` |
| HUD icons | `BattleATB/BattleHudUgui.cs:60`; `HUD/AdminOverlay.cs:323` |
| STAYS Resources (justified-local) | `Core/Data/CanonicalJson.cs`, `LocalJsonCatalogSource.cs` (catalog TextAssets), `GameSfx.cs` procedural fallbacks, UiClick, mixer, `TitleController.cs:267` title art |

### 3.6 Content update / versioning
- Bundles are already content-hash named (`gear_assets_all_<hash>.bundle`) → immutable-cacheable.
- `addressables_content_state.bin` exists per-target under `Assets/AddressableAssetsData/<target>/` — keep committing it.
- Policy: **full rebuild + versioned remote path** (`/aa/WebGL/v<N>/`) per release train (matches
  the owner's pre-release→release-train model) rather than Addressables incremental update —
  atomic, simple, old versions stay up until the train retires them.

### 3.7 Phases — each behind a regression gate, ending at FULL Addressables

| Phase | Work | Gate | Milestone |
|---|---|---|---|
| 0 | Read tonight's build report; measure new `.unityweb` sizes; apply §4 quick wins (cache headers) | deploy succeeds; sizes logged | baseline locked |
| 1 | Sync/async gate (warm-up manifest) + `GroupAndMigrateHeroes` (heroes out of Resources, local bundles) | CompileGate + AutoPilot fleet + textured-knight oracle | `.data` −40–50 MB → **100 MB wall comfortably cleared** (the milestone, not the finish) |
| 2 | Enemies + Pets + Structures groups; flip `AtbCombatantSwapper`/`PetDeployer`/`VisualFactory` | fleet + orc-family-animated oracle | `.data` ≈ 35–45 MB |
| 3 | Music + Icons/Portraits/Talents/PanelArt; flip audio + portrait call sites | fleet + music-plays + panel screenshots | `.data` ≈ 25–30 MB |
| 4 | **Remote flip:** stand up CDN (§5), set `Remote.LoadPath`, `BuildRemoteCatalog=true`, move all labeled groups remote; flip `Gear` too; async-first loader cleanup (3.3a) | end-to-end on Pi Browser via pinet.com + cache-header verification + fleet | boot `.data` = justified-local only (~15–25 MB); **full streaming architecture live** |
| 5 | New content born Addressable (dungeon-kit scenes WO-595, future heroes/packs) — Resources becomes closed to binaries | recipe-level check in content WOs | destination reached |

---

## 4. QUICK WINS — audit result: import-settings lever ALREADY PULLED; one config win found

Verified per-meta / per-file tonight:
- **Texture caps:** WebGL platform overrides present on every top offender — hero basecolor
  21.3 MB source → **1024 + crunch**; HudIcons → **128**; RpgUi panels → **512**; Dragon/enemy
  jpgs → **512**; pet fbm → **512** (WO-191/211/342 + `WebGLTextureOptimizer` did the work).
- **Audio:** WebGL override Vorbis **q0.3**, `preloadAudioData: 0` (verified `mainworld1_NEW.mp3.meta`);
  the 07-01 DecompressOnLoad fix is in (`WebGLAudioSlim.cs`).
- **Brotli:** enforced in code — `Assets/Editor/WebGLBuild.cs:89–90` (`Brotli` +
  `decompressionFallback = true`, `-noBrotli` deprecated); ProjectSettings agrees
  (`webGLCompressionFormat: 0`, `webGLDecompressionFallback: 1`). Payloads are `.unityweb`
  (no Content-Encoding header needed) — itch- and Vercel-safe. Live proof: 85.21 MB data payload.
- **Stripping:** IL2CPP High + engine stripping on; URP prefiltering flags broadly enabled;
  Lightmap/Fog stripping Automatic. Sane — leave alone.

**Applied tonight: NOTHING in Assets/** — a release build was in flight (import-setting edits would
invalidate it), and every candidate was already optimized. The remaining fat is structural
(Resources force-inclusion) = exactly §3.

**Phase-0 quick wins (next session, low-risk, each justified):**
1. **Q1 — cache headers (`vercel.json`, no Unity involvement):** content-hash-named files
   (`*.unityweb` with hash prefix, `aa/**/*_<hash>.bundle`) → `Cache-Control: public,
   max-age=31536000, immutable`; keep `index.html` `no-store` (stale-COEP lesson) and
   `catalog.*`/`settings.json` `no-cache`. Live site currently revalidates EVERYTHING (`max-age=0`) —
   free repeat-load win, zero gameplay risk.
2. Verify/drop `StreamingAssets/Video/Defenders.mp4` (3.96 MB) if unreferenced.
3. Hero fbm/atlas double-ship (~10 MB source) — solved for free by Phase 1 migration; do not hand-fix.
4. Sprite-atlas the 492 ItemIcons when they become the `Icons_Items` group (memory + draw calls).
5. Re-check `webGLMemorySize: 512` for Pi mobile once `.data` shrinks (candidate 384).

---

## 5. CDN ARCHITECTURE for remote bundles (Phase 4)

**Constraints:** Pi deploy has **COEP removed** (deliberate — pi-sdk.js unblock) → no cross-origin
isolation, single-threaded WebGL (`webGLThreadsSupport: 0`), and cross-origin fetches ARE permitted
(no CORP requirement). Backend rail = Vercel functions + Neon. Cloudflare is canon-listed as the Pi
backend host candidate. Vercel static hosting caps at 100 MB/file (the original wall) — fine for
split bundles, wrong economics for a growing asset library.

**Recommendation:**
1. **Phase 1–3 (staging posture):** bundles ride same-origin under the Vercel deploy
   (`StreamingAssets/aa/`) — zero new infrastructure, already proven by the live Gear bundle.
2. **Phase 4 origin: Cloudflare R2 + Cloudflare CDN.** Zero egress fees (bundles are the
   egress-heavy class), no per-file cap, custom domain (`cdn.<game-domain>`; r2.dev URL for the
   testbed), and it aligns with Cloudflare-as-Pi-backend canon. Alternative if consolidating on
   Vercel: **Vercel Blob** (no 100 MB static cap, same project/env) — acceptable, costlier egress.
   Do not use Vercel static hosting for the remote bundle store.
3. **Headers on the bundle host:**
   ```
   Access-Control-Allow-Origin: *                          (UnityWebRequest CORS; sufficient with COEP off)
   Cache-Control: public, max-age=31536000, immutable      -> *.bundle (content-hash named)
   Cache-Control: no-cache                                  -> catalog_*.{json,bin,hash}, settings.json
   ```
   Do NOT introduce any `Cross-Origin-Resource-Policy` requirement anywhere — the game page must
   keep running without COEP while pi-sdk.js is cross-origin.
4. **Versioned layout:** `https://<cdn>/aa/WebGL/v<N>/…`; `Remote.LoadPath` carries the version.
   Old versions stay up until the release train retires them — in-flight clients never break.
5. **Pi specifics:** pinet.com fronts the Vercel app; bundle fetches originate from the game origin,
   so the CORS grant above covers them. Keep `no-store` on `index.html`.

---

## 6. LIGHTING FOR WEBGL (dungeon/outpost kit build-out, WO-595) — recommendations only

**Current URP asset audit (`Assets/Settings/DeNelle-URP.asset`, verified):** HDR OFF, MSAA 2x,
render scale 1, main light per-pixel with 1024 shadowmap / 1 cascade / 30 m distance, additional
lights **per-vertex** (limit 4, no shadows), soft shadows low, SRP batcher ON, reflection probe
blending OFF. A well-tuned WebGL/mobile profile — keep as baseline.

1. **Bake, don't realtime.** Kit geometry is static and this WebGL is single-threaded (COEP off →
   no SharedArrayBuffer). Baked lightmaps + Light Probes for the kit; `Mixed → Subtractive` for the
   one main light if dynamic characters need it, else fully Baked + probes.
2. Torches/crystals = baked emissive + per-vertex additional point lights, no shadows — the current
   asset already enforces exactly this.
3. Lightmap budget ~1–2 × 1024 per dungeon scene, compressed; `m_LightmapStripping: Automatic`
   already strips unused modes.
4. **Landmine (canon, repeat):** lighting/lightmap `.asset` files are binary — the `.gitattributes`
   binary rules force-fixed 06-30 must keep covering them; never git-restore a baked artifact,
   **re-bake** (memory `gitattributes-binary-asset-eol-corruption`). Do not touch those rules.
5. Bakes are batchmode jobs behind the single Unity gate (CLAUDE.md §3) — per-scene bake WOs, editor closed.
6. **Addressables interaction (streaming lens):** a kit scene packed as a `scene`-label bundle
   carries its lightmaps in the bundle — bake **before** grouping, and re-run the Addressables build
   after any re-bake. Dungeon scenes should be **born Addressable** (never pass through Resources).

---

## RESUME POINTER (next session, clean tree)
1. Read `Builds/webgl-build.log` final build report; measure new `.unityweb` payloads vs the live
   85.21 MB baseline; apply quick-win Q1 (vercel.json cache headers) with the deploy.
2. WO-545 Phase 1 = warm-up manifest + `GroupAndMigrateHeroes` + rebuild + fleet gate.
3. Never commit a `link.xml` deletion — the Addressables build regenerates it and High stripping
   depends on it (§2).
