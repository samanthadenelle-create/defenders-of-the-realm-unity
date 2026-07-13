# Blink + Obsidian UI — Understanding Report (investigation only, 2026-06-28)

> READ-ONLY learning pass. No code changed. Sourced from a 4-way parallel investigation
> (Blink asset structure / Obsidian code integration / MVVM panel usage / web+vendor docs).
> Purpose: be the SME on the Obsidian UI before any enhancement is proposed.

---

## 0. The single most important finding (read first)

**The connected node-graph you showed (right image — nodes joined by lines, rank pips like `3/3`,
a CONFIRM button, left/right pagination) is NOT something the Obsidian kit or RPG Builder ships.**

- Obsidian UI is a **uGUI sprite pack** by **Blink** (the RPG Builder vendor). It provides the *art*
  for a talent tree — `Talent_Tree_Panel.png`, `Talent_Border_1..6.png` (node frames),
  `TalentTree_Decoration_1/2`, and a `TalentTree.prefab` — but **not** a node-graph engine.
- Per Blink's official docs, **RPG Builder's talent tree is a tiered GRID** (horizontal tiers ×
  vertical slots, point-spend, gated by requirement templates) — **no drawn connector lines, no
  per-node rank counters, no pagination** as built-ins.
- Therefore the node-graph **wiring** (curved connector lines + `x/y` rank pips + page arrows + a
  confirm/commit step) is **custom work we build ourselves**, using the Obsidian frame art for the
  look. The kit gives us frames and a panel; it does not give us the graph.

**Open question for the owner (this decides the whole effort):** is that right-image look
(a) the target *aesthetic* you want us to build custom on our existing tree data, or (b) something
you believe is a ready-made prefab/scene inside the kit we can just switch on? Investigation says
it's (a) — custom — but if you saw it in a specific demo scene, point me at it and I'll open it.

---

## 1. Blink architecture summary

- `Assets/Blink/` (~13 GB, **gitignored** — on-disk warehouse, never committed) holds art:
  Characters, Weapons (400 prefabs), Animations, NPCs, Textures, and **`Art/UI/Obsidian_UI/`**.
- **Asset policy (firm):** anything used at runtime must be **copied into `Assets/Resources/` and
  committed** — `Resources.Load` cannot see gitignored Blink. This is why a re-skin = "mirror the
  PNG into `Resources/RpgUi/` then commit."
- Relevant docs already in repo: `docs/BLINK_UI.md` (the re-skin architecture, ACTIVE),
  `docs/WO466_BLINK_UI_FINDINGS_2026-06-16.md`, `docs/BLINK_NOTES.md` (stale), and the design spec
  `docs/UI/OBSIDIAN_UI_DESIGN_skilltree_inventory.md`.

## 2. Obsidian UI — the asset (vendor facts)

- **Name:** "OBSIDIAN UI - RPG / MMORPG / ARPG", by **Blink** (Blink Studios). ~$40, Nov 2021,
  ~34 MB. uGUI sprite pack; pipeline-agnostic (Built-in/URP/HDRP — it's just sprites).
- Store: https://assetstore.unity.com/packages/2d/gui/obsidian-ui-rpg-mmorpg-arpg-206302 ·
  Docs/support: blinkstudios.dev + Discord (no per-sprite manual exists).
- **No published 9-slice border table** — the source of truth is the border values baked into each
  shipped sprite's meta. **No UXML/UI Toolkit anywhere** — it is uGUI sprites only (matches our
  "code-built uGUI, no UXML in builds" canon).
