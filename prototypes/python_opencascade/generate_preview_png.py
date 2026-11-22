"""
generate_preview_png.py
=======================
Generates a visual PNG render of Tube 12 and Tube 11 templates for visual inspection.
"""
import sys
import os
sys.path.append(r'C:\Program Files\FreeCAD 1.1\bin')
sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import matplotlib.pyplot as plt
import matplotlib.patches as patches
import numpy as np

from bsae_rbf_policy import BSAERBFPolicyManager
from opencascade_unroller import OpenCASCADEUnroller

unroller = OpenCASCADEUnroller(collar_length_mm=38.1)
tubes = unroller.load_and_process_step("1.5.2_TUBEFRAME-26.STEP")

# Select Tube #12
target_tube = None
for t in tubes:
    if t.tube_id == "TUBE_12":
        target_tube = t
        break

fig, ax = plt.subplots(figsize=(8.5, 11.0), dpi=150)
ax.set_xlim(0, 8.5)
ax.set_ylim(0, 11.0)
ax.set_aspect('equal')
ax.axis('off')

page_border = patches.Rectangle((0, 0), 8.5, 11.0, linewidth=1, edgecolor='#CBD5E0', facecolor='white')
ax.add_patch(page_border)

margin = 0.5
safe_rect = patches.Rectangle((margin, margin), 7.5, 10.0, linewidth=0.5, edgecolor='#E2E8F0', facecolor='none', linestyle=':')
ax.add_patch(safe_rect)

# Header
ax.text(margin, 10.5, "BSAE RULES 2027 REV A - REAR BRACED FRAME CUT TEMPLATES", fontsize=12, fontweight='bold', color='#1A365D')
ax.text(margin, 10.3, "PRINT SCALE: 1:1 ACTUAL SIZE (100%) | PAPER: US LETTER (8.5\" x 11\") | DO NOT SCALE", fontsize=8, color='#4A5568')
ax.text(margin, 10.05, f"TUBE: {target_tube.tube_id} | OD: 1.000\" | WALL: 0.049\" | TOTAL CUT LEN: {target_tube.total_tube_length_mm/25.4:.2f}\"", fontsize=9, fontweight='bold', color='#2D3748')

# Calibration Box
calib_box = patches.Rectangle((8.5 - margin - 1.0, 11.0 - margin - 1.0), 1.0, 1.0, linewidth=1.5, edgecolor='black', facecolor='#F7FAFC')
ax.add_patch(calib_box)
ax.plot([8.5 - margin - 0.5, 8.5 - margin - 0.5], [11.0 - margin - 1.0, 11.0 - margin], 'k--', linewidth=0.5)
ax.plot([8.5 - margin - 1.0, 8.5 - margin], [11.0 - margin - 0.5, 11.0 - margin - 0.5], 'k--', linewidth=0.5)
ax.text(8.5 - margin - 0.5, 11.0 - margin - 0.45, "1.000\" x 1.000\"", fontsize=7, fontweight='bold', ha='center')
ax.text(8.5 - margin - 0.5, 11.0 - margin - 0.6, "CALIBRATION BOX", fontsize=6, ha='center')

# End 1 (Notch Depth = 1.69 in)
end1 = target_tube.end_1
w_in = end1.template_width_mm / 25.4
h_in = end1.template_height_mm / 25.4
tab_in = 0.25
x_start = margin + tab_in
y_top = 8.8

# Overlap tab
ax.add_patch(patches.Rectangle((x_start - tab_in, y_top - h_in), tab_in, h_in, linewidth=0.8, edgecolor='#A0AEC0', facecolor='#EDF2F7', linestyle='--'))
ax.text(x_start - tab_in/2, y_top - h_in/2, "TAB (0.25\")", fontsize=6, rotation=90, va='center', ha='center', color='#4A5568')

# Baseline
ax.plot([x_start - tab_in, x_start + w_in], [y_top - h_in, y_top - h_in], 'k--', linewidth=1.2)
ax.text(x_start + 0.1, y_top - h_in - 0.15, "✂ CUT COLLAR BASELINE (STRAIGHT CUT) ✂", fontsize=6.5, color='black')

# Side seams
ax.plot([x_start, x_start], [y_top - h_in, y_top], 'k-', linewidth=0.8)
ax.plot([x_start + w_in, x_start + w_in], [y_top - h_in, y_top], 'k-', linewidth=0.8)

# Centerlines
ax.plot([x_start, x_start], [y_top - h_in - 0.2, y_top + 0.2], color='#E53E3E', linestyle='-.', linewidth=1.5)
ax.plot([x_start + w_in, x_start + w_in], [y_top - h_in - 0.2, y_top + 0.2], color='#E53E3E', linestyle='-.', linewidth=1.5)
ax.text(x_start + 0.05, y_top - h_in + 0.1, "CL [0°] SEAM", fontsize=7.5, fontweight='bold', color='#E53E3E')
ax.plot([x_start + w_in/2, x_start + w_in/2], [y_top - h_in - 0.15, y_top + 0.15], color='#3182CE', linestyle='--', linewidth=1.0)
ax.text(x_start + w_in/2, y_top - h_in + 0.1, "CL [180°]", fontsize=7, fontweight='bold', color='#3182CE', ha='center')

# Cut Profile curve
x_pts = x_start + end1.unwrapped_cut_pts[:, 0] / 25.4
y_pts = (y_top - h_in) + end1.unwrapped_cut_pts[:, 1] / 25.4
ax.plot(x_pts, y_pts, 'k-', linewidth=2.2)
ax.fill_between(x_pts, y_top - h_in, y_pts, color='#68D391', alpha=0.2)

# Label
box_x = x_start + w_in + 0.35
box_w = 8.5 - margin - box_x
box_h = h_in
info_box = patches.Rectangle((box_x, y_top - h_in), box_w, box_h, linewidth=1.2, edgecolor='#1A365D', facecolor='#F7FAFC')
ax.add_patch(info_box)
header_bar = patches.Rectangle((box_x, y_top - 0.28), box_w, 0.28, facecolor='#1A365D', edgecolor='none')
ax.add_patch(header_bar)
ax.text(box_x + 0.1, y_top - 0.18, "BSAE 2027 REV A | TUBE 12 (END 1 - NODE A_L)", fontsize=7.5, fontweight='bold', color='white')
ax.text(box_x + 0.1, y_top - 0.45, f"Notch Depth: {end1.cut_depth_mm/25.4:.3f}\" ({end1.cut_depth_mm:.1f}mm)", fontsize=7.5, fontweight='bold', color='#C53030')
ax.text(box_x + 0.1, y_top - 0.65, f"Circumference: {end1.template_width_mm/25.4:.3f}\"", fontsize=7, color='#2D3748')
ax.text(box_x + 0.1, y_top - h_in + 0.15, "ALIGN CL [0°] WITH TUBE SCRIBED LINE", fontsize=6.5, fontweight='bold', color='#2B6CB0')

preview_png = os.path.join(os.path.dirname(os.path.abspath(__file__)), "tube12_smooth_preview.png")
plt.tight_layout()
plt.savefig(preview_png, dpi=150)
print(f"Smooth preview saved to: {preview_png}")
