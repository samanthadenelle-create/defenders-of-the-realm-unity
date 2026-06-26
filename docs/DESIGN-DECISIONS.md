> ⚠ **STALE NARRATIVE — the Cathedral Spire replacing the world-Tree was REVERSED (owner ruling 2026-06-26): the living world-Tree is canon.** Treat any "Spire replaces the Tree" / "Heart-Tree burned" entries as superseded; other entries may still hold. Live reality: `CANON_GROUND_TRUTH_2026-06-26.md` + `docs/COMBAT_PIVOT_NORTHSTAR.md`.

# Design Decisions — Defenders of the Realm v2 Unity port

Source-of-truth changelog for what has shifted away from the board-game original
during the Unity port. Dates are absolute (ISO `YYYY-MM-DD`). Bullets are
factual past-tense; this is a PM reference, not narrative. Cross-references at
the foot of the file link the longer-form storyline and dragon-boss notes.

---

## Worldbuilding

### 1. Village renamed to Elarion

- **What:** The defended village is named **Elarion** throughout code, UXML, and canon strings. The board-game stand-in "Avalon" is retired.
- **When:** Established by 2026-05-20 (current state).
- **Why:** Distance the v2 product from the v1 working name and anchor the cathedral-spire premise.
- **Where:** `Assets/Editor/VillageSceneBuilder.cs` builds the scene around `BuildElarion(...)` at line 763; the spire GameObject is named `"Heart (Cathedral Spire)"`. Storyline title in `docs/STORYLINE.md` reads "Elarion-of-the-Spire".

### 2. Tree of Life replaced by the Cathedral Spire

- **What:** The plaza centerpiece is no longer a world-tree with violet crystalline veins; it is the Tripo cathedral FBX, scaled up to read as a stone spire (`Heart (Cathedral Spire)`).
- **When:** 2026-05-20.
- **Why:** Owner direction: replace the rock-cluster + tree centerpiece with the fantasy cathedral; raise the silhouette by ~15% (7m to 8.05m) so it reads at city-edge distance.
- **Where:** `Assets/Editor/VillageSceneBuilder.cs` `BuildElarion(...)` (line 763) — loads `Assets/Models/Cathedral/Cathedral.fbx`, normalises to 8.05m at line 797, attaches `DeNelle.Core.TripoMaterialFixer` with `Textures/Cathedral` fallback. When the cathedral FBX loads the legacy tree, crystal-vein, and standing-stone-ring dressing all goto-skip (line 856).

### 3. Keep and Avalon Banner removed

- **What:** The Keeper's Keep building and the flanking violet Avalon Banner pole no longer spawn. The plaza centre is the cathedral alone.
- **When:** 2026-05-20.
- **Why:** Owner direction ("THESE TWO THINGS NEED REMOVED") — both read as clutter next to the new spire.
- **Where:** `Assets/Editor/VillageSceneBuilder.cs` `BuildKeep(Transform)` at line 954 is now an empty no-op body with an explanatory comment.

### 4. Heart Pond / wilderness ponds removed

- **What:** No ponds spawn in the exterior wilderness. The blue-disc water quads are disabled.
- **When:** 2026-05-20.
- **Why:** Owner direction ("leftover from heart pond") — the discs read as a puddle around the cathedral.
- **Where:** `Assets/Editor/ExteriorTerrainBuilder.cs` `PlacePonds(Transform)` at line 953 early-returns on line 958 (followed by `#pragma warning disable CS0162` and the unreachable legacy code preserved for re-enabling later).

### 5. Wilderness boulder / cliff scatter removed

- **What:** No boulder scatter, no cliff-rock clusters in the exterior terrain. Other wilderness dressing (scatter trees, shrine, standing stones, mounds, stone veins) is similarly off or absent.
- **When:** 2026-05-20.
- **Why:** Owner direction ("rocks in front of door") — rocks were landing visible from the cardinal-gate thresholds and reading as obstacles.
- **Where:** `Assets/Editor/ExteriorTerrainBuilder.cs` `ScatterRocks(...)` sets `int boulderTarget = 0;` at line 863 so the while-loop never executes; the two `SeedCliff(...)` calls are commented out at lines 887-888. Centerpiece crystal-veins are skipped via the `SkipLegacyDressing` goto in `BuildElarion` (`VillageSceneBuilder.cs` line 856).

### 6. Crystal Mine relocated outside the west wall

