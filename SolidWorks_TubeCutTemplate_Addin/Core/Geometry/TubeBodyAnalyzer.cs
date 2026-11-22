using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using BajaSAE.TubeCutTemplateAddin.Core.Models;

namespace BajaSAE.TubeCutTemplateAddin.Core.Geometry
{
    public class CylinderFaceGroup
    {
        public double RadiusMm { get; set; }
        public Point3D Axis { get; set; }
        public Point3D Center { get; set; }
        public List<IFace2> Faces { get; } = new List<IFace2>();
        public double TotalAreaMm2 { get; set; }
    }

    public class TubeCylinderInfo
    {
        public IBody2 Body { get; set; }
        public string BodyName { get; set; } = "";
        public double OuterRadiusMm { get; set; }
        public double OuterDiameterMm => OuterRadiusMm * 2.0;
        public double WallThicknessMm { get; set; }
        public Point3D AxisVector { get; set; }
        public Point3D AxisCenter { get; set; }
        public List<IFace2> OuterFaces { get; set; } = new List<IFace2>();
        public List<IFace2> NonOuterFaces { get; set; } = new List<IFace2>();
    }

    /// <summary>
    /// Analyzes SolidWorks solid bodies to detect primary cylindrical tube skins,
    /// outer diameter, wall thickness, and central axis line.
    /// Converts SolidWorks native SI meters to physical millimeters.
    /// </summary>
    public static class TubeBodyAnalyzer
    {
        private const double SwMeterToMm = 1000.0;
        private const double SwSqMeterToSqMm = 1000000.0;

        /// <summary>
        /// Analyzes a solid body to determine if it is a round tube within the specified OD range.
        /// Returns TubeCylinderInfo if valid, or null if not a round tube.
        /// </summary>
        public static TubeCylinderInfo AnalyzeTubeBody(IBody2 body, double minOdMm = 19.05, double maxOdMm = 44.45)
        {
            if (body == null) return null;

            object[] faceArray = body.GetFaces() as object[];
            if (faceArray == null || faceArray.Length == 0) return null;

            var cylGroups = new List<CylinderFaceGroup>();
            var allFaces = new List<IFace2>();

            foreach (object faceObj in faceArray)
            {
                if (!(faceObj is IFace2 face)) continue;
                allFaces.Add(face);

                ISurface surf = face.GetSurface() as ISurface;
                if (surf == null || !surf.IsCylinder()) continue;

                double[] cylParams = surf.CylinderParams as double[];
                if (cylParams == null || cylParams.Length < 7) continue;

                // Parameters: [0..2] Origin (m), [3..5] Axis Vector, [6] Radius (m)
                Point3D center = new Point3D(
                    cylParams[0] * SwMeterToMm,
                    cylParams[1] * SwMeterToMm,
                    cylParams[2] * SwMeterToMm
                );

                Point3D axis = new Point3D(
                    cylParams[3],
                    cylParams[4],
                    cylParams[5]
                ).Normalize();

                double radiusMm = cylParams[6] * SwMeterToMm;
                double areaMm2 = face.GetArea() * SwSqMeterToSqMm;

                // Match with existing group sharing radius, collinear axis and center line
                bool matched = false;
                foreach (var g in cylGroups)
                {
                    if (Math.Abs(g.RadiusMm - radiusMm) < 0.25)
                    {
                        double axisDot = Math.Abs(g.Axis.Dot(axis));
                        if (axisDot > 0.999)
                        {
                            // Radial distance of center to g.Axis
                            Point3D diff = center - g.Center;
                            Point3D radial = diff - (g.Axis * diff.Dot(g.Axis));
                            if (radial.Length < 0.5)
                            {
                                g.Faces.Add(face);
                                g.TotalAreaMm2 += areaMm2;
                                matched = true;
                                break;
                            }
                        }
                    }
                }

                if (!matched)
                {
                    var newGroup = new CylinderFaceGroup
                    {
                        RadiusMm = radiusMm,
                        Axis = axis,
                        Center = center,
                        TotalAreaMm2 = areaMm2
                    };
                    newGroup.Faces.Add(face);
                    cylGroups.Add(newGroup);
                }
            }

            if (cylGroups.Count == 0) return null;

            // Group with the largest total surface area is the outer cylinder skin
            cylGroups.Sort((a, b) => b.TotalAreaMm2.CompareTo(a.TotalAreaMm2));
            CylinderFaceGroup primaryOuterGroup = cylGroups[0];

            double outerOdMm = primaryOuterGroup.RadiusMm * 2.0;

            // Check if outer diameter falls within requested tube range (e.g. 0.75" to 1.75")
            if (outerOdMm < (minOdMm - 0.5) || outerOdMm > (maxOdMm + 0.5))
            {
                return null;
            }

            // Determine wall thickness by finding the concentric inner bore
            double wallThickMm = 1.65; // Default 0.065" (1.65 mm) for solid bodies
            for (int i = 1; i < cylGroups.Count; i++)
            {
                var candidateInner = cylGroups[i];
                if (candidateInner.RadiusMm < (primaryOuterGroup.RadiusMm - 0.4))
                {
                    double axisDot = Math.Abs(primaryOuterGroup.Axis.Dot(candidateInner.Axis));
                    if (axisDot > 0.99)
                    {
                        wallThickMm = primaryOuterGroup.RadiusMm - candidateInner.RadiusMm;
                        break;
                    }
                }
            }

            // Categorize non-outer faces (copes, planar cut ends, inner bore, etc.)
            var outerFaceSet = new HashSet<IFace2>(primaryOuterGroup.Faces);
            var nonOuterFaces = new List<IFace2>();
            foreach (var f in allFaces)
            {
                if (!outerFaceSet.Contains(f))
                {
                    nonOuterFaces.Add(f);
                }
            }

            string bName = null;
            try { bName = body.Name; } catch { }
            if (string.IsNullOrWhiteSpace(bName))
            {
                bName = "Tube";
            }

            return new TubeCylinderInfo
            {
                Body = body,
                BodyName = bName,
                OuterRadiusMm = primaryOuterGroup.RadiusMm,
                WallThicknessMm = wallThickMm,
                AxisVector = primaryOuterGroup.Axis,
                AxisCenter = primaryOuterGroup.Center,
                OuterFaces = primaryOuterGroup.Faces,
                NonOuterFaces = nonOuterFaces
            };
        }
    }
}
