using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Microsoft.Office.Interop.Excel;

namespace BridgeOpt
{
    public enum Extremes
    {
        FZ = 0,
        MX = 1,
        MY = 2
    }
    public enum Phases
    {
        InitialPhase = 0,
        OperationalPhase = 1
    }
    public enum Combinations
    {
        DesignCombination = 0,
        RareCombination = 1,
        FrequentCombination = 2,
        QuasiPermanentCombination = 3
    }
    public enum Girders
    {
        LeftGirder = 0,
        RightGirder = 1
    }

    //Hierachy: Combination [Design, Rare, Frequent, Quasi-permanent] > Phase [Initial, Operational] > Forces [FZ, MX, MY] > Extreme [Min, Max] > Force [FZ, MX, MY]
    public class Force
    {
        public double X;

        public double FZ;
        public double MX;
        public double MY;

        public Force(double fz = 0.0, double mx = 0.0, double my = 0.0, double x = 0.0) { FZ = fz; MX = mx; MY = my; X = x; }
        public double GetForce(Extremes force)
        {
            if (force == Extremes.FZ) return FZ;
            if (force == Extremes.MX) return MX;
            return MY;
        }
        public double GetForce(int force) { return GetForce((Extremes) force); }
    }
    public class Combination
    {
        public class Phase
        {
            public class Forces
            {
                public class Extreme
                {
                    public List<Force> Min;
                    public List<Force> Max;

                    public Extreme()
                    {
                        Min = new List<Force>();
                        Max = new List<Force>();
                    }
                    public void AddMinimum(double fz = 0.0, double mx = 0.0, double my = 0.0, double x = 0.0) { Min.Add(new Force(fz, mx, my, x)); }
                    public void AddMaximum(double fz = 0.0, double mx = 0.0, double my = 0.0, double x = 0.0) { Max.Add(new Force(fz, mx, my, x)); }

                    public void ReplaceRecentMaximum(double fz = 0.0, double mx = 0.0, double my = 0.0)
                    {
                        Max.Last().FZ = fz;
                        Max.Last().MX = mx;
                        Max.Last().MY = my;
                    }
                    public void ReplaceRecentMinimum(double fz = 0.0, double mx = 0.0, double my = 0.0)
                    {
                        Min.Last().FZ = fz;
                        Min.Last().MX = mx;
                        Min.Last().MY = my;
                    }

                    private Force GetForces(double x, List<Force> forces)
                    {
                        foreach (Force force in forces) { if (Math.Round(force.X - x, 6) == 0) return force; }
                        //In case if no exact x was found:
                        for (int i = 0; i < forces.Count() - 1; i++)
                        {
                            if ((forces[i].X <= x) && (forces[i + 1].X >= x)) return forces[i];
                        }
                        return new Force();
                    }
                    public Force GetMinForces(double x) { return GetForces(x, Min); }
                    public Force GetMaxForces(double x) { return GetForces(x, Max); }
                }

                public Extreme FZ;
                public Extreme MX;
                public Extreme MY;

                public Forces()
                {
                    FZ = new Extreme();
                    MX = new Extreme();
                    MY = new Extreme();
                }
                public Extreme GetExtremes(Extremes forceSet)
                {
                    if (forceSet == Extremes.FZ) return FZ;
                    if (forceSet == Extremes.MX) return MX;
                    return MY;
                }
                public Extreme GetExtremes(int forceSet) { return GetExtremes((Extremes) forceSet); }
            }

            public Forces InitialPhase;
            public Forces OperationalPhase;

            public Phase()
            {
                InitialPhase = new Forces();
                OperationalPhase = new Forces();
            }
            public Forces GetPhase(Phases phase)
            {
                if (phase == Phases.InitialPhase) return InitialPhase;
                return OperationalPhase;
            }
            public Forces GetPhase(int phase) { return GetPhase((Phases) phase); }
        }

