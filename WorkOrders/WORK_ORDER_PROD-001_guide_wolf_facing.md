# PROD-001 — The guide wolf walks north facing left

**Status:** DONE — awaiting owner verification (see §5; **NOT PUSHABLE** until every box is confirmed)
**Minted:** 2026-08-17 (CLI seat) — the first ticket of the post-launch PROD series
**Priority:** HIGH — the guide is the first character a new player follows in the FTUE.
**Provenance:** owner, 2026-08-17, on a live build: *"the wolf still walks north facing left"*.
**Supersedes:** WO-1032 (same defect, pre-launch numbering).

---

## 1. The defect

The Echo guide (the walking ice wolf) travels north but faces 90° off — it moves sideways. The player
follows this character through the founding walk, so it is on screen during the first minutes of the
game.

## 2. Root cause — a constant that was right when it was written

`Assets/_Modules/Pets/PetDeployer.cs` applied a **hardcoded yaw to every pet**:

```csharp
const float PetForwardYaw = -90f;  // +X (authored forward) → +Z (root forward)
visual.transform.localRotation = Quaternion.Euler(0f, PetForwardYaw, 0f);
```

Its own comment records why it existed: the ORIGINAL pet mesh authored forward along **+X**, and
`Pet.FaceToward` rotates the root with `LookRotation`, so the root's forward is **+Z**. A −90° yaw
mapped one to the other. Correct, for that mesh.

**Then the guide's body was replaced with the ice wolf (WO-961).** If the new mesh already authors
forward as +Z, the −90° does not correct anything — **it introduces the 90° error.** Same line, same
value, opposite effect, because the asset underneath it changed.

> ### ⛔ WHY THE FIX IS NOT `-90f → 0f`
> That would fix the wolf and **break every pet still on a +X-forward mesh**, and the next body swap
> would re-open it a third time. A single global yaw cannot be correct for a set of independently
> authored meshes. This is the same class as the shield seat and the stale build-list literal: a
> number that was true once, welded in place, outliving the thing it described.

## 3. The fix — derive per body, and say what was measured

`DerivePetForwardYaw(GameObject visual, string species)` measures the body instead of assuming it:

- A pet body is **longer along its travel axis than across it**, so the longer horizontal extent IS
  the authored forward. X longer → mesh faces +X → yaw −90. Z longer → already forward → yaw 0.
- Bounds are read from the combined renderers **before** any rotation or scaling is applied, while
  the visual still sits at identity under a fresh root — so world extents equal local extents at that
  instant. ⚠ Moving this call after the rotation or after `NormalizePetHeight` invalidates it.
- **Ambiguity is reported, never silently resolved.** A body within 15% of square cannot be judged by
  this rule, so it keeps the legacy −90° *and logs that it did*. That is the case a per-body override
  would serve, and the trace is how anyone would find out it had arrived.
- Every path emits a `FlowTrace` line carrying **the measurement and the choice**, so a wrong call
  shows up in one log line instead of being re-litigated from a screenshot months later.

## 4. Files changed

- `Assets/_Modules/Pets/PetDeployer.cs` — the constant replaced by `DerivePetForwardYaw`; the helper
  added beside `NormalizePetHeight`, whose renderer-bounds pattern it follows.

## 5. ⛔ VERIFICATION CHECKLIST — the owner tests, the CLI verifies each line against evidence

**No push while any box is unconfirmed.** The CLI does not tick these from its own gates; each needs
either the owner's observation or a captured artefact, and the CLI states which.

| # | What to check | How it is verified | State |
|---|---|---|---|
| 1 | The guide wolf faces the direction it walks | Owner observation on device | ☐ |
| 2 | It faces correctly walking **north**, the reported case | Owner observation | ☐ |
| 3 | It faces correctly turning through other headings (E/S/W) | Owner observation — a fix that only works on one axis is not a fix | ☐ |
| 4 | The measurement fired and chose | `adb logcat` shows `[Flow:Pets] forward-yaw: '<species>' measured x=… z=… -> authored forward is +Z, applying 0°` | ☐ |
| 5 | No pet fell into the ambiguous branch | No `too SQUARE to judge` warning in the trace | ☐ |
| 6 | **Other pets did not regress** — the real risk of this change | Deploy a non-wolf Echo; owner observation + its own forward-yaw trace line | ☐ |
| 7 | Compile gate green | `COMPILE_GATE_OK` by marker on a fresh log | ☐ |
| 8 | Regression suite no worse than baseline (206/210) | `DataRegression` run | ☐ |

⚠ **Box 6 is the one that matters most and is easiest to skip.** The old constant was correct for
+X-forward meshes; if any shipped pet still uses one, this change is exactly where it would break —
and the wolf looking right would hide it.

## 6. Not in scope

- No change to `Pet.FaceToward`, the leash, or any movement code — this is purely the visual seat.
- No re-authoring of the offsets in `OffsetForge/offsets.json` (that is the **shield** seat, a
  different defect, still open).
