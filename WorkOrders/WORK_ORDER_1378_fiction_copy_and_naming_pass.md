# WORK ORDER 1378 - The fiction, copy and naming pass

**Status:** READY TO IMPLEMENT
**Silo / Lane:** Copy / canonical JSON strings - data-only, no gameplay logic
**Type:** COPY + NAMING, owner-ruled creative direction
**Minted:** 2026-09-04 (CLI)
**Source of truth:** `docs/CREATIVE_CANON_ELARION_2026-09-04.md` - ⛔ **this WO POINTS at that file and
deliberately does NOT restate its strings.** A string copied into a second doc is a defect waiting for
a date (CLAUDE.md §2/§5/§16).

---

## §1. WHY THIS IS THE HIGHEST-VALUE COPY LANE IN THE PROGRAMME

⛔ **Nothing in the shipping game ever tells a player that raiding is how they get richer.** Verified
2026-09-04 across the tutorial beats, `guide-content.json`, the daily quests and the Journey screen.
The Guide's opening line is:

> *"Raids are where your trained troops earn their keep."*

That is a sentence about payroll, and it is the closest thing the game has to a motive for attacking
anyone. **A loop the player has to infer is a loop most players never find** - which is precisely the
funnel the economy programme is measured on. This lane is ~40 strings and no new systems.

⚠ **The conformance audit (2026-09-04) measured it harder:** `tutorial-steps.json` contains **ZERO**
occurrences of "barracks", "raid", "army" or "troop". The FTUE does not mention the loop at all.

## §2. WHAT TO CHANGE - every string lives in the canon file, section by section

| Canon § | What lands where |
|---|---|
| §3 | The four target names + the **empty `description` fields**, in `Assets/Resources/Data/Canonical/scene-configs.json` |
| §8.1 | Teaching beats: Barracks completed, first army granted, first raid prompt, target screen |
| §8.2 | Victory as a homecoming - `MEMORY RECLAIMED`, then the supplies |
| §8.3 | Failure (`THE HEART'S REACH FAILED`) and manual retreat (`TACTICAL WITHDRAWAL`) - two DIFFERENT screens |
| §8.4 | The five Journey card subtitles |
| §5 | **Realm Vigil** - the weekly ladder; `Threat` stays with the Iron Bastion ladder |
| §11 | Mirelle the Facetkeeper; rarity ladder Rough -> Cut -> Refined -> Radiant -> **Echoed** |
| §6 | `HIRE REINFORCEMENTS`, never `Skip Training` |

**Also rewrite the `raids` section of `guide-content.json` end to end.** It explains the interface well
and never says who these camps belong to or why the player should want to break one.

## §3. ⛔ RULES

- **Display names and descriptions ONLY. The stable ids are LIVE SAVE KEYS** - `raider_camp_small`,
  `fortified_garrison`, `mage_enclave` must not be renamed.
- ⚠ **Dual-copy:** canonical JSON lives in BOTH `Assets/Resources/Data/Canonical/` and
  `Assets/StreamingAssets/Data/Canonical/`. **Resources WINS at runtime.** Keep them byte-identical.
- **ASCII only** in player-facing strings.
- ⛔ **Do not implement a first-pass name.** Canon §2 lists what the direction's own author superseded;
  shipping "Marches" or "The Splinter Camp" is a defect, not a preference.
- ⛔ Every renamed concept ships with a **regression case proven RED first** against the old string, so
  a revert is caught. Register it in `DataRegression` - an unregistered oracle never runs, and the
  registry meta-oracle will catch the omission.

## §4. ⚠ COORDINATE - `guide-content.json` was already edited today

The concurrent P0 lane changed the raids section to say "Open Journey, then Raids" and to name wood and
iron (`guide-content.json:280`). **Build on that edit; do not revert it.** This lane replaces the
*fiction*, and that fix was to the *direction* - both must survive.

## §5. ACCEPTANCE

- [ ] All four targets carry a name and a one-line description; no `description` field is empty
- [ ] The Guide's raids section names the loop and keeps the corrected Journey -> Raids direction
- [ ] The FTUE mentions Barracks -> army -> raid; `grep -ci` on `tutorial-steps.json` is no longer 0
- [ ] Victory, failure and manual retreat are three distinct screens with the canon copy
- [ ] Five Journey subtitles present
- [ ] `REGRESSION_OK n/n suites` on a fresh log, with the new oracles registered
- [ ] `UI_CAPTURE_OK` and **the PNGs opened** - compile-green never proves a panel reads right
