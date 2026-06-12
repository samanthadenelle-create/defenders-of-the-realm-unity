# Master Catalog — village-npcs

Area: `Assets/_Modules/Village/NPCs/`
Assembly: **DeNelle.Village** (`Assets/_Modules/Village/DeNelle.Village.asmdef`) — namespace `DeNelle.Village`.
asmdef refs: DeNelle.Core, DeNelle.AI, DeNelle.Cosmetics, DeNelle.Data, DeNelle.Pets, DeNelle.Wallet, DeNelle.Audio, UniTask, YarnSpinner.Unity, Unity.AI.Navigation, Unity.Localization, Cinemachine, InputSystem, TextMeshPro, Lean*, URP. **No DeNelle.HUD ref** — HUD reached by reflection.

Verified by reading every `.cs` in scope plus asmdef, one animator controller, and `CompanionSpawner.CompanionClassFor`. Comments were cross-checked against code; mismatches are in FLAGS.

---

## CODE — Story companions (the recruited party-of-4)

### StoryCompanion.cs
- ns `DeNelle.Village`, asmdef DeNelle.Village. `sealed MonoBehaviour, IDamageableStructure`.
- Responsibility: one recruited companion that **follows the hero, fights** (basic attack + per-class Tier-2 ability), **speaks** via TownsfolkBubble, and is **mortal** (takes contact damage).
- Bootstrap: none (no RuntimeInitializeOnLoad) — spawned by StoryCompanionInjector. Self-registers into a static registry on `OnEnable`.
- Key public:
  - `static IReadOnlyList<StoryCompanion> Active` — O(1) join-ordered registry (lowest instance id first); read by PartyHudBridge.
  - `float Hp`, `float MaxHp` (default 120), `bool IsAlive` — party-frame bar source.
  - `void TakeDamage(float)`, `void Heal(float)` — HP model (mortal; Knight Bulwark soak reduces dmg).
  - `void Configure(HeroClass)` — set before Start; drives name + class kit.
  - `HeroClass Hero`, `string DisplayName` (→ CompanionDialogue.NameFor).
  - `void SetBubble(TownsfolkBubble)`, `TownsfolkBubble Bubble`, `void SetHero(Transform)`.
  - `void SetSpeechSuppressed(bool)` — WO-277; mutes ambient chatter while a scripted beat drives the bubble (follow+fight unaffected).
  - `IDamageableStructure.IsAlive/ApplyContactDamage` — enemy contact lane hits it via the injector's Default-layer child hitbox.
- Behaviour: `Update` → TickClassAbility → (UpdateCombat else UpdateFollow) → UpdateSpeech → DriveAnimator.
  - Combat: reads shared `TargetManager` (same list hero reticle uses); leashed to hero (`LeashFromHero 22m`); Knight melee (2.4m, no projectile), casters ranged (12m, RangedAttackVFX orb/arrow).
  - Class abilities: Cleric Mend (heal most-wounded ally — hero HeroHealth + other StoryCompanions + DeNelle.Pets.Pet), Knight Taunt+Bulwark (EnemyBrain.TauntTo), Ranger Multishot, Mage Arcane Burst (AoE via TargetManager.CollectInRange).
  - Follow: NavMeshAgent when on-mesh+in-reach, else lerp; WO-301 teleport at 28m (seam stranding).
  - On 0 HP `Fall()` → SetActive(false); respawns next hub load.
- Deps: TargetManager, Enemy/EnemyBrain/EnemyDamageable, HeroHealth, DeNelle.Pets.Pet, RangedAttackVFX, VFXManager, GameSfx, IDamageable/DamageElement (Core.Combat).
- Status: **WIRED/LIVE.** Reused for Arena defenders/attackers too (see injector SpawnDefender/SpawnAttacker).

