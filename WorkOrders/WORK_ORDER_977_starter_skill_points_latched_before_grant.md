# WORK ORDER 977 — Starter skill points can be silently never granted, and the latch says otherwise

**Status:** DONE — code landed 2026-08-14 (`HeroProgression.ApplyLevelRewards`): grants run first, the latch flips ONLY on a confirmed `SkillSystem.AvailablePoints` delta, a null `SkillSystem.Instance` and a throw each raise a `FlowTrace.Fail` naming the lost points, and the old hollow `"granted 2 starter skill points"` line is replaced with a measured `availablePoints <before>-><after> (delta=…, calls=…)` trace. **Still owed:** the §4 regression covering the null + healthy paths is NOT written (`DataRegression.cs` is lane-fenced to the committer), and no runtime capture proving the null path can be produced — the null case fires at most once per player and cannot be forced from this seat, so the null branch is verified by construction/review only, not by captured data.
**Lane:** Village / Hero progression
**Severity:** player-facing, **fires for every player exactly once**
**Minted:** 2026-08-10 (CLI), from the hollow-assertion audit (`docs/reference/HOLLOW_ASSERTIONS_REGISTRY.md`)

---

## 1. The defect

`Assets/_Modules/Village/Hero/HeroProgression.cs`

- **`:266`** — the "already granted" latch flips
- **then** two null-conditional grants run
- **`:269`** — logs *"granted 2 starter skill points"*

The latch is set **before** the grants, and those two grants — **unlike the identical call twelve
lines above** — are **not** wrapped in the `try`/`catch` that would raise a `FlowTrace.Fail`.

So if `SkillSystem` is null at that moment, the null-conditional silently no-ops, the player gets
**zero** skill points, the latch is **already set so it never retries**, and the log says *granted*.

## 2. Why this cadence is the worst possible one

It fires **once per player, ever.** That means:

- It cannot be reproduced by replaying the same save — the latch is set, so the code path is gone.
- Every capture and every log review reads *"granted 2 starter skill points"*, because the trace is
  downstream of the latch, not of the grant.
- The player just… has no skill points, at the exact moment they are learning what skill points are.

The neighbouring call twelve lines up is wrapped correctly, which is the strongest evidence this is
an oversight rather than a deliberate difference — and the reason it should be fixed to match rather
than redesigned.

## 3. Fix

**Order of operations, in this order:**

1. Perform the grants.
2. **Confirm** they landed (check the resulting point total, not the absence of an exception).
3. Latch **only on confirmed success**.
4. Wrap in the same `try`/`catch` + `FlowTrace.Fail` idiom as the call twelve lines above — match the
   neighbour, do not invent a new pattern (§ house style).

**Make the trace falsifiable** (this is the WO-976 rule applied): log the **resulting point total**,
not the intent. `granted 2 starter skill points` must become something a broken run can contradict —
e.g. `starter points: requested=2 granted=<n> total=<t>`.

## 4. Acceptance criteria

- [ ] With `SkillSystem` deliberately null, the latch does **not** set and the trace reports failure.
- [ ] With `SkillSystem` present, points are granted exactly once and the trace states the resulting
      total.
- [ ] A second run over the same save does not double-grant.
- [ ] Regression covering both the null and healthy paths, registered in `DataRegression.cs`
      (committer adds the registration — that file is lane-fenced).
- [ ] Brace balance + 0 NUL bytes (§1, §0).

## 5. What NOT to do

- Do **not** just move the log line below the grants. That fixes the *reporting* and leaves the
  latch-before-grant bug intact — the player still loses their points, silently.
- Do **not** remove the latch. Once-only is correct; the ordering is what is wrong.

## 6. Related

WO-976 (`hasSurface` false green) and WO-973 (`bubble=ok`) are the same underlying failure class —
a log asserting intent rather than outcome. This one is the most expensive of the three because the
thing it hides is a permanent loss of player progression.
