using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksDxfBatchExporter.Standalone
{
    class Program
    {
        private const double MinNominalThicknessMeters = 0.00254; // 0.100" (2.54 mm)
        private const double MaxNominalThicknessMeters = 0.00381; // 0.150" (3.81 mm)

        static void Main(string[] args)
        {
            Console.Title = "SolidWorks 2026 - Batch 1/8\" Plate DXF Exporter";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("========================================================================");
            Console.WriteLine("  SolidWorks Batch 1/8\" Plate DXF Exporter (Standalone / 2026 Ready)");
            Console.WriteLine("========================================================================\n");
            Console.ResetColor();

            ISldWorks swApp = null;
            try
            {
                swApp = (ISldWorks)Marshal.GetActiveObject("SldWorks.Application");
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Could not connect to a running instance of SOLIDWORKS.");
                Console.WriteLine("Please open SOLIDWORKS and load your assembly, then run this tool again.");
                Console.ResetColor();
                WaitExit();
                return;
            }

            IModelDoc2 activeDoc = swApp.ActiveDoc as IModelDoc2;
            if (activeDoc == null || activeDoc.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[WARNING] Please open and activate an Assembly document (.sldasm) in SOLIDWORKS.");
                Console.ResetColor();
                WaitExit();
                return;
            }

            string asmPath = activeDoc.GetPathName();
            if (string.IsNullOrEmpty(asmPath))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[WARNING] The active assembly must be saved to disk first.");
                Console.ResetColor();
                WaitExit();
                return;
            }

            string asmDir = Path.GetDirectoryName(asmPath);
            string asmTitle = activeDoc.GetTitle();
            Console.WriteLine($"Active Assembly: {asmTitle}");
            Console.WriteLine($"Export Folder:   {asmDir}\n");

            // Freeze graphics updates
            IModelView activeView = activeDoc.ActiveView as IModelView;
            bool prevGraphicsUpdate = true;
            if (activeView != null)
            {
                prevGraphicsUpdate = activeView.EnableGraphicsUpdate;
                activeView.EnableGraphicsUpdate = false;
            }

            swApp.DocumentVisible(false, (int)swDocumentTypes_e.swDocPART);

            try
            {
                AssemblyDoc asmDoc = (AssemblyDoc)activeDoc;
                object[] components = asmDoc.GetComponents(false) as object[];
                Dictionary<string, int> manifest = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                if (components != null)
                {
                    foreach (object compObj in components)
                    {
                        if (compObj is Component2 comp)
                        {
                            try
                            {
                                if (!comp.IsSuppressed())
                                {
                                    string compPath = comp.GetPathName();
                                    if (!string.IsNullOrEmpty(compPath) && compPath.EndsWith(".sldprt", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (manifest.ContainsKey(compPath))
                                            manifest[compPath]++;
                                        else
                                            manifest[compPath] = 1;
                                    }
                                }
                            }
                            finally
                            {
                                Marshal.ReleaseComObject(comp);
                            }
                        }
                    }
                }

                if (manifest.Count == 0)
                {
                    Console.WriteLine("No unsuppressed .sldprt components found in the active assembly.");
                    WaitExit();
                    return;
                }

                Console.WriteLine($"Discovered {manifest.Count} unique part files across assembly hierarchy.\n");

                int exportedCount = 0;
                int skippedCount = 0;

                foreach (KeyValuePair<string, int> entry in manifest)
                {
                    string partPath = entry.Key;
                    int qty = entry.Value;
                    string partName = Path.GetFileNameWithoutExtension(partPath);

                    int openErrors = 0;
                    int openWarnings = 0;

                    IModelDoc2 partModel = swApp.OpenDoc6(
                        partPath,
                        (int)swDocumentTypes_e.swDocPART,
                        (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                        "",
                        ref openErrors,
                        ref openWarnings
                    ) as IModelDoc2;

                    if (partModel == null)
                    {
                        skippedCount++;
                        continue;
                    }

                    string partTitle = partModel.GetTitle();
                    PartDoc partDoc = partModel as PartDoc;

                    try
                    {
                        object[] bodies = partDoc.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
                        if (bodies == null || bodies.Length == 0)
                        {
                            skippedCount++;
                            continue;
                        }

                        IBody2 targetBody = null;
                        foreach (object bObj in bodies)
                        {
                            if (bObj is IBody2 body)
                            {
                                if (targetBody == null)
                                {
                                    double[] box = body.GetBodyBox() as double[];
                                    if (box != null && box.Length >= 6)
                                    {
                                        double dx = Math.Abs(box[3] - box[0]);
                                        double dy = Math.Abs(box[4] - box[1]);
                                        double dz = Math.Abs(box[5] - box[2]);

                                        double thickness = Math.Min(dx, Math.Min(dy, dz));
                                        if (thickness >= MinNominalThicknessMeters && thickness <= MaxNominalThicknessMeters)
                                        {
                                            targetBody = body;
                                            continue;
                                        }
                                    }
                                }
                                Marshal.ReleaseComObject(body);
                            }
                        }

                        if (targetBody == null)
                        {
                            skippedCount++;
                            continue;
                        }

                        IFace2 largestPlanarFace = null;
                        double maxArea = -1.0;
                        double[] faceNormal = null;

                        try
                        {
                            object[] faces = targetBody.GetFaces() as object[];
                            if (faces != null)
                            {
                                foreach (object fObj in faces)
                                {
                                    if (fObj is IFace2 face)
                                    {
                                        Surface surf = face.GetSurface() as Surface;
                                        bool isPlane = false;
                                        double[] pParams = null;

                                        if (surf != null)
                                        {
                                            isPlane = surf.IsPlane();
                                            if (isPlane)
                                                pParams = surf.PlaneParams as double[];
                                            Marshal.ReleaseComObject(surf);
                                        }

                                        if (isPlane)
                                        {
                                            double area = face.GetArea();
                                            if (area > maxArea)
                                            {
                                                maxArea = area;
                                                if (largestPlanarFace != null)
                                                    Marshal.ReleaseComObject(largestPlanarFace);
                                                largestPlanarFace = face;
                                                if (pParams != null && pParams.Length >= 6)
                                                    faceNormal = new double[] { pParams[3], pParams[4], pParams[5] };
                                                continue;
                                            }
                                        }
                                        Marshal.ReleaseComObject(face);
                                    }
                                }
                            }

                            if (largestPlanarFace == null)
                            {
                                skippedCount++;
                                continue;
                            }

                            int actErrors = 0;
                            IModelDoc2 activePartDoc = swApp.ActivateDoc3(
                                partTitle,
                                true,
                                (int)swRebuildOnActivation_e.swDontRebuildActiveDoc,
                                ref actErrors
                            ) as IModelDoc2 ?? partModel;

                            activePartDoc.ClearSelection2(true);

                            IEntity faceEntity = largestPlanarFace as IEntity;
                            bool isSelected = faceEntity != null && faceEntity.Select4(false, null);

                            if (isSelected)
                            {
                                double[] alignment = new double[12];
                                if (faceNormal != null && faceNormal.Length == 3)
                                {
                                    alignment[9] = faceNormal[0];
                                    alignment[10] = faceNormal[1];
                                    alignment[11] = faceNormal[2];
                                }

                                string dxfFileName = $"{partName}_QTY-[{qty}].dxf";
                                string dxfFilePath = Path.Combine(asmDir, dxfFileName);

                                bool success = ((PartDoc)activePartDoc).ExportToDWG2(
                                    dxfFilePath,
                                    partPath,
                                    (int)swExportToDWG_e.swExportToDWG_ExportSelectedFacesOrLoops,
                                    true,
                                    alignment,
                                    false,
                                    false,
                                    0,
                                    null
                                );

                                if (success)
                                {
                                    exportedCount++;
                                    Console.ForegroundColor = ConsoleColor.Green;
                                    Console.WriteLine($"  [EXPORTED] {dxfFileName}");
                                    Console.ResetColor();
                                }
                                else
                                {
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine($"  [FAILED] {partName}");
                                    Console.ResetColor();
                                }
                            }
                        }
                        finally
                        {
                            if (largestPlanarFace != null)
                                Marshal.ReleaseComObject(largestPlanarFace);
                            if (targetBody != null)
                                Marshal.ReleaseComObject(targetBody);
                        }
                    }
                    finally
                    {
                        if (partModel != null)
                            Marshal.ReleaseComObject(partModel);
                        swApp.CloseDoc(partTitle);
                    }
                }

                // Reactivate root assembly
                int asmActErr = 0;
                swApp.ActivateDoc3(asmTitle, true, (int)swRebuildOnActivation_e.swDontRebuildActiveDoc, ref asmActErr);

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n========================================================================");
                Console.WriteLine($" Batch Export Finished: {exportedCount} parts exported, {skippedCount} skipped.");
                Console.WriteLine($" DXFs saved to: {asmDir}");
                Console.WriteLine("========================================================================");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[ERROR] {ex.Message}");
                Console.ResetColor();
            }
            finally
            {
                swApp.DocumentVisible(true, (int)swDocumentTypes_e.swDocPART);
                if (activeView != null)
                {
                    activeView.EnableGraphicsUpdate = prevGraphicsUpdate;
                    Marshal.ReleaseComObject(activeView);
                }
            }

            WaitExit();
        }

        static void WaitExit()
        {
            Console.WriteLine("\nPress any key to exit...");
            try { Console.ReadKey(); } catch { }
        }
    }
}
