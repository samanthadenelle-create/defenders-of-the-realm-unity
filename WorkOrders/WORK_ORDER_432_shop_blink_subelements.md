# WORK ORDER 432 — Dress the Shop's structural sub-elements with Blink sprites

**Status:** READY TO IMPLEMENT
**Lane:** UI / Presentation (re-skin) — follow-on to **WO-431** (ShopPanel MVVM slice).
**Type:** Cosmetic re-skin, flag-gated. No logic/behavior change.
**Owner routing:** CLI writes the code (UI never touches `.cs`).

---

## 1. Problem (RCA from a live F8 capture)

Owner note: *"see mixed, can see ui"* — captured with `ff.blinkchrome=1` (the
`FeatureFlags.BlinkChrome` flag ON).

With BlinkChrome ON, the Forge shop reads as **Blink sprites dressing OUR structure** —
a "mixed" look. The flag already correctly hides our decorative chrome (rims/glows/niches/
header shadow/solid fills — gated in `ElarionUiKit` + `ShopPanel.BuildChrome`), and the
panel base frame + buttons use Blink sprites (`RpgUiCatalog.PanelVendor` = `Merchant_Panel`,
`ButtonGold`/`ButtonFrame` = Obsidian buttons). **But the structural sub-elements are still
our code-built `ElarionUiKit` pieces**, so they read as ours:

| Sub-element | Today (ours) | Should read (Blink) |
|---|---|---|
| **Row tiles** (each buy/sell/equip row bg) | flat `ElarionUiKit.Cell` tint (`rowImg.color = ElarionUiKit.Cell`) | Blink per-item slot plate (`Inventory_Slot`, beveled `ArticleSlot`/`ItemBackground` look) |
| **List well / tray** | `ElarionUiKit.Well` (`Track` = black 45%) | neutralized (transparent) so the Blink panel shows through, OR a Blink panel plate |
| **Detail pane frame** | `RpgUiCatalog.PanelPortrait` | **already Blink-mapped** (importer maps `panel_portrait` ← `Stats_Panel.png`) — VERIFY, likely no change |
| **Tab bar / filter bar layout, fonts/label colors** | `ElarionUi` palette | acceptable to keep (layout is ours-only per binding map); only neutralize backing tints if they read as ours |

**Most visible mismatch = the ROW TILES.** Blink lays each item on its own beveled
`ArticleSlot` plate (`ItemBackground` sprite); ours is a flat tinted rectangle. Fixing the
row tile alone removes ~80% of the "mixed" read.

---

## 2. Goal

Dress the shop's remaining structural sub-elements with Blink sprites so the panel reads as
**ONE Blink Obsidian surface**, not Blink-on-ours — **WITHOUT** swapping to a full Blink
prefab View yet (that is the larger §5 endgame; see §9 below).

**Every change is gated behind `ff.blinkchrome` (`DeNelle.Core.FeatureFlags.BlinkChrome`):**
- **flag OFF** → our original look is *byte-for-byte unchanged* (flat `Cell` rows, `Well` tray).
- **flag ON** → row tiles use the Blink slot plate, the well is neutralized, the panel reads
  as one Blink surface.

This is the **same pattern as the existing chrome gating** (`ElarionUiKit.Niche`/`Header`/
`Rule`/`AddInnerRim`/`AddRimUnderline` all branch on `FeatureFlags.BlinkChrome`, and
`ShopPanel.BuildChrome` zeroes `fillColor.a`/`glowColor.a` when the flag is ON).

---

## 3. The Blink slot sprite to use (and the import step it needs)

**Source art (identified):**
`Assets/Blink/Art/UI/Obsidian_UI/Slots_Obsidian/Inventory_Slot.png`

This is the per-item slot/`ItemBackground` plate — it is what `MerchantPanel.prefab`'s
`ArticleSlot > ItemBackground` references (the binding-map §1/§3 `ArticleSlot = ItemBackground +
ItemIcon + ItemName + Price` insight). `Armor_Slot.png` is the alt; **use `Inventory_Slot.png`**
(the generic article plate).

