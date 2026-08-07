// =============================================================================
// WallRepairController — the player-facing wall / gate / building repair loop.
// -----------------------------------------------------------------------------
// Workstream B, feature 1. WallSegment / Gate / Building already expose a
// Repair(amount) primitive and raise DamageChanged / HpChanged / Collapsed
// events — the repair PRIMITIVE exists; this file builds the player INTERACTION
// LOOP that was entirely missing:
//
//   1. SCAN      — on a short timer, find the damaged structures in the village
//                  and keep a calm amber RepairHighlight pulsing over each one.
//   2. SELECT    — the player taps / clicks a structure; a camera raycast finds
//                  it, RepairTarget wraps it, and a bright violet highlight
//                  marks the selection.
//   3. PROMPT    — a MATERIALS cost is computed and shown (the HUD repair
//                  prompt). OWNER RULING 2026-07-11: repair cost = damage
//                  fraction x the structure's own BUILD cost in its own
//                  materials (wood/iron/food per its catalog row); a destroyed
//                  structure (fraction 1) is a REBUILD at full build cost.
//                  Crystals are NEVER spent on repair (resource-model canon:
//                  Wood/Iron/Food build structures; Crystals = the special arc).
//                  The prompt is MODAL: while it is open every tap belongs to
//                  the prompt's Confirm / Cancel buttons, never the world — the
//                  interaction stays deterministic.
//   4. CONFIRM   — on confirm: if the material wallets cover the cost, spend
//                  through the SAME construction-economy path build-mode
//                  placement charges (EconomyService.TrySpend — the
//                  BuildModeController.ChargeLedger seam) PLUS the GameState
//                  Wood/Iron mirror (the GrantSpendable both-sides pattern, see
//                  SpendMaterials) and call the structure's existing Repair();
//                  otherwise show an insufficient-materials message.
//
// MODULE ISOLATION (port spec Part 2): this file lives in DeNelle.Village and
// references only DeNelle.Core (GameStateService) + Village types. It CANNOT
// reference DeNelle.HUD, so it never touches VillageHudController directly.
// Instead it raises plain UnityEvents (PromptShown / PromptHidden /
// FeedbackShown / HighlightCountChanged) and exposes ConfirmRepair() /
// CancelRepair() / RequestRepair(target) public methods. The scene-setup editor
// file (WallRepairSceneSetup.cs) cross-wires those to the HUD by reflection —
// the same passive-display pattern VillageHudController already documents for
// its BuildRequested event.
//
// INPUT: the project uses the LEGACY Input Manager (UnityEngine.Input) — see the
// workstream constraints. Tap = Input.GetMouseButtonDown(0) OR a began touch.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using DeNelle.Core.Catalog;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using DeNelle.Village.Buildings.Progression;
// The multi-resource cost shape shared with build-mode placement. Aliased: the
// bare name 'ResourceCost' resolves to DeNelle.Village.ResourceCost (the
// EconomyService struct) inside this namespace.
using CoreCost = DeNelle.Core.Catalog.ResourceCost;

namespace DeNelle.Village
{
    /// <summary>
    /// Payload for <see cref="WallRepairController.PromptShown"/> — the data the
    /// HUD repair prompt needs to render one selection.
    /// </summary>
    [Serializable]
    public struct RepairPromptInfo
    {
        /// <summary>Player-facing structure name (e.g. "North Gate").</summary>
        public string StructureName;
        /// <summary>
        /// The fully-composed, ready-to-display prompt sub-line — e.g.
        /// "Repair the North Gate? Cost: 12 wood, 4 iron". Composed here (the
        /// HUD shows it verbatim), so the materials cost travels IN the text.
        /// </summary>
        public string Subtitle;
        /// <summary>
        /// LEGACY (owner 2026-07-11): crystals are NO LONGER spent on repair —
        /// always 0 now. Field kept so the WallRepairHudBridge payload shape is
        /// unchanged; the real cost is <see cref="CostText"/> (in the Subtitle).
        /// </summary>
        public int CrystalCost;
        /// <summary>The composed in-kind materials cost, e.g. "12 wood, 4 iron".</summary>
        public string CostText;
        /// <summary>True when the material wallets cover the repair cost.</summary>
        public bool Affordable;
        /// <summary>Damage fraction 0..1 of the selected structure.</summary>
        public float DamageFraction;
        /// <summary>True when fully destroyed — the prompt verb is "Rebuild" (full build cost).</summary>
        public bool Destroyed;
    }

    /// <summary>A UnityEvent carrying a <see cref="RepairPromptInfo"/>.</summary>
    [Serializable]
    public sealed class RepairPromptEvent : UnityEvent<RepairPromptInfo> { }

    /// <summary>A UnityEvent carrying a feedback message + an is-error flag.</summary>
    [Serializable]
    public sealed class RepairFeedbackEvent : UnityEvent<string, bool> { }

    /// <summary>A UnityEvent carrying an int count (damaged-structure tally).</summary>
    [Serializable]
    public sealed class RepairCountEvent : UnityEvent<int> { }

