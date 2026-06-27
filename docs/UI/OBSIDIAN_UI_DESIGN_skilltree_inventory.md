# Obsidian UI Design Spec — Skill Tree & Inventory restyle

> DESIGN SPEC ONLY (read-only UI design pass). No code, no edits. For CLI to implement.
> Grounded in THIS project's verified plumbing — every constant / file:line below is real.
> Date: 2026-06-27.

## 0. What this is (and the one gate)

Two existing MVVM screens get an Obsidian dark-fantasy reskin **in place** (restyle, not
greenfield): the Knight **Skill Tree** (`HeroSkillTreePanelMvvm.cs`) and the **Inventory**
(`HeroInventoryController` partials — `InventoryUIBuilder.cs` / `InventoryGrid.cs`, bound to
`InventoryVM.cs`). Both already build with `ElarionUiKit` + `RpgUiCatalog` 9-slice sprites;
this spec tightens them onto ONE Obsidian frame family and pushes node/cell icons through
`ConceptIconResolver`.

**The gate:** `ff.blinkchrome` (`FeatureFlags.cs:88`, default **OFF**). When OFF our painted
chrome (solid fill + ember glow + rune strip) shows; when ON those neutralize to `alpha 0` so
the bare Obsidian panel sprite reads clean (`InventoryUIBuilder.cs:52-63`,
`HeroSkillTreePanelMvvm.cs:165`). **This design must look correct in BOTH states** — that is
the existing contract (sprite-first, procedural fallback on null, `RpgUiCatalog.cs:12-16`). The
restyle does not flip the default; CLI may expose an in-build toggle later.

## 1. Architecture honored (state-back)

- **Strict MVVM, dumb View.** The View binds `model → widget` and routes input back as
  commands; it never reads game state. Skill tree: `HeroSkillTreePanelMvvm.Render()` repaints
  from `_vm.*` ONLY (`:103-110`); node plate colour/labels come from `SkillNodeVM` fields
  (`:299-348`). Inventory: `BuildCellsFromVM` is "a pure projection of `vm.Slots`"
  (`InventoryGrid.cs:67-73`); every cell's id/name/icon-keys/rarity/equipped come from `ItemVM`.
- **Presentation never touches game objects.** Icons are carried as KEYS (`InventoryVM` doc
  `:17-21`: `IconRole`=kind, `IconName`=id) — the View resolves the Sprite. Same for skill
  nodes (concept id → sprite via the resolver).
- **One frame family across both screens.** Both windows use `RpgUiCatalog.PanelWindowDark`
  (=`PanelDefault`, `RpgUiCatalog.cs:95,102`) via `ElarionUiKit.PanelFramed` (`:958`). Cells and
  nodes share the `slot`/`grid` plate family. Buttons share `ButtonFrame`/`ButtonGold`/
  `ButtonConfirm`/`ButtonExit`. This is the consistency anchor.
- **Don't hand-roll 9-slice.** `ElarionUiKit.PanelFramed/Well/Niche/Slot/Card/ButtonPack/
  TechGearSocket` already apply `Image.Type.Sliced` + sprite-first + procedural fallback. Feed
  them `RpgUiCatalog` ids; never `new Image()` a raw Obsidian PNG (BLINK.md:287-302).

---

## 2. SKILL TREE — `HeroSkillTreePanelMvvm`

### 2.1 ASCII layout

```
 ╔══════════════════════════════════════════════════════════════════╗  panel_window_dark
 ║  ⌖  KNIGHT SKILLS                                           [ X ]  ║  Header + ButtonExit
 ║  Wisdom 4      Skill Points 2                                      ║  _walletText (Gilt)
 ╟──────────────────────────────────────────────────────────────────╢
 ║  ┌── RANGED ──┐   ┌─ HEAL · SUSTAIN ─┐   ┌── CONTROL ──┐          ║  3 branch Wells
 ║  │   Tier 3   │   │      Tier 3      │   │   Tier 3    │          ║
 ║  │  ┌──────┐  │   │   ┌──────┐       │   │  ┌──────┐   │          ║  node = slot_talent
 ║  │  │ [ic] │  │   │   │ [ic] │       │   │  │ [ic] │   │  gold-rim║   when CanUnlock
 ║  │  │ Name │  │   │   │ Name │       │   │  │ Name │   │  green   ║   when Owned
 ║  │  │ 3 Wis│  │   │   │Owned │       │   │  │ Lock │   │  dim     ║   when Locked
 ║  │  └──┬───┘  │   │   └──┬───┘       │   │  └──────┘   │          ║
 ║  │  Tier 2│   │   │   Tier 2│        │   │   Tier 2    │          ║  gilt prereq line
 ║  │  ┌──┴───┐  │   │   ┌──┴───┐       │   │  ┌──────┐   │          ║   (BuildPrereqLine)
 ║  │  │ node │  │   │   │ node │       │   │  │ node │   │          ║
 ║  │  └──────┘  │   │   └──────┘       │   │  └──────┘   │          ║
 ║  │  Tier 1    │   │   Tier 1         │   │   Tier 1    │          ║
 ║  │  ┌──────┐  │   │   ┌──────┐       │   │  ┌──────┐   │          ║
 ║  └──┴──────┴──┘   └────┴──────┴──────┘   └──┴──────┴──┘          ║
 ╟──────────────────────────────────────────────────────────────────╢
 ║                   [  Equip Skills  ]        [ Close ]             ║  ButtonGold + ButtonFrame
 ╚══════════════════════════════════════════════════════════════════╝
