# Claude Code Work Order — Enemy Mob Sets + VFX Layer

**Priority:** P0 (enemy archetypes) / P1 (VFX)
**Raised:** 2026-05-27
**Owner:** Samantha (samanthadenelle@gmail.com)
**Repo:** `C:\Users\Kayden-Laptop\Documents\defenders-unity` (branch `master`)
**Unity:** `6000.4.7f1`

## Context — why this is high priority

`EnemyBrain.cs` (DEF-21) already ships `EnemyRole.Tank`, `.Healer`, `.Ranged` and the full
targeting logic for each. `WaveEnemyGroup.cs` already has `EnemyGroupEntry.Role`.
`EnemyBehaviorTree.cs` (DEF-43) is wired and ready. **The AI brain is live — it just has
no enemies to run it on.**

The village currently has 4 enemies: Hollow Walker, Hollow Warrior, Hollow Rogue,
Necromancer. All are `DPS` role, all skeleton-only. Expanding to three archetypes makes
every EnemyBrain feature actually matter in gameplay, and it's almost entirely
data+prefab work — the hard AI code is done.

## Read these first

1. `docs/enemy-codex.md` — stat anchors, animation coverage, gap list. **Design source
   for all numbers in this work order.**
2. `docs/kaykit-asset-catalog.md` — model paths, confirmed "untapped" status of Mystery
   Monthly packs, shared-rig fact.
3. `Assets/_Modules/Village/Enemies/EnemyBrain.cs` — Tank / Healer / Ranged targeting
   already implemented.
4. `Assets/_Modules/Village/Waves/WaveEnemyGroup.cs` — EnemyRole enum + EnemyGroupEntry
   schema the wave SOs use.
5. `Assets/_Modules/BattleATB/Engine/Defs.cs` — existing ENEMY_DEFS format to match.

## Do-not-touch list (inherited)

- `VillageController.cs`, `WallLayout.Segments/Gates`, `ExteriorRoot` GameObject,
  `TerrainBaseDepth` — per the standing do-not-touch rule in `docs/claude-code-work-order.md`.
- The three `.meta` files locked by `ba393bb`.

---

# Phase A — Tank / Healer / Ranged enemy mob sets (P0)

Three archetypes added: **Hollow Brute** (Tank), **Hollow Mender** (Healer),
**Hollow Caster** (Ranged). All models on disk; no purchase needed. All share
`Rig_Medium` / `Rig_Large`, so `HumanoidEnemy.controller` / `LargeEnemy.controller`
cover them automatically once assigned in the prefab.

---

## A-1 — enemies.json: add three new entries

**File:** `Assets/StreamingAssets/Data/Canonical/enemies.json`

Append inside the `"enemies": [...]` array after the `hollow-rogue` entry and before
`necromancer`. Insert verbatim:

```json
{
  "id": "hollow-brute",
  "name": "Hollow Brute",
  "displayName": "The Bone-Golem",
  "modelKey": "Skeleton_Golem",
  "ai": "charger",
  "hp": 900,
  "moveSpeed": 1.6,
  "contactDamage": 24,
  "attackInterval": 1.8,
  "height": 3.0,
  "boss": false,
  "role": "Tank",
  "flavor": "Bone upon bone, fused by the Withering into something that does not know it is heavy."
},
{
  "id": "hollow-mender",
  "name": "Hollow Mender",
  "displayName": "Hollow Mender",
  "modelKey": "Witch",
  "ai": "walker",
  "hp": 110,
  "moveSpeed": 2.0,
  "contactDamage": 6,
  "attackInterval": 1.3,
  "height": 1.85,
  "boss": false,
  "role": "Healer",
  "flavor": "It remembers how to fix things. It does not remember that they are the enemy now."
},
{
  "id": "hollow-caster",
  "name": "Hollow Caster",
  "displayName": "Hollow Caster",
  "modelKey": "Skeleton_Mage",
  "ai": "walker",
  "hp": 70,
  "moveSpeed": 2.0,
  "contactDamage": 9,
  "attackInterval": 1.8,
  "height": 1.82,
  "boss": false,
  "role": "Ranged",
  "flavor": "It stopped before it reached the Heart. The Withering sends what it can, not what it plans."
}
```

