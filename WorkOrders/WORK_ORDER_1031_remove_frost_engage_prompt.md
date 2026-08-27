# WORK ORDER 1031 — Remove the "Frost" task prompt from town (⚠ NOT the wolf — they are the SAME OBJECT)  — **OWNER CLOSED 2026-08-22** (felt-verified by the owner; PO closes, section 13).

**Status:** CLOSED 2026-08-27 — owner felt-tested PASS on APK 2026.08.27.343739 (village review).

*(Board note 2026-08-24: bucket corrected DONE/IMPLEMENTED → **FIXED**. Nothing about the work changed — §13 reserves DONE/closing for the PO, and this line's own text says the owner's felt-verify is still owed, so the row belongs in the felt-test queue, not the closed pile.)*

> ### VERIFIED AT SOURCE 2026-08-22 - the removal is **MACHINE-PINNED AS AN ABSENCE**, which is stronger than a grep
> `Assets/Editor/Regression/EchoEngageDialogueRegression.cs` is **INVERTED by WO-1031** (`:2`) and asserts the
> removal holds: no engage-prompt members on `PetTaskController`, no invented species-to-name table, no `pet_task`
> verb, and `CheckNoFrostSpeaker` (`:157-166`) FAILS if a `"name": "Frost"` speaker record ever returns to
> `dialogues.json`. Registered at `Assets/Editor/Regression/DataRegression.cs:1018` as the
> `echo-engage-dialogue suite`. A re-add now turns the suite RED - the absence cannot silently regress.


> Shipped in **6c880ea1b** — *"fix(dialogue): WO-1031 - delete the 'Frost' engage prompt; the wolf is Aldwin"*.
> `PetTaskController.BuildEngageDef` is **gone**, and its absence is PINNED by a dedicated oracle
> (`Assets/Editor/Regression/EchoEngageDialogueRegression.cs:120,143`), so the prompt cannot return
> unnoticed. `Assets/Editor/UICaptureLaunch.cs:871` was re-pointed off the deleted `pet_engage` builder
> in the same change.
>
> The status line sat at READY for a day after the work landed — the board is derived from this line, so
> it read as outstanding. Flip the status in the SAME commit as the work (CLAUDE.md §2).
**Minted:** 2026-08-16 (UI seat) — provenance stack bumped 1031 → 1032 in the same edit
**Lane:** Village / Pets engagement surface. ⚠ Touches the FTUE guide's object — read §2 before editing.
**Supersedes:** **WO-1030** on its player-facing goal (see §6). WO-1030 made the panel *render
correctly*; the owner wants the panel *gone*.
**Provenance:** owner 2026-08-16 — *"Diagnosis completely all references to frost FTUE or in town and
create WO to remove"*, plus F8 **seq=2432** `flagged`:
`"[Main_Castle_Overworld] Frost screen still here"`.

---

## 1. ⛔ STOP. READ THIS BEFORE YOU GREP-AND-DELETE

**"Frost" is not a separate character. "Frost" is the DISPLAY NAME of the `ice-wolf` species — and the
`ice-wolf` IS THE FTUE GUIDE WOLF.**

```
PetTaskController.SpeakerName(species)  :215-224
    case "ice-wolf":  return "Frost";
```

And the guide, from the regressions that pin it:

```
OneGuideBodyRegression.cs:13
  [Flow:Tutorial] step 'founding_greet' grant.starterPet - guide BODY summoned ('ice-wolf') at (2.00, 0.06, 3.00)
OneGuideBodyRegression.cs:15
  [Flow:Tutorial] FocusMask resolved highlightId=world.guide target=Pet_ice-wolf
FoundingGuideWolfBodyRegression.cs:69-74
  Species = "ice-wolf"; BodyPrefab = "Assets/Resources/Pets/ice-wolf.prefab"
```

⚠ **The owner ruled on 2026-08-16: *"the one for guide is the wolf.fbx and is the correct FTUE."***
A seat that removes "all references to Frost" **deletes the FTUE guide** — the single thing she has
explicitly confirmed is correct. That is the failure mode this section exists to prevent.

**What is being removed is the ENGAGE PROMPT SURFACE. The wolf stays. The guide stays. The FTUE stays.**

## 2. Complete reference map (traced 2026-08-16 — this is the "diagnose completely")

