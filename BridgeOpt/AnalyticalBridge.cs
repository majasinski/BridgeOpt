using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using RobotOM;
using static BridgeOpt.LayoutDefinition;

namespace BridgeOpt
{
    public class AnalyticalContour
    {
        public List<RobotGeoPoint3D> Points;
        public AnalyticalContour()
        {
            Points = new List<RobotGeoPoint3D>();
        }
        public AnalyticalContour(List<RobotGeoPoint3D> points)
        {
            Points = new List<RobotGeoPoint3D>();
            AddPoints(points);
        }

        public void AddPoint(RobotGeoPoint3D point)
        {
            RobotGeoPoint3D copiedPoint = new RobotGeoPoint3D();
            copiedPoint.X = point.X;
            copiedPoint.Y = point.Y;
            copiedPoint.Z = point.Z;

            Points.Add(copiedPoint);
        }
        public void AddPoints(List<RobotGeoPoint3D> points)
        {
            foreach (RobotGeoPoint3D point in points)
            {
                AddPoint(point);
            }
        }
    }

    public class PrestressingCaseResult
    {
        public DiscreteTendon DiscreteTendon;
        public List<double> MY;

        public PrestressingCaseResult(DiscreteTendon discreteTendon)
        {
            DiscreteTendon = discreteTendon;
            MY = new List<double>();
        }
        public void AddResult(double my) { MY.Add(my); }
    }

    public class AnalyticalBridge
    {
        public PhysicalBridge PhysicalBridge;
        public int SpanDivision = 10;

        public List<IRobotNode> LeftGirderNodes = new List<IRobotNode>();
        public List<IRobotNode> RightGirderNodes = new List<IRobotNode>();
        public List<IRobotNode> SupportNodes = new List<IRobotNode>();

        public List<IRobotBar> LeftGirderBars = new List<IRobotBar>();
        public List<IRobotBar> RightGirderBars = new List<IRobotBar>();
        public List<IRobotBar> TransverseBars = new List<IRobotBar>();
        public List<int> Cases = new List<int>();
        public List<int> MobileCases = new List<int>();
        public List<int> MobileRoutes = new List<int>();
        public List<PrestressingCaseResult> PrestressingCaseResults = new List<PrestressingCaseResult>();

        public Envelopes Envelopes;

        private static RobotCodes Robot = null;
        private static RobotProject RobotProject;

        public IRobotObjObject Slab = null;
        public string Path;
        private readonly bool CreateFromExistingFile = false;

        public AnalyticalBridge(PhysicalBridge bridge, int spanDivision = 10, bool isPrestressing = false)
        {
            Stopwatch localStopWatch = new Stopwatch(); localStopWatch.Start();

            Robot = new RobotCodes();
            Robot.InitializeRobot();

            PhysicalBridge = bridge;
            if (isPrestressing)
            {
                Path = PhysicalBridge.Directory + Globals.RobotFiles.PrestressingModel;
                PhysicalBridge.AnalyticalPrestressedBridge = this;
            }
            else
            {
                Path = PhysicalBridge.Directory + Globals.RobotFiles.MainModel;
                PhysicalBridge.AnalyticalBridge = this;
            }
            RobotProject = Robot.Application.Project;

            if (File.Exists(Path))
            {
                RobotProject.Open(Path);
                CreateFromExistingFile = true;
            }
            else
            {
                RobotProject.New(IRobotProjectType.I_PT_SHELL);
                CreateFromExistingFile = false;
            }
            SpanDivision = spanDivision;

            //Nodes and bars definition:
            int girderANodeIndex = 1000;
            int girderBNodeIndex = 2000;
            int transverseBarIndex = 3001;

            RobotProject.Structure.Objects.BeginMultiOperation();
            RobotProject.Structure.Cases.BeginMultiOperation();

            for (int i = 0; i < PhysicalBridge.SpanLength.Count(); i++)
            {
                double length = PhysicalBridge.SpanLength[i];
                double startLength = 0;
                if (i > 0)
                {
                    for (int j = 0; j < i; j++) startLength += PhysicalBridge.SpanLength[j];
                }
                for (int j = 0; j <= SpanDivision; j++)
                {
                    if ((i > 0) && (j == 0))
                    {
                        continue;
                    }
                    RobotProject.Structure.Nodes.Create(girderANodeIndex, startLength + (length / SpanDivision) * j, -1 * Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("OL").AsDouble()), 0.0); //Y axle reversed in Robot
                    RobotProject.Structure.Nodes.Create(girderBNodeIndex, startLength + (length / SpanDivision) * j, -1 * Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("OP").AsDouble()), 0.0); //Y axle reversed in Robot
                    if ((j == 0) || (j == SpanDivision))
                    {
                        SupportNodes.Add(RobotProject.Structure.Nodes.Get(girderANodeIndex) as IRobotNode);
                        SupportNodes.Add(RobotProject.Structure.Nodes.Get(girderBNodeIndex) as IRobotNode);
                    }
                    LeftGirderNodes.Add(RobotProject.Structure.Nodes.Get(girderANodeIndex) as IRobotNode);
                    RightGirderNodes.Add(RobotProject.Structure.Nodes.Get(girderBNodeIndex) as IRobotNode);

                    if (j > 0)
                    {
                        RobotProject.Structure.Bars.Create(girderANodeIndex, girderANodeIndex - 1, girderANodeIndex);
                        RobotProject.Structure.Bars.Create(girderBNodeIndex, girderBNodeIndex - 1, girderBNodeIndex);

                        LeftGirderBars.Add(RobotProject.Structure.Bars.Get(girderANodeIndex) as IRobotBar);
                        RightGirderBars.Add(RobotProject.Structure.Bars.Get(girderBNodeIndex) as IRobotBar);
                    }
                    RobotProject.Structure.Bars.Create(transverseBarIndex, girderANodeIndex, girderBNodeIndex);
                    TransverseBars.Add(RobotProject.Structure.Bars.Get(transverseBarIndex) as IRobotBar);

                    girderANodeIndex++; girderBNodeIndex++;
                    transverseBarIndex++;
                }
            }
            SetBarsCharacteristics(bridge);

            //Supports definition:
            IRobotLabel bearingXYZ = RobotProject.Structure.Labels.Create(IRobotLabelType.I_LT_SUPPORT, "Fixed Bearing");
            IRobotNodeSupportData bearingXYZData = bearingXYZ.Data;
            bearingXYZData.SetFixed(IRobotNodeSupportFixingDirection.I_NSFD_UX, 1);
            bearingXYZData.SetFixed(IRobotNodeSupportFixingDirection.I_NSFD_UY, 1);
            bearingXYZData.SetFixed(IRobotNodeSupportFixingDirection.I_NSFD_UZ, 1);
            bearingXYZData.SetFixed(IRobotNodeSupportFixingDirection.I_NSFD_RX, 0);
            bearingXYZData.SetFixed(IRobotNodeSupportFixingDirection.I_NSFD_RY, 0);
            bearingXYZData.SetFixed(IRobotNodeSupportFixingDirection.I_NSFD_RZ, 0);
            RobotProject.Structure.Labels.Store(bearingXYZ);

            IRobotLabel bearingXZ = RobotProject.Structure.Labels.Create(IRobotLabelType.I_LT_SUPPORT, "Unidirectional Bearing, Y");
            IRobotNodeSupportData bearingXZData = bearingXZ.Data;
            bearingXZData.SetFixed(IRobotNodeSupportFixingDirection.I_NSFD_UX, 1);
            bearingXZData.SetFixed(IRobotNodeSupportFixingDirection.I_NSFD_UY, 0);
            bearingXZData.SetFixed(IRobotNodeSupportFixingDirection.I_NSFD_UZ, 1);
            bearingXZData.SetFixed(IRobotNodeSupportFixingDirection.I_NSFD_RX, 0);
            bearingXZData.SetFixed(IRobotNodeSupportFixingDirection.I_NSFD_RY, 0);
            bearingXZData.SetFixed(IRobotNodeSupportFixingDirection.I_NSFD_RZ, 0);
            RobotProject.Structure.Labels.Store(bearingXZ);

            IRobotLabel bearingYZ = RobotProject.Structure.Labels.Create(IRobotLabelType.I_LT_SUPPORT, "Unidirectional Bearing, X");
            IRobotNodeSupportData bearingYZData = bearingYZ.Data;
            bearingYZData.SetFixed(IRobotNodeSupportFixingDirection.I_NSFD_UX, 0);
            bearingYZData.SetFixed(IRobotNodeSupportFixingDirection.I_NSFD_UY, 1);
            bearingYZData.SetFixed(IRobotNodeSupportFixingDirection.I_NSFD_UZ, 1);
            bearingYZData.SetFixed(IRobotNodeSupportFixingDirection.I_NSFD_RX, 0);
            bearingYZData.SetFixed(IRobotNodeSupportFixingDirection.I_NSFD_RY, 0);
            bearingYZData.SetFixed(IRobotNodeSupportFixingDirection.I_NSFD_RZ, 0);
            RobotProject.Structure.Labels.Store(bearingYZ);

            IRobotLabel bearingZ = RobotProject.Structure.Labels.Create(IRobotLabelType.I_LT_SUPPORT, "Multidirectional Bearing");
            IRobotNodeSupportData bearingZData = bearingZ.Data;
            bearingZData.SetFixed(IRobotNodeSupportFixingDirection.I_NSFD_UX, 0);
            bearingZData.SetFixed(IRobotNodeSupportFixingDirection.I_NSFD_UY, 0);
            bearingZData.SetFixed(IRobotNodeSupportFixingDirection.I_NSFD_UZ, 1);
            bearingZData.SetFixed(IRobotNodeSupportFixingDirection.I_NSFD_RX, 0);
            bearingZData.SetFixed(IRobotNodeSupportFixingDirection.I_NSFD_RY, 0);
            bearingZData.SetFixed(IRobotNodeSupportFixingDirection.I_NSFD_RZ, 0);
            RobotProject.Structure.Labels.Store(bearingZ);

