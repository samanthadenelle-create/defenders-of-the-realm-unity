// =============================================================================
// VfxResourceArtMirror - makes the tracked Resources/VFX prefabs GENUINELY
// self-contained by mirroring the art they reference out of the gitignored packs
// and re-pointing every reference at the mirror.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Namespace: DeNelle.Editor   (editor-only)
//
// THE DEFECT THIS CORRECTS - A SHIPPED, COMMITTED CLAIM THAT WAS FALSE.
//
// On 2026-08-05 the Particle Pack recipes were duplicated into
// Assets/Resources/VFX/** and the Boss_FireBreath commit message states that the
// tracked copy is what ships, so shipped VFX no longer depend on gitignored art.
// THAT IS NOT TRUE. AssetDatabase.CopyAsset duplicates the PREFAB FILE ONLY. It
// never duplicates the materials, textures, shaders, meshes or animation the
// prefab points at - those references keep pointing straight back into the pack.
//
// Measured on this tree BEFORE this builder existed (recursive GUID walk over
// every .prefab under Assets/Resources/VFX/**):
//
//   28 prefabs -> 73 distinct assets inside two GITIGNORED roots
//                 Assets/UnityTechnologies/  (.gitignore:399)  30 assets
//                 Assets/Spells Pack/        (.gitignore:214)  43 assets
//   Boss_FireBreath ........ 6 (Embers.mat, SmokeDark.mat, fireball.mat + 3 .tif)
//   Env_Candle ............. 2 (TinyFlame.mat -> TinyFlame.tif)
//   Harvest_Food/Crystal/
//     Collector_Ready ...... 9 each (2 materials, FireFly.fbx MESH,
//                            ParticlesLight.prefab via the LIGHTS MODULE,
//                            FireFly.shader, 4 textures)
//   Projectile_Storm ...... 14 (worst row)
//   Flash_generic .......... 0 (its art is Lana Studio, which is TRACKED)
//
// CONSEQUENCE: on a fresh clone, the laptop, or CI - any machine without the
// packs - every one of those references resolves to nothing and the effect
// renders with MISSING materials: magenta, untextured white, black or invisible
// depending on platform. The owner's acceptance criterion that night was visual
// proof of "no magenta leak through, no missing shaders". This defect IS that,
// latent only because this machine happens to have the packs on disk.
//
// WHAT THIS BUILDER DOES
//
//   PASS 1  STRIP pack CODE. Two Spells Pack DEMO MonoBehaviours
//           (ZakhanSpellsPack.Projectile, ZakhanSpellsPack.CreateProjectile,
//           Assets/Spells Pack/Demo/Scripts/) are serialized onto seven of the
//           mirrored projectile prefabs. A .cs CANNOT be mirrored: both copies
//           would compile into Assembly-CSharp and collide (CS0101 duplicate
//           type), so the compile gate would go red for every parallel lane.
//           They are REMOVED, not mirrored, and that is the right call twice
//           over - grepped, no project code references ZakhanSpellsPack, and
//           what these demo components do inside a POOLED, manager-driven VFX
//           prefab is actively wrong: Projectile.Start() does
//           GetComponent<Rigidbody>().linearVelocity (NRE with no Rigidbody,
//           and the effect physically flies away if there is one) and its
//           OnCollisionEnter Destroy(gameObject)s a POOLED instance, which
//           corrupts the VFXManager pool; CreateProjectile InvokeRepeating-spawns
//           a fireball every second, forever. Removing them also severs the ONLY
//           reason those prefabs reached six pack .prefab files (the demo scripts
//           held them in ExplosionPrefab / Fireball fields) - 10 of the 73
//           exposed assets, 4.06 MB, disappear from the mirror set for free.
//           Every removal is logged by prefab, component and script path.
//
//   PASS 2  MIRROR + REMAP, recursively to a fixed point. For every reference
//           that resolves into a gitignored art root: copy the asset ONCE into
//           Assets/Resources/VFX/_Shared/<bucket>/ and re-point the reference at
//           the copy. A mirrored MATERIAL is then processed the same way, so its
//           textures and its shader are mirrored too - a mirrored material whose
//           texture still points into the pack has solved nothing. The worklist
//           is a fixed point, so mirror-of-mirror-of-mirror terminates only when
//           nothing new appears.
//
//   PASS 3  VERIFY, and refuse to claim success otherwise. Re-reads
//           AssetDatabase.GetDependencies(prefab, recursive:true) per prefab and
//           counts what STILL resolves into a gitignored root. Any non-zero count
//           NAMES the prefab and the offending assets and emits the failure
//           marker with NO success marker. This whole task is worthless if it
//           reports success while the exposure remains.
//
// DEDUPE: mirrors are keyed by SOURCE ASSET PATH, so the 12 prefabs that share
// Glow.mat get ONE Glow.mat, and TinyFlame.mat is mirrored once for both
// Env_Candle and Aura_NearDeath. 27 materials / 19 png / 11 tif / 1 fbx /
// 1 shader / 2 anim / 1 controller / 1 prefab -- 63 files, not 183 copies.
//
// IDEMPOTENT + GUID-STABLE: the source->mirror map is persisted to
// Assets/Editor/VfxArtMirrorManifest.json (Assets/Editor never ships, so this
// costs the build nothing). A mirror file that already exists is REUSED, never
// re-copied - which is exactly what preserves its .meta GUID, and therefore every
// prefab reference into it, across runs. On a second run the prefabs no longer
// reference the pack at all, so PASS 2 finds nothing to do and writes nothing.
//
// SCOPE: only what the shipped prefabs actually reference is mirrored. The packs
// are NEVER bulk-copied - dumping a pack into Resources/ defeats the whole point
// of the curated catalog (Unity ships every byte under Resources/ unstripped).
// The packs themselves are never modified, deleted or reimported.
//
// NO YAML IS WRITTEN BY HAND. Every asset edit goes through AssetDatabase /
// PrefabUtility / SerializedObject and Unity owns the serialization (CLAUDE.md
// sections 0 + 3). The one plain-text edit is the mirrored .shader's DECLARED
// NAME (see RenameMirroredShader) - ShaderLab, not YAML.
//
// RUN:
//   Editor menu : Defenders/VFX/Mirror VFX Art Into Resources
//   Batchmode   : DeNelle.Editor.VfxResourceArtMirror.Run
//   Markers     : VFX_ART_MIRROR_OK / VFX_ART_MIRROR_FAIL
//                 (distinct to this entry point - a marker shared with another
//                  entry point cannot say WHICH step passed, the 2026-08-02
//                  gate defect.)
//
// THE RULE HAS ONE HOME. What counts as a "gitignored art root", and the
// dependency measurement itself, live in
// DeNelle.Editor.Regression.VfxResourceSelfContainmentRegression and are CALLED
// from here, never re-derived. Two derivations of one rule is how a tool and its
// gate come to disagree while both report success.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Rule = DeNelle.Editor.Regression.VfxResourceSelfContainmentRegression;

