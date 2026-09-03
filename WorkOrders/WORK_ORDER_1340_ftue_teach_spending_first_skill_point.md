# WORK ORDER 1340 — FTUE: teach the player HOW TO SPEND their first skill point

**Status:** DONE (code + data + oracle landed; awaiting lead gate/commit and owner felt-verify)
**Silo:** Tutorial / FTUE (Village.Tutorial.V2 + Core.Tutorial) — no scene files, no bake
**Owner ask (2026-09-03, verbatim):** *"can we add a FTUE for learning how to apply a skill point after getting first one"*
**Route confirmed by owner (2026-09-03, verbatim):** *"the path to the skills tree is fixed, it's Hero then Skills"* — on installed build `2026.09.03.353742`
**Stage:** CLI implemented + self-verified (source/JSON oracle proven RED on four mutations). PO closes.

---

## 1. THE HEADLINE FINDING — THE BEAT ALREADY EXISTED AND TAUGHT NOTHING

This is not a greenfield feature. `ctx_talents` has been in the step registry since WO-T1:

```json
"trigger":    { "type": "signal", "signal": "skillpoint.earned:first" },
"highlight":  [],
"completion": { "signal": "dialogue.ended:tut_ctx_talents" }
```

It fired on the right event. Then it **completed the instant the player closed the text box** — which
is the first thing anybody does — and `oneShot` marked it seen **forever**.

So the guide said *"Open your talents and shape how you fight"*, the player tapped to dismiss, the
beat was recorded as delivered, and a player who never found the talent screen **was taught nothing
and could never be told again**. `"highlight": []` meant it did not even point anywhere.

**Why no suite caught it:** at every individual layer the beat was correct — the step parsed, the
dialogue id existed, the trigger had a live publisher, the one-shot persisted. Only the
**relationship** between the completion signal and the thing being taught was wrong. That is the
same shape as the `HeroSkillTreeDoorRegression` lesson (five talent suites passed while the door was
unreachable, because each tested a layer and none tested the door).

