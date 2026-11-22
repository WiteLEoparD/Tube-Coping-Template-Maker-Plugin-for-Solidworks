# Contributing to Red Lion Racing Tube Cut Templates

We welcome contributions from team members and collegiate racing fabricators.

## Development Workflow

1. **Prerequisites:**
   - Visual Studio 2022 (Community or Professional) with the *.NET desktop development* workload.
   - .NET Framework 4.8 Targeting Pack.
   - SolidWorks 2022 or newer for live add-in debugging.

2. **Branching Strategy:**
   - Create a feature branch from `main`: `feature/your-feature-name` or `fix/your-fix-name`.
   - Keep commits focused and descriptive.

3. **Building Locally:**
   - Open `SolidWorks_TubeCutTemplate_Addin.sln`.
   - Build using the **Release | x64** configuration.
   - Run the `StandaloneDemo` console application to test unrolling logic without opening SolidWorks.

4. **Testing Guidelines:**
   - Ensure all output formats (PDF, DXF, SVG) preserve exact 1:1 physical dimensions (72 points = 1.000 inch).
   - Verify that all text written to PDF streams uses standard ASCII characters to avoid font stream encoding errors.
   - Always verify that the calibration square is exactly 1.000" x 1.000".

5. **Submitting Changes:**
   - Submit a pull request with a clear description of the enhancement or bug fix.
