# WORK ORDER 976 — `hasSurface` is a false green: `panelSettings=ok canvas=ok` proves nothing

**Status:** DONE — surfaceWired + measured UiSurfaceProbe verify (WO-976).
**Lane:** Instrumentation / Core UI
**Minted:** 2026-08-10 (CLI). Found by a sweep during the WO-973 read-only prep — i.e. this ticket
exists because the last false-green cost us a defect that had to be found in a **screenshot**.

---

## 1. The defect

`Assets/_Modules/Core/UI/AddressableUIManager.cs:234` emits:

```
panelSettings=ok canvas=ok => hasSurface=
```

Both halves are **non-null reference checks**. A UI surface with both references present can still be:

- zero-sized
- positioned entirely offscreen
- behind another element's sort order
- fully transparent

…and this line prints `ok` through **every one of those.** It asserts that two fields were assigned.
It says nothing about whether a player can see anything.

## 2. Why this one matters more than the others

This is the same disease as WO-973's `bubble=ok`, but on a **far more trafficked path**: the shared
UI surface resolver, not one NPC's speech bubble. Every screen that resolves through it inherits a
line that reads like verification and isn't.

**The cost is not hypothetical.** WO-973's giant clipped speech bubble was reported by its own trace
as `bubble=ok` — so it survived until a human looked at a screenshot. A trace that cannot fail is
**worse than no trace**, because it actively steers the next reader away from the broken thing.
Someone grepping `[Flow:*]` for a blank-screen report will read `hasSurface=true` and go look
somewhere else.

## 3. Siblings — listed so the sweep is not repeated

| File:line | Line | Why it's hollow |
|---|---|---|
| `CompanionGearSetup.cs:208` | `result=ok` | Emitted after an `AddComponent` that essentially cannot return null |
| `HudCompassWidget.cs:529` | `hero=ok` | A non-null check |
| `TowerLoopDevHarness.cs:171` | — | **Dev harness — ignore, out of scope** |

## 4. ⛔ The fix is NOT to delete these lines

CLAUDE.md §12 is binding: **instrumentation is permanent.** Deleting a hollow `Warn`/`Step` turns a
misleading line into *no* line, and the next regression in that system starts from zero evidence.

**The fix is to make each line assert something that CAN fail.** For `hasSurface`, that means the
values that decide whether a human sees the panel:

- resolved rect size (in px, post-layout — not the authored value)
- whether it is within the viewport
- sort order / draw order relative to whatever is above it
- resolved opacity

Then `hasSurface=` becomes a claim the data can contradict, which is the entire point.

**Emit-timing warning, learned on WO-973:** a UI trace fired before layout settles reports pre-settle
values and is hollow for a *different* reason. Emit after layout resolves, and if that frame is not
obvious, say so on the WO rather than guessing.

## 5. Acceptance criteria

- [ ] `AddressableUIManager.cs:234` reports resolved size / visibility / sort order — values capable
      of failing — and no longer reduces to non-null checks.
- [ ] The two siblings in §3 are either corrected the same way or annotated in-place as deliberately
      advisory, so no future reader mistakes them for coverage.
- [ ] A deliberately broken surface (zero-sized or offscreen) makes the line report the failure. **Prove
      this by breaking one on purpose** — a fix to a false-green that is not itself falsified is just a
      new false green.
- [ ] No `FlowTrace` call deleted anywhere in the change (§12).
- [ ] Brace balance + 0 NUL bytes on every `.cs` touched (§1, §0).

## 6. Related

- **WO-973** — `bubble=ok` on Bryn's speech bubble; same disease, and the case that proves the cost.
- **WO-968 §11b** — `[Flow:GaitF] bodyErr` prints `0.0` as a pass while measuring nothing in dungeons
  (it derives from a velocity that is 0 by design under foreign ownership). Three instances of the
  same failure class found in one night is the argument for a standing rule: **a trace field that
  cannot report failure is a bug, not a nicety.** Consider adding that line to
  `docs/INSTRUMENTATION_STANDARD.md` as part of this WO.
