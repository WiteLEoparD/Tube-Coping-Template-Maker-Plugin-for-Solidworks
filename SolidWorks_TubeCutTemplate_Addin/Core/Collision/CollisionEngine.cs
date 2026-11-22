using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using BajaSAE.TubeCutTemplateAddin.Core.Models;
using BajaSAE.TubeCutTemplateAddin.Core.Geometry;

namespace BajaSAE.TubeCutTemplateAddin.Core.Collision
{
    /// <summary>
    /// Performs automated collision and penetration checks between tube bodies.
    /// In accordance with mission requirements: If two tube bodies collide or penetrate
    /// through each other in CAD, they are skipped and explicit diagnostic warnings are logged.
    /// </summary>
    public static class CollisionEngine
    {
        private const double SwMeterToMm = 1000.0;
        private const double SwCubicMeterToCubicMm = 1000000000.0;
        public const double CollisionVolumeThresholdMm3 = 20.0; // 20 mm³ cutoff
        public const double MaxNotchDepthMultiplier = 3.5;      // 3.5 * OD cutoff

        /// <summary>
        /// Scans all candidate tube bodies for mutual penetration collisions.
        /// </summary>
        public static List<CollisionDiagnostic> CheckBodyIntersections(
            List<TubeCylinderInfo> tubeList,
            ISldWorks swApp,
            out HashSet<string> collidingBodyNames)
        {
            var diagnostics = new List<CollisionDiagnostic>();
            collidingBodyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (tubeList == null || tubeList.Count < 2) return diagnostics;

            for (int i = 0; i < tubeList.Count; i++)
            {
                TubeCylinderInfo tubeA = tubeList[i];
                if (tubeA.Body == null) continue;

                double[] boxA = tubeA.Body.GetBodyBox() as double[];
                if (boxA == null || boxA.Length < 6) continue;

                for (int j = i + 1; j < tubeList.Count; j++)
                {
                    TubeCylinderInfo tubeB = tubeList[j];
                    if (tubeB.Body == null) continue;

                    double[] boxB = tubeB.Body.GetBodyBox() as double[];
                    if (boxB == null || boxB.Length < 6) continue;

                    // 1. Fast Axis-Aligned Bounding Box (AABB) intersection test
                    if (!BoxesOverlap(boxA, boxB, marginM: 0.0005))
                    {
                        continue;
                    }

                    // 2. Exact SolidWorks B-Rep Boolean Intersection
                    try
                    {
                        IBody2 copyA = tubeA.Body.Copy() as IBody2;
                        if (copyA == null) continue;

                        int opError = 0;
                        object resultBodiesObj = copyA.Operations2(
                            (int)swBodyOperationType_e.SWBODYINTERSECT,
                            tubeB.Body,
                            out opError
                        );

                        if (resultBodiesObj is object[] resultBodies && resultBodies.Length > 0)
                        {
                            double totalOverlapVolMm3 = 0.0;

                            foreach (object rBodyObj in resultBodies)
                            {
                                if (rBodyObj is IBody2 rBody)
                                {
                                    double[] massProps = rBody.GetMassProperties(1.0) as double[];
                                    if (massProps != null && massProps.Length > 3)
                                    {
                                        double volM3 = massProps[3];
                                        totalOverlapVolMm3 += volM3 * SwCubicMeterToCubicMm;
                                    }
                                }
                            }

                            if (totalOverlapVolMm3 > CollisionVolumeThresholdMm3)
                            {
                                string nameA = tubeA.BodyName;
                                string nameB = tubeB.BodyName;

                                collidingBodyNames.Add(nameA);
                                collidingBodyNames.Add(nameB);

                                diagnostics.Add(new CollisionDiagnostic
                                {
                                    Tube1Name = nameA,
                                    Tube2Name = nameB,
                                    IntersectionVolumeMm3 = totalOverlapVolMm3,
                                    Reason = $"Solid bodies physically collide and penetrate each other in CAD (Overlap Volume: {totalOverlapVolMm3:F1} mm³ > {CollisionVolumeThresholdMm3} mm³). Tubes must be properly trimmed using Trim/Extend before generating templates."
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log warning if boolean kernel throws on non-manifold condition
                        System.Diagnostics.Debug.WriteLine($"[CollisionEngine] Error testing {tubeA.BodyName} vs {tubeB.BodyName}: {ex.Message}");
                    }
                }
            }

            return diagnostics;
        }

        /// <summary>
        /// Validates that notch depth does not exceed 3.5 * OD, which indicates
        /// an abnormal mid-body piercing cut rather than a valid end cope.
        /// </summary>
        public static bool ValidateNotchDepth(double cutDepthMm, double tubeOdMm, out string warning)
        {
            double maxAllowed = tubeOdMm * MaxNotchDepthMultiplier;
            if (cutDepthMm > maxAllowed)
            {
                warning = $"Notch depth ({cutDepthMm:F1} mm) exceeds 3.5x OD ({maxAllowed:F1} mm). Mid-body penetration detected.";
                return false;
            }

            warning = "";
            return true;
        }

        private static bool BoxesOverlap(double[] bA, double[] bB, double marginM)
        {
            // b[0..2] Min (X,Y,Z), b[3..5] Max (X,Y,Z)
            if (bA[3] + marginM < bB[0] || bB[3] + marginM < bA[0]) return false;
            if (bA[4] + marginM < bB[1] || bB[4] + marginM < bA[1]) return false;
            if (bA[5] + marginM < bB[2] || bB[5] + marginM < bA[2]) return false;
            return true;
        }
    }
}
