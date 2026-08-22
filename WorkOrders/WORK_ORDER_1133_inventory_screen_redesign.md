**Status:** READY TO IMPLEMENT — the design pass is DELIVERED (see "THE DESIGN" below, added 2026-08-21 by the UI seat). CLI implements from it.

# WORK ORDER 1133 — Inventory screen: redesign the Bag, and justify the gear view or cut it

**Minted:** 2026-08-21 (CLI seat, banner bumped 1133 -> 1134 in the SAME edit)
**Assigned:** UI seat (Claude UI) for the DESIGN. CLI implements from the ratified design.
**Class:** DESIGN / redesign. Not a defect list — the screen works, it does not *serve*.
**Evidence:** `docs/ui-evidence/2026-08-21_inventory_weapons_seeker.png` — captured live off
the owner's Solana Seeker, Grom Ironhand, landscape 2670x1200.

> **THIS IS THE SCREEN YOU GET WHEN OPENING THE ARMOR / GEAR VIEW** (owner, 2026-08-21).
> That matters for the design: it is not only a bag listing, it is the surface a player
> reaches when they set out to answer *"what am I wearing, and should I change it?"* The
> capture happens to show the Weapons tab, but the entry intent is gear.

> **ALL BROKEN ELEMENTS FOLD INTO THIS TICKET** (owner ruling: *"broken folds into that
> other ticket"*). Do NOT mint separate defect tickets for the empty preview box, the
> clipped tab label, the sub-floor touch targets or the bleed-through. They are listed
> below as design inputs, and they are fixed BY the redesign — not patched around it and
> not tracked in parallel, which would produce two competing sources of truth for one
> screen.

---

## THE OWNER'S VERDICT (2026-08-21, verbal, this session)

> "there is no benefit to the gear view like it is, and opening isnt much better"

Read that twice before designing anything. She is not asking for the same screen with
tidier spacing. She is saying **the screen does not pay for the tap that opens it.** A
redesign that fixes the defects below and keeps the shape would still fail her test.

**The question this WO must answer: what does a player LEARN or DECIDE here that they
could not before opening it?** If the honest answer is "nothing", the right outcome may be
to fold inventory into another surface and delete this one. That is an allowed outcome and
should be proposed if the evidence supports it.

---

## WHAT IS ON SCREEN NOW (observed, not inferred)

Left: a gold hero card overlapping the panel's own ornate frame — an EMPTY dark preview
box above a `VIEW GEAR` button, then `Grom Ironhand / KNIGHT / LV 4`, then HP `123/175`,
mana `12/12`, and an unlabelled purple XP bar.
Centre-top: `INVENTORY` title, then five tabs — Weapons (selected), Armor, Trinkets,
Potions, Skills.
Centre: a ~5x5 item grid holding TWO tiles, each with a tiny letter (`U`, `C`).
Below: a full-width gold hint bar, `Tap an item to inspect it.`
Bottom-right: currency strip — `1230` gold, `291` crystals, `0` (flask).
Bottom-centre: `Close`.

---

## THE CONCRETE PROBLEMS (each is visible in the capture)

1. **~40% of the content area is dead black.** The grid sits right-of-centre with a void to
   its left. Two items float in a five-column grid on a 2670px-wide screen.
2. **The selected-tab treatment eats its own label.** The orange chevron on `Weapons`
   overlaps the word — the trailing letters are obscured. The selected state is destroying
   the thing it selects.
3. **The hero preview box is EMPTY.** A flat dark rectangle where a hero should be. This is
   not a mystery: WO-592 (embedded orbit-rotatable hero viewport) was never built, and F8
   seq 2833 recorded `[Flow:Equip] RT PROBE: the preview render texture is a UNIFORM clear
   colour`. **An empty box is worse than no box** — it reads as broken, and it is the single
   biggest reason the gear view "has no benefit".
4. **Item tiles are far below the touch floor.** Tiny tiles with 1-character rarity letters
   (`U`, `C`) that are near-illegible at arm's length. Canon floor is `MinTouchPx = 112`.
5. **The hint bar outweighs the content.** A full-width gold band saying "Tap an item to
   inspect it" is visually louder than the two items it describes.
6. **The currency strip is orphaned** bottom-right, colliding with the frame and unrelated
   to anything else on the panel.
7. **The hero card breaks the frame**, overlapping the ornate border rather than sitting
   inside the layout.
8. **Background HUD bleeds through** — `2/6` (Echoes) is visible at the right edge behind
   the panel.
9. **Frame vs content mismatch** — heavy ornate grey chrome around flat black content; it
   does not read as the Obsidian kit used elsewhere.

---

## HARD CONSTRAINTS (non-negotiable — a design that breaks these cannot ship)

- **UXML DOES NOT WORK IN PLAYER BUILDS.** Code-built uGUI only. Do not design anything that
  implies a UIDocument.
- **The owner is RED/GREEN COLOURBLIND.** Rarity, state and quality must NEVER be carried by
  colour alone — pair with shape, glyph, border weight or a word. `U`/`C` letters are the
  right instinct; they are just too small.
- **`MinTouchPx = 112`** for every interactive element.
- **Landscape.** The build is landscape-locked (`allowedAutorotateToPortrait: 0`).
- **TMP strings ASCII-only** — non-ASCII renders as tofu.
- Player-facing sentences come from `canon-strings.json` (CLAUDE.md §7), never typed inline.
- Bag reaches this screen from the SIX-face action bar (Build, Talk, Bag, Raids, Quests,
  Manage). Do not propose a seventh face.
- ⚠ There is a **feature-flagged-off Map tab** inside Bag (`FeatureFlags.MapTab`), OFF
  because realm travel is a WO-827 stub. Any tab-row redesign must not assume it away, and
  must not turn it on.

---

## WHAT THE DESIGN MUST DELIVER

1. **A stated purpose.** One sentence: what this screen is FOR. Everything else justifies
   itself against that sentence.
2. **The gear-view decision, argued.** Either:
   (a) make the hero preview genuinely useful — see the equipped set at a glance, compare a
       candidate against what is worn, understand what a swap changes; or
   (b) **cut it** and say what replaces the value it was supposed to add.
   ⛔ Do not specify a live 3D orbit viewport without pricing it: `SeatingEditorOverlay`
   already exists at 745 lines with no such viewer, and the RT probe above shows the render
   texture path is currently blank. If the design needs a rendered hero, it must name how
   that render is produced and why it will not be an empty box again.
3. **A layout that uses the screen.** Landscape, no dead 40%.
4. **Comparison, not just enumeration.** The player's real question at a weapon tile is
   *"is this better than what I have?"* — a grid of icons cannot answer it. Stat deltas
   against the equipped item are the highest-value thing this screen could add, and the
   repo already carries the pattern (`PartyShopPanelMvvm` preview pane, WO-486/501).
5. **Empty-state design.** Two items in twenty-five slots is the NORMAL early-game case, not
   an edge case. Design for a nearly-empty bag deliberately.
6. **Rarity that survives greyscale.** `U`/`C` at a legible size, or shape/border.
7. Mockups or precise geometry the CLI can build without inventing values.

---

## OUT OF SCOPE

- The equip/stat SYSTEM. This is presentation. If the design needs a stat the model does not
  expose, name it and it becomes a separate CLI ticket.
- `WO-1050` (The Night Market — Realm Store presentation) is the sibling storefront redesign;
  keep the two visually coherent but do not merge them.
- The Skills tab's talent tree (WO-1021 shipped its parity pass).

## ACCEPTANCE

The owner opens Bag on the Seeker and can answer, without being told:
**"what do I have, what is worn, and is this one better?"** — and the screen has earned the
tap that opened it.

Captures required before it is called done (greyscale pass included): the near-empty bag,
a bag with 20+ items, and each tab.

---

---

# THE DESIGN — delivered 2026-08-21 (UI seat)

**Interactive wireframe + full brief:** https://claude.ai/code/artifact/5aebbd7f-9cdb-4883-9f1a-88ab812348dd
**Name:** The Armory Rail. **Sibling:** WO-1050 (The Night Market) — same four-light palette and
greyscale ladder so the two screens read as one game; layouts stay separate, per this WO's scope rule.

## D0. The stated purpose (the one sentence everything justifies itself against)

> **This screen tells you what you are carrying, what you are wearing, whether a thing is better
> than the thing it would replace — and where else you can go from here.**

The last clause is the owner's steer of 2026-08-21, verbatim: *"we need a clean way to display and
let the player know how to intuitively get to each next section."* It is not a nice-to-have bolted
onto a bag redesign; it is the failure the screen is actually exhibiting, and it drives the layout.

## D1. THE FINDING THAT SETTLES THE GEAR-VIEW QUESTION — read before designing or building anything

**The good gear screen ALREADY EXISTS, and the bag has a hidden door to it.**

| Verified at source | Where |
|---|---|
| A large live 3D hero with equipped gear visible, framed by labelled Obsidian slot plates — Full Armor Set, Shield (Off Hand), Weapon (Main Hand), Amulet, Ring — with a per-slot drawer of compatible owned items | `EquipmentPanel.cs` (1,452 lines, bound MVVM over `EquipVM`) |
| The render rig that produces it: clones the actor, strips gameplay components, mirrors weapon + shield + armour tier through a real `EquipmentController`, disposes cleanly | `HeroPreviewViewer.cs` (570 lines) |
| Called from **six** sites | EquipmentPanel, PartyShopPanelMvvm, BuildingUpgradePanelMvvm, BuildPreviewModal, InventoryPaperDoll, MotionCasterWindow |
| The bag's gold `VIEW GEAR` ribbon | `InventoryPaperDoll.cs:99-110` |
| ...which routes to that very panel | `InventoryUIBuilder.cs:336` — `PanelRouter.Open(PanelId.EquipmentPanel)` |

**So the empty navy rectangle the owner is looking at is a preview box sitting directly above a
button that opens the real preview.**

### The answer to (a) make the gear view useful / (b) cut it

**Neither — PROMOTE it.** The gear view becomes **rail entry one**, reachable in one tap from
anywhere in the bag, through the same `PanelRouter` call that exists today. What gets cut is the
**empty box and the ribbon** — the door, not the room.

**This also discharges the pricing requirement.** The WO forbids specifying a live 3D orbit viewport
without naming how the render is produced. **No new viewport is specified.** The render is
`HeroPreviewViewer`, already built, already proven at five other call sites.

### ✅ ANSWERED 2026-08-22 by F8 seq=3585 — EquipmentPanel's preview is ALSO blank

D1 asked which of the two preview paths actually produces pixels. **The probe fired on the
EquipmentPanel path**, so the question is closed and the answer is the bad one:

```
HeroPreviewViewer:ProbeRenderedContent   (HeroPreviewViewer.cs:411)
EquipmentPanel:BeginOrRetargetPreview    (EquipmentPanel.cs:1243)
EquipmentPanel:RenderPreview -> Render -> Bind -> Open
HeroInventoryController:OpenGearPreview  (InventoryUIBuilder.cs:341)   <- the VIEW GEAR ribbon
```

> *"RT PROBE: the preview render texture is a UNIFORM clear colour — the preview box is blank at the
> SOURCE, not at the panel. Fix the model/culling, not the RawImage."*

**What this does and does not change:**

- **Does NOT change the design.** Promoting Gear to rail entry one is still right: the room is better
  than the door either way, and `EquipmentPanel` remains the only surface with worn-slot plates and a
  per-slot drawer.
- **DOES block the Gear section** exactly as D1 predicted. Rail entries 2-7, the stage, the compare
  pane and every other part of this ticket are **unblocked and can proceed**.
- **The blank RT is a SEPARATE defect** — filed as **WO-1059**. Do not attempt to fix it inside this
  redesign; the probe's own text points at the model/culling, not at any panel this WO touches.
- ⛔ **Do not ship the Gear section over a blank box.** A second empty preview would be worse than
  the first, which is the whole reason D1 exists.

### The one thing to instrument BEFORE any layout work (CLAUDE.md §12)

F8 seq 2833 probed the **paper-doll's** render texture as a uniform clear colour. That is
`InventoryPaperDoll`'s path, **not** `EquipmentPanel`'s. Probe **both** call sites and capture which
one produces pixels before touching layout. If `EquipmentPanel`'s preview is also blank on device
that is a **separate CLI defect**, and it blocks the Gear section only — not the rest of the screen.
**No edit until a captured line names the cause.** A second empty box would be worse than the first.

## D2. The navigation model — the top tab strip becomes a LEFT RAIL

In landscape this is not a style preference. It is the only shape that fits the sections without
clipping, sits under the left thumb, and fills the exact band that is dead black today — so the fix
to navigation is also the fix to the wasted 40%.

| What a rail buys | Why tabs cannot |
|---|---|
| Labels never clip | A vertical entry owns the full rail width; no selected-state chevron can eat its own word (problem 2) |
| **Counts fit** | Each entry carries its item count. The player sees Armor is empty and Weapons has 2 **before** tapping — the reason to go, ahead of going |
| Gear joins the model | Rail entry one instead of a ribbon painted on a card. Always visible, never hidden (problem 3) |
| It scales | Six sections today, seven when `FeatureFlags.MapTab` ships. A top strip at this width already cannot |
| It fills the void | Occupies the left band that is currently black (problem 1) |

**Three more wayfinding devices, none of them a tutorial:**

1. The pane footer always names the next step in plain words — *"Armor is empty — the Armorer sells plate."*
2. An empty section says **what fills it**, never shows nothing.
3. The rail mark stays put while the stage scrolls, so "where am I" never leaves the screen.

## D3. Geometry — exact values, so nothing is invented at build time

Canvas **2670 x 1200** landscape. Three zones, summing exactly to width.

| Zone | Size | Holds |
|---|---:|---|
| Header bar | full width x **120** | hero name, class/level, HP/MP/XP vitals, Close (top-right `button_exit`) |
| **Rail** | **374** wide (14%) | 7 entries, each **374 x 132**, 8 px gap, 2 separators |
| **Stage** | **1496** wide (56%) | the selected section |
| **Pane** | **800** wide (30%) | detail / compare, always present |
| Purse strip | full width x **84** | gold / crystals / flasks + the next-step hint |

- Body height = `1200 - 120 - 84` = **996**. Rail content = `7 x 132 + gaps + 2 separators` is about 980. Fits.
- **Grid:** stage padding 28 each side gives 1440 usable. **6 columns**, 16 px gaps, so cell = **226 x 226**.
  Rows scroll. This replaces the literal `new Vector2(78f, 72f)` at `InventoryGrid.cs:83`.
- **Every interactive element is authored above `MinTouchPx = 112` on its short side**, so
  `ClampMinTouch` is a **no-op**. Do not rely on the clamp being kind — a sub-floor element inflates
  and stacks into its neighbour, which is the 2026-07-16 grey-plate defect class.

### Gear section (stage contents when rail entry one is active)

Left: hero niche (`slot_character`) holding the `HeroPreviewViewer` RawImage, full stage height.
Right: a column of five worn slots — Main Hand, Off Hand, Armour, Amulet, Ring — each a `slot_armor`
plate with an icon, an uppercase slot key and the worn item's name; vacant reads *"empty"* in italic
dim, never a blank plate.

### Pane states

- **Nothing selected:** what is worn, the aggregate stat contribution, and the highest-value gap
  (*"Two slots are empty. An amulet and a ring are the cheapest points of defence you can add."*).
- **Item selected:** three columns — **Worn | delta | This** — one row per stat, then a one-line
  plain-words verdict (*"Hits harder, swings slower."*), then the action button and a line naming
  what the action replaces.

## D4. Obsidian / Blink mapping — every id VERIFIED present on disk

**The 2026-06-27 spec's "art not yet mirrored" list is STALE — it has since been imported.**
Globbed `Assets/Resources/RpgUi/` this session; the two paper-doll plates this design needs are there.

| Region | Builder | Sprite id | Note |
|---|---|---|---|
| Window frame | `ElarionUiKit.PanelFramed` | `panel_window_dark` | already correct — keep. Replaces the grey ornate chrome (problem 9) |
| Rail ground | `ElarionUiKit.Well` | `panel_grid` | reads as a carved niche column |
| Rail entry | `ElarionUiKit.Slot` | `slot_action` | on disk. Selected = plate + 3 px mark, never colour alone |
| Grid cell | `ElarionUiKit.Slot` | `slot_item` | keep the existing cell path; change only the SIZE |
| Rarity frame | overlay on the cell | `rarity_1`..`rarity_5` | **all five on disk**. Border weight escalates with tier |
| Worn-gear slot | `ElarionUiKit.Slot` | `slot_armor`, `slot_armor_2` | **mirrored since the spec** |
| Hero niche | `ElarionUiKit.Niche` | `slot_character` | **mirrored**. Frames the RawImage |
| Live hero | `HeroPreviewViewer` | RenderTexture to RawImage | the existing rig — see D1 |
| Primary action | `ElarionUiKit.ButtonPack` | `button_gold` / `button_confirm` | ROLE, not sprite — the chrome branch resolves it |
| Close | `ElarionUiKit.ButtonPack` | `button_exit` | top-right X; kills the bottom-centre button colliding with the frame ornament |
| Item icons | `ConceptIconResolver.ResolveAny` | `icon_sword`, `icon_shield`... | keys, never sprites, in the View |

**Still to mirror (both fall back cleanly until then — not a blocker):** `icon_helmet`,
`icon_spellbook` from `Icons_Obsidian/`, via `BlinkUiImporter.BuildTable()` at icon border 0.

**Do not hand-roll 9-slice and do not write an `ObsidianUiHelper`** — `RpgUiCatalog` +
`ElarionUiKit` ARE that helper. Feed the kit ids; it applies `Image.Type.Sliced` with a procedural
fallback on null.

**Style comes from ONE authority.** Every value above is requested as a semantic token —
`UiStyle.Frame.Window`, `UiStyle.Slot(state)`, `UiStyle.Button(role)`, `UiStyle.StatePlate(state)` —
per the owner's singleton directive (`docs/UI/OBSIDIAN_UI_DESIGN_skilltree_inventory.md` §6). No raw
hex in the View, no `RpgUiCatalog.PanelX` named at a call site, and **the `ff.blinkchrome` branch
lives in exactly one place** instead of the four it is spread across today
(`HeroSkillTreePanelMvvm.cs:165,198`; `InventoryUIBuilder.cs:52`; `InventoryGrid.cs:253`).
**Verify in BOTH flag states** — the existing `InventoryUIBuilder.cs:52` alpha-neutralisation handles it.

## D5. Colour never carries meaning (owner is red/green colourblind)

Four lights, luminance-stepped, shared with WO-1050: **gold 195 / verdant 177 / ember 145 / aether
113** (rec.709 of 255). Ground is violet-biased near-black (`#0A0810` / `#16111F`), not neutral grey.

| Thing | Encoded as | NOT as |
|---|---|---|
| Rarity | letter (`U`/`C`) at legible size **plus `rarity_n` border weight** | a colour tint. The letters were the right instinct and only too small (problem 4) |
| Stat delta | glyph + sign + number: up +3, down -0.2, or a dash | a green number beside a red one |
| Worn | the **word** `WORN` plus a border | a green tint |
| Selected | border + 3 px rail mark + the pane changing | colour alone |
| Vitals | one header row, luminance-separated | the saturated green/blue/magenta bars (problem 9) — those go |

**The greyscale pass is the gate.** Strip hue: every state must still be readable from its word or
its shape. Do not ask the owner to approve hues; ask about behaviour.

## D6. Touch

- **Every target at least 112 px on its short side** (D3). Cells 226, rail entries 374 x 132.
- **Thumb zones:** landscape thumbs rest bottom-left and bottom-right — rail under the left, the
  pane's action button under the right, grid in the middle where a finger sweeps.
- **No hover states.** Selected / worn / vacant are the only states, and each is drawn.
- **Nothing critical in the top corners** except Close, which belongs there.

## D7. Answers to "WHAT THE DESIGN MUST DELIVER", item by item

| # | Required | Answered in |
|---:|---|---|
| 1 | A stated purpose | **D0** |
| 2 | The gear-view decision, argued + the render priced | **D1** — promote, not cut; render is the existing `HeroPreviewViewer` |
| 3 | A layout that uses the screen | **D3** — 374 / 1496 / 800, no dead band |
| 4 | Comparison, not enumeration | **D3 pane states** — Worn / delta / This |
| 5 | Empty-state design | **D8** — the normal case, designed and captured FIRST |
| 6 | Rarity that survives greyscale | **D5** |
| 7 | Mockups / precise geometry | **D3** plus the interactive wireframe linked above |
| + | Exact player-facing strings with key names | **D9** — flat camelCase keys, ASCII-only, both canon-strings copies |

## D8. Build order (roughly half of this ticket is REMOVAL)

1. **Instrument both preview paths and capture** (D1). Nothing else starts until this reads.
2. **Delete:** the empty preview box + the `VIEW GEAR` ribbon (`InventoryPaperDoll.cs:99-110`), the
   second hero card, the full-width hint bar, the bottom-centre Close.
3. **Rail** replaces the tab row (`InventoryUIBuilder.cs:185-225`, which hardcodes an inactive colour
   and a `Resources.Load` path). Entries bind `vm.Tabs` — `.Label` and `.Count` **both already exist**
   on `InventoryTab` — plus Gear and Map. **Do not turn `FeatureFlags.MapTab` on**; render it dimmed
   and inert.
4. **Stage** — grid resize + carved empty cells; Gear section hosts niche + worn slots.
5. **Pane** binds `vm.Selected` (`InventoryDetail`: Name / Stats / Rarity / CanEquip / CanUse — all
   present today).
6. **Empty states + greyscale captures.**

### The one thing the model does not expose (a SEPARATE CLI ticket, per this WO's OUT OF SCOPE rule)

The delta column needs **the equipped item's stats alongside the candidate's**. `InventoryVM` already
resolves `equippedId` at `:451` (weapons) and `:482` (armor), so the seam exists — but exposing a
comparison on `InventoryDetail` is a MODEL change. **Name it, do not assume it.** Until it ships, the
pane renders the candidate's stats and the verdict line, with the delta column absent (never faked).

### Captures required before done

Near-empty bag / a bag with 20+ items / **every rail section** / **both `ff.blinkchrome` states** /
**a greyscale pass**. Open the PNGs — compile-green never proved a panel looked right.

### Acceptance, extended

The ticket's test stands — *what do I have, what is worn, is this one better?* — **plus the owner's
steer: what else is in here, and how do I get to it?** The rail answers that last one **before she
taps anything**, which is the point of the redesign.

---

## D9. String table — exact keys and exact text (added for the implementing seat)

Every player-facing sentence on this screen, with the key it goes in. **ASCII-only** (non-ASCII
renders as tofu in TMP). Keys follow the file's existing convention: **flat camelCase, no dot
namespacing** — verified against the 133 live keys in `canon-strings.json`.

⛔ **Both copies, byte-identical:** `Assets/Resources/Data/Canonical/canon-strings.json` **and**
`Assets/StreamingAssets/Data/Canonical/canon-strings.json`.

### Rail entries

| Key | Text |
|---|---|
| `invRailGear` | `Gear` |
| `invRailWeapons` | `Weapons` |
| `invRailArmor` | `Armor` |
| `invRailTrinkets` | `Trinkets` |
| `invRailPotions` | `Potions` |
| `invRailSkills` | `Skills` |
| `invRailMap` | `Map` |
| `invRailMapSoon` | `soon` |
| `invRailWorn` | `worn` |
| `invRailHeader` | `Sections` |

### Worn-slot keys (Gear section)

| Key | Text |
|---|---|
| `invSlotMainHand` | `Main Hand` |
| `invSlotOffHand` | `Off Hand` |
| `invSlotArmor` | `Armor` |
| `invSlotAmulet` | `Amulet` |
| `invSlotRing` | `Ring` |
| `invSlotEmpty` | `empty` |

### Empty-section lines (the NORMAL early-game case)

Each names **what fills it**, never shows nothing.

| Key | Text |
|---|---|
| `invEmptyWeapons` | `Nothing here yet. The Market sells blades, and Hollow raiders drop them.` |
| `invEmptyArmor` | `Nothing here yet. The Armorer in town sells plate, and Hollow captains drop it.` |
| `invEmptyTrinkets` | `Nothing here yet. Trinkets come from dungeon chests below the Ember Deep.` |
| `invEmptyPotions` | `Nothing here yet. Brew flasks at the Healer's Cottage, or buy them at the Market.` |
| `invEmptySkills` | `The Knight tree opens here. Ranged, Heal and Sustain, Control.` |
| `invEmptyMapLocked` | `Realm travel is still being built. This section stays visible so it is never a surprise when it opens.` |

### Pane — nothing selected

| Key | Text |
|---|---|
| `invPaneNoSelection` | `Nothing selected` |
| `invPaneGearGaps` | `Two slots are empty. An amulet and a ring are the cheapest points of defence you can add.` |
| `invPaneNothingToCompare` | `There is nothing here to compare yet.` |

⚠ `invPaneGearGaps` is written for the two-empty-slot case. **If the count is dynamic, this becomes
a format string with one `{0}` and the slot names composed by the VM** — that is an implementing
call, not a design call. Flagging it rather than pretending one sentence covers every state.

### Pane — item selected

| Key | Text |
|---|---|
| `invPaneColumnWorn` | `Worn` |
| `invPaneColumnThis` | `This` |
| `invPaneWornBadge` | `WORN` |
| `invActionEquip` | `Equip` |
| `invActionUse` | `Use` |
| `invActionWorn` | `Worn` |
| `invActionGoTo` | `Go to {0}` |
| `invNextReplaces` | `replaces {0}` |
| `invNextCompareHint` | `tap another item to compare it` |
| `invNextRailHint` | `the rail keeps every section one tap away` |
| `invNextCountHint` | `sections with items show a count on the rail` |

### Verdict lines (the one-line plain-words judgement under the deltas)

These are **per-comparison**, so they are composed, not stored whole. The composed shape:

| Key | Text |
|---|---|
| `invVerdictTradeoff` | `{0}, {1}.` |
| `invVerdictBetter` | `Better than what you are carrying.` |
| `invVerdictWorse` | `Worse than what you are carrying.` |
| `invVerdictSame` | `No change to your stats.` |
| `invVerdictWearing` | `This is what you are carrying now.` |

Example composition: `invVerdictTradeoff` with `Hits harder` + `swings slower` gives
*"Hits harder, swings slower."* The clause fragments come from the stat deltas, not from a
hand-written sentence per item.

### Purse strip

| Key | Text |
|---|---|
| `invPurseGold` | `GOLD` |
| `invPurseCrystals` | `CRYSTALS` |
| `invPurseFlasks` | `FLASKS` |

The right-hand hint on the purse strip **reuses the `invEmpty*` line** for the emptiest section —
one string, two placements, so the wording can never drift between them.

### Not a string

The rarity marks (`U`, `C`, and the rest of the tier letters) are **not** canon-strings rows — they
are single-glyph tier codes drawn from the item's own rarity field, and translating them would break
the greyscale read they exist for.

---

## RELATED FINDING, recorded here so it is not lost

**No NPC at the armorer.** The owner also reported *"no npc for armor"* on the same
session. Checked at source: `structures-catalog.json` gives **every** vendor storefront
`npcModel: None` — market, lumbermill, forge, armorer, jeweler, collector_* alike, so the
armorer is not specially broken. But `CastleVendorNpcInjector.cs:114` DOES map it:

```
case "armorer": return new Vendor { BodyRes = BodySmith, StructureId = "armorer",
                                    Label = "Armorer", Arch = Archetype.Blacksmith };
```

So the mapping exists and the body is named, yet no NPC appears. That is a RUNTIME
question (does the injector run in this hub scene? does `BodySmith` resolve? was the
armorer built?) and it is **not a design question** — so it is NOT part of this WO's
design work. It is recorded here because the two were reported together and because a
vendor with no vendor is part of why the gear surface feels unstaffed. CLI to instrument
and settle it separately, per §12 — no fix until a captured line names the cause.

