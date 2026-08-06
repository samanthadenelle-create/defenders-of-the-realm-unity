# WORK ORDER 911 - Unified Manage/Queues Screen (bar face re-pointed, tabbed channels, Finish Now, cancel/refund, 5-per-line cap)

**Status: READY TO IMPLEMENT - FULLY RULED. No open questions.**

> ## Q12 - RULED (owner, 2026-08-06). THE LAST ONE; NOTHING IS OPEN.
>
> **A collapsed xN Train card offers NO cancel affordance at all.** Her words: *"can not cancel on a
> collapsed card, must expand then select item to cancel and others automatically move up."*
>
> The required flow, in order:
> 1. The collapsed xN card shows **no Cancel control** - it cannot be the target of a cancel.
> 2. The player **expands** the card into its N individual jobs.
> 3. The player **selects the specific item** to cancel. Cancel is keyed to that one job.
> 4. The remaining items **automatically move up** to close the gap - no hole, no manual reorder.
>
> Refund follows Q1 (100% flat) for the one cancelled item ONLY.
>
> This is the same principle as Q11 (Finish Now = explicit item only): **never let a destructive or
> paid verb act on an ambiguous aggregate.** `CancelChannelJob` is keyed by id, not index
> (`BuildTimerService.cs:533-556`), so the expanded row already has the right handle - the engine verb
> needs no change. The collapse at `BuildTimerService.cs:690-693` is a PRESENTATION concern; expanding
> must reveal the real per-job ids rather than re-deriving them.
>
> **Everything in section 8 is now RULED (owner, 2026-08-06). Do not re-open Q1-Q13.**

**Minted:** 2026-08-05 (UI seat). Number taken from the `CLI_LANES_WO_NUMBERS.md` banner - the sole
numbering authority. This WO does not edit that file.
**Ruled:** 2026-08-06 (owner). 12 of 13 open questions closed; see section 8.
**Lane:** HUD/UI (presentation) + Core/Jobs (engine) + Economy/Monetization. Crosses lanes; see section 7.
**Audit basis:** read at source 2026-08-05. Every claim carries a file:line. Nothing is inferred.
Section 11 lists the three things this audit could NOT confirm and the one check that settles each,
plus (added 2026-08-06) the consequences of the rulings that this audit could not verify at source.

---

## 0. THE GOVERNING LENS - RTS systems, CoC shell (owner framing, 2026-08-06)

Per project canon this game is **"Warcraft/Starcraft meets Clash of Clans"**: **CoC decides the SHELL**
(the bar, the town, the tap grammar, the timers-as-pain economy) and **the RTS decides the SYSTEMS**.

Her framing for this screen, verbatim: **"Think Warcraft-style parallel production lines."** Three
parallel queues - Builder, Research, Troop - is the RTS half of that sentence, presented in a CoC shell.

**The single biggest fact for the estimate: the parallelism ALREADY EXISTS.**
`BuildTimerService.SlotCount(ChannelId)` is per-channel (`BuildTimerService.cs:159-165`), `BoughtSlots`
is per-channel state (`ObsidianQueueState.cs:33-36`), and completion is already channel-generic
(`CompleteChannelJob(ChannelId, string)`, `BuildTimerService.cs:460-476`). The engine runs independent
production lines today. **This is a UI job, not an engine one.** Anyone scoping this as "build parallel
queues" has mis-read the tree and will over-estimate it by a wide margin. The engine work that remains
is narrow and named: per-job cost storage (M2), a depth cap field (M1), a price on `BuySlot` (M11), and
generalizing three Builder-hardcoded wrappers (M5).

---

## 1. The owner's spec, VERBATIM

Message 1:

> "For the builders queue, it would be a smart idea to move at to its own dedicated button at the
> bottom where we can open up the queue and see the different types of queues, one for building, one
> for troops, and maybe one for repairs, or repairs can go in with building. Also, one for upgrades.
> Anything that's applicable should be in a single screen"

Message 2:

> "would be nice to offer speed ups they could use to finish a quest, also ablilty to see all the
> items in the queue and cancel the second thing and refund the amount and bump up the next item, but
> requires to be able to see the queue. max of five things in the queue, maybe upgradable later"

**Restated target:** one dedicated BOTTOM BAR button opening ONE screen that shows ALL queue channels
as tabs, with per-item visibility, cancel-with-refund that promotes the next item, speed-ups, and a
5-item cap that is upgradable later.

---

## 2. MONETIZATION SPINE (owner ruling - first-class scope, not an appendix)

### 2a. The ruling, in her framing

```
Faucet (buy)  : Crystal packs in Realm Store
Sink  (spend) : "Finish now" on EVERY real wait
Free path     : Wait, or an optional Ad when ads work
NOT a sink    : combat power, permanent damage buffs
```

> "Waiting is felt pain; crystals turn pain into optional spend without selling win. That's the
> bent-covenant convenience-only rule."

**Her DOs and DON'Ts - these are acceptance criteria, carried into section 9:**

| DO | DON'T |
|---|---|
| Price **from remaining time** | "1 crystal any job forever" (flat fee) |
| Finish on **ALL channels** | Builder only |
| **Always show Finish while a job runs**, plus a "get crystals" route when broke | Hide it when they cannot afford it |
| A **minimum price** so short jobs still cost something | A free instant on a 10-second job - "kills feel" |
| Later, second-priority sinks: **+builder slot**, **extra train queue** - still convenience, still crystals | Sell combat power or permanent buffs |

### 2b. HEADLINE CORRECTION: the curve is not unbuilt - it SHIPS, for one channel

The routing brief suspected `instantFinishCrystalsPerMinute` might have zero readers and that the
model was "DESIGNED AND UNBUILT". **The code says otherwise, and the difference changes the whole
shape of this work.** Her pricing model is implemented, wired to a spend, and reachable in game -
but only on the Builder channel.

The chain, read at source end to end:

1. `Assets/_Modules/Core/Catalog/BuildTimerConfig.cs:152-158` - `InstantFinishPrice(double remainingSeconds)`:
   ```
   if (instantFinishCrystalsPerMinute <= 0) return 0;   // paid skip disabled
   double minutes = Mathf.Max(0f, (float)remainingSeconds) / 60.0;
   int price = Mathf.CeilToInt((float)minutes * instantFinishCrystalsPerMinute);
   return Mathf.Max(price, instantFinishMinCrystals);
   ```
   **That IS her per-remaining-time curve WITH her minimum price.** Already written.
2. `Assets/_Modules/Village/Buildings/BuildTimerService.cs:368-373` calls it with live remaining seconds.
3. `BuildTimerService.cs:413-425` - `TryInstantFinish`: checks `state.Resources.Crystals < price` (`:420`),
   spends `svc.AddCrystals(-price)` (`:422`), completes the job (`:423`).
4. `Assets/_Modules/Village/BuildMode/ObsidianQueueHud.cs:272` reads the price, `:285-288` builds the
   crystal button, `:364` calls `TryInstantFinish`.
5. Reachable in game via `HudKitController.cs:785-787` -> `ObsidianQueueGate.RequestToggle` ->
   `ObsidianQueueHud.cs:110`.

**The five constants she cited, verbatim from `BuildTimerConfig.cs:77-94`, under a header that literally
reads "Premium instant-finish (convenience IAP, not power)":**

| Constant | Value | Declared | Read by | Verdict |
|---|---|---|---|---|
| `instantFinishCrystalsPerMinute` | 1 | `:87` | `BuildTimerConfig.cs:154,156` | **LIVE** |
| `instantFinishMinCrystals` | 5 | `:90` | `BuildTimerConfig.cs:157` | **LIVE** |
| `adSkipSeconds` | 15f * 60f = 900 | `:79` | `BuildTimerService.cs:405` | **LIVE** (but see B1) |
| `adSkipsPerDay` | 10 | `:82` | `BuildTimerService.cs:605` | **LIVE** |
| `freeBuildSlots` | 2 | `:94` | `BuildTimerService.cs:161` | **LIVE** |

**All five are read. None is dead config.** There is no authored `.asset` - `BuildTimerService.cs:898-902`
falls back to `CreateDefault()`, so the C# defaults above ARE the live shipped numbers.

**Therefore the gap is REACH, not construction:** channels, discoverability, the broke-case route, and
the faucet. Whoever builds this must not re-derive or re-invent the curve.

### 2c. The six wiring questions, answered

**1. Is the pricing curve read by any code?** Yes - see 2b. The headline is the opposite of the
suspicion: it is built and reachable, Builder-only.

**2. Is there an ad path?** A stub, no SDK.
`Assets/_Modules/Village/Monetization/RewardedAdManager.cs:96-100` - `ShowAdInternal` invokes the
reward callback immediately; `:36` is an 8-minute cooldown; no ad package exists in
`Packages/manifest.json`. Her "when ads work" is exactly the right framing. Today the green "Ad"
button is a **free -15 min button, 10x/day**. That is **B1** in section 5.

**3. Do crystals actually flow in? One ledger?** One ledger, confirmed - **but the faucet is CLOSED in
release builds.**
- Same wallet both directions: the sink reads `state.Resources.Crystals` and spends via
  `GameStateService.AddCrystals(-price)` (`BuildTimerService.cs:420,422`); the pack grant routes
  Food/Crystals through `AddFood`/`AddCrystals` into the same persisted GameState ledger
  (`Assets/_Modules/Wallet/PackStoreVM.cs:103`). Corroborated by `docs/MASTER_CATALOG/economy-meta.md:25`.