- Ships an optional `*_RPGB` RPG-Builder re-skin unitypackage — **ignore it** (we don't run RPG
  Builder; it only re-skins RPG Builder's own uGUI panels and pulls its types).

### Obsidian sprite catalog (≈290 PNG, 11 categories)
Panels (22: incl. `Talent_Tree_Panel`, Inventory, Merchant, Crafting, Stats, Dialogue…),
Buttons (42: 5 styles × 4 colors + close/slider/toggle), Slots (18: `Inventory_Slot`,
`Armor_Slot`, `Character_Slot`, **`Talent_Border_1..6`**, **`Rarity_1..5`**),
Decoration (38: incl. `TalentTree_Decoration_1/2`), Elements (31), HUD (50: bars, nameplates,
cast bars), Icons (70, ability/class glyphs — **not** gear-item art), Cursors (10), Shapes (9),
Fonts (4 TTF families — need TMP generation), plus 27 sample **prefabs** incl. `TalentTree.prefab`
and an `OBSIDIAN_DEMO.unity` showcase scene.

## 3. How Obsidian is integrated in OUR code (the seam)

A clean **sprite-first, null-safe** chain — every link degrades to a procedural look if art is absent:

1. **`BlinkUiImporter.cs`** (`Assets/Editor/`, menu `Defenders > Art > Import Blink UI Pack`) —
   copies chosen Obsidian PNGs → `Assets/Resources/RpgUi/<role>/<canonical>.png`, forcing
   `Sprite / Single / Uncompressed / FullRect` and a uniform 9-slice border (panels 48, slot 24,
   button 24/12, icons 0). **Only ~35 of 290 sprites are mirrored so far** — and the **talent art
   (`Talent_Border_*`, `Talent_Tree_Panel`, talent decorations) is NOT yet mirrored.**
2. **`RpgUiCatalog.cs`** (`Core/UI`) — lazy-loads `Resources/RpgUi/<role>` via `Resources.LoadAll`,
   maps id→Sprite, returns `null` when missing (never throws). Defines the role/id constants
   (`RolePanel`/`PanelWindowDark`, `RoleSlot`/`slot_item`, `RoleButton`/`button_gold|confirm`,
   `RoleIcons`/`icon_sword|shield|heart`, bars, etc.). Note: **`slot_talent` is referenced by the
   theme but no art is mirrored to it yet.**
3. **`ElarionUiKit.cs`** (`Core/UI`) — the uGUI builder kit (PanelFramed, Well, Niche, Card, Slot,
   ButtonPack, TechGearSocket, ApplyRounded…). Applies 9-slice via `Image.Type.Sliced` when a
   RpgUiCatalog sprite is supplied; otherwise draws a procedural rounded quad. Reads colors from
   `UiStyle.Theme` now (Phase b).
4. **`ConceptIconResolver.cs`** (`Core/UI`) — data-driven concept→icon via
   `Resources/Data/Canonical/concept-icons.json`. Knight ability ids already mapped
   (`knight.ranged-poke→icon_sword`, `knight.mending-salve→icon_heart`, etc.). Null-safe.
5. **`UiStyle.cs`** (`Core/UI`) — the theme singleton (the owner's "one style for everything").
   **Built but Phase-a: not yet consumed by panels.** Exposes `Try(Style.Obsidian)`, `StatePlate`,
   `Frame/Slot/Button/Color/Font/Icon` tokens. `ForObsidian()` currently == `ForDefault()`
   (placeholder), so the lever exists but isn't differentiated yet.
6. **`FeatureFlags.BlinkChrome`** (`ff.blinkchrome`, default OFF) — the gate. OFF = our painted
   chrome (gilt rim/rule/shadow) shows over the sprite; ON = that chrome neutralizes to alpha-0 so
   the bare Obsidian panel reads clean. Branches in ~12 panels; the canonical single read is
   `UiStyle.Chrome`. **The design contract: every screen must look correct in BOTH states.**

**End-to-end:** importer mirrors PNG → `RpgUiCatalog.Get(role,id)` loads it → `ElarionUiKit` puts
it on a 9-sliced `Image` → `BlinkChrome` decides whether our extra dressing layers on top.

## 4. MVVM panel usage — what's solid, what's flat

All target panels are **real MVVM** (pure VM, dumb View, `IPanelView`/`IPanelViewModel`, routed by
`PanelRouter`). Obsidian sprite consumption is **inconsistent** by panel:

| Panel | MVVM | Frame uses Obsidian | "Flat" today | Chrome-gated upgrade? |
|---|---|---|---|---|
| **HeroSkillTreePanelMvvm** (talent) | ✅ | ✅ `PanelWindowDark` | **node cards = `ApplyRounded` (no frame sprite); NO node icons; prereq "lines" = flat thin gold rects; chips/cost = text-only** | ❌ none |
| **HeroLoadoutPanelMvvm** (equip skills Q/W/E/R) | ✅ | ✅ | slot tiles + choice cards = `ApplyRounded`, no icons, text chips | partial |
| **InventoryUIBuilder/Grid** | ✅ (partial) | ✅ | icon wells bare, text chips; **cells already swap to `slot_item` when chrome ON** | ✅ |
| **EquipmentPanel** | ✅ | ✅ `PanelVendor` | minimal — **best example**: sprite-first slots/bars + fallback | ✅ |
| **TalentTreePanel.cs** (OLD) | ❌ UIToolkit, deprecated | — | superseded by HeroSkillTreePanelMvvm; **do not enhance** | — |

So the talent tree's "flat" feel is concrete: **(a)** node plates are procedural rounded quads, not
the Obsidian `Talent_Border_*` frame; **(b)** nodes show no icon (text only) even though
`ConceptIconResolver` could supply them; **(c)** prerequisite connectors are flat 1-px gold
rectangles, not styled/curved edges; **(d)** state (Owned/Unlock/cost) is plain text, no chips.

The VM side is **correct and complete** — `HeroSkillTreeVM` already exposes per-node `IconPath`,
`Prereqs`, `Owned/CanUnlock/LockReason`, `WisdomCost`, `IsCapstone`, `IsEquipped`, `Kind`. **A
restyle needs no VM changes** — only the View paints those existing fields with real sprites.

## 5. Best practices discovered (vendor + general uGUI)

- **Read 9-slice borders off the shipped sprite meta** — Blink publishes no table; don't guess.
- **Image type = Sliced (+ Fill Center)** for panels/buttons; size the RectTransform, never scale the
  sprite (corner distortion). Our importer already sets FullRect + borders.
- **Atlas the icons** (separate atlas from panels) to cut draw calls when many slots instantiate.
- **Keep textures uncompressed** for thin gold borders (compression artifacts show).
- **Fonts need TMP generation** from the TTFs (separate pass; not in a sprite re-skin).
- **Don't write an `ObsidianUiHelper`** — `RpgUiCatalog` + `ElarionUiKit` already ARE that helper
  (project canon). Feed them ids; let them 9-slice.
- **No UXML** — code-built uGUI only (our panels already comply; survives WebGL).

## 6. Current strengths and gaps

**Strengths**
- The sprite-first + null-safe seam is solid and reversible (re-run importer to swap packs).
- Every consumer degrades gracefully → a missing Obsidian sprite can never blank a screen.
- The theme singleton (`UiStyle`) + the design spec already exist — the architecture for "one
  Obsidian style across everything" is laid; it just isn't wired through panels yet.
- VMs are complete and dumb-View-clean; restyles are pure presentation.

**Gaps**
- Talent/slot/decoration Obsidian art **not mirrored** into `Resources/RpgUi` (importer table has no
  `slot_talent`, `panel_talent`, `Rarity_*`, `Armor_Slot` entries yet).
- Talent + loadout node/card frames are procedural, not sprite — the visible "flat".
- `UiStyle` is Phase-a (built, unconsumed); `ForObsidian()` not yet differentiated from default.
- The node-graph UX (lines + rank pips + pagination) has **no art and no engine** — fully custom.
- Icon gaps noted in `BLINK_UI.md` (shield/talk/heart had no clean Obsidian match originally).

## 7. Safe enhancement directions (NO code proposed — for a future session)

Two distinct paths, very different in size. Both keep the VM untouched and stay null-safe.

**Path A — Obsidian *reskin* of the existing tree (matches the kit + the existing spec; lower risk).**
Mirror `Talent_Border_1..6` → `slot_talent[_1..6]`, `Talent_Tree_Panel` → `panel_talent`, talent
decorations, and `Rarity_*` via `BlinkUiImporter.BuildTable()`; then the View paints node plates with
`slot_talent` (state-tinted), adds node icons via `ConceptIconResolver`, and styles the connectors.
This is the spec in `OBSIDIAN_UI_DESIGN_skilltree_inventory.md` and removes the "flat" feel while
keeping today's tiered grid geometry. Verify in BOTH `ff.blinkchrome` states.

**Path B — custom *node-graph* tree (matches the right image; larger, custom).**
Everything in A **plus** a new graph layout: curved/edged connector sprites between prereq→node,
per-node rank pips (`x/y`), a confirm/commit step, and pagination between branches/trees. No kit art
exists for the edges or pips → custom art + custom layout code. Bigger build; should be its own WO
with owner sign-off on the exact layout, since the kit does not provide it.

**Recommended sequencing (when the owner greenlights building):** do Path A first (fast, banks the
Obsidian look, low risk), felt-verify, then decide whether Path B's graph is worth the custom work —
possibly routed through `UiStyle.Try(Style.Obsidian)` so the whole UI moves together.

## 8. Gotchas / limitations to respect
- Blink is gitignored — mirror+commit anything used at runtime (else it's absent in clean builds).
- Two states always: `ff.blinkchrome` ON and OFF must both look right (existing contract).
- The old `TalentTreePanel.cs` (UIToolkit) is deprecated — don't enhance it; HeroSkillTreePanelMvvm
  owns `PanelId.HeroTalents`.
- Talent "Owned everything / no Wisdom charged / all capstones" seen in felt-test = **stale
  `dotr-talents-v1` PlayerPrefs save**, not a code bug (`WisdomCurrencyService` enforces cost +
  single-capstone on a clean state).
- No vendor 9-slice numbers — read borders from the sprite meta when mirroring new art.

---

*Confidence: high on our code seam + the asset facts; the one item to confirm with the owner is the
provenance of the right-image node-graph (custom build vs a demo prefab to point me at).*