**Also fix:** `hollow-warrior`'s flavor text still says "the lantern-light barely slows"
— BUG-019 holdout. Change to:

```json
"flavor": "Heavier bone, slower stride. A wall of the dead that the Withering barely slows."
```

**Verify:** `jq '.enemies | length'` on the file returns `7`. The `necromancer` entry
must remain last (it is the `boss: true` entry the wave composer queries first).

---

## A-2 — Defs.cs: add three ATB combat definitions

**File:** `Assets/_Modules/BattleATB/Engine/Defs.cs`

In `ENEMY_DEFS` (the `Dictionary<string, EnemyDef>` or equivalent table), add:

```csharp
// ── Hollow Brute (Tank) ──────────────────────────────────────────────────────
["hollow-brute"] = new EnemyDef
{
    Id          = "hollow-brute",
    Name        = "The Bone-Golem",
    Archetype   = EnemyArchetype.Tank,
    BaseHp      = 320,
    BaseAttack  = 30,
    Speed       = 0.85,
    Defense     = 0.22,
    Element     = ElementType.Physical,
    Special     = new SpecialDef
    {
        Name    = "Bone Slam",
        Damage  = 35,
        Target  = TargetType.SingleEnemy,
        Status  = StatusEffect.None,    // raw burst — no status on the Brute
    }
},

// ── Hollow Mender (Healer) ───────────────────────────────────────────────────
["hollow-mender"] = new EnemyDef
{
    Id          = "hollow-mender",
    Name        = "Hollow Mender",
    Archetype   = EnemyArchetype.Caster,
    BaseHp      = 85,
    BaseAttack  = 10,           // deliberately low — it heals, it doesn't hit
    Speed       = 1.0,
    Defense     = 0.08,
    Element     = ElementType.Aether,
    Special     = new SpecialDef
    {
        Name    = "Mend",
        Damage  = 0,
        Target  = TargetType.SingleAlly,
        Status  = StatusEffect.None,
        HealAmount = 35,        // restores 35 HP to the lowest-HP ally in ATB
    }
},

// ── Hollow Caster (Ranged) ───────────────────────────────────────────────────
["hollow-caster"] = new EnemyDef
{
    Id          = "hollow-caster",
    Name        = "Hollow Caster",
    Archetype   = EnemyArchetype.Caster,
    BaseHp      = 75,
    BaseAttack  = 18,
    Speed       = 1.05,
    Defense     = 0.08,
    Element     = ElementType.Aether,
    Special     = new SpecialDef
    {
        Name    = "Withering Bolt",
        Damage  = 14,
        Target  = TargetType.AllEnemies,    // AoE bolt in ATB
        Status  = StatusEffect.Poison,
        StatusChance = 0.40,
    }
},
```

**Note:** Match the exact field names and enum values to the existing `skeleton` /
`necromancer` entries already in `ENEMY_DEFS`. If `HealAmount` is not yet a field on
`SpecialDef`, add it — it is also needed for the Apprentice of the Apothecary
(canon-locked boss, already in `ENEMY_DEFS["hollow-apprentice"]`). Check whether the
Healer's Tincture special already sets it.

**Verify:** `AiTest.cs` test suite green. Run **Defenders ▸ Tests ▸ Run ATB Tests** in
Unity Test Runner. No compile errors. The three new keys must resolve in a
`Defs.ENEMY_DEFS.ContainsKey()` check.

---

## A-3 — Model promotion: get the three models into the live set

### Skeleton_Mage.glb (Hollow Caster)

The Caster's model file is in `KayKit Skeletons 1.1/characters/fbx(unity)/Skeleton_Mage.fbx`.
The live `.glb` set lives in `Assets/Models/KayKit/enemies/`.

Options (pick whichever the local setup supports):
- **Option A — re-export:** open `Skeleton_Mage.fbx` in Unity, export as `.glb` using
  the KayKit pipeline and drop into `Assets/Models/KayKit/enemies/Skeleton_Mage.glb`.
