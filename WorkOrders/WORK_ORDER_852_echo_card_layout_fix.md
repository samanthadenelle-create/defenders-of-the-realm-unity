# WORK ORDER 852 â€” Echo card: fixed-band layout so the resource picker is clean + usable

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Author:** UI/QA triage (read-only RCA, Â§13) â€” Claude UI
**Lane:** HUD/UI â€” `EchoCardView.cs` (+ possibly `EchoCardVM.cs` copy). View-layout only; no economy/VM-logic change.
**Origin:** owner felt-test 2026-08-02, ECHO card (WO-830 resource picker) â€” *"read-only RCA this so it's clean and
loads so it can be used."* Same bug class as WO-832 Â§4 / WO-841 (fraction bands vs fixed pixel line boxes).

---

## 1. Symptom (owner screenshot)
On the ECHO card (Elowen, "Echo 2 of 2"), the **info block overlaps the resource-picker buttons**: the affinity/
synergy text ("Favors: Wood", "Gathering Wood - Lv 1 - +55% (best)", "Provisions synergy â€¦ ACTIVE (+10% all
harvest)") is drawn ON TOP of the top picker chips (Wood/Iron/Food/Gold). Only the **bottom chip ("Crystals")** is
fully readable/tappable. The picker is not usably interactive. (The affinity/synergy TEXT itself is correct â€” WO-830
is working; this is purely layout.)

## 2. RCA â€” fraction-of-body bands collapse (sourced from live code)
`EchoCardView.Build()` (`Assets/_Modules/Village/Harvest/EchoCardView.cs:129-210`) stacks **six text bands + a
five-row picker** into one body well using **fraction anchors**:
- name `0.87â€“0.97` (L176), what/Favors `0.79â€“0.85` (L182), state `0.72â€“0.78` (L187), synergy `0.65â€“0.71` (L193),
  ask `0.59â€“0.64` (L198), ResourcePicker container `0.05â€“0.57` (L206-207).
- `RebuildChips()` (`:248-297`) splits the picker container into **`1/n` equal slices** (n=5 â†’ each â‰ˆ10% of the
  ~half-body band) at `rowH = 1f/n` (L263-266), and builds each chip via `ElarionUiKit.Button`.

Two failures compound:
1. **Picker rows are below the touch floor and overflow.** Five chips in the `0.05â€“0.57` band give each row only
   ~10% of body height; the kit button renders at its **min touch height**, so buttons overflow their `1/n` slots,
   stack over each other, and push UP into the info text above. That's why only the last chip clears.
2. **Fraction text bands under-height the TMP line box** â€” the same vertical-culling lesson CLI just applied in
   WO-832 Â§4 / WO-841 ("fraction bands scaled with the card/pane and under-heighted the font's line box"). The info
   lines and the top chips collide.

Net: total content (6 text lines + 5 touch-sized rows, some with an affinity note under them) **exceeds the body
well**, and fraction bands don't reserve real per-row height, so it overlaps instead of fitting.

## 3. The fix â€” reuse the WO-841 / RumorBoard fixed-pixel-band pattern
Do NOT keep tuning fractions â€” apply the SAME lesson already in the tree (`WORK_ORDER_841` / the RumorBoard
fixed-pixel bands, and `BuildingUpgradePanelLayoutTests`):
- **Fixed ref-pixel bands, top-down:** each info line = ONE `ElarionUiKit.FontFloor` line box; each resource row =
  **`MinTouchPx`** for the button (+ one line box for the affinity note when present). Stack from the top of the body.
- **If the total exceeds the body well, put the resource picker in a scroll well** (the RumorBoard/`EchoRosterView`
  scroll pattern) so all five chips are reachable at full touch size â€” OR raise the modal's top / lower the info
  block so 5 touch rows + the info lines fit above the shared Close band. Picker bottom stays clear of Close
  (the WO-555 clearance the header comment cites).
- **Tighten the info block (recommended, reduces the stack):** it currently spends 4 lines that partly repeat
  ("â€¦Favors: Wood" + "Gathering Wood â€¦ +55%"). Consider merging Favors into the state line and keeping synergy as
  its own status line â€” fewer fixed bands = more room for touch-sized chips. Keep colorblind law (icon+text, never hue).
- Keep every chip â‰¥ `MinTouchPx` tall; the selected chip keeps its Gold face + "(now)" text cue (already correct).

## 4. Files to edit
- `Assets/_Modules/Village/Harvest/EchoCardView.cs` â€” `Build()` bands + `RebuildChips()` row sizing â†’ fixed pixel
  line boxes / touch floor (+ scroll well if needed). View-layout only.
- (`EchoCardVM.cs` â€” only if the info-line consolidation moves a string; no economy logic.)
- Add/extend an EditMode layout invariant test (mirror `BuildingUpgradePanelLayoutTests`) asserting the info bands and
  the 5 chip rows are disjoint and each chip â‰¥ touch floor.

## 5. Acceptance criteria (headless UI-capture, editor CLOSED)
- [ ] `RunCaptureHeadless` of the Echo card: NO overlap â€” every info line readable AND all five resource chips
      (Wood/Iron/Food/Gold/Crystals) fully visible, each â‰¥ touch floor, each tappable, above the Close band.
- [ ] The selected resource still shows the Gold face + "(now)"; tapping a chip reassigns (VM `AssignResource` path
      unchanged); synergy/affinity text unchanged (WO-830 behavior preserved).
- [ ] Layout invariant test green; `CompileGate` green.

## 6. Separate (already flagged â€” route to WO-834 Â§3)
The dev **"What looks wrong? (Enter = save Â· Esc = save blank)"** capture field bleeding over the ECHO header is the
`BreakCaptureHarness` note box that isn't fully dev-gated (WO-834 Â§3) â€” it renders over this screen too. Fix belongs
there, not here.

## 7. Do NOT
- Do NOT change the resource-picker behavior, the affinity/synergy math, or the VM economy logic (WO-830 is correct).
- Do NOT go back to scaling fraction bands â€” use fixed pixel line boxes / touch floor (the proven WO-841 pattern).
- Do NOT hand-edit scenes; single View-layout change (+ its test).

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `EchoCardView.cs:2-3,22-60` — fixed-band layout. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