namespace DeNelle.Editor
{
    /// <summary>
    /// Mirrors every gitignored-pack asset a Resources/VFX prefab depends on into the
    /// tracked tree and remaps the references. Idempotent; prints VFX_ART_MIRROR_OK
    /// only when every prefab measures ZERO remaining pack dependencies.
    /// </summary>
    public static class VfxResourceArtMirror
    {
        // -- Markers (distinct per entry point) ---------------------------------
        private const string MarkerOk   = "VFX_ART_MIRROR_OK";
        private const string MarkerFail = "VFX_ART_MIRROR_FAIL";
        private const string Tag        = "[VfxResourceArtMirror] ";

        // -- Paths --------------------------------------------------------------
        // The mirror lives INSIDE the curated VFX tree so the self-containment gate's
        // "everything under Assets/Resources/VFX/" invariant covers it too.
        private const string SharedRoot = Rule.SharedRoot;

        // The source->mirror map. Assets/Editor is stripped from every player build,
        // so this manifest is free at runtime. It is what makes a mirror name
        // COLLISION detectable across runs (two different pack assets that share a
        // file name must not silently reuse each other's mirror).
        private const string ManifestPath = "Assets/Editor/VfxArtMirrorManifest.json";

        // Prefix pushed onto a mirrored shader's DECLARED name. Both the pack shader
        // and its mirror exist on this machine; two ShaderLab files declaring the same
        // Shader "..." name make Shader.Find ambiguous and log a duplicate-name warning.
        private const string MirrorShaderPrefix = "VFXMirror/";

