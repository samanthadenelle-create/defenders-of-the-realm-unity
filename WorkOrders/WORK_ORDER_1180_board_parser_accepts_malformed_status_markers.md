# WO-1180 - The board parser accepts a malformed `**Status:**` and hides the rows it rescues

**Status:** READY. **Silo:** Tooling/board.
**Origin:** review of the 2026-08-24 board reflection - *"WO-932 exposed a parser-level weakness, not
merely a one-file typo."* Correct, and the fix needs to be narrower than it first looks.

## The finding

`tools/board_build.py:300` matches the status line with:

```python
re.search(r"^\*\*Status:?\*?\*?:?\s*(.+)$", text, re.MULTILINE)
```

⚠ **Both the colon and the closing asterisks are OPTIONAL**, so `**Status: PHASES 1-4 ...**` -
markers *inside* the bold - parses happily. WO-932 carried exactly that and nobody could see it.

## ⛔ AND THE PROPOSED FIX WAS ALREADY TRIED, THE OTHER WAY ROUND

The review suggested *"classify from the leading canonical status token only."* **`bucket_of`
already does that** - the leading-word test was promoted to **every** keyword on **2026-08-23**,
after a sweep found **FOURTEEN** tickets mis-bucketed by substring: *"the PRE-ACK hole closed"* read
as **Closed**, *"design complete, can be implemented"* read as **Done**, *"UNBLOCKED"* read as
**Blocked**.

⚠ **That error only ever ran ONE WAY: toward "finished."** Live work rendered as done, so nobody
looked at it again - and a board that hides open work is worse than no board.

⭐ **The substring pass is kept DELIBERATELY as a fallback**, and the comment at
`tools/board_build.py` says why: many legacy statuses lead with a non-canonical word
(`PARTIAL 2026-08-22 - ...`), and a leading-token-ONLY rule would dump every one of them into
Unlabeled - **trading a silent mis-bucket for a loud false defect.** Implementing the review's
suggestion literally would reopen the problem that change solved.

## The narrower fix - keep both properties

1. **Tighten the regex to require the exact marker `**Status:**`.** A malformed marker becomes a
   **named, reported defect**, not a silent success. ⚠ Report it; do not drop the row - a row that
   vanishes is the failure mode this whole ticket is about.
2. ⭐ **Keep the substring fallback, but COUNT AND LIST every row it rescues.** That is the real
   answer. A row classified by fallback rather than by its leading token is precisely the fragile
   case - **WO-932 was one edit from vanishing because it lived there** - and today nothing
   distinguishes it from a healthy row. Printing `FALLBACK_BUCKETED n: <files>` converts an invisible
   class into a visible worklist that can be drained ticket by ticket, without a false-defect wave.
3. Once that list is empty, and **only then**, the fallback can be removed.


## Scope amendment 2026-08-24 (Ready-queue audit, `READY_FOR_REVIEW.md`)

Two further defects are **proven** and belong to this ticket. Both are additive to the narrower fix
above — neither changes items 1–3.

4. **Malformed `**Status:` markers were real and widespread.** Confirmed on **WO-1008, WO-827 and
   WO-822** (all three used `**Status:` with no closing `**`). ⚠ All three have since been repaired
   by hand, so they are **history, not live fixtures** — prove this case by **inducing** a malformed
   marker on a scratch file, per acceptance item 4. The rule stands: such a row must stay **VISIBLE**
   and be **REPORTED** as malformed.

5. ⛔ **`WO-?` is not a valid assignable key.** Two unrelated tickets shared it —
   `WorkOrders/WORK_ORDER_ad_generator.md` and `WorkOrders/WORK_ORDER_economy_store_packs.md` — so a
   single board key addressed two different pieces of work. **The board must never let unrelated
   tickets share an assignable key.** Detect and report any duplicate or unresolvable id (`WO-?`,
   repeated numbers) by name; do not silently merge them into one row. *(Both of those two are now
   non-assignable — economy_store_packs is CLOSED/superseded and ad_generator is SPEC pending RCA —
   so, as with item 4, prove by induction rather than against those files.)*

⛔ **NOT in scope: `.RESULT.md` exclusion.** An earlier draft of this audit claimed frozen
`.RESULT.md` files were contaminating source rows (cited on WO-1001/935/932/557). **That claim does
not survive checking and is retracted.** `tools/board_build.py:319` already `continue`s on any
`base.endswith(".RESULT.md")` before anything else runs, `grep -c "RESULT\.md" BOARD.html` returns
**0**, and WO-1001 renders exactly **one** row. What exists is the `<span class="res">RESULT</span>`
**badge** drawn on the source ticket's own row (`:315-316` builds the `results` set, `:408` renders it) — a
badge that was misread as a second row. ⭐ **Do not "fix" this.** Writing a repair for correct
behaviour is how a working capability gets broken.

## Acceptance

- [ ] A malformed `**Status: ...**` marker is reported by name, and the row is still rendered
- [ ] The build prints how many rows were bucketed by fallback, and which
- [ ] `Unlabeled: 0` still holds, and no row silently changes bucket - **diff the counts before and
      after** and account for every delta