- **What:** The Crystal Mine is no longer one of the interior gameplay buildings; it sits outside the curtain wall to the north-west, requiring the hero to walk out the west or north gate to mine.
- **When:** 2026-05-20.
- **Why:** Owner direction ("move those mines outside the village for foraging").
- **Where:** `Assets/Editor/VillageSceneBuilder.cs` `Buildings[]` static array (line 982), Crystal Mine entry at line 987 with `X = -38f, Z = 14f, YawDeg = 135f`.

---

## Heroes and Companions

### 7. Three hero classes — Mage, Knight, Ranger

- **What:** The hero-select screen presents three cards: Mage (arcane violet), Knight (steel-gold), Ranger (wood-green). Knight and Ranger are additions to the original single-hero board-game cast.
- **When:** Knight and Ranger added 2026-05-20. (Catalog already had Mage.)
- **Why:** Broaden the class fantasy to support the RPG-mobile pivot and give the title screen three flanks to click.
- **Where:** `Assets/_Modules/Onboarding/HeroCatalog.cs` static `Heroes[]` array (lines 62-73). Enum lives in `DeNelle.Core.State.HeroClass`. Hero FBXs ship under `Assets/Models/Wizard/`, `Assets/Models/Knight/`, `Assets/Models/Ranger/`. (Note: code uses `HeroClass.Mage`; "Wizard" appears in UI copy and the FBX folder name.)

### 8. Three companion pets — Aether Sprite, Flame Pup, Ice Wolf

- **What:** Three starter "Warden" pets: an Aether Sprite (aether / Heart-Ward), a Flame Pup (flame / Hearth-Hound), and an Ice Wolf (ice / Frost-Warden). Each has five bond ranks with per-rank perks.
- **When:** 2026-05-20 (canon `pets.json`).
- **Why:** Replace the board-game pet token with a class-flavoured trio that supports the bond-progression system.
- **Where:** Canon data at `Assets/StreamingAssets/Data/Canonical/pets.json`. Display catalog at `Assets/_Modules/Onboarding/IntroPetCatalog.cs`. Deployment logic at `Assets/_Modules/Pets/PetDeployer.cs`. NOTE: in code/data the fairy pet is named **Aether Sprite** (id `pet-aether-sprite`), not "Twilight Sprite" — see "Pivots that did not match code" below.

### 9. New chibi wizard portrait

- **What:** New mage portrait — chibi mage with starry purple robe and crystal staff — replaces the prior render. Knight and ranger portraits remain JPG.
- **When:** 2026-05-20.
- **Why:** Owner-driven art update to push the title screen toward the RPG-mobile feel.
- **Where:** PNG at `Assets/_Modules/Onboarding/Resources/HeroPortraits/mage.png` (sibling JPGs: `knight.jpg`, `ranger.jpg`). Loaded by `TitleController.cs` line 205 (`Resources.Load<Texture2D>("HeroPortraits/mage")`) and assigned as the left flank's background image. Git commit `4701357` — "New wizard portrait — chibi mage with starry robe + crystal staff".

---

## Gameplay

### 10. BuildMenu simplified to Tower + Repair

- **What:** The build menu shows two cards only — "Build Tower" (Arcane Tower) and "Repair Wall". The five-building grid is gone.
- **When:** 2026-05-20.
- **Why:** Owner direction ("when click build should get two options one for tower one for repair").
- **Where:** `Assets/_Modules/Village/Buildings/UI/BuildMenu.cs` `Render()` method (lines 236-255). Repair routes through `DeNelle.Village.WallRepairController` by reflection (line 285) — `RepairNearestDamagedWall` / `ConfirmRepair` / `StartRepair`, whichever resolves first.

### 11. Two playable dungeons — Healer's Cottage and Folk's Old Granary

- **What:** Two real dungeons exist: Healer's Cottage (west portal, scene `Dungeon_HealersCottage`) and Folk's Old Granary (east portal, scene `Dungeon_FolksGranary`). Granary is the newer of the two and uses ~80 KayKit prefabs.
- **When:** Folk's Old Granary authored 2026-05-20.
- **Why:** Promote the previously-stubbed second dungeon to real geometry for the playable loop.
- **Where:** Healer's Cottage built by `Assets/Editor/DungeonSceneBuilder.cs` (2161 lines). Folk's Old Granary built by `Assets/Editor/FolksGranaryBuilder.cs` (1064 lines — larger than the ~700-line estimate in the brief). The remaining five secondary dungeons stay as `Assets/Editor/DungeonStubBuilder.cs` placeholders.

### 12. Dungeon portals — walk-in or F-key, violet disc + floating sign

