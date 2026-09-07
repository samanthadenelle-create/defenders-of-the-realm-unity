# WO-1521: a quest counter says one is ready to claim while the rumor board says the board is quiet

**Status:** READY TO IMPLEMENT - owner report 2026-09-06 20:18
**Silo:** Village quests / rumor board - the quest service and its claim surface, `QuestRewardBridge.cs`,
`JourneyDeckSubtitleVM`, and the rumor board panel.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1521 -> 1522 in the same edit).

## 1. EVIDENCE

Owner report, verbatim:

> "quests say one quest to claim but no idea how or what to do to complete it"

Device frame `Logs/device/screens/owner-screen-20260906-201850.png` (build 358574, 20:18):

```
"Brom's Rumor Board"     PREVIOUS / NEXT / CLOSE
centre card              "The board is quiet. / Brom posts more as Elarion wakes."
quest rows               NONE
backdrop                 ABSENT - the town bleeds through (same class as WO-1462)
```

The counter that contradicts it is composed here:

```
Assets/_Modules/Core/HudModel/JourneyDeckSubtitleVM.cs:21
  QuestsSubtitle = activeQuests + " active . " + readyToClaim + " ready to claim";
```

So two surfaces read two different quest states: one says something is ready, the other says there is nothing.
Nothing anywhere names the quest, its objective, or where to claim it.

**Correction to WO-1477:** PREVIOUS **already exists** on this screen. WO-1477 (rumor board PREVIOUS button)
must verify at source before adding a second one - the owner's "a previous button would be nice" may have been
about a different surface, or about the button not working. Noted in that ticket too.

## 2. FIX SHAPE

- **ONE quest authority feeds both surfaces.** The counter and the board read the SAME list. Do not add a
  second list to fix the disagreement.
- A **CLAIMABLE** quest renders on the board as its own row: objective text, reward, and a CLAIM door through
  `PanelRouter` / the VM verb - never a second path.
- An **ACTIVE** quest renders with its objective and a GO-TO door to the place that completes it. That is the
  half the owner's "no idea how or what to do" is actually asking for.
- `"The board is quiet."` paints ONLY when the list is empty.
- The counter's tap opens the board scrolled to the claimable row.
- Backdrop from the kit (shares WO-1462's fix).

## 3. WHAT NOT TO DO
- Do not add a second quest list or a second claim path.
- Do not auto-claim. She wants to know what to do, not to have it done.

## 4. ACCEPTANCE
- [ ] Measured case: a claimable quest in state produces a board ROW carrying its objective and a CLAIM door.
- [ ] Measured case: the counter and the board agree on the count, from the one authority.
- [ ] Measured case: the quiet copy NEVER paints while the list is non-empty. RED today.
- [ ] Door assertions follow `WelcomeBackDoorsRegression`'s pattern.
- [ ] Headless `RumorBoard_*.png` captured and opened.
- [ ] `REGRESSION_OK n/n` on a fresh log.
