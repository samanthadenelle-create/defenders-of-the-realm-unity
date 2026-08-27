# WORK ORDER 1251 - Crystal Mine renders colourless: a NULL material slot on its renderer

**Status:** CLOSED 2026-08-27 — owner Pass (felt-validated).
**Silo:** Art pipeline / structure assets
**Severity:** P2. A built structure in the player's town renders wrong.
**Origin:** Owner, on device, 2026-08-27: *"crystal mine lost colors - flagged"*. Tester APK built
11:47, commit `fffa4ea9c`.

---

## ⭐ THE CAUSE IS ALREADY IN THE CAPTURED DATA. DO NOT RE-DIAGNOSE IT.

F8 device captures seq 3618 and 3619 both say, verbatim:

```
[Flow:StructureAssets] dep MISS on 'Structures/CrystalMine':
renderer 'CrystalMine' has a NULL material slot - that renderer draws engine-default
```

That is the dead step, named by the instrumentation, on the owner's device. **A static read of the
prefab locates candidates; this line concludes.** Go straight to the null slot.

⭐ This is the same failure class as the castle "pink floor", which cost three guess-and-fix cycles
against a terrain theory that was wrong, and was then settled by one headless dump naming colourless
URP/Lit tiles. A renderer with no material draws the engine default. That is the whole bug.

## What to check, in order

1. The `Structures/CrystalMine` prefab's renderer material array - which slot is null, and whether
   the mesh has more submeshes than the prefab has materials.
2. Whether the material exists but failed to resolve (an unpushed or re-hashed bundle) rather than
   being genuinely unassigned. ⛔ **These two look identical on screen and have completely different
   fixes.** The capture says NULL SLOT, which points at authoring, but confirm before editing.
3. Whether other structures share the same source material and are one edit away from the same fault
   - **sweep the set, do not fix the single instance.** Search by the shared token (basecolor /
   diffuse / the material name), not by "CrystalMine", because a name-first search only confirms the
   guess and cannot discover siblings.

## ⛔ Section 16 applies if it turns out to be a bundle

If the material resolves through Addressables, remember the art is served from the R2 CDN with **no
local fallback**, and **bundle names are content-hashed so every content build needs its own push**.
A missing push renders as placeholder art with **no error on screen**. The one sanctioned path is
`tools\r2-ship.ps1` - do not re-inline a push or a verify anywhere.

## Also note

`MagentaGuard` exists because URP renders the built-in Standard shader magenta and a magenta
primitive gets hidden. **Do not "fix" this by hiding the renderer** - a hidden structure is worse
than a grey one, because it silently removes a building the player owns.

## Required

1. The null slot identified and filled with the correct authored material.
2. A statement of whether it was authoring or resolution (see check 2 above).
3. The sweep result: which other structures share the fault, if any.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts read off the marker.
2. ⭐ **A screenshot of the Crystal Mine rendering correctly.** Screenshots are primary evidence for
   visual defects - `FlowTrace` shows what the code believes, the image shows what the player sees.
3. A regression asserting **no structure renderer has a null material slot** - the general form, not
   a CrystalMine special case. Prove RED first (WO-1138) by nulling a slot and watching it fail.
   ⛔ It must not be a hollow pass: if it cannot enumerate the structure set it must assert or emit
   `RegressionOutcome.Skip`/`PartialSkip`, never return quietly green.
4. Owner felt-verifies on device.

## What NOT to touch

- ⛔ `MagentaGuard`'s primitive protection.
- ⛔ Do not hide, disable, or substitute the renderer to make the symptom go away.
- ⛔ Do not re-run the Addressables grouper "to be safe" - that re-hashes every bundle and obliges a
  full R2 push, which is how every enemy became a capsule on 2026-08-20.
