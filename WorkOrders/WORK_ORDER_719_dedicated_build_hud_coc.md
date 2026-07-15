# WORK ORDER 719 — Dedicated Build HUD (Clash-of-Clans-simple chrome)

**Status:** READY TO IMPLEMENT  
**Priority:** P0 (build mode IS the demo)  
**Phase:** 2 (Pillars)  
**Effort:** M–L  
**Depends on:** 717 preferred (real frames/slots); 718 preferred (kit law)  
**Program:** Grok-03  
**Prior analysis:** session build-HUD read-only review · Grok-02 §5 build row  

---

## Goal

Replace the **stacked multi-canvas build overlay** with **one dedicated Build HUD** presentation that owns edit-mode chrome — CoC simplicity: shop + one place bar + one select bar + full wallet — **without** rebuilding `BuildModeController` placement engine.

---

## North-star layout

```
[ Wallet chips W/I/F/G ]     BUILD          [ Done ]
[ world + ghost ]
[ place only: Rotate | PLACE | Cancel ]
[ select only: Move | Upgrade | Sell | Cancel ]
[ Town | Defenses (| Walls if flag) ]
[ shop card grid — icon + cost + FREE ]
```

---

## Tasks

### A — One chrome owner
1. Introduce **`BuildHudController`** (or equivalent single root canvas) that parents:
   - palette dock / tabs / cards  
   - selection strip  
   - place intent bar (merge `BuildPlaceButton` + touch rotate/cancel)  
2. Remove **duplicate** Rotate Left/Right sources (PlaceButton vs LeanTouch bar — **one** family).  
3. Keep `BuildModeHudBridge` hiding combat HUD; build chrome is the only visible UI.

### B — Kit compliance
4. Tabs via **`BuildTabRow`** (Town / Defenses; Walls if `ff.wallstab`).  
5. Header wallet via **`BuildWalletRow`** (wood/iron/food; gold if used) — **not** crystals-only string.  
6. Cards: icon-first kit slots; keep FREE / cost labels (ASCII, CompactNumber).  
7. Orient remains **dev-only** (not next to Done for players).

### C — States
8. **Browse / Placing / Selected** mutually exclusive intent bars (Grok session CoC table).  
9. Optional: collapse shop height while placing (phone).

### D — Engine seams (minimal)
10. Wire existing events only: `OnEntrySelected`, place latch, move/sell/upgrade, Exit.  
11. FlowTrace: `[Flow:BuildHud] state=Browse|Placing|Selected`.

---

## Files (expected)

- `Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs`  
- `BuildSelectionUI.cs` · `BuildPlaceButton.cs` · `LeanTouchBuildDriver.cs`  
- New `BuildHudController.cs` (or similar)  
- Possibly `BuildModeController.cs` (host only — no placement math rewrite)  
- **Do not** touch Village.unity by hand · **do not** greenfield placement grid  

---

## Acceptance

- [ ] Single visual system: no double rotate stacks on device.  
- [ ] Wallet shows multi-resource chips matching card costs.  
- [ ] Tabs kit-based; Town default for !Onboarded.  
- [ ] PLACE still works on WebGL/mobile (explicit button path preserved).  
- [ ] Done exits + saves.  
- [ ] COMPILE_GATE_OK · brace/NUL · owner felt G1.  
- [ ] RESULT with before/after notes + FlowTrace lines.

---

## Not in scope

- Wall drag-lines (WO-708) · ghost validation messages polish · seed town recipes · UITK preview re-enable (if re-enabled, uGUI only in a later WO).

---

## RESULT

`WorkOrders/WORK_ORDER_719_dedicated_build_hud_coc.RESULT.md`
