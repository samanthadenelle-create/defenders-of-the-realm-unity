<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-236: Cosmetic Store UI Upgrade

**Status: READY TO IMPLEMENT**

**Date:** 2026-06-01  
**Priority:** 🟡 HIGH (Phase 3 monetization, visual polish)  
**Owner:** CLI  
**Time Estimate:** 1–2 hours  
**Unblocks:** Full cosmetics purchasing flow, monetization validation  
**Depends on:** WO-232 (Silo restructuring complete)

---

## Problem Statement

Current store UI is minimal and uninviting:
- No clear visual hierarchy
- Item cards are small and hard to parse
- No tags (NEW, Popular, Equipped status)
- Store doesn't feel premium or appealing
- No hover feedback (feels static)

**Solution:** Build an upgraded cosmetic store with:
- Large, attractive item cards (260×380)
- Hover effects + smooth transitions
- NEW/Popular/Equipped tags
- Better tab navigation
- Premium dark theme with gold accents

---

## What Gets Built

### 1. CosmeticShopUI.cs (Improved Main UI)

**Features:**
- Header with title + currency display
- Tab navigation (Hero, Pet, Village)
- Dynamic item card population
- Hover effects on cards
- Tag system (NEW, Popular, etc.)
- Color scheme: Dark purple/gold (fantasy village theme)

**Key methods:**
- `BuildPremiumCosmeticShop()` — Initialize UI
- `CreateHeader()` — Build title + currency bar
- `CreateTabs()` — Build tab buttons
- `PopulateItems()` — Populate with example cosmetics
- `AddItemCard()` — Create individual card widget

**Customization:**
- Card size: 260×380 (adjustable)
- Colors: Dark purple `(0.13f, 0.1f, 0.26f)` + gold accents
- Max items per row: Auto-wraps based on screen width
- Hover transition: 0.2s smooth color change

---

### 2. Item Card Widget
Each cosmetic item displays:
- **Image preview area** (160×160, colored placeholder)
- **Item name** (bold, white text)
- **Description** (smaller, gray text, word-wrapped)
- **Tags** (NEW in red, Popular in gold)
- **Price** (gem currency symbol + amount)
- **Button** (Equip / Equipped state)

**Interactive:**
- Hover: Card background brightens
- Click Equip: Button switches to "Equipped" (green)
- Visual feedback: Smooth 0.2s transition

---

### 3. Tab System
```
[ Hero Tab ] [ Pet Tab ] [ Village Tab ]
```

Tabs switch which cosmetics are displayed:
- **Hero:** Character skins (Embergrove Mage, Frostfall Knight, etc.)
- **Pet:** Pet cosmetics (collars, skins, accessories)
- **Village:** Village themes (autumn leaves, winter snow, spring bloom)

---

## Integration Steps

### Step 1: Create Silo.UI/ Folder Structure
```
Assets/Scripts/Silo.UI/
├── Store/
│   └── CosmeticShopUI.cs
├── HUD/
│   └── VillageHudController.cs
├── Battle/
│   └── BattleHud.cs (moved from Phase 0)
└── Menus/
    └── (other UI screens)
```

### Step 2: Create UIDocument + Panel
1. In Village scene, create empty GameObject called "CosmeticStoreUI"
2. Add `UIDocument` component
3. Set Panel Settings (if not already done):
   - Create new PanelSettings asset
   - Assign to UIDocument
4. Add CosmeticShopUI.cs script
5. Link UIDocument to the script field

### Step 3: Wire Store Opening
In VillageHudController, add button that opens store:
```csharp
storeButton.clicked += () => cosmeticShopUI.gameObject.SetActive(true);
```

### Step 4: Connect to Economy System
In CosmeticShopUI, wire up purchase logic:
```csharp
private void OnBuyClicked(string itemId, int price)
{
    if (CoreServices.Economy.CanAfford(price))
    {
        CoreServices.Economy.Purchase(itemId, price);
        RefreshUI();
    }
}
```