            SupportNodes[0].SetLabel(IRobotLabelType.I_LT_SUPPORT, bearingYZ.Name);
            SupportNodes[1].SetLabel(IRobotLabelType.I_LT_SUPPORT, bearingZ.Name);
            SupportNodes[2].SetLabel(IRobotLabelType.I_LT_SUPPORT, bearingXYZ.Name);
            SupportNodes[3].SetLabel(IRobotLabelType.I_LT_SUPPORT, bearingXZ.Name);
            SupportNodes[4].SetLabel(IRobotLabelType.I_LT_SUPPORT, bearingYZ.Name);
            SupportNodes[5].SetLabel(IRobotLabelType.I_LT_SUPPORT, bearingZ.Name);

            //Shell defition:
            IRobotLabel shellThickness = RobotProject.Structure.Labels.Create(IRobotLabelType.I_LT_PANEL_THICKNESS, "No Thickness");
            IRobotThicknessData shellThicknessData = shellThickness.Data;
            shellThicknessData.ThicknessType = IRobotThicknessType.I_TT_HOMOGENEOUS;
            shellThicknessData.MaterialName = "BETON";
            ((IRobotThicknessHomoData) shellThicknessData.Data).ThickConst = 0.001;

            RobotProject.Structure.Labels.Store(shellThickness);

