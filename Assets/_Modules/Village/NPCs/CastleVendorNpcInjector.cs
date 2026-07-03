// =============================================================================
// CastleVendorNpcInjector — runtime, NON-DESTRUCTIVE placement of a STATIC vendor
// NPC at each of the 8 castle storefronts, wired to the existing YarnSpinner
// structure dialogue. Mirrors VillageNpcInjector's self-bootstrap, but spawns
// STAND-STILL NPCs (no wander / no follow) and gates to the castle hub scene.
// -----------------------------------------------------------------------------
// WHY a runtime injector (not a scene edit / regen):
//   CastleHubBuilder bakes the 8 storefronts into MainCastle_Hall.unity, each
//   with an EMPTY marker child named "NPC_<Role>_Interactable" at front-offset
//   (0,0,6). Re-saving / regenerating that scene to drop real NPC bodies in
//   carries the project's known scene-resave corruption risk (CLAUDE.md §3), and
//   the markers are otherwise inert. So this self-bootstrapping DDOL singleton,
//   on every MainCastle_Hall load, FINDS those markers and spawns a real NPC body
//   at each — WITHOUT ever touching the .unity file. Idempotent per load.
//
// STATIC, not townsfolk: we reuse VillageNpcInjector's townsfolk body source
//   (the Resources/NPCs People-pack prefabs: mesh + URP material + Animator +
//   AmbientNPC + TownsfolkBubble), but Configure(arch, wander:FALSE, ...) — which
//   makes AmbientNPC itself disable the NavMeshAgent and stand its ground (see
//   AmbientNPC.Start ~line 190). No roam, no follow. The NPC just stands at the
//   shopfront with its idle sway and faces outward toward the approaching hero.
//
// INTERACTION: a slim CastleNpcInteractable (same proximity F / mobile-tap pattern
//   as BuildingInteractable) opens the ONE parameterized Yarn structure dialogue
//   via DialogueService.PlayStructure(structureId, label). No new dialogue is
//   invented — it's the existing StructureMenu / PetHouse node path.
//
// Village -> Core only; uses DialogueService + PanelManager exactly as the
// existing village interaction code does (no reflection, no cross-asmdef ref).
// =============================================================================

using System.Collections;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Runtime, non-destructive placement of static vendor NPCs at the castle storefronts.</summary>
    public sealed class CastleVendorNpcInjector : MonoBehaviour
    {
        public static CastleVendorNpcInjector Instance { get; private set; }

        private const string TargetScene = "MainCastle_Hall";

        // The empty interact markers CastleHubBuilder bakes under each storefront are
        // named "NPC_<Role>_Interactable" (role = first token of the structure name).
        private const string MarkerPrefix = "NPC_";
        private const string MarkerSuffix = "_Interactable";

        // Resources/NPCs People-pack bodies — same source VillageNpcInjector uses. We
        // pick a body per role for a little visual variety (smith for the metal trades,
        // merchant for the commerce stalls, peasants for the rest). All fall back to the
        // merchant if a specific body is missing.
        private const string BodySmith    = "NPCs/NPC_Blacksmith";
        private const string BodyMerchant = "NPCs/NPC_Merchant";
        private const string BodyPeasantA = "NPCs/NPC_Peasant_Mevina";
        private const string BodyPeasantB = "NPCs/NPC_Peasant_Tob";

        /// <summary>
        /// One vendor definition keyed by the marker ROLE (the first token of the
        /// storefront name, e.g. "Blacksmith"). StructureId is the id the Yarn
        /// StructureMenu / DialogueCommandBridge + ResourceBuildingProgression expect.
        /// </summary>
        private struct Vendor
        {
            public string BodyRes;
            public string StructureId;
            public string Label;
            public TownsfolkDialogue.Archetype Arch;
        }

        // Role -> vendor. The roles come from CastleHubBuilder's 8 structures
        // (header ~25-27): Blacksmith, Lumbermill, Windmill, EchoHollow, Forge,
        // ArcaneTower, Jeweler, Marketplace.
        //
        // structureId mapping rationale (ids verified against
        // Buildings/Progression/ResourceBuildingProgression.cs + Resources/Portraits/*):
        //   - REAL data + portrait exist for: farm, lumbermill, forge, market, pet-house.
        //   - Blacksmith -> "forge"     (metal/weapon trade shares the forge data)
        //   - Lumbermill -> "lumbermill"
        //   - Windmill   -> "farm"      (food production == Farm's Food resource)
        //   - EchoHollow -> "pet-house" (routes to the dedicated PetHouse Yarn node)
        //   - Forge      -> "forge"
        //   - ArcaneTower-> "arcane-tower" (no progression def yet; StructureMenu still
        //                                   opens gracefully — no yield/portrait, talk works)
        //   - Jeweler    -> "jeweler"   (own shoppable storefront — Jeweler's Bench, gem/jewelry goods)
        //   - Marketplace-> "market"
        // The "no def" ids (arcane-tower) are SAFE: CmdStructureStatus tolerates a null
        // Find() and a missing Portraits/<id> image, so the menu still shows + the Talk
        // path runs. See "Uncertainty" in the work-order report.
        private static Vendor VendorFor(string role)
        {
            switch (role.ToLowerInvariant())
            {
                case "blacksmith":
                    // WO-444 (owner 2026-06-13): the BLACKSMITH sells ARMOR, the FORGE sells weapons.
                    // StructureId drives VendorStockContract.AllowedFor — "armorer" => Armor (was "forge"
                    // => Weapon, which made the blacksmith wrongly sell weapons). "armorer" is a recognized
                    // vendor context (AutoPilotDriver storefront set); missing portrait/def degrades gracefully.
                    return new Vendor { BodyRes = BodySmith,    StructureId = "armorer",      Label = "Armorer", Arch = TownsfolkDialogue.Archetype.Blacksmith };
                case "lumbermill":
                    return new Vendor { BodyRes = BodyPeasantB, StructureId = "lumbermill",   Label = "Lumbermill", Arch = TownsfolkDialogue.Archetype.Villager };
                case "windmill":
                    return new Vendor { BodyRes = BodyPeasantA, StructureId = "farm",         Label = "Windmill",   Arch = TownsfolkDialogue.Archetype.Villager };
                case "echohollow":
                    return new Vendor { BodyRes = BodyPeasantA, StructureId = "pet-house",    Label = "Echo Hollow", Arch = TownsfolkDialogue.Archetype.Villager };
                case "forge":
                    return new Vendor { BodyRes = BodySmith,    StructureId = "forge",        Label = "Forge",      Arch = TownsfolkDialogue.Archetype.Blacksmith };
                case "arcanetower":
                    return new Vendor { BodyRes = BodyPeasantB, StructureId = "arcane-tower", Label = "Arcane Tower", Arch = TownsfolkDialogue.Archetype.Elder };
                case "jeweler":
                    return new Vendor { BodyRes = BodyMerchant, StructureId = "jeweler",      Label = "Jeweler",    Arch = TownsfolkDialogue.Archetype.Quartermaster };
                case "marketplace":
                    return new Vendor { BodyRes = BodyMerchant, StructureId = "market",       Label = "Marketplace", Arch = TownsfolkDialogue.Archetype.Quartermaster };
                case "apothecary":
                    // Owner F8 2026-07-02 ("should have a NPC"): the Apothecary is a RUNTIME
                    // station (CraftingStationInjector) with no baked marker, so it's spawned by
                    // the deferred pass below, not the marker loop. structureId "apothecary" has
                    // an authored conversation (dialogues.json) ending in OpenAlchemy — the same
                    // ConsumableCrafting panel the station's own interact opens.
                    return new Vendor { BodyRes = BodyPeasantA, StructureId = "apothecary",   Label = "Apothecary", Arch = TownsfolkDialogue.Archetype.Villager };
            }
            // Unknown role -> generic merchant talking to the market. Never silently skip,
            // so a future storefront still gets a working NPC.
            return new Vendor { BodyRes = BodyMerchant, StructureId = "market", Label = role, Arch = TownsfolkDialogue.Archetype.Quartermaster };
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("CastleVendorNpcInjector").AddComponent<CastleVendorNpcInjector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (SceneManager.GetActiveScene().name == TargetScene) Inject();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TargetScene) Inject();
        }

        // Holder so re-injection (idempotent) is trivial: clear the prior holder, respawn.
        private const string HolderName = "CastleVendorNPCs (runtime)";

        private void Inject()
        {
            // Idempotent: nuke any prior runtime holder so a re-load doesn't double-spawn.
            var prior = GameObject.Find(HolderName);
            if (prior != null) Destroy(prior);

            var holder = new GameObject(HolderName);
            Transform hero = ResolveHero();

            // Collect the storefront interact markers by name (prefix + suffix). They are
            // empty children CastleHubBuilder placed at the front of each building.
            var all = FindObjectsByType<Transform>();
            int placed = 0;
            foreach (var t in all)
            {
                if (t == null) continue;
                string n = t.name;
                if (!n.StartsWith(MarkerPrefix) || !n.EndsWith(MarkerSuffix)) continue;

                // role = the bit between "NPC_" and "_Interactable" (e.g. "Blacksmith").
                string role = n.Substring(MarkerPrefix.Length, n.Length - MarkerPrefix.Length - MarkerSuffix.Length);
                if (string.IsNullOrEmpty(role)) continue;

                if (SpawnVendor(t, role, hero, holder.transform)) placed++;
            }

            // U: placed==0 means no storefront got a vendor (markers missing or every spawn failed) —
            // Fail-loud to the break-log; a healthy run Steps the count.
            if (placed == 0)
                FlowTrace.Fail("Village",
                    "CastleVendorNpcInjector: placed 0 static vendor NPCs — no storefront marker spawned a body.");
            else
                FlowTrace.Step("Village",
                    $"CastleVendorNpcInjector: placed {placed} static vendor NPCs at castle storefronts.");
            Debug.Log($"[CastleVendorNpcInjector] placed {placed} static vendor NPCs at castle storefronts.");

            // Owner F8 2026-07-02 ("Interact: Apothecary" bare prompt — "should have a NPC"):
            // the Apothecary is a RUNTIME station (CraftingStationInjector), so it has no baked
            // "NPC_*_Interactable" marker for the loop above to find. Deferred pass: wait for the
            // station's Building to exist (injector order is nondeterministic), then spawn its
            // herbalist through the SAME SpawnVendor path as every storefront NPC.
            StopCoroutine(nameof(SpawnApothecaryWhenReady));
            StartCoroutine(nameof(SpawnApothecaryWhenReady));
        }

        // Deferred apothecary NPC: polls (a few frames, ~6s cap) for the runtime-injected
        // Apothecary Building, then reuses SpawnVendor with a synthetic marker child so the
        // placement/interaction/sign composition is identical to the baked storefronts.
        private IEnumerator SpawnApothecaryWhenReady()
        {
            const float TimeoutSeconds = 6f;
            float deadline = Time.unscaledTime + TimeoutSeconds;
            while (Time.unscaledTime < deadline)
            {
                if (SceneManager.GetActiveScene().name != TargetScene) yield break; // scene moved on
                if (GameObject.Find("CastleVendor_Apothecary") != null ||
                    GameObject.Find("CastleVendor_Apothecary_Placeholder") != null)
                    yield break;                                                    // already placed

                Building station = null;
                foreach (var b in FindObjectsByType<Building>())
                    if (b != null && b.Type == BuildingType.ApothecaryWorkbench) { station = b; break; }

                if (station != null)
                {
                    var holder = GameObject.Find(HolderName);
                    if (holder == null) yield break;   // injector re-ran / tearing down

                    // Synthetic marker AT the station (parent = station transform) — SpawnVendor
                    // reads the marker-to-parent distance (<1m => default 5m front offset) and
                    // faces the NPC toward the Heart, exactly like the baked markers.
                    var marker = new GameObject("NPC_Apothecary_Marker (runtime)");
                    marker.transform.SetParent(station.transform, false);

                    bool ok = SpawnVendor(marker.transform, "Apothecary", ResolveHero(), holder.transform);
                    Destroy(marker);
                    if (ok) FlowTrace.Step("Village",
                        "CastleVendorNpcInjector: apothecary NPC placed at the runtime station (deferred pass).");
                    else FlowTrace.Fail("Village",
                        "CastleVendorNpcInjector: apothecary NPC spawn FAILED at the runtime station.");
                    yield break;
                }
                yield return null;
            }
            // Station never appeared — self-report (the bare-prompt symptom would persist).
            FlowTrace.Warn("Village",
                "CastleVendorNpcInjector: apothecary station never appeared within 6s — no apothecary NPC placed.");
        }

        /// <summary>Spawns ONE static NPC at the marker and attaches the interaction.</summary>
        // The castle centre to face NPCs toward — the Heart (world-tree). Runtime-found; CastleHubBuilder
        // places it at (0,0,12), the fallback used if the controller isn't up yet.
        private static Vector3 HeartCenter()
        {
            var h = FindAnyObjectByType<HeartController>();
            return h != null ? h.transform.position : new Vector3(0f, 0f, 12f);
        }

        private bool SpawnVendor(Transform marker, string role, Transform hero, Transform parent)
        {
            using var _ = FlowTrace.Enter("Village", $"CastleVendorNpcInjector.SpawnVendor role='{role}'");
            Vendor v = VendorFor(role);

            var prefab = Resources.Load<GameObject>(v.BodyRes)
                         ?? Resources.Load<GameObject>(BodyMerchant);
            if (prefab == null)
            {
                // T/U: load-miss — fall back to a placeholder so the storefront still gets a working
                // vendor, and self-report (Warn -> break-log instead of a swallowed LogWarning).
                FlowTrace.Warn("Village",
                    $"CastleVendorNpcInjector: no body prefab for role '{role}' (missing Resources/{v.BodyRes}) — placeholder used.");
                Debug.LogWarning($"[CastleVendorNpcInjector] no body prefab for role '{role}' (missing Resources/{v.BodyRes}) — placeholder used.");
                return SpawnPlaceholder(marker, role, v, hero, parent);
            }

            // CENTER-FACING PLACEMENT (owner 2026-06-21): put every vendor on the building's side
            // FACING THE HEART (the tree at castle centre), at the marker's tuned distance — so NPCs are
            // always BETWEEN their building and the tree, never behind/beside it ("easier to find").
            // Preserves the hand-baked front-offset DISTANCE; only redirects WHICH side it sits on.
            Vector3 buildingPos = marker.parent != null ? marker.parent.position : marker.position;
            Vector3 flatBuild = new Vector3(buildingPos.x, 0f, buildingPos.z);
            float frontDist = Vector3.Distance(flatBuild, new Vector3(marker.position.x, 0f, marker.position.z));
            if (frontDist < 1f) frontDist = 5f;   // marker sits at the building -> default front distance
            Vector3 center = HeartCenter();
            Vector3 toHeart = new Vector3(center.x - buildingPos.x, 0f, center.z - buildingPos.z);
            toHeart = toHeart.sqrMagnitude < 0.01f ? Vector3.forward : toHeart.normalized;
            Vector3 pos = flatBuild + toHeart * frontDist;
            if (NavMesh.SamplePosition(pos, out var hit, 4f, NavMesh.AllAreas))
                pos = hit.position;
            // Face the Heart / approaching hero (the hero comes from the centre).
            Quaternion rot = Quaternion.LookRotation(toHeart, Vector3.up);

            GameObject go = null;
            Guard.Try("Village", $"instantiate vendor body role='{role}'", () =>
            {
                go = Instantiate(prefab, pos, rot, parent);
            });
            if (go == null)
            {
                // G/R: Instantiate returned/threw null — fall back to a placeholder so the storefront
                // is never left vendorless, and self-report.
                FlowTrace.Fail("Village",
                    $"CastleVendorNpcInjector: Instantiate returned null for role '{role}' ('{v.BodyRes}') — placeholder used.");
                return SpawnPlaceholder(marker, role, v, hero, parent);
            }
            go.name = $"CastleVendor_{role}";

            // V (render-verify): a body with no enabled mesh reads as an invisible vendor. Prove it
            // renders; on failure drop it and fall back to the placeholder (never an invisible vendor).
            if (!VerifyNpcRenders(go, v.BodyRes))
            {
                FlowTrace.Fail("Village",
                    $"CastleVendorNpcInjector: vendor body role='{role}' ('{v.BodyRes}') has no visible mesh — dropping, placeholder used.");
                Destroy(go);
                return SpawnPlaceholder(marker, role, v, hero, parent);
            }

            NormalizeToHeroHeight(go);
            // T-033 ("NPCs floating"): scaling about a non-feet pivot lifts the model's
            // feet off the floor AND the NavMesh-sampled Y sits a touch ABOVE the visual
            // floor (voxel cell height). Raycast DOWN to the real floor collider and seat
            // the renderer-bounds bottom onto it; falls back to the navmesh Y if no floor
            // is hit. (Replaces the old seat-to-navmesh-Y that left them hovering.)
            NpcGroundSeat.Seat(go, pos.y);

            // STATIC: Configure with wander=FALSE. AmbientNPC.Start then disables its
            // NavMeshAgent and the NPC stands its ground (idle sway only — no roam/follow).
            var npc = go.GetComponent<AmbientNPC>();
            if (npc != null)
            {
                npc.Configure(v.Arch, /*wander*/ false, pos);
                // Do NOT hand it the hero: with no hero the townsfolk proximity-chatter
                // bubble stays quiet, so it never competes with the structure dialogue our
                // CastleNpcInteractable opens. (The idle visual + animator still run.)
            }
            // Belt-and-braces: if a NavMeshAgent slips through (e.g. AmbientNPC missing),
            // make sure nothing tries to move the body.
            var agent = go.GetComponent<NavMeshAgent>();
            if (agent != null) { agent.enabled = false; }

            AttachInteraction(go, v, hero);
            return true;
        }

        // Minimal capsule fallback if the People-pack body is absent (Models gitignored on
        // a fresh clone). Getting the INTERACTION working is the priority. // TODO real NPC art
        private bool SpawnPlaceholder(Transform marker, string role, Vendor v, Transform hero, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"CastleVendor_{role}_Placeholder";
            go.transform.SetParent(parent, false);

            Vector3 pos = marker.position;
            if (NavMesh.SamplePosition(pos, out var hit, 4f, NavMesh.AllAreas)) pos = hit.position;
            go.transform.position = pos + Vector3.up * 1f;
            go.transform.rotation = marker.rotation;

            // The capsule's collider would block the hero; the interaction is proximity-based,
            // so make it non-blocking.
            var col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            AttachInteraction(go, v, hero);
            return true;
        }

        private void AttachInteraction(GameObject body, Vendor v, Transform hero)
        {
            // G: a throw while wiring the interaction would otherwise spawn a mute, uninteractable
            // vendor with no log. Guard it so the failure self-reports (Fail -> break-log) and is skipped.
            Guard.Try("Village", $"attach vendor interaction '{v.StructureId}'", () =>
            {
                var interact = body.AddComponent<CastleNpcInteractable>();
                interact.Configure(v.StructureId, v.Label, hero);
                BuildingInteractable.MarkNpcCovered(v.StructureId);   // the matching building defers its prompt — NPC owns the talk

                // T-034: an always-visible type sign floats above the vendor so the player
                // can tell a shop from an upgrade from a talk NPC from a distance. The body
                // is rescaled to hero height, so place the sign in the body's LOCAL space at
                // a height that clears the head regardless of the (already-applied) scale.
                float localHeadClear = SignHeightAboveHead(body);
                InteractableSign.ForStructureId(body, v.StructureId, localHeadClear);
            });
        }

        // V (render-verify): the spawned body must carry >=1 ENABLED Renderer with an actual mesh.
        // Traces the counts so a capture splits "no mesh" from a real spawn. Returns false => caller
        // drops it + uses a placeholder (never an invisible vendor).
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

        /// <summary>
        /// Local-space Y (in the body's already-scaled frame) that floats the sign just
        /// above the NPC's head. Converts a fixed world clearance through the body's
        /// lossy scale so the sign sits the same world distance above every NPC, no
        /// matter the pack body's native scale.
        /// </summary>
        private static float SignHeightAboveHead(GameObject body)
        {
            const float WorldClearAboveOrigin = 2.6f; // ~head height (1.95) + a touch of air
            float scaleY = body.transform.lossyScale.y;
            return scaleY > 0.01f ? WorldClearAboveOrigin / scaleY : WorldClearAboveOrigin;
        }

        // Reuse VillageNpcInjector's height normalization so the People-pack bodies sit at
        // ~hero height instead of towering (the packs import at varying native scales).
        private static void NormalizeToHeroHeight(GameObject go)
        {
            float npcScale = 1f;
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                if (b.size.y > 0.01f) npcScale = 1.95f / b.size.y;   // ~hero height (1.8) + a touch
            }
            if (npcScale > 0.01f && !Mathf.Approximately(npcScale, 1f))
            {
                go.transform.localScale *= npcScale;
                // Keep any speech bubble at real world size on the rescaled body.
                var bubbleRoot = go.transform.Find("BubbleRoot");
                if (bubbleRoot != null) bubbleRoot.localScale = Vector3.one / Mathf.Max(0.01f, npcScale);
            }
        }

        // (Ground-seating now lives in the shared NpcGroundSeat helper — it raycasts to
        // the real floor instead of the navmesh Y, fixing the residual hover. See T-033.)

        // Name-based hero lookup (matches AmbientNPC / VillageNpcInjector): the hero rig is
        // named "Hero (...)"; the project also tags it "Player".
        private static Transform ResolveHero()
        {
            var tagged = GameObject.FindWithTag("Player");
            if (tagged != null) return tagged.transform;
            foreach (var t in FindObjectsByType<Transform>())
                if (t != null && t.name.StartsWith("Hero")) return t;
            return null;
        }
    }

    // =========================================================================
    // CastleNpcInteractable — slim proximity [F] / mobile-tap interaction that
    // opens the parameterized Yarn structure dialogue for ONE vendor. Mirrors
    // BuildingInteractable's pattern (ActivateRadius ~6m, registers the shared
    // MobileInteractButton, only the NEAREST in-range acts on the global F key,
    // suppressed while a dialogue / modal panel is open) but is keyed by an explicit
    // structureId instead of a Building component — the castle NPCs have no Building.
    // =========================================================================
    [DisallowMultipleComponent]
    public sealed class CastleNpcInteractable : MonoBehaviour
    {
        private const float ActivateRadius = 6f;
        private const float StructureCloseRadius = ActivateRadius + 4f;

        private string _structureId;
        private string _label;

        // TEST SEAM (data-verify, 2026-06-20): the last routing DECISION Interact() made, exposed so
        // the headless AutoPilot oracle (AssertVendorTalkRoute) can assert a SHOPPABLE vendor's Talk
        // press routes to the dialogue ("talk-dialogue"), NOT the upgrade panel ("upgrade-panel").
        // This is observable WITHOUT rendering — the Yarn dialogue + UITK upgrade panel are both
        // invisible in -nographics, so the opened surface can't distinguish the routes; the decision can.
        public static string LastInteractRoute;
        public static string LastInteractId;
        private Transform _hero;
        private bool _openedStructure;

        public void Configure(string structureId, string label, Transform hero)
        {
            _structureId = structureId;
            _label = label;
            _hero = hero;
        }

        private void Update()
        {
            if (_hero == null) { ResolveHero(); return; }

            // Build mode: release + bail (player is authoring, not interacting).
            if (MobileInteractButton.Suppressed)
            {
                MobileInteractButton.Release(this);
                TalkPromptRegistry.Deregister(transform);
                return;
            }

            float distSqr = (_hero.position - transform.position).sqrMagnitude;
            bool inRange = distSqr <= ActivateRadius * ActivateRadius;

            // Walk-away auto-close: if WE opened the structure dialogue and the hero left.
            if (_openedStructure)
            {
                if (!DialogueService.IsRunning) _openedStructure = false;
                else if (distSqr > StructureCloseRadius * StructureCloseRadius)
                {
                    DialogueService.Stop();
                    _openedStructure = false;
                }
            }

            // While any dialogue is on screen, drop our prompt so it doesn't stack under it.
            if (DialogueService.IsRunning)
            {
                MobileInteractButton.Release(this);
                TalkPromptRegistry.Deregister(transform);
                return;
            }

            if (inRange)
            {
                // WO-416: do NOT raise the shared MobileInteractButton for talk NPCs. The HUD
                // TALK button (+ its glow) is the canonical interaction trigger now, so the old
                // bottom-centre "Talk: <name>" element was a redundant duplicate at vendors. We
                // still register with TalkPromptRegistry so the HUD TALK button fires us, and the
                // desktop [F] path below is untouched.
                TalkPromptRegistry.Register(transform, Interact);
                // Owner severe: flip the HUD context button to its UPGRADE face when near an
                // upgradable storefront. The NPC-fronted path never set HudBuildingFocus, so the
                // button never swapped (trace: focus='<none>'). Match BuildingInteractable.
                if (IsUpgradableId(_structureId)) HudBuildingFocus.Set(_structureId);
                else                              HudBuildingFocus.Clear(_structureId);
            }
            else
            {
                MobileInteractButton.Release(this);
                TalkPromptRegistry.Deregister(transform);
                HudBuildingFocus.Clear(_structureId);
            }

            // Mobile-first: the HUD TALK button (via TalkPromptRegistry above) is the
            // canonical trigger. The desktop F-key trigger was removed.
        }

        private void Interact()
        {
            if (string.IsNullOrEmpty(_structureId)) return;
            // DATA-VERIFY (owner 2026-06-20, never inference-fix): log the routing inputs + chosen
            // branch so a HEADLESS capture PROVES Talk reaches the Buy/Sell dialogue for shoppable
            // vendors (forge/armorer/market/jeweler) instead of being stolen by the upgrade panel.
            LastInteractId = _structureId;
            LastInteractRoute = ResolveRoute(_structureId);
            FlowTrace.Step("Village", $"CastleNpc.Interact '{_label}' id='{_structureId}' -> route={LastInteractRoute}");
            // §12 / WO-413: the castle vendor NPCs are the primary live interaction surface (the
            // home hub is MainCastle_Hall). They open the SAME parameterized StructureMenu, so the
            // shop-vs-upgrade split is decided data-driven by that node's gates (seeded from
            // BuildingCatalog caps in CmdStructureStatus) — never here. Trace the id used so a
            // "wrongly offers shop" report maps to the catalog entry behind it.
            // UPGRADE-ONLY buildings route STRAIGHT to the code-built Building Upgrade panel (owner:
            // Mill/Old Pell, Arcane Tower must NOT show a Yarn menu). But a SHOPPABLE vendor (forge,
            // armorer, market, jeweler) is a TALK target first: Talk fires the vendor DIALOGUE
            // (Buy/Sell/Leave + quest options), per owner 2026-06-21. Its upgrade, if any, is reached
            // through the dedicated HUD context/Upgrade button — never by stealing the Talk press.
            if (LastInteractRoute == "upgrade-panel")
            {
                if (PanelRouter.Open(PanelId.BuildingUpgrade, _structureId))
                    FlowTrace.Step("Village", $"CastleNpc '{_label}' -> MVVM Building Upgrade (focus='{_structureId}').");
                else
                    FlowTrace.Warn("Village", $"Building Upgrade panel opener not registered for '{_structureId}' — NOT falling through to Yarn.");
                return;
            }

            FlowTrace.Step("Village", $"CastleNpc '{_label}' -> structure '{_structureId}' (StructureMenu gates shop/upgrade)");
            if (DialogueService.PlayStructure(_structureId, _label))
            {
                _openedStructure = true;
                Debug.Log($"[CastleNpcInteractable] {_label} -> structure dialogue '{_structureId}'.");
                return;
            }

            // WO-576: PlayStructure returned false — no conversation authored AND not a shoppable vendor
            // (the deleted-Yarn hole). NEVER dead-end the Talk: fall back to this building's upgrade panel
            // if it has one (mirrors BuildingInteractable's TryPanelFor fallback), else self-report. This
            // is the safety net behind the flavor-dialogue fix, so a future content gap can't silently no-op.
            if (IsUpgradableId(_structureId) && PanelRouter.Open(PanelId.BuildingUpgrade, _structureId))
            {
                FlowTrace.Step("Village", $"CastleNpc '{_label}' -> Building Upgrade (no conversation/shop fallback, focus='{_structureId}').");
                return;
            }
            FlowTrace.Warn("Village", $"CastleNpc '{_label}' id='{_structureId}': Talk had no conversation, shop, or upgrade panel — nothing to open.");
        }

        // Upgradable = city tiers (BuildingTierCatalog) OR legacy resource buildings — same test
        // BuildingInteractable uses, so the building's own interact and its NPC agree.
        private static bool IsUpgradableId(string id) =>
            !string.IsNullOrEmpty(id) &&
            (DeNelle.Core.State.BuildingTierCatalog.IsUpgradable(id) ||
             Buildings.Progression.ResourceBuildingProgression.IsResourceBuilding(id));

        // Shoppable = the catalog entry opts in (buildings.json isShoppable). A shoppable vendor's
        // Talk press opens the Buy/Sell/Leave dialogue (StructureMenu), never the upgrade panel —
        // upgrade is reached through the dedicated HUD context/Upgrade button (owner 2026-06-21).
        private static bool IsShoppableId(string id) =>
            !string.IsNullOrEmpty(id) &&
            BuildingCatalog.Find(id)?.IsShoppable == true;

        // A building whose Talk opens a NON-shop interactive FUNCTION (e.g. the barracks drillmaster's troop
        // TRAINING menu). Like a shoppable vendor, its Talk press opens that function and its upgrade is
        // reached via the HUD context/Upgrade button — NEVER by stealing the Talk press (owner 2026-06-21:
        // "match what we use everywhere else"). Extend this set as other "has a menu" buildings appear.
        // Ticket #11: barracks became upgradable, which (without this) routed its NPC to the upgrade panel
        // and made the troop-TRAINING flow unreachable through the drillmaster — this restores the vendor pattern.
        private static readonly System.Collections.Generic.HashSet<string> TalkFunctionIds =
            new System.Collections.Generic.HashSet<string> { "barracks" };
        public static bool HasTalkFunctionId(string id) =>
            !string.IsNullOrEmpty(id) && TalkFunctionIds.Contains(id);

        // True if a CONVERSATION is authored for this structure in dialogues.json (WO-576). A
        // resource/upgrade-only NPC with a flavor line (farm/lumbermill/arcane-tower) is a Talk
        // target FIRST — its upgrade rides the HUD context/Upgrade button (HudBuildingFocus),
        // never the Talk press. Without this, the upgrade short-circuit stole the Talk and the
        // farmer/woodcutter/arcanist "Talk" went nowhere (the deleted Yarn StructureMenu's hole).
        private static bool HasConversation(string id) =>
            DeNelle.Core.Dialogue.DialogueCatalog.Find(id) != null;

        // SHARED routing decision — the SINGLE source of truth for Interact()'s branch AND the headless
        // oracle (AssertVendorTalkRoute), so the test can never drift from the real route. PURE, no side
        // effects: a structure with an authored CONVERSATION, a SHOPPABLE vendor, or a TALK-FUNCTION
        // building opens the Talk dialogue (its primary function); the upgrade panel is reached ONLY when
        // upgrade is the building's ONLY function (upgradable, NOT shoppable, no conversation, no talk
        // function). Upgrade for the talk-first ones is the HUD context button (owner 2026-06-21).
        // Verifiable headless WITHOUT rendering.
        public static string ResolveRoute(string structureId) =>
            (IsUpgradableId(structureId) && !IsShoppableId(structureId)
                && !HasTalkFunctionId(structureId) && !HasConversation(structureId))
                ? "upgrade-panel" : "talk-dialogue";

        private void ResolveHero()
        {
            var tagged = GameObject.FindWithTag("Player");
            if (tagged != null) { _hero = tagged.transform; return; }
            var loco = FindAnyObjectByType<HeroLocomotion>();
            if (loco != null) _hero = loco.transform;
        }

        private void OnDisable()
        {
            MobileInteractButton.Release(this);
            TalkPromptRegistry.Deregister(transform);
            if (!string.IsNullOrEmpty(_structureId)) HudBuildingFocus.Clear(_structureId);
        }
    }
}