- **THE BREAK:** `Assets/_Modules/Core/FeatureFlags.cs:555` -
  `RealmStorePurchase => Get("realmstorepurchase", defaultOn: IsDevBuild)`. In a RELEASE build the flag
  is OFF, and `Assets/_Modules/Wallet/PackStore.cs:374-378` renders **"Coming soon"** in place of the
  Buy CTA. **In the APK testers actually receive, a player cannot buy crystals at all.** Her
  faucet -> sink loop is non-functional in the shipped build. This is **B8** and it is the highest-severity
  monetization finding in this audit.

**4. Per-channel reach.** Violated today: Builder only.
- `InstantFinishPrice` (`BuildTimerService.cs:370`), `CanWatchAdToSkip` (`:382`) and `ApplySkipSeconds`
  (`:430`) all hard-resolve the Builder channel.
- The UI suppresses the row entirely for other channels: `ObsidianQueueHud.cs:270-274` - "price>0 gates
  the Instant button so Train/Research never show a false Instant CTA", early-returning at `:274`.
- **GOOD NEWS, and it makes this cheap:** the completion machinery is ALREADY channel-generic -
  `CompleteChannelJob(ChannelId, string)` at `BuildTimerService.cs:460-476`. Only the price /
  eligibility / spend WRAPPERS are Builder-hardcoded. "Finish on all channels" is a generalization of
  three method bodies, not new machinery.

**5. The broke case.** Half-compliant already, and the deeplink exists.
- The crystal button is gated on `price > 0` (`ObsidianQueueHud.cs:282`), **not** on affordability - so
  it already stays VISIBLE when the player is broke, which matches her rule. But the tap silently
  no-ops: `TryInstantFinish` returns false at `BuildTimerService.cs:420` with no feedback and no route.
- **A store deeplink EXISTS and is callable from any assembly:**
  `PanelRouter.Register(PanelId.RealmStore, OpenRealmStore)` at
  `Assets/_Modules/Wallet/PackStoreBootstrap.cs:47`, opener at `:67`. So `PanelRouter.Open(PanelId.RealmStore)`
  is the ready-made "get crystals" route - **though it lands on the release-gated store of B8.**
- **Colourblind law (owner is red/green colourblind):** an unaffordable state must be encoded in TEXT,
  never by a red face alone. The existing queue surfaces already hold this line
  (`QueueRailView.cs:76-78`) and the new screen must too.
- **A queued job shows NO action row at all:** `ObsidianQueueHud.cs:268` early-returns on
  `job.StartMs <= 0`. On a 5-deep queue with 2 slots, 3 items would offer nothing. See Q5.

**6. Slot purchase - is her "later sink" half-built?** Yes, and better than expected.
- `BoughtSlots` is modelled, per-channel, and persisted: `ObsidianQueueState.cs:33-36`, validated
  `SaveSchema.cs:833`, summed into `SlotCount` at `BuildTimerService.cs:159-165`.
- `BuySlot(ChannelId)` exists at `BuildTimerService.cs:168-178` and **charges nothing** - the
  "premium currency handled by caller" contract has a stub caller (`ObsidianQueueHud.cs:355-356`).
  That is **B3**.
- **Because `BoughtSlots` lives on `ChannelState`, not globally, her "extra train queue" is the SAME
  verb with a different argument:** `BuySlot(ChannelId.Train)`. Both of her named second-priority
  sinks are one price away from working.

### 2d. CAP vs CONCURRENCY - do not conflate these two numbers

This is the sharpest risk in the whole WO, because getting it wrong would directly undercut her
monetization ruling.

| Her "max of five things in the queue" | `freeBuildSlots = 2` |
|---|---|
| Queue **LENGTH** / depth - how many items may be lined up | **CONCURRENCY** - how many run at once |
| **Does not exist in the codebase at all** | Shipped and live |
| No proof - there is nothing to cite | `BuildTimerConfig.cs:92-94`, whose header literally reads "Build slots (concurrency / scarcity)" |

**The model has no notion of queue length distinct from active slots.** `ChannelState` holds
`ActiveJobs` (bounded by `SlotCount`) and `PendingQueue` (**UNBOUNDED**) - `ObsidianQueueState.cs:39,42`;
`ObsidianQueueEngine.cs:53-55` appends to pending unconditionally with no length check. A grep for
MaxQueue/QueueCap/maxPending across `Assets/` hits only analytics (`EventTracker.cs:63`) and nav
(`NavPathCoordinator.cs:49`).

**Therefore "5" is a NEW axis.** And critically:

> **Do NOT implement the cap of 5 by raising `freeBuildSlots`.** That would give the player five
> simultaneous builders, collapse the CoC scarcity the entire timer economy rests on, and REMOVE the
> waiting pain that her crystal sink exists to monetize. Raising concurrency destroys the sink.
> `freeBuildSlots` is also oracle-pinned at 2 (`BuildEconomyRegression.cs:1183-1184`).

### 2e. "Finish now on EVERY real wait" - scope boundary needed

Her sink rule says every real wait, but this WO's audit covers only the three Obsidian queue channels.
Other real waits exist in the game (harvest/offline-harvest timers, raid cooldowns, the 8-minute ad
cooldown at `RewardedAdManager.cs:36`, daily quest rolls). Whether "every real wait" means this screen
only, or a game-wide Finish Now pass, was **Q11** - it materially changes the size of the work.

> **RULED (Q11, 2026-08-06): EXPLICIT ITEM ONLY.** Finish Now applies to the item the player explicitly
> picks in this screen. **NOT a game-wide pass.** The harvest timers, raid cooldowns, ad cooldown and
> daily quest rolls listed above are **out of scope** - this boundary is closed on the narrow side, and
> it is the second-largest thing that shrinks the estimate (after section 0).

---

## 3. THE 2026-08-01 REVERSAL - read before touching the bar

> **RULED 2026-08-06 (Q10 + Q13, merged) - read this before section 3b.** The new entry is **NOT an
> 8th face**. The existing **Upgrade** face is **REPLACED** by the unified Manage/Queues screen, and
> **Map moves into Bag as a tab**. Quest stays. Net bar: **7 -> 6 faces**. `ActionBarButtonId.Upgrade = 6`
> is **re-pointed, not added** (`HudActionBarModel.cs:55-64`). See section 8 Q10+Q13 for the full ruling,
> including the flagged count consequence the audit could not verify.

Her ask REVERSES a standing owner ruling. This must be surfaced, not silently absorbed.

**The standing ruling (CLAUDE.md section 7, owner 2026-08-01):** the bar Queues BUTTON is RETIRED;
the right-column Builders chip (QueueStatus band) is the one Queues entry.

**It is enforced by live oracles.** A dedicated bottom Queues button re-introduces exactly the widget
those oracles ban. Each must change in the SAME commit as the reversal:

| File:line | Current assertion | Must become |
|---|---|---|
| `Assets/Editor/Regression/ObsidianQueueRegression.cs:283-284` | fails if `HudKitController.cs` contains `Register("workQueueButton"` | inverted - REQUIRE the registration (a missing bar button becomes the regression) |
| `Assets/Editor/Regression/ObsidianQueueRegression.cs:302` | fails if `hud-areas.json` contains `workQueueButton` | inverted - REQUIRE the row in BOTH canonical copies |
| `Assets/Editor/Regression/ObsidianQueueRegression.cs:288` | logs "bar button retired 2026-08-01" on the pass path | reworded - leaving this makes the gate log assert a fact that is no longer true |

`ObsidianQueueRegression.cs:278-279` (HudKitController must call `ObsidianQueueGate.RequestToggle`) and
`:303` (`queueStatusChip` row required) **stay as they are - RULED: the chip is NOT retired** (Q10). It
survives as a **status glance only** (count/timer), with the bar face as the single door. The `:303` row
assertion therefore still passes unchanged.

**Canon that must move in the same commit** (CLAUDE.md section 15 - a state change with no canon update
is an incomplete change): `CLAUDE.md` section 7 (the retirement sentence AND the face count), plus any
`CANON_GROUND_TRUTH_*.md` / `docs/HANDOVER.md` line restating the 08-01 retirement.

**Do not let an implementing agent "fix the failing test" by deleting it.** The reversal is a deliberate
owner decision; the oracle must keep guarding the NEW state, inverted, not vanish.

### 3b. The bar has no free slot, and the count is hardcoded

| Fact | Proof |
|---|---|
| The `calm(town)` bar has **SEVEN** faces: Build, Talk, Bag, Raids, Map, Quests, Upgrade | `Assets/Resources/Data/Canonical/hud-areas.json:48-58`; built at `Assets/_Modules/HUD/Kit/HudKitController.cs:449-540` |
| `ButtonCount = 7` is a const; the id enum's ORDER is the bar order | `Assets/_Modules/Core/HudModel/HudActionBarModel.cs:74`, enum `:55-64` |
| Slot width uses **literal** `6f`/`7f`, NOT derived from `ButtonCount` | `HudKitController.cs:106` - `BarSlotW = (1f - BarGap * 6f) / 7f`. An 8th face keeps 1/7 width and the group overflows its zone (`ApplyActionBar`, `:1342-1343`) |
| The 7-face MAX set and its exact ORDER are oracle-asserted | `Assets/Editor/Regression/HudActionBarRegression.cs:125-128`, exact-count-and-order helper `ExpectSet` at `:151-161`; further exact-set asserts at `:82-83`, `:86-88`, `:132`, `:134-135`; EditMode mirror `Assets/Tests/EditMode/HudActionBarModelTests.cs:171,184` |

