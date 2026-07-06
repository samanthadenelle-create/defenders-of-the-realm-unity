// =============================================================================
// BattleController — wires the pure-C# ATB engine to scene + UI
// -----------------------------------------------------------------------------
// C# port of src/modules/battle-atb/BattleATB.tsx (the AtbBattleScreen +
// useAtbBattleLoop pair). A MonoBehaviour that lives in ATBBattle.unity and
// owns the bridge between three layers:
//
//   1. The pure engine     — DeNelle.BattleATB.Engine.* (math, no rendering).
//   2. The runtime state   — ATBRuntimeState (the ScriptableObject "store").
//   3. The scene + the UI  — placeholder capsule combatants + the BattleHUD
//                            UI Toolkit document.
//
// NO combat logic lives here. Every mutation routes through ATBRuntimeState,
// which in turn routes through the engine. This controller only:
//   • builds a BattleSetup from SceneRouter.PendingBattle (or a dev fallback),
//   • subscribes to the runtime state's UnityEvents,
//   • re-renders the HUD (ATB / HP bars, log) and the capsules on every change,
//   • forwards the "Attack" button to ATBRuntimeState.ChooseAction.
//
// Week-2 placeholder scope (v2 port-spec Part 5): one hero capsule, one enemy
// capsule, ATB bars in UI Toolkit, an Attack button that submits an action,
// the engine resolves the turn, the battle log scrolls. Multi-combatant
// portraits / abilities / targeting come with the full Week-2+ HUD port.
//
// Async (port-spec mandate): the post-battle hand-back uses `async UniTask`,
// never `async void`. The fire-and-forget call site uses .Forget().
// =============================================================================

using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DeNelle.BattleATB.Engine;
using DeNelle.BattleATB.State;
using DeNelle.Core;
using DeNelle.Core.Combat;  // ActorAnimator / IActorAnimator for 3D fight anims (knight swings, mage casts, hits, deaths)
using TMPro;
using UnityEngine;

namespace DeNelle.BattleATB
{
    /// <summary>
    /// Scene controller for <c>ATBBattle.unity</c>. Bridges the pure engine, the
    /// <see cref="ATBRuntimeState"/> store, the placeholder capsule combatants
    /// and the <c>BattleHUD</c> UI Toolkit document.
    /// </summary>
    public sealed class BattleController : MonoBehaviour
    {
        // ---------------------------------------------------------------------
        // Inspector wiring — set by BattleSceneBuilder (reflection) or by hand
        // ---------------------------------------------------------------------

        [Header("Runtime state")]
        [Tooltip("The runtime-only ScriptableObject store. Created by the scene builder.")]
        [SerializeField] private ATBRuntimeState _runtimeState;

        // NOTE: Old UIDocument + complex VisualElement BattleHud removed.
        // We now use a clean code-built uGUI FF7-style HUD (BattleHudUgui) that is self-contained
        // and matches the requested classic layout. No UXML — guaranteed to work in builds.

        // WO-163: cached "Attack" trigger hash — guards SubmitPlayerAction's swing
        // so it never drives an absent param (the placeholder capsule has none).
        private static readonly int BattleControllerAttackHash = Animator.StringToHash("Attack");

        [Header("Placeholder combatants (capsule meshes)")]
        [Tooltip("Transform of the hero capsule.")]
        [SerializeField] private Transform _heroCapsule;

        [Tooltip("Transform of the enemy capsule.")]
        [SerializeField] private Transform _enemyCapsule;

        [Header("Battle setup")]
        [Tooltip("Seed used when no BattleParams were handed off (dev / direct play).")]
        [SerializeField] private int _fallbackSeed = 42;

        [Tooltip("Enemy def id for the dev fallback battle. Must be a key of ENEMY_DEFS.")]
        [SerializeField] private string _fallbackEnemyDefId = "skeleton";

        [Tooltip("Hero display name for the dev fallback battle.")]
        [SerializeField] private string _fallbackHeroName = "Blaise";

        [Tooltip("Seconds to linger on the result before returning to the previous scene.")]
        [SerializeField] private float _returnDelaySeconds = 2.5f;

        // ---------------------------------------------------------------------
        // Dynamic HUD — WO-169 P1 code-built party battle screen
        // ---------------------------------------------------------------------

        /// <summary>FF7-style code-built uGUI HUD (classic command window bottom-left, party status bottom-right with 4 slots + ATB bars, info top).
        /// Replaces the previous overly complex VisualElement BattleHud. Pure uGUI so it works in builds. Wired to the same ATB engine/state.</summary>
        private BattleHudUgui _hudUgui;

        /// <summary>True once the result hand-back has been kicked off (fire once).</summary>
        private bool _returnScheduled;
        private int _playerAttackCombo;  // for cycling Knight melee combo swings in PlayAttack
        // Cursor into the cumulative battle log — TryDriveHitAndDeathAnims / SpawnFloatingDamage
        // only process entries ADDED since the last turn. Without it both methods replayed the
        // entire log every TurnResolved (re-spawning damage numbers + re-firing hit/death anims
        // for all past hits). Reset to 0 on battle start. (Replaces the old BattleVfx log-diff cursor.)
        private int _lastProcessedLogIndex;