### StoryCompanionInjector.cs
- ns `DeNelle.Village`. `sealed MonoBehaviour`, singleton `Instance`.
- Responsibility: self-bootstrapping DDOL spawner — on each **hub** scene load builds ONE code-built StoryCompanion body **per persisted party-roster member** (up to 4 classes).
- Bootstrap: `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)] Bootstrap()` → creates the GameObject. `Awake` does `Destroy(this)` dedup (not gameObject — landmine note), DDOL, hooks `SceneManager.sceneLoaded` + `GameStateService.PlayerChanged`.
- Key public:
  - `Transform CompanionTransform`, `StoryCompanion Companion` (override-class one while a beat frames it, else first spawned).
  - `void SetHeroClassOverride(HeroClass?)` — WO-277/238; forces the single companion's class + re-spawns. Static `s_heroClassOverride`.
  - `static internal GameObject SpawnDefender(HeroClass, Vector3, Transform parent)` — WO-389 Arena DEFENDER: pinned to a stationary guard-post transform (HOLDs cell, silent, no bubble).
  - `static internal GameObject SpawnAttacker(HeroClass, Vector3, Transform captain)` — WO-389 Arena ATTACKER: hero-leashed to the captain, silent.
- Spawn detail: `BuildPlaceholder` skins a real hero mesh from `Resources/Heroes/<slug>` (Knight/Ranger/Mage/Cleric) via VisualFactory.Skin (-90° LocalRotation, root motion OFF), strips embedded FBX camera/light/audio/rigidbody (DEF-275 blue-orb fix), binds per-class diffuse from `Heroes/Textures/*` (WO-310), adds NavMeshAgent + Default-layer CompanionHitbox + StoryCompanion. Capsule-tint fallback if mesh missing.
- Mapping: `CompanionClassFor` delegates to `CompanionSpawner.CompanionClassFor` (Mage→Knight, Knight→Ranger, Ranger→Cleric, Cleric→Mage) — companion never mirrors player.
- Deps: GameStateService/GameState (PartyMemberIds = HeroClass names), HubScenes, VisualFactory/SkinOptions, CompanionSpawner, TownsfolkBubble, CompanionDialogue.
- Status: **WIRED/LIVE.** Hub-gated.

### CompanionDialogue.cs
- ns `DeNelle.Village`. `static class`. Plain data twin of TownsfolkDialogue, no asmdef dep.
- Responsibility: per-hero story-companion line tables (names, 1 intro line, ~5 contextual lines each).
- Key public: `string NameFor(HeroClass)`, `string[] IntroPoolFor/PoolFor(HeroClass)`, `string IntroFor(HeroClass)`, `string LineFor(HeroClass,int)` (modulo, never throws).
- Companion identities: Knight→Grom "Veteran of the Wall"; Ranger→Sylas "Scout of the Reach"; Mage→Thrain "Keeper of the Light"; Cleric→Elara "Acolyte of the Heart". `// LOCALIZE:` constants (kept out of en.json).
- Status: **WIRED/LIVE** (read by StoryCompanion + all join beats).

---

## CODE — Join beats (recruit the roster, one at a time)

Canon join order: **Sylas (beat-1) → Elara (wave 3) → Grom (first OuterWorld return).** All three: `sealed MonoBehaviour, [DisallowMultipleComponent]`, ns DeNelle.Village, `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)] Bootstrap()`, **hub-gated** (`HubScenes.IsHub`, widened from exact "Village2"), one-shot via `GameState.SeenTutorials` + `s_ranThisSession` + `static bool ForceRun` dev override, async UniTask (never async void), self-destruct after run. Each substitutes a different free class if the canonical class clashes with the player/party (CompanionSpawner mapping). Speaker lines via TutorialDialogue driving the companion's TownsfolkBubble (ambient suppressed during).

### SylasFirstMeeting.cs (WO-238/DEF-180)
- The FIRST meeting → join (Ranger/Sylas). SeenKey `sylas_first_meeting`. Tuning: SettleSeconds 1.5, MeetRange 14, ProximityTimeout 8s.
- Flow: settle → if Ranger + Yarn node `SylasFirstMeeting` exists, **delegate to DialogueService.Play** (Yarn node IS the recruit via `<<RecruitCompanion Ranger>>` bridge) and mark seen; else use injector class override + scripted bubble lines + WaitUntilBeside.
- `ShouldRun` **stands down when `CastleCompanionIntroducerInjector.Active`** (single-trigger guarantee, owner 2026-06-12) and defers while FTUE eligible (`!Onboarded`).
- Status: **LIVE but largely superseded** by the walk-up introducer NPC for the canonical path (it no-ops when that injector is active); still the fallback for the rare player-IS-Ranger substitution.

