// =============================================================================
// RaidSelectionVM — the pure ViewModel behind RaidSelectionScreen (the raid grid).
// Strict-MVVM migration Silo D.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// Owns the SceneConfigCatalog projection: the FOUR raid targets (fallback to all
// enemy raids) as ItemVM cards + per-id helpers (difficulty / target time / reward
// hint / description / unlockVictories). The View (RaidSelectionScreen) binds this,
// renders the card grid from vm.Raids + the helpers, and routes a card tap through
// vm.DefFor(id) to open the deploy screen — it never touches the gameplay catalog.
//
// 2026-09-04 — THE ESCALATION GATE (economy map §4). Before today this VM hard-listed
// three ids and emitted EVERY card unlocked at EVERY victory count: ItemVM has always
// carried Locked + LockReason and the VM passed neither, so "upgrade -> unlock a harder
// raid" had no reader. It now compares each def's authored unlockVictories (0/3/10/20)
// against an INJECTED victory count, and refuses a target whose scene this build cannot
// load. Names/copy: docs/CREATIVE_CANON_ELARION_2026-09-04.md §3.
//
// 2026-09-05 - WO-1402: THE ROWS SAY WHAT A RAID PAYS. Every row exposes a spoils
// ESTIMATE ("Spoils: ~1800 wood, ~1100 iron, ~2200 gold") produced by the settle
// payout's OWN formula (RaidScoring.EstimateSpoils -> ProjectLoot -> ComputeLoot, the
// chain RaidScoring.LootFor pays through) - there is no second loot table and no
// literal. The three identical gold pips are hidden until per-camp star ratings VARY
// (no producer records them today, so they stay hidden by data). A camp whose
// garrison exceeds the fieldable army carries the WORD "LOCKED - needs Army N"
// (WO-1389 compare rule: garrison bodies vs deployable bodies). VM owns every string;
// RaidSelectionScreen only renders them. Pinned by RaidSelectionSpoilsRegression.
//
// SEPARATE from RaidDeployVM by design (different domain: this is the browse grid,
// that is the pre-raid deploy math). They only share the SceneConfigDef formatting.
// PURE C#: no UnityEngine UI types; unit-testable over a fake def list (§2c).
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.UI.Mvvm;
using DeNelle.Village;

namespace DeNelle.Village.Hero
{
    /// <summary>Pure ViewModel for the Raids-tab card grid.</summary>
    public sealed class RaidSelectionVM : IPanelViewModel, IDisposable
    {
        /// <summary>Icon role key on each raid card (the View maps it to art; no game state).</summary>
        public const string IconRoleRaid = "raid";

        // The FOUR raid targets, in escalation order (mirrors the View's grid).
        //
        // - THESE ARE LIVE SAVE KEYS. RaidClaimService persists PlayerPrefs
        // "dotr-raid-owner-<id>" / "dotr-raid-crystalday-<id>" keyed on exactly these
        // strings (RaidClaimService.cs:53,62). NEVER rename one to match a display name:
        // the creative canon (docs/CREATIVE_CANON_ELARION_2026-09-04.md §3) renames the
        // CARD, via scene-configs.json displayName, and says so in its own rule.
        //
        // iron_bastion joined the list on 2026-09-04 (economy map §4, tier 4). Its scene
        // was baked 2026-08-21 and was reachable by nothing - see the availability gate
        // below and the ORPHAN note deleted in the same commit.
        private static readonly string[] FlagshipRaidIds =
        {
            "raider_camp_small",
            "fortified_garrison",
            "mage_enclave",
            "iron_bastion",
        };

