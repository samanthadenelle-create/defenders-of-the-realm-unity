# WO-961 — The founding Echo guide gets a BODY, and it is the Ice Wolf

**Status:** READY TO IMPLEMENT
**Date:** 2026-08-10 · **Priority:** HIGH (the FTUE's second beat tells the player to follow something that does not exist)
**Block:** main line (CLI) · **Lane:** Tutorial / Pets / art-rig
**Owner ruling 2026-08-10:** *"we should have Ice wolf"* + *"under pets"* +
`D:\eoa\Assets\Resources\Pets\ice-wolf.fbx`

## §1 What is actually wrong (captured, not inferred)

The tutorial's beat 2 objective reads **"Follow {guide} to the gate"**. From the owner's `Player.log`:

```
[Flow:Tutorial] step 'founding_greet' grant.starterPet - visible echo MODEL birth SCRAPPED
                (echoes are portrait cards now); roster grant + StarterPetId still applied.
[Flow:Tutorial] step 'founding_greet' grant.starterPet APPLIED: 'aether-sprite'
                (acquiredNew=True, StarterPetId='pet-aether-sprite', roster=1 pet(s)).
```

**No body is spawned at all.** The grant writes a roster entry and an id; the world stays empty. The
copy says follow, and there is nothing to follow. (`world.guide` then falls through its resolution chain
to the steward stand-in or the Heart.)

## §2 The ruling this REVERSES — and the half of it that still bites

`TutorialFlow.cs:1307-1319` records an owner call of **2026-07-16**: the founding Echo must read as an
ethereal spirit, *"NOT the quadruped ice-wolf that T-posed."* The owner's 2026-08-10 ruling supersedes
the creative half. **The technical half is still true, and it is not specific to the wolf:**

| Fact | Verified at source |
|---|---|
| `ice-wolf.fbx` exists and is git-TRACKED | `Assets/Resources/Pets/ice-wolf.fbx` |
| The load path already works | `PetDeployer.cs:616` `Resources.Load<GameObject>("Pets/" + def.Species)`; `pets.json` has `pet-ice-wolf` / species `ice-wolf` |
| The wolf has **no avatar and no clips** | `ice-wolf.fbx.meta`: `animationType: 2` (Generic), `avatarSetup: 0`, `clipAnimations: []` |
| **So does the sprite** | `aether-sprite.fbx.meta`: `animationType: 2`, `avatarSetup: 0` |
| There is no controller and no clip anywhere for pets | zero `.controller`, zero `.anim` under `Assets/Resources/Pets`; `Pets/Pet` and `Pets/PetIdle` are both entries in `HudUiRegression.MissingResourceBaseline` |

> ⚠ **A comment lies here (CLAUDE.md mandatory-first-step).** `TutorialFlow.cs:1310-1312` claims
> aether-sprite is *"the only HUMANOID rig (AccuRig CC_Base_*)"*. Its meta says **Generic, no avatar**.
> The sprite only reads acceptable because `EchoSpiritPresentation` hovers and drifts it, which MASKS the
> missing idle. A quadruped has no such mask — which is exactly why the wolf read as broken in July and
> the sprite did not. Fix the comment in the same commit.

**Consequence: dropping the wolf in as-is ships a sliding bind-pose statue (QR-5.3).** The mesh is the
part we already have; the rig, the clips and the controller are the work.

## §3 Canon supports the ruling

The unlock card in the owner's own session reads `[Flow:Echo] unlock card: 'I accept your power'
id=echo-frosthowl`. **Frosthowl IS the ice wolf.** Today the soul granted (Frosthowl) and the body
configured (aether-sprite) are different animals; this ticket makes them the same one.

## §4 Scope

1. **Un-scrap the body birth for the GUIDE only.** Echoes stay portrait cards in the roster UI; the
   founding guide is the one Echo with a world body, because a beat literally instructs the player to
   follow it. Keep the fallback chain (steward → Heart) intact for when no body resolves — fail-visible.
2. **Species = `ice-wolf`.** Change `TutorialFlow.StarterPetSpecies` and rewrite its doc-comment to
   record the new ruling AND the retired one (never silently flip an owner call).
3. **Rig + animate the wolf:** an avatar (Generic-with-clips or Humanoid-if-the-skeleton-allows), one
   IDLE and one WALK clip, and a controller at `Resources/Pets/ice-wolf.controller` (or the shared
   `Resources/Pets/PetIdle.controller`, which `PetDeployer.WirePetAnimator` already probes and which is
   currently a known-missing path). **Verify, do not assume** — a flipped import flag is not an avatar
   (QR-5.3, `HumanoidRigFixup` verifies after it repairs).
4. **Presentation:** decide whether `EchoSpiritPresentation`'s hover/drift stays on a quadruped. Owner's
   call; the recommendation is a grounded walk with a faint frost aura, not a floating wolf.
5. **Retire the known-missing baseline entries** for `Pets/Pet` / `Pets/PetIdle` if the controller lands.

## §5 Acceptance criteria

1. A New Game FTUE spawns a visible ice-wolf body near the Heart on the ARRIVE beat, and the WALK beat's
   `world.guide` highlight lands on THAT body.
2. The wolf plays an idle at rest and a walk while leading — **no bind pose, no sliding** (a captured
   `[Flow:Pets]` line naming the resolved controller is the proof, not a screenshot alone).
3. `StarterPetId` and the roster still record the same Echo the unlock card names.
4. A regression pinning: the species constant, that the controller resolves, and that the guide anchor
   resolves to a real body rather than the Heart fallback in a hub scene.
5. Screenshot/device capture before it reaches the owner (headless markers cannot see a T-pose).

## §6 What NOT to touch

The Echo roster/portrait-card model, `PetSelect` (bypassed under `ff.bypasspetselect`), the WO-962 anchor
latch, and the other two pet species — this ticket does not re-open which pets exist, only which one the
founding guide wears.
