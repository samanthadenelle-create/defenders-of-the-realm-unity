<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-13
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-13) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 696 — Damaged structures: context button = REPAIR, upgrade gated until repaired *(renumbered from WO-684, 2026-07-13 — 684 is the outstanding-items board)*

**Status: READY TO IMPLEMENT** (owner ruling 2026-07-12, verbatim intent: "outside of battle,
instead of Upgrade, a button that says Repair — cannot upgrade till repaired").
**Lane:** HUD/Progression. **Type:** NEW (owner-classified 2026-07-12: "technically new
functionality but should be simple and reuse almost everything") — correctly routed as a spec/WO
per pipeline §13, NOT a bug-fix. Reuse inventory: WO-672 repair costing + spend path, the
existing Village→HUD proximity push bridges, BuildingUpgradeVM status plumbing, HUD context
slot rendering. The only genuinely new code = the service-level "damaged blocks upgrade" gate +
the button-label swap read.

## The ruling (canon)

1. **A damaged structure's context action is REPAIR, not Upgrade.** When the player is at a
   structure whose `HpFraction < 1` (or Broken), the HUD context slot that shows "Upgrade"
   shows **"Repair"** (+ its in-kind cost, e.g. "Repair · 36 wood 36 iron") instead. Tap =
   the WO-672 repair spend (the SAME one costing path as the wave-report Repair All — one
   owner, never a second price).
2. **Cannot upgrade until repaired — everywhere, one rule.** The gate lives in the SERVICE
   layer (BuildingUpgradeService / the tier verb), not in any one button: the HUD context
   button, the enhancement panel tiles, and the build-mode Upgrade verb all READ the same
   "damaged → upgrade blocked" state (One Model: readers of one capability, no per-surface
   logic). Blocked upgrade surfaces show the reason: "Repair first" (+ cost) — text, never
   color-only.
3. **Scope: outside battle/DEFEND** (matches the owner's framing and the existing wave-report
   flow during/after waves). During an active wave the context bar behavior is unchanged.
4. **EXTENSION (owner, same session): this applies to ANY damaged structure — and towers get
   proximity management.** The context behavior is a CAPABILITY READ, not a building feature
   (One Model §2b): anything implementing `IDamageableStructure` (towers, gates, wall segments,
   collectors, buildings) offers **Repair** on approach while damaged; anything Upgradable
   (towers have the tier ladder + `ReskinForLevel`) offers **Upgrade** on approach when healthy
   — so a tower can be managed by walking up to it, not only through build-mode selection.
   - Tower Upgrade routes to the EXISTING upgrade verb/panel (the BuildSelectionUI upgrade
     path / tier costing — reuse; note the orphaned `TowerUpgradeButton.cs` — reconcile or
     retire it rather than shipping a third path).
   - **Walls granularity (RULED 2026-07-13, owner delegated the pick to CLI):** context Repair
     at a wall repairs the NEAREST damaged segment (default). The "Repair wall · <sum>"
     whole-connected-run option is DEFERRED to felt-testing — build the nearest-segment path
     only; leave the run-sum as a noted extension point, not dead code.
   - One interaction seam: this rides the same proximity/interaction service the buildings
     use (WO-391 consolidation direction) — capability flags decide the verb; NO per-type
     context code.

## Implementation notes (verified seams)

- HUD context bar = `VillageHudController` slots fed by the Village→HUD push bridges
  (presentation observes; the structure exposes damaged-state + repair cost, the HUD renders).
  The proximity structure's state already flows for the Talk/Upgrade routing — extend the same
  push with `hpFraction`/`repairCost`, don't add a new bridge.
- Repair spend + visual refresh = the WO-672 path. **Depends on ticket REP-1** (repair leaves
  the damaged state showing) — land REP-1's fix FIRST or in the same wave, else this button
  inherits the same "repaired but still looks broken" bug.
- Gate check in `BuildingUpgradeVM`: damaged building → tier/perk purchase blocked with
  Status = "Repair the <name> first — Repair · <cost>." (and ideally the tier tile's bottom
  line says it too).
- `[Flow:Upgrade]` / `[Flow:Repair]` step-in/out on the gate + the swap decision.

## Acceptance
- [ ] At a damaged Forge (out of combat): context shows "Repair · <cost>"; tap charges once,
      structure visuals + production recover, button reverts to "Upgrade".
- [ ] Upgrade attempts on a damaged structure (context button, enhancement panel, build-mode
      verb) are all blocked with the "Repair first" reason — one service rule, three readers.
- [ ] Undamaged structures: exactly today's behavior.
- [ ] Approach a damaged TOWER → "Repair · <cost>"; healthy tower → "Upgrade" opens its
      existing upgrade flow; same for gates/collectors; wall segment repair per the owner's
      granularity pick. Verb chosen by capability flags — grep proves no per-type branches.
- [ ] COMPILE_GATE_OK + fleet probe (damage a structure headless → assert upgrade verb
      rejects + repair verb restores + upgrade then passes) + owner felt-pass (PO closes).

## What NOT to touch
Repair pricing/costing math (WO-672) · wave-report Repair All flow · the context bar's other
verbs (Build/Talk/Bag).

*Cross-refs:* WO-672 (damage lifecycle + in-kind repair) · ticket REP-1 (fix first — same seam) ·
F8-42 (repair-costs directive this extends) · WO-680 (panel status grammar).
