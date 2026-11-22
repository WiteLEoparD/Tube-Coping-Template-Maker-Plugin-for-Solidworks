# Technical Architecture & Engineering Specifications

### Geometric Extraction, 1:1 Unrolling Math, Collision Engine, and BSAE 2027 Rev A Node Policy

---

## 1. Overview of Geometric Challenges in Tube Coping

When two circular structural tubes intersect in 3D space, their intersection curve on the outer cylinder skin is a 3D space curve (bi-quadratic or higher-order algebraic curve). Unwrapping this 3D curve onto a 2D flat plane without physical distortion or numerical spikes is non-trivial:
1. **Longitudinal CAD Seams:** CAD kernels (including Parasolid in SolidWorks) represent full cylinders as two 180° cylindrical patches joined by two internal longitudinal seam edges. If these internal CAD seams are mistakenly sampled, the unroller produces vertical spike artifacts.
2. **Comb / Sawtooth Oscillations:** Point-cloud projection without strict topological loop ordering causes multi-valued loops, back-tracking, and zigzagging spikes across the cut line.
3. **Rotational Clocking Misalignment:** An independent coordinate system at End 1 and End 2 results in arbitrary rotational phase shifts. In physical fabrication, a 5° clocking error between ends ruins fit-up in the chassis jig.
4. **Paper Waste:** Unrolling a 40" tube requires multiple sheets of paper wrapping straight unnotched metal.

---

## 2. Geometric Pipeline & Algorithms

### Step 1: Cylinder Skin Identification & Geometric Grouping
1. Enumerate all faces `IFace2` on the selected `IBody2`.
2. Filter for cylindrical surfaces (`ISurface.IsCylinder`).
3. Group cylindrical faces sharing:
   - Radius $R \pm 0.25\text{ mm}$
   - Collinear axis vector: $|\vec{a}_1 \cdot \vec{a}_2| > 0.999$
   - Centerline distance: $\|(\vec{c}_1 - \vec{c}_2) - ((\vec{c}_1 - \vec{c}_2) \cdot \vec{a}_1)\vec{a}_1\| < 0.5\text{ mm}$
4. Compute total surface area for each group. The group with the **maximum total surface area** is uniquely designated as the **tube's primary outer skin**.
5. Inner bore cylinder faces with smaller radius are identified to extract wall thickness $t = R_{outer} - R_{inner}$.

### Step 2: True Cut Edge Extraction
An edge `IEdge` belongs to the notch coping boundary IF AND ONLY IF:
$$\text{Adjacent Face } 1 \in \text{OuterCylinderFaces} \quad \text{AND} \quad \text{Adjacent Face } 2 \notin \text{OuterCylinderFaces}$$
- Non-outer faces include planar miter end faces, cylindrical cope faces, and inner bore surfaces.
- **Longitudinal CAD seams** have outer cylinder faces on *both* sides, so they are **strictly and automatically discarded**.

### Step 3: Shared Canonical Clocking Reference Vector $\vec{u}$
To guarantee that the $0^\circ$ centerline on End 1 and End 2 represents a single continuous physical scribe line along the raw tube:
$$\vec{a} = \frac{\vec{\text{Axis}}}{\|\vec{\text{Axis}}\|}$$
$$\vec{z}_{\text{global}} = [0, 0, 1]^T$$
If $|\vec{a} \cdot \vec{z}_{\text{global}}| > 0.92$ (tube is nearly vertical), use $\vec{y}_{\text{global}} = [0, 1, 0]^T$ to prevent gimbal lock.
$$\vec{u} = \frac{\vec{z}_{\text{global}} - (\vec{z}_{\text{global}} \cdot \vec{a})\vec{a}}{\|\vec{z}_{\text{global}} - (\vec{z}_{\text{global}} \cdot \vec{a})\vec{a}\|}$$
$$\vec{v} = \vec{a} \times \vec{u}$$
The orthonormal basis $\{\vec{u}, \vec{v}, \vec{a}\}$ is identical and continuous along the entire tube.

### Step 4: Topological Loop Assembly & Sorting
1. True cut edges are assembled head-to-tail by endpoint distance matching ($\epsilon < 1.0\text{ mm}$).
2. Each closed loop's mean axial coordinate is calculated:
   $$\bar{z} = \frac{1}{N}\sum_{k=1}^N (\vec{p}_k - \vec{c}) \cdot \vec{a}$$
3. The loop with minimum $\bar{z}$ is End 1; the loop with maximum $\bar{z}$ is End 2.

### Step 5: Spike-Free Single-Valued Unwrapping
Along the ordered head-to-tail perimeter walk of each loop:
1. Dense points $\vec{p}_k$ are evaluated.
2. For each point:
   $$z_k = (\vec{p}_k - \vec{c}) \cdot \vec{a}$$
   $$\vec{r}_k = (\vec{p}_k - \vec{c}) - z_k \vec{a}$$
   $$\theta_k = \text{atan2}(\vec{r}_k \cdot \vec{v}, \vec{r}_k \cdot \vec{u}) \pmod{2\pi}$$
3. Phase unwrapping ensures monotonic angular progression:
   $$\Delta \theta_k = \theta_k - \theta_{k-1}$$
   If $\Delta \theta_k > \pi \implies \Delta \theta_k -= 2\pi$; if $\Delta \theta_k < -\pi \implies \Delta \theta_k += 2\pi$.
4. An equidistant 360-point grid $\phi_i = \frac{2\pi \cdot i}{359}$ for $i \in [0, 359]$ is queried via linear interpolation from the unwrapped profile.
5. This guarantees a single-valued function $z(\phi)$ with **zero comb/sawtooth oscillations**.

### Step 6: Paper-Saving Collar Truncation
Instead of unrolling full tube length, the template establishes:
- Straight collar baseline at $y = 0$
- Notch depth: $\Delta z = \max(z) - \min(z)$
- Straight extension: $L_{\text{collar}} = 1.50\text{ in} = 38.1\text{ mm}$
- Template Width: $W = 2\pi R = \pi \cdot OD$
- Template Height: $H = \Delta z + L_{\text{collar}}$

---

## 3. Collision & Penetration Filter Specifications

Per Mission Requirement 7:
> *"Automated intersection check: If two tube bodies collide or penetrate through each other in CAD, ignore them and issue a warning/error diagnostic rather than producing corrupt geometry."*

### Implementation:
1. **AABB Pre-Filter:** Bounding boxes of tube bodies are compared. Non-overlapping pairs are skipped immediately.
2. **Exact SolidWorks B-Rep Boolean Intersection:**
   - Evaluates `body1.Copy().Operations2((int)swBodyOperationType_e.SWBODYINTERSECT, body2, out int err)`.
   - If an intersection solid is returned, its volume is calculated via `rBody.GetMassProperties(1.0)`.
   - **Collision Volume Cutoff:** If overlap volume $> 20.0\text{ mm}^3$ (or $> 0.001\text{ in}^3$), mutual penetration is confirmed.
3. **Notch Depth Cutoff:**
   - If notch depth exceeds $3.5 \times OD$, it indicates an uncut piercing penetration across the tube's mid-body rather than a valid end cope.
4. **Diagnostic Action:**
   - Both colliding bodies are excluded from template generation.
   - A `CollisionDiagnostic` record is created logging:
     - Colliding Body A & B names
     - Overlap Volume ($\text{mm}^3$)
     - Recommended CAD remediation: "Tubes must be coped/trimmed using Weldments Trim/Extend before generating templates."
   - The user is notified in the PropertyManagerPage summary dialog.

---

## 4. BSAE Rules 2027 Revision A - RBF Node Policy

Section B.3 of the **2027 Baja SAE Ruleset (Revision A)** defines the Rear Braced Frame (RBF) structural requirements:

### Named Roll Cage Nodes (Points)
| Node | Description | Rulebook Role |
| :---: | :--- | :--- |
| **$B_L, B_R$** | Rear Roll Hoop Left/Right Upper Corners | Primary Roll Cage Node (Article B.3.2) |
| **$S_L, S_R$** | Intermediate Rear Roll Hoop Nodes (SIM / FAB_Lower) | Primary Roll Cage Node (Article B.3.2) |
| **$A_L, A_R$** | Lower Rear Roll Hoop Nodes (LFS / LC) | Primary Roll Cage Node (Article B.3.2) |
| **$R_L, R_R$** | Rear Braced Frame Aft Vertices (FAB_Upper / FAB_Lower / RLC) | Rear Bracing Primary Node (Article B.3.2.13) |
| **$C_L, C_R$** | Roll Hoop Overhead Forward Nodes (RHO / FBM) | Primary Roll Cage Node (Article B.3.2) |
| **$D_L, D_R$** | Front Roll Hoop Lower Nodes (FBM / SIM / LFS) | Primary Roll Cage Node (Article B.3.2) |

### Centerline Offset Compliance Policy
- **Maximum Allowable Offset:** Under Article B.3.2.13, tube centerlines forming a structural node must intersect or terminate within an allowable offset sphere:
  $$\text{Offset Distance} \le 2.000\text{ inches} \quad (50.800\text{ mm})$$
- The Add-in computes the physical distance from each tube end apex to the nearest standard node and flags compliance on the printable template label.
