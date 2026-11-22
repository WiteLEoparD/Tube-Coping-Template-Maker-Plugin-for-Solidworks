using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;
using BajaSAE.TubeCutTemplateAddin.Core.Models;
using BajaSAE.TubeCutTemplateAddin.Core.Geometry;
using BajaSAE.TubeCutTemplateAddin.Core.Collision;
using BajaSAE.TubeCutTemplateAddin.Core.Unroller;
using BajaSAE.TubeCutTemplateAddin.Core.BSAE;
using BajaSAE.TubeCutTemplateAddin.Core.ChassisDiagram;
using BajaSAE.TubeCutTemplateAddin.Export;

namespace BajaSAE.TubeCutTemplateAddin.UI
{
    /// <summary>
    /// Event handler for the SolidWorks PropertyManagerPage (PMP).
    /// Implements IPropertyManagerPage2Handler9 COM interface.
    /// </summary>
    [ComVisible(true)]
    [Guid("A4532B01-4D95-46D9-BA76-028A184D74BE")]
    public class TubeTemplatePMPHandler : IPropertyManagerPage2Handler9
    {
        private readonly ISldWorks _swApp;
        private readonly TubeTemplatePMPPage _page;

        public TubeTemplatePMPHandler(ISldWorks swApp, TubeTemplatePMPPage page)
        {
            _swApp = swApp;
            _page = page;
        }

        public void AfterActivation()
        {
            _page.OnAfterActivation();
        }

        public void OnClose(int reason)
        {
            if (reason == (int)swPropertyManagerPageCloseReasons_e.swPropertyManagerPageClose_Okay)
            {
                // User clicked the Green Checkmark OK
                _page.ExecuteGeneration();
            }
        }

        public void AfterClose()
        {
        }

        public bool OnHelp() => true;

        public bool OnPreviousPage() => true;

        public bool OnNextPage() => true;

        public bool OnPreview() => true;

        public void OnUndo() { }

        public void OnRedo() { }

        public bool OnTabClicked(int id) => true;

        public void OnGroupExpand(int id, bool expanded) { }

        public void OnGroupCheck(int id, bool @checked) { }

        public void OnCheckboxCheck(int id, bool @checked)
        {
            _page.OnCheckboxChanged(id, @checked);
        }

        public void OnOptionCheck(int id) { }

        public void OnButtonPress(int id)
        {
            _page.OnButtonPressed(id);
        }

        public void OnTextboxChanged(int id, string text) { }

        public void OnNumberboxChanged(int id, double value)
        {
            _page.OnNumberboxChanged(id, value);
        }

        public void OnComboboxEditChanged(int id, string text) { }

        public void OnComboboxSelectionChanged(int id, int item) { }

        public void OnListboxSelectionChanged(int id, int item) { }

        public void OnSelectionboxFocusChanged(int id) { }

        public void OnSelectionboxListChanged(int id, int count) { }

        public void OnSelectionboxCalloutCreated(int id) { }

        public void OnSelectionboxCalloutDestroyed(int id) { }

        public bool OnSubmitSelection(int id, object selection, int selType, ref string itemText)
        {
            // Allow solid bodies and weldment members
            return selType == (int)swSelectType_e.swSelSOLIDBODIES ||
                   selType == (int)swSelectType_e.swSelBODYFEATURES;
        }

        public int OnActiveXControlCreated(int id, bool status) => 0;

        public void OnSliderPositionChanged(int id, double value) { }

        public void OnSliderTrackingCompleted(int id, double value) { }

        public bool OnKeystroke(int id, int key, int crossKey, int modifier) => true;

        public void OnPopupMenuItem(int id) { }

        public void OnPopupMenuItemUpdate(int id, ref int retval) { }

        public void OnGageTableSelected(int id, int gage) { }

        public void OnWhatsNew() { }

        public void OnGainedFocus(int id) { }

        public void OnLostFocus(int id) { }

        public int OnWindowFromHandleControlCreated(int id, bool status) => 0;

