# Felt-test DELTA - the second 2026-08-25 APK (build 2)

**Build:** `Builds/Android/DefendersOfTheRealm.apk`, rebuilt at HEAD this evening.
**HEAD:** `e397018a8`. The APK's own version stamp is the authority - read it in Settings, not here.
**Supersedes:** nothing. `FELT_TEST_2026-08-25.md` is the 08:22 build's sheet and **its lap still
stands** - every row in it is still in this build. This file is the DELTA: the five things that
exist ONLY in build 2, because 76 commits landed after the 08:22 APK was cut.

⛔ **If you see tinted capsules or placeholder buildings, stop the lap and say so.** Content was
proven hosted for this build (`R2_PUSH_OK` then `R2_PARITY_OK`) before it was installed, so capsules
would mean something new broke - not the old missed-push failure.

---

## Read this first: which sheet answers which question

| Question | Sheet |
|---|---|
| Store prompts + prices, waves 5/10, build palette, over-cap wording, gear ability line | `FELT_TEST_2026-08-25.md` - unchanged in build 2 |
| Stone, the staff, ads returning to their caller, phantom harvest yield | **this file** |

---

## The delta lap

### 1. STONE has replaced FOOD everywhere the player can see it (WO-1163)

The biggest change in this build, and the one most likely to show a seam. Food is retired as a
player-facing resource; Stone takes its save slot. Internally the field is still the old one, so a
leak shows up as the WORD, not as a broken number.

| # | Where | Look for | Fail tell |
|---|---|---|---|
| D1 | Town HUD resource strip | **Stone**, with the new Stone icon. Your old Food balance carried across 1:1 | The word **Food** anywhere on the strip; a missing or placeholder icon; the number changing from what you had |
| D2 | Build menu, a **level 1** structure | Cost reads **Wood + Gold** | Stone or Iron in an L1 cost |
| D3 | Any **level 2** upgrade | Cost reads **Stone + Gold** | Food named; or L2 still asking for the old basket |
| D4 | Any **level 3** upgrade | Cost reads **Iron + Gold** | Anything else |
| D5 | Barracks - train any troop | Cost is **Gold only** | A material cost on training. Legacy troops should have folded their old Wood+Iron+Food total into Gold |
| D6 | Queue a build, then **cancel** it | The full basket comes back - materials **and** Gold - in one refund, and the resource toast agrees with the strip | A partial refund; Gold not returned; two toasts disagreeing; the strip not matching Manage |
| D7 | The **Quarry** | It renders as a real model with a readable portrait in the palette card | A placeholder, a magenta model, or a blank portrait |
| D8 | Quest text that used to pay Food | Presentation says **Stone** | A quest still saying Food. Wire fields deliberately kept the old key, so this is a display question only |
| D9 | **Greyscale check on the Stone icon** | Distinguishable from Wood and Iron at a glance, by SHAPE not colour | Stone reading as a twin of Iron. That is a real defect for you specifically, and it is my job to have caught it - tell me if it slipped |

### 2. The staff sits in the hand correctly (WO-970)

The staff carried a stale `+90` grip compensation from before the bounds solver was repaired. It has
been reset to neutral. **This row is a veto, not a bug hunt** - you approved the reset in advance and
this is you looking at the result.

| # | Where | Look for | Fail tell |
|---|---|---|---|
| D10 | Equip `staff_A` on a Mage, **drawn** | Gripped at the shaft, pointing where a staff should point | Held by the head; laid flat across the body; rotated a quarter turn |
| D11 | Same staff, **sheathed** | Sits on the back in a plausible carry | Clipping through the body, or standing off it at an angle |
| D12 | A **wand**, drawn and sheathed | **Unchanged** from before. The wand keeps its own independent calibration | The wand having moved. That would mean the reset was too broad |

### 3. A rewarded ad returns you where you started (WO-1204)

Your report: after an ad played you landed in Pause/Settings instead of the screen that invoked it.
Cause was Android's own pause callback opening Pause underneath the ad and swap-closing your caller.

| # | Where | Look for | Fail tell |
|---|---|---|---|
| D13 | **Daily Chest** - invoke a rewarded ad, let it run to the reward | You come back to **Daily Chest**, still open, reward applied | Landing in Pause or Settings. That is the original defect |
| D14 | A **second, non-Settings caller** (Manage/Queues is the other pinned one) | Same answer - back to the caller that asked | Back to Pause; or back to Daily Chest instead of the caller you used |
| D15 | Invoke an ad and **dismiss it early** | Back to the caller, no reward, no error screen | A terminal error; or a different destination than D13 |
| D16 | Invoke an ad when **none is available** | A worded refusal, and you stay where you are | A blank screen, a spinner that never ends, or navigation away |
| D17 | ⭐ **Now background the app normally** - home button, no ad involved | **Pause opens**, exactly as it always has | Pause NOT opening. The fix scopes the suppression to ad presentation only; if ordinary backgrounding stopped opening Pause, the scope is leaking and that is worse than the bug it fixed |

### 4. A recalled Echo stops paying (Batch 12 R4)

A destroyed or recalled Echo kept paying its harvest yield forever, because the site never pruned it.

| # | Where | Look for | Fail tell |
|---|---|---|---|
| D18 | Assign an Echo to a harvest site, note the rate, then **recall or despawn it** | The yield rate **drops** by that Echo's share | The rate holding steady after the Echo is gone. That is the phantom |
| D19 | Re-assign it | The rate comes back up, once, not twice | A double count - the Echo paying from two entries |

---

## What did NOT change in build 2 - do not re-run these expecting a difference

- **WO-1190 store prompts/prices** - the server half is still not deployed. "No prompt + no prices"
  remains the expected half-shipped answer.
- **WO-1179 waves from 2 and 4 sides** - unchanged, and still the least-proven rows anywhere. If you
  only have one long sitting, spend it there.
- **WO-814 gear ability line** - still deliberately silent until you author the names.
- **WO-1186 palette chips under minimum touch size** - still your open decision, not an escaped bug.

## Still not proven by anything on this device

`MAINNET_SALES_ENABLED` is untested. Your wallet is waved through **before** that switch is
consulted, so your store working still proves nothing about what a stranger sees. Only a price-list
call from a wallet that is not yours settles it.
