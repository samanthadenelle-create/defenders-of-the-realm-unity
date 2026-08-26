# WORK ORDER 1207 - Capacity must mean something: a trimmed harvest is REPORTED, and the player is TOLD

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-25 (CLI lead, main line; banner bumped 1207 -> 1209 with WO-1208 in the same edit)
**Silo:** Economy / HUD
**Ruled by:** the owner, 2026-08-25, felt-testing build `2026.08.25.341262`.

---

## Owner rulings, verbatim - and note what she is NOT asking for

> "first time granted me alot but could have been if there was no storage for it, i expect that,
> but id like to warn"
> "otherwise the capacity means nothing"
> "they get a warn on harvest but no warn on battle rewards cause one is choice"

Three rulings, and the third is a SCOPE FENCE:

1. **The loss STAYS.** She is not asking for the clamp to change, for surplus to be held, or for
   capacity to be softened. Overflow at a full store is discarded - the standing clamp-and-warn
   ruling (WO-901 sec.5), and it is what makes capacity a real constraint.
2. **The silence GOES**, and so does a number that was never true.
3. **HARVEST warns. BATTLE REWARDS DO NOT.** Collecting is a CHOICE the player times - she could have
   built storage, spent first, or collected sooner - so the warning is actionable and teaches the cap.
   Battle rewards arrive whether she wants them or not; warning there scolds her for something she did
   not choose, and it fires mid combat-resolution. Do not "improve" on this by warning everywhere: a
   warning the player cannot act on is noise, and noise is how the actionable one gets ignored.

## Proving evidence - captured on the owner's device, 2026-08-25 19:22:33

```
[Flow:Echo] DumpSilos split (pool 17) by harvest weights [W 0/I 3600/F 0/G 0/C 0] -> iron 17
[Flow:Echo] DumpSilos: town bank cap trimmed the dump -- requested W0/I17/F0, applied W0/I0/F0.
            The overflow is LOST (clamp-and-warn).
[Flow:Echo] DumpSilos: banked +0 wood, +0 iron, +0 food, +0 gold, +0 crystals (pool 17)
[Flow:Harvest] collect-all total-banked=17
[Flow:Harvest] ambient collector chip -> CollectAll banked=17
```

Seventeen iron were discarded. **The player was shown 17 as banked. Nothing on screen said a word.**

## Root cause - one line, and the code around it already knows better

`Assets/_Modules/Village/Harvest/EchoService.cs:450` - **`return pool;`**

`DumpSilos` returns the PRE-CLAMP pool. The block directly above it is already correct and documents
why (ECON-SWEEP 2026-08-16, defect 2): it reads `eco.GrantSpendable(...)` back into `applied`,
reassigns `wood/iron/food`, and traces the true `banked +0`. **Then it returns the number it just
disproved.**

Both consumers repeat it faithfully:

- `Assets/_Modules/Village/Buildings/Progression/ResourceCollectorService.cs:29` - `total += echo.DumpSilos();`
- `Assets/_Modules/Village/Buildings/Progression/CollectorStatusPublisher.cs:105` - prints that total.

This is the **WO-978 class** - *"logged the amount requested as though it were credited"* - one layer up.

## What to build

1. **Return what was banked.** `DumpSilos` returns the sum of the APPLIED shares, never `pool`. Gold is
   uncapped and applies in full; the clamped trio come from `applied`. Do not touch the clamp, the
   split weights, or the silo reset.
2. **Tell the player, in words, on the HARVEST path only.** When `applied != requested`, raise the
   EXISTING at-cap sentence - do not author new copy. `BankOverflowToastPresenter.cs:107` already says
   *"{Resource} storage FULL - {Lost} lost. Build or upgrade a {container}, or spend {resource}."*
   One voice for the cap.
   - The over-cap wording (*"above storage ... all of it is yours to spend"*) is a DIFFERENT state and
     must not appear here: at the cap the surplus really was destroyed, so loss language is correct.
     WO-1191 draws exactly this line.
3. **One toast per dump**, naming every trimmed resource - not one per resource per tick, and never a
   repeated scold on each following collect.
4. **Battle-reward grants stay silent** when clamped (ruling 3). If a shared helper is introduced, the
   warning must be opt-IN per call site, so a future grant path cannot inherit a toast by accident.
5. The WO-953 "+N" pops already use the applied shares - leave them; they are the one honest surface
   today.
6. **The VICTORY SUMMARY must show what was banked, and this is NOT a warning.** Ruling 3 says battle
   rewards do not WARN. It does not license the screen printing a number that never landed. Third
   instance of the same class, found the same evening:

   `Assets/_Modules/Village/Arena/BattleArena.cs:3004-3007`
   ```csharp
   econ.Grant(wood: wood, iron: iron);
   summary.Wood = wood;      // <- the REQUESTED amount
   summary.Iron = iron;      // <- never reads back what Grant applied
   FlowTrace.Step("BattleArena", $"GrantWinReward: +{wood} wood, +{iron} iron.");
   ```

   Owner report 2026-08-25, verbatim: *"7 foes killed earned 12 iron?"*. Device trace for that fight:
   `GrantWinReward: +33 wood, +15 iron.` and `SUMMARY xp=238 wisdom=0 wood=33 iron=15 gold=42 kills=7`.
   **The screen said 15 with her iron store nearly full; 12 is what the bank had room for.** Read the
   grant back like `EchoService` already does, and set `summary` + the trace from the APPLIED values.
   Silent is fine. Wrong is not.

## Acceptance criteria

- A registered oracle that dumps a silo into a FULL bank and asserts: the returned total equals the
  CREDITED total (not the pool), and a trim raises exactly ONE player-facing warning naming the
  resource and the lost amount.
- A second case: a clamped BATTLE REWARD grant raises NO player-facing warning - ruling 3 pinned, so a
  later refactor cannot quietly widen it.
- **Proven RED first** - with `return pool;` restored the oracle must fail naming the mismatch; quote
  that red in the RESULT. A pin never seen red is not evidence.
- `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n>` on fresh logs, judged by marker, never exit code.
- A capture showing the sentence, opened and read.

## What NOT to touch

- The clamp, the cap values, `TownBankCapacity`, or the clamp-and-warn ruling.
- `BankOverflowToastPresenter`'s two sentences - reuse, never reword. Copy is the owner's.
- `GrantSpendable`'s contract. It is already honest; the bug is the caller discarding its answer.