### ElaraWaveThreeJoin.cs (party-of-4)
- SECOND join (Cleric/Elara), JoinWave 3. Trigger: lazily hooks `WaveManager.OnWaveCleared(int)` in Update. SeenKey `elara_wave3_join`.
- The JOIN = `GameStateService.AddToParty(id)` → PlayerChanged → injector spawns the body. Then runs, in the same beat: **Echo (pet) intro** (key `companion_echo_intro`, WO-360; uses GameState.PetName) and a **hero gear-up offer** (key `companion_gear_offer`, WO-364) via RunGearOffer → GearOfferChoiceUI + CompanionGearSetup.Apply.
- Status: **WIRED/LIVE.** Carries the Echo-intro + gear-up sub-beats.

### GromOuterWorldReturnJoin.cs (party-of-4)
- THIRD join (Knight/Grom), fills party to 4. SeenKey `grom_world_return_join`.
- Trigger is **geometric** (additive OuterWorld has no scene swap): latch `_venturedOut` past FarRadius 70m from origin, fire when back within HomeRadius 40m.
- JOIN = AddToParty(id) + scripted bubble lines. No gear/echo sub-beats.
- Status: **WIRED/LIVE.**

---

## CODE — Castle hub injectors + interactables (walk-up Talk NPCs)

### CastleCompanionIntroducerInjector.cs (owner 2026-06-12)
- ns DeNelle.Village. `sealed MonoBehaviour`, singleton `Instance`, **`static bool Active`**.
- Responsibility: replaces the fragile auto-FTUE companion-intro chain with a **walk-up introducer NPC**. Hub-gated DDOL; spawns ONE static People-pack body (`NPCs/NPC_Ranger_Scout`, fallbacks Tob/Merchant, capsule fallback) at fixed courtyard pos `(-4,0,-30)` past the keep exit, NavMesh-snapped.
- Bootstrap: `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`; `Awake` sets `Active=true` **before** legacy FTUE paths run (single-trigger).
- On Talk → `DialogueService.Play("SylasFirstMeeting")` (Yarn node IS intro+recruit via `<<RecruitCompanion Ranger>>`). One-shot key `castle_companion_introducer`; retires the holder after firing.
- Const `IntroNode = "SylasFirstMeeting"`. Configures the body's AmbientNPC to stand still (wander=false), disables NavMeshAgent.
- Deps: HubScenes, AmbientNPC, TownsfolkDialogue.Archetype, CompanionIntroducerInteractable, DialogueService, GameStateService.
- Status: **WIRED/LIVE — canonical companion-intro path.**

### CompanionIntroducerInteractable.cs (same file, second class)
- `sealed MonoBehaviour, [DisallowMultipleComponent]`. Slim proximity [F]/mobile-Talk; ActivateRadius 6m; fire-once `_fired`.
- `void Configure(node, label, hero, Action onPlayed)`. Registers with TalkPromptRegistry in range, suppressed during dialogue/build mode (MobileInteractButton.Suppressed). On Interact → `DialogueService.Play(_node)` once, then onPlayed.
- Status: **WIRED/LIVE.**

### CastleVendorNpcInjector.cs
- ns DeNelle.Village. `sealed MonoBehaviour`, singleton. Target scene **`MainCastle_Hall`** (exact match, NOT HubScenes).
- Responsibility: non-destructive placement of a STATIC vendor NPC at each of the 8 castle storefront markers (`NPC_<Role>_Interactable` baked by CastleHubBuilder), wired to the parameterized Yarn structure dialogue.
- Bootstrap: `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`; DDOL; idempotent per load (clears holder `CastleVendorNPCs (runtime)`).
- Role→Vendor table (`VendorFor`): Blacksmith/Forge→`forge`, Lumbermill→`lumbermill`, Windmill→`farm`, EchoHollow→`pet-house`, ArcaneTower→`arcane-tower` (no progression def — opens gracefully), Jeweler/Marketplace→`market`. Body source `Resources/NPCs/*`, height-normalized to ~1.95m, AmbientNPC Configure(wander=false), agent disabled.
- Attaches CastleNpcInteractable + `BuildingInteractable.MarkNpcCovered(structureId)` (the building defers its prompt).
- Status: **WIRED/LIVE.**

### CastleNpcInteractable.cs (same file)
- `sealed MonoBehaviour, [DisallowMultipleComponent]`. Proximity [F]/mobile-tap → `DialogueService.PlayStructure(structureId, label)`. ActivateRadius 6m, walk-away auto-close at +4m.
- `void Configure(structureId, label, hero)`. WO-416: does NOT raise the shared MobileInteractButton (removed redundant "Talk: <name>"); registers TalkPromptRegistry; desktop [F] only for nearest in-range (IsNearestInRange).
- Status: **WIRED/LIVE.**

