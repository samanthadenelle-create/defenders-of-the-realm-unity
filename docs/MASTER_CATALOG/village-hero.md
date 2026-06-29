# MASTER CATALOG — village-hero

> **STALE: 2026-06-28** — "Blaise + class bodies" / party-of-4 / Blink full-body rig is RETIRED.
> Player hero = a **single Tripo self-rigged Knight ("Grom")**, static armor, **no mesh-swap / no body
> swap** (combat pivot 2026-06-22; Blink hero rig JUNKED). Treat any "Blaise / class body swap / party"
> prose below as historical. Current truth = `CANON_GROUND_TRUTH_2026-06-28.md` + `docs/COMBAT_PIVOT_NORTHSTAR.md`.

**Scope:** `Assets/_Modules/Village/Hero/` — the player-hero (Blaise + class bodies) feature
area: locomotion, abilities, body swap, gear/equip, combat-feel, cameras, input, HUD bridges,
inventory/shop UI. **Assembly:** `DeNelle.Village` (`DeNelle.Village.asmdef`); refs include
`DeNelle.Core`, `DeNelle.AI`, `DeNelle.Cosmetics`, `DeNelle.Data`, `DeNelle.Pets`,
`DeNelle.Wallet`, `DeNelle.Audio`, `Unity.InputSystem`, `Unity.Cinemachine`, `LeanTouch`,
`LeanCommon`, `CW.Common`, `Unity.AI.Navigation`, `YarnSpinner.Unity`. Most types are
`namespace DeNelle.Village`; the equip/shop/rumor UI cluster is `namespace DeNelle.Village.Hero`.
Verified by reading every `.cs` file (~50 types) + the JSON data.

> **Top correction (the reason this catalog exists):** `HeroLocomotion`'s file-header comment
> says *"no Rigidbody, no NavMeshAgent — pure transform"*. **This is STALE/FALSE.** Awake()
> adds and drives a `NavMeshAgent` (`_agent.Move(step)`); the pure-transform path is only an
> off-mesh fallback. See FLAGS for the full comment-vs-code mismatch class — it recurs across
> the folder.

---

## CODE — Movement / control core

### HeroLocomotion `HeroLocomotion.cs` — `DeNelle.Village`
WASD/dpad/stick/touch walking for the hero. **MonoBehaviour**, `[DisallowMultipleComponent]`.
Wired by VillageSceneBuilder.BuildHero / re-ensured by HeroControlEnsurer.
- **REAL movement model (NOT the header):** Awake() does `GetComponent<NavMeshAgent>() ?? AddComponent`,
  configures it (radius 0.4, height 1.8, `updateRotation=false`, `autoBraking=false`, speed 30 so
  Move() never caps), and each Update reads input → eased `Velocity` → `_agent.Move(step)` when
  `_agent.isOnNavMesh`, else `transform.position += step` (off-mesh fallback). Facing = manual
  `LookRotation(Velocity)` on the root. So: **NavMeshAgent kinematically driven by input** (not
  pathfinding, not pure transform).
- Awake() also OVERRIDES serialized `_moveSpeed`/accel/decel (6/55/45) — scene-baked values stale.
- Key public surface:
  - `Vector3 Velocity {get;}` — XZ velocity; read by SmartMobileCamera, HeroFootstepController.
  - `static bool InputSuppressed {get;}` — WO-377 global input gate; **owned here**, raised on Yarn
    dialogue start/cleared on complete (hooks DialogueRunner, resilient retry coroutine, single-flight
    guarded against a coroutine fork-bomb). Consulted by HeroAbilityInput (+ PlayerAttackController/BuildMode out-of-scope).
  - `void WarpTo(Vector3, Quaternion?)` — WO-383 teleport-aware seam warp (disable agent → move → `agent.Warp` → re-enable); raises `event Action OnTeleported` (SmartMobileCamera subscribes to snap).
  - `bool IsAutoWalking`, `bool AutoWalkArrived`, `void SetAutoWalk(Transform)`, `void ClearAutoWalk()` — WO-277 tutorial auto-walk (drives hero along NavMesh to a target).
  - `static bool GroundSnapEnabled` — DEF-147 off-mesh gravity ground-snap / re-bind (anti hover-exploit).
- Subscribes WaveManager.OnWaveCleared → 2.5s victory pose (DEF-70; the never-clearing-latch bug fixed).
- Drives both legacy `Animator.SetFloat("Speed")` (param-guarded, WO-174) AND `ActorAnimator` (canonical).
- Reads camera basis from `SmartMobileCamera.CameraYaw` (WO-387 camera-relative; 0 in top-down).
- Reflection-reads `DeNelle.HUD.VirtualDPadLean.Move` (no hard HUD ref) + `VirtualJoystick.Move`.
- `static bool IsLiftCarrying()` → `LiftPlatform.AnyCarrying()` (direct, both in DeNelle.Village).

### HeroControlEnsurer `HeroControlEnsurer.cs` — `DeNelle.Village`
**MonoBehaviour** + `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` self-bootstrap DDOL singleton.
Keeps the hero controllable and RECOVERS if the hero root is destroyed early. Activates for Village*/
Castle*/MainCastle*/CastleHub* scenes. **This is the runtime attach-point for many hero components:**
on Ensure() it adds (if absent) HeroLocomotion, HeroDeathLogger, **HeroTargetIndicator**, PlayerAttackController,
GearLoadout, and wires SmartMobileCamera (`SetTarget`+`ForceFollowImmediate`), disabling CinemachineBrain.
`SpawnEmergencyHero()` builds a capsule "Hero (Blaise)" if none exists (≤8 retries, polled via Watch coroutine).
Deliberately does NOT attach HeroReachRing (DEF-205, see FLAGS). Nested `HeroDeathLogger` MonoBehaviour
warns if the hero is destroyed while "Village2" is active.

