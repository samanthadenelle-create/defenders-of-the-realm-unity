# WORK ORDER 1166 — Echo acquisition has FIVE competing accounts, and Echo Hollow's job depends on which you believe

**Status:** READY FOR OWNER RULING. No code change until §5 is answered — the question is *which model is real*, and every implementation follows from that.

**Minted:** 2026-08-24 (CLI), banner bumped 1166 → 1167 in the same edit.
**Provenance:** the owner asked a simple question — *"what does echo hollow provide? A second entrance to manage echo screen?"* — and the honest answer turned out to be **"it depends which of five models you believe."**

---

## 1. The five accounts, each verified at source

| # | Source | Says |
|---|---|---|
| 1 | `GameStateService.cs:1005` | *"the pet is acquired **ONLY** from the Echo Hollow pet-shop (PetHouse Yarn node → `<<spawn_named_pet>>` → `PetAcquisitionService.Acquire`), **never pre-granted**"* — owner ruling 2026-06-13 |
| 2 | `PetAcquisitionService.cs:19-22` | *"**THE THREE ACQUISITION PATHS**: 1. Tame — won the bond mini-game. 2. Hatch — an egg's care timer finished. 3. Rescue — freed a caged beast."* **No shop mentioned at all.** |
| 3 | WO-587 / `GameState.PopulationEchoSlots` | Milestone-driven **slot** unlocks, seeded at 1; `IPopulationService.EchoSlotsUnlocked` is read by HUD + VMs |
| 4 | **Owner, 2026-08-24** | *"we now have the echo granted as the guide and others arrive at thresholds"* |
| 5 | `dialogues.json:251,277,303` | An **"Echo Warden"** NPC with `grant_ice` / `grant_flame` / `grant_aether` nodes, live, granting `spawn_named_pet` |

**Five models of one mechanic, layered as the design moved, none retired.**

## 1b. ⭐ LARGELY RESOLVED 2026-08-24 — the OWNER'S MODEL IS THE ONE THAT SHIPPED

A stale-comment sweep settled most of this. **Account #4 — the owner's current design — is what the
code actually does**, and the comments describing the others are simply false.

**PROVING LINE:** `Assets/_Modules/Village/Tutorial/V2/TutorialFlow.cs:1610`
```csharp
acquiredNew = petSvc.Acquire(StarterPetSpecies, PetAcquisitionSource.Starter);   // grant.starterPet
```
with `StarterPetSpecies = "ice-wolf"` (`:1498`) — and ice-wolf is **Aldwin, the Ice Echo**, matching
the owner's own device readout *"Echo 1 of 1 — Aldwin, the Ice Echo"*. **The first Echo is granted
with the guide, exactly as ruled.**

### So the accounts collapse:

| # | Verdict |
|---|---|
| **#4 owner's model** — granted as guide, rest at thresholds | ✅ **TRUE — implemented** |
| **#1** "ONLY from the shop, **never pre-granted**" | ❌ **FALSE, and it is asserted in FOUR files** |
| **#5** Echo Warden shop nodes | ⚠️ **live but now a SECOND path**, not the only one |
| **#2** Tame / Hatch / Rescue | ⚠️ the grant primitive is real; the three named paths need confirming |
| **#3** threshold slot unlocks | ✅ compatible — slots and ownership are different axes |

⛔ **THE FOUR FILES ASSERTING THE FALSE EXCLUSIVITY** — this is why it read as settled fact:
`Core/State/GameStateService.cs:1005` · `Onboarding/PetSelectController.cs:645` and `:661`
(*"acquired **SOLELY** from the Echo Hollow pet-shop"*) · `Pets/PetDeployer.cs:140`.

⚠ **Four independent restatements make a false claim look corroborated.** Nobody re-derives a fact
that four files agree on — which is exactly how this survived. All four also name a **Yarn** node,
and Yarn is FULLY REMOVED (WO-557); the verb survived into `DialogueCommandSink.cs:310` →
`Acquire(species, PetAcquisitionSource.Gift)`.

### What is still genuinely open

1. **Are the Echo Warden's three grant nodes still wanted?** They are a live second acquisition path
   handing out species (`ice-wolf` / `flame-pup` / `aether-sprite`) that the threshold model does not
   need. Keep, or retire?
2. **§5 below still stands: what is Echo Hollow FOR?** With acquisition proven to come from the
   guide-grant and thresholds, the Hollow's mechanical justification is gone regardless of how (1)
   is answered.
