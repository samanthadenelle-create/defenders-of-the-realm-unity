# WORK ORDER 967 — The dungeon action bar defaults to the KNIGHT kit (hardcoded literal)  — **OWNER CLOSED 2026-08-22** (felt-verified by the owner; PO closes, section 13).

**Status:** DONE — shipped `70eaf1c6` ("fix(hud): WO-967"); owner felt-verify owed (PO closes, §13). RESULT file still owed (not fabricated). *(Status corrected 2026-08-14: the line still read READY after the commit landed.)*

> ### VERIFIED AT SOURCE 2026-08-22 (status audit) - and the symptom is now unreachable
> **Scene: `Dungeon_HealersCottage` - LEGACY / SUPERSEDED PIPELINE.** This ticket targeted the **hand-built**
> dungeon. The game now loads **COMPOSED** dungeons, which **carry the town hero across** rather than spawning a
> baked one - `Assets/_Modules/Dungeons/ComposedDungeonHost.cs:13-23` ("SceneRouter.GoDungeonScene now CARRIES
> the town hero into a composed dungeon, because the baked Keeper has no HeroAbilities") and `:92-99`.
> `ComposedDungeonHost.cs` and `ComposedDungeonBootstrap.cs` reference **no** `DungeonHero` / `DungeonCameraRig`
> (grep, 2026-08-22), so the hardcoded-Knight literal this WO removed can no longer be reached on the live path.
> Owner felt-verify still owed (PO closes, CLAUDE.md 13).

**Minted:** 2026-08-10 (F8 seq 2312 lane)
**Silo:** HUD presentation (`DeNelle.Village/HUD`) — file-disjoint from the locomotion + tutorial lanes live on this same scene
**Stage:** QA RCA complete → CLI implements → PO felt-verifies + closes

---

## 1. Owner report (VERBATIM)

> **"in dungeon i have the knights action bar loading"**

> **"as Thrain"**

She is playing a **MAGE**. Scene `Dungeon_HealersCottage`, F8 capture seq 2312
(`logs/f8-inbox/capture-20260810-183326.md`).

---

## 2. The proving line (PROVEN-BY-CAPTURE)

`logs/f8-inbox/capture-20260810-183326.md:26` (and repeated at :32, :35, :40, :43, :49, :55, :75, :82):

```
[Flow:HeroLoco] vel=0.00 m/s | clips=[mixamo.com(w=1.00,len=3.63s)] | avatar=MageAvatar | controller=Mage
```

The BODY and the ANIMATOR are correctly Mage in the dungeon. The ACTION BAR is not.

**Correction to the triage premise — "Thrain" is NOT a Knight clue.** `Thrain` is the
canon **MAGE** name: `Assets/Resources/Data/Canonical/en.json:145` → `"hero.mage.name": "Thrain"`
(READ-AT-SOURCE). The Knight is Grom. So the identity/nameplate layer is **CORRECT** and only
the **ability layer** is wrong. That narrows the split rather than widening it, and it is itself
the tell — see section 4, where the two layers are shown to read different sources.

---

## 3. RCA — the split, named precisely

### 3.1 Single source of truth for the hero's class (question 1)

`GameState.HeroClass`, typed **`HeroClassOpt`** — READ-AT-SOURCE:

- `Assets/_Modules/Core/State/HeroClassOpt.cs:20-28` — `None = 0, Mage = 1, Knight = 2, Ranger = 3, Cleric = 4`.
  Deliberately an OPTION type so "unchosen" is distinguishable from a real class.
- `Assets/_Modules/Core/State/Enums.cs:47-53` — the real `HeroClass` enum. **Its zero value is `Mage`, not Knight.**
- The runtime carrier is `HeroAbilities._heroClass` (`Assets/_Modules/Village/Hero/HeroAbilities.cs:48`),
  seeded from `GameState` in `Awake` (`HeroAbilities.cs:297-315`) and re-asserted by
  `HeroBodySwapper` via `SetHeroClass` (`HeroAbilities.cs:277-282`).
- The canonical class → job-key → name resolver is `PlayableHeroes.JobKey` →
  `HeroCanonNames.ForJob` (`Assets/_Modules/Core/State/HeroCanonNames.cs:44-58`).

### 3.2 Who populates the right ActionBar, and what it reads (question 2)

`AbilityLoadoutProducer` — `Assets/_Modules/Village/HUD/HudModelProducers.cs:381-479` (READ-AT-SOURCE).

```
390:  if (_abilities == null || !_abilities) _abilities = Object.FindAnyObjectByType<HeroAbilities>();
392:  string cls = _abilities != null && !string.IsNullOrEmpty(_abilities.HeroClass) ? _abilities.HeroClass : "knight";
...
469:  if (slot == AbilitySlot.Q) return AbilityCatalog.Find(cls, slot);
477:  return eq ?? AbilityCatalog.Find(cls, slot);
```

`AbilityCatalog.Find("knight", ...)` returns exactly the kit she is seeing —
`Assets/Resources/Data/Canonical/abilities.json:19-22`: **Sword Heroic (Q) / Shield Charge (W) /
Warden's Grace (E) / Radiant Strike (R)**.

### 3.3 Why `_abilities` is null in a dungeon (question 4 — YES, DUNGEON-SPECIFIC)

The composed dungeon hero **carries no `HeroAbilities` component at all**, by construction, and
this is documented in two places in the tree (READ-AT-SOURCE):

- `Assets/_Modules/Village/Hero/GearLoadout.cs:1257-1260` — *"A composed dungeon hero carries NO
  HeroAbilities: `DungeonBaker.PopulateForPlay` attaches only HeroLocomotion + HeroBodySwapper, and
  HeroControlEnsurer's emergency wiring is gated by IsVillageScene, which every dg_* scene fails."*
- `Assets/_Modules/Dungeons/DungeonController.cs:471-474` — *"a composed dungeon Keeper deliberately
  carries NO HeroAbilities."*

Confirmed at the bake site: `Assets/Editor/RoomForge/DungeonBaker.cs:1168-1187` attaches
`HeroLocomotion` and `HeroBodySwapper` and nothing else. Confirmed at the ensure site:
`HeroControlEnsurer.EnsureHeroCombatComponents` (`Assets/_Modules/Village/Hero/HeroControlEnsurer.cs:409-493`)
adds HeroDeathLogger, HeroTargetIndicator, PlayerAttackController, WeaponTrailController,
GearLoadout, HeroArmorVisual, HeroLoadout, HeroHealth, HeroHitReaction — **never HeroAbilities**.
And repo-wide, `AddComponent<HeroAbilities>()` appears in exactly **one** file, an EditMode test
(`Assets/Tests/EditMode/HeroAbilityEffectTests.cs:79`). There is no runtime path that can give a
dungeon hero a HeroAbilities.

So in the dungeon: `FindAnyObjectByType<HeroAbilities>()` → **null** → `cls = "knight"` → Knight kit.
**The town path is correct** because `VillageSceneBuilder.BuildHero` puts `HeroAbilities` on the
hero root (cited in `HeroControlEnsurer.cs:478-480`), so line 392 takes its true branch.

### 3.4 Why the NAME stayed right while the BAR went wrong — the exact seam

The two HUD layers read **different** sources, and only one of them has a memory.

`HudModelHost` is `DontDestroyOnLoad` (`Assets/_Modules/Village/HUD/HudModelHost.cs:55`), and it owns
both producers (`HudModelHost.cs:70` HeroVitals, `:76` AbilityLoadout). They therefore survive the
town → dungeon load **with their cached fields intact**.

- **Nameplate path — sticky, so it stayed correct.** `HeroVitalsProducer`
  (`HudModelProducers.cs:87`): `string cls = _abilities != null && ... ? _abilities.HeroClass : (_classId ?? "knight");`
  `_classId` still holds `"mage"` cached from town, so the fallback never reaches the `"knight"`
  literal, and `HudKitController.cs:1468` renders `HeroCanonNames.ForJob("mage")` = **"Thrain"**. Correct.
- **Ability path — no sticky, so it fell to the literal.** `AbilityLoadoutProducer`
  (`HudModelProducers.cs:392`) has **no `_classId` memory** — it goes straight to `"knight"`.

**THAT is the split**: one producer remembers the class across the scene load and the other
hardcodes Knight. Same file, twelve lines apart, three hundred lines of distance.

### 3.4a Owner scope "ONLY IN DUNGEON" — confirmed, and it is the same line

The owner reports the wrong bar appears **only** after entering the dungeon; the town is correct in
the same session. That matches the mechanism exactly and needs no extra hypothesis:

- **Town:** `VillageSceneBuilder.BuildHero` puts `HeroAbilities` on the hero root
  (cited `HeroControlEnsurer.cs:478-480`), so `HudModelProducers.cs:392` takes its **true** branch
  and reads the real class. Correct bar.
- **Dungeon load boundary:** the composed hero is baked with `HeroLocomotion` + `HeroBodySwapper`
  only (`DungeonBaker.cs:1168-1187`) and `HeroControlEnsurer.EnsureHeroCombatComponents`
  (`HeroControlEnsurer.cs:409-493`) provisions nine components but **not** `HeroAbilities`.
  `HeroControlEnsurer.Ensure`'s own scene gate `IsVillageScene` fails every `dg_*` scene
  (`DungeonController.cs:728-729`). So `FindAnyObjectByType<HeroAbilities>()` returns null and
  `:392` takes its **false** branch.

**The exact line where the class stops being read from the persisted source and starts being
defaulted is `Assets/_Modules/Village/HUD/HudModelProducers.cs:392`.** Nothing "loses" or overwrites
the class — `GameState.HeroClass` is still Mage throughout, which is precisely why the nameplate and
the gear layer stay right. The HUD simply stops asking the state and starts asserting `"knight"`.

**On "as Thrain":** Thrain is the correct Mage name and it is correct in BOTH scenes (§2, §3.4). If
the owner is instead reporting that the town shows a *different* name than the dungeon, that would
be a separate defect and this WO does not cover it — but nothing in the source supports it: the town
resolves the name from a live `HeroAbilities` class and the dungeon from the sticky `_classId`, and
both hold `"mage"`. The instrumentation in §5.2(c) makes that answerable from the next capture
instead of by argument.

### 3.5 Is there a DEFAULT-TO-KNIGHT in the chain? (question 5) — YES, three literals

**PROVEN AT SOURCE. No capture is needed to settle this.**

| # | File:line | Literal |
|---|---|---|
| 1 | `Assets/_Modules/Village/HUD/HudModelProducers.cs:392` | `: "knight"` — **the reported bug** |
| 2 | `Assets/_Modules/Village/HUD/HudModelProducers.cs:87` | `: (_classId ?? "knight")` — latent; bites on a cold boot straight into a dungeon |
| 3 | `Assets/_Modules/Village/HUD/HudModelProducers.cs:139` | `: "knight"` — `PartyProducer` slot 0, same latent bug |

It is **not** an enum-zero default: `HeroClass`'s zero value is `Mage` (`Enums.cs:49`) and
`HeroClassOpt`'s zero is `None` (`HeroClassOpt.cs:23`). It is not `AbilityCatalog.DefaultClass`
either — that constant is `"mage"` (`Assets/_Modules/Village/Hero/AbilityCatalog.cs:207`). All three
are **hand-written `"knight"` string literals in the HUD producer file**, and they are the only
default-to-Knight in the chain.

### 3.6 This is a REPEAT of an already-fixed bug, in a second reader

`GearLoadout.CurrentJob` (`GearLoadout.cs:1284-1306`) had **precisely this defect** and was fixed
under F8 seq-642: it now falls back to the persisted `GameState.HeroClass`
(`GearLoadout.PersistedPlayerJob`, `GearLoadout.cs:1328-1334`) before any catalog default, with a
`FlowTrace.Once`. Its header notes the old behavior "corrupted a save slot the player never played."
**The HUD producer was never given the same treatment.** The fix below is that same step-3 fallback,
applied to the second reader.

---

## 4. THE FIX — one line per candidate

**Primary (do this).** Add one shared resolver to `HudModelProducers.cs` and route all three call
sites through it. This is the HP B2B-correct fix: presentation stops inventing a class and asks the
state layer, without touching the hero objects.

```csharp
/// <summary>The hero's class for HUD display: the live HeroAbilities when one exists, else the
/// PERSISTED GameState.HeroClass (the same source HeroBodySwapper builds the BODY from — a composed
/// dungeon hero carries no HeroAbilities, see GearLoadout.CurrentJob), else the catalog default.
/// Never the hardcoded "knight" literal that made a Mage read as Grom's kit in every dungeon.</summary>
private static string HudHeroClass(HeroAbilities abilities, string cached = null)
{
    if (abilities != null && !string.IsNullOrEmpty(abilities.HeroClass)) return abilities.HeroClass;
    if (!string.IsNullOrEmpty(cached)) return cached;
    var svc = DeNelle.Core.State.GameStateService.Instance;
    var opt = svc != null && svc.State != null
        ? DeNelle.Core.State.HeroClassOptExtensions.ToNullable(svc.State.HeroClass) : null;
    if (opt.HasValue)
    {
        string job = DeNelle.Core.State.PlayableHeroes.JobKey(opt.Value);
        DeNelle.Core.Diagnostics.FlowTrace.Once("HudModel", "class-from-gamestate-" + job,
            "HUD hero class: no live HeroAbilities (composed dungeon hero) - resolved '" + job +
            "' from the PERSISTED GameState.HeroClass, NOT a hardcoded class.");
        if (!string.IsNullOrEmpty(job)) return job;
    }
    DeNelle.Core.Diagnostics.FlowTrace.Warn("HudModel",
        "HUD hero class: no HeroAbilities AND no persisted GameState.HeroClass - falling back to '" +
        AbilityCatalog.DefaultClass + "'. The ability bar, the nameplate and the party card will all " +
        "key off that class; fix the SOURCE, do not treat this line as normal.");
    return AbilityCatalog.DefaultClass;
}
```

Then the three one-line edits:

- `HudModelProducers.cs:392` → `string cls = HudHeroClass(_abilities);`
- `HudModelProducers.cs:87`  → `string cls = HudHeroClass(_abilities, _classId);`
- `HudModelProducers.cs:139` → `string cls = HudHeroClass(_abilities);`

`PlayableHeroes.JobKey` and `GameStateService` are both `DeNelle.Core` — `DeNelle.Village` already
references it, so no asmdef change. `AbilityCatalog` is already in scope in this file.

**Secondary / DO NOT DO IN THIS WO — record only.** "Give the dungeon hero a real `HeroAbilities`"
would also fix the bar, but it is a **gameplay** change, not a presentation one: it would hand the
dungeon hero a live mana pool, castable Q/W/E/R and the whole `HeroAbilities.Update` tick, in a scene
that was deliberately built without them (`DungeonController.cs:471-474`). That is an owner design
call about whether abilities are castable in dungeons at all, and it belongs in its own ticket. This
WO makes the HUD **truthful**; it does not decide what the dungeon hero can cast.

---

## 5. Instrumentation — the seam is SILENT, and that is a finding in its own right

### 5.1 Measured silence (PROVEN-BY-CAPTURE, orchestrator grep of the live session)

Both `Player.log` and `break-log.jsonl` from her session were grepped for `Thrain`,
`Sword Wielding`, `Sword Heroic`, `Shield Charge`, `Warden's Grace`, `Radiant Strike`,
`HeroAbilities`, `CombatArc`, and every `Flow:Ability*` tag. **ZERO hits.** (The only `Warden`
matches are `TorchWardenDresser`, the dungeon NPC — unrelated.)

By contrast the BOTTOM bar traces itself on every posture change
(`[Flow:HudKit] action bar set -> [...]`).

**So the right-hand ability bar and the hero class/name identity emit NOTHING.** That is confirmed
at source: `AbilityLoadoutProducer` (`HudModelProducers.cs:381-479`) has no FlowTrace at all except
the icon-unmapped warn (`:445-449`), and neither `HeroVitalsProducer` nor `PartyProducer` traces the
class it resolved. This is why seq 2312 can prove the *body* is a Mage and says not one word about
the *bar* — and it is why **this bug was only catchable by the owner's eyes**. She should not have
had to tell us the scope. Half the cost of this session is the missing trace, not the missing fix.

**Ship the instrumentation even though the cause is already settled from source.** The three
literals in §3.5 are proven and need no capture; the instrumentation is for the NEXT one.

### 5.2 What to add

**(a) Class provenance — already in the fix.** The `FlowTrace.Once` / `FlowTrace.Warn` inside
`HudHeroClass` (§4) is the instrumentation, not a separate step: it names the class AND **where it
came from** — live component / persisted state / default. The `Warn` on the default path is the
loud one, deliberately: **a silent default is exactly what produced a Knight bar on a Mage with
nobody noticing.** Per CLAUDE.md §12 these are PERMANENT; do not strip them once green.

**(b) The bound ability set.** Add one line to the existing per-loadout-change signature block
(`HudModelProducers.cs:440-450`, which already fires only on change, never per poll) so the next
capture names the class, its SOURCE and the resulting skill ids:

```csharp
DeNelle.Core.Diagnostics.FlowTrace.Step("HudModel",
    $"ability bar bound: class='{cls}' source={(_abilities != null ? "HeroAbilities(live)" : "GameState/default")} " +
    $"ids=[{string.Join(",", slots.ConvertAll(s => s.Name))}] sig={sig}");
```

**(c) Hero identity.** `HudKitController.cs:1468` resolves the display name through
`HeroCanonNames.ForJob(v.ClassId)` and says nothing about it. Add one `FlowTrace.Once` there (keyed
on the resolved name so it fires once per identity, never per frame) naming the resolved display
name, the job key it came from, and the table (`en.json` key `hero.<job>.name`) — so "who does the
game think I am" is answerable from a capture instead of from the owner's eyes.

### 5.3 What the next capture must read

In a dungeon, as a Mage:

```
[Flow:HudModel] HUD hero class: no live HeroAbilities (composed dungeon hero) - resolved 'mage' from the PERSISTED GameState.HeroClass, NOT a hardcoded class.
[Flow:HudModel] ability bar bound: class='mage' source=GameState/default ids=[Fireball,Arcane Shell,Mend,Meteor Strike] ...
[Flow:HudKit]   hero identity: 'Thrain' from job 'mage' (en.json hero.mage.name)
```

If a capture ever reads `class='knight'` on a Mage again, that line names the defect on sight.

---

## 6. Files to edit

- `Assets/_Modules/Village/HUD/HudModelProducers.cs` — **this file only.**

## 7. What NOT to touch (other lanes are live on this same scene)

- `HeroLocomotion.cs`, `DungeonHero`, `HeroGaitForensics.cs`, `SmartMobileCamera.cs` — the
  `vel=0.00`-with-moving-position + frozen-camera lane (F8 2312/2313).
- The dialogue JSONs — the tutorial guide-identity lane (WO-1014).
- `HeroControlEnsurer.cs`, `DungeonBaker.cs`, `DungeonController.cs` — the secondary fix in §4 is
  **recorded, not implemented**. Do not add `HeroAbilities` to the dungeon hero in this WO.
- `abilities.json` — the Knight kit is correct data. Nothing is wrong with the data; the wrong
  class is being asked for.

## 8. Relationship to the other live lanes — SHARED ROOT? **NO.**

Stated explicitly, as asked:

- **vs. the `vel=0.00` / frozen-camera lane (F8 2312/2313): NOT the same root.** That lane lives in
  the locomotion + camera rig. This one never touches movement — it is a HUD producer picking a
  string. The two share a *scene* and a *capture*, nothing else. Note the same capture shows
  `[Flow:HeroLoco] ... controller=Mage` — the very line that proves the class is intact is the line
  the other lane is investigating for a different reason.
- **vs. WO-1014 (tutorial guide identity): NOT the same root**, but they are **adjacent and worth
  one look together**. Both are "a surface names the wrong character." WO-1014 is about the
  *dialogue/guide* identity read from JSON; this is about the *ability bar* class read from a missing
  component. Different readers, different sources, no shared line. If WO-1014's RCA also lands on a
  hardcoded class/name literal, that is a pattern worth a sweep — but it is not a shared cause here.

## 9. Acceptance criteria

1. Enter `Dungeon_HealersCottage` as a **Mage**. The right ActionBar renders the **Mage** kit —
   **Fireball (Q) / Arcane Shell (W) / Mend (E) / Meteor Strike (R)**
   (`abilities.json` mage block, `Assets/Resources/Data/Canonical/abilities.json:7`) — never
   Sword Heroic / Shield Charge / Warden's Grace / Radiant Strike.
2. The nameplate still reads **Thrain** (it was already correct — do not regress it).
3. `grep -n '"knight"' Assets/_Modules/Village/HUD/HudModelProducers.cs` returns **zero** hits.
4. A dungeon capture contains `[Flow:HudModel] ... class 'mage' (abilities component ABSENT)` and the
   `class-from-gamestate-mage` Once line.
5. Enter the town as a Mage: the bar, the nameplate and the party card slot 0 are unchanged from
   today (the `abilities != null` branch is identity — this must be a pure no-op in the village).
6. Play a **Knight** into a dungeon: the bar reads the Knight kit — i.e. it is right *because the
   state says Knight*, not because a literal said so.
7. Brace balance + NUL scan clean on `HudModelProducers.cs`.
8. `COMPILE_GATE_OK`; `REGRESSION_OK <n>/<n> suites`; `UI_CAPTURE_OK` with the dungeon bar PNG
   **opened and looked at** (this is a UI change — compile-green never proves a bar looks right).

## 10. Regression to add

Extend the HUD/ability regression with a case `[hud-class-fallback]` that asserts: with **no**
`HeroAbilities` present and `GameState.HeroClass = Mage`, the resolved HUD class is `"mage"` — and
that the string `"knight"` appears nowhere as a fallback literal in `HudModelProducers.cs`. The
second half is a source pin and is what stops this regressing a third time (it already regressed
once, in `GearLoadout`, under F8 seq-642).

---

**Provenance:** RCA by the F8 seq-2312 QA lane, 2026-08-10, read-only, from
`logs/f8-inbox/capture-20260810-183326.md` + source. Every claim in §3 is cited file:line and marked
PROVEN-BY-CAPTURE or READ-AT-SOURCE. The default-to-Knight is settled from source alone
(`HudModelProducers.cs:87/139/392`) and needs no further capture.
