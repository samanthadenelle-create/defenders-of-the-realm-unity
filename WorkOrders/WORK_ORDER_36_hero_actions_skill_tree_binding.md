# WORK ORDER 36 — Hero Ability Actions Mapped to Hero Class + Skill Tree

**Status:** DONE

> **DONE - verified in HEAD 2026-08-14 (phantom sweep).** The work is present at HeroAbilities.cs:277,284-315 + HeroTalentModifiers.cs:62,583-603.
> Status had read READY because the landing commit did not flip this line in the same commit
> (CLAUDE.md §2), so the DERIVED board (BOARD.html) kept re-serving finished work.
> _Prior status line, preserved: Status: READY TO IMPLEMENT_

**Date:** 2026-05-26
**Author:** Owner design spec — playtest feedback
**Priority:** High — currently all three hero classes cast Mage abilities
              regardless of which hero is selected; talent tree nodes have no
              effect on ability stats

---

## Problem

> "Character actions should be mapped to hero type and tied back to skill tree"

Two distinct bugs:

### Bug 1 — Wrong abilities for Knight / Ranger

`HeroAbilities._heroClass` is initialised to `AbilityCatalog.DefaultClass`
(`"mage"`) and **never updated** to match the selected hero. So a Knight
always casts Arcane Bolt / Frost Nova / Healing Beacon / Meteor Strike —
the Mage kit — instead of Shield Bash / Bulwark Slam / Oath Ward / Lantern Charge.

```csharp
// HeroAbilities.cs line ~18
[SerializeField] private string _heroClass = AbilityCatalog.DefaultClass;
// ↑ stays "mage" at runtime; nobody writes it for Knight/Ranger
```

`abilities.json` already has the Knight and Ranger ability sets authored
(per WO-31 note and existing file). The data is there; it is just never read.

### Bug 2 — Talent tree nodes do not affect ability stats

`HeroTalentPanel` displays the talent tree and `WisdomCurrencyService` tracks
which nodes are unlocked, but `HeroAbilities` never reads the talent state.
An unlocked "Power Surge" node that says "+20% damage" has zero in-game effect.

---

## Fix — Part 1: Wire _heroClass at Runtime

### A. `HeroBodySwapper.Start()` — set `_heroClass` after swapping body

After the body swap, use the same reflection pattern already in place to update
`HeroAbilities._heroClass`:

```csharp
// HeroBodySwapper.Start() — after the existing animator re-cache loop:
string heroSlug = SlugFor(cls)?.ToLowerInvariant() ?? "mage";
foreach (var mb in GetComponentsInChildren<MonoBehaviour>(true))
{
    if (mb == null) continue;
    if (mb.GetType().Name != "HeroAbilities") continue;
    var f = mb.GetType().GetField("_heroClass",
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance);
    if (f != null)
    {
        f.SetValue(mb, heroSlug);
        Debug.Log($"[HeroBodySwapper] Set HeroAbilities._heroClass = '{heroSlug}'");
    }
}
```

### B. `HeroAbilities.Awake()` — self-resolve as backstop

Add a self-resolution path in `Awake()` so the class is correct even without
the reflection write (e.g. in test scenes where HeroBodySwapper is absent):

```csharp
private void Awake()
{
    _mana = _maxMana;
    _animator = GetComponentInChildren<Animator>();

    // Resolve hero class from GameState if this is a runtime play session.
    var svc = GameStateService.Instance;
    if (svc != null)
    {
        var opt = svc.State?.HeroClass.ToNullable();
        if (opt.HasValue)
        {
            _heroClass = opt.Value switch
            {
                HeroClass.Knight => "knight",
                HeroClass.Ranger => "ranger",
                HeroClass.Mage   => "mage",
                _                => AbilityCatalog.DefaultClass,
            };
        }
    }
}
```

---

## Fix — Part 2: Talent Tree → Ability Stat Modifiers

### New class: `HeroTalentModifiers.cs`

**File**: `Assets/_Modules/Village/Talents/HeroTalentModifiers.cs`

