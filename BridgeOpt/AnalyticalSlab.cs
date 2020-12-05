#define SLABDEF //Output: slab definition
#define SLABULS //Output: slab ULS check
#define SLABSLS //Output: slab SLS check

#define SLABREBARS //Output: final slab rebars arrangement

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using Microsoft.Office.Interop.Excel;
using Application = Microsoft.Office.Interop.Excel.Application;

namespace BridgeOpt
{
    public enum Cantilever { Left = 0, Right = 1 }
    public enum SlabCombinations
    {
        Design = 0,
        Rare = 1,
        Infrequent = 2,
        Frequent = 3,
        QuasiPermanent = 4
    }

    public class AnalyticalSlab
    {
        private readonly List<int> RebarSpacing = new List<int> { 200, 150, 125, 100 };
        public PhysicalBridge PhysicalBridge;

        public double[] CantileverOverhang;
        public double[] G1; //Corresponds to the G1 (or G11) parameter of a physical bridge, for left (or right) cantilever
        public double[] G2; //Corresponds to the G2 (or G10) parameter of a physical bridge, for left (or right) cantilever
        public double[] G3; //Corresponds to the G3 (or G9) parameter of a physical bridge, for left (or right) cantilever
        public double[] WL; //Corresponds to the WL (or WP) parameter of a physical bridge, for left (or right) cantilever

        public double SpanLength;
        public double DesignSpanLength;
        public double G4; //Corresponds to the G4 parameter of a physical bridge
        public double G5; //Corresponds to the G5 parameter of a physical bridge
        public double G6; //Corresponds to the G6 parameter of a physical bridge
        public double G7; //Corresponds to the G7 parameter of a physical bridge
        public double G8; //Corresponds to the G8 parameter of a physical bridge
        public double PL; //Corresponds to the PL parameter of a physical bridge
        public double PP; //Corresponds to the PP parameter of a physical bridge

        public double[] CantileverMoments;
        public double[] SpanMoments;

        public SlabRebarsArrangement SlabRebars;

        public AnalyticalSlab(PhysicalBridge bridge)
        {
            Stopwatch localStopWatch = new Stopwatch(); localStopWatch.Start();
            PhysicalBridge = bridge;
            PhysicalBridge.AnalyticalSlab = this;

            //Cantilever part of the AnalyticalSlab class:
            CantileverOverhang = new double[2];
            CantileverOverhang[(int) Cantilever.Left] = Converters.ToMeters(Math.Abs(bridge.Superstructure.ParametersMap.get_Item("O1").AsDouble() - bridge.Superstructure.ParametersMap.get_Item("OL").AsDouble()) - 0.5 * bridge.Superstructure.ParametersMap.get_Item("BL").AsDouble() - bridge.Superstructure.ParametersMap.get_Item("BL1").AsDouble());
            CantileverOverhang[(int) Cantilever.Right] = Converters.ToMeters(Math.Abs(bridge.Superstructure.ParametersMap.get_Item("O5").AsDouble() - bridge.Superstructure.ParametersMap.get_Item("OP").AsDouble()) - 0.5 * bridge.Superstructure.ParametersMap.get_Item("BP").AsDouble() - bridge.Superstructure.ParametersMap.get_Item("BP1").AsDouble());

            G1 = new double[2];
            G2 = new double[2];
            G3 = new double[2];
            WL = new double[2];

            G1[(int) Cantilever.Left] = Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("G1").AsDouble());
            G2[(int) Cantilever.Left] = Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("G2").AsDouble());
            G3[(int) Cantilever.Left] = Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("G3").AsDouble());
            WL[(int) Cantilever.Left] = Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("WL").AsDouble());
            if (WL[(int) Cantilever.Left] == 0)
            {
                G2[(int) Cantilever.Left] = Math.Max(G2[(int) Cantilever.Left], G3[(int) Cantilever.Left]);
                G3[(int) Cantilever.Left] = Math.Max(G2[(int) Cantilever.Left], G3[(int) Cantilever.Left]);
            }
            G1[(int) Cantilever.Right] = Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("G11").AsDouble());
            G2[(int) Cantilever.Right] = Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("G10").AsDouble());
            G3[(int) Cantilever.Right] = Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("G9").AsDouble());
            WL[(int) Cantilever.Right] = Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("WP").AsDouble());
            if (WL[(int) Cantilever.Right] == 0)
            {
                G2[(int) Cantilever.Right] = Math.Max(G2[(int) Cantilever.Right], G3[(int) Cantilever.Right]);
                G3[(int) Cantilever.Right] = Math.Max(G2[(int) Cantilever.Right], G3[(int) Cantilever.Right]);
            }

            //Middle span slab part of the AnalyticalSlab class:
            SpanLength = Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("OP").AsDouble() - bridge.Superstructure.ParametersMap.get_Item("OL").AsDouble() -
                                       0.5 * bridge.Superstructure.ParametersMap.get_Item("BL").AsDouble() - bridge.Superstructure.ParametersMap.get_Item("BL2").AsDouble() -
                                       0.5 * bridge.Superstructure.ParametersMap.get_Item("BP").AsDouble() - bridge.Superstructure.ParametersMap.get_Item("BP2").AsDouble());
            G4 = Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("G4").AsDouble());
            G5 = Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("G5").AsDouble());
            G7 = Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("G7").AsDouble());
            G8 = Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("G8").AsDouble());
            DesignSpanLength = SpanLength + GetLeftSpanDelta() + GetRightSpanDelta();

            PL = Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("PL").AsDouble());
            if (PL == 0)
            {
                G4 = Math.Max(G4, G5);
                G5 = Math.Max(G4, G5);
            }
            PP = Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("PP").AsDouble());
            if (PP == 0)
            {
                G7 = Math.Max(G7, G8);
                G8 = Math.Max(G7, G8);
            }

            G6 = Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("G6").AsDouble());
            if (G6 == 0) G6 = 0.5 * (G5 + G7);

#if SLABDEF
            using (StreamWriter slabDefFile = new StreamWriter(PhysicalBridge.Directory + Globals.TextFiles.SlabDefinition, false))
            {
                slabDefFile.WriteLine(string.Format("G1\tG2\tG3\tG4\tG5\tG6\tG7\tG8\tG9\tG10\tG11\tWL\tWP\tPL\tPP\tCO.1\tCO.2\tL\tL.d"));
                slabDefFile.WriteLine(string.Format("{0:0.000}\t{1:0.000}\t{2:0.000}\t{3:0.000}\t{4:0.000}\t{5:0.000}\t{6:0.000}\t{7:0.000}\t{8:0.000}\t{9:0.000}\t{10:0.000}\t{11:0.000}\t{12:0.000}\t{13:0.000}\t{14:0.000}\t{15:0.000}\t{16:0.000}\t{17:0.000}\t{18:0.000}",
                    G1[(int) Cantilever.Left], G2[(int) Cantilever.Left], G3[(int) Cantilever.Left], G4, G5, G6, G7, G8, G3[(int) Cantilever.Right], G2[(int) Cantilever.Right], G1[(int) Cantilever.Right], WL[(int) Cantilever.Left], WL[(int) Cantilever.Right], PL, PP, CantileverOverhang[(int) Cantilever.Left], CantileverOverhang[(int) Cantilever.Right], SpanLength, DesignSpanLength));
            }
#endif
            CalculateEnvelopes();
            using (StreamWriter stopwatchSummary = new StreamWriter(PhysicalBridge.Directory + Globals.TextFiles.StopwatchSummary, true)) stopwatchSummary.WriteLine("\tAnalytical slab, envelopes:\t\t" + DateTime.Now.ToString() + "\t" + localStopWatch.Elapsed.ToString()); localStopWatch.Restart();

            List<UltimateLimitState> spanUltimateLimitStates = new List<UltimateLimitState>();
            List<ServiceabilityLimitState> spanServiceabilityLimitStates = new List<ServiceabilityLimitState>();
            CalculateLimitStates(SpanMoments[(int) SlabCombinations.Design], SpanMoments[(int) SlabCombinations.Infrequent], Math.Min(G5, Math.Min(G6, G7)), false, ref spanUltimateLimitStates, ref spanServiceabilityLimitStates);
            using (StreamWriter stopwatchSummary = new StreamWriter(PhysicalBridge.Directory + Globals.TextFiles.StopwatchSummary, true)) stopwatchSummary.WriteLine("\tAnalytical slab, span limit states:\t" + DateTime.Now.ToString() + "\t" + localStopWatch.Elapsed.ToString()); localStopWatch.Restart();

            List<UltimateLimitState> supportUltimateLimitStates = new List<UltimateLimitState>();
            List<ServiceabilityLimitState> supportServiceabilityLimitStates = new List<ServiceabilityLimitState>();
            CalculateLimitStates(CantileverMoments[(int) SlabCombinations.Design], CantileverMoments[(int) SlabCombinations.Infrequent], Math.Min(G3[(int) Cantilever.Left], Math.Min(G3[(int) Cantilever.Right], Math.Min(G4, G8))), true, ref supportUltimateLimitStates, ref supportServiceabilityLimitStates);
            using (StreamWriter stopwatchSummary = new StreamWriter(PhysicalBridge.Directory + Globals.TextFiles.StopwatchSummary, true)) stopwatchSummary.WriteLine("\tAnalytical slab, support limit states:\t" + DateTime.Now.ToString() + "\t" + localStopWatch.Elapsed.ToString()); localStopWatch.Restart();

            int maxLayers = 6;
