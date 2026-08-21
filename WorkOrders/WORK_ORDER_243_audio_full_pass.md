**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 243 — Audio Full Pass ("Sound Everything")

**Status: READY TO IMPLEMENT**
**Author:** UI (creative lane)
**WO Number:** 243
**Date:** 2026-06-02

---

## Philosophy

Every meaningful moment in the game should have a sound. Not every pixel — every *moment*. The audio tells the player where they are, how safe they are, and whether something just mattered. If the player closes their eyes and plays for 10 seconds, they should know exactly what state the game is in.

All audio routes through the existing `IAudioService` / `AudioService` / `CoreServices.Audio` path. No new audio managers. No TMPro. Null-conditional (`?.`) on all service calls.

---

## 1. Music — state-based, one track per state

| State | Track | Mood |
|---|---|---|
| Village — calm (no wave) | `Music_Village_Calm` | Warm, low, the Heartwood hums. Strings + light woodwind. |
| Village — wave incoming (30s before) | `Music_Village_Alert` | Tension rises. Percussion enters, strings quicken. |
| Village — under attack | `Music_Village_Combat` | Full combat drive. Urgent, rhythmic, never chaotic. |
| Village — wave cleared | `Music_Village_Victory` | 8-bar resolution. Warm swell then back to Calm. |
| OuterWorld — exploration | `Music_World_Explore` | Open, wandering. Lighter than village. Wind instruments. |
| OuterWorld — combat nearby | `Music_World_Combat` | Faster tempo, darker. Enemy aggro triggers this. |
| Outpost claimed | `Music_Outpost_Settle` | Short 4-bar sting — "you own this now." Then fades to Explore. |
| ATB battle | `Music_ATB_Battle` | Tense, rhythmic. Already specced in DEF-36 / WO-76. |
| ATB — last chance (HP < 25%) | `Music_ATB_LastChance` | Crimson, ominous. Already specced in LastChanceLightingPreset. |
| Hero death | *(silence)* | One soft held tone only. NOT the defeat track. |
| Heartwood destroyed | `GameOver.mp3` | Already in `Assets/Resources/Audio/Music/GameOver.mp3`. |
| Main menu | `Music_MainMenu` | Ambient, the Heartwood's chord. Slow, patient. |

**Transitions:** crossfade 1.5s between states. `AudioService` should expose `PlayMusic(MusicTrack, float fadeIn)`.

---

## 2. Heartwood — living ambient sounds

The Heartwood is alive and regrowing. It should sound like it.

| HP state | Sound |
|---|---|
| 100% — 75% | `Sfx_Heartwood_Healthy` — low warm hum, leaves rustling, occasional crystal chime. Loop. |
| 74% — 40% | `Sfx_Heartwood_Strained` — hum gets deeper, occasional groan, chimes less frequent. |
| 39% — 15% | `Sfx_Heartwood_Critical` — dissonant undertone, bark cracking sounds, wind picks up. |
| Under attack (receiving damage) | `Sfx_Heartwood_Hit` — deep resonant impact, like a bell struck hard. |
| Destroyed | `Sfx_Heartwood_Fall` — long descending tone, crack, then silence. |

Subscribe to `HeartController.OnHealthChanged` and `OnDestroyed`.

---

## 3. World — ambient environment sounds

| Location | Sounds |
|---|---|
| Village plaza (near Heartwood) | Birdsong, distant market chatter, fire crackle from torches, Heartwood hum (positional) |
| Village walls | Wind, occasional guard footstep, distant banners |
| OuterWorld — forest/fields | Birds, wind in grass, distant wolf (far), branch snap (random) |
| OuterWorld — near enemy camp | Hostile ambience — deeper wind, silence where birds should be |
| Outpost (claimed) | Woodchopping, hammer on iron, workers murmuring |
| Night / raid approaching | Hollow Ones counter-note begins as distant hum, grows as raid nears |

All ambient: positional AudioSources, 3D spatial blend, loop. Fade based on distance from player.

---

## 4. Hero SFX

| Action | Sound |
|---|---|
| Footstep (walk) | `Sfx_Hero_Step_Grass` / `Sfx_Hero_Step_Stone` — 4-sample pool, random pitch ±5% |
| Footstep (sprint) | Faster cadence, slightly heavier |
| Jump / land | `Sfx_Hero_Land` |
| Basic attack | Per-class: Knight `Sfx_Grom_Swing`, Ranger `Sfx_Sylas_Arrow`, Wizard `Sfx_Thrain_Cast`, Healer `Sfx_Elara_Strike` |
| Ability cast | Per-ability SfxId (existing in `SfxClipLibrary`) |
| Take damage | `Sfx_Hero_Hit` — short grunt, 2-sample pool |
| Death | `Sfx_Hero_Death` — longer exhale, body fall |
| Heal received | `Sfx_Hero_Heal` — soft warm chime |
| Level up | `Sfx_Hero_LevelUp` — rising sparkle |

---

## 5. Combat SFX

| Action | Sound |
|---|---|
| Enemy footstep | `Sfx_Enemy_Step` — heavier, 2-sample pool |
| Enemy attack wind-up | `Sfx_Enemy_Telegraph` — short hiss/growl |
| Enemy hit (receives damage) | `Sfx_Enemy_Hit` — wet impact, per-type variation |
| Enemy death | `Sfx_Enemy_Death` — per-type (Frost-Voice: shattering ice; Ember-Voice: ember burst; Half-Voice: fading wail) |
| Wave spawn (enemies entering) | `Sfx_Wave_Spawn` — ominous distant horn |
| Wave cleared | `Sfx_Wave_Clear` — brief triumphant sting |
| Boss approaching | `Sfx_Boss_Approach` — deep reverberant footsteps |

---

## 6. Village / Building SFX

