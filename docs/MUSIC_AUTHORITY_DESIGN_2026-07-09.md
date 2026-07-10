# Music Authority — Single-Bed Singleton Design (2026-07-09)

**Status:** DESIGN — owner-confirmed, READY TO IMPLEMENT. **Owner directives (2026-07-09):**
"all must push as a singleton", "two cannot ever exist", "use design methodology", "we use all
strong structures and classes", "no shortcuts". Resolution model = **priority-layer stack (owner-selected)**.

**Ticket:** F8 (2026-07-09) "two songs at once. fix this" in `Main_Castle_Overworld`.

---

## The problem (verified from code, not comments)

Music can double because **three independent players each own their own music AudioSources** and
run their own crossfades — nothing structurally forbids two beds:

| Player | Owns | Plays via |
|---|---|---|
| `Assets/_Modules/Audio/AudioService.cs` | A/B crossfade sources | `PlayMusic` / `CrossfadeTo` |
| `Assets/_Modules/Village/Audio/BattleMusicManager.cs` | its **own** `_sourceA/_sourceB` (`BuildAudioSources` :513, `CreateSource` :523) | battle-pool crossfade (`Crossfade` :556) |
| `Assets/_Modules/Village/Audio/WaveMusicController.cs` | its **own** `_sourceA/_sourceB` (`CreateMusicSource` :94) | combat/exploration crossfade (`CrossfadeTo` :160) |
| (`HeartwoodAmbientController.cs`) | same A/B-bed pattern (ambient) — audit on touch | — |

`AbilityAudioBridge.PlayMusic(string)` and the scene/flow callers (`BattleArena` :428/:1564/:1712,
`ArenaMode` :191/:491, `GameOverScreen` :194, `StoryIntroController` :163, `IntroSequencePlayer` :228)
are **fine** — they route through `CoreServices.Audio` / `AudioService.PlayMusic`. They are *requesters*,
not players. The defect is the three real players.

A cooperating-stop patch (owner rejected) only ties two players together and still permits overlap.
**The fix is structural: one owner, so two cannot exist.**

---

## The invariant

> **Exactly one class owns music AudioSources.** Every other system *requests* a track through a
> typed contract; none can play one. Two beds are impossible **by construction** — there is one
> playback pair, and it always plays exactly one resolved track.

---

## Strong types (in `DeNelle.Core.Audio` — Core owns the contract; players reference Core only)

```csharp
// Priority order, low -> high. The authority always sounds the HIGHEST active layer.
public enum MusicLayer
{
    None     = 0,
    Ambient  = 1,   // hub / village idle bed
    Overworld= 2,   // open-world exploration
    Wave     = 3,   // wave loop combat/exploration (WaveMusicController)
    Battle   = 4,   // staged battle / arena bed (BattleMusicManager, BattleArena)
    Outcome  = 5,   // victory / defeat sting bed
    Cutscene = 6,   // title / story intro — tops everything
}

public readonly struct MusicRequest
{
    public readonly MusicTrack Track;
    public readonly MusicLayer Layer;
    public MusicRequest(MusicTrack track, MusicLayer layer) { Track = track; Layer = layer; }
}

public interface IMusicAuthority
{
    void Push(MusicRequest req);   // set/replace the track for req.Layer, then re-resolve top
    void Release(MusicLayer layer);// clear that layer, then re-resolve top (auto-fallback)
    MusicTrack Current { get; }    // the single resolved bed currently sounding
}
```

`IAudioService` gains nothing breaking; `IMusicAuthority` is the new seam. `AudioService`
implements both (or delegates music to an owned `MusicDirector`).

---

## `MusicDirector` — the singleton (in `DeNelle.Audio`)

- **Owns the ONLY music AudioSource pair** (the A/B crossfade). Moves the existing 1.5s crossfade
  routine here as the single implementation.
- **State:** a dense `MusicTrack[]` indexed by `MusicLayer` (the active request per layer;
  `MusicTrack.None` = layer inactive). Strong, allocation-free, deterministic.
- **Resolution rule:** `top = highest layer whose track != None`. On every `Push`/`Release`,
  recompute `top`; if `top`'s track differs from the sounding bed, **crossfade the one pair** to it.
  If no layer is active → fade to silence.
- **Auto-fallback:** `Release(Battle)` re-resolves to `Wave` (or `Ambient`) with no caller having to
  "restore ambient" — this deletes the entire class of bug where a caller forgets to restore.