So "a dedicated button at the bottom" is not a free slot - it would be an 8th face requiring the enum, the
const, the two hardcoded literals, a `hud-areas.json` row in BOTH canonical copies (asserted
byte-identical at `ObsidianQueueRegression.cs:305-307`), and six oracle updates. **Or** an existing face
gives way. That was Q10.

**RULED (2026-08-06): an existing face gives way - twice.** The **Upgrade** face becomes the
Manage/Queues door (`ActionBarButtonId.Upgrade = 6` re-pointed in place), and **Map** leaves the bar for
a tab inside **Bag**. **No enum VALUE is added.** The 8th-face problem - the enum extension, the
`ButtonCount` increase, and the geometry overflow at `HudKitController.cs:106` - **is dissolved, not
solved.** That is the ruling's stated benefit and it removes the largest cost item this audit found.

> **FLAGGED - a consequence this audit CANNOT verify, and the check that settles it.** The ruling states
> "no `ButtonCount` change is needed", which holds exactly for the Upgrade->Manage **re-point**. But
> moving **Map** off the bar is a **REMOVAL**, and the bar count is a hardcoded const (`ButtonCount = 7`,
> `HudActionBarModel.cs:74`) with literal slot geometry (`BarSlotW = (1f - BarGap * 6f) / 7f`,
> `HudKitController.cs:106`) and exact-set/order oracles (`HudActionBarRegression.cs:125-128`, `:82-83`,
> `:86-88`, `:132`, `:134-135`; `HudActionBarModelTests.cs:171,184`). A 6-face bar built on a 7-slot
> width leaves a trailing gap. **This WO does not assert what the code does here - it names the check:**
> read `HudActionBarModel.cs:74` and `HudKitController.cs:106` together and confirm whether the bar
> renders `ButtonCount` faces or the model's actual face list; then either drop the const to 6 and derive
> the two literals, or state why 7 still holds. **Do NOT let an implementer silently leave a dead slot,
> and do NOT let them delete an exact-set oracle to make a 6-face bar pass** - update the expected set.
> (The `ActionBarButtonId.Map` enum value's fate - retired vs. left dormant - is part of the same check.)

**STALE CANON FOUND:** CLAUDE.md section 7 states "calm(town) bar = 6 faces". It is **7**
(`hud-areas.json:48-58`, `HudActionBarModel.cs:74`) - the `upgradeButton` face landed after that line
was written. Correct it whether or not this WO proceeds.

### 3c. Today's entry point is an undiscoverable double-tap

`HudKitController.cs:781-791` - the FIRST tap on the Builders chip expands an inline card rail (`:790`);
tapping AGAIN collapses it and calls `ObsidianQueueGate.RequestToggle()` (`:785-787`). That second tap is
the ONLY live player path: `ObsidianQueueHud.OpenWorkQueue()` (`ObsidianQueueHud.cs:136`) has **zero
callers** outside two regression reflection probes (`ObsidianQueueRegression.cs:180`, `:263`).

This is the owner's complaint stated in mechanism terms. It supports the reversal.

---

## 4. AUDIT TABLE - every spec item, verified at source

Legend: **EXISTS** = present and works. **PARTIAL** = present but scoped/unwired/unreachable.
**ABSENT** = not built. "UI reaches it?" is tracked separately from the mechanism.

| # | Spec item | Mechanism | UI reaches it? | Proof (file:line) | Gap |
|---|---|---|---|---|---|
| 1 | See ALL items in a channel | **EXISTS** (data) | **PARTIAL** | Full ordered lists `ActiveJobs`/`PendingQueue` at `ObsidianQueueState.cs:39,42`; uncapped accessors `BuildTimerService.cs:188-199` | Publish caps at 24 (`BuildTimerService.cs:696` `MaxPublishedCards`, applied `:707,:716`); the rail shows only as many cards as fit at `MinTouchPx` then a dead "+N MORE" tail (`QueueRailView.cs:371-386`, tail `:511-519`) and is **deliberately non-scrollable** (`QueueRailView.cs:509-511`). Item 5 of 5 may be unviewable today. |
| 2 | CANCEL an arbitrary item | **EXISTS** | **ABSENT** | `CancelChannelJob(ChannelId, string)` `BuildTimerService.cs:533-556` - handles ACTIVE (`:539-546`) and PENDING (`:547-554`); Builder wrapper `:530` | Keyed by `structureId`, **not by index**. Only caller in the tree is a sell (`BuildModeController.cs:2180`). No queue UI offers Cancel - rows offer only Instant and Ad (`ObsidianQueueHud.cs:266-299`). **Hazard:** identical PENDING troop trains collapse to ONE xN card at publish (`BuildTimerService.cs:690-693`), so on Train a card is not 1:1 with a job - "cancel the second thing" is ambiguous there (Q12). |
| 3 | REFUND on cancel | **ABSENT** | **ABSENT** | `BuildJobData` carries StructureId/JobType/Kind/Channel/StartMs/DurationMs/TargetTier and **nothing else** - whole struct `Assets/_Modules/Core/State/BuildJobData.cs:48-119`. Contract is explicit: "caller owns any refund" `BuildTimerService.cs:527` | A queued job does not know what it cost, so a refund is **not computable today**. Needs new persisted fields + a schema bump (current `CurrentVersion = 36`, `SaveSchema.cs:11`) + a migration defaulting old jobs to 0. **DECIDED (Q1): the fraction is 100%, always** - the conflicting precedents in section 6 D1 no longer need weighing. |
| 4 | PROMOTE the next item after cancel | **EXISTS** and is sound | **ABSENT** | Cancelling an ACTIVE job frees the slot and pulls the pending head: `BuildTimerService.cs:542` -> `ObsidianQueueEngine.PullIntoFreeSlots` (`ObsidianQueueEngine.cs:122-134`, `StartMs = now` at `:131`). Explicit reorder ALSO exists: `ReorderPending(ChannelId, targetId, index)` `BuildTimerService.cs:562-576` | **NOT the fragile part the brief feared.** Pending jobs carry `StartMs = 0` (`ObsidianQueueEngine.cs:53`) and can never complete (engine guards `StartMs > 0` at `:86`), so mid-list removal needs no re-timing - `List.RemoveAt` at `:550` is correct and the rest shift up. Both paths `Persist()` (`:543`,`:551`) and republish (`:639-643`). **The gap is UI only.** |
| 5 | SPEED-UPS ("Finish now") | **EXISTS**, Builder only | **PARTIAL** | Full chain in section 2b. Config `BuildTimerConfig.cs:79,82,87,90`; price `:152-158`; spend `BuildTimerService.cs:413-425`; buttons `ObsidianQueueHud.cs:285-288`, `:294-297` | **Builder channel ONLY** (`:370`,`:382`,`:430`) - violates her "ALL channels" rule. **A QUEUED job gets no row at all** (`ObsidianQueueHud.cs:268`) and cannot be sped up (`:371`,`:383`,`:436`). **DECIDED (Q5): a queued job MUST be Finish-Now-able** - so this row needs BOTH a row and a price for not-yet-started work; pending jobs carry `StartMs = 0` (`ObsidianQueueEngine.cs:53`), which is exactly what the current early-return keys on. Broke case shows the button but silently no-ops with no store route (section 2c #5). |
| 6 | CAP of 5, upgradable | **ABSENT** | n/a | `ObsidianQueueEngine.Enqueue` appends **unconditionally** - `ObsidianQueueEngine.cs:53-55`. No length check anywhere. `BuildTimerService` rejects only a DUPLICATE id (`:251`,`:314`), never a full queue | **No queue-depth concept exists.** What exists is a SLOT (concurrency) cap - see section 2d, which must be read before anyone touches this row. Depth and concurrency are different axes and conflating them breaks the monetization ruling. **DECIDED (Q4): 5 TOTAL PER LINE** - per channel/queue, not global. **DECIDED (Q6): the upgrade lever is ECHO-gated crystal purchase.** |
| 7 | REPAIRS | enum only - **ABSENT** as a job | **ABSENT** (as a job) | `JobKind.Repair = 2` `Assets/_Modules/Core/Jobs/JobKind.cs:39`, routed to Builder by the `default:` arm `:91-92`, oracle-asserted `ObsidianQueueRegression.cs:104` | **Nothing ever enqueues it.** All other references are display strings (`ObsidianQueueHud.cs:450-451,467`; `BuildTimerService.cs:785`). **No `IJobEffect` handler exists** - only three are registered, all barracks (`BarracksService.cs:393-395`), and `IJobEffect.cs:66-71` silently no-ops an unregistered kind, so a Repair job would complete and do nothing. A DIFFERENT repair system ships - see 4b. **DECIDED (Q2): repair is NOT a timed queued job.** The "convert repair to a timed job" branch is **DEAD** - keep the existing instant crystal spend-and-heal, surfaced cleanly inside the manage screen IF it fits. **Follow-up, NOT this WO:** `JobKind.Repair = 2` is unenqueued and unhandled - it should either stay dormant with a comment saying so, or be removed. Note it is oracle-asserted (`ObsidianQueueRegression.cs:104`), so removal is not free. |
| 8 | UPGRADES as a distinct tab | **EXISTS** as a view filter | **ABSENT** | All upgrade kinds already ride Builder (`JobKind.cs:91-92` covers Upgrade/TowerUpgrade/WallUpgrade/BarracksUpgrade). Every published entry carries a `Verb` (`ObsidianQueueGate.cs:61`, resolved at `BuildTimerService.cs:785`); the raw job carries `Kind` (`BuildJobData.cs:68`) | A tab filtering Builder entries by Kind/Verb is a **pure VIEW change - zero engine, zero balance impact**. **DECIDED (Q3): VIEW/TAB. The "own channel = balance change" branch is DEAD** - upgrades do NOT get an independent worker pool, so the progression-speed increase never happens. Keep the existing upgrade model and viewer; present it as TABS: **Buildings, Walls (TBD), Research (tiered items), Troops**. Her words: *"feels like the current upgrade experience, just organized better."* Presentation over the existing model. |
| 9 | BOTTOM BUTTON | **ABSENT** (retired) | n/a | Section 3b in full | Bar is FULL at 7 with hardcoded 6f/7f geometry (`HudKitController.cs:106`) and six exact-set oracles. No 8th slot exists. **DECIDED (Q10+Q13): no 8th face is built.** The **Upgrade** face is re-pointed to the unified Manage/Queues screen and **Map moves into Bag as a tab** - net 7 -> 6. The chip survives as a status glance only. See the FLAGGED count check in section 3b. |

### 4b. Repairs - what actually ships today

There IS a working repair feature. It is **instant spend-and-heal and lives entirely outside the queue**,
so "repairs go in with building" is not a fold of two similar systems - it is a decision about whether
repair becomes timed (and therefore whether it becomes a Finish Now sink).

- `Assets/_Modules/Village/Walls/WallRepairController.cs` - `ConfirmRepair()` `:912`, `RepairAll()` `:745-782`,
  `RepairAllCost()` `:722`, `CostFor` `:531`, destroyed-fraction `:516`.
- Reachable UI, three surfaces, all instant: hub REPAIR ALL button
  `Assets/_Modules/Village/Walls/HubRepairAffordance.cs:57,260`; end-of-battle CTA
  `Assets/_Modules/Village/UI/EndState/EndStateVM.cs:415-427`; post-wave nudge
  `Assets/_Modules/Village/Waves/WaveFeedbackDirector.cs:139`.
- **Structure damage does not persist.** `SaveSchema.cs:265` declares a `buildingDamage` dictionary,
  plumbed through `GameStateService.cs:451,542,962`, but **no gameplay code ever writes it**. Damage
  exists only on live scene objects. See section 11 item 1.

Note the monetization angle: `packs.json` already sells `instant-repair` tokens (see B2), which presumes
repair is a wait worth skipping. Today it is instant and free-of-time, so there is nothing to skip.

> **RULED (Q2, 2026-08-06): repair stays INSTANT. Do NOT convert it into a queued job.** She prefers the
> existing instant heal system, surfaced cleanly inside the manage screen if it fits there. Consequences:
> repair never becomes a Finish Now sink; the `instant-repair` token in `packs.json` remains a thing with
> nothing to skip (that is B2's problem, not this WO's); and `WallRepairController` is **read/called**,
> never restructured. Fold-vs-convert (D2) is closed on the FOLD side.

---

## 5. BUGS - defects in shipped code, true regardless of this WO

Not part of the queue-screen scope, but four of them sit directly on surfaces this screen exposes.

| ID | Severity | Defect | Proof |
|---|---|---|---|
| **B8** | **CRITICAL (blocks the ruling)** | **The crystal FAUCET is closed in release builds.** `RealmStorePurchase` defaults to `IsDevBuild`, so in a shipped APK the store renders "Coming soon" instead of a Buy CTA. The player cannot buy crystals, so the faucet -> sink loop she just ruled does not function in the build testers receive. | `Assets/_Modules/Core/FeatureFlags.cs:555`; `Assets/_Modules/Wallet/PackStore.cs:374-378` |
| **B2** | HIGH (real money) | Paid packs promise `instant-build`/`instant-repair` tokens and **grant nothing** - the grant path documents itself as a no-op pending a token tray. Founder's Vow ships 25/25 and delivers zero. | Authored `Assets/Resources/Data/Canonical/packs.json:31,47-48,64-66,82-85,102-105,243-245`; typed `Assets/_Modules/Wallet/PackCatalog.cs:61,80,214-223`; the no-op `Assets/_Modules/Wallet/PackStoreVM.cs:126-128` |
| **B1** | HIGH | The "Ad" speed-up **grants with no ad** - the ad manager's `ShowAdInternal` invokes the reward callback immediately and no ad SDK exists in `Packages/manifest.json`. Live effect: a free -15 min button, 10x/day, gated only by an 8-minute cooldown. | `Assets/_Modules/Village/Monetization/RewardedAdManager.cs:96-100`, `:36`; consumed at `BuildTimerService.cs:405` |
| **B3** | MEDIUM | `BuySlot(ChannelId)` increments `BoughtSlots` and **charges nothing** - unlimited free parallel workers, which also erodes the waiting pain the sink monetizes. | `BuildTimerService.cs:168-178`; stub caller `ObsidianQueueHud.cs:355-356` |
| **B4** | MEDIUM (UX) | The only queue entry point is an undiscoverable double-tap on a status chip. | `HudKitController.cs:781-791`; `ObsidianQueueHud.cs:136` has no callers |
| **B5** | LOW (latent) | Cancel-pending mutates `ch.PendingQueue` directly with no engine call (`:550`) while cancel-active routes through `PullIntoFreeSlots` (`:542`). Correct today because a pending job holds no slot; breaks silently if that ever changes. | `BuildTimerService.cs:539-555` |
| **B6** | LOW (canon) | CLAUDE.md section 7 says the calm(town) bar is 6 faces. It is 7. | `hud-areas.json:48-58`; `HudActionBarModel.cs:74` |
| **B7** | LOW | `ad-placements.json` authors a full placement/reward table including `reward.build.skip1` and `place.build.skip`, with **zero C# consumers**. | `Assets/Resources/Data/Canonical/ad-placements.json:51-56,96-105` |

**Dependency callouts:**
- **B8 blocks the monetization ruling end to end.** A "get crystals" route (her broke-case rule) that
  deeplinks to a store showing "Coming soon" is worse than no route.
  **RULED (Q9): FIX IT, in this WO.** Open `RealmStorePurchase` in release builds and remove the
  "Coming soon" copy (`FeatureFlags.cs:555`, `PackStore.cs:374-378`). B8 moves from "out of scope" to
  **IN SCOPE** - see section 7. It is the highest-severity finding in this audit and the crystal faucet
  is closed in the shipped APK until it is fixed.
- **B2 blocks any ruling that makes the speed-up an ITEM** - the token she would spend is the one packs
  already fail to grant. Her ruling says crystals, so this is currently a non-blocker.
- **B1 undercuts the sink** while it grants free time with no ad.
  **RULED (Q8): SHIP THE AD BUTTON ON.** Wiring a real ad behind it is **WO-912's scope, not this WO's**.
  That creates a hard **ORDERING DEPENDENCY**, carried into section 9 as an acceptance criterion:
  **shipping the button BEFORE the ad SDK lands gives away free time** (`RewardedAdManager.cs:96-100`
  invokes the reward callback immediately; no ad package exists in `Packages/manifest.json`;
  `adSkipSeconds` = 900 consumed at `BuildTimerService.cs:405`, `adSkipsPerDay` = 10 at `:605`).

---

## 6. MISSING FEATURES vs BALANCE DECISIONS

### 6a. MISSING FEATURES (build work; no judgement call once ruled)

The BUILD column is added 2026-08-06: what the rulings turned each row into.

| ID | Feature | Why it is missing | BUILD (post-ruling) |
|---|---|---|---|
| M1 | Queue-DEPTH cap of any value | No depth concept exists (`ObsidianQueueEngine.cs:53-55`). Distinct from concurrency - section 2d | **BUILD: 5 per line**, as a `BuildTimerConfig` data field |
| M2 | Per-job COST storage | Precondition for any refund. `BuildJobData.cs:48-119` has no cost fields; needs schema v37 + migration (old jobs -> 0) | **BUILD.** Refund is 100% of paid, so the stored cost must be exact at every charge site (section 11 item 3). Coordinate the bump with Q7/WO-912 |
| M3 | Cancel UI | Engine verb exists (`BuildTimerService.cs:533`), nothing calls it | **BUILD** - collapsed-Train cards expose NO cancel; expand-then-pick only (Q12, ruled) |
| M4 | Reorder / "bump up" UI | `ReorderPending` exists (`:562-576`), nothing calls it | **BUILD** |
| M5 | Finish Now on Train and Research | Builder-only wrappers (`:370`,`:382`,`:430`); completion is already generic (`:460-476`) | **BUILD** - generalize three wrappers; no new machinery |
| M6 | Finish Now on a QUEUED job | No row is built (`ObsidianQueueHud.cs:268`) and the API rejects it (`:371`,`:383`,`:436`) | **BUILD (Q5: YES)** - needs a row AND a price for `StartMs = 0` work |
| M7 | Broke-case "get crystals" route from the Finish button | Deeplink exists (`PackStoreBootstrap.cs:47,67`), nothing calls it from the queue; and it lands on B8 | **BUILD** - and **B8 is fixed here** (Q9), so the route no longer dead-ends |
| M8 | Repair as a queued job | No enqueue site, no `IJobEffect` handler (`IJobEffect.cs:66-71`) | **DO NOT BUILD (Q2).** Repair stays instant. Row is dead |
| M9 | The dedicated bottom button + the tabbed screen | Retired 08-01; section 3 | **BUILD as a RE-POINT of the `Upgrade` face** (Q10+Q13) - no 8th face |
| M10 | A browsable list host for 5+ items | The rail tails to a dead "+N MORE" and is deliberately non-scrollable (`QueueRailView.cs:509-519`) | **BUILD** - the cap is 5 per line, so the host must show 5 |
| M11 | A PRICE on `BuySlot` (her "+builder slot" / "extra train queue" sinks) | Mechanism and per-channel state ship; only the charge is missing (B3) | **BUILD (Q6/Q7)** - Echo-gated, crystal-priced. The TEMPORARY ad-unlock variant is schema work shared with WO-912 |

### 6b. BALANCE DECISIONS - ALL SIX RULED 2026-08-06

Kept in full: the stakes column is the reasoning the ruling was made against, and the citations are still
the proof an implementer needs. **The "What is at stake" text describes the choice AS IT WAS - it is not a
live option list. Read the RULED column as binding.**

| ID | Decision | What was at stake | RULED (2026-08-06) |
|---|---|---|---|
| D1 | Refund fraction | Three **conflicting precedents** in-tree: sell a structure = **50%** (`BuildEconomyRegression.cs:111-112,509`); cancel a placement = **100%** (`BuildMenuRealEconomyRegression.cs:578-587`); and a prior owner ruling of **100% of paid** in `WorkOrders/WORK_ORDER_799_queue_cancel_refund_engine.md:12-13`, against a WC3 reference of 75/100/100/75 at `:11`. Also: refunding a **partially elapsed ACTIVE** job at 100% is a free time-machine (start, cancel, restart) - and it lets a player dodge the Finish Now sink entirely | **100%, regardless of elapsed time.** Consistent with her WO-799 ruling. The Finish-Now-dodge interaction is **ACCEPTED, not unresolved** - see Q1 |
| D2 | Repairs: fold or convert | Fold = surface the existing INSTANT repair inside the screen (cheap, no pacing change, but nothing to sell). Convert = repair becomes a timed Builder job that consumes slots, stops being instant, and becomes a Finish Now sink (which is what `packs.json` already presumes) | **FOLD.** Repair stays the existing instant crystal spend. **Convert branch is DEAD** |
| D3 | Upgrades: filter or channel | Filter = free, zero balance impact. Channel = a new independent worker pool, a straight progression-speed increase that reduces waiting and therefore the sink | **FILTER / VIEW - tabs over the existing upgrade model.** **Channel branch is DEAD**, so no balance change |
| D4 | Cap grain and inclusivity | Per-channel (matches the existing grain - `BoughtSlots` is on `ChannelState`, `ObsidianQueueState.cs:36`) or global. AND: does 5 include ACTIVE jobs (2 building + 3 waiting) or is it 5 WAITING on top? Materially different games | **5 TOTAL PER LINE** (per channel/queue), **not global** - matches the existing `ChannelState` grain |
| D5 | The "upgradable later" lever for DEPTH | `SlotCount`'s own doc already names the intended dial for SLOTS: "milestone unlocks - +1 slot at account L10/L20" (`BuildTimerService.cs:154-157`). Whether DEPTH uses the same lever, a crystal purchase, or a building level is unruled | **Tied to ECHOES.** Each Echo above 2 unlocks the OPTION to purchase one extra queue slot with crystals - a **two-step gate**: Echo count unlocks the RIGHT to buy, crystals complete it. Not the account-level milestone dial |
| D6 | Ad path's future | Her ruling keeps ads as the free path "when ads work". Until an SDK lands, B1 means the button is free time. Ship it, hide it, or gate it behind a real SDK? | **SHIP IT ON.** Real ad wiring = **WO-912**. Ordering dependency is an acceptance criterion (section 9) |

---

## 7. SCOPE BOUNDARY

**In scope:** the unified Manage/Queues screen, its tabs (Buildings / Walls TBD / Research / Troops), its
list host, the re-pointed bar face, Map-into-Bag, the 08-01 reversal and its oracle inversions, Finish
Now generalized to all channels INCLUDING queued jobs, with the broke-case route, the cancel/refund
(100%)/reorder UI, the per-line depth cap of 5, the Echo-gated slot purchase, **the B8 store-gate fix
(Q9)**, and the canon updates.

**Explicitly OUT of scope:** B1's underlying defect (the ad button SHIPS ON per Q8; **real ad wiring is
WO-912**), B2, B3, B7 (separate defects - file individually). Any change to `freeBuildSlots`
(oracle-pinned at 2, `BuildEconomyRegression.cs:1183-1184`). The WC3 percentage refund table (parked per
`WORK_ORDER_799:60`; D1 ruled 100% flat, so the table stays parked). A game-wide Finish Now pass (Q11
ruled: explicit item only). Converting repair into a timed job (Q2 ruled out). Removing or re-homing
`JobKind.Repair` (small follow-up, not this WO).

**CHANGED BY THE RULINGS (2026-08-06):** B8 moved OUT of the out-of-scope list and INTO scope per Q9.

**MERGED IN - WO-905 (Q13).** This screen **IS** WO-905's "Manage" screen. Do not build two.
`WorkOrders/WORK_ORDER_905_manage_screen_upgrade_browser.md` (Status: SPEC, not implemented) already
specs one screen carrying rails for Builders, Training and Research (`:81`), reachable from the bar face
named "Upgrade" - which is exactly the face this WO re-points. **WO-905 is absorbed by WO-911.** Whoever
implements should read WO-905 for its rail/browser detail and mark it superseded rather than build it.

**CROSS-REFERENCE - WO-912 (ads + rolling window).** Two seams bind these WOs and they must be **designed
together, not twice**: (a) the real ad SDK behind the Q8 button, and (b) the **temporary ad-unlocked queue
slot** from Q7, whose expiry state has no home today. See the Q7 flag in section 8.

**Prior art that must NOT be greenfielded:**

- `WorkOrders/WORK_ORDER_799_queue_cancel_refund_engine.md` - **Status: READY TO IMPLEMENT, never
  implemented** (no `.RESULT.md` on disk; `BuildJobData` still has no cost fields). It already specs the
  cancel verb, cost-on-job plumbing, item images and per-row Cancel buttons. **WO-911 should supersede or
  absorb it, not duplicate it.** Its signature drifted: WO-799:27 specs `CancelJob(ChannelId, string)`;
  the tree shipped `CancelChannelJob(ChannelId, string)` (`BuildTimerService.cs:533`).
- `WorkOrders/WORK_ORDER_905_manage_screen_upgrade_browser.md` - **Status: SPEC, not implemented.** It
  specs a "Manage" screen carrying rails for Builders, Training and Research (`:81`) - a second screen
  substantially overlapping this one, reachable from the bar face already named "Upgrade". **Two screens
  doing the same job was the biggest scope risk here. RULED (Q13): they are ONE screen. MERGED - build
  once.** See the merge note above.
- `QueueRailView` is already a reusable component with a documented public contract
  (`QueueRailView.cs:6-23`) and an oracle asserting the host-agnostic signature
  (`ObsidianQueueRegression.cs:373-376`). Reuse it; do not fork it.
- `BuildTimerConfig.InstantFinishPrice` (`:152-158`) IS her pricing curve. Do not write a second one.

---

## 8. THE RULINGS - ALL 13 CLOSED (owner, 2026-08-06)

Every question below carries the evidence it was ruled against; the ruling is attached to it. **Nothing
here is re-openable by an implementer.** Q12 was the last open item and was ruled 2026-08-06; there are
now no outstanding decisions in this work order.

Her monetization ruling ANSWERS several questions this audit would otherwise have had to ask, and they
are recorded as **RULED** below so no one re-opens them.

**RULED by the monetization spine (no action needed):**
- ~~What does a speed-up cost?~~ **Crystals, priced from remaining time, with a minimum.** Already
  implemented at `BuildTimerConfig.cs:152-158`.
- ~~Which channels get Finish Now?~~ **ALL of them.**
- ~~Hide the button when broke?~~ **No - always show it while a job runs, plus a route to buy.**
- ~~Is a speed-up an item?~~ **No - crystals.** (Which keeps B2 off the critical path.)

**RULED 2026-08-06 - the twelve:**

**Q1 - Refund fraction.** *(Evidence:)* 100% of paid (your WO-799 ruling), 50% (matching the existing
sell rule), or the WC3 split? And does an ACTIVE job that is 90% elapsed refund the same as an untouched
pending one? Note the monetization interaction: a 100% refund on a nearly-done job is a free alternative
to paying crystals to finish it. *(D1)*

> **RULED: 100%.** Full refund of resources **regardless of how much time has elapsed** - an ACTIVE job
> at 90% refunds exactly the same as an untouched pending one. Matches her earlier WO-799 ruling
> (`WORK_ORDER_799:12-13`).
>
> **The Finish-Now interaction is ACCEPTED, not unresolved.** Yes, a 100% refund on a nearly-done job is
> a free alternative to paying crystals. She has taken that trade knowingly: **the player still loses the
> elapsed TIME, and the time is the real cost.** Do not re-raise this as a defect, do not "protect" the
> sink with a partial refund, and do not add an elapsed-time scaling curve.

**Q2 - Repairs: real channel, view filter, or fold?** *(Evidence:)* Repair is not a queued job today - it
is a separate instant spend-and-heal system that already ships and works
(`WallRepairController.cs:912,745-782`). Making it a tab means either surfacing that instant system
inside the screen, or converting repair into a timed job (which creates the `instant-repair` sink your
packs already sell). *(D2)*

> **RULED: repairs are NOT a timed queued job.** Fold into Builder or keep as the existing instant
> crystal spend - **she prefers the existing instant heal system**, surfaced cleanly inside the manage
> screen **if it fits**. **DO NOT convert repair into a queued job.**
>
> **Branch killed:** "convert repair to a timed job" is DEAD. Consequently `JobKind.Repair = 2`
> (`JobKind.cs:39`) remains unenqueued and unhandled - no enqueue site, no `IJobEffect` handler
> (`IJobEffect.cs:66-71` silently no-ops it). It should **either stay dormant with a comment saying so,
> or be removed**; it is oracle-asserted at `ObsidianQueueRegression.cs:104`, so removal is not free.
> **That is a small follow-up, NOT part of this WO.**

**Q3 - Upgrades: view filter over Builder, or its own channel?** *(Evidence:)* Filter is free. A channel
grants a new independent worker pool and speeds all progression. *(D3)*

> **RULED: keep the existing upgrade model and viewer.** Present upgrades as **TABS: Buildings, Walls
> (TBD), Research (tiered items), Troops.** Her words: *"feels like the current upgrade experience, just
> organized better."* This is **presentation over the existing model, not a new channel.**
>
> **Branch killed:** the "own channel = balance change" branch is DEAD. Upgrades do NOT get an
> independent `SlotCount` pool (`BuildTimerService.cs:159-165`), so there is **no progression-speed
> change and no balance impact** - audit row 8 is decided on the free side. **Walls is TBD** as a tab's
> CONTENT; the tab itself is specified.

**Q4 - Cap of 5: per-channel or global? And does it count the active jobs?** *(Evidence:)* Per-channel
matches the existing grain. "5 total" vs "2 active + 5 waiting" are very different games. **Read section
2d first - this is queue DEPTH, a new axis, not `freeBuildSlots`.** *(D4)*

> **RULED: 5 TOTAL PER LINE** - per channel / per queue, **not global**. Five on Builder, five on
> Research, five on Troop, independently. Matches the existing `ChannelState` grain
> (`ObsidianQueueState.cs:33-36`). Section 2d still governs: this is DEPTH, and **`freeBuildSlots` is
> not touched**.

**Q5 - Can a QUEUED job be Finish-Now'd?** *(Evidence:)* Today it cannot, and it does not even get a row
(`ObsidianQueueHud.cs:268`; `BuildTimerService.cs:371,383,436`). With 2 slots and a 5-deep queue, 3 of 5
items would show nothing. Options: allow it (promote to active), show it disabled with a stated reason,
or price it as "finish everything ahead of it too".

> **RULED: YES.** Allow **Complete Now with crystals even while the job is still waiting** (not started).
>
> **Implementation consequence, already identified by this audit and now load-bearing:** pending jobs
> carry **`StartMs = 0`** (`ObsidianQueueEngine.cs:53`) and **today get no row at all** - the UI
> early-returns on `job.StartMs <= 0` (`ObsidianQueueHud.cs:268`) and the API rejects them
> (`BuildTimerService.cs:371,383,436`). So this ruling needs **BOTH a row AND a price for not-yet-started
> work.** The existing curve prices from REMAINING time (`BuildTimerConfig.cs:152-158`); what "remaining"
> means for a job that has not started is a pricing input the implementer must define against that curve
> **without writing a second curve** (section 10). *(This WO does not choose that definition - it names
> the constraint.)*

**Q6 - What is the DEPTH cap's upgrade lever?** *(Evidence:)* Crystal purchase (matching her `BuySlot`
sinks), account-level milestone (the dial already named at `BuildTimerService.cs:154-157`), or a building
level? *(D5)*

