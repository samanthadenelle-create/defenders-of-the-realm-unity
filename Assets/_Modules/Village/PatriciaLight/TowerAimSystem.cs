// =============================================================================
// TowerAimSystem — manual-aim reticle + aim-assisted target resolution for the
// Defend-the-Tower mode.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Defend
//
// INPUT-SOURCE AGNOSTIC BY DESIGN (do-once-do-right):
//   The reticle is moved, and firing is signalled, through a small public API:
//     AddAimDelta / SetReticleScreen / SignalFire / FireHeld.
//   Any driver can feed it — the Lean Touch mobile driver (drag = AddAimDelta,
//   tap = SignalFire, pinch = camera AddZoom) is the PRIMARY path; a built-in
//   editor/desktop fallback (mouse + I/J/K/L + right-stick) lets us iterate in
//   the editor and in the Windows build without deploying to a phone. Adding the
//   touch driver requires NO change to this file.
//
//   Target resolution = the live hostile whose SCREEN position is nearest the
//   reticle within an assist radius (aim-assist). Because the reticle lives in
//   the viewport, the locked target is always on-screen / in front — this is what
//   makes backward shots impossible (replaces the old NearestHostile face-snap).
//
//   Renders the crosshair + a lock box via IMGUI so it ALWAYS shows in player
//   builds (UI-Toolkit HUDs have repeatedly come up empty here).
// =============================================================================

using UnityEngine;
using UnityEngine.InputSystem;
using DeNelle.Core.Combat;

namespace DeNelle.Village.Defend
{
    [DisallowMultipleComponent]
    public sealed class TowerAimSystem : MonoBehaviour
    {
        // ── Wiring set by PatriciaLightController ─────────────────────────────
        public Camera    Cam;          // the active follow camera
        public Transform AimOrigin;    // the hero (search centre for targets)
        public float     Range = 60f;  // world search radius for hostiles

        // ── Tuning ────────────────────────────────────────────────────────────
        [SerializeField] private float _assistRadiusPx   = 95f;    // reticle-to-enemy lock distance (screen px)
        [SerializeField] private float _keyNudgePxPerSec  = 1100f;  // editor key/stick reticle speed
        [SerializeField] private float _crosshairSize     = 34f;

        // ── State ─────────────────────────────────────────────────────────────
        private Vector2 _reticle;
        private bool    _init;
        private bool    _externalDriver;     // a touch driver has taken over → suppress desktop fallback
        private float   _externalFireUntil;  // SignalFire hold window
        private readonly Collider[] _buf = new Collider[64];

        private IDamageable _current;         // cached lock target (refreshed each frame)
        private bool    _lockValid;
        private Vector2 _lockScreen;

        /// <summary>The aim-assisted hostile currently under the reticle (or null).</summary>
        public IDamageable CurrentTarget => (_current != null && _current.IsAlive) ? _current : null;
        public Vector2 Reticle => _reticle;

        // ── Public driver API (Lean Touch / any input source calls these) ─────
        /// <summary>Move the reticle by a screen-space delta (e.g. a touch drag).</summary>
        public void AddAimDelta(Vector2 screenDelta) { _externalDriver = true; _reticle += screenDelta; Clamp(); }
        /// <summary>Set the reticle to an absolute screen position (e.g. a tap point).</summary>
        public void SetReticleScreen(Vector2 screenPos) { _externalDriver = true; _reticle = screenPos; Clamp(); }
        /// <summary>Request a fire for the next <paramref name="holdSeconds"/> (debounced by the shooter's cooldown).</summary>
        public void SignalFire(float holdSeconds = 0.12f) { _externalFireUntil = Time.time + Mathf.Max(0.01f, holdSeconds); }
        /// <summary>Declare that an external driver owns input (suppresses the desktop fallback) without moving the reticle.</summary>
        public void ClaimExternalDriver() { _externalDriver = true; }

        private void OnEnable() => EnsureInit();

        private void EnsureInit()
        {
            if (_init) return;
            _init = true;
            _reticle = new Vector2(Screen.width * 0.5f, Screen.height * 0.52f);
        }

        private void Update()
        {
            EnsureInit();
            if (!_externalDriver) DesktopFallback();
            Clamp();
            ScanTarget();   // refresh the lock every frame so the crosshair reads live
        }

        // Editor / Windows-build input so we can iterate without a phone. Mouse
        // moves the reticle; I/J/K/L nudge it (kept off A/D + arrows, which strafe);
        // right-stick nudges on a gamepad.
        private void DesktopFallback()
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.delta.ReadValue().sqrMagnitude > 0.01f)
                _reticle = mouse.position.ReadValue();

            Vector2 nudge = Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.jKey.isPressed) nudge.x -= 1f;
                if (kb.lKey.isPressed) nudge.x += 1f;
                if (kb.kKey.isPressed) nudge.y -= 1f;
                if (kb.iKey.isPressed) nudge.y += 1f;
            }
            var gp = Gamepad.current;
            if (gp != null)
            {
                Vector2 r = gp.rightStick.ReadValue();
                if (r.sqrMagnitude > 0.02f) nudge += r;
            }
            if (nudge != Vector2.zero)
                _reticle += nudge * _keyNudgePxPerSec * Time.deltaTime;
        }

        /// <summary>True while the player is holding fire (touch SignalFire, or the desktop fallback).</summary>
        public bool FireHeld
        {
            get
            {
                if (Time.time < _externalFireUntil) return true;
                if (_externalDriver) return false;   // touch only fires via SignalFire
                var m = Mouse.current;    if (m != null && m.leftButton.isPressed) return true;
                var k = Keyboard.current; if (k != null && k.spaceKey.isPressed)   return true;
                var g = Gamepad.current;  if (g != null && (g.rightTrigger.isPressed || g.buttonSouth.isPressed)) return true;
                return false;
            }
        }

        private void Clamp()
        {
            _reticle.x = Mathf.Clamp(_reticle.x, 0f, Screen.width);
            _reticle.y = Mathf.Clamp(_reticle.y, 0f, Screen.height);
        }

        // Nearest live hostile to the reticle in screen space, within the assist radius.
        private void ScanTarget()
        {
            _lockValid = false;
            _current   = null;
            if (Cam == null) Cam = Camera.main;
            if (Cam == null || AimOrigin == null) return;

            int n = Physics.OverlapSphereNonAlloc(
                AimOrigin.position, Range, _buf, ~0, QueryTriggerInteraction.Collide);
            float bestPx = _assistRadiusPx;
            for (int i = 0; i < n; i++)
            {
                var col = _buf[i];
                if (col == null) continue;
                var dmg = col.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive || dmg.Faction != CombatFaction.Hostile) continue;

                Vector3 sp = Cam.WorldToScreenPoint(dmg.WorldPosition + Vector3.up * 0.6f);
                if (sp.z <= 0f) continue;   // behind the camera
                float d = Vector2.Distance(new Vector2(sp.x, sp.y), _reticle);
                if (d < bestPx)
                {
                    bestPx = d; _current = dmg;
                    _lockScreen = new Vector2(sp.x, sp.y); _lockValid = true;
                }
            }
        }

        // ── Crosshair render (IMGUI — always shows in builds) ─────────────────
        private static Texture2D Px => Texture2D.whiteTexture;

        private void OnGUI()
        {
            EnsureInit();
            float cx = _reticle.x, cy = Screen.height - _reticle.y;   // GUI y is top-down
            Color c = _lockValid ? new Color(1f, 0.35f, 0.25f, 0.95f) : new Color(1f, 1f, 1f, 0.8f);
            float s = _crosshairSize, t = 2f, gap = 6f;

            GUI.color = c;
            GUI.DrawTexture(new Rect(cx - s, cy - t * 0.5f, s - gap, t), Px);            // left tick
            GUI.DrawTexture(new Rect(cx + gap, cy - t * 0.5f, s - gap, t), Px);          // right tick
            GUI.DrawTexture(new Rect(cx - t * 0.5f, cy - s, t, s - gap), Px);            // top tick
            GUI.DrawTexture(new Rect(cx - t * 0.5f, cy + gap, t, s - gap), Px);          // bottom tick
            GUI.DrawTexture(new Rect(cx - 1.5f, cy - 1.5f, 3f, 3f), Px);                 // centre dot

            if (_lockValid)
            {
                float lx = _lockScreen.x, ly = Screen.height - _lockScreen.y, b = 22f;
                GUI.color = new Color(1f, 0.30f, 0.20f, 0.9f);
                GUI.DrawTexture(new Rect(lx - b, ly - b, 2f * b, 2f), Px);
                GUI.DrawTexture(new Rect(lx - b, ly + b - 2f, 2f * b, 2f), Px);
                GUI.DrawTexture(new Rect(lx - b, ly - b, 2f, 2f * b), Px);
                GUI.DrawTexture(new Rect(lx + b - 2f, ly - b, 2f, 2f * b), Px);
            }
            GUI.color = Color.white;
        }
    }
}
