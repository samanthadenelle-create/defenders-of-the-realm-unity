# Combat SFX — provenance & license

> ## ⚠ SUPERSEDED 2026-08-04 — READ THIS BANNER BEFORE THE TABLE BELOW
>
> **The provenance record in §Historical below was checked and is WRONG — not merely
> "unverified".** Every Freesound ID it logged resolves to an unrelated sound (evidence in
> `docs/SME/AUDIO_SME.md` §4b). The masters in this folder therefore have **UNKNOWN
> provenance**, and one of the three checked IDs is **CC-BY-NC**, which would be unusable
> in a commercial release.
>
> Because the runtime copies in `Assets/_Modules/Audio/Resources/Sfx/` were **byte-identical**
> to these masters (hash-verified), "the masters don't ship" was never a defence — the
> unknown-licence audio shipped.
>
> **Action taken 2026-08-04:** the mirror table in `Assets/Editor/Audio/SfxResourceMirror.cs`
> was re-pointed so 14 of the 16 runtime combat clips are sourced from the **licensed leohpaz
> pack** instead. The old record below is kept verbatim — a licence file's history matters —
> and is superseded, not deleted.

---

## 1. Current state (2026-08-04)

### 1a. Re-pointed to leohpaz — licence RESOLVED

Source pack: `Assets/Leohpaz/RPG_Essentials_Free/` — leohpaz, *RPG Essentials Sound Effects —
FREE!* (Unity Asset Store, publisher 61102), **purchased 2026-06-29**
(`docs/SME/ASSET_STORE_LEDGER_2026-07-12.md:10`). Licence: **Unity Asset Store Extension Asset
EULA** — commercial use permitted, redistribution-in-a-build permitted, **no attribution
required**.

The **loader keys did not move** — game code still calls `Resources.Load("Sfx/SwordClash")`
etc. Only the source of each mirrored file changed.

| Runtime clip (loader key) | Was (unknown provenance) | Now (leohpaz, licensed) |
|---|---|---|
| `SwordClash.wav` | `sword_clash_1.wav` | `10_Battle_SFX/39_Block_03.wav` |
| `SwordClash2.wav` | `sword_clash_2.wav` | `10_Battle_SFX/22_Slash_04.wav` |
| `SwordClash3.wav` | `sword_clash_3.wav` | `10_Battle_SFX/15_Impact_flesh_02.wav` |
| `SwordClash4.wav` | `sword_clash_4.wav` | `12_Player_Movement_SFX/45_Landing_01.wav` |
| `SwordSwing.wav` | `melee_swing.wav` | `12_Player_Movement_SFX/56_Attack_03.wav` |
| `HeroHit.wav` | `sword_clash_3.wav` (see §1c) | `12_Player_Movement_SFX/61_Hit_03.wav` |
| `WeaponDraw.wav` | `sword_draw.wav` | `10_UI_Menu_SFX/070_Equip_10.wav` |
| `SpellCast.wav` | `cast_spell.wav` | `8_Atk_Magic_SFX/18_Thunder_02.wav` |
| `EnemyCastCharge.wav` | `enemy_cast_chant.wav` | `8_Atk_Magic_SFX/45_Charge_05.wav` |
| `EnemyDeath.wav` | `enemy_death.wav` | `10_Battle_SFX/69_Enemy_death_01.wav` |
| `EnemyDeath2.wav` | `enemy_death_2.wav` | `10_Battle_SFX/69_Enemy_death_01.wav` (same clip — the free pack has only one death take) |
| `BuildingUpgrade.wav` | `building_construct.wav` | `8_Buffs_Heals_SFX/16_Atk_buff_04.wav` |
| `UiClick.wav` | `ui_select.wav` | `10_UI_Menu_SFX/013_Confirm_03.wav` |
| `TowerArrowHit.wav` | `projectile_whoosh_1.wav` | `10_Battle_SFX/77_flesh_02.wav` |

**⚠ NOT YET IN EFFECT ON DISK.** The `Assets/Leohpaz/` pack is **gitignored**
(`.gitignore:372`), and the re-point is only a table edit. The committed
`Resources/Sfx/*.wav` are still the old unknown-provenance bytes until
`Defenders > Audio > Mirror SFX to Resources` is run **on a machine that has the pack**
and the regenerated `Resources/Sfx/*.wav` are committed. **The blocker is not cleared
until that mirror run is committed.**

### 1b. NOT re-pointed — remaining licence blocker

