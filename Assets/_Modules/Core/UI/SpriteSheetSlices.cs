// =============================================================================
// SpriteSheetSlices - name-keyed slices of an owner-authored sprite sheet (WO-1359).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI
//
// THE PROBLEM IT FILLS: the owner hands over finished art as ONE sheet - five
// action-bar emblems in a row. Unity's own multi-sprite slicing would bind the
// regions to the importer and to their grid POSITION, and the sheet's reading
// order is not guaranteed to match the order the bar draws its faces in. A
// position-keyed slice therefore swaps two faces the day a sheet is re-ordered,
// and both faces still look plausible, so it ships. (On the first sheet the bar
// order and the sheet order already disagreed: MANAGE and JOURNEY were
// transposed.)
//
// SO THE REGIONS ARE DATA, KEYED BY NAME. Beside the .png sits a .json manifest:
//
//   { "sheet": "<Resources path>",
//     "faces": { "build": { "x":0.02,"y":0.23,"width":0.19,"height":0.52 }, ... } }
//
// Rects are NORMALIZED 0..1 in Unity texture space (origin BOTTOM-LEFT), which is
// what lets them survive a maxTextureSize downscale untouched - the one thing a
// pixel rect cannot do. The manifest is DERIVED from the sheet's own alpha
// bounding boxes, not typed by hand; DeNelle.Editor's "Elarion/UI/Re-slice Action
// Bar Emblems" regenerates it from whatever sheet is on disk. Adopting a new sheet
// is: drop the .png, run that item. If the new sheet re-orders the emblems, the
// ONLY edit is the name order in that tool.
//
// NULL-SAFE CONTRACT (the RpgUiCatalog / ConceptIconResolver contract, verbatim):
// every miss - no manifest, no texture, no such face, a malformed rect - returns
// NULL and is traced, and every caller keeps the fallback it already had. A
// missing icon must never become a missing button.
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// Resolves "&lt;Resources sheet path&gt;#&lt;face name&gt;" to a <see cref="Sprite"/> cut from an
    /// owner-authored sheet, using the normalized rects in the sheet's sibling .json manifest.
    /// Null on any miss (caller keeps its fallback); never throws; cached per address.
    /// </summary>
    public static class SpriteSheetSlices
    {
        /// <summary>The separator between a sheet path and a face name in an icon address.</summary>
        public const char AddressSeparator = '#';

        [Serializable]
        private sealed class SliceRect
        {
            [JsonProperty("x")] public float X;
            [JsonProperty("y")] public float Y;
            [JsonProperty("width")] public float Width;
            [JsonProperty("height")] public float Height;
        }

        [Serializable]
        private sealed class SheetManifest
        {
            [JsonProperty("sheet")] public string Sheet;
            [JsonProperty("faces")] public Dictionary<string, SliceRect> Faces
                = new Dictionary<string, SliceRect>(StringComparer.OrdinalIgnoreCase);
        }

        // sheetPath -> manifest (null when there is none; the null is cached too, so a sheet
        // without a manifest is looked for once and not once per HUD rebuild).
        private static readonly Dictionary<string, SheetManifest> _manifests =
            new Dictionary<string, SheetManifest>(StringComparer.OrdinalIgnoreCase);
        // full "sheet#face" address -> sprite (null cached for the same reason).
        private static readonly Dictionary<string, Sprite> _slices =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        /// <summary>True when <paramref name="address"/> names a sheet face rather than a plain
        /// Resources sprite path (i.e. it carries the '#' separator).</summary>
        public static bool IsSheetAddress(string address)
        {
            return !string.IsNullOrEmpty(address) && address.IndexOf(AddressSeparator) > 0;
        }

        /// <summary>
        /// The sprite for "sheetPath#faceName", or NULL when the sheet, the manifest or that named
        /// face is absent - in which case the caller keeps whatever fallback it already had.
        /// </summary>
        public static Sprite Resolve(string address)
        {
            if (!IsSheetAddress(address)) return null;
            string key = address.Trim();
            Sprite cached;
            if (_slices.TryGetValue(key, out cached)) return cached;

            Sprite made = null;
            try { made = Build(key); }
            catch (Exception e)
            {
                FlowTrace.Throttle("Icon", "slice-throw:" + key, 30f,
                    "sheet slice '" + key + "' threw " + e.GetType().Name + " - caller keeps its fallback");
            }
            _slices[key] = made;
            return made;
        }

        private static Sprite Build(string address)
        {
            int cut = address.IndexOf(AddressSeparator);
            string sheetPath = address.Substring(0, cut).Trim();
            string faceName = address.Substring(cut + 1).Trim();
            if (sheetPath.Length == 0 || faceName.Length == 0) return null;

            var manifest = ManifestFor(sheetPath);
            if (manifest == null || manifest.Faces == null) return null;

            SliceRect r;
            if (!manifest.Faces.TryGetValue(faceName, out r) || r == null)
            {
                FlowTrace.Throttle("Icon", "slice-noface:" + address, 30f,
                    "sheet '" + sheetPath + "' has no face named '" + faceName + "' - the manifest " +
                    "is keyed BY NAME on purpose, so this is a naming mismatch, not an ordering one");
                return null;
            }
            if (r.Width <= 0f || r.Height <= 0f ||
                r.X < -0.001f || r.Y < -0.001f || r.X + r.Width > 1.001f || r.Y + r.Height > 1.001f)
            {
                FlowTrace.Throttle("Icon", "slice-badrect:" + address, 30f,
                    "face '" + faceName + "' has a rect outside the 0..1 normalized range - " +
                    "re-derive the manifest from the sheet rather than hand-editing it");
                return null;
            }

            var tex = Resources.Load<Texture2D>(sheetPath);
            if (tex == null)
            {
                FlowTrace.Throttle("Icon", "slice-nosheet:" + sheetPath, 30f,
                    "sheet texture '" + sheetPath + "' is not in Resources - caller keeps its fallback");
                return null;
            }

            // Normalized -> pixels against the LIVE texture size, so a maxTextureSize downscale
            // (or a re-export at another resolution) needs no manifest change at all.
            var px = new Rect(
                Mathf.Round(r.X * tex.width),
                Mathf.Round(r.Y * tex.height),
                Mathf.Round(r.Width * tex.width),
                Mathf.Round(r.Height * tex.height));
            px.width = Mathf.Min(px.width, tex.width - px.x);
            px.height = Mathf.Min(px.height, tex.height - px.y);
            if (px.width < 1f || px.height < 1f) return null;

            // Sprite.Create samples on the GPU - it does NOT require a readable texture, which is
            // why the sheet can stay compressed and non-readable.
            var sprite = Sprite.Create(tex, px, new Vector2(0.5f, 0.5f), 100f, 0,
                                       SpriteMeshType.FullRect);
            if (sprite != null) sprite.name = faceName;
            FlowTrace.Once("Icon", "slice-hit:" + address,
                "sliced '" + faceName + "' from " + sheetPath + " at " + px);
            return sprite;
        }

        private static SheetManifest ManifestFor(string sheetPath)
        {
            SheetManifest cached;
            if (_manifests.TryGetValue(sheetPath, out cached)) return cached;

            SheetManifest parsed = null;
            var asset = Resources.Load<TextAsset>(sheetPath);
            if (asset == null || string.IsNullOrEmpty(asset.text))
            {
                FlowTrace.Throttle("Icon", "slice-nomanifest:" + sheetPath, 30f,
                    "no slice manifest beside sheet '" + sheetPath + "' - run " +
                    "Elarion/UI/Re-slice Action Bar Emblems to derive one from its alpha");
            }
            else
            {
                try { parsed = JsonConvert.DeserializeObject<SheetManifest>(asset.text); }
                catch (Exception e)
                {
                    FlowTrace.Throttle("Icon", "slice-badmanifest:" + sheetPath, 30f,
                        "slice manifest for '" + sheetPath + "' did not parse (" + e.GetType().Name +
                        ") - caller keeps its fallback");
                }
            }
            _manifests[sheetPath] = parsed;
            return parsed;
        }

        /// <summary>Editor/regression hook: forget every cached manifest and slice so a fresh art
        /// drop is picked up without a domain reload.</summary>
        public static void ClearCache()
        {
            _manifests.Clear();
            _slices.Clear();
        }
    }
}
