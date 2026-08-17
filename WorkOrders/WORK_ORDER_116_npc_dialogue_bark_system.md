<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 116 — Living NPCs: Authorable Dialogue, Barks & Region Quest-Threads

**Status:** READY — PARTIAL - remainder named by the 2026-08-14 phantom sweep

> **PARTIAL - re-scoped 2026-08-14 (phantom sweep).** Most of this WO is present in HEAD; a named
> remainder is outstanding. No per-WO path:line was recorded here: see the 2026-08-14 phantom sweep for the
> implementation site and the remainder. Do not re-implement the shipped part.
> (Any prior dated reconciliation note on this file stands - see the preserved line below.)
> _Prior status line, preserved: Status: READY TO IMPLEMENT_

**Date:** 2026-05-29
**Priority:** High — turns the four regional questlines into something the player can actually meet and follow; populates Rung 3 exploration
**Scope:** Medium — one new `NpcData` ScriptableObject + a field-NPC runtime that **extends the existing village NPC stack** (AmbientNPC / TownsfolkDialogue / TownsfolkBubble / VillageNpcInjector), a lightweight quest-thread hook, and the nine canon NPCs as the first data set
**Depends on:** WO-86 (SO data architecture — DONE), WO-107 (climate zones + ZoneManager), WO-112 (the ward-tether — quest objectives tie to relight/claim beats), `docs/regions-narrative-and-npcs.md` (the nine NPCs + four questlines), `docs/narrative-bible.md` §7 (the in-game text library / bark snippets)
**Canon source:** `docs/regions-narrative-and-npcs.md` (NPCs §2–§6, ward-tether §0), `docs/narrative-bible.md` (tone §8, snippet library §7)

---

## Vision

The bible gives us a world full of voices we have only described. The regions doc names **nine NPCs** —
a road-warden, a field-elder, a wandering almsgiver, a frost-spirit, a quarryman, a silent ferryman, a
lantern-widow, a dying warden, and a Hollow One that does not attack — each tied to one of the four
marches and each carrying a couple of canon lines already written for them.

Right now the village has **ambient townsfolk** (the `AmbientNPC` / `TownsfolkDialogue` / `TownsfolkBubble`
stack — proximity word-bubbles, archetype line pools, runtime placement via `VillageNpcInjector`). That
stack works and the owner has already approved its look. What it **cannot** do yet is:

1. Speak as a *named, authored* character (it speaks as an archetype: "Trader", "Elder"…).
2. Live out in the **regions** (it only knows the Village hub).
3. Carry a **quest thread** (talk → do a thing → return).

This work order does the smallest honest thing: it **extends** that proven stack rather than building a
second one. We add an authorable `NpcData` ScriptableObject so a named character (Maeren, Brightwheat,
Old Bram…) can be authored once and dropped into any region; a thin `FieldNpc` runtime that reuses the
exact proximity/bubble behaviour `AmbientNPC` already proved; idle **barks** drawn from the bible's
snippet library; and a **one-thread-per-region** quest hook wired to the ward-tether (WO-112) so each
march's anchor questline (talk → relight the ward / claim the node → return) becomes real.

The result: push into a region, meet the soul who lives there, hear them in their own voice, do the one
thing they ask, and feel the march come a little more alive. Rung 3 stops feeling empty.

---

## 0. RECONCILE FIRST — what already exists (extend, do not duplicate)

This is the single most important section. **There is already an NPC + speech-bubble system.** Build on it.