        public void OnListboxRMBUp(int id, int posX, int posY) { }

        public void OnNumberBoxTrackingCompleted(int id, double value) { }
    }

    /// <summary>
    /// Constructs and manages the SolidWorks PropertyManagerPage UI for Tube Cut Templates.
    /// </summary>
    public class TubeTemplatePMPPage
    {
        private readonly ISldWorks _swApp;
        private IPropertyManagerPage2 _pmp;
        private TubeTemplatePMPHandler _handler;

        // Control IDs
        public const int GroupSelectionId = 1;
        public const int SelectionBoxId = 2;
        public const int ChkSelectAllTubesId = 3;

        public const int GroupSettingsId = 4;
        public const int NumCollarLengthId = 5;

        public const int GroupFormatsId = 6;
        public const int ChkPdfId = 7;
        public const int ChkDxfId = 8;
        public const int ChkSvgId = 9;

        public const int GroupOutputDirId = 10;
        public const int TxtOutputDirId = 11;
        public const int BtnBrowseDirId = 12;

        public const int GroupActionId = 13;
        public const int BtnGenerateId = 14;
        public const int LblStatusId = 15;

        // Controls
        private IPropertyManagerPageGroup _grpSelection;
        private IPropertyManagerPageSelectionbox _selBoxTubes;
        private IPropertyManagerPageCheckbox _chkSelectAllTubes;

        private IPropertyManagerPageGroup _grpSettings;
        private IPropertyManagerPageNumberbox _numCollarLength;

        private IPropertyManagerPageGroup _grpFormats;
        private IPropertyManagerPageCheckbox _chkPdf;
        private IPropertyManagerPageCheckbox _chkDxf;
        private IPropertyManagerPageCheckbox _chkSvg;

        private IPropertyManagerPageGroup _grpOutputDir;
        private IPropertyManagerPageTextbox _txtOutputDir;
        private IPropertyManagerPageButton _btnBrowseDir;

        private IPropertyManagerPageGroup _grpAction;
        private IPropertyManagerPageButton _btnGenerate;
        private IPropertyManagerPageLabel _lblStatus;

        public TemplateOptions Options { get; } = new TemplateOptions();

        public TubeTemplatePMPPage(ISldWorks swApp)
        {
            _swApp = swApp;
        }