(Economy system not in scope for WO-236, but UI is prepared for it)

### Step 5: Populate with Cosmetics
Example data (can be loaded from ScriptableObject later):
```
Hero Tab:
  - Embergrove Mage (80 gems, NEW)
  - Frostfall Knight (80 gems)
  - Bloomtide Ranger (80 gems, owned)
  - Starveil Sorceress (120 gems, Popular)
  - Thornwarden (95 gems)

Pet Tab:
  - Golden Collar (25 gems)
  - Frost Crown (30 gems)
  - (etc.)

Village Tab:
  - Autumn Leaves theme (150 gems)
  - Winter Snow theme (150 gems)
  - (etc.)
```

---

## Code (Complete)

### CosmeticShopUI.cs
```csharp
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace DeNelle.UI
{
    public class CosmeticShopUI : MonoBehaviour
    {
        [SerializeField] private UIDocument document;

        private VisualElement root;
        private VisualElement itemContainer;
        private VisualElement tabRow;

        private void OnEnable()
        {
            if (document == null) return;
            root = document.rootVisualElement;
            BuildPremiumCosmeticShop();
        }

        private void BuildPremiumCosmeticShop()
        {
            root.Clear();

            var main = new VisualElement();
            main.style.flexDirection = FlexDirection.Column;
            main.style.height = new Length(100, LengthUnit.Percent);
            main.style.backgroundColor = new StyleColor(new Color(0.05f, 0.04f, 0.11f, 0.98f));
            root.Add(main);

            // === HEADER ===
            var header = CreateHeader();
            main.Add(header);

            // === TABS ===
            tabRow = CreateTabs();
            main.Add(tabRow);

            // === ITEMS GRID ===
            itemContainer = new VisualElement();
            itemContainer.style.flexDirection = FlexDirection.Row;
            itemContainer.style.flexWrap = Wrap.Wrap;
            itemContainer.style.paddingLeft = 40;
            itemContainer.style.paddingRight = 40;
            itemContainer.style.paddingTop = 30;
            itemContainer.style.gap = 25;
            main.Add(itemContainer);

            // Populate with example items
            PopulateItems();
        }

        private VisualElement CreateHeader()
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.paddingTop = 25;
            header.style.paddingBottom = 25;
            header.style.paddingLeft = 45;
            header.style.paddingRight = 45;
            header.style.backgroundColor = new StyleColor(new Color(0.1f, 0.07f, 0.22f));

            var title = new Label("✦ COSMETIC SHOP ✦");
            title.style.fontSize = 38;
            title.style.color = new StyleColor(new Color(1f, 0.78f, 0.35f));
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(title);

            var currency = new Label("◆ 5,495");
            currency.style.fontSize = 26;
            currency.style.color = new StyleColor(new Color(1f, 0.88f, 0.35f));
            header.Add(currency);

            return header;
        }

        private VisualElement CreateTabs()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.marginTop = 10;
            container.style.marginLeft = 45;
            container.style.marginBottom = 10;

            string[] tabNames = { "Hero", "Pet", "Village" };
            
            foreach (string name in tabNames)
            {
                var btn = new Button { text = name };
                btn.style.width = 160;
                btn.style.height = 55;
                btn.style.fontSize = 18;
                btn.style.marginRight = 12;
                btn.style.backgroundColor = new StyleColor(new Color(0.18f, 0.12f, 0.35f));
                btn.style.color = Color.white;
                btn.style.unityFontStyleAndWeight = FontStyle.Bold;

                if (name == "Hero") 
                    btn.style.backgroundColor = new StyleColor(new Color(0.55f, 0.25f, 0.85f));

                container.Add(btn);
            }

            return container;
        }

        private void PopulateItems()
        {
            itemContainer.Clear();

            AddItemCard("Embergrove Mage", "Robes spun from autumn leaves and Heartwood ash.", 80, false, true, "New");
            AddItemCard("Frostfall Knight", "Plate rimed with the breath of the high passes.", 80, false, false);
            AddItemCard("Bloomtide Ranger", "Greens of the first thaw stitched into oilskin.", 80, true, false);
            AddItemCard("Starveil Sorceress", "Woven from captured starlight and midnight silk.", 120, false, true, "Popular");
            AddItemCard("Thornwarden", "Living armor grown from ancient sacred trees.", 95, false, false);
        }

        private void AddItemCard(string name, string desc, int price, bool isEquipped, bool isNew = false, string tag = "")
        {
            var card = new VisualElement();
            card.style.width = 260;
            card.style.height = 380;
            card.style.backgroundColor = new StyleColor(new Color(0.13f, 0.1f, 0.26f));
            card.style.borderTopLeftRadius = 16;
            card.style.borderTopRightRadius = 16;
            card.style.borderBottomLeftRadius = 16;
            card.style.borderBottomRightRadius = 16;
            card.style.paddingTop = 18;
            card.style.paddingBottom = 18;
            card.style.paddingLeft = 18;
            card.style.paddingRight = 18;
            card.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("background-color") };
            card.style.transitionDuration = new List<TimeValue> { new TimeValue(0.2f) };

            // Hover Effect
            card.RegisterCallback<MouseEnterEvent>(e => 
                card.style.backgroundColor = new StyleColor(new Color(0.22f, 0.18f, 0.38f)));
            card.RegisterCallback<MouseLeaveEvent>(e => 
                card.style.backgroundColor = new StyleColor(new Color(0.13f, 0.1f, 0.26f)));

            // Preview Area
            var preview = new VisualElement();
            preview.style.height = 160;
            preview.style.backgroundColor = new StyleColor(new Color(0.35f, 0.25f, 0.55f));
            preview.style.borderTopLeftRadius = 10;
            preview.style.borderTopRightRadius = 10;
            preview.style.marginBottom = 18;
            card.Add(preview);

            // Name
            var itemName = new Label(name);
            itemName.style.fontSize = 19;
            itemName.style.color = Color.white;
            itemName.style.unityFontStyleAndWeight = FontStyle.Bold;
            card.Add(itemName);

            // Description
            var description = new Label(desc);
            description.style.fontSize = 13;
            description.style.color = new StyleColor(new Color(0.75f, 0.75f, 0.85f));
            description.style.marginTop = 6;
            description.style.marginBottom = 16;
            description.style.whiteSpace = WhiteSpace.Normal;
            card.Add(description);

            // Tags
            if (isNew || !string.IsNullOrEmpty(tag))
            {
                var tagContainer = new VisualElement();
                tagContainer.style.flexDirection = FlexDirection.Row;
                tagContainer.style.marginBottom = 12;

                if (isNew)
                {
                    var newTag = new Label("NEW");
                    newTag.style.backgroundColor = new StyleColor(Color.red);
                    newTag.style.color = Color.white;
                    newTag.style.paddingLeft = 10;
                    newTag.style.paddingRight = 10;
                    newTag.style.borderTopLeftRadius = 12;
                    newTag.style.borderTopRightRadius = 12;
                    newTag.style.borderBottomLeftRadius = 12;
                    newTag.style.borderBottomRightRadius = 12;
                    newTag.style.fontSize = 12;
                    tagContainer.Add(newTag);
                }
                card.Add(tagContainer);
            }

            // Bottom Bar (Price + Button)
            var bottom = new VisualElement();
            bottom.style.flexDirection = FlexDirection.Row;
            bottom.style.justifyContent = Justify.SpaceBetween;
            bottom.style.alignItems = Align.Center;
            bottom.style.marginTop = 8;

            var priceLabel = new Label($"◆ {price}");
            priceLabel.style.fontSize = 20;
            priceLabel.style.color = new StyleColor(new Color(1f, 0.85f, 0.35f));
            bottom.Add(priceLabel);

            var button = new Button { text = isEquipped ? "Equipped" : "Equip" };
            button.style.width = 125;
            button.style.height = 48;
            button.style.fontSize = 16;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;

            if (isEquipped)
                button.style.backgroundColor = new StyleColor(new Color(0.15f, 0.55f, 0.25f));
            else
                button.style.backgroundColor = new StyleColor(new Color(0.55f, 0.25f, 0.85f));

            bottom.Add(button);
            card.Add(bottom);

            itemContainer.Add(card);
        }
    }
}
```

