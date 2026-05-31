using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Editor
{
    // WO-181: reflection bridge + model/material/strip utility helpers, split out of
    // VillageSceneBuilder.cs. Same partial class -- moves only, no logic change.
    public static partial class VillageSceneBuilder
    {
        private static void SetObjectField(SerializedObject so, string field, UnityEngine.Object value)
        {
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[VillageSceneBuilder] Serialized field '{field}' not found on " +
                                 $"{so.targetObject.GetType().Name} -- wiring skipped for that field.");
                return;
            }
            prop.objectReferenceValue = value;
        }

        // =====================================================================
        //  KayKit model loading
        // =====================================================================

        /// <summary>
        /// Loads a model GameObject at an asset path. Returns null (caller falls
        /// back to a placeholder) when the asset is missing. Tries the given
        /// path, then the same path with a ".prefab" extension.
        /// </summary>
        private static GameObject LoadModel(string assetPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (model != null) return model;

            string asPrefab = Path.ChangeExtension(assetPath, ".prefab")?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(asPrefab))
            {
                model = AssetDatabase.LoadAssetAtPath<GameObject>(asPrefab);
                if (model != null) return model;
            }
            return null;
        }

        /// <summary>
        /// Instantiates a loaded KayKit model. When <paramref name="model"/> is
        /// null a clearly-labelled placeholder cube is returned and the miss is
        /// logged + tallied.
        /// </summary>
        private static GameObject InstantiateModel(GameObject model, string assetLabel,
            string placeholderLabel)
        {
            if (model != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                if (instance != null)
                {
                    instance.name = model.name;
                    // The whole Hexagon pack shares one atlas — force the shared
                    // URP material on so instances render textured even when the
                    // FBX importer's material remap fails to resolve (the
                    // decoration/nature meshes; see unity-decisions.md 2026-05-19).
                    ForceHexMaterial(instance);
                    return instance;
                }
            }
            return MakePlaceholderCube($"{assetLabel} -> {placeholderLabel}");
        }

        /// <summary>
        /// Axis-aligned bounds of every mesh under <paramref name="go"/>, expressed
        /// in <paramref name="go"/>'s OWN local space — independent of any rotation
        /// on <paramref name="go"/> itself OR on its parents.
        ///
        /// <para>Why this exists. The naive measure used <c>Renderer.bounds.size</c>,
        /// which is a WORLD-space AABB: once the piece (or a parent) is rotated, that
        /// AABB no longer maps to the mesh's own X/Y/Z extents, so a "length along
        /// local X" reading was actually returning the piece's depth/thickness. We
        /// instead take each child MeshFilter's <c>sharedMesh.bounds</c> (true mesh
        /// space) and push its 8 corners through
        /// <c>go.worldToLocalMatrix * meshFilter.transform.localToWorldMatrix</c>.
        /// That round-trip cancels every rotation between the mesh and
        /// <paramref name="go"/>, leaving extents measured along
        /// <paramref name="go"/>'s local axes — exactly the axes <c>localScale</c>
        /// stretches.</para>
        /// </summary>
        private static bool TryMeasureLocalBounds(GameObject go, out Bounds local)
        {
            local = default;
            if (go == null) return false;
            bool any = false;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf == null || mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                Matrix4x4 m = go.transform.worldToLocalMatrix *
                              mf.transform.localToWorldMatrix;
                Vector3 c = mb.center, e = mb.extents;
                for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 corner = m.MultiplyPoint3x4(
                        c + new Vector3(sx * e.x, sy * e.y, sz * e.z));
                    if (!any) { local = new Bounds(corner, Vector3.zero); any = true; }
                    else local.Encapsulate(corner);
                }
            }
            // Skinned meshes (rare for static dressing) — fall back to renderer
            // localBounds, which is already mesh-space for a SkinnedMeshRenderer.
            if (!any)
            {
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (smr == null) continue;
                    Bounds lb = smr.localBounds;
                    if (!any) { local = lb; any = true; }
                    else local.Encapsulate(lb);
                }
            }
            return any;
        }

        /// <summary>
        /// Scales a wall/fence visual so it spans exactly <paramref name="runLength"/>
        /// along the run direction. The KayKit straight modules are a fixed length;
        /// this stretches whichever of the visual's HORIZONTAL local axes is the long
        /// one (the run axis) up to <paramref name="runLength"/>, leaving height and
        /// thickness untouched. Auto-detecting the long axis makes the fit correct
        /// regardless of the piece's native orientation or its yaw-fix rotation —
        /// so straights tile flush against the native-scale corner pieces.
        /// </summary>
        private static void FitWallVisualToRun(GameObject visual, float runLength)
        {
            if (visual == null || runLength <= 0.01f) return;
            if (!TryMeasureLocalBounds(visual, out var lb)) return;

            var s = visual.transform.localScale;
            // The run axis is the longer of the two horizontal mesh extents.
            if (lb.size.x >= lb.size.z)
            {
                if (lb.size.x > 0.01f) s.x *= runLength / lb.size.x;
            }
            else
            {
                if (lb.size.z > 0.01f) s.z *= runLength / lb.size.z;
            }
            visual.transform.localScale = s;
        }

        /// <summary>
        /// WO-126: some polyperfect perimeter prefabs (notably Gate_Medieval) ship with a
        /// material slot left UNASSIGNED, so that submesh renders as Unity's magenta error
        /// material — even though every real polyperfect material is already URP/Lit (the URP
        /// fixer converts 0). Replace any null / error-shader slot with a shared stone
        /// fallback so the gate arch reads as stone, not pink. Valid materials are untouched.
        /// </summary>
        private static Material _perimeterStoneFallback;
        private static void RepairPerimeterMaterials(GameObject go)
        {
            if (go == null) return;
            if (_perimeterStoneFallback == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) return;
                _perimeterStoneFallback = new Material(sh) { name = "PerimeterStoneFallback" };
                if (_perimeterStoneFallback.HasProperty("_BaseColor"))
                    _perimeterStoneFallback.SetColor("_BaseColor", new Color(0.55f, 0.53f, 0.49f));
            }
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    bool bad = m == null || m.shader == null || m.shader.name.Contains("InternalError");
                    if (bad)
                    {
                        Debug.Log($"[VillageSceneBuilder] WO-126 perimeter material repair: '{go.name}/{r.name}' slot {i} " +
                                  $"was {(m == null ? "NULL" : (m.shader == null ? "null-shader" : m.shader.name))} -> stone fallback.");
                        mats[i] = _perimeterStoneFallback;
                        changed = true;
                    }
                }
                if (changed) r.sharedMaterials = mats;
            }

            // WO-126: the gate's material is a valid grey URP/Lit (GATE-DIAG confirmed
            // M_21_Grey_Light_LPUP, baseColor ~0.65, no texture) — the "purple gate" is the
            // DUSK AMBIENT glowing on that bright flat face, not a material bug. Tint the grey
            // stone slots to a warmer, dimmer masonry tone (matching the curtain walls) so the
            // gate reads as stone under the dusk light. Wood/other slots are left alone, and we
            // use an instance material (not the shared asset) so nothing else in the scene shifts.
            if (go.name.Contains("Gate"))
            {
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.sharedMaterials;
                    bool ch = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] != null && mats[i].name.Contains("Grey"))
                        {
                            var tint = new Material(mats[i]);
                            if (tint.HasProperty("_BaseColor")) tint.SetColor("_BaseColor", new Color(0.46f, 0.43f, 0.39f));
                            mats[i] = tint;
                            ch = true;
                        }
                    }
                    if (ch) r.sharedMaterials = mats;
                }
            }
        }

        /// <summary>
        /// Normalises a KayKit prop / dressing instance to a consistent, believable
        /// size. KayKit props are authored across several folders/packs at wildly
        /// different native mesh scales — a barrel, a haybale and a weapon rack do
        /// NOT share a unit, so dropping them all in at <c>localScale = 1</c> makes
        /// some read far too big and others too small next to each other and the
        /// buildings.
        ///
        /// <para>The fix: measure the instance's true mesh bounds (rotation-immune,
        /// via <see cref="TryMeasureLocalBounds"/>) and apply a UNIFORM scale that
        /// brings its largest extent — horizontal footprint or height, whichever
        /// dominates — to <paramref name="targetSize"/> world units. Every prop type
        /// is then sized to the same yardstick, so the village dressing reads
        /// coherently. The scale is clamped to a sane band so a freak mesh (or a
        /// placeholder cube) can't explode or vanish.</para>
        /// </summary>
        /// <param name="go">The prop instance to rescale (multiplies its current localScale).</param>
        /// <param name="targetSize">Desired largest world-space dimension, in metres.</param>
        private static void NormalizeProp(GameObject go, float targetSize)
        {
            if (go == null || targetSize <= 0.001f) return;
            if (!TryMeasureLocalBounds(go, out var lb)) return;

            // Largest of the three native extents under the prop's current scale.
            Vector3 cur = go.transform.localScale;
            float nativeMax = Mathf.Max(
                lb.size.x * Mathf.Abs(cur.x),
                Mathf.Max(lb.size.y * Mathf.Abs(cur.y), lb.size.z * Mathf.Abs(cur.z)));
            if (nativeMax < 0.0001f) return;

            float factor = targetSize / nativeMax;
            factor = Mathf.Clamp(factor, 0.05f, 40f); // guard freak meshes / placeholders
            go.transform.localScale = cur * factor;
        }

        /// <summary>
        /// Lifts/lowers <paramref name="go"/> on its local Y so the bottom of
        /// its combined renderer bounds lands at the parent's y. Use after
        /// <see cref="NormalizeProp"/> on any FBX whose pivot is off-floor
        /// (most Tripo exports). World-space bounds are read, then the offset
        /// is applied in local space so further parent transforms are respected.
        /// </summary>
        private static void SnapFeetToParent(GameObject go)
        {
            if (go == null) return;
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs == null || rs.Length == 0) return;
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            float parentY = go.transform.parent != null ? go.transform.parent.position.y : 0f;
            float footOffset = b.min.y - parentY;
            if (Mathf.Abs(footOffset) < 0.001f) return;
            go.transform.localPosition -= new Vector3(0f, footOffset, 0f);
        }

        // =====================================================================
        //  Reflection helpers
        // =====================================================================

        private static Component AddVillageComponent(GameObject go, string fullTypeName)
        {
            var type = FindType(fullTypeName);
            if (type == null)
            {
                Debug.LogError($"[VillageSceneBuilder] Type '{fullTypeName}' not found -- is the " +
                               "DeNelle.Village assembly compiled? Component skipped.");
                return null;
            }
            return go.AddComponent(type);
        }

        private static System.Collections.IEnumerable ReadEnumerable(Type type, string propName)
        {
            var prop = type.GetProperty(propName, BindingFlags.Public | BindingFlags.Static);
            if (prop == null)
            {
                Debug.LogError($"[VillageSceneBuilder] Static property '{propName}' not found on {type.Name}.");
                return null;
            }
            return prop.GetValue(null) as System.Collections.IEnumerable;
        }

        private static object GetMember(object instance, string name)
        {
            var t = instance.GetType();
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (f != null) return f.GetValue(instance);
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p != null) return p.GetValue(instance);
            Debug.LogWarning($"[VillageSceneBuilder] Member '{name}' not found on {t.Name}.");
            return null;
        }

        private static void InvokeConfigure(Component target, string method, params object[] args)
        {
            if (target == null) return;
            var t = target.GetType();
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (m.Name != method) continue;
                var ps = m.GetParameters();
                if (ps.Length != args.Length) continue;
                try
                {
                    var coerced = new object[args.Length];
                    for (int i = 0; i < args.Length; i++)
                        coerced[i] = CoerceArg(args[i], ps[i].ParameterType);
                    m.Invoke(target, coerced);
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[VillageSceneBuilder] {t.Name}.{method}() invoke failed: {e.Message}");
                    return;
                }
            }
            Debug.LogWarning($"[VillageSceneBuilder] No '{method}' overload with {args.Length} arg(s) on {t.Name}.");
        }

        private static object CoerceArg(object value, Type targetType)
        {
            if (value == null) return null;
            if (targetType.IsInstanceOfType(value)) return value;
            if (targetType.IsEnum) return Enum.ToObject(targetType, value);
            if (typeof(IConvertible).IsAssignableFrom(targetType) && value is IConvertible)
                return Convert.ChangeType(value, targetType);
            return value;
        }

        private static void RegisterWith(Component controller, string method, params object[] args)
        {
            if (controller == null) return;
            InvokeConfigure(controller, method, args);
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }

        // =====================================================================
        //  Primitive / colour helpers
        // =====================================================================

        private static Transform NewChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static GameObject PrimitiveChild(Transform parent, string name,
            PrimitiveType prim, Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(prim);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            ApplyColor(go, color);
            return go;
        }

        private static GameObject MakePlaceholderCube(string label)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"[PLACEHOLDER] {label}";
            // Force a neutral-gray URP material so the placeholder doesn't render
            // as URP's magenta "missing material" sphere/cube. Also drop the
            // collider — placeholder dressing must not block pathing / picks.
            ApplyColor(cube, new Color(0.65f, 0.65f, 0.65f));
            var col = cube.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
            NotePlaceholder(label);
            return cube;
        }

        private static void NotePlaceholder(string label)
        {
            _placeholderCount++;
            if (_placeholders.Count < 24) _placeholders.Add(label);
            Debug.LogWarning($"[VillageSceneBuilder] KayKit asset missing -- placeholder primitive used for: {label}");
        }

        /// <summary>Strips every collider from a model instance (fence / prop dressing).</summary>
        private static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>())
                UnityEngine.Object.DestroyImmediate(c);
        }

        /// <summary>
        /// Strips every Rigidbody from a model instance. Imported meshes from
        /// third-party packs occasionally include a default Rigidbody on the
        /// root — combined with our hero collider that meant the hero fell
        /// through the village floor (gravity applied + no ground collision).
        /// </summary>
        private static void StripRigidbodies(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Rigidbody>())
                UnityEngine.Object.DestroyImmediate(r);
        }
    }
}
