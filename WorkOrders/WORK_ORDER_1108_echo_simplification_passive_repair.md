# WORK ORDER 1108 — Echo simplification: auto-harvest, count-driven passive repair, escort-then-vanish

**Status:** READY TO IMPLEMENT (owner-approved 2026-08-16)
**Lane:** Echo/harvest (`EchoBonusCalculator`, `EchoAssignments`, `EchoCardVM`) + pet world-presence
(`PetDeployer`, `PetHeroLeash`, `TutorialFlow`). ⚠ Two disjoint silos — see §5.
**Minted:** 2026-08-16 (CLI seat) — banner bumped 1108 -> 1109 in the SAME edit.
**Provenance:** owner, verbatim:
> *"The only thing that should happen for the pet or the echo is it takes you to the gate, gives you
> your dialogue, then it disappears... The only time it reappears is after your battle... What if we
> simplify it? Where they automatically just harvests. You choose what it chooses to harvest, and the
> number of pets that we have just passively takes towards healing. Does that work better?"*
> — then, on the recommendation: *"Get it done. Do it that way. That's fine with me."*

---

## 1. ⚠ READ THIS FIRST — two thirds of the ask is ALREADY BUILT

Source-verified before writing this spec. The request decomposes into three parts and they are in
**wildly** different states, so the naive reading ("build the Echo simplification") would greenfield
a system that already ships.

| Ask | State | Work |
|---|---|---|
| (a) Echoes auto-harvest, player picks the resource | **BUILT END TO END** | ~none |
| (b) Echo COUNT passively drives repair | **NEW, but tiny** | one function |
| (c) Escort to gate -> dialogue -> vanish -> reappear once after battle | **HALF built** | the vanish/reappear half is new |

### (a) is already the shipping behaviour — do not rebuild it

- The picker offers exactly **5 resources** (`EchoAssignments.PickableResources`
  `Assets/_Modules/Village/Harvest/EchoAssignments.cs:95`) and `PickableLanes = { LaneHarvest }` (`:91`).
- Accrual is **automatic every frame** — `EchoService.Update()` adds `RatePerSecond * deltaTime` into
  the silo (`Assets/_Modules/Village/Harvest/EchoService.cs:293-301`).
- Yield routes to the **assigned** resource via `EchoBonusCalculator.HarvestTargetWeights()`
  (`EchoBonusCalculator.cs:157-195`).

⚠ **The ONE real gap vs the word "automatically":** banking the silo is a **manual tap**.
`EchoService.DumpSilos()` (`:329`) is driven by `ResourceCollectorService.CollectAll()`
(`ResourceCollectorService.cs:27-29`) from `EchoWorkforceHud.cs:154`. The only auto-claim is
`AutoHarvestService` (`:41-51`, every 20 s) and it is **perk-gated on `ModifierService.Active.AutoCollect`**.
**This is an OWNER DECISION, not an implementation detail** — see §4 D1. Do not silently ungate a perk
the economy is tuned around.

## 2. ⚠ CANON CORRECTION — "affinity DOUBLES the yield" is FALSE against live tuning

`CLAUDE.md` §7 states the WO-830 rule as *"matching that Echo's affinity **doubles** the yield."*
**The shipped numbers do not do that**, and a seat implementing to the canon sentence would ship a
~20x buff.

The match bonus is **additive inside a spec-sum**, not a multiplier:
`LaneContribution = BaseContributionPerEcho + (match ? PreferredLaneMatchBonus : 0) + PerLevelBonus*(level-1)`
(`EchoBonusCalculator.cs:410-417`). Live values (`Assets/Resources/Data/Canonical/echoes-balance.json:5-9`):
`baseContributionPerEcho = 0.02`, `preferredLaneMatchBonus = 0.03`, `perLevelBonus = 0.01`.

So a matched Lv1 Echo contributes **+5% vs +2%** — 2.5x the per-echo *term*, roughly **+3% absolute**
on the aggregate. That matches the owner's own ruling recorded in that file's `_authoringNotes:3`
(*"+5% not 55%"*). The `0.75` default in `EchoBalanceCatalog.cs:51` is dead — the json overrides it.