| Existing piece | Path | What it does | Our move |
|---|---|---|---|
| `AmbientNPC` | `Assets/_Modules/Village/NPCs/AmbientNPC.cs` | Proximity + hysteresis word-bubble, NavMesh wander/idle, animator driver, body-tint safety net | **Reference pattern.** `FieldNpc` lifts its proximity/bubble loop. Do NOT rewrite it; do NOT delete it (village hub still uses it). |
| `TownsfolkDialogue` | `Assets/_Modules/Village/NPCs/TownsfolkDialogue.cs` | Static archetype → line-pool table, `LineFor(archetype, index)` round-robin | **Reuse the line-pick helper shape.** `NpcData` carries per-character lines instead of archetype pools — but the same "step the cursor, modulo the pool" idiom. |
| `TownsfolkBubble` | `Assets/_Modules/Village/NPCs/TownsfolkBubble.cs` | World-space code-built speech bubble (name + line), `Show(name, line)` / `Hide()` | **Reuse as-is.** Both `AmbientNPC` and the new `FieldNpc` drive the same bubble component. NOT UXML — keep it that way (PIPELINE_STATE §8). |
| `VillageNpcInjector` | `Assets/_Modules/Village/NPCs/VillageNpcInjector.cs` | Runtime placement that swaps placeholders → real prefabs WITHOUT touching `Village.unity` | **Model for region placement.** Field NPCs follow the same "place at runtime / ride the rebake" discipline (see §6). |
| `WandererDialogue` | `Assets/_Modules/Dungeons/Wanderer/WandererDialogue.cs` | The dungeon twin of TownsfolkDialogue | Leave alone — different module. Confirms the line-table idiom is the house style. |
| `DailyQuestService`, `QuestState`, `QuestProgress` | `Assets/_Modules/Core/Quests/DailyQuests.cs`, `Core/State/NestedTypes.cs` | Daily-quest rolling + per-quest progress, `Report(eventId, amount)` | **Reuse the `Report(eventId, amount)` event idiom** for our region-thread progress (§4). Do NOT fork a second quest manager. |

**Rule:** if you find yourself writing a second speech bubble, a second proximity loop, or a second quest
ticker — stop. Extend the one that exists.

---

## 1. Data model — DESIGN ONLY

The real code is CLI's. These blocks illustrate shape and intent only, following the WO-86 SO pattern
(`[CreateAssetMenu(menuName = "Defenders/Data/…")]`, namespace `DeNelle.Data`, pure data — no behaviour).

### 1a. `NpcData` — ScriptableObject (the authored character)

```csharp
using System;
using UnityEngine;

namespace DeNelle.Data
{
    [CreateAssetMenu(menuName = "Defenders/Data/NPC", fileName = "Npc_")]
    public class NpcData : ScriptableObject
    {
        [Header("Identity")]
        public string id;              // stable save/quest key, e.g. "npc_maeren"
        public string displayName;     // bubble attribution, e.g. "Maeren the Roadwarden"
        public Region region;          // reuse WO-107/112 region enum (Goldfields/Stoneback/Mirewood/Ashwood)

        [Header("Presentation")]
        public GameObject prefab;      // model prefab (People-pack or placeholder); null = primitive fallback
        public Sprite portrait;        // optional, for a future dialogue panel (not required v1)
        public Color tint = Color.white; // body tint fallback (mirrors AmbientNPC.EnsureBodyTinted)

        [Header("Idle barks (proximity / timed flavour)")]
        [TextArea] public string[] idleBarks;   // canon lines from regions-narrative-and-npcs.md (§7)

        [Header("Quest dialogue")]
        [TextArea] public string[] questIntroLines;   // shown when the thread is OFFERED
        [TextArea] public string[] questActiveLines;   // shown while the objective is in progress
        [TextArea] public string[] questDoneLines;     // shown on RETURN / completion

        [Header("Quest thread")]
        public NpcQuestThread questThread;   // null = pure ambient NPC, no thread (§4)
    }
}
```

### 1b. `NpcQuestThread` — the lightweight staged objective (v1)

One anchor thread per region. Deliberately tiny: a three-beat staged objective, no branching, no timers.

