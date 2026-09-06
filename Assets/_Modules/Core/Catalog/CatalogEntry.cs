// =============================================================================
// CatalogEntry — one def in the catalog. visualPrefabPath = LOOK (a real
// polyperfect prefab path), repo = BEHAVIOR. A Composite is a pre-snapped set
// of cell placements. This is the unit the build palette lists and the
// dispatcher builds. Pure data (DeNelle.Core).
// =============================================================================
using UnityEngine;

namespace DeNelle.Core.Catalog
{
    /// <summary>A cell inside a Composite: which cell-entry, at what relative offset + rotation.</summary>
    [System.Serializable]
    public sealed class CellPlacement
    {
        public string  cellEntryId;
        public Vector3 offset;
        public float   yRotation;   // 90° steps

        public CellPlacement() { }
        public CellPlacement(string cellEntryId, Vector3 offset, float yRotation)
        {
            this.cellEntryId = cellEntryId;
            this.offset = offset;
            this.yRotation = yRotation;
        }
    }

    [System.Serializable]
    public sealed class CatalogEntry
    {
        public string      id;
        public string      displayName;
        /// <summary>
        /// WO-1081 - the one player-facing sentence saying what this building does.
        /// Authored data; absent/blank falls back to the per-type sentence.
        /// </summary>
        public string      description;
        public CatalogType type;
        public EntryKind   kind = EntryKind.Cell;

        /// <summary>
        /// WHAT THIS BUILDING IS — an open, data-authored role string (WO-1161).
        /// Resolve it through <see cref="StructureRoles"/>:
        /// <c>StructureRoles.By[StructureRole.Armorer].DisplayName</c>.
        ///
        /// <para>⛔ THE `id` IS AN OPAQUE SAVE KEY AND THIS FIELD EXISTS SO IT CAN STAY ONE.
        /// `everBuiltStructureIds`, BaseLayout records, baked scenes, vendors.json and
        /// dialogues.json all join on `id`, and the game is LIVE on the Solana dApp Store —
        /// renaming `forge` orphans every existing player's building. The id never moves;
        /// the ROLE carries the meaning, and the DISPLAY NAME is free to change.</para>
        ///
        /// <para>⛔ OPEN VOCABULARY, ON PURPOSE (owner 2026-08-23: "the idea is staying
        /// fluid" / "if we add a building we do not want to have to manually code it").
        /// Any string is legal. <see cref="StructureRole"/> only names the few roles CODE
        /// branches on; a brand-new role resolves with no code change at all.</para>
        ///
        /// <para>⛔ FUNCTION IS THE AUTHORITY (owner: "which sells weapons, that is the
        /// weaponsmith use the JSON data") — assign the role from what the row DOES in
        /// vendors.json, NEVER from the word currently printed on its tile. Ignoring that
        /// is how `forge` came to sell weapons while displaying "Armorer".</para>
        ///
        /// Absent/empty = unroled, which is exactly how every row behaved before this
        /// field existed. Nothing regresses by leaving it unset.
        /// </summary>
        public string      role;

        /// <summary>
        /// WO-2005 — the Manage &gt; BUILD filter chips this row belongs to. **DATA IS THE
        /// AUTHORITY** (Manage redesign canon §3 / owner ruling 5): the UI may NOT infer a
        /// category from an id prefix, a class name, an asset name or the tab a row used to
        /// live on.
        ///
        /// <para>⛔ THIS IS NOT <see cref="role"/>, AND IT MUST NEVER BE FOLDED INTO IT. A role
        /// identifies exactly ONE row — <c>StructureRoles.Index</c> emits a
        /// <c>FlowTrace.Fail</c> collision the moment two rows claim the same one — so a role
        /// can never be a category. A filter is many-to-many by construction, which is why it
        /// is a separate array.</para>
        ///
        /// <para>Legal tokens are named (and validated) by <see cref="BuildFilter"/>;
        /// comparison is ordinal case-insensitive. ALL is NOT authored here — it is the
        /// unfiltered list, and authoring it would be duplicated state.</para>
        ///
        /// <para>Null/empty = the row is not Manage content at all (today: <c>deco_torch</c>,
        /// <c>repair_default</c>). Every row the build browser OFFERS must carry at least one
        /// token; <c>BuildInventoryFilterRegression</c> fails the build otherwise.</para>
        /// </summary>
        public string[]    manageFilters;

        /// <summary>
        /// WO-2005 — the art-sheet tile name for this row (e.g. <c>building-sky-ballista</c> for
        /// id <c>tower_siege_tower</c>), from the Sheet A delivery
        /// (<c>docs/ART_DELIVERY_2026-09-06_manage_assets.md</c> appendix).
        ///
        /// <para>⛔ THE ART NAME AND THE CATALOG ID ARE DIFFERENT LANGUAGES ON PURPOSE, and the
        /// join lives HERE, in data. <c>building-sky-ballista</c> is <c>tower_siege_tower</c>;
        /// <c>building-wooden-palisade</c> is <c>wall_wood</c>. A resolver that parsed names
        /// would have to encode both of those as special cases and would break on the next
        /// re-skin — the id is a live save key and can never move to match the art.</para>
        ///
        /// <para>Null = no tile is drawn for this row on any delivered sheet (today:
        /// <c>mill</c>, <c>deco_torch</c>, <c>repair_default</c>). Not an error — a missing tile
        /// is a presentation fallback, never a gate.</para>
        /// </summary>
        public string      manageArtKey;

