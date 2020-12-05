using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using Point = System.Windows.Point;
using static BridgeOpt.CentralTriangle;

namespace BridgeOpt
{
    public class PhysicalBridge
    {
        public ExternalCommandData CommandData;
        public DataForm DataForm;
        public string Directory;
        public string Name;

        public Element Superstructure;
        public List<Element> Elements = new List<Element>();

        public List<double> SpanLength = new List<double>();
        public CrossSection LeftGirderCrossSection;
        public CrossSection RightGirderCrossSection;
        public Concrete Concrete = new Concrete(40);
        public double Cover; //mm

        public AnalyticalSlab AnalyticalSlab;
        public AnalyticalBridge AnalyticalBridge;
        public AnalyticalBridge AnalyticalPrestressedBridge;
        public AnalyticalGirders AnalyticalGirders;

        public List<double> CriticalCrossSections = new List<double>();
        public List<Point> SuperstructureCrossSection = new List<Point>();
        public int[] RebarDiameters = new[] { 12, 16, 20, 25, 32 };

        public PhysicalBridge(ExternalCommandData commandData)
        {
            CommandData = commandData;
            FilteredElementCollector collector = new FilteredElementCollector(commandData.Application.ActiveUIDocument.Document).WhereElementIsNotElementType();
            foreach (Element element in collector)
            {
                if ((element.Category != null) && element.Category.HasMaterialQuantities) Elements.Add(element);
            }
            Superstructure = FindSuperstructure();
            Cover = 40;

            //Dividing cross section contour into two parts:
            double divisionAxle = 0.5 * Converters.ToMeters(Superstructure.ParametersMap.get_Item("OL").AsDouble() + Superstructure.ParametersMap.get_Item("OP").AsDouble());
            SuperstructureCrossSection = GetFrontalVertices(Superstructure, divisionAxle);

            if (SuperstructureCrossSection.Count > 0)
            {
                List<Point> leftGirderPoints = new List<Point>();
                List<Point> rightGirderPoints = new List<Point>();

                foreach (Point crossSectionPoint in SuperstructureCrossSection)
                {
                    if (Math.Round(crossSectionPoint.X - divisionAxle, 8) <= 0.0) leftGirderPoints.Add(new Point(crossSectionPoint.X, crossSectionPoint.Y));
                    if (Math.Round(crossSectionPoint.X - divisionAxle, 8) >= 0.0) rightGirderPoints.Add(new Point(crossSectionPoint.X, crossSectionPoint.Y));
                }
                if (leftGirderPoints.First() == leftGirderPoints.Last()) leftGirderPoints.RemoveAt(leftGirderPoints.Count() - 1);
                if (rightGirderPoints.First() == rightGirderPoints.Last()) rightGirderPoints.RemoveAt(rightGirderPoints.Count() - 1);

                LeftGirderCrossSection = new CrossSection(leftGirderPoints);
                RightGirderCrossSection = new CrossSection(rightGirderPoints);

                LeftGirderCrossSection.Height = Converters.ToMeters(Superstructure.ParametersMap.get_Item("HL").AsDouble());
                LeftGirderCrossSection.Width = Converters.ToMeters(Superstructure.ParametersMap.get_Item("BL").AsDouble());
                LeftGirderCrossSection.LeftCantileverOverhang = Converters.ToMeters(Superstructure.ParametersMap.get_Item("OL").AsDouble() - Superstructure.ParametersMap.get_Item("BL1").AsDouble() - Superstructure.ParametersMap.get_Item("O1").AsDouble()) - 0.5 * LeftGirderCrossSection.Width;
                LeftGirderCrossSection.RightCantileverOverhang = divisionAxle - Converters.ToMeters(Superstructure.ParametersMap.get_Item("OL").AsDouble() + Superstructure.ParametersMap.get_Item("BL2").AsDouble()) - 0.5 * LeftGirderCrossSection.Width;

                RightGirderCrossSection.Height = Converters.ToMeters(Superstructure.ParametersMap.get_Item("HP").AsDouble());
                RightGirderCrossSection.Width = Converters.ToMeters(Superstructure.ParametersMap.get_Item("BP").AsDouble());
                RightGirderCrossSection.LeftCantileverOverhang = Converters.ToMeters(Superstructure.ParametersMap.get_Item("OP").AsDouble() - Superstructure.ParametersMap.get_Item("BP2").AsDouble()) - divisionAxle - 0.5 * RightGirderCrossSection.Width;
                RightGirderCrossSection.RightCantileverOverhang = Converters.ToMeters(Superstructure.ParametersMap.get_Item("O5").AsDouble() - Superstructure.ParametersMap.get_Item("OP").AsDouble() - Superstructure.ParametersMap.get_Item("BP1").AsDouble()) - 0.5 * RightGirderCrossSection.Width;

                LeftGirderCrossSection.MomentsOfInertia.IS = CalculateTorsionConstant(LeftGirderCrossSection);
                RightGirderCrossSection.MomentsOfInertia.IS = CalculateTorsionConstant(LeftGirderCrossSection);
            }
        }

