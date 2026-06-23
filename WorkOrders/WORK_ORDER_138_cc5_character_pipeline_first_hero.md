# WORK ORDER 138 — CC5 Character Pipeline: first animated hero on the shared rig

**Status: READY TO IMPLEMENT**
**Lane:** Character engine foundation. Depends on the validated CC5→Unity pipeline (this session).
**Goal:** Get ONE CC5 hero into the game — correct scale, correct materials, valid Humanoid rig — and
play a **retargeted** combat animation on the `Character` substrate. Proves "one rig → skin all →
animate all → inherit all" *in-engine*, the foundation every hero/enemy then rides.

> Owner direction (2026-05-30): Elden-Ring-themed, create-your-hero, mobile-first. Art = **CC5 +
> InstaLOD**, motion = **Mixamo/Humanoid retarget**, classes = weapon→role→HUD, races = morphs. Docs:
> `CHARACTER_CREATOR.md`, `CHARACTER_ARCHITECTURE.md`, `ENGINE_MASTER_PLAN.md`.

## Pipeline validation (already proven — baseline numbers from the test export)
A default CC5 character exported via InstaLOD + Auto-Setup imported as:
- LOD0 **7,790 tris** · LOD1 3,556 · LOD2 1,664 — **mobile-light** (realistic CC base is normally 50–100k).
- **1 material / 1 submesh** (InstaLOD atlas) → **~1 draw call per character** → crowds are cheap.
- **Humanoid avatar `valid=True human=True`** → retargeting confirmed viable.
- Known fixes folded in below: **import scale** (came in ~0.3 m, needs ~1.8 m) + **80-bone skeleton**
  (optimize toward ~30–54 for crowds, later).

## Owner manual steps (minimal — everything else is code/batchmode)
1. **Mixamo** (mixamo.com, free Adobe login): download **one Idle, one Walk/Run, one Sword Slash** as
   **FBX for Unity, *without* skin** (Mixamo rig retargets via Humanoid). Drop into `Assets/_Incoming/Anims/`.
2. **One CC5 hero export** when ready (same Unity-3D preset; InstaLOD ON is fine — it gave us the atlas).
   Tell the agent the path.
*(Soupday tools + all wiring = the agent's job.)*

## Phase 1 — Render one hero correctly (materials + scale + rig)
- Install **`soupday/CC_Unity_Tools` (URP)** into the project (Package Manager git URL or `.unitypackage`).
  This builds the CC skin/hair/eye **URP materials** (no more pink).
- Import settings on the hero FBX: **AnimationType = Humanoid**, **scale fix** so height ≈ 1.8 m,
  generate the avatar. Confirm it renders skinned + correct size in a test scene.
- **Acceptance:** hero stands at human scale, real materials (not pink/grey), in bind/idle pose.

## Phase 2 — Make it move (retargeted animation)
- Import the Mixamo clips as **Humanoid**; confirm they **retarget onto the CC avatar** (the proof that
  motion authored once drives any skin).
- Build a minimal **Animator controller**: Idle ↔ Locomotion (blend by speed) + a **Swing** trigger.
- **Acceptance:** the CC5 hero idles, and on a dev key plays a **retargeted sword swing** — *rig + skin +
  animate + inherit, live in-engine.*

## Phase 3 — Wire onto the `Character` substrate (the engine seam)
- Minimal `Character` MonoBehaviour per `CHARACTER_ARCHITECTURE`: holds the Animator (+ stubs for
  NavMeshAgent/Health/VFX to fill later). A **dev harness** spawns the hero in a test scene/runtime.
- **`Equipment → ActionSet` stub:** a `WeaponDef` (sword) maps action "swing" → the Swing clip; `DoAction`
  plays it through the Animator. Prove a *data-defined* weapon drives the animation (not a hardcoded key).
- **Acceptance:** swapping the weapon's ActionSet swaps the moveset; one hero proves the seam the whole
  catalog plugs into.

## Validation gate
- Run the hero through **`Perf Budget (Standard Phone)`** at LOD0 and with ~20 instances. Green = ship the
  pipeline; if heavy, drop to LOD1/LOD2 for crowds + bone-optimize. Log the numbers.

## What NOT to touch
- ❌ `VillageSceneBuilder.cs` / `Village.unity` — use a **separate test scene or runtime spawn**; no
  scene hand-edits, no bake needed.
- ❌ Don't delete/replace the existing Knight/Mage/Ranger yet — this runs **alongside** until parity is
  proven (no big-bang). The CC5 hero is additive.
- ❌ Don't commit the heavy test FBX/anim files (`Assets/_Incoming/` stays out of git); CLI sole-commits.

## Carry-over constraints
- Humanoid rig is the contract (every skin + morph binds to it). · Single-material/draw is the mobile win
  — preserve it. · `DoAction` routes VFX through `VFXManager` only (later). · Cosmetic fabrics/skins swap
  **look only**, never the weapon's **repo** (catalog⊥repo). · Brace/compile-gate every `.cs`; build-
  connected session writes, CLI verifies + commits.

## Definition of done
A CC5 hero — correct scale, real materials, valid Humanoid rig — **idles and swings a sword via a
data-defined weapon ActionSet, on the `Character` substrate, inside the mobile perf budget.** From there,
every new hero is a skin, every enemy a morph, every class a weapon — and they all inherit this motion.
That is the character engine, proven on one body.
