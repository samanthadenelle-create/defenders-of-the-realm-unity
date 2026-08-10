// =============================================================================
// HeroGaitForensics — owner F8 2026-07-12 ("look at the data for walking and
// running, hip bones and smart camera. debug print every change in a detailed
// log. must be fixed now").
//
// Per-frame capture of every signal in the gait/camera loop, two outputs:
//   1. [Flow:GaitF] CHANGE lines — printed whenever a tracked value moves past
//      its epsilon (heading, camera yaw, animator state, speed band) + a 1 Hz
//      heartbeat. Captured by the F8 harness/break-log like all Flow lines.
//   2. gait-forensics.csv (persistentDataPath) — EVERY frame, machine-readable,
//      for exact amplitude/phase analysis (hip weave frequency, cam/heading
//      correlation). Header row on session start; file overwritten per session.
//
// Tracked per frame: velocity magnitude + heading; transform yaw (+delta);
// camera yaw (+delta, the camera-relative move basis); HIP BONE local X/Z and
// world lateral offset from the root (the "hip bones" ask — weave amplitude);
// animator state hash, dominant clip + weight, Speed param vs actual m/s
// (foot-skate ratio).
//
// WO-965 (F8 seq 2309, "Mage faces northwest when running north") added the four
// fields that make that capture DECISIVE, without renaming any existing one:
//   bodyYaw      - world Y euler of the "HeroBody" child (the VISIBLE facing; the
//                  root yaw above is NOT what the owner sees).
//   bodyLocalYaw - its LOCAL Y euler, i.e. the swapper's applied forward-yaw
//                  (+15 Knight / -90 others, HeroBodySwapper.cs:263) plus anything
//                  else that rotated the body after the swap.
//   bodyErr      - DeltaAngle(velHeading, bodyYaw): the felt error in degrees.
//   basisYaw     - SmartMobileCamera.CameraYaw, the REAL camera-relative movement
//                  basis. camYaw (the camera transform) is a velocity-lead-biased
//                  LookRotation and is confounded while moving, so it alone cannot
//                  discriminate a camera-space conversion error.
//
// Self-bootstrapping (never a scene/prefab edit): attaches to the hero when a
// HeroLocomotion appears. Toggle: PlayerPrefs "ff.gaitforensics" (default ON
// while the investigation runs — strip or default-off once root cause ships,
// §12 lifecycle). LateUpdate so every writer this frame has already run.
// =============================================================================

using System.Globalization;
using System.IO;
using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Gait/camera forensic recorder — change-lines to FlowTrace, every
    /// frame to gait-forensics.csv. See header; owner F8 2026-07-12.</summary>
    public sealed class HeroGaitForensics : MonoBehaviour
    {
        private const string Sys = "GaitF";
        private const float HeadingEpsDeg = 2.0f;
        private const float CamYawEpsDeg = 0.5f;

        private HeroLocomotion _loco;
        private Animator _animator;
        private Transform _hips;
        private StreamWriter _csv;

        // WO-965: the ROOT yaw is NOT what the owner sees. HeroBodySwapper parents the visual
        // under the hero root as a child named exactly "HeroBody" (HeroBodySwapper.cs:225/284/351)
        // and stamps a LOCAL forward-yaw on it (+15 Knight, -90 every other class, :263). So the
        // perceived facing is the BODY's world yaw, and bodyYaw - velHeading is the felt error.
        // Cached (never Find per frame); re-resolved at most 1 Hz because a body swap replaces it.
        private Transform _body;
        private float _nextBodyProbe;

        private float _lastYaw, _lastCamYaw, _lastHeading;
        private int _lastStateHash;
        private float _lastBeat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (PlayerPrefs.GetInt("ff.gaitforensics", 1) == 0) return;
            var host = new GameObject("GaitForensics (runtime)");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<GaitForensicsAttacher>();
        }

        /// <summary>Polls for the hero (survives scene loads/body swaps) and keeps
        /// exactly one forensics component attached to it.</summary>
        private sealed class GaitForensicsAttacher : MonoBehaviour
        {
            private float _next;
            private void Update()
            {
                if (Time.unscaledTime < _next) return;
                _next = Time.unscaledTime + 1f;
                var loco = FindFirstObjectByType<HeroLocomotion>();
                if (loco != null && loco.GetComponent<HeroGaitForensics>() == null)
                    loco.gameObject.AddComponent<HeroGaitForensics>();
            }
        }

        private void OnEnable()
        {
            _loco = GetComponent<HeroLocomotion>();
            _animator = GetComponentInChildren<Animator>(true);
            if (_animator != null && _animator.isHuman)
                _hips = _animator.GetBoneTransform(HumanBodyBones.Hips);

            string path = Path.Combine(Application.persistentDataPath, "gait-forensics.csv");
            try
            {
                _csv = new StreamWriter(path, append: false);
                // WO-965 appended the last four columns (bodyYaw/bodyLocalYaw/bodyErr/basisYaw).
                // APPENDED, never reordered - existing column names/positions are unchanged so
                // anything already parsing this CSV keeps working.
                _csv.WriteLine("t,dt,velMag,velHeading,yaw,yawDelta,camYaw,camYawDelta," +
                               "hipLocalX,hipLocalZ,hipLateralWorld,stateHash,clip,clipW,animSpeedParam,skateRatio," +
                               "bodyYaw,bodyLocalYaw,bodyErr,basisYaw");
                FlowTrace.Step(Sys, $"forensics ON — csv: {path} (change-lines: heading>{HeadingEpsDeg}deg, camYaw>{CamYawEpsDeg}deg, state).");
            }
            catch (System.Exception e)
            {
                FlowTrace.Warn(Sys, $"csv open failed ({e.Message}) — change-lines only.");
                _csv = null;
            }
        }

        private void OnDisable()
        {
            _csv?.Flush();
            _csv?.Dispose();
            _csv = null;
        }

        private void LateUpdate()
        {
            if (_loco == null) return;

            Vector3 vel = _loco.Velocity;
            float velMag = vel.magnitude;
            float heading = velMag > 0.05f ? Mathf.Atan2(vel.x, vel.z) * Mathf.Rad2Deg : _lastHeading;
            float yaw = transform.eulerAngles.y;
            var cam = Camera.main;
            float camYaw = cam != null ? cam.transform.eulerAngles.y : 0f;

            // WO-965 (a): camYaw above is the TRANSFORM yaw of the camera, which the rig aims with a
            // LookRotation at a velocity-lead-biased point - so while moving it is confounded and
            // cannot discriminate a camera-space conversion error. SmartMobileCamera.CameraYaw
            // (SmartMobileCamera.cs:334) is the ACTUAL movement basis HeroLocomotion converts against
            // (pure player pan; 0 when orbit-behind is OFF). Log both; they are not the same number.
            var smart = SmartMobileCamera.Instance;
            float basisYaw = smart != null ? smart.CameraYaw : 0f;

            // WO-965 (b): the BODY, not the root. Re-resolve at most 1 Hz (body swaps replace it).
            if (_body == null && Time.unscaledTime >= _nextBodyProbe)
            {
                _nextBodyProbe = Time.unscaledTime + 1f;
                _body = transform.Find("HeroBody");
                if (_body == null)
                    FlowTrace.Throttle(Sys, "no-herobody", 5f,
                        "no child named 'HeroBody' under the hero root - bodyYaw/bodyErr will " +
                        "read 0 (body not swapped in yet?).");
            }
            float bodyYaw = _body != null ? _body.eulerAngles.y : 0f;
            float bodyLocalYaw = _body != null ? _body.localEulerAngles.y : 0f;
            // The number the owner perceives: how far the VISIBLE body points off the direction of travel.
            float bodyErr = _body != null && velMag > 0.2f ? Mathf.DeltaAngle(heading, bodyYaw) : 0f;

            float yawDelta = Mathf.DeltaAngle(_lastYaw, yaw);
            float camYawDelta = Mathf.DeltaAngle(_lastCamYaw, camYaw);
            float headingDelta = Mathf.DeltaAngle(_lastHeading, heading);

            // Hip bone: local X/Z in root space + world lateral offset from the root
            // (the weave signal — amplitude tells whether the sway is IN THE CLIP
            // or from root rotation).
            float hipLX = 0f, hipLZ = 0f, hipLat = 0f;
            if (_hips != null)
            {
                Vector3 hl = transform.InverseTransformPoint(_hips.position);
                hipLX = hl.x; hipLZ = hl.z;
                hipLat = Vector3.Dot(_hips.position - transform.position, transform.right);
            }

            int stateHash = 0; string clip = ""; float clipW = 0f; float speedParam = 0f;
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                var st = _animator.GetCurrentAnimatorStateInfo(0);
                stateHash = st.shortNameHash;
                var infos = _animator.GetCurrentAnimatorClipInfo(0);
                for (int i = 0; i < infos.Length; i++)
                    if (infos[i].clip != null && infos[i].weight > clipW)
                    { clip = infos[i].clip.name; clipW = infos[i].weight; }
                speedParam = _animator.GetFloat("Speed");
            }
            float skate = velMag > 0.2f && speedParam > 0.01f ? speedParam / velMag : 0f;

            _csv?.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0:F3},{1:F4},{2:F3},{3:F1},{4:F1},{5:F2},{6:F1},{7:F2},{8:F4},{9:F4},{10:F4},{11},{12},{13:F2},{14:F2},{15:F2}," +
                "{16:F1},{17:F1},{18:F1},{19:F1}",
                Time.time, Time.deltaTime, velMag, heading, yaw, yawDelta, camYaw, camYawDelta,
                hipLX, hipLZ, hipLat, stateHash, clip, clipW, speedParam, skate,
                bodyYaw, bodyLocalYaw, bodyErr, basisYaw));

            bool stateChanged = stateHash != _lastStateHash;
            bool headingJump = velMag > 0.2f && Mathf.Abs(headingDelta) > HeadingEpsDeg;
            bool camMoved = Mathf.Abs(camYawDelta) > CamYawEpsDeg;
            bool beat = Time.time - _lastBeat >= 1f;
            if (stateChanged || headingJump || camMoved || beat)
            {
                _lastBeat = Time.time;
                FlowTrace.Step(Sys,
                    $"vel={velMag:F2}@{heading:F0}deg dHead={headingDelta:F1} yaw={yaw:F0} dYaw={yawDelta:F1} " +
                    $"camYaw={camYaw:F0} dCam={camYawDelta:F1}{(camMoved && velMag > 0.2f ? " CAM-MOVED-WHILE-MOVING" : "")} " +
                    $"basisYaw={basisYaw:F0} bodyYaw={bodyYaw:F0} bodyLocalYaw={bodyLocalYaw:F0} bodyErr={bodyErr:F1} " +
                    $"hipX={hipLX:F3} hipLat={hipLat:F3} clip={clip}({clipW:F2}) speedP={speedParam:F2} skate={skate:F2}" +
                    (stateChanged ? " STATE-CHANGE" : ""));
            }

            _lastYaw = yaw; _lastCamYaw = camYaw; _lastHeading = heading; _lastStateHash = stateHash;
        }
    }
}
