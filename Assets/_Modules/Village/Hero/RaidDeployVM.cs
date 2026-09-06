// =============================================================================
// RaidDeployVM — the pure ViewModel behind RaidDeployScreen (pre-raid deploy).
// Strict-MVVM migration Silo D.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// Moves ALL the deploy math OUT of the View: the party roster (hero class +
// companions), the deployable-troop grouping + owned counts, the army-cap readout,
// the deployable count, and the POWER RATING (sum of each deployable troop's attack
// * veterancy). The View (RaidDeployScreen) binds this and renders portraits + the
// troop list + the summary + the DEPLOY CTA purely from vm.* — it never reads
// GameState / TroopCatalog itself.
//
// SEPARATE from RaidSelectionVM by design (different domain: the browse grid vs the
// deploy math). PURE C#: no UnityEngine UI types; unit-testable over a fake army +
// a fake troop-info resolver (§2c).
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.State;
using DeNelle.Core.UI.Mvvm;
using DeNelle.Village;

namespace DeNelle.Village.Hero
{
    /// <summary>Pure ViewModel for the pre-raid tactical deploy screen.</summary>
    public sealed class RaidDeployVM : IPanelViewModel, IDisposable
    {
        /// <summary>Icon role key on each troop row (the View maps it to art; no game state).</summary>
        public const string IconRoleTroop = "troop";

        /// <summary>Display/combat facts about a troop def, resolved by the caller's seam
        /// (the live path wires this to TroopCatalog). A null def yields the fallbacks.</summary>
        public readonly struct TroopInfo
        {
            public readonly string DisplayName;
            public readonly float Attack;
            public readonly bool Ranged;
            public readonly int Slots;
            public TroopInfo(string displayName, float attack, bool ranged, int slots)
            {
                DisplayName = displayName;
                Attack = attack;
                Ranged = ranged;
                Slots = slots;
            }
        }

        private readonly SceneConfigDef _def;
        private readonly ArmyStorage _army;
        private readonly List<string> _partyClasses = new List<string>();
        private readonly Func<string, TroopInfo> _troopInfo;
        private readonly Action _onClose;
        /// <summary>The View's live-GameState snapshot, when it supplied one (see
        /// <see cref="Readiness"/>). Null on the pure path.</summary>
        private readonly ArmyReadiness.Snapshot? _injectedReadiness;

        private readonly List<ItemVM> _troops = new List<ItemVM>();
        private readonly Dictionary<string, bool> _rangedById = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> _siegeById = new Dictionary<string, bool>();
        private bool _disposed;

        // ── IPanelViewModel ───────────────────────────────────────────────────

        public event Action Changed;

        public string Title => "RAID: " + RaidName;

        public void Close() => _onClose?.Invoke();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Changed = null;
        }

        // ── Read-only data the View renders ─────────────────────────────────────

        /// <summary>Raid display name (raw displayName, else the id — the View may re-space it).</summary>
        public string RaidName =>
            _def == null ? "Raid"
            : (!string.IsNullOrEmpty(_def.displayName) ? _def.displayName : _def.id);

        /// <summary>Raw display name (may be empty) so the View can apply the kit spacer to the id.</summary>
        public string DisplayNameRaw => _def != null && !string.IsNullOrEmpty(_def.displayName) ? _def.displayName : "";
        public string RaidId => _def != null ? _def.id : "";

        public string Difficulty => _def != null ? _def.difficulty : null;
        /// <summary>
        /// Honest raid clock target for 3★ "under the clock" — prefers authored
        /// recommendedClearTime when it matches the live scorer default band, else
        /// <see cref="RaidScoring.DefaultClockSeconds"/> so the UI never shows 270s
        /// while combat ends at 180s.
        /// </summary>
        public float TargetTime
        {
            get
            {
                float authored = _def != null ? _def.recommendedClearTime : 0f;
                // Prefer authored when it is the live clock (or within 1s); else tell the truth.
                if (authored > 0f && System.Math.Abs(authored - RaidScoring.DefaultClockSeconds) < 1.5f)
                    return authored;
                if (authored > 0f && authored <= RaidScoring.DefaultClockSeconds + 0.5f)
                    return authored;
                return RaidScoring.DefaultClockSeconds;
            }
        }