| # | reference | keep / remove |
|---|---|---|
| 1 | `PetTaskController.SpeakerName()` :215-224 — `ice-wolf → "Frost"` | **remove with the prompt** (it exists only to name the speaker in that dialogue) |
| 2 | `PetTaskController.BuildEngageDef()` :168-212 — the 2-choice def | **REMOVE** — this is the screen |
| 3 | `PetTaskController.Engage()` :147-161 | **REMOVE** — see §3, the auto-fire is the actual complaint |
| 4 | `PetTaskController.TickEngagement()` :120-145 — tap + **proximity** triggers | **REMOVE the trigger path** |
| 5 | `PetTaskController.ApplyEngagementChoice()` :236+ / the `pet_task` verb | **REMOVE** if nothing else routes to it — ⚠ **verify the verb has no other caller first** |
| 6 | `Resources/Pets/ice-wolf.prefab` + `.controller` | ⛔ **KEEP — THIS IS THE GUIDE WOLF BODY** |
| 7 | `FoundingGuideWolfBodyRegression` | ⛔ **KEEP** — pins that exactly one asset answers `Resources.Load("Pets/ice-wolf")` |
| 8 | `OneGuideBodyRegression` | ⛔ **KEEP** — pins the one-guide lock (`112d1c0dc`) |
| 9 | `GuideLeadMovementRegression` | ⛔ **KEEP** — pins guide-lead movement |
| 10 | `EchoEngageDialogueRegression` (new, from WO-1030) | **RETIRE with the prompt** — it asserts `ice-wolf` speaks as `"Frost"`, which becomes false by design. ⚠ It was written **yesterday**; do not leave it asserting a removed feature |
| 11 | `PetCatalogTest` / `EconomyMetaCatalogRegression` — `pet-ice-wolf` in the 3 starter species | ⛔ **KEEP** — catalog membership, unrelated to the prompt |
| 12 | `PetPortraitRenderer` :40-45 — renders `pet-ice-wolf.png` | **KEEP** (harmless); the portrait is used elsewhere in pet UI |
| 13 | `CombatAtbRegression` `PetSpecies.IceWolf` | ⛔ **KEEP** — battle-side, unrelated |
| 14 | `SaveSchema.StarterPetId` :236 | ⛔ **KEEP** — save field; removing it is a schema bump for no reason |

**Unrelated "Frost" — do not touch:** `Frost Golem` (enemy rig, `AnimatorSetup`), `Dungeon_FrostStair`
(`DungeonStubBuilder`), `Frost_Projectile` / `Frost_Impact` (Hovl VFX catalog), `Frost Nova` (mage
animator), `Frostfall` (starter bundle). These share the word and nothing else.

## 2b. ★ THE NAME "FROST" IS A CANON VIOLATION — found 2026-08-16, and it reframes this ticket

The ice Echo's canonical name is **Aldwin**, not Frost:

| source | line |
|---|---|
| `EchoRosterCatalog.cs:18` | *"echo #1 (**Aldwin**, the founding Ice Echo…)"* |
| `TutorialGuide.cs:20` | *"founding Echo (`EchoRosterCatalog.ByCount(1)` — **Aldwin, the Ice Echo**)"* |
| `TutorialGuide.cs:61-65` | guide speaker name e.g. **"Aldwin"**; card title **"Aldwin, the Ice Echo"** |
| `AutoPilotDriver.cs:3245` | objective strip reads *"Follow **Aldwin** to the gate"* |
| owner screenshot 2026-08-16 | tutorial banner: *"…ow **Aldwin** to the gate"* |

**So the tutorial calls him Aldwin — correctly — and then `PetTaskController.SpeakerName()` calls the
SAME character "Frost" the moment the player walks near him.** That is not a styling defect; the game
addresses one character by two names in the same scene.

⚠ **This is a second, independent reason the prompt must go**, and it retires the last argument for
keeping it. `SpeakerName()`'s whole species→name table (`ice-wolf → "Frost"`, `flame-pup → "Ember"`,
`aether-sprite → "Aether"`) is an **invented naming scheme that bypasses `EchoRosterCatalog`**, the
actual name authority. It is the "hand-authored instead of derived" pattern the 2026-08-06 canon thread
names as the recurring root cause.