```csharp
using System;
using UnityEngine;

namespace DeNelle.Data
{
    public enum NpcQuestStage { Offered, Active, Complete }

    public enum NpcQuestObjective
    {
        RelightWard,   // light a specific ward-stone (WO-112) on this NPC's march
        ClaimNode,     // claim the march's resource node (WO-110/111, gated by node-ward)
        ReturnToNpc    // come back to the NPC after the deed (the "return" beat)
    }

    [Serializable]
    public class NpcQuestThread
    {
        public string threadId;            // e.g. "thread_goldfields_long_road"
        public string title;               // e.g. "The Long Road"
        [TextArea] public string summary;  // one-line journal blurb (bible-tone)

        // The single deed that completes the thread, tied to the ward-tether (WO-112).
        public NpcQuestObjective objective;
        public string targetWardId;        // matches a WardStoneData.id (WO-112) — for RelightWard
        public string targetNodeId;        // matches a CollectionPoint id (WO-111) — for ClaimNode

        [TextArea] public string completionFlavor;   // bible-tone line on completion
    }
}
```

### 1c. `FieldNpc` — runtime MonoBehaviour (the in-world character)

Lives in `DeNelle.Village` alongside `AmbientNPC` (regions are gameplay, Village→Core only). It **reuses
`TownsfolkBubble`** and mirrors `AmbientNPC`'s proximity loop — it is intentionally a close twin so the
behaviour is identical and already-proven. The difference: it reads its name + lines from an `NpcData`
asset, and it can pick which line *set* to speak based on the quest stage.

```csharp
using UnityEngine;
using DeNelle.Data;

namespace DeNelle.Village
{
    /// <summary>
    /// A named, authored NPC out in the regions (or the village). Mirrors AmbientNPC's
    /// proximity word-bubble loop, but speaks an NpcData's authored lines and can offer
    /// a single staged quest thread (WO-112 ward-tether hook). Reuses TownsfolkBubble.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FieldNpc : MonoBehaviour
    {
        [SerializeField] private NpcData _data;
        [SerializeField] private TownsfolkBubble _bubble;   // SAME bubble component as AmbientNPC
        [SerializeField] private float _speakRadius = 5.5f;
        [SerializeField] private float _speakHysteresis = 1.5f;
        [SerializeField] private float _barkIntervalSeconds = 75f; // §3 timed ambient barks

        private Transform _hero;
        private int _lineCursor;
        // ...proximity / hysteresis loop copied from AmbientNPC.UpdateProximity...

        public void Configure(NpcData data) { _data = data; /* tint, prefab already on go */ }
        public void SetHero(Transform hero) { _hero = hero; }
        public void SetBubble(TownsfolkBubble bubble) { _bubble = bubble; }

        // On approach: pick the line set for the current quest stage, else an idle bark.
        private string NextLine()
        {
            var thread = NpcQuestService.Instance?.StageFor(_data?.questThread?.threadId);
            string[] pool = thread switch
            {
                NpcQuestStage.Offered  => _data.questIntroLines,
                NpcQuestStage.Active   => _data.questActiveLines,
                NpcQuestStage.Complete => _data.questDoneLines,
                _                      => _data.idleBarks,
            };
            if (pool == null || pool.Length == 0) pool = _data.idleBarks;
            return pool[(_lineCursor++) % pool.Length];   // same modulo idiom as TownsfolkDialogue.LineFor
        }
    }
}
```

---

## 2. The nine NPCs — first `NpcData` author set

These are the v1 `NpcData` assets, straight from `docs/regions-narrative-and-npcs.md` §6 and the
per-region NPC sections. Author one `.asset` per NPC (created in the Editor via the CreateAssetMenu path —
`.asset` files cannot be made in batchmode, per WO-86 RESULT). The **starting barks** below are the canon
lines already written; CLI seeds `idleBarks[]` with these and the author can extend later.

