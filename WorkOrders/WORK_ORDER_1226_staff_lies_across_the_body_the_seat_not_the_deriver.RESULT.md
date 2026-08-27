# WO-1226 RESULT — bounce: Thrain's staff is in inventory, absent in the world

**Status of work:** implemented, not committed, Status line on the WO left as READY (owner/orchestrator flips).
**Silo:** `EquipmentController.cs` + `AttachmentOffsetRegression.cs` only.
**Not done here:** commit, Unity batchmode, Status flip, HUD, structures, Offset Forge.

---

## Bounce (owner felt-test 2026-08-27)

> "With the Thrain character, says i have staff from load (shows in inventory) but nothing is displayed"

This bounce is **DISPLAY MISSING**, not the original horizontal-orientation report. Inventory/loadout names the staff (`tripo_staff_a` / `mage_oak` → mesh `staff_A`); the world (and the inventory paper-doll camera) showed an empty hand.

The previous "Fixed" landing (`b303c4fbf`) only moved `_staffGripEuler` to `(90,0,0)` and split TrailRenderer out of the compensate AABB. That is the **orientation** half. It cannot make a missing prop appear. `tiltFromVertical=0deg` is what the broken build already printed.

---

## Proving cause (seat/visibility, not the deriver)

Captured on the two prior device builds the staff **was** instantiated (`renderers=2(inactive=0)`, ids `mage_oak` / `tripo_staff_a`, mesh `staff_A`). `renderers=2` was **not** a second mesh: `staff_A.fbx` has one Geometry. The extra renderer is the code-built **TrailRenderer** on `GripRoot` (WO-1226 first pass). That cube AABB never reached the deriver.

The Thrain bounce is a **later hole on the same seat**:

1. **KayKit `staff_A` is a ~2 cm Bits mesh.** `WeaponBoundsOrient.NormalizeInto` scales it to `heldLength` (1.30 m) from `Renderer.bounds`. If the mesh GO is inactive at instantiate (`importVisibility`, a hidden FBX node) or the renderer is not yet in hierarchy, `r.bounds` is empty, NormalizeInto **no-ops the scale**, and a 2 cm shaft is what "nothing is displayed" looks like. Shipped meshes may have Read/Write OFF — we do not touch vertices.

2. **`VerifyWeaponRendersNow` asked `r.enabled` only.** `r.enabled` is typically still true on an inactive GameObject. The verify **passed** an invisible prop and left it on the hero.

3. **`Transform.SetParent` does not copy layer.** `HeroPreviewViewer` clones the body onto `HeroPreview` and the preview camera culls that layer only. `Instantiate` leaves the staff on Default, so the paper-doll is empty-handed while the inventory **text** still says staff. World Default→Default is fine; Thrain is the class that has **no baked weapon**, so the paper-doll has nothing else to show. Grom's Paladin path can still read as armed from the bake.

4. **`PackageBakedGearMarker` skipped ALL weapon attach.** The Paladin bake is a sword. A Mage/Thrain body that inherited the marker (or a class swap that left it on) skipped the staff: inventory/loadout still named it, world hand empty. **This is the class-filtered skip the bounce named.** Sword/shield still skip (Paladin de-dupe). Staff/wand/mage ids now **attach anyway**, and `LateAttachRetry` no longer early-outs those ids.

5. **MagentaGuard deferred sweep** (`1s/3s/8s`, `hideStrayPrimitives:true`) will `r.enabled=false` a fallback **Cube** with a broken shader. A missed Resources load then becomes an invisible hero, not a grey box. `EnsureWeaponRenderersVisible` registers the prop via `ProtectPrimitiveArt` and re-enables mesh renderers.

**Branch at fault:** `AttachLoadedProp` (Resources path; Addressable completion uses the same method) — **both DRAWN and SHEATHED**, because they are the same prop reparented by `ApplyHoldPose`. The sheathe socket is now created on the **anchor bone's layer** so the reparent cannot drop the staff onto Default under a Preview camera.

**Not the deriver.** Did not flip `_sheatheLongAxisSign` (WO-1136). Did not touch `WeaponOrientHelper` shield substantiation (WO-1215). Did not touch structure `-90` rows or `TripoAxisBake`.

**Drawn staff verticality** from the previous landing is unchanged: `StaffDrawnGripNudgeDefault = (90,0,0)`. If a post-visibility screenshot still shows a 90° lie, the next cut is the **seat transform** (`_staffGripEuler` / hand-bone axes), not a new measurement.

---

## What changed

`Assets/_Modules/Village/Hero/EquipmentController.cs`

- `EnsureWeaponRenderersVisible` — activate hidden GOs, re-enable **mesh** renderers (trails/lines stay off), copy the **seat** layer, `MagentaGuard.ProtectPrimitiveArt`. `FlowTrace.Step("Equip", "SHOW …")` / `Fail` when `activeMeshRenderers=0`. **Not stripped.**
- Called **before** NormalizeInto (so bounds exist), after parenting to the hand, and **after** `ApplyHoldPose` (drawn hand vs sheathed hip).
- `CountActiveMeshRenderers` — enabled + `activeInHierarchy` + non-null mesh. Public for the regression.
- `VerifyWeaponRendersNow` now **skips inactive GameObjects** (`if (!r.gameObject.activeInHierarchy)`).
- `PackageBakedGear` no longer skips staff/wand/mage ids; `LateAttachRetry` still retries those.
- Sheathe socket `go.layer = anchor.gameObject.layer`.

`Assets/Editor/Regression/AttachmentOffsetRegression.cs`

- **Case 10 `[staff-loadout-shows-renderers]`** — FAILS if a staff in loadout has zero active mesh renderers after the shipped visibility pass. Not a tilt number.
  - Live: `Resources.Load("Heroes/Props/Weapons/staff_A")` + `EnsureWeaponRenderersVisible` must return `>0`.
  - Tombstone: all mesh renderers disabled **and** GOs inactive → `CountActiveMeshRenderers` **must be 0** (predicate has teeth; the old `r.enabled`-only verify would have gone green here).
  - Tombstone after `EnsureWeaponRenderersVisible` must return `>0`.
  - Source lint: attach calls the helper before seating **and** after `ApplyHoldPose`; verify skips inactive GOs.

---

## Landmines honoured

- Did not flip `_sheatheLongAxisSign`.
- Did not touch `WeaponOrientHelper` shield fix.
- Did not touch structure `-90` / `TripoAxisBake`.
- `staff_A` remains geometrically symmetrical (WO-1136); we did not invent a tip sign for it.
- Mesh Read/Write OFF: no vertex path; visibility uses renderer/GO/layer, scale still uses `Renderer.bounds` after the GO is active.
- Drawn vs sheathed: SHOW pass runs on whichever parent `ApplyHoldPose` left.

---

## Gates

Brace-balance + NUL: `EquipmentController.cs` 788/788, `AttachmentOffsetRegression.cs` 52/52, nuls=0.
**Not run (task: do not Unity batchmode):** `COMPILE_GATE_OK` / `REGRESSION_OK`. Orchestrator batch-gates.

Owner still felt-verifies on device: Thrain with a staff in loadout must show the shaft in town (sheathed, hip, vertical) **and** in combat (drawn, vertical, pointed end up). A `SHOW … activeMeshRenderers=0` line in the next F8 harvest is a fail, not a pass.
