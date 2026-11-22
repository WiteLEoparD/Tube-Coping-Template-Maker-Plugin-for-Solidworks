using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BajaSAE.TubeCutTemplateAddin.Core.Models;

namespace BajaSAE.TubeCutTemplateAddin.Export
{
    /// <summary>
    /// Generates scalable vector graphics (SVG) with true 1:1 physical dimensions (8.5" x 11.0" US Letter).
    /// Branded for Red Lion Racing | University of Manitoba.
    /// </summary>
    public static class SvgTemplateExporter
    {
        private const double LetterWidthPt = 612.0;
        private const double LetterHeightPt = 792.0;
        private const double MmToPt = 72.0 / 25.4;
        private const double SafeMarginPt = 36.0;

        public static string ExportSvg(UnwrappedTube tube, string outputPath)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\" ?>");
            string wStr = F(LetterWidthPt);
            string hStr = F(LetterHeightPt);
            sb.AppendLine($"<svg baseProfile=\"full\" width=\"8.5in\" height=\"11.0in\" viewBox=\"0 0 {wStr} {hStr}\" version=\"1.1\" xmlns=\"http://www.w3.org/2000/svg\">");

            // Stylesheet defs
            sb.AppendLine("  <defs>");
            sb.AppendLine("    <style type=\"text/css\"><![CDATA[");
            sb.AppendLine("      .cutline { stroke: #000000; stroke-width: 2.0; fill: none; }");
            sb.AppendLine("      .baseline { stroke: #000000; stroke-width: 1.0; stroke-dasharray: 4,4; fill: none; }");
            sb.AppendLine("      .centerline { stroke: #E53E3E; stroke-width: 1.2; stroke-dasharray: 6,2,2,2; fill: none; }");
            sb.AppendLine("      .midline { stroke: #3182CE; stroke-width: 0.8; stroke-dasharray: 4,4; fill: none; }");
            sb.AppendLine("      .overlap { stroke: #A0AEC0; stroke-width: 0.5; stroke-dasharray: 3,3; fill: #EDF2F7; }");
            sb.AppendLine("      .text-main { font-family: Helvetica, Arial, sans-serif; font-size: 8px; fill: #1A202C; }");
            sb.AppendLine("      .text-bold { font-family: Helvetica, Arial, sans-serif; font-size: 8px; font-weight: bold; fill: #1A202C; }");
            sb.AppendLine("    ]]></style>");
            sb.AppendLine("  </defs>");

            // 1.000" x 1.000" Calibration Box
            double calX = LetterWidthPt - SafeMarginPt - 72.0;
            double calY = SafeMarginPt;
            sb.AppendLine($"  <rect x=\"{F(calX)}\" y=\"{F(calY)}\" width=\"72.0\" height=\"72.0\" fill=\"none\" stroke=\"black\" stroke-width=\"1\" />");
            sb.AppendLine($"  <line x1=\"{F(calX + 36.0)}\" y1=\"{F(calY)}\" x2=\"{F(calX + 36.0)}\" y2=\"{F(calY + 72.0)}\" stroke=\"gray\" stroke-dasharray=\"2,2\" />");
            sb.AppendLine($"  <line x1=\"{F(calX)}\" y1=\"{F(calY + 36.0)}\" x2=\"{F(calX + 72.0)}\" y2=\"{F(calY + 36.0)}\" stroke=\"gray\" stroke-dasharray=\"2,2\" />");
            sb.AppendLine($"  <text x=\"{F(calX + 8.0)}\" y=\"{F(calY + 32.0)}\" class=\"text-bold\">1.000\" x 1.000\"</text>");
            sb.AppendLine($"  <text x=\"{F(calX + 6.0)}\" y=\"{F(calY + 44.0)}\" class=\"text-main\">CALIBRATION</text>");

            // Header Text: Red Lion Racing | University of Manitoba
            string tubeId = string.IsNullOrWhiteSpace(tube.TubeId) ? "Chassis Tube" : tube.TubeId;
            sb.AppendLine($"  <text x=\"{F(SafeMarginPt)}\" y=\"{F(SafeMarginPt + 4.0)}\" class=\"text-bold\" font-size=\"12px\" fill=\"#B8141F\">RED LION RACING | UNIVERSITY OF MANITOBA</text>");
            sb.AppendLine($"  <text x=\"{F(SafeMarginPt)}\" y=\"{F(SafeMarginPt + 16.0)}\" class=\"text-main\" font-size=\"8.5px\">1:1 CHASSIS TUBE CUT TEMPLATE WRAP</text>");
            sb.AppendLine($"  <text x=\"{F(SafeMarginPt)}\" y=\"{F(SafeMarginPt + 28.0)}\" class=\"text-bold\" font-size=\"8px\">TUBE: {tubeId} | OD: {tube.TubeOdInches:F3}\" ({tube.TubeOdMm:F1}mm) | WALL: {tube.TubeWallInches:F3}\" | CUT LENGTH: {tube.TotalTubeLengthInches:F2}\"</text>");

