// =============================================================================
// BossFireBreathBuilder (WO-759 / WO-757) - SCRIPT-authors the ASSET half of the
// Syndrath fire-breath ship: duplicates the Particle Pack FlameThrower recipe into
// Resources/VFX/Boss/Boss_FireBreath.prefab, wires its VFXCatalog row, and authors
// the VFX_BreathSocket on Boss_Dragon.prefab.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Namespace: DeNelle.Editor   (editor-only)
//
// WHY THIS EXISTS (the whole point):
//   Every step below touches a .prefab / .asset. Hand-editing that YAML is banned
//   (CLAUDE.md section 0 + section 3 - mount garble + resave corruption history), so the work
//   goes through AssetDatabase / PrefabUtility / SerializedObject and UNITY owns
//   the serialization. Nothing here writes a byte of YAML itself.
//
// WHAT IT DOES (WO-759 sections 6, 7.4, 7.5, 7.7):
//   1. AssetDatabase.CopyAsset  FlameThrower.prefab -> Resources/VFX/Boss/Boss_FireBreath.prefab
//      KEEPING THE WHOLE TREE (root + "FireEmbers (3)" + "Smoke"). Flattening or
//      dropping a layer is a review failure (WO section 2.3 / section 11) - so the copy is
//      VERIFIED against the source descendant + ParticleSystem counts and hard-fails
//      on any mismatch.
//   2. Renames the root, scales it to 2.5 (WO section 7.7 default; owner retunes), and
//      clears playOnAwake on every layer (WO section 11 anti-pattern: a combat prefab is
//      Play/Stop'd from code - VFXManager.PlayLoop calls PlayAllParticles, which
//      Clear()+Play()s EVERY system in the tree, so nothing is lost by clearing it,
//      and a prewarmed pool instance can no longer emit a stray jet at the origin).
//   3. Proves the CONTINUOUS family from the prefab itself (root emission
//      rateOverTime > 0 -> loop). The loop-vs-oneshot decision is the WO's central
//      fork; guessing it authors a WRONG catalog row, so a burst-shaped root
//      (rate 0 + bursts) FAILS the run instead.
//   4. Adds/updates the VFXCatalog row via SerializedObject:
//      Type=Boss_FireBreath, Prefab=the duplicate, IsLoop=true, PoolSize=2,
//      MinQuality=1 (skip on Low, WO section 5.4), LifetimeOverride=0 (auto-detect).
//   5. Authors "VFX_BreathSocket" on Boss_Dragon.prefab under the best head/jaw/
//      snout bone it can resolve BY NAME, with a small forward offset so the jet
//      starts outside the mesh. PrefabUtility.LoadPrefabContents / SaveAsPrefabAsset
//      only. Falls back to the prefab root with a LogWarning - never hard-crashes.
//      If DragonBoss exposes a "_breathSocket" field it is wired in the same pass.
//
// IDEMPOTENT - safe to run twice:
//   * The duplicate is copied only when absent; an existing one is REUSED (its GUID,
//     and therefore the catalog reference, survives).
//   * Scale is written only while it is still the untouched 1,1,1 - an owner-tuned
//     scale is preserved and reported.
//   * The catalog row is looked up by VFXType and UPDATED in place; only a missing
//     row grows the array. No duplicate rows, ever.
//   * The socket is found by name and reused; a non-zero (owner-tuned) local offset
//     is preserved and reported rather than stomped.
//
// TO FORCE A CLEAN REBUILD of the prefab: delete
//   Assets/Resources/VFX/Boss/Boss_FireBreath.prefab   and re-run.
//
// WHY REFLECTION FOR THE ENUM:
//   VFXType.Boss_FireBreath is appended by the sibling runtime change. Naming it in
//   C# here would make THIS file un-compilable until that lands and would take the
//   whole DeNelle.Editor assembly (and the compile gate) down with it. The value is
//   resolved BY NAME at run time instead, and its absence is a clean marker-FAIL.
//
// RUN:
//   Editor menu : Defenders/VFX/Build Boss FireBreath
//   Batchmode   : DeNelle.Editor.BossFireBreathBuilder.Build
//   Markers     : BOSS_FIREBREATH_BUILD_OK  /  BOSS_FIREBREATH_BUILD_FAIL
//                 (distinct to this entry point - a shared marker cannot say which
//                  step passed, which is the 2026-08-02 gate defect.)
//
// DOES NOT TOUCH: VFXType.cs, DragonBoss.cs, DeNelle-URP.asset, the Particle Pack
// itself (SOURCE RECIPE ONLY - never reimported, duplicated or modified), swoop /
// orbit math, DragonCinematicFlyby, or any second VFX bus.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Editor builder for the WO-759 boss fire breath assets: duplicates the pack
    /// FlameThrower into Resources/VFX/Boss, wires the VFXCatalog row and authors the
    /// VFX_BreathSocket on Boss_Dragon. Idempotent; prints BOSS_FIREBREATH_BUILD_OK.
    /// </summary>
    public static class BossFireBreathBuilder
    {
        // ── Markers (distinct per entry point) ────────────────────────────────
        private const string MarkerOk   = "BOSS_FIREBREATH_BUILD_OK";
        private const string MarkerFail = "BOSS_FIREBREATH_BUILD_FAIL";
        private const string Tag        = "[BossFireBreathBuilder] ";

        // ── Paths ─────────────────────────────────────────────────────────────
        // NOTE the spaces AND the ampersand in the pack folder name - both are legal
        // in an AssetDatabase path and must survive verbatim.
        private const string SourcePrefabPath =
            "Assets/UnityTechnologies/ParticlePack/EffectExamples/Fire & Explosion Effects/Prefabs/FlameThrower.prefab";

        private const string DestDir        = "Assets/Resources/VFX/Boss";
        private const string DestPrefabPath = "Assets/Resources/VFX/Boss/Boss_FireBreath.prefab";
        private const string DestRootName   = "Boss_FireBreath";

        private const string CatalogPath   = "Assets/Resources/VFX/VFXCatalog.asset";
        private const string BossPrefabPath = DeNelle.Core.AssetRoots.EnemyContent + "/Boss_Dragon.prefab";

        // ── Type names resolved at run time (see header) ──────────────────────
        private const string CatalogTypeName  = "DeNelle.Village.VFXCatalog, DeNelle.Village";
        private const string VfxTypeEnumName  = "DeNelle.Village.VFXType, DeNelle.Village";
        private const string BreathEnumMember = "Boss_FireBreath";
        private const string BossScriptName   = "DragonBoss";
        private const string BreathSocketField = "_breathSocket";

        // ── Tunables (WO section 7.7 defaults - owner retunes in the inspector) ───────
        private const float DefaultRootScale = 2.5f;
        private const int   CatalogPoolSize  = 2;
        private const bool  CatalogIsLoop    = true;
        private const int   CatalogMinQuality = 1;    // 0 always, 1 skip-Low, 2 High-only

        private const string SocketName = "VFX_BreathSocket";

        // Socket forward offset as a fraction of the dragon's overall size, clamped to
        // sane world metres. Proportional so it stays outside the mesh whatever scale
        // the boss prefab ends up at.
        private const float SocketOffsetFraction = 0.06f;
        private const float SocketOffsetMin      = 0.25f;
        private const float SocketOffsetMax      = 1.50f;

        // Head-end bone name keywords, best first. Scored case-insensitively.
        private static readonly string[] BoneKeywords = { "snout", "mouth", "jaw", "chin", "head" };

        // Rig helper suffixes that are NOT good parents for a VFX socket: control
        // nulls and zero-length nub tips do not always follow the deform skeleton.
        private static readonly string[] BoneExcludes = { "ctrl", "nub", "hlp", "ikgoal", "goal" };

        // =====================================================================
        //  Entry point
        // =====================================================================

        /// <summary>
        /// Builds the Boss_FireBreath prefab + catalog row + Boss_Dragon socket.
        /// Idempotent. Prints BOSS_FIREBREATH_BUILD_OK on success, _FAIL otherwise.
        /// </summary>
        [MenuItem("Defenders/VFX/Build Boss FireBreath")]
        public static void Build()
        {
            var report = new StringBuilder();
            try
            {
                GameObject dest = BuildPrefab(report);
                VerifyContinuousFamily(dest, report);
                WriteCatalogRow(dest, report);
                AuthorSocket(report);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log(Tag + "DONE. " + report);
                Debug.Log(MarkerOk + " - " + report);
            }
            catch (Exception e)
            {
                Debug.LogError(Tag + "FAILED: " + e.Message + "\n" + e.StackTrace);
                Debug.LogError(MarkerFail + " - " + e.Message + " || progress: " + report);
            }
        }

        // =====================================================================
        //  1-2. Duplicate the recipe (whole tree) + scale + playOnAwake
        // =====================================================================

        private static GameObject BuildPrefab(StringBuilder report)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
            if (source == null)
                throw new Exception("source recipe prefab not found at '" + SourcePrefabPath +
                                    "' - the Particle Pack must already be imported (never reimport it).");

            int srcDescendants = CountDescendants(source.transform);
            int srcSystems     = source.GetComponentsInChildren<ParticleSystem>(true).Length;
            report.Append("source=FlameThrower(").Append(srcDescendants).Append(" descendants, ")
                  .Append(srcSystems).Append(" ParticleSystems); ");

            EnsureDir(DestDir);

            bool freshCopy = false;
            var dest = AssetDatabase.LoadAssetAtPath<GameObject>(DestPrefabPath);
            if (dest == null)
            {
                if (!AssetDatabase.CopyAsset(SourcePrefabPath, DestPrefabPath))
                    throw new Exception("AssetDatabase.CopyAsset('" + SourcePrefabPath + "' -> '" +
                                        DestPrefabPath + "') returned false.");
                AssetDatabase.ImportAsset(DestPrefabPath, ImportAssetOptions.ForceUpdate);
                dest = AssetDatabase.LoadAssetAtPath<GameObject>(DestPrefabPath);
                freshCopy = true;
                if (dest == null)
                    throw new Exception("copied to '" + DestPrefabPath + "' but the asset would not load back.");
            }
            report.Append(freshCopy ? "copied NEW; " : "reused EXISTING (idempotent); ");

            // -- THE REVIEW-FAILURE GUARD: the whole multi-layer tree must be present.
            int dstDescendants = CountDescendants(dest.transform);
            int dstSystems     = dest.GetComponentsInChildren<ParticleSystem>(true).Length;
            if (dstDescendants != srcDescendants || dstSystems != srcSystems)
                throw new Exception("LAYER LOSS: duplicate has " + dstDescendants + " descendants / " +
                                    dstSystems + " ParticleSystems but the source recipe has " +
                                    srcDescendants + " / " + srcSystems +
                                    ". The multi-layer tree (root + FireEmbers + Smoke) must survive intact " +
                                    "(WO-759 section 2.3) - delete '" + DestPrefabPath + "' and re-run.");
            report.Append("tree=").Append(dstDescendants).Append(" descendants / ")
                  .Append(dstSystems).Append(" systems [");
            AppendChildNames(dest.transform, report);
            report.Append("] VERIFIED vs source; ");

            // -- Edit the prefab asset through prefab contents (Unity owns the write).
            GameObject contents = PrefabUtility.LoadPrefabContents(DestPrefabPath);
            try
            {
                bool dirty = false;

                if (contents.name != DestRootName)
                {
                    report.Append("root renamed '").Append(contents.name).Append("' -> '")
                          .Append(DestRootName).Append("'; ");
                    contents.name = DestRootName;
                    dirty = true;
                }

                Vector3 scale = contents.transform.localScale;
                bool untouched = Mathf.Approximately(scale.x, 1f)
                              && Mathf.Approximately(scale.y, 1f)
                              && Mathf.Approximately(scale.z, 1f);
                if (untouched)
                {
                    contents.transform.localScale = Vector3.one * DefaultRootScale;
                    report.Append("scale 1 -> ").Append(DefaultRootScale.ToString("0.##")).Append("; ");
                    dirty = true;
                }
                else
                {
                    report.Append("scale PRESERVED at ").Append(Fmt(scale))
                          .Append(" (already tuned - not stomped); ");
                }

                // playOnAwake off on every layer: this is a combat prefab, played
                // explicitly by VFXManager.PlayLoop -> PlayAllParticles (which Clear()s
                // and Play()s every system in the tree, children included).
                int cleared = 0;
                foreach (var ps in contents.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    if (!main.playOnAwake) continue;
                    main.playOnAwake = false;
                    cleared++;
                }
                if (cleared > 0)
                {
                    report.Append("playOnAwake cleared on ").Append(cleared).Append(" system(s); ");
                    dirty = true;
                }

                if (dirty) PrefabUtility.SaveAsPrefabAsset(contents, DestPrefabPath);
                else       report.Append("prefab already in target state; ");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.ImportAsset(DestPrefabPath, ImportAssetOptions.ForceUpdate);
            dest = AssetDatabase.LoadAssetAtPath<GameObject>(DestPrefabPath);
            if (dest == null)
                throw new Exception("'" + DestPrefabPath + "' would not reload after the edit pass.");

            Debug.Log(Tag + "prefab: " + DestPrefabPath + " (scale " +
                      Fmt(dest.transform.localScale) + ", " + dstSystems + " ParticleSystems)");
            return dest;
        }

        // =====================================================================
        //  3. Prove CONTINUOUS vs BURST from the prefab itself
        // =====================================================================

        private static void VerifyContinuousFamily(GameObject dest, StringBuilder report)
        {
            var systems = dest.GetComponentsInChildren<ParticleSystem>(true);
            if (systems.Length == 0)
                throw new Exception("the duplicate has NO ParticleSystem at all - nothing would ever emit.");

            float rootRate = 0f;
            int   rootBursts = 0;

            report.Append("emission{");
            for (int i = 0; i < systems.Length; i++)
            {
                var ps  = systems[i];
                var em  = ps.emission;
                var mn  = ps.main;

                float rate   = em.rateOverTime.constantMax;
                int   bursts = em.burstCount;

                if (i > 0) report.Append(", ");
                report.Append(ps.gameObject.name).Append(":rate=").Append(rate.ToString("0.##"))
                      .Append(",bursts=").Append(bursts)
                      .Append(",loop=").Append(mn.loop ? "Y" : "N")
                      .Append(em.enabled ? string.Empty : ",EMISSION-OFF");

                Debug.Log(Tag + "emission layer '" + ps.gameObject.name + "': rateOverTime=" +
                          rate.ToString("0.##") + " bursts=" + bursts + " looping=" + mn.loop +
                          " emissionEnabled=" + em.enabled);

                if (ps.transform == dest.transform)
                {
                    rootRate   = rate;
                    rootBursts = bursts;
                }
            }
            report.Append("}; ");

            // Family A (CONTINUOUS) = rateOverTime > 0 on the root. That is the signal
            // the whole IsLoop decision rests on (WO-759 section 3) - never assumed.
            if (rootRate > 0f)
            {
                report.Append("family=CONTINUOUS (root rateOverTime=")
                      .Append(rootRate.ToString("0.##")).Append(" > 0) -> IsLoop=true; ");
                Debug.Log(Tag + "family CONTINUOUS confirmed: root rateOverTime=" +
                          rootRate.ToString("0.##") + " - PlayAura/PlayLoop + VFXHandle.Stop is correct.");
                return;
            }

            throw new Exception("FAMILY MISMATCH: the root system's rateOverTime is " +
                                rootRate.ToString("0.##") + " with " + rootBursts +
                                " burst(s) - that reads as the BURST family (WO-759 section 3 family B), " +
                                "not the continuous stream this ship needs. Refusing to author an " +
                                "IsLoop=true catalog row on a oneshot recipe. Re-check the source prefab.");
        }

        // =====================================================================
        //  4. VFXCatalog row (SerializedObject - Unity owns the serialization)
        // =====================================================================

        private static void WriteCatalogRow(GameObject prefab, StringBuilder report)
        {
            var catalogType = Type.GetType(CatalogTypeName);
            if (catalogType == null)
                throw new Exception("could not resolve '" + CatalogTypeName + "'. Is DeNelle.Village compiled?");

            var enumType = Type.GetType(VfxTypeEnumName);
            if (enumType == null)
                throw new Exception("could not resolve '" + VfxTypeEnumName + "'.");

            if (!Enum.IsDefined(enumType, BreathEnumMember))
                throw new Exception("VFXType." + BreathEnumMember + " is not defined yet. The enum value is " +
                                    "the sibling runtime change (WO-759 section 7.3, VFXType.cs) - it must land before " +
                                    "the catalog row can be authored. Nothing was written to the catalog.");

            int enumValue = (int)Enum.Parse(enumType, BreathEnumMember);
            int enumOrdinal = EnumOrdinalFor(enumType, enumValue);

            var catalog = AssetDatabase.LoadAssetAtPath(CatalogPath, catalogType) as ScriptableObject;
            if (catalog == null)
                throw new Exception("VFXCatalog asset not found at '" + CatalogPath +
                                    "'. Run Defenders/VFX/Generate VFX Catalog first - this builder ADDS a row, " +
                                    "it never rebuilds the catalog.");

            var so = new SerializedObject(catalog);
            var entries = so.FindProperty("Entries");
            if (entries == null)
                throw new Exception("VFXCatalog has no serialized 'Entries' array property.");

            // Find an existing row for this type (UPDATE, never append a duplicate).
            int rowIndex = -1;
            for (int i = 0; i < entries.arraySize; i++)
            {
                var t = entries.GetArrayElementAtIndex(i).FindPropertyRelative("Type");
                if (t != null && t.enumValueIndex == enumOrdinal) { rowIndex = i; break; }
            }

            bool appended = rowIndex < 0;
            if (appended)
            {
                rowIndex = entries.arraySize;
                entries.arraySize = rowIndex + 1;
            }

            var e = entries.GetArrayElementAtIndex(rowIndex);
            var pType   = e.FindPropertyRelative("Type");
            var pPrefab = e.FindPropertyRelative("Prefab");
            var pPool   = e.FindPropertyRelative("PoolSize");
            var pLoop   = e.FindPropertyRelative("IsLoop");
            var pMinQ   = e.FindPropertyRelative("MinQuality");
            var pLife   = e.FindPropertyRelative("LifetimeOverride");

            if (pType   == null || pPrefab == null || pPool == null ||
                pLoop   == null || pMinQ   == null || pLife == null)
                throw new Exception("VFXCatalog.Entry is missing an expected field " +
                                    "(Type/Prefab/PoolSize/IsLoop/MinQuality/LifetimeOverride) - " +
                                    "the row shape changed; update this builder before running it again.");

            pType.enumValueIndex        = enumOrdinal;
            pPrefab.objectReferenceValue = prefab;
            pPool.intValue              = CatalogPoolSize;
            pLoop.boolValue             = CatalogIsLoop;
            pMinQ.intValue              = CatalogMinQuality;
            pLife.floatValue            = 0f;   // auto-detect from the particle duration

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);

            report.Append("catalog row ").Append(appended ? "APPENDED" : "UPDATED")
                  .Append(" at index ").Append(rowIndex).Append('/').Append(entries.arraySize)
                  .Append(" (Type=").Append(BreathEnumMember)
                  .Append(", IsLoop=").Append(CatalogIsLoop)
                  .Append(", PoolSize=").Append(CatalogPoolSize)
                  .Append(", MinQuality=").Append(CatalogMinQuality).Append("); ");

            Debug.Log(Tag + "catalog row " + (appended ? "appended" : "updated") + " at index " + rowIndex +
                      " of " + entries.arraySize + ": Type=" + BreathEnumMember + " (ordinal " + enumOrdinal +
                      ", value " + enumValue + ") Prefab=" + DestPrefabPath + " IsLoop=" + CatalogIsLoop +
                      " PoolSize=" + CatalogPoolSize + " MinQuality=" + CatalogMinQuality);
        }

        // =====================================================================
        //  5. VFX_BreathSocket on Boss_Dragon.prefab
        // =====================================================================

        private static void AuthorSocket(StringBuilder report)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath) == null)
                throw new Exception("boss prefab not found at '" + BossPrefabPath + "'.");

            GameObject contents = PrefabUtility.LoadPrefabContents(BossPrefabPath);
            try
            {
                Transform root = contents.transform;

                // -- Resolve the anchor bone by NAME (never by index/assumption). ----
                Transform anchor = ResolveBreathBone(root, out string anchorWhy);
                bool fallback = anchor == null;
                if (fallback)
                {
                    anchor = root;
                    Debug.LogWarning(Tag + "no head/jaw/mouth/snout bone matched on '" + BossPrefabPath +
                                     "' - FALLING BACK to the prefab ROOT '" + root.name +
                                     "' as the " + SocketName + " parent. The jet will not follow head motion " +
                                     "until an owner re-parents it.");
                }

                // -- Find or create the socket (idempotent by name). -----------------
                Transform socket = FindDescendantByName(root, SocketName);
                bool created = socket == null;
                if (created)
                {
                    var go = new GameObject(SocketName);
                    socket = go.transform;
                    go.layer = anchor.gameObject.layer;
                }

                Transform prevParent = created ? null : socket.parent;
                if (socket.parent != anchor)
                    socket.SetParent(anchor, worldPositionStays: false);

                // -- Offset: forward of the DRAGON (the prefab root), not of the bone.
                // Bone axes on a DCC-exported rig are arbitrary; the root's +Z is the
                // direction the dragon faces, which is what "outside the snout" means.
                // Aim itself is a runtime LookRotation (WO-759 section 2.4) - this is only the
                // start point.
                float size = MeasureSize(root);
                float offsetLen = Mathf.Clamp(size * SocketOffsetFraction, SocketOffsetMin, SocketOffsetMax);
                Vector3 tip = TipOf(anchor);
                Vector3 worldTarget = tip + root.forward * offsetLen;

                Vector3 before = socket.localPosition;
                bool tuned = !created && before.sqrMagnitude > 0.0001f;
                if (tuned)
                {
                    Debug.Log(Tag + "socket local offset PRESERVED at " + Fmt(before) +
                              " (already tuned - not stomped). Delete the child to re-derive it.");
                }
                else
                {
                    socket.position = worldTarget;
                    socket.rotation = root.rotation;   // sane default; code re-aims each cast
                }
                socket.localScale = Vector3.one;

                PrefabUtility.SaveAsPrefabAsset(contents, BossPrefabPath);

                // -- Optional: wire DragonBoss._breathSocket if the field exists. ----
                string wired = WireBreathSocketField(contents, socket);

                report.Append("socket '").Append(SocketName).Append("' ")
                      .Append(created ? "CREATED" : "REUSED")
                      .Append(" parent=").Append(anchor.name)
                      .Append(fallback ? " (ROOT FALLBACK)" : " (" + anchorWhy + ")")
                      .Append(" localPos=").Append(Fmt(socket.localPosition))
                      .Append(" worldPos=").Append(Fmt(socket.position))
                      .Append(" offsetLen=").Append(offsetLen.ToString("0.###"))
                      .Append(" bossSize=").Append(size.ToString("0.##"))
                      .Append("; ").Append(wired).Append("; ");

                Debug.Log(Tag + "socket '" + SocketName + "' " + (created ? "created" : "reused") +
                          " under bone '" + anchor.name + "'" +
                          (prevParent != null && prevParent != anchor ? " (re-parented from '" + prevParent.name + "')" : string.Empty) +
                          " - localPosition=" + Fmt(socket.localPosition) +
                          " worldPosition=" + Fmt(socket.position) +
                          " (forward offset " + offsetLen.ToString("0.###") +
                          "m along the dragon's forward; measured boss size " + size.ToString("0.##") + "m). " + wired);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.ImportAsset(BossPrefabPath, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>
        /// Best head-end bone by NAME. Deform bones win over ctrl/nub/helper nodes, and
        /// the keyword order (snout, mouth, jaw, chin, head) puts the jet as far forward
        /// as the rig allows. Returns null when nothing matches (caller falls back).
        /// </summary>
        private static Transform ResolveBreathBone(Transform root, out string why)
        {
            Transform best = null;
            int bestScore = 0;
            string bestWhy = string.Empty;

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root) continue;
                string lower = t.name.ToLowerInvariant();
                if (lower.Contains(SocketName.ToLowerInvariant())) continue;   // never anchor to ourselves

                int keywordIdx = -1;
                for (int k = 0; k < BoneKeywords.Length; k++)
                {
                    if (lower.Contains(BoneKeywords[k])) { keywordIdx = k; break; }
                }
                if (keywordIdx < 0) continue;

                bool excluded = false;
                foreach (var x in BoneExcludes)
                {
                    if (lower.Contains(x)) { excluded = true; break; }
                }

                // Keyword rank dominates; a clean deform bone outranks a helper node of
                // the same keyword. Excluded nodes stay eligible as a last resort so a
                // ctrl-only rig still gets a head-end socket rather than the root.
                int score = (BoneKeywords.Length - keywordIdx) * 10 + (excluded ? 0 : 5);
                if (score <= bestScore) continue;

                bestScore = score;
                best = t;
                bestWhy = "matched '" + BoneKeywords[keywordIdx] + "'" + (excluded ? ", helper node" : ", deform bone");
            }

            why = bestWhy;
            return best;
        }

        /// <summary>
        /// The forward-most point of a bone: its own nub/tip child when the rig has one
        /// (nubs mark the end of a chain), otherwise the bone's own origin.
        /// </summary>
        private static Vector3 TipOf(Transform bone)
        {
            for (int i = 0; i < bone.childCount; i++)
            {
                string n = bone.GetChild(i).name.ToLowerInvariant();
                if (n.Contains("nub") || n.Contains("tip") || n.Contains("end"))
                    return bone.GetChild(i).position;
            }
            return bone.position;
        }

        /// <summary>Longest bounds axis across every renderer, as a world-space size hint.</summary>
        private static float MeasureSize(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return 1f;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            return longest > 0.01f ? longest : 1f;
        }

        /// <summary>
        /// Wires DragonBoss._breathSocket to the socket when that serialized field
        /// exists (it is the sibling runtime change). Never fails the build if absent.
        /// </summary>
        private static string WireBreathSocketField(GameObject contents, Transform socket)
        {
            foreach (var mb in contents.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                if (mb.GetType().Name != BossScriptName) continue;

                var so = new SerializedObject(mb);
                var prop = so.FindProperty(BreathSocketField);
                if (prop == null)
                    return BossScriptName + "." + BreathSocketField +
                           " not present yet (runtime half not landed) - socket left unwired";

                if (prop.propertyType != SerializedPropertyType.ObjectReference)
                    return BossScriptName + "." + BreathSocketField + " is not an object reference - left unwired";

                if (ReferenceEquals(prop.objectReferenceValue, socket))
                    return BossScriptName + "." + BreathSocketField + " already wired";

                prop.objectReferenceValue = socket;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, BossPrefabPath);
                return BossScriptName + "." + BreathSocketField + " WIRED to " + SocketName;
            }
            return BossScriptName + " component not found on the boss prefab root - socket left unwired";
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private static Transform FindDescendantByName(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != root && t.name == name) return t;
            }
            return null;
        }

        private static int CountDescendants(Transform root)
        {
            int n = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != root) n++;
            }
            return n;
        }

        private static void AppendChildNames(Transform root, StringBuilder sb)
        {
            var names = new List<string>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != root) names.Add(t.name);
            }
            sb.Append(string.Join(" + ", names.ToArray()));
        }

        /// <summary>
        /// SerializedProperty.enumValueIndex is the ORDINAL position in the enum's value
        /// list, not the underlying int - map the value back to its ordinal.
        /// </summary>
        private static int EnumOrdinalFor(Type enumType, int underlyingValue)
        {
            var values = Enum.GetValues(enumType);
            for (int i = 0; i < values.Length; i++)
            {
                if ((int)values.GetValue(i) == underlyingValue) return i;
            }
            throw new Exception("enum value " + underlyingValue + " has no ordinal in " + enumType.Name + ".");
        }

        private static void EnsureDir(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;
            var parts = dir.Split('/');
            string cur = parts[0];                       // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        private static string Fmt(Vector3 v)
        {
            return "(" + v.x.ToString("0.###") + ", " + v.y.ToString("0.###") + ", " + v.z.ToString("0.###") + ")";
        }
    }
}
