# Pipeline State — Defenders of the Realm v2 (ground-truth catalog seed)

Compiled by the build-connected CLI from the actual code (2026-05-29), to seed the
PM catalog. Status legend: **BUILT** (works) · **WIRED** (in-scene/connected) ·
**STUB** (class exists, no runtime) · **MISSING** (not started) · **SPEC** (doc only).

> **Reconciliation rule (recurring trap):** several systems are *more built* than their
> specs assume. Always check this doc + memory before writing a WO that "creates" something
> — the store, animation, and monetization were all near-complete already. Reconcile, don't duplicate.

---

## 0. How the pipeline works
- **Roles:** Claude UI = specs / work orders / logic + routing. The **CLI (this side) = writes + build-verifies all code.** Owner = PM, catalogs + routes, makes creative calls.
- **Asset packs:** owner imports Asset Store packs in the Unity editor (CLI can't); CLI integrates. Big packs are **gitignored** (re-import on clone); only the specific used assets are committed.
- **Build/bake (CLI, batchmode — refuse if the editor is open):**
  - `build-windows.ps1` → Win64 player (`Builds/Windows/…exe`); wipes output first (stale-exe crash guard).
  - `run-unity-method.ps1 -Method <X> -LogName <Y>` → runs an editor static method (bakes, animator builds, etc.).
  - Launch a scene directly: `…exe -bootScene <SceneName>` (e.g. `PatriciaLightMode`, `Village`).
  - 505 license line is transient; judge by the success marker, not the exit code.

## 1. Asset packs
| Pack | Location | Git | Use |
|---|---|---|---|
| KayKit Skeletons 1.1 | Assets/Models/KayKit (gitignored) → chars copied to **Resources/Enemies** (committed) | mixed | Enemy horde meshes + URP atlas |
| KayKit Character Animations 1.1 | Assets/Models/KayKit (gitignored) | ignored | Shared Rig_Medium/Large clip library |
| KayKit Medieval Hexagon | Assets/Models/KayKit (gitignored) | ignored | Village dressing + walls/gates/ground |
| Black Dragon | → **Resources/Enemies/Dragon.fbx** (committed) | tracked | DTT air flyer + boss model |
| Lean Touch (CW) | Assets/Plugins/CW (committed, examples trimmed) | tracked | Mobile gestures |
| Low Poly Ultimate Pack (polyperfect) | Assets/polyperfect (**gitignored**, 246MB) | ignored | Arena props (siege engines) + future village rebuild |

## 2. Defend-the-Tower (PatriciaLight) — **BUILT**, the polished pillar
Scene: `Assets/Scenes/PatriciaLightMode.unity` (baked by `Assets/Editor/PatriciaLightSceneBuilder.cs`). Director: `Assets/_Modules/Village/PatriciaLight/PatriciaLightController.cs`.
- Manual aim-assist combat — `TowerAimSystem` (input-agnostic reticle+target) + `LeanTouchAimDriver` (drag=aim, hold=fire, pinch=zoom) + desktop mouse/keyboard fallback. **BUILT.**
- HUD (code-built UIDocument): tower HP bar (top), circular cooldown rings (Painter2D), color-coded damage pops (cyan hero / green pet via `IDamageTintable`), pet Engage/Repair toggle, FX overlays. **BUILT.**
- Abilities resolve at the crosshair; heal/ward repairs the tower (`HeroAbilities.AimPointOverride` + `HealHandler`). **BUILT.**
- Enemies: KayKit skeleton families (HollowRoster: Walker/Warrior/Rogue/Caster/Brute, wave-gated, stat/size/speed-scaled) + Dragon flyers; spread formation; animated via factory. **BUILT.**
- Arena dressed with polyperfect siege engines + warmer ground. **BUILT.**
- Camera: `HeroOverShoulderCamera` (locked facing, pinch-zoom hooks). **BUILT.**

## 3. Enemy + animation pipeline — **BUILT**
- `Assets/Editor/AnimatorSetup.cs` → builds shared controllers (HumanoidEnemy / LargeEnemy / Boss / Hero / Pet / Npc) into `Assets/Generated/Animators/` (gitignored) from the Character Animations 1.1 library. Canonical (docs/enemy-codex.md §5).
- `EnemyAnimatorSetup.cs` (DTT) → skeleton avatars + copies controllers to Resources/Enemies for runtime load.
- `EnemyAnimatorFactory.Apply(mesh, modelName)` → picks the shared controller by rig family.
- `VisualFactory.Skin(host, key, SkinOptions)` → load→fit→seat→fix-materials→strip-colliders (Enemy/Structure/Prop presets).
- `Enemy.cs` already drives Speed/Attack/Hit/Dead → any rigged mesh walks/attacks/dies for free.
- **Apex DragonBoss** (`Assets/_Modules/Village/Enemies/DragonBoss.cs`): own kinematic-flight class (NOT Enemy/NavMesh), 3-phase encounter, baked Fly take + code-driven dives. Prefab: `Assets/Prefabs/Village/Generated/Boss_Dragon.prefab`. **BUILT.**

## 4. Village (the tower-defense town) — **WIRED, with gaps**
Scene: `Assets/Scenes/Village.unity` (⚠ corruption-on-resave history — regenerate ONLY via the builder, never hand-save). Builder: `Assets/Editor/VillageSceneBuilder.cs` (`BuildVillage`).
- Wave loop `Assets/_Modules/Village/Waves/WaveManager.cs`: countdown→spawn→breach/clear→next. **BUILT.** Spawn-spread + stuck-enemy failsafe added (77984d9).
- 5 gameplay buildings on lightweight **polyperfect _M Medieval** prefabs (WO-101 Phase A, 4cf0037) — Tripo meshes shed (Seeker file-size win); store/interactable wiring on the plot root (dispatch by `Building.Type`) untouched. **Rebake VERIFIED loads (no level3 crash); rebake is SAFE.** ✅ Materials URP-converted via `PolyperfectUrpFix` (69 mats built-in→URP/Lit) — render correctly (confirmed). Note: polyperfect is gitignored, so re-run `Defenders/Art/Fix Polyperfect URP Materials` on a fresh clone.
- **WO-101 Phase B** (pending): walls/ground/roads/prop-dressing/nature + MarketplaceInteractor store-wiring (same builder/rebake).
- ✅ **wave-4 apex dragon FIXED** (4cf0037): rebake re-wires `_apexBossPrefab` + WaveManager has a Resources/Enemies/Boss_Dragon fallback for the stale scene. (Pending in-game confirm that Syndrath flies.)

## 5. Store / monetization — **~70% BUILT** (do NOT greenfield)
Specs: `docs/monetization-v2-spec.md` (locked), `WORK_ORDER_73/75`, `CC_MONETIZATION_RECONCILIATION.md`.
- `PackStore.cs` + `PackCatalog.cs` + `packs.json` (5 packs) — **BUILT** (devnet-stubbed payments).
- `MarketplaceInteractor.cs` — village [F] store-open trigger — **BUILT, not placed in scene**.
- `WalletService.cs` (SOL/USDC/SKR, devnet-stub), `GlimmerCurrencyService.cs`, `CosmeticShopPanel.cs` — **BUILT.**
- **MISSING:** scene wiring (Marketplace + store UI into the village), `CosmeticApplier`, `BattlePass` runtime (`BattlePassData.cs` = STUB), glimmer-grant in packs (1-line). UXML render risk in builds.

## 6. Spell book — targeting **BUILT**, creative spells **SPEC**
- Fire/cast now resolve at the crosshair; backward-shots fixed; heal→tower. **BUILT.**
- Creative roster (Rage team-buff, slow-all, DoT zone, fireball-freeze) + the prerequisite "enemies act on slow/freeze" — **SPEC** (`SPELL_BOOK_DESIGN.md`).

## 7. Open backlog (ideas → WOs)
Wave-4 apex fix (#20) · DTT camera look-down + mobile on-screen buttons (#19) · village redesign (polyperfect buildings/walls) · store scene-wiring · compass target-triangles · "village under siege" wave affordance · enemy role behaviors + wave composition (`ENEMY_WAVE_DESIGN.md`) · idle/economy pillar (resource gathering + pet auto-harvest + offline) · air-dragon flap.

## 8. Design docs / roadmaps in-repo
`SPELL_BOOK_DESIGN.md` · `ENEMY_WAVE_DESIGN.md` · `docs/enemy-codex.md` · `docs/monetization-v2-spec.md` · `docs/port-notes/animation-setup.md` · `CC_MONETIZATION_RECONCILIATION.md` · WORK_ORDER_*.md.
