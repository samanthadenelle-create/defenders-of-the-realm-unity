<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-378: Town HUD Modernization — Replace Old UI with Clean Design

**Status:** READY TO IMPLEMENT  
**Estimated Effort:** P1 (1–1.5 days — UI redesign + integration)  
**Priority:** HIGH (visual quality, consistency)  
**Lane:** 4 UI/HUD

---

## Overview

**Current state:**
- Old, dated HUD design in town/build mode
- Clunky layout, too much visual noise
- Doesn't match the approved battle HUD style
- Quest log and objectives cluttering the screen

**Target state:**
- Clean, minimal HUD (matches battle HUD aesthetic from WO-334)
- Only essential info visible
- Modern styling (dark theme, gold accents)
- Quests/objectives non-intrusive (collapsible or tab-based)

---

## Current Issues (Screenshot Analysis)

**What's wrong:**
1. **Top-left:** Old green "SAFE" bar with outdated health/resource display
2. **Top-center:** Clunky "OBJECTIVE" box with quest text
3. **Right side:** Full vertical quest list taking up screen space
4. **Bottom:** Console error still visible
5. **Overall:** Inconsistent with battle HUD approved in WO-334

**Missing from current design:**
- Clean resource counter (gold, wood, food icons)
- Wave timer / next wave countdown
- Build mode toggle visibility
- Quest tracking (collapsible, not always visible)

---

## Target Design (Approved Battle HUD + Town Adaptation)

### WO-334 Approved Battle HUD Reference

**From WO-334 (approved):**
- Dark background panels (semi-transparent black)
- Gold/amber text and borders
- Clean icon-based resource display
- Compact layout
- No clutter

### Town HUD Adaptation

**Keep from battle HUD:**
- Color scheme (dark + gold)
- Typography (sans-serif, clean)
- Spacing and padding rules
- Icon style

**Add for town mode:**
- Current resources (wood, gold, food) in top-left
- Wave timer (when applicable)
- Quests (collapsible accordion)
- Build mode status (visible when building)

---

## New Town HUD Layout

### Top-Left: Resource Counter
```
┌─────────────────────┐
│ 🪵 Wood: 150        │  ← Icon + number
│ ⭐ Gold: 320        │
│ 🍎 Food: 80         │
│ 💎 Ore: 45          │
└─────────────────────┘

Style: Dark panel, gold text, icons on left
```

### Top-Right: Wave Status (During Build Phase)
```
┌──────────────────────┐
│ Next Wave: 1         │  ← Countdown timer
│ 00:45 until attack   │
│ [Ready] button       │
└──────────────────────┘
```

### Right-Side: Quest Log (Collapsible)
```
┌─────────────────────┐
│ ▼ Quests (2)        │  ← Collapse/expand
├─────────────────────┤
│ • Build tower       │  ← Quest list
│ • Survive wave 1    │
└─────────────────────┘

Or when collapsed:
┌─────────────┐
│ ▶ Quests(2) │  ← Arrow shows it's collapsed
└─────────────┘
```

### Center-Bottom: Build Mode (When Active)
```
┌───────────────────────────────────┐
│ BUILD MODE: Watchtower            │  ← Current building
│ Cost: 🪵 100 | ⭐ 50              │  ← Cost display
│ [Rotate] [Place] [Cancel]         │  ← Action buttons
└───────────────────────────────────┘
```

---

## Color Palette (Consistent with WO-334)

| Element | Color | Hex | Notes |
|---|---|---|---|
| Panel Background | Dark gray/black | #1a1a1a | Semi-transparent (80% opacity) |
| Text (Primary) | White | #FFFFFF | Primary info |
| Text (Secondary) | Light gray | #CCCCCC | Secondary info |
| Accents | Gold | #D4AF37 | Borders, highlights |
| Success | Green | #4CAF50 | Build ready |
| Warning | Orange | #FF9800 | Not enough resources |
| Error | Red | #F44336 | Can't build here |

---

## UI Components to Build/Modify

### 1. ResourcePanel (Top-Left)
```csharp
public class ResourcePanel : MonoBehaviour
{
    [SerializeField] private Text _woodText;
    [SerializeField] private Text _goldText;
    [SerializeField] private Text _foodText;
    [SerializeField] private Text _oreText;
    
    void Update()
    {
        // Update resource display
        _woodText.text = CoreServices.Resources.Wood.ToString();
        _goldText.text = CoreServices.Resources.Gold.ToString();
        // ... etc
    }
}
```

### 2. QuestLog (Right-Side, Collapsible)
```csharp
public class QuestLogPanel : MonoBehaviour
{
    [SerializeField] private Button _toggleButton;
    [SerializeField] private RectTransform _contentPanel;
    
    void Start()
    {
        _toggleButton.onClick.AddListener(ToggleExpand);
    }
    
    void ToggleExpand()
    {
        _contentPanel.gameObject.SetActive(!_contentPanel.gameObject.activeSelf);
        // Update arrow icon (▼ or ▶)
    }
}
```

### 3. WaveTimer (Top-Right, When Applicable)
```csharp
public class WaveTimerPanel : MonoBehaviour
{
    [SerializeField] private Text _countdownText;
    private float _timeUntilWave;
    
    void Update()
    {
        _timeUntilWave -= Time.deltaTime;
        _countdownText.text = Mathf.Max(0, _timeUntilWave).ToString("F1") + "s";
    }
}
```

