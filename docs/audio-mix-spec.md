# Audio Mix Specification

**Status:** Canonical mix-direction for the audioManager track registry. Owner-locked 2026-05-18.
**Owner:** DeNelle Studios
**Purpose:** Single source of truth for every music track's default volume, fade behavior, and transition pattern. Replaces ad-hoc volume settings scattered across mount sites.

---

## 1. Philosophy

The bible voice ("short sentences with weight; quiet stakes; old bones, modern English; no grimdark") dictates the audio register. **Music sets the bones of the moment; it does not perform.** Sound design (footsteps, ambient, combat SFX, NPC speech bubbles, lantern crackle, dragon-flame shimmer) sits ABOVE the music in the mix at every game state. Music ducks or peaks depending on whether the moment is meant to land or breathe.

Two practical consequences:

- **Long-session states (village, dungeon) play music quietly** so the player isn't fatigued after 30 minutes.
- **Short emotional beats (title, victory, defeat) play music louder** because they're transient and meant to leave a mark.

The master volume slider in Settings scales every track's default volume multiplicatively. A player who sets master to 0.5 hears every track at half its tuned default.

## 2. The track registry — owner-locked mix

Per-track defaults land in `src/lib/audioManager.ts` (or wherever the track registry lives — grep for `playTrack` to find it). All values are master-volume-pre-scale; the manager multiplies by master.

| Track key   | Source file                                  | Default volume | Loop | Fade-in | Fade-out | Notes                                                          |
| ----------- | -------------------------------------------- | -------------- | ---- | ------- | -------- | -------------------------------------------------------------- |
| `title`     | `/audio/title.mp3`                           | **0.6**        | ✅    | 1200ms  | 1000ms   | Title screen + opening cinematic. Moderate — supports the 14-line narrative without competing. |
| `village`   | `/audio/village.mp3`                         | **0.4**        | ✅    | 1200ms  | 1000ms   | Long-session exploration music. Soft so players aren't fatigued. |
| `dungeon`   | `/audio/dungeons/echoes-beneath-elarion.mp3` | **0.25**       | ✅    | 1200ms  | 1000ms   | Very soft — ambient only. Owner directive 2026-05-18. Footsteps + NPC bubbles + sound design all sit above it. |
| `battle`    | `/audio/battle.mp3`                          | **0.7**        | ✅    | 600ms   | 600ms    | ATB Last Stand combat. Featured — drives tension. Crossfade is quick because combat is a moment, not a state. |
| `victory`   | `/audio/victory.mp3`                         | **0.7**        | ❌    | 200ms   | 800ms    | Plays once on battle win. Hard fade-in (celebratory beat), gentle fade-out into next state. |
| `defeat`    | `/audio/defeat.mp3`                          | **0.5**        | ❌    | 1500ms  | 1500ms   | Plays once on battle loss. Softer than victory — bible voice: "the Hollow Ones are grief, not Sauron." Let the loss land slowly. |

