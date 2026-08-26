# HANDOFF - 2026-08-26 evening (CLI seat)

## THE ONE THING THE NEXT SEAT MUST DO FIRST

**GATE THE TREE.** Eight-plus lanes of finished, brace-verified code sit UNCOMMITTED.
Unity is CLOSED and the runner's guard is clear (verified 13:39). The only reason it
has not been gated is that agents were still writing.

```
.un-unity-method.ps1 -Method DeNelle.Editor.CompileGate.Run -LogName gate-compile-1
.un-unity-method.ps1 -Method DeNelle.Editor.DataRegression.RunAll -LogName gate-reg-1
```
Judge by MARKER on a FRESH log, never the exit code (memory: gates-report-success-without-proving-it).

### A RED PROOF IS OWED BEFORE THE GATE (WO-1233)
The battle-lock agent could not run Unity. To bank the RED:
1. Comment out the single line `Guard.Try(Sys, "clear pursuit window at battle end", PostureSignals.ClearPursuits);`
   in `Assets/_Modules/Core/Combat/BattleSessionEnd.cs` (~line 134).
2. Run the regression -> BattleQuiescence cases 1-3 MUST FAIL.
3. RESTORE THE LINE IMMEDIATELY. Verify with `grep -c ClearPursuits` before moving on.
(The lead did this once already and had to restore it when Unity turned out to be open.)

## PROD: 58 READY IS NOT 58 SHIP BLOCKERS

Ship gates are the FOUR MARKERS (CLAUDE.md sections 8 + 16), not an empty board:
`COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n>` + `UI_CAPTURE_OK` + `R2_PARITY_OK`.

**Actually blocks a prod push:**
- WO-1233 battle-lock survives the battle (P0 softlock, 8 of 9 arena wins). FIXED, needs gate.
- WO-1223 fail-closed gating (four of five failure modes were wide open). FIXED, needs gate.
- The R2 push - `tools2-ship.ps1`. **Bundle names are content-hashed; EVERY content build
  needs ITS OWN push.** A previous push can never cover this one (section 16).

**Everything else on the board is polish, presentation or new feature.** Shipping with an
untidy Treasure panel is legal; shipping with a town the player cannot interact with is not.

## LANES DONE, UNCOMMITTED, ALL BRACE-VERIFIED BY THE LEAD (not on agent say-so)

| Lane | WO | Note |
|---|---|---|
| Staff drawn pose | 1226 | `_staffGripEuler -> (90,0,0)`, DERIVED. Pin moved WITH the owner ruling. |
| Battle-lock | 1233 | Root: `PursuitBattleProbe` held it. `ClearPursuits` had ONE caller, on scene load; the arena stages in-place with no scene load. |
| Raid payout | - | Raids pay once at end. Gold + XP still per-kill (owner: "gold and EXP are good"). |
| Fail-closed gating | 1223 | 4 of 5 failure modes were OPEN. `ParseState` ended `default: return Open`. |
| Enemy level | 1232 | Two retired HP/25 sites killed. **See correction below.** |
| VFX pool | 1229 | NOT a leak - unrestrained demand. + dungeon 48 tier + absolute aura allowlist. |
| Hollow passes | - | 4 guards that asserted nothing now report. |
| Hero select + card | 1083/1234 | Portrait path was in **11 literals across 7 files**; now 2, both in the constant. |

## CORRECTIONS THE NEXT SEAT MUST NOT UNDO

1. **`Enemy.Level` IS ALSO the HP/25 heuristic** (`Enemy.cs:623`, `round(def.Hp/25f)`).
   There is NO authored level field on `EnemyDef`. `necromancer` = 1700 hp = exactly Lv 68,
   the wave-6 boss. WO-1232's original premise was wrong and is corrected in the ticket.
   The doc comment on `Enemy.Level` is what misled it - section 12 applies to comments too.
2. **Five faces in open town is CORRECT.** Talk is proximity-gated. Do not "fix" it.
3. **Offline first-run = every dungeon SEALED** is the owner's ruling, not a bug.
4. **Passive Echo repair SPENDS materials** - owner ruled the spend STAYS.

## OWNER RULINGS MADE TODAY (all recorded in their tickets)

Staff stands vertical | portals fail closed | raids pay at end only | wave window 300->900s
(Easy 30 / Normal 15 / Hard 9, approved: "changes from casual to hardcore") | CTA reads
"Train Army", strings only | mana recipe moves to match the scroll art | treasure overflow =
6 lines then scroll | rail = toggle + gold "+4" hint | Echo repair spend stays | dungeon VFX
tier ON | accessibility aura absolutely unrefusable | granary open / cottage sealed in prod.

## PRODUCTION DB - ALREADY DONE, DO NOT REDO

`dungeon_status` seeded and verified by shape query: `DUNGEON_ROWS_OK 6/6 covered`.
`dg_folks_granary=open`, `dg_healers_cottage=sealed`. schema.sql aligned.
NOTE `ON CONFLICT DO NOTHING` means schema.sql will NOT back-fill a provisioned DB - that is
why those rows were missing in the first place.

## STILL OWED TO THE OWNER (raised, unanswered - none blocking)

- WO-1232 presentation: recommend BOSS / ELITE / APEX as WORDS. A number is unavoidable
  nonsense while level == HP/25.
- `hollow-brute` (900 hp -> Lv 36) reads LETHAL forever; it is an ordinary wave brute.
- Dungeon 48-tier is a Seeker PERF item - watch particle overdraw when LOOKING INTO a lit
  room (fill-rate bound), not on entry.
- Side carousel cards still draw their own labels under baked plates (~217 px, likely illegible).
- `WebGLTextureShrink` clamps HeroPortraits to 256 px - marginal for a card with a name plate.

## F8 DEVICE BRIDGE IS LIVE (WO-1227)

Background loop running, 30 s poll. It delivered its first two captures today (seq 3608/3609),
both triaged and acked. **The owner is no longer the detector on device.**
Backfill digest: `logs/f8-inbox/DEVICE_BACKFILL_2026-08-26.md` (736 entries, gitignored).
