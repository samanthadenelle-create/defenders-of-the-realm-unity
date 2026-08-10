# WORK ORDER 102 — Village Bug Cluster: Dragon Airborne / Enemy Aggro / Stacked Bars

**Status:** CLOSED — SUPERSEDED by WO-125 (owner-approved sweep 2026-08-09: old-scene bug cluster; scene deleted, survivors re-ticketed)
**Date:** 2026-05-29
**Priority:** High — all three are visible in every playtest session
**Scope:** Small — targeted fixes, no system rewrites
**Files to touch:** `DragonBoss.cs`, `HeartController.cs`, `HeroHealth.cs`, `EnemyBrain.cs`, `Boss_Dragon` prefab inspector

---

## Bug 1 — Dragon stays in air: stalemate (can't attack dragon; dragon can't hit tree)

### Observed
Dragon spawns and orbits at altitude. Player can't target it. Dragon fires breath attacks but
the Heart takes zero damage. Neither side makes progress — stalemate.

### Root cause (two separate faults)

**Fault A — HeartController does not implement IDamageableStructure.**
`DragonBoss.Configure()` tries to wire the heart as an `IDamageableStructure` so swoop/breath
damage routes into it:

```csharp
// DragonBoss.cs line ~237
_heartStructure = anchor.GetComponentInParent<IDamageableStructure>();
```

`HeartController` only extends `MonoBehaviour` — it does not implement `IDamageableStructure`.
So `_heartStructure` is always `null`, and `DealStrike()` fires the `StruckHeart` event but
deals zero contact damage. The dragon breathes fire all day and the tree is never scratched.

**Fault B — Orbit height of 22 m puts the dragon above the viewport.**
`WaveManager.SpawnApexBoss()` spawns the dragon at `heartT.position + (0, 22, 0)` to match
`DragonBoss._orbitHeight = 22f`. The village camera looks roughly horizontal — the dragon is
above the player's field of view and above most spell raycast ceilings.

Phase 1 (100–60% HP) has only a 25% chance of initiating a swoop (`BeginSwoop()`) and swoops
take 3.4 seconds to bottom out at 4.5 m. The dragon spends most of Phase 1 at 22 m doing
fire-breath passes that can't hurt the tree (Fault A) and can't be seen or hit by the player.

### Fixes

**Fix A — HeartController implements IDamageableStructure.**

In `HeartController.cs`, add the interface to the class declaration and implement it:

```csharp
// Change:
public sealed class HeartController : MonoBehaviour

// To:
public sealed class HeartController : MonoBehaviour, IDamageableStructure
```

Then add the two interface members. `IsAlive` already mirrors existing HP logic; 
`ApplyContactDamage` routes into the existing damage path:

```csharp
// IDamageableStructure — lets dragon breath/swoops actually hurt the Heart
bool IDamageableStructure.IsAlive => _hp > 0f;

void IDamageableStructure.ApplyContactDamage(float amount)
{
    // Route through the existing damage method so all downstream events
    // (OnHealthChanged, state transitions, HUD updates) still fire.
    SetHp(_hp - amount);
}
```

`SetHp` (or equivalent private HP-setter that already fires `OnHealthChanged`) should already
exist. If it is inlined, extract it to a `private void SetHp(float next)` helper first:

```csharp
private void SetHp(float next)
{
    next = Mathf.Clamp(next, 0f, _maxHp);         // _maxHp = 100f per inspector
    if (Mathf.Approximately(next, _hp)) return;
    _hp = next;
    OnHealthChanged?.Invoke(_hp);
    // ... existing state-derive logic ...
}
```

**Fix B — Lower orbit height so the dragon is visible.**

On the `Boss_Dragon` prefab (`Assets/Prefabs/Village/Generated/Boss_Dragon.prefab`), set the
following in the `DragonBoss` inspector component:

| Field | Old value | New value |
|---|---|---|
| `_orbitHeight` | 22 | **10** |
| `_swoopLowHeight` | 4.5 | **2.5** |
| Phase 1 swoop chance (see below) | 0.25 | **0.55** |

The swoop chance is hardcoded in `TickAttackCadence()` in `DragonBoss.cs`:

```csharp
// Old:
case DragonPhase.Circling:
    if (UnityEngine.Random.value < 0.25f) BeginSwoop();
    else FireBreath();
    break;

// New:
case DragonPhase.Circling:
    if (UnityEngine.Random.value < 0.55f) BeginSwoop();
    else FireBreath();
    break;
```

10 m orbit is well within camera view from the village ground plane and within reach of the
hero's spell raycasts.

### Acceptance criteria
- Dragon is clearly visible from the ground while orbiting
- Each breath/swoop attack visibly reduces the Heart HP bar
- Hero can land spell hits on the dragon (HP bar decrements)
- Dragon dies and spirals down after sufficient damage

---

## Bug 2 — Enemies walk past / through the hero without engaging

### Observed
Enemy AI ignores the hero completely, marching straight through to the Heart.

### Root cause (two separate faults)

**Fault A — Hero may lack the "Player" tag.**
`EnemyBrain.Start()` resolves the hero with:

```csharp
var heroGo = GameObject.FindWithTag("Player");
_heroTransform = heroGo != null ? heroGo.transform : null;
```

If the hero `GameObject` in the scene does not have the Unity tag `"Player"` set,
`_heroTransform` is always `null`. `FindNearbyHero()` returns `null`, and enemies never
route toward the hero regardless of proximity.

**Check:** Open the Village scene, select the hero GameObject (the one carrying
`HeroAbilities`/`HeroLocomotion`), and confirm `Tag = Player` in the Inspector. If it is
`Untagged`, set it to `Player`.

