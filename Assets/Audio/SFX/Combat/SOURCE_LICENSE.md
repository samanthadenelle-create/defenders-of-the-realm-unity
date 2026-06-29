# Combat SFX — provenance & license

Processed via ffmpeg (trim + loudnorm to -16 LUFS, 44.1kHz). Raw sources from Freesound.

## ⚠ License to verify per file (Freesound mixes CC0 and CC-BY)
Each Freesound sound is either **CC0** (no attribution) or **CC-BY** (must credit the author).
Look up each ID at `https://freesound.org/s/<ID>/` and record the license below. For CC-BY keep an
in-game credits line; prefer CC0 where possible.

| File | Source | Freesound ID | License | Use |
|------|--------|--------------|---------|-----|
| sword_clash_1..4.wav | "sword against sword" | 6341 | TODO verify | melee hit (4 variations, no repeat) |
| footsteps_walk_loop.wav | "footsteps knight walking for rpg" | 426521 | TODO verify | hero walk loop |
| dragon_roar.wav | "dragon shout roar" | 98277 | TODO verify | dragon spawn / attack |

## Still needed to complete #51 combat feel
- sword **swing/whoosh** (the swish BEFORE the clash) — search Freesound CC0 "sword whoosh"
- **cast charge** + **cast land** (magic skills) — "magic charge", "spell impact"
- **enemy death** grunt — "monster death", "orc death"
- optional: block/parry, hit-flesh, level-up, ward chime

## Notes
- Earlier ElevenLabs free-tier AI SFX were rejected (poor quality) and removed. Generator kept at
  `Tools/AudioGen/generate-sfx.ps1`; slicer at `Tools/AudioGen/rip-clips.ps1`.
- Key in `.secrets/elevenlabs.key` (gitignored) — rotate the one shown in chat.
- ffmpeg: installed via winget (Gyan.FFmpeg).
