// =============================================================================
// RaidDeployController — the troop DEPLOY / RALLY / RETREAT HUD + tap state machine
// for a raid base (WO-453 Step 4, first-playable).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Lives in a RaidBase_* scene (self-installs via a RuntimeInitialize hook when the
// loaded scene is enemy-owned AND its name starts "RaidBase"). It is the player's
// command surface for the assault:
//
//   DEPLOY  — tap a troop tile in the bottom tray to ARM that TroopDefId, then tap
//             the ground (RaycastGround, the BuildMode-proven world tap) to drop one
//             deployable PlayerTroop of that type onto the NavMesh. Quantity drains
//             over multiple taps (one PlayerTroop per tap); the tile count counts down.
//             Each drop spawns through the canonical TroopDeployer.SpawnFromArmy path
//             (stamps OwnedTroopId + applies the veterancy DamageMultiplier).
//   RALLY   — toggle Rally on, then tap the ground to set the global TroopRally.Point.
//             Idle troops (no foe in range) walk to it; a foe in range ALWAYS wins
//             (rally only fills the idle gap — owner-decided default).
//   RETREAT — survivors = the living deployed bodies' OwnedTroopIds; reconcile the
//             army (deployed-but-not-survivor → wounded) and evac home via GoCastle.
//
// Code-built uGUI (NO UXML — repo rule). NON-modal: a bottom tray + Rally/Retreat
// buttons that never blacken the screen. Input is the NEW Input System (Mouse.current,
// never legacy Input.*), with an optional Lean.Touch tap mirrored in for mobile.
//
// SCOPE (first playable): win/stars are OUT — only the loss/RETREAT exit is wired.
// Deploy anywhere on the NavMesh (no zone gating). Tunables are [SerializeField] so
// the owner can tune by feel later.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DeNelle.Core;
using DeNelle.Core.State;
using DeNelle.Core.UI;

namespace DeNelle.Village
{
    /// <summary>
    /// The raid-base troop command HUD: a bottom troop tray + Rally toggle + Retreat
    /// button, plus the DEPLOY / RALLY tap state machine. Self-installs into a
    /// <c>RaidBase_*</c> enemy-owned scene; spawns through <see cref="TroopDeployer"/>
    /// and exits via <see cref="SceneRouter.GoCastle"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RaidDeployController : MonoBehaviour
    {
        // ── Tunables (owner tunes by feel later) ──────────────────────────────
        [Header("Deploy")]
        [Tooltip("Lateral spread (m) between troops dropped from repeated taps of the same tile.")]
        [SerializeField] private float _deploySpread = 1.1f;
        [Tooltip("Ground raycast distance (m) for a deploy / rally tap.")]
        [SerializeField] private float _rayDistance = 800f;
        [Tooltip("Layer mask the deploy/rally tap ray tests first (falls back to all layers).")]
        [SerializeField] private LayerMask _groundMask = ~0;

        [Header("Rally")]
        [Tooltip("Arrival epsilon (m) — a troop within this of the rally point idles instead of jittering. " +
                 "Mirrors TroopController.RallyArrivalEpsilon; kept here so the owner can expose/tune it.")]
        [SerializeField] private float _rallyArrivalEpsilon = 1.25f;

        [Header("Retreat")]
        [Tooltip("If true, the first Retreat tap asks for confirm (second tap evacs); false = evac immediately.")]
        [SerializeField] private bool _retreatConfirm = true;
        [Tooltip("Recovery seconds applied to every troop that was deployed but did NOT survive the raid.")]
        [SerializeField] private float _recoverySeconds = 120f;

        // ── Runtime UI ────────────────────────────────────────────────────────
        private GameObject _ui;
        private Camera _camera;
        private TMPro.TextMeshProUGUI _status;
        private Button _rallyButton;
        private Button _retreatButton;
        private readonly List<TrayTile> _tiles = new List<TrayTile>();

        // ── Tap state machine ─────────────────────────────────────────────────
        private string _armedDefId;     // the TroopDefId armed for the next ground tap (null = none)
        private bool _rallyMode;        // true while the Rally toggle is on (next tap sets the rally point)
        private bool _retreatPending;   // first Retreat tap, awaiting confirm (when _retreatConfirm)

        // ── Tracking deployed troops (controller + owning army id) ────────────
        private readonly List<Deployed> _deployed = new List<Deployed>();

        // Lean tap latch (mobile) — raised by a Lean.Touch finger tap, consumed in Update.
        private bool _leanTapLatched;
        private Vector2 _leanTapPoint;

        private struct Deployed
        {
            public TroopController Controller;
            public string OwnedId;
        }

        private struct TrayTile
        {
            public string DefId;
            public Button Button;
            public TMPro.TextMeshProUGUI CountLabel;
        }

        // =====================================================================
        //  Self-install — add this controller to a RaidBase_* enemy-owned scene
        // =====================================================================

        /// <summary>
        /// On every scene load, if the active scene is a <c>RaidBase_*</c> the deploy HUD
        /// installs itself (one frame later so SceneOwnership has resolved + the garrison
        /// spawner has marked the scene enemy-owned). Idempotent — never double-installs.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallHook()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            TryInstall(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                                          UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            TryInstall(scene.name);
        }