- **Option B — direct FBX reference:** change `Enemy.Configure()` to resolve
  `modelKey: "Skeleton_Mage"` against the full `fbx(unity)` pack path rather than only
  the `.glb` live set. Check `EnemyFamilyTestSpawner.cs` for how modelKey resolution works.

### Skeleton_Golem.glb (Hollow Brute)

Same pack (`KayKit Skeletons 1.1/characters/fbx(unity)/Skeleton_Golem.fbx`). The
catalog notes the `.glb` is **not** in the live set — it must be promoted explicitly.
Same Option A / B choice above.

The Golem uses `Rig_Large` — assign `LargeEnemy.controller`, not `HumanoidEnemy.controller`,
in the prefab.

### Witch.fbx (Hollow Mender)

Path: `KayKit Mystery Monthly Series 5/5 - November 2024 - Witch/characters/Witch.fbx`

Import from the Mystery Monthly warehouse:
1. In Unity Project window, navigate to the Witch FBX and import it.
2. Set Rig → Humanoid. Confirm the avatar maps to KayKit's `Rig_Medium` skeleton
   (all Mystery Monthly humanoids share it — retargeting should be automatic).
3. Run **Defenders ▸ Tools ▸ Fix KayKit Materials** after import so the URP material
   is applied to the new mesh.

**Verify:** Each model appears in the scene with correct URP material (not pink/magenta).
Run `EnemyFamilyTestSpawner` on a test wave to confirm the enemy spawns and walks.

---

## A-4 — Prefab creation: three new enemy prefabs

For each archetype, duplicate the closest existing enemy prefab, then configure:

| Prefab to create | Duplicate from | Changes |
|---|---|---|
| `Enemies/HollowBrute.prefab` | `HollowWarrior.prefab` | Swap mesh → Skeleton_Golem, Animator → `LargeEnemy.controller`, `EnemyBrain.Role` = `EnemyRole.Tank`, `_threatScanRadius` = 14, scale ≈ 1.3–1.4× |
| `Enemies/HollowMender.prefab` | `HollowWalker.prefab` | Swap mesh → Witch, Animator → `HumanoidEnemy.controller`, `EnemyBrain.Role` = `EnemyRole.Healer`, `_healScanRadius` = 8, `_healAmount` = 20, `_healInterval` = 2.5 |
| `Enemies/HollowCaster.prefab` | `HollowRogue.prefab` | Swap mesh → Skeleton_Mage, Animator → `HumanoidEnemy.controller`, `EnemyBrain.Role` = `EnemyRole.Ranged` |

Each prefab must carry: `Enemy`, `EnemyBrain`, `NavMeshAgent`, `EnemyDamageable`,
`Animator`. `EnemyBehaviorTree` is optional — attach only if you want BT targeting
for a specific archetype (the Caster is a good candidate for a "hold at range + bolt"
BT extension; see §A-6 below).

**Set `Enemy._enemyDefId`** in the inspector (or via `Configure()`) to match the
`enemies.json` id: `"hollow-brute"`, `"hollow-mender"`, `"hollow-caster"`.

**Verify:** Hit Play, `EnemyFamilyTestSpawner` with each new prefab. Tank charges
hero on proximity. Healer finds an ally at < 70% HP and heals. Caster marches and
its `EnemyRole.Ranged` does not crash (current Ranged falls back to Heart-march, which
is correct — the ranged-stop extension is §A-6).

---

## A-5 — WaveEnemyGroup SOs: first mixed-role groups

Create three new `WaveEnemyGroup` ScriptableObjects in `Assets/Data/Waves/` (or
wherever existing group SOs live — check the Project window):

**`WaveGroup_LightRaid_WithTank.asset`** — ThreatValue 6
- Entry 0: HollowWalker × 4, Role DPS, Formation Line
- Entry 1: HollowBrute × 1, Role Tank

**`WaveGroup_HealerPack.asset`** — ThreatValue 7
- Entry 0: HollowWarrior × 3, Role DPS, Formation Wedge
- Entry 1: HollowMender × 1, Role Healer

**`WaveGroup_CasterLine.asset`** — ThreatValue 8
- Entry 0: HollowWalker × 3, Role DPS, Formation Line
- Entry 1: HollowCaster × 2, Role Ranged

