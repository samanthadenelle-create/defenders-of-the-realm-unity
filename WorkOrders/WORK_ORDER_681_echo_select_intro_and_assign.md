**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-13
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-13) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 681 — Echo select: introduce what an Echo is + offer the gather assignment

**Status: READY TO IMPLEMENT** (owner directive 2026-07-12: "On selecting an Echo, there should
be something about what an echo is, what they are for, or asking what it can help gather for you").
**Lane:** Village/Harvest UX. **Type:** NEW (thin UX layer over the BUILT Echo workforce).

## Context (canon + code, verified)

Echoes are the autonomous harvesters released by the Tree of Life (COMBAT_PIVOT_NORTHSTAR:
"render the flavor, fake the sim"; workforce cap 5, drag-assign is the designed ONE interaction).
Built today: `EchoService` (accrual), `EchoWorkforceHud` (Collect All), WO-587 population growth
(save v28+). **WO-658 (Grok audit) already specs the assignment slots (drag/pick → resource
lane) — this WO is the SELECT/INTRO half and should land WITH or just before WO-658, sharing
its pick UI.** Today tapping an Echo does nothing — a wandering spirit with no explanation is
exactly the "working system that reads as missing" class.

## Spec

1. **Echo tap/click → a small Obsidian card** (master-frame modal, PanelManager-registered,
   one Close): the Echo's name/portrait socket + TWO lines:
   - WHAT: "An Echo — a spirit of the Tree. It gathers for Elarion while you fight, even while
     you're away." (final copy = owner pass; verbiage stays diegetic, ten-year-old-clear).
   - STATE: what it's gathering now + rate ("Gathering wood · +12/min") or "Idle — waiting for
     your word." (reads from EchoService — VM relays, View never touches the service).
2. **The ask verb:** one action row — "What should you gather?" → the WO-658 lane picker
   (wood/iron/food chips with the mirrored currency icons). Picking assigns via the WO-658
   seam. If WO-658's picker isn't landed yet, this card IS its natural host — implement the
   picker here per that spec (one surface, don't build two).
3. **First-meeting beat (once per save):** the FIRST Echo tap plays a one-line dialogue through
   the standard DialogueService path ("The Tree stirs — a spirit answers…") then opens the
   card. One-shot flag in GameState (additive). Keeps lore delivery on the existing rail, no
   new systems.
4. **Discoverability:** Echoes get the standard interact affordance on proximity/hover (the
   same Talk/interact tell NPCs use — reuse the interaction service seam, presentation observes).

## Gates / acceptance
- [ ] Tap an Echo → card explains what it is + shows live gather state; assign changes the
      lane and the card + HUD reflect it immediately; persists across reload.
- [ ] First-ever tap plays the intro line exactly once per save.
- [ ] MVVM split (VM owns EchoService reads; dumb-skin View via the master factory) +
      colorblind-safe states + one-action-one-button.
- [ ] `[Flow:Echo]` step-in/out on select/assign; COMPILE_GATE_OK + fleet panel probe + owner
      felt-pass (PO closes).

## What NOT to touch
EchoService accrual math · Collect All flow (WO-663 spine) · echo cap/growth (WO-587).

*Cross-refs:* WO-658 (assignment slots — shared UI) · `ECHO_WORKFORCE_SPEC.md` ·
COMBAT_PIVOT_NORTHSTAR (echo canon) · `docs/UI_BLINK_TEMPLATE_CANON.md`.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
