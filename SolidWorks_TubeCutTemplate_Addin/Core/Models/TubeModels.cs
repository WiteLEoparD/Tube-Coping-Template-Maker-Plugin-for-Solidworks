using System;
using System.Collections.Generic;

namespace BajaSAE.TubeCutTemplateAddin.Core.Models
{
    /// <summary>
    /// Represents a 3D point or vector in physical millimeters.
    /// </summary>
    public struct Point3D
    {
        public double X;
        public double Y;
        public double Z;

        public Point3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Point3D Zero => new Point3D(0, 0, 0);

        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

        public Point3D Normalize()
        {
            double len = Length;
            if (len < 1e-12)
                return new Point3D(0, 0, 1);
            return new Point3D(X / len, Y / len, Z / len);
        }

        public double Dot(Point3D other)
        {
            return X * other.X + Y * other.Y + Z * other.Z;
        }

        public Point3D Cross(Point3D other)
        {
            return new Point3D(
                Y * other.Z - Z * other.Y,
                Z * other.X - X * other.Z,
                X * other.Y - Y * other.X
            );
        }

        public double DistanceTo(Point3D other)
        {
            return (this - other).Length;
        }

        public static Point3D operator +(Point3D a, Point3D b) => new Point3D(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Point3D operator -(Point3D a, Point3D b) => new Point3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Point3D operator *(Point3D a, double scalar) => new Point3D(a.X * scalar, a.Y * scalar, a.Z * scalar);
        public static Point3D operator *(double scalar, Point3D a) => new Point3D(a.X * scalar, a.Y * scalar, a.Z * scalar);
        public static Point3D operator /(Point3D a, double scalar) => new Point3D(a.X / scalar, a.Y / scalar, a.Z / scalar);

        public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
    }

    /// <summary>
    /// Represents a 2D point on an unrolled flat wrap sheet (physical millimeters).
    /// </summary>
    public struct Point2D
    {
        public double X;
        public double Y;

        public Point2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() => $"({X:F2}, {Y:F2})";
    }

    /// <summary>
    /// Unrolled 2D cut profile for a single tube end (fishmouth cope).
    /// </summary>
    public class TubeEndProfile
    {
        public int EndIndex { get; set; } // 1 (Start) or 2 (End)
        public string NodeName { get; set; } = "UNMAPPED";
        public string MatingTarget { get; set; } = "Chassis Tube";
        public double TubeOdMm { get; set; } = 31.75;
        public double TubeWallMm { get; set; } = 1.65;
        public double CollarLengthMm { get; set; } = 38.1; // Default 1.50"

        /// <summary>
        /// Equidistant 360-point unwrapped cut coordinates (X in [0, pi*OD], Y relative to collar baseline).
        /// </summary>
        public List<Point2D> UnwrappedCutPoints { get; set; } = new List<Point2D>();

        public double CutDepthMm { get; set; }
        public double TemplateWidthMm { get; set; }  // pi * OD
        public double TemplateHeightMm { get; set; } // CutDepthMm + CollarLengthMm
        public double CutAngleDeg { get; set; } = 90.0;

        public Point3D NodePoint3D { get; set; }
        public double CenterlineOffsetMm { get; set; }

        public double TubeOdInches => TubeOdMm / 25.4;
        public double TubeWallInches => TubeWallMm / 25.4;
        public double CutDepthInches => CutDepthMm / 25.4;
        public double CollarLengthInches => CollarLengthMm / 25.4;
        public double TemplateWidthInches => TemplateWidthMm / 25.4;
        public double TemplateHeightInches => TemplateHeightMm / 25.4;
        public double CenterlineOffsetInches => CenterlineOffsetMm / 25.4;
    }

    /// <summary>
    /// Complete tube member containing both End 1 and End 2 profiles.
    /// </summary>
    public class UnwrappedTube
    {
        public string TubeId { get; set; } = "";
        public string TubeName { get; set; } = "";
        public double TubeOdMm { get; set; }
        public double TubeWallMm { get; set; }
        public Point3D AxisVector { get; set; }
        public Point3D AxisCenter { get; set; }
        public Point3D ReferenceClockingU { get; set; } // theta = 0 vector
        public Point3D ReferenceClockingV { get; set; } // tube axis x U

        public double TotalTubeLengthMm { get; set; } // Tip-to-tip cut length
        public double CenterToCenterLengthMm { get; set; }

        public TubeEndProfile End1 { get; set; }
        public TubeEndProfile End2 { get; set; }

        // BSAE Classification
        public string BsaeMemberName { get; set; } = "";
        public string BsaeCategory { get; set; } = "Primary Member";
        public string BsaeRuleRef { get; set; } = "BSAE 2027 Rev A - Art. B.3";

        // Locator Diagram Thumbnail
        public string ThumbnailImagePath { get; set; } = "";
        public Point3D CenterOfMass { get; set; }

        // Validation & Collision Flags
        public bool IsValid { get; set; } = true;
        public bool HasCollisionError { get; set; } = false;
        public string WarningMessage { get; set; } = "";

        public double TubeOdInches => TubeOdMm / 25.4;
        public double TubeWallInches => TubeWallMm / 25.4;
        public double TotalTubeLengthInches => TotalTubeLengthMm / 25.4;
        public double CenterToCenterLengthInches => CenterToCenterLengthMm / 25.4;
    }

    /// <summary>
    /// Diagnostic report for an intersection collision or abnormal penetration.
    /// </summary>
    public class CollisionDiagnostic
    {
        public string Tube1Name { get; set; } = "";
        public string Tube2Name { get; set; } = "";
        public double IntersectionVolumeMm3 { get; set; }
        public double MaxNotchDepthMm { get; set; }
        public string Reason { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public override string ToString() =>
            $"[COLLISION DIAGNOSTIC] '{Tube1Name}' <-> '{Tube2Name}' | Vol: {IntersectionVolumeMm3:F1} mm³ | {Reason}";
    }

    /// <summary>
    /// User options configured in the PropertyManagerPage.
    /// </summary>
    public class TemplateOptions
    {
        public double CollarLengthInches { get; set; } = 1.50; // 38.1 mm
        public bool GeneratePdf { get; set; } = true;
        public bool GenerateDxf { get; set; } = true;
        public bool GenerateSvg { get; set; } = true;
        public string OutputDirectory { get; set; } = "";
        public bool SelectAllChassisTubes { get; set; } = false;

        public double MinOdInches { get; set; } = 0.75;
        public double MaxOdInches { get; set; } = 1.75;

        public double CollarLengthMm => CollarLengthInches * 25.4;
    }

    /// <summary>
    /// Execution results and summary statistics.
    /// </summary>
    public class GenerationReport
    {
        public int TotalBodiesExamined { get; set; }
        public int TubesProcessed { get; set; }
        public int TubesSkippedDueToCollision { get; set; }
        public List<string> GeneratedPdfFiles { get; set; } = new List<string>();
        public List<string> GeneratedDxfFiles { get; set; } = new List<string>();
        public List<string> GeneratedSvgFiles { get; set; } = new List<string>();
        public List<CollisionDiagnostic> CollisionDiagnostics { get; set; } = new List<CollisionDiagnostic>();
        public TimeSpan Duration { get; set; }
    }
}
