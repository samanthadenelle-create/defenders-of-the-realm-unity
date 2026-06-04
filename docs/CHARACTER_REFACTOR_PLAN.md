# Character Refactor Plan — the unified actor substrate (implementation-ready)

> Hand-off doc for **UI** (the coding agent). CLI compile-gates + commits.
> Expands `docs/CHARACTER_ARCHITECTURE.md` into a concrete, phased refactor grounded in the
> EXISTING code. **This is mostly EXTRACTION + UNIFICATION, not greenfield.** Hero, Enemy, and
> Pet all work today; every phase keeps the loop playable. Routes against `docs/NORTH_STAR.md`
> (data-driven content, the async-PvP / auto-battle arena) and `docs/ARCHITECTURE_NORTH_STAR.md`
> (load-bearing principles #1 data-driven, #3 deterministic/headless-simulatable, #4 swappable
> behind interfaces). The **brain seam** is the #3 enabler.

---

## 0. The one-paragraph thesis

Every actor that moves — Hero, Enemy, Pet, Townsfolk — already carries the same four organs:
a **mover** (NavMeshAgent for Hero+Enemy; kinematic for Pet+Townsfolk), an **Animator** driven
by movement/action state, a **health/damage hook** (`IDamageable` / `IDamageableStructure` /
`HeroHealth`), and **VFX routed through `VFXManager`**. The differences are *who decides*
(player input vs AI) and *what actions are available* (sword vs staff). The refactor extracts the
four shared organs into one `Character` component, splits the deciding into a swappable `Brain`,
routes every action through one `DoAction(actionId, target)` verb, and makes equipment + the entity
roster **data** (`CharacterDef` / `WeaponDef`) built by one `CharacterFactory`. Once decision is a
swappable brain, an `AIBrain` can drive a Hero — which is the async-PvP / offline-base-defense
substrate the North Star arena needs.

---

## 1. Reconciliation — what EXISTS today vs the gap

The substrate is ~70% present, scattered across per-entity classes. Reuse, do not rebuild.

