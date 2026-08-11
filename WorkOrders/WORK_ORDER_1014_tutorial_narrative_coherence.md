# WORK ORDER 1014 — Tutorial narrative coherence: ONE guide, one identity, one arc

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-10 (UI seat) — provenance stack bumped 1014 → 1015 in the same edit
**Lane:** Tutorial content/narrative + guide behaviour. **Companion to WO-1012** (which owns the
presentation kit + pacing); this WO owns WHO THE GUIDE IS and WHAT THE SCRIPT SAYS.
**Provenance:** owner felt-test 2026-08-10, verbatim: *"The wolf is supposed to guide it, however the
story line in[trodu]ces himself as Storm or something and a NPC and wolf both spawn together. There is no
knowledge of who this wolf is. The intro is badly composed."* · *"then when you are walking back towards
tree the wolf asks what to do, but never explained that's what it does"* · *"then as you walk to the
entrance (wolf is supposed to lead, but doesn't move) introduces player to another wolf."*
**Depends on:** WO-1012 (pet-Echo guide ruling + presentation kit), WO-961 (gave the founding guide a
walking ice-wolf body), the `{guide}` runtime token (`TutorialGuide`), `PetHeroLeash.SetLeadTarget`,
`TutorialWorldAnchors` (`world.guide`, `guide_gate`), `tutorial-steps.json`, `dialogues.json`.

---

## 1. ROOT CAUSE (verified at source 2026-08-10 — this is the whole bug)

**TWO COMPLETE, CONTRADICTORY TUTORIAL SCRIPTS ARE LIVE AT THE SAME TIME** in
`Assets/StreamingAssets/Data/Canonical/dialogue/dialogues.json`:

| Arc | Dialogue ids | Speaker | Status |
|---|---|---|---|
| **LEGACY "Sylas" arc** | `tut_move_to_sylas`, `tut_meet_sylas`, `tut_first_tower`, `tut_first_tower_done`, `tut_world_encounter*`, `tut_return_home`, `tut_freedom` | **hard-coded `"Sylas"`** — a human Scout of the Reach who self-introduces ("Sylas, Scout of the Reach") | **MUST BE RETIRED** |
| **CURRENT founding arc** | `tut_founding_greet`, `tut_founding_hollow(+_done)`, `tut_founding_stores`, `tut_founding_town`, `tut_founding_echo`, `tut_founding_defense`, `tut_town_wave(+_done)`, `tut_ctx_*` | `{guide}` token → the pet-Echo (WO-961 ice-wolf body) | KEEP + fix |

Every reported symptom falls out of this one fact:
- **"A NPC and wolf both spawn together"** → the legacy arc spawns the Sylas human; the founding arc
  spawns the pet-Echo wolf. Both are armed.
- **"Introduces himself as Storm or something"** → a name collision in the mixed script. NOTE: the
  Echo roster has a **`Storm` affinity** and `echo-stormcoil-serpent` (`EchoRosterCatalog.cs:56/186`) —
  the wolf is picking up a roster/affinity name, not an authored guide name. **The wolf has NO canon
  name today.**
- **"No knowledge of who this wolf is"** → `tut_founding_greet` opens with *"This is your castle - and
  this tree is what we defend"* — there is **no self-introduction beat at all**. The legacy arc HAD one
  (Sylas introduces himself); the founding arc never got one written.
- **"The wolf asks what to do, but never explained that's what it does"** → the utility line
  (*"I can farm. I can mend. Put me to work, Keeper."*) is buried as the THIRD line of
  `tut_founding_hollow_done`, i.e. AFTER the ask lands. Ordering defect.
- **"Wolf is supposed to lead, but doesn't move"** → `founding_walk` declares the lead
  (`PetHeroLeash.SetLeadTarget` re-asserted by `TutorialFlow.TickProximityProbe`) but the body does not
  path. Runtime defect — instrument before fixing (§12).
- **"Introduces player to another wolf"** → a SECOND wolf at the entrance: either the legacy arc's NPC
  slot now dressed as a wolf, or a duplicate pet spawn. Must resolve to ONE wolf, ever.

## 1c. ✅ CONFIRMED IN-GAME + OWNER RULING — F8 seq=2316 (2026-08-10 20:30, `Main_Castle_Overworld`)

Owner flag, verbatim: **"PET and NPC. Remove NPC"**. This is §1's two-live-arcs defect reproduced in the
HUB scene, with the fix ruled.

**Harvested trace — the guide side is CORRECT:**
```
[Flow:Tutorial] FocusMask SHOW highlightId=world.guide style=Gesture
[Flow:Tutorial] FocusMask resolved highlightId=world.guide target=Pet_ice-wolf style=Gesture rect=(851,246,120,120)
[Flow:Tutorial] GuidePointer SHOW chevron highlightId=world.guide
[Flow:Tutorial] GuidePointer SHOW chevron highlightId=world.gate_direction
```
`world.guide` resolves to **`Pet_ice-wolf`** — exactly the WO-1012 pet-Echo ruling, working. **The pet is
right; the problem is the NPC standing next to it.** So this is not a resolution bug — a SECOND body is
being spawned by the legacy path.

**OWNER RULING: remove the NPC. The pet-Echo is the sole guide.** No dialogue re-point, no "hide it for
now" — the legacy guide NPC does not spawn in the tutorial at all. This makes §2a (retire the legacy
Sylas arc) the P0 slice of this WO, and it satisfies §2e (one guide body, ever) at the same time.
CLI: find what spawns the NPC in `Main_Castle_Overworld` (scene-baked object vs the legacy arc's spawner)
and remove that path — then the "exactly one guide body" regression proves it stays gone.

**Owner screenshot, same session — the pet dialogue is up and shows THREE more items:**
- **The wolf is now named "Frost" — ⚠ SAME NAME-LEAK CLASS AS "Storm" (§1).** `Frost` is an **Echo
  AFFINITY** in `EchoRosterCatalog` (Aldwin/Frost), not an authored guide name. The guide is still
  drawing identity from the affinity roster. **§5's open question is NOT resolved by this** — CLI must
  confirm whether "Frost" is authored-on-purpose or leaked; if leaked, the fix is the same as Storm.
- **The portrait is a generic placeholder silhouette, not a wolf** — the guide has no portrait art bound.
  A guide with a stranger's silhouette undercuts the whole "who is this wolf" fix. Bind the pet's real
  portrait (or its Echo card art).
- **`Echoes 1/6` chip overlaps the `Skip` button** at right — the same HUD-chip-over-chrome class as
  WO-1010 D7/D18. Chips need suppressing (or z-ordering under) while tutorial chrome/dialogue is up.

**⚠⚠ SECOND SCREENSHOT — THE DEFECT IS VISIBLE, AND THE NAME IS INCONSISTENT ON ONE SCREEN:**
- **Both bodies are in frame, sharing one highlight:** a **human NPC** (bearded villager in vest + hat)
  standing AT the Heart with the **white wolf pet** beside him, both inside the same gold guide glow.
  That is the owner's "PET and NPC" exactly. **Remove the human.**
- **THE GUIDE HAS TWO DIFFERENT NAMES ON SCREEN IN THE SAME SESSION:** the dialogue speaker read
  **"Frost"**, the objective strip reads **"Follow Aldwin to the gate"**. Per `EchoRosterCatalog` those
  are the SAME roster row — **`Aldwin` is the Echo's NAME and `Frost` is its AFFINITY** (Aldwin/Frost ->
  Food). So one surface prints the name and another prints the affinity. **This is the root of the
  "Storm"/"Frost" confusion in §1 — not a random leak, but two surfaces reading different FIELDS of the
  same row.** Fix: ONE resolver for the guide's display name (`{guide}` token) that always returns the
  NAME field; never the affinity. Add a regression asserting dialogue-speaker == objective-name.
