# UI Presentation Layer — MVVM Binding Map (Ours ↔ Blink Obsidian)

**Status:** DESIGN SPEC (owner-directed 2026-06-17). UI/Claude authored; CLI implements
behind work orders. **This is the map, not a green light to refactor** — sequencing +
permission-gate tests (§2c of `ARCHITECTURE_PRINCIPLES.md`) gate each migration.

**Owner directive that produced this (2026-06-17):**
> "Shouldn't we always use a standard MVVM model anyway — to remove problems like that?
> So that no matter what, we attach the same wires to a new UI. **UI shouldn't pull from state.**"
> "Step back and look at the entire object, the container holding it, how is it constructed,
> how can we map to that. We have proof already — we can see theirs peeking through."

---

## 0. Why MVVM (and why it's already our law, just informal)

`ARCHITECTURE_PRINCIPLES.md §2` already says: **"Presentation is a separate layer that
NEVER touches the objects. Objects expose state; the presentation layer observes and
renders."** MVVM is the *formal mechanism* for that rule. Today we honor it inconsistently:

- ✅ HUD does it: `*HudBridge` classes feed `IVillageHud` via `CoreServices.Hud`; the HUD
  renders (WO-403). That's a ViewModel seam in all but name.
- ❌ Modals violate it: `ShopPanel` reads `EconomyService.Instance` directly, builds rows
  from catalog calls, and calls buy logic inline. The View *pulls from state* and *owns
  behavior* — so the art and the wiring are welded together. **That weld is exactly why
  "our UI sits on top of theirs": there's no seam to swap the View at.**

### The contract (the "wire harness")

```
   MODEL                     VIEWMODEL                         VIEW
 (game state,            (per-panel binding contract)     (the skin — swappable)
  services, catalog)     ─ exposes read-only DATA  ───────▶  binds fields to widgets
        ▲                ─ raises CHANGE events    ───────▶  re-renders on change
        │                ─ accepts COMMANDS        ◀───────  raises intent (no logic)
        └──── commands mutate model, model change re-feeds the VM ───┘
```

**Three hard rules (these are the win):**
1. **The View never reads game state and never calls a service.** It binds to VM data and
   raises VM commands. (Fixes the §2 violation at the root.)
2. **The ViewModel never references a `GameObject`, `Image`, `Sprite`, or `RectTransform`.**
   It's pure C#, unit-testable without a scene (§2c permission gate falls out for free).
3. **The same VM binds to ANY View** — our `ElarionUiKit` code-built panel, a Blink
   `MerchantPanel.prefab`, or a future one. Swap the skin; the wires don't move.

> **The proof is already on screen:** the Blink panel sprite shows *through* our chrome.
> That proves the *art* is swappable. MVVM makes the *wiring* swappable too — the other half.

---

## 1. The repeating-unit insight (how Blink constructs a panel)

Every Blink Obsidian panel is the same shape, and so is every panel of ours — once you see
it, the whole map is one pattern repeated:

```
CONTAINER (fixed panel-frame sprite, portrait)
├─ chrome/decoration (ornamental — pure View, no data)
├─ header / title            ← bound to VM.Title
├─ currency / wallet lockup  ← bound to VM.Wallet
├─ close button              ← raises VM.Close()
└─ LIST CONTAINER (LayoutGroup)         ← bound to VM.Items  (IReadOnlyList<ItemVM>)
   └─ SLOT/ROW (the repeating unit)      ← bound to one ItemVM
      ├─ background plate     ← View art
      ├─ icon                 ← bound to ItemVM.Icon
      ├─ name                 ← bound to ItemVM.Name
      ├─ price (+ currency)   ← bound to ItemVM.Price / .Affordable
      └─ (click)              ← raises VM.Select(item) / VM.Buy(item)
```

**Blink's `MerchantPanel` concretely** (reconstructed from the prefab):
`MerchantPanel` (533×800 portrait) → `Title`, `BlinkCoinAmount` (Icon+amount), `CloseButton`,
`ArticleSlots` (**GridLayoutGroup**, 2-col, cell 224.6×75, spacing 10) → repeated
`ArticleSlot` = `ItemBackground` + `ItemIcon` + `ItemName` + `Price`(+`CurrencyIcon`).
No ScrollRect, no detail pane, no tabs in the demo — **each item is a self-contained slot
card.** Ours uses bare text rows over a shared `Well` with a `View` button and no icon — same
data, weld instead of seam.

---

## 2. Panel-by-panel map (Ours ↔ Blink ↔ the ViewModel that wires both)

