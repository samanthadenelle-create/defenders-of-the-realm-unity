// =============================================================================
// RealmPinProducers — the NAMED publishers that actually put content on the map
// (WO-829 §3, program WO-825).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World
//
// THE HOLE THIS CLOSES: RealmPinBoard, RealmPins and RealmAtmosphereStyle all
// shipped, both surfaces read the board — and NOTHING EVER PUBLISHED. The board
// sat at zero pins forever, so the parchment map and the corner minimap were
// faithfully rendering an empty registry. The seam existed; the producers did not.
//
// ── WHY EACH PRODUCER IS NAMED, AND WHY THE NAME IS A CONSTANT ────────────────
// RealmPinBoard.Publish is per-source REPLACE. That is what makes a producer
// IDEMPOTENT: "hero" re-publishing its one You-pin at 1 Hz overwrites its own
// bucket, so the board holds ONE hero pin after a thousand ticks, not a thousand.
// The property depends entirely on the id being STABLE — the ids live in
// DeNelle.Core.World.RealmPinSources as constants precisely so a producer cannot
// half-rename itself, land in a second bucket, and duplicate every pin it owns
// with no way to ever clear the orphan. (This is the same bug the retired
// TownHudBridge.PushMinimapPois had from the other direction: it rebuilt the
// WHOLE list every tick and clobbered everyone else's pins.)
//
// Each producer therefore obeys three rules:
//   1. ONE source id, from RealmPinSources, used for both Publish and Clear.
//   2. Publish the COMPLETE current set every time — never append.
//   3. Nothing to show => Clear (Publish of an empty list does this for you), so
//      a stale pin cannot outlive the thing it pointed at.
//
// ── FOG / SPOILERS ────────────────────────────────────────────────────────────
// Gated by the EXISTING predicate, RealmPinBoard.RevealsDetail, fed the live
// region state from RealmMapVM.RegionStateFor — the ONE derivation site for
// "is this region discovered?". No second fog rule is defined here, and none may
// be: the predicate is fail-closed by design (an unknown state reads as locked),
// and a producer that rolled its own would be the thing that leaks.
//
// ── RAID PINS ARE MARKERS, NOT PERMISSION ─────────────────────────────────────
// A RaidTarget pin says "there is a camp there". It does NOT say the player may
// attack it: the full-army gate is re-checked on the Raids flow when the player
// actually taps through. Nothing in this file bypasses, pre-checks or implies
// that gate, and the pin labels are deliberately descriptive ("Raid camp") rather
// than imperative ("Attack") for exactly that reason.
//
// ── ISOLATION ─────────────────────────────────────────────────────────────────
// Self-bootstrapping via RuntimeInitializeOnLoadMethod (the
// DungeonWorldPortalSpawner pattern): no scene edit, no prefab dependency, no
// bake. Every world lookup is Guard-wrapped (§12) and null-tolerant, so a scene
// without heroes/portals/outposts publishes nothing instead of throwing into the
// HUD. ASCII-only player strings; Elarion, never Avalon (the data id "avalon" is
// wire-compat with the React save and is not a player-facing string).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;
using DeNelle.Core.World;
using DeNelle.Village.Hero;                // RealmMapVM.RegionStateFor — the ONE state derivation
using DeNelle.Village.World.Camps;         // RaidOutpostSystem / EnemyOutpost — the live raid camps

namespace DeNelle.Village.World
{
    /// <summary>
    /// The shipped content-pin producers. Each <c>Publish*</c> is idempotent: calling it
    /// twice leaves the board exactly as calling it once did (see the file header).
    /// </summary>
    public static class RealmPinProducers
    {
        /// <summary>Seconds between automatic refreshes when <see cref="RealmPinProducerHost"/>
        /// is driving. 1 Hz: the hero pin is the only fast-moving one and the minimap's own
        /// projection is what makes it look smooth, not this cadence.</summary>
        public const float RefreshSeconds = 1f;