        /// <summary>Where this battle came from — a village tree-breach "Last Stand"
        /// (Village) vs a dungeon encounter (Dungeon). Gates Flee: you cannot flee
        /// the Last Stand (owner). Defaults to Dungeon so the dev/direct-play path
        /// stays fully testable (Flee available).</summary>
        private BattleSource _source = BattleSource.Dungeon;

        // ---------------------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------------------

        private bool _subscribed;   // runtime-state events wired

        private void OnDisable()
        {
            if (_runtimeState != null)
            {
                _runtimeState.OnBattleChanged.RemoveListener(HandleBattleChanged);
                _runtimeState.OnActionSubmitted.RemoveListener(HandleActionSubmitted);
                _runtimeState.OnTurnResolved.RemoveListener(HandleTurnResolved);
                _runtimeState.OnOutcome.RemoveListener(HandleOutcome);
            }
            // WO-93: drop the idle-timeout listener so a re-entered scene doesn't double-wire.
            var mgr = ATBCombatManager.Instance;
            if (mgr != null) mgr.onEnemyAutoAttack.RemoveListener(HandleIdleTimeout);
            _subscribed = false;
        }

        private void Start()
        {
            Subscribe();   // wire events

            if (_runtimeState == null)
            {
                Debug.LogError("[BattleController] No ATBRuntimeState assigned — battle cannot run.");
                return;
            }
            _returnScheduled = false;
            _lastProcessedLogIndex = 0;   // fresh battle — start the log cursor at the top

            BattleSetup setup = BuildSetup();
            // Tag the source from the handoff: Wave > 0 is a village tree-breach
            // "Last Stand" (Heart consequences commit on close); Wave == 0 or no
            // handoff (dungeon / dev) is a Dungeon encounter (no village damage).
            _source = ResolveSource();
            _runtimeState.StartBattle(setup, _source);

            // Build the clean FF7 uGUI HUD (code-built Canvas + panels exactly as specified).
            // Self-contained — creates BattleHUD_Canvas if needed. No UIDocument / UXML.
            var hudGo = new GameObject("BattleHudUgui");
            _hudUgui = hudGo.AddComponent<BattleHudUgui>();
            _hudUgui.Build(); // creates its own ScreenSpaceOverlay Canvas + Scaler (1080x1920 ref)

            // Wire callbacks (same surface the engine/state expect).
            // WO-93 fix: route through SubmitPlayerAction (the WO-169 single entry
            // point) instead of calling _runtimeState.ChooseAction directly. The
            // direct call bypassed ATBCombatManager.OnPlayerActed(), so the idle
            // "you turtled" timeout could fire right after the player acted.
            // SubmitPlayerAction is a strict superset: it guards on IsAwaitingPlayer()
            // (closing a stale-click hole), plays the hero swing anim, resets the idle
            // timer via OnPlayerActed(), then calls the same ChooseAction(action).
            _hudUgui.OnAction = action => SubmitPlayerAction(action);

            // Initial paint of the FF7 layout.
            _hudUgui.Render(_runtimeState);

            // WO-68: arm the turn-timer now that the battle is live.
            // WO-93: wire the previously-dangling idle-timeout event so it actually
            // does something. When the player idles past maxTurnTime, force a Defend
            // for the active hero (a fair "you turtled" consequence) routed through the
            // same engine path as a normal click — instead of the event firing into the
            // void. AddListener is idempotent-safe here because Start runs once per scene.
            var mgr = ATBCombatManager.Instance;
            if (mgr != null)
            {
                mgr.onEnemyAutoAttack.RemoveListener(HandleIdleTimeout); // guard double-wire
                mgr.onEnemyAutoAttack.AddListener(HandleIdleTimeout);
                mgr.StartCombat();
            }
        }

        /// <summary>
        /// WO-93 — the idle turn-timer expired. Apply the punitive default: force the
        /// active player to Defend (turtle) so idling has a real, fair consequence and
        /// the battle never stalls waiting on the player. No-op if the engine isn't
        /// awaiting a player action (AI turn / battle over). Routes through the same
        /// ChooseAction path a HUD click would, and resets the idle timer for the next turn.
        /// </summary>
        private void HandleIdleTimeout()
        {
            if (_runtimeState == null || !_runtimeState.IsAwaitingPlayer()) return;
            _runtimeState.ChooseAction(BattleAction.MakeDefend());
            ATBCombatManager.Instance?.OnPlayerActed(); // re-arm for the next player turn
        }

        /// <summary>
        /// DEF-259 #1 — drive the cosmetic ATB charge. The engine resolves turn order
        /// discretely (it never fills bars over time), so without this the gauges read
        /// as frozen. This animates the displayed bars only; it never calls the engine.
        /// </summary>
        private void Update()
        {
            if (_hudUgui == null || _runtimeState == null) return;
            if (_runtimeState.Battle == null) return;
            _hudUgui.TickVisualAtb(_runtimeState, Time.deltaTime);
        }

