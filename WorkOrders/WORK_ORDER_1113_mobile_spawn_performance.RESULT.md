# WO-1113 — Mobile spawn/VFX performance (RETROACTIVE RECORD)

**Status:** DONE — shipped 2026-08-16 in `a24654c21` ("the twenty-lane wave")
**Recorded:** 2026-08-17 (CLI seat), retroactively — see §1 for why this file exists

---

## 1. Why this file exists at all

**This work shipped without ever minting a work order.** The code stamps `WO-1113` across **30 sites**,
but no `WORK_ORDER_1113_*.md` was ever created — so the number was live in the codebase and invisible
to `CLI_LANES_WO_NUMBERS.md`, which is the sole numbering authority (CLAUDE.md §2).

On 2026-08-17 the CLI seat read "next free = 1113" off the banner — correctly, by the rules — and minted
`WORK_ORDER_1113_dungeon_status_field.md` for an unrelated feature. **One number, two meanings.** The
collision was caught the same night by a READY-status audit, and resolved by renaming the *document*
(`1113 → 1114`, `f88a3e0f4` → renamed) rather than the *code*, because 30 code references and a
registered regression oracle are the durable artifact and a WO file with no implementation is not.

> ### ⛔ THE LESSON, AND IT IS THE ONE §2 ALREADY WARNS ABOUT
> **Stamping a WO number into code without minting its file is the same bug as minting without bumping
> the banner** — it puts a number in use that the authority cannot see. The banner is only an authority
> over numbers it knows about. §2 says *"each seat bumps ITS OWN banner row in the SAME edit as the
> mint"*; this is the mirror case: **if you stamp a number in code, the WO file must exist.**
> A number is consumed the moment anything references it, not the moment a file appears.

---

## 2. What actually shipped under this number

Three mobile-performance fixes, all in the 2026-08-16 twenty-lane wave:

- **Spawn budget / concurrency control** — `Assets/_Modules/Village/Waves/WaveManager.cs`
  (lines ~296, 367, 728, 1543, 1909, 1973, 2019, 2135, 2583, 3195)
- **Smart spawner budgeting** — `Assets/_Modules/Village/Enemies/SmartEnemySpawner.cs`
  (lines ~68, 145, 189, 255, 306, 315, 323)
- **VFX pre-warm accounting** — pinned by
  `Assets/Editor/Regression/SpawnBudgetAndVfxWarmRegression.cs`, whose header reads
  *"Pins the three WO-1113 MOBILE-PERFORMANCE fixes"*

The oracle is registered in `DataRegression`, so the behaviour is gate-enforced and cannot silently
regress. That regression suite is the real acceptance evidence for this WO; this file only records
provenance.

---

## 3. Status of the number

- **WO-1113 = mobile spawn/VFX performance** (this file). DONE.
- **WO-1114 = dungeon status field** (the remotely-flippable in-world door state). READY TO IMPLEMENT.
- Banner bumped to **next free = 1115** in the same edit that recorded this.

## 4. Not verified here

Whether this work also satisfies the long-standing READY `WORK_ORDER_51_mobile_performance.md`
(minted 2026-05-28). Plausible but **unproven** — it needs WO-51's acceptance criteria read against
`SpawnBudgetAndVfxWarmRegression`. Flagged, not claimed.
