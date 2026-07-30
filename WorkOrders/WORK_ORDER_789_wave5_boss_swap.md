# WORK ORDER 789 — Wave 5: swap the test apex dragon for a lower Cave Troll boss (1050 HP)

**Status:** READY TO IMPLEMENT
**Lane:** Lane 2 (Combat/AI) — data-driven (waves.json), with a small optional schema/code add
**Type:** EXISTING (a test override left in the data; the boss/wave systems are built)
**Minted:** 2026-07-30 (owner felt-report + screenshot "Syndrath the Devourer 4200/4200" on wave 5, + owner boss choice)
**Author:** UI/RCA seat. CLI implements + gates. PO felt-verifies + closes.

---

## Symptom (owner)

> "level 5 wave spawns the dragon syndrath HP bar but should be a different lower level boss with 1/4 the HP."

Screenshot confirms: wave 5 shows the top-of-screen apex boss bar **"Syndrath the Devourer — Phase I
— 4200/4200"**. The owner wants wave 5 to field a **lower-level ground boss** at **1/4 the HP**
(4200 ÷ 4 = **1050**), not the apex dragon.

**Owner's boss choice (this session):** **Cave Troll** (`troll`, native HP 320), pinned to **1050**.

---

## RCA — proven from the data (not a mystery bug; a TEST override left in)

`Assets/Resources/Data/Canonical/waves.json` `waveId:5` ("Stonebreakers") carries a **self-labelled
test override**:

```
"_comment": "TEST ONLY (2026-07-24) — apex dragon 'Syndrath' bolted onto wave 5 so the owner reaches
             the flying boss fast for testing. REVERT before ship: delete the 'apexBoss' block below
             (the real apex wave is 20). Enemies left intact so the wave still plays; the dragon spawns
             alongside them via WaveManager.SpawnApexBoss.",
"waveId": 5,
"apexBoss": { "id": "boss-dragon-syndrath", "hp": 4200, "nameKey": "bossSyndrath" }
```

- `WaveManager.SpawnWave` reads `wave.IsApexBossWave` → `SpawnApexBoss(wave.ApexBoss)`
  (`Assets/_Modules/Village/Waves/WaveManager.cs:1339-1340`), which instantiates the `Boss_Dragon`
  prefab driven by `DragonBoss` and Configures it to HP 4200 (`DragonBoss.cs:170-173`, `:420-426`).
- `BossHealthBar` (the "Syndrath the Devourer" top bar) is **apex-only**: it auto-discovers a live
  `DragonBoss` and shows for it (`BossHealthBar.cs:8, 45-50, 109`). So removing the apex block removes
  that bar automatically — no bar code to touch.

This override is what the owner is seeing. Wave 20 ("The Last Wing") legitimately declares the same
apexBoss — that is the **real** apex wave and must stay.

---

## Key facts the fix must respect

1. **Ground `boss` field has NO hp override.** `WaveManager.cs:1327-1333` spawns `wave.Boss` as a
   plain batch (`Type = wave.Boss, Count = 1`); HP comes from `enemies.json` (`troll` = 320) and is
   then multiplied by the wave HP-scaling curve. There is no per-wave HP field for a ground boss today
   (only `apexBoss.hp` exists). To land **exactly 1050**, see the two options below.
2. **Boss/apexBoss declarations stay LIVE even under smart-composition.** The WO-362 smart path
   discards authored `enemies[]` batches, but "Only countdownSeconds, boss and apexBoss still take
   effect" (waves.json schema note / `WaveManager.cs:1376`). So editing wave 5's boss IS honored
   regardless of the open `_smartComposition` ruling (WO-783 D1).
3. **A ground boss does NOT get the apex boss bar.** The troll will show the normal enemy health bar
   (floating), not the giant top-of-screen bar. That matches the ask ("a different lower level boss").
   *If the owner later wants a proper boss bar for ground bosses, that is a SEPARATE follow-up —
   BossHealthBar is DragonBoss-only today. Flag it; do not scope-creep it here.*
4. **Edit BOTH copies:** `Assets/Resources/Data/Canonical/waves.json` AND
   `Assets/StreamingAssets/Data/Canonical/waves.json` (they must agree; a data regression checks parity).

---

## The fix

**On `waveId:5` in BOTH waves.json copies:**
1. **Delete** the `apexBoss` block and the "TEST ONLY … REVERT before ship" `_comment`.
2. **Add** a ground boss for the troll pinned to 1050 HP. Two acceptable implementations — CLI picks:

   - **Option A (preferred — reusable, mirrors apexBoss):** give the ground-boss wave path an optional
     HP override. Change `waveId:5` to e.g.
     ```
     "boss": "troll",
     "bossHp": 1050
     ```
     and add `BossHp` to `WaveData`/`WaveDef` + apply it in `WaveManager` when spawning the `wave.Boss`
     batch (set the spawned enemy's max/current HP to `bossHp` and **exempt it from the wave HP-scaling
     curve** so it lands exactly 1050, not 1050×curve). This makes "pin a boss's HP per wave" a general
     tool.

   - **Option B (data-only fallback, zero code):** add a dedicated boss variant to `enemies.json`
     (e.g. `troll-warlord`, HP 1050, based on `troll`, flagged to skip wave HP-scaling if such a flag
     exists) and set `"boss": "troll-warlord"` on wave 5. Avoids touching WaveManager but adds a
     near-duplicate enemy row.

   Confirm which path via the proving step below; either must yield **exactly 1050 effective HP**.

Keep the wave-5 regular enemies intact so the wave still plays around the troll.

---

## Root candidates / proving steps the CLI must run before/after editing (§12)

- **Confirm the HP landing value:** run a headless AutoPilot/wave session to wave 5 and read the trace —
  the troll boss must report **1050/1050 max HP**, NOT 320 and NOT 320×curve. If Option A, verify the
  override bypasses `WaveScalingCurve`; if Option B, verify the variant isn't re-scaled.
- **Confirm no dragon:** wave 5 spawns NO `DragonBoss`; `BossHealthBar` stays hidden (no "Syndrath"
  bar). Grep the trace for `[BossBar]`/`SpawnApexBoss` — should be absent on wave 5.
- **Confirm wave 20 unchanged:** the apex Syndrath still fields at wave 20 (do not regress the real
  apex wave).

---

## Acceptance

- [ ] Wave 5 fields a **Cave Troll** ground boss at **1050/1050 HP**; no Syndrath dragon, no apex bar.
- [ ] Wave 20 still spawns Syndrath (HP 4200) — untouched.
- [ ] Both waves.json copies (Resources + StreamingAssets) edited and in parity; data regression green.
- [ ] Headless wave-5 run trace quoted in the `.RESULT.md` showing troll HP = 1050 and no `DragonBoss`.
- [ ] If Option A: brace/NUL gate passes on `WaveData`/`WaveManager`; `COMPILE_GATE_OK` emitted.
- [ ] Handed to owner for the felt-pass; **PO closes**.

## What NOT to touch

- **Wave 20's apexBoss (the real Syndrath apex wave)** — leave it exactly as is.
- Do not change `troll`'s native HP in enemies.json (it's a brute used elsewhere) — pin HP at the
  WAVE level (Option A) or via a variant (Option B), not by editing the base troll.
- Do not build a new apex bar for the troll here — ground bosses use the normal enemy bar; a
  boss-bar-for-ground-bosses request is a separate WO.
- Do not resolve the `_smartComposition` authority question (WO-783 D1) in this WO — the boss field is
  honored either way.

---

*Notion "Work Orders" DB row — pending (add on a tooled session; NOTION_SOURCE_OF_TRUTH.md).*
