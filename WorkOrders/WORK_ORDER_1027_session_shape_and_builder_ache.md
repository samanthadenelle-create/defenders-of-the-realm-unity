# WORK ORDER 1027 — The ratchet has no ache: give the session a shape

**Status:** READY TO IMPLEMENT — ★ §4 RULED 2026-08-17: **(b) empty-slot silhouette + (a) count glance**

> Owner ruling 2026-08-17 (*"open ones follow your recommendations"*): the peek rail shows a visibly
> **empty socket** as the resting state, with the **"2 of 3 lines idle" numeral** alongside it.
> **(c) the active nudge toast is REJECTED** — it was the strongest pull and the most annoying, and the
> ache is meant to inform, not nag.
>
> ⚠ WHY THIS PAIR AND NOT A BADGE: the message is carried by **shape and number**, so it satisfies the
> colourblind law **by construction rather than by mitigation** — there is no hue to get wrong. CoC's red
> badge is banned here outright (the owner is red/green colourblind, and this repo already carries two
> open colour-only defects: build placement ghost, hero health bar). Do not add a colour accent "to help";
> if the silhouette and the numeral do not read on their own, the fix is a clearer SHAPE, never a hue.
**Minted:** 2026-08-15 (UI seat) — provenance stack bumped 1027 → 1028 in the same edit
**Lane:** HUD / town flow. No new systems — surfacing only.
**Provenance:** `docs/DESIGN_REVIEW_COC_WC3_LENS_2026-08-15.md` §3 ⓸.

---

## 1. The gap

Our queue is **mechanically better than CoC's**: three channels (Builder / Train / Research), depth cap
5 per line, 2 free slots, an Echo-gated crystal-priced extra slot (`BuildTimerService.TryBuySlot`).

But Clash of Clans' retention engine was never the queue. **It is the ache of an idle builder** — the
small, specific discomfort of knowing a slot is sitting empty and something *could* be cooking. That
discomfort is what brings a player back at lunch.

We have the slots and none of the ache. Concretely, a player today cannot answer:

- *Is anything of mine idle right now?*
- *What should I do next?*
- *Am I done for this session?*

## 2. Why this is the cheapest item in the review

Nothing new is built. Every fact needed already exists in `BuildTimerService` and the queue model. This
is **surfacing**, and it is what makes WO-1026 and WO-1028 legible when they land — a player with no
session shape will not notice new loops either.

## 3. Scope

1. **Idle-slot pressure, at a glance.** The player must be able to tell — without opening a panel —
   that a queue line is idle. The right-column Builders chip already exists as a status glance with an
   inline peek rail (canon §7); it is the natural home. ⚠ **Do NOT add an eighth action-bar face** —
   canon §7 is explicit that there is exactly ONE Queues entry and the `Upgrade` face is it.
2. **A "what next" answer.** When a line is idle, the player should be one tap from the thing that
   fills it. Not a tutorial — a shortcut.
3. **A session-complete signal.** The quiet inverse of the ache: when every line is loaded and the
   day's quests are done, say so. **CoC never did this and it is a genuine improvement** — a player who
   knows they are finished leaves satisfied instead of hunting for a missed thing.

## 4. ⛔ OWNER CALL — the tone of the ache

CoC's version is a **red badge**, which is nagging and effective. It is also:

- **colour-only** — ⚠ forbidden here; the owner is red/green colourblind, and this repo already has two
  open colour-only defects (build placement ghost, hero health bar — anchor 2026-08-09)
- exhausting at high frequency

**Options for the owner (ask about FEELING and FREQUENCY, never hue — memory
`owner-colorblind-delegate-visual-creative`):**

| option | feel |
|---|---|
| **(a) Count glance** | "2 of 3 lines idle" — informational, calm, no pressure |
| **(b) Empty-slot silhouette** | the peek rail shows a visibly *empty* socket — shape-carried, reads at a glance, no nag |
| **(c) Active nudge** | a toast on entering town when a line is idle — strongest pull, most annoying |

**Recommendation: (b) as the default state + (a) as the numeral.** Shape and count carry the whole
message with zero hue dependence, and it satisfies the colourblind law by construction rather than by
mitigation.

## 5. Explicitly OUT of scope

- Any change to queue **mechanics** — depth cap stays 5/line, `freeBuildSlots` stays 2. ⚠ Canon §8:
  **never** implement a depth change by raising concurrency
- Any change to `TryBuySlot` pricing or the Echo gate (ruling Q6)
- A new panel. This is glance + shortcut on surfaces that already exist
- Push notifications (separate lane, needs platform work)

## 6. Acceptance criteria

- [ ] From the town, with no panel open, the player can tell **at a glance** whether any queue line is
      idle
- [ ] The idle state is legible **in greyscale** — no hue-only signal (colourblind law)
- [ ] An idle line is **one tap** from the screen that fills it
- [ ] When all lines are loaded, the player gets a clear "you're set" state
- [ ] Zero queue-mechanic changes — `freeBuildSlots` == 2 and depth cap == 5 verified unchanged
- [ ] No new action-bar face; the bar stays at **6 visible**, `ButtonCount` stays **7**,
      `Map` stays dormant at ordinal 4

## 7. Verify

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites`
2. **Screenshots in both states** (idle vs fully loaded) + a **greyscale pass** on each
3. Owner felt-verifies. The question is: *"after ten seconds in town, do I know what to do next?"*