Assign these to `WaveManager._waveGroupSequence` at wave slots 5, 7, and 9
respectively (after the first 4 waves establish the base threat rhythm; adjust to
taste after playtesting).

**Verify:** Run wave 5 in Play. Confirm the Brute charges the hero inside `_threatScanRadius`;
the Walkers continue to march the Heart. Run wave 7 — confirm Mender moves toward the
most-wounded Warrior and HP ticks up. Console log `[EnemyBrain]` lines should name
the heal target.

---

## A-6 — Hollow Caster ranged-stop behavior (EnemyBehaviorTree extension)

The Caster's `EnemyRole.Ranged` today marches the Heart (EnemyBrain fallback path).
To make it actually stop and "cast" at range, attach `EnemyBehaviorTree` to the
`HollowCaster.prefab` and extend the tree with a hold-at-range branch:

```csharp
// In EnemyBehaviorTree.BuildTree(), add before the chase branch:
// 3b. Caster: hold at preferred range and cast
new Sequence(
    new Condition(() => _brain.Role == EnemyRole.Ranged),
    new Condition(IsInPreferredCastRange),
    new ActionNode(HoldAndCast)
),
```

Where `_preferredCastRange = 8f` (serialized field, default). `HoldAndCast` calls
`_enemy.SetBrainTargetPosition(transform.position)` (hold) and fires the animator
`Cast` trigger to play the Skeleton_Mage's casting animation. Actual projectile VFX
wiring is Phase B.

**`EnemyBrain.TriggerAttack()`** is the hook already reserved for this in the code
(`// Expand here for ranged enemies.` comment at line 126). Wire the Cast trigger there
so any future BT node that calls `TriggerAttack()` fires the animation.

**Verify:** Caster stops at ~8 m from the Heart, plays idle/cast loop, does not close
to melee. Mage.Attack animation clip plays on the `Cast` trigger.

---

# Phase B — VFX layer: Mirza Beig Ultimate VFX (P1)

**Pack is already installed** at `Assets/Mirza Beig/Particle Systems/Ultimate VFX/`.
All prefab paths below are confirmed from the on-disk file listing.

**Pack structure recap:**
- `Prefabs/Oneshot/` — base demo oneshots (impacts, smoke, fire, smoke, portals)
- `Prefabs/Loop/` — base demo loops (auras, portals, ambient)
- `Expansions/XP - ACTION/` — explosions, flamethrower, dark smoke
- `Expansions/XP - CONSTR. KIT/` — building-block oneshots: hitballs, rings, shockwaves, embers, sparks, electricity, lightning, blobs, shards, leaves, snow, liquids, smoke wisps
- `Expansions/XP - SHOCKWAVES/` — crystal nova
- `Expansions/XP - STORM/` — rain, snow, fog (atmosphere)
- `Expansions/XP - TITLES/` — fire, embers, streaks (title screen ambient)

All paths below use the short form. Full path prefix for all = `Assets/Mirza Beig/Particle Systems/Ultimate VFX/`.

---

## B-1 — Hero ability VFX (Q/W/E/R cast effects)

**File:** `Assets/_Modules/Village/Hero/AbilityVfxKit.cs` (already exists — check if
VFX prefab slots are exposed; wire them if not).

Assign per ability slot:

| Slot | Prefab | Notes |
|---|---|---|
| **Q — basic bolt** (travel) | `Expansions/XP - CONSTR. KIT/Prefabs/Oneshot/Hitballs/pf_vfx-ult_xp-ckit_psys_oneshot_hitBall2.prefab` | Small, fast-moving. Tint to hero element color. |
| **Q — impact** | `Expansions/XP - CONSTR. KIT/Prefabs/Oneshot/Rings/pf_vfx-ult_xp-ckit_psys_oneshot_hitRing2-solid.prefab` | Spawn at hit point. |
| **W — AoE burst** | `Expansions/XP - ACTION/Prefabs/Oneshot/pf_vfx-ult_xp-action_psys_oneshot_explosion2.prefab` | AoE impact at ground. Use `explosion-colour` variant for element colour. |
| **E — buff/utility** | `Expansions/XP - CONSTR. KIT/Prefabs/Oneshot/Sparkles/pf_vfx-ult_xp-ckit_psys_oneshot_sparkle3-burst.prefab` | On the hero at cast point. |
| **R — ultimate** | `Prefabs/Oneshot/pf_vfx-ult_demo_psys_oneshot_ultima2.prefab` | Largest one in the base pack — use for the hero's power move. |
| **R — shockwave** | `Expansions/XP - CONSTR. KIT/Prefabs/Oneshot/Shockwaves/pf_vfx-ult_xp-ckit_psys_oneshot_distortedShockwave2.prefab` | Layer under the ultima burst. |