| `id` | `displayName` | `region` | Starting `idleBarks[]` (canon) | Quest thread |
|---|---|---|---|---|
| `npc_maeren` | Maeren the Roadwarden | Goldfields (E) | *"Roads don't close because the dark says so. They close when the last cart stops coming. Mine hasn't yet. Light me a stone further out and it won't today either."* | **The Long Road** — relight Goldfields ward → return |
| `npc_brightwheat` | Brightwheat | Goldfields (E) | *"My grandfather sowed to the far ridge. My father to the elms. I sow to the fencepost now. I would like to sow further before I'm done."* | (claim the grain/gold node — ambient support to Maeren's thread) |
| `npc_sister_wren` | Sister Wren | Goldfields (E) | *"I have buried three valleys' worth of kind people. I keep walking east to west because the walking is the only prayer I have left."* | (ambient — news of the wider war) |
| `npc_elder_cold` | The Elder Cold (the Frostmother) | Stoneback (W) | *"…The little wolf went down the mountain. …It has not come back to tell me why. …Speak, small Keeper. The cold is patient, but I am older than patience."* | **The Cold-Wandered's Kin** — relight Stoneback wards → return |
| `npc_garrick` | Garrick the Last Hewer | Stoneback (W) | *"Aye, the old one came through. Said little. Touched the cold-iron vein, looked north a long while, and went on. I told them the path. I tell everyone the path. They go anyway."* | (claim the crystal/cold-iron seam node) |
| `npc_old_sedge` | Old Sedge (the Ferryman) | Mirewood (S) | *(non-verbal — see §3a. A bubble caption stands in for his silence: "(He holds out one hand, palm up, and waits.)")* | (gate/passage beat — non-verbal) |
| `npc_vessa` | Vessa, the Lantern-Widow | Mirewood (S) | *"I keep the lamp because someone must. … You keep a lamp too, Keeper. Yours is just bigger."* | **What the Water Keeps** — relight Mirewood wards / claim node → return |
| `npc_old_bram` | Old Bram | Ashwood (N) | *"I keep one stone. Just the one. When my words go — and they go, more each day — I'll still know to keep the one stone lit. Some things you don't forget. You just… stop being able to say them."* | **The Failing Wards** — relight Ashwood wards → return |
| `npc_one_who_remembers` | The One Who Remembers | Ashwood (N) | *(non-verbal — §3a. Caption only: "(It tilts its head, as if listening to a song it can no longer hear.)")* | (no thread — the emotional center; never speaks, never fights) |

**Tone discipline (bible §8):** short sentences, grounded vocabulary, no "thee/thou", hopeful core, the
Hollow Ones are grief not villains. The previous-Keeper / Alduin threads stay **implicit** (regions doc §8
default) — Wren's healers, Garrick's "the old one came through", Bram's fading, the One Who Remembers. Name
nothing the player can only infer.

---

## 3. Bark system — proximity + timed ambient lines

Barks are the cheap, high-value layer that makes a region feel inhabited. Two triggers, both reusing the
proven pattern:

- **Proximity bark** (already how `AmbientNPC` works): when the Keeper enters `speakRadius`, the bubble
  fades in with the next line for the current state (quest-stage pool, else `idleBarks[]`); it hides past
  the hysteresis margin so an edge-loitering Keeper doesn't flicker it.
- **Timed ambient bark** (new, lightweight): a stationary NPC the Keeper is *near but not engaging* may
  surface a faint line every `_barkIntervalSeconds` (~60–90s, matching the bible §7.8 cadence for ambient
  inner-thought subtitles). This is the same "fire one of these every 60–90s of inactive play" rhythm the
  bible already calls for — reuse that interval.

**Reuse the bible's snippet library directly.** Where an NPC has no extra authored barks, the village hub
NPCs can keep drawing from `TownsfolkDialogue`, and ambient Keeper-voice subtitles (the §7.8 "the Pup is
dreaming of fire again" lines) remain owned by whatever already surfaces them — do not duplicate that
table here. Region NPC barks live on their `NpcData.idleBarks[]`, seeded from §2.

### 3a. Non-verbal NPCs (Old Sedge, the One Who Remembers)

Two NPCs **never speak** (bible canon — the Hollow Ones lost their voices first; Sedge is liminal). For
these, the bubble shows a **stage-direction caption in parentheses** instead of dialogue (same mechanism
the bible §7.9 uses for pet vocalization captions: *"(a soft musical hum)"*). The `idleBarks[]` for these
two are written as parenthetical captions, and the bubble renders them in the same component — no new UI.

### 3b. UI — code-built, never UXML

The speech bubble is **`TownsfolkBubble`** — already a code-built world-space bubble. Both `AmbientNPC` and
`FieldNpc` drive it. **Do NOT introduce UXML** (PIPELINE_STATE §8: UXML does not work in builds). A future
full-screen dialogue panel (portrait + line, for the longer quest beats) should also be code-built and is
**out of scope for v1** — the bubble carries v1.

---

## 4. Quest-thread hook — one anchor thread per region (lightweight v1)

Each region's anchor questline (regions doc §2–§5) becomes **one staged thread**: **talk → do the deed
(relight the ward / claim the node, via WO-112) → return to the NPC.** No branching, no fail state, no
timer — this is a v1 punctuation beat, not a quest engine.

### 4a. `NpcQuestService` — the thin thread tracker (singleton)

Lives in `DeNelle.Core.Quests` next to `DailyQuestService` (so it persists the same way and HUD can read
it). It does **not** fork the daily-quest manager — it tracks the four region anchor threads only.

```csharp
namespace DeNelle.Core.Quests
{
    public class NpcQuestService : MonoBehaviour
    {
        public static NpcQuestService Instance { get; private set; }

        // Current stage of a thread by id (Offered when first talked to, Active after accept, Complete on return).
        public NpcQuestStage? StageFor(string threadId) { /* read from QuestState/save */ }

        // Called when the player talks to the NPC who owns the thread.
        public void OfferOrAdvance(string threadId) { /* Offered -> Active; or Complete -> nothing */ }

        // The deed hook: WO-112 calls this when a ward relights / node is claimed.
        // We match the ward/node id against any Active thread's targetWardId / targetNodeId.
        public void Report(string eventId, string targetId) { /* idiom mirrors DailyQuestService.Report */ }
    }
}
```

### 4b. Wiring to the ward-tether (WO-112)

The deed beat **reuses WO-112's events** — it does not re-implement relighting. WO-112's
`WardTetherService.OnWardStateChanged` (ward lit) and the node-claim path already fire; this WO only adds
**one line** at those points to notify the thread tracker:

```csharp
// inside WO-112's WardStone.SetLit / node-claim success (CLI adds this line):
NpcQuestService.Instance?.Report("ward_lit", ward.data.id);   // ?. — cross-module, may be absent
```

- On `RelightWard` threads: the matching ward's `id` ticking through marks the thread's deed done →
  stage moves toward **Complete-on-return**.
- On `ClaimNode` threads: the node id (WO-111 CollectionPoint) ticking through does the same.
- **Return beat:** the thread becomes `Complete` only when the player walks back to the NPC after the
  deed (the `FieldNpc` calls `OfferOrAdvance` on the next approach, sees the deed is done, plays
  `questDoneLines`, and the thread closes with `completionFlavor`).
- If WO-112 is not yet in the build, `NpcQuestService.Instance?.Report(...)` is a null-safe no-op — the
  NPCs still bark and offer; the thread simply never completes. **No hard dependency at runtime.**

### 4c. Persistence

Thread stages persist like the rest of quest progress. Reuse the existing `QuestState` / `QuestProgress`
save structures (`Assets/_Modules/Core/State/NestedTypes.cs`) — add the four thread ids as tracked
progress keys, OR a small `Dictionary<string,NpcQuestStage>` on the save model. **Do not invent a new save
file** — ride `GameState` / the existing save path, same as DailyQuests.

---

## 5. Region quest-thread summary (the four anchors)

| Thread id | Region | Owner NPC | Deed (WO-112 hook) | Bible-tone close |
|---|---|---|---|---|
| `thread_goldfields_long_road` | Goldfields (E) | Maeren | RelightWard `ward_goldfields_2` (the node-ward) | *the road stays open one more day* |
| `thread_stoneback_cold_kin` | Stoneback (W) | The Elder Cold | RelightWard `ward_stoneback_3` (the seam) | *the mountain lends its winter, or hardens* |
| `thread_mirewood_water_keeps` | Mirewood (S) | Vessa | ClaimNode `node_mirewood` (gated by the final ward) | *the drowned hall's record is given* |
| `thread_ashwood_failing_wards` | Ashwood (N) | Old Bram | RelightWard `ward_ashwood_3` (the last warden's stand) | *one more stone held against the dark* |

(Ward/node ids must match the assets WO-112 / WO-111 author — confirm with those WOs before seeding
`targetWardId` / `targetNodeId`. If an id differs, the thread silently won't complete — so align them.)

---

## 6. World placement — rides the architect rebake (do NOT fork a placement path)

Region NPCs are scene/world objects, so their placement is a **`VillageSceneBuilder` / region-build
concern** — and that file is the serialization bottleneck (CLAUDE.md §9). Mirror exactly how
`VillageNpcInjector` already solves this for village townsfolk:

- **Preferred (matches existing pattern):** a small runtime injector (`FieldNpcInjector`, twin of
  `VillageNpcInjector`) reads the `NpcData` set, instantiates each NPC's prefab at its authored region
  position at runtime, wires `FieldNpc.Configure(data)` + `SetBubble` + `SetHero`, and snaps to NavMesh —
  **without touching any scene file.** This keeps placement off the bottleneck entirely.
- **If baked instead:** add a `BuildRegionNpcs(...)` step to the region builder, called **after**
  WO-107's `BuildClimateZones()` (so NPCs sit inside their zones) — and it **must ride the next
  architect-lane rebake**, single-touch, queued as a CLI bake line. **UI does not fire batchmode.**
- **Never hand-edit `Village.unity`** (or any region scene) — NPCs appear only via the injector or the
  builder rebake.
- Do **not** author a conflicting second placement path. Pick the injector route (recommended) OR the
  builder route — not both.

NPC region anchor positions should align with each march's ward-stone line (WO-112 §5) — the owner NPC
stands near their march's relight beat (e.g. Maeren by the Goldfields road ward, Old Bram at the last
Ashwood ward). Final positions are the architect's call on the rebake.

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/Data/NpcData.cs` | **Create** — `NpcData` ScriptableObject + `NpcQuestThread` / enums (`DeNelle.Data`, WO-86 pattern) |
| `Assets/_Modules/Village/NPCs/FieldNpc.cs` | **Create** — runtime named NPC; mirrors `AmbientNPC`'s proximity loop, reuses `TownsfolkBubble` |
| `Assets/_Modules/Village/NPCs/FieldNpcInjector.cs` | **Create** — runtime placement (twin of `VillageNpcInjector`); NO scene edit |
| `Assets/_Modules/Core/Quests/NpcQuestService.cs` | **Create** — thin four-thread tracker next to `DailyQuestService` |
| `Assets/_Modules/Core/State/NestedTypes.cs` | **Edit (if needed)** — add thread-stage persistence (reuse `QuestState`/`QuestProgress`, no new save file) |
| `Assets/_Modules/Environment/WardStone.cs` (WO-112) | **Edit (one line, when WO-112 lands)** — `NpcQuestService.Instance?.Report("ward_lit", data.id);` on relight |
| `Assets/Data/NPCs/*.asset` | **Create (in Editor)** — the nine `NpcData` assets (§2); `.asset` cannot be batchmode-made (WO-86 RESULT) |
| `Assets/_Modules/Village/NPCs/AmbientNPC.cs` | **Do NOT rewrite** — village hub still uses it; only referenced as the pattern |
| `Assets/_Modules/Village/NPCs/TownsfolkBubble.cs` | **Reuse as-is** — both NPC types share it |
| Region / `Village.unity` scene files | Rebuilt via injector or builder rebake — **do NOT hand-edit** |

**Assembly discipline:** `NpcData` + quest data in `DeNelle.Data` / `DeNelle.Core.Quests` (pure data, Core
side). `FieldNpc` + injector in `DeNelle.Village` (gameplay). **Village → Core only.** All cross-module
calls (HUD readout of a thread, audio on bark) go through `CoreServices` with null-conditional `?.` —
never a direct `DeNelle.HUD` reference. `NpcQuestService.Instance?.Report(...)` from WO-112 is `?.`-guarded.

---

## Acceptance Criteria

- [ ] `NpcData` ScriptableObject authorable via `Defenders/Data/NPC` menu (WO-86 pattern); carries id, name, region, prefab/tint, `idleBarks[]`, quest line sets, optional `NpcQuestThread`
- [ ] All nine canon NPCs (§2) authored as `NpcData` assets with their canon starting barks seeded
- [ ] `FieldNpc` reuses `TownsfolkBubble` and the `AmbientNPC` proximity/hysteresis loop — no second bubble, no second proximity system
- [ ] Approaching a named NPC shows their authored line (quest-stage pool if a thread is active, else `idleBarks[]`), stepping deterministically through the pool like `TownsfolkDialogue.LineFor`
- [ ] Non-verbal NPCs (Old Sedge, the One Who Remembers) show parenthetical stage-direction captions, not dialogue — same bubble component
- [ ] Timed ambient barks fire on the bible §7.8 cadence (~60–90s) when the Keeper lingers near a stationary NPC
- [ ] Each of the four regions has exactly one anchor quest thread (talk → relight ward / claim node → return); no branching, no fail state, no timer in v1
- [ ] `NpcQuestService` tracks the four threads, advances stage on the deed via a `Report(eventId, targetId)` call from WO-112 (one line, `?.`-guarded) — does NOT fork the daily-quest manager
- [ ] Thread completes only on RETURN to the owner NPC after the deed; closes with `completionFlavor`
- [ ] If WO-112 / WO-111 are absent, NPCs still bark and offer threads; threads simply never complete (no runtime crash, no hard dependency)
- [ ] Thread stages persist via the existing `QuestState`/`GameState` save path — no new save file
- [ ] NPC placement rides the runtime injector (preferred) OR the architect rebake — never a hand-edited scene, never a second placement path
- [ ] No UXML introduced — speech bubble stays code-built (PIPELINE_STATE §8)
- [ ] Tone matches bible §8 (short, grounded, no fake-archaic, Hollow Ones as grief); previous-Keeper / Alduin threads stay implicit
- [ ] Brace balance check passes on every `.cs` touched; cross-module calls use `?.`; files implementing any Core interface carry the matching `using`

---

## Do NOT touch

- **Do NOT build a second NPC / speech-bubble / proximity system** — extend `AmbientNPC` / `TownsfolkBubble` / `TownsfolkDialogue`. Reuse, don't duplicate.
- **Do NOT fork a second quest manager** — `NpcQuestService` tracks only the four region threads and reuses the `Report(eventId, …)` idiom from `DailyQuestService`.
- **Do NOT re-implement ward relighting** — the deed beat reuses WO-112's existing ward/node events with a single `?.`-guarded notify line.
- **Do NOT introduce UXML** — code-built UI only (PIPELINE_STATE §8). The full-screen dialogue panel is out of scope for v1.
- **Do NOT hand-edit `Village.unity` or any region scene** — placement via injector or the architect rebake only; UI never fires batchmode.
- **Do NOT author a conflicting placement path** — pick the runtime injector (recommended) OR the builder bake step, not both.
- **Do NOT reference `DeNelle.HUD` from `DeNelle.Village`/`DeNelle.Core`** — any HUD thread readout goes through `CoreServices` / `IVillageHud`.
- **Do NOT name the previous Keeper or make the Alduin thread explicit** — implicit only (regions doc §8, bible §10 defaults).
- **Do NOT invent a new save file** — ride `GameState` / `QuestState`, same as DailyQuests.
- Do not touch ATB, WalletService, monetization, or clan code.
