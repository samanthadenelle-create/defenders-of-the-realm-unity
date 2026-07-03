# ANIMATION DOSSIER — 2026-07-03 (deep-session prep)

Compiled 2026-07-03 from the **actual assets and code** (controller YAML parsed directly, `.meta`
files read, code cited by line — no comment-trust). Working tree = branch `wip/village2-and-f8-tickets`.

> **⚠ The 07-02 animation fixes are UNCOMMITTED working-tree changes** — `Knight.controller`,
> `OrcHumanoid.controller`, `AnimatorSetup.cs`, `BuildOrcHumanoidController.cs`,
> `HeroAnimatorFactory.cs`, `HeroBodySwapper.cs`, `HeroLocomotion.cs`, `Enemy.cs` all show `M` in
> `git status`. Anything decided tomorrow lands on top of an unbanked diff.

---

## 1. Master-state framing → animation submodel status

Owner ruling (docs/HUD_OBSIDIAN_ARCHITECTURE_2026-07-03.md §A4.7 + memory `master-posture-state-tree`):
the posture arc is the MASTER STATE; animation is a dumb submodel of it:
`calm gait → hostile(prebattle) alert/weapon-drawn → activebattle combat locomotion/stances →
postbattle victory | death | injured locomotion`.

What exists in the controllers **today** (hero = `Assets/Resources/Heroes/Knight.controller`,
enemy = `Assets/Resources/Enemies/OrcHumanoid.controller`):

