# Dragon Boss — the Black Dragon flying-boss encounter

**Date:** 2026-05-19
**Slice:** Make the imported Black Dragon usable in-game as a flying boss — fix the
material for URP, configure the FBX rig/clips, build the AnimatorController, and
scaffold a flying-boss MonoBehaviour + prefab.
**Status:** Source written. The `.controller` and `.prefab` assets are built by
an editor script the integrator runs in Unity (no Unity access here — cannot
build, run the script, or verify rendering).
**Design source:** `docs/enemy-codex.md` §4 (boss-design vocabulary — phases,
signature mechanic, telegraph) — applied to a NEW apex encounter the codex's
roster does not cover.

> **⚠ OWNER RATIFICATION REQUIRED (spec Part 1, review-and-approve rule).**
> The Black Dragon is **not** in `enemy-codex.md` — the codex is a humanoid /
> quadruped KayKit slate of 19 entities. This boss is **agent-authored**. Two
> calls below are flagged for the owner to ratify or override:
> 1. **The name** — proposed **"Vael, the Ash-Wing"** (placeholder; see §1).
> 2. **The placement** — proposed as a **special apex village wave-boss**,
>    above the canon Necromancer of the Wound (see §2).
> Nothing here renames or contradicts a canon-locked item.

---

## 1. Identity — who the dragon is

The realm's bestiary (`enemy-codex.md`) is built from KayKit humanoids. A dragon
is a different order of threat: not a risen Folk, not a corrupted-by-proximity
beast — a **set-piece apex predator**. The codex's set-piece boss tier (§1.3)
explicitly leaves room for content the owner pulls in; the Black Dragon is the
first true *aerial* boss and sits **above** the eight named bosses as a rare,
realm-scale event.

**Proposed name — "Vael, the Ash-Wing"** *(agent-authored — owner to ratify).*
A black dragon drawn to Avalon by the Withering's spreading rot — it does not
serve Alduin, it simply hunts where the realm is weakest. The "Ash-Wing" reads
the black hide; "Vael" is a short, speakable boss-name. If the owner prefers the
dragon to be a *hand of the Wound* (a corrupted great-beast, thematically tied
to Alduin) the lore can be re-pointed without touching code — only the displayed
name string changes.

**Tone note:** the codex's register is "these are mourning stories, not twitch
tests" (§4). A dragon strains that register — it is the one enemy that is
genuinely *monstrous*, not pitiable. Recommend keeping it rare (one apex event)
so it reads as the realm's register *breaking* under the Withering, not the norm.

## 2. Where it fits — placement

**Proposed: a special apex village wave-boss** *(agent-authored — owner to ratify).*

| Option | Fit | Verdict |
| ------ | --- | ------- |
| **Apex village wave** (recommended) | The dragon circles the Avalon Heart and dives on it — the village's flight/threat geometry already has the Heart as the focal point. A flying boss is a natural escalation above the ground-bound Necromancer. | **Recommended** — best showcase, least new systems. |
| ATB turn-based encounter | The codex says all *humanoid* boss fights run in the ATB engine (§4). But a flying boss's whole appeal is the *flight* — circle, stoop, swoop — which ATB's turn-based abstraction throws away. | Not recommended — wastes the asset. |
| Dungeon mini-boss | No dungeon biome calls for a sky-boss; dungeons are interiors. | Not a fit. |

The recommendation: a **rare apex wave** (e.g. a milestone wave well beyond the
canon `BOSS_EVERY = 6` Necromancer cadence — owner picks the wave number). The
Heart's `HeartState.Boss` state already exists for boss waves; the dragon drives
it the same way. The `DragonBoss` MonoBehaviour is **placement-agnostic** — it
just needs an *anchor* `Transform` (the Heart) via `Configure`; the integrator
decides which wave / trigger spawns it.

## 3. Flight behaviour

A flying boss does **not** path a NavMesh. `DragonBoss` owns its own **kinematic
flight** — no `NavMeshAgent`, no `Rigidbody`. It needs only the anchor (the
Heart) and open sky above the village.

- **Orbit** — the dragon circles the anchor on a ring at cruise height
  (`_orbitHeight`, default 22u), banking to face its travel direction. The ring
  **tightens and quickens** with each phase.
- **Swoop** — the dive attack: the dragon drops from the orbit on a parabolic
  arc toward the anchor, **strikes at the bottom of the arc** (within
  `_strikeRadius`), and climbs back to cruise height. The whole dive-and-climb
  takes `_swoopDuration` (~3.4s) and accelerates past cruise speed.
