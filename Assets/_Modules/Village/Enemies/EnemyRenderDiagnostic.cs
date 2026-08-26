// =============================================================================
//  EnemyRenderDiagnostic - WO-1210. Why does a fully textured enemy render BLACK?
// -----------------------------------------------------------------------------
//  THE SYMPTOM (owner device, 2026-08-25, build 2026.08.26.341323): every enemy
//  in a daylight wave renders as a flat black cut-out while the hero standing
//  beside it is lit, the terrain is lit, and the SAME enemy's lock-on portrait
//  renders fully textured and green-skinned in the very same frame.
//
//  WHAT IS ALREADY RULED OUT, so nobody re-hunts it:
//    * NOT missing content. [Flow:EnemyPool] resolves 'model:Hollow_Walker' and
//      pools it, behind R2_PARITY_OK on the catalog the device reports.
//    * NOT untextured art. EnemyBodyColorGuard's FINAL audit reports
//      textured=6/7, unpainted=0, repaired=0 across skeleton AND orc families.
//
//  ⛔ THIS FILE CONCLUDES NOTHING. It MEASURES (CLAUDE.md sec.12): static reading
//  LOCATES candidates, it never CONCLUDES, and two static theories about the
//  2026-08-20 capsule incident were both wrong at the cost of an hour before one
//  device line settled it. So this prints state and leaves the reading to a human.
//
//  ⭐ THE HERO IS THE CONTROL GROUP. A textured mesh rendering black is either
//  receiving no light or resolving no lit shader variant - and the hero, in the
//  same frame, under the same sky, is proof that the scene itself can light a
//  skinned body. THE DIFFERENCE BETWEEN THE TWO READINGS IS THE ANSWER. That is
//  why every block below is emitted for the enemy AND the hero together; a
//  reading of the enemy alone would be a number with nothing to compare it to.
//
//  Fires ONCE per family per session (FlowTrace.Once), so a wave of twenty bodies
//  costs one trace, not twenty.
//
//  ⛔ INSTRUMENTATION IS PERMANENT (CLAUDE.md sec.12, owner ruling 2026-08-09).
//  When this is solved, flag it off - never delete it. A stripped diagnostic turns
//  the next regression in this system back into a blank page.
// =============================================================================
using System.Text;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    public static class EnemyRenderDiagnostic
    {
        /// <summary>
        /// Emit one comparison dump for this family: scene lighting, then the enemy's
        /// renderers, then the hero's, so the two can be read side by side.
        /// Guarded end to end - a diagnostic must never be the thing that breaks a spawn.
        /// </summary>
        public static void ReportOnce(GameObject enemyVisual, string enemyId, string family)
        {
            if (enemyVisual == null) return;

            Guard.Try("EnemyRenderDiag", "report-" + (family ?? "?"), () =>
            {
                string key = "render-diag-" + (family ?? enemyId ?? "unknown");
                FlowTrace.Once("EnemyRenderDiag", key, BuildReport(enemyVisual, enemyId, family));
            });
        }

        private static string BuildReport(GameObject enemyVisual, string enemyId, string family)
        {
            var sb = new StringBuilder(1024);
            sb.Append("WO-1210 render dump for id='").Append(enemyId ?? "?")
              .Append("' family='").Append(family ?? "?").Append("'. ");

            // ── 1. THE SCENE'S LIGHTING CONTRACT ──────────────────────────────────
            // A dynamic, never-lightmapped body takes its indirect light from ambient.
            // If ambientMode is Baked in a scene with no baked probe data, dynamic
            // bodies go black while lightmapped static geometry stays lit - which is
            // EXACTLY the shape of the screenshot. Measured, not assumed.
            sb.Append("AMBIENT mode=").Append(RenderSettings.ambientMode)
              .Append(" intensity=").Append(RenderSettings.ambientIntensity.ToString("0.00"))
              .Append(" flat=").Append(RenderSettings.ambientLight)
              .Append(" sky=").Append(RenderSettings.ambientSkyColor)
              .Append(" equator=").Append(RenderSettings.ambientEquatorColor)
              .Append(" ground=").Append(RenderSettings.ambientGroundColor)
              .Append(" | probes=").Append(LightmapSettings.lightProbes != null
                    ? LightmapSettings.lightProbes.count.ToString() : "NONE")
              .Append(" lightmaps=").Append(LightmapSettings.lightmaps != null
                    ? LightmapSettings.lightmaps.Length.ToString() : "0")
              .Append(". ");

            // ── 2. THE MAIN LIGHT, AND WHETHER IT CAN SEE THIS BODY ───────────────
            // A culling mask that excludes the enemy's layer lights everything except
            // the enemies - a single-bit difference that renders exactly this symptom.
            var sun = RenderSettings.sun;
            if (sun == null)
            {
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l != null && l.type == LightType.Directional && l.isActiveAndEnabled) { sun = l; break; }
            }
            if (sun != null)
            {
                int enemyLayer = enemyVisual.layer;
                bool lightsEnemy = (sun.cullingMask & (1 << enemyLayer)) != 0;
                sb.Append("SUN name='").Append(sun.name).Append("' enabled=").Append(sun.isActiveAndEnabled)
                  .Append(" intensity=").Append(sun.intensity.ToString("0.00"))
                  .Append(" color=").Append(sun.color)
                  .Append(" cullingMask=0x").Append(sun.cullingMask.ToString("X"))
                  .Append(" enemyLayer=").Append(enemyLayer).Append(" ('")
                  .Append(LayerMask.LayerToName(enemyLayer)).Append("') LIGHTS-ENEMY=").Append(lightsEnemy)
                  .Append(". ");
            }
            else
            {
                sb.Append("SUN none - no active directional light found. ");
            }

            // ── 3. THE ENEMY, THEN THE HERO. Same fields, same order, on purpose. ──
            AppendRenderers(sb, "ENEMY", enemyVisual);

            var hero = GameObject.FindWithTag("Player");
            if (hero != null) AppendRenderers(sb, "HERO(control)", hero);
            else sb.Append("HERO(control) absent - no GameObject tagged 'Player' this frame; ")
                   .Append("the comparison is INCOMPLETE and this dump proves less than it should. ");

            return sb.ToString();
        }

        private static void AppendRenderers(StringBuilder sb, string label, GameObject root)
        {
            var rends = root.GetComponentsInChildren<Renderer>(true);
            sb.Append(label).Append(" renderers=").Append(rends.Length).Append(" ");
            if (rends.Length == 0) { sb.Append("(none - nothing to draw). "); return; }

            // First TWO renderers only: enough to compare, short enough to read on a
            // phone log. A twenty-slot dump is a dump nobody reads.
            int shown = 0;
            foreach (var r in rends)
            {
                if (r == null || shown >= 2) continue;
                shown++;
                sb.Append("[").Append(r.GetType().Name).Append(" '").Append(r.name).Append("'")
                  .Append(" enabled=").Append(r.enabled)
                  .Append(" probeUsage=").Append(r.lightProbeUsage)
                  .Append(" reflUsage=").Append(r.reflectionProbeUsage)
                  .Append(" probeAnchor=").Append(r.probeAnchor != null ? "set" : "null")
                  .Append(" staticBatch=").Append(r.isPartOfStaticBatch)
                  .Append(" shadows=").Append(r.shadowCastingMode)
                  .Append(" layer=").Append(LayerMask.LayerToName(r.gameObject.layer));

                var mats = r.sharedMaterials;
                if (mats != null)
                {
                    int m = 0;
                    foreach (var mat in mats)
                    {
                        if (mat == null) { sb.Append(" mat").Append(m++).Append("=NULL"); continue; }
                        sb.Append(" mat").Append(m++).Append("='").Append(mat.name).Append("'")
                          .Append(" shader='").Append(mat.shader != null ? mat.shader.name : "<null>").Append("'")
                          // ⛔ isSupported is the one that catches a variant stripped out of the
                          // Android build: it is TRUE in the editor and FALSE on device.
                          .Append(" supported=").Append(mat.shader != null && mat.shader.isSupported)
                          .Append(" queue=").Append(mat.renderQueue);
                        if (mat.HasProperty("_BaseColor")) sb.Append(" _BaseColor=").Append(mat.GetColor("_BaseColor"));
                        else if (mat.HasProperty("_Color")) sb.Append(" _Color=").Append(mat.GetColor("_Color"));
                        if (mat.HasProperty("_BaseMap"))
                            sb.Append(" _BaseMap=").Append(mat.GetTexture("_BaseMap") != null ? "set" : "NULL");
                    }
                }
                sb.Append("] ");
            }
            if (rends.Length > shown) sb.Append("(+").Append(rends.Length - shown).Append(" more) ");
        }
    }
}