        /// <summary>Wires the runtime-state events. Idempotent.</summary>
        private void Subscribe()
        {
            if (_subscribed || _runtimeState == null) return;
            _runtimeState.OnBattleChanged.AddListener(HandleBattleChanged);
            _runtimeState.OnActionSubmitted.AddListener(HandleActionSubmitted);
            _runtimeState.OnTurnResolved.AddListener(HandleTurnResolved);
            _runtimeState.OnOutcome.AddListener(HandleOutcome);
            _subscribed = true;
        }

        // ---------------------------------------------------------------------
        // Setup construction — port of BattleATB.tsx's setup memo
        // ---------------------------------------------------------------------

        /// <summary>
        /// Build a <see cref="BattleSetup"/> from <see cref="SceneRouter.PendingBattle"/>
        /// when present, else a single-enemy dev fallback so the scene plays when
        /// opened directly in the editor.
        /// </summary>
        private BattleSetup BuildSetup()
        {
            BattleParams handoff = SceneRouter.PendingBattle;

            int wave = handoff != null && handoff.Wave > 0 ? handoff.Wave : 1;
            int seed = _fallbackSeed;

            // BUG-009: build the real breach roster — one enemy per breaching id,
            // each mapped to a valid engine def (was a single inspector-fallback
            // enemy regardless of who actually breached).
            var enemies = BuildEnemyRoster(handoff);

            HeroClass heroClass = ResolveHeroClass(); // owner: ATB ran as Mage even when you're an Archer
            string heroName = ResolveHeroName(heroClass); // DEF-259 #7: was always "Blaise"

            // WO-169 P0 — surface a REAL party of up to 4 (hero + the player's pets),
            // each with its own per-member control mode. Was Pets = empty → forced 1v1.
            List<PartyMemberSpec> party = BuildParty(heroClass, heroName);

            return new BattleSetup
            {
                Wave = wave,
                Seed = seed,
                // Multi-member party is authoritative; the legacy HeroClass/Pets
                // fields are left as a harmless fallback for any path that ignores
                // PartyMembers (and they keep the engine's legacy branch buildable).
                PartyMembers = party,
                HeroClass = heroClass,
                HeroName = heroName,
                Pets = new List<PartyPetSpec>(),
                Enemies = enemies,
                Inventory = BuildInventory(),
                Reinforcements = false,
            };
        }

        /// <summary>
        /// WO-169 P0 — assemble the playable party from live game state, capped at 4
        /// at the controller/setup level (NOT by touching the engine MAX_PARTY).
        /// Member 0 is the Keeper/hero (defaults to Player control); up to 3 of the
        /// player's pets follow (default AI). Each member's stored control-mode
        /// preference (AtbControlModeStore) wins over the default once set, and is
        /// seeded on first join so it persists.
        /// </summary>
        private List<PartyMemberSpec> BuildParty(HeroClass heroClass, string heroName)
        {
            const int MaxParty = 4; // controller-level cap; engine MAX_PARTY untouched
            var party = new List<PartyMemberSpec>();

            // Hero / Keeper — id "hero", default Player.
            party.Add(MakeMember(
                id: "hero",
                name: heroName,
                heroClass: heroClass,
                species: null,
                bondRank: 0,
                aiMode: PetAiMode.Balanced,
                defaultMode: ControlMode.Player));

            // PIVOT (owner 2026-06-22): single-hero combat. When ff.singlehero is ON (default), the
            // party is JUST the hero — no pets/companions surfaced (companions were a net negative;
            // direction is one hero + a wider skill tree). Flag OFF restores the hero+pets party below.
            if (!DeNelle.Core.FeatureFlags.SingleHero)
            {
                // The player's collected pets → companion members (default AI).
                var svc = DeNelle.Core.State.GameStateService.Instance;
                var state = (svc != null) ? svc.State : null;
                if (state != null && state.Pets != null)
                {
                    int idx = 0;
                    foreach (var pet in state.Pets)
                    {
                        if (party.Count >= MaxParty) break;
                        PetSpecies species = MapPetSpecies(pet.Species);
                        int bond = BondRankFor(state, pet.Species);
                        string name = !string.IsNullOrEmpty(pet.Nickname) ? pet.Nickname : DefaultPetName(species);
                        party.Add(MakeMember(
                            id: $"pet-{idx}",
                            name: name,
                            heroClass: null,
                            species: species,
                            bondRank: bond,
                            aiMode: PetAiMode.Balanced,
                            defaultMode: ControlMode.AI));
                        idx++;
                    }
                }
            }

            Debug.Log($"[BattleController] ATB party surfaced: {party.Count} member(s) " +
                      $"(hero {heroClass} + {party.Count - 1} pet(s)).");
            return party;
        }

