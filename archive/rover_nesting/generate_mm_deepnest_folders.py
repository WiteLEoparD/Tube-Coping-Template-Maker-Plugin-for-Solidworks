import os
import glob
import re
import shutil
import ezdxf
import sys

sys.path.insert(0, r"C:\Users\mhasa\.gemini\antigravity\brain\7bfe4bfb-3a40-4398-aa66-17f51c564b14\scratch")
from test_robust_extractor import extract_polygon_robust

workspace_dir = r"c:\Users\mhasa\OneDrive\Documents\Auto template generator"
dxf_src_dir = os.path.join(workspace_dir, "rover_dxfs")

dir_qty_half = os.path.join(workspace_dir, "Rover_DXFs_mm_by_quantity")
dir_qty_full = os.path.join(workspace_dir, "Rover_DXFs_mm_by_quantity_full_wheels")
dir_unique = os.path.join(workspace_dir, "Rover_DXFs_mm_unique_parts")
dir_sheets = os.path.join(workspace_dir, "Stock_Sheets_mm")

for d in [dir_qty_half, dir_qty_full, dir_unique, dir_sheets]:
    if os.path.exists(d):
        shutil.rmtree(d)
    os.makedirs(d, exist_ok=True)

def create_clean_mm_dxf(src_path, dst_path):
    poly = extract_polygon_robust(src_path)
    minx_in, miny_in, maxx_in, maxy_in = poly.bounds
    minx_mm = minx_in * 25.4
    miny_mm = miny_in * 25.4
    w_mm = (maxx_in - minx_in) * 25.4
    h_mm = (maxy_in - miny_in) * 25.4

    src_doc = ezdxf.readfile(src_path)
    insunits = src_doc.header.get("$INSUNITS", 1)
    scale = 25.4 if insunits == 1 else 1.0

    new_doc = ezdxf.new(dxfversion="R2010")
    new_doc.header["$INSUNITS"] = 4 # Millimeters
    new_doc.header["$MEASUREMENT"] = 1 # Metric
    new_msp = new_doc.modelspace()

    for e in src_doc.modelspace():
        t = e.dxftype()
        if t in ("MTEXT", "TEXT"):
            continue # strip watermark
        elif t == "LINE":
            p1 = (e.dxf.start.x * scale - minx_mm, e.dxf.start.y * scale - miny_mm)
            p2 = (e.dxf.end.x * scale - minx_mm, e.dxf.end.y * scale - miny_mm)
            new_msp.add_line(p1, p2, dxfattribs={"layer": "0"})
        elif t == "ARC":
            c = (e.dxf.center.x * scale - minx_mm, e.dxf.center.y * scale - miny_mm)
            r = e.dxf.radius * scale
            new_msp.add_arc(c, r, e.dxf.start_angle, e.dxf.end_angle, dxfattribs={"layer": "0"})
        elif t == "CIRCLE":
            c = (e.dxf.center.x * scale - minx_mm, e.dxf.center.y * scale - miny_mm)
            r = e.dxf.radius * scale
            new_msp.add_circle(c, r, dxfattribs={"layer": "0"})
        elif t == "LWPOLYLINE":
            pts = []
            for p in e.get_points('xyb'):
                pts.append((p[0] * scale - minx_mm, p[1] * scale - miny_mm, p[2]))
            poly_entity = new_msp.add_lwpolyline(pts, format='xyb', dxfattribs={"layer": "0"})
            if e.is_closed:
                poly_entity.close(True)
        elif t == "POLYLINE":
            pts = [(v.dxf.location.x * scale - minx_mm, v.dxf.location.y * scale - miny_mm) for v in e.vertices]
            poly_entity = new_msp.add_lwpolyline(pts, format='xy', dxfattribs={"layer": "0"})
            if e.is_closed:
                poly_entity.close(True)

    new_doc.saveas(dst_path)
    return w_mm, h_mm

# Process all files
src_files = sorted(glob.glob(os.path.join(dxf_src_dir, "*.dxf")))
manifest = []

