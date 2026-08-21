<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-24
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-24) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK_ORDER_508 — normalize locomotion (slide-proof animation feed)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Origin:** owner caught the design smell ("is that logic flawed?") during the orc-slide fix.

## The flaw
The humanoid locomotion blend trees use ABSOLUTE speed thresholds (idle 0 / walk 6 / run 9 m/s), tuned to the
HERO's move speed (6). Any character with a DIFFERENT move speed must have its thresholds hand-matched or it
SLIDES (plays idle while moving). The orcs (move 2.2-3.2) inherited the hero's 6/9 thresholds -> never crossed
"walk" -> slid. The scoped fix this session lowered the ORC thresholds to 1.5/3.5, but the fragility remains:
every new character/speed is a future slide waiting to happen.

## The robust fix (this WO)
NORMALIZE the locomotion feed so thresholds are universal and speed-agnostic:
- Feed the blend parameter a 0..1 fraction = `worldSpeed / moveSpeed` (clamped 0..~1.2), instead of raw m/s.
- Set ALL humanoid locomotion blend trees (hero + orc + any future) to normalized thresholds: idle 0 /
  walk ~0.5 / run ~1.0.
- Touch points: `ActorAnimator.SetLocomotion(float)` (divide by the actor's move speed before SetFloat), the
  hero animator builder (`HeroAnimatorFactory`) + the orc builder (`BuildOrcHumanoidController`) blend
  thresholds, and anywhere `AnimParams.Speed` is set directly (e.g. Enemy.cs legacy SetFloat). The actor must
  know its move speed to normalize (pass it in / read it).
- After: ANY character at ANY move speed plays walk/run correctly. No per-character threshold tuning, no slide.

## Risk / why deferred
This changes how Speed is fed for the HERO too (which currently animates correctly). Do it deliberately, with
a felt-verify that the hero's walk/run is unregressed — NOT blind in the middle of other work. The scoped orc
fix (1.5/3.5) holds in the meantime.

## Acceptance
- Hero + orcs + (a test high/low-speed character) all play walk/run at their real speeds; zero slide.
- Normalized thresholds (0/0.5/1.0) in every humanoid locomotion blend tree; ActorAnimator divides by move speed.
- Hero locomotion felt-verified unregressed. Controllers rebuilt + gate-clean.

## Do NOT
Ship blind — the hero feed is felt-sensitive. Pair with an owner felt-verify of the hero's walk.

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `ActorAnimator.cs:113-118 raw m/s` — normalized speed feed unbuilt. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict. ⚠ NOTE FOR ANYONE REOPENING: the 2026-08-21 read-only audit had classified this one OPEN - STILL VALID, with the evidence cited above. The owner's review supersedes that call (owner statements are ground truth). The audit line is left in place deliberately, so if this work turns out to be needed, the evidence for it is still here rather than erased.
