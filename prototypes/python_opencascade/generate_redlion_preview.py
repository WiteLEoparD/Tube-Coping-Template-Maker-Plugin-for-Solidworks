import os
import sys
import matplotlib
os.environ['MPLCONFIGDIR'] = os.path.dirname(os.path.abspath(__file__))
import matplotlib.pyplot as plt
import matplotlib.patches as patches
import numpy as np
from PIL import Image

# Setup figure 8.5 x 11.0 at 150 DPI
fig, ax = plt.subplots(figsize=(8.5, 11.0), dpi=150)
ax.set_xlim(0, 8.5)
ax.set_ylim(0, 11.0)
ax.set_aspect('equal')
ax.axis('off')

# Background
page = patches.Rectangle((0, 0), 8.5, 11.0, facecolor='white', edgecolor='#D0D5DD', linewidth=1)
ax.add_patch(page)

margin = 0.5 # 36 pt

# 1. Official UMSAE Red Lion Racing Logo (Top-Left)
logo_path = os.path.join(os.path.dirname(__file__), "SolidWorks_TubeCutTemplate_Addin", "Resources", "red_lion_logo.png")
if os.path.exists(logo_path):
    logo_img = Image.open(logo_path)
    # 58 pt x 55 pt -> 0.805 in x 0.764 in
    logo_w_in = 0.81
    logo_h_in = 0.76
    ax.imshow(logo_img, extent=[margin, margin + logo_w_in, 11.0 - margin - logo_h_in - 0.05, 11.0 - margin - 0.05], zorder=10)

text_x = margin + 0.95

# 2. Header Title & Specs
ax.text(text_x, 10.35, "RED LION RACING | UNIVERSITY OF MANITOBA", fontsize=11, fontweight='bold', color='#B8141F', va='top')
ax.text(text_x, 10.15, "Formula & Baja SAE Team | 1:1 Scale Chassis Tube Cut Template", fontsize=7.2, fontweight='bold', color='#333E48', va='top')
ax.text(text_x, 9.95, "TUBE: Tube_1   |   OD: 1.250\" (31.8mm)   |   WALL: 0.065\"", fontsize=7.6, fontweight='bold', color='black', va='top')
ax.text(text_x, 9.77, "CUT LENGTH: 23.65\" (600.7mm)   |   ROUTING: S_R -> A_R", fontsize=7.0, color='#2D3748', va='top')
ax.text(text_x, 9.60, "PRINT AT 100% SCALE (ACTUAL SIZE - DO NOT FIT TO PAGE)", fontsize=6.5, fontweight='bold', color='#555555', va='top')

# 3. Calibration Box (Top-Right: 1.000" x 1.000")
cal_x = 8.5 - margin - 1.0
cal_y = 11.0 - margin - 1.0
cal_rect = patches.Rectangle((cal_x, cal_y), 1.0, 1.0, facecolor='#FCFDFF', edgecolor='black', linewidth=1.2)
ax.add_patch(cal_rect)
# Crosshairs
ax.plot([cal_x + 0.5, cal_x + 0.5], [cal_y, cal_y + 1.0], color='#777777', linestyle='--', linewidth=0.6)
ax.plot([cal_x, cal_x + 1.0], [cal_y + 0.5, cal_y + 0.5], color='#777777', linestyle='--', linewidth=0.6)
ax.text(cal_x + 0.5, cal_y + 0.65, "1.000\" x 1.000\"", fontsize=6.5, fontweight='bold', color='black', ha='center', va='center')
ax.text(cal_x + 0.5, cal_y + 0.50, "CALIBRATION BOX", fontsize=5.0, color='#444444', ha='center', va='center')
ax.text(cal_x + 0.5, cal_y + 0.35, "MEASURE W/ CALIPERS", fontsize=4.8, fontweight='bold', color='#B8141F', ha='center', va='center')

# 4. Divider Rule
ax.plot([margin, 8.5 - margin], [9.42, 9.42], color='#D0D5DD', linewidth=0.8)

# Helper function to draw an End Profile
def draw_end(y_start, end_num, node_name, cut_depth_in, collar_in, is_straight):
    w_in = np.pi * 1.250 # 3.927"
    h_in = collar_in + (cut_depth_in if not is_straight else 0.1)
    tab_w = 0.25
    x_orig = margin + tab_w
    y_base = y_start - h_in

    # Overlap Tab
    tab = patches.Rectangle((x_orig - tab_w, y_base), tab_w, h_in, facecolor='#F3F4F6', edgecolor='#9CA3AF', linestyle='--', linewidth=0.7)
    ax.add_patch(tab)
    ax.text(x_orig - tab_w/2, y_base + h_in/2, "TAPE OVERLAP TAB (0.25\")", fontsize=5.5, rotation=90, ha='center', va='center', color='#4B5563')

    # Collar Baseline
    ax.plot([x_orig - tab_w, x_orig + w_in], [y_base, y_base], 'k--', linewidth=1.0)
    ax.text(x_orig + 0.1, y_base - 0.12, "-- CUT COLLAR BASELINE (STRAIGHT CUT AROUND TUBE) --", fontsize=6.0, fontweight='bold', color='black')

    # Side Seams
    ax.plot([x_orig, x_orig], [y_base, y_base + h_in], 'k-', linewidth=0.8)
    ax.plot([x_orig + w_in, x_orig + w_in], [y_base, y_base + h_in], 'k-', linewidth=0.8)

    # Centerlines
    # 0 deg
    ax.plot([x_orig, x_orig], [y_base - 0.15, y_base + h_in + 0.15], color='#DC2626', linestyle='-.', linewidth=1.2)
    ax.plot([x_orig + w_in, x_orig + w_in], [y_base - 0.15, y_base + h_in + 0.15], color='#DC2626', linestyle='-.', linewidth=1.2)
    ax.text(x_orig + 0.05, y_base + h_in + 0.05, "CL [0 deg]", fontsize=6.8, fontweight='bold', color='#DC2626')
    ax.text(x_orig + w_in - 0.65, y_base + h_in + 0.05, "CL [360 deg]", fontsize=6.8, fontweight='bold', color='#DC2626')

    # 180 deg
    ax.plot([x_orig + w_in/2, x_orig + w_in/2], [y_base - 0.1, y_base + h_in + 0.1], color='#2563EB', linestyle='--', linewidth=0.9)
    ax.text(x_orig + w_in/2, y_base + h_in + 0.05, "CL [180 deg]", fontsize=6.8, fontweight='bold', color='#2563EB', ha='center')

    # Scribe labels
    ax.text(x_orig + 0.05, y_base + 0.06, "SCRIBE LINE", fontsize=6.0, fontweight='bold', color='#DC2626')
    ax.text(x_orig + w_in - 0.65, y_base + 0.06, "SCRIBE LINE", fontsize=6.0, fontweight='bold', color='#DC2626')

    # Cut curve
    theta = np.linspace(0, 2*np.pi, 200)
    xs = x_orig + theta / (2*np.pi) * w_in
    if is_straight:
        ys = y_base + collar_in * np.ones_like(theta)
    else:
        # Coping profile (miter / fishmouth)
        ys = y_base + collar_in + cut_depth_in * (np.sin(theta)**2)

    ax.plot(xs, ys, 'k-', linewidth=2.0)

    # Cut Spec Badge
    badge_x = x_orig + w_in + 0.25
    badge_w = 8.5 - margin - badge_x
    badge_h = 0.85
    badge_y = y_base + max(0, (h_in - badge_h) / 2)

    badge_rect = patches.Rectangle((badge_x, badge_y), badge_w, badge_h, facecolor='#FAFBFD', edgecolor='#B8141F', linewidth=1.0)
    ax.add_patch(badge_rect)
    header_strip = patches.Rectangle((badge_x, badge_y + badge_h - 0.22), badge_w, 0.22, facecolor='#B8141F', edgecolor='none')
    ax.add_patch(header_strip)

    ax.text(badge_x + 0.08, badge_y + badge_h - 0.14, f"END {end_num} CUT SPECIFICATION", fontsize=7.0, fontweight='bold', color='white')
    ax.text(badge_x + 0.08, badge_y + badge_h - 0.36, f"Chassis Node: {node_name}", fontsize=6.8, fontweight='bold', color='black')
    depth_str = f"Notch Depth: {cut_depth_in:.3f}\" ({cut_depth_in*25.4:.1f} mm)" + (" (Straight Cut)" if is_straight else "")
    ax.text(badge_x + 0.08, badge_y + badge_h - 0.50, depth_str, fontsize=6.5, color='#222222')
    ax.text(badge_x + 0.08, badge_y + badge_h - 0.64, f"Collar Extension: {collar_in:.2f}\" ({collar_in*25.4:.1f} mm)", fontsize=6.5, color='#222222')
    ax.text(badge_x + 0.08, badge_y + badge_h - 0.77, "Align CL [0 deg] to scribed tube seam line", fontsize=6.2, fontweight='bold', color='#2563EB')

    return h_in

# Draw End 1
h1 = draw_end(9.20, 1, "S_R", 0.000, 1.50, is_straight=True)

# Draw End 2
h2 = draw_end(9.20 - h1 - 0.6, 2, "A_R", 0.134, 1.50, is_straight=False)

out_preview = os.path.join(os.path.dirname(__file__), "template_redlion_preview.png")
plt.savefig(out_preview, dpi=150, bbox_inches='tight', facecolor='white')
print(f"Preview saved to {out_preview}")