| Action | Sound |
|---|---|
| Building placed | `Sfx_Build_Place` — satisfying thud + settle |
| Building upgrade | `Sfx_Build_Upgrade` — construction sounds, finish with a small chime |
| Building damaged | `Sfx_Build_Hit` — crack / wood splintering |
| Building destroyed | `Sfx_Build_Destroy` — collapse, dust |
| Gate opens | `Sfx_Gate_Open` — heavy iron mechanism, chain |
| Gate closes | `Sfx_Gate_Close` |
| Wall repaired | `Sfx_Wall_Repair` — stone grinding, mortar |

---

## 7. Node / Outpost SFX

| Action | Sound |
|---|---|
| Camp cleared (last enemy dies) | `Sfx_Camp_Clear` — brief silence then a short rising tone |
| Node claimed | `Sfx_Node_Claim` — ownership sting, like a flag being planted |
| Outpost Hall spawns | `Sfx_Outpost_Spawn` — wood construction burst, 2 seconds |
| Building placed at outpost | `Sfx_Build_Place` (reuse) |
| Worker ambient (loop) | `Sfx_Worker_Hammer` — rhythmic, looped, low volume |
| Workers flee (raid starts) | `Sfx_Worker_Scatter` — brief alarmed shout, footsteps |
| Outpost under raid | `Sfx_Raid_Attack` — drums, enemy war cry |
| Outpost destroyed | `Sfx_Outpost_Destroy` — collapse, then mournful single note |
| Raze in progress | `Sfx_Raze_Progress` — slow demolition sounds, looping |
| Raze complete | `Sfx_Raze_Complete` — final crash, then silence |

---

## 8. Resource SFX

| Action | Sound |
|---|---|
| Pet harvests a node | `Sfx_Harvest_Collect` — per-type: Wood = axe+creak, Iron = metal clink, Crystal = chime, Food = soft rustle |
| Resource banked (HUD updates) | `Sfx_Resource_Bank` — small coin-type chime, very brief |
| Resource cap reached | `Sfx_Resource_Full` — gentle warning tone |
| Can't afford (upgrade/build) | `Sfx_UI_CantAfford` — dull thud |

---

## 9. UI SFX

| Action | Sound |
|---|---|
| Button tap | `Sfx_UI_Tap` — very short, clean click |
| Panel open | `Sfx_UI_Open` — subtle whoosh |
| Panel close | `Sfx_UI_Close` — reverse whoosh |
| Upgrade purchased | `Sfx_UI_Upgrade` — bright chime |
| Claim prompt appears | `Sfx_UI_Prompt` — soft ping |
| Raid warning appears | `Sfx_UI_RaidWarning` — sharp alert sting (short, 0.4s) |
| Party member joins | `Sfx_Party_Join` — warm chord, brief |
| Error / can't do | `Sfx_UI_Error` — dull low tone |

---

## 10. Companion ambient voice lines

When Sylas, Elara, or Grom say their ambient travel lines (WO-238), a short idle vocal SFX fires before the text appears — a brief "Hmm" or "Hey" that signals speech is coming. This bridges text-only dialogue to a voiced feel without needing full VO recording.

| Companion | Pre-line vocal |
|---|---|
| Sylas | `Sfx_Sylas_Idle_01` through `_04` — short, dry, alert tone |
| Elara | `Sfx_Elara_Idle_01` through `_04` — warm, measured |
| Grom | `Sfx_Grom_Idle_01` through `_04` — low, brief grunt |

These can be AI-generated (ElevenLabs) — 0.5s each. 4 variants per character to prevent repetition.

---

## Implementation notes

**All SFX:** add new `SfxId` entries to `DeNelle.Core` for any sounds not already in the enum. Follow the existing pattern.

**All music:** add new `MusicTrack` entries. `AudioService.PlayMusic(track, fadeIn: 1.5f)` — crossfade between states.

**State machine for music:** Add a `MusicStateController.cs` that listens to:
- `WaveManager.OnWaveStart` / `OnWaveEnd` → toggle village states
- `RegionMobSpawner` aggro events → toggle world states
- `ClaimableNode.OnClaimed` → play outpost sting
- `HeartController.OnDestroyed` → GameOver.mp3

**Volume:** Master → Music (0.7 default) / SFX (1.0 default) / Ambient (0.5 default). Respect existing mute toggle.

**Mobile:** all positional audio uses 3D spatial blend 0.8. Non-positional UI sounds: 2D.

---

## Asset sourcing priority

1. **Reuse existing** — `Assets/Audio/` already has some clips. Use them first.
2. **Free CC0** — freesound.org, OpenGameArt. Check `docs/audio-credits.md` if it exists.
3. **AI-generated** — ElevenLabs for voice lines, Suno/Udio for music stings.
4. **Placeholder** — Unity's built-in `AudioClip` generation for anything not sourced. Ship with placeholder, replace post-launch.

---

## Acceptance criteria

- [ ] Music changes state correctly: calm → alert → combat → victory in village
- [ ] Heartwood ambient hum plays and changes with HP
- [ ] Every enemy death has a typed sound
- [ ] Every building action (place/upgrade/destroy) has sound
- [ ] Node claim + outpost spawn have distinct sounds
- [ ] All UI buttons produce tap feedback
- [ ] Raid warning has audio sting
- [ ] Companion ambient lines have pre-line vocal
- [ ] Music crossfades smoothly (no hard cuts)
- [ ] Mobile: no audio latency > 100ms on tap sounds
- [ ] Mute toggle respected across all sounds

## What NOT to touch
- Existing `SfxId` enum values — only add, never rename or remove
- `ATBSoundManager` (WO-76 / DEF-36) — ATB audio is separate, this WO is village/world only
- `Village.unity` — do not hand-edit

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
