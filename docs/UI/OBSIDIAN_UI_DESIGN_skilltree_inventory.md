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
