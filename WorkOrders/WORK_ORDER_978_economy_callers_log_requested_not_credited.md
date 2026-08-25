# WORK ORDER 978 — Economy callers log the amount REQUESTED, not the amount CREDITED

**Status:** BLOCKED - ⚠ **the REGRESSION SLICE LANDED 2026-08-24 (`78c3ecbec`)** — `Assets/Editor/Regression/EconomyCreditReportingRegression.cs` is committed AND registered in `DataRegression.cs`, and it has now EXECUTED and PASSED: `[economy-credit-reporting] ECONOMY_CREDIT_REPORTING OK` inside `REGRESSION_OK 277/277` on `Builds/reg-wave6.log`. *(Status audit 2026-08-25: the prior "NEVER EXECUTED / the run is OWED" clause was true on 2026-08-24 and is now FALSE; that half is proven. Bucket UNCHANGED - this ticket stays BLOCKED on the owner-sent-back §1/§6 reconciliation below, which is untouched.)* The block below is unchanged and still governs the §1/§6 doc reconciliation. *(Status audit 2026-08-24.)* ⛔ **SENT BACK by the owner 2026-08-24** (batch 2, ruling 5): the §6 recommendation used a **crystal** example, but crystals are the one UNCAPPED currency - verified at source today (`TownBankCapacity.cs:238-242`, `:478-482`; `EconomyService.cs:469-476`; `TownBankCapRegression.cs` case `[no-crystal-cap]` fails the build if that ever changes). ⛔ This ticket's §1 crystal example is factually wrong. **The unblock: reconcile §1 and §6 onto capped resources (wood/iron/stone) only.** ⚠ The regression slice is unaffected. *(Prior line:)* READY TO IMPLEMENT (regression slice) - the testable seam is DECIDED by the lead 2026-08-24: a **source-structural** assertion over the four callers, ⛔ **not** `internal` + `InternalsVisibleTo`. ⚠ The §6 behaviour question stays open for the owner and does **not** block this - the suite pins honest REPORTING, correct under every possible ruling.
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

---

## ⛔ SENT BACK 2026-08-24 — batch 2, ruling 5: the recommendation contradicted itself, and this ticket's §1 is factually wrong.

**The owner caught it.** The §6 recommendation used *"Storage full — 240 of 500 **crystals** collected"*
as its worked example. ⛔ **But WO-1165 establishes crystals as the one UNCAPPED currency.** Both
cannot be canon.

### ⭐ HER RULING

- **Crystals remain UNCAPPED and ALWAYS PAY IN FULL.** ⛔ This ruling must **never** be implemented in
  a way that introduces a crystal cap, implicitly or otherwise.
- **Capped resources — wood / iron / stone — pay what fits, DISCARD the overflow, and disclose exactly
  what was collected.** The honest sentence, with a capped resource in it:
  > *"Storage full: 240 of 500 stone collected."*
- ⛔ **No secret overflow wallet, either way.** An overflow store is a second wallet with its own caps,
  its own UI and its own bugs, bought to avoid a sentence.
- ⛔ Never colour alone, and never a number that quietly differs from the announced one.

### ⭐ VERIFIED AT SOURCE 2026-08-24 — crystals are NOT capped anywhere in the code

Read at source, not assumed. The owner's ruling matches the code; **this ticket's §1 does not.**

- `Assets/_Modules/Core/Economy/TownBankCapacity.cs:238-242` — `UncappableResources = { BankResource.Crystals, BankResource.Coins }`.
- `TownBankCapacity.cs:290-296` — `IsCapped()` is false for those, so the capped set is exactly **Wood, Iron, Food**.
- `TownBankCapacity.cs:478-482` — **the** clamp, `ClampGrant`; line 481 returns `requested` unchanged for an uncapped resource, with the owner ruling cited inline.
- `TownBankCapacity.cs:381-383` / `:407-409` / `:436-438` — `BaseCapOf` / `MaxOf` / `RoomFor` all early-return `int.MaxValue` for crystals: infinite ceiling, infinite headroom.
- `Assets/_Modules/Village/EconomyService.cs:469-476` — the crystal credit path makes **no clamp call at all**; it goes straight to `GameStateService.AddCrystals`.
- `Assets/_Modules/Core/State/GameStateService.cs:440-448` — `AddCrystals` applies a **floor only** (`Mathf.Max(0, …)`), never an upper bound.
- `Assets/Resources/Data/Canonical/storage-caps.json` — authors caps for **wood / iron / food only**; a `crystals` key would be *unreachable* because `BaseCapOf` returns `int.MaxValue` before the dictionary is consulted.
- `Assets/Editor/Regression/TownBankCapRegression.cs:162-191` — case `[no-crystal-cap]` **fails the build** if crystals or coins ever become capped.

⛔ **CORRECTION OWED IN THIS TICKET'S §1.** `§1` (lines ~26-27) claims *"`EconomyService.Grant` routes
to the clampable `EarnedIncome` kind. So when the town bank is at cap, the player is credited 0 while
the log reads `+500 crystals`."* **That specific scenario cannot happen** — crystals are never
clamped. The underlying defect (callers logging *requested* rather than *credited*) is real, but it
applies **only to wood / iron / food**. Rewrite §1's example onto a capped resource.

### The unblock

**Status → BLOCKED**, on exactly one thing: **reconcile §1 and §6 against the verified code above** —
re-state the behaviour spec for capped resources only, with a capped-resource example string, and
strike the crystal framing. Once §1 no longer asserts a crystal cap, the behaviour half is buildable.

⚠ The **regression half** stays unaffected — the source-structural seam decided by the lead 2026-08-24
pins honest REPORTING and is correct under every possible ruling.

---

## ⭐ OWNER CLARIFICATION 2026-08-24 - "the CAPPED THREE, whatever they're called"

The ruling's worked example said *"240 of 500 **stone** collected."* ⚠ **Stone does not exist yet** - it
arrives with WO-1163's rename, which is unshipped. Today the capped three are **wood / iron / food**.

⭐ **Owner: "yes the capped three whatever they're called."** So the rule is recorded **STRUCTURALLY, not
by name**, and it survives this rename and every future one:

> **A reward pays what fits and discloses the shortfall in words for any resource where
> `TownBankCapacity.IsCapped()` returns TRUE. Resources it returns FALSE for always pay in full.**

⛔ **Do NOT hardcode a resource-name list anywhere.** `TownBankCapacity.UncappableResources` (`:238-242`)
is the single authority; `IsCapped()` (`:290-296`) is the single read. ⚠ A hardcoded list is a fact
written twice — this repo's dominant failure mode — and it would go stale **the day WO-1163 lands**,
which is *this week*.

⭐ **Crystals and Coins stay uncapped and always pay in full**, verified five ways at source, with
`[no-crystal-cap]` (`TownBankCapRegression.cs:162`) **failing the build** if that changes.

⚠ **§1's premise is still FALSE and still owed a correction** — it claims crystals route to a clampable
kind and a player is credited 0 while the log reads `+500 crystals`. **That scenario cannot occur.** The
real defect (callers logging *requested* rather than *credited*) applies **only to the capped set**.
⛔ The regression slice is unaffected and remains handable.