> **NOT YET IMPORTED.** There is **no slot role in `Resources/RpgUi`** today
> (`Assets/Resources/RpgUi/panel/`, `button/`, `icons/`, `bars/`, `potion/`, `badge/` only —
> no `slot/`, no `*slot*` png). The plate must be mirrored into Resources first, exactly like
> every other Blink sprite (BLINK_UI.md asset policy: Blink is gitignored / not under Resources,
> so the importer COPIES the used slice into committed `Resources/RpgUi`).

### 3a. Importer change — add the slot plate to `BlinkUiImporter.BuildTable()`
File: `Assets/Editor/BlinkUiImporter.cs`

Add an entry mirroring `Inventory_Slot.png` into a **new `slot` role** under a canonical name
`slot_item`:

```csharp
// ── SLOTS (RoleSlot) — per-item article plate for shop/inventory rows ──
new Entry { Src = "Slots_Obsidian/Inventory_Slot.png", Role = "slot", Name = "slot_item", Border = 24 },
```

(9-slice border ~24 so the plate scales clean to the row rect; tune in-Play if the bevel
stretches. The importer's `ForceSprite` already sets Sprite + 9-slice from `Border`.)

### 3b. Catalog role + constant — `RpgUiCatalog.cs`
File: `Assets/_Modules/Core/UI/RpgUiCatalog.cs`

Add a `RoleSlot` role and a `SlotItem` name constant (mirrors the existing `RolePanel` /
`PanelVendor` pattern — role folder + well-known name):

```csharp
public const string RoleSlot = "slot";
// slot/ — per-item article plate (Blink ArticleSlot/ItemBackground), 9-sliced:
public const string SlotItem = "slot_item";
```

`RpgUiCatalog.Get(RoleSlot, SlotItem)` then returns the plate sprite (or **null** when the
slice is not imported — callers keep the procedural fallback, per the sprite-first contract).

### 3c. The bake/import run
`Defenders > Art > Import Blink UI Pack` (`DeNelle.Editor.BlinkUiImporter.Run`) must be
re-run so the new `slot/slot_item.png` lands in `Resources/RpgUi/slot/` and is committed.
**This is a batchmode editor run — UI does NOT fire it; it goes in this WO for CLI.**

---

## 4. Files / methods to edit

> **Line numbers below are post-WO-431-MVVM-refactor (verified against current `ShopPanel.cs`).**
> The old `CreateBuyRow` (~L842) / `CreateSellRow` (~L1079) are GONE — the refactor unified them
> into a single `CreateRow`. Use the method names, not the stale RCA line numbers.

### A. `Assets/_Modules/Village/Hero/ShopPanel.cs`

1. **`CreateRow(Transform parent, ItemVM item)` — ~L521** (buy/sell row factory).
   Today (L532–534):
   ```csharp
   var rowImg = row.GetComponent<Image>();
   rowImg.color = ElarionUiKit.Cell;
   ElarionUiKit.ApplyRounded(rowImg);
   ```
   Change: when `FeatureFlags.BlinkChrome` is ON and the slot plate resolves, dress `rowImg`
   with the Blink plate (`sprite` = `RpgUiCatalog.Get(RoleSlot, SlotItem)`, `type = Sliced`,
   `color = Color.white`) **instead of** the `Cell` tint + procedural rounded. Flag OFF → keep
   the exact current two lines. Sprite-first: if the plate is null (not imported), fall back to
   the current `Cell` look even when flag ON (never blank a row).
   - Keep `rowBtn.targetGraphic = rowImg;` and the click wiring untouched — the plate is the
     button's graphic, same as today.

2. **`CreateEquipRow(Transform parent, ItemVM item)` — ~L596**.
   Same edit at L603–605 (`rowImg.color = ElarionUiKit.Cell; ElarionUiKit.ApplyRounded(rowImg);`).
   Apply the identical flag-gated plate swap so equip rows match buy/sell rows.

3. **`BuildScrollContent(int rowCount)` — ~L462** (the list well).
   Today (L464–466):
   ```csharp
   var well = ElarionUiKit.Well(_contentRoot.transform, Vector2.zero, Vector2.one);
   var wImg = well.GetComponent<Image>();
   if (wImg != null) wImg.raycastTarget = false;
   ```
   Change: when the flag is ON, **neutralize the well** so the Blink panel shows through behind
   the per-item plates (Blink uses per-slot plates with no shared well — binding-map §3:
   *"Well → (Blink uses per-slot plates; no shared well)"*). Set `wImg.color` alpha → 0 (keep the
   GameObject for layout, same technique as the `*SolidFill` alpha-0 fix in BLINK_UI.md and
   `ElarionUiKit.Niche`). Flag OFF → keep the current `Well` look. Do **not** change the
   `Viewport`/`ScrollContent`/`VerticalLayoutGroup`/`ContentSizeFitter`/`ScrollRect` mechanism —
   only the backing well's tint.

4. **`BuildDetailsPane(Transform panel)` — ~L357** — **VERIFY ONLY, likely no edit.**
   The portrait frame already uses `RpgUiCatalog.Get(RolePanel, PanelPortrait)` (L373), and
   `panel_portrait` IS Blink-mapped (importer: `Stats_Panel.png` → `panel_portrait`). Confirm in
   the capture that the detail frame reads Blink. The `else` branch falls back to `ElarionUiKit.Well`
   only when the sprite is absent — fine. If on inspection the detail pane still reads as ours,
   apply the same flag-gated neutralize as the list well; otherwise leave it.

5. **Tab bar / filter bar — VERIFY, neutralize only if they read as ours.**
   Buttons already use Blink (`ButtonFrame`/`ButtonGold`). The bars are bare `RectTransform`
   containers (no backing Image), so there is likely nothing to dress. If the `TabSelectedTint` /
   `TabRestTint` multiply makes the Blink button art read wrong under the flag, that is a separate
   tuning note — **do NOT restyle tabs in this WO** unless the capture shows a clear ours-read.

### B. `Assets/Editor/BlinkUiImporter.cs` — §3a entry.

### C. `Assets/_Modules/Core/UI/RpgUiCatalog.cs` — §3b role + constant.

> **Optional (cleaner, CLI's call):** put the row-plate dressing as a small shared helper on
> `ElarionUiKit` (e.g. `DressRowPlate(Image img)` that does the flag-gated plate-or-Cell choice)
> so `CreateRow` + `CreateEquipRow` + any future list panel share one gated recipe (binding-map §3:
> the `Slot` unit is ~70% of the visual surface — one helper, reused). Acceptable to keep it inline
> in `ShopPanel` for this slice if cleaner. Either way, **flag-gate at the single choice point.**

---

## 5. Flag-gating requirement (the contract)

- **`ff.blinkchrome` OFF (default):** row tiles = `ElarionUiKit.Cell` + procedural rounded;
  list well = `ElarionUiKit.Well` (Track black 45%). **Identical to today** — zero visual diff.
- **`ff.blinkchrome` ON:** row tiles = Blink `slot_item` plate (9-sliced, `Color.white`); list
  well = transparent (object kept for layout); detail pane = Blink (already); panel reads as one
  Blink Obsidian surface.
- **Sprite-first safety:** every flag-ON path that points at a Blink sprite must fall back to the
  current look if `RpgUiCatalog.Get(...)` returns null (slice not imported on a given machine).
  A row must NEVER blank.

---

## 6. Acceptance criteria

1. **Flag OFF — pixel-unchanged.** With `ff.blinkchrome=0`, the shop looks exactly as it does
   today: flat `Cell` rows over the dark `Well`. (Verified by toggling the
   `Defenders/Debug/Blink Chrome` menu / `ff.blinkchrome` and eyeballing — diff = none.)
2. **Flag ON — each row is a Blink plate.** With `ff.blinkchrome=1`, every buy / sell / equip
   row background is the Blink `Inventory_Slot` article plate (beveled), not a flat tint.
3. **Flag ON — the well/tray reads Blink or is neutralized.** The scroll tray no longer shows our
   black `Well`; the Blink panel shows through behind the per-item plates.
4. **Flag ON — detail pane reads Blink** (confirm `panel_portrait` is the `Stats_Panel` art);
   tab/filter bars do not read as a competing ours-surface.
5. **Behavior / layout unchanged.** Row height (`RowHeightPx`), spacing, the
   `VerticalLayoutGroup` + `ContentSizeFitter` + `ScrollRect` mechanism, click-to-select, buy/sell/
   equip, the "never-empty" stock, and `CurrentStock` are all untouched. The
   `ShopPanelRowRenderTests` guard still passes.
6. **`slot/slot_item.png`** exists under `Resources/RpgUi/slot/` (committed) after the importer run,
   and `RpgUiCatalog.Get(RoleSlot, SlotItem)` resolves it at runtime.
7. **Brace balance + compile gate** pass on every `.cs` touched (`COMPILE_GATE_OK`).

---

## 7. What NOT to touch

- **Do NOT change `ShopVM` or any logic** — economy reads, catalog→row building, buy/sell/equip,
  vendor-gold, affordability, never-empty fallback, stock contract. This WO is the **View skin only**;
  the View must still bind the same VM the same way.
- **Do NOT break the scroll-layout mechanism** — leave `BuildScrollContent`'s `Viewport`,
  `ScrollContent`, `VerticalLayoutGroup`, `ContentSizeFitter`, `ScrollRect`, the per-row
  `LayoutElement` (`preferredHeight`/`minHeight`), `RowHeightPx`, `RowGapPx` exactly as they are.
  Only the **backing tints/sprites** change.
- **Do NOT restyle when the flag is OFF.** Every edit branches on `FeatureFlags.BlinkChrome`; the
  OFF path must reproduce the current code verbatim.
- **Do NOT change row click wiring** (`rowBtn.targetGraphic`, `_vm?.Select(id)`, the `View`/`EQUIP`
  child buttons).
- **Do NOT hand-edit any `.unity` scene** or fire a bake of the Village scene. (The only batchmode
  run here is the **Blink importer**, §3c.)
- **Do NOT introduce a new font swap** (Obsidian font pass is out of scope — BLINK_UI.md deferred).
- **Do NOT migrate to the Blink prefab View** (that is §9 / binding-map §5 — explicitly out of scope).

---

## 8. Test gate (ARCHITECTURE_PRINCIPLES §2c)

This is **cosmetic** — the gate is a **play-capture retest** (F8 / headless capture with
`ff.blinkchrome=1`, owner eyeball of flag-ON vs flag-OFF). **No new unit test is required** unless a
**computed value** is introduced (e.g. a helper that picks a sprite by some derived condition) — if
so, add a small pure test locking that choice. The existing `ShopPanelRowRenderTests` must remain
green (proves the row-render mechanism is intact).

---

## 9. Alternative (the bigger future option — NOT this WO)

The full endgame is **adopt the Blink `MerchantPanel.prefab` wholesale** as the shop View, bound to
the same `ShopVM` (binding-map **§5 step 2/4**: *extract VM (done in WO-431) → rebind our View →
optionally drop in the Blink prefab View*). That swaps the entire skin (panel + grid + slots) for
Blink art in one move, no per-element dressing. It is **holistic/leverage** work, its own WO, and
must NOT be smuggled into this player-facing re-skin. **This WO-432 is the cheap interim** that makes
the current code-built View read as one Blink surface today, fully reversible via the flag.

---

## Cross-refs
- `docs/UI_MVVM_BINDING_MAP.md` §1 (repeating-unit / `ArticleSlot = ItemBackground+ItemIcon+ItemName+Price`),
  §3 (primitive map: `Card`/`Slot` ↔ `ArticleSlot`/`ItemBackground`; `Well` → no shared Blink well), §5 (the prefab-View endgame).
- `docs/BLINK_UI.md` (re-skin system, importer, the `*SolidFill` alpha-0 / neutralize technique, slots deferred-TODO).
- `Assets/_Modules/Core/FeatureFlags.cs` (`BlinkChrome`, `ff.blinkchrome`, the `Defenders/Debug/Blink Chrome` menu).
- WO-431 (ShopPanel MVVM slice — the View this dresses).