**Retention framing (the owner's own):** her stated top business problem is *"our retention number is
very low and people are not returning"*, and WO-1306 deliberately made the mage's first point buy a
**castable** rather than a stat. That work is wasted if the point is never spent. This beat is what
makes WO-1306 land.

---

## 2. WHAT GRANTS THE FIRST POINT, AND THE SIGNAL WE TRIGGER ON

Established at source, not assumed.

`HeroProgression.ApplyLevelRewards` (`Assets/_Modules/Village/Hero/HeroProgression.cs:333-347`) runs
on every hero level and grants **two different currencies**:

| Currency | Granter | What it is |
|---|---|---|
| **Wisdom** | `WisdomCurrencyService.Instance.Grant(WisdomForLevel(newLevel))` | **the talent-tree unlock currency** |
| Craft skill point | `SkillSystem.Instance.GrantSkillPoint()` | Blacksmith / Woodworking / Arcane — gates tower placement |

⛔ **THESE ARE TWO SEPARATE SYSTEMS AND CONFLATING THEM IS THE TRAP.** `SkillSystem`'s own header
says so: *"it is ADDITIVE and does not conflict with the existing ... hero TalentTree ... those track
hero XP/talents, this tracks separate craft-skill levels used to gate buildables."* The talent panel
displays both (`HeroSkillTreeVM.RemainingWisdom` and `RemainingSkillPoints`), which is exactly how a
seat mistakes one for the other. **The talent tree spends WISDOM.**

**Trigger (unchanged, already correct):** `skillpoint.earned:first` — `TutorialSignals.FirstSkillPoint`,
raised by `TutorialSignalAdapters` off `HeroProgression.OnAnyLevelUp`. Every hero level banks a point,
so the first level-up **is** the first point earned; the contextual one-shot's `tutorial_ctx`
persistence dedupes to the first. **Found, not invented — no new trigger was added.**

---

## 3. THE ROUTE TO THE SPEND UI — VERIFIED WORKING BEFORE TEACHING IT

The owner previously reported the skill tree had *no path at all* and called it *"a huge issue"*. That
is **fixed**, and it is now **double-pinned**:

- **Live route:** action-bar **HERO** face -> `PanelId.HeroDeck` -> the **SKILLS** card -> `PanelId.HeroSkillTree`.
  - The bar face is built at `HudKitController.cs:778-790` (labelled *"Hero"*, opens `PanelId.HeroDeck`;
    the enum member stays `ActionBarButtonId.Bag` because **the ordinal is load-bearing** per CLAUDE.md §7).
  - The card is `PlayerDeckWorkspace.CardsFor(Hero)` -> `Route("Skills", "Learn and improve hero talents", "skill", PanelId.HeroSkillTree, "skills")`
    (`PlayerDeckWorkspace.cs:383`), built by `BuildCard` as GameObject **`DeckCard_Skills`**.
- **Pinned by `HeroSkillTreeDoorRegression`** (`SKILL_TREE_DOOR_OK`), which exists because commit
  `d6d3146b2` silently dropped the Skills entry and made the whole talent stack unreachable.
- **Owner-confirmed on device** the same day, on build `2026.09.03.353742`, with a screenshot showing
  four cards: BAG, EQUIPMENT, SKILLS, LOADOUT.

**Verdict: the route works today. We teach that route and nothing else.** No bar face was re-pointed.

> ### ⚠ KNOWN DEFECT ON THAT SCREEN — NOT THIS LANE
> Every label on the Hero panel currently renders **twice**, by two owners in two fonts with two
> wordings (*"SKILLS / Learn and equip abilities"* over *"SKILLS / Learn and improve hero talents"*).
> That is **WO-1341**, owned elsewhere. This WO does **not** touch it and **adds no third label
> producer** — the highlight anchors to the **card's own rect**, whose geometry is unambiguous, and
> the beat's words are delivered through the existing dialogue system.

---

## 4. WHAT WAS BUILT

### 4.1 A completion signal that means *a point was actually spent*

`TutorialSignals.FirstTalentLearned = "talent.learned:first"`
(`Assets/_Modules/Core/Tutorial/TutorialSignals.cs`)

**Sole publisher:** `WisdomCurrencyService.Unlock`
(`Assets/_Modules/Village/Talents/WisdomCurrencyService.cs:154-172`), raised **only after** the
Wisdom debit and the `_unlocked.Add` have landed.

That method is **the one choke point every learn path funnels through** — the legacy immediate
`HeroSkillTreeVM.Unlock` *and* the node-graph plan/CONFIRM `HeroSkillTreeVM.Commit` both call it. So
the signal cannot be raised by a path that did not move the player's tree, and there is exactly one
place to keep honest.

### 4.2 A route-following spotlight (new, minimal schema)

New optional field `route` on `TutorialStepDef` (`TutorialRouteHop { signal, highlight }`,
`Assets/_Modules/Core/Tutorial/TutorialStepModel.cs`). While a hint is live, reaching a hop's signal
**re-points** the spotlight; an **empty** highlight **releases** it.

This is why one beat can walk a two-tap path without a chain of separate one-shots — each of which
would have to guess a trigger, could fire out of order, and (as `panel.opened:*` would) could fire
for a player who has no point to spend.

It is **presentation only**: a hop that never fires costs nothing and can never hold the beat.

### 4.3 Two new highlight ids

Added to `TutorialHighlightRegistry.KnownIds` (which `DataRegression` validates every authored
highlight against):

- **`hud.hero_button`** — registered eagerly beside the existing `hud.build_button` precedent
  (`HudKitController.cs`).
- **`deck.card.skills`** — a **lazy resolver** on `GameObject.Find("DeckCard_Skills")`, following the
  established `hud.pets` pattern, because the deck's cards are built per `RenderPage` only once the
  player opens the Hero panel. Resolves **the card's own rect, never a label** (see WO-1341 above).
  Degrades gracefully to nothing if absent.

### 4.4 The interpreter: contextual beats can now await the world

`TutorialFlow.cs`. A contextual step whose `completion.signal` is anything other than its own
`dialogue.ended:<intro>` is treated as a **TEACH beat** (`TutorialStepDef.AwaitsGameplayCompletion` —
**derived, not a second authored flag**, so the two fields can never disagree):

- Latch **cleared on arm** (`TutorialSignals.Clear`) — the bus latches, so a stale raise from earlier
  in the session would otherwise complete the beat the instant it armed and teach nothing.
- Completion on the authored gameplay signal -> `CTX-TAUGHT` trace.
- Route hops re-point the spotlight -> `CTX-ROUTE` trace.
- **The dialogue-ended branch is guarded off for a teach beat.** Closing the text box is the player
  agreeing to go and do it, not the doing.

### 4.5 Data (both canonical twins, byte-identical)

`tutorial-steps.json` v4 -> **v5**; `ctx_talents` now:

```json
"highlight":  ["hud.hero_button"],
"route":      [ { "signal": "panel.opened:HeroDeck",      "highlight": "deck.card.skills" },
                { "signal": "panel.opened:HeroSkillTree", "highlight": "" } ],
"completion": { "signal": "talent.learned:first" }
```

`DataRegression`'s `KnownSignal` vocabulary gained `FirstTalentLearned` (without it the new signal is
rejected as unknown).

---

## 5. IT CANNOT WEDGE, AND IT GATES NOTHING

*"A tutorial step that can wedge is worse than no tutorial step."*

