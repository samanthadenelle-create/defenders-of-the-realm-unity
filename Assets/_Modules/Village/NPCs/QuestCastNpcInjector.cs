// =============================================================================
// QuestCastNpcInjector - runtime, NON-DESTRUCTIVE seating of the QUEST CAST: the
// named characters quests.json addresses by name but that had no body anywhere in
// the hub. Without them the player can Accept and track a quest whose objective
// names a person who does not exist (the owner ruled this a BUG, 2026-08-03: the
// game promises a completability it cannot keep).
// -----------------------------------------------------------------------------
// SCOPE - only the cast members who had NO body. Four of the named cast already
// stand in the hub as unnamed role speakers and are fixed in DATA alone
// (dialogues.json speaker records), not here:
//   Borin Emberhand = the "forge" speaker      (seated by CastleVendorNpcInjector)
//   Halvard         = the "armorer" speaker    (seated by CastleVendorNpcInjector)
//   Old Pell        = the "lumbermill" speaker (seated by CastleVendorNpcInjector)
//   Mother Wren     = the "farm" speaker       (seated by CastleVendorNpcInjector)
// This injector seats the two with no existing body: the Village Elder (at the
// Heart, the anchor the quest text itself names) and Fenn Wildmane (the beast
// trainer - see the ANCHOR CAVEAT below).
//
// WHY a runtime injector: the hub is Main_Castle_Overworld and hand-editing a
// .unity file carries the project's resave-corruption history (CLAUDE.md §3). This
// is the same self-bootstrapping DDOL singleton shape as CastleVendorNpcInjector /
// BarracksNpcInjector / SylasStewardInjector - it never touches a scene file and is
// idempotent per load (a re-load nukes the prior runtime holder).
//
// ANCHOR CAVEAT (owner sign-off pending): quest "vendor.stable" (Wild Hearts) puts
// Fenn Wildmane at a STABLE. No stable exists in structures-catalog.json. The
// pet-house (Echo Hollow) is the only beast/companion building in the game, so Fenn
// is anchored there beside the Echo Warden. Moving him is ONE field
// (CastMember.AnchorBuildingId).
//
// BODIES: reuse only. Both slugs are staged KayKit bodies that NO injector currently
// spawns (Cleric is authored on fountain_healing and Farmer_B on mill - neither has a
// vendor AnchorRole), so nothing in the hub gains a duplicate face and no new art is
// introduced. The slug is an owner-revisable one-word swap, exactly like repo.npcModel.
//
// COMBAT: reuses the ONE combat authority (AmbientNPC.IsCombatActive) via
// CastleVendorWaveHider for the renderers and a direct read in the interactable for
// the Talk prompt. No second combat poll is invented (the law in the vendor file).
//
// Village -> Core only. Guard/FlowTrace per §12 / docs/INSTRUMENTATION_STANDARD.md.
// =============================================================================

using System.Collections;
using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using CoreDialogue = DeNelle.Core.Dialogue;

namespace DeNelle.Village
{
    /// <summary>Runtime, non-destructive seating of the named quest cast that had no body.</summary>
    public sealed class QuestCastNpcInjector : MonoBehaviour
    {
        public static QuestCastNpcInjector Instance { get; private set; }

        private const string TargetScene = "MainCastle_Hall";
        private const string MergedTargetScene = "Main_Castle_Overworld";
        /// <summary>Castle-hub only, the same exact pair the vendor + barracks injectors gate on
        /// (never Village2 / raid targets).</summary>
        private static bool IsCastleHubScene(string n) => n == TargetScene || n == MergedTargetScene;

        private const string HolderName = "QuestCastNPCs (runtime)";

        /// <summary>Slow tick - a building is placed on player time, so the poll never times out
        /// (mirrors CastleVendorNpcInjector.AnchorVendorsToPlacedBuildings).</summary>
        private const float PollSeconds = 2f;

        /// <summary>Ground band around the navmesh Y that a spawn sample must land in, so a
        /// wall-adjacent sample cannot seat an NPC on the elevated wall-walk mesh (the
        /// "NPC on top of the gatehouse" symptom). Same band as NpcGroundSeat / the vendors.</summary>
        private const float GroundBandLow = -0.35f;
        private const float GroundBandHigh = 0.75f;

        /// <summary>One named quest-cast member with no pre-existing body.</summary>
        private struct CastMember
        {
            /// <summary>GameObject name + the label this NPC reports in traces.</summary>
            public string Name;
            /// <summary>dialogues.json id this NPC's Talk plays.</summary>
            public string DialogueId;
            /// <summary>Staged KayKit slug under Resources/NPCs/KayKit/ (owner-revisable one-word swap).</summary>
            public string KayKitSlug;
            /// <summary>People-pack body used when the KayKit slug fails to load.</summary>
            public string FallbackBody;
            /// <summary>Building.BuildingId this NPC stands at; null/empty = anchor to the Heart.</summary>
            public string AnchorBuildingId;
            /// <summary>Offset from the anchor. Heart-anchored: world offset from the trunk.
            /// Building-anchored: (right, up, forward) in the building's own local axes.</summary>
            public Vector3 AnchorOffset;
            public TownsfolkDialogue.Archetype Arch;
        }

        // The cast. Each row is data an owner ruling can retarget without touching logic.
        private static readonly CastMember[] Members =
        {
            // "Speak with the Village Elder at the Heart of Elarion." (quests.json
            // elarion.welcome/meet-elder) - the quest text names the anchor, so the Elder
            // stands at the Heart. Offset puts him south-west of the trunk on the open
            // approach lawn, clear of Sylas's founding-steward spot at heart+(2,0,-9)
            // (SylasStewardInjector.CourtyardOffset) so the two never overlap.
            new CastMember
            {
                Name = "Village Elder",
                DialogueId = "village_elder",
                KayKitSlug = "Cleric",
                FallbackBody = "NPCs/NPC_Peasant_Tob",
                AnchorBuildingId = null,
                AnchorOffset = new Vector3(-6f, 0f, -8f),
                Arch = TownsfolkDialogue.Archetype.Elder,
            },
            // "Train a pet ability with Fenn Wildmane." (quests.json vendor.stable/train-ability).
            // See ANCHOR CAVEAT in the header: no stable structure exists; the pet-house is the
            // nearest authored anchor. Local offset stands him to the building's RIGHT so he does
            // not contest the Echo Warden's front-of-building spot.
            new CastMember
            {
                Name = "Fenn Wildmane",
                DialogueId = "fenn_wildmane",
                KayKitSlug = "Farmer_B",
                FallbackBody = "NPCs/NPC_Peasant_Mevina",
                AnchorBuildingId = "pet-house",
                AnchorOffset = new Vector3(5f, 0f, 2f),
                Arch = TownsfolkDialogue.Archetype.Farmer,
            },
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("QuestCastNpcInjector").AddComponent<QuestCastNpcInjector>();
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

        /// <summary>The live holder every seated cast member parents under. Held as a REFERENCE,
        /// not looked up by name: Destroy is deferred to end-of-frame, so between the nuke and the
        /// fresh spawn below a GameObject.Find(HolderName) can legally return the DOOMED holder and
        /// the pass would parent its NPCs to an object about to vanish. A destroyed holder reads
        /// fake-null here, which is exactly the "poll should stop" signal.</summary>
        private Transform _holder;

        private void Inject()
        {
            using var _ = FlowTrace.Enter("QuestCast", "QuestCastNpcInjector.Inject");

            // Idempotent: nuke any prior runtime holder so a re-load never double-spawns, and
            // stop an in-flight poll from the previous load so a stale coroutine cannot race
            // the fresh pass below.
            if (_holder != null) Destroy(_holder.gameObject);
            StopCoroutine(nameof(SeatCast));

            _holder = new GameObject(HolderName).transform;
            StartCoroutine(nameof(SeatCast));
        }

        /// <summary>Deferred seating pass: each member is placed as soon as its anchor exists.
        /// A building-anchored member whose building is not placed yet simply is not spawned -
        /// the poll keeps watching (placement is a player-paced event, so no timeout). This is
        /// what keeps a WO-834 blank Build-Your-Own town blank: no pet-house, no Fenn.</summary>
        private IEnumerator SeatCast()
        {
            var pending = new System.Collections.Generic.HashSet<string>();
            foreach (var m in Members) pending.Add(m.Name);

            while (pending.Count > 0)
            {
                if (!IsCastleHubScene(SceneManager.GetActiveScene().name)) yield break;  // scene moved on
                if (_holder == null) yield break;   // holder destroyed => the injector re-ran; this pass is stale

                var live = FindObjectsByType<Building>(FindObjectsSortMode.None);
                var heart = FindAnyObjectByType<HeartController>();
                Transform hero = ResolveHero();

                foreach (var m in Members)
                {
                    if (!pending.Contains(m.Name)) continue;

                    // Already seated by an earlier pass of THIS holder? Settle the member. Scoped to
                    // the holder rather than a global find, so a body that a prior Inject already
                    // marked for destruction can never masquerade as a live survivor.
                    if (_holder.Find(m.Name) != null) { pending.Remove(m.Name); continue; }

                    Vector3 pos;
                    Quaternion rot;
                    if (string.IsNullOrEmpty(m.AnchorBuildingId))
                    {
                        // Heart-anchored. The Heart is scene furniture, so this seats on pass 0;
                        // an unresolved Heart falls back to the canon origin rather than skipping.
                        if (heart == null)
                            FlowTrace.Warn("QuestCast",
                                $"{m.Name}: no HeartController resolved - anchoring to the canon origin (0,0,0) + offset.");
                        Vector3 heartPos = heart != null ? heart.transform.position : Vector3.zero;
                        pos = heartPos + m.AnchorOffset;
                        rot = FaceToward(pos, heartPos);
                    }
                    else
                    {
                        Transform anchor = null;
                        foreach (var b in live)
                            if (b != null && b.IsAlive &&
                                string.Equals(b.BuildingId, m.AnchorBuildingId, System.StringComparison.OrdinalIgnoreCase))
                            { anchor = b.transform; break; }
                        if (anchor == null) continue;   // not placed yet - keep watching

                        // Local (right, up, forward) offset so the NPC follows the yaw the player
                        // chose at placement, instead of a world offset that could land inside the mesh.
                        pos = anchor.position
                              + anchor.right * m.AnchorOffset.x
                              + anchor.forward * m.AnchorOffset.z;
                        pos.y = anchor.position.y + m.AnchorOffset.y;
                        rot = FaceToward(pos, HeartCenter());
                    }

                    pos = SampleGround(pos, m.Name);

                    if (SpawnMember(m, pos, rot, hero, _holder))
                    {
                        pending.Remove(m.Name);
                        FlowTrace.Step("QuestCast",
                            $"seated '{m.Name}' at {pos} (anchor={(string.IsNullOrEmpty(m.AnchorBuildingId) ? "Heart" : m.AnchorBuildingId)}, " +
                            $"talk plays dialogue '{m.DialogueId}').");
                    }
                }

                yield return new WaitForSeconds(PollSeconds);
            }

            FlowTrace.Step("QuestCast", "every quest-cast member is seated - poll complete.");
        }

        // ── Body (the vendor/barracks idiom: KayKit first, People next, capsule last) ──

        private bool SpawnMember(CastMember m, Vector3 pos, Quaternion rot, Transform hero, Transform parent)
        {
            using var _ = FlowTrace.Enter("QuestCast", $"SpawnMember '{m.Name}'");

            // KayKit FIRST (WO-818 idiom). These two slugs are staged but un-spawned by any
            // other injector, so seating them adds no duplicate face and no new art. Loaded by
            // path rather than KayKitNpcBody.Load because a story NPC has no catalog row to
            // carry repo.npcModel - the resolver's ResourcesRoot/ArmIdle are still reused.
            string kayKitRes = KayKitNpcBody.ResourcesRoot + m.KayKitSlug;
            GameObject prefab = null;
            Guard.Try("QuestCast", $"load KayKit body '{kayKitRes}'",
                () => prefab = Resources.Load<GameObject>(kayKitRes));
            string bodyRes = kayKitRes;
            if (prefab == null)
            {
                // Authored-but-broken slug: ONE Warn (the F8 harness captures it), then the
                // People chain keeps the character visible - never a blank NPC.
                FlowTrace.Warn("QuestCast",
                    $"'{m.Name}': KayKit body '{m.KayKitSlug}' loads NULL from Resources/{kayKitRes} " +
                    $"- falling back to the People prefab '{m.FallbackBody}'.");
                kayKitRes = null;
                bodyRes = m.FallbackBody;
                Guard.Try("QuestCast", $"load People body '{m.FallbackBody}'",
                    () => prefab = Resources.Load<GameObject>(m.FallbackBody));
            }
            if (prefab == null)
            {
                FlowTrace.Warn("QuestCast",
                    $"'{m.Name}': no body prefab at all (Models gitignored on a fresh clone?) - placeholder used.");
                return SpawnPlaceholder(m, pos, rot, hero, parent);
            }

            GameObject go = null;
            Guard.Try("QuestCast", $"instantiate body '{m.Name}'",
                () => go = Instantiate(prefab, pos, rot, parent));
            if (go == null)
            {
                FlowTrace.Fail("QuestCast",
                    $"'{m.Name}': Instantiate returned null for '{bodyRes}' - placeholder used.");
                return SpawnPlaceholder(m, pos, rot, hero, parent);
            }
            // LOAD-BEARING NAME: the poll's already-seated check is _holder.Find(m.Name), so a
            // re-entered pass never seats a second copy of the same character.
            go.name = m.Name;

            // Render-verify (§12): a body with no enabled mesh reads as a missing character.
            if (!VerifyNpcRenders(go, bodyRes, m.Name))
            {
                FlowTrace.Fail("QuestCast",
                    $"'{m.Name}': body '{bodyRes}' has no visible mesh - dropping, placeholder used.");
                Destroy(go);
                return SpawnPlaceholder(m, pos, rot, hero, parent);
            }

            // WO-833: a KayKit body ships an Animator + Humanoid avatar but NO controller, so it
            // renders its bind pose ("NPC Stuck in T Pose") - arm the shared retargeted idle.
            // People-chain bodies (kayKitRes null) keep their own animator and are never armed.
            if (kayKitRes != null) KayKitNpcBody.ArmIdle(go, kayKitRes, "QuestCast");

            NormalizeToHeroHeight(go);
            NpcGroundSeat.Seat(go, pos.y);

            // STATIC: wander=false so AmbientNPC disables its agent and the character holds its
            // post. No hero is handed over, so the proximity chatter bubble stays quiet and never
            // competes with the Talk dialogue (the vendor rule).
            var npc = go.GetComponent<AmbientNPC>();
            if (npc != null) npc.Configure(m.Arch, /*wander*/ false, pos);

            var agent = go.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            AttachInteraction(go, m, hero);
            return true;
        }

        /// <summary>Capsule fallback so the character is still THERE and talkable when no body
        /// prefab loads (Models are gitignored on a fresh clone). The interaction is the point.</summary>
        private bool SpawnPlaceholder(CastMember m, Vector3 pos, Quaternion rot, Transform hero, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = m.Name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos + Vector3.up * 1f;
            go.transform.rotation = rot;

            // Proximity interaction - the capsule collider must never block the hero.
            var col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            AttachInteraction(go, m, hero);
            return true;
        }

        private void AttachInteraction(GameObject body, CastMember m, Transform hero)
        {
            // A throw while wiring would otherwise leave a mute, un-talkable character with no
            // log - Guard it so the failure self-reports into the break-log.
            Guard.Try("QuestCast", $"attach Talk for '{m.Name}'", () =>
            {
                var interact = body.AddComponent<QuestCastInteractable>();
                interact.Configure(m.DialogueId, m.Name, hero);

                // Duck out of sight for the duration of a wave, exactly like the vendors, off the
                // SAME combat authority (AmbientNPC.IsCombatActive) - no second signal.
                body.AddComponent<CastleVendorWaveHider>();
            });
        }

        // ── Placement helpers (shared shape with the vendor / barracks injectors) ──

        /// <summary>Navmesh-snap a spawn point, rejecting any hit outside the ground band so a
        /// wall-adjacent sample cannot seat the NPC on the elevated wall-walk mesh. An
        /// out-of-band or missing hit keeps the computed courtyard position.</summary>
        private static Vector3 SampleGround(Vector3 pos, string who)
        {
            if (!NavMesh.SamplePosition(pos, out var hit, 6f, NavMesh.AllAreas)) return pos;
            if (hit.position.y >= GroundBandLow && hit.position.y <= GroundBandHigh) return hit.position;
            FlowTrace.Step("QuestCast",
                $"'{who}' spawn sample rejected: navmesh hit y={hit.position.y:F2} outside ground band " +
                $"[{GroundBandLow}..{GroundBandHigh}] (wall-top/elevated mesh) - using the computed position.");
            return pos;
        }

        /// <summary>Flat look-rotation from <paramref name="from"/> toward <paramref name="target"/>;
        /// identity when the two coincide.</summary>
        private static Quaternion FaceToward(Vector3 from, Vector3 target)
        {
            Vector3 dir = target - from;
            dir.y = 0f;
            return dir.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(dir.normalized, Vector3.up)
                : Quaternion.identity;
        }

        /// <summary>The castle centre NPCs face - the Heart (world-tree). Runtime-found;
        /// CastleHubBuilder places it at (0,0,12), the fallback if the controller is not up yet.</summary>
        private static Vector3 HeartCenter()
        {
            var h = FindAnyObjectByType<HeartController>();
            return h != null ? h.transform.position : new Vector3(0f, 0f, 12f);
        }

        /// <summary>The spawned body must carry at least one ENABLED Renderer with an actual mesh.
        /// Traces the counts so a capture splits "no mesh" from a real spawn.</summary>
        private static bool VerifyNpcRenders(GameObject go, string res, string who)
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
                FlowTrace.Warn("QuestCast",
                    $"VerifyNpcRenders '{who}' ('{res}'): {total} renderer(s), {enabledWithMesh} enabled-with-mesh - reads invisible.");
            return ok;
        }

        /// <summary>Scale the body to ~hero height (packs import at varying native scales) and
        /// counter-scale the speech bubble so it keeps its authored size.</summary>
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

        private static Transform ResolveHero()
        {
            var tagged = GameObject.FindWithTag("Player");
            if (tagged != null) return tagged.transform;
            var loco = FindAnyObjectByType<HeroLocomotion>();
            return loco != null ? loco.transform : null;
        }

        /// <summary>Test/oracle seam: the dialogue ids this injector's cast members Talk into.</summary>
        public static string[] CastDialogueIds()
        {
            var ids = new string[Members.Length];
            for (int i = 0; i < Members.Length; i++) ids[i] = Members[i].DialogueId;
            return ids;
        }

        /// <summary>Test/oracle seam: the GameObject names this injector seats.</summary>
        public static string[] CastNames()
        {
            var names = new string[Members.Length];
            for (int i = 0; i < Members.Length; i++) names[i] = Members[i].Name;
            return names;
        }
    }

    // =========================================================================
    // QuestCastInteractable - slim proximity Talk for ONE story NPC. Mirrors
    // SylasStewardInteractable (the precedent for a non-Building hub NPC) rather
    // than CastleNpcInteractable, because a quest-cast member has no structureId:
    // it must NOT MarkNpcCovered a building, claim HudBuildingFocus, or hang an
    // InteractableSign keyed to a structure kind. Talk routes straight to the
    // authored dialogues.json conversation.
    //
    // WO-416 law: the HUD TALK button is the canonical affordance (via
    // TalkPromptRegistry -> TalkHudBridge); this never raises the shared
    // MobileInteractButton and never binds a desktop key.
    // =========================================================================
    /// <summary>Proximity Talk that plays one quest-cast member's authored dialogue.</summary>
    [DisallowMultipleComponent]
    public sealed class QuestCastInteractable : MonoBehaviour
    {
        private const float ActivateRadius = 6f;

        private string _dialogueId;
        private string _label;
        private Transform _hero;

        public void Configure(string dialogueId, string label, Transform hero)
        {
            _dialogueId = dialogueId;
            _label = label;
            _hero = hero;
        }

        /// <summary>Test seam: the dialogue id this NPC's Talk plays.</summary>
        public string DialogueId => _dialogueId;

        private void Update()
        {
            if (_hero == null) { ResolveHero(); return; }

            // Drop the prompt while the builder / a dialogue owns input (every NPC's rule), and
            // while combat is live - the SAME authority CastleVendorWaveHider hides the body on,
            // so an invisible character can never leave a live Talk light behind.
            if (MobileInteractButton.Suppressed ||
                CoreDialogue.DialogueService.IsRunning ||
                AmbientNPC.IsCombatActive)
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
            if (string.IsNullOrEmpty(_dialogueId))
            {
                FlowTrace.Warn("QuestCast", $"Talk on '{_label}' with no dialogue id configured - nothing to play.");
                return;
            }
            if (CoreDialogue.DialogueService.Play(_dialogueId))
                FlowTrace.Step("QuestCast", $"Talk: '{_label}' played dialogue '{_dialogueId}'.");
            else
                FlowTrace.Warn("QuestCast",
                    $"Talk: DialogueService.Play('{_dialogueId}') returned false for '{_label}' - row missing from dialogues.json?");
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