⚠ **Alduin ≠ Aldwin.** `DungeonLoreReadableRegression:74-91` pins these as *different characters one
letter apart* — **Alduin the Mournful** (necromancer, dungeon lore) vs **Aldwin the Ice Echo** (WO-881).
**Do not "correct" one into the other.** If any replacement copy is written, spell it **Aldwin**.

## 2d. ⛔ OWNER RULING 2026-08-16 — THE WOLF IS ECHO #1. "Frost" was never a character.

> Owner, verbatim: **"the wolf isnt frost or shouldnt be its the first Echo"**

This **confirms §2b at the design level, not just the data level**. The guide wolf's identity is
**Echo #1 — Aldwin, the founding Ice Echo** (`EchoRosterCatalog.ByCount(1)`). "Frost" was a name
invented inside `PetTaskController.SpeakerName()` and never existed in the roster, the narrative bible,
or the tutorial copy.

**Consequence for naming:** anywhere the guide is addressed, it is **Aldwin** — the name the tutorial
already uses ("Follow Aldwin to the gate"). The species→name table dies with the prompt (§4c); nothing
should replace it. If a name is ever needed, read it from **`EchoRosterCatalog`**, the authority — do
not re-hand-author one. (The canon "derive it, don't hand-author it" pattern; `SpeakerName()` is a
textbook case.)

### ⚠ CONSEQUENCE FOR THE DESPAWN — flag before implementing §4

If the wolf **is** Echo #1, then the guide body and a **roster entry the player has earned** are the
same character. The HUD chip in the owner's screenshots reads **`Echoes 1/6`** — that 1 is Aldwin.

**The despawn must remove the BODY, not the Echo.**

- [ ] After despawn, the player still **has Echo #1 (Aldwin)** — `Echoes 1/6` unchanged, roster intact,
      the Echo tab still lists and can assign him
- [ ] No save state implying the Echo was lost, un-earned, or reset
- [ ] ⚠ Do not let the despawn path touch `EchoAssignments` or the Echo roster — it despawns a
      **world actor**, nothing more

**Getting this wrong silently deletes the player's first Echo** — and the Echo lane is a progression
pillar (`docs/DESIGN_REVIEW_COC_WC3_LENS_2026-08-15.md`). It would read as a save bug, not a tutorial
bug, and would be diagnosed far from here.

## 3. WHY it keeps appearing — the part that makes it feel broken

`PetTaskController.TickEngagement()` :137-145:

```csharp
// Auto-greet on approach: armed + close + not too soon since the last auto-greet.
if (_armed && dist <= EngageRadius && Time.time - _lastAutoEngage >= MinAutoInterval)
{ _armed = false; _lastAutoEngage = Time.time; Engage("proximity"); }
```

…and it **re-arms** whenever the hero walks away (`if (dist > RearmRadius) _armed = true;`).

So the prompt is **not** player-initiated. The FTUE grants the starter pet
(`grant.starterPet`, `TutorialStepModel.cs:73`), that wolf remains in town, and **every time the player
walks near their own guide the panel takes over the screen.** That is why it reads as "still here" —
it is unsolicited, repeating, and modal, in the middle of the hub the player is trying to use.

⚠ **This is the root of the complaint, not the panel's styling.** WO-1030 improved how the panel
*looked*. The defect is that it *appears at all, uninvited*.

## 4. ★ OWNER RULING RECEIVED 2026-08-16 — THE WOLF DESPAWNS. This is the real fix.

> **"after tutorial ends (cancel) or placed defensive structure the wolf should despawn"**

**This supersedes the §4b options below and is structurally better than all of them.** The guide is a
**tutorial actor with a defined exit**, not a permanent town resident. Once it leaves, the "Frost
screen" cannot occur in town at all — the trigger has no subject. That removes the defect by removing
the *cause* rather than suppressing the *symptom*.

### The despawn contract — TWO independent triggers, whichever fires first

| # | trigger | note |
|---|---|---|
| 1 | **Tutorial ends** — including the **CANCEL / Skip** path | ⚠ Cancel is the one that gets missed. A player who skips must not be left with a permanent wolf; test the skip path explicitly |
| 2 | **A defensive structure is placed** | The FTUE's build objective. Fires even if the tutorial is still nominally running |

