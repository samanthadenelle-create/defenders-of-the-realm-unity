# Weapon mesh archetypes — the analytical reference the orient helper classifies against

**Status:** REFERENCE DICTIONARY. Owner-specified 2026-08-19; composed here as measurable geometry.
**Companion to:** `docs/WEAPON_ARMOR_ORIENT_LOGIC.md` (the binding canon) and WO-1123 (`WeaponOrientHelper`).
**Purpose:** describe what each weapon archetype's mesh *is*, in terms a program can measure, so a seat is
DERIVED from geometry + name rather than hand-typed. Every rule below must be computable from vertices
and bounds alone.

---

## 0. THE SHARED FRAME (owner ruling, 2026-08-19)

Sort the mesh's local bounds extents and name them by RANK, never by the authored axis letter:

| rank | name here | owner's words |
|---|---|---|
| longest | **L** (maps to Y) | "the y as the longest of the mesh" |
| middle | **M** (maps to X) | "x as the middle of the mesh" |
| shortest | **T** (maps to Z) | "z would be the narrowest of the mesh" |

⚠ **L/M/T are ranks, not axis letters.** A model authored Z-up has its longest extent on Z; the rank
survives that, the letter does not. Every rule below is expressed in ranks.

**Bounds alone cannot orient anything.** A bounding box is symmetric — it tells you the shape of the
envelope, never which end is the tip or which face is the front. Every archetype below therefore names a
**disambiguator**: a measurement over the actual vertices that separates the two ends of an axis. That
disambiguator is the whole difference between a seat that works and a seat that is 180° out.

**The universal measurement primitive:** slice the mesh into N bins along an axis (N ≈ 16–32) and, per
bin, record the cross-sectional extent (max radius, or the M×T bounding area of the vertices in that
bin). The resulting **profile curve** is what distinguishes a tip from a hilt, a head from a foot, a
convex face from a concave one. Everything below reads that curve.

---

## 1. SWORD

### The mesh, described

A sword is a long, flat, tapering blade with a short, wide interruption near one end (the cross-guard),
a narrow grip behind it, and usually a small terminal mass (the pommel).

- **L (longest)** = tip → pommel. Dominates: a longsword runs L/M ≈ 8–15, a shortsword ≈ 4–8.
- **M (middle)** = blade width, and the cross-guard span. This is the axis the guard sticks out along.
- **T (shortest)** = blade thickness. A sword is a *flat* object: T is small and roughly constant.

Along L the profile is not symmetric, and that asymmetry is the whole signal:

```
   TIP                                                            POMMEL
    |                                                                |
    v                                                                v
   ....______________________________________....______....___
   :  \_______________ blade _______________/    :guard:  grip  :pommel:
   :   profile tapers smoothly toward the tip    : LOCAL:       : small :
   :   (cross-section shrinks to ~0)             : SPIKE: thin  : bump  :
   0.0 ------------------ along L ------------------------------> 1.0
```

### How to tell the tip from the hilt (the disambiguator)

The owner's rule — *"find the pointy edge that goes farthest away… you find the edge that is NOT sharp,
and you go up to the hilt"* — is a statement about the profile curve:

1. **The tip end** is where the cross-section **decreases monotonically to near zero**. A sharp edge is,
   measurably, a vanishing cross-section.
2. **The hilt end** is where the profile shows a **local MAXIMUM in M that is not the blade** — the
   cross-guard spikes wider than the blade, then collapses to the grip. That spike is the single most
   reliable landmark on a sword mesh, because it is the only place where width *increases* as you move
   away from the blade.
3. If no guard spike exists (some Blink/stylised blades), fall back to: the tip is the end with the
   smaller terminal cross-section. Log a Warn naming which rule fired — the fallback is weaker and we
   want to know when it is carrying a seat.

### The seat

- Blade points **+L, away from the hand**. Never blade-in-hand, never laid flat.
- **Grip point:** just *inboard* of the guard spike, on the pommel side — the owner's "go up to the hilt".
  Expressed as a fraction of L measured from the pommel end, it lands around 0.05–0.15 for most swords,
  but it should be **measured from the guard landmark, not typed as a fraction**.
- The flat of the blade (the T normal) should face a consistent direction relative to the palm, so the
  edge is not presented into the hand.

### Name clues (owner: *"the word short sword, long sword… will just give you clues"*)

The name never chooses the axis — **L is the longest extent regardless.** The name predicts the
**expected ratio**, and so is useful as a *sanity check*, not as an input:

| name contains | expected L/M | if measured ratio disagrees |
|---|---|---|
| `long`, `great`, `claymore`, `2h`, `two-hand` | high (≈8–15) | Warn: possible wrong mesh or wrong scale |
| `short`, `dagger`, `dirk`, `1h` | lower (≈4–8) | Warn |
| anything else | 4–15 | no signal |

A dagger and a longsword are the same archetype with different ratios; do not branch behaviour on the
name. Use it only to flag a mesh that does not look like what it is called.

---

## 2. SHIELD

### The mesh, described

A shield is a broad, thin, **curved plate**: convex on the side facing the world, concave on the side
facing the body, with a handle/strap boss on the concave side.

- **T (shortest)** = the **thickness**, and its axis is the **face normal**. Owner: *"whichever of the
  three is the shortest is the thickness of the shield."*
- **L and M** = the face itself (height and width). For a heater/kite, L > M; for a round shield, L ≈ M.

Viewed edge-on, along L:

