# GROK GUIDANCE WORK ORDER — Reconcile and execute the 2026-08-22 Ready queue

**Status:** UNTRACKED GUIDANCE — NOT A BOARD ITEM  
**Authoring lens:** GROK / advisory design and implementation guidance  
**For:** CLI lead / sole committer  
**Date:** 2026-08-22  
**Scope:** WO-1139, 1138, 1137, 1136, 1135, 1134, 1133, 1128, 1121, 1114,
1051, 1047, 1026, 992, 903, 829, and the unnumbered SKR Store design.

> This file is deliberately unnumbered and must not be added to `BOARD.html`, the numbering banner,
> or a backlog lane. It is a routing and quality brief, not a new source of product truth. Each numbered
> WO remains authoritative for its own behavior. If this guidance conflicts with an owner ruling or a
> newer measured fact, the ruling/fact wins and the discrepancy must be recorded before implementation.

---

## 0. Outcome

Turn this group into one coherent delivery program without implementing stale duplicates, silently
closing partial work, or mixing unrelated changes into commits. The intended result is:

1. reconcile the queue against the current tree and captured evidence;
2. reclassify decision/evidence-blocked items before pulling implementation;
3. retire only superseded board records, never their historical documents;
4. implement the live numbered WOs in dependency order;
5. gate and commit each coherent lane by explicit path; and
6. leave the board accurately derived from the status lines in the authoritative WOs.

This is guidance, not blanket authorization to resolve unanswered creative choices. The CLI should
execute decided behavior and surface undecided behavior to the owner with a concrete recommendation.

---

## 1. First-pass disposition

| Record | Recommended disposition | Why |
|---|---|---|
| WO-1139 | **KEEP READY — implement** | It is the ruled loss-stakes implementation and the remaining gate for enabling Siege. |
| WO-1138 | **KEEP READY — implement** | The four-line detector window demonstrably misses five of six known hollow-pass sites. |
| WO-1137 | **RECLASSIFY: OWNER DECISION REQUIRED** | The WO still offers two materially different fallback policies. Do not choose product failure behavior by inference. |
| WO-1136 | **RECLASSIFY: EVIDENCE + OWNER DECISION REQUIRED** | `staff_A` has no decidable sheathe orientation without the requested device view and creative ruling. |
| WO-1135 | **KEEP READY — implement** | Wood/iron/steel wall appearances still depend on embedded FBX materials and lack tracked tier ownership. |
| WO-1134 | **KEEP READY — implement** | The ladder terminus is now ruled at 12/18/24 clears and loss stakes are owned by WO-1139. |
| WO-1133 | **KEEP READY — implement design** | The design pass is delivered; implementation remains. |
| WO-1128 | **KEEP READY, RECONCILE FIRST** | Client provisional accrual seams exist; prove exactly which server reconciliation pieces are already present before adding another owner. |
| WO-1121 | **KEEP READY — staged implementation** | Buy gate and truthful catalog work may land while purchases remain off; live rail activation requires every financial gate. |
| WO-1114 main WO | **KEEP PARTIAL — finish/verify remaining scope** | A gate-green partial delivery is not completion. Reconcile the current tree and untracked endpoint before continuing. |
| WO-1114 implementation plan | **SUPERSEDED COMPANION — no independent board card** | It is an execution companion to the main WO, not a second deliverable. Preserve the file; remove only duplicate board authority through status metadata. |
| WO-1051 | **KEEP READY — implement** | Daily Chest geometry still has a specified collision between claim actions and the shared Close control. |
| WO-1047 | **KEEP PARTIAL/INSTRUMENTED** | The object class is narrowed to `BreakableContainer`; a current-device capture must prove the live path before the targeting/art fix. |
| WO-1026 delivery WO | **ALREADY COMPLETE** | Siege cadence and the persisted Defense Report shipped; loss stakes were explicitly split to WO-1139. |
| WO-1026 implementation plan | **SUPERSEDED — do not implement again** | The old READY plan is historical input to the completed delivery and now creates a false duplicate Ready card. |
| WO-992 | **KEEP PARTIAL — reconcile six rows** | Some rows are already deleted/wired/escalated; remaining work must follow the recorded per-class disposition, not the original headline. |
| WO-903 | **KEEP READY — implement** | Its former storage-cap blocker was resolved and the quarter-fill presentation remains unbuilt. |
| WO-829 | **KEEP PARTIAL — finish presentation/producers** | Core work landed, but the parchment realm-map treatment and content-pin producers remain. |
| Unnumbered SKR Store design | **REMOVE FROM READY; RECONCILE OR DEPRECATE AS A STANDALONE PLAN** | Its own status says draft, its aged banner says Ready, and newer store/payment WOs now own much of the surface. Mine valid requirements; do not implement it wholesale. |

