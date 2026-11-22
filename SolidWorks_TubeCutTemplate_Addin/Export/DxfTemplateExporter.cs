using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BajaSAE.TubeCutTemplateAddin.Core.Models;

namespace BajaSAE.TubeCutTemplateAddin.Export
{
    /// <summary>
    /// Generates precision 2D AutoCAD DXF (R12/2000 ASCII) files for CNC plasma/laser tube notchers,
    /// vinyl plotters, or digital inspection. Layered geometry with exact physical dimensions in millimeters.
    /// </summary>
    public static class DxfTemplateExporter
    {
        public static string ExportDxf(UnwrappedTube tube, string outputPath)
        {
            var sb = new StringBuilder();

            // DXF Header & Layers
            WriteDxfHeader(sb);
            WriteDxfTables(sb);

            // ENTITIES section
            sb.AppendLine("0");
            sb.AppendLine("SECTION");
            sb.AppendLine("2");
            sb.AppendLine("ENTITIES");

            double yOffset = 0.0;

            // Draw Calibration Box (1.000" x 1.000" = 25.4 x 25.4 mm)
            WriteCalibrationBox(sb, 0, yOffset + 10.0);
            yOffset += 45.0;

            // Draw End 1
            if (tube.End1 != null)
            {
                WriteEndTemplateDxf(sb, tube, tube.End1, xOrigin: 20.0, yBase: yOffset);
                yOffset += tube.End1.TemplateHeightMm + 40.0;
            }

            // Draw End 2
            if (tube.End2 != null)
            {
                WriteEndTemplateDxf(sb, tube, tube.End2, xOrigin: 20.0, yBase: yOffset);
            }

            // End of Entities and File
            sb.AppendLine("0");
            sb.AppendLine("ENDSEC");
            sb.AppendLine("0");
            sb.AppendLine("EOF");

            File.WriteAllText(outputPath, sb.ToString(), Encoding.ASCII);
            return outputPath;
        }

        private static void WriteDxfHeader(StringBuilder sb)
        {
            sb.AppendLine("0");
            sb.AppendLine("SECTION");
            sb.AppendLine("2");
            sb.AppendLine("HEADER");
            sb.AppendLine("9");
            sb.AppendLine("$ACADVER");
            sb.AppendLine("1");
            sb.AppendLine("AC1009"); // AutoCAD R12 ASCII (universal compatibility)
            sb.AppendLine("9");
            sb.AppendLine("$INSUNITS");
            sb.AppendLine("70");
            sb.AppendLine("4"); // Millimeters
            sb.AppendLine("0");
            sb.AppendLine("ENDSEC");
        }

        private static void WriteDxfTables(StringBuilder sb)
        {
            sb.AppendLine("0");
            sb.AppendLine("SECTION");
            sb.AppendLine("2");
            sb.AppendLine("TABLES");

            // LAYER TABLE
            sb.AppendLine("0");
            sb.AppendLine("TABLE");
            sb.AppendLine("2");
            sb.AppendLine("LAYER");
            sb.AppendLine("70");
            sb.AppendLine("7");

            WriteLayerDef(sb, "0_CUT_CONTOUR", 7);     // White / Black
            WriteLayerDef(sb, "0_COLLAR_BASELINE", 4); // Cyan
            WriteLayerDef(sb, "0_CENTERLINE_0", 1);    // Red
            WriteLayerDef(sb, "0_CENTERLINE_180", 5);  // Blue
            WriteLayerDef(sb, "0_OVERLAP_TAB", 8);     // Gray
            WriteLayerDef(sb, "0_CALIBRATION", 3);     // Green
            WriteLayerDef(sb, "0_ANNOTATIONS", 2);     // Yellow

            sb.AppendLine("0");
            sb.AppendLine("ENDTAB");
            sb.AppendLine("0");
            sb.AppendLine("ENDSEC");
        }

        private static void WriteLayerDef(StringBuilder sb, string layerName, int colorCode)
        {
            sb.AppendLine("0");
            sb.AppendLine("LAYER");
            sb.AppendLine("2");
            sb.AppendLine(layerName);
            sb.AppendLine("70");
            sb.AppendLine("0");
            sb.AppendLine("62");
            sb.AppendLine(colorCode.ToString());
            sb.AppendLine("6");
            sb.AppendLine("CONTINUOUS");
        }

        private static void WriteCalibrationBox(StringBuilder sb, double x, double y)
        {
            const double sizeMm = 25.4; // 1.000 inch
            string layer = "0_CALIBRATION";

            WriteLine(sb, layer, x, y, x + sizeMm, y);
            WriteLine(sb, layer, x + sizeMm, y, x + sizeMm, y + sizeMm);
            WriteLine(sb, layer, x + sizeMm, y + sizeMm, x, y + sizeMm);
            WriteLine(sb, layer, x, y + sizeMm, x, y);

            // Crosshairs
            WriteLine(sb, layer, x + sizeMm / 2.0, y, x + sizeMm / 2.0, y + sizeMm);
            WriteLine(sb, layer, x, y + sizeMm / 2.0, x + sizeMm, y + sizeMm / 2.0);

            WriteText(sb, layer, x + 2.0, y + sizeMm / 2.0 + 3.0, 2.5, "1.000\" x 1.000\" (25.4mm)");
            WriteText(sb, layer, x + 2.0, y + sizeMm / 2.0 - 5.0, 2.0, "CALIBRATION BOX");
        }