**The behaviour is correct and the DOC is wrong.** Fix the CLAUDE.md sentence to say *match bonus*, not
*doubles*, in the same commit. The "never a lock" half of the rule is intact and verified (any chip is
tappable — `EchoCardVM.cs:229-233`).

## 3. The work

### (b) Count-driven passive repair — the whole change is one function

Today repair is an **assignable lane**: `EchoRepairService` (a MonoBehaviour, `Update()` at `:254-259`
on a 1.5 s `ScanInterval`) banks damage **fractions** and spends them most-damaged-first through
`WallRepairController.TryRepairWorst` (`:319`). Its ONLY rate input is
`EchoBonusCalculator.RepairFractionsPerSecond()` (`:303-316`), which iterates Echoes **filtered to
`lane == Repair`**.

**Change:** drop the lane filter — sum over **every owned Echo** (`EchoService.Instance.EchoCount`).
`EchoRepairService` needs **no change at all**; it reads nothing else.

⚠ **DEFAULT I AM TAKING, and the owner should overrule it if wrong:** the per-Echo term keeps its
**level** component (`PerLevelBonus*(level-1)`). Her words were *"the number of pets"* — a strict
reading is count-only, which would make Echo levels worthless for repair. Keeping level preserves the
existing progression axis and is the smaller change. **Flagged, not buried** — §4 D2.

**Retiring the repair chip** (it is now meaningless — every Echo repairs):
- `EchoAssignments`: keep `LaneRepair` and `NormalizeToken`'s handling (`:378`) for **READ
  COMPATIBILITY** — saved `repair:N` tokens exist in the wild. A stored `repair:N` must migrate to a
  harvest resource on read, never crash and never silently become `idle` (which would zero that Echo's
  yield). **No schema bump** — this is the same read-migration pattern WO-830 used.
- `EchoCardVM`: remove `RepairTaskChip` from `TaskChips` (the card then offers 5 resources, no 6th).
- `EchoRepairStatus.NoneAssigned` (`EchoRepairService.cs:62-72`) becomes **unreachable** — every Echo
  repairs, so the only honest zero-states are `NothingToRepair` / `WaitingMaterials`. Do not leave a
  status the code can no longer produce; delete it or make the enum's dead value fail loudly.

⚠ **A DUPLICATE, UNRELATED REPAIR LOOP EXISTS** and must not be confused with the above:
`PetTaskController.TickRepair()` (`Assets/_Modules/Village/Pets/PetTaskController.cs:123-157`) drives
`WallRepairController.RepairAll()` for a **world pet** whose `PetTask == Repair`. After WO-1031 deleted
the engage prompt, `PetTaskController.Update()` does **exactly one thing** — this loop (`:80-86`) — and
`PetTaskInstaller` (`:186-223`) still bolts the component onto every spawned pet once a second. **Once
repair is passive, this loop is a second uncoordinated repairer of the same walls.** Retire it and let
`PetTaskInstaller` stop attaching a husk, or state why it must stay.

### (c) Escort -> dialogue -> vanish -> reappear once after battle

**The escort half already exists and is data-driven — reuse it, do not re-author it:**
- Beat 2/8 in `Assets/Resources/Data/Canonical/tutorial/tutorial-steps.json:27-31` — objective
  *"Follow {guide} to the gate"*, highlight `world.guide` + `world.gate_direction`, completion
  `hero.reached:guide_gate`.
- The lead seam is `PetHeroLeash.SetLeadTarget(Vector3)` (`Assets/_Modules/Pets/PetHeroLeash.cs:145`,
  `LeadArriveRadius = 2.2f` `:105`), re-asserted each frame from `TutorialFlow.TickProximityProbe`
  (`:1731`), cleared at `TutorialFlow.cs:472` / `:1234` / `:1270`.
- Exactly ONE Echo ever gets a world body, and only for this beat —
  `TutorialFlow.ApplyStarterPetGrant` -> `deployer.SummonAt(birthPos)` (`TutorialFlow.cs:1458`). Roster
  Echoes are portrait cards (`:1416-1420`). **This is consistent with the standing ruling that the
  wolf body is a one-time FTUE device and every later beat is a dialogue screen.**

**What is genuinely new: there is NO despawn path for a pet, anywhere.** A grep over the pet stack
returns no despawn/recall/destroy. `EchoAutoDeployTrigger`'s header says so outright — *"The Echo
PERSISTS — it is never despawned here"* (`Assets/_Modules/Village/World/Camps/EchoAutoDeployTrigger.cs:11-13`),
guarded by a static `s_summonedThisSession` (`:51`).