        /// <summary>Build one member, applying the persisted control-mode preference
        /// (seeding the default on first join so the choice sticks).</summary>
        private static PartyMemberSpec MakeMember(
            string id, string name, HeroClass? heroClass, PetSpecies? species,
            int bondRank, PetAiMode aiMode, ControlMode defaultMode)
        {
            if (!AtbControlModeStore.HasPreference(id))
                AtbControlModeStore.Set(id, defaultMode);
            ControlMode mode = AtbControlModeStore.Get(id, defaultMode);
            return new PartyMemberSpec
            {
                Id = id,
                Name = name,
                HeroClass = heroClass,
                Species = species,
                BondRank = bondRank,
                AiMode = aiMode,
                ControlMode = mode,
            };
        }

        /// <summary>Map the Core pet-species enum onto the engine's. Same three
        /// species, same order — a 1:1 projection.</summary>
        private static PetSpecies MapPetSpecies(DeNelle.Core.State.PetSpecies s)
        {
            switch (s)
            {
                case DeNelle.Core.State.PetSpecies.AetherSprite: return PetSpecies.AetherSprite;
                case DeNelle.Core.State.PetSpecies.FlamePup: return PetSpecies.FlamePup;
                case DeNelle.Core.State.PetSpecies.IceWolf: return PetSpecies.IceWolf;
                default: return PetSpecies.AetherSprite;
            }
        }

        /// <summary>Bond rank for a species from GameState.PetBonds [Aether,Flame,Ice].</summary>
        private static int BondRankFor(DeNelle.Core.State.GameState state, DeNelle.Core.State.PetSpecies s)
        {
            if (state.PetBonds == null) return 0;
            int i = (int)s; // enum order matches the [Aether, Flame, Ice] bond list
            return (i >= 0 && i < state.PetBonds.Count) ? Mathf.Clamp(state.PetBonds[i], 0, 4) : 0;
        }

        private static string DefaultPetName(PetSpecies s)
        {
            switch (s)
            {
                case PetSpecies.FlamePup: return "Flame Pup";
                case PetSpecies.IceWolf: return "Ice Wolf";
                default: return "Aether Sprite";
            }
        }

        /// <summary>The carried item inventory. Reads the player's ATB consumables
        /// from GameState; seeds a few potions if the pouch is empty so Item is
        /// usable in dev/direct-play (preserves the old behaviour).</summary>
        private static Dictionary<ItemKind, int> BuildInventory()
        {
            var inv = new Dictionary<ItemKind, int>();
            var svc = DeNelle.Core.State.GameStateService.Instance;
            var state = (svc != null) ? svc.State : null;
            if (state != null)
            {
                inv[ItemKind.Potion] = Mathf.Max(0, state.Inventory.Potions);
                inv[ItemKind.ManaCrystal] = Mathf.Max(0, state.Inventory.ManaCrystals);
                inv[ItemKind.Cleanse] = Mathf.Max(0, state.Inventory.Cleanses);
            }
            int total = 0;
            foreach (var kv in inv) total += kv.Value;
            if (total == 0) inv[ItemKind.Potion] = 3; // keep Item usable in dev
            return inv;
        }

        /// <summary>
        /// The hero's class for this battle, read from the live GameState so the ATB
        /// hero matches the class the player chose (owner: "drops into the stub with
        /// Mage — started as Archer"). GameState carries a Core HeroClassOpt; map it
        /// to the engine HeroClass. Falls back to Mage when there is no save / None.
        /// </summary>
        private static HeroClass ResolveHeroClass()
        {
            var svc = DeNelle.Core.State.GameStateService.Instance;
            var opt = (svc != null && svc.State != null)
                ? svc.State.HeroClass
                : DeNelle.Core.State.HeroClassOpt.None;
            HeroClass cls;
            switch (opt)
            {
                case DeNelle.Core.State.HeroClassOpt.Knight: cls = HeroClass.Knight; break;
                case DeNelle.Core.State.HeroClassOpt.Ranger: cls = HeroClass.Ranger; break;
                case DeNelle.Core.State.HeroClassOpt.Mage:   cls = HeroClass.Mage;   break;
                // WO-226: the Cleric is a caster — it reuses the Mage archetype in the
                // ATB engine (stats/abilities) until it gets its own kit. Mapping it
                // here keeps the engine HeroClass enum 3-valued so HERO_STATS /
                // HERO_ABILITIES lookups never miss a key.
                case DeNelle.Core.State.HeroClassOpt.Cleric: cls = HeroClass.Mage;   break;
                default:                                     cls = HeroClass.Mage;   break; // no save / None
            }
            Debug.Log($"[BattleController] ATB hero class resolved to {cls} (GameState={opt}).");
            return cls;
        }

