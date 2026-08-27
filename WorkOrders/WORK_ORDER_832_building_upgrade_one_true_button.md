# WORK ORDER 832 — Building Enhancement panel: ONE unambiguous Upgrade button

**Status:** CLOSED 2026-08-27 — owner felt-tested PASS on APK 2026.08.27.343739 (village review).
**Author:** UI/QA triage (read-only RCA, §13) — Claude UI
**Lane:** HUD/UI — single file (`BuildingUpgradePanelMvvm.cs`), View-only. No VM/logic/scene change.
**Origin:** owner felt-test 2026-08-02, "Lumber Mill Enhancements" screen — *"it's really impossible to know which
button starts the upgrade. Can we clean it up so only one button truly denotes it?"* Ties to the morning UI review
(`docs/qa/UI_REVIEW_2026-08-01.md`, building_upgrade panel).

---

## 1. RCA — three gold "Upgrade"-styled controls; two of them commit (sourced from live code)
File: `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs`. The bright gold fill
(`ElarionUi.GoldButton`) — the color the eye reads as "the action" — is used on THREE different controls:

1. **The "Upgrade" TAB** — `RestyleTabs()` (~line 522-532): the selected tab sets
   `_tabs[i].Fill.color = ElarionUi.GoldButton`, a full gold-filled rounded pill **identical in color and shape to
   the CTA**. But it only switches Upgrade/Skills — it is NOT an action. This is the primary confusion.
2. **The in-card "Upgrade" button** — `BuildTierCard()` (~line 736-740): the `available` tier renders
   `BuildGoldButton(card, "Upgrade", …)` whose onClick calls `_vm?.Select(id)` — a REAL upgrade commit, inside the
   tier card.
3. **The right-pane CTA "Upgrade"** — `BuildDetailCta()` (~line 886-888): `BuildGoldButton(host, "Upgrade", …)` →
   `_vm?.Select(selId)` — ALSO a real upgrade commit.