- [ ] Prove each case by **inducing** it and watching the build report it
- [ ] A malformed marker is reported **by name** and its row still renders (induced, item 4)
- [ ] A duplicate / unresolvable id (`WO-?`, a repeated number) is reported **by name** and is never
      collapsed into one row (induced, item 5)
- [ ] ⭐ **The strengthened green bar:** a build is only clean when it reports `Unlabeled 0` **AND**
      zero malformed markers **AND** zero duplicate ids **AND** zero closed-status contradictions.
      Any one of those non-zero is a **FAILING** board, not a warning.

## ⚠ SCOPE CORRECTION 2026-08-24 (independent gate check) — the duplicate-id worklist is UNDERSIZED BY HALF

⚠ **Do not change this ticket's status — it is landed and committed.** This is a **scope-accuracy
note** for whoever drains the backlog it exposed. Every figure below was recomputed from
`tools/board_build.py`'s own parse at HEAD.

### ⛔ `WO-?` is TWENTY files, not two

§5 above relayed *"two unrelated files share `WO-?`"* (`WORK_ORDER_ad_generator.md`,
`WORK_ORDER_economy_store_packs.md`). ⛔ **That pair was the ILLUSTRATION inside a code comment — the
Ad Generator and Economy Store Packs example — not the count.** The real set is **20 work-order files
that parse with no assignable number**, among them:

`WORK_ORDER_COMBAT_VFX_BATCH_2026-07-10.md` · `WORK_ORDER_KNIGHT_ANIM_4button.md` ·
`WORK_ORDER_MON002_mainnet_skr_one_wood_canary.md` · `WORK_ORDER_PROGRAM_723_731_coc_arena_barracks.md` ·
`WORK_ORDER_PROGRAM_732_736_barracks_troop_roster.md` · `WORK_ORDER_PROGRAM_740_743_room_forge_into_mainline.md` ·
`WORK_ORDER_UI-001_night_market_landscape_visual_redesign.md` · `WORK_ORDER_UI-002_store_commerce_state_clarity.md` ·
`WORK_ORDER_Village2_Enemy_Stronghold.md` · `WORK_ORDER_ad_generator.md` ·
`WORK_ORDER_economy_store_packs.md` · `WORK_ORDER_offline_storage_logic.md` ·
`WORK_ORDER_outpost_base_footprint.md` · `WORK_ORDER_pi_browser_integration.md` ·
`WORK_ORDER_pi_browser_integration_DEEP.md` · `WORK_ORDER_second_grom_companion.md` ·
`WORK_ORDER_skr_staking_and_seeker.md` · `WORK_ORDER_skr_store_design.md` ·
`WORK_ORDER_store_packs_content.md` · `WORK_ORDER_techdebt_ledger_2026-06-28.md`

*(The 17 `WORK_ORDER_PROD-*` files also parse without a WO number, but they carry a resolved
`PROD-N` id and are **not** part of the `WO-?` set.)*

### ⚠ A real distinction inside that 20 — two different diseases

⛔ **`MON002` and `UI-001`/`UI-002` are PRIVATE SERIES the banner authority does not mint.** They land
in `WO-?` for a **different reason** than an unnumbered draft does: an unnumbered draft was never
given an id, whereas these were given an id from a series the board cannot resolve.
⛔ **Do not "fix" them by minting CLI numbers from the banner** — that would renumber live work and
break every inbound reference. The board needs to **resolve** their series, not overwrite it.

### ⛔ Duplicate ids are ~61, not ~40

The board's own guard, run at HEAD:

| Set | Numbers claimed by >1 file | Excess files |
|---|---|---|
| All parsed rows | **61** | **74** |
| Restricted to real work orders | **56** | **69** |

⭐ **Worst offender: `WO-430` is claimed by SIX files** — `Handover_Triage_Detailed_Work_Orders`,
`city_upgrades_modifiers`, `comprehensive_instrumentation_TGVRU`, `gear_catalog_from_db`,
`offline_troop_garrison_defense`, `ui_mvvm_seam`. Next worst are 3-way: `WO-110`, `WO-136`,
`WO-282`, `WO-432`. `WO-280` = 2 (`go_live_blockers`, `village2_wiring_gate`).

⚠ **`WO-1026` = 2 is an ARTEFACT, not a collision** — the guard keys on `num` without filtering
plan/companion docs, so `WORK_ORDER_1026_IMPLEMENTATION_PLAN.md` collides with its own ticket
`WORK_ORDER_1026_raid_defense_consequence_loop.md`. Filter companion docs before counting, or this
class inflates the number and erodes trust in the rest of it.

### ⭐ PROD ids have ZERO duplicates

**17 PROD rows, 0 duplicate ids.** ⭐ Worth recording explicitly: it means the **PROD series' minting
discipline works and the WO series' does not.** The fix for the WO series is therefore procedural,
not merely a de-duplication pass — whatever PROD does at mint time is the thing to copy.
