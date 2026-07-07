# Ability Icon Audit — Battle HUD (WO-609 HudKit)
Date: 2026-07-05  •  Scope: read-only data-integrity audit  •  Repo: C:\eoa

## Verdict (TL;DR)
**A blank ability slot is not merely possible — it is the GUARANTEED current state for every
equipped ability on the action rail (Q/W/E/R) and the assignable hot-swap bar.**
The slot builder is fed the ability decorative **glyph** ("✦ ❄ ✚ ☄ ➹ ➾") as the icon key, but the
icon resolver only understands **concept tokens** (effect / abilityId), so every lookup returns
`null`, and `SetIcon(null)` *disables* the icon Image with no placeholder fallback. The RPG icon art
that WOULD satisfy these slots exists on disk and is already mapped in `concept-icons.json` — the
producer is simply passing the wrong key.

---

## 1. The resolution path (traced)

Ability -> slot Sprite, for the active WO-609 HudKit:

1. **Producer builds the slot record** and stores `def.Icon` (the glyph) as `IconKey`:
   - actionRail Q/W/E/R: `AbilityLoadoutProducer.Poll` — `string icon = equipped ? def.Icon : null;`
     then `new AbilitySlotRecord(key, key, name, "", icon, accent, ...)`
     Assets/_Modules/Village/HUD/HudModelProducers.cs:401 and :403
     (Q = `AbilityCatalog.Find(cls, slot)`, W/E/R = `HeroLoadout` id -> `AbilityCatalog.FindById`, :414-420)
   - assignable bar: `AssignableLoadoutProducer.Poll` — `string icon = def != null ? def.Icon : null;`
     Assets/_Modules/Village/HUD/HudModelProducers.cs:653
2. **AbilitySlotRecord**: the 5th ctor arg lands in `IconKey`
   Assets/_Modules/Core/HudModel/HudModelTypes.cs:105,116,123
3. **HudKit turns IconKey into a Sprite**:
   - abilities: `h.SetIcon(string.IsNullOrEmpty(s.IconKey) ? null : UiStyle.Icon(s.IconKey));`
     Assets/_Modules/HUD/Kit/HudKitController.cs:799
   - assignable: same call — Assets/_Modules/HUD/Kit/HudKitController.cs:814
4. **UiStyle.Icon** -> `ConceptIconResolver.Resolve` (single-arg, no default fallback)
   Assets/_Modules/Core/UI/UiStyle.cs:318-327
5. **ConceptIconResolver.Resolve** looks up the lower-cased key in `concept-icons.json`; **unmapped => returns null** (it does NOT fall through to its own default/DefaultSprite)
   Assets/_Modules/Core/UI/ConceptIconResolver.cs:79-95
6. **ActionSlotHandle.SetIcon(null)** -> `icon.sprite = null; icon.enabled = false;` — the icon Image is turned OFF. No placeholder.
   Assets/_Modules/Core/UI/ElarionUiKitObsidian.cs:767-772

### Why every lookup misses
The value passed at step 1 is the glyph string from `abilities.json` (`"icon": "✦"` etc.).
The `concept-icons.json` map is keyed by **effect names** (strike/snare/aoe/heal/meteor/cleave/dash/
knockback/taunt) and **abilityIds** (knight.ranged-poke, ...) — see
Assets/Resources/Data/Canonical/concept-icons.json:6-38. There is **no key for any glyph char**.
`Resolve("✦")` therefore always returns null.

Proof the art is present and reachable (if the correct key were passed):
Assets/Resources/RpgUi/icons/ contains icon_sword.png, icon_shield.png, icon_heart.png,
icon_combat.png, ... — all 10 concept icons exist on disk. The failure is the **key**, not missing art.

---

## 2. Every HUD-reachable ability + icon resolution

Data source: Assets/StreamingAssets/Data/Canonical/abilities.json (mirrored to Assets/Resources/...).
"Icon ref" is the raw `icon` glyph the producer forwards. "Resolves?" = does that value produce a
Sprite through the live HudKit path (UiStyle.Icon(glyph)).

### Class basics — actionRail Q (AbilityCatalog.Find(cls, Q))
| Ability id / slot | Source | Icon ref (glyph) | Field present? | Resolves on HUD? |
|---|---|---|---|---|
| mage.q — Arcane Bolt | class-basic (Q) | ✦ | YES | **MISSING** (glyph != concept) |
| knight.q — Heroic Leap | class-basic (Q) | ➹ | YES | **MISSING** |
| ranger.q — Quick Shot | class-basic (Q) | ✦ | YES | **MISSING** |

### Skill-tree equippables — actionRail W/E/R (HeroLoadout) + assignable bar (FindById)
knight-skills pool, referenced by talent nodes knight.t1n2/t1n4/... (abilityId):
| Ability id | Granting node (hero-talents.json) | Icon ref | Field present? | Resolves on HUD? |
|---|---|---|---|---|
| knight.ranged-poke — Throwing Spear | knight.t1n2 Spear Thrust | ➹ | YES | **MISSING** |
| knight.mending-salve — Mending Salve | knight.t1n4 (mod) / pool | ✚ | YES | **MISSING** |
| knight.snare-arrow — Snare Arrow | pool | ❄ | YES | **MISSING** |
| knight.suppressing-volley — Suppressing Volley | knight.t3n1 (mod) / pool | ✦ | YES | **MISSING** |
| knight.shield-bash — Shield Bash | pool | ✦ | YES | **MISSING** |

### Universal skills — assignable bar (shared nodes n9-n11 grant these)
| Ability id | Granting node | Icon ref | Field present? | Resolves on HUD? |
|---|---|---|---|---|
| universal.arcane-bolt — Arcane Bolt | shared.n9 | ✦ | YES | **MISSING** |
| universal.mend — Mend | shared.n10 | ✚ | YES | **MISSING** |
| universal.dash — Dash | shared.n11 | ➾ | YES | **MISSING** |

Notes:
- The per-class **W/E/R kit defs** (mage Frost Nova / Healing Beacon / Meteor Strike; knight Shield
  Bash / Defender Call / Radiant Strike; ranger Snare Trap / Mending Salve / Storm of Arrows) carry
  **no `id`** in abilities.json, so AbilityCatalog.FindById cannot equip them into HeroLoadout W/E/R
  — they are not reachable on the rail today. If ids are added later they hit the same glyph->null path.
- The mage/ranger talent trees are "stored, not wired in V1" (hero-talents.json:6,42,73); their skill
  nodes reference abilityIds (ranger.hunters-mark, mage.void-rift, ...) that **do not exist in
  abilities.json** — so even the data layer cannot resolve them yet (latent gap, out of scope for V1 HUD).

---

## 3. Counts
- **HUD-reachable active abilities (icon-bearing, resolvable path): 11**
  (3 class-basic Q + 5 knight-skills + 3 universal)
- **Have a non-empty `icon` field in data: 11 / 11** (100% — data is complete)
- **Resolve to an actual Sprite on the HUD today: 0 / 11**
- **Missing / at-risk (render no ability image): 11 / 11**

The data is NOT the problem — every ability has an icon glyph. The **resolution wiring** is the
problem: 100% of ability slots fail to produce art.

---

## 4. Fallback verdict — can a blank slot happen today?
**YES — and it is the current default, not an edge case.**

- The slot icon Image is disabled on a null sprite, with no placeholder:
  ActionSlotHandle.SetIcon: `icon.sprite = s; icon.enabled = s != null;`
  Assets/_Modules/Core/UI/ElarionUiKitObsidian.cs:767-772
- The HudKit call sites pass null straight through on any miss, with no guard:
  HudKitController.cs:799 (abilities), :814 (assignable).
- UiStyle.Icon(glyph) returns null for every glyph (no concept match), and does **not** invoke
  ConceptIconResolver.DefaultSprite() (icon_combat) as a catch-all — that safety net exists in the
  resolver (ConceptIconResolver.cs:157-161) but the HUD path never calls it.

Result: the slot **frame chrome** (Action_Bar_Slot art or the procedural dark cell) + cooldown ring
still draw, but the **ability image area is empty** for every equipped ability. This violates the
owner "there must ALWAYS be an image" rule and HUD_OBSIDIAN §1 "null art can never blank a surface".

(Peripheral, same root cause: concept-icons.json:40-44 maps gold/wood/iron/food/crystal to role
`currency`, but there is **no Assets/Resources/RpgUi/currency/ folder** — those currency-chip icons
also resolve null. Not an ability slot, but flagged since it fails the same "always an image" bar.)

---

## 5. Recommendation
Two independent fixes; do BOTH for a guaranteed non-blank slot.

**A. Feed the resolver a concept key, not the glyph (fixes all 11 at once, art already on disk).**
In AbilityLoadoutProducer (HudModelProducers.cs:401) and AssignableLoadoutProducer (:653), set the
record icon key from a *resolvable* concept — prefer the ability id, fall back to the effect —
instead of def.Icon. e.g. store def.Id (or def.Effect) and let UiStyle.Icon(id, effect) /
ConceptIconResolver.ResolveAny(id, effect) pick. Every effect (strike/snare/aoe/heal/meteor/cleave/
dash/knockback/taunt) and every knight-skill id is already mapped in concept-icons.json, and the
target sprites exist in Resources/RpgUi/icons/. No new art needed for the 11 abilities.
- Add the 3 universal ids to the map for exact art (optional; they otherwise fall to effect):
  universal.arcane-bolt->icon_combat, universal.mend->icon_heart, universal.dash->icon_combat.

**B. Add a guaranteed placeholder in the slot builder (defense-in-depth, honors §1).**
In ActionSlotHandle.SetIcon (ElarionUiKitObsidian.cs:767), when s == null for an *equipped* slot,
substitute ConceptIconResolver.DefaultSprite() (the icon_combat catch-all already defined at
concept-icons.json:4) rather than disabling the Image — OR have the two HudKit call sites
(HudKitController.cs:799,814) pass a non-null fallback. This makes a blank ability image
structurally impossible even if a future ability has an unmapped concept.

**Missing-art to actually author: none for the 11 current abilities** (art + mappings exist; only the
key is wrong). The only genuinely-absent art is the RpgUi/currency/* set (peripheral, resource
chips) — either add that folder or remap those five concepts to an existing role/name.
