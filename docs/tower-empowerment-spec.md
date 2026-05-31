# Tower Empowerment Spec — Defenders of the Realm (Unity v2)

**Status:** Design spec — review-and-approve before implementation. **Owner to ratify ability names, costs, and elemental assignments.**
**Game:** Defenders of the Realm, Unity 6 LTS, URP.
**Owner:** DeNelle Studios.
**Date:** 2026-05-27.
**Author:** Game-design agent.

**Source docs:** `Assets/_Modules/Core/Data/TowerData.cs` (3-level upgrade chain), `Assets/_Modules/Village/Buildings/Tower.cs` (`MaxLevel = 3`, `SpecialAbility`), `Assets/_Modules/Village/Buildings/TowerCombat.cs` (auto-fire loop), `Assets/_Modules/Core/Data/SpecialAbility.cs`, `docs/elemental-codex.md`.

---

## 0. Summary

A max-level tower has proven itself. The player has invested significantly in one defensive position. **Empowerment** is the reward for that investment: a one-time unlock that transforms the tower from a good defensive asset into a *landmark* — something the player builds strategy around.

Every tower type gets **one unique Empowerment ability** that changes its fundamental behavior rather than simply adding a damage multiplier. Empowerment is expensive and limited — the player chooses which towers earn it.

---

## 1. Design Intent

**What empowerment is not:**
- Not a fourth upgrade level. MaxLevel stays at 3. Empowerment is a separate lane.
- Not a passive multiplier. "+20% damage" is boring. Every empowerment changes how the tower *works*.
- Not available until the tower is at Level 3. Empowerment is a prestige state.

**What empowerment is:**
- A one-time, irreversible unlock available only at Level 3.
- A mechanical identity shift — the tower gains a new behavior that requires the player to think about it differently.
- Visually distinct — an empowered tower glows with its elemental aura so the player can read the battlefield at a glance.
- Expensive and intentionally scarce — a fully empowered tower line is a late-game achievement.

---

## 2. The Empowerment Loop (player-facing)