        /// <summary>
        /// DEF-259 #7 — the ATB hero name was hard-wired to "Blaise" regardless of the
        /// chosen hero. GameState carries no free-text hero name, so derive the canon
        /// roster name from the selected class (matches HeroSelect / BattleHud portraits:
        /// Mage→Thrain, Knight→Grom, Ranger→Sylas, Cleric→Elara). Falls back to the
        /// inspector dev name only when there is no selection at all.
        /// </summary>
        private string ResolveHeroName(HeroClass heroClass)
        {
            var svc = DeNelle.Core.State.GameStateService.Instance;
            var opt = (svc != null && svc.State != null)
                ? svc.State.HeroClass
                : DeNelle.Core.State.HeroClassOpt.None;
            switch (opt)
            {
                case DeNelle.Core.State.HeroClassOpt.Mage:   return "Thrain";
                case DeNelle.Core.State.HeroClassOpt.Knight: return "Grom";
                case DeNelle.Core.State.HeroClassOpt.Ranger: return "Sylas";
                case DeNelle.Core.State.HeroClassOpt.Cleric: return "Elara";
                default:
                    // No save / None — keep the dev fallback so direct-play still names someone.
                    return string.IsNullOrEmpty(_fallbackHeroName) ? "Hero" : _fallbackHeroName;
            }
        }

        /// <summary>
        /// BUG-009: builds the ATB enemy roster from the breach handoff — one
        /// <see cref="BreachEnemySpec"/> per breaching id, each mapped to a valid
        /// <see cref="Defs.ENEMY_DEFS"/> key, capped so a huge breach stays a
        /// playable battle. WaveManager now passes engine def ids (Enemy.EngineDefId)
        /// in <see cref="BattleParams.BreachedIds"/>, so the roster matches who
        /// actually breached. Empty / no handoff (dev / direct play) → one fallback.
        /// </summary>
        private List<BreachEnemySpec> BuildEnemyRoster(BattleParams handoff)
        {
            const int MaxEnemies = 6;
            var roster = new List<BreachEnemySpec>();

            string[] ids = handoff != null ? handoff.BreachedIds : null;
            if (ids != null)
            {
                foreach (string raw in ids)
                {
                    if (roster.Count >= MaxEnemies) break;
                    roster.Add(new BreachEnemySpec { DefId = MapToEngineDef(raw) });
                }
            }

            if (roster.Count == 0)
            {
                // WO-481 combat-pivot: the V1 prototype encounter is the Orc FAMILY (leader +
                // followers), not a lone skeleton. With no breach handoff (dev / direct ATBBattle
                // play) stage the family so the battle anchor shows the real V1 fight. The swapper
                // reads the SAME list (AtbPrototypeEncounter.OrcFamily) so engine roster ↔ visuals match.
                foreach (var id in AtbPrototypeEncounter.OrcFamily)
                {
                    if (roster.Count >= MaxEnemies) break;
                    roster.Add(new BreachEnemySpec { DefId = MapToEngineDef(id) });
                }
            }
            return roster;
        }

        /// <summary>
        /// Maps a breached-enemy id onto a valid <see cref="Defs.ENEMY_DEFS"/> key.
        /// Exact engine keys (e.g. "skeleton", "necromancer") pass straight through;
        /// village enemies.json ids ("hollow-walker", "hollow-warrior", "hollow-rogue")
        /// are mapped to the closest ATB archetype (WO-94); anything unknown falls
        /// back to "skeleton" so the battle always has a valid roster.
        /// </summary>
        private static string MapToEngineDef(string id)
        {
            if (string.IsNullOrEmpty(id)) return "skeleton";
            if (Defs.ENEMY_DEFS.ContainsKey(id)) return id;          // already a valid engine key

            string lower = id.ToLowerInvariant();

            // WO-94: village enemies.json ids — map to ATB engine archetypes.
            if (lower == "hollow-warrior") return "hollow-warrior"; // standard melee (not bruiser tank)
            if (lower == "hollow-walker")  return "skeleton";    // standard grunt
            if (lower == "hollow-rogue")   return "skeleton";    // fast skirmisher → grunt fallback

            // Generic heuristics for dungeon / other 3D-layer tokens.
            if (lower.Contains("necro"))   return "necromancer";
            if (lower.Contains("warrior") || lower.Contains("tank") || lower.Contains("bruiser")
                || lower.Contains("bulwark") || lower.Contains("boss")
                || lower.Contains("dragon")) return "bruiser";
            if (lower.Contains("goblin"))  return "goblin";
            return "skeleton";
        }

        /// <summary>
        /// The battle's source. A handoff with <c>Wave &gt; 0</c> is a village
        /// tree-breach "Last Stand" (<see cref="BattleSource.Village"/>); <c>Wave == 0</c>
        /// or no handoff (dungeon encounter / dev direct-play) is a
        /// <see cref="BattleSource.Dungeon"/>. Gates Flee — you cannot abandon the
        /// Last Stand, but a dungeon encounter is a clean escape.
        /// </summary>
        private static BattleSource ResolveSource()
        {
            BattleParams h = SceneRouter.PendingBattle;
            return (h != null && h.Wave > 0) ? BattleSource.Village : BattleSource.Dungeon;
        }

        // ---------------------------------------------------------------------
        // Event handlers — driven by ATBRuntimeState's UnityEvents
        // ---------------------------------------------------------------------