**Build:** a despawn verb on `PetDeployer` (mirroring `SpawnPet` `:409`), fired when beat 2/8 completes
— i.e. at the existing `SetLeadTarget` clear point, so arrival and vanish are the same event and cannot
disagree. Then a **single** re-summon after the first battle resolves, using the same
once-per-session static guard idiom `EchoAutoDeployTrigger` already uses, so it cannot re-fire.

⚠ **`EchoAutoDeployTrigger` summons the Echo at an enemy outpost once per session and is a SECOND
appearance seam.** Reconcile it with the new reappear rule or the Echo will show up twice by two
different owners. Do not add a third.

## 4. Owner decisions this WO does NOT presume

- **D1 — the auto-claim perk.** Silo accrual is automatic; **banking is a manual tap** unless the
  `autoCollect` capstone is owned. Does *"they automatically just harvest"* mean the tap goes away for
  everyone (which retires a perk the economy is tuned around), or does it mean today's behaviour and
  the perk stands? **Defaulting to: perk stands, no change.**
- **D2 — count only, or count x level?** Defaulting to **count x level** (§3). Count-only makes Echo
  levels worthless for repair.
- **D3 — the repair rate is currently a CODE DEFAULT.** `repairFractionPerHour = 2f`
  (`EchoBalanceCatalog.cs:74`) is **absent from `echoes-balance.json`**. Making repair passive across
  every Echo multiplies the aggregate by the roster size — at 6 Echoes that is a **6x** repair rate on
  a number that was never authored in data. It should be tuned and moved into the json, and the tuning
  is a balance call, not mine.

## 5. Silos — these two lanes are file-disjoint, run them in parallel

- **Lane A (economy):** `EchoBonusCalculator.cs`, `EchoAssignments.cs`, `EchoCardVM.cs`,
  `EchoRepairService.cs`, `echoes-balance.json` (BOTH copies — Resources wins at runtime,
  StreamingAssets is the fallback, byte-identical), `CLAUDE.md` §7 (the §2 correction).
- **Lane B (world presence):** `PetDeployer.cs`, `PetHeroLeash.cs`, `TutorialFlow.cs`,
  `EchoAutoDeployTrigger.cs`, `PetTaskController.cs` + `PetTaskInstaller`.

## 6. Regression — UPDATE these, do not duplicate

- `EchoSpecializationRegression.cs` — owns the **token grammar round-trip including `repair:N`**
  (`:12-26`) and drives `EchoService.DumpSilos()` live (`:612`, `:638`). Any grammar change breaks this
  FIRST. It must be rewritten to assert the **read-migration** of a stored `repair:N`, not its absence.
- `EchoResourcePickerRegression.cs` — group 6 asserts `TaskChips` is *5 resources + "Repair structures"
  LAST + `AssignRepair` persists* (`:11-26`). That assertion **inverts**: the chip must be gone and a
  re-add must fail.
- `EchoEngageDialogueRegression.cs` — already inverted by WO-1031; leave it.
- EditMode: `Assets/Tests/EditMode/EchoRepairTaskTests.cs` asserts the repair label, the safe reject,
  that the chip is never "preferred", and an honest-zero rate. Its first three cases die with the chip;
  **the honest-zero case must survive in a new form** (zero damaged structures still means zero accrual).
- **New assertion required:** passive repair scales with `EchoCount` — an Echo added with no assignment
  change must raise `RepairFractionsPerSecond()`.

## 7. Acceptance

- With N Echoes and NO repair assignment, damaged walls repair; the rate rises when an Echo is added.
- A save containing `repair:3` loads, does not crash, and that Echo harvests something real (not idle) —
  proven by a regression case, not by inspection.
- The Echo leads to the gate, delivers its dialogue, and is **gone** from the world afterward; it
  reappears exactly once after the first battle and never again that session.
- The card offers 5 resources and no repair chip; re-adding the chip fails the suite.
- `CLAUDE.md` §7 no longer says affinity "doubles" the yield.
- Owner felt-verify on device — appearance/disappearance is a felt beat and headless cannot judge it.