    /// <summary>
    /// Drives the player wall / gate / building repair loop: highlight damaged
    /// structures, tap-to-select, show an in-kind materials cost (damage
    /// fraction x the structure's own catalog build cost — full build cost =
    /// REBUILD when destroyed), spend + repair on confirm. A self-contained
    /// Village sub-system MonoBehaviour — the scene-setup editor file adds it
    /// to the built village scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WallRepairController : MonoBehaviour
    {
        // ── Inspector wiring ─────────────────────────────────────────────────

        [Header("Scene refs (wire in the inspector, or leave blank to auto-find)")]
        [Tooltip("Camera the selection raycast is cast from. Blank: Camera.main.")]
        [SerializeField] private Camera _camera;

        [Tooltip("Layers the selection raycast may hit. Default: everything.")]
        [SerializeField] private LayerMask _selectableMask = ~0;

        // ── Repair cost (owner ruling 2026-07-11) ────────────────────────────
        // No serialized cost constants any more: repair is priced DATA-ONLY as
        // damage-fraction x the structure's own catalog build cost in its own
        // materials (wood/iron/food). Structures with no materials row anywhere
        // price from the 'repair_default' catalog row (structures-catalog.json,
        // dual-copy). Crystals are never charged. The old _fullRepairCost /
        // _minRepairCost / _useGameState / _localCrystalBalance fields are
        // removed — spends go through the construction economy (EconomyService).

        [Header("Scanning")]
        [Tooltip("Seconds between damaged-structure rescans. Keeps the highlight set fresh " +
                 "without scanning every frame.")]
        [SerializeField, Min(0.1f)] private float _rescanInterval = 0.75f;

        // DEF-226: the always-on amber repair disc the auto-scan pooled over every
        // damaged structure read as a confusing, unexplained green/gold ground
        // artifact (and in one screenshot floated near a hero). DECISION: suppress
        // the always-on repair highlight entirely. Default this OFF so no marker
        // auto-spawns during normal play; the repair MECHANIC stays intact and a
        // highlight is shown ONLY on an explicit selection (tap / RequestRepair /
        // SurfaceWorstRepair). A baked scene value is overridden at runtime in
        // Awake() so the build ships suppressed with no rebake.
        [Tooltip("DEF-226: leave OFF. When true the controller auto-scans the scene and shows an " +
                 "always-on repair disc over every damaged structure (the suppressed confusing artifact). " +
                 "When false, highlights appear only on an explicit repair selection.")]
        [SerializeField] private bool _autoFindStructures = false;

        // ── Events — the HUD bridge (the editor cross-wires these) ───────────

        [Header("Events (cross-wired to the HUD by WallRepairSceneSetup)")]
        [Tooltip("Raised when a structure is selected — carries the prompt data the HUD shows.")]
        public RepairPromptEvent PromptShown = new RepairPromptEvent();

        [Tooltip("Raised when the selection / prompt is cleared — the HUD hides the prompt.")]
        public UnityEvent PromptHidden = new UnityEvent();

        [Tooltip("Raised on a repair result — carries the message + an is-error flag for the HUD toast.")]
        public RepairFeedbackEvent FeedbackShown = new RepairFeedbackEvent();

        [Tooltip("Raised when the count of damaged structures changes — the HUD may show a badge.")]
        public RepairCountEvent DamagedCountChanged = new RepairCountEvent();

        // ── Runtime state ────────────────────────────────────────────────────

        private readonly List<RepairTarget> _damaged = new List<RepairTarget>();
        private readonly List<RepairHighlight> _highlightPool = new List<RepairHighlight>();
        private RepairHighlight _selectionHighlight;
        private RepairTarget _selected;
        private Transform _highlightRoot;
        private float _rescanTimer;
        private int _lastDamagedCount = -1;

        // Explicitly-registered structures (used when _autoFindStructures is off).
        private readonly List<GameObject> _registered = new List<GameObject>();

        // When the HUD repair prompt consumes a tap (Confirm / Cancel button),
        // the bridge calls SuppressNextWorldTap() so the SAME pointer-press is
        // not also read as a world tap by this controller's raycast. UI Toolkit
        // and this MonoBehaviour both see the one OS click; this is the seam.
        private int _suppressTapUntilFrame = -1;

        /// <summary>True while a structure is selected and the prompt is up.</summary>
        public bool HasSelection => _selected != null && _selected.IsValid;

        /// <summary>The currently-selected repair target, or null.</summary>
        public RepairTarget Selected => _selected;

        /// <summary>Count of damaged structures found by the most recent scan.</summary>
        public int DamagedCount => _damaged.Count;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (_camera == null) _camera = Camera.main;

            // DEF-226: force the always-on auto-scan OFF at runtime, even if a baked
            // scene instance was serialized with _autoFindStructures = true. This is
            // the no-rebake override pattern used by other DEF-* fixes — the BUILD
            // ships with the confusing always-on repair disc suppressed without
            // touching / re-baking the scene. The repair mechanic is unaffected:
            // a highlight is still shown on an explicit selection (tap / RequestRepair).
            _autoFindStructures = false;
        }

        private void OnEnable()
        {
            EnsureHighlightRoot();
            Rescan();
        }

        private void OnDisable()
        {
            ClearSelection();
        }

        private void Update()
        {
            // Periodic rescan keeps the damaged-structure highlight set current
            // as enemies wear walls down / the player repairs them.
            _rescanTimer -= Time.deltaTime;
            if (_rescanTimer <= 0f)
            {
                _rescanTimer = _rescanInterval;
                Rescan();
            }

            // If the selected structure was destroyed / removed, drop the prompt.
            if (_selected != null && !_selected.IsValid)
                ClearSelection();

            if (TapPressedThisFrame())
                HandleTap();
        }

        // =====================================================================
        //  Structure registration (used when _autoFindStructures is off)
        // =====================================================================

        /// <summary>
        /// Registers an explicit set of structure GameObjects to consider for
        /// repair. Only consulted when <c>_autoFindStructures</c> is false — the
        /// default scans the whole scene. Each object should carry (on itself or
        /// a child) a <see cref="WallSegment"/>, <see cref="Gate"/> or
        /// <see cref="Building"/>.
        /// </summary>
        public void RegisterStructures(IEnumerable<GameObject> structures)
        {
            if (structures == null) return;
            foreach (var go in structures)
                if (go != null && !_registered.Contains(go)) _registered.Add(go);
            Rescan();
        }

        // =====================================================================
        //  Scanning + highlights
        // =====================================================================

