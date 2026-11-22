"""
verify_all_solids_unroll.py
===========================
Tests topological cut loop extraction across all 50 solids in 1.5.2_TUBEFRAME-26.STEP
"""
import sys
sys.path.append(r'C:\Program Files\FreeCAD 1.1\bin')
import FreeCAD
import Part
import numpy as np

shape = Part.Shape()
shape.read('1.5.2_TUBEFRAME-26.STEP')

print(f"Testing all {len(shape.Solids)} solids:")

failed = []
success = []

for sid, s in enumerate(shape.Solids):
    # Find all cylindrical faces and group by radius
    cyl_faces = []
    for f in s.Faces:
        if hasattr(f.Surface, 'Radius'):
            cyl_faces.append((f.Surface.Radius, f))
    if not cyl_faces:
        continue
    cyl_faces.sort(key=lambda x: x[0], reverse=True)
    outer_r = cyl_faces[0][0]
    
    # Check if tube-like (outer radius ~12.7 or ~15.88 mm)
    if outer_r < 10.0:
        continue

    def is_outer_cyl(f):
        return hasattr(f.Surface, 'Radius') and abs(f.Surface.Radius - outer_r) < 0.2

    # Find true cut edges: shared by an outer cylinder face AND a non-outer-cylinder face
    cut_edges = []
    for e in s.Edges:
        adj_faces = [f for f in s.Faces if any(e2.isSame(e) for e2 in f.Edges)]
        cyl_adj = [f for f in adj_faces if is_outer_cyl(f)]
        non_cyl_adj = [f for f in adj_faces if not is_outer_cyl(f)]
        if len(cyl_adj) >= 1 and len(non_cyl_adj) >= 1:
            cut_edges.append(e)

    # Topological loop grouping
    loops = []
    rem = list(cut_edges)
    while rem:
        cur = [rem.pop(0)]
        ch = True
        while ch:
            ch = False
            for i, e in enumerate(rem):
                if any(v1.Point.distanceToPoint(v2.Point) < 1e-2 for le in cur for v1 in e.Vertexes for v2 in le.Vertexes):
                    cur.append(rem.pop(i))
                    ch = True
                    break
        loops.append(cur)

    # Sort loops by z position
    outer_faces = [f for f in s.Faces if is_outer_cyl(f)]
    axis = np.array([outer_faces[0].Surface.Axis.x, outer_faces[0].Surface.Axis.y, outer_faces[0].Surface.Axis.z])
    axis = axis / np.linalg.norm(axis)
    center = np.array([outer_faces[0].Surface.Center.x, outer_faces[0].Surface.Center.y, outer_faces[0].Surface.Center.z])

    loop_info = []
    for l_idx, loop in enumerate(loops):
        pts = []
        for e in loop:
            pts.extend([e.valueAt(p) for p in np.linspace(e.FirstParameter, e.LastParameter, 5)])
        z_avg = np.mean([np.dot(np.array([p.x, p.y, p.z]) - center, axis) for p in pts])
        z_min = min([np.dot(np.array([p.x, p.y, p.z]) - center, axis) for p in pts])
        z_max = max([np.dot(np.array([p.x, p.y, p.z]) - center, axis) for p in pts])
        loop_info.append((z_avg, z_max - z_min, len(loop)))

    loop_info.sort(key=lambda x: x[0])

    if len(loop_info) >= 2:
        success.append(sid + 1)
        end1_depth = loop_info[0][1]
        end2_depth = loop_info[-1][1]
        print(f"Solid #{sid+1:02d} | OD={outer_r*2/25.4:.2f}\" | Loops={len(loop_info)} | End 1 notch={end1_depth/25.4:.2f}\" | End 2 notch={end2_depth/25.4:.2f}\"")
    else:
        failed.append((sid + 1, len(loop_info)))
        print(f"Solid #{sid+1:02d} | OD={outer_r*2/25.4:.2f}\" | Loops={len(loop_info)} [CHECK]")

print(f"\nResult: {len(success)} solids successfully extracted, {len(failed)} need check.")