```

Layout is the EXISTING one (`:148-217` chrome, `:228-267` columns top-down tier3→tier1,
`:271-284` gilt connectors). The restyle changes the SPRITES + node-state plates, not the
geometry.

### 2.2 RpgUiCatalog roles/constants per region

| Region | Builder | Sprite (RpgUiCatalog) | Notes |
|---|---|---|---|
| Window frame | `PanelFramed(... packSpriteName: RpgUiCatalog.PanelWindowDark)` (`:160`) | `RolePanel` / `PanelWindowDark` (`:95`) | already correct — keep |
| Branch column | `ElarionUiKit.Well` (`:230`) | `RolePanel`/`PanelGrid` recommended (today raw glass) | dress Well with `PanelGrid` so columns read as carved niches |
| Skill node card | `BuildNodeCard` (`:288`) — TODAY a raw `Image`+`ApplyRounded` (`:296-304`) | **NEW** `RoleSlot` / `slot_talent` (the Obsidian `Talent_Border_*`) | see 2.3; falls back to current colour plate when art absent |
| Node icon | NEW `AddIcon`/`TechGearSocket` inside the card | via `ConceptIconResolver.Resolve(node.Id)` → real `RoleIcons` sprite | see 2.4 |
| Equip button | `ButtonPack(... Gold)` (`:195-198`) | `ButtonGold` (chrome OFF) / `ButtonConfirm` (chrome ON) — already branches | keep |
| Close button | `ButtonPack(... Quiet)` (`:208-210`) | `ButtonFrame` | keep; consider `ButtonExit` for an X variant |
| Header rule / wallet | `ElarionUiKit.Header` (`:172`), `_walletText` Gilt (`:183`) | — | keep |

### 2.3 Node state styling (Locked / Owned / Unlockable / Equipped)

The states already exist (`:299-348`); map each to an Obsidian sprite **tint** over the new
`slot_talent` plate (keep the colour as the fallback when the plate art is absent):

| State (VM field) | Today (`:301-346`) | Obsidian restyle |
|---|---|---|
| **Owned** (`node.Owned`) | green plate α0.22, label "Owned"/Gilt | `slot_talent` tinted `ElarionUi.Affordable` (green), full α; "Owned" gilt; subtle inner rim |
| **Unlockable** (`node.CanUnlock`) | gold plate α0.20, "<n> Wisdom"/Affordable, button interactable | `slot_talent` tinted **`ElarionUi.Gold` full α + gold rim glow** (the hero state); cost in Affordable green |
| **Locked** (`!Owned && !CanUnlock`) | dim `ElarionUiKit.Cell` α0.40, `LockReason`/Danger, button off | `slot_talent` desaturated → tint `ElarionUiKit.Cell` α0.40, icon at α0.5; show `node.LockReason` (the specific "why", never bare LOCKED) |
| **Equipped** (`node.IsEquipped`) | "EQUIPPED" chip, Affordable (`:325-327`) | add a small `RoleBadge`/`badge_level` corner chip OR keep the gilt text chip; gold corner gem |
| Kind chip | "SKILL"/Aether vs "STAT" (`:319-322`) | keep — Aether for Skill, ParchmentDim for Stat |

Concrete sprite + tint source: the Obsidian pack has `Talent_Border_1..6.png` in
`Slots_Obsidian/` (BLINK.md:251) — mirror as `slot_talent` (or `slot_talent_1..6` for a
rarity-style escalation by tier). Until mirrored, the View keeps the current colour plates
(null-safe contract, `RpgUiCatalog.cs:12-16`).

### 2.4 Icon resolution (skill nodes)

Today nodes render NO icon (text only). Add a centred icon well per node via
`ConceptIconResolver.Resolve(node.Id)` (`ConceptIconResolver.cs:79`) → `RpgUiCatalog.Get`. The
Knight ability ids are ALREADY mapped in `concept-icons.json:20-24`:

- `knight.ranged-poke` → `icons/icon_sword`
- `knight.mending-salve` → `icons/icon_heart`
- `knight.snare-arrow` → `icons/icon_shield`
- `knight.suppressing-volley` → `icons/icon_combat`
- `knight.shield-bash` → `icons/icon_shield`

For STAT nodes (no ability id), fall back to a branch glyph: Ranged→`icon_sword`,
Heal→`icon_heart`, Control→`icon_shield` (already in the table as `sword`/`heal`/`shield`).
The View calls `ConceptIconResolver.ResolveAny(node.Id, node.Branch, kindToken)` and keeps a
letter-glyph fallback on null — zero icon choices in C#, the JSON decides.

### 2.5 MVVM binding seam (which VM field → which widget)

The View is already dumb; the restyle only changes how the SAME fields paint:

| Widget | VM field (real) | file:line |
|---|---|---|
| Header title | `_vm.Title` | `:106` |
| Wallet line | `_vm.RemainingWisdom`, `_vm.RemainingSkillPoints` | `:107-108` |
| Columns | `_vm.Branches` (List), `_vm.Nodes` (List<SkillNodeVM>) | `:117,129` |
| Node plate state | `node.Owned`, `node.CanUnlock`, `node.LockReason`, `node.IsEquipped` | `:301,302,345,325` |
| Node label/cost | `node.Name`, `node.WisdomCost`, `node.Kind` (SkillNodeKind) | `:315,339,319` |
| Node tap | `_vm.Unlock(node.Id)` | `:311` |
| Equip button | `OpenLoadout` → `PanelRouter.Open(PanelId.HeroLoadout)` | `:219-224` |
| Close | `_vm.Close()` | `:209` |

No new VM fields required for the reskin. (If a per-node icon CONCEPT id differs from `node.Id`,
add a `node.ConceptId` getter on the VM — optional; `node.Id` already works for the Knight set.)

---

## 3. INVENTORY — `HeroInventoryController` (UIBuilder + Grid) bound to `InventoryVM`

### 3.1 ASCII layout

```
 ╔══════════════════════════════════════════════════════════════════╗  panel_window_dark
 ║  ⌖  INVENTORY                                              [ X ]   ║  Header + ButtonFrame X
 ╟────────────┬─────────────────────────────────────────────────────╢
 ║            │ [Weapons][Armor][Access][Consum][Skills→]            ║  tabs (panel_tab)
 ║  ┌──────┐  ├─────────────────────────────────────────────────────╢
 ║  │PORTR.│  │ ┌────┐┌────┐┌────┐┌────┐┌────┐                       ║  grid (panel_grid host)
 ║  │ Niche│  │ │slot││slot││slot││slot││slot│  5 col landscape      ║   cell = slot_item
 ║  │ Lvl  │  │ └────┘└────┘└────┘└────┘└────┘                       ║   (BlinkChrome ON) /
 ║  │ bars │  │ ┌────┐┌────┐┌────┐┌────┐┌────┐                       ║   panel_inventory OFF
 ║  │ stats│  │ │ ✦  ││sel ││ v  ││    ││    │  ← selected=gold rim  ║   equipped = v chip
 ║  └──────┘  │ └────┘└────┘└────┘└────┘└────┘   scroll ↕            ║
 ║            │                                                       ║
 ╟────────────┴─────────────────────────────────────────────────────╢
 ║ [Sort][Filter]              GOLD 0 │ CRYSTALS 0 │ WALLET SKR      ║  footer (Track tray)
 ╚══════════════════════════════════════════════════════════════════╝
