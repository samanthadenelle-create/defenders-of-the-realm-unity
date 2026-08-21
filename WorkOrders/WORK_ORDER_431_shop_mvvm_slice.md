**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

> ⚠ **NUMBER COLLISION — this document does not own WO-431; `WORK_ORDER_431_raid_rewards_victory.md` does.**
> Referred to hereafter as **WO-431-B (shop MVVM slice)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> ownership decided by **first-on-disk** (`git log --follow --diff-filter=A`): the winner's file was created first.
> Banner only — nothing was renumbered or deleted.
> ⚠ **Work HAS shipped under this number** — commit messages and/or a `.RESULT.md` cite WO-431 for THIS document. It is deliberately **not renumbered**; a renumber would orphan those references. Use the alias above when you need to name it unambiguously.

# WORK ORDER 431 — Shop Vertical Slice: extract ShopVM (first MVVM consumer)

**Status: READY TO IMPLEMENT**
**Depends on:** WO-430 (the MVVM seam) — must land first.
**Lane:** Holistic / structural (`ARCHITECTURE_PRINCIPLES.md §3`). Behavior-preserving refactor; no
new player-facing feature. Do NOT change shop visuals or flow in this WO.
**Spec source:** `docs/UI_MVVM_BINDING_MAP.md` §2 (Vendor row), §4, §5 step 2.
**Owner directive:** "UI shouldn't pull from state… we don't ship sloppy architecture." (2026-06-17)

---

## Goal

Cut the weld in `ShopPanel`: move ALL state/logic (economy reads, catalog→row building, buy/sell
execution, affordability) **out of the View into a pure `ShopVM`**, and re-point `ShopPanel` to
**bind** the VM instead of pulling from services. After this, `ShopPanel` is a dumb skin — and a Blink
`MerchantPanel` prefab could bind the same `ShopVM` with zero logic changes. Prove the seam on the one
panel we've been staring at.

**Behavior must be identical** — same items, prices, tabs (Buy/Equip/Sell), affordability colors, buy
results, status messages. This is a move, not a redesign.

## Current violation (what we're fixing)

`Assets/_Modules/Village/Hero/ShopPanel.cs` today:
- Reads `EconomyService.Instance` / `EconomyService.Instance.CanAfford(...)` directly in the View.
- Builds rows by calling catalog/inventory code inline (`BuildScrollContent`, per-row name/price).
- Executes purchases inline (`buyAction`, `_selectedAction`, the `Purchased …` status strings).
- Subscribes to economy changes via `_ecoHandler` in the View.

All four belong in the ViewModel, not the View (`docs/UI_MVVM_BINDING_MAP.md §0` rule 1: the View
never reads game state and never calls a service).

## What to build

### CREATE `Assets/_Modules/Village/Hero/ShopVM.cs`  (assembly `DeNelle.Village`, pure C#, no Unity UI)
A class implementing `DeNelle.Core.UI.IPanelViewModel`:

- **Construction:** `ShopVM(string vendorContext, IEconomy economy, IShopCatalog catalog)` — inject the
  seams; do NOT new up `EconomyService.Instance` inside (resolve via constructor / a thin provider so
  the VM is unit-testable without a scene). If no economy interface exists yet, wrap the concrete
  `EconomyService` behind a minimal `IEconomy { bool CanAfford(...); bool TrySpend(...); event Changed }`
  in `DeNelle.Core` and adapt — reconcile, don't rewrite EconomyService.
- **Data (read-only props):**
  - `string Title` (vendor display name)
  - `WalletVM Wallet`
  - `ShopMode Mode` (Buy/Equip/Sell enum)
  - `IReadOnlyList<ItemVM> Items` (for the active mode; affordability already computed)
  - `string SelectedId`, `ItemVM? Selected` (drives the detail pane)
  - `string Status` (the status-line text)
  - `string ActionLabel` ("Purchase"/"Sell")
- **Commands:** `void SetMode(ShopMode)`, `void Select(string id)`, `void Buy()`/`void Sell()` (acts on
  Selected), `void Close()`.
