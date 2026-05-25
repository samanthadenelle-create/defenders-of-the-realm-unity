# WORK ORDER 12 — RESULT (structural regression check; behavioral re-verify is eyes-on)

**Executed:** 2026-05-24 (evening) under Standing Authority #35 + WO-025
**Baseline:** commit `36a8ec0` (pre-recovery last-known-good).
**Outcome:** No structural regression. **No committed change touched any curated scene, project setting, or package manifest since the baseline.** Build is green and all five buildable scenes boot clean. Every key recovery fix is still on disk. Behavioral feature re-verification + save/load round-trip remain owner-eyes-on (now easy via `-bootScene`).

---

## 1. Headline — critical-file A/B (task 2.5)

`git diff 36a8ec0 HEAD --` on the WO's critical files returns **empty**:

```
Village.unity, ATBBattle.unity, HeroSelect.unity, PetSelect.unity, Title.unity,
ProjectSettings.asset, QualitySettings.asset, manifest.json, packages-lock.json
  → 0 committed changes since 36a8ec0
```

**No unexpected scene/settings/manifest diffs exist** — because *no commit since the baseline modified any of them.* The entire recovery + WO-05…WO-21 sequence was achieved without committing a single curated-scene or project-settings change (the hard-rule discipline held throughout; runtime-attach patterns were used instead of scene edits). The working tree has *uncommitted* reimport churn on some of these files, but that is local Unity-reimport state, not a committed regression.

Full `36a8ec0→HEAD` diff = 47 files, +2240/−232 — all accounted for: recovery code (`TripoMaterialFixer`, `HeroBodySwapper`, `build-windows.ps1`), this session's WO code (`Dungeon*`, `Gate*`, `HeartHudBridge`, `HeroAbilities*`, `VillageController`, `DevBootScene`, recovered `hexagons_medieval.mat`), and docs/RESULTs. No stray edits.

## 2. Build + scene-load regression signal

- `[DesktopBuild] SUCCEEDED` (559 MB, 0 compile errors/warnings) — nothing compile-broke.
- `-bootScene` boot of **all 5 buildable scenes** (Title, Village, ATBBattle, Dungeon_HealersCottage, Dungeon_FolksGranary) → each loads, stays alive, **0 runtime errors** — nothing load-broke.
- Village runtime: pets `loaded=True/tintActive=True`, HUD `Bound`, `magenta/InternalError` count 0, entrances placed — the recovery's headline symptoms (magenta art, invisible pets) are gone in the player build.

## 3. Recovery fixes — still on disk (task 2.4)

| Recovery fix | On disk? |
|---|---|
| `TripoMaterialFixer` Awake→`Start() => Run()` | ✅ |
| Wave-1 timer 300s (`waves.json`) | ✅ (`300` present) |
| `GameAudioMixer.mixer` (de-corrupted) | ✅ present |
| `HeroAnimatorSetup` → `Assets/Resources/Heroes/` paths | ✅ |
| `SceneCleanupTools.cs` | ✅ present |
| `HeroBodySwapper` hardening | ✅ (in committed diff) |
| KayKit `.meta` GUID remaps | ✅ implied — Village builds + boots with 0 missing-shader/material (the village KayKit refs resolve) |

## 4. Recovery objectives (`docs/recovery-work-orders.md` Agents 1–7) — advanced by this WO sequence

That doc is the original symptom list. Mapping to delivered work:

| Agent objective | Status |
|---|---|
| 1 & 2 — GUID/material so buildings render correct colours (not magenta) | ✅ **WO-05** (recovered the shared hex atlas material; 0 magenta build-verified) |
| 3 — dungeon portal loads a real dungeon scene, not a stub | ◑ **WO-19** (entrances `SceneRouter.LoadScene(Dungeon_*)` to the real scenes; interior styling separate) |
| 4 — top-left HUD shows HP + Mana + Spire health | ✅ **WO-07** (mana push) + **WO-20** (Heart/Spire HP + crystals push) — all 4 readouts now fed |
| 5 — Build button opens build menu | ◑ **WO-06** (HUD + Build-button→BuildMenu.Open wiring verified; full flow eyes-on) |
| 6 — master volume toggle on HUD | ☐ not in scope of any WO yet (settings — eyes-on) |
| 7 — village/exterior architecture | ☐ long-term design (out of scope) |

## 5. Acceptance criteria

| AC | Status |
|---|---|
| 1. OVERNIGHT_REPORT working-claims re-verified | ◑ **structural** signals all green (build + 5-scene boot + recovery fixes on disk); **behavioral** re-verify (does each feature *play* the same) is eyes-on |
| 2. Recovery fixes re-verified | ✅ §3 |
| 3. Unexpected scene-file diffs documented | ✅ **none** — no committed critical-file change since baseline (§1) |
| 4. Save/load round-trip | ☐ eyes-on (needs a played session + relaunch; not headlessly drivable) |
| 5. This RESULT.md | ✅ |

## 6. Remaining (eyes-on)

- Behavioral re-verification of `OVERNIGHT_REPORT.md`'s feature claims (combat feel, wave loop, audio, settings) — now much easier with `-bootScene <name>` to jump straight to any scene.
- Save/load round-trip (play → save → relaunch → confirm restore).
- A/B build of `36a8ec0` (WO §2.2) was **skipped** as optional — moot at the scene/settings level since those files are byte-identical to the baseline in every commit.

**Bottom line: no structural regression detected.** The recovery + WO sequence added/fixed code and a recovered material without disturbing any curated scene or project setting at the commit level, and every buildable scene loads clean in the player.
