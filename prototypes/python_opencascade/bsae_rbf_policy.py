"""
bsae_rbf_policy.py
==================
Baja SAE (BSAE) Rules 2027, Revision A - Roll Cage & Rear Braced Frame (RBF)
Node Policy and Member Classification Specification.

References:
- Baja SAE Rules 2027 Rev A, Article B.3 (Roll Cage Objective and Requirements)
- Technical Inspection Roll Cage Spec Sheet (CDP / SES)
- Rear Braced Frame (RBF) Named Points & Triangulation Nodes
"""

from dataclasses import dataclass, field
from typing import Dict, List, Optional, Tuple
import math
import numpy as np


# Standard BSAE Tube Specifications (inches & mm)
PRIMARY_MIN_OD_IN = 1.250     # 31.75 mm
PRIMARY_MIN_WALL_IN = 0.065   # 1.65 mm
PRIMARY_ALT_OD_IN = 1.000     # 25.40 mm
PRIMARY_ALT_WALL_IN = 0.095   # 2.41 mm

SECONDARY_MIN_OD_IN = 1.000   # 25.40 mm
SECONDARY_MIN_WALL_IN = 0.035 # 0.89 mm

# BSAE Node Policy: Maximum allowable centerline offset at any node
MAX_NODE_OFFSET_IN = 2.000    # 50.8 mm
MAX_NODE_OFFSET_MM = 50.800


@dataclass
class BSAENode:
    """Represents a named node (Point) per BSAE Rules 2027 Rev A."""
    name: str              # e.g., 'B_L', 'B_R', 'S_L', 'R_L', 'RLC_L', etc.
    description: str       # Full description per rulebook
    point_xyz: np.ndarray  # Coordinate in mm (or inches)
    is_primary_node: bool = True
    connected_members: List[str] = field(default_factory=list)


# Standard Named Points in BSAE 2027 Rev A Rear Braced Frame (RBF)
STANDARD_RBF_NODES: Dict[str, dict] = {
    # Upper Rear Roll Hoop Nodes
    "B_L": {
        "description": "Rear Roll Hoop Left Upper Corner (RRH / RHO / FAB_Upper)",
        "role": "Primary Roll Cage Node (Article B.3.2)",
    },
    "B_R": {
        "description": "Rear Roll Hoop Right Upper Corner (RRH / RHO / FAB_Upper)",
        "role": "Primary Roll Cage Node (Article B.3.2)",
    },
    # Intermediate Rear Roll Hoop Nodes (Side Impact / Lower Bracing)
    "S_L": {
        "description": "Rear Roll Hoop Left Intermediate Node (RRH / SIM / FAB_Lower)",
        "role": "Primary Roll Cage Node (Article B.3.2)",
    },
    "S_R": {
        "description": "Rear Roll Hoop Right Intermediate Node (RRH / SIM / FAB_Lower)",
        "role": "Primary Roll Cage Node (Article B.3.2)",
    },
    # Lower Rear Roll Hoop Nodes
    "A_L": {
        "description": "Rear Roll Hoop Left Bottom Node (RRH / LFS / LC)",
        "role": "Primary Roll Cage Node (Article B.3.2)",
    },
    "A_R": {
        "description": "Rear Roll Hoop Right Bottom Node (RRH / LFS / LC)",
        "role": "Primary Roll Cage Node (Article B.3.2)",
    },
    # Rear Braced Frame Apex / Aft Vertices
    "R_L": {
        "description": "Rear Braced Frame Left Aft Vertex (FAB_Upper_L / FAB_Lower_L / RLC)",
        "role": "Rear Bracing Primary Node (Article B.3.2.13)",
    },
    "R_R": {
        "description": "Rear Braced Frame Right Aft Vertex (FAB_Upper_R / FAB_Lower_R / RLC)",
        "role": "Rear Bracing Primary Node (Article B.3.2.13)",
    },
    # Roll Hoop Overhead / Front Nodes (for mating context)
    "C_L": {
        "description": "Roll Hoop Overhead Left Forward Node (RHO / FBM)",
        "role": "Primary Roll Cage Node (Article B.3.2)",
    },
    "C_R": {
        "description": "Roll Hoop Overhead Right Forward Node (RHO / FBM)",
        "role": "Primary Roll Cage Node (Article B.3.2)",
    },
    "D_L": {
        "description": "Front Roll Hoop Left Lower Node (FBM / SIM / LFS)",
        "role": "Primary Roll Cage Node (Article B.3.2)",
    },
    "D_R": {
        "description": "Front Roll Hoop Right Lower Node (FBM / SIM / LFS)",
        "role": "Primary Roll Cage Node (Article B.3.2)",
    },
}


# Standard Tube Member Definitions in BSAE Rear Braced Frame
STANDARD_RBF_MEMBERS: Dict[str, dict] = {
    "FAB_Upper_L": {
        "display_name": "Rear Upper Brace - Left (FAB-U-L)",
        "start_node": "B_L",
        "end_node": "R_L",
        "category": "Primary Member (Rear Bracing)",
        "rule_ref": "BSAE 2027 Rev A - Art. B.3.2.13",
        "min_od_in": 1.250,
        "min_wall_in": 0.065,
    },
    "FAB_Upper_R": {
        "display_name": "Rear Upper Brace - Right (FAB-U-R)",
        "start_node": "B_R",
        "end_node": "R_R",
        "category": "Primary Member (Rear Bracing)",
        "rule_ref": "BSAE 2027 Rev A - Art. B.3.2.13",
        "min_od_in": 1.250,
        "min_wall_in": 0.065,
    },
    "FAB_Lower_L": {
        "display_name": "Rear Lower Brace - Left (FAB-L-L)",
        "start_node": "S_L",
        "end_node": "R_L",
        "category": "Primary Member (Rear Bracing)",
        "rule_ref": "BSAE 2027 Rev A - Art. B.3.2.13",
        "min_od_in": 1.250,
        "min_wall_in": 0.065,
    },
    "FAB_Lower_R": {
        "display_name": "Rear Lower Brace - Right (FAB-L-R)",
        "start_node": "S_R",
        "end_node": "R_R",
        "category": "Primary Member (Rear Bracing)",
        "rule_ref": "BSAE 2027 Rev A - Art. B.3.2.13",
        "min_od_in": 1.250,
        "min_wall_in": 0.065,
    },
    "RLC": {
        "display_name": "Rear Lateral Crossmember (RLC)",
        "start_node": "R_L",
        "end_node": "R_R",
        "category": "Primary Member (Lateral Crossmember)",
        "rule_ref": "BSAE 2027 Rev A - Art. B.3.2.13.2",
        "min_od_in": 1.000,
        "min_wall_in": 0.065,
    },
    "RRH_Upper": {
        "display_name": "Rear Roll Hoop Upper Crossmember",
        "start_node": "B_L",
        "end_node": "B_R",
        "category": "Primary Member (Roll Hoop)",
        "rule_ref": "BSAE 2027 Rev A - Art. B.3.2.1",
        "min_od_in": 1.250,
        "min_wall_in": 0.065,
    },
    "RRH_Diag": {
        "display_name": "Rear Roll Hoop Diagonal Bracing",
        "start_node": "B_L",
        "end_node": "S_R",
        "category": "Primary Member (Triangulation)",
        "rule_ref": "BSAE 2027 Rev A - Art. B.3.2.4",
        "min_od_in": 1.000,
        "min_wall_in": 0.065,
    },
    "LC_Intermediate": {
        "display_name": "Lateral Crossmember at S (LC-S)",
        "start_node": "S_L",
        "end_node": "S_R",
        "category": "Primary Member (Lateral Crossmember)",
        "rule_ref": "BSAE 2027 Rev A - Art. B.3.2.5",
        "min_od_in": 1.000,
        "min_wall_in": 0.065,
    },
    "LC_Lower": {
        "display_name": "Lower Lateral Crossmember (LC-A)",
        "start_node": "A_L",
        "end_node": "A_R",
        "category": "Primary Member (Lateral Crossmember)",
        "rule_ref": "BSAE 2027 Rev A - Art. B.3.2.5",
        "min_od_in": 1.000,
        "min_wall_in": 0.065,
    },
}


class BSAERBFPolicyManager:
    """
    Manages node validation, automatic member classification, and labeling
    for cut templates per BSAE Rules 2027 Rev A.
    """

    def __init__(self, custom_nodes: Optional[Dict[str, np.ndarray]] = None):
        self.nodes: Dict[str, BSAENode] = {}
        if custom_nodes:
            for name, coord in custom_nodes.items():
                desc = STANDARD_RBF_NODES.get(name, {}).get("description", f"User Node {name}")
                self.nodes[name] = BSAENode(name=name, description=desc, point_xyz=np.array(coord, dtype=float))

    def register_node(self, name: str, coord: np.ndarray, description: str = ""):
        if not description:
            description = STANDARD_RBF_NODES.get(name, {}).get("description", f"Node {name}")
        self.nodes[name] = BSAENode(name=name, description=description, point_xyz=np.array(coord, dtype=float))

    def identify_closest_node(self, endpoint: np.ndarray, max_distance_mm: float = MAX_NODE_OFFSET_MM) -> Tuple[Optional[str], float]:
        """
        Finds the closest BSAE named node to a given 3D tube cut endpoint.
        Returns (node_name, distance_mm).
        """
        if not self.nodes:
            return None, float("inf")

        closest_node = None
        min_dist = float("inf")

        for name, node in self.nodes.items():
            dist = np.linalg.norm(node.point_xyz - endpoint)
            if dist < min_dist:
                min_dist = dist
                closest_node = name

        if min_dist <= max_distance_mm:
            return closest_node, min_dist
        return closest_node, min_dist

    def classify_member(self, start_pt: np.ndarray, end_pt: np.ndarray, member_name_hint: str = "") -> dict:
        """
        Classifies a tube member between start_pt and end_pt according to BSAE RBF rules.
        """
        start_node, dist_start = self.identify_closest_node(start_pt)
        end_node, dist_end = self.identify_closest_node(end_pt)

        # Check if matches standard member
        matched_member_key = None
        for key, mem in STANDARD_RBF_MEMBERS.items():
            # Check direct or reverse match
            if (start_node == mem["start_node"] and end_node == mem["end_node"]) or \
               (start_node == mem["end_node"] and end_node == mem["start_node"]):
                matched_member_key = key
                break

        # Fallback to name hint if available
        if not matched_member_key and member_name_hint:
            for key in STANDARD_RBF_MEMBERS:
                if key.lower() in member_name_hint.lower():
                    matched_member_key = key
                    break

        member_info = STANDARD_RBF_MEMBERS.get(matched_member_key, {
            "display_name": member_name_hint or "Chassis Member",
            "start_node": start_node or "Node_1",
            "end_node": end_node or "Node_2",
            "category": "Structural Member",
            "rule_ref": "BSAE 2027 Rev A - Section B.3",
            "min_od_in": 1.000,
            "min_wall_in": 0.035,
        })

        # Check compliance with Node Policy (offset <= 2.0 inches / 50.8 mm)
        offset_compliant = (dist_start <= MAX_NODE_OFFSET_MM) and (dist_end <= MAX_NODE_OFFSET_MM)

        return {
            "member_key": matched_member_key or member_name_hint or "Tube_Member",
            "display_name": member_info["display_name"],
            "start_node": start_node,
            "start_node_dist_mm": dist_start,
            "end_node": end_node,
            "end_node_dist_mm": dist_end,
            "category": member_info["category"],
            "rule_ref": member_info["rule_ref"],
            "min_od_in": member_info["min_od_in"],
            "min_wall_in": member_info["min_wall_in"],
            "node_offset_compliant": offset_compliant,
        }

    def format_end_label(
        self,
        tube_name: str,
        end_idx: int, # 1 or 2
        node_name: Optional[str],
        mating_member: str,
        tube_od_in: float,
        tube_wall_in: float,
        cut_angle_deg: Optional[float] = None,
        node_offset_mm: Optional[float] = None
    ) -> List[str]:
        """
        Creates the standardized text block to print on the 1:1 cut template.
        """
        node_str = f"NODE: {node_name}" if node_name else "NODE: UNMAPPED"
        if node_offset_mm is not None and node_offset_mm < float("inf"):
            node_str += f" (Offset: {node_offset_mm:.1f} mm / {node_offset_mm/25.4:.2f}\")"

        compliance = "[COMPLIANT <= 2.0\" OFFSET]" if (node_offset_mm and node_offset_mm <= MAX_NODE_OFFSET_MM) else "[CHECK OFFSET]"

        lines = [
            f"BSAE 2027 REV A - REAR BRACED FRAME",
            f"TUBE: {tube_name} | END {end_idx}",
            f"{node_str} {compliance}",
            f"MATING TARGET: {mating_member}",
            f"SPEC: OD {tube_od_in:.3f}\" x {tube_wall_in:.3f}\" Wall ({tube_od_in*25.4:.1f}x{tube_wall_in*25.4:.1f}mm)",
        ]
        if cut_angle_deg is not None:
            lines.append(f"INTERSECTION ANGLE: {cut_angle_deg:.1f} deg")

        lines.append("CENTERLINE [CL]: ALIGN WITH TUBE MARKED SEAM")
        return lines
