# WORK ORDER 978 — Economy callers log the amount REQUESTED, not the amount CREDITED

**Status:** READY TO IMPLEMENT (regression slice) - the testable seam is DECIDED by the lead 2026-08-24: a **source-structural** assertion over the four callers, ⛔ **not** `internal` + `InternalsVisibleTo`. ⚠ The §6 behaviour question stays open for the owner and does **not** block this - the suite pins honest REPORTING, correct under every possible ruling.
>  PRIOR: **Status:** BLOCKED - owner question open. All four callers log measured before/after deltas, but ⛔ **§6 (what should happen AT cap) is still open for the owner**, and the regression + `DataRegression` registration were NOT added. *(Bucket corrected 2026-08-24: led with DONE while naming two open items.)*
>  PRIOR: **Status:** DONE — all four callers now log MEASURED before/after wallet deltas as `credited/requested` with a `Warn` on any shortfall; `EconomyService.cs` untouched (it was already correct). No site fell back to a bare `requested=` label: every callee is `void` (or returns levels/bool, not an amount), but each had an observable total (`EconomyService.Wood/Food/Iron/Crystals/Coins`, `GameState.Resources.*`, `GlimmerCurrencyService.Glimmer`, `HeroProgression.LifetimeXp`, `VillageInventory.Get`, `GameState.EchoCount`), so every axis is a real measurement. Also fixed in scope: `DailyQuestRewardBridge`'s latch-before-grant (same shape as WO-977) — a re-entrancy set now guards double-grants and `ClaimedAtUnix` latches only after a confirmed credit. **The §6 open question (what should happen AT cap) is still open for the owner — logging only.** Regression + `DataRegression` registration NOT added (lane-fenced to the committer).
**Lane:** Economy / instrumentation
**Severity:** player-facing, and **invisible in every capture** — which is what makes it expensive
**Minted:** 2026-08-10 (CLI), from the hollow-assertion audit (`docs/reference/HOLLOW_ASSERTIONS_REGISTRY.md`)

---

## 1. The defect

Four callers log the value they *passed in* as though it landed:

> ⚠ **Path correction (found during implementation):** three of the four paths below were wrong in
> the original mint — there is no `Village/Raids/`, `Village/Outposts/`, or `Village/Economy/`
> directory. Corrected here so the next reader does not grep for files that do not exist.

- `Assets/_Modules/Village/World/Camps/RaidVictoryController.cs:277`
- `Assets/_Modules/Village/Quests/DailyQuestRewardBridge.cs:126`
- `Assets/_Modules/Village/World/Camps/ChallengeOutpostVictoryController.cs:147`
- `Assets/_Modules/Village/Population/PopulationService.cs:211`

`EconomyService.Grant` routes to the **clampable `EarnedIncome`** kind. So when the town bank is at
cap, **the player is credited 0 while the log reads `+500 crystals`.**

## 2. ⚠ The authority is HONEST — do not "fix" `EconomyService`

`Assets/_Modules/Village/EconomyService.cs:416` already prints the **post-clamp amount** *and*
the **resulting total**. It is doing exactly the right thing.

**The bug is entirely caller-side.** Anyone who reads this ticket and starts editing `EconomyService`
is fixing the one component that got it right. Say it out loud in the fix commit.

## 3. Why this one matters more than it looks

This is the exact shape of *"I did the raid and got nothing"* — a complaint that is **unfalsifiable
from the logs**, because every log line agrees the reward was paid. It converts a real economy bug
into an argument about whether the player misremembered.

It also silently hides a design question: at cap, is paying 0 correct? Overflow, partial credit, and
a refusal message are all defensible; **silently paying nothing while announcing payment is not.**
Surface the clamp so that question can be asked.

## 4. Fix

1. Every caller logs the **returned credited amount**, never the argument.
2. When `credited < requested`, that is not a routine line — emit a `FlowTrace.Warn` naming both
   numbers and the reason (at cap), so a capture shows the shortfall instead of hiding it.
