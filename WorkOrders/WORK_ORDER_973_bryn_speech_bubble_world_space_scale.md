# WORK ORDER 973 — Bryn's speech bubble is a giant skewed world-space card

**Status:** DONE — closed by the owner 2026-08-19.
**Lane:** Dialogue / presentation
**Silo:** `Dungeon_HealersCottage` NPC speak bubbles
**Minted:** 2026-08-10 (CLI). Banner bumped 973 → 974 in the same edit.
**Found by:** the WO-968 headed dungeon proof — **in the pixels**, not by the owner.

---

## 1. Symptom (screenshot-proven)

During a headed run of `Dungeon_HealersCottage`, screenshots `01_idle` through `05_right` are roughly
**60 % covered** by a large skewed trapezoid card reading:

> "Bryn the Wa… / The path opens easy… / mind the rocks — th… / cottage keeps her sh…"

The text is **clipped off the right edge** in every shot. This is a **readability defect, not a
cosmetic one** — the player cannot finish the sentence.

Artifacts: `scratchpad/run1/01_idle.png` … `05_right.png` (same run as the `Player.log` below).

## 2. What the shape already proves

**A screen-space canvas cannot skew.** The card renders as a trapezoid, which means it is being
viewed in perspective — i.e. it lives in the world, not on the HUD. That single observation removes
the entire "is it a HUD panel?" branch before anyone opens a file.

> ### ⚠ CORRECTION (2026-08-10, read-only prep) — the CONCLUSION holds, the MECHANISM above was wrong
>
> This section originally said *"it is a **world-space canvas**"*. **There is no Canvas at all.**
> `Assets/_Modules/Dungeons/Wanderer/WandererBubble.cs:2-21` documents why at source: the project
> renders UI through **UI Toolkit**, and a UGUI world-space Canvas would drag `UnityEngine.UI` into
> the `DeNelle.Dungeons` asmdef. So the bubble is a **`TextMesh` + a `Quad` primitive** — raw 3D
> geometry.
>
> **Consequence: do NOT "fix" this by converting it to screen-space or by adding a Canvas.** That
> reintroduces the exact asmdef dependency the design deliberately avoided (§5 assembly law). The
> world-space choice is deliberate and documented, not an accident.
>
> Two further corrections to the symptom reading, both **independent of the frozen camera**:
> - **The text is not clipped by the panel.** `ResizePanelToText` (`:134-158`) only ever *grows* the
>   quad (`Mathf.Max` against the minimums, `:154-155`), and a `TextMesh` is not masked by a quad
>   anyway. All four lines truncating at the same right boundary means they run off the **viewport**.
> - **Wrap and scale are ONE problem, not two.** `_wrapWidth` sets line length → text bounds → panel
>   growth. Tuning them separately will chase its own tail.

Paired data from the same run:

```
[Flow:Dungeon] Bryn.Configure 'bryn-the-wanderer' at (-31.00, 0.00, -2.00) (speakRadius=6, bubble=ok)
```

It clears once the hero leaves the 6 m speak radius — which is what proves the offending object is
**this** speak bubble and not some other overlay.

## 3. The instrumentation lesson (fix this too)

`bubble=ok` reported **success while the thing was unreadable.** The trace asserts *construction*
and says nothing about *legibility* — no scale, no canvas mode, no distance, no rect size. A green
line next to a broken screen is worse than no line, because it actively steers the next reader away.

Add these fields permanently (§12 — instrumentation is never stripped, only flagged off):
`worldScale=`, `rectSize=`, `textBounds=`, `wrap=`, `distToCam=`. `textBounds` and `wrap` are what
actually *drive* the panel growth — without them you see the symptom but not the cause.

> ### ⚠ TWO CORRECTIONS to this section (2026-08-10 prep) — both matter
>
> **1. `canvasMode=` must NOT be implemented.** There is no Canvas (see §2 correction), so that field
> would print a constant lie forever — the very disease this section exists to cure. Use
> `render=TextMesh+Quad (no Canvas)`, which states the actual mechanism.
>
> **2. The emit point moves out of `Bryn.Configure`.** At `Configure` the bubble has never been
> shown, the panel is still at its authored minimum, and `Camera.main` may not be seated (that is the
> WO-968 race). Worse, `_resizePending = 3` (`WandererBubble.cs:124`) means the final size settles up
> to **3 frames after `Show`**. A trace at `Configure` — or even at `Show` — reports a pre-settle
> size: *another line that asserts construction and stays silent on legibility.* It belongs in
> `WandererBubble`, in `LateUpdate` on the frame `_resizePending` hits 0, where all values are real.
> **`Show` is currently untraced entirely.**
>
> **3. Bryn's own line should stop saying `ok`.** `bubble={(_bubble != null ? "ok" : "MUTE")}` is a
> non-null cast check and nothing more. `bubbleSeam=wired/MUTE` is honest about what it proves and
> stops reading as "the bubble is fine".

## 4. ⚠ DO NOT tune the scale yet — you would be tuning against a bug

For the entire run that produced these screenshots, the dungeon camera was **frozen at its bind
seat** (the WO-968 Cinemachine pipeline-cache race — camera stuck at `pos=(-28.50, 2.25, 3.20)
yaw=180`, one distinct pose across 43 heartbeats). Bryn sits at `(-31.00, 0.00, -2.00)`, roughly
**5 m from that stationary camera.**

