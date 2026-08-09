# WORK ORDER 573 — Inventory screen felt-bug fix (portrait blob + empty grid)

**Status:** DONE (reconciled 2026-08-09 from the tree - both fixes are in code: `InventoryPaperDoll.cs:29` carries the WO-573 FIX for the gold oval blob, and `InventoryGrid.cs:28/101/272` carries the WO-573 instrumentation plus the styled empty state. NOT felt-verified; no `.RESULT.md`)

Status: IMPLEMENTED (edit-only; awaiting CLI batch-gate + commit + PO felt-verify)
Date: 2026-06-28
Branch base: wip/village2-and-f8-tickets (ff-merged to tip before edits)
Silo: UI / presentation (HeroInventoryController partials). Code-built uGUI, no UXML.
Source: owner felt-test screenshot of the live Inventory screen.

---

## Issues reported (owner screenshot)

1. HERO PORTRAIT renders as a giant solid yellow/gold OVAL blob covering the left
   character card; name "Grom Ironhand" + HP/MP/LVL bars render around it.
2. Item GRID is EMPTY across ALL tabs (Weapons, Armor, Consumables confirmed by owner),
   showing a bare gray default-sprite frame instead of styled item cells.

The black+gold Obsidian chrome (title, Close, tabs, currency rail) is CORRECT and was
NOT touched (WO-554 / WO-565 preserved).

---

## RCA — Issue 1 (gold oval blob)  [proven from code, not guessed]

`InventoryPaperDoll.RebuildPaperDoll()` built the left card like this:

- `medBand` (the FULL card background) loaded a sprite through this fallback chain:
  `Tech .../Profile tabs/P1/fill.png` (Tech pack is gitignored → null) →
  `RpgUiCatalog.PanelPortrait` ("panel_portrait") → `RpgUiCatalog.PanelProfile`
  (`"profile_frame"` = **"Profile tab P1 — gold sunburst portrait medallion"**,
  RpgUiCatalog.cs:99). On the live build the chain falls through to that **gold
  sunburst medallion** sprite, stretched across this NARROW/TALL card → reads as a
  giant gold oval.
- NO real portrait art was ever loaded — the "face" was a hand-rolled tinted disc
  (`AddCircle`, blue) + a gold `AddCircleRim` + a class glyph. (`StoneNiche` is
  near-black `(0.03,0.03,0.038)`, so the medBand `else` branch was not the gold source —
  the `profile_frame` medallion sprite was.)