3. **Words, never colour alone** (owner is red/green colourblind): if any of these paths drive a
   player-facing reward popup, the popup must state the actual amount, and state plainly when a
   reward was capped. A number that silently differs from the announced one is worse than a refusal.

## 5. Acceptance criteria

- [ ] With the bank at cap, all four paths log `credited=0` (or the partial amount) and `Warn`, and
      **no** line claims the full amount.
- [ ] With headroom, behaviour and logs are unchanged.
- [ ] `EconomyService.cs` is **not** modified (it is already correct) — or, if it is, the WO explains
      why in writing.
- [ ] Player-facing reward text reflects the credited amount.
- [ ] Regression covering the at-cap and headroom cases, registered in `DataRegression.cs` (committer
      adds the registration — that file is lane-fenced).
- [ ] Brace balance + 0 NUL bytes (§1, §0).

## 6. Open question for the owner (do not decide this in code)

**At cap, what should happen?** Pay 0, pay partial, refuse with a message, or overflow into something
else? The current behaviour (pay 0, announce full) is certainly wrong, but the replacement is a design
call, not an engineering one. Ship the honest logging first — it is correct under every possible
ruling — and park the behaviour change on her answer.

## 7. Related

WO-976, WO-977, WO-973 — same failure class (asserting intent rather than outcome). Registry:
`docs/reference/HOLLOW_ASSERTIONS_REGISTRY.md`.

---

## ⭐ LEAD DECISION 2026-08-24 — the testable seam. This ticket is now HANDABLE.

The feeding agent correctly refused to hand this out: its acceptance owes **a regression and only a
regression**, but the two reporting helpers — `LogCredit` (`RaidVictoryController.cs:434-458`) and
`Report` (`DailyQuestRewardBridge.cs:177-236`) — are **`private static`**, and this repo's suites
**forbid `System.Reflection`** (`TownBankCapRegression.cs:9`). Handed out as written it comes back as
"cannot test this."

### ⛔ DECIDED: a SOURCE-STRUCTURAL assertion. NOT `internal` + `InternalsVisibleTo`.

- ⭐ **The precedent is already the house pattern** — `File.ReadAllText` source lints are standard in
  this folder (`AdPlacementCovenantRegression`, `AdServiceSeamRegression`,
  `AdGateAndArenaReturnRegression`, `AggroLeashRegression`, and more).
- ⛔ **`internal` + `InternalsVisibleTo` widens the PRODUCTION API surface permanently so a test can
  reach in.** A lasting cost paid for a test's convenience — and it invites the next caller to use the
  newly-`internal` member for something else entirely.
- ⭐ **The property IS structural**: *each of the four callers reads an observable total before and
  after, and logs `credited/requested` with a `Warn` on shortfall.* A shape question is what a source
  lint is for.

### The four callers to assert over

`Village/World/Camps/RaidVictoryController.cs` · `Village/Quests/DailyQuestRewardBridge.cs` ·
`Village/World/Camps/ChallengeOutpostVictoryController.cs` · `Village/Population/PopulationService.cs`

### ⚠ Assert on TOKENS, never exact strings

A lint that matches a whole formatted line **breaks on a reformat and reads as a real failure** —
which is how a suite gets switched off. Assert that each reporting block contains the `credited` and
`requested` tokens **and** a `Warn`, not one particular sentence.

⭐ **§6 does NOT block this.** The suite pins honest **reporting**, which is correct under every
possible answer to "what happens at cap". The behaviour half waits on the owner; the regression half
does not.

### Scope

- ⛔ **Acceptance bullet 4 (player-facing reward text) is OUT** — presentation, needs a captured PNG.
- One NEW `Assets/Editor/Regression/<name>.cs`, **read-only over the four callers**.
- ⚠ The `DataRegression.cs` registration line is **lane-fenced to the committer** — the seat writes
  the suite, the lead registers it. Two seats in the registry is how it collides.