print("Converting unique parts to mm...")
for f in src_files:
    filename = os.path.basename(f)
    m = re.search(r"_QTY-\[(\d+)\]", filename)
    qty = int(m.group(1)) if m else 1
    base_name = re.sub(r"_QTY-\[\d+\]\.dxf", "", filename)
    
    unique_dst = os.path.join(dir_unique, filename)
    w_mm, h_mm = create_clean_mm_dxf(f, unique_dst)
    
    manifest.append({
        'filename': filename,
        'base_name': base_name,
        'src_path': f,
        'unique_mm_path': unique_dst,
        'qty': qty,
        'w_mm': w_mm,
        'h_mm': h_mm,
        'w_in': w_mm / 25.4,
        'h_in': h_mm / 25.4,
        'is_wheel_half': "Wheel_Half" in filename,
        'is_wheel_full': "Wheel_QTY" in filename
    })

print(f"Processed {len(manifest)} unique part types.")

# Generate quantity copies for Folder 1 (Half Wheels - 125 files)
count_half = 0
for item in manifest:
    if item['is_wheel_full']:
        continue # Skip full wheel in half-wheel folder
    qty = item['qty']
    base = item['base_name']
    for i in range(1, qty + 1):
        if qty == 1:
            dest_name = f"{base}.dxf"
        else:
            dest_name = f"{base}_{i:02d}.dxf" if qty >= 10 else f"{base}_{i}.dxf"
        shutil.copy2(item['unique_mm_path'], os.path.join(dir_qty_half, dest_name))
        count_half += 1

print(f"Generated {count_half} DXF copies in {dir_qty_half}")

# Generate quantity copies for Folder 2 (Full Wheels - 113 files)
count_full = 0
for item in manifest:
    if item['is_wheel_half']:
        continue # Skip half wheel in full-wheel folder
    qty = item['qty']
    base = item['base_name']
    for i in range(1, qty + 1):
        if qty == 1:
            dest_name = f"{base}.dxf"
        else:
            dest_name = f"{base}_{i:02d}.dxf" if qty >= 10 else f"{base}_{i}.dxf"
        shutil.copy2(item['unique_mm_path'], os.path.join(dir_qty_full, dest_name))
        count_full += 1

print(f"Generated {count_full} DXF copies in {dir_qty_full}")

# Create Stock Sheet DXFs (Sheet 1: 17.5" x 23.5", Sheet 2: 17.5" x 11.75")
def create_sheet_dxf(w_mm, h_mm, name, dst_path):
    doc = ezdxf.new(dxfversion="R2010")
    doc.header["$INSUNITS"] = 4 # Millimeters
    doc.header["$MEASUREMENT"] = 1 # Metric
    msp = doc.modelspace()
    pts = [(0.0, 0.0), (w_mm, 0.0), (w_mm, h_mm), (0.0, h_mm), (0.0, 0.0)]
    poly = msp.add_lwpolyline(pts, dxfattribs={"layer": "0"})
    poly.close(True)
    doc.saveas(dst_path)

sheet1_w_mm = 23.5 * 25.4 # 596.9 mm
sheet1_h_mm = 17.5 * 25.4 # 444.5 mm
sheet1_path = os.path.join(dir_sheets, "Sheet_1_17.5x23.5in_444.5x596.9mm.dxf")
create_sheet_dxf(sheet1_w_mm, sheet1_h_mm, "Sheet 1", sheet1_path)

sheet2_w_mm = 17.5 * 25.4 # 444.5 mm
sheet2_h_mm = 11.75 * 25.4 # 298.45 mm
sheet2_path = os.path.join(dir_sheets, "Sheet_2_17.5x11.75in_444.5x298.45mm.dxf")
create_sheet_dxf(sheet2_w_mm, sheet2_h_mm, "Sheet 2", sheet2_path)

print(f"Created Stock Sheets in {dir_sheets}:")
print(f"  - Sheet 1: {sheet1_w_mm:.1f} x {sheet1_h_mm:.1f} mm ({sheet1_w_mm/25.4:.1f} x {sheet1_h_mm/25.4:.1f} in)")
print(f"  - Sheet 2: {sheet2_w_mm:.1f} x {sheet2_h_mm:.1f} mm ({sheet2_w_mm/25.4:.1f} x {sheet2_h_mm/25.4:.1f} in)")

# Print summary table
print("\n" + "=" * 90)
print(f"{'Part Name':<45} | QTY | Width x Height (mm) | Width x Height (in)")
print("=" * 90)
for it in manifest:
    print(f"{it['base_name']:<45} | {it['qty']:3d} | {it['w_mm']:6.2f} x {it['h_mm']:6.2f} mm | {it['w_in']:5.2f} x {it['h_in']:5.2f} in")
print("=" * 90)
