using System;
using System.Collections.Generic;
using System.Linq;
using BajaSAE.TubeCutTemplateAddin.Core.Models;
using BajaSAE.TubeCutTemplateAddin.Core.Geometry;

namespace BajaSAE.TubeCutTemplateAddin.Core.Unroller
{
    /// <summary>
    /// Implements precision 1:1 unwrapping of cylindrical coping profiles,
    /// eliminating comb/sawtooth oscillations through closed topological perimeter
    /// sampling and monotonic angular unwrapping.
    /// </summary>
    public static class SpikeFreeUnroller
    {
        public const int DefaultGridSamples = 360;

        /// <summary>
        /// Computes the canonical shared clocking vector U perpendicular to the tube axis.
        /// This vector represents theta = 0 for both End 1 and End 2 so fabricators can scribe
        /// a single straight line along the raw tube to rotationally index both notches.
        /// </summary>
        public static Point3D ComputeSharedClockingVector(Point3D axis)
        {
            Point3D a = axis.Normalize();
            Point3D globalZ = new Point3D(0.0, 0.0, 1.0);

            // If tube is nearly vertical along Z, use global Y (lateral)
            Point3D globalRef = Math.Abs(a.Dot(globalZ)) > 0.92 ?
                new Point3D(0.0, 1.0, 0.0) : globalZ;

            Point3D u = globalRef - a * globalRef.Dot(a);
            if (u.Length < 1e-6)
            {
                globalRef = new Point3D(1.0, 0.0, 0.0);
                u = globalRef - a * globalRef.Dot(a);
            }

            return u.Normalize();
        }

        /// <summary>
        /// Unrolls a validated tube body into true 1:1 End 1 and End 2 cut templates.
        /// </summary>
        public static UnwrappedTube UnrollTube(
            TubeCylinderInfo info,
            List<SampledEdge> cutEdges,
            double collarLengthMm = 38.1)
        {
            double r = info.OuterRadiusMm;
            double od = info.OuterDiameterMm;
            double wall = info.WallThicknessMm;
            Point3D axis = info.AxisVector.Normalize();
            Point3D center = info.AxisCenter;

            Point3D uClocking = ComputeSharedClockingVector(axis);
            Point3D vClocking = axis.Cross(uClocking).Normalize();

            double circumference = 2.0 * Math.PI * r;

            // If no cut edges (straight cut ends), generate square cut baseline templates
            if (cutEdges == null || cutEdges.Count == 0)
            {
                return GenerateSquareCutTube(info, collarLengthMm, uClocking, vClocking);
            }

            // 1. Assemble cut edges into closed topological loops
            List<List<SampledEdge>> loops = AssembleClosedLoops(cutEdges);

            if (loops == null || loops.Count == 0)
            {
                return GenerateSquareCutTube(info, collarLengthMm, uClocking, vClocking);
            }

            // 2. Sort loops along tube axis to identify End 1 (min Z) and End 2 (max Z)
            var loopWithZ = new List<Tuple<double, List<SampledEdge>>>();
            foreach (var loop in loops)
            {
                var pts = loop.SelectMany(e => e.SampledPoints).ToList();
                if (pts.Count == 0) continue;
                double meanZ = pts.Average(p => (p - center).Dot(axis));
                loopWithZ.Add(Tuple.Create(meanZ, loop));
            }

            if (loopWithZ.Count == 0)
            {
                return GenerateSquareCutTube(info, collarLengthMm, uClocking, vClocking);
            }

            loopWithZ.Sort((a, b) => a.Item1.CompareTo(b.Item1));

            TubeEndProfile p1;
            TubeEndProfile p2;

            if (loopWithZ.Count == 1)
            {
                // One end has a cope notch, the other end is straight square cut
                p1 = UnrollSingleLoop(
                    loopWithZ[0].Item2, axis, center, r, wall, uClocking, vClocking,
                    collarLengthMm, isEnd1: true, endIdx: 1);
                p2 = GenerateSquareCutProfile(r, wall, collarLengthMm, isEnd1: false, endIdx: 2, center, axis);
            }
            else
            {
                var end1Loop = loopWithZ[0].Item2;
                var end2Loop = loopWithZ[loopWithZ.Count - 1].Item2;

                p1 = UnrollSingleLoop(
                    end1Loop, axis, center, r, wall, uClocking, vClocking,
                    collarLengthMm, isEnd1: true, endIdx: 1);

                p2 = UnrollSingleLoop(
                    end2Loop, axis, center, r, wall, uClocking, vClocking,
                    collarLengthMm, isEnd1: false, endIdx: 2);
            }

            double totalLengthMm = 0.0;
            if (p1 != null && p2 != null)
            {
                totalLengthMm = p2.NodePoint3D.DistanceTo(p1.NodePoint3D);
            }
            if (totalLengthMm < 1.0)
            {
                totalLengthMm = Math.Abs(loopWithZ[loopWithZ.Count - 1].Item1 - loopWithZ[0].Item1);
                if (totalLengthMm < 1.0) totalLengthMm = 500.0; // Fallback sensible length
            }

            return new UnwrappedTube
            {
                TubeId = info.BodyName,
                TubeName = info.BodyName,
                TubeOdMm = od,
                TubeWallMm = wall,
                AxisVector = axis,
                AxisCenter = center,
                ReferenceClockingU = uClocking,
                ReferenceClockingV = vClocking,
                TotalTubeLengthMm = totalLengthMm,
                CenterToCenterLengthMm = Math.Max(0.0, totalLengthMm - (p1.CutDepthMm + p2.CutDepthMm) / 2.0),
                End1 = p1,
                End2 = p2,
                IsValid = true
            };
        }