- **Fire-breath** — a stationary strafing attack: the dragon stays on the orbit
  and breathes on the anchor. Cheaper than a swoop; dominant in Phase 1.
- **Death fall** — at zero HP the dragon corkscrews down, pitching nose-first,
  over `_deathFallSeconds` (~4.5s), then the GameObject is destroyed. The FBX
  ships no death clip, so **the fall *is* the death animation** (code-driven).

## 4. Attack phases

HP-gated behaviour, mirroring the codex's boss vocabulary (§4 — phases are
behaviour switches; telegraphs are readable warnings):

| Phase | HP band | Behaviour | Telegraph |
| ----- | ------- | --------- | --------- |
| **Phase 1 — The Circling** | 100–60% | High, lazy orbit. Mostly fire-breath passes; an occasional probing swoop (~25%). Establishes the threat. | A fire-breath pass is preceded by the Attack-trigger wing-beat + (VFX) a glow at the throat. |
| **Phase 2 — The Stooping** | 60–25% | Orbit tightens (~×0.78 radius) and quickens (~×1.45 speed). Dive-swoops now dominate (~65%). | The swoop *is* the telegraph — the dragon visibly peels off the ring and drops; the player has the dive's travel time to react. |
| **Phase 3 — The Last Wing** | 25–0% | Tightest, fastest orbit (~×0.6 radius, ×1.9 speed). **Relentless swoops**, short cadence (~2.6s), no fire-breath respite. | Continuous — the fight is now a damage race. |
| **Death — The Falling** | 0% | The spiralling fall, then destroy. | — |

**Signature mechanic:** the **dive-swoop** — the one thing the player must learn.
It is fully readable (the dragon leaves the orbit and the dive has travel time),
keeping the cozy-register "fair telegraph" rule. The escalation is *cadence and
orbit geometry*, not new attack types — the dragon gets *faster and closer*,
which reads as a predator closing in.

Tuning lives on the `DragonBoss` inspector fields (orbit radius/height/speed,
swoop duration, per-phase attack intervals, swoop/breath damage, HP). Default HP
is `4200` — well above the canon Necromancer's `1700` — anchoring it as the apex.
`Configure(id, anchor, maxHp)` lets the data/encounter layer override HP.

## 5. Files produced / changed

| File | Change | Purpose |
| ---- | ------ | ------- |
| `Assets/Black Dragon/Materials/Dragon_Bump_Col2.mat` | **edited** | Rewritten from the Unity-5 Built-in `Standard` shader (magenta in URP) to `Universal Render Pipeline/Lit`. `_BaseMap` = `Dragon_Bump_Col2.jpg`, `_BumpMap` = `Dragon_Nor_mirror2.jpg`, `_NORMALMAP` keyword on. Matches the `KayKitMaterials` URP/Lit pattern. |
| `Assets/_Modules/Village/Enemies/DragonBoss.cs` | **new** | The flying-boss MonoBehaviour — kinematic flight, phases, attacks, HP, death. Implements `DeNelle.Core.Combat.IDamageable` directly. |
| `Assets/Editor/DragonAnimatorSetup.cs` | **new** | Editor script — builds `Dragon.controller` and assembles `Boss_Dragon.prefab`. |
| `Assets/Generated/Animators/Dragon.controller` | **generated** | Built by the editor script — Fly/Idle/Attack/Death states, Speed/Attack/Dead parameters. |
| `Assets/Prefabs/Village/Generated/Boss_Dragon.prefab` | **generated** | Built by the editor script — dragon FBX rig + Animator + trigger collider + `DragonBoss`. |

No `.asmdef` files were changed. `Animator` is `UnityEngine` core, so
`DeNelle.Village` needs no new reference. `DeNelle.Editor` does **not** reference
`DeNelle.Village` — `DragonAnimatorSetup` adds `DragonBoss` by **reflection**
(`FindType` by full name), the same isolation discipline `VillageSceneBuilder`
uses; the editor script takes no compile-time gameplay dependency.

The FBX `.fbx.meta` was **not** edited — it is already correct (see §7).

## 6. The material fix

`Dragon_Bump_Col2.mat` shipped on the Built-in pipeline `Standard` shader
(`m_Shader: {fileID: 46, guid: 0000…f000…}`) — URP renders that **magenta**.
The fix rewrites the `.mat` onto `Universal Render Pipeline/Lit`
(`guid: 933532a4fcc9baf4fa0491de14d08ed7`, the same shader the project's
`*_URP.mat` files use), wiring:

