using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using BajaSAE.TubeCutTemplateAddin.Core.Models;
using BajaSAE.TubeCutTemplateAddin.Core.Geometry;

namespace BajaSAE.TubeCutTemplateAddin.Core.ChassisDiagram
{
    /// <summary>
    /// Deprecated: Chassis locator diagram generation has been disabled as requested.
    /// Retained as stub for binary backwards-compatibility.
    /// </summary>
    public static class ChassisLocatorRenderer
    {
        public static string RenderLocatorThumbnail(
            IModelDoc2 modelDoc,
            List<TubeCylinderInfo> allChassisTubes,
            TubeCylinderInfo activeTube,
            UnwrappedTube unwrappedTube,
            string outputDirectory)
        {
            return string.Empty;
        }

        public static void GenerateSyntheticChassisDiagram(
            List<TubeCylinderInfo> allTubes,
            TubeCylinderInfo activeTube,
            UnwrappedTube unwrappedTube,
            string outputPath)
        {
            // Disabled: No-op
        }
    }
}