        private static TubeEndProfile UnrollSingleLoop(
            List<SampledEdge> loop,
            Point3D axis,
            Point3D center,
            double radius,
            double wallThick,
            Point3D uClocking,
            Point3D vClocking,
            double collarLengthMm,
            bool isEnd1,
            int endIdx,
            int numSamples = DefaultGridSamples)
        {
            double circumference = 2.0 * Math.PI * radius;

            // 1. Order edges head-to-tail
            List<Tuple<SampledEdge, bool>> orderedEdges = OrderEdgesHeadToTail(loop);

            // 2. Sample dense 3D points along the continuous perimeter walk
            var loopPoints = new List<Point3D>();
            foreach (var item in orderedEdges)
            {
                SampledEdge e = item.Item1;
                bool isReversed = item.Item2;

                var pts = e.SampledPoints;
                if (isReversed)
                {
                    for (int i = pts.Count - 1; i >= 0; i--)
                        loopPoints.Add(pts[i]);
                }
                else
                {
                    for (int i = 0; i < pts.Count; i++)
                        loopPoints.Add(pts[i]);
                }
            }

            if (loopPoints.Count == 0)
            {
                return GenerateSquareCutProfile(radius, wallThick, collarLengthMm, isEnd1, endIdx, center, axis);
            }

            // 3. Compute (theta, z) relative to shared clocking vector
            var thetas = new List<double>();
            var zCoords = new List<double>();

            foreach (Point3D p in loopPoints)
            {
                double z = (p - center).Dot(axis);
                Point3D radial = (p - center) - (axis * z);

                double uComp = radial.Dot(uClocking);
                double vComp = radial.Dot(vClocking);
                double th = Math.Atan2(vComp, uComp);
                if (th < 0) th += 2.0 * Math.PI;

                thetas.Add(th);
                zCoords.Add(z);
            }

            // 4. Monotonic unwrapped angle along perimeter walk
            double[] unwrappedTh = UnwrapAngles(thetas);
            double[] zArr = zCoords.ToArray();

            if (unwrappedTh.Length > 1 && unwrappedTh[unwrappedTh.Length - 1] < unwrappedTh[0])
            {
                // Invert direction to ensure strictly monotonic ascending parameterization
                for (int i = 0; i < unwrappedTh.Length; i++)
                    unwrappedTh[i] = -unwrappedTh[i];
                Array.Reverse(unwrappedTh);
                Array.Reverse(zArr);
            }

            double thStart = unwrappedTh[0];
            double thEnd = unwrappedTh[unwrappedTh.Length - 1];

            // Ensure complete 2*pi loop coverage
            var unwrapList = new List<double>(unwrappedTh);
            var zList = new List<double>(zArr);
            if (thEnd < thStart + 2.0 * Math.PI)
            {
                unwrapList.Add(thStart + 2.0 * Math.PI);
                zList.Add(zArr[0]);
            }

            double[] finalTh = unwrapList.ToArray();
            double[] finalZ = zList.ToArray();

            // 5. Query equidistant physical angle grid [0, 2*pi] with 360 points
            var interpZ = new double[numSamples];
            double phiStep = (2.0 * Math.PI) / (numSamples - 1);

            for (int i = 0; i < numSamples; i++)
            {
                double phi = i * phiStep;
                double qTh = thStart + ((phi - (thStart % (2.0 * Math.PI)) + 2.0 * Math.PI) % (2.0 * Math.PI));
                interpZ[i] = InterpolateLinear(qTh, finalTh, finalZ);
            }

            double zMin = interpZ.Min();
            double zMax = interpZ.Max();
            double cutDepth = zMax - zMin;

            // 6. Map to collar baseline:
            // Straight cut baseline is at y = 0.
            // Notch profile rises above baseline at y >= collarLengthMm.
            var unwrappedCutPts = new List<Point2D>();
            Point3D nodePt;

            if (isEnd1)
            {
                double baseline = zMax + collarLengthMm;
                for (int i = 0; i < numSamples; i++)
                {
                    double x = (i * phiStep) * radius;
                    double y = baseline - interpZ[i];
                    unwrappedCutPts.Add(new Point2D(x, y));
                }
                nodePt = center + axis * zMin;
            }
            else
            {
                double baseline = zMin - collarLengthMm;
                for (int i = 0; i < numSamples; i++)
                {
                    double x = (i * phiStep) * radius;
                    double y = interpZ[i] - baseline;
                    unwrappedCutPts.Add(new Point2D(x, y));
                }
                nodePt = center + axis * zMax;
            }

            double templateH = cutDepth + collarLengthMm;

            return new TubeEndProfile
            {
                EndIndex = endIdx,
                TubeOdMm = radius * 2.0,
                TubeWallMm = wallThick,
                CollarLengthMm = collarLengthMm,
                UnwrappedCutPoints = unwrappedCutPts,
                CutDepthMm = cutDepth,
                TemplateWidthMm = circumference,
                TemplateHeightMm = templateH,
                CutAngleDeg = 90.0,
                NodePoint3D = nodePt
            };
        }

