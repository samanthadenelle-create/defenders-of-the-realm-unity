// =============================================================================
// SylasStewardInjector — WO-702 "The Founding of Elarion": Sylas's BODY for the
// founding beats (owner ruling 2026-07-13: "use the model for him, then unload it").
// -----------------------------------------------------------------------------
// ⚠ WO-1012 P2 (owner re-ruling 2026-08-09): the tutorial GUIDE is now the
// player's first PET-ECHO — tutorial dialogue attributes to the "{guide}" token
// (TutorialGuide seam), NOT to Sylas, and TutorialWorldAnchors.ResolveGuide
// prefers the live pet body. This steward body remains as the PARKED-ROTATION
// STAND-IN / physical fallback the guide anchor resolves to before the pet
// deploys (and Sylas stays a canon village NPC in his own right).
//
// Original rationale (WO-702): under ff.singlehero the walk-up companion
// introducer no-ops, so NO steward body exists — the guide anchor would fall to
// a synthetic courtyard anchor and "world.guide" would spotlight empty air.
//
// This injector mirrors CastleCompanionIntroducerInjector's proven shape
// (RuntimeInitializeOnLoadMethod bootstrap → HubScenes.IsHub gate → runtime
// holder → Resources body + height-normalize + NpcGroundSeat) and spawns the
// Ranger-Scout body NEAR THE HEART, named "Sylas", so:
//   * TutorialWorldAnchors.ResolveGuide finds it by name (GameObject.Find("Sylas"))
//     as its stand-in fallback and the "world.guide" highlight + "guide_anchor"
//     proximity resolve to a REAL character standing at the tree when no pet
//     body is deployed — the fresh-spawn vista (tree + well + steward).
//   * A manual Talk (TalkPromptRegistry proximity prompt) REPLAYS the current
//     founding step's intro line (TutorialFlow.CurrentIntroDialogueId) — the flow
//     itself auto-plays each beat, so Talk is a courtesy replay, never a gate.
//
// LIFECYCLE (the "then unload it" half): the body exists ONLY while the founding
// arc is incomplete — gate = FeatureFlags.TutorialV2 && !GameState.Onboarded, the
// SAME arc-incomplete signal the FTUE peace window uses (TutorialFlow.Hostiles-
// SuppressedForTutorial; no new flag). A cheap 1 Hz poll watches Onboarded and
// destroys the holder the moment the arc completes.
//
// NO wander: AmbientNPC configured wander:false + NavMeshAgent disabled (the
// introducer's exact static-body idiom). Village → Core only. Guard/FlowTrace
// per §12 (docs/INSTRUMENTATION_STANDARD.md).
// =============================================================================

using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using DeNelle.Core.UI;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using CoreDialogue = DeNelle.Core.Dialogue;

namespace DeNelle.Village
{
    /// <summary>
    /// Runtime, non-destructive spawn of Sylas's steward body near the Heart for
    /// the WO-702 founding beats; despawns the moment onboarding completes.
    /// </summary>
    public sealed class SylasStewardInjector : MonoBehaviour
    {
        public static SylasStewardInjector Instance { get; private set; }

        /// <summary>The body prefab — the same Ranger-Scout source the companion
        /// introducer uses (Sylas IS the Ranger scout, owner pin #1). Capsule fallback.</summary>
        private const string StewardBody   = "NPCs/NPC_Ranger_Scout";
        private const string FallbackBody  = "NPCs/NPC_Peasant_Tob";

        private const string HolderName = "SylasSteward (runtime)";

        // Offset from the Heart toward the known-walkable courtyard point (6,0,4) —
        // the same safe town band TutorialWorldAnchors.ResolveTownAnchor uses — so
        // Sylas stands "beneath the tree" a few strides into the courtyard.
        // Owner F8 2026-07-13 ("is he an actual character? something I can see?") — the log
        // proved him ALIVE at heart+(4,0,3) = world (4,0,15), which is 3m from the trunk
        // center of the giant Heart tree: spawned INSIDE/behind the canopy, invisible from
        // the player's south approach. Offset moves him SOUTH of the trunk onto open lawn,
        // in the camera's natural line when walking up from spawn.
        private static readonly Vector3 CourtyardOffset = new Vector3(2f, 0f, -9f);

        private const float DespawnPollInterval = 1f;
        private float _nextPollAt;

        // WO-1014 (owner felt-test 2026-08-10, verbatim: "but still wolf and npc"): the
        // guide-body watch runs FASTER than the Onboarded watch. WO-961 gives the guide a
        // real wolf body a beat or two after the hub loads — i.e. AFTER this stand-in has
        // legitimately seated — so the window where both exist is measured from the summon,
        // not from scene load. A 1 Hz check would leave the pair visible for up to a second
        // at the exact moment the player is looking at the tree.
        private const float GuideBodyPollInterval = 0.25f;
        private float _nextGuideCheckAt;
        private bool _standDownTraced;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // FTUE-1 (owner F8 2026-07-13 "No Sylas", twice, while the headless probe
            // PASSES): every gate below logs UNCONDITIONALLY (plain Debug.Log, no
            // FlowTrace variables) so the next interactive session NAMES the dead gate —
            // the tutorial's own identically-registered bootstrap runs in her sessions,
            // this one has produced ZERO lines. Instrument-first, §12.
            Debug.Log("[SylasSteward] Bootstrap ENTER (RuntimeInitializeOnLoadMethod fired).");
            // The founding steward only exists in the Tutorial V2 world; flag OFF =
            // fully dormant (the legacy director owns its own Sylas presentation).
            if (!FeatureFlags.TutorialV2) { Debug.Log("[SylasSteward] Bootstrap EXIT: ff.tutorialv2 OFF."); return; }
            if (Instance != null) { Debug.Log("[SylasSteward] Bootstrap EXIT: instance already live."); return; }
            new GameObject("SylasStewardInjector").AddComponent<SylasStewardInjector>();
            Debug.Log("[SylasSteward] Bootstrap: injector GameObject created.");
        }

        private void Awake()
        {
            Debug.Log($"[SylasSteward] Awake (activeScene='{SceneManager.GetActiveScene().name}').");
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            if (HubScenes.IsHub(SceneManager.GetActiveScene().name)) Inject();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (HubScenes.IsHub(scene.name)) Inject();
        }

        // 1 Hz Onboarded watch — "use the model for him, then unload it".
        // FTUE-1 ROOT (owner F8 "no Sylas. Verify with DATA please", 2026-07-13, proven by
        // her session log: Bootstrap ENTER + injector created at Title, then NO Inject line
        // ever): the old code ALSO destroyed the INJECTOR here — and this poll runs on the
        // TITLE screen, where the previously-loaded save (Onboarded=true after her Skip)
        // made ArcIncomplete false, so the injector silently self-destructed BEFORE New Game
        // reset Onboarded. RuntimeInitializeOnLoadMethod fires once per app run — nothing
        // rebuilt it, and the hub loaded stewardless. THE FIX: unload the BODY only; the
        // injector stays resident (a dormant 1 Hz check) so a New Game in the same app run
        // gets its fresh spawn via OnSceneLoaded -> Inject.
        private void Update()
        {
            // ── WO-1014: ONE GUIDE, EVER ────────────────────────────────────────
            // The stand-in exists ONLY to give the guide a physical presence while the
            // guide has no body of its own. WO-961 shipped that body, so from the moment
            // it summons this steward is a SECOND figure standing where the guide is —
            // exactly what the owner saw ("but still wolf and npc"). It is retired here
            // rather than deleted outright because it is still the honest degradation
            // path: TutorialFlow's own summon-failure branch says so in as many words
            // ("'Follow {guide}' will resolve to the steward stand-in"). Body-less guide
            // -> stand-in seats. Guide with a body -> stand-in stands down. A chain.
            if (Time.unscaledTime >= _nextGuideCheckAt)
            {
                _nextGuideCheckAt = Time.unscaledTime + GuideBodyPollInterval;
                var holderNow = GameObject.Find(HolderName);
                if (holderNow != null && TutorialWorldAnchors.HasLiveGuideBody)
                {
                    if (!_standDownTraced)
                    {
                        _standDownTraced = true;
                        FlowTrace.Step("SylasSteward",
                            "the founding guide now HAS a world body (WO-961) - standing the steward " +
                            "stand-in DOWN so exactly one guide figure is ever present. The stand-in is " +
                            "the body-less fallback only; it is not deleted, so a failed guide summon " +
                            "still degrades to a real character instead of empty air.");
                    }
                    Destroy(holderNow);
                }
            }

            if (Time.unscaledTime < _nextPollAt) return;
            _nextPollAt = Time.unscaledTime + DespawnPollInterval;
            if (!ArcIncomplete())
            {
                var holder = GameObject.Find(HolderName);
                if (holder != null)
                {
                    FlowTrace.Step("SylasSteward", "founding arc complete (Onboarded) — despawning Sylas's body (owner: 'use the model, then unload it'); injector stays resident for a New Game this run.");
                    Destroy(holder);
                }
            }
        }

        /// <summary>The founding-arc-incomplete gate — the SAME signal the FTUE peace
        /// window keys on (ff.tutorialv2 + !Onboarded). No new flag (owner sequencing ruling).</summary>
        private static bool ArcIncomplete()
        {
            if (!FeatureFlags.TutorialV2) return false;
            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            return state != null && !state.Onboarded;
        }

        private void Inject()
        {
            using var _ = FlowTrace.Enter("SylasSteward", "Inject");

            if (!ArcIncomplete())
            {
                FlowTrace.Step("SylasSteward", "arc already complete (Onboarded) or state unavailable — no steward spawned.");
                return;
            }

            // WO-1014: never seat a stand-in for a guide that already has a body. This is
            // the RELOAD / re-enter case (a save resumed mid-arc, a hub re-load after the
            // summon); the first-run case — hub loads, steward seats, wolf summons a beat
            // later — is caught by the guide-body watch in Update. Same single authority.
            if (TutorialWorldAnchors.HasLiveGuideBody)
            {
                FlowTrace.Step("SylasSteward",
                    "the founding guide ALREADY has a world body - stand-in NOT seated (WO-1014: one " +
                    "guide figure, ever; 'world.guide' resolves to the live body at the head of the chain).");
                return;
            }

            // Idempotent: nuke any prior runtime holder so a re-load never double-spawns.
            var prior = GameObject.Find(HolderName);
            if (prior != null) Destroy(prior);

            var holder = new GameObject(HolderName);

            // Position: the live Heart (the tree, scene centre) + a courtyard offset,
            // snapped to the walkable NavMesh. Heart unresolved => canon origin (0,0,0).
            var heart = FindAnyObjectByType<HeartController>();
            if (heart == null)
                FlowTrace.Warn("SylasSteward", "Inject: no HeartController resolved — steward anchors to the canon origin (0,0,0) + courtyard offset.");
            Vector3 basePos = (heart != null ? heart.transform.position : Vector3.zero) + CourtyardOffset;
            if (NavMesh.SamplePosition(basePos, out var hit, 8f, NavMesh.AllAreas))
                basePos = hit.position;

            // Face the Heart/tree — the greeting reads "at the tree", not into a wall.
            Vector3 toHeart = (heart != null ? heart.transform.position : Vector3.zero) - basePos;
            toHeart.y = 0f;
            Quaternion rot = toHeart.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(toHeart.normalized, Vector3.up)
                : Quaternion.identity;

            GameObject body = null;
            Guard.Try("SylasSteward", "spawn steward body", () => body = SpawnBody(basePos, rot, holder.transform));
            if (body == null)
            {
                FlowTrace.Fail("SylasSteward", "Inject: body spawn failed entirely — 'world.guide' falls back to the Heart / synthetic town anchor (flow degrades, never blocks).");
                return;
            }

            // THE LOAD-BEARING NAME: TutorialWorldAnchors.ResolveGuide finds this
            // stand-in by GameObject.Find("Sylas") when no pet-Echo body is deployed
            // (WO-1012 P2) — this is what points the spotlight at him.
            body.name = "Sylas";

            Guard.Try("SylasSteward", "attach steward Talk", () => AttachInteraction(body));

            FlowTrace.Step("SylasSteward", $"Sylas steward spawned at {basePos} (near the Heart, facing the tree) — founding beats have a body.");
        }

        // ── Body (the introducer/vendor idiom: Resources body + normalize + seat) ──

        private GameObject SpawnBody(Vector3 pos, Quaternion rot, Transform parent)
        {
            using var _ = FlowTrace.Enter("SylasSteward", "SpawnBody");

            GameObject prefab = null;
            string usedKey = null;
            foreach (var key in new[] { StewardBody, FallbackBody })
            {
                FlowTrace.Try("SylasSteward", $"Resources.Load '{key}'",
                    () => { if (prefab == null) prefab = Resources.Load<GameObject>(key); });
                if (prefab != null) { usedKey = key; break; }
            }
            if (prefab == null)
            {
                FlowTrace.Warn("SylasSteward", "no body prefab found (Models gitignored on a fresh clone?) — capsule placeholder used.");
                return SpawnPlaceholder(pos, rot, parent);
            }

            GameObject go = null;
            FlowTrace.Try("SylasSteward", "Instantiate steward body",
                () => go = Instantiate(prefab, pos, rot, parent));
            if (go == null)
            {
                FlowTrace.Fail("SylasSteward", $"Instantiate returned null for '{usedKey}' — capsule placeholder used.");
                return SpawnPlaceholder(pos, rot, parent);
            }

            NormalizeToHeroHeight(go);
            NpcGroundSeat.Seat(go, pos.y);

            // STATIC body: no wander (owner spec), belt-and-braces disable any agent.
            var npc = go.GetComponent<AmbientNPC>();
            if (npc != null) npc.Configure(TownsfolkDialogue.Archetype.Villager, /*wander*/ false, pos);
            var agent = go.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            // Render-verify (§12: anything that renders can be broken).
            int enabled = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                if (r != null && r.enabled) enabled++;
            if (enabled <= 0)
                FlowTrace.Fail("SylasSteward", $"steward body from '{usedKey}' has NO enabled renderer — Sylas will be invisible.");
            else
                FlowTrace.Step("SylasSteward", $"steward body OK from '{usedKey}' ({enabled} enabled renderer(s)).");

            return go;
        }

        private static GameObject SpawnPlaceholder(Vector3 pos, Quaternion rot, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.SetParent(parent, false);
            go.transform.position = pos + Vector3.up * 1f;
            go.transform.rotation = rot;
            var col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;   // never block the hero
            return go;
        }

        private void AttachInteraction(GameObject body)
        {
            if (body == null) return;
            var interact = body.AddComponent<SylasStewardInteractable>();
            if (interact == null)
                FlowTrace.Fail("SylasSteward", "AttachInteraction: AddComponent failed — Sylas is visible but un-talkable (the flow's auto-played beats still run).");
        }

        private static void NormalizeToHeroHeight(GameObject go)
        {
            float scale = 1f;
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                if (b.size.y > 0.01f) scale = 1.95f / b.size.y;
            }
            if (scale > 0.01f && !Mathf.Approximately(scale, 1f))
            {
                go.transform.localScale *= scale;
                var bubbleRoot = go.transform.Find("BubbleRoot");
                if (bubbleRoot != null) bubbleRoot.localScale = Vector3.one / Mathf.Max(0.01f, scale);
            }
        }
    }

    // =========================================================================
    // SylasStewardInteractable — courtesy Talk: replays the CURRENT founding
    // step's intro line. Mirrors CompanionIntroducerInteractable's registry shape
    // (proximity TalkPromptRegistry, suppressed during dialogue/build mode) but:
    //   * routes to TutorialFlow.CurrentIntroDialogueId (the live beat's line),
    //   * NEVER auto-fires (the flow auto-plays each beat on step enter),
    //   * never one-shots — re-talk = re-hear the current instruction.
    // =========================================================================
    [DisallowMultipleComponent]
    public sealed class SylasStewardInteractable : MonoBehaviour
    {
        private const float ActivateRadius = 6f;

        private Transform _hero;

        private void Update()
        {
            if (_hero == null) { ResolveHero(); return; }

            // Build mode / open dialogue: drop the prompt (same rules as every NPC).
            if (MobileInteractButton.Suppressed || CoreDialogue.DialogueService.IsRunning)
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
            var flow = FindAnyObjectByType<TutorialFlow>();
            string dialogueId = flow != null ? flow.CurrentIntroDialogueId : null;
            if (string.IsNullOrEmpty(dialogueId))
            {
                FlowTrace.Step("SylasSteward", "Talk: no live founding step with an intro line — nothing to replay.");
                return;
            }
            if (CoreDialogue.DialogueService.Play(dialogueId))
                FlowTrace.Step("SylasSteward", $"Talk: replayed the current beat's line '{dialogueId}'.");
            else
                FlowTrace.Warn("SylasSteward", $"Talk: DialogueService.Play('{dialogueId}') returned false — row missing?");
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
