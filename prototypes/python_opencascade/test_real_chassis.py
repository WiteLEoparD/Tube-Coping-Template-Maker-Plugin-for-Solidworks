"""
test_real_chassis.py
====================
Runs template extraction on 2.20-TUBE-FRAME.STEP enforcing the
user-specified intersection policy:
"If tubes intersect, ignore them and put out an error/warning.
Don't try to figure it out. I will provide a file where tubes don't intersect."
"""
import sys
import os
import glob
sys.path.append(r'C:\Program Files\FreeCAD 1.1\bin')
sys.path.append(os.path.dirname(os.path.abspath(__file__)))

from bsae_rbf_policy import BSAERBFPolicyManager
from opencascade_unroller import OpenCASCADEUnroller
from letter_sheet_generator import LetterSheetGenerator
import numpy as np

STEP_FILE = "2.20-TUBE-FRAME.STEP"

print("=" * 80)
print(f"PROCESSING CAD MODEL: {STEP_FILE}")
print("Enforcing user intersection policy: Intersecting tubes are ignored with warnings.")
print("=" * 80)

# Detected chassis nodes from CAD coordinates
REAL_NODES = {
    # Upper Rear Roll Hoop Nodes
    "B_L": np.array([-196.9, 412.8, 1158.5]),
    "B_R": np.array([ 196.9, 412.8, 1158.5]),
    # Intermediate Rear Roll Hoop Nodes (Side Impact / Lower Bracing)
    "S_L": np.array([-258.5, 409.5, 663.9]),
    "S_R": np.array([ 258.5, 409.5, 663.9]),
    # Lower Rear Roll Hoop Nodes
    "A_L": np.array([-369.0, 599.5, -160.6]),
    "A_R": np.array([ 330.8, 602.2, -550.6]),
    # Rear Braced Frame Aft Apex Nodes
    "R_L": np.array([-200.1, 1171.1, -313.8]),
    "R_R": np.array([ 195.4, 1181.8, -316.7]),
    # Secondary Rear Nodes
    "R2_L": np.array([-223.8, 1207.5, 524.9]),
    "R2_R": np.array([ 223.8, 1207.5, 524.9]),
    # Front Roll Hoop / Cockpit Nodes
    "C_L": np.array([-114.3, 0.0, 571.8]),
    "C_R": np.array([ 115.6, 0.0, 568.3]),
    "D_L": np.array([-316.4, -13.0, 3.5]),
    "D_R": np.array([ 318.3, 1.7, -2.2]),
}

policy_mgr = BSAERBFPolicyManager()
for name, pt in REAL_NODES.items():
    policy_mgr.register_node(name, pt)

out_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "real_chassis_templates")
os.makedirs(out_dir, exist_ok=True)

# Clean out old templates
for old_file in glob.glob(os.path.join(out_dir, "*.*")):
    try:
        os.remove(old_file)
    except Exception:
        pass

sheet_gen = LetterSheetGenerator(output_dir=out_dir)

unroller = OpenCASCADEUnroller(collar_length_mm=38.1, detect_collisions=True)
tubes = unroller.load_and_process_step(STEP_FILE)

print(f"\nSuccessfully validated and unrolled {len(tubes)} non-intersecting tubes.")
print(f"Ignored {len(unroller.ignored_tubes_log)} intersecting/abnormal tubes per user policy.\n")

valid_generated = []

for idx, tube in enumerate(tubes):
    start_pt = tube.end_1.node_point_3d if tube.end_1 else tube.axis_center
    end_pt = tube.end_2.node_point_3d if tube.end_2 else tube.axis_center

    start_node, dist_s = policy_mgr.identify_closest_node(start_pt, max_distance_mm=150.0)
    end_node, dist_e = policy_mgr.identify_closest_node(end_pt, max_distance_mm=150.0)

    tube.end_1.node_name = start_node
    tube.end_1.centerline_offset_mm = dist_s
    tube.end_2.node_name = end_node
    tube.end_2.centerline_offset_mm = dist_e

    tube.tube_name = f"Member_{start_node or 'Node'}_{end_node or 'Node'}"
    paths = sheet_gen.generate_all_formats(tube, prefix=f"Chassis_{tube.tube_id}_")
    valid_generated.append((tube, paths))
    
    print(f"[{len(valid_generated)}/{len(tubes)}] {tube.tube_id}: {tube.tube_name}")
    print(f"     OD: {tube.tube_od_mm/25.4:.3f}\" | Wall: {tube.tube_wall_mm/25.4:.3f}\" | Cut Length: {tube.total_tube_length_mm/25.4:.2f}\"")
    print(f"     End 1: Node {start_node} (Offset: {dist_s/25.4:.2f}\") | Notch Depth: {tube.end_1.cut_depth_mm/25.4:.3f}\"")
    print(f"     End 2: Node {end_node} (Offset: {dist_e/25.4:.2f}\") | Notch Depth: {tube.end_2.cut_depth_mm/25.4:.3f}\"")
    print(f"     PDF: {os.path.basename(paths['pdf'])}")
    print(f"     DXF: {os.path.basename(paths['dxf'])}")
    print()

print("=" * 80)
print(f"SUMMARY REPORT FOR {STEP_FILE}:")
print(f"  Valid 1:1 Templates Generated: {len(valid_generated)}")
print(f"  Ignored Intersecting Tubes:    {len(unroller.ignored_tubes_log)}")
print(f"  Output Directory:              {out_dir}")
print("=" * 80)

if unroller.ignored_tubes_log:
    print("\nIGNORED TUBES DETAIL (Intersecting bodies in CAD):")
    for tid, reason in unroller.ignored_tubes_log:
        print(f"  * {tid}: {reason}")
