# WORK ORDER 554 — Shared Obsidian Panel Chrome (black + gold trim, one Close)

**Status:** DONE

> **DONE - verified in HEAD 2026-08-14 (phantom sweep).** The work is present at ElarionUiKit.cs:187,537.
> Status had read READY because the landing commit did not flip this line in the same commit
> (CLAUDE.md §2), so the DERIVED board (BOARD.html) kept re-serving finished work.
> _Prior status line, preserved: Status: READY (implemented in worktree; awaiting orchestrator gate + commit)_

**Owner directive (BINDING, 2026-06-28):** the panel canvas/background brown is unappealing.
Go to a **BLACK panel + just a GOLD TRIM**. **No per-panel "X" buttons — every panel uses ONE
consistent Close button.** The chrome is **created ONCE in the presentation layer and reused** by
every panel (DRY) — not re-authored per panel.

**WO #:** placeholder 554 — owner to slot into `MASTER_PIPELINES_BACKLOG_2026-06-06.md` /
`CLI_LANES_WO_NUMBERS.md` (next-free authority is the master doc, NOT the filesystem max).

**Lane:** UI/presentation (DeNelle.Core.UI). §5-clean: kit lives in Core.UI which both HUD and
Village reference; no HUD↔Village edge introduced.

---

## 1. What was built — THE canonical chrome factory

New API in `Assets/_Modules/Core/UI/ElarionUiKit.cs` (DeNelle.Core.UI), the ONE place the panel
chrome is authored:

```csharp
// Tokens
public static readonly Color ObsidianFill = new Color(0.02f, 0.02f, 0.025f, 0.98f); // owner-specified near-black
public static Color ObsidianTrim => /* runic gold, opaque (ElarionUi.Gold) */;
public const float ObsidianTrimPx = 3f;

// Result handle: parent your UI under .content (spans inner black at 0..1, like the old framed panel)
public sealed class PanelChrome { GameObject backdrop, root, content; TextMeshProUGUI title; Button close; }

// THE factory — black fill + gold-trim border + gold header + ONE standard Close button
public static PanelChrome BuildObsidianPanel(Transform parent, string title,
    Vector2 anchorMin, Vector2 anchorMax, Action onClose,
    float headerX0 = 0.06f, float headerX1 = 0.94f, bool withBackdrop = true);

// Whole modal in one call (canvas + scrim + chrome) — for NEW panels
public sealed class ObsidianModal { GameObject canvas; PanelChrome chrome; }
public static ObsidianModal BuildObsidianModal(string name, string title,
    Vector2 anchorMin, Vector2 anchorMax, Action onClose, int sortingOrder = 31000);

// The ONE shared Close button (top-right corner, gold-trim chip, "Close") — replaces per-panel X
public static Button ObsidianCloseButton(Transform parent, Action onClose);
```

Design:
- **Black panel:** `ObsidianFill` (0.02,0.02,0.025,0.98) inner fill.
- **Gold trim:** an outer gold (`ObsidianTrim`) Image with the black fill inset by `ObsidianTrimPx`
  (3px) → reads as a clean gold border. **Sprite-free** (pure tinted quads) so it is identical on
  every target incl. WebGL and can never blank (no brown wood-frame sprite anymore).
- **Gold header:** reuses `ElarionUiKit.Header` (gilt title).
- **ONE Close button:** `ObsidianCloseButton` — top-right corner, consistent everywhere.
- `.content` spans 0..1 over the black fill, so panels' existing fraction-anchored content drops in
  unchanged (near drop-in for the old `PanelFramed` transform).

This SUPERSEDES the per-panel recipe every panel had copy-pasted:
`backdrop + PanelFramed(brown PanelVendor/PanelWindowDark sprite) + dark solidFill + Header + own X/Close`.

---

## 2. Per-panel conversion ledger

| Panel | File | Status | Notes |
|---|---|---|---|
| EquipmentPanel (Gear Preview) | `Assets/_Modules/Village/Hero/EquipmentPanel.cs` (~80) | CONVERTED | corner "X" → shared Close; brown frame → black+gold |
| Inventory | `Assets/_Modules/Village/Hero/InventoryUIBuilder.cs` (~43) | CONVERTED | corner "X" → shared Close; dropped ember-glow + rune-strip + brown frame |
| Shop (vendor) | `Assets/_Modules/Village/Hero/ShopPanel.cs` (~271, ~352) | CONVERTED | footer Close removed; kept subtle per-vendor accent glow over black |
| Party/Gear Shop | `Assets/_Modules/Village/Hero/PartyShopPanelMvvm.cs` (~276, ~368) | CONVERTED | footer Close removed |
| Crafting (Alchemy) | `Assets/_Modules/Village/Items/CraftingPanelMvvm.cs` (~230, ~262) | CONVERTED | footer Close removed |
| Building Upgrade | `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs` (~161, ~213) | CONVERTED | footer Close removed; kept Upgrade CTA |
| Hero Skill Tree | `Assets/_Modules/Village/Talents/HeroSkillTreePanelMvvm.cs` (~421, ~596) | CONVERTED | footer Close removed; kept CONFIRM/Cancel |

For shop/crafting/building/skilltree the **footer "Close" buttons were removed** and replaced by the
single shared top-right Close (satisfies "one consistent Close button"). Their CTAs
(Purchase / Equip / Upgrade / CONFIRM / Cancel) were kept.

### Deferred (UI-Toolkit / UXML — do NOT port to uGUI chrome now)
These are UI-Toolkit (`VisualElement`) panels; converting to the uGUI factory is a structural port:
- `Assets/_Modules/HUD/CosmeticShopPanel.cs`
- `Assets/_Modules/Village/Crafting/VillageCraftingPanel.cs`
- PackStore / BuildMenu (UXML) — same.

**Canon note:** UXML renders empty in player builds (PIPELINE_STATE §8), so these should eventually
become code-built uGUI anyway — at which point they adopt `BuildObsidianPanel`. Tracked as follow-up.

### Minor follow-ups (not in this WO)
- EquipmentPanel's change-DRAWER sub-tray still uses `PanelFramed` (mostly covered by a dark fill).
  Convert to a header/close-less Obsidian fill for full consistency.
- Other secondary trays/wells still pull theme tokens (warm wood). A later lever: flip the
  `UiStyle.Theme` surface tokens (Glass/Cell/StoneNiche) to near-black so even un-converted surfaces
  go dark in ONE place. Held — broad visual change, needs felt-test.

---

## 3. Acceptance criteria
- [x] ONE canonical chrome factory in Core.UI (`BuildObsidianPanel` + `ObsidianCloseButton` + `BuildObsidianModal`).
- [x] Black fill (0.02,0.02,0.025,0.98) + gold trim border + gold header + single Close.
- [x] High-traffic panels converted; per-panel X/footer-Close removed.
- [x] No data/MVVM binding touched — chrome only.
- [x] Brace balance OK on every edited .cs.
- [x] §5 assembly rules respected; LogWarning (never error) on missing sprites (factory is sprite-free).

## 4. Owner decisions to confirm (flagged)
- **Close placement:** currently TOP-RIGHT corner chip labelled "Close". Switch to a footer bar or an
  icon-only mark? It is ONE line in `ObsidianCloseButton` — trivial to change globally.
- **Exact black / gold values:** using owner-specified `0.02,0.02,0.025,0.98` + `ElarionUi.Gold` trim,
  3px border. Tune in the `ObsidianFill` / `ObsidianTrim` / `ObsidianTrimPx` tokens (one place).
