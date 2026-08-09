# WORK ORDER 792 — Enemy attacks deal ZERO damage to the hero

**Status:** DONE (reconciled 2026-08-09 from the tree - commit `f4f31180` landed the WO-790/791/792 wave, this WO being the enemy-to-hero damage probe. NOT felt-verified; no `.RESULT.md`)

**Status:** SHIPPED 2026-07-30 (f4f31180 — enemy-to-hero damage probe).
**Lane:** Lane 2 (Combat/AI)
**Type:** EXISTING (combat is built; damage-to-hero is landing at 0)
**Minted:** 2026-07-30 (owner felt-report)
**Author:** UI/RCA seat (candidate-level RCA — needs a headless proving read per §12). CLI implements + gates. PO felt-verifies + closes.

---

## Symptom (owner)

"**enemy attacks do zero damage to hero**" — in the outpost/garrison fight the hero's health does not
drop when enemies attack.

## What is known (read-only)

- The hero's own melee routes correctly through `IDamageable.TakeDamage` — that path is fine
  (`Assets/_Modules/Village/Enemies/PlayerAttackController.cs` is the HERO's attack controller,
  despite the name; `:562` `damageable.TakeDamage(...)`).
- The enemy→hero damage path is separate: an enemy melee lands on `HeroHealth` (implements
  `IDamageableStructure`/`IDamageable`). `HeroHealth` is referenced across the combat code
  (parry/deflect seam in `PlayerAttackController.cs:298,338`), so the receiving component exists.

## Root candidates (NOT yet proven — CLI must instrument first, §12/HARD GATE)

1. **Enemies never actually strike** because they're frozen off the NavMesh (WO-791) — so
   `HeroHealth.TakeDamage` is never called. If true, this is a DUPLICATE of WO-791, not a separate
   damage bug. **Check first.**
2. **Enemy attack damage is 0** for the garrison roster (a stat/scaling path that yields 0 for these
   outpost enemy ids, or a level/tier multiplier that zeroes out).
3. **HeroHealth ignores the hit** in this scene (wrong faction/layer, invulnerability flag left on
   after a scene transition from the dungeon/arena, or a guarded early-return that swallows the hit).
4. **The enemy hit routes to the wrong target** (a decoy/structure `IDamageable` instead of
   `HeroHealth`) so the hero never receives it.

## Proving step the CLI MUST run before editing (§12)

Run a headless outpost/garrison encounter (with WO-791's off-mesh issue worked around so enemies can
reach the hero) and instrument the enemy-attack → hero path:
- Does `HeroHealth.TakeDamage` get **called at all**? (FlowTrace at the enemy melee apply + at
  `HeroHealth.TakeDamage` entry.)
- If called, **what damage value** arrives (0 vs >0) and is there an early-return (invuln/faction/
  layer) that swallows it?

That single trace splits it into: (a) never-called → WO-791 (frozen enemies) or wrong-target;
(b) called-with-0 → damage/scaling; (c) called-then-swallowed → HeroHealth guard/invuln. Fix THAT.

## Candidate fix locations (to confirm after the trace)
- Enemy melee damage application (EnemyBrain/Enemy attack → `HeroHealth`) and the garrison enemy stat/
  scaling that sets attack damage.
- `HeroHealth.TakeDamage` guards (faction/layer/invulnerability), especially any invuln flag set on a
  scene transition (dungeon → arena → outpost) that isn't cleared.

## Acceptance
- [ ] Headless trace quoted showing enemy attacks now call `HeroHealth.TakeDamage` with a **non-zero**
      value and the hero's HP drops.
- [ ] The RCA in the `.RESULT.md` names which of the 4 candidates was the real root (proven by the trace).
- [ ] Brace/NUL gate green on any `.cs` edited; `COMPILE_GATE_OK`.
- [ ] Handed to owner for felt-pass; **PO closes**.

## What NOT to touch
- Do not "fix" by inflating enemy damage blindly — instrument first; a 0 could be never-called
  (WO-791) rather than a damage value.
- Hero→enemy damage works — leave `PlayerAttackController`'s outgoing path alone.

*Notion row pending.*
