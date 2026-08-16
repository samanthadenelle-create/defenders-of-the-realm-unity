# RESULT — WO-993 retire the physical pet stack (aura, progression, spirit layer)

**Date:** 2026-08-16  **Seat:** CLI (commit `b63bc7190`)
**Status:** DONE — pending PO felt-verify

Owner rulings: *"we dont use the pet aura anymore since we descoped them to simply helpers and not
physical items around us"*, *"same with pet progression"*, *"auracontroller can be retired"*.

Scoped against the WO's **own correction** — harvest Echoes are faucet-only, the GUIDE keeps its body,
and `PetHeroLeash` **STAYS** (the WO's earlier "leash gone too" is superseded by its own correction; the
leash is what makes the wolf guide move).

## What shipped

**Deleted:** `Village/Pets/AuraController.cs` (its only non-self reference was a comment),
`Pets/PetProgression.cs` (all three `AddComponent` sites tombstoned; `Pet`'s progression multiplier fields
and their sole-caller setter removed), `Harvest/EchoSpiritPresentation.cs`.

⚠ **THE SPIRIT LAYER WAS ALREADY DEAD — trust the code over the WO.** `EchoSpiritPresentation` had ZERO
live callers before this lane started; WO-961 removed its attach on 2026-08-10 and
`FoundingGuideWolfBodyRegression` has been ASSERTING ITS ABSENCE since. So item 3 was a file deletion plus
two comment fixes — **nothing about the wolf's look or movement changes at runtime.** The owner needed to
know this: **no visual decision was pending after all.**

## Deliberately NOT done — kept, with reasons written into the code

- `XpEarnerRegistry` / `IXpEarner` / `ProgressionManager` — `HeroProgression` is now the sole earner and
  they are the cross-asmdef seam.
- `FeatureFlags.PetCombat` — still gates `Pet` hunt/attack and `PetHarvester`, both pinned by
  `GuideLeadMovementRegression`.
- `PetData.damagePerLevel` / `hpMultiplierPerLevel` — now unread but **SERIALIZED ON SHIPPED `.asset`
  FILES**. Removing them is a data migration, not a code retirement, and doing it quietly is how a save
  breaks.
- `Aura_HeartPulse` and its Hovl row — two live consumers. A warning is written in so the next reader does
  not delete the row on the strength of this retirement.

## Gap closed that Lane B (WO-1108) opened

⚠ `FoundingGuideWolfBodyRegression` case (d) watched only `TutorialFlow` for the spirit layer, but WO-1108
Lane B moved the spawn one hop to `EchoWorldPresence` — so a re-added
`AddComponent<EchoSpiritPresentation>` at the NEW site would have re-attached the hover with the case
still green. It now scans both. **WIDENED, not weakened**; no assertion was re-pointed to make anything
pass.

## Verification

Guide proven intact by replaying all five `[founding-guide-wolf]` case-5 assertions against the tree:
species `ice-wolf`, summon-before-`Acquire` ordering holds, ZERO unguarded `SummonAt` statements, no Sylas
stand-in. Gate at the time: **171/176**, the 5 reds all pre-existing.

Canon updated in the same breath (§15): `docs/MASTER_CATALOG/misc-modules.md`,
`docs/MASTER_CATALOG/village-systems.md`, `docs/reference/VFX_AUDIO_WIRING_MAP.md`, both module READMEs.

## Owner decision left open

None outstanding — the one that looked open (the Echo's visual layer) turned out to be moot, see above.