#if SLABULS
            using (StreamWriter slabUlsFile = new StreamWriter(PhysicalBridge.Directory + Globals.TextFiles.SlabULS, false))
            {
                string strTitle = "M\tH\tB";
                for (int i = 0; i < maxLayers; i++) strTitle += "\tR" + (i + 1) + ".n\tR" + (i + 1) + ".f\tR" + (i + 1) + ".A\tR" + (i + 1) + ".z";
                strTitle += "\tA\tB\tX\tSC.T\tSC.B\tSR.T\tSR.B";
                slabUlsFile.WriteLine(strTitle);

                slabUlsFile.WriteLine("");
                slabUlsFile.WriteLine("Span ULS\n");
                foreach (UltimateLimitState limitState in spanUltimateLimitStates)
                {
                    if (limitState == null) slabUlsFile.WriteLine("Less than minimum reinforcement");
                    else
                    {
                        string strLine = string.Format("{0:0.0}\t{1:0.000}\t{2:0.000}", limitState.M, limitState.DiscreteCrossSection.CrossSection.Height, limitState.DiscreteCrossSection.CrossSection.Width);
                        for (int i = 0; i < maxLayers; i++)
                        {
                            if (i < limitState.DiscreteCrossSection.ReinforcementLayers.Count()) strLine += string.Format("\t{0:0.000}\t{1:0}\t{2:0.0}\t{3:0.0}", limitState.DiscreteCrossSection.ReinforcementLayers[i].NumberOfRebars, limitState.DiscreteCrossSection.ReinforcementLayers[i].RebarsDiameter, limitState.DiscreteCrossSection.ReinforcementLayers[i].Area * Math.Pow(1000, 2), 1000 * limitState.DiscreteCrossSection.ReinforcementLayers[i].Ordinate);
                            else strLine += "\t0.000\t0\t0.0\t0.0";
                        }

                        if (Math.Abs(limitState.StrainState.A) < 10) strLine += string.Format("\t{0:0.000}", limitState.StrainState.A);
                        else if (Math.Abs(limitState.StrainState.A) < 100) strLine += string.Format("\t{0:0.00}", limitState.StrainState.A);
                        else strLine += string.Format("\t{0:0.0}", limitState.StrainState.A);

                        if (Math.Abs(limitState.StrainState.B) < 10) strLine += string.Format("\t{0:0.000}", limitState.StrainState.B);
                        else if (Math.Abs(limitState.StrainState.B) < 100) strLine += string.Format("\t{0:0.00}", limitState.StrainState.B);
                        else strLine += string.Format("\t{0:0.0}", limitState.StrainState.B);

                        strLine += string.Format("\t{0:0.000}\t{1:0.0}\t{2:0.0}\t{3:0.0}\t{4:0.0}", limitState.StrainState.X(limitState.DiscreteCrossSection.CrossSection.Height), limitState.TopConcreteStress, limitState.BottomConcreteStress, limitState.TopReinforcementStress, limitState.BottomReinforcementStress);
                        slabUlsFile.WriteLine(strLine);
                    }
                }

                slabUlsFile.WriteLine("");
                slabUlsFile.WriteLine("Support ULS\n");
                foreach (UltimateLimitState limitState in supportUltimateLimitStates)
                {
                    if (limitState == null) slabUlsFile.WriteLine("Less than minimum reinforcement");
                    else
                    {
                        string strLine = string.Format("{0:0.0}\t{1:0.000}\t{2:0.000}", limitState.M, limitState.DiscreteCrossSection.CrossSection.Height, limitState.DiscreteCrossSection.CrossSection.Width);
                        for (int i = 0; i < maxLayers; i++)
                        {
                            if (i < limitState.DiscreteCrossSection.ReinforcementLayers.Count()) strLine += string.Format("\t{0:0.000}\t{1:0}\t{2:0.0}\t{3:0.0}", limitState.DiscreteCrossSection.ReinforcementLayers[i].NumberOfRebars, limitState.DiscreteCrossSection.ReinforcementLayers[i].RebarsDiameter, limitState.DiscreteCrossSection.ReinforcementLayers[i].Area * Math.Pow(1000, 2), 1000 * limitState.DiscreteCrossSection.ReinforcementLayers[i].Ordinate);
                            else strLine += "\t0.000\t0\t0.0\t0.0";
                        }

                        if (Math.Abs(limitState.StrainState.A) < 10) strLine += string.Format("\t{0:0.000}", limitState.StrainState.A);
                        else if (Math.Abs(limitState.StrainState.A) < 100) strLine += string.Format("\t{0:0.00}", limitState.StrainState.A);
                        else strLine += string.Format("\t{0:0.0}", limitState.StrainState.A);

                        if (Math.Abs(limitState.StrainState.B) < 10) strLine += string.Format("\t{0:0.000}", limitState.StrainState.B);
                        else if (Math.Abs(limitState.StrainState.B) < 100) strLine += string.Format("\t{0:0.00}", limitState.StrainState.B);
                        else strLine += string.Format("\t{0:0.0}", limitState.StrainState.B);

                        strLine += string.Format("\t{0:0.000}\t{1:0.0}\t{2:0.0}\t{3:0.0}\t{4:0.0}", limitState.StrainState.X(limitState.DiscreteCrossSection.CrossSection.Height), limitState.TopConcreteStress, limitState.BottomConcreteStress, limitState.TopReinforcementStress, limitState.BottomReinforcementStress);
                        slabUlsFile.WriteLine(strLine);
                    }
                }
            }
#endif

#if SLABSLS
            using (StreamWriter slabSlsFile = new StreamWriter(PhysicalBridge.Directory + Globals.TextFiles.SlabSLS, false))
            {
                string strTitle = "M\tH\tB";
                for (int i = 0; i < maxLayers; i++) strTitle += "\tR" + (i + 1) + ".n\tR" + (i + 1) + ".f\tR" + (i + 1) + ".A\tR" + (i + 1) + ".z";
                strTitle += "\tA\tB\tX\tSR\tw.k";
                slabSlsFile.WriteLine(strTitle);

                slabSlsFile.WriteLine("");
                slabSlsFile.WriteLine("Span SLS\n");
                foreach (ServiceabilityLimitState limitState in spanServiceabilityLimitStates)
                {
                    if (limitState == null) slabSlsFile.WriteLine("Less than minimum reinforcement");
                    else
                    {
                        string strLine = string.Format("{0:0.0}\t{1:0.000}\t{2:0.000}", limitState.M, limitState.DiscreteCrossSection.CrossSection.Height, limitState.DiscreteCrossSection.CrossSection.Width);
                        for (int i = 0; i < maxLayers; i++)
                        {
                            if (i < limitState.DiscreteCrossSection.ReinforcementLayers.Count()) strLine += string.Format("\t{0:0.000}\t{1:0}\t{2:0.0}\t{3:0.0}", limitState.DiscreteCrossSection.ReinforcementLayers[i].NumberOfRebars, limitState.DiscreteCrossSection.ReinforcementLayers[i].RebarsDiameter, limitState.DiscreteCrossSection.ReinforcementLayers[i].Area * Math.Pow(1000, 2), 1000 * limitState.DiscreteCrossSection.ReinforcementLayers[i].Ordinate);
                            else strLine += "\t0.000\t0\t0.0\t0.0";
                        }

                        if (Math.Abs(limitState.StrainState.A) < 10) strLine += string.Format("\t{0:0.000}", limitState.StrainState.A);
                        else if (Math.Abs(limitState.StrainState.A) < 100) strLine += string.Format("\t{0:0.00}", limitState.StrainState.A);
                        else strLine += string.Format("\t{0:0.0}", limitState.StrainState.A);

                        if (Math.Abs(limitState.StrainState.B) < 10) strLine += string.Format("\t{0:0.000}", limitState.StrainState.B);
                        else if (Math.Abs(limitState.StrainState.B) < 100) strLine += string.Format("\t{0:0.00}", limitState.StrainState.B);
                        else strLine += string.Format("\t{0:0.0}", limitState.StrainState.B);

                        strLine += string.Format("\t{0:0.000}\t{1:0.0}\t{2:0.000}", limitState.StrainState.X(limitState.DiscreteCrossSection.CrossSection.Height), limitState.ReinforcementStress, limitState.CrackWidth);
                        slabSlsFile.WriteLine(strLine);
                    }
                }

                slabSlsFile.WriteLine("");
                slabSlsFile.WriteLine("Support SLS\n");
                foreach (ServiceabilityLimitState limitState in supportServiceabilityLimitStates)
                {
                    if (limitState == null) slabSlsFile.WriteLine("Less than minimum reinforcement");
                    else
                    {
                        string strLine = string.Format("{0:0.0}\t{1:0.000}\t{2:0.000}", limitState.M, limitState.DiscreteCrossSection.CrossSection.Height, limitState.DiscreteCrossSection.CrossSection.Width);
                        for (int i = 0; i < maxLayers; i++)
                        {
                            if (i < limitState.DiscreteCrossSection.ReinforcementLayers.Count()) strLine += string.Format("\t{0:0.000}\t{1:0}\t{2:0.0}\t{3:0.0}", limitState.DiscreteCrossSection.ReinforcementLayers[i].NumberOfRebars, limitState.DiscreteCrossSection.ReinforcementLayers[i].RebarsDiameter, limitState.DiscreteCrossSection.ReinforcementLayers[i].Area * Math.Pow(1000, 2), 1000 * limitState.DiscreteCrossSection.ReinforcementLayers[i].Ordinate);
                            else strLine += "\t0.000\t0\t0.0\t0.0";
                        }

                        if (Math.Abs(limitState.StrainState.A) < 10) strLine += string.Format("\t{0:0.000}", limitState.StrainState.A);
                        else if (Math.Abs(limitState.StrainState.A) < 100) strLine += string.Format("\t{0:0.00}", limitState.StrainState.A);
                        else strLine += string.Format("\t{0:0.0}", limitState.StrainState.A);

                        if (Math.Abs(limitState.StrainState.B) < 10) strLine += string.Format("\t{0:0.000}", limitState.StrainState.B);
                        else if (Math.Abs(limitState.StrainState.B) < 100) strLine += string.Format("\t{0:0.00}", limitState.StrainState.B);
                        else strLine += string.Format("\t{0:0.0}", limitState.StrainState.B);

                        strLine += string.Format("\t{0:0.000}\t{1:0.0}\t{2:0.000}", limitState.StrainState.X(limitState.DiscreteCrossSection.CrossSection.Height), limitState.ReinforcementStress, limitState.CrackWidth);
                        slabSlsFile.WriteLine(strLine);
                    }
                }
            }