        public void Show()
        {
            int errors = 0;
            _handler = new TubeTemplatePMPHandler(_swApp, this);

            int options = (int)swPropertyManagerPageOptions_e.swPropertyManagerOptions_OkayButton |
                          (int)swPropertyManagerPageOptions_e.swPropertyManagerOptions_CancelButton;

            _pmp = _swApp.CreatePropertyManagerPage(
                "Red Lion Racing — Tube Cut Templates (U of M)",
                options,
                _handler,
                ref errors
            ) as IPropertyManagerPage2;

            if (_pmp != null && errors == 0)
            {
                BuildControls();
                _pmp.Show();
            }
            else
            {
                MessageBox.Show("Failed to create SolidWorks PropertyManagerPage.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BuildControls()
        {
            int grpOptions = (int)swAddGroupBoxOptions_e.swGroupBoxOptions_Expanded |
                             (int)swAddGroupBoxOptions_e.swGroupBoxOptions_Visible;

            // 1. Tube Selection Group
            _grpSelection = _pmp.AddGroupBox(GroupSelectionId, "Chassis Tube Selection", grpOptions) as IPropertyManagerPageGroup;
            if (_grpSelection != null)
            {
                _selBoxTubes = _grpSelection.AddControl(
                    SelectionBoxId,
                    (short)swPropertyManagerPageControlType_e.swControlType_Selectionbox,
                    "Select Chassis Tubes",
                    (short)swPropertyManagerPageControlLeftAlign_e.swControlAlign_LeftEdge,
                    (int)swAddControlOptions_e.swControlOptions_Visible | (int)swAddControlOptions_e.swControlOptions_Enabled,
                    "Select cylindrical solid bodies or weldment structural members"
                ) as IPropertyManagerPageSelectionbox;

                if (_selBoxTubes != null)
                {
                    _selBoxTubes.SetSelectionFilters(new int[] {
                        (int)swSelectType_e.swSelSOLIDBODIES,
                        (int)swSelectType_e.swSelBODYFEATURES
                    });
                    _selBoxTubes.SingleEntityOnly = false;
                    _selBoxTubes.Height = 80;
                }

                _chkSelectAllTubes = _grpSelection.AddControl(
                    ChkSelectAllTubesId,
                    (short)swPropertyManagerPageControlType_e.swControlType_Checkbox,
                    "Select All Chassis Tubes (0.75\" - 1.75\" OD)",
                    (short)swPropertyManagerPageControlLeftAlign_e.swControlAlign_LeftEdge,
                    (int)swAddControlOptions_e.swControlOptions_Visible | (int)swAddControlOptions_e.swControlOptions_Enabled,
                    "Automatically scan model for all round chassis tubing"
                ) as IPropertyManagerPageCheckbox;
            }

            // 2. Collar Length Settings Group
            _grpSettings = _pmp.AddGroupBox(GroupSettingsId, "Paper-Saving Collar Settings", grpOptions) as IPropertyManagerPageGroup;
            if (_grpSettings != null)
            {
                _numCollarLength = _grpSettings.AddControl(
                    NumCollarLengthId,
                    (short)swPropertyManagerPageControlType_e.swControlType_Numberbox,
                    "Collar Length (in):",
                    (short)swPropertyManagerPageControlLeftAlign_e.swControlAlign_LeftEdge,
                    (int)swAddControlOptions_e.swControlOptions_Visible | (int)swAddControlOptions_e.swControlOptions_Enabled,
                    "Straight paper extension length beyond cut notch (default: 1.50\")"
                ) as IPropertyManagerPageNumberbox;

                if (_numCollarLength != null)
                {
                    _numCollarLength.SetRange2(
                        (int)swNumberboxUnitType_e.swNumberBox_UnitlessDouble,
                        0.25, 12.0, true, 0.25, 0.1, 0.5
                    );
                    _numCollarLength.Value = Options.CollarLengthInches;
                }
            }

            // 3. Output Formats Group
            _grpFormats = _pmp.AddGroupBox(GroupFormatsId, "Export Formats", grpOptions) as IPropertyManagerPageGroup;
            if (_grpFormats != null)
            {
                _chkPdf = _grpFormats.AddControl(
                    ChkPdfId,
                    (short)swPropertyManagerPageControlType_e.swControlType_Checkbox,
                    "PDF (US Letter 1:1 Scale Printable)",
                    (short)swPropertyManagerPageControlLeftAlign_e.swControlAlign_LeftEdge,
                    (int)swAddControlOptions_e.swControlOptions_Visible | (int)swAddControlOptions_e.swControlOptions_Enabled,
                    "True 1:1 scale US Letter PDF with calibration box"
                ) as IPropertyManagerPageCheckbox;
                if (_chkPdf != null) _chkPdf.Checked = Options.GeneratePdf;

                _chkDxf = _grpFormats.AddControl(
                    ChkDxfId,
                    (short)swPropertyManagerPageControlType_e.swControlType_Checkbox,
                    "DXF (AutoCAD / CNC Plasma & Laser)",
                    (short)swPropertyManagerPageControlLeftAlign_e.swControlAlign_LeftEdge,
                    (int)swAddControlOptions_e.swControlOptions_Visible | (int)swAddControlOptions_e.swControlOptions_Enabled,
                    "Layered 2D DXF vector paths"
                ) as IPropertyManagerPageCheckbox;
                if (_chkDxf != null) _chkDxf.Checked = Options.GenerateDxf;

                _chkSvg = _grpFormats.AddControl(
                    ChkSvgId,
                    (short)swPropertyManagerPageControlType_e.swControlType_Checkbox,
                    "SVG (Scalable Vector Graphics)",
                    (short)swPropertyManagerPageControlLeftAlign_e.swControlAlign_LeftEdge,
                    (int)swAddControlOptions_e.swControlOptions_Visible | (int)swAddControlOptions_e.swControlOptions_Enabled,
                    "Clean SVG with physical dimensions in inches"
                ) as IPropertyManagerPageCheckbox;
                if (_chkSvg != null) _chkSvg.Checked = Options.GenerateSvg;
            }

            // 4. Output Directory Group
            _grpOutputDir = _pmp.AddGroupBox(GroupOutputDirId, "Output Directory", grpOptions) as IPropertyManagerPageGroup;
            if (_grpOutputDir != null)
            {
                _txtOutputDir = _grpOutputDir.AddControl(
                    TxtOutputDirId,
                    (short)swPropertyManagerPageControlType_e.swControlType_Textbox,
                    "Save To:",
                    (short)swPropertyManagerPageControlLeftAlign_e.swControlAlign_LeftEdge,
                    (int)swAddControlOptions_e.swControlOptions_Visible | (int)swAddControlOptions_e.swControlOptions_Enabled,
                    "Folder where template files will be generated"
                ) as IPropertyManagerPageTextbox;

                string defaultDir = GetDefaultOutputDirectory();
                if (_txtOutputDir != null) _txtOutputDir.Text = defaultDir;
                Options.OutputDirectory = defaultDir;

                _btnBrowseDir = _grpOutputDir.AddControl(
                    BtnBrowseDirId,
                    (short)swPropertyManagerPageControlType_e.swControlType_Button,
                    "Browse...",
                    (short)swPropertyManagerPageControlLeftAlign_e.swControlAlign_LeftEdge,
                    (int)swAddControlOptions_e.swControlOptions_Visible | (int)swAddControlOptions_e.swControlOptions_Enabled,
                    "Select destination folder"
                ) as IPropertyManagerPageButton;
            }

            // 5. Execution & Live Diagnostic Status Group
            _grpAction = _pmp.AddGroupBox(GroupActionId, "Generate Templates", grpOptions) as IPropertyManagerPageGroup;
            if (_grpAction != null)
            {
                _btnGenerate = _grpAction.AddControl(
                    BtnGenerateId,
                    (short)swPropertyManagerPageControlType_e.swControlType_Button,
                    "Generate Cut Templates Now",
                    (short)swPropertyManagerPageControlLeftAlign_e.swControlAlign_LeftEdge,
                    (int)swAddControlOptions_e.swControlOptions_Visible | (int)swAddControlOptions_e.swControlOptions_Enabled,
                    "Extract coping profiles, check collisions, and generate 1:1 sheets"
                ) as IPropertyManagerPageButton;

                _lblStatus = _grpAction.AddControl(
                    LblStatusId,
                    (short)swPropertyManagerPageControlType_e.swControlType_Label,
                    "Ready",
                    (short)swPropertyManagerPageControlLeftAlign_e.swControlAlign_LeftEdge,
                    (int)swAddControlOptions_e.swControlOptions_Visible | (int)swAddControlOptions_e.swControlOptions_Enabled,
                    "Status and diagnostic log"
                ) as IPropertyManagerPageLabel;
            }
        }

        public void OnAfterActivation()
        {
            UpdateStatus("Select chassis tubes or check 'Select All Chassis Tubes'.");
        }

        public void OnCheckboxChanged(int id, bool @checked)
        {
            switch (id)
            {
                case ChkSelectAllTubesId:
                    Options.SelectAllChassisTubes = @checked;
                    if (@checked) AutoSelectAllChassisTubes();
                    break;
                case ChkPdfId:
                    Options.GeneratePdf = @checked;
                    break;
                case ChkDxfId:
                    Options.GenerateDxf = @checked;
                    break;
                case ChkSvgId:
                    Options.GenerateSvg = @checked;
                    break;
            }
        }

        public void OnNumberboxChanged(int id, double value)
        {
            if (id == NumCollarLengthId)
            {
                if (value > 0.0)
                {
                    Options.CollarLengthInches = value;
                }
            }
        }

        public void OnButtonPressed(int id)
        {
            if (id == BtnBrowseDirId)
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Select Output Directory for Baja SAE Tube Templates";
                    fbd.SelectedPath = Options.OutputDirectory;
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        Options.OutputDirectory = fbd.SelectedPath;
                        if (_txtOutputDir != null) _txtOutputDir.Text = fbd.SelectedPath;
                    }
                }
            }
            else if (id == BtnGenerateId)
            {
                ExecuteGeneration();
            }
        }

