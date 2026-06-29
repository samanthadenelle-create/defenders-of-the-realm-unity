# UI Capture Test Scenarios — Store + Inventory (graphics-enabled headless)

**Goal:** get the **Store** and **Inventory** screens visually correct by capturing REAL screenshots
from an automated graphics-enabled run, reading them back, and iterating against the Blink Obsidian
template (`docs/UI_BLINK_TEMPLATE_CANON.md`). Owner directive 2026-06-28.

> **Why a special harness:** the AutoPilot fleet runs `-nographics` → `break_*.png` are BLANK. Real
> capture requires a **graphics-enabled** player (display/GPU present — the owner's Windows box has one).
> Harness = a `UICaptureMode` that boots, opens each panel deterministically, and calls
> `ScreenCapture.CaptureScreenshot(<path>)`, then quits. CLI builds it; capture runs while the owner sleeps.

## Harness requirements (CLI to build — `Assets/_Modules/.../Diagnostics/UICaptureMode.cs`)
- Boot via a launch flag (e.g. `-uiCapture` or `-bootScene MainCastle_Hall -captureUI`), graphics ON (do NOT pass `-nographics`).
- Drive each scenario below by opening the panel via its controller API (NOT by simulating clicks — deterministic), wait 2 frames for layout, `ScreenCapture.CaptureScreenshot("Builds/UICaps/<name>.png", 1)`.
- Emit a `FlowTrace.Step("UICap", "<name> captured WxH")` per shot so a `-nographics` log still proves it ran.
- Capture at a fixed resolution (e.g. 1920×1080 and a 1080×2340 portrait pass for mobile) so layouts are comparable.
- Exit clean after the last shot.

## SCENARIO SET A — STORE / SHOP
For each: open the panel, capture, then assert the listed oracles (FlowTrace.Fail on miss).

| # | Scenario | Open via | Capture | Oracles (what "working" means) |
|---|----------|----------|---------|--------------------------------|
| A1 | Pack Store — default tab | `PackStore`/store controller open | `store_packs.png` | ≥1 pack row rendered; each row has icon (NOT glyph fallback), title, price, Buy button; chrome = Blink Obsidian frame (black + gold trim); no overlapping/clipped text |
| A2 | Store — icon resolution | same, scroll/all tabs | `store_icons.png` | every pack/item icon resolves to a sprite (WO-542 — confirm no letter-glyph fallbacks); flag any null→glyph |
| A3 | Store — SKR / premium tab (if present) | switch tab | `store_skr.png` | SKR/premium packs list with price + disclaimer; covenant-clean (no combat items) |
| A4 | Store — buy flow modal (stub) | trigger a buy on a free/devnet SKU | `store_buy.png` | confirm/purchase modal renders over the panel; no dead buttons |

## SCENARIO SET B — INVENTORY (post WO-585)
| # | Scenario | Open via | Capture | Oracles |
|---|----------|----------|---------|---------|
| B1 | Inventory — Weapons tab | inventory controller open | `inv_weapons.png` | paper-doll hero renders (live model); item grid shows owned weapons; icons resolve (flag glyph fallbacks per `ItemIconCatalog` null returns) |
| B2 | Inventory — tap select + detail | open, select first item | `inv_select.png` | tapped cell highlights; **detail strip renders** (name/stats + Equip CTA) — proves WO-585 sidebar wiring |
| B3 | Inventory — equip feedback | select then invoke Equip | `inv_equip.png` | Status/toast confirmation visible ("Equipped X") even on re-equip |
| B4 | Inventory — Armor / Accessories / Consumables / Skills tabs | switch each tab | `inv_armor.png`, `inv_acc.png`, `inv_consum.png`, `inv_skills.png` | each tab populates or shows a clean empty-state (no blank grid + no errors); chrome consistent |

## Pass/iterate loop (the actual method)
1. CLI builds `UICaptureMode` → graphics-enabled run → PNGs land in `Builds/UICaps/`.
2. CLI **reads each PNG** and compares to the Blink template + the oracles above.
3. For each miss: RCA from the capture + FlowTrace (data, not guess, §12) → fix → re-capture that one scenario.
4. Repeat until every oracle passes; log what was captured (no silent skips).
5. Known first-target defects to expect: **store/inventory icon glyph-fallbacks** (`ItemIconCatalog.ForWeapon` returns null for wand/censer/staff + unmapped ids; RpgUiCatalog fallback art absent) — fix by adding sliced art or mapping ids to existing sheet sprites.

## Sequencing tonight
- BLOCKED until the WebGL build frees the Unity lock (one editor lock).
- Then: gate Pi → build `UICaptureMode` → run captures → review → iterate Store + Inventory.
- Felt/visual confirmation still finalises with the owner on the morning build; this harness gets them *close* autonomously.