        public Phase Design;
        public Phase Rare;
        public Phase Frequent;
        public Phase QuasiPermanent;

        public Combination()
        {
            Design = new Phase();
            Rare = new Phase();
            Frequent = new Phase();
            QuasiPermanent = new Phase();
        }
        public Phase GetCombination(Combinations combination)
        {
            if (combination == Combinations.DesignCombination) return Design;
            if (combination == Combinations.RareCombination) return Rare;
            if (combination == Combinations.FrequentCombination) return Frequent;
            return QuasiPermanent;
        }
        public Phase GetCombination(int combination) { return GetCombination((Combinations) combination); }
    }

    public class Envelopes
    {
        private readonly int startRow = 8;
        private int GetEndRow(Worksheet ws)
        {
            int endRow = ws.Cells.SpecialCells(XlCellType.xlCellTypeLastCell).Row;
            return Math.Max(startRow, endRow);
        }

        public Combination LeftGirder = new Combination();
        public Combination RightGirder = new Combination();

        public Envelopes(PhysicalBridge bridge, int[] mobileCases, int step = 1)
        {
            Stopwatch localStopWatch = new Stopwatch(); localStopWatch.Start();
            Application excel = new Application();

            string csvCases = Globals.ExcelFiles.SimpleCasesSheetName();
            excel.Workbooks.OpenText(bridge.Directory + Globals.ExcelFiles.CaseSheetPath(csvCases), DataType: XlTextParsingType.xlDelimited, Semicolon: true, Comma: false, Space: false, Local: true);

            string[] csvMobileCases = new string[mobileCases.Count()]; int index = 0;
            foreach (int mobileCase in mobileCases)
            {
                csvMobileCases[index] = Globals.ExcelFiles.CaseSheetName(mobileCase);
                excel.Workbooks.OpenText(bridge.Directory + Globals.ExcelFiles.CaseSheetPath(csvMobileCases[index]), DataType: XlTextParsingType.xlDelimited, Semicolon: true, Comma: false, Space: false, Local: true);
                
                index++;
            }
            Workbook envelopes = excel.Workbooks.Open(bridge.Directory + Globals.ExcelFiles.GirdersEnvelopes, true);
            using (StreamWriter stopwatchSummary = new StreamWriter(bridge.Directory + Globals.TextFiles.StopwatchSummary, true)) stopwatchSummary.WriteLine("\tEnvelopes, sheets opened:\t\t" + DateTime.Now.ToString() + "\t" + localStopWatch.Elapsed.ToString()); localStopWatch.Restart();

            PrintEnvelopes(envelopes, Phases.InitialPhase, Extremes.MY, bridge.CriticalCrossSections, false);
            PrintEnvelopes(envelopes, Phases.OperationalPhase, Extremes.FZ, step, true); //Design combination only
            PrintEnvelopes(envelopes, Phases.OperationalPhase, Extremes.MX, step, true); //Design combination only
            PrintEnvelopes(envelopes, Phases.OperationalPhase, Extremes.MY, bridge.CriticalCrossSections, false);
            using (StreamWriter stopwatchSummary = new StreamWriter(bridge.Directory + Globals.TextFiles.StopwatchSummary, true)) stopwatchSummary.WriteLine("\tEnvelopes, sheets updated:\t\t" + DateTime.Now.ToString() + "\t" + localStopWatch.Elapsed.ToString()); localStopWatch.Restart();

            envelopes.Close(true);
            excel.Workbooks[csvCases].Close(false);
            foreach (string csvMobileCase in csvMobileCases) excel.Workbooks[csvMobileCase].Close(false);
            excel.Quit();

            using (StreamWriter stopwatchSummary = new StreamWriter(bridge.Directory + Globals.TextFiles.StopwatchSummary, true)) stopwatchSummary.WriteLine("\tEnvelopes, sheets saved and closed:\t" + DateTime.Now.ToString() + "\t" + localStopWatch.Elapsed.ToString());
            localStopWatch.Stop();
        }

