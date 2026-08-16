# WORK ORDER 1032 — The guide wolf runs sideways: a hand-authored yaw fix for a mesh that was replaced

**Status:** READY TO IMPLEMENT (⚠ step 1 is measurement — see §3)
**Minted:** 2026-08-16 (UI seat) — provenance stack bumped 1032 → 1033 in the same edit
**Lane:** Pets / visual orientation. Disjoint from WO-1031 (which removes a dialogue, not the body).
**Provenance:** owner 2026-08-16, verbatim **"wOLF RUNS SIDEWAYS"**, with an FTUE screenshot — the guide
wolf at the gate, body perpendicular to its travel direction.

---

## 1. Leading hypothesis, from source — a correction that outlived its mesh

`PetDeployer.cs:442-443`:

```csharp
const float PetForwardYaw = -90f;   // +X (authored forward) → +Z (root forward)
visual.transform.localRotation = Quaternion.Euler(0f, PetForwardYaw, 0f);
```

The comment (`:432-441`) records exactly why it exists — **DEF-95**, owner field-test *"pet travels in
reverse"*:

> *"The pet meshes are Tripo exports (**ice-wolf = icecrystalfox3dmodel**) of the SAME family as the
> hero bodies, which import facing +X (EAST) in their bind pose… Apply the same single, consistent -90°
> yaw so visual forward == travel."*

**That constant is correct for the mesh it was written against — and that mesh has been replaced.**
`FoundingGuideWolfBodyRegression.cs:74` names `Assets/Art/Retired/Pets/ice-wolf-fox-legacy.fbx` as
**RETIRED**, and the owner confirmed 2026-08-16 that the guide is now **the wolf.fbx**.

So: a **hardcoded −90° correction authored for a Tripo fox is still being applied unconditionally to a
different, newer wolf mesh.** If the wolf is authored +Z-forward (the Unity norm) rather than +X, that
−90° is not a correction — it *is* the 90° error the owner is looking at.

⚠ **This is a HYPOTHESIS, not a conclusion.** It is well-evidenced but §12 forbids the edit until
measured. See §3.

## 2. ⚠ This is the canon "hand-authored vs derived" pattern, exactly

The 2026-08-06 canon thread names the recurring root cause of the project's worst bugs:

> *"**a flag authored BY HAND instead of DERIVED from the thing it describes**"* — `IsLoop` (53 of 122
> picks wrong) · `HeroTalentNodeDef.Hidden` (zero readers, its comment lied) · the UI capture resolution
> (a label, not a layout) · `CatalogBootstrap.RegisterFallback` (all three rows drifted).

`PetForwardYaw = -90f` is another one. It hand-encodes a property (*"this mesh faces +X"*) of an asset
that can be swapped without anyone touching the constant — and it was.

**Therefore: do NOT fix this by changing −90 to 0 (or to +90).** That replaces one hand-authored
constant with another and the next mesh swap breaks it again, silently, in the FTUE — the first sixty
seconds of the game.

## 3. STEP 1 — MEASURE the mesh's authored forward (do not eyeball it)

1. Instrument the deploy path: log the body prefab's name, its bind-pose bounds, and the applied yaw —
   `[Flow:Pets] body='<asset>' authoredForward=<axis> appliedYaw=<deg>`.
2. Determine the wolf's **actual authored forward axis** from the asset, not from the screenshot.
3. Confirm the same for the other two species (`flame-pup`, `aether-sprite`) — ⚠ if they are still
   Tripo +X exports, **a blanket change breaks them**. That is the trap: the constant may be right for
   two of three bodies and wrong for the one the owner sees most.

## 4. STEP 2 — the fix: derive it, then pin it

**Derive** the yaw from the asset (or carry it as **per-body data** beside the body path — the pattern
canon prescribes: *"Derive it, and PIN the owner's standing rulings above the derivation with their
reason"*).

⚠ **Precedent to follow, not reinvent:** `RepoProps.preservePrefabRotation` — a per-catalog-row opt-in
with `StructureFactory.OptsFor` as the single reader (`439e03ee`), created after a blanket rotation
change **laid the whole town on its side** with every gate green. Same class of bug, same shape of fix.

**And it needs an oracle.** ⚠ **Headless gates cannot see orientation** — canon's lesson of 2026-08-09,
where `PreservePrefabRotation` on all structures reproduced only on the dungeon→town return path *with
every marker green throughout*. A regression asserting "visual forward ≈ travel direction" after a
simulated move is the only thing that catches the next swap; a compile gate never will.

## 5. Acceptance criteria

- [ ] The guide wolf faces its **travel direction** throughout the FTUE walk to the gate
- [ ] `flame-pup` and `aether-sprite` are **verified unbroken** — the whole trap of this ticket
- [ ] The yaw is **derived or per-body data**, not a new global constant (§4)
- [ ] A regression pins "visual forward ≈ root travel forward" per species — headless markers alone
      cannot see this (§4)
- [ ] `FoundingGuideWolfBodyRegression` + `OneGuideBodyRegression` still pass **unmodified**
- [ ] The measured `[Flow:Pets]` line is pasted in the RESULT — the proof, not the screenshot

## 6. Verify

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites`
2. **Screenshot the FTUE walk** — memory `screenshots-are-primary-evidence-for-visual-defects`;
   orientation is precisely the class markers cannot judge
3. Owner felt-verifies + closes (§13)
