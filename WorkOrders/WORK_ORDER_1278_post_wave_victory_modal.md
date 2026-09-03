# WORK ORDER 1278 - Post-wave victory modal

**Status:** CLOSED 2026-09-03 - owner felt-test PASS. PRIOR STATUS: FIXED 2026-08-29 - the framed post-wave victory moment is present in Seeker tester APK 2026.08.29.346849; awaiting owner device test.

Replace the tiny yellow post-wave result with the standard framed Obsidian end-state modal. It must lead with the cleared wave, report only banked payout and persisted unlock authority, state the next action, and never ellipsize player-facing result copy.

Wave-result presentation now holds the live countdown through the shared `WorldHold` until the one primary action or a short unscaled timeout closes it. Wave 1 uses a restrained burst and five-second maximum hold; Wave 7 uses the full milestone beat and eight-second maximum hold.

Evidence: `PostWaveVictoryModalRegression.RunAll`; compile/static gates pending the shared build handoff.
