# WORK ORDER 1166 — Echo acquisition has FIVE competing accounts, and Echo Hollow's job depends on which you believe

**Status:** RESOLVED 2026-08-24 (owner). The five competing accounts collapse to ONE: **account #4 is what shipped** — the first Echo is granted with the guide (`TutorialFlow.cs:1610`, `StarterPetSpecies = "ice-wolf"` = Aldwin), the rest arrive at thresholds. The other four are stale comments, not designs.

⛔ **The four files asserting the FALSE exclusivity remain the lesson:** `GameStateService.cs:1005`, `PetSelectController.cs:645` and `:661`, `PetDeployer.cs:140` all state the pet is acquired "ONLY"/"SOLELY" from the Echo Hollow shop, and all four also name a **Yarn** node — and Yarn was fully removed (WO-557). ⚠ **Four independent restatements made a false claim look corroborated**, which is exactly why nobody re-derived it. That is the same one-fact-written-many-times failure as the stale WO-number block, the retired asmdef table, and the 1-of-1 treasury in nine files.

**Echo Hollow keeps its job (Option D, staged):** home + wardrobe. ⚠ Stage 1 is honestly thin and the owner said so — an Echo is barely on screen (`EchoWorldPresence` gives it a body for the escort beat ONLY), so a skin is a portrait swap. Stage 2 — *"Enter echo hollow, separate scene, rooms for echos with 3d bodies, nothing but cosmetic"* — is where it earns the building, and is deliberately unscheduled.

⛔ **Selling an affinity change stays REJECTED** on its own arithmetic: `preferredLaneMatchBonus` 0.03 against `baseContributionPerEcho` 0.02, beside a 0.20 six-set and 0.25 tri-synergy — a rounding error that would delete the assignment decision. Pure appearance yes; anything that moves a number no.

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
| **⭐ D — THE WARDROBE** (owner, 2026-08-24) | Where the six souls LIVE and where you RE-SKIN them. Mechanical enough to justify a building, zero covenant risk, and it unblocks the store's "beauty" half. **Recommended.** |

### ⭐ OPTION D — the Echo wardrobe (added 2026-08-24)

**Owner:** *"Benefit of adding could be skins (cosmetic)… they purchase echo cosmetic"*.

Echo Hollow becomes **the home and the wardrobe**: the roaming area where the Echoes live, and the
surface where you change how they look. It keeps the baked twin
(`EchoHollow_Pets_RoamingArea`), keeps the manage door as a legitimate second doorway, and adds a
verb that is **explicitly sanctioned by the covenant** — *"convenience and BEAUTY, never combat
power."*

⭐ **It also unblocks the largest dark revenue in the store.** The 2026-08-24 monetization pass found
**9 of 13 non-incidental SKUs are hidden for exactly ONE reason: cosmetics do not render**
(`hero-wardrobe-pack`, `realm-defender-bundle`, the three seasonal bundles, `echo-patron-pack`…).
The covenant's beauty half is currently unsellable, so the wardrobe is not a new monetization
surface — it is the missing *destination* for one that already exists.

### ⭐ D IS STAGED, AND STAGE 1 IS HONESTLY THIN (owner, 2026-08-24)

**Owner, verbatim:** *"creating the skins is simple, adds no value other than a portrait card"* ·
*"So far only a player card, but it's there for an idea"* · *"Eventually an area players can go
interact with their echos"* · *"Enter echo hollow — separate scene with rooms for echos with 3d
bodies nothing but cosmetic"* · *"That's future plans but I know that is important to some people"*.

⚠ **The owner is right that Stage 1 barely pays, and the reason is VISIBILITY, not effort.** An
Echo has almost no on-screen life: `EchoWorldPresence` gives it a body for the **escort beat only**,
and that body is **GONE the moment the beat completes** (arrival and vanish fire at the same
lead-clear point — `EchoWorldPresenceRegression`, WO-1108 Lane B). Outside that one walk, an Echo is
a **row in a menu**. So a skin today changes a portrait on a card the player sees while assigning
harvest — real, but small. **Do not price Stage 1 like a hero skin, and do not build a store around
it.**

| Stage | What it is | Value |
|---|---|---|
| **1 — the card** (today) | Portrait swap on the roster card | Thin. Ships as *an idea made visible*, not as a revenue line. |
| **2 — ENTER ECHO HOLLOW** (future) | A **separate scene**: rooms, one per Echo, **3D bodies, purely cosmetic** | ⭐ This is where the skin becomes a thing you can look at — and where the SKU earns its price. |

**Stage 2 is what makes the building.** The door stops being a second route to a menu and becomes a
place you go: your six souls, housed, visible, dressed. That is a flavour-and-place argument that
actually holds — and the owner names the audience precisely: *"important to some people."* It is not
a mass-market driver and should not be justified as one.

⛔ **THE ONE ARCHITECTURAL LANDMINE, flagged now so Stage 2 does not trip it.** CLAUDE.md §7:
**`EchoWorldPresence` is the Echo's ONE appearance owner** — one owner, one lifecycle, no second
spawner, and `PetDeployer.DespawnEcho` is the first despawn path in the game. A new scene that
instantiates its own Echo bodies is **exactly the second spawner that rule forbids**, and it would
be a natural, innocent way to build it. Stage 2 routes its bodies through the existing appearance
owner, or it extends that owner — it never grows a parallel one. Pin it with the existing
`EchoWorldPresenceRegression` before the scene exists, not after.

⚠ Stage 2 is **future plans, deliberately unscheduled.** Recording it here so Stage 1 is built as
its first step rather than as a dead end — the portrait must come from the same cosmetic entry that
a room body will later read.

### ⛔ REJECTED IN THE SAME BREATH: selling an AFFINITY CHANGE

The owner also asked whether an Echo cosmetic could **change its affinity to a desired one**.
**No — and the arithmetic is the argument.** From `echoes-balance.json`:

| Knob | Value |
|---|---|
| `baseContributionPerEcho` | **0.02** |
| `preferredLaneMatchBonus` | **0.03** ← the whole value of "matched" |
| `sixSetBonusGlobalHarvest` | **0.20** |
| `hiddenTriSynergyBonus` | **0.25** |

1. **It sells almost nothing.** A matched affinity is +3 points on a 2-point base, sitting beside a
   **20%** six-set bonus and a **25%** tri-synergy. It is a rounding error in our own balance file.
2. ⛔ **It DELETES A DECISION, which is the real cost.** WO-830 canon: *"the player PICKS each Echo's
   harvest resource — matching that Echo's affinity pays an ADDITIVE MATCH BONUS. Never gate an Echo
   to one resource."* The interesting choice is WHICH Echo tends WHAT, with six unique affinities
   pulling against what you actually need. Sell your way to always-matched and every Echo becomes
   interchangeable — the player has paid to remove the puzzle.
3. It is also the wrong side of the covenant: not combat power, but **buying an advantage**, and
   WO-1165 §1 already records gold-adjacent purchases as the live covenant risk.

**Pure appearance skins: yes. Anything that moves a number: no.** That line is the whole ruling.

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