- `_BaseMap` **and** `_MainTex` → `Dragon_Bump_Col2.jpg` (the colour/albedo).
- `_BumpMap` → `Dragon_Nor_mirror2.jpg` (the normal map — its `.meta` already
  has `textureType: 1` NormalMap + `externalNormalMap: 1`, so no texture-import
  fix is needed).
- `_NORMALMAP` in `m_ValidKeywords` so the URP shader samples the normal map.
- Full URP/Lit float/colour property block (`_Surface 0` opaque, `_WorkflowMode 1`
  metallic, `_Smoothness 0.2` — a faint sheen suits scaled hide), copied from a
  known-good project `*_URP.mat`.

This is the **same approach** `KayKitMaterials.ConfigureUrpLitMaterial` takes
(URP/Lit, `_BaseMap`, flat surface) — done as a direct `.mat` edit because the
dragon is a single one-off asset outside `Assets/Models/KayKit/`, so the
`KayKitMaterials` folder-walker and the `AssetImportPostprocessor` hook (both
scoped to `Assets/Models/KayKit/`) do not touch it.

## 7. The FBX rig & clips

The dragon `.fbx.meta` was inspected and is **already correctly configured** —
no edit needed:

- **Rig:** `animationType: 2` = **Generic**. Correct — a dragon is not a
  humanoid; Generic is the right rig (the task's required setting).
- **Animations:** `importAnimation: 1`, `legacyGenerateAnimations: 4`,
  `clipAnimations: []`. This is the **exact same pattern** as the KayKit
  Character Animations FBXs (`Rig_Medium_*.fbx`): Unity auto-splits the takes
  into named `AnimationClip` sub-assets — it is **not** "one long take".
- **The baked clips** — the meta's `fileIDToRecycleName` block lists the four
  `7400000`-series takes:

  | FBX take | Role |
  | -------- | ---- |
  | `Armature\|Fly_New` | **Fly** — the airborne loop. The dragon's primary state. |
  | `Armature\|Idel_New` *(sic — "Idel" mis-spelled in the source)* | **Idle** — grounded / hover-still loop. |
  | `Armature\|Run_New` | ground locomotion — unused by a flying boss (kept available). |
  | `Armature\|Walk_New` | ground locomotion — unused by a flying boss (kept available). |

- **There is NO Attack take and NO Death take in the FBX.** This is handled, not
  worked around:
  - **Attack** — `DragonBoss` realises the strike as a **dive-swoop** (movement)
    plus the Attack-trigger wing-beat; the controller's `Attack` state reuses the
    Fly clip as its Motion. A bespoke attack clip can be dropped onto that state
    later with no code change.
  - **Death** — `DragonBoss` realises death as the **spiralling fall** (movement);
    the controller's `Death` state reuses Idle as a safe placeholder Motion.

Because the meta is already right, **`clipAnimations` was deliberately left empty**
— hand-authoring explicit clip entries needs per-clip `firstFrame`/`lastFrame`
ranges that cannot be known without opening the FBX in Unity, and a wrong range
would truncate a clip. The auto-split takes are correct as-is; the AnimatorController
builder scans them by keyword (robust to the `Armature|` prefix and the "Idel"
mis-spelling), exactly as `AnimatorSetup.cs` does for the KayKit clips.

## 8. The AnimatorController

`DragonAnimatorSetup.BuildDragonAnimator` builds
`Assets/Generated/Animators/Dragon.controller`:

| State | Clip (keyword match) | Notes |
| ----- | -------------------- | ----- |
| **Fly** *(default)* | `fly` / `flight` / `soar` / `glide` → `Fly_New` | Default state — a flying boss is always aloft. |
| **Idle** | `idle` / `idel` / `rest` / `hover` → `Idel_New` | Grounded / hover-still fallback. |
| **Attack** | `attack` / `bite` / `strike` / `claw` → none → **reuses Fly** | FBX has no attack take; `DragonBoss` drives the dive. |
| **Death** | `death` / `die` / `dead` → none → **reuses Idle** | FBX has no death take; `DragonBoss` drives the fall. |

**Parameters** (the exact names `DragonBoss.cs` drives via `Animator.StringToHash`):
`Speed` (float), `Attack` (trigger), `Dead` (bool).

**Transitions:** Fly ↔ Idle on the `Speed` float (threshold `0.5`); `Attack`
fires from Fly/Idle and returns to Fly on completion; `Death` latches from any
state on the `Dead` bool and never exits.

## 9. The boss prefab

`DragonAnimatorSetup.BuildDragonBossPrefab` assembles
`Assets/Prefabs/Village/Generated/Boss_Dragon.prefab`:

- **DragonRig** child — the dragon FBX, with an `Animator` whose
  `runtimeAnimatorController` is `Dragon.controller`, `applyRootMotion` off
  (`DragonBoss` owns the flight).
- **CapsuleCollider** on the root — a **trigger**, oriented along Z (the dragon
  is long): `height 7`, `radius 1.6`. The body the hero abilities + pets sweep
  via `OverlapSphere`; a trigger keeps the airborne dragon from physically
  shoving the village. Same discipline as `Enemy_HollowWalker.prefab`.
- **DragonBoss** on the root — added by reflection. `DragonBoss` implements
  `IDamageable` itself, so **no `EnemyDamageable` adapter is needed** — the hero
  and the isolated `DeNelle.Pets` module find the boss through the Core seam
  directly.

On a missing FBX the builder substitutes a labelled placeholder capsule and logs
a warning — it never blocks (the project's builder discipline).

## 10. The `-executeMethod` entry points

```
-executeMethod DeNelle.Editor.DragonAnimatorSetup.BuildDragonAnimator
-executeMethod DeNelle.Editor.DragonAnimatorSetup.BuildDragonBossPrefab
-executeMethod DeNelle.Editor.DragonAnimatorSetup.BuildAll          (both, in order)
```

Also as menu items under **Defenders ▸ Animation ▸**. All idempotent — re-running
overwrites the `.controller` / `.prefab` in place.

## 11. Integrator steps (open — needs Unity)

1. **Reimport the dragon FBX** so Unity splits the four takes into clip
   sub-assets, and confirm the `Dragon_Bump_Col2` material now renders correctly
   (not magenta) — the `.mat` edit takes effect on the next AssetDatabase refresh.
2. **Run the build** — `Defenders ▸ Animation ▸ Build Dragon Boss (Controller +
   Prefab)`. Confirm `Dragon.controller` lands in `Assets/Generated/Animators/`
   and `Boss_Dragon.prefab` in `Assets/Prefabs/Village/Generated/`. Check the
   console summary — it logs which clip each state matched.
3. **Verify the dragon FBX import** — `animationType` should read **Generic** in
   the Rig tab; the four clips (`Fly_New`, `Idel_New`, `Run_New`, `Walk_New`)
   should appear in the Animation tab. If the take-names came through differently
   than the keyword search expects, drop the right clip onto the named controller
   state (the build logs any unmatched state).
4. **Check the dragon's import scale** — the FBX is from a 2017-era pack
   (`useFileScale: 1`, `globalScale: 1`); it may need a `scaleFactor` tweak so
   the dragon reads as a large apex boss against the village hexes. Set it on the
   prefab's `DragonRig` child or the FBX importer.
5. **Spawn it in the apex encounter.** The integrator wires the chosen apex wave
   (or a debug spawner) to `Instantiate` the prefab and call
   `DragonBoss.Configure(bossId, heartTransform, maxHp)`. Subscribe to
   `DragonBoss.StruckHeart` / `PhaseChanged` / `Died` for camera shake, the Heart
   threat state (`HeartState.Boss`), the boss HP bar, and the wave-clear hook.
6. **Confirm clear sky** — the orbit is at `_orbitHeight` 22u and the swoop dives
   to ~4.5u; make sure the apex encounter has open airspace above the Heart and
   the boss camera frames the orbit ring (the `OnDrawGizmosSelected` ring helps).

## 12. Known gaps / follow-ups

- **No bespoke Attack / Death clips** (§7). The dive-swoop and the death-fall are
  code-driven; the controller's Attack/Death states ride placeholder Motions. A
  commissioned wing-strike + a death clip would replace those state Motions with
  no `DragonBoss` change — low priority, the code-driven beats read fine.
- **`ApplyStatus` is inert.** A flying boss has no ground-slow; `DragonBoss`
  accepts the `IDamageable` status call but does nothing. A future pass could let
  `Freeze` interrupt a swoop (a fair, readable counter-play beat).
- **Name + placement unratified** (§1, §2) — the owner must confirm or override
  "Vael, the Ash-Wing" and the apex-village-wave placement before this is canon.
- **Fire-breath VFX** — `DragonBoss` fires the damage + the `StruckHeart` event
  on a breath pass, but the breath cone / throat-glow VFX is a separate art pass.
- **ATB cross-over** — if the owner ever wants the dragon to *also* appear as an
  ATB combatant (e.g. a breach into the turn-based scene), it needs an
  `ENEMY_DEFS` entry in `BattleATB/Engine/Defs.cs` — out of scope here; this
  slice is the real-time flying encounter only.