**Verify:** Play Village scene, trigger each ability. VFX spawns at the correct
world position and auto-destroys. No particles linger after the clip lifetime.

---

## B-2 — Arcane Tower projectile spell

**Files:** `Assets/_Modules/Village/Buildings/ProjectilePool.cs`,
`Assets/_Modules/Village/Buildings/PooledProjectile.cs`, `Tower.cs`

1. Locate the current projectile prefab (`Tower.cs` `[SerializeField]` → `ProjectilePool._prefab`).
2. **Travel prefab:** assign `Expansions/XP - CONSTR. KIT/Prefabs/Oneshot/Hitballs/pf_vfx-ult_xp-ckit_psys_oneshot_hitBall2.prefab`
   (small, compact — reads clearly at tower-fire speed).
3. **Impact prefab:** spawn on `PooledProjectile.ReturnToPool()` / `OnHit`:
   - `Expansions/XP - CONSTR. KIT/Prefabs/Oneshot/Shockwaves/pf_vfx-ult_xp-ckit_psys_oneshot_distortedShockwave-light.prefab`
   - + `Expansions/XP - CONSTR. KIT/Prefabs/Oneshot/Sparks/pf_vfx-ult_xp-ckit_psys_oneshot_sparks.prefab`

**Tier color tinting:**
- Tier 1: violet (Withering-adjacent — the tower counters the dark)
- Tier 2: gold/amber
- Tier 3: white-core — use the `hitBall2-burst2` variant for a bigger pop

**Verify:** Build Arcane Tower, fire at wave. Projectile flies, impact plays.
No physics collider conflict with the VFX prefab (set Prefab Layer to `Ignore Raycast`).

---

## B-3 — Hollow Caster ranged bolt VFX

The Caster holds at range and fires a Withering bolt (§A-6). Wire to `HoldAndCast()`:

1. Add `[SerializeField] private GameObject _casterBoltPrefab;` to a new
   `EnemyCasterVfx` component (keep `EnemyBehaviorTree` scope-clean).
2. **Travel prefab:**
   `Expansions/XP - CONSTR. KIT/Prefabs/Oneshot/Blobs/pf_vfx-ult_xp-ckit_psys_oneshot_blob-hollow.prefab`
   — the hollow blob reads as a slow, ominous Withering projectile rather than a clean bolt.
3. **Impact prefab:**
   `Prefabs/Oneshot/pf_vfx-ult_demo_psys_oneshot_purplePuff.prefab`
   — dark purple puff on contact. Layer with `smokeWisps` for a lingering trail:
   `Expansions/XP - CONSTR. KIT/Prefabs/Oneshot/Smoke Wisps/pf_vfx-ult_xp-ckit_psys_oneshot_smokeWisps.prefab`

Spawn the bolt at the Skeleton_Mage's staff-tip bone (`staff_tip` or nearest hand bone),
travel toward `HeartController.transform.position`. Despawn on contact or after 3 s.

**Verify:** Hollow Caster holds at ~8 m, fires a dark blob every `_attackInterval`.
Purple puff lands at the Heart / structure. Reads as Withering magic, distinct from tower.

---

## B-4 — Healer pulse VFX

Wire to `EnemyBrain.TickHeal()` immediately after `ally.Heal(_healAmount)`:

1. Add `[SerializeField] private GameObject _healPulsePrefab;` to `EnemyBrain`.
2. **Heal pulse prefab:**
   `Expansions/XP - CONSTR. KIT/Prefabs/Oneshot/Sparkles/pf_vfx-ult_xp-ckit_psys_oneshot_sparkle3-burst.prefab`
   Tint **pale green** (override `Start Color` in the prefab instance or use a recoloured duplicate).