### VillageNpcInjector.cs (DEF-91 Phase 3)
- ns DeNelle.Village. `sealed MonoBehaviour`, singleton. Target scene **`Village2`** (exact).
- Responsibility: at runtime removes baked placeholder AmbientNPCs and instantiates the 4 People-pack prefabs (Mevina/Tob/Merchant/Blacksmith) at the same positions/archetypes, no Village.unity edit. Hardcoded `Defs[]` mirrors VillageSceneBuilder.BuildTownsfolk. Height-normalize + bubble counter-scale.
- Bootstrap: `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`, DDOL, idempotent.
- Status: **LIVE but Village2 is the abandoned raid-target** (canon home = MainCastle_Hall). Fires only when Village2 is the active scene.

---

## CODE — Ambient townsfolk

### AmbientNPC.cs (Workstream D)
- ns DeNelle.Village. `sealed MonoBehaviour, [DisallowMultipleComponent]`.
- Responsibility: one ambient villager — **wanders** the NavMesh or **idles**, shows a proximity TownsfolkBubble on hero approach with hysteresis; drives Animator Speed/IsTalking. Village twin of the dungeon's Bryn.
- Key public: `void Configure(Archetype, bool wander, Vector3 homeAnchor)`, `void SetHero(Transform)`, `void SetBubble(TownsfolkBubble)`, `void SetReducedMotion(bool)`, `bool Speaking`.
- Detail: NavMeshAgent disabled if not wandering / no mesh (stands ground); idle Y-sway (per-NPC seed phase); WO-29 white-pill tint safety net; WO-163 caches `_hasSpeedParam`/`_hasTalkingParam` so it never spams 3,351 errors driving an absent param.
- Added to Village scene by VillageSceneBuilder via reflection (SerializedObject + Configure).
- Status: **WIRED/LIVE** (used by VillageNpcInjector + both castle injectors for static bodies).

### TownsfolkDialogue.cs (Workstream D)
- ns DeNelle.Village. `static class`. No asmdef dep.
- Responsibility: ambient flavour-line table. **enum Archetype** (STABLE values, do not renumber): Trader=0, Villager=1, Guard=2, Child=3, Elder=4, Blacksmith=5, Quartermaster=6, Archmage=7, Farmer=8.
- Named wardens (WO-116): Brunhild the Smith, Aldric Quartermaster, Archmage Sela, Goodman Harrow.
- Key public: `string NameFor(Archetype)`, `string[] PoolFor(Archetype)`, `string LineFor(Archetype,int)`, `int ArchetypeCount`. `// LOCALIZE:` constants.
- Status: **WIRED/LIVE.**

### TownsfolkBubble.cs (Workstream D)
- ns DeNelle.Village. `sealed MonoBehaviour, [DisallowMultipleComponent]`.
- Responsibility: self-building, billboarded world-space speech bubble (TextMesh-on-quads + tail; no UGUI/UXML). DeNelle.Village twin of WandererBubble (module isolation — no Dungeons dep).
- Key public: `void Show(speakerName, line)`, `void Hide()`, `bool IsVisible`.
- Detail: builds panel/outline/tail/text in Awake; rounded SDF shader `DeNelle/UI/RoundedChatBubble` (flat fallback); single global active bubble (`s_activeBubble` — steals slot); auto-hide after `_autoHideSeconds` 4.5 (owner playtest 2026-06-03); whole hierarchy on Ignore-Raycast layer 2 (DEF-151 camera-catch fix); manual word-wrap.
- Status: **WIRED/LIVE.**

### TownsfolkController.cs (Workstream D)
- ns DeNelle.Village. `sealed MonoBehaviour, [DisallowMultipleComponent]`.
- Responsibility: scene-level coordinator — pushes the Keeper transform + reduced-motion to every child AmbientNPC. NOT required (each NPC self-resolves).
- Key public: `void SetHero(Transform)`, `void SetReducedMotion(bool)`, `int TownsfolkCount`.
- Added by VillageSceneBuilder by reflection to the "Townsfolk" sub-root. Tied to Village.unity / Village2 town authoring.
- Status: **LIVE but tied to the abandoned hand-authored Village** — castle injectors don't use it (they Configure NPCs directly).

