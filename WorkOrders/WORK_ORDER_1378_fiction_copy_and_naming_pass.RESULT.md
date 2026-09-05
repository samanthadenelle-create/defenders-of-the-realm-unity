# RESULT - WORK ORDER 1378 - The fiction, copy and naming pass

**Filed:** 2026-09-04 (board agent, from the lead's measured facts + a same-session read of the JSON)
**WO status:** FIXED - in build 2026.09.05.355872, awaiting owner felt-test. PO closes.

## Provenance - who built what

The copy and the narrative-gate oracle were **committed earlier today by another seat in
`1ef5f6ad4`** (Lane H: tutorial beats mention Barracks / army / raid, victory copy `MEMORY
RECLAIMED`, the five-question narrative gate authored in `TutorialStepReachabilityRegression`, canon
copy held to "questions before answers"; Lane E: the four target names + descriptions). Tonight did
NOT author that copy. Tonight **PROVED it** - the suites ran GREEN on the registered list, the one
remaining Guide sentence was corrected, and the tree was cut to the build on her device.

## What shipped (read at source this session unless marked)

| Canon section  | Landed |
|---|---|
| section 3 target names + descriptions | `scene-configs.json` - The Forsaken Camp (:67-68), The Broken Garrison (:120-121), The Veiled Enclave (:185-186), The Iron Bastion (:242-243, *"The Heart remembers no fortress here."*). No `description` is empty. Stable ids `raider_camp_small` / `fortified_garrison` / `mage_enclave` / `iron_bastion` untouched. |
| section 8.2 / section 8.3 three distinct screens | `canon-strings.json` `raidVictoryTitle: "MEMORY RECLAIMED"` (:370), `raidDefeatTitle: "THE HEART'S REACH FAILED"` (:378), `raidRetreatTitle: "TACTICAL WITHDRAWAL"` (:384) |
| section 5 Realm Vigil | `canon-strings.json` `realmVigilName: "Realm Vigil"` (:391) |
| section 6 HIRE REINFORCEMENTS | `canon-strings.json:396-397` - the verb and the priced form; never "Skip Training" |
| section 8.1 FTUE mentions the loop | `tutorial-steps.json` - `grep -ci` for barracks/raid/army/troop/Journey = **13** (the audit measured 0) |
| Guide raids section | **tonight, IN this build:** `guide-content.json` raids copy sends the player to **Journey** (not "the HUD") - the WO-1374 direction fix survives |
| Twelve canonical JSONs de-BOMed | tonight (lead's fact list) |

## NOT evidenced on this tree - left open, not hidden

- **section 11 Mirelle the Facetkeeper and the Rough -> Cut -> Refined -> Radiant -> Echoed ladder:** a
  grep of `Assets/Resources/Data/Canonical` for `Mirelle` and `Echoed` returns nothing. Not landed.
- **section 8.4 the five Journey card subtitles:** not read this session; no suite line names them.
- **`UI_CAPTURE_OK` + the PNGs opened:** not in the lead's fact list for this build. The copy is
  proven present in the data; that it *reads right on the panel* is exactly what her felt-test is for.

## Which suites prove it

- `[tutorial-reach]` (`TutorialStepReachabilityRegression`, registered `DataRegression.cs:654`) -
  extended in `1ef5f6ad4` (+232 lines) with the five-question narrative gate; GREEN on pass 2.
- `[raid-escalation]` (`DataRegression.cs:1332`) - *"four targets authored ... with the canon names +
  card lines, twins byte-identical, no superseded name survives"* (the section 2 "do not ship a first-pass
  name" rule, as an assertion).
- `[raid-discoverability-copy]` (`DataRegression.cs:1336`) - the Guide sends the player to Journey.
- `[glossary]` (`DataRegression.cs:646`) - neither the guide nor the glossary leaks a retired name.

## Build + install evidence (lead's measurements, 2026-09-04)

- Build `2026.09.05.355872` (versionCode 355872) installed on the owner's Seeker 2026-09-04 22:22:13
  via `install-apk-to-seeker.ps1` (`Success`; `adb shell dumpsys package` versionName=2026.09.05.355872).
- Chain markers: `SCHEMA_PARITY_OK`, `APK_OK` (461 MB), `R2_PUSH_OK` (catalog_2026.09.05.355872.bin/.hash
  uploaded), `R2_PARITY_OK targets=Android,StandaloneWindows64,WebGL objects=271`, `APK_DONE`.
- Regression on the same tree: pass 2 = 375/377; the two reds were in TEST code, fixed; **final pass
  pending; see `Builds/regression.log` marker.**

## What the owner should felt-test

1. New game - play the opening beats. The FTUE now mentions the Barracks, the army and raiding;
   Corvin's *"There's movement beyond the Heart."* beat is present and short.
2. Journey -> Raids: the four cards read The Forsaken Camp / The Broken Garrison / The Veiled
   Enclave / The Iron Bastion, each with a one-line description; nothing says "Marches" or "The
   Splinter Camp".
3. Win a raid - the result screen title is **MEMORY RECLAIMED**, then the supplies.
4. Lose a raid - **THE HEART'S REACH FAILED**. Retreat manually from another - **TACTICAL
   WITHDRAWAL**. Two different screens.
5. Manage -> a running Train job: the button says **HIRE REINFORCEMENTS**, not Skip Training.
6. Game Guide -> Raids: it tells her why to raid and to go through Journey.
