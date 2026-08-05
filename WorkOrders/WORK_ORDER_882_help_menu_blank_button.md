# WORK ORDER 882 — Help menu: a blank/empty third button

**Status:** READY (small). **Lane:** HUD/UI — `HelpMenu.cs` / its VM. **WO#:** UI-seat block; **882**.
**Source:** `docs/ui-review/screens-2026-08-04/HelpMenu_2340x1080.png`.

## 1. Bad (from the capture)
The menu shows `Report a Bug` · `Controls` · **[blank button]** · `Close`. The third row renders as an **empty,
label-less button** — an entry that resolved to nothing (likely a dev-only item, e.g. Reset Hero & Pet / Dev Tools,
stripped in release but leaving its slot). A tappable blank box reads as broken.

## 2. Fix — the VM decides the entries; the View renders only what exists (MVVM law)
- **The menu-entry LIST is the VM's** (`HelpMenuVM` or the model): it must OMIT an entry that is unavailable (dev-only
  in a release build) rather than emit a blank. The View renders exactly the entries the VM provides — **it must not
  build a button for a null/empty entry.** No dev-only entry should ever produce a blank slot in a ship build.
- Result: Report a Bug + Controls + Close (dev items appear only in dev builds), no empty row.

## 3. Acceptance
- [ ] On-device (release build): no blank/label-less button in Help; only real, labeled entries. Dev entries appear
      only in dev/editor builds. `CompileGate` green. Verify on Seeker.

## 4. Do NOT
- Do NOT let the View render a button for an empty/unavailable entry — the VM filters the list. No fraction bands.