So the player sees the word "Upgrade" on a gold control in three places; two of them actually upgrade (#2 card, #3
CTA) and one is a tab (#1). Nothing signals which is *the* button. (Note: tapping a tier card already SELECTS it via
the whole-card button, `BuildTierCard` ~line 662-667 → `SelectTier(id)`, which repaints the right detail pane — so
the in-card Upgrade button #2 is redundant with "select the card, then press the CTA".)

## 2. The rule to establish
**Exactly ONE bright-gold filled button on the panel = the single commit CTA (the right-pane Upgrade).** Every other
gold-looking control is demoted to what it actually is:
- **Tabs are navigation, not actions** → a tab indicator, never the CTA's fill.
- **Tier cards are selectable tiles** → selection is shown by border/fill highlight (already present), with NO inner
  gold commit button.
- Reserve `ElarionUi.GoldButton` (solid bright fill) EXCLUSIVELY for `BuildDetailCta`'s live Upgrade / Raise
  Village Tier CTA.

## 3. Changes (View-only — do NOT touch `BuildingUpgradeVM` or the `_vm.Select` commit path)

### 3a. Demote the Upgrade/Skills tabs so they don't mimic the CTA (`RestyleTabs`, ~line 522-532)
Selected tab must read as a TAB, not a gold action button. Recommended (implementer's discretion on exact values):
- Selected tab: **dark fill + a gold underline bar** (a thin `ElarionUi.Gilt` rule along the tab's bottom edge) +
  gold **text**. Unselected: dark fill + dim text (as today).
- Do NOT fill the selected tab with `ElarionUi.GoldButton`. The underline + gold text is the "selected" signal.
- (Alternative if an underline is fiddly in this layout: a muted/outline gold — clearly less saturated and
  distinctly *flatter* than the CTA — but the underline is preferred; a second gold-filled pill is what we're
  removing.)

### 3b. Remove the in-card commit button; the card only SELECTS (`BuildTierCard`, ~line 736-740, `available` branch)
- Delete the `available` branch's `BuildGoldButton(card, "Upgrade", …)`.
- The whole card is already a select button (~line 662-667). For the `available` tier, replace the gold button with
  a NON-CTA affordance so the card still reads as "ready + tappable" without competing with the CTA — e.g. a small
  gold-**text** tag "Ready ▸" or "Tap to view" (text only, NO gold fill), or simply rely on the existing
  gold-rim/CardFillLit highlight the available card already gets (`BuildTierCard` ~line 653-655). Owner-preferred
  default: a quiet gold-text "Ready ▸" tag, no button chrome.
- Keep the `owned` "Unlocked" tag (~line 731) and the `locked` `BuildLockButton` (~line 745) as-is — those are dark,
  not gold-filled, so they don't read as the primary CTA. (Confirm the lock button never uses `GoldButton`.)

### 3c. Confirm the result
After 3a+3b the ONLY bright-gold filled button anywhere on the panel is the right-pane CTA from `BuildDetailCta`
(Upgrade / Raise Village Tier). Selecting a tier card repaints the right pane; the CTA commits. One true button.

## 4. Secondary (verify, fix only if confirmed real — not the core ask)

> **FIXED (2026-08-02, edit-only agent, alongside WO-841):** the UI seat CONFIRMED the clipping on
> fresh pixels (NOT a stale build), so the §4 tail was implemented in `BuildingUpgradePanelMvvm.cs`
> using the RumorBoard fixed-pixel-band lesson (TMP vertical culling: fraction bands scaled with the
> card/pane and under-heighted the font's line box). Tier-card head/name/effect/footer and the detail
> benefit rows + CTA are now FIXED ref-pixel bands sized in whole `ElarionUiKit.FontFloor` line boxes
> (card effect = 3 lines, footer/lock = 2 lines, benefit rows 1 or 2 lines by length, CTA band =
> `MinTouchPx`); the illustration flexes in the remainder. The detail list also dropped its first row
> (verbatim duplicate of the pane title) to make room. Invariants pinned in
> `Assets/Tests/EditMode/BuildingUpgradePanelLayoutTests.cs`. Pending gates: CompileGate + EditMode +
> `RunCaptureHeadless building_upgrade` re-capture.
The owner's screenshot still shows effect/preview text clipping mid-word — card "Wood production +12%. **Structu**",
detail "Wood production +12%. **Structur**", "Opens Reinforced Blades (**Wood**" and truncated tier buttons
"Unlock '**Re**". The code added `FitBlock` wrapping (~line 708-726, 802-812), so this may be a STALE build (the same
stale-build tell as the Echo panel). CLI: confirm against a FRESH build + `RunCaptureHeadless building_upgrade`
(editor CLOSED). If it still clips at the shipped band heights, widen the card effect band / detail row height so it
wraps instead of ellipsizing. Do this ONLY if the fresh capture reproduces it.

## 5. Files to edit
- `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs` — `RestyleTabs`, `BuildTierCard`
  (and, only if §4 reproduces, the effect/detail band anchors). View-only.

## 6. Acceptance criteria (headless UI-capture, editor CLOSED)
- [ ] `RunCaptureHeadless` → `building_upgrade` shows exactly ONE bright-gold filled button (the right-pane CTA).
- [ ] The selected Upgrade tab is visually distinct from the CTA (underline/text, not a gold-filled pill).
- [ ] Tier cards have no inner gold "Upgrade" button; tapping a card selects it (right pane repaints) and does NOT
      commit an upgrade; only the CTA calls `_vm.Select`.
- [ ] Owned tier still reads "Unlocked"; locked tier still shows its dim lock/reason; CTA still commits + still shows
      the busy/afford/gate reason lines (`BuildDetailCta` behavior preserved).
- [ ] `CompileGate` green; no change to `BuildingUpgradeVM` or the commit path.

## 7. Do NOT
- Do NOT alter `BuildingUpgradeVM` or `_vm.Select` (the commit is correct; this is purely which control shows it).
- Do NOT change the Skills tab row grammar, the currency pills, or the Close button.
- Do NOT introduce a second gold-filled action button anywhere on the panel (that is the whole bug).
- Do NOT hand-edit scenes; single-file View change only.