        /// <summary>Re-render the FF7 uGUI HUD whenever the live snapshot changes.</summary>
        private void HandleBattleChanged(BattleState state)
        {
            _hudUgui?.Render(_runtimeState);
        }

        /// <summary>
        /// The player just submitted an action. Drive the uGUI command feedback + 3D anims.
        /// </summary>
        private void HandleActionSubmitted(BattleState state)
        {
            _hudUgui?.Render(_runtimeState);

            // Drive 3D ActorAnimator on the swapped models for real fight feel:
            // - Knight PlayAttack(combo) → sword swings from the Knight action clips.
            // - Mage PlayCast() → Wizard_Spell_Cast animation.
            // - Enemy gets a wind-up/attack pose.
            // This uses the canonical IActorAnimator already wired on the capsules by AtbCombatantSwapper.
            TryDriveActionAnim(state);
        }

        /// <summary>
        /// A turn fully resolved. Update the uGUI HUD + drive 3D hit/death reactions.
        /// </summary>
        private void HandleTurnResolved(BattleState state)
        {
            _hudUgui?.Render(_runtimeState);

            // Only react to log entries ADDED since the last turn (cursor) — the log is
            // cumulative, so looping the whole thing would replay every past hit/death each turn.
            int from = _lastProcessedLogIndex;

            // On resolution, drive hit reactions and death falls on the 3D models.
            // Every direct hit → PlayHit (flinch / impact anim from Shared_Hit_Reaction).
            // Death → Die (latches Dead + fall animation from Shared_Death, immersive collapse).
            TryDriveHitAndDeathAnims(state, from);

            // Simple world-space floating damage numbers above the capsules (3D field).
            // Uses the same capsules the swapper populated. Keeps the central arena clean.
            SpawnFloatingDamage(state, from);

            // Advance the cursor past everything we just processed.
            _lastProcessedLogIndex = state.Log?.Count ?? _lastProcessedLogIndex;
        }

        /// <summary>Battle reached a final outcome — schedule the scene hand-back.</summary>
        private void HandleOutcome(AtbBattleResult result)
        {
            if (result == null || _returnScheduled) return;
            _returnScheduled = true;
            ATBCombatManager.Instance?.StopCombat(); // WO-68: halt the turn timer
            ReturnAfterResult(result).Forget();
        }

        /// <summary>
        /// WO-169 — single entry point for every player action the HUD commits
        /// (attack / ability / item / defend). Routes through ATBRuntimeState, which
        /// owns the engine call + clone-at-boundary. Guards on the awaiting-player
        /// gate so a stale click can't submit. Triggers the hero swing animation +
        /// idle-timer reset for parity with the old per-button handlers.
        /// </summary>
        private void SubmitPlayerAction(BattleAction action)
        {
            if (_runtimeState == null || !_runtimeState.IsAwaitingPlayer() || action == null) return;

            // WO-93: hero swing on the placeholder capsule / swapped model (no-op
            // when there is no Animator). Only meaningful for an attack/ability.
            // WO-163: only fire when the controller declares "Attack" — the
            // placeholder capsule has no such param, so an unguarded SetTrigger
            // logs a warning on every action.
            if (_heroCapsule != null && action.Kind != ActionKind.Defend)
            {
                var heroAnim = _heroCapsule.GetComponentInChildren<Animator>();
                if (heroAnim != null && heroAnim.runtimeAnimatorController != null)
                {
                    foreach (var p in heroAnim.parameters)
                        if (p.nameHash == BattleControllerAttackHash) { heroAnim.SetTrigger(BattleControllerAttackHash); break; }
                }
            }

            ATBCombatManager.Instance?.OnPlayerActed(); // reset idle-pressure timer
            _runtimeState.ChooseAction(action);
        }

        // ---------------------------------------------------------------------
        // Rendering — delegate to the dynamic HUD + drive the hero capsule
        // ---------------------------------------------------------------------

        /// <summary>Re-bind the dynamic HUD + reflect the hero/first-enemy capsules.</summary>
        private void Render(BattleState state)
        {
            if (state == null) return;
            _hudUgui?.Render(_runtimeState);
            RenderCapsules(state);
        }

        /// <summary>
        /// Reflect death on the two placeholder capsules (the 3D layer kept for the
        /// hero + first enemy; the full per-unit visuals are the deferred model-swap
        /// follow-up). The 2D combatant cards are the authoritative per-unit display.
        /// </summary>
        private void RenderCapsules(BattleState state)
        {
            BattleUnit hero = FirstUnit(state, UnitKind.Hero);
            BattleUnit enemy = FirstUnit(state, Side.Enemy);
            ApplyCapsuleState(_heroCapsule, hero);
            ApplyCapsuleState(_enemyCapsule, enemy);
        }

        /// <summary>Tilt a fallen capsule over; keep a live one upright.</summary>
        private static void ApplyCapsuleState(Transform capsule, BattleUnit unit)
        {
            if (capsule == null) return;
            bool down = unit == null || !unit.Alive;
            capsule.localRotation = down
                ? Quaternion.Euler(90f, capsule.localEulerAngles.y, 0f)
                : Quaternion.Euler(0f, capsule.localEulerAngles.y, 0f);
        }