**No track defaults to 1.0.** Even at max settings, the audioManager scales down — leaves headroom for the master slider to PUSH louder than 1.0 if a player wants (cap at 1.5× via the slider's max).

## 3. State-transition crossfades

When a state change requests a new track, the manager crossfades. The transition pattern matters as much as the volume.

| From         | To           | Pattern                                | Why                                                                                 |
| ------------ | ------------ | -------------------------------------- | ----------------------------------------------------------------------------------- |
| `title`      | `village`    | Crossfade 1200ms                       | Soft transition into long-session music                                             |
| `village`    | `dungeon`    | Crossfade 1200ms                       | Mood shift — quieter, sets the dungeon's "quiet stakes" register                    |
| `village`    | `battle`     | Crossfade 600ms                        | Combat ramp-up; tension rising fast                                                 |
| `dungeon`    | `battle`     | Crossfade 600ms                        | Same                                                                                |
| `battle`     | `victory`    | Hard cut + 200ms fade-in on victory     | Celebratory beat needs the cut to land. No mush.                                    |
| `battle`     | `defeat`     | 1500ms cross-fade                       | Slow grief beat. The loss should sink in, not slap.                                 |
| `victory`    | `village`    | Crossfade 1000ms                       | Return to base; gentle                                                              |
| `victory`    | `dungeon`    | Crossfade 1000ms                       | Return to dungeon if the win was a random encounter / scripted lore fight inside    |
| `defeat`     | `village`    | Crossfade 1500ms                       | Slow return; gives the player breathing room before the village's softness lands   |
| `dungeon`    | `village`    | Crossfade 1200ms                       | Dungeon exit; warm return                                                           |

The audioManager's existing `currentTrack === track` short-circuit prevents thrash on identical-track requests.

## 4. Special-case volume nudges

These are non-default modulations applied by specific game events on top of the per-track default. The audioManager should support a temporary volume override that auto-reverts after a window.

| Event                                                | Track    | Volume dip                  | Duration | Purpose                                                       |
| ---------------------------------------------------- | -------- | --------------------------- | -------- | ------------------------------------------------------------- |
| **Checkpoint shrine activated** (v1.1+)              | `dungeon`| 0.25 → 0.15 → back to 0.25  | 4s total | Marks the "deep breath" beat. Subtle.                         |
| **First Watch Stop fires** (any of 9 triggers)       | `village`| 0.4 → 0.28 → back to 0.4    | 5s       | Lets the Mentor's bible-voiced line land without music competition |
| **Lore stone read in dungeon** (any of 4 per Cottage)| `dungeon`| 0.25 → 0.12 → back to 0.25  | 6s       | Same reasoning — the journal entry IS the moment              |
| **Boss intro cinematic** (any dungeon boss)          | `dungeon`| 0.25 → 0.0 → silence        | until battle starts | Total silence before the boss reveal. Then `battle` track hard-cuts in. |
| **Dragon hero-banner first-load on title screen**    | `title`  | 0.6 → 0.4 → back to 0.6     | 8s       | Lets the player register the Heart-Wing visually before music re-asserts |

All nudges respect `prefers-reduced-motion: reduce` by snapping (no fade) AND respect master volume scaling.

## 5. Reduced-motion / accessibility

- **`prefers-reduced-motion: reduce`** — every fade in §3 and §4 becomes a hard cut OR snaps to target volume instantly. No animated transitions. Track changes are instantaneous.
- **Master mute toggle** — exists in Settings; cuts all music immediately, ignores fade durations.
- **First-tap unlock** — covered by T52 (`docs/launch-triage-2026-05-18.md`). The audio context unlocks on the first user gesture; until then, all `playTrack()` calls queue and fire as a hard cut on first gesture.

## 6. Implementation pattern

```ts
// audioManager.ts — extend the track registry shape:

interface TrackDefinition {
  src: string;
  defaultVolume: number;   // pre-master, 0..1.0
  loop: boolean;
  fadeInMs: number;
  fadeOutMs: number;
}

const TRACKS: Record<TrackKey, TrackDefinition> = {
  title:    { src: '/audio/title.mp3',                           defaultVolume: 0.6,  loop: true,  fadeInMs: 1200, fadeOutMs: 1000 },
  village:  { src: '/audio/village.mp3',                         defaultVolume: 0.4,  loop: true,  fadeInMs: 1200, fadeOutMs: 1000 },
  dungeon:  { src: '/audio/dungeons/echoes-beneath-elarion.mp3', defaultVolume: 0.25, loop: true,  fadeInMs: 1200, fadeOutMs: 1000 },
  battle:   { src: '/audio/battle.mp3',                          defaultVolume: 0.7,  loop: true,  fadeInMs: 600,  fadeOutMs: 600  },
  victory:  { src: '/audio/victory.mp3',                         defaultVolume: 0.7,  loop: false, fadeInMs: 200,  fadeOutMs: 800  },
  defeat:   { src: '/audio/defeat.mp3',                          defaultVolume: 0.5,  loop: false, fadeInMs: 1500, fadeOutMs: 1500 },
};

// playTrack reads the per-track config:
function playTrack(key: TrackKey, opts?: { fadeMs?: number; volumeOverride?: number }) {
  const cfg = TRACKS[key];
  const fadeMs = opts?.fadeMs ?? cfg.fadeInMs;
  const targetVolume = (opts?.volumeOverride ?? cfg.defaultVolume) * masterVolume;
  // … existing crossfade logic with the resolved volume
}

// Temporary nudge helper for §4 events:
function nudgeVolume(key: TrackKey, toVolume: number, durationMs: number, fadeMs: number = 400) {
  // dip to toVolume over fadeMs, hold for (durationMs - 2*fadeMs), restore over fadeMs
}
```

## 7. Unity port note

For the parallel Unity port (`docs/v2-unity-port-spec.md`), this entire spec maps to Unity's AudioMixer with parallel volume settings on six AudioSource components, one per track. The `nudgeVolume` helper becomes a coroutine that tweens the AudioSource volume over the duration. The crossfade table in §3 lives in a `MusicDirector` MonoBehaviour. Per-track defaults stored as a ScriptableObject so the values stay owner-tunable post-build.

## 8. Acceptance

A reviewer with the live build should be able to:

1. Open Settings → confirm a master volume slider exists and ranges 0..1.5×.
2. Hear `'title'` at moderate volume on first load — present but not competing with the 14-line cinematic.
3. Enter the village → music softens to ~0.4 master-scaled.
4. Enter a dungeon → music drops to ~0.25 master-scaled. **Music is quiet enough that footsteps and Bryn's speech bubble are clearly audible above it.**
5. Trigger a random encounter → music transitions to `'battle'` at ~0.7 master-scaled, crossfade is quick.
6. Win the battle → hard cut into `'victory'`, then smooth return to dungeon music at 0.25.
7. Lose a battle → slow 1500ms crossfade into `'defeat'` at 0.5 — the music sits with the loss instead of moving on quickly.
8. Read a dungeon lore stone → music dips to 0.12 for 6 seconds, then restores. The dip is subtle, not jarring.
9. Toggle `prefers-reduced-motion: reduce` in DevTools → all fades become hard cuts.
10. Master-mute via Settings → all music stops immediately, regardless of which track is playing.

## 9. Owner-tunable knobs

Every value in §2 and §4 is a number constant in `audioManager.ts`. Owner can adjust by editing the constant and pushing — no architectural change needed. Common tunings if playtest reveals issues:

- **Dungeon too quiet?** Bump `defaultVolume` from 0.25 → 0.3
- **Village fatiguing on long sessions?** Drop from 0.4 → 0.32
- **Combat not hitting hard enough?** Bump `battle` from 0.7 → 0.8 (and consider 0.85 for the boss-fight overlay)
- **Title competing with the cinematic?** Drop from 0.6 → 0.45
- **Defeat track feels too sad?** Stays at 0.5 — bible voice says the dark mourns. Resist the urge to mute it harder.

---

_Music is the bones of the moment. The bones are quiet. Tend the Heart. Hold the dark._
