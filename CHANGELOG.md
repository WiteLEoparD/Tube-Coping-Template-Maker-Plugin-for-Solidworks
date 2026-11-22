# Changelog

All notable changes to the Red Lion Racing SolidWorks Tube Cut Template Generator will be documented in this file.

## [1.0.0] - 2026-11-21

### Added
- Native SolidWorks Add-in (`SwAddin`) registered under Weldments and Assembly CommandManager tabs.
- Interactive PropertyManagerPage (PMP) UI with multi-body selection and 0.75"–1.75" OD automatic tube scanner.
- True 1:1 scale US Letter PDF exporter with zero external DLL dependencies.
- 1.000" x 1.000" calibration verification box with caliper crosshairs.
- Spike-free unrolling engine with monotonic angular unwrapping and 360-point equidistant perimeter grid.
- Shared 0° and 180° continuous clocking seams for rotational notch indexing.
- Paper-saving straight collar extension (default 1.50" / 38.1 mm).
- Layered 2D AutoCAD R12/2000 DXF and scalable SVG export.
- Standalone Windows GUI installer (`RedLionRacing_TubeTemplates_Installer.exe`) with zero-admin per-user COM registration.
- UMSAE Red Lion Racing official team branding and high-resolution logo embedding.
- Complete standalone verification test runner (`StandaloneDemo.exe`).

### Changed
- Removed CAD collision/penetration filtering so all tubes are processed individually without being skipped.
- Removed 3D chassis locator thumbnail rendering to eliminate viewport freezing and streamline batch generation.
- Cleaned up text encoding to guarantee zero `?` characters in PDF outputs.