```
        FRONT (world-facing)              BACK (body-facing)
              convex                          concave
                 )                              (
                 )   <-- rounded arc            (   <-- indented arc
                 )                              (  [ handle / boss ]
                 )                              (
        <-- T (thickness): the smallest extent of the three -->
```

### How to tell the front from the back (the disambiguator)

This is the one that bounds can never answer, and the owner named the signal exactly: *"a rounded arc on
one side, and an indented arc on the other."* **Curvature sign along T.**

1. Fit a plane to the face (the L×M plane through the centroid).
2. For each vertex, take its signed offset along T from that plane.
3. **The convex side is the one whose surface bulges AWAY from the plane in a smooth dome** — the mean
   offset of the outer shell is positive and its extremum sits near the face centre.
   **The concave side dishes INWARD** — its extremum sits near the rim, with the centre pulled back.
4. Equivalent and cheaper: compare the **centroid to the bounding-box centre along T**. On a dished
   plate the mass sits toward the convex shell, so the centroid is displaced toward the front. The sign
   of that displacement is the answer.
5. **The handle is the second confirmation.** The boss/strap is a local mass on the concave side — a
   cluster of vertices standing proud of the dish, near the face centre. Finding it both confirms the
   back and gives the grip point directly.

### The seat (owner, verbatim intent)

> *"The thinness/thickness of the shield is facing away from the player, with the handle where the hand
> mounts on the off-player's hand."*

- **T axis = face normal, convex side pointing AWAY from the player** (out at the world).
- **Concave/handle side faces the body**, and the **handle location is the grip point** on the off-hand.
- L runs vertically (top of shield up) when L > M; for a round shield L/M ≈ 1 and vertical is arbitrary —
  pick by the handle's own long axis if one is detectable, else by the authored frame, and Warn.

This replaces the current behaviour, which is: **identity when drawn** and a hand-typed
`(0, 90, 192)` when sheathed — a value whose own code comment concedes it has *"no relationship to
geometry OR the chest-bone axes."*

---

## 3. STAFF

### The mesh, described

A staff is a long shaft of near-constant cross-section, usually with a head, crook, crystal or ornament
at one end.

- **L (longest)** = the shaft. Dominates hard: L/M ≈ 10–25.
- **M ≈ T** — the shaft is essentially round, so the two smaller extents are nearly equal. **That near-
  equality is itself the archetype's signature** and is what separates a staff from a sword (flat: T ≪ M).

```
   HEAD (ornament / crystal / crook)                          FOOT
    |                                                            |
    v                                                            v
   (##)=========================================================
    ^  local bulge          near-constant cross-section
    0.0 ------------------ along L ------------------------> 1.0
                                      ^
                                grip at 0.75 from the FOOT
```

### How to tell the head from the foot (the disambiguator)

The **head is a local bulge**: a bin whose cross-section exceeds the shaft median by a clear margin
(≈1.5× or more), sitting within the outer ~20% of L. The foot is the plain end.

If no bulge exists — a plain quarterstaff — the ends are genuinely interchangeable. **Do not guess**: log
a Warn, keep the authored frame's +L as the head, and let an owner dial override.

### The seat (owner ruling, 2026-08-19)

> *"The longest length is Y, and you go three quarters of the way up Y, and that can be where the hand is
> attached for a staff."*

- **Grip at 0.75 along L**, measured **from the foot toward the head**.
- Head points up/forward, shaft roughly vertical.

> ⚠ **This SUPERSEDES the older canon line** in `docs/WEAPON_ARMOR_ORIENT_LOGIC.md` that says *"staff:
> longest vertical, grip lower third."* The owner's 2026-08-19 ruling is the live value: **0.75, not
> 0.33.** Record the supersession where the old line lives; do not leave two numbers in canon.

---

## 4. BOW — the existing model, unchanged

The bow already derives its seat and is **felt-verified correct by the owner (2026-08-19)**. It is the
template WO-1123 generalises from, not a target to modify.

- **L** = limb axis (tip to tip), **M** = the belly/back depth, **T** = limb thickness.
- Its disambiguator is the **string**, and the seat is solved by
  `WeaponBoundsOrient.ComputeBowHeldRotation` — limb axis vertical, belly facing the aim direction.
- Preserve its behaviour byte-for-byte when it becomes the BOW archetype inside the generalised helper.
  A regression here is a regression in something the owner has already accepted.

---

## 5. WHAT THIS DICTIONARY IS FOR, AND ITS LIMIT

Every rule above is a **measurement over vertices**, so each one can be logged with its inputs and
outputs and can therefore FAIL VISIBLY:

```
[Flow:Orient] archetype=Sword mesh='sword_A' extents L=0.98(Y) M=0.11(X) T=0.02(Z) ratio L/M=8.9
              guardSpike@0.86 tipEnd=-L gripAt=0.09 -> rot=(...)  rule=guard-landmark
```

A line like that is falsifiable: if `tipEnd` names the pommel, the screenshot and the log disagree and
somebody learns something. A line that only says "applied sword orientation" teaches nothing.

**The limit, stated plainly:** derivation gets the seat *close and consistent*, and it is not a substitute
for the owner's eye. `docs/ARCHITECTURE.md:155-159` records the bow shipping 90° out at the attach seat
while its derived value was arithmetically correct — *"a value can be derived correctly and still land
wrong one transform up the chain."* So the loop stays: **geometry proposes, the owner disposes, and
`manual: true` makes her correction permanent.** That flag is currently authored on 81 weapon rows and
read by nothing — fixing that is the first half of WO-1123, and it must land BEFORE any derived pass runs,
or the first automatic pass erases every pose she has ever dialled.
