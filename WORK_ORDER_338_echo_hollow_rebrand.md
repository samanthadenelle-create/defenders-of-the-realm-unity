# WORK ORDER 338 — Echo Hollow Rebrand (formerly "Pet House")

**Status:** READY TO IMPLEMENT
**Lane:** 12 (Narrative/Onboarding/Quests) + touches Lane 4 (UI strings) and Lane 1 (world label)
**Priority:** MEDIUM — naming polish; no gameplay regression risk
**Creative decision:** Approved by owner 2026-06-07
**Dependency:** Do AFTER WO-337 (dialogue overlap fix) — easier to edit clean dialogue

---

## Creative Direction

### Concept
Pets in Defenders of the Realm are **Echoes** — spirit-bound companions that
resonate with the hero, remnants of wild creatures whose essence was captured
by the Heart of Elarion. They are not tamed animals; they are living reverberations
of the natural world, bound by ancient attunement.

### The Building: **Echo Hollow**
> *An ancient recess carved into the roots of the world tree where spirit-bound
> companions rest between battles, their presence felt as a low hum in the stones.*

The NPC keeper is the **Echo Warden** (or **Hollow Keeper** — pick whichever
fits the existing NPC's voice).

### Terminology Glossary (apply consistently across all text)

| Old term | New term | Notes |
|---|---|---|
| Pet House | Echo Hollow | Building name |
| Pet | Echo | General reference ("your echo") |
| Pets | Echoes | Plural |
| Guardian (if used) | Echo / Companion | Functional synonym |
| "adopt a pet" | "attune an echo" | Verb for acquiring |
| "your pet will fight for you" | "your echo fights at your side" | Action phrase |
| Hollow Keeper / Echo Warden | (either) | NPC keeper title |

### Tone
The Echo Hollow feels **reverent**, not cozy. The NPC speaks with quiet gravity,
not pet-shop cheerfulness. Echoes are rare gifts. Adjust any lines that feel
too mundane.

---

## Files to Update

### 1. Yarn Dialogue Files
Search `Assets/_Modules/Narrative/` (and `Assets/Dialogue/`, `Assets/Yarn/`) for
any `.yarn` files referencing "Pet House", "pet", "guardian", "adopt":

```
Suggested search terms:
  grep -ri "pet house" Assets/
  grep -ri "adopt" Assets/
  grep -ri "guardian" Assets/Dialogue/
```

Update all NPC lines and player option text to the new glossary above.

**Example rewrite** (the line visible in the bug screenshot):

*Before:*
> "Welcome to the Pet House. Choose a guardian and they'll fight at your side,
>  defend your home, and come back."

*After:*
> "Welcome to the Echo Hollow. The echoes here seek a bond. Choose one — they
>  will fight at your side, guard Elarion, and return to you always."

Player options:
- "I'll take a look." → keep or adjust
- "Not now, come back later." → keep

---

### 2. UI Strings / en.json
Find and replace in `Assets/_Modules/Core/Localisation/en.json`
(or wherever string tables live):

```json
"building_pet_house": "Echo Hollow",
"pet_house_interact_label": "Echo Hollow",
"pet_house_description": "Attune a spirit companion to fight at your side.",
"pet_singular": "Echo",
"pet_plural": "Echoes",
"pet_action_acquire": "Attune Echo",
"pet_action_dismiss": "Release Echo"
```

---

### 3. VillageSceneBuilder — World Label
In `Assets/_Modules/Editor/VillageSceneBuilder.cs`, find the line that sets the
world-space label for the Pet House building and update the string:

```csharp
// Before:
label.text = "Pet House";
// After:
label.text = "Echo Hollow";
```

Also update any `BuildingType.PetHouse` enum string display if used in a
`[InspectorName]` or `ToString()`.

---

### 4. Code Identifiers (rename where safe)

| Symbol | Action |
|---|---|
| `BuildingType.PetHouse` | Rename to `BuildingType.EchoHollow` (update all usages) |
| Any `petHouseLabel` variable | Rename to `echoHollowLabel` |
| `"PetHouse"` string tag / key | Rename to `"EchoHollow"` everywhere |

Run a full solution search for "PetHouse" and "Pet House" before committing.
Ensure the `Village.unity` label rebuild is triggered via the scene builder menu
after any VillageSceneBuilder change (never hand-edit the scene).

---

### 5. DESIGN-DECISIONS.md
Add a new entry:

```markdown
## Design Decision #N — Echo Hollow (formerly Pet House)

**Date:** 2026-06-07
**Decision:** Rename "Pet House" to "Echo Hollow". Pets are canonically called
"Echoes" — spirit-bound companions attuned through the Heart of Elarion. The
building name, all NPC dialogue, and UI strings must use the new terminology.
**Glossary:** See WORK_ORDER_338_echo_hollow_rebrand.md for full term table.
```

---

### 6. Polyperfect Catalog + Any Asset Refs
If `docs/polyperfect-asset-catalog.md` or any doc references the building by
its old name, update in place.

---

## Acceptance Criteria

- [ ] Building world label reads "Echo Hollow" in game view
- [ ] "Interact: Pet House" tooltip → "Interact: Echo Hollow"
- [ ] NPC opening line uses new Echo Hollow / echo glossary
- [ ] All player choice options use updated terminology
- [ ] `BuildingType.PetHouse` renamed to `BuildingType.EchoHollow` with no
      compile errors
- [ ] `en.json` / string table updated; no hardcoded "Pet House" strings remain
      (grep confirm)
- [ ] DESIGN-DECISIONS.md entry added
- [ ] Village scene label rebuilt via VillageSceneBuilder menu (not hand-edited)
- [ ] No regression to pet/echo gameplay logic

## What NOT to Touch

- Village.unity (hand-edits forbidden; use SceneBuilder menu)
- Any gameplay code that is string-keyed only by pet *slot index*
- WO-297–299 pet system code (these implement the mechanic; this WO is naming only)
