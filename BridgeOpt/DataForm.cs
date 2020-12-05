using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using GeneticSharp.Domain.Fitnesses;
using GeneticSharp.Domain.Populations;
using Color = System.Drawing.Color;
using Form = System.Windows.Forms.Form;

namespace BridgeOpt
{
    [Transaction(TransactionMode.Manual)]
    public partial class DataForm : Form
    {
        public PhysicalBridge PhysicalBridge;
        private QuantitiesAndSchedules QuantitiesAndSchedules;

        private readonly Random rnd = new Random();

        public DataForm()
        {
            InitializeComponent();
        }

        private void TryParse(bool formatStrings = false)
        {
            bool succeeded = true;
            succeeded &= IsDouble(DataFormSpanLength1, formatStrings, 2);
            succeeded &= IsDouble(DataFormSpanLength2, formatStrings, 2);
            succeeded &= IsDouble(DataFormRoadThickness, formatStrings, 3);
            succeeded &= IsDouble(DataFormLearnRate, formatStrings, 4);
            succeeded &= IsDouble(DataFormErrorTreshold, formatStrings, 4);
            succeeded &= IsDouble(DataFormPrestressDisc, formatStrings, 4);
            succeeded &= IsDouble(DataFormCrossoverProbability, formatStrings, 3);
            succeeded &= IsDouble(DataFormCrossoverDistributionIndex, formatStrings, 3);
            succeeded &= IsDouble(DataFormMutationProbability, formatStrings, 3);
            succeeded &= IsDouble(DataFormMutationDistributionIndex, formatStrings, 2);
            succeeded &= IsDouble(DataFormTendonCrossoverProbability, formatStrings, 3);
            succeeded &= IsDouble(DataFormTendonCrossoverDistributionIndex, formatStrings, 3);
            succeeded &= IsDouble(DataFormTendonMutationProbability, formatStrings, 3);
            succeeded &= IsDouble(DataFormTendonMutationDistributionIndex, formatStrings, 2);

            succeeded &= IsInteger(DataFormInitGeneration);
            DataFormLoadDeployment.Enabled = IsInteger(DataFormInitGeneration);
            DataFormRegenerateCases.Enabled = IsInteger(DataFormInitGeneration) && DataFormDeployment.Items.Count > 0;
            succeeded &= IsInteger(DataFormSpanDivision);
            succeeded &= IsInteger(DataFormEnvelopeStep);
            succeeded &= IsInteger(DataFormTrainingSetSize);
            succeeded &= IsInteger(DataFormTestSetSize);
            succeeded &= IsInteger(DataFormMaxEpochs);
            succeeded &= IsInteger(DataFormHiddenUnits);
            succeeded &= IsInteger(DataFormMinAnchorageZ);
            succeeded &= IsInteger(DataFormMaxAnchorageZ1);
            succeeded &= IsInteger(DataFormMinSpanX);
            succeeded &= IsInteger(DataFormMaxSpanX);
            succeeded &= IsInteger(DataFormMinSpanZ);
            succeeded &= IsInteger(DataFormMaxSpanZ1);
            succeeded &= IsInteger(DataFormMinSupportZ1);
            succeeded &= IsInteger(DataFormMaxSupportZ);
            succeeded &= IsInteger(DataFormMinSupportRadius);
            succeeded &= IsInteger(DataFormMaxSupportRadius);
            succeeded &= IsInteger(DataFormPopulationSize);
            succeeded &= IsInteger(DataFormMaxGenerations);
            succeeded &= IsInteger(DataFormTendonPopulationSize);
            succeeded &= IsInteger(DataFormTendonMaxGenerations);
            succeeded &= IsInteger(DataFormTendonGeneticAlgorithmRuns);

            if (DataFormMaxAnchorageZCheckBox.Checked) succeeded &= IsInteger(DataFormMaxAnchorageZ2);
            else DataFormMaxAnchorageZ2.BackColor = SystemColors.Window;

            if (DataFormMaxSpanZCheckBox.Checked) succeeded &= IsInteger(DataFormMaxSpanZ2);
            else DataFormMaxSpanZ2.BackColor = SystemColors.Window;

            if (DataFormMinSupportZCheckBox.Checked) succeeded &= IsDouble(DataFormMinSupportZ2, formatStrings, 3);
            else DataFormMinSupportZ2.BackColor = SystemColors.Window;

            double doubleParser;
            if (DataFormUniformPlanarLoads.Rows.Count > 1)
            {
                foreach (DataGridViewRow row in DataFormUniformPlanarLoads.Rows)
                {
                    if (DataFormUniformPlanarLoads.Rows.IndexOf(row) == DataFormUniformPlanarLoads.Rows.Count - 1) break;
                    for (int i = 0; i < row.Cells.Count - 1; i++)
                    {
                        if (double.TryParse((string) row.Cells[i].Value, out doubleParser) == false) succeeded = false;
                        else if (formatStrings) row.Cells[i].Value = string.Format("{0:0.000}", doubleParser);
                    }
                }
            }
            if (DataFormUniformLinearLoads.Rows.Count > 1)
            {
                foreach (DataGridViewRow row in DataFormUniformLinearLoads.Rows)
                {
                    if (DataFormUniformLinearLoads.Rows.IndexOf(row) == DataFormUniformLinearLoads.Rows.Count - 1) break;
                    for (int i = 0; i < row.Cells.Count - 1; i++)
                    {
                        if (double.TryParse((string) row.Cells[i].Value, out doubleParser) == false) succeeded = false;
                        else if (formatStrings) row.Cells[i].Value = string.Format("{0:0.000}", doubleParser);
                    }
                }
            }

            if (DataFormSchedules.Rows.Count > 1)
            {
                foreach (DataGridViewRow row in DataFormSchedules.Rows)
                {
                    if (DataFormSchedules.Rows.IndexOf(row) == DataFormSchedules.Rows.Count - 1) break;

                    if (double.TryParse((string) row.Cells[1].Value, out doubleParser) == false) succeeded = false;
                    else if (formatStrings) row.Cells[1].Value = string.Format("{0:0.0}", doubleParser);
                }
            }

            if (DataFormLeftFootway.Checked)
            {
                succeeded &= IsDouble(DataFormLeftFootwayStart, formatStrings, 3);
                succeeded &= IsDouble(DataFormLeftFootwayEnd, formatStrings, 3);
            }
            else
            {
                DataFormLeftFootwayStart.BackColor = SystemColors.Window;
                DataFormLeftFootwayEnd.BackColor = SystemColors.Window;
            }

            if (DataFormRightFootway.Checked)
            {
                succeeded &= IsDouble(DataFormRightFootwayStart, formatStrings, 3);
                succeeded &= IsDouble(DataFormRightFootwayEnd, formatStrings, 3);
            }
            else
            {
                DataFormRightFootwayStart.BackColor = SystemColors.Window;
                DataFormRightFootwayEnd.BackColor = SystemColors.Window;
            }

            if (DataFormCarriageway.Checked)
            {
                succeeded &= IsDouble(DataFormCarriagewayStart, formatStrings, 3);
                succeeded &= IsDouble(DataFormCarriagewayEnd, formatStrings, 3);
            }
            else
            {
                DataFormCarriagewayStart.BackColor = SystemColors.Window;
                DataFormCarriagewayEnd.BackColor = SystemColors.Window;
            }
            ApplyButton.Enabled = succeeded;
        }