        private readonly string[] testParameters = new string[] { "G1", "G2", "G3", "G4", "G5" };
        private Element FindSuperstructure()
        {
            foreach (Element element in Elements)
            {
                if (element.Category.Id.IntegerValue == (int) BuiltInCategory.OST_StructuralFraming)
                {
                    bool found = true;
                    List<string> parameterNames = new List<string>();
                    foreach (Parameter parameter in element.ParametersMap)
                    {
                        parameterNames.Add(parameter.Definition.Name);
                    }

                    foreach (string testParameter in testParameters)
                    {
                        if (parameterNames.Contains(testParameter) == false)
                        {
                            found = false;
                            break;
                        }
                    }
                    if (found) return element;
                }
            }
            return null;
        }

        private Face GetFrontalFace(Element element)
        {
            //Returns one of the solid's faces for the cross-section vertices computation:
            if (element.Category.Id.IntegerValue != (int) BuiltInCategory.OST_StructuralFraming)
            {
                throw new Exception("Element is not a type of the structural framing category.");
            }

            GeometryElement geo = ((FamilyInstance) element).GetOriginalGeometry(new Options());
            foreach (Face face in ((Solid) geo.ElementAt(0)).Faces)
            {
                XYZ normalVector = face.ComputeNormal(new UV(face.GetBoundingBox().Min.U, face.GetBoundingBox().Min.V));
                if ((Math.Abs(Math.Round(normalVector.X, 3)) == 1.0) && (Math.Abs(Math.Round(normalVector.Y, 3)) == 0.0) && (Math.Abs(Math.Round(normalVector.Z, 3)) == 0.0))
                {
                    return face;
                }
            }
            return null;
        }

        private List<Point> GetFrontalVertices(Element element, double? additionalDivision = null)
        {
            List<Point> contourPoints = new List<Point>();
            Face frontalFace = GetFrontalFace(element);
            if (frontalFace == null) return contourPoints;

            foreach (EdgeArray edges in frontalFace.EdgeLoops)
            {
                foreach (Edge edge in edges)
                {
                    contourPoints.Add(new Point(Math.Round(Converters.ToMeters(edge.AsCurve().GetEndPoint(0).Y), 8), Math.Round(Converters.ToMeters(edge.AsCurve().GetEndPoint(0).Z), 8)));
                    contourPoints.Add(new Point(Math.Round(Converters.ToMeters(edge.AsCurve().GetEndPoint(1).Y), 8), Math.Round(Converters.ToMeters(edge.AsCurve().GetEndPoint(1).Z), 8)));
                }
            }
            contourPoints.Add(contourPoints.First());

            if (additionalDivision != null)
            {
                for (int i = contourPoints.Count() - 2; i >= 0; i--)
                {
                    if (contourPoints[i].X == contourPoints[i + 1].X) continue;
                    else if (contourPoints[i].X > contourPoints[i + 1].X)
                    {
                        if ((contourPoints[i].X >= additionalDivision) && (contourPoints[i + 1].X <= additionalDivision))
                        {
                            double y = contourPoints[i].Y + ((double) additionalDivision - contourPoints[i].X) * (contourPoints[i + 1].Y - contourPoints[i].Y) / (contourPoints[i + 1].X - contourPoints[i].X);
                            contourPoints.Insert(i + 1, new Point((double) additionalDivision, y));
                        }
                    }
                    else
                    {
                        if ((contourPoints[i].X <= additionalDivision) && (contourPoints[i + 1].X >= additionalDivision))
                        {
                            double y = contourPoints[i].Y + ((double) additionalDivision - contourPoints[i].X) * (contourPoints[i + 1].Y - contourPoints[i].Y) / (contourPoints[i + 1].X - contourPoints[i].X);
                            contourPoints.Insert(i + 1, new Point((double) additionalDivision, y));
                        }
                    }
                }
            }
            contourPoints.RemoveAt(contourPoints.Count() - 1);
            contourPoints = contourPoints.Distinct().ToList();
            return contourPoints;
        }

