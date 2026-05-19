# ATB Combat Engine — C# Port Specification (Week 2)

Read-only analysis of the React/TypeScript ATB engine for the Unity 6 / C# port of
*Defenders of the Realm*. This document is the line-by-line port contract for the
`src/lib/atb/*` modules. **The port is a literal equivalent — do not change logic.**
Genuine bugs are recorded under "Flags" at the end; they are NOT to be silently fixed.

- Source root (LOCKED — read only): `C:\Users\Kayden-Laptop\Documents\defenders-of-the-realm\src\lib\`
- Target root: `C:\Users\Kayden-Laptop\Documents\defenders-unity\Assets\_Modules\BattleATB\Engine\`

---

## 0. Files analysed

| Source file | Status | Target C# file |
|---|---|---|
| `src/lib/atb/types.ts` | present | `Types.cs` |
| `src/lib/atb/rng.ts` | present | `Rng.cs` |
| `src/lib/atb/targeting.ts` | present | `Targeting.cs` |
| `src/lib/atb/actions.ts` | present | `Actions.cs` |
| `src/lib/atb/ai.ts` | present | `Ai.cs` |
| `src/lib/atb/combat.ts` | present | `Combat.cs` |
| `src/lib/atb/turn.ts` | present | `Turn.cs` |
| `src/lib/atb/state.ts` | present | `BattleState.cs` |
| `src/lib/atb/defs.ts` | present | `Defs.cs` (+ ScriptableObjects) |
| `src/lib/atbEngine.ts` | present | re-export barrel — see §11 |
| `src/lib/atbEngine.test.ts` | present | port to NUnit/EditMode tests — see §12 |

**Supporting dependency files** (outside the brief, but the engine imports them — port required):

| Source file | Status | Notes |
|---|---|---|
| `src/data/battleScaling.ts` | present | `waveScaling`, `isBossWave`, `bossHpMul`, `bossOrdinal`, `BOSS_EVERY`. See §10. |
| `src/content/story.ts` | present | provides `HeroClass = 'mage' \| 'knight' \| 'ranger'`. |
| `src/data/gameDesign.ts` | present | provides `PetSpecies = 'aether-sprite' \| 'flame-pup' \| 'ice-wolf'`. |

No files were missing.

---

## 1. Engine dependency DAG & namespace

The TS engine is a strict downward-only DAG (declared in `atbEngine.ts`):

```
rng < types < defs < state < targeting < {combat, ai} < actions < turn
```

Preserve this. Suggested namespace: `Defenders.BattleATB.Engine`. All files compile
into one asmdef (`Defenders.BattleATB.Engine.asmdef`) with **no UnityEngine dependency
in the pure-logic files** except `Defs.cs` if ScriptableObjects are used (see §9).

### Global C# porting conventions (apply to every file)

- TS `number` is an IEEE-754 double. Use C# `double` for all gameplay math (ATB fill,
  damage spread, multipliers) **except** the RNG internals (§2) and integer counters.
  Do **not** use `float` — `float` rounding would diverge from the TS reference.
- TS `Math.round` uses **round-half-up toward +∞** (`Math.round(2.5) === 3`,
  `Math.round(-2.5) === -2`). C# `Math.Round` defaults to **banker's rounding**
  (round-half-to-even). **You MUST replace every `Math.round` with a helper**:
  `static int RoundTs(double x) => (int)Math.Floor(x + 0.5);`
  This matters in `applyHeal`, `applyResource`, `calculateDamage`, `tickStatuses`,
  `buildHeroUnit`, `buildPetUnit`, `buildEnemyUnit`.
- TS `Math.floor` → `Math.Floor` (identical for the finite positive values used here).
- TS `Math.max`/`Math.min` → `Math.Max`/`Math.Min` (`double` overloads).
- TS `??` (nullish coalescing) → explicit null/`HasValue` checks. Note `?? 1` on an
  optional numeric field must treat **only `null`/`undefined`** as missing — `0` is a
  real value and must NOT be coalesced away.
- TS object/array spread used for cloning → explicit copy (see `CloneBattle`, §8).
- `Record<K, V>` → `Dictionary<K, V>` (or a typed struct table where keys are enums).
- String-literal unions → C# `enum`. Keep the string spellings available for the log
  text (`StatusKind` is interpolated into log strings, e.g. `"afflicted with burn"`).
  Provide a `ToString()`-style mapping that yields the lowercase TS spelling.
- All "pure helper" modules become `static class`es of `static` methods.
- `BattleState` and `BattleUnit` are mutated in place by the engine — port them as
  **mutable reference types (`class`)**, not structs. Small value bundles
  (`StatusEffect`, `Rng`, `DamageResult`, `BattleLogEntry`) may be `class` too for
  simplicity; do NOT make `Rng` a struct unless every call site passes it `ref`
  (the TS code mutates `rng.seed` through a shared reference — see §2).

---

## 2. `rng.ts` → `Rng.cs` — DETERMINISTIC PRNG (anti-cheat critical)

> The port must be **bit-for-bit reproducible**: the same `seed` must yield an
> identical float sequence in C# as in TS. This section is exhaustive.

### 2.1 Algorithm identified: **mulberry32**

A 32-bit single-state PRNG. State is one `uint32` (`Rng.seed`), which is also the
cursor — snapshot/restore it to replay a battle. The exact TS:

```ts
export interface Rng { seed: number; }

export function createRng(seed: number): Rng {
  return { seed: seed >>> 0 };
}

export function rngNext(rng: Rng): number {
  let t = (rng.seed = (rng.seed + 0x6d2b79f5) >>> 0);
  t = Math.imul(t ^ (t >>> 15), t | 1);
  t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
  return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
}
```

### 2.2 Exact integer semantics — how each TS op maps to C#

`rng.seed` is the live state and must be a **`uint`** (unsigned 32-bit) in C#. `t` is
a working value that must also be held as **`uint`** so every shift/xor/multiply wraps
mod 2^32 exactly as the TS `>>>`/`Math.imul` do.

| TS expression | Meaning | C# (with `uint seed`, `uint t`) |
|---|---|---|
| `seed >>> 0` | coerce to uint32 | `unchecked((uint)seed)` — see note on the `int` seed below |
| `(rng.seed + 0x6d2b79f5) >>> 0` | add, wrap to uint32 | `t = unchecked(seed + 0x6d2b79f5u);` then `seed = t;` |
| `t >>> 15` | **logical** right shift | `t >> 15` (C# `>>` on `uint` is already logical) |
| `t | 1`, `t | 61` | bitwise OR | `t \| 1u`, `t \| 61u` |
| `t ^ x` | bitwise XOR | `t ^ x` |
| `Math.imul(a, b)` | 32-bit signed integer multiply, low 32 bits | `unchecked(a * b)` on `uint` operands (low 32 bits identical to `Math.imul`) |
| `t + Math.imul(...)` | add, stays in JS number (may exceed 32 bits) | see §2.3 — must wrap |
| `(t ^ (t >>> 14)) >>> 0` | final value as uint32 | `(t ^ (t >> 14))` (already `uint`) |
| `/ 4294967296` | divide by 2^32 → float in [0,1) | `(double)value / 4294967296.0` |

### 2.3 The ONE subtle line — `t ^= t + Math.imul(t ^ (t >>> 7), t | 61);`

In TS this is evaluated as a JS `number` (double): `Math.imul(...)` returns a value in
the signed int32 range, `t` is currently a uint32-range value, and their **sum can be
up to ~2^33** — held exactly as a double. The `^=` then re-coerces the whole thing
back to **int32** (JS bitwise operators coerce both operands to int32 via ToInt32,
which is `value mod 2^32` taken as signed).

In C#, if `t` is `uint` and you compute `t + (a * b)` with all `uint`, the addition
**already wraps mod 2^32** under `unchecked` — which is exactly the low-32-bits result
`^=` would produce. So:

```csharp
public sealed class Rng { public uint Seed; }

public static Rng CreateRng(uint seed) => new Rng { Seed = seed };

