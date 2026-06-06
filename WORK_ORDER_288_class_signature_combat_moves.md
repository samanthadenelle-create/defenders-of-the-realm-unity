# WORK ORDER 288 — Class Signature Combat Moves (the fight is the hook)

**Status: IN PROGRESS — core shipped, class variants are SPEC.**
**Lane:** Combat/AI + VFX/Audio. **Why:** owner thesis (2026-06-06) — *"a satisfying fight
scene drives deeper engagement; a lot rides on the open-world fight."* Combat feel is the
single highest-leverage thing for the grant demo (reviewers judge the first fight).
**Asset:** the ActorCore/Magical-Moves kit (~47 anims: parry/deflect/dodge/barrage/etc.)
is the animation source for these moves.

The pattern is unified: **a timing window → a payoff (slo-mo + buff/negate + signature VFX).**
Each class gets one signature timing move on this same seam.

## ✅ SHIPPED
- **Impact audio** — weapon clash on melee connect / spell zap for casters (commit fcb… / 8fc…).
- **Slo-mo death blow** — `CombatFeedbackManager.RegisterKill` → deeper slow-time + camera kick,
  rate-capped so it stays special (commit 8fc557a). Frames each creature's death anim.
- **Knight perfect parry → riposte** (commit f4d39a2): block-raise opens a 0.25s parry window;
  an enemy hit inside it is NEGATED → `CombatFeedbackManager.Parry()` slow-time + clang + a 3×
  "RIPOSTE!" next swing. Big payoff vs a heavy tank. Public seam: `PlayerAttackController.OpenParryWindow(seconds)`.

## 🟡 NEXT — class variants (all reuse the seam above)

0. **Parry TELL (do this FIRST — it's what makes parry usable).** Like Zelda/soulslikes,
   show the player WHEN to parry. **Enemies already telegraph** — `Enemy.cs` has `_telegraphing`
   + a `WindUp` animator trigger + `TelegraphThenAttack(duration)` (DEF-48) with per-type
   `EnemyTypeVfxSet.TelegraphDuration`. Hook the tell onto that windup: a brief **flash/glint +
   cue sound** (near the enemy or a hero-side prompt) during the telegraph so the player knows a
   parryable strike is coming. Align `ParryWindow` to the telegraph→strike timing. Without this,
   the parry is frustrating; with it, it's the addictive "see it → nail it" loop.
   - **WindUp animation per creature:** the timing system is there, but the telegraph only
     READS if the creature's controller has a `WindUp` state (code guards with `_hasWindUpParam`
     — safe no-op without one). New creatures (wraith/ogre/ogre-mage/troll) just need a **wind-up
     anticipation pose** added to their controller (easy to author from the kit's poses; build it
     in via `EnemyAnimatorFactory`). A clear, readable wind-up pose IS the visual tell.

1. **Mage — magical parry / deflect (skill-gated so it's RARE, not spammy).** A deflect ability
   calls `OpenParryWindow()` → same negate + slo-mo + riposte payoff, but with a **magic
   shield/deflect VFX + cast anim**. Owner refinement: success is gated on the incoming
   projectile's **angle + range + velocity** (a "perfect deflect" cone/timing) — only a well-
   aimed deflect at the right moment works, so it stays a skill flex. On success: a 1-sec
   **deflect flash** + shimmer/ward sound + "DEFLECT!" label (optionally reflect the projectile
   back). Wire from `HeroAbilities` (a defensive slot) into the existing parry path.
2. **Ranger — barrage / perfect shot.** Either (a) a **multi-arrow barrage** ability, or (b) a
   **perfect-release** timing on the bow draw (release in a window → a power shot: bonus damage +
   pierce + a brief slo-mo). Reuses the perfect-hit-window pattern already in PlayerAttackController.
3. **Per-creature death VFX** so the slo-mo death blow lands per type:
   - **Wraith (floats)** → dissolve/mist (no ragdoll); float locomotion (hover, no foot-plant).
   - **Ogre / troll** → heavy topple + dust.
   - Wire via the shared enemy controllers (`EnemyAnimatorFactory`) + per-archetype `EnemyTypeVfxSet`.

## Tuning (eyes-on, in-editor on the runtime `[CombatFeedbackManager]`)
`killSloMoTimescale` (0.3) / `killSloMoDuration` (0.45) / `killSloMoMinInterval` (6) / shake.
Parry: `ParryWindow` (0.25s), `RiposteWindow` (2s), `RiposteMultiplier` (3×) — consts in
PlayerAttackController; promote to SerializeField if live-tuning is wanted.

## Notes
- Keep slo-mo SPECIAL (rate-capped / skill-gated) so it doesn't become constant stutter.
- All `Time.timeScale` dips restore in a `finally` (safe); `CombatFeedbackManager.OnDestroy` resets too.
- Local WO (Linear maxed; tracked in-repo). Files now ≤288.