        private bool IsInteger(System.Windows.Forms.TextBox textBox)
        {
            if (int.TryParse(textBox.Text, out int intParser) == false)
            {
                textBox.BackColor = Color.Red;
                return false;
            }
            
            textBox.BackColor = SystemColors.Window;
            return true;
        }
        private bool IsDouble(System.Windows.Forms.TextBox textBox, bool formatStrings, int digits = 3)
        {
            if (double.TryParse(textBox.Text, out double doubleParser) == false)
            {
                textBox.BackColor = Color.Red;
                return false;
            }

            textBox.BackColor = SystemColors.Window;
            if (formatStrings)
            {
                string formatString;
                if (digits == 0) formatString = "{0:0}";
                else
                {
                    formatString = "{0:0.";
                    for (int i = 0; i < digits; i++) formatString += "0";
                    formatString += "}";
                }
                textBox.Text = string.Format(formatString, doubleParser);
            }
            return true;
        }

        private void DataFormSpanLength1_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormSpanLength1_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormSpanLength2_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormSpanLength2_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormSpanDivision_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormSpanDivision_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormTrainingSetSize_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormTrainingSetSize_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormUniformPlanarLoads_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            TryParse(true);
        }
        private void DataFormUniformLinearLoads_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            TryParse(true);
        }

        private void DataFormRoadThickness_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormRoadThickness_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormLeftFootwayStart_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormLeftFootwayStart_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormLeftFootwayEnd_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormLeftFootwayEnd_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormRightFootwayStart_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormRightFootwayStart_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormRightFootwayEnd_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormRightFootwayEnd_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormCarriagewayStart_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormCarriagewayStart_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormCarriagewayEnd_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormCarriagewayEnd_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormInitGeneration_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormInitGeneration_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormEnvelopeStep_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormEnvelopeStep_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormTestSetSize_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormTestSetSize_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormMaxEpochs_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormMaxEpochs_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormHiddenUnits_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormHiddenUnits_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormLearnRate_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormLearnRate_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormErrorTreshold_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormErrorTreshold_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormPrestressDisc_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormPrestressDisc_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormMinAnchorageZ_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormMinAnchorageZ_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormMaxAnchorageZ1_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormMaxAnchorageZ1_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormMaxAnchorageZ2_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormMaxAnchorageZ2_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormMinSpanX_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormMinSpanX_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormMaxSpanX_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormMaxSpanX_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormMinSpanZ_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormMinSpanZ_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormMaxSpanZ1_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormMaxSpanZ1_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormMaxSpanZ2_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormMaxSpanZ2_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormMinSupportZ1_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormMinSupportZ1_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormMinSupportZ2_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormMinSupportZ2_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormMaxSupportZ_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormMaxSupportZ_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormMinSupportRadius_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormMinSupportRadius_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormMaxSupportRadius_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormMaxSupportRadius_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormPopulationSize_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormPopulationSize_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormCrossoverProbability_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormCrossoverProbability_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormCrossoverDistributionIndex_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormCrossoverDistributionIndex_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormMutationProbability_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormMutationProbability_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormMutationDistributionIndex_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormMutationDistributionIndex_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormMaxGenerations_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormMaxGenerations_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormTendonPopulationSize_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormTendonPopulationSize_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormTendonCrossoverProbability_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormTendonCrossoverProbability_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormTendonCrossoverDistributionIndex_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormTendonCrossoverDistributionIndex_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormTendonMutationProbability_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormTendonMutationProbability_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormTendonMutationDistributionIndex_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormTendonMutationDistributionIndex_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormTendonMaxGenerations_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormTendonMaxGenerations_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormTendonGeneticAlgorithmRuns_TextChanged(object sender, EventArgs e)
        {
            TryParse(false);
        }
        private void DataFormTendonGeneticAlgorithmRuns_Leave(object sender, EventArgs e)
        {
            TryParse(true);
        }

        private void DataFormSchedules_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            TryParse(true);
        }

        private void ApplyButton_Click(object sender, EventArgs e)
        {
            Stopwatch mainStopWatch = new Stopwatch();
            if (DataFormDeployment.SelectedIndices.Count == 0)
            {
                if (DataFormResolveCurrentModel.Checked)
                {
                    mainStopWatch.Start();
                    string name = PhysicalBridge.CommandData.Application.ActiveUIDocument.Document.Title;
                    string dir = DataFormDir.Text + Path.DirectorySeparatorChar + name + Path.DirectorySeparatorChar;

                    OptimizationCase opt = new OptimizationCase(PhysicalBridge);
                    opt.Resolve(PhysicalBridge, name, dir);
                }
                else
                {
                    MessageBox.Show("No cases selected in the deployment window.", "No selected cases", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            ApplyButton.Enabled = false; CancelButton.Enabled = false;
            if (DataFormDeployment.SelectedIndices.Count > 0)
            {
                mainStopWatch.Start();
                foreach (int index in DataFormDeployment.SelectedIndices)
                {
                    string name = Globals.ModelName(int.Parse(DataFormInitGeneration.Text), index + 1);
                    string dir = Globals.ModelDir(DataFormDir.Text, int.Parse(DataFormInitGeneration.Text), index + 1, true);

                    OptimizationCase opt = new OptimizationCase(DataFormDeployment.Items[index] as string);
                    opt.Resolve(PhysicalBridge, name, dir);
                }
            }
            Close();

            mainStopWatch.Stop();
            TaskDialog.Show("Deployment Resolved", "Selected cases calculated.\nTotal time: " + mainStopWatch.Elapsed.ToString());
        }
        private void CancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void DataForm_Shown(object sender, EventArgs e)
        {
            PhysicalBridge.DataForm = this;

            ElementsLabel.Text = "Number of elements: " + PhysicalBridge.Elements.Count();
            SuperstructureID.Text = "Superstructure ID: " + PhysicalBridge.Superstructure.Id;

            double totalWidth = Math.Max(Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("O1").AsDouble()), Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("O5").AsDouble())) - Math.Min(Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("O1").AsDouble()), Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("O5").AsDouble()));
            DataFormTotalWidth.Text = string.Format("{0:0.000}", totalWidth);

            QuantitiesAndSchedules = new QuantitiesAndSchedules(PhysicalBridge);
            QuantitiesAndSchedules.LoadSchedules(true);

            //Filling uniform planar load grid with default data:
            DataFormUniformPlanarLoads.Rows.Add("5.925", "0.000", "1.090", "Zabudowa chodnikowa, lewa");
            DataFormUniformPlanarLoads.Rows.Add("2.300", "1.090", "8.090", "Jezdnia");
            DataFormUniformPlanarLoads.Rows.Add("6.038", "8.090", "10.590", "Zabudowa chodnikowa, lewa");
            //Filling uniform linrar load grid with default data:
            DataFormUniformLinearLoads.Rows.Add("0.600", "0.000", "Deska gzymsowa, lewa");
            DataFormUniformLinearLoads.Rows.Add("1.000", "0.320", "Barieroporęcz");
            DataFormUniformLinearLoads.Rows.Add("0.500", "1.340", "Kolektor, lewy");
            DataFormUniformLinearLoads.Rows.Add("0.500", "7.840", "Kolektor, prawy");
            DataFormUniformLinearLoads.Rows.Add("1.000", "8.860", "Bariera energochłonna");
            DataFormUniformLinearLoads.Rows.Add("1.000", "10.490", "Balustrada");
            DataFormUniformLinearLoads.Rows.Add("0.600", "10.590", "Deska gzymsowa, prawa");

            DataFormDir.Text = DataFormDir.Text.TrimEnd(Path.DirectorySeparatorChar);
            DataFormSeedDir.Text = DataFormSeedDir.Text.TrimEnd(Path.DirectorySeparatorChar);

            int? recentGenNumber = GetRecentGenerationNumber(DataFormDir.Text);
            if (recentGenNumber == null) recentGenNumber = 0;
            DataFormRecentGeneration.Text = "Most Recent Generation: " + (int) recentGenNumber;
            DataFormInitGeneration.Text = ((int) recentGenNumber).ToString();

            TryParse();
        }

        private void DataFormDeleteUniformPlanarLoadButton_Click(object sender, EventArgs e)
        {
            if (DataFormUniformPlanarLoads.SelectedRows.Count > 0)
            {
                foreach (DataGridViewRow row in DataFormUniformPlanarLoads.SelectedRows)
                {
                    try { DataFormUniformPlanarLoads.Rows.RemoveAt(row.Index); }
                    catch { }
                }
            }
            TryParse(true);
        }

        private void DataFormDeleteUniformLinearLoadButton_Click(object sender, EventArgs e)
        {
            if (DataFormUniformLinearLoads.SelectedRows.Count > 0)
            {
                foreach (DataGridViewRow row in DataFormUniformLinearLoads.SelectedRows)
                {
                    try { DataFormUniformLinearLoads.Rows.RemoveAt(row.Index); }
                    catch { }
                }
            }
            TryParse(true);
        }

        private void DataFormLeftFootway_CheckedChanged(object sender, EventArgs e)
        {
            if (DataFormLeftFootway.Checked)
            {
                DataFormLeftFootwayStart.Text = "0.000";
                DataFormLeftFootwayStart.Enabled = true;

                DataFormLeftFootwayEnd.Text = "0.000";
                DataFormLeftFootwayEnd.Enabled = true;
            }
            else
            {
                DataFormLeftFootwayStart.Enabled = false;
                DataFormLeftFootwayStart.Clear();

                DataFormLeftFootwayEnd.Enabled = false;
                DataFormLeftFootwayEnd.Clear();
            }
            TryParse();
        }

        private void DataFormRightFootway_CheckedChanged(object sender, EventArgs e)
        {
            if (DataFormRightFootway.Checked)
            {
                DataFormRightFootwayStart.Text = "0.000";
                DataFormRightFootwayStart.Enabled = true;

                DataFormRightFootwayEnd.Text = "0.000";
                DataFormRightFootwayEnd.Enabled = true;
            }
            else
            {
                DataFormRightFootwayStart.Enabled = false;
                DataFormRightFootwayStart.Clear();

                DataFormRightFootwayEnd.Enabled = false;
                DataFormRightFootwayEnd.Clear();
            }
            TryParse();
        }

        private void DataFormCarriage_CheckedChanged(object sender, EventArgs e)
        {
            if (DataFormCarriageway.Checked)
            {
                DataFormCarriagewayStart.Text = "0.000";
                DataFormCarriagewayStart.Enabled = true;

                DataFormCarriagewayEnd.Text = "0.000";
                DataFormCarriagewayEnd.Enabled = true;
            }
            else
            {
                DataFormCarriagewayStart.Enabled = false;
                DataFormCarriagewayStart.Clear();

                DataFormCarriagewayEnd.Enabled = false;
                DataFormCarriagewayEnd.Clear();
            }
            TryParse();
        }

        private bool IsValidGenerationDirectory(string dir)
        {
            try { dir = Path.GetFileName(@dir); }
            catch { return false; }
            if (dir.StartsWith("G"))
            {
                dir = dir.Remove(0, 1);
                return int.TryParse(dir, out int intParser);
            }
            return false;
        }
        private int? GetGenerationNumber(string dir)
        {
            try { dir = Path.GetFileName(@dir); }
            catch { return null; }
            if (dir.StartsWith("G"))
            {
                dir = dir.Remove(0, 1);
                if (int.TryParse(dir, out int intParser)) return intParser;
                return null;
            }
            return null;
        }
        private int? GetRecentGenerationNumber(string genDir)
        {
            if (Directory.Exists(@genDir) == false) return null;
            string[] genDirectories = Directory.GetDirectories(@genDir);

            int recentGenNumber = 0; bool found = false;
            foreach (string genDirectory in genDirectories)
            {
                if (IsValidGenerationDirectory(genDirectory))
                {
                    found = true;
                    recentGenNumber = Math.Max(recentGenNumber, (int) GetGenerationNumber(genDirectory));
                }
            }
            if (found == false) return null;
            return recentGenNumber;
        }

        private void DataFormLoadDeployment_Click(object sender, EventArgs e)
        {
            if (Directory.Exists(@DataFormDir.Text) == false)
            {
                MessageBox.Show("The specified generations directory does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            int? recentGenNumber = GetRecentGenerationNumber(DataFormDir.Text);
            int currentGenNumber = 0;
            
            bool passing = false;
            if (recentGenNumber == null)
            {
                DialogResult reply = MessageBox.Show("No valid generations found in the repository. Do you want to create initial generation including initial deployment?", "No generations", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (reply == DialogResult.Yes)
                {
                    Directory.CreateDirectory(Globals.GenerationDir(DataFormDir.Text, currentGenNumber));
                    passing = true;
                }
                else return;
            }
            else
            {
                if (int.TryParse(DataFormInitGeneration.Text, out currentGenNumber))
                {
                    if (currentGenNumber > recentGenNumber)
                    {
                        DialogResult reply = MessageBox.Show("Specified initial generation number is greater than most recent one. Do you want to get deploy cases basing on the most recent generation?", "New generation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (reply != DialogResult.Yes) return;
                    }
                }
                else MessageBox.Show("Invalid initial generation number specified.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            string inputPath = Globals.GenerationDir(DataFormDir.Text, currentGenNumber) + Globals.Input;
            if (File.Exists(inputPath))
            {
                var lines = File.ReadAllLines(inputPath);
                DataFormDeployment.Items.Clear();
                DataFormDeployment.Items.AddRange(lines);
                MessageBox.Show("Select items to calculate.", "Data loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                if (currentGenNumber == 0)
                {
                    DialogResult reply;
                    if (passing == false) reply = MessageBox.Show("No deployment created for the first generation. New random population will be generated. Continue?", "Initial generation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    else reply = DialogResult.Yes;

                    if (reply == DialogResult.Yes)
                    {
                        DataFormDeployment.Items.Clear();
                        for (int i = 0; i < int.Parse(DataFormPopulationSize.Text); i++)
                        {
                            OptimizationCase opt = new OptimizationCase(PhysicalBridge, new OptimizationRules(Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("O1").AsDouble()), Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("O5").AsDouble())), rnd);
                            DataFormDeployment.Items.Add(opt.EncodeCase(i + 1));
                        }
                        if (Directory.Exists(Globals.GenerationDir(DataFormDir.Text, currentGenNumber)) == false) Directory.CreateDirectory(Globals.GenerationDir(DataFormDir.Text, currentGenNumber));

                        using (StreamWriter saveFile = new StreamWriter(inputPath))
                        {
                            foreach (var item in DataFormDeployment.Items) saveFile.WriteLine(item.ToString());
                        }
                    }
                    else return;
                }
                else
                {
                    if (Directory.Exists(Globals.GenerationDir(DataFormDir.Text, (int) recentGenNumber)) == false)
                    {
                        MessageBox.Show("No previous generation existing.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    string previousInputPath = Globals.GenerationDir(DataFormDir.Text, (int) recentGenNumber) + Globals.Input;
                    string previousOutputPath = Globals.GenerationDir(DataFormDir.Text, (int) recentGenNumber) + Globals.Output;
                    if (File.Exists(previousInputPath) == false)
                    {
                        MessageBox.Show("Invalid repository structure for the previous generation.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    var lines = File.ReadAllLines(previousInputPath);
                    DataFormDeployment.Items.Clear();
                    DataFormDeployment.Items.AddRange(lines);

                    List<OptimizationCase> opts = new List<OptimizationCase>();
                    List<double> fitness = new List<double>();
                    for (int index = 0; index < DataFormDeployment.Items.Count; index++)
                    {
                        string name = Globals.ModelName((int) recentGenNumber, index + 1);
                        string dir = Globals.ModelDir(DataFormDir.Text, (int) recentGenNumber, index + 1, true);

                        if (Directory.Exists(dir) == false)
                        {
                            MessageBox.Show("Directory " + name + " does not exist in generation no. " + ((int) recentGenNumber) + ".", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        if (File.Exists(dir + Globals.TextFiles.CostSummary) == false)
                        {
                            MessageBox.Show("No valid output file for case " + name + ".", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        opts.Add(new OptimizationCase(DataFormDeployment.Items[index] as string));
                        var caseOutputFile = File.ReadAllLines(dir + Globals.TextFiles.CostSummary); double f = 0.0;
                        foreach (string line in caseOutputFile)
                        {
                            if (line.Contains("Total cost"))
                            {
                                if (double.TryParse(line.Remove(0, line.IndexOf("\t") + 1), out f) == false) f = 0.0;
                                break;
                            }
                        }
                        if (f == 0.0)
                        {
                            MessageBox.Show("Invalid output file for case " + name + ".", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        fitness.Add(f);
                    }
                    using (StreamWriter previousOutputFile = new StreamWriter(previousOutputPath, false))
                    {
                        for (int i = 0; i < opts.Count(); i++)
                        {
                            previousOutputFile.WriteLine(opts[i].EncodeCase(i + 1) + string.Format("\tCost: {0:0.0}", fitness[i]));
                        }
                    }
                    if (Directory.Exists(Globals.GenerationDir(DataFormDir.Text, currentGenNumber)) == false) Directory.CreateDirectory(Globals.GenerationDir(DataFormDir.Text, currentGenNumber));

                    string auxInputPath = Globals.GenerationDir(DataFormDir.Text, currentGenNumber) + Globals.AuxiliaryInput;
                    List<OptimizationCase> auxGenerationOpts = ResolveNewGeneration(opts, fitness);
                    using (StreamWriter saveFile = new StreamWriter(auxInputPath))
                    {
                        foreach (OptimizationCase opt in auxGenerationOpts) saveFile.WriteLine(opt.EncodeCase(auxGenerationOpts.IndexOf(opt) + 1));
                    }

                    List<OptimizationCase> newGenerationOpts = ResolveNewGeneration(opts, fitness);
                    DataFormDeployment.Items.Clear();
                    for (int i = 0; i < newGenerationOpts.Count(); i++) DataFormDeployment.Items.Add(newGenerationOpts[i].EncodeCase(i + 1));

                    using (StreamWriter saveFile = new StreamWriter(inputPath))
                    {
                        foreach (var item in DataFormDeployment.Items) saveFile.WriteLine(item.ToString());
                    }
                }
            }
        }

        private void DataFormLoadIntoModel_Click(object sender, EventArgs e)
        {
            if (DataFormDeployment.SelectedIndex == -1) return;
            OptimizationCase opt = new OptimizationCase(DataFormDeployment.Items[DataFormDeployment.SelectedIndex].ToString());
            opt.RefreshSpan(PhysicalBridge);
        }

        private void DataFormMaxAnchorageZCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (DataFormMaxAnchorageZCheckBox.Checked)
            {
                DataFormMaxAnchorageZ2.Text = "0";
                DataFormMaxAnchorageZ2.Enabled = true;
            }
            else
            {
                DataFormMaxAnchorageZ2.Enabled = false;
                DataFormMaxAnchorageZ2.Clear();
            }
            TryParse();
        }

        private void DataFormMaxSpanZCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (DataFormMaxSpanZCheckBox.Checked)
            {
                DataFormMaxSpanZ2.Text = "0";
                DataFormMaxSpanZ2.Enabled = true;
            }
            else
            {
                DataFormMaxSpanZ2.Enabled = false;
                DataFormMaxSpanZ2.Clear();
            }
            TryParse();
        }

        private void DataFormMinSupportZCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (DataFormMinSupportZCheckBox.Checked)
            {
                DataFormMinSupportZ2.Text = "0.500";
                DataFormMinSupportZ2.Enabled = true;
            }
            else
            {
                DataFormMinSupportZ2.Enabled = false;
                DataFormMinSupportZ2.Clear();
            }
            TryParse();
        }

        private void DataFormDir_Leave(object sender, EventArgs e)
        {
            DataFormDir.Text = DataFormDir.Text.TrimEnd(Path.DirectorySeparatorChar);
        }
        private void DataFormSeedDir_Leave(object sender, EventArgs e)
        {
            DataFormSeedDir.Text = DataFormSeedDir.Text.TrimEnd(Path.DirectorySeparatorChar);
        }

        private void DataFormDeployment_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (DataFormDeployment.SelectedIndex == -1)
            {
                DataFormLoadIntoModel.Enabled = false;
                DataFormRegenerateCases.Enabled = false;
                DataFormSCR.Enabled = false;
            }
            else if (DataFormDeployment.SelectedIndices.Count > 0)
            {
                if (DataFormDeployment.SelectedIndices.Count > 1) DataFormLoadIntoModel.Enabled = false;
                else DataFormLoadIntoModel.Enabled = true;

                DataFormRegenerateCases.Enabled = true;
                DataFormSCR.Enabled = true;
                DataFormResolveCurrentModel.Checked = false;
            }
        }

        private void DataFormReloadScheduleButton_Click(object sender, EventArgs e)
        {
            QuantitiesAndSchedules.LoadSchedules();
        }

        private void DataFormDeleteScheduleButton_Click(object sender, EventArgs e)
        {
            if (DataFormSchedules.SelectedRows.Count > 0)
            {
                foreach (DataGridViewRow row in DataFormSchedules.SelectedRows)
                {
                    try { DataFormSchedules.Rows.RemoveAt(row.Index); }
                    catch { }
                }
            }
            TryParse(true);
        }

        private void DataFormResolveCurrentModel_CheckedChanged(object sender, EventArgs e)
        {
            if (DataFormResolveCurrentModel.Checked)
            {
                DataFormDeployment.SelectedIndices.Clear();
                DataFormLoadIntoModel.Enabled = false;
                DataFormRegenerateCases.Enabled = false;
                DataFormSCR.Enabled = false;
            }
        }

        private void DataFormRegenerateCases_Click(object sender, EventArgs e)
        {
            if (DataFormDeployment.SelectedIndices.Count > 0)
            {
                foreach(int index in DataFormDeployment.SelectedIndices)
                {
                    OptimizationCase opt = new OptimizationCase(PhysicalBridge, new OptimizationRules(Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("O1").AsDouble()), Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("O5").AsDouble())), rnd);
                    DataFormDeployment.Items[index] = opt.EncodeCase(index + 1);
                }
                int currentGenNumber = int.Parse(DataFormInitGeneration.Text);
                if (Directory.Exists(Globals.GenerationDir(DataFormDir.Text, currentGenNumber)) == false) Directory.CreateDirectory(Globals.GenerationDir(DataFormDir.Text, currentGenNumber));

                string inputPath = Globals.GenerationDir(DataFormDir.Text, currentGenNumber) + Globals.Input;
                using (StreamWriter saveFile = new StreamWriter(inputPath, false))
                {
                    foreach (var item in DataFormDeployment.Items) saveFile.WriteLine(item.ToString());
                }
            }
        }

        private void DataFormSCR_Click(object sender, EventArgs e)
        {
            if (DataFormDeployment.SelectedIndices.Count > 0)
            {
                int currentGenNumber = int.Parse(DataFormInitGeneration.Text);
                if (Directory.Exists(Globals.GenerationDir(DataFormDir.Text, currentGenNumber)) == false)
                {
                    MessageBox.Show("Generation directory does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                using (StreamWriter saveFile = new StreamWriter(Globals.GenerationDir(DataFormDir.Text, currentGenNumber) + Globals.InputScr, false))
                {
                    foreach (int index in DataFormDeployment.SelectedIndices)
                    {
                        OptimizationCase opt = new OptimizationCase(DataFormDeployment.Items[index] as string);
                        opt.RefreshSpan(PhysicalBridge);

                        PhysicalBridge = new PhysicalBridge(PhysicalBridge.CommandData);
                        saveFile.WriteLine(PhysicalBridge.ToScr());
                    }
                }
            }
        }

        private List<OptimizationCase> ResolveNewGeneration(List<OptimizationCase> opts, List<double> fitness)
        {
            int populationSize = opts.Count();
            Random rnd = new Random();

            double[] lowerBound = opts.First().Boundaries.Min.ToArray();
            double[] upperBound = opts.First().Boundaries.Max.ToArray();
            int a; int b;

            //Selection:
            List<OptimizationCase> parents = new List<OptimizationCase>();
            while (parents.Count < populationSize)
            {
                a = rnd.Next(0, populationSize - 1);
                b = rnd.Next(0, populationSize - 1);

                if (fitness[a] < fitness[b]) parents.Add(opts[a]);
                else parents.Add(opts[b]);
            }

            //Crossover:
            List<OptimizationCase> offspring = new List<OptimizationCase>();
            SimulatedBinaryCrossover crossover = new SimulatedBinaryCrossover(double.Parse(DataFormCrossoverDistributionIndex.Text), lowerBound, upperBound);

            double crossoverProbability = double.Parse(DataFormCrossoverProbability.Text);
            while (offspring.Count < populationSize)
            {
                if (parents.Count == 2)
                {
                    a = rnd.Next(0, parents.Count() - 1);
                    if (a == 0) b = 1;
                    else b = 0;
                }
                else
                {
                    a = rnd.Next(0, parents.Count() - 1);
                    do { b = rnd.Next(0, parents.Count() - 1); }
                    while (a == b);
                }
                OptimizationCase firstParent = new OptimizationCase(parents[a].EncodeCase());
                OptimizationCase secondParent = new OptimizationCase(parents[b].EncodeCase());
                parents.Remove(firstParent);
                parents.Remove(secondParent);

                if (rnd.NextDouble() <= crossoverProbability)
                {
                    double u = rnd.NextDouble();
                    OptimizationCase firstOffspring = new OptimizationCase(opts.First().Boundaries);
                    OptimizationCase secondOffspring = new OptimizationCase(opts.First().Boundaries);
                    for (int i = 0; i < opts.First().Parameters.Count(); i++)
                    {
                        double beta = crossover.CalculateBeta(crossover.DistributionIndex, firstParent.Parameters[i], secondParent.Parameters[i], lowerBound[i], upperBound[i], u);
                        firstOffspring.Parameters[i] = 0.5 * (firstParent.Parameters[i] + secondParent.Parameters[i] - beta * Math.Abs(secondParent.Parameters[i] - firstParent.Parameters[i]));
                        secondOffspring.Parameters[i] = 0.5 * (firstParent.Parameters[i] + secondParent.Parameters[i] + beta * Math.Abs(secondParent.Parameters[i] - firstParent.Parameters[i]));
                    }
                    offspring.Add(firstOffspring);
                    offspring.Add(secondOffspring);
                }
                else
                {
                    offspring.Add(firstParent);
                    offspring.Add(secondParent);
                }
            }

            //Mutation:
            PolynomialMutation mutation = new PolynomialMutation(double.Parse(DataFormMutationDistributionIndex.Text), lowerBound, upperBound);

            double mutationProbability = double.Parse(DataFormMutationProbability.Text);
            foreach (OptimizationCase opt in offspring)
            {
                if (rnd.NextDouble() <= mutationProbability)
                {
                    double u = rnd.NextDouble();
                    for (int i = 0; i < opt.Parameters.Count(); i++)
                    {
                        double delta = mutation.CalculateDelta(mutation.DistributionIndex, opt.Parameters[i], lowerBound[i], upperBound[i], u);
                        opt.Parameters[i] = opt.Parameters[i] + delta * (opt.Boundaries.Max[i] - opt.Boundaries.Min[i]);           
                    }
                }
            }
            return offspring;
        }
    }
}