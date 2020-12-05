#define TENDONDEF //Output: tendons definition
#define TENDONSCR //Output: tendons script definitiom
#define TENDONCHK //Output: code limits check

//If LIMITED is defined, limited reports are printed:
//      First 10 generations,
//      Every 20th generation,
//      Final generation:
//#define LIMITED

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using GeneticSharp.Domain;
using GeneticSharp.Domain.Chromosomes;
using GeneticSharp.Domain.Fitnesses;
using GeneticSharp.Domain.Populations;
using GeneticSharp.Domain.Reinsertions;
using GeneticSharp.Domain.Selections;
using GeneticSharp.Domain.Terminations;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Data.Text;
using static BridgeOpt.LayoutDefinition;

namespace BridgeOpt
{
    public enum SectionData
    {
        SpanSection = 0,
        MidspanSection = 1,
        SupportSection = 2
    }

    public class Moments
    {
        public double Min;
        public double Max;

        public Moments(double minEnvelope, double maxEnvelope)
        {
            Min = minEnvelope;
            Max = maxEnvelope;
        }
    }
    public class OptimizationCombinations
    {
        public Moments DesignCombination;
        public Moments RareCombination;
        public Moments FrequentCombination;
        public Moments QuasiPermanentCombination;

        public OptimizationCombinations(double designMomentMin, double designMomentMax, double rareMomentMin, double rareMomentMax, double frequentMomentMin, double frequentMomentMax, double quasiPermanentMomentMin, double quasiPermanentMomentMax)
        {
            DesignCombination = new Moments(designMomentMin, designMomentMax);
            RareCombination = new Moments(rareMomentMin, rareMomentMax);
            FrequentCombination = new Moments(frequentMomentMin, frequentMomentMax);
            QuasiPermanentCombination = new Moments(quasiPermanentMomentMin, quasiPermanentMomentMax);
        }
    }
    public class OptimizationPhase
    {
        public List<OptimizationCombinations> Sections = new List<OptimizationCombinations>();
        public OptimizationPhase(int numberOfSections, List<double> minMoments, List<double> maxMoments)
        {
            for (int i = 0; i < numberOfSections; i++)
            {
                Sections.Add(new OptimizationCombinations(minMoments[4 * i], maxMoments[4 * i], minMoments[4 * i + 1], maxMoments[4 * i + 1], minMoments[4 * i + 2], maxMoments[4 * i + 2], minMoments[4 * i + 3], maxMoments[4 * i + 3]));
            }
        }
    }

    public class TendonOptimizationResultComponent
    {
        public FullTendon FullTendon = null;
        public PartialTendon PartialTendon = null;
        public int NumberOfStrands;
        public int NumberOfTendons;

        public double TendonMass;

        public TendonOptimizationResultComponent(FullTendon fullTendon, int numberOfStrands, int numberOfTendons)
        {
            FullTendon = fullTendon;
            FullTendon.PrestressForce = numberOfTendons * numberOfStrands * FullTendon.PrestressForce;
            FullTendon.TendonArea = numberOfTendons * numberOfStrands * 0.000150;
            NumberOfStrands = numberOfStrands;
            NumberOfTendons = numberOfTendons;

            DiscreteTendon discreteTendon = new DiscreteTendon(fullTendon, 0);
            TendonMass = FullTendon.TendonArea * discreteTendon.GetTendonLength(discreteTendon.GetFullTendon().PointE.X) * 7850; //kg
        }
        public TendonOptimizationResultComponent(PartialTendon partialTendon, int numberOfStrands, int numberOfTendons)
        {
            PartialTendon = partialTendon;
            PartialTendon.PrestressForce = numberOfTendons * numberOfStrands * PartialTendon.PrestressForce;
            PartialTendon.TendonArea = numberOfTendons * numberOfStrands * 0.000150;
            NumberOfTendons = numberOfTendons;

            DiscreteTendon discreteTendon = new DiscreteTendon(partialTendon, 0);
            TendonMass = 2 * PartialTendon.TendonArea * discreteTendon.GetTendonLength(discreteTendon.GetPartialTendon().PointD.X) * 7850; //kg
        }
    }
    public class TendonOptimizationResult
    {
        public List<TendonOptimizationResultComponent> FullTendons = new List<TendonOptimizationResultComponent>();
        public List<TendonOptimizationResultComponent> PartialTendons = new List<TendonOptimizationResultComponent>();
        public double Mass;
        public bool Penalized = false;

        public TendonOptimizationResult() { }
        public void AddFullTendon(FullTendon tendon, int numberOfStrands, int numberOfTendons)
        {
            FullTendons.Add(new TendonOptimizationResultComponent(tendon, numberOfStrands, numberOfTendons));
            CalculateMass();
        }
        public void AddPartialTendon(PartialTendon tendon, int numberOfStrands, int numberOfTendons)
        {
            PartialTendons.Add(new TendonOptimizationResultComponent(tendon, numberOfStrands, numberOfTendons));
            CalculateMass();
        }

        private void CalculateMass()
        {
            Mass = 0.0;

            foreach (TendonOptimizationResultComponent tendon in FullTendons) Mass += tendon.TendonMass;
            foreach (TendonOptimizationResultComponent tendon in PartialTendons) Mass += tendon.TendonMass;
            Mass = 2 * Mass; //Two girders
        }

        public List<DiscreteTendon> ToDiscreteTendons(double dx = 0.5)
        {
            List<DiscreteTendon> discreteTendons = new List<DiscreteTendon>();
            foreach (TendonOptimizationResultComponent tendon in FullTendons) discreteTendons.Add(new DiscreteTendon(tendon.FullTendon, dx));
            foreach (TendonOptimizationResultComponent tendon in PartialTendons) discreteTendons.Add(new DiscreteTendon(tendon.PartialTendon, dx));

            return discreteTendons;
        }
    }

    public class TendonOptimization
    {
        public int[] NumberOfStrands = new[] { 12, 15, 19, 22, 24, 27, 31 };
        public PhysicalBridge PhysicalBridge;
        public Envelopes Envelopes;

        public TendonNeuralNetwork Network;
        public TendonOptimizationResult Result;
        public int Runs;

        public double InitialPrestressForce;

        public enum TendonType
        {
            FullTendon = 1,
            PartialTendon = 2
        }

        public enum StressData
        {
            BottomMin = 0,
            BottomMax = 1,
            TopMin = 2,
            TopMax = 3
        }