        // Fixed-point safety stop. The real graph is ~90 assets deep in the tens;
        // anything past this is a cycle bug, and looping forever in batchmode is worse
        // than failing loudly.
        private const int MaxWorkItems = 5000;

        // Asset kinds whose references live in SerializedObject-editable data. Everything
        // else (textures, models, ShaderLab, shadergraph JSON) is either a leaf or is
        // owned by an importer, and is verified by PASS 3 rather than rewritten here.
        private static readonly string[] EditableExtensions =
        {
            ".prefab", ".mat", ".controller", ".overridecontroller", ".anim",
            ".asset", ".physicmaterial", ".mixer", ".mask",
        };

        // Serialized properties that must NEVER be repointed: the script binding and the
        // nested-prefab linkage. Rewriting either corrupts the asset. If one of them
        // points into a pack it is reported as an unmirrorable dependency instead.
        private static readonly string[] UntouchableProperties =
        {
            "m_Script", "m_CorrespondingSourceObject", "m_PrefabInstance",
            "m_PrefabAsset", "m_SourcePrefab",
        };

        // ---------------------------------------------------------------------
        //  Run state
        // ---------------------------------------------------------------------
        private static Dictionary<string, string> _mirrorBySource;
        private static Dictionary<string, string> _sourceByMirror;
        private static List<string> _created;
        private static List<string> _reused;
        private static List<string> _recreated;
        private static List<string> _stripped;
        private static List<string> _errors;

        // =====================================================================
        //  Entry point
        // =====================================================================

        /// <summary>
        /// Mirrors the pack art every Resources/VFX prefab depends on into the tracked
        /// tree, remaps the references recursively, then PROVES zero remaining exposure.
        /// Prints VFX_ART_MIRROR_OK only on a clean verify; on ANY failure prints
        /// VFX_ART_MIRROR_FAIL and no success marker.
        /// </summary>
        [MenuItem("Defenders/VFX/Mirror VFX Art Into Resources")]
        public static void Run()
        {
            var summary = new StringBuilder();

            _mirrorBySource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _sourceByMirror = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _created   = new List<string>();
            _reused    = new List<string>();
            _recreated = new List<string>();
            _stripped  = new List<string>();
            _errors    = new List<string>();

            try
            {
                LoadManifest();

                var prefabs = Rule.VfxPrefabPaths();
                if (prefabs.Count == 0)
                    throw new Exception("no prefabs found under " + Rule.VfxRoot +
                                        " - the curated VFX tree is empty or missing; there is nothing to mirror.");

                // -- BEFORE: the measurement that names the defect ------------------
                var before = new Dictionary<string, int>(StringComparer.Ordinal);
                int beforeTotal = 0;
                foreach (var p in prefabs)
                {
                    int n = Rule.PackDependenciesOf(p).Count;
                    before[p] = n;
                    beforeTotal += n;
                }
                Debug.Log(Tag + "BEFORE: " + beforeTotal + " reference(s) into gitignored art roots across " +
                          prefabs.Count + " prefab(s).");

                // -- PASS 1: strip pack CODE (cannot be mirrored - see header) -------
                foreach (var p in prefabs) StripPackScripts(p);

                // -- PASS 2: mirror + remap to a fixed point -------------------------
                var queue = new Queue<string>();
                var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in prefabs) queue.Enqueue(p);

                int work = 0;
                while (queue.Count > 0)
                {
                    if (++work > MaxWorkItems)
                        throw new Exception("mirror worklist exceeded " + MaxWorkItems +
                                            " items - the dependency walk is not converging (cycle bug). " +
                                            "Refusing to loop forever in batchmode.");

                    string path = queue.Dequeue();
                    if (!processed.Add(path)) continue;

                    var touched = new List<string>();
                    RemapAsset(path, touched);
                    foreach (var m in touched) queue.Enqueue(m);   // processed set dedupes
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                SaveManifest();

                // -- PASS 3: VERIFY. No success marker unless this is clean ----------
                int afterTotal = 0, outsideTracked = 0;
                var stillExposed = new List<string>();
                foreach (var p in prefabs)
                {
                    var offenders = Rule.PackDependenciesOf(p);
                    afterTotal += offenders.Count;

                    int outside = CountOutsideVfx(p);
                    outsideTracked += outside;

                    Debug.Log(Tag + Short(p) + ": packDeps " + before[p] + " -> " + offenders.Count +
                              " (deps resolving outside " + Rule.VfxRoot + " at all: " + outside +
                              " - tracked art such as Assets/Lana Studio and URP package shaders is legitimate)");

                    if (offenders.Count > 0)
                    {
                        stillExposed.Add(Short(p) + " STILL reaches " + offenders.Count +
                                         " gitignored asset(s): " + string.Join(", ", offenders.ToArray()));
                    }
                }

                long bytes = MirrorBytes();
                summary.Append("prefabs=").Append(prefabs.Count)
                       .Append("; packDeps ").Append(beforeTotal).Append(" -> ").Append(afterTotal)
                       .Append("; mirrored ").Append(_mirrorBySource.Count).Append(" distinct source asset(s) [")
                       .Append(_created.Count).Append(" copied NEW, ").Append(_reused.Count)
                       .Append(" reused EXISTING (GUID preserved), ").Append(_recreated.Count)
                       .Append(" re-copied after a missing mirror]; bytes added under ").Append(SharedRoot)
                       .Append(" = ").Append(bytes).Append(" (").Append((bytes / 1048576.0).ToString("0.00"))
                       .Append(" MB); pack MonoBehaviours removed = ").Append(_stripped.Count)
                       .Append("; deps still resolving outside the VFX tree at all (tracked art, legitimate) = ")
                       .Append(outsideTracked);

                foreach (var s in _stripped) Debug.LogWarning(Tag + "STRIPPED - " + s);

                if (stillExposed.Count > 0)
                {
                    foreach (var s in stillExposed) Debug.LogError(Tag + "EXPOSED - " + s);
                    _errors.Add(stillExposed.Count + " prefab(s) still reach gitignored art after the remap: " +
                                string.Join(" | ", stillExposed.ToArray()));
                }

                if (_errors.Count > 0)
                    throw new Exception(_errors.Count + " error(s): " + string.Join(" | ", _errors.ToArray()));

                Debug.Log(Tag + "DONE. " + summary);
                Debug.Log(MarkerOk + " - " + summary);
            }
            catch (Exception e)
            {
                Debug.LogError(Tag + "FAILED: " + e.Message + "\n" + e.StackTrace);
                Debug.LogError(MarkerFail + " - " + e.Message + " || progress: " + summary);
            }
        }