**Fault B — `Enemy.ProbeForStructure()` cannot find the hero.**
Even when `EnemyBrain` correctly routes the enemy's NavMesh destination toward the hero,
`Enemy.TickContactAttack()` only locks on to `IDamageableStructure` targets:

```csharp
private IDamageableStructure ProbeForStructure()
{
    // SphereCast ahead — only IDamageableStructure hits register
    var structure = hit.collider.GetComponentInParent<IDamageableStructure>();
    ...
}
```

`HeroHealth` does not implement `IDamageableStructure`. So enemies arrive at the hero,
find no structure to lock on to, and keep pathing past toward the Heart.

### Fix

**Fix A — Tag check** (scene, not code): Ensure the hero `GameObject` has the `Player` tag.
The `VillageSceneBuilder` should set this tag when it places the hero. Add to
`VillageSceneBuilder.PlaceHero()` (or equivalent):

```csharp
heroGo.tag = "Player";
```

**Fix B — HeroHealth implements IDamageableStructure.**

```csharp
// Change:
public sealed class HeroHealth : MonoBehaviour

// To:
public sealed class HeroHealth : MonoBehaviour, IDamageableStructure
```

Implement the two interface members:

```csharp
// IDamageableStructure — lets enemies stop and melee-attack the hero
bool IDamageableStructure.IsAlive => IsAlive;   // already exists: _hp > 0f

void IDamageableStructure.ApplyContactDamage(float amount) => TakeDamage(amount);
```

`TakeDamage(float)` is already public and handles all HP, feedback, and death logic.
This one-line routing is the entire fix — enemies will now stop in front of the hero and
deal contact damage on their normal attack interval (1.3 s), exactly like they stop at walls.

### Note on HeroHealth's own proximity scan
`HeroHealth.Update()` already has a fallback damage loop (enemies within `EngageRadius = 1.5 m`
deal 6 damage/sec) for the case where enemies reach the hero without stopping. With the
`IDamageableStructure` fix landed, enemies stop farther out and attack on the melee interval.
The proximity fallback can stay — it acts as a contact-damage floor if the enemy crowd-surges.

### Acceptance criteria
- Standing near enemies causes the hero health bar to decrement
- Enemies stop in front of the hero and play attack animations rather than walking through
- Enemies resume marching to the Heart after the hero is killed

---

## Bug 3 — Hero health bar and tree health bar stacked

### Observed
Both bars appear at the same position in the top-left corner — they render on top of each other.

### Root cause
`HeroHealth.OnGUI()` draws its IMGUI bar at a fixed screen position:

```csharp
// HeroHealth.cs
const float w = 260f, h = 22f, x = 20f, y = 64f;
```

`VillageHudController` renders the Heart HP bar via a UIDocument (`_heartHpFill`) anchored
to the top-left of the canvas. Both elements land at approximately the same pixel row. IMGUI
and UIToolkit are separate render passes — neither knows about the other — so they stack
without clipping.

### Fix

Move the HeroHealth IMGUI bar down so it clears the UIDocument Heart bar. The UIDocument
bar sits roughly in the 0–80 px zone; the hero bar at `y=64` collides with it.

In `HeroHealth.cs` `OnGUI()`:

```csharp
// Old:
const float w = 260f, h = 22f, x = 20f, y = 64f;

// New:
const float w = 260f, h = 22f, x = 20f, y = 110f;
```

`y = 110f` puts the hero bar below the UIDocument Heart HP bar with ~25 px clearance.

**Optional polish** (same pass, low risk): add a small label above the bar:

```csharp
// Before the backdrop DrawTexture:
GUI.color = Color.white;
GUI.Label(new Rect(x, y - 18f, w, 18f), "Hero", new GUIStyle(GUI.skin.label)
    { fontSize = 11, fontStyle = FontStyle.Bold });
```

The Heart bar already has its own label in the UIDocument. The hero IMGUI bar's `GUI.Label`
already prints "Hero  {hp} / {max}" centred on the bar, so the separate label above is
optional — only add if the owner wants the name offset.

### Acceptance criteria
- Hero HP bar and Heart HP bar are clearly separated, no overlap
- Both bars update correctly during play

---

## Files to edit

| File | Change |
|---|---|
| `Assets/_Modules/Village/Heart/HeartController.cs` | Implement `IDamageableStructure`; add `SetHp()` helper if not already extracted |
| `Assets/_Modules/Village/Hero/HeroHealth.cs` | Implement `IDamageableStructure`; move IMGUI bar from `y=64` to `y=110` |
| `Assets/_Modules/Village/Enemies/DragonBoss.cs` | Bump Phase-1 swoop chance from 0.25 → 0.55 |
| `Assets/Prefabs/Village/Generated/Boss_Dragon.prefab` | Set `_orbitHeight = 10`, `_swoopLowHeight = 2.5` |
| `Assets/Editor/VillageSceneBuilder.cs` | Ensure hero GO gets `tag = "Player"` when placed |

**Do NOT touch:**
- `WaveManager.cs` — dragon spawn height (`+22f`) is intentional as a starting altitude; `DragonBoss.Configure()` will keep the dragon at the new `_orbitHeight = 10` set on the prefab
- `EnemyBrain.cs` — logic is correct; the bug is purely the missing tag + missing interface
- `Village.unity` — never hand-edit; rebuild via `VillageSceneBuilder`

---

## Build verification

After applying:
1. Run the Village scene, skip to wave 4 (or use DevPanel to fast-forward)
2. Confirm dragon is visible from ground level, orbits at ~10 m, swoops regularly
3. Confirm Heart HP decrements when dragon attacks
4. Confirm hero HP decrements when standing near enemies
5. Confirm enemies stop and play attack animation in front of the hero
6. Confirm two health bars are visually separated (no overlap)
7. Confirm no `NullReferenceException` in Player.log for HeartController or HeroHealth
