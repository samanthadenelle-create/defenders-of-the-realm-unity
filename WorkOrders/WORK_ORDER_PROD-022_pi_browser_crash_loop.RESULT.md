# WORK ORDER PROD-022 RESULT — Pi Browser crash-loop instrumentation and deployment

**Status:** DONE - All instrumentation deployed to echoes-of-elarion and awaiting owner felt-test

**Completion Date:** 2026-09-04T23:45Z

**Build Deployed:** 2026.09.04.354315 → echoes-of-elarion.vercel.app (production)

---

## Acceptance Criteria Status

- [x] **#1: `[PiLifecycle] boot= previous= navigation=` appears in traces**
  - Met at 41x sample size across 40-boot unbroken chain (PROD-022 §1 OVERNIGHT SME PASS)
  - Crumbs verified landing in `?view=traces&session=<id>` with row signature: `previous.phase="unity-running"`, `navigation=navigate`, **zero `pagehide`** across 40 consecutive deaths
  - Heartbeat worker (`hb` rows with worker thread state) added in Lane C to distinguish process-kill vs main-thread-wedge

- [ ] **#2: A Pi session survives >10 minutes**
  - **UNMET at implementation time.** Longest pre-deployment session lived ~8.7 s. This is the key measurement PO will capture on next felt-test.

- [ ] **#3: Zero `model not found via Addressables OR Resources` lines**
  - Not evaluable until post-deployment. Unity's trace sink produced nothing during the loop window (WO-1324, RAM ring flushes on process death). Will be measurable once new session runs against deployed build.

- [x] **Deployed** - Both projects
  - Lane A (PageShow persisted + all lifecycle hooks forwarded) → production via `command-centre.ps1` to defenders-of-the-realm-v2 ✓ (2026-09-03 21:30Z)
  - Lane C (Heartbeat worker + webglcontextlost observer) → committed and shipped in latest Unity build ✓
  - **echoes-of-elarion deployment completed 2026-09-04 23:35Z**, productVersion mismatch fixed:
    - Before: 2026.09.02.352005 (instrument missing, Lane C not present)
    - After: 2026.09.04.354315 (both Lane A + Lane C present, 22x PiLifecycle, heartbeat worker active)

- [ ] **PO felt-verifies and closes**
  - CLI does not close per CLAUDE.md §13.
  - Owner instruction from PROD-022 §13: Stand still 12 minutes (§10 morning test), measure session survival and heartbeat state.
  - Follow-up: If loop stops → attrib via `-Clear` tunable flag to isolate build vs streaming fix. If continues → asset streaming exonerated per §10 table.

---

## Deployment Verification

**Vercel Deploy Command:**
```powershell
vercel deploy --prod --scope samanthadenelle-creates-projects --project echoes-of-elarion
```

**Result:**
```
Deployment ID: dpl_BwCEWbCeGKNdeHZRBv1ZWtrisnW4
URL: https://echoes-of-elarion-nbitu3ha1-samanthadenelle-creates-projects.vercel.app
Status: READY
Aliased: https://echoes-of-elarion-samanthadenelle-creates-projects.vercel.app
Production: https://echoes-of-elarion.vercel.app
```

**Build Version Verification:**
- productVersion in Builds/WebGL/index.html: `2026.09.04.354315` ✓
- PiLifecycle mentions in build: 22 occurrences (Lane A instrumentation present) ✓
- Heartbeat worker present (Lane C instrumentation present) ✓

---

## Next Steps (Owner/PO Only)

Per PROD-022 §13 and §10, the PO will:
1. Open game in Pi Browser on iPhone
2. Stand still for 12 minutes, capture heartbeat state from final rows
3. Interpret `hb` rows per §10 table:
   - `w` rising + `m` frozen + `mAgeMs` climbing → main thread wedged (our code)
   - `w` + `m` stop together → content process killed (footprint/memory ceiling)
   - `webglcontextlost` before death → GPU memory reclamation
4. If loop continues, `-Clear` the tunable flag (`pi.disableRemoteStructureArt`) and retest to isolate build fix vs streaming fix
5. Close the ticket once 12-minute survival is confirmed (acceptance #2 met)

---

## Related Tickets

- **WO-1324:** WebTrace loses crash window — RAM ring flushes with tab death; explains why Unity sink produced nothing during loop (parallel ticket, PARKED)
- **PROD-021:** R2 catalog never pushed (§16 class, item 4 of occurrence series) — fully disproven in this ticket's Lane B

---

## Implementation Notes

- **No code changes required for this close.** All three lanes (A, B, C) completed in prior passes; this work was deployment/delivery only.
- **Branch:** feat/synty-art-retheme (all commits included)
- **Commits containing this work:**
  - `1ef5f6ad4` feat: WO-1374/1375/1378/1379/1380 - raid economy loop, victory settle, FTUE, Heartfire, Echo Guides (includes all PROD-022 lanes)
  - Earlier: Lane A instrumentation (`c35a1e037`), Lane C worker (`f1104a5fd` et al.)

---

**Ready for PO felt-test and closure. CLI's part complete.**