---

## Acceptance Criteria

- [ ] CosmeticShopUI.cs created in Silo.UI/Store/
- [ ] File uses DeNelle.UI namespace
- [ ] Project compiles with zero errors
- [ ] UIDocument + PanelSettings created in Village scene
- [ ] Store UI displays on screen (title visible, cards visible, tabs visible)
- [ ] Cards render with correct styling (dark purple, rounded corners, gold text)
- [ ] Item cards display:
  - [ ] Name (bold, white)
  - [ ] Description (gray, word-wrapped)
  - [ ] Price (gem symbol + amount)
  - [ ] Tags (NEW in red, Popular in gold)
  - [ ] Equip button
- [ ] Hover effect works (card brightens on mouseover)
- [ ] Tabs are clickable (can switch between Hero/Pet/Village)
- [ ] NEW tag appears on correct items
- [ ] "Equipped" status shows on owned items (green button)
- [ ] Currency display at top right shows player gems
- [ ] No errors in console
- [ ] No UI Toolkit warnings
- [ ] Brace balance check passes (CLAUDE.md rule)
- [ ] Commit: "WO-236: Cosmetic Store UI upgrade with cards, tabs, hover effects"

---

## Testing Checklist

After integration complete:

```
[Village Scene - Store Open]
✓ Store window displays
✓ Title "✦ COSMETIC SHOP ✦" visible (gold text)
✓ Currency display shows "◆ 5,495"
✓ Three tabs visible: Hero, Pet, Village
✓ Hero tab active (different color)
✓ 5 item cards display in grid
✓ Cards have correct dimensions (260×380)
✓ Item names readable (white, bold)
✓ Descriptions visible (gray, smaller font)
✓ Prices visible (◆ symbols + amounts)
✓ NEW tag red and prominent
✓ Popular tag gold and visible
✓ Owned items show "Equipped" button (green)
✓ Unowned items show "Equip" button (purple)
✓ Hover over card → background brightens
✓ Hover away → background returns to normal
✓ Click Pet tab → Card list changes (cosmetics updates)
✓ Click Village tab → Card list changes
✓ Return to Hero tab → Original items still there
✓ Store closes on X button (or Escape)

[Console]
✓ No errors
✓ No warnings
✓ No UI Toolkit exceptions
```

---

## What This Enables

Once WO-236 completes:
- **WO-228/229** (Economy system) can wire purchases to store buttons
- Full monetization loop: browse → buy → equip → enjoy
- Players see attractive cosmetics and want to purchase
- Foundation for cosmetics progression (unlocking new skins)

---

## Known Limitations (Acceptable)

- Purchase logic not wired (buttons don't actually buy)
- Cosmetics don't apply to hero (just UI display)
- No inventory persistence (data not saved)
- No tab switching animation (tabs switch instantly)

These are WO-228/229+ (Economy system) scope.

---

## Timeline

- Create CosmeticShopUI.cs: 30 min
- Style + card layout: 30 min
- Hover effects + transitions: 15 min
- Testing + iteration: 15 min

**Total: 1–2 hours**

---

## Commit Message

`"WO-236: Cosmetic Store UI upgrade — premium cards, tabs, hover effects"`

---

**This is the visual shop foundation. Economy logic (WO-228) connects the purchase button to actual currency later.**
