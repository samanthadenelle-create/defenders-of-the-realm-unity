# WO TRUE STATUS — 2026-08-08 (known dictionary)

**What this is:** the reconciled, evidence-backed status of every work order touched in the four days
to 2026-08-08. It exists because the board's `Status:` lines had drifted so far from the tree that
they were actively dangerous — sending sessions to rebuild finished systems and to skip unstarted ones.

**Authority:** this file records what the TREE says at HEAD `07d2c6f8`. Where a WO's `Status:` line and
this file disagree, **this file wins and the WO file has been corrected to match.** Owner felt-verification
is a separate axis and is NOT recorded as done by anything here (CLAUDE.md §13 — the PO closes, not the CLI).

**Method.** Four independent read-only audits, one per number block. For each WO: quote the Status line,
check for a `*.RESULT.md`, search git for shipping commits, and run one decisive tree check on the WO's
core acceptance artifact. Claims were then spot-verified at source by the CLI seat before any status was
rewritten — agent output is a proposal, never truth (memory `cli-gatekeeper-agent-role-model`).

---

## 0. The headline

**~52 of ~91 work orders carried a status that disagreed with the tree.**

| Verdict | Count |
|---|---|
| DONE (reconciled from the tree, felt-verification outstanding) | 38 |
| PARTIAL | 17 |
| NOT STARTED | 20 |
| SUPERSEDED | 6 |
| NEEDS-OWNER-RULING | 5 |
| DESIGN-ONLY / no status field | 5 |

**38 RESULT files were created in this pass.** Before it, the newest RESULT file in the repository was
`WORK_ORDER_831_*.RESULT.md`.

---

## 1. THE ROOT CAUSE — one dead convention

The RESULT-file protocol (CLAUDE.md §2: *"CLI saves a `WORK_ORDER_NNN_*.RESULT.md` when done and verified"*)
**stopped at WO-831 and never resumed.** Everything downstream follows from that:

- Nine WOs in the 679-871 block shipped production code **with a dedicated new regression suite each** and
  still read `READY TO IMPLEMENT`.
- The entire 900-926 range had **zero** RESULT files.
- Anyone using "does a RESULT file exist?" as the done-gate would have marked ~38 shipped WOs as unstarted.

**WO-918 was written specifically to fix this, and was itself never started.** Its own §0 predicted this
audit almost verbatim: *"Shipped work still reads READY on disk... so the next session re-implements or
re-audits finished work."*

### Four failure modes, worth naming separately

1. **Shipped-but-silent** (the largest class). A commit lands the work and never touches the Status line.
   Worst case: **WO-857**, fully delivered by `177b24a7` — a 708-line `TownBankCapacity.cs`, a new catalog,
   a dual-copied JSON and a 779-line regression suite — by a commit that **never names the WO at all**.
2. **Born stale.** WO-919, 920, 921 and 922 were **first added to the repo by the very commit that
   implemented them** (`94c23be3`, `3b344919`). They were authored as specs and shipped in the same wave, so
   their Status lines were never true for a single moment.
3. **Self-contradicting files.** **WO-863** carried a `DEPLOYED 2026-08-05 — both URLs LIVE and PUBLIC`
   banner at line 1-22 and `READY TO IMPLEMENT` at line 25.
4. **Ruling routed around.** **WO-874**: the owner ruled WIRE. `4c1da079` instead promoted
   `SpawnVfxFor`/`PlayDeathShake` to statics called from `Enemy.cs:720/2701` — delivering the visible tell
   while silently abandoning the ruling, **with no reversal recorded.** `AddComponent<EliteVFXController>`
   is **0** repo-wide (verified at source). The aura and `OnEliteAttack` have never run in the shipped game.

---

## 2. NUMBER COLLISIONS — three live, all dangerous

| Number | The two claimants | Why it bites |
|---|---|---|
| **911** | `WORK_ORDER_911_timer_speedup_crystals_all_channels.md` (**PARTIAL**) vs `WORK_ORDER_911_unified_queue_screen.md` (**DONE**) | Commits crediting "WO-911" all mean the SCREEN. The timer half rode in `4fab809f` without ever saying 911. **A RESULT file or board row keyed to bare "911" cannot be correct for both** — one is done, one is not. WO-905's SUPERSEDED banner says "ABSORBED BY WO-911" with no filename, so a reader who opens the timer WO finds no Manage screen and concludes the absorption never happened. |
| **760** | `WORK_ORDER_760_vfx_common_attach_architecture.md` (**PARTIAL**) vs `WORK_ORDER_760_dragon_syndrath_fly_land_burn_tree.md` (READY) | The dragon WO owns **every** `WO-760` commit in the log, so a grep-based status check reports green for the wrong ticket. |
| **759** | `WORK_ORDER_759_particle_pack_eoa_implementation_playbook.md` (**DONE**) vs an orphan `WORK_ORDER_759_vfx_manual_picks_gameplay_wire.RESULT.md` with no matching spec | A RESULT-file check on "759" reports DONE for the wrong work. |