        private void PrintEnvelopes(Workbook wb, Phases phase, Extremes force, int step = 1, bool designOnly = false)
        {
            string leftSheetName = Enum.GetName(typeof(Extremes), force) + "-L ";
            string rightSheetName = Enum.GetName(typeof(Extremes), force) + "-P ";
            if (phase == Phases.InitialPhase) { leftSheetName += "F0";  rightSheetName += "F0"; }
            else { leftSheetName += "F2"; rightSheetName += "F2"; }

            Worksheet leftGirderSheet = wb.Worksheets[leftSheetName];
            Worksheet rightGirderSheet = wb.Worksheets[rightSheetName];

            int[] maxColumns; //Columns indices for combination maxima: design, rare, frequent and quasi-permanent, respectively
            int[] minColumns; //Columns indices for combination minima: design, rare, frequent and quasi-permanent, respectively
            int[] offColumns; //Number of factors sets per combination: design, rare, frequent and quasi-permanent, respectively
            if (designOnly)
            {
                maxColumns = new int[1] { 57 };
                minColumns = new int[1] { 96 };
                offColumns = new int[1] { 13 };
            }
            else
            {
                maxColumns = new int[4] { 57, 127, 157, 183 };
                minColumns = new int[4] { 96, 142, 172, 186 };
                offColumns = new int[4] { 13, 5, 5, 1 };
            }

            int endRow = GetEndRow(wb.Worksheets["CWK"]); int combination = 0;
            for (int col = 0; col < maxColumns.Count(); col++)
            {
                Combination.Phase.Forces.Extreme leftGirderSets = LeftGirder.GetCombination(combination).GetPhase(phase).GetExtremes(force);
                Combination.Phase.Forces.Extreme rightGirderSets = RightGirder.GetCombination(combination).GetPhase(phase).GetExtremes(force);
                
                for (int row = startRow; row <= endRow; row += step)
                {
                    double x;

                    x = ((Range) leftGirderSheet.Cells[row, 2]).Value;
                    leftGirderSets.AddMaximum(((Range) leftGirderSheet.Cells[row, maxColumns[combination]]).Value, ((Range) leftGirderSheet.Cells[row, maxColumns[combination] + offColumns[combination]]).Value, ((Range) leftGirderSheet.Cells[row, maxColumns[combination] + 2 * offColumns[combination]]).Value);
                    leftGirderSets.AddMinimum(((Range) leftGirderSheet.Cells[row, minColumns[combination]]).Value, ((Range) leftGirderSheet.Cells[row, minColumns[combination] + offColumns[combination]]).Value, ((Range) leftGirderSheet.Cells[row, minColumns[combination] + 2 * offColumns[combination]]).Value);
                    leftGirderSets.Max.Last().X = x;
                    leftGirderSets.Min.Last().X = x;

                    x = ((Range) rightGirderSheet.Cells[row, 2]).Value;
                    rightGirderSets.AddMaximum(((Range) rightGirderSheet.Cells[row, maxColumns[combination]]).Value, ((Range) rightGirderSheet.Cells[row, maxColumns[combination] + offColumns[combination]]).Value, ((Range) rightGirderSheet.Cells[row, maxColumns[combination] + 2 * offColumns[combination]]).Value);
                    rightGirderSets.AddMinimum(((Range) rightGirderSheet.Cells[row, minColumns[combination]]).Value, ((Range) rightGirderSheet.Cells[row, minColumns[combination] + offColumns[combination]]).Value, ((Range) rightGirderSheet.Cells[row, minColumns[combination] + 2 * offColumns[combination]]).Value);
                    rightGirderSets.Max.Last().X = x;
                    rightGirderSets.Min.Last().X = x;
                }
                combination++;
            }
        }
        private void PrintEnvelopes(Workbook wb, Phases phase, Extremes force, List<double> sections, bool designOnly = false)
        {
            string leftSheetName = Enum.GetName(typeof(Extremes), force) + "-L ";
            string rightSheetName = Enum.GetName(typeof(Extremes), force) + "-P ";
            if (phase == Phases.InitialPhase) { leftSheetName += "F0"; rightSheetName += "F0"; }
            else { leftSheetName += "F2"; rightSheetName += "F2"; }

            Worksheet leftGirderSheet = wb.Worksheets[leftSheetName];
            Worksheet rightGirderSheet = wb.Worksheets[rightSheetName];

            int[] maxColumns; //Columns indices for combination maxima: design, rare, frequent and quasi-permanent, respectively
            int[] minColumns; //Columns indices for combination minima: design, rare, frequent and quasi-permanent, respectively
            int[] offColumns; //Number of factors sets per combination: design, rare, frequent and quasi-permanent, respectively
            if (designOnly)
            {
                maxColumns = new int[1] { 57 };
                minColumns = new int[1] { 96 };
                offColumns = new int[1] { 13 };
            }
            else
            {
                maxColumns = new int[4] { 57, 127, 157, 183 };
                minColumns = new int[4] { 96, 142, 172, 186 };
                offColumns = new int[4] { 13, 5, 5, 1 };
            }

            int endRow = GetEndRow(wb.Worksheets["CWK"]); int combination = 0;
            for (int col = 0; col < maxColumns.Count(); col++)
            {
                Combination.Phase.Forces.Extreme leftGirderSets = LeftGirder.GetCombination(combination).GetPhase(phase).GetExtremes(force);
                Combination.Phase.Forces.Extreme rightGirderSets = RightGirder.GetCombination(combination).GetPhase(phase).GetExtremes(force);

                for (int row = startRow; row <= endRow; row++)
                {
                    foreach (double x in sections)
                    {
                        if (Math.Round(((Range) leftGirderSheet.Cells[row, 2]).Value - x, 3) == 0)
                        {
                            leftGirderSets.AddMaximum(((Range) leftGirderSheet.Cells[row, maxColumns[combination]]).Value, ((Range) leftGirderSheet.Cells[row, maxColumns[combination] + offColumns[combination]]).Value, ((Range) leftGirderSheet.Cells[row, maxColumns[combination] + 2 * offColumns[combination]]).Value);
                            leftGirderSets.AddMinimum(((Range) leftGirderSheet.Cells[row, minColumns[combination]]).Value, ((Range) leftGirderSheet.Cells[row, minColumns[combination] + offColumns[combination]]).Value, ((Range) leftGirderSheet.Cells[row, minColumns[combination] + 2 * offColumns[combination]]).Value);
                            leftGirderSets.Max.Last().X = x;
                            leftGirderSets.Min.Last().X = x;
                        }

                        if (Math.Round(((Range) rightGirderSheet.Cells[row, 2]).Value - x, 3) == 0)
                        {
                            rightGirderSets.AddMaximum(((Range) rightGirderSheet.Cells[row, maxColumns[combination]]).Value, ((Range) rightGirderSheet.Cells[row, maxColumns[combination] + offColumns[combination]]).Value, ((Range) rightGirderSheet.Cells[row, maxColumns[combination] + 2 * offColumns[combination]]).Value);
                            rightGirderSets.AddMinimum(((Range) rightGirderSheet.Cells[row, minColumns[combination]]).Value, ((Range) rightGirderSheet.Cells[row, minColumns[combination] + offColumns[combination]]).Value, ((Range) rightGirderSheet.Cells[row, minColumns[combination] + 2 * offColumns[combination]]).Value);
                            rightGirderSets.Max.Last().X = x;
                            rightGirderSets.Min.Last().X = x;
                        }
                    }
                }
                combination++;
            }
        }

        public Combination GetGirder(Girders girder)
        {
            if (girder == Girders.LeftGirder) return LeftGirder;
            return RightGirder;
        }
        public Combination GetGirder(int girder) { return GetGirder((Girders) girder);  }
    }
}