using System;
using System.IO;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

namespace BridgeOpt
{
    [Transaction(TransactionMode.Manual)]
    public class RevitCodes : IExternalCommand
    {
        public PhysicalBridge PhysicalBridge;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            PhysicalBridge = new PhysicalBridge(commandData);

            DataForm dataForm = new DataForm();
            PhysicalBridge.DataForm = dataForm;
            dataForm.PhysicalBridge = PhysicalBridge;
            dataForm.ShowDialog();

            return Result.Succeeded;
        }
    }

    static class Globals
    {
        private static readonly string GirdersDir = Path.DirectorySeparatorChar + "Girders";
        private static readonly string GirdersEnvelopesDir = GirdersDir + Path.DirectorySeparatorChar + "Envelopes";
        private static readonly string TendonsDir = GirdersDir + Path.DirectorySeparatorChar + "Tendons";
        private static readonly string TendonsNeuralNetworkDir = TendonsDir + Path.DirectorySeparatorChar + "Tendons Neural Network";

        private static readonly string ModelsDir = Path.DirectorySeparatorChar + "Models";
        private static readonly string SlabDir = Path.DirectorySeparatorChar + "Slab";
        private static readonly string SlabEnvelopesDir = SlabDir + Path.DirectorySeparatorChar + "Envelopes";

        public static int ReferenceRebarDiameter = 25;
        public static int NominalStirrupDiameter = 20;

        public static class ExcelFiles
        {
            public static string SlabEnvelopes = SlabEnvelopesDir + Path.DirectorySeparatorChar + "Envelopes.xlsx";
            public static string GirdersEnvelopes = GirdersEnvelopesDir + Path.DirectorySeparatorChar + "Envelopes.xlsx";

            public static string CaseSheetPath(string fileName) { return GirdersEnvelopesDir + Path.DirectorySeparatorChar + fileName; }

            public static string CaseSheetName(int caseNumber) { return "Model. Case " + caseNumber + ".csv"; }
            public static string SimpleCasesSheetName() { return "Model. Simple Cases.csv"; }
        }

        public static class TextFiles
        {
            public static string GeneralGoemetrySummary = Path.DirectorySeparatorChar + "Summary. General Geometry.txt";
            public static string CostSummary = Path.DirectorySeparatorChar + "Summary. Cost.txt";
            public static string StopwatchSummary = Path.DirectorySeparatorChar + "Summary. Stopwatch.txt";

            public static string SlabDefinition = SlabDir + Path.DirectorySeparatorChar + "Slab. Definition.txt";
            public static string SlabRebarsArrangement = SlabDir + Path.DirectorySeparatorChar + "Slab. Rebars Arrangement.txt";
            public static string SlabULS = SlabDir + Path.DirectorySeparatorChar + "Slab. ULS.txt";
            public static string SlabSLS = SlabDir + Path.DirectorySeparatorChar + "Slab. SLS.txt";

            public static string PrestressingModel = RobotFiles.PrestressingModel.Replace(".rtd", ".txt");
            public static string PrestressingScr = RobotFiles.PrestressingModel.Replace(".rtd", " (SCR).txt");

            public static string NeuralNetworkLogs = TendonsNeuralNetworkDir + Path.DirectorySeparatorChar + "Neural Network Logs.txt";

            public static string TendonScrFile(int run)
            {
                return TendonsDir + Path.DirectorySeparatorChar + "Tendon Optimization, run " + run + ". Layout (SCR).txt";
            }
            public static string TendonDefFile(int run, Girders girder)
            {
                return TendonsDir + Path.DirectorySeparatorChar + "Tendon Optimization, run " + run + ". Definition, " + (girder == Girders.LeftGirder ? "Left" : "Right") + " Girder.txt";
            }
            public static string TendonChkFile(int run, Girders girder)
            {
                return TendonsDir + Path.DirectorySeparatorChar + "Tendon Optimization, run " + run + ". Limit states, " + (girder == Girders.LeftGirder ? "Left" : "Right") + " Girder.txt";
            }
            public static string FinalTendon = TendonsDir + Path.DirectorySeparatorChar + "Final Tendon.txt";

            public static string BendingCalcFile(int section, Girders girder)
            {
                return GirdersDir + Path.DirectorySeparatorChar + "Bearing Capacity, Section " + section + "-" + section + ", " + (girder == Girders.LeftGirder ? "Left" : "Right") + " Girder.txt";
            }
            public static string ShearCalcFile(Girders girder)
            {
                return GirdersDir + Path.DirectorySeparatorChar + "Shear + Torsion, " + (girder == Girders.LeftGirder ? "Left" : "Right") + " Girder.txt";
            }
            public static string GirdersRebarsArrangement = GirdersDir + Path.DirectorySeparatorChar + "Girders. Rebars Arrangement.txt";
        }

        public static class RobotFiles
        {
            public static string MainModel = ModelsDir + Path.DirectorySeparatorChar + "Model. Envelopes.rtd";
            public static string PrestressingModel = ModelsDir + Path.DirectorySeparatorChar + "Model. Prestressing.rtd";
        }

        public static class RevitFiles
        {
            public static string RevitPath(PhysicalBridge bridge) { return bridge.Directory + ModelsDir + Path.DirectorySeparatorChar + bridge.Name + ".rvt"; }
        }

        public static string Input = "Input.txt";
        public static string InputScr = "Input. SCR.txt";
        public static string Output = "Output.txt";

        public static string AuxiliaryInput = "Input_AUX.txt";
        public static string ModelName(int generationNumber, int modelNumber)
        {
            return "G" + generationNumber + "_M" + modelNumber;
        }
        public static string ModelDir(string dir, int generationNumber, int modelNumber, bool trimSeparatorChar = false)
        {
            return GenerationDir(dir, generationNumber) + ModelName(generationNumber, modelNumber) +
                (trimSeparatorChar ? "" : Path.DirectorySeparatorChar.ToString());
        }

        public static string GenerationDir(string dir, int generationNumber, bool trimSeparatorChar = false)
        {
            return dir + Path.DirectorySeparatorChar + "G" + generationNumber + (trimSeparatorChar ? "" : Path.DirectorySeparatorChar.ToString());
        }
    }
}