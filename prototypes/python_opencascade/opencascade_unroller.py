"""
opencascade_unroller.py
=======================
Robust Tube Cut-Profile Extraction and Unwrapping Engine using
OpenCASCADE / FreeCAD Python API.

Key Engineering Highlights:
- Intersection Detection & Policy: Detects physical collisions/intersections between tube bodies.
  If tubes intersect or have abnormal penetration depths, they are ignored with a clear warning/error.
- Primary Cylinder Body Identification: Uniquely identifies the tube's own outer cylindrical skin
  via (Radius, Collinear Axis, Axis Line) geometric grouping, discarding mating/cutting faces.
- Clean Topological Loop Extraction: Discards internal CAD seams; stitches true cut edges head-to-tail.
- Smooth Single-Valued 1:1 Unrolling: Monotonic perimeter traversal eliminates spikes and comb artifacts.
- Shared 0° Clocking Reference Vector: Identical across End 1 and End 2 for rotational alignment.
- Paper-Saving Collar Truncation: Unrolls cut zone + adjustable straight collar baseline (default 1.5").
- True 1:1 Scale: Width = pi * OD, Height = notch depth + collar.
"""

from dataclasses import dataclass, field
from typing import Dict, List, Optional, Tuple, Set
import os
import sys
import math
import numpy as np


@dataclass
class TubeEndProfile:
    """Represents the unwrapped 2D cut profile for a single tube end."""
    end_index: int                       # 1 (Start) or 2 (End)
    node_name: Optional[str] = None      # e.g., 'B_L', 'R_L'
    mating_target: str = "Chassis Tube"  # e.g., "RRH (1.25\" OD)"
    tube_od_mm: float = 31.75            # Outer diameter (1.25 in)
    tube_wall_mm: float = 1.65           # Wall thickness (0.065 in)
    collar_length_mm: float = 38.1       # Paper collar straight extension (1.5 in)
    
    # 2D coordinates of the unwrapped cut profile (x in [0, pi*OD], y relative to collar baseline)
    unwrapped_cut_pts: np.ndarray = field(default_factory=lambda: np.zeros((0, 2)))
    
    # Key dimensions (in mm and inches)
    cut_depth_mm: float = 0.0            # Peak-to-valley notch depth
    template_width_mm: float = 0.0       # pi * OD
    template_height_mm: float = 0.0      # cut_depth + collar_length
    cut_angle_deg: float = 90.0          # Angle of cut relative to axis
    
    # 3D endpoints & vectors
    node_point_3d: np.ndarray = field(default_factory=lambda: np.zeros(3))
    centerline_offset_mm: float = 0.0    # Distance from cut apex to rulebook node


@dataclass
class UnwrappedTube:
    """Represents a complete chassis tube with both ends unwrapped."""
    tube_id: str
    tube_name: str
    tube_od_mm: float
    tube_wall_mm: float
    axis_vector: np.ndarray               # 3D normalized axis vector
    axis_center: np.ndarray               # Point on axis
    reference_clocking_u: np.ndarray      # Shared 3D vector representing theta=0
    
    total_tube_length_mm: float           # Tip-to-tip cut length
    center_to_center_length_mm: float     # Node-to-node length
    
    end_1: Optional[TubeEndProfile] = None
    end_2: Optional[TubeEndProfile] = None
    
    bsae_classification: dict = field(default_factory=dict)
    
    # Intersection & validation flags
    is_valid: bool = True
    has_intersection_error: bool = False
    warning_message: str = ""


def compute_shared_clocking_vector(axis: np.ndarray) -> np.ndarray:
    """
    Computes a canonical reference vector 'u' perpendicular to the tube axis 'a'.
    This vector represents theta = 0 (the reference centerline) for BOTH End 1 and End 2.
    
    Using global Z (chassis vertical) as master reference ensures that the
    marked centerline corresponds to a single continuous straight longitudinal line
    along the physical tube.
    """
    a = axis / np.linalg.norm(axis)
    global_z = np.array([0.0, 0.0, 1.0])
    
    # If the tube is nearly vertical along Z, use global Y (lateral)
    if abs(np.dot(a, global_z)) > 0.92:
        global_ref = np.array([0.0, 1.0, 0.0])
    else:
        global_ref = global_z
        
    u = global_ref - np.dot(global_ref, a) * a
    norm_u = np.linalg.norm(u)
    if norm_u < 1e-6:
        global_ref = np.array([1.0, 0.0, 0.0])
        u = global_ref - np.dot(global_ref, a) * a
        norm_u = np.linalg.norm(u)
        
    return u / norm_u


class OpenCASCADEUnroller:
    """
    Unwraps tubes from STEP / IGES / BREP files using FreeCAD's OpenCASCADE bindings.
    Enforces user policy: If tubes intersect, ignore them and output an error/warning.
    """

    def __init__(self, collar_length_mm: float = 38.1, detect_collisions: bool = True):
        self.collar_length_mm = collar_length_mm
        self.detect_collisions = detect_collisions
        self.ignored_tubes_log: List[Tuple[str, str]] = []
        self._check_freecad()

    def _check_freecad(self):
        try:
            import FreeCAD
            import Part
            self.has_freecad = True
        except ImportError:
            fc_bin = r"C:\Program Files\FreeCAD 1.1\bin"
            if os.path.exists(fc_bin) and fc_bin not in sys.path:
                sys.path.append(fc_bin)
                try:
                    import FreeCAD
                    import Part
                    self.has_freecad = True
                    return
                except ImportError:
                    pass
            self.has_freecad = False

    def load_and_process_step(self, step_filepath: str) -> List[UnwrappedTube]:
        """
        Loads a STEP / IGES file, identifies tube bodies, tests for body intersections,
        and extracts unwrapped profiles for non-intersecting tubes.
        """
        if not self.has_freecad:
            raise RuntimeError(
                "FreeCAD / OpenCASCADE API not found in the current Python environment.\n"
                "Please run using FreeCAD's Python (e.g. FreeCADCmd) or ensure FreeCAD is on sys.path."
            )

        import FreeCAD
        import Part

        if not os.path.exists(step_filepath):
            raise FileNotFoundError(f"File not found: {step_filepath}")

        shape = Part.Shape()
        shape.read(step_filepath)

        solids = shape.Solids
        if not solids:
            solids = [shape]

        self.ignored_tubes_log.clear()

        # 1. Identify tube solids (outer radius >= 8.0 mm / ~5/8" OD)
        tube_solids_info = []
        for s_idx, solid in enumerate(solids):
            info = self._get_tube_cylinder_info(solid)
            if info is not None:
                tube_solids_info.append((s_idx + 1, solid, info))

        # 2. Collision / Intersection Detection among tube bodies
        colliding_ids: Dict[int, List[Tuple[int, float]]] = {}
        if self.detect_collisions and len(tube_solids_info) > 1:
            for i in range(len(tube_solids_info)):
                id1, s1, _ = tube_solids_info[i]
                bb1 = s1.BoundBox
                for j in range(i + 1, len(tube_solids_info)):
                    id2, s2, _ = tube_solids_info[j]
                    bb2 = s2.BoundBox
                    # Fast bounding box collision check
                    if (bb1.XMax >= bb2.XMin and bb1.XMin <= bb2.XMax and
                        bb1.YMax >= bb2.YMin and bb1.YMin <= bb2.YMax and
                        bb1.ZMax >= bb2.ZMin and bb1.ZMin <= bb2.ZMax):
                        try:
                            common = s1.common(s2)
                            vol = common.Volume
                            # Overlap volume threshold > 20.0 mm^3 (ignores tangent contact)
                            if vol > 20.0:
                                colliding_ids.setdefault(id1, []).append((id2, vol))
                                colliding_ids.setdefault(id2, []).append((id1, vol))
                        except Exception:
                            pass

        tubes: List[UnwrappedTube] = []

        # 3. Process each tube body
        for tube_num, solid, cyl_info in tube_solids_info:
            tube_id = f"TUBE_{tube_num}"

            # Check if tube intersects other tubes
            if tube_num in colliding_ids:
                colliders = colliding_ids[tube_num]
                col_desc = ", ".join([f"TUBE_{other} (vol={v:.1f} mm³)" for other, v in colliders])
                warn_msg = f"TUBE_{tube_num:02d} intersects with {col_desc}. IGNORED per policy."
                print(f"[WARNING / INTERSECTION] {warn_msg}")
                self.ignored_tubes_log.append((tube_id, warn_msg))
                # Skip generating template per user request
                continue

            tube = self._process_solid_with_info(solid, tube_id=tube_id, cyl_info=cyl_info)
            if tube:
                # Check for abnormal notch depth (indicates penetrating cut)
                max_allowed_notch = tube.tube_od_mm * 3.5
                d1 = tube.end_1.cut_depth_mm if tube.end_1 else 0.0
                d2 = tube.end_2.cut_depth_mm if tube.end_2 else 0.0

                if d1 > max_allowed_notch or d2 > max_allowed_notch:
                    deepest = max(d1, d2)
                    warn_msg = (
                        f"{tube_id} has abnormal cut notch depth ({deepest/25.4:.2f}\" > {max_allowed_notch/25.4:.2f}\") "
                        f"indicating mid-body intersection. IGNORED per policy."
                    )
                    print(f"[WARNING / ABNORMAL NOTCH] {warn_msg}")
                    self.ignored_tubes_log.append((tube_id, warn_msg))
                    continue

                tubes.append(tube)

        return tubes

    def _get_tube_cylinder_info(self, solid) -> Optional[dict]:
        """
        Identifies the primary tube cylindrical skin by geometric grouping:
        same radius, collinear axis direction, and coincident axis line.
        """
        cyl_faces = []
        for face in solid.Faces:
            if hasattr(face.Surface, "Radius") and hasattr(face.Surface, "Axis") and hasattr(face.Surface, "Center"):
                cyl_faces.append(face)

        if not cyl_faces:
            return None

        groups = []
        for f in cyl_faces:
            r = f.Surface.Radius
            ax = np.array([f.Surface.Axis.x, f.Surface.Axis.y, f.Surface.Axis.z], dtype=float)
            ax = ax / np.linalg.norm(ax)
            cnt = np.array([f.Surface.Center.x, f.Surface.Center.y, f.Surface.Center.z], dtype=float)

            matched = False
            for g in groups:
                gr, gax, gcnt, gfaces = g
                if abs(r - gr) < 0.2 and abs(np.dot(ax, gax)) > 0.999:
                    vec = cnt - gcnt
                    radial_dist = np.linalg.norm(vec - np.dot(vec, gax) * gax)
                    if radial_dist < 0.5:
                        gfaces.append(f)
                        matched = True
                        break
            if not matched:
                groups.append((r, ax, cnt, [f]))

        # Filter out small radii (< 8.0 mm)
        groups = [g for g in groups if g[0] >= 8.0]
        if not groups:
            return None

        # Group with largest total surface area is the primary tube body
        groups.sort(key=lambda g: sum(f.Area for f in g[3]), reverse=True)
        primary_outer_r, tube_axis, tube_center, tube_outer_faces = groups[0]

        # Determine inner radius / wall thickness
        inner_r = primary_outer_r - 1.65  # default 0.065" (1.65mm)
        for g in groups[1:]:
            if g[0] < primary_outer_r - 0.4:
                inner_r = g[0]
                break

        wall_thick = primary_outer_r - inner_r

        return {
            'outer_r': primary_outer_r,
            'wall_thick': wall_thick,
            'axis': tube_axis,
            'center': tube_center,
            'outer_faces': tube_outer_faces
        }

    def _process_solid_with_info(self, solid, tube_id: str, cyl_info: dict) -> Optional[UnwrappedTube]:
        """
        Unrolls a validated, non-intersecting tube solid body cleanly into 1:1 cut profiles.
        """
        outer_r = cyl_info['outer_r']
        wall_thick = cyl_info['wall_thick']
        axis = cyl_info['axis']
        center = cyl_info['center']
        tube_outer_faces = cyl_info['outer_faces']

        non_outer_faces = [f for f in solid.Faces if not any(f.isSame(of) for of in tube_outer_faces)]

        # Reference clocking vector (theta = 0)
        u_clocking = compute_shared_clocking_vector(axis)
        v_clocking = np.cross(axis, u_clocking)
        v_clocking = v_clocking / np.linalg.norm(v_clocking)

        circumference = 2.0 * math.pi * outer_r

        # Extract true cut edges: shared by tube outer skin AND a non-outer face
        # (Internal CAD longitudinal seams between outer faces are naturally excluded)
        cut_edges = []
        for e in solid.Edges:
            adj = [f for f in solid.Faces if any(e2.isSame(e) for e2 in f.Edges)]
            on_outer = any(any(f.isSame(of) for of in tube_outer_faces) for f in adj)
            on_non_outer = any(any(f.isSame(nof) for nof in non_outer_faces) for f in adj)
            if on_outer and on_non_outer:
                cut_edges.append(e)

        # Fallback if uncut cylinder
        if not cut_edges:
            pts_all = [np.array([v.Point.x, v.Point.y, v.Point.z]) for v in solid.Vertexes]
            z_all = [np.dot(p - center, axis) for p in pts_all]
            z_min = min(z_all)
            z_max = max(z_all)
            unrolled_straight = np.column_stack([
                np.linspace(0, circumference, 360),
                np.full(360, self.collar_length_mm)
            ])
            p1 = TubeEndProfile(
                end_index=1,
                tube_od_mm=outer_r * 2.0,
                tube_wall_mm=wall_thick,
                collar_length_mm=self.collar_length_mm,
                unwrapped_cut_pts=unrolled_straight,
                cut_depth_mm=0.0,
                template_width_mm=circumference,
                template_height_mm=self.collar_length_mm,
                node_point_3d=center + z_min * axis
            )
            p2 = TubeEndProfile(
                end_index=2,
                tube_od_mm=outer_r * 2.0,
                tube_wall_mm=wall_thick,
                collar_length_mm=self.collar_length_mm,
                unwrapped_cut_pts=unrolled_straight,
                cut_depth_mm=0.0,
                template_width_mm=circumference,
                template_height_mm=self.collar_length_mm,
                node_point_3d=center + z_max * axis
            )
            return UnwrappedTube(
                tube_id=tube_id,
                tube_name=f"Member_{tube_id}",
                tube_od_mm=outer_r * 2.0,
                tube_wall_mm=wall_thick,
                axis_vector=axis,
                axis_center=center,
                reference_clocking_u=u_clocking,
                total_tube_length_mm=z_max - z_min,
                center_to_center_length_mm=z_max - z_min,
                end_1=p1,
                end_2=p2
            )

        # Assemble cut edges into closed boundary loops
        loops = []
        rem = list(cut_edges)
        while rem:
            cur = [rem.pop(0)]
            changed = True
            while changed:
                changed = False
                for i, e in enumerate(rem):
                    if any(v1.Point.distanceToPoint(v2.Point) < 1e-2 for le in cur for v1 in e.Vertexes for v2 in le.Vertexes):
                        cur.append(rem.pop(i))
                        changed = True
                        break
            loops.append(cur)

        # Sort loops by Z to separate End 1 (min Z) and End 2 (max Z)
        loop_z_data = []
        for loop in loops:
            pts = []
            for e in loop:
                pts.extend([e.valueAt(p) for p in np.linspace(e.FirstParameter, e.LastParameter, 5)])
            z_vals = [np.dot(np.array([p.x, p.y, p.z]) - center, axis) for p in pts]
            loop_z_data.append((np.mean(z_vals), loop))

        loop_z_data.sort(key=lambda item: item[0])

        end1_loop = loop_z_data[0][1]
        end2_loop = loop_z_data[-1][1] if len(loop_z_data) > 1 else loop_z_data[0][1]

        p1 = self._unroll_loop_smooth(
            end1_loop, axis, center, outer_r, u_clocking, v_clocking,
            is_end_1=True, end_idx=1, wall_thick=wall_thick
        )
        p2 = self._unroll_loop_smooth(
            end2_loop, axis, center, outer_r, u_clocking, v_clocking,
            is_end_1=False, end_idx=2, wall_thick=wall_thick
        )

        total_length = float(np.linalg.norm(p2.node_point_3d - p1.node_point_3d))
        if total_length < 1e-2:
            pts_all = [np.array([v.Point.x, v.Point.y, v.Point.z]) for v in solid.Vertexes]
            z_all = [np.dot(p - center, axis) for p in pts_all]
            total_length = max(z_all) - min(z_all)

        return UnwrappedTube(
            tube_id=tube_id,
            tube_name=f"Member_{tube_id}",
            tube_od_mm=outer_r * 2.0,
            tube_wall_mm=wall_thick,
            axis_vector=axis,
            axis_center=center,
            reference_clocking_u=u_clocking,
            total_tube_length_mm=total_length,
            center_to_center_length_mm=total_length - (p1.cut_depth_mm + p2.cut_depth_mm) / 2.0,
            end_1=p1,
            end_2=p2
        )

    def _unroll_loop_smooth(
        self,
        loop: list,
        axis: np.ndarray,
        center: np.ndarray,
        radius: float,
        u_clocking: np.ndarray,
        v_clocking: np.ndarray,
        is_end_1: bool,
        end_idx: int,
        wall_thick: float,
        num_samples: int = 360
    ) -> TubeEndProfile:
        """
        Samples a closed topological boundary loop head-to-tail and unwraps it
        with monotonic angle tracking to ensure clean, spike-free 1:1 cut profiles.
        """
        circumference = 2.0 * math.pi * radius

        # 1. Order edges head-to-tail
        ordered_edges = [(loop[0], False)]
        unused = list(loop[1:])
        while unused:
            last_e, last_rev = ordered_edges[-1]
            last_pt = last_e.valueAt(last_e.FirstParameter if last_rev else last_e.LastParameter)
            best_i = None
            best_rev = False
            for i, cand in enumerate(unused):
                p_first = cand.valueAt(cand.FirstParameter)
                p_last = cand.valueAt(cand.LastParameter)
                if p_first.distanceToPoint(last_pt) < 1e-2:
                    best_i = i; best_rev = False; break
                elif p_last.distanceToPoint(last_pt) < 1e-2:
                    best_i = i; best_rev = True; break
            if best_i is not None:
                ordered_edges.append((unused.pop(best_i), best_rev))
            else:
                ordered_edges.append((unused.pop(0), False))

        # 2. Sample dense 3D points along the ordered chain
        loop_points = []
        for e, rev in ordered_edges:
            n_pts = max(40, int(e.Length / 0.25))
            params = np.linspace(e.FirstParameter, e.LastParameter, n_pts)
            if rev:
                params = params[::-1]
            for param in params:
                pt = e.valueAt(param)
                loop_points.append(np.array([pt.x, pt.y, pt.z]))

        loop_points = np.array(loop_points)

        # 3. Compute (theta, z) relative to shared clocking vector
        thetas = []
        z_coords = []
        for p in loop_points:
            z = np.dot(p - center, axis)
            radial = (p - center) - z * axis
            th = math.atan2(np.dot(radial, v_clocking), np.dot(radial, u_clocking)) % (2.0 * math.pi)
            thetas.append(th)
            z_coords.append(z)

        thetas = np.array(thetas)
        z_coords = np.array(z_coords)

        # 4. Monotonic unwrapped angle along loop perimeter walk
        unwrapped_th = np.unwrap(thetas)
        if unwrapped_th[-1] < unwrapped_th[0]:
            unwrapped_th = -unwrapped_th
            unwrapped_th = unwrapped_th[::-1]
            z_coords = z_coords[::-1]

        th_start = unwrapped_th[0]
        # Append endpoint if loop does not quite reach 2*pi
        if unwrapped_th[-1] < th_start + 2.0 * math.pi:
            unwrapped_th = np.append(unwrapped_th, th_start + 2.0 * math.pi)
            z_coords = np.append(z_coords, z_coords[0])

        # 5. Query equidistant physical angle grid [0, 2*pi]
        phis = np.linspace(0.0, 2.0 * math.pi, num_samples, endpoint=True)
        query_thetas = th_start + ((phis - (th_start % (2.0 * math.pi))) % (2.0 * math.pi))

        interp_z = np.interp(query_thetas, unwrapped_th, z_coords)

        z_min = float(np.min(interp_z))
        z_max = float(np.max(interp_z))
        cut_depth = z_max - z_min

        # Collar layout: baseline at y = 0
        if is_end_1:
            baseline = z_max + self.collar_length_mm
            y_pts = baseline - interp_z
            node_pt = center + z_min * axis
        else:
            baseline = z_min - self.collar_length_mm
            y_pts = interp_z - baseline
            node_pt = center + z_max * axis

        x_pts = phis * radius
        unwrapped_pts = np.column_stack([x_pts, y_pts])
        template_h = cut_depth + self.collar_length_mm

        return TubeEndProfile(
            end_index=end_idx,
            tube_od_mm=radius * 2.0,
            tube_wall_mm=wall_thick,
            collar_length_mm=self.collar_length_mm,
            unwrapped_cut_pts=unwrapped_pts,
            cut_depth_mm=cut_depth,
            template_width_mm=circumference,
            template_height_mm=template_h,
            node_point_3d=node_pt
        )



def generate_analytical_coped_tube(
    tube_name: str,
    tube_od_in: float,
    tube_wall_in: float,
    length_in: float,
    end1_target_od_in: float,
    end1_angle_deg: float,
    end1_offset_in: float = 0.0,
    end2_target_od_in: float = 0.0,
    end2_angle_deg: float = 90.0,
    end2_offset_in: float = 0.0,
    clocking_angle_deg: float = 0.0,
    collar_len_in: float = 1.5,
    num_samples: int = 360
) -> UnwrappedTube:
    """
    Synthesizes an exact analytical coped tube profile for standard BSAE tubes
    with compound notch clocking and straight collar extension.
    """
    tube_od_mm = tube_od_in * 25.4
    tube_wall_mm = tube_wall_in * 25.4
    tube_len_mm = length_in * 25.4
    collar_len_mm = collar_len_in * 25.4
    
    r1 = (tube_od_mm / 2.0)
    circ = 2.0 * math.pi * r1
    
    theta_grid = np.linspace(0.0, 2.0 * math.pi, num_samples, endpoint=True)
    x_pts = theta_grid * r1
    
    # End 1 Analytical Cope
    r2_1 = (end1_target_od_in * 25.4) / 2.0
    alpha_1 = math.radians(end1_angle_deg)
    
    z_end1 = []
    for th in theta_grid:
        arg = (r2_1**2 - (r1 * math.sin(th))**2)
        if arg > 0:
            val = (math.sqrt(arg) - r2_1) / math.sin(alpha_1)
        else:
            val = 0.0
        z_end1.append(val)
    z_end1 = np.array(z_end1)
    d1 = float(np.max(z_end1) - np.min(z_end1))
    y1 = (z_end1 - np.min(z_end1)) + collar_len_mm
    
    p1 = TubeEndProfile(
        end_index=1,
        tube_od_mm=tube_od_mm,
        tube_wall_mm=tube_wall_mm,
        collar_length_mm=collar_len_mm,
        unwrapped_cut_pts=np.column_stack([x_pts, y1]),
        cut_depth_mm=d1,
        template_width_mm=circ,
        template_height_mm=d1 + collar_len_mm,
        node_point_3d=np.array([0.0, 0.0, 0.0])
    )
    
    # End 2 Analytical Cope with clocking
    if end2_target_od_in > 0:
        r2_2 = (end2_target_od_in * 25.4) / 2.0
        alpha_2 = math.radians(end2_angle_deg)
        clock_rad = math.radians(clocking_angle_deg)
        z_end2 = []
        for th in (theta_grid + clock_rad):
            arg = (r2_2**2 - (r1 * math.sin(th))**2)
            if arg > 0:
                val = (math.sqrt(arg) - r2_2) / math.sin(alpha_2)
            else:
                val = 0.0
            z_end2.append(val)
        z_end2 = np.array(z_end2)
        d2 = float(np.max(z_end2) - np.min(z_end2))
        y2 = (z_end2 - np.min(z_end2)) + collar_len_mm
    else:
        d2 = 0.0
        y2 = np.full(num_samples, collar_len_mm)
        
    p2 = TubeEndProfile(
        end_index=2,
        tube_od_mm=tube_od_mm,
        tube_wall_mm=tube_wall_mm,
        collar_length_mm=collar_len_mm,
        unwrapped_cut_pts=np.column_stack([x_pts, y2]),
        cut_depth_mm=d2,
        template_width_mm=circ,
        template_height_mm=d2 + collar_len_mm,
        node_point_3d=np.array([0.0, 0.0, tube_len_mm])
    )
    
    return UnwrappedTube(
        tube_id=tube_name,
        tube_name=tube_name,
        tube_od_mm=tube_od_mm,
        tube_wall_mm=tube_wall_mm,
        axis_vector=np.array([0.0, 0.0, 1.0]),
        axis_center=np.array([0.0, 0.0, tube_len_mm / 2.0]),
        reference_clocking_u=np.array([1.0, 0.0, 0.0]),
        total_tube_length_mm=tube_len_mm,
        center_to_center_length_mm=tube_len_mm - (d1 + d2) / 2.0,
        end_1=p1,
        end_2=p2
    )
