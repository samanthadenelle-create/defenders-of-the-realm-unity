# HANDOVER - CLI lead session, 2026-08-25

**Branch:** `wip/village2-and-f8-tickets` - clean, everything pushed at handover.
**Banner:** CLI main line next free = **1204**. UI seat = **1083**. One active heading; verify before minting.

---

## 1. FIRST ACTION NEXT SESSION

`BATCH_STATE.md` carries a finished **Batch 12** section that has NOT been committed yet.
Read it, confirm the two points below, then commit and tell the owner it is ready to courier.

1. WO-1201 must be **absent from the Codex lanes** and present in the "not in this batch" fence.
2. The fence must name the Grok no-touch list (section 3).

Both were applied by the writing agent, but verify - the change landed mid-write.

---

## 2. WHO HAS WHAT

| Seat | Owns | Notes |
|---|---|---|
| **Grok** | **WO-1202**, with **WO-1201 folded in as its Phase A** | Grok authored 1202 and wrote "do not leave 1201 as a second competing pickup". Owner assigned both to Grok. WO-1201 on disk already reads FOLDED INTO WO-1202. |
| **Codex** | WO-1163 (bounced, money bug), WO-1199 (two one-line fixes), then Batch 12 lanes 1-4 | Batch 11 was couriered. Batch 12 is written and awaiting commit. |
| **UI seat** | WO-1192 (unblocked), WO-1194, class-select badge (renumbering to 1083) | ONE-WAY bridge. Section 5. |
| **Owner** | The reward worksheet; the palette-image call | Section 4. |
| **CLI (you)** | Verify, gate, commit, push. **Sole committer.** | |

**The one shared surface:** `DataRegression.cs` registration is COMMITTER-FENCED. Both lanes will want
an oracle registered there. Neither seat adds it - the lead does, at commit time.

---

## 3. THE COLLISION YOU MUST NOT LET HAPPEN

WO-1202 folds WO-1201 in as Phase A. If Codex also picks up 1201, **two seats build the same
typed-reward-list migration over the same 63 stages in the same two canonical `quests.json` copies.**

While the Grok lane is live, Codex must not touch: `quests.json` (either copy), the `QuestReward`
type, `QuestService`, `QuestRewardBridge`, `QuestCompletabilityRegression`.

This is the Batch 8 failure (WO-1137/1138 handed out as fresh when both were already finished).
It is the single most likely way to waste a day.

---

## 4. OPEN OWNER DECISIONS

1. **Magic is a dead reward.** Quests pay **325 Magic across 9 stages**; the only sink in the game is
   the Forge 6th tier at **3 Magic, once**. If Magic stays as-is, the true "pays nothing" count is
   **42 of 63**, not 33. Worth ruling before she authors values.
2. **The empty stages** - `docs/QUEST_REWARD_WORKSHEET.md` is the fill-in sheet (63 rows, XP and Other
   left blank, difficulty and scarcity included). The WO-1201/1202 migration must NOT be coupled to
   her authoring; it can land against blanks.
3. **The build palette card image** - keep a thumbnail, or go text-only? UNRULED. She floated removing
   it ("easier less work"); the lead countered with thumbnail-plus-text, because the image is what
   makes a card recognisable without reading. One capture settles it.
4. **Quest 3 `forgemasters_act1` ("Honest Steel")** pays nothing on every stage AND ends on nothing -
   the only quest with either property. Author a reward, or let it say "Unlocks Act 2".

---

## 5. WO-1200 IS CASE (c) - THERE IS NO MACHINE TRANSPORT

The UI seat `git push` 403s AND its GitHub MCP write 403s. `SendMessage` reaches it; it **cannot
reply**. **The owner remains the courier, in both directions.** Do not close WO-1200 as fixed, and do
not invent a transport - a mailbox neither seat can reach is worse than an honest gap, because silence
would then read as "nothing queued".

If anyone builds it later: keep QUEUE semantics regardless of transport - append-only, one file per
message, oldest-first, ack exactly one. A single-slot mailbox is what lost F8 captures 2307 and 2308.

---

## 6. FINDINGS THAT MUST NOT BE LOST

**A. Two in-code comments are DELETION HAZARDS.**
- `AudioBootstrap.cs:163-167` calls four battle-music tracks "unreferenced / left unwired" -
  `BattleMusicManager.cs:107-126` **wires all four**. A size-trim seat would delete live music.
- `DevClock.cs:28-34` warns a time-skip moves "EIGHT live consumers"; there are **20**, including a
  **purchased harvest boost** and raid cooldowns.

**B. The `stone` money bug is a CLASS, not an incident.** `packs.json` authored `stone`,
`PackCatalog.cs:64` bound only `[JsonProperty("food")]`, Newtonsoft dropped it silently, and three
LIVE SKUs would have granted nothing - no exception, no log, no red test.
`ModifierKeyCoverageRegression` already covers this shape for ONE file, and its own header calls
itself "deliberately GENERIC". There are **66 canonical files and no equivalent for the other 65**.
A `CanonicalKeyCoverage` oracle closes the class repo-wide. **Highest-value lane available.**

**C. The cost formatter is written THIRTEEN times** plus a fourteenth that re-parses one, already
drifted four ways. Spec is in WO-1195. `currency_food.png` is a **stock agribusiness logo** (tractor,
fields, water tower) - it fails greyscale by ILLEGIBILITY, not hue. Missing icons: `stone`, `wisdom`,
`magic`.

**D. `rewardWisdom` pays CRYSTALS at runtime** (`DailyQuestRewardBridge.cs:252-266`) while the HUD
label still says "Wisdom". A live currency mislabel on a reward. **Not ticketed yet.**

**E. The board PARTIAL badge renderer has NO quoted-span exemption** (`board_build.py:148-152`), so
any row that merely NAMES the badge is rendered with it, silently asserting a landing. The `--check`
path IS exempted; the renderer is not. Ticketed as **WO-1203**.

**F. Quest titles in `docs/ui-captures/RumorBoard_2670x1200.png` are FIXTURES** hardcoded in
`UICaptureLaunch.cs`. Do not brief anything off them - they are not shipping data.

---

## 7. TRAPS CONFIRMED THIS SESSION

- **Judge by MARKER on a FRESH log, never the exit code.** Held all day.
- **`Builds/r2-parity.log` is UTF-16** - a plain grep returns 0 hits. That is an encoding artifact,
  not a missing marker.
- **`Builds/` is gitignored** - captures there are unreachable to other seats. Copy to
  `docs/ui-captures/` (LFS-tracked) if a seat needs to see one.
- **`git lfs push --all` may be needed before a push** - 3,219 objects were unpushed at one point and
  the remote declined. Anyone who cloned before that had broken pointers.
- **A refusal test is NOT acceptance** (WO-1199). Prove the SUCCESS path; name ops-owned items.
- **Grok stamps wrong dates** (WO-1202 says "Minted 2026-08-17" on a 2026-08-25 mint). Dates are how
  we judge whether a ticket is still true. Worth telling it.

---

## 8. GATE STATE

`COMPILE_GATE_OK` + `REGRESSION_OK 279/279 suites, 0 red` verified this session on the tree that
included WO-1198 and the WO-1196 oracle. Backend suite **56/56** with no `DATABASE_URL`.
`BOARD_CHECK_OK` 0 unlabeled, 0 contradictions, 1135 rows.

**WO-1163 and WO-1199 have ZERO bytes in this tree** - their work exists only in unmerged Codex
worktrees and was reviewed and returned. Do not read their READY status as "not started".
