"""
inspect_real_chassis.py
=======================
Detailed inspection of 1.5.2_TUBEFRAME-26.STEP
"""
import sys
sys.path.append(r'C:\Program Files\FreeCAD 1.1\bin')
import FreeCAD
import Part
import numpy as np

shape = Part.Shape()
shape.read('1.5.2_TUBEFRAME-26.STEP')

print(f"Total solids: {len(shape.Solids)}")

tube_data = []

for i, s in enumerate(shape.Solids):
    cyl_faces = []
    for f in s.Faces:
        if hasattr(f.Surface, 'Radius'):
            cyl_faces.append((f.Surface.Radius, f.Surface.Axis, f.Surface.Center))
    if not cyl_faces:
        continue
    cyl_faces.sort(key=lambda x: x[0], reverse=True)
    r, axis, center = cyl_faces[0]
    od_mm = r * 2.0
    od_in = od_mm / 25.4

    ax = np.array([axis.x, axis.y, axis.z], dtype=float)
    ax = ax / np.linalg.norm(ax)
    c = np.array([center.x, center.y, center.z], dtype=float)

    pts = [np.array([v.Point.x, v.Point.y, v.Point.z]) for v in s.Vertexes]
    projs = [np.dot(p - c, ax) for p in pts]
    z_min, z_max = min(projs), max(projs)

    p1 = c + z_min * ax
    p2 = c + z_max * ax
    length_mm = z_max - z_min
    length_in = length_mm / 25.4

    tube_data.append({
        "id": i + 1,
        "solid": s,
        "od_in": od_in,
        "length_in": length_in,
        "p1": p1,
        "p2": p2,
        "axis": ax,
        "center": c,
        "bb": s.BoundBox
    })

    print(f"Solid #{i+1:02d} | OD={od_in:.3f}\" | Len={length_in:5.1f}\" | P1=({p1[0]:6.1f},{p1[1]:6.1f},{p1[2]:6.1f}) -> P2=({p2[0]:6.1f},{p2[1]:6.1f},{p2[2]:6.1f}) | Axis=({ax[0]:.2f},{ax[1]:.2f},{ax[2]:.2f})")

# Find the highest point (top of roll cage)
all_z = [max(t["p1"][2], t["p2"][2]) for t in tube_data]
max_z = max(all_z)
print(f"\nHighest Z point in frame: {max_z:.1f} mm ({max_z/25.4:.1f} in)")

# Find min and max Y (front to rear)
all_y = [t["center"][1] for t in tube_data]
print(f"Y range: min={min(all_y):.1f} mm, max={max_y if 'max_y' in locals() else max(all_y):.1f} mm")