        public TendonOptimization(PhysicalBridge bridge, Envelopes envelopes, TendonNeuralNetwork network, double initialPrestressForce = 223.0)
        {
            PhysicalBridge = bridge;

            Envelopes = envelopes;
            Network = network;

            InitialPrestressForce = initialPrestressForce;
            Result = null;

            List<TendonOptimizationResult> optimizationResults = new List<TendonOptimizationResult>();
            for (int run = 0; run < int.Parse(PhysicalBridge.DataForm.DataFormTendonGeneticAlgorithmRuns.Text); run++)
            {
                TendonOptimizationResult TendonOptimizationResult = RunTendonOptimization();
                if (TendonOptimizationResult.Penalized == false) optimizationResults.Add(TendonOptimizationResult);
            }
            if (optimizationResults.Count == 0)
            {
                for (int run = 0; run < int.Parse(PhysicalBridge.DataForm.DataFormTendonGeneticAlgorithmRuns.Text); run++)
                {
                    TendonOptimizationResult TendonOptimizationResult = RunTendonOptimization(2);
                    if (TendonOptimizationResult.Penalized == false) optimizationResults.Add(TendonOptimizationResult);
                }
            }

            if (optimizationResults.Count() > 0)
            {
                if (optimizationResults.Count() == 1) Result = optimizationResults.First();
                else Result = optimizationResults.OrderBy(t => t.Mass).First();

                using (StreamWriter finalTendonFile = new StreamWriter(PhysicalBridge.Directory + Globals.TextFiles.FinalTendon, false))
                {
                    string strTitle = "\tA.Z\tB.X\tB.Z\tC.Z\tR\tN.t\tN.s\tF\tA\tL\tW\tW.tot";
                    strTitle += "\tN1\tN1.t\tN1.tot\tM1\tM1.t\tM1.tot";
                    strTitle += "\tN2\tN2.t\tN2.tot\tM2\tM2.t\tM2.tot";
                    strTitle += "\tN3\tN3.t\tN3.tot\tM3\tM3.t\tM3.tot";

                    finalTendonFile.WriteLine(strTitle);

                    double totalMass = 0; double[] totalForce = new double[3]; double[] totalMoment = new double[3];
                    totalForce[0] = 0; totalMoment[0] = 0;
                    totalForce[1] = 0; totalMoment[1] = 0;
                    totalForce[2] = 0; totalMoment[2] = 0;
                    foreach (TendonOptimizationResultComponent comp in Result.FullTendons)
                    {
                        FullTendon tendon = comp.FullTendon;
                        DiscreteTendon discreteTendon = new DiscreteTendon(tendon, 0);

                        totalMass += comp.TendonMass;
                        for (int i = 0; i < PhysicalBridge.CriticalCrossSections.Count(); i++) totalForce[i] += discreteTendon.GetTendonForce(PhysicalBridge.CriticalCrossSections[i]);

                        List<double> moments = Network.Predict(discreteTendon);
                        totalMoment[0] += comp.NumberOfTendons * comp.NumberOfStrands * moments[0];
                        totalMoment[1] += comp.NumberOfTendons * comp.NumberOfStrands * moments[1];
                        totalMoment[2] += comp.NumberOfTendons * comp.NumberOfStrands * moments[2];

                        string str = "Full tendon\t";
                        str += string.Format("{0:0.000}\t{1:0.000}\t{2:0.000}\t{3:0.000}\t{4:0.000}\t{5:0}\t{6:0}\t{7:0.000}\t{8:0.00000}\t{9:0.000}\t{10:0.0}\t{11:0.0}", tendon.PointA.Z, tendon.PointB.X, tendon.PointB.Z, tendon.PointC.Z, tendon.SupportRadius, comp.NumberOfTendons, comp.NumberOfStrands, tendon.PrestressForce, tendon.TendonArea, discreteTendon.GetTendonLength(PhysicalBridge.SpanLength.First() + PhysicalBridge.SpanLength.Last()), comp.TendonMass, totalMass);
                        for (int i = 0; i < PhysicalBridge.CriticalCrossSections.Count(); i++) str += string.Format("\t{0:0.0}\t{1:0.0}\t{2:0.0}\t{3:0.0}\t{4:0.0}\t{5:0.0}", discreteTendon.GetTendonForce(PhysicalBridge.CriticalCrossSections[i]) / (comp.NumberOfTendons * comp.NumberOfStrands), discreteTendon.GetTendonForce(PhysicalBridge.CriticalCrossSections[i]), totalForce[i], moments[i], comp.NumberOfTendons * comp.NumberOfStrands * moments[i], totalMoment[i]);

                        finalTendonFile.WriteLine(str);
                    }
                    foreach (TendonOptimizationResultComponent comp in Result.PartialTendons)
                    {
                        PartialTendon tendon = comp.PartialTendon;
                        DiscreteTendon discreteTendon = new DiscreteTendon(tendon, 0);

                        totalMass += comp.TendonMass;
                        for (int i = 0; i < PhysicalBridge.CriticalCrossSections.Count(); i++)
                        {
                            if (PhysicalBridge.CriticalCrossSections[i] == PhysicalBridge.SpanLength.First()) totalForce[i] += 2 * discreteTendon.GetTendonForce(PhysicalBridge.CriticalCrossSections[i]);
                            else totalForce[i] += discreteTendon.GetTendonForce(PhysicalBridge.CriticalCrossSections[i]);
                        }

                        List<double> moments = Network.Predict(discreteTendon);
                        totalMoment[0] += comp.NumberOfTendons * comp.NumberOfStrands * moments[0];
                        totalMoment[1] += comp.NumberOfTendons * comp.NumberOfStrands * moments[1];
                        totalMoment[2] += comp.NumberOfTendons * comp.NumberOfStrands * moments[2];

                        string str = "Partial tendon\t";
                        str += string.Format("{0:0.000}\t{1:0.000}\t{2:0.000}\t{3:0.000}\t{4:0.000}\t{5:0}\t{6:0}\t{7:0.000}\t{8:0.00000}\t{9:0.000}\t{10:0.0}\t{11:0.0}\t", tendon.PointA.Z, tendon.PointB.X, tendon.PointB.Z, tendon.PointC.Z, tendon.SupportRadius, comp.NumberOfTendons, comp.NumberOfStrands, tendon.PrestressForce / (comp.NumberOfTendons * comp.NumberOfStrands), tendon.TendonArea, discreteTendon.GetTendonLength(PhysicalBridge.SpanLength.First() + PhysicalBridge.SpanLength.Last()), comp.TendonMass, totalMass) + "\t";
                        for (int i = 0; i < PhysicalBridge.CriticalCrossSections.Count(); i++)
                        {
                            if (PhysicalBridge.CriticalCrossSections[i] == PhysicalBridge.SpanLength.First()) str += string.Format("\t{0:0.0}\t{1:0.0}\t{2:0.0}\t{3:0.0}\t{4:0.0}\t{5:0.0}", 2 * discreteTendon.GetTendonForce(PhysicalBridge.CriticalCrossSections[i]) / (comp.NumberOfTendons * comp.NumberOfStrands), 2 * discreteTendon.GetTendonForce(PhysicalBridge.CriticalCrossSections[i]), totalForce[i], moments[i], comp.NumberOfTendons * comp.NumberOfStrands * moments[i], totalMoment[i]);
                            else str += string.Format("\t{0:0.0}\t{1:0.0}\t{2:0.0}\t{3:0.0}\t{4:0.0}\t{5:0.0}", discreteTendon.GetTendonForce(PhysicalBridge.CriticalCrossSections[i]) / (comp.NumberOfTendons * comp.NumberOfStrands), discreteTendon.GetTendonForce(PhysicalBridge.CriticalCrossSections[i]), totalForce[i], moments[i], comp.NumberOfTendons * comp.NumberOfStrands * moments[i], totalMoment[i]);
                        }
                        finalTendonFile.WriteLine(str);
                    }
                    finalTendonFile.WriteLine("\nSCR:");
                    foreach (TendonOptimizationResultComponent comp in Result.FullTendons) finalTendonFile.WriteLine(comp.FullTendon.ToScr());
                    foreach (TendonOptimizationResultComponent comp in Result.PartialTendons) finalTendonFile.WriteLine(comp.PartialTendon.ToScr());

                    finalTendonFile.WriteLine(string.Format("\nTotal mass:\t{0:0.0}", Result.Mass));
                }
            }
        }

        public TendonOptimizationResult RunTendonOptimization(int maxNumberOfLayers = 1)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            Runs += 1;