**Both 911 files now carry a mutual collision banner naming the other by full filename.** Renumbering is
an owner call; the banner is the stopgap.

---

## 3. THE MOST MISLEADING INDIVIDUAL LINES (now fixed)

1. **WO-901 phase table row F read `WITHHELD`** while `177b24a7` had landed the entire town bank cap **the
   same day**. This would have sent a session to rebuild a 708-line system. Row F and the "why phase F is
   withheld" section are both now corrected.
2. **WO-1001's RESULT file** claimed *"slice 6-7 + Phase 2 NOT started"*. Both landed — `1ea03b84`
   (slices 6-8) and `335f6b81` (Phase 2, three themed dungeons) are ancestors of HEAD. The single most
   wrong status on the board.
3. **WO-854** claimed phases 3-7 were gated on owner rulings. `QuestCompletabilityRegression.cs:213` locks
   `MinCompletableStages = 63` — the phase-7 endpoint — shipped as `QUEST_REACH_OK 63/63`.
4. **WO-872's index** presented 873-883 as one uniform READY block. Truth: 6 shipped, 4 never started,
   1 blocked on the owner.

---

## 4. WHAT THE STATUS LINES WERE HIDING — real holes

Reconciling upward is only half the value. These were hidden by an optimistic or stale status:

- **WO-884 / 885 — the VFX facade never existed.** Four of the five files WO-884 §9 mandates
  (`VfxFacade.cs`, `VfxSocket.cs`, `VfxElement.cs`, `VfxEmitter.cs`) are **absent** (verified at source).
  `Vfx.On(` = 0, `VfxBones` = 0. WO-885's own precondition was that platform, so its children 886-893
  wired **straight to `VFXManager`** and its "LOCKED contract, non-negotiable" was silently voided by the
  very WOs it indexes.
- **WO-898 — the headline mechanic was never built.** `crystalsPerBracket` = **0 hits repo-wide** (verified
  at source). No 5-minute bracket curve, no impulse dialog, no Jupiter fallback. Only progress bars shipped.
  Its monetization premise has since MOVED anyway: `ef40c0e7` purged premium-currency ad rewards.
- **WO-875 — never attempted.** `HeroAbilities.cs:1887` still reads `RegistryOnlyMotionVfx = true`
  (verified at source), the exact mask §4 forbids, still gating 4 call sites.
- **WO-877 — never attempted.** `Assets/Editor/AnimatorSetup.cs` still present (verified at source).
- **WO-923 — marked READY, actually blocked on research.** The prefab kit half exists, but acceptance §6
  (`dg_descent_probe` = `PathComplete`) is unmet after **four** failed hypothesis rounds. See WO-927.
- **WO-894 — DONE with a latent trap.** The arena + FLAWLESS + 5-spoils case still compresses to 0.992 and
  is only unreachable because nothing sets `perfect` true. It regresses the moment that is wired.

---

## 5. Docs that carried NO status field at all

Six board artifacts could not tell a reader whether they were live. All now carry one:

| Doc | Assigned status |
|---|---|
| `DUNGEON_WO_INDEX.md` | **STALE** — it is the canonical map for 8 dungeon WOs and could not say which were done |
| `HANDOFF_GROK_DUNGEON_MULTILEVEL_NAV.md` | **STILL LIVE** — its P0 is unresolved at HEAD |
| `STAIR_PREFAB_SCRIPT_CONTRACT.md` | **SATISFIED** — consumed; without this a reader would re-do the work |
| `RESEARCH_BRIEF_GROK_NAVMESH_STITCHING.md` | **SENT — NO ANSWER RECORDED**; its Q1 is load-bearing for the stitch design |
| `REVIEW_MAP_IMAGINE_DUNGEON_2026-08-07.md` | **ROUTING MAP VALID — ALL TARGETS NOT STARTED** |
| `BRIEF_bow_prop_corrupt_bounds_for_grok.md` | **NOT STARTED** (its own status was already accurate) |

⚠ **`6e0cde93`'s subject reads "land WO 923-926"** but its diff is `.gitignore`, `TimelineSettings.asset`
and five `.md` files. **It landed the DOCUMENTS, not the code.** All four targets are unstarted.

---

## 6. Standing rules this audit re-proves

- **Never trust a `Status:` line.** Check for a RESULT file, a shipping commit, AND the acceptance artifact.
  All three, because each of them lied somewhere in this set.
- **Never key anything to a bare WO number.** Three numbers have two claimants.
- **A commit that ships a WO must name it.** `177b24a7` and `4fab809f` did not, and became untraceable.
- **Write the RESULT file in the same breath as the work.** The entire 52-item drift is one skipped step,
  repeated ~90 times.

---

*Produced 2026-08-08 at HEAD `07d2c6f8`. Refresh at the next Sunday sweep (`SUNDAY_HOUSEKEEPING.md` step 2),
and add a row here whenever a WO's true status moves.*
