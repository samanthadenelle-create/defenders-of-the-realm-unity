# WO-599 — Combat locks modals (no shopping while being killed)

**Status:** READY TO IMPLEMENT
**Lane:** 4 (UI/HUD) + 2 (combat signal) — PanelRouter-level, one rule for every panel
**Origin:** owner F8 2026-07-02 flag_12: "While being attacked I can sit here and shop while I can see the enemy killing me. seems like a bug."

## The captured evidence
flag_12 screenshot: Arcane Tower upgrade panel open and fully interactive while the hero takes wave
damage behind it (fire VFX visible around the modal; HP draining). The player can browse/spend
through their own death.

## Rule (decide once, apply to every panel — no per-panel hacks)
When the hero **takes damage** (HeroHealth.TakeDamage while a modal is open):
1. Any open modal panel **closes immediately** (through the shared PanelRouter close path — the
   same one shared-Close uses), and
2. A brief HUD combat cue fires (existing hit feedback; no new chrome), so the player understands
   WHY their panel closed.
3. Re-opening panels stays possible between hits (we don't hard-lock the UI during a wave — being
   *hit* interrupts, being *in combat* doesn't forbid), matching action-RPG convention.
4. Exception list is DATA (a `combatSafe: true` flag per panel id): Settings + Bug Report stay
   openable always; Dev/Admin panels exempt.

## Implementation sketch
- One subscriber: PanelRouter (or a small CombatModalGuard beside it) listens to the existing
  hero-damage signal (HeroHealth event / FlowTrace-adjacent hook) and calls the shared CloseAll
  for non-`combatSafe` panels. VM/presentation seam respected — the guard is router-level, no
  panel edits.
- FlowTrace.Step("UI", "combat-closed <panel> (hero hit)") for capture.
- Bot oracle hook: the WO-597 popup-close oracle gains a case — open a panel, spawn/route damage,
  assert the panel closed (FlowTrace.Fail "PANEL_COMBAT_LOCK" if still open).

## Acceptance criteria
- [ ] Hero takes a hit with any shop/upgrade/inventory panel open → panel closes same frame + cue
- [ ] Settings/BugReport (combatSafe) unaffected
- [ ] Rule lives in ONE place (router-level); zero per-panel special cases
- [ ] Exceptions declared in data, not code branches
- [ ] Fleet asserts the behavior (PANEL_COMBAT_LOCK oracle case)