        private static void TryInstall(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            if (!sceneName.StartsWith("RaidBase", System.StringComparison.OrdinalIgnoreCase)) return;
            if (FindAnyObjectByType<RaidDeployController>() != null) return;

            // Clear any stale rally from a prior raid so it can't leak into this one.
            TroopRally.Clear();

            var go = new GameObject("RaidDeployController");
            go.AddComponent<RaidDeployController>();
            Debug.Log($"[RaidDeployController] self-installed in raid scene '{sceneName}'.");
        }

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        // The raid scorer (WO-771.6) — bound a few frames after Start so its clock can
        // END the raid (retreat) when time runs out. Null when scoring isn't present.
        private RaidScoring _scoring;

        private void Start()
        {
            _camera = Camera.main;
            BuildHud();
            StartCoroutine(BindScoringRoutine());
        }

        private void OnDestroy()
        {
            // Don't let a rally leak across scenes.
            TroopRally.Clear();
            if (_scoring != null) _scoring.OnTimeExpired -= OnRaidTimeExpired;
            if (_rallyFlag != null) Destroy(_rallyFlag);
            if (_ui != null) Destroy(_ui);
        }

        // The scorer self-installs the same frame this HUD does; poll a few frames for
        // it, then subscribe so the 180s clock expiry ends the raid via the retreat path
        // (survivors reconciled, no soft-lock). Mirrors RaidVictoryController.BindRoutine.
        private IEnumerator BindScoringRoutine()
        {
            for (int i = 0; i < 10 && _scoring == null; i++)
            {
                _scoring = RaidScoring.Instance;
                if (_scoring != null) break;
                yield return null;
            }
            if (_scoring != null)
            {
                _scoring.OnTimeExpired -= OnRaidTimeExpired;
                _scoring.OnTimeExpired += OnRaidTimeExpired;
            }
        }

        // The raid clock ran out (RaidScoring.OnTimeExpired): call off the assault and
        // evac through the normal retreat (reconciles survivors/wounded, GoCastle).
        private void OnRaidTimeExpired()
        {
            SetStatus("Time! The assault is called off - your warband retreats.");
            DoRetreat();
        }

        private void Update()
        {
            if (_camera == null) { _camera = Camera.main; if (_camera == null) return; }

            // Read the place/rally tap THIS frame (new Input System mouse, or a Lean tap).
            if (!TryReadTapPoint(out Vector2 screenPoint)) return;

            if (_rallyMode) { HandleRallyTap(screenPoint); return; }
            if (!string.IsNullOrEmpty(_armedDefId)) { HandleDeployTap(screenPoint); return; }
        }

        // =====================================================================
        //  Input — new Input System mouse + optional Lean tap (NO legacy Input.*)
        // =====================================================================

