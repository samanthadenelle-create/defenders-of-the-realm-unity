// =============================================================================
// GearPropRendersRegression [gear-prop-renders]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// WHY THIS ORACLE EXISTS (owner report 2026-08-18, build 2026.08.19.331306:
// "shield is missing and sword is now wrong", Grom Lv1, fresh tutorial):
//
//   [Flow:Equip] parent-scale compensate: parent='SheatheSocket_Back'
//                lossy=(1.666,1.666,1.666) authored=1 -> worldBounds=(0, 0, 0)
//
// A held prop that resolves to worldBounds=(0,0,0) renders NOTHING. The player sees
// an empty hand / an empty back. Every gate in the project was green for that build:
// the catalog row parsed, the address loaded, the attach ran, the seat was written and
// the render-verify passed — because every one of those checks asks "did the pipeline
// RUN", and not one asks "does the thing the pipeline produced have any VOLUME".
//
// THE INVARIANT, stated once: a weapons.json row that resolves to a prefab must resolve
// to a prefab that MEASURES SOMETHING — at least one renderer, on an active GameObject,
// carrying a non-null mesh with non-degenerate bounds. That is the cheapest possible
// statement of "the player can see the item", and it is checkable with no scene, no
// PlayMode and no device.
//
// AND THE ADDRESS ITSELF IS PART OF THE INVARIANT. Commit c072e5736 records the exact
// failure this half catches: weapons.json had pointed knight_shield_starter at
// "gear/weapon/ShieldWithItemLogic" while NOTHING published that address, so the
// Addressables load failed, EquipmentController fell back to the legacy shield_A mesh,
// and "the swap looked done and changed nothing". A catalog row naming an address no
// group publishes is a silent downgrade to the previous asset — never an error.
//
// WHAT IS A FAILURE vs WHAT IS A NOTE (deliberate, so this suite is not a new red):
//   FAIL  • an ADDRESSABLE gear row whose address is not registered in any Addressables
//           group, or is registered but DANGLING (guid resolves to no asset).
//   FAIL  • any resolved prefab (addressable or Resources) that MEASURES NOTHING.
//   NOTE  • a Resources-path row that resolves to no prop on disk. That is already
//           covered by the armed-hero path (EquipmentController falls back to a tinted
//           primitive, which DOES render), and ArmedHeroInvariantRegression owns the
//           "is the hero armed at all" question. Two suites asserting one fact is how
//           they drift apart.
//   NOTE  • a row with NO prefabPath at all — e.g. knight_starter, which carries no
//           prefabPath/loadVia/category in either weapons.json copy and is resolved
//           only by EquipmentController's hardcoded IdMap. Reported so the catalog/code
//           disagreement stays visible; ArmedHeroInvariantRegression already documents
//           the missing-category half of that same gap.
//
// "NO ART AUTHORED" AND "ART EXISTS BUT MEASURES NOTHING" ARE DIFFERENT FINDINGS and this
// suite must never merge them. A row that resolves to NOTHING on disk is a note (the
// tinted-primitive fallback still renders, and the hero is still visibly armed); a row
// that resolves to a REAL asset which cannot draw is a failure (the player sees an empty
// hand and no gate says so). Note also that the [icon-coverage] suite's GLYPH-ONLY rows
// (mage_oak, mage_arcane, mage_void, aegis_aetherstaff, aegis_hallowed_censer) are about
// UI ICON art, a DIFFERENT AXIS: all five resolve real held props on disk
// (Heroes/Props/Weapons/staff_A..D, hammer_A) and are expected GREEN here. A row can want
// an icon and have a perfectly good weapon mesh.
//
// MEASURED ON AN ASSET, NOT AN INSTANCE (corrected 2026-08-18 after this suite went red on
// all 96 rows at once): every property read below is one that is meaningful on an
// un-instantiated prefab — activeSelf up the parent chain, Renderer.enabled, and
// sharedMesh.bounds. activeInHierarchy and Renderer.bounds are NOT: both need a scene, and
// asking them of an asset returns false/empty for every asset equally. See Measure().
//
// SHARED AUTHORITY, NOT A SECOND COPY: the Resources path comes from the runtime's own
// EquipmentController.ResolveWeaponMeshResourcePath (public for exactly this reason) and
// the addressable predicate mirrors EquipmentController.LoadsViaAddressable's two
// clauses. Re-typing either here is how a gate and the game stop agreeing.
//
// Wire into the suite from DataRegression.RunAll (one line):
//   if (!GearPropRendersRegression.Run(out var gearPropReason)) failures.Add(gearPropReason); else log.AppendLine("[gear-prop-renders] " + gearPropReason);
// =============================================================================
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class GearPropRendersRegression
    {
        // Mirrors EquipmentController.LoadsViaAddressable (both clauses, same order).
        private static bool LoadsViaAddressable(string prefabPath, string loadVia)
        {
            if (!string.IsNullOrEmpty(loadVia) &&
                loadVia.Equals("addressable", StringComparison.OrdinalIgnoreCase))
                return true;
            return !string.IsNullOrEmpty(prefabPath) &&
                   prefabPath.StartsWith("gear/", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Why a prefab does or does not measure anything. The three failing shapes are kept
        /// DISTINCT because they have three different fixes — collapsing them to "invisible"
        /// is what made the device line ("worldBounds=(0,0,0)") unactionable.
        /// </summary>
        private enum Verdict { Renders, NoRenderer, AllInactive, AllDisabled, NoMesh, DegenerateMesh }

        // ⛔ THE MISTAKE THIS HELPER EXISTS TO PREVENT, RECORDED BECAUSE IT COST A RED RUN
        // (2026-08-18). The first version of Measure asked `r.gameObject.activeInHierarchy`. On an
        // un-instantiated PREFAB ASSET that is ALWAYS false — the asset belongs to no scene, so
        // "active in hierarchy" has no hierarchy to be active in. The suite therefore failed all 96
        // catalog rows with the identical signature `active=0 withMesh=0 extent=0`, which is not 96
        // broken props: it is one broken measurement, run 96 times. The tell was that the failure
        // count EQUALLED the row count.
        //
        // On an asset the valid question is `activeSelf` up the object's own parent chain (each
        // link is a serialized field, meaningful with or without a scene). Same for the renderer:
        // `Renderer.enabled` is serialized and readable; `Renderer.bounds` is WORLD-space and is
        // meaningless on an asset, so the volume below comes from `sharedMesh.bounds` — the mesh's
        // OWN space — scaled by the prefab's authored transform chain.
        private static bool ActiveWithinAsset(Transform t, Transform root)
        {
            for (Transform cur = t; cur != null; cur = cur.parent)
            {
                if (!cur.gameObject.activeSelf) return false;
                if (cur == root) break;
            }
            return true;
        }

        private static Verdict Measure(GameObject prefab, out string detail)
        {
            detail = "";
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                detail = "renderers=0";
                return Verdict.NoRenderer;
            }

            Transform root = prefab.transform;
            int total = 0, inactive = 0, disabled = 0, withMesh = 0;
            float bestVolume = 0f;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                total++;
                if (!ActiveWithinAsset(r.transform, root)) { inactive++; continue; }
                if (!r.enabled) { disabled++; continue; }

                Mesh mesh = null;
                if (r is SkinnedMeshRenderer smr) mesh = smr.sharedMesh;
                else
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null) mesh = mf.sharedMesh;
                }
                if (mesh == null) continue;
                withMesh++;

                // Bounds in the MESH's own space times the authored transform chain: enough to
                // separate "a real volume" from "a point". The runtime world size is the seat's
                // job (SeatNative normalizes the longest axis to heldLength); this only asks the
                // one question the device line could not answer — is there anything to see at all?
                Vector3 s = mesh.bounds.size;
                Vector3 ls = r.transform.lossyScale;
                float vol = Mathf.Abs(s.x * ls.x) + Mathf.Abs(s.y * ls.y) + Mathf.Abs(s.z * ls.z);
                if (vol > bestVolume) bestVolume = vol;
            }

            int drawable = total - inactive - disabled;
            detail = $"renderers={total} inactive={inactive} disabled={disabled} " +
                     $"withMesh={withMesh} meshExtent={bestVolume:0.####}";
            if (total == 0) return Verdict.NoRenderer;
            if (drawable == 0) return inactive >= disabled ? Verdict.AllInactive : Verdict.AllDisabled;
            if (withMesh == 0) return Verdict.NoMesh;
            if (bestVolume <= 1e-5f) return Verdict.DegenerateMesh;
            return Verdict.Renders;
        }

        /// <summary>
        /// Proves every weapons.json row that names a prefab resolves to a prefab the player
        /// could actually SEE. Returns true (PASS) with a summary, or false + a reason naming
        /// each defect. Deterministic, self-contained, no scene / no PlayMode.
        /// </summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- GEAR PROP RENDERS (every catalog prop measures a real volume) ---");

            IReadOnlyList<WeaponDef> weapons;
            try { weapons = GearCatalog.AllWeapons(); }
            catch (Exception ex)
            {
                reason = "GEAR_PROP_RENDERS FAILED: GearCatalog.AllWeapons() threw — " + ex.Message;
                return false;
            }
            if (weapons == null || weapons.Count == 0)
            {
                reason = "GEAR_PROP_RENDERS FAILED: GearCatalog.AllWeapons() returned no rows — " +
                         "the weapons catalog did not load, so nothing below could be checked.";
                return false;
            }

            // --- The published address book (settings -> groups -> entries) ------------------
            var addressToGuid = new Dictionary<string, string>(StringComparer.Ordinal);
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                foreach (var group in settings.groups)
                {
                    if (group == null) continue;
                    foreach (var entry in group.entries)
                    {
                        if (entry == null || string.IsNullOrEmpty(entry.address)) continue;
                        addressToGuid[entry.address] = entry.guid;
                    }
                }
            }

            int checkedAddressable = 0, checkedResources = 0, noPrefabPath = 0;

            foreach (var w in weapons)
            {
                if (w == null || string.IsNullOrEmpty(w.id)) continue;

                if (LoadsViaAddressable(w.prefabPath, w.loadVia))
                {
                    string address = w.prefabPath;
                    if (string.IsNullOrEmpty(address))
                    {
                        failures.Add($"'{w.id}' declares loadVia=addressable but has NO prefabPath — " +
                                     "there is no address to load, so the row can only ever fall back.");
                        continue;
                    }
                    if (settings == null)
                    {
                        notes.Add($"'{w.id}' -> '{address}': no AddressableAssetSettings object in this " +
                                  "project state, so the address book could not be read.");
                        continue;
                    }
                    if (!addressToGuid.TryGetValue(address, out string guid))
                    {
                        failures.Add($"'{w.id}' names address '{address}' which NO Addressables group " +
                                     "publishes. The load fails at runtime and the equip SILENTLY falls " +
                                     "back to the legacy mesh — the swap looks done and changes nothing " +
                                     "(this is the c072e5736 failure, verbatim).");
                        continue;
                    }
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = string.IsNullOrEmpty(path)
                        ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null)
                    {
                        failures.Add($"'{w.id}' address '{address}' is registered but DANGLING " +
                                     $"(guid={guid} resolves to no loadable prefab).");
                        continue;
                    }
                    checkedAddressable++;
                    var verdict = Measure(prefab, out string detail);
                    if (verdict != Verdict.Renders)
                        failures.Add($"'{w.id}' -> '{address}' ({path}) MEASURES NOTHING [{verdict}] — " +
                                     $"{detail}. On device this is the worldBounds=(0,0,0) line: the slot " +
                                     "attaches, seats and verifies, and the player sees no item.");
                    else
                        log.AppendLine($"  addressable '{w.id}' -> '{address}' OK ({detail})");
                    continue;
                }

                if (string.IsNullOrEmpty(w.prefabPath))
                {
                    // Resolved by EquipmentController's hardcoded IdMap, not by the catalog.
                    noPrefabPath++;
                }

                string resPath = null;
                try { resPath = EquipmentController.ResolveWeaponMeshResourcePath(w.id); }
                catch (Exception ex)
                {
                    failures.Add($"'{w.id}': ResolveWeaponMeshResourcePath threw — {ex.Message}");
                    continue;
                }
                if (string.IsNullOrEmpty(resPath))
                {
                    notes.Add($"'{w.id}': no Resources prop path resolved (falls back to the tinted " +
                              "primitive, which renders — armed-hero owns that assertion).");
                    continue;
                }
                var resPrefab = Resources.Load<GameObject>(resPath);
                if (resPrefab == null)
                {
                    notes.Add($"'{w.id}' -> Resources '{resPath}': not on disk (tinted-primitive " +
                              "fallback path; see ArmedHeroInvariantRegression).");
                    continue;
                }
                checkedResources++;
                var resVerdict = Measure(resPrefab, out string resDetail);
                if (resVerdict != Verdict.Renders)
                    failures.Add($"'{w.id}' -> Resources '{resPath}' MEASURES NOTHING [{resVerdict}] — " +
                                 $"{resDetail}. The hero holds an invisible prop.");
                else
                    log.AppendLine($"  resources '{w.id}' -> '{resPath}' OK ({resDetail})");
            }

            var summary = new StringBuilder();
            summary.Append("checked ").Append(checkedAddressable).Append(" addressable + ")
                   .Append(checkedResources).Append(" Resources gear prop(s) over ")
                   .Append(weapons.Count).Append(" catalog row(s); ")
                   .Append(noPrefabPath).Append(" row(s) carry no prefabPath and are resolved by " +
                                                "EquipmentController's IdMap instead of the catalog");
            if (notes.Count > 0) summary.Append("; ").Append(notes.Count).Append(" note(s)");

            if (failures.Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append("GEAR_PROP_RENDERS FAILED (").Append(failures.Count).Append("): ");
                for (int i = 0; i < failures.Count; i++)
                {
                    if (i > 0) sb.Append(" | ");
                    sb.Append(failures[i]);
                }
                sb.Append("  [").Append(summary).Append("]");
                reason = sb.ToString();
                return false;
            }

            reason = "GEAR_PROP_RENDERS_OK: " + summary + ". Every catalog-named gear prop resolves " +
                     "to a prefab with at least one active, meshed, non-degenerate renderer.";
            return true;
        }
    }
}
