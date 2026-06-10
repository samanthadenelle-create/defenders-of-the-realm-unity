// =============================================================================
// EquipmentController — visually equips real (KayKit) weapon meshes on a Humanoid
// hero by attaching them to the rig's hand bones, driven by the existing Gear-v1
// equip data (GearLoadout / WeaponDef). Armor is stubbed (entry point wired, no
// visual yet — assets incoming).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY THIS EXISTS / WHAT IT GENERALIZES:
//   Gear-v1 already attaches a *primitive cube* sword/staff/mace to the RightHand
//   bone via GearVisualApplier.AttachWeaponVisual (GearVisualApplier.cs:104-203,
//   parent resolved at :137 GetBoneTransform(HumanBodyBones.RightHand)), and the
//   Ranger gets a real KayKit bow via HeroBowAttachment (LeftHand bone, prop loaded
//   from Resources/Heroes/Props + bounds-normalized). This controller GENERALIZES
//   that pattern to ALL weapon classes using the real KayKit weapon meshes:
//     • resolve the equipped weapon id (from GearLoadout.EquippedWeapon, the SAME
//       data model — no new gear model invented),
//     • map the id -> a KayKit mesh + per-weapon grip offset/rotation,
//       (mesh attaches to RightHand; shields -> LeftHand),
//     • instantiate, parent, destroy the previous prop on swap (no stacking),
//     • re-attach whenever GearLoadout.OnGearChanged fires (the SAME event the
//       cube path already raises on equip-change).
//   The legacy cube GearVisualApplier stays as the no-mesh fallback (it null-guards
//   and is gated OFF by EnablePrimitiveGear), so nothing double-stacks: when a real
//   mesh resolves we use it; otherwise we keep the existing behaviour.
//
// MESH-LOADING GAP (important):
//   The KayKit weapon FBXs live under Assets/Models/KayKit/.../KayKit Fantasy
//   Weapons Bits 1.0/Assets/fbx(unity)/ — that folder is NOT a Resources folder
//   (and the pack is gitignored), so Resources.Load CANNOT reach them at runtime /
//   in a build. This mirrors the exact constraint HeroBowAttachment documents for
//   the bow. The build-safe convention already used for the bow is to COPY the
//   needed KayKit props into Assets/Resources/Heroes/Props/ (committed, Resources-
//   loadable). So this controller loads each weapon mesh from
//       Resources/Heroes/Props/Weapons/<meshName>
//   FIRST; if absent (mesh not yet copied), it falls back to a tinted primitive so
//   the hero still reads as armed. ACTION FOR ART/CLI: drop sword_A/D/G, staff_A,
//   wand_A, bow_A, dagger_A, axe_A, hammer_A, shield_A (as prefabs or fbx) into
//   Assets/Resources/Heroes/Props/Weapons/ to light up the real meshes. Until then
//   the primitive fallback renders.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Component on a hero. Reads the hero's equipped weapon (via GearLoadout, the
    /// Gear-v1 data model) and attaches a real KayKit weapon mesh to the Humanoid
    /// rig's RightHand bone (shields -> LeftHand) with a per-weapon grip transform.
    /// Re-attaches on equip-change. Armor is a wired-but-no-op stub for now.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EquipmentController : MonoBehaviour
    {
        // Resources sub-path the build-safe KayKit weapon props are copied into
        // (mirrors HeroBowAttachment's "Heroes/Props/Bow"). See file header / gap note.
        private const string WeaponPropResourceDir = "Heroes/Props/Weapons/";

        private const string PropName = "EquipmentProp_Weapon";

        // ── Weapon-id -> KayKit mesh + grip map ──────────────────────────────────
        // TODO data-driven: add a `visualMesh` (string) + `grip` (pos/euler/scale) to
        // weapons.json and read them off WeaponDef instead of this hardcoded table.
        // For now this maps the ACTUAL ids in weapons.json (mage_*, knight_*, ranger_*,
        // aegis_*) onto owned KayKit Fantasy Weapons Bits meshes. The grip values seat
        // the hilt/grip in the palm for a ~1.8m Humanoid hero; tune against a screenshot.
        private sealed class WeaponVisual
        {
            public string mesh;          // KayKit mesh name under Resources/Heroes/Props/Weapons/
            public bool leftHand;        // shields -> LeftHand; everything else RightHand
            public Vector3 gripPos;      // local position on the hand bone
            public Vector3 gripEuler;    // local rotation on the hand bone
            public float heldLength;     // longest-axis target length (m) after bounds-normalize
            public Color tint;           // fallback-primitive tint when the mesh isn't present
        }

        // Per-archetype grip presets (one place to tune each weapon family's seat).
        private static WeaponVisual Sword(string mesh) => new WeaponVisual
        {
            mesh = mesh, leftHand = false,
            gripPos = new Vector3(0f, 0.02f, 0f), gripEuler = new Vector3(0f, 0f, 0f),
            heldLength = 0.95f, tint = new Color(0.74f, 0.75f, 0.78f)
        };
        private static WeaponVisual Dagger(string mesh) => new WeaponVisual
        {
            mesh = mesh, leftHand = false,
            gripPos = new Vector3(0f, 0.01f, 0f), gripEuler = new Vector3(0f, 0f, 0f),
            heldLength = 0.40f, tint = new Color(0.70f, 0.72f, 0.76f)
        };
        private static WeaponVisual Axe(string mesh) => new WeaponVisual
        {
            mesh = mesh, leftHand = false,
            gripPos = new Vector3(0f, 0.02f, 0f), gripEuler = new Vector3(0f, 0f, 0f),
            heldLength = 0.80f, tint = new Color(0.68f, 0.66f, 0.62f)
        };
        private static WeaponVisual Hammer(string mesh) => new WeaponVisual
        {
            mesh = mesh, leftHand = false,
            gripPos = new Vector3(0f, 0.02f, 0f), gripEuler = new Vector3(0f, 0f, 0f),
            heldLength = 0.85f, tint = new Color(0.66f, 0.66f, 0.68f)
        };
        private static WeaponVisual Staff(string mesh) => new WeaponVisual
        {
            mesh = mesh, leftHand = false,
            gripPos = new Vector3(0f, 0.05f, 0f), gripEuler = new Vector3(0f, 0f, 0f),
            heldLength = 1.30f, tint = new Color(0.60f, 0.50f, 0.40f)
        };
        private static WeaponVisual Wand(string mesh) => new WeaponVisual
        {
            mesh = mesh, leftHand = false,
            gripPos = new Vector3(0f, 0.01f, 0f), gripEuler = new Vector3(0f, 0f, 0f),
            heldLength = 0.45f, tint = new Color(0.55f, 0.45f, 0.62f)
        };
        private static WeaponVisual Bow(string mesh) => new WeaponVisual
        {
            mesh = mesh, leftHand = true,   // bow goes in the off/bow (LEFT) hand
            // owner spec: bow longest->Y, grip=center; TUNABLE — nudge gripEuler on playtest
            // NormalizeInto already seats the bow to spec deterministically: LONGEST axis
            // (limbs/nock-to-nock) -> local +Y (upright), NARROWEST -> +X (thin left-right,
            // curve depth -> +Z forward), bounds-CENTRE at the grip root origin (hand grips
            // the middle of the curve). So gripEuler stays ZERO here — exactly the
            // proven-correct value HeroBowAttachment uses for the Ranger's held bow
            // (HeroBowAttachment.GripLocalEuler == (0,0,0); a prior +91 Z tweak rotated the
            // already-correct bow ~90° sideways — that WAS the "bow is turned" bug). Keep this
            // a single value: if a touch off in-hand, nudge ONLY this gripEuler on playtest.
            gripPos = new Vector3(0f, 0f, 0f), gripEuler = new Vector3(0f, 0f, 0f),
            heldLength = 0.92f, tint = new Color(0.36f, 0.22f, 0.10f)
        };
        private static WeaponVisual Shield(string mesh) => new WeaponVisual
        {
            mesh = mesh, leftHand = true,   // shields -> LeftHand per spec
            gripPos = new Vector3(-0.05f, 0f, 0f), gripEuler = new Vector3(0f, -10f, 0f),
            heldLength = 0.55f, tint = new Color(0.58f, 0.60f, 0.64f)
        };

        // Exact-id overrides keyed by the ids actually present in weapons.json.
        // (Falls through to the keyword classifier below for anything not listed.)
        // TODO data-driven: delete this once weapons.json carries visualMesh/grip.
        private static readonly Dictionary<string, WeaponVisual> IdMap =
            new Dictionary<string, WeaponVisual>(System.StringComparer.OrdinalIgnoreCase)
        {
            // Mage — wand at low tier, staff higher.
            { "mage_starter",        Wand("wand_A")   },
            { "mage_oak",            Staff("staff_A") },
            { "mage_arcane",         Staff("staff_B") },
            { "mage_void",           Staff("staff_C") },
            { "aegis_aetherstaff",   Staff("staff_D") },

            // Knight — sword tiers -> sword_A / sword_D / sword_G by tier.
            { "knight_starter",      Sword("sword_A") },
            { "knight_iron",         Sword("sword_D") },
            { "knight_oath",         Sword("sword_F") },
            { "knight_dawn",         Sword("sword_G") },
            { "aegis_emberbrand",    Sword("sword_G") },

            // Ranger — bows (LeftHand). NOTE: the Ranger's held bow is ALSO provided by
            // HeroBowAttachment; see EquipBestForHero() where we skip bows to avoid a
            // duplicate. Kept here so a non-ranger equipping a bow still gets one.
            { "ranger_starter",      Bow("bow_A") },
            { "ranger_yew",          Bow("bow_B") },
            { "ranger_storm",        Bow("bow_C") },
            { "ranger_eclipse",      Bow("bow_C") },
            { "aegis_heartwood_longbow", Bow("bow_C") },

            // Cleric — censer reads closest to a mace/hammer; use hammer_A stand-in.
            { "aegis_hallowed_censer", Hammer("hammer_A") },
        };

        // ── Runtime state ────────────────────────────────────────────────────────
        private Animator _animator;
        private GearLoadout _loadout;
        private GameObject _currentWeaponProp;
        private string _currentWeaponId;
        private int _armorTier;

        // If true, a bow equip is skipped here because HeroBowAttachment owns the
        // ranger's held bow (set when the hero already carries that component).
        private bool _deferBowToBowAttachment;

        private void Awake()
        {
            CacheRig();
            _loadout = GetComponent<GearLoadout>();
            // The ranger's bow is owned by HeroBowAttachment; don't double-attach.
            _deferBowToBowAttachment = GetComponent<HeroBowAttachment>() != null;
        }

        private void OnEnable()
        {
            if (_loadout == null) _loadout = GetComponent<GearLoadout>();
            if (_loadout != null) _loadout.OnGearChanged += HandleGearChanged;
            // Auto-read the equipped weapon on enable (the same data the cube path uses).
            EquipBestForHero();
        }

        private void OnDisable()
        {
            if (_loadout != null) _loadout.OnGearChanged -= HandleGearChanged;
        }

        private void HandleGearChanged() => EquipBestForHero();

        /// <summary>
        /// Re-reads the hero's currently equipped weapon from GearLoadout and shows the
        /// matching mesh. This is the hook into the EXISTING equip-change event — no new
        /// gear model. Safe to call repeatedly (idempotent on an unchanged id).
        /// </summary>
        public void EquipBestForHero()
        {
            if (_loadout == null) _loadout = GetComponent<GearLoadout>();
            string id = _loadout != null && _loadout.EquippedWeapon != null
                ? _loadout.EquippedWeapon.id
                : null;
            Equip(id);
        }

        /// <summary>
        /// Show the weapon mesh for <paramref name="weaponId"/> (an id from weapons.json),
        /// attaching it to the Humanoid hand bone with the mapped grip offset. Passing null
        /// or an empty id unequips. Destroys the previous prop first (no stacking).
        /// </summary>
        public void Equip(string weaponId)
        {
            // Idempotent: same weapon already shown -> nothing to do.
            if (string.Equals(_currentWeaponId, weaponId, System.StringComparison.OrdinalIgnoreCase)
                && _currentWeaponProp != null)
                return;

            DestroyCurrentWeapon();
            _currentWeaponId = weaponId;

            if (string.IsNullOrEmpty(weaponId)) return; // unequip

            WeaponVisual vis = Resolve(weaponId);
            if (vis == null) return;

            // The ranger's held bow is HeroBowAttachment's job — skip here to avoid two bows.
            if (_deferBowToBowAttachment && vis.mesh != null && vis.mesh.StartsWith("bow"))
                return;

            CacheRig();
            if (_animator == null || !_animator.isHuman)
            {
                // Generic/invalid avatar: the hand bones won't resolve. Match the existing
                // applier's choice — skip rather than dump geometry on the root.
                return;
            }

            Transform hand = _animator.GetBoneTransform(
                vis.leftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            if (hand == null)
            {
                Debug.LogWarning($"[EquipmentController] Humanoid rig missing " +
                                 $"{(vis.leftHand ? "LeftHand" : "RightHand")} bone — " +
                                 $"weapon '{weaponId}' not attached (cosmetic only).");
                return;
            }

            GameObject prop = LoadWeaponMesh(vis.mesh) ?? BuildFallbackPrimitive(vis);
            if (prop == null) return;

            prop.name = PropName;
            // Cosmetic only — strip physics/colliders a prefab might carry.
            foreach (var c in prop.GetComponentsInChildren<Collider>(true)) if (c != null) Destroy(c);
            foreach (var rb in prop.GetComponentsInChildren<Rigidbody>(true)) if (rb != null) Destroy(rb);

            // Seat via a grip root, bounds-normalized like HeroBowAttachment so any FBX
            // (regardless of its own pivot/scale/orientation) lands with the long axis up.
            var gripRoot = new GameObject(PropName);
            NormalizeInto(prop, gripRoot.transform, vis.heldLength);

            gripRoot.transform.SetParent(hand, false);
            gripRoot.transform.localPosition = vis.gripPos;
            gripRoot.transform.localRotation = Quaternion.Euler(vis.gripEuler);

            _currentWeaponProp = gripRoot;
        }

        /// <summary>Removes the currently-shown weapon prop (no-op if none).</summary>
        public void Unequip()
        {
            DestroyCurrentWeapon();
            _currentWeaponId = null;
        }

        // ── ARMOR STUB ───────────────────────────────────────────────────────────
        /// <summary>
        /// Entry point for armor visuals. Wired now so callers (GearLoadout, shop/equip
        /// UI) can drive it, but visually a NO-OP today.
        /// </summary>
        // TODO armor texture-swap — assets incoming; for now no-op (record the tier so the
        // visual pass can read it once the armor textures/meshes land).
        public void SetArmorTier(int tier)
        {
            _armorTier = Mathf.Max(0, tier);
            // No visual yet. When armor art lands: swap the body's material/texture (or
            // attach plate pieces to Chest/Shoulder/Leg bones) keyed off _armorTier.
        }

        /// <summary>Current armor tier (0 = none). Exposed for the future armor visual pass.</summary>
        public int ArmorTier => _armorTier;

        // ── Internals ──────────────────────────────────────────────────────────────
        private void CacheRig()
        {
            if (_animator != null && _animator.isHuman) return;
            // Body lives under "HeroBody" on the hero root (same convention as GearLoadout
            // / GearVisualApplier). Fall back to any child Animator.
            var body = transform.Find("HeroBody");
            _animator = body != null ? body.GetComponentInChildren<Animator>() : null;
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

        private void DestroyCurrentWeapon()
        {
            if (_currentWeaponProp != null)
            {
                Destroy(_currentWeaponProp);
                _currentWeaponProp = null;
            }
        }

        /// <summary>
        /// Map a weapons.json id -> a WeaponVisual: exact-id table first, then keyword
        /// classification on the id (so future ids still resolve to a sensible family).
        /// </summary>
        private static WeaponVisual Resolve(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return null;
            if (IdMap.TryGetValue(weaponId, out var hit)) return hit;

            string id = weaponId.ToLowerInvariant();
            // Order matters: more specific keywords first.
            if (id.Contains("bow"))     return Bow("bow_A");
            if (id.Contains("dagger"))  return Dagger("dagger_A");
            if (id.Contains("axe"))     return Axe("axe_A");
            if (id.Contains("hammer") || id.Contains("mace")) return Hammer("hammer_A");
            if (id.Contains("staff"))   return Staff("staff_A");
            if (id.Contains("wand") || id.Contains("scepter") || id.Contains("scept"))
                                        return Wand("wand_A");
            if (id.Contains("shield"))  return Shield("shield_A");
            // Job-coded ids without a weapon keyword.
            if (id.StartsWith("mage"))  return Staff("staff_A");
            if (id.StartsWith("ranger"))return Bow("bow_A");
            // Default: a sword (knight / generic melee).
            return Sword("sword_A");
        }

        /// <summary>
        /// Loads a KayKit weapon mesh from the build-safe Resources path (prefab first,
        /// then a model/fbx GameObject). Returns null when the prop hasn't been copied
        /// into Resources/Heroes/Props/Weapons yet (see file header gap note).
        /// </summary>
        private static GameObject LoadWeaponMesh(string meshName)
        {
            if (string.IsNullOrEmpty(meshName)) return null;
            string path = WeaponPropResourceDir + meshName;
            var prefab = Resources.Load<GameObject>(path);
            return prefab != null ? Instantiate(prefab) : null;
        }

        /// <summary>
        /// Tinted-primitive stand-in for when the real KayKit mesh isn't in Resources yet
        /// (keeps the hero visibly armed). One thin box; NormalizeInto sizes it to heldLength.
        /// </summary>
        private static GameObject BuildFallbackPrimitive(WeaponVisual vis)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            // A thin tall box reads as a blade/haft; NormalizeInto puts the long axis up.
            go.transform.localScale = new Vector3(0.05f, 1f, 0.05f);

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (sh != null)
                {
                    var mat = new Material(sh);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", vis.tint);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", vis.tint);
                    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.6f);
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.5f);
                    mr.sharedMaterial = mat;
                }
            }
            return go;
        }

        // ── Bounds-normalize (generalized from HeroBowAttachment.NormalizeInto) ──────
        // Parents `prop` under `parent`, orienting its LONGEST axis -> parent +Y, its
        // NARROWEST axis -> parent +X, bounds-centre at the origin, scaled so the longest
        // axis is `targetLength` m. Deterministic from renderer bounds — any weapon FBX
        // lands right without hand-guessed Euler. (Kept self-contained so this controller
        // doesn't depend on HeroBowAttachment's private helper.)
        private static void NormalizeInto(GameObject prop, Transform parent, float targetLength)
        {
            prop.transform.SetParent(parent, false);
            prop.transform.localPosition = Vector3.zero;
            prop.transform.localRotation = Quaternion.identity;
            prop.transform.localScale = Vector3.one;

            if (!TryLocalBounds(prop, parent, out Bounds b0)) return;
            Vector3 sz = b0.size;
            int lng = (sz.x >= sz.y && sz.x >= sz.z) ? 0 : (sz.y >= sz.z ? 1 : 2);
            int sht = (sz.x <= sz.y && sz.x <= sz.z) ? 0 : (sz.y <= sz.z ? 1 : 2);
            if (sht == lng) sht = (lng + 1) % 3;

            Quaternion alignLong = Quaternion.FromToRotation(Axis(lng), Vector3.up);
            prop.transform.localRotation = alignLong;

            Vector3 shortAfter = alignLong * Axis(sht); shortAfter.y = 0f;
            if (shortAfter.sqrMagnitude > 1e-5f)
                prop.transform.localRotation =
                    Quaternion.FromToRotation(shortAfter.normalized, Vector3.right) * alignLong;

            if (TryLocalBounds(prop, parent, out Bounds b1) && b1.size.y > 1e-4f)
                prop.transform.localScale = Vector3.one * (targetLength / b1.size.y);

            if (TryLocalBounds(prop, parent, out Bounds b2))
                prop.transform.localPosition -= b2.center;
        }

        private static Vector3 Axis(int i) =>
            i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

        private static bool TryLocalBounds(GameObject prop, Transform parent, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            foreach (var r in prop.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                Bounds wb = r.bounds;
                Vector3 c = parent.InverseTransformPoint(wb.center);
                Vector3 e = parent.InverseTransformVector(wb.extents);
                var lb = new Bounds(c, new Vector3(Mathf.Abs(e.x), Mathf.Abs(e.y), Mathf.Abs(e.z)) * 2f);
                if (!any) { bounds = lb; any = true; } else bounds.Encapsulate(lb);
            }
            return any;
        }
    }
}
