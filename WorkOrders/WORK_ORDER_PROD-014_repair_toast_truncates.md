# PROD-014 — The "NEED MORE TO REPAIR" toast truncates on both lines

**Status:** READY. **Silo:** HUD.
**Reported:** owner felt-test, Seeker, 2026-08-24.

## Symptom

```
NEED MORE TO REP…
115 iron short - go fa…
```

Both lines clipped.

## Why it matters more than it looks

This is the toast that explains **why a repair the player just tried was refused**. Truncated, it names neither the problem nor the remedy — the player is told "no" and not told what to do about it.

⚠ **Same class as the "Price unavailable" clipping** found on this same device the same day (14 of 16 glyphs rendered). Same lesson: **a compile-green build proves nothing about layout.** Both were found by eye, on a device, after every gate had passed.

## Investigate

- Fixed-width container vs the string length; whether the copy is authored or composed at runtime.
- ⚠ Whether these strings live in `canon-strings.json` (§7 requires player-facing copy to). The sibling `RepairHighlight` labels are **hardcoded literals** (`"Repair"` / `"Repair?"`, zero `repair` keys in canon), so this family has form.
- Prefer copy that fits the narrowest supported width over a container that grows — a container sized to the longest string moves the problem rather than removing it.

## Acceptance

- [ ] Both lines render complete at 2670x1200 **and** at the narrowest supported width
- [ ] Proven by a captured PNG that is actually opened, not by a compile
