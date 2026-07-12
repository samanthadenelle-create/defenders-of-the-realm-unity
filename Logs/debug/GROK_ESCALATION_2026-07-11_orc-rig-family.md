# GROK ESCALATION — 2026-07-11 evening session (two-failure protocol)

**From:** CLI session (Claude) per the standing rule: two failed fix attempts on the same issue →
write logs + issue to `C:\eoa\logs\debug\` for Grok, apply Grok's answer, re-test.
**Repo:** `C:\eoa` · branch `wip/village2-and-f8-tickets` · exe under test: `Builds/Windows/DefendersOfTheRealm.exe`
**Unity:** 6000.4.8f1 / URP. Hero rig = KnightV3 (Tripo self-rigged), enemies = mixed Tripo orcs + AccuRig skeletons.

---

## STATUS LEDGER FIRST (so nothing already-fixed is re-litigated)

### FIXED and owner-felt-verified tonight (do NOT rework)
- Corpses land on the ground (settle clamp: `footGap` capped 3m, lift capped 1.5m; ground mask fixed —
  the raycast targeted the nonexistent `Terrain`/`Ground` layers; owner verbatim: "on a positive note
  all enemies died on the ground").
- Green single-color enemies (Orc_Shaman + Orc_Necromancer): dangling `externalObjects` material remap
  guids (targets deleted/never committed) → repaired via extract+remap (`TripoEnemyMaterialExtractor`,
  markers ORC_SHAMAN_REPAIR_OK / ORC_NECRO_REPAIR_OK; audit sweeps the whole enemy folder now).
- Sky-corpse + camera-straight-up at the last kill (footGap=53.58 launched the corpse; the victory
  death-cam re-read the risen transform per frame → focus height now frozen at the kill).
- Timers/upgrades, in-kind repair pricing, endless waves, level persistence — all felt-closed.

### FIXED in tree, pending owner verify on the NEXT build (building now)
- Hero walking on a clip literally named `0_T-Pose` (0.04s): the Motion Caster registry's FBX clip
  loader took the FIRST sub-asset; ActorCore FBXs ship a T-pose take before the motion take.
  Loader now rejects t-pose/bind/preview/<0.1s takes and prefers the longest motion take
  (`MotionCastings.IsRealMotionClip`). Proof line that caught it:
  `[Flow:HeroLoco] vel=6.00 m/s | clips=[0_T-Pose(w=1.00,len=0.04s)] | controller=KnightMocap`.

---

## THE ESCALATED ISSUE — Tripo orc enemy family: frozen bones (T-pose, no death-fall, air-hang pose)

### Owner-felt symptoms (multiple F8s across two days; owner verbatim "I have asked many times …
RESOLVE with data NOW")
1. Enemies standing/sliding in T-pose (arena and overworld).
2. Dead enemies "not landing on ground" — after the transform-grounding fix, corpses GROUND at the
   root but the body **hangs in its last standing pose**: the death-FALL animation never plays.
3. "All enemies look/animate the same" flavor complaints trace to the same frozen family.

### The per-instance evidence (rendered owner sessions — trust these; see the headless caveat below)
```
[Flow:EnemyPose] id=-42486 model=Orc_Warrior: everPlayed=True boneMoved=False — frozen T-pose
  (rig=Humanoid vs Humanoid clips on controller 'OrcHumanoid_Warrior').
[Flow:EnemyPose] id=-70392 model=Orc_Tank: everPlayed=True boneMoved=False — frozen T-pose
  (rig=Humanoid vs Humanoid clips on controller 'OrcHumanoid_Tank').
[Flow:Enemy] animator: model 'Orc_Berserker' rig is GENERIC but controller 'OrcWarband' carries
  Humanoid clips — bind/T-pose (sliding statue).