> VM = the binding contract. **Data** = read-only fields the View shows. **Commands** = intent
> the View raises. Build VMs on the existing `IVillageHud`/`CoreServices`/`PanelRouter`/`*Bridge`
> seams — reconcile, don't greenfield (`ARCHITECTURE_PRINCIPLES.md §3`).

| Domain | Our View (file) | Blink View (prefab) | ViewModel contract (the wires) |
|---|---|---|---|
| **Vendor / shop** | `Village/Hero/ShopPanel.cs` | `MerchantPanel` | `ShopVM`: Data{ Title, Wallet, Items[ItemVM], Selected }; Cmds{ SetMode(buy/sell), Select(id), Buy(id), Sell(id), Close } |
| **Inventory** | `Village/Hero/InventoryUIBuilder.cs` | `Inventory` | `InventoryVM`: Data{ Tabs, Slots[ItemVM], Weight/Cap }; Cmds{ SelectTab, Select(slot), Use/Equip(slot), Drop(slot), Close } |
| **Equipment** | `Village/Hero/EquipmentPanel.cs` | `Inventory` (paperdoll) / `Characters` | `EquipVM`: Data{ Portrait, Stats, Slots[SlotVM], HP/MP }; Cmds{ Equip(slot,item), Unequip(slot), Close } |
| **Crafting** | `Village/Crafting/VillageCraftingPanel.cs` | `Crafting` | `CraftVM`: Data{ Recipes[RecipeVM], Selected, CanCraft, Inputs }; Cmds{ Select, Craft, Close } |
| **Enchant / socket** | *(none yet)* | `Enchanting`, `Socketing` | `EnchantVM`/`SocketVM`: Data{ Item, Slots, Gems, Cost }; Cmds{ Apply, Remove, Close } |
| **Building upgrade** | `Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs` (UIDocument twin DELETED 2026-07-02) | Obsidian master frame (FrameCore) | `BuildingUpgradeVM`: Perks grid{ id, name, effect, cost, state }; CostFor/EffectFor/LockReason; Cmds{ UnlockPerk, Close } |
| **Talent / tech tree** | `HUD/HeroTalentPanel.cs` | `TalentTree` | `TalentVM`: Data{ Nodes[NodeVM], Points }; Cmds{ Learn(node), Reset, Close } |
| **Pet / companion** | `HUD/PetSkillTreePanel.cs` | `PetPanel` | `PetVM`: Data{ Pet, Skills[NodeVM], Bond }; Cmds{ Learn, Summon/Dismiss, Close } |
| **Cosmetic / monetize** | `HUD/CosmeticShopPanel.cs` | `MerchantPanel` (reskinned) | `StoreVM`: Data{ Packs[ItemVM], Wallet }; Cmds{ Buy, Close } |
| **Quest log / tracker** | *(tracker via bridges)* | `QuestLog`, `QuestPanel`, `QuestTracker` | `QuestVM`: Data{ Active[QuestVM], Selected, Objectives }; Cmds{ Track, Abandon, Close } |
| **Loot** | *(item pickup via `ItemHud`)* | `Loot` | `LootVM`: Data{ Drops[ItemVM] }; Cmds{ Take(id), TakeAll, Close } |
| **Dialogue** | `DialogueUI/CompanionDialoguePresenter.cs` | `Dialogues` | `DialogueVM`: Data{ Speaker, Line, Options[] }; Cmds{ Choose(i), Advance, End } |
| **HUD core (bars)** | `HUD/VillageHudController.cs` (+ `IVillageHud`) | `HUDCore`, `HUDCore_Diablo` | `HudVM`: Data{ HP, MP, Wave, Currency, Context }; (read-mostly, fed by `*HudBridge`) |
| **Nameplates** | *(enemy/NPC world labels)* | `TargetNameplate`, `PartyNameplate` | `NameplateVM`: Data{ Name, HP, Level, Hostile } |
| **Cast/progress bars** | *(wave/cooldown feedback)* | `CastBar1-3`, `Bar1-7`, `DiabloHealth/Mana` | `BarVM`: Data{ Fill01, Label, Color } |
| **Game menu / settings** | `Settings/SettingsModel.cs` + menu | `GameMenu` | `MenuVM`: Cmds{ Resume, Settings, Quit, … } |
| **Title / onboarding** | `Onboarding/TitleController.cs` | `LoginScreen`, `LoadingScreen` | `TitleVM`: Cmds{ Start, Continue, … } |
| **Character select/create** | *(none — single hero)* | `CharacterSelection`, `CharacterCreation`, `Characters` | *(future; map when multi-hero lands)* |
| **Chat / minimap** | *(none)* | `Chat`, `Minimap` | *(out of scope for now)* |