        // =====================================================================
        //  PASS 1 - strip pack CODE (a .cs cannot be mirrored)
        // =====================================================================

        /// <summary>
        /// Removes every MonoBehaviour on a prefab whose SCRIPT asset lives in a
        /// gitignored pack. See the header for why removal (not mirroring) is the only
        /// correct move: a duplicated .cs collides in Assembly-CSharp, and these
        /// particular demo components are actively harmful inside a pooled VFX prefab.
        /// Nothing is guessed - the script's asset path is read off the real MonoScript.
        /// </summary>
        private static void StripPackScripts(string prefabPath)
        {
            GameObject contents = null;
            try
            {
                contents = PrefabUtility.LoadPrefabContents(prefabPath);
                var kill = new List<MonoBehaviour>();
                int missingScripts = 0;

                foreach (var mb in contents.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) { missingScripts++; continue; }

                    var script = MonoScript.FromMonoBehaviour(mb);
                    if (script == null) continue;

                    string scriptPath = AssetDatabase.GetAssetPath(script);
                    if (!Rule.IsInGitignoredArtRoot(scriptPath)) continue;

                    _stripped.Add(Short(prefabPath) + ": removed '" + mb.GetType().Name + "' on '" +
                                  PathOf(mb.transform, contents.transform) + "' (script '" + scriptPath +
                                  "' is gitignored -> MISSING SCRIPT on any fresh clone; a .cs cannot be " +
                                  "mirrored without colliding in Assembly-CSharp, and this demo component " +
                                  "does not belong in a pooled VFX prefab)");
                    kill.Add(mb);
                }

                if (missingScripts > 0)
                {
                    _errors.Add(Short(prefabPath) + " already carries " + missingScripts +
                                " MISSING-SCRIPT component(s) - their script GUID cannot be read, so this " +
                                "builder cannot tell whether they point into a pack. Resolve them by hand.");
                }

                if (kill.Count == 0) return;

                foreach (var mb in kill)
                {
                    if (mb != null) UnityEngine.Object.DestroyImmediate(mb, true);
                }
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
            }
            catch (Exception e)
            {
                _errors.Add("strip pass on '" + prefabPath + "' threw " + e.GetType().Name + ": " + e.Message);
            }
            finally
            {
                if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // =====================================================================
        //  PASS 2 - mirror + remap
        // =====================================================================

        /// <summary>
        /// Repoints every reference in one asset that resolves into a gitignored pack,
        /// mirroring the target on demand. Adds every mirror it pointed at to
        /// <paramref name="touched"/> so the caller can walk the mirror's OWN references
        /// - that recursion is the whole point (a mirrored material whose texture still
        /// points into the pack has solved nothing).
        /// </summary>
        private static void RemapAsset(string path, List<string> touched)
        {
            if (!IsEditable(path)) return;   // leaf / importer-owned; PASS 3 verifies it

            if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                GameObject contents = null;
                try
                {
                    contents = PrefabUtility.LoadPrefabContents(path);
                    bool dirty = false;
                    foreach (var c in contents.GetComponentsInChildren<Component>(true))
                    {
                        if (c == null) continue;
                        dirty |= RemapObject(c, path, touched);
                    }
                    if (dirty)
                    {
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    }
                }
                catch (Exception e)
                {
                    _errors.Add("remap of prefab '" + path + "' threw " + e.GetType().Name + ": " + e.Message);
                }
                finally
                {
                    if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
                }
                return;
            }

            try
            {
                // Sub-assets matter: an AnimatorController holds its states as sub-objects,
                // and it is the STATE that references the .anim clip.
                var objects = AssetDatabase.LoadAllAssetsAtPath(path);
                bool dirty = false;
                foreach (var o in objects)
                {
                    if (o == null) continue;
                    if (RemapObject(o, path, touched))
                    {
                        EditorUtility.SetDirty(o);
                        dirty = true;
                    }
                }
                if (dirty) AssetDatabase.SaveAssets();
            }
            catch (Exception e)
            {
                _errors.Add("remap of asset '" + path + "' threw " + e.GetType().Name + ": " + e.Message);
            }
        }

