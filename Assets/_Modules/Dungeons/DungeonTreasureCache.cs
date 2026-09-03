// =============================================================================
// DungeonTreasureCache (WO-850) - the reward at the bottom of a composed dungeon.
// -----------------------------------------------------------------------------
// OWNER REQUEST (2026-08-02): "can we add treasure at deepest, simple crafting
// supply", with two rulings taken on the spec:
//   LOOT        = a FIXED bundle of basic crafting materials (no RNG), PLUS a
//                 crafting-recipe unlock granted on the FIRST CLEAR only.
//   INTERACTION = prompt, then a small confirm/reward panel (not walk-in
//                 auto-claim) - the deepest room earns a beat.
//
// WHY FIXED, NOT A ROLL: DungeonLootGrant.GrantTable rolls LootTableCatalog, so
// the same chest would pay differently every run. The owner asked for a supply,
// not a slot machine - so this routes through DungeonLootGrant.GrantFixed (still
// the ONE granting seam; we do not call the larder directly).
//
// "DEEPEST" IS COMPUTED, NOT AUTHORED. No layout in the tree carries a depth or
// boss flag (dg_starter_loop is all "hub"/"combat"; archetype "boss" exists in
// rooms-catalog.json but no shipped layout uses it). So depth = BFS hop distance
// from the graph's entry room over the layout's connections[], which IS authored.
// Ties break on ordinal instanceId so the same layout always yields the same room
// - a random or "furthest cell" pick would make the chest un-regressable. (Note:
// furthest-EUCLIDEAN and furthest-BY-HOPS disagree on dg_starter_loop - turn2 vs
// turn3 - which is exactly why the rule is stated instead of assumed. The BFS
// answer there is turn3, at 8 hops.)
//
// PERSISTENCE: no schema bump. The first-clear one-shot rides GameState
// .SeenTutorials via GameStateService.MarkTutorialSeen, the established pattern
// (see TorchWardenDress.GrantTorchOnce). DungeonRuntimeState.OpenChest is
// deliberately NOT used for the one-shot: it is a runtime-only ScriptableObject
// that does not survive save/load, so it cannot carry "first clear ever".
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using DeNelle.Dungeons.RoomForge;
using DeNelle.Village;
using DeNelle.Village.Crafting;   // RecipeUnlocks - the first-clear teach record

namespace DeNelle.Dungeons
{
    /// <summary>
    /// Auto-injects one <see cref="DungeonTreasureCache"/> into the DEEPEST room of a
    /// composed dungeon scene. Mirrors DungeonExitInteractable's hook + idempotency shape.
    /// </summary>
    internal static class DungeonTreasureSpawner
    {
        private const string Sys = "DungeonTreasure";
        private static bool s_hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (s_hooked) return;
            s_hooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            // A build that boots straight into a composed dungeon already loaded its
            // scene before this hook - process the active scene once, too.
            TryInject(SceneManager.GetActiveScene());
            FlowTrace.Step(Sys, "installed sceneLoaded hook (composed-dungeon treasure auto-inject)");
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryInject(scene);

        private static void TryInject(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            Guard.Try(Sys, $"inject treasure into '{scene.name}'", () =>
            {
                Transform composeRoot = FindComposeRoot(scene);
                if (composeRoot == null) return;   // not a composed dungeon - nothing to do

                // Idempotent: never a second cache (re-entry, or a future bake-time chest).
                if (UnityEngine.Object.FindAnyObjectByType<DungeonTreasureCache>() != null)
                {
                    FlowTrace.Step(Sys, $"treasure already present in '{scene.name}' - skip inject");
                    return;
                }

                string dungeonId = composeRoot.name.StartsWith(ComposeRootPrefix, StringComparison.Ordinal)
                    ? composeRoot.name.Substring(ComposeRootPrefix.Length)
                    : composeRoot.name;

                string roomId = ResolveDeepestRoomId(dungeonId);
                Transform room = roomId != null ? FindChild(composeRoot, roomId) : null;
                if (room == null)
                {
                    // No layout, unreadable layout, or the room is not in the baked scene.
                    // FAIL LOUD AND SKIP - never seat the reward at an arbitrary spot where
                    // the player would find it two metres from the entrance.
                    FlowTrace.Warn(Sys, $"no deepest room resolved for '{dungeonId}' " +
                        $"(roomId='{roomId ?? "<none>"}') - treasure NOT injected");
                    return;
                }

                var cache = DungeonTreasureCache.Spawn(room.position, dungeonId, roomId);
                var taggedHero = GameObject.FindGameObjectWithTag("Player");
                if (taggedHero != null) cache.SetHero(taggedHero.transform);

                FlowTrace.Step(Sys, $"injected TREASURE cache into '{scene.name}' at room '{roomId}' " +
                    $"{room.position} (hero={(taggedHero != null ? "tagged" : "unresolved")})");
            });
        }

