# WORK ORDER 833 - KayKit NPC idle animation (fix "NPC Stuck in T Pose")

**Status:** FIXED — implemented 2026-08-02; awaiting gate confirmation (compile gate + KayKitNpcAnimatorSetup.Build run + DataRegression NPC_MODELS) and owner felt-verify.
**Trigger:** owner F8 2026-08-02 "NPC Stuck in T Pose" - all 12 KayKit structure NPCs (WO-818) render frozen T-poses in the hub.
**Silo:** World/NPCs (follow-up to WO-818; its RESULT already flagged "KayKit bodies stand statically (no AmbientNPC/Animator) - animated idles = follow-up WO").

## RCA (from captured state + verified import settings, not theory)

- The WO-818 stager (`Assets/Editor/KayKitNpcImporter.cs`) copies each KayKit FBX into tracked
  `Assets/Resources/NPCs/KayKit/` and flips the copy to **Humanoid** (`animationType: 3`,
  `avatarSetup: CreateFromThisModel`, `importAnimation: false`) - verified in the staged `.fbx.meta`
  files and the stager code (lines 194-197).
- A Humanoid model prefab imports with an **Animator component on its root carrying the generated
  avatar but NO runtimeAnimatorController** (the stager's own avatar-verdict pass reads exactly that
  Animator - `KayKitNpcImporter.cs:200-206`, verdict "OK Humanoid avatar" 12/12 per the WO-818 RESULT).
- A skinned humanoid whose Animator has no controller **renders its bind pose** - for these rigs the
  T-pose the owner captured. Nothing is broken in the meshes, injectors, or mapping; the bodies were
  simply never given anything to play.

### Owner's question answered explicitly
**A KayKit-specific animation controller is NOT needed.** Because every staged body is Humanoid,
the project's own mocap idle retargets onto all 12 rigs - zero new animation assets. KayKit does
ship its own animation pack (`Assets/Models/KayKit/KayKit Character Animations 1.1/Animations/fbx/
Rig_Medium/*.fbx` - General/Movement/Combat/etc. multi-clip FBXs), but those import **Generic**
(`animationType: 2`) and the pack is **gitignored** - using them would need re-rigging to Humanoid
(or Generic-only bodies) plus staging. That stays an optional FLAVOR follow-up (KayKit-authored
idles have more character), not the fix.

### Clip chosen (retarget of the project's own idle)
`Assets/Action/Knight/Motion/studio-mocap-series-magical-moves/m-standby-idle.fbx` - the hero's
calm standby idle (`HeroAnimatorFactory.MocapIdleClip`), Humanoid (`animationType: 3`) with
`loopTime: 1`, tracked in git. It already drives the KnightV3 humanoid rig, so it is proven
retarget-safe.

## What was built

1. **Editor factory** `Assets/Editor/KayKitNpcAnimatorSetup.cs` (new)
   - Menu `Defenders/Art/Build KayKit NPC Idle Controller`; batchmode
     `-executeMethod DeNelle.Editor.KayKitNpcAnimatorSetup.Build`.
   - Creates `Assets/Resources/NPCs/KayKit/KayKitNpcIdle.controller` (under Resources so runtime
     can `Resources.Load` it; folder is tracked - COMMIT the controller + its .meta) with ONE
     default `Idle` state playing the mocap standby clip. No parameters, no transitions.
   - Logs humanoid + loop verdicts on the clip; marker `KAYKIT_IDLE_OK` / `KAYKIT_IDLE_FAIL`.
   - DragonAnimatorSetup pattern (asset creation, EnsureFolder, idempotent overwrite).

2. **Runtime arming** `Assets/_Modules/Village/NPCs/KayKitNpcBody.cs`
   - New `IdleControllerRes` const + `ArmIdle(GameObject bodyInstance, string resolvedRes, string system)`.
   - Normal path (the verified import case): the instantiated body already has Animator + avatar ->
     ONLY assigns `runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("NPCs/KayKit/KayKitNpcIdle")`
     and `applyRootMotion = false` (NPCs are ground-seated; the idle must not drift them).
   - Defensive fallback: no Animator -> `AddComponent<Animator>` + avatar recovered from the FBX's
     sub-assets via `Resources.LoadAll<Avatar>(resolvedRes)` (the Avatar is a sub-asset of the staged
     FBX, so LoadAll on the FBX's Resources path returns it at runtime).
   - `Guard.Try` wrapped; controller-missing => ONE `FlowTrace.Warn` and the NPC stays VISIBLE in
     bind pose (never blank); success => `FlowTrace.Once` Step "KayKit idle armed (controller=KayKitNpcIdle,
     retargeted humanoid clip)".

3. **Both injectors call it** (the only two KayKit body consumers)
   - `BarracksNpcInjector.SpawnDrillmaster`: after render-verify, `if (kayKitRes != null) KayKitNpcBody.ArmIdle(go, kayKitRes, "Village");`
   - `CastleVendorNpcInjector.SpawnVendor`: `kayKitRes` hoisted out of the load block; same gated call
     after render-verify. People-chain bodies (kayKitRes null) are never touched - they ship their own
     Animator + controller.

4. **Oracle** `Assets/Editor/Regression/DataRegression.cs` `CheckNpcModels` section (d):
   asserts `Assets/Resources/NPCs/KayKit/KayKitNpcIdle.controller` exists AND references >= 1
   animation clip - a missing/empty controller now fails the gate (`NPC_MODELS_FAIL`), not the felt-test.

## Files
- `Assets/Editor/KayKitNpcAnimatorSetup.cs` (NEW)
- `Assets/_Modules/Village/NPCs/KayKitNpcBody.cs` (ArmIdle + const)
- `Assets/_Modules/Village/NPCs/BarracksNpcInjector.cs` (one gated call)
- `Assets/_Modules/Village/NPCs/CastleVendorNpcInjector.cs` (hoist + one gated call)
- `Assets/Editor/Regression/DataRegression.cs` (oracle section d)
- `Assets/Resources/NPCs/KayKit/KayKitNpcIdle.controller` (GENERATED by the factory run - commit it)

## Runbook (orchestrator)
1. Compile gate.
2. Run `DeNelle.Editor.KayKitNpcAnimatorSetup.Build` (batchmode) -> expect `KAYKIT_IDLE_OK`;
   commit the generated controller + meta.
3. `DataRegression.RunAll` -> `NPC_MODELS_OK` now includes "NM idle controller OK".
4. Owner felt-verify in the hub.

## Acceptance criteria
- [ ] All 12 KayKit structure NPCs play a looping calm idle in the hub - NO T-pose.
- [ ] People-chain NPCs, the hero rig, Bryn (Rogue_Hooded), and skeleton enemies are untouched
      (ArmIdle only fires when the KayKit resolver supplied the body).
- [ ] Deleting the controller asset fails the DataRegression gate (NPC_MODELS_FAIL) AND at runtime
      degrades to ONE Warn + visible bind-pose NPC - never a blank/missing NPC.
- [ ] `KAYKIT_IDLE_OK` marker from the factory; humanoid + loop verdicts logged OK.

## Do NOT touch
- Hero rig / `Resources/Heroes/*`, Bryn, skeleton enemies, StructureSingleton, SaveMigrator,
  FoundingChoiceController (other lanes own those).
- The staged FBXs themselves (no re-import, no move) - the fix is purely additive.

## Follow-up (optional, owner-priced)
- FLAVOR: stage + Humanoid-convert (or Generic-rig) clips from `KayKit Character Animations 1.1`
  for KayKit-authored idles with more character. Owner picks; not needed for the fix.