Two rows had no honest leohpaz equivalent and deliberately still read from the
unknown-provenance masters. They were **not** forced onto a wrong-sounding clip.

| Runtime clip | Still sourced from | Why no substitute | What to source |
|---|---|---|---|
| `FootstepsWalk.wav` | `footsteps_walk_loop.wav` (5.83 s stereo) | `HeroLocomotion.cs:707` assigns this to a **looping** `AudioSource`. Every leohpaz step is a single 0.67 s one-shot; looped, that becomes a metronomic 1.5 Hz single step with no L/R variation — audibly wrong on the game's most-continuous sound. | Either a licensed multi-step **walk loop**, or a `HeroLocomotion` change to a timed one-shot stepper cycling `03_Step_grass_03` / `08_Step_rock_02` / `12_Step_wood_03` (code change — own WO). |

`DragonRoar` was originally the second unresolved row (the free pack has no creature
vocalisation of any kind). It was **replaced concurrently by another seat on 2026-08-04** while
this remediation was in progress — see §1e.

### 1e. ⚠ CONCURRENT CHANGES BY ANOTHER SEAT (2026-08-04, ~14:19–14:21) — needs reconciliation

Two audio changes landed in the shared working tree from a different session during this work.
Neither was made here, and neither is recorded anywhere. Flagging per the multi-session rule
(CLAUDE.md §11) — the sole committer must reconcile them by explicit path.

1. **`dragon_roar.wav` → `dragon_roar.mp3`.** The `.wav` master and `Resources/Sfx/DragonRoar.wav`
   are **staged for deletion**; `dragon_roar.mp3` (65 246 bytes, valid ID3v2.4) and
   `Resources/Sfx/DragonRoar.mp3` are new and untracked. The mirror table's unresolved row was
   updated to follow the `.mp3` so the mirror does not silently skip it
   (`Resources.Load("Sfx/DragonRoar")` is extension-agnostic, so the loader key is unaffected).
   **⛔ The replacement's source and licence are NOT recorded in any file.** An unlabelled
   replacement is the same blocker in new bytes — whoever made the swap must log its provenance
   here before ship. *(Note: the "dragon licence blocker closed" language in recent commits refers
   to the **model** — the WDallgraphics rig, WO-760 — not to this audio clip. Do not let the two
   be conflated into a false all-clear.)*

2. **`Assets/Resources/Audio/bellssteel-panic.mp3` was DELETED** (staged delete; gone from disk).
   This is one of the five clips the owner **cleared** in §2a on the same day, and it was
   explicitly designated the owner's call, not an agent's. Its deletion was not requested as part
   of this remediation and is not recorded anywhere. It is recoverable from `HEAD`. **Escalate to
   the owner before committing that deletion** — it may be intentional housekeeping (the file is
   an unreferenced orphan, and `docs/SME/AUDIO_SME.md` §5.8 does suggest wiring or deleting it),
   but a clip cleared hours earlier being silently dropped is exactly the kind of unlogged change
   this file exists to prevent.

### 1c. Found during the re-point, missed by the earlier audit

`Assets/_Modules/Audio/Resources/Sfx/HeroHit.wav` was **shipping without a mirror row**. It is
byte-identical (md5 `15dd4ea228e07df58b64a51a6064c627`) to `sword_clash_3.wav`, i.e. a 16th
unknown-provenance runtime clip, and it is live — loaded by `GameSfx.cs:192` and
`AudioService.cs:715`. It was presumably left behind by the 2026-07-02 hand mirror. A row for it
was added in §1a so the re-point actually covers it; without that row the mirror would have left
the unlicensed file in place forever.

### 1d. Masters retained, not deleted

The 17 WAVs in this folder are **kept on disk**. They are no longer the source of 14 of the 16
runtime clips, but they remain the source for the two §1b rows, and deleting evidence during a
licence remediation is the wrong move. Do not delete them without an owner ruling.

---

## 2. Owner attestations (2026-08-04)

These are **owner attestations** — the owner's direct recollection, recorded and dated. That is
legitimate provenance, but it is a **different class of evidence** from the hash-verified,
EULA-covered leohpaz rows above. Recorded here so a future readiness check does not re-raise
them as open questions.

### 2a. The five loose owner-drop clips — CLEARED

Owner, verbatim, 2026-08-04: the clips came **"from a free commercial free rights store"** — i.e.
a royalty-free source granting commercial rights.

**OWNER-ATTESTED: commercially licensed, royalty-free source.** Covers:

- `Assets/Resources/Sfx/Heal.mp3`
- `Assets/Resources/Sfx/Spell_Impact.mp3`
- `Assets/Resources/Sfx/Swords_Clash.mp3`
- `Assets/Resources/Sfx/LookoutHorn.wav`
- `Assets/Resources/Audio/bellssteel-panic.mp3`

*Nice-to-have, NOT a blocker:* if the specific storefront is ever recalled (Pixabay, Mixkit, a
CC0 pack, etc.), naming it here would strengthen the record for a store submission. The owner has
answered; these five are cleared.

### 2b. Music — CLEARED for commercial use

Owner, 2026-08-04: her Suno subscription tier is **Pro**.

**OWNER-ATTESTED: generated under a Suno Pro subscription, commercial rights granted.** This
closes the open question raised at `docs/SME/AUDIO_SME.md:236` (the concern was that Suno's FREE
tier historically granted no commercial rights — Pro does). All 16 owner-generated music tracks
are cleared for commercial use, not merely "clear of third parties".

---

## 3. Bottom line — the unverified bucket

After the 2026-08-04 rulings and this re-point, the **only** audio with unresolved provenance is:

1. The 14 combat clips in §1a — **resolved by the table edit, pending the mirror run + commit**.
2. `FootstepsWalk.wav` (§1b) — **genuinely open**; one licensed walk loop to source (or a small
   `HeroLocomotion` change).
3. `DragonRoar.mp3` (§1e) — **replaced by another seat with an unlabelled file**; its provenance
   must be recorded by whoever swapped it. Not resolved just because the old file is gone.

Everything else is hash-verified (leohpaz), EULA-covered (leohpaz + Hovl Studio), or
owner-attested (§2).

**Credits:** `HelpMenu.cs` (`OnShowCredits`) was corrected on 2026-08-04 — it previously claimed
"Audio: original soundtrack", which was affirmatively inaccurate now that a large share of
shipping SFX is third-party. It now names leohpaz and Hovl Studio. Note that leohpaz's Asset
Store EULA does **not** require attribution; naming them is good practice, not a licence
obligation. See also the open question of whether a 5-second toast is a durable enough credits
surface for a store listing (flagged, separate WO).

---

## Historical — the ORIGINAL record (superseded 2026-08-04, kept verbatim)

> The three Freesound IDs below were verified and **none of them matches the sound it is claimed
> to be** (`docs/SME/AUDIO_SME.md` §4b): 6341 is a Waldorf PPG synth brass note (CC-BY 4.0),
> 426521 is a metal statue falling in snow (CC0), 98277 is a 48 s synthesizer sequence
> (**CC-BY-NC 4.0**). Treat every claim in this section as unreliable.

Processed via ffmpeg (trim + loudnorm to -16 LUFS, 44.1kHz). Raw sources from Freesound.

### ⚠ License to verify per file (Freesound mixes CC0 and CC-BY)
Each Freesound sound is either **CC0** (no attribution) or **CC-BY** (must credit the author).
Look up each ID at `https://freesound.org/s/<ID>/` and record the license below. For CC-BY keep an
in-game credits line; prefer CC0 where possible.

| File | Source | Freesound ID | License | Use |
|------|--------|--------------|---------|-----|
| sword_clash_1..4.wav | "sword against sword" | 6341 | TODO verify → **PROVEN WRONG** | melee hit (4 variations, no repeat) |
| footsteps_walk_loop.wav | "footsteps knight walking for rpg" | 426521 | TODO verify → **PROVEN WRONG** | hero walk loop |
| dragon_roar.wav | "dragon shout roar" | 98277 | TODO verify → **PROVEN WRONG** | dragon spawn / attack |

*(The other 14 WAVs in this folder never had an ID logged at all.)*

### Still needed to complete #51 combat feel
- sword **swing/whoosh** (the swish BEFORE the clash) — search Freesound CC0 "sword whoosh"
- **cast charge** + **cast land** (magic skills) — "magic charge", "spell impact"
- **enemy death** grunt — "monster death", "orc death"
- optional: block/parry, hit-flesh, level-up, ward chime

### Notes
- Earlier ElevenLabs free-tier AI SFX were rejected (poor quality) and removed. Generator kept at
  `Tools/AudioGen/generate-sfx.ps1`; slicer at `Tools/AudioGen/rip-clips.ps1`.
- Key in `.secrets/elevenlabs.key` (gitignored) — rotate the one shown in chat.
- ffmpeg: installed via winget (Gyan.FFmpeg).
