# WORK ORDER 1332 - RESULT

**Status:** BLOCKED - premise refuted; no rename performed; the pin oracle was widened.
**Date:** 2026-09-02
**Files changed:** 1 (`Assets/Editor/Regression/DungeonLoreReadableRegression.cs`) + this WO's banner.
**Files renamed:** ZERO.

---

## 1. The headline

**The repo can spell the wolf's name. There is no split.**

`Alduin` and `Aldwin` are two separately-authored characters whose names differ by one letter:

| Name | Who | Authority |
|---|---|---|
| **Alduin the Mournful** | The Necromancer boss - "He was a healer, once" | `Assets/Resources/Data/Canonical/canon-strings.json:26-27`; `docs/narrative-bible.md:57` (section "The leader"), `:367` (character table: "The Necromancer boss; once a healer who tried to seal the Wound") |
| **Aldwin, the Ice Echo** | Echo #1, the founding companion / tutorial guide wolf | `Assets/_Modules/Village/Harvest/EchoRosterCatalog.cs:149` |

The ticket's 34-vs-30 measurement counted **two different people's names** and read the tally as one
name spelled two ways.

## 2. The proof: zero crossover

Every occurrence in `Assets/` (43 files; `*.json *.cs *.yarn *.asset *.unity *.prefab`) was classified
by context. **Not one is wrong.** Cross-context greps return only the pinning oracles themselves:

```
grep -rniIE "alduin" Assets/ | grep -iE "wolf|echo|ice|guide|companion|pet"
  -> only DungeonLoreReadableRegression / EchoEngageDialogueRegression (the pins), plus
     lore-fragments.json's _comment and en.json's boss line (matched on "Bryn"/"boss",
     not on any echo copy)
grep -rniIE "aldwin" Assets/ | grep -iE "necro|mournful|boss|journal"
  -> only DungeonLoreReadableRegression (the pin)
```

Representative correct-in-context sites:

- **Alduin (boss):** `en.json:50,72,90,133` (wave warning / heart voice / victory / first-boss-kill);
  `enemies.json:212,228` ("Alduin's Necromancer"); `lore-fragments.json:26,37,48,60,73`
  (`"speaker": "Alduin the Mournful"`); `dungeons/healers-cottage.json:216,226,236,248` (journal
  entries); `GameStrings_en.asset:176,249,309,469`.