> **RULED: tied to ECHOES.** **Each Echo above 2 unlocks the OPTION to purchase one extra queue slot with
> crystals.** It is a **two-step gate**: the Echo count unlocks the RIGHT to buy; crystals complete the
> purchase. Not the account-level milestone dial at `BuildTimerService.cs:154-157`, and not a building
> level.
>
> *(Unverified at source by this audit, and the check that settles it: this WO did NOT open the Echo
> system this session and cannot cite where the owned-Echo COUNT is read from. Before implementing, find
> the authoritative Echo-count accessor and confirm it is reachable from the queue/economy assembly
> without a new cross-assembly dependency - CLAUDE.md section 5 forbids Village <-> HUD direct
> references. Six Echoes exist per CLAUDE.md section 7, so the lever tops out at four extra slots if
> "above 2" means Echoes 3-6; confirm that reading with the PO if the implementation needs the ceiling.)*

**Q7 - Do the "+builder slot" / "extra train queue" sinks land in THIS WO or a follow-up?** *(Evidence:)*
She called them second-priority. The mechanism and per-channel state already ship (`BuySlot`,
`BoughtSlots`); only a price is missing (B3/M11).

> **RULED: ALL THREE sinks are on the table** for the extra slot -
> 1. a **special pack**,
> 2. a **direct crystal purchase**, and
> 3. a **TEMPORARY unlock after watching X ads**, for a duration.
>
> > ### FLAG - THE TEMPORARY AD-UNLOCK IS NEW STATE NOBODY HAS SCOPED
> >
> > A slot that **EXPIRES** needs an **expiry timestamp persisted per channel**. The state that exists
> > today cannot express it: **`ObsidianQueueState.BoughtSlots` is a permanent `int` with no expiry
> > concept** (`ObsidianQueueState.cs:33-36`, validated `SaveSchema.cs:833`, summed into `SlotCount` at
> > `BuildTimerService.cs:159-165`). Adding expiry is therefore a **SCHEMA question**, not a UI one - and
> > it lands in the same save area as **WO-912's rolling-window work** (`SaveSchema` `AdSkipsUsedToday` /
> > `AdSkipDayKey`).
> >
> > **CROSS-REFERENCE WO-912 EXPLICITLY: design the two together, not twice.** Both need per-player
> > time-bounded ad state in `SaveSchema`; solving them separately will produce two incompatible
> > expiry/rollover mechanisms in the same file. This WO also already needs a schema bump for per-job
> > cost (M2, v36 -> v37, `SaveSchema.cs:11`) - **one coordinated bump, not three.**
> >
> > *(Not verified at source by this audit: the exact `SaveSchema` line numbers for `AdSkipsUsedToday` /
> > `AdSkipDayKey` were not opened this session - they are cited from WO-912's scope, not from a read.
> > Check: open `SaveSchema.cs` and confirm both fields and their current rollover semantics before
> > designing the expiry.)*