        /// <summary>
        /// Rebuilds the damaged-structure list and refreshes the in-world
        /// highlight markers. Cheap enough to run a few times a second; called
        /// on a timer and after every repair.
        /// </summary>
        public void Rescan()
        {
            _damaged.Clear();
            CollectDamaged(_damaged);

            // DEF-226: only paint the always-on pooled "repairable" discs when the
            // (now default-OFF, runtime-forced-OFF) auto-scan is enabled. With it
            // suppressed we still keep _damaged + the damaged-count badge current,
            // but no unprompted ground disc is shown — a highlight appears ONLY on
            // an explicit selection (the bright selection marker, see Select()).
            EnsureHighlightRoot();
            int poolIndex = 0;
            if (_autoFindStructures)
            {
                // Keep the highlight pool sized to the damaged set, repositioning
                // each marker over its structure. The selected structure gets the
                // bright marker instead of a pool one.
                for (int i = 0; i < _damaged.Count; i++)
                {
                    var t = _damaged[i];
                    if (_selected != null && _selected.SameAs(t)) continue; // selection marker covers it

                    RepairHighlight hl = GetPooledHighlight(poolIndex++);
                    hl.SetVisible(true);
                    hl.SetSelected(false);
                    hl.FitTo(t);
                }
            }
            // Hide any surplus pooled markers (also hides ALL of them when suppressed).
            for (int i = poolIndex; i < _highlightPool.Count; i++)
                _highlightPool[i].SetVisible(false);

            if (_damaged.Count != _lastDamagedCount)
            {
                _lastDamagedCount = _damaged.Count;
                DamagedCountChanged?.Invoke(_damaged.Count);
            }
        }

        /// <summary>
        /// Fills <paramref name="into"/> with a RepairTarget for every damaged
        /// structure. When <c>_autoFindStructures</c> is off (DEF-226 default) the
        /// periodic scan only considers explicitly-registered structures, so no
        /// always-on disc is pooled. Explicit flows that need the full scene set
        /// (e.g. <see cref="SurfaceWorstRepair"/>) call <see cref="CollectAllDamaged"/>.
        /// </summary>
        private void CollectDamaged(List<RepairTarget> into)
        {
            if (_autoFindStructures)
            {
                CollectAllDamaged(into);
            }
            else
            {
                foreach (var go in _registered)
                {
                    if (go == null) continue;
                    var t = RepairTarget.TryWrap(go.transform);
                    if (t != null && t.IsValid && t.NeedsRepair) into.Add(t);
                }
            }
        }

        /// <summary>
        /// Scans the whole scene for damaged WallSegment / Gate / Building targets,
        /// independent of the always-on highlight suppression (DEF-226). Used by the
        /// explicit repair flow so "repair the worst structure" still works while the
        /// unprompted ground disc stays suppressed.
        /// </summary>
        private void CollectAllDamaged(List<RepairTarget> into)
        {
            AddDamagedOfType<WallSegment>(into);
            AddDamagedOfType<Gate>(into);
            AddDamagedOfType<Building>(into);
        }

        private static void AddDamagedOfType<T>(List<RepairTarget> into) where T : Component
        {
#if UNITY_2023_1_OR_NEWER
            var found = UnityEngine.Object.FindObjectsByType<T>();
#else
            var found = UnityEngine.Object.FindObjectsByType<T>();
#endif
            foreach (var c in found)
            {
                if (c == null) continue;
                var t = RepairTarget.TryWrap(c);
                if (t == null || !t.IsValid || !t.NeedsRepair) continue;
                // WO-753 ruling (owner 2026-07-19, SUPERSEDES WO-672): a DESTROYED structure is LOST
                // - it is NOT a Repair-All target (rebuild fresh at full cost). Skip anything at/over
                // the destroyed threshold so the "Repair All" offer never tries to repair-back-online
                // a destroyed WallSegment/Gate/Building.
                if (t.DamageFraction >= DestroyedFraction) continue;
                into.Add(t);
            }
        }

        private void EnsureHighlightRoot()
        {
            if (_highlightRoot != null) return;
            var go = new GameObject("RepairHighlights");
            go.transform.SetParent(transform, false);
            _highlightRoot = go.transform;
        }

        private RepairHighlight GetPooledHighlight(int index)
        {
            while (_highlightPool.Count <= index)
                _highlightPool.Add(RepairHighlight.Create(_highlightRoot));
            return _highlightPool[index];
        }

        // =====================================================================
        //  Selection — tap-to-select
        // =====================================================================

