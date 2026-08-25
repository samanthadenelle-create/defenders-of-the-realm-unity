# WORK_ORDER_505 — BATTLE CLOSING (victory/defeat audio + star rating)

**Status:** FIXED — awaiting owner felt-verify (PO closes, §13). *(Status audit 2026-08-24: bucket correction — the line led DONE while naming verification, and in WO-977's case engineering, still owed; DONE is reserved for a PO close. Body unchanged.)* Prior line: DONE (gate + DataRegression verified; owner felt-verify pending) · Combat/Arena lane · 2026-06-24
**Origin:** creative battle-arc gap-check (top-3 "fix first": the death-cam climax played in SILENCE; the
time-box timer showed but no stars were ever revealed).

## What shipped
1. **Victory/defeat audio under the death-cam.** `BattleArena.Resolve(won)` now plays `MusicTrack.Victory`
   (win) / `MusicTrack.Defeat` (loss) via `CoreServices.Audio` and HOLDS it under the death-cam, restoring
   Overworld only after a `RewardCueSeconds` (2.5s) beat — the climax is no longer cut to silence.
   (Verified the clips exist + resolve: `Assets/Audio/Resources/victory.mp3` / `defeat.mp3`,
   `MusicTrack.Victory/Defeat` already in both Core + Audio enums — no additions needed.)
2. **Star rating.** `BattleStarRating` (pure, tunable consts): duration <=90s -> 3 stars, <=120s -> 2, else 1;
   reward multiplier 1.0 / 1.25 / 1.5x applied to XP, Wisdom, wood/iron in `GrantWinReward`. Stars rendered in
   the result banner (`BattleArenaHud.ShowResult(won, stars)`).
3. **Headless self-verification.** `DataRegression.CheckBattleClosing`: victory/defeat clips assert non-null
   (catches the silent-track bug class) + star tiers/multipliers assert for sample durations. REGRESSION_OK.

## Owner felt-tuning (bones -> finesse)
- Star time-thresholds (`ThreeStarSeconds 90`, `TwoStarSeconds 120`) and the fanfare/sting choice — named
  one-line consts. The star-row visual + reward-multiplier curve are owner's to dial.

## Files
`BattleArena.cs`, `BattleArenaHud.cs`, `BattleStarRating.cs` (new), `Assets/Editor/Regression/DataRegression.cs`.

## Not in scope
Role icons on enemies + the rest of the creative gap list (separate). Gear-drop chance left unscaled by stars
(a multiplier on a binary drop is meaningless).