| Posture state | Hero (Knight.controller) | Enemy (OrcHumanoid) | Verdict |
|---|---|---|---|
| **calm gait** | `Locomotion` blend tree (default state): idle@0 / walk@2 / run@6, clips `Shared_Idle` / `Shared_Walk_Forward` / `Shared_Run_Forward` | `Locomotion` tree: `Orc Idle`@0 / `standing walk forward`@1.5 / `standing run forward`@3.5 | **EXISTS** — but it is the ONLY gait; same clips in and out of combat |
| **hostile(prebattle) alert / weapon-drawn** | **NO STATE.** `InCombat` (Bool) param exists but **zero transitions reference it** — a stub. Unused clips sit ready: `Assets/Action/Shared/Shared_Combat_Idle.fbx` (loop), `Assets/Action/Knight/draw sword 1.fbx` + `draw sword 2` + `sheath sword 1/2`, `sword and shield idle`, `standing taunt battlecry` | **NO STATE.** `InCombat` param also declared and also unused by any transition | **MISSING — the big gap.** Clips already imported; only controller states + a driver are absent |
| **activebattle combat locomotion/stances** | **NO combat-stance locomotion** — battle reuses the calm `Locomotion` tree. Combat *actions* exist: `Attack0/1/2` (Combo int selects `standing melee attack horizontal` / `combo ver. 1` / `combo ver. 2`, state speed 1.3), `Cast_q/w/e/r` + full-body/upper split (see §2), `Block` (`Shared_Block`), `Hit` (`Shared_Hit_Reaction`) | Actions exist: `Attack` (`standing melee combo attack ver. 1`), `WindUp` (`Standing 2H Magic Attack 01`), `Cast` (`Spell Cast.fbx`), `Hit` (`Shared_Hit_Reaction`) | **PARTIAL** — actions yes, combat *locomotion/stance* no. Unused candidates: `sword and shield walk/run/strafe (×4)` in Assets/Action/Knight |
| **postbattle victory** | `Victory` state (trigger `Victory`), clip `Shared_Victory_Pose` (256 frames, non-loop). Bridge: `Assets/_Modules/Village/Hero/HeroVictoryPoseBridge.cs` | none (enemies don't win) | **EXISTS** (hero) |
| **death** | 3 states: `Death` (`Shared_Death`), `DeathLeft` (`Shared_Standing_Death_Left`), `DeathRight` (`Shared_Standing_Death_Right`) — `Dead` Bool + `DeathDir` int (0/1/2) select | `Dead` state, clip `Shared_Death` (Dead Bool latch) | **EXISTS** — but see §5.2 "dying in air" |
| **injured locomotion** | `InjuredLocomotion` blend tree: `injured idle`@0 / `injured walk`@2 / `injured run`@6 (Assets/Action/Enemies), entered on `Injured` Bool | `InjuredLocomotion` tree: same 3 clips @0/1.5/3.5, gated by `FeatureFlags.EnemyInjuredStance` + HpFraction<0.3 (`Enemy.cs:866-867`) | **EXISTS** (scaffolding per §A4.7) |

**Stubbed parameters (declared, never used by any transition):** hero `InCombat`, `WindUp`;
enemy `InCombat`. These are the pre-drilled holes for the prebattle/activebattle states.

---

## 2. Controller inventory (parsed from YAML)

### 2.1 Hero — `Assets/Resources/Heroes/Knight.controller`

**Parameters:** Speed(Float), InCombat(Bool, unused), Attack(Trig), Combo(Int), Cast(Trig),
CastVariant(Int), WindUp(Trig, unused), Block(Bool), Hit(Trig), HitDir(Int, unused in transitions),
Dead(Bool), DeathDir(Int), Victory(Trig), Injured(Bool).

**Layers:** 2.
- `Base Layer` — default state `Locomotion`.
- `Upper Body` — defaultWeight **1**, **`m_Mask: {fileID: 0}` — NO avatar mask.** Default state
  `Empty` (no motion), so it's inert until a Cast fires; but when `CastUpper_*` plays it overrides
  the **whole body**, not just the upper body. (Works because the moving-cast split below routes
  here only via AnyState triggers; still a mislabeled layer worth knowing.)

**Base Layer states (16):**

| State | speed | Clip (guid→file) |
|---|---|---|
| Locomotion (default) | 1 | BlendTree ↓ |
| InjuredLocomotion | 1 | BlendTree ↓ |
| Attack0 | 1.3 | `Action/Knight/standing melee attack horizontal.fbx` |
| Attack1 | 1.3 | `Action/Knight/standing melee combo attack ver. 1.fbx` |
| Attack2 | 1.3 | `Action/Knight/standing melee combo attack ver. 2.fbx` |
| Cast / Cast_q | 1.3 | `standing melee attack horizontal` (q = default cast reuses a MELEE clip) |
| Cast_w | 1.3 | `standing melee attack 360 high` |
| Cast_e | 1.3 | `sword and shield power up` |
| Cast_r | 1.3 | `standing melee attack downward` |
| Victory | 1 | `Action/Shared/Shared_Victory_Pose.fbx` |
| Hit | 1 | `Shared_Hit_Reaction` |
| Death | 1 | `Shared_Death` |
| DeathLeft | 1 | `Shared_Standing_Death_Left` |
| DeathRight | 1 | `Shared_Standing_Death_Right` |
| Block | 1 | `Shared_Block` |

**Upper Body states (6):** `Empty` (default, no motion), `CastUpper`/`CastUpper_q`
(`standing melee attack horizontal`), `CastUpper_w` (`360 high`), `CastUpper_e` (`power up`),
`CastUpper_r` (`attack downward`) — all speed 1.3. Cast routing: AnyState → full-body `Cast_*`
requires `Speed < 2` (standing); `Speed ≥ 2` NOT required on the upper variants — the upper-layer
AnyState fires on `Cast`+`CastVariant` unconditionally, base keeps Locomotion (cast-while-running).

**Locomotion blend tree (the 07-02 fix, baked in the asset):** 1-D on `Speed`,
`m_UseAutomaticThresholds: 0`:

| Child clip | Threshold | m_TimeScale |
|---|---|---|
| Shared_Idle | 0 | 1 |
| Shared_Walk_Forward | 2 | **2** |
| Shared_Run_Forward | 6 | **3** |

`InjuredLocomotion` mirrors it exactly (injured idle/walk/run @0/2/6, timeScale 1/2/3).
**Uncommitted diff confirms the change:** was thresholds 0/6/9 with timeScale 1/1/1 → now 0/2/6
with 1/2/3 (both trees).

Builder: `Assets/Editor/HeroAnimatorFactory.cs` — thresholds set at `:255-257`, cadence bake in
`ApplyLocomotionCadence` `:451-460` (`threshold ≥ 5.9 → ×3; ≥ 1.9 → ×2`).

### 2.2 Enemy — OrcHumanoid family (`Assets/Resources/Enemies/`)

`OrcHumanoid.controller` = full **AnimatorController** (1 layer, no mask). `_Mage` / `_Tank` /
`_Warrior` = **AnimatorOverrideControllers** on that base.

**Base params:** Speed(F), InCombat(B, unused), Attack(T), Cast(T), CastVariant(I), WindUp(T),
Hit(T), HitDir(I), Dead(B), DeathDir(I), Injured(B).

**Base states:** Locomotion (default, tree ↓), InjuredLocomotion (tree ↓), Attack
(`standing melee combo attack ver. 1`), WindUp (`Standing 2H Magic Attack 01`), Cast
(`Spell Cast.fbx`), Hit (`Shared_Hit_Reaction`), Dead (`Shared_Death`).

**Blend trees** (1-D on Speed, all timeScale **1** — enemies did NOT get the cadence bake):

| Tree | @0 | @1.5 | @3.5 |
|---|---|---|---|
| Locomotion | `Orc Idle` | `standing walk forward` | `standing run forward` |
| InjuredLocomotion | `injured idle` | `injured walk` | `injured run` |

**Role overrides:**

| Override | Replaces | With |
|---|---|---|
| _Mage | Attack clip | `Spell Cast.fbx` (mage melee = a cast) |
| _Tank | Idle / Attack / Cast | `sword and shield idle` / `standing melee attack downward` / `standing taunt battlecry` |
| _Warrior | Cast | `Sword And Shield Attack.fbx` |

**Assignment:** `Assets/_Modules/Village/Enemies/EnemyAnimatorFactory.cs` — `RigFor` `:29-62`
(Orc_Warrior/Tank/Mage → OrcHumanoid; Troll/Demon/OgreMage → LargeHumanoid; Necromancer → Boss;
Orc_Berserker/Shaman → OrcWarband; default → HumanoidMedium), role pick in `ControllerForModel`
`:86-99`, `Resources.Load("Enemies/"+name)` + assignment `:125-139`, fallback to base OrcHumanoid
`:128-136`. Sole call site: `EnemyFactory.cs:157`.
**ATB path does NOT use role overrides:** `AtbCombatantSwapper.cs:624-639` maps all orc roles to
plain `"OrcHumanoid"`.

### 2.3 KayKit-clip controller family (`AnimatorSetup` output)

`HumanoidEnemy` / `Boss` / `LargeEnemy` (and Generated `Pet`, `Hero`, `Npc`) are built by
`Assets/Editor/AnimatorSetup.cs` (`Defenders/Animation/Build Animator Controllers`, `:145`) from
the 8 KayKit Rig_Medium FBXs (`:88-98`) into `Assets/Generated/Animators/`, with copies in
`Assets/Resources/Enemies/`.

| Controller | States (speed) | Notes |
|---|---|---|
| HumanoidEnemy | Idle, Move, Attack(1.15), Hit, Death | Idle/Hit/Death share ONE clip guid; Idle↔Move gate at Speed 0.1 |
| Boss | + Cast(1.15) | same clip set + a cast |
| LargeEnemy | Idle/Move/Attack/Hit/Death | Rig_Large clips |
| LargeHumanoid | Locomotion tree (`Orc Idle`@0 / walk@1.8 / run@3.5), Attack=`Standing 2H Magic Area Attack 01`, Death=`Falling Back Death` | Mixamo, not KayKit |
| OrcWarband | Locomotion (`Orc Idle`@0 / run@5 — NO walk child), Attack=`Sword And Shield Attack`, Death=`Falling Back Death` | Mixamo |

**⚠ Missing-clip risk:** the KayKit clip guids referenced by HumanoidEnemy/Boss/LargeEnemy/Pet
(`48065a22…` Idle/Hit/Death, `2077604e…` Move, `4016ba86…` Attack, `c402cd47…` Boss Cast, plus the
LargeEnemy trio) resolve **only inside the gitignored `Assets/Models/KayKit` folder** — verified:
`48065a22…` = `KayKit Character Animations 1.1/Animations/fbx/Rig_Medium/Rig_Medium_General.fbx.meta`;
no tracked `.meta` declares them. They play on this machine (pack on disk) but are **Missing on any fresh
clone / build agent without the pack** → those enemies would idle/die in a frozen pose. Same class
of problem as polyperfect (§4 CLAUDE.md).

### 2.4 Pets — broken chain

- Only `Assets/Generated/Animators/Pet.controller` exists (params Speed/Attack/Hit/**Dead**;
  KayKit clip guids). It is **NOT in Resources**.
- `Assets/_Modules/Pets/PetDeployer.cs:687-700` loads `Resources/Pets/<species>` then
  `Resources/Pets/Pet` — **both null today** → pets run with no controller (bind pose).
- `Assets/_Modules/Pets/PetAnimatorController.cs` fires a **`Death` trigger**, controller declares
  **`Dead` Bool** — mismatch even if the controller were wired.

---

## 3. Clip source map

### 3.1 `Assets/Action/Shared/` — the Mixamo shared set (15 FBX, all animationType 3 = Humanoid, scale 1)

| Clip | Frames (≈30fps) | Loop | Used today by |
|---|---|---|---|
| Shared_Idle | 110 | yes | Knight+Orc Locomotion@0 |
| Shared_Walk_Forward | 34 | yes | Knight Locomotion@2 |
| Shared_Run_Forward | 28 | yes | Knight Locomotion@6 |
| Shared_Combat_Idle | 103 | yes | **UNUSED** (the prebattle-stance clip, sitting idle) |
| Shared_Block | 37 | no | Knight Block |
| Shared_Hit_Reaction | 14 | no | Knight Hit + Orc Hit |
| Shared_Death | 112 | no | Knight Death + Orc Dead |
| Shared_Standing_Death_Left / _Right | 69 / 61 | no | Knight DeathLeft/DeathRight |
| Shared_Victory_Pose | 257 | no | Knight Victory |
| Shared_Injured_Turn_Left / _Right | 28 / 56 | no | **UNUSED** |
| Shared_Turn_Right / Shared_turn_left | 29 / 29 | no | **UNUSED** (no turn-in-place states) |
| Shared_Standing_Torch_Walk_Forward_Stop_Edge | 81 | yes | **UNUSED** |

Import pipeline: `Assets/Editor/ActionClipImporter.cs` auto-sets Humanoid + in-place root for
everything under `Assets/Action/`.

### 3.2 Other Mixamo folders under `Assets/Action/`

| Folder | Count | Consumers |
|---|---|---|
| `Knight/` | 99 fbx | Knight attacks/casts (7 clips used); Orc _Tank overrides (3); **~90 unused** incl. draw/sheath sword ×4, sword-and-shield walk/run/strafe/idle/death/impact sets, taunts, kicks, jumps |
| `Ranger/` | 13 | HeroAnimatorFactory RangerRoots (Ranger hero archetype only) |
| `Wizard/` | 15 | HeroAnimatorFactory WizardRoots — see §5.4 caster candidates |
| `Enemies/` (injured*) | 20 | 3 used in both Injured trees; 17 unused (injured turns/jumps/backwards set) |
| root loose | 36 | `Orc Idle`, `standing walk/run forward`, `Spell Cast`, `Standing 2H Magic Attack 01`, `Sword And Shield Attack`, `Falling Back Death` used by enemy controllers; rest unused |

### 3.3 KayKit library (gitignored, on disk)

`Assets/Models/KayKit/KayKit Character Animations 1.1/Animations/fbx/` — 8 `Rig_Medium_*` FBXs,
**131 unique clips + 8 T-Pose takes** (embedded takes, auto-split):

| FBX | Clips | Highlights |
|---|---|---|
| CombatMelee | 21 | Melee_1H_Attack_Chop/Stab/Slice ×5, 2H ×5, Block set ×4, Dualwield ×3, Unarmed ×3 |
| CombatRanged | 19 | Ranged_Magic_Spellcasting(+Long)/Raise/Shoot/Summon, Bow set ×6, 1H/2H gun sets |
| General | 14 | Death_A/B (+poses), Hit_A/B, Idle_A/B, Interact, PickUp, Spawn ×2, Throw, Use_Item |
| MovementBasic | 10 | Walking_A/B/C, Running_A/B, Jump ×5 |
| MovementAdvanced | 12 | Dodge ×4, strafes, crouch/crawl/sneak, holding-weapon runs |
| Simulation | 13 | Cheering(=victory candidate), sit/lie sets, Waving |
| Special | 14 | full Skeletons set (awaken/death/resurrect/taunt/walk) |
| Tools | 28 | Chop/Dig/Hammer/Fish/Saw/Pickaxe/Work — the **echo-workforce** clip set |

Characters: `KayKit Adventurers 2.0/Characters/fbx/Knight.fbx` etc.
**Which play today:** only what `HumanoidEnemy`/`Boss`/`LargeEnemy`/`Pet` reference (an
Idle/Move/Attack/Death handful) — and Pet's consumer is broken (§2.4). 120+ clips unused.

### 3.4 Rig types (from the .metas — the retarget gate)

| Asset | animationType | Meaning |
|---|---|---|
| KayKit characters + all Rig_Medium clips (`Rig_Medium_General.fbx.meta:106`) | **2** | **Generic** |
| Tripo Knight hero (`Assets/Resources/Heroes/Knight.fbx.meta:622`) | **3** | **Humanoid** |
| All `Assets/Action/*` Mixamo clips | 3 | Humanoid |

**Consequence: KayKit clips can NOT retarget onto the Tripo Knight as imported** — Generic clips
only play on the rig they were authored for. See §6b.

---

## 4. The 07-02 fixes + knobs (with felt-status)

| Fix | Where | What it does | Felt-status |
|---|---|---|---|
| Hero cadence bake | `HeroAnimatorFactory.cs:243-258, 451-460` + baked into `Knight.controller` | Thresholds 0/6/9→0/2/6; per-child timeScale walk×2 / run×3 to cancel the global 0.5× for locomotion only | **Owner verdict: "walk animation is horrible"** — insufficient or worse; see §5.1 |
| Global hero anim speed | `HeroBodySwapper.cs:32` `HeroAnimSpeed = 0.5f`, applied `:343` (`anim.speed = 0.5`), `applyRootMotion = false` at `:339` | Halves ALL hero clip playback ("Mixamo clips run fast", owner 2026-05-30) | Long-standing; now half-cancelled by the bake — two layers fighting |
| Runtime cadence knob | `Assets/_Modules/Village/Hero/HeroLocomotionCadence.cs` (attached at `HeroBodySwapper.cs:351`) | PlayerPrefs **`anim.runCadence`**, default **1.5** = baked behavior (zero change), clamp 0.5–3.0. Applies `anim.speed = 0.5 × (value/1.5)` ONLY while base layer is in Locomotion/InjuredLocomotion and not in transition (`:104-110`); restores 0.5 on exit so ShapeAttackTempo/attack pacing is untouched. Dev-panel "Animation (feel)" buttons nudge it live | Landed, **never felt-tuned** — the knob the owner can turn tomorrow without a rebake |
| Enemy anti-chop smoothing | `Enemy.cs:846-854` (`AnimSpeedDampSecs = 0.12` at `:214`) | Exponential-smooths the Speed feed (raw agent velocity + position-delta estimator `:835-843` from commit 92abcd6b "slide in combat") before `SetFloat`; <0.02 settles to true 0 | Landed, **felt-check pending** |
| See-through joints | `HeroBodySwapper.cs` (+`_Cull`=0 double-sided; uncommitted) | Tripo open-shell fix, not motion | pending |
| KayKit side-by-side proof | `Assets/_Modules/DevTools/KayKitAnimProof.cs`, buttons at `DevPanelController.cs:809-810` | **DevPanel → "Animation (feel)" → "Spawn KayKit Knight (anim proof)"** (editor/dev-build only). Loads the KayKit Knight via AssetDatabase, spawns 2 m right of the hero with the `HumanoidEnemy` controller + a scripted 4 m-square walker at 1.6 m/s (1.5 s idle pauses) so Idle↔Move exercises like a real enemy. "Despawn KayKit proof" removes it | Ready to fire — the A/B for §6b |

---

## 5. Open wounds

### 5.1 "Walk animation is horrible" — the FULL multiplier chain in one view

Every multiplier between input and visible stride (hero):

| # | Stage | Value | Source |
|---|---|---|---|
| 1 | Input → target velocity | `move × _moveSpeed(=6, hardcoded in Awake) × HeroHealth.MoveSpeedMultiplier(1 healthy)` | `HeroLocomotion.cs:344, 643` |
| 2 | Velocity easing | `MoveTowards`, accel 55 / decel 45 m/s² (`:345-346, 644-647`) → 0→6 m/s in ~0.11 s | `HeroLocomotion.cs` |
| 3 | Drive | `NavMeshAgent.Move()` each frame (input-driven; agent.speed=30/accel=200 are non-capping, `:359-360`); **not** SetDestination | `HeroLocomotion.cs:352-366` |
| 4 | Animator feed | `Speed = Velocity.magnitude` raw, **no damping** (`SetFloat` at `:845`, `:1023`; seam-crossing feeds constant `_moveSpeed` at `:750`) | `HeroLocomotion.cs` |
| 5 | Blend tree | 1-D on Speed: idle@0(×1) / walk@2(×2) / run@6(×3) | Knight.controller |
| 6 | Global playback | `anim.speed = 0.5` | `HeroBodySwapper.cs:343` |
| 7 | Runtime knob | ×(`anim.runCadence`/1.5), default 1.5 → ×1.0 | `HeroLocomotionCadence.cs:109` |
| 8 | Root motion | OFF (`applyRootMotion=false`) — clips play in place, ALL translation from step 3 | `HeroBodySwapper.cs:339` |

**Worked example @ 6 m/s travel (full stick):** Speed=6 → 100 % run child →
`Shared_Run_Forward` (28 frames ≈ 0.93 s/cycle at 1×) at effective playback
**timeScale 3 × anim.speed 0.5 × knob 1.0 = 1.5× real-time** → one stride cycle every **0.62 s**
while covering **3.7 m of ground**. A Mixamo in-place run's authored stride is ~2.5–3 m/cycle, so
~20–30 % residual foot-skate at default knob. Raising `anim.runCadence` to ~1.9–2.0 makes the
cycle 0.47 s ≈ 2.9 m/cycle — near skate-free, one PlayerPrefs write.

**Why it still reads "horrible" — three structural findings, not tuning:**
1. **Keyboard input never walks.** Full stick snaps Speed 0→6 in ~0.11 s (step 2), so the walk
   band (@2) is a ~0.07 s transient. The "walk animation" the owner sees on WebGL/joystick is the
   **mid-band blend** (e.g. 4 m/s = 50/50 walk+run mix).
2. **The mid-band mixes two unsynced clips at different timeScales.** Walk(×2, 34-frame cycle) and
   run(×3, 28-frame cycle) have unmatched foot-phase and now *unmatched playback rates* — a 1-D
   blend of cadence-mismatched clips churns legs. This is the class of ugliness per-child
   timeScale *creates* even as it fixes skate at the poles. (Fix options: cycle-offset matching,
   or a walk clip whose cadence at ×2 matches run at ×3, or collapse to idle/run only.)
3. **The chain is 3 layers deep for one number** (bake ×2/×3 canceling a global ×0.5 modulated by a
   runtime knob normalized to 1.5). Any future edit to one layer silently re-breaks the others —
   candidate for collapsing to ONE authoritative cadence table (owner thinks in data structures:
   a locomotion-cadence JSON row per state beats three multipliers in three files).

### 5.2 "Enemies still dying in air"

The death path (`Enemy.cs Die() :1953-1984`): stop agent → **release transform ownership +
disable agent** (`:1964-1968`, fix #55: a live agent re-wrote corpse Y every frame) →
**`SnapBodyToGround()` FIRST** (`:1975`; impl `:2186-2226`: raycast down from pos+2 m on
Default/Terrain/Ground, grounds the **visible bottom** via `PivotToVisibleBottomGap()`, navmesh
`SamplePosition` fallback) → `_actor.Die()` sets `Dead` Bool → FlowTrace step. Pool return keeps a
per-frame settle during the death hold (3.5 s, `:149`).

So a snap DOES exist and was capture-proven for the pivot case. If floats persist, the un-eliminated suspects are:
1. **Flyers** — `_isFlying` enemies legitimately die at altitude; nothing animates a fall (the
   clip `Falling Back Death`/`Shared_Death` plays in place at death height).
2. **`footGap` measured mid-animation** — `PivotToVisibleBottomGap()` reads renderer bounds at the
   death instant; a raised-leg pose inflates the gap → body seated `footGap` too high.
3. **Layer mask miss** — snap rays only Default/Terrain/Ground; corpses over props/walls on other
   layers fall through to the navmesh fallback (navmesh can sit above/below the visual surface).
4. **KayKit-family controllers with missing Death clips** (§2.3) — no death motion at all reads
   as "froze in the air mid-run".
Next instrument: the FlowTrace line at `:2215` already prints ground/footGap/pivotY per death —
one felt-repro + F8 gives the numbers that pick between (2) and (3).

### 5.3 "Slide and pulled back" (rubber-banding)

- **Hero:** input drives `agent.Move()` — the NavMeshAgent **constrains to the navmesh**, so
  walking into an unbaked edge/stale bake = the visible "pulled back" (memory:
  `rebake-navmesh-after-terrain-change`). Seam crossings warp+cooldown (`_seamReengageAt`,
  `HeroLocomotion.cs:891` region) — mistuned endpoints historically ping-ponged (WO-593 diff, now
  navmesh-snapped). The hero has **no SetDestination path-following** — no classic band.
- **Enemy slide:** the position-delta estimator (`Enemy.cs:830-843`) exists precisely because
  agent `velocity` reads ~0 under `SetDestination`+formation drift; before smoothing the Speed feed
  popped bands (walk@1.5/run@3.5). Anti-chop (§4) is the fix awaiting felt-verify. Residual
  suspect: `Mathf.Lerp`-smoothed feed **lags direction reversals** by ~0.12 s → brief moonwalk on
  about-faces; and enemy trees have **no cadence compensation at all** (timeScale 1 everywhere) —
  orc feet skate by the same math the hero just got fixed for (run clip 1× at 3.5 m/s travel).

### 5.4 Caster cast clip (generic)

Enemy cast today = `Assets/Action/Spell Cast.fbx` (OrcHumanoid `Cast` state; _Mage also overrides
its Attack to the same clip) + `WindUp` = `Standing 2H Magic Attack 01`. Telegraph work landed in
commit 18313270 (wind-up floor + orb). **Wizard-set candidates on disk** (`Assets/Action/Wizard/`):
`Standing 1H Magic Attack 01/02/03`, `standing 1H cast spell 01`, `Standing 2H Cast Spell 01`,
`Standing 2H Magic Attack 01–05`, `Standing 2H Magic Area Attack 01/02`, `Wizard_Spell_Cast`,
`Wizard_Heal`, `standing idle`. KayKit alternates: `Ranged_Magic_Spellcasting(_Long)`,
`Ranged_Magic_Raise/Shoot/Summon` (Generic-rig — enemy-side only if a KayKit body is used).

### 5.5 Awaiting felt-verify (nothing here is closed)

Enemy anti-chop + estimator, hero cadence bake + knob, see-through-joints cull fix, mage telegraph
— all in the uncommitted tree; none PO-closed. §13 pipeline: CLI verified headless only.

---

## 6. Options for tomorrow, pre-analyzed

### (a) Tune the existing Mixamo chain — cheapest, minutes per iteration
| Knob | Where | Range to try |
|---|---|---|
| `anim.runCadence` | PlayerPrefs, dev-panel "Animation (feel)" buttons, live | 1.5 (today) → **1.8–2.1** (skate-free @6 m/s per §5.1 math); walk scales with it |
| Walk threshold | `HeroAnimatorFactory.cs:256` + rebake | @2 → **@3–3.5** widens the pure-walk band on analog input |
| Kill the mid-band churn | rebake | Either drop the walk child (idle@0/run@4) or phase-sync walk/run (`m_CycleOffset`) |
| `HeroAnimSpeed` | `HeroBodySwapper.cs:32` (recompile) | Collapse it to 1.0 and remove the ×2/×3 bake = ONE layer (then attack states need their 1.3 speeds re-tuned ÷2 — this is the "stop fighting yourself" refactor) |
| Enemy cadence | `BuildOrcHumanoidController.cs` + rebake | Give orc trees the same per-child timeScale treatment (they have none) |

### (b) KayKit retarget for the hero
- **Blocked as-is:** KayKit = **Generic** (animationType 2), Tripo Knight = **Humanoid** (3) —
  Generic clips don't retarget to a Humanoid avatar. Options: (i) re-import KayKit rigs as
  Humanoid (uncertain — stylized bone proportions may not map to the humanoid avatar), or
  (ii) **swap the hero body to the KayKit Knight** and use the 131-clip library natively
  (the art-direction call flagged in memory `kaykit-character-library-uncatalogued`).
- **What the side-by-side shows:** KayKit clip quality on the KayKit body next to the Tripo hero —
  it proves the *library*, not the *retarget*. Fire it first (§4, one button).
- Also mind §2.3: KayKit guids are untracked — any KayKit-clip decision needs the pack made
  build-safe (import step or Addressables), or fresh clones ship Missing clips.

### (c) Root-motion switch
Today `applyRootMotion=false` (`HeroBodySwapper.cs:339`); ALL translation comes from
`NavMeshAgent.Move()` fed by input. Turning root motion on breaks, in order: the input-velocity
model (speed would come from the clip, so `_moveSpeed`/`MoveSpeedMultiplier`/accel tuning all dead),
navmesh constraint (needs `OnAnimatorMove` → route `anim.deltaPosition` through `agent.Move` to
keep the surface clamp), the manual seam-link stepping (`stepLen = _moveSpeed × dt`,
`HeroLocomotion.cs:878`), and the Speed-param feed (must switch to input magnitude, not velocity).
Foot-skate goes to zero by construction, but this is a locomotion-model rewrite, not a tune —
**not a tomorrow move** under polish-phase rules.

### (d) Per-state clip replacement priorities under the master framing
| Priority | State | Gap | Ready clips |
|---|---|---|---|
| 1 | hostile(prebattle) weapon-drawn stance | NO state; `InCombat` param pre-drilled both controllers | `Shared_Combat_Idle` (loop), `draw sword 1/2` as the transition, `sword and shield idle` |
| 2 | activebattle combat locomotion | NO stance-gait; calm walk/run plays in combat | `sword and shield walk/run/strafe` set (Knight folder, unused) |
| 3 | caster cast | generic `Spell Cast.fbx` | Wizard set §5.4 |
| 4 | enemy victory/taunt on hero-death | nothing | `standing taunt battlecry`/`chest thump` (Knight folder), KayKit `Cheering` |
| 5 | turn-in-place | nothing (yaw slew only, WO-423 comment `:649-654`) | `Shared_Turn_*`, `standing turn left/right 90` |
Zero-clip states needing sourcing: none for the hero arc — every master-state slot has at least a
candidate clip already imported. The work is controller states + a posture driver, not asset hunting.

---

## 7. Ten-minute quick-start for the session

1. Dev panel → **"Animation (feel)"** → *Spawn KayKit Knight (anim proof)* — A/B the libraries live.
2. Same group: nudge **`anim.runCadence`** 1.5 → 1.8 → 2.0 while running — the only live knob.
3. Walk verdict triage: test on **analog/joystick partial input** (the mid-band blend, §5.1
   finding 2) vs full-stick (pure run) — they are different bugs.
4. Death float repro → F8: the FlowTrace `SnapBodyToGround(...)` line (`Enemy.cs:2215`) prints
   ground/footGap/pivotY — reads directly onto §5.2 suspects 2 vs 3.
5. Remember: everything animation-touched is **uncommitted** — bank or bounce before layering more.

---

## OWNER SPEC ADDENDUM (2026-07-03 afternoon — Knight hero package, BINDING for the controller build)

The dedicated Knight package (`Assets/HeroPackages/Knight/`, extracted clips in
`Animations/Extracted/`) ships **DIRECTIONAL DEATHS by owner design** — the death animation
is selected by the killing hit's direction relative to the hero, plus one special:

| Trigger | Death clip (owner mapping) |
|---|---|
| Hit from FRONT | `Signature_Death_Forward.anim` (Death - Forward.fbx — owner-mapped 07-03) |
| Hit from LEFT | left-hit death |
| Hit from RIGHT | right-hit death |
| Hit from BACK | back death |
| SPECIAL: assassinate | the assassinate take |
| DEFAULT / direction unknown | `Combat_Weapon_Combat_Movement_Locked_Death.anim` (Combat Movement Locked\Death.fbx — owner-mapped 07-03) |

Implementation shape (her data-table style): the postbattle/death node of the master posture
tree resolves through a small direction→clip lookup (dot/cross of attacker→hero vector vs
hero forward), assassinate flagged by the killing ability. NOT one canned death clip.
The eight extracted `*_Death*` takes map to this table — final clip-to-slot assignment is
the owner's to confirm against the takes (names alone don't say which is which; she knows).

Also owner-confirmed: the eight Death.fbx files are all DIFFERENT takes (not duplicates) —
the path-based extraction names preserved every one.

**Gap update (owner, 07-03):** pre-combat UNSHEATHE is covered — package `Sheathing Sword`
reversed (state speed -1) or the repo's `Assets/Action/Knight` draw-sword clips copied in;
owner also has the draw SFX (`swordraw-89023.mp3`, Downloads) to pair with the
hostile(prebattle) posture flip. Remaining gaps: victory pose, block, injured locomotion.

**Owner mapping (07-03):** hostile(prebattle) combat idle = `Standing Aim Idle 01`
(extracted: `Combat_Weapon_Combat_Movement_Locked_Standing_Aim_Idle_01.anim`) — owner
ruling; supersedes the SME's "rifle-flavored, dead weight" note. Prebattle sequence:
posture flip → unsheathe (reversed Sheathing Sword + draw SFX) → Standing Aim Idle 01 loop.

**Owner design note (07-03):** the WeaponSkill melee variants (Combo, Downward Slice,
Inward/Outward Slash, Stabbing, Swing, BackSwing, 360 High, GreatSword Swing, Fists) are
the SPECIAL-ABILITY animation pool — talent-tree/ability unlocks each get a signature
swing. Wire through the existing canonical `weaponskill-animations.json` seam
(ability id → clip key), pure data. All 61 package clips extracted as of 15:25.

**Owner binding rule (07-03):** ability→animation is bound AT THE SKILL (skill-tree level
data — the skill def row carries its clip key). The HUD hot-swap slots (quick-swap 1-4 /
Hero Loadout) inherit the animation with the skill automatically; slots know nothing about
clips. One definition, every surface follows — her lookup-table-over-control-flow pattern.

**OWNER RULING (07-03 ~16:00, CANON — supersedes the Tripo-Knight hero body):** the
Mixamo PALADIN (Knight_Hero.fbx in the hero package) IS the new hero body — owner's
words: "better render, better details." The dedicated-rig package is body + animations,
not animation-only. Downstream: HeroBodySwapper/visual pipeline targets the Paladin mesh
(sword/shield/helmet baked, Sword_joint/Shield_joint attachment bones); Tripo Knight
retires from the hero slot (enemies unaffected). **RESOLVED 2026-07-03 (Opus): integration
landed + committed — package shape = prefab-key (Resources/Heroes/KnightPackage.prefab behind
ff.heropackage); gap-fill = shared Action set. Remaining = felt-tune only: forwardYaw/height,
assassinate-clip confirm, EquipmentController duplicate-sword de-dup.**
