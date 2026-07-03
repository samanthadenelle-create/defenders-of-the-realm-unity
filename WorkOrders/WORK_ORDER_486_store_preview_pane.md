# WORK ORDER 486 — Store Preview Pane + Per-Item Sprites (Knight gear)

**Status:** READY TO IMPLEMENT
**Silo:** Monetization/UI (Store) — `PartyShop` MVVM. File-disjoint from the Knight combat lane; **queue BEHIND the Knight** (this is shop polish, not the V1 north-star).
**Feature flag:** ships behind the existing `FeatureFlags.PartyShop` (OFF). No new flag.
**Lane note:** touches ONLY the PartyShop view + the gear catalog data — does NOT touch `VillageSceneBuilder`, combat, or any `.unity` scene. Safe to fan out as an edit-only agent; orchestrator batch-gates + commits per §11.

---

## 0. Context / north-star

The PartyShop (`FeatureFlags.PartyShop`) is the code-built uGUI MVVM weapon/armor shop
(`PartyShopVM` + `PartyShopPanelMvvm`). It already has: party selector, Buy/Sell tabs,
category chips (All/Weapons/Armor), per-row stat+delta line, and per-row sprite resolution
with a glyph fallback. What it is MISSING for the owner's store vision:

1. A right-side **PREVIEW PANE** — when a row is selected, a large item image + name + flavor +
   stats render in a dedicated pane (today the detail only appears inline per-row, cramped).
2. The list rows are **full panel width** — with a preview pane added, the rows must **narrow**
   to free the right third for the preview.
3. **Real per-item sprites** wired by id (`iconPath`) with an **emoji/glyph fallback so a row or
   the preview NEVER blanks**. Today no knight weapon/shield carries an `iconPath`, so every row
   falls through to the generic tier-glyph sheet (a generic sword silhouette, not the actual item).

This WO delivers all three for the **Knight gear set** (5 swords + 5 shields) as the proof slice;
the same `iconPath` mechanism then generalizes to every other class with zero code change (data only).

---

## 1. SME — current reuse map (cite file:line)

**Everything below already exists. REUSE it; do not greenfield a second shop.**

- **`PartyShopVM.cs`** (`Assets/_Modules/Village/Hero/PartyShopVM.cs`)
  - `PartyShopDetail` struct **already carries** `Stats`, `Delta`, `Description`, `IconPath`,
    `IconRole`, `IconName` — `PartyShopVM.cs:62-87`. **The preview pane needs NO new VM fields** —
    it binds the existing `Selected` payload.
  - `public PartyShopDetail? Selected` — `PartyShopVM.cs:256-257` — the selected row's detail,
    null when nothing selected. **This is the preview pane's data source.**
  - `public string SelectedId` + `void Select(string id)` — `PartyShopVM.cs:253, 311-316` — the
    View already calls `_vm.Select(id)` on row tap (`PartyShopPanelMvvm.cs:604`).
  - Detail is populated from `w.iconPath` for weapons (`PartyShopVM.cs:462-464`) and `a.iconPath`
    for armor (`:480-482`). **Wiring `iconPath` in the JSON flows straight into the preview.**
  - `MemberLabel` (`:237-248`), `Status` (`:263`), `Title` (`:190`) — reuse as-is.

- **`PartyShopPanelMvvm.cs`** (`Assets/_Modules/Village/Hero/PartyShopPanelMvvm.cs`)
  - `BuildChrome()` — `:217-319`. The content/grid area is anchored
    `cr.anchorMin = (0.04, 0.12)`, `cr.anchorMax = (0.96, 0.70)` at **`:300`** — this is the value
    to NARROW (see §3).
  - `Render()` — `:199-213` repaints from `_vm.*` only. **Add a `RenderPreview()` call here.**
  - `ResolveItemSprite(detail, item)` — `:671-693` — **already does
    `Resources.Load<Sprite>(iconPath)` first (`:676`), then `ItemIconCatalog` art, then a pack
    glyph.** This is the never-blank fallback chain. **REUSE for the preview's large image.**
  - `CreateRow(...)` — `:583-667` — the per-row builder (icon host at `:607-624`, name, stat line,
    price, action button). Rows go into a `VerticalLayoutGroup` scroll content (`BuildScrollContent`
    `:520-570`).
  - `HighlightSelectedRow()` — `:500-515` — already reflects `_vm.SelectedId`. The preview repaint
    hangs off the same Render pass.