- ⇒ **§5's open question is now sharper, and cheaper:** the wolf may already HAVE a canon name —
  **Aldwin** — inherited from the starter Echo. Owner: adopt `Aldwin` as the guide's name (and never
  show `Frost` as a name), or author a distinct one? Either way the affinity must stop appearing as a
  name.

**Also shipped and working (visible in this shot):** WO-1012's **thin bottom objective strip with
progress beads** (`Follow Aldwin to the gate` + 8 beads, first filled) is live and looks right — the fat
top banner is gone. That is the redesign landing.

**Credit where due — §2c is partly fixed:** the line *"Keeper, I'm at your side. What should I tend to?"*
now presents **`Gather resources` / `Repair structures`** as explicit choices, so the ask carries its own
explanation. That satisfies the spirit of §2c at the point of the ask; the remaining §2c work is the
IDENTITY beat coming BEFORE it (who the wolf is, stated once, first).

**Side note, good news:** the trace shows WO-1012's presentation kit is LIVE and instrumented
(`FocusMask`, `GuidePointer`, `style=Gesture`, resolved rects). The P1 skin shipped.

## 1d. §2d PROVEN BY DATA — the walk beat times out because the guide never leads (F8 seq=2318)

Harness ERROR, same session:
```
[Flow:Tutorial] STEP-STUCK :: founding_walk — no 'hero.reached:guide_gate' after 120s in-step
  (bound 120s, builder time excluded; ff.tutorialv2 on; builderOpenedThisStep=False, coachBeats=2);
  RESCUED via watchdog and recorded as SKIPPED - the step was NOT completed
```
**This is the owner's *"wolf is supposed to lead, but doesn't move"* (§1), now with a hard proof line.**
The player never reaches `guide_gate`, the step hangs the full 120s, and the watchdog SKIPS it — so a
first-time player silently loses the walk beat entirely. §2d moves from "verify" to **CONFIRMED DEFECT,
fix required**. Note `builderOpenedThisStep=False` rules out the build-menu detour as the cause.
⚠ The watchdog rescue is doing its job, but a **skipped** beat is not a passed one — do not let the
rescue mask this in future runs; the acceptance criterion is the signal firing, not the watchdog saving.

