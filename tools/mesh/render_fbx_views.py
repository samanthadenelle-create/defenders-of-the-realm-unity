# Headless greyscale turntable-style evidence renders for a character FBX.
# Colourblind-safe by construction: ONE flat light-grey material + luminance-only
# Workbench studio lighting. No hue anywhere, so differences read as SHAPE only.
# Usage:
#   blender -b --factory-startup --python tools/mesh/render_fbx_views.py -- <in.fbx> <outdir> <label> [framing.json]
import bpy, sys, os, json, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
src, outdir, label = argv[0], argv[1], argv[2]
framing_path = argv[3] if len(argv) > 3 else None
os.makedirs(outdir, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src, automatic_bone_orientation=True)
for o in list(bpy.data.objects):
    if o.type == 'ARMATURE':
        o.hide_render = True
meshes = [o for o in bpy.data.objects if o.type == 'MESH']

def world_bounds():
    lo = Vector((1e9, 1e9, 1e9)); hi = Vector((-1e9, -1e9, -1e9))
    for o in bpy.data.objects:
        if o.type != 'MESH':
            continue
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            lo = Vector((min(lo[i], w[i]) for i in range(3)))
            hi = Vector((max(hi[i], w[i]) for i in range(3)))
    return lo, hi

_lo, _hi = world_bounds()
_ext = _hi - _lo
if _ext.z == min(_ext):
    # depth landed on Z => file is Y-up. Stand it up so the render views are sane.
    for o in bpy.data.objects:
        if o.parent is None:
            o.rotation_euler.rotate_axis('X', math.radians(90))
    bpy.context.view_layer.update()
    print("[RENDER] rotated Y-up source to Z-up for framing")

# --- world-space bounds, so every density is framed IDENTICALLY -------------
lo, hi = world_bounds()
if framing_path and os.path.exists(framing_path):
    f = json.load(open(framing_path)); lo = Vector(f["lo"]); hi = Vector(f["hi"])
elif framing_path:
    json.dump({"lo": list(lo), "hi": list(hi)}, open(framing_path, "w"))
ctr = (lo + hi) / 2.0
size = hi - lo
print(f"[RENDER] {label} bounds lo={tuple(round(v,4) for v in lo)} hi={tuple(round(v,4) for v in hi)}")

# --- flat grey material, one for every mesh --------------------------------
mat = bpy.data.materials.new("EvidenceGrey")
mat.diffuse_color = (0.72, 0.72, 0.72, 1.0)
for o in meshes:
    o.data.materials.clear(); o.data.materials.append(mat)

sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sc.render.resolution_x = 900; sc.render.resolution_y = 1200
sc.render.resolution_percentage = 100
sc.render.film_transparent = False
if sc.world is None:
    sc.world = bpy.data.worlds.new("EvidenceWorld")
sc.world.color = (0.06, 0.06, 0.06)
sh = sc.display.shading
sh.light = 'STUDIO'; sh.studio_light = 'Default'
sh.color_type = 'SINGLE'; sh.single_color = (0.72, 0.72, 0.72)
sh.show_shadows = False; sh.show_cavity = True
sh.cavity_type = 'BOTH'; sh.curvature_ridge_factor = 1.0; sh.curvature_valley_factor = 1.0

cam_d = bpy.data.cameras.new("Cam"); cam = bpy.data.objects.new("Cam", cam_d)
sc.collection.objects.link(cam); sc.camera = cam
cam_d.type = 'ORTHO'

def shoot(fname, yaw_deg, pitch_deg, focus, ortho_h):
    cam_d.ortho_scale = ortho_h
    yaw = math.radians(yaw_deg); pitch = math.radians(pitch_deg)
    dist = max(size) * 4.0 + 1.0
    dirv = Vector((math.sin(yaw) * math.cos(pitch), -math.cos(yaw) * math.cos(pitch), math.sin(pitch)))
    cam.location = focus + dirv * dist
    # aim at focus
    fwd = (focus - cam.location).normalized()
    cam.rotation_euler = fwd.to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(outdir, fname)
    bpy.ops.render.render(write_still=True)
    print(f"[RENDER] wrote {sc.render.filepath}.png")

aspect = sc.render.resolution_y / float(sc.render.resolution_x)
body_h = max(size.z, max(size.x, size.y) * aspect) * 1.10
head = Vector((ctr.x, ctr.y, lo.z + size.z * 0.90))
head_h = size.z * 0.26
hands = Vector((ctr.x, ctr.y, lo.z + size.z * 0.55))

# NOTE: Workbench WIREFRAME shading is viewport-only -- it renders BLANK offscreen.
# Topology loss is read instead from faceting in the solid+cavity render, which is
# why show_cavity is on above. Do not re-add a wireframe pass expecting it to work.
sh.type = 'SOLID'
shoot(f"{label}_front_solid", 0, 0, ctr, body_h)
shoot(f"{label}_threequarter_solid", 40, 0, ctr, body_h)
shoot(f"{label}_side_solid", 90, 0, ctr, body_h)
shoot(f"{label}_head_solid", 15, 0, head, head_h)
shoot(f"{label}_torsohands_solid", 0, 0, hands, size.z * 0.42)
print("[RENDER] RENDER_OK")
