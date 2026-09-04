# WORK ORDER 1370 - The HARVEST RESULT modal does not say what 3000 is, or that anything was lost

**Status:** READY TO IMPLEMENT
**Silo / Lane:** Core/UI - `Assets/_Modules/Core/UI/HarvestOverflowModal.cs` (copy + layout only)
**Type:** EXISTING system, legibility defect
**Minted:** 2026-09-04 (CLI), from her screen mid-playtest
**Severity:** P2 - not a crash, but it is the screen that reports a LOSS, which is the worst place to
be unreadable.

## THE REPORT

Owner, looking at the modal: ***"this screenshot now shows i have 3000 of what? I cannot tell what
its trying to convey"***.

Screenshot: `logs/f8-inbox/device/live-20260904-095525.png`.

What is on screen:

```
                        * HARVEST RESULT

    Stone
    Collected: 0 of 90   |   Uncollected: 90

    Storage: 3000 / 3000. Upgrade a Stoneyard, or
    spend stone, before collecting again.

    Each uncollected amount was not added to storage.

                          CLOSE
```

**What it is actually trying to say:** *your stone store is full, so all 90 stone you just harvested
was thrown away.* A player should get that in one read. She could not get it at all.

## THE CAUSE - a loop written for a LIST, rendering ONE item

`Assets/_Modules/Core/UI/HarvestOverflowModal.cs:55-60`:

```csharp
lines.Add($"{s.ResourceName}\nCollected: {s.Granted} of {s.Requested}   |   Uncollected: {s.Lost}");
lines.Add($"Storage: {s.Current} / {s.Max}. Upgrade a {s.ContainerName}, or spend {s.ResourceName.ToLowerInvariant()}, before collecting again.");
lines.Add("Each uncollected amount was not added to storage.");
```

Four defects fall out of that shape:

1. ⛔ **`Storage: 3000 / 3000` names no resource.** It is emitted as its OWN line, three lines below
   the word `Stone`, with a blank line between. **This is exactly her question - "3000 of what?"** The
   resource name is in scope (`s.ResourceName`) and simply is not used on the line that needs it.
2. ⚠ **"Each uncollected amount" implies a list that is not there.** The sentence is written for the
   multi-resource case; with one resource it reads as if something is missing.
3. ⚠ **`Collected: 0 of 90 | Uncollected: 90` states ONE fact twice.** If 0 of 90 were collected, 90
   uncollected is arithmetic, not information. It burns the most legible line in the panel on a
   restatement.
4. ⛔ **The word LOST never appears.** "was not added to storage" is a passive euphemism for *you
   threw away 90 stone*. The player's actual loss is the one thing the panel must land.

## SUGGESTED SHAPE - ⛔ FINAL WORDING IS THE OWNER'S (SAMANTHA.md rule 8)

Offered as a starting point to react to, NOT to implement unasked:

```
    Stone store is FULL - 3000 / 3000

    90 stone was lost.

    Upgrade a Stoneyard, or spend stone, before collecting again.
```

The principles behind it, which are the reviewable part:
- **The resource name and its number live on the SAME line.** No orphaned figure, ever.
- **The loss is stated as a loss**, in the player's words, in the largest line.
- **Drop the redundant restatement** - one arithmetic fact, once.
- **Singular and plural read correctly** - the "Each..." line only appears when there IS more than one.
- ⚠ **Do not carry meaning in colour.** The owner is red/green colourblind; "full" and "lost" must be
  carried by wording, size and position (`CLAUDE.md` §7 / memory `owner-colorblind-delegate-visual-creative`).
- ⚠ **ASCII only** in TMP strings - non-ASCII renders as tofu on device.

## ACCEPTANCE

- [ ] The resource name appears on the same line as its storage figure. No orphaned number anywhere.
- [ ] The loss is stated explicitly, using the word lost (or the owner's chosen wording).
- [ ] The single-resource case reads correctly - no "Each" without a list.
- [ ] The multi-resource case still reads correctly. ⚠ **Check BOTH** - the current bug exists
      precisely because only the multi case was considered.
- [ ] ⛔ **A screenshot at the device's real 2670x1200, opened and looked at** before this is called
      done. `UI_CAPTURE_OK` proves a panel rendered, never that it can be read
      (memory `screenshots-are-primary-evidence-for-visual-defects`).
- [ ] Owner has read the new copy and approved the wording.

## WHAT NOT TO TOUCH

- ⛔ Not the overflow LOGIC. Capping at 3000 and discarding the remainder is the WO-837 stockpile
      ruling working as designed - containers cap capacity. This ticket is copy and layout ONLY.
- ⛔ Do not rename `Stoneyard` or the `Stone` resource. Both are real and authored
      (`structures-catalog.json`); ⚠ note `GameState.cs:59-61` records that the player-facing Stone
      balance rides the legacy `Resources.Food` slot (WO-1163/WO-1212) - **do not "tidy" that here.**