public static double RngNext(Rng rng)
{
    unchecked
    {
        uint t = (rng.Seed = rng.Seed + 0x6D2B79F5u);
        t = (uint)((t ^ (t >> 15)) * (t | 1u));
        t ^= t + (uint)((t ^ (t >> 7)) * (t | 61u));
        return (double)(t ^ (t >> 14)) / 4294967296.0;
    }
}
```

Wrap the whole body in `unchecked` so multiply/add overflow silently truncates instead
of throwing — this reproduces JS `Math.imul` + ToUint32 exactly. **Verified mentally**:
`Math.imul`'s contract is "the low 32 bits of the true integer product"; an `unchecked`
`uint * uint` in C# yields exactly the low 32 bits. The intermediate-sum width does not
matter because only the low 32 bits survive the `^=`/`>>> 0`.

### 2.4 The seed type — `createRng(seed: number)`

`BattleSetup.seed` is a TS `number`. `createRng` does `seed >>> 0`, which is
**ToUint32**: for any finite number it is `(((floor(seed) mod 2^32) + 2^32) mod 2^32)`.
In practice seeds passed are small non-negative integers (`0xa11ce`, `0xbeef`, `1..12`).

Port `BattleSetup.seed` as `long` or `int`, and have `CreateRng` perform the ToUint32
coercion explicitly: `unchecked((uint)seedValue)` works for any `int`; if `seed` can be
a large/fractional value, replicate ToUint32 fully. For the values this engine actually
uses, `unchecked((uint)seed)` is exact. Recommend `int seed` on `BattleSetup` and a
documented assumption that seeds are non-negative 32-bit integers.

### 2.5 Derived functions

```ts
rngInt(rng, min, max)  => min + Math.floor(rngNext(rng) * (max - min + 1));
rngChance(rng, p)      => rngNext(rng) < p;
rngPick(rng, list)     => list.length === 0 ? null : list[rngInt(rng, 0, list.length-1)];
```

C# port:

```csharp
public static int RngInt(Rng rng, int min, int max)
    => min + (int)Math.Floor(RngNext(rng) * (max - min + 1));

public static bool RngChance(Rng rng, double p) => RngNext(rng) < p;

public static T RngPick<T>(Rng rng, IReadOnlyList<T> list) where T : class
    => list.Count == 0 ? null : list[RngInt(rng, 0, list.Count - 1)];
```

- `RngInt` arithmetic: `min`, `max` are `int`; `RngNext` is `double`; the product is
  `double`; `Math.Floor` → `double`; cast to `int`. Since `RngNext < 1`, the result is
  in `[min, max]` inclusive — matches TS. Keep `double` for the multiply.
- `rngChance(p)` — note the call sites pass `statusChance ?? 1` etc. The comparison is
  `< p`; `p === 1` is always-true except `RngNext` never returns exactly 1, so
  effectively always-true. Preserve `<` (not `<=`).
- `rngPick<T>` returns `null` for an empty list. T is always a reference type
  (`BattleUnit`) at call sites, so `where T : class` is fine and returning `null` is
  literal. If a value-type instantiation is ever needed, switch to `bool TryPick(...)`.

### 2.6 Divergence flags for `Rng.cs`

- **F-RNG-1 (must-fix-in-port-to-stay-faithful):** `Rng.seed` MUST be `uint`. Using
  `int` and signed `>>` would corrupt every draw.
- **F-RNG-2:** every arithmetic op in `RngNext` MUST be inside `unchecked`. A checked
  context throws `OverflowException` on the multiplies/add.
- **F-RNG-3:** `4294967296.0` must be a `double` literal; integer division would zero
  the result.
- **F-RNG-4:** `Rng` must be a shared mutable reference (`class`), because TS mutates
  `rng.seed` through aliased references (`state.rng` is handed to many helpers and they
  all advance the *same* cursor). A `struct` passed by value would fork the stream.

---

## 3. `types.ts` → `Types.cs`

Type-only module. Port as enums + plain classes. **No behaviour.**

### 3.1 Enums (string-literal unions)

| TS union | C# enum | Members (TS spelling) |
|---|---|---|
| `Side` | `enum Side` | `Party`, `Enemy` |
| `ActionKind` | `enum ActionKind` | `Attack`, `Ability`, `Item`, `Defend`, `Rally` |
| `AbilitySlot` | `enum AbilitySlot` | `Q`, `W`, `E`, `R` |
| `ElementType` | `enum ElementType` | `Physical`, `Aether`, `Flame`, `Ice` |
| `StatusKind` | `enum StatusKind` | `Burn`, `Poison`, `Bleed`, `Slow`, `Freeze`, `Stun`, `Regen`, `Haste`, `Shield`, `Mark` |
| `ItemKind` | `enum ItemKind` | `Potion`, `ManaCrystal`, `Cleanse` (TS strings `potion`/`mana_crystal`/`cleanse`) |
| `PetAiMode` | `enum PetAiMode` | `Aggressive`, `Defensive`, `Balanced` |
| `BattleOutcome` | `enum BattleOutcome` | `None`, `Victory`, `Defeat` (TS `null` → `None`) |
| `BattlePhase` | `enum BattlePhase` | `Intro`, `Filling`, `AwaitingInput`, `Resolving`, `Ended` |
| `TargetMode` | `enum TargetMode` | `SingleEnemy`, `AllEnemies`, `RandomEnemies`, `Self`, `SingleAlly`, `AllAllies` |
| `EnemyArchetype` | `enum EnemyArchetype` | `Grunt`, `Caster`, `Tank`, `Boss` |
| `HeroClass` (from story.ts) | `enum HeroClass` | `Mage`, `Knight`, `Ranger` |
| `PetSpecies` (from gameDesign.ts) | `enum PetSpecies` | `AetherSprite`, `FlamePup`, `IceWolf` (TS `aether-sprite`/`flame-pup`/`ice-wolf`) |

`BattleLogEntry.event` is a 14-member union — port as `enum BattleLogEvent`:
`BattleStart, TurnStart, Attack, Ability, Item, Defend, Rally, StatusTick, StatusApply,
StatusExpire, Death, Skip, Victory, Defeat`.

Keep a `static string ToToken(this StatusKind)` etc. helper that returns the exact TS
lowercase/hyphenated spellings — they are interpolated verbatim into `BattleLogEntry.text`
and into `cleanseStatuses`'s `removed.join(', ')`.

### 3.2 Interfaces → plain classes (mutable where the engine mutates them)

`StatusEffect` — **mutable class** (`potency`/`turns` are mutated in `tickStatuses`,
`applyStatus`):
```csharp
class StatusEffect { StatusKind Kind; int Turns; double Potency; }
```
`turns` is integer-valued; `potency` is `double` (`mark` blueprint potency is `0.3`).

`AbilityDef` — immutable data class (lives in static tables / ScriptableObject).
Fields: `AbilitySlot Slot; string Name; int Cost; int CooldownTurns; TargetMode Target;
ElementType Element; int Damage; int? Hits; int? Heal; double? HealPctSelf;
StatusKind? ApplyStatus; double? StatusChance; StatusKind? SelfStatus;
double? ArmorPierce; int? Splash;`
All `?`-typed TS fields → C# nullable. **Do not default `null` numerics to 0** — the
engine checks `ability.damage === 0` and `ability.heal && ability.heal > 0` separately.

`ItemDef` — immutable: `ItemKind Kind; string Name; int? Heal; int? RestoreResource;
bool? Cleanse; TargetMode Target` (`Target` is always `SingleAlly`).

`EnemyDef` — immutable; contains a nested `Special` type:
```csharp
class EnemyDef {
  string Id; string Name; EnemyArchetype Archetype;
  int BaseHp; int BaseAttack; double Speed; double Defense; ElementType Element;
  EnemySpecial Special; // nullable
}
class EnemySpecial {
  string Name; int Damage; TargetMode Target;
  StatusKind? ApplyStatus; double? StatusChance; int? SelfHeal;
}
```

`BattleUnit` — **mutable class**. Full field list (preserve every field):
`string Id; Side Side; string Name; UnitKind Kind` (`UnitKind` enum: `Hero, Pet, Enemy`);
`int Hp; int MaxHp; int Resource; int MaxResource; int ResourceRegen;`
`double Atb; double Speed; double Defense; int Attack; ElementType Element;`
`List<StatusEffect> Statuses; Dictionary<AbilitySlot,int> Cooldowns; bool Defending;`
`bool Alive;` then hero/pet-only optionals `HeroClass? HeroClass; PetSpecies? Species;
int? BondRank; PetAiMode? AiMode;` and enemy-only `EnemyArchetype? Archetype;
string EnemyDefId;`.
Note: `atb`, `speed`, `defense` are `double`; `attack`, `hp`, `resource` are `int`.

`RallyReserveUnit` — `PetSpecies Species; string Name; int BondRank; PetAiMode AiMode;`

`BattleLogEntry` — `int Turn; string SourceId; string TargetId; BattleLogEvent Event;
string Text; int? Amount; bool? Crit; StatusKind? Status;` (`sourceId`/`targetId` may be
`null` — keep `string` nullable).

`BattleState` — **mutable class**. Fields:
`int Wave; BattlePhase Phase; BattleOutcome Outcome; List<BattleUnit> Units;
List<RallyReserveUnit> Reserve; string ActiveUnitId; int TurnCounter; Rng Rng;
Dictionary<ItemKind,int> Inventory; List<BattleLogEntry> Log; bool ReinforcementsApplied;`

`BreachEnemySpec` — `string DefId; int? Hp; List<StatusEffect> Statuses;` (statuses nullable).
`PartyPetSpec` — `PetSpecies Species; string Name; int BondRank; PetAiMode AiMode;
bool JoinsImmediately;`
`BattleSetup` — `int Wave; int Seed; HeroClass HeroClass; string HeroName;
List<PartyPetSpec> Pets; List<BreachEnemySpec> Enemies;
Dictionary<ItemKind,int> Inventory (nullable); bool? Reinforcements;`

`DamageInput` — `BattleUnit Attacker; BattleUnit Target; int BasePower;
ElementType Element; double? ArmorPierce; bool? CanCrit; Rng Rng;`
`DamageResult` — `int Damage; bool Crit; bool Shielded;` (can be a `struct`; not mutated).

`BattleAction` — TS discriminated union of 5 shapes. Port as **one class with an
`ActionKind Kind` discriminant + nullable payload fields**, OR a small class hierarchy.
Recommended single-class form (simplest, matches the `switch` in `applyAction`):
```csharp
class BattleAction {
  ActionKind Kind;
  string TargetId;       // attack / item / ability(optional)
  AbilitySlot? Slot;     // ability
  ItemKind? Item;        // item
  int ReserveIndex;      // rally
}
```
Factory helpers `BattleAction.Attack(id)`, `.Ability(slot,id)`, `.Item(item,id)`,
`.Defend()`, `.Rally(index)` keep call sites readable.

---

## 4. `defs.ts` → `Defs.cs` (+ ScriptableObjects)

Pure static data — no logic. Two viable shapes:

**A. Plain `static class` of `static readonly` tables** (recommended for the literal
port — guarantees byte-identical values, no Inspector drift, easy to diff vs TS).
**B. ScriptableObjects** for designer-tunable data (`HERO_ABILITIES`, `PET_ABILITIES`,
`ENEMY_DEFS`, `HERO_STATS`, `PET_STATS`, `ITEM_DEFS`).

Recommendation: do **A first** for Week 2 (faithful port + tests), then optionally
back it with ScriptableObjects later. If SOs are used, author them so their serialized
values exactly equal the tables below, and load them through a `Defs` facade so engine
code stays SO-agnostic.

### 4.1 Constants

```
ATB_BASE_FILL  = 12      (int)   — declared, exported, but NOT referenced by the engine; see Flag F-DEFS-1
ATB_FULL       = 100     (int)
SLOW_FILL_MUL  = 0.5     (double)
HASTE_FILL_MUL = 1.5     (double)
ATB_RESET      = 0       (int)
MAX_PARTY      = 8       (int)
MAX_ENEMIES    = 8       (int)
CRIT_CHANCE    = 0.12    (double)
CRIT_MULT      = 1.6     (double)
BOSS_EVERY     = 6       (int, from battleScaling.ts)
```

### 4.2 `STATUS_BLUEPRINTS` — `Dictionary<StatusKind, StatusBlueprint>`

`StatusBlueprint { int Turns; double Potency; bool Buff; }`

| kind | turns | potency | buff |
|---|---|---|---|
| burn | 3 | 6 | false |
| poison | 4 | 4 | false |
| bleed | 4 | 3 | false |
| slow | 2 | 0 | false |
| freeze | 1 | 0 | false |
| stun | 1 | 0 | false |
| regen | 3 | 5 | true |
| haste | 3 | 0 | true |
| shield | 1 | 0 | true |
| mark | 3 | 0.3 | false |

### 4.3 `HERO_ABILITIES` — `Dictionary<HeroClass, AbilityDef[]>`

Knight: Q `Shield Slam` (cost 20, cd 1, single-enemy, physical, dmg 35, applyStatus
stun, statusChance 0.4) · W `Guard` (cost 0, cd 2, self, physical, dmg 0, selfStatus
shield) · E `Whirlwind` (cost 40, cd 3, all-enemies, physical, dmg 25) · R `Last Stand`
(cost 80, cd 6, self, physical, dmg 0, healPctSelf 0.5, selfStatus haste).

Ranger: Q `Pierce Shot` (cost 15, cd 1, single-enemy, physical, dmg 45, armorPierce
0.5) · W `Volley` (cost 30, cd 2, random-enemies, physical, dmg 25, hits 3) · E
`Hunter's Mark` (cost 20, cd 2, single-enemy, physical, dmg 0, applyStatus mark) · R
`Rain of Arrows` (cost 50, cd 5, all-enemies, physical, dmg 35, applyStatus bleed).

