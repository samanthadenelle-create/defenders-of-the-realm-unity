# WORK ORDER 1049 — `BakeLayoutBatch` silently strips every piece of play content

**Status:** DONE - 2026-08-21 CLI, gate-green (COMPILE_GATE_OK + REGRESSION_OK 234/234).
**Minted:** 2026-08-17 (CLI seat, UI block) as 1048, RENUMBERED to 1049 the same hour — see §7
**Priority:** **HIGH.** It is a one-word defect that empties a dungeon, reports success, and exits 0.
**Provenance:** owner F8 seq 2515 (*"[dg_bonecrypt] LEave still on all steps with an exit portal in
dungeons"*) → I ran the pending WO-1043 re-bake to fix it → **the bake emptied all three dungeons** →
caught by `[authored-placed]` ×13 in the next regression, reverted, root-caused.

---

## 1. What happened, stated plainly — I caused this and the gate caught it

The owner reported leave pads on every stairwell in `dg_bonecrypt`. The diagnosis was correct: the
egress fix (6 exits → 2, `dd17a793f`, 08-16 16:35) changed the **baker**, but the dungeon **scenes**
were last baked **08-14 09:04**, so they still carried the old pads. The fix was to re-bake — which is
exactly what WO-1043 had been sitting READY to do.

The re-bake removed the pads (`Leave` refs 10→0, 7→0, 8→0). It also removed **everything else**:

| dungeon | chests | oil stones | traps | keys | locks |
|---|---|---|---|---|---|
| `dg_bonecrypt` | 4 → **0** | 2 → **0** | 2 → **0** | 1 → **0** | 1 → **0** |
| `dg_ember_deep` | 5 → **0** | 3 → **0** | 3 → **0** | — | — |
| `dg_sunken_vault` | 5 → **0** | 3 → **0** | 2 → **0** | 1 → **0** | 1 → **0** |

Three shipped dungeons reduced to empty geometry. **Scenes were reverted from HEAD immediately**; the
tree is back to known-good (pads present, content present) and nothing was committed.

---

## 2. ⛔ ROOT CAUSE — a defaulted boolean, and nothing shouts

`Assets/Editor/RoomForge/DungeonBaker.cs`:

```csharp
public static void BakeFromFile(string layoutAssetPath, bool populateForPlay = false)   // :238
...
public static void BakeLayoutBatch()            // :187  — THE HEADLESS/CI ENTRY POINT
{
    ...
    BakeFromFile(path);                          // :221  ← no second argument
}
```

**`populateForPlay` defaults to `false`, and the batch entry point takes the default.** So the one
entry point a headless run, a CI job or an agent will reach is the one that bakes geometry and drops
every chest, oil stone, trap, key and lock — silently.

### Why this is worse than an ordinary bug
- **It reports success.** Unity exits **0**. The only log noise is the routine `Lifecycle ERROR ...
  exit code reload scopes` teardown line that every batchmode run emits, so there is nothing to react to.
- **The wrong outcome is the DEFAULT.** Getting it right requires knowing to pass a second argument
  that the batch signature does not mention and no caller demonstrates.
- **It looks like it worked.** The dungeon still loads, still has rooms, corridors and stairs. The
  content is missing, which is invisible until a player walks it or an oracle counts it.
- **It is aimed squarely at automation.** `BakeDefault`/`BakeSelected` are MenuItems needing a
  selected asset; `BakeLayoutBatch` exists *specifically* to be driven headlessly — the exact context
  with no human to notice five missing chests.

---

## 3. What saved it — and the argument this makes

`[authored-placed]` (`ComposedDungeonRunRegression`, WO-1112) compares the **authored layout JSON**
against the **baked scene** and failed ×13 with, verbatim:

> *"layout authors 5 'chests' but the baked scene contains only 0 BreakableContainer — authored
> content that never reached the scene is invisible to the player and to every other gate"*

That oracle is nine days old and paid for itself completely on this one run. Note precisely what it
asserts: not "the baker ran", not "the scene exists", but **"what was authored is what is in the
scene."** A gate on the *process* would have passed here; only a gate on the *outcome* caught it.

---

## 4. The fix

**Preferred — make the safe outcome the default.** Change `BakeLayoutBatch` to pass
`populateForPlay: true`. A dungeon baked for the game should contain the game.

Then decide the fate of the `false` path deliberately:
- If a geometry-only bake is genuinely needed, it must be **opt-in and explicit** — a `-layoutOnly`
  CLI flag, never a silent default — and it must **log loudly** that it is producing an unplayable scene.
- If nothing needs it, delete the parameter. An unused dangerous default is worse than no parameter.

⚠ **Do NOT "fix" this by remembering to pass the argument.** The defect is that the correct call is
not the obvious one. Any fix whose success depends on the next caller knowing a convention has not
fixed it — it has re-armed it for whoever forgets.

---

## 5. Acceptance

1. `BakeLayoutBatch` on all three dungeons leaves chest / oil-stone / trap / key / lock counts
   **equal to the authored layout**, not zero.
2. `[authored-placed]` green on the re-baked scenes.
3. The leave pads are still gone afterward — this WO must not undo WO-1043's actual fix.
4. Any surviving geometry-only path is opt-in, and says so in the log.
5. `COMPILE_GATE_OK` + `REGRESSION_OK`.
6. ⚠ **Diff the baked scenes before committing.** These files are binary-serialized, so "it looks
   fine" is not available — count the components.

---

## 5b. ⭐ THE STRUCTURAL FINDING — why nothing caught the stale scenes for two days

The owner asked whether the leave pads were a REGRESSION of WO-987 / WO-1007 / WO-1008 (the
WO-962 → WO-1036 shape: a fix ships, the symptom returns). **They are not**, and the WOs say so
themselves — WO-1007/1008 status: *"RE-BAKE DONE 2026-08-14: all 7 dungeons re-composed,
COMPOSE_ALL_OK 7/7, **13 pads now bake label='Leave'**"*. The pads were the DESIGN on 08-14. The
egress ruling that retired them landed **08-16 16:35**. Nothing regressed — a ruling superseded a
shipped feature and the baked artifact lagged behind it.

**So why did no gate go red for two days?**

`DungeonEgressRegression` reflects into `DungeonBaker.PlaceComposeExtracts` and RUNS IT
(`:292`). It never opens a shipped `.unity`. It therefore proves **the baker would produce two
exits** — and is structurally blind to a scene on disk that has eight. It is a **PROCESS gate**.

Contrast `[authored-placed]` (`ComposedDungeonRunRegression`), which **reads the baked scene** and
compares it to the authored layout. That is an **OUTCOME gate**, and it caught the content strip
in this WO within a single run.

> ### THE GENERAL RULE THIS ESTABLISHES
> **A gate on the CODE cannot see a stale ARTIFACT.** Any subsystem whose output is BAKED —
> dungeons, terrain, navmesh, addressable catalogs, scene builders — needs at least one oracle that
> reads the SHIPPED ARTIFACT, not just one that re-runs the producer. Otherwise the gate stays green
> for exactly as long as nobody re-bakes, which is unbounded.

**Recommended (own it here or mint it separately): a BAKE-STALENESS oracle.** Fail when a baked
artifact is older than the code that produces it — e.g. any `Assets/Scenes/DungeonCompose/*.unity`
whose last-modified predates the newest commit touching `DungeonBaker.cs` or the layout JSON it was
baked from. That is one check that catches every stale-bake defect in the class, including the next
one, rather than one oracle per symptom. It would have gone red at 08-16 16:35 — the moment the
ruling landed — instead of waiting for a player to walk into it.

⚠ Note the trap in writing it: it must compare against the **producer's** last change, not "is it
older than N days". A dungeon nobody has touched in a year is not stale; a dungeon baked one hour
before the baker changed is.

## 6. Still open behind this: WO-1043 itself

The owner's original complaint is **NOT fixed**. `dg_bonecrypt` still has its 8 leave pads on every
stairwell, because the re-bake had to be reverted. WO-1043 stays READY and is now **blocked on this
ticket** — the re-bake cannot land until the baker stops stripping content.

---

## 7. Numbering note — I collided, in the paragraph where I wrote about not colliding

Minted **1048** at the owner's instruction after reading the banner. **The UI seat minted its own
1048 from the same F8 (seq 2515) while I was writing this file**, and bumped the banner correctly in
its own edit. Theirs is first-on-disk-and-referenced, so per §2 theirs wins and **this ticket
renumbered to 1049**. Different subjects, same trigger: theirs asks whether *any* floor should carry
a Leave exit (a design question for the owner); this one is the bake defect that empties a dungeon.

⚠ **THE LESSON IS NOT "READ THE BANNER" — I DID READ IT.** I read it, then spent several minutes
authoring this document, and minted into a number that had been consumed in between. The banner is
only authoritative *at the instant it is read*, and a long write is exactly the window where it goes
stale. **Re-read the banner at WRITE time, not at DECIDE time** — or better, claim the number by
creating the file first and filling it after.

This is the third numbering incident today (the UI seat's 1043 collision from a carried private
counter; the stale banner ROW that still read 1045 while 1045/1046 existed; and now this). All three
have the same shape: **a number that was true when someone looked, and false when they acted.**

*(Also corrected in this same edit: the banner ROW read "next free = 1045" while `WORK_ORDER_1045_*`
and `WORK_ORDER_1046_*` both existed on disk — the 1046 mint wrote its bump into the surrounding
PROSE and never updated the ROW. And my first attempt to fix that row reworded it to "UI seat block:
next free =", which **broke `board_build.py`'s regex** (`UI seat next free\s*=\s*\*\*(\d+)\*\*`) so
the board silently reported 1043 — a consumed number. A banner the parser cannot read is precisely
the precondition for the five-collision day the banner itself documents. Restored to the exact
parseable phrase and verified with `BANNER_OK next mint - CLI: 1115, UI seat: 1049`.)*

## 7b. Original numbering note

Minted **1048** at the owner's explicit instruction. The banner's UI-seat row read *"next free =
1045"* while `WORK_ORDER_1045_*` and `WORK_ORDER_1046_*` both existed on disk — the row went stale
because the previous mint wrote its bump into the surrounding **prose** and never updated the **row**.
That is the §2 failure mode in its quietest form: the banner was wrong in exactly the way that causes
the next seat to collide. Row corrected to **1049** in this same edit.

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `DungeonBaker.cs:223,240 populateForPlay=false` — headless bakes without play content. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **CLI 2026-08-21:** c436b858a - populateForPlay default REMOVED so the compiler asks at every call site