**F8 seq=2317** (*"Now says frost"*) is the same naming defect as §1c — the objective strip changed from
`Aldwin` to `Frost` mid-session, which is exactly the two-surfaces-two-fields bug. Covered above.

## 2. What to do

### 2a. RETIRE the legacy Sylas arc (the single highest-value fix)
- Remove/disable every `tut_move_to_sylas` / `tut_meet_sylas` / `tut_first_tower*` / `tut_world_encounter*`
  / `tut_return_home` / `tut_freedom` dialogue AND whatever arms them (step defs, scene wiring, the NPC
  spawn). **One arc may be live; the founding arc is it.**
- ⚠ Do not delete blind: some legacy lines teach genuinely useful ideas (the town-vs-open-world
  distinction in `tut_return_home` — *"Two fights, Keeper"*). **Salvage the IDEAS into the founding arc
  as later contextual one-shots**, re-voiced as the wolf. Retire the SPEAKER and the arc, not the wisdom.
- Sylas remains a real hero name (`HeroCanonNames`) — retiring the arc must NOT break the hero roster or
  any non-tutorial use.

### 2b. GIVE THE WOLF AN IDENTITY (new content — the missing beat)
The wolf is an **Echo of Elarion** (WO-1012 ruling): the awakened essence of one of the people the Heart
guarded, returning in a wolf's form. Author a **self-introduction as the FIRST beat**, before any
instruction:
- It wakes at the Heart, approaches, and says who/what it is — name, what it is, why it is here — in the
  established voice (short, grounded, lightly archaic; the owner's voice pass sets final copy).
- **NAME RULING NEEDED (owner):** the wolf currently has no authored name and is accidentally reading a
  roster affinity ("Storm"). Options: (a) author a canon name; (b) deliberately keep it unnamed —
  "the Echo" — and let the player name it later; (c) adopt "Storm" as canon and reserve it in the roster
  so nothing else claims it. **CLI: do not invent a name — bounce to the owner.**
- The `{guide}` token must resolve to that identity everywhere (objectives already read
  "Meet {guide} beneath the tree").

### 2c. FIX THE ORDERING — utility explained BEFORE the ask
Move the *"I can farm. I can mend. Put me to work, Keeper."* line OUT of `tut_founding_hollow_done`'s
third slot and into the identity beat (2b) or immediately after it — the wolf states what it can do
**before** it ever asks for an order. Then the ask reads as an offer the player understands.

### 2d. FIX THE LEAD (runtime)
`founding_walk` must have the wolf actually WALK to `guide_gate` with the player following.
**Instrument first (§12):** trace `PetHeroLeash.SetLeadTarget` → agent path → arrival; read whether the
target is set, whether the agent has a path, whether something (leash follow-mode? nav?) overrides it.
Fix the step the data names — do not guess.

### 2e. ONE WOLF, EVER
Exactly one guide body may exist in the tutorial. Guard it: the spawn path must be idempotent, and any
second wolf/NPC introduction beat is removed with the legacy arc. Add a regression that fails if two
guide bodies are alive simultaneously.

### 2f. RE-COMPOSE THE INTRO (the owner's "badly composed")
Final beat order for the opening, consistent with WO-1012's arc:
1. **WAKE + IDENTITY** — the wolf-Echo wakes at the Heart, walks to the player, says who it is and what
   it can do. (No instruction yet.)
2. **THE PLACE** — one line on what the Heart is and what was lost (the existing greet copy, trimmed).
3. **WALK** — "Come with me" → the wolf LEADS to the gate (2d).
4. …then WO-1012's arc continues (build one → ack → one cannon → timers → gate defense → win).
No second NPC. No second wolf. No name drift.

## 3. Constraints

- **Copy is content, not code:** all lines live in `dialogues.json`; the owner does the final voice pass.
  CLI wires structure and may draft placeholders clearly marked `<<DRAFT — owner voice pass>>`.
- ASCII only in TMP strings; `{guide}` stays a runtime token (never bake a literal name into copy).
- No new dialogue system, no new spawn system. Use the existing rails.
- **Instrument (§12):** `[Flow:Tutorial]` lines for guide-spawned (with body id + count), lead-target-set,
  lead-arrived, each dialogue id fired — so "two guides" or "no movement" is provable from one capture.
- WO-1012's presentation kit and WO-1013's plans beat are NOT re-litigated here.

## 4. Acceptance criteria

- [ ] Exactly ONE guide body spawns in the tutorial, ever (regression-pinned; capture-proven).
- [ ] No legacy Sylas tutorial dialogue can fire; no human scout NPC spawns for the tutorial.
- [ ] The wolf introduces itself — who it is, what it is, why it is here — as the FIRST spoken beat.
- [ ] The wolf's utility is stated BEFORE it asks for an order.
- [ ] The wolf physically LEADS the walk beat; the player following it completes the step.
- [ ] No "another wolf" introduction anywhere in the flow.
- [ ] `{guide}` resolves to one consistent identity in every objective + line; no "Storm" leakage from
      the Echo affinity roster (unless the owner rules Storm canon per 2b).
- [ ] Salvaged ideas (town-vs-open-world) survive as contextual one-shots in the wolf's voice.
- [ ] `[Flow:Tutorial]` capture shows: one spawn, lead set, lead arrived, dialogue ids in the new order.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` + `UI_CAPTURE_OK`, then an owner felt-test of
      the first 3 minutes: target verdict "I know who this wolf is and it led me."

## 5. Open question for the owner (blocks 2b only)

1. **The wolf's name.** It has none authored today and is accidentally surfacing "Storm" from the Echo
   affinity roster. Author a canon name / keep it "the Echo" unnamed / adopt Storm as canon? CLI must
   not invent one.

## 6. What NOT to touch

- The hero roster / `HeroCanonNames` (Sylas stays a hero — only the tutorial ARC retires).
- WO-1012's presentation kit + pacing, WO-1013's plans beat, WO-1010's build UI.
- Wave balance, the Onboarded gate, the peace window.