```csharp
/// <summary>
/// Reads the player's unlocked talent nodes (WisdomCurrencyService) and
/// returns stat multipliers that HeroAbilities applies per-cast.
/// Designed as a pure lookup — no MonoBehaviour, no state.
/// </summary>
public static class HeroTalentModifiers
{
    /// <summary>Damage multiplier from talent nodes for a given class + slot.</summary>
    public static float DamageMultiplier(string heroClass, AbilitySlot slot)
    {
        float mul = 1f;
        var tree = HeroTalentCatalog.GetTree(heroClass);
        if (tree == null) return mul;

        // Read unlocked nodes from WisdomCurrencyService (reflection bridge
        // same as PetSkillTreePanel — WisdomCurrencyService may be in a
        // different asmdef).
        var unlocked = GetUnlockedNodes(heroClass);
        if (unlocked == null) return mul;

        foreach (var node in tree.Nodes)
        {
            if (!unlocked.Contains(node.Id)) continue;
            mul += DamageBonus(node);
        }
        return mul;
    }

    /// <summary>Cooldown reduction multiplier (1.0 = no reduction).</summary>
    public static float CooldownMultiplier(string heroClass, AbilitySlot slot)
    {
        // Similar pattern: sum CdReduction talent bonuses.
        // Week 6 stub: return 1f until talent node data includes cdReduction field.
        return 1f;  // STUB — Week 6
    }

    private static float DamageBonus(HeroTalentNodeDef node)
    {
        // Talent descriptions encode bonuses as "+X% damage" or "+X damage".
        // Parse the description string for now; replace with a typed field in
        // hero-talents.json (Week 7).
        var desc = node.Description ?? "";
        if (desc.Contains("damage", System.StringComparison.OrdinalIgnoreCase))
        {
            // Simple heuristic: tier1=+10%, tier2=+20%, tier3=+30%
            return node.Tier switch
            {
                "tier1" => 0.10f,
                "tier2" => 0.20f,
                "tier3" => 0.30f,
                _       => 0.05f,
            };
        }
        return 0f;
    }

    private static System.Collections.Generic.HashSet<string> GetUnlockedNodes(string heroClass)
    {
        // Reflection bridge to WisdomCurrencyService.GetUnlockedNodes(heroClass)
        // — mirrors the AbilityAudioBridge pattern.
        // STUB — Week 6: return empty set until WisdomCurrencyService is wired.
        return new System.Collections.Generic.HashSet<string>(); // STUB — Week 6
    }
}
```

### Wire modifiers into `HeroAbilities.ResolveEffect()`

```csharp
// HeroAbilities.ResolveEffect() — apply talent damage multiplier:
private void ResolveEffect(AbilityDef def, Vector3 origin)
{
    float dmgMul = HeroTalentModifiers.DamageMultiplier(_heroClass, (AbilitySlot)System.Array.IndexOf(...));
    float damage = def.Damage * dmgMul;   // replace all def.Damage refs with damage
    ...
}
```

### Wire talent cooldown reduction into `TryCast()`

```csharp
// HeroAbilities.TryCast() — apply talent cooldown reduction:
float cdMul = HeroTalentModifiers.CooldownMultiplier(_heroClass, slot);
_cooldownRemaining[(int)slot] = def.Cooldown * cdMul;
```

---

## Ability Slot → HUD Label Fix

The HUD currently shows generic Q/W/E/R icons. Now that each class has its own
ability names, update `HeroAbilitiesHudBridge` to read the ability name + icon
from `AbilityCatalog.Find(_heroClass, slot)` and refresh after
`HeroBodySwapper` completes its swap.

```csharp
// HeroAbilitiesHudBridge — add:
public void RefreshForClass(string heroClass)
{
    for (int i = 0; i < 4; i++)
    {
        var slot = (AbilitySlot)i;
        var def  = AbilityCatalog.Find(heroClass, slot);
        if (def == null) continue;
        SetSlotIcon(slot, def.Icon);
        SetSlotName(slot, def.Name);
        SetSlotColor(slot, def.UnityColor);
    }
}
```

Call `RefreshForClass(heroSlug)` from `HeroBodySwapper.Start()` after
setting `_heroClass`.

---

## hero-talents.json additions (data)

The existing `hero-talents.json` talent nodes have text descriptions but no
typed `damageBonus` / `cdReduction` fields. For Week 7 add:

```json
{
  "id": "knight-t1a",
  "name": "Iron Will",
  "tier": "tier1",
  "column": "a",
  "cost": 1,
  "description": "Shield Bash deals +10% damage.",
  "damageBonus": 0.10,
  "cdReduction": 0.0,
  "prerequisites": []
}
```

This unlocks the typed path in `HeroTalentModifiers.DamageBonus()`.
**Week 7 data task** — no code change required beyond removing the description
parser stub.

---

## Files to Edit / Create

| File | Change |
|---|---|
| `Assets/_Modules/Village/Hero/HeroBodySwapper.cs` | Set `_heroClass` field via reflection; call `HeroAbilitiesHudBridge.RefreshForClass` |
| `Assets/_Modules/Village/Hero/HeroAbilities.cs` | `Awake()` self-resolve hero class from GameState; apply `dmgMul` + `cdMul` from `HeroTalentModifiers` |
| `Assets/_Modules/Village/Hero/HeroAbilitiesHudBridge.cs` | Add `RefreshForClass(string)` to re-skin the HUD icons/names/colors |
| `Assets/_Modules/Village/Talents/HeroTalentModifiers.cs` | **New** — talent → stat multiplier lookup |
| `Assets/StreamingAssets/Data/Canonical/hero-talents.json` | Add `damageBonus` / `cdReduction` fields per node (Week 7 data task) |

---

## Acceptance Criteria

- [ ] Selecting Knight in character select → Knight abilities (Shield Bash / Bulwark Slam / Oath Ward / Lantern Charge) appear in the HUD
- [ ] Selecting Ranger → Ranger abilities (Quick Shot / Snare Trap / Mending Salve / Storm of Arrows) appear in the HUD
- [ ] Pressing 1/2/3/4 fires the correct class ability (Knight casts Shield Bash on 1, not Arcane Bolt)
- [ ] Unlocking a tier-1 talent node increases the ability damage by the documented amount
- [ ] Mage abilities unchanged
- [ ] No scene re-bake required
- [ ] Talent modifier code clearly marked `// STUB — Week 6/7` where not yet wired