        /// <summary>
        /// Walks every serialized object-reference on one object and repoints the ones
        /// that resolve into a gitignored pack. Collects first and writes second so the
        /// iterator is never mutated underneath itself.
        /// </summary>
        private static bool RemapObject(UnityEngine.Object target, string ownerPath, List<string> touched)
        {
            var so = new SerializedObject(target);
            var pending = new List<KeyValuePair<string, UnityEngine.Object>>();

            var it = so.GetIterator();
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;

                var referenced = it.objectReferenceValue;
                if (referenced == null) continue;

                string srcPath = AssetDatabase.GetAssetPath(referenced);
                if (!Rule.IsInGitignoredArtRoot(srcPath)) continue;

                if (IsUntouchable(it.propertyPath))
                {
                    _errors.Add(Short(ownerPath) + " -> '" + it.propertyPath + "' points at '" + srcPath +
                                "' inside a gitignored pack, and that property is structural (script binding / " +
                                "nested-prefab linkage) so it CANNOT be repointed safely. This asset kind is not " +
                                "self-containable by copy; it needs authoring, not a mirror.");
                    continue;
                }

                string mirrorPath = GetOrCreateMirror(srcPath);
                if (mirrorPath == null) continue;                  // error already recorded
                touched.Add(mirrorPath);

                var replacement = FindCounterpart(referenced, mirrorPath);
                if (replacement == null)
                {
                    _errors.Add(Short(ownerPath) + " -> '" + it.propertyPath + "': mirrored '" + srcPath +
                                "' to '" + mirrorPath + "' but could not find the matching sub-object (" +
                                referenced.GetType().Name + " '" + referenced.name + "') inside the mirror. " +
                                "The reference was left pointing at the pack rather than pointed at the wrong object.");
                    continue;
                }

                pending.Add(new KeyValuePair<string, UnityEngine.Object>(it.propertyPath, replacement));
            }

            if (pending.Count == 0) return false;

