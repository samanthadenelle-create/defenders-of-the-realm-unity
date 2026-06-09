# ATB UI Design Specification — Final Fantasy VII Classic Style

**Target:** Exact FF7 battle screen aesthetic for "The Last Stand" WAVE 1

---

## Layout Overview

```
┌─────────────────────────────────────────────────────────────────┐
│  The Last Stand                              Sylas's Turn       │
│  WAVE 1                                                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  [Party]              [Central Arena]         [Enemies]        │
│  Sylas HP ████        Stone dungeon           Skeleton HP ████ │
│  (with portraits)     + torchlight            Skeleton HP ████ │
│  Lian HP ████         + pillars               Skeleton HP ████ │
│  M'3r HP ████                                 Skeleton HP ████ │
│                                               Skeleton HP ████ │
│                                               Skeleton HP ████ │
│                                               Skeleton HP ████ │
│                                               Skeleton HP ████ │
├─────────────────────────────────────────────────────────────────┤
│ ┌──────────────────┐  ┌────────────────────────────────────┐  │
│ │ [Skills]         │  │ Sylas's Turn                       │  │
│ │ ─────────────    │  │ ─────────────────────────────────  │  │
│ │ > Slash          │  │ [Portrait] Sylas                   │  │
│ │   Firebolt       │  │ HP  6060 / 10270                   │  │
│ │   Power Strike   │  │ MP     56 / 1027                   │  │
│ │   Heal           │  │ ATB    45 / 2431  [████████░░░░]  │  │
│ │   Sylas          │  │                                    │  │
│ │   Heal           │  │ [Portrait] Lian                    │  │
│ │                  │  │ HP   [████░░] 45/2431              │  │
│ │ Slash            │  │ MP   [████░░] 17/3459              │  │
│ │ Firebolt         │  │ ATB  [████░░] 45/2431              │  │
│ │ Power Strike     │  │                                    │  │
│ │ Heal             │  │ [Portrait] M'3r                    │  │
│ │ Hybrdy Hero      │  │ HP   [████░░] 56/1027              │  │
│ │ Ley for          │  │ MP   [████░░] 17/3459              │  │
│ │ Hand Hero        │  │ ATB  [████░░] 45/2431              │  │
│ │ Ley for          │  │                                    │  │
│ │ Hybrdy Hero      │  │ (Party 4 shown here)               │  │
│ └──────────────────┘  └────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Canvas Structure

```
Canvas (Battle HUD)
├── Arena (central 60% of screen)
│   ├── PartyGroup (left side)
│   │   ├── Character1 (Sylas + HP bar)
│   │   ├── Character2 (Lian + HP bar)
│   │   ├── Character3 (M'3r + HP bar)
│   │   └── Character4 (4th party + HP bar)
│   │
│   ├── EnemyGroup (right side)
│   │   ├── Enemy1 (Skeleton + HP bar)
│   │   ├── Enemy2 (Skeleton + HP bar)
│   │   ├── Enemy3 (Skeleton + HP bar)
│   │   ├── Enemy4 (Skeleton + HP bar)
│   │   ├── Enemy5 (Skeleton + HP bar)
│   │   ├── Enemy6 (Skeleton + HP bar)
│   │   ├── Enemy7 (Skeleton + HP bar)
│   │   └── Enemy8 (Skeleton + HP bar)
│   │
│   └── BattleInfo (top-center)
│       ├── Title ("The Last Stand")
│       ├── Wave ("WAVE 1")
│       └── ActiveCharacter ("Sylas's Turn")
│
├── CommandPanel (bottom-left 25% of screen)
│   ├── MainMenu
│   │   ├── Skills (selected)
│   │   ├── Guard
│   │   ├── Item
│   │   └── Run
│   │
│   └── SubMenu (Skills open)
│       ├── Slash
│       ├── Firebolt
│       ├── Power Strike
│       ├── Heal
│       ├── Sylas (character name = subskills?)
│       ├── Heal (second)
│       ├── (more skills/descriptions)
│       └── [Cursor highlight on Slash]
│
└── PartyStatusPanel (bottom-right 25% of screen)
    ├── Character1Card
    │   ├── Portrait (small, 64×64)
    │   ├── Name (Sylas)
    │   ├── HP bar (6060 / 10270)
    │   ├── MP bar (56 / 1027)
    │   └── ATB bar (45 / 2431)
    │
    ├── Character2Card
    │   └── (same as above)
    │
    ├── Character3Card
    │   └── (same as above)
    │
    └── Character4Card
        └── (same as above)
```

---

## Visual Style (FF7 Classic)

### Colors
- **Panel backgrounds:** Dark blue-grey (#2c3e50 or similar)
- **Panel borders:** Light grey/silver (#bdc3c7)
- **Text (main):** White (#ffffff)
- **Text (secondary):** Light grey (#ecf0f1)
- **HP bars:** Red (#e74c3c)
- **MP bars:** Blue (#3498db)
- **ATB bars:** Yellow/gold (#f39c12)
- **Selected item:** Gold/yellow highlight
- **Beveled edges:** 2-3 px inset/outset border effect

### Typography
- **Font:** Retro pixelated (FF7 style) — like "Battle" or similar
- **Title:** Large, bold, white
- **Commands:** Medium, white
- **Stats:** Small, light grey
- **Descriptions:** Small, white

### Panels
- **CommandPanel:** Square/rectangular, beveled border, dark blue
- **PartyStatusPanel:** Grid of 4 cards, each with portrait + stats
- **MainMenu (top):** Skills / Guard / Item / Run (4 options)
- **SubMenu (open):** Scrollable list of abilities with descriptions

### Cursor
- **Highlight:** Gold/yellow color on selected item
- **Arrow indicator:** `>` symbol before selected option
- **Animation:** Optional subtle pulse/flicker

---

## Specific Elements

### CommandPanel (Bottom-Left)

**Main Menu:**
```
┌────────────────────┐
│ [Skills]           │
│ Guard              │
│ Item               │
│ Run                │
└────────────────────┘
```

**Sub-Menu (Skills open):**
```
┌────────────────────┐
│ > Slash            │
│   Firebolt         │
│   Power Strike     │
│   Heal             │
│   Sylas            │
│   Heal (2nd)       │
│                    │
│ Slash              │
│ Firebolt           │
│ Power Strike       │
│ Heal               │
│ Hybrdy Hero        │
│ Ley for            │
│ Hand Hero          │
│ Ley for            │
│ Hybrdy Hero        │
└────────────────────┘
```

**Cursor:** `>` before "Slash" (highlighted in gold)

### PartyStatusPanel (Bottom-Right)

**Single Character Card:**
```
┌──────────────────────────┐
│ [Portrait] Sylas         │
│ HP   6060 / 10270        │
│      [████████░░░░░░░░]  │
│ MP      56 / 1027        │
│      [██░░░░░░░░░░░░░░]  │
│ ATB     45 / 2431        │
│      [████░░░░░░░░░░░░]  │
└──────────────────────────┘
```

**4 Cards Stacked:**
- All 4 characters shown
- Same format repeated
- Allows at-a-glance party health check

### Arena (Central 60%)

**Background:**
- Dark stone dungeon
- Pillars/architecture
- Torchlight (atmospheric)
- Moody color palette (greys, browns, shadows)

**Party (Left side):**
- Sylas (white-haired female)
- Lian (character 2)
- M'3r (character 3)
- 4th party member
- Each with name label above
- Each with HP bar above (gold text, red bar)
- Standing/facing right (toward enemies)

**Enemies (Right side):**
- Skeleton x8 (or variable count)
- Golden/bone color
- Arranged in formation
- Each with name label above
- Each with HP bar above (gold text, red bar)
- Facing left (toward party)

**Title & Info (Top-Center):**
```
The Last Stand
WAVE 1

(Top-right or near active character)
Sylas's Turn
```

---

## Mobile Landscape Specifications

**Aspect Ratio:** 16:9 (landscape)
**Resolution:** 1280×720 (or scale to fit)

**Layout proportions:**
- **Arena:** 60% (center, 1280×432)
- **Bottom panels:** 40% (1280×288 split)
  - Command panel: Left 320px
  - Party status: Right 640px
  - Margins: 20px

**Touchable areas:**
- CommandPanel buttons: 80px × 40px (large for touch)
- PartyStatusPanel: Display only (no touches)
- Arena: Display only (no touches)

---

## Interactivity

### CommandPanel
- Highlight selected item in gold/yellow
- Show sub-menu when "Skills" selected
- Cursor moves with arrow keys or touch
- Select with Enter/Space or tap
- Shows ability descriptions in sub-menu

### PartyStatusPanel
- Read-only (display party status)
- Updates HP/MP/ATB in real-time
- Shows only numbers and bars (no buttons)

### Arena
- Shows character/enemy positions
- HP bars update on damage
- Text indicators (Sylas's Turn, etc.)
- No interaction

---

## Animation & Effects

### Smooth Updates
- HP bars animate smoothly (not instant)
- ATB bars fill/deplete smoothly
- Text updates appear immediately
- No lag or jitter

### Optional Effects
- Slight glow on selected item
- Cursor pulse/flicker
- Panel fade-in on battle start
- Status number highlight (damage numbers)

---

## Comparison to Current Build

**Current issues (from screenshots):**
- ❌ Command panel not FF7-styled
- ❌ Party status panel layout unclear
- ❌ Missing sub-menu descriptions
- ❌ Text hierarchy not clear
- ❌ Colors not consistent

**This spec provides:**
- ✅ Exact FF7 layout
- ✅ Clear panel hierarchy
- ✅ Color palette (blue-grey-gold)
- ✅ Typography (retro pixelated)
- ✅ Mobile landscape layout
- ✅ Interactivity rules

---

## Implementation Checklist

- [ ] Canvas structure matches layout above
- [ ] CommandPanel styled (dark blue, beveled)
- [ ] PartyStatusPanel styled (4 character cards)
- [ ] Arena background (stone dungeon, moody)
- [ ] Party positioned left, enemies right
- [ ] HP/MP/ATB bars update smoothly
- [ ] Cursor highlight in gold
- [ ] Text is readable (white on dark)
- [ ] Mobile landscape (1280×720) fits well
- [ ] No clipping or overflow
- [ ] FF7 aesthetic achieved

---

## Notes

This is the EXACT visual design from the reference image. Dev team should use this spec to build the ATB UI to match FF7 classic style perfectly.