1. Build a tower (Level 1). Upgrade it to Level 2, then Level 3.
2. At Level 3, the upgrade button is disabled ("Max Level"). A new **Empower** button appears in the tower UI — gold-framed, glowing.
3. The player taps **Empower**. Cost is shown: **Aether Crystals** (a rare currency — see §4). If they have enough, confirm prompt fires.
4. On confirm, the tower plays the **Empowerment VFX sequence** (nova burst + persistent glow ring). The Empower button is replaced by a glowing **"Empowered"** badge.
5. The tower now fires/behaves according to its Empowerment ability for the rest of the session (and across saves — it's persistent).

---

## 3. Per-Tower Empowerment Abilities

### 3.1 Arcane Tower — "Mana Surge" *(Aether element)*

**Design:** The tower normally fires one bolt per tick at the nearest enemy. Mana Surge changes the fire cadence: every **5th shot** becomes a **triple-burst** — three bolts fire in a 30°-spread fan, each dealing 60% of normal damage. The burst can hit multiple enemies if they are clustered.

**Mechanical feel:** The tower pulses between normal operation and a high-intensity moment. The player learns to group enemies so the burst makes contact. Pairs naturally with the Hollow Mender archetype — the burst can outrange a healer hiding behind a Brute screen.

**Visual:**
- At empower: `Prefabs/Oneshot/pf_vfx-ult_demo_psys_oneshot_ultima2.prefab` (full-size) — a single dramatic nova.
- Persistent: `Prefabs/Loop/pf_vfx-ult_demo_psys_loop_nucleus.prefab` (dim, tinted violet) orbiting the tower tip.
- Burst fire: the three bolts use `hitBall2.prefab` (standard arcane bolt) — the fan spread reads clearly.
- Burst impact: `hitRing2-solid.prefab` (smaller scale than normal).

**ATB element:** Aether. Burst bolts count as Aether damage for elemental resistance purposes.

**Ability name (owner to ratify):** Mana Surge.

---

### 3.2 Frost Tower *(planned)* — "Glacial Core" *(Ice element)*

**Design:** A Frost Tower normally fires slowing bolts at individual targets. Glacial Core makes the tower a **permanent AoE slow field**: all enemies within the tower's range move at 70% speed, at all times, regardless of whether they have been hit. The frost aura emanates from the tower itself — no projectile needed. The tower still fires standard frost bolts in addition to the aura.

**Mechanical feel:** Transforms the tower from "I slow what I hit" to "I own this territory." Any enemy that enters the frost zone is compromised. Players place a Glacial Core tower at a choke point to create a guaranteed slow corridor for other towers to exploit.

**Visual:**
- At empower: `explosion2` tinted ice blue (inverted — outward cold burst, not fire).
- Persistent: `XP-STORM/pf_vfx-ult_xp-storm_psys_loop_lightSnow2.prefab` (large radius loop at tower base) + `pf_vfx-ult_xp-storm_psys_loop_groundFog.prefab` (tight, cold-blue fog ring).
- Enemy slow debuff indicator: a brief `hitBall2-burst2` tinted blue on each newly-slowed enemy.

**ATB element:** Ice.

**Ability name (owner to ratify):** Glacial Core.

---

### 3.3 Flame Tower *(planned)* — "Eternal Ember" *(Flame element)*

**Design:** A Flame Tower normally fires high-damage single bolts. Eternal Ember adds a **Burn status** to every hit: enemies take 4 damage per second for 4 seconds (16 total Burn damage per hit). Burn stacks are capped at 1 per enemy — re-hitting a burning enemy resets the timer rather than adding a second stack. This rewards focused fire over the Brute enemies with high HP.

**Mechanical feel:** "Light it and let it burn." The tower's DPS doubles on any enemy it has time to work on. The player learns to let burning enemies walk into other towers' range rather than wasting shots. Pairs with slow-effect towers (Frost Tower + Flame Tower = slow the enemy, then burn it as it drags through the kill zone).

**Visual:**
- At empower: `XP-TITLES/pf_vfx-ult_xp-titles_psys_loop_fire.prefab` (one-shot burst, large) for the nova.
- Persistent: `fire.prefab` loop (small) at the tower's muzzle point — the tower always appears to be "hot."
- Enemy Burn debuff: `fire.prefab` loop (tiny) attached to the burning enemy as a child for the 4-second duration.

**ATB element:** Flame.

**Ability name (owner to ratify):** Eternal Ember.

---

### 3.4 Arrow Tower *(planned)* — "True Aim" *(Physical element)*

**Design:** An Arrow Tower normally targets the single nearest enemy. True Aim adds a **secondary targeting lock**: the tower acquires *two* targets each fire tick — the nearest enemy (primary) and the highest-HP enemy within range (secondary). Both arrows fire simultaneously. If primary and secondary are the same enemy, both arrows hit the same target.

**Mechanical feel:** The tower becomes a priority-kill expert. It naturally pressure-tests the most dangerous enemy in range while clearing the nearest threat. Against a Necromancer + Walker escort, True Aim puts an arrow into the boss every tick while still clearing the fodder screen.

**Visual:** No persistent VFX loop — this is a Physical-element tower and the aura-less identity is intentional (see `elemental-codex.md` §1). The second arrow is the visual tell. At empower, a single `shards2-burst2` burst (bone-white) fires from the tower tip.

**ATB element:** Physical.

**Ability name (owner to ratify):** True Aim.

---

## 4. Aether Crystals — the Empowerment Currency

Empowerment costs a dedicated rare currency: **Aether Crystals** (display name: "Crystals" in UI shorthand, full name in the store). They are not the same as the standard gold/coin currency used for building and upgrading.

### Sources

| Source | Yield | Condition |
|---|---|---|
| Crystal Mine (max level) | +1 Crystal per completed wave | Passive; Crystal Mine must be built and upgraded to L3 |
| Wave bonus chest | +1–2 Crystals | Rare drop on completing a wave without any enemy reaching the Heart |
| Boss wave completion | +3 Crystals | On defeating the Necromancer wave-boss |
| Dungeon completion | +2–5 Crystals | Per dungeon, once per dungeon |

### Empowerment costs (suggested — owner to tune)

| Tower | Crystal cost |
|---|---|
| Arcane Tower | 8 Crystals |
| Frost Tower | 10 Crystals |
| Flame Tower | 10 Crystals |
| Arrow Tower | 6 Crystals |

**Design rationale:** At +1 Crystal per wave from a maxed Crystal Mine, the player accrues roughly 1 empowerment worth of crystals every 8–10 waves. The early game should feel like "I can empower *one* tower this run." A full fortification of all towers is a long-term goal.

---

## 5. Data Model — Changes Required

All changes are **additive** and do not break existing TowerData assets. No existing `.cs` files require non-additive edits.

### 5.1 New enum: `EmpowermentAbility`

Lives in `Assets/_Modules/Core/Data/SpecialAbility.cs` alongside the existing `SpecialAbility` enum:

```csharp
/// <summary>
/// The unique ability a tower unlocks at max level via Empowerment.
/// Separate from SpecialAbility (which is the per-upgrade passive).
/// </summary>
public enum EmpowermentAbility
{
    None,
    ManaSurge,       // Arcane Tower — triple-burst every 5th shot
    GlacialCore,     // Frost Tower — permanent AoE slow field
    EternalEmber,    // Flame Tower — Burn DoT on every hit
    TrueAim,         // Arrow Tower — dual-target lock
}
```

### 5.2 New serializable class: `TowerEmpowermentData`

Add to `Assets/_Modules/Core/Data/TowerData.cs` (before the TowerData class):

```csharp
/// <summary>
/// Authoring data for a tower's max-level Empowerment — the ability unlocked
/// after Level 3. Cost is in Aether Crystals (a separate rare currency).
/// </summary>
[System.Serializable]
public class TowerEmpowermentData
{
    [Tooltip("Human-readable ability name shown in the Empower button tooltip.")]
    public string abilityName = "Empowerment";

    [Tooltip("Short description shown in the Empower confirm popup.")]
    [TextArea(2, 4)]
    public string abilityDescription = "Unlocks a unique ability for this tower.";

    [Tooltip("Aether Crystal cost to activate.")]
    [Min(1)] public int crystalCost = 8;

    [Tooltip("Which empowerment behavior to activate in TowerCombat.")]
    public EmpowermentAbility ability = EmpowermentAbility.None;

    [Tooltip("VFX prefab to instantiate once at the moment of empowerment (oneshot nova).")]
    public GameObject empowerNovaPrefab;

    [Tooltip("VFX prefab to keep as a child of the tower permanently after empowerment.")]
    public GameObject empowerAuraPrefab;
}
```

Then add a single field to `TowerData`:

```csharp
[Header("Empowerment (unlocked at Max Level)")]
public TowerEmpowermentData empowerment;
```

### 5.3 Tower.cs additions

Add to `Tower.cs`:

```csharp
// ── Empowerment ───────────────────────────────────────────────────────────

/// <summary>True once the player has paid the crystal cost and activated empowerment.</summary>
public bool IsEmpowered { get; private set; }

/// <summary>
/// Called by TowerEmpowerButton when the player confirms the Empower action.
/// Validates max level, deducts crystals, fires the VFX, and marks IsEmpowered.
/// </summary>
public bool TryEmpower()
{
    if (_currentLevel < MaxLevel)
    {
        Debug.LogWarning("[Tower] Empower attempted below max level.");
        return false;
    }
    if (IsEmpowered)
    {
        Debug.LogWarning("[Tower] Already empowered.");
        return false;
    }
    if (_data?.empowerment == null || _data.empowerment.ability == EmpowermentAbility.None)
    {
        Debug.LogWarning("[Tower] No empowerment data authored on this TowerData.");
        return false;
    }

    // Deduct Aether Crystals via CrystalEconomy (new service — see §5.5).
    if (!CrystalEconomy.Instance.TrySpend(_data.empowerment.crystalCost))
        return false;

    IsEmpowered = true;
    ApplyEmpowermentVFX();

    // Notify TowerCombat so it picks up the new behavior on the next fire tick.
    GetComponent<TowerCombat>()?.OnEmpowered(_data.empowerment.ability);

    Debug.Log($"[Tower] '{_data.towerName}' empowered with {_data.empowerment.ability}.");
    return true;
}

private void ApplyEmpowermentVFX()
{
    var emp = _data.empowerment;
    if (emp.empowerNovaPrefab != null)
    {
        // Oneshot nova — instantiate at world, auto-destroy.
        var nova = Instantiate(emp.empowerNovaPrefab, transform.position, Quaternion.identity);
        Destroy(nova, 4f);
    }
    if (emp.empowerAuraPrefab != null)
    {
        // Persistent aura — parent to tower, lives for tower's lifetime.
        Instantiate(emp.empowerAuraPrefab, transform.position, Quaternion.identity, transform);
    }
}
```

### 5.4 TowerCombat.cs additions

Add to `TowerCombat.cs`:

```csharp
private EmpowermentAbility _empowerment = EmpowermentAbility.None;
private int _shotsSinceLastBurst = 0;   // ManaSurge counter

/// <summary>Called by Tower.TryEmpower after crystal deduction. Activates the ability.</summary>
public void OnEmpowered(EmpowermentAbility ability)
{
    _empowerment = ability;

    // GlacialCore — start the slow-field coroutine.
    if (ability == EmpowermentAbility.GlacialCore)
        StartCoroutine(SlowFieldLoop());
}

// In the existing Fire() method, add after the standard projectile fire:
private void ApplyEmpowermentEffect(IDamageable target)
{
    switch (_empowerment)
    {
        case EmpowermentAbility.ManaSurge:
            _shotsSinceLastBurst++;
            if (_shotsSinceLastBurst >= 5)
            {
                _shotsSinceLastBurst = 0;
                FireManaSurgeBurst();   // fires 2 additional bolts in a spread
            }
            break;
        case EmpowermentAbility.EternalEmber:
            // Apply Burn status via a new BurnEffect component on the enemy.
            var enemy = (target as EnemyDamageable)?.Enemy;
            if (enemy != null)
                BurnEffect.Apply(enemy, damage: 4f, duration: 4f);
            break;
        case EmpowermentAbility.TrueAim:
            // Handled in the targeting pass — TrueAim acquires a second target.
            break;
        // GlacialCore handled in SlowFieldLoop(); no per-shot logic.
    }
}
```

### 5.5 New service: `CrystalEconomy`

A lightweight singleton mirroring `EconomyService` for Aether Crystals:

```
Assets/_Modules/Village/CrystalEconomy.cs
```

Exposes `TrySpend(int cost) → bool`, `AddCrystals(int amount)`, `CurrentCrystals`. Persists to `GameState` via a new `aetherCrystals` int field in `PlayerState`. **No blockchain — this is a local-only off-chain currency.**

---

## 6. UI — Empower Button

Location: `Assets/_Modules/Village/Buildings/UI/TowerEmpowerButton.cs` (new file, alongside `TowerUpgradeButton.cs`).

### Behavior

- Hidden until `tower.CurrentLevel == Tower.MaxLevel`.
- Shows crystal cost (icon + number). If `CrystalEconomy.CurrentCrystals < crystalCost`, button is greyed with "Need X Crystals."
- On tap: show a confirm popup with the ability name + description.
- On confirm: call `tower.TryEmpower()`. If `true`, swap button to "Empowered" badge (gold star icon). If `false` (insufficient crystals), show "Not enough Crystals" toast.

### Inspector wiring

Add `TowerEmpowerButton` to the `upgradeUIPrefab` layout in the same canvas as `TowerUpgradeButton`. The existing `TowerManagerPanel` can wire it the same way — it already has a tower reference.

---

## 7. Implementation Order

This is a **design spec**, not an implementation work order. When the owner ratifies the design, the implementation ticket (suggested: `DEF-90 Tower Empowerment`) should execute these steps in order:

1. Add `EmpowermentAbility` enum to `SpecialAbility.cs`.
2. Add `TowerEmpowermentData` class and `empowerment` field to `TowerData.cs`.
3. Create `CrystalEconomy.cs` singleton (mirrors `EconomyService`).
4. Add `aetherCrystals` to `GameState.PlayerState` + `SaveSchema` + `SaveMigrator` (add migration step: default 0).
5. Wire Crystal Mine max-level perk: on wave complete, `CrystalEconomy.Instance.AddCrystals(1)`.
6. Add `IsEmpowered`, `TryEmpower()`, `ApplyEmpowermentVFX()` to `Tower.cs`.
7. Add `OnEmpowered()`, `ApplyEmpowermentEffect()`, `SlowFieldLoop()`, `BurnEffect.Apply()` to `TowerCombat.cs`.
8. Create `TowerEmpowerButton.cs` UI component.
9. Author Arcane Tower empowerment in the `ArcaneTower` TowerData asset: set `empowerment.ability = ManaSurge`, `crystalCost = 8`, wire nova + aura VFX prefabs (see `elemental-codex.md §4 — Aether`).
10. Verify: place Arcane Tower, upgrade to L3, empower with 8 crystals, confirm triple-burst fires on every 5th shot, nova VFX plays once, aura loop persists.

---

## 8. Ratification Checklist

- [ ] Ability names (Mana Surge, Glacial Core, Eternal Ember, True Aim) — approve or rename
- [ ] Aether Crystal economy — is "Crystal Mine L3 = +1 per wave" the right acquisition rate?
- [ ] Empowerment costs (8 / 10 / 10 / 6 Crystals) — approve or adjust
- [ ] ManaSurge burst count trigger (every 5th shot) — approve or change
- [ ] EternalEmber Burn stats (4 dmg/sec × 4 sec = 16 total) — approve or tune
- [ ] GlacialCore slow percentage (70% speed = 30% slow) — approve or tune
- [ ] TrueAim second-target selection rule (highest HP within range) — approve or change
- [ ] CrystalEconomy: confirm off-chain (local only), not connected to SKR token or wallet
- [ ] `aetherCrystals` field name in `GameState` — approve the save-schema key

---

## 9. Completing the Level-4 / "Imbued" Tier

**Status:** READY — owner-ratified. This section completes the empowerment set
the original spec opened. The owner has confirmed three decisions, treated as
final here: (1) the imbuement set was **missing a Healing / Ward aura** and one
is added below; (2) empowerment is presented to the player as the **"Imbued"
tier / "Level 4"** while the code structure stays MaxLevel 3 + Empowerment
prestige; (3) two further behavior-changing imbuements round out the set so the
tier reads as a complete, curated collection rather than four scattered perks.

All new imbuements follow the original §1 design intent: **behavior-changing,
not flat multipliers**, one identity per imbuement, visually legible via an
elemental aura. They reuse the existing data model from §5 — each is a new
`EmpowermentAbility` enum value plus authoring data on the relevant TowerData.
No `MaxLevel` change is required (see §9d for the option flagged for the owner).

### 9a. Wardlight — the Healing / Ward Aura *(Aether)*

**The missing piece.** The set had offense (Mana Surge, True Aim), control
(Glacial Core), and attrition (Eternal Ember) — but nothing that *kept the
realm standing*. Wardlight is the Arcane Tower's signature imbuement and the
clearest expression of its ward-stone identity: the spire stops being only a
weapon and becomes a **place of safety the player builds around**.

**Design.** An imbued Arcane Tower projects a **persistent ward aura** out to a
fixed radius. Two effects, both passive, both continuous:

1. **Mend** — friendly structures (towers, walls, gates, buildings) inside the
   radius regenerate HP slowly over time, up to their max. This is the
   anti-attrition layer: a wall battered in one wave knits back before the next.
2. **Ward** — structures inside the radius take **reduced incoming damage** (a
   flat percentage damage soak) while the aura holds. This is the "stand here
   and you are safer" layer that makes placement matter.

The Heart of Elarion, if it sits inside the radius, **also benefits from both
effects** — this is the high-value placement the lore practically begs for: a
ward-spire raised close to the Heart literally extends the Heart's own
protection, exactly the ward-stone fiction from the narrative bible and the
ward-tether concept in `regions-narrative-and-npcs.md`.

**Mechanical feel.** Wardlight is the **keystone tower** — the one you empower
first to make every other defense more durable. It changes the player's mental
model from "towers kill things" to "this tower is a sanctuary." It pairs
naturally with the wall-top tower layer (WO-109): a Wardlight spire near the
ramparts keeps the whole battlement section repaired between waves. It is
deliberately **not offensive** — Wardlight does not buff damage; it buys
survival, and the player must still bring the kills via other towers. The Arcane
Tower keeps firing its standard ward-stone bolts while the aura holds; the aura
is an addition, not a replacement.

**Ratification stats (owner to tune).**

| Stat | Value | Note |
|---|---|---|
| Ward radius | 8 m | Roughly 1.3× a base tower's fire range; generous enough to cover a cluster of buildings or a wall section |
| Mend rate | 2 HP/sec | Per structure inside the radius; capped at that structure's max HP |
| Mend tick cadence | every 0.5 s | Apply 1 HP per tick so the regen reads as a steady pulse, not a jump |
| Ward damage soak | 20% reduction | Flat reduction to incoming damage for structures inside the radius |
| Affects | Towers, Walls, Gates, Buildings, **and the Heart** | All `IDamageableStructure` implementers (see CLAUDE.md §6) |
| Affects enemies? | No | Aura is friendly-only; it never heals or shields a Hollow One |
| Stacking | Does not stack | Two overlapping Wardlight auras do not double mend/soak — the stronger value applies (prevents trivializing defense with two cheap spires) |

**VFX intent.** Warm, not violet — this is restoration, and the codex is firm
that healing reads warm-white while corruption reads violet (`elemental-codex.md`
§3, Healing Beacon note). Match the Hero's Healing Beacon palette for player
consistency.

- At empower (oneshot nova): `XP-CONSTR.KIT/.../sparkle3-burst.prefab` tinted
  candle-warm white (`#FFFBE8`), large scale — a single gentle bloom, not an
  explosion.
- Persistent aura: a soft ground-ring at the ward radius. Suggest
  `Prefabs/Loop/pf_vfx-ult_demo_psys_loop_finalRest.prefab` (the Hollow Mender /
  Inn-Keeper "something still cares here" loop from the codex), tinted warm white,
  scaled to the 8 m radius and kept low/dim so it does not clutter the battlefield.
- Per-structure mend tell: a tiny `sparkle3-burst` (warm white) ticking on a
  structure as it regenerates, so the player can *see* what the ward is mending.
- Heart-inside-radius emphasis (optional polish): when the Heart is inside the
  aura, brighten the Heart's own `nucleus` pulse a notch — a quiet visual reward
  for the keystone placement.

**Element:** Aether. Wardlight is the purest Aether imbuement in the set — the
Heart's own light, shared and reshared (`narrative-bible.md` §7.11).

**Ability name — owner to ratify (3 options):**

1. **Wardlight** *(recommended)* — short, bible-voiced ("old bones, modern
   English"), and ties directly to the ward-stone canon. Reads instantly as
   "this is the warding tower." It is the pick.
2. **Keeper's Mercy** — warmer, more emotional; leans on the Keeper's mourning
   tone. Strong but slightly long for a UI badge, and "Mercy" reads softer than
   the protective steel the aura actually provides.
3. **The Sheltering** — evocative and on-tone (cf. "The Withering"), frames the
   tower as an active verb. Good fallback if "Wardlight" feels too utilitarian.

> Flavor line (Empower confirm popup): *"The ward-stones wake. While the light
> holds, the stones around it stand a little longer."*

### 9b. Consecrate — the Vulnerability Aura *(Aether)*

**Design.** A second Aether imbuement, the offensive mirror of Wardlight.
Instead of protecting friends, Consecrate **marks ground against the enemy**: an
imbued tower projects a persistent aura within which **all enemies take
increased damage from every source** — this tower, other towers, the Hero's
spells, everything. It does not deal damage itself; it makes a patch of ground
*lethal* and lets the rest of the defense do more with the same shots.

**Mechanical feel.** Consecrate is the **force-multiplier / choke-point tower**.
The player learns to drop it on a kill corridor — the chokepoint where a Glacial
Core already slows the tide, or the lane every wave funnels through — so that
everything that walks the consecrated ground dies faster to fire it was already
taking. It rewards the same territorial thinking as Glacial Core but on the
damage axis instead of the speed axis, and the two combo beautifully: slow the
enemy in the frost field, consecrate the same ground, and a lane becomes a
grinder. Like Glacial Core, the value is in *where* you place it, not in raw
output.

**Ratification stats (owner to tune).**

| Stat | Value | Note |
|---|---|---|
| Consecrate radius | 7 m | Slightly tighter than Wardlight — a kill zone, not a blanket |
| Vulnerability | +25% damage taken | Applied to all damage enemies take while inside the radius |
| Applies to | All damage sources | Tower bolts, Burn DoT, Hero spells, ATB-tagged hits — element-agnostic |
| Affects | Enemies only | Never touches friendly structures |
| Stacking | Does not stack | Overlapping Consecrate auras apply the single strongest value, not additive |
| Persistence | Continuous while imbued | No charge-up; the ground is consecrated for the rest of the session |

**VFX intent.** This is *Aether turned outward against the enemy* — so it leans
toward the violet/corruption-answering end of the Aether palette, distinct from
Wardlight's warm white, so the two Aether auras never read the same at a glance.

- At empower (oneshot nova): `Prefabs/Oneshot/.../ultima2.prefab` tinted pale
  violet (`#9B6FFF`) — a sharp arcane bloom.
- Persistent aura: a faint **violet ground-ring** at the consecrate radius.
  Suggest `Prefabs/Loop/pf_vfx-ult_demo_psys_loop_ghostPortal.prefab` scaled
  flat and wide, dimmed, tinted violet — the same family as the Necromancer's
  aura but inverted in meaning (the Keeper turning the Wound's own light against
  it).
- Per-enemy mark: a brief violet `hitRing2-solid` flickers on an enemy as it
  enters the consecrated ground, so the player can read who is currently
  vulnerable.

**Element:** Aether.

**Ability name — owner to ratify:** **Consecrate** *(recommended)* — one word,
verb-as-name (cf. "The Withering," "The Sheltering"), and theologically apt for
ward-stone magic sanctifying ground. Alternates: **Hallowed Ground**, **The
Unmaking** (darker, leans into "we turn their own light on them").

> Flavor line: *"The stones name this ground. What walks here walks thinner."*

### 9c. Rally — the Haste Aura *(Aether/Physical support)*

**Design.** The third addition and the **support/tempo** imbuement. An imbued
tower projects an aura within which **other friendly towers fire faster** — a
flat fire-rate increase to every tower inside the radius (the Rally tower itself
included). It changes no single tower's behavior; it changes the *pace of the
whole defense around it*, which is a fundamentally different lever than damage or
control.

**Mechanical feel.** Rally is the **cluster anchor** — it wants to be planted in
the middle of a dense tower nest so its haste washes over as many towers as
possible. It pushes the player toward concentrated base-design (a tight ring of
towers around a Rally spire) as a deliberate alternative to spreading coverage
thin. Where Wardlight makes a cluster durable and Consecrate makes a lane
lethal, Rally makes a cluster *fast* — the three Aether auras form a clean
support trio (defend / debuff / tempo) that the player can mix to taste. It is
the most "build strategy around it" imbuement of the new three.

**Ratification stats (owner to tune).**

| Stat | Value | Note |
|---|---|---|
| Rally radius | 7 m | Same footprint discipline as Consecrate |
| Fire-rate bonus | +30% attack speed | Applied to fire cadence of every tower inside the radius |
| Affects | Friendly towers only | Not walls/gates/Heart (they don't fire); does not affect the Hero |
| Self-affecting | Yes | The Rally tower benefits from its own aura |
| Stacking | Does not stack | Overlapping Rally auras apply the single strongest value |
| Interaction note | Multiplicative with Mana Surge cadence | A Rally'd Mana Surge tower simply reaches its every-5th-shot burst sooner — no special-casing needed |

**VFX intent.** Bright, energizing, high-intensity Aether — this should read as
*urgency*, the opposite of Wardlight's calm.

- At empower (oneshot nova): `Prefabs/Loop/.../electroCore.prefab` as a brief
  burst, tinted bright violet-white — a crackle of quickened mana.
- Persistent aura: a tight, faintly-pulsing energy ring. Suggest
  `Prefabs/Oneshot/.../distortedShockwave-light.prefab` looped/retriggered at a
  steady cadence so the ground visibly "beats" — telegraphing that towers here
  are sped up.
- Per-tower tell: a small `hitBall2-burst2` (violet-white) at a hasted tower's
  muzzle when it gains the buff, so the player sees the cluster light up.

**Element:** Aether (support flavor). Could be reskinned Physical if the owner
prefers a "war-drum / banner" reading over an arcane one — flagged in the
checklist.

**Ability name — owner to ratify:** **Rally** *(recommended)* — crisp, martial,
instantly legible. Alternates: **Quickening**, **War-Song** (ties the "song"
metaphor from the bible to the towers — strong if the owner wants the Heart's
song fiction extended to the defenses).

> Flavor line: *"The song quickens. The stones hear it, and answer faster."*

### 9d. Terminology — presenting "Imbue / Level 4" without changing code

The owner has ratified that empowerment is presented to the player as the
**"Imbued" tier**, framed as a **"Level 4."** This is a **UI / copy decision
only** — the code structure from §5 does not change. Concretely:

- **`MaxLevel` stays at 3.** Empowerment remains a separate prestige lane, exactly
  as built. No enum, save-schema, or upgrade-chain change.
- **Player-facing copy** reframes the prestige state as the tower's fourth tier:
  - The **Empower** button (§6) is relabeled **"Imbue"** (gold-framed, glowing).
  - The post-empower badge reads **"Imbued — Lv 4"** (or "Imbued" with a small
    "IV" glyph) instead of "Empowered."
  - The confirm popup header reads *"Imbue this tower"* and shows the imbuement's
    bible-voiced name + flavor line as the body.
  - In any "Level X / 3" display, an imbued tower shows **"Lv 4 · Imbued"** —
    the only place the player ever sees a "4," and it is cosmetic text, not a
    `_currentLevel` value.
- **Internal naming stays "Empowerment."** Enum (`EmpowermentAbility`), data
  class (`TowerEmpowermentData`), methods (`TryEmpower`), and currency logic are
  unchanged. The "Imbued / Level 4" language lives only in display strings and
  the new imbuement names — keeping the design fiction and the code vocabulary
  cleanly separated.

> **Implementation note for CLI:** this is achievable by editing display strings
> only (`TowerEmpowerButton` labels + the confirm popup copy + the tower info
> readout). No data-model or `Tower.cs` logic change. Map each
> `EmpowermentAbility` enum value to its player-facing imbuement name in a small
> display lookup (a `switch` or a serialized name field already exists on
> `TowerEmpowermentData.abilityName` — populate it with the ratified names).

**Option flagged for the owner — a true 4th upgrade level.** If the owner would
rather make Imbue a *genuine* fourth upgrade step (tap "Upgrade" a fourth time,
spend gold + crystals, `MaxLevel = 4`) instead of a parallel prestige lane, that
is possible but is **a larger, non-additive change**: it touches `MaxLevel`, the
3-entry upgrade-stat arrays in TowerData, the upgrade-cost progression, the save
schema (level can now be 4), and `SaveMigrator`. **Recommendation: keep the
prestige-lane structure and present it as "Level 4" in copy** — the player gets
the "Level 4" feeling with zero risk to the existing save/upgrade code. The true
4th-level option is documented here only so the owner can choose it deliberately;
the rest of this spec assumes the prestige-lane structure.

### 9e. Data-model deltas for the new imbuements

Purely additive to §5 — no existing code changes beyond new enum entries and the
per-ability effect branch (the data model from §5.2 already supports authoring
all of these on any TowerData).

Extend the `EmpowermentAbility` enum (§5.1):

```csharp
public enum EmpowermentAbility
{
    None,
    ManaSurge,       // Arcane Tower — triple-burst every 5th shot
    GlacialCore,     // Frost Tower — permanent AoE slow field
    EternalEmber,    // Flame Tower — Burn DoT on every hit
    TrueAim,         // Arrow Tower — dual-target lock
    Wardlight,       // Arcane Tower — heal + damage-soak ward aura (NEW)
    Consecrate,      // Arcane Tower — enemy vulnerability aura (NEW)
    Rally,           // Arcane Tower — friendly fire-rate haste aura (NEW)
}
```

The three new auras are all **persistent radius effects**, structurally identical
to GlacialCore's `SlowFieldLoop()` (§5.4) — a coroutine that, on a fixed cadence,
finds the relevant colliders in radius and applies its effect. Suggested
implementation shape (design intent, CLI to write the actual code):

- `WardFieldLoop()` — every 0.5 s, find `IDamageableStructure` in radius; call a
  new additive `Heal(amount)` on each (capped at max HP) and register a
  damage-soak modifier while in range. (`IDamageableStructure` already covers
  Heart, towers, walls, gates, buildings — CLAUDE.md §6.)
- `ConsecrateFieldLoop()` — tag enemies in radius with a vulnerability multiplier
  applied at their damage-intake site; clear the tag when they leave the radius.
- `RallyFieldLoop()` — find `TowerCombat` components in radius; apply a fire-rate
  multiplier while in range, restore on exit.

> **Cross-module rule reminder (CLAUDE.md §5):** all three live in
> `DeNelle.Village` and touch only `DeNelle.Core` interfaces. The damage-soak and
> vulnerability modifiers must route through `IDamageableStructure` /
> the enemy damage-intake path, not reach across to HUD. Use null-conditional
> (`?.`) on any cross-module service call.

---

## 10. Arcane Tower as a Buildable Type

**Status:** READY — owner-ratified. The Arcane Tower joins the buildable roster
as the **magic / support tower**. This section reconciles it with the WO-109
roster (Ground Tower / Wall Tower / Corner Bastion) and should be promoted into
its own implementation work order (suggested **WO-113 — Arcane Tower buildable
type**); it is a design spec only here.

### 10a. Reconciling with the WO-109 roster

WO-109 defines three structural tower tiers by **placement and proportion**.
The Arcane Tower is a **fourth, orthogonal type** — it is defined by **role
(magic/support), not by where it sits.** It slots in cleanly:

| Type | Prefab | Placement | Role | Source |
|---|---|---|---|---|
| **Ground Tower** | `Tower_Medieval_Big` | Ground, standalone | Long-range, high-HP workhorse | WO-109 |
| **Wall Tower** | `Tower_Medieval_Wood` | Wall top only | Mid-range, cheap, elevated | WO-109 |
| **Corner Bastion** | `Tower_Castle_Round` | Corner positions | Auto-placed, not player-built | WO-109 |
| **Arcane Tower** | `Tower_Castle_Square` *(see 10c)* | Ground, standalone | **Magic / support — ward-stone spire** | **THIS SPEC** |

**Naming-collision flag for the owner.** WO-109's "Ground Tower" reuses
`Tower_Medieval_Big`, which the polyperfect catalog and the original empowerment
spec both label as the **Arcane Tower** building. These are currently the *same
mesh*. To make the Arcane Tower a visually distinct buildable, this spec assigns
it a **different prefab** (`Tower_Castle_Square`, §10c) so "Ground Tower" and
"Arcane Tower" do not look identical on the field. Owner to confirm the prefab
split (see §11).

### 10b. Role and base behavior

The Arcane Tower is the **magic/support tower** — the only buildable whose
identity is its Aether element and whose empowerment options are the support
auras of §9 (Wardlight / Consecrate / Rally), in addition to its existing Mana
Surge offensive imbuement.

- **Base behavior (Level 1–3, pre-imbue):** fires **ward-stone bolts** — pooled
  Aether projectiles at the nearest enemy, exactly as the current `TowerCombat`
  fire loop with the `hitBall2.prefab` (tinted pale violet) projectile and
  `hitRing2-solid` / `distortedShockwave-light` impact from `elemental-codex.md`
  §4. Single-target, moderate damage, moderate fire rate — a *reliable* damage
  dealer, not a specialist, so the player's reason to build it is its
  **empowerment ceiling** (the §9 auras), not its raw L1 output.
- **Element:** Aether (white→violet glow at the muzzle).
- **Role identity:** it is the tower you build to *enable* a base — the one whose
  Imbued tier turns a defensive cluster into a sanctuary (Wardlight), a kill zone
  (Consecrate), or a fast gun-line (Rally). It is deliberately the support pillar
  of the roster.

### 10c. Prefab, footprint, cost

| Property | Value | Note |
|---|---|---|
| **Prefab (recommended)** | `_M/Prefabs_M/..._M/Tower_Castle_Square.fbx` | A square keep-tower — reads as a built, magical spire distinct from the round Bastion and the `_Big` Ground Tower. The catalog lists it ("`Tower_Castle_Square` — square keep") but it is currently unassigned to any building, so claiming it for the Arcane Tower avoids a mesh collision. |
| **Placeholder fallback** | `Tower_Medieval_Big` (current Arcane mesh) | If `Tower_Castle_Square` is not present in the imported pack, fall back to `Tower_Medieval_Big` and `Debug.LogWarning` ("Arcane Tower prefab Tower_Castle_Square missing — using Tower_Medieval_Big placeholder"). Per CLAUDE.md §4: warning, not error — pack may not be imported. |
| **Footprint** | `2×2` grid plots | Larger than the Wall Tower (1×1); it is a landmark support structure, and a bigger footprint discourages spamming it. |
| **Placement** | Ground only (`BuildZone.Ground`) | Not a wall-top tower — it is a raised spire, structurally a ground build like the Ground Tower. |
| **Build cost** | **120 gold** (build) + standard L2/L3 upgrade costs | Between the Wall Tower (50) and the implied Ground Tower cost (≈150). Its power is in the imbue ceiling, so the base build stays affordable. |
| **Imbue cost** | Per §4 — **8 Aether Crystals** for Mana Surge; the three new auras (Wardlight / Consecrate / Rally) suggested at **10 Crystals** each (support auras are higher-impact than the offensive burst). Owner to tune in §11. |

**Build-palette wiring (design intent — mirrors WO-109's `BuildableItem`):**

```csharp
new BuildableItem
{
    id          = "arcane_tower",
    displayName = "Arcane Tower",
    prefab      = Resources.Load<GameObject>("polyperfect/Tower_Castle_Square"),
    footprint   = new Vector2Int(2, 2),
    goldCost    = 120,
    zoneRestriction = BuildZone.Ground   // ground-only, like the Ground Tower
}
```

### 10d. Tie to the ward-tether exploration lore

The Arcane Tower **is** the ward-stone, made buildable. This is the single
strongest lore hook in the roster, and it should be surfaced in copy:

- In the narrative bible, the Arcane Tower "houses the defensive ward-stones…
  they answer your call" (`narrative-bible.md` §6, §7.13). The buildable Arcane
  Tower is the player physically *raising a ward-stone* inside the walls.
- `regions-narrative-and-npcs.md` §0 establishes that **relighting ward-stones in
  the field extends the Heart's reach** (the ward-tether), and explicitly notes
  "building/relighting wards in the field is the same magic as raising the
  ward-spire at home." The buildable Arcane Tower is the **at-home half** of that
  same mechanic: the spire you raise in the village is the same craft as the
  marches' ward-stones.
- This makes the Arcane Tower the **mechanical bridge** between the Defend pillar
  (towers in the village) and the Explore pillar (ward-stones in the regions).
  When WO-112 (ward-stone relight / reach system, flagged in the regions doc) is
  built, the field ward-stones can reuse the Arcane Tower's ward-bolt + aura
  behavior — one Aether ward-craft system, two contexts.
- **Copy hook (build menu / first-build tutorial):** *"Raise a ward-stone. The
  first Keepers planted these to carry the Heart's song past the walls. Yours
  will answer the same way."* (Tone-matched to `narrative-bible.md` §7.2.)

### 10e. This becomes its own work order

This section is a **design spec, not an implementation order.** When ratified,
promote it to **WO-113 — Arcane Tower buildable type**, executing roughly:

1. Add `arcane_tower` `BuildableItem` to the WO-108 build palette
   (`BuildPaletteUI`), `BuildZone.Ground`, 2×2 footprint, 120 gold.
2. Resolve the prefab (`Tower_Castle_Square` with `Tower_Medieval_Big` fallback +
   `LogWarning`).
3. Confirm the Arcane Tower's TowerData asset carries the Aether element and the
   ward-bolt projectile (`hitBall2` violet) — already specified in
   `elemental-codex.md` §4.
4. Author the four imbuement options on the Arcane Tower TowerData (Mana Surge +
   the three §9 auras) per §9e, with ratified crystal costs.
5. Wire the build-menu copy hook (§10d) and the "Imbue / Level 4" UI labels (§9d).
6. Verify: build an Arcane Tower (120 gold), upgrade to L3, imbue with Wardlight,
   confirm the ward aura mends a damaged adjacent wall and soaks damage; repeat
   for Consecrate and Rally.

> **Lane note (CLAUDE.md §9):** this is a Combat/AI + UI task and touches
> `BuildPaletteUI` / `TowerCombat` / TowerData — it does **not** touch
> `VillageSceneBuilder.cs` (the serialization bottleneck) and can run in parallel
> with the World/Environment lane.

---

## 11. Ratification Checklist — New Content (§9–§10)

### Imbuements (§9)

- [ ] **Wardlight** name — approve, or pick "Keeper's Mercy" / "The Sheltering"
- [ ] Wardlight stats — 8 m radius, 2 HP/sec mend, 20% damage soak — approve or tune
- [ ] Wardlight affects the **Heart** when in radius — confirm this is intended (it is the keystone-placement payoff)
- [ ] Wardlight non-stacking rule (strongest value, not additive) — approve
- [ ] **Consecrate** name — approve, or pick "Hallowed Ground" / "The Unmaking"
- [ ] Consecrate stats — 7 m radius, +25% damage taken, all sources — approve or tune
- [ ] **Rally** name — approve, or pick "Quickening" / "War-Song"
- [ ] Rally stats — 7 m radius, +30% fire rate, friendly towers only — approve or tune
- [ ] Rally element — keep **Aether**, or reskin **Physical** (war-drum/banner reading)?
- [ ] All three new auras are **Arcane Tower** imbuement options — confirm the Arcane Tower is the single home for the support-aura set
- [ ] Imbue crystal costs for the new auras (suggested 10 each) — approve or tune

### Terminology (§9d)

- [ ] Present empowerment as **"Imbued" tier / "Level 4"** in UI copy only, `MaxLevel` stays 3 — **confirm** (owner-ratified; flagged here for the record)
- [ ] **Option:** make Imbue a true 4th upgrade level (`MaxLevel = 4`, non-additive) — **decline (recommended)** or elect
- [ ] "Imbue" button label + "Imbued — Lv 4" badge copy — approve wording

### Arcane Tower buildable (§10)

- [ ] Arcane Tower joins the buildable roster as the **magic/support tower** — confirm (owner-ratified)
- [ ] **Prefab split:** Arcane Tower uses `Tower_Castle_Square`, distinct from the Ground Tower's `Tower_Medieval_Big` — approve, or accept the two looking identical
- [ ] Footprint 2×2, ground-only placement — approve or adjust
- [ ] Build cost 120 gold — approve or tune
- [ ] Ward-tether lore hook + build-menu copy — approve the fiction tie
- [ ] Promote §10 to **WO-113 — Arcane Tower buildable type** — approve the work-order split
- [ ] Confirm §9 auras (especially Wardlight `Heal`) route only through `IDamageableStructure` / Core interfaces (CLAUDE.md §5 cross-module rule)