        private double CalculateTorsionConstant(CrossSection cs)
        {
            double a = 0.5 * Math.Max(cs.Height, cs.Width);
            double b = 0.5 * Math.Min(cs.Height, cs.Width);

            return a * Math.Pow(b, 3) * (16 / 3 - 17 / 5 * b / a * (1 - Math.Pow(b, 4) / (12 * Math.Pow(a, 4))));
        }

        public void PrintSummary()
        {
            using (StreamWriter generalGeometrySummary = new StreamWriter(Directory + Globals.TextFiles.GeneralGoemetrySummary, false))
            {
                generalGeometrySummary.WriteLine("S1\tS2\tS3\tS4\tG1\tG2\tG3\tG4\tG5\tG6\tG7\tG8\tG9\tG10\tG11\tO1\tO2\tO3\tO4\tO5\tOL\tOP\tHL\tHP\tBL1\tBL\tBL2\tBP2\tBP\tBP1\tWL\tWP\tPL\tPP");
                
                generalGeometrySummary.WriteLine(string.Format("{0:0.000}\t{1:0.000}\t{2:0.000}\t{3:0.000}", Superstructure.ParametersMap.get_Item("S1").AsDouble(), Superstructure.ParametersMap.get_Item("S2").AsDouble(), Superstructure.ParametersMap.get_Item("S3").AsDouble(), Superstructure.ParametersMap.get_Item("S4").AsDouble()) +
                    
                    string.Format("\t{0:0.000}\t{1:0.000}\t{2:0.000}\t{3:0.000}\t{4:0.000}\t{5:0.000}\t{6:0.000}\t{7:0.000}\t{8:0.000}\t{9:0.000}\t{10:0.000}",
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("G1").AsDouble()),
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("G2").AsDouble()),
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("G3").AsDouble()),
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("G4").AsDouble()),
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("G5").AsDouble()),
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("G6").AsDouble()),
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("G7").AsDouble()),
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("G8").AsDouble()),
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("G9").AsDouble()),
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("G10").AsDouble()),
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("G11").AsDouble())) +
                    
                    string.Format("\t{0:0.000}\t{1:0.000}\t{2:0.000}\t{3:0.000}\t{4:0.000}",
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("O1").AsDouble()),
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("O2").AsDouble()),
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("O3").AsDouble()),
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("O4").AsDouble()),
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("O5").AsDouble())) +
                    
                    string.Format("\t{0:0.000}\t{1:0.000}\t{2:0.000}\t{3:0.000}\t{4:0.000}\t{5:0.000}\t{6:0.000}\t{7:0.000}\t{8:0.000}\t{9:0.000}",
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("OL").AsDouble()),
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("OP").AsDouble()),
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("HL").AsDouble()),
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("HP").AsDouble()),
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("BL1").AsDouble()),
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("BL").AsDouble()),
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("BL2").AsDouble()),
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("BP2").AsDouble()),
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("BP").AsDouble()),
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("BP1").AsDouble())) +

                    string.Format("\t{0:0.000}\t{1:0.000}\t{2:0.000}\t{3:0.000}",
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("WL").AsDouble()),
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("WP").AsDouble()),
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("PL").AsDouble()),
                        Converters.ToMeters(Superstructure.ParametersMap.get_Item("PP").AsDouble())));

                generalGeometrySummary.WriteLine("\nCross-section (SCR):");
                generalGeometrySummary.WriteLine(ToScr());
                generalGeometrySummary.WriteLine("\nLeft girder, cross-section (SCR):");
                generalGeometrySummary.WriteLine(LeftGirderCrossSection.ToScr());
                generalGeometrySummary.WriteLine("\nRight girder, cross-section (SCR):");
                generalGeometrySummary.WriteLine(RightGirderCrossSection.ToScr());
            }
        }

        public string ToScr(double multiplier = 1000)
        {
            string scr; scr = "_PLINE ";
            foreach (Point vertex in SuperstructureCrossSection)
            {
                scr = scr + (multiplier * vertex.X) + "," + (multiplier * vertex.Y) + " ";
            }
           scr = scr + (multiplier * SuperstructureCrossSection.First().X) + "," + (multiplier * SuperstructureCrossSection.First().Y) + " ";
            return scr + " ";
        }
    }
}