# WO-1479 RESULT - CANCEL quotes the refund it is about to pay

**Status:** AWAITING OWNER MATCH - device frame vs mockup panel 9 (QUEUE overlay) not yet passed (2026-09-07); code landed uncommitted in the working tree. The owner walked all nine Manage screens on build 358872 beside docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png and none matched; headless capture is evidence, never the verdict. *(was: IMPLEMENTED - 2026-09-07 uncommitted, awaiting gate)*
**Seat:** implementation lane (edit-only). No Unity run, no git, no commit.

## What was actually wrong (the WO's premise was half right)

Read at source before touching anything:

- `ManageScreenVM.cs:993` already set `RefundText = job.Paid.Describe()`, and
  `ManageScreenPanel.cs:5546-5556` already drew `"Refund: " + refundText` beside Cancel. So the
  NON-ZERO case was already quoted before the tap. "Never quotes the refund" is false for it.
- The ZERO case was **deliberately suppressed in the View** (`refundText == "nothing"`). The 18:39
  capture's bare CANCEL is that suppression firing. So the player who gets **nothing** back was the
  only player told nothing at all - and the rule deciding it lived in a skin (the WO-1512 breach
  shape): the View prefixed the sentence and then string-matched the model's text to hide it.

## What shipped

1. **`ObsidianQueueVM`** owns the quote (+93):
   - `RefundQuote { Basket, HasRefund, Line }`, `RefundPrefix`, `NoRefundLine`.
   - `QuoteRefund(JobCost)` - pure, runs on every row of every Rebuild, no trace, no service.
   - `QuoteRefund(ChannelId, structureId)` / `TryGetPaidBasket` - reads the LIVE job's v37 basket by
     walking `ActiveJobsOf` then `PendingJobsOf`, exact-id, **the same order and match as the
     cancel's own `FindInChannel`**, so row and service cannot disagree.
2. **`ManageScreenVM.MakeJobRow`** composes `RefundText` through `ObsidianQueueVM.QuoteRefund` (+15/-2).
3. **`ManageScreenPanel`** renders `r.RefundText` verbatim whenever non-empty; both view-side rules
   deleted (+16/-12).
4. **`ObsidianQueueRegression.CheckWo1479RefundQuote`** (+250), four cases: exact basket quoted;
   zero basket says "No refund - nothing was paid for this job"; **live round trip** on a real
   `BuildTimerService` (+ `GameStateService`) - quote, then `CancelChannelJobWithRefund`, then
   field-compare the quote against the basket the service actually credited, plus the basket-less
   twin; and a lint that the drawer no longer re-decides the zero case.

Brace-balanced + NUL-free on all four. FlowTrace untouched.

## Deviations / contradictions (owner or lead must rule)

1. **No cancel CONFIRM was built.** The lane brief asked for one; WO-1479 section 3 says the figure
   belongs where the decision is made, and `ManageScreenPanel.cs:150` (WO-1058 section 2.2)
   forbids a confirm dialog, cooldown or tap lockout in this panel. Inventing an unruled two-step
   arm on a destructive control was the wrong call to make silently. The quote is on the row,
   beside Cancel, before the press.
2. **The figure is NOT on the button face** (WO section 2 asks for it). Arithmetic from the file's
   own constants: cluster `0.455-0.72` of ~1490 ref px, split 2-3 ways = ~192/~122 px. "Refund: 120
   wood, 40 iron" ellipsises at `ElarionUiKit.FontFloor` - the "HIRE REIN..." failure this same file
   recorded yesterday. Text column is the only seat that holds it.
3. **The WO's evidence line (`ManageScreenVM.cs:2558 refunded`) points at the post-cancel Notice**,
   not the pre-cancel `RefundText` it did not know existed. `Cancel()` was left untouched (its
   "Nothing to refund." branch is pinned by `EconomySweepRegression` case 3).

## Owed before DONE

- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK n/n` on a fresh log (this seat ran no Unity).
- [ ] Fresh `ManageFlow_BUILD_queue` capture with a basket-less job in the drawer, PNG opened.
- [ ] Owner felt-verify (does the zero wording read as reassurance or as a warning?).
