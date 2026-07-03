// =============================================================================
// KayKitAnimProof — side-by-side animation-quality proof (proof-before-decision,
// owner-endorsed 2026-06-24; memory `kaykit-character-library-uncatalogued`).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.DevTools (dev/editor only — compiled out of release)
//
// WHAT: spawns the KayKit Adventurers 2.0 Knight beside the playable (Tripo)
// hero, driven by the PROVEN Resources/Enemies/HumanoidEnemy controller — the
// controller AnimatorSetup builds from KayKit's own Rig_Medium clip library
// (Character Animations 1.1), the same Generic-rig pipeline every KayKit enemy
// already animates through. A tiny scripted mover (no NavMesh dependency) walks
// it in a small square loop with idle pauses at the corners, so the owner can
// stand the two side by side and judge idle + walk quality directly.
//
// This is the PROOF harness, not the pivot: nothing about the hero pipeline is
// touched; despawn removes every trace.
//
// GUARDS (§4 canon): the KayKit pack is GITIGNORED — a fresh clone may not have
// it. Missing model / missing controller = Debug.LogWarning + no-op, never an
// error. The model loads via AssetDatabase (it is not under Resources/), so the
// spawn is EDITOR-ONLY; a development player build logs a warning and no-ops.
// =============================================================================

#if DEVELOPMENT_BUILD || UNITY_EDITOR

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.DevTools
{
    /// <summary>
    /// Dev-panel proof harness: spawns a KayKit Knight beside the hero, animated
    /// by the KayKit-clip HumanoidEnemy controller, walking a small scripted loop.
    /// Editor-only load (the gitignored pack is outside Resources); §4-guarded.
    /// </summary>
    public static class KayKitAnimProof
    {
        private const string KnightFbxPath =
            "Assets/Models/KayKit/KayKit Adventurers 2.0/Characters/fbx/Knight.fbx";
        // Built by AnimatorSetup from KayKit Rig_Medium clips (Character Animations
        // 1.1) and copied into Resources/Enemies by EnemyAnimatorSetup — carries the
        // Speed float + Idle/Move states this mover drives.
        private const string ControllerRes = "Enemies/HumanoidEnemy";

        private static GameObject s_instance;

        /// <summary>Spawns (or re-spawns) the proof knight beside the hero.</summary>
        public static void SpawnBesideHero()
        {
            Despawn(); // idempotent — one proof knight at a time

#if UNITY_EDITOR
            var model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(KnightFbxPath);
            if (model == null)
            {
                Debug.LogWarning(
                    $"[KayKitAnimProof] KayKit pack not imported ({KnightFbxPath} missing) — " +
                    "no-op. The pack is gitignored; copy it in on a fresh clone (CLAUDE.md §4).");
                return;
            }

            var ctrl = Resources.Load<RuntimeAnimatorController>(ControllerRes);
            if (ctrl == null)
            {
                Debug.LogWarning(
                    $"[KayKitAnimProof] controller 'Resources/{ControllerRes}' missing — no-op. " +
                    "Run Defenders > Animation > Build Animator Controllers, then EnemyAnimatorSetup.");
                return;
            }

            // Beside the hero (canon tag `Player`, WO-450); world origin as fallback.
            var hero = GameObject.FindWithTag("Player");
            Vector3 pos = hero != null
                ? hero.transform.position + hero.transform.right * 2f
                : Vector3.zero;
            Quaternion rot = hero != null ? hero.transform.rotation : Quaternion.identity;

            s_instance = Object.Instantiate(model, pos, rot);
            s_instance.name = "KayKit_Knight_AnimProof";

            var anim = s_instance.GetComponentInChildren<Animator>();
            if (anim == null) anim = s_instance.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;                       // the mover owns position
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; // judging quality — never cull

            var mover = s_instance.AddComponent<KayKitProofMover>();
            mover.Bind(anim, pos);

            FlowTrace.Step("DevTools",
                $"KayKitAnimProof spawned '{model.name}' at {pos} (controller {ControllerRes})");
#else
            Debug.LogWarning(
                "[KayKitAnimProof] editor-only — the gitignored KayKit pack loads via " +
                "AssetDatabase, which does not exist in a player build. Run in the editor.");
#endif
        }

        /// <summary>Removes the proof knight (safe if none exists).</summary>
        public static void Despawn()
        {
            if (s_instance == null) return;
            Object.Destroy(s_instance);
            s_instance = null;
            FlowTrace.Step("DevTools", "KayKitAnimProof despawned");
        }
    }

    /// <summary>
    /// Minimal scripted walker for the proof knight: a small square loop at walk
    /// pace with an idle pause at each corner, feeding the Animator's Speed float
    /// so the Idle&lt;-&gt;Move blend exercises exactly like a real enemy. No
    /// NavMesh, no gameplay hooks — pure presentation.
    /// </summary>
    public sealed class KayKitProofMover : MonoBehaviour
    {
        private const float WalkSpeed   = 1.6f; // m/s — matches a KayKit walk-clip pace
        private const float LoopHalf    = 2.0f; // square loop is 4m per side
        private const float CornerPause = 1.5f; // idle at each corner (shows the blend)
        private const float TurnSlerp   = 6f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        private Animator _anim;
        private Vector3[] _corners;
        private int _target;
        private float _pauseLeft;

        /// <summary>Binds the animator and centres the loop on the spawn point.</summary>
        public void Bind(Animator anim, Vector3 centre)
        {
            _anim = anim;
            _corners = new[]
            {
                centre + new Vector3(-LoopHalf, 0f, -LoopHalf),
                centre + new Vector3( LoopHalf, 0f, -LoopHalf),
                centre + new Vector3( LoopHalf, 0f,  LoopHalf),
                centre + new Vector3(-LoopHalf, 0f,  LoopHalf),
            };
            _target = 0;
            _pauseLeft = CornerPause; // open on an idle so the owner sees both states
        }

        private void Update()
        {
            if (_anim == null || _corners == null) return;

            if (_pauseLeft > 0f)
            {
                _pauseLeft -= Time.deltaTime;
                _anim.SetFloat(SpeedHash, 0f);
                return;
            }

            Vector3 to = _corners[_target] - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist < 0.1f)
            {
                _target = (_target + 1) % _corners.Length;
                _pauseLeft = CornerPause;
                return;
            }

            Vector3 dir = to / dist;
            transform.position += dir * (WalkSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, Quaternion.LookRotation(dir), TurnSlerp * Time.deltaTime);
            _anim.SetFloat(SpeedHash, WalkSpeed);
        }
    }
}

#endif
