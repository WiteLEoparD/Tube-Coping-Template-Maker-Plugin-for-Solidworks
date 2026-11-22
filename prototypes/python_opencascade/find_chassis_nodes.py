"""
find_chassis_nodes.py
=====================
Extracts real intersection nodes from 1.5.2_TUBEFRAME-26.STEP
"""
import sys
sys.path.append(r'C:\Program Files\FreeCAD 1.1\bin')
import FreeCAD
import Part
import numpy as np

shape = Part.Shape()
shape.read('1.5.2_TUBEFRAME-26.STEP')

# Collect all tube endpoints
all_endpoints = []

for i, s in enumerate(shape.Solids):
    cyl_faces = []
    for f in s.Faces:
        if hasattr(f.Surface, 'Radius'):
            cyl_faces.append((f.Surface.Radius, f.Surface.Axis, f.Surface.Center))
    if not cyl_faces:
        continue
    cyl_faces.sort(key=lambda x: x[0], reverse=True)
    r, axis, center = cyl_faces[0]
    od_in = r * 2.0 / 25.4
    
    ax = np.array([axis.x, axis.y, axis.z], dtype=float)
    ax = ax / np.linalg.norm(ax)
    c = np.array([center.x, center.y, center.z], dtype=float)

    pts = [np.array([v.Point.x, v.Point.y, v.Point.z]) for v in s.Vertexes]
    projs = [np.dot(p - c, ax) for p in pts]
    z_min, z_max = min(projs), max(projs)

    p1 = c + z_min * ax
    p2 = c + z_max * ax
    length_in = (z_max - z_min) / 25.4

    if length_in > 1.5:  # skip tiny filler stubs
        all_endpoints.append((p1, f"Solid_{i+1}_End1", i+1, od_in))
        all_endpoints.append((p2, f"Solid_{i+1}_End2", i+1, od_in))

# Cluster endpoints within 60 mm (2.36 inches) to identify chassis nodes
clusters = []
visited = set()

for idx, (pt, name, s_id, od) in enumerate(all_endpoints):
    if idx in visited:
        continue
    cluster = [(pt, name, s_id, od)]
    visited.add(idx)
    for other_idx, (other_pt, other_name, other_sid, other_od) in enumerate(all_endpoints):
        if other_idx in visited:
            continue
        if np.linalg.norm(pt - other_pt) < 65.0:  # ~2.5 inches
            cluster.append((other_pt, other_name, other_sid, other_od))
            visited.add(other_idx)
    clusters.append(cluster)

# Filter clusters with >= 2 members
nodes = []
for c_idx, c in enumerate(clusters):
    if len(c) >= 2:
        pts = np.array([item[0] for item in c])
        avg_pt = np.mean(pts, axis=0)
        members = [f"#{item[2]} ({item[3]:.2f}\")" for item in c]
        nodes.append({
            "node_id": len(nodes) + 1,
            "center": avg_pt,
            "members": members,
            "count": len(c)
        })

# Sort nodes by Z descending, then Y descending
nodes.sort(key=lambda n: (n["center"][2], n["center"][1]), reverse=True)

print(f"\nDiscovered {len(nodes)} Structural Nodes in 1.5.2_TUBEFRAME-26.STEP:\n")
for n in nodes:
    cp = n["center"]
    print(f"Node #{n['node_id']:02d} | Coord (mm): ({cp[0]:6.1f}, {cp[1]:6.1f}, {cp[2]:6.1f}) | Coord (in): ({cp[0]/25.4:5.1f}, {cp[1]/25.4:5.1f}, {cp[2]/25.4:5.1f}) | Members ({n['count']}): {', '.join(n['members'][:5])}")
