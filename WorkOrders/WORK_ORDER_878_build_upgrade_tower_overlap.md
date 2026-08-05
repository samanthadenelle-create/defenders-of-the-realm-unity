# WORK ORDER 878 — Build "Upgrade Tower" panel: text overlaps a hidden button

**Status:** READY. **Lane:** HUD/UI — `BuildMenu.cs` / `BuildMenuVM` (upgrade-tower view). **WO#:** UI-seat block; **878**.
**Source:** `docs/ui-review/screens-2026-08-04/BuildMenuUpgradeTower_2340x1080.png`.

## 1. Bad (from the capture)
The cost/preview text is drawn OVER a button: "Cost: 600 wood, 600 iron, 600 crystals / Lvl 1 to 2: dmg 23.8 to 46.8,
range 18m to 22m" **overlaps a second button** (the Upgrade action button is hidden behind the text, only its top
sliver shows under "Mage Tower (Lvl 1/3)"). The "UPGRADE TOWER" label also clips the "< Back" button corner. Same
fraction-band failure class (WO-841/852).

## 2. Fix — fixed-pixel DISJOINT bands; logic stays in the VM
- Lay the view as stacked **fixed-pixel** bands that cannot overlap: Back · header (name + Lvl) · cost/preview text
  (its own reserved band, wraps) · the **Upgrade/state button** (its own band, ≥ `MinTouchPx`) · Close.
- **MVVM law:** the View only RENDERS strings/state the `BuildMenuVM` provides (name, cost text, affordability, the
  "Not enough Crystals (600)" state). The View computes nothing and must not overlap-stack — no business logic in
  presentation. If any cost/affordability string is assembled in the View today, move it to the VM.

## 3. Acceptance
- [ ] On-device: cost/preview text and the Upgrade button do not overlap; the Upgrade button is fully visible;
      "UPGRADE TOWER" doesn't clip Back. `CompileGate` green. Verify on Seeker.

## 4. Do NOT
- No fraction bands; no computed state in the View (VM provides it). No scene edits.