#endif
            SlabRebars = SlabRebarsArrangement(spanUltimateLimitStates, supportUltimateLimitStates, spanServiceabilityLimitStates, supportServiceabilityLimitStates);
            using (StreamWriter stopwatchSummary = new StreamWriter(PhysicalBridge.Directory + Globals.TextFiles.StopwatchSummary, true)) stopwatchSummary.WriteLine("\tAnalytical slab, rebars arrangement:\t" + DateTime.Now.ToString() + "\t" + localStopWatch.Elapsed.ToString());
            localStopWatch.Stop();

            PhysicalBridge.AnalyticalSlab = this;
            return;
        }

        public double GetLeftSpanDelta()  { return Math.Min(0.5 * G4, 0.5 * (PhysicalBridge.Superstructure.ParametersMap.get_Item("BL").AsDouble() + PhysicalBridge.Superstructure.ParametersMap.get_Item("BL1").AsDouble() + PhysicalBridge.Superstructure.ParametersMap.get_Item("BL2").AsDouble())); }
        public double GetRightSpanDelta() { return Math.Min(0.5 * G8, 0.5 * (PhysicalBridge.Superstructure.ParametersMap.get_Item("BP").AsDouble() + PhysicalBridge.Superstructure.ParametersMap.get_Item("BP1").AsDouble() + PhysicalBridge.Superstructure.ParametersMap.get_Item("BP2").AsDouble())); }

        public double CantileverMomentBySelfWeight(double x, double gamma = 25.0, Cantilever side = Cantilever.Left)
        {
            x = Math.Min(Math.Max(x, 0.0), CantileverOverhang[(int) side]);

            double l1 = CantileverOverhang[(int) side] - WL[(int) side];
            double l2 = WL[(int) side];

            double m;
            if (x > l2)
            {
                double middleG;
                if (l1 > 0) middleG = G1[(int) side] + (G2[(int) side] - G1[(int) side]) * (l1 - (x - l2)) / l1;
                else middleG = G3[(int) side];

                m = Math.Min(G1[(int) side], middleG) * (l1 - (x - l2)) * 0.5 * (l1 - (x - l2));
                if (G1[(int) side] < middleG) m += 0.5 * (middleG - G1[(int) side]) * (l1 - (x - l2)) * (l1 - (x - l2)) / 3;
                else m += 0.5 * (G1[(int) side] - middleG) * (l1 - (x - l2)) * 2 * (l1 - (x - l2)) / 3;
            }
            else
            {
                double middleG;
                if (l2 > 0) middleG = G2[(int) side] + (G3[(int) side] - G2[(int) side]) * (l2 - x) / l2;
                else middleG = G3[(int) side];

                m = Math.Min(G1[(int) side], G2[(int) side]) * l1 * (l2 - x + 0.5 * l1);
                if (G1[(int) side] < G2[(int) side]) m += 0.5 * (G2[(int) side] - G1[(int) side]) * l1 * (l2 - x + l1 / 3);
                else m += 0.5 * (G1[(int) side] - G2[(int) side]) * l1 * (l2 - x + 2 * l1 / 3);

                m += Math.Min(G2[(int) side], middleG) * (l2 - x) * 0.5 * (l2 - x);
                if (G2[(int) side] < middleG) m += 0.5 * (middleG - G2[(int) side]) * (l2 - x) * (l2 - x) / 3;
                else m += 0.5 * (G2[(int) side] - middleG) * (l2 - x) * 2 * (l2 - x) / 3;
            }
            return -1 * gamma * m;
        }
        public double CantileverMomentByPointLoad(double x, double loadValue, double loadLoc)
        {
            loadValue = -1 * loadValue;
            loadLoc = Math.Max(loadLoc, 0.0); x = Math.Max(x, 0.0);

            if (loadLoc < x) return 0.0;
            return loadValue * (loadLoc - x);
        }
        public double CantileverMomentByUniformLoad(double x, double loadValue, double loadStartLoc, double loadEndLoc)
        {
            loadValue = -1 * loadValue;
            if (loadEndLoc < loadStartLoc)
            {
                double loc = loadStartLoc;
                loadStartLoc = loadEndLoc; loadEndLoc = loc;
            }
            loadStartLoc = Math.Max(loadStartLoc, 0.0); loadEndLoc = Math.Max(loadEndLoc, 0.0);
            x = Math.Max(x, 0.0);

            if (x < loadStartLoc) return loadValue * (loadEndLoc - loadStartLoc) * (loadStartLoc - x + 0.5 * (loadEndLoc - loadStartLoc));
            if (x < loadEndLoc) return 0.5 * loadValue * Math.Pow(loadEndLoc - x, 2);
            return 0.0;
        }

        public double SpanMomentBySelfWeight(double x, double gamma = 25.0)
        {
            x = Math.Min(Math.Max(x, 0.0), DesignSpanLength);

            List<double> c = new List<double>();
            List<double> r = new List<double>();

            if (PL > 0)
            {
                if (G4 > G5) c.Add((1.0 / 3 * (G4 - G5) * Math.Pow(PL, 2) + G5 * Math.Pow(PL, 2)) / ((G4 + G5) * PL));
                else c.Add((2.0 / 3 * (G5 - G4) * Math.Pow(PL, 2) + G4 * Math.Pow(PL, 2)) / ((G4 + G5) * PL));
                r.Add(0.5 * (G4 + G5) * PL);
            }
            else { c.Add(0); r.Add(0); }

            if (DesignSpanLength - PL - PP > 0)
            {
                if (G5 > G7) c.Add((1.0 / 3 * (G5 - G7) * Math.Pow(DesignSpanLength - PL - PP, 2) + G7 * Math.Pow(DesignSpanLength - PL - PP, 2)) / ((G5 + G7) * (DesignSpanLength - PL - PP)) + PL);
                else c.Add((2.0 / 3 * (G7 - G5) * Math.Pow(DesignSpanLength - PL - PP, 2) + G5 * Math.Pow(DesignSpanLength - PL - PP, 2)) / ((G5 + G7) * (DesignSpanLength - PL - PP)) + PL);
                r.Add(0.5 * (G5 + G7) * (DesignSpanLength - PL - PP));
            }
            else { c.Add(0); r.Add(0); }

            if (PP > 0)
            {
                if (G7 > G8) c.Add((1.0 / 3 * (G7 - G8) * Math.Pow(PP, 2) + G8 * Math.Pow(PP, 2)) / ((G7 + G8) * PP) + (DesignSpanLength - PP));
                else c.Add((2.0 / 3 * (G8 - G7) * Math.Pow(PP, 2) + G7 * Math.Pow(PP, 2)) / ((G7 + G8) * PP) + (DesignSpanLength - PP));
                r.Add(0.5 * (G7 + G8) * PP);
            }
            else { c.Add(0); r.Add(0); }

            double rB = 0; double rA = 0;
            for (int i = 0; i < c.Count(); i++)
            {
                rB += c[i] * r[i];
                rA += r[i];
            }
            rB = rB / DesignSpanLength;
            rA = rA - rB;

            double div = 0.001;
            double m = 0; double v = rA;
            for (int i = 0; i < (int) Math.Round(x / div, 0); i++)
            {
                double g1 = 0.0, g2 = 0.0;
                double v1 = 0.0, v2 = 0.0;
                if ((0.001 * (i + 1) <= PL) && (PL > 0))
                {
                    g1 = G4 - (G4 - G5) * div * i / PL;
                    g2 = G4 - (G4 - G5) * div * (i + 1) / PL;
                }
                else if ((0.001 * (i + 1) <= DesignSpanLength - PP) && (DesignSpanLength - PL - PP > 0))
                {
                    g1 = G5 - (G5 - G7) * (div * i - PL) / (DesignSpanLength - PL - PP);
                    g2 = G5 - (G5 - G7) * (div * (i + 1) - PL) / (DesignSpanLength - PL - PP);
                }
                else if (PP > 0)
                {
                    g1 = G7 - (G7 - G8) * (div * i - DesignSpanLength + PP) / PP;
                    g2 = G7 - (G7 - G8) * (div * (i + 1) - DesignSpanLength + PP) / PP;
                }

                v1 = v;
                v2 = v - 0.5 * (g1 + g2) * div;

                m += 0.5 * (v1 + v2) * div;
                v = v2;
            }
            return gamma * m;
        }
        public double SpanMomentByPointLoad(double x, double loadValue, double loadLoc)
        {
            loadLoc = Math.Min(Math.Max(loadLoc, 0.0), DesignSpanLength);
            x = Math.Min(Math.Max(x, 0.0), DesignSpanLength);

            if (x < loadLoc) return loadValue * (DesignSpanLength - loadLoc) * x / DesignSpanLength;
            return loadValue * loadLoc * (DesignSpanLength - x) / DesignSpanLength;
        }
        public double SpanMomentByUniformLoad(double x, double loadValue, double loadStartLoc, double loadEndLoc)
        {
            if (loadEndLoc < loadStartLoc)
            {
                double loc = loadStartLoc;
                loadStartLoc = loadEndLoc; loadEndLoc = loc;
            }
            loadStartLoc = Math.Min(Math.Max(loadStartLoc, 0.0), DesignSpanLength);
            loadEndLoc = Math.Min(Math.Max(loadEndLoc, 0.0), DesignSpanLength);

            x = Math.Min(Math.Max(x, 0.0), DesignSpanLength);
            double rB = (loadEndLoc - loadStartLoc) * loadValue * (loadStartLoc + 0.5 * (loadEndLoc - loadStartLoc)) / DesignSpanLength;
            double rA = (loadEndLoc - loadStartLoc) * loadValue - rB;

            if (x < loadStartLoc) return rA * x;
            if (x < loadEndLoc)
            {
                double rX = rA - loadValue * (x - loadStartLoc);
                return rA * loadStartLoc + 0.5 * (rA + rX) * (x - loadStartLoc);
            }
            return rA * loadStartLoc + 0.5 * (rA - rB) * (loadEndLoc - loadStartLoc) - rB * (x - loadEndLoc);
        }

        private readonly int startRow = 6;
        public void CalculateEnvelopes(double movingLoadStep = 0.100)
        {
            Application excel = new Application();
            Workbook slabEnvelopes = excel.Workbooks.Open(PhysicalBridge.Directory + Globals.ExcelFiles.SlabEnvelopes);
            ClearEnvelopes(slabEnvelopes);

            List<double> lanes = new List<double>();
            double carriagewayWidth = 0.0, remainingArea = 0.0;
            if (PhysicalBridge.DataForm.DataFormCarriageway.Checked)
            {
                carriagewayWidth = double.Parse(PhysicalBridge.DataForm.DataFormCarriagewayEnd.Text) - double.Parse(PhysicalBridge.DataForm.DataFormCarriagewayStart.Text);
                if (carriagewayWidth < 5.400)
                {
                    lanes.Add(3.000);
                    remainingArea = carriagewayWidth - 3.000;
                }
                else if (carriagewayWidth < 6.000)
                {
                    lanes.Add(0.5 * carriagewayWidth);
                    lanes.Add(0.5 * carriagewayWidth);
                    remainingArea = 0.0;
                }
                else
                {
                    for (int i = 0; i < Math.Min((int) Math.Floor(carriagewayWidth / 3.000), 3); i++)
                    {
                        lanes.Add(3.000);
                        remainingArea += 3.000;
                    }
                    remainingArea = carriagewayWidth - remainingArea;
                }
            }
            int row;
            List<double> m = new List<double> { 0.0, 0.0, 0.0 };

            double leftCantileverMax = CantileverOverhang[(int) Cantilever.Left];
            double spanMin = CantileverOverhang[(int) Cantilever.Left] + Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("BL").AsDouble()) + Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("BL1").AsDouble()) + Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("BL2").AsDouble()) - GetLeftSpanDelta();
            double spanMax = spanMin + DesignSpanLength;
            double rightCantileverMin = CantileverOverhang[(int) Cantilever.Left] + Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("BL").AsDouble()) + Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("BL1").AsDouble()) + Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("BL2").AsDouble()) + SpanLength +
                                                                                    Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("BP").AsDouble()) + Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("BP1").AsDouble()) + Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("BP2").AsDouble());
            //Self-weight:
            row = startRow;
            slabEnvelopes.Worksheets["CWK"].Cells[row, 2] = CantileverMomentBySelfWeight(0.0, 25.0, Cantilever.Left); row++;
            slabEnvelopes.Worksheets["CWK"].Cells[row, 2] = SpanMomentBySelfWeight(0.5 * DesignSpanLength, 25.0); row++;
            slabEnvelopes.Worksheets["CWK"].Cells[row, 2] = CantileverMomentBySelfWeight(0.0, 25.0, Cantilever.Right);

            //Equipment self-weight:
            row = startRow; m = new List<double> { 0.0, 0.0, 0.0 };
            if (PhysicalBridge.DataForm.DataFormUniformPlanarLoads.Rows.Count > 1)
            {
                foreach (DataGridViewRow gridRow in PhysicalBridge.DataForm.DataFormUniformPlanarLoads.Rows)
                {
                    if (PhysicalBridge.DataForm.DataFormUniformPlanarLoads.Rows.IndexOf(gridRow) == PhysicalBridge.DataForm.DataFormUniformPlanarLoads.Rows.Count - 1) break;
                    m[0] += CantileverMomentByUniformLoad(0.0, double.Parse((string) gridRow.Cells[0].Value), leftCantileverMax - double.Parse((string) gridRow.Cells[2].Value), leftCantileverMax - double.Parse((string) gridRow.Cells[1].Value));
                    m[1] += SpanMomentByUniformLoad(0.5 * DesignSpanLength, double.Parse((string) gridRow.Cells[0].Value), double.Parse((string) gridRow.Cells[1].Value) - spanMin, double.Parse((string) gridRow.Cells[2].Value) - spanMin);
                    m[2] += CantileverMomentByUniformLoad(0.0, double.Parse((string) gridRow.Cells[0].Value), double.Parse((string) gridRow.Cells[1].Value) - rightCantileverMin, double.Parse((string) gridRow.Cells[2].Value) - rightCantileverMin);
                }
            }
            if (PhysicalBridge.DataForm.DataFormUniformLinearLoads.Rows.Count > 1)
            {
                foreach (DataGridViewRow gridRow in PhysicalBridge.DataForm.DataFormUniformLinearLoads.Rows)
                {
                    if (PhysicalBridge.DataForm.DataFormUniformLinearLoads.Rows.IndexOf(gridRow) == PhysicalBridge.DataForm.DataFormUniformLinearLoads.Rows.Count - 1) break;
                    m[0] += CantileverMomentByPointLoad(0.0, double.Parse((string) gridRow.Cells[0].Value), leftCantileverMax - double.Parse((string) gridRow.Cells[1].Value));
                    m[1] += SpanMomentByPointLoad(0.5 * DesignSpanLength, double.Parse((string) gridRow.Cells[0].Value), double.Parse((string) gridRow.Cells[1].Value) - spanMin);
                    m[2] += CantileverMomentByPointLoad(0.0, double.Parse((string) gridRow.Cells[0].Value), double.Parse((string) gridRow.Cells[1].Value) - rightCantileverMin);
                }
            }
            slabEnvelopes.Worksheets["CWW"].Cells[row, 2] = m[0]; row++;
            slabEnvelopes.Worksheets["CWW"].Cells[row, 2] = m[1]; row++;
            slabEnvelopes.Worksheets["CWW"].Cells[row, 2] = m[2];

            //Footway:
            row = startRow;
            if (PhysicalBridge.DataForm.DataFormLeftFootway.Checked)
            {
                slabEnvelopes.Worksheets["CHO"].Cells[row, 2] = CantileverMomentByUniformLoad(0.0, 5.00, leftCantileverMax - double.Parse(PhysicalBridge.DataForm.DataFormLeftFootwayEnd.Text), leftCantileverMax - double.Parse(PhysicalBridge.DataForm.DataFormLeftFootwayStart.Text)); row++;
                slabEnvelopes.Worksheets["CHO"].Cells[row, 2] = SpanMomentByUniformLoad(0.5 * DesignSpanLength, 5.00, double.Parse(PhysicalBridge.DataForm.DataFormLeftFootwayEnd.Text) - spanMin, double.Parse(PhysicalBridge.DataForm.DataFormLeftFootwayStart.Text) - spanMin); row++;
                slabEnvelopes.Worksheets["CHO"].Cells[row, 2] = CantileverMomentByUniformLoad(0.0, 5.00, double.Parse(PhysicalBridge.DataForm.DataFormLeftFootwayEnd.Text) - rightCantileverMin, double.Parse(PhysicalBridge.DataForm.DataFormLeftFootwayStart.Text) - rightCantileverMin);
            }
            row = startRow;
            if (PhysicalBridge.DataForm.DataFormRightFootway.Checked)
            {
                slabEnvelopes.Worksheets["CHO"].Cells[row, 3] = CantileverMomentByUniformLoad(0.0, 5.00, leftCantileverMax - double.Parse(PhysicalBridge.DataForm.DataFormRightFootwayEnd.Text), leftCantileverMax - double.Parse(PhysicalBridge.DataForm.DataFormRightFootwayStart.Text)); row++;
                slabEnvelopes.Worksheets["CHO"].Cells[row, 3] = SpanMomentByUniformLoad(0.5 * DesignSpanLength, 5.00, double.Parse(PhysicalBridge.DataForm.DataFormRightFootwayEnd.Text) - spanMin, double.Parse(PhysicalBridge.DataForm.DataFormRightFootwayStart.Text) - spanMin); row++;
                slabEnvelopes.Worksheets["CHO"].Cells[row, 3] = CantileverMomentByUniformLoad(0.0, 5.00, double.Parse(PhysicalBridge.DataForm.DataFormRightFootwayEnd.Text) - rightCantileverMin, double.Parse(PhysicalBridge.DataForm.DataFormRightFootwayStart.Text) - rightCantileverMin);
            }

            //LM1 (UDL + TS):
            if (PhysicalBridge.DataForm.DataFormCarriageway.Checked)
            {
                RectangularFootprintLoad tsLoad = new RectangularFootprintLoad(0.400, 0.400, 100.0);
                tsLoad = tsLoad.Disperse(double.Parse(PhysicalBridge.DataForm.DataFormRoadThickness.Text) + 0.5 * G6, 45.0);

                int loadSteps = (int) Math.Ceiling((carriagewayWidth - lanes.Sum()) / movingLoadStep) + 1;
                double loadStrip = (carriagewayWidth - lanes.Sum()) / (loadSteps - 1);
                double loadStartLoc = double.Parse(PhysicalBridge.DataForm.DataFormCarriagewayStart.Text);

                for (int i = 0; i < loadSteps; i++)
                {
                    row = startRow; m = new List<double> { 0.0, 0.0, 0.0 };

                    Lane leftRemainingArea = new Lane();
                    Lane rightRemainingArea = new Lane();
                    if (remainingArea > 0)
                    {
                        leftRemainingArea = new Lane(double.Parse(PhysicalBridge.DataForm.DataFormCarriagewayStart.Text), loadStartLoc - double.Parse(PhysicalBridge.DataForm.DataFormCarriagewayStart.Text));
                        rightRemainingArea = new Lane(leftRemainingArea.End + lanes.Sum(), double.Parse(PhysicalBridge.DataForm.DataFormCarriagewayEnd.Text) - (leftRemainingArea.End + lanes.Sum()));
                        if (rightRemainingArea.Width < 0.0) break;

                        m[0] += CantileverMomentByUniformLoad(0.0, 1.00, leftCantileverMax - leftRemainingArea.End, leftCantileverMax - leftRemainingArea.Start);
                        m[0] += CantileverMomentByUniformLoad(0.0, 1.00, leftCantileverMax - rightRemainingArea.End, leftCantileverMax - rightRemainingArea.Start);

                        m[1] += SpanMomentByUniformLoad(0.5 * DesignSpanLength, 1.00, leftRemainingArea.Start - spanMin, leftRemainingArea.End - spanMin);
                        m[1] += SpanMomentByUniformLoad(0.5 * DesignSpanLength, 1.00, rightRemainingArea.Start - spanMin, rightRemainingArea.End - spanMin);

                        m[2] += SpanMomentByUniformLoad(0.0, 1.00, leftRemainingArea.Start - rightCantileverMin, leftRemainingArea.End - rightCantileverMin);
                        m[2] += SpanMomentByUniformLoad(0.0, 1.00, rightRemainingArea.Start - rightCantileverMin, rightRemainingArea.End - rightCantileverMin);

                        slabEnvelopes.Worksheets["UDL-QR"].Cells[row, 2 + i] = m[0]; row++;
                        slabEnvelopes.Worksheets["UDL-QR"].Cells[row, 2 + i] = m[1]; row++;
                        slabEnvelopes.Worksheets["UDL-QR"].Cells[row, 2 + i] = m[2];
                    }

                    double totalWidth = 0.0;
                    for (int j = 0; j < lanes.Count(); j++)
                    {
                        Lane lane = new Lane(loadStartLoc + totalWidth, lanes[j]);
                        row = startRow; m = new List<double> { 0.0, 0.0, 0.0 };

                        m[0] = CantileverMomentByUniformLoad(0.0, 1.00, leftCantileverMax - lane.End, leftCantileverMax - lane.Start);
                        m[1] = SpanMomentByUniformLoad(0.5 * DesignSpanLength, 1.00, lane.Start - spanMin, lane.End - spanMin);
                        m[2] = CantileverMomentByUniformLoad(0.0, 1.00, lane.Start - rightCantileverMin, lane.End - rightCantileverMin);

                        slabEnvelopes.Worksheets["UDL-" + (j + 1).ToString()].Cells[row, 2 + i] = m[0]; row++;
                        slabEnvelopes.Worksheets["UDL-" + (j + 1).ToString()].Cells[row, 2 + i] = m[1]; row++;
                        slabEnvelopes.Worksheets["UDL-" + (j + 1).ToString()].Cells[row, 2 + i] = m[2];

                        row = startRow; m = new List<double> { 0.0, 0.0, 0.0 };
                        for (int ts = 0; ts < 2; ts++)
                        {
                            double loc, bm;
                            if (ts == 0) loc = loadStartLoc + totalWidth + 0.5 * lanes[j] - 1.000;
                            else loc = loadStartLoc + totalWidth + 0.5 * lanes[j] + 1.000;

                            //Left cantilever:
                            if (leftCantileverMax - loc - 0.5 * tsLoad.Width > 0)
                            {
                                bm = tsLoad.CantileverEffectiveWidth(leftCantileverMax - loc);
                                if (bm > 1.200)
                                {
                                    bm += 1.200;
                                    m[0] += CantileverMomentByUniformLoad(0.0, 2 * tsLoad.Distributed, leftCantileverMax - loc - 0.5 * tsLoad.Width, leftCantileverMax - loc + 0.5 * tsLoad.Width) / bm;
                                }
                                else m[0] += CantileverMomentByUniformLoad(0.0, tsLoad.Distributed, leftCantileverMax - loc - 0.5 * tsLoad.Width, leftCantileverMax - loc + 0.5 * tsLoad.Width) / bm;
                            }
                            else
                            {
                                double loadLength = 0.5 * tsLoad.Width + leftCantileverMax - loc;
                                if (loadLength < 0.0) loadLength = 0.0;

                                bm = tsLoad.CantileverEffectiveWidth(0.5 * loadLength);
                                if (bm > 1.200)
                                {
                                    bm += 1.200;
                                    m[0] += CantileverMomentByUniformLoad(0.0, 2* tsLoad.Distributed, 0.0, loadLength) / bm;
                                }
                                else m[0] += CantileverMomentByUniformLoad(0.0, tsLoad.Distributed, 0.0, loadLength) / bm;
                            }

                            //Span:
                            if (loc - spanMin - 0.5 * tsLoad.Width < 0)
                            {
                                double loadLength = 0.5 * tsLoad.Width + loc - spanMin;
                                if (loadLength < 0.0) loadLength = 0.0;

                                bm = tsLoad.SpanEffectiveWidth(0.5 * loadLength, DesignSpanLength);
                                if (bm > 1.200)
                                {
                                    bm += 1.200;
                                    m[1] += SpanMomentByUniformLoad(0.5 * DesignSpanLength, 2 * tsLoad.Distributed, 0.0, loadLength) / bm;
                                }
                                else m[1] += SpanMomentByUniformLoad(0.5 * DesignSpanLength, tsLoad.Distributed, 0.0, loadLength) / bm;
                            }
                            else if (loc - spanMin + 0.5 * tsLoad.Width < DesignSpanLength)
                            {
                                bm = tsLoad.SpanEffectiveWidth(loc - spanMin, DesignSpanLength);
                                if (bm > 1.200)
                                {
                                    bm += 1.200;
                                    m[1] += SpanMomentByUniformLoad(0.5 * DesignSpanLength, 2 * tsLoad.Distributed, loc - spanMin - 0.5 * tsLoad.Width, loc - spanMin + 0.5 * tsLoad.Width) / bm;
                                }
                                else m[1] += SpanMomentByUniformLoad(0.5 * DesignSpanLength, tsLoad.Distributed, loc - spanMin - 0.5 * tsLoad.Width, loc - spanMin + 0.5 * tsLoad.Width) / bm;
                            }
                            else
                            {
                                double loadLength = 0.5 * tsLoad.Width + DesignSpanLength + spanMin - loc;
                                if (loadLength < 0.0) loadLength = 0.0;

                                bm = tsLoad.SpanEffectiveWidth(0.5 * loadLength, DesignSpanLength);
                                if (bm > 1.200)
                                {
                                    bm += 1.200;
                                    m[1] += SpanMomentByUniformLoad(0.5 * DesignSpanLength, 2 * tsLoad.Distributed, DesignSpanLength - loadLength, DesignSpanLength) / bm;
                                }
                                else m[1] += SpanMomentByUniformLoad(0.5 * DesignSpanLength, tsLoad.Distributed, DesignSpanLength - loadLength, DesignSpanLength) / bm;
                            }

                            //Right cantilever:
                            if (loc - rightCantileverMin - 0.5 * tsLoad.Width > 0)
                            {
                                bm = tsLoad.CantileverEffectiveWidth(loc - rightCantileverMin);
                                if (bm > 1.200)
                                {
                                    bm += 1.200;
                                    m[2] += CantileverMomentByUniformLoad(0.0, 2 * tsLoad.Distributed, loc - rightCantileverMin - 0.5 * tsLoad.Width, loc - rightCantileverMin + 0.5 * tsLoad.Width) / bm;
                                }
                                else m[2] += CantileverMomentByUniformLoad(0.0, tsLoad.Distributed, loc - rightCantileverMin - 0.5 * tsLoad.Width, loc - rightCantileverMin + 0.5 * tsLoad.Width) / bm;
                            }
                            else
                            {
                                double loadLength = 0.5 * tsLoad.Width + loc - rightCantileverMin;
                                if (loadLength < 0.0) loadLength = 0.0;

                                bm = tsLoad.CantileverEffectiveWidth(0.5 * loadLength);
                                if (bm > 1.200)
                                {
                                    bm += 1.200;
                                    m[2] += CantileverMomentByUniformLoad(0.0, 2 * tsLoad.Distributed, 0.0, loadLength) / bm;
                                }
                                else m[2] += CantileverMomentByUniformLoad(0.0, tsLoad.Distributed, 0.0, loadLength) / bm;
                            }
                        }
                        slabEnvelopes.Worksheets["TS-" + (j + 1).ToString()].Cells[row, 2 + i] = m[0]; row++;
                        slabEnvelopes.Worksheets["TS-" + (j + 1).ToString()].Cells[row, 2 + i] = m[1]; row++;
                        slabEnvelopes.Worksheets["TS-" + (j + 1).ToString()].Cells[row, 2 + i] = m[2];

                        totalWidth += lanes[j];
                    }

                    if (remainingArea == 0) break;
                    loadStartLoc += loadStrip;
                }
            }

            //LM2:
            if (PhysicalBridge.DataForm.DataFormCarriageway.Checked)
            {
                RectangularFootprintLoad bLoad = new RectangularFootprintLoad(0.350, 0.600, 200.0);

                int loadSteps = (int) Math.Ceiling((carriagewayWidth - bLoad.Width - 2.000) / movingLoadStep) + 1;
                double loadLocInc = (carriagewayWidth - bLoad.Width - 2.000) / (loadSteps - 1);
                double loadLoc = double.Parse(PhysicalBridge.DataForm.DataFormCarriagewayStart.Text) + 0.5 * bLoad.Width;

                bLoad = bLoad.Disperse(double.Parse(PhysicalBridge.DataForm.DataFormRoadThickness.Text) + 0.5 * G6, 45.0);

                for (int i = 0; i < loadSteps; i++)
                {
                    row = startRow; m = new List<double> { 0.0, 0.0, 0.0 };
                    for (int j = 0; j < 2; j++)
                    {
                        double loc, bm;
                        if (j == 0) loc = loadLoc;
                        else loc = loadLoc + 2.000;

                        //Left cantilever:
                        if (leftCantileverMax - loc - 0.5 * bLoad.Width > 0)
                        {
                            bm = bLoad.CantileverEffectiveWidth(leftCantileverMax - loc);
                            m[0] += CantileverMomentByUniformLoad(0.0, bLoad.Distributed, leftCantileverMax - loc - 0.5 * bLoad.Width, leftCantileverMax - loc + 0.5 * bLoad.Width) / bm;
                        }
                        else
                        {
                            double loadLength = 0.5 * bLoad.Width + leftCantileverMax - loc;
                            if (loadLength < 0.0) loadLength = 0.0;

                            bm = bLoad.CantileverEffectiveWidth(0.5 * loadLength);
                            m[0] += CantileverMomentByUniformLoad(0.0, bLoad.Distributed, 0.0, loadLength) / bm;
                        }
                    
                        //Span:
                        if (loc - spanMin - 0.5 * bLoad.Width < 0)
                        {
                            double loadLength = 0.5 * bLoad.Width + loc - spanMin;
                            if (loadLength < 0.0) loadLength = 0.0;

                            bm = bLoad.SpanEffectiveWidth(0.5 * loadLength, DesignSpanLength);
                            m[1] += SpanMomentByUniformLoad(0.5 * DesignSpanLength, bLoad.Distributed, 0.0, loadLength) / bm;
                        }
                        else if (loc - spanMin + 0.5 * bLoad.Width < DesignSpanLength)
                        {
                            bm = bLoad.SpanEffectiveWidth(loc - spanMin, DesignSpanLength);
                            m[1] += SpanMomentByUniformLoad(0.5 * DesignSpanLength, bLoad.Distributed, loc - spanMin - 0.5 * bLoad.Width, loc - spanMin + 0.5 * bLoad.Width) / bm;
                        }
                        else
                        {
                            double loadLength = 0.5 * bLoad.Width + DesignSpanLength + spanMin - loc;
                            if (loadLength < 0.0) loadLength = 0.0;

                            bm = bLoad.SpanEffectiveWidth(0.5 * loadLength, DesignSpanLength);
                            m[1] += SpanMomentByUniformLoad(0.5 * DesignSpanLength, bLoad.Distributed, DesignSpanLength - loadLength, DesignSpanLength) / bm;
                        }

                        //Right cantilever:
                        if (loc - rightCantileverMin - 0.5 * bLoad.Width > 0)
                        {
                            bm = bLoad.CantileverEffectiveWidth(loc - rightCantileverMin);
                            m[2] += CantileverMomentByUniformLoad(0.0, bLoad.Distributed, loc - rightCantileverMin - 0.5 * bLoad.Width, loc - rightCantileverMin + 0.5 * bLoad.Width) / bm;
                        }
                        else
                        {
                            double loadLength = 0.5 * bLoad.Width + loc - rightCantileverMin;
                            if (loadLength < 0.0) loadLength = 0.0;

                            bm = bLoad.CantileverEffectiveWidth(0.5 * loadLength);
                            m[2] += CantileverMomentByUniformLoad(0.0, bLoad.Distributed, 0.0, loadLength) / bm;
                        }
                    }
                    slabEnvelopes.Worksheets["LM2"].Cells[row, 2 + i] = m[0]; row++;
                    slabEnvelopes.Worksheets["LM2"].Cells[row, 2 + i] = m[1]; row++;
                    slabEnvelopes.Worksheets["LM2"].Cells[row, 2 + i] = m[2];

                    loadLoc += loadLocInc;
                }
            }
            SpanMoments = new double[Enum.GetNames(typeof(SlabCombinations)).Length];
            CantileverMoments = new double[Enum.GetNames(typeof(SlabCombinations)).Length];

            double simplifiedSpanMoment; int combinationCol = 10;
            for (int i = 0; i < Enum.GetNames(typeof(SlabCombinations)).Length; i++)
            {
                simplifiedSpanMoment = slabEnvelopes.Worksheets["Podsumowanie"].Cells[6, combinationCol].Value;
                SpanMoments[i] = 0.60 * simplifiedSpanMoment;
                CantileverMoments[i] = -0.75 * simplifiedSpanMoment + Math.Min(Math.Min(slabEnvelopes.Worksheets["Podsumowanie"].Cells[12, combinationCol].Value, slabEnvelopes.Worksheets["Podsumowanie"].Cells[14, combinationCol].Value), 0);
                combinationCol++;
            }
            slabEnvelopes.Close(true);
            excel.Quit();
        }

        private void ClearEnvelopes(Workbook wb)
        {
            int maxSteps = 200;
            ((Range) wb.Worksheets["CWK"].Range[wb.Worksheets["CWK"].Cells[startRow, 2], wb.Worksheets["CWK"].Cells[startRow + 2, 2]]).ClearContents();
            ((Range) wb.Worksheets["CWW"].Range[wb.Worksheets["CWW"].Cells[startRow, 2], wb.Worksheets["CWW"].Cells[startRow + 2, 2]]).ClearContents();
            ((Range) wb.Worksheets["CHO"].Range[wb.Worksheets["CHO"].Cells[startRow, 2], wb.Worksheets["CHO"].Cells[startRow + 2, 3]]).ClearContents();
            
            ((Range) wb.Worksheets["UDL-1"].Range[wb.Worksheets["UDL-1"].Cells[startRow, 2], wb.Worksheets["UDL-1"].Cells[startRow + 2, maxSteps + 1]]).ClearContents();
            ((Range) wb.Worksheets["UDL-2"].Range[wb.Worksheets["UDL-2"].Cells[startRow, 2], wb.Worksheets["UDL-2"].Cells[startRow + 2, maxSteps + 1]]).ClearContents();
            ((Range) wb.Worksheets["UDL-3"].Range[wb.Worksheets["UDL-3"].Cells[startRow, 2], wb.Worksheets["UDL-3"].Cells[startRow + 2, maxSteps + 1]]).ClearContents();

            ((Range) wb.Worksheets["UDL-QR"].Range[wb.Worksheets["UDL-QR"].Cells[startRow, 2], wb.Worksheets["UDL-QR"].Cells[startRow + 2, maxSteps + 1]]).ClearContents();

            ((Range) wb.Worksheets["TS-1"].Range[wb.Worksheets["TS-1"].Cells[startRow, 2], wb.Worksheets["TS-1"].Cells[startRow + 2, maxSteps + 1]]).ClearContents();
            ((Range) wb.Worksheets["TS-2"].Range[wb.Worksheets["TS-2"].Cells[startRow, 2], wb.Worksheets["TS-2"].Cells[startRow + 2, maxSteps + 1]]).ClearContents();
            ((Range) wb.Worksheets["TS-3"].Range[wb.Worksheets["TS-3"].Cells[startRow, 2], wb.Worksheets["TS-3"].Cells[startRow + 2, maxSteps + 1]]).ClearContents();

            ((Range) wb.Worksheets["LM2"].Range[wb.Worksheets["LM2"].Cells[startRow, 2], wb.Worksheets["LM2"].Cells[startRow + 2, maxSteps + 1]]).ClearContents();
        }

        public void CalculateLimitStates(double mEd, double mEk, double crossSectionHeight, bool isSupport, ref List<UltimateLimitState> ultimateLimitStates, ref List<ServiceabilityLimitState> serviceabilityLimitStates)
        {
            double crossSectionWidth = 1.000;
            double minimumReinforcementArea = new DiscreteCrossSection(crossSectionHeight, crossSectionWidth, PhysicalBridge.Concrete).GetMinimumReinforcementArea(crossSectionWidth, crossSectionHeight);

            bool capacityAssured = false; int layers = 1;
            while (capacityAssured == false)
            {
                foreach (int diameter in PhysicalBridge.RebarDiameters)
                {
                    foreach (int spacing in RebarSpacing)
                    {
                        if (crossSectionWidth / (0.001 * spacing) * 0.25 * Math.PI * Math.Pow(0.001 * diameter, 2) >= minimumReinforcementArea)
                        {
                            DiscreteCrossSection cs = new DiscreteCrossSection(crossSectionHeight, crossSectionWidth, PhysicalBridge.Concrete);
                            if (isSupport)
                            {
                                cs.ReinforcementLayers.Add(new Reinforcement(crossSectionWidth / (0.001 * spacing), PhysicalBridge.RebarDiameters.Min(), 0.001 * (PhysicalBridge.Cover + 0.5 * PhysicalBridge.RebarDiameters.Min()), new ReinforcingSteel()));
                                if (layers > 1)
                                {
                                    double s = spacing == 200 ? 100 : spacing;
                                    for (int i = 1; i < layers - 1; i++) cs.ReinforcementLayers.Add(new Reinforcement(crossSectionWidth / (0.001 * s), PhysicalBridge.RebarDiameters.Max(), crossSectionHeight - 0.001 * (PhysicalBridge.Cover + 2 * (i - 1) * PhysicalBridge.RebarDiameters.Max() + 0.5 * PhysicalBridge.RebarDiameters.Max()), new ReinforcingSteel()));
                                }
                                cs.ReinforcementLayers.Add(new Reinforcement(crossSectionWidth / (0.001 * spacing), diameter, crossSectionHeight - 0.001 * (PhysicalBridge.Cover + 2 * (layers - 1) * PhysicalBridge.RebarDiameters.Max() + 0.5 * diameter), new ReinforcingSteel()));
                            }
                            else
                            {
                                cs.ReinforcementLayers.Add(new Reinforcement(crossSectionWidth / (0.001 * spacing), PhysicalBridge.RebarDiameters.Min(), crossSectionHeight - 0.001 * (PhysicalBridge.Cover + 0.5 * PhysicalBridge.RebarDiameters.Min()), new ReinforcingSteel()));
                                if (layers > 1)
                                {
                                    double s = spacing == 200 ? 100 : spacing;
                                    for (int i = 1; i < layers - 1; i++) cs.ReinforcementLayers.Add(new Reinforcement(crossSectionWidth / (0.001 * s), PhysicalBridge.RebarDiameters.Max(), 0.001 * (PhysicalBridge.Cover + 2 * (i - 1) * PhysicalBridge.RebarDiameters.Max() + 0.5 * PhysicalBridge.RebarDiameters.Max()), new ReinforcingSteel()));
                                }
                                cs.ReinforcementLayers.Add(new Reinforcement(crossSectionWidth / (0.001 * spacing), diameter, 0.001 * (PhysicalBridge.Cover + 2 * (layers - 1) * PhysicalBridge.RebarDiameters.Max() + 0.5 * diameter), new ReinforcingSteel()));
                            }
                            ultimateLimitStates.Add(new UltimateLimitState(0.0, mEd, cs));
                            serviceabilityLimitStates.Add(new ServiceabilityLimitState(0.0, mEk, cs, PhysicalBridge.Cover));

                            if ((ultimateLimitStates.Last().State == LimitState.Assured) && (serviceabilityLimitStates.Last().State == LimitState.Assured))
                                capacityAssured = true;
                        }
                        else
                        {
                            ultimateLimitStates.Add(null);
                            serviceabilityLimitStates.Add(null);
                        }
                    }

                    //Mixed arrangement:
                    if (layers > 1) continue;
                    if (diameter < PhysicalBridge.RebarDiameters.Max())
                    {
                        int mixedDiameter = PhysicalBridge.RebarDiameters[PhysicalBridge.RebarDiameters.ToList().IndexOf(diameter) + 1];
                        foreach (int spacing in RebarSpacing)
                        {
                            int mixedSpacing = 2 * spacing;
                            if (crossSectionWidth / (0.001 * mixedSpacing) * 0.25 * Math.PI * Math.Pow(0.001 * mixedDiameter, 2) + crossSectionWidth / (0.001 * mixedSpacing) * 0.25 * Math.PI * Math.Pow(0.001 * diameter, 2)  >= minimumReinforcementArea)
                            {
                                DiscreteCrossSection cs = new DiscreteCrossSection(crossSectionHeight, crossSectionWidth, PhysicalBridge.Concrete);
                                if (isSupport)
                                {
                                    cs.ReinforcementLayers.Add(new Reinforcement(crossSectionWidth / (0.001 * spacing), PhysicalBridge.RebarDiameters.Min(), 0.001 * (PhysicalBridge.Cover + 0.5 * PhysicalBridge.RebarDiameters.Min()), new ReinforcingSteel()));
                                    cs.ReinforcementLayers.Add(new Reinforcement(crossSectionWidth / (0.001 * mixedSpacing), diameter, crossSectionHeight - 0.001 * (PhysicalBridge.Cover + 0.5 * diameter), new ReinforcingSteel()));
                                    cs.ReinforcementLayers.Add(new Reinforcement(crossSectionWidth / (0.001 * mixedSpacing), mixedDiameter, crossSectionHeight - 0.001 * (PhysicalBridge.Cover + 0.5 * mixedDiameter), new ReinforcingSteel()));
                                }
                                else
                                {
                                    cs.ReinforcementLayers.Add(new Reinforcement(crossSectionWidth / (0.001 * spacing), PhysicalBridge.RebarDiameters.Min(), crossSectionHeight - 0.001 * (PhysicalBridge.Cover + 0.5 * PhysicalBridge.RebarDiameters.Min()), new ReinforcingSteel()));
                                    cs.ReinforcementLayers.Add(new Reinforcement(crossSectionWidth / (0.001 * mixedSpacing), diameter, 0.001 * (PhysicalBridge.Cover + 0.5 * diameter), new ReinforcingSteel()));
                                    cs.ReinforcementLayers.Add(new Reinforcement(crossSectionWidth / (0.001 * mixedSpacing), mixedDiameter, 0.001 * (PhysicalBridge.Cover + 0.5 * mixedDiameter), new ReinforcingSteel()));
                                }
                                ultimateLimitStates.Add(new UltimateLimitState(0.0, mEd, cs));
                                serviceabilityLimitStates.Add(new ServiceabilityLimitState(0.0, mEk, cs, PhysicalBridge.Cover));

                                if ((ultimateLimitStates.Last().State == LimitState.Assured) && (serviceabilityLimitStates.Last().State == LimitState.Assured))
                                    capacityAssured = true;
                            }
                            else
                            {
                                ultimateLimitStates.Add(null);
                                serviceabilityLimitStates.Add(null);
                            }
                        }
                    }
                }

                if (capacityAssured == true) break;
                else layers++;
            }
            return;
        }

        public SlabRebarsArrangement SlabRebarsArrangement(List<UltimateLimitState> spanUltimateLimitStates, List<UltimateLimitState> supportUltimateLimitStates, List<ServiceabilityLimitState> spanServiceabilityLimitStates, List<ServiceabilityLimitState> supportServiceabilityLimitStates)
        {
            int spanLimitStates = spanUltimateLimitStates.Count();
            int supportLimitStates = supportUltimateLimitStates.Count();

            List<SlabRebarsArrangement> SlabRebarsArrangements = new List<SlabRebarsArrangement>();
            for (int i = 0; i < spanLimitStates; i++)
            {
                if ((spanUltimateLimitStates[i] == null) || (spanServiceabilityLimitStates[i] == null)) continue;
                if ((spanUltimateLimitStates[i].State == LimitState.Assured) && (spanServiceabilityLimitStates[i].State == LimitState.Assured))
                {
                    for (int j = 0; j < supportLimitStates; j++)
                    {
                        if ((supportUltimateLimitStates[j] == null) || (supportServiceabilityLimitStates[j] == null)) continue;
                        if ((supportUltimateLimitStates[j].State == LimitState.Assured) && (supportServiceabilityLimitStates[j].State == LimitState.Assured))
                        {
                            double spanSpacing = 1000 / spanUltimateLimitStates[i].DiscreteCrossSection.ReinforcementLayers.First().NumberOfRebars;
                            double supportSpacing = 1000 / supportUltimateLimitStates[j].DiscreteCrossSection.ReinforcementLayers.First().NumberOfRebars;

                            if ((Math.Round(spanSpacing - supportSpacing, 3) == 0) || (Math.Round(2 * spanSpacing - supportSpacing, 3) == 0) || (Math.Round(spanSpacing - 2 * supportSpacing, 3) == 0))
                            {
                                SlabRebarsArrangements.Add(new SlabRebarsArrangement(this, spanUltimateLimitStates[i].DiscreteCrossSection.ReinforcementLayers, supportUltimateLimitStates[j].DiscreteCrossSection.ReinforcementLayers));
                            }
                        }
                    }
                }
            }
            SlabRebarsArrangements = SlabRebarsArrangements.OrderBy(a => a.Mass).ToList();
            SlabRebarsArrangement SlabRebarsArrangement = SlabRebarsArrangements.First();

#if SLABREBARS
            using (StreamWriter slabArrFile = new StreamWriter(PhysicalBridge.Directory + Globals.TextFiles.SlabRebarsArrangement, false))
            {
                string strTitle = "R1.n\tR1.f\tR1.s\tR1.L\tR2.n\tR2.f\tR2.s\tR2.L\tR3.n\tR3.f\tR3.s\tR3.L\tA\tL.nom\tM";
                for (int i = 0; i <= 7; i++)
                {
                    string title = "";
                    double reinforcementLength = 0.0; double d = 0.0;
                    List<Reinforcement> reinforcementLayers = new List<Reinforcement>();

                    switch (i)
                    {
                        case 0:
                            title = "Bottom, left cantilever:";
                            reinforcementLength = SlabRebarsArrangement.BottomCantileverRebarsLength[(int) Cantilever.Left];
                            reinforcementLayers = new List<Reinforcement> { SlabRebarsArrangement.BottomCantileverRebars[(int) Cantilever.Left] };

                            d = 1 * 0.001 * 40;
                            break;
                        case 1:
                            title = "Bottom, left thickening:";
                            reinforcementLength = SlabRebarsArrangement.BottomSpanAdditionalRebarsLength[(int) Cantilever.Left];
                            reinforcementLayers = new List<Reinforcement> { SlabRebarsArrangement.BottomSpanAdditionalRebars[(int) Cantilever.Left] };

                            d = 2 * 0.001 * 40;
                            break;
                        case 2:
                            title = "Bottom, span:";
                            reinforcementLength = SlabRebarsArrangement.BottomSpanRebarsLength;
                            reinforcementLayers = SlabRebarsArrangement.BottomSpanRebars;

                            d = 2 * 0.001 * 40;
                            break;
                        case 3:
                            title = "Bottom, right thickening:";
                            reinforcementLength = SlabRebarsArrangement.BottomSpanAdditionalRebarsLength[(int) Cantilever.Right];
                            reinforcementLayers = new List<Reinforcement> { SlabRebarsArrangement.BottomSpanAdditionalRebars[(int) Cantilever.Right] };

                            d = 2 * 0.001 * 40;
                            break;
                        case 4:
                            title = "Bottom, right cantilever:";
                            reinforcementLength = SlabRebarsArrangement.BottomCantileverRebarsLength[(int) Cantilever.Right];
                            reinforcementLayers = new List<Reinforcement> { SlabRebarsArrangement.BottomCantileverRebars[(int) Cantilever.Right] };

                            d = 1 * 0.001 * 40;
                            break;
                        case 5:
                            title = "Top, left cantilever:";
                            reinforcementLength = SlabRebarsArrangement.TopCantileverRebarsLength[(int) Cantilever.Left];
                            reinforcementLayers = SlabRebarsArrangement.TopCantileverRebars;

                            d = 0;
                            break;
                        case 6:
                            title = "Top, span:";
                            reinforcementLength = SlabRebarsArrangement.TopSpanRebarsLength;
                            reinforcementLayers = new List<Reinforcement> { SlabRebarsArrangement.TopSpanRebars };

                            if (reinforcementLength == 0) d = 0;
                            else d = 2 * 0.001 * 40;

                            break;
                        case 7:
                            title = "Top, right cantilever:";
                            reinforcementLength = SlabRebarsArrangement.TopCantileverRebarsLength[(int) Cantilever.Right];
                            reinforcementLayers = SlabRebarsArrangement.TopCantileverRebars;

                            d = 0;
                            break;
                    }

                    double area = 0.0; double mass = 0.0;
                    foreach (Reinforcement reinforcementLayer in reinforcementLayers)
                    {
                        if (reinforcementLayer == null) continue;
                        area += reinforcementLayer.Area * Math.Pow(1000, 2);
                        mass += reinforcementLayer.Area * (reinforcementLength + d * reinforcementLayer.RebarsDiameter) * 7850;
                    }

                    string str = "";
                    for (int j = 0; j < 3; j++)
                    {
                        if (str.Length > 0) str += "\t";
                        try
                        {
                            str += string.Format("{0:0}\t{1:0}\t{2:0.0}\t{3:0.000}", reinforcementLayers[j].NumberOfRebars, reinforcementLayers[j].RebarsDiameter, 1000 / reinforcementLayers[j].NumberOfRebars, reinforcementLength + d * reinforcementLayers[j].RebarsDiameter);
                        }
                        catch { str += string.Format("{0:0}\t{1:0}\t{2:0.0}\t{3:0.000}", 0, 0, 0, 0); }
                    }
                    str += string.Format("\t{0:0.0}\t{1:0.000}\t{2:0.0}\n", area, reinforcementLength, mass);

                    slabArrFile.WriteLine(title);
                    slabArrFile.WriteLine(strTitle);
                    slabArrFile.WriteLine(str);
                }
                slabArrFile.WriteLine(string.Format("Total mass per unit:\t{0:0.0}", SlabRebarsArrangement.Mass));
                slabArrFile.WriteLine(string.Format("Total mass:\t{0:0.0}", SlabRebarsArrangement.Mass * (PhysicalBridge.SpanLength.First() + PhysicalBridge.SpanLength.Last())));
            }
#endif
            return SlabRebarsArrangement;
        }
    }

    public class RectangularFootprintLoad
    {
        //Measured along the road, x:
        public double Length;
        //Measured perpendicually to the road (transversally), y:
        public double Width;
        public double ResultantForce;

        public double Area;
        public double Distributed;
        public double Pressure;

        public RectangularFootprintLoad(double length, double width, double resultantForce)
        {
            Length = length; Width = width;
            ResultantForce = resultantForce;
            
            Area = Width * Length;
            if (Length > 0.0) Distributed = ResultantForce / Width;
            else Distributed = 0.0;
            if (Area > 0.0) Pressure = ResultantForce / Area;
            else Pressure = 0.0;
        }

        public RectangularFootprintLoad Disperse(double depth, double angle)
        {
            //Angle measured in relation to the horizontal plane, degrees
            double d;
            if (angle == 90.0) d = 0.0;
            else d = depth / Math.Tan(Math.PI * angle / 180);

            Length += 2.0 * d; Width += 2.0 * d;

            Area = Width * Length;
            if (Length > 0.0) Distributed = ResultantForce / Width;
            else Distributed = 0.0;
            if (Area > 0.0) Pressure = ResultantForce / Area;
            else Pressure = 0.0;

            return this;
        }

        public double CantileverEffectiveWidth(double x) { return Length + 1.5 * x; }
        public double SpanEffectiveWidth(double x, double l) { return Length + 2.5 * x * (1 - x / l); }
    }

    public class Lane
    {
        public double Start;
        public double Width;
        public double End;

        public Lane(double x, double width)
        {
            Start = x;
            Width = width;

            End = Start + Width;
        }
        public Lane() : this(0.0, 0.0) { }
    }
}