        /// <summary>DungeonBaker's compose-scene root name prefix (shared with DungeonRoomBinder).</summary>
        private const string ComposeRootPrefix = "DungeonCompose_";

        private static Transform FindComposeRoot(Scene scene)
        {
            GameObject[] roots;
            try { roots = scene.GetRootGameObjects(); }
            catch (Exception ex)
            {
                FlowTrace.Warn(Sys, $"GetRootGameObjects failed for '{scene.name}': {ex.Message}");
                return null;
            }
            for (int i = 0; i < roots.Length; i++)
            {
                var go = roots[i];
                if (go != null && go.name.StartsWith(ComposeRootPrefix, StringComparison.Ordinal))
                    return go.transform;
            }
            return null;
        }

        // DungeonBaker names each baked room GameObject EXACTLY its layout instanceId
        // (falling back to the prefab stem) and parents it directly under the compose
        // root - so a name match on the direct children is the room lookup.
        private static Transform FindChild(Transform root, string childName)
        {
            foreach (Transform child in root)
            {
                if (child != null && string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
                    return child;
            }
            return null;
        }

        /// <summary>
        /// The deepest room's instanceId for <paramref name="dungeonId"/>, or null when the
        /// layout is missing/unreadable. Internal for the regression oracle.
        /// </summary>
        internal static string ResolveDeepestRoomId(string dungeonId)
        {
            var layout = DungeonTreasureCache.LoadLayout(dungeonId);
            if (layout == null) return null;
            return DungeonTreasureCache.DeepestRoomId(layout, ResolveEntryRoomId(layout));
        }

        /// <summary>
        /// The BFS source room for <paramref name="layout"/>. Prefers the canonical
        /// <see cref="DungeonTreasureCache.EntryRoomId"/> ("entry") - the id DungeonBaker
        /// itself looks up to seat the hero - and falls back to the FIRST authored room
        /// when a layout does not use that convention (d4_sunken_crypt_spine names its
        /// rooms by prefab; demo_branching_kit calls its entrance "start"). Without the
        /// fallback those layouts would silently get no treasure at all.
        /// </summary>
        internal static string ResolveEntryRoomId(DungeonComposeLayout layout)
        {
            if (layout == null || layout.rooms == null || layout.rooms.Count == 0)
                return DungeonTreasureCache.EntryRoomId;

            foreach (var r in layout.rooms)
            {
                if (r == null) continue;
                string id = string.IsNullOrEmpty(r.instanceId) ? r.prefab : r.instanceId;
                if (string.Equals(id, DungeonTreasureCache.EntryRoomId, StringComparison.Ordinal))
                    return DungeonTreasureCache.EntryRoomId;
            }

            foreach (var r in layout.rooms)
            {
                if (r == null) continue;
                string id = string.IsNullOrEmpty(r.instanceId) ? r.prefab : r.instanceId;
                if (string.IsNullOrEmpty(id)) continue;
                FlowTrace.Warn(Sys, $"layout '{layout.dungeonId}' has no '{DungeonTreasureCache.EntryRoomId}' " +
                    $"room - using the first authored room '{id}' as the BFS source");
                return id;
            }
            return DungeonTreasureCache.EntryRoomId;
        }
    }

    /// <summary>
    /// The in-world treasure cache: a lit chest in the dungeon's deepest room that
    /// prompts on approach and pays a FIXED crafting-material bundle (plus a
    /// first-clear-only recipe unlock) through a small confirm panel.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonTreasureCache : MonoBehaviour
    {
        private const string Sys = "DungeonTreasure";

        // Matched to DungeonExitInteractable so both dungeon affordances feel identical:
        // the shared Interact button arms before the player is standing on the prop.
        private const float ActivateRadius = 4.5f;
        private const float CheckInterval = 0.15f;

        /// <summary>The room id every composed layout is EXPECTED to seat the hero in, and the
        /// BFS source. Matches the id DungeonBaker.PopulateForPlay looks up for the hero seat.
        /// Layouts that do not use it are handled by DungeonTreasureSpawner.ResolveEntryRoomId.</summary>
        internal const string EntryRoomId = "entry";

        /// <summary>Resources path of the dual-copy compose layouts (mirrors DungeonRoomBinder).</summary>
        private const string LayoutsResourcePath = "Data/Canonical/dungeon-layouts/";

        /// <summary>
        /// THE ONE TUNING POINT for the cache payout. Authored, fixed, and pinned by
        /// DungeonTreasureRegression so a silent edit fails the gate.
        ///
        /// Every id here MUST be a row in materials.json (both dual copies) - that is the
        /// larder's catalog, and an id outside it deposits a key with no display name and
        /// no icon.
        ///
        /// THE CACHE PAYS THE TORCH'S OWN MATS. Owner ruling 2026-08-02: dry-reed /
        /// oil-soaked-cloth / ember-resin were PROMOTED into materials.json (both copies,
        /// version 3) precisely so the larder can hold them and this cache can pay the
        /// recipe it unlocks below. Two of each = TWO torches. (They have no ItemIcons art
        /// yet, so they ride the glyph fallback like the other icon-less rows.)
        ///
        /// The potion pair stays so the cache still reads as a general crafting supply:
        /// ing_moonbloom x2 + ing_spring_water x1 is exactly "Brew Mending Salve"
        /// (consumable-recipes.json, craft-cons_mending_salve) - one salve.
        /// </summary>
        internal static readonly (string Id, int Count)[] FixedBundle =
        {
            ("dry-reed", 2),
            ("oil-soaked-cloth", 2),
            ("ember-resin", 2),
            ("ing_moonbloom", 2),
            ("ing_spring_water", 1),
        };

        /// <summary>
        /// Persisted one-shot key for the FIRST-CLEAR recipe grant. Namespaced per dungeon so
        /// each dungeon's deepest cache teaches once. Rides SeenTutorials - NO schema bump.
        /// </summary>
        internal static string FirstClearKey(string dungeonId) => "dun_cache_firstclear:" + (dungeonId ?? "unknown");

        private Transform _hero;
        private bool _heroFound;
        private bool _isInRange;
        private bool _opened;          // per-run guard: the cache pays ONCE per dungeon run
        private float _nextProximityCheck;
        private string _dungeonId = string.Empty;
        private string _roomId = string.Empty;
        private DungeonTreasureBeacon _beacon;

        // =====================================================================
        //  WO-1347 - THE OWNER-TAGGED IDLE SHIMMER
        // ---------------------------------------------------------------------
        //  HER TAG, VERBATIM (Assets/Editor/VfxManualPicks.json):
        //      Treasure_Aura -> Lana Studio/Casual RPG VFX/Prefabs/Loot/Loot_iddle.prefab
        //      isLoop false, scale 1.0        her words: "treasure chest"
        //
        //  'iddle' is the PACK'S OWN TYPO for idle. That is the real filename and it is
        //  never corrected in a path string, or the load fails. Nothing here reads a
        //  prefab path at all - the key is mapped VERBATIM and VFXManager resolves it
        //  from the catalog. No prefab is picked, substituted or rescaled here (memory
        //  vfx-map-owner-tags-no-creative-pick), and Loot_iddle.prefab is NOT modified
        //  on disk: it is a shared pack asset and editing it would silently change
        //  every other user of it.
        //
        //  WHERE THIS CHEST LIVES, ANSWERED FROM CODE: this is a WORLD object built from
        //  primitives by BuildVisual below, in world space, in the deepest room of a
        //  composed dungeon. It is NOT a UI element, so a world-space particle composite
        //  seats correctly here (parented to this transform, scale untouched). The
        //  daily-login chest is a DIFFERENT system and a UI modal - see
        //  DeNelle.Village.Monetization.DailyChestController, which carries her separate
        //  DailyChestCollect_Aura tag.
        //
        //  LIFECYCLE, AND ITS ONE OWNER: this component. spawn -> IDLE (_opened false,
        //  shimmer on) -> Open -> panel -> Claim -> shimmer OFF, beacon extinguished,
        //  prop deactivated. A shimmer still playing over an emptied chest reads as a bug
        //  and invites a second tap, so the stop is asserted on the claim path AND on
        //  OnDisable/OnDestroy rather than trusted to one of them.
        //
        //  (!) THE isLoop CONFLICT, HONOURED RATHER THAN FIXED. Her tag says isLoop:false
        //  while 7 of the prefab's 10 ParticleSystems are authored looping (measured from
        //  the prefab YAML, longest system 4s). Read literally, false means the shimmer
        //  dies after ~4 seconds and the chest sits DARK while it is still unopened -
        //  the opposite of what an idle loot effect is for. Her flag is NOT edited (this
        //  file never writes VfxManualPicks.json). Instead the LIFETIME is driven by the
        //  chest's own unopened state, which is the right owner of that lifetime whichever
        //  way the flag reads:
        //      * catalog row is a ONE-SHOT (today) -> PlayKey returns null, and the burst
        //        is re-fired every RefireSeconds while the chest is unopened.
        //      * catalog row is a LOOP (if she retags) -> PlayKey returns a handle, it is
        //        held, and nothing re-fires. No code change needed either way.
        //  Reported to her in one sentence in the WO-1347 RESULT so she can retag in
        //  seconds if she prefers the loop.
        // =====================================================================

        /// <summary>Her key, verbatim. The catalog owns the key -> prefab mapping.</summary>
        private const string ShimmerKey = "Treasure_Aura";

        /// <summary>FlowTrace tag for the shimmer decisions (shares the cache's system tag).</summary>
        private const string ShimmerSys = "TreasureVfx";

        /// <summary>Metres above the chest origin to seat the shimmer - just over the lid
        /// (body 0.45 + lid 0.95 in BuildVisual), so it reads as coming OFF the chest.</summary>
        private const float ShimmerHeight = 1.0f;

        /// <summary>Seconds between one-shot re-fires. MEASURED, not chosen: the prefab's
        /// longest lengthInSec is 4, so this re-arms just before the previous burst ends and
        /// the shimmer is continuous with no double-density overlap.</summary>
        private const float RefireSeconds = 3.9f;

        private VFXHandle _shimmer;        // non-null only when the row resolved as a LOOP
        private bool _shimmerIsOneShot;    // true when the row resolved as a one-shot burst
        private bool _shimmerStarted;      // a spawn has been attempted at least once
        private float _nextShimmerRefire;

        /// <summary>Create the cache at <paramref name="position"/> and build its visual.</summary>
        public static DungeonTreasureCache Spawn(Vector3 position, string dungeonId, string roomId)
        {
            var go = new GameObject("DungeonTreasure (Cache)");
            go.transform.position = position;
            var cache = go.AddComponent<DungeonTreasureCache>();
            cache._dungeonId = dungeonId ?? string.Empty;
            cache._roomId = roomId ?? string.Empty;
            cache.BuildVisual();
            return cache;
        }

        /// <summary>Push the Player-tagged hero (canon sec.7) so proximity never depends on a
        /// HeroLocomotion lookup that excludes a disabled/neutralized component.</summary>
        public void SetHero(Transform hero)
        {
            _hero = hero;
            _heroFound = hero != null;
        }

        // =====================================================================
        //  DEEPEST-ROOM MATH (pure + static -> unit-testable by the oracle)
        // =====================================================================

        /// <summary>Reads the Resources dual-copy compose layout, or null when absent/unreadable.</summary>
        internal static DungeonComposeLayout LoadLayout(string dungeonId)
        {
            if (string.IsNullOrEmpty(dungeonId)) return null;
            DungeonComposeLayout layout = null;
            Guard.Try(Sys, $"load compose layout '{dungeonId}'", () =>
            {
                var asset = Resources.Load<TextAsset>(LayoutsResourcePath + dungeonId);
                if (asset == null || string.IsNullOrEmpty(asset.text))
                {
                    FlowTrace.Warn(Sys, $"no compose layout Resource for '{dungeonId}'");
                    return;
                }
                layout = JsonConvert.DeserializeObject<DungeonComposeLayout>(asset.text);
            });
            return layout;
        }

        /// <summary>
        /// PURE: the instanceId furthest from <paramref name="entryId"/> by BFS hop count over
        /// the layout's connections (treated as UNDIRECTED - a corridor is walkable both ways).
        /// Ties break on the LOWEST ordinal instanceId so the answer is stable for a given
        /// layout. Returns null when the layout is empty, the entry room is absent, or nothing
        /// is reachable beyond the entry.
        /// </summary>
        internal static string DeepestRoomId(DungeonComposeLayout layout, string entryId)
        {
            if (layout == null || layout.rooms == null || layout.rooms.Count == 0) return null;
            if (string.IsNullOrEmpty(entryId)) return null;

            // Room id set (instanceId, falling back to prefab exactly as the rest of the
            // dungeon code resolves names - see DungeonRoomBinder.LoadEncounters).
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in layout.rooms)
            {
                if (r == null) continue;
                string id = string.IsNullOrEmpty(r.instanceId) ? r.prefab : r.instanceId;
                if (!string.IsNullOrEmpty(id)) ids.Add(id);
            }
            if (!ids.Contains(entryId)) return null;

            // Undirected adjacency.
            var adj = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            void Link(string a, string b)
            {
                if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return;
                if (!ids.Contains(a) || !ids.Contains(b)) return;
                if (!adj.TryGetValue(a, out var list)) { list = new List<string>(); adj[a] = list; }
                if (!list.Contains(b)) list.Add(b);
            }
            if (layout.connections != null)
            {
                foreach (var c in layout.connections)
                {
                    if (c == null) continue;
                    Link(c.fromInstance, c.toInstance);
                    Link(c.toInstance, c.fromInstance);
                }
            }

            // BFS from entry. The queue is drained in non-decreasing depth order, so the
            // winner is simply "strictly deeper wins; equal depth keeps the lowest ordinal
            // id" - which is independent of dictionary/enumeration order.
            var dist = new Dictionary<string, int>(StringComparer.Ordinal) { [entryId] = 0 };
            var queue = new Queue<string>();
            queue.Enqueue(entryId);
            string best = null;
            int bestDist = 0;
            while (queue.Count > 0)
            {
                string cur = queue.Dequeue();
                int d = dist[cur];
                if (d > 0)
                {
                    if (best == null || d > bestDist) { best = cur; bestDist = d; }
                    else if (d == bestDist && string.CompareOrdinal(cur, best) < 0) best = cur;
                }
                if (!adj.TryGetValue(cur, out var neighbours)) continue;
                neighbours.Sort(StringComparer.Ordinal);
                foreach (var n in neighbours)
                {
                    if (dist.ContainsKey(n)) continue;
                    dist[n] = d + 1;
                    queue.Enqueue(n);
                }
            }

            // An unreachable-from-entry layout (no connections authored) leaves best null;
            // seating the reward ON the entry is worse than not seating it at all.
            if (best == null)
            {
                FlowTrace.Warn(Sys, "deepest-room BFS found no room beyond the entry - layout has no usable connections");
                return null;
            }
            return best;
        }

        // =====================================================================
        //  VISUAL
        // =====================================================================

        private void BuildVisual()
        {
            // No chest prefab exists under Resources/ (the KayKit chest.fbx is edit-time art
            // only), so the cache is built from primitives exactly like the exit beacon.
            var body = AddDecor(PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f),
                new Vector3(1.25f, 0.8f, 0.85f), new Color(0.42f, 0.29f, 0.13f));
            var lid = AddDecor(PrimitiveType.Cube, new Vector3(0f, 0.95f, 0f),
                new Vector3(1.3f, 0.22f, 0.9f), new Color(0.55f, 0.41f, 0.16f));
            var band = AddDecor(PrimitiveType.Cube, new Vector3(0f, 0.62f, 0f),
                new Vector3(1.34f, 0.16f, 0.94f), new Color(0.86f, 0.71f, 0.30f));
            if (body == null || lid == null || band == null)
                FlowTrace.Warn(Sys, "cache decor partially failed to build (cache still interactable)");

            var lightGo = new GameObject("Cache_Light");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.86f, 0.5f);
            light.range = 10f;
            light.intensity = 2.0f;

            _beacon = gameObject.AddComponent<DungeonTreasureBeacon>();
            _beacon.Bind(light);

            StartShimmer("cache built");
        }

        /// <summary>
        /// WO-1347: spawn her tagged idle shimmer over the UNOPENED chest. Idempotent, and a
        /// hard no-op once the chest is opened - the gate is the chest's own state, not a
        /// timer and not the build event.
        /// <para>Null-safe end to end: VFXManager.PlayKey no-ops (returns null) when the
        /// manager or the catalog row is not ready, so an un-regenerated catalog costs the
        /// shimmer and nothing else. That is exactly why the trace below states whether the
        /// prefab RESOLVED - a missing effect and a deliberately subtle one are otherwise
        /// indistinguishable (CLAUDE.md section 12).</para>
        /// </summary>
        private void StartShimmer(string why)
        {
            if (_opened) return;
            if (_shimmer != null && _shimmer.IsAlive) return;

            bool resolves = VFXManager.CanPlayKey(ShimmerKey);
            Vector3 at = transform.position + Vector3.up * ShimmerHeight;

            Guard.Try(ShimmerSys, "spawn treasure idle shimmer", () =>
            {
                // Her scale is 1.0 and it is passed as 0 = "use the catalog row's DefaultScale",
                // i.e. nothing here rescales her effect. Parented to this transform so it tracks
                // the chest; no tint is passed, so no hue is imposed on a colourblind-safe read.
                _shimmer = VFXManager.PlayKey(ShimmerKey, at, Quaternion.identity, transform);
            });

            _shimmerStarted = true;
            _shimmerIsOneShot = _shimmer == null;
            _nextShimmerRefire = Time.unscaledTime + RefireSeconds;

            FlowTrace.Step(ShimmerSys,
                $"idle shimmer '{ShimmerKey}' ({why}): prefabResolved={resolves} " +
                $"handle={(_shimmer != null ? "LOOP (held)" : "null -> ONE-SHOT (re-fired while unopened)")} " +
                $"space=WORLD pos={at} chest='{_dungeonId}'/'{_roomId}' opened={_opened}. " +
                (resolves
                    ? "Her isLoop:false makes this a burst; the chest's UNOPENED state owns the lifetime."
                    : "Key not in the runtime catalog yet - regenerate it (Defenders/VFX/Generate Hovl " +
                      "VFX Catalog) or the shimmer stays absent. Nothing else is affected."));
        }

        /// <summary>
        /// WO-1347: stop the shimmer. Called from the claim path AND from OnDisable/OnDestroy,
        /// because a shimmer outliving its chest is the exact failure the ticket names. Safe to
        /// call any number of times and on a chest that never had one.
        /// </summary>
        private void StopShimmer(string why)
        {
            bool had = _shimmerStarted;
            if (_shimmer != null)
            {
                _shimmer.Stop(immediate: true);
                _shimmer = null;
            }
            _shimmerStarted = false;
            _shimmerIsOneShot = false;
            if (had)
                FlowTrace.Step(ShimmerSys,
                    $"idle shimmer '{ShimmerKey}' STOPPED ({why}) - chest '{_dungeonId}'/'{_roomId}' " +
                    $"opened={_opened}. Nothing is left playing over an emptied chest.");
        }

        private GameObject AddDecor(PrimitiveType type, Vector3 localPos, Vector3 scale, Color color)
        {
            GameObject go = null;
            Guard.Try(Sys, "build cache decor", () =>
            {
                go = GameObject.CreatePrimitive(type);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = localPos;
                go.transform.localScale = scale;
                // Colliders stripped: the cache is prompt-driven, and a solid box in the
                // deepest room would block the player's path around the reward.
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                var rend = go.GetComponent<Renderer>();
                if (rend != null)
                {
                    var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                    if (sh != null) rend.material = new Material(sh) { color = color };
                    else rend.material.color = color;
                }
            });
            return go;
        }

        // =====================================================================
        //  PROMPT
        // =====================================================================

        // MobileInteractButton is a PER-FRAME claim: Request() sets a flag the button
        // clears every frame, so a prompt shown on the range-entry edge alone would blink
        // out immediately. Request every frame while in range - the exact shape
        // DungeonExitInteractable.Update uses. The proximity TEST stays on an interval.
        private void Update()
        {
            if (_opened) return;

            // WO-1347: her isLoop:false resolves as a BURST, so the idle shimmer is re-armed
            // from the chest's own unopened state rather than left to die after ~4s. When the
            // row resolves as a LOOP instead (a retag), _shimmerIsOneShot is false and this
            // never runs. Cheap: one float compare per frame in the common case.
            if (_shimmerIsOneShot && Time.unscaledTime >= _nextShimmerRefire)
            {
                _nextShimmerRefire = Time.unscaledTime + RefireSeconds;
                Guard.Try(ShimmerSys, "re-fire treasure idle shimmer", () =>
                    VFXManager.PlayKey(ShimmerKey, transform.position + Vector3.up * ShimmerHeight,
                                       Quaternion.identity, transform));
                FlowTrace.Throttle(ShimmerSys, "refire", 30f,
                    $"idle shimmer '{ShimmerKey}' re-fired (isLoop:false burst, chest still UNOPENED) " +
                    $"at {transform.position + Vector3.up * ShimmerHeight} space=WORLD.");
            }

            if (!_heroFound)
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) SetHero(tagged.transform);
                if (!_heroFound) return;
            }
            // The hero rig can be replaced (body-swap) after we first cached it - re-resolve
            // rather than dereferencing a destroyed Transform (DungeonPortal DEF-40 lesson).
            if (_hero == null) { _heroFound = false; return; }

            // Build/authoring mode only - MobileInteractButton.Suppressed is the build-mode
            // flag. The modal case needs no handling here: Request() itself bails while
            // PanelManager.AnyOpen, so the button stays hidden under our reward panel.
            if (MobileInteractButton.Suppressed) { ReleasePrompt(); return; }

            if (Time.unscaledTime >= _nextProximityCheck)
            {
                _nextProximityCheck = Time.unscaledTime + CheckInterval;
                Vector3 d = _hero.position - transform.position;
                d.y = 0f;
                _isInRange = d.sqrMagnitude <= ActivateRadius * ActivateRadius;
            }

            if (_isInRange) MobileInteractButton.Request(this, "Open the cache", Open);
            else MobileInteractButton.Release(this);
        }

        private void ReleasePrompt()
        {
            _isInRange = false;
            MobileInteractButton.Release(this);
        }

        private void OnDisable()
        {
            ReleasePrompt();
            // WO-1347: Claim deactivates this GameObject, so this is the belt to the claim
            // path's braces - and it also covers a scene teardown that never reaches Claim.
            StopShimmer("cache disabled");
        }

        private void OnDestroy() => StopShimmer("cache destroyed");

        // =====================================================================
        //  OPEN -> PANEL -> GRANT
        // =====================================================================

        private void Open()
        {
            if (_opened) return;
            _opened = true;
            ReleasePrompt();
            // WO-1347: OPENED is the state change the shimmer is gated on, so it stops HERE -
            // at the tap - not at the later grant. That also covers the case where her tag is
            // retagged to isLoop:true and a held loop would otherwise keep playing under the
            // reward panel.
            StopShimmer("cache opened");

            bool firstClear = !FirstClearTaken(_dungeonId);
            FlowTrace.Step(Sys, $"cache opened in '{_dungeonId}' room '{_roomId}' (firstClear={firstClear})");

            // The panel OWNS the grant: nothing is credited until the player taps Take, so a
            // dismissed panel can never silently eat the reward (the WO-844 potion lesson).
            // Show() returns false when it refused to open (duplicate / arbiter rejection) and
            // Guard.Try returns false when it THREW - either way the panel is not on screen
            // and never will be, so the cache pays directly rather than becoming a dead prop.
            bool shown = false;
            bool ran = Guard.Try(Sys, "build treasure reward panel",
                () => { shown = DungeonTreasurePanel.Show(FixedBundle, firstClear, () => Claim(firstClear)); });
            if (!ran || !shown)
            {
                FlowTrace.Warn(Sys, $"reward panel not shown (threw={!ran}) - granting directly " +
                    "so the cache is never a dead prop");
                Claim(firstClear);
            }
        }

        private void Claim(bool firstClear)
        {
            Guard.Try(Sys, "grant treasure cache", () =>
            {
                int granted = DungeonLootGrant.GrantFixed(FixedBundle, $"cache:{_dungeonId}");
                FlowTrace.Step(Sys, $"cache paid {granted} material(s) from the fixed bundle.");

                if (firstClear)
                {
                    // RecipeUnlocks.Unlock and MarkTutorialSeen each persist via Save(), which
                    // also flushes the inventory mutation above - the TorchWardenDress idiom.
                    // Two keys, two jobs: the recipe record is global ("the player knows the
                    // torch"), the first-clear key is per-dungeon ("this cache has taught").
                    RecipeUnlocks.UnlockFromDungeonCache(_dungeonId);
                    var svc = GameStateService.Instance;
                    if (svc != null) svc.MarkTutorialSeen(FirstClearKey(_dungeonId));
                    else FlowTrace.Warn(Sys, $"no GameStateService - first clear of '{_dungeonId}' NOT recorded");
                    FlowTrace.Step(Sys, $"FIRST CLEAR of '{_dungeonId}' - recipe unlock recorded.");
                }
            });

            // Retire the prop so the deepest room reads as looted. Extinguish first so the
            // point light is off even if something re-enables this GameObject later.
            //
            // WO-1347: the owner-tagged idle shimmer dies on the SAME beat and for the same
            // reason - "unopened" is the state that owns its lifetime, and this is the moment
            // that state ends. Asserted here as well as in OnDisable so the order can never
            // leave a shimmer over an emptied chest.
            StopShimmer("cache claimed");
            if (_beacon != null) _beacon.Extinguish();
            gameObject.SetActive(false);
        }

        private static bool FirstClearTaken(string dungeonId)
        {
            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            if (state == null || state.SeenTutorials == null) return false;
            return state.SeenTutorials.TryGetValue(FirstClearKey(dungeonId), out bool seen) && seen;
        }
    }

    /// <summary>Gentle pulse so the cache reads as "reward here" from across the room.</summary>
    public sealed class DungeonTreasureBeacon : MonoBehaviour
    {
        private const float PulseSpeed = 2.0f;
        private const float IntensityBase = 2.0f;
        private const float IntensityAmp = 0.5f;

        private Light _light;

        public void Bind(Light light) => _light = light;

        public void Extinguish()
        {
            if (_light != null) _light.enabled = false;
        }

        private void Update()
        {
            if (_light == null) return;
            _light.intensity = IntensityBase + Mathf.Sin(Time.unscaledTime * PulseSpeed) * IntensityAmp;
        }
    }
}
