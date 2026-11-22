# SolidWorks to FreeCAD / OpenCASCADE Tube Cut Template Guide
## BSAE Rules 2027, Revision A - Rear Braced Frame (RBF)

This guide explains how to model, trim, and export tube chassis assemblies from **SolidWorks** for automatic 1:1 Letter cut template generation using the OpenCASCADE/FreeCAD engine.

---

### 1. Modeling Tubes in SolidWorks for Clean Coping

For off-road chassis (Baja SAE / FSAE), use **SolidWorks Weldments**:

1. **3D Sketch Wireframe:**
   - Draw the centerline skeleton of the Rear Braced Frame using 3D sketches.
   - Constrain key intersection points to match the BSAE Named Points:
     - `Node B_L` / `Node B_R`: Top corners of the Rear Roll Hoop (RRH).
     - `Node S_L` / `Node S_R`: Intermediate RRH nodes (Side Impact Member junction).
     - `Node A_L` / `Node A_R`: Lower RRH nodes.
     - `Node R_L` / `Node R_R`: Rear bracing aft apex nodes.
2. **Structural Member Features:**
   - Apply Round Tubing profiles:
     - **Primary Members:** `1.25" OD x 0.065" Wall` (or `1.00" OD x 0.095"`).
     - **Secondary Members:** `1.00" OD x 0.035"` (or `0.049"`).
3. **Trim / Extend Tool (Fishmouth Generation):**
   - In SolidWorks, click **Trim/Extend**.
   - Corner Type: Select **End Trim** or **Body Trim**.
   - Bodies to Trim: Select your bracing tube (e.g. `FAB_Upper_L`).
   - Trimming Boundary: Select the mating tube (e.g. `RRH`).
   - SolidWorks will automatically produce an exact 3D cylindrical coping cut (fishmouth).

---

### 2. Exporting from SolidWorks to STEP (Recommended)

**STEP AP214 or AP242** is the gold standard for FreeCAD and OpenCASCADE because it stores exact analytical cylinders and mathematical 3D B-Rep intersection curves (unlike OBJ/STL meshes, which approximate curves with faceted triangles).

#### Export Steps:
1. In SolidWorks, go to **File -> Save As**.
2. Set **Save as type** to **STEP AP214 (*.step, *.stp)** or **STEP AP242**.
3. Click the **Options...** button in the Save As dialog:
   - **Output as:** Select `Solid/Surface geometry`.
   - **Export face/edge properties:** Checked.
   - **Split periodic faces:** **Unchecked** (prevents splitting cylinders into halves).
4. Click **OK** and save your file (e.g. `Baja_Chassis_RBF.step`).

> **Note on OBJ Files:** While OBJ mesh files can be opened in FreeCAD, STEP preserves the true circular radius, exact axis, and smooth unroll curves needed for high-precision 1:1 fabrication templates.

---

### 3. Naming Bodies in SolidWorks Cut-List (Optional but Recommended)

In your SolidWorks FeatureManager design tree:
1. Expand the **Cut-List** folder.
2. Rename each body to match its BSAE role:
   - `FAB_Upper_L`
   - `FAB_Upper_R`
   - `FAB_Lower_L`
   - `FAB_Lower_R`
   - `RLC`
   - `RRH_Upper`
   - `RRH_Upright_L`
3. When exported, the template generator automatically reads these names or classifies them against the 3D node coordinates.

---

### 4. Running the Template Generator

#### Option A: Quick Batch CLI
Open PowerShell or Command Prompt in the generator folder:
```powershell
.\run_template_generator.bat "Baja_Chassis_RBF.step" --collar 1.5
```

#### Option B: Inside FreeCAD GUI (One-Click Macro)
1. Launch **FreeCAD**.
2. Click **Macro -> Macros...**
3. Select `FreeCAD_TubeTemplate_Macro.py` and click **Execute**.
4. Browse for your `.step` file and click **Generate 1:1 Templates**.

---

### 5. Fabrication & Wrapping Instructions

Every template output on US Letter paper includes:
1. **1.000" x 1.000" Calibration Box:**
   - Measure this box with digital calipers before cutting.
   - If it is not exactly 1.000" (25.4 mm), check your printer settings: ensure **Actual Size (100%)** is selected, **not "Fit to Page"**.
2. **0° Reference Alignment Line (Centerline / Seam Line):**
   - Lay your raw tube on a flat table.
   - Place a piece of angle iron along the tube and draw a single continuous straight pencil/marker line along its full length.
   - Wrap the **End 1 template** so its `CL [0°] SEAM` line aligns with your marked line.
   - Wrap the **End 2 template** on the opposite end so its `CL [0°] SEAM` line aligns with the **exact same** marked line.
   - **Result:** Both end fishmouths will be clocked with 0° rotational error!
3. **Collar Baseline:**
   - Cut along the dashed collar baseline around the tube.
   - Wrap tightly around the tube and tape down using the 0.25" overlap tab.
   - Trace the heavy solid cut profile onto the tube with a paint pen or scribe, and notch with a hole saw, notch cutter, grinder, or plasma.
