"""
auto_template_cli.py
====================
Main Command-Line Application for Auto-Generating 1:1 Letter Tube Cut Templates
from SolidWorks CAD models via OpenCASCADE / FreeCAD Python API.

Complies with BSAE Rules 2027, Revision A - Rear Braced Frame (RBF) Node Policy.
"""

import os
import sys
import argparse
import numpy as np

from bsae_rbf_policy import BSAERBFPolicyManager, STANDARD_RBF_NODES, STANDARD_RBF_MEMBERS
from opencascade_unroller import OpenCASCADEUnroller, generate_analytical_coped_tube, UnwrappedTube
from letter_sheet_generator import LetterSheetGenerator


def process_cad_model(
    input_file: str,
    output_dir: str = "./output_templates",
    collar_in: float = 1.5,
    custom_node_coords: dict = None
):
    """
    Loads a SolidWorks-exported STEP/IGES model, extracts complex cut profiles
    using FreeCAD/OpenCASCADE, maps ends to BSAE 2027 Rev A RBF nodes, and
    generates 1:1 Letter PDF, SVG, and DXF templates.
    """
    print("=" * 80)
    print(" BSAE 2027 REV A - AUTOMATIC TUBE CUT TEMPLATE GENERATOR ")
    print("=" * 80)
    print(f"Input CAD File:      {input_file}")
    print(f"Output Directory:    {output_dir}")
    print(f"Paper Size:          US Letter (8.5\" x 11.0\") - True 1:1 Scale")
    print(f"Paper-Saving Collar: {collar_in:.2f} inches ({collar_in*25.4:.1f} mm)")
    print("-" * 80)

    os.makedirs(output_dir, exist_ok=True)
    collar_mm = collar_in * 25.4

    # Initialize BSAE Policy Manager with standard or custom node locations
    policy_mgr = BSAERBFPolicyManager()
    if custom_node_coords:
        for node_name, coord in custom_node_coords.items():
            policy_mgr.register_node(node_name, coord)
    else:
        # Default BSAE 2027 Rev A Rear Braced Frame Node Coordinates (mm)
        default_nodes = {
            "B_L": np.array([-100.0, 254.0, 1041.0]),
            "B_R": np.array([-100.0, -254.0, 1041.0]),
            "S_L": np.array([-50.0, 280.0, 508.0]),
            "S_R": np.array([-50.0, -280.0, 508.0]),
            "A_L": np.array([0.0, 300.0, 50.0]),
            "A_R": np.array([0.0, -300.0, 50.0]),
            "R_L": np.array([-650.0, 220.0, 480.0]),
            "R_R": np.array([-650.0, -220.0, 480.0]),
        }
        for name, pt in default_nodes.items():
            policy_mgr.register_node(name, pt)

    # Initialize OpenCASCADE unroller
    unroller = OpenCASCADEUnroller(collar_length_mm=collar_mm)
    sheet_gen = LetterSheetGenerator(output_dir=output_dir)

    # Check file format
    ext = os.path.splitext(input_file)[1].lower()

    if ext in [".step", ".stp", ".iges", ".igs", ".brep"]:
        print("Processing exact B-Rep geometry via OpenCASCADE / FreeCAD Part module...")
        tubes = unroller.load_and_process_step(input_file)
    else:
        raise ValueError(f"Unsupported CAD format: {ext}. Please use STEP (.step, .stp) or IGES.")

    if not tubes:
        print("Warning: No tubular cylindrical bodies found in the CAD file.")
        return []

    print(f"Successfully identified {len(tubes)} tube bodies with complex copes.")
    print("-" * 80)

    results = []

    for idx, tube in enumerate(tubes):
        # Classify member and ends per BSAE Rules 2027 Rev A
        start_pt = tube.end_1.node_point_3d if tube.end_1 else tube.axis_center
        end_pt = tube.end_2.node_point_3d if tube.end_2 else tube.axis_center

        classification = policy_mgr.classify_member(start_pt, end_pt, member_name_hint=tube.tube_id)
        tube.bsae_classification = classification
        tube.tube_name = classification["display_name"]

        # Assign node names to ends
        if tube.end_1:
            tube.end_1.node_name = classification["start_node"]
            tube.end_1.centerline_offset_mm = classification["start_node_dist_mm"]
            tube.end_1.mating_target = f"Target at {classification['start_node']}"

        if tube.end_2:
            tube.end_2.node_name = classification["end_node"]
            tube.end_2.centerline_offset_mm = classification["end_node_dist_mm"]
            tube.end_2.mating_target = f"Target at {classification['end_node']}"

        # Generate 1:1 Letter Templates (PDF, SVG, DXF)
        file_paths = sheet_gen.generate_all_formats(tube, prefix=f"{idx+1:02d}_")

        print(f"[{idx+1}/{len(tubes)}] TUBE: {tube.tube_name}")
        print(f"     Role:           {classification['category']}")
        print(f"     Rule Ref:       {classification['rule_ref']}")
        print(f"     Dimensions:     OD {tube.tube_od_mm/25.4:.3f}\" ({tube.tube_od_mm:.1f}mm) x {tube.tube_wall_mm/25.4:.3f}\" Wall")
        print(f"     Cut Length:     {tube.total_tube_length_mm/25.4:.2f}\" ({tube.total_tube_length_mm:.1f}mm)")
        if tube.end_1:
            print(f"     End 1 (Start):  Node {tube.end_1.node_name} | Notch Depth: {tube.end_1.cut_depth_mm/25.4:.3f}\" | Offset: {tube.end_1.centerline_offset_mm/25.4:.2f}\"")
        if tube.end_2:
            print(f"     End 2 (End):    Node {tube.end_2.node_name} | Notch Depth: {tube.end_2.cut_depth_mm/25.4:.3f}\" | Offset: {tube.end_2.centerline_offset_mm/25.4:.2f}\"")
        print(f"     Letter PDF:     {file_paths['pdf']}")
        print(f"     Letter SVG:     {file_paths['svg']}")
        print(f"     AutoCAD DXF:    {file_paths['dxf']}")
        print()

        results.append({
            "tube": tube,
            "classification": classification,
            "files": file_paths
        })

    print("=" * 80)
    print(" BATCH TEMPLATE GENERATION COMPLETE")
    print(f" Generated 1:1 true-scale templates in: {os.path.abspath(output_dir)}")
    print("=" * 80)
    return results


def run_parametric_demo(output_dir: str = "./demo_templates"):
    """
    Generates a demonstration set of standard BSAE 2027 Rev A Rear Braced Frame
    tubes (FAB Upper, FAB Lower, RLC) with exact analytical coping and creates
    the 1:1 Letter PDF, SVG, and DXF templates.
    """
    print("=" * 80)
    print(" GENERATING PARAMETRIC BSAE 2027 REV A RBF DEMO TEMPLATES ")
    print("=" * 80)

    os.makedirs(output_dir, exist_ok=True)
    sheet_gen = LetterSheetGenerator(output_dir=output_dir)

    # 1. FAB_Upper_L (Rear Upper Brace Left: Node B_L to Node R_L)
    # 1.25" OD x 0.065" wall, length ~38.5", coped at B_L around 1.25" RRH at 45 deg, coped at R_L around 1.00" RLC at 60 deg
    fab_upper_l = generate_analytical_coped_tube(
        tube_name="FAB_Upper_L",
        tube_od_in=1.250,
        tube_wall_in=0.065,
        length_in=38.5,
        end1_target_od_in=1.250,
        end1_angle_deg=45.0,
        end1_offset_in=0.0,
        end2_target_od_in=1.000,
        end2_angle_deg=60.0,
        end2_offset_in=0.0,
        clocking_angle_deg=15.0,  # 15 degree compound clocking between end notches
        collar_len_in=1.5
    )
    fab_upper_l.end_1.node_name = "B_L"
    fab_upper_l.end_1.mating_target = "RRH Upper Crossmember (1.25\" OD)"
    fab_upper_l.end_2.node_name = "R_L"
    fab_upper_l.end_2.mating_target = "Rear Lateral Crossmember RLC (1.00\" OD)"

    # 2. FAB_Lower_L (Rear Lower Brace Left: Node S_L to Node R_L)
    # 1.25" OD x 0.065" wall, length ~32.0", coped at S_L around 1.25" RRH upright at 55 deg, coped at R_L around 1.00" RLC at 50 deg
    fab_lower_l = generate_analytical_coped_tube(
        tube_name="FAB_Lower_L",
        tube_od_in=1.250,
        tube_wall_in=0.065,
        length_in=32.0,
        end1_target_od_in=1.250,
        end1_angle_deg=55.0,
        end1_offset_in=0.0,
        end2_target_od_in=1.000,
        end2_angle_deg=50.0,
        end2_offset_in=0.0,
        clocking_angle_deg=-20.0,
        collar_len_in=1.5
    )
    fab_lower_l.end_1.node_name = "S_L"
    fab_lower_l.end_1.mating_target = "RRH Upright at SIM (1.25\" OD)"
    fab_lower_l.end_2.node_name = "R_L"
    fab_lower_l.end_2.mating_target = "Rear Lateral Crossmember RLC (1.00\" OD)"

    # 3. RLC (Rear Lateral Crossmember: Node R_L to Node R_R)
    # 1.00" OD x 0.065" wall, length ~18.0", square cut / miter
    rlc = generate_analytical_coped_tube(
        tube_name="RLC",
        tube_od_in=1.000,
        tube_wall_in=0.065,
        length_in=18.0,
        end1_target_od_in=1.250,
        end1_angle_deg=80.0,
        end1_offset_in=0.0,
        end2_target_od_in=1.250,
        end2_angle_deg=80.0,
        end2_offset_in=0.0,
        clocking_angle_deg=0.0,
        collar_len_in=1.5
    )
    rlc.end_1.node_name = "R_L"
    rlc.end_1.mating_target = "FAB Aft Junction Left"
    rlc.end_2.node_name = "R_R"
    rlc.end_2.mating_target = "FAB Aft Junction Right"

    demo_tubes = [fab_upper_l, fab_lower_l, rlc]

    for t in demo_tubes:
        paths = sheet_gen.generate_all_formats(t)
        print(f"Generated 1:1 templates for: {t.tube_id}")
        print(f"  PDF (1:1 Letter): {paths['pdf']}")
        print(f"  SVG (1:1 Vector): {paths['svg']}")
        print(f"  DXF (1:1 CAD):    {paths['dxf']}")

    print("-" * 80)
    print(f"All demo templates created in {os.path.abspath(output_dir)}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="BSAE 2027 Rev A - Tube Cut Template Generator")
    parser.add_argument("input_cad", nargs="?", default="", help="Path to exported SolidWorks STEP/IGES file")
    parser.add_argument("--output-dir", default="./output_templates", help="Output directory for 1:1 Letter templates")
    parser.add_argument("--collar", type=float, default=1.5, help="Collar straight length in inches (default: 1.5 in)")
    parser.add_argument("--demo", action="store_true", help="Run parametric demo generator for BSAE RBF")

    args = parser.parse_args()

    if args.demo or not args.input_cad:
        run_parametric_demo(output_dir=args.output_dir)
    else:
        process_cad_model(args.input_cad, output_dir=args.output_dir, collar_in=args.collar)
