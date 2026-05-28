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
