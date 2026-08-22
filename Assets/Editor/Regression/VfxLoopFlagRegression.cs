// =============================================================================
// VfxLoopFlagRegression [vfx-loop-flag] -- the oracle that stops a BURST prefab
// from being catalogued as a LOOP, and the single home of the loop-vs-burst
// derivation the whole project shares.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression   Namespace: DeNelle.Editor.Regression
//
// WHY THIS EXISTS (the leak it guards, proven from captured data):
//   VFXManager.Hovl.cs ~283-288 -- a row with IsLoop TRUE increments _activeLoops,
//   hands back a VFXHandle and registers NO reclaim deadline. Only the oneshot
//   branch (~290-297) calls RegisterOneshot + ReturnHovlAfterSeconds. The ONLY
//   loop reclaim in the system is PruneDestroyedFromSet (VFXManager.cs ~973),
//   which frees loops whose HOST was destroyed -- and pooled hosts are never
//   destroyed. So any fire-and-forget play of a loop-flagged row leaks one of the
//   _maxActiveLoops = 20 slots (VFXManager.cs:142) for the rest of the session.
//   Six separate F8 captures show that cap saturated and starving a live effect:
//     capture-20260730-175552.md:55  PlayKey('ArcherTower_Projectile') SKIPPED -
//                                    active loops 20/20 (cap hit)
//     capture-20260730-175447.md:21  ARcaneTower_Projectile
//     capture-20260730-175729.md:54  ArcaneTower-Baselevel_Projectile
//     capture-20260716-205819.md:99  Poi_NodeAura
//     capture-20260716-210343.md:97  Poi_Landmark
//   The flags got there because IsLoop was a STICKY MANUAL UI TOGGLE in
//   VfxCasterWindow (a checkbox, force-set true for the Projectile/Aura roles),
//   never a read of the prefab. 95 of 135 HovlVfxCatalog rows carried IsLoop:1,
//   including PP_BigExplosion / PP_MuzzleFlash / PP_EarthShatter and friends --
//   rate-0 + burst-at-t0 prefabs that self-terminate in under a second.
//
// WHAT IT ASSERTS:
//   For every catalog row (HovlVfxCatalog.asset AND VFXCatalog.asset) whose
//   prefab RESOLVES and carries a ParticleSystem, the STORED IsLoop equals the
//   value DERIVED from that prefab's emission. Rows whose prefab is missing (the
//   art packs are gitignored; a fresh clone has none of them) or that carry no
//   ParticleSystem at all are SKIPPED AND COUNTED -- never failed. A suite that
//   goes red on a clean clone is a suite the next person deletes.
//
// SHARED DERIVATION (deliberate): TryDerive below is the ONE implementation.
//   VfxLoopFlagAudit (the fixer) and VfxCasterWindow (the authoring UI) both call
//   it, because DeNelle.Editor references DeNelle.EditorRegression. The oracle is
//   not "testing its own algorithm": it asserts a fact about STORED DATA (the
//   .asset bytes) against the prefabs on disk. One derivation means the tool that
//   writes the flag and the gate that judges it can never disagree.
//
// POSITIVE CONTROL (prove it can go red): open
//   Assets/Resources/VFX/HovlVfxCatalog.asset, flip any one row's `IsLoop: 0` to
//   `IsLoop: 1` (e.g. PP_TinyExplosion), re-run the suite -- it must fail naming
//   that exact key with stored=True derived=False. Flip it back.
//
// Editor-only asset reads. No scene, no play mode, no runtime singletons.
//
// Registered in DataRegression.RunAll as [vfx-loop-flag].
// =============================================================================