- **What:** Each dungeon entry point is a glowing violet ground disc with a billboard sign reading "▼ Name ▼". Player walks onto the disc (trigger collider) or presses F when in range.
- **When:** 2026-05-20.
- **Why:** Owner observation ("cannot find entrance to healers cottage") — needed a visible, always-on marker rather than a painted arch that depended on a fragile shader.
- **Where:** `Assets/Editor/VillageSceneBuilder.cs` `SpawnDungeonPortal()` (line 1669) and `BuildOneDungeonPortal(...)` (line 1704). Portals at `(-18, 0, 6)` for HealersCottage and `(+18, 0, 6)` for FolksGranary — both relocated off the N-S gate spine to the east and west. Runtime trigger handled by `DeNelle.Village.DungeonPortal` (resolved via reflection in `VillageSceneBuilder.cs`).

### 13. F-key building interactions

- **What:** Walking near any of the five gameplay buildings shows a gold "〔 F 〕 Label" prompt. Pressing F triggers per-building behaviour: PetHouse opens the pet-skill-tree panel (simulated `P` press), Workshop opens crafting (`K`), Arcane Tower opens the talent tree (`T`), Crystal Mine and Farm fire crystal / food toasts.
- **When:** 2026-05-20.
- **Why:** Owner observation ("no interaction") — buildings needed an affordance.
- **Where:** Component `Assets/_Modules/Village/Buildings/BuildingInteractable.cs`. Action dispatch `Interact()` at line 92; key simulation `SimulateKeyPress(KeyCode)` at line 136. Wired onto every `Building` in the scene by `VillageSceneBuilder.WireBuildingInteractables()` (line 1588). Activation radius 6m, prompt height 3.2m above the building.

---

## Art and UI

### 14. HP and Mana both top-left

- **What:** Hero HP card and mana panel are stacked at the top-left corner of the village HUD. Mana used to sit at the bottom of the screen.
- **When:** 2026-05-20.
- **Why:** Owner direction ("HP bar top left, mana is bottom — please move both to top left").
- **Where:** `Assets/_Modules/HUD/VillageHudController.cs` `MoveManaPanelToTopLeft()` at line 277 — overrides the UXML layout in `Assets/_Modules/HUD/VillageHud.uxml` by setting `manaPanel.style.top = 64; left = 16; width = 220;` at runtime (so the UXML asset is unchanged).

### 15. Title screen — flanks pick the hero; Start button hidden

- **What:** The three hero flanks (wizard left, knight centre, archer right) are themselves the start trigger. The Start button is hidden, Connect Wallet is moved to the top-right, and an amber "✦ Select your Hero to begin ✦" call-to-action sits below the title block.
- **When:** 2026-05-20 ("still love the title" — owner ratified).
- **Why:** Owner direction — fewer clicks to game-start, and the flanks already read as the hero offer.
- **Where:** `Assets/_Modules/Onboarding/TitleController.cs` — flank wiring at lines 219-221 (`WireFlankAsHeroPicker`), Start hidden at line 231 (`_startButton.style.display = DisplayStyle.None`), Connect Wallet repositioned at lines 235-243, CTA inserted by `AddHeroCallToAction(...)` at line 274. UXML asset `Assets/_Modules/Onboarding/UI/TitleScreen.uxml` still declares the Start button — it is hidden at runtime, not removed from the document.

### 16. Village structures spread ~1.5× wider

- **What:** The dressing-building cluster (Residential SW, Workshop NE, etc.) sits with each footprint pushed out ~1.5× from its prior position so the hero and pet pack can path between them.
- **When:** 2026-05-20.
- **Why:** Owner direction ("spread out the town structures wider — the clustered ones prevent navigation").
- **Where:** `Assets/Editor/VillageSceneBuilder.cs` `BuildCityDressing(Transform)` at line 1146 — explanatory comment at lines 1148-1152; `BuildingScale` constant is 3.0f (line 95) and is applied as `localScale *= BuildingScale` throughout the building placement helpers.

---

## Open Questions and Direction Shifts

### 17. Dragon cinematic fly-bys, parallel to wave-boss combat

- **What:** Random non-interactive Dragon fly-bys across Elarion (every 35-80s) while the apex wave-boss encounter (Syndrath the Devourer, full HP-gated phases) remains the real fight. Flyby is skipped while the wave-boss is alive in the scene.
- **When:** 2026-05-20 (owner-ratified).
- **Why:** Owner direction — "Being a boss we could have him randomly fly across the city. cinematic not interactive."
- **Where:** `Assets/_Modules/Village/Cinematics/DragonCinematicFlyby.cs` — schedule fields `_minIntervalSeconds = 35f`, `_maxIntervalSeconds = 80f`. Reuses `Boss_Dragon` prefab; disables the combat `DragonBoss` brain by reflection so it flies on a straight-line FlightDriver Lerp. Wave-boss combat in `DeNelle.Village.DragonBoss` (see `docs/port-notes/dragon-boss.md`).

