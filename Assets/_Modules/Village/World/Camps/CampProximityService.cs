// =============================================================================
// CampProximityService — the SCENE-facing half of the camp claim prompt (MVVM
// migration Silo G, WO "DungeonHud + Camps + LevelUp").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World.Camps
//
// Owns everything the claim prompt needs from the live scene so the View no longer
// does it per-frame:
//   * hero resolution (GameObject.FindWithTag "Player", or the undefined-tag-safe
//     "HeroTarget" fallback) + Camera.main — cached.
//   * the nearest CLEARED-but-unclaimed camp within ClaimRange (the proximity /
//     cleared / claimed reconciliation that used to live in CampPromptUI).
//   * world -> screen projection for positioning the prompt button.
// It hands the VM a narrow <see cref="ICampTarget"/> wrapper so CampPromptVM stays
// scene-free and unit-testable (a fake ICampProximity drives it in EditMode).
// =============================================================================
using UnityEngine;

namespace DeNelle.Village.World.Camps
{
    /// <summary>Narrow seam over a claimable camp — what the VM needs to project the
    /// prompt and route the Claim / Build commands, without referencing the
    /// MonoBehaviour <see cref="ClaimableCamp"/>. WorldAnchor is the prompt's world
    /// position (View projects it to screen).</summary>
    public interface ICampTarget
    {
        bool Cleared { get; }
        bool Claimed { get; }
        /// <summary>World position the screen prompt anchors to (camp head height).</summary>
        Vector3 WorldAnchor { get; }
        /// <summary>Stable identity for "is this the same camp as last frame" compares.</summary>
        object Key { get; }
        void Claim();
        void BuildOutpost(OutpostType type);
    }

    /// <summary>The scene-proximity seam the VM polls each frame.</summary>
    public interface ICampProximity
    {
        /// <summary>Refresh cached hero / camera references (cheap, idempotent).</summary>
        void EnsureRefs();
        /// <summary>The nearest CLEARED-but-unclaimed camp within claim range, or null.</summary>
        ICampTarget FindClaimable();
        /// <summary>Projects a world position to screen. False when behind the camera.</summary>
        bool TryProject(Vector3 world, out Vector2 screen);
    }

    /// <summary>
    /// Production <see cref="ICampProximity"/> over the live scene + CampSystem.Camps.
    /// A plain class (no MonoBehaviour) the View owns and polls.
    /// </summary>
    public sealed class CampProximityService : ICampProximity
    {
        /// <summary>Hero must be within this many metres of a cleared camp to claim.</summary>
        public float ClaimRange = 7f;

        private Transform _hero;
        private Camera _cam;

        public void EnsureRefs()
        {
            if (_hero == null)
            {
                var p = GameObject.FindWithTag("Player");
                if (p == null)
                {
                    // "HeroTarget" may be undefined (FindWithTag throws on an undefined tag).
                    var ht = SafeFindWithTag("HeroTarget");
                    if (ht != null) p = ht;
                }
                _hero = p != null ? p.transform : null;
            }
            if (_cam == null) _cam = Camera.main;
        }

        public ICampTarget FindClaimable()
        {
            if (_hero == null) return null;
            ClaimableCamp best = null;
            float bestSqr = ClaimRange * ClaimRange;
            var camps = CampSystem.Camps;
            for (int i = 0; i < camps.Count; i++)
            {
                var c = camps[i];
                if (c == null || !c.Cleared || c.Claimed) continue;
                float sqr = (c.transform.position - _hero.position).sqrMagnitude;
                if (sqr <= bestSqr) { bestSqr = sqr; best = c; }
            }
            return best != null ? new CampTarget(best) : null;
        }

        public bool TryProject(Vector3 world, out Vector2 screen)
        {
            if (_cam == null) _cam = Camera.main;
            Vector3 sp = _cam != null
                ? _cam.WorldToScreenPoint(world)
                : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 1f);
            if (sp.z < 0f) { screen = default; return false; }   // behind camera -> hide
            screen = new Vector2(sp.x, sp.y);
            return true;
        }

        /// <summary>Undefined-tag-safe FindWithTag (Unity throws on an undefined tag).</summary>
        private static GameObject SafeFindWithTag(string tag)
        {
            try { return GameObject.FindWithTag(tag); }
            catch (UnityEngine.UnityException) { return null; }
        }

        /// <summary>Wraps a live <see cref="ClaimableCamp"/> as the VM's narrow seam.</summary>
        private sealed class CampTarget : ICampTarget
        {
            private readonly ClaimableCamp _camp;
            public CampTarget(ClaimableCamp camp) { _camp = camp; }
            public bool Cleared => _camp != null && _camp.Cleared;
            public bool Claimed => _camp != null && _camp.Claimed;
            public Vector3 WorldAnchor =>
                _camp != null ? _camp.transform.position + Vector3.up * 2.2f : Vector3.zero;
            public object Key => _camp;
            public void Claim() { if (_camp != null) _camp.Claim(); }
            public void BuildOutpost(OutpostType type) { if (_camp != null) _camp.BuildOutpost(type); }
        }
    }
}
