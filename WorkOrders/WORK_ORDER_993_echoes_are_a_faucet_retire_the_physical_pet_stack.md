# WORK ORDER 993 — Echoes are a FAUCET: retire the physical pet stack

**Status:** FIXED — AWAITING OWNER FELT-TEST TO CLOSE. Prior status: DONE 2026-08-16 (`b63bc7190`) — RESULT filed; pending PO felt-verify
**Minted:** 2026-08-14 (CLI)
**Silo:** Echoes / harvest / pets
**Source:** OWNER RULINGS, 2026-08-14

---

## The rulings

> *"we dont use the pet aura anymore since we descoped them to simply helpers and not physical items around us"*
> *"same with pet progression"*
> *"auracontroller can be retired"*
> *"pet leash gone too"*
> *"we are not doing the animation, faucet only"*

Confirmed against two explicit questions:

- **Echo harvest = FAUCET ONLY, no animation.** Echoes are assigned in a panel and resources accrue.
  **Nothing walks to a node on screen.**
- **The founding Echo's floating-spirit look RETIRES TOO.** `EchoSpiritPresentation` goes.

**Echoes are a SYSTEM, not a companion.** That is the whole ruling, and every item below follows from it.

> ## ⛔⛔ SCOPE CORRECTION — OWNER, 2026-08-14: *"we use the wolf fbx as the echo guide"*
> **THE DESCOPE APPLIES TO HARVEST ECHOES, NOT TO THE ECHO GUIDE.** There are TWO kinds of Echo and
> they take opposite dispositions:
>
> | | Echo **GUIDE** (the wolf) | Echo **HARVESTERS** |
> |---|---|---|
> | Physical presence | **YES — keep** | **NO — faucet only** |
> | Walks in world | **YES — keep** (`PetHeroLeash` drives it) | **NO — retire the walk** |
> | Purpose | leads the player through the founding arc / FTUE | ticks resources into the bank |
>
> **`PetHeroLeash` THEREFORE STAYS.** The earlier ruling *"pet leash gone too"* is superseded by this
> one — the leash is what makes the wolf guide move, and the guide is staying.
>
> ⚠ **THE GUIDE HAS NO OTHER BODY.** `BlankStartCensusRegression.cs:42-43` states it outright:
> *"A blank start now seats no founding NPC at all — **the guide's only body is the wolf the founding
> arc summons**"*. There is no fallback NPC to fall back to. Break the wolf and the founding arc summons
> a guide that cannot lead.
>
> ⚠ **A DEDICATED SUITE PROTECTS IT:** `FoundingGuideWolfBodyRegression` `[founding-guide-wolf]`,
> registered at `DataRegression.cs:650`. Any retirement that reds this suite has gone too far — treat it
> as the fence, not as an obstacle.
> Asset: `Assets/Animals/Low Poly Animals/Simple Wolf/wolf.fbx` (+ `Prefab/wolf.prefab`).
> Not to be confused with `Assets/Art/Retired/Pets/ice-wolf-fox-legacy.fbx` (retired) or the Blink
> `WolfSet_*` ARMOUR sets (unrelated, gitignored).
>
> **What `EchoSpiritPresentation`'s retirement now means:** the wolf guide **keeps its body and its
> movement**, and loses the ethereal layer — the hover, the slow yaw drift, and the `Aura_HeartPulse`
> glow. It becomes a grounded wolf that walks, rather than a floating spirit. That is the whole delta.

## ⛔ THE ONE THING THAT MUST NOT BE MISSED

> ### `PetHeroLeash` IS THE TUTORIAL GUIDE LEAD — and per the correction above it **STAYS**.
> The detail below is retained because it documents WHY it is load-bearing. Do not delete it.

47 non-self references. The pet plumbing was **repurposed** into the thing that walks the player through
onboarding:

```
 6  TutorialFlow.cs                      <- SetLeadTarget: the guided walk
 5  GuideLeadMovementRegression.cs
 2  TutorialWorldAnchors.cs              <- WO-962's anchor latch feeds this
 1  TutorialSignals.cs
 1  TutorialStepReachabilityRegression.cs
 4  StoryCompanion.cs
```

**Removing the leash is not a delete — it is an FTUE change.** The guided walk must be given a
replacement lead, or the step must be removed, **in the same change**. Deleting the symbol and leaving
the tutorial step standing produces a step that stops leading while the gate stays green — a dead FTUE
that nothing reports. Two regressions cover this and will need rewriting rather than deleting, or the
coverage vanishes with the feature.

⚠ Note WO-962 landed a `guide_gate` anchor **latch** today whose whole purpose is feeding
`SetLeadTarget`. Whatever replaces the lead must keep that latch meaningful or WO-962 becomes moot.

## ⛔ THE SECOND THING: THE FAUCET MUST KEEP PAYING

`PetHarvester` (WO-229) currently banks yield **on arrival at a node**:

