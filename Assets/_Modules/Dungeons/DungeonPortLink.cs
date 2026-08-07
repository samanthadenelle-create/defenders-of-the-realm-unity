// =============================================================================
// DungeonPortLink — a simple interact-to-port traversal link (WO-711 items 3-4).
// -----------------------------------------------------------------------------
// Owner rulings (live dungeon walk, 2026-07-13, verbatim):
//   "ANYWHERE WITH A DOOR, USE DOOR ACTION NAV-LINK PORT FROM ONE SIDE OF DOOR
//    TO OTHER" · "SAME WITH STEPS GOING UP" · "WE CAN COOK LATER BUT FOR NOW
//    SIMPLE."
//
// One link = one SIDE of a door/staircase. The Keeper walks into the link's
// radius, an interact prompt shows ("Open Door" / "Climb") on the shared
// MobileInteractButton (touch) plus the desktop [F] key, and on interact the
// hero is ported to the paired point on the other side / the other landing:
// short fade (ScreenFader, the HomeReturnPortalInjector port idiom) -> warp
// via DungeonHero.Teleport (the CharacterController-safe warp — it disables
// the controller across the move so it never fights/rubber-bands) -> face
// onward -> fade in. Links are authored at runtime by
// DungeonController.DressTraversalLinks() — never hand-placed in the scene
// (CLAUDE.md section 3: no scene hand-edits).
//
// Simplest honest form per the owner's scope law: no door-swing animation, no
// nav-link mesh, no stair walking — a proximity prompt and a port. Cook later.
// =============================================================================

using Cysharp.Threading.Tasks;
using DeNelle.Core.Diagnostics;
using DeNelle.Village;
using DeNelle.Village.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// One side of a door/stair traversal port. Proximity prompt (shared
    /// MobileInteractButton + desktop [F]) -> short fade -> teleport the Keeper
    /// to the paired point on the other side, facing onward.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonPortLink : MonoBehaviour
    {
        /// <summary>Fade timings — short and cheap (owner: simple; cook later).</summary>
        private const float FadeOutSeconds = 0.15f;
        private const float FadeInSeconds = 0.2f;

        // ── Configured at author time (DressTraversalLinks) ─────────────────

        // [SerializeField] IS LOad-BEARING, not decoration (WO-1001 slice 1b fix).
        // The COMPOSED path configures these AT BAKE TIME (DungeonBaker.PlaceStairPort) and then
        // saves the scene. Unity serializes only public fields and [SerializeField] privates, so
        // while these were plain privates every value was DISCARDED by SaveScene — and Update()'s
        // first line bails on `_hero == null`, so every baked stair port was inert forever. The
        // bake still reported saved=True and no gate noticed. The cottage path was unaffected
        // because DungeonController re-Configures at runtime, which is exactly why this hid.
        [SerializeField] private string _prompt = "Open Door";
        [SerializeField] private Vector3 _target;
        [SerializeField] private float _targetFacingY;
        [SerializeField] private Transform _hero;
        private DungeonHero _heroController;   // runtime-only (cottage assigns; composed passes null)
        [SerializeField] private float _radius = 2.5f;
        [SerializeField] private string _fromLabel = "?";
        [SerializeField] private string _toLabel = "?";

        // ── Runtime ──────────────────────────────────────────────────────────

        private bool _inRange;
        private bool _porting;
        private bool _heroRebindTried;

        /// <summary>
        /// Wires the link. <paramref name="target"/> is the paired point on the
        /// other side of the door / the other stair landing (already
        /// ground-band-seated by the dress pass); <paramref name="targetFacingY"/>
        /// faces the Keeper onward after the port.
        /// </summary>
        public void Configure(
            string prompt, Vector3 target, float targetFacingY,
            Transform hero, DungeonHero heroController,
            string fromLabel, string toLabel, float radius = 2.5f)
        {
            _prompt = string.IsNullOrEmpty(prompt) ? "Open Door" : prompt;
            _target = target;
            _targetFacingY = targetFacingY;
            _hero = hero;
            _heroController = heroController;
            _fromLabel = fromLabel ?? "?";
            _toLabel = toLabel ?? "?";
            _radius = Mathf.Max(0.5f, radius);
        }

        /// <summary>
        /// Safety net for the composed path: a serialized Transform reference survives the bake,
        /// but a hero re-created at runtime would leave it null and silently kill the port. Rebind
        /// ONCE off the Player tag and say so, rather than sitting inert with no explanation.
        /// </summary>
        private bool TryRebindHero()
        {
            if (_hero != null) return true;
            if (_heroRebindTried) return false;
            _heroRebindTried = true;

            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged == null)
            {
                FlowTrace.Warn("Dungeon", $"PortLink '{name}': no serialized hero AND no Player-tagged object - this port is INERT.");
                return false;
            }
            _hero = tagged.transform;
            FlowTrace.Step("Dungeon", $"PortLink '{name}': hero reference was null - rebound to '{tagged.name}' via the Player tag.");
            return true;
        }

        private void Update()
        {
            if (_porting) return;
            if (_hero == null && !TryRebindHero()) return;

            // FULL 3D distance — the cottage stacks three levels over the same
            // XZ footprint (ground Y=0 / loft Y=6 / cellar Y=-6), so a planar
            // check would arm a ground-floor door while the Keeper stands in
            // the loft above it.
            bool nowInRange =
                (_hero.position - transform.position).sqrMagnitude <= _radius * _radius;

            if (nowInRange != _inRange)
            {
                _inRange = nowInRange;
                if (!_inRange) MobileInteractButton.Release(this);
            }

            if (!_inRange || MobileInteractButton.Suppressed) return;

            // Shared touch button — must be requested every frame while in range.
            MobileInteractButton.Request(this, _prompt, Port);

            // Desktop [F] — the dungeon runs on the Input System (DungeonHero).
            var kb = Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame) Port();
        }

        /// <summary>Fires the port (prompt tap / [F]). Re-entry guarded.</summary>
        public void Port()
        {
            if (_porting || _hero == null) return;
            _porting = true;
            MobileInteractButton.Release(this);
            Guard.Try("Dungeon", $"port link '{name}'", () => PortAsync().Forget());
        }

        private async UniTaskVoid PortAsync()
        {
            // Step IN / step OUT around the whole traversal: the scope logs "-> ..." here and
            // "<- ... (Xms)" on dispose, so a port that starts and never finishes (a fade that
            // never returns, a warp that throws) is visible as an ENTER with no matching EXIT
            // rather than as silence.
            using var _scope = FlowTrace.Enter("Dungeon", $"PortLink '{name}' {_fromLabel}->{_toLabel}");

            FlowTrace.Step("Dungeon",
                $"PortLink '{name}': '{_prompt}' from '{_fromLabel}' {_hero.position} " +
                $"-> '{_toLabel}' {_target}.");

            // Short fade masks the cut (the HomeReturnPortalInjector idiom);
            // ScreenFader lazily self-installs and never throws.
            ScreenFader fader = null;
            Guard.Try("Dungeon", "port fade-out", () =>
            {
                fader = ScreenFader.EnsureInstalled();
            });
            if (fader != null) await fader.FadeOut(FadeOutSeconds);

            WarpHero();

            if (fader != null) await fader.FadeIn(FadeInSeconds);
            _porting = false;
        }

        /// <summary>
        /// The warp itself. Prefer <see cref="DungeonHero.Teleport"/> (CharacterController
        /// cottage path). WO-1001 slice 1b: composed dungeons bake a
        /// <see cref="HeroLocomotion"/> hero instead — use its WarpTo so the NavMeshAgent
        /// re-seats on the destination floor island (triggered stair / multi-level).
        /// Raw transform + CC toggle is last-resort, mirroring DungeonController.PlaceHero.
        /// </summary>
        private void WarpHero()
        {
            if (_heroController != null)
            {
                _heroController.Teleport(_target, _targetFacingY);
                FlowTrace.Step("Dungeon",
                    $"PortLink '{name}': warped (DungeonHero.Teleport) -> {_target}, " +
                    $"facing {_targetFacingY:0}.");
                return;
            }

            if (_hero == null) return;

            // Composed Pipeline A hero (DungeonBaker.PopulateForPlay) — NavMeshAgent mover.
            var loco = _hero.GetComponent<HeroLocomotion>();
            if (loco != null)
            {
                loco.WarpTo(_target, Quaternion.Euler(0f, _targetFacingY, 0f));
                FlowTrace.Step("Dungeon",
                    $"PortLink '{name}': warped (HeroLocomotion.WarpTo) -> {_target}, " +
                    $"facing {_targetFacingY:0} (WO-1001 1b multi-level stair port).");
                return;
            }

            var cc = _hero.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            _hero.position = _target;
            _hero.rotation = Quaternion.Euler(0f, _targetFacingY, 0f);
            if (cc != null) cc.enabled = true;
            FlowTrace.Step("Dungeon",
                $"PortLink '{name}': warped (raw transform fallback) -> {_target}.");
        }

        private void OnDisable()
        {
            _inRange = false;
            MobileInteractButton.Release(this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.95f, 0.75f, 0.3f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _radius);
            Gizmos.DrawLine(transform.position, _target);
            Gizmos.DrawWireSphere(_target, 0.4f);
        }
    }
}
