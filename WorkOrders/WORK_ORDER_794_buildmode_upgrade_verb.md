<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-30
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-30) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-794 — Build-mode UPGRADE verb: pick Upgrade, carousel minimizes, tap a building to upgrade

**Status:** SPEC STUB — needs full spec pass; NOTE a slice already landed out-of-band (cd661967 upgrade CTA explains itself pre-tap).
**Minted:** 2026-07-30 from an owner F8 (verbatim below), classified NEW-FEATURE per docs/TICKET_PIPELINE.md
**Owner F8 (22:48 UTC, Main_Castle_Overworld):** "Can we add an upgrade button, so after selecting the
button upgrade (if they can) will minimize the bulding selection to select what to upgrade?"

**Intent:** an Upgrade mode inside build mode - tap Upgrade, the palette carousel minimizes (same
minimize-on-select pattern the owner ruled for placement, memory build-hud-mobile-design), then tap a
placed building to open its upgrade. Buildings that CAN upgrade should read as selectable.

**Notes for the spec pass:** reuse BuildModeController's existing UpgradeSelected + PanelRouter
BuildingUpgrade path (do not greenfield); the "can upgrade" read comes from BuildingTierCatalog /
ResourceBuildingProgression via CatalogRegistry.ResolveUpgradeId (mind the collector-id landmine,
WO-783 SME notes L3); eligibility must read as symbol/label, never colour-only. Related: WO-696
(repair-before-upgrade context), WO-739 (generic upgrade panel).
