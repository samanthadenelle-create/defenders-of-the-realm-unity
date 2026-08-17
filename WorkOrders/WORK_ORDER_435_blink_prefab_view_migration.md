> ⚠ **NUMBER COLLISION — this document does not own WO-435; `WORK_ORDER_435_weapon_grip_orientation.md` does.**
> Referred to hereafter as **WO-435-B (Blink prefab view migration)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> ownership decided by **first-on-disk** (`git log --follow --diff-filter=A`): the winner's file was created first.
> Banner only — nothing was renumbered or deleted.

# WORK ORDER 435 — Blink Prefab View Migration (Merchant / Shop)

**Status: SPEC — NOT READY TO IMPLEMENT.**
Blocked on owner sign-off of two decisions (see §0 Open Questions): (1) the A/B flag
name + default, and (2) the target-View scope (PartyShopPanelMvvm vs legacy ShopPanel).
Once the owner confirms those, this flips to READY TO IMPLEMENT.

**WO #:** 435 (provisional). **Confirm against `CLI_LANES_WO_NUMBERS.md` before minting** —
that file currently says "Next free WO = 430", which is stale (430–434 are already in flight:
432 = building tech-tree, 433 = Village2 raid, 434 = NUL-byte compile-gate guard). 435 is the
next free slot above that block; reconcile the counter in `CLI_LANES_WO_NUMBERS.md` +
`MASTER_PIPELINES_BACKLOG_2026-06-06.md` when this is slotted into a lane.

**Lane:** 4 — UI / HUD (presentation only; §9 parallel-safe — does not touch
VillageSceneBuilder, combat, or scene `.unity` files).

**Author:** UI/Claude (spec). **Implements:** CLI (sole code author + committer, CLAUDE.md §2).

**Cross-refs:** `docs/UI_MVVM_BINDING_MAP.md` (the map this WO executes — §2 row "Vendor / shop",
§5 sequencing step 2/4), memory `ui-mvvm-binding-seam`, memory `ui-chrome-composition-and-blink-flag`,
`Assets/_Modules/Core/FeatureFlags.cs`, `ARCHITECTURE_PRINCIPLES.md` §2 (presentation-separation) / §2c
(test-gate) / §3 (leverage, never smuggled into player-facing work), CLAUDE.md §8 (UXML dead in builds —
**prefabs are fine**, this is a `GameObject` prefab not a UIDocument), CLAUDE.md §5 (assembly boundaries).

---

## 0. Open Questions (owner must answer before READY)

1. **Flag name + default.** Proposal: a NEW flag `FeatureFlags.BlinkPrefabView`
   (`Get("blinkprefabview", defaultOn: false)`), kept SEPARATE from the existing
   `BlinkChrome` (which only hides *our* dressing) and from `PartyShop` (which selects
   *which code View* opens). Rationale: `BlinkChrome` is an art-only toggle on the
   code-built View; this WO swaps the *entire View object* for a prefab, an orthogonal
   axis — overloading `BlinkChrome` would make the two states unrepresentable. Default
   **OFF** per the demo law ("ships OFF, flipped ON when proven"). Owner: confirm the
   name `blinkprefabview` and the OFF default, or direct reuse of `BlinkChrome`.

2. **Which View is the migration target?** **CRITICAL — the live shop is NOT `ShopPanel`.**
   `FeatureFlags.PartyShop` defaults **ON** (`FeatureFlags.cs:82`), and when ON `CmdOpenShop`
   routes to `PanelRouter.Open(PanelId.PartyShop)` → **`PartyShopPanelMvvm`** (party selector +
   single-tap buy/equip/sell). The legacy `ShopPanel` only opens when `PartyShop` is OFF.
   So "swap the shop View for the Blink prefab" almost certainly means swap **`PartyShopPanelMvvm`'s
   View**, binding **`PartyShopVM`** — NOT `ShopPanel`/`ShopVM`. Both VMs are already View-agnostic
   (`IPanelViewModel`, no Unity UI types), so the seam exists for either. This spec is written
   **primarily against `PartyShopVM` + `PartyShopPanelMvvm`** (the live path) and notes the
   `ShopVM`/`ShopPanel` deltas inline. Owner: confirm we target the live PartyShop path.