        /// <summary>
        /// True for the single frame a deploy/rally tap is confirmed, with the screen
        /// point of the tap. Reads the new Input System mouse (left-click) and a Lean
        /// touch tap latch (mobile). A tap over a UI element (the tray / buttons) is
        /// rejected so a tile/Rally/Retreat press never also drops a troop behind it.
        /// </summary>
        private bool TryReadTapPoint(out Vector2 screenPoint)
        {
            screenPoint = Vector2.zero;

            if (_leanTapLatched)
            {
                _leanTapLatched = false;
                screenPoint = _leanTapPoint;
                return !IsPointerOverUi(screenPoint);
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                screenPoint = mouse.position.ReadValue();
                return !IsPointerOverUi(screenPoint);
            }
            return false;
        }

        /// <summary>
        /// True when a screen point is over one of THIS HUD's interactable graphics (the
        /// tray tiles / Rally / Retreat). Uses the canvas GraphicRaycaster so a tap meant
        /// for a button never falls through to a ground deploy. Null-safe.
        /// </summary>
        private bool IsPointerOverUi(Vector2 screenPoint)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return false;
            var data = new UnityEngine.EventSystems.PointerEventData(es) { position = screenPoint };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(data, hits);
            foreach (var h in hits)
                if (h.gameObject != null && _ui != null && h.gameObject.transform.IsChildOf(_ui.transform))
                    return true;
            return false;
        }

        /// <summary>
        /// Cursor/finger → ground raycast (the BuildModeController.RaycastGround pattern):
        /// the configured ground mask first, then all layers as a fallback so a scene whose
        /// ground sits on an unexpected layer still resolves a hit.
        /// </summary>
        private bool RaycastGround(Vector2 screenPoint, out RaycastHit hit)
        {
            Ray ray = _camera.ScreenPointToRay(screenPoint);
            if (Physics.Raycast(ray, out hit, _rayDistance, _groundMask)) return true;
            return Physics.Raycast(ray, out hit, _rayDistance, ~0);
        }

        // =====================================================================
        //  DEPLOY
        // =====================================================================

        private void HandleDeployTap(Vector2 screenPoint)
        {
            if (!RaycastGround(screenPoint, out RaycastHit hit))
            {
                SetStatus("Tap on the ground to deploy.");
                return;
            }

            var army = Army();
            if (army == null) { SetStatus("No army to deploy."); return; }

            // The next deployable troop of the armed type (healthy, not already deployed).
            PlayerTroop next = NextDeployableOfType(army, _armedDefId);
            if (next == null)
            {
                SetStatus($"No more {DisplayName(_armedDefId)} ready to deploy.");
                Disarm();
                RefreshTiles();
                return;
            }

            // Spread repeated drops of the same tap-target out around a small ring.
            int stackIndex = CountDeployedOfType(_armedDefId);
            var troop = TroopDeployer.SpawnFromArmy(next, hit.point, stackIndex, _deploySpread);
            if (troop == null)
            {
                SetStatus($"Couldn't deploy {DisplayName(_armedDefId)}.");
                return;
            }

            _deployed.Add(new Deployed { Controller = troop, OwnedId = next.Id });
            // Feed the scorer (WO-771.6): count the deploy + log it for re-watch. Null-safe.
            RaidScoring.Instance?.RecordDeploy(_armedDefId, hit.point);
            SetStatus($"Deployed {DisplayName(_armedDefId)}. Tap again to deploy more.");
            RefreshTiles();
        }

        // The next army troop of this def that is deployable AND not already on the field.
        private PlayerTroop NextDeployableOfType(ArmyStorage army, string defId)
        {
            if (army == null || army.Owned == null || string.IsNullOrEmpty(defId)) return null;
            foreach (var t in army.Owned)
            {
                if (t == null || !t.IsDeployable) continue;
                if (t.TroopDefId != defId) continue;
                if (IsDeployed(t.Id)) continue;
                return t;
            }
            return null;
        }

        private bool IsDeployed(string ownedId)
        {
            if (string.IsNullOrEmpty(ownedId)) return false;
            foreach (var d in _deployed)
                if (d.OwnedId == ownedId) return true;
            return false;
        }

        private int CountDeployedOfType(string defId)
        {
            int n = 0;
            foreach (var d in _deployed)
                if (d.Controller != null && d.Controller.TroopId == defId) n++;
            return n;
        }

        // Remaining deployable (in the army, healthy, not yet on the field) of a def.
        private int RemainingOfType(ArmyStorage army, string defId)
        {
            if (army == null || army.Owned == null) return 0;
            int n = 0;
            foreach (var t in army.Owned)
            {
                if (t == null || !t.IsDeployable) continue;
                if (t.TroopDefId != defId) continue;
                if (IsDeployed(t.Id)) continue;
                n++;
            }
            return n;
        }

        // =====================================================================
        //  RALLY
        // =====================================================================

        private void HandleRallyTap(Vector2 screenPoint)
        {
            if (!RaycastGround(screenPoint, out RaycastHit hit))
            {
                SetStatus("Tap the ground to set the rally point.");
                return;
            }
            TroopRally.Point = hit.point;
            ShowRallyFlag(hit.point);
            DeNelle.Core.Diagnostics.FlowTrace.Step("Raid",
                $"rally point moved to {hit.point} — warband musters there.");
            SetStatus("Rally set — idle troops will muster there.");
        }

        // =====================================================================
        //  RALLY FLAG — a visible muster marker dropped/moved at the rally point
        // =====================================================================
        // WO-457: TroopRally.Point is data only; the player needs to SEE where the
        // warband is rallying. No flag prefab exists, so we build a cheap primitive
        // marker (a thin pole + a gold banner quad) and just MOVE it on each rally
        // tap. Non-colliding (the troops path THROUGH the point), null-safe.
        private GameObject _rallyFlag;

        private void ShowRallyFlag(Vector3 groundPoint)
        {
            if (_rallyFlag == null) _rallyFlag = BuildRallyFlag();
            if (_rallyFlag == null) return;
            _rallyFlag.transform.position = groundPoint;
            _rallyFlag.SetActive(true);
        }

        private GameObject BuildRallyFlag()
        {
            try
            {
                var root = new GameObject("RallyFlag");

                // Thin pole (cylinder, ~2.4m tall). Strip the collider so it never
                // blocks deploy/rally raycasts or troop pathing.
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "Pole";
                pole.transform.SetParent(root.transform, false);
                pole.transform.localScale = new Vector3(0.08f, 1.2f, 0.08f);
                pole.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                StripCollider(pole);
                TintRenderer(pole, new Color(0.32f, 0.22f, 0.12f)); // dark wood

                // Gold banner quad near the top of the pole.
                var banner = GameObject.CreatePrimitive(PrimitiveType.Quad);
                banner.name = "Banner";
                banner.transform.SetParent(root.transform, false);
                banner.transform.localScale = new Vector3(0.9f, 0.6f, 1f);
                banner.transform.localPosition = new Vector3(0.45f, 2.0f, 0f);
                StripCollider(banner);
                TintRenderer(banner, new Color(0.85f, 0.68f, 0.22f)); // gilt

                return root;
            }
            catch (System.Exception e)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Raid", "rally flag build failed: " + e.Message);
                return null;
            }
        }

        private static void StripCollider(GameObject go)
        {
            var col = go != null ? go.GetComponent<Collider>() : null;
            if (col != null) Destroy(col);
        }

        private static void TintRenderer(GameObject go, Color c)
        {
            var r = go != null ? go.GetComponent<Renderer>() : null;
            if (r != null && r.material != null) r.material.color = c;
        }

        private void ToggleRally()
        {
            _rallyMode = !_rallyMode;
            if (_rallyMode) Disarm();   // rally + deploy are exclusive arm states
            SetStatus(_rallyMode ? "Rally: tap the ground to set the muster point." : "Rally off.");
            RefreshRallyButton();
        }

        // =====================================================================
        //  RETREAT — the loss/exit path (win/stars are out of first-playable scope)
        // =====================================================================

        private void OnRetreatPressed()
        {
            if (_retreatConfirm && !_retreatPending)
            {
                _retreatPending = true;
                SetStatus("Retreat? Tap again to confirm — survivors come home, the fallen recover.");
                if (_retreatButton != null)
                {
                    var lbl = _retreatButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                    if (lbl != null) lbl.text = "Confirm Retreat";
                }
                return;
            }
            DoRetreat();
        }

        private void DoRetreat()
        {
            var army = Army();
            if (army != null)
            {
                // Survivors = the living deployed bodies' owning ids; everyone else we
                // deployed fell → wounded (recovery countdown). NEVER deleted.
                var deployedIds = new List<string>();
                var survivorIds = new List<string>();
                foreach (var d in _deployed)
                {
                    if (string.IsNullOrEmpty(d.OwnedId)) continue;
                    deployedIds.Add(d.OwnedId);
                    if (d.Controller != null && d.Controller.IsAlive)
                        survivorIds.Add(d.OwnedId);
                }
                army.ReconcileAfterRaid(deployedIds, survivorIds, _recoverySeconds);
                Debug.Log($"[RaidDeployController] retreat — deployed {deployedIds.Count}, " +
                          $"survivors {survivorIds.Count}, wounded {deployedIds.Count - survivorIds.Count}.");
            }

            TroopRally.Clear();
            GameStateService.Instance?.Save();
            SetStatus("Retreating to the castle...");
            SceneRouter.GoCastle();
        }

        // =====================================================================
        //  HUD construction (code-built uGUI, non-modal bottom tray)
        // =====================================================================

        private void BuildHud()
        {
            if (_ui != null) Destroy(_ui);

            // A plain overlay canvas (NOT a modal scrim — the world stays visible/playable).
            _ui = ElarionUiKit.BuildModalCanvas("RaidDeployHud", 30000);

            // Bottom command bar — a framed dark-glass strip across the bottom.
            var bar = ElarionUiKit.Panel(_ui.transform, new Vector2(0.02f, 0.01f), new Vector2(0.98f, 0.16f), deep: true);

            // Status line just above the bar.
            var statusGo = new GameObject("Status", typeof(TMPro.TextMeshProUGUI));
            statusGo.transform.SetParent(_ui.transform, false);
            var sr = statusGo.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(0.02f, 0.165f);
            sr.anchorMax = new Vector2(0.98f, 0.205f);
            sr.offsetMin = Vector2.zero; sr.offsetMax = Vector2.zero;
            _status = statusGo.GetComponent<TMPro.TextMeshProUGUI>();
            _status.fontSize = ElarionUi.FontLabel;
            _status.color = ElarionUi.Parchment;
            _status.alignment = TMPro.TextAlignmentOptions.Center;
            _status.raycastTarget = false;

            BuildTrayTiles(bar.transform);

            // Rally toggle + Retreat — right edge of the bar.
            _rallyButton = ElarionUiKit.Button(bar.transform, "Rally", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.70f, 0.18f), new Vector2(0.83f, 0.82f), ToggleRally);
            _retreatButton = ElarionUiKit.Button(bar.transform, "Retreat", ElarionUiKit.ButtonKind.Danger,
                new Vector2(0.845f, 0.18f), new Vector2(0.985f, 0.82f), OnRetreatPressed);

            SetStatus("Tap a troop, then tap the ground to deploy. Rally to muster. Retreat to leave.");
            RefreshTiles();
            RefreshRallyButton();
        }

        // One tile per troop TYPE the player has deployable in this raid (deduped by def).
        private void BuildTrayTiles(Transform bar)
        {
            _tiles.Clear();
            var army = Army();

            // Distinct deployable def ids, in catalog order so Footman/Archer read stably.
            var defIds = new List<string>();
            if (army != null && army.Owned != null)
            {
                foreach (var t in army.Owned)
                {
                    if (t == null || !t.IsDeployable || string.IsNullOrEmpty(t.TroopDefId)) continue;
                    if (!defIds.Contains(t.TroopDefId)) defIds.Add(t.TroopDefId);
                }
            }

            if (defIds.Count == 0)
            {
                ElarionUiKit.Label(bar, "No troops to deploy — train at the Barracks first.",
                    0.18f, 0.82f, ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                    TMPro.TextAlignmentOptions.Left, 0.03f, 0.68f);
                return;
            }

            // Lay the tiles across the left ~68% of the bar.
            int count = defIds.Count;
            float left = 0.03f, right = 0.68f;
            float w = (right - left) / Mathf.Max(1, count);
            for (int i = 0; i < count; i++)
            {
                string defId = defIds[i];
                float x0 = left + i * w;
                float x1 = x0 + w * 0.94f;

                string label = DisplayName(defId);
                var btn = ElarionUiKit.Button(bar, label, ElarionUiKit.ButtonKind.Gold,
                    new Vector2(x0, 0.18f), new Vector2(x1, 0.82f), () => ArmTile(defId));

                // A small count badge in the tile's top-right corner.
                var countGo = new GameObject("Count", typeof(TMPro.TextMeshProUGUI));
                countGo.transform.SetParent(btn.transform, false);
                var cr = countGo.GetComponent<RectTransform>();
                cr.anchorMin = new Vector2(0.55f, 0.5f);
                cr.anchorMax = new Vector2(0.97f, 0.97f);
                cr.offsetMin = Vector2.zero; cr.offsetMax = Vector2.zero;
                var ct = countGo.GetComponent<TMPro.TextMeshProUGUI>();
                ct.fontSize = ElarionUi.FontLabel;
                ct.color = ElarionUi.Ink;
                ct.alignment = TMPro.TextAlignmentOptions.TopRight;
                ct.raycastTarget = false;

                _tiles.Add(new TrayTile { DefId = defId, Button = btn, CountLabel = ct });
            }
        }

        private void ArmTile(string defId)
        {
            _rallyMode = false;
            RefreshRallyButton();
            _armedDefId = defId;
            SetStatus($"{DisplayName(defId)} armed — tap the ground to deploy.");
            RefreshTiles();
        }

        private void Disarm() => _armedDefId = null;

        // Refresh each tile's remaining count + highlight the armed one.
        private void RefreshTiles()
        {
            var army = Army();
            foreach (var tile in _tiles)
            {
                int remaining = RemainingOfType(army, tile.DefId);
                if (tile.CountLabel != null) tile.CountLabel.text = "x" + remaining;
                if (tile.Button != null)
                {
                    tile.Button.interactable = remaining > 0;
                    // Tint the armed tile's frame brighter via the label colour as a cheap cue.
                    var lbl = tile.Button.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                    if (lbl != null)
                        lbl.color = (tile.DefId == _armedDefId) ? ElarionUi.Affordable : ElarionUi.Ink;
                }
            }
        }

        private void RefreshRallyButton()
        {
            if (_rallyButton == null) return;
            var lbl = _rallyButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (lbl != null) lbl.text = _rallyMode ? "Rally ON" : "Rally";
        }

        private void SetStatus(string s)
        {
            if (_status != null) _status.text = s;
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private static ArmyStorage Army()
        {
            var svc = GameStateService.Instance;
            return svc != null && svc.State != null ? svc.State.Army : null;
        }

        private static string DisplayName(string defId)
        {
            var d = TroopCatalog.Find(defId);
            return d != null && !string.IsNullOrEmpty(d.DisplayName) ? d.DisplayName
                 : (string.IsNullOrEmpty(defId) ? "Troop" : defId);
        }

        // ── Lean.Touch tap (mobile) — latched here, consumed in Update ─────────
        // Cheap mirror of the desktop tap so a phone can deploy/rally too. Lean is
        // already vendored + referenced by this asmdef (see LeanTouchBuildDriver).
        private void OnEnable()  { Lean.Touch.LeanTouch.OnFingerTap += OnLeanTap; }
        private void OnDisable() { Lean.Touch.LeanTouch.OnFingerTap -= OnLeanTap; }

        private void OnLeanTap(Lean.Touch.LeanFinger finger)
        {
            if (finger == null || finger.Index < 0) return;   // skip simulated mouse (desktop path owns it)
            if (finger.IsOverGui) return;                       // a UI tap is handled by the button itself
            _leanTapPoint = finger.ScreenPosition;
            _leanTapLatched = true;
        }
    }
}
