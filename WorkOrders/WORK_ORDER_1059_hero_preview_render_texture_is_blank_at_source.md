**Status:** READY TO IMPLEMENT — the instrument has already named the layer; fix the source, not the panel

# WORK ORDER 1059 — The hero preview render texture is blank AT THE SOURCE

**Minted:** 2026-08-22 (UI seat — Claude UI; UI-block banner bumped 1059 -> 1060 in the SAME edit)
**Assigned:** CLI implements. UI writes no `.cs` (CLAUDE.md §2).
**Lane:** Village / hero presentation
**Class:** DEFECT. **Blocks the Gear section of WO-1133 and nothing else.**
**Source:** F8 capture **seq=3585**, `logs/f8-inbox/capture-20260822-120045-seq3585.md`,
2026-08-22 12:00:45, `t=4568s`. Prior sighting: **seq=2833** on the paper-doll path.

---

## 0. One-line truth

**Both hero-preview call sites render nothing, and the probe already says where the fault is.**

> *"RT PROBE: the preview render texture is a UNIFORM clear colour — the preview box is blank at the
> SOURCE, not at the panel. Fix the model/culling, not the RawImage."*
> — `HeroPreviewViewer.ProbeRenderedContent`, `HeroPreviewViewer.cs:411`

This is a §12 success story: the instrument existed, it fired, and it eliminated an entire class of
wrong fix before anyone opened a file. **Do not go looking at RawImages, anchors or panel layout.**

---

## 1. What the capture proves

```
HeroPreviewViewer:ProbeRenderedContent   (HeroPreviewViewer.cs:411)
EquipmentPanel:BeginOrRetargetPreview    (EquipmentPanel.cs:1243)
EquipmentPanel:RenderPreview             (:555)
EquipmentPanel:Render                    (:544)
EquipmentPanel:Bind                      (:531)
EquipmentPanel:Open                      (:333)
HeroInventoryController:OpenGearPreview  (InventoryUIBuilder.cs:341)   <- the VIEW GEAR ribbon
```

**Both known consumers are blank:**

| Path | Evidence |
|---|---|
| `InventoryPaperDoll` (the bag's own preview box) | F8 **seq=2833** |
| **`EquipmentPanel`** (the full gear screen) | F8 **seq=3585** — this capture |

So it is **not** a per-panel wiring mistake. It is the shared rig: `HeroPreviewViewer` produces a
uniform clear colour for every caller.

⚠ **This closes WO-1133's D1 question.** That ticket asked for exactly this probe before layout work,
named `EquipmentPanel` as the untested path, and said a blank result there would be a separate defect
blocking the Gear section only. **That is what happened.** WO-1133 has been updated; the rest of that
redesign is unblocked.

---

## 2. Where to look — the rig's own documented preconditions

`HeroPreviewViewer`'s header states how it works, and each clause is a candidate. Instrument each and
capture which one is false before editing:

| # | Precondition (from the header / code) | How it fails silently |
|---:|---|---|
| 1 | Clones the actor body onto a far-off origin on a dedicated **`HeroPreview` layer** | The layer is **optional in TagManager** (layers 9-31 are unnamed in this project) and the code falls back to **layer 31**. If anything else claims 31, or the camera's mask and the clone's layer disagree, the camera renders empty space |
| 2 | A **DISABLED** camera driven manually via `camera.Render()` — *"URP SKIPS an off-screen Base camera in its auto render loop"* | If the manual `Render()` is not reached on the frame the probe samples, the RT is whatever it was cleared to |
| 3 | Strips gameplay MonoBehaviours / colliders / rigidbodies from the clone | Over-stripping could disable the renderers themselves |
| 4 | The source body must exist | `Begin` returns false and the panel skips the preview — **but the probe fired, so a viewer WAS created**; this is likely not it |
| 5 | A key light is created with the rig | No light + unlit-sensitive materials = a black RT, which reads as "uniform clear colour" |

**⛔ Candidate 1 is the strongest lead and is the cheapest to test:** log the resolved preview layer,
the clone's actual layer, and the camera's `cullingMask` in one line. If they disagree, the RCA is
done. **Confirm it; do not assume it.**

⚠ Note the timing: this capture is at `t=4568s` — **76 minutes into the session**, after a scene had
been loaded and a great deal had happened. Check whether the rig works on a fresh open and degrades,
or never works at all. **A "never worked" and a "stopped working" have different fixes.**

---

## 3. What NOT to do

- **Do not touch the RawImage, its anchors, or any panel layout.** The probe explicitly rules that
  out. Changing presentation here would produce a "fix" that changes nothing and closes the ticket
  falsely.
- **Do not build a second preview rig.** `HeroPreviewViewer` is used by six call sites; a parallel
  path would double the surface and leave five of them broken.
- **Do not delete or weaken `ProbeRenderedContent`.** It is the only reason this defect is
  diagnosable, and instrumentation is permanent (§12). If anything, it should also fire on the
  paper-doll path.
- **Do not fix this inside WO-1133.** Different system, different lane.

---

## 4. Acceptance

1. A captured line names **which** precondition in §2 was false. **No edit before that line exists.**
2. `EquipmentPanel` shows the live dressed hero — weapon, shield and armour tier mirrored, as the
   rig's header promises.
3. The paper-doll path (seq=2833) is **re-probed and also green**, or is deliberately removed by
   WO-1133 — state which.
4. `ProbeRenderedContent` **stops firing** in a normal session, and would still fire if the rig broke
   again (prove by temporarily breaking it).
5. The other four `HeroPreviewViewer` consumers are unregressed: `PartyShopPanelMvvm`,
   `BuildingUpgradePanelMvvm`, `BuildPreviewModal`, `MotionCasterWindow`.
6. Verified **on a fresh open AND after ~an hour** (§2 timing note).
7. `COMPILE_GATE_OK`; brace-check every `.cs`; screenshots opened, not just taken.

## 5. Files

**Read first:** `logs/f8-inbox/capture-20260822-120045-seq3585.md` ·
`Assets/_Modules/Village/Hero/HeroPreviewViewer.cs` (header + `ProbeRenderedContent` at `:411`) ·
`Assets/_Modules/Village/Hero/EquipmentPanel.cs:1243`

**Likely edit:** `HeroPreviewViewer.cs` (layer resolution / camera mask / manual render).

**Related:** `WorkOrders/WORK_ORDER_1133_inventory_screen_redesign.md` §D1 — this WO is its blocker
for the Gear section only.