- **Changed event:** raise after any command mutates state, and on economy `Changed`, so the View
  re-renders. Unsubscribe in `Dispose()`.
- All the buy/sell/affordability/status logic moves here verbatim from `ShopPanel` — same outcomes.

### MODIFY `Assets/_Modules/Village/Hero/ShopPanel.cs`  (becomes the View)
- Implement `DeNelle.Core.UI.IPanelView`.
- In `Bind(vm)`: store `(ShopVM)vm`, subscribe `vm.Changed → Render()`, do initial `Render()`.
- `Render()` reads ONLY `vm.*` data to populate widgets (header←Title, currency←Wallet, tabs←Mode,
  list←Items, detail←Selected, footer label←ActionLabel, status←Status).
- Tab buttons call `vm.SetMode(...)`; row click calls `vm.Select(id)`; Purchase calls `vm.Buy()`;
  Close calls `vm.Close()`. **No `EconomyService`, no catalog calls, no buy logic left in the file.**
- Keep ALL existing visuals exactly as they are (the BlinkChrome gating, gold Purchase button, framed
  View buttons, Well, detail pane, backdrop). This WO does not restyle.
- Whoever opens the shop (the vendor interaction / `PanelRouter`) constructs the `ShopVM` and calls
  `shopPanel.Bind(vm)` — wire at the existing open-site; do not add a parallel router.

### MODIFY (only if needed) `Assets/_Modules/Core/.../IEconomy.cs`
- Add the minimal economy interface IF one doesn't already exist, and have `EconomyService` implement
  it. Additive only.

## Acceptance criteria
- [ ] Compile gate passes (`COMPILE_GATE_OK`), brace balance on every edited file (CLAUDE.md §1).
- [ ] `ShopPanel.cs` contains **zero** references to `EconomyService`, catalog lookups, or buy
      execution (grep to confirm). It only binds/render/raises commands.
- [ ] `ShopVM.cs` has **no** `GameObject/Image/Sprite/RectTransform/MonoBehaviour` references.
- [ ] In-game behavior is **identical**: open each vendor (Forge/Armorer/Jeweler/Arcane/Market/Lumber +
      default), Buy/Equip/Sell tabs, affordability colors, purchase adds to inventory, status strings
      match. (Owner felt-retest before push.)
- [ ] No memory leak: `ShopVM.Dispose()` unsubscribes the economy handler; `ShopPanel` calls it on close.

## Tests (PERMISSION GATE — required, `ARCHITECTURE_PRINCIPLES.md §2c)`
CREATE `Assets/_Modules/Village/Tests/ShopVMTests.cs` (EditMode, no scene), using a fake `IEconomy`:
- [ ] Items list for each mode is non-empty and prices match the catalog.
- [ ] `Affordable` flips with the fake wallet balance.
- [ ] `Buy()` on an affordable Selected spends and raises `Changed`; on unaffordable it does not spend
      and sets the "select/can't afford" status.
- [ ] `SetMode` swaps the Items list and resets Selection.
- [ ] `Dispose()` unsubscribes (no callback after dispose).
This is what makes the refactor "done" — the View swap is safe only because these stay green.

## What NOT to touch
- Do NOT restyle the shop or change layout — behavior/visual parity only.
- Do NOT migrate inventory/equipment/crafting here (later WOs, same pattern).
- Do NOT introduce a Blink prefab View yet — that's a follow-on once the seam is proven.
- Do NOT greenfield EconomyService or the catalog — wrap/adapt behind thin interfaces.
- §0: CLI writes on Windows path (Write/Edit), never mount/bash.

---
*Cross-ref:* `docs/UI_MVVM_BINDING_MAP.md §2/§4/§5`, WO-430 (seam), `ARCHITECTURE_PRINCIPLES.md
§2/§2c/§3`. After this proves out: generalize the bound `Slot` unit (map §5 step 3), then roll
inventory → equipment → crafting → quests.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
