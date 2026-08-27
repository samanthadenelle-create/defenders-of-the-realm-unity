// =============================================================================
// ManaRecipeScrollService -- WO-1235 "the tutorial entrance to crafting": the ONE
// authored scroll drop that teaches the Mana Draught recipe.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Modelled on CastleDefensePlansService (WO-1013), which the WO names as the
// proven pattern: self-bootstrapping (RuntimeInitializeOnLoadMethod, no scene
// authoring, no VillageSceneBuilder re-save), a cheap 1 s scan, and EVERY
// acceptance shape falling out of ONE pure rule decided from persisted state.
// The prop is scene-owned and deterministically re-spawned on every scene entry
// until collected; nothing about the prop itself is saved.
//
// [STOP] IT IS A COPY OF THE PATTERN, NOT A SECOND TENANT OF THAT SERVICE. WO-1235
// says so explicitly: "Copy the pattern; do not fold a second drop into it."
//
// =============================================================================
//  THE LOOP THIS BUYS (owner, 2026-08-26) -- read it before touching the rule
// -----------------------------------------------------------------------------
//   have -> use -> RUN OUT -> find the recipe -> craft more.
//
// The three founding Mana Draughts (StartingBudget.FoundingManaPotions) are NOT
// a gift, they are the SETUP: the player must FEEL the resource run out before a
// recipe means anything. Every decision below serves that ordering, and the
// owner ruled all three open questions on it:
//
//   1. TRIGGER -- the scroll becomes discoverable when Mana Potions fall to
//      NeedThreshold (0 or 1) for the FIRST time. Verbatim: "That creates the
//      exact 'I need this -> I discover the solution' teaching moment." So the
//      gate is CONSUMPTION, not waves and not tutorial completion.
//
//   2. SCOPE -- the scroll unlocks Crafting as a VISIBLE SYSTEM but grants ONLY
//      the Mana Potion recipe. Verbatim: "Introduces the mechanic without
//      vomiting the whole crafting catalog onto a new player." [STOP] Do NOT unlock
//      the recipe book. That is why RecipeUnlockKeys.GatedRecipeIds is an
//      allow-list of ONE and this service unlocks exactly one id.
//
//   3. LOCATION -- a HARD PRECONDITION, not a polish item. Verbatim: "Never
//      teach a verb the player cannot immediately perform." NO STATION, NO
//      SCROLL. See the next block, because this is the part that bites.
//
// =============================================================================
//  [WARN] THE STATION PRECONDITION, AND WHY THIS DROP MAY NOT APPEAR AT ALL YET
// -----------------------------------------------------------------------------
// StationReachable is an ACTUAL SCAN of the loaded scene for a live Building of
// BuildingType.ApothecaryWorkbench -- the one thing that opens
// PanelId.ConsumableCrafting. It calls ConsumableUseService.AlchemyBenchIsStanding
// rather than growing a second copy of that scan, because the empty-larder line
// and this gate must never be able to disagree about whether a brewer exists.
//
// [STOP] ON MOST SAVES TODAY THERE IS NO BREWER, AND THAT IS NOT A BUG IN THIS FILE.
// ConsumableUseService's own header (verified at source, 2026-08-26) records it:
// CraftingStationInjector stands down unconditionally on a strategic-placement
// save, structures-catalog.json has NO "apothecary" row so the palette can never
// build one, and no apothecary is baked into the hub scene. Net: "usually there
// is no brewer to send anyone to."
//
// So on such a save this service correctly withholds the scroll FOREVER, and
// SAYS SO on a throttled trace naming the precondition by name. That is the
// owner's ruling working exactly as written -- teaching a verb with no surface
// to perform it on is the failure she ruled out. It is ALSO a blocking content
// gap that only she can close, and the three candidate closures are:
//     (a) author an "apothecary" row in structures-catalog.json so it is
//         buildable (that file is owner/lead-scoped);
//     (b) lift the CraftingStationInjector standdown for the FTUE (a WO-703 /
//         BLANK-1 placement ruling, not a CLI call);
//     (c) re-point PanelId.ConsumableCrafting at the existing buildable
//         "workshop" row (displayName "Crafting Station"), which today opens the
//         GEAR workshop instead.
// [STOP] NONE of those is decided here. The gate is implemented honestly and the
// trace makes the gap impossible to miss; picking the closure is an owner call.
// =============================================================================

