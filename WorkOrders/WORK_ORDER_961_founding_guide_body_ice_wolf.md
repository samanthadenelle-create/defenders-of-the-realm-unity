# WO-961 — The founding Echo guide gets a BODY, and it is the Ice Wolf

**Status:** FIXED 2026-08-29 - owner device-tested on Seeker APK 2026.08.29.346931 and confirmed the wolf color is fixed. Spawn-separation remains a distinct verification item if overlap recurs.

*(Board note 2026-08-24: bucket corrected DONE/IMPLEMENTED → **FIXED**. Nothing about the work changed — §13 reserves DONE/closing for the PO, and this line's own text says the owner's felt-verify is still owed, so the row belongs in the felt-test queue, not the closed pile.)*
**Date:** 2026-08-10 · **Priority:** HIGH (the FTUE's second beat tells the player to follow something that does not exist)
**Block:** main line (CLI) · **Lane:** Tutorial / Pets / art-rig
**Owner ruling 2026-08-10:** *"we should have Ice wolf"* + *"under pets"* +
`D:\eoa\Assets\Resources\Pets\ice-wolf.fbx`

## §0 ⚠ THERE ARE THREE RULINGS IN THIS CHAIN, NOT TWO — and two of yours contradict each other

Surfaced 2026-08-10 while reading the scrapped birth site (`TutorialFlow.cs:1386-1393`). The species
pick is the SMALLER of the two reversals this WO carries:

| Date | Ruling | Where it lives |
|---|---|---|
| **2026-07-16** | the founding Echo must read as an ethereal spirit, **NOT the quadruped ice-wolf that T-posed** | `TutorialFlow.cs:1307-1319` |
| **2026-07-17** | *"Echoes are portrait-card spirits, NOT 3D models — scrap giving them a model."* The visible birth (`PetDeployer.SummonAt` + the `EchoSpiritPresentation` floating-spirit layer) was **retired entirely** | `TutorialFlow.cs:1386-1393` |
| **2026-08-10** | *"we should have Ice wolf"*, *"owners decision to switch"*, reaffirmed | this WO |

**The 07-17 ruling and the WO-1012 tutorial contradict each other, and that contradiction IS the
defect.** WO-1012's beat 2 (authored 2026-08-10) says **"Follow {guide} to the gate"** — an instruction
that requires a body — while 07-17 had removed every Echo body from the world. So the guide falls down
its resolution chain to the **Sylas steward NPC**, which is exactly what the owner flagged in F8 seq
2304 with the single word **"npc"**.

### The scope call (recommended, owner may override in one word)

**NARROW.** Restore a world body for the **tutorial guide only**, because a beat instructs the player to
follow it. **Echoes in the roster stay portrait cards** — the 07-17 intent (no menagerie of 3D pets)
is preserved everywhere else. The alternative, broad reading — every Echo gets a body again — is a much
larger change to the Echo pillar and is NOT what the F8 asked for.

⚠ Do **not** re-enable `EchoSpiritPresentation`'s floating-spirit layer on the wolf: it existed to MASK
the aether-sprite's missing idle, and a hovering wolf is wrong. The wolf's own clips replace it.

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
| The wolf DOES carry a full humanoid skeleton | the FBX contains the complete **AccuRig `CC_Base_` biped**: Hip/Pelvis/Waist/Spine01-02, Clavicle, Upperarm, Forearm, Hand + Index/Mid/Pinky/Thumb 1-3, Thigh/Calf/Foot/ToeBase + 5 toes, NeckTwist, Head, JawRoot, Tongue01-03, Teeth, Eyes, Breast — plus a ~95-entry facial expression set (`ice-wolf.json`, `"Generation": "AccuRig"`) |
| **So does the sprite** | `aether-sprite.fbx` carries **141** `CC_Base_*` bones — same family |
| But BOTH are IMPORTED AS GENERIC | `ice-wolf.fbx.meta` / `aether-sprite.fbx.meta`: `animationType: 2` (Generic), `avatarSetup: 0`, `skeleton: []`, `clipAnimations: []` — so Unity builds **no avatar** and nothing can retarget |
| There is no controller and no clip anywhere for pets | zero `.controller`, zero `.anim` under `Assets/Resources/Pets`; `Pets/Pet` and `Pets/PetIdle` are both entries in `HudUiRegression.MissingResourceBaseline` |

> ### ⚠ CORRECTION 2026-08-10 (this WO said the opposite for a few minutes — owner caught it)
> An earlier revision of this ticket called `TutorialFlow.cs:1310-1312`'s *"the only HUMANOID rig
> (AccuRig CC_Base_*)"* **false at source**. That was wrong, and it was wrong in the way §12 warns about:
> it read the **import setting** and called it the **skeleton**. The comment is RIGHT that the rig is an
> AccuRig CC_Base humanoid; its only error is the word **"only"** — the wolf has the identical skeleton.
> What is true is that both are imported **Generic**, so the humanoid rig is present and ignored. **That
> is the whole T-pose story: the rig was always there, Unity was told not to build an avatar from it.**
> Correct the "only" in the code comment; do not delete the humanoid claim.

> ### ⚠ THE REAL RISK, and it is not the import flag
> The object in `ice-wolf.json` is named **`"fox"`**, the mesh is **`Coyote_Mesh`**, and the textures are
> `Coyote_Mesh_Bake_Pbr_Diffuse/Normal.png`. **`ice-wolf.fbx` is a fox/coyote body auto-rigged onto a
> HUMAN biped skeleton** — fore-legs mapped as arms (with fingers and thumbs), hind-legs as legs.
> Flipping the import to Humanoid WILL build an avatar and WILL retarget the project's existing clips —
> onto a quadruped built like a person. That, not the missing controller, is the likely reason it read
> broken in July. A rig setting does not fix a skeleton/mesh mismatch.

**Consequence: dropping the wolf in as-is ships a sliding bind-pose statue (QR-5.3); flipping it to
Humanoid may instead ship an upright fox.** The mesh is the part we already have; the question is which
of the two paths in §4.0 the body actually needs.

## §3 Canon supports the ruling

The unlock card in the owner's own session reads `[Flow:Echo] unlock card: 'I accept your power'
id=echo-frosthowl`. **Frosthowl IS the ice wolf.** Today the soul granted (Frosthowl) and the body
configured (aether-sprite) are different animals; this ticket makes them the same one.

## §3.5 THE BODY IS SOURCED — provenance, recorded here permanently

**2026-08-10, owner-supplied:** `simple_wolf.unitypackage` (3.4 MB, 15 assets), imported to
`Assets/Animals/Low Poly Animals/`. Ships a **real quadruped rig** with five baked clips
(`wolf_rig|default`, `idle2`, `running`, `sniffing`, `fallen`), a matching `simple_wolf.controller`,
`Simple Wolf.mat` and three textures (color / normal / ao). This SETTLES §4.0: **Path B without the
cost** — a genuine animal rig, already animated, so neither the biped retarget nor a commission is
needed. The fox mesh is not used.

- **Source:** https://www.cgtrader.com/items/1947050/download-page (CGTrader), owner-downloaded.
- **Licence — OWNER RULING 2026-08-10, reaffirmed: "it is free no license." CLEARED TO SHIP.**
  The question was raised once (the package carries no LICENSE/README file, and the CGTrader page is
  behind a login so it could not be read from the CLI) and the owner ruled. **It is decided — do not
  re-open it at the next audit, and do not re-raise it in a future session.** Recorded here precisely so
  the next seat inherits the ruling instead of re-litigating it from the missing file.
- Provenance kept above for the same reason the WO-760 dragon replacement is documented: if the asset is
  ever swapped, the next seat knows exactly what came from where.

## §4.0 SETTLED BY §3.5 — kept as the reasoning, in case the wolf is ever replaced

Owner asked (2026-08-10): *"if easier i can get an animal rig but simple is better right?"* Simple is
better — but simple means the path that ENDS UP RIGHT, not the fewest steps. There are two, and one
screenshot tells us which:

- **Path A — free (5 minutes, zero new assets).** Flip `ice-wolf.fbx` to `animationType: 3` (Humanoid),
  let Unity build the avatar off the CC_Base skeleton that is already there, retarget ONE existing
  humanoid idle. If a fox on biped animation reads acceptably at pet scale, we are done.
- **Path B — an animal rig.** A real quadruped skeleton + an idle and a walk. Costs money/time, and is
  the only honest answer if Path A reads as a person-shaped fox.

**Do not buy anything until Path A has been captured.** The test is: flip the import, drop one idle,
run `UICaptureLaunch` / a device screencap, and LOOK. Headless markers cannot see a T-pose or an upright
fox (the 08-09 lesson: *"headless gates cannot see orientation"*), so this decision is made on pixels.
Record the screenshot in this WO either way — a proven "Path A is ugly" is what justifies the spend.

⚠ Whichever path wins, the **fox-vs-wolf** question is the owner's and is separate: the shipped body is
a fox/coyote named `ice-wolf`, representing the Echo **Frosthowl**. That may read perfectly well as a
small canine companion, or it may be the reason to commission a wolf.

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