            double currentY = SafeMarginPt + 70.0;

            // Render End 1
            if (tube.End1 != null)
            {
                RenderEndSvg(sb, tube, tube.End1, currentY);
                currentY += (tube.End1.TemplateHeightMm * MmToPt) + 40.0;
            }

            // Render End 2
            if (tube.End2 != null)
            {
                RenderEndSvg(sb, tube, tube.End2, currentY);
            }

            sb.AppendLine("</svg>");

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
            return outputPath;
        }

        private static void RenderEndSvg(StringBuilder sb, UnwrappedTube tube, TubeEndProfile end, double yBase)
        {
            double wPt = end.TemplateWidthMm * MmToPt;
            double hPt = end.TemplateHeightMm * MmToPt;
            double overlapPt = 18.0; // 0.25"
            double xOrigin = SafeMarginPt + overlapPt;
            double yBottom = yBase + hPt;

            // 1. Overlap Tab
            sb.AppendLine($"  <rect x=\"{F(xOrigin - overlapPt)}\" y=\"{F(yBase)}\" width=\"{F(overlapPt)}\" height=\"{F(hPt)}\" class=\"overlap\" />");

            // 2. Collar Baseline
            sb.AppendLine($"  <line x1=\"{F(xOrigin - overlapPt)}\" y1=\"{F(yBottom)}\" x2=\"{F(xOrigin + wPt)}\" y2=\"{F(yBottom)}\" class=\"baseline\" />");

            // 3. Side Seams
            sb.AppendLine($"  <line x1=\"{F(xOrigin)}\" y1=\"{F(yBase)}\" x2=\"{F(xOrigin)}\" y2=\"{F(yBottom)}\" stroke=\"black\" stroke-width=\"0.8\" />");
            sb.AppendLine($"  <line x1=\"{F(xOrigin + wPt)}\" y1=\"{F(yBase)}\" x2=\"{F(xOrigin + wPt)}\" y2=\"{F(yBottom)}\" stroke=\"black\" stroke-width=\"0.8\" />");

            // 4. Centerlines
            sb.AppendLine($"  <line x1=\"{F(xOrigin)}\" y1=\"{F(yBase - 8.0)}\" x2=\"{F(xOrigin)}\" y2=\"{F(yBottom + 8.0)}\" class=\"centerline\" />");
            sb.AppendLine($"  <line x1=\"{F(xOrigin + wPt)}\" y1=\"{F(yBase - 8.0)}\" x2=\"{F(xOrigin + wPt)}\" y2=\"{F(yBottom + 8.0)}\" class=\"centerline\" />");
            sb.AppendLine($"  <line x1=\"{F(xOrigin + wPt / 2.0)}\" y1=\"{F(yBase - 5.0)}\" x2=\"{F(xOrigin + wPt / 2.0)}\" y2=\"{F(yBottom + 5.0)}\" class=\"midline\" />");

            // 5. Fishmouth Coping Cut Contour
            if (end.UnwrappedCutPoints != null && end.UnwrappedCutPoints.Count > 1)
            {
                var ptsStr = new StringBuilder();
                foreach (var pt in end.UnwrappedCutPoints)
                {
                    double sx = xOrigin + pt.X * MmToPt;
                    double sy = yBottom - pt.Y * MmToPt; // Flip Y for standard SVG coords
                    if (ptsStr.Length > 0) ptsStr.Append(" ");
                    ptsStr.Append($"{F(sx)},{F(sy)}");
                }
                sb.AppendLine($"  <polyline class=\"cutline\" points=\"{ptsStr}\" />");
            }

            // 6. Label (Red Lion Racing Clean Layout)
            string nodeStr = string.IsNullOrWhiteSpace(end.NodeName) ? $"End {end.EndIndex}" : end.NodeName;
            sb.AppendLine($"  <text x=\"{F(xOrigin + wPt + 15.0)}\" y=\"{F(yBase + 15.0)}\" class=\"text-bold\">END {end.EndIndex} CUT SPEC | NODE: {nodeStr}</text>");
            sb.AppendLine($"  <text x=\"{F(xOrigin + wPt + 15.0)}\" y=\"{F(yBase + 30.0)}\" class=\"text-main\">Cut Depth: {end.CutDepthInches:F3}\" ({end.CutDepthMm:F1}mm) | Collar: {end.CollarLengthInches:F2}\" ({end.CollarLengthMm:F1}mm)</text>");
        }

        private static string F(double v) => v.ToString("F2", CultureInfo.InvariantCulture);
    }
}
