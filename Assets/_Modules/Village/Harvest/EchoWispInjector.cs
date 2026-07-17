// =============================================================================
// EchoWispInjector + EchoInteractable -- WO-681: give the (previously invisible)
// Echo workforce a tappable presence in the hub, with the STANDARD interact
// affordance, so "what is an Echo" has a place to live.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// TODAY (verified from code, not comments): EchoService owns the workforce as
// pure numbers -- NO world body exists (the embodied worker at nodes is WO-659,
// not this WO). WO-681 needs an Echo the player can SELECT, so this injector
// spawns one lightweight wandering wisp per owned Echo in the hub courtyard:
//   - self-bootstrapping DDOL spawner ([RuntimeInitializeOnLoadMethod], hub-gated
//     via HubScenes.IsHub) -- the CastleCompanionIntroducerInjector pattern;
//   - wisp body = code-built sphere (no collider), gentle float/bob drift within
//     a small radius (spirits of the Tree, not NavMesh agents -- WO-659 owns the
//     embodied at-node worker later);
//   - DISCOVERABILITY = the standard interact seam NPCs use: each wisp carries an
//     EchoInteractable that self-registers into TalkPromptRegistry in range, so
//     the HUD Talk button lights and routes the press (TalkHudBridge), exactly
//     like CastleNpcInteractable / CompanionIntroducerInteractable. Presentation
//     observes; no new proximity system.
//
// SELECT flow (WO-681 spec 3): first-ever tap plays the one-line intro through
// the standard DialogueService path, one-shot (GameState.SeenTutorials via
// EchoCardVM.MarkFirstMeetingSeen -- additive, no schema change), THEN opens the
// card. Every later tap opens the card directly. [Flow:Echo] step-in/step-out.
// =============================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Hub-gated DDOL spawner: one wandering Echo wisp per owned Echo (EchoService
    /// .EchoCount), rebuilt on scene load and on Changed/EchoUnlocked. Each wisp is
    /// tappable via the standard Talk/interact seam (see <see cref="EchoInteractable"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EchoWispInjector : MonoBehaviour
    {
        public static EchoWispInjector Instance { get; private set; }

        // Courtyard anchor -- the same walk-up plaza the introducer NPC uses
        // (CastleCompanionIntroducerInjector: fixed courtyard pos past the keep exit).
        private static readonly Vector3 Anchor = new Vector3(2f, 0f, -26f);
        private const float RingRadius = 5f;      // spawn ring around the anchor
        private const float FloatHeight = 1.1f;   // hover height of a wisp

        private readonly List<GameObject> _wisps = new List<GameObject>();
        private GameObject _holder;               // per-scene runtime holder (not DDOL)
        private int _builtForCount = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("EchoWispInjector");
            DontDestroyOnLoad(go);
            go.AddComponent<EchoWispInjector>();
        }

        private void Awake()
        {
            // Destroy(this) -- NOT Destroy(gameObject): may share a host
            // (CLAUDE.md memory: singleton-dedup-destroys-host).
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                Unbind();
                Instance = null;
            }
        }

        private void Start()
        {
            Bind();
            RebuildIfHub("start");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;   // additive OuterWorld stream: keep the hub wisps
            _builtForCount = -1;                        // scene swap destroyed the old holder
            RebuildIfHub($"scene '{scene.name}'");
        }

        private void Bind()
        {
            var svc = EchoService.Instance;
            if (svc != null)
            {
                svc.Changed += OnWorkforceChanged;
                svc.EchoUnlocked += OnEchoUnlocked;
            }
        }

        private void Unbind()
        {
            var svc = EchoService.Instance;
            if (svc != null)
            {
                svc.Changed -= OnWorkforceChanged;
                svc.EchoUnlocked -= OnEchoUnlocked;
            }
        }

        private void OnEchoUnlocked(int newCount) => RebuildIfHub($"echo unlocked ({newCount})");

        private void OnWorkforceChanged()
        {
            // Changed fires every accrual tick -- only rebuild when the COUNT moved.
            var svc = EchoService.Instance;
            if (svc == null || svc.EchoCount == _builtForCount) return;
            RebuildIfHub("count changed");
        }

        /// <summary>(Re)spawn the wisp ring when the active scene is a hub. Idempotent per count.</summary>
        private void RebuildIfHub(string reason)
        {
            // SCRAPPED (owner felt-test 2026-07-17): "Echoes are portrait-card spirits, NOT 3D
            // models." The visible floating wisp BODIES are retired -- echoes now live as portrait
            // cards (EchoUnlockDialogue + EchoRosterView, opened by the HUD "Pets" button). We keep
            // this injector inert (never spawns a body) rather than deleting it, so the abstract
            // EchoService workforce/silo is untouched. Any previously-built wisps are cleared.
            Clear();
            FlowTrace.Step("Echo", $"WispInjector: wisp bodies SCRAPPED (echoes are portrait cards now); no 3D echo body spawned ({reason}).");
            return;
#pragma warning disable CS0162 // unreachable-by-design: the spawn path is retained (dormant) for reference only
            string scene = SceneManager.GetActiveScene().name;
            if (!HubScenes.IsHub(scene)) { Clear(); return; }
            var svc = EchoService.Instance;
            if (svc == null) return;
            int count = svc.EchoCount;
            if (count == _builtForCount && _holder != null) return;

            Clear();
            _holder = new GameObject("EchoWisps (runtime)");   // per-scene, NOT DDOL

            int built = 0;
            for (int i = 0; i < count; i++)
            {
                int index = i;   // capture
                bool ok = Guard.Try("Echo", $"spawn echo wisp {index}", () => SpawnWisp(index, count));
                if (ok) built++;
            }
            _builtForCount = count;
            FlowTrace.Step("Echo",
                $"WispInjector: {built}/{count} echo wisp(s) spawned in '{scene}' ({reason}).");
#pragma warning restore CS0162
        }

        private void Clear()
        {
            for (int i = _wisps.Count - 1; i >= 0; i--)
                if (_wisps[i] != null) Destroy(_wisps[i]);
            _wisps.Clear();
            if (_holder != null) { Destroy(_holder); _holder = null; }
            _builtForCount = -1;
        }

        private void SpawnWisp(int echoIndex, int count)
        {
            // Ring placement around the courtyard anchor (deterministic per index).
            float ang = (echoIndex / (float)Mathf.Max(1, count)) * Mathf.PI * 2f;
            Vector3 basePos = Anchor + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * RingRadius;

            // Ground the anchor point via NavMesh when available (spirits still float above it).
            if (UnityEngine.AI.NavMesh.SamplePosition(basePos, out var hit, 8f, UnityEngine.AI.NavMesh.AllAreas))
                basePos = hit.position;

            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = $"EchoWisp_{echoIndex}";
            body.transform.SetParent(_holder != null ? _holder.transform : null, false);
            body.transform.position = basePos + Vector3.up * FloatHeight;
            body.transform.localScale = Vector3.one * 0.45f;

            // No physics: the interact seam is proximity-based (TalkPromptRegistry),
            // and a solid collider would shove the hero around.
            var col = body.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Life-force green spirit tint (identity is carried by the Talk prompt +
            // card TEXT, never by color alone -- colorblind law).
            var rend = body.GetComponent<Renderer>();
            if (rend != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader != null)
                {
                    var mat = new Material(shader);
                    var tint = new Color(0.45f, 0.85f, 0.55f, 1f);
                    mat.color = tint;
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", tint * 1.6f);
                    rend.material = mat;
                }
            }

            var drift = body.AddComponent<EchoWispDrift>();
            drift.Configure(basePos + Vector3.up * FloatHeight, 2.5f, 0.35f + echoIndex * 0.07f);

            var interact = body.AddComponent<EchoInteractable>();
            interact.Configure(echoIndex);

            _wisps.Add(body);
        }
    }

    /// <summary>Gentle float: slow circular drift + vertical bob around a home point.
    /// Pure presentation -- observes nothing, mutates nothing.</summary>
    [DisallowMultipleComponent]
    public sealed class EchoWispDrift : MonoBehaviour
    {
        private Vector3 _home;
        private float _radius = 2.5f;
        private float _speed = 0.35f;
        private float _phase;

        public void Configure(Vector3 home, float radius, float speed)
        {
            _home = home;
            _radius = Mathf.Max(0.5f, radius);
            _speed = Mathf.Max(0.05f, speed);
            _phase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            float t = Time.time * _speed + _phase;
            var offset = new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t * 0.8f)) * _radius;
            float bob = Mathf.Sin(Time.time * 1.7f + _phase) * 0.25f;
            transform.position = _home + offset + Vector3.up * bob;
        }
    }

    /// <summary>
    /// The standard proximity interact affordance for an Echo wisp: registers into
    /// TalkPromptRegistry in range (the HUD Talk button lights + routes the press --
    /// the SAME seam every talkable NPC uses), suppressed during dialogue / build
    /// mode / an open modal. On Interact: first-ever tap per save plays the one-line
    /// intro (standard DialogueService path) then opens the Echo card; later taps
    /// open the card directly. Mirrors CompanionIntroducerInteractable minus the
    /// fire-once retirement (an Echo stays selectable forever).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EchoInteractable : MonoBehaviour
    {
        private const float ActivateRadius = 6f;

        private int _echoIndex;
        private Transform _hero;
        private bool _beatRunning;   // re-entry guard while the intro line plays

        public void Configure(int echoIndex) => _echoIndex = echoIndex;

        private void Update()
        {
            if (_hero == null) { ResolveHero(); return; }

            // Build mode / an open modal / a running dialogue: drop our prompt
            // (same suppression set the NPC interactables use).
            if (MobileInteractButton.Suppressed || DialogueService.IsRunning || _beatRunning)
            {
                TalkPromptRegistry.Deregister(transform);
                return;
            }

            float distSqr = (_hero.position - transform.position).sqrMagnitude;
            if (distSqr <= ActivateRadius * ActivateRadius)
                TalkPromptRegistry.Register(transform, Interact);
            else
                TalkPromptRegistry.Deregister(transform);
        }

        private void Interact()
        {
            using var _t = FlowTrace.Enter("Echo", "Select");
            if (_beatRunning) return;

            if (EchoCardVM.NeedsFirstMeeting)
            {
                // First-meeting beat (one per save): the line rides the standard
                // dialogue rail; the card opens when the line finishes. Mark seen
                // FIRST so a mid-line quit can never replay-loop the beat.
                EchoCardVM.MarkFirstMeetingSeen();
                if (DialogueService.NodeExists(EchoCardVM.FirstMeetingNode) &&
                    DialogueService.Play(EchoCardVM.FirstMeetingNode))
                {
                    FlowTrace.Step("Echo", "Select: first-meeting line playing; card opens after.");
                    _beatRunning = true;
                    StartCoroutine(OpenCardAfterDialogue());
                    return;
                }
                // Unauthored / busy dialogue rail -> logged by DialogueService; the
                // card still opens so the tap is never a dead press.
                FlowTrace.Warn("Echo", "Select: first-meeting line did not launch -- opening card directly.");
            }

            FlowTrace.Step("Echo", $"Select: opening card for echo {_echoIndex}.");
            EchoCard.Open(_echoIndex);
        }

        private IEnumerator OpenCardAfterDialogue()
        {
            // Wait out the one-liner (bounded so a stuck dialogue can never wedge the beat).
            float deadline = Time.unscaledTime + 30f;
            while (DialogueService.IsRunning && Time.unscaledTime < deadline)
                yield return null;
            _beatRunning = false;
            FlowTrace.Step("Echo", $"Select: first-meeting line done -- opening card for echo {_echoIndex}.");
            EchoCard.Open(_echoIndex);
        }

        private void ResolveHero()
        {
            var tagged = GameObject.FindWithTag("Player");
            if (tagged != null) { _hero = tagged.transform; return; }
            var loco = FindAnyObjectByType<HeroLocomotion>();
            if (loco != null) _hero = loco.transform;
        }

        private void OnDisable()
        {
            TalkPromptRegistry.Deregister(transform);
        }
    }
}