| Concern | Today (reuse this) | Gap (the refactor) |
|---|---|---|
| **Nav (Hero)** | `HeroLocomotion` — **already a `NavMeshAgent`** driven by `agent.Move(step)` (step 1 DONE, 2026-05-30); world-relative input, accel/decel ramp, off-mesh fallback | Extract the mover into `Character`; keep the exact agent tuning (radius 0.4, updateRotation off, NoObstacleAvoidance) |
| **Nav (Enemy)** | `Enemy` — `NavMeshAgent` + `DriveNav()` with DEF-56 path throttle, `NavPathCoordinator` stagger, brain-target override seam (`SetBrainTargetPosition`) | Same mover, different brain; the override seam is the proto-brain — formalize it |
| **Nav (Pet)** | `Pet` (DeNelle.Pets) — **kinematic** `MoveToward` with accel/arrival damp, hunt/return | Character must support BOTH a NavMeshAgent mover and a kinematic mover (Pet stays kinematic for now) |
| **Nav (Townsfolk)** | `AmbientNPC` + `TownsfolkController` registry; idle sway, watch hero | Lightweight Character variant; no combat brain |
| **Animation** | All three drive an Animator by `Speed` float + action triggers (`Cast`/`Attack`/`Hit`/`Dead`/`Victory`); hashes cached; null-guarded; `GetComponentInChildren<Animator>()` + self-heal | Unify the param vocabulary + the resolve/self-heal into `Character` (already near-identical in each) |
| **Health / damage** | `IDamageable` (enemies, via `EnemyDamageable` adapter), `IDamageableStructure` (Heart/Building/Tower/Gate/Wall/HeroHealth), `HeroHealth`, `Pet.TakeDamage`, `DamageAttribution`, `IDamageTintable` — all in `DeNelle.Core.Combat` | Character exposes one health hook; keep the Core interfaces as the cross-module seam |
| **VFX routing** | `VFXManager` (singleton, pooled, quality-gated, procedural fallback) + `VFXCatalog` (`VFXType`→prefab); `AbilityVfxKit`, `VfxPool`, `EnemyTypeVfxSet` | This IS the action→VFX layer. `DoAction` calls `VFXManager.Play(...)`; no new VFX system |
| **Abilities (Hero)** | `HeroAbilities` — mana, 4 cooldown slots, `TryCast(slot)`, effect resolution, `AbilityCatalog` (abilities.json per class), `AimPointOverride`/`HealHandler` for DTT | This is the proto-ActionSet. Generalize `TryCast(slot)`→`DoAction(actionId,target)`; the staff's ActionSet = the magic class spell list |
| **Skin / body swap** | `HeroBodySwapper` — loads `Resources/Heroes/<slug>.fbx`, swaps mesh, retargets URP materials, normalizes height, re-caches animators, sets hero class | **Skin infra already exists.** This is `CharacterFactory`'s model+material step extracted; reuse wholesale |
| **Cosmetics / monetization** | `CosmeticApplier` (material/prefab/VFX swap by cosmetic id), `CosmeticCatalog`, `GlimmerCurrencyService`, `BattlePassManager`; `WalletService`/`CurrencyKind` | Equipment/skin layer plugs the existing `CosmeticApplier` in as the visual swapper — don't reinvent |
| **HUD ability bar** | `VillageHudController` (passive, DeNelle.HUD) exposes `AbilityRequested` event + `SetAbilitySlot/SetMana/SetAbilityCooldown`; `HeroAbilitiesHudBridge` wires it via reflection (Village↔HUD isolation); `HeroAbilityInput` (keys 1-4) | **Dynamic HUD binding already half-built** — `PushClassLoadoutIfChanged()` re-targets cells on class change. Extend "class changed" → "equipped weapon changed" |
| **Data SOs** | `EnemyData` (DeNelle.Data), `PetType`/`PetData`, `TacticalData`, `TowerData`, `CosmeticDef`, JSON catalogs (`AbilityCatalog`, `PetCatalog`, enemies.json) | `CharacterDef` / `WeaponDef` / `ActionDef` are NEW SOs that consolidate these; existing SOs become overlays/sources |
| **AI brain seam** | `EnemyBrain` (role targeting + tactical states) drives `Enemy` ONLY via `SetBrainTarget*` (no direct nav). `EnemyBehaviorTree`, `EnemyGroupCoordinator` | This is the proto-brain. Promote the "decider that pushes intent to a mover" pattern to a `Brain` base shared by Hero too |

**Single biggest reuse:** the **NavMeshAgent mover + Animator-from-movement is already live on both
Hero and Enemy** (`HeroLocomotion` and `Enemy` are near-identical movers). The unified nav step is
done. The refactor is extracting that shared mover and the action/VFX plumbing — not writing new
movement, animation, health, or VFX systems.

---

## 2. Target class structure

All new gameplay classes live in **`DeNelle.Village`** (it already references Core, Pets, Cosmetics,
Data, Audio). The **data SOs** live in **`DeNelle.Core.Data`** or **`DeNelle.Data`** so any assembly
can author/read them without referencing Village. HUD stays passive in `DeNelle.HUD` (reflection
bridge unchanged). See §5 for assembly rules.

### 2.1 `Character` — the shared substrate (new, in DeNelle.Village)

```
[DisallowMultipleComponent] sealed class Character : MonoBehaviour, IDamageable (optional)
  CharacterType Type            // Hero | Enemy | Pet | Townsfolk  (enum in Core.Data)
  ICharacterMover Mover         // wraps NavMeshAgent OR kinematic transform (strategy)
  Animator Animator             // resolved/self-healed exactly like HeroLocomotion.ResolveAnimator
  IHealth Health                // wraps HeroHealth / Enemy HP / Pet HP behind one hook
  Equipment Equipment           // current weapon + armor + skin -> the ActionSet
  CharacterDef Def              // the data it was built from (for respawn / snapshot)

  // movement API the Brain calls (intent in, locomotion out):
  void MoveIntent(Vector3 worldDir)         // player path (Hero) — drives Mover.Move(step)
  void SetDestination(Vector3 worldPos)     // AI path (Enemy/Pet) — drives Mover.SetDestination
  void Stop()
  Vector3 Velocity { get; }                  // feeds Animator "Speed" (already the pattern)

  // the ONE action verb (replaces TryCast / contact-attack / pet Attack):
  bool DoAction(int actionId, ICharacterTarget target)
     -> ActionDef a = Equipment.ActionSet.Resolve(actionId)
     -> if (!a.Ready) return false;                       // cooldown/resource gate
     -> Animator.SetTrigger(a.AnimTrigger)                // clip
     -> a.Effect.Execute(this, target)                    // damage/heal/spawn (reuses HeroAbilities resolve)
     -> VFXManager.Play(a.VfxType, ResolveVfxPoint(target))  // VFX by action
     -> return true;
```