        /// <summary>
        /// PRESENTATION ONLY (WO-963) — the build palette carousel's authored display order.
        /// LOWER SORTS FIRST; 0 (the default, i.e. the JSON key absent) means UNAUTHORED and
        /// sorts AFTER every authored row, keeping its current relative position — the sort is
        /// STABLE on catalog row order, so an unauthored row can never jump.
        ///
        /// Seeded to the TUTORIAL'S TEACHING ORDER so the shelf and the script agree. The
        /// palette NEVER reads tutorial-steps.json at runtime (ARCHITECTURE_PRINCIPLES §1/§2 —
        /// presentation must not take a teaching script as an input); the catalog carries the
        /// order and BuildCarouselTutorialOrderRegression is what keeps the two agreeing.
        ///
        /// Consumed by <c>BuildPaletteVM.SortForDisplay</c> and nothing else: it gates nothing,
        /// prices nothing and changes no group split.
        /// </summary>
        public int         displayOrder;

        /// <summary>LOOK — Resources/polyperfect-style prefab path. Resolved to a model at build time.</summary>
        public string      visualPrefabPath;

        /// <summary>
        /// LOOK (WO-707, ported from HubStructureVisualInjector.Swap.texPath) — OPTIONAL
        /// Resources texture FORCED onto the skinned visual's materials when the model's
        /// embedded material lost its texture link and would render colorless (e.g. the
        /// 'Structures/arcane tower' Tripo FBX — its owner-dialed swap row carried
        /// texPath "Structures/ArcaneTower_Albedo" -- the atlas was moved OUT of the nested
        /// "arcane tower/" folder whose name collided with the sibling "arcane tower.fbx"
        /// (that collision made Resources.Load return null and left the spire pure white)). Applied by
        /// StructureFactory.Create after the skin; null (default) = untouched.
        /// JSON deserializes "visualTexturePath" straight in.
        /// </summary>
        public string      visualTexturePath;

        /// <summary>BEHAVIOR — stats, nav, placement, behaviour id.</summary>
        public RepoProps   repo = new RepoProps();

        /// <summary>Composites only: the cell set to drop as a bundle. Null for cells.</summary>
        public CellPlacement[] composite = null;

        /// <summary>
        /// Orientation correction (CatalogOrientationBaker / the future Orientation
        /// Inspector). Auto-baked entries are ADVISORY (manual=false) and are NOT
        /// applied — a bounds heuristic can't tell an authored-flat-but-skinned-upright
        /// model (e.g. tower2: Y=0.37, Z=1.00) from a genuinely tipped one, so auto-
        /// rotating would tip good assets. Only HUMAN-verified (manual=true) corrections
        /// are applied by StructureFactory.
        /// </summary>
        public OrientationFix orientation = null;
    }

    /// <summary>A stored keep-vertical correction for a catalog entry's visual.</summary>
    [System.Serializable]
    public sealed class OrientationFix
    {
        public bool    corrected;
        public bool    manual;        // true = human-verified (Inspector) → applied; false = advisory
        public float[] euler;         // [x,y,z] degrees
        public float[] offset;        // [x,y,z] metres
        public float   scale = 1f;    // UNIFORM scale multiplier (legacy / back-compat).
        // NON-UNIFORM per-axis scale (WO: stretch X or Z, keep Y height) — multiplies on
        // TOP of the uniform `scale` per component. OPTIONAL + backward-compatible: when
        // absent/null (every existing entry) EffectiveScale falls back to (1,1,1), so the
        // effective scale equals the legacy uniform `scale` exactly. Serialized as [x,y,z].
        public float[] scaleAxis;     // [x,y,z] per-axis multipliers; null/short → (1,1,1)
        public string  note;

        public Vector3 Euler  => euler  != null && euler.Length  == 3 ? new Vector3(euler[0],  euler[1],  euler[2])  : Vector3.zero;
        public Vector3 Offset => offset != null && offset.Length == 3 ? new Vector3(offset[0], offset[1], offset[2]) : Vector3.zero;

        /// <summary>Per-axis multipliers as a Vector3, defaulting to (1,1,1) when unset/short.</summary>
        public Vector3 ScaleAxis => scaleAxis != null && scaleAxis.Length == 3
            ? new Vector3(scaleAxis[0], scaleAxis[1], scaleAxis[2])
            : Vector3.one;

        /// <summary>
        /// The full per-axis scale to APPLY: uniform <see cref="scale"/> (clamped to a sane
        /// positive, legacy 1) times the per-axis <see cref="ScaleAxis"/> per component. For
        /// every existing entry (scale only, no scaleAxis) this returns (s,s,s) — identical
        /// to the old uniform behaviour. Returned components are forced positive (≥ a tiny
        /// epsilon) so a 0/garbage value never collapses the mesh.
        /// </summary>
        public Vector3 EffectiveScale
        {
            get
            {
                float s = scale > 0f ? scale : 1f;
                Vector3 a = ScaleAxis;
                return new Vector3(
                    Mathf.Max(0.0001f, s * a.x),
                    Mathf.Max(0.0001f, s * a.y),
                    Mathf.Max(0.0001f, s * a.z));
            }
        }

        /// <summary>True when the effective scale differs from identity on any axis.</summary>
        public bool HasScale
        {
            get
            {
                Vector3 e = EffectiveScale;
                return !Mathf.Approximately(e.x, 1f)
                    || !Mathf.Approximately(e.y, 1f)
                    || !Mathf.Approximately(e.z, 1f);
            }
        }
    }
}