So the card's apparent size in these shots is measured against a camera that was never where it
should have been. **Pick no numbers from these images.**

## 4b. Where the numbers actually live — TWO places, and one that cannot be tuned at all

`Bryn.Configure` does **not** build the bubble; it only casts a pre-wired reference
(`Bryn.cs:137`, `bubble=ok` at `:157-158`). `WandererBubble` self-builds in `Awake()` → `Build()`
(`:169-205`), and is attached at **bake** time by `Assets/Editor/DungeonSceneBuilder.cs:1312-1325`.

**There is no prefab** — so this is not a five-minute asset fix. But it is also **not a pure code
fix**, and this is the trap:

| Value | Code default | Baked into `Dungeon_HealersCottage.unity:7395-7400` |
|---|---|---|
| `_panelWidth` | 4.4 | 4.4 |
| `_panelHeight` | 1.7 | 1.7 |
| `_wrapWidth` | 34 | 34 |
| text scale | **hardcoded `Vector3.one * 0.16f` at `:195`** | not serialized — **cannot be tuned from the scene** |

**Editing the code defaults alone changes nothing**, because the scene holds its own serialized copy.
The fix needs the code defaults changed **and a dungeon re-bake** — and per the shared-tree corruption
history, that re-bake happens in an **isolated worktree**, never in the shared tree.

## 4c. The reference implementation — a sibling with sane numbers

`IWandererBubble` / `WandererBubble` is dungeon-only; no other NPC uses it, so the blast radius of
one bubble is correct. **But a parallel implementation already ships:**
`DeNelle.Village.TownsfolkBubble` (`Assets/_Modules/Village/NPCs/TownsfolkBubble.cs`), used by every
village NPC prefab under `Assets/Resources/NPCs/CraftPixPeople/`.

| | WandererBubble (Bryn) | TownsfolkBubble (village) |
|---|---|---|
| `_panelWidth` | **4.4** | **1.8** |
| `_panelHeight` | **1.7** | **0.7** |
| `_wrapWidth` | **34** | **22** |
| text scale | **0.16** (hardcoded) | **0.07** (serialized) |
| auto-hide / outline | none | 4.5 s / outline quad |

Bryn's card is ~2.4× wider, ~2.4× taller, with ~2.3× larger text than the shipped village bubble.
**This comparison is authored-value against authored-value and does NOT depend on the frozen camera**
— so it is usable evidence today: 4.4 is the outlier, not the baseline. It won't give the final
number, but it gives the re-shoot something to aim at.

⚠ Worth naming on its own: **two independent bubble implementations exist.** That is a structural
smell (§2b two-stack scar). Not this WO's job to merge them, but do not add a third.

## 5. What NOT to touch

- Do **not** "fix" this by moving, re-seating or re-parenting the dungeon camera. That is WO-968's
  fence and its fix is already written.
- Do not convert the bubble to screen-space as a shortcut — world-space is presumably deliberate
  (it belongs to an NPC in the world). Confirm the intent before changing the mode.
- Bryn's dialogue text/content is not in scope; this is purely presentation.

## 6. Steps

1. **Re-shoot first.** Once the WO-968 camera fix is in a build, take a fresh headed run of
   `Dungeon_HealersCottage` and walk into Bryn's 6 m radius. Measure the card against a *working*
   camera.
2. If it is still oversized/clipped, fix the world-space canvas scale and the rect's text wrapping so
   the full line fits at the ranges a player actually reads it from (i.e. anywhere inside 6 m).
3. Add the four trace fields from §3.
4. Write a regression — but **not** the one this line originally described.

   > ⚠ It first said *"pin the bubble's world scale and rect against the authored values."* **The
   > authored values ARE the defect**, and they exist in **two places that can silently diverge**
   > (the code defaults and the scene-serialized copy — see §4b). Pinning against "authored" would
   > pin the bug in place.
   >
   > Pin instead: **code default == scene-serialized == the post-re-shoot sane value.** That drift is
   > exactly how a corrected default ships with no visible effect, because the bake still carries the
   > old number.

5. Re-bake the dungeon **in an isolated worktree** (shared-tree bake corruption history), then
   re-shoot to confirm.

## 7. Acceptance criteria

- [ ] Headed screenshots at 3 distances inside the speak radius show the **complete** text, unclipped.
- [ ] The bubble occupies a sane fraction of frame (nothing near the ~60 % observed here).
- [ ] `[Flow:Dungeon] Bryn.Configure` emits `canvasMode` / `worldScale` / `rectSize` / `distToCam`.
- [ ] Regression registered in `DataRegression.cs` (committer adds the registration — that file is
      lane-fenced) and the gate prints its marker.
- [ ] Brace balance + 0 NUL bytes on every `.cs` touched (§1, §0).

## 8. Provenance

Discovered by the WO-968 dungeon SME during the owner-ordered headed proof run
("checked with headed proof, and the matching data inside halers cottage"). Reported to the
orchestrator with screenshots and the matching `Player.log`; filed here so the dialogue lane owns it
rather than the dungeon lane.