        // Reused buffers — a producer publishing at 1 Hz forever must not allocate a list
        // per tick. RealmPinBoard.Publish COPIES what it is given, so reuse is safe.
        private static readonly List<RealmPin> _buf = new List<RealmPin>(8);

        /// <summary>Run every producer once. Safe to call at any time from any scene.</summary>
        public static void PublishAll()
        {
            PublishHero();
            PublishDungeons();
            PublishRaids();
            PublishArmy();
        }

        /// <summary>Drop every pin this file owns (scene teardown / regression hook).
        /// Deliberately clears only OUR source ids — <c>RealmPinBoard.ClearAll</c> would
        /// also wipe the legacy VillageHudController bucket, which is not ours to drop.</summary>
        public static void ClearAll()
        {
            RealmPinBoard.Clear(RealmPinSources.Hero);
            RealmPinBoard.Clear(RealmPinSources.Dungeons);
            RealmPinBoard.Clear(RealmPinSources.Raids);
            RealmPinBoard.Clear(RealmPinSources.Army);
        }

        // ── "realm.hero" — where the player is ────────────────────────────────

        /// <summary>Publish the player's own position as the single <c>You</c> pin.
        /// The hero is found BY COMPONENT (<c>HeroLocomotion</c>), not by a tag — CLAUDE.md
        /// §7: a GameObject carries one tag and the "HeroTarget" tag was never declared.</summary>
        public static void PublishHero()
        {
            var hero = Guard.Try("RealmPins", "find hero",
                () => Object.FindFirstObjectByType<HeroLocomotion>(), null);

            _buf.Clear();
            if (hero != null)
            {
                var p = hero.transform.position;
                _buf.Add(new RealmPin(RealmPinKind.You, p.x, p.z,
                    RealmAtmosphereStyle.Pin(RealmPinKind.You).Label, HomeRegionId()));
            }
            RealmPinBoard.Publish(RealmPinSources.Hero, _buf);
        }

        // ── "realm.dungeons" — the portals the player has found ───────────────

        /// <summary>Publish one <c>Dungeon</c> pin per live <see cref="DungeonPortal"/>.
        /// Sourced from the PORTALS THEMSELVES rather than from a table of where dungeons
        /// ought to be, so a portal that failed to place cannot leave a pin pointing at
        /// nothing — the pin and the door are the same fact.</summary>
        public static void PublishDungeons()
        {
            var portals = Guard.Try("RealmPins", "find dungeon portals",
                () => Object.FindObjectsByType<DungeonPortal>(FindObjectsSortMode.None),
                System.Array.Empty<DungeonPortal>());

            string region = HomeRegionId();
            _buf.Clear();
            if (portals != null && RevealsDetail(region))
            {
                for (int i = 0; i < portals.Length; i++)
                {
                    var portal = portals[i];
                    if (portal == null) continue;
                    var p = portal.transform.position;
                    _buf.Add(new RealmPin(RealmPinKind.Dungeon, p.x, p.z,
                        portal.DisplayName, region));
                }
            }
            RealmPinBoard.Publish(RealmPinSources.Dungeons, _buf);
        }

        // ── "realm.raids" — camps that are still standing ─────────────────────

        /// <summary>
        /// Publish one <c>RaidTarget</c> pin per UNCLEARED outpost. Cleared camps drop out
        /// on the next refresh, which is the whole reason producers re-publish rather than
        /// append: clearing a camp removes its pin with no explicit teardown call anywhere.
        ///
        /// MARKER ONLY — see the file header. The label names the place and its garrison;
        /// it never says the player may attack, because the full-army gate is checked on
        /// the Raids flow and a pin that implied otherwise would be promising an action the
        /// game is about to refuse.
        /// </summary>
        public static void PublishRaids()
        {
            var outposts = Guard.Try("RealmPins", "read raid outposts",
                () => RaidOutpostSystem.Outposts, System.Array.Empty<EnemyOutpost>());

            string region = HomeRegionId();
            _buf.Clear();
            if (outposts != null && RevealsDetail(region))
            {
                for (int i = 0; i < outposts.Length; i++)
                {
                    var o = outposts[i];
                    if (o == null || o.Cleared) continue;
                    var p = o.transform.position;
                    int alive = o.AliveCount;
                    string label = RealmAtmosphereStyle.Pin(RealmPinKind.RaidTarget).Label;
                    if (alive > 0) label += " - " + alive + " defending";
                    _buf.Add(new RealmPin(RealmPinKind.RaidTarget, p.x, p.z, label, region,
                                          count: alive > 0 ? 1 : 0));
                }
            }
            RealmPinBoard.Publish(RealmPinSources.Raids, _buf);
        }