        private void HandleTap()
        {
            // A tap that the HUD repair prompt consumed (its Confirm / Cancel
            // button) is not a world tap — the bridge flags it via
            // SuppressNextWorldTap() so the same pointer-press is not also
            // raycast into the world here.
            if (Time.frameCount <= _suppressTapUntilFrame) return;

            // While the repair prompt is up it is MODAL — every tap belongs to
            // the prompt (Confirm / Cancel), never the world. This keeps the
            // interaction deterministic regardless of UI-Toolkit-vs-Update
            // event ordering: the player must use the prompt buttons. A fresh
            // world tap can only START a selection, never fight an open one.
            if (HasSelection) return;

            var cam = _camera != null ? _camera : Camera.main;
            if (cam == null) return;

            Vector2 screen = PointerScreenPosition();
            Ray ray = cam.ScreenPointToRay(screen);
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, _selectableMask))
                return; // tapped empty space — nothing to select.

            var target = RepairTarget.TryWrap(hit.collider);
            if (target == null)
            {
                // NO SILENT FAILURE (CLAUDE.md section 12.2). This early return is the exact
                // path a player walks when they tap a BURNING tower / harvest site / collector
                // and nothing happens: RepairTarget wraps only WallSegment / Gate / Building,
                // so every other damageable surface taps to null here and is silently ignored.
                // Name what was actually hit so a capture distinguishes "tapped scenery" from
                // "tapped a damaged structure this controller cannot address".
                var hitGo = hit.collider != null ? hit.collider.gameObject : null;
                FlowTrace.Throttle("Repair", "tap-not-repairable", 2f,
                    $"tap hit '{(hitGo != null ? hitGo.name : "<null>")}' but RepairTarget could not wrap it - " +
                    "no repair prompt. RepairTarget covers WallSegment/Gate/Building only; towers, " +
                    "harvest sites and collectors are reachable ONLY through Repair-All.");
                return; // tapped something that is not a repairable structure.
            }

            Select(target);
        }

        /// <summary>
        /// WO-38: rescans, then surfaces the most-damaged structure's repair prompt
        /// (highest <see cref="RepairTarget.DamageFraction"/>). Returns true when a
        /// damaged structure was found and selected. The wave-clear director calls
        /// this to nudge the player to repair after surviving a wave.
        /// </summary>
        public bool SurfaceWorstRepair()
        {
            // DEF-226: scan the whole scene here regardless of the always-on
            // highlight suppression so the post-wave "repair your worst structure"
            // nudge still finds a target — this is an EXPLICIT repair request, which
            // is exactly the interaction the suppressed always-on disc is replaced by.
            var all = new List<RepairTarget>();
            CollectAllDamaged(all);
            if (all.Count == 0) return false;
            int best = 0;
            for (int i = 1; i < all.Count; i++)
                if (all[i].DamageFraction > all[best].DamageFraction) best = i;
            RequestRepair(all[best]);
            return true;
        }

        /// <summary>
        /// Selects <paramref name="target"/> for repair: marks it with the bright
        /// highlight and raises <see cref="PromptShown"/> so the HUD shows the
        /// materials cost. An undamaged structure is rejected with feedback.
        /// </summary>
        public void RequestRepair(RepairTarget target)
        {
            if (target != null && target.IsValid) Select(target);
        }

        private void Select(RepairTarget target)
        {
            if (!target.NeedsRepair)
            {
                ClearSelection();
                FeedbackShown?.Invoke(WallRepairStrings.IntactMessage, false);
                return;
            }

            _selected = target;

            EnsureHighlightRoot();
            if (_selectionHighlight == null)
                _selectionHighlight = RepairHighlight.Create(_highlightRoot);
            _selectionHighlight.SetVisible(true);
            _selectionHighlight.SetSelected(true);
            _selectionHighlight.FitTo(target);

            // Re-run the scan so the pool markers no longer double up on the
            // newly-selected structure.
            Rescan();

            RaisePrompt();
        }

        /// <summary>Clears the current selection and hides the repair prompt.</summary>
        public void CancelRepair() => ClearSelection();

        private void ClearSelection()
        {
            bool had = _selected != null;
            _selected = null;
            if (_selectionHighlight != null) _selectionHighlight.SetVisible(false);
            if (had)
            {
                PromptHidden?.Invoke();
                Rescan(); // re-show the calm marker on the de-selected structure
            }
        }

        private void RaisePrompt()
        {
            if (_selected == null || !_selected.IsValid) return;
            CoreCost cost = CostFor(_selected);
            bool destroyed = _selected.DamageFraction >= DestroyedFraction;
            string name = _selected.DisplayName;
            string costText = DescribeMaterials(cost);
            PromptShown?.Invoke(new RepairPromptInfo
            {
                StructureName = name,
                // The materials cost travels IN the sub-line (the HUD shows it
                // verbatim); destroyed rows read "Rebuild", damaged read "Repair".
                Subtitle = string.Format(
                    destroyed ? WallRepairStrings.RebuildSubtitleFormat
                              : WallRepairStrings.SubtitleWithCostFormat,
                    name, costText),
                CrystalCost = 0,           // owner 2026-07-11: crystals never spent on repair
                CostText = costText,
                Affordable = CanAffordMaterials(cost),
                DamageFraction = _selected.DamageFraction,
                Destroyed = destroyed,
            });
        }

        // =====================================================================
        //  Cost (owner ruling 2026-07-11 — in-kind materials, data-driven)
        // =====================================================================

        /// <summary>Damage fraction at/above which a structure counts as destroyed → REBUILD.</summary>
        public const float DestroyedFraction = 0.999f;

        /// <summary>The data-driven default cost row for structures with no materials row anywhere.</summary>
        private const string DefaultCostCatalogId = "repair_default";
        /// <summary>Scene-built village wall ring (never player-placed) prices as the canon wall row.</summary>
        private const string FallbackWallCatalogId = "wall_stone";
        /// <summary>Scene-built cardinal gates price as the canon gate row.</summary>
        private const string FallbackGateCatalogId = "gate_stone";

        /// <summary>
        /// Materials cost to repair <paramref name="target"/> — the structure's
        /// own catalog BUILD cost (wood/iron/food) scaled by its damage fraction.
        /// Destroyed (fraction ~1) = the full build cost = the REBUILD price.
        /// Crystals are never charged (owner 2026-07-11).
        /// </summary>
        public CoreCost CostFor(RepairTarget target)
        {
            if (target == null || !target.IsValid) return default;
            return CostForFraction(target.DamageFraction,
                BuildCostForComponent(target.Transform));
        }

        /// <summary>
        /// The same one cost authority for structures that are not RepairTarget-
        /// wrappable (towers / harvest sites / collectors — HpFraction/IsBroken
        /// surfaces): resolves <paramref name="structure"/>'s catalog build cost
        /// and scales it by <paramref name="damageFraction"/>.
        /// </summary>
        public CoreCost CostForStructure(Component structure, float damageFraction)
            => CostForFraction(damageFraction, BuildCostForComponent(structure));

        /// <summary>
        /// The one formula: per-material ceil(buildCost x damage fraction). A
        /// destroyed structure (fraction ≥ <see cref="DestroyedFraction"/>) pays
        /// the FULL build cost — that IS the rebuild option. Crystals slot is
        /// always 0 (never spent on repair). WO-676 STEWARD (Master Mason): the
        /// `repairCost` talent sum discounts the fraction here — the ONE pricing
        /// choke point every repair path (prompt, confirm, Repair-All, rebuild)
        /// already flows through. Identity at sum 0.
        /// </summary>
        public static CoreCost CostForFraction(float damageFraction, CoreCost buildCost)
        {
            float frac = Mathf.Clamp01(damageFraction);
            if (frac >= DestroyedFraction) frac = 1f;   // destroyed = full rebuild cost

            // ONE HeroTalentModifiers read (WO-676 §2b). StatSum is internally null-safe
            // (0 with no service/tree/nodes); clamped so a mis-authored node can never
            // make repairs free-negative. Applied after the destroyed normalization so
            // rebuilds are discounted the same as repairs.
            float discount = Mathf.Clamp01(DeNelle.Village.Talents.HeroTalentModifiers.StatSum(
                HeroTalentClassReader.Slug(), "repairCost"));
            if (discount > 0f)
            {
                frac *= 1f - discount;
                FlowTrace.Once("Talent", "repairCost",
                    $"repairCost -{discount:P0} applied to repair pricing (WO-676 Master Mason).");
            }

            return new CoreCost
            {
                wood     = Mathf.CeilToInt(buildCost.wood * frac),
                food     = Mathf.CeilToInt(buildCost.food * frac),
                iron     = Mathf.CeilToInt(buildCost.iron * frac),
                crystals = 0,   // owner 2026-07-11: crystals are never spent on repair
            };
        }

        /// <summary>
        /// Resolves a structure's BUILD cost in materials from where that cost
        /// actually lives, in precedence order:
        ///   1. a PlacedStructure parent → its own catalog row (the id placement charged);
        ///   2. a Building → the structures-catalog row matching Building.BuildingId
        ///      (workshop / market / jeweler / forge / mill / lumbermill / arcane-tower /
        ///      pet-house — buildings.json authors crystalCost ONLY, so it cannot
        ///      supply materials and is not consulted);
        ///   3. a ResourceCollector → the Collector row whose repo.collectorBuildingId
        ///      matches (collector_farm / collector_lumbermill / collector_forge);
        ///   4. a scene-built WallSegment / Gate (never Occupy()'d, no PlacedStructure)
        ///      → the canon wall_stone / gate_stone rows;
        ///   5. anything else (runtime stations absent from every catalog — the
        ///      Apothecary case — and harvest sites) → the data-driven
        ///      'repair_default' catalog row (dual-copy structures-catalog.json).
        /// A row whose materials (wood/iron/food) are all zero does not count —
        /// resolution falls through (crystals-only rows can't price a repair).
        /// </summary>
        private static CoreCost BuildCostForComponent(Component structure)
        {
            if (structure == null) return DefaultBuildCost();

            var ps = structure.GetComponentInParent<PlacedStructure>();
            if (ps != null)
            {
                var c = MaterialsFromEntry(CatalogRegistry.Get(ps.itemId), out bool ok);
                if (ok) return c;
            }

            var building = structure.GetComponentInParent<Building>();
            if (building != null && !string.IsNullOrEmpty(building.BuildingId))
            {
                var c = MaterialsFromEntry(CatalogRegistry.Get(building.BuildingId), out bool ok);
                if (ok) return c;
            }

            var collector = structure.GetComponentInParent<ResourceCollector>();
            if (collector != null && !string.IsNullOrEmpty(collector.BuildingId))
            {
                foreach (var e in CatalogRegistry.OfType(CatalogType.Collector))
                {
                    if (e == null) continue;
                    string cid = e.repo != null && !string.IsNullOrEmpty(e.repo.collectorBuildingId)
                        ? e.repo.collectorBuildingId : e.id;
                    if (cid != collector.BuildingId) continue;
                    var c = MaterialsFromEntry(e, out bool ok);
                    if (ok) return c;
                }
            }

            if (structure.GetComponentInParent<WallSegment>() != null)
            {
                var c = MaterialsFromEntry(CatalogRegistry.Get(FallbackWallCatalogId), out bool ok);
                if (ok) return c;
            }
            if (structure.GetComponentInParent<Gate>() != null)
            {
                var c = MaterialsFromEntry(CatalogRegistry.Get(FallbackGateCatalogId), out bool ok);
                if (ok) return c;
            }

            return DefaultBuildCost();
        }

        /// <summary>
        /// The materials slice (wood/iron/food — crystals dropped per the ruling)
        /// of a catalog row's build cost. <paramref name="found"/> is false when
        /// the row is missing or authors no materials (crystals-only rows).
        /// </summary>
        private static CoreCost MaterialsFromEntry(CatalogEntry entry, out bool found)
        {
            found = false;
            var repo = entry != null ? entry.repo : null;
            if (repo == null) return default;
            var c = new CoreCost
            {
                wood = repo.cost.wood,
                food = repo.cost.food,
                iron = repo.cost.iron,
                crystals = 0,
            };
            found = c.wood > 0 || c.food > 0 || c.iron > 0;
            return c;
        }

        /// <summary>
        /// The 'repair_default' catalog row's materials (data-driven, dual-copy
        /// structures-catalog.json). An emergency in-code constant only if the
        /// row is missing — warned, never silent.
        /// </summary>
        private static CoreCost DefaultBuildCost()
        {
            var c = MaterialsFromEntry(CatalogRegistry.Get(DefaultCostCatalogId), out bool found);
            if (found) return c;
            FlowTrace.Warn("Repair",
                $"catalog row '{DefaultCostCatalogId}' missing/material-less — " +
                "using the emergency in-code default (30 wood, 15 iron). Restore the data row.");
            return new CoreCost { wood = 30, iron = 15 };
        }

        /// <summary>True when the wood/iron/food slots are all zero (a free repair).</summary>
        public static bool MaterialsZero(CoreCost c) => c.wood == 0 && c.food == 0 && c.iron == 0;

        /// <summary>
        /// Player-facing materials list, e.g. "12 wood, 4 iron" (skips zero slots;
        /// plain copy, no glyphs — tofu rule). "nothing" for an all-zero cost.
        /// </summary>
        public static string DescribeMaterials(CoreCost c)
        {
            // WO-697: currency amounts render through the ONE kit formatter
            // (ElarionUi.CompactNumber — verbatim below 10k, "98.6k"/"1.2m" above),
            // so a six-digit rebuild price can never clip a banner/prompt line.
            var parts = new List<string>(3);
            if (c.wood > 0) parts.Add(DeNelle.Core.UI.ElarionUi.CompactNumber(c.wood) + " wood");
            if (c.iron > 0) parts.Add(DeNelle.Core.UI.ElarionUi.CompactNumber(c.iron) + " iron");
            if (c.food > 0) parts.Add(DeNelle.Core.UI.ElarionUi.CompactNumber(c.food) + " food");
            return parts.Count > 0 ? string.Join(", ", parts) : "nothing";
        }

        // =====================================================================
        //  WO-672 Slice E — Repair All (the damage-report CTA)
        // =====================================================================

        /// <summary>One repairable item in the Repair-All sweep (uniform view).</summary>
        private struct RepairAllItem
        {
            public string Name;
            public float DamageFraction;
            public CoreCost Cost;     // in-kind materials (owner 2026-07-11); crystals slot always 0
            public Action Fix;        // the structure's full-restore path (REP-1: RepairFull, not a magnitude)
            public Func<float> FractionNow; // live re-read of the damage fraction (post-fix proof line)
        }

        /// <summary>
        /// Materials cost of repairing EVERYTHING currently damaged (walls / gates /
        /// buildings via <see cref="CostFor"/>, towers / harvest sites / collectors
        /// via <see cref="CostForStructure"/> — every one priced from its own
        /// catalog row). The damage-report CTA shows this total; all-zero = clean.
        /// </summary>
        public CoreCost RepairAllCost()
        {
            var total = default(CoreCost);
            foreach (var item in CollectRepairAllSet())
            {
                total.wood += item.Cost.wood;
                total.food += item.Cost.food;
                total.iron += item.Cost.iron;
            }
            return total;
        }

        /// <summary>
        /// WO-672 Slice E + owner ruling 2026-07-11: repairs every damaged structure
        /// WORST-FIRST, spending in-kind materials through the SAME construction-
        /// economy path build-mode placement charges (<see cref="SpendMaterials"/> →
        /// EconomyService.TrySpend + the GameState Wood/Iron mirror — never a second
        /// wallet path). Greedy per-resource affordability: an unaffordable item is
        /// skipped (a cheaper, less-damaged one later in the sweep may still fit;
        /// partial repair is honest). Raises <see cref="FeedbackShown"/> with the
        /// summary (the existing HUD toast surface).
        /// Returns (repairedCount, spentMaterials, remainingDamaged).
        /// </summary>
        public (int repairedCount, CoreCost spent, int remainingDamaged) RepairAll()
        {
            var items = CollectRepairAllSet();
            items.Sort((a, b) => b.DamageFraction.CompareTo(a.DamageFraction));   // worst-first
            FlowTrace.Step("Repair", $"RepairAll: {items.Count} damaged, wallet={WalletLine()}");

            int repaired = 0, remaining = 0;
            var spent = default(CoreCost);
            foreach (var item in items)
            {
                bool rebuild = item.DamageFraction >= DestroyedFraction;
                if (!MaterialsZero(item.Cost) && !SpendMaterials(item.Cost,
                        (rebuild ? "rebuild " : "repair ") + item.Name))
                {
                    remaining++;
                    FlowTrace.Step("Repair",
                        $"RepairAll: SKIPPED '{item.Name}' (dmg {item.DamageFraction:0.00}) — " +
                        $"cost {DescribeMaterials(item.Cost)} unaffordable, wallet={WalletLine()}");
                    continue;
                }
                var fix = item.Fix;
                Guard.Try("Repair", $"RepairAll fix '{item.Name}'", () => fix?.Invoke());
                repaired++;
                spent.wood += item.Cost.wood;
                spent.food += item.Cost.food;
                spent.iron += item.Cost.iron;
                // REP-1 post-fix state: re-read the live fraction AFTER the fix ran —
                // a paid repair that leaves damage on the structure is a logged line.
                float postFrac = item.FractionNow != null ? item.FractionNow() : -1f;
                FlowTrace.Step("Repair",
                    $"RepairAll: {(rebuild ? "REBUILT" : "repaired")} '{item.Name}' " +
                    $"(dmg {item.DamageFraction:0.00} -> post-fix {postFrac:0.00}) " +
                    $"for {DescribeMaterials(item.Cost)}");
            }

            FlowTrace.Step("Repair",
                $"RepairAll SUMMARY: repaired={repaired} spent={DescribeMaterials(spent)} " +
                $"remaining={remaining} wallet={WalletLine()}");
            if (repaired > 0)
                FeedbackShown?.Invoke(remaining > 0
                    ? $"Repaired {repaired} structures for {DescribeMaterials(spent)} - {remaining} still damaged (out of materials)"
                    : $"Repaired {repaired} structures for {DescribeMaterials(spent)}", false);
            else if (items.Count > 0)
                FeedbackShown?.Invoke("Not enough materials to repair anything", true);

            ClearSelection();
            Rescan();
            return (repaired, spent, remaining);
        }

        /// <summary>
        /// DIAGNOSTIC SEAM (read-only, no mutation) - a one-line description of everything
        /// the Repair-All sweep currently considers repairable, as
        /// "name(dmg=frac,cost); ...". Exists because "there is no option to repair" is
        /// ambiguous from the outside: it can mean the backend never offered the structure
        /// (a LOGIC gap) or that it offered it and no surface showed it (a UI gap). Reading
        /// this line next to a burning structure's name settles which, with no guessing.
        /// Used by <see cref="RepairAvailabilityProbe"/>; safe to call at any time.
        /// </summary>
        public string DescribeRepairAllSet()
        {
            var items = CollectRepairAllSet();
            if (items.Count == 0) return "<empty - nothing is currently repairable>";
            var sb = new System.Text.StringBuilder();
            foreach (var item in items)
            {
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(item.Name).Append("(dmg=").Append(item.DamageFraction.ToString("0.00"))
                  .Append(',').Append(DescribeMaterials(item.Cost)).Append(')');
            }
            return sb.ToString();
        }

        /// <summary>Compact wallet line for FlowTrace (in-session EconomyService pools).</summary>
        private static string WalletLine()
        {
            var econ = EconomyService.Instance;
            if (econ == null) return "<no EconomyService>";
            return $"W{econ.Wood} I{econ.Iron} F{econ.Food}";
        }

        /// <summary>
        /// The full damaged set as uniform Repair-All items: the RepairTarget scan
        /// (walls / gates / buildings — <see cref="CollectAllDamaged"/>, the same
        /// set the single-repair flow sees), the WO-672 Slice A tower surface
        /// (Tower / DefenseTower / ArcaneTower / HarvestSite: HpFraction / IsBroken
        /// / Repair()), and resource collectors. Owner ruling 2026-07-11: EVERY
        /// item is priced from its own catalog row's materials (collectors are no
        /// longer free — their Collector row authors a real build cost).
        /// </summary>
        private List<RepairAllItem> CollectRepairAllSet()
        {
            var items = new List<RepairAllItem>();

            var targets = new List<RepairTarget>();
            CollectAllDamaged(targets);
            foreach (var t in targets)
            {
                if (t == null || !t.IsValid || !t.NeedsRepair) continue;
                var tc = t;
                items.Add(new RepairAllItem
                {
                    Name = t.DisplayName,
                    DamageFraction = t.DamageFraction,
                    Cost = CostFor(t),
                    Fix = () => tc.RepairFull(),   // REP-1: full BY CONTRACT (a fixed 100f under-repaired 120..240-MaxHp buildings), same as ConfirmRepair
                    FractionNow = () => tc.DamageFraction,
                });
            }

            // WO-753 ruling (owner 2026-07-19, SUPERSEDES WO-672's repair-back-online): a DESTROYED
            // (IsBroken) tower / spire / harvest site / collector is LOST - it is NOT a Repair-All
            // target; it returns ONLY via a full-cost build-mode placement. Skip every broken one so
            // the offer only lists still-standing DAMAGED structures.
            foreach (var t in UnityEngine.Object.FindObjectsByType<Tower>(FindObjectsSortMode.None))
            {
                if (t == null || t.IsBroken) continue;
                AddHpItem(items, t.gameObject.name, t, t.HpFraction, t.IsBroken, t.Repair,
                    () => t.IsBroken ? 1f : 1f - Mathf.Clamp01(t.HpFraction));
            }
            foreach (var t in UnityEngine.Object.FindObjectsByType<DefenseTower>(FindObjectsSortMode.None))
            {
                if (t == null || t.IsBroken) continue;
                AddHpItem(items, t.gameObject.name, t, t.HpFraction, t.IsBroken, t.Repair,
                    () => t.IsBroken ? 1f : 1f - Mathf.Clamp01(t.HpFraction));
            }
            foreach (var t in UnityEngine.Object.FindObjectsByType<ArcaneTower>(FindObjectsSortMode.None))
            {
                if (t == null || t.IsBroken) continue;
                AddHpItem(items, t.gameObject.name, t, t.HpFraction, t.IsBroken, t.Repair,
                    () => t.IsBroken ? 1f : 1f - Mathf.Clamp01(t.HpFraction));
            }
            foreach (var t in UnityEngine.Object.FindObjectsByType<DeNelle.Village.World.HarvestSite>(FindObjectsSortMode.None))
            {
                if (t == null || t.IsBroken) continue;
                AddHpItem(items, t.gameObject.name, t, t.HpFraction, t.IsBroken, t.Repair,
                    () => t.IsBroken ? 1f : 1f - Mathf.Clamp01(t.HpFraction));
            }

            foreach (var c in ResourceCollectorRegistry.All)
            {
                if (c == null || c.IsBroken) continue;
                float frac = 1f - Mathf.Clamp01(c.HpFraction);
                if (frac <= 0.0001f) continue;
                var cc = c;
                items.Add(new RepairAllItem
                {
                    Name = c.BuildingId,
                    DamageFraction = frac,
                    // Priced from its Collector catalog row (was free pre-ruling).
                    Cost = CostForStructure(c, frac),
                    Fix = () => cc.Repair(),
                    FractionNow = () => cc.IsBroken ? 1f : 1f - Mathf.Clamp01(cc.HpFraction),
                });
            }

            return items;
        }

        /// <summary>Adds one costed item for an HpFraction/IsBroken structure when damaged
        /// — priced from the structure's own catalog row via <see cref="CostForStructure"/>.</summary>
        private void AddHpItem(List<RepairAllItem> items, string name, Component structure,
            float hpFraction, bool broken, Action repair, Func<float> fractionNow)
        {
            float frac = broken ? 1f : 1f - Mathf.Clamp01(hpFraction);
            if (frac <= 0.0001f) return;
            items.Add(new RepairAllItem
            {
                Name = name,
                DamageFraction = frac,
                Cost = CostForStructure(structure, frac),
                Fix = repair,
                FractionNow = fractionNow,
            });
        }

        // =====================================================================
        //  Confirm — spend materials + repair (owner ruling 2026-07-11)
        // =====================================================================

        /// <summary>
        /// Confirms the repair/rebuild of the selected structure. Checks the
        /// material wallets, spends the in-kind cost through the construction
        /// economy (<see cref="SpendMaterials"/> — the SAME EconomyService path
        /// build-mode placement charges), calls <see cref="RepairTarget.RepairFull"/>
        /// (full restore BY CONTRACT, REP-1) and raises success feedback.
        /// On a shortfall it raises an insufficient-materials message and leaves
        /// the prompt up so the player can cancel. Bound to the HUD prompt's
        /// Confirm button by the scene-setup editor file.
        /// </summary>
        public void ConfirmRepair()
        {
            if (_selected == null || !_selected.IsValid)
            {
                ClearSelection();
                return;
            }

            if (!_selected.NeedsRepair)
            {
                FeedbackShown?.Invoke(WallRepairStrings.IntactMessage, false);
                ClearSelection();
                return;
            }

            CoreCost cost = CostFor(_selected);
            bool rebuild = _selected.DamageFraction >= DestroyedFraction;
            if (!CanAffordMaterials(cost))
            {
                FeedbackShown?.Invoke(
                    string.Format(WallRepairStrings.InsufficientFormat, DescribeMaterials(cost)), true);
                // Keep the prompt up but refresh affordability so the HUD greys
                // the confirm button.
                RaisePrompt();
                return;
            }

            if (!SpendMaterials(cost, (rebuild ? "rebuild " : "repair ") + _selected.DisplayName))
            {
                FeedbackShown?.Invoke(
                    string.Format(WallRepairStrings.InsufficientFormat, DescribeMaterials(cost)), true);
                RaisePrompt();
                return;
            }

            // REP-1: full repair BY CONTRACT — RepairFull resolves each structure's
            // own full-restore magnitude. The old hardcoded Repair(100f) assumed
            // every structure was 0..100-scaled; buildings.json authors MaxHp
            // 120..240, so a paid repair left buildings visibly damaged.
            _selected.RepairFull();

            FeedbackShown?.Invoke(
                rebuild ? WallRepairStrings.RebuiltMessage : WallRepairStrings.SuccessMessage, false);
            ClearSelection();
            Rescan();
        }

        // =====================================================================
        //  Materials wallet — THE construction-economy spend path (WO-131 seam)
        // =====================================================================

        /// <summary>
        /// True when the material wallets cover <paramref name="cost"/> — the
        /// SAME EconomyService.CanAfford gate build-mode placement validates
        /// against (BuildModeController.CanAfford). A free cost is affordable.
        /// </summary>
        public bool CanAffordMaterials(CoreCost cost)
        {
            if (MaterialsZero(cost)) return true;
            var econ = EconomyService.Instance;
            return econ != null && econ.CanAfford(BuildModeController.ToEconomy(cost));
        }

        /// <summary>
        /// Spends an in-kind materials cost through the SAME construction-economy
        /// path build-mode placement charges: <see cref="EconomyService.TrySpend"/>
        /// (the atomic multi-resource debit BuildModeController.ChargeLedger
        /// drives at placement, WO-131). WO-842: TrySpend now debits the SINGLE
        /// GameState-backed wallet (GameState.Wood/Iron — the same fields
        /// ResourceLedger spends) and persists+announces it itself, so the old
        /// dual-wallet DEBIT mirror this method carried is GONE — re-debiting
        /// GameState here would charge the repair TWICE. FlowTrace on every
        /// spend + fail — no silent path.
        /// </summary>
        private bool SpendMaterials(CoreCost cost, string what)
        {
            if (MaterialsZero(cost)) return true;

            var econ = EconomyService.Instance;
            if (econ == null)
            {
                FlowTrace.Fail("Repair",
                    $"spend BLOCKED for '{what}' — EconomyService absent (cost {DescribeMaterials(cost)})");
                return false;
            }

            if (!econ.TrySpend(BuildModeController.ToEconomy(cost)))
            {
                FlowTrace.Step("Repair",
                    $"spend REFUSED for '{what}' — cost {DescribeMaterials(cost)} > wallet {WalletLine()}");
                return false;
            }

            FlowTrace.Step("Repair",
                $"SPENT {DescribeMaterials(cost)} on '{what}' (EconomyService.TrySpend, unified GameState wallet WO-842; wallet now {WalletLine()})");
            return true;
        }

        // =====================================================================
        //  Input — LEGACY Input Manager (workstream constraint)
        // =====================================================================

        private static bool TapPressedThisFrame()
        {
            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                return t.phase == TouchPhase.Began;
            }
            return Input.GetMouseButtonDown(0);
        }

        private static Vector2 PointerScreenPosition()
        {
            if (Input.touchCount > 0) return Input.GetTouch(0).position;
            return Input.mousePosition;
        }

        /// <summary>
        /// Tells the controller to ignore world taps for the current + next
        /// frame. The HUD bridge calls this when the repair prompt's Confirm /
        /// Cancel button consumes a pointer-press, so that same OS click is not
        /// also raycast into the world (UI Toolkit and this MonoBehaviour both
        /// see the one click). A two-frame window covers same-frame and
        /// next-frame ordering between the UI event and Update().
        /// </summary>
        public void SuppressNextWorldTap()
        {
            _suppressTapUntilFrame = Time.frameCount + 1;
        }
    }
}