```

Geometry is the EXISTING one (`InventoryUIBuilder.cs:25-94` root, `:169-259` tabs, `:97-150`
footer; `InventoryGrid.cs:22-65` grid, `:188-305` cell). Restyle = unify sprites + fix the
`PanelVendor`-vs-window mismatch already noted in-code (`:47-50`).

### 3.2 RpgUiCatalog roles/constants per region

| Region | Builder | Sprite (RpgUiCatalog) | Notes |
|---|---|---|---|
| Window frame | `PanelFramed(... PanelWindowDark)` (`:49-50`) | `RolePanel`/`PanelWindowDark` | already fixed off `PanelVendor` — keep; matches skill tree |
| Portrait niche | `ElarionUiKit.Niche` (`:78`) | `RolePanel`/`PanelPortrait` recommended | `PanelPortrait` (`:98`) is the ornate portrait frame — dress the niche with it |
| Tab row | `BuildTabs` (`:169`) | `RolePanel`/`PanelTab` (`:216`) | already the committed fallback; **remove the gitignored `Resources.Load("Tech hud elements/...")` primary** (`:209-213`) or keep as optional — `PanelTab` is the canon |
| Tab icon | `TabPackIcon` (`:274`) | `IconSword`/`IconShield`/`IconHeart`/`PotionHealth` fallbacks already wired (`:282-288`) | keep fallbacks; prefer routing through `ConceptIconResolver` (3.4) |
| Grid host | `ElarionUiKit.Well` + `DressPanel(PanelInventory)` (`:90-91`) | `RolePanel`/`PanelInventory` | keep |
| Item cell tile | `BuildGearCell` (`:188`) | `RoleSlot`/`SlotItem` when `BlinkChrome` ON, else `RolePanel`/`PanelGrid`→`PanelInventory` (`:252-256`) | already the intended dual path — keep |
| Cell icon socket | `TechGearSocket` (`:271`) | — (procedural socket) | keep; feed `ResolveItemIcon` sprite (`:276`) |
| Sort/Filter/Close | `ButtonPack(... Quiet)` | `ButtonFrame` (`:73,106,109`) | keep |
| Footer wells | `ResourceWell` (`:139`) | raw glass + rim | optional: dress with `RoleBars`/`panel_bar` |

### 3.3 Cell state styling (empty / filled / selected / equipped / locked)

States already exist in `BuildGearCell` (`:188-305`). Obsidian mapping:

| State | Today | Obsidian restyle |
|---|---|---|
| **Empty tab** | `BuildEmptyNote` text (`:313`) | keep text note (data-empty path is load-bearing, §12) |
| **Filled** | `slot_item`/`PanelGrid` tile + rarity outer frame (`:194-217,252-263`) | keep — `slot_item` is the Obsidian per-item plate; rarity tints the outer `CellFrame` |
| **Selected** (`selected`) | gold outer frame α1 + `SelGlow` α0.45 (`:195-197,224-236`) | keep gold rim; this is the hero highlight — matches skill-tree unlockable gold |
| **Equipped** (`equipped`) | `CellSel` tile + green "v" chip top-right (`:287-294`) | keep; use `RoleBadge`/`badge_level` chip art for parity with node Equipped chip |
| **Locked** (`locked`) | parchment veil α0.45 + gold lock chip (`:295-304`) | desaturate `slot_item` tint, dim icon α0.5 (matches node Locked) |
| Rarity gem | corner gem `RarityColor` (`:284-285`) | keep — Obsidian `Rarity_1..5` could mirror later (3.5) |

Rarity → frame strength/colour already data-driven (`RarityColor/RarityInk/RarityFrameStrength`,
`:191-194`). No change needed; optionally swap the outer frame to Obsidian `Rarity_*` plates.

### 3.4 Icon resolution (inventory)

Cells resolve via `ResolveItemIcon(role, id)` (`InventoryGrid.cs:132-155`): real item art first
(`ItemIconCatalog.ForWeapon/ForArmor`), pack-icon fallback (`IconSword`/`IconShield`/potion).
This is correct — keep it. To push MORE through the data map, route the fallback through
`ConceptIconResolver.ResolveAny(id, role, type)` so the JSON can override per-item without code.
The role keys (`InventoryVM.IconRoleWeapon/Armor/Potion`, `:87-89`) are the concept tokens; add
rows to `concept-icons.json` for any specific item id needing bespoke art. Today `sword`,
`shield`, `potion`, `inventory`, `bag` already map (`concept-icons.json:26-37`).

### 3.5 MVVM binding seam (InventoryVM → widgets)

`InventoryGrid.BuildCellsFromVM` is already a pure `vm.Slots` projection (`:67-114`). Field map:

| Widget | VM field (real) | file:line |
|---|---|---|
| Header title | `InventoryVM.Title` ("Inventory") | `InventoryVM.cs:134` |
| Tab chips (label+count) | `vm.Tabs` (`IReadOnlyList<InventoryTab>`: `.Label`, `.Count`) | `:153,37-49` |
| Active tab | `vm.ActiveTab` / `vm.ActiveTabIndex`; command `vm.SelectTab(i)` | `:155-157,198` |
| Cell list | `vm.Slots` (`IReadOnlyList<ItemVM>`) | `:150` |
| Cell icon keys | `ItemVM.IconRole`, `ItemVM.IconName` | `InventoryVM.cs:86-89` |
| Cell name / rarity / equipped | `ItemVM.Name`, `.Rarity`, `.Equipped` | grid `:104` |
| Selected highlight | `vm.SelectedId` / `vm.SelectedSlotIndex` | `:159,162` |
| Cell tap | `vm.SelectById(id)` → `vm.Equip()` (gear) / `vm.Use()` (consumable) | grid `:109-112`; VM `:190,244,210` |
| Detail pane (if shown) | `vm.Selected` (`InventoryDetail?`: Name/Stats/Desc/StackCount/CanUse/CanEquip) | `:174,56-82` |
| Footer wallet | NOT in VM — read directly (`GameStateService`) in `BuildFooterBar` (`:111-127`) | acceptable (passive readout); could move to VM later |

> Note: the current `HeroInventoryController` predates a full `InventoryVM` rebind (Phase C
> partially done — the grid is VM-projected, chrome/tabs are not yet). The restyle should NOT
> regress the dumb-View rule: tab `_tab` state should read `vm.ActiveTab`. Flag for CLI but out
> of scope for a pure reskin.

---

## 4. What art is MISSING (mirror into Resources/RpgUi before full skin)

Verified present today (glob of `Assets/Resources/RpgUi/**`): panels (`panel_window`,
`panel_window_dark`, `panel_grid`, `panel_inventory`, `panel_vendor`, `panel_bar`, `panel_tab`,
`panel_quest`, `panel_portrait`, `profile_frame`), buttons (`button_frame`, `button_gold`,
`button_confirm`, `button_exit`), `slot/slot_item`, icons (`icon_sword/shield/heart/combat/
inventory/quest/settings/talk/tree/compass`), bars, potion, `badge_level`.

**NOT yet mirrored — add to `BlinkUiImporter.BuildTable()` then re-run `Defenders > Art >
Import Blink UI Pack`** (`BlinkUiImporter.cs:62-91`; source folders BLINK.md:248-251):

| Want | Obsidian source | Proposed canonical id | Role |
|---|---|---|---|
| Skill-node frame | `Slots_Obsidian/Talent_Border_1..6.png` | `slot_talent` (or `slot_talent_1..6`) | `slot` |
| Skill-tree window (optional alt) | `Panels_Obsidian/Talent_Tree_Panel.png` | `panel_talent` | `panel` |
| Equip/armor slot (paper-doll) | `Slots_Obsidian/Armor_Slot.png`, `Character_Slot.png` | `slot_armor`, `slot_character` | `slot` |
| Skill/spell glyph | `Icons_Obsidian/icon-spellbook.png` | `icon_spellbook` | `icons` |
| Helmet/armor glyph | `Icons_Obsidian/Helmet.png` | `icon_helmet` | `icons` |
| Rarity cell plates (optional) | `Slots_Obsidian/Rarity_1..5.png` | `slot_rarity_1..5` | `slot` |
| Currency (footer) | `Copper_/Silver_/Gold_Currency.png` | `icon_gold` etc. | `icons` |

All are null-safe: until mirrored, both Views keep their current colour-plate / glyph fallback
(`RpgUiCatalog.cs:12-16`, `ConceptIconResolver.cs:18-22`). The reskin degrades gracefully.

New `concept-icons.json` rows to add alongside the art: `skill`/`spell` → `icons/icon_spellbook`;
`armor`/`helmet` → `icons/icon_helmet`; per-branch stat fallbacks if desired.

---

## 5. Build note (for CLI)

- This is a **DESIGN SPEC** — implementation + the C# edits are CLI's (UI never writes `.cs`,
  CLAUDE.md §2). No code here.
- **`ff.blinkchrome` (default OFF)** gates the dark-glass clean-through state
  (`FeatureFlags.cs:88`). Verify the restyle in BOTH OFF (our chrome) and ON (bare Obsidian
  sprite) — the existing `:165` / `InventoryUIBuilder.cs:52` branches already handle the α
  neutralization; keep them.
- **Reuse, don't reinvent:** build via `ElarionUiKit.PanelFramed/Well/Niche/Slot/Card/
  ButtonPack/TechGearSocket` + feed `RpgUiCatalog` ids; the kit applies the 9-slice. Do NOT
  write an `ObsidianUiHelper` — `RpgUiCatalog` + `ElarionUiKit` ARE that helper (BLINK.md:344).
- **No UXML** (§8 — UXML does not render in builds). Code-built uGUI only — both target Views
  already are.
- **Importer 9-slice borders** (BLINK.md:281-285): panels 48, slot 24, button 24, icons 0
  (`preserveAspect`). New `slot_talent`/`slot_*` should import at the slot border (24); new
  icons at 0. Set in `BlinkUiImporter.ForceSprite`.
- Order of work: (1) mirror missing art + concept-icons rows; (2) skill-tree node plate →
  `slot_talent` + node icons via resolver; (3) inventory portrait niche → `PanelPortrait`,
  confirm `slot_item`/`PanelGrid` cell path; (4) verify both flag states headless.

---

## 6. UiStyle — single theme singleton (one style for everything)

> Owner directive (extends this design): "make a styling-type SINGLETON for ONE UI style for
> EVERYTHING — not piece this and piece that." This is her One-Model method applied to
> PRESENTATION: ONE style authority every dumb View pulls from; styling is never decided
> per-screen. DESIGN ONLY below — no code. This supersedes the per-region tables in §2.2 / §3.2
> as the *mechanism* (those tables become the DEFAULT VALUES inside the one theme record).

### 6.1 Inventory of style decided piecemeal TODAY (the "piece this/piece that")

There already IS a partial authority — `ElarionUi` (`ElarionUi.cs:38`, "the ONE in-game UI
theme", Core so HUD+Village both read it) holds the **palette + font scale + spacing**. That is
the seed of the singleton. But it stops at colours/fonts; **frames, slot sprites, button
sprites, state-tints, and the chrome gate are each re-decided at every call site**, and a SECOND
palette (`ShopTheme.cs:39-81`) duplicates it. Where style is independently decided today:

| Style concern | Authority today? | Piecemeal decision sites (file:line) |
|---|---|---|
| Palette (gold/parchment/danger/affordable/aether/disabled) | `ElarionUi.cs:44-99` ✅ | DUPLICATED by `ShopTheme.cs:39-81` (re-aliases the same colours); kit re-derives `Glass/Cell/CellSelected/Accent` `ElarionUiKit.cs:57-72` |
| Font sizes (Title/Head/Body/Label/Micro) | `ElarionUi.cs:87-91` ✅ | consumed ad-hoc; some call sites pass `FontTitle + 4` (`InventoryGrid.cs:276`), `FontMicro + 2` (`:281`) — magic deltas |
| Spacing / radius / tap target | `ElarionUi.cs:94-99` ✅ | but cell SIZE is a literal `new Vector2(78f,72f)`, spacing `(6f,6f)`, padding `RectOffset(4..)` in `InventoryGrid.cs:56-58` — not in the authority |
| **Window frame sprite** | ❌ none | each panel names it: skill tree `PanelWindowDark` (`HeroSkillTreePanelMvvm.cs:161`), inventory `PanelWindowDark` (`InventoryUIBuilder.cs:50`) — chosen independently, was wrong once (`PanelVendor` bug, `:47-50`) |
| **Slot / cell sprite** | ❌ none | inventory cell decides `SlotItem` vs `PanelGrid` vs `PanelInventory` inline (`InventoryGrid.cs:252-256`); skill node uses NO sprite, raw `ApplyRounded` (`HeroSkillTreePanelMvvm.cs:296-304`) |
| **Button frame sprite** | ❌ none | each call passes `packSpriteName`: `ButtonGold`/`ButtonConfirm` (skilltree `:198`), `ButtonFrame` (`:210`, inv `:73,106`) — per call |
| **State tint: Locked/Owned/Unlock/Equipped** | ❌ none | skill node hardcodes the gold-rim/green/dim plate (`HeroSkillTreePanelMvvm.cs:301-304,332-346`); inventory cell hardcodes selected-gold/equipped-green/locked-veil (`InventoryGrid.cs:195-197,244-246,287-304`) — SAME semantic states, two independent literal sets |
| **Tab fill / icon** | ❌ none | `InventoryUIBuilder.cs:185-225` literal `inactive` colour + `Resources.Load("Tech hud elements/...")` then `PanelTab` fallback |
| **ff.blinkchrome on/off branch** | ❌ none | branched in ≥4 places: `HeroSkillTreePanelMvvm.cs:165,198`; `InventoryUIBuilder.cs:52`; `InventoryGrid.cs:253`; `ElarionUiKit.cs:154-205` |

**Count:** ~9 style concerns, of which only 3 (palette/font/spacing) have an authority, and even
those are duplicated (`ShopTheme`) and bypassed (literal cell sizes, magic font deltas). Frames,
slots, buttons, state-tints and the chrome gate are decided **per panel, ~12+ independent sites**.
That spread is exactly what the singleton removes.

### 6.2 The single authority — `DeNelle.Core.UI.UiStyle`

A static facade in **`DeNelle.Core.UI`** (same assembly as `ElarionUi`/`RpgUiCatalog`/
`ConceptIconResolver`, so HUD + Village + Audio all read it with NO forbidden edge — CLAUDE.md
§5). It OWNS nothing new conceptually — it *composes* the three existing primitives
(`RpgUiCatalog` sprites + `ConceptIconResolver` icons + a single `UiTheme` record of values) and
exposes them as **semantic tokens**. Views and `ElarionUiKit` call ONLY `UiStyle.*`; no raw hex,
no `RpgUiCatalog.PanelX`, no `ff.blinkchrome` branch survives at a call site.

Facade shape (semantic accessors — illustrative names, not code):

```
DeNelle.Core.UI.UiStyle           // static facade; reads UiStyle.Theme (a UiTheme record)
  Theme        : UiTheme          // the ONE swappable record (§6.5). Set once at boot.
  Chrome       : bool             // == FeatureFlags.BlinkChrome, read in ONE place

  Frame.Window / .Vendor / .Grid / .Portrait / .Quest   -> Sprite (RpgUiCatalog panel role)
  Slot(SlotState)                 -> Sprite  (Empty/Filled/Selected/Equipped/Locked -> slot_item/slot_talent...)
  Button(ButtonRole)              -> Sprite  (Primary->ButtonGold|ButtonConfirm[chrome], Neutral->ButtonFrame, Close->ButtonExit)

  Color.Locked / .Owned / .Unlockable / .Selected / .Equipped / .Disabled
  Color.TextPrimary / .TextDim / .Accent / .Danger / .Affordable / .Aether
  Color.PanelFill(bool chromeAware)        // returns alpha-0 when Chrome, solid otherwise — gate lives HERE

  Font.Title / .Header / .Body / .Caption / .Micro   -> int (size)
  Pad.Sm / .Md / .Lg ; Radius.Sm/.Md/.Lg ; CellSize ; TapTarget   -> float/Vector2

  Icon(conceptId, fallbackConcept...)      -> Sprite  (wraps ConceptIconResolver.ResolveAny)
  StatePlate(state)                        -> (Sprite slot, Color tint)  // the node/cell state in ONE call
```

Mapping the §6.1 concerns into tokens (the authority's defaults = today's correct values):

- `Frame.Window` → `RpgUiCatalog.Get(RolePanel, theme.WindowPanel)` where `theme.WindowPanel =
  PanelWindowDark`. `Frame.Vendor/Grid/Portrait` → `PanelVendor/PanelGrid/PanelPortrait`.
- `Slot(state)` → `slot_item` (inventory) / `slot_talent` (tree) per `theme.SlotByState`; null-safe
  so the kit keeps its procedural plate.
- `Button(role)` → `theme.ButtonPrimary` resolves to `ButtonConfirm` when `Chrome` else
  `ButtonGold`; `Neutral`→`ButtonFrame`; `Close`→`ButtonExit`. The chrome branch dies here.
- `Color.Locked/Owned/Unlockable/Selected/Equipped` → the semantic tints (today's
  `ElarionUiKit.Cell α0.40` / `Affordable` / `Gold` / `Gold rim` / `Affordable`), named ONCE.
- `Font.*`/`Pad.*`/`CellSize` → re-export `ElarionUi.Font*`/`Pad*` + the cell size literal,
  killing the magic `+4`/`+2` deltas and the inline `Vector2(78,72)`.
- `Icon(conceptId)` → `ConceptIconResolver.ResolveAny(...)` — a View asks "icon for concept X",
  never names a sprite.

`ElarionUi` is NOT deleted — it becomes the *default value provider* the `UiTheme` record reads
(palette/fonts). `ShopTheme` collapses into `UiStyle.Color.*` (its 20 aliases are duplicates).

### 6.3 ElarionUiKit + Views consume ONLY UiStyle — before/after

**(a) Skill node plate** — `HeroSkillTreePanelMvvm.cs:299-304`
```
// BEFORE (literal state tints, no sprite, decided in the View):
if (node.Owned)        plate = new Color(ElarionUi.Affordable.r, .g, .b, 0.22f);
else if (node.CanUnlock) plate = new Color(ElarionUi.Gold.r, .g, .b, 0.20f);
else                   plate = new Color(ElarionUiKit.Cell.r, .g, .b, 0.40f);
img.color = plate;

// AFTER (one semantic call; sprite + tint from the authority):
var (slot, tint) = UiStyle.StatePlate(node.Owned ? SlotState.Owned
                    : node.CanUnlock ? SlotState.Unlockable : SlotState.Locked);
ElarionUiKit.ApplySlot(img, slot, tint);   // kit applies 9-slice; null slot -> procedural
```

**(b) Inventory cell selected/equipped** — `InventoryGrid.cs:195-260`
```
// BEFORE: gold-rim literal + SlotItem-vs-PanelGrid-vs-PanelInventory inline + chrome branch.
Color frameCol = selected ? new Color(ElarionUi.Gold..., 1f) : new Color(rc.r..., frameAlpha);
Sprite cellTile = FeatureFlags.BlinkChrome ? RpgUiCatalog.Get(RoleSlot, SlotItem) : null;
if (cellTile == null) cellTile = RpgUiCatalog.Get(RolePanel, PanelInventory);

// AFTER: state -> (sprite,tint) from the authority; rarity stays a data overlay.
var (tile, tint) = UiStyle.StatePlate(selected ? SlotState.Selected
                    : equipped ? SlotState.Equipped : SlotState.Filled);
ElarionUiKit.ApplySlot(img, tile, RarityBlend(tint, rarity));
```

**(c) Button** — any `ButtonPack` call (`HeroSkillTreePanelMvvm.cs:198`, `InventoryUIBuilder.cs:73`)
```
// BEFORE: each caller picks the sprite + branches on chrome:
packSpriteName: FeatureFlags.BlinkChrome ? RpgUiCatalog.ButtonConfirm : RpgUiCatalog.ButtonGold
// AFTER: ask for the ROLE; the authority resolves sprite + chrome:
sprite: UiStyle.Button(ButtonRole.Primary)
```

`ElarionUiKit` itself changes its constructors to read defaults from `UiStyle`: `PanelFramed`'s
default `packSpriteName` → `UiStyle.Frame.Window`; `Glass/Cell/CellSelected/Accent` (`:57-72`)
→ `UiStyle.Color.*`; `ButtonPack` default sprite → `UiStyle.Button(Neutral)`. Then a View that
passes nothing gets the themed default automatically.

### 6.4 Feasibility ("if possible") + migration

**Verdict: VIABLE.** The hard prerequisite is already met — `ElarionUi` proves a Core-resident
style authority that HUD (`DeNelle.HUD`) and Village (`DeNelle.Village`) both legally read
without a HUD↔Village edge (`ElarionUi.cs:9-12`). `UiStyle` sits beside it in `DeNelle.Core.UI`,
reads `RpgUiCatalog` + `ConceptIconResolver` (both Core), so EVERY assembly can consume it.
Sprite-first null-safety (`RpgUiCatalog.cs:12-16`) means a missing themed sprite degrades to the
kit's procedural look — the migration can't blank a screen.

Phased, non-breaking (each phase ships + headless-verifies green before the next):
- **(a) Introduce `UiStyle` + `UiTheme`** reading default values from `ElarionUi` + the §2/§3
  frame choices. Nothing consumes it yet — pure addition, zero risk.
- **(b) Route `ElarionUiKit` through it** — kit color/frame/button defaults pull from `UiStyle`.
  Visual no-op (defaults == today's values); proven by screenshot diff.
- **(c) Migrate panels to semantic tokens** — replace the per-site literals in
  `HeroSkillTreePanelMvvm` / `InventoryUIBuilder` / `InventoryGrid` (and later ShopPanel,
  EquipmentPanel) with `UiStyle.*`. One panel per commit (file-disjoint lanes, §9).
- **(d) Delete dead style literals** — fold `ShopTheme` into `UiStyle.Color.*`; remove the magic
  font deltas and inline cell-size; assert no `ff.blinkchrome` branch remains outside `UiStyle`.

**Honest blockers / things that resist semantic tokens:**
- **Rarity tint** is genuinely DATA (per-item, `RarityColor` from the item's rarity key,
  `InventoryGrid.cs:191`) — it is a *data overlay on top of* the state plate, not a theme token.
  Keep it as data; `UiStyle.StatePlate` returns the base, rarity blends over it (shown in 6.3b).
- **Gitignored Tech-pack `Resources.Load("Tech hud elements/...")` primaries**
  (`InventoryUIBuilder.cs:209`, `InventoryGrid.cs:204`) are clean-build-absent; the migration
  should DROP those primaries and let `UiStyle.Frame/Slot` (committed `RpgUi`) be the source —
  this also removes a fragile path, but it is a deliberate look change to confirm with the owner.
- **HUD bars / orbs** (HP/MP fill colours, `ElarionUi.cs:80-83`) are semantic already; fold in,
  but the live HUD is the riskiest surface — migrate it LAST (phase c tail), felt-verify.

### 6.5 The theme record — the "one style" you can swap to A/B a whole look

`UiStyle.Theme` is ONE data object (`UiTheme`) holding EVERY token value: the panel/slot/button
sprite NAMES (strings resolved through `RpgUiCatalog`), the semantic colours, the font sizes, the
spacing/cell scale, and the icon default. Swap that one record → the whole game reskins. This is
literally the owner's "try() it" — A/B an Obsidian look vs a parchment look by assigning a
different record at boot.

**Recommendation: a code-default `UiTheme` record NOW, JSON-backed LATER (not a ScriptableObject).**
- A **code-default record** (a plain `[Serializable]` struct/class with the current values) is the
  zero-risk phase-(a) form — no asset wiring, no inspector drag-drop (which is BANNED, memory
  `never-dragdrop-or-manual-playtest`), compiles into every build, WebGL-safe.
- Promote to **JSON in `Resources/Data/Canonical/ui-theme.json`** loaded via `CanonicalJson`
  (exactly how `concept-icons.json` loads, `ConceptIconResolver.cs:42,172`) once more than one
  theme exists. This matches the project's data-driven canon (memory
  `owner-thinks-in-data-structures`) and the WebGL-safe Resources convention, and lets the owner
  A/B by editing data, no recompile.
- **Avoid a ScriptableObject:** it needs inspector authoring/drag-drop (banned) and doesn't fit
  the JSON-catalog pattern the rest of the data uses. The sprite NAMES stay strings the JSON
  holds; `RpgUiCatalog`/`ConceptIconResolver` already resolve string→Sprite null-safely.

This is a separate WO from the Obsidian reskin (§1-5): the reskin can land FIRST against today's
per-panel sites, then phase (c) migrates those same sites onto `UiStyle` — or, cleaner, introduce
`UiStyle` (phase a/b) BEFORE the reskin so the Obsidian values are authored ONCE in the record.

### 6.6 SCOPE — every screen consumes the ONE style (the full offender roster)

The singleton is NOT scoped to skill-tree + inventory. EVERY player-facing surface is a current
"piece this/piece that" offender (each picks its own frame/colours/fonts today) and EVERY one
becomes a `UiStyle.*` consumer. The real screens + their style touchpoints:

| Screen | Real file:line | Style it decides itself TODAY → after |
|---|---|---|
| **Shop (party)** | `PartyShopPanelMvvm.cs:40` (+ `PartyShopVM.cs`) | own panel frame + row plates + buttons → `UiStyle.Frame.Vendor` / `UiStyle.Slot(state)` / `UiStyle.Button(Primary)` |
| **Shop (legacy/base)** | `ShopPanel.cs:29` (+ `ShopVM.cs`), `ShopTheme.cs:39-81` | `ShopTheme` is a DUPLICATE palette → folds into `UiStyle.Color.*`; frame → `UiStyle.Frame.Vendor` |
| **Cosmetic shop / packs** | `CosmeticShopPanel.cs`, `PackStore.cs:38` | own card frames/price chips → `UiStyle.Frame.Grid` + `UiStyle.Slot` + `UiStyle.Color.Accent` |
| **Building upgrade** | `BuildingUpgradePanelMvvm.cs:30` (+ `BuildingUpgradeVM.cs`) | own window + tier rows + Affordable/Disabled tints → `UiStyle.Frame.Window` + `UiStyle.Color.Unlockable/Disabled` |
| **Tower manager / swap** | `TowerManagerPanel.cs:20`, `TowerSwapMenu.cs`, `TowerEmpowerButton.cs` | own plates + buttons → `UiStyle.Frame.*` + `UiStyle.Button(...)` |
| **Build menu** | `BuildMenu.cs:50` | own button strip + cost colours → `UiStyle.Button` + `UiStyle.Color.Affordable/Danger` |
| **Inventory** | `InventoryUIBuilder.cs` / `InventoryGrid.cs` (bound `InventoryVM.cs`) | §3 — cell/frame/tab → `UiStyle.Slot(state)` / `UiStyle.Frame.Grid` |
| **Equipment / gear** | `EquipmentPanel.cs:41`, `GearLoadout.cs:29`, `HeroLoadoutPanelMvvm.cs` | own paper-doll slots + chrome branch (`EquipmentPanel.cs:113-114,475-600`) → `UiStyle.Slot` + `UiStyle.Frame.Portrait`; chrome branch dies in `UiStyle` |
| **Consumables** | inventory Consumables tab (`InventoryVM.cs:354`, `InventoryGrid` potion path), `ConsumableUseService.cs` | potion cell/icon → `UiStyle.Slot` + `UiStyle.Icon("potion"/id)` |
| **Skill tree** | `HeroSkillTreePanelMvvm.cs:34` (+ `HeroSkillTreeVM.cs`) | §2 — node plate → `UiStyle.StatePlate(state)` |
| **Dialogue** | `DialogueView.cs:20` (+ `DialogueViewModel.cs`) | own panel/name plate + body font → `UiStyle.Frame.Window` + `UiStyle.Font.Body/Header` |
| **Battle HUD** | `DeNelle.HUD` (`VillageHudController`, bars `ElarionUi.cs:80-83`) | HP/MP fill + frame → `UiStyle.Color.*` + `UiStyle.Frame` (migrate LAST — riskiest, felt-verify) |
| **Modals / scrim / buttons** | `ElarionUiKit.BuildModalCanvas`, `Scrim`, `ButtonPack` | kit defaults → read from `UiStyle` so EVERY modal inherits the theme with no per-call sprite |

Because they ALL read the one `UiStyle`, a single theme swap restyles the whole game in one move
— which is the point of §6.7.

### 6.7 `UiStyle.Try(Style.Obsidian)` — the named, typed, try-able theme lever

The owner wants a NAMED theme you can TRY at the whole-UI level: `UiStyle.Try(Style.Obsidian)`
swaps the ACTIVE theme record and reskins EVERYTHING at once (every screen in §6.6, because they
all read `UiStyle`). The `Style` sits ABOVE the per-concept icon map — frames/palette/fonts/
spacing come from the `Style`'s record; icons still resolve through `ConceptIconResolver` +
`concept-icons.json` underneath (unchanged).

```
enum Style { Default, Obsidian /*, Parchment, … extensible */ }