### 4. BuildModeHUD (Center-Bottom, When Building)
```csharp
public class BuildModeHUD : MonoBehaviour
{
    [SerializeField] private Text _buildingNameText;
    [SerializeField] private Text _costText;
    [SerializeField] private Button _rotateButton;
    [SerializeField] private Button _placeButton;
    [SerializeField] private Button _cancelButton;
    
    void OnBuildModeStart(Structure structure)
    {
        gameObject.SetActive(true);
        _buildingNameText.text = structure.Name;
        _costText.text = $"🪵 {structure.WoodCost} | ⭐ {structure.GoldCost}";
    }
    
    void OnBuildModeEnd()
    {
        gameObject.SetActive(false);
    }
}
```

---

## Canvas Hierarchy (New Design)

```
Canvas (Town HUD)
├── Render Mode: ScreenSpace-Camera
├── Sort Order: 50 (above game, below dialogue)
│
├── ResourcePanel (Top-Left)
│   ├── WoodDisplay
│   ├── GoldDisplay
│   ├── FoodDisplay
│   └── OreDisplay
│
├── WaveTimerPanel (Top-Right)
│   ├── CountdownText
│   └── ReadyButton
│
├── QuestLogPanel (Right-Side)
│   ├── ToggleButton (▼/▶)
│   └── QuestList (collapsible)
│       ├── QuestItem (prefab)
│       └── ...
│
└── BuildModeHUD (Center-Bottom, hidden by default)
    ├── BuildingNameText
    ├── CostDisplay
    ├── RotateButton
    ├── PlaceButton
    └── CancelButton
```

---

## Styling Details

### Panels
- **Border:** 1 px gold (#D4AF37)
- **Background:** Dark with gradient (#1a1a1a to #0a0a0a)
- **Shadow:** Subtle drop shadow (2 px, #000000, opacity 0.5)
- **Padding:** 12 px (consistency)
- **Border-radius:** 4 px (subtle rounding)

### Text
- **Font:** Arial or Segoe UI (clean, sans-serif)
- **Size:** 14–16 px (readable)
- **Weight:** Normal (400) or Bold (700) for headers
- **Shadow:** Subtle text shadow (1 px, #000000, opacity 0.3)

### Icons
- **Size:** 16×16 px (consistent)
- **Style:** Flat, simple shapes
- **Color:** White (#FFFFFF) or gold (#D4AF37)

### Buttons
- **Style:** Flat (no 3D bevels)
- **Padding:** 8 px × 12 px
- **Hover:** Brighten to #FFD700
- **Transition:** 0.2 seconds smooth

---

## Interaction Patterns

### Quest Toggle
```
User clicks "▼ Quests(2)"
    ↓
Quest list collapses, arrow becomes "▶"
    ↓
Screen space freed up
    ↓
User clicks "▶ Quests" again
    ↓
List expands, arrow becomes "▼"
```

### Build Mode Activation
```
User clicks building in palette
    ↓
BuildModeHUD appears (center-bottom)
    ↓
Shows building name + cost
    ↓
User places building or clicks [Cancel]
    ↓
BuildModeHUD disappears
```

### Wave Timer
```
Build phase active
    ↓
WaveTimerPanel shows "Next Wave: 45s"
    ↓
Timer counts down
    ↓
When <10s: Warning color (orange)
    ↓
When 0s: "Wave Starting!" (red)
```

---

## Files to Create/Modify

### New Files
- `Assets/UI/Town/ResourcePanel.cs`
- `Assets/UI/Town/QuestLogPanel.cs`
- `Assets/UI/Town/WaveTimerPanel.cs`
- `Assets/UI/Town/BuildModeHUD.cs`
- `Assets/UI/Town/TownHUDController.cs` (main orchestrator)

### Modify Existing
- `Assets/Scenes/Village.unity` — Replace old HUD canvas
- `Assets/_Modules/Village/VillageController.cs` — Integrate new HUD

### Assets Needed
- Resource icons (wood, gold, food, ore) — 16×16 px
- Quest marker icons
- Build mode cursor graphic

---

## Migration Path

1. **Create new Town HUD prefab** (alongside old one)
2. **Test in separate scene** (verify layout, styling)
3. **Swap prefab in Village scene** (old → new)
4. **Test integration** (resources update, quests display)
5. **Polish and iterate** (color tweaks, spacing)
6. **Deprecate old HUD** (archive or delete)

---

## Testing Checklist

- [ ] Resource display updates correctly (gold, wood, food, ore)
- [ ] Quest log expands/collapses smoothly
- [ ] Wave timer counts down properly
- [ ] Build mode HUD appears when building
- [ ] All text is readable (good contrast)
- [ ] Buttons are clickable and respond
- [ ] Styling matches WO-334 approved battle HUD
- [ ] No clipping or layout issues
- [ ] Works on different resolutions (responsive)
- [ ] Performance is good (no FPS drop)

---

## Acceptance Criteria

- [ ] Old town HUD completely replaced
- [ ] New HUD uses approved color scheme (dark + gold)
- [ ] Resources, quests, wave timer all displayed
- [ ] Clean, minimal layout (no clutter)
- [ ] Consistent with battle HUD style
- [ ] All interactive elements work correctly
- [ ] No visual glitches or alignment issues
- [ ] Performance maintained

---

## Related Work Orders

- WO-334: Battle HUD approval (design reference)
- WO-355: Portrait Responsiveness (ensure HUD works on mobile)
- WO-357: Mobile Touch (HUD must be touch-friendly)

---

## Priority

**HIGH.** Visual quality and consistency matter. Old HUD looks dated and unprofessional. New design matches approved battle HUD and elevates entire game presentation.

---

## Notes

- Base design on approved WO-334 battle HUD
- Keep layout simple and minimal
- Collapsible quest log keeps screen clean
- Resource display always visible (critical for building)
- Wave timer appears/disappears contextually
- All text must be readable (contrast check required)