### 18. Lantern motif under reconsideration

- **What:** The "lantern / Lampwardens" framing in the v1 narrative bible is dropped. The storyline has been revised toward a "Stone Choir" frame (the spire holds the realm's last note; Choristers hold the chord).
- **When:** 2026-05-20.
- **Why:** Owner direction — "the lantern motif might not work as well with how we have pivoted. its becoming more of a RPG mobile."
- **Where:** `docs/STORYLINE.md` — top-of-file banner reads "Revised 2026-05-20 — lantern motif dropped per owner direction; replaced with the 'Stone Choir' frame." Supersedes (but does not delete) `docs/narrative-bible.md`.

### 19. Product feel pivoting to RPG-mobile

- **What:** Tone, motif, and UI direction now optimise for mobile RPG marketing rather than the board-game-bookish v1 feel. "Chibi wizard meets Gothic spire meets KayKit market stalls — pitched for a mobile RPG: every noun should slot into a banner, every beat into a screenshot." (storyline preface).
- **When:** 2026-05-20.
- **Why:** Owner direction — "the product feel is becoming RPG-mobile, not board-game-bookish."
- **Where:** Storyline tone block in `docs/STORYLINE.md` line 7. Wizard portrait change (#9), title-screen flow (#15), and lantern-motif drop (#18) all flow from this pivot.

### 20. Storyline reframed by creative

- **What:** Heart-Tree burned a century ago. The spire is a stone reliquary raised over its stump that "sings" one held note. No king, no keep — Choristers hold the chord. Alduin the Mournful, the Hollow Ones, the Withering, the cycle all carry over from the narrative bible.
- **When:** 2026-05-20.
- **Why:** Marries the in-world fiction to the worldbuilding pivots (#2, #3): the cathedral has to exist for a reason, the missing Keep has to be explained.
- **Where:** `docs/STORYLINE.md` sections 1-3 (the central conflict, the absent king, the Withering's Choir). Frost-Voice / Ember-Voice / Half-Voice categories at lines 45-47 give the wave-spawner three flavour buckets.

---

### 21. Echo Hollow (formerly "Pet House")

- **What:** Rename the "Pet House" building to **Echo Hollow**. Pets are canonically **Echoes** — spirit-bound companions attuned through the Heart of Elarion, not tamed animals. The verb for acquiring is **attune** (not adopt); the building's keeper NPC is the **Echo Warden**. The building name, all NPC dialogue, and player-facing UI strings use the new terminology. Tone is reverent, not cozy.
- **When:** 2026-06-07 (WO-338, approved by owner).
- **Why:** Naming polish that aligns the building with the established "Echoes" lore already used in the companion dialogue; "Pet House" read as pet-shop-mundane.
- **Where:** Building label in `Assets/Editor/VillageSceneBuilder.Content.cs`; `Assets/Dialogue/Structures/StructureMenu.yarn` (PetHouse node), `CompanionMeeting.yarn`, `PostTutorialGuidance.yarn`; canon-strings / structures-catalog / en.json / cosmetics.json (Resources + StreamingAssets copies); `BuildingInteractable.LabelFor`, `PetSkillTreePanel` header, `TutorialDirector` narration. Full glossary in `WORK_ORDER_338_echo_hollow_rebrand.md`.
- **NOT changed (deliberate):** The INTERNAL `pet-house` building id, the `BuildingType.PetHouse` enum value (=1), the `"PetHouse"` Yarn node title, and the `$pet*` Yarn variables / echo species ids (`ice-wolf` / `flame-pup` / `aether-sprite`) stay as-is — they are cross-keyed by the catalog JSON (`"type": "PetHouse"`), CityManifest, DialogueService routing, and PetDeployer. These are never shown to the player; renaming them carries breakage risk with zero player-visible benefit. Display/lore text only was rebranded.

---

## Cross-references

- `docs/STORYLINE.md` — full Stone-Choir narrative; supersedes `docs/narrative-bible.md`.
- `docs/port-notes/dragon-boss.md` — Syndrath wave-boss combat (HP phases, dive-swoops, fire breath).
- `docs/port-notes/dragon-wave-wiring.md` — how the dragon prefab + animator are wired into the wave loop.
- `CLAUDE.md` — not present at repo root (verified 2026-05-20). Owner / agent operating instructions live in user-memory at `~/.claude/projects/<project>/memory/MEMORY.md`.
