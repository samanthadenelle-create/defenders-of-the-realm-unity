# WORK ORDER 1203 - the board cannot say "it exists, but not here"

**Status:** READY - two related defects in the badge mechanism that shipped this morning. (1) The
`--check` contradiction detector cannot tell a status line that NAMES the new sub-badge from one that
CLAIMS a partially-landed slice, so the very ticket that shipped the detector was the first row its
own detector falsely accused. (2) THE REAL ONE: there is no label meaning "built in a dev lane, not in
this tree", so WO-1163 and WO-1199 - both verified as having nothing on disk, both reviewed and
returned for revision in unmerged Codex lanes - can only be labelled with something that lies. Spec is
for a SECOND sub-badge in the existing mechanism plus the lint fix, in one change. ⛔ No new bucket.
**Minted:** 2026-08-25 (CLI lead, main line; banner bumped 1203 -> 1204 in the same edit)
**Silo:** Tooling / board
**Origin:** found by the board keeper during the 2026-08-25 reconcile, immediately downstream of
WO-1197's fix landing (`5f3985928`). Direct successor to WO-1197 - read that ticket first.

---

## DEFECT 1 - the new sub-badge falsely accuses any ticket that merely DISCUSSES it

WO-1197 shipped a sub-badge this morning (`5f3985928`: `tools/board_build.py` +
`docs/BOARD.md` section 3b). The name of that badge is a bare word, and the `--check` contradiction
detector matches that same bare word.

⛔ **`--check` cannot distinguish a status line that NAMES the badge from one that CLAIMS a partial
landing.** WO-1197's own status line necessarily names the badge it shipped, so the build printed:

```
BOARD_CHECK_FAIL 1 status contradiction(s)
```

The board keeper resolved it for now by wrapping the badge-name mentions in quotation marks - the
parser's OWN sanctioned `"reported, not asserted"` exemption (`_QUOTED_SPAN`, `board_build.py:229`,
applied at `:252`) - plus a dated BOARD-KEEPER NOTE on the row explaining why the quotes are load
bearing and must not be tidied away.

⚠ **That is a workaround, not a fix.** The exemption keeps the record honest but leaves the blind spot
in place: the next ticket that discusses the badge trips it again.

⭐ **And this ticket is very likely that ticket. That is the cheapest possible reproduction case - it
reproduced while being written.** Two findings from that, and they are NOT the same finding:

1. `status_contradiction()` lints only rows whose LEADING word is in `_FINISHED_LEADS`
   (`board_build.py:224-237`). This row leads with `READY`, so the contradiction lint does not fire
   on it. **The blind spot is therefore narrower than it first looks - but it is still live**, and it
   fires on exactly the rows that matter most: any FIXED/DONE row that documents the badge, which is
   every future handback on this mechanism, including this ticket's own.
2. ⭐ **`has_landed_partial()` (`board_build.py:148-152`) has NO quoted-span exemption at all.** It is
   a bare case-insensitive word match with no lead-word gate and no `_QUOTED_SPAN` check, so it fires
   on ANY row in ANY bucket that contains the word - quoted or not. The board keeper's quoting
   workaround protects `--check` and does **not** protect the badge. That means a Ready row that only
   DISCUSSES the badge gets RENDERED with it, silently asserting a landing that never happened -
   which is DEFECT 2's exact harm, arriving through DEFECT 1's mechanism.

**This ticket's own status line is written to avoid the badge word entirely, because there is
currently no way to name it safely.** Needing to censor a word to describe the tool is itself the
proof the blind spot is real.

## DEFECT 2 - ⭐ THE REAL ONE. There is no way to say "it exists, but not here."

The board keeper DECLINED to badge WO-1163 and WO-1199 with the new sub-badge, and **its reasoning is
correct**: `has_landed_partial()` means *a slice is on disk*, and for both of those, **nothing is in
this tree.** Verified, not assumed:

| WO | Verification that nothing landed | Where the work actually is |
|---|---|---|
| **WO-1163** | no `stone` key in EITHER canonical `packs.json` at HEAD | `codex/wo1163-r2`, reviewed, returned with a money bug (`PackCatalog` has no `stone` binding; Newtonsoft drops it silently) |
| **WO-1199** | `tools/command-centre.ps1` appears in NO commit reachable from HEAD - `git log --all -- tools/command-centre.ps1` is EMPTY | the dev lane's own tree, revision 2 returned as a NEAR PASS with two one-line fixes outstanding |

Both were **built in unmerged Codex lanes, reviewed by the lead, and returned for revision.**

⛔ **So both available labels lie:**

* The new sub-badge **asserts a landing that never happened.** Nothing is on disk. A reader who
  believes it goes looking for code that HEAD does not contain.
* Bare `READY` **understates** it: it says "spec only, take it from scratch", when in fact a verified,
  near-complete revision exists elsewhere and a puller would be duplicating real work.

⭐ **THREE tickets are in this state right now** - WO-1163, WO-1199, and the WO-1195 lane is heading
there. That is what makes this a GAP in the vocabulary rather than an edge case worth a habit.

### ⚠ The direction of harm - it is the one that got Batch 8 refused