> *"A deployed pet, when no enemy needs fighting, autonomously walks to the nearest resource node,
> harvests it on a tick, and the yield is banked into the EXISTING economy… reuses the Village MineNode
> — its `TryAutoExtract()` already banks one extract into GameState on the node's cooldown, the SAME
> path `Worker.cs` and the player's [F] tap use. The pet adds NO new currency and NO new banking path."*

Removing movement removes the **trigger**, not the banking. So:

- **KEEP** the banking path exactly as-is — `MineNode.TryAutoExtract` into `GameState`. Do **not**
  invent a second economy path; that constraint is the reason WO-229 was accepted in the first place.
- **REPLACE** the arrival trigger with a **tick**, driven by the Echo's panel assignment
  (`<resource>:<level>` token grammar, WO-830) rather than by proximity to a node.
- ⚠ **Open question for the owner (does not block the retirement, does block the tick design):**
  does a faucet Echo still bind to a specific MineNode, or does it credit its assigned resource with no
  node at all? The Manage picker assigns a **resource**, not a node, which points at "no node binding" —
  but that is an inference, not a ruling. **Ask before implementing the tick.**
- **Echo affinity stays a MATCH BONUS, never a lock** (WO-830, binding): the player picks each Echo's
  harvest resource; matching its affinity **doubles** yield. Maren harvests Crystals, not Repairs.

## Retire (by SYMBOL, never by folder)

| Symbol | Evidence it is safe to retire |
|---|---|
| `AuraController` | 1 non-self ref (`GearAura.cs:8`). Never wired: GUID appears only in its own `.cs.meta` |
| `PetAuraVFX` | 1 non-self ref, and it is a **comment** (`ParticlePackVfxBatchBuilder.cs:1055`) |
| `PetBrain` | 2 refs, **both inside `AuraController`** — they go to zero with it |
| `EchoSpiritPresentation` | Owner ruling: retire. Orphans its `PetDeployer.SummonAt` attach site |
| Pet **progression** (level-scaling surface) | Owner ruling: descoped |
| ~~`PetHeroLeash`~~ | ⛔ **KEEP — REVERSED by the wolf-guide correction above.** It drives the guide's walk |
| `PetHarvester` **movement half** | Retire the walk-to-node steering. **Keep the banking half** (see above) |
| ~~`Pet.cs` locomotion~~ | ⛔ **KEEP the locomotion the WOLF GUIDE uses.** Retire only the harvester's steering — the guide and the harvesters share `Pet.cs`, so this is a **per-caller** retirement, not a per-file one |

**Also orphaned, handle explicitly rather than leaving dangling:**
- `Aura_PetLevel1/2/3` catalog keys — remove or mark orphaned in the VFX wiring map.
- The pet registrant in `VfxAuraProximityCuller` (`PetAuraVFX.cs:132`) — a dead branch in the nearest-N ring.
- `PetClipPlayer` (5 refs, all in `PetDeployer`), `PetIdleRoutines`, `PetBillboard`, `EchoAutoDeployTrigger`,
  `MineNodeBridge` — assess each; several exist only to serve movement/animation.
- ⚠ **`Aura_HeartPulse` is NOT orphaned** — the Heart of Elarion keeps its own use. Only the Echo's use goes.

## ⚠ Void this acceptance criterion

`WORK_ORDER_128_pet_anti_ranged_ability.md:394/402` lists **"WO-58 aura — BUILT — DO NOT BREAK"** as an
acceptance criterion. It has been protecting something that **never ran**:
`WORK_ORDER_58.RESULT.md:38-43` claims `PetProgression` calls `SetLevel` / `PlayLevelUpBurst`; **neither
call exists at HEAD.** An archived doc caught this on 2026-05-30, called it *"PARTIAL/UNWIRED"*, and it
was never closed. Add it to `docs/reference/HOLLOW_ASSERTIONS_REGISTRY.md`.

## Acceptance criteria

- Every retired symbol is **gone**, and every regression that covered it is **rewritten or deliberately
  deleted with a stated reason** — never left asserting a feature that no longer exists.
- **The FTUE guided walk still works, or its step is gone.** Prove with a run, not by reading the code.
- **The harvest faucet still pays.** Prove with a captured before/after resource delta — not a trace line
  saying it ticked. §1.4b: a line that prints the same whether or not resources moved proves nothing.
- No new economy path introduced. The banking still goes through `MineNode.TryAutoExtract`.
- `COMPILE_GATE_OK` and a full regression pass after the deletions — removing a type can break a
  reference a name grep missed (Unity serialises by GUID; check `.cs.meta` guids across `.unity`/`.prefab`
  before deleting anything with a scene presence).

## What NOT to do

- ⛔ Do **not** delete by folder. `_Modules/Pets` contains both retired and live code.
- ⛔ Do **not** delete `PetHeroLeash` before the FTUE lead has a replacement or the step is removed.
- ⛔ Do **not** invent a second banking path for the faucet.
- ⛔ Do **not** touch `Aura_HeartPulse` on the Heart of Elarion.
- ⛔ Do not strip any `FlowTrace` from surviving files (CLAUDE.md §12, BINDING).
