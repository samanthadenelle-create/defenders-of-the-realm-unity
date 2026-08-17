<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

> ⚠ **UNRESOLVED NUMBER COLLISION — WO-280 is claimed by more than one file and OWNERSHIP IS NOT DECIDED.**
> Co-claimants: `WORK_ORDER_280_go_live_blockers.md`, `WORK_ORDER_280_village2_wiring_gate.md`
> Both added in the SAME commit (first-on-disk is a dead tie) and each is cited by exactly one other doc — the cross-reference tiebreak is also a tie.
> Flagged (not resolved) by the 2026-08-16 Sunday board-grooming pass — a wrong ownership call is worse
> than a flagged unknown, so this needs an **owner ruling**. Nothing renumbered or deleted. Until it is
> ruled, cite this WO by FILENAME, never by bare number.

# WO-280: Go-Live Blockers — Fix ALL before pushing to itch.io
**Status:** READY TO IMPLEMENT
**Priority:** P0 — BLOCKS DEPLOYMENT

These 9 bugs must be fixed before the next WebGL build deploys. Each one has a specific file/location to investigate.

---

## BUG 1: Giant blue sphere in village
**What:** A massive blue sphere clips through a building in the village. Unknown origin.
**Where to look:**
```bash
# Find any sphere primitives in the scene hierarchy
grep -rn "PrimitiveType.Sphere\|CreatePrimitive" Assets/_Modules/ --include="*.cs"

# Or in Unity: Hierarchy → search "Sphere" — find any GameObject with a MeshFilter containing the Unity sphere mesh

# Also check Village2Generator.cs for any debug sphere creation
grep -rn "Sphere\|sphere" Assets/Editor/Village2Generator.cs
```
**Fix:** Find the GameObject, delete it or disable its MeshRenderer. If it's a trigger volume, keep the Collider but remove the MeshRenderer component.

---

## BUG 2: Heartwood tree lost materials (grey/untextured)
**What:** `tree_of_life.fbx` renders as flat grey. Materials stripped during Village2 generation.
**Where to look:**
```
Assets/Resources/Structures/tree_of_life.fbx
Assets/Art/TripoStructures/
```
**Root cause:** The tree prefab's materials are not URP-compatible, or they get lost when Village2Generator instantiates it. `Instantiate()` preserves materials ONLY if the source prefab has them assigned.
**Fix (step by step):**
1. Select `tree_of_life.fbx` in Project panel
2. Check the Materials tab in Inspector — are materials assigned?
3. If pink/missing: run menu `Defenders > Art > Fix Polyperfect URP Materials` or manually assign a URP Lit material with the correct texture
4. Create a proper Prefab from the fixed mesh: drag into `Assets/Resources/Structures/TreeOfLife.prefab`
5. Update Village2Generator line 59 to reference this prefab instead of the raw FBX
6. The prefab will preserve materials on every instantiation

---

## BUG 3: Green disc (node selection indicator)
**What:** Green circle on the ground that follows the hero. Has persisted through 10+ "fixes."
**Root cause identified:** It's the ClaimableNode / MineNode selection indicator from the unfinished node system.
**Where to look:**
```bash
# Find the indicator script
grep -rn "SelectionIndicator\|selectionRing\|indicator\|green.*disc\|green.*circle" Assets/_Modules/ --include="*.cs"

# Find ClaimableNode and its visual components
grep -rn "class ClaimableNode\|class MineNode" Assets/_Modules/ --include="*.cs"
```
**Fix:**
1. In Unity Play mode, click the green disc in Scene view — note which GameObject is selected
2. Find what script enables it — likely a `Projector`, `DecalProjector`, `SpriteRenderer`, or `MeshRenderer` child
3. Either disable the entire ClaimableNode system for now, or specifically disable the indicator renderer
4. Search for any `AetherCrystal` or `MineNode` GameObjects in the scene and deactivate them

---

## BUG 4: Companion spawns as clone of hero
**What:** The companion character is the same model as the selected hero. Should be a DIFFERENT character.
**Where to look:**
```bash
# Find companion spawning logic
grep -rn "companion\|Companion" Assets/_Modules/ --include="*.cs" | head -20

# Check hero spawn / body swap
grep -rn "HeroBodySwapper\|CompanionSpawner\|companionPrefab" Assets/_Modules/ --include="*.cs"
```
**Fix:** The companion spawner needs a mapping table:
```csharp
// If hero == "Ranger" (Sylas), spawn companion == "Cleric" (Elara)
// If hero == "Knight" (Grom), spawn companion == "Ranger" (Sylas)
// If hero == "Mage" (Thrain), spawn companion == "Knight" (Grom)
// If hero == "Cleric" (Elara), spawn companion == "Mage" (Thrain)
```
Hero prefabs are at `Assets/Resources/Heroes/` — Cleric.fbx, Knight.fbx, Mage.fbx, Ranger.fbx.
CC5 models at `Assets/Models/People/Human/` — human_Cleric.fbx, human_tank.fbx, Human_Wizard.fbx, Human_Ranger.fbx.

If no `CompanionSpawner` exists yet, create one that reads `$heroClass` from GameState and instantiates the mapped companion prefab.

---