---

## CODE — Abilities (Q/W/E/R)

### HeroAbilities `HeroAbilities.cs` — `DeNelle.Village`
**MonoBehaviour**, `[DisallowMultipleComponent]`. Blaise's Q/W/E/R kit; owns mana pool + per-slot
cooldowns; resolves casts via `Physics.OverlapSphere` → Core `IDamageable`. Port of React castAbility.ts.
- Public: `float Mana`, `MaxMana`, `string HeroClass`, `float ManaRegenMultiplier {get;set;}`,
  `float CooldownRemaining(slot)`, `float CooldownFraction(slot)`, `bool CanCast(slot)`,
  `void SetHeart(HeartController)`, `void SetHeroClass(string)` (HeroBodySwapper calls this post-swap),
  `bool TryCast(AbilitySlot)` (the cast entry; cooldown+mana gate).
- DTT/aim override fields: `Vector3? AimPointOverride`, `IDamageable LockedTarget` (set by HeroTargetIndicator),
  `Func<float,bool> HealHandler`.
- Damage chain: `def.Damage × HeroTalentModifiers.DamageMultiplier × HeroProgression.DamageMultiplier ×
  AttackTimingBonus × GearLoadout.WeaponMult`. Offensive casts LAUNCH a visible projectile (RangedAttackVFX)
  and land damage on arrival; meteor explodes on arrival; heal routes to HeroHealth (or HealHandler in DTT).
- Awake() self-resolves class from `GameStateService.State.HeroClass` (WO-36 backstop; Cleric→"mage" loadout).
- Lazily AddComponents: HeroProgression(get), RangedAttackVFX, GearLoadout. Param-guards "Cast" trigger (WO-163).
- Reaches the airborne apex boss via `WaveManager.LiveApexBoss` (WO-125).

### HeroAbilityInput `HeroAbilityInput.cs` — `DeNelle.Village`
**MonoBehaviour**, `[DisallowMultipleComponent][RequireComponent(HeroAbilities)]`. Fires TryCast from
keyboard `1/2/3/4` (NOT Q/W/E/R — W is movement), gamepad face buttons (S/E/W/N), and **left-click/Space
fire slot Q** (primary attack at the locked target). Respects `HeroLocomotion.InputSuppressed`.

### AbilityCatalog `AbilityCatalog.cs` — `DeNelle.Village`
Static loader for `abilities.json`. Enums `AbilitySlot {Q,W,E,R}`, `AbilityEffect {Strike,Snare,Aoe,Cleave,Heal,Meteor}`;
`[Serializable]` `AbilityDef` (Newtonsoft, with `SlotEnum`/`EffectEnum`/`UnityColor` accessors), `AbilityClassDef`, `AbilityCatalogData`.
`const DefaultClass="mage"`. `GetLoadout(class)`, `Find(class,slot)`, `Reload()`. WebGL-safe via `CoreServices`-adjacent `DeNelle.Core.CanonicalJson.Read` (Resources first, StreamingAssets fallback).

### AttackTimingBonus `AttackTimingBonus.cs` — `DeNelle.Village`
**MonoBehaviour** singleton, `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` bootstrap (DDOL).
DEF-47 combo-rhythm: chains casts within 1.2s → 1.0/1.15/1.30/1.50× damage. `static float NotifyCast(pos)`
(HeroAbilities calls it), `static int ChainDepth`, `static float WindowRemaining`, `event Action<int> OnChainChanged`.
Pops a `DamageNumberSpawner.SpawnLabel` "CHAIN ×N". Awake `Destroy(this)` (not gameObject) on dup — singleton-dedup safe.

---

## CODE — Body swap / animation / IK / pose

### HeroBodySwapper `HeroBodySwapper.cs` — `DeNelle.Village` (1187 lines — the big one)
**MonoBehaviour**, `[DisallowMultipleComponent]`. At Start() swaps the baked Wizard placeholder for the
chosen class FBX (`Resources/Heroes/<slug>.fbx`; Knight/Ranger/Mage/Cleric). Uses the shared
`VisualFactory.Skin` (FitHeight/SeatOnGround/StripColliders + `LocalRotation = -90° yaw` forward correction, WO-326).
Loads `Resources/Heroes/<slug>.controller`; ensures the Humanoid avatar; `applyRootMotion=false`; `speed=0.5`
(Mixamo too fast); `cullingMode=AlwaysAnimate`; `Rebind()`. **Reflection-writes** the private `_animator` field
on HeroLocomotion + HeroAbilities post-swap and calls `SetHeroClass(abilitySlug)` (Cleric→Mage). Adds
GearLoadout + EquipmentController; calls `GearVisualApplier.Apply`; for Ranger calls `HeroBowAttachment.AttachTo`.
Material pipeline: `RetargetMaterialsToUrp` (Phong→URP/Lit, double-sided), `ApplyExtractedTexture` (per-class
basecolor atlas from `Resources/Heroes/Textures/*`), `ApplyClassTint` fallback, Knight `ApplyFlatSteelStopgap`.
WO-376: drives a clean idle (`DriveIdlePose`), holds idle during Yarn dialogue (resilient runner hook +
single-flight retry, same fork-bomb guard as HeroLocomotion). **Heavy comment archaeology** — many
contradictory dated WO notes about the Knight texture (see FLAGS).