            foreach (var kv in pending)
            {
                var prop = so.FindProperty(kv.Key);
                if (prop == null)
                {
                    _errors.Add(Short(ownerPath) + ": property '" + kv.Key + "' vanished between the walk and " +
                                "the write - the reference was NOT repointed.");
                    continue;
                }
                prop.objectReferenceValue = kv.Value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // =====================================================================
        //  Mirror creation - dedupe by SOURCE PATH, reuse to preserve GUIDs
        // =====================================================================

        /// <summary>
        /// The tracked mirror of one pack asset, created on first demand and REUSED ever
        /// after. Reuse is what preserves the mirror's .meta GUID across runs, which is
        /// what keeps every prefab reference into it valid - so a re-run mirrors nothing
        /// new and re-points nothing. Returns null (and records an error) on failure.
        /// </summary>
        private static string GetOrCreateMirror(string srcPath)
        {
            string existing;
            if (_mirrorBySource.TryGetValue(srcPath, out existing))
            {
                if (!File.Exists(AbsoluteOf(existing)))
                {
                    // The manifest knows this mirror but the file is gone (deleted by hand,
                    // or a partial checkout). Re-copy to the SAME path so the map stays
                    // stable, and say plainly that its GUID is new - every reference that
                    // pointed at the old GUID is now dangling and must be re-run.
                    if (!CopyInto(srcPath, existing)) return null;
                    _recreated.Add(existing);
                    Debug.LogWarning(Tag + "mirror '" + existing + "' was MISSING and has been re-copied from '" +
                                     srcPath + "'. Its GUID is NEW, so any reference that still points at the old " +
                                     "GUID is dangling - re-run this builder until it reports a clean verify.");
                }
                return existing;
            }

            string destPath = SharedRoot + BucketFor(srcPath) + "/" + Path.GetFileName(srcPath);

            string otherSource;
            if (_sourceByMirror.TryGetValue(destPath, out otherSource) &&
                !string.Equals(otherSource, srcPath, StringComparison.OrdinalIgnoreCase))
            {
                _errors.Add("MIRROR NAME COLLISION: '" + srcPath + "' and '" + otherSource +
                            "' both want the mirror path '" + destPath + "'. Refusing to let one silently " +
                            "reuse the other's art. Give one of them a disambiguating destination name in " +
                            "this builder before re-running.");
                return null;
            }

            if (File.Exists(AbsoluteOf(destPath)))
            {
                // Present on disk but absent from the manifest: adopt it rather than
                // overwrite it. Adopting keeps the existing GUID (idempotence); a
                // re-copy would mint a new one and dangle every reference into it.
                _reused.Add(destPath);
            }
            else
            {
                if (!CopyInto(srcPath, destPath)) return null;
                _created.Add(destPath);
                if (destPath.EndsWith(".shader", StringComparison.OrdinalIgnoreCase))
                    RenameMirroredShader(destPath);
            }

            _mirrorBySource[srcPath] = destPath;
            _sourceByMirror[destPath] = srcPath;
            return destPath;
        }

        private static bool CopyInto(string srcPath, string destPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(srcPath) == null)
            {
                _errors.Add("source asset '" + srcPath + "' would not load - the pack is not imported on this " +
                            "machine, so its art CANNOT be mirrored here. Import the pack and re-run; nothing " +
                            "was faked.");
                return false;
            }

            EnsureFolder(DirectoryOf(destPath));
            if (!AssetDatabase.CopyAsset(srcPath, destPath))
            {
                _errors.Add("AssetDatabase.CopyAsset('" + srcPath + "' -> '" + destPath + "') returned false.");
                return false;
            }
            AssetDatabase.ImportAsset(destPath, ImportAssetOptions.ForceUpdate);
            return true;
        }

        /// <summary>
        /// The object INSIDE the mirror that corresponds to <paramref name="original"/>.
        /// CopyAsset preserves an asset's internal local file IDs, so the local ID is an
        /// EXACT match - which matters because these references are often to a sub-object,
        /// not the main asset (the Lights module points at a Light COMPONENT inside
        /// ParticlesLight.prefab; a ParticleSystemRenderer points at a Mesh inside an FBX).
        /// Falls back to type+name, then to the main asset, then gives up loudly.
        /// </summary>
        private static UnityEngine.Object FindCounterpart(UnityEngine.Object original, string mirrorPath)
        {
            var candidates = AssetDatabase.LoadAllAssetsAtPath(mirrorPath);
            var originalType = original.GetType();

            string guid;
            long localId;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(original, out guid, out localId))
            {
                foreach (var c in candidates)
                {
                    if (c == null || c.GetType() != originalType) continue;
                    string g2;
                    long id2;
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(c, out g2, out id2) && id2 == localId)
                        return c;
                }
            }