**Q8 - Ad button: ship, hide, or SDK-gate?** *(Evidence:)* It currently grants free time with no ad -
B1: `RewardedAdManager.cs:96-100` invokes the reward callback immediately, `:36` is an 8-minute cooldown,
and no ad package exists in `Packages/manifest.json`. *(D6)*

> **RULED: SHIP IT ON.**
>
> **Wiring a real ad behind the button is WO-912's scope, NOT this WO's.** That produces a hard
> **ORDERING DEPENDENCY, carried into section 9 as an acceptance criterion: shipping the button BEFORE
> the ad SDK lands gives away free time** (-15 min x up to 10/day - `adSkipSeconds` 900 consumed at
> `BuildTimerService.cs:405`, `adSkipsPerDay` 10 at `:605`). The button is ruled ON; the ORDER in which
> it reaches players is a release-sequencing gate the PO must clear.

**Q9 - Fix B8 (the release-gated store) as part of this, or accept a broke-case route that lands on
"Coming soon"?** *(Evidence:)* Her rule requires a "get crystals" route; today that route dead-ends in a
release build.

> **RULED: FIX IT.** Open **`RealmStorePurchase` in release builds** and **remove the "Coming soon"**
> copy - `FeatureFlags.cs:555` (`Get("realmstorepurchase", defaultOn: IsDevBuild)`) and
> `PackStore.cs:374-378`. **This is B8, the highest-severity finding in this audit: the crystal faucet is
> CLOSED in the shipped APK.** B8 is hereby **IN SCOPE** for this WO (section 7 updated).