3. Correct the four false comments in whichever direction (1) settles — **do not "fix" them by
   asserting the shop is the only path**, which is the falsehood being retired.

## 2. Two extra wrinkles inside account #5

⚠ **The comment says "Yarn node" and YARN IS FULLY REMOVED** (WO-557). The *verb* survived the
migration — `spawn_named_pet` now lives in the JSON dialogue system — so the mechanism still runs,
but the sentence describing it names a deleted engine. A reader checking it would conclude Echo
acquisition is broken. It isn't; the comment is.

⚠ **The dialogue grants SPECIES; the roster is NAMED SOULS.** The Warden hands out `ice-wolf`,
`flame-pup`, `aether-sprite`. The Echo roster is six named people — Aldwin, Elowen, Corvin, Bran,
Doran, Maren (`EchoRosterCatalog`) — and canon is explicit that **an Echo is the awakened essence of
a person the Heart guards**, not a pet species. The owner's own device shows *"Echo 1 of 1 —
**Aldwin, the Ice Echo**"*, so a mapping clearly exists, but two vocabularies are live at once and
the shop speaks the older one.

## 3. ⛔ WHY THIS MATTERS BEYOND TIDINESS: it decides whether a BUILDING has a job

**Echo Hollow's entire mechanical justification is account #1.** If Echoes are *bought there*, it is
a gate on the whole workforce pillar: no Hollow → no Echoes → no auto-harvest, no offline income, no
roster. That is load-bearing.

**Under account #4 — the owner's current design — nothing is bought there.** The first Echo arrives
with the guide; the rest unlock at thresholds. So the Hollow is left with:
- its **roaming area** (baked twin `EchoHollow_Pets_RoamingArea`) — where they live
- a **second door to the manage screen** — which is exactly what the owner suspected

That may still deserve a building, but on a *flavour and place* argument, not a mechanical one — and
it should be chosen, not inherited from a ruling that no longer describes the game.

⚠ It also has FTUE weight: `TutorialHighlightRegistry` highlights `build.card.pet-house` by name,
and the founding flow seeds a pet-house signature into `BaseLayout`. A building the tutorial teaches
you to place should have a reason that survives the next question about it.

## 4. What is NOT in doubt

- `PetAcquisitionService.Acquire` is the **one grant primitive** and is clean — roster + OwnedPets +
  auto slot assignment, with gating explicitly the CALLER's job. Whatever model wins, it funnels here.
- **Slots and ownership are different axes.** `PopulationEchoSlots` unlocks *how many can be
  deployed*; acquisition decides *which you own*. Accounts #3 and #4 are not necessarily in conflict
  — a threshold could unlock a slot, a grant could fill it. Do not collapse them by accident.
- Save state is sound: `PetActiveSlots` (v34) round-trips, `SyncSlotsFromState` rebuilds on load.

## 5. ⛔ THE RULING NEEDED — one question, everything follows

**Which model is real, and what is Echo Hollow for?**

| Option | Echo Hollow becomes |
|---|---|
| **A — the home** | Keep it: the roaming area is real, it puts life in the town, and the manage door is a legitimate second doorway to one destination. Flavour justification, honestly labelled. |
| **B — the threshold surface** | It becomes where you SEE what unlocks next and CLAIM an Echo when a threshold trips. Gives it a mechanical verb again without reviving the shop. |
| **C — retire it** | If Echoes arrive by grant and threshold, a building with no verb is a tile occupying a palette slot. ⚠ Its id is a frozen save key — retire from the PALETTE, never delete the row. |

**Then, whichever wins:**
1. Retire the three accounts that lose. Delete the "ONLY from the Echo Hollow pet-shop" sentence or
   correct it; the Tame/Hatch/Rescue header either describes reality or it does not.
2. Decide the fate of the **Echo Warden's three grant nodes** — live today, granting species.
3. Reconcile **species ids vs named souls** into one vocabulary.
4. Fix the **Yarn** reference regardless of which option wins — it is false under all five.

## 6. The pattern, for the record

This is the same failure as the naming cluster (WO-1161), the crossed perk ladders (WO-1163 §4d) and
the half-shipped go-live: **a decision was made, a newer decision replaced it, and the older one was
never retired.** Nothing here is broken — it is over-specified, which is worse, because every
statement is individually defensible and collectively meaningless. The cost is that a simple
question about one building took five source reads to answer.
