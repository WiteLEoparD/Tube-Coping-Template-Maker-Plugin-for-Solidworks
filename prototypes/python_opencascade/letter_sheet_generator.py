"""
letter_sheet_generator.py
=========================
Generates 1:1 True-Scale Printable Templates on US Letter Paper (8.5" x 11").

Outputs:
1. Multi-page Printable Vector PDF (via ReportLab)
2. Scalable Vector Graphics SVG (via svgwrite)
3. AutoCAD 2D Vector DXF (via ezdxf)

Features:
- Precision 1:1 scale (zero distortion, no fit-to-page)
- Alignment Centerlines (0 deg and 180 deg) with crosshairs
- 1.000" x 1.000" Calibration Verification Box
- BSAE Rules 2027 Rev A Rear Braced Frame Node Tech Inspection Label
- Overlap tape tabs and wrap direction arrows
- Efficient packing of End 1 and End 2 on Letter sheets to save paper
"""

import os
import math
from typing import List, Tuple, Optional
import numpy as np

# ReportLab vector PDF generation
from reportlab.lib.pagesizes import letter
from reportlab.pdfgen import canvas
from reportlab.lib import colors

# SVG generation
import svgwrite

# DXF generation
import ezdxf
from ezdxf.enums import TextEntityAlignment

from opencascade_unroller import UnwrappedTube, TubeEndProfile
from bsae_rbf_policy import (
    BSAERBFPolicyManager,
    MAX_NODE_OFFSET_IN,
    PRIMARY_MIN_OD_IN,
    PRIMARY_MIN_WALL_IN
)

# US Letter Dimensions in points (1 in = 72 pt, 1 mm = 72 / 25.4 pt)
LETTER_WIDTH_PT = 8.5 * 72.0      # 612.0 pt
LETTER_HEIGHT_PT = 11.0 * 72.0    # 792.0 pt
MM_TO_PT = 72.0 / 25.4
IN_TO_PT = 72.0
PT_TO_MM = 25.4 / 72.0

SAFE_MARGIN_PT = 0.5 * 72.0       # 0.5 inch (36 pt) printable margin


class LetterSheetGenerator:
    """
    Renders 1:1 scale printable templates on US Letter sheets.
    """

    def __init__(self, output_dir: str = "./cut_templates"):
        self.output_dir = output_dir
        os.makedirs(self.output_dir, exist_ok=True)
        self.policy_mgr = BSAERBFPolicyManager()

    def generate_all_formats(self, tube: UnwrappedTube, prefix: str = "") -> dict:
        """
        Generates PDF, SVG, and DXF for a given unwrapped tube.
        """
        base_name = f"{prefix}{tube.tube_id}".replace(" ", "_")
        pdf_path = os.path.join(self.output_dir, f"{base_name}_Letter_1to1.pdf")
        svg_path = os.path.join(self.output_dir, f"{base_name}_Letter_1to1.svg")
        dxf_path = os.path.join(self.output_dir, f"{base_name}_1to1.dxf")

        self.generate_pdf([tube], pdf_path)
        self.generate_svg(tube, svg_path)
        self.generate_dxf(tube, dxf_path)

        return {
            "pdf": pdf_path,
            "svg": svg_path,
            "dxf": dxf_path,
        }

    # =========================================================================
    # 1. REPORTLAB PDF GENERATOR (TRUE 1:1 US LETTER)
    # =========================================================================
    def generate_pdf(self, tubes: List[UnwrappedTube], output_filepath: str):
        c = canvas.Canvas(output_filepath, pagesize=letter)
        c.setTitle("BSAE 2027 Rev A - 1:1 Tube Cut Templates (Letter)")
        c.setAuthor("BSAE Tube Cut Template Generator")

        for tube in tubes:
            # Check if End 1 and End 2 can fit on a single page
            h1_pt = tube.end_1.template_height_mm * MM_TO_PT
            h2_pt = tube.end_2.template_height_mm * MM_TO_PT
            w_pt = tube.end_1.template_width_mm * MM_TO_PT

            total_h_pt = h1_pt + h2_pt + 180.0  # including headers and margins

            if total_h_pt <= (LETTER_HEIGHT_PT - SAFE_MARGIN_PT):
                # Both ends fit on one sheet
                self._draw_tube_sheet(c, tube, draw_end1=True, draw_end2=True)
                c.showPage()
            else:
                # End 1 on page 1
                self._draw_tube_sheet(c, tube, draw_end1=True, draw_end2=False)
                c.showPage()
                # End 2 on page 2
                self._draw_tube_sheet(c, tube, draw_end1=False, draw_end2=True)
                c.showPage()

        c.save()

    def _draw_tube_sheet(self, c: canvas.Canvas, tube: UnwrappedTube, draw_end1: bool, draw_end2: bool):
        # Draw Page Header & Calibration Verification Box
        self._draw_page_header_and_calib(c, tube)

        current_y_pt = LETTER_HEIGHT_PT - SAFE_MARGIN_PT - 90.0

        if draw_end1 and tube.end_1:
            drawn_h = self._draw_end_template_pdf(c, tube, tube.end_1, y_start=current_y_pt)
            current_y_pt -= (drawn_h + 30.0)

        if draw_end2 and tube.end_2:
            self._draw_end_template_pdf(c, tube, tube.end_2, y_start=current_y_pt)

    def _draw_page_header_and_calib(self, c: canvas.Canvas, tube: UnwrappedTube):
        # Header text
        c.setFont("Helvetica-Bold", 12)
        c.drawString(SAFE_MARGIN_PT, LETTER_HEIGHT_PT - SAFE_MARGIN_PT - 5, "BSAE RULES 2027 REV A - REAR BRACED FRAME CUT TEMPLATES")

        c.setFont("Helvetica", 8)
        c.drawString(SAFE_MARGIN_PT, LETTER_HEIGHT_PT - SAFE_MARGIN_PT - 18,
                     "PRINT SCALE: 1:1 ACTUAL SIZE (100%) | PAPER: US LETTER (8.5\" x 11\") | DO NOT SCALE / FIT TO PAGE")

        c.setFont("Helvetica-Bold", 9)
        c.drawString(SAFE_MARGIN_PT, LETTER_HEIGHT_PT - SAFE_MARGIN_PT - 32,
                     f"TUBE ID: {tube.tube_id}  |  OD: {tube.tube_od_mm/25.4:.3f}\" ({tube.tube_od_mm:.1f}mm)  |  WALL: {tube.tube_wall_mm/25.4:.3f}\"  |  CUT LENGTH: {tube.total_tube_length_mm/25.4:.2f}\" ({tube.total_tube_length_mm:.1f}mm)")

        # 1.000" x 1.000" Calibration Verification Box at top right
        calib_size = 72.0  # exactly 1 inch
        calib_x = LETTER_WIDTH_PT - SAFE_MARGIN_PT - calib_size
        calib_y = LETTER_HEIGHT_PT - SAFE_MARGIN_PT - calib_size - 10

        c.setStrokeColor(colors.black)
        c.setLineWidth(1.0)
        c.rect(calib_x, calib_y, calib_size, calib_size)

        # Crosshair inside box
        c.setLineWidth(0.5)
        c.setDash([2, 2])
        c.line(calib_x + calib_size / 2.0, calib_y, calib_x + calib_size / 2.0, calib_y + calib_size)
        c.line(calib_x, calib_y + calib_size / 2.0, calib_x + calib_size, calib_y + calib_size / 2.0)
        c.setDash()

        c.setFont("Helvetica-Bold", 6.5)
        c.drawCentredString(calib_x + calib_size / 2.0, calib_y + calib_size / 2.0 + 8, "1.000\" x 1.000\"")
        c.setFont("Helvetica", 5.5)
        c.drawCentredString(calib_x + calib_size / 2.0, calib_y + calib_size / 2.0 - 2, "CALIBRATION BOX")
        c.drawCentredString(calib_x + calib_size / 2.0, calib_y + calib_size / 2.0 - 10, "MEASURE W/ CALIPER")

    def _draw_end_template_pdf(self, c: canvas.Canvas, tube: UnwrappedTube, end: TubeEndProfile, y_start: float) -> float:
        """
        Draws the unrolled wrap template for a single tube end.
        Returns the vertical height in points occupied by this template.
        """
        w_pt = end.template_width_mm * MM_TO_PT
        h_pt = end.template_height_mm * MM_TO_PT
        collar_pt = end.collar_length_mm * MM_TO_PT
        overlap_tab_pt = 0.25 * IN_TO_PT  # 0.25 inch tape tab

        x_origin = SAFE_MARGIN_PT
        y_base = y_start - h_pt

        # 1. Draw Overlap Tab (0.25 in left of template)
        c.setStrokeColor(colors.gray)
        c.setFillColor(colors.whitesmoke)
        c.setLineWidth(0.5)
        c.setDash([3, 3])
        c.rect(x_origin - overlap_tab_pt, y_base, overlap_tab_pt, h_pt, fill=1, stroke=1)
        c.setDash()

        c.saveState()
        c.setFont("Helvetica-Oblique", 6)
        c.setFillColor(colors.dimgray)
        c.translate(x_origin - overlap_tab_pt / 2.0, y_base + h_pt / 2.0)
        c.rotate(90)
        c.drawCentredString(0, 0, "TAPE OVERLAP TAB (0.25\")")
        c.restoreState()

        # 2. Draw Collar Baseline (straight cut line at y_base)
        c.setStrokeColor(colors.black)
        c.setLineWidth(0.8)
        c.setDash([4, 3])
        c.line(x_origin - overlap_tab_pt, y_base, x_origin + w_pt, y_base)
        c.setDash()

        c.setFont("Helvetica", 6.5)
        c.setFillColor(colors.black)
        c.drawString(x_origin + 5, y_base - 8, "✂ CUT COLLAR BASELINE (STRAIGHT CUT AROUND TUBE) ✂")

        # 3. Draw Side Seams (at x = 0 and x = W)
        c.setLineWidth(0.8)
        c.line(x_origin, y_base, x_origin, y_base + h_pt)
        c.line(x_origin + w_pt, y_base, x_origin + w_pt, y_base + h_pt)

        # 4. Draw Centerlines (Theta = 0 deg and Theta = 180 deg)
        # Main Clocking Centerline at x = 0 (and x = W)
        c.setStrokeColor(colors.red)
        c.setLineWidth(1.2)
        c.setDash([6, 2, 2, 2])  # Dash-dot style
        c.line(x_origin, y_base - 14, x_origin, y_base + h_pt + 14)
        c.line(x_origin + w_pt, y_base - 14, x_origin + w_pt, y_base + h_pt + 14)

        # Mid Centerline (180 deg) at x = W / 2
        c.setStrokeColor(colors.blue)
        c.setLineWidth(0.8)
        c.line(x_origin + w_pt / 2.0, y_base - 10, x_origin + w_pt / 2.0, y_base + h_pt + 10)
        c.setDash()

        # Centerline crosshair tick marks
        c.setLineWidth(0.6)
        c.line(x_origin - 6, y_base, x_origin + 6, y_base)
        c.line(x_origin + w_pt - 6, y_base, x_origin + w_pt + 6, y_base)
        c.line(x_origin + w_pt / 2.0 - 6, y_base, x_origin + w_pt / 2.0 + 6, y_base)

        c.setFont("Helvetica-Bold", 7.5)
        c.setFillColor(colors.red)
        c.drawString(x_origin + 4, y_base + 6, "CL [0°] SEAM")
        c.drawString(x_origin + w_pt - 60, y_base + 6, "CL [360°] SEAM")

        c.setFillColor(colors.blue)
        c.drawCentredString(x_origin + w_pt / 2.0, y_base + 6, "CL [180°]")

        # 5. Draw the Fishmouth / Notch Cut Contour (Bold Solid Line)
        if len(end.unwrapped_cut_pts) > 1:
            c.setStrokeColor(colors.black)
            c.setLineWidth(1.8)  # Heavy solid cut line
            path = c.beginPath()

            first_pt = end.unwrapped_cut_pts[0]
            x_screen_0 = x_origin + first_pt[0] * MM_TO_PT
            y_screen_0 = y_base + first_pt[1] * MM_TO_PT
            path.moveTo(x_screen_0, y_screen_0)

            for pt in end.unwrapped_cut_pts[1:]:
                x_s = x_origin + pt[0] * MM_TO_PT
                y_s = y_base + pt[1] * MM_TO_PT
                path.lineTo(x_s, y_s)

            c.drawPath(path, stroke=1, fill=0)

        # 6. BSAE Rules 2027 Rev A Tech Inspection Label Box
        info_x = x_origin + w_pt + 15
        info_y = y_base
        info_w = LETTER_WIDTH_PT - SAFE_MARGIN_PT - info_x
        info_h = max(h_pt, 75.0)

        c.setStrokeColor(colors.navy)
        c.setFillColor(colors.HexColor("#F8F9FA"))
        c.setLineWidth(1.0)
        c.rect(info_x, info_y, info_w, info_h, fill=1, stroke=1)

        # Header bar for label
        c.setFillColor(colors.HexColor("#1A365D"))
        c.rect(info_x, info_y + info_h - 16, info_w, 16, fill=1, stroke=0)
        c.setFillColor(colors.white)
        c.setFont("Helvetica-Bold", 8)
        c.drawString(info_x + 6, info_y + info_h - 12, f"BSAE TECH LABEL: END {end.end_index}")

        # Text inside label
        node_display = end.node_name or "UNMAPPED_NODE"
        label_lines = [
            f"Node: {node_display}  (RBF Section B.3)",
            f"Mating Target: {end.mating_target}",
            f"OD: {end.tube_od_mm/25.4:.3f}\" | Wall: {end.tube_wall_mm/25.4:.3f}\"",
            f"Notch Depth: {end.cut_depth_mm/25.4:.3f}\" ({end.cut_depth_mm:.1f}mm)",
            f"Cut Angle: {end.cut_angle_deg:.1f} deg",
            f"Circumference: {end.template_width_mm/25.4:.3f}\" ({end.template_width_mm:.1f}mm)",
            "Align CL [0°] with scribed tube centerline!",
        ]

        c.setFillColor(colors.black)
        c.setFont("Helvetica-Bold", 7.5)
        text_y = info_y + info_h - 28
        for line in label_lines:
            if "Node:" in line:
                c.setFont("Helvetica-Bold", 8)
                c.setFillColor(colors.HexColor("#C53030"))  # Highlight node in red
            elif "Align CL" in line:
                c.setFont("Helvetica-Bold", 7.5)
                c.setFillColor(colors.HexColor("#2B6CB0"))
            else:
                c.setFont("Helvetica", 7.5)
                c.setFillColor(colors.black)

            c.drawString(info_x + 6, text_y, line)
            text_y -= 11

        return h_pt

    # =========================================================================
    # 2. SVG GENERATOR (1:1 TRUE PHYSICAL INCHES)
    # =========================================================================
    def generate_svg(self, tube: UnwrappedTube, output_filepath: str):
        """Generates crisp SVG with 1:1 physical dimensions (in inches)."""
        dwg = svgwrite.Drawing(
            output_filepath,
            size=(f"{LETTER_WIDTH_PT/72.0}in", f"{LETTER_HEIGHT_PT/72.0}in"),
            viewBox=f"0 0 {LETTER_WIDTH_PT} {LETTER_HEIGHT_PT}"
        )

        # Style defs
        dwg.defs.add(dwg.style("""
            .cutline { stroke: #000000; stroke-width: 2.0; fill: none; }
            .baseline { stroke: #000000; stroke-width: 1.0; stroke-dasharray: 4,4; fill: none; }
            .centerline { stroke: #E53E3E; stroke-width: 1.2; stroke-dasharray: 6,2,2,2; fill: none; }
            .midline { stroke: #3182CE; stroke-width: 0.8; stroke-dasharray: 4,4; fill: none; }
            .overlap { stroke: #A0AEC0; stroke-width: 0.5; stroke-dasharray: 3,3; fill: #EDF2F7; }
            .text-main { font-family: Helvetica, Arial, sans-serif; font-size: 8px; fill: #1A202C; }
            .text-bold { font-family: Helvetica, Arial, sans-serif; font-size: 8px; font-weight: bold; fill: #1A202C; }
        """))

        # Add Calibration Box
        calib_size = 72.0
        calib_x = LETTER_WIDTH_PT - SAFE_MARGIN_PT - calib_size
        calib_y = SAFE_MARGIN_PT

        dwg.add(dwg.rect((calib_x, calib_y), (calib_size, calib_size), fill="none", stroke="black", stroke_width=1))
        dwg.add(dwg.line((calib_x + 36, calib_y), (calib_x + 36, calib_y + 72), stroke="gray", stroke_dasharray="2,2"))
        dwg.add(dwg.line((calib_x, calib_y + 36), (calib_x + 72, calib_y + 36), stroke="gray", stroke_dasharray="2,2"))
        dwg.add(dwg.text("1.000\" x 1.000\"", insert=(calib_x + 12, calib_y + 32), class_="text-bold"))
        dwg.add(dwg.text("CALIBRATION", insert=(calib_x + 12, calib_y + 44), class_="text-main"))

        # Draw End 1 and End 2
        y_cursor = SAFE_MARGIN_PT + 70.0
        for end in [tube.end_1, tube.end_2]:
            if not end:
                continue
            w_pt = end.template_width_mm * MM_TO_PT
            h_pt = end.template_height_mm * MM_TO_PT
            tab_pt = 18.0

            # Overlap Tab
            dwg.add(dwg.rect((SAFE_MARGIN_PT - tab_pt, y_cursor), (tab_pt, h_pt), class_="overlap"))

            # Baseline
            dwg.add(dwg.line((SAFE_MARGIN_PT - tab_pt, y_cursor + h_pt), (SAFE_MARGIN_PT + w_pt, y_cursor + h_pt), class_="baseline"))

            # Side seams
            dwg.add(dwg.line((SAFE_MARGIN_PT, y_cursor), (SAFE_MARGIN_PT, y_cursor + h_pt), stroke="black", stroke_width=0.8))
            dwg.add(dwg.line((SAFE_MARGIN_PT + w_pt, y_cursor), (SAFE_MARGIN_PT + w_pt, y_cursor + h_pt), stroke="black", stroke_width=0.8))

            # Centerlines
            dwg.add(dwg.line((SAFE_MARGIN_PT, y_cursor - 10), (SAFE_MARGIN_PT, y_cursor + h_pt + 10), class_="centerline"))
            dwg.add(dwg.line((SAFE_MARGIN_PT + w_pt, y_cursor - 10), (SAFE_MARGIN_PT + w_pt, y_cursor + h_pt + 10), class_="centerline"))
            dwg.add(dwg.line((SAFE_MARGIN_PT + w_pt / 2.0, y_cursor - 8), (SAFE_MARGIN_PT + w_pt / 2.0, y_cursor + h_pt + 8), class_="midline"))

            # Cut curve
            if len(end.unwrapped_cut_pts) > 1:
                points = [(SAFE_MARGIN_PT + p[0] * MM_TO_PT, y_cursor + (end.template_height_mm - p[1]) * MM_TO_PT) for p in end.unwrapped_cut_pts]
                dwg.add(dwg.polyline(points, class_="cutline"))

            # Label
            dwg.add(dwg.text(f"BSAE 2027 REV A | TUBE: {tube.tube_id} | END {end.end_index} | NODE: {end.node_name}",
                             insert=(SAFE_MARGIN_PT + w_pt + 20, y_cursor + 20), class_="text-bold"))
            dwg.add(dwg.text(f"Mating Target: {end.mating_target} | Cut Depth: {end.cut_depth_mm/25.4:.2f}\"",
                             insert=(SAFE_MARGIN_PT + w_pt + 20, y_cursor + 35), class_="text-main"))

            y_cursor += (h_pt + 40.0)

        dwg.save()

    # =========================================================================
    # 3. DXF GENERATOR (AUTOCAD 1:1 CNC / PLOTTER)
    # =========================================================================
    def generate_dxf(self, tube: UnwrappedTube, output_filepath: str):
        """Generates 1:1 DXF file with layered geometries in inches."""
        doc = ezdxf.new(dxfversion="R2018")
        msp = doc.modelspace()

        # Create distinct layers
        doc.layers.add(name="CUT_PROFILE", color=7)      # White/Black
        doc.layers.add(name="COLLAR_BASELINE", color=1)  # Red
        doc.layers.add(name="CENTERLINE_0DEG", color=3)  # Green
        doc.layers.add(name="CENTERLINE_180DEG", color=5)# Blue
        doc.layers.add(name="ANNOTATIONS", color=2)      # Yellow
        doc.layers.add(name="CALIBRATION_BOX", color=4)  # Cyan

        # Calibration Box (1.0 x 1.0 inch)
        msp.add_lwpolyline([(0, 0), (1.0, 0), (1.0, 1.0), (0, 1.0)], close=True, dxfattribs={"layer": "CALIBRATION_BOX"})
        msp.add_line((0.5, 0), (0.5, 1.0), dxfattribs={"layer": "CALIBRATION_BOX"})
        msp.add_line((0, 0.5), (1.0, 0.5), dxfattribs={"layer": "CALIBRATION_BOX"})

        y_offset_in = 2.0
        for end in [tube.end_1, tube.end_2]:
            if not end:
                continue

            w_in = end.template_width_mm / 25.4
            h_in = end.template_height_mm / 25.4

            # Collar baseline
            msp.add_line((0, y_offset_in), (w_in, y_offset_in), dxfattribs={"layer": "COLLAR_BASELINE"})

            # Side seams
            msp.add_line((0, y_offset_in), (0, y_offset_in + h_in), dxfattribs={"layer": "CUT_PROFILE"})
            msp.add_line((w_in, y_offset_in), (w_in, y_offset_in + h_in), dxfattribs={"layer": "CUT_PROFILE"})

            # Centerline 0 deg
            msp.add_line((0, y_offset_in - 0.2), (0, y_offset_in + h_in + 0.2), dxfattribs={"layer": "CENTERLINE_0DEG"})
            msp.add_line((w_in, y_offset_in - 0.2), (w_in, y_offset_in + h_in + 0.2), dxfattribs={"layer": "CENTERLINE_0DEG"})

            # Centerline 180 deg
            msp.add_line((w_in / 2.0, y_offset_in - 0.15), (w_in / 2.0, y_offset_in + h_in + 0.15), dxfattribs={"layer": "CENTERLINE_180DEG"})

            # Cut curve
            if len(end.unwrapped_cut_pts) > 1:
                pts_in = [(p[0] / 25.4, y_offset_in + p[1] / 25.4) for p in end.unwrapped_cut_pts]
                msp.add_lwpolyline(pts_in, dxfattribs={"layer": "CUT_PROFILE"})

            # Text
            label_str = f"BSAE 2027 REV A | TUBE: {tube.tube_id} | END {end.end_index} | NODE: {end.node_name}"
            msp.add_text(label_str, height=0.1, dxfattribs={"layer": "ANNOTATIONS"}).set_placement((0, y_offset_in - 0.35))

            y_offset_in += (h_in + 1.5)

        doc.saveas(output_filepath)
