# WO-675 RESULT — Building upgrade panel Obsidian redesign (DONE — CLI side; PO felt-pass = live preview)

**Committed:** `60d46c31` (2026-07-13 early, "DeNelle Tools hub + WO-675 upgrade panel Obsidian
redesign"), gated. Behind `ff.buildingupgradepanel`; LIVE on preview `9ncz1sks9` — the UI seat
confirmed seeing it in preview 07-13. RESULT written 2026-07-13 during the sync handoff.

- Panel rebuilt on the master-frame formula (FrameTalent chrome, band/chip layout, footer
  CurrencyChips, toast status) over `BuildingUpgradeVM` (MVVM held).
- **Follow-up already landed on top (07-13 wave, ungated tree): WO-680/UPG-1** — the owner's
  first live session on this panel found the Tier-2 legibility dead-end + "Unlock Maxed" dead
  CTA; fixed same day (IsMax → no CTA, named-action gate copy, `[Flow:Upgrade]` traces).
- Spec amendment A1–A4 (footer clipping / tile anatomy / sparse-grid plates) is PARKED — needs
  a factory-level (ElarionUiKit zone) pass; tracked in the UPG-1 ticket metadata.
