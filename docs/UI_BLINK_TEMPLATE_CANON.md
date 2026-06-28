# UI / Blink Obsidian Template Canon (BINDING)

**Status:** Canonical, owner-ratified 2026-06-28. Read before touching ANY in-game UI.
**Owner framing:** "you create a template of [screen], all use [that template], then add styling
and logic to fill with your conditions (model) … one master frame to create those structures
which fill elements to their styled areas." This doc is that formula, made explicit.

---

## 0. The one-line rule

**The Blink frame IS the chrome. Screens NEVER restyle — they DROP chrome-less content into the
frame's pre-styled drop-zones and bind their model.** One master factory builds every panel.

---

## 1. Framework decision (settled — do not relitigate)

- **Code-built uGUI only.** Unity's own 6000.x manual: *runtime UI → uGUI recommended; UI Toolkit =
  "alternative, in active development."* The Blink "OBSIDIAN UI" pack is a **2D sprite/uGUI** pack.
- **Do NOT use UI Toolkit / UXML / USS at runtime.** That is the documented §8 landmine
  ("UXML doesn't render in player builds") — it is UI Toolkit's runtime PanelSettings/theme
  requirement biting us. Settled by reading the docs; don't re-try it.

## 2. The master factory

`DeNelle.Core.UI.ElarionUiKit.BuildObsidianPanel(parent, title, min, max, onClose, …, frameName)`
(and `BuildObsidianModal(..., frameName)`) is the **ONE** entry point for every panel.

- Pass a `frameName` (a `RpgUiCatalog.Frame*` id). When the mirrored art resolves, the panel renders
  the **real ornate Blink frame sprite** and returns its **drop-zones** in `chrome.layout`.
- Pass no frame → the procedural black+gold panel (the safe fallback / "make our own in C#").
- `chrome.content` = transparent 0..1 overlay (legacy fraction layouts still work).
- `chrome.title` (in the header zone) + `chrome.close` (the ONE shared Close) are pre-built.

## 3. The drop-zones (`ElarionUiKit.FrameLayout`)

Returned as `chrome.layout` when a frame is used. Parent your content to these — they are
transparent, pre-positioned RectTransforms measured from the frame art:

| zone | what goes there |
|---|---|
| `layout.header` | title / screen name (already holds `chrome.title`) |
| `layout.body`   | the main content well — grid / list / tree / detail |
| `layout.medallion` | circular portrait socket (top-left), may be null |
| `layout.footer` | wallet / action strip along the base, may be null |

Zones are defined **once per frame** in `ElarionUiKit.ZonesFor(frameName)` (fractions, so they
survive any stretch). To fit a new frame: add a `case` with its measured rects. **This is the only
place you tune layout — never per screen.**

## 4. Recipe — building or restyling ANY screen

```csharp
var chrome = ElarionUiKit.BuildObsidianPanel(parent, "CRAFTING", min, max, OnClose,
                 frameName: RpgUiCatalog.FrameCrafting);
BuildRecipeList(chrome.layout.body);     // drop CHROME-LESS content into the zone
BindModel(chrome.layout);                // fill with your conditions (the VM/model)
```

Rules:
- **No per-screen chrome.** No cards/wells/rims/footers of your own — the frame supplies all of it.
  If an old sub-panel double-frames, make it a **transparent layout host** (see §6).
- **Content fits the zone**, not the full panel rect (fractions inside `layout.body`, not 0..1 of
  the panel — those overlap the frame's ornate border).
- **Slots use the real Blink slot art**: `RpgUiCatalog.Get(RoleSlot, SlotItem)` (Inventory_Slot),
  `SlotArmor`, `SlotCharacter`, rarity_1..5, talent_1..4. Empty slots still draw the frame so a
  grid/tree reads as a grid/tree even when sparse.

## 5. The art pipeline (gitignore-safe)

`Assets/Blink` is **gitignored** — never reference it directly (breaks fresh clone / CI / WebGL).
`Assets/Editor/RpgUiImporter.cs` **mirrors** the needed Blink PNGs into **committed**
`Assets/Resources/RpgUi/{frame,silhouette,slot,button}` (`CopyAsset` = fresh GUID, no clash), forcing
Sprite import (9-slice for slots/buttons; Simple for full panels). `RpgUiCatalog` loads them WebGL-safe.

- To add a Blink sprite: add an `Entry { Root = BlinkRoot, Src = "…", Role = "…", Name = "…" }` to
  `RpgUiImporter.BuildEntryTable()`, run `Defenders/Art/Import RPG UI Pack`
  (`run-unity-method DeNelle.Editor.RpgUiImporter.Run`), commit the new `Resources/RpgUi/...` PNGs.
- **Always sprite-FIRST with a null fallback** — every `RpgUiCatalog.Get` returns null when art is
  absent; the caller keeps a procedural fallback so a panel can never blank.

## 6. Frame catalog (which frame per screen-type)

| screen | frame id | Blink art |
|---|---|---|
| Inventory | `FrameInventory` | Inventory_Panel |
| Character / Gear (paper-doll) | `FrameCharacter` | Stats_Panel |
| Crafting / Alchemy / Jeweler | `FrameCrafting` | Crafting_Panel |
| Skill / Talent tree | `FrameTalent` | Talent_Tree_Panel |
| Shops (vendor / gear) | `FrameMerchant` | Merchant_Panel |
| Dialogue | `FrameDialogue` | Dialogue_Panel (portrait socket + body) |
| Rumor / quest board | `FrameQuest` | Quest_Log_Panel |
| generic | `FrameCore` | Core_Panel |

Silhouettes for paper-dolls: `RpgUiCatalog.Get(RoleSilhouette, SilMale/SilFemale/SilPet)`.

## 7. Verify by screenshot-vs-template (owner method, BINDING)

After building/restyling a screen, capture it and **compare to its Blink template PNG**
(`Assets/Blink/Art/UI/Obsidian_UI/Panels_Obsidian/*.png`). The fleet is `-nographics` (blank shots) —
use a graphics-enabled capture (built player) or the owner's F8. Tune the frame's zones in
`ZonesFor` (one place), never per screen. The owner is never the bug detector.

---

## 8. Worked example — dialogue (the reference implementation)

`DeNelle.HUD.DialogueView` builds the whole dialogue strip from the factory:
`BuildObsidianPanel(…, frameName: FrameDialogue)` → speaker → `layout.header`, body+tap →
`layout.body`, speaker portrait → `layout.medallion`. The view re-styles nothing; it binds the
`DialogueViewModel`. Copy this shape for any new screen.

**This formula is to be reused for every UI surface, forever. New screens are trivial: grab the
frame, fill the zones, bind the model.**