        private void AutoSelectAllChassisTubes()
        {
            IModelDoc2 modelDoc = _swApp.ActiveDoc as IModelDoc2;
            if (modelDoc == null) return;

            UpdateStatus("Scanning model for chassis tubing (0.75\" - 1.75\" OD)...");

            var bodies = GetAllSolidBodies(modelDoc);
            int selectedCount = 0;

            ISelectionMgr selMgr = modelDoc.SelectionManager as ISelectionMgr;
            ISelectData selData = selMgr.CreateSelectData();

            foreach (var body in bodies)
            {
                var cylInfo = TubeBodyAnalyzer.AnalyzeTubeBody(body, Options.MinOdInches * 25.4, Options.MaxOdInches * 25.4);
                if (cylInfo != null)
                {
                    body.Select2(true, null);
                    selectedCount++;
                }
            }

            UpdateStatus($"Selected {selectedCount} chassis tubes.");
        }

        public void ExecuteGeneration()
        {
            IModelDoc2 modelDoc = _swApp.ActiveDoc as IModelDoc2;
            if (modelDoc == null)
            {
                MessageBox.Show("Please open a SolidWorks Weldment Part or Assembly model first.", "No Active Model", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            UpdateStatus("Extracting selected tube bodies...");

            // 1. Collect selected bodies
            var selectedBodies = GetSelectedOrAllBodies(modelDoc);
            if (selectedBodies.Count == 0)
            {
                MessageBox.Show("No chassis tubes selected. Please select tube bodies or enable 'Select All Chassis Tubes'.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 2. Analyze bodies to detect cylindrical outer skin and OD
            var analyzedTubes = new List<TubeCylinderInfo>();
            foreach (var b in selectedBodies)
            {
                var info = TubeBodyAnalyzer.AnalyzeTubeBody(b, Options.MinOdInches * 25.4, Options.MaxOdInches * 25.4);
                if (info != null)
                {
                    analyzedTubes.Add(info);
                }
            }

            if (analyzedTubes.Count == 0)
            {
                MessageBox.Show("None of the selected bodies are round tubes between 0.75\" and 1.75\" OD.", "Invalid Bodies", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 3. Process All Tubes Individually (Zero Collision Skipping)
            // Each tube is unwrapped based on its own B-Rep geometry and existing cut edges,
            // regardless of any CAD overlaps or intersections with neighboring bodies.
            var validTubes = new List<TubeCylinderInfo>(analyzedTubes);
            var skippedTubes = new List<TubeCylinderInfo>();

            string outDir = string.IsNullOrEmpty(Options.OutputDirectory) ? GetDefaultOutputDirectory() : Options.OutputDirectory;
            Directory.CreateDirectory(outDir);

            var policyMgr = new BsaeRbfPolicyManager();
            var unwrappedList = new List<UnwrappedTube>();
            var report = new GenerationReport
            {
                TotalBodiesExamined = analyzedTubes.Count,
                CollisionDiagnostics = new List<CollisionDiagnostic>()
            };

            // 4. Process each valid tube
            for (int i = 0; i < validTubes.Count; i++)
            {
                var tubeInfo = validTubes[i];
                string tubeDisplayName = string.IsNullOrWhiteSpace(tubeInfo.BodyName) || 
                                         tubeInfo.BodyName.Equals("Tube", StringComparison.OrdinalIgnoreCase) || 
                                         tubeInfo.BodyName.StartsWith("TubeBody", StringComparison.OrdinalIgnoreCase)
                                         ? $"Tube_{i + 1}" : tubeInfo.BodyName;
                tubeInfo.BodyName = tubeDisplayName;

                UpdateStatus($"Processing Tube {i + 1} of {validTubes.Count}: {tubeDisplayName}...");
                System.Windows.Forms.Application.DoEvents();

                try
                {
                    var cutEdges = CutEdgeExtractor.ExtractCutEdges(tubeInfo);
                    var unwrapped = SpikeFreeUnroller.UnrollTube(tubeInfo, cutEdges, Options.CollarLengthMm);

                    if (unwrapped == null)
                    {
                        report.CollisionDiagnostics.Add(new CollisionDiagnostic
                        {
                            Tube1Name = tubeDisplayName,
                            Reason = "Could not unroll tube geometry (null profile returned)."
                        });
                        skippedTubes.Add(tubeInfo);
                        continue;
                    }

                    unwrapped.TubeId = tubeDisplayName;
                    unwrapped.TubeName = tubeDisplayName;

                    // Check notch depth validation
                    double maxCutDepth = Math.Max(unwrapped.End1?.CutDepthMm ?? 0, unwrapped.End2?.CutDepthMm ?? 0);
                    string depthWarning;
                    if (!CollisionEngine.ValidateNotchDepth(maxCutDepth, unwrapped.TubeOdMm, out depthWarning))
                    {
                        unwrapped.IsValid = false;
                        unwrapped.HasCollisionError = true;
                        unwrapped.WarningMessage = depthWarning;
                        report.CollisionDiagnostics.Add(new CollisionDiagnostic
                        {
                            Tube1Name = unwrapped.TubeId,
                            Reason = depthWarning
                        });
                        skippedTubes.Add(tubeInfo);
                        continue;
                    }

                    // BSAE 2027 Rev A Member & Node Classification
                    Point3D pt1 = unwrapped.End1?.NodePoint3D ?? Point3D.Zero;
                    Point3D pt2 = unwrapped.End2?.NodePoint3D ?? Point3D.Zero;
                    var classResult = policyMgr.ClassifyMember(pt1, pt2, unwrapped.TubeId);

                    unwrapped.BsaeMemberName = classResult.MemberName;
                    unwrapped.BsaeCategory = classResult.Category;
                    unwrapped.BsaeRuleRef = classResult.RuleRef;

                    if (unwrapped.End1 != null) unwrapped.End1.NodeName = classResult.StartNode;
                    if (unwrapped.End2 != null) unwrapped.End2.NodeName = classResult.EndNode;

                    // Export requested formats
                    string baseName = unwrapped.TubeId.Replace(" ", "_");

                    if (Options.GeneratePdf)
                    {
                        string pdfPath = Path.Combine(outDir, $"{baseName}_Letter_1to1.pdf");
                        PdfTemplateExporter.ExportPdf(unwrapped, pdfPath);
                        report.GeneratedPdfFiles.Add(pdfPath);
                    }

                    if (Options.GenerateDxf)
                    {
                        string dxfPath = Path.Combine(outDir, $"{baseName}_1to1.dxf");
                        DxfTemplateExporter.ExportDxf(unwrapped, dxfPath);
                        report.GeneratedDxfFiles.Add(dxfPath);
                    }

                    if (Options.GenerateSvg)
                    {
                        string svgPath = Path.Combine(outDir, $"{baseName}_Letter_1to1.svg");
                        SvgTemplateExporter.ExportSvg(unwrapped, svgPath);
                        report.GeneratedSvgFiles.Add(svgPath);
                    }

                    unwrappedList.Add(unwrapped);
                    report.TubesProcessed++;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ExecuteGeneration] Error processing tube {tubeInfo.BodyName}: {ex.Message}");
                    report.CollisionDiagnostics.Add(new CollisionDiagnostic
                    {
                        Tube1Name = tubeInfo.BodyName,
                        Reason = $"Geometric extraction error: {ex.Message}"
                    });
                    skippedTubes.Add(tubeInfo);
                }
            }

            report.TubesSkippedDueToCollision = skippedTubes.Count;

            // Display completion report
            ShowSummaryReport(report, outDir);
        }

        private void ShowSummaryReport(GenerationReport report, string outputDir)
        {
            string msg = $"Tube Cut Template Generation Complete!\n\n" +
                         $"• Total Tubes Processed: {report.TubesProcessed} of {report.TotalBodiesExamined}\n" +
                         $"• Generated PDF Sheets: {report.GeneratedPdfFiles.Count}\n" +
                         $"• Generated DXF Files: {report.GeneratedDxfFiles.Count}\n" +
                         $"• Generated SVG Files: {report.GeneratedSvgFiles.Count}\n\n";

            if (report.CollisionDiagnostics.Count > 0)
            {
                msg += "DIAGNOSTIC LOG:\n";
                foreach (var diag in report.CollisionDiagnostics)
                {
                    msg += $"• {diag.Tube1Name}: {diag.Reason}\n";
                }
                msg += "\n";
            }

            msg += $"Output Directory:\n{outputDir}\n\nWould you like to open the output folder now?";

            UpdateStatus($"Done! Generated {report.TubesProcessed} tube templates.");

            var dlgResult = MessageBox.Show(msg, "Red Lion Racing Tube Templates", MessageBoxButtons.YesNo,
                report.TubesSkippedDueToCollision > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

            if (dlgResult == DialogResult.Yes)
            {
                try { System.Diagnostics.Process.Start("explorer.exe", outputDir); } catch { }
            }
        }

        private void UpdateStatus(string text)
        {
            if (_lblStatus != null) _lblStatus.Caption = text;
        }

        private List<IBody2> GetSelectedOrAllBodies(IModelDoc2 modelDoc)
        {
            var result = new List<IBody2>();
            ISelectionMgr selMgr = modelDoc.SelectionManager as ISelectionMgr;
            int selCount = selMgr?.GetSelectedObjectCount2(-1) ?? 0;

            for (int i = 1; i <= selCount; i++)
            {
                object selObj = selMgr.GetSelectedObject6(i, -1);
                if (selObj is IBody2 b)
                {
                    result.Add(b);
                }
                else if (selObj is IFeature feat)
                {
                    object[] featBodies = feat.GetFaces() as object[];
                    // Or extract body from feature
                }
            }

            if (result.Count == 0 || Options.SelectAllChassisTubes)
            {
                return GetAllSolidBodies(modelDoc);
            }

            return result;
        }

        private List<IBody2> GetAllSolidBodies(IModelDoc2 modelDoc)
        {
            var list = new List<IBody2>();
            if (modelDoc is IPartDoc partDoc)
            {
                object[] bodies = partDoc.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
                if (bodies != null)
                {
                    foreach (var b in bodies)
                        if (b is IBody2 body) list.Add(body);
                }
            }
            else if (modelDoc is IAssemblyDoc assyDoc)
            {
                object[] comps = assyDoc.GetComponents(true) as object[];
                if (comps != null)
                {
                    foreach (var compObj in comps)
                    {
                        if (compObj is IComponent2 comp)
                        {
                            object[] compBodies = comp.GetBodies2((int)swBodyType_e.swSolidBody) as object[];
                            if (compBodies != null)
                            {
                                foreach (var b in compBodies)
                                    if (b is IBody2 body) list.Add(body);
                            }
                        }
                    }
                }
            }
            return list;
        }

        private string GetDefaultOutputDirectory()
        {
            IModelDoc2 modelDoc = _swApp?.ActiveDoc as IModelDoc2;
            string modelPath = modelDoc?.GetPathName();
            if (!string.IsNullOrEmpty(modelPath))
            {
                string dir = Path.GetDirectoryName(modelPath);
                return Path.Combine(dir, "Tube_Templates");
            }
            return Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "BSAE_Tube_Templates");
        }
    }
}
