# WORK ORDER 844 — Bag "Use" actually applies the potion effect

**Status:** IMPLEMENTED — pending gates (CompileGate + EditMode run by the CLI committer)
**Author:** edit-only implementation agent (proven RCA supplied by orchestrator)
**Lane:** Hero/Inventory — `InventoryVM.cs` + new `BagConsumableUseEffect.cs` + `InventoryVMTests.cs`.
**Origin:** owner felt-test — *"using potions does nothing should restore HP."*

---

## 1. RCA — proven (file:line)

The Bag's Use verb consumed the item with ZERO effect applied:

- `Assets/_Modules/Village/Hero/InventoryVM.cs` (pre-fix `Use()`, old :241-256) called ONLY
  `_store.TryRemove(SelectedId, 1)` then set Status "Used X." — a pure inventory decrement,
  no heal, no mana, nothing. The toast lied.
- The real effect authority exists and works: `Assets/_Modules/Village/Items/ConsumableUseService.cs`
  `TryUse(id, inFight)` — gate checks (:64-97), consume from `VillageInventory` (:100), then
  `ApplyEffect` (:111-139): Heal -> `HeroHealth.Heal` + Hovl heal VFX (:147-179), Mana ->
  `HeroAbilities.RestoreManaOverTime` (:186-204), plus the authored use-cooldown (:104-106).
- Only the two battle belt slots routed through it: `Assets/_Modules/Village/HUD/HudKitCommandBridge.cs:90-100`.
  The Bag never did.

## 2. Fix shape — one effect authority, seam-injected (MVVM kept)

- **New seam on the VM:** `InventoryVM` ctor takes `Func<string, InventoryUseResult> useEffect`
  (optional, null in tests). `InventoryUseResult` (same file) carries `Applied` + player-facing
  `FailReason`. **Decrement contract:** `Applied == true` means the SEAM already consumed the item
  (exactly what `TryUse` does at ConsumableUseService.cs:100) — `Use()` never calls `TryRemove`
  anymore, so one drink can never double-spend. The VM stays pure + fake-testable.
- **Real wiring:** `InventoryVM.CreateDefault` (the sole DI site) binds the new
  `Assets/_Modules/Village/Hero/BagConsumableUseEffect.cs` — honest pre-gates (feature dark /
  non-catalog material / rest-only mid-fight via live `BattleLock.IsInBattle()` / cooldown running /
  heal+rest at full HP) each refuse WITH a truthful reason and consume NOTHING (TryUse spends
  the moment it runs, so the gates sit before it), then `ConsumableUseService.TryUse(id, inFight)`
  applies the same spend+effect+VFX+cooldown path the battle belt fires.
- **Honest feedback:** "Used X." only on an applied effect; refusal surfaces the reason
  ("Already at full health." / "Cannot be used during a fight." / "Ready in Ns." / "That cannot
  be used.") and the item is KEPT. No seam bound -> `FlowTrace.Warn` (effect-path-missing) +
  "Nothing happens." + item kept — never consume-for-nothing.
- **Instrumentation (SS12):** `FlowTrace.Step("Inventory", "Use '<id>' -> APPLIED/refused: <reason>")`
  in `Use()`; `FlowTrace.Warn` on the missing-seam path. No silent catch anywhere.

## 3. Id reconciliation (verified)

- consumables.json header: *"A consumable's 'id' is also its VillageInventory larder key"* —
  the Bag's Consumables tab projects straight from `VillageInventory.Counts`
  (`InventoryStore.OwnedConsumables()`, IInventoryStore.cs:182-198), so Bag ids ARE the ids
  `ConsumableCatalog`/`TryUse` know. The WO-609 belt ids `HpPotionId='minor-heal-potion'` /
  `ManaPotionId='cons_mana_draught'` (HudCommands.cs:35,38) are both authored catalog rows —
  same store, same ids: **Bag count and belt count always agree** (both decrement the one
  `VillageInventory.Instance`).
- The Consumables tab is also the owned-item catch-all (crafting materials, drops). Those have
  no catalog row -> the adapter refuses "That cannot be used." and keeps them (previously they
  were silently destroyed by Use).

## 4. Files touched

- `Assets/_Modules/Village/Hero/InventoryVM.cs` — `InventoryUseResult`, seam field/ctor param,
  `CreateDefault` binds the real adapter, `Use()` rewritten (seam-routed, honest status, FlowTrace).
- `Assets/_Modules/Village/Hero/BagConsumableUseEffect.cs` — NEW: the real effect wiring
  (pre-gates + `ConsumableUseService.TryUse`).
- `Assets/Tests/EditMode/InventoryVMTests.cs` — old `use_consumable_calls_store_and_raises_changed`
  (which locked the BUG: remove-only == "Used") replaced by four WO-844 tests:
  applied-consumes-once+`Used`, refused-keeps-item+truthful-reason, no-seam-keeps-item,
  gear-never-reaches-seam.

## 5. Acceptance criteria

- [ ] EditMode: the four WO-844 `InventoryVMTests` green (fake seam — no scene).
- [ ] `CompileGate` green (`COMPILE_GATE_OK` marker verified, not exit code).
- [ ] Owner felt-check: at low HP, Bag -> Consumables -> potion -> Use: **HP visibly restores**
      (same heal VFX as the belt slot), toast "Used Minor Healing Draught.", count drops by 1.
- [ ] Owner felt-check: at FULL HP, Use: toast "Already at full health.", **item NOT consumed**.
- [ ] Belt + Bag counts stay in sync after using from either surface.
- [ ] A crafting material in the Consumables tab refuses "That cannot be used." and is kept.

## 6. Do NOT

- Do NOT re-add a `TryRemove` to `Use()` — the seam owns the decrement (double-spend hazard).
- Do NOT give the Bag its own heal math — `ConsumableUseService` is the single effect authority.
- Do NOT touch `HudKitCommandBridge` belt slots, EconomyService, BuildMode, or StructureSingleton
  files (other lanes own them).
