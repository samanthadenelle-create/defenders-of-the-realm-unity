# Character Packs SME — Black Dragon · Supercyan · Models/People · Models/Pet (+ Cathedral, CastleGate)

**Date:** 2026-07-12 (overnight SME research session)
**Scope:** `Assets\Black Dragon\`, `Assets\Supercyan\`, `Assets\Models\People\`, `Assets\Models\Pet\`, plus a brief pass on `Assets\Models\Cathedral\` and `Assets\Models\CastleGate\`.
**Method:** every claim below is verified from the working tree (file listings, `.fbx.meta` import settings, JSON metadata inside the packs) and from code greps of `Assets/_Modules` + `Assets/Editor` — not from comments alone. Web provenance was researched and cited per pack. Cross-referenced against the owner's purchase ledger `docs/SME/ASSET_STORE_LEDGER_2026-07-12.md`.

---

## Table of contents

1. [Black Dragon](#1-black-dragon)
2. [Supercyan — Character Pack: Fantasy RPG v3.0.0](#2-supercyan--character-pack-fantasy-rpg-v300)
3. [Models/People — CGTrader NPC set + Reallusion FighterClass LODs](#3-modelspeople)
4. [Models/Pet — Tripo pet husk (live meshes moved to Resources/Pets)](#4-modelspet)
5. [Models/Cathedral and Models/CastleGate (brief)](#5-modelscathedral-and-modelscastlegate-brief)
6. [Rig compatibility audit — all packs at a glance](#6-rig-compatibility-audit--all-packs-at-a-glance)
7. [Opportunities + gaps](#7-opportunities--gaps)
8. [Executive summary](#8-executive-summary)

---

## 1. Black Dragon

### 1.1 Identity + inventory

**Not an Asset Store purchase and not Tripo-generated.** The single FBX's exact filename — `Dragon_Baked_Actions_fbx_7.4_binary.fbx` — identifies it as the free **"Black Dragon Rigged and Game Ready"** by **Dennis Haupt (3DHaupt / Sketchfab handle dennish2010)**, distributed on Free3D, Sketchfab, CGTrader, Blendswap and the author's own site. The `_fbx_7.4_binary` suffix is Blender's FBX 7.4 binary exporter naming; the author added the "baked actions" FBX to the package specifically because users reported animation-import problems in Unity. This matches the ledger's note that Black Dragon is *not* on the store ledger.

- Sketchfab: https://sketchfab.com/3d-models/black-dragon-with-idle-animation-fb0053a2e59b43868e934c239bf4eb36
- Free3D: https://free3d.com/3d-model/black-dragon-rigged-and-game-ready-92023.html
- Author's site: https://3dhaupt.com/black-dragon-rigged-and-game-ready-download/
- CGTrader (free = editorial license; paid = commercial): https://www.cgtrader.com/free-3d-models/animal/dinosaur/dragon-rigged-and-game-ready

**⚠ LICENSE FLAG (the single most important finding in this dossier).** Every free distribution of this model is **non-commercial**: the Sketchfab download is CC Attribution-**NonCommercial** (confirmed via the Sketchfab API record), the Free3D download is personal/editorial-use only, and the CGTrader free listing is Editorial. A commercial release (Pi hackathon store build) requires either the **paid CGTrader license** or a **replacement dragon**. This needs an owner decision before ship.

**On disk** (`Assets/Black Dragon/`): one FBX + `Materials/` folder. Nothing else.

| Property | Value (verified) |
|---|---|
| Mesh | ~4,293 quad polys (author's spec); ~38k tris as triangulated |
| Rig | Custom Blender dragon armature (non-humanoid). Unity import: **Generic** (`animationType: 2`), avatar created from this model (`avatarSetup: 1`) — verified in `Dragon_Baked_Actions_fbx_7.4_binary.fbx.meta` |
| Animation | Four baked takes, auto-split by Unity (`clipAnimations: []` in the meta): **Fly_New, Idel_New (sic — the misspelling is in the file), Run_New, Walk_New**. **No Attack take, no Death take.** (Take names per the code canon in `DragonBoss.cs`/`DragonAnimatorSetup.cs`; the author's page also lists Jump and Open Wings, which are not in our baked-actions import.) |
| Materials | FbxSurfacePhong at source — unrenderable in URP; fixed at runtime by `TripoMaterialFixer` (see 1.2) |

### 1.2 How WE consume it — the apex wave-boss, confirmed

The Black Dragon is **"Syndrath the Devourer"**, the apex flying wave-boss of the Elarion village defense — confirmed in code, not just docs:

- `Assets/_Modules/Village/Enemies/DragonBoss.cs:2-39` — the boss MonoBehaviour. Header canon: rigged Generic dragon, four takes, no attack/death clips, so the encounter is **code-driven**: the dragon stays on the Fly clip; Attack and Death are realised as movement + VFX beats. It implements `DeNelle.Core.Combat.IDamageable` **directly** (no `EnemyDamageable` adapter) so hero abilities and the isolated Pets module can damage it across the Core seam. It flies **kinematically** — no NavMeshAgent, no Rigidbody — anchored on the Heart. HP-gated phases: Circling (100–60%), Stooping (60–25%), LastWing (25–0%), then a spiralling code-driven fall on death (`DragonPhase` enum, `DragonBoss.cs:52-62`).
- `Assets/Editor/DragonAnimatorSetup.cs:75-96` — builds `Assets/Generated/Animators/Dragon.controller` (states **Fly** = default, **Idle** ← "Idel_New", **Attack** reuses the Fly clip, **Death** reuses Idle as placeholder; parameters `Speed` float / `Attack` trigger / `Dead` bool) and assembles `Assets/Prefabs/Village/Generated/Boss_Dragon.prefab` (FBX visual + trigger CapsuleCollider on enemy layer 8 + `DragonBoss` added **by reflection** — DeNelle.Editor takes no compile-time dependency on DeNelle.Village). Clip matching is keyword-based and tolerant of the `Armature|` prefix and the "Idel" misspelling (`DragonAnimatorSetup.cs:137-140`).
- `Assets/_Modules/Village/Waves/WaveManager.cs:106-109` — `_apexBossPrefab` (Boss_Dragon) is spawned for any wave whose `waves.json` entry declares an `apexBoss` object; `WaveData.cs:281,320-323` (`ApexBossDef`, `IsApexBossWave`).
- **The wave data confirms the apex placement:** `Assets/Resources/Data/Canonical/waves.json` (mirrored in StreamingAssets) has **20 waves**; exactly one — the terminal wave **"The Last Wing"** — declares `"apexBoss": {"id": "boss-dragon-syndrath", "hp": 4200, "nameKey": "bossSyndrath"}`. So yes: the Black Dragon is the wave-20 apex dragon.
- Ecosystem around the boss (all verified consumers): `BossHealthBar.cs:109` (finds the live `DragonBoss`), `TowerCombat.cs:242,293` and `HeroAbilities.cs:1591` (target `WaveManager.LiveApexBoss` — the boss is NOT in the OverlapSphere enemy set), `BattleMusicManager.cs:242-258` (`OnApexBossSpawned` scoring), `DragonCinematicFlyby.cs` (ambient fly-by cameos that stand down while the real boss is alive, lines 21, 246), `TownsfolkDialogue.cs:95-106` + `AmbientNPC.cs:610-613` (NPC dread-dialogue tiers keyed to how near the dragon wave is), `DevPanelController.cs:32`, and editor-side `DragonPreview.cs:17`.
- Rendering: the FBX's Phong materials are rebuilt to URP/Lit at runtime by `Assets/_Modules/Core/TripoMaterialFixer.cs` — its header (lines 7-8) lists "dragon" among the FBXs it exists for.

**Two canon inconsistencies found (flag, don't fix here):**
1. `TownsfolkDialogue.cs:106` — `public const int DragonWaveId = 4;` — the dialogue default says the dragon arrives on wave 4, but the canonical `waves.json` puts the apexBoss on wave 20 of 20. Either the constant is stale from an earlier 4-wave loop or nothing passes the real wave id. Worth one verification pass on `TierForWave` call sites.
2. `TripoMaterialFixer.cs:7-8` mis-attributes the dragon (and the CastleGate tower) as "Tripo AI-generated". The dragon is a 3DHaupt Blender export — the *symptom* (Phong materials) is the same, the provenance is not. Comment-only error, but it hides the license issue.

### 1.3 Rig compatibility

**Generic, non-humanoid — deliberately outside the shared-rig Humanoid retarget pipeline** (PeopleCharacterImporter / EnemyAnimatorFactory territory does not apply). That is fine: it is a set-piece with its own controller and code-driven behaviour. No loose-part markers, no `tripo_part_*` nodes; the avatar is valid for its own clips. Nothing about this rig needs AccuRig.

### 1.4 Intended usage (author's design) vs ours

The author ships it as a game-ready ambient/boss creature with baked in-place actions (idle/walk/run/fly) intended to be driven by game code — exactly how `DragonBoss` uses it. We are using the asset as intended; we've simply layered our phase machine and VFX beats on top because it has no combat clips.

---

## 2. Supercyan — Character Pack: Fantasy RPG v3.0.0

### 2.1 Identity + inventory

**Publisher:** Supercyan (Finland) — Unity Asset Store publisher 22143 (https://assetstore.unity.com/publishers/22143), official site https://www.supercyanassets.com/. Owner's ledger: **Character Pack: Fantasy RPG v3.0.0, purchased 2026-06-14**. Store list of the family: https://assetstore.unity.com/lists/supercyan-character-packs-99828. Supercyan's model: all their packs share one Humanoid rig and one ever-growing shared animation library (310+ clips advertised), back-distributed free to all pack owners — which is why our copy contains far more animation than the fantasy set alone.

**Characters** (`Assets/Supercyan/Models/Fantasy/`, 8 bodies): `fantasy_archer`, `fantasy_barbarian`, `fantasy_demon`, `fantasy_knight`, `fantasy_mage`, `fantasy_orc`, `fantasy_skeleton`, `fantasy_wizard`.

**Weapons/items** (same folder): Arrow, Axe (left+right), Bow, Knife (left+right), Mace, Shield, Spear, StaffHeroes, StaffMonsters, Sword; plus `Sample Item/item_pack_mug`.

**Prefab matrix** (`Assets/Supercyan/Prefabs/Fantasy/`): every character and weapon in **High Quality** and **Mobile** tiers, each in three flavours — `Base/` (mesh+animator only), `WithItemAnimators/` (characters pre-wired for item hold/use animation), `WithItemLogic/` (weapons carrying grab/hold logic). This is the pack's intended composition system (see 2.4).

**Animation library** (`Assets/Supercyan/Animations/` — **351 animation FBX files** counted on disk): 
- `CharacterPackAnimations/MovementAnimations/` — the shared `common_people@*` set (idle, walk, run, backwards-walk/run, jump-up/float/down, pickup, dance, conversation, button-press, lose, no-gesture…).
- `CharacterPackAnimations/Fantasy/` — the combat set: `fantasy@arming_*` draw/sheathe for each weapon style (bow, dual axes, dual knives, mage staff, spear, sword-and-shield, unarmed fast/slow) and `fantasy@attack_*` per weapon family — Bow ShootAndReload, DualAxes chops, DualKnives pierces/slash, Spear thrust, Staff casting Projectile + **Summon**, SwordAndShield chop/slash/thrust/**shield bash**, most with crouch variants.
- Plus the cross-pack sets our license includes: AimAnimations (`military@pistol/rifle...`), Crouch/Prone families, EnvironmentInteractionAnimations, professions sets (Cleaners, OfficeWorkers, RetailWorkers), PlaceholderAnimations.
- `AnimatorAssets/CharacterPackAnimatorAssets/` — the pack's ready-made AnimatorControllers.

**Rig — verified Humanoid:** `fantasy_knight.fbx.meta` shows `animationType: 3` (Humanoid) with `copyAvatar: 1` — every character **copies one shared avatar** whose source lives in the animation FBX family (`lastHumanDescriptionAvatarSource` GUID resolves to `military@pistol_aim_0_ver3.FBX` et al.). One rig, many skins — precisely Supercyan's design and precisely our shared-rig doctrine.

### 2.2 The v3.0.0 shader situation — audited, already handled

The ledger warns: v3.0.0 switched the pack from Supercyan's custom cel/vertex shaders to the **Built-in Standard shader** "for easier render pipeline change", which renders wrong under URP. **Audit result: our imported copy is already converted.** 
- `Assets/Editor/SupercyanUrpMaterialFix.cs` (menu `Defenders/Art/Fix Supercyan URP Materials`, batchmode `DeNelle.Editor.SupercyanUrpMaterialFix.Run`) rewrites every material under `Assets/Supercyan/Materials` to `Universal Render Pipeline/Lit`. Its header (lines 6-8) records the root cause from the pack's own URP readme: materials ship on Built-in shaders (`m_Shader fileID 46`) and the custom "SupercyanShader" is unusable in URP.
- Spot-check proof: `Assets/Supercyan/Materials/Fantasy/High Quality/fantasy_knight_body.mat` carries `m_Shader: {guid: 933532a4fcc9baf4fa0491de14d08ed7}` — the URP/Lit shader GUID. The characters render correctly in URP. Re-run the fixer after any pack re-import/upgrade.

### 2.3 How WE consume it

- `Assets/Editor/SupercyanResourceWire.cs` (menu `Defenders/Troops/Wire Supercyan Bodies`, batchmode `DeNelle.Editor.SupercyanResourceWire.Run`) creates **prefab variants** inside `Assets/Resources/Heroes/` that inherit the Supercyan mesh + Animator + materials: `Knight → SC_Footman`, `Archer → SC_Archer` (map at lines 28-37). Both variants exist on disk (`Assets/Resources/Heroes/SC_Footman.prefab`, `SC_Archer.prefab`). Idempotent; warns-and-skips if the pack isn't imported.
- **Data wiring:** `Assets/Resources/Data/Canonical/troops.json` (mirrored in StreamingAssets) — `troop-footman` uses `"model": "SC_Footman"` and `troop-archer` uses `"model": "SC_Archer"`, both with `"modelYaw": 0`.
- **Runtime:** `Assets/_Modules/Village/Troops/TroopFactory.cs:80-90` skins the troop via `VisualFactory.Skin(go.transform, "Heroes/" + model, skinOpts)` with `skinOpts.LocalRotation = Euler(0, def.ModelYaw, 0)`. The per-pack facing rule is documented at `TroopFactory.cs:84` and `TroopDef.cs:42`: **Tripo/AccuRig bodies import facing +X → yaw −90 (historic default); Supercyan humanoids face +Z → yaw 0.** Missing model → tinted-capsule fallback so the troop still spawns damageable.
- The buildable-troop system (WO-453) is therefore the pack's only live consumer: the **Footman** (melee bruiser) and **Archer** (ranged by reach) friendly fighters. The other six bodies, all weapons, and the item/accessory scripts are currently **unused** by our code.

### 2.4 The pack's own implementation/logic — how it's meant to be used

Documented from the shipped scripts (`Assets/Supercyan/Scripts/`), for whenever we want deeper use:

- **`CommonCharacterAssets/SimpleCharacterControl.cs`** — the demo player controller. Rigidbody + Animator; two `ControlMode`s: **Tank** (up = forward, left/right rotate) and **Direct** (camera-relative movement). Movement interpolates H/V axes, scales walk (0.33) vs run, does jump with a min interval and grounded-check via `OnCollisionEnter/Stay/Exit` contact-normal dot tests, and drives the animator's `MoveSpeed`/`Grounded`/jump params. Implements the pack's `IInitializable` so the CharacterMaker can inject the Animator/Rigidbody. We do NOT use it (our locomotion is HeroLocomotion/NavMesh) — it's reference-grade only.
- **Item system** (`Scripts/Items/` + `Scripts/ItemAssets/`): weapons are `ItemObject`/`ItemLogic` prefabs (`WithItemLogic` tier) that a character grabs via `ItemHoldLogic` + `Hand` anchor points; `CharacterItemAnimator` + `AnimatorOverridesApplier` + `ItemAnimationsObject` apply per-item **AnimatorOverrideControllers** so holding a bow vs a sword swaps the arming/attack clip set (the `ItemAnimationsObjects/` folder holds these override assets). This is how the `fantasy@arming_*`/`fantasy@attack_*` clips are meant to be bound.
- **Accessory (wearables) system** (v2.2.0 feature — "wearable items like bags"): `AccessoryObject`/`AccessoryLogic`/`AccessoryWearLogic` + editor `AccessoryAttacherWizard`. The trick, visible in `AccessoryLogic.cs`: a skinned accessory ships with a duplicate rig; on wear the accessory's SkinnedMeshRenderer is re-bound to the character's bones and the duplicate rig is destroyed at `Awake` (`Destroy(m_rig)`), so one accessory fits every character sharing the avatar. Cheap cosmetic-slot tech if we ever want visible bags/hats on troops or NPCs.
- **CharacterMaker** (`Scripts/CharacterMaker/`, `AppearanceObjects/`, `BehaviorObjects/`): `CharacterMakerWizard` composes a character from a `CharacterAppearanceObject` (which mesh/materials) + `CharacterBehaviorObject` (which controller/scripts) — the pack's own "factory" pattern.
- Docs in-pack: `Character Pack Fantasy Readme.pdf`, `Character Pack Readme.pdf`, `Supercyan Universal Render Pipeline Readme.pdf` (root of `Assets/Supercyan/`).

### 2.5 Rig compatibility

**Best-in-class for our shared-rig Humanoid pipeline.** Valid shared Humanoid avatar (copyAvatar from the pack's own source), no loose parts, no Tripo markers, weapons as separate rigid FBXs. Any Humanoid clip we already retarget (ActorCore mocap, KayKit-adjacent sets) will play on all 8 bodies, and the pack's own 351-clip library retargets onto any other Humanoid in the project. The only per-pack rule to respect is facing: **+Z, yaw 0** (vs Tripo's −90).

---

## 3. Models/People

`Assets/Models/People/` contains **two unrelated populations** — an NPC pack purchased from CGTrader, and Reallusion Character Creator LOD exports left over from the Tripo-repair pipeline. **Blink cross-reference: nothing in this folder is Blink-origin** — no Blink orcs or humans here (Blink's Stylized Orcs Bundle is a separate pack covered by the Blink dossier); the humans here are CGTrader (NPC set) and Reallusion CC (FighterClass trio).

### 3.1 Population A — the CGTrader NPC set (Blacksmith, Merchant, Peasants)

**Identity:** a store-bought character set from **CGTrader** — proven by `Assets/Editor/NpcPackTrimmer.cs:2-13`, which names "the original CGTrader purchase" and the duplicate `Assets/Models/People/CGTrader Tob` folder it removed (WO-93). Naming conventions (`SKM_` skeletal mesh, `AS_` anim sequence, `T_..._Base_color` textures) are Unreal-marketplace-style, typical of CGTrader multi-engine products. The exact CGTrader listing wasn't identifiable from the web by generic names; the purchase record is the authority.

**Inventory** (4 characters, each with source `.ma`/`.obj`/`.mtl` + Unity FBX + per-character animation FBXs + PNG textures):

| Character | Mesh | Animations on disk |
|---|---|---|
| Blacksmith | `Blacksmith/SKM_Blacksmith.fbx` (+ prop meshes `SM_Anvil`, `SM_Hammed` (sic), `SM_Sword_Blank`) | `AS_Blacksmith_` Forging, Idle_1, Talking, Talking2, Walk |
| Merchant | `Merchant/SKM_Merchant.fbx` | `AS_Merchant_` Idle_1, Talking, Talking2, Walk |
| Peasant (female, "Mevina") | `Peasant/SKM_Peasant_Mevina.fbx` | `AS_Peasant_Mevina_` Idle_1, Talking, Talking2, Walk |
| Peasant Tob (male) | `Peasant Tob/SKM_Peasant_Tob_Unity.fbx` (also non-Unity variant) | `AS_Peasant_Tob_` Idle_1, Talking, Talking2, Walk |

The animation set was deliberately **trimmed** from 99 FBX (~171 MB) to just the clips the controllers use (~30-40 MB); everything removed is recoverable from `<repo>/Backups/People_Trim/` or the original purchase (`NpcPackTrimmer.cs`).

**Rig:** **Generic, per-character** — verified `SKM_Blacksmith.fbx.meta`: `animationType: 2`, own avatar (`avatarSetup: 1`). `Assets/Editor/NpcPackSetup.cs:3,11` states the policy: "FBX import (GENERIC rigs) + URP/Lit materials … Pass 1: SKM_*.fbx → Generic + own avatar, no animation, no materials." These NPCs are **intentionally outside** the shared-rig Humanoid pipeline: each body plays only its own AS_* clips. That's fine for ambient townsfolk (idle/talk/walk/forge is all they do) but means **no mocap retargeting** onto them without a Humanoid re-rig.

**How WE consume them — the full pipeline, editor to runtime:**
1. `Assets/Editor/NpcPackSetup.cs` — imports the FBXs Generic + builds `MAT_*` URP/Lit materials from the `T_*_Base_color`/`_Normal_OpenGL` textures (lines 29-111).
2. `Assets/Editor/NpcPackBuild.cs:29,52-70` — builds per-character AnimatorControllers (locomotion via a `Speed` param: Idle_1 ↔ Walk; talk beats Talking/Talking2; Blacksmith additionally Forging) and saves runtime prefabs `NPC_Blacksmith`, `NPC_Merchant`, `NPC_Peasant_Mevina`, `NPC_Peasant_Tob` — all four exist in `Assets/Resources/NPCs/`.
3. Runtime consumers (all `Resources.Load` by path):
   - `Assets/_Modules/Village/NPCs/VillageNpcInjector.cs:49-55` — Mevina = Villager (wanders), Tob = Elder, Merchant = Quartermaster, Blacksmith = Blacksmith archetype, at fixed village positions.
   - `CastleTownsfolkInjector.cs:64-65` — both peasants as castle townsfolk.
   - `CastleVendorNpcInjector.cs:62-65` — Blacksmith + Merchant as vendors, peasants as fallbacks.
   - `BarracksNpcInjector.cs:50-51` — Blacksmith body doubles as the Drillmaster, Merchant as fallback.
   - `CastleCompanionIntroducerInjector.cs:91-92` — Tob (fallback body), Merchant (fallback 2).
   - Dialogue/ambience: `TownsfolkDialogue` archetype lines + the dragon-dread tiers (§1.2).
   - `VillageSceneBuilder.Content.cs:704` — reuses `Blacksmith/` prop meshes (anvil etc.) as scene dressing.
4. Hygiene tooling: `NpcPackSourceCompressor.cs` (texture compression pass over the folder), `NpcPackTrimmer.cs` (the WO-93 trim).

### 3.2 Population B — the FighterClass LOD trio (Reallusion CC/AccuRig artifacts)

`0_FighterClass_High_High_1024_LOD0.Fbx`, `1_FighterClass_Normal_Normal_1024_LOD1.Fbx`, `2_FighterClass_Low_Low_1024_LOD2.Fbx`, each with a sibling `.json`.

**Identity — these are the AccuRig-repair artifacts from the Tripo saga.** The JSON metadata proves the chain: exporter version `1.10.1822.1`, bone set `CC_Base_*` (Reallusion Character Creator skeleton), and inside the `.fbm` an embedded `ranger.fbx` whose JSON declares `"Generation": "RL_CharacterCreator_Base_Std_G3"` with `Motion_Dummy_Female` texture folders — i.e. the Tripo-generated **ranger** was rebuilt through Reallusion tooling (AccuRig / Character Creator) and exported at three LODs. This is the owner's documented repair path for the loose-part Tripo rigs.

**Current import state:** Generic (`animationType: 2`) with **no avatar** (`avatarSetup: 0`) — verified in `0_FighterClass...LOD0.Fbx.meta`. So as they sit, they are **not animation-ready**; they are source/reference material.

**Consumption:** effectively none at runtime. `Assets/Editor/PeopleCharacterImporter.cs:1247` reaches into `0_FighterClass_High_High_1024_LOD0.fbm/` only to extract embedded textures during hero import. `Assets/Editor/TripoTextureImportCap.cs:20,62` caps texture import sizes across `Assets/Models/People/`.

**⚠ Stale-code flag:** `PeopleCharacterImporter.cs:44-56` still maps `Assets/Models/People/Human/Human_Wizard.fbx`, `human_tank.fbx`, `Human_Ranger.fbx`, `human_Cleric.fbx` and `Assets/Models/People/Orc/Orc_Berserker.fbx`, `Orc_Shaman.fbx`, `orc necromancer.fbx` — **none of these `Human/` or `Orc/` folders exist on disk any more** (the hero bodies now live as `Assets/Resources/Heroes/Knight.fbx`/`KnightV3.fbx`/`*.tripo-extracted` etc., per the dedicated-rig/Addressables canon). The importer's copy-map is a no-op against the current tree; it would warn-and-skip if re-run. Catalog-worthy staleness, not a runtime bug.

---

## 4. Models/Pet

### 4.1 Identity + inventory — a husk; the live pets moved

`Assets/Models/Pet/` currently contains **no usable model**:
- `0_Fox_Normal_Normal_512_LOD0.fbm/` — orphaned baked textures (`Coyote_Mesh_Bake_Diffuse/Metallic/Normal.png`) whose parent FBX is gone.
- `_archive_raw/sprite.fbx` — the archived **raw Tripo export** of the fairy pet, with unmistakable Tripo markers: `tripo_image_61f79f65-..._Metallic/Roughness.png` textures and a GUID-named material.

**The live pet meshes are in `Assets/Resources/Pets/`:** `aether-sprite.fbx` (fairy), `flame-pup.fbx`, `ice-wolf.fbx` (fox-family — source model "icecrystalfox3dmodel"), each accompanied by a `.tripo-extracted` marker file and a `.json` — **all three are Tripo-generated**, of the same family as the original Tripo hero bodies.

### 4.2 How WE consume the pets

`Assets/_Modules/Pets/PetDeployer.cs` (`SpawnPet`, lines ~408-460):
- Loads `Resources/Pets/<species>` mesh first, tinted-capsule fallback if missing (comment at 424: "aether-sprite (fairy) + ice-wolf (fox) Tripo FBXs landed; flame-pup still capsule until its mesh ships" — note flame-pup.fbx now exists on disk, so that comment is dated).
- Applies the **Tripo facing correction**: constant `PetForwardYaw = -90f` (+X authored forward → +Z root forward), the DEF-95 "pet travels in reverse" fix — same rule as hero/troop Tripo bodies.
- **Strips Tripo FBX contamination**: embedded Camera nodes (which otherwise hijack the screen view from the pet — 2026-05-25 root cause), AudioListeners, baked Lights/particle auras; normalizes height to 1.1; strips colliders.
- Materials fixed by `TripoMaterialFixer` (Phong → URP/Lit), which names "fox" and "fairy" in its header.

### 4.3 Rig compatibility

The pets are quadruped/creature Tripo rigs driven procedurally (movement + facing by `Pet.cs`, no retargeted clips) — the loose-part Humanoid concern doesn't apply, and no AccuRig pass is needed while they remain code-animated. If pets ever need real clips, they'd need proper creature rigs.

---

## 5. Models/Cathedral and Models/CastleGate (brief)

**`Assets/Models/Cathedral/` is an EMPTY folder** — verified: it contains nothing but `.`/`..` (not even meta stubs inside). History: the Heart of Elarion once used a Cathedral FBX (`HeartController.cs:250` still explains that "the builder strips the Cathedral FBX colliders"), and `SceneRouter.cs:164` names a `Dungeon_GlassCathedral` scene id (unrelated to this folder). The folder is dead weight — delete at the polish-end asset purge.

**`Assets/Models/CastleGate/`** — one static structure: `castle+ballast+Tower.fbx` + `castle+ballast+Tower.fbm/..._basecolor.jpg`. **Tripo-generated** (it's the "castle ballast tower" named in `TripoMaterialFixer.cs:8`; single baked basecolor texture is the Tripo signature). No rig, no animation — pure architecture. Consumers:
- `Assets/Editor/VillageSceneBuilder.Walls.cs:279-294` — loads it as the "CastleArch" for the (abandoned) Village.unity build; warns-and-skips if missing.
- `Assets/Editor/CastleHomeBuilder.cs:87` — comment-level intent only ("use CastleGate model or another tower" for the south gatehouse); the code actually instantiates a tower prefab, not this FBX.
- `CastleGateNavVerify.cs` is about the castle gate *navmesh*, not this model.

Since the live world is MainCastle_Hall + OuterWorld and Village.unity is abandoned, this FBX is **effectively unused in the shipping path**.

---

## 6. Rig compatibility audit — all packs at a glance

| Model | Source | Unity rig (verified in .meta) | Shared-rig Humanoid pipeline fit | Loose-part / T-pose risk |
|---|---|---|---|---|
| Black Dragon | 3DHaupt (free, **non-commercial**) | Generic, own avatar, 4 baked takes | No — and shouldn't be; set-piece with own controller | None |
| Supercyan ×8 (archer, barbarian, demon, knight, mage, orc, skeleton, wizard) | Supercyan (Asset Store, v3.0.0, owned) | **Humanoid**, one shared copied avatar | **Yes — ideal.** 351-clip library retargets both ways. Facing +Z (yaw 0) | None |
| NPC set ×4 (Blacksmith, Merchant, Mevina, Tob) | CGTrader (owned) | Generic, own avatar each, own AS_* clips | No (by design) — ambient NPCs on bespoke clips; Humanoid re-rig required for mocap | None |
| FighterClass LOD 0/1/2 (+embedded ranger) | Tripo → Reallusion CC/AccuRig re-export | Generic, **no avatar** (avatarSetup 0) | Not as imported; the CC_Base_* skeleton IS Humanoid-mappable if ever needed | Repaired lineage (AccuRig); raw Tripo dummy textures embedded |
| Pets: aether-sprite, flame-pup, ice-wolf | **Tripo-generated** | Creature rigs, code-driven | N/A (procedural animation) | Tripo markers present (.tripo-extracted, embedded cameras/lights — already stripped at runtime by PetDeployer) |
| Pet/_archive_raw sprite.fbx | **Tripo raw** | Unused archive | N/A | Raw Tripo (tripo_image_* textures) |
| castle+ballast+Tower | **Tripo-generated** | Static, no rig | N/A | N/A |

**Tripo vs store-bought, in one line:** Tripo-generated = the three pets, the archived sprite, the CastleGate tower, and the *ancestry* of the FighterClass LODs (repaired via AccuRig). Store/marketplace-bought = Supercyan (Asset Store), NPC set (CGTrader). Free-web = the Black Dragon (3DHaupt — with the license caveat).

---

## 7. Opportunities + gaps

**Cheap wins (assets already owned, rigs already valid):**
1. **Six unused Supercyan Humanoid bodies** — barbarian, demon, mage, orc, skeleton, wizard — are one `SupercyanResourceWire` map entry each away from being troops, NPCs, or enemy variants. Demon/skeleton/orc slot naturally as enemy-family skins; barbarian/mage/wizard as troop classes or castle NPCs. Zero rig work: same shared avatar as the already-live Footman/Archer, yaw 0.
2. **The Supercyan combat clip set is untapped.** `fantasy@attack_SwordAndShield_*`, `Bow_ShootAndReload`, `Staff_casting_Summon` etc. are Humanoid clips that retarget onto ANY Humanoid in the project — including as filler for troop attack beats (troops currently have no weapon visuals; `troops.json` notes "no projectile visual yet" for the archer).
3. **Supercyan weapon prefabs + item-override system** could give Footman/Archer visible weapons via `WithItemLogic` prefabs and `AnimatorOverridesApplier` — or just static hand-parented meshes.
4. **The accessory (wearable) tech** (`AccessoryLogic` skinned-rebind pattern) is a proven pattern for cosmetic slots on any shared-avatar body — relevant to the Wardrobe/Dressable capability canon.
5. **The NPC set's Talking/Talking2 beats** are only partially leveraged — any new vendor/quest NPC should keep reusing these four bodies before commissioning new ones (BarracksNpcInjector already demonstrates body-reuse as the Drillmaster).

**Gaps / debt:**
1. **⛔ Black Dragon license** — non-commercial on every free tier. Buy the CGTrader commercial license or replace the model before any monetized release. (Owner decision; flagged to the ledger.)
2. **Dragon combat clips** — no Attack/Death takes; current code-driven beats are fine, but a bespoke strike/death clip could later drop into the already-reserved Attack/Death animator states with zero code change (`DragonAnimatorSetup` designed for exactly this).
3. **`TownsfolkDialogue.DragonWaveId = 4` vs the 20-wave canonical table** — verify call sites pass the real apex wave id, else the NPC dread-dialogue escalates 16 waves early.
4. **`PeopleCharacterImporter` stale map** — `Models/People/Human/*` and `Models/People/Orc/*` paths no longer exist; the copy-map is dead code against the current tree.
5. **Folder hygiene (defer to polish-end purge per canon):** empty `Models/Cathedral/`; orphaned `Models/Pet/0_Fox...fbm` textures; `Models/Pet/_archive_raw`; the FighterClass LOD trio if the tripo-extracted heroes have fully superseded it; `Models/CastleGate` if Village.unity stays abandoned.
6. **FighterClass LODs have no avatar** — harmless while unused, but if anyone tries to animate them as-is they'll get a T-pose; a Humanoid import switch (the CC_Base_* skeleton maps cleanly) is the fix, not AccuRig (they already went through AccuRig).
7. **TripoMaterialFixer comment mis-attributes** the dragon and castle tower as Tripo — worth a one-line comment fix so the license provenance isn't hidden.

---

## 8. Executive summary

This dossier covers the four character/creature packs plus two structure folders. The headline finding is a **licensing risk**: the Black Dragon — our apex wave-boss "Syndrath the Devourer" — is not a store purchase at all. It is Dennis Haupt's (3DHaupt) free dragon from Sketchfab/Free3D, and every free distribution of it is licensed **non-commercial only**. The game uses it superbly (a fully code-driven three-phase flying boss that spawns on the final wave, "The Last Wing", of the twenty-wave defense — confirmed in the wave data and boss code), but before any commercial release the owner must either buy the paid CGTrader license for it or commission a replacement. Technically the asset is healthy: a Generic-rig dragon with four baked flight/ground loops, whose missing attack and death animations the code deliberately works around.

The Supercyan Character Pack: Fantasy RPG (version 3.0.0, owned) is the strongest under-used asset in the project. It ships eight characters on a single shared Humanoid avatar — a perfect match for our shared-rig retargeting doctrine — plus a 351-file animation library including full weapon-draw and per-weapon attack sets, a weapon/item hold system, and a wearable-accessory system. The version-3 shader concern from the purchase ledger is already resolved on our side: an editor tool converted every material to URP Lit, and a spot-check confirms the conversion is in place. Today we use exactly two bodies (the Knight and Archer, rewired as the buildable Footman and Archer troops); the other six bodies, all weapons, and the entire combat clip set are sitting unused and are the cheapest possible roster expansion available to us.

The Models/People folder holds two things: a four-character ambient NPC set purchased from CGTrader (Blacksmith, Merchant, and two Peasants) that is fully wired — built into prefabs by editor tooling and spawned by five different NPC injectors across the village and castle — and a set of leftover LOD exports from the Tripo rig-repair effort (Reallusion Character Creator re-exports of the Tripo ranger). The NPC set intentionally uses per-character Generic rigs with bespoke idle/talk/walk clips, which is right for townsfolk but means they cannot receive mocap without re-rigging. Nothing in this folder is Blink-origin. The pet models are all Tripo-generated and live in Resources/Pets, with the runtime already hardened against Tripo's quirks (embedded cameras, wrong facing, unrenderable materials); the Models/Pet folder itself is an empty husk awaiting the end-of-project purge, as is the completely empty Cathedral folder. The CastleGate tower is a Tripo structure used only by the abandoned Village scene builder.

Recommended actions, in order: resolve the dragon license; expand the troop/NPC roster from the six idle Supercyan bodies; fix the stale dragon-wave constant in the townsfolk dialogue and the stale copy-map in the people importer; and fold the dead folders into the planned polish-phase asset purge.