3. Instantiate at `ally.transform.position + Vector3.up * 1f` on each tick.
   The prefab is self-destroying — no manual cleanup needed.

**Verify:** Mender heals a wounded Warrior. Green sparkle burst plays on the Warrior
at each heal tick. Frequency matches `_healInterval` (default 2.5 s).

---

## B-5 — Enemy elemental auras (Loop prefabs, child GameObjects)

Add as a **child GameObject** on each relevant prefab, always active. No code needed
for this first pass — static aura is correct until status-event toggling is scoped.

| Enemy | Prefab | Mount | Scale |
|---|---|---|---|
| **Hollow Caster** | `Prefabs/Loop/pf_vfx-ult_demo_psys_loop_electroCore.prefab` | Root | 0.5× |
| **Necromancer** | `Prefabs/Loop/pf_vfx-ult_demo_psys_loop_ghostPortal.prefab` | Root | 1.1× |
| **Hollow Brute** | `Prefabs/Loop/pf_vfx-ult_demo_psys_loop_dusty.prefab` | Root | 0.8× |
| **Hollow Mender** | `Prefabs/Loop/pf_vfx-ult_demo_psys_loop_finalRest.prefab` | Root | 0.6× |
| **Ice (reserved — FrostGolem / Ice Wolf)** | `Expansions/XP - STORM/Prefabs/Loop/pf_vfx-ult_xp-storm_psys_loop_lightSnow2.prefab` | Root | 0.8× |
| **Fire (reserved — Wildlands Raider)** | `Expansions/XP - TITLES/Prefabs/Loop/pf_vfx-ult_xp-titles_psys_loop_fire.prefab` | Root | 0.6× |

The Mender's aura is named `finalRest` — fits the narrative (it is grief trying to
restore something it cannot save). The Caster's `electroCore` reads as crackling
Withering energy; the Necromancer's `ghostPortal` as a rift in the world.

**Verify:** Each enemy type reads its role on sight. Check none of the aura prefabs
have world-space particle emission that breaks with enemy movement — if so, set
Simulation Space to `Local` on the particle system.

---

## B-6 — Hit impacts and skeleton death bursts

Wire at two points:

### Hit impact — `EnemyDamageable.ApplyDamage()`

Spawn at hit world position, facing the incoming hit direction:

**Prefab:** `Expansions/XP - CONSTR. KIT/Prefabs/Oneshot/Hitballs/pf_vfx-ult_xp-ckit_psys_oneshot_hitBall2-burst2.prefab`

Scale 0.4× — small, fast. Keep below 0.4 s lifetime so it doesn't clutter screen
during dense waves.

### Skeleton death — `Enemy.Die()`

The Hollow Ones are bone — death should scatter:

- `Expansions/XP - CONSTR. KIT/Prefabs/Oneshot/Shards/pf_vfx-ult_xp-ckit_psys_oneshot_shards2-burst2.prefab`
  — reads as bone scatter. Layer with:
- `Expansions/XP - CONSTR. KIT/Prefabs/Oneshot/Smoke Wisps/pf_vfx-ult_xp-ckit_psys_oneshot_smokeWisps.prefab`
  — the Withering leaving the body.

**Boss / Necromancer death** (heavier):
- `Expansions/XP - ACTION/Prefabs/Oneshot/pf_vfx-ult_xp-action_psys_oneshot_explosion6.prefab`
- + `Expansions/XP - CONSTR. KIT/Prefabs/Oneshot/Shockwaves/pf_vfx-ult_xp-ckit_psys_oneshot_distortedShockwave2.prefab`

**Verify:** Enemy hit → small burst. Skeleton dies → shards + smoke wisp. Necromancer dies →
big explosion + shockwave ring. No particles linger after `Destroy()` — check all
prefab lifetimes are shorter than the enemy's death animation duration.

---

## B-7 — Bonus uses (no code change, editor-only drops)

These require no code — just prefab drops in the Unity scene or inspector.