- **Aldwin (Echo #1):** `en.json:118,119` (`petAmbient.iceWolf.*`), `:127` (`keeperAmbient.6`);
  `glossary.json:34,99`; `guide-content.json:214,248`; `EchoRosterCatalog.cs:149,156`.

## 3. Why this ticket must not be executed

Executing it would:

1. **Attribute a necromancer's journal to the player's founding companion** (the Healer's Cottage
   lore stones are Alduin the Mournful's - `docs/DUNGEON_DESIGNS.md` D2: "the journal she's been
   reading is Alduin's").
2. **Break two shipped suites written to forbid exactly this correction:**
   - `Assets/Editor/Regression/DungeonLoreReadableRegression.cs` section 5 (WO-881) - fails if the
     lore copy source names `Aldwin`.
   - `Assets/Editor/Regression/EchoEngageDialogueRegression.cs:167-168` - *"Note Aldwin != Alduin
     the Mournful - do not correct one into the other"*.

## 4. This is the SECOND minting of the same false premise, in the OPPOSITE direction

`WorkOrders/WORK_ORDER_881_lore_modal_scroll_and_name.md:10-24` carries a correction banner dated
**2026-08-05**: WO-881 sections 1-3 asked to rename **Alduin -> Aldwin** and were **NOT ACTIONED** for
the same reason. WO-1332 asks for **Aldwin -> Alduin**. The name pair is a recurring attractor for
find-and-replace; that is why section 5b (below) now guards every file rather than three.

## 5. The occurrences deliberately LEFT as `Aldwin`

Per the trap section, everything left is named. **In this ticket the reason is uniform and stronger
than "it is an id": every single one is CORRECT PROSE about a different character.**

### 5a. Player-facing prose - CORRECT AS AUTHORED (Echo #1's name)

| file:line | text | reason |
|---|---|---|
| `Assets/Resources/Data/Canonical/en.json:118` | `petAmbient.iceWolf.1` "Aldwin came back from the orchard with ice on his muzzle." | ice-wolf ambient line; Echo #1 |
| `Assets/Resources/Data/Canonical/en.json:119` | `petAmbient.iceWolf.2` "Aldwin pads silently..." | ice-wolf ambient line; Echo #1 |
| `Assets/Resources/Data/Canonical/en.json:127` | `keeperAmbient.6` "Aldwin came back from the orchard..." | keeper ambient about the wolf; Echo #1 |
| `Assets/Resources/Data/Canonical/glossary.json:34` | "There are six -- Aldwin, Elowen, Corvin, Bran, Doran and Maren" | Echo roster |
| `Assets/Resources/Data/Canonical/glossary.json:99` | "Provisions (Elowen and Aldwin)" | Echo synergy pair |
| `Assets/Resources/Data/Canonical/guide-content.json:214` | "There are six in all: Aldwin, Elowen, ..." | Echo roster |
| `Assets/Resources/Data/Canonical/guide-content.json:248` | "Provisions (Elowen and Aldwin)" | Echo synergy pair |
| `Assets/_Modules/Village/Harvest/EchoRosterCatalog.cs:149` | `DisplayName = "Aldwin, the Ice Echo"` | **the name authority for Echo #1** |
| `Assets/_Modules/Village/Harvest/EchoRosterCatalog.cs:156` | Flavor: "...I was the first it kept -- Aldwin, a keeper of the old light..." | Echo #1's founding lore |

*(each `Assets/Resources/Data/Canonical/*` row above is duplicated at the same content in its
`Assets/StreamingAssets/Data/Canonical/` twin - both sides left untouched and byte-identical.)*

### 5b. Comments / test fixtures / capture tags - CORRECT, and several are load-bearing

| file:line | reason |
|---|---|
| `Assets/Resources/Data/Canonical/dialogue/dialogues.json:95` | `_note`: "the guide wolf is Echo #1, Aldwin" - correct |
| `Assets/_Modules/Core/Tutorial/TutorialGuide.cs:20,61,65` | doc comments naming the guide's identity - correct |
| `Assets/_Modules/Village/Tutorial/V2/TutorialGuideIdentityInstaller.cs:7,35,70` | derives the short speaker name from the roster - correct |
| `Assets/_Modules/Village/Tutorial/V2/TutorialFlow.cs:1739`, `TutorialWorldAnchors.cs:369` | comments - correct |
| `Assets/_Modules/Village/Progression/SpirePlansCelebration.cs:10,26,189` | comments: speaker is Echo #1 - correct |
| `Assets/_Modules/Village/Harvest/EchoRosterCatalog.cs:18,20,142`; `EchoService.cs:65,590` | comments - correct |
| `Assets/_Modules/Pets/PetDeployer.cs:670`; `Assets/_Modules/DevTools/AutoPilotDriver.cs:3352` | comments - correct |
| `Assets/Editor/Regression/EchoResourcePickerRegression.cs:269,276,277,280,294,296,298` | **assertion strings + a local `aldwinResource`** - renaming would break the pair test |
| `Assets/Editor/Regression/EchoSpecializationRegression.cs:14,72,388,426,436` | affinity-table assertions - correct |
| `Assets/Editor/Regression/TutorialCoachEscalationRegression.cs:240,244` | **asserts the literal objective string "Follow Aldwin to the gate"** - renaming breaks the suite |
| `Assets/Editor/Regression/FoundingGuideWolfBodyRegression.cs:508,516`; `OneGuideBodyRegression.cs:17` | comments - correct |
| `Assets/Editor/Regression/EchoEngageDialogueRegression.cs:24,27,134,165-168` | **the pin that forbids this rename** |
| `Assets/Editor/Regression/DungeonLoreReadableRegression.cs:12-15`, section 5 | **the pin that forbids this rename** |
| `Assets/Editor/UICaptureLaunch.cs:1000,1032,1064,1094,1135,1606,1647,1648` | capture **filename tags** (`EchoUnlockDialogue_Aldwin_...png`, `DialogueCompact_Aldwin`) + fixture speakers - renaming orphans the golden-capture names |
| `Assets/Tests/EditMode/EchoRosterVMTests.cs:64` | comment - correct |

### 5c. Ids / keys - untouched, and note the direction

The only id-shaped hits carry the **boss's** lowercase name, and were also left alone:
`Assets/_Modules/Core/Enemies/EnemyResolver.cs:257` (`["alduin"] = new EnemyClass`) and `:260`
(`Variant = "alduin"`); `canon-strings.json:26-27` keys `"alduin"` / `"alduinTitle"`. These are
catalog / variant keys - live save-adjacent data, never renamed.

## 6. Twins

All eight canonical files that name either character were byte-compared before and after; **all eight
are IDENTICAL across `Assets/Resources/Data/Canonical/` and `Assets/StreamingAssets/Data/Canonical/`**
(`canon-strings.json`, `dialogue/dialogues.json`, `dungeons/healers-cottage.json`, `en.json`,
`enemies.json`, `glossary.json`, `guide-content.json`, `lore-fragments.json`). No data file was
modified by this ticket.

**`.yarn` dialogue files:** none exist in the repo (`find . -name "*.yarn"` returns empty). Dialogue
lives in `Assets/Resources/Data/Canonical/dialogue/dialogues.json`.

## 7. The oracle (acceptance item 4)

`DungeonLoreReadableRegression` section 5 already pinned three sources (`lore-fragments.json` on both
twins, `canon-strings.json` Resources side, `EchoRosterCatalog.cs`) and the View. **That is precisely
why WO-1332 was minable: the five files a repo-wide sweep would actually have rewritten were
unguarded.**

Added **section 5b** to the same suite (one owner, no second system):

- **Single-sided files**, checked on **both twins**, each with a required name and a forbidden one:
  `enemies.json` (+`Alduin` / -`Aldwin`), `dungeons/healers-cottage.json` (+`Alduin's journal` /
  -`Aldwin`), `canon-strings.json` (+`Alduin the Mournful` / -`Aldwin`), `glossary.json` (+`Aldwin` /
  -`Alduin`), `guide-content.json` (+`Aldwin` / -`Alduin`).
- **`en.json` carries both names**, so it is pinned **per line by key**: a line whose key matches
  `iceWolf`/`keeperAmbient` may not say `Alduin`; a line whose key matches `boss`/`victory`/`milestone`
  may not say `Aldwin`; and **both halves must still be present**, so a deletion cannot pass either.

### The RED proof (mutation)

The section-5b predicate was ported verbatim to a standalone harness and run against the real files
(no Unity gate was run - the lead gates):

```
BASELINE            -> DUNGEON_LORE_OK (5b)

MUTATION: Assets/Resources/Data/Canonical/en.json:118
          "petAmbient.iceWolf.1": "Aldwin came back...  ->  "Alduin came back...
          (exactly the rename WO-1332 asked for)

AFTER MUTATION      -> DUNGEON_LORE_FAIL:
    en.json (Canonical) names 'Alduin' on an ECHO line:
      "petAmbient.iceWolf.1": "Alduin came back from the orchard with ice on his muzzle. You did not ask.",

RESTORED            -> DUNGEON_LORE_OK (5b)
                       twin byte-compare: IDENTICAL
                       git status: en.json clean
```

## 8. Gate hygiene

```
Assets/Editor/Regression/DungeonLoreReadableRegression.cs   BALANCED   clean (no NUL)
```

No Unity gate run, no `git add`, no commit (per the work order routing). WO-1330's files
(`RemoteTunables.cs`, `abilities.json`, `hero-talents.json`, `RemoteTunablesDefaultsRegression.cs`)
were not touched. WO-1326's wolf art / materials / textures were not touched.

## 9. Adjacent finding, NOT actioned (out of scope: "fix the name, nothing else")

Player-facing canonical copy is **not ASCII-only today** - e.g. `lore-fragments.json:27` and
`dungeons/healers-cottage.json:216` use an em dash (U+2014) inside displayed titles
(`"Alduin's journal - entry 1"` is authored with U+2014). Same in `DungeonSceneBuilder.cs:995-1028`.
That is a real ASCII-rule violation in shipped strings, it predates this ticket, and correcting it is
a copy edit this WO forbids. Worth its own ticket.

## 10. What the owner needs to decide

> *"alduin is the wolf"*

That five-word ruling collides with a shipped boss of the same name. Two ways forward, and **only she
can pick**:

- **(A) Names stand as authored.** The wolf is Aldwin, the boss is Alduin. Nothing changes; section 5b
  keeps them apart; WO-1332 closes as invalid. *(This is what the tree, the narrative bible and both
  existing suites already assume.)*
- **(B) The wolf becomes Alduin.** Then the **necromancer boss needs a new name**, propagated across
  `canon-strings.json`, `en.json`, `enemies.json`, `lore-fragments.json`, `dungeons/healers-cottage.json`,
  `GameStrings_en.asset`, `DungeonSceneBuilder.cs` and `docs/narrative-bible.md` - with the
  `"alduin"` / `Variant = "alduin"` **catalog keys left alone** as save-adjacent data. That is a new,
  larger WO and a creative naming decision, not a spelling fix.

Recommendation: **(A)**, because the split is deliberate authored canon with a paper trail back to
2026-08-05 - but this is the owner's call, not the CLI's.
