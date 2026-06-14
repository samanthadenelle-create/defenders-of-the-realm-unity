// =============================================================================
// CastleCompanionIntroducerInjector — a WALK-UP companion-introducer NPC (owner
// decision 2026-06-12: replace the fragile auto-FTUE companion-intro chain with a
// simple proximity-Talk trigger).
// -----------------------------------------------------------------------------
// THE PROBLEM this replaces:
//   The scripted companion intro had THREE tangled, fragile auto-start paths —
//     • TutorialDirector.RunFastPath's brief companion hook (the default "fast path"),
//     • CompanionMeetingTrigger's full Yarn "CompanionMeeting" autostart,
//     • the standalone SylasFirstMeeting beat,
//   each behind a once-per-save PlayerPrefs/SeenTutorials gate and a -yarnAlways
//   launch flag — and it still did NOT reliably fire on MainCastle_Hall entry. The
//   owner wants it SIMPLE and robust: a companion-introducer NPC the player WALKS UP
//   TO; on Talk it plays the intro dialogue and recruits the first companion.
//
// WHAT THIS IS (reuses the proven castle vendor / proximity-Talk pattern):
//   A self-bootstrapping DontDestroyOnLoad singleton — the SAME shape as
//   CastleVendorNpcInjector — that, on every hub-scene load (gated on
//   HubScenes.IsHub so it fires in MainCastle_Hall, not just the abandoned
//   Village2), spawns ONE static introducer NPC a short walk from the hero (a
//   visible distance across the courtyard, toward the south gate). It gives the NPC
//   a townsperson body the same way the vendors get theirs (Resources/NPCs People-
//   pack prefab + height-normalize), and attaches a slim CompanionIntroducerInteractable
//   that REGISTERS WITH TalkPromptRegistry — so the HUD's proximity Talk button lights
//   up on approach and, when pressed (or desktop [F]), plays the intro Yarn node.
//
// THE INTRO + RECRUIT (no new dialogue / recruit system):
//   On Talk -> DialogueService.Play("SylasFirstMeeting"). That authored Yarn node IS
//   the intro+recruit: it speaks Sylas's first-meeting lines and fires
//   <<command: RecruitCompanion Ranger>> through the command bridge (which
//   DialogueService installs on the runner), enrolling the Ranger companion into the
//   party — StoryCompanionInjector then spawns his following/fighting body. Same path
//   SylasFirstMeeting.cs already delegated to; we just gate it behind a walk-up NPC.
//
// THE SINGLE-TRIGGER GUARANTEE:
//   This injector sets CastleCompanionIntroducerInjector.Active = true the moment it
//   exists. The three legacy auto-FTUE companion-INTRO paths each check that flag and
//   SKIP their own companion meeting/recruit when it is set — so the NPC is the SOLE
//   intro+recruit trigger and there are never two. The wave loop is untouched: the
//   FTUE still marks Onboarded + kicks the wave loop (it just no longer HOSTS the
//   companion meeting), so combat starts exactly as before.
//
// Once-per-save (cleanly resettable): a GameState.SeenTutorials key, the same one-
// shot store the project already persists + the Reset Yarn / save-wipe already clears.
// Fork-bomb-safe: one fire-once guard on the Talk handler, no per-frame coroutine.
//
// Village -> Core only; code-spawn only, no scene hand-edit. Null-guarded throughout.
// =============================================================================

using DeNelle.Core;
using DeNelle.Core.State;
using DeNelle.Core.UI;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>
    /// Runtime, non-destructive placement of the WALK-UP companion-introducer NPC in
    /// the castle hub. Reuses the vendor body + the TalkPromptRegistry proximity-Talk
    /// pattern; on Talk it plays the intro Yarn node that recruits the first companion.
    /// </summary>
    public sealed class CastleCompanionIntroducerInjector : MonoBehaviour
    {
        public static CastleCompanionIntroducerInjector Instance { get; private set; }

        /// <summary>
        /// True once the walk-up introducer NPC is the active intro path. The legacy
        /// auto-FTUE companion-intro paths (TutorialDirector fast-path hook,
        /// CompanionMeetingTrigger, SylasFirstMeeting beat) read this and SKIP their
        /// own companion meeting/recruit so the NPC is the SINGLE intro+recruit trigger.
        /// Static + set in Awake so it's already true by the time those run.
        /// </summary>
        public static bool Active { get; private set; }

        /// <summary>
        /// The intro Yarn node the introducer plays on Talk. "SylasFirstMeeting" IS the
        /// intro+recruit: it speaks the first-meeting lines and fires
        /// &lt;&lt;command: RecruitCompanion Ranger&gt;&gt; through the command bridge,
        /// enrolling the first companion into the party.
        /// </summary>
        public const string IntroNode = "SylasFirstMeeting";

        /// <summary>One-shot save key — once the intro has played the NPC stands down.</summary>
        private const string SeenKey = "castle_companion_introducer";

        /// <summary>The People-pack body — the same source the vendors use. Falls back to a capsule.</summary>
        private const string IntroducerBody = "NPCs/NPC_Ranger_Scout";
        private const string FallbackBody   = "NPCs/NPC_Peasant_Tob";
        private const string FallbackBody2  = "NPCs/NPC_Merchant";

        // How far across the courtyard the introducer stands from the hero spawn — a
        // visible walk, toward -Z (the south gate side of MainCastle_Hall). Picked to be
        // close enough to be obvious yet far enough that the player WALKS UP to it.
        private const float IntroducerDistance = 12f;

        private const string HolderName = "CastleCompanionIntroducer (runtime)";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("CastleCompanionIntroducerInjector")
                .AddComponent<CastleCompanionIntroducerInjector>();
        }

        private void Awake()
        {
            // Destroy(this) — never Destroy(gameObject) on the shared-host landmine; this
            // injector lives on its own object so it's moot, but keep the safe idiom.
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            Active = true;   // claim the intro path BEFORE the legacy FTUE paths run
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            if (HubScenes.IsHub(SceneManager.GetActiveScene().name)) Inject();
        }

        private void OnDestroy()
        {
            if (Instance == this) { Instance = null; Active = false; }
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (HubScenes.IsHub(scene.name)) Inject();
        }

        private void Inject()
        {
            // Already recruited on this save? Then the intro is done — don't re-spawn the
            // introducer (the recruited companion already follows via StoryCompanionInjector).
            if (HasSeen())
            {
                Debug.Log("[CastleCompanionIntroducer] intro already seen on this save — no introducer spawned.");
                return;
            }

            // Idempotent: nuke any prior runtime holder so a re-load doesn't double-spawn.
            var prior = GameObject.Find(HolderName);
            if (prior != null) Destroy(prior);

            var holder = new GameObject(HolderName);
            Transform hero = ResolveHero();

            // QUICK FIX (owner 2026-06-13, RCA): the introducer used to spawn at a FIXED far
            // point (0,0,+20) while the hero spawns ~(0,y,-11) by the Tree of Life — ~31m away —
            // so players testing combat near the tree never reached it and the recruit
            // (SylasFirstMeeting -> RecruitCompanion Ranger -> AddToParty) never fired. Place the
            // introducer a SHORT walk in FRONT of the hero instead, so they meet it immediately.
            // 5m is just inside ActivateRadius (6m, prompt lights) and just OUTSIDE AutoFireRadius
            // (4m, auto-start) — the player sees it before it fires. Fall back to the old fixed
            // point if the hero can't be resolved at inject time. (The bigger "NPC becomes the
            // companion" unify is WO-432, not here.)
            Vector3 basePos = hero != null
                ? hero.position + FlatDir(hero.forward) * 5f
                : new Vector3(0f, 0f, 20f);
            if (NavMesh.SamplePosition(basePos, out var hit, 6f, NavMesh.AllAreas))
                basePos = hit.position;

            // Face back toward the hero / courtyard so the player walks up to its front.
            Quaternion rot = hero != null
                ? Quaternion.LookRotation(FlatDir(hero.position - basePos), Vector3.up)
                : Quaternion.identity;

            GameObject body = SpawnBody(basePos, rot, holder.transform);
            AttachInteraction(body, hero);

            Debug.Log($"[CastleCompanionIntroducer] spawned walk-up introducer at {basePos} " +
                      $"(courtyard, past the keep exit). Talk -> '{IntroNode}' (recruits the first companion).");
        }

        // ── Body (reuse the vendor body source + normalize) ──────────────────

        private GameObject SpawnBody(Vector3 pos, Quaternion rot, Transform parent)
        {
            var prefab = Resources.Load<GameObject>(IntroducerBody)
                         ?? Resources.Load<GameObject>(FallbackBody)
                         ?? Resources.Load<GameObject>(FallbackBody2);
            if (prefab == null)
            {
                Debug.LogWarning("[CastleCompanionIntroducer] no People-pack body found " +
                                 "(Models may be gitignored on a fresh clone) — capsule placeholder used.");
                return SpawnPlaceholder(pos, rot, parent);
            }

            var go = Instantiate(prefab, pos, rot, parent);
            go.name = "CompanionIntroducer";

            NormalizeToHeroHeight(go);
            // T-033 ("NPCs floating"): raycast DOWN to the real floor collider and seat the
            // renderer-bounds bottom onto it (pivot- and scale-agnostic), falling back to the
            // navmesh Y if no floor is hit. Replaces seating to the navmesh Y, which sits a
            // touch ABOVE the visual floor and left the introducer hovering.
            NpcGroundSeat.Seat(go, pos.y);

            // STATIC: configure the ambient driver to stand still (no wander/follow), the
            // same as the vendors. Belt-and-braces disable any NavMeshAgent so nothing moves it.
            var npc = go.GetComponent<AmbientNPC>();
            if (npc != null) npc.Configure(TownsfolkDialogue.Archetype.Villager, /*wander*/ false, pos);
            var agent = go.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            return go;
        }

        private GameObject SpawnPlaceholder(Vector3 pos, Quaternion rot, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "CompanionIntroducer_Placeholder";
            go.transform.SetParent(parent, false);
            go.transform.position = pos + Vector3.up * 1f;
            go.transform.rotation = rot;
            var col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;   // proximity Talk — never block the hero
            return go;
        }

        private void AttachInteraction(GameObject body, Transform hero)
        {
            var interact = body.AddComponent<CompanionIntroducerInteractable>();
            interact.Configure(IntroNode, "Stranger", hero, OnIntroPlayed);
        }

        /// <summary>Called once the intro Yarn node has been launched — persist the one-shot
        /// flag and retire the introducer so it can't be re-talked.</summary>
        private void OnIntroPlayed()
        {
            MarkSeen();
            var holder = GameObject.Find(HolderName);
            if (holder != null) Destroy(holder, 0.5f);
            Debug.Log("[CastleCompanionIntroducer] intro played — companion recruited; introducer retired.");
        }

        // ── One-shot persistence (resettable via the save wipe / Reset Yarn) ──

        private static bool HasSeen()
        {
            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            return state != null
                && state.SeenTutorials != null
                && state.SeenTutorials.TryGetValue(SeenKey, out bool seen)
                && seen;
        }

        private static void MarkSeen() => GameStateService.Instance?.MarkTutorialSeen(SeenKey);

        // ── Helpers (mirror the vendor injector) ─────────────────────────────

        private static Vector3 FlatDir(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }

        private static void NormalizeToHeroHeight(GameObject go)
        {
            float npcScale = 1f;
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                if (b.size.y > 0.01f) npcScale = 1.95f / b.size.y;
            }
            if (npcScale > 0.01f && !Mathf.Approximately(npcScale, 1f))
            {
                go.transform.localScale *= npcScale;
                var bubbleRoot = go.transform.Find("BubbleRoot");
                if (bubbleRoot != null) bubbleRoot.localScale = Vector3.one / Mathf.Max(0.01f, npcScale);
            }
        }

        // (Ground-seating now lives in the shared NpcGroundSeat helper — it raycasts to
        // the real floor instead of the navmesh Y, fixing the residual hover. See T-033.)

        private static Transform ResolveHero()
        {
            var tagged = GameObject.FindWithTag("Player");
            if (tagged != null) return tagged.transform;
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t != null && t.name.StartsWith("Hero")) return t;
            return null;
        }
    }

    // =========================================================================
    // CompanionIntroducerInteractable — slim proximity [F] / mobile-Talk interaction
    // that plays the companion-INTRO Yarn node on approach. Mirrors
    // CastleNpcInteractable exactly (ActivateRadius ~6m, registers with
    // TalkPromptRegistry so the HUD Talk button fires it, suppressed during dialogue /
    // build mode, desktop [F] for the nearest in-range) but routes to a Yarn NODE via
    // DialogueService.Play instead of a structure menu — and fires ONCE, then retires.
    // =========================================================================
    [DisallowMultipleComponent]
    public sealed class CompanionIntroducerInteractable : MonoBehaviour
    {
        private const float ActivateRadius = 6f;
        private const float AutoFireRadius = 4f; // owner 2026-06-12: auto-start the meeting on close approach

        private string _node;
        private string _label;
        private Transform _hero;
        private System.Action _onPlayed;
        private bool _fired;   // fire-once guard (no re-entry, no double recruit)

        public void Configure(string node, string label, Transform hero, System.Action onPlayed)
        {
            _node = node;
            _label = label;
            _hero = hero;
            _onPlayed = onPlayed;
        }

        private void Update()
        {
            if (_fired) return;
            if (_hero == null) { ResolveHero(); return; }

            // Build mode: release + bail (player is authoring, not interacting).
            if (MobileInteractButton.Suppressed)
            {
                TalkPromptRegistry.Deregister(transform);
                return;
            }

            // While any dialogue is on screen, drop our prompt so it doesn't stack under it.
            if (DialogueService.IsRunning)
            {
                TalkPromptRegistry.Deregister(transform);
                return;
            }

            float distSqr = (_hero.position - transform.position).sqrMagnitude;
            bool inRange = distSqr <= ActivateRadius * ActivateRadius;

            if (inRange)
            {
                // Register with the proximity registry so the HUD TALK button fires us (the
                // canonical mobile trigger). Desktop [F] handled just below.
                TalkPromptRegistry.Register(transform, Interact);
                // Owner 2026-06-12: AUTO-start the meeting on close approach (no Talk press) —
                // the Talk prompt still shows at ActivateRadius; within AutoFireRadius it fires.
                if (!PanelManager.AnyOpen && (Input.GetKeyDown(KeyCode.F) || distSqr <= AutoFireRadius * AutoFireRadius))
                    Interact();
            }
            else
            {
                TalkPromptRegistry.Deregister(transform);
            }
        }

        private void Interact()
        {
            if (_fired || string.IsNullOrEmpty(_node)) return;
            if (DialogueService.Play(_node))
            {
                _fired = true;
                TalkPromptRegistry.Deregister(transform);
                Debug.Log($"[CompanionIntroducerInteractable] {_label} -> intro node '{_node}' (recruits the first companion).");
                _onPlayed?.Invoke();
            }
        }

        private void ResolveHero()
        {
            var tagged = GameObject.FindWithTag("Player");
            if (tagged != null) { _hero = tagged.transform; return; }
            var loco = FindObjectOfType<HeroLocomotion>();
            if (loco != null) _hero = loco.transform;
        }

        private void OnDisable()
        {
            TalkPromptRegistry.Deregister(transform);
        }
    }
}
