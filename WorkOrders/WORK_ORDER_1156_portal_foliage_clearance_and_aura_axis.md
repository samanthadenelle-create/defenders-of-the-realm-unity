**Status:** FIXED 2026-08-23 (Codex) — runtime foliage cleared from the measured portal bounds and both approach directions, derived from live renderer bounds; portal seat UNCHANGED so WO-1062’s evidence still applies; clearance re-runs after the async art swap. ⭐ THE AURA WAS DIRECTIONAL — confirmed by opening the images, and it inherited the same wrong axis WO-1062 corrected; now +90° Y onto the measured Root.right doorway normal. PORTAL_ANGLE_CAPTURE_OK 40. ⚠ Foliage clearance still needs a fresh DEVICE approach capture. AWAITING OWNER FELT-TEST.

# WORK ORDER 1156 — Portal foliage clearance, and the threshold aura may inherit the wrong axis

**Minted:** 2026-08-23 (CLI, banner bumped 1156 -> 1157 in this SAME edit)
**Lane:** World / dungeon presentation. **Class:** SPLIT FROM A PARENT, PLUS ONE ADJACENT SUSPECT.
**Parent:** WO-1062 (portal reads differently from different angles) — §4 of that ticket instructs this split explicitly ("if non-trivial, split it into its own ticket").

---

## 1. FOLIAGE CLEARANCE / CAMERA STANDOFF (WO-1062 §4, unstarted)

The portal can be occluded by OuterWorld tree placement, and the approach camera can end up inside
foliage. Needs tree-collider queries against the actual placement rather than a fixed radius, which is
why it was too big to ride along with the axis fix.

⚠ **Do not solve this by moving the portal.** Its seat is derived, and WO-1062's captures are keyed to
that seat — a moved portal invalidates the evidence that ticket just produced.

## 2. THE ADJACENT SUSPECT — `AttachThresholdAura`

`DungeonWorldPortalSpawner.AttachThresholdAura` (~`:1107`) plays `Portal_Threshold_Aura` using
`p.Root.rotation`. **WO-1062 proved that the doorway normal on the shipping art is `Root.right`, not
`Root.forward`** — the ±90 had been pointing the rune planes at the arch's stone side, and the defect
survived a committed "fix" because nobody had looked at a picture from the actual approach angle.

If that aura effect is directional, it inherits the *same wrong assumption* from the *same root
rotation*. Nobody has checked.

⛔ **CHECK BEFORE CHANGING.** WO-1062 §5 says do not re-tune the aura, and that still holds. The task
here is to determine whether the effect is directional at all. If it is radially symmetric, record
that and close this half — a "fix" to a symmetric effect is a change with no observable difference,
which is worse than nothing because it looks like progress.

## ⛔ 3. HOW TO JUDGE THIS TICKET — the parent's lesson, which cost real time

> **A NUMERIC GATE SCORED THE PORTAL BACKWARDS.** The broken edge-on configuration produced a HIGHER
> bright-pixel count than the correct face-on one, because edge-on smearing inflates it. The captures
> were the data; every number available agreed with the wrong answer.

So: **capture from several angles and OPEN THE IMAGES.** Do not report a conclusion from a filename,
a log line, or a pixel count. Use the existing harness — `DeNelle.Editor.StructurePoseCapture.RunPortalAngles`
(`Assets/Editor/StructurePoseCapture.cs:161-410`, marker `PORTAL_ANGLE_CAPTURE_OK`), which already
orbits 8 headings × 3 configs and stages the real `Portal.fbx`. Extend it; do not write a fourth
capture harness.

Baseline to compare against: `docs/ui-evidence/portal-angles-2026-08-23/` (24 PNGs, magenta = 0).

## ⛔ CONSTRAINTS

- ⚠ **AABB CANNOT PROVE ORIENTATION** (+90 and −90 are bounds-identical). If orientation is in
  question, use `JewelerPitchSolver.TaperRatio`. The basis-vector test also lies on these meshes.
- Do NOT hand-edit `.unity` scenes; do not bake with the editor open.
- Owner is RED/GREEN COLOURBLIND — the portal currently reads by LUMINANCE (near-white on dark stone).
  Keep it that way; never let hue carry the read.
- NEVER strip FlowTrace/Guard instrumentation (§12). The axis, geometry and resolved normal are now
  logged — keep them.
- Content is CDN-served and content-hashed: if you re-bake or change an Addressable, an R2 push is
  owed (`tools\r2-ship.ps1`). WO-1062 itself owed none — both its edits were `.cs`.

## ACCEPTANCE

- [ ] The portal is not occluded by foliage from any approach heading, shown in captures
- [ ] The approach camera never ends up inside foliage
- [ ] `Portal_Threshold_Aura`: stated in the RESULT as either directional (and corrected, with
      before/after images) or radially symmetric (and left alone, with the evidence)
- [ ] Magenta still 0 across the capture set
- [ ] The portal's seat is unchanged, so WO-1062's evidence still applies