        /// <summary>
        /// THE ESCALATION INPUT: how many raid victories the player has banked. Compared
        /// against each def's authored <c>unlockVictories</c> (0 / 3 / 10 / 20, economy map §4).
        ///
        /// <para>WHY A PROVIDER AND NOT A DIRECT READ. At git HEAD this session NO victory
        /// counter existed anywhere in the tree (grepped raidsWon / raidVictories / victoryCount /
        /// totalRaidVictories across Assets/ and api/: zero hits; RaidClaimService persists only
        /// per-camp one-time flags, never a total). The counter is a SIBLING LANE's file in this
        /// same release, so this VM neither invents a PlayerPrefs key nor guesses a field name -
        /// it reads through ONE injectable seam and stays pure C#.</para>
        ///
        /// <para>MEASURED IN THE WORKING TREE 2026-09-04: that lane landed
        /// <c>GameState.RaidVictories</c> (GameState.cs:629), incremented by
        /// RaidVictoryController.RecordVictory with a one-shot backfill for older saves.
        /// RaidSelectionScreen.OpenInternal wires this provider to it, and that is the ONLY
        /// wiring site. Unwired (headless, EditMode, a stateless probe) the default is 0, which
        /// locks the gated tiers VISIBLY with a reason - never silently open.</para>
        /// </summary>
        public static Func<int> VictoryCountProvider;

        /// <summary>
        /// Second gate: can this def's scene actually be LOADED in this build? Injected so the
        /// VM stays pure C# (no UnityEngine); <see cref="CreateDefault"/> wires it to
        /// <c>SceneRouter.IsSceneInBuild</c>. Null = assume every scene is loadable, which is
        /// what the EditMode tests and headless projections want.
        ///
        /// <para>THIS EXISTS BECAUSE OF A MEASURED HOLE, not as defensive decoration.
        /// RaidBase_IronBastion.unity bakes 127 GameObjects and NO
        /// HeroStartPoint_PlayerSpawn marker (measured 2026-09-04 against
        /// RaidBase_mage_enclave's 270 GameObjects + 1 marker). HeroControlEnsurer seats the
        /// carried hero at that marker, so entering that scene today strands the hero at its
        /// TOWN world pose. The scene is therefore registered in Build Settings DISABLED,
        /// Application.CanStreamedLevelBeLoaded returns false for it, and this predicate turns
        /// that into a locked card with a sentence instead of a dead tap.</para>
        /// </summary>
        public static Func<string, bool> SceneAvailableProvider;

        /// <summary>
        /// WO-1402 - THE ARMY INPUT for the row's <c>LOCKED - needs Army N</c> word: how many
        /// troop BODIES the player can field right now. Compared against each camp's garrison
        /// headcount (<see cref="GarrisonCount"/>) - the SAME two numbers RaidDeployVM's scout
        /// report puts side by side ("Garrison: 9 defenders - you field 3", WO-1389 pressure
        /// point 2), so the row and the report can never disagree about which camp is above
        /// the army. Injected so the VM stays pure C#; RaidSelectionScreen.OpenInternal wires
        /// it to <c>GameState.Army.GetDeployable()</c> and that is the ONLY wiring site.
        /// Unwired / null / throwing = UNKNOWN (-1): no row carries the word, because a
        /// headless or pre-state frame must never print a lock it cannot prove.
        /// </summary>
        public static Func<int> DeployableTroopsProvider;

        /// <summary>
        /// WO-1402 - per-camp BEST STAR RATING, or -1 when unknown. The three gold pips on
        /// every row were identical on every camp on every frame (merged UI review row 1) and
        /// therefore carried nothing; they are drawn only once ratings actually VARY across
        /// the rows (<see cref="ShowStarPips"/>). MEASURED AT SOURCE 2026-09-05: no producer
        /// persists a per-camp star record (grepped BestStars / bestStars / StarsFor across
        /// Assets/_Modules: DungeonRunGrade and BattleStarRating only, neither per camp), so
        /// this stays null in the shipping wiring and the pips stay hidden - by data, not by
        /// deletion. The first lane that records stars per camp wires this and the pips return.
        /// </summary>
        public static Func<string, int> BestStarsProvider;

        /// <summary>Sentinel for "not known" on the army and star inputs.</summary>
        public const int Unknown = -1;

        /// <summary>The word a row carries when the camp's garrison exceeds the fieldable army.
        /// ASCII; the number is the garrison headcount the player must be able to field.</summary>
        public const string ArmyLockPrefix = "LOCKED - needs Army ";

        /// <summary>Prefix of every spoils line; the oracle asserts on it.</summary>
        public const string SpoilsPrefix = "Spoils: ";

        private readonly List<SceneConfigDef> _defs = new List<SceneConfigDef>();
        private readonly List<ItemVM> _raids = new List<ItemVM>();
        private readonly Dictionary<string, SceneConfigDef> _byId =
            new Dictionary<string, SceneConfigDef>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _spoilsById =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _starsById =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Action _onClose;
        private readonly int _victories;
        private readonly int _deployableTroops;
        private readonly Func<string, bool> _sceneAvailable;
        private readonly Func<string, int> _bestStars;
        private bool _showStarPips;
        private bool _disposed;

        // ── IPanelViewModel ───────────────────────────────────────────────────

        public event Action Changed;

        public string Title => "RAIDS";

        public void Close() => _onClose?.Invoke();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Changed = null;
        }

        // ── Read-only data the View renders ─────────────────────────────────────

        /// <summary>One card per raid (Name = raw displayName, may be empty — the View
        /// falls back to a spaced id; Locked + LockReason carry the escalation gate).
        /// Never null.</summary>
        public IReadOnlyList<ItemVM> Raids => _raids;

        /// <summary>The raw SceneConfigDef for a card id (the View forwards it to the deploy
        /// screen so it never re-pulls the catalog itself), or null.</summary>
        public SceneConfigDef DefFor(string id) =>
            id != null && _byId.TryGetValue(id, out var d) ? d : null;

        // Per-card presentation inputs (raw values; the View formats colour/time/hint).
        public string DifficultyFor(string id) { var d = DefFor(id); return d != null ? d.difficulty : null; }
        public float TargetTimeFor(string id) { var d = DefFor(id); return d != null ? d.recommendedClearTime : 0f; }
        public float RewardMultiplierFor(string id) { var d = DefFor(id); return d != null ? d.rewardMultiplier : 1f; }
        public float ShardChanceFor(string id) { var d = DefFor(id); return d != null ? d.shardDropChance : 0f; }
        /// <summary>The creative canon's one-line card copy for this target (may be null/empty).</summary>
        public string DescriptionFor(string id) { var d = DefFor(id); return d != null ? d.description : null; }
        /// <summary>Authored victory threshold for this target (0 = always available).</summary>
        public int UnlockVictoriesFor(string id) { var d = DefFor(id); return d != null ? d.unlockVictories : 0; }

        // -- WO-1402: what a raid PAYS, and whether the army can take it ----------

        /// <summary>
        /// The row's spoils line - <c>Spoils: ~1800 wood, ~1100 iron, ~2200 gold</c> - or null
        /// when the estimate is all zero (the View then paints no line and the trace says why).
        /// Computed ONCE per row in <see cref="Rebuild"/> from <see cref="EstimateSpoils"/>;
        /// the View only renders the string.
        /// </summary>
        public string SpoilsLineFor(string id) =>
            id != null && _spoilsById.TryGetValue(id, out var s) ? s : null;

        /// <summary>
        /// The ESTIMATE behind the spoils line - the settle payout's own formula
        /// (<c>RaidScoring.EstimateSpoils</c> -> <c>ProjectLoot</c> -> <c>ComputeLoot</c>, the
        /// same chain <c>RaidScoring.LootFor</c> pays through at settle), quoted at a clean
        /// 3-star clear. There is deliberately NO second loot table and NO literal here: the
        /// camp authors a <c>rewardMultiplier</c>, the tunable rail authors the bases, and the
        /// scorer owns the arithmetic. Static so the oracle can call it beside the VM.
        /// </summary>
        public static DeNelle.Village.ResourceCost EstimateSpoils(SceneConfigDef d)
        {
            if (d == null) return default(DeNelle.Village.ResourceCost);
            return RaidScoring.EstimateSpoils(d.id, d.rewardMultiplier);
        }

        /// <summary>
        /// Formats an estimate as the row's line. A RANGE-FEEL "~" on every number (owner
        /// ruling WO-1402: a range or estimate, never exact); zero currencies are dropped;
        /// all-zero returns null. Wood, iron, gold in the economy map's order. Pure, static,
        /// ASCII - the oracle asserts on this exact grammar.
        /// </summary>
        public static string FormatSpoils(DeNelle.Village.ResourceCost est)
        {
            var parts = new List<string>(3);
            if (est.Wood > 0) parts.Add("~" + Approx(est.Wood) + " wood");
            if (est.Iron > 0) parts.Add("~" + Approx(est.Iron) + " iron");
            if (est.Coins > 0) parts.Add("~" + Approx(est.Coins) + " gold");
            return parts.Count == 0 ? null : SpoilsPrefix + string.Join(", ", parts);
        }

        /// <summary>
        /// Rounds an estimate to a number that READS as an estimate: to the nearest 50 below
        /// 1000, the nearest 100 at or above. 1980 -> 2000, 1650 -> 1700, 275 -> 300. Never 0
        /// for a positive input.
        /// </summary>
        public static int Approx(int amount)
        {
            if (amount <= 0) return 0;
            int step = amount < 1000 ? 50 : 100;
            int rounded = (int)(Math.Round(amount / (double)step) * step);
            return rounded < step ? step : rounded;
        }

        /// <summary>Fieldable troop bodies this projection was built against; <see cref="Unknown"/> when unwired.</summary>
        public int DeployableTroops => _deployableTroops;

        /// <summary>
        /// <c>LOCKED - needs Army N</c> when this camp's garrison headcount exceeds the army
        /// the player can field (WO-1389 compare rule: garrison bodies vs deployable bodies);
        /// null when the army covers it OR when the army is <see cref="Unknown"/>. The colour
        /// edge bar may keep painting the tier; this WORD is what carries the state, because
        /// the owner is red/green colourblind and a hue is not a sentence.
        /// </summary>
        public string ArmyLockWordFor(string id) => ArmyLockWord(DefFor(id), _deployableTroops);

        /// <summary>Static form so the oracle and the VM produce the identical word.</summary>
        public static string ArmyLockWord(SceneConfigDef d, int deployableTroops)
        {
            if (d == null || deployableTroops < 0) return null;
            int garrison = GarrisonCount(d);
            return garrison > deployableTroops ? ArmyLockPrefix + garrison : null;
        }

        /// <summary>Best star rating recorded for this camp, or <see cref="Unknown"/>.</summary>
        public int BestStarsFor(string id) =>
            id != null && _starsById.TryGetValue(id, out var s) ? s : Unknown;

        /// <summary>
        /// True only when at least two rows carry a KNOWN rating and those ratings DIFFER.
        /// Identical pips on every row say nothing (merged UI review row 1), so uniform or
        /// unknown ratings hide the row of pips entirely; the View reads this once per build.
        /// </summary>
        public bool ShowStarPips => _showStarPips;

        /// <summary>
        /// WO-1389 pressure point 4 - what the wins BUY, before the player can enter: the scout
        /// line for a card, "Iron walls . 15 defenders" (wall tier + garrison headcount from the
        /// def, the same two facts RaidDeployVM's scout report opens with). Null when the def
        /// authors neither, so an unauthored row paints nothing. Pure string work.
        /// </summary>
        public string ScoutLineFor(string id) => ScoutLine(DefFor(id));

        /// <summary>
        /// WO-1389 - the NEXT camp the ladder has not yet opened for <paramref name="victories"/>
        /// banked wins: the first FLAGSHIP def (the same ordered list CreateDefault renders) whose
        /// authored unlockVictories exceeds the count. Null when every camp is open (the ladder is
        /// climbed) or the catalog resolves nothing - the post-raid dialogue then drops its camp
        /// sentence rather than inventing one. ONE resolution site, shared by the dialogue text
        /// tokens (PostRaidBeatTokens) and the regression oracle, so the card and the sentence can
        /// never name different camps.
        /// </summary>
        public static SceneConfigDef NextLockedCamp(int victories)
        {
            if (victories < 0) victories = 0;
            SceneConfigDef best = null;
            foreach (var id in FlagshipRaidIds)
            {
                var def = SceneConfigCatalog.Find(id);
                if (def == null)
                {
                    DeNelle.Core.Diagnostics.FlowTrace.Warn("Raid",
                        "NextLockedCamp: flagship raid id '" + id + "' does not resolve in scene-configs.json - skipped.");
                    continue;
                }
                if (def.unlockVictories <= victories) continue;
                if (best == null || def.unlockVictories < best.unlockVictories) best = def;
            }
            DeNelle.Core.Diagnostics.FlowTrace.Step("Raid", "NextLockedCamp(victories=" + victories + ") -> " +
                (best != null ? "'" + best.id + "' at " + best.unlockVictories + " wins" : "<none - ladder climbed>"));
            return best;
        }

        /// <summary>Static composer so the dialogue token and the oracle read the SAME sentence.</summary>
        public static string ScoutLine(SceneConfigDef d)
        {
            if (d == null) return null;
            var parts = new List<string>(2);
            if (!string.IsNullOrEmpty(d.wallTier)) parts.Add(SpaceCamelCase(d.wallTier) + " walls");
            int defenders = GarrisonCount(d);
            if (defenders > 0) parts.Add(defenders + (defenders == 1 ? " defender" : " defenders"));
            return parts.Count == 0 ? null : string.Join(" . ", parts);
        }

        /// <summary>Garrison headcount authored on a def (sum of composition counts), 0 when none.</summary>
        public static int GarrisonCount(SceneConfigDef d)
        {
            if (d == null || d.garrison == null || d.garrison.composition == null) return 0;
            int n = 0;
            foreach (var u in d.garrison.composition)
                if (u != null && u.count > 0) n += u.count;
            return n;
        }

        /// <summary>"ReinforcedSteel" -> "Reinforced Steel" (mirrors RaidDeployVM; no regex).</summary>
        private static string SpaceCamelCase(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(s.Length + 4);
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i]) && !char.IsUpper(s[i - 1]) && s[i - 1] != ' ')
                    sb.Append(' ');
                sb.Append(s[i]);
            }
            return sb.ToString();
        }

        // ── Construction / resolution ───────────────────────────────────────────

        /// <summary>The ONLY resolution site: pulls the flagship raids (fallback to all
        /// enemy raids) from <see cref="SceneConfigCatalog"/> so the View never touches it.</summary>
        public static RaidSelectionVM CreateDefault(Action onClose = null)
        {
            var list = new List<SceneConfigDef>();
            foreach (var id in FlagshipRaidIds)
            {
                var def = SceneConfigCatalog.Find(id);
                if (def != null) list.Add(def);
            }
            if (list.Count == 0)
                foreach (var def in SceneConfigCatalog.All)
                    if (def != null && def.IsEnemy) list.Add(def);

            int victories = 0;
            var provider = VictoryCountProvider;
            if (provider != null)
            {
                // Guarded: a provider fault must never blank the raid grid, and must never be
                // swallowed without a log (CLAUDE.md §12).
                try { victories = provider(); }
                catch (Exception ex)
                {
                    DeNelle.Core.Diagnostics.FlowTrace.Warn("Raid",
                        "RaidSelectionVM: VictoryCountProvider threw (" + ex.GetType().Name + ": " +
                        ex.Message + ") - treating the player as 0 victories, so every gated camp " +
                        "shows LOCKED with its reason rather than silently unlocking.");
                    victories = 0;
                }
            }
            else
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Raid",
                    "RaidSelectionVM: no VictoryCountProvider wired - the raid-victory counter is " +
                    "another lane's file in this release. Treating the player as 0 victories, so " +
                    "camps gated above 0 read LOCKED with their reason. Wire it with " +
                    "RaidSelectionVM.VictoryCountProvider = () => theirCounter.");
            }

            // WO-1402 - the army input. Unknown (-1) when unwired or faulting, never 0: a 0
            // would print "LOCKED - needs Army N" on every camp of a headless frame, which is
            // a lock the frame cannot prove.
            int deployable = Unknown;
            var armyProvider = DeployableTroopsProvider;
            if (armyProvider != null)
            {
                try { deployable = armyProvider(); }
                catch (Exception ex)
                {
                    DeNelle.Core.Diagnostics.FlowTrace.Warn("Raid",
                        "RaidSelectionVM: DeployableTroopsProvider threw (" + ex.GetType().Name + ": " +
                        ex.Message + ") - army UNKNOWN, so no row carries the army lock word.");
                    deployable = Unknown;
                }
            }
            else
            {
                DeNelle.Core.Diagnostics.FlowTrace.Step("Raid",
                    "RaidSelectionVM: no DeployableTroopsProvider wired - army UNKNOWN, no row " +
                    "carries 'LOCKED - needs Army N' (expected headless / EditMode).");
            }

            return new RaidSelectionVM(list, onClose, victories, SceneAvailableProvider,
                                       deployable, BestStarsProvider);
        }

        public RaidSelectionVM(IReadOnlyList<SceneConfigDef> defs, Action onClose,
                               int victories = 0, Func<string, bool> sceneAvailable = null,
                               int deployableTroops = Unknown, Func<string, int> bestStars = null)
        {
            _onClose = onClose;
            _victories = victories < 0 ? 0 : victories;
            _sceneAvailable = sceneAvailable;
            _deployableTroops = deployableTroops < 0 ? Unknown : deployableTroops;
            _bestStars = bestStars;
            if (defs != null)
                foreach (var d in defs)
                {
                    if (d == null) continue;
                    _defs.Add(d);
                    if (!string.IsNullOrEmpty(d.id)) _byId[d.id] = d;
                }
            Rebuild();
        }

        /// <summary>Banked raid victories this projection was built against (read-only, for traces).</summary>
        public int Victories => _victories;

        /// <summary>True when this card is gated shut. Mirrors the ItemVM the View renders.</summary>
        public bool IsLocked(string id)
        {
            var d = DefFor(id);
            return d != null && ResolveLock(d) != null;
        }

        /// <summary>The player-facing sentence for a locked card, or null when it is open.</summary>
        public string LockReasonFor(string id)
        {
            var d = DefFor(id);
            return d != null ? ResolveLock(d) : null;
        }

        /// <summary>
        /// THE ONE LOCK RESOLVER - returns the player-facing reason, or null when the card is open.
        ///
        /// <para>Order matters and is deliberate: the PROGRESSION gate is checked first, so a
        /// player who has not earned a target is told to go earn it (the actionable sentence)
        /// rather than told the expedition is not ready (true, but nothing they can do).
        /// Availability is the fallback for a target the player HAS earned whose scene this
        /// build cannot load.</para>
        ///
        /// <para>NEVER A BARE "Locked". Both sentences name the missing thing AND the remedy,
        /// and both stand on their own in greyscale - the owner is red/green colourblind, so the
        /// state is carried by the WORDS (the same law RaidCooldownService's copy follows).
        /// Voice: docs/CREATIVE_CANON_ELARION_2026-09-04.md §0/§3. ASCII only.</para>
        /// </summary>
        private string ResolveLock(SceneConfigDef d)
        {
            int need = d.unlockVictories;
            if (need > 0 && _victories < need)
            {
                int remaining = need - _victories;
                return "The Heart cannot reach this far yet - win " + remaining +
                       (remaining == 1 ? " more raid" : " more raids") + " to press on.";
            }

            var avail = _sceneAvailable;
            if (avail != null && !string.IsNullOrEmpty(d.sceneName))
            {
                bool ok;
                try { ok = avail(d.sceneName); }
                catch (Exception ex)
                {
                    // Never swallow (CLAUDE.md §12). Fail OPEN on a probe fault: SceneRouter's own
                    // IsSceneRegistered gate still refuses an unloadable scene, so the worst case
                    // is a refusal one screen later - strictly better than hiding an earned target.
                    DeNelle.Core.Diagnostics.FlowTrace.Warn("Raid",
                        "RaidSelectionVM: scene-availability probe threw for '" + d.sceneName + "' (" +
                        ex.GetType().Name + ": " + ex.Message + ") - treating it as available.");
                    ok = true;
                }
                if (!ok)
                {
                    DeNelle.Core.Diagnostics.FlowTrace.Warn("Raid",
                        "RaidSelectionVM: '" + d.id + "' is EARNED (" + _victories + " of " + need +
                        " victories) but its scene '" + d.sceneName + "' cannot be loaded in this " +
                        "build - the card stays locked with a sentence instead of dead-ending the " +
                        "player. Register the scene ENABLED in Build Settings once it bakes a " +
                        "HeroStartPoint_PlayerSpawn marker.");
                    return "The Heart remembers no fortress here. This expedition is not ready.";
                }
            }

            return null;
        }

        private void Rebuild()
        {
            _raids.Clear();
            _spoilsById.Clear();
            _starsById.Clear();

            // WO-1402 - star ratings first, because ShowStarPips is a property of the WHOLE
            // grid (do they vary?), not of one row.
            int knownStars = 0, minStars = int.MaxValue, maxStars = int.MinValue;
            foreach (var d in _defs)
            {
                if (d == null || string.IsNullOrEmpty(d.id)) continue;
                int stars = Unknown;
                if (_bestStars != null)
                {
                    try { stars = _bestStars(d.id); }
                    catch (Exception ex)
                    {
                        DeNelle.Core.Diagnostics.FlowTrace.Warn("Raid",
                            "RaidSelectionVM: BestStarsProvider threw for '" + d.id + "' (" +
                            ex.GetType().Name + ": " + ex.Message + ") - rating UNKNOWN for that row.");
                        stars = Unknown;
                    }
                }
                if (stars > 3) stars = 3;
                _starsById[d.id] = stars < 0 ? Unknown : stars;
                if (stars >= 0)
                {
                    knownStars++;
                    if (stars < minStars) minStars = stars;
                    if (stars > maxStars) maxStars = stars;
                }
            }
            _showStarPips = knownStars >= 2 && minStars != maxStars;

            foreach (var d in _defs)
            {
                if (d == null) continue;
                // Name carries the RAW displayName (may be empty); the View falls back to a
                // kit-spaced id so the VM never references the presentation kit.
                string name = string.IsNullOrEmpty(d.displayName) ? "" : d.displayName;
                string lockReason = ResolveLock(d);
                // ItemVM has ALWAYS carried Locked + LockReason (ItemVM.cs:32,35); this VM passed
                // neither, so every card shipped UNLOCKED at every victory count. Named args
                // because both sit behind rarity/equipped in the positional list.
                _raids.Add(new ItemVM(d.id, name, IconRoleRaid, d.id, 0, "", true,
                                      rarity: null, equipped: false,
                                      locked: lockReason != null, lockReason: lockReason));

                // WO-1402 - the spoils ESTIMATE, once per row, from the settle payout's own
                // formula. A null line is not silent: the trace below names the row.
                var est = EstimateSpoils(d);
                string spoils = FormatSpoils(est);
                if (!string.IsNullOrEmpty(d.id)) _spoilsById[d.id] = spoils;
                string armyWord = ArmyLockWord(d, _deployableTroops);

                DeNelle.Core.Diagnostics.FlowTrace.Step("Raid",
                    "row '" + d.id + "' spoils est=" + est.Wood + "w/" + est.Iron + "i/" + est.Coins +
                    "g x" + d.rewardMultiplier.ToString("0.##") + " text=" +
                    (spoils != null ? "\"" + spoils + "\"" : "<none - estimate all zero>") +
                    " pips=" + (_showStarPips ? "shown" : "hidden") +
                    " lock=" + (lockReason != null ? "escalation" : armyWord != null ? "\"" + armyWord + "\"" : "none") +
                    " (garrison " + GarrisonCount(d) + ", army " +
                    (_deployableTroops < 0 ? "unknown" : _deployableTroops.ToString()) + ")");
            }

            DeNelle.Core.Diagnostics.FlowTrace.Step("Raid",
                "RaidSelectionVM projected " + _raids.Count + " raid card(s) at " + _victories +
                " victories; locked=" + CountLocked() + "; pips=" + (_showStarPips ? "shown" : "hidden") +
                " (" + knownStars + " rated); army=" +
                (_deployableTroops < 0 ? "unknown" : _deployableTroops.ToString()) + ".");
        }

        private int CountLocked()
        {
            int n = 0;
            for (int i = 0; i < _raids.Count; i++) if (_raids[i].Locked) n++;
            return n;
        }
    }
}
