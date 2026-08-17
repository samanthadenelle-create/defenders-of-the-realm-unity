<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-14
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-14) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 721 — HUD vitals only (fill-contract + clear target)

**Status:** READY TO IMPLEMENT  
**Priority:** P1 (combat trust)  
**Phase:** 3 (Combat UI)  
**Effort:** M  
**Depends on:** 718 kit-law preferred  
**Program:** Grok-03 · **Guidance:** Grok-02 §4.1 · `docs/HUD_OBSIDIAN_ARCHITECTURE_2026-07-03.md` §1.1 only  

---

## Goal

Fix the **felt-broken vitals class** without executing the full 07-03 HUD demolition:

1. HP/MP (and target HP) bars obey the **fill contract** (sprite + `fillAmount` only).  
2. Target frame **fully clears** on no target / death.  
3. Bars use kit Obsidian bar art when present (sprite-first).

**Not** a rewrite of wave chrome, d-pad, chat dock, or hostile/friendly trees.

---

## Tasks

1. **Locate** live vitals painters (`BattleHud9Zone`, `VillageHudController` party rows, `FloatingHealthBar` if in scope).  
2. **Eliminate** sprite-less `Image.Type.Filled` paths (9/145 class).  
3. Route fills through **`BuildObsidianBar`** or a shared helper that enforces the contract.  
4. **`BuildTargetFrame.Clear()`** (or equivalent) on null target — empty name, fill 0, no stale bar.  
5. FlowTrace once when fill set: `cur/max fillAmount=` for low-HP proof.  
6. Optional headless: set hero HP to 9/145, assert fillAmount ∈ (0.05, 0.10) ± tolerance.

---

## Files (expected)

- `Assets/_Modules/Village/Arena/BattleHud9Zone.cs`  
- `Assets/_Modules/HUD/VillageHudController.cs` (vitals only)  
- `Assets/_Modules/Core/UI/ElarionUiKit*.cs` (only if bar API gaps)  
- Possibly `FloatingHealthBar.cs` if world bars still sizeDelta-fill  

---

## Acceptance

- [ ] Screenshot or capture proof: low HP bar **not full**.  
- [ ] Kill target → frame cleared (no full bar under “No Target”).  
- [ ] No regression: bars still update on damage/heal.  
- [ ] COMPILE_GATE_OK · owner gate G3.  

---

## Not in scope

- Full HudKit area JSON · controller cluster redesign · PARRY stamp pool (unless one-line) · party MP model rearchitecture beyond fill binding.

---

## RESULT

`WorkOrders/WORK_ORDER_721_hud_vitals_fill_contract.RESULT.md`
