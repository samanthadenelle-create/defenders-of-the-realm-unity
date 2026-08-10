# SESSION RESUME — 2026-08-10 (write-down at ~95% context, before compaction)

**Purpose:** the live in-flight state of this session, so any seat (post-compaction or fresh) resumes
without re-deriving. Delete when the wave is closed.

## Git state
- Branch `wip/village2-and-f8-tickets`, **HEAD `322c88bc`, PUSHED, local == origin.**
- Morning wave = 11 lane commits (wallet WO-931 · death-pin · battle-music · WO-945 grace · WO-811
  Echo repair · WO-948 walls · WO-1013 plans · WO-929+955 VFX · suite registrations · F8 listener
  hooks incl. the .gitignore un-ignore · docs/board/canon).
- Gates that produced it: `COMPILE_GATE_OK` (Builds/compile-gate-morning-wave2.log, 0 error CS) +
  `REGRESSION_FAIL 135/136` where the single red was the then-in-flight WO-1012 ui-obsidian ratchet.

## IN FLIGHT (uncommitted in the shared tree — DO NOT `git add -A`)
1. **WO-1012 tutorial pipeline — COMPLETE, review CONFORMS TRUE, uncommitted.** Its two blocker
   edits are APPLIED by the orchestrator in `DataRegression.cs` (mandatory pin 7→8; `TutorialBandRepelled`
   added to `KnownSignal`). Open findings recorded in the WO's new §7.
2. **Wave-3 fan-out (workflow, 9 lanes):** WO-950 · 951 · 952 · 953 · 956 · 958 · 959 · 960 · 949.
3. **Exit-affordance lane:** WO-957 + 1007 + 1008 combined (owner pins IN: keep the pads, the word
   is **"Leave"**).
4. **World atmosphere lane:** WO-85 modernization (grass/paths). ⚠ Mid-task correction sent: grass +
   roads ALREADY SHIPPED at `cc24da5a` / `bfacf0b3` (2026-08-07, ExteriorTerrainBuilder + committed
   terrain data + merged scene; nothing touched them since) yet the owner has never seen it — the
   agent is investigating why it does not read (runtime override? too subtle, esp. red/green
   colourblind — VALUE contrast must carry), and must ENHANCE that builder, not paint beside it.

## THE NEXT MECHANICAL STEP (owner-authorized, blocked only on lanes settling)
The last compile gate (`Builds/compile-gate-1012.log`) was RED with **15 errors, all in
`GearAura.cs`** = the WO-959 lane mid-write. Attribution rule: no other lane drew a diagnostic.
**Plan:** when the fan-out + exit lanes report, run ONE compile gate over the settled tree →
`DataRegression.RunAll` → RESULT files + status flips → lane commits by explicit path → push →
**wipe + rebuild the Windows exe** (owner is waiting on it; she stopped playtesting for it).

## OWNER PINS OPEN (one word each unblocks)
- **WO-954** — which models the hollow family wears (they are still KayKit `Skeleton_*`).
- **WO-947** — 4 cost-basket calls (arcane pair = crystals+iron?; healer/caravan magical?; jeweler
  regular?; arcane-tower shop crystal-based?).
- **WO-917** — dodge glyph art pick (surface candidates rather than waiting).
- **WO-1013** — "Arcane Tower" (art sheet) vs "Arcane Spire" (buildable) vs `arcane-tower` (existing
  building id) naming.

## STANDING RULES SET TODAY (also in CLAUDE.md + memory)
- The F8 listener is hook-enforced for every seat (`.claude/settings.json` + `.claude/hooks/`).
- **The pipeline never idles** — refill agent lanes on every completion.
- Recommended-option autonomy: act on my recommendation, she overrides after.
- Cost baskets: regular = wood+iron; magical = crystal-based; never all three.
