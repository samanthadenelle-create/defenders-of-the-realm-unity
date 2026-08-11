# WORK ORDER 973 — Bryn's speech bubble is a giant skewed world-space card

**Status:** READY TO IMPLEMENT (first step is a re-shot — see §6)
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

**A screen-space canvas cannot skew.** The card renders as a trapezoid, which means it is a
**world-space** canvas being viewed in perspective, at wildly wrong scale for its distance. That
single observation removes the entire "is it a HUD panel?" branch before anyone opens a file.

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

Add to the same `FlowTrace` line, permanently (§12 — instrumentation is never stripped, only flagged
off): `canvasMode=`, `worldScale=`, `rectSize=`, `distToCam=`. Those four fields would have made this
a one-read diagnosis instead of a pixel discovery.

## 4. ⚠ DO NOT tune the scale yet — you would be tuning against a bug

For the entire run that produced these screenshots, the dungeon camera was **frozen at its bind
seat** (the WO-968 Cinemachine pipeline-cache race — camera stuck at `pos=(-28.50, 2.25, 3.20)
yaw=180`, one distinct pose across 43 heartbeats). Bryn sits at `(-31.00, 0.00, -2.00)`, roughly
**5 m from that stationary camera.**

So the card's apparent size in these shots is measured against a camera that was never where it
should have been. **Pick no numbers from these images.**

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
4. Write a regression that pins the bubble's world scale and rect against the authored values, so a
   future prefab edit cannot silently re-inflate it.

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