Mage: Q `Arcane Bolt` (cost 12, cd 0, single-enemy, aether, dmg 30) · W `Flameblast`
(cost 35, cd 2, single-enemy, flame, dmg 50, applyStatus burn) · E `Frost Nova` (cost
30, cd 3, all-enemies, ice, dmg 25, applyStatus freeze, statusChance 0.5) · R `Tempest`
(cost 70, cd 5, single-enemy, aether, dmg 80, splash 40).

### 4.4 `HERO_STATS` — `Dictionary<HeroClass, HeroClassStats>`

`HeroClassStats { int MaxHp; int MaxResource; int ResourceRegen; double Speed;
double Defense; int Attack; ElementType Element; }`

| class | maxHp | maxResource | resourceRegen | speed | defense | attack | element |
|---|---|---|---|---|---|---|---|
| knight | 180 | 100 | 6 | 0.85 | 0.25 | 24 | physical |
| ranger | 120 | 60 | 4 | 1.15 | 0.10 | 20 | physical |
| mage | 90 | 80 | 5 | 1.00 | 0.05 | 16 | aether |

### 4.5 `PET_STATS` — `Dictionary<PetSpecies, PetSpeciesStats>`

`PetSpeciesStats { int MaxHp; int Attack; double Speed; ElementType Element;
int MaxResource; int ResourceRegen; }`

| species | maxHp | attack | speed | element | maxResource | resourceRegen |
|---|---|---|---|---|---|---|
| aether-sprite | 70 | 22 | 1.00 | aether | 40 | 5 |
| flame-pup | 90 | 28 | 0.95 | flame | 40 | 5 |
| ice-wolf | 110 | 18 | 0.85 | ice | 40 | 5 |

### 4.6 `PET_ABILITIES` — `Dictionary<PetSpecies, AbilityDef[]>`

aether-sprite: Q `Twinspark` (cost 14, cd 1, single-enemy, aether, dmg 26) ·
W `Heartbloom` (cost 18, cd 2, all-allies, aether, dmg 0, heal 22, applyStatus regen).
flame-pup: Q `Emberbite` (cost 14, cd 1, single-enemy, flame, dmg 30, applyStatus burn)
· W `Pyre Bond` (cost 20, cd 3, all-enemies, flame, dmg 22, applyStatus burn,
statusChance 0.6).
ice-wolf: Q `Frostbite` (cost 14, cd 1, single-enemy, ice, dmg 20, applyStatus slow) ·
W `Glacial Bond` (cost 22, cd 3, single-enemy, ice, dmg 28, applyStatus freeze,
statusChance 0.5).

### 4.7 `ITEM_DEFS` — `Dictionary<ItemKind, ItemDef>`

potion `Healing Potion` (heal 40, target single-ally) · mana_crystal `Mana Crystal`
(restoreResource 25, target single-ally) · cleanse `Cleanse` (cleanse true, target
single-ally).

### 4.8 `ENEMY_DEFS` — `Dictionary<string, EnemyDef>` (keyed by string id)