        /// <summary>Soft est clear (2-star band) for the preview line; never longer than the raid clock.</summary>
        public float EstClearTime
        {
            get
            {
                float clock = TargetTime;
                float est = _def != null && _def.twoStarTime > 0f ? _def.twoStarTime : clock * 0.85f;
                if (est <= 0f) est = clock * 0.85f;
                return est > clock ? clock : est;
            }
        }

        public string SceneName => _def != null ? _def.sceneName : null;

        /// <summary>
        /// WO-932: deploy only when a scene name is authored AND that scene is in the
        /// player Build Settings (same gate as <see cref="DeNelle.Core.SceneRouter.GoRaid"/>).
        /// </summary>
        public bool CanDeploy =>
            _def != null
            && !string.IsNullOrEmpty(_def.sceneName)
            && DeNelle.Core.SceneRouter.IsSceneInBuild(_def.sceneName);

        /// <summary>Hero class first, then companion classes (deduped); never empty.</summary>
        public IReadOnlyList<string> PartyClasses => _partyClasses;

        /// <summary>One row per deployable troop type — Id = troopDefId, Name = display name
        /// (raw; may be empty), Price = owned count. Never null.</summary>
        public IReadOnlyList<ItemVM> Troops => _troops;

        public int DeployableCount { get; private set; }
        public int PowerRating { get; private set; }

        /// <summary>
        /// WO-823 Phase E's ONE readiness snapshot, resolved for THIS screen -
        /// <see cref="DeNelle.Village.ArmyReadiness"/> is the single formula and this VM never
        /// re-rolls it. Injected by the View at Open (the live GameState read, which is the only
        /// place the in-flight Train-queue slots and EverCompletedRaid are knowable); on the pure
        /// path (tests, headless, no GameState) it is computed from the roster this VM was handed.
        /// Presentation may WORD copy from <see cref="ArmyReadiness.Snapshot.RequiredSlots"/> /
        /// <see cref="ArmyReadiness.Snapshot.Ready"/>; it must never re-decide the raid door -
        /// RaidEntryGate / RaidSelectionScreen remain the one authority on "may this player raid".
        /// </summary>
        public ArmyReadiness.Snapshot Readiness { get; private set; }

        /// <summary>
        /// WO-1403: the number the footer is BOUND to, read straight off the ONE readiness
        /// snapshot (<see cref="Readiness"/>.DeployableSlots). It is SLOT-WEIGHTED, like every
        /// other readiness surface in the game - deliberately NOT the raw headcount
        /// <see cref="DeployableCount"/>, whose divergence from ArmyReadiness was the WO-823
        /// Phase E grey-button-versus-open-gate defect. The two agree for 1-slot troops and at
        /// zero (no deployable troop means no deployable slot), which is the only comparison the
        /// footer makes. The WO-1389 compare line ("you field N") keeps DeployableCount on
        /// purpose: it is measured against a garrison HEADCOUNT.
        /// </summary>
        public int Fielded => Readiness.DeployableSlots;

        /// <summary>The primary CTA word when the player has troops to field.</summary>
        public const string PrimaryAssaultLabel = "BEGIN ASSAULT";
        /// <summary>The primary CTA word when the player has NO troops: a door to the Barracks
        /// (Manage > Troops), never an assault the screen invited them to lose.</summary>
        public const string PrimaryTrainLabel = "TRAIN TROOPS";

        /// <summary>
        /// WO-1403 (owner ruling 2026-09-05, merged review section 2 #2, default NO: no assault
        /// with zero troops). Words carry the state: <see cref="Fielded"/> == 0 -> TRAIN TROOPS,
        /// otherwise BEGIN ASSAULT. Pure; pinned by RaidDeployZeroArmyRegression [zero-army-vm].
        /// </summary>
        public string PrimaryCtaLabel => Fielded > 0 ? PrimaryAssaultLabel : PrimaryTrainLabel;

        /// <summary>True when the footer draws BEGIN ASSAULT at all (Fielded &gt; 0). At zero the
        /// assault button is NOT drawn - not greyed, not renamed SCOUT ONLY - because BEGIN ASSAULT
        /// loads the raid scene and the Heartfire charge is SPENT once inside it
        /// (RaidDeployController.TryInstall -> HeartfireService.TrySpend, cited at
        /// RaidSelectionScreen.cs:553-556; the selection door only READS HasCharge). A zero-army
        /// assault therefore burns a charge on a guaranteed loss.
        ///
        /// (!) DELIBERATELY NOT bound to <see cref="ArmyReadiness.Snapshot.Ready"/>. The only
        /// question this footer asks is ZERO OR NOT; Ready would put a SECOND raid gate on the
        /// deploy screen (a first-raid player with 2 of the required 3 slots would lose the
        /// button entirely), which is exactly the second opinion WO-823 Phase E removed from
        /// this file. Ready/RequiredSlots reach the footer as TRACE only.</summary>
        public bool ShowAssault => Fielded > 0;

        private readonly List<string> _scoutReport = new List<string>();

        /// <summary>WO-839 #3: scout-report intel lines for the deploy screen's intel band —
        /// honest facts the scouting party could see, from the raid's SceneConfigDef only
        /// (wall tier + gates, garrison headcount, boss), then WO-1403's line 4, the spoils
        /// estimate - which is NOT the cosmetic rewardMultiplier repeated as intel: it is what a
        /// win PAYS, from WO-1402's producer (RaidScoring.EstimateSpoils -&gt; ProjectLoot -&gt;
        /// ComputeLoot at RaidScoring.EstimateStars), the same chain the settle screen credits
        /// through. See <see cref="SpoilsLine"/>. Never null; always at least one line.</summary>
        public IReadOnlyList<string> ScoutReport => _scoutReport;

        /// <summary>"Army: N / M slots" (or "Army: -" with no roster).</summary>
        public string ArmyCapText { get; private set; }

        /// <summary>Whether a troop row's role is ranged (drives the glyph). Pure VM data.</summary>
        public bool IsRanged(string troopDefId) =>
            troopDefId != null && _rangedById.TryGetValue(troopDefId, out var r) && r;

        /// <summary>WO-933: three-way role glyph for the pre-deploy list (SIE / RNG / MEL).</summary>
        public string RoleGlyph(string troopDefId)
        {
            if (string.IsNullOrEmpty(troopDefId)) return "MEL";
            if (_siegeById.TryGetValue(troopDefId, out var siege) && siege) return "SIE";
            if (_rangedById.TryGetValue(troopDefId, out var ranged) && ranged) return "RNG";
            return "MEL";
        }

        /// <summary>Canon companion name for a class word (pure mapping moved off the View).</summary>
        public string CompanionName(string cls)
        {
            switch ((cls ?? "").Trim().ToLowerInvariant())
            {
                case "mage":
                case "wizard": return "Thrain";
                case "knight": return "Grom";
                case "ranger": return "Sylas";
                case "cleric":
                case "healer": return "Elara";
                default: return string.IsNullOrEmpty(cls) ? "Hero" : cls;
            }
        }

        // ── Commands ────────────────────────────────────────────────────────────

