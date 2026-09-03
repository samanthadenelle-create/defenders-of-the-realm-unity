# WORK ORDER 1296 — Modal and World Feedback Ownership

**Status:** FIXED — implemented by a8811ec73 `fix(feedback): modal sequencing, silent intact-tap, burn seated from renderer bounds` — covers all three reported items (DailyChest OfferWhenUiClear, silent intact tap, StructureBurn seated from renderer bounds). Awaiting the owner's felt-verification (PO closes, CLAUDE.md §13). *(Board status audit 2026-09-02; body unchanged.)* *(Prior line:)* **Status:** IN PROGRESS — 2026-09-01

## Player reports

- The daily chest is opened while the founding Echo card is active; one panel replaces the other and Close appears to dismiss both.
- Tapping an undamaged structure creates a clipped yellow `That structure is undamaged` toast over the HUD.
- Building fire VFX is anchored to the gameplay root rather than the rendered structure and can appear beside the mesh.

## Root causes

- `DailyChestController` uses a fixed delay and calls `PanelManager.NotifyOpened`; the manager correctly swaps the existing modal, so timing—not z-order—is the defect.
- `WallRepairController` publishes intact taps through the shared repair-feedback toast although no player decision or error occurred.
- `StructureBurn` authors its VFX anchor at local `(0, yOffset, 0)`, ignoring child-model offsets and renderer bounds.

## Patch

- Queue the daily chest until the modal arbiter has remained clear, then claim the offer slot only after a successful open.
- Make intact repair taps and late intact confirmations silent no-ops, retaining diagnostics.
- Seat fire VFX from non-particle renderer bounds and refresh the seat before loop/impact playback.

## Acceptance

- Echo teaching closes normally; the daily chest opens afterward as a distinct interaction.
- An intact structure produces no toast or yellow overlay.
- Fire anchor X/Z follows the visible structure and its Y lies within/above the visible body rather than the gameplay pivot.