| Use | Prefab | Where |
|---|---|---|
| **Title screen ambient embers** | `Expansions/XP - TITLES/Prefabs/Loop/pf_vfx-ult_xp-titles_psys_loop_embers2.prefab` | Child of `TitleScreen` Canvas root, behind the hero flanks |
| **Title screen light streaks** | `Expansions/XP - TITLES/Prefabs/Loop/pf_vfx-ult_xp-titles_psys_loop_streaks.prefab` | Same parent, lower depth |
| **Dungeon portal entrance** | `Prefabs/Loop/pf_vfx-ult_demo_psys_loop_hyperspace.prefab` | Child of `DungeonEntrance` prefab trigger volume |
| **Dungeon ground fog** | `Expansions/XP - STORM/Prefabs/Loop/pf_vfx-ult_xp-storm_psys_loop_groundFog.prefab` | Room-level ambient in `Dungeon_HealersCottage.unity` |
| **Heart / Spire ambient pulse** | `Prefabs/Loop/pf_vfx-ult_demo_psys_loop_nucleus.prefab` | Child of `HeartController` root at scale 0.5×, always active |

---

# Phase C — Verification + commit

## C-1 — Full integration smoke test

Play village from wave 1 through wave 9 (the first mixed-role groups arrive at wave 5).
Confirm:

- Waves 1–4: unchanged (Walker, Warrior, Rogue — DPS only).
- Wave 5: Hollow Brute spawns, charges hero within aggro range, soaks hits.
- Wave 7: Hollow Mender spawns, heals most-damaged Warrior (confirm Console:
  `[EnemyBrain] Healing ...`).
- Wave 9: Hollow Casters hold at range, fire violet bolts toward the Heart.
- Arcane Tower fires spell projectile at all three new types.
- All three have visible auras.
- Hit impacts and death bursts play on all enemy types.

## C-2 — Bug log

Add to `docs/qa/bug-log.md`:

```
BUG-023 | Open | Enemy mob sets — tank/healer/ranged archetypes not yet live | 2026-05-27
BUG-024 | Open | VFX layer (Spells Pack + RPG FVX) — not yet imported/wired | 2026-05-27
```

Close both bugs by wave when confirmed in Play.

## C-3 — Commit

```powershell
git add `
    Assets/StreamingAssets/Data/Canonical/enemies.json `
    Assets/_Modules/BattleATB/Engine/Defs.cs `
    Assets/_Modules/Village/Enemies/EnemyBehaviorTree.cs `
    Assets/_Modules/Village/Enemies/EnemyBrain.cs `
    Assets/Resources/Enemies/ `
    Assets/Data/Waves/ `
    Assets/VFX/ `
    docs/enemy-mob-sets-work-order.md `
    docs/qa/bug-log.md

git commit -m "Add tank/healer/ranged enemy archetypes + VFX layer

Phase A: Hollow Brute (Tank), Hollow Mender (Healer), Hollow Caster (Ranged).
enemies.json, Defs.cs ATB entries, prefabs, WaveEnemyGroup SOs. EnemyBrain
Tank/Healer roles now have live enemies to run them. Caster holds at range.

Phase B: Spells Pack + RPG FVX wired — hero abilities, tower projectiles,
caster bolt, healer pulse, elemental auras, hit impacts, death bursts.

Closes BUG-023, BUG-024 (when verified in Play).
See docs/enemy-mob-sets-work-order.md."

git push origin master
```

---

# Open questions for owner before shipping

1. **Hollow Mender model choice** — Witch is the strongest visual fit (healer-coded,
   cauldron props). Acceptable? Alternative is `Skeleton_Mage` with a recolour.
2. **Caster hold-range tuning** — `_preferredCastRange = 8f` is a starting point.
   Tune per playfeel; expose in the `HollowCaster.prefab` inspector.
3. **Aura always-on vs. event-driven** — always-on first pass is fast; event-driven
   (aura flares on heal/attack, dims at rest) is a polish pass. Confirm scope.
4. **Wave slot numbers** — waves 5/7/9 are design anchors from the codex. Adjust to
   taste after first playtest.
5. **VFX prefab paths** — integrator should note the real Spells Pack and RPG FVX
   folder paths from the Unity Project window before starting Phase B (both packs
   are already installed). Descriptive names in the work order are placeholders.
