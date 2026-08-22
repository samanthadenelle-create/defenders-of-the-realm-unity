**Status:** READY TO IMPLEMENT — RCA complete; the fix is one argument at each of FOUR call sites (§4)

# WORK ORDER 1054 — An OPTIONAL sfx override miss raises a FALSE error and trips F8

**Minted:** 2026-08-22 (UI seat — Claude UI; UI-block banner bumped 1054 -> 1055 in the SAME edit)
**Assigned:** CLI implements. UI writes no `.cs` (CLAUDE.md §2).
**Lane:** Core / instrumentation
**Class:** DEFECT — **instrumentation**, not audio. No sound is missing.
**Source:** F8 captures **seq=3577, 3579, 3580, 3582** (see §2b — four keys in 27 minutes). First:, `logs/f8-inbox/capture-20260822-104813-seq3577.md`,
scene `Main_Castle_Overworld`, 2026-08-22 10:48:12. **This capture cost the owner an F8 press on a
non-problem.**

---

## 0. One-line truth

**`Sfx/Strike` is an OPTIONAL override that the caller already survives — and the loader reports its
absence as a hard error.** Nothing is silent. The ability plays a generated clip exactly as designed.
The only defect is that a working code path screams.

---

## 1. RCA — complete, from the captured trace (no repro needed)

The captured stack:

```
AudioAssetLoader:Load<AudioClip>      (AudioAssetLoader.cs:197)   <- FlowTrace.Fail here
AudioAssetLoader:LoadClip             (AudioAssetLoader.cs:121)
ProceduralSfx:ForKind                 (AbilityAudioBridge.cs:91)  <- the caller
ProceduralSfx:ForClassAndKind         (AbilityAudioBridge.cs:135)
AbilityAudioBridge:PlayForClassAndKind(AbilityAudioBridge.cs:31)
HeroAbilities:SpawnVfx                (HeroAbilities.cs:2777)
```

**The caller, `AbilityAudioBridge.cs:91`:**

```csharp
// TODO(sfx): drop a CC0 wav at the audio key "Sfx/<Kind>" to OVERRIDE the generated clip
AudioClip clip = DeNelle.Core.AudioAssetLoader.LoadClip("Sfx/" + kind) ?? Generate(kind);
```

The `?? Generate(kind)` **is** the fallback. A miss here is the designed, expected path.

**The loader, `AudioAssetLoader.cs:117-121`** — the API already has the parameter for this, and its
own doc comment names this exact caller as the canonical example:

```csharp
/// <param name="optional">TRUE when the CALLER can survive a miss — a synth-fallback SFX
/// key, or a pooled rotation extra. Downgrades the both-paths-missed report from Fail
/// (error-level, trips F8) to Warn. Defaults to FALSE so a required clip going missing
/// stays loud...</param>
public static AudioClip LoadClip(string key, bool optional = false)
```

**`ProceduralSfx.ForKind` never passes it**, so it takes the `false` default and lands in the `else`
branch at `:197`, which emits `FlowTrace.Fail` — error level, trips F8 — with the text *"REQUIRED by
its caller."*

**That sentence is false for this caller.** The loader is not wrong and the default is not wrong
(reporting too much is the right default, as its comment says); the **call site failed to declare
itself**.

## 2. ⚠ It is not one key — it is every unauthored effect kind

`Resources/Sfx/` currently holds **four** clips: `Heal.mp3`, `LookoutHorn.wav`, `Spell_Impact.mp3`,
`Swords_Clash.mp3`. There is no `Strike`.

Since the key is built as `"Sfx/" + kind` over `AbilityEffect`, **every effect kind without an
authored override fires this false error the first time it plays** (`s_reportedMisses` dedupes to
once per key, per session). The owner will keep collecting them as she plays different abilities.

## 2b. FOUR keys in 27 minutes — the §2 prediction, confirmed live

| seq | Key | Call site | Time |
|---|---|---|---|
| 3577 | `Sfx/Strike` | #1 `AbilityAudioBridge.cs:91` | 10:48:12 |
| 3579 | `Sfx/Sfx_ComboSmall` | #2 `ProceduralSfx.cs:64` | 10:49:37 |
| 3580 | `Sfx/Sfx_WaveClear` | #2 `ProceduralSfx.cs:64` | 10:49:41 |
| 3582 | `Sfx/Sfx_ComboBig` | #2 `ProceduralSfx.cs:64` | **11:15:35** |

**Four owner F8 presses, on four non-problems, inside one play session.** seq=3582 arrived through
`KillComboTracker.OnKillStreakChanged -> VFXManager.Play -> AudioService.PlaySfxAtPosition ->
ProceduralSfx.For` — i.e. **a kill streak in normal play makes the game report an error.**

⚠ **And site #2 documents its own tolerance three lines above the call:**