**Whichever comes first wins, and the despawn must be idempotent** — both firing must not double-despawn
or throw.

### Requirements

- **Despawn cleanly.** ⚠ Tear down the guide's own VFX/attachments with it — memory
  `destroyed-items-no-rebuild-full-cost-and-vfx-cleanup`: one owner tears down its own VFX on death.
  A leaked effect where the wolf stood is the predictable failure here.
- **Persist the despawn.** It must not respawn on reload. ⚠ `SaveSchema.StarterPetId` (`:236`) is the
  field to reason about — **do not delete it**, but ensure a despawned guide does not re-summon from it
  on load. Record which flag carries the state.
- **⚠ Do NOT break the `founding_walk` step.** The guide must survive long enough to lead the hero to
  the gate. There is an **open `[Flow:Tutorial] STEP-STUCK :: founding_walk`** (F8 seq 2343, no
  `hero.reached:guide_gate` after 241s) — a despawn that fires early turns that intermittent stall into
  a permanent hard block on the first minute of the game. **Verify the walk step still completes before
  and after this change.**
- **Instrument it.** `FlowTrace.Step("Pets", "guide despawned — trigger=<tutorial_end|cancel|defense_placed>")`.
  This is a lifecycle with two entry points and a save interaction; §12 says the trace is what makes the
  first bug cheap.

### What this does to the rest of this WO

- §2 items **1–5** (the `SpeakerName` table, `BuildEngageDef`, `Engage`, the proximity trigger,
  `ApplyEngagementChoice`) — still **REMOVE**. Despawn stops it in town; removal stops it *everywhere*,
  including any future context where a pet is deployed. **Do both.** Despawn alone leaves the invented
  "Frost" naming scheme (§2b) live and reachable.
- §2 items **6–9** (wolf prefab + the three guide regressions) — ⛔ still **KEEP, UNMODIFIED**.
- The §4b table below is retained as **history only** — the owner has ruled.

### Additional acceptance criteria

- [ ] Wolf despawns when the tutorial **completes**
- [ ] Wolf despawns when the tutorial is **cancelled / skipped** — test this path explicitly
- [ ] Wolf despawns when a **defensive structure is placed**, even mid-tutorial
- [ ] Both triggers firing is safe (idempotent, no double-despawn, no exception)
- [ ] Despawn **persists across save/reload** — no respawn
- [ ] **No leaked VFX or attachments** at the despawn point
- [ ] ⚠ `founding_walk` still completes — the guide survives until `hero.reached:guide_gate`
- [ ] The despawn `FlowTrace` line, with its trigger, is pasted in the RESULT

## 4c. ⛔ FINAL RULING 2026-08-16 — REMOVE THE SCREEN. UNCONDITIONAL. No options remain.

> Owner, verbatim: **"remove this screen then"** — and, on where the function lives:
> **"it gets managed from the echo tab"**

**Every open question in this WO is now closed.** There is no "keep tap, drop proximity" variant, no
phased option, no conditional. The prompt is **deleted**, both trigger paths with it.

### Verified: removal loses NO functionality (checked at source 2026-08-16)

The tasking the prompt offered already has a real home — this dialogue was a **redundant second entry
point**, not the only one:

| surface | evidence |
|---|---|
| `EchoAssignments.cs:14-16` | lane tokens `harvest` / `crafting` / `defense` / `exploration` / `repair` — the same two the prompt offered, plus three it never could |
| `EchoAssignments.cs:17` | WO-830 per-Echo harvest RESOURCE picker — **richer** than the prompt's bare "Gather resources" |
| `EchoRepairService` (WO-811) | the repair lane advances real structure repair |
| `EchoCardView.cs` / `EchoCardVM.cs` | the Echo tab UI that owns assignment |

So the prompt exposed **2 of 5 lanes** with no resource choice, while the Echo tab exposes all five with
the WO-830 picker. It was strictly the weaker surface. **One home for tasking, and it is the Echo tab.**

⚠ **This also removes a canon violation by construction:** the prompt was the only caller of
`SpeakerName()`'s invented `ice-wolf → "Frost"` table (§2b). Deleting the prompt deletes the last place
the game calls Aldwin by the wrong name.

### The removal list is §2 items 1–5, unconditional