**Ours-only (additive, no Blink demo equivalent — keep, they're features):** Buy/Sell/Equip
**tabs** in shop, the right-hand **detail pane**, the full-screen **scene-occluding backdrop**
(`ShopBackdrop`/`EquipBackdrop` — we need it; Blink dims the game instead), **arena/raid/wave**
HUD bridges, **build-mode** HUD.

**Blink-only (we lack — candidates to adopt):** `Enchanting`, `Socketing`, `Loot`, `Chat`,
`Minimap`, nameplates, character create/select.

---

## 3. Shared primitives map (our kit ↔ Blink components)

The repeating units are themselves a small, fixed kit. Map once, reuse everywhere.

| Our `ElarionUiKit` primitive | Blink Obsidian component | Bound to |
|---|---|---|
| `PanelFramed` (panel-frame sprite) | panel root frame (`MerchantPanel`/`Inventory` root) | container, no data |
| `Header` (crest + title + rule) | `Title` + decoration | `VM.Title` |
| `Card` / `Slot` (rarity-framed tile) | `ArticleSlot` / inventory cell (`ItemBackground`+`ItemIcon`) | one `ItemVM` |
| `Well` (recessed tray) | (Blink uses per-slot plates; no shared well) | list backing only |
| `Niche` (portrait alcove) | paperdoll portrait frame | `VM.Portrait` |
| `Button` / `ButtonPack` | `Rectangle*/Rounded*` buttons, `Close_Button` | a `Command` |
| *(toggle — none)* | `Toggle1-3` | a bool `Command` |
| *(bar — HUD only)* | `Bar1-7`, `CastBar1-3`, `DiabloHealth/Mana` | `BarVM.Fill01` |
| currency label (ad hoc) | `BlinkCoinAmount` (Icon+amount lockup) | `VM.Wallet` |

**Implication:** define a `Slot` View contract (`ItemVM` → plate+icon+name+price+click) once,
and every list panel (shop, inventory, loot, crafting, cosmetics) reuses it with a different
`IReadOnlyList<ItemVM>`. That single unit is ~70% of the visual surface.

---

## 4. How this removes "problems like that" (the owner's actual ask)

- **"Attach the same wires to a new UI":** the VM is the wire harness. A Blink prefab View and
  our code-built View implement the *same* `IView` bind points → swapping art never re-wires logic.
- **"UI shouldn't pull from state":** enforced — Views hold no service refs; the `ShopPanel →
  EconomyService.Instance` weld (and its siblings) is cut. State flows in via VM; intent flows
  out via commands.
- **"Theirs peeking through":** the BlinkChrome flag already proved art-swap. MVVM finishes the
  job so we can adopt a Blink prefab wholesale (not just its sprite) without touching gameplay.
- **Bonus (free permission gate, §2c):** VMs are pure C# → unit-testable. Each migration ships
  with a VM test that locks behavior; the View swap passes only if green. No "fixed" on faith.

---

## 5. Sequencing (leverage order — to be cut into WOs, tests-first per §2c)

1. **Define the seam:** `IPanelView` + `ItemVM`/`SlotVM`/`BarVM` value types in `DeNelle.Core.UI`
   (pure data, no Unity refs). The harness everything plugs into.
2. **Shop vertical slice:** extract `ShopVM` from `ShopPanel` (move `EconomyService`/catalog/buy
   logic out of the View into the VM); re-point `ShopPanel` to bind it. Ships with `ShopVM` tests.
   Proves the seam on the panel we're already staring at.
3. **Generalize the `Slot` unit:** one bound slot-card used by inventory, loot, crafting, cosmetics.
4. **Roll the rest** down the §2-leverage order (inventory → equipment → crafting → quests → …),
   each: extract VM (tests) → rebind our View → optionally drop in the Blink prefab View.
5. **Adopt Blink-only panels** (enchant/socket/loot) last, greenfield-bound to fresh VMs.

**NOT in this doc's mandate:** doing the refactor. This maps the territory; each numbered step is
its own work order, queued as **holistic/leverage** work (§3) — never smuggled into a player-facing
change, always behind the test gate.

---

*Cross-refs:* `ARCHITECTURE_PRINCIPLES.md` (§2 presentation-separation, §2c test gate, §3 leverage),
`CLAUDE.md` (§5 assembly boundaries — VMs live in `DeNelle.Core.UI`, Views in their module),
`docs/BLINK_UI.md` (sprite re-skin + BlinkChrome flag), memory `ui-chrome-composition-and-blink-flag`.
