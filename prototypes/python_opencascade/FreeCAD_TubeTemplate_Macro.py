"""
FreeCAD_TubeTemplate_Macro.py
=============================
FreeCAD GUI & CLI Macro for Auto-Generating 1:1 Letter Tube Cut Templates.

Installation in FreeCAD:
1. Open FreeCAD.
2. Go to 'Macro' -> 'Macros...' -> 'User macros location'.
3. Copy this file into that folder (or open directly via Macro -> Execute Macro).
4. Run the macro to automatically process the currently active 3D chassis document
   or browse for an exported SolidWorks STEP / IGES file.
"""

import os
import sys

# Ensure current script folder is on sys.path
script_dir = os.path.dirname(os.path.abspath(__file__))
if script_dir not in sys.path:
    sys.path.insert(0, script_dir)

import FreeCAD
import FreeCADGui
import Part
from PySide6 import QtWidgets, QtCore, QtGui

from bsae_rbf_policy import BSAERBFPolicyManager
from opencascade_unroller import OpenCASCADEUnroller
from letter_sheet_generator import LetterSheetGenerator


class TubeTemplateDialog(QtWidgets.QDialog):
    """Interactive GUI Dialog in FreeCAD."""

    def __init__(self, parent=None):
        super().__init__(parent)
        self.setWindowTitle("BSAE 2027 Rev A - Tube Cut Template Generator")
        self.resize(550, 380)
        self.init_ui()

    def init_ui(self):
        layout = QtWidgets.QVBoxLayout(self)

        # Header Info
        header = QtWidgets.QLabel("<b>BSAE Rules 2027 Rev A - Rear Braced Frame (RBF)</b><br>"
                                 "Auto-generate 1:1 Letter Cut Templates from SolidWorks Models")
        header.setStyleSheet("font-size: 13px; color: #1A365D; margin-bottom: 8px;")
        layout.addWidget(header)

        # Input File Selection
        file_box = QtWidgets.QGroupBox("Input CAD Model (SolidWorks Export)")
        file_layout = QtWidgets.QHBoxLayout(file_box)
        self.file_edit = QtWidgets.QLineEdit()
        self.file_edit.setPlaceholderText("Select STEP / IGES file (or leave empty to use active document)...")
        file_layout.addWidget(self.file_edit)

        browse_btn = QtWidgets.QPushButton("Browse...")
        browse_btn.clicked.connect(self.browse_file)
        file_layout.addWidget(browse_btn)
        layout.addWidget(file_box)

        # Parameters
        param_box = QtWidgets.QGroupBox("Template Configuration")
        param_layout = QtWidgets.QGridLayout(param_box)

        param_layout.addWidget(QtWidgets.QLabel("Paper Size:"), 0, 0)
        paper_lbl = QtWidgets.QLabel("<b>US Letter (8.5\" x 11.0\") - 1:1 Scale</b>")
        param_layout.addWidget(paper_lbl, 0, 1)

        param_layout.addWidget(QtWidgets.QLabel("Paper-Saving Collar Length:"), 1, 0)
        self.collar_spin = QtWidgets.QDoubleSpinBox()
        self.collar_spin.setRange(0.5, 5.0)
        self.collar_spin.setValue(1.5)
        self.collar_spin.setSuffix(" inches")
        param_layout.addWidget(self.collar_spin, 1, 1)

        param_layout.addWidget(QtWidgets.QLabel("Output Directory:"), 2, 0)
        self.out_edit = QtWidgets.QLineEdit(os.path.join(script_dir, "output_templates"))
        param_layout.addWidget(self.out_edit, 2, 1)

        layout.addWidget(param_box)

        # Status / Log Box
        self.log_box = QtWidgets.QTextEdit()
        self.log_box.setReadOnly(True)
        self.log_box.setStyleSheet("background-color: #2D3748; color: #E2E8F0; font-family: Consolas, monospace; font-size: 11px;")
        layout.addWidget(self.log_box)

        # Buttons
        btn_layout = QtWidgets.QHBoxLayout()
        run_btn = QtWidgets.QPushButton("Generate 1:1 Templates (PDF/SVG/DXF)")
        run_btn.setStyleSheet("background-color: #2B6CB0; color: white; font-weight: bold; padding: 6px;")
        run_btn.clicked.connect(self.run_generation)
        btn_layout.addWidget(run_btn)

        close_btn = QtWidgets.QPushButton("Close")
        close_btn.clicked.connect(self.close)
        btn_layout.addWidget(close_btn)
        layout.addLayout(btn_layout)

    def browse_file(self):
        path, _ = QtWidgets.QFileDialog.getOpenFileName(
            self, "Select STEP / IGES file", script_dir, "CAD Files (*.step *.stp *.iges *.igs *.brep)"
        )
        if path:
            self.file_edit.setText(path)

    def log(self, text):
        self.log_box.append(text)
        FreeCAD.Console.PrintMessage(text + "\n")

    def run_generation(self):
        input_file = self.file_edit.text().strip()
        collar_in = self.collar_spin.value()
        out_dir = self.out_edit.text().strip()

        # If no file specified, try using active FreeCAD document
        if not input_file:
            doc = FreeCAD.ActiveDocument
            if not doc:
                self.log("Error: No active FreeCAD document and no file selected.")
                return
            # Export active doc shapes to temporary STEP file
            temp_step = os.path.join(script_dir, "_temp_active_doc.step")
            shapes = [obj.Shape for obj in doc.Objects if hasattr(obj, "Shape") and obj.Shape.Solids]
            if not shapes:
                self.log("Error: No solid bodies found in active document.")
                return
            compound = Part.makeCompound(shapes)
            compound.exportStep(temp_step)
            input_file = temp_step
            self.log(f"Exported {len(shapes)} solids from active document.")

        self.log(f"Processing: {input_file}")
        self.log(f"Collar length: {collar_in} inches")

        try:
            from auto_template_cli import process_cad_model
            results = process_cad_model(input_file, output_dir=out_dir, collar_in=collar_in)
            self.log(f"SUCCESS: Generated templates for {len(results)} tubes in {out_dir}")
            QtWidgets.QMessageBox.information(self, "Completed", f"1:1 Templates generated successfully!\nSaved to: {out_dir}")
        except Exception as e:
            self.log(f"ERROR: {str(e)}")
            QtWidgets.QMessageBox.critical(self, "Error", str(e))


def run_macro():
    dialog = TubeTemplateDialog(FreeCADGui.getMainWindow())
    dialog.show()


if __name__ == "__main__":
    run_macro()