3. **Prefab as content-source vs instantiated-as-View.** Two ways to consume the prefab
   (§4). Recommended: **instantiate the prefab and drive it by named child lookup** (no per-row
   `Button`/icon-resolution lost). Owner: no action unless you prefer option B.

---

## 1. Current State vs Target

### Current (flag OFF — what ships today)
- Shop opens via `CmdOpenShop` → (PartyShop ON) `PanelRouter.Open(PanelId.PartyShop)` →
  `PartyShopPanelMvvm.Open(...)` (`PartyShopPanelMvvm.cs:90`).
- `PartyShopPanelMvvm` is a **code-built** uGUI View (`ElarionUiKit.BuildModalCanvas` +
  `Scrim` + `PanelFramed`, sortingOrder 31000, builds its own Canvas — needs no PanelSettings).
  It constructs `PartyShopVM` (`ConstructViewModel`, line 109), `Bind()`s it, and `Render()`s
  on `vm.Changed`. Registered with `PanelManager.Register("Party Shop", …)` + two
  `PanelRouter.Register(PanelId.PartyShop, …)` openers in `Awake` (lines 71–73).
- The Blink `MerchantPanel.prefab` art already "peeks through" via `BlinkChrome` (our dressing
  hidden, their slot/panel sprites shown) — proving the *art* is swappable. The *object* is not
  yet swapped: we still build every widget in code.

### Target (flag ON — this WO)
- A new `BlinkMerchantView : MonoBehaviour, IPanelView` that **instantiates
  `MerchantPanel.prefab`** (inside our existing modal canvas + scrim + world-occluding backdrop),
  looks up the named prefab children, and binds the **SAME `PartyShopVM`** — repainting the
  prefab's `Title` / `BlinkCoinAmount` / `ArticleSlot`s from `vm.*` on every `vm.Changed`,
  routing taps back as `vm.Act(id)` / `vm.Select(id)` / `vm.SelectMember(i)` / `vm.SetTab(...)` /
  `vm.Close()`.
- Flag OFF ⇒ **pixel-identical to today** (the code-built View is untouched and is the one that
  spawns). Flag ON ⇒ the prefab View spawns instead. Both register the SAME `PanelId.PartyShop`
  and `PanelManager` handle, so they never double-open (mirror the PartyShop-vs-ShopPanel guard).
- `PartyShopVM` / `ShopVM` are **NOT modified** (§7). This is a View swap only — the wires don't move.
  Same pattern as **WO-432**, whose `BuildingUpgradeService` VM-layer is already View-agnostic
  and is swapped behind `FeatureFlags.BuildingUpgradePanel` exactly this way.

---

## 2. The Blink `MerchantPanel.prefab` — verified structure

File: `Assets/Blink/Art/UI/Obsidian_UI/Prefabs_Obsidian/MerchantPanel.prefab` (6635 lines YAML,
read from the actual asset). Root `MerchantPanel` RectTransform = **532.9 × 800** portrait,
sprite root frame.

**Root child order** (`MerchantPanel` RectTransform `m_Children`, prefab:1646):

| RootOrder | Child name | Type / components | Role |
|---|---|---|---|
| 0 | `Title` | TMP text, `m_text: MERCHANT` (prefab:1165) | panel header |
| 1 | `Decoration` | Image (ornamental) (prefab:15) | chrome — no data |
| 2 | (Image / portrait decoration) | Image | chrome — no data |
| 3 | `Icon` | Image, 70×70 (prefab:784) | merchant portrait/crest — no data |
| 4 | `CloseButton` | Image + **`Button`** (prefab:4393, Button at :4455, `m_OnClick` empty) | dismiss |
| 5 | `ArticleSlots` | **`GridLayoutGroup`** (prefab:3090) | the LIST container |
| 6 | `BlinkCoinAmount` | TMP text, `m_text: 525`, gold color (prefab:858) | wallet / currency readout |

