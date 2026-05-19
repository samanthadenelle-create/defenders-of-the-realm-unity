# Week 4 — Hero Abilities + Starter Pets + Gate Force-Field

**Date:** 2026-05-19
**Slice:** v2-unity-port-spec.md Part 5 Week 4 — the hero ability kit (Q/W/E/R), the three starter pets deployed near the Heart, and the cardinal-gate force-field that collapses below 25% HP.
**Status:** Source files written. Integration items below are open (no Unity access — cannot build prefabs, assign LayerMasks in the inspector, create the force-field material, or wire the scene).

## Files produced

| File | Purpose |
| ---- | ------- |
| `Assets/StreamingAssets/Data/Canonical/abilities.json` | Hero ability defs — Q/W/E/R per class. v2 reads `mage` (Blaise). |
| `Assets/StreamingAssets/Data/Canonical/pets.json` | The three starter guardian pets + per-bond-rank stat rows. |
| `Assets/_Modules/Village/Hero/AbilityCatalog.cs` | Typed `AbilityDef`/`AbilityClassDef` records + static `AbilityCatalog` loader. |
| `Assets/_Modules/Village/Hero/HeroAbilities.cs` | Q/W/E/R cast block — mana pool, cooldowns, cast resolution, placeholder particle VFX. |
| `Assets/_Modules/Pets/PetCatalog.cs` | Typed `PetDef`/`PetBondRank` records + static `PetCatalog` loader (incl. `petPost()` deploy-ring geometry). |
| `Assets/_Modules/Pets/Pet.cs` | One guardian-pet MonoBehaviour — hunts + attacks the nearest enemy in range. |
| `Assets/_Modules/Pets/PetDeployer.cs` | Spawns the three starter pets at slots ringing the Heart. |
| `Assets/_Modules/Core/Combat/IDamageable.cs` | **The cross-module combat seam** — shared damageable contract (see below). |
| `Assets/_Modules/Village/Enemies/EnemyDamageable.cs` | Adapter exposing the village `Enemy` as `IDamageable`. |
| `Assets/Shaders/ForceFieldGate.shader` | URP-compatible violet gate-shimmer shader with an HP-driven `_Collapse` property. |
| `Assets/_Modules/Village/Gates/Gate.cs` | **Extended** (Week-3 skeleton → Week-4 gameplay): takes damage, drives the shader collapse, toggles the blocker. |

No asmdef changes were needed. `DeNelle.Village` and `DeNelle.Pets` both already reference `DeNelle.Core`, so the new `DeNelle.Core.Combat` namespace is visible to both. `VillageController.cs` was **not** edited — scene wiring is the integrator's job.

## Sourced vs. authored data

**Sourced verbatim from the React v1 repo** (read-only — nothing written there):

- **Mage abilities** — `src/modules/village/hero/abilities/mage.ts` (`MAGE_ABILITIES`). Field map: `cd → cooldown`, `mana → manaCost`, `power → damage`, `reach → range`. Exact values: Q Arcane Bolt (cd 0.5 / mana 0 / dmg 16 / range 13), W Frost Nova (cd 12 / mana 3 / dmg 26 / radius 5.2 / freeze 1.4), E Healing Beacon (cd 16 / mana 4 / heal 48), R Meteor Strike (cd 45 / mana 7 / dmg 80 / radius 6). Effect kinds + colours/icons also verbatim.
- **Cast resolution math** — `src/modules/village/hero/castAbility.ts`. `HeroAbilities.ResolveEffect` is line-equivalent: cooldown+mana gate, `nearest()` for strike/snare/meteor, `blast()` radius hit-test with the `ENEMY_HIT_R = 0.85` pad, heal → Heart HP.
- **Mana regen** — `src/modules/village/scene/heroAbilities.ts`: `0.9/s × manaRegenMul`; pool 0–10. Carried as `_manaRegenPerSecond` / `_maxMana` with a `ManaRegenMultiplier` hook for the Aether pet's Mana Tide perk.
- **Pets** — `src/modules/pets/petData.ts` (`PETS`, `petAttack(rank)=9+rank*5`, `PET_HUNT_SPEED=4.4`, `PET_ATTACK_RANGE=2.7`, `PET_ATTACK_CD=0.75`, `BOND_STAGES`, `PET_PERKS`, `PET_PERK_DESC`, `petPost()` ring at radius 11) and `src/lib/aggression.ts` (`petMaxHp` — per-species bond-scaled HP: Aether `60+rank*20`, Flame `80+rank*25`, Ice `100+rank*30`). All three pet names, elements, archetypes, tints, the five bond stages, and all 12 perk names + descriptions are verbatim.

**Authored (defensible values, not in the React repo):**

- **Knight + Ranger ability sets** in `abilities.json` — the React repo has `knight.ts` / `ranger.ts` but the Week-4 brief only requires the Mage. Authored placeholder loadouts marked `"_comment": "AUTHORED placeholder"` so v2.1 can re-sync from a future v1 `data/abilities.json` (port spec Part 8). v2 foundation reads `mage` only, so these are inert until then.
- **Damage element mapping** — abilities.json has no element field in v1. `HeroAbilities.ElementOf` maps Frost Nova→Ice, Meteor→Flame, the rest→Aether for future resist math. Reversible; cosmetic until enemies carry per-element resistances.
- **Force-field collapse ramp** — the React `Gate.tsx` has no shader; the collapse-below-25% curve (`_Collapse` eases 0→1 as HP falls through the 25%→0% band) is authored to satisfy the port-spec acceptance ("force field visibly collapses below 25% HP").

## Module-isolation resolution (port spec Part 2) — the key design call

**Problem:** Pets attack enemies. The `Enemy` type lives in `DeNelle.Village`. The `DeNelle.Pets` asmdef must **not** reference `DeNelle.Village` (and vice-versa) — port spec Part 2 forbids one gameplay module's asmdef referencing another's.

**Decision: a shared `IDamageable` interface in `DeNelle.Core`.**

- `Assets/_Modules/Core/Combat/IDamageable.cs` defines `IDamageable` (`Faction`, `WorldPosition`, `Hp`, `IsAlive`, `TakeDamage`, `ApplyStatus`) plus the `CombatFaction` / `DamageElement` / `StatusEffect` enums. It has zero scene coupling beyond a world position.
- Both `DeNelle.Village` and `DeNelle.Pets` already reference `DeNelle.Core`, so no asmdef edits were needed.
- `Pet.cs` discovers targets via `Physics.OverlapSphere` on an enemy `LayerMask`, then `GetComponentInParent<IDamageable>()`. It never names `DeNelle.Village.Enemy`.
- `HeroAbilities.cs` is in `DeNelle.Village` so it *could* touch `Enemy` directly — but it deliberately uses the **same** `IDamageable` seam, keeping the cast code engine-agnostic and consistent with the Pets path.
- `EnemyDamageable.cs` (a new `DeNelle.Village` file) adapts the existing `Enemy` to `IDamageable`. It sits on the Enemy GameObject; `GetComponentInParent<IDamageable>()` finds it.

**Alternatives considered:**
1. *Keep pet-vs-enemy combat on the Village side* (a Village-owned `PetCombatDriver` reaches into pets). Rejected — it inverts ownership: pet AI is a Pets-module concern, and the React project keeps pet AI in `PetSprite`.
2. *`Pet : IDamageable` only, Village queries pets.* Doesn't solve the reverse direction (pets damaging enemies).
3. *A `DeNelle.Combat` shared asmdef.* Heavier than needed — `DeNelle.Core` already exists and is referenced by both.

The interface seam (option chosen) is the lowest-friction, mirrors the React combat-registry abstraction, and is the pattern future modules (towers, traps) should reuse. **A decisions-log row should be added to `docs/unity-decisions.md` by the owner/integrator** — this notes file does not edit that log per the task brief.

**Why `EnemyDamageable` is a separate adapter, not `Enemy : IDamageable` directly:** the cleanest end state is for `Enemy` to implement `IDamageable` itself. The Week-4 brief says "create NEW files" and leaves `Enemy.cs` to the integrator, so the bridge ships as a new component. The integrator may fold it into `Enemy` and delete the adapter — both forms satisfy the same contract.

## Integrator wiring checklist (no Unity access here)

1. **Enemy prefab** — add `EnemyDamageable` to the Enemy prefab (or add `[RequireComponent(typeof(EnemyDamageable))]` to `Enemy`). Without it, **hero abilities and pets cannot find or hit enemies.** Optionally fold the interface into `Enemy` directly and delete `EnemyDamageable.cs`.
2. **Enemy LayerMask** — put enemies on a dedicated layer (e.g. `Enemy`). Assign that mask to `HeroAbilities._enemyMask`, `Pet._enemyMask` (or via `Pet.SetEnemyMask`), and `PetDeployer._enemyMask`.
3. **HeroAbilities** — add the component to the hero rig (Blaise). Call `HeroAbilities.SetHeart(villageController.Heart)` so Healing Beacon (E) can heal. Wire the Input System Q/W/E/R actions to `TryCast(AbilitySlot.Q/W/E/R)`. HUD reads `CooldownFraction` / `Mana` / `CanCast`.
4. **PetDeployer** — add to the village scene. Call `SetHeartPosition(villageController.Heart.transform.position)`, `SetEnemyMask(enemyMask)`, `SetBondRanks(aether, flame, ice)` from `GameState.petBonds` (indexed `[Aether, Flame, Ice]`), then `DeployStarterPets()` once the scene is up.
5. **Pet prefab** — optional. If unset, `PetDeployer` builds tinted placeholder capsules (KayKit pet meshes import later, port spec Part 7). Placeholder pet colliders are triggers so pets do not block enemy pathing.
6. **Force-field material** — create a URP material using `DeNelle/Village/ForceFieldGate`, assign it to the gate's force-field sheet renderer, and wire that renderer into `Gate._forceFieldRenderer`. `Gate` drives the `_Collapse` property at runtime via a `MaterialPropertyBlock`.
7. **Gate damage** — `Gate` already implements `TakeDamage` / `Repair` / `SetHp`. The integrator should make `Gate` implement the existing `IDamageableStructure` (in `Enemy.cs`) so enemies' contact attacks route into `Gate.TakeDamage` — i.e. `ApplyContactDamage(amount) => TakeDamage(amount)`. Then enemies can wear a gate down and pour through on collapse.
8. **Mana Tide perk** — when the Aether Sprite reaches bond rank 2, raise `HeroAbilities.ManaRegenMultiplier` (React used a multiplier on the 0.9/s regen). The deeper per-rank perks (burn, novas, freeze) are later wiring; `Pet.Attack` already applies the Ice Wolf's rank-1 Frostbite slow as the one perk with a clean `IDamageable` hook today.

## Known limitations / later passes

- **Status effects** — `EnemyDamageable.ApplyStatus` records freeze/slow/burn expiry timers and exposes `IsFrozen` / `IsSlowed` / `IsBurning`, but `Enemy.cs` does not yet read them into nav speed. The integrator hooks these into the NavMeshAgent speed when `Enemy` gains status fields (mirrors the React `EnemyRuntime.freezeUntil` / `slowUntil`).
- **VFX** — abilities use Unity built-in `ParticleSystem` bursts as placeholders (port spec Week 4 explicitly allows this). Final ability VFX art is a later pass.
- **Pet movement** — `Pet` uses kinematic `MoveTowards` drift. If pets should respect the baked NavMesh, the integrator swaps in a `NavMeshAgent` (the asmdef can already see `UnityEngine.AI`).
- **Pet bond perks** — only the Ice Wolf Frostbite slow and the Aether Mana Tide hook are represented. Twinspark / Heartbloom / Emberbite / Pyre Bond etc. need the village combat registry + FX, which is deeper Week-4+ work.
- **Android StreamingAssets** — `AbilityCatalog` / `PetCatalog` read `abilities.json` / `pets.json` synchronously via `File.ReadAllText`, valid in the Editor + on Windows/macOS. On Android, StreamingAssets is inside the APK and needs a `UnityWebRequest` read — same caveat already noted for `PackCatalog.cs` / `Theme.cs`; to be addressed with the Week-7/8 Seeker build.
- **Schema tests** — port spec Part 4 calls for a `SchemaTests.cs` per data file. `abilities.json` / `pets.json` schema tests should be added alongside the existing data-file tests.