using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class VfxLoopFlagRegression
    {
        public const string HovlCatalogPath = "Assets/Resources/VFX/HovlVfxCatalog.asset";
        public const string TypedCatalogPath = "Assets/Resources/VFX/VFXCatalog.asset";

        // How many curve samples to take when an emission rate is authored as a curve
        // rather than a constant. Keys alone can miss a hump between them.
        private const int CurveSamples = 12;

        // =====================================================================
        //  THE DERIVATION -- the single authority on "is this prefab a loop?"
        // =====================================================================
        //
        // COMBINING RULE (explicit, and it is an AND, not an OR):
        //
        //   derivedIsLoop = root.main.loop  AND  root emits by RATE
        //
        //   where "emits by RATE" means the emission module is ENABLED and
        //   max(rateOverTime) > 0 OR max(rateOverDistance) > 0.
        //
        // Both halves are load-bearing:
        //   * main.loop == false  -> the system stops on its own after main.duration.
        //     It is NOT a sustained stream no matter how high the rate is, so it must
        //     be a oneshot or the pool slot is never reclaimed.
        //   * rate == 0 with only BURSTS -> the classic explosion/impact shape:
        //     everything is spat out at t=0 and the system idles. Even when someone
        //     left main.loop ticked on such a prefab (looping an empty stream), it
        //     self-terminates visually and must never hold a loop slot. This is the
        //     exact shape of all eleven PP_* explosion/impact rows the audit flips.
        //
        // rateOverDistance counts as a rate on purpose: a projectile trail that emits
        // per metre travelled is a genuinely sustained emitter whose lifetime is the
        // FLIGHT, not a duration -- calling it a oneshot would reclaim it mid-flight.
        // The audit reports how many rows qualify by distance alone so that widening
        // is auditable rather than silent.
        //
        // AUTHORITY IS THE ROOT SYSTEM: the prefab root's own ParticleSystem, or the
        // first one in the hierarchy when the root is a bare transform (the Hovl
        // packs' usual shape). Sub-emitters and secondary smoke layers are decoration;
        // the root is what "this effect" means. When a child disagrees with the root
        // the detail string says so -- reported, never allowed to change the verdict.
        //
        // ONE EXCEPTION, and it is measured, not assumed: a root whose EMISSION MODULE
        // IS DISABLED emits nothing at all, so it is a container, not the effect. The
        // authority then falls through to the first system that can actually emit.
        // Assets/Lana Studio/Casual RPG VFX/Prefabs/Fire/Fire_medium.prefab is exactly
        // this shape -- root ParticleSystem with `enabled: 0` on its emission over a
        // child emitting 15/sec on loop. Reading the disabled root would call the
        // burning-structure / torch / fog auras one-shots and cut them off mid-burn.
        // The fallback skips systems on inactive GameObjects for the same reason.
        //
        // Returns FALSE when the truth is UNDETERMINABLE (null prefab, or no
        // ParticleSystem anywhere in it). An undeterminable prefab must never flip a
        // stored flag and must never fail a gate -- callers skip and name it.
        // ── Standing owner rulings outrank the derivation ────────────────────────
        //
        // The prefab is the authority on what the ART DOES. It is NOT the authority on
        // what the GAME SHOULD DO with it. Where the owner has already ruled on a row
        // after seeing it in play, that ruling wins and is pinned here with its reason.
        //
        // A pin is not a workaround for an inconvenient derivation - it is the record of
        // a decision that came from felt play, which no amount of reading emission
        // modules can reproduce. Adding one requires an owner ruling to point at.
        //
        // Keyed by catalog key; value = the flag the row MUST carry.
        private static readonly System.Collections.Generic.Dictionary<string, bool> OwnerPinned =
            new System.Collections.Generic.Dictionary<string, bool>
        {
            // The upgrade fireworks. The prefab genuinely emits continuously (rate 5 on
            // loop), so the derivation promotes it to a loop - and that is exactly the
            // "perma-fireworks" bug the owner reported and had fixed: the celebration
            // never ended. It is played fire-and-forget at BuildModeController's upgrade
            // completion with its handle discarded, so as a loop it would also leak one of
            // the 20 global loop slots forever. A celebration is FINITE. The existing
            // vfx-aura-diff oracle already encodes this ruling and correctly failed the
            // first run of the derivation - that failure is why this table exists.
            { "UpgradeStructureComplete_Aura", false },

            // The Realm Store landmark ring (WO-1052; owner tagged this key to
            // Assets/Resources/VFX/Markers/Marker8_SafeZoneLoop.prefab on 2026-08-21 as the store's
            // persistent near-field signature -- see HovlVfxCatalogGenerator's Map entry).
            //
            // WHAT THE ART ACTUALLY DOES, read from the prefab: root Marker8_SafeZoneLoop is
            // looping:1, lengthInSec:1, emission ENABLED, rateOverTime Constant 0, and ONE burst of
            // 1 at t=0.1. So `rootLoop && byRate` derives FALSE purely on the zero rate.
            // (The `minScalar: 10` sitting next to `scalar: 0` is Unity's vestigial default in the
            // unused TwoConstants slot -- every prefab in Resources/VFX carries it. The derivation
            // reads `scalar`, which is correct; this is NOT a mis-read field.)
            //
            // WHY THE DERIVATION'S PREMISE DOES NOT HOLD HERE: the "rate 0 + bursts only" clause is
            // justified above as the explosion shape that "spits everything out at t=0 and idles".
            // That is true of a system whose main.loop is FALSE. This one loops, and a LOOPING
            // system re-fires its bursts every duration cycle -- the ring genuinely repeats forever
            // and never self-terminates. It is a persistent landmark exactly as the owner intended.
            //
            // WHY A PIN AND NOT A WIDER DERIVATION: seven other prefabs on disk share this exact
            // shape (Buff_Light, PortalCircleDarkStar, Casting_Fire, Casting_Fire_2, BigExplosion,
            // TalentNodePointer, Cast_MuzzleFlash) and EVERY one is stored IsLoop:0 and passes.
            // Widening the rule to (loop && bursts) would flip all seven to expected-loop and
            // re-open the pool leak for the muzzle-flash/explosion rows -- the precise bug this
            // oracle exists to stop. Emission alone cannot separate the two cases; only the CALL
            // SITE can, which is what a pin is for.
            //
            // WHY True IS SAFE HERE (the leak cannot occur): RealmStoreBeacon RETAINS the handle in
            // _nearAura and releases it on four paths -- leaving the 20m proximity ring, scene
            // unload, OnDisable, OnDestroy. It is not fire-and-forget (which is where the documented
            // leak comes from), poolSize is 1, and it only runs while the hero stands at the store.
            // Stamping False instead would send it down the oneshot branch, whose reclaim deadline
            // would return the ring to the pool while the player is still standing there: the
            // landmark vanishes and _nearAura is left holding a stale handle.
            //
            // NOTE for the owner: the ruling pointed at here is her 2026-08-21 pick of this key as a
            // PERSISTENT landmark, not a felt-play ruling on the flag itself. The pin is also the
            // only durable home for it -- HovlVfxCatalogGenerator overrides its own Map literal with
            // TryResolveExpected, so without this entry the next regenerate silently flips the row
            // back to 0 and the beacon starts despawning under the player.
            { "store.beacon.near", true },
        };

        /// <summary>
        /// The flag a row must carry, honouring any standing owner ruling over the
        /// derived value. Returns false when there is no pin for this key.
        /// </summary>
        public static bool TryOwnerPin(string key, out bool pinnedIsLoop, out string why)
        {
            pinnedIsLoop = false;
            why = null;
            if (string.IsNullOrEmpty(key)) return false;
            if (!OwnerPinned.TryGetValue(key, out pinnedIsLoop)) return false;
            why = "PINNED by a standing owner ruling - the prefab's own emission is overridden here on purpose";
            return true;
        }

        /// <summary>
        /// THE flag a catalog row must carry: a standing owner ruling if one exists,
        /// otherwise the prefab's derived truth. Every consumer - the audit, this suite,
        /// and both catalog generators - goes through here, so a pin cannot be honoured in
        /// one place and forgotten in another. That divergence is how the original bug
        /// survived: one surface believed a checkbox, another believed the art.
        /// Returns false only when the truth is UNDETERMINABLE (skip and name it).
        /// </summary>
        public static bool TryResolveExpected(string key, GameObject prefab,
                                              out bool expected, out string detail)
        {
            string why;
            if (TryOwnerPin(key, out expected, out why))
            {
                // Still derive, purely so the report can say what the art actually does
                // and how far the ruling departs from it. A pin that silently matched the
                // derivation would be dead weight nobody could audit.
                bool derived; string dd;
                detail = TryDerive(prefab, out derived, out dd)
                    ? why + " (art derives " + derived + "; " + dd + ")"
                    : why + " (art underivable: " + dd + ")";
                return true;
            }
            return TryDerive(prefab, out expected, out detail);
        }

        public static bool TryDerive(GameObject prefab, out bool derivedIsLoop, out string detail)
        {
            derivedIsLoop = false;
            detail = "no prefab";
            if (prefab == null) return false;

            var systems = prefab.GetComponentsInChildren<ParticleSystem>(true);
            if (systems == null || systems.Length == 0)
            {
                detail = "no ParticleSystem in prefab";
                return false;
            }

            bool viaFallback;
            var root = PickAuthority(prefab, systems, out viaFallback);
            if (root == null)
            {
                detail = "no ParticleSystem in prefab";
                return false;
            }

            bool rootLoop = root.main.loop;
            var em = root.emission;
            bool emissionOn = em.enabled;
            float rateTime = emissionOn ? MaxOf(em.rateOverTime) : 0f;
            float rateDist = emissionOn ? MaxOf(em.rateOverDistance) : 0f;
            int bursts = emissionOn ? em.burstCount : 0;

            bool byRate = rateTime > 0f || rateDist > 0f;
            derivedIsLoop = rootLoop && byRate;

            // Report-only: does any OTHER system in the prefab derive differently?
            bool childDisagrees = false;
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (ps == null || ps == root) continue;
                var cem = ps.emission;
                bool cByRate = cem.enabled && (MaxOf(cem.rateOverTime) > 0f || MaxOf(cem.rateOverDistance) > 0f);
                if ((ps.main.loop && cByRate) != derivedIsLoop) { childDisagrees = true; break; }
            }

            var sb = new StringBuilder();
            sb.Append("root='").Append(root.gameObject.name).Append("'");
            if (viaFallback) sb.Append(" (prefab root emits nothing -- authority fell through to this system)");
            sb.Append(", loop=").Append(rootLoop ? "True" : "False");
            sb.Append(", emission=").Append(emissionOn ? "on" : "OFF");
            sb.Append(", rateOverTime=").Append(rateTime.ToString("0.###"));
            sb.Append(", rateOverDistance=").Append(rateDist.ToString("0.###"));
            sb.Append(", bursts=").Append(bursts);
            sb.Append(", systems=").Append(systems.Length);
            if (childDisagrees) sb.Append(", NOTE: a child system derives differently (root is authority)");
            detail = sb.ToString();
            return true;
        }

        /// <summary>
        /// The ONE system whose emission decides the verdict: the prefab root's own
        /// ParticleSystem when it can actually emit, otherwise the first system in
        /// hierarchy order that can (emission module enabled, GameObject active). A
        /// disabled/inactive shell emits nothing and cannot speak for the effect. If
        /// nothing can emit, the root (or first) system is returned anyway so the caller
        /// still gets a reading rather than a silent skip.
        /// </summary>
        public static ParticleSystem PickAuthority(GameObject prefab, ParticleSystem[] systems, out bool viaFallback)
        {
            viaFallback = false;
            if (prefab == null) return null;
            if (systems == null || systems.Length == 0)
                systems = prefab.GetComponentsInChildren<ParticleSystem>(true);
            if (systems == null || systems.Length == 0) return null;

            var own = prefab.GetComponent<ParticleSystem>();
            if (own != null && CanEmit(own)) return own;

            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (ps == null || ps == own) continue;
                if (!CanEmit(ps)) continue;
                viaFallback = true;
                return ps;
            }

            if (own != null) return own;
            for (int i = 0; i < systems.Length; i++)
                if (systems[i] != null) return systems[i];
            return null;
        }

        private static bool CanEmit(ParticleSystem ps)
        {
            if (ps == null) return false;
            if (!ps.gameObject.activeSelf) return false;
            return ps.emission.enabled;
        }

        /// <summary>True when the derivation qualifies ONLY through rateOverDistance
        /// (rateOverTime is zero). Lets the audit report how much the distance clause
        /// widens the rule instead of hiding it.</summary>
        public static bool QualifiesByDistanceOnly(GameObject prefab)
        {
            if (prefab == null) return false;
            bool viaFallback;
            var root = PickAuthority(prefab, null, out viaFallback);
            if (root == null) return false;
            var em = root.emission;
            if (!em.enabled) return false;
            return MaxOf(em.rateOverTime) <= 0f && MaxOf(em.rateOverDistance) > 0f;
        }

        /// <summary>Largest value a MinMaxCurve can take, across every authoring mode.
        /// Curve modes are sampled (keys alone can miss a hump) and scaled by the
        /// curve multiplier, which is where Unity actually stores the magnitude.</summary>
        public static float MaxOf(ParticleSystem.MinMaxCurve c)
        {
            switch (c.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return c.constant;
                case ParticleSystemCurveMode.TwoConstants:
                    return Mathf.Max(c.constantMin, c.constantMax);
                case ParticleSystemCurveMode.Curve:
                    return Mathf.Abs(c.curveMultiplier) * MaxOfCurve(c.curve);
                case ParticleSystemCurveMode.TwoCurves:
                    return Mathf.Abs(c.curveMultiplier) *
                           Mathf.Max(MaxOfCurve(c.curveMin), MaxOfCurve(c.curveMax));
                default:
                    return c.constantMax;
            }
        }

        private static float MaxOfCurve(AnimationCurve curve)
        {
            if (curve == null) return 0f;
            float max = 0f;
            var keys = curve.keys;
            if (keys != null)
                for (int i = 0; i < keys.Length; i++)
                    max = Mathf.Max(max, keys[i].value);
            for (int i = 0; i <= CurveSamples; i++)
                max = Mathf.Max(max, curve.Evaluate(i / (float)CurveSamples));
            return max;
        }

        // =====================================================================
        //  THE ORACLE
        // =====================================================================

        /// <summary>
        /// Asserts every resolvable catalog row's stored IsLoop equals the value derived
        /// from its prefab's emission. Missing prefabs / prefabs with no ParticleSystem
        /// are skipped and counted (gitignored art packs must not turn this red).
        /// </summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            int checkedRows = 0, skipped = 0, catalogsFound = 0;
            var notes = new List<string>();

            // --- HovlVfxCatalog: string-keyed rows (the 135-row catalog) -------------
            var hovl = AssetDatabase.LoadAssetAtPath<HovlVfxCatalog>(HovlCatalogPath);
            if (hovl == null)
            {
                failures.Add("HovlVfxCatalog.asset did not load from " + HovlCatalogPath +
                             " -- VFXManager.PlayKey resolves nothing and every keyed effect no-ops.");
            }
            else
            {
                catalogsFound++;
                var rows = hovl.Rows ?? new HovlVfxCatalog.Row[0];
                int hSkipped = 0;
                for (int i = 0; i < rows.Length; i++)
                {
                    var row = rows[i];
                    string key = string.IsNullOrEmpty(row.Key) ? ("<row " + i + ">") : row.Key;
                    bool derived;
                    string detail;
                    if (!TryResolveExpected(key, row.Prefab, out derived, out detail))
                    {
                        skipped++; hSkipped++;
                        continue;
                    }
                    checkedRows++;
                    if (row.IsLoop != derived)
                        failures.Add("HovlVfxCatalog '" + key + "': stored IsLoop=" + row.IsLoop +
                                     " but the prefab derives " + derived + " (" + detail +
                                     "). A burst prefab flagged as a loop never returns its pool slot " +
                                     "and permanently burns one of the 20 loop slots.");
                }
                notes.Add("hovl rows=" + rows.Length + " skipped=" + hSkipped);
            }

            // --- VFXCatalog: VFXType-keyed rows --------------------------------------
            // Types are reported via ToString() only -- naming an enum MEMBER here would
            // fail-compile the whole editor assembly the day that member is renamed.
            var typed = AssetDatabase.LoadAssetAtPath<VFXCatalog>(TypedCatalogPath);
            if (typed == null)
            {
                failures.Add("VFXCatalog.asset did not load from " + TypedCatalogPath +
                             " -- every VFXType falls through to the procedural fallback.");
            }
            else
            {
                catalogsFound++;
                var entries = typed.Entries ?? new VFXCatalog.Entry[0];
                int tSkipped = 0;
                for (int i = 0; i < entries.Length; i++)
                {
                    var e = entries[i];
                    string name = e.Type.ToString();
                    bool derived;
                    string detail;
                    if (!TryResolveExpected(name, e.Prefab, out derived, out detail))
                    {
                        skipped++; tSkipped++;
                        continue;
                    }
                    checkedRows++;
                    if (e.IsLoop != derived)
                        failures.Add("VFXCatalog '" + name + "': stored IsLoop=" + e.IsLoop +
                                     " but the prefab derives " + derived + " (" + detail +
                                     "). Fix it with Defenders/VFX/Audit Loop Flags, and correct the " +
                                     "isLoop argument in VFXCatalogGenerator.Map or the next Generate undoes it.");
                }
                notes.Add("typed rows=" + entries.Length + " skipped=" + tSkipped);
            }

            if (catalogsFound == 0)
            {
                reason = "vfx-loop-flag: NEITHER VFX catalog asset loaded -- nothing could be verified.";
                return false;
            }

            if (failures.Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append("vfx-loop-flag FAILED (").Append(failures.Count).Append(" row(s) disagree with their prefab; ")
                  .Append(checkedRows).Append(" checked, ").Append(skipped).Append(" skipped): ");
                sb.Append(string.Join(" | ", failures.ToArray()));
                reason = sb.ToString();
                return false;
            }

            reason = "vfx-loop-flag OK: " + checkedRows + " catalog row(s) match their prefab emission (" +
                     string.Join(", ", notes.ToArray()) + "); " + skipped +
                     " skipped (prefab absent or no ParticleSystem -- gitignored art packs are not a failure).";
            return failures.Count == 0;
        }
    }
}