```
Key signature: **the animator PLAYS (everPlayed=True) but bones never move (boneMoved=False)** —
on rigs that now read `rig=Humanoid` with Humanoid clips. Rig-type mismatch is NOT the root for
Warrior/Tank (it still is for Berserker, whose avatar repair failed — see attempts).

### FIX ATTEMPTS MADE (the two failures that trigger this escalation)
1. **Attempt 1 — Humanoid re-import** (`PeopleCharacterImporter.ImportOrcFamily`): Shaman + Warrior
   verdict "OK Humanoid [reimported]"; **Warrior still froze in the owner's rendered session after**.
   Berserker: "WARN avatar valid but GENERIC — FAIL avatar repair (pass 1 CreateFromThisModel, pass 2
   donor sourceAvatar, pass 3 donor humanDescription all failed)".
2. **Attempt 2 — in-family avatar repair + scope extension** (Tank/Mage added to the repair family;
   donors restricted in-family): Tank still froze in the owner's rendered session (and Tank hadn't
   even been touched when it first froze — the defect predates and survives the import passes).

### DEEP RCA RESULT (read-only agent, asset-level, 2026-07-11 ~19:20)
The break is one layer below rig-type: **the Humanoid avatar ↔ mesh binding on the Tripo orc rig.**
- `Orc_Warrior.fbx.meta` skeleton lists **22 direct children of the root**: `ParentNode` +
  **`tripo_part_0 … tripo_part_20`** — 21 mesh chunks sitting as RIGID SIBLINGS at the root, while the
  animatable bone chain (`Hip → Pelvis → L_Thigh …`) is buried under `ParentNode/Armature/Root`.
  If the visible SkinnedMeshRenderers are those `tripo_part_*` chunks (rigid, not weighted to the Hip
  chain), a PERFECT humanoid retarget animates the buried skeleton while the visible body never moves —
  exactly `everPlayed=True, boneMoved=False`.
- **Degenerate bind pose:** mapped bones stored non-T-pose (`Hip` z=3.895 rot {0.064,0.585,0.687,0.426},
  `L_Thigh` ~180° about X, twist-heavy) — `CreateFromThisModel` built the avatar from a posed skeleton.
- Import scale oddities: `globalScale 5.1–7.9`, `useFileScale 0` (working Skeleton_* rigs: scale 1,
  clean CC_Base chain, 51 mapped human bones vs the orcs' 22).
- Spawn chain verified clean (no stale prefab avatar): `EnemyFactory.Build → VisualFactory.Skin →
  Resources.Load(FBX itself; no Orc_*.prefab exists) → Instantiate → EnemyAnimatorFactory.Apply` sets
  ONLY `runtimeAnimatorController` — the freshly-imported FBX avatar is what reaches the Animator.
- Controllers verified healthy: `OrcHumanoid_Warrior/_Tank` override controllers carry a valid base
  (`OrcHumanoid.controller`, guid-matched) with populated override pairs.

### DISCRIMINATOR NOW IN PLACE (lands in the build compiling right now)
The pose-verifier FAIL line now appends:
`avatar=<name> avatarValid=<bool> isHuman=<bool> sampleBone=<name> smrRoot=<rootBone name>`
- `avatarValid=False` ⇒ degenerate avatar ⇒ in-editor hand-map / better donor repair MIGHT save it.
- `avatarValid=True` + `sampleBone`/`smrRoot` = `tripo_part_*` ⇒ **the mesh is skinned to loose parts —
  no importer setting can fix an un-weighted mesh; the FBX needs re-rig/re-export (AccuRig).**
One rendered fight on the new build produces this verdict per orc model.

### THE ASKS FOR GROK
1. Given the meta evidence (21 rigid `tripo_part_*` root chunks; posed bind; scale 5–8; 22 mapped
   bones), is there ANY Unity-import-side salvage for these Tripo orc FBXs (skin-binding transfer,
   avatar hand-map strategy, model-importer trick), or is re-rig/re-export (e.g. through AccuRig,
   which produced the WORKING skeleton family) the only honest fix?
2. If re-export: the exact AccuRig/Blender pipeline steps to preserve the existing materials
   (freshly repaired via externalObjects) and keep `Resources/Enemies/Orc_*.fbx` paths stable so the
   catalog/spawner/registry rows don't churn.
3. Berserker-specific: its avatar resists all three repair passes (CreateFromThisModel / donor
   sourceAvatar / donor humanDescription, in-family donors). Anything left before re-export?
4. Sanity-check the discriminator plan (avatarValid vs sampleBone) — is there a case it misreads?

### Supporting files for Grok (attach/read)
- `C:\eoa\Assets\Resources\Enemies\Orc_Warrior.fbx.meta` (+ Orc_Tank/Orc_Berserker/Orc_Shaman metas)
- `C:\eoa\Assets\Resources\Enemies\Skeleton_Warrior.fbx.meta` (the WORKING contrast)
- `C:\eoa\Assets\_Modules\Village\Enemies\EnemyAnimatorFactory.cs` (Apply + EnemyPoseVerifier)
- `C:\eoa\Assets\Editor\PeopleCharacterImporter.cs` (ImportOrcFamily + repair passes)
- `C:\eoa\Assets\Editor\BuildOrcHumanoidController.cs` (the override family)
- Owner F8 captures: `C:\eoa\logs\f8-inbox\capture-20260711-1813*.md`, `capture-20260711-181728.md`
- Screenshots: `%USERPROFILE%\AppData\LocalLow\DeNelle\Defenders of the Realm\flag_20260711-230400_02/_03/_04/_08.png`

### Headless caveat (do not chase)
`-nographics` fleet runs false-fail EVERY enemy's pose check (Animator `CullUpdateTransforms` = bones
not written offscreen). The verifier now skips headless/culled — only rendered-session verdicts count.

---

## SECOND (MINOR) OPEN ITEM — Berserker aside, nothing else is blocked on Grok
All other tonight's F8s are fixed-and-committed (see the ledger). The one adjacent open decision is
OWNER-side, not technical: whether to re-export ALL Tripo orcs via AccuRig in one batch (owner already
owns the pipeline that produced the working skeleton family).