        private static List<Tuple<SampledEdge, bool>> OrderEdgesHeadToTail(List<SampledEdge> loop)
        {
            var ordered = new List<Tuple<SampledEdge, bool>>();
            if (loop == null || loop.Count == 0) return ordered;

            var unused = new List<SampledEdge>(loop);
            ordered.Add(Tuple.Create(unused[0], false));
            unused.RemoveAt(0);

            while (unused.Count > 0)
            {
                var lastItem = ordered[ordered.Count - 1];
                Point3D lastPt = lastItem.Item2 ? lastItem.Item1.StartPoint : lastItem.Item1.EndPoint;

                int bestIdx = -1;
                bool bestRev = false;
                double bestDist = double.MaxValue;

                for (int i = 0; i < unused.Count; i++)
                {
                    SampledEdge cand = unused[i];
                    double dStart = cand.StartPoint.DistanceTo(lastPt);
                    double dEnd = cand.EndPoint.DistanceTo(lastPt);

                    if (dStart < bestDist)
                    {
                        bestDist = dStart;
                        bestIdx = i;
                        bestRev = false;
                    }
                    if (dEnd < bestDist)
                    {
                        bestDist = dEnd;
                        bestIdx = i;
                        bestRev = true;
                    }
                }

                if (bestIdx >= 0 && bestDist < 5.0)
                {
                    ordered.Add(Tuple.Create(unused[bestIdx], bestRev));
                    unused.RemoveAt(bestIdx);
                }
                else
                {
                    // If tolerance gap, attach next available
                    ordered.Add(Tuple.Create(unused[0], false));
                    unused.RemoveAt(0);
                }
            }

            return ordered;
        }

