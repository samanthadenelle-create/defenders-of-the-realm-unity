# Wardrobe & Cosmetics Architecture (Dressable capability)

**Status:** foundation SHIPPED (rig-level dress); data-driven layer = WO-456.
**Owner-ratified:** 2026-06-20. **Read this before touching character clothing / cosmetics / the cosmetic store.**

This is canon so the next session does **not** re-derive it (CLAUDE.md rule: read embedded
canon first, or the owner pays for rework). It is verified from the actual code, not comments.

---

## 1. The principle: dressing is a CAPABILITY at the rig level

A character being clothed is **not** a hero feature — it is a **capability of the skinned
humanoid body**, living at the **same level as the animation rig**. The rig is built in the one
shared path every character goes through, `VisualFactory.Skin`. The wardrobe lives **right there**,
beside the rig — not bolted onto `HeroArmorVisual` (a gameplay layer only some bodies carry).

Consequences (all intentional):
- **Generic:** hero, companions, arena fighters, **and any future human-skinned enemy** are dressed
  for free — no per-spawn code.
- **Capability-gated:** a body either *is dressable* (it ships outfit-set renderers) or it isn't
  (a skeleton / animal / structure). `IsDressable=false` → the wardrobe leaves it untouched.
- **One model:** mirrors the Carriable/Equippable ontology (`docs/ITEM_MODEL.md`) — a `Dressable`
  capability + an outfit collection.

This was the owner's call (2026-06-20): *"why not at the same level the animation rig lives?"* — yes.

---

## 2. The Blink body is modular (proven from a runtime capture)

The Blink human base body (`Assets/Blink/.../HumanMale_Character` / `HumanFemale_Character`, loaded via
`VisualFactory.Skin` / `HeroBodySwapper`) is a modular `SkinnedMeshRenderer` character:

- **Bare-skin mannequin:** `Arms`, `Legs`, `Chest`, `Feet` (single-token names, no prefix) + skin meshes
  (head / hands / face / hair…).
- **Swappable OUTFIT sets:** renderers **name-prefixed** by set — `Starter_*`, `Cloth1_*`, `Cloth2_*`,
  `Cloth3_*` (e.g. `Starter_Chest`, `Cloth1_Pants`). The `Starter` set is baked into the base prefab.

With **no** outfit shown the body reads as **underwear** (bare skin). That was TKT-2.

**Coverage note:** the outfit sets are **sleeveless** — they cover torso/legs/feet/hands/head/shoulders
but **not arms**. So the dressed look keeps the **bare arms** (skin) on purpose (an owner-liked look),
and hides the bare torso/legs the outfit covers.

---

## 3. What ships today (the seam)

`Assets/_Modules/Village/BlinkWardrobe.cs` — the capability, the single home for the dress logic:

| Member | Does |
|---|---|
| `IsDressable(GameObject)` | true if the body ships any outfit-set renderer (`Starter_*`/`Cloth*_*`). The gate. |
| `DressInStarter(GameObject)` | dress to the default outfit (`DefaultOutfit = "Starter"`). |
| `Dress(GameObject, outfit)` | dress to a **named** outfit set — the data-ready entry point. |
| `IsSkinRenderer` / `IsOutfitPart` / `IsOutfitOf` / `IsBareArm` | the renderer-name vocabulary (single home). |

`Dress(body, outfit)` sets, deterministically by renderer name (no snapshot, idempotent):
- **SHOW:** skin + the chosen outfit's pieces + bare arms.
- **HIDE:** bare torso/legs/feet (the outfit covers them) + **every other** outfit set.

**Invoked at the rig level** — `VisualFactory.Skin` (just after `VerifyRenders`, before it returns the
body): `if (BlinkWardrobe.IsDressable(go)) BlinkWardrobe.DressInStarter(go);`. So every dressable body
is clothed at birth.

**`HeroArmorVisual` USES it, does not own it:**
- `HideBaseBody` pieces-only branch → `BlinkWardrobe.DressInStarter(baseBody)` then overlays the armor
  pieces (uncovered spots show clothing, never skin).
- `RestoreBaseBody` (unequip / no-armor / non-Blink default) → `BlinkWardrobe.DressInStarter` (a
  restored body is clothed, not bare).
- Full-body armor sets still hide the **entire** base body (the armor is the whole character).
- `ArmorShipsOwnSkin` uses `BlinkWardrobe.IsSkinRenderer`.

### Why bare-skin-underlayer was wrong (history, so nobody repeats it)
The first fix kept the **bare mannequin** under pieces armor so a limb is never *missing*. But uncovered
areas then showed **skin = underwear** (owner: *"still just underwear"* under armor; *"start in something
other than underwear"*). The fix: the never-naked underlayer is a **clothed outfit**, not bare skin.

---

## 4. The data-driven layer (WO-456 — NOT yet built)

Owner architecture (2026-06-20), to be specced + built next. The default outfit is already a **parameter**
(`Dress(body, outfit)`), so this plugs onto the existing seam without re-plumbing:

1. **Per-character wardrobe JSON.** Each character carries a wardrobe definition (canonical data, the
   `Resources/StreamingAssets/Data/Canonical` system): a **default outfit** + the **owned/available**
   outfit collection. *"Depending on the character selected the JSON is different."*
2. **A living collection.** Unlock/buy an outfit → it joins the character's owned set → becomes equippable.
   The collection **adapts** as items are gained.
3. **Feeds the cosmetic store.** That same owned-vs-available collection **is** the cosmetic store's
   inventory (`PanelId.CosmeticShop`). Buy → add to the collection → equippable. **One data model, two
   consumers** (wardrobe + store). *"Which feeds directly to the stores when they go."*
4. **Selection drives the look.** Choosing an outfit calls `BlinkWardrobe.Dress(body, chosenOutfit)`.

This is the One Model again: a `Dressable` capability + an outfit collection, same shape as
Carriable/Equippable. Monetizable, future-proof (human enemies, new outfit packs), and the wardrobe +
store never drift because they read the same source.

---

## 5. Key files

| File | Role |
|---|---|
| `Assets/_Modules/Village/BlinkWardrobe.cs` | the Dressable capability (this doc's subject) |
| `Assets/_Modules/Village/VisualFactory.cs` | the rig level — calls the wardrobe after building any body |
| `Assets/_Modules/Village/Hero/HeroArmorVisual.cs` | gameplay armor swaps; *uses* the wardrobe |
| `Assets/_Modules/Village/Hero/HeroBodySwapper.cs` | builds the hero's Blink base body |
| `Assets/_Modules/Village/NPCs/StoryCompanionInjector.cs` | builds companion / arena bodies |
| `docs/ITEM_MODEL.md` | the capability-ontology this mirrors |
| `WORK_ORDER_456_*` | the data-driven wardrobe + store feed (to be written) |

**Follow-up flagged:** `TroopFactory` skins `Heroes/*` models but does not add `HeroArmorVisual`; troops
are dressed by the `VisualFactory.Skin` hook if they are Blink humans — confirm in a build, else they
inherit the same one-line treatment.