A puller who trusts the bucket **re-does work that already exists in a lane.** WO-1137 and WO-1138
were handed out as fresh work when both were already finished; the board was the proximate cause.

**This is that same mechanism, one layer out - and the layer is what makes it worse.** In the Batch 8
case the finished work was in the tree, so a diligent seat could in principle have grepped HEAD and
found it. Here **the work is not in the tree at all**, so no amount of grepping HEAD reveals it. The
board is the ONLY place that knowledge can live, and right now the board has no word for it.

## What to build

⛔ **Do NOT add a new BUCKET.** WO-1197's constraint still binds and is repeated here deliberately: a
fourth destination changes what "Ready" means for every existing query and every seat's mental model.
The bucket vocabulary in `docs/BOARD.md` section 3b is canon.

Spec: **a SECOND sub-badge, in the same mechanism the existing one already uses** (a sibling of
`has_landed_partial()` feeding a sibling of the `<span class="partial">` render at
`board_build.py:590`), meaning **built in a dev lane, not in this tree.**

⭐ **The implementer names it.** ⚠ Requirement on the name: **it must not be confusable with the
existing sub-badge**, in the rendered card or in the status-line keyword. The entire defect is that
the two states look alike - "a slice landed here" and "it is finished somewhere else" are opposite
facts about where the code is, and a reader glancing at the card must not have to squint. Do not pick
a near-synonym; do not pick a word that shares a stem.

### Requirements

1. ⛔ **Derivable from the STATUS LINE alone.** The generator must not consult git, a worktree, or a
   network. `board_build.py` parses `WorkOrders/*.md` and stays that way. (Note that both rows above
   ALREADY state their verification in prose - the facts are on the line, they are just not machine
   readable.)
2. ⭐ **It must carry WHERE the work is** - the branch or lane name. "It exists elsewhere" is useless
   without "elsewhere is `codex/wo1199`." A puller who cannot find the lane is back to re-doing it.
   This is the field that makes the badge actionable rather than merely honest, so treat it as part of
   the badge, not an optional extra.
3. ⭐ **Distinguish REVIEWED-AND-RETURNED from AWAITING-REVIEW if it can be done without a third
   badge.** They mean opposite things to a reader: *returned* means the ball is in the dev lane's
   court, do not touch it; *awaiting review* means the LEAD owes something and the ticket is blocked
   on us. ⛔ **If separating them genuinely needs a THIRD badge, do not invent one - say so in the
   handback and let the owner rule.** WO-1197's lesson holds: the cheaper shape shipped and the
   handback named what it gave up, which is why the badge was trustworthy on day one.
4. **Fix DEFECT 1 in the SAME change.** `--check` must not flag a row that merely discusses a badge,
   and the badge renderer must not badge one either. ⭐ **Prove it against WO-1197's real row and
   against THIS ticket's own row.** Both are live cases sitting in the tree today. Specifically:
   * `status_contradiction()` must stop firing on WO-1197's row **without relying on the quotation
     marks** - then the BOARD-KEEPER NOTE on that row can record that the quotes are no longer
     load bearing (⚠ do not delete that note; it is the record of the incident).
   * `has_landed_partial()` must not badge a row that only names the badge. It currently has no
     quoted-span guard whatsoever - see DEFECT 1 finding 2.
   * ⛔ **`--check` must keep failing on GENUINE defects only.** It currently reports 0 unlabeled and
     0 contradictions, and that number is honest enough to gate on. Do not make it noisy, and do not
     buy quiet by weakening the patterns for real claims.
5. **Per `docs/BOARD.md` section 4, `tools/board_build.py` and `docs/BOARD.md` section 3b move in the
   SAME commit.** A keyword the parser knows and the doc does not is invisible to every human; the
   reverse silently produces `Unlabeled`.
6. ⛔ **Do NOT hand-edit `BOARD.html`** - it is generated output.
7. ⚠ **Leave the 244 `NEAR_MISS_STATUS_MARKER` rows, the 61 duplicate numbers and the 17 unnumbered
   rows alone.** Standing instruction; none of them is this ticket.

## Acceptance

1. **Prove it against the THREE REAL ROWS - WO-1163, WO-1199 and WO-1197 - ⛔ not a synthetic
   fixture.** That was WO-1197's own acceptance rule and it is precisely why its badge was
   trustworthy on day one: a fixture proves the regex, a real row proves the vocabulary.
2. WO-1163 and WO-1199 each render the new badge AND name their lane, and ⛔ neither renders the
   existing landed-slice badge - nothing of either is on disk.
3. WO-1197 renders the existing landed-slice badge (it genuinely did land a slice) and ⛔ is NOT
   flagged as a contradiction, with its quotation marks no longer doing the work.
4. This ticket's own row renders NEITHER sub-badge and is not flagged - it discusses both and claims
   neither. ⭐ Once that holds, its status line can be rewritten to name the badges plainly, and doing
   so is the closing proof.
5. Both affected rows stay in the `Ready` bucket. ⛔ The badge must not move them - a returned lane is
   still assignable, to the lane that owns it.
6. `BOARD_CHECK_OK` still reports **0 unlabeled and 0 contradictions** afterwards.
7. The handback says which shape was chosen for requirement 3, and what it gave up.
