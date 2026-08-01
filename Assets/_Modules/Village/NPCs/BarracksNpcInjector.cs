// =============================================================================
// BarracksNpcInjector — runtime, NON-DESTRUCTIVE placement of the drillmaster NPC
// at the castle Barracks (WO-453 troop-training flow). Mirrors
// CastleVendorNpcInjector's self-bootstrap, but keys off the single 'CastleBarracks'
// building GameObject (placed by CastleBarracksPlacer) instead of the storefront
// "NPC_<Role>_Interactable" markers — the barracks prefab has no such marker child.
// -----------------------------------------------------------------------------
// WHY a runtime injector (not a scene edit / regen):
//   The barracks is a polyperfect prefab dropped into MainCastle_Hall by the
//   CastleBarracksPlacer editor tool. Re-saving / regenerating that scene to add a
//   real NPC body carries the project's known scene-resave corruption risk
//   (CLAUDE.md §3). So this self-bootstrapping DDOL singleton, on every
//   MainCastle_Hall load, FINDS the 'CastleBarracks' root and spawns a static
//   drillmaster NPC in FRONT of it — WITHOUT ever touching the .unity file.
//   Idempotent per load (a re-load nukes the prior runtime holder).
//
// STATIC, not townsfolk: reuses the same Resources/NPCs People-pack body source +
//   AmbientNPC.Configure(arch, wander:FALSE, ...) the vendor injector uses, so the
//   drillmaster stands his ground (idle sway only, no roam/follow).
//
// INTERACTION: the SAME slim CastleNpcInteractable the vendors use, configured with
//   structureId "barracks". Its Talk opens DialogueService.PlayStructure("barracks",
//   "Barracks") → the Barracks_MainMenu Yarn node (PlayStructure routes "barracks"
//   to that node, mirroring the pet-house branch). No reflection, no cross-asmdef ref.
// =============================================================================

using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Runtime, non-destructive placement of the drillmaster NPC at the castle Barracks.</summary>
    public sealed class BarracksNpcInjector : MonoBehaviour
    {
        public static BarracksNpcInjector Instance { get; private set; }

        private const string TargetScene = "MainCastle_Hall";
        // WO-608 merge: castle-hub chrome must fire on the merged Main_Castle_Overworld too,
        // while staying castle-only. Mirrors CastleBeamHider / CastleVendorNpcInjector.
        private const string MergedTargetScene = "Main_Castle_Overworld";
        private static bool IsCastleHubScene(string n) => n == TargetScene || n == MergedTargetScene;

        // The building root CastleBarracksPlacer drops into the scene.
        private const string BarracksRootName = "CastleBarracks";

        // Resources/NPCs People-pack body — the smith reads as a fitting drillmaster
        // (the vendor injector uses the same body source). Merchant is the safe fallback.
        private const string BodyDrillmaster = "NPCs/NPC_Blacksmith";
        private const string BodyFallback    = "NPCs/NPC_Merchant";

        private const string StructureId = "barracks";
        private const string Label        = "Barracks";

        // How far IN FRONT of the building origin the drillmaster stands (toward the
        // plaza / approaching hero). The barracks faces castle centre, so place the NPC
        // along the building's forward and let the navmesh snap settle the exact spot.
        private const float FrontOffset = 4.5f;

        private const string HolderName = "BarracksNPC (runtime)";

        // WO-724: the unlock (ff.barracks + founding-complete) can flip true LIVE while the
        // player is standing in the hub - the FTUE completes IN-scene (the town wave loop
        // kicks with no reload; TutorialFlow.FinishFlow). A cheap 1 Hz poll surfaces the
        // Barracks the moment founding completes, without waiting for the next hub load.
        private const float UnlockPollInterval = 1f;
        private float _nextPollAt;

        // F8 seq528: true when the current drillmaster is anchored to a PLACED catalog
        // barracks (placed wins); false = baked-anchored, so the poll keeps watching for a
        // placed one to reseat onto.
        private bool _anchoredToPlaced;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("BarracksNpcInjector").AddComponent<BarracksNpcInjector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (IsCastleHubScene(SceneManager.GetActiveScene().name)) Inject();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsCastleHubScene(scene.name)) Inject();
        }

        // WO-724: 1 Hz watch for the unlock flipping true LIVE (founding completes in-hub
        // with no scene reload). When it does, surface the baked Barracks building (the
        // visual injector reactivates + skins it) and then place the drillmaster. Guarded
        // so it only fires once per surfacing (no re-spawn while the holder already stands).
        private void Update()
        {
            if (Time.unscaledTime < _nextPollAt) return;
            _nextPollAt = Time.unscaledTime + UnlockPollInterval;

            if (!IsCastleHubScene(SceneManager.GetActiveScene().name)) return;
            if (!DeNelle.Village.BarracksUnlock.IsUnlocked) return;
            if (GameObject.Find(HolderName) != null)
            {
                // RESEAT WATCH (owner F8 seq528): the drillmaster stands at the legacy BAKED
                // CastleBarracks but the player has now PLACED a catalog barracks — placed
                // wins, so re-Inject (idempotent: nukes the holder, reseats at the placed
                // instance). One-way: once anchored to a placed barracks it never bounces back.
                if (!_anchoredToPlaced)
                {
                    foreach (var b in Object.FindObjectsByType<Building>(FindObjectsSortMode.None))
                        if (b != null && b.IsAlive &&
                            string.Equals(b.BuildingId, StructureId, System.StringComparison.OrdinalIgnoreCase))
                        {
                            FlowTrace.Step("Barracks",
                                "poll: PLACED barracks detected while the drillmaster is baked-anchored — reseating (placed wins).");
                            Inject();
                            return;
                        }
                }
                return;   // already surfaced this scene (and correctly anchored)
            }

            FlowTrace.Step("Barracks",
                "unlock flipped true in-hub (ff.barracks + founding-complete) - surfacing the Barracks live (1 Hz poll).");
            // Reactivate + skin the baked CastleBarracks the lock had deactivated, then place the NPC.
            HubStructureVisualInjector.EnsureBarracksSurfaced();
            Inject();
        }

        private void Inject()
        {
            using var _ = FlowTrace.Enter("Village", "BarracksNpcInjector.Inject");

            // WO-724 unlock rule (charter OPTION A): the drillmaster only exists when the
            // feature flag is ON (ff.barracks, default OFF) AND founding is complete
            // (GameState.Onboarded). Single source of truth = BarracksUnlock.IsUnlocked;
            // ff.barracks OFF => fully hidden (regression), founding-incomplete => not yet.
            if (!DeNelle.Village.BarracksUnlock.IsUnlocked)
            {
                FlowTrace.Step("Barracks",
                    $"Inject stand-down - Barracks locked (ff.barracks={DeNelle.Core.FeatureFlags.Barracks}, " +
                    $"foundingComplete={DeNelle.Village.BarracksUnlock.FoundingComplete}); no drillmaster.");
                return;
            }
            FlowTrace.Step("Barracks", "Inject - Barracks unlocked (flag ON + founding-complete); placing the drillmaster.");

            // Idempotent: nuke any prior runtime holder so a re-load doesn't double-spawn.
            var prior = GameObject.Find(HolderName);
            if (prior != null) Destroy(prior);

            // WO-812 + owner F8 seq528 ("Barracks has no NPC" — at the barracks SHE placed):
            // a PLACED/replayed catalog barracks (Building id "barracks") anchors the
            // drillmaster FIRST — placed wins, same rule as the vendor eviction; the legacy
            // baked CastleBarracks is the fallback when nothing is placed. Exactly ONE holder
            // spawns (idempotent nuke above), so never two drillmasters. The 1 Hz unlock poll
            // doubles as the reseat watcher: place a Barracks in build mode and the
            // drillmaster moves from the baked anchor within a second.
            GameObject barracks = null;
            foreach (var b in Object.FindObjectsByType<Building>(FindObjectsSortMode.None))
                if (b != null && b.IsAlive &&
                    string.Equals(b.BuildingId, StructureId, System.StringComparison.OrdinalIgnoreCase))
                {
                    barracks = b.gameObject;
                    FlowTrace.Step("Barracks",
                        "BarracksNpcInjector: anchoring the drillmaster to the PLACED catalog barracks (placed wins, WO-812).");
                    break;
                }
            _anchoredToPlaced = barracks != null;

            // NO-DOUBLES STANDDOWN (owner F8 2026-08-01 "Where is the drillmaster?" — she was
            // at the BAKED twin): with a placed barracks live, the legacy baked CastleBarracks
            // still stood re-skinned across town — two identical barracks, drillmaster at one,
            // owner at the other. WO-812's rule ("prefer ONE live Barracks") finishes here:
            // the baked building deactivates while a placed one exists (idempotent; the
            // storefront-standdown pattern — never a scene edit).
            if (_anchoredToPlaced)
            {
                var baked = GameObject.Find(BarracksRootName);
                if (baked != null)
                {
                    baked.SetActive(false);
                    FlowTrace.Step("Barracks",
                        "baked 'CastleBarracks' stood down — the PLACED barracks owns the trade (no doubles).");
                }
            }

            if (barracks == null) barracks = GameObject.Find(BarracksRootName);

            if (barracks == null)
            {
                // Expected pre-placement — Warn (not Fail), still self-reports. WO-812: the fix
                // for this state is now in the player's hands (Build menu -> Barracks).
                FlowTrace.Warn("Village",
                    "BarracksNpcInjector: no baked 'CastleBarracks' AND no placed catalog barracks — drillmaster not placed (build one from the Town palette).");
                Debug.Log("[BarracksNpcInjector] no baked or placed barracks in scene — drillmaster not placed " +
                          "(Build menu -> Barracks places one; first is free).");
                return;
            }

            var holder = new GameObject(HolderName);
            Transform hero = ResolveHero();

            if (SpawnDrillmaster(barracks.transform, hero, holder.transform))
            {
                FlowTrace.Step("Village", "BarracksNpcInjector: placed the drillmaster NPC at the Barracks.");
                Debug.Log("[BarracksNpcInjector] placed the drillmaster NPC at the Barracks.");

                // WO-813 ONCE-TEACH (owner: "some dialogue and raid tutorial"): the first time
                // the Barracks surfaces with its drillmaster after founding, tell the player
                // where the army comes from. One-shot via the SeenTutorials ledger; the full
                // Sylas Yarn beat + the Train-3 task ride the UI seat's copy pass (WO-813 §1).
                var st = DeNelle.Core.State.GameStateService.Instance != null
                    ? DeNelle.Core.State.GameStateService.Instance.State : null;
                if (st != null && st.SeenTutorials != null &&
                    !(st.SeenTutorials.TryGetValue("barracks_intro", out bool seen) && seen))
                {
                    st.SeenTutorials["barracks_intro"] = true;
                    DeNelle.Core.State.GameStateService.Instance.Save();
                    DeNelle.Core.UI.ElarionUiKit.ShowToast(
                        "Elarion needs soldiers. The drillmaster at the Barracks trains them.",
                        DeNelle.Core.UI.ElarionUiKit.ToastTone.Info);
                    FlowTrace.Step("Barracks", "WO-813 once-teach fired (barracks_intro marked seen).");
                }
            }
            else
            {
                FlowTrace.Fail("Village", "BarracksNpcInjector: failed to place the drillmaster NPC.");
                Debug.LogWarning("[BarracksNpcInjector] failed to place the drillmaster NPC.");
            }
        }

        private bool SpawnDrillmaster(Transform barracks, Transform hero, Transform parent)
        {
            using var _ = FlowTrace.Enter("Village", "BarracksNpcInjector.SpawnDrillmaster");

            // CENTER-FACING PLACEMENT (owner 2026-06-21): stand the drillmaster on the barracks' side
            // FACING THE HEART (the tree at castle centre), so it's always between the barracks and the
            // tree ("easier to find"). Was placed along barracks.forward, which didn't point at the tree —
            // that's why the barracks NPC read as "missing". Mirrors CastleVendorNpcInjector.
            Vector3 center = HeartCenter();
            Vector3 toHeart = new Vector3(center.x - barracks.position.x, 0f, center.z - barracks.position.z);
            toHeart = toHeart.sqrMagnitude < 0.01f ? barracks.forward : toHeart.normalized;
            Vector3 pos = barracks.position + toHeart * FrontOffset;
            if (NavMesh.SamplePosition(pos, out var hit, 6f, NavMesh.AllAreas))
                pos = hit.position;
            // Face the Heart / approaching hero.
            Quaternion rot = Quaternion.LookRotation(toHeart, Vector3.up);

            var prefab = Resources.Load<GameObject>(BodyDrillmaster)
                         ?? Resources.Load<GameObject>(BodyFallback);
            if (prefab == null)
            {
                // T/U: load-miss — fall back to a placeholder so the barracks still gets a drillmaster,
                // and self-report (Warn -> break-log).
                FlowTrace.Warn("Village",
                    $"BarracksNpcInjector: no body prefab (missing Resources/{BodyDrillmaster}) — placeholder used.");
                Debug.LogWarning($"[BarracksNpcInjector] no body prefab (missing Resources/{BodyDrillmaster}) — placeholder used.");
                return SpawnPlaceholder(pos, rot, hero, parent);
            }

            GameObject go = null;
            Guard.Try("Village", "instantiate drillmaster body", () =>
            {
                go = Instantiate(prefab, pos, rot, parent);
            });
            if (go == null)
            {
                // G/R: Instantiate returned/threw null — fall back to a placeholder, self-report.
                FlowTrace.Fail("Village",
                    $"BarracksNpcInjector: Instantiate returned null for '{BodyDrillmaster}' — placeholder used.");
                return SpawnPlaceholder(pos, rot, hero, parent);
            }
            go.name = "BarracksDrillmaster";

            // V (render-verify): a body with no enabled mesh reads as an invisible drillmaster. Prove
            // it renders; on failure drop it and fall back to the placeholder.
            if (!VerifyNpcRenders(go, BodyDrillmaster))
            {
                FlowTrace.Fail("Village",
                    $"BarracksNpcInjector: drillmaster body '{BodyDrillmaster}' has no visible mesh — dropping, placeholder used.");
                Destroy(go);
                return SpawnPlaceholder(pos, rot, hero, parent);
            }

            NormalizeToHeroHeight(go);
            NpcGroundSeat.Seat(go, pos.y);

            // STATIC: wander=FALSE → AmbientNPC disables its NavMeshAgent and stands its
            // ground (idle sway only). Blacksmith archetype reads as a gruff drillmaster.
            var npc = go.GetComponent<AmbientNPC>();
            if (npc != null) npc.Configure(TownsfolkDialogue.Archetype.Blacksmith, /*wander*/ false, pos);

            var agent = go.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            AttachInteraction(go, hero);
            return true;
        }

        // Minimal capsule fallback if the People-pack body is absent (Models gitignored
        // on a fresh clone). Getting the INTERACTION working is the priority.
        private bool SpawnPlaceholder(Vector3 pos, Quaternion rot, Transform hero, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "BarracksDrillmaster_Placeholder";
            go.transform.SetParent(parent, false);
            go.transform.position = pos + Vector3.up * 1f;
            go.transform.rotation = rot;

            // Proximity-based interaction → don't let the capsule collider block the hero.
            var col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            AttachInteraction(go, hero);
            return true;
        }

        private void AttachInteraction(GameObject body, Transform hero)
        {
            // G: a throw while wiring the interaction would otherwise spawn a mute, uninteractable
            // drillmaster with no log. Guard it so the failure self-reports (Fail -> break-log).
            Guard.Try("Village", $"attach barracks interaction '{StructureId}'", () =>
            {
                var interact = body.AddComponent<CastleNpcInteractable>();
                interact.Configure(StructureId, Label, hero);
                BuildingInteractable.MarkNpcCovered(StructureId);   // the building defers — NPC owns the talk

                // Always-visible type sign above the drillmaster (same as the vendor NPCs).
                float localHeadClear = SignHeightAboveHead(body);
                InteractableSign.ForStructureId(body, StructureId, localHeadClear);
            });
        }

        // V (render-verify): the spawned body must carry >=1 ENABLED Renderer with an actual mesh.
        // Traces the counts so a capture splits "no mesh" from a real spawn. Returns false => caller
        // drops it + uses a placeholder (never an invisible drillmaster).
        private static bool VerifyNpcRenders(GameObject go, string res)
        {
            if (go == null) return false;
            int total = 0, enabledWithMesh = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                total++;
                if (!r.enabled) continue;
                bool hasMesh =
                    (r is SkinnedMeshRenderer smr && smr.sharedMesh != null) ||
                    (r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null);
                if (hasMesh) enabledWithMesh++;
            }
            bool ok = enabledWithMesh > 0;
            if (!ok)
                FlowTrace.Warn("Village",
                    $"VerifyNpcRenders '{res}': {total} renderer(s), {enabledWithMesh} enabled-with-mesh — reads invisible.");
            return ok;
        }

        private static float SignHeightAboveHead(GameObject body)
        {
            const float WorldClearAboveOrigin = 2.6f;
            float scaleY = body.transform.lossyScale.y;
            return scaleY > 0.01f ? WorldClearAboveOrigin / scaleY : WorldClearAboveOrigin;
        }

        // Reuse the vendor injector's height normalization so the People-pack body sits
        // at ~hero height instead of towering (packs import at varying native scales).
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

        // The castle centre to face the drillmaster toward — the Heart (world-tree). Runtime-found;
        // CastleHubBuilder places it at (0,0,12), the fallback if the controller isn't up yet.
        private static Vector3 HeartCenter()
        {
            var h = FindAnyObjectByType<HeartController>();
            return h != null ? h.transform.position : new Vector3(0f, 0f, 12f);
        }

        private static Transform ResolveHero()
        {
            var tagged = GameObject.FindWithTag("Player");
            if (tagged != null) return tagged.transform;
            foreach (var t in FindObjectsByType<Transform>())
                if (t != null && t.name.StartsWith("Hero")) return t;
            return null;
        }
    }
}