No historical WO file should be deleted. “Deprecate” means an explicit superseded banner/status that
prevents the board generator from presenting it as executable work, with a pointer to the live owner.

---

## 2. Binding CLI method

Before editing, follow the repository preflight and read-first canon. Then:

1. Regenerate the board from the repo before trusting its categories.
2. Record `git status --short`; protect every pre-existing path and every other seat's work.
3. For each pulled WO, compare its asserted line numbers, files, and missing behavior to the current
   source. Comments and an earlier regression count are not current proof.
4. For bugs, read the captured trace first. If the decisive runtime evidence does not exist, add
   permanent `FlowTrace`/`Guard` instrumentation and capture it before changing behavior.
5. Never create a parallel state owner, wallet, catalog, timer, save field, or UI shell merely because
   the WO predates the current implementation.
6. Make the smallest cohesive change that satisfies the current WO and update its regression in the
   same lane.
7. Run brace/NUL/compile/data gates required by the touched files. Run the relevant headless or device
   proof when the acceptance criterion is visual, lifecycle-based, networked, or persistence-based.
8. Write/update the numbered WO's RESULT and status only when its full acceptance contract is proven.
9. Stage by explicit path. Never use `git add -A`, never absorb untracked logs/build output, and never
   commit a file merely because another seat left it modified.

### Commit discipline

Prefer one commit per independently revertible outcome. A reasonable grouping is:

- queue/document reconciliation only;
- regression hardening (WO-1138);
- art/material authoring (WO-1135, then WO-1136 only after ruling);
- siege/endgame logic (WO-1139 followed by WO-1134);
- each large UI surface separately (WO-1133, WO-1051, WO-829, WO-903);
- offline/backend reconciliation (WO-1128);
- money-rail implementation with activation held off (WO-1121);
- dungeon status and dungeon prop as separate commits (WO-1114, WO-1047);
- dead-code reconciliation (WO-992).

Do not combine all listed WOs into one commit. “One program” describes sequencing and shared quality,
not an undifferentiated diff.

---

## 3. Recommended execution order

### Phase A — Repair the queue, without changing gameplay

1. Reconcile the two WO-1026 documents. Keep the completed delivery authoritative; mark the old plan
   as a superseded companion pointing to the delivery and WO-1139.
2. Make the WO-1114 implementation plan a non-board companion of the partial main WO.
3. Remove the unnumbered SKR Store design from executable Ready state. Cross-reference its still-valid
   requirements into WO-1121 or a newly owner-approved, numbered follow-up; do not mint that follow-up
   from this guidance file.
4. Reclassify WO-1137 and WO-1136 as awaiting their explicit pins unless the required rulings/evidence
   are already recorded elsewhere in newer canon.
5. Regenerate `BOARD.html` and confirm there is one actionable row per deliverable.

This phase changes documentation/status metadata only. It must not rewrite frozen historical bodies.

### Phase B — Strengthen the gates before broad feature work

Implement WO-1138 first. A detector that misses known hollow passes makes later “green” runs less
trustworthy. The fix must scan the complete relevant method/body or syntax structure, not merely expand
four lines to another magic number. Pin all six known sites and add a formatting-variation fixture so a
line wrap cannot reopen the hole.

Then implement WO-1135 so tier-material ownership is explicit and tracked before additional visual
work depends on it. Do not duplicate embedded FBX material instances into opaque runtime mutations;
establish a canonical, source-controlled tier mapping and make the regression validate identity and
assignment.

### Phase C — Close the siege/endgame contract

Implement WO-1139 before WO-1134:

- the loss report must derive from the same transaction that mutates resources;
- apply the ruled theft floor and cap exactly once;
- exclude crystals and all prohibited permanent/progression loss;
- make repair cost deterministic, inspectable, and equal in the report and wallet mutation;
- preserve offline readability and idempotence across reload/retry;
- enable `FeatureFlags.Siege` only in the final change after the complete loss loop is green.

Then implement WO-1134 using the ruled clear caps: regular 12, hard 18, extreme 24. At the cap, a camp
remains repeatable but no longer escalates and its progression rewards cease at the same terminus.
Cooldowns and attrition must use the recorded owner values in that WO. A capped camp must never roll
over, overflow, or silently reset its clear count.

Add an integration regression that covers win, loss, reload, capped clear, and repeated capped clear.
The report, save, wallet, camp state, and UI copy must agree after every transition.

### Phase D — Player-facing UI and world readability

Treat these as separate visual deliverables that share tokens/components but retain bounded ownership:

- **WO-1133 Bag:** implement the delivered information hierarchy. Reuse the established panel shell,
  spacing, typography, item-card, empty-state, focus, and close/back behaviors. Keep the gear view only
  if it enables a distinct player decision; otherwise remove the redundant navigation rather than
  maintaining two representations of the same inventory.
