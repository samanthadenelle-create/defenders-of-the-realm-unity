<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-23
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-23) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK_ORDER_493 — COMBAT FEEDBACK & GAME FEEL (hit juice + death cam)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Goal:** hits LAND — restore + extend the combat-feel layer so the real-time arena (and overworld)
fights feel impactful, not flat. Pairs with [[atb-flat-vs-overworld-animated-combat]] + WO-491 (animation).

## Items (owner 2026-06-23)
1. **Hit flash — "bring back the red":** a damage flash on the struck body (hero AND enemy) — red/white
   emissive or overlay pulse on hit. Owner says "bring back" → likely EXISTS in old code; find + revive
   (grep DamageFlash / HitFlash / flash / OnDamaged emissive) before writing new.
2. **Controller impact / rumble:** gamepad rumble on hit (light on taking a hit, heavier on a big hit /
   landing a big blow). Via the input system's haptics; gate for no-controller (keyboard) gracefully.
3. **Camera shake on big hit:** a short screen-shake on heavy hits (big enemy blow, hero power attack /
   shield bash). Tune magnitude/duration so it punctuates without nausea; only on BIG hits, not every tick.
4. **Death camera hold (~10s) — both enemy AND hero:** on a death (enemy or hero), hold/linger the camera
   on the dying actor for ~10s so the full DEATH animation cycle plays out (don't cut away / restart
   instantly). Applies in the BattleArena (and overworld). For the hero death this is the defeat beat;
   for the enemy it's the kill payoff. Make sure the arena teardown / return waits for the death cam.
5. **Injured stance at low HP (Fallout-style) — hero AND enemy:** below an HP threshold (or after a big
   hit), the actor adopts a WOUNDED posture/locomotion — hunched, limping, slower — so health reads from
   the body, not just a bar. An animation STATE driven by HP (ties to WO-491's controller: add an
   `Injured` bool param + injured idle/walk clips, blended in under the threshold). Restore to normal if healed.

## Notes / where to look
- "Bring back" implies regression — the hit feedback (red flash etc.) was from the **WAVES system and is
  REALLY OLD** (owner 2026-06-23), so dig the wave-defense combat + deep git history (HitFeedback,
  DamageFlash, CameraShake, ScreenShake, the old WaveManager/Enemy hit reactions) and revive, not greenfield.
- Hook points: `HeroHealth`/`Enemy` `OnDamaged`/`Died` events (flash + rumble + shake); a camera
  controller for shake + the death-hold; `BattleArena` death/teardown flow for the 10s hold + return.
- ASCII logs, brace gate, §12 (instrument the death-cam timing). Build on a clean committed base.
- Related juice already present: damage numbers (`[Flow:Feedback] damage number spawned`), `Cast_*` VFX.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