            //List<double> outputs = fullTendonNetwork.Predict(new double[] { 1.000, 15.000, 0.365, 1.200, -10.000 });
            int predictionInputs = Network.NumberOfInputs;
            double zc = 0.5 * (0.5 * PhysicalBridge.LeftGirderCrossSection.Height + 0.5 * PhysicalBridge.RightGirderCrossSection.Height); //(Math.Abs(PhysicalBridge.LeftGirderCrossSection.Boundaries.Bottom) + Math.Abs(PhysicalBridge.RightGirderCrossSection.Boundaries.Bottom));
            //Boundaries: minimal and maximal abscissa of tendons inflection point B (middle span)
            double minMiddleSpanX = 0.001 * double.Parse(PhysicalBridge.DataForm.DataFormMinSpanX.Text);
            double maxMiddleSpanX = Math.Round(Math.Min(PhysicalBridge.SpanLength.First(), PhysicalBridge.SpanLength.Last()) - 0.001 * double.Parse(PhysicalBridge.DataForm.DataFormMaxSpanX.Text), 3);
            //Boundaries: minimal and maximal ordinate of tendons inflection point B (middle span)
            double minMiddleSpanZ = 0.001 * double.Parse(PhysicalBridge.DataForm.DataFormMinSpanZ.Text);
            double maxMiddleSpanZ = Math.Round(0.5 * Math.Min(Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("HL").AsDouble()), Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("HP").AsDouble())) + 0.001 * double.Parse(PhysicalBridge.DataForm.DataFormMaxSpanZ1.Text), 3);
            //Boundaries: minimal and maximal ordinate of tendons at the support axis
            double minSupportZ = 0.001 * double.Parse(PhysicalBridge.DataForm.DataFormMinSupportZ1.Text);
            if (PhysicalBridge.DataForm.DataFormMinSupportZCheckBox.Checked == true) minSupportZ = Math.Round(Math.Max(minSupportZ, double.Parse(PhysicalBridge.DataForm.DataFormMinSupportZ2.Text) * Math.Min(Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("HL").AsDouble()), Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("HP").AsDouble()))), 3);
            double maxSupportZ = Math.Round(Math.Min(Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("HL").AsDouble()), Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("HP").AsDouble())) - 0.001 * double.Parse(PhysicalBridge.DataForm.DataFormMaxSupportZ.Text), 3);
            //Boundaries: minimal and maximal radius of tendons curvature at the support axis
            double minSupportRadius = -0.001 * double.Parse(PhysicalBridge.DataForm.DataFormMaxSupportRadius.Text);
            double maxSupportRadius = -0.001 * double.Parse(PhysicalBridge.DataForm.DataFormMinSupportRadius.Text);

            //Boundaries space assurance:
            maxMiddleSpanZ = Math.Max(maxMiddleSpanZ, minMiddleSpanZ + 0.250);
            minSupportZ = Math.Min(minSupportZ, maxSupportZ - 0.250);

            int numberOfTendons = maxNumberOfLayers;
            int numberOfFullTendons = numberOfTendons;

            //Initial boundaries:
            double[] initMinBoundaries = new double[]
            {
                minMiddleSpanX,
                minMiddleSpanZ,
                minSupportZ,
                minSupportRadius,
                1,
                12
            };
            double[] initMaxBoundaries = new double[]
            {
                maxMiddleSpanX,
                maxMiddleSpanZ,
                maxSupportZ,
                maxSupportRadius,
                5,
                31
            };
            //Boundaries:
            double[] minBoundaries = new double[]
            {
                minMiddleSpanX,
                minMiddleSpanZ,
                minSupportZ,
                minSupportRadius,
                1,
                12
            };
            double[] maxBoundaries = new double[]
            {
                maxMiddleSpanX,
                maxMiddleSpanZ,
                maxSupportZ,
                maxSupportRadius,
                5,
                31
            };
            int numberOfInputs = minBoundaries.Length;

            double width = Math.Min(PhysicalBridge.LeftGirderCrossSection.Width, PhysicalBridge.RightGirderCrossSection.Width) - 2 * 0.100;
            initMaxBoundaries[numberOfInputs - 2] = (int) Math.Ceiling(0.5 * width / 0.100);
            maxBoundaries[numberOfInputs - 2] = (int) Math.Ceiling(0.5 * width / 0.100);
            if (maxNumberOfLayers > 1)
            {
                List<double> initMinBoundariesList = initMinBoundaries.ToList(); List<double> minBoundariesList = minBoundaries.ToList();
                List<double> initMaxBoundariesList = initMaxBoundaries.ToList(); List<double> maxBoundariesList = maxBoundaries.ToList();
                for (int i = 0; i < maxNumberOfLayers - 1; i++)
                {
                    initMinBoundariesList.AddRange(initMinBoundaries.Take(numberOfInputs));
                    initMaxBoundariesList.AddRange(initMaxBoundaries.Take(numberOfInputs));
                    minBoundariesList.AddRange(minBoundaries.Take(numberOfInputs));
                    maxBoundariesList.AddRange(maxBoundaries.Take(numberOfInputs));
                }
                initMinBoundaries = initMinBoundariesList.ToArray(); minBoundaries = minBoundariesList.ToArray();
                initMaxBoundaries = initMaxBoundariesList.ToArray(); maxBoundaries = maxBoundariesList.ToArray();

                for (int i = 1; i < maxNumberOfLayers; i++)
                {
                    initMinBoundaries[1 + numberOfInputs * i] = initMinBoundaries[1 + numberOfInputs * (i - 1)] + 0.200;
                    initMaxBoundaries[1 + numberOfInputs * i] = initMaxBoundaries[1 + numberOfInputs * (i - 1)] + 0.200;

                    minBoundaries[1 + numberOfInputs * i] = minBoundaries[1 + numberOfInputs * (i - 1)] + 0.200;
                    maxBoundaries[1 + numberOfInputs * i] = maxBoundaries[1 + numberOfInputs * (i - 1)] + 0.200;
                }
                for (int i = maxNumberOfLayers - 2; i >= 0; i--)
                {
                    initMaxBoundaries[2 + numberOfInputs * i] = initMaxBoundaries[2 + numberOfInputs * (i + 1)] - 0.200;
                    initMinBoundaries[2 + numberOfInputs * i] = initMinBoundaries[2 + numberOfInputs * (i + 1)] - 0.200;

                    maxBoundaries[2 + numberOfInputs * i] = maxBoundaries[2 + numberOfInputs * (i + 1)] - 0.200;
                    minBoundaries[2 + numberOfInputs * i] = minBoundaries[2 + numberOfInputs * (i + 1)] - 0.200;
                }
            }

            //Cross section parameters:
            double crossSectionBottom; double crossSectionBottomModulus;
            double crossSectionTop; double crossSectionTopModulus;

            double[] crossSectionArea = new double[] { PhysicalBridge.LeftGirderCrossSection.Area, PhysicalBridge.RightGirderCrossSection.Area };
            double[][] crossSectionModules = new double[Enum.GetNames(typeof(Girders)).Length][];

            crossSectionBottom = PhysicalBridge.LeftGirderCrossSection.Boundaries.Bottom; crossSectionBottomModulus = PhysicalBridge.LeftGirderCrossSection.MomentsOfInertia.IX / crossSectionBottom;
            crossSectionTop = (PhysicalBridge.LeftGirderCrossSection.Height + PhysicalBridge.LeftGirderCrossSection.Boundaries.Bottom); crossSectionTopModulus = PhysicalBridge.LeftGirderCrossSection.MomentsOfInertia.IX / crossSectionTop;
            crossSectionModules[(int) Girders.LeftGirder] = new double[] { crossSectionBottomModulus, crossSectionBottomModulus, crossSectionTopModulus, crossSectionTopModulus };

            crossSectionBottom = PhysicalBridge.RightGirderCrossSection.Boundaries.Bottom; crossSectionBottomModulus = PhysicalBridge.RightGirderCrossSection.MomentsOfInertia.IX / crossSectionBottom;
            crossSectionTop = (PhysicalBridge.RightGirderCrossSection.Height + PhysicalBridge.RightGirderCrossSection.Boundaries.Bottom); crossSectionTopModulus = PhysicalBridge.RightGirderCrossSection.MomentsOfInertia.IX / crossSectionTop;
            crossSectionModules[(int) Girders.RightGirder] = new double[] { crossSectionBottomModulus, crossSectionBottomModulus, crossSectionTopModulus, crossSectionTopModulus };

            OptimizationPhase[][] phases = new OptimizationPhase[Enum.GetNames(typeof(Girders)).Length][];
            for (int girder = 0; girder < Enum.GetNames(typeof(Girders)).Length; girder++)
            {
                phases[girder] = new OptimizationPhase[Enum.GetNames(typeof(Phases)).Length];
                for (int phase = 0; phase < Enum.GetNames(typeof(Phases)).Length; phase++)
                {
                    List<double> minMoments = new List<double>();
                    List<double> maxMoments = new List<double>();
                    foreach (double x in PhysicalBridge.CriticalCrossSections)
                    {
                        minMoments.Add(Envelopes.GetGirder(girder).GetCombination(Combinations.DesignCombination).GetPhase(phase).MY.GetMinForces(x).MY);
                        minMoments.Add(Envelopes.GetGirder(girder).GetCombination(Combinations.RareCombination).GetPhase(phase).MY.GetMinForces(x).MY);
                        minMoments.Add(Envelopes.GetGirder(girder).GetCombination(Combinations.FrequentCombination).GetPhase(phase).MY.GetMinForces(x).MY);
                        minMoments.Add(Envelopes.GetGirder(girder).GetCombination(Combinations.QuasiPermanentCombination).GetPhase(phase).MY.GetMinForces(x).MY);

                        maxMoments.Add(Envelopes.GetGirder(girder).GetCombination(Combinations.DesignCombination).GetPhase(phase).MY.GetMaxForces(x).MY);
                        maxMoments.Add(Envelopes.GetGirder(girder).GetCombination(Combinations.RareCombination).GetPhase(phase).MY.GetMaxForces(x).MY);
                        maxMoments.Add(Envelopes.GetGirder(girder).GetCombination(Combinations.FrequentCombination).GetPhase(phase).MY.GetMaxForces(x).MY);
                        maxMoments.Add(Envelopes.GetGirder(girder).GetCombination(Combinations.QuasiPermanentCombination).GetPhase(phase).MY.GetMaxForces(x).MY);
                    }
                    phases[girder][phase] = new OptimizationPhase(PhysicalBridge.CriticalCrossSections.Count(), minMoments, maxMoments);
                }
            }

            int populationSize = int.Parse(PhysicalBridge.DataForm.DataFormTendonPopulationSize.Text);
            PrestressingChromosome chromosome = new PrestressingChromosome(initMinBoundaries, initMaxBoundaries, minBoundaries, maxBoundaries);
            Population population = new Population(populationSize, populationSize, chromosome);

            ForceSet[] prestressingForces = new ForceSet[Enum.GetNames(typeof(SectionData)).Length];
            prestressingForces[(int) SectionData.SpanSection] = new ForceSet(0.0, 0.0, 0.0);
            prestressingForces[(int) SectionData.MidspanSection] = new ForceSet(0.0, 0.0, 0.0);
            prestressingForces[(int) SectionData.SupportSection] = new ForceSet(0.0, 0.0, 0.0);

            double rinf = 0.90; double rsup = 1.10;
            int generationNumber = 1;

#if TENDONDEF
            string[] tendonDef = new string[Enum.GetNames(typeof(Girders)).Length];
            tendonDef[(int) Girders.LeftGirder] = "";
            tendonDef[(int) Girders.RightGirder] = "";
#endif

#if TENDONCHK
            string[] tendonChk = new string[Enum.GetNames(typeof(Girders)).Length];
            tendonChk[(int) Girders.LeftGirder] = "";
            tendonChk[(int) Girders.RightGirder] = "";
#endif

#if TENDONSCR
            string tendonScr = "";