- **It gates nothing by construction.** `flowId: "contextual"` -> never enters the mandatory chain,
  `pausePressure: false`, `UiSpotlight.MaskStyle.Glow` (no dim, **never blocks input**). A player who
  ignores it plays on with zero difference.
- **Finite escape bound, always ticking.** `ContextualAwaitSeconds = 240f` in
  `TutorialFlow.TickContextual`, which runs **every frame in every phase** — not only while the FTUE
  is armed. On expiry: **`CTX-STUCK :: ctx_talents - no 'talent.learned:first' after 240s`**, then
  `CompleteContextual("timeout")` clears the spotlight and marks it seen.
- **It names itself** (CLAUDE.md §12). WO-1300 exists because two stuck beats emitted **nothing** and
  cost two investigations; a teach beat that quietly stopped pointing would be that defect in a
  cheaper coat.
- **Skip paths already cover it:** `SkipAll` marks every contextual one-shot seen and dismisses the
  live hint.
- **Await state is cleared on every exit path** (complete / timeout / skip / dismiss), so a surviving
  `_ctxAwaitSignal` can never make the *next* ordinary hint refuse to close.

> ### ⚠ `ContextualAwaitSeconds` IS NOT THE MANDATORY WATCHDOG
> `WatchdogSeconds` (120f) bounds a step that **blocks** the FTUE and WO-962 §3 forbids lengthening
> it — `TutorialCompletionPublisherRegression` case 5 pins that, and this WO **did not touch it**.
> The 240s bound governs a hint that blocks nothing; the failure it prevents is a spotlight that never
> lets go, not a player who cannot proceed.

### The WO-1300 shape was deliberately not reintroduced

WO-1300's defect was *a completion signal whose sole publisher could be skipped* (`RunScriptedTownWave`
was a `.Forget()` over an unguarded await chain). Here the publisher is a **plain synchronous return
path inside the method that performs the purchase** — no async, no fire-and-forget, nothing to fault
past. And unlike a mandatory beat, **the escape does not depend on the publisher existing at all.**

---

## 6. DRAFT STRINGS — ⛔ AWAITING THE OWNER'S WORDING

**These are functional placeholders, deliberately plain. Names, tone and wording are hers. No flavour
was invented.**

| Where | Draft (ASCII-only) |
|---|---|
| `tut_ctx_talents` line | `A talent point is yours, Keeper. Open Hero, then Skills, and spend it on something you can use.` |
| `ctx_talents` objective | `Spend your talent point: open Hero, then Skills` |

Both **name the route in words** (`Hero`, then `Skills`), which is load-bearing twice over: the owner
is **red/green colourblind**, so the affordance must read by words/position, never hue — and the lazy
highlight resolvers degrade to nothing if a rect is absent, leaving the words as the affordance that
always survives. The oracle **pins that both hop names stay in the objective text**.

ASCII-only verified: **zero** codepoints > 126 in either JSON twin.

---

## 7. ORACLE — PROVEN RED, REPORTED HONESTLY

Extended the **existing** suite (`TutorialCompletionPublisherRegression`, marker
`TUTORIAL_COMPLETION_PUBLISHER_OK`) with **case 6 `[teach-spend]`** rather than adding a new suite.
Also added the `talent.learned` family rule to `PublishersFor`, so case 3 stays honest.

Case 6 asserts: completion signal is `talent.learned:first` (**not** a `dialogue.ended:*`); the step
stays contextual / non-pausing / skippable / oneShot; `highlight[0]` is `hud.hero_button`; `route` is
non-empty; the objective names both hops; the signal has **exactly one** publisher and it is in
`WisdomCurrencyService`; the raise sits **after** `_unlocked.Add`; and the escape exists
(`ContextualAwaitSeconds` spent in `TickContextual`, a `CTX-STUCK` line, a `timeout` completion, and
the guard on the dialogue-ended **branch condition**).

### Mutation evidence

Predicates evaluated against the real files with mutations applied to the real tree and reverted
(`git` confirms the tree was restored: `SkillSystem.cs` byte-identical to HEAD, no whitespace churn).

| # | Mutation | Result |
|---|---|---|
| Baseline | tree as written | **GREEN (0 findings)** |
| **M1** | completion reverted to `dialogue.ended:tut_ctx_talents` (**the original shipped defect**) | **RED** `completion-signal` |
| **M2** | `CTX-STUCK` self-report stripped from the escape path | **RED** `no-CTX-STUCK` |
| **M3** | `_ctxAwaitSignal` guard deleted from the dialogue-ended branch | **RED** `onsignal-no-guard` |
| **M4** | signal also raised from `SkillSystem.SpendPoint` (the unrelated **craft** economy) | **RED** `publisher-unique(2 sites)` |