## BUG 5: Yarn dialogue not wired — no DialogueRunner in scene
**What:** All `.yarn` files are written but nothing plays. No intro, no companion dialogue, no NPC speech.
**Where to look:**
```
Assets/Dialogue/Intro/IntroSequence.yarn     — 9-screen intro
Assets/Dialogue/Tutorial/CompanionMeeting.yarn — tutorial flow
Assets/Dialogue/Lore/WorldLore.yarn          — lore nodes
Assets/Dialogue/NPCs/SoulAwakening.yarn      — NPC soul emergence
Assets/Dialogue/Companion/PostTutorialGuidance.yarn — post-tutorial paths
```
**Fix (step by step):**
1. In Village2 scene, create empty GameObject named "DialogueSystem"
2. Add component: `Dialogue Runner` (from Yarn Spinner package)
3. Add component: `Line View` (or wire to existing TownsfolkBubble UI)
4. Add component: `Options List View` (for branching choices)
5. Assign all `.yarn` files to the Dialogue Runner's Yarn Project
6. Create `YarnCommandBridge.cs` that registers custom commands:
```csharp
[YarnCommand("camera_focus")]
public static void CameraFocus(string target) { /* move camera */ }

[YarnCommand("start_autowalk")]
public static void StartAutoWalk(string target) { /* move hero */ }

[YarnCommand("spawn_wave_at_nearest")]
public static void SpawnWave(int count) { /* trigger wave */ }

[YarnCommand("grant_resources_for_towers")]
public static void GrantResources(int towerCount) { /* grant exact cost */ }

[YarnCommand("show_pet_name_prompt")]
public static void ShowPetName() { /* show name input */ }
```
7. Call `dialogueRunner.StartDialogue("Intro_Screen1")` on scene load for the intro
8. Call `dialogueRunner.StartDialogue("CompanionMeeting")` after hero select for the tutorial

---

## BUG 6: Intro sequence missing — game loads straight to hero select or village
**What:** No intro cinematic/story plays before the game starts. Music plays but no narrative.
**Where to look:**
```bash
# Find what loads first
grep -rn "LoadScene\|SceneManager" Assets/_Modules/ --include="*.cs" | grep -i "title\|hero\|select\|intro"
```
**Fix:** The intro sequence is defined in `Assets/Dialogue/Intro/IntroSequence.yarn` (9 screens). It needs:
1. A scene or overlay that plays BEFORE hero select
2. Each screen shows text + image/animation + audio cue
3. Text fades in, waits, fades out, next screen
4. Tappable to advance (skip button after first playthrough)
5. Final screen transitions to hero select

---

## VERIFICATION (all 6)

After fixing ALL bugs:
- [ ] No blue sphere visible anywhere in the village
- [ ] Heartwood tree renders with full color/materials
- [ ] No green disc anywhere in any scene
- [ ] Companion is a DIFFERENT character model from the hero
- [ ] Dialogue plays when triggered (test: enter Play mode, verify intro or NPC speech)
- [ ] Intro narrative plays before hero select on first load
- [ ] Screenshot each fix — attach to this work order before marking Done
- [ ] Test on WebGL build (not just editor) before deploying to itch.io

---

## BUG 7: Level up grants no stat increases
**What:** Leveling up shows a skill point popup but HP, mana, damage, cooldowns don't change. Hero doesn't actually get stronger.
**Where to look:**
```bash
grep -rn "OnLevelUp\|LevelUp\|levelUp" Assets/_Modules/Village/Hero/ --include="*.cs"
grep -rn "maxHP\|baseDamage\|maxMP" Assets/_Modules/Village/Hero/ --include="*.cs"
```
**Fix:** In `HeroProgression.OnLevelUp` (or wherever the level event fires):
```csharp
// Apply stat increases
HeroHealth.maxHP += 10;
HeroHealth.currentHP = HeroHealth.maxHP; // Full heal on level up
HeroMana.maxMP += 5;
HeroMana.currentMP = HeroMana.maxMP;    // Full mana restore
HeroAbilities.baseDamage += 2;

// Visual feedback
VFXManager.Instance.SpawnLevelUpBurst(hero.transform.position);
FloatingText.Spawn(hero.transform, "+Level Up!", Color.gold);
FloatingText.Spawn(hero.transform, "+10 HP  +5 MP  +2 ATK", Color.white);
```

## BUG 8: Skill popup blocks gameplay — should be side notification
**What:** "Level 2! Spend a skill point" popup spawns dead center, covers entire view during combat. Can't see enemies, can't play.
**Where to look:**
```bash
grep -rn "LevelUpSkillPopup\|SkillPopup\|skill.*popup" Assets/_Modules/ --include="*.cs"
```
**Fix:** Replace center modal with a small pill notification anchored to left edge or top-left. Player taps it when ready — never auto-opens during a wave.

## BUG 9: Thrain (mage) model rotated 90° — Elara works fine
**What:** Selecting Thrain as hero results in model facing 90° wrong. Elara (cleric) works correctly. Per-prefab rotation offset issue.
**Where to look:**
```bash
grep -rn "rotation\|Euler\|forward" Assets/_Modules/Village/Hero/HeroBodySwapper.cs
```
**Fix:** Apply same -90° Y rotation offset that was applied to Cleric prefab. Check ALL 4 hero prefabs:
- Cleric (works) — note its rotation offset value
- Mage (broken) — apply same offset
- Knight — verify
- Ranger — verify

---

## VERIFICATION (all 9)

- [ ] No blue sphere visible anywhere in the village
- [ ] Heartwood tree renders with full color/materials
- [ ] No green disc anywhere in any scene
- [ ] Companion is a DIFFERENT character model from the hero
- [ ] Dialogue plays when triggered (test: enter Play mode, verify intro or NPC speech)
- [ ] Intro narrative plays before hero select on first load
- [ ] Level up increases HP/MP/damage + full heal + visual feedback
- [ ] Skill popup appears as side notification, not center modal
- [ ] All 4 hero models face correct direction (verify each)
- [ ] Screenshot each fix — attach to this work order before marking Done
- [ ] Test on WebGL build (not just editor) before deploying to itch.io

## Do NOT Touch
- Village2Generator layout/positioning (separate WO-279)
- Tower placement system
- Wave manager / combat systems
- Any .unity scene files by hand