---

## CODE — HUD bridges + talk registry (reflection seams; Village→HUD asmdef-isolated)

### TalkPromptRegistry.cs
- ns DeNelle.Village. `static class`. Village-internal.
- Responsibility: O(1) self-registering list of talkable NPCs currently in range (NPCs register from their existing in-range check — no new proximity scan; OuterWorld-leak lesson).
- Key public: `int Count`, `void Register(Transform, Action)`, `void Deregister(Transform)`, `Action NearestTalk(Vector3 from)`.
- Status: **WIRED/LIVE** (written by Castle*Interactable, read by TalkHudBridge).

### TalkHudBridge.cs
- ns DeNelle.Village. `sealed MonoBehaviour`. Bootstrap `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` + DDOL.
- Responsibility: gates the HUD Talk button on `TalkPromptRegistry.Count > 0` (edge-triggered push, 0.25s poll) and routes a Talk press to `NearestTalk(hero)`. Reaches VillageHudController.`SetTalkAvailable(bool)` + `TalkRequested` UnityEvent **by reflection** (Talk not on IVillageHud — Core stays clean). MaxResolveAttempts 240 then gives up.
- Status: **WIRED/LIVE.**

### PartyHudBridge.cs
- ns DeNelle.Village. `sealed MonoBehaviour`. Bootstrap `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` + DDOL.
- Responsibility: fills HUD party slots 1..3 from `StoryCompanion.Active` (WO-403 — O(1) registry, replaced the per-0.5s FindObjectsByType OuterWorld-leak suspect). Roster-gated by `GameState.PartySize` (WO-301). Pushes real Hp/MaxHp. Resolves HUD via `CoreServices.Hud`, calls `SetPartyMember(int,string,float,float)` + `SetPartyMemberVisible(int,bool)` **by reflection**.
- Status: **WIRED/LIVE.** (Header comment is stale — see FLAGS.)

---

## CODE — Gear-up sub-beat (WO-364, rides ElaraWaveThreeJoin)

### CompanionGearSetup.cs
- ns DeNelle.Village. `static class`.
- Responsibility: gear-by-class grant. `struct GearGrant {WeaponId,ArmorId,WeaponLabel,ArmorLabel}`. `GearGrant GrantFor(HeroClass)` (Knight=knight_iron/armor_plate, Ranger=ranger_starter/armor_leather, Mage=mage_oak/armor_cloth, default/Cleric=knight_starter/armor_leather). `GearGrant Apply(HeroClass)` resolves the hero's GearLoadout (Player-tagged, lazily adds), equips weapon+armor (ignores level req — story grant), VFX/SFX + GearGrantToast. Null-guarded.
- Status: **WIRED/LIVE.**

### GearGrantToast.cs
- ns DeNelle.Village. `sealed MonoBehaviour, [DisallowMultipleComponent]`.
- Responsibility: non-blocking top-centre "+<Armor>/+<Weapon>" toast. Code-built uGUI on own ScreenSpaceOverlay canvas (NOT UXML — WebGL landmine), auto-dismiss 4s + fade. `static void Show(armorLabel, weaponLabel=null)`. blocksRaycasts=false.
- Status: **WIRED/LIVE.**