> ### ⚠ M3 CAUGHT A HOLLOW ASSERTION IN MY OWN ORACLE — RECORDED, NOT QUIETLY FIXED
> The first version of the guard check asked only whether `OnSignal` **mentioned** `_ctxAwaitSignal`
> anywhere. **M3 passed it GREEN.** The teach-completion branch above still mentions the field, so the
> method-wide search stayed satisfied **while the defect was fully restored**. The assertion was
> re-scoped to the *branch condition that actually decides*. An assertion that cannot fail is worse
> than no assertion, because it reports green — the WO-1138 hollow-pass class. **This is why the
> brief says prove it RED first: without M3 this suite would have shipped blind to the exact
> regression it exists to prevent.**

> ### ⛔ WHAT I DID **NOT** RUN — READ THIS BEFORE TRUSTING THE ABOVE
> **No Unity gate was run** (forbidden by the brief; the lead gates and commits). The table above is
> a **faithful re-evaluation of case 6's own predicates** — same anchors, same JSON/source assertions
> — **not an execution of the C# suite**. Case 6 is a pure source/JSON lint, so the predicates are
> exactly reproducible, but **the lead must still run the real suite**, because only that proves the
> C# **compiles** and that `ExtractMethod`/`CollectRaiseSites` behave as I modelled them.
> Per CLAUDE.md §8: judge by the **marker on a fresh log**, never the exit code.

---

## 8. FILES TOUCHED

| File | Change | Braces |
|---|---|---|
| `Assets/_Modules/Core/Tutorial/TutorialSignals.cs` | new `FirstTalentLearned` | BALANCED / clean |
| `Assets/_Modules/Core/Tutorial/TutorialStepModel.cs` | `TutorialRouteHop`, `Route`, `AwaitsGameplayCompletion` | BALANCED / clean |
| `Assets/_Modules/Core/UI/TutorialHighlightRegistry.cs` | 2 KnownIds + `deck.card.skills` resolver | BALANCED / clean |
| `Assets/_Modules/HUD/Kit/HudKitController.cs` | register `hud.hero_button` | BALANCED / clean |
| `Assets/_Modules/Village/Talents/WisdomCurrencyService.cs` | **the sole publisher** | BALANCED / clean |
| `Assets/_Modules/Village/Tutorial/V2/TutorialFlow.cs` | teach-await + route hops + escape bound | BALANCED / clean |
| `Assets/Editor/Regression/DataRegression.cs` | `KnownSignal` += `FirstTalentLearned` | BALANCED / clean |
| `Assets/Editor/Regression/TutorialCompletionPublisherRegression.cs` | case 6 + family rule | BALANCED / clean |
| `Assets/{Resources,StreamingAssets}/Data/Canonical/tutorial/tutorial-steps.json` | v5, `ctx_talents` | twins md5-identical |
| `Assets/{Resources,StreamingAssets}/Data/Canonical/dialogue/dialogues.json` | draft line | twins md5-identical |

No scene file touched. No bake. No new `System.Reflection`. Cross-module calls unchanged.
**Lane fences respected:** nothing under `Assets/HeroContent`, no hero FBX metas, no
`BattleQuiescenceRegression`/`PursuitBattleProbe`/modal-handle code, no store lane
(`PackStore`/`NightMarket*`/`packs.json`/`canon-strings.json`/`hud-areas.json`), no
`BOARD.html`/`tools/board_build.py`. `HeroSkillTreePanelMvvm.cs` **not touched at all** — its layout
solver, axis rotation, lattice/pitch maths, extents and node-plate label sizing are untouched.

---

## 9. FOR THE LEAD

1. Gate at HEAD: `COMPILE_GATE_OK`, then `DeNelle.Editor.Regression.TutorialCompletionPublisherRegression.RunAll`
   -> `TUTORIAL_COMPLETION_PUBLISHER_OK`, plus `DataRegression.RunAll` -> `REGRESSION_OK <n>/<n> suites`
   (the new signal/highlight ids pass through `KnownSignal` + `KnownIds`) and `SKILL_TREE_DOOR_OK`.
2. **Markers on fresh logs, not exit codes.**
3. `WisdomCurrencyService.cs` is CRLF in the working tree while HEAD is LF (pre-existing, repo-wide);
   the diff is 8 added lines only — no EOL churn was introduced anywhere.

## 10. FOR THE OWNER (PO closes)

- **Wording is yours** — §6 is a placeholder, not a proposal.
- **Felt-verify:** level up once, then confirm the guide's line points you at **HERO**, the glow moves
  to the **SKILLS** card when the Hero panel opens, and the hint **stays with you until you actually
  spend a point** rather than vanishing when you dismiss the text.
- **Ignore it entirely and confirm nothing blocks** — that path is the one this WO most wants tested.

---

**Provenance:** owner ask 2026-09-03; route confirmed by owner on build `2026.09.03.353742`; trigger,
route, currency split and the pre-existing hollow beat all established from source this session.
