<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK_ORDER_325 — Nothing happens at resource node (mine upgrade/harvest dead)

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 6 (Economy/Progression) · **Origin:** owner playtest 2026-06-06 (screenshot)
**Reconcile with:** `MineNode` / `CrystalMine` interaction + upgrade, `EconomyService.TrySpend`, NPCCommand/prompt input

## Problem
At a resource node the prompt shows **"[G] Upgrade Mine T1→2 (25 ◆)"**, but pressing **G / interacting does
nothing** — no upgrade, no harvest, no feedback. The dev console shows **NullReferenceException spam**
("Object reference not set to an instance of an object") — likely the interact handler throws and aborts.

## Goal
Interacting at a node works: the **G** action performs the upgrade (spends crystals via EconomyService,
applies the tier change + feedback), and harvest/interact generally functions — no NRE.

## Where to look
- The node interact path (`MineNode`/`CrystalMine`): the **G** input binding → TryUpgrade/Harvest; confirm
  it's wired and not null (the NRE suggests a missing reference — EconomyService instance? prompt target?
  upgrade data? visual?).
- `EconomyService.TrySpend(crystals: 25)` → tier change → `StructureTierVisual`/visual swap + SFX/feedback.
- Null-guard the interact handler so a missing optional (visual/audio) can't abort the whole action.

## Acceptance criteria
- [ ] Pressing G at the node performs the upgrade: 25 crystals spent (if affordable), tier T1→T2 applied, visible feedback.
- [ ] Insufficient resources gives a clear "can't afford" response (not silence).
- [ ] No NullReferenceException on approach/interact/upgrade.
- [ ] Harvest/interact at nodes works generally (not just upgrade).
- [ ] Costs via EconomyService; brace check; CompileGate OK; build SUCCESS; verify in play.

## Root cause (triage 2026-06-06)
**Confidence: Likely (with two corrections).**
- **Correction 1 — source of the prompt.** "[G] Upgrade Mine T1→2 (25 ✦)" comes from **`CrystalMineNode`**,
  not `MineNode`. `CrystalMineNode` owns the [G] upgrade verb + that exact bubble string
  (`Assets/_Modules/Village/World/CrystalMineNode.cs:243`, key `:87`, Update `:147-173`). `MineNode` uses [F]
  to harvest and has no upgrade (`Assets/_Modules/Village/World/MineNode.cs:329`).
- **Correction 2 — economy service.** It spends via **`CrystalEconomy.TrySpend`**, not `EconomyService.TrySpend`
  (`CrystalMineNode.cs:197-207`).
- **Root of "does nothing":** the [G] handler is already null-guarded — `TryUpgrade` requires
  `CrystalEconomy.Instance`; if null it logs a warning and returns (`:197-202`), and if the player's transform
  isn't tagged `"Player"` (`ResolvePlayer` `:235`) `_inRange` never becomes true so [G] is ignored. So the most
  likely cause is **`CrystalEconomy.Instance` is null in the scene** (no bootstrap) or the player tag mismatch —
  not a thrown exception in this handler.
- **On the reported NRE:** the interact handler does NOT throw — the NRE spam the owner saw is the ambient
  WO-328 flood, which makes the (silent) failed upgrade look like a crash. **WO-328 will NOT fix this** — its
  root is separate.

**Suggested minimal fix:** verify/ensure `CrystalEconomy` is bootstrapped in the world scene; confirm the
player is tagged `Player`; add a "can't afford / no economy" feedback path so the press is never silent.

## Do NOT touch
- No `.unity` edits. Don't fork MineNode/EconomyService — fix the wiring + null-guard. Ties WO-228/229/266.