| id | name | archetype | baseHp | baseAtk | speed | defense | element | special |
|---|---|---|---|---|---|---|---|---|
| goblin | Goblin | grunt | 60 | 14 | 1.00 | 0.05 | physical | Reckless Swing — dmg 22, single-ally |
| skeleton | Skeleton | grunt | 70 | 16 | 0.95 | 0.10 | physical | Bone Shard — dmg 18, single-ally, applyStatus bleed |
| bruiser | Bruiser | tank | 140 | 20 | 0.75 | 0.30 | physical | Patch Up — dmg 0, self, selfHeal 40 |
| necromancer | Necromancer | caster | 85 | 18 | 1.05 | 0.10 | aether | Hex — dmg 14, all-allies, applyStatus poison, statusChance 0.7 |
| hollow-captain | Hollow Captain | tank | 220 | 26 | 0.90 | 0.25 | physical | Rallying Cry — dmg 30, all-allies, applyStatus slow, statusChance 0.5 |
| hollow-king | Hollow King | boss | 420 | 34 | 1.00 | 0.30 | aether | Sovereign Wrath — dmg 44, all-allies, applyStatus burn, statusChance 0.6 |
| hollow-apprentice | The Apprentice of the Apothecary | boss | 175 | 24 | 1.00 | 0.12 | aether | Tincture — dmg 0, single-ally, applyStatus slow, statusChance 1 |

Note `special.target` uses `TargetMode` values `single-ally` / `self` / `all-allies` —
which, evaluated through `resolveTargets` from an enemy actor, resolve to **party**
units (see §5: `single-ally` for an enemy actor returns the actor itself unless... see
Flag F-ACT-1).

---

## 5. `targeting.ts` → `Targeting.cs`

`static class Targeting`. Two pure functions.

### `resolveTargets(state, actor, mode, explicitTargetId, rng, hits) → BattleUnit[]`