        // ── "realm.army" — where the player's soldiers muster ─────────────────

        /// <summary>Publish an <c>Army</c> pin per built barracks. Read off the live
        /// <see cref="PlacedStructure"/> markers — the same objects the BaseLayout save
        /// round-trips — so a barracks that was moved or sold moves or loses its pin with
        /// no second bookkeeping path.</summary>
        public static void PublishArmy()
        {
            var placed = Guard.Try("RealmPins", "find placed structures",
                () => Object.FindObjectsByType<PlacedStructure>(FindObjectsSortMode.None),
                System.Array.Empty<PlacedStructure>());

            string region = HomeRegionId();
            _buf.Clear();
            if (placed != null)
            {
                for (int i = 0; i < placed.Length; i++)
                {
                    var s = placed[i];
                    if (s == null || string.IsNullOrEmpty(s.itemId)) continue;
                    if (s.itemId.IndexOf("barracks", System.StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    var p = s.transform.position;
                    _buf.Add(new RealmPin(RealmPinKind.Army, p.x, p.z,
                        RealmAtmosphereStyle.Pin(RealmPinKind.Army).Label, region));
                }
            }
            RealmPinBoard.Publish(RealmPinSources.Army, _buf);
        }

        // ── shared helpers ────────────────────────────────────────────────────

        /// <summary>The realm-map id of the home base. Falls back to "" (an un-regioned
        /// world pin) rather than a hardcoded id — the catalog is the source of truth and
        /// a world pin with no region still draws correctly on the minimap.</summary>
        private static string HomeRegionId()
        {
            var home = RealmMapCatalog.Home;
            return home != null && !string.IsNullOrEmpty(home.Id) ? home.Id : "";
        }

        /// <summary>THE fog gate, and the only one: the shared fail-closed predicate fed by
        /// the single live state derivation. An un-regioned pin ("" region) is world-only
        /// content with nothing to spoil, so it passes.</summary>
        private static bool RevealsDetail(string regionId)
        {
            if (string.IsNullOrEmpty(regionId)) return true;
            return RealmPinBoard.RevealsDetail(RealmMapVM.RegionStateFor(regionId));
        }
    }

    /// <summary>
    /// The driver: ticks <see cref="RealmPinProducers.PublishAll"/> so the board reflects
    /// the live world. Self-bootstraps after scene load (no scene edit, no prefab, no bake)
    /// and marks itself DontDestroyOnLoad so a scene change does not silently stop the
    /// publishing — the failure mode this whole file exists to end.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RealmPinProducerHost : MonoBehaviour
    {
        private static RealmPinProducerHost _instance;
        private float _next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("RealmPinProducerHost");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<RealmPinProducerHost>();
            FlowTrace.Step("RealmPins", "producer host bootstrapped (publishing every "
                + RealmPinProducers.RefreshSeconds + "s)");
        }

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + RealmPinProducers.RefreshSeconds;
            // Guard the whole sweep (§12): a producer that throws must not take the tick —
            // and therefore every OTHER producer — down with it.
            Guard.Try("RealmPins", "producer sweep", RealmPinProducers.PublishAll);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            RealmPinProducers.ClearAll();
        }
    }
}