using DeNelle.Core.Diagnostics;
using DeNelle.Core.HUD;
using DeNelle.Core.State;
using DeNelle.Core.UI;
using DeNelle.Village.Crafting;
using DeNelle.Village.Items;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Installs the single persistent <see cref="ManaRecipeScrollService"/>.</summary>
    public static class ManaRecipeScrollBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (ManaRecipeScrollService.Instance != null) return;
            var go = new GameObject("ManaRecipeScrollService");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<ManaRecipeScrollService>();

            // WO-1105's lesson, applied from line one: PROVE the service installed. A run where
            // the scroll never spawned must not log byte-identically to one where it did.
            FlowTrace.Step("Progression",
                "mana-scroll service installed (DDOL, self-bootstrap; need threshold " +
                ManaRecipeScrollService.NeedThreshold + " mana potion(s)) -- WO-1235");
        }
    }

    /// <summary>
    /// Owns the WO-1235 recipe-scroll lifecycle: latches the "I have run out" moment,
    /// decides from persisted state whether the scroll should stand, spawns the prop, and
    /// emits the [Flow:Progression] funnel lines. The collect/teach half lives in
    /// <see cref="ManaRecipeScrollPickup"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManaRecipeScrollService : MonoBehaviour
    {
        /// <summary>
        /// The mana-potion count at or below which the player has "run out" (owner ruling #1:
        /// "falls to 0 or 1"). 1, not 0, deliberately: the teaching moment is the player
        /// realising the stack is ending, and offering the recipe at the LAST potion means they
        /// still have one in hand while they learn to make more.
        /// [STOP] The number is the CONSTANT. Never restate it in prose -- that is how
        /// CastleDefensePlansService's own header went stale once already.
        /// </summary>
        public const int NeedThreshold = 1;

        /// <summary>The SeenTutorials key latching "mana potions have hit the threshold at least
        /// once". Persisted so the moment survives a reload: a player who ran out, quit, and came
        /// back has still run out. Namespaced so it cannot collide with a real tutorial key.</summary>
        public const string NeedFeltKey = "mana_scroll_need_felt";

        /// <summary>The recipe this scroll teaches. One id, per owner ruling #2.</summary>
        public const string RecipeId = RecipeUnlockKeys.ManaPotionRecipeId;

        public static ManaRecipeScrollService Instance { get; private set; }

        private GameObject _prop;
        private float _nextScan;
        private const float ScanInterval = 1.0f;    // the CastleDefensePlansService cadence
        private const float HeartbeatSeconds = 5f;  // throttle for the not-spawning heartbeat

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // =====================================================================
        //  THE PURE RULES (pinned headless by ManaRecipeScrollRegression)
        // =====================================================================

        /// <summary>
        /// True when a mana-potion count means the player has felt the need. Pure, so the
        /// truth table is pinned without a play session.
        /// </summary>
        public static bool ShouldLatchNeed(int manaPotionCount) => manaPotionCount <= NeedThreshold;

        /// <summary>
        /// The ONE spawn rule. Spawn IFF the need has been felt AND a crafting station is
        /// actually reachable AND the recipe is not already taught AND no prop is standing.
        ///
        /// [KEY] stationReachable is the owner's HARD PRECONDITION (ruling #3) and it sits INSIDE
        /// the rule, not beside it, so no caller can route around it. It is also what makes
        /// ruling #2's "unlocks Crafting as a VISIBLE SYSTEM" true for free: the scroll cannot
        /// be reached unless the door already exists, so the player is never handed a key to a
        /// room that is not there.
        ///
        /// [KEY] taught closes the rule FOREVER once collected -- and it is also what protects
        /// every existing player, who was grandfathered as taught by SaveMigrator.MigrateToV40
        /// and therefore never sees a scroll for a recipe they already had.
        /// </summary>
        public static bool ShouldSpawnDrop(bool needFelt, bool stationReachable, bool taught, bool propAlive)
            => needFelt && stationReachable && !taught && !propAlive;

        // =====================================================================
        //  SCAN
        // =====================================================================

        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + ScanInterval;

            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            if (state == null)
            {
                FlowTrace.Throttle("Progression", "scroll-idle-nostate", HeartbeatSeconds,
                    "mana-scroll idle: no GameState yet (GameStateService" +
                    (svc == null ? ".Instance" : ".State") + " is null) -- still scanning");
                return;
            }

            bool taught = RecipeUnlocks.IsUnlocked(RecipeId);
            if (taught)
            {
                FlowTrace.Throttle("Progression", "scroll-idle-taught", HeartbeatSeconds,
                    $"mana-scroll idle: recipe '{RecipeId}' is already taught on this save " +
                    "(collected here, or grandfathered by the v40 migration because this player " +
                    "predates the recipe gate) -- the rule is closed forever");
                return;
            }

            // -- the need latch (owner ruling #1) ------------------------------------
            bool needFelt = NeedAlreadyFelt(state);
            if (!needFelt)
            {
                int have = ManaPotionCount();
                if (ShouldLatchNeed(have))
                {
                    needFelt = true;
                    svc.MarkTutorialSeen(NeedFeltKey);   // sets the key AND Save()s
                    FlowTrace.Step("Progression",
                        $"mana-scroll NEED LATCHED: mana potions fell to {have} (threshold {NeedThreshold}) " +
                        "for the first time. This is the WO-1235 teaching moment -- the recipe now means " +
                        "something because the player has personally felt it run out.");
                }
                else
                {
                    FlowTrace.Throttle("Progression", "scroll-idle-stocked", HeartbeatSeconds,
                        $"mana-scroll idle: {have} mana potion(s) in the larder (need <= {NeedThreshold}). " +
                        "The potions are the SETUP, not the reward -- the scroll waits for the player to " +
                        "run out (WO-1235 owner ruling 1).");
                    return;
                }
            }

            // -- the HARD station precondition (owner ruling #3) ----------------------
            bool stationReachable = StationReachable();
            bool propAlive = _prop != null;

            if (!ShouldSpawnDrop(needFelt, stationReachable, taught, propAlive))
            {
                if (!stationReachable)
                    FlowTrace.Throttle("Progression", "scroll-idle-nostation", HeartbeatSeconds,
                        "mana-scroll WITHHELD: the player has run out, but NO crafting station is standing " +
                        "in this scene (a live Building of BuildingType.ApothecaryWorkbench). Owner ruling " +
                        "WO-1235 #3 is a HARD precondition -- 'Never teach a verb the player cannot " +
                        "immediately perform' -- so no station means no scroll. [WARN] This is EXPECTED on most " +
                        "saves today and is a CONTENT gap, not a code fault: CraftingStationInjector stands " +
                        "down on strategic-placement saves, structures-catalog.json has no 'apothecary' row, " +
                        "and none is baked in the hub. See this file's header for the three closures, all of " +
                        "which are owner calls.");
                else
                    FlowTrace.Throttle("Progression", "scroll-idle-rule", HeartbeatSeconds,
                        $"mana-scroll not spawning: needFelt={needFelt} stationReachable={stationReachable} " +
                        $"taught={taught} propAlive={propAlive} -- ShouldSpawnDrop false");
                return;
            }

            Guard.Try("Progression", "spawn mana recipe scroll", SpawnDrop);
        }

        private static bool NeedAlreadyFelt(GameState state)
            => state.SeenTutorials != null
               && state.SeenTutorials.TryGetValue(NeedFeltKey, out bool felt) && felt;

        /// <summary>How many Mana Draughts the larder holds. HudCommands.ManaPotionId is the
        /// MANA id; HpPotionId is health and reusing it here would gate the mana lesson on the
        /// healing stack (WO-1235 warns about exactly that).</summary>
        private static int ManaPotionCount()
        {
            var inv = VillageInventory.Instance;
            return inv != null ? inv.Get(HudCommands.ManaPotionId) : 0;
        }

        /// <summary>
        /// The owner's ruling-#3 precondition: can this player perform the verb RIGHT NOW?
        ///
        /// Two clauses, both load-bearing:
        ///   - the bench must be standing -- delegated to the ONE existing owner of "is a brewer
        ///     standing" (ConsumableUseService) so the two answers can never diverge;
        ///   - the item-drop lane must be ON. ItemCraftingService.CanCraft and TryCraft both
        ///     refuse outright while ItemDropSystem.Enabled is false ("SHIPS DARK"), so teaching
        ///     the recipe with the lane dark would hand the player a recipe the craft button
        ///     silently rejects -- the exact failure "never teach a verb the player cannot
        ///     immediately perform" rules out, just one layer deeper than the building.
        /// </summary>
        private static bool StationReachable()
            => ItemDropSystem.Enabled && ConsumableUseService.AlchemyBenchIsStanding();

        // =====================================================================
        //  SPAWN -- primitives + glint, the CastleDefensePlansService grammar
        // =====================================================================

        private void SpawnDrop()
        {
            Vector3 seat = ResolveSeat(out string seatSource);

            _prop = new GameObject("ManaRecipeScroll_Drop");
            // Scene-owned on purpose (NOT under this DDOL service): the prop dies with the
            // scene and the scan deterministically re-spawns it from state.
            _prop.transform.position = seat;

            BuildVisual(_prop.transform);

            var sphere = _prop.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 1.6f;
            // Kinematic body so the trigger fires regardless of how the hero rig is composed
            // (CharacterController vs collider+rigidbody).
            var rb = _prop.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            _prop.AddComponent<ManaRecipeScrollPickup>();

            FlowTrace.Step("Progression",
                $"mana-scroll-spawned @ {seat} (seat={seatSource}) -- one authored drop, persists " +
                "until collected; WO-1235");
        }

        /// <summary>
        /// Seat the scroll AT THE CRAFTING STATION the precondition just proved exists. That is
        /// not decoration: the owner's shipped sequence ends "UI directs them to the
        /// ALREADY-ACCESSIBLE crafting station", and a scroll that lies on the bench IS that
        /// direction, with no chrome. Falls back to a near-Heart seat only if the bench vanished
        /// between the check and the spawn (a race the scan would re-try anyway).
        /// </summary>
        private static Vector3 ResolveSeat(out string source)
        {
            var buildings = FindObjectsByType<Building>(FindObjectsSortMode.None);
            if (buildings != null)
            {
                // Deterministic pick by ordinal NAME. FindObjectsByType is UNORDERED, and the
                // WO-1105 lesson is that an unbroken tie resolves on iteration order -- the same
                // save seating the drop somewhere different run to run.
                Building pick = null;
                for (int i = 0; i < buildings.Length; i++)
                {
                    var b = buildings[i];
                    if (b == null || !b.IsAlive || b.Type != BuildingType.ApothecaryWorkbench) continue;
                    if (pick == null || string.CompareOrdinal(b.name ?? "", pick.name ?? "") < 0) pick = b;
                }
                if (pick != null)
                {
                    source = "bench:" + pick.name;
                    return GroundSnap(pick.transform.position + pick.transform.forward * 1.8f);
                }
            }
            source = "fallback:heart-approach";
            return GroundSnap(new Vector3(0f, 0f, 10f));
        }

        private static Vector3 GroundSnap(Vector3 seat)
        {
            if (Physics.Raycast(seat + Vector3.up * 20f, Vector3.down, out var hit, 60f))
                return hit.point + Vector3.up * 0.15f;
            return seat + Vector3.up * 0.3f;
        }

        /// <summary>Neutral high-luminance pale gold, shared with the plans drop. NOT a semantic
        /// hue -- the owner is red/green colour-blind, so the prop reads by luminance and motion.</summary>
        private static readonly Color GlintTint = new Color(1f, 0.86f, 0.5f, 1f);

        private static void BuildVisual(Transform parent)
        {
            // A rolled scroll on a small stand: parchment cylinder + two ribbon bands.
            AddDecor(parent, PrimitiveType.Cylinder, new Vector3(0f, 0.42f, 0f),
                new Vector3(0.16f, 0.46f, 0.16f), new Color(0.94f, 0.90f, 0.76f),
                euler: new Vector3(0f, 0f, 90f));                                    // parchment roll
            AddDecor(parent, PrimitiveType.Cylinder, new Vector3(-0.24f, 0.42f, 0f),
                new Vector3(0.19f, 0.05f, 0.19f), new Color(0.55f, 0.30f, 0.62f),
                euler: new Vector3(0f, 0f, 90f));                                    // ribbon band
            AddDecor(parent, PrimitiveType.Cylinder, new Vector3(0.24f, 0.42f, 0f),
                new Vector3(0.19f, 0.05f, 0.19f), new Color(0.55f, 0.30f, 0.62f),
                euler: new Vector3(0f, 0f, 90f));                                    // ribbon band
            AddDecor(parent, PrimitiveType.Cube, new Vector3(0f, 0.14f, 0f),
                new Vector3(0.70f, 0.20f, 0.34f), new Color(0.40f, 0.28f, 0.15f));   // wooden stand

            var lightGo = new GameObject("Scroll_Glint");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = GlintTint;
            light.range = 14f;
            light.intensity = 2.4f;

            parent.gameObject.AddComponent<ManaScrollGlint>().Bind(light);
        }

        private static void AddDecor(Transform parent, PrimitiveType type, Vector3 localPos,
            Vector3 scale, Color color, Vector3 euler = default)
        {
            Guard.Try("Progression", "build scroll decor", () =>
            {
                var go = GameObject.CreatePrimitive(type);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = localPos;
                go.transform.localScale = scale;
                if (euler != Vector3.zero) go.transform.localEulerAngles = euler;
                // Strip the primitive's solid collider: the pickup's own sphere TRIGGER is the
                // one collider this prop owns (a solid box would snag paths at the bench).
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
        }
    }

    /// <summary>
    /// Walk-over collection of the recipe scroll. Mirrors the CastleDefensePlansPickup grammar:
    /// trigger sphere + GetComponentInParent&lt;HeroHealth&gt; hero check + one-shot _taken latch.
    ///
    /// MECHANICS vs PRESENTATION: <see cref="TryCollect"/> IS the mechanics (the persisted
    /// recipe unlock). The toast that points at the bench is presentation and can fail without
    /// costing the player the unlock.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManaRecipeScrollPickup : MonoBehaviour
    {
        private bool _taken;

        private void OnTriggerEnter(Collider other)
        {
            if (_taken) return;
            if (other == null || other.GetComponentInParent<HeroHealth>() == null) return;

            if (!TryCollect())
            {
                // Already taught (stale prop from a race) -- retire the prop quietly.
                if (RecipeUnlocks.IsUnlocked(ManaRecipeScrollService.RecipeId))
                {
                    _taken = true;
                    gameObject.SetActive(false);
                    Destroy(gameObject);
                }
                return;
            }

            _taken = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        /// <summary>
        /// The WO-1235 collection mechanics, callable headless (regression oracle): idempotence
        /// gate -&gt; persisted recipe unlock -&gt; the one directing line. Returns TRUE only on the
        /// one real collection; every later call returns false and teaches nothing twice.
        /// </summary>
        public static bool TryCollect()
        {
            string recipeId = ManaRecipeScrollService.RecipeId;

            if (RecipeUnlocks.IsUnlocked(recipeId))
            {
                FlowTrace.Step("Progression",
                    $"mana-scroll collect ignored: recipe '{recipeId}' already taught (once-ever gate held)");
                return false;
            }

            RecipeUnlocks.Unlock(recipeId);     // writes the SeenTutorials key AND Save()s

            if (!RecipeUnlocks.IsUnlocked(recipeId))
            {
                // sec.12: no silent failure. If the write did not land, say so -- the prop stays
                // standing (the caller does not retire it) so a later walk-over retries.
                FlowTrace.Fail("Progression",
                    $"mana-scroll collect FAILED: '{recipeId}' did not persist (no GameStateService?). " +
                    "The scroll was NOT taught; the prop stays for a retry.");
                return false;
            }

            FlowTrace.Step("Progression",
                $"mana-scroll COLLECTED: recipe '{recipeId}' taught and persisted. Crafting is now a " +
                "visible system for this player and holds exactly ONE recipe (owner ruling WO-1235 #2 -- " +
                "the recipe book stays shut). WO-1235");

            // [KEY] The owner's shipped sequence ends "UI directs them to the ALREADY-ACCESSIBLE
            // crafting station". The station is guaranteed to exist -- ShouldSpawnDrop refused to
            // put this scroll in the world otherwise -- so this line can promise it honestly.
            // ASCII only; the meaning is entirely in the words, never in a hue.
            Guard.Try("Progression", "mana-scroll direction toast", () =>
                ElarionUiKit.ShowToast(
                    "Recipe learned: Mana Draught. Brew it at the Apothecary.",
                    ElarionUiKit.ToastTone.Gold, 5f));

            return true;
        }
    }

    /// <summary>The scroll's GLINT: gentle bob + slow spin + light pulse. Pure presentation,
    /// runtime-only, unscaled time so it breathes through any pause (the CastlePlansGlint
    /// grammar).</summary>
    internal sealed class ManaScrollGlint : MonoBehaviour
    {
        private Light _light;
        private float _baseY;
        private float _baseIntensity = 1.8f;

        public void Bind(Light light)
        {
            _light = light;
            if (_light != null) _baseIntensity = _light.intensity;
        }

        private void Start() => _baseY = transform.position.y;

        private void Update()
        {
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.2f);
            var p = transform.position;
            p.y = _baseY + Mathf.Lerp(0f, 0.14f, k);
            transform.position = p;
            transform.Rotate(0f, 30f * Time.unscaledDeltaTime, 0f, Space.World);
            if (_light != null)
                _light.intensity = Mathf.Lerp(_baseIntensity * 0.75f, _baseIntensity * 1.25f, k);
        }
    }
}