**`ArticleSlots`** = `GridLayoutGroup` (`m_CellSize: 224.6×75`, `m_Spacing: 10×10`,
`m_Constraint: 1` FixedColumnCount, `m_ConstraintCount: 2` → **2 columns**, `m_Padding.Top: 15`,
`m_ChildAlignment: 1` UpperCenter; prefab:3127–3150). Holds **12 design-time `ArticleSlot`
children** (placeholder cards baked into the prefab — these are the template to clone/reuse,
NOT a fixed stock count).

**`ArticleSlot`** (the repeating unit — e.g. prefab:3163; each is an `Image` plate,
color `~(0.17,0.15,0.24)`, slot sprite, **NO `Button` component on the slot itself**) has
these named children:

| Child of ArticleSlot | Component | Bound to |
|---|---|---|
| `ItemBackground` | Image, 65×65, anchored left (prefab:551 / 3314) | row art (rarity tint target) |
| `ItemIcon` | Image, child of `ItemBackground`, stretched −8 inset, often `m_Enabled: 0` (prefab:89 / 1399) | the item icon sprite |
| `ItemName` | TMP text, `m_text: Mana Potion` placeholder (prefab:163 / 1473) | item name |
| `Price` | TMP text, `m_text: 25`, gold color, has a `CurrencyIcon` child (prefab:626 / 2016) | item price |
| `CurrencyIcon` | Image, child of `Price` (prefab:4084) | coin glyph next to price |

