#define SUMMGEOMETRY

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;

namespace BridgeOpt
{
    public enum Symbols
    {
        G1, G2, G3, G4, G5, G7, G8, G9, G10, G11,

        WL,
        WP,
        PL,
        PP,
        OL,
        OP,
        HL,
        HP,
        BL,
        BP,

        BL1, BL2,
        BP2, BP1
    }

    public class OptimizationRules
    {
        public List<double> Min = new List<double>();
        public List<double> Max = new List<double>();
        public double MinCantileverOverhang = 1.000;
        public double MinSpanLength = 1.000;

        public OptimizationRules()
        {
            foreach (Symbols i in Enum.GetValues(typeof(Symbols)))
            {
                Min.Add(0.0);
                Max.Add(0.0);
            }
        }
        public OptimizationRules(double minEdge, double maxEdge)
        {
            foreach(Symbols i in Enum.GetValues(typeof(Symbols)))
            {
                Min.Add(0.0); 
                Max.Add(0.0);
            }

            for (int i = (int) Symbols.G1; i <= (int) Symbols.G11; i++) { Min[i] = 0.180; Max[i] = 0.600; }
            Max[(int) Symbols.G1] = 0.5 * Max[(int) Symbols.G1]; Max[(int) Symbols.G11] = 0.5 * Max[(int) Symbols.G11];
            Max[(int) Symbols.G5] = 0.400;
            Max[(int) Symbols.G7] = 0.400;

            for (int i = (int) Symbols.WL; i <= (int) Symbols.WP; i++) { Min[i] = 0.000; Max[i] = maxEdge - minEdge; }
            for (int i = (int) Symbols.PL; i <= (int) Symbols.PP; i++) { Min[i] = 0.000; Max[i] = maxEdge - minEdge; }
            Min[(int) Symbols.OL] = minEdge; Max[(int) Symbols.OL] = maxEdge;
            Min[(int) Symbols.OP] = minEdge; Max[(int) Symbols.OP] = maxEdge;

            Min[(int) Symbols.HL] = 0.800; Max[(int) Symbols.HL] = 2.200; Min[(int) Symbols.HP] = Min[(int) Symbols.HL]; Max[(int) Symbols.HP] = Max[(int) Symbols.HL];
            Min[(int) Symbols.BL] = 0.500; Max[(int) Symbols.BL] = 3.000; Min[(int) Symbols.BP] = Min[(int) Symbols.BL]; Max[(int) Symbols.BP] = Max[(int) Symbols.BL];
            for (int i = (int) Symbols.BL1; i <= (int) Symbols.BP1; i++) { Min[i] = 0.000; Max[i] = 0.300; }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class OptimizationCase
    {
        public PhysicalBridge PhysicalBridge;
        public AnalyticalSlab AnalyticalSlab;
        public AnalyticalGirders AnalyticalGirders;

        public AnalyticalBridge AnalyticalBridge;
        public AnalyticalBridge AnalyticalPrestressedBridge;

        public TendonNeuralNetwork TendonNeuralNetwork;
        public TendonOptimization TendonOptimization;
        public QuantitiesAndSchedules QuantitiesAndSchedules;

        public List<double> Parameters = new List<double>();
        public OptimizationRules Boundaries;

        public OptimizationCase()
        {
            foreach (string parameter in Enum.GetNames(typeof(Symbols))) Parameters.Add(0.0);
            Boundaries = new OptimizationRules();
        }
        public OptimizationCase(OptimizationRules boundaries)
        {
            foreach (string parameter in Enum.GetNames(typeof(Symbols))) Parameters.Add(0.0);
            Boundaries = boundaries;
        }
        public OptimizationCase(PhysicalBridge bridge)
        {
            foreach (string parameter in Enum.GetNames(typeof(Symbols))) Parameters.Add(Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item(parameter).AsDouble()));
            Boundaries = new OptimizationRules();

            foreach (int i in Enum.GetValues(typeof(Symbols)))
            {
                Boundaries.Min[i] = Parameters[i];
                Boundaries.Max[i] = Parameters[i];
            }
        }
        public OptimizationCase(PhysicalBridge seed, OptimizationRules boundaries = null, Random rnd = null)
        {
            double minEdge = Converters.ToMeters(seed.Superstructure.ParametersMap.get_Item("O1").AsDouble());
            double maxEdge = Converters.ToMeters(seed.Superstructure.ParametersMap.get_Item("O5").AsDouble());
            foreach (Symbols i in Enum.GetValues(typeof(Symbols))) Parameters.Add(0.0);

            if (boundaries == null) boundaries = new OptimizationRules(minEdge, maxEdge);
            Boundaries = boundaries;

            //Case generation:
            if (rnd == null) rnd = new Random();
            Parameters[(int) Symbols.BL] = Boundaries.Min[(int) Symbols.BL] + rnd.NextDouble() * (Boundaries.Max[(int) Symbols.BL] - Boundaries.Min[(int) Symbols.BL]); Parameters[(int) Symbols.BP] = Parameters[(int) Symbols.BL];
            Parameters[(int) Symbols.HL] = Boundaries.Min[(int) Symbols.HL] + rnd.NextDouble() * (Boundaries.Max[(int) Symbols.HL] - Boundaries.Min[(int) Symbols.HL]); Parameters[(int) Symbols.HP] = Parameters[(int) Symbols.HL];

            Parameters[(int) Symbols.BL1] = Boundaries.Min[(int) Symbols.BL1] + rnd.NextDouble() * (Boundaries.Max[(int) Symbols.BL1] - Boundaries.Min[(int) Symbols.BL1]);
            Parameters[(int) Symbols.BL2] = Parameters[(int) Symbols.BL1];
            Parameters[(int) Symbols.BP1] = Parameters[(int) Symbols.BL1];
            Parameters[(int) Symbols.BP2] = Parameters[(int) Symbols.BL1];

            double totalGirderWidth = Parameters[(int) Symbols.BL] + Parameters[(int) Symbols.BL1] + Parameters[(int) Symbols.BL2];
            double minOffset, maxOffset;

            if (rnd.NextDouble() < 0.5)
            {
                minOffset = Boundaries.Min[(int) Symbols.OL] + Boundaries.MinCantileverOverhang + 0.5 * totalGirderWidth;
                maxOffset = Boundaries.Max[(int) Symbols.OL] - Boundaries.MinCantileverOverhang - totalGirderWidth - Boundaries.MinSpanLength - 0.5 * totalGirderWidth;
                Parameters[(int) Symbols.OL] = minOffset + rnd.NextDouble() * (maxOffset - minOffset);

                minOffset = Parameters[(int) Symbols.OL] + Boundaries.MinSpanLength + totalGirderWidth;
                maxOffset = Boundaries.Max[(int) Symbols.OP] - Boundaries.MinCantileverOverhang - 0.5 * totalGirderWidth;
                Parameters[(int) Symbols.OP] = minOffset + rnd.NextDouble() * (maxOffset - minOffset);
            }
            else
            {
                minOffset = Boundaries.Min[(int) Symbols.OP] + Boundaries.MinCantileverOverhang + totalGirderWidth + Boundaries.MinSpanLength + 0.5 * totalGirderWidth;
                maxOffset = Boundaries.Max[(int) Symbols.OP] - Boundaries.MinCantileverOverhang - 0.5 * totalGirderWidth;
                Parameters[(int) Symbols.OP] = minOffset + rnd.NextDouble() * (maxOffset - minOffset);

                minOffset = Boundaries.Min[(int) Symbols.OL] + Boundaries.MinCantileverOverhang + totalGirderWidth + 0.5 * totalGirderWidth;
                maxOffset = Parameters[(int) Symbols.OP] - Boundaries.MinSpanLength - totalGirderWidth;
                Parameters[(int) Symbols.OL] = minOffset + rnd.NextDouble() * (maxOffset - minOffset);
            }

            if (rnd.NextDouble() < 0.5) { Parameters[(int) Symbols.WL] = 0.000; Parameters[(int) Symbols.WP] = 0.000; }
            else
            {
                double cantileverOverhang = Math.Min(Parameters[(int) Symbols.OL] - minEdge, maxEdge - Parameters[(int) Symbols.OP]) - 0.5 * totalGirderWidth;
                if (cantileverOverhang > Boundaries.Max[(int) Symbols.WL]) cantileverOverhang = Boundaries.Max[(int) Symbols.WL];
                Parameters[(int) Symbols.WL] = Boundaries.Min[(int) Symbols.WL] + rnd.NextDouble() * (cantileverOverhang - Boundaries.Min[(int) Symbols.WL]);
                Parameters[(int) Symbols.WP] = Parameters[(int) Symbols.WL];
            }

            double spanLength = Parameters[(int) Symbols.OP] - Parameters[(int) Symbols.OL] - totalGirderWidth;
            Parameters[(int) Symbols.PL] = Boundaries.Min[(int) Symbols.PL] + rnd.NextDouble() * (Math.Min(0.5 * spanLength, Boundaries.Max[(int) Symbols.PL]) - Boundaries.Min[(int) Symbols.PL]);
            Parameters[(int) Symbols.PP] = Parameters[(int) Symbols.PL];

            Parameters[(int) Symbols.G1] = Boundaries.Min[(int) Symbols.G1] + rnd.NextDouble() * (Boundaries.Max[(int) Symbols.G1] - Boundaries.Min[(int) Symbols.G1]); Parameters[(int) Symbols.G11] = Parameters[(int) Symbols.G1];
            Parameters[(int) Symbols.G3] = Boundaries.Min[(int) Symbols.G3] + rnd.NextDouble() * (Boundaries.Max[(int) Symbols.G3] - Boundaries.Min[(int) Symbols.G3]); Parameters[(int) Symbols.G9] = Parameters[(int) Symbols.G3];
            if (Parameters[(int) Symbols.G3] < Parameters[(int) Symbols.G1]) Parameters[(int) Symbols.G3] = Parameters[(int) Symbols.G1];
            if (Parameters[(int) Symbols.WL] == 0.000) Parameters[(int) Symbols.G2] = Parameters[(int) Symbols.G3];
            else
            {
                Parameters[(int) Symbols.G2] = Boundaries.Min[(int) Symbols.G2] + rnd.NextDouble() * (Boundaries.Max[(int) Symbols.G2] - Boundaries.Min[(int) Symbols.G2]);
                if (Parameters[(int) Symbols.G2] < Parameters[(int) Symbols.G1]) Parameters[(int) Symbols.G2] = Parameters[(int) Symbols.G1];
                if (Parameters[(int) Symbols.G2] > Parameters[(int) Symbols.G3]) Parameters[(int) Symbols.G2] = Parameters[(int) Symbols.G3];
            }
            Parameters[(int) Symbols.G10] = Parameters[(int) Symbols.G2];

            Parameters[(int) Symbols.G5] = Boundaries.Min[(int) Symbols.G5] + rnd.NextDouble() * (Boundaries.Max[(int) Symbols.G5] - Boundaries.Min[(int) Symbols.G5]);
            Parameters[(int) Symbols.G7] = Parameters[(int) Symbols.G5];
            if (Parameters[(int) Symbols.PL] == 0.000) Parameters[(int) Symbols.G4] = Parameters[(int) Symbols.G5];
            else
            {
                Parameters[(int) Symbols.G4] = Boundaries.Min[(int) Symbols.G4] + rnd.NextDouble() * (Boundaries.Max[(int) Symbols.G4] - Boundaries.Min[(int) Symbols.G4]);
                if (Parameters[(int) Symbols.G4] < Parameters[(int) Symbols.G5]) Parameters[(int) Symbols.G4] = Parameters[(int) Symbols.G5];
            }
            Parameters[(int) Symbols.G8] = Parameters[(int) Symbols.G4];
            for (int p = 0; p < Parameters.Count(); p++) Parameters[p] = Math.Round(Math.Round(Parameters[p] / 0.0005) * 0.0005, 4);
        }
        public OptimizationCase(string encoded)
        {
            foreach (Symbols i in Enum.GetValues(typeof(Symbols))) Parameters.Add(0.0);
            Boundaries = new OptimizationRules();

            encoded = encoded.Remove(0, encoded.IndexOf("\t") + 1);
            while (encoded.Length > 0)
            {
                string trimmed;
                if (encoded.Contains("\t"))
                {
                    trimmed = encoded.Substring(0, encoded.IndexOf("\t"));
                    encoded = encoded.Substring(encoded.IndexOf("\t") + 1);
                }
                else
                {
                    trimmed = encoded;
                    encoded = "";
                }

                string paramName = trimmed.Substring(0, trimmed.IndexOf(":"));
                string paramValue = trimmed.Substring(trimmed.IndexOf(":") + 2);
                paramValue = paramValue.Substring(0, paramValue.IndexOf(" "));

                if (Enum.GetNames(typeof(Symbols)).ToList().Contains(paramName) && double.TryParse(paramValue, out double val))
                {
                    int index = Enum.GetNames(typeof(Symbols)).ToList().IndexOf(paramName);
                    Parameters[index] = val;

                    string minBound, maxBound;
                    minBound = trimmed.Substring(trimmed.IndexOf("(") + 1).TrimEnd(')'); maxBound = minBound;
                    minBound = minBound.Substring(0, minBound.IndexOf(" "));
                    maxBound = maxBound.Substring(maxBound.LastIndexOf(" ") + 1);

                    if (double.TryParse(minBound, out double minVal) && double.TryParse(maxBound, out double maxVal))
                    {
                        Boundaries.Min[index] = minVal;
                        Boundaries.Max[index] = maxVal;
                    }
                }
            }
        }

        public void Resolve(PhysicalBridge seed, string modelName, string modelDirectory)
        {
            DirectoryCopy(@seed.DataForm.DataFormSeedDir.Text, modelDirectory, true);
            Stopwatch caseStopWatch = new Stopwatch();
            Stopwatch localStopWatch = new Stopwatch();

            using (StreamWriter stopwatchSummary = new StreamWriter(modelDirectory + Globals.TextFiles.StopwatchSummary, false)) stopwatchSummary.WriteLine("Start:\t\t\t\t\t\t" + DateTime.Now.ToString());
            caseStopWatch.Start();  localStopWatch.Start();
            bool penalized = !RefreshSpan(seed);

            PhysicalBridge = new PhysicalBridge(seed.CommandData);
            PhysicalBridge.DataForm = seed.DataForm;

            PhysicalBridge.Name = modelName;
            PhysicalBridge.Directory = modelDirectory;
            PhysicalBridge.SpanLength.Add(double.Parse(PhysicalBridge.DataForm.DataFormSpanLength1.Text));
            PhysicalBridge.SpanLength.Add(double.Parse(PhysicalBridge.DataForm.DataFormSpanLength2.Text));

            PhysicalBridge.CriticalCrossSections = new List<double>();
            PhysicalBridge.CriticalCrossSections.Add(0.4 * PhysicalBridge.SpanLength.First());
            PhysicalBridge.CriticalCrossSections.Add(0.5 * PhysicalBridge.SpanLength.First());
            PhysicalBridge.CriticalCrossSections.Add(1.0 * PhysicalBridge.SpanLength.First());
            if (PhysicalBridge.SuperstructureCrossSection.Count == 0) penalized = true;

#if SUMMGEOMETRY
            PhysicalBridge.PrintSummary();
#endif
            Document doc = PhysicalBridge.CommandData.Application.ActiveUIDocument.Document;
            if (!penalized)
            {
                QuantitiesAndSchedules = new QuantitiesAndSchedules(PhysicalBridge);
                QuantitiesAndSchedules.LoadSchedules();
                using (StreamWriter stopwatchSummary = new StreamWriter(modelDirectory + Globals.TextFiles.StopwatchSummary, true))
                {
                    stopwatchSummary.WriteLine("Model refreshed:\t\t\t\t" + DateTime.Now.ToString() + "\t" + localStopWatch.Elapsed.ToString() + "\n");
                    stopwatchSummary.WriteLine("Analytical slab:");
                }
                localStopWatch.Reset();

                //Slab analysis:
                Stopwatch slabStopWatch = new Stopwatch();
                slabStopWatch.Start();
                AnalyticalSlab = new AnalyticalSlab(PhysicalBridge);
                slabStopWatch.Stop();
                using (StreamWriter stopwatchSummary = new StreamWriter(modelDirectory + Globals.TextFiles.StopwatchSummary, true))
                {
                    stopwatchSummary.WriteLine("Analytical slab, end time:\t\t\t" + DateTime.Now.ToString() + "\t" + slabStopWatch.Elapsed.ToString() + "\n");
                    stopwatchSummary.WriteLine("General model creation:");
                }

                //Fine element method model, general loads:
                Stopwatch generalModelStopWatch = new Stopwatch();
                generalModelStopWatch.Start();
                AnalyticalBridge = new AnalyticalBridge(PhysicalBridge, int.Parse(PhysicalBridge.DataForm.DataFormSpanDivision.Text), false);
                generalModelStopWatch.Stop();
                using (StreamWriter stopwatchSummary = new StreamWriter(modelDirectory + Globals.TextFiles.StopwatchSummary, true))
                {
                    stopwatchSummary.WriteLine("General model creation, end time:\t\t" + DateTime.Now.ToString() + "\t" + generalModelStopWatch.Elapsed.ToString() + "\n");
                    stopwatchSummary.WriteLine("Girder envelopes:");
                }

                //General envelopes:
                Stopwatch envelopesStopWatch = new Stopwatch();
                envelopesStopWatch.Start();
                AnalyticalBridge.Envelopes = new Envelopes(PhysicalBridge, AnalyticalBridge.MobileCases.ToArray(), int.Parse(PhysicalBridge.DataForm.DataFormEnvelopeStep.Text));
                envelopesStopWatch.Stop();
                using (StreamWriter stopwatchSummary = new StreamWriter(modelDirectory + Globals.TextFiles.StopwatchSummary, true))
                {
                    stopwatchSummary.WriteLine("Girder envelopes, end time:\t\t\t" + DateTime.Now.ToString() + "\t" + envelopesStopWatch.Elapsed.ToString() + "\n");
                    stopwatchSummary.WriteLine("Prestressed model creation:");
                }

                //Fine element method model, prestressing loads:
                Stopwatch prestressingModelStopWatch = new Stopwatch();
                prestressingModelStopWatch.Start();
                AnalyticalPrestressedBridge = new AnalyticalBridge(PhysicalBridge, int.Parse(PhysicalBridge.DataForm.DataFormSpanDivision.Text), true);
                prestressingModelStopWatch.Stop();
                using (StreamWriter stopwatchSummary = new StreamWriter(modelDirectory + Globals.TextFiles.StopwatchSummary, true))
                {
                    stopwatchSummary.WriteLine("Prestressed model creation, end time:\t\t" + DateTime.Now.ToString() + "\t" + prestressingModelStopWatch.Elapsed.ToString() + "\n");
                }

                //Tenonds, neural network, prediction of prestressing effects:
                Stopwatch tendonNetworkStopWatch = new Stopwatch();
                tendonNetworkStopWatch.Start();
                TendonNeuralNetwork = new TendonNeuralNetwork(AnalyticalPrestressedBridge);
                tendonNetworkStopWatch.Stop();
                using (StreamWriter stopwatchSummary = new StreamWriter(modelDirectory + Globals.TextFiles.StopwatchSummary, true))
                {
                    stopwatchSummary.WriteLine("Tendon neural network:\t\t\t\t" + DateTime.Now.ToString() + "\t" + tendonNetworkStopWatch.Elapsed.ToString());
                }

                //Tenonds, genetic algorithm, optimization of tendons layout:
                Stopwatch tendonOptimizationStopWatch = new Stopwatch();
                tendonOptimizationStopWatch.Start();
                TendonOptimization = new TendonOptimization(PhysicalBridge, AnalyticalBridge.Envelopes, TendonNeuralNetwork);
                tendonOptimizationStopWatch.Stop();
                using (StreamWriter stopwatchSummary = new StreamWriter(modelDirectory + Globals.TextFiles.StopwatchSummary, true))
                {
                    stopwatchSummary.WriteLine("Tendon optimization:\t\t\t\t" + DateTime.Now.ToString() + "\t" + tendonOptimizationStopWatch.Elapsed.ToString());
                }

                if (TendonOptimization.Result == null) penalized = true;
                if (!penalized)
                {
                    //Bearing capacity check, reinforcement calculation:
                    Stopwatch girderReinforcementStopWatch = new Stopwatch();
                    girderReinforcementStopWatch.Start();
                    AnalyticalGirders = new AnalyticalGirders(PhysicalBridge, TendonOptimization.Result.ToDiscreteTendons());
                    girderReinforcementStopWatch.Stop();
                    using (StreamWriter stopwatchSummary = new StreamWriter(modelDirectory + Globals.TextFiles.StopwatchSummary, true))
                    {
                        stopwatchSummary.WriteLine("Girders reinforcement:\t\t\t\t" + DateTime.Now.ToString() + "\t" + girderReinforcementStopWatch.Elapsed.ToString());
                    }
                    localStopWatch.Start();

                    if (AnalyticalGirders.GirdersRebars == null) penalized = true;
                    if (!penalized)
                    {
                        using (Transaction transaction = new Transaction(doc, "Pushing calculated data"))
                        {
                            transaction.Start();
                            if (penalized == false)
                            {
                                PhysicalBridge.Superstructure.LookupParameter("Masa zbrojenia").Set(AnalyticalGirders.GirdersRebars.Mass + AnalyticalSlab.SlabRebars.Mass);
                                PhysicalBridge.Superstructure.LookupParameter("Masa sprężenia").Set(TendonOptimization.Result.Mass);
                                PhysicalBridge.Superstructure.LookupParameter("Wskaźnik zbrojenia").Set((AnalyticalGirders.GirdersRebars.Mass + AnalyticalSlab.SlabRebars.Mass) / Converters.ToCubicMeters(PhysicalBridge.Superstructure.GetMaterialVolume(PhysicalBridge.Superstructure.GetMaterialIds(false).First())));
                            }
                            else
                            {
                                PhysicalBridge.Superstructure.LookupParameter("Masa zbrojenia").Set(0.000);
                                PhysicalBridge.Superstructure.LookupParameter("Masa sprężenia").Set(0.000);
                                PhysicalBridge.Superstructure.LookupParameter("Wskaźnik zbrojenia").Set(0.000);
                            }
                            transaction.Commit();
                        }
                    }
                    else using (StreamWriter stopwatchSummary = new StreamWriter(modelDirectory + Globals.TextFiles.StopwatchSummary, true)) stopwatchSummary.WriteLine("\nModel penalized due to unresolved girders rebars arrangement.");
                }
                else using (StreamWriter stopwatchSummary = new StreamWriter(modelDirectory + Globals.TextFiles.StopwatchSummary, true)) stopwatchSummary.WriteLine("\nModel penalized due to unresolved tendons layout.");
            }
            else using (StreamWriter stopwatchSummary = new StreamWriter(modelDirectory + Globals.TextFiles.StopwatchSummary, true)) stopwatchSummary.WriteLine("\nModel penalized due to unresolved span geometry.");

            double fitness = 0.0;
            if (penalized) fitness = Math.Pow(10, 8);
            else QuantitiesAndSchedules.Calculate(true);

            using (StreamWriter stopwatchSummary = new StreamWriter(modelDirectory + Globals.TextFiles.StopwatchSummary, true)) stopwatchSummary.WriteLine("Model changes pushed:\t\t\t\t" + DateTime.Now.ToString() + "\t" + localStopWatch.Elapsed.ToString()); localStopWatch.Restart();

            doc.SaveAs(Globals.RevitFiles.RevitPath(PhysicalBridge));
            caseStopWatch.Stop(); localStopWatch.Stop();
            using (StreamWriter stopwatchSummary = new StreamWriter(modelDirectory + Globals.TextFiles.StopwatchSummary, true))
            {
                stopwatchSummary.WriteLine("Model saved:\t\t\t\t\t" + DateTime.Now.ToString() + "\t" + localStopWatch.Elapsed.ToString());
                stopwatchSummary.WriteLine("\nTotal:\t\t\t\t\t\t\t\t\t" + caseStopWatch.Elapsed.ToString());
            }
        }

        public bool RefreshSpan(PhysicalBridge bridge)
        {
            bool success = true;
            Document doc = bridge.CommandData.Application.ActiveUIDocument.Document;
            using (Transaction transaction = new Transaction(doc, "Initializing individual"))
            {
                transaction.Start();

                var failureOptions = transaction.GetFailureHandlingOptions();
                failureOptions.SetFailuresPreprocessor(new DuplicateMarkSwallower());
                transaction.SetFailureHandlingOptions(failureOptions);

                string[] parameters = Enum.GetNames(typeof(Symbols));
                for (int p = 0; p < parameters.Count(); p++)
                {
                    GlobalParameter globalParameter = doc.GetElement(GlobalParametersManager.FindByName(doc, parameters[p])) as GlobalParameter;
                    if (globalParameter != null) globalParameter.SetValue(new DoubleParameterValue(Converters.FromMeters(Parameters[p])));
                }

                double volume = 0.0;
                try 
                { 
                    volume = Converters.FromCubicMeters(bridge.Superstructure.GetMaterialVolume(bridge.Superstructure.GetMaterialIds(false).First()));
                }
                catch { success = false; };

                if (success)
                {
                    FilteredElementCollector collector = new FilteredElementCollector(doc);
                    List<Element> levels = collector.OfClass(typeof(Level)).ToElements().ToList();

                    if (levels.Count() > 0)
                    {
                        double O2 = Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("O2").AsDouble());
                        double O3 = Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("O3").AsDouble());
                        double O4 = Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("O4").AsDouble());

                        double S1 = bridge.Superstructure.ParametersMap.get_Item("S1").AsDouble();
                        double S2 = bridge.Superstructure.ParametersMap.get_Item("S2").AsDouble();
                        double S3 = bridge.Superstructure.ParametersMap.get_Item("S3").AsDouble();
                        double S4 = bridge.Superstructure.ParametersMap.get_Item("S4").AsDouble();

                        double bearingHeight = 0.169;
                        for (int i = levels.Count() - 1; i >= 0; i--)
                        {
                            if ((levels[i].Name.Contains("Wierzch ławy podłożyskowej") == false) && (levels[i].Name.Contains("Wierzch trzonów") == false)) levels.RemoveAt(i);
                        }
                        if (levels.Count() > 0)
                        {
                            double zRef = O3 < 0 ? -1.0 * O3 * S3 / 100 : -1.0 * O3 * S2 / 100;

                            double zLeft;
                            if (Parameters[(int) Symbols.OL] > O2) zLeft = zRef + (O3 - Parameters[(int) Symbols.OL]) * S2 / 100;
                            else zLeft = zRef - (O2 - O3) * S2 / 100 + (O2 - Parameters[(int) Symbols.OL]) * S1 / 100;

                            double zRight;
                            if (Parameters[(int) Symbols.OP] < O4) zRight = zRef + (O3 - Parameters[(int) Symbols.OP]) * S3 / 100;
                            else zRight = zRef - (O4 - O3) * S3 / 100 + (O4 - Parameters[(int) Symbols.OP]) * S4 / 100;

                            double nominalBearingSpace = Converters.ToMeters(((doc.GetElement(GlobalParametersManager.FindByName(doc, "Nominalna wysokość przestrzeni łożyskowania")) as GlobalParameter).GetValue() as DoubleParameterValue).Value);
                            double roadLayersThickness = Converters.ToMeters(((doc.GetElement(GlobalParametersManager.FindByName(doc, "Nominalna grubość nawierzchni")) as GlobalParameter).GetValue() as DoubleParameterValue).Value);
                            double z = Math.Round((Math.Min(zLeft - Parameters[(int) Symbols.HL], zRight - Parameters[(int) Symbols.HP]) - nominalBearingSpace - roadLayersThickness) / 0.05) * 0.05;
                            foreach (Element level in levels) (level as Level).Elevation = Converters.FromMeters(z);

                            GlobalParameter leftBearingAshlar = doc.GetElement(GlobalParametersManager.FindByName(doc, "Wysokość ciosu. Dźwigar lewy")) as GlobalParameter;
                            GlobalParameter rightBearingAshlar = doc.GetElement(GlobalParametersManager.FindByName(doc, "Wysokość ciosu. Dźwigar prawy")) as GlobalParameter;
                            leftBearingAshlar.SetValue(new DoubleParameterValue(Converters.FromMeters(zLeft - Parameters[(int) Symbols.HL] - roadLayersThickness - z - bearingHeight)));
                            rightBearingAshlar.SetValue(new DoubleParameterValue(Converters.FromMeters(zRight - Parameters[(int) Symbols.HP] - roadLayersThickness - z - bearingHeight)));
                        }
                    }
                }
                transaction.Commit();
            }
            return success;
        }

        public string EncodeCase(int index = 0)
        {
            string encoded = index.ToString();
            for (int p = 0; p < Parameters.Count(); p++)
            {
                encoded += "\t" + Enum.GetNames(typeof(Symbols))[p] + ": " + string.Format("{0:0.0000} ({1:0.0000} do {2:0.0000})", Parameters[p], Boundaries.Min[p], Boundaries.Max[p]);
            }
            return encoded;
        }

        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            if (!dir.Exists) throw new DirectoryNotFoundException("Source directory does not exist or could not be found: " + sourceDirName);

            DirectoryInfo[] dirs = dir.GetDirectories();
            if (Directory.Exists(destDirName)) Directory.Delete(destDirName, true);
            Directory.CreateDirectory(destDirName);

            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, false);
            }
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs);
                }
            }
        }
    }

    public class DuplicateMarkSwallower : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
        {
            var failures = accessor.GetFailureMessages();
            foreach (var failure in failures)
            {
                //var id = failure.GetFailureDefinitionId();
                //if (BuiltInFailures.GeneralFailures.ErrorInFamilyResolved == id)
                accessor.DeleteWarning(failure);
            }
            return FailureProcessingResult.Continue;
        }
    }
}