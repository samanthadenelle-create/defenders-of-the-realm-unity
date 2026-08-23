# WORK ORDER 1104 — The Arcane Spire plans MOMENT: wave 3, celebration, and the Echo's call to arms

**Status:** FIXED — AWAITING OWNER FELT-TEST TO CLOSE. Prior status: DONE 2026-08-16 (`449dd9df1` + `ec247d4f1`) — RESULT filed; pending PO felt-verify (the PoiBeacon discoverability call is hers)
**Minted:** 2026-08-16 (CLI seat) — banner bumped 1104 -> 1105 in the same edit
**Lane:** Progression / FTUE / celebration UI. Interacts with WO-1031 (guide despawn) — see §5.
**Provenance:** owner, live playtest 2026-08-16, two statements:
1. *"when do i get the arcane spire plans?"* — asked while the drop was silently failing (below).
2. *"it should be given after wave 3. a celebratory screen and new plans. thena FTUE with Echo
   explaining that we should hurry and add this new type of defense"* — the ruling this WO builds.

---

## 1. WHY SHE NEVER GOT THEM — the captured proof (SS1, FIXED)

F8 **seq 2434-2442** — nine captures in 31 seconds, `Main_Castle_Overworld`, t=2510s (~42 min into
her session), retrying on every 3 s scan:

```
[Flow:Progression] spawn plans drop FAILED: UnityException: Tag: SpawnPoint is not defined.
  at UnityEngine.GameObject.FindGameObjectsWithTag (System.String tag)
  at DeNelle.Village.CastleDefensePlansService.ResolveGateSeat  (CastleDefensePlansService.cs:183)
  at DeNelle.Village.CastleDefensePlansService.SpawnDrop        (:132)
  at ... Guard.Try (Guard.cs:34)
```

**Read the failure precisely, because it is good news:** every gate BEFORE the seat resolution had
already PASSED — `!unlocked`, `!propAlive`, `WavesCompleted >= RequiredWavesSurvived`, and a live
village `WaveManager`. She had EARNED the plans. The drop then died on where to *put* them:

- `ResolveGateSeat` prefers a `Gate` object; **the merged hub has none**, so the `else` branch is the
  live path, not the rare one.
- That branch called `GameObject.FindGameObjectsWithTag("SpawnPoint")`, and **`SpawnPoint` is not
  declared in `ProjectSettings/TagManager.asset`** (declared tags: Tower, Building, HeartTarget,
  Player). `FindGameObjectsWithTag` THROWS on an undeclared tag.
- The throw escaped past the `fallback:heart-approach` seat at `:200`, so **the fallback that exists
  precisely for this case was unreachable.** `Guard.Try` caught it, logged, and the scan retried
  forever. This is the WO-1038 class (undeclared tags = live `UnityException`s).

**FIX LANDED (uncommitted, awaiting gate):** resolve by COMPONENT, not tag —
`FindObjectsByType<WaveSpawnPoint>(FindObjectsSortMode.None)`, exactly how the gate branch above it
already works. `WaveSpawnPoint` is what `CastleHubBuilder.PlaceCastleSpawnPoints` actually seats
(canon §7, 12 m outside each gate), and a component lookup **cannot throw on missing project
settings**. ⚠ `CastleHubBuilder.cs:2415` already noted that `EnemyBrain` guards undefined tags —
this site was the one that did not. **Canon pattern: derive from the thing itself, never from a
hand-authored label that a settings file has to agree with.**

## 2. SS2 — the wave gate (LANDED, uncommitted)

`RequiredWavesSurvived` **2 -> 3** per the ruling. Constant + doc comment updated; `ShouldSpawnDrop`
is unchanged (one reader, one rule). Note WO-1013 §1 authored the original 2 — this WO supersedes
that number only, not its "gate on WAVES, not tutorial completion" reasoning (skip-tutorial players
stay covered for free).

## 3. SS3 — the celebration screen (NEW BUILD)

The moment must READ as a reward, not as a prop quietly appearing in the courtyard.

- Fires ONCE, on the wave-3 clear that satisfies the gate — not on scene re-entry (the prop
  re-spawns deterministically from state on every entry; the CELEBRATION must not).
- Content: the win beat ("you held three waves"), the plans as an earned unlock, and the new
  structure it opens (`tower_arcane_spire`).
- ⚠ Build it through `ElarionUiKit` / the Obsidian frame — the `[ui-obsidian]` ratchet HARD-FAILS on
  new hand-rolled UI (`CaravanStatusChip` tripped it in this same session).
- ⚠ Register with `PanelManager` (Register + NotifyOpened + NotifyClosed) or it is invisible to the
  back-button / battle-lock arbiter — the exact defect `[modal-registration]` caught on
  `DungeonExitInteractable` this session.
- ASCII-only TMP strings; never meaning by colour alone.

## 4. SS4 — the Echo's call to arms (NEW BUILD)

An FTUE beat where **the Echo** tells the player to hurry and raise this new defense.

- **The Echo is ALDWIN** — Echo #1, the founding Ice Echo (`EchoRosterCatalog`), per the owner's
  2026-08-16 naming ruling recorded in WO-1031. ⛔ Do NOT introduce "Frost"; that name was invented
  inside `SpeakerName()` and WO-1031 is deleting it. Names read from `EchoRosterCatalog`, the authority.
- Copy must carry URGENCY (her word: *"we should hurry"*) — this is the pivot from surviving waves to
  fortifying, and it is the emotional payload of the whole beat.
- Route through the dialogue spine (`DialogueCommandSink` verbs), not a bespoke panel.
- One guide at a time: the `tutorialv2` one-guide lock (`112d1c0dc`) is canon — this beat must not
  put a second narrator on screen.

## 4a. ⭐⭐ THE GOVERNING RULING — the guide BODY is a one-time device (2026-08-16)

Owner, verbatim: *"the wolf was a one time idea to make it tangible, then rest can just be the
tutorial image"*.

**This is bigger than this ticket and binds every future teaching moment.** The physical guide body
(the wolf = Aldwin, Echo #1) exists ONCE, for the opening FTUE, to make the player's first minutes
tangible. It then leaves — WO-1031's despawn is the intended end of its life, not a workaround —
and it never returns. **Every later tutorial / guidance beat is a DIALOGUE SCREEN with the tutorial
image: no world actor, no re-summoned body, no follow-the-NPC step.**

Consequences, all of which simplify work already on the board:
- WO-1031's despawn is unconditional and permanent by design. Nothing later needs the body back.
- WO-1104 SS4 (below) is a screen, not an actor.
- The `founding_walk` STEP-STUCK class (WO-1036) is a cost only the FIRST beat pays; no future beat
  can inherit it, because no future beat asks the player to follow anything.
- Recorded in auto-memory as `tutorial-guide-body-one-time-then-images` so it survives this session.

## 4b. ⭐ OWNER RULING 2026-08-16 — THE BEAT IS A DIALOGUE SCREEN, NOT A WORLD ACTOR

Verbatim: *"i dont need to see it, can be a dialogue scre[e]n right like the introduction screen"*.

**This is §5's option (a), and it closes the collision — SS4 needs NO body, so nothing here depends
on WO-1031's despawn and the two tickets are now independent.** Implement as a full-screen dialogue
beat in the cold-open idiom:

- **Model on `StoryIntroController`** (`Assets/_Modules/Onboarding/StoryIntroController.cs`) — the
  cold-open cinematic she is naming. It is already the right shape: its OWN
  `ElarionUiKit.BuildModalCanvas` ScreenSpaceOverlay with CanvasGroup fades, kit-typography TMP
  line label, beats delivered ONE AT A TIME over a dark backdrop, tap-to-advance with a ~1.25 s
  grace window, and an Obsidian Skip button (GameObject named `CloseButton`, the one shared Close
  convention). Reuse that construction; do NOT hand-roll a second overlay (the `[ui-obsidian]`
  ratchet hard-fails new non-kit UI).
- ⚠ **Do NOT reuse `StoryIntroController` itself** — its `Play()` is gated on `GameState.Onboarded`
  being false (the first-launch cold open) and it lives in the TITLE scene. This beat fires in the
  hub, post-wave-3, on an Onboarded save. Same PATTERN, separate controller.
- Speaker is **Aldwin** (§4). With no body required, the portrait/name comes from
  `EchoRosterCatalog` — the authority — and the Echo can speak from anywhere.
- Skippable on the same terms as the cold open (explicit Skip never gated); the unlock and the
  grant must already be committed before it plays, so skipping can never cost the player the spire.

**Consequence for §3:** the celebration and this beat can now be ONE screen — the cold-open idiom
already sequences beats, so "you held three waves / here are the plans / hurry and raise it" is
three beats of one dialogue screen rather than a modal plus a separate FTUE step. Cheapest build,
and it reads as a single authored moment instead of two popups.

## 5. ⚠ THE INTERACTION THAT WILL BREAK THIS IF NOBODY CHECKS IT — ✅ RESOLVED by §4b

**WO-1031 §4 despawns the guide body when the tutorial ends OR a defensive structure is placed.**
This WO then asks that same Echo to deliver a NEW beat **after wave 3** — which is comfortably after
both despawn triggers. **If the body is gone, this beat has no speaker.**

Whichever ticket lands second MUST verify the other. Two shapes, owner's call:
- (a) the beat is a HUD/dialogue voice that needs no body (cheapest, no resurrection), or
- (b) the Echo re-appears for the beat — in which case the despawn must be re-entrant and WO-1031's
  "removes the BODY, never the roster entry" criterion becomes load-bearing here too.

## 6. What NOT to touch

- `EchoAssignments` / the Echo roster — the celebration and the FTUE must not touch the player's
  earned Echoes (WO-1031: body != Echo; `Echoes 1/6` must not move).
- `ShouldSpawnDrop`'s shape — one reader, one rule; only the constant moved.
- The prop's re-spawn-from-state behaviour (§3 first bullet).

## 7. Acceptance

- A fresh save reaches wave 3, and the plans prop appears with `[Flow:Progression] plans-drop-spawned`
  naming a real seat source (`spawnpoint:WaveSpawnPoint-<dir>` or `gate:<id>`), with **zero**
  `spawn plans drop FAILED` lines in the run.
- Re-entering the scene re-spawns the prop but does NOT replay the celebration.
- The celebration renders through the kit at the device resolution (open the PNG) and registers with
  PanelManager.
- The Echo beat names Aldwin, appears once, and does not put a second guide on screen.
- A regression pins the seat resolution against an EMPTY TagManager (the bug could only ship because
  nothing asserted the un-throwing path).