- **WO-1051 Daily Chest:** reserve a protected footer/safe-area for the shared Close action. Claims must
  scroll or reflow above it at every target aspect ratio; do not solve one screenshot with fixed pixels.
  Validate no overlap, minimum touch targets, long localization strings, claimed/ready/locked states,
  and phone safe-area insets.
- **WO-903 Pallets:** derive quarter states from normalized stored/capacity values through one shared
  fill-state classifier. Art and labels consume the same 0/25/50/75/100 result. Define zero-cap and
  over-cap behavior and prevent per-resource copies of threshold logic.
- **WO-829 Realm Map:** finish the parchment/atmosphere layer and the actual pin producers. Pins must
  communicate type, availability, and selection without color alone. Use the common map-pin component,
  deterministic collision handling, and one legend/filter grammar.

For every screen, capture before/after images from the same device, resolution, state, and camera. Put
the evidence in the numbered WO's normal evidence location, not in this guidance file. Visual review is
not replaced by a data regression; both are required where applicable.

### Phase E — Server authority and money rails

#### WO-1128: offline accrual

First map the current end-to-end flow: last authoritative server time → disconnect window → provisional
client display → save request → server recomputation/reconciliation → accepted delta → client correction
and telemetry. Current client files already mention provisional accrual and server offset; that is a
reason to reconcile, not to add a second coordinator.

The server must calculate the grant from server-known state and bounded elapsed time. A client-reported
amount is a claim to compare/log, never the authority. Make the operation idempotent with a stable
window/receipt identity; retries must not double-grant. Return authoritative time and accepted delta,
and make correction legible without punishing an honest offline player. Test clock-forward, clock-back,
duplicate request, stale save, very long absence, no assigned worker, storage boundary, and concurrent
device requests.

#### WO-1121: live money rails

Separate “implemented and testable” from “enabled for players.” Keep purchasing off until every listed
rail, environment, entitlement, receipt, retry, restore, and observability gate passes. The UI must
explain the actionable refusal state rather than presenting a dead Buy control.

Financial invariants:

- server/chain-confirmed settlement precedes entitlement;
- a stable purchase id makes post-pay fulfillment idempotent;
- reconnect/retry and process death cannot lose a paid entitlement or grant twice;
- wallet requirement above the ruled threshold is enforced by the common purchase gate;
- catalog content is grantable and accurately described;
- environment/network/mint mismatches fail closed before charge;
- receipts and support diagnostics contain identifiers but no secrets;
- activation of `RealmStorePurchase` is its own final, reviewable change.

Do not let the aged SKR Store design redefine SKR from a payment rail into a held premium balance unless
the owner explicitly re-rules that product model. Extract its ethical/catalog guidance only where it
matches current canon.

### Phase F — Finish partial technical tickets

#### WO-1114: dungeon status

Inventory what is actually landed in the main WO: client model/cache, fetch lifecycle, portal appearance
owner, editor/dev controls, endpoint/schema/admin path, telemetry, and regression coverage. Treat any
untracked `api/dungeon-status.js` as another seat's work until ownership and diff provenance are known.
Do not stage it opportunistically.

There must be one appearance owner (`ApplyDoorState`) called from every construction/swap path. A closed
door remains present and understandable; it is not deleted from the world. Network failure uses the
specified cache/default policy and emits a diagnostic. Complete the main WO only when every remaining
acceptance item is present and current gates prove it; the companion plan never gets its own completion.

#### WO-1047: hostile dungeon prop

Use the installed-current-build capture to prove the object identity and registration path. Preserve
damageability if `BreakableContainer` is intentional, while excluding scenery/loot containers from
combat target selection through a semantic targeting contract—not a name check or one prefab exception.
Resolve the orange-cube presentation through the canonical art/addressable path and verify remote content
shipping. Test manual damage, auto-targeting, target cycling, destroyed state, missing art fallback, and
fresh-device remote load.

#### WO-992: unwired classes

Use the WO's per-row disposition as the current unit of work. For each named class, prove one of:

1. a real composition root instantiates/calls it and a regression pins that seam;
2. it is intentionally dormant behind a named feature flag with an owner and no shipping side effects;
3. it is deleted with all references, metadata, catalog/docs, and tests reconciled; or
4. it is split into a specifically owned follow-up because wiring it would exceed this ticket.

Do not resurrect the stale `AuraController` row. Verify the present `TorchFireController`,
`CosmeticApplier`, and `CryptoPaymentManager` state from source before acting; their status has changed
since the original headline. Deleting a `.meta` without resolving the corresponding asset/class and
references is not completion.

### Phase G — Decision-blocked art/catalog items

#### WO-1137

Present the owner with the two explicit policies already in the WO and recommend fail-loud/no fallback
for a canonical catalog when absence would otherwise launch the wrong game. If offline/bootstrap needs a
fallback, generate it from canonical data at build time and pin parity; do not maintain a second hand-
authored catalog. Implement only after the policy is recorded in the numbered WO.