### GearOfferChoiceUI.cs
- ns DeNelle.Village. `sealed MonoBehaviour, [DisallowMultipleComponent]`.
- Responsibility: two-button "outfit me?" choice (Visit the forge / I'm already equipped). Code-built uGUI, manual tap hit-test (no EventSystem). `static void Show(Action<bool> onChosen)` — true=forge, false=in-place; **both auto-equip in place** (forge walk is flavour only). MinHold 0.4s, AutoChoose 12s failsafe → false.
- Status: **WIRED/LIVE.**

---

## ASSETS (Animators / Materials)

`NPCs/Animators/*.controller` (AC_AmbientNPC_Mevina, AC_AmbientNPC_Tob, AC_Blacksmith, AC_Merchant): ambient-NPC locomotion controllers.
- Verified `AC_AmbientNPC_Tob`: params **Speed** (float) + **IsTalking** (bool); states Idle/Walk (Speed thresholds 0.1/0.05) + Talk (AnyState on IsTalking). Matches AmbientNPC's SpeedHash/TalkingHash exactly. Default = Idle.
`NPCs/Materials/*.mat` (MAT_Blacksmith[_Anvil/_Hammer], MAT_Merchant, MAT_Peasant_Mevina, MAT_Peasant_Tob): townsfolk body/prop materials.
Note: the People-pack body **prefabs** the injectors load (`Resources/NPCs/NPC_*`) and hero meshes (`Resources/Heroes/*`) live under Resources, not in this folder; gitignored Models on fresh clone → capsule fallbacks fire.

---

## FLAGS

### Stale comment vs. code
- **PartyHudBridge.cs header (lines 14-17)**: "Companions currently have no health (they are immortal support units)... HP bar is a PLACEHOLDER full bar." **FALSE now** — StoryCompanion is mortal; the Update body reads real `c.Hp`/`c.MaxHp` (lines 91-99 say so explicitly). Header contradicts its own code; update it.
- **"the project defines no 'Player' tag" comment, repeated** in StoryCompanion (line 24-25, 712-714), StoryCompanionInjector (611), AmbientNPC (444-446), TownsfolkController (45-47), VillageNpcInjector (160-161), SylasFirstMeeting (331-332). But CastleVendorNpcInjector (276-281), CastleCompanionIntroducerInjector (270-272, 355-357), CastleNpcInteractable (394-397), TalkHudBridge (111), CompanionGearSetup (147) all call `GameObject.FindWithTag("Player")` as the **primary** lookup. A "Player" tag clearly exists now (CLAUDE.md §7 confirms hero is tagged `Player`). The older name-based files carry an out-of-date "no Player tag" rationale — same comment-vs-code mismatch class as the HeroLocomotion example. Inconsistent hero-resolution strategy across the area (tag-first vs name-first); harmless but worth unifying.
- **StoryCompanionInjector.BuildPlaceholder (lines 384-389)**: comment "No speech bubble on the story companion (owner: remove his text bubble)... so SetBubble simply no-ops now." Yet StoryCompanion.SetBubble + UpdateSpeech are fully live, the injector's `SpawnOne` calls `comp.SetBubble(bubble)` (line 203), and join beats drive `companion.Bubble`. The placeholder build no longer attaches a bubble, but the comment overstates ("simply no-ops") — bubbles ARE driven on companions via the join beats' TutorialDialogue and (for non-suppressed companions) ambient chatter. Mildly contradictory; verify intended behaviour.

### Dead / superseded / duplicate
- **SylasFirstMeeting.cs** is effectively superseded for the canonical path: it stands down whenever `CastleCompanionIntroducerInjector.Active` (always true in the hub once that injector exists). It survives only as the player-IS-Ranger substitution fallback + a legacy auto path. Three historical autostart paths (TutorialDirector fast-path hook, CompanionMeetingTrigger, this beat) were collapsed into the walk-up NPC — confirm the other two are also gated off `Active`.
- **TownsfolkController.cs** is alive but bound to the hand-authored Village/Village2 town (added by VillageSceneBuilder). The castle hub injectors configure NPCs directly and never use it → unused in the canonical MainCastle_Hall path.

### Scene-gated / disabled
- **VillageNpcInjector** gates on exact scene `Village2` — the **abandoned** raid-target/old town (memory: Village2 canonical-vs-Village.unity, but castle is the home hub). It does not fire in MainCastle_Hall. The 4 People-pack townsfolk only appear in Village2.
- **CastleVendorNpcInjector** gates on exact `MainCastle_Hall` (not HubScenes) — won't populate vendors in any other hub variant.
- Join beats + CastleCompanionIntroducerInjector use `HubScenes.IsHub` (broader) — note the inconsistency: vendors/village townsfolk use exact-scene matching while companions/intro use the hub list. A new hub scene would get companions but not vendors.

### Broken / contradictory
- **CastleVendorNpcInjector** maps ArcaneTower→`arcane-tower` which has **no ResourceBuildingProgression def + no portrait** — documented as "safe" (StructureMenu opens gracefully), but it's a known data gap (Talk works, no yield/portrait). Confirmed by the source comment, not a crash.
- **StoryCompanion.TryClericMend** still scans `FindObjectsByType<StoryCompanion>` and `<DeNelle.Pets.Pet>` every cast (lines 464, 479) despite the WO-403 push to a registry for the HUD — per-cast allocation in the heal path (perf, given the OuterWorld-leak history). Not broken, but counter to the area's own hardening direction.
