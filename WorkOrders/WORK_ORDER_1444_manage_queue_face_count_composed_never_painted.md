# WO-1444: the Manage QUEUE face count is composed by the model and painted by nobody

**Status:** SPEC - needs an owner ruling before implementation (two valid fixes, they contradict each other)
**Silo:** `ManageScreenPanel` / `ManageScreenVM` / `ManageViewContract` (the Manage 2000-block). Disjoint from
every raid, wallet, HUD-rail and Build-mode lane.
**Source:** found 2026-09-06 while re-verifying WO-1443 at source (its section 7B). Minted by the CLI seat from
the banner (`CLI_LANES_WO_NUMBERS.md`, main line 1444 -> 1445 in the same edit).

---

## 1. THE FINDING, AT SOURCE

WO-1443 §1B deleted the `IDLE . 0 OF 5` line on the understanding that the queue count would ride on the QUEUE
face instead. The model composes exactly that (`ManageScreenVM.cs:3481`):

```csharp
FaceCountText = cap > 0 ? (full ? "FULL" : depth + "/" + cap) : null,
```

**Nothing reads it.** A repo-wide search for `FaceCountText` returns the declaration
(`ManageViewContract.cs:343`), two comments, and that one assignment. The pill label is hardcoded
(`ManageScreenPanel.cs:2091`, `label.text = "QUEUE";`) and the red badge beside it is recomputed by the View's own
sum across channels (`ManageScreenPanel.cs:2368-2372`), not from the model.

So there are two authorities on queue fullness, one of them unreachable, which is the composed-but-unpainted
state `ManageViewContract.cs:337-341` says it was written to avoid. The word `FULL` never reaches a player: a
player with a full queue sees a badge count and nothing that says the next tap will be refused.

## 2. WHY THIS IS AN OWNER CALL, NOT A CLI FIX

The screen as rendered **matches the owner's mockup** (WO-1443 §7, twenty-four capture rounds). The two fixes:

- **(a) Paint `FaceCountText` on the pill** (`QUEUE 3/5`, `QUEUE FULL`) and delete the View's private badge sum so
  the model is the one authority. Changes the picture the owner approved.
- **(b) Delete `FaceCountText`** and keep the badge. Keeps the picture; drops the `FULL` affordance §1B assumed and
  leaves the View computing state the architecture says belongs in the VM (`docs/ARCHITECTURE_PRINCIPLES.md`,
  presentation never owns the objects).

Recommendation, stated so the ruling is one word: **(a)**, with the count on the pill and the badge retired. The
mockup shows the pill; adding `3/5` inside it is the smaller visual change and it is the one that tells the player
why a tap is refused. If the owner wants the picture untouched, (b) plus a `FULL` toast at the refused tap.

## 3. ACCEPTANCE (once ruled)

- [ ] Exactly ONE authority for queue fullness reaches the screen (VM), pinned by a case in
      `ManageOneHeadingRegression.cs` or the queue drawer suite that fails if the View recomputes the sum.
- [ ] A full queue is visible as the word `FULL` somewhere the player looks before tapping (pill or toast).
- [ ] `ManageScreenPanel.TroopSprite` (`:3739`), a second dead troop-art loader found in the same pass
      (`RenderList()` early-returns at `:2899` because `WorkspaceActive` is always true), is deleted so there is one
      art path. Not a ruling; housekeeping in the same silo.
- [ ] `REGRESSION_OK n/n` on a fresh log; headless `Manage*` PNGs opened.