`Character` is intentionally thin — it OWNS the organs and the two seams (`MoveIntent`/`DoAction`)
and delegates everything else. It does NOT decide (that's the Brain) and does NOT hold ability
math (that's `ActionEffect`, lifted from `HeroAbilities.ResolveEffect`).

`ICharacterMover` is the key abstraction so Pet can stay kinematic while Hero/Enemy use NavMeshAgent:
- `NavAgentMover` — wraps `NavMeshAgent`; `Move(step)` = `agent.Move` (Hero), `SetDestination` =
  throttled `agent.SetDestination` (Enemy, port DEF-56 throttle + `NavPathCoordinator`).
- `KinematicMover` — wraps `Pet.MoveToward`'s accel/arrival logic for pets/townsfolk.

### 2.2 `Brain` — the swappable decision layer (new)

```
abstract class Brain : MonoBehaviour
  protected Character Self;
  protected virtual void Awake() => Self = GetComponent<Character>();
  abstract void Tick();          // called from Character.Update or its own Update

PlayerInputBrain : Brain        // = HeroLocomotion input + HeroAbilityInput, merged
   Tick(): read WASD/stick -> Self.MoveIntent(dir);  read keys 1-4 -> Self.DoAction(slot, AimTarget)

EnemyAIBrain : Brain            // = EnemyBrain role/tactical logic, retargeted onto Character
   Tick(): ChooseTarget() -> Self.SetDestination(dest);  in-range -> Self.DoAction(attackId, target)

PetBrain : Brain                // = Pet hunt/return + leash
   Tick(): NearestHostile() -> SetDestination/DoAction; field clear -> SetDestination(homePost)

TownsfolkBrain : Brain          // = AmbientNPC idle/watch; no DoAction
```

**Load-bearing property:** `PlayerInputBrain` and `EnemyAIBrain` call the **identical**
`Self.DoAction(...)`. Swapping a Hero's `PlayerInputBrain` for an `EnemyAIBrain` makes the Hero
self-drive with the same animations + VFX — that is the async-PvP / auto-battle substrate.

### 2.3 `Equipment` + `Weapon` + `ActionSet` (new)

```
class Equipment : MonoBehaviour
  WeaponDef Weapon; ArmorDef Armor; SkinDef Skin;
  ActionSet ActionSet { get; }                 // == Weapon.Actions, rebuilt on Equip
  event Action<ActionSet> OnLoadoutChanged;    // HUD bridge subscribes (replaces class-changed poll)

  void Equip(WeaponDef w):
     Weapon = w;
     ActionSet = ActionSet.Build(w.Actions);   // per-action runtime cooldown/resource state
     ApplyVisual(w.Skin);                       // reuse CosmeticApplier / HeroBodySwapper material path
     OnLoadoutChanged?.Invoke(ActionSet);       // -> HUD rebinds Q/F/E/R to this weapon's actions

ActionSet                                        // runtime wrapper over WeaponDef.Actions
  bool TryDo(int actionId, Character self, ICharacterTarget target)  // cooldown/mana gate, then ActionDef.Effect
  float CooldownFraction(int actionId)           // for the HUD sweep
```

`hero.Equipment.Equip(staffDef)` → ActionSet becomes the staff's spell list → `OnLoadoutChanged`
fires → HUD shows the spells. `hero.DoAction(0, target)` and `enemy.DoAction(0, target)` both flow
through `ActionSet.TryDo` → same clip+VFX path, only the `ActionDef` differs.

### 2.4 `CharacterFactory` (new, in DeNelle.Village)

```
static class CharacterFactory
  Character Create(CharacterDef def, Vector3 pos, Quaternion rot):
     1. Instantiate def.ModelPrefab (or Resources.Load like HeroBodySwapper) at pos/rot
     2. Add Character; set Type; build Mover (NavAgent vs Kinematic from def.MoverKind)
     3. Wire Health from def.Stats (HeroHealth / Enemy HP / Pet HP)
     4. Add the Brain named by def.BrainKind (PlayerInput | EnemyAI | Pet | Townsfolk)
     5. Add Equipment; Equip(def.Loadout.Weapon) -> ActionSet + visual
     6. If Type==Hero && BrainKind==PlayerInput: bind HUD (find VillageHudController, hook OnLoadoutChanged)
     7. Apply material/skin (reuse HeroBodySwapper.RetargetMaterialsToUrp + CosmeticApplier)
     return character;
```

ONE creation path. WaveManager calls `CharacterFactory.Create(enemyDef,...)` instead of
`Instantiate + Enemy.Configure`. PetDeployer, the hero builder, and townsfolk all route here over time.

---

## 3. Data model — "author a new actor = a new asset"

New SOs in `DeNelle.Core.Data` (authorable from any assembly; Village reads them). They consolidate
the scattered `EnemyData` / `PetData` / `AbilityCatalog` JSON / `CosmeticDef` into one shape.

```
[CreateAssetMenu(menuName="Defenders/Character Def")]
CharacterDef : ScriptableObject
  CharacterType Type            // Hero | Enemy | Pet | Townsfolk
  MoverKind     Mover           // NavAgent | Kinematic
  BrainKind     Brain           // PlayerInput | EnemyAI | Pet | Townsfolk
  GameObject    ModelPrefab     // or string resourcesPath (HeroBodySwapper-style)
  Stats         Stats           // maxHp, moveSpeed, baseDamage  (folds EnemyData/PetType)
  Loadout                       // WeaponDef weapon; ArmorDef armor; SkinDef skin
  // AI-only: EnemyRole role; TacticalData tactics  (reuse existing SO)

[CreateAssetMenu(menuName="Defenders/Weapon Def")]
WeaponDef : ScriptableObject
  string        Id              // "sword_iron", "staff_arcane"
  SkinDef       Skin            // mesh/material in-hand
  ActionDef[]   Actions         // the verbs this weapon grants
  // sword -> [Swing, Parry];  staff -> the magic class spell list (4 casts)

[CreateAssetMenu(menuName="Defenders/Action Def")]
ActionDef : ScriptableObject (or [Serializable] struct inside WeaponDef)
  int           ActionId        // stable slot/verb id
  string        DisplayName
  string        HudKey          // "Q"/"F"/"E"/"R" (or "1".."4")  -> HUD binding
  string        Glyph
  string        AnimTrigger     // "Swing"/"Cast"/"Parry"  -> Animator
  VFXType       Vfx             // -> VFXManager.Play
  ActionEffectKind Effect       // Strike | Aoe | Heal | Meteor | Snare | Parry (folds AbilityEffect)
  float         Damage, Range, Cooldown, ResourceCost
```

- **Sword** `WeaponDef`: `Actions = [ {Swing, AnimTrigger="Swing", Vfx=Impact_Physical, Effect=Strike},
  {Parry, AnimTrigger="Parry", Vfx=Impact_ShockwaveRing, Effect=Parry} ]`.
- **Staff** `WeaponDef`: `Actions` = the mage class' 4 spells, sourced from today's `abilities.json`
  (Arcane Bolt/Frost Nova/Healing Beacon/Meteor) — `ActionEffectKind` maps 1:1 to the existing
  `AbilityEffect` enum, so `ActionEffect.Execute` is `HeroAbilities.ResolveEffect` lifted verbatim.

**A new enemy/pet/weapon = a new asset.** Motion, animation, health, VFX, brain plumbing are
inherited from `Character` + `Brain` + `CharacterFactory`. This is North Star principle #1 applied
to actors (CoC adding Dragons = author data, not systems).

---

## 4. The brain seam — why it's load-bearing

Splitting *decision* (Brain) from *embodiment* (Character) is the single highest-value seam:

1. **Async PvP / auto-battle (North Star arena).** "Author BOTH sides" needs a Hero that runs
   without a live player. With the seam, you attach an `EnemyAIBrain`-style attacker brain to a
   Hero-type Character → it fights itself, identical clips+VFX. No second engine (North Star: "a
   *mode* on the loop, not a new engine").
2. **Deterministic / headless-simulatable combat (ARCHITECTURE_NORTH_STAR #3 — the expensive
   retrofit).** A Brain that consumes a target list and emits `MoveIntent`/`DoAction` intents is
   already the headless-friendly shape: swap input devices/`Time` for a fixed tick and the same
   brains replay a snapshot server-side. Building the seam now is the cheap insurance against the
   PvP-forces-a-rewrite risk.
3. **Smart targeting + tactics ladder.** The North Star's targeting/maneuver tiers live entirely in
   Brain subclasses (`EnemyAIBrain` already has role + tactical state). `Character` never changes as
   AI gets smarter — exactly the "1% addition, 100% meta shift" content runway.
4. **It already exists in proto form.** `EnemyBrain` drives `Enemy` ONLY through
   `SetBrainTargetPosition` — it never touches the agent directly. Formalizing that into a `Brain`
   base that Hero also uses is a small, proven step, not a leap.

---

## 5. Phased migration — each phase ships, loop stays playable

Assembly rules held throughout (CLAUDE.md §5): **Village → Core only; HUD passive (Core seam);
Pets does not reference Village.** New gameplay classes go in `DeNelle.Village`; new SOs in
`DeNelle.Core.Data`/`DeNelle.Data`; HUD binding stays the reflection bridge. `Character` may
implement `DeNelle.Core.Combat.IDamageable` (Core interface) so it slots into existing target sweeps.

### Phase 0 — Foundations (no behavior change)
- Add enums to `DeNelle.Core.Data`: `CharacterType`, `MoverKind`, `BrainKind`, `ActionEffectKind`
  (alias of existing `AbilityEffect`). Add `ICharacterMover`, `ICharacterTarget`, `IHealth`
  interfaces (Core).
- **Don't break:** nothing references these yet. Pure additive. Compiles, loop unchanged.

### Phase 1 — Extract the mover (DONE for nav unification; now formalize)
- Create `NavAgentMover` + `KinematicMover` wrapping the EXACT logic already in `HeroLocomotion`
  (agent.Move + tuning), `Enemy.DriveNav` (DEF-56 throttle + NavPathCoordinator), `Pet.MoveToward`.
- Leave `HeroLocomotion`/`Enemy`/`Pet` calling the new movers internally (delegate, don't delete).
- **Don't break:** keep `HeroLocomotion`'s off-mesh fallback, the -90° facing correction, the
  victory-pose suppression, and Enemy's autoRepath=false. Validate movement feel unchanged.

### Phase 2 — Introduce `Character`, adopt on Enemy first (lowest risk)
- Add `Character` component. Make `Enemy` *host* a `Character` (or have `Character` wrap Enemy's HP)
  exposing Type=Enemy, the NavAgentMover, the Animator, and Health via the existing
  `EnemyDamageable`/`IDamageable`.
- `EnemyBrain` keeps working unchanged (it already only pushes destinations).
- **Don't break:** `WaveManager.Configure`, breach roster (`EnemyId`/`EngineDefId`), death VFX/XP/
  Glimmer path in `Enemy.Die`, the contact-attack probe. Character wraps; Enemy logic stays.

### Phase 3 — Split the Brain
- Create `Brain` base; implement `EnemyAIBrain` by retargeting `EnemyBrain`'s `ChooseTarget`/
  `ComputeTacticalDestination` onto `Character.SetDestination` + `Character.DoAction(attackId)`.
- Implement `PlayerInputBrain` = `HeroLocomotion` input loop + `HeroAbilityInput` merged, calling
  `Character.MoveIntent` + `Character.DoAction`.
- **Don't break:** keep both old components present and authoritative until the brain is validated
  in play; flip the hero/enemy prefabs to the brain ONLY after parity is confirmed. EnemyBrain's
  `EnemyBehaviorTree`/`EnemyGroupCoordinator` hooks must still fire.

### Phase 4 — The action verb + ActionSet + Equipment
- Lift `HeroAbilities.ResolveEffect` into `ActionEffect.Execute` (same OverlapSphere/NearestHostile/
  Blast math, same `IDamageable`/`DamageAttribution` calls, same `AimPointOverride`/`HealHandler`).
- Add `Equipment` + `ActionSet`; build the **sword** and **staff** `WeaponDef`s. Route
  `Character.DoAction` → `ActionSet.TryDo` → `ActionEffect` + `VFXManager.Play` + Animator trigger.
- `HeroAbilities.TryCast(slot)` becomes a thin shim calling `DoAction(slot)` (keeps existing input +
  HUD bridge working during migration).
- **Don't break:** mana/cooldown semantics, DTT `AimPointOverride`/`HealHandler`, talent/level
  multipliers (`HeroTalentModifiers`, `HeroProgression`), `AttackTimingBonus`, the two combat-feel
  stacks (route VFX through `VFXManager` only; don't double-fire VfxPool + VFXManager — see §6).

### Phase 5 — Dynamic HUD binding to equipped weapon
- Extend `HeroAbilitiesHudBridge`: subscribe to `Equipment.OnLoadoutChanged` instead of (or in
  addition to) `PushClassLoadoutIfChanged`. On change, push `ActionDef.HudKey/Glyph/DisplayName`
  per slot via the existing `SetAbilitySlot` reflection call. `AbilityRequested(slot)` →
  `Character.DoAction(slot, AimTarget)`.
- **Don't break:** HUD stays passive in `DeNelle.HUD`; all Village↔HUD calls stay reflection
  (no new asmdef edge). Mana/cooldown sweep push unchanged.

### Phase 6 — `CharacterFactory` becomes the single creation path
- WaveManager → `CharacterFactory.Create(enemyDef)`. PetDeployer → factory (kinematic mover, PetBrain).
- Hero build (currently `VillageSceneBuilder.BuildHero` + `HeroBodySwapper`) → factory, reusing
  `HeroBodySwapper`'s mesh-swap/material-retarget as the factory's visual step.
- **Don't break:** `VillageSceneBuilder.cs` is the serialization bottleneck (CLAUDE.md §9) and is
  being edited by another agent — coordinate via WO; the factory is called FROM the builder, the
  builder's scene authoring is untouched. Keep `Enemy.Configure` as the factory's stat-apply step.

### Phase 7 — Townsfolk + cleanup
- `TownsfolkBrain` (idle/watch) on a kinematic Character; `TownsfolkController` keeps coordinating.
- Once brains are authoritative, slim `HeroLocomotion`/`HeroAbilities`/`Enemy`/`Pet` to delegate
  shells or fold them in. Do this LAST, only after every phase validated in play.

---

## 6. Risks + guardrails

- **Never big-bang.** Hero/Enemy/Pet WORK today. Every phase leaves old components present and
  authoritative until parity is confirmed in play; only then flip prefabs to the new path. The repo
  history is littered with re-save / singleton-dedup / mount-sync disasters (see MEMORY.md) — small,
  reversible steps only.
- **Two combat-feel stacks (MEMORY: two-combat-feel-stacks).** There are DEF-stack
  (`VfxPool`/`CombatFeedbackManager`/`CameraShakeBridge`) and WO-stack (`VFXManager`/`HitStopManager`)
  paths. `DoAction` must route through **`VFXManager.Play` only** and let the existing per-entity
  code keep its hit-feel calls — do NOT add a second VFX/shake fire per action or hits double up.
- **Pet stays kinematic.** `ICharacterMover` exists precisely so Pet keeps `MoveToward` (no NavMesh
  dependency / leash behavior) while Hero+Enemy use NavMeshAgent. Don't force Pet onto an agent.
- **Animation stays movement/action-driven.** It already is on all three (`Speed` float + triggers).
  Keep the resolve/self-heal (`ResolveAnimator`, the post-body-swap re-cache) — these guard the
  T-pose/"sliding statue" bugs. `Character` owns the self-heal; don't drop it.
- **HUD stays passive, Core seam intact.** No `DeNelle.Village` ↔ `DeNelle.HUD` direct reference;
  binding stays the reflection bridge. No new `System.Reflection` in *bridge scripts beyond what
  exists* (the existing reflection bridges are fine; don't add reflection inside `Character`/`Brain`).
- **Core can't reference Village.** `CharacterDef`/`WeaponDef`/enums go in `Core.Data`. Brains/
  Character/Factory go in Village. Anything Pets needs stays on Core interfaces (`IDamageable`).
- **DTT overrides must survive.** `AimPointOverride` + `HealHandler` (PatriciaLight) are how the
  turret hero aims; preserve them as `ActionEffect` context, or DTT breaks.
- **Brace gate + CLI compile-gate every `.cs`** (CLAUDE.md §1). UI writes via Write/Edit on the
  Windows path only; CLI build-verifies and sole-commits.

---

## 7. Work-order breakdown (hand-off queue)

Each WO is one UI implementation + one CLI compile-gate. Numbered from the current ceiling (106+).
Order respects the phases; the loop stays playable after each.

- **WO-106 — Core character contracts.** Add `CharacterType`/`MoverKind`/`BrainKind`/
  `ActionEffectKind` enums + `ICharacterMover`/`ICharacterTarget`/`IHealth` to `DeNelle.Core.Data` /
  `DeNelle.Core.Combat`. Pure additive. (Phase 0)
- **WO-107 — Movers.** `NavAgentMover` + `KinematicMover` wrapping existing `HeroLocomotion` /
  `Enemy.DriveNav` / `Pet.MoveToward` logic verbatim; entities delegate. (Phase 1)
- **WO-108 — `Character` component + adopt on Enemy.** Wrap Enemy's HP/agent/animator; implement
  `IDamageable`; keep `Enemy`/`EnemyBrain` authoritative. (Phase 2)
- **WO-109 — `Brain` base + `EnemyAIBrain`.** Retarget `EnemyBrain` onto `Character.SetDestination`/
  `DoAction`; behind a prefab flag, off by default. (Phase 3a)
- **WO-110 — `PlayerInputBrain`.** Merge `HeroLocomotion` input + `HeroAbilityInput` onto
  `Character.MoveIntent`/`DoAction`; flag-gated. (Phase 3b)
- **WO-111 — `ActionEffect` extraction.** Lift `HeroAbilities.ResolveEffect` (+ Blast/NearestHostile,
  talent/level/timing mults, AimPointOverride/HealHandler) into `ActionEffect.Execute`. (Phase 4a)
- **WO-112 — `Equipment` + `ActionSet` + `WeaponDef`/`ActionDef` SOs.** Author the sword + staff
  defs; `DoAction`→`ActionSet.TryDo`; `HeroAbilities.TryCast` becomes a shim. (Phase 4b)
- **WO-113 — Dynamic HUD binding.** `HeroAbilitiesHudBridge` subscribes to
  `Equipment.OnLoadoutChanged`; rebind Q/F/E/R on equip via existing `SetAbilitySlot`. (Phase 5)
- **WO-114 — `CharacterDef` SO + `CharacterFactory`.** One `Create(def)` path; reuse
  `HeroBodySwapper` material/mesh step + `CosmeticApplier` for visuals. (Phase 6a)
- **WO-115 — Route WaveManager + PetDeployer through the factory.** Keep `Enemy.Configure`/breach/
  death-XP intact as factory steps; coordinate VillageSceneBuilder via WO. (Phase 6b)
- **WO-116 — `TownsfolkBrain` + kinematic Character for NPCs.** (Phase 7a)
- **WO-117 — Slim legacy components to delegates.** Only after full play-validation. (Phase 7b)
- **WO-118 — (stretch) AI-driven Hero proof.** Attach `EnemyAIBrain` to a Hero-type Character in a
  test scene → self-fighting hero (the async-PvP substrate smoke test). (validates the seam)

---

*End of plan. Build it as extraction, validate each rung in play, keep the brain seam clean — that
seam is the bridge from "village TD" to the North Star arena.*
