# Blender headless mesh decimation for SKINNED / RIGGED FBX characters.
# Usage (see docs/MESH_DECIMATION_PROCESS.md):
#   blender -b --factory-startup --python tools/mesh/decimate_skinned_fbx.py -- <src.fbx> <outdir> <tris1> [tris2 ...]
# Writes <outdir>/<name>_<tris>k.fbx plus <outdir>/decimate-report.csv
# NEVER point <src.fbx> at a shipping asset. Work on a copy.
import bpy, sys, os, csv

argv = sys.argv[sys.argv.index("--") + 1:]
src, outdir = argv[0], argv[1]
targets = [int(x) for x in argv[2:]]
os.makedirs(outdir, exist_ok=True)
name = os.path.splitext(os.path.basename(src))[0]
rows = []

def import_src():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=src, automatic_bone_orientation=True)

def meshes():
    return [o for o in bpy.data.objects if o.type == 'MESH']

def tri_count():
    n = 0
    for o in meshes():
        o.data.calc_loop_triangles()
        n += len(o.data.loop_triangles)
    return n

def export(path):
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=False,
        object_types={'ARMATURE', 'MESH'},
        use_mesh_modifiers=True, mesh_smooth_type='FACE',
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
        bake_anim=False, path_mode='AUTO', embed_textures=False,
        apply_scale_options='FBX_SCALE_NONE', axis_forward='-Z', axis_up='Y')

import_src()
SRC_TRIS = tri_count()
SRC_BYTES = os.path.getsize(src)
print(f"[DECIM] source {name}: tris={SRC_TRIS} bytes={SRC_BYTES}")

# ratio 1.0 = round-trip CONTROL. Isolates exporter/format overhead from the
# real geometry saving, so the per-density numbers are apples-to-apples.
for target in [SRC_TRIS] + targets:
    import_src()
    ratio = min(1.0, target / float(SRC_TRIS))
    for o in meshes():
        bpy.context.view_layer.objects.active = o
        # Decimate CANNOT be applied while shape keys exist. Verify each key is
        # inert (moves no vertex) BEFORE removing it -- a real blend shape must
        # stop the run, not be silently discarded.
        if o.data.shape_keys:
            moved = 0
            for kb in o.data.shape_keys.key_blocks:
                for i, p in enumerate(kb.data):
                    if (p.co - o.data.vertices[i].co).length > 1e-6:
                        moved += 1
            if moved:
                raise SystemExit(f"[DECIM] ABORT: {o.name} has LIVE blend shapes "
                                 f"({moved} moved verts). Decimation would destroy them.")
            print(f"[DECIM] {o.name}: removing {len(o.data.shape_keys.key_blocks)} inert shape key(s)")
            o.shape_key_clear()
        if ratio < 1.0:
            m = o.modifiers.new(name="DecimateForShip", type='DECIMATE')
            m.decimate_type = 'COLLAPSE'
            m.ratio = ratio
            m.use_collapse_triangulate = True
            # Decimate must run FIRST, before Armature, or the skinning bakes in.
            bpy.ops.object.modifier_move_to_index(modifier=m.name, index=0)
            bpy.ops.object.modifier_apply(modifier=m.name)

    got = tri_count()
    tag = "orig-roundtrip" if ratio >= 1.0 else f"{target//1000}k"
    out = os.path.join(outdir, f"{name}_{tag}.fbx")
    export(out)
    b = os.path.getsize(out)
    vg = sum(len(o.vertex_groups) for o in meshes())
    vt = sum(len(o.data.vertices) for o in meshes())
    uv = min([len(o.data.uv_layers) for o in meshes()] or [0])
    bones = sum(len(o.data.bones) for o in bpy.data.objects if o.type == 'ARMATURE')
    print(f"[DECIM] {tag}: ratio={ratio:.5f} tris={got} verts={vt} bones={bones} "
          f"vgroups={vg} uv={uv} bytes={b} ({b/1048576:.2f} MB)")
    rows.append(dict(tag=tag, ratio=round(ratio, 5), tris=got, verts=vt, bones=bones,
                     vgroups=vg, uv_sets=uv, bytes=b, mb=round(b / 1048576, 2),
                     pct_of_source=round(100.0 * b / SRC_BYTES, 1)))

with open(os.path.join(outdir, "decimate-report.csv"), "w", newline="") as f:
    w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
    w.writeheader(); w.writerows(rows)
print("[DECIM] DECIMATE_OK")