> **Key gotchas the binding layer MUST handle (verified from YAML, not assumed):**
> - The slot card has **no `Button`** — only an `Image`. The View must **`AddComponent<Button>()`**
>   to each cloned slot (targetGraphic = the slot Image) to make rows tappable. Blink's demo slots
>   were display-only.
> - `ItemIcon` ships **disabled** (`m_Enabled: 0`) on several template slots — enable it when an
>   icon resolves, hide it otherwise (mirror `ShopPanel.RenderDetails`'s `_detailsIcon.enabled`).
> - `CloseButton` is a real `Button` (prefab:4455) with an **empty `m_OnClick`** — wire it in code
>   to `vm.Close()` (Unity-serialized persistent calls can't reference our runtime VM).
> - `MerchantPanel` root is **anchored `0.5,0.5` with `AnchoredPosition x:-53.3`** (off-centre) —
>   when re-parented into our centred modal canvas, **reset `anchoredPosition` to center it**.
> - The prefab has **no ScrollRect, no tabs, no party-selector, no detail pane, no per-currency
>   wallet breakdown** — those are OURS (see §3 "extra elements").

**Siblings** (`PetPanel.prefab`, `QuestPanel.prefab`) are the same Obsidian shape — this WO's
`BlinkMerchantView` pattern generalizes to them later (§8).

---

## 3. Binding Map — prefab child ↔ `PartyShopVM` member ↔ action

The single source of truth for `BlinkMerchantView`. (Member names verified in `PartyShopVM.cs`.)

### Panel-level

| Prefab element (child of `MerchantPanel`) | `PartyShopVM` member | Action (direction) |
|---|---|---|
| `Title` (TMP) | `vm.Title` (string; `PartyShopVM.cs:180`) | render: set `.text` on `vm.Changed` |
| `BlinkCoinAmount` (TMP) | `vm.Coins` (int; `:247`) | render: set `.text = vm.Coins.ToString()` |
| `CloseButton` (Button) | `vm.Close()` (`:182`) | command: `onClick → vm.Close()` |
| `Icon` (Image) | — (`vm` exposes no portrait) | static art — leave as-is |
| `Decoration*` (Image) | — | static art — leave as-is |
| `ArticleSlots` (GridLayoutGroup) | `vm.Items` (`IReadOnlyList<ItemVM>`; `:232`) | render: clone N slots from the template |

### Per-slot (`ArticleSlot` clone) — bound to one `ItemVM`

`ItemVM` exposes (from `ShopVM`/shared): `Id`, `Name`, `IconRole`, `IconName`, `Price`,
`Affordable`, plus rarity. The View resolves the **Sprite** from `IconRole`+`IconName` via
`ItemIconCatalog` / `RpgUiCatalog` (presentation — same resolver as `ShopPanel.ResolveIcon`,
`ShopPanel.cs:232`). The VM never hands over a Sprite.

| Prefab element (child of `ArticleSlot`) | `ItemVM` member | Action |
|---|---|---|
| `ArticleSlot` Image (the plate) | — (selection state) | render: tint with `RowSelectedTint` when `item.Id == vm.SelectedId` (mirror `_rowPlates` hold, `PartyShopPanelMvvm.cs:63`); + **`AddComponent<Button>`** → `vm.Act(item.Id)` |
| `ItemBackground` Image | — / `item.Affordable` | render: optional rarity/affordability tint |
| `ItemIcon` Image | `item.IconRole` + `item.IconName` | render: `sprite = ResolveIcon(role,name)`; `enabled = sprite != null` |
| `ItemName` TMP | `item.Name` | render: set `.text` |
| `Price` TMP | `item.Price`, `item.Affordable` | render: `.text = item.Price + ""` (or "Free"); color = affordable→`Affordable` else `Danger` |
| `CurrencyIcon` Image | — | static coin glyph — leave as-is |
| (slot click) | `vm.Act(item.Id)` (`:293`) — single-tap BUY/EQUIP/SELL | command |
| (slot tap-to-inspect, if a detail pane is kept) | `vm.Select(item.Id)` (`:282`) | command |

### Ours-only elements with NO prefab home (must be ADDED over the prefab, not from it)

`PartyShopVM` exposes more than the Blink demo prefab shows. These stay as our code-built
overlays composited with the instantiated prefab (do NOT drop them):

| Our feature | `PartyShopVM` member | Where it goes |
|---|---|---|
| Party-member selector bar | `vm.Party` (`:199`), `vm.SelectMember(i)` (`:261`), `vm.SelectedMemberIndex` (`:215`), `vm.MemberLabel` (`:218`) | code-built button row (reuse `PartyShopPanelMvvm._partyBar`) above/below the prefab |
| BUY / SELL tabs | `vm.Tab` (`:196`), `vm.SetTab(PartyShopTab)` (`:272`) | code-built tab bar (reuse `_tabBar`) |
| Status line | `vm.Status` (`:244`) | code-built TMP at panel bottom (reuse `_statusText`) |
| Detail / stat-delta pane | `vm.Selected` / `vm.DetailFor(id)` (`PartyShopDetail`, `:237`/`:241`) | code-built right pane (optional — keep if owner wants the delta line) |
| World-occluding backdrop + scrim | (none — View concern) | `ElarionUiKit.BuildModalCanvas` + `Scrim` (we keep ours; Blink only dims) |
| Per-resource wallet (Wood/Iron/Food/Crystals) | `vm.Wood`/`Iron`/`Food`/`Crystals` (`:248–251`) | optional — Blink shows coins only via `BlinkCoinAmount`; keep a code-built strip if desired |

> If the owner instead targets **`ShopVM`/`ShopPanel`** (Open Q #2): the slot binding is identical
> (`ItemVM` is shared); the ours-only set differs — `ShopVM` has **BUY/EQUIP/SELL** (3 modes,
> `vm.SetMode`), a **Type filter bar** (`vm.BuyFilter`/`vm.SetFilter`/`vm.FilterBarVisible`), a
> **detail pane** (`vm.Selected` → `ShopDetail`), and split `vm.Buy()`/`vm.Sell()` commands instead
> of the unified `vm.Act(id)`. Map those the same way (code-built overlays over the prefab).

---

## 4. Approach — instantiate-and-drive (recommended)

**Option A (recommended):** `Instantiate(merchantPanelPrefab)` under our modal canvas; resolve
children by name (`transform.Find("ArticleSlots")`, etc.); cache one `ArticleSlot` as the **row
template**, then `SetActive(false)` the baked design-time slots and clone the template once per
`vm.Items[i]` (destroy clones on each `Render`, like `PartyShopPanelMvvm.ClearContent`). Add a
`Button` to each clone + the `CloseButton` wiring. Keeps the real per-item icon, name, price, and
single-tap action with zero loss.

**Option B (rejected unless owner prefers):** treat the prefab purely as a sprite/style *source*
and keep building widgets in code. That's basically what `BlinkChrome` already does — it would not
deliver the "adopt the prefab wholesale" intent and would leave the weld in place. Note it; do not
implement without owner direction.

**Prefab reference:** load via `Resources` or an Addressables/`AssetReference` enabler (the Blink
bundle already feeds the gear generator via an Addressables enabler — memory
`blink-canonical-art-foundation`). Do NOT hard-code a `guid`. If the prefab fails to load, the View
must **fall back to the code-built path + `FlowTrace.Warn`** (never a blank panel — §12 / WO-465).

---

## 5. Phased Steps

**Phase 0 — Prep (no behavior change).**
- Add `FeatureFlags.BlinkPrefabView` (`Get("blinkprefabview", defaultOn:false)`) + an editor
  toggle under `Defenders/Debug/...` mirroring the `BlinkChrome` `MenuItem` pattern
  (`FeatureFlags.cs:147–166`).
- Add a doc-comment on the flag (the §0/§1 contract).
- No View code yet. Gate: compiles, flag resolves OFF.

**Phase 1 — `BlinkMerchantView` skeleton.**
- New `Assets/_Modules/Village/Hero/BlinkMerchantView.cs`, `namespace DeNelle.Village.Hero`,
  `: MonoBehaviour, IPanelView`. Stub `Bind(IPanelViewModel)` / `Unbind()` (downcast to
  `PartyShopVM`), `Open(vendorContext, displayName)`, `Close()`, `IsOpen`.
- Builds OUR modal canvas + scrim + backdrop, then `Instantiate`s the prefab inside it and
  resolves the named children (assert each is found; `FlowTrace.Fail` + fallback if not).
- Does NOT register with PanelRouter yet. Gate: compiles, no double-register.

**Phase 2 — Spawn-time flag toggle (the A/B switch).**
- In the PartyShop bootstrap (`PartyShopPanelMvvmBootstrap.cs`) — and symmetrically anywhere the
  shop View is spawned — spawn `BlinkMerchantView` **when `BlinkPrefabView` is ON**, else spawn
  `PartyShopPanelMvvm` (today's path). Exactly ONE registers `PanelId.PartyShop` +
  `PanelManager.Register("Party Shop", …)` per the resolved flag, so the two never double-open
  (mirror the existing PartyShop-vs-ShopPanel suppression, FeatureFlags.cs:74–82).
- Gate: flag OFF ⇒ `PartyShopPanelMvvm` spawns (unchanged); flag ON ⇒ `BlinkMerchantView` spawns.

**Phase 3 — Data → prefab mapping (the binding).**
- Implement `Render()` per the §3 table: `Title`, `BlinkCoinAmount`, `CloseButton`, and the
  `ArticleSlots` clone-per-`vm.Items` loop (icon resolve, name, price+color, selection hold, the
  `vm.Act(id)` tap). Composite the ours-only overlays (party bar, tabs, status, optional detail).
- Reuse the proven helpers: `ResolveIcon` logic from `ShopPanel.cs:232`, the `_rowPlates`
  selection-hold from `PartyShopPanelMvvm.cs`, and the never-empty fallback row (a visible
  empty-state card when `vm.Items.Count == 0`, mirror `ShopPanel.CreateEmptyStateRow`).
- Guard every slot build with `Guard.TryEach("Store","build blink slot", vm.Items, …)` so one bad
  `ItemVM` is logged + skipped, never blanks the grid (§12).
- Gate: flag ON renders a working merchant grid bound to live `PartyShopVM`; buy/equip/sell/close
  all work; flag OFF byte-identical to today.

**Phase 4 — Polish + cutover (owner-gated, separate flip).**
- Visual parity pass (grid spacing, the off-centre root re-center, font/scale on our canvas).
- Self-verify via the headless autopilot fleet (the shop oracle asserts `vm.CurrentStock` /
  `CurrentStock` is non-empty and a buy succeeds) in BOTH flag states.
- Only after the owner confirms felt-quality ON: consider flipping the default ON ("unflag when
  proven"). Cutover (deleting `PartyShopPanelMvvm`) is a LATER WO — keep both paths alive here.

---

## 6. Acceptance Criteria

- [ ] `FeatureFlags.BlinkPrefabView` exists, defaults OFF, has an editor toggle + doc-comment.
- [ ] Flag **OFF ⇒ pixel-identical to today** (same `PartyShopPanelMvvm` spawns; nothing about the
      current shop changes — verified by screenshot diff or fleet oracle parity).
- [ ] Flag **ON ⇒** the Blink `MerchantPanel.prefab` is instantiated and is the visible shop;
      `Title`, `BlinkCoinAmount`, and one `ArticleSlot` per `vm.Items[i]` render from the VM.
- [ ] Each rendered slot shows the real item icon (resolved from `IconRole`+`IconName`), name,
      and price (affordable→affordable color, else danger); `ItemIcon` enabled only when a sprite
      resolves.
- [ ] Slot tap fires `vm.Act(id)` (BUY/EQUIP/SELL single-tap); `CloseButton` fires `vm.Close()`;
      party-bar tap fires `vm.SelectMember`; BUY/SELL tab fires `vm.SetTab` — **in both flag states
      the SAME `PartyShopVM` is bound** (one `Bind`, re-render on `vm.Changed`).
- [ ] Exactly ONE View registers `PanelId.PartyShop` + the `PanelManager` handle per resolved flag
      (no double-open; `PanelManager.AnyOpen` true after open, false after close).
- [ ] Empty stock ⇒ a visible empty-state card, never a blank panel; one bad `ItemVM` is logged +
      skipped, never aborts the grid (`Guard.TryEach` + `FlowTrace`).
- [ ] Prefab-load failure ⇒ `FlowTrace.Warn` + fall back to the code-built View (no blank screen).
- [ ] `PartyShopVM.cs` (and `ShopVM.cs`) are **byte-unchanged** (`git diff` empty for both).
- [ ] Brace-balance gate passes on every `.cs` touched; `COMPILE_GATE_OK`; NUL-byte guard clean.
- [ ] A VM-bind regression test (§2c) asserts the new View renders the same `vm.Items` count +
      titles the code-built View does (lock behavior before the swap is trusted).

---

## 7. What NOT to Touch

- **Do NOT modify `PartyShopVM.cs` or `ShopVM.cs`** — no new members, no signature changes. The VM
  is the wire harness; this WO only adds a new View that plugs into it. If a binding "needs" a VM
  change, STOP and bounce back — that's a separate VM WO.
- **Do NOT change the economy / catalog / equip seams** (`IEconomy`, `InventoryStore`,
  `IEquipTarget`, `GearCatalog`, `StoreStockService`). Buy/sell/equip math is unchanged.
- **Do NOT edit the `MerchantPanel.prefab` asset** to wire our logic into `m_OnClick` (Unity
  persistent calls can't reach our runtime VM, and editing the vendored Blink asset diverges it
  from the pack). All wiring is in code at instantiate-time. A *minimal* prefab edit (e.g. adding a
  `Button` component to the slot template) is acceptable ONLY if done as a code `AddComponent` at
  runtime, not baked into the asset — prefer runtime.
- **Do NOT flip any default ON** in this WO (including `BlinkPrefabView`). Default stays OFF;
  flipping is a later owner-gated step (§5 Phase 4).
- **Do NOT delete `PartyShopPanelMvvm`** (or `ShopPanel`). Both paths stay alive through the
  transition; removal is a future cutover WO.
- **Do NOT use UXML / UIDocument** (§8) — the prefab is a uGUI `GameObject` prefab; that's allowed.
- **Do NOT touch any `.unity` scene file** (the View self-spawns its canvas).

---

## 8. Risks

- **Layout-binding differences.** The prefab uses a 2-col `GridLayoutGroup` (fixed 224.6×75 cells)
  vs our `VerticalLayoutGroup`+`ContentSizeFitter`+`ScrollRect` list. The prefab has **no
  ScrollRect** — a long stock list will overflow the 800px panel. Mitigation: add a `ScrollRect` +
  `RectMask2D` around `ArticleSlots` at instantiate-time (our list mechanism is the cured "no-stock"
  fix, guarded by `ShopPanelRowRenderTests` — preserve that contract).
- **Extra prefab elements to wire or hide.** `CloseButton` has an empty `m_OnClick`; `ItemIcon`
  ships disabled; the root is off-centre (`x:-53.3`). Each must be handled in code (§2 gotchas) or
  the panel looks broken. The 12 baked placeholder `ArticleSlot`s must be hidden/destroyed so only
  VM-driven clones show.
- **Missing prefab elements we depend on.** No party-selector, no BUY/SELL tabs, no detail pane, no
  per-resource wallet in the prefab — all must be composited as our code-built overlays. If they're
  forgotten, ON-state loses features the OFF-state has (a regression masquerading as a "skin").
- **Testing BOTH flag states.** Every acceptance check runs twice (ON and OFF). The fleet oracle
  must run in both. A green ON-state that silently broke OFF-state is the failure mode to guard.
- **Prefab/asset availability.** The Blink bundle is vendored but icon sprites resolve through
  `ItemIconCatalog`/Addressables — a missing-bundle environment must degrade (warn + fallback), not
  hard-fail.
- **Selection-hold + affordability tint** on a sprite-9-sliced slot plate (Blink slots are
  `Image.Type.Sliced`, white) behaves differently from our procedural rounded `Cell` — verify the
  multiply-tint reads correctly on the Blink plate (same concern as `DressRowPlate`,
  `ShopPanel.cs:629`).

---

## 9. Generalization Note (later WOs, not this one)

`MerchantPanel`, `PetPanel.prefab`, and `QuestPanel.prefab` are the same Obsidian shape (panel
frame + `Title` + `CloseButton` + a layout-group list of slot cards). Once `BlinkMerchantView`
proves the instantiate-and-drive pattern, factor the shared mechanics (instantiate-into-our-canvas,
named-child resolve, clone-slot-per-`vm.Items`, `CloseButton`→`vm.Close`, never-empty fallback,
`Guard.TryEach` slots) into a small reusable `BlinkPanelView` base, and migrate `PetPanel`
(`PetVM`) and `QuestPanel` (`QuestVM`) the same way — each behind its own flag, tests-first
(`docs/UI_MVVM_BINDING_MAP.md` §5 step 4). That is the leverage payoff: one prefab-View harness,
every Obsidian panel adopted wholesale. **Out of scope for WO-435** — note only.
