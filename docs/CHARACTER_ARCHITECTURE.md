# Character Architecture — the unified actor substrate

> Target architecture (owner-driven, 2026-05-30). Everything that moves in the world —
> Hero, Enemy, Pet, Townsfolk — converges on **one `Character` substrate**. Type flags the
> differences; a swappable **brain** decides; a universal **action verb** drives animation + VFX.
> This is not cleanup — it's the foundation for the two-sided combat / auto-battle vision in
> `NORTH_STAR.md` (model both worlds, automate attack strategy).

## The shape

```
Character (shared MonoBehaviour)
  ├─ NavMeshAgent        — navigation on the SHARED NavMesh (one "walkable" everywhere)
  ├─ Animator            — animation pulled from MOVEMENT/action state (not bespoke per type)
  ├─ Health              — IDamageableStructure
  ├─ VFX hooks           — hit / death / cast / action, routed through VFXManager by (type, action)
  └─ CharacterType enum  — Hero | Enemy | Pet | Townsfolk  (selects clips + VFX + tuning)

Brain (swappable decision layer — the valuable seam)
  ├─ PlayerInputBrain    — reads WASD/stick, drives the Character (today: HeroLocomotion)
  └─ EnemyAIBrain        — pathfinds / strategises, drives the SAME Character (today: EnemyBrain)
```

**Why the brain seam matters:** if the only difference between hero and enemy is the brain,
then an **AIBrain can drive a Hero-type Character** → async PvP, auto-battle, "your base
defends itself offline." That's CoC's two-sided combat, for free, once the substrate is shared.

## The action verb (owner: `hero.Fire(flyingTarget)`)

One universal call, type+action select the specifics, animation + VFX fire automatically:

```
character.Do(ActionType.Fire, target);
   // -> Animator: play the Fire clip for this CharacterType
   // -> VFXManager.Play(vfxFor(type, Fire), at/toward target)
```

`hero.Fire(flier)` and `enemy.Fire(hero)` resolve through the **same code path** — the verb is
universal, only the clip + VFX id differ by type/action. The AIBrain calls the identical `Fire`
the player's input does → **identical animation + VFX on both sides.** That symmetry IS the engine.

## Equipment drives the action set (owner: `hero.weapon`)

The **weapon defines what a character can do** — its own action enum, clips, VFX/spells, and the
HUD bindings. Equipping a weapon **reconfigures the character + the HUD dynamically.**

```
WeaponDef (data)
  ├─ ActionSet            — the verbs this weapon grants + their clip + VFX/spell + cooldown
  │     sword  -> { Swing, Parry }                       (swing clip + slash VFX; parry clip + block)
  │     staff  -> references the Magic class' spell list (cast clips + spell VFX)
  ├─ HUD bindings         — Q/F/E/R map to THIS weapon's actions; rebinds on equip
  └─ skin/model           — the visual (sword vs staff in hand)

character.Equip(weaponDef)
   -> character's available actions = weaponDef.ActionSet
   -> HUD ability buttons rebind to those actions (hero.Equip(staff) -> HUD shows the spells)
character.DoAction(actionId, target)   // routed through the equipped weapon's ActionSet
```

So `hero.weapon = sword` gives Swing/Parry; `hero.weapon = staff` pulls the magic class, casts
spells, and **the HUD buttons rewire themselves to match** — no hard-coded ability bar.

**Armor + skins** are the same idea, lighter: visual swap (mesh/material) ± stat modifiers, so
re-skinning a hero/enemy/pet is data, not code. (Cosmetics monetization plugs in here.)

## Data-driven: define it, the factory inherits the rest

A **`CharacterFactory`** is the single creation path for everything that moves. You author a
**`CharacterDef`** (data) and the factory wires the shared substrate from it — "select a new
enemy / pet / anything and the rest is inherited."

```
CharacterDef (ScriptableObject / data)
  ├─ CharacterType        — Hero | Enemy | Pet | Townsfolk
  ├─ skin/model           — the visual
  ├─ stats                — health, speed, ...
  ├─ brain                — PlayerInput | EnemyAI | PetFollow   (the decision layer)
  └─ loadout              — WeaponDef(s) + armor

CharacterFactory.Create(def)
  -> instantiate model, add Character (NavMeshAgent + Animator + Health + VFX), attach Brain,
     Equip loadout, bind HUD if player. ONE path; type/def selects the differences.
```

A new enemy = a new `CharacterDef` + maybe a new `WeaponDef`. Motion, NavMesh, animation, VFX,
brain plumbing are **inherited from the shared base** — no new controller class per entity. This
is the [NORTH_STAR] "data-driven, not hand-authored" principle applied to actors, and it's how
the meta-counter engine stays cheap to extend (CoC adding Dragons = author data, not systems).

## Content model: Catalog (the look) + Repo (the properties)

Adding anything — by a designer in the builder or a player in build-mode — is one call:
`home.AddFrom(catalog, "stairs")`, and it composes two delegated sources:

- **Catalog** → the *appearance*: which prefab/mesh/skin. (The polyperfect prefab catalog is already
  exactly this; cosmetics/skins plug in here.)
- **Repo** → the *properties/behavior*: the `NavSurface` plank, stats, climb logic, build cost, the
  `WeaponDef`/`ActionSet` for an actor, etc. (data assets).
- **Dispatcher** → composes them: select "stairs" and you inherit the look (catalog) **and** the invisible
  nav plank + properties (repo), assembled with **zero per-item code**.

Decoupling look from behavior is the payoff: **re-skin without touching logic** (swap the catalog entry),
**re-tune behavior without touching art** (swap the repo entry). And because both halves are *inherited*,
a player adding from the catalog gets a complete, working object — which is the mechanical reason
**player-created content reuses the same engine** (build-mode = the dispatcher pointed at player picks).

## Control scheme + camera (the player-side layer)

The brain's PLAYER variant needs a pluggable **input scheme** so the same Character works across
devices with consistent controls, and the view is its own swappable piece:

- **InputScheme (typed, swappable)** — `LeanTouchScheme` (mobile touch), `KeyboardScheme`,
  `GamepadScheme`. Each feeds `PlayerInputBrain` a uniform *intent* (move dir + action triggers),
  so **everyone gets identical HUD controls regardless of platform.** The HUD control set is a
  function of (input scheme × equipped weapon's action set) — bind once, works everywhere.
- **Camera (one shared controller, modes by context)** — fixed-angle follow today; **pan/zoom**
  (drag-to-pan / pinch, CoC-style survey) is a **mode in the SAME `CameraController`**, not a
  separate feature. Combat → follow; build/survey → pan+zoom. Built once in the engine, so **every
  hero/player inherits it** — panning is a universal capability, never a one-off bolt-on.

Neither touches the shared Character substrate — they *drive* it. Same principle as everything
else here: **typed + swappable + consistent everywhere; build it in the engine, all inherit it.**

## Migration — incremental, never big-bang

Each step keeps every entity working; validate (playtest) before the next.

1. ✅ **Nav substrate unified** — hero on a `NavMeshAgent` on the shared NavMesh
   (`HeroLocomotion`, 2026-05-30). Enemies already there. *Step 1 — validate movement feel.*
2. **Rampart/vertical proof** — bake stairs + a rampart walkway walkable; confirm hero climbs
   AND enemies path up to attack a defender on top. (Proves the shared mesh tactically.)
3. **Extract `Character` base** — pull nav + animation-from-movement + health + VFX hooks out of
   `HeroLocomotion` / `EnemyBrain` / `Pet` into one component; add `CharacterType`.
4. **Split the brain** — `PlayerInputBrain` / `EnemyAIBrain` both drive `Character`. This is the
   seam the auto-battle AI plugs into later.
5. **Action verb layer** — `Character.Do(action, target)` routing clips + VFX by (type, action).

## Guardrails
- Hero/Enemy/Pet/Townsfolk all **work today** — refactor toward this, don't rewrite in one shot.
- Animation stays **movement/action-driven** (it already is for both — keep it).
- Keep the decision layer (input vs AI) cleanly separable — it's the load-bearing seam.
- Reconcile with [[two-combat-feel-stacks]]; route against `docs/NORTH_STAR.md`.
