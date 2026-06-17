# Blink Obsidian UI — re-skin system (2026-06-17)

The whole game UI re-skins to Blink's **Obsidian** theme with **zero UI-code rewrite**, by feeding the
existing sprite-first kit (`RpgUiCatalog` → `ElarionUiKit`) the Obsidian sprites under its canonical names.

## Why this works (architecture — settled)
- Every screen (store, equip, inventory, HUD, dialogue, raid, troop) builds from `ElarionUiKit`, which
  pulls named sprites from `Resources/RpgUi/<role>/<canonical>` **sprite-first, with a procedural fallback**.
- **Keep the code-built UI. Do NOT move to Blink's UXML/UI-Toolkit templates** — UXML does not survive
  WebGL builds here (learned the hard way). The win is Blink **sprites** on the existing code-built panels.
- So re-skin = mirror the Obsidian slice into `Resources/RpgUi` under the kit's names. Fully reversible.

## How to run / revert
- **`Defenders > Art > Import Blink UI Pack`** (`DeNelle.Editor.BlinkUiImporter`) — copies the Obsidian
  slice into `Resources/RpgUi/<role>/<canonical>.png` + sets Sprite import + 9-slice borders.
- **`Defenders > Art > Import RPG UI Pack`** (`RpgUiImporter`) — restores the original Tech-hud look.
- Blink is gitignored + not under Resources, so the importer COPIES the used slice INTO Resources
  (committed) — the asset policy for any runtime-loaded Blink art.

## Mapped so far (`BlinkUiImporter.BuildTable`)
- **Panels** → Merchant→`panel_vendor`, Core_2→`panel_window_dark` (default), Core→`panel_window`,
  Inventory→`panel_grid`/`panel_inventory`, Quest→`panel_quest`, Stats→`panel_portrait`, Panel_Element→`panel_bar`/`panel_tab`.
- **Buttons** → Button1_Gray→`button_frame`, Button1_Yellow→`button_gold`, Close→`button_exit`.
- **Icons** → Sword→`icon_sword`, settings/inventory/quest-icon→`icon_settings`/`icon_inventory`/`icon_quest`.

## The masking-fill gotcha (fixed)
Some panels drew a near-opaque `*SolidFill` inset OVER the panel frame (added back when the old sprite
was see-through) — that painted out the detailed Obsidian art ("shop looked the same"). Fixed by setting
the fill alpha → 0 (object kept for layout) on **ShopPanel, EquipmentPanel, InventoryUIBuilder**. Other
panels (TroopTraining, RaidDeploy/Select) use `PanelWindowDark` with NO solid fill → they re-skin
automatically. If a row reads low-contrast on a busy panel, raise that one fill's alpha.

## Deferred / TODO (need an in-Play eyeball)
- **Bars** (`HUD_Obsidian`): the HUD uses TWO fill sources — `player_hp_fill`/`player_mp_fill` widget
  sprites for the party frames vs. `RpgUiCatalog.BarFrame/BarFill` for the mana track (with conditional
  tinting). Map carefully (frame = `Stat_Bar_Background`, fill = colored or tinted) + eyeball before committing.
- **Icons**: `icon_shield`/`icon_talk`/`icon_heart` had no clean Obsidian match in the sampled set —
  left on the Tech-hud fallback; revisit the 70-icon set for matches.
- **Slots** (`Slots_Obsidian`: Inventory_Slot, Armor_Slot, Rarity_1..5): the inventory cells / gear
  sockets — map once the panel look is confirmed.
- **Buttons**: only one frame mapped; Obsidian ships Gray/Green/Red/Yellow × 5 styles + hover states for
  richer button theming (quiet/gold/danger) later.
- **Fonts**: Obsidian ships Acme/Alata/Merriweather/Titillium — a TMP font swap is a separate, bigger pass.

## Reversibility + safety
Every change is sprite-first (null → procedural fallback) and the fills are kept (alpha 0), so nothing
breaks if Blink is absent on a machine. Re-running `RpgUiImporter` fully reverts the look.
