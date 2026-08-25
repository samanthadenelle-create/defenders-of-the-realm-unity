# WORK ORDER 1193 - the marker ratchet cannot tell a MENTION from an EMISSION

**Status:** FIXED - landed 2026-08-25 at `9ad4ddcfd` (`Assets/Editor/Regression/RegressionMarkerRegression.cs`). Verified at source this session: string literals are length-preserved-masked and ownership counts only where a marker reaches a `MarkerSink` (line 339). Measured 253 owner pairs vs 257 under the old text scan, zero orphaned markers. Owner felt-close owed.
**Minted:** 2026-08-25 (CLI lead, main line; banner bumped 1193 -> 1194 in the same edit)
**Silo:** Tooling / gates
**Origin:** surfaced by WO-1080's implementation, 2026-08-25, when it tripped the ratchet with a
negative test fixture.

---

## The defect

`RegressionMarkerRegression` RULE 1 asserts each `*_OK` marker is emitted by exactly ONE oracle file.
It decides ownership by **scanning source text for the literal**. It therefore counts a marker that a
file merely *mentions* as a marker that file *emits*.

`CaptureProvenanceRegression.cs` contains `"UI_CAPTURE_OK 51"` as a **negative fixture** - a line its
parser must REFUSE, proving the parser does not accept a foreign marker. That is a good test. The
ratchet read it as a second emitter and turned `DataRegression` red.

## STOP THIS IS THE SECOND INSTANCE, AND THE FIRST ONE PREDICTED IT

`RegressionMarkerRegression.cs:438-449` already carries a NAMED exclusion for `HollowPassFixtures.cs`
on exactly this class, and its own comment says:

> *"A DECLARATION INSIDE A STRING LITERAL IS NOT A DECLARATION... the general fix is to strip verbatim
> string literals before this scan; that is a wider change to a load-bearing gate, so this is a
> NARROW, NAMED exclusion of one file and the general case is left flagged rather than silently
> absorbed."*

That prediction has now come true in RULE 1. Two further live mentions sit benign only because nobody
else emits those markers: `HubSceneLiteralRegression.cs:176` and `RaidScoringRegression.cs:259` both
name another suite's marker inside prose failure text.

⭐ **So the count is: one named exclusion, one red build, two latent.** The general case has been
deferred twice and has now cost a gate run. That is the argument for fixing the mechanism.

## STOP THE OBVIOUS FIX IS MEASURABLY WRONG - it was tested, not assumed

The tempting heuristic is *"require a logging sink on the same line."* Measured across every oracle
file under `Assets/Editor`:

- **249** marker-literal sites have a sink on the same line.
- **36 do NOT** - and the dominant shape among them is the **correct** one:
  `private const string MarkerOk = "X_OK";` (at least 13 of the 36), plus helper-call sinks such as
  `Require(builder, "DUNGEON_KIT_BUILD_OK", ...)` and `Pass("LAYOUT_VALIDATE_OK ...")`.

⛔ **That heuristic would silently strip uniqueness protection from 13+ genuine emitters** - trading a
LOUD false positive for a QUIET false negative. For a ratchet that is strictly the worse direction:
a noisy gate gets fixed, a silent one gets trusted.

## The fix to build: classify by whether the literal REACHES A SINK

Two passes over the already-comment-stripped source:

1. If the literal sits on a `const string` / `static readonly string` **declaration**, resolve the
   identifier and count the file as an owner **only if that identifier appears at a sink anywhere in
   the file**.
2. Otherwise require a sink within the same **statement** - join to the `;`, not to end-of-line.

A literal that reaches no sink is a **MENTION**, not an emission.

⭐ **Why this keeps every tooth:** a real emitter always reaches a sink. It classifies the WO-1080
fixture correctly even as a bare literal, because that string flows into a `bad[]` array consumed by
`TryParseHeadLine` and never into a log.

The cost is that the sink list must know this repo's helper wrappers (`Require`, `Pass`, and any
sibling), which is why this is a ticket rather than an inline tweak.

## Acceptance criteria

1. The `HollowPassFixtures.cs` **named exclusion is DELETED** and that file passes on the general
   rule. ⛔ If the exclusion still has to exist, the mechanism did not work - do not keep it as a
   safety net alongside the fix.
2. `CaptureProvenanceRegression.cs`'s fixture can be written as a **bare literal** again and the gate
   stays green. ⭐ Re-joining that token is the acceptance test - see the note in that file.
3. Every one of the 249 genuine emitters is still counted as an owner. Prove the count, do not assert
   it: state the before/after owner totals.
4. A deliberately introduced SECOND real emitter of an existing marker still turns the suite RED.
   ⛔ A ratchet that has not been seen red on the thing it exists to catch is not evidence.
5. `HubSceneLiteralRegression.cs:176` and `RaidScoringRegression.cs:259` stop being latent hazards.

## The related gap, worth fixing in the same pass or ticketing separately

RULE 1's uniqueness ratchet keys **only on `*_OK`**. It does not protect:
- `UI_CAPTURE_HEAD` and `UI_CAPTURE_STAMP` (no `_OK` suffix),
- `UI_CAPTURE_PROVENANCE_FAIL` (`_FAIL`, which `MarkerInLiteral` does not capture),
- any suite reason of the shape `CAPTURE_PROVENANCE OK` (a **space**, not an underscore).

⚠ So a whole family of markers this repo relies on has its uniqueness guaranteed by a **manual table
in a work order**, not by a gate. ⛔ Do NOT "fix" this by renaming markers to fit the regex - the
`UI_CAPTURE_HEAD` shape is specified by WO-1080 and parsed by `tools/board_build.py`. The regex should
learn the markers; the markers should not deform to fit the regex.

## What NOT to touch

- ⛔ Do not add exemptions, allowlists or per-file exclusions. This ticket exists to REMOVE one.
- ⛔ Do not weaken RULE 1's uniqueness assertion itself. The replacement must be at least as strict.
- ⛔ The pre-existing, documented `DUNGEON_EXIT_OK` allowlisted pair
  (`RegressionMarkerRegression.cs:118-123`) is deliberate and stays.