UiStyle.Active : Style                         // current style (read)
UiStyle.Try(Style s)                           // load that style's UiTheme record -> set active -> raise Changed
UiStyle.Theme  : UiTheme                        // the active record the tokens read (§6.5)
event UiStyle.Changed                           // fired by Try(); live screens re-skin / next-open applies
```

Flow:
1. **Each `Style` maps to ONE `UiTheme` record** — the token bundle (frame sprite names, palette,
   fonts, spacing, default icon). `Style.Default` = today's parchment/stone values
   (`ElarionUi`); `Style.Obsidian` = the §2-§5 Obsidian token set (PanelWindowDark, slot_talent,
   ButtonConfirm-when-chrome, gold-rim states).
2. **`UiStyle.Try(s)`** = resolve `s` → its `UiTheme` (code-default record now, JSON
   `ui-theme.<style>.json` later, §6.5) → assign `UiStyle.Theme` → set `Active = s` → raise
   `Changed`. One call, whole-UI swap. This IS her "try() it" lever at the global level.
3. **Restyle propagation via `Changed`:** the open panel(s) subscribe in their `Open()` and, on
   `Changed`, re-run their existing `Render()`/`Rebuild` (skill tree `HeroSkillTreePanelMvvm.cs:103`,
   inventory `InventoryGrid.RebuildGrid`) — they already fully rebuild from VM + tokens, so a
   rebuild repaints with the new theme for free. Closed panels simply pick it up on next `Open()`
   (no live subscription needed). No new per-screen state — the Views stay dumb.
4. **Icon override stays underneath:** `UiStyle.Icon(conceptId)` still calls
   `ConceptIconResolver.ResolveAny` (`ConceptIconResolver.cs:102`); a `Style` can OPTIONALLY name
   a different `concept-icons.json` variant in its record, but by default all styles share the one
   concept map — the Style governs frames/palette/fonts, not per-concept icon identity.
5. **`ff.blinkchrome` becomes an input to the active record, not a call-site branch:** `UiStyle`
   reads `FeatureFlags.BlinkChrome` in ONE place (`UiStyle.Chrome`) and the Obsidian record's
   `Button(Primary)` / `Color.PanelFill` resolve accordingly. Flipping the flag is effectively a
   sub-variant of the Obsidian style; no screen branches on it.

A debug menu item (`Defenders/Debug/UI Style ▸ Obsidian|Default`) calling `UiStyle.Try(...)` lets
the owner A/B the entire UI live — no recompile, no per-screen edit. That is the singleton's
payoff: ONE lever, every screen.
