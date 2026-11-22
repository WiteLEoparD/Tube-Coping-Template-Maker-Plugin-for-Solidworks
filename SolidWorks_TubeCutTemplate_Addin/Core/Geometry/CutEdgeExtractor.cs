using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using BajaSAE.TubeCutTemplateAddin.Core.Models;

namespace BajaSAE.TubeCutTemplateAddin.Core.Geometry
{
    public class SampledEdge
    {
        public IEdge Edge { get; set; }
        public Point3D StartPoint { get; set; }
        public Point3D EndPoint { get; set; }
        public List<Point3D> SampledPoints { get; set; } = new List<Point3D>();
        public double LengthMm { get; set; }
    }

    /// <summary>
    /// Extracts true 3D cutting edges from a SolidWorks tube body,
    /// rigorously discarding internal longitudinal CAD seam edges.
    /// </summary>
    public static class CutEdgeExtractor
    {
        private const double SwMeterToMm = 1000.0;

        /// <summary>
        /// Extracts all true cut edges from the tube body that define the end notch boundaries.
        /// </summary>
        public static List<SampledEdge> ExtractCutEdges(TubeCylinderInfo info, int samplesPerEdge = 40)
        {
            var result = new List<SampledEdge>();
            if (info?.Body == null) return result;

            var outerFaceSet = new HashSet<IFace2>(info.OuterFaces);
            object[] edgeArray = info.Body.GetEdges() as object[];
            if (edgeArray == null) return result;

            foreach (object edgeObj in edgeArray)
            {
                if (!(edgeObj is IEdge edge)) continue;

                // An edge is a cut edge IF AND ONLY IF it is shared by the outer cylinder skin
                // AND a non-outer face (planar miter face, cope cut face, or inner bore).
                object[] adjFaces = edge.GetTwoAdjacentFaces2() as object[];
                if (adjFaces == null || adjFaces.Length < 2)
                {
                    adjFaces = edge.GetTwoAdjacentFaces() as object[];
                }

                if (adjFaces == null || adjFaces.Length < 2) continue;

                bool hasOuterFace = false;
                bool hasNonOuterFace = false;

                foreach (object faceObj in adjFaces)
                {
                    if (faceObj is IFace2 face)
                    {
                        if (outerFaceSet.Contains(face))
                            hasOuterFace = true;
                        else
                            hasNonOuterFace = true;
                    }
                }

                // If not shared between outer and non-outer, it's either an internal seam or an inner-only edge
                if (!hasOuterFace || !hasNonOuterFace) continue;

                // Verify that edge is not a longitudinal seam edge parallel to the axis
                ICurve curve = edge.GetCurve() as ICurve;
                if (curve == null) continue;

                double startParam = 0.0, endParam = 1.0;
                double[] curveParams = edge.GetCurveParams() as double[];
                if (curveParams != null && curveParams.Length >= 2)
                {
                    startParam = curveParams[0];
                    endParam = curveParams[1];
                }

                double lengthM = 0.05;
                try
                {
                    lengthM = curve.GetLength2(startParam, endParam);
                }
                catch
                {
                    lengthM = 0.05;
                }

                double lengthMm = lengthM * SwMeterToMm;
                if (lengthMm < 0.05) continue; // Skip microscopic degenerate tolerance edges

                // Sample points along the 3D curve
                int nPts = Math.Max(samplesPerEdge, (int)(lengthMm / 0.5));
                var points = new List<Point3D>();

                double pStep = (endParam - startParam) / Math.Max(1, nPts - 1);
                for (int i = 0; i < nPts; i++)
                {
                    double t = startParam + i * pStep;
                    double[] evalData = curve.Evaluate2(t, 0) as double[];
                    if (evalData != null && evalData.Length >= 3)
                    {
                        points.Add(new Point3D(
                            evalData[0] * SwMeterToMm,
                            evalData[1] * SwMeterToMm,
                            evalData[2] * SwMeterToMm
                        ));
                    }
                }

                if (points.Count < 2) continue;

                // Check direction vector between start and end: if strictly parallel to axis and linear, skip seam
                Point3D chord = (points[points.Count - 1] - points[0]).Normalize();
                if (Math.Abs(chord.Dot(info.AxisVector)) > 0.9995 && curve.IsLine())
                {
                    // Internal longitudinal seam edge
                    continue;
                }

                result.Add(new SampledEdge
                {
                    Edge = edge,
                    StartPoint = points[0],
                    EndPoint = points[points.Count - 1],
                    SampledPoints = points,
                    LengthMm = lengthMm
                });
            }

            return result;
        }
    }
}