```csharp
// Authored CC0/recorded drop-in wins over the synth (same convention as GameSfx): the audio
// key "Sfx/Sfx_<Id>" ... Missing -> fall through to synth.
AudioClip clip = DeNelle.Core.AudioAssetLoader.LoadClip("Sfx/" + ResourceName(id)) ?? Generate(id);
```

*"Missing -> fall through to synth"* — **the caller states in a comment that the miss is survivable,
and then does not say so in the argument that exists to say it.** That is the entire bug, written
down next to itself.

## 3. The fix

```csharp
// AbilityAudioBridge.cs:91
AudioClip clip = DeNelle.Core.AudioAssetLoader.LoadClip("Sfx/" + kind, optional: true) ?? Generate(kind);
```

That is the whole change. It moves the report from `Fail` to `Warn`, which is the branch already
written at `:190-194` and which says the right thing:

> *"OPTIONAL, the caller declared this miss survivable (synth SFX fallback...). Reported ONCE per key
> so a thin rotation stays visible rather than invisible. NOT an error and NOT silent audio."*

⛔ **Do NOT "fix" this by authoring a `Strike` clip.** That silences one key and leaves every other
effect kind still screaming. And ⛔ **do NOT flip the loader's default to `optional: true`** — that
would make a genuinely missing music track silent *and* quiet, which is the failure the default
exists to prevent.

## 4. The sweep is DONE — there are exactly FOUR `"Sfx/"` call sites, and ALL FOUR are tolerant

Grepped this session. **Every one of them survives a miss, and not one declares `optional`.**

| # | Call site | Miss behaviour | Fix |
|---:|---|---|---|
| 1 | `Village/Hero/AbilityAudioBridge.cs:91` | `?? Generate(kind)` — **synth fallback, no sound is lost** | `optional: true` |
| 2 | `Audio/ProceduralSfx.cs:64` | `?? Generate(id)` — **synth fallback, no sound is lost** | `optional: true` |
| 3 | `Village/Hero/HeroAbilities.cs:2485` | null-checked; **already logs its own** `FlowTrace.Once("sfximpact-missing:…")` | `optional: true` |
| 4 | `Village/Vfx/ActionBundlePlayer.cs:316` | null-checked; **already logs its own** `FlowTrace.Once("sfx-missing:…")` | `optional: true` |

**Sites 1 and 2 are the false-alarm class** — a generated clip plays, so the error is simply wrong.

**Sites 3 and 4 are a different and slightly worse bug: DOUBLE REPORTING.** Those two genuinely go
silent on a miss — but they *already* say so, in their own words, at `Once` level, with better text
than the loader has (they name the row's `sfxId` and tell you where to drop a WAV). The loader's
`Fail` on top of that is a **second report of the same fact, at error level, tripping F8**. Declaring
them optional keeps their accurate report and drops the redundant loud one.

⚠ **Do not "fix" 3 and 4 by deleting their own `Once` logs instead.** Those are the reports that
carry the actionable detail; the loader's generic one is the duplicate. Keep the specific, drop the
generic.

**Excluded from the sweep:** `Editor/MotionCasterWindow.cs:1391` uses a raw
`Resources.Load<AudioClip>` and never touches this loader. Editor-only, leave it.

## 5. Why this is worth a ticket at all

A false ERROR is worse than no log. It trips F8, it lands in the owner's inbox, it costs a triage
cycle — and repeated often enough it teaches every seat to skim past errors, which is exactly the
instinct CLAUDE.md §12 and §14 exist to build in the opposite direction. **The owner must never be
the bug detector, and she especially must not be the detector for bugs that are not bugs.**

## 6. Acceptance

1. Playing a mage ability with no authored override emits a **Warn**, not a Fail, and **does not
   trip F8**.
2. Audio is unchanged — the generated clip still plays. Confirm by ear or by a non-null clip assert.
2b. Sites 3 and 4 still report their miss **once**, in their own words — the duplicate loader error
   is gone, the specific one remains.
2c. **Replay the three captures that produced this ticket** — `Sfx/Strike` (seq 3577),
   `Sfx/Sfx_ComboSmall` (3579), `Sfx/Sfx_WaveClear` (3580). None may trip F8 afterwards.
3. A genuinely required miss (e.g. a music track's primary clip) **still emits Fail**.
4. `COMPILE_GATE_OK`; brace-check every `.cs` touched.

## 7. Files

**Edit — all four, named in §4:** `Assets/_Modules/Village/Hero/AbilityAudioBridge.cs:91` ·
`Assets/_Modules/Audio/ProceduralSfx.cs:64` · `Assets/_Modules/Village/Hero/HeroAbilities.cs:2485` ·
`Assets/_Modules/Village/Vfx/ActionBundlePlayer.cs:316`

**Read, do not edit:** `Assets/_Modules/Core/Addressables/AudioAssetLoader.cs` — the loader and its
default are correct as written.