- The real art DOES exist: `Assets/_Modules/Onboarding/Resources/HeroPortraits/Grom.jpg`
  (+ Thrain/Sylas/Elara). BUT it is imported as `textureType: 0` / `spriteMode: 0`
  (Default texture, NOT a Sprite — see Grom.jpg.meta), so `Resources.Load<Sprite>` returns
  NULL; it must be loaded as `Texture2D` and wrapped in a `Sprite`
  (mirrors `TitleController.FramePortrait`'s Texture2D fallback).

## RCA — Issue 2 (empty grid across all tabs)  [data-empty BY DESIGN + an empty-note presentation bug]

- The grid is a pure projection of `InventoryVM.Slots`, which is built from
  `IInventoryStore.OwnedWeapons()/OwnedArmor()/OwnedConsumables()`. The concrete
  `InventoryStore` derives all three ONLY from `VillageInventory.Counts` (=
  persisted `GameState.GearInventory`) — IInventoryStore.cs:110-146.
- Documented DATA GAP (HeroInventoryController.cs:24-30): **gear is class+level
  auto-equip; there is NO per-player owned-weapons/armor list.** So weapons/armor are
  never written into `GearInventory` → `OwnedWeapons()/OwnedArmor()` return EMPTY →
  Weapons & Armor tabs are data-empty BY DESIGN. `EquipVM` (Gear Preview) uses the same
  owned-only source, so it is empty too.
- Consumables show only `GearInventory` ids that are not gear; empty if the save owns no
  potions. → ALL tabs empty is consistent with "owns nothing in GearInventory", which is
  the expected state under auto-equip, NOT a grid build/instantiate crash.
- PRESENTATION BUG (independently real): `BuildEmptyNote` parented the empty message into
  the scroll `content`, which has a `GridLayoutGroup` → the note was forced to a single
  78x72 cell and was unreadable, so the empty state never showed — only the well's gray
  frame remained. That is the "gray default-sprite placeholder" the owner saw.

Conclusion: NOT render-broken in the build path. It is (a) data-empty by an unresolved
design gap, and (b) an empty-state that never rendered. Both addressed below; the data
SOURCE is flagged to the PO (it is a design reversal, not a presentation fix — §13).

---

## Fixes implemented (edit-only, no gate/commit)

### `Assets/_Modules/Village/Hero/InventoryPaperDoll.cs`
- Removed the gold-sprite card background. `medBand` is now a flat OBSIDIAN fill
  (black + thin gold inner rim — WO-554 chrome). Kills the gold-oval source.
- Loads the REAL hero portrait via new `LoadHeroPortrait(job)` (Sprite first, then
  Texture2D → `Sprite.Create`) keyed by new `PortraitSlug(job)` (knight→Grom,
  mage→Thrain, ranger→Sylas, healer/cleric→Elara). Rendered in a fixed gold-rimmed
  frame at the top of the card with `preserveAspect = true` → never an ellipse/blob.
- No art on disk → clean dark placeholder + class crest (never a raw gold ellipse).
  Logs a `FlowTrace.Warn` when art is missing (added `using DeNelle.Core.Diagnostics`).
- Re-laid-out the card vertically (portrait top → name/class·LV → HP/MP/LVL bars),
  widened `PaperDollBarTech` to full card width (was right-half 0.50–0.97). No overlap.

### `Assets/_Modules/Village/Hero/InventoryGrid.cs`
- `RebuildGrid` instrumentation (§12 — the decisive capture): one line logging
  `store`, `villageInventory` presence, `ownedCounts` size, active tab, `slots` count;
  plus a post-build `content children` count. This splits data-empty (owned=0) vs
  projection-broken (owned>0, slots=0) vs built-but-invisible (slots>0, children=0).
- No-slots path now renders a STYLED obsidian empty-state (`BuildEmptyState`) directly
  into the grid root (NOT inside the GridLayoutGroup), so the message renders full-size
  and the bare gray frame is gone.
- `EmptyTabNote` copy for Weapons/Armor now points the player to Gear Preview for
  equipped gear.

Brace check: InventoryPaperDoll.cs 29/29 OK; InventoryGrid.cs 53/53 OK.

## Files modified (for reconcile, stage by explicit path)
- `Assets/_Modules/Village/Hero/InventoryPaperDoll.cs`
- `Assets/_Modules/Village/Hero/InventoryGrid.cs`

(No VM/store edits — InventoryVM.cs, InventoryStore/IInventoryStore.cs, EquipVM.cs UNCHANGED.)

## NOT touched
- Obsidian chrome (WO-554), hidden Sort/Filter (WO-565), tab row, currency rail, equip
  routing, the VM/store seams.

---

## OWNER-DECISION FLAGS (PO)

1. **Why items don't appear (the real cause): the inventory + Gear Preview list OWNED
   gear only, and there is NO owned-gear model (gear is class+level auto-equip), so they
   are empty by design.** This is a design reversal call, not a presentation fix, so it was
   NOT done unilaterally (§2/§13). Options:
   - (A) Make Weapons/Armor tabs list class-eligible CATALOG gear (restore pre-WO-434
     visible-gear behavior) — fast, shows items now; would also want EquipVM parity.
   - (B) Seed a real owned-gear list (loot/shop/craft grants write to GearInventory) —
     correct long-term; matches the "what you OWN" intent WO-434 set.
   Until chosen, the tabs show the new styled empty-state (not a broken gray box).
2. **Portrait import:** the HeroPortraits textures are imported as Default (not Sprite);
   the code wraps them at runtime. If you want crisp/atlassed portraits, flip the importer
   to Sprite (2D) — optional, the runtime wrap works as-is.
3. **Confirm the next felt-test/headless capture** shows the `[Flow:Inventory] RebuildGrid`
   line with `ownedCounts=0` to confirm data-empty (vs a surprise `ownedCounts>0, slots=0`
   that would mean a projection bug to chase).
