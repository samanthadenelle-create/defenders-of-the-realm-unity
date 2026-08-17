> ⚠ **NUMBER COLLISION — this document does not own WO-430; `WORK_ORDER_430_Handover_Triage_Detailed_Work_Orders.md` does.**
> Referred to hereafter as **WO-430-D (UI MVVM seam)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> ownership decided by **first-on-disk** (`git log --follow --diff-filter=A`): the winner's file was created first.
> Banner only — nothing was renumbered or deleted.
> ⚠ **Work HAS shipped under this number** — commit messages and/or a `.RESULT.md` cite WO-430 for THIS document. It is deliberately **not renumbered**; a renumber would orphan those references. Use the alias above when you need to name it unambiguously.

# WORK ORDER 430 — UI MVVM Seam (the binding harness)

**Status: READY TO IMPLEMENT**
**Lane:** Holistic / structural (`ARCHITECTURE_PRINCIPLES.md §3`) — NOT player-facing. Do not
smuggle into any UX change.
**Spec source:** `docs/UI_MVVM_BINDING_MAP.md` §0, §3, §5 step 1.
**Owner directive:** "We always use a standard MVVM model… so no matter what we attach the same
wires to a new UI. UI shouldn't pull from state. We don't ship sloppy architecture." (2026-06-17)

---

## Goal

Create the **binding seam** that every panel View will plug into, so a ViewModel (pure C# state +
commands) can drive ANY View — our `ElarionUiKit` code-built panel today, a Blink Obsidian prefab
tomorrow — without re-wiring. This is the missing load-bearing contract that §2 of the architecture
law assumed but was never built below the HUD. **This WO adds the seam ONLY — it migrates no panel.**

## Why now

Modals (`ShopPanel`, etc.) currently read game state directly (`EconomyService.Instance`) and own
their behavior — a §2 violation. The fix is not "know MVVM"; it's making the right way the only easy
way: once this seam exists, binding to a VM is *less* code than the weld. WO-431 is the first consumer.

## What to build

All in assembly **`DeNelle.Core`**, namespace **`DeNelle.Core.UI`** (Views in their own modules will
reference Core — never the reverse; CLAUDE.md §5). **Pure C# — no `UnityEngine` UI types** (`GameObject`,
`Image`, `Sprite`, `RectTransform`, `Canvas`) anywhere in these files. `Sprite` references are
forbidden in the VM layer; pass sprite **ids/role-names** (string) and let the View resolve via
`RpgUiCatalog`.

### Files to CREATE

1. `Assets/_Modules/Core/UI/Mvvm/IPanelViewModel.cs`
   - `interface IPanelViewModel` — base contract:
     - `string Title { get; }`
     - `event System.Action Changed;` — VM raises when any bound data changes; View re-renders.
     - `void Close();` — the universal close command.
     - `void Dispose();` — unsubscribe from model/services (no leaks; mirror `ShopPanel`'s
       `_ecoHandler` unsubscribe discipline).

2. `Assets/_Modules/Core/UI/Mvvm/IPanelView.cs`
   - `interface IPanelView` — the bind point a View implements:
     - `void Bind(IPanelViewModel vm);` — subscribe to `vm.Changed`, do initial render.
     - `void Unbind();` — detach.
   - Keep it minimal; concrete Views downcast the VM to their specific type in `Bind`.

3. `Assets/_Modules/Core/UI/Mvvm/ItemVM.cs`
   - `readonly struct ItemVM` (value type, no allocations in hot lists): fields
     `string Id, string Name, string IconRole, string IconName, int Price, string CurrencyId,
     bool Affordable, string Rarity, bool Equipped, bool Locked`.
   - This is THE repeating-unit contract (`docs/UI_MVVM_BINDING_MAP.md §1, §3`) — one bound slot
     card serves shop / inventory / loot / crafting / cosmetics.

4. `Assets/_Modules/Core/UI/Mvvm/SlotVM.cs`
   - `readonly struct SlotVM` for equipment/paperdoll/socket slots: `string SlotKey, ItemVM? Content,
     bool Highlighted`.

5. `Assets/_Modules/Core/UI/Mvvm/BarVM.cs`
   - `readonly struct BarVM`: `float Fill01, string Label, string ColorRole`. (HP/MP/cast/progress.)

6. `Assets/_Modules/Core/UI/Mvvm/WalletVM.cs`
   - `readonly struct WalletVM`: a small list/array of `(string CurrencyId, string IconRole, int Amount)`
     — the `BlinkCoinAmount`-style lockup contract. (Use a fixed-size struct or `IReadOnlyList`; no
     per-frame alloc.)

### Files to MODIFY
- None. This is purely additive.

## Acceptance criteria

- [ ] All 6 files compile in `DeNelle.Core` (run the compile gate → `COMPILE_GATE_OK`).
- [ ] **Zero `UnityEngine` UI-type references** in any file (grep: no `GameObject`, `Image`,
      `Sprite`, `RectTransform`, `Canvas`, `MonoBehaviour`). `using UnityEngine;` only if needed for
      `Color`/`Mathf` — prefer not to; pass color as a role string.
- [ ] No new asmdef edges (Core references nothing new).
- [ ] Brace balance check passes on every file (CLAUDE.md §1).
- [ ] XML `<summary>` on each type stating it is a **pure binding contract, View-agnostic**.

## What NOT to touch
- Do NOT modify `ShopPanel`, `EquipmentPanel`, `InventoryUIBuilder`, `ElarionUiKit`, or any HUD
  bridge in this WO. (That's WO-431+.)
- Do NOT add a ViewModel implementation here — contracts/value types ONLY.
- Do NOT reference Blink prefabs.
- §0: UI never edits `.cs` via mount/bash — CLI writes on the Windows path with Write/Edit.

## Tests (permission gate, `ARCHITECTURE_PRINCIPLES.md §2c)`
- Not required for pure data structs/interfaces with no logic. (The behavior gate lands in WO-431
  where the first real ViewModel exists.) If any struct gains a computed property, add an EditMode
  assertion for it.

---
*Cross-ref:* `docs/UI_MVVM_BINDING_MAP.md` (full map), `ARCHITECTURE_PRINCIPLES.md §2/§2c/§3`,
`CLAUDE.md §5`. Next: **WO-431** (shop vertical slice — first consumer of this seam).
