# Deepnest Ready DXF Package

All SolidWorks educational product watermarks (`MTEXT` / `TEXT`) have been completely stripped out, and all geometries have been normalized to **1:1 Millimeters (`$INSUNITS = 4`)** with bounding boxes aligned to origin `(0, 0)`.

---

## 📁 Folders Created

### 1. `Rover_DXFs_mm_by_quantity/` (Recommended - 125 files)
Contains individual DXF copies for each part instance based on its BOM quantity, including the **24 sliced half-wheels** (`Wheel_Half_REV_B_01.dxf` .. `_24.dxf`).
- Simply **Select All (Ctrl+A)** and drag into Deepnest. Each file will automatically have `Quantity = 1`.

### 2. `Rover_DXFs_mm_by_quantity_full_wheels/` (113 files)
Same as above, but with the **12 original full wheels** (`Wheel_01.dxf` .. `_12.dxf`) instead of the sliced half-wheels.

### 3. `Rover_DXFs_mm_unique_parts/` (22 files)
Contains 1 master clean DXF for each unique part type with the quantity tagged in the filename (e.g. `Paddle_0.25in_QTY-[56].dxf`).
- Useful if you prefer importing fewer files into Deepnest and setting the quantity column manually.

### 4. `Stock_Sheets_mm/` (2 files)
Pre-dimensioned sheet boundary DXFs ready to import into Deepnest:
- `Sheet_1_17.5x23.5in_444.5x596.9mm.dxf`: **596.9 mm × 444.5 mm** ($23.5'' \times 17.5''$)
- `Sheet_2_17.5x11.75in_444.5x298.45mm.dxf`: **444.5 mm × 298.45 mm** ($17.5'' \times 11.75''$)

---

## ⚙️ Recommended Deepnest Settings
1. **Import Parts**: Click `Import` and select all 125 files from `Rover_DXFs_mm_by_quantity`.
2. **Import Sheets**: Click `Import` and select the two sheet DXFs from `Stock_Sheets_mm`. In Deepnest's table, check the box **"is sheet"** for both sheet files.
3. **Settings (Gear Icon)**:
   - **Space between parts**: `2.5 mm` (~0.10 in)
   - **Sheet margin**: `3.0 mm` (~0.12 in)
   - **Curve tolerance**: `0.2 mm`
   - **Rotations**: `4` (90°) or `8` (45°)
4. Click **Start Nest**!