**Q10 + Q13 - MERGED. The bar gets SMALLER, and this is the same screen as WO-905's "Manage".**
*(Evidence, Q10:)* The bar is full at 7 (`hud-areas.json:48-58`). An 8th needs the enum, `ButtonCount`,
the hardcoded `6f`/`7f` (`HudKitController.cs:106`), rows in both JSON copies, and six oracle updates. A
replacement must name its victim. Also: does the Builders CHIP survive alongside the button, or retire in
its place? *(Evidence, Q13:)* WO-905 already specs one screen with all three channel rails
(`WORK_ORDER_905:81`), reachable from the existing "Upgrade" bar face.

> **RULED:**
> - **Replace the current `Upgrade` face** with a unified **Manage/Queues** screen holding the three
>   queues: **Builder, Research, Troop**.
> - **This IS WO-905's "Manage" screen - MERGE them, do not build two.**
> - **Map moves into Bag as a tab.** **Quest stays.**
> - **Defenses upgrades live as a tab inside the queue screen** (per Q3's tab set).
> - Her framing: *"Think Warcraft-style parallel production lines."*
>
> **Consequences to record:**
> 1. **The bar goes 7 -> 6 faces.** There is **NO eighth button**, so the enum/geometry/oracle problem
>    Q10 raised **is dissolved**: `ActionBarButtonId.Upgrade = 6` (`HudActionBarModel.cs:55-64`) is
>    **RE-POINTED, not added**, and per the ruling **no `ButtonCount` increase is needed.**
>    **See the FLAGGED check in section 3b:** the ruling's "no `ButtonCount` change" statement is exact
>    for the re-point, but removing **Map** from the bar is a REMOVAL that this audit cannot confirm is
>    free against a hardcoded `ButtonCount = 7` (`:74`) and `BarSlotW = (1f - BarGap * 6f) / 7f`
>    (`HudKitController.cs:106`). Named check, not an assertion.
> 2. **The right-rail Builders CHIP survives, as a STATUS GLANCE ONLY** - count/timer, no door. **The new
>    bar face is the single entry point.** Otherwise there are two doors and the 2026-08-01 "one Queues
>    entry" oracle has nothing left to mean. The chip's oracle row
>    (`ObsidianQueueRegression.cs:303`) stays; the double-tap door at `HudKitController.cs:781-791`
>    (B4's undiscoverable path) is what goes away.

**Q11 - Does "Finish now on EVERY real wait" mean this screen only, or a game-wide pass?** *(Evidence:)*
Other real waits exist (harvest / offline harvest, raid cooldowns, the ad cooldown at
`RewardedAdManager.cs:36`, daily quest rolls). This materially changes the size of the work.
*(Section 2e)*

> **RULED: EXPLICIT ITEM ONLY.** Finish Now applies to the item the player explicitly picks in this
> screen. **NOT a game-wide pass over every wait.** Harvest timers, raid cooldowns, the ad cooldown and
> daily quest rolls are **out of scope** - section 2e's boundary question is closed on the narrow side.

**Q12 - Cancel on a collapsed Train card. RULED 2026-08-06.** Identical pending troop
trains collapse into ONE xN card (`BuildTimerService.cs:690-693`), so on the Train tab **a card is not
one job**. Does cancelling a x5 card **cancel all five, cancel one, or must the card expand first**?

> **RULED: THE CARD MUST EXPAND FIRST. A collapsed xN card has NO cancel affordance.** Owner, verbatim:
> *"can not cancel on a collapsed card, must expand then select item to cancel and others automatically
> move up."* Expand -> select the one item -> cancel that job -> **remaining items auto-close the gap.**
> Refund per Q1 (100% flat) for that one item only. Same principle as Q11: a destructive or paid verb
> never acts on an ambiguous aggregate.
>
> Note the shape of the problem, which the ruling fits cleanly: cancel is keyed by `structureId`, not by index
> (`CancelChannelJob(ChannelId, string)`, `BuildTimerService.cs:533-556`), and the collapse happens at
> PUBLISH time (`:690-693`), so the underlying jobs are still individually addressable - the ambiguity is
> in the CARD, not the engine.

---

## 9. ACCEPTANCE CRITERIA

**All parameterized placeholders are RESOLVED to the ruled values (2026-08-06).** An implementer never
has to look a question up - the number/behaviour is written inline below. The only criterion still
carrying an open dependency is the Q12 one, which is explicitly labelled BLOCKED.

**Monetization spine (her DOs, as gates)**
- [ ] Finish Now price is derived from REMAINING TIME via the existing
      `BuildTimerConfig.InstantFinishPrice` (`:152-158`) - **no second pricing path is written**, and no
      flat fee appears anywhere.
- [ ] A near-done job still costs at least `instantFinishMinCrystals` (`:90`) - asserted with a job at
      ~5 seconds remaining, which must NOT be free.
- [ ] Finish Now is offered on **Builder, Train AND Research** - **exactly those three; Q2 and Q3 add no
      new channel** (repairs stay instant, upgrades are a view) - spending the same single crystal ledger
      (`GameStateService.AddCrystals`).
- [ ] Finish Now is offered on a **QUEUED (not started) job**, not just a running one *(Q5)*. A job with
      `StartMs = 0` (`ObsidianQueueEngine.cs:53`) gets **a row AND a price**; the current early-return at
      `ObsidianQueueHud.cs:268` and the API rejections at `BuildTimerService.cs:371,383,436` no longer
      suppress it. Asserted with a 5-deep queue on 2 slots: **all 5 items offer Complete Now**, none is
      row-less.
- [ ] Finish Now fires **only on the item the player explicitly picks** *(Q11)*. No game-wide pass: no
      harvest timer, raid cooldown, ad cooldown or daily-quest roll gains a Finish Now in this WO.
- [ ] While a job RUNS, the Finish button is **always visible**, including when the player cannot
      afford it; the unaffordable state is encoded in **TEXT**, never by colour alone (owner is
      red/green colourblind - the standard `QueueRailView.cs:76-78` already holds).
- [ ] Tapping Finish while broke opens the crystal-buy route via `PanelRouter.Open(PanelId.RealmStore)`
      (`PackStoreBootstrap.cs:47,67`) - it never silently no-ops as it does today
      (`BuildTimerService.cs:420`).
- [ ] **B8 is FIXED in this WO** *(Q9)*: `RealmStorePurchase` is OPEN in release builds
      (`FeatureFlags.cs:555`) and the **"Coming soon"** copy is gone (`PackStore.cs:374-378`), so the
      broke-case route lands on a real Buy CTA in a **release** build - verified in a release-configured
      build, not just a dev build.
- [ ] Nothing in this screen sells combat power or a permanent buff.

**Ad button (Q8) - ordering dependency**
- [ ] The Ad speed-up button **ships ON**.
- [ ] **ORDERING GATE (blocking, PO-cleared):** the button must **NOT reach players before the real ad
      SDK lands (WO-912)**. Until then `RewardedAdManager.ShowAdInternal` invokes the reward callback
      immediately (`:96-100`) with no ad package in `Packages/manifest.json`, so shipping the button
      early **gives away free time** - up to 10 x -15 min/day (`BuildTimerService.cs:405`, `:605`). The
      implementer must state, at hand-off, whether the SDK has landed; the PO clears the release order.
      **Wiring the real ad is WO-912's scope, not this WO's.**

**Entry and reversal** *(all resolved by the Q10+Q13 merge)*
- [ ] A single dedicated bottom-bar face opens the unified Manage/Queues screen in ONE tap from town.
- [ ] **No 8th face is added.** The existing **`Upgrade` face is RE-POINTED** to the new screen -
      `ActionBarButtonId.Upgrade = 6` (`HudActionBarModel.cs:55-64`) keeps its VALUE.
- [ ] **`Map` leaves the bar and becomes a TAB inside `Bag`.** `Quests` stays. Resulting calm(town) bar:
      **Build, Talk, Bag, Raids, Quests, Manage = 6 faces**.
- [ ] The bar count/geometry question in the section 3b FLAG is **settled at source before the bar is
      built**: `ButtonCount` (`HudActionBarModel.cs:74`) and `BarSlotW = (1f - BarGap * 6f) / 7f`
      (`HudKitController.cs:106`) either move to 6 with the literals derived, or the implementer states
      why 7 still holds. **A 6-face bar must not render with a dead trailing slot** - checked on the
      captured PNGs, not by reading.
- [ ] The three retirement assertions at `ObsidianQueueRegression.cs:283-284`, `:288` and `:302` are
      INVERTED (not deleted) so the gate guards the button's presence.
- [ ] The bar face set/order oracles (`HudActionBarRegression.cs:125-128` and the exact-set asserts at
      `:82-83`, `:86-88`, `:132`, `:134-135`; `HudActionBarModelTests.cs:171,184`) are **UPDATED to the
      new 6-face set and order - never deleted or weakened** to make a smaller bar pass.
- [ ] `hud-areas.json` updated in BOTH canonical copies, still byte-identical
      (`ObsidianQueueRegression.cs:305-307`).
- [ ] The **Builders CHIP remains, as a STATUS GLANCE ONLY** (count/timer) - its oracle row
      (`ObsidianQueueRegression.cs:303`) still passes, and it is **no longer a door**: the double-tap
      open path at `HudKitController.cs:781-791` is retired so the bar face is the **single** entry
      point (preserving the meaning of the 08-01 "one Queues entry" rule).
- [ ] CLAUDE.md section 7 updated in the SAME commit: the 08-01 bar-button retirement recorded as
      REVERSED 2026-08-06, the face count recorded as **6** with the NEW membership (the old "6 faces"
      line was stale for a different reason - B6 - and must not be left to read as if it were already
      correct).
- [ ] **WO-905 marked SUPERSEDED by WO-911** (`WORK_ORDER_905_manage_screen_upgrade_browser.md`) - one
      screen exists, not two.

**The screen**
- [ ] ONE screen (the merged Manage/Queues screen), tabbed, holding the three production lines:
      **Builder, Research, Troop** *(Q10+Q13)*.
- [ ] **Upgrades appear as TABS over the EXISTING upgrade model/viewer - not a new channel** *(Q3)*:
      **Buildings, Walls (TBD), Research (tiered items), Troops.** No new `SlotCount` pool is created for
      upgrades (`BuildTimerService.cs:159-165` is unchanged in grain), so **no balance shift**. Defenses
      upgrades live as a tab inside this screen.
- [ ] **Repairs are NOT a queued job** *(Q2)*. If repair is surfaced here at all it is the EXISTING
      instant crystal spend-and-heal (`WallRepairController.cs:912`, `RepairAll()` `:745-782`,
      `RepairAllCost()` `:722`), presented cleanly - **"if it fits"; it is not mandatory**. No repair job
      is ever enqueued, and `JobKind.Repair` is left alone (dormant-with-comment or removed is a separate
      follow-up).
- [ ] Every item in the selected channel's queue is reachable and readable - no dead "+N MORE" tail. A
      queue at the cap **of 5** is fully browsable at 1920x1080 AND 2340x1080 with rows at or above
      `ElarionUiKit.MinTouchPx` (112).
- [ ] Reuses `QueueRailView` or a documented sibling; the host-agnostic signature oracle
      (`ObsidianQueueRegression.cs:373-376`) still passes.
- [ ] ASCII-only strings; state carried by TEXT, not colour.

**Cancel / refund / promote**
- [ ] Cancelling the SECOND item in a 3-deep queue removes exactly that item, the order closes up, and
      the screen plus the Builders chip repaint within 1s.
- [ ] Cancelling the ACTIVE item frees its slot and the next pending job starts immediately.
- [ ] The refund lands in the single wallet at **100% of what was paid** *(Q1)*, asserted as an exact
      wallet delta.
- [ ] **100% holds regardless of elapsed time** *(Q1)*: asserted on BOTH an untouched pending job AND an
      ACTIVE job that is ~90% elapsed - **the two refunds are identical**. No elapsed-time scaling, no
      partial-refund "sink protection". The player's loss is the elapsed TIME; that is the accepted cost.
- [ ] A job from a pre-v37 save cancels cleanly with a zero refund and traces the migration case.
- [ ] "Bump up the next item" is reachable, driving the existing `ReorderPending`
      (`BuildTimerService.cs:562-576`) - no new reorder engine.
- [ ] **BLOCKED ON Q12 - do not implement until ruled:** cancel on a COLLAPSED Train card. Identical
      pending troop trains publish as ONE xN card (`BuildTimerService.cs:690-693`). Whether a x5 card
      cancels five, cancels one, or must expand first is **the one unruled decision**. Build the rest;
      leave this path to the PO's ruling rather than guessing a default.

**Cap**
- [ ] The depth cap is **5 TOTAL PER LINE** - per channel/queue, **NOT global** *(Q4)*. Asserted: Builder
      at 5 does **not** block enqueueing on Research or Troop.
- [ ] Enqueue is REFUSED at the DEPTH cap with a player-readable reason, on **every** entry path into
      `ObsidianQueueEngine.Enqueue` (`:43-56`) - not just this screen's buttons.
- [ ] The depth cap is authored as DATA (a new `BuildTimerConfig` field alongside `freeBuildSlots`) so
      "upgradable later" is a data change, not a code change.
- [ ] **`freeBuildSlots` is NOT changed** - it stays 2 and its oracle
      (`BuildEconomyRegression.cs:1183-1184`) still passes. The depth cap is a separate field.

**Extra-slot sinks (Q6 + Q7)**
- [ ] The depth-cap upgrade lever is **ECHO-GATED, two-step** *(Q6)*: **each Echo above 2 unlocks the
      OPTION to purchase one extra queue slot with crystals.** Echo count unlocks the RIGHT to buy;
      crystals complete it. Neither step alone grants the slot. **Not** the account-level milestone dial
      (`BuildTimerService.cs:154-157`); **not** a building level.
- [ ] `BuySlot(ChannelId)` (`BuildTimerService.cs:168-178`) **charges** - it must not remain the free
      increment of B3. The per-channel `BoughtSlots` state (`ObsidianQueueState.cs:33-36`) is reused, not
      re-modelled.
- [ ] **All three sinks are permitted** *(Q7)*: a special pack, a direct crystal purchase, and a
      TEMPORARY unlock after watching X ads (for a duration).
- [ ] **BEFORE any temporary/expiring slot is implemented, the schema is designed JOINTLY WITH WO-912.**
      `BoughtSlots` is a permanent `int` with **no expiry concept**; an expiring slot needs a persisted
      per-channel expiry timestamp, which is the same `SaveSchema` territory as WO-912's rolling-window
      ad state (`AdSkipsUsedToday` / `AdSkipDayKey`). **One coordinated schema bump** covering per-job
      cost (M2) and any expiry field - not two competing rollover mechanisms in one file.

**Gates**
- [ ] `COMPILE_GATE_OK`.
- [ ] `REGRESSION_OK <n>/<n> suites` - read the count off the marker, never restated.
- [ ] `UI_CAPTURE_OK` with the PNGs OPENED and reviewed: full-cap queue, empty queue, mid-cancel, and
      **the broke state showing a visible Finish button**, at 1920x1080 and 2340x1080.
- [ ] The `[obsidian-queue]` oracle extended with: Finish Now priced from remaining time on **all three**
      channels; the minimum price holds on a near-done job; **a QUEUED (`StartMs = 0`) job is
      Finish-Now-able and priced**; cancel refunds **100%** of the stored cost, **identically for a
      90%-elapsed active job and an untouched pending one**; cancel-active frees the slot and cascades;
      **enqueue is refused at 5 PER LINE and only on that line**; the v36->v37 migration holds.
- [ ] `UI_CAPTURE_OK` PNGs also show the **new 6-face bar with no dead trailing slot** and the **Builders
      chip rendering as a status glance** (see the section 3b FLAG).

---

## 10. WHAT NOT TO TOUCH

- `ObsidianQueueEngine.Resolve`'s offline catch-up chaining (`ObsidianQueueEngine.cs:100-116`) - the
  freed slot deliberately starts at `done.FinishMs`, not `now`, so offline chains drain correctly. A
  cap check belongs in `Enqueue`, never in `Resolve`.
- `BuildTimerConfig.freeBuildSlots` (oracle-pinned at 2) - and see section 2d for WHY raising it would
  break the monetization ruling.
- `BuildTimerConfig.InstantFinishPrice` (`:152-158`) - generalize its CALLERS, do not rewrite the curve.
- The `QueueRailView` fixed-pixel band geometry and its `FitToWidth` measurement
  (`QueueRailView.cs:97-119`, `:627-644`) - both encode fixed WO-841/852/883 bugs.
- The Train instant path (`TroopTrainingVM`) - PO-gated separately per `WORK_ORDER_799:59`.
- **The existing upgrade model and viewer** *(Q3)* - this WO **presents** it as tabs. Do not re-model
  upgrades, do not give them a channel, do not change what an upgrade costs or how long it takes.
  "Feels like the current upgrade experience, just organized better."
- **`WallRepairController`** *(Q2)* - call it, surface it, never restructure it into a timed job.
- **The refund fraction** *(Q1)* - 100%, flat. Do not add an elapsed-time curve to "protect" the sink.
- `CLI_LANES_WO_NUMBERS.md` - the orchestrator/CLI owns the banner.
- Any `.unity` scene file.

---

## 11. WHERE THIS AUDIT IS UNSURE

Stated plainly, each with the ONE check that settles it.

1. **Whether structure damage survives a raid-to-town transition.** `buildingDamage` is declared and
   plumbed (`SaveSchema.cs:265`; `GameStateService.cs:451,542,962`) but never written by gameplay, so
   damage appears to live only on scene objects. Bears directly on Q2 (a Repairs tab needs something
   durable to list). *Settles it:* damage a structure, save, reload, read HP back - or `FlowTrace` the
   write sites across a raid return.
2. **Whether the stockpile buildings (lumberyard/foundry/silo) could serve as the depth cap's upgrade
   lever.** CLAUDE.md records them as capacity-cap progression buildings, but that is RESOURCE storage
   capacity; I did not open those buildings this session and cannot assert they expose a queue-relevant
   level. *Settles it:* read the three building definitions and check whether any exposes a level
   another system already reads as a cap.
3. **The exact cost each charge site pays**, which M2 must persist onto the job. WO-799:33 names
   `BuildingUpgradeService.TryUpgrade`, `BarracksService` (x3) and `BuildModeController.Place`. I
   verified the refund helpers exist (`BuildModeController.RefundCostFor` via
   `BuildEconomyRegression.cs:463`; `TowerPlacementSystem.RefundForCancel` via
   `BuildMenuRealEconomyRegression.cs:578`) but did NOT open each charge site to confirm that list is
   complete. *Settles it:* grep every caller of the enqueue seams (`BuildTimerService.cs:217,226,305,309`)
   and confirm each has a charge immediately preceding it. **Q1's 100%-flat ruling raises the stakes
   here: the refund is now exactly "what was paid", so the stored cost must be right at every site.**

### 11b. ADDED 2026-08-06 - consequences of the rulings this audit could NOT verify at source

The rulings were made against the evidence above; these four consequences were NOT read at source this
session and are recorded as CHECKS, not claims. None blocks the ruling; each blocks its own line of code.

4. **The 6-face bar's count and geometry** *(Q10)*. The ruling states no `ButtonCount` change is needed -
   exact for the Upgrade->Manage re-point, but Map's REMOVAL is a separate count change against a
   hardcoded `ButtonCount = 7` (`HudActionBarModel.cs:74`) and `BarSlotW = (1f - BarGap * 6f) / 7f`
   (`HudKitController.cs:106`). *Settles it:* read those two together, decide whether the bar renders
   `ButtonCount` faces or the model's actual list, then verify on the captured PNGs that a 6-face bar has
   no dead trailing slot. Also decide `ActionBarButtonId.Map`'s fate (retired vs. dormant).
5. **Where Map lives inside Bag** *(Q10)*. The Realm Map shipped as WO-826 and the bar's Map face is
   asserted in the exact-set oracles; I did NOT open the Bag panel this session and cannot assert it has
   a tab host. *Settles it:* open the Bag panel and confirm whether it already supports tabs, or whether
   a tab host is new UI work inside this WO.
6. **The authoritative owned-Echo COUNT accessor** *(Q6)*. The Echo-gated slot purchase reads "each Echo
   above 2", but I did not open the Echo system and cannot cite where that count is read. *Settles it:*
   find the accessor and confirm it is reachable from the queue/economy assembly without violating the
   cross-assembly rule (CLAUDE.md section 5). Confirm the ceiling with the PO (six Echoes exist per
   CLAUDE.md section 7).
7. **WO-912's existing save fields** *(Q7)*. `AdSkipsUsedToday` / `AdSkipDayKey` are cited from WO-912's
   scope, not from a read of `SaveSchema.cs`. *Settles it:* open `SaveSchema.cs`, confirm both fields and
   their rollover semantics, and design the expiring-slot timestamp in the SAME pass so the two WOs share
   one mechanism and one schema bump (with M2's per-job cost).