#endif
            //Fitness function:
            var fitness = new FuncFitness((c) =>
            {
                prestressingForces[(int) SectionData.SpanSection].FX = 0.0;
                prestressingForces[(int) SectionData.SpanSection].MY = 0.0;

                prestressingForces[(int) SectionData.MidspanSection].FX = 0.0;
                prestressingForces[(int) SectionData.MidspanSection].MY = 0.0;

                prestressingForces[(int) SectionData.SupportSection].FX = 0.0;
                prestressingForces[(int) SectionData.SupportSection].MY = 0.0;

                PrestressingChromosome prestressingChromosome = c as PrestressingChromosome;

                double[][] tendonParamSets = new double[numberOfTendons][];
                List<DiscreteTendon> tendons = new List<DiscreteTendon>();

                //Repairing gene to mantain int[] numberOfStrands array:
                for (int i = 0; i < numberOfTendons; i++)
                {
                    var strands = from num in NumberOfStrands select new { num, difference = Math.Abs((double) prestressingChromosome.GetGene(i * (predictionInputs + 1) + 5).Value - num) };
                    var strandsCorr = (from correction in strands orderby correction.difference select correction).First().num;
                    if ((double) prestressingChromosome.GetGene(i * (predictionInputs + 1) + 5).Value < NumberOfStrands[0]) strandsCorr = 0;

                    prestressingChromosome.ReplaceGene(i * (predictionInputs + 1) + 4, new Gene(Math.Round((double) prestressingChromosome.GetGene(i * (predictionInputs + 1) + 4).Value)));
                    prestressingChromosome.ReplaceGene(i * (predictionInputs + 1) + 5, new Gene((double) strandsCorr));
                }
                double anchorageZ = ResolveAnchorageOrdinate(zc, prestressingChromosome.GetGenes().Where((g, i) => (i % 4 == 0) && (i > 0)).Select(g => (double) g.Value).ToArray(), prestressingChromosome.GetGenes().Where((g, i) => (i % 5 == 0) && (i > 0)).Select(g => (double) g.Value).ToArray());

                //Calculating total weight of the actuall prestressing system, resolving internal forces due to prestress:
                double totalWeight = 0;

                bool printout = true;
#if LIMITED
                if (generationNumber <= 10) printout = true;
                else if (generationNumber % 20 == 0) printout = true;
                else printout = false;
#endif
                for (int i = 0; i < numberOfTendons; i++)
                {
                    tendonParamSets[i] = new double[predictionInputs + 2];
                    tendonParamSets[i][0] = anchorageZ + i * 0.350;

                    int index = 1;
                    foreach (double element in prestressingChromosome.GetGenes().Skip(i * (predictionInputs + 1)).Take(predictionInputs + 1).Select(g => (double) g.Value).ToArray())
                    {
                        tendonParamSets[i][index] = element;
                        index++;
                    }

                    //Full tendons definition:
                    if (i < numberOfFullTendons)
                    {
                        FullTendon tendon = new FullTendon(0.0, tendonParamSets[i][0], tendonParamSets[i][1], tendonParamSets[i][2], PhysicalBridge.SpanLength.First(), tendonParamSets[i][3], PhysicalBridge.SpanLength.First() + (PhysicalBridge.SpanLength.First() - tendonParamSets[i][1]), tendonParamSets[i][2], PhysicalBridge.SpanLength.First() + PhysicalBridge.SpanLength.Last(), tendonParamSets[i][0], 2.0, 2.0, tendonParamSets[i][4])
                        {
                            PrestressForce = InitialPrestressForce
                        };
                        tendons.Add(new DiscreteTendon(tendon, zc, PhysicalBridge.CriticalCrossSections));
                        totalWeight += tendonParamSets[i][5] * tendonParamSets[i][6] * 0.000150 * tendons.Last().GetTendonLength(PhysicalBridge.SpanLength.First() + PhysicalBridge.SpanLength.Last()) * 7850; //kg

                        if (printout)
                        {
#if TENDONDEF
                            tendonDef[(int) Girders.LeftGirder] += "Full tendon\t";
                            tendonDef[(int) Girders.RightGirder] += "Full tendon\t";
#endif
#if TENDONCHK
                            tendonChk[(int) Girders.LeftGirder] += "Full tendon";
                            tendonChk[(int) Girders.RightGirder] += "Full tendon";
#endif
#if TENDONSCR
                            tendonScr += tendon.ToScr(1000) + "\n";
#endif                  
                        }
                    }
                    //Partial tendons definition:
                    else
                    {
                        PartialTendon tendon = new PartialTendon(0.0, tendonParamSets[i][0], tendonParamSets[i][1], tendonParamSets[i][2], PhysicalBridge.SpanLength.First(), tendonParamSets[i][3], 2.0, 6.0, tendonParamSets[i][4])
                        {
                            PrestressForce = InitialPrestressForce
                        };
                        tendons.Add(new DiscreteTendon(tendon, zc, PhysicalBridge.CriticalCrossSections));
                        totalWeight += 2 * tendonParamSets[i][5] * tendonParamSets[i][6] * 0.000150 * tendons.Last().GetTendonLength(PhysicalBridge.SpanLength.First() + 6.0) * 7850; //kg

                        if (printout)
                        {
#if TENDONDEF
                            tendonDef[(int) Girders.LeftGirder] += "Partial tendon\t";
                            tendonDef[(int) Girders.RightGirder] += "Partial tendon\t";
#endif
#if TENDONCHK
                            tendonChk[(int) Girders.LeftGirder] += "Partial tendon";
                            tendonChk[(int) Girders.RightGirder] += "Partial tendon";
#endif
#if TENDONSCR
                            tendonScr += tendon.ToScr(1000) + "\n";
#endif
                        }
                    }

                    List<double> moments = new List<double>();
                    prestressingForces[(int) SectionData.SpanSection].FX += tendonParamSets[i][5] * tendonParamSets[i][6] * tendons.Last().GetTendonForce(PhysicalBridge.CriticalCrossSections[(int) SectionData.SpanSection]);
                    prestressingForces[(int) SectionData.MidspanSection].FX += tendonParamSets[i][5] * tendonParamSets[i][6] * tendons.Last().GetTendonForce(PhysicalBridge.CriticalCrossSections[(int) SectionData.MidspanSection]);
                    if (i < numberOfFullTendons)
                    {
                        prestressingForces[(int) SectionData.SupportSection].FX += tendonParamSets[i][5] * tendonParamSets[i][6] * tendons.Last().GetTendonForce(PhysicalBridge.CriticalCrossSections[(int) SectionData.SupportSection]);
                        moments = Network.Predict(tendonParamSets[i].Take(predictionInputs).ToArray());
                    }
                    else
                    {
                        prestressingForces[(int) SectionData.SupportSection].FX += 2 * tendonParamSets[i][5] * tendonParamSets[i][6] * tendons.Last().GetTendonForce(PhysicalBridge.CriticalCrossSections[(int) SectionData.SupportSection]);
                        moments = Network.Predict(tendonParamSets[i].Take(predictionInputs).ToArray());
                    }
                    prestressingForces[(int) SectionData.SpanSection].MY += tendonParamSets[i][5] * tendonParamSets[i][6] * moments[0];
                    prestressingForces[(int) SectionData.MidspanSection].MY += tendonParamSets[i][5] * tendonParamSets[i][6] * moments[1];
                    prestressingForces[(int) SectionData.SupportSection].MY += tendonParamSets[i][5] * tendonParamSets[i][6] * moments[2];

                    if (printout)
                    {
#if TENDONDEF
                        for (int girder = 0; girder < Enum.GetNames(typeof(Girders)).Length; girder++)
                        {
                            if (i < numberOfFullTendons) tendonDef[girder] += string.Format("{0:0.000}\t{1:0.000}\t{2:0.000}\t{3:0.000}\t{4:0.000}\t{5:0}\t{6:0}\t{7:0.000}\t{8:0.00000}\t{9:0.000}\t{10:0.0}\t{11:0.0}\t", tendonParamSets[i][0], tendonParamSets[i][1], tendonParamSets[i][2], tendonParamSets[i][3], tendonParamSets[i][4], tendonParamSets[i][5], tendonParamSets[i][6], tendons.Last().InitialForce, tendonParamSets[i][5] * tendonParamSets[i][6] * 0.000150, tendons.Last().GetTendonLength(PhysicalBridge.SpanLength.First() + PhysicalBridge.SpanLength.Last()), tendonParamSets[i][5] * tendonParamSets[i][6] * 0.000150 * tendons.Last().GetTendonLength(PhysicalBridge.SpanLength.First() + PhysicalBridge.SpanLength.Last()) * 7850, totalWeight);
                            else tendonDef[girder] += string.Format("{0:0.000}\t{1:0.000}\t{2:0.000}\t{3:0.000}\t{4:0.000}\t{5:0}\t{6:0}\t{7:0.00000}\t{8:0.000}\t{9:0.0}\t{10:0.0}\t{11:0.0}\t", tendonParamSets[i][0], tendonParamSets[i][1], tendonParamSets[i][2], tendonParamSets[i][3], tendonParamSets[i][4], tendonParamSets[i][5], tendonParamSets[i][6], tendons.Last().InitialForce, tendonParamSets[i][5] * tendonParamSets[i][6] * 0.000150, tendons.Last().GetTendonLength(PhysicalBridge.SpanLength.First() + 6.0), 2 * tendonParamSets[i][5] * tendonParamSets[i][6] * 0.000150 * tendons.Last().GetTendonLength(PhysicalBridge.SpanLength.First() + 6.0) * 7850, totalWeight);
                            //Internal prestressing forces, span:
                            tendonDef[girder] += string.Format("{0:0.0}\t{1:0.0}\t{2:0.0}\t{3:0.0}\t{4:0.0}\t{5:0.0}", tendons.Last().GetTendonForce(PhysicalBridge.CriticalCrossSections[0]), tendonParamSets[i][5] * tendonParamSets[i][6] * tendons.Last().GetTendonForce(PhysicalBridge.CriticalCrossSections[0]), prestressingForces[(int) SectionData.SpanSection].FX, moments[0], tendonParamSets[i][5] * tendonParamSets[i][6] * moments[0], prestressingForces[(int) SectionData.SpanSection].MY) + "\t";
                            //Internal prestressing forces, midspan:
                            tendonDef[girder] += string.Format("{0:0.0}\t{1:0.0}\t{2:0.0}\t{3:0.0}\t{4:0.0}\t{5:0.0}", tendons.Last().GetTendonForce(PhysicalBridge.CriticalCrossSections[1]), tendonParamSets[i][5] * tendonParamSets[i][6] * tendons.Last().GetTendonForce(PhysicalBridge.CriticalCrossSections[1]), prestressingForces[(int) SectionData.MidspanSection].FX, moments[1], tendonParamSets[i][5] * tendonParamSets[i][6] * moments[1], prestressingForces[(int) SectionData.MidspanSection].MY) + "\t";
                            //Internal prestressing forces, support:        
                            tendonDef[girder] += string.Format("{0:0.0}\t{1:0.0}\t{2:0.0}\t{3:0.0}\t{4:0.0}\t{5:0.0}", tendons.Last().GetTendonForce(PhysicalBridge.CriticalCrossSections[2]), tendonParamSets[i][5] * tendonParamSets[i][6] * tendons.Last().GetTendonForce(PhysicalBridge.CriticalCrossSections[2]), prestressingForces[(int) SectionData.SupportSection].FX, moments[2], tendonParamSets[i][5] * tendonParamSets[i][6] * moments[2], prestressingForces[(int) SectionData.SupportSection].MY);

                            if (i < numberOfTendons - 1) tendonDef[girder] += "\n";
                        }
#endif

#if (TENDONCHK && (TENDONDEF || TENDONSCR))
                        if (i < numberOfTendons - 1)
                        {
                            tendonChk[(int) Girders.LeftGirder] += "\n";
                            tendonChk[(int) Girders.RightGirder] += "\n";
                        }
#endif
                    }
                }

                double[][][] prestressingStresses = new double[Enum.GetNames(typeof(Girders)).Length][][];
                for (int girder = 0; girder < Enum.GetNames(typeof(Girders)).Length; girder++)
                {
                    prestressingStresses[girder] = new double[Enum.GetNames(typeof(SectionData)).Length][];
                    for (int section = 0; section < Enum.GetNames(typeof(SectionData)).Length; section++)
                    {
                        prestressingStresses[girder][section] = new double[Enum.GetNames(typeof(StressData)).Length];
                        for (int stress = 0; stress < Enum.GetNames(typeof(StressData)).Length; stress++)
                        {
                            prestressingStresses[girder][section][stress] = 0.001 * (prestressingForces[section].FX / crossSectionArea[girder] + prestressingForces[section].MY / crossSectionModules[girder][stress]);
                        }
                    }

                    if (printout)
                    {
#if TENDONDEF
                        tendonDef[girder] += string.Format("\t{0:0.0000}\t{1:0.0000}\t{2:0.0000}\t{3:0.0}\t{4:0.0}\t{5:0.0}\t{6:0.0}\t{7:0.0}\t{8:0.0}\t", crossSectionArea[girder], crossSectionModules[girder][(int) StressData.BottomMax], crossSectionModules[girder][(int) StressData.TopMax], prestressingStresses[girder][(int) SectionData.SpanSection][(int) StressData.BottomMax], prestressingStresses[girder][(int) SectionData.SpanSection][(int) StressData.TopMax], prestressingStresses[girder][(int) SectionData.MidspanSection][(int) StressData.BottomMax], prestressingStresses[girder][(int) SectionData.MidspanSection][(int) StressData.TopMax], prestressingStresses[girder][(int) SectionData.SupportSection][(int) StressData.BottomMax], prestressingStresses[girder][(int) SectionData.SupportSection][(int) StressData.TopMax]);
#endif
                    }
                }

                //Tendons collision check:
                int collisions = 0;
                if (numberOfTendons > 1)
                {
                    collisions = CollisionCheck(tendons, 0.200, 0.350);
                }

                if (printout)
                {
#if TENDONDEF
                    tendonDef[(int) Girders.LeftGirder] += collisions + "\n";
                    tendonDef[(int) Girders.RightGirder] += collisions + "\n";
#endif
                }

                //Rare stresses, for max compression: sigma < 0.60 fck
                double[][][][] rareStresses = new double[Enum.GetNames(typeof(Girders)).Length][][][];
                for (int girder = 0; girder < Enum.GetNames(typeof(Girders)).Length; girder++)
                {
                    rareStresses[girder] = new double[Enum.GetNames(typeof(Phases)).Length][][];
                    for (int phase = 0; phase < Enum.GetNames(typeof(Phases)).Length; phase++)
                    {
                        double m = 1.00;
                        if (phase == (int) Phases.OperationalPhase) m = 0.85;

                        rareStresses[girder][phase] = new double[Enum.GetNames(typeof(SectionData)).Length][];
                        for (int section = 0; section < Enum.GetNames(typeof(SectionData)).Length; section++)
                        {
                            rareStresses[girder][phase][section] = new double[Enum.GetNames(typeof(StressData)).Length];
                            for (int stress = 0; stress < Enum.GetNames(typeof(StressData)).Length; stress++)
                            {
                                if (stress == (int) StressData.BottomMax || stress == (int) StressData.TopMax) rareStresses[girder][phase][section][stress] = m * Math.Max(rinf * prestressingStresses[girder][section][stress], rsup * prestressingStresses[girder][section][stress]) + 0.001 * Math.Max(phases[girder][phase].Sections[section].RareCombination.Max / crossSectionModules[girder][stress], phases[girder][phase].Sections[section].RareCombination.Min / crossSectionModules[girder][stress]);
                                else rareStresses[girder][phase][section][stress] = m * Math.Min(rinf * prestressingStresses[girder][section][stress], rsup * prestressingStresses[girder][section][stress]) + 0.001 * Math.Min(phases[girder][phase].Sections[section].RareCombination.Max / crossSectionModules[girder][stress], phases[girder][phase].Sections[section].RareCombination.Min / crossSectionModules[girder][stress]);
                            }
                        }
                    }
                }

                //Frequent stress, for max tension: sigma > 0.0 MPa
                double[][][][] frequentStresses = new double[Enum.GetNames(typeof(Girders)).Length][][][];
                for (int girder = 0; girder < Enum.GetNames(typeof(Girders)).Length; girder++)
                {
                    frequentStresses[girder] = new double[Enum.GetNames(typeof(Phases)).Length][][];
                    for (int phase = 0; phase < Enum.GetNames(typeof(Phases)).Length; phase++)
                    {
                        double m = 1.00;
                        if (phase == (int) Phases.OperationalPhase) m = 0.85;

                        frequentStresses[girder][phase] = new double[Enum.GetNames(typeof(SectionData)).Length][];
                        for (int section = 0; section < Enum.GetNames(typeof(SectionData)).Length; section++)
                        {
                            frequentStresses[girder][phase][section] = new double[Enum.GetNames(typeof(StressData)).Length];
                            for (int stress = 0; stress < Enum.GetNames(typeof(StressData)).Length; stress++)
                            {
                                if (stress == (int) StressData.BottomMax || stress == (int) StressData.TopMax) frequentStresses[girder][phase][section][stress] = m * Math.Max(rinf * prestressingStresses[girder][section][stress], rsup * prestressingStresses[girder][section][stress]) + 0.001 * Math.Max(phases[girder][phase].Sections[section].FrequentCombination.Max / crossSectionModules[girder][stress], phases[girder][phase].Sections[section].FrequentCombination.Min / crossSectionModules[girder][stress]);
                                else frequentStresses[girder][phase][section][stress] = m * Math.Min(rinf * prestressingStresses[girder][section][stress], rsup * prestressingStresses[girder][section][stress]) + 0.001 * Math.Min(phases[girder][phase].Sections[section].FrequentCombination.Max / crossSectionModules[girder][stress], phases[girder][phase].Sections[section].FrequentCombination.Min / crossSectionModules[girder][stress]);
                            }
                        }
                    }
                }

                //Quasi - permanent stress, for max compression: sigma < 0.45 fck
                double[][][][] quasiPermanentStresses = new double[Enum.GetNames(typeof(Girders)).Length][][][];
                for (int girder = 0; girder < Enum.GetNames(typeof(Girders)).Length; girder++)
                {
                    quasiPermanentStresses[girder] = new double[Enum.GetNames(typeof(Phases)).Length][][];
                    for (int phase = 0; phase < Enum.GetNames(typeof(Phases)).Length; phase++)
                    {
                        double m = 1.00;
                        if (phase == (int) Phases.OperationalPhase) m = 0.85;

                        quasiPermanentStresses[girder][phase] = new double[Enum.GetNames(typeof(SectionData)).Length][];
                        for (int section = 0; section < Enum.GetNames(typeof(SectionData)).Length; section++)
                        {
                            quasiPermanentStresses[girder][phase][section] = new double[Enum.GetNames(typeof(StressData)).Length];
                            for (int stress = 0; stress < Enum.GetNames(typeof(StressData)).Length; stress++)
                            {
                                if (stress == (int) StressData.BottomMax || stress == (int) StressData.TopMax) quasiPermanentStresses[girder][phase][section][stress] = m * Math.Max(rinf * prestressingStresses[girder][section][stress], rsup * prestressingStresses[girder][section][stress]) + 0.001 * Math.Max(phases[girder][phase].Sections[section].QuasiPermanentCombination.Max / crossSectionModules[girder][stress], phases[girder][phase].Sections[section].QuasiPermanentCombination.Min / crossSectionModules[girder][stress]);
                                else quasiPermanentStresses[girder][phase][section][stress] = m * Math.Min(rinf * prestressingStresses[girder][section][stress], rsup * prestressingStresses[girder][section][stress]) + 0.001 * Math.Min(phases[girder][phase].Sections[section].QuasiPermanentCombination.Max / crossSectionModules[girder][stress], phases[girder][phase].Sections[section].QuasiPermanentCombination.Min / crossSectionModules[girder][stress]);
                            }
                        }
                    }
                }

                int[][] exceeds = new int[Enum.GetNames(typeof(Girders)).Length][];
                exceeds[(int) Girders.LeftGirder] = new int[Enum.GetNames(typeof(Combinations)).Length - 1];
                exceeds[(int) Girders.RightGirder] = new int[Enum.GetNames(typeof(Combinations)).Length - 1];

                double[] exceedance = new double[Enum.GetNames(typeof(Combinations)).Length - 1];

                //Rare stresses, for max compression: sigma < 0.60 fck
                for (int girder = 0; girder < Enum.GetNames(typeof(Girders)).Length; girder++)
                {
                    exceeds[girder][(int) Combinations.RareCombination - 1] = 0;
                    for (int phase = 0; phase < Enum.GetNames(typeof(Phases)).Length; phase++)
                    {
                        for (int section = 0; section < Enum.GetNames(typeof(SectionData)).Length; section++)
                        {
                            for (int stress = 0; stress < Enum.GetNames(typeof(StressData)).Length; stress++)
                            {
#if TENDONCHK
                                if (printout) tendonChk[girder] += string.Format("\t{0:0.0}", rareStresses[girder][phase][section][stress]);
#endif
                                if (Math.Round(rareStresses[girder][phase][section][stress], 1) > 0.6 * PhysicalBridge.Concrete.CompressiveStrength)
                                {
                                    exceeds[girder][(int) Combinations.RareCombination - 1]++;
                                    exceedance[(int) Combinations.RareCombination - 1] = Math.Max(Math.Round(rareStresses[girder][phase][section][stress] - 0.6 * PhysicalBridge.Concrete.CompressiveStrength, 1), exceedance[(int) Combinations.RareCombination - 1]);
                                }
                            }
                        }
                    }
#if TENDONCHK
                    if (printout) tendonChk[girder] += string.Format("\t{0:0}", exceeds[girder][(int) Combinations.RareCombination - 1]);
#endif
                }

                //Frequent stress, for max tension: sigma > 0.0 MPa
                for (int girder = 0; girder < Enum.GetNames(typeof(Girders)).Length; girder++)
                {
                    exceeds[girder][(int) Combinations.FrequentCombination - 1] = 0;
                    for (int phase = 0; phase < Enum.GetNames(typeof(Phases)).Length; phase++)
                    {
                        for (int section = 0; section < Enum.GetNames(typeof(SectionData)).Length; section++)
                        {
                            for (int stress = 0; stress < Enum.GetNames(typeof(StressData)).Length; stress++)
                            {
#if TENDONCHK
                                if (printout) tendonChk[girder] += string.Format("\t{0:0.0}", frequentStresses[girder][phase][section][stress]);
#endif
                                if (Math.Round(frequentStresses[girder][phase][section][stress], 1) < 0)
                                {
                                    exceeds[girder][(int) Combinations.FrequentCombination - 1]++;
                                    exceedance[(int) Combinations.FrequentCombination - 1] = Math.Max(Math.Round(Math.Abs(frequentStresses[girder][phase][section][stress]), 1), exceedance[(int) Combinations.FrequentCombination - 1]);
                                }
                            }
                        }
                    }
#if TENDONCHK
                    if (printout) tendonChk[girder] += string.Format("\t{0:0}", exceeds[girder][(int) Combinations.FrequentCombination - 1]);
#endif
                }

                //Quasi - permanent stress, for max compression: sigma < 0.45 fck
                for (int girder = 0; girder < Enum.GetNames(typeof(Girders)).Length; girder++)
                {
                    exceeds[girder][(int) Combinations.QuasiPermanentCombination - 1] = 0;
                    for (int phase = 0; phase < Enum.GetNames(typeof(Phases)).Length; phase++)
                    {
                        for (int section = 0; section < Enum.GetNames(typeof(SectionData)).Length; section++)
                        {
                            for (int stress = 0; stress < Enum.GetNames(typeof(StressData)).Length; stress++)
                            {
#if TENDONCHK
                                if (printout) tendonChk[girder] += string.Format("\t{0:0.0}", quasiPermanentStresses[girder][phase][section][stress]);
#endif
                                if (Math.Round(quasiPermanentStresses[girder][phase][section][stress], 1) > 0.45 * PhysicalBridge.Concrete.CompressiveStrength)
                                {
                                    exceeds[girder][(int) Combinations.QuasiPermanentCombination - 1]++;
                                    exceedance[(int) Combinations.QuasiPermanentCombination - 1] = Math.Max(Math.Round(quasiPermanentStresses[girder][phase][section][stress] - 0.45 * PhysicalBridge.Concrete.CompressiveStrength, 1), exceedance[(int) Combinations.QuasiPermanentCombination - 1]);
                                }
                            }
                        }
                    }
#if TENDONCHK
                    if (printout) tendonChk[girder] += string.Format("\t{0:0}", exceeds[girder][(int) Combinations.QuasiPermanentCombination - 1]);
#endif
                }
#if TENDONCHK
                if (printout)
                {
                    tendonChk[(int) Girders.LeftGirder] += string.Format("\t{0:0}\t{1:0.0}\t{2:0}\t", exceeds[(int) Girders.LeftGirder].Sum(), exceedance.Sum(), collisions) + "FIT" + population.CurrentGeneration.Chromosomes.IndexOf(c) + "<\n";
                    tendonChk[(int) Girders.RightGirder] += string.Format("\t{0:0}\t{1:0.0}\t{2:0}\t", exceeds[(int) Girders.RightGirder].Sum(), exceedance.Sum(), collisions) + "FIT" + population.CurrentGeneration.Chromosomes.IndexOf(c) + "<\n";
                }
#endif
                prestressingChromosome.Weight = totalWeight;
                prestressingChromosome.Exceedance[(int) Combinations.RareCombination - 1] = exceedance[(int) Combinations.RareCombination - 1];
                prestressingChromosome.Exceedance[(int) Combinations.FrequentCombination - 1] = exceedance[(int) Combinations.FrequentCombination - 1];
                prestressingChromosome.Exceedance[(int) Combinations.QuasiPermanentCombination - 1] = exceedance[(int) Combinations.QuasiPermanentCombination - 1];
                prestressingChromosome.Exceeds = exceeds[(int) Girders.LeftGirder].Sum() + exceeds[(int) Girders.RightGirder].Sum();
                prestressingChromosome.Collisions = collisions;

                if (c == population.CurrentGeneration.Chromosomes.Last())
                {
                    double minWeight = (population.CurrentGeneration.Chromosomes.First() as PrestressingChromosome).Weight;
                    foreach (PrestressingChromosome pc in population.CurrentGeneration.Chromosomes) minWeight = Math.Min(minWeight, pc.Weight);
                    foreach (PrestressingChromosome pc in population.CurrentGeneration.Chromosomes)
                    {
                        //pc.Fitness = (pc.Collisions + 1) * ((minWeight / pc.Weight - 1) - 2 * pc.Exceedance.Sum());
                        pc.Fitness = pc.Weight - 10000 * pc.Collisions - 10000 * pc.Exceeds;
                    }
                    if (printout)
                    {
#if TENDONCHK
                        foreach (PrestressingChromosome pc in population.CurrentGeneration.Chromosomes)
                        {
                            tendonChk[(int) Girders.LeftGirder] = tendonChk[(int) Girders.LeftGirder].Replace("FIT" + population.CurrentGeneration.Chromosomes.IndexOf(pc) + "<", string.Format("{0:0.000}", pc.Fitness));
                            tendonChk[(int) Girders.RightGirder] = tendonChk[(int) Girders.RightGirder].Replace("FIT" + population.CurrentGeneration.Chromosomes.IndexOf(pc) + "<", string.Format("{0:0.000}", pc.Fitness));
                        }
#endif
                    }
                    return (double) c.Fitness;
                }
                return 0.0;
            });

            TournamentSelection selection = new TournamentSelection(2, true);
            SimulatedBinaryCrossover crossover = new SimulatedBinaryCrossover(double.Parse(PhysicalBridge.DataForm.DataFormTendonCrossoverDistributionIndex.Text), minBoundaries, maxBoundaries);
            PolynomialMutation mutation = new PolynomialMutation(double.Parse(PhysicalBridge.DataForm.DataFormTendonMutationDistributionIndex.Text), minBoundaries, maxBoundaries);

            GenerationNumberTermination termination = new GenerationNumberTermination(int.Parse(PhysicalBridge.DataForm.DataFormTendonMaxGenerations.Text));

            var geneticAlgorithm = new GeneticAlgorithm(population, fitness, selection, crossover, mutation);
            geneticAlgorithm.CrossoverProbability = float.Parse(PhysicalBridge.DataForm.DataFormTendonCrossoverProbability.Text);
            geneticAlgorithm.MutationProbability = float.Parse(PhysicalBridge.DataForm.DataFormTendonMutationProbability.Text);
            geneticAlgorithm.Termination = termination;
            geneticAlgorithm.Reinsertion = new UniformReinsertion();

            geneticAlgorithm.GenerationRan += (sender, e) =>
            {
                bool append = !(geneticAlgorithm.GenerationsNumber == 1);
                bool printout = true;
#if LIMITED
                if (generationNumber <= 10) printout = true;
                else if (generationNumber % 20 == 0) printout = true;
                else printout = false;
#endif
                if (printout)
                {
#if TENDONDEF
                    for (int girder = 0; girder < Enum.GetNames(typeof(Girders)).Length; girder++)
                    {
                        using (StreamWriter tendonDefFile = new StreamWriter(PhysicalBridge.Directory + Globals.TextFiles.TendonDefFile(Runs, (Girders) Enum.GetValues(typeof(Girders)).GetValue(girder)), append))
                        {
                            if (append == false)
                            {
                                string strTitle = "\tA.Z\tB.X\tB.Z\tC.Z\tR\tN.t\tN.s\tF\tA\tL\tW\tW.tot";
                                strTitle += "\tN1\tN1.t\tN1.tot\tM1\tM1.t\tM1.tot";
                                strTitle += "\tN2\tN2.t\tN2.tot\tM2\tM2.t\tM2.tot";
                                strTitle += "\tN3\tN3.t\tN3.tot\tM3\tM3.t\tM3.tot";

                                strTitle += "\tGA\tGM.B\tGM.T";

                                strTitle += "\tS1.B\tS1.T";
                                strTitle += "\tS2.B\tS2.T";
                                strTitle += "\tS3.B\tS3.T";

                                tendonDefFile.WriteLine(strTitle + "\tC\n");
                            }
                            tendonDefFile.WriteLine("Generation:\t" + generationNumber + "\t\tPopulation size:\t" + geneticAlgorithm.Population.CurrentGeneration.Chromosomes.Count + "\n");
                            tendonDefFile.WriteLine(tendonDef[girder]);
                        }
                        tendonDef[girder] = "";
                    }
#endif

#if TENDONSCR
                    using (StreamWriter tendonScrFile = new StreamWriter(PhysicalBridge.Directory + Globals.TextFiles.TendonScrFile(Runs), append))
                    {
                        tendonScrFile.WriteLine("Generation:\t" + generationNumber + "\t\tPopulation size:\t" + geneticAlgorithm.Population.CurrentGeneration.Chromosomes.Count + "\n");
                        tendonScrFile.WriteLine(tendonScr);
                    }
                    tendonScr = "";
#endif

#if TENDONCHK
                    for (int girder = 0; girder < Enum.GetNames(typeof(Girders)).Length; girder++)
                    {
                        using (StreamWriter tendonChkFile = new StreamWriter(PhysicalBridge.Directory + Globals.TextFiles.TendonChkFile(Runs, (Girders) Enum.GetValues(typeof(Girders)).GetValue(girder)), append))
                        {
                            if (append == false) tendonChkFile.WriteLine(ReturnChkTitle(Girders.LeftGirder) + "\n");

                            tendonChkFile.WriteLine("Generation:\t" + generationNumber + "\t\tPopulation size:\t" + geneticAlgorithm.Population.CurrentGeneration.Chromosomes.Count + "\n");
                            tendonChkFile.WriteLine(tendonChk[girder]);
                        }
                        tendonChk[girder] = "";
                    }
#endif
                }
                generationNumber++;
            };

            geneticAlgorithm.Start();
            stopWatch.Stop();

            TendonOptimizationResult finalSet = new TendonOptimizationResult();
            List<IChromosome> validSolutions = geneticAlgorithm.Population.CurrentGeneration.Chromosomes.Where(pc => (pc as PrestressingChromosome).Collisions == 0).
                Where(pc => (pc as PrestressingChromosome).Exceedance[(int) Combinations.RareCombination - 1] / (0.6 * PhysicalBridge.Concrete.CompressiveStrength) <= 0.05).
                Where(pc => (pc as PrestressingChromosome).Exceedance[(int) Combinations.QuasiPermanentCombination - 1] / (0.45 * PhysicalBridge.Concrete.CompressiveStrength) <= 0.05).
                Where(pc => (pc as PrestressingChromosome).Exceedance[(int) Combinations.FrequentCombination - 1] <= 0.5).ToList();

            if (validSolutions.Count == 0) finalSet.Penalized = true;
            else
            {
                validSolutions = validSolutions.OrderByDescending(pc => (pc as PrestressingChromosome).Weight).ToList();
                PrestressingChromosome bestChromosome = validSolutions.First() as PrestressingChromosome;
                for (int i = 0; i < numberOfTendons; i++)
                {
                    double[] tendonParamSets = new double[predictionInputs + 2];
                    tendonParamSets[0] = ResolveAnchorageOrdinate(zc, bestChromosome.GetGenes().Where((g, j) => (j % 4 == 0) && (j > 0)).Select(g => (double) g.Value).ToArray(), bestChromosome.GetGenes().Where((g, j) => (j % 5 == 0) && (j > 0)).Select(g => (double) g.Value).ToArray()) + i * 0.350;

                    int index = 1;
                    foreach (double element in bestChromosome.GetGenes().Skip(i * (predictionInputs + 1)).Take(predictionInputs + 1).Select(g => (double) g.Value).ToArray())
                    {
                        tendonParamSets[index] = element;
                        index++;
                    }

                    if (i < numberOfFullTendons) //Full tendons:
                    {
                        FullTendon tendon = new FullTendon(0.0, tendonParamSets[0], tendonParamSets[1], tendonParamSets[2], PhysicalBridge.SpanLength.First(), tendonParamSets[3], PhysicalBridge.SpanLength.First() + (PhysicalBridge.SpanLength.First() - tendonParamSets[1]), tendonParamSets[2], PhysicalBridge.SpanLength.First() + PhysicalBridge.SpanLength.Last(), tendonParamSets[0], 2.0, 2.0, tendonParamSets[4])
                        {
                            PrestressForce = InitialPrestressForce,
                            PrestressType = PrestressTypes.DoubleSided
                        };
                        finalSet.AddFullTendon(tendon, (int) Math.Round(tendonParamSets[6]), (int) Math.Round(tendonParamSets[5]));
                    }
                    else //Partial tendons:
                    {
                        PartialTendon tendon = new PartialTendon(0.0, tendonParamSets[0], tendonParamSets[1], tendonParamSets[2], PhysicalBridge.SpanLength.First(), tendonParamSets[3], 2.0, 6.0, tendonParamSets[4])
                        {
                            PrestressForce = InitialPrestressForce
                        };
                        finalSet.AddPartialTendon(tendon, (int) Math.Round(tendonParamSets[6]), (int) Math.Round(tendonParamSets[5]));
                    }
                    finalSet.Penalized = false;
                }
            }
            stopWatch.Stop();
            return finalSet;
        }

        private int CollisionCheck(List<DiscreteTendon> tendons, double minVerticalDistance = 0.200, double minAnchorageDistance = 0.350)
        {
            int collisions = 0;
            foreach (DiscreteTendon tendon in tendons)
            {
                foreach (DiscreteTendon counterTendon in tendons)
                {
                    if (tendon == counterTendon) continue;

                    List<TendonPoint> tendonPoints = new List<TendonPoint>();
                    if (tendon.Tendon is FullTendon) tendonPoints.AddRange(((FullTendon) tendon.Tendon).TendonPoints);
                    else if (tendon.Tendon is PartialTendon) tendonPoints.AddRange(((PartialTendon) tendon.Tendon).TendonPoints);

                    bool isCounterBelow = false;
                    foreach (TendonPoint point in tendonPoints)
                    {
                        double counterZ = counterTendon.GetTendonZ(point.X);
                        if (point == tendonPoints.First())
                        {
                            if (Math.Abs(point.Z - counterZ) < minAnchorageDistance) collisions++;
                            if (point.Z > counterZ) isCounterBelow = true;
                        }
                        else
                        {
                            if (point == tendonPoints.Last())
                            {
                                if (tendon.Tendon is FullTendon && counterTendon.Tendon is FullTendon)
                                {
                                    if (Math.Abs(point.Z - counterZ) < minAnchorageDistance) collisions++;
                                }
                                else if (Math.Abs(point.Z - counterZ) < minVerticalDistance) collisions++;
                            }
                            else if (Math.Abs(point.Z - counterZ) < minVerticalDistance) collisions++;

                            if (point.Z < counterZ && isCounterBelow) { isCounterBelow = !isCounterBelow; collisions++; }
                            else if (point.Z > counterZ && !isCounterBelow) { collisions++; isCounterBelow = !isCounterBelow; }
                        }
                    }
                }
            }
            return collisions;
        }

        private double ResolveAnchorageOrdinate(double zc, double[] numberOfTendons, double[] numberOfStrands, double minDistance = 0.350)
        {
            double areaSumm = 0; double corrSumm = 0;
            for (int i = 0; i < numberOfTendons.Count(); i++)
            {
                areaSumm += numberOfTendons[i] * numberOfStrands[i];
                corrSumm += numberOfTendons[i] * numberOfStrands[i] * (i * minDistance);
            }

            if (areaSumm > 0.0) return (zc * areaSumm - corrSumm) / areaSumm;
            return 0.0;
        }

        private string ReturnChkTitle(Girders girder)
        {
            string g;
            if (girder == Girders.LeftGirder) g = "L";
            else g = "R";

            //Rare combination:
            string rareTitle = "\tSR." + g + ".0.1.Bmin\tSR." + g + ".0.1.Bmax\tSR." + g + ".0.1.Tmin\tSR." + g + ".0.1.Tmax";
            rareTitle += "\tSR." + g + ".0.2.Bmin\tSR." + g + ".0.2.Bmax\tSR." + g + ".0.2.Tmin\tSR." + g + ".0.2.Tmax";
            rareTitle += "\tSR." + g + ".0.3.Bmin\tSR." + g + ".0.3.Bmax\tSR." + g + ".0.3.Tmin\tSR." + g + ".0.3.Tmax";

            rareTitle += "\tSR." + g + ".2.1.Bmin\tSR." + g + ".2.1.Bmax\tSR." + g + ".2.1.Tmin\tSR." + g + ".2.1.Tmax";
            rareTitle += "\tSR." + g + ".2.2.Bmin\tSR." + g + ".2.2.Bmax\tSR." + g + ".2.2.Tmin\tSR." + g + ".2.2.Tmax";
            rareTitle += "\tSR." + g + ".2.3.Bmin\tSR." + g + ".2.3.Bmax\tSR." + g + ".2.3.Tmin\tSR." + g + ".2.3.Tmax";

            rareTitle += "\tE.R";

            //Frequent combination:
            string frequentTitle = "\tSF." + g + ".0.1.Bmin\tSF." + g + ".0.1.Bmax\tSF." + g + ".0.1.Tmin\tSF." + g + ".0.1.Tmax";
            frequentTitle += "\tSF." + g + ".0.2.Bmin\tSF." + g + ".0.2.Bmax\tSF." + g + ".0.2.Tmin\tSF." + g + ".0.2.Tmax";
            frequentTitle += "\tSF." + g + ".0.3.Bmin\tSF." + g + ".0.3.Bmax\tSF." + g + ".0.3.Tmin\tSF." + g + ".0.3.Tmax";

            frequentTitle += "\tSF." + g + ".2.1.Bmin\tSF." + g + ".2.1.Bmax\tSF." + g + ".2.1.Tmin\tSF." + g + ".2.1.Tmax";
            frequentTitle += "\tSF." + g + ".2.2.Bmin\tSF." + g + ".2.2.Bmax\tSF." + g + ".2.2.Tmin\tSF." + g + ".2.2.Tmax";
            frequentTitle += "\tSF." + g + ".2.3.Bmin\tSF." + g + ".2.3.Bmax\tSF." + g + ".2.3.Tmin\tSF." + g + ".2.3.Tmax";

            frequentTitle += "\tE.F";

            //Quasi-permanent combination:
            string quasiTitle = "\tSQ." + g + ".0.1.Bmin\tSQ." + g + ".0.1.Bmax\tSQ." + g + ".0.1.Tmin\tSQ." + g + ".0.1.Tmax";
            quasiTitle += "\tSQ." + g + ".0.2.Bmin\tSQ." + g + ".0.2.Bmax\tSQ." + g + ".0.2.Tmin\tSQ." + g + ".0.2.Tmax";
            quasiTitle += "\tSQ." + g + ".0.3.Bmin\tSQ." + g + ".0.3.Bmax\tSQ." + g + ".0.3.Tmin\tSQ." + g + ".0.3.Tmax";

            quasiTitle += "\tSQ." + g + ".2.1.Bmin\tSQ." + g + ".2.1.Bmax\tSQ." + g + ".2.1.Tmin\tSQ." + g + ".2.1.Tmax";
            quasiTitle += "\tSQ." + g + ".2.2.Bmin\tSQ." + g + ".2.2.Bmax\tSQ." + g + ".2.2.Tmin\tSQ." + g + ".2.2.Tmax";
            quasiTitle += "\tSQ." + g + ".2.3.Bmin\tSQ." + g + ".2.3.Bmax\tSQ." + g + ".2.3.Tmin\tSQ." + g + ".2.3.Tmax";

            quasiTitle += "\tE.Q";

            return rareTitle + frequentTitle + quasiTitle + "\tE\tE.tot\tC\tFIT";
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class TendonOptimizationTest : IExternalCommand
    {
        public PhysicalBridge PhysicalBridge;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            PhysicalBridge = new PhysicalBridge(commandData);
            PhysicalBridge.DataForm = new DataForm();

            PhysicalBridge.Name = "Tendon Optimization Test";
            PhysicalBridge.Directory = @"D:\_OPT\Generations\_Test Units\Tendon Optimization Test\";
            PhysicalBridge.SpanLength.Add(32.0);
            PhysicalBridge.SpanLength.Add(32.0);

            PhysicalBridge.CriticalCrossSections = new List<double>();
            PhysicalBridge.CriticalCrossSections.Add(0.4 * PhysicalBridge.SpanLength.First());
            PhysicalBridge.CriticalCrossSections.Add(0.5 * PhysicalBridge.SpanLength.First());
            PhysicalBridge.CriticalCrossSections.Add(1.0 * PhysicalBridge.SpanLength.First());
            Envelopes envelopes = new Envelopes(PhysicalBridge, new int[4] { 601, 602, 603, 604 }, 1);

            Matrix<double> inputToHiddenWeights = DelimitedReader.Read<double>(@"D:\_OPT\Generations\_Test Units\Tendon Optimization Test\NN\InputToHiddenWeights.csv", false, "\t", false);
            Vector<double> inputToHiddenBiases = DelimitedReader.Read<double>(@"D:\_OPT\Generations\_Test Units\Tendon Optimization Test\NN\InputToHiddenBiases.csv", false, "\t", false).Column(0);
            
            Matrix<double> hiddenToOutputWeights = DelimitedReader.Read<double>(@"D:\_OPT\Generations\_Test Units\Tendon Optimization Test\NN\HiddenToOutputWeights.csv", false, "\t", false);
            Vector<double> hiddenToOutputBiases = DelimitedReader.Read<double>(@"D:\_OPT\Generations\_Test Units\Tendon Optimization Test\NN\HiddenToOutputBiases.csv", false, "\t", false).Column(0);

            Vector<double> normInputAvg = DelimitedReader.Read<double>(@"D:\_OPT\Generations\_Test Units\Tendon Optimization Test\NN\NormInputAvg.csv", false, "\t", false).Column(0);
            Vector<double> normInputStd = DelimitedReader.Read<double>(@"D:\_OPT\Generations\_Test Units\Tendon Optimization Test\NN\NormInputStd.csv", false, "\t", false).Column(0);

            Vector<double> normOutputAvg = DelimitedReader.Read<double>(@"D:\_OPT\Generations\_Test Units\Tendon Optimization Test\NN\NormOutputAvg.csv", false, "\t", false).Column(0);
            Vector<double> normOutputStd = DelimitedReader.Read<double>(@"D:\_OPT\Generations\_Test Units\Tendon Optimization Test\NN\NormOutputStd.csv", false, "\t", false).Column(0);

            TendonNeuralNetwork network = new TendonNeuralNetwork(inputToHiddenWeights, inputToHiddenBiases, hiddenToOutputWeights, hiddenToOutputBiases, normInputAvg, normInputStd, normOutputAvg, normOutputStd);
            TendonOptimization opt = new TendonOptimization(PhysicalBridge, envelopes, network);
            return Result.Succeeded;
        }
    }
}