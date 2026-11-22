using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using BajaSAE.TubeCutTemplateAddin.Core.Models;

namespace BajaSAE.TubeCutTemplateAddin.Export
{
    /// <summary>
    /// Generates true 1:1 scale printable vector PDF templates on standard US Letter paper (8.5" x 11.0").
    /// Pure standalone implementation with zero external DLL dependency conflicts inside SolidWorks.
    /// Exact physical scale: 72 points = 1.000 inch.
    /// Branded for Red Lion Racing | University of Manitoba.
    /// </summary>
    public class PdfTemplateExporter
    {
        public const double LetterWidthPt = 612.0;   // 8.5" * 72 pt/in
        public const double LetterHeightPt = 792.0;  // 11.0" * 72 pt/in
        public const double MmToPt = 72.0 / 25.4;    // 1 mm in points (~2.8346 pt)
        public const double InToPt = 72.0;
        public const double SafeMarginPt = 36.0;     // 0.5" margin

        private static Image _cachedLogo = null;
        private static bool _logoAttempted = false;

        public static string ExportPdf(UnwrappedTube tube, string outputPath)
        {
            var exporter = new PdfTemplateExporter();
            exporter.GenerateTubeSheet(new List<UnwrappedTube> { tube }, outputPath);
            return outputPath;
        }

        public void GenerateTubeSheet(List<UnwrappedTube> tubes, string outputPath)
        {
            var pdfDoc = new MinimalPdfWriter();

            foreach (var tube in tubes)
            {
                if (tube.End1 == null && tube.End2 == null) continue;

                double h1Pt = (tube.End1?.TemplateHeightMm ?? 0.0) * MmToPt;
                double h2Pt = (tube.End2?.TemplateHeightMm ?? 0.0) * MmToPt;

                // Check if both End 1 and End 2 fit cleanly on a single Letter sheet
                double totalOccupied = h1Pt + h2Pt + 190.0; // including margins and headers

                if (tube.End1 != null && tube.End2 != null && totalOccupied <= (LetterHeightPt - SafeMarginPt))
                {
                    // Single sheet containing both End 1 and End 2
                    var page = pdfDoc.AddPage(LetterWidthPt, LetterHeightPt);
                    DrawTubePage(page, tube, drawEnd1: true, drawEnd2: true);
                }
                else
                {
                    // Page 1: End 1
                    if (tube.End1 != null)
                    {
                        var p1 = pdfDoc.AddPage(LetterWidthPt, LetterHeightPt);
                        DrawTubePage(p1, tube, drawEnd1: true, drawEnd2: false);
                    }
                    // Page 2: End 2
                    if (tube.End2 != null)
                    {
                        var p2 = pdfDoc.AddPage(LetterWidthPt, LetterHeightPt);
                        DrawTubePage(p2, tube, drawEnd1: false, drawEnd2: true);
                    }
                }
            }

            pdfDoc.Save(outputPath);
        }

        private void DrawTubePage(PdfPage page, UnwrappedTube tube, bool drawEnd1, bool drawEnd2)
        {
            // 1. Page Header (Red Lion Racing Logo + University of Manitoba)
            DrawPageHeader(page, tube);

            // 2. 1.000" x 1.000" Calibration Verification Box (Far right)
            DrawCalibrationBox(page, tube);

            // 3. Render End Templates
            double currentY = LetterHeightPt - SafeMarginPt - 94.0;

            if (drawEnd1 && tube.End1 != null)
            {
                double drawnH = DrawEndTemplate(page, tube, tube.End1, currentY);
                currentY -= (drawnH + 35.0);
            }

            if (drawEnd2 && tube.End2 != null)
            {
                DrawEndTemplate(page, tube, tube.End2, currentY);
            }
        }

        private void DrawPageHeader(PdfPage page, UnwrappedTube tube)
        {
            // 1. Red Lion Racing Official Logo (Top-Left)
            var logo = GetRedLionLogo();
            double logoW = 58.0;
            double logoH = 55.0; // 58.0 / 1.061 ~ 54.7
            double logoX = SafeMarginPt;
            double logoY = LetterHeightPt - SafeMarginPt - logoH - 1.0;

            if (logo != null)
            {
                page.DrawImage(logo, logoX, logoY, logoW, logoH);
            }

            double textX = logo != null ? (SafeMarginPt + logoW + 12.0) : SafeMarginPt;

            string tubeId = string.IsNullOrWhiteSpace(tube.TubeId) ? "Chassis Tube" : tube.TubeId;
            string end1Node = tube.End1?.NodeName ?? "End 1";
            string end2Node = tube.End2?.NodeName ?? "End 2";

            // Title: RED LION RACING | UNIVERSITY OF MANITOBA
            page.SetFont("Helvetica-Bold", 11.0);
            page.SetFillColor(0.72, 0.08, 0.12); // Red Lion Crimson (#B8141F)
            page.DrawText(textX, LetterHeightPt - SafeMarginPt - 12.0, "RED LION RACING | UNIVERSITY OF MANITOBA");

            // Subtitle: Formula & Baja SAE Team | 1:1 Scale Chassis Tube Cut Template
            page.SetFont("Helvetica-Bold", 7.2);
            page.SetFillColor(0.20, 0.22, 0.26); // Slate
            page.DrawText(textX, LetterHeightPt - SafeMarginPt - 24.0,
                "Formula & Baja SAE Team | 1:1 Scale Chassis Tube Cut Template");

            // Tube Specifications
            page.SetFont("Helvetica-Bold", 7.6);
            page.SetFillColor(0.0, 0.0, 0.0);
            page.DrawText(textX, LetterHeightPt - SafeMarginPt - 36.0,
                $"TUBE: {tubeId}   |   OD: {tube.TubeOdInches:F3}\" ({tube.TubeOdMm:F1}mm)   |   WALL: {tube.TubeWallInches:F3}\"");

            // Cut Length & Routing
            page.SetFont("Helvetica", 7.0);
            page.SetFillColor(0.18, 0.18, 0.18);
            page.DrawText(textX, LetterHeightPt - SafeMarginPt - 47.0,
                $"CUT LENGTH: {tube.TotalTubeLengthInches:F2}\" ({tube.TotalTubeLengthMm:F1}mm)   |   ROUTING: {end1Node} -> {end2Node}");

            // Print Scale Note
            page.SetFont("Helvetica-Bold", 6.5);
            page.SetFillColor(0.35, 0.35, 0.35);
            page.DrawText(textX, LetterHeightPt - SafeMarginPt - 57.0,
                "PRINT AT 100% SCALE (ACTUAL SIZE - DO NOT FIT TO PAGE)");

            // Horizontal Divider Rule
            page.SetStrokeColor(0.80, 0.82, 0.85);
            page.SetLineWidth(0.75);
            page.DrawLine(SafeMarginPt, LetterHeightPt - SafeMarginPt - 64.0, LetterWidthPt - SafeMarginPt, LetterHeightPt - SafeMarginPt - 64.0);
        }

        private void DrawCalibrationBox(PdfPage page, UnwrappedTube tube)
        {
            // Exactly 1.000" x 1.000" (72 x 72 pt) anchored at far right margin
            double boxSize = 72.0;
            double boxX = LetterWidthPt - SafeMarginPt - boxSize; // 504.0 pt
            double boxY = LetterHeightPt - SafeMarginPt - boxSize - 2.0; // 682.0 pt

            // Outer box
            page.SetStrokeColor(0.0, 0.0, 0.0);
            page.SetLineWidth(1.0);
            page.DrawRect(boxX, boxY, boxSize, boxSize, fill: false, stroke: true);

            // Centered dashed crosshairs
            page.SetStrokeColor(0.45, 0.45, 0.45);
            page.SetLineWidth(0.5);
            page.SetDashPattern(new double[] { 2, 2 }, 0);
            page.DrawLine(boxX + boxSize / 2.0, boxY, boxX + boxSize / 2.0, boxY + boxSize);
            page.DrawLine(boxX, boxY + boxSize / 2.0, boxX + boxSize, boxY + boxSize / 2.0);
            page.ClearDashPattern();

            // Caliper verification text
            page.SetFont("Helvetica-Bold", 6.2);
            page.SetFillColor(0.0, 0.0, 0.0);
            page.DrawTextCentered(boxX + boxSize / 2.0, boxY + boxSize / 2.0 + 8.0, "1.000\" x 1.000\"");
            page.SetFont("Helvetica", 5.0);
            page.DrawTextCentered(boxX + boxSize / 2.0, boxY + boxSize / 2.0 - 2.0, "CALIBRATION BOX");
            page.SetFont("Helvetica-Bold", 4.5);
            page.SetFillColor(0.72, 0.08, 0.12); // Red Lion Crimson
            page.DrawTextCentered(boxX + boxSize / 2.0, boxY + boxSize / 2.0 - 10.0, "MEASURE W/ CALIPERS");
        }

        private double DrawEndTemplate(PdfPage page, UnwrappedTube tube, TubeEndProfile end, double yStart)
        {
            double wPt = end.TemplateWidthMm * MmToPt;
            double hPt = end.TemplateHeightMm * MmToPt;
            double collarPt = end.CollarLengthMm * MmToPt;
            double overlapTabPt = 0.25 * InToPt; // 0.25" tape overlap tab

            double xOrigin = SafeMarginPt + overlapTabPt;
            double yBase = yStart - hPt;

            // 1. Tape Overlap Tab (0.25" dashed extension on the left)
            page.SetFillColor(0.95, 0.96, 0.98);
            page.SetStrokeColor(0.6, 0.65, 0.7);
            page.SetLineWidth(0.5);
            page.SetDashPattern(new double[] { 3, 3 }, 0);
            page.DrawRect(xOrigin - overlapTabPt, yBase, overlapTabPt, hPt, fill: true, stroke: true);
            page.ClearDashPattern();

            // Rotated text inside overlap tab
            page.SetFont("Helvetica", 5.5);
            page.SetFillColor(0.4, 0.4, 0.4);
            page.DrawTextRotated90(xOrigin - overlapTabPt / 2.0 + 2.0, yBase + hPt / 2.0, "TAPE OVERLAP TAB (0.25\")");

            // 2. Collar Baseline (straight cut line at yBase)
            page.SetStrokeColor(0.0, 0.0, 0.0);
            page.SetLineWidth(0.8);
            page.SetDashPattern(new double[] { 4, 3 }, 0);
            page.DrawLine(xOrigin - overlapTabPt, yBase, xOrigin + wPt, yBase);
            page.ClearDashPattern();

            page.SetFont("Helvetica-Bold", 6.0);
            page.SetFillColor(0.0, 0.0, 0.0);
            page.DrawText(xOrigin + 6.0, yBase - 8.0, "-- CUT COLLAR BASELINE (STRAIGHT CUT AROUND TUBE) --");

            // 3. Side Seams (x = 0 and x = W)
            page.SetStrokeColor(0.2, 0.2, 0.2);
            page.SetLineWidth(0.8);
            page.DrawLine(xOrigin, yBase, xOrigin, yBase + hPt);
            page.DrawLine(xOrigin + wPt, yBase, xOrigin + wPt, yBase + hPt);

            // 4. Centerlines (0 deg and 180 deg)
            // Primary Scribe Alignment Centerline [0 deg] at x = 0 and x = W
            page.SetStrokeColor(0.85, 0.15, 0.15); // Red
            page.SetLineWidth(1.1);
            page.SetDashPattern(new double[] { 6, 2, 2, 2 }, 0); // Dash-dot
            page.DrawLine(xOrigin, yBase - 12.0, xOrigin, yBase + hPt + 12.0);
            page.DrawLine(xOrigin + wPt - 0.2, yBase - 12.0, xOrigin + wPt - 0.2, yBase + hPt + 12.0);

            // Secondary Centerline [180 deg] at half circumference
            page.SetStrokeColor(0.15, 0.45, 0.85); // Blue
            page.SetLineWidth(0.8);
            page.DrawLine(xOrigin + wPt / 2.0, yBase - 8.0, xOrigin + wPt / 2.0, yBase + hPt + 8.0);
            page.ClearDashPattern();

            // Centerline crosshair ticks
            page.SetStrokeColor(0.85, 0.15, 0.15);
            page.SetLineWidth(0.6);
            page.DrawLine(xOrigin - 5.0, yBase, xOrigin + 5.0, yBase);
            page.DrawLine(xOrigin + wPt - 5.0, yBase, xOrigin + wPt + 5.0, yBase);

            page.SetStrokeColor(0.15, 0.45, 0.85);
            page.DrawLine(xOrigin + wPt / 2.0 - 5.0, yBase, xOrigin + wPt / 2.0 + 5.0, yBase);

            // Top Centerline labels
            page.SetFont("Helvetica-Bold", 6.8);
            page.SetFillColor(0.85, 0.15, 0.15);
            page.DrawText(xOrigin + 3.0, yBase + hPt + 4.0, "CL [0 deg]");
            page.DrawText(xOrigin + wPt - 48.0, yBase + hPt + 4.0, "CL [360 deg]");

            page.SetFillColor(0.15, 0.45, 0.85);
            page.DrawTextCentered(xOrigin + wPt / 2.0, yBase + hPt + 4.0, "CL [180 deg]");

            // Bottom Scribe alignment label
            page.SetFont("Helvetica-Bold", 6.0);
            page.SetFillColor(0.85, 0.15, 0.15);
            page.DrawText(xOrigin + 3.0, yBase + 4.0, "SCRIBE LINE");
            page.DrawText(xOrigin + wPt - 48.0, yBase + 4.0, "SCRIBE LINE");

            // 5. Bold Fishmouth / Notch Cut Contour
            if (end.UnwrappedCutPoints != null && end.UnwrappedCutPoints.Count > 1)
            {
                page.SetStrokeColor(0.0, 0.0, 0.0);
                page.SetLineWidth(1.8); // Heavy cut line

                var screenPts = new List<Tuple<double, double>>();
                foreach (var pt in end.UnwrappedCutPoints)
                {
                    double sx = xOrigin + pt.X * MmToPt;
                    double sy = yBase + pt.Y * MmToPt;
                    screenPts.Add(Tuple.Create(sx, sy));
                }

                page.DrawPolyline(screenPts);
            }

            // 6. Compact Cut Specification Badge (Red Lion Racing Clean Layout)
            double infoX = xOrigin + wPt + 14.0;
            double infoW = LetterWidthPt - SafeMarginPt - infoX;
            double infoH = 56.0; // Compact badge height
            double infoY = yBase + Math.Max(0.0, (hPt - infoH) / 2.0); // Centered vertically next to cut profile

            if (infoW > 80.0)
            {
                infoW = Math.Min(infoW, 200.0); // Clean maximum width

                // Box background
                page.SetFillColor(0.98, 0.98, 0.99);
                page.SetStrokeColor(0.72, 0.08, 0.12); // Red Lion Crimson border
                page.SetLineWidth(0.8);
                page.DrawRect(infoX, infoY, infoW, infoH, fill: true, stroke: true);

                // Header bar
                page.SetFillColor(0.72, 0.08, 0.12); // Red Lion Crimson header
                page.DrawRect(infoX, infoY + infoH - 14.0, infoW, 14.0, fill: true, stroke: false);

                page.SetFont("Helvetica-Bold", 7.0);
                page.SetFillColor(1.0, 1.0, 1.0);
                string endTitle = end.EndIndex == 1 ? "END 1 CUT SPECIFICATION" : "END 2 CUT SPECIFICATION";
                page.DrawText(infoX + 6.0, infoY + infoH - 10.5, endTitle);

                // Clean detail rows (essential fabrication data only!)
                double textY = infoY + infoH - 23.5;
                double lineStep = 9.0;

                page.SetFont("Helvetica-Bold", 6.8);
                page.SetFillColor(0.0, 0.0, 0.0);
                string nodeStr = string.IsNullOrWhiteSpace(end.NodeName) ? $"End {end.EndIndex}" : end.NodeName;
                page.DrawText(infoX + 6.0, textY, $"Chassis Node: {nodeStr}");
                textY -= lineStep;

                page.SetFont("Helvetica", 6.5);
                page.SetFillColor(0.15, 0.15, 0.15);
                string depthNote = end.CutDepthInches < 0.005 ? " (Straight Cut)" : "";
                page.DrawText(infoX + 6.0, textY, $"Notch Depth: {end.CutDepthInches:F3}\" ({end.CutDepthMm:F1} mm){depthNote}");
                textY -= lineStep;

                page.DrawText(infoX + 6.0, textY, $"Collar Extension: {end.CollarLengthInches:F2}\" ({end.CollarLengthMm:F1} mm)");
                textY -= lineStep;

                page.SetFont("Helvetica-Bold", 6.2);
                page.SetFillColor(0.15, 0.40, 0.80); // Blue alignment reminder
                page.DrawText(infoX + 6.0, textY, "Align CL [0 deg] to scribed tube seam line");
            }

            return hPt;
        }

        private static Image GetRedLionLogo()
        {
            if (_logoAttempted) return _cachedLogo;
            _logoAttempted = true;

            try
            {
                var asm = typeof(PdfTemplateExporter).Assembly;

                // 1. Try embedded manifest resource
                foreach (var name in asm.GetManifestResourceNames())
                {
                    if (name.IndexOf("red_lion_logo.png", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        using (var stream = asm.GetManifestResourceStream(name))
                        {
                            if (stream != null)
                            {
                                using (var mem = new MemoryStream())
                                {
                                    stream.CopyTo(mem);
                                    mem.Seek(0, SeekOrigin.Begin);
                                    _cachedLogo = new Bitmap(Image.FromStream(mem));
                                    return _cachedLogo;
                                }
                            }
                        }
                    }
                }

                // 2. Try adjacent paths on disk
                string baseDir = AppDomain.CurrentDomain.BaseDirectory ?? "";
                string asmDir = Path.GetDirectoryName(asm.Location) ?? "";
                string[] candidatePaths = new string[]
                {
                    Path.Combine(baseDir, "Resources", "red_lion_logo.png"),
                    Path.Combine(baseDir, "red_lion_logo.png"),
                    Path.Combine(asmDir, "Resources", "red_lion_logo.png"),
                    Path.Combine(asmDir, "red_lion_logo.png"),
                    @"C:\Users\mhasa\OneDrive\Documents\Auto template generator\SolidWorks_TubeCutTemplate_Addin\Resources\red_lion_logo.png"
                };

                foreach (var p in candidatePaths)
                {
                    if (!string.IsNullOrEmpty(p) && File.Exists(p))
                    {
                        using (var src = Image.FromFile(p))
                        {
                            _cachedLogo = new Bitmap(src);
                            return _cachedLogo;
                        }
                    }
                }
            }
            catch { }

            return null;
        }
    }

    /// <summary>
    /// Lightweight, zero-dependency pure C# vector PDF document writer.
    /// Handles PDF syntax, graphics state, coordinate transformation, and JPEG image embedding.
    /// </summary>
    public class MinimalPdfWriter
    {
        private readonly List<PdfPage> _pages = new List<PdfPage>();

        public PdfPage AddPage(double widthPt, double heightPt)
        {
            var p = new PdfPage(widthPt, heightPt, _pages.Count + 1);
            _pages.Add(p);
            return p;
        }

        public void Save(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (var writer = new StreamWriter(fs, Encoding.ASCII))
            {
                var offsets = new List<long>();
                fs.Seek(0, SeekOrigin.Begin);

                // Header
                writer.WriteLine("%PDF-1.4");
                writer.WriteLine("%\xE2\xE3\xCF\xD3");
                writer.Flush();

                // Object 1: Catalog
                offsets.Add(fs.Position);
                writer.WriteLine("1 0 obj");
                writer.WriteLine("<< /Type /Catalog /Pages 2 0 R >>");
                writer.WriteLine("endobj");
                writer.Flush();

                // Object 2: Pages
                offsets.Add(fs.Position);
                writer.WriteLine("2 0 obj");
                writer.Write("<< /Type /Pages /Kids [");
                for (int i = 0; i < _pages.Count; i++)
                {
                    writer.Write($" {3 + i * 3} 0 R");
                }
                writer.WriteLine($" ] /Count {_pages.Count} >>");
                writer.WriteLine("endobj");
                writer.Flush();

                int currentObjId = 3;

                // Write each page
                for (int i = 0; i < _pages.Count; i++)
                {
                    var page = _pages[i];
                    int pageObjId = currentObjId;
                    int contentObjId = currentObjId + 1;
                    int resourceObjId = currentObjId + 2;
                    currentObjId += 3;

                    // Page object
                    offsets.Add(fs.Position);
                    writer.WriteLine($"{pageObjId} 0 obj");
                    writer.WriteLine($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {page.Width.ToString("F1", CultureInfo.InvariantCulture)} {page.Height.ToString("F1", CultureInfo.InvariantCulture)}] /Contents {contentObjId} 0 R /Resources {resourceObjId} 0 R >>");
                    writer.WriteLine("endobj");
                    writer.Flush();

                    // Content Stream
                    byte[] streamBytes = Encoding.ASCII.GetBytes(page.GetContentStream());
                    offsets.Add(fs.Position);
                    writer.WriteLine($"{contentObjId} 0 obj");
                    writer.WriteLine($"<< /Length {streamBytes.Length} >>");
                    writer.WriteLine("stream");
                    writer.Flush();

                    fs.Write(streamBytes, 0, streamBytes.Length);
                    fs.Flush();

                    writer.WriteLine();
                    writer.WriteLine("endstream");
                    writer.WriteLine("endobj");
                    writer.Flush();

                    // Resource dictionary (Fonts and Images)
                    offsets.Add(fs.Position);
                    writer.WriteLine($"{resourceObjId} 0 obj");
                    writer.Write("<< /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> /F2 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >> >>");

                    if (page.Images.Count > 0)
                    {
                        writer.Write(" /XObject <<");
                        foreach (var img in page.Images)
                        {
                            writer.Write($" /{img.ResourceName} {img.ObjectId} 0 R");
                        }
                        writer.Write(" >>");
                    }

                    writer.WriteLine(" >>");
                    writer.WriteLine("endobj");
                    writer.Flush();
                }

                // Write Image XObjects
                foreach (var page in _pages)
                {
                    foreach (var img in page.Images)
                    {
                        offsets.Add(fs.Position);
                        writer.WriteLine($"{img.ObjectId} 0 obj");
                        writer.WriteLine($"<< /Type /XObject /Subtype /Image /Width {img.Width} /Height {img.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {img.JpegData.Length} >>");
                        writer.WriteLine("stream");
                        writer.Flush();

                        fs.Write(img.JpegData, 0, img.JpegData.Length);
                        fs.Flush();

                        writer.WriteLine();
                        writer.WriteLine("endstream");
                        writer.WriteLine("endobj");
                        writer.Flush();
                    }
                }

                // XRef Table
                long xrefPos = fs.Position;
                int totalObjects = offsets.Count + 1;
                writer.WriteLine("xref");
                writer.WriteLine($"0 {totalObjects}");
                writer.WriteLine("0000000000 65535 f ");
                for (int i = 0; i < offsets.Count; i++)
                {
                    writer.WriteLine($"{offsets[i]:D10} 00000 n ");
                }

                // Trailer
                writer.WriteLine("trailer");
                writer.WriteLine($"<< /Size {totalObjects} /Root 1 0 R >>");
                writer.WriteLine("startxref");
                writer.WriteLine(xrefPos);
                writer.WriteLine("%%EOF");
                writer.Flush();
            }
        }
    }

    public class PdfImageResource
    {
        public string ResourceName { get; set; }
        public int ObjectId { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] JpegData { get; set; }
    }

    public class PdfPage
    {
        public double Width { get; }
        public double Height { get; }
        public int PageNumber { get; }
        public List<PdfImageResource> Images { get; } = new List<PdfImageResource>();

        private readonly StringBuilder _sb = new StringBuilder();
        private int _imgCounter = 0;
        private static int _globalImageObjId = 200;

        public PdfPage(double width, double height, int pageNumber)
        {
            Width = width;
            Height = height;
            PageNumber = pageNumber;
        }

        public string GetContentStream() => _sb.ToString();

        public void SetLineWidth(double w) =>
            _sb.AppendLine($"{w.ToString("F2", CultureInfo.InvariantCulture)} w");

        public void SetStrokeColor(double r, double g, double b) =>
            _sb.AppendLine($"{r.ToString("F3", CultureInfo.InvariantCulture)} {g.ToString("F3", CultureInfo.InvariantCulture)} {b.ToString("F3", CultureInfo.InvariantCulture)} RG");

        public void SetFillColor(double r, double g, double b) =>
            _sb.AppendLine($"{r.ToString("F3", CultureInfo.InvariantCulture)} {g.ToString("F3", CultureInfo.InvariantCulture)} {b.ToString("F3", CultureInfo.InvariantCulture)} rg");

        public void SetDashPattern(double[] dash, double phase)
        {
            var dStr = string.Join(" ", Array.ConvertAll(dash, d => d.ToString("F1", CultureInfo.InvariantCulture)));
            _sb.AppendLine($"[{dStr}] {phase.ToString("F1", CultureInfo.InvariantCulture)} d");
        }

        public void ClearDashPattern() => _sb.AppendLine("[] 0 d");

        public void DrawLine(double x1, double y1, double x2, double y2)
        {
            _sb.AppendLine($"{x1.ToString("F2", CultureInfo.InvariantCulture)} {y1.ToString("F2", CultureInfo.InvariantCulture)} m");
            _sb.AppendLine($"{x2.ToString("F2", CultureInfo.InvariantCulture)} {y2.ToString("F2", CultureInfo.InvariantCulture)} l S");
        }

        public void DrawRect(double x, double y, double w, double h, bool fill, bool stroke)
        {
            _sb.AppendLine($"{x.ToString("F2", CultureInfo.InvariantCulture)} {y.ToString("F2", CultureInfo.InvariantCulture)} {w.ToString("F2", CultureInfo.InvariantCulture)} {h.ToString("F2", CultureInfo.InvariantCulture)} re");
            if (fill && stroke) _sb.AppendLine("B");
            else if (fill) _sb.AppendLine("f");
            else if (stroke) _sb.AppendLine("S");
        }

        public void DrawPolyline(List<Tuple<double, double>> points)
        {
            if (points == null || points.Count < 2) return;
            _sb.AppendLine($"{points[0].Item1.ToString("F2", CultureInfo.InvariantCulture)} {points[0].Item2.ToString("F2", CultureInfo.InvariantCulture)} m");
            for (int i = 1; i < points.Count; i++)
            {
                _sb.AppendLine($"{points[i].Item1.ToString("F2", CultureInfo.InvariantCulture)} {points[i].Item2.ToString("F2", CultureInfo.InvariantCulture)} l");
            }
            _sb.AppendLine("S");
        }

        public void SetFont(string fontName, double size)
        {
            string fId = fontName.Contains("Bold") ? "/F2" : "/F1";
            _sb.AppendLine($"BT {fId} {size.ToString("F1", CultureInfo.InvariantCulture)} Tf ET");
        }

        public void DrawText(double x, double y, string text)
        {
            string safe = EscapePdfString(text);
            _sb.AppendLine($"BT {x.ToString("F2", CultureInfo.InvariantCulture)} {y.ToString("F2", CultureInfo.InvariantCulture)} Td ({safe}) Tj ET");
        }

        public void DrawTextCentered(double x, double y, string text)
        {
            double approxWidth = text.Length * 3.4;
            DrawText(x - approxWidth / 2.0, y, text);
        }

        public void DrawTextRotated90(double x, double y, string text)
        {
            string safe = EscapePdfString(text);
            _sb.AppendLine("q");
            _sb.AppendLine($"0 1 -1 0 {x.ToString("F2", CultureInfo.InvariantCulture)} {y.ToString("F2", CultureInfo.InvariantCulture)} cm");
            _sb.AppendLine($"BT 0 0 Td ({safe}) Tj ET");
            _sb.AppendLine("Q");
        }

        public void DrawImage(Image img, double x, double y, double width, double height)
        {
            if (img == null) return;
            try
            {
                byte[] jpegBytes;
                int imgW = img.Width;
                int imgH = img.Height;

                // Composite onto pure white background to prevent PNG alpha turning black in JPEG
                using (var bmp = new Bitmap(imgW, imgH, PixelFormat.Format24bppRgb))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.White);
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        g.DrawImage(img, 0, 0, imgW, imgH);
                    }
                    using (var ms = new MemoryStream())
                    {
                        bmp.Save(ms, ImageFormat.Jpeg);
                        jpegBytes = ms.ToArray();
                    }
                }

                _imgCounter++;
                string resName = $"Im{_imgCounter}";
                int objId = ++_globalImageObjId;

                Images.Add(new PdfImageResource
                {
                    ResourceName = resName,
                    ObjectId = objId,
                    Width = imgW,
                    Height = imgH,
                    JpegData = jpegBytes
                });

                _sb.AppendLine("q");
                _sb.AppendLine($"{width.ToString("F2", CultureInfo.InvariantCulture)} 0 0 {height.ToString("F2", CultureInfo.InvariantCulture)} {x.ToString("F2", CultureInfo.InvariantCulture)} {y.ToString("F2", CultureInfo.InvariantCulture)} cm");
                _sb.AppendLine($"/{resName} Do");
                _sb.AppendLine("Q");
            }
            catch
            {
            }
        }

        public void DrawImage(string imagePath, double x, double y, double width, double height)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath)) return;
            try
            {
                using (var img = Image.FromFile(imagePath))
                {
                    DrawImage(img, x, y, width, height);
                }
            }
            catch
            {
            }
        }

        private static string EscapePdfString(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = SanitizeAscii(s);
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (c == '\\') sb.Append("\\\\");
                else if (c == '(') sb.Append("\\(");
                else if (c == ')') sb.Append("\\)");
                else if (c >= 32 && c <= 126) sb.Append(c);
                else sb.Append(' ');
            }
            return sb.ToString();
        }

        public static string SanitizeAscii(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s
                .Replace("—", " | ")
                .Replace("–", "-")
                .Replace("➔", "->")
                .Replace("→", "->")
                .Replace("°", " deg")
                .Replace("“", "\"")
                .Replace("”", "\"")
                .Replace("‘", "'")
                .Replace("’", "'")
                .Replace("•", "*")
                .Replace("±", "+/-");
        }
    }
}
