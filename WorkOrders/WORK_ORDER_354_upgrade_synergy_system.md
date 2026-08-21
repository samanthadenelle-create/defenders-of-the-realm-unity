<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-354: Upgrade Tier Display & Synergy Bonuses

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Estimated Effort:** P1–P2 (3–5 days)  
**Priority:** High (core gameplay depth)  
**Lane:** Gameplay/Systems (parallel to HUD work)

---

## Overview

Implement a structure upgrade tier system with synergy bonuses. Each structure has a max level (1–3). Upgrading unlocks new abilities and auras that buff nearby structures. Display tier info on palette cards, preview panel, and selection UI. Calculate & broadcast active bonuses in real-time so players see their synergy payoff.

**Why:** Encourages strategic village design (place resources near defenses for synergy). Clarifies upgrade value ("What does Lv 2 even do?"). Creates a meta-game of optimization.

---

## Acceptance Criteria

- [ ] Each structure in catalog has maxLevel & tier cost data (repo.maxLevel, repo.tierCost[])
- [ ] Palette cards show tier badge (Lv X/Max)
- [ ] Preview panel shows next tier stats + new aura bonuses
- [ ] Selection UI (tap-to-edit) shows "Upgrade" button with cost, disabled at max tier
- [ ] Synergy system: define which structures grant bonuses (Lumbermill, Watchtower, Armory, etc.)
- [ ] Bonuses calculated per placement: scan nearby structures, sum active buffs
- [ ] Real-time feedback: as ghost moves, synergy preview updates
- [ ] Placement feedback shows active synergies ("Valid • +8% DPS (Lumbermill) • +15% Range (Watchtower)")
- [ ] Zero allocations during synergy recalc (cache results, only update on ghost move)
- [ ] Works in WebGL (no serialization quirks)

---

## Files to Modify

### New Files
- `Assets/_Modules/Core/Catalog/SynergyDefinition.cs` — ScriptableObject list of synergy effects
- `Assets/_Modules/Village/BuildMode/SynergyCalculator.cs` — Real-time bonus detection

### Existing Files
- `Assets/_Modules/Core/Catalog/CatalogEntry.cs` — Add fields: int maxLevel, ResourceCost[] tierCosts, SynergyEffect[] auras
- `Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs` — Render tier badge on cards
- `Assets/_Modules/Village/BuildMode/BuildStructureInfoPanel.cs` (WO-352) — Show next tier benefits
- `Assets/_Modules/Village/BuildMode/BuildModeController.cs` — Integrate synergy detection
- `Assets/_Modules/Village/BuildMode/GhostPreview.cs` — Display placement feedback with synergies
- `Assets/_Modules/Village/BuildMode/BuildSelectionUI.cs` (WO-108 P2) — Show "Upgrade" button with cost/tier display

### No Changes Required
- `PlacementGrid` (grid occupancy unchanged)
- `StructureFactory` (creation unchanged; tiers handled by persisted state)

---

## Design Spec

### Synergy Rules

| Structure | Aura | Tier | Range | Effect |
|-----------|------|------|-------|--------|
| **Lumbermill** | +8% DPS | Lv 2+ | Adjacent (2m) | Defenses in adjacent cells get +8% damage |
| **Watchtower** | +15% Range | Lv 1+ | 12m radius | All defenses within 12m get +15% range |
| **Armory** | +10% HP | Lv 1+ | Adjacent | Walls in adjacent cells get +10% HP |
| **Barracks** | +12% Troop Speed | Lv 2+ | 8m radius | Units in garrison get +12% speed |
| **Treasury** | +5% Resource Gen | Lv 1+ | 5m radius | Resource structures nearby generate +5% more |

**Note:** Each synergy is additive. A tower can receive +8% DPS from Lumbermill AND +15% Range from Watchtower simultaneously.

### Catalog Entry Extensions

```csharp
public class CatalogEntry
{
    // ... existing fields ...
    
    // NEW: Upgrade tiers
    public int maxLevel = 1;  // 1 = not upgradeable, 3 = two upgrades available
    public ResourceCost[] tierCosts;  // [0] unused, [1] = cost to reach Lv 2, [2] = cost to reach Lv 3
    
    // NEW: Synergy auras this structure grants
    public SynergyEffect[] auras;  // Empty if no auras, or [ +8% DPS at Lv 2+, ... ]
}

[System.Serializable]
public class SynergyEffect
{
    public string id;  // "lumbermill_dps_buff"
    public string displayName;  // "+8% DPS"
    public string description;  // "Granted by Lumbermill (Lv 2+)"
    public float magnitude;  // 1.08f for 8% buff
    public SynergyType type;  // DPS, Range, HP, Speed, ResourceGen, etc.
    public int minTierRequired;  // Lv 2+ = minTierRequired = 2
    public float rangeMeters;  // 2 for adjacent, 12 for Watchtower
    public bool adjacentOnly;  // true = only orthogonal neighbors, false = radius
}

public enum SynergyType
{
    DPS, Range, HP, AttackSpeed, Speed, ResourceGeneration, Defence, Armor
}
```

### Preview Panel Display (WO-352 Integration)

**Next Tier Box:**
```
Upgrade to Lv 2
New benefits:
  • DPS: 12 → 18 (+50%)
  • Range: 8m → 9m
  • NEW AURA: +5% defense to adjacent structures

Cost: ◆ 45  W 30
[Upgrade Now button]
```

### Palette Card Tier Badge

```
Stone Tower
◆ 75  W 20 — DPS: 12
[Lv 1/3]  ← tier badge
```

### Placement Feedback

When ghost is over a valid location:
```
✓ Valid placement • No overlap • Gate clearance OK
Synergies: +8% DPS (Lumbermill) • +15% Range (Watchtower)
```

