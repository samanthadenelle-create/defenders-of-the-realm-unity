# SESSION RESUME — 2026-08-10 (updated after the wave-3 SETTLE; the earlier card is superseded)

**Purpose:** the live in-flight state, so any seat resumes without re-deriving. Delete when the
remaining partials are closed.

## Git state

- Branch `wip/village2-and-f8-tickets`. The 2026-08-10 morning wave (11 commits) plus the **wave-3
  settle (12 commits)** are in. Working tree CLEAN at the time of writing.
- Settle commits, in order: WO-950 · WO-951 · WO-953 · WO-956 · WO-958 · WO-959 · WO-960 · WO-952
  (partial) · WO-957/1007/1008 (partial) · WO-949 (partial) · the wave-3 oracle registrations ·
  docs/board/canon.

## Gates over the settled tree (read off the markers, never off this card)

- `Builds/gate-settle4.log` → `COMPILE_GATE_OK`, zero `error CS`
- `Builds/regression-settle3.log` → **`REGRESSION_OK 143/143 suites`** (136 → 143: seven wave-3
  oracles registered)
- `Builds/ui-capture-settle.log` → `UI_CAPTURE_OK 62` + `UI_CAPTURE_FIDELITY_OK 44`; the
  `UI_GEOMETRY_FAIL x16` is WO-941's pre-existing RumorBoard/RealmMap baseline

## What the settle actually had to fix (three lanes died mid-write)

The expiring session left the tree **non-compiling** in three places, each completed by the committer:

1. `EndStateView.cs` — three helpers re-signed to take `panelWidthFrac`, four call sites left on the
   old arity (`error CS7036`).
2. `DungeonExitInteractable.cs` — `BuildLeavePad` written against `ApplyDecorMaterial` and
   `BuildWorldLabel`, neither of which existed (`error CS0103`). Both extracted from the existing
   inline blocks so the pad and the true exit share ONE material path and ONE label path.
3. Then two real regression reds: the `[ui-obsidian]` ratchet on `GuidePointer.cs`, and a
   `Resources.Load` literal with no asset (`Dungeon/Exit/dungeon_texture`).

## STILL OPEN — the five partials (all still READY, scope in the WO body)

1. **WO-952** — geometry landed; the capture case + `COMPRESSED`-absence oracle do NOT exist. That is
   the whole remaining scope.
2. **WO-957 / 1007 / 1008** — code landed, the owner's **"Leave"** relabel is in the data (13 labels,
   both copies byte-identical). **NOT re-baked**, so none of it is on screen yet; `_isTrueExit` is a
   `SerializeField` on BAKED objects. The re-bake belongs in an **isolated worktree** (memory
   `dungeon-scene-shared-tree-corruption`), not the shared tree. No layout authors `exitRoomId` yet;
   the per-layout one-beacon regression was not written.
3. **WO-949** — respawn-in-town + 3 founding potions landed; "teach the cost of dying" did not, and the
   discovery it required (what dying actually costs today) was never run. Likely a DESIGN GAP pin.
4. **WO-85** — never started. The real question is *why the already-shipped grass/roads do not read*;
   value contrast must carry it.

## OWNER PINS OPEN (one word each unblocks)

- **WO-954** which models the hollow family wears · **WO-947** the four cost-basket calls · **WO-917**
  dodge glyph art · **WO-1013** Arcane Tower vs Spire naming · **D8** Walls-tab ruling.
- New from this settle: **WO-956** — does the grunt BODY read different to you? (the new umber and the
  old orc green may collapse to the same olive under deuteranopia; the fix would be luminance, not
  another hue) · **WO-960** shelf depth (5 levels × 2 rows?) · **WO-959** confirm "unsheathed" means the
  combat carry state (sword on the back in town = no flames).
- **WO-85** — in a fresh player build, is the ground visibly grass-and-roads at all, or
  visible-but-too-subtle? That word decides which fix it becomes.

## STANDING RULES SET TODAY (also in CLAUDE.md + memory)

- The F8 listener is hook-enforced for every seat (`.claude/settings.json` + `.claude/hooks/`).
- **The pipeline never idles** — refill agent lanes on every completion (CLAUDE.md §11).
- Recommended-option autonomy: act on the recommendation, she overrides after.
- Cost baskets: regular = wood+iron; magical = crystal-based; never all three.
- **A `.RESULT.md` forces the board's Done bucket** — so a partial lane gets its outcome written into
  the WO body instead, and the status line keeps a canonical keyword plus the remaining scope.