            RobotPointsArray shellContour = new RobotPointsArray(); shellContour.SetSize(4);
            shellContour.Set(1, 0.0, -1 * Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("O1").AsDouble()), 0.0);
            shellContour.Set(2, 0.0, -1 * Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("O5").AsDouble()), 0.0);
            shellContour.Set(3, PhysicalBridge.SpanLength[0] + PhysicalBridge.SpanLength[1], -1 * Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("O5").AsDouble()), 0.0);
            shellContour.Set(4, PhysicalBridge.SpanLength[0] + PhysicalBridge.SpanLength[1], -1 * Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("O1").AsDouble()), 0.0);

            RobotProject.Structure.Objects.CreateContour(1, shellContour);
            IRobotObjObject shell = (IRobotObjObject) RobotProject.Structure.Objects.Get(1);
            shell.Main.Attribs.Meshed = 1;
            shell.SetLabel(IRobotLabelType.I_LT_PANEL_THICKNESS, shellThickness.Name);
            shell.Update();

            //Meshing parameters:
            shell.Mesh.Params.SurfaceParams.Method.Method = IRobotMeshMethodType.I_MMT_COONS;
            shell.Mesh.Params.SurfaceParams.Method.ForcingRatio = IRobotMeshForcingRatio.I_MFR_FORCED;
            shell.Mesh.Params.SurfaceParams.Generation.Type = IRobotMeshGenerationType.I_MGT_ELEMENT_SIZE;
            shell.Mesh.Params.SurfaceParams.Generation.ElementSize = 0.5 * Math.Min(PhysicalBridge.SpanLength[0], PhysicalBridge.SpanLength[1]) / SpanDivision;
            shell.Mesh.Params.SurfaceParams.Coons.PanelDivisionType = IRobotMeshPanelDivType.I_MPDT_SQUARE_IN_RECT;
            shell.Mesh.Params.SurfaceParams.Coons.ForcingRatio = IRobotMeshForcingRatio.I_MFR_FORCED;
            Slab = shell;

            using (StreamWriter stopwatchSummary = new StreamWriter(PhysicalBridge.Directory + Globals.TextFiles.StopwatchSummary, true)) stopwatchSummary.WriteLine("\tAnalytical bridge, model created:\t" + DateTime.Now.ToString() + "\t" + localStopWatch.Elapsed.ToString()); localStopWatch.Restart();
            if (!isPrestressing) ComputeAnalyticalBridge();
            else ComputeAnalyticalPrestressedBridge(int.Parse(PhysicalBridge.DataForm.DataFormTrainingSetSize.Text) + int.Parse(PhysicalBridge.DataForm.DataFormTestSetSize.Text), double.Parse(PhysicalBridge.DataForm.DataFormPrestressDisc.Text) * (PhysicalBridge.SpanLength.First() + PhysicalBridge.SpanLength.Last()), false);

            if (isPrestressing) PhysicalBridge.AnalyticalPrestressedBridge = this;
            else PhysicalBridge.AnalyticalBridge = this;
        }

        public void SetBarsCharacteristics(PhysicalBridge bridge)
        {
            IRobotLabel girderASection = RobotProject.Structure.Labels.Create(IRobotLabelType.I_LT_BAR_SECTION, "Girder A");
            IRobotBarSectionData girderASectionData = girderASection.Data;
            girderASectionData.Type = IRobotBarSectionType.I_BST_STANDARD;
            girderASectionData.ShapeType = IRobotBarSectionShapeType.I_BSST_UNKNOWN;
            girderASectionData.SetValue(IRobotBarSectionDataValue.I_BSDV_AX, bridge.LeftGirderCrossSection.Area);
            girderASectionData.SetValue(IRobotBarSectionDataValue.I_BSDV_IX, 0.553805813);
            girderASectionData.SetValue(IRobotBarSectionDataValue.I_BSDV_IY, bridge.LeftGirderCrossSection.MomentsOfInertia.IX);
            girderASectionData.SetValue(IRobotBarSectionDataValue.I_BSDV_IZ, bridge.LeftGirderCrossSection.MomentsOfInertia.IY);
            girderASectionData.SetValue(IRobotBarSectionDataValue.I_BSDV_VZ, Math.Abs(Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("HL").AsDouble()) + bridge.LeftGirderCrossSection.Boundaries.Bottom));
            girderASectionData.SetValue(IRobotBarSectionDataValue.I_BSDV_VPZ, Math.Abs(bridge.LeftGirderCrossSection.Boundaries.Bottom));
            girderASectionData.SetValue(IRobotBarSectionDataValue.I_BSDV_VY, Math.Abs(bridge.LeftGirderCrossSection.Boundaries.Right - (Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("OL").AsDouble()) - bridge.LeftGirderCrossSection.GravityCenter.X)));
            girderASectionData.SetValue(IRobotBarSectionDataValue.I_BSDV_VPY, Math.Abs(bridge.LeftGirderCrossSection.Boundaries.Left - (Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("OL").AsDouble()) - bridge.LeftGirderCrossSection.GravityCenter.X)));
            girderASectionData.MaterialName = "BETON";

            IRobotLabel girderBSection = RobotProject.Structure.Labels.Create(IRobotLabelType.I_LT_BAR_SECTION, "Girder B");
            IRobotBarSectionData girderBSectionData = girderBSection.Data;
            girderBSectionData.Type = IRobotBarSectionType.I_BST_STANDARD;
            girderBSectionData.ShapeType = IRobotBarSectionShapeType.I_BSST_UNKNOWN;
            girderBSectionData.SetValue(IRobotBarSectionDataValue.I_BSDV_AX, bridge.RightGirderCrossSection.Area);
            girderBSectionData.SetValue(IRobotBarSectionDataValue.I_BSDV_IX, 0.553758815);
            girderBSectionData.SetValue(IRobotBarSectionDataValue.I_BSDV_IY, bridge.RightGirderCrossSection.MomentsOfInertia.IX);
            girderBSectionData.SetValue(IRobotBarSectionDataValue.I_BSDV_IZ, bridge.RightGirderCrossSection.MomentsOfInertia.IY);
            girderBSectionData.SetValue(IRobotBarSectionDataValue.I_BSDV_VZ, Math.Abs(Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("HP").AsDouble()) + bridge.RightGirderCrossSection.Boundaries.Bottom));
            girderBSectionData.SetValue(IRobotBarSectionDataValue.I_BSDV_VPZ, Math.Abs(bridge.RightGirderCrossSection.Boundaries.Bottom));
            girderBSectionData.SetValue(IRobotBarSectionDataValue.I_BSDV_VY, Math.Abs(bridge.RightGirderCrossSection.Boundaries.Right - (Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("OP").AsDouble()) - bridge.RightGirderCrossSection.GravityCenter.X)));
            girderBSectionData.SetValue(IRobotBarSectionDataValue.I_BSDV_VPY, Math.Abs(bridge.RightGirderCrossSection.Boundaries.Left - (Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("OP").AsDouble()) - bridge.RightGirderCrossSection.GravityCenter.X)));
            girderBSectionData.MaterialName = "BETON";

            IRobotLabel transverseBarsSection = RobotProject.Structure.Labels.Create(IRobotLabelType.I_LT_BAR_SECTION, "Plate");
            IRobotBarSectionData transverseBarsSectionData = transverseBarsSection.Data;
            transverseBarsSectionData.ShapeType = IRobotBarSectionShapeType.I_BSST_CONCR_BEAM_RECT;
            transverseBarsSectionData.Concrete.SetValue(IRobotBarSectionConcreteDataValue.I_BSCDV_BEAM_B, 1.600);
            transverseBarsSectionData.Concrete.SetValue(IRobotBarSectionConcreteDataValue.I_BSCDV_BEAM_H, 0.250);
            transverseBarsSectionData.CalcNonstdGeometry();

            RobotProject.Structure.Labels.Store(girderASection);
            RobotProject.Structure.Labels.Store(girderBSection);
            RobotProject.Structure.Labels.Store(transverseBarsSection);
            foreach (IRobotBar bar in LeftGirderBars)
            {
                bar.SetLabel(IRobotLabelType.I_LT_BAR_SECTION, girderASection.Name);
            }
            foreach (IRobotBar bar in RightGirderBars)
            {
                bar.SetLabel(IRobotLabelType.I_LT_BAR_SECTION, girderBSection.Name);
            }
            foreach (IRobotBar bar in TransverseBars)
            {
                bar.SetLabel(IRobotLabelType.I_LT_BAR_SECTION, transverseBarsSection.Name);
            }
        }

        private void ComputeAnalyticalBridge()
        {
            Stopwatch localStopWatch = new Stopwatch(); localStopWatch.Start();

            AddSelfWeight(101, "Ciężar własny konstrukcji", "CWK");
            AddSuperimposedDeadLoad(102, "Ciężar własny wyposażenia", "CWW");

            double settlementValue = -0.001 * ((double) 10 / 3);
            AddDifferentialSettlementCase(201, "Osiadanie podpór 1", "DS1", 1, settlementValue);
            AddDifferentialSettlementCase(202, "Osiadanie podpór 2", "DS2", 2, settlementValue);
            AddDifferentialSettlementCase(203, "Osiadanie podpór 3", "DS3", 3, settlementValue);

            AddThermalGradientCase(301, "Obciążenie termiczne, TM heat", "T1", 10.5);
            AddThermalGradientCase(302, "Obciążenie termiczne, TM cool", "T2", -8.0);

            List<AnalyticalContour> loadContours = new List<AnalyticalContour>();
            RobotGeoPoint3D pointA = new RobotGeoPoint3D();
            RobotGeoPoint3D pointB = new RobotGeoPoint3D();
            RobotGeoPoint3D pointC = new RobotGeoPoint3D();
            RobotGeoPoint3D pointD = new RobotGeoPoint3D();

            //UDL, układ A, przęsło 1:
            pointA.X = 0.000; pointA.Y = -0.500; pointA.Z = 0.000;
            pointB.X = 0.000; pointB.Y = -3.500; pointB.Z = 0.000;
            pointC.X = PhysicalBridge.SpanLength[0]; pointC.Y = -3.500; pointC.Z = 0.000;
            pointD.X = PhysicalBridge.SpanLength[0]; pointD.Y = -0.500; pointD.Z = 0.000;
            loadContours.Add(new AnalyticalContour(new List<RobotGeoPoint3D> { pointA, pointB, pointC, pointD }));

            pointA.X = 0.000; pointA.Y = -0.500; pointA.Z = 0.000;
            pointB.X = 0.000; pointB.Y = 2.500; pointB.Z = 0.000;
            pointC.X = PhysicalBridge.SpanLength[0]; pointC.Y = 2.500; pointC.Z = 0.000;
            pointD.X = PhysicalBridge.SpanLength[0]; pointD.Y = -0.500; pointD.Z = 0.000;
            loadContours.Add(new AnalyticalContour(new List<RobotGeoPoint3D> { pointA, pointB, pointC, pointD }));

            pointA.X = 0.000; pointA.Y = 2.500; pointA.Z = 0.000;
            pointB.X = 0.000; pointB.Y = 3.500; pointB.Z = 0.000;
            pointC.X = PhysicalBridge.SpanLength[0]; pointC.Y = 3.500; pointC.Z = 0.000;
            pointD.X = PhysicalBridge.SpanLength[0]; pointD.Y = 2.500; pointD.Z = 0.000;
            loadContours.Add(new AnalyticalContour(new List<RobotGeoPoint3D> { pointA, pointB, pointC, pointD }));

            AddUniformlyDistributedLoadCase(401, "Obciążenie UDL, układ A, przęsło 1", "UDL-A1", new List<double> { -9000, -2500, -2500 }, loadContours);
            loadContours.Clear();

            //UDL, układ A, przęsło 2:
            pointA.X = PhysicalBridge.SpanLength[0]; pointA.Y = -0.500; pointA.Z = 0.000;
            pointB.X = PhysicalBridge.SpanLength[0]; pointB.Y = -3.500; pointB.Z = 0.000;
            pointC.X = PhysicalBridge.SpanLength[0] + PhysicalBridge.SpanLength[1]; pointC.Y = -3.500; pointC.Z = 0.000;
            pointD.X = PhysicalBridge.SpanLength[0] + PhysicalBridge.SpanLength[1]; pointD.Y = -0.500; pointD.Z = 0.000;
            loadContours.Add(new AnalyticalContour(new List<RobotGeoPoint3D> { pointA, pointB, pointC, pointD }));

            pointA.X = PhysicalBridge.SpanLength[0]; pointA.Y = -0.500; pointA.Z = 0.000;
            pointB.X = PhysicalBridge.SpanLength[0]; pointB.Y = 2.500; pointB.Z = 0.000;
            pointC.X = PhysicalBridge.SpanLength[0] + PhysicalBridge.SpanLength[1]; pointC.Y = 2.500; pointC.Z = 0.000;
            pointD.X = PhysicalBridge.SpanLength[0] + PhysicalBridge.SpanLength[1]; pointD.Y = -0.500; pointD.Z = 0.000;
            loadContours.Add(new AnalyticalContour(new List<RobotGeoPoint3D> { pointA, pointB, pointC, pointD }));

            pointA.X = PhysicalBridge.SpanLength[0]; pointA.Y = 2.500; pointA.Z = 0.000;
            pointB.X = PhysicalBridge.SpanLength[0]; pointB.Y = 3.500; pointB.Z = 0.000;
            pointC.X = PhysicalBridge.SpanLength[0] + PhysicalBridge.SpanLength[1]; pointC.Y = 3.500; pointC.Z = 0.000;
            pointD.X = PhysicalBridge.SpanLength[0] + PhysicalBridge.SpanLength[1]; pointD.Y = 2.500; pointD.Z = 0.000;
            loadContours.Add(new AnalyticalContour(new List<RobotGeoPoint3D> { pointA, pointB, pointC, pointD }));

            AddUniformlyDistributedLoadCase(402, "Obciążenie UDL, układ A, przęsło 2", "UDL-A2", new List<double> { -9000, -2500, -2500 }, loadContours);
            loadContours.Clear();

            //UDL, układ B, przęsło 1:
            pointA.X = 0.000; pointA.Y = 0.500; pointA.Z = 0.000;
            pointB.X = 0.000; pointB.Y = 3.500; pointB.Z = 0.000;
            pointC.X = PhysicalBridge.SpanLength[0]; pointC.Y = 3.500; pointC.Z = 0.000;
            pointD.X = PhysicalBridge.SpanLength[0]; pointD.Y = 0.500; pointD.Z = 0.000;
            loadContours.Add(new AnalyticalContour(new List<RobotGeoPoint3D> { pointA, pointB, pointC, pointD }));

            pointA.X = 0.000; pointA.Y = 0.500; pointA.Z = 0.000;
            pointB.X = 0.000; pointB.Y = -2.500; pointB.Z = 0.000;
            pointC.X = PhysicalBridge.SpanLength[0]; pointC.Y = -2.500; pointC.Z = 0.000;
            pointD.X = PhysicalBridge.SpanLength[0]; pointD.Y = 0.500; pointD.Z = 0.000;
            loadContours.Add(new AnalyticalContour(new List<RobotGeoPoint3D> { pointA, pointB, pointC, pointD }));

            pointA.X = 0.000; pointA.Y = -2.500; pointA.Z = 0.000;
            pointB.X = 0.000; pointB.Y = -3.500; pointB.Z = 0.000;
            pointC.X = PhysicalBridge.SpanLength[0]; pointC.Y = -3.500; pointC.Z = 0.000;
            pointD.X = PhysicalBridge.SpanLength[0]; pointD.Y = -2.500; pointD.Z = 0.000;
            loadContours.Add(new AnalyticalContour(new List<RobotGeoPoint3D> { pointA, pointB, pointC, pointD }));

            AddUniformlyDistributedLoadCase(403, "Obciążenie UDL, układ B, przęsło 1", "UDL-B1", new List<double> { -9000, -2500, -2500 }, loadContours);
            loadContours.Clear();

            //UDL, układ B, przęsło 2:
            pointA.X = PhysicalBridge.SpanLength[0]; pointA.Y = 0.500; pointA.Z = 0.000;
            pointB.X = PhysicalBridge.SpanLength[0]; pointB.Y = 3.500; pointB.Z = 0.000;
            pointC.X = PhysicalBridge.SpanLength[0] + PhysicalBridge.SpanLength[1]; pointC.Y = 3.500; pointC.Z = 0.000;
            pointD.X = PhysicalBridge.SpanLength[0] + PhysicalBridge.SpanLength[1]; pointD.Y = 0.500; pointD.Z = 0.000;
            loadContours.Add(new AnalyticalContour(new List<RobotGeoPoint3D> { pointA, pointB, pointC, pointD }));

            pointA.X = PhysicalBridge.SpanLength[0]; pointA.Y = 0.500; pointA.Z = 0.000;
            pointB.X = PhysicalBridge.SpanLength[0]; pointB.Y = -2.500; pointB.Z = 0.000;
            pointC.X = PhysicalBridge.SpanLength[0] + PhysicalBridge.SpanLength[1]; pointC.Y = -2.500; pointC.Z = 0.000;
            pointD.X = PhysicalBridge.SpanLength[0] + PhysicalBridge.SpanLength[1]; pointD.Y = 0.500; pointD.Z = 0.000;
            loadContours.Add(new AnalyticalContour(new List<RobotGeoPoint3D> { pointA, pointB, pointC, pointD }));

            pointA.X = PhysicalBridge.SpanLength[0]; pointA.Y = -2.500; pointA.Z = 0.000;
            pointB.X = PhysicalBridge.SpanLength[0]; pointB.Y = -3.500; pointB.Z = 0.000;
            pointC.X = PhysicalBridge.SpanLength[0] + PhysicalBridge.SpanLength[1]; pointC.Y = -3.500; pointC.Z = 0.000;
            pointD.X = PhysicalBridge.SpanLength[0] + PhysicalBridge.SpanLength[1]; pointD.Y = -2.500; pointD.Z = 0.000;
            loadContours.Add(new AnalyticalContour(new List<RobotGeoPoint3D> { pointA, pointB, pointC, pointD }));

            AddUniformlyDistributedLoadCase(404, "Obciążenie UDL, układ B, przęsło 2", "UDL-B2", new List<double> { -9000, -2500, -2500 }, loadContours);
            loadContours.Clear();

            //Obciążenie chodnika, przęsło 1:
            pointA.X = 0.000; pointA.Y = -4.360; pointA.Z = 0.000;
            pointB.X = 0.000; pointB.Y = -5.860; pointB.Z = 0.000;
            pointC.X = PhysicalBridge.SpanLength[0]; pointC.Y = -5.860; pointC.Z = 0.000;
            pointD.X = PhysicalBridge.SpanLength[0]; pointD.Y = -4.360; pointD.Z = 0.000;
            loadContours.Add(new AnalyticalContour(new List<RobotGeoPoint3D> { pointA, pointB, pointC, pointD }));

            AddUniformlyDistributedLoadCase(501, "Obciążenia chodnika, przęsło 1", "CHO1", new List<double> { -5000 }, loadContours);
            loadContours.Clear();

            //Obciążenie chodnika, przęsło 2:
            pointA.X = PhysicalBridge.SpanLength[0]; pointA.Y = -4.360; pointA.Z = 0.000;
            pointB.X = PhysicalBridge.SpanLength[0]; pointB.Y = -5.860; pointB.Z = 0.000;
            pointC.X = PhysicalBridge.SpanLength[0] + PhysicalBridge.SpanLength[1]; pointC.Y = -5.860; pointC.Z = 0.000;
            pointD.X = PhysicalBridge.SpanLength[0] + PhysicalBridge.SpanLength[1]; pointD.Y = -4.360; pointD.Z = 0.000;
            loadContours.Add(new AnalyticalContour(new List<RobotGeoPoint3D> { pointA, pointB, pointC, pointD }));

            AddUniformlyDistributedLoadCase(502, "Obciążenia chodnika, przęsło 2", "CHO2", new List<double> { -5000 }, loadContours);
            loadContours.Clear();

            List<RobotGeoPoint3D> routePoints = new List<RobotGeoPoint3D>();
            RobotGeoPoint3D routePointA = new RobotGeoPoint3D();
            RobotGeoPoint3D routePointB = new RobotGeoPoint3D();
            double step = 0.5 * (PhysicalBridge.SpanLength[0] + PhysicalBridge.SpanLength[1]) / SpanDivision;

            //Obciążenie tandemem TS, układ A, pojazd 1:
            routePointA.X = 0.000; routePointA.Y = -2.000; routePointA.Z = 0.000; routePoints.Add(routePointA);
            routePointB.X = PhysicalBridge.SpanLength[0] + PhysicalBridge.SpanLength[1]; routePointB.Y = -2.000; routePointB.Z = 0.000; routePoints.Add(routePointB);
            AddMobileLoadCase(601, "Obciążenie TS, układ A, pojazd 1", "TS-A1", routePoints, step, 100000);
            routePoints.Clear();

            //Obciążenie tandemem TS, układ A, pojazd 2:
            routePointA.X = 0.000; routePointA.Y = 1.000; routePointA.Z = 0.000; routePoints.Add(routePointA);
            routePointB.X = PhysicalBridge.SpanLength[0] + PhysicalBridge.SpanLength[1]; routePointB.Y = 1.000; routePointB.Z = 0.000; routePoints.Add(routePointB);
            AddMobileLoadCase(602, "Obciążenie TS, układ A, pojazd 2", "TS-A2", routePoints, step, 100000);
            routePoints.Clear();

            //Obciążenie tandemem TS, układ B, pojazd 1:
            routePointA.X = 0.000; routePointA.Y = 2.000; routePointA.Z = 0.000; routePoints.Add(routePointA);
            routePointB.X = PhysicalBridge.SpanLength[0] + PhysicalBridge.SpanLength[1]; routePointB.Y = 2.000; routePointB.Z = 0.000; routePoints.Add(routePointB);
            AddMobileLoadCase(603, "Obciążenie TS, układ B, pojazd 1", "TS-B1", routePoints, step, 100000);
            routePoints.Clear();

            //Obciążenie tandemem TS, układ B, pojazd 2:
            routePointA.X = 0.000; routePointA.Y = -1.000; routePointA.Z = 0.000; routePoints.Add(routePointA);
            routePointB.X = PhysicalBridge.SpanLength[0] + PhysicalBridge.SpanLength[1]; routePointB.Y = -1.000; routePointB.Z = 0.000; routePoints.Add(routePointB);
            AddMobileLoadCase(604, "Obciążenie TS, układ B, pojazd 2", "TS-B2", routePoints, step, 100000);
            routePoints.Clear();

            using (StreamWriter stopwatchSummary = new StreamWriter(PhysicalBridge.Directory + Globals.TextFiles.StopwatchSummary, true)) stopwatchSummary.WriteLine("\tAnalytical bridge, load cases created:\t" + DateTime.Now.ToString() + "\t" + localStopWatch.Elapsed.ToString()); localStopWatch.Restart();

            Calculate();
            using (StreamWriter stopwatchSummary = new StreamWriter(PhysicalBridge.Directory + Globals.TextFiles.StopwatchSummary, true)) stopwatchSummary.WriteLine("\tAnalytical bridge, model calculated:\t" + DateTime.Now.ToString() + "\t" + localStopWatch.Elapsed.ToString()); localStopWatch.Restart();
            PrintBarResults(Cases.ToArray(), Globals.ExcelFiles.SimpleCasesSheetName(), false);
            PrintBarResults(MobileCases.ToArray(), "Model. Case", true);
            Close(true, false);

            using (StreamWriter stopwatchSummary = new StreamWriter(PhysicalBridge.Directory + Globals.TextFiles.StopwatchSummary, true)) stopwatchSummary.WriteLine("\tAnalytical bridge, results printed:\t" + DateTime.Now.ToString() + "\t" + localStopWatch.Elapsed.ToString());
            localStopWatch.Stop();
        }

        private void ComputeAnalyticalPrestressedBridge(int numberOfCases, double dx, bool includePartialTendons = false)
        {
            Stopwatch localStopWatch = new Stopwatch(); localStopWatch.Start();
            StreamWriter outputFile = new StreamWriter(PhysicalBridge.Directory + Globals.TextFiles.PrestressingScr);

            //Retrieving SupportZ boundaries:
            double minSupportZ = 0.001 * double.Parse(PhysicalBridge.DataForm.DataFormMinSupportZ1.Text);
            if (PhysicalBridge.DataForm.DataFormMinSupportZCheckBox.Checked == true) minSupportZ = Math.Round(Math.Max(minSupportZ, double.Parse(PhysicalBridge.DataForm.DataFormMinSupportZ2.Text) * Math.Min(Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("HL").AsDouble()), Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("HP").AsDouble()))), 3);
            double maxSupportZ = Math.Round(Math.Min(Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("HL").AsDouble()), Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("HP").AsDouble())) - 0.001 * double.Parse(PhysicalBridge.DataForm.DataFormMaxSupportZ.Text), 3);

            //Retrieving AnchorageZ boundaries:
            double minAnchorageZ = 0.001 * double.Parse(PhysicalBridge.DataForm.DataFormMinAnchorageZ.Text);
            double maxAnchorageZ = Math.Round(Math.Min(Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("HL").AsDouble()), Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("HP").AsDouble())) - 0.001 * double.Parse(PhysicalBridge.DataForm.DataFormMaxAnchorageZ1.Text), 3);

            //Retrieving MiddleSpanX boundaries:
            double minMiddleSpanX = 0.001 * double.Parse(PhysicalBridge.DataForm.DataFormMinSpanX.Text);
            double maxMiddleSpanX = Math.Round(Math.Min(PhysicalBridge.SpanLength.First(), PhysicalBridge.SpanLength.Last()) - 0.001 * double.Parse(PhysicalBridge.DataForm.DataFormMaxSpanX.Text), 3);

            //Retrieving MiddleSpanZ boundaries:
            double minMiddleSpanZ = 0.001 * double.Parse(PhysicalBridge.DataForm.DataFormMinSpanZ.Text);
            double maxMiddleSpanZ = Math.Round(0.5 * Math.Min(Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("HL").AsDouble()), Converters.ToMeters(PhysicalBridge.Superstructure.ParametersMap.get_Item("HP").AsDouble())) + 0.001 * double.Parse(PhysicalBridge.DataForm.DataFormMaxSpanZ1.Text), 3);

            //Retrieving SupportRadius boundaries:
            double minSupportRadius = 0.001 * double.Parse(PhysicalBridge.DataForm.DataFormMinSupportRadius.Text);
            double maxSupportRadius = 0.001 * double.Parse(PhysicalBridge.DataForm.DataFormMaxSupportRadius.Text);

            double zc = 0.5 * (Math.Abs(PhysicalBridge.LeftGirderCrossSection.Boundaries.Bottom) + Math.Abs(PhysicalBridge.RightGirderCrossSection.Boundaries.Bottom));

            Random rnd = new Random();
            List<Tuple<int, DiscreteTendon>> discreteTendons = new List<Tuple<int, DiscreteTendon>>();
            List<FullTendon> fullTendons = new List<FullTendon>(); int fullTendonCaseNumber = 1000;
            for (int i = 1; i <= numberOfCases; i++)
            {
                //SupportZ:         Test case, L = 32,00 m, H = 1,600 m: 800 mm to 1400 mm
                double supportZ = 0.001 * rnd.Next((int) (1000 * minSupportZ), (int) (1000 * maxSupportZ));
                //AnchorageZ:       Test case, L = 32,00 m, H = 1,600 m: 300 mm to min(supportZ, 1300 mm)
                if (PhysicalBridge.DataForm.DataFormMaxAnchorageZCheckBox.Checked == true) maxAnchorageZ = Math.Min(maxAnchorageZ, supportZ - 0.001 * double.Parse(PhysicalBridge.DataForm.DataFormMaxAnchorageZ2.Text));
                double anchorageZ = 0.001 * rnd.Next((int) (1000 * minAnchorageZ), (int) (1000 * maxAnchorageZ));
                //MiddleSpanX:      Test case, L = 32,00 m, H = 1,600 m: 6000 mm to 26000 mm
                double middleSpanX = 0.001 * rnd.Next((int) (1000 * minMiddleSpanX), (int) (1000 * maxMiddleSpanX));
                //MiddleSpanZ:      Test case, L = 32,00 m, H = 1,600 m: 200 mm to min(supportZ, 1000 mm)
                if (PhysicalBridge.DataForm.DataFormMaxSpanZCheckBox.Checked == true) maxMiddleSpanZ = Math.Min(maxMiddleSpanZ, supportZ - 0.001 * double.Parse(PhysicalBridge.DataForm.DataFormMaxSpanZ2.Text));
                double middleSpanZ = 0.001 * rnd.Next((int) (1000 * minMiddleSpanZ), (int) (1000 * maxMiddleSpanZ));
                //SupportRadius:    Test case, L = 32,00 m, H = 1,600 m: -7000 to -10000 mm
                double supportRadius = -0.001 * rnd.Next((int) (1000 * minSupportRadius), (int) (1000 * maxSupportRadius));

                fullTendons.Add(new FullTendon(0, anchorageZ, middleSpanX, middleSpanZ, PhysicalBridge.SpanLength.First(), supportZ, PhysicalBridge.SpanLength.First() + (PhysicalBridge.SpanLength.First() - middleSpanX), middleSpanZ, PhysicalBridge.SpanLength.First() + PhysicalBridge.SpanLength.Last(), anchorageZ, 2, 2, supportRadius));
                if (fullTendons.Last().IsValid() == false)
                {
                    fullTendons.RemoveAt(fullTendons.Count() - 1);
                    i = i - 1;
                }
                else
                {
                    fullTendons.Last().TendonArea = 150; //mm^2
                    fullTendons.Last().PrestressForce = 223; //kN
                    fullTendons.Last().PrestressType = PrestressTypes.DoubleSided;

                    discreteTendons.Add(new Tuple<int, DiscreteTendon>(fullTendonCaseNumber, new DiscreteTendon(fullTendons.Last(), zc, dx)));
                    outputFile.WriteLine(fullTendons.Last().ToScr(1000));

                    fullTendonCaseNumber++;
                }
            }

            List<PartialTendon> partialTendons = new List<PartialTendon>(); int partialTendonCaseNumber = 2000;
            for (int i = 1; i <= numberOfCases; i++)
            {
                if (includePartialTendons == false)
                {
                    break;
                }
                //SupportZ:         Test case, L = 32,00 m, H = 1,600 m: 800 mm to 1400 mm
                double supportZ = 0.001 * rnd.Next((int) (1000 * minSupportZ), (int) (1000 * maxSupportZ));
                //AnchorageZ:       Test case, L = 32,00 m, H = 1,600 m: 300 mm to min(supportZ, 1300 mm)
                if (PhysicalBridge.DataForm.DataFormMaxAnchorageZCheckBox.Checked == true) maxAnchorageZ = Math.Min(maxAnchorageZ, supportZ - 0.001 * double.Parse(PhysicalBridge.DataForm.DataFormMaxAnchorageZ2.Text));
                double anchorageZ = 0.001 * rnd.Next((int) (1000 * minAnchorageZ), (int) (1000 * maxAnchorageZ));
                //MiddleSpanX:      Test case, L = 32,00 m, H = 1,600 m: 6000 mm to 26000 mm
                double middleSpanX = 0.001 * rnd.Next((int) (1000 * minMiddleSpanX), (int) (1000 * maxMiddleSpanX));
                //MiddleSpanZ:      Test case, L = 32,00 m, H = 1,600 m: 200 mm to min(supportZ, 1000 mm)
                if (PhysicalBridge.DataForm.DataFormMaxSpanZCheckBox.Checked == true) maxMiddleSpanZ = Math.Min(maxMiddleSpanZ, supportZ - 0.001 * double.Parse(PhysicalBridge.DataForm.DataFormMaxSpanZ2.Text));
                double middleSpanZ = 0.001 * rnd.Next((int) (1000 * minMiddleSpanZ), (int) (1000 * maxMiddleSpanZ));
                //SupportRadius:    Test case, L = 32,00 m, H = 1,600 m: -7000 to -10000 mm
                double supportRadius = -0.001 * rnd.Next((int) (1000 * minSupportRadius), (int) (1000 * maxSupportRadius));

                partialTendons.Add(new PartialTendon(0, anchorageZ, middleSpanX, middleSpanZ, PhysicalBridge.SpanLength.First(), supportZ, 2, 6, supportRadius));
                partialTendons.Last().TendonArea = 150; //mm^2
                partialTendons.Last().PrestressForce = 223; //kN

                discreteTendons.Add(new Tuple<int, DiscreteTendon>(partialTendonCaseNumber, new DiscreteTendon(partialTendons.Last(), zc, dx)));
                outputFile.WriteLine(partialTendons.Last().ToScr(1000));

                partialTendonCaseNumber++;
            }
            outputFile.Close();

            foreach (Tuple<int, DiscreteTendon> discreteTendon in discreteTendons)
            {
                if (discreteTendon.Item1 <= fullTendonCaseNumber) AddPrestressingCase(discreteTendon.Item1, "Sprężenie F " + discreteTendon.Item1.ToString(), discreteTendon.Item2, false);
                else AddPrestressingCase(discreteTendon.Item1, "Sprężenie P " + discreteTendon.Item1.ToString(), discreteTendon.Item2, true);
            }

            using (StreamWriter stopwatchSummary = new StreamWriter(PhysicalBridge.Directory + Globals.TextFiles.StopwatchSummary, true)) stopwatchSummary.WriteLine("\tAnalytical bridge, load cases created:\t" + DateTime.Now.ToString() + "\t" + localStopWatch.Elapsed.ToString()); localStopWatch.Restart();
            
            Calculate();
            using (StreamWriter stopwatchSummary = new StreamWriter(PhysicalBridge.Directory + Globals.TextFiles.StopwatchSummary, true)) stopwatchSummary.WriteLine("\tAnalytical bridge, model calculated:\t" + DateTime.Now.ToString() + "\t" + localStopWatch.Elapsed.ToString()); localStopWatch.Restart();
            PrintBarPrestressingResults(discreteTendons, PhysicalBridge.Directory + Globals.TextFiles.PrestressingModel, true);
            Close(true, true);

            using (StreamWriter stopwatchSummary = new StreamWriter(PhysicalBridge.Directory + Globals.TextFiles.StopwatchSummary, true)) stopwatchSummary.WriteLine("\tAnalytical bridge, results printed:\t" + DateTime.Now.ToString() + "\t" + localStopWatch.Elapsed.ToString());
            localStopWatch.Stop();
        }

        private void DecodeToANSI(string filePath, bool envelopes = false)
        {
            StreamReader fileStreamer = new StreamReader(filePath);
            string fileContent = fileStreamer.ReadToEnd();
            fileStreamer.Close();

            if (envelopes) fileContent = fileContent.Replace(">>", "");
            if (envelopes) fileContent = fileContent.Replace("<<", "");
            StreamWriter fileWriter = new StreamWriter(@filePath, false, Encoding.GetEncoding(1250));
            fileWriter.Write(fileContent);
            fileWriter.Close();
        }

        public void PrintBarResultsEnvelopes(int caseNumber, string fileName)
        {
            RobotTable robotTable = RobotProject.ViewMngr.CreateTable(IRobotTableType.I_TT_FORCES, IRobotTableDataType.I_TDT_ENVELOPE);
            string filePath = PhysicalBridge.Directory + Globals.ExcelFiles.CaseSheetPath(fileName);

            robotTable.Select(IRobotSelectionType.I_ST_CASE, caseNumber.ToString());
            robotTable.Select(IRobotSelectionType.I_ST_BAR, LeftGirderBars.First().Number.ToString() + "do" + LeftGirderBars.Last().Number.ToString() + " " + RightGirderBars.First().Number.ToString() + "do" + RightGirderBars.Last().Number.ToString());
            robotTable.Printable.SaveToFile(filePath, IRobotOutputFileFormat.I_OFF_TEXT);

            DecodeToANSI(filePath, true);
        }
        public void PrintBarResultsEnvelopes(int[] caseNumbers, string fileName, bool separatedFiles = false)
        {
            if (separatedFiles)
            {
                foreach (int caseNumber in caseNumbers) PrintBarResultsEnvelopes(caseNumber, fileName + " " + caseNumber.ToString());
                return;
            }
            RobotTable robotTable = RobotProject.ViewMngr.CreateTable(IRobotTableType.I_TT_FORCES, IRobotTableDataType.I_TDT_ENVELOPE);
            string printed = caseNumbers.First().ToString();
            if (caseNumbers.Count() > 1)
            {
                foreach (int caseNumber in caseNumbers) printed = printed + " " + caseNumber.ToString();
            }
            string filePath = PhysicalBridge.Directory + Globals.ExcelFiles.CaseSheetPath(fileName);
            
            robotTable.Select(IRobotSelectionType.I_ST_CASE, printed);
            robotTable.Select(IRobotSelectionType.I_ST_BAR, LeftGirderBars.First().Number.ToString() + "do" + LeftGirderBars.Last().Number.ToString() + " " + RightGirderBars.First().Number.ToString() + "do" + RightGirderBars.Last().Number.ToString());
            robotTable.Printable.SaveToFile(filePath, IRobotOutputFileFormat.I_OFF_TEXT);

            DecodeToANSI(filePath, true);
        }

        public void PrintBarResults(int caseNumber, string fileName)
        {
            RobotTable robotTable = RobotProject.ViewMngr.CreateTable(IRobotTableType.I_TT_FORCES, IRobotTableDataType.I_TDT_DEFAULT);
            string filePath = PhysicalBridge.Directory + Globals.ExcelFiles.CaseSheetPath(fileName);

            robotTable.Select(IRobotSelectionType.I_ST_CASE, caseNumber.ToString());
            robotTable.Select(IRobotSelectionType.I_ST_BAR, LeftGirderBars.First().Number.ToString() + "do" + LeftGirderBars.Last().Number.ToString() + " " + RightGirderBars.First().Number.ToString() + "do" + RightGirderBars.Last().Number.ToString());
            robotTable.Printable.SaveToFile(filePath, IRobotOutputFileFormat.I_OFF_TEXT);

            DecodeToANSI(filePath);
            robotTable.Visible = 0;
        }
        public void PrintBarResults(int[] caseNumbers, string fileName, bool separatedFiles = false)
        {
            if (separatedFiles)
            {
                foreach (int caseNumber in caseNumbers) PrintBarResults(caseNumber, Globals.ExcelFiles.CaseSheetName(caseNumber));
                return;
            }
            RobotTable robotTable = RobotProject.ViewMngr.CreateTable(IRobotTableType.I_TT_FORCES, IRobotTableDataType.I_TDT_DEFAULT);
            string printed = caseNumbers.First().ToString();
            if (caseNumbers.Count() > 1)
            {
                foreach (int caseNumber in caseNumbers) printed = printed + " " + caseNumber.ToString();
            }
            string filePath = PhysicalBridge.Directory + Globals.ExcelFiles.CaseSheetPath(fileName);

            robotTable.Select(IRobotSelectionType.I_ST_CASE, printed);
            robotTable.Select(IRobotSelectionType.I_ST_BAR, LeftGirderBars.First().Number.ToString() + "do" + LeftGirderBars.Last().Number.ToString() + " " + RightGirderBars.First().Number.ToString() + "do" + RightGirderBars.Last().Number.ToString());
            robotTable.Printable.SaveToFile(filePath, IRobotOutputFileFormat.I_OFF_TEXT);

            DecodeToANSI(filePath);
            robotTable.Visible = 0;
        }

        public void PrintBarPrestressingResults(List<Tuple<int, DiscreteTendon>> discreteTendons, string filePath, bool overwrite = false, bool byQuery = true)
        {
            List<IRobotBar> selBars = new List<IRobotBar>();
            foreach(double x in PhysicalBridge.CriticalCrossSections) selBars.Add(FindBar(x));

            RobotResultQueryParams resultsQuery = Robot.Application.CmpntFactory.Create(IRobotComponentType.I_CT_RESULT_QUERY_PARAMS);
            using (StreamWriter outputFile = new StreamWriter(filePath, !overwrite))
            {
                string titleStr = "";
                if (overwrite) titleStr = "Case Number" + "\t" + "A.X" + "\t" + "A.Y" + "\t" + "B.X" + "\t" + "B.Y" + "\t" + "C.X" + "\t" + "C.Y" + "\t" + "D.X" + "\t" + "D.Y" + "\t" + "E.X" + "\t" + "E.Y" + "\t" + "RC" + "\t" + "AP" + "\t" + "F" + "\t" + "FX";

                foreach (Tuple<int, DiscreteTendon> tendonTuple in discreteTendons)
                {
                    int caseNumber = tendonTuple.Item1;
                    DiscreteTendon discreteTendon = tendonTuple.Item2;

                    string str = "";
                    if (discreteTendon.IsFullTendon())
                    {
                        FullTendon fullTendon = discreteTendon.Tendon as FullTendon;
                        str = caseNumber.ToString() + "\t" + string.Format("{0:0.000}\t{1:0.000}\t{2:0.000}\t{3:0.000}\t{4:0.000}\t{5:0.000}\t{6:0.000}\t{7:0.000}\t{8:0.000}\t{9:0.000}\t{10:0.000}\t{11:0.000}\t{12:0.000}\t{13:0.000}",
                            fullTendon.PointA.X, fullTendon.PointA.Z,
                            fullTendon.PointB.X, fullTendon.PointB.Z,
                            fullTendon.PointC.X, fullTendon.PointC.Z,
                            fullTendon.PointD.X, fullTendon.PointD.Z,
                            fullTendon.PointE.X, fullTendon.PointE.Z, fullTendon.SupportRadius, fullTendon.TendonArea, fullTendon.PrestressType != PrestressTypes.RightSided ? fullTendon.PrestressForce : 0, fullTendon.PrestressType != PrestressTypes.LeftSided ? fullTendon.PrestressForce : 0);
                    }
                    else if (discreteTendon.IsPartialTendon())
                    {
                        PartialTendon partialTendon = discreteTendon.Tendon as PartialTendon;
                        str = caseNumber.ToString() + "\t" + string.Format("{0:0.000}\t{1:0.000}\t{2:0.000}\t{3:0.000}\t{4:0.000}\t{5:0.000}\t{6:0.000}\t{7:0.000}\t\t\t{8:0.000}\t{9:0.000}\t{10:0.000}\t",
                            partialTendon.PointA.X, partialTendon.PointA.Z,
                            partialTendon.PointB.X, partialTendon.PointB.Z,
                            partialTendon.PointC.X, partialTendon.PointC.Z,
                            partialTendon.PointD.X, partialTendon.PointD.Z, partialTendon.SupportRadius, partialTendon.TendonArea, partialTendon.PrestressForce);
                    }

                    List<double> forcesFX = new List<double>();
                    List<double> forcesMY = new List<double>();
                    if (byQuery) //Speeds up the results printout process
                    {
                        resultsQuery.Reset();

                        RobotSelection barSelection = RobotProject.Structure.Selections.Create(IRobotObjectType.I_OT_BAR);
                        //barSelection.AddText(LeftGirderBars.First().Number.ToString() + "do" + LeftGirderBars.Last().Number.ToString());
                        foreach (IRobotBar selBar in selBars) barSelection.AddOne(selBar.Number);
                        resultsQuery.Selection.Set(IRobotObjectType.I_OT_BAR, barSelection);

                        RobotSelection caseSelection = RobotProject.Structure.Selections.Create(IRobotObjectType.I_OT_CASE);
                        caseSelection.AddOne(caseNumber);
                        resultsQuery.Selection.Set(IRobotObjectType.I_OT_CASE, caseSelection);

                        resultsQuery.SetParam(IRobotResultParamType.I_RPT_BAR_RELATIVE_POINT, 0.000);
                        resultsQuery.ResultIds.SetSize(2);
                        resultsQuery.ResultIds.Set(1, (int) IRobotExtremeValueType.I_EVT_FORCE_BAR_FX);
                        resultsQuery.ResultIds.Set(2, (int) IRobotExtremeValueType.I_EVT_FORCE_BAR_MY);

                        RobotResultRowSet set = new RobotResultRowSet();
                        IRobotResultQueryReturnType res = RobotProject.Structure.Results.Query(resultsQuery, set);
                        do
                        {
                            bool setCheck = set.MoveFirst();
                            while (setCheck)
                            {
                                forcesFX.Add(0.001 * set.CurrentRow.GetValue(resultsQuery.ResultIds.Get(1)));
                                forcesMY.Add(0.001 * set.CurrentRow.GetValue(resultsQuery.ResultIds.Get(2)));
                                setCheck = set.MoveNext();
                            }
                        }
                        while (res == IRobotResultQueryReturnType.I_RQRT_MORE_AVAILABLE);

                        foreach (double fx in forcesFX)
                        {
                            if (titleStr.Length > 0) titleStr += "\t";
                            str = str + "\t" + string.Format("{0:0.0}", fx);
                        }
                        //forcesFX.Add(0.001 * RobotProject.Structure.Results.Bars.Forces.Value(LeftGirderBars.Last().Number, caseNumber, 0.999).FX);
                        //str = str + "\t" + string.Format("{0:0.0}", forcesFX.Last());

                        if (titleStr.Length > 0) titleStr = titleStr + "MY";
                        foreach (double my in forcesMY)
                        {
                            if (titleStr.Length > 0) titleStr += "\t";
                            str = str + "\t" + string.Format("{0:0.0}", my);
                        }
                        //forcesMY.Add(0.001 * RobotProject.Structure.Results.Bars.Forces.Value(LeftGirderBars.Last().Number, caseNumber, 0.999).MY);
                        //str = str + "\t" + string.Format("{0:0.0}", forcesMY.Last());
                    }
                    else
                    {
                        foreach (IRobotBar bar in selBars)
                        {
                            forcesFX.Add(0.001 * RobotProject.Structure.Results.Bars.Forces.Value(bar.Number, caseNumber, 0.000).FX);

                            if (titleStr.Length > 0) titleStr += "\t";
                            str = str + "\t" + string.Format("{0:0.0}", 0.001 * RobotProject.Structure.Results.Bars.Forces.Value(bar.Number, caseNumber, 0.000).FX);
                        }
                        //forcesFX.Add(0.001 * RobotProject.Structure.Results.Bars.Forces.Value(LeftGirderBars.Last().Number, caseNumber, 0.999).FX);
                        //str = str + "\t" + string.Format("{0:0.0}", forcesFX.Last());

                        if (titleStr.Length > 0) titleStr = titleStr + "\t" + "MY";
                        foreach (IRobotBar bar in selBars)
                        {
                            forcesFX.Add(0.001 * RobotProject.Structure.Results.Bars.Forces.Value(bar.Number, caseNumber, 0.000).MY);

                            if (titleStr.Length > 0) titleStr += "\t";
                            str = str + "\t" + string.Format("{0:0.0}", 0.001 * RobotProject.Structure.Results.Bars.Forces.Value(bar.Number, caseNumber, 0.000).MY);
                        }
                        //forcesMY.Add(0.001 * RobotProject.Structure.Results.Bars.Forces.Value(LeftGirderBars.Last().Number, caseNumber, 0.999).MY);
                        //str = str + "\t" + string.Format("{0:0.0}", forcesMY.Last());                    
                    }
                    PrestressingCaseResults.Add(new PrestressingCaseResult(tendonTuple.Item2));
                    foreach (double my in forcesMY) PrestressingCaseResults.Last().AddResult(my);

                    if (titleStr.Length > 0)
                    {
                        outputFile.WriteLine(titleStr);
                        titleStr = "";
                    }
                    outputFile.WriteLine(str);
                }
            }
        }

        public void Calculate()
        {
            Slab.Mesh.Remove();
            RobotProject.CalcEngine.GenerationParams.NeglectedGeoObjects = "";
            if (MobileRoutes.Count() > 0) //Neglecting mobile load routes when generating mesh
            {
                for (int i = 0; i < MobileRoutes.Count(); i++)
                {
                    if (i == 0) RobotProject.CalcEngine.GenerationParams.NeglectedGeoObjects = MobileRoutes[i].ToString();
                    else RobotProject.CalcEngine.GenerationParams.NeglectedGeoObjects = RobotProject.CalcEngine.GenerationParams.NeglectedGeoObjects + " " + MobileRoutes[i].ToString();
                }
            }

            IRobotStructureGeoAnalyser structureData = Robot.Application.CmpntFactory.Create(IRobotComponentType.I_CT_STRUCTURE_GEO_ANALYSER);
            structureData.Precision = 0.001;
            structureData.Correct(); //Removes duplicated nodes after mesh generation
            RobotProject.CalcEngine.Calculate();
        }

        public AnalyticalBridge Copy(string newPath)
        {
            if (RobotProject.IsActive == 1)
            {
                RobotProject.Structure.Objects.EndMultiOperation();
                RobotProject.Structure.Cases.EndMultiOperation();
                Close(true);
            }
            RobotProject.Open(Path);

            Path = newPath;
            return this;
        }

        public void Modify(PhysicalBridge bridge, bool close = true)
        {
            foreach (IRobotNode node in LeftGirderNodes)
            {
                node.Y = -1 * (Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("OL").AsDouble()) - 0.200); //Y axle reversed in Robot
                ((IRobotNode) RobotProject.Structure.Nodes.Get(node.Number)).Y = node.Y;
            }
            foreach (IRobotNode node in RightGirderNodes)
            {
                node.Y = -1 * (Converters.ToMeters(bridge.Superstructure.ParametersMap.get_Item("OP").AsDouble()) + 0.200); //Y axle reversed in Robot
                ((IRobotNode) RobotProject.Structure.Nodes.Get(node.Number)).Y = node.Y;
            }
            SetBarsCharacteristics(bridge);
            if (close) Close(true);
        }

        public void Close(bool save = true, bool quitRobot = false)
        {
            RobotProject.Structure.Objects.EndMultiOperation();
            RobotProject.Structure.Cases.EndMultiOperation();

            if (save) RobotProject.SaveAs(Path);
            RobotProject.Close();
            
            if (quitRobot) Robot.DisposeRobot();
        }

        private RobotSimpleCase GetExistingCase(int caseNumber, string caseName)
        {
            RobotCaseServer robotCaseServer = RobotProject.Structure.Cases;
            RobotCaseCollection robotCases = robotCaseServer.GetAll();
            if (robotCases.Count > 0)
            {
                for (int i = 1; i <= robotCases.Count; i++)
                {
                    IRobotCase robotCase = robotCases.Get(i);
                    if (robotCase.Type != IRobotCaseType.I_CT_SIMPLE) continue;
                    if (robotCase.Number == caseNumber && robotCase.Name == caseName) return (RobotSimpleCase) robotCase;
                }
            }
            return null;
        }

        public void AddSelfWeight(int caseNumber, string caseName, string caseLabel)
        {
            RobotCaseServer robotCaseServer = RobotProject.Structure.Cases;          
            RobotSimpleCase selfWeightCase;
            if (CreateFromExistingFile)
            {
                selfWeightCase = GetExistingCase(caseNumber, caseName);
                if (selfWeightCase == null) selfWeightCase = robotCaseServer.CreateSimple(caseNumber, caseName, IRobotCaseNature.I_CN_PERMANENT, IRobotCaseAnalizeType.I_CAT_STATIC_LINEAR);
            }
            else selfWeightCase = robotCaseServer.CreateSimple(caseNumber, caseName, IRobotCaseNature.I_CN_PERMANENT, IRobotCaseAnalizeType.I_CAT_STATIC_LINEAR);
            RobotLoadRecordMngr records = selfWeightCase.Records;
            selfWeightCase.label = caseLabel;

            Cases.Add(caseNumber);
            records.New(IRobotLoadRecordType.I_LRT_DEAD);
            IRobotLoadRecord loadRecord = records.Get(records.Count);
            loadRecord.SetValue((short) IRobotDeadRecordValues.I_DRV_ENTIRE_STRUCTURE, 0);
            loadRecord.SetValue((short) IRobotDeadRecordValues.I_DRV_Z, -1);
            loadRecord.SetValue((short) IRobotDeadRecordValues.I_DRV_COEFF, 1.000);

            loadRecord.Objects.FromText(LeftGirderBars.First().Number.ToString() + "do" + LeftGirderBars.Last().Number.ToString() + " " +
                                        RightGirderBars.First().Number.ToString() + "do" + RightGirderBars.Last().Number.ToString());          
        }

        public void AddSuperimposedDeadLoad(int caseNumber, string caseName, string caseLabel)
        {
            RobotCaseServer robotCaseServer = RobotProject.Structure.Cases;
            RobotSimpleCase superimposedDeadLoadCase;
            if (CreateFromExistingFile)
            {
                superimposedDeadLoadCase = GetExistingCase(caseNumber, caseName);
                if (superimposedDeadLoadCase == null) superimposedDeadLoadCase = robotCaseServer.CreateSimple(caseNumber, caseName, IRobotCaseNature.I_CN_PERMANENT, IRobotCaseAnalizeType.I_CAT_STATIC_LINEAR);
            }
            else superimposedDeadLoadCase = robotCaseServer.CreateSimple(caseNumber, caseName, IRobotCaseNature.I_CN_PERMANENT, IRobotCaseAnalizeType.I_CAT_STATIC_LINEAR);
            RobotLoadRecordMngr records = superimposedDeadLoadCase.Records;
            superimposedDeadLoadCase.label = caseLabel;

            Cases.Add(caseNumber);
            IRobotLoadRecord loadRecord;

            records.New(IRobotLoadRecordType.I_LRT_BAR_UNIFORM);
            loadRecord = records.Get(records.Count);
            loadRecord.SetValue((short) IRobotBarUniformRecordValues.I_BURV_PZ, 1000 * (-18.0));
            loadRecord.Objects.FromText(LeftGirderBars.First().Number.ToString() + "do" + LeftGirderBars.Last().Number.ToString());

            records.New(IRobotLoadRecordType.I_LRT_BAR_UNIFORM);
            loadRecord = records.Get(records.Count);
            loadRecord.SetValue((short) IRobotBarUniformRecordValues.I_BURV_PZ, 1000 * (-24.0));
            loadRecord.Objects.FromText(RightGirderBars.First().Number.ToString() + "do" + RightGirderBars.Last().Number.ToString());

            records.New(IRobotLoadRecordType.I_LRT_BAR_MOMENT_DISTRIBUTED);
            loadRecord = records.Get(records.Count);
            loadRecord.SetValue((short) IRobotBarMomentDistributedRecordValues.I_BMDRV_MX, 1000 * (-2.0));
            loadRecord.Objects.FromText(LeftGirderBars.First().Number.ToString() + "do" + LeftGirderBars.Last().Number.ToString());

            records.New(IRobotLoadRecordType.I_LRT_BAR_MOMENT_DISTRIBUTED);
            loadRecord = records.Get(records.Count);
            loadRecord.SetValue((short) IRobotBarMomentDistributedRecordValues.I_BMDRV_MX, 1000 * 7.0);
            loadRecord.Objects.FromText(RightGirderBars.First().Number.ToString() + "do" + RightGirderBars.Last().Number.ToString());
        }

        public void AddPrestressingCase(int caseNumber, string caseName, DiscreteTendon tendon, bool doubleInverted = false)
        {
            RobotCaseServer robotCaseServer = RobotProject.Structure.Cases;
            RobotSimpleCase prestressingCase;
            if (CreateFromExistingFile)
            {
                prestressingCase = GetExistingCase(caseNumber, caseName);
                if (prestressingCase == null) prestressingCase = robotCaseServer.CreateSimple(caseNumber, caseName, IRobotCaseNature.I_CN_PERMANENT, IRobotCaseAnalizeType.I_CAT_STATIC_LINEAR);
            }
            else prestressingCase = robotCaseServer.CreateSimple(caseNumber, caseName, IRobotCaseNature.I_CN_PERMANENT, IRobotCaseAnalizeType.I_CAT_STATIC_LINEAR);
            RobotLoadRecordMngr records = prestressingCase.Records;

            Cases.Add(caseNumber);
            for (int i = 0; i < tendon.Points.Count(); i++)
            {
                IRobotBar bar = FindBar(tendon.Points[i].X);
                records.New(IRobotLoadRecordType.I_LRT_BAR_FORCE_CONCENTRATED);

                IRobotLoadRecord loadRecord = records.Get(prestressingCase.Records.Count);
                loadRecord.SetValue((short) IRobotBarForceConcentrateRecordValues.I_BFCRV_FX, Math.Round(1000 * tendon.Forces[i].FX, 1));
                loadRecord.SetValue((short) IRobotBarForceConcentrateRecordValues.I_BFCRV_FZ, Math.Round(1000 * tendon.Forces[i].FZ, 1));
                loadRecord.SetValue((short) IRobotBarForceConcentrateRecordValues.I_BFCRV_CY, Math.Round(1000 * tendon.Forces[i].MY, 1));
                loadRecord.SetValue((short) IRobotBarForceConcentrateRecordValues.I_BFCRV_REL, 0);
                loadRecord.SetValue((short) IRobotBarForceConcentrateRecordValues.I_BFCRV_GENERATE_CALC_NODE, ((i == 0) || (i == tendon.Points.Count() - 1)) ? 1 : 0);
                loadRecord.SetValue((short) IRobotBarForceConcentrateRecordValues.I_BFCRV_X, FindLoadPlacement(tendon.Points[i].X));

                loadRecord.Objects.AddText(bar.Number.ToString() + " " + (bar.Number + 1000).ToString());
            }

            if (doubleInverted)
            {
                double length = 0;
                foreach (IRobotBar bar in LeftGirderBars) length += bar.Length;

                for (int i = 0; i < tendon.Points.Count(); i++)
                {
                    IRobotBar bar = FindBar(length - tendon.Points[i].X);
                    records.New(IRobotLoadRecordType.I_LRT_BAR_FORCE_CONCENTRATED);

                    IRobotLoadRecord loadRecord = records.Get(prestressingCase.Records.Count);
                    loadRecord.SetValue((short) IRobotBarForceConcentrateRecordValues.I_BFCRV_FX, Math.Round(-1000 * tendon.Forces[i].FX, 1));
                    loadRecord.SetValue((short) IRobotBarForceConcentrateRecordValues.I_BFCRV_FZ, Math.Round(1000 * tendon.Forces[i].FZ, 1));
                    loadRecord.SetValue((short) IRobotBarForceConcentrateRecordValues.I_BFCRV_CY, Math.Round(-1000 * tendon.Forces[i].MY, 1));
                    loadRecord.SetValue((short) IRobotBarForceConcentrateRecordValues.I_BFCRV_REL, 0);
                    loadRecord.SetValue((short) IRobotBarForceConcentrateRecordValues.I_BFCRV_GENERATE_CALC_NODE, ((i == 0) || (i == tendon.Points.Count() - 1)) ? 1 : 0);
                    loadRecord.SetValue((short) IRobotBarForceConcentrateRecordValues.I_BFCRV_X, FindLoadPlacement(length - tendon.Points[i].X));

                    loadRecord.Objects.AddText(bar.Number.ToString() + " " + (bar.Number + 1000).ToString());
                }
            }
        }

        public void AddDifferentialSettlementCase(int caseNumber, string caseName, string caseLabel, int supportIndex, double settlementValue)
        {
            RobotCaseServer robotCaseServer = RobotProject.Structure.Cases;
            RobotSimpleCase differentialSettlementCase;
            if (CreateFromExistingFile)
            {
                differentialSettlementCase = GetExistingCase(caseNumber, caseName);
                if (differentialSettlementCase == null) differentialSettlementCase = robotCaseServer.CreateSimple(caseNumber, caseName, IRobotCaseNature.I_CN_PERMANENT, IRobotCaseAnalizeType.I_CAT_STATIC_LINEAR);
            }
            else differentialSettlementCase = robotCaseServer.CreateSimple(caseNumber, caseName, IRobotCaseNature.I_CN_PERMANENT, IRobotCaseAnalizeType.I_CAT_STATIC_LINEAR);
            RobotLoadRecordMngr records = differentialSettlementCase.Records;
            differentialSettlementCase.label = caseLabel;

            Cases.Add(caseNumber);
            records.New(IRobotLoadRecordType.I_LRT_NODE_DISPLACEMENT);
            IRobotLoadRecord loadRecord = records.Get(records.Count);
            loadRecord.SetValue((short) IRobotNodeDisplacementRecordValues.I_NDRV_UZ, settlementValue);
            loadRecord.Objects.FromText(SupportNodes[2 * (supportIndex - 1)].Number.ToString() + " " + SupportNodes[2 * (supportIndex - 1) + 1].Number.ToString());
        }

        public void AddThermalGradientCase(int caseNumber, string caseName, string caseLabel, double gradient)
        {
            RobotCaseServer robotCaseServer = RobotProject.Structure.Cases;
            RobotSimpleCase thermalGradientCase;
            if (CreateFromExistingFile)
            {
                thermalGradientCase = GetExistingCase(caseNumber, caseName);
                if (thermalGradientCase == null) thermalGradientCase = robotCaseServer.CreateSimple(caseNumber, caseName, IRobotCaseNature.I_CN_TEMPERATURE, IRobotCaseAnalizeType.I_CAT_STATIC_LINEAR);
            }
            else thermalGradientCase = robotCaseServer.CreateSimple(caseNumber, caseName, IRobotCaseNature.I_CN_TEMPERATURE, IRobotCaseAnalizeType.I_CAT_STATIC_LINEAR);
            RobotLoadRecordMngr records = thermalGradientCase.Records;
            thermalGradientCase.label = caseLabel;

            Cases.Add(caseNumber);
            records.New(IRobotLoadRecordType.I_LRT_BAR_THERMAL);
            IRobotLoadRecord loadRecord = records.Get(records.Count);
            loadRecord.SetValue((short) IRobotBarThermalRecordValues.I_BTRV_TZ, gradient);
            loadRecord.Objects.FromText(LeftGirderBars.First().Number.ToString() + "do" + LeftGirderBars.Last().Number.ToString() + " " +
                                        RightGirderBars.First().Number.ToString() + "do" + RightGirderBars.Last().Number.ToString());
        }

        public void AddUniformlyDistributedLoadCase(int caseNumber, string caseName, string caseLabel, List<double> values, List<AnalyticalContour> contours) //Load Model 1 (LM1), UDL component and footway load
        {
            RobotCaseServer robotCaseServer = RobotProject.Structure.Cases;
            RobotSimpleCase uniformlyDistributedLoadCase;
            if (CreateFromExistingFile)
            {
                uniformlyDistributedLoadCase = GetExistingCase(caseNumber, caseName);
                if (uniformlyDistributedLoadCase == null) uniformlyDistributedLoadCase = robotCaseServer.CreateSimple(caseNumber, caseName, IRobotCaseNature.I_CN_EXPLOATATION, IRobotCaseAnalizeType.I_CAT_STATIC_LINEAR);
            }
            else uniformlyDistributedLoadCase = robotCaseServer.CreateSimple(caseNumber, caseName, IRobotCaseNature.I_CN_EXPLOATATION, IRobotCaseAnalizeType.I_CAT_STATIC_LINEAR);
            RobotLoadRecordMngr records = uniformlyDistributedLoadCase.Records;
            uniformlyDistributedLoadCase.label = caseLabel;

            Cases.Add(caseNumber);
            foreach (AnalyticalContour contour in contours)
            {
                records.New(IRobotLoadRecordType.I_LRT_IN_CONTOUR);
                IRobotLoadRecord loadRecord = records.Get(records.Count);

                loadRecord.SetValue((short) IRobotInContourRecordValues.I_ICRV_PX1, 0.0);
                loadRecord.SetValue((short) IRobotInContourRecordValues.I_ICRV_PX2, 0.0);
                loadRecord.SetValue((short) IRobotInContourRecordValues.I_ICRV_PX3, 0.0);

                loadRecord.SetValue((short) IRobotInContourRecordValues.I_ICRV_PY1, 0.0);
                loadRecord.SetValue((short) IRobotInContourRecordValues.I_ICRV_PY2, 0.0);
                loadRecord.SetValue((short) IRobotInContourRecordValues.I_ICRV_PY3, 0.0);

                loadRecord.SetValue((short) IRobotInContourRecordValues.I_ICRV_PZ1, values[contours.IndexOf(contour)]);
                loadRecord.SetValue((short) IRobotInContourRecordValues.I_ICRV_PZ2, values[contours.IndexOf(contour)]);
                loadRecord.SetValue((short) IRobotInContourRecordValues.I_ICRV_PZ3, values[contours.IndexOf(contour)]);

                loadRecord.SetValue((short) IRobotInContourRecordValues.I_ICRV_NPOINTS, contour.Points.Count());

                int index = 1;
                foreach (RobotGeoPoint3D point in contour.Points)
                {
                    ((IRobotLoadRecordInContour) loadRecord).SetContourPoint(index, point.X, point.Y, point.Z);
                    index++;
                }
                ((IRobotLoadRecordInContour) loadRecord).SetVector(0, 0, -1);
                loadRecord.Objects.AddOne(Slab.Number);
            }
        }

        public void AddMobileLoadCase(int caseNumber, string caseName, string caseLabel, List<RobotGeoPoint3D> routePoints, double routeStep = 1.000, double axleLoad = 100000) //Load Model 1 (LM1), TS component, 100.0 kN by default
        {
            RobotCaseServer robotCaseServer = RobotProject.Structure.Cases;
            RobotMobileCase mobileLoadCase = robotCaseServer.CreateMobile(caseNumber, caseName, IRobotCaseNature.I_CN_EXPLOATATION);
            mobileLoadCase.label = caseLabel;

            MobileCases.Add(caseNumber);
            RobotGeoPolyline routePolyline = Robot.Application.CmpntFactory.Create(IRobotComponentType.I_CT_GEO_POLYLINE);
            foreach (RobotGeoPoint3D routePoint in routePoints)
            {
                RobotGeoSegment routeSegment = Robot.Application.CmpntFactory.Create(IRobotComponentType.I_CT_GEO_SEGMENT_LINE);
                routeSegment.P1.Set(routePoint.X, routePoint.Y, routePoint.Z);
                routePolyline.Add(routeSegment);
                routePolyline.Initialize();
            }
            MobileRoutes.Add(RobotProject.Structure.Objects.FreeNumber);
            RobotObjObject routeObject = RobotProject.Structure.Objects.Create(MobileRoutes.Last());
            routeObject.Main.Geometry = (RobotGeoObject) routePolyline;
            routeObject.Initialize();
            routeObject.Update();

            RobotMobileCaseRoute route = mobileLoadCase.GetRoute();
            route.Geometry = routeObject.Number;
            
            RobotMobileCaseSegmentFactors factors = route.GetFactors(1);
            factors.VL = 1;
            factors.VR = 1;
            route.SetFactors(1, factors);

            route.LoadDirection.Set(0, 0, -1);
            route.Step = routeStep;
            mobileLoadCase.SetRoute(route);

            if (RobotProject.Structure.Labels.Exist(IRobotLabelType.I_LT_VEHICLE, "TS") == 0)
            {
                IRobotLabel vehicleLabel = RobotProject.Structure.Labels.Create(IRobotLabelType.I_LT_VEHICLE, "TS");
                RobotVehicleData vehicleData = vehicleLabel.Data;

                RobotVehicleLoad vehicleLoadFirstAxle = vehicleData.Loads.New();
                vehicleLoadFirstAxle.Type = IRobotVehicleLoadType.I_VLT_SURFACE;
                vehicleLoadFirstAxle.X = -0.800; vehicleLoadFirstAxle.S = 2.000;
                vehicleLoadFirstAxle.DX = 0.400; vehicleLoadFirstAxle.DY = 0.400;
                vehicleLoadFirstAxle.F = axleLoad / (4 * 0.400 * 0.400);

                RobotVehicleLoad vehicleLoadSecondAxle = vehicleData.Loads.New();
                vehicleLoadSecondAxle.Type = IRobotVehicleLoadType.I_VLT_SURFACE;
                vehicleLoadSecondAxle.X = 0.400; vehicleLoadSecondAxle.S = 2.000;
                vehicleLoadSecondAxle.DX = 0.400; vehicleLoadSecondAxle.DY = 0.400;
                vehicleLoadSecondAxle.F = axleLoad / (4 * 0.400 * 0.400);

                vehicleData.B = 2.400;
                RobotProject.Structure.Labels.Store(vehicleLabel);
            }
            mobileLoadCase.Vehicle = "TS";
        }

        private int FindEnvelopeForMobileCase(int caseNumber, bool maximum)
        {
            RobotCaseServer robotCaseServer = RobotProject.Structure.Cases;

            string caseName = robotCaseServer.Get(caseNumber).Name;
            string suffix = maximum ? "+" : "-";

            RobotCaseCollection cases = robotCaseServer.GetAll();
            for (int i = 1; i <= cases.Count; i++)
            {
                if (cases.Get(i) is RobotMobileCase)
                {
                    if (((RobotMobileCase) cases.Get(i)).Name == caseName + suffix)
                    {
                        return ((RobotMobileCase) cases.Get(i)).Number;
                    }
                }
            }
            return 0;
        }

        private IRobotBar FindBar(double x)
        {
            double length = 0;
            foreach (IRobotBar bar in LeftGirderBars)
            {
                if (x <= length + bar.Length) return bar;
                length += bar.Length;
            }
            return LeftGirderBars.Last();
        }

        private double FindLoadPlacement(double x)
        {
            double length = 0;
            foreach (IRobotBar bar in LeftGirderBars)
            {
                if (x <= length + bar.Length) break;
                length += bar.Length;
            }
            return x - length;
        }
    }
}