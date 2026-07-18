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
        // WO-608 merge: MainCastle_Hall + OuterWorld collapse into Main_Castle_Overworld.
        // Castle-hub chrome must fire on the merged scene too, while staying castle-only
        // (never Village2/raids). Mirrors CastleBeamHider / CastleSpawnPointInjector.
        private const string MergedTargetScene = "Main_Castle_Overworld";
        private static bool IsCastleHubScene(string n) => n == TargetScene || n == MergedTargetScene;

        // (WO-682: the baked "NPC_<Role>_Interactable" marker constants were deleted with
        // the flag-off marker loop — vendors anchor to the Building collection instead.)

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
                    // the anchor pass (AnchorRoles). structureId "apothecary" has
                    // an authored conversation (dialogues.json) ending in OpenAlchemy — the same
                    // ConsumableCrafting panel the station's own interact opens.
                    return new Vendor { BodyRes = BodyPeasantA, StructureId = "apothecary",   Label = "Apothecary", Arch = TownsfolkDialogue.Archetype.Villager };
                case "jewelersbench":
                    // Owner 2026-07-03 ("every building in town needs an NPC as the speaker; jeweler
                    // lacks that"): the Jeweler's Bench is a RUNTIME station (JewelerStationInjector,
                    // BuildingType.JewelersBench, id "jewelers-bench") with no baked marker, so it's
                    // spawned by the anchor pass like the apothecary. structureId MUST match the
                    // station's Building id ("jewelers-bench") so MarkNpcCovered defers the bench's own
                    // prompt to this NPC, and so PlayStructure finds the authored "jewelers-bench"
                    // conversation (Sable) that ends in OpenJeweler -> the SAME JewelerCrafting panel
                    // the station's BuildingInteractable opens. Mirrors the Apothecary/Herbalist wiring.
                    return new Vendor { BodyRes = BodyMerchant, StructureId = "jewelers-bench", Label = "Jeweler", Arch = TownsfolkDialogue.Archetype.Quartermaster };
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

        // Holder so re-injection (idempotent) is trivial: clear the prior holder, respawn.
        private const string HolderName = "CastleVendorNPCs (runtime)";

        // The body SpawnVendor/SpawnPlaceholder most recently produced — read by the
        // placement hook to tag the building's VendorSeatMarker (per-building idempotency).
        private GameObject _lastSpawnedVendor;

        private void Inject()
        {
            // Idempotent: nuke any prior runtime holder so a re-load doesn't double-spawn,
            // and stop any in-flight anchor poll from the previous load so a stale
            // coroutine can never race the fresh pass below.
            var prior = GameObject.Find(HolderName);
            if (prior != null) Destroy(prior);
            StopCoroutine(nameof(AnchorVendorsToPlacedBuildings));

            new GameObject(HolderName);

            // WO-673 L4 (always on — WO-682 removed ff.strategicplacement): the baked
            // storefronts (and their NPC_<Role>_Interactable markers) are stood down —
            // buildings exist only where the PLAYER placed them (plus the row-less
            // injector stations). Vendors anchor to the live Building COLLECTION by id
            // (One Model: readers query the collection, never a baked name). The old
            // marker loop + the apothecary/jeweler deferred passes were deleted with the
            // flag — AnchorVendorsToPlacedBuildings covers every role, stations included.
            FlowTrace.Step("Vendor",
                "anchoring vendors to the Building collection (strategic placement always on).");
            StartCoroutine(nameof(AnchorVendorsToPlacedBuildings));
        }

        // (WO-682: the flag-off marker loop + the apothecary/jeweler deferred passes were
        // deleted with ff.strategicplacement — AnchorVendorsToPlacedBuildings below is the
        // ONE vendor-spawn path; its AnchorRoles table covers the stations too.)

        // ── WO-673 L4 (always on — WO-682) — vendor anchoring by Building collection ──
        // Role (VendorFor key) -> the Building.BuildingId that anchors it. Under strategic
        // placement the baked storefronts are stood down, so each vendor waits for a LIVE
        // Building carrying its id — placed by the player (StructureFactory "GameplayBuilding"
        // sets BuildingId == catalog id), replayed from a migrated save, or (for the two
        // runtime stations) injected by their station injector. Ids verified against
        // structures-catalog.json (:483-773) + CraftingStationInjector/JewelerStationInjector
        // StationId constants.
        private static readonly (string Role, string BuildingId)[] AnchorRoles =
        {
            ("Blacksmith",    "armorer"),        // no placed armorer catalog row yet (L1) — awaits one
            ("Lumbermill",    "collector_lumbermill"), // WO-707: Sawmill retires from the palette — anchor to the surviving Lumbermill tile; dialogue structureId stays "lumbermill" (VendorFor)
            ("Windmill",      "collector_farm"),       // WO-707: Mill retires from the palette — anchor to the Farm tile; dialogue structureId stays "farm" (VendorFor)
            ("EchoHollow",    "pet-house"),
            ("Forge",         "forge"),
            ("ArcaneTower",   "arcane-tower"),
            ("Jeweler",       "jeweler"),
            ("Marketplace",   "market"),
            ("Apothecary",    "apothecary"),     // runtime station (CraftingStationInjector.StationId)
            ("JewelersBench", "jewelers-bench"), // runtime station (JewelerStationInjector.StationId)
        };

        // Generalized deferred pass (the apothecary/jeweler pattern above, for EVERY role):
        // poll the live Building collection on a slow tick; when a role's building exists,
        // spawn its vendor through the SAME SpawnVendor path via a synthetic marker child at
        // the baked front offset (local (0,0,6)), so placement/interaction/sign composition
        // is identical to the baked storefronts. A role whose building doesn't exist yet
        // simply isn't spawned — the poll keeps watching (no timeout: placement is a
        // player-paced event) so the vendor appears the moment the building does.
        private IEnumerator AnchorVendorsToPlacedBuildings()
        {
            const float PollSeconds = 2f;   // slow tick — placement happens on player time
            var pending = new System.Collections.Generic.HashSet<string>();
            foreach (var a in AnchorRoles) pending.Add(a.Role);

            while (pending.Count > 0)
            {
                if (!IsCastleHubScene(SceneManager.GetActiveScene().name)) yield break; // scene moved on
                var holder = GameObject.Find(HolderName);
                if (holder == null) yield break;   // injector re-ran / tearing down

                var live = FindObjectsByType<Building>();
                foreach (var (role, buildingId) in AnchorRoles)
                {
                    if (!pending.Contains(role)) continue;

                    // Already placed (a prior pass / re-load survivor)? Settle the role.
                    if (GameObject.Find($"CastleVendor_{role}") != null ||
                        GameObject.Find($"CastleVendor_{role}_Placeholder") != null)
                    {
                        pending.Remove(role);
                        continue;
                    }

                    Building anchor = null;
                    foreach (var b in live)
                        if (b != null && b.IsAlive && b.BuildingId == buildingId) { anchor = b; break; }

                    if (anchor == null)
                    {
                        // Skip decision — Once per id so the poll never spams the trace.
                        FlowTrace.Once("Vendor", $"await-{buildingId}",
                            $"{role} awaiting building '{buildingId}' — vendor not spawned.");
                        continue;
                    }

                    // Synthetic marker at the baked marker's front offset (local (0,0,6)) —
                    // SpawnVendor derives the front DISTANCE from it and redirects the vendor
                    // to the building's Heart-facing side, exactly like the baked markers and
                    // the station deferred passes above.
                    var marker = new GameObject($"NPC_{role}_Marker (runtime)");
                    marker.transform.SetParent(anchor.transform, false);
                    marker.transform.localPosition = new Vector3(0f, 0f, 6f);

                    bool ok = SpawnVendor(marker.transform, role, ResolveHero(), holder.transform);
                    Destroy(marker);
                    if (ok)
                    {
                        pending.Remove(role);
                        FlowTrace.Step("Vendor",
                            $"{role} anchored to placed '{buildingId}' @ {anchor.transform.position}");
                    }
                    else
                    {
                        // SpawnVendor self-reported the failure — keep the role pending so the
                        // next tick retries (e.g. Resources not yet loaded).
                        FlowTrace.Fail("Vendor",
                            $"{role} spawn FAILED at placed '{buildingId}' — will retry next poll.");
                    }
                }

                if (pending.Count == 0) break;
                yield return new WaitForSecondsRealtime(PollSeconds);
            }
            FlowTrace.Step("Vendor", "vendor anchor poll complete — every role has its NPC.");
        }

        // ── PLACEMENT HOOK (owner device felt-test 2026-07-16, SHOW-STOPPER) ──────────
        // A storefront the player PLACES in build mode must get its vendor NPC RIGHT NOW,
        // not up to 2s later (the poll tick) and not never (the AnchorRoles poll misses the
        // WO-707 palette ids workshop/lumberyard/foundry/silo/collector_*, and settles each
        // role ONCE). BuildModeController.Place calls this the instant a placement commits;
        // it reuses the SAME SpawnVendor path the scene-load poll uses — NO parallel NPC
        // system, one vendor per trade.
        /// <summary>Spawn the vendor NPC for a just-placed building, immediately. Idempotent
        /// (skips if that role's vendor already exists — the poll or an earlier placement may
        /// have beaten us). No-op until the injector's Awake ran (Instance null) and outside a
        /// castle-hub scene. Reuses <see cref="SpawnVendor"/>, so a missing prefab still logs
        /// + falls back to a placeholder rather than silently leaving the storefront vendorless.</summary>
        public static void NotifyBuildingPlaced(string buildingId, Transform buildingTransform)
        {
            if (Instance == null || buildingTransform == null || string.IsNullOrEmpty(buildingId))
            {
                // SHOW-STOPPER RCA (owner "still no NPC", 2026-07-16): this guard was FULLY SILENT.
                // A placement that fired the notify before the injector bootstrapped (Instance null)
                // produced ZERO trace, reading to the owner as "no NPC" with an EMPTY errors-only
                // break-log. Fail-loud (lands in break-log) so the next capture names the dead
                // precondition instead of vanishing. Instance is normally set by the AfterSceneLoad
                // Bootstrap, so a null here means the placement beat the injector's Awake.
                FlowTrace.Fail("NpcSeat",
                    $"NotifyBuildingPlaced NO-OP: injectorReady={(Instance != null)}, hasTransform={(buildingTransform != null)}, " +
                    $"id='{buildingId ?? "<null>"}' — vendor NPC NOT spawned for the placed building.");
                return;
            }
            Instance.SpawnVendorForPlaced(buildingId, buildingTransform);
        }

        private void SpawnVendorForPlaced(string buildingId, Transform buildingTransform)
        {
            using var _ = FlowTrace.Enter("NpcSeat", $"CastleVendorNpcInjector.SpawnVendorForPlaced id='{buildingId}'");
            string activeScene = SceneManager.GetActiveScene().name;
            if (!IsCastleHubScene(activeScene))
            {
                // Build mode only runs in a buildable (castle-hub) scene, so a placement whose ACTIVE
                // scene isn't a hub is anomalous — Warn (lands in the errors-only break-log) naming the
                // scene, so a scene-name drift (e.g. OuterWorld active instead of Main_Castle_Overworld)
                // is captured instead of silently dropping every placed vendor.
                FlowTrace.Warn("NpcSeat",
                    $"placed '{buildingId}' — active scene '{activeScene}' is NOT a castle-hub scene " +
                    $"(expected '{TargetScene}' or '{MergedTargetScene}') — no vendor spawned. If the owner IS in the " +
                    "hub, the hub scene name drifted; add it to IsCastleHubScene.");
                return;
            }
            string role = RoleForBuildingId(buildingId);
            if (string.IsNullOrEmpty(role))
            {
                // Tower / wall / gate / mine / fountain / decoration are storefront-less BY DESIGN
                // (quiet Step). But an UNRECOGNIZED placeable id (a real building the mapping forgot)
                // is the "no NPC" bug class — Warn it (captured) so the missing id names itself.
                if (IsKnownNonStorefront(buildingId))
                    FlowTrace.Step("NpcSeat", $"placed '{buildingId}' is a non-storefront (tower/wall/gate/deco) — no NPC by design.");
                else
                    FlowTrace.Warn("NpcSeat",
                        $"placed '{buildingId}' maps to NO vendor role but is NOT a known non-storefront — " +
                        "UNMAPPED building id, so it gets no NPC. Add it to RoleForBuildingId/AnchorRoles.");
                return;
            }
            // PER-BUILDING idempotency (owner "every building in town needs an NPC as the speaker"):
            // the old check was per-ROLE-GLOBAL — it no-op'd whenever that trade already had a vendor
            // ANYWHERE (market/silo/foundry/lumberyard all map to "Marketplace"; forge/workshop ->
            // "Forge"), so a freshly-placed second building of a shared trade got NO NPC. Now we only
            // skip if THIS building already carries a live vendor (double-notify guard), so every
            // placed building gets its own speaker while a re-notify of the same building can't stack one.
            var seated = buildingTransform.GetComponent<VendorSeatMarker>();
            if (seated != null && seated.Vendor != null)
            {
                FlowTrace.Step("NpcSeat",
                    $"building '{buildingId}' already has its vendor ('{seated.Vendor.name}') — placement hook no-op.");
                return;
            }

            var holder = GameObject.Find(HolderName);
            if (holder == null) holder = new GameObject(HolderName);   // poll not up yet — make the parent

            // Synthetic marker at the baked front offset (local (0,0,6)) so SpawnVendor derives the
            // building's front DISTANCE + facing exactly like the poll and the baked storefronts.
            var marker = new GameObject($"NPC_{role}_Marker (placed)");
            marker.transform.SetParent(buildingTransform, false);
            marker.transform.localPosition = new Vector3(0f, 0f, 6f);

            _lastSpawnedVendor = null;
            bool ok = SpawnVendor(marker.transform, role, ResolveHero(), holder.transform);
            Destroy(marker);

            if (ok)
            {
                // Tag the building so a re-notify (double placement event) can't stack a second vendor,
                // and so the tag self-clears if the vendor is ever destroyed (Vendor -> Unity fake-null).
                if (seated == null) seated = buildingTransform.gameObject.AddComponent<VendorSeatMarker>();
                seated.Vendor = _lastSpawnedVendor;
                FlowTrace.Step("NpcSeat",
                    $"vendor NPC spawned for placed '{buildingId}' (role '{role}') at {buildingTransform.position}.");
            }
            else
                FlowTrace.Fail("NpcSeat",
                    $"vendor NPC spawn FAILED for placed '{buildingId}' (role '{role}').");
        }

        /// <summary>Reverse of <see cref="AnchorRoles"/> (buildingId -> vendor role), PLUS the
        /// WO-707 palette ids that carry no AnchorRoles entry but ARE storefronts the player
        /// places (workshop/mill/lumbermill/lumberyard/foundry/silo). Returns null for
        /// non-storefront ids (towers/walls/gates/deco) so placing those never spawns a
        /// spurious merchant.</summary>
        private static string RoleForBuildingId(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId)) return null;
            foreach (var (role, id) in AnchorRoles)
                if (string.Equals(id, buildingId, System.StringComparison.OrdinalIgnoreCase))
                    return role;
            // Placeable storefront ids WO-707 grooming left out of AnchorRoles — give each a
            // sensible vendor so every trade building gets an NPC to Talk/trade with.
            switch (buildingId.ToLowerInvariant())
            {
                case "workshop":        return "Forge";       // in-world label "Forge" (weapons)
                case "mill":            return "Windmill";
                case "lumbermill":      return "Lumbermill";
                case "collector_forge": return "Forge";       // structures-catalog id AnchorRoles missed -> was NPC-less
                case "lumberyard":
                case "foundry":
                case "silo":            return "Marketplace"; // storage container — generic merchant
                default:                return null;          // not a storefront -> no vendor
            }
        }

        /// <summary>True for the storefront-less placeable ids (towers/walls/gates/mines/fountains/
        /// decorations/repair) that legitimately get NO vendor — so <see cref="SpawnVendorForPlaced"/>
        /// stays quiet for those but WARNS (captures) on an UNRECOGNIZED building id it forgot to map.</summary>
        private static bool IsKnownNonStorefront(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId)) return true;
            string id = buildingId.ToLowerInvariant();
            return id.StartsWith("tower_") || id.StartsWith("wall_") || id.StartsWith("gate_") ||
                   id.StartsWith("mine_")  || id.StartsWith("deco_") || id.StartsWith("fountain_") ||
                   id.StartsWith("repair_");
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

            // FRONT-OF-BUILDING PLACEMENT (owner 2026-07-13 "the agents should be at the
            // front of each building" — supersedes the 2026-06-21 Heart-facing rule for
            // player-placed structures): the vendor stands on the side the building FACES
            // (the door side = the placed root's forward, i.e. the yaw the player chose at
            // placement). Fallback: when the building root has no meaningful facing
            // (identity yaw baked shell), keep the old Heart-facing side so baked-era
            // saves look unchanged. Preserves the hand-baked front-offset DISTANCE.
            Vector3 buildingPos = marker.parent != null ? marker.parent.position : marker.position;
            Vector3 flatBuild = new Vector3(buildingPos.x, 0f, buildingPos.z);
            float frontDist = Vector3.Distance(flatBuild, new Vector3(marker.position.x, 0f, marker.position.z));
            if (frontDist < 1f) frontDist = 5f;   // marker sits at the building -> default front distance
            Vector3 center = HeartCenter();
            Vector3 toHeart = new Vector3(center.x - buildingPos.x, 0f, center.z - buildingPos.z);
            toHeart = toHeart.sqrMagnitude < 0.01f ? Vector3.forward : toHeart.normalized;
            Vector3 front = toHeart;   // fallback: the Heart-facing side
            if (marker.parent != null)
            {
                Vector3 fwd = marker.parent.forward; fwd.y = 0f;
                if (fwd.sqrMagnitude > 0.01f) front = fwd.normalized;
            }
            Vector3 pos = flatBuild + front * frontDist;
            // WO-703 / BLANK-1: constrain the spawn sample to the GROUND RING — the 3D
            // sample near a wall-adjacent building can resolve to the elevated wall-walk
            // navmesh (the "NPC on top of the gatehouse" symptom). Accept the hit only
            // inside the ground band around the scripted flat ground y=0 (mirrors
            // CastleTownsfolkInjector / NpcGroundSeat bands); out-of-band -> keep the
            // computed courtyard position instead.
            if (NavMesh.SamplePosition(pos, out var hit, 4f, NavMesh.AllAreas))
            {
                if (hit.position.y >= -0.35f && hit.position.y <= 0.75f)
                    pos = hit.position;
                else
                    FlowTrace.Step("Village",
                        $"vendor '{role}' spawn sample rejected: navmesh hit y={hit.position.y:F2} outside " +
                        "ground band [-0.35..0.75] (wall-top/elevated mesh) — using courtyard position.");
            }
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
            _lastSpawnedVendor = go;
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
            _lastSpawnedVendor = go;
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

            // Ticket F8-14 (owner 2026-07-08: "when the other NPCs leave we should hide the
            // vendor ones, then show after"): vendors are wander=false so AmbientNPC's flee
            // state machine deliberately skips them — this watcher hides the body + kills the
            // Talk registration on the SAME combat signal the townsfolk flee on, restores after.
            if (body.GetComponent<CastleVendorWaveHider>() == null)
                body.AddComponent<CastleVendorWaveHider>();
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
    // VendorSeatMarker — a lightweight tag added to a PLACED building once its vendor
    // NPC exists, so the placement hook is idempotent PER BUILDING (never stacks a
    // second vendor on a double placement event) without falling back to the old
    // per-role-global check that starved same-trade buildings of their own speaker.
    // Vendor is a UnityEngine.Object reference: if the NPC is ever destroyed it becomes
    // fake-null, so the building naturally re-qualifies for a fresh vendor.
    // =========================================================================
    [DisallowMultipleComponent]
    public sealed class VendorSeatMarker : MonoBehaviour
    {
        public GameObject Vendor;
    }

    // =========================================================================
    // CastleVendorWaveHider — ticket F8-14: vendor NPCs duck OUT OF SIGHT for the
    // duration of a wave/battle, exactly like the fleeing townsfolk, then reappear
    // at their storefronts when the fight ends. Vendors are configured wander=false
    // so AmbientNPC's flee state machine deliberately skips them — this watcher
    // REUSES the SAME combat authority (AmbientNPC.IsCombatActive: wave
    // Countdown/Active OR BattleLock, shared 0.25s poll) instead of inventing a
    // second signal.
    //
    // Hide = renderers off (body + floating sign) + CastleNpcInteractable disabled —
    // its OnDisable releases the MobileInteractButton, deregisters from
    // TalkPromptRegistry (so the HUD Talk light dies) and clears HudBuildingFocus.
    // The GameObject stays ACTIVE so this watcher keeps polling for the all-clear
    // (mirrors AmbientNPC.SetBodyVisible's hide-without-deactivate rule).
    // =========================================================================
    /// <summary>Hides a castle vendor NPC while combat is live (ticket F8-14) and
    /// restores it after — same signal the townsfolk flee-to-shelter system uses.</summary>
    [DisallowMultipleComponent]
    public sealed class CastleVendorWaveHider : MonoBehaviour
    {
        // Only the renderers WE disabled get restored — a renderer something else
        // deliberately disabled (e.g. an injector fallback) stays off.
        private readonly System.Collections.Generic.List<Renderer> _hiddenRenderers =
            new System.Collections.Generic.List<Renderer>();
        private CastleNpcInteractable _interact;
        private bool _hidden;
        private bool _counted;

        // Observability: one Step per global combat transition, carrying the counts.
        private static int s_registered;
        private static int s_hiddenCount;
        private static bool s_lastCombat;

        private void Start()
        {
            _interact = GetComponent<CastleNpcInteractable>();
            s_registered++;
            _counted = true;
        }

        private void OnDestroy()
        {
            if (_counted && s_registered > 0) s_registered--;
            if (_hidden && s_hiddenCount > 0) s_hiddenCount--;
        }

        private void Update()
        {
            bool combat = AmbientNPC.IsCombatActive;

            // One announcement per global transition (first hider to notice logs it),
            // instrumented per the ticket: "vendors hidden (wave)" / "vendors restored".
            if (combat != s_lastCombat)
            {
                s_lastCombat = combat;
                FlowTrace.Step("Village", combat
                    ? $"vendors hidden (wave): {s_registered} vendor NPC(s) duck out of sight"
                    : $"vendors restored: {s_registered} vendor NPC(s) back at their storefronts " +
                      $"(were hidden={s_hiddenCount})");
            }

            if (combat && !_hidden) Hide();
            else if (!combat && _hidden) Show();
        }

        private void Hide()
        {
            _hidden = true;
            s_hiddenCount++;
            _hiddenRenderers.Clear();
            // Fetch fresh (not cached at Start): the InteractableSign / bubble children
            // may be built after this component attaches.
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (r != null && r.enabled) { r.enabled = false; _hiddenRenderers.Add(r); }
            // OnDisable releases the interact button, the TalkPromptRegistry entry
            // (HUD Talk light) and HudBuildingFocus — and its Update never re-registers
            // while disabled, so talk-shopping is unreachable for the whole wave.
            if (_interact != null) _interact.enabled = false;
        }

        private void Show()
        {
            _hidden = false;
            if (s_hiddenCount > 0) s_hiddenCount--;
            foreach (var r in _hiddenRenderers)
                if (r != null) r.enabled = true;
            _hiddenRenderers.Clear();
            if (_interact != null) _interact.enabled = true;
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
        // BuildingInteractable uses, so the building's own interact and its NPC agree. A collector's
        // catalog id ("collector_lumbermill"/"collector_farm") is first resolved to its bare
        // upgrade-keyed id ("lumbermill"/"farm") so a collector NPC's upgrade fallback fires
        // (ResolveUpgradeId is a pass-through for every non-collector id).
        private static bool IsUpgradableId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            string upg = DeNelle.Core.Catalog.CatalogRegistry.ResolveUpgradeId(id);
            return DeNelle.Core.State.BuildingTierCatalog.IsUpgradable(upg) ||
                   Buildings.Progression.ResourceBuildingProgression.IsResourceBuilding(upg);
        }

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