        private static List<List<SampledEdge>> AssembleClosedLoops(List<SampledEdge> edges)
        {
            var loops = new List<List<SampledEdge>>();
            var remaining = new List<SampledEdge>(edges);

            while (remaining.Count > 0)
            {
                var currentLoop = new List<SampledEdge> { remaining[0] };
                remaining.RemoveAt(0);

                bool changed = true;
                while (changed)
                {
                    changed = false;
                    for (int i = 0; i < remaining.Count; i++)
                    {
                        SampledEdge cand = remaining[i];
                        bool connects = currentLoop.Any(le =>
                            le.StartPoint.DistanceTo(cand.StartPoint) < 1.0 ||
                            le.StartPoint.DistanceTo(cand.EndPoint) < 1.0 ||
                            le.EndPoint.DistanceTo(cand.StartPoint) < 1.0 ||
                            le.EndPoint.DistanceTo(cand.EndPoint) < 1.0);

                        if (connects)
                        {
                            currentLoop.Add(cand);
                            remaining.RemoveAt(i);
                            changed = true;
                            break;
                        }
                    }
                }
                loops.Add(currentLoop);
            }

            return loops;
        }

        private static double[] UnwrapAngles(List<double> angles)
        {
            if (angles.Count == 0) return new double[0];
            double[] result = new double[angles.Count];
            result[0] = angles[0];

            double cum = 0.0;
            for (int i = 1; i < angles.Count; i++)
            {
                double diff = angles[i] - angles[i - 1];
                if (diff > Math.PI)
                    cum -= 2.0 * Math.PI;
                else if (diff < -Math.PI)
                    cum += 2.0 * Math.PI;
                result[i] = angles[i] + cum;
            }

            return result;
        }

        private static double InterpolateLinear(double x, double[] xArr, double[] yArr)
        {
            if (xArr.Length == 0) return 0.0;
            if (xArr.Length == 1 || x <= xArr[0]) return yArr[0];
            if (x >= xArr[xArr.Length - 1]) return yArr[yArr.Length - 1];

            // Binary search for interval
            int low = 0, high = xArr.Length - 1;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (xArr[mid] < x)
                    low = mid + 1;
                else
                    high = mid - 1;
            }

            int idx1 = Math.Max(0, low - 1);
            int idx2 = Math.Min(xArr.Length - 1, low);

            if (idx1 == idx2) return yArr[idx1];

            double fraction = (x - xArr[idx1]) / (xArr[idx2] - xArr[idx1]);
            return yArr[idx1] + fraction * (yArr[idx2] - yArr[idx1]);
        }

        private static TubeEndProfile GenerateSquareCutProfile(
            double radius, double wall, double collar, bool isEnd1, int endIdx, Point3D center, Point3D axis)
        {
            double circumference = 2.0 * Math.PI * radius;
            var pts = new List<Point2D>();
            for (int i = 0; i < DefaultGridSamples; i++)
            {
                double x = (i / (double)(DefaultGridSamples - 1)) * circumference;
                pts.Add(new Point2D(x, collar));
            }

            return new TubeEndProfile
            {
                EndIndex = endIdx,
                TubeOdMm = radius * 2.0,
                TubeWallMm = wall,
                CollarLengthMm = collar,
                UnwrappedCutPoints = pts,
                CutDepthMm = 0.0,
                TemplateWidthMm = circumference,
                TemplateHeightMm = collar,
                CutAngleDeg = 90.0,
                NodePoint3D = center
            };
        }

        private static UnwrappedTube GenerateSquareCutTube(
            TubeCylinderInfo info, double collarMm, Point3D uClocking, Point3D vClocking)
        {
            var p1 = GenerateSquareCutProfile(info.OuterRadiusMm, info.WallThicknessMm, collarMm, true, 1, info.AxisCenter, info.AxisVector);
            var p2 = GenerateSquareCutProfile(info.OuterRadiusMm, info.WallThicknessMm, collarMm, false, 2, info.AxisCenter, info.AxisVector);

            return new UnwrappedTube
            {
                TubeId = info.BodyName,
                TubeName = info.BodyName,
                TubeOdMm = info.OuterDiameterMm,
                TubeWallMm = info.WallThicknessMm,
                AxisVector = info.AxisVector,
                AxisCenter = info.AxisCenter,
                ReferenceClockingU = uClocking,
                ReferenceClockingV = vClocking,
                TotalTubeLengthMm = 300.0, // Default nominal
                CenterToCenterLengthMm = 300.0,
                End1 = p1,
                End2 = p2,
                IsValid = true
            };
        }
    }
}
