# WO-1472 RESULT - the 25 names were a false positive; the real gap was that nothing walked the catalog

**Status:** FIXED, WITH THE TICKET'S PREMISE CORRECTED. Uncommitted in the working tree as of
2026-09-06 21:00, awaiting the wave-two gate.
**Commit:** none. All three files are working-tree modifications.
**Files:**
- `Assets/_Modules/Cosmetics/CosmeticApplier.cs:272-295` - the empty-category branch keeps its
  `FlowTrace` scope and LOSES the false-positive `Warn`; `:192` the real detector moves to `Start()`,
  which runs after the same-frame `Attach` and reports once per name.
- `Assets/_Modules/Cosmetics/CosmeticOwnershipService.cs` (+12).
- `Assets/Editor/Regression/CosmeticApplyRegression.cs:702-745` - rule 7,
  `CheckEveryStructureResolvesAMember`, walks the WHOLE structures catalog; `:118-127` the dated
  exemption table (WO-1495 shape: reason plus remove-by date, e.g. `deco_torch` 2026-12-06). An
  unreadable or empty catalog is a NAMED failure, never a stand-down.

## The ticket premise did not survive contact with the code

The 25 names were structure ROOT names, not catalog ids: `StructureFactory` names a root
`entry.displayName` ("Archer Tower"), which is why the log read as a catalog gap. `Attach()` calls
`AddComponent<CosmeticApplier>()` on an already-active host and Unity runs `OnEnable` synchronously
inside `AddComponent`, so `Refresh()` landed in the empty-category branch ONE STATEMENT before
`Attach` assigned the category and called `Refresh()` again. The component's GUID sits on zero prefabs
and zero scenes, so a pre-bind null is the only way that branch is reachable. Every one of the 25 was
bound a statement later, so WO section 2's worklist was deliberately NOT executed - nothing was
unbound. The question it asked was still real and unasserted: rules 1-6 all measured one hand-picked
id ("forge"), so a new catalog row could ship with no cosmetic member and no gate would notice.

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites
green, 0 skipped)` - NOT `REGRESSION_OK`. The reds were a UI-MVVM violation on
`BuildPreviewModal.cs:252-253` and a hollow pass at `NightMarketNoWalletRegression.cs:761`, both fixed
at source in `eb161dc98` (20:10), AFTER both logs. Neither log postdates that commit or the current
working tree, so the wave-two gate is owed.

## Acceptance

- [ ] Zero `has no category bound` lines in a full town plus raid session - NOT captured. The Warn is
      removed at source, so zero is expected, but that is a source read and not a session.
- [x] Regression: every catalog structure id resolves a category, exemptions explicit and dated.
- [ ] `REGRESSION_OK n/n` on a fresh log - not obtained, see the gates line.

Owed: one town plus raid device session on the post-fix build, judged by absence of the message.
