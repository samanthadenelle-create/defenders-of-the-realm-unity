# PROD-013 — The in-world repair marker renders as a giant opaque slab and pushes its own label off-screen

**Status:** FIXED 2026-08-24, awaiting owner felt-verify. **Silo:** Village/world.
**Reported:** owner felt-test, Seeker — *"Purple shader says repair but no option to repair"*.

## ⛔ It was NOT a shader defect — refuted by measurement

Sampled from the photo: **(119, 35, 225)**. `RepairHighlight.SelectedColor` is `(0.49,0.23,0.93)` = sRGB **(125, 59, 237)** — a match. Unity's error magenta is `(255,0,255)`; **R=119 rules it out**. A sweep of EVERY `.mat` in `Assets/` for a purple tint returned one unrelated hit. **The colour is correct and authored.**

⚠ Magenta-as-missing-shader is a real class in this project (the castle pink floor, CLAUDE.md §12), which is what made it a reasonable first read — and why it had to be measured rather than assumed.

## Three symptoms, one cause

1. **SIZE.** `RepairTarget.TryGetWorldBounds` encapsulated *every* renderer in the hierarchy — including VFX children (a Hovl aura measures **12.5 m** on device) and the baked mesh `HubStructureVisualInjector` hides with `r.enabled = false` (⚠ **not** `SetActive(false)`, so it is still returned by `GetComponentsInChildren` and still inflates the box). Extents 6.25 pinned the **9 m clamp** — a ~20 m slab over a ~3 m hut.

2. **OPACITY.** `BuildMarkerMaterial` set only `_Surface`/`_Blend`, which do **not** re-run URP's ShaderGUI at runtime, so no blend state was ever written; `renderQueue` alone only changes sort order. It drew fully opaque despite `alpha 0.85`. ⚠ Five other transparency sites in this repo already write the full `_SrcBlend`/`_DstBlend`/`_ZWrite` + keyword set — this was the lone "best-effort" variant, which is the tell.

3. ⭐ **THE LABEL — and this is what the owner actually reported.** `RepairLabel` is a **child of the marker transform** at `localPosition.y = 2.2`, so the oversized scale dragged it **off the top of the screen** (the screenshot shows only "Rep"). *"No option to repair"* was partly this: the affordance was rendered out of view.

## Fix

Bounds now count only renderers that are **enabled** and are not `ParticleSystemRenderer` (a particle system's bounds describe where particles may *travel*, not where the structure *is*), with a warned fallback to the unfiltered box rather than silently returning nothing. Full transparent blend state written explicitly, plus a trace naming which of the three shader fallbacks resolved — they behave differently under transparency and that was previously unrecorded.

## Acceptance

- [ ] Owner felt-verify: marker fits the structure, grass visible through it, label on screen
- [x] `COMPILE_GATE_OK` · `REGRESSION_OK 273/273`

## Note of record - 2026-08-24, `55bb991a4` (the dead "Repair?" prompt)

Recorded here because this is the closest governing ticket for the repair marker/prompt surface, and
because a shipped fix with **no ticket of record anywhere** is how a fix becomes invisible.

`55bb991a4` - *""Repair?" was a dead prompt for a live feature - a reflection seam had drifted
silently"* - touched `HudKitController.cs`, `VillageHudController.cs`, `RepairHighlight.cs`,
`RepairTarget.cs`, and added `Editor/Regression/RepairHudContractRegression.cs` pinning the 5-member
contract.

⛔ **It is NOT attributed to WO-1024, and the distinction is the point.** WO-1024 repaired
**installation / lifecycle timing**; `55bb991a4` repaired a later **reflection-contract drift between
`WallRepairHudBridge` and the HUD methods**. Same surface, **distinct failure mechanism**. WO-1024
closed 100 minutes after this landed, which makes the coincidence tempting and the attribution
wrong - and a guessed attribution is worse than none, because it retires a mechanism nobody actually
fixed.

⚠ This is a **note of record, not a reopening.** PROD-013's own status is unchanged.