---

## Implementation Notes

### SynergyCalculator.cs
```csharp
public sealed class SynergyCalculator
{
    /// <summary>
    /// Calculate active bonuses at a given placement cell.
    /// Returns a list of synergies that apply to a structure placed here.
    /// </summary>
    public static List<SynergyEffect> CalculateBonusesAtCell(
        Vector2Int cell,
        PlacementGrid grid,
        Dictionary<Vector2Int, PlacedStructure> occupancy)
    {
        var bonuses = new List<SynergyEffect>();
        
        // Scan nearby cells for structures with auras
        foreach (var (neighborCell, structure) in occupancy)
        {
            if (!IsWithinRange(cell, neighborCell, /* range based on aura */)) continue;
            
            var entry = CatalogRegistry.Get(structure.catalogEntryId);
            if (entry?.auras == null) continue;
            
            // Check if aura is active at structure's current tier
            int currentTier = GetStructureTier(structure);
            foreach (var aura in entry.auras)
            {
                if (currentTier >= aura.minTierRequired && !bonuses.Any(b => b.id == aura.id))
                {
                    bonuses.Add(aura);
                }
            }
        }
        
        return bonuses;
    }
    
    private static bool IsWithinRange(Vector2Int from, Vector2Int to, SynergyEffect aura)
    {
        if (aura.adjacentOnly)
            return (Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y)) == 1;  // Manhattan distance
        
        float dist = Vector2.Distance(from, to) * 2;  // 2m per cell
        return dist <= aura.rangeMeters;
    }
}
```

### BuildModeController Integration
```csharp
private void UpdateGhostSynergies()
{
    if (_ghost?.transform == null) return;
    
    Vector2Int cell = _grid.WorldToCell(_ghost.transform.position);
    var bonuses = SynergyCalculator.CalculateBonusesAtCell(cell, _grid, _grid.Occupancy);
    
    // Update ghost feedback UI
    _placementFeedbackLabel.text = FormatSynergies(bonuses);
    
    // Update preview panel if shown
    if (_infoPanel != null) _infoPanel.RefreshActiveBonuses(bonuses);
}

private void OnGhostMoved()
{
    UpdateGhostSynergies();  // Called each frame as ghost moves
}

private string FormatSynergies(List<SynergyEffect> bonuses)
{
    if (bonuses.Count == 0) return "Valid placement";
    
    var parts = bonuses.Select(b => $"{b.displayName} ({b.description})");
    return "Valid placement\n" + string.Join(" • ", parts);
}
```

### Selection UI Tier Display (WO-108 P2 Integration)
When player taps a placed structure:
```csharp
public void Show(PlacedStructure structure, int currentTier, int maxTier, 
                 int nextTierCost, bool canAfford)
{
    var entry = CatalogRegistry.Get(structure.catalogEntryId);
    
    _titleLabel.text = currentTier > 1 
        ? $"{entry.displayName}  (Lv {currentTier}/{maxTier})"
        : entry.displayName;
    
    if (currentTier >= maxTier)
    {
        _upgradeBtn.text = "Max Tier";
        _upgradeBtn.SetEnabled(false);
    }
    else
    {
        _upgradeBtn.text = $"Upgrade  ◆ {nextTierCost}";
        _upgradeBtn.SetEnabled(canAfford);
    }
}
```

---

## Data Setup (Editor / Catalog)

Add to each CatalogEntry in the inspector:
```
maxLevel: 3
tierCosts: [0, 45W+30C, 90W+60C]  // Lv 2 cost, Lv 3 cost

auras:
  [0] Lumbermill +8% DPS
      - minTierRequired: 2
      - rangeMeters: 2.0
      - adjacentOnly: true
      - magnitude: 1.08
      - type: DPS
```

Alternatively, load from a ScriptableObject synergy catalog.

---

## Testing Checklist

- [ ] Palette card shows tier badge (Lv X/Max)
- [ ] Preview panel shows next tier stats & auras
- [ ] Ghost synergy detection works as it moves (real-time update)
- [ ] Bonuses appear/disappear when ghost enters/exits range
- [ ] Placement feedback displays synergies on valid cells
- [ ] Upgrade button shows cost, disabled at max tier
- [ ] Tier costs persisted & charged correctly (WO-131)
- [ ] Synergy effects apply after upgrade (live calculation)
- [ ] Multiple synergies stack correctly (Lumber+Watch tower on same tower)
- [ ] Works in WebGL (no serialization issues)
- [ ] Zero allocations in synergy recalc (cache + list reuse)

---

## What NOT to Touch

- Combat/damage calculation (synergies affect UI only; actual stat application is separate)
- PlacementGrid occupancy
- StructureFactory placement logic
- GameStateService persistence (handled by WO-131)

---

## Dependencies

- **Depends on:** WO-108 (BuildModeController, GhostPreview), WO-352 (preview panel), WO-131 (persistence)
- **Unblocks:** WO-356 (placement validation messages)
- **Parallel:** WO-353 (filters), WO-355 (portrait layout)

---

## Acceptance Sign-Off

- [ ] Brace balance check passed
- [ ] All synergy definitions clear in inspector/code
- [ ] Zero allocations during synergy recalc
- [ ] Tier persistence verified (save/load cycle)
- [ ] Works in WebGL build

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `no SynergyDefinition/SynergyCalculator` — synergy + tier preview unbuilt. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict. ⚠ NOTE FOR ANYONE REOPENING: the 2026-08-21 read-only audit had classified this one OPEN - STILL VALID, with the evidence cited above. The owner's review supersedes that call (owner statements are ground truth). The audit line is left in place deliberately, so if this work turns out to be needed, the evidence for it is still here rather than erased.