### HeroAimIK `HeroAimIK.cs` — `DeNelle.Village`
**MonoBehaviour** (root) + nested **HeroAimIKReceiver** MonoBehaviour (on the HeroBody/Animator GO, because
`OnAnimatorIK` only fires on the Animator's own GO). DEF-70 upper-body aim IK toward `_aimTarget` via
`SetIKRotation(RightHand)`. `void SetAimTarget(Transform)`, `float IkWeight`. **Largely DORMANT:** the spec
wants an "UpperBody" layer-1 + avatar mask the current controller doesn't have, and **no code sets an aim
target** (no caller of SetAimTarget found in this folder) — degrades gracefully to layer-0 / no-op. See FLAGS.

### HeroChargeVFX `HeroChargeVFX.cs` — `DeNelle.Village`
**MonoBehaviour**. DEF-70 charge-up particle + audio. `void StartCharge()`, `void ReleaseCharge()`.
**DEAD/UNWIRED:** both methods are explicitly TODO "wire from HeroCombat once it lands" — **no caller exists**.
Builds a placeholder ParticleSystem if none assigned. See FLAGS.

### HeroImpactFeedback `HeroImpactFeedback.cs` — `DeNelle.Village`
**MonoBehaviour**. DEF-70: `void PlayRecoil()` (Animator "BowRecoil" trigger — param-guarded, controller lacks it
today → no-op) + `void PlayHaptic(intensity,duration)` (gamepad rumble). PlayHaptic IS LIVE — called by
HeroHealth.TakeDamage. PlayRecoil is unwired ("wire from HeroCombat" TODO). See FLAGS.

### HeroVictoryPoseBridge `HeroVictoryPoseBridge.cs` — `DeNelle.Village`
**MonoBehaviour**, `[RequireComponent(WaveManager)]` — a BRIDGE that lives on the WaveManager GO. Fires the hero
Animator "VictoryPose" trigger on OnWaveCleared / resets on OnWaveStarted. **Mostly inert:** the controller has
no "VictoryPose" param (param-guarded no-op). Note: HeroLocomotion ALSO does its own victory pose on
OnWaveCleared via the "Victory" trigger — two parallel victory paths. See FLAGS (duplicate).

### HeroPoseController `HeroPoseController.cs` — `DeNelle.Village`
**MonoBehaviour** + `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` DDOL singleton. WO-365 context pose:
TOWN (relaxed, weapon sheathed) ↔ COMBAT (ready, weapon drawn). Listens (listen-only) to WaveManager
OnWaveStarted/OnBreach/OnWaveCleared/OnDefeat + 0.5s poll. Drives Animator bool "Combat" (param-guarded; controller
lacks it → no-op) AND the always-working half: SetActive on "BowProp"/"GearVisual_*" weapon children at the
0.30s transition midpoint. Village-scene-gated, WebGL try/catch-guarded.

### HeroFootstepController `HeroFootstepController.cs` — `DeNelle.Village`
**MonoBehaviour**. DEF-57 velocity-driven footstep audio (reads `HeroLocomotion.Velocity`, lerps interval
walk↔run). `void PlayStep()` (also callable from an Animation Event). **Needs `_footstepClips` assigned** (none
wired by default → silent until clips set). Auto-adds a 3D AudioSource.

---

## CODE — Combat-feel / VFX / projectiles

### RangedAttackVFX `RangedAttackVFX.cs` — `DeNelle.Village`
**MonoBehaviour**, `[DisallowMultipleComponent]`. DEF-23 projectile launcher. `void FireArrow(targetPos, onArrive)`,
`void FireSpellOrb(targetPos, onArrive)` — HeroAbilities calls these. Spawns a real particle-FX-bodied projectile
(`ProjectileVFXCatalog.SpawnFlying`) carried by ProjectileMover; green-fire/red-land cast bursts. Raw placeholder
primitives suppressed by default (`ShowPlaceholderProjectiles=false`, WO-280; `-showPlaceholderProjectiles` opt-in).

### ProjectileMover `ProjectileMover.cs` — `DeNelle.Village`
**MonoBehaviour**, `[DisallowMultipleComponent]`. Lerps start→target with optional parabolic arc, faces travel,
spawns `ImpactFX`, fires `onArrive` payload (damage/status land on connect), self-destructs. `void Launch(target,speed,arc,onArrive)`.

### AbilityVfxKit `AbilityVfxKit.cs` — `DeNelle.Village`
Static, asset-free procedural ability VFX (WO-35/37). `PlayHeroAbility(kind,color,pos,radius,targetHint,class)`
(tries VFXManager prefab first, then procedural), `SpawnAbilityVfx(...)`, `SpawnAbilityVfxForClass(...)` —
class-specific shapes (Knight ground-shockwave, Ranger arrow streak, Mage arcane). URP-safe runtime particles +
soft-dot texture. Nested internal `VfxLightFade` MonoBehaviour (fades a point-light then self-destroys).

### AbilityAudioBridge `AbilityAudioBridge.cs` — `DeNelle.Village`
Static. Per-ability + per-class SFX through `CoreServices.Audio` (WO-41, reflection removed). `PlayForKind`,
`PlayForClassAndKind`, `PlayMusic(name)`, `PlayDangerSting()`. Nested internal `ProceduralSfx` synthesises
click-free clips in code (cached; prefers `Resources/Sfx/<Kind>` if present).

### AbilityCooldownUI `AbilityCooldownUI.cs` — `DeNelle.Village`
**MonoBehaviour**. Drives an `Image.fillAmount` cooldown sweep on an ability button. `void StartCooldown(duration)`.
Belt-and-braces fallback path (HeroAbilities `GetComponent<AbilityCooldownUI>()?.StartCooldown`); the live HUD
sweep goes via HeroAbilitiesHudBridge reflection. Mostly a legacy/secondary surface.

### HeroTargetIndicator `HeroTargetIndicator.cs` — `DeNelle.Village`
**MonoBehaviour**, `[DisallowMultipleComponent]`. Camera-facing reticle billboard over the current hostile target +
Tab/right-shoulder manual cycle. Attached by HeroControlEnsurer. `IDamageable CurrentTarget {get;}`. Scans via
TargetManager registry ∪ Enemy-layer OverlapSphere (Enemy-mask, 128-buf — the 64-buf overflow bug fixed). Forward-arc
gate (DEF-269, 45m range, dot 0.35) for auto-acquire; manual lock bypasses. Feeds `HeroAbilities.AimPointOverride`
+ `LockedTarget`; toggles `FloatingHealthBar.SetTargetedOn`. Runtime ring texture + URP/Unlit transparent quad (WebGL-safe).

### HeroReachRing `HeroReachRing.cs` — `DeNelle.Village`
**MonoBehaviour**, `[DisallowMultipleComponent]`. Faint ground ring showing melee reach (auto-syncs to
PlayerAttackController.AttackRange). **DEAD-BY-POLICY:** HeroControlEnsurer deliberately does NOT attach it (DEF-205
"mystery indicator" removed); class kept for a future opt-in. See FLAGS.

---

## CODE — Health / hit reactions

### HeroHealth `HeroHealth.cs` — `DeNelle.Village`
**MonoBehaviour**, `[DisallowMultipleComponent]`, `: IDamageableStructure`. Singleton `Instance`. Hero HP + contact
damage (OverlapSphere Enemy layer, 1s ticks) + IMGUI health bar (suppressed when `CoreServices.Hud != null`, WO-411).
`float MaxHp/Hp/Fraction`, `bool IsAlive`, `void TakeDamage(float)`, `void Heal(float)`, `void Respawn(Vector3)`;
`event Action<float,float> OnHealthChanged`, `event Action OnDied`, `event Action OnDeath`. DEF-102 death→respawn
(hero is NOT the lose condition — the Heart is). Applies `GearLoadout.ArmorDefense` reduction + perfect-parry
(`PlayerAttackController.TryConsumeParry`). Drives ActorAnimator Die/Revive (WO-284/285). **Bootstrap:** nested
`HeroHealthBootstrap` MonoBehaviour + `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` DDOL — polls for a
HeroAbilities GO and AddComponents HeroHealth + HeroHitReaction (deliberately NO floating bar on the hero).

### HeroHitReaction `HeroHitReaction.cs` — `DeNelle.Village`
**MonoBehaviour**, `[DisallowMultipleComponent]`. Red IMGUI screen-edge damage flash + death slow-mo (Time.timeScale
ramp). Driven off HeroHealth.OnHealthChanged/OnDied (this branch's real C# events, NOT the greenfield WO UnityEvents).
Plays ActorAnimator `PlayHit(Gut)` on non-fatal hits (WO-285). Attached by HeroHealthBootstrap.

---

## CODE — Gear / equip system

### GearCatalog `GearCatalog.cs` — `DeNelle.Village`
Static loader for `weapons.json` / `armor.json`. `[Serializable]` `GearReq{level,dex,arcane,might}`,
`WeaponDef` (id/name/icon/job/rarity/damageMult/reach/req/setId/saga/flavor/makersMark/buy{Wood,Food,Iron,Crystals},
`bool IsAegis`), `ArmorDef` (…/defense/hpBonus/…). `BestWeapon(job,level)`, `BestArmor(job,level)`, `FindWeapon(id)`,
`FindArmor(id)`, `AllWeapons()`, `AllArmors()`, `GetBuyCost(def)→ResourceCost`, `Reload()`. WebGL-safe via
`DeNelle.Core.CanonicalJson` + Newtonsoft. Graceful: missing catalog → no gear (1.0 mult / 0 defense).

### GearLoadout `GearLoadout.cs` — `DeNelle.Village`
**MonoBehaviour**, `[DisallowMultipleComponent]`. The live hero gear model. Auto-equips best eligible weapon+armor
by class+level (re-evaluates on HeroProgression.OnLevelUp). `float WeaponMult` (HeroAbilities reads), `float ArmorDefense`
(HeroHealth reads), `WeaponDef EquippedWeapon`, `ArmorDef EquippedArmor`, `event Action OnGearChanged`,
`void Refresh()`, `void EquipWeaponById(id)`, `void EquipArmorById(id)`. WO-295 Aegis full-set bonus:
`bool AegisSetActive`, `float WardRefundFraction` (0.25 when set), per-class weapon perk folded into WeaponMult +
bonus defense; lazily attaches AegisSetEffect. **The canonical equip model the inventory/shop drive.**

### EquipmentController `EquipmentController.cs` — `DeNelle.Village`
**MonoBehaviour**, `[DisallowMultipleComponent]`. Visually equips REAL KayKit weapon meshes on the Humanoid rig's
hand bones (shields→LeftHand), driven by GearLoadout.EquippedWeapon. `void Equip(weaponId)`, `void EquipBestForHero()`,
`void Unequip()`, `void EquipOffHand(WeaponDef|string)` (shield→LeftHand), `void SetCombatActive(bool)` (idle-lowered ↔ combat-ready hold), `void SetArmorTier(int)` (**WO-567: tints the static hero BODY per tier via MaterialPropertyBlock — NO mesh swap, no Blink revival; coexists with HeroArmorRimLight emission via GetPropertyBlock-merge; tier table = owner-tunable BONES**).
Maps weapon ids → KayKit mesh + grip preset (Sword/Dagger/Axe/Hammer/Staff/Wand/Bow/Shield); bounds-normalizes any FBX
(`NormalizeInto`); **geometric sword grip-point inference** (`SeatByHandle` — vertex width-profile finds the crossguard
spike → grips the handle; build-safe `isReadable` guard). **MESH-LOADING GAP:** loads from `Resources/Heroes/Props/Weapons/<mesh>`
which is NOT yet populated → falls back to a tinted primitive (see FLAGS / file header ACTION). Skips bows when
HeroBowAttachment owns them. Re-attaches on OnGearChanged.

### GearVisualApplier `GearVisualApplier.cs` — `DeNelle.Village`
Static. Legacy PRIMITIVE-cube gear visuals (sword/staff/mace on RightHand, shield/pauldron/chest for plate).
`void Apply(body, loadout)`, `void ReapplyForHero(loadout)`. **GATED OFF by default:** `static bool EnablePrimitiveGear = false`
(the cubes "read as a square in the torso"). Apply() still always CLEARS stale "GearVisual_*" children. Bows always
skipped (HeroBowAttachment owns them). Superseded by EquipmentController for real meshes; see FLAGS (legacy/duplicate).

### HeroBowAttachment `HeroBowAttachment.cs` — `DeNelle.Village`
**MonoBehaviour**, `[DisallowMultipleComponent]`. Cosmetic bow on the Ranger's LeftHand bone. `static void AttachTo(heroRoot,body)`
(HeroBodySwapper calls for Ranger). Loads `Resources/Heroes/Props/Bow` (committed) → else builds a procedural low-poly
bow+string. Bounds-normalizes (`NormalizeInto`: longest→+Y, narrowest→+X, grip-centred, scaled to BowHeldLength 0.92m);
`GripLocalEuler = (0,0,0)` (the "+91 Z = bow turned sideways" bug removed). Self-bootstrap retry (≤120 frames) until Humanoid bones bind.

### AegisSetEffect `AegisSetEffect.cs` — `DeNelle.Village`
**MonoBehaviour**, `[DisallowMultipleComponent]`. WO-295 "Oathweld" ward — while the full Aegis set is worn, refunds
a fraction (`GearLoadout.WardRefundFraction` 0.25) of hero damage taken as HP to the HeartController (+ ward VFX pulse).
Listens HeroHealth.OnHealthChanged (decrease-only). `bool WardActive`, `void Refresh()`. Lazily attached by
GearLoadout.EnsureSetEffect(); inert when set not equipped.

### GearAppraisal `GearAppraisal.cs` — `DeNelle.Village`
Static, read-only lore/value surface (WO-300). `GearAppraisalResult Appraise(WeaponDef|ArmorDef)` + `…WeaponId/ArmorId(id)`.
Derives maker's mark (Emberhand/Oathweld/Heartwood/Last-Pressing) from def/saga, tier label/hex (GearTierTable), estimated
crystal value (tier base + stat worth + legendary/Elarion premium). `GearAppraisalResult` has `Summary()`/`FullText()`.
Used by ShopPanel buy labels. No I/O, WebGL-safe.

### ItemIconCatalog `ItemIconCatalog.cs` — `DeNelle.Village`
Static. Maps a weapon/armor/consumable → a real artwork Sprite sliced from `Resources/ItemIcons/*` sheets
(`Resources.LoadAll<Sprite>`, WebGL-safe), keyword + rarity-tier matching; null → caller uses glyph fallback.
`ForWeapon(WeaponDef)`, `ForArmor(ArmorDef)`, `ForConsumable(id,name)`. Used by HeroInventoryController. Depends on
ItemIconSlicer (editor) having produced the sheets.

### HeroEquipment `HeroEquipment.cs` — `DeNelle.Village.Hero` (NOTE: different namespace)
**MonoBehaviour**. WO-109 "basic equip" with `enum EquipmentSlot{MainHand,Armor}` + `[Serializable] EquippedItem`.
`bool Equip(itemId)` / `void Unequip(slot)` / `EquippedItem GetEquipped(slot)` / `void TryEquipDemoWeapon()`.
**DEMO/STALE PARALLEL SYSTEM:** only knows hardcoded `"basic_sword"`/`"leather_armor"`; attaches a primitive cube
"EquippedSword"; bonuses only `Debug.Log`'d (TODO "patch the controller"). **Superseded by GearLoadout/EquipmentController**
— this is the foundation stub, only opened by EquipmentPanel via Yarn "OpenEquip". See FLAGS (duplicate/stale).

---

## CODE — Cameras

### SmartMobileCamera `SmartMobileCamera.cs` — `DeNelle.Village` (the canonical camera, ~950 lines)
**MonoBehaviour**, `[DisallowMultipleComponent][RequireComponent(Camera)]`. Singleton `Instance`. DEF-53 adaptive
third-person follow: movement-lead, combat-zoom (FOV+distance), optional auto-framing, player-authoritative orbit
(`_panYaw` via `AddYaw`/`AddPitch` — NEVER velocity-driven, kills the curl/spiral), WO-385 occluder-FADE (ShadowsOnly)
instead of pull-in, WO-383 teleport snap (subscribes HeroLocomotion.OnTeleported). `float CameraYaw {get}` (HeroLocomotion's
movement basis; 0 when orbit off), `void SetTarget(Transform)`, `void ForceFollowImmediate()`, `void Shake(intensity,duration)`,
`bool FramingEnabled`, `void EnforceSoleCamera()`. Awake() force-migrates the baked top-down/orbit-off scene values
(`_forceCameraFix`) with no rebake. **Disables sibling VillageCamera** (sole-camera contract).

### VillageCamera `VillageCamera.cs` — `DeNelle.Village`
**MonoBehaviour**, `[RequireComponent(Camera)]`. The LEGACY trivial fixed-offset follow rig + sole-camera enforcer +
editor-only drift diagnostics. `void SetTarget(Transform)`. **DISABLED at runtime by SmartMobileCamera** (kept as fallback).
See FLAGS (superseded).

### HeroCinemachineRig `HeroCinemachineRig.cs` — `DeNelle.Village`
**MonoBehaviour**, `[DisallowMultipleComponent]`. Cinemachine-3 OTS rig (CinemachineCamera + ThirdPersonFollow + Deoccluder,
IgnoreTag "Player"). **DEAD/UNUSED:** its own header + VillageCamera's note say it's commented out of the builder; the live
camera is SmartMobileCamera; HeroControlEnsurer actively DISABLES CinemachineBrain. See FLAGS (dead).

### CameraModeController `CameraModeController.cs` — `DeNelle.Village`
**MonoBehaviour**, `[RequireComponent(Camera)][DefaultExecutionOrder(100)]`. WO-338 context camera: post-processes SMC's
seat to add a TOWN bird's-eye mode (locked to town centre/origin) and blends 0.6s. `CameraMode Mode`, `bool IsTownActive`,
`void SetTownCentre(Vector3)`. **Effectively gated to BUILD MODE ONLY** — `EvaluateContext` resolves TOWN only while
`BuildModeController.IsActive` (the idle-in-town TOWN engaged a "stuck on the tree" bug, now disabled); the IsWaveActive/
IsInBattle/IsExploring helpers exist but no longer drive the mode. Suspends/resumes SMC as the single writer.

### CameraModeControllerBootstrap `CameraModeControllerBootstrap.cs` — `DeNelle.Village`
**static** + `[RuntimeInitializeOnLoadMethod(SubsystemRegistration reset + AfterSceneLoad)]`. Auto-AddComponents
CameraModeController onto the SMC camera in "Village"-named scenes (idempotent, try/catch). No scene edit.

---

## CODE — Input drivers

### VirtualJoystick `VirtualJoystick.cs` — `DeNelle.Village`
**MonoBehaviour**, `[DisallowMultipleComponent]` + `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` DDOL singleton.
Code-built on-screen thumbstick (polls legacy `UnityEngine.Input` touch/mouse, no EventSystem). `static Vector2 Move`
(HeroLocomotion reads), `static bool IsInZone(screenPos)` (CameraPanInput excludes the stick zone). Shown only on a
touch/mobile target with a live hero. WebGL/mobile movement input.

### CameraPanInput `CameraPanInput.cs` — `DeNelle.Village`
**MonoBehaviour**, `[DisallowMultipleComponent]` + `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` DDOL singleton.
DEF-202/204 slide-to-pan (Lean Touch) + right-mouse + Q/E/R/F keyboard orbit → `SmartMobileCamera.AddYaw/AddPitch`.
`static Instance`. Tap-vs-drag threshold (12px) so taps pass to attack; excludes joystick-zone + GUI + build mode;
hero-presence gated. Keyboard orbit is the WebGL-reliable path (browser eats right-drag).

---

## CODE — HUD bridge / progression

### HeroAbilitiesHudBridge `HeroAbilitiesHudBridge.cs` — `DeNelle.Village`
**MonoBehaviour**, `[DisallowMultipleComponent][RequireComponent(HeroAbilities)]`. Cross-asmdef **reflection** bridge to
`DeNelle.HUD.VillageHudController` (Village can't ref HUD). IN: HUD `AbilityRequested` event → `HeroAbilities.TryCast`.
OUT (per-frame reflection invokes): `SetMana`, `SetHeroHp`, `SetAbilityCooldown`, `SetAbilitySlot` (5- or 6-arg with accent
hex) — pushes the active class's Q/W/E/R glyph/name/desc when the class changes (WO-36). Self-resolves the HUD at runtime
(WO-428/421) since the serialized ref is only wired in VillageSceneBuilder.

### HeroProgression `HeroProgression.cs` — `DeNelle.Village`
**MonoBehaviour**, `[DisallowMultipleComponent]`, `: IXpEarner`. Hero XP/level + level rewards. Singleton `Instance`;
**Bootstrap** `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` DDOL standalone that ProgressionManager later migrates onto
the hero (Awake takes-over + migrates XP, never Destroy(gameObject) — the frozen-village bug fixed). `const Id="hero"`
(matches HeroAbilities damage attribution). `int Level`, `float Xp/XpToNext/LifetimeXp`, `float DamageMultiplier`
(+6%/level, cap 3×; HeroAbilities reads), `int AddXp(float)`; `event Action<int> OnLevelUp`, `static event Action<int> OnAnyLevelUp`
(instance-swap-proof, DEF-261), `event Action<float,float> OnXpChanged`. Front-loaded quadratic XP curve; grants Wisdom
(WisdomCurrencyService) + skill points (SkillSystem) per level. Registers in Core XpEarnerRegistry. Core→Village circular-ref
note: damage attribution writes via the registry id, not a direct call.

---

## CODE — Equip / shop / quest UI (`DeNelle.Village.Hero` namespace)

### HeroEquipHud `HeroEquipHud.cs` — `DeNelle.Village` (note: NOT .Hero)
**MonoBehaviour** singleton + `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` Bootstrap. A single compact bag-icon HUD
button → opens the inventory modal. `static EnsureExists()`, `void` build. Auto-spawns in hub scenes (MainCastle_Hall/Village2/
CastleHub). WO-411: now wires to the HUD TOWN-ACTIONS row's BAG via reflection (`InventoryRequested`) → `HeroInventoryController.Open`;
self-heals the wire each frame until bound (the "BAG dead in castle" fix). Code-built uGUI, WebGL-safe rounded sprite.

### HeroInventoryController (split) — `HeroInventoryController.cs` + `InventoryUIBuilder.cs` / `InventoryPaperDoll.cs` / `InventoryGrid.cs` / `InventorySidebar.cs` (DeNelle.Village.Hero ns, partial class)
**MonoBehaviour** singleton (partial across 5 files for maintainability; no behaviour change). Full-screen code-built uGUI inventory + gear modal (same Open/Close/Toggle/Ensure, same GearLoadout drive, same W/A Tech dark-wood+gold styling via ElarionUiKit + Tech hud pack sprites for sockets/tabs/cells). Tabs Weapons/Armor/Outfits/Consumables, paper-doll, 4-col grid, detail sidebar with TechPrimary EQUIP. PanelManager registered. **DATA GAP unchanged.** (Split executed to resolve prior monolithic 1573-line state while preserving 100% prior layout/Tech W/A polish and calls from HeroEquipHud/VillageHud.)

### ShopPanel `ShopPanel.cs` — `DeNelle.Village.Hero` (994 lines)
**MonoBehaviour**. Code-built vendor shop (BUY/SELL/EQUIP). `void Open(string vendorContext)`. Opened via Yarn "OpenShop"
(NPCCommandBridge). Uses EconomyService (TrySpend/Grant ResourceCost), VillageInventory (Add/Get/TryConsume), GearLoadout
(equip), GearCatalog (stock), GearAppraisal (maker's-mark labels). Vendor flavour filter (armorer/forge) with a never-empty
fallback (WO-406); scrollable content (WO-412 collapse fix). Light-parchment palette. LIVE.

### EquipmentPanel `EquipmentPanel.cs` — `DeNelle.Village.Hero`
**MonoBehaviour**. WORLD-SPACE code-built panel for the **legacy HeroEquipment** stub. `void Open()`. Opened via Yarn "OpenEquip".
Only the two demo items (basic_sword/leather_armor). **STALE/DEMO** — paired with the superseded HeroEquipment, not the
canonical GearLoadout path. See FLAGS (duplicate/stale).

### RumorBoardPanel `RumorBoardPanel.cs` — `DeNelle.Village.Hero`
**MonoBehaviour**. WO-304 Brom's quest board (browse/accept) — code-built overlay (uGUI+TMPro). `void Open()/Close()`.
Read-only consumer of Core `QuestService`/`QuestCatalog` (only write is `StartQuest(id)`); repaints on QuestChanged.
Opened via Yarn "OpenRumorBoard". (Hero-adjacent, in the Hero folder by location; quest-domain.)

---

## DATA (JSON)

### weapons.json — `Assets/StreamingAssets/Data/Canonical/` (+ Resources/Data/Canonical mirror that WINS at load; +`Assets/Data/Canonical` source)
Schema `{ "_note", "weapons":[ {id,name,icon(emoji),job(mage|knight|ranger|cleric),rarity,damageMult,reach?,req:{level,dex?,arcane?,might?},setId?,saga?,flavor?,makersMark?,buyWood?,buyFood?,buyIron?,buyCrystals?} ] }`.
**16 weapons:** 4 each mage/knight/ranger (common→epic, mult 1.0→2.1) + 4 legendary `aegis_*` (one per class incl. cleric,
mult 2.2–2.4, `setId:"aegis"` only on… actually only the armor carries setId here — note: aegis WEAPONS have NO setId field, so
`WeaponDef.IsAegis` returns FALSE for them — see FLAGS). reach only on knight/cleric melee. **Copy:** mirror MUST stay in sync
(Resources copy wins in WebGL). Editing tunes gear with no recompile.

### armor.json — same locations
Schema `{ "_note", "armor":[ {id,name,icon,job("any"),rarity,defense(0..0.9),hpBonus,req,setId?,saga?,flavor?,makersMark?,buy*} ] }`.
**5 armors:** cloth/leather/chain/plate (common→epic, def 0.04→0.20) + `aegis_plate` "Aegis of Elarion" (legendary, def 0.28,
`setId:"aegis"`). hpBonus carried but **v1 applies defense only** (per _note). The aegis armor IS the only piece with setId.

### abilities.json — `Assets/StreamingAssets/Data/Canonical/` (+ Resources mirror)
Schema `{ version, "_comment", classes:{ <class>:{ displayName, abilities:{ q/w/e/r:{slot,key,name,description,icon,color(hex),effect,cooldown,manaCost,damage,range,freeze?} } } } }`.
**3 classes** (mage/knight/ranger; **no cleric class** → Cleric uses mage loadout in code). Mage is VERBATIM React port; Knight+Ranger
are **AUTHORED placeholders** flagged for re-sync when React v1 publishes (see _comment). `description` is HUD-only, re-authorable.
Keyboard `key` differs from slot (W uses "F" — W is movement).

---

## FLAGS

### Stale comment-vs-code (the requested mismatch class)
1. **HeroLocomotion** — header: *"no Rigidbody, no NavMeshAgent — pure transform"* and *"primitive Capsule with auto-collider…
   depenetration"*. **CODE:** it IS a `NavMeshAgent`, driven by `_agent.Move()`; the hero has no movement collider; walls are
   enforced by the absence of NavMesh, not depenetration. The class XML summary ("Kinematic transform translation") is also stale.
   **This is the headline mismatch.**
2. **VillageCamera** — header claims it's the single active camera and "HeroCinemachineRig is commented-out". True today only
   because **SmartMobileCamera disables VillageCamera at runtime**; the header reads as if VillageCamera is live.
3. **HeroAimIK / HeroChargeVFX / HeroImpactFeedback.PlayRecoil / HeroVictoryPoseBridge / HeroPoseController** — all carry long
   headers describing controller additions ("UpperBody layer 1 + mask", "VictoryPose/BowRecoil/Combat params") **that the live
   HeroAnimatorSetup controller does NOT contain**, so these drives are param-guarded no-ops today. The comments describe intended,
   not actual, behaviour.
4. **ItemIconCatalog** `Sheets[]` lists `ItemIcons/inEJH` (bows) but the folder listing shows other sheet GUIDs present
   (Ud37F/WRdWM/VxBVb/bRUz5/CtQcX/jdRCa have `.meta`s in git status); verify `inEJH` exists or bow art silently falls to glyph.

### Dead / unwired code
- **HeroCinemachineRig** — superseded by SmartMobileCamera; HeroControlEnsurer disables CinemachineBrain. Not in the builder. DEAD.
- **HeroChargeVFX** — `StartCharge/ReleaseCharge` have no caller (explicit "wire from HeroCombat" TODO). DEAD.
- **HeroAimIK** — no code calls `SetAimTarget`; with no aim target IkWeight collapses to 0. Effectively DORMANT.
- **HeroReachRing** — intentionally NOT attached (DEF-205); kept for a future opt-in. DEAD-BY-POLICY.
- **HeroImpactFeedback.PlayRecoil** — no caller (TODO). (PlayHaptic IS live via HeroHealth.)
- **GearVisualApplier** — primitive-cube path gated off (`EnablePrimitiveGear=false`); superseded by EquipmentController. LEGACY.

### Duplicate / parallel systems
- **Two equip stacks:** canonical **GearLoadout + EquipmentController** (data-driven, real meshes, drives HeroAbilities/HeroHealth)
  vs legacy **HeroEquipment + EquipmentPanel** (`DeNelle.Village.Hero`, hardcoded demo items, cube sword, log-only bonuses, Yarn
  "OpenEquip"). The latter is a stale WO-109 stub — do not extend it; route equip through GearLoadout.
- **Two weapon-visual paths:** EquipmentController (real KayKit, current) vs GearVisualApplier (primitive cubes, gated off).
- **Two victory-pose paths on wave-clear:** HeroLocomotion's own "Victory" trigger + 2.5s movement-suppress, AND
  HeroVictoryPoseBridge's "VictoryPose" trigger on the WaveManager GO. Both subscribe OnWaveCleared. (The bridge is inert today
  — no param — so no live conflict, but it's redundant intent.)
- **Namespace split:** equip-system + shop/quest UI live in `DeNelle.Village.Hero` while everything else is `DeNelle.Village`
  (HeroEquipHud is `.Village` despite opening the `.Village.Hero`-adjacent inventory). Easy to mis-reference.

### Scene-gated / disabled / contradictory
- **CameraModeController TOWN mode** is gated to BUILD MODE ONLY (the idle-town bird's-eye caused the "stuck on the tree"
  unplayable-village bug); its IsWaveActive/IsInBattle/IsExploring helpers are now vestigial.
- **EquipmentController mesh gap:** real KayKit weapon meshes aren't in `Resources/Heroes/Props/Weapons/` → every hero shows the
  tinted-primitive fallback until art is copied (file-header ACTION FOR ART/CLI). Not a bug, but the "real meshes" are not live.
- **aegis weapons have no `setId`** in weapons.json (only `aegis_plate` armor does) → `WeaponDef.IsAegis` is FALSE for all four
  aegis weapons → `GearLoadout.AegisSetActive` (needs BOTH aegis weapon AND armor) can NEVER be true → the Oathweld ward
  (AegisSetEffect) + per-class Aegis weapon perk are effectively UNREACHABLE. **Likely a data bug** (add `"setId":"aegis"` to the
  four aegis weapons). Flag for owner.
- **HeroFootstepController** ships with no `_footstepClips` → silent until clips are assigned.
- **abilities.json** has no `cleric` class; Cleric body (own FBX) + Cleric.controller exist but fire the Mage loadout (by design,
  routed in HeroBodySwapper `abilitySlug` + HeroAbilities Awake) — intentional, documented, but a latent gap if a cleric kit is expected.
- **IMGUI hero HP bar (HeroHealth.OnGUI)** is a FALLBACK only — suppressed whenever `CoreServices.Hud` is registered (WO-411);
  same for the contact-damage IMGUI bar. The real bar is the uGUI VillageHudController via HeroAbilitiesHudBridge.

---

*Items cataloged: ~50 code types (45 files) + 3 JSON data files. All entries verified by reading the source, not comments.*
