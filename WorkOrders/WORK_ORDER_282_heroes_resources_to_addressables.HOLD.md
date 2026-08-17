> ⚠ **NUMBER COLLISION — this document does not own WO-282; `WORK_ORDER_282_BuildPreviewModal_Premium_Rotation.md` does.**
> Referred to hereafter as **WO-282-C (heroes to Addressables, HOLD copy)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> the two files were added in the **same commit**, so first-on-disk is a tie; ownership decided on **cross-references** (the winner is the file the rest of the corpus cites).
> Banner only — nothing was renumbered or deleted.

# WORK ORDER 282 — HELD (not shipped overnight)

**Status:** BLOCKED — HELD for a daytime, play-verified session. **Not** started.
**Date:** 2026-06-06 (overnight run)
**Decision by:** CLI — flagged per owner's best-practice-pushback standing instruction.

## Why held (not a refusal — a sequencing call)

WO-282 converts the **hero-spawn critical path** from synchronous `Resources.Load`
to **async Addressables** at four runtime call sites:

- `HeroBodySwapper.cs:36/147` — loads the hero prefab + controller **mid-`Start()`**,
  then feeds it straight into `VisualFactory.Skin` → avatar bind → material retarget →
  ability wiring, all inline and assumed ready that frame.
- `AtbCombatantSwapper.cs:92`, `StoryCompanionInjector.cs:179`, `PatriciaLight` ~690.

Reasons this should not land blind overnight:

1. **It's the "does the hero appear at all" path** in village, ATB, story, and DTT.
   A subtle async/handle bug compiles fine but yields **no hero body in every scene** —
   the worst possible regression to discover on the morning of a grant build.
2. **Acceptance is interactive.** WO §6 requires play smoke tests across hero-select →
   village → ATB → PatriciaLight for **all 4 classes**, plus Event-Viewer handle-leak
   checks. None of that is verifiable headless tonight.
3. **It's explicitly non-urgent.** WO header: *Medium priority, not a gameplay blocker,
   sequence after in-flight work.* The base-build-size win is real but not time-critical.
4. **The queue sanctions this.** `OVERNIGHT_QUEUE_2026-06-06.md` STEP 2: *"If the gate
   fails, STOP… Animation half can still land alone."* WO-283 gated clean and landed alone.
5. Substantial **blind editor automation** (create `Heroes` group, `MoveAsset` 4 FBX +
   4 controllers + deps preserving GUIDs, mark-addressable + addresses, `AssetReference`
   wiring on `HeroesGroupConfig`, content build) — many silent failure modes, each needing
   a play check.

## State that's already favorable for the resume

- Addressables 2.9.1 installed; `AddressableAssetSettings` + profiles exist (Heroes would
  **not** be the first group — Localization groups already built).
- UniTask 2.5.10 present (`com.cysharp.unitask`) for the async convention.
- `AddressablesGroupConfig.cs` already has a `HeroesGroupConfig` scaffold (note: its fields
  are stale "Blaise" placeholders — reconcile to Knight/Ranger/Mage/Cleric slugs).
- WO-283 built the 4 controllers in `Resources/Heroes/` as planned — they're the assets
  WO-282 relocates. `HeroAnimatorFactory` output path must be reconciled when they move.

## Recommended resume plan (daytime)

1. Build the `Heroes` group (On Demand / Remote / LZ4 / Pack Separately).
2. Move the 4 FBX + 4 controllers + deps → `Assets/Art/Characters/Heroes/`, mark
   addressable `Heroes/{slug}`, fix `HeroesGroupConfig` to the real slugs.
3. Convert the 4 call sites. **Consider `LoadAssetAsync().WaitForCompletion()` first**
   (keeps control flow synchronous + low-risk; the group can be local-bundled), then
   migrate to true `await` once a play session confirms heroes still spawn.
4. Reconcile `HeroAnimatorFactory` controller output path (no longer Resources).
5. CompileGate → Addressables content build → player build → **play-test all 4 classes**.
6. Commit, write RESULT, close the Linear issue.
