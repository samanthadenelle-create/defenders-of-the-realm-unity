// =============================================================================
// ItemMoteShapes - the SHAPE FAMILY table for world drop motes.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Items
//
// WHY THIS EXISTS (WO-1132 follow-on, 2026-08-21):
//   ItemPickupSpawner spawned ONE hardcoded gold sphere for EVERY drop. A chest
//   that rolled Iron Scrap and one that rolled a Heartwood Bough left motes that
//   were byte-for-byte the same object - the player could not tell what was on
//   the floor without walking over it. The drop had no identity at all.
//
// THE LESSON THIS FILE OBEYS (proven the same day on IngredientPickup):
//   The sibling defect looked like "the tint is broken". It was NOT. Every tint
//   parsed fine; the authored tints were PASTELS on a non-emissive URP/Lit
//   sphere, so under light they all washed to the same white pellet. COLOUR WAS
//   NEVER GOING TO CARRY IDENTITY - and for this owner (red/green colourblind)
//   it could not carry it in principle. So identity rides SHAPE.
//
// AND: the identity data was ALREADY AUTHORED and nothing read it. Every
//   consumables.json and materials.json row carries a `glyph` char (the
//   [item-identity] oracle case 2 pins that they all do). ItemIdentity.GlyphOf
//   already surfaced it. This file is the missing half - glyph -> silhouette -
//   NOT a new parallel identity table. Nothing here re-authors an item's
//   meaning; it only says what a '*' or a 'Y' LOOKS like in the world.
//
// UNAUTHORED IDS (loot-tables.json drops four ids no catalog owns:
//   monster-hide, wild-herb, tattered-cloth, rare-essence - the [item-identity]
//   oracle reports them as a PO content gap). They are NOT crashes and they are
//   NOT silently the same sphere: an id with no row resolves a family by a
//   stable FNV-1a hash of the id, so each gets a distinct, deterministic
//   silhouette, and the mote is still NAMED from ItemIdentity.DisplayName
//   (which returns the raw id when unauthored). The right fix is authoring the
//   rows; this file just refuses to hide the gap behind an identical pellet.
//
// PURE-ISH: the spec side (ResolveGlyph / PartsFor / SignatureFor / TintFor) has
// no GameObject construction and never throws, so an EditMode oracle can read the
// silhouette of every dropped id without entering play mode. Construction lives
// in ItemPickupSpawner.
//
// ASCII strings only.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Village.Items
{
    /// <summary>One primitive of a mote silhouette, in the mote body's local space.</summary>
    public readonly struct MotePartSpec
    {
        public readonly string Name;
        public readonly PrimitiveType Primitive;
        public readonly Vector3 LocalPosition;
        public readonly Vector3 LocalScale;
        public readonly Quaternion LocalRotation;

        public MotePartSpec(string name, PrimitiveType primitive, Vector3 localPosition,
                            Vector3 localScale, Quaternion localRotation)
        {
            Name = name;
            Primitive = primitive;
            LocalPosition = localPosition;
            LocalScale = localScale;
            LocalRotation = localRotation;
        }

        public MotePartSpec(string name, PrimitiveType primitive, Vector3 localPosition, Vector3 localScale)
            : this(name, primitive, localPosition, localScale, Quaternion.identity) { }
    }

    /// <summary>
    /// glyph char -> silhouette. The ONE place a drop mote's shape is decided.
    /// </summary>
    public static class ItemMoteShapes
    {
        /// <summary>
        /// Every glyph this table draws a distinct silhouette for. Doubles as the
        /// deterministic fallback roster for an id/glyph the table cannot resolve -
        /// so a fallback still lands on a REAL family, never on a bare sphere.
        /// Order is load-bearing (it indexes the hash); append, never reorder.
        /// </summary>
        public static readonly char[] Roster =
            { '|', '~', '*', '=', 'T', 'Y', 'b', 'o', '+', '#', '.',
              '/', 'j', 'O', 'S', 'H', '!', '@', '%', '^', '>' };

        // -- Kind tints. THESE ARE REINFORCEMENT, NEVER IDENTITY. -----------------
        // Three buckets only (what the row IS, not which row it is), and the three
        // are separated in Rec.709 LUMA - i.e. they survive a greyscale pass - so
        // they add a cue for a sighted player without ever being the cue.
        public static readonly Color ConsumableTint = new Color(0.98f, 0.93f, 0.78f, 1f); // luma ~0.93
        public static readonly Color MaterialTint = new Color(0.86f, 0.66f, 0.24f, 1f); // luma ~0.67
        public static readonly Color UnknownTint = new Color(0.45f, 0.44f, 0.42f, 1f); // luma ~0.44

        /// <summary>Tint for an id, chosen by which catalog owns it (never by which row).</summary>
        public static Color TintFor(string id)
        {
            switch (ItemIdentity.KindOf(id))
            {
                case ItemIdentityKind.Consumable: return ConsumableTint;
                case ItemIdentityKind.Material: return MaterialTint;
                default: return UnknownTint;
            }
        }

        /// <summary>True when a catalog actually owns this id (i.e. it has authored identity).</summary>
        public static bool HasAuthoredIdentity(string id) => ItemIdentity.Resolve(id).IsKnown;

        /// <summary>
        /// The family glyph this id draws as.
        /// <para>
        /// AUTHORED row with an authored glyph the table knows -> that glyph, verbatim.
        /// AUTHORED row whose glyph the table does not draw yet -> a family picked from
        /// the CHAR (so the authored identity still decides, stably).
        /// NO row at all -> a family picked from a stable hash of the ID, so the four
        /// unauthored drop ids get four distinct silhouettes instead of four identical
        /// spheres. Never throws; empty/null id lands on a defined family.
        /// </para>
        /// </summary>
        public static char ResolveGlyph(string id)
        {
            var row = ItemIdentity.Resolve(id);
            if (row.IsKnown && !string.IsNullOrEmpty(row.Glyph))
            {
                char c = row.Glyph[0];
                if (IsDrawn(c)) return c;
                return Roster[c % Roster.Length];
            }
            return Roster[(int)(StableHash(id) % (uint)Roster.Length)];
        }

        /// <summary>True when the table draws a bespoke silhouette for this char.</summary>
        public static bool IsDrawn(char glyph)
        {
            for (int i = 0; i < Roster.Length; i++)
                if (Roster[i] == glyph) return true;
            return false;
        }

        /// <summary>FNV-1a over the id - stable across runs, platforms and Unity versions
        /// (string.GetHashCode is NOT, which is why this is hand-rolled).</summary>
        public static uint StableHash(string s)
        {
            uint h = 2166136261u;
            if (string.IsNullOrEmpty(s)) return h;
            for (int i = 0; i < s.Length; i++)
            {
                h ^= (uint)(s[i] & 0xFF);
                h *= 16777619u;
            }
            return h;
        }

        /// <summary>Human-readable family name for a glyph (used by the oracle's report).</summary>
        public static string FamilyName(char glyph)
        {
            switch (glyph)
            {
                case '|': return "stalk";
                case '~': return "droplet";
                case '*': return "shard-cluster";
                case '=': return "folded-slab";
                case 'T': return "mushroom";
                case 'Y': return "forked-root";
                case 'b': return "flask";
                case 'o': return "cored-ring";
                case '+': return "faceted-cross";
                case '#': return "brick-lattice";
                case '.': return "dust-scatter";
                case '/': return "bone-shard";
                case 'j': return "hooked-vial";
                case 'O': return "hollow-ring";
                case 'S': return "twisted-bar";
                case 'H': return "girder-plate";
                case '!': return "capped-vial";
                case '@': return "lumpy-stone";
                case '%': return "tied-bundle";
                case '^': return "tepee";
                case '>': return "chevron";
                default: return "pebble";
            }
        }

        /// <summary>
        /// A colour-free description of the silhouette: family name plus the sorted
        /// multiset of primitives it is built from. THIS is what a greyscale pass sees,
        /// and it is what the oracle compares - two ids whose signatures differ are
        /// distinguishable with every hue stripped out.
        /// </summary>
        public static string SignatureFor(char glyph)
        {
            int cubes = 0, spheres = 0, cylinders = 0, other = 0;
            var parts = PartsFor(glyph);
            for (int i = 0; i < parts.Count; i++)
            {
                switch (parts[i].Primitive)
                {
                    case PrimitiveType.Cube: cubes++; break;
                    case PrimitiveType.Sphere: spheres++; break;
                    case PrimitiveType.Cylinder: cylinders++; break;
                    default: other++; break;
                }
            }
            return FamilyName(glyph) + ":cube" + cubes + ",sph" + spheres +
                   ",cyl" + cylinders + ",oth" + other;
        }

        /// <summary>Signature for an ITEM ID (resolves its family first). Never throws.</summary>
        public static string SignatureForId(string id) => SignatureFor(ResolveGlyph(id));

        /// <summary>
        /// The parts of one silhouette, in the mote body's local space. Overall footprint
        /// is held near the old 0.45-scale sphere so drop density reads the same.
        /// Never null.
        /// </summary>
        public static IReadOnlyList<MotePartSpec> PartsFor(char glyph)
        {
            var p = new List<MotePartSpec>(8);
            switch (glyph)
            {
                case '|':   // dry-reed - a tall thin stalk with a node.
                    p.Add(new MotePartSpec("Stalk", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.07f, 0.26f, 0.07f)));
                    p.Add(new MotePartSpec("Node", PrimitiveType.Cylinder, new Vector3(0f, 0.06f, 0f), new Vector3(0.12f, 0.02f, 0.12f)));
                    break;

                case '~':   // liquids / oil-soaked cloth - a tapering droplet.
                    p.Add(new MotePartSpec("Belly", PrimitiveType.Sphere, new Vector3(0f, -0.06f, 0f), new Vector3(0.32f, 0.30f, 0.32f)));
                    p.Add(new MotePartSpec("Taper", PrimitiveType.Sphere, new Vector3(0f, 0.12f, 0f), Vector3.one * 0.17f));
                    p.Add(new MotePartSpec("Tip", PrimitiveType.Sphere, new Vector3(0f, 0.22f, 0f), Vector3.one * 0.08f));
                    break;

                case '*':   // blooms / herbs / resin - a radiating shard cluster.
                    p.Add(new MotePartSpec("Core", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.15f));
                    for (int i = 0; i < 4; i++)
                    {
                        var rot = Quaternion.Euler(0f, i * 90f, 52f);
                        p.Add(new MotePartSpec("Shard" + i, PrimitiveType.Cube,
                            rot * new Vector3(0f, 0.13f, 0f), new Vector3(0.05f, 0.24f, 0.05f), rot));
                    }
                    break;

                case '=':   // cloth scrap / iron scrap - a flat folded slab.
                    p.Add(new MotePartSpec("LeafLower", PrimitiveType.Cube, new Vector3(0f, -0.05f, 0.03f), new Vector3(0.40f, 0.05f, 0.26f)));
                    p.Add(new MotePartSpec("LeafUpper", PrimitiveType.Cube, new Vector3(0f, 0.02f, -0.04f), new Vector3(0.34f, 0.05f, 0.22f)));
                    break;

                case 'T':   // fungus - a stalk plus a domed cap.
                    p.Add(new MotePartSpec("Stalk", PrimitiveType.Cylinder, new Vector3(0f, -0.11f, 0f), new Vector3(0.10f, 0.11f, 0.10f)));
                    p.Add(new MotePartSpec("Cap", PrimitiveType.Sphere, new Vector3(0f, 0.06f, 0f), new Vector3(0.34f, 0.20f, 0.34f)));
                    break;

                case 'Y':   // ironroot / heartwood bough - a forked root.
                    p.Add(new MotePartSpec("Taproot", PrimitiveType.Cylinder, new Vector3(0f, -0.12f, 0f), new Vector3(0.08f, 0.12f, 0.08f)));
                    AddFork(p, "ForkA", 34f);
                    AddFork(p, "ForkB", -34f);
                    break;

                case 'b':   // oil flask - round belly, narrow neck, stopper.
                    p.Add(new MotePartSpec("Belly", PrimitiveType.Sphere, new Vector3(0f, -0.06f, 0f), new Vector3(0.30f, 0.28f, 0.30f)));
                    p.Add(new MotePartSpec("Neck", PrimitiveType.Cylinder, new Vector3(0f, 0.13f, 0f), Vector3.one * 0.10f));
                    p.Add(new MotePartSpec("Stopper", PrimitiveType.Cube, new Vector3(0f, 0.24f, 0f), new Vector3(0.13f, 0.06f, 0.13f)));
                    break;

                case 'o':   // ember crystal / bomb - a solid core inside a tight halo ring.
                    p.Add(new MotePartSpec("Core", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.16f));
                    AddRing(p, "Halo", 6, 0.19f, new Vector3(0.06f, 0.06f, 0.11f));
                    break;

                case '+':   // shards / salves - a three-axis faceted cross.
                    p.Add(new MotePartSpec("AxisX", PrimitiveType.Cube, Vector3.zero, new Vector3(0.34f, 0.09f, 0.09f)));
                    p.Add(new MotePartSpec("AxisY", PrimitiveType.Cube, Vector3.zero, new Vector3(0.09f, 0.34f, 0.09f)));
                    p.Add(new MotePartSpec("AxisZ", PrimitiveType.Cube, Vector3.zero, new Vector3(0.09f, 0.09f, 0.34f)));
                    break;

                case '#':   // heartstone / tonic - stacked, offset brickwork.
                    p.Add(new MotePartSpec("CourseLower", PrimitiveType.Cube, new Vector3(0f, -0.10f, 0f), new Vector3(0.34f, 0.07f, 0.24f)));
                    p.Add(new MotePartSpec("CourseMid", PrimitiveType.Cube, Vector3.zero, new Vector3(0.28f, 0.07f, 0.30f), Quaternion.Euler(0f, 45f, 0f)));
                    p.Add(new MotePartSpec("CourseUpper", PrimitiveType.Cube, new Vector3(0f, 0.10f, 0f), new Vector3(0.34f, 0.07f, 0.24f)));
                    break;

                case '.':   // arcane dust - a loose scatter of grains.
                    p.Add(new MotePartSpec("Grain0", PrimitiveType.Sphere, new Vector3(0f, 0.02f, 0f), Vector3.one * 0.13f));
                    p.Add(new MotePartSpec("Grain1", PrimitiveType.Sphere, new Vector3(0.15f, -0.07f, 0.06f), Vector3.one * 0.09f));
                    p.Add(new MotePartSpec("Grain2", PrimitiveType.Sphere, new Vector3(-0.13f, 0.09f, -0.05f), Vector3.one * 0.08f));
                    p.Add(new MotePartSpec("Grain3", PrimitiveType.Sphere, new Vector3(0.04f, 0.16f, 0.12f), Vector3.one * 0.07f));
                    p.Add(new MotePartSpec("Grain4", PrimitiveType.Sphere, new Vector3(-0.06f, -0.12f, 0.13f), Vector3.one * 0.07f));
                    break;

                case '/':   // bone fragment - a tilted shaft with knobbed ends.
                {
                    var rot = Quaternion.Euler(0f, 0f, 38f);
                    p.Add(new MotePartSpec("Shaft", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.06f, 0.22f, 0.06f), rot));
                    p.Add(new MotePartSpec("KnobA", PrimitiveType.Sphere, rot * new Vector3(0f, 0.21f, 0f), Vector3.one * 0.13f));
                    p.Add(new MotePartSpec("KnobB", PrimitiveType.Sphere, rot * new Vector3(0f, -0.21f, 0f), Vector3.one * 0.13f));
                    break;
                }

                case 'j':   // quench oil - a vial with a hooked foot.
                    p.Add(new MotePartSpec("Body", PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0f), new Vector3(0.10f, 0.18f, 0.10f)));
                    p.Add(new MotePartSpec("Foot", PrimitiveType.Cube, new Vector3(0.09f, -0.15f, 0f), new Vector3(0.17f, 0.06f, 0.09f)));
                    p.Add(new MotePartSpec("Bead", PrimitiveType.Sphere, new Vector3(0f, 0.25f, 0f), Vector3.one * 0.10f));
                    break;

                case 'O':   // heartwood core - a HOLLOW ring, no core (reads apart from 'o').
                    AddRing(p, "Rim", 8, 0.26f, new Vector3(0.08f, 0.08f, 0.13f));
                    break;

                case 'S':   // reforged steel - a twisted bar.
                    p.Add(new MotePartSpec("ArmUpper", PrimitiveType.Cube, new Vector3(-0.10f, 0.11f, 0f), new Vector3(0.22f, 0.07f, 0.09f)));
                    p.Add(new MotePartSpec("Spine", PrimitiveType.Cube, Vector3.zero, new Vector3(0.09f, 0.26f, 0.09f), Quaternion.Euler(0f, 0f, 38f)));
                    p.Add(new MotePartSpec("ArmLower", PrimitiveType.Cube, new Vector3(0.10f, -0.11f, 0f), new Vector3(0.22f, 0.07f, 0.09f)));
                    break;

                case 'H':   // oathweld plating - a girder.
                    p.Add(new MotePartSpec("FlangeL", PrimitiveType.Cube, new Vector3(-0.13f, 0f, 0f), new Vector3(0.08f, 0.34f, 0.11f)));
                    p.Add(new MotePartSpec("FlangeR", PrimitiveType.Cube, new Vector3(0.13f, 0f, 0f), new Vector3(0.08f, 0.34f, 0.11f)));
                    p.Add(new MotePartSpec("Web", PrimitiveType.Cube, Vector3.zero, new Vector3(0.30f, 0.08f, 0.09f)));
                    break;

                case '!':   // draughts / last pressing - a capped vial.
                    p.Add(new MotePartSpec("Body", PrimitiveType.Cylinder, new Vector3(0f, -0.04f, 0f), new Vector3(0.11f, 0.18f, 0.11f)));
                    p.Add(new MotePartSpec("Cap", PrimitiveType.Sphere, new Vector3(0f, 0.20f, 0f), Vector3.one * 0.12f));
                    break;

                case '@':   // rough stone - an irregular lump.
                    p.Add(new MotePartSpec("MassA", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.30f, 0.26f, 0.28f)));
                    p.Add(new MotePartSpec("MassB", PrimitiveType.Sphere, new Vector3(0.11f, 0.07f, -0.05f), Vector3.one * 0.20f));
                    p.Add(new MotePartSpec("MassC", PrimitiveType.Sphere, new Vector3(-0.10f, -0.06f, 0.08f), Vector3.one * 0.17f));
                    break;

                case '%':   // rations / stew - two parcels under a tie.
                    p.Add(new MotePartSpec("ParcelA", PrimitiveType.Cube, new Vector3(-0.07f, 0f, 0f), Vector3.one * 0.21f, Quaternion.Euler(0f, 20f, 0f)));
                    p.Add(new MotePartSpec("ParcelB", PrimitiveType.Cube, new Vector3(0.09f, -0.02f, 0.04f), Vector3.one * 0.16f, Quaternion.Euler(0f, -25f, 0f)));
                    p.Add(new MotePartSpec("Tie", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0f), new Vector3(0.36f, 0.05f, 0.06f)));
                    break;

                case '^':   // tent kit / campfire - a leaning tepee.
                    for (int i = 0; i < 3; i++)
                    {
                        float yaw = i * 120f;
                        var rot = Quaternion.Euler(16f, yaw, 0f);
                        p.Add(new MotePartSpec("Pole" + i, PrimitiveType.Cylinder,
                            Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, -0.02f, 0.07f),
                            new Vector3(0.05f, 0.20f, 0.05f), rot));
                    }
                    break;

                case '>':   // swiftstep - a forward chevron.
                    p.Add(new MotePartSpec("WingL", PrimitiveType.Cube,
                        Quaternion.Euler(0f, 35f, 0f) * new Vector3(0f, 0f, -0.11f),
                        new Vector3(0.07f, 0.07f, 0.26f), Quaternion.Euler(0f, 35f, 0f)));
                    p.Add(new MotePartSpec("WingR", PrimitiveType.Cube,
                        Quaternion.Euler(0f, -35f, 0f) * new Vector3(0f, 0f, -0.11f),
                        new Vector3(0.07f, 0.07f, 0.26f), Quaternion.Euler(0f, -35f, 0f)));
                    break;

                default:    // Unreachable via ResolveGlyph (it always lands on the roster);
                            // kept defined so a hand-passed char can never produce nothing.
                    p.Add(new MotePartSpec("Pebble", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.34f));
                    p.Add(new MotePartSpec("Chip", PrimitiveType.Cube, new Vector3(0.12f, 0.10f, 0f), Vector3.one * 0.11f));
                    break;
            }
            return p;
        }

        private static void AddFork(List<MotePartSpec> p, string name, float roll)
        {
            var rot = Quaternion.Euler(0f, 0f, roll);
            p.Add(new MotePartSpec(name, PrimitiveType.Cylinder,
                rot * new Vector3(0f, 0.14f, 0f), new Vector3(0.07f, 0.14f, 0.07f), rot));
        }

        private static void AddRing(List<MotePartSpec> p, string name, int segments, float radius, Vector3 scale)
        {
            for (int i = 0; i < segments; i++)
            {
                float yaw = i * (360f / segments);
                var rot = Quaternion.Euler(0f, yaw, 0f);
                p.Add(new MotePartSpec(name + i, PrimitiveType.Cube,
                    rot * new Vector3(0f, 0f, radius), scale, rot));
            }
        }
    }
}
