# WO-542 — Shop Icon Rendering: Transparent-Background Images Fitted to Shop Window

**Status:** READY TO IMPLEMENT  
**Lane:** 4 — Store / Inventory / Gear  
**Size:** S–M  
**Mint date:** 2026-06-27  

---

## Problem

The shop panel currently renders item icons as emoji fallbacks. The `iconPath` field
already exists on `tripo_*` weapons (e.g. `"iconPath": "ItemIcons/tripo_sword_a"`), but
`ShopPanel.cs` does not yet read or display them. Additionally, the `ItemIcons/` PNGs
were captured against a white/opaque background and may not fit cleanly inside the shop
window slot without alpha-channel support.

**Current state:**
- `weapons.json` / `armor.json` carry `"iconPath"` on all tripo_* entries
- Canonical entries (mage_starter → aegis set) have no `iconPath` yet — emoji only
- `ShopPanel.cs` ignores `iconPath` entirely; uses emoji from `"icon"` field

---

## Acceptance Criteria

1. **`ShopPanel.cs` reads `iconPath`** — if a `GearDef` has a non-empty `iconPath`,
   load the sprite via `Resources.Load<Sprite>()` and display it in the item slot's
   `Image` component instead of the emoji text. Fall back to emoji if sprite missing.

2. **Alpha/transparency** — sprites are displayed with `Image.preserveAspect = true`
   and `Image.color = Color.white` (not tinted). Shop slot background shows through
   around the weapon silhouette. If the existing PNGs in `ItemIcons/` are opaque-BG,
   add a note in the RESULT.md flagging which ones need a re-export with alpha channel
   (do NOT silently ignore the missing transparency).

3. **Slot sizing** — icon fills the shop slot without overflow. Target: icon takes 70–80%
   of the slot's rect, centered. Use `ContentSizeFitter` or `RectTransform.sizeDelta`
   as appropriate to the existing slot prefab structure.

4. **No UXML.** All changes are code-built UI only (WebGL does not render UXML).

5. **No blink_armor_* references** — those entries were stripped from `armor.json`
   (2026-06-27). Do not re-add or reference `blink_armor_*` / `blink_*` iconPaths.

---

## Files to Touch

| File | Change |
|---|---|
| `Assets/_Modules/HUD/Shop/ShopPanel.cs` | Read `iconPath`, load sprite, display |
| `Assets/Resources/ItemIcons/` | Verify existing PNGs; flag any needing alpha re-export |
| `Assets/Resources/Data/Canonical/weapons.json` | DO NOT edit — just read |
| `Assets/Resources/Data/Canonical/armor.json` | DO NOT edit — just read |

---

## Do NOT Touch

- `weapons.json` / `armor.json` — data was cleaned this session (2026-06-27); do not re-add blink entries
- `VillageHudController.cs` — wrong lane
- Any `.unity` scene files
- `VillageSceneBuilder.cs`

---

## Notes

- The cleaned catalogs now have **34 weapons** (18 canonical + 16 tripo) and **5 armors**
- Tripo weapons carry `iconPath`; canonical weapons currently do not — the icon slot
  should gracefully fall back to emoji for those
- Future pass: capture transparent-background icon renders for canonical weapons and
  add `iconPath` to them — spec as a separate WO

---

## Verification

- Headless: load `ShopPanel` with a `tripo_sword_a` entry and assert the `Image` component
  has a non-null sprite (DataRegression or AutoPilot probe)
- Felt: shop window shows weapon silhouette with transparent background in the slot,
  no emoji fallback for tripo items