        /// <summary>DEPLOY -> load the raid scene (no-op when the raid has no battleground).</summary>
        public void Deploy()
        {
            if (!CanDeploy)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Fail("Raid",
                    $"Deploy refused: raid='{RaidId}' scene='{SceneName ?? "<null>"}' " +
                    $"(missing name or not in Build Settings).");
                return;
            }
            // WO-1403: the ruling is enforced at the ONE command, not only at the button that is
            // no longer drawn. Zero fielded -> no scene load, and the trace says why. Fielded is
            // the WO-823 slot-weighted ArmyReadiness snapshot's own DeployableSlots, so this
            // refusal and the readiness gate upstream cannot disagree about "no army".
            if (Fielded <= 0)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Raid",
                    $"Deploy refused: raid='{RaidId}' fielded=0 - WO-1403 ruling, no assault with zero " +
                    "troops; the deploy screen offers TRAIN TROOPS instead.");
                return;
            }
            DeNelle.Core.Diagnostics.FlowTrace.Step("Raid",
                $"Deploy BEGIN ASSAULT raid='{RaidId}' -> scene '{_def.sceneName}' " +
                $"(deployableTroops={DeployableCount} power={PowerRating}).");
            DeNelle.Core.SceneRouter.GoRaid(_def.sceneName);
        }

        // ── Construction / resolution ───────────────────────────────────────────

        /// <summary>The ONLY resolution site: pulls the army + party from GameState and the
        /// troop facts from TroopCatalog so the View never touches either.</summary>
        public static RaidDeployVM CreateDefault(SceneConfigDef def, Action onClose = null)
            => CreateDefault(def, null, onClose);

        /// <summary>
        /// WO-823 Phase E / WO-1403: same resolution, with the readiness snapshot the View
        /// already took from the live GameState (RaidDeployScreen.OpenInternal ->
        /// <see cref="ArmyReadiness.Compute(GameState)"/>). Passing it in - rather than the VM
        /// taking a second Compute of its own - is what keeps ONE snapshot behind the footer,
        /// the trace and the command. Null falls back to the pure roster computation.
        /// </summary>
        public static RaidDeployVM CreateDefault(SceneConfigDef def, ArmyReadiness.Snapshot? readiness,
                                                 Action onClose = null)
        {
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            var army = state != null ? state.Army : null;
            var party = BuildPartyClasses(state);

            Func<string, TroopInfo> resolver = id =>
            {
                var d = TroopCatalog.Find(id);
                if (d == null) return new TroopInfo(null, 10f, false, 1);
                bool ranged = d.Role != null && d.Role.ToLowerInvariant().Contains("ranged");
                return new TroopInfo(d.DisplayName, d.AttackDamage, ranged, d.Slots > 0 ? d.Slots : 1);
            };

            return new RaidDeployVM(def, army, party, resolver, onClose, readiness);
        }

        public RaidDeployVM(SceneConfigDef def, ArmyStorage army, IReadOnlyList<string> partyClasses,
                            Func<string, TroopInfo> troopInfo, Action onClose,
                            ArmyReadiness.Snapshot? readiness = null)
        {
            _def = def;
            _army = army;
            _troopInfo = troopInfo ?? (id => new TroopInfo(null, 10f, false, 1));
            _onClose = onClose;
            _injectedReadiness = readiness;

            if (partyClasses != null) _partyClasses.AddRange(partyClasses);
            if (_partyClasses.Count == 0) _partyClasses.Add("Knight");

            Rebuild();
            BuildScoutReport();
        }

        // WO-839 #3: honest intel from the def only (walls / gates / garrison / boss).
        // Pure string work — stays unit-testable with no Unity types.
        private void BuildScoutReport()
        {
            _scoutReport.Clear();
            if (_def != null)
            {
                if (!string.IsNullOrEmpty(_def.wallTier))
                {
                    string gates = _def.entranceCount > 0
                        ? ", " + _def.entranceCount + (_def.entranceCount == 1 ? " gate" : " gates")
                        : "";
                    _scoutReport.Add("Walls: " + SpaceCamelCase(_def.wallTier) + gates);
                }
                var g = _def.garrison;
                if (g != null)
                {
                    int defenders = 0;
                    if (g.composition != null)
                        foreach (var u in g.composition)
                            if (u != null && u.count > 0) defenders += u.count;
                    if (defenders > 0)
                    {
                        // WO-1389 pressure point 2: the report COMPARES. "Garrison: 15 defenders -
                        // you field 3" puts the seven empty slots next to the number they are
                        // measured against. DeployableCount is computed by Rebuild() before this
                        // runs (constructor order), so the two halves are read from one snapshot.
                        _scoutReport.Add("Garrison: " + defenders + (defenders == 1 ? " defender" : " defenders") +
                                         " - you field " + DeployableCount);
                    }
                    if (!string.IsNullOrEmpty(g.boss))
                        _scoutReport.Add("Boss: " + TitleCaseId(g.boss));
                }
                // WO-1403 line 4 (merged review section 2 #1, default YES: show expected loot, a
                // range never exact): "Spoils: ~1800 wood, ~1100 iron, ~2200 gold" -
                // WO-1402's estimate, WO-1402's formatter, this screen's prefix (see the producer
                // banner below). The player is told what a raid PAYS on the screen where they
                // decide to raid.
                string spoils = SpoilsLine(_def);
                if (!string.IsNullOrEmpty(spoils)) _scoutReport.Add(spoils);
            }
            if (_scoutReport.Count == 0) _scoutReport.Add("No scout intel available.");
        }

        // =====================================================================
        //  WO-1403 line 4: ONE PRODUCER, and it is WO-1402's
        // ---------------------------------------------------------------------
        //  The WO says the spoils line comes "from the same producer as WO-1402", and it
        //  does - literally the same two statics, not a parallel copy of the arithmetic:
        //    RaidSelectionVM.EstimateSpoils(def)  -> RaidScoring.EstimateSpoils(id, mult)
        //                                         -> ProjectLoot(EstimateStars=3, ...)
        //                                         -> ComputeLoot  (the settle payout's own
        //                                            chain, via RaidScoring.LootFor)
        //    RaidSelectionVM.FormatSpoils(est)    -> "Spoils: ~1800 wood, ~1100 iron, ~2200 gold"
        //  The line is now byte-identical to the row the player just tapped, PREFIX INCLUDED -
        //  pinned by RaidDeployZeroArmyRegression [zero-army-spoils]. It used to differ ("Spoils
        //  if you win: "), and that longer prefix overflowed the four-line SCOUT REPORT well and
        //  ATE THE GOLD AMOUNT on both capture resolutions (see SpoilsPrefix below for the
        //  measurement). The prefix indirection stays in the code so a future lane can give the
        //  deploy screen its own words the moment they fit - it is a no-op today, not a mistake.
        //
        //  (!) THIS FILE MUST NEVER CALL RaidScoring.ComputeLoot / ProjectLoot DIRECTLY.
        //  An earlier draft of this lane did exactly that - a 2-star, round-to-50, wood+iron
        //  estimate - and it would have quoted the SAME camp differently on two adjacent
        //  screens (3-star, Approx, wood+iron+gold on the row). A second loot formula is the
        //  drift seed; the oracle reds on a direct ComputeLoot call from here.
        // =====================================================================

        /// <summary>
        /// The deploy screen's prefix — "Spoils: ", the SAME word the selection row uses.
        ///
        /// ⛔ IT WAS "Spoils if you win: " AND THAT CLIPPED, measured 2026-09-05 on the fresh
        /// capture: RaidDeploy_1920x1080.png reads "Spoils if you win: ~1800 wood, ~1100 iron,"
        /// and stops — the gold amount is simply gone, on the screen where the player decides
        /// whether the raid is worth it. The SCOUT REPORT well is budgeted for FOUR lines
        /// (WO-1403), so the line cannot wrap into a fifth; the only lever is length, and the
        /// eleven characters of "if you win" were the whole overflow. At 1920x1080 the well
        /// seated 42 characters of that line before the cut; the longest live line is now
        /// "Spoils: ~4000 wood, ~2400 iron, ~6500 gold" (42), and RaidDeployScreen widens the
        /// report block from x 0.08-0.92 to 0.05-0.96 (+13%) so it lands with ~4 characters of
        /// slack instead of none.
        ///
        /// The word still carries the meaning (the owner is red/green colourblind — never a
        /// colour), and it now matches the block's own grammar: Walls: / Garrison: / Boss: /
        /// Spoils:. The conditional lives in the header the line sits under, SCOUT REPORT, and
        /// in the deploy screen's whole purpose.
        /// </summary>
        public const string SpoilsPrefix = RaidSelectionVM.SpoilsPrefix;

        /// <summary>
        /// "Spoils: ~1800 wood, ~1100 iron, ~2200 gold", or null when the estimate
        /// is all zero (no def, or a tunable-rail fault - RaidScoring.EstimateSpoils never
        /// throws, it answers an empty basket and traces why). No line beats "~0 wood".
        /// </summary>
        public static string SpoilsLine(SceneConfigDef def)
        {
            if (def == null) return null;
            string row = RaidSelectionVM.FormatSpoils(RaidSelectionVM.EstimateSpoils(def));
            if (string.IsNullOrEmpty(row)) return null;
            return row.StartsWith(RaidSelectionVM.SpoilsPrefix, StringComparison.Ordinal)
                ? SpoilsPrefix + row.Substring(RaidSelectionVM.SpoilsPrefix.Length)
                : row;
        }

        /// <summary>"ReinforcedSteel" -> "Reinforced Steel" (pure, no Unity/regex).</summary>
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

        /// <summary>"necromancer" / "orc-warlord" -> "Necromancer" / "Orc Warlord" (id, never raw on screen).</summary>
        private static string TitleCaseId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            var sb = new System.Text.StringBuilder(id.Length);
            bool startOfWord = true;
            foreach (var ch in id)
            {
                if (ch == '-' || ch == '_' || ch == ' ')
                {
                    sb.Append(' ');
                    startOfWord = true;
                    continue;
                }
                sb.Append(startOfWord ? char.ToUpperInvariant(ch) : ch);
                startOfWord = false;
            }
            return sb.ToString().Trim();
        }

        private void Rebuild()
        {
            _troops.Clear();
            _rangedById.Clear();
            _siegeById.Clear();

            // Group deployable troops by TroopDefId (preserving first-seen order), count each.
            var counts = new Dictionary<string, int>();
            var order = new List<string>();
            float power = 0f;
            int deployable = 0;
            int deployableSlots = 0;

            if (_army != null)
            {
                foreach (var t in _army.GetDeployable())
                {
                    if (t == null || string.IsNullOrEmpty(t.TroopDefId)) continue;
                    deployable++;
                    if (!counts.ContainsKey(t.TroopDefId)) { counts[t.TroopDefId] = 0; order.Add(t.TroopDefId); }
                    counts[t.TroopDefId]++;
                    var info = _troopInfo(t.TroopDefId);
                    power += info.Attack * t.DamageMultiplier;
                    // NOT a second slot formula: CreateDefault's resolver reads the same
                    // `d.Slots > 0 ? d.Slots : 1` off the same TroopCatalog entry that
                    // TroopDialogueCommands.SlotOf reads, so this sum equals the one
                    // ArmyReadiness.Compute(GameState) would produce for the same roster.
                    deployableSlots += info.Slots > 0 ? info.Slots : 1;
                }
            }

            foreach (var defId in order)
            {
                var info = _troopInfo(defId);
                _rangedById[defId] = info.Ranged;
                // Siege role from live catalog (TroopInfo has no role field) — catalog is the authority.
                var cat = TroopCatalog.Find(defId);
                _siegeById[defId] = cat != null
                    && string.Equals(cat.Role, "siege", System.StringComparison.OrdinalIgnoreCase);
                string name = string.IsNullOrEmpty(info.DisplayName) ? "" : info.DisplayName;
                // Price carries the owned count for this troop type.
                _troops.Add(new ItemVM(defId, name, IconRoleTroop, defId, counts[defId], "", true));
            }

            DeployableCount = deployable;
            PowerRating = (int)System.Math.Round(power);
            ArmyCapText = ComputeArmyCapText();

            // ── The ONE readiness snapshot behind Fielded / ShowAssault / PrimaryCtaLabel ──
            // Order of preference, and why:
            //  1. The View's injected snapshot - ArmyReadiness.Compute(GameState) taken at
            //     RaidDeployScreen.OpenInternal. Only that read can see the in-flight Train
            //     queue (BarracksService.CommittedTrainingSlots) and GameState.EverCompletedRaid.
            //  2. No GameState (tests / headless / a VM built straight from a roster): the seam
            //     overload with queued=0 and the STRICT default for everCompletedRaid, so the
            //     fallback can never silently soften a gate.
            //  3. No army at all ("Army: -", RaidDeployUiRegression's null-def case): the
            //     WO-813/WO-820 never-false-block snapshot. Compute(ArmyStorage,...) would NRE
            //     on army.MaxArmySize here, and a missing roster must never throw a screen.
            Readiness = _injectedReadiness ?? (_army != null
                ? ArmyReadiness.Compute(_army, deployableSlots, 0)
                : new ArmyReadiness.Snapshot { Ready = true });
        }

        private string ComputeArmyCapText()
        {
            if (_army == null)
            {
                // WO-1110 §2 — this used to fall through silently. "Army: -" is the honest
                // readout when there is no army, but the reason must reach a capture.
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Raid",
                    "army cap text: no army on the VM - the deploy screen reads 'Army: -'.");
                return "Army: -";
            }
            try
            {
                Func<string, int> slotOf = id =>
                {
                    var info = _troopInfo(id);
                    return info.Slots > 0 ? info.Slots : 1;
                };
                int used = _army.SlotsUsed(slotOf);
                return "Army: " + used + " / " + _army.MaxArmySize + " slots";
            }
            catch (Exception ex)
            {
                // WO-1110 §2 — was a bare `catch { return "Army: -"; }`. The fallback string
                // stays (the screen must still render), but a swallowed throw here made the
                // army readout report "unknown" with nothing anywhere saying why.
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Raid",
                    "army cap text THREW - the deploy screen reads 'Army: -' instead of a slot " +
                    "count: " + ex.GetType().Name + ": " + ex.Message);
                return "Army: -";
            }
        }

        // Hero class first, then companion classes from PartyMemberIds (deduped). Always
        // returns at least the hero placeholder so the row never reads empty.
        private static List<string> BuildPartyClasses(GameState state)
        {
            var list = new List<string>();
            if (state != null && state.HeroClass != HeroClassOpt.None)
                list.Add(state.HeroClass.ToString());
            if (state != null && state.PartyMemberIds != null)
                foreach (var id in state.PartyMemberIds)
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    if (!list.Contains(id)) list.Add(id);
                }
            if (list.Count == 0) list.Add("Knight");
            return list;
        }
    }
}