        private static void WriteEndTemplateDxf(
            StringBuilder sb, UnwrappedTube tube, TubeEndProfile end, double xOrigin, double yBase)
        {
            double w = end.TemplateWidthMm;
            double h = end.TemplateHeightMm;
            double overlapW = 6.35; // 0.25" in mm

            // 1. Overlap Tab
            WriteLine(sb, "0_OVERLAP_TAB", xOrigin - overlapW, yBase, xOrigin - overlapW, yBase + h);
            WriteLine(sb, "0_OVERLAP_TAB", xOrigin - overlapW, yBase + h, xOrigin, yBase + h);
            WriteLine(sb, "0_OVERLAP_TAB", xOrigin - overlapW, yBase, xOrigin, yBase);

            // 2. Straight Collar Baseline
            WriteLine(sb, "0_COLLAR_BASELINE", xOrigin - overlapW, yBase, xOrigin + w, yBase);

            // 3. Side Seams
            WriteLine(sb, "0_CUT_CONTOUR", xOrigin, yBase, xOrigin, yBase + h);
            WriteLine(sb, "0_CUT_CONTOUR", xOrigin + w, yBase, xOrigin + w, yBase + h);

            // 4. Centerlines (0 deg and 180 deg)
            WriteLine(sb, "0_CENTERLINE_0", xOrigin, yBase - 8.0, xOrigin, yBase + h + 8.0);
            WriteLine(sb, "0_CENTERLINE_0", xOrigin + w, yBase - 8.0, xOrigin + w, yBase + h + 8.0);
            WriteLine(sb, "0_CENTERLINE_180", xOrigin + w / 2.0, yBase - 5.0, xOrigin + w / 2.0, yBase + h + 5.0);

            // 5. Fishmouth Coping Cut Contour
            if (end.UnwrappedCutPoints != null && end.UnwrappedCutPoints.Count > 1)
            {
                var pts = end.UnwrappedCutPoints;
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    double x1 = xOrigin + pts[i].X;
                    double y1 = yBase + pts[i].Y;
                    double x2 = xOrigin + pts[i + 1].X;
                    double y2 = yBase + pts[i + 1].Y;
                    WriteLine(sb, "0_CUT_CONTOUR", x1, y1, x2, y2);
                }
            }

            // 6. Labels and Node Annotation
            WriteText(sb, "0_ANNOTATIONS", xOrigin + 4.0, yBase + 4.0, 3.5, "CL [0 DEG] SEAM");
            WriteText(sb, "0_ANNOTATIONS", xOrigin + w / 2.0 - 15.0, yBase + 4.0, 3.5, "CL [180 DEG]");
            WriteText(sb, "0_ANNOTATIONS", xOrigin + w - 40.0, yBase + 4.0, 3.5, "CL [360 DEG] SEAM");

            string label = $"BSAE 2027 REV A | TUBE: {tube.TubeId} | END {end.EndIndex} | NODE: {end.NodeName} | OD: {end.TubeOdInches:F3}\"";
            WriteText(sb, "0_ANNOTATIONS", xOrigin + w + 10.0, yBase + h - 5.0, 4.0, label);
        }

        private static void WriteLine(StringBuilder sb, string layer, double x1, double y1, double x2, double y2)
        {
            sb.AppendLine("0");
            sb.AppendLine("LINE");
            sb.AppendLine("8");
            sb.AppendLine(layer);
            sb.AppendLine("10");
            sb.AppendLine(x1.ToString("F3", CultureInfo.InvariantCulture));
            sb.AppendLine("20");
            sb.AppendLine(y1.ToString("F3", CultureInfo.InvariantCulture));
            sb.AppendLine("30");
            sb.AppendLine("0.0");
            sb.AppendLine("11");
            sb.AppendLine(x2.ToString("F3", CultureInfo.InvariantCulture));
            sb.AppendLine("21");
            sb.AppendLine(y2.ToString("F3", CultureInfo.InvariantCulture));
            sb.AppendLine("31");
            sb.AppendLine("0.0");
        }

        private static void WriteText(StringBuilder sb, string layer, double x, double y, double height, string text)
        {
            sb.AppendLine("0");
            sb.AppendLine("TEXT");
            sb.AppendLine("8");
            sb.AppendLine(layer);
            sb.AppendLine("10");
            sb.AppendLine(x.ToString("F3", CultureInfo.InvariantCulture));
            sb.AppendLine("20");
            sb.AppendLine(y.ToString("F3", CultureInfo.InvariantCulture));
            sb.AppendLine("30");
            sb.AppendLine("0.0");
            sb.AppendLine("40");
            sb.AppendLine(height.ToString("F2", CultureInfo.InvariantCulture));
            sb.AppendLine("1");
            sb.AppendLine(text ?? "");
        }
    }
}