- **Idempotent:** `Push` of the already-sounding top track is a no-op (no re-trigger churn).

---

## Migration — the three players become **policy providers** owning ZERO AudioSources

- **`AudioService.PlayMusic(track)`** → `_director.Push(new MusicRequest(track, LayerFor(track)))`.
  `StopMusic()` → `Release(LayerFor(currentDirectorRequest))` (or `Release(Ambient/Overworld)`).
  Keeps `IAudioService` API stable → every existing caller (BattleArena, ArenaMode, GameOverScreen,
  StoryIntro, AbilityAudioBridge) works unchanged.
- **`BattleMusicManager`** → keep its battle-pool *selection* logic; **delete** `BuildAudioSources`,
  `CreateSource`, `_sourceA/_sourceB`, `CrossfadeRoutine`. Start = `Push(Battle, chosenPoolTrack)`,
  end = `Release(Battle)`.
- **`WaveMusicController`** → **delete** its `_sourceA/_sourceB` + `CreateMusicSource` + local
  `CrossfadeTo`; combat = `Push(Wave, combatTrack)`, exploration = `Push(Wave, explorationTrack)`,
  wave cleared = `Release(Wave)` (auto-falls to Ambient).
- **`HeartwoodAmbientController`** → if it drives a *music* bed, `Push(Ambient)/Release(Ambient)`;
  if it is positional SFX ambience (not a music bed), leave it (audit on implementation).

### `LayerFor(MusicTrack)` (initial mapping — tune in review)
| MusicTrack | Layer |
|---|---|
| Title | Cutscene |
| Victory, Defeat | Outcome |
| Battle, Arena | Battle |
| Raid | Wave |
| Overworld, Dungeon | Overworld |
| Village | Ambient |

---

## Instrumentation (§12 — assert-fail-loud, no silent overlap ever)

- `[Flow:Audio]` `FlowTrace.Step` on every `Push` / `Release` / crossfade: name `layer`, `track`,
  the resolved `top`, and the outgoing bed.
- `FlowTrace.Fail` (LogError → lands in break-log) if the director ever detects **two music sources
  audible at once** — this must never fire; it is the runtime proof of the invariant.
- Retain a `[Flow:Audio]` line at each transition so a headless/felt run can show a single bed.

---

## Proof plan ("confirmed in all ways that fire music" — owner bar)

Every music call site resolves to `Push`/`Release` on the one authority. Verify at EVERY transition:

| Transition | Path |
|---|---|
| Wave countdown → active → cleared | WaveMusicController Push(Wave)/Release(Wave) |
| Battle / arena enter → victory/defeat → exit | BattleMusicManager + BattleArena Push(Battle)/Push(Outcome)/Release |
| Zone crossing mid-wave | WorldMusicDirector → AudioService.PlayMusic → Push(Overworld/Ambient) |
| Additive scene load | scene bootstrap PlayMusic → Push |
| Hero death → EndState | GameOverScreen Push(Outcome/Defeat) |
| Title / story intro | StoryIntro Push(Cutscene) → Release |

- **Headless (where reachable):** compile gate, build, drive the hub wave loop, grep `[Flow:Audio]` —
  assert exactly one active bed at each transition and **zero** `FlowTrace.Fail` overlap lines.
- **Felt (owner):** hub Town → wave → clear → death → (arena if reachable). Exactly one track audible
  at every seam, especially crossing a zone edge mid-wave (the original repro).
- **No shortcut:** not "sounds fine" — the `[Flow:Audio]` trace must SHOW single-bed at each seam
  before the ticket is closed.

---

## Files

**New:** `Assets/_Modules/Core/Audio/MusicLayer.cs`, `MusicRequest.cs`, `IMusicAuthority.cs`
(or one file). `Assets/_Modules/Audio/MusicDirector.cs`.
**Edit:** `AudioService.cs` (delegate music to director), `BattleMusicManager.cs`,
`WaveMusicController.cs`, (`HeartwoodAmbientController.cs` if music).
**Do NOT touch:** SFX (`GameSfx`, `PlaySfx`, mixer groups), clip assignments in `AudioBootstrap.cs`
(only the playback ownership moves), `Village.unity`.

**Assembly law:** new contract types live in `DeNelle.Core.Audio`; `DeNelle.Village` players
reference Core only — never HUD. `MusicDirector` lives in `DeNelle.Audio` (already Core-only).
