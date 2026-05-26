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
//   3. PROMPT    — a crystal cost is computed and shown (the HUD repair prompt).
//                  The prompt is MODAL: while it is open every tap belongs to
//                  the prompt's Confirm / Cancel buttons, never the world — the
//                  interaction stays deterministic.
//   4. CONFIRM   — on confirm: if the crystal balance covers the cost, deduct
//                  the crystals (GameStateService, mirroring BuildMenu) and call
//                  the structure's existing Repair(); otherwise show an
//                  insufficient-funds message.
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
using DeNelle.Core.State;

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
        /// "Repair the North Gate?". Composed here (the HUD shows it verbatim).
        /// </summary>
        public string Subtitle;
        /// <summary>Crystal cost to repair the selected structure.</summary>
        public int CrystalCost;
        /// <summary>True when the current crystal balance covers <see cref="CrystalCost"/>.</summary>
        public bool Affordable;
        /// <summary>Damage fraction 0..1 of the selected structure.</summary>
        public float DamageFraction;
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
    /// structures, tap-to-select, show a crystal cost, deduct + repair on
    /// confirm. A self-contained Village sub-system MonoBehaviour — the
    /// scene-setup editor file adds it to the built village scene.
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

        [Header("Repair cost")]
        [Tooltip("Crystal cost of a FULL repair of an undamaged-to-destroyed structure. " +
                 "The shown cost scales with how damaged the picked structure actually is.")]
        [SerializeField, Min(1)] private int _fullRepairCost = 40;

        [Tooltip("Minimum crystal cost for any repair, however light the damage.")]
        [SerializeField, Min(1)] private int _minRepairCost = 5;

        [Tooltip("When true, crystals are spent through GameStateService (the real save). " +
                 "When false, the local test balance below is used (standalone testing).")]
        [SerializeField] private bool _useGameState = true;

        [Tooltip("Crystal balance used when GameState is unavailable (standalone testing only).")]
        [SerializeField, Min(0)] private int _localCrystalBalance = 200;

        [Header("Scanning")]
        [Tooltip("Seconds between damaged-structure rescans. Keeps the highlight set fresh " +
                 "without scanning every frame.")]
        [SerializeField, Min(0.1f)] private float _rescanInterval = 0.75f;

        [Tooltip("When true the controller scans the whole scene for WallSegment / Gate / " +
                 "Building. When false only the structures registered via RegisterStructures().")]
        [SerializeField] private bool _autoFindStructures = true;

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

            // Keep the highlight pool sized to the damaged set, repositioning each
            // marker over its structure. The selected structure gets the bright
            // marker instead of a pool one.
            EnsureHighlightRoot();
            int poolIndex = 0;
            for (int i = 0; i < _damaged.Count; i++)
            {
                var t = _damaged[i];
                if (_selected != null && _selected.SameAs(t)) continue; // selection marker covers it

                RepairHighlight hl = GetPooledHighlight(poolIndex++);
                hl.SetVisible(true);
                hl.SetSelected(false);
                hl.FitTo(t);
            }
            // Hide any surplus pooled markers.
            for (int i = poolIndex; i < _highlightPool.Count; i++)
                _highlightPool[i].SetVisible(false);

            if (_damaged.Count != _lastDamagedCount)
            {
                _lastDamagedCount = _damaged.Count;
                DamagedCountChanged?.Invoke(_damaged.Count);
            }
        }

        /// <summary>Fills <paramref name="into"/> with a RepairTarget for every damaged structure.</summary>
        private void CollectDamaged(List<RepairTarget> into)
        {
            if (_autoFindStructures)
            {
                AddDamagedOfType<WallSegment>(into);
                AddDamagedOfType<Gate>(into);
                AddDamagedOfType<Building>(into);
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

        private static void AddDamagedOfType<T>(List<RepairTarget> into) where T : Component
        {
#if UNITY_2023_1_OR_NEWER
            var found = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            var found = UnityEngine.Object.FindObjectsOfType<T>();
#endif
            foreach (var c in found)
            {
                if (c == null) continue;
                var t = RepairTarget.TryWrap(c);
                if (t != null && t.IsValid && t.NeedsRepair) into.Add(t);
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
                return; // tapped something that is not a repairable structure.

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
            Rescan();
            if (_damaged.Count == 0) return false;
            int best = 0;
            for (int i = 1; i < _damaged.Count; i++)
                if (_damaged[i].DamageFraction > _damaged[best].DamageFraction) best = i;
            RequestRepair(_damaged[best]);
            return true;
        }

        /// <summary>
        /// Selects <paramref name="target"/> for repair: marks it with the bright
        /// highlight and raises <see cref="PromptShown"/> so the HUD shows the
        /// crystal cost. An undamaged structure is rejected with feedback.
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
            int cost = CostFor(_selected);
            string name = _selected.DisplayName;
            PromptShown?.Invoke(new RepairPromptInfo
            {
                StructureName = name,
                Subtitle = string.Format(WallRepairStrings.SubtitleFormat, name),
                CrystalCost = cost,
                Affordable = CrystalBalance >= cost,
                DamageFraction = _selected.DamageFraction,
            });
        }

        // =====================================================================
        //  Cost
        // =====================================================================

        /// <summary>
        /// Crystal cost to repair <paramref name="target"/> — the full-repair
        /// cost scaled by how damaged the structure is, floored at the minimum.
        /// </summary>
        public int CostFor(RepairTarget target)
        {
            if (target == null || !target.IsValid) return _minRepairCost;
            float frac = Mathf.Clamp01(target.DamageFraction);
            int scaled = Mathf.CeilToInt(_fullRepairCost * frac);
            return Mathf.Max(_minRepairCost, scaled);
        }

        // =====================================================================
        //  Confirm — deduct crystals + repair
        // =====================================================================

        /// <summary>
        /// Confirms the repair of the selected structure. Checks the crystal
        /// balance, deducts the cost, calls the structure's existing
        /// <c>Repair()</c> primitive (full repair) and raises success feedback.
        /// On a short balance it raises an insufficient-funds message and leaves
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

            int cost = CostFor(_selected);
            if (CrystalBalance < cost)
            {
                FeedbackShown?.Invoke(
                    string.Format(WallRepairStrings.InsufficientFormat, cost), true);
                // Keep the prompt up but refresh affordability so the HUD greys
                // the confirm button.
                RaisePrompt();
                return;
            }

            if (!SpendCrystals(cost))
            {
                FeedbackShown?.Invoke(
                    string.Format(WallRepairStrings.InsufficientFormat, cost), true);
                RaisePrompt();
                return;
            }

            // Repair() on each structure clamps to pristine — pass a full
            // 0..100-scale repair so the structure is fully restored.
            _selected.Repair(100f);

            FeedbackShown?.Invoke(WallRepairStrings.SuccessMessage, false);
            ClearSelection();
            Rescan();
        }

        // =====================================================================
        //  Crystal balance — GameState-backed or local (mirrors BuildMenu.cs)
        // =====================================================================

        /// <summary>The current crystal balance the repair flow spends from.</summary>
        public int CrystalBalance
        {
            get
            {
                if (_useGameState)
                {
                    var service = GameStateService.Instance;
                    if (service != null && service.State != null)
                        return service.State.Resources.Crystals;
                }
                return _localCrystalBalance;
            }
        }

        /// <summary>
        /// Spends <paramref name="amount"/> crystals. Mirrors BuildMenu.SpendCrystals
        /// — mutates <see cref="GameState.Resources"/> (a struct, written back
        /// whole), persists via <see cref="GameStateService.Save"/> and raises
        /// <c>ResourcesChanged</c> so the HUD crystal counter refreshes. Returns
        /// false when the balance is short.
        /// </summary>
        private bool SpendCrystals(int amount)
        {
            if (amount <= 0) return true;

            if (_useGameState)
            {
                var service = GameStateService.Instance;
                if (service != null && service.State != null)
                {
                    var r = service.State.Resources;
                    if (r.Crystals < amount) return false;
                    r.Crystals -= amount;
                    service.State.Resources = r;
                    service.Save();
                    service.ResourcesChanged.Invoke();
                    return true;
                }
            }

            if (_localCrystalBalance < amount) return false;
            _localCrystalBalance -= amount;
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
