> ## STOP - CORRECTION 2026-09-02. THE PREMISE IS REFUTED. NO RENAME WAS PERFORMED.
> **There is no misspelling.** "Alduin" and "Aldwin" are TWO AUTHORED CHARACTERS one letter apart,
> and the sweep below found **ZERO crossover** - every one of the 34+6 `Alduin` hits is the
> necromancer, every one of the 30+9 `Aldwin` hits is the Ice Echo. The 34-vs-30 measurement counted
> two different people's names and read it as a split.
> - **Alduin the Mournful** - the Necromancer boss, once a healer. Authority: `canon-strings.json:26-27`,
>   `docs/narrative-bible.md:57` (SS"The leader") and `:367` (character table: "The Necromancer boss").
> - **Aldwin, the Ice Echo** - Echo #1, the founding companion / guide wolf. Authority:
>   `EchoRosterCatalog.cs:149`.
>
> Normalising them would attribute a necromancer's journal to the player's founding companion, and it
> would **break two existing suites that exist to forbid exactly this**:
> `DungeonLoreReadableRegression` SS5 and `EchoEngageDialogueRegression.cs:167-168`
> ("Note Aldwin != Alduin the Mournful - do not correct one into the other").
>
> **This has now been minted TWICE on the same false premise, in OPPOSITE directions.** WO-881 SS1-SS3
> asked to rename Alduin -> Aldwin and was corrected + not actioned on 2026-08-05. WO-1332 asks for the
> reverse. See the RESULT for the widened oracle that closes the gap that let it recur.
>
> **BLOCKED on the owner:** the ruling *"alduin is the wolf"* collides with the shipped boss name. Only
> she can decide whether the wolf is renamed (and the boss then needs a NEW name across
> `canon-strings.json`, `en.json`, `enemies.json`, `lore-fragments.json`, `healers-cottage.json`,
> `GameStrings_en.asset` and `docs/narrative-bible.md`) or the two names stand as authored. Not guessable.


> ### OWNER RULING 2026-09-02 - CLOSED WITH NO ACTION
> Asked directly whether she meant the wolf should become Alduin (which would force a rename of the
> Necromancer boss across seven files of shipped copy), the owner chose: **leave both as authored.**
> - The wolf remains **Aldwin, the Ice Echo** (Echo #1, the founding companion).
> - The boss remains **Alduin the Mournful** (the Necromancer, once a healer).
>
> Her earlier line *"alduin is the wolf"* was a passing spelling, not a rename instruction, and the
> CLI turned it into a ticket without first checking whether the two names belonged to two people.
> **That check is the whole lesson.** A measurement can be arithmetically correct and still mean
> nothing: 34-vs-30 was a real count of a boundary that does not exist. Before normalising ANY name in
> this repo, establish that the variants refer to the same entity - `DungeonLoreReadableRegression`
> and `EchoEngageDialogueRegression.cs:167-168` already existed to say so, and neither WO-881 nor
> WO-1332 read them first.

# WORK ORDER 1332 - The repo cannot spell the wolf's name: 34 "Alduin" vs 30 "Aldwin"

**Status:** CLOSED BY THE OWNER 2026-09-02 - NO WORK REQUIRED, premise refuted. She ruled "leave both as authored": the wolf stays **Aldwin, the Ice Echo**, the boss stays **Alduin the Mournful**. No rename was performed and none is wanted. The oracle was widened so this cannot be minted a fourth time. *(Closed by the PO, not by the CLI - she made the call directly when the collision was put to her.)*
**Silo / Lane:** Canon / player-facing strings
**Type:** EXISTING defect (shipped copy)
**Minted:** 2026-09-02 (CLI) on a direct owner ruling.
**Severity:** P3 - cosmetic, but the player sees it and it is trivially correct.

## The owner's ruling

> *"alduin is the wolf"*

**Alduin** is correct. `Aldwin` is wrong, everywhere.

## The measurement

A case-insensitive sweep of `Assets/Resources/Data/Canonical/` and `Assets/_Modules`
(`--include=*.json --include=*.cs`) returns:

```
  34  Alduin
  30  Aldwin
   6  alduin
```

The repo is split almost exactly down the middle. This is the "one fact written twice" disease
wearing a character's name: nobody was wrong on purpose, the two spellings simply propagated in
parallel and neither side ever saw the other.

## The work

Normalise every occurrence to **Alduin**, preserving the existing casing convention at each site
(a lowercase `alduin` in an id or a key stays lowercase - see the warning below).

## ⛔ THE TRAP - IDS ARE NOT PROSE. READ THIS BEFORE A SINGLE REPLACE.

**A blind find-and-replace will break saves.** Ids, PlayerPrefs keys, addressable addresses, save
fields, catalog keys and dialogue node names are DATA, not copy. If `aldwin` appears as part of a
persisted id or an addressable address, renaming it silently orphans whatever refers to it - and this
game is LIVE with real players who have saves.

So, per occurrence, classify FIRST:
1. **PLAYER-FACING PROSE** (dialogue, names, tooltips, quest text) - fix it. This is the point of the
   ticket.
2. **AN ID / KEY / ADDRESS / SAVE FIELD** - **DO NOT RENAME IT.** Leave it, and record it in the
   RESULT as deliberate debt with its file:line. An ugly id nobody sees costs nothing; a broken save
   costs a player their town.
3. **A COMMENT** - fix it, it is free.

If any occurrence is genuinely ambiguous between prose and key, LEAVE IT and list it. Do not guess.

Precedent that makes this non-negotiable: CLAUDE.md records that catalog ids are LIVE SAVE KEYS and
must never be renamed (the structure-role work added a role enum as a NEW FIELD rather than renaming
ids, for exactly this reason).

## Also check, since you are already sweeping

- `Assets/StreamingAssets/Data/Canonical/` twins - the Resources copy WINS at load, so a fix applied
  to only one copy is invisible or, worse, half-applied. Keep both byte-identical.
- Any `.yarn` dialogue files, and `canon-strings.json`.

## Acceptance

- [ ] Every player-facing occurrence reads **Alduin**.
- [ ] Every occurrence left as `Aldwin` is NAMED in the RESULT with its file:line and the reason
      (id / key / address / save field).
- [ ] Both canonical twins are byte-identical where the file is duplicated.
- [ ] An oracle pins the spelling in player-facing strings so it cannot drift back. Prove it RED
      first (reintroduce one, watch it fail, restore) and report the mutation.
- [ ] ASCII-only, as with every player-facing string.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on FRESH logs, markers asserted.
- [ ] PO closes.

## What NOT to touch

- Do not rename ids, keys, addresses or save fields (see the trap above).
- Do not restyle or rewrite the surrounding copy while you are in there - fix the name, nothing else.
- Do not touch the wolf's art, materials or textures (that is WO-1326, awaiting an owner ruling).