#### WO-1136

Produce the requested device evidence with `staff_A` in idle, locomotion, combat transition, and relevant
camera angles. Recommend the pose with the clearest silhouette and least hand/body clipping, then record
the owner's choice. Keep offsets/orientation in the common weapon-seat data model; do not special-case the
hero update loop. Validate all 12 weapons after the change so fixing the last item cannot regress eleven.

---

## 4. Shared-system opportunities (without a cross-domain god class)

“Common” should mean common primitives and contracts, not one manager that owns unrelated screens and
gameplay. Reuse or extract only where at least two live consumers share the same invariant:

- panel shell: safe area, title rail, close/back footer, modal focus, transition lifecycle;
- visual tokens: typography, spacing, colors, rarity/state treatments, disabled/locked semantics;
- responsive collection: card/list/grid layout and scroll/footer reservation;
- normalized progress classifier: thresholds and display state, with domain values supplied by caller;
- canonical content lookup: one loader, validation policy, and observable failure contract;
- transaction receipt/idempotency primitive: stable operation identity and replay-safe result;
- target eligibility interface/filter: combat semantics independent of art prefab names;
- authored appearance mapping: stable keys to tracked materials/prefabs with explicit fallback diagnostics.

Do not make UI presentation reference Village implementations directly, and do not move domain rules
into a generic UI utility merely to maximize reuse. The shared layer owns mechanics; each bounded context
owns meaning.

---

## 5. Cross-WO acceptance gates

The program is complete only when all of the following are true:

- [ ] The board shows no duplicate actionable WO-1026 or WO-1114 plan card.
- [ ] The unnumbered SKR design is not presented as Ready while its own body says Draft.
- [ ] WO-1137 and WO-1136 have recorded owner decisions before behavior/art is changed.
- [ ] WO-1138 catches all six known hollow passes plus formatting variants.
- [ ] Wall tier materials are source-controlled, uniquely mapped, and regression-pinned.
- [ ] Siege loss is idempotent, bounded, crystal-safe, report-consistent, and only then enabled.
- [ ] Endgame escalation caps at 12/18/24 while capped camps remain repeatable.
- [ ] Bag, Daily Chest, Pallets, and Realm Map pass target-device visual and interaction review.
- [ ] Offline accrual is server-authoritative and replay-safe across hostile clock/device scenarios.
- [ ] Purchase fulfillment is post-pay reliable; Buy remains off until every launch gate passes.
- [ ] Dungeon status has one state/appearance flow and the main WO—not its plan—owns completion.
- [ ] Dungeon props can be damaged as intended without becoming invalid combat targets.
- [ ] Every WO-992 class has a proved live, dormant, deleted, or explicitly deferred disposition.
- [ ] Every completed numbered WO has a RESULT, current regression evidence, and honest status.
- [ ] `BOARD.html` is regenerated from the repo after status reconciliation.
- [ ] Commits contain only explicit paths changed for the named outcome; logs, builds, caches, and other
      seats' work are absent.

---

## 6. Stop conditions and owner handoffs

Stop and ask the owner rather than infer when:

- WO-1137's fallback policy remains undecided;
- WO-1136's pose cannot be judged from captured device views;
- the SKR product model would change from payment rail to held premium currency;
- a gear view in WO-1133 would be cut despite a distinct equipped/loadout use not represented elsewhere;
- enabling Siege or live purchasing would occur without every gate in its numbered WO;
- current source contradicts a ruled number or player-facing promise; or
- completing a ticket requires absorbing unexplained changes already in the working tree.

The handoff should contain the measured fact, the smallest set of choices, the CLI recommendation, and
the player impact of each choice. Do not return an open-ended “what do you want?” when the evidence can
narrow the decision.

---

## 7. What not to touch under this guidance

- Do not delete historical work-order or RESULT files.
- Do not hand-edit `BOARD.html`; regenerate it only after authoritative status changes.
- Do not mint a WO number or alter the numbering banner for this untracked guidance file.
- Do not turn on Siege or real-money purchasing early for testing convenience.
- Do not hand-edit scenes or embed unrelated refactors in visual work.
- Do not replace canonical catalogs with a second runtime catalog.
- Do not commit logs, captures, APKs, Addressables output, caches, or pre-existing unrelated changes.
- Do not claim visual, network, payment, or device behavior complete from compilation alone.

---

## 8. Suggested final report from the CLI

For each numbered WO, report: disposition; exact files changed; captured proof used; gates and markers;
before/after evidence where visual; commit hash; remaining owner felt-verification; and any deliberately
untouched dirty paths. Also report the deprecated/superseded records separately so “not implemented” is
never confused with “forgotten.”