            foreach (var c in candidates)
            {
                if (c != null && c.GetType() == originalType && c.name == original.name) return c;
            }

            var main = AssetDatabase.LoadMainAssetAtPath(mirrorPath);
            if (main != null && main.GetType() == originalType) return main;

            return null;
        }

        /// <summary>
        /// Pushes the mirrored shader's DECLARED name under a mirror prefix. Both files
        /// exist on this machine; two ShaderLab sources declaring the same Shader "..."
        /// name make Shader.Find ambiguous and log a duplicate-name warning. Materials
        /// bind their shader by GUID, so renaming costs nothing. ShaderLab is plain text,
        /// not YAML - this is the only text edit in the builder.
        /// </summary>
        private static void RenameMirroredShader(string shaderPath)
        {
            try
            {
                string abs = AbsoluteOf(shaderPath);
                string text = File.ReadAllText(abs);

                int decl = text.IndexOf("Shader", StringComparison.Ordinal);
                if (decl < 0) return;
                int open = text.IndexOf('"', decl);
                if (open < 0) return;
                int close = text.IndexOf('"', open + 1);
                if (close < 0) return;

                string name = text.Substring(open + 1, close - open - 1);
                if (name.StartsWith(MirrorShaderPrefix, StringComparison.Ordinal)) return;

                text = text.Substring(0, open + 1) + MirrorShaderPrefix + name + text.Substring(close);
                File.WriteAllText(abs, text);
                AssetDatabase.ImportAsset(shaderPath, ImportAssetOptions.ForceUpdate);
                Debug.Log(Tag + "mirrored shader '" + shaderPath + "' renamed '" + name + "' -> '" +
                          MirrorShaderPrefix + name + "' so it cannot collide with the still-present pack shader.");
            }
            catch (Exception e)
            {
                _errors.Add("could not rename mirrored shader '" + shaderPath + "': " +
                            e.GetType().Name + ": " + e.Message);
            }
        }

        // =====================================================================
        //  Manifest (source -> mirror). Assets/Editor never ships.
        // =====================================================================

        [Serializable]
        private class MirrorEntry
        {
            public string source;
            public string mirror;
        }

        [Serializable]
        private class MirrorManifest
        {
            public string note;
            public MirrorEntry[] entries;
        }

        private static void LoadManifest()
        {
            string abs = AbsoluteOf(ManifestPath);
            if (!File.Exists(abs)) return;

            try
            {
                var manifest = JsonUtility.FromJson<MirrorManifest>(File.ReadAllText(abs));
                if (manifest == null || manifest.entries == null) return;

                foreach (var e in manifest.entries)
                {
                    if (e == null || string.IsNullOrEmpty(e.source) || string.IsNullOrEmpty(e.mirror)) continue;
                    _mirrorBySource[e.source] = e.mirror;
                    _sourceByMirror[e.mirror] = e.source;
                }
                Debug.Log(Tag + "manifest loaded: " + _mirrorBySource.Count + " known source->mirror pair(s).");
            }
            catch (Exception e)
            {
                // A corrupt manifest must never silently become "no mirrors known" - that
                // would re-copy everything with fresh GUIDs and dangle every reference.
                _errors.Add("manifest '" + ManifestPath + "' could not be read (" + e.GetType().Name + ": " +
                            e.Message + "). Refusing to proceed with an empty map: that would re-copy every " +
                            "mirror with a NEW GUID and dangle every reference into it.");
            }
        }

        private static void SaveManifest()
        {
            try
            {
                var sources = new List<string>(_mirrorBySource.Keys);
                sources.Sort(StringComparer.Ordinal);   // deterministic file, reviewable diffs

                var manifest = new MirrorManifest
                {
                    note = "source->mirror map for VfxResourceArtMirror. REUSING a mirror is what preserves " +
                           "its GUID across runs; do not delete entries by hand.",
                    entries = new MirrorEntry[sources.Count],
                };
                for (int i = 0; i < sources.Count; i++)
                    manifest.entries[i] = new MirrorEntry { source = sources[i], mirror = _mirrorBySource[sources[i]] };

                EnsureFolder(DirectoryOf(ManifestPath));
                File.WriteAllText(AbsoluteOf(ManifestPath), JsonUtility.ToJson(manifest, true));
                AssetDatabase.ImportAsset(ManifestPath, ImportAssetOptions.ForceUpdate);
            }
            catch (Exception e)
            {
                _errors.Add("could not write the manifest '" + ManifestPath + "': " +
                            e.GetType().Name + ": " + e.Message);
            }
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        /// <summary>Where a mirrored asset lands, by kind. Only buckets we actually use.</summary>
        private static string BucketFor(string assetPath)
        {
            string ext = Path.GetExtension(assetPath).ToLowerInvariant();
            switch (ext)
            {
                case ".mat":
                    return "Materials";
                case ".png": case ".jpg": case ".jpeg": case ".tga": case ".tif": case ".tiff":
                case ".psd": case ".exr": case ".bmp": case ".hdr": case ".gif": case ".cubemap":
                    return "Textures";
                case ".shader": case ".shadergraph": case ".shadersubgraph":
                case ".cginc": case ".hlsl": case ".glslinc": case ".compute":
                    return "Shaders";
                case ".fbx": case ".obj": case ".dae": case ".blend": case ".mesh":
                    return "Models";
                case ".anim": case ".controller": case ".overridecontroller": case ".mask":
                    return "Animation";
                case ".prefab":
                    return "Prefabs";
                default:
                    return "Misc";
            }
        }

        private static bool IsEditable(string assetPath)
        {
            string ext = Path.GetExtension(assetPath).ToLowerInvariant();
            for (int i = 0; i < EditableExtensions.Length; i++)
                if (ext == EditableExtensions[i]) return true;
            return false;
        }

        private static bool IsUntouchable(string propertyPath)
        {
            for (int i = 0; i < UntouchableProperties.Length; i++)
            {
                if (propertyPath.EndsWith(UntouchableProperties[i], StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>Total bytes the mirror added under the shared root (source files + .meta).</summary>
        private static long MirrorBytes()
        {
            string abs = AbsoluteOf(SharedRoot.TrimEnd('/'));
            if (!Directory.Exists(abs)) return 0L;

            long total = 0L;
            foreach (var f in Directory.GetFiles(abs, "*", SearchOption.AllDirectories))
            {
                // No silent swallow (CLAUDE.md section 12): an unreadable file skews the
                // size report the orchestrator judges the build-size cost with, so say so.
                try { total += new FileInfo(f).Length; }
                catch (IOException e) { Debug.LogWarning(Tag + "could not size '" + f + "': " + e.Message); }
            }
            return total;
        }

        /// <summary>How many recursive deps resolve outside the curated VFX tree at all (tracked art included).</summary>
        private static int CountOutsideVfx(string assetPath)
        {
            int n = 0;
            var deps = AssetDatabase.GetDependencies(assetPath, true);
            foreach (var d in deps)
            {
                if (string.Equals(d, assetPath, StringComparison.OrdinalIgnoreCase)) continue;
                if (!Rule.IsInVfxRoot(d)) n++;
            }
            return n;
        }

        private static string DirectoryOf(string assetPath)
        {
            int slash = assetPath.LastIndexOf('/');
            return slash < 0 ? assetPath : assetPath.Substring(0, slash);
        }

        private static string Short(string assetPath)
        {
            return assetPath.StartsWith(Rule.VfxRoot, StringComparison.OrdinalIgnoreCase)
                ? assetPath.Substring(Rule.VfxRoot.Length)
                : assetPath;
        }

        private static string AbsoluteOf(string assetPath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void EnsureFolder(string dir)
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

        /// <summary>Hierarchy path from the prefab root, for a readable strip report.</summary>
        private static string PathOf(Transform t, Transform root)
        {
            var stack = new List<string>();
            var cur = t;
            while (cur != null && cur != root)
            {
                stack.Add(cur.name);
                cur = cur.parent;
            }
            stack.Reverse();
            return stack.Count == 0 ? "<root>" : string.Join("/", stack.ToArray());
        }
    }
}
