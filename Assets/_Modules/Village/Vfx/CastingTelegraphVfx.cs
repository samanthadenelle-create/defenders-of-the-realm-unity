// =============================================================================
// CastingTelegraphVfx - owner ruling 2026-08-16: the Spells Pack Casting_* loop
// prefabs telegraph a CAST WIND-UP on the caster, INSTEAD of the HUD cast bar.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Owner ruling (verbatim): "Assets\Spells Pack\Particles\Prefabs\Projectiles\
// Casting\ use these when telegraphing Wind up of casting instead of the current
// HUD casting bar."
//
// PRESENTATION REPLACEMENT ONLY - cast timing / interrupts / refunds are owned
// by the cast seams (HeroAbilities.CastRoutine, Enemy.RootedCast) and are not
// touched here. This class only:
//   1. maps a spell SCHOOL to the school's Casting_* base prefab (the _2/_3/_4
//      suffixed prefabs are alternates the owner can re-tag later; the BASE
//      variant per school is the default),
//   2. spawns/despawns the loop parented to the caster (feet/hands per the
//      prefab's own authored shape - no per-call offset guessing),
//   3. tracks which casters have a LIVE spawned telegraph so the HUD cast-bar
//      producer can suppress the bar ONLY when the VFX actually spawned.
//
// FALLBACK RULE (load-bearing): a missing mirror prefab => FlowTrace.Warn ONCE
// per school + return null, and the caller/producer keeps the HUD bar for that
// cast. The player must ALWAYS see wind-up feedback - bar suppression applies
// strictly when IsTelegraphed(caster) is true.
//
// REVERSIBILITY: UseVfxTelegraph (default TRUE per the ruling's "instead of").
// Flipping it false makes TryBegin a no-op => IsTelegraphed never true => the
// HUD cast bar path is byte-identical to the pre-ruling behaviour.
//
// Prefab source: Assets/Spells Pack/Particles/Prefabs/Projectiles/Casting/
// (GITIGNORED pack). Runtime loads the committed MIRRORS under
// Assets/Resources/VFX/Projectiles/ (SpellsPackVfxMirror owns mirroring; as of
// 2026-08-16 only Casting_Fire + Casting_Fire_2 are mirrored - the other
// schools are registered here future-proof and warn+fall back until mirrored).
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Spawns the school-matched Spells Pack <c>Casting_*</c> loop on a caster
    /// during a spell wind-up, replacing the HUD cast bar as the telegraph
    /// (owner ruling 2026-08-16). The HUD bar remains the FALLBACK whenever the
    /// school's mirror prefab is missing or <see cref="UseVfxTelegraph"/> is off.
    /// </summary>
    public static class CastingTelegraphVfx
    {
        /// <summary>
        /// Master switch (default ON per the owner's "instead of"). FALSE restores
        /// the pre-ruling HUD cast bar for every wind-up - one flag flip, fully
        /// reversible. Lives here (not FeatureFlags) per the WO's file constraints.
        /// </summary>
        public static bool UseVfxTelegraph = true;

        private const string FlowSystem = "CastTelegraph";

        // School -> committed mirror path (Resources.Load, no extension). BASE
        // variant per school; Casting_*_2/_3/_4 are owner-retaggable alternates.
        private static readonly Dictionary<string, string> SchoolPrefabPaths =
            new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "arcane", "VFX/Projectiles/Casting_Arcane" },
                { "dark",   "VFX/Projectiles/Casting_Dark"   },
                { "fire",   "VFX/Projectiles/Casting_Fire"   },
                { "ice",    "VFX/Projectiles/Casting_Ice"    },
                { "light",  "VFX/Projectiles/Casting_Light"  },
                { "nature", "VFX/Projectiles/Casting_Nature" },
                { "storm",  "VFX/Projectiles/Casting_Storm"  },
            };

        // Keyword -> school, matched in declaration order against (vfx key,
        // then name, then id) - the authored VFX key is the strongest element
        // signal, the display name next, the id last. "storm" is checked before
        // "light" so "lightning"/"Thunderbolt" resolve to storm, not light.
        private static readonly (string school, string[] keywords)[] SchoolKeywords =
        {
            ("fire",   new[] { "fire", "flame", "ember", "meteor", "burn", "cataclysm" }),
            ("ice",    new[] { "ice", "frost", "freez", "chill", "snow" }),
            ("storm",  new[] { "storm", "thunder", "lightning", "shock" }),
            ("nature", new[] { "nature", "poison", "thorn", "root", "venom" }),
            ("light",  new[] { "light", "holy", "heal", "mend", "salve", "radiant", "grace", "aegis" }),
            ("dark",   new[] { "dark", "void", "shadow", "drain", "curse" }),
            ("arcane", new[] { "arcane", "mana", "blink", "magic" }),
        };

        /// <summary>Terminal fallback school when nothing matches.</summary>
        public const string DefaultSchool = "arcane";

        // Warn ONCE per school on a missing mirror (Section 12: logged, never silent,
        // never per-cast spam).
        private static readonly HashSet<string> _warnedMissing =
            new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        // Casters with a LIVE spawned telegraph - the HUD cast-bar producer
        // suppresses the bar only for these (the load-bearing fallback rule).
        private static readonly HashSet<Component> _active = new HashSet<Component>();

        /// <summary>
        /// Resolves a spell's school from its authored strings. Priority: the
        /// authored VFX cast key (strongest element signal), then the display
        /// name, then the id. Unresolved => <see cref="DefaultSchool"/>.
        /// </summary>
        public static string ResolveSchool(string id, string name, string vfxCastKey)
        {
            string s = MatchSchool(vfxCastKey);
            if (s == null) s = MatchSchool(name);
            if (s == null) s = MatchSchool(id);
            return s ?? DefaultSchool;
        }

        private static string MatchSchool(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string lower = text.ToLowerInvariant();
            for (int i = 0; i < SchoolKeywords.Length; i++)
            {
                var (school, keywords) = SchoolKeywords[i];
                for (int k = 0; k < keywords.Length; k++)
                    if (lower.Contains(keywords[k])) return school;
            }
            return null;
        }

        /// <summary>
        /// Spawns the school's Casting_* loop parented to <paramref name="caster"/>
        /// at its root (the prefab's authored shape places the feet ring / hand
        /// glows). Returns the live instance, or NULL when the telegraph could not
        /// spawn (flag off / missing mirror / null caster) - the caller and the
        /// HUD producer then keep the cast bar for this cast (never a silent
        /// no-telegraph). Call <see cref="End"/> on complete/interrupt/death.
        /// </summary>
        public static GameObject TryBegin(Component caster, string school, string abilityName, float windupSeconds)
        {
            if (!UseVfxTelegraph || caster == null) return null;

            Prune(); // drop fake-null entries from casters destroyed mid-cast

            if (string.IsNullOrEmpty(school)) school = DefaultSchool;
            if (!SchoolPrefabPaths.TryGetValue(school, out string path))
            {
                WarnOnce(school, "unregistered school '" + school + "'");
                return null;
            }

            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null)
            {
                // Mirror not committed yet (only Casting_Fire/_2 mirrored as of
                // 2026-08-16). Warn once per school; the HUD bar carries the
                // telegraph for this cast.
                WarnOnce(school, "mirror prefab missing at Resources/" + path +
                                 " - falling back to the HUD cast bar");
                return null;
            }

            var go = Object.Instantiate(prefab, caster.transform.position,
                                        caster.transform.rotation, caster.transform);
            go.name = "CastingTelegraph_" + school;
            _active.Add(caster);

            FlowTrace.Step(FlowSystem,
                "windup-telegraph START school=" + school + " prefab=" + path +
                " caster=" + caster.name + " ability='" + (abilityName ?? "?") +
                "' windup=" + windupSeconds.ToString("0.00") + "s (HUD bar suppressed for this cast)");
            return go;
        }

        /// <summary>
        /// TRUE while <paramref name="caster"/> has a live spawned wind-up telegraph.
        /// The HUD cast-bar producer suppresses the bar ONLY when this is true -
        /// a failed spawn leaves it false, so the bar fallback shows.
        /// </summary>
        public static bool IsTelegraphed(Component caster)
        {
            return caster != null && _active.Contains(caster);
        }

        /// <summary>
        /// Despawns a caster's wind-up telegraph (cast committed, interrupted, or
        /// the caster died). Safe on null/already-ended handles. A caster DESTROYED
        /// mid-cast needs no call - the instance is parented and dies with it
        /// (the registry entry is pruned on the next TryBegin).
        /// </summary>
        public static void End(Component caster, GameObject handle, string reason)
        {
            bool wasActive = caster != null && _active.Remove(caster);
            if (handle != null)
            {
                Object.Destroy(handle);
                FlowTrace.Step(FlowSystem,
                    "windup-telegraph END caster=" + (caster != null ? caster.name : "<gone>") +
                    " reason=" + (reason ?? "?"));
            }
            else if (wasActive)
            {
                // Registered but the instance is already gone (destroyed with a
                // dead caster's hierarchy) - trace the close so the window is
                // bounded in a capture.
                FlowTrace.Step(FlowSystem, "windup-telegraph END (instance already gone) reason=" + (reason ?? "?"));
            }
        }

        // ── Target-of-cast marker (owner pick 2026-08-16, second ruling) ──────────
        // "Assets\Hovl Studio\Map track markers VFX\Prefabs\Marker 2 Pointer Loop.prefab"
        // -> "Target of Casting spell": during the wind-up the spell's TARGET shows the
        // pointer loop, hovering on the targeted unit (parented) or standing on the
        // ground point for area casts. ADDITIVE ONLY - the HUD-bar fallback rule does
        // NOT extend to this marker: a missing mirror just warn-onces and skips it.
        //
        // The asset is the SAME mirror TalentPointerVfxMirror.cs committed for the
        // talent-tree node pointer (one mirrored asset, two consumers). Its destination
        // naming is talent-specific; the committer may want to rename it neutrally
        // later - until then both consumers load this path.
        private const string TargetMarkerResourcePath = "VFX/UI/TalentNodePointer";
        private const string TargetMarkerWarnKey = "_target-marker";

        /// <summary>
        /// Spawns the Marker 2 Pointer Loop on the spell's TARGET for the wind-up:
        /// parented to <paramref name="targetUnit"/> when the cast targets a unit,
        /// else standing at <paramref name="groundPoint"/> for area casts. Returns
        /// null (traced, never silent) for untargeted/self casts, a missing mirror
        /// (warn once), or <see cref="UseVfxTelegraph"/> off. The instance carries a
        /// windup+1s auto-destroy safety net; call <see cref="EndTargetMarker"/> on
        /// complete/interrupt/caster death (a dead TARGET tears the parented
        /// instance down with its hierarchy).
        /// </summary>
        public static GameObject TryBeginTargetMarker(Component caster, Transform targetUnit,
                                                      Vector3? groundPoint, string abilityName, float windupSeconds)
        {
            if (!UseVfxTelegraph || caster == null) return null;
            if (targetUnit == null && !groundPoint.HasValue)
            {
                FlowTrace.Step(FlowSystem,
                    "target-marker SKIP: untargeted/self cast '" + (abilityName ?? "?") +
                    "' by " + caster.name + " - nothing to point at");
                return null;
            }

            var prefab = Resources.Load<GameObject>(TargetMarkerResourcePath);
            if (prefab == null)
            {
                if (_warnedMissing.Add(TargetMarkerWarnKey))
                    FlowTrace.Warn(FlowSystem, "target-marker mirror missing at Resources/" +
                        TargetMarkerResourcePath + " - marker skipped (additive-only; " +
                        "warned once, no bar fallback implied)");
                return null;
            }

            GameObject go;
            string where;
            if (targetUnit != null)
            {
                go = Object.Instantiate(prefab, targetUnit.position, Quaternion.identity, targetUnit);
                where = "unit=" + targetUnit.name;
            }
            else
            {
                go = Object.Instantiate(prefab, groundPoint.Value, Quaternion.identity);
                where = "point=" + groundPoint.Value.ToString("F1");
            }
            go.name = "CastTargetMarker";
            // Safety net: an unparented ground marker (or a leaked handle on a caster
            // destroyed mid-cast) self-destroys shortly after the wind-up window.
            Object.Destroy(go, Mathf.Max(0.05f, windupSeconds) + 1f);

            FlowTrace.Step(FlowSystem,
                "target-marker START " + where + " path=" + TargetMarkerResourcePath +
                " caster=" + caster.name + " ability='" + (abilityName ?? "?") +
                "' windup=" + windupSeconds.ToString("0.00") + "s");
            return go;
        }

        /// <summary>Despawns the target marker (commit/interrupt/caster death). Safe on null.</summary>
        public static void EndTargetMarker(GameObject handle, string reason)
        {
            if (handle == null) return;
            Object.Destroy(handle);
            FlowTrace.Step(FlowSystem, "target-marker END reason=" + (reason ?? "?"));
        }

        private static void WarnOnce(string school, string detail)
        {
            if (!_warnedMissing.Add(school)) return;
            FlowTrace.Warn(FlowSystem, "school '" + school + "': " + detail +
                                       " (warned once; subsequent casts fall back silently to the bar)");
        }

        // Remove fake-null keys left by casters destroyed mid-cast (their VFX
        // instance died with the hierarchy; only the registry entry lingers).
        private static readonly List<Component> _pruneScratch = new List<Component>();
        private static void Prune()
        {
            _pruneScratch.Clear();
            foreach (var c in _active)
                if (c == null) _pruneScratch.Add(c);
            for (int i = 0; i < _pruneScratch.Count; i++)
                _active.Remove(_pruneScratch[i]);
        }
    }
}