- **`GearCatalog.cs` / `WeaponDef`** (`Assets/_Modules/Village/Hero/GearCatalog.cs`)
  - `WeaponDef.iconPath` — `GearCatalog.cs:97` (inventory/store sprite key; **currently null for all
    gear** — comment at `:92-95` says the generator populates it; for Knight V1 we author it directly
    in the canonical JSON since the generator isn't wired for these loose sprites yet).
  - `ArmorDef.iconPath` — `:174` (shields are weapons with `category:"shield"`, so they use
    `WeaponDef.iconPath` — see §4 note).
  - `WeaponDef.flavor` — `:83`, `WeaponDef.makersMark` — `:84` — the preview's flavor text.
  - `WeaponDef.rarity`, `damageMult`, `reach`, `req.level` — the preview's stat block.

- **`ItemIconCatalog.cs`** (`Assets/_Modules/Village/Hero/ItemIconCatalog.cs:57-92`) — the
  `ForWeapon`/`ForArmor` keyword→tier-sheet fallback. Stays as the SECOND fallback after `iconPath`.

- **Loose-sprite convention:** `Resources.Load<Sprite>(path)` (no extension, Resources-relative).
  Knight gear sprites live at **`Assets/Resources/ItemIcons/Weapons/<id>.png`** → `iconPath` =
  `"ItemIcons/Weapons/<id>"`. (`Resources/ItemIcons/` already holds loose `tripo_*.png` /
  `blink_*.png` sprites the same way.)

---

## 2. Deliverable A — right-side PREVIEW PANE

**Build a preview pane in `PartyShopPanelMvvm.BuildChrome()`, bound to `_vm.Selected`.**

- New panel field `_previewRoot` (RectTransform) + cached child refs:
  `_previewImage` (Image), `_previewName` (TMP), `_previewFlavor` (TMP), `_previewStats` (TMP),
  `_previewDelta` (TMP).
- Anchor it to the **right third** of the framed panel, spanning the list's vertical band:
  `anchorMin = (0.66, 0.12)`, `anchorMax = (0.96, 0.70)` (matches the narrowed grid's Y band so the
  preview sits beside the list). Use `ElarionUiKit.Well(...)` for the backing plate (same dark-glass
  the list uses) so it reads as one cohesive panel; respect `FeatureFlags.BlinkChrome` alpha like the
  Well at `:526-527`.
- Layout inside the pane (top→bottom):
  - **Large image** — square, top ~55% of the pane, `preserveAspect = true`, `raycastTarget = false`.
    Sprite resolved via the **existing `ResolveItemSprite(detail, item)` chain** (extract/reuse so the
    preview and rows share ONE resolver — do NOT duplicate the load logic). When nothing is selected,
    show the empty-state copy "Select an item to preview." and no image.
  - **Name** (`detail.IconName`→def name; reuse the row's name source) — gilt, bold, `FontHead`.
  - **Stats** — `detail.Value.Stats` (e.g. "+25% dmg   reach 3.4m   1h"). `FontLabel`.
  - **Delta** — `detail.Value.Delta` (e.g. "+25% dmg vs equipped"), colored via the existing
    `DeltaColor(delta)` helper (`:695-701`). Hidden when empty.
  - **Flavor** — `detail.Value.Description` (the VM's `DescribeGear` line). For richer flavor, prefer
    the def's `flavor`/`makersMark` if the VM is extended to pass them through `Description`
    (OPTIONAL — see §5; not required for v1, `Description` is acceptable).
- **`RenderPreview()`** — new private method called from `Render()` (`:199-213`). Reads `_vm.Selected`
  (a `PartyShopDetail?`). When null → empty state. When set → fill the cached refs. **No state-pull:
  the View reads only `_vm.Selected`** (architecture law — presentation never touches game state).
- Tapping a row already calls `_vm.Select(id)` (`:604`) → `Changed` → `Render()` → `RenderPreview()`.
  **No new input wiring needed.**
- Selecting the first row by default is OPTIONAL nicety: after `RebuildList()`, if `_vm.SelectedId`
  is null and `_vm.Items.Count > 0`, the VM could auto-select item[0]. Keep v1 simple: empty preview
  until the player taps. (Do not add VM logic unless trivial.)

---

## 3. Deliverable B — NARROW the list rows

**Current grid width:** `PartyShopPanelMvvm.cs:300` —
`cr.anchorMin = new Vector2(0.04f, 0.12f); cr.anchorMax = new Vector2(0.96f, 0.70f);`
(grid spans X 0.04→0.96, i.e. ~92% of the panel — full width).

**Target:** narrow the grid's right edge so the preview pane (§2, X 0.66→0.96) fits beside it:
`cr.anchorMin = new Vector2(0.04f, 0.12f); cr.anchorMax = new Vector2(0.635f, 0.70f);`
(grid X 0.04→0.635; a ~0.025 gutter before the preview at 0.66.)

- Rows live in a `VerticalLayoutGroup` with `childForceExpandWidth = true` (`:553`), so narrowing the
  `_contentRoot` anchor automatically narrows every row — **no per-row width edit needed.**
- Sanity-check the in-row sub-anchors still read at the narrower width: name `0.16→0.66`
  (`:628`), stat line `0.16→0.66` (`:635`), price `0.66→0.80` (`:642`), action button `0.82→0.985`
  (`:662`). These are FRACTIONS of the row, so they rescale automatically — but verify the action
  button + price don't crowd at the narrower pixel width; if cramped, widen the icon-host/name split
  modestly. (Cosmetic; tune at build-review.)
- The category bar (`:287-294`), party bar, tabs, header, wallet stay full-width above the grid —
  only the scroll grid + the new preview share the lower band.

---

## 4. Deliverable C — SPRITE wiring (iconPath + Resources.Load + emoji fallback)

**The mechanism already exists** (`ResolveItemSprite` `:671-693`, `Resources.Load<Sprite>(iconPath)`
`:676`). This deliverable is **DATA**: populate `iconPath` on the 10 Knight gear entries in the
canonical `weapons.json`, plus a hardening pass on the fallback so it never blanks.

- **Canonical JSON** is `Assets/Resources/Data/Canonical/weapons.json` (Resources, WebGL-safe path
  per `GearCatalog.cs:416 CanonicalJson.Read`). The mirror copies under
  `Assets/StreamingAssets/...`, `Builds/...` are downstream — **edit the `Resources/Data/Canonical`
  copy; the build pipeline re-syncs the others** (do NOT hand-sync builds).
- Add `"iconPath": "ItemIcons/Weapons/<id>"` to each of the 10 Knight entries (ids in §4-table).
- **Shields are `WeaponDef` rows** (`category:"shield"`, see `GearCatalog.cs:128-129` `IsOffHandItem`;
  they live in `weapons.json`, not `armor.json`). So all 10 use `WeaponDef.iconPath` and the BUY/SELL
  weapon row path (`AddBuyWeaponRow` `:457`). **Confirm: shields are NOT in `armor.json`.** (Verified:
  `knight_shield_starter` is in `weapons.json` with `category:"shield"`.)
- **Emoji/glyph fallback (never-blank guarantee):** the resolver chain is
  `iconPath sprite → ItemIconCatalog.ForWeapon → pack glyph (IconSword/IconShield)`. The row already
  draws a `/` or `[]` text glyph when the sprite is null (`:619-624`). For the **preview pane**, when
  the resolved sprite is null, render the def's emoji `icon` field (`WeaponDef.icon`, e.g. "🗡️"/"🛡️")
  as a large TMP glyph instead of a broken image — this is the owner-requested "emoji fallback so it
  never blanks." (The row keeps its existing `/`/`[]` glyph; the preview uses the richer emoji.)
  - **Minor VM touch (allowed, additive):** `PartyShopDetail` does not currently carry the emoji
    `icon`. Add an optional `string Emoji` field to `PartyShopDetail` populated from `w.icon`/`a.icon`
    so the preview's fallback has it (the View must not state-pull the def). One field, one assignment
    site each in `AddBuyWeaponRow`/`AddBuyArmorRow`/`BuildSell`. Brace-balance after.

---

## 5. Deliverable D — the 10 image slots (OWNER ART HAND-OFF list)

**Shared id set — coordinate with the gear-stats + weapon-VFX prep (same weapon ids).** Current
`weapons.json` has 4 knight swords + 1 shield. This WO adds **a 5th sword + 4 new shields** so the set
is **5 swords + 5 shields = 10**. New JSON entries (id, name, rarity, req) must be authored alongside
the `iconPath` field.

### Swords (5) — `category` absent (main-hand 1h/2h)
| id | name | rarity | status | sprite path |
|---|---|---|---|---|
| `knight_starter` | Squire's Blade | common | exists | `Resources/ItemIcons/Weapons/knight_starter.png` |
| `knight_iron` | Iron Longsword | uncommon | exists | `Resources/ItemIcons/Weapons/knight_iron.png` |
| `knight_oath` | Oathkeeper | rare | exists | `Resources/ItemIcons/Weapons/knight_oath.png` |
| `knight_dawn` | Dawnbreaker | epic | exists | `Resources/ItemIcons/Weapons/knight_dawn.png` |
| `knight_aegis` | Aegis Edge (5th — NEW) | legendary | **NEW JSON entry** | `Resources/ItemIcons/Weapons/knight_aegis.png` |

### Shields (5) — `category:"shield"`
| id | name | rarity | status | sprite path |
|---|---|---|---|---|
| `knight_shield_starter` | Squire's Heater | common | exists | `Resources/ItemIcons/Weapons/knight_shield_starter.png` |
| `knight_shield_iron` | Iron Kite Shield | uncommon | **NEW JSON entry** | `Resources/ItemIcons/Weapons/knight_shield_iron.png` |
| `knight_shield_oath` | Oathbound Bulwark | rare | **NEW JSON entry** | `Resources/ItemIcons/Weapons/knight_shield_oath.png` |
| `knight_shield_dawn` | Dawnward Aegis | epic | **NEW JSON entry** | `Resources/ItemIcons/Weapons/knight_shield_dawn.png` |
| `knight_shield_aegis` | Aegis of Elarion | legendary | **NEW JSON entry** | `Resources/ItemIcons/Weapons/knight_shield_aegis.png` |

> The owner / a creative pass owns the final NAMES + the 5th-sword/4-shield STAT tuning (damageMult,
> req.level, costs). The ids above are the contract the art + the gear-stats + weapon-VFX prep all key
> on — **do not rename without updating all three.** If the gear-stats prep already minted different
> 5th-sword / shield ids, defer to THAT id set and update this table (single shared set).

### Sprite import settings (owner art hand-off)
Each of the 10 PNGs dropped into `Assets/Resources/ItemIcons/Weapons/`:
- **Texture Type:** `Sprite (2D and UI)`
- **Format:** transparent **PNG** (alpha; item on transparent bg — no baked background)
- **Sprite Mode:** Single
- **Generate Mip Maps:** **OFF** (UI sprite, never minified in 3D)
- **Alpha Is Transparency:** ON; **Wrap:** Clamp; **Filter:** Bilinear
- Square-ish source (≥256², 512² ideal) for the large preview image to read crisply.
- Filename == the `iconPath` leaf (e.g. `knight_oath.png` → `ItemIcons/Weapons/knight_oath`).

Until the owner supplies the real PNGs, the **emoji + ItemIconCatalog tier-glyph fallback covers
every slot** (never blank) — so this WO is implementable + verifiable BEFORE the art lands.

---

## 6. Reuse-vs-new-code table

| Concern | REUSE (existing) | NEW |
|---|---|---|
| Selected-item data | `PartyShopVM.Selected` / `PartyShopDetail` (`PartyShopVM.cs:256, 62`) | — |
| Sprite resolution (iconPath→catalog→glyph) | `ResolveItemSprite` (`PartyShopPanelMvvm.cs:671`) | extract to share with preview |
| Emoji-never-blank | def `icon` field (`WeaponDef.icon`) | `PartyShopDetail.Emoji` field + preview glyph fallback |
| Row tap → select | `_vm.Select(id)` (`:604`), `HighlightSelectedRow` (`:500`) | — |
| Stat / delta strings | `WeaponStats`/`ArmorStats`/`DeltaVsEquipped*`/`DescribeGear` (VM) | — |
| Delta color | `DeltaColor` (`:695`) | — |
| Dark-glass plate | `ElarionUiKit.Well` (`:520`) | preview pane Well |
| Grid layout | `BuildScrollContent` VLG (`:520-570`) | narrow `_contentRoot` anchor (`:300`) |
| Preview pane chrome | `ElarionUiKit.AddImage`/`Label`/`Header` | `BuildPreviewPane()` + `RenderPreview()` |
| Per-item art | `iconPath` field (`GearCatalog.cs:97`) | author on 10 JSON entries + 4 new shield + 1 sword entries |

**Net new code:** ~1 `BuildPreviewPane()` + ~1 `RenderPreview()` in `PartyShopPanelMvvm.cs`,
1 anchor-value change, 1 `Emoji` field + 3 assignment sites in `PartyShopVM.cs`. **No new files,
no new assembly, no new flag.** Pure data: 5 new JSON entries + 10 `iconPath` fields.

---

## 7. Files to edit
- `Assets/_Modules/Village/Hero/PartyShopPanelMvvm.cs` — preview pane build + render; narrow grid anchor.
- `Assets/_Modules/Village/Hero/PartyShopVM.cs` — add `PartyShopDetail.Emoji` (one field + assignments).
- `Assets/Resources/Data/Canonical/weapons.json` — 10 `iconPath` fields + 5 new gear entries
  (1 sword + 4 shields).

**Do NOT touch:** any `.unity` scene, `VillageSceneBuilder.cs`, combat/ATB/BattleArena code,
`GearCatalog.cs` schema (the fields already exist), `ItemIconCatalog.cs`, the StreamingAssets/Builds
JSON mirrors (downstream — pipeline re-syncs).

---

## 8. Acceptance criteria
1. With `FeatureFlags.PartyShop` ON, opening the shop and tapping a row shows a **right-side preview
   pane**: large image + name + stats + delta + flavor, driven by `_vm.Selected`.
2. The list rows are **narrower** (grid right edge at ~0.635) and the preview sits beside them; no
   overlap, no clipping.
3. Every catalog row resolves **a sprite OR an emoji/glyph** — **NO blank icon** in any row or in the
   preview, even before the art PNGs are imported (fallback chain proven).
4. The 10 Knight ids (5 swords + 5 shields) carry `iconPath` and, once PNGs land in
   `Resources/ItemIcons/Weapons/`, the real art shows in row + preview; before that, emoji/glyph shows.
5. View never reads game state — preview binds only `_vm.Selected` / `_vm.*` (architecture law).
6. Brace-balance check passes on both `.cs` files (§1 gate).

---

## 9. Headless verify (no owner playtest)
Add/extend a `DataRegression` (or AutoPilot store oracle) asserting:
- **`STORE-ICON-RESOLVES`:** for EVERY weapon + armor def in the catalog, the resolver chain yields a
  non-null result OR a non-empty emoji fallback — i.e. `ResolveItemSprite(...) != null`
  `|| !string.IsNullOrEmpty(def.icon)`. Fail loud (FlowTrace.Fail) on any def that resolves NEITHER a
  sprite NOR an emoji. This is the "never blanks" guarantee, gate-checked.
- **`STORE-KNIGHT-SET`:** the 5 sword + 5 shield Knight ids all load from the catalog and each has a
  non-empty `iconPath`.
- Run under the existing headless `CompileGate` + AutoPilot fleet (no editor open; §3 of CLAUDE.md).
- The shop is flag-gated OFF, so the regression must enable `FeatureFlags.PartyShop` (or test the VM
  + resolver directly without opening the panel — preferred, since the VM is pure/unit-testable).

---

## 10. Notes for the implementer
- Keep the preview pane **presentation-only** — every datum comes from `_vm.Selected` / VM getters.
  If the VM doesn't expose something the preview needs (e.g. the def's `flavor`/`makersMark` distinct
  from `Description`), add it to `PartyShopDetail` as an additive field, never state-pull from the def
  in the View.
- Extract the sprite resolver so rows + preview share ONE code path (DRY; avoids a second blank-bug
  surface).
- Instrument per §12: a `FlowTrace.Step("Store", "preview rendered id=…")` on `RenderPreview` and a
  `FlowTrace.Warn` when the preview falls back to emoji (so headless capture can prove the never-blank
  path took which branch).