        // ---------------------------------------------------------------------
        // Scene hand-back — async UniTask, never async void
        // ---------------------------------------------------------------------

        /// <summary>
        /// Linger on the result, then return to the scene the battle came from.
        /// The settled outcome already survives on <see cref="ATBRuntimeState.Result"/>
        /// for the caller (the breach trigger / dungeon controller) to read and
        /// apply Heart / building damage or resume the dungeon encounter.
        ///
        /// BUG-008 fix: the destination is <see cref="BattleParams.ReturnScene"/>
        /// off the handoff — a village breach returns to the village, a dungeon
        /// encounter returns to <c>Dungeon_HealersCottage</c>. The hard-coded
        /// village return is gone. A missing handoff (dev / direct play) and an
        /// empty ReturnScene both fall back to the village.
        /// </summary>
        private async UniTask ReturnAfterResult(AtbBattleResult result)
        {
            float delay = Mathf.Max(0f, _returnDelaySeconds);
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(delay),
                ignoreTimeScale: true);

            await SceneRouter.LoadSceneWithFade(ResolveReturnScene());
        }

        /// <summary>
        /// The scene to return to after the battle — the handoff's
        /// <see cref="BattleParams.ReturnScene"/>, defaulting to the village when
        /// no handoff was supplied or the field is blank.
        ///
        /// WO-94 Bug 2: explicitly refuses to return to ATBBattle itself — a
        /// misconfigured ReturnScene that names "ATBBattle" would restart the same
        /// fight instead of exiting, producing the "fight restarts after winning"
        /// symptom. Falls back to Village in that case.
        /// </summary>
        private static string ResolveReturnScene()
        {
            // RETURN-POINT (return-point feature): prefer the scene the player actually fought
            // from (stashed on GoBattle), so the round-trip lands where they left — not the
            // Village2 default. Falls through to the handoff's ReturnScene, then Village.
            string fromReturnPoint = SceneRouter.Return?.Scene;
            if (!string.IsNullOrEmpty(fromReturnPoint))
            {
                if (string.Equals(fromReturnPoint, SceneRouter.ATBBattle,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning(
                        "[BattleController] Return-point scene was ATBBattle — would restart " +
                        "the fight. Falling back to handoff/Village. (return-point feature)");
                }
                else
                {
                    return fromReturnPoint;
                }
            }

            BattleParams handoff = SceneRouter.PendingBattle;
            if (handoff != null && !string.IsNullOrEmpty(handoff.ReturnScene))
            {
                // Guard: never reload the battle scene itself.
                if (string.Equals(handoff.ReturnScene, SceneRouter.ATBBattle,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning(
                        "[BattleController] ReturnScene was set to ATBBattle — " +
                        "this would restart the same fight. Falling back to Village. (WO-94)");
                    return SceneRouter.Village;
                }
                return handoff.ReturnScene;
            }
            return SceneRouter.Village;
        }

        /// <summary>
        /// (Old BindUi / UIDocument / old BattleHud completely removed. The FF7 uGUI HUD
        /// is self-built in Start() and driven via _hudUgui. See BattleHudUgui.cs.)
        /// </summary>
        private void HandleControlModeToggled(string memberId, ControlMode mode)
        {
            AtbControlModeStore.Set(memberId, mode);
        }

        /// <summary>First unit on a side (party first, enemies after — engine order).</summary>
        private static BattleUnit FirstUnit(BattleState state, Side side)
        {
            foreach (BattleUnit u in state.Units)
            {
                if (u.Side == side) return u;
            }
            return null;
        }

        /// <summary>First unit of a kind.</summary>
        private static BattleUnit FirstUnit(BattleState state, UnitKind kind)
        {
            foreach (BattleUnit u in state.Units)
            {
                if (u.Kind == kind) return u;
            }
            return null;
        }

        // ── 3D ActorAnimator drive for immersive fights (knight swings, mage casts, hits/falls) ──

        private void TryDriveActionAnim(BattleState state)
        {
            if (state == null || string.IsNullOrEmpty(state.ActiveUnitId)) return;

            var unit = state.Units.FirstOrDefault(u => u.Id == state.ActiveUnitId);
            if (unit == null) return;

            bool isHeroSide = unit.Side == Side.Party;

            ActorAnimator actor = isHeroSide
                ? GetActorOnCapsule(_heroCapsule)
                : GetActorOnCapsule(_enemyCapsule);

            if (actor == null) return;

            // For player side: Knight-style → PlayAttack (uses combo clips); caster (Mage) → PlayCast.
            // Enemy side: basic attack telegraph.
            if (isHeroSide)
            {
                // Resolve from the setup hero class if possible (simple heuristic for now).
                if (IsCasterHeroClass())
                    actor.PlayCast();
                else
                {
                    // Knight-style: cycle the 3-swing combo (factory maps Combo int + Attack trigger
                    // to the Knight/ standing melee clips). Gives satisfying multi-hit swing feel.
                    _playerAttackCombo = (_playerAttackCombo + 1) % 3;
                    actor.PlayAttack(_playerAttackCombo);
                }
            }
            else
            {
                actor.PlayWindUp(); // or PlayAttack(0) for enemy wind-up/attack feel
            }
        }

        private void TryDriveHitAndDeathAnims(BattleState state, int fromIndex)
        {
            if (state?.Log == null) return;

            // Scan only NEW log entries (>= fromIndex) for damage/death to drive reactions on the
            // corresponding 3D model. This gives visible flinch on every direct hit and collapse on KO.
            for (int i = Mathf.Max(0, fromIndex); i < state.Log.Count; i++)
            {
                var entry = state.Log[i];
                if (entry.Event != BattleLogEvent.Attack && entry.Event != BattleLogEvent.Ability && entry.Event != BattleLogEvent.Death) continue;

                bool targetIsHero = entry.TargetId != null && state.Units.Any(u => u.Id == entry.TargetId && u.Side == Side.Party);
                Transform capsule = targetIsHero ? _heroCapsule : _enemyCapsule;
                var actor = GetActorOnCapsule(capsule);
                if (actor == null) continue;

                if (entry.Event == BattleLogEvent.Death || (entry.TargetId != null && !state.Units.Any(u => u.Id == entry.TargetId && u.Alive)))
                {
                    actor.Die(DeathDirection.Fall);  // immersive collapse / fall
                }
                else if ((entry.Event == BattleLogEvent.Attack || entry.Event == BattleLogEvent.Ability) && (entry.Amount ?? 0) > 0)
                {
                    // Direct hit → flinch / impact reaction (Shared_Hit_Reaction anim)
                    var dir = HitDirection.Front;
                    actor.PlayHit(dir);
                }
            }
        }

        private static ActorAnimator GetActorOnCapsule(Transform capsule)
        {
            if (capsule == null) return null;
            // Don't use ?? — GetComponent returns Unity fake-null (a non-null managed ref to a
            // destroyed/absent native object), so ?? never falls through. Unity's == handles it.
            var a = capsule.GetComponent<ActorAnimator>();
            if (a == null) a = capsule.GetComponentInChildren<ActorAnimator>(true);
            return a;
        }

        private bool IsCasterHeroClass()
        {
            // Matches the ResolveHeroClass mapping (Mage/Cleric use cast anims; Knight/Ranger use attack swings).
            // For ATB the hero class is resolved at setup time; here we use a lightweight check against the fallback/known.
            // In a full multi-party this would inspect the ActiveUnit kind.
            return _fallbackHeroName.Contains("Mage") || _fallbackHeroName.Contains("Thrain") || _fallbackHeroName.Contains("Elara");
        }

        // Simple 3D floating damage for the arena (keeps the central field clean while giving punchy feedback).
        // Called from HandleTurnResolved for every Strike entry. Uses the capsules populated by AtbCombatantSwapper.
        private void SpawnFloatingDamage(BattleState state, int fromIndex)
        {
            if (state?.Log == null || _heroCapsule == null || _enemyCapsule == null) return;

            for (int i = Mathf.Max(0, fromIndex); i < state.Log.Count; i++)
            {
                var entry = state.Log[i];
                if ((entry.Event != BattleLogEvent.Attack && entry.Event != BattleLogEvent.Ability) || (entry.Amount ?? 0) == 0) continue;

                bool isHeroTarget = entry.TargetId != null && state.Units.Any(u => u.Id == entry.TargetId && u.Side == Side.Party);
                Transform capsule = isHeroTarget ? _heroCapsule : _enemyCapsule;

                var go = new GameObject("FloatDmg");
                go.transform.SetParent(capsule, false);
                go.transform.localPosition = new Vector3(0, 2.4f, 0);

                var tmp = go.AddComponent<TextMeshPro>();
                tmp.text = entry.Amount > 0 ? entry.Amount.ToString() : "+" + (-entry.Amount);
                tmp.fontSize = 3.5f;
                tmp.color = entry.Amount > 0 ? (entry.Crit ?? false ? new Color(1f, 0.9f, 0.3f) : new Color(0.95f, 0.25f, 0.25f)) : new Color(0.3f, 0.95f, 0.4f);
                tmp.alignment = TextAlignmentOptions.Center;

                Destroy(go, 1.3f);
                StartCoroutine(AnimateFloat(go.transform, tmp));
            }
        }

        private System.Collections.IEnumerator AnimateFloat(Transform t, TextMeshPro tmp)
        {
            float t0 = Time.time;
            float dur = 1.1f;
            Vector3 start = t.localPosition;
            while (Time.time - t0 < dur && t != null)
            {
                float p = (Time.time - t0) / dur;
                t.localPosition = start + new Vector3(0, p * 1.6f, 0);
                if (tmp) tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, 1f - Mathf.Pow(p, 0.7f));
                yield return null;
            }
            if (t) Destroy(t.gameObject);
        }
    }
}