C#: `static List<BattleUnit> ResolveTargets(BattleState state, BattleUnit actor,
TargetMode mode, string explicitTargetId, Rng rng, int hits)`

`enemySide = actor.side == Party ? Enemy : Party`. Switch on `mode`:
- `single-enemy`: if `explicitTargetId` set and `getUnit` finds a living unit on
  `enemySide`, return `[that]`; else `rngPick` from `livingUnits(enemySide)`; empty list
  if pick is null.
- `all-enemies`: `livingUnits(state, enemySide)`.
- `random-enemies`: loop `i < hits && pool.Count > 0`; each iter `rngPick(pool)`, push
  if non-null. **Note the pool is NOT removed-from** — the same unit can be picked
  multiple times across hits (Volley can multi-hit one foe). Faithful; preserve.
- `self`: `[actor]`.
- `single-ally`: if `explicitTargetId` set and `getUnit` finds a living unit on
  `actor.side`, return `[that]`; else `[actor]`.
- `all-allies`: `livingUnits(state, actor.side)`.
- default: empty list.

Port note: TS `explicitTargetId: string | undefined` → C# `string` (null = absent).
`explicitTargetId` truthiness: TS treats `""` (empty string) as falsy — `chooseEnemyAction`
can produce `targetId: ''`. So the C# check must be `!string.IsNullOrEmpty(explicitTargetId)`,
**not** just `!= null`, to match TS `if (explicitTargetId)`.

### `adjacentUnitIds(state, unit) → string[]`

C#: `static List<string> AdjacentUnitIds(BattleState state, BattleUnit unit)`
Filter `state.units` to `unit.side`, find index of `unit` by id; push id of `idx-1`
(if `idx > 0`) and id of `idx+1` (if `idx >= 0 && idx < count-1`). Preserve order:
left neighbour first, then right.

---

## 6. `combat.ts` → `Combat.cs`

`static class Combat`. Pure damage/status logic that mutates units and logs.

### `elementMultiplier(attacker, defender) → number`
`static double ElementMultiplier(ElementType attacker, ElementType defender)`
Hard-coded RPS: flame>ice 1.25; ice>aether 1.25; aether>flame 1.25; ice→flame 0.85;
aether→ice 0.85; flame→aether 0.85; otherwise 1. Literal `if`-chain.

### `calculateDamage(input: DamageInput) → DamageResult`
`static DamageResult CalculateDamage(DamageInput input)`
Order of operations (RNG-sensitive — preserve **exactly**, the RNG advances inside):
1. `pierce = element == Aether ? 1 : Clamp(input.ArmorPierce ?? 0, 0, 1)`.
2. `effectiveDefense = target.Defense * (1 - pierce)`.
3. `elementMul = ElementMultiplier(element, target.Element)`.
4. `markMul = HasStatus(target, Mark) ? 1.3 : 1`.
5. `defendMul = target.Defending ? 0.5 : 1`.
6. `crit = input.CanCrit != false && RngChance(rng, CRIT_CHANCE)` — **first RNG draw**.
   Note `CanCrit != false`: a `null`/absent `CanCrit` counts as allowed-to-crit. In C#
   with `bool? CanCrit`, write `input.CanCrit != false` literally (true and null → crit
   allowed; only explicit false blocks it). **The `RngChance` call must happen even
   when `canCrit` is true** — `&&` short-circuits, so if `CanCrit == false` the draw is
   skipped. This is order-significant; port the `&&` faithfully so the stream matches.
7. `critMul = crit ? CRIT_MULT : 1`.
8. `raw = basePower * (1-effectiveDefense) * elementMul * markMul * defendMul * critMul`.
9. `spread = 0.92 + RngNext(rng) * 0.16` — **second RNG draw** (always happens).
10. `raw *= spread`.
11. `damage = Max(1, RoundTs(raw))` — **use the `RoundTs` helper, not `Math.Round`**.
12. If `HasStatus(target, Shield)` → return `{ damage: 0, crit, shielded: true }`.
    Else return `{ damage, crit, shielded: false }`.

> RNG-ordering flag **F-CMB-1**: the shield check happens *after* both RNG draws, so a
> shielded hit still consumes 1 (or 2) RNG values. Faithful — do not reorder.

### `applyDamage(target, result) → number`
`static int ApplyDamage(BattleUnit target, DamageResult result)`
If `result.Shielded`: `ConsumeStatus(target, Shield)`, return 0. Else
`target.Hp = Max(0, target.Hp - result.Damage)`; if `Hp == 0` → `Alive = false`;
return `result.Damage`.

### `applyHeal(target, amount) → number`
`static int ApplyHeal(BattleUnit target, double amount)`
If `!target.Alive || amount <= 0` return 0. `before = Hp`;
`Hp = Min(MaxHp, Hp + RoundTs(amount))`; return `Hp - before`.
Note `amount` is `double` (call sites pass `actor.maxHp * healPctSelf` and `status.potency`).

### `applyResource(target, amount) → number`
`static int ApplyResource(BattleUnit target, double amount)`
`before = Resource`; `Resource = Min(MaxResource, Resource + RoundTs(amount))`;
return `Resource - before`. **No alive check** (unlike `applyHeal`) — faithful.

### `applyStatus(unit, kind) → void`
If `!unit.Alive` return. Look up blueprint. If a status of `kind` already exists:
`existing.Turns = Max(existing.Turns, bp.Turns)`,
`existing.Potency = Max(existing.Potency, bp.Potency)` — **refresh, keep higher**.
Else push `new StatusEffect { Kind=kind, Turns=bp.Turns, Potency=bp.Potency }`.

### `consumeStatus(unit, kind) → void`
Remove **all** instances of `kind` from `unit.Statuses` (TS `.filter(s => s.kind !== kind)`
— despite the docstring "one charge", it removes every match). Port literally.

### `cleanseStatuses(unit) → StatusKind[]`
`static List<StatusKind> CleanseStatuses(BattleUnit unit)`
Keep statuses whose blueprint `Buff == true`; collect+remove the rest; return removed.

### `tickStatuses(state, unit) → boolean`
`static bool TickStatuses(BattleState state, BattleUnit unit)` — returns
"must skip turn". Logic:
- `skip = false`. Iterate `unit.Statuses` (the live list):
  - skip entries with `turns <= 0`.
  - `burn`/`poison`: `lost = Min(Hp, RoundTs(potency))`; `Hp -= lost`; if `Hp<=0` →
    `Alive=false`; `pushLog` status-tick, `amount = lost`.
  - `bleed`: same damage, log "bleeds for", **then `status.Potency += 1`** (bleed
    worsens; mutates the live effect).
  - `regen`: `healed = ApplyHeal(unit, potency)`; log, `amount = -healed`.
  - `freeze`/`stun`: `skip = true`.
  - default (slow/haste/shield/mark): nothing.
  - After the switch (for every non-`turns<=0` entry): `status.Turns -= 1`.
- Then collect `expired = statuses where Turns <= 0`, `pushLog` status-expire for each,
  and `unit.Statuses = statuses where Turns > 0`.
- Return `skip && unit.Alive`.

> Iteration note: TS iterates the array while later reassigning `unit.statuses`. The
> `for...of` runs over the *original* array reference; reassignment happens after. In
> C#, snapshot the list (`foreach` over a copy or index the original) and only
> reassign `unit.Statuses` afterward. Decrement `turns` on the same `StatusEffect`
> objects (they're shared references) so the later `Where(Turns>0)` filter sees it.

---

## 7. `ai.ts` → `Ai.cs`

`static class Ai`. Pure action-*choosing*; imports neither actions nor combat.

### `chooseEnemyAction(state, unit) → BattleAction`
- `def = ENEMY_DEFS[unit.enemyDefId]` (may be null). `foes = livingUnits(state, Party)`.
- If `def?.special` is null OR `foes.Count == 0`: pick attack target via
  `pickEnemyAttackTarget`, return `Attack(t?.Id ?? "")`.
- Else `useSpecial` by archetype:
  - grunt: `RngChance(rng, 0.2)`.
  - caster: `RngChance(rng, 0.5)`.
  - tank: if `special.SelfHeal != null` → `unit.Hp < unit.MaxHp * 0.4`; else
    `RngChance(rng, 0.35)`.
  - boss: `RngChance(rng, unit.Hp < unit.MaxHp * 0.6 ? 0.55 : 0.3)`.
- If `!useSpecial`: `pickEnemyAttackTarget`, return `Attack(...)`.
- Else return `Ability(slot: Q, targetId: foes[0]?.Id)`. (Enemy specials dispatch
  through `resolveEnemySpecial`; the `slot` is a synthetic placeholder.)

> RNG-ordering flag **F-AI-1**: for tank with `SelfHeal != null`, `useSpecial` is a
> pure HP comparison — **no RNG draw**. For tank without selfHeal, and grunt/caster/
> boss, exactly **one `RngChance` draw** happens. Preserve which branches draw.

### `pickEnemyAttackTarget(state, unit, foes, rng) → BattleUnit?`
If `foes` empty → null. If `unit.Archetype == Tank`: return the foe with the **lowest
`defense`** (`reduce` — on tie the earliest-index foe wins, since `<` is strict).
Else `rngPick(rng, foes)`.

### `choosePetAction(state, unit) → BattleAction`
- `kit = availableAbilities(unit)`; `allies = livingUnits(Party)`; `foes = livingUnits(Enemy)`.
- `supportAbilities = kit where (heal ?? 0) > 0 || (damage == 0 && selfStatus != null)`.
- `damageAbilities = kit where damage > 0`.
- `someoneHurt = allies.Any(a => a.Hp < a.MaxHp * 0.7)`.
- `lowestFoe = foes.Count>0 ? foes.reduce(min by hp) : null` (tie → earliest index).
- `mode = unit.AiMode ?? Balanced`.
- If `(mode==Defensive || (mode==Balanced && someoneHurt)) && supportAbilities.Count>0`:
  return `Ability(slot: supportAbilities[0].Slot)` (no targetId).
- Else if `damageAbilities.Count>0 && lowestFoe != null`: pick max-damage ability
  (`reduce`, tie → earliest), return `Ability(slot, lowestFoe.Id)`.
- Else if `lowestFoe != null`: return `Attack(lowestFoe.Id)`.
- Else `Defend()`. (`void rng;` in TS is a no-op — drop it.)

> Note **choosePetAction draws NO RNG** — all picks are deterministic reductions.
> Preserve that (don't accidentally introduce a random pick).

---

## 8. `state.ts` → `BattleState.cs`

`static class BattleStateOps` (or split: keep the `BattleState`/`BattleUnit` data
classes in `Types.cs`, put these functions in `BattleState.cs` as a static helper
class). Pure builders + read helpers.

### Bond helpers
- `petBondDamageMul(bondRank) → 1 + 0.18 * Clamp(bondRank, 0, 4)` (`double`).
- `petBondHpMul(bondRank) → 1 + 0.12 * Clamp(bondRank, 0, 4)` (`double`).
- `petUnlockedAbilityCount(bondRank) → bondRank>=2 ? 2 : bondRank>=1 ? 1 : 0` (`int`).

### Unit builders
- `buildHeroUnit(setup, reinforced) → BattleUnit`. `stats = HERO_STATS[heroClass]`;
  `maxHp = RoundTs(stats.MaxHp * (reinforced ? 1.3 : 1))`; id `"hero"`, kind hero,
  hp=maxHp, resource=maxResource, atb 0, copies speed/defense/attack/element, empty
  statuses/cooldowns, `defending=false`, `alive=true`, sets `HeroClass`.
- `buildPetUnit(spec, index, reinforced) → BattleUnit`. `stats = PET_STATS[species]`;
  `maxHp = RoundTs(stats.MaxHp * petBondHpMul(bondRank) * (reinforced?1.3:1))`;
  id `$"pet-{index}"`; **`defense` hard-coded `0.1`** (NOT from stats);
  `attack = RoundTs(stats.Attack * petBondDamageMul(bondRank))`; sets Species/BondRank/AiMode.
- `buildEnemyUnit(spec, index, wave) → BattleUnit`. `def = ENEMY_DEFS[spec.DefId]` —
  **throw** `Exception($"Unknown enemy def id: {spec.DefId}")` if missing.
  `scaling = waveScaling(wave)`; `boss = def.Archetype==Boss && isBossWave(wave)`;
  `hpMul = scaling.HpMul * (boss ? bossHpMul(wave) : 1)`;
  `maxHp = RoundTs(def.BaseHp * hpMul)`; `attack = RoundTs(def.BaseAttack * scaling.HeartDmgMul)`;
  id `$"enemy-{index}"`; hp = `spec.Hp != null ? Max(1, Min(spec.Hp, maxHp)) : maxHp`;
  resource/maxResource/resourceRegen = 0; `speed = def.Speed * scaling.SpeedMul`;
  statuses = **deep copy** of `spec.Statuses` (`.map(s => ({...s}))`) or empty.

### `createBattle(setup) → BattleState`
- `reinforced = setup.Reinforcements == true`.
- `hero = buildHeroUnit(...)`.
- `joining = setup.Pets.Where(JoinsImmediately).Take(MAX_PARTY - 1)` — note **7** slots
  for joining pets (hero takes the 8th).
- `benched = setup.Pets.Where(!JoinsImmediately)`.
- `petUnits = joining.Select((p,i) => buildPetUnit(p, i, reinforced))`.
- `enemyUnits = setup.Enemies.Take(MAX_ENEMIES).Select((e,i) => buildEnemyUnit(e,i,wave))`.
- `inventory` = dictionary with all 3 `ItemKind`s defaulted to 0, overridden by
  `setup.Inventory` if present (`?? 0` per key).
- `reserve` = `benched.Select(p => new RallyReserveUnit{...})`.
- Build `BattleState`: `units = [hero, ...petUnits, ...enemyUnits]`, phase `Intro`,
  outcome `None`, `turnCounter 0`, `rng = createRng(setup.Seed)`, empty log,
  `reinforcementsApplied = reinforced`.
- `pushLog` a `battle-start` entry (text differs if reinforced).

### Read helpers (all pure)
- `pushLog(state, entry)` — `state.Log.Add(entry)`.
- `getUnit(state, id) → BattleUnit?` — `Units.FirstOrDefault(u => u.Id == id)`.
- `livingUnits(state, side) → List<BattleUnit>` — filter `side == side && Alive`.
  **Order-preserving** — keep `Units` order; downstream tie-breaks depend on it.
- `lowestHpEnemy(state) → BattleUnit?` — reduce-min by Hp over living enemies, earliest
  on tie. (Exported but not used inside the engine — UI helper. Port anyway.)
- `hasStatus(unit, kind) → bool` — `Statuses.Any(s => s.Kind==kind && s.Turns>0)`.
- `statusFillMul(unit) → double` — `mul=1; if haste *= 1.5; if slow *= 0.5;` return mul.
  (Both can apply → 0.75.)
- `isBattleOver(state) → bool` — either side's living count is 0.
- `computeOutcome(state) → BattleOutcome` — `!enemiesAlive && partyAlive → Victory`;
  `!partyAlive → Defeat`; else `None`.
- `availableAbilities(unit) → AbilityDef[]` — `unitAbilityKit` filtered by
  `resource >= cost && (cooldowns[slot] ?? 0) <= 0`.
- `unitAbilityKit(unit) → AbilityDef[]` — hero → `HERO_ABILITIES[heroClass]`; pet →
  `PET_ABILITIES[species].Take(petUnlockedAbilityCount(bondRank ?? 0))`; else empty.
- `findAbility(unit, slot) → AbilityDef?` — first kit entry with matching slot.

### `cloneBattle(state) → BattleState`
Deep copy: new `BattleState` with new `BattleUnit` list (each unit field-copied, with
**new** `Statuses` list of copied `StatusEffect`s and a new `Cooldowns` dictionary),
new `Reserve` list of copied `RallyReserveUnit`s, new `Rng` (`new Rng{Seed=...}`), new
`Inventory` dictionary, new `Log` list of copied entries. **Must be a fully independent
deep copy** — `atbEngine.test.ts` asserts mutating the copy never touches the original.
In C# write an explicit `CloneBattle`; do not rely on `MemberwiseClone` (it would share
the lists).

---

## 9. `actions.ts` → `Actions.cs`

`static class Actions`. The resolve* family + `applyAction` dispatcher. Mutates state.

### `strike(state, actor, target, basePower, element, opts) → void`
`opts` is an options bundle — port as a small `struct StrikeOpts { double? ArmorPierce;
StatusKind? ApplyStatus; double? StatusChance; BattleLogEvent Event; string Label;
bool? CanCrit; }` (`Event` is only ever `Attack` or `Ability`).
Logic:
- if `!target.Alive` return.
- `result = CalculateDamage(new DamageInput{ attacker, target, basePower, element,
  armorPierce = opts.ArmorPierce, canCrit = opts.CanCrit, rng = state.Rng })`.
- `dealt = ApplyDamage(target, result)`.
- `pushLog` an `opts.Event` entry; text is the shielded form if `result.Shielded`, else
  the hit form with `dealt` and a `" (CRIT!)"` suffix when `result.Crit`.
- If `target.Hp <= 0 && !target.Alive`: `pushLog` a `death` entry.
- Else if `opts.ApplyStatus != null && !result.Shielded && target.Alive &&
  RngChance(state.Rng, opts.StatusChance ?? 1)`: `applyStatus` + `pushLog` status-apply.

> RNG-ordering flag **F-ACT-2**: the status-roll `RngChance` is gated behind `&&`, so a
> shielded hit / a kill / a no-status strike skips that draw. `calculateDamage` already
> drew 1–2 values. Preserve the exact `&&` chain.

### `resolveAttack(state, actor, targetId) → void`
`target = getUnit(targetId)`; `enemySide` opposite of actor; `real = (target alive &&
on enemySide) ? target : rngPick(state.Rng, livingUnits(enemySide))`. If `real` null →
return. Else `strike(state, actor, real, actor.Attack, actor.Element, { event=Attack,
label="a basic attack" })`.

### `resolveAbility(state, actor, slot, targetId) → void`
- `ability = findAbility(actor, slot)`; if null return.
- `actor.Resource = Max(0, actor.Resource - ability.Cost)`;
  `actor.Cooldowns[slot] = ability.CooldownTurns`.
- `pushLog` an `ability` "casts" entry.
- `targets = resolveTargets(state, actor, ability.Target, targetId, state.Rng,
  ability.Hits ?? 1)`.
- **Damage component** — if `ability.Damage > 0`: `strike` each target with
  armorPierce/applyStatus/statusChance from the ability. Then **splash** — if
  `ability.Splash != null` (and `> 0` — TS `if (ability.splash && ...)`) and
  `targets.Count > 0`: for each `adjacentUnitIds(state, targets[0])`, if that unit is
  alive, `strike` it for `ability.Splash` with `canCrit = false`.
- **Heal component** — if `ability.Heal != null && ability.Heal > 0`: `applyHeal` each
  target, `pushLog` with `amount = -healed`.
- **Self-heal** — if `ability.HealPctSelf != null && > 0`:
  `applyHeal(actor, actor.MaxHp * healPctSelf)`, `pushLog` `amount = -healed`.
- **Non-damage status** — if `ability.ApplyStatus != null && ability.Damage == 0`:
  for each living target, `RngChance(state.Rng, ability.StatusChance ?? 1)` → applyStatus
  + log. (Damage abilities already applied status inside `strike`.)
- **Self status** — if `ability.SelfStatus != null`: `applyStatus(actor, selfStatus)` + log.

> Flag **F-ACT-3**: TS `if (ability.splash && targets.length > 0)` — `splash` is
> truthy-checked, so a `splash` of `0` is treated as "no splash". Port as
> `ability.Splash.HasValue && ability.Splash.Value != 0 && targets.Count > 0`. (No def
> has splash 0, so it's moot, but stay literal.) Same pattern for `heal`/`healPctSelf`
> (already handled with explicit `> 0`).

### `resolveItem(state, actor, item, targetId) → bool`
- if `(inventory[item] ?? 0) <= 0` return false.
- `def = ITEM_DEFS[item]`; `target = getUnit(targetId)`;
  `real = (target alive && target.Side == actor.Side) ? target : actor`.
- `inventory[item] -= 1`.
- if `def.Heal`: `applyHeal(real, def.Heal)` + log.
- if `def.RestoreResource`: `applyResource(real, def.RestoreResource)` + log.
- if `def.Cleanse`: `cleanseStatuses(real)`; log lists removed or generic text.
- return true.

### `resolveDefend(state, actor) → void`
`actor.Defending = true`; `applyResource(actor, 8)` (literal 8); `pushLog` a `defend` entry.

### `resolveRally(state, actor, reserveIndex) → bool`
- if `reserveIndex < 0 || >= reserve.Count` → false.
- if `livingUnits(state, Party).Count >= MAX_PARTY` → false.
- `reserve = state.Reserve[reserveIndex]`; remove it from the list.
- `petCount = state.Units.Count(u => u.Kind == Pet)`.
- `unit = buildPetUnit(new PartyPetSpec{ species,name,bondRank,aiMode,
  joinsImmediately=true }, petCount, state.ReinforcementsApplied)`.
- **Override id**: `unit.Id = $"pet-rally-{state.TurnCounter}-{petCount}"`.
- Insert just before the first enemy: `firstEnemyIdx = Units.FindIndex(Side==Enemy)`;
  if `-1` append, else `Units.Insert(firstEnemyIdx, unit)`.
- `pushLog` a `rally` entry; return true.

### `resolveEnemySpecial(state, unit) → void`
- `def = ENEMY_DEFS[unit.EnemyDefId]` (nullable). If `def?.Special` null: fallback —
  `t = rngPick(state.Rng, livingUnits(Party))`; if `t` `resolveAttack(state, unit, t.Id)`;
  return.
- `special = def.Special`; `pushLog` an `ability` "uses {name}" entry.
- if `special.SelfHeal != null && > 0`: `applyHeal(unit, special.SelfHeal)` + log;
  **return** (selfHeal specials do nothing else).
- `targets = resolveTargets(state, unit, special.Target, null, state.Rng, 1)`.
- for each target: if `special.Damage > 0` → `strike` with applyStatus/statusChance;
  else if `special.ApplyStatus != null && target.Alive &&
  RngChance(state.Rng, special.StatusChance ?? 1)` → applyStatus + log.

### `applyAction(state, actor, action) → void`
`switch (action.Kind)`:
- `attack`: `resolveAttack(state, actor, action.TargetId)`.
- `ability`: if `actor.Kind == Enemy` → `resolveEnemySpecial(state, actor)`. Else
  `ability = findAbility(actor, action.Slot)`; `usable = ability != null &&
  actor.Resource >= ability.Cost && (actor.Cooldowns[slot] ?? 0) <= 0`; if usable →
  `resolveAbility(...)`; else fallback → `foe = rngPick(livingUnits(Enemy))`, if foe
  `resolveAttack`.
- `item`: if `!resolveItem(...)` → `resolveDefend(state, actor)`.
- `defend`: `resolveDefend(state, actor)`.
- `rally`: if `!resolveRally(...)` → `resolveDefend(state, actor)`.

---

## 10. `turn.ts` → `Turn.cs` & `battleScaling.ts`

### 10.1 `battleScaling.ts` → `BattleScaling.cs`

`static class BattleScaling`. `const int BOSS_EVERY = 6;`
`private const double FIRST_WAVE_SPEED_MUL = 0.85;`
`struct WaveScaling { int EnemyCount; double HpMul; double SpeedMul; double HeartDmgMul; }`
- `isBossWave(wave) → wave > 0 && wave % BOSS_EVERY == 0`.
- `bossOrdinal(wave) → Max(1, (int)Math.Floor(wave / (double)BOSS_EVERY))`.
- `bossHpMul(wave) → 1 + 0.6 * (bossOrdinal(wave) - 1)`.
- `waveScaling(wave)`: `w = Max(1, Floor(wave))`; `steps = w - 1`;
  `enemyCount = w<=1 ? 8 : Min(8 + steps*2, 12)`;
  `hpMul = 1 + 0.16*steps`;
  `speedMul = w<=1 ? 0.85 : Min(1 + 0.012*steps, 1.28)`;
  `heartDmgMul = 1 + 0.05*steps`.
Note: `enemyCount` is computed but the ATB engine never reads it (enemy count is driven
by the breach roster). `bossOrdinal`/`waveScaling` use integer-valued waves.

### 10.2 `turn.ts` → `Turn.cs`

`static class Turn`.

### `advanceToNextTurn(state) → BattleUnit?`
- if `isBattleOver(state)` → null.
- loop `guard = 0..<100000`:
  - `ready = readyUnit(state)`; if non-null return it.
  - `minSteps = double.PositiveInfinity`. For each living unit: `rate = u.Speed *
    statusFillMul(u)`; skip if `rate <= 0`; `need = (ATB_FULL - u.Atb) / rate`; if
    `need < minSteps` → `minSteps = need`.
  - if `!double.IsFinite(minSteps) || minSteps <= 0` → `minSteps = 0.001`.
  - For each living unit: `rate = u.Speed * statusFillMul(u)`;
    `u.Atb = Min(ATB_FULL, u.Atb + rate * minSteps)`.
- after the loop return `readyUnit(state)` (unreachable in practice).

> Port `Infinity` as `double.PositiveInfinity`; `isFinite` as `double.IsFinite`. The
> `100000` guard is literal. All ATB math in `double`.

### `readyUnit(state) → BattleUnit?`
First unit in `Units` order with `Alive && Atb >= ATB_FULL`. **Lowest index wins ties**
— this is the deterministic tie-break; preserve `Units` ordering everywhere.

### `isPlayerControlled(unit) → bool` — `unit.Kind == Hero`.

### `beginNextTurn(state) → BattleState`
- if `phase == Ended` return state.
- `pending = computeOutcome(state)`; if non-`None` → `endBattle(state, pending)`.
- `actor = advanceToNextTurn(state)`; if null → `endBattle(state, computeOutcome(state)
  ?? Defeat)` (`?? Defeat` → if `computeOutcome` returns `None`, use `Defeat`).
- `state.ActiveUnitId = actor.Id`; `state.TurnCounter += 1`; `actor.Defending = false`.
- `pushLog` `turn-start`.
- `mustSkip = tickStatuses(state, actor)`.
- if `!actor.Alive`: `pushLog` `death` ("succumbs"), `return finishTurn(state, actor)`.
- `afterTick = computeOutcome(state)`; if non-`None` → `endBattle`.
- if `mustSkip`: `pushLog` `skip`, `return finishTurn(state, actor)`.
- `state.Phase = isPlayerControlled(actor) ? AwaitingInput : Resolving`; return state.

### `resolveAiTurn(state) → BattleState`
- if `phase != Resolving || ActiveUnitId == null` return state.
- `actor = getUnit(ActiveUnitId)`; if null or `!Alive` return state.
- if `isPlayerControlled(actor)` return state.
- `action = actor.Kind == Enemy ? chooseEnemyAction(...) : choosePetAction(...)`.
- `applyAction(state, actor, action)`; `return finishTurn(state, actor)`.

### `submitAction(state, action) → BattleState`
- if `phase != AwaitingInput || ActiveUnitId == null` return state.
- `actor = getUnit(ActiveUnitId)`; if null / `!Alive` / `!isPlayerControlled` return state.
- `applyAction(state, actor, action)`; `return finishTurn(state, actor)`.

### `finishTurn(state, actor) → BattleState`
- if `actor.Alive`: `applyResource(actor, actor.ResourceRegen)`; for each slot in
  `actor.Cooldowns.Keys`: `cd = cooldowns[slot] ?? 0`; if `cd > 0` → `cooldowns[slot] = cd-1`.
- `actor.Atb = ATB_RESET`; `state.ActiveUnitId = null`; `return beginNextTurn(state)`.

> Port note: iterating `Cooldowns.Keys` while assigning into the same dictionary is
> fine in C# only if you **don't add/remove keys** — here only values change, which is
> allowed. To be safe, snapshot `Cooldowns.Keys.ToList()` before the loop.

### `endBattle(state, outcome) → BattleState`
`phase = Ended`; `outcome = outcome`; `activeUnitId = null`; `pushLog` `victory` or
`defeat` entry. Return state.

### `startBattle(state) → BattleState`
if `phase != Intro` return state; `phase = Filling`; `return beginNextTurn(state)`.

### `autoResolveBattle(state, maxTurns = 5000) → BattleState`
- if `phase == Intro` → `startBattle`.
- loop while `phase != Ended && turns < maxTurns`: `turns++`; if `AwaitingInput &&
  ActiveUnitId`: `hero = getUnit(...)`; if hero → `submitAction(s, autoHeroAction(s,
  hero))` else break. Else if `Resolving` → `resolveAiTurn`. Else → `beginNextTurn`.
- return s.
Default `maxTurns = 5000` (C# optional param or overload).

### `autoHeroAction(state, hero) → BattleAction`
- `foes = livingUnits(Enemy)`; if empty → `Defend()`.
- `lowestFoe = foes.reduce(min by hp)`; `allies = livingUnits(Party)`;
  `heroHurt = hero.Hp < hero.MaxHp * 0.4`.
- if `heroHurt && (inventory.potion ?? 0) > 0` → `Item(Potion, hero.Id)`.
- `usable = availableAbilities(hero)`; `damageAbilities = usable where damage>0`.
- if `damageAbilities.Count>0`: pick max-damage → `Ability(slot, lowestFoe.Id)`.
- `supportAbilities = usable where selfStatus != null || healPctSelf != null`.
- if `supportAbilities.Count>0 && (heroHurt || allies.Count == 1)` →
  `Ability(supportAbilities[0].Slot)` (no target).
- else `Attack(lowestFoe.Id)`.

---

## 11. `atbEngine.ts` — re-export barrel

`atbEngine.ts` is a pure re-export barrel: it re-exports the engine's entire public
surface so `@/lib/atbEngine` importers keep working. **C# has no module-barrel concept.**
Do not create an `AtbEngine.cs`. Instead:
- Place every type/static class in `Defenders.BattleATB.Engine`.
- External consumers `using Defenders.BattleATB.Engine;`.
- If a single facade is desired, add an optional `static class AtbEngine` that forwards
  the most-used entry points (`CreateBattle`, `StartBattle`, `SubmitAction`,
  `ResolveAiTurn`, `BeginNextTurn`, `AutoResolveBattle`, `CloneBattle`) — but this is
  cosmetic; the namespace already exposes everything.

**Public surface inventory** (what the barrel re-exports — the port's public API):

- rng: `Rng` (type), `createRng`, `rngNext`, `rngInt`, `rngChance`, `rngPick` — 1 type + 5 fns.
- types: 23 exported types — `Side, ActionKind, AbilitySlot, ElementType, StatusKind,
  ItemKind, PetAiMode, BattleOutcome, BattlePhase, StatusEffect, TargetMode, AbilityDef,
  ItemDef, EnemyArchetype, EnemyDef, BattleUnit, RallyReserveUnit, BattleLogEntry,
  BattleState, BreachEnemySpec, PartyPetSpec, BattleSetup, DamageInput, DamageResult,
  BattleAction` — that is 25 names (the brief's count: 25 type-level exports).
- defs: 16 exported values — `STATUS_BLUEPRINTS, ATB_BASE_FILL, ATB_FULL, SLOW_FILL_MUL,
  HASTE_FILL_MUL, ATB_RESET, HERO_ABILITIES, HERO_STATS, PET_STATS, PET_ABILITIES,
  ITEM_DEFS, ENEMY_DEFS, MAX_PARTY, MAX_ENEMIES, CRIT_CHANCE, CRIT_MULT`.
- state: 14 fns — `petBondDamageMul, petBondHpMul, petUnlockedAbilityCount, createBattle,
  getUnit, livingUnits, lowestHpEnemy, hasStatus, isBattleOver, computeOutcome,
  availableAbilities, unitAbilityKit, findAbility, cloneBattle`.
- combat: 7 fns — `calculateDamage, applyDamage, applyHeal, applyResource, applyStatus,
  consumeStatus, cleanseStatuses`.
- ai: 2 fns — `chooseEnemyAction, choosePetAction`.
- turn: 8 fns — `advanceToNextTurn, isPlayerControlled, beginNextTurn, resolveAiTurn,
  submitAction, startBattle, autoResolveBattle, autoHeroAction`.

**Not re-exported (module-internal, but still must be ported):** `targeting.ts`'s
`resolveTargets`/`adjacentUnitIds`; `combat.ts`'s `elementMultiplier`; `actions.ts`'s
`strike, resolveAttack, resolveAbility, resolveItem, resolveDefend, resolveRally,
resolveEnemySpecial, applyAction`; `ai.ts`'s `pickEnemyAttackTarget`; `state.ts`'s
`buildHeroUnit, buildPetUnit, buildEnemyUnit, pushLog`; `turn.ts`'s `readyUnit,
finishTurn, endBattle`. Port them as `internal`/`public static` so tests can reach them.

### Public-surface totals

- **Public functions: 36 re-exported** (5 rng + 14 state + 7 combat + 2 ai + 8 turn).
- **Internal/helper functions: 20** (2 targeting + 1 combat + 8 actions + 1 ai + 4 state
  + 3 turn + 1 `battleScaling` group has 4: `isBossWave, bossOrdinal, bossHpMul,
  waveScaling`). Total functions to port = **36 + 20 + 4 = 60**.
- **Types/interfaces/unions: 25** exported from `types.ts`; plus `HeroClass`,
  `PetSpecies` (deps), `WaveScaling`, and internal-only `StatusBlueprint`,
  `HeroClassStats`, `PetSpeciesStats`, `BattleLogEvent` (enum derived from the log
  union), `UnitKind` (derived) — **~33 type definitions** total in the C# port.
- **Constants: 16 from defs + `BOSS_EVERY`** = 17.

---

## 12. `atbEngine.test.ts` → EditMode NUnit tests

Port the test suite to a Unity **EditMode** test assembly
(`Assets/_Modules/BattleATB/Tests/Engine/`). Map `describe`/`test` → `[TestFixture]`/
`[Test]`. `bun:test` `expect(...).toBe/.toBeGreaterThan/...` → NUnit `Assert.That(...,
Is.EqualTo/GreaterThan/...)`.

Key behaviours the tests pin (the port must keep them green):
- **RNG determinism**: same seed → identical 50-draw sequence; floats in `[0,1)`;
  `rngInt` within inclusive bounds. **Add a golden-vector test** (see §13).
- `createBattle`: 3-party/5-enemy roster from `sampleSetup`; hero stats by class; wave
  HP scaling monotonic; reinforcements inflate HP; party/enemy caps at 8.
- damage: defense reduces damage; aether ignores armour; shield negates + is consumed;
  defending halves; `applyHeal` clamps & never revives; damage floor 1 + kill flags dead.
- status: burn ticks down then expires; cleanse strips debuffs keeps buffs.
- pet bond: `petBondDamageMul(4) > (0)`; `petUnlockedAbilityCount` 0/1/2/2.
- turn order: faster Ranger acts before a slow Bruiser field.
- rally / items: Rally pulls a benched pet, inventory decremented; potion heals.
- full round-trip: deterministic 3v5 terminates `< 4000` turns, outcome consistent;
  `autoResolveBattle` is reproducible (same seed → same outcome/turnCounter/log length);
  different seeds vary battle length; `cloneBattle` is an independent deep copy.

`sampleSetup` default seed `0xa11ce`; other seeds used: `0xbeef`, `1..12`.

---

## 13. RNG cross-language verification (do this first in Week 2)

Because the engine is anti-cheat-relevant, before porting any gameplay code:

1. In the LOCKED React repo, **run** (do not modify any committed file — use a scratch
   script outside the repo, or a throwaway REPL) `createRng(12345)` then `rngNext` 10×
   and record the 10 doubles to full precision. Also record `createRng(0xa11ce)` × 10.
2. Hard-code those as a **golden vector** in the C# EditMode test
   (`RngGoldenVectorTest`). The C# `RngNext` must reproduce every value **exactly**
   (`Assert.That(v, Is.EqualTo(expected))` with zero tolerance — the division by 2^32
   yields an exactly-representable double, so exact equality is correct).
3. Only once the golden vector passes, port the rest of the engine.

This is the single most important safeguard for "the same seed yields an identical
sequence in C# as in TS."

---

## 14. Flags

Items found during analysis. **None are to be fixed in the port** — port the behaviour
as-is; these are recorded for the design/anti-cheat owners.

- **F-DEFS-1 (dead constant):** `ATB_BASE_FILL = 12` is exported and documented as
  "ATB units a speed-1.0 unit gains per second," but **no engine file references it**.
  `advanceToNextTurn` is a discrete event-step simulation (it scales by `minSteps`,
  never by a per-second rate), so `ATB_BASE_FILL` has no effect on outcomes. Port the
  constant for API parity, but it is inert. *Not a bug — just unused.*

- **F-RNG-1 / F-RNG-2 / F-RNG-3 / F-RNG-4:** see §2.6 — these are **mandatory C#
  porting requirements** (uint state, `unchecked`, double literal, reference-type
  `Rng`). Not TS bugs; they are traps where a naive C# port would silently diverge.

- **F-TARG-1 (single-ally for an enemy actor):** in `resolveTargets`, `single-ally`
  with `explicitTargetId` checks `t.side === actor.side`. `resolveEnemySpecial` calls
  `resolveTargets(..., special.target, undefined, ...)`, so for an enemy special with
  `target: 'single-ally'` (e.g. `goblin` Reckless Swing, `skeleton` Bone Shard,
  `hollow-apprentice` Tincture) the `explicitTargetId` is `undefined` → the `single-ally`
  branch returns **`[actor]`** — i.e. the enemy targets *itself*. So Reckless Swing /
  Bone Shard / Tincture, as defined, hit the **casting enemy**, not a party member.
  This looks like a **content/design bug** (those specials presumably intend to hit a
  party member, which would need `target: 'single-enemy'` from the enemy's frame).
  *Behaviour is faithful to the TS — port exactly. Flag for the combat designer.*

- **F-TARG-2 (random-enemies allows repeats):** `random-enemies` re-`rngPick`s from the
  full pool each hit without removal, so Ranger's `Volley` (hits 3) can strike the same
  foe up to 3×. Likely intentional ("random"), but noted in case the designer expected
  distinct targets.

- **F-ACT-1 (enemy ability slot is synthetic):** `chooseEnemyAction` returns
  `{ kind:'ability', slot:'q', ... }` for enemies, but enemies have no Q/W/E/R kit;
  `applyAction` routes `actor.kind === 'enemy'` straight to `resolveEnemySpecial` and
  ignores the slot. Faithful; the `slot:'q'` is a harmless placeholder. Port the field
  but don't rely on it.

- **F-CMB-1 / F-ACT-2 (RNG-ordering, informational):** `calculateDamage` always draws
  the spread value and conditionally the crit value *before* the shield check; `strike`
  conditionally draws the status-chance value after. The RNG cursor advances on
  shielded/killed hits too. This is correct and deterministic — flagged only so the
  porter does not "optimize" by reordering, which would desync the seed stream.

- **F-CMB-2 (`consumeStatus` removes all, not one):** `consumeStatus` docstring says
  "Remove one charge / instance" but the implementation `filter`s out **every** instance
  of the kind. Since `applyStatus` never stacks duplicates of the same kind (it
  refreshes in place), there is only ever one instance, so the observable behaviour is
  identical. *Doc/impl mismatch, not a runtime bug. Port the impl (remove-all).*

- **F-STATE-1 (`Math.round` half-up vs banker's rounding):** see the global convention
  in §0 — this is the highest-risk silent divergence after the RNG. Every `Math.round`
  in `combat.ts` and `state.ts` MUST go through a half-up `RoundTs` helper, or scaled
  HP / healing / damage will differ from the reference by ±1 on exact-half values and
  cascade into different battle outcomes.

---

## 15. Suggested Week-2 port order

1. `Rng.cs` + `RngGoldenVectorTest` (§2, §13) — verify bit-exact before anything else.
2. `Types.cs` (enums + data classes) and `BattleScaling.cs`.
3. `Defs.cs` (static tables; SOs optional/later).
4. `BattleState.cs` (builders + read helpers + `CloneBattle`).
5. `Targeting.cs`, then `Combat.cs`, then `Ai.cs`.
6. `Actions.cs`.
7. `Turn.cs`.
8. Port `atbEngine.test.ts` to EditMode tests; run the full 3v5 round-trip and the
   determinism tests; confirm `autoResolveBattle` reproduces outcome + turnCounter +
   log length for a fixed seed.
