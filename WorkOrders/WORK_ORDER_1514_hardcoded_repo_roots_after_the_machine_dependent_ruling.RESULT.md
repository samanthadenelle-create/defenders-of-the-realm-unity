# WO-1514 RESULT - every hardcoded repo root is gone from the scripts; the "one helper" half was not built

**Status:** DONE except one acceptance clause - uncommitted in the working tree as of 2026-09-06 21:45, awaiting
the wave-two gate.
**Commit:** none. Edit-only lane, tooling silo (no C#, no Unity).
**Files:** nine `dev/tmp/*.py` (`commit_dungeon`, `commit_lanes`, `commit_mirror_assets`, `commit_wave3`,
`commit_wave4`, `lanes`, `mint_felt`, `pull_codex_six`, `split_mon`), all `M`. The two untracked root scripts
`slice-manage-ui.py` and `slice-manage-ui-v2.py` are **DELETED** - no longer on disk.
**Gates:** not applicable and none run - these are Python scripts outside `Assets/`, which the compile gate does
not read. `Builds/cg-quiet.log` (20:04) and the RED `Builds/cg-aab.log` (20:54) are both irrelevant here.

## 1. What landed, measured

`git grep -n 'D:\eoa\|C:\eoa' -- '*.py' '*.ps1'` returns **zero hits** across the whole tracked tree. The
substitution is the same shape in every file, e.g. `dev/tmp/lanes.py`:

```python
-ROOT=r'D:\eoa'
+from pathlib import Path
+# CLAUDE.md sec.0 (owner ruling 2026-08-09): the repo root is MACHINE-DEPENDENT
+# (C:\eoa on one seat, D:\eoa on another) - resolve it from this script's own
+# location, never hardcode a drive letter. dev/tmp/<script>.py -> parents[2].
+ROOT = str(Path(__file__).resolve().parents[2])
```

Each file carries the ruling's date and reason inline, so the next seat sees why rather than just what. The ticket
said ten files under `dev/tmp/`; there are **nine** `.py` there - the count in sec.1 was off by one, no tenth file
is missing. `ProjectSettings.asset:273` (keystore) is untouched as sec.1 directed - it is overwritten at build
time by `AndroidBuild.cs:399` from `keystore.properties`.

## 2. Acceptance

- [x] Zero hits for `D:\eoa` or `C:\eoa` in tracked scripts - grep in sec.1, zero.
- [x] The two root scripts tracked under `tools/` or deleted - **DELETED**. Untracked scratch, so no history is
      lost and nothing references them.
- [ ] One resolver helper, twelve callers - **NOT MET AS WRITTEN**. `grep -c 'parents\[2\]' dev/tmp/*.py` returns
      `2` in each of the nine files (comment line plus expression), i.e. nine INLINE copies of the same one-line
      walk, not one imported helper. Behaviour is correct on both machines, but the duplication the clause guards
      against is still present - the same argument CLAUDE.md sec.0/sec.2 make. Drift risk is low (a one-line copy,
      not a stale constant), yet the criterion is open, and calling it met would be the hearsay sec.11B forbids.

## 3. Owed

Either a `dev/tmp/_root.py` (or `tools/repo_root.py`) that the nine import, or an explicit owner ruling that nine
inline `parents[2]` walks are acceptable and the clause is retired. No gate and no device capture apply.
