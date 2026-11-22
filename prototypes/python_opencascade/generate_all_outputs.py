import os
import glob
import re
import shutil
import ezdxf
from shapely.geometry import box, Polygon, MultiPolygon
from shapely.affinity import rotate, translate
import sys

sys.path.insert(0, r"C:\Users\mhasa\.gemini\antigravity\brain\7bfe4bfb-3a40-4398-aa66-17f51c564b14\scratch")
from test_robust_extractor import extract_polygon_robust

workspace_dir = r"c:\Users\mhasa\OneDrive\Documents\Auto template generator"
dxf_src_dir = os.path.join(workspace_dir, "rover_dxfs")

# Target folders
dir_dxf_qty = os.path.join(workspace_dir, "Rover_DXFs_mm_by_quantity")
dir_svg_qty = os.path.join(workspace_dir, "Rover_SVGs_mm_by_quantity")
dir_dxf_unique = os.path.join(workspace_dir, "Rover_DXFs_mm_unique_parts")
dir_svg_unique = os.path.join(workspace_dir, "Rover_SVGs_mm_unique_parts")
dir_sheets = os.path.join(workspace_dir, "Stock_Sheets_mm")

for d in [dir_dxf_qty, dir_svg_qty, dir_dxf_unique, dir_svg_unique, dir_sheets]:
    os.makedirs(d, exist_ok=True)
    for existing_file in glob.glob(os.path.join(d, "*.*")):
        try:
            os.remove(existing_file)
        except Exception:
            pass

# 1. First, ensure WheelSpokes_Half_REV_B_QTY-[8].dxf exists in rover_dxfs
spokes_src = os.path.join(dxf_src_dir, "WheelSpokes_QTY-[4].dxf")
poly_spokes = extract_polygon_robust(spokes_src)
p90 = rotate(poly_spokes, 90, origin=(0, 0))
top_spoke_half = p90.intersection(box(-3.5, 0.0, 3.5, 3.5))
minx, miny, maxx, maxy = top_spoke_half.bounds
top_spoke_half = translate(top_spoke_half, -minx, -miny)

# Save half spoke to rover_dxfs (in inches)
spoke_half_dxf_path = os.path.join(dxf_src_dir, "WheelSpokes_Half_REV_B_QTY-[8].dxf")
doc_sh = ezdxf.new(dxfversion="R2010")
doc_sh.header["$INSUNITS"] = 1
msp_sh = doc_sh.modelspace()
poly_ext = msp_sh.add_lwpolyline(list(top_spoke_half.exterior.coords), dxfattribs={"layer": "0"})
poly_ext.close(True)
for hole in top_spoke_half.interiors:
    poly_h = msp_sh.add_lwpolyline(list(hole.coords), dxfattribs={"layer": "0"})
    poly_h.close(True)
doc_sh.saveas(spoke_half_dxf_path)
print(f"Generated {os.path.basename(spoke_half_dxf_path)}")

# 2. Helper to write DXF in mm
def save_poly_as_dxf_mm(p_mm, dst_path):
    doc = ezdxf.new(dxfversion="R2010")
    doc.header["$INSUNITS"] = 4 # Millimeters
    doc.header["$MEASUREMENT"] = 1 # Metric
    msp = doc.modelspace()
    
    poly_ext = msp.add_lwpolyline(list(p_mm.exterior.coords), dxfattribs={"layer": "0"})
    poly_ext.close(True)
    for hole in p_mm.interiors:
        poly_h = msp.add_lwpolyline(list(hole.coords), dxfattribs={"layer": "0"})
        poly_h.close(True)
    doc.saveas(dst_path)

# 3. Helper to write SVG in mm
def save_poly_as_svg_mm(p_mm, w_mm, h_mm, dst_path):
    def format_ring(coords):
        pts = [f"{x:.3f} {y:.3f}" for x, y in coords]
        return "M " + " L ".join(pts) + " Z"
    
    path_data = [format_ring(p_mm.exterior.coords)]
    for hole in p_mm.interiors:
        path_data.append(format_ring(hole.coords))
        
    d_attr = " ".join(path_data)
    svg_content = f'''<?xml version="1.0" encoding="UTF-8" standalone="no"?>
<svg xmlns="http://www.w3.org/2000/svg" version="1.1"
     width="{w_mm:.3f}mm" height="{h_mm:.3f}mm"
     viewBox="0 0 {w_mm:.3f} {h_mm:.3f}">
  <path d="{d_attr}" fill="none" stroke="#000000" stroke-width="0.25" fill-rule="evenodd" />
</svg>'''
    with open(dst_path, 'w', encoding='utf-8') as f:
        f.write(svg_content)

# 4. Process all active parts (excluding original full wheel and full spokes)
all_dxfs = sorted(glob.glob(os.path.join(dxf_src_dir, "*.dxf")))
manifest = []

for f in all_dxfs:
    fname = os.path.basename(f)
    if fname in ("Wheel_QTY-[12].dxf", "WheelSpokes_QTY-[4].dxf"):
        continue # Sliced versions used instead
        
    m = re.search(r"_QTY-\[(\d+)\]", fname)
    qty = int(m.group(1)) if m else 1
    base_name = re.sub(r"_QTY-\[\d+\]\.dxf", "", fname)
    
    poly = extract_polygon_robust(f)
    minx, miny, maxx, maxy = poly.bounds
    poly_norm = translate(poly, -minx, -miny)
    
    # Scale to mm
    from shapely.affinity import scale as shp_scale
    poly_mm = shp_scale(poly_norm, xfact=25.4, yfact=25.4, origin=(0, 0))
    minx_m, miny_m, maxx_m, maxy_m = poly_mm.bounds
    w_mm = maxx_m - minx_m
    h_mm = maxy_m - miny_m
    
    # Unique files
    dxf_uniq_path = os.path.join(dir_dxf_unique, fname)
    svg_uniq_path = os.path.join(dir_svg_unique, fname.replace(".dxf", ".svg"))
    save_poly_as_dxf_mm(poly_mm, dxf_uniq_path)
    save_poly_as_svg_mm(poly_mm, w_mm, h_mm, svg_uniq_path)
    
    manifest.append({
        'fname': fname,
        'base_name': base_name,
        'qty': qty,
        'w_mm': w_mm,
        'h_mm': h_mm,
        'w_in': w_mm / 25.4,
        'h_in': h_mm / 25.4,
        'poly_mm': poly_mm,
        'dxf_uniq_path': dxf_uniq_path,
        'svg_uniq_path': svg_uniq_path
    })

# 5. Populate per-quantity copies
total_dxf_copies = 0
total_svg_copies = 0

for item in manifest:
    qty = item['qty']
    base = item['base_name']
    for i in range(1, qty + 1):
        if qty == 1:
            d_name = f"{base}.dxf"
            s_name = f"{base}.svg"
        else:
            suffix = f"_{i:02d}" if qty >= 10 else f"_{i}"
            d_name = f"{base}{suffix}.dxf"
            s_name = f"{base}{suffix}.svg"
            
        shutil.copy2(item['dxf_uniq_path'], os.path.join(dir_dxf_qty, d_name))
        shutil.copy2(item['svg_uniq_path'], os.path.join(dir_svg_qty, s_name))
        total_dxf_copies += 1
        total_svg_copies += 1

print(f"Generated {total_dxf_copies} DXF copies in {dir_dxf_qty}")
print(f"Generated {total_svg_copies} SVG copies in {dir_svg_qty}")

# 6. Create Sheet DXFs and SVGs
sheet1_w_mm = 23.5 * 25.4 # 596.9 mm
sheet1_h_mm = 17.5 * 25.4 # 444.5 mm
sheet2_w_mm = 17.5 * 25.4 # 444.5 mm
sheet2_h_mm = 11.75 * 25.4 # 298.45 mm

# Sheet 1
p_s1 = Polygon([(0, 0), (sheet1_w_mm, 0), (sheet1_w_mm, sheet1_h_mm), (0, sheet1_h_mm)])
save_poly_as_dxf_mm(p_s1, os.path.join(dir_sheets, "Sheet_1_17.5x23.5in_444.5x596.9mm.dxf"))
save_poly_as_svg_mm(p_s1, sheet1_w_mm, sheet1_h_mm, os.path.join(dir_sheets, "Sheet_1_17.5x23.5in_444.5x596.9mm.svg"))

# Sheet 2
p_s2 = Polygon([(0, 0), (sheet2_w_mm, 0), (sheet2_w_mm, sheet2_h_mm), (0, sheet2_h_mm)])
save_poly_as_dxf_mm(p_s2, os.path.join(dir_sheets, "Sheet_2_17.5x11.75in_444.5x298.45mm.dxf"))
save_poly_as_svg_mm(p_s2, sheet2_w_mm, sheet2_h_mm, os.path.join(dir_sheets, "Sheet_2_17.5x11.75in_444.5x298.45mm.svg"))

print("Stock sheets generated in DXF and SVG formats.")

# Summary Table
print("\n" + "=" * 95)
print(f"{'Part Name':<42} | QTY | Width x Height (mm) | Width x Height (in) | Net Area (ea)")
print("=" * 95)
total_parts = 0
total_area_sqin = 0.0
for it in manifest:
    area_in = it['poly_mm'].area / (25.4**2)
    print(f"{it['base_name']:<42} | {it['qty']:3d} | {it['w_mm']:6.2f} x {it['h_mm']:6.2f} mm | {it['w_in']:5.2f} x {it['h_in']:5.2f} in | {area_in:6.2f} sq.in")
    total_parts += it['qty']
    total_area_sqin += area_in * it['qty']
print("=" * 95)
print(f"Total parts to nest: {total_parts} | Total net material area: {total_area_sqin:.2f} sq.in")