`SpeakerName()` · `BuildEngageDef()` · `Engage()` · `TickEngagement()`'s trigger path (**both** tap and
proximity) · `ApplyEngagementChoice()` + the `pet_task` verb (⚠ still verify no other caller first).

⛔ **§2 items 6–14 remain KEEP, UNMODIFIED** — above all the wolf body and the three guide regressions.
The §4 despawn ruling still stands and is complementary: **despawn removes the wolf from town; this
removes the prompt everywhere.** Do both.

### Additional acceptance criteria

- [ ] Tapping the guide wolf does **nothing** — no dialogue, no prompt, no toast
- [ ] Walking near it does **nothing**
- [ ] Echo harvest/repair assignment still fully reachable **from the Echo tab**, all five lanes plus
      the WO-830 resource picker — ⚠ **verify this explicitly**; it is now the ONLY path
- [ ] No orphaned `pet_task` verb left registered with no producer
- [ ] `SpeakerName()` is gone — grep proves no `ice-wolf → "Frost"` mapping survives anywhere

## 4b. (HISTORY — superseded by §4 and §4c) Options considered before the rulings

The prompt is the only surface that assigns a pet to **harvest** vs **repair**. Removing it removes
that control. Three outcomes:

| option | result |
|---|---|
| **(a) Remove prompt, keep assignment elsewhere** | Echo tasking already lives in the Echoes/Manage screens — route it there. **Recommended:** one home for tasking, no world-modal |
| **(b) Remove prompt and the tasking entirely** | Simplest. ⚠ Only if pet harvest/repair is not a live mechanic — it **is** (`EchoRepairService`, `EchoAssignments`), so this likely loses real function |
| **(c) Keep tap, remove proximity only** | Smallest change: the panel appears only when deliberately tapped. Kills the ambush without losing the control |

**Recommendation: (c) as the immediate fix, (a) as the destination.** (c) stops the intrusion in one
line-level change and cannot lose functionality; (a) then consolidates tasking into the screen that
already owns it. ⚠ **Do not do (b) without an explicit owner ruling** — it deletes a live mechanic.

## 5. Acceptance criteria

- [ ] Walking anywhere in town near the guide wolf **never** auto-opens a dialogue
- [ ] ⛔ **The FTUE still completes with the wolf as the guide** — `founding_greet` summons the
      `ice-wolf` body, `FocusMask` still resolves `world.guide → Pet_ice-wolf`
- [ ] `FoundingGuideWolfBodyRegression`, `OneGuideBodyRegression`, `GuideLeadMovementRegression` all
      still **PASS UNMODIFIED** — ⚠ if any needs editing to pass, the wolf was damaged; **stop and revert**
- [ ] `EchoEngageDialogueRegression` is retired or rewritten — it must not assert a removed feature
- [ ] Pet harvest/repair assignment remains reachable per the §4 ruling
- [ ] `Resources/Pets/ice-wolf.prefab` + `.controller` untouched
- [ ] No save-schema change (`StarterPetId` stays)

## 6. Relationship to WO-1030 (do not silently orphan it)

WO-1030 is **IMPLEMENTED** (`323f3c97f`) and fixed two real defects in `DialogueView`:

- **Defect A** — the options clamp (`bodyPx` capped by `_maxBodyPx`, starving the option list in
  landscape). ⛔ **KEEP THIS FIX.** It is in the **shared** `DialogueView`, which is the canon
  reference implementation (`UI_BLINK_TEMPLATE_CANON.md` §8) — **every dialogue in the game** benefits,
  not just this prompt. Removing the Frost screen does not make that bug go away anywhere else.
- **Defect B** — the portrait resolver's display-name-vs-id key mismatch. **KEEP** for the same reason.

**Only the Frost prompt itself is removed.** Update WO-1030's RESULT to note its player-facing trigger
is retired while its `DialogueView` fixes remain load-bearing — so nobody later "reverts WO-1030" and
silently re-breaks option clipping across every conversation in the game.

## 7. Verify

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` — with the three guide regressions **green and
   unmodified**
2. Headless: run the FTUE start-to-finish; assert the guide summons and the walk step completes
3. Walk the hub near the wolf repeatedly — **no dialogue**
4. Owner felt-verifies + closes (§13)
