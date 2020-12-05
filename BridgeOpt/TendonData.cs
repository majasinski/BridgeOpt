using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Autodesk.Revit.UI;

namespace BridgeOpt
{
    public class LayoutDefinition
    {
        public enum PrestressTypes
        {
            LeftSided = 0,
            RightSided = 1,
            DoubleSided = 2
        }

        public class TendonPoint
        {
            public double X;
            public double Z;

            public TendonPoint(double x = 0, double z = 0)
            {
                X = x;
                Z = z;
            }
        }

        public class TendonSegment
        {
            public TendonPoint StartPoint;
            public TendonPoint EndPoint;

            public double ArcRadius = 0;
            public TendonPoint ArcCenter = null;

            public TendonSegment(TendonPoint startPoint, TendonPoint endPoint, double arcRadius = 0, TendonPoint arcCenter = null)
            {
                StartPoint = startPoint;
                EndPoint = endPoint;

                ArcRadius = arcRadius;
                ArcCenter = arcCenter;
            }

            public bool IsArc()
            {
                return !(ArcCenter == null);
            }
        }

        public class ForceSet
        {
            public double FX;
            public double FZ;
            public double MY;

            public ForceSet(double forceFX, double forceFZ, double forceMY)
            {
                FX = forceFX;
                FZ = forceFZ;
                MY = forceMY;
            }
        }

        public class FullTendon
        {
            public TendonPoint PointA;
            public TendonPoint PointB;
            public TendonPoint PointC;
            public TendonPoint PointD;
            public TendonPoint PointE;
            public double TransferLengthA;
            public double TransferLengthE;

            public double SupportRadius;

            public readonly TendonPoint Point1;
            public readonly TendonPoint Point2;
            public readonly TendonPoint Point3;
            public readonly TendonPoint Point4;
            public readonly List<TendonPoint> TendonPoints = new List<TendonPoint>();

            public readonly TendonSegment SegmentA1;
            public readonly TendonSegment Segment1B;
            public readonly TendonSegment SegmentB2;
            public readonly TendonSegment Segment2C;
            public readonly TendonSegment SegmentC3;
            public readonly TendonSegment Segment3D;
            public readonly TendonSegment SegmentD4;
            public readonly TendonSegment Segment4E;
            public readonly List<TendonSegment> TendonSegments = new List<TendonSegment>();

            public double PrestressForce;
            public PrestressTypes PrestressType;
            public double TendonArea;
            public double PrestressSteelElasticityModulus = 195; //GPa

            public FullTendon(TendonPoint pointA, TendonPoint pointB, TendonPoint pointC, TendonPoint pointD, TendonPoint pointE, double transferLengthA, double transferLengthE, double supportRadius)
            {
                PointA = pointA;
                PointB = pointB;
                PointC = pointC;
                PointD = pointD;
                PointE = pointE;

                TransferLengthA = transferLengthA;
                TransferLengthE = transferLengthE;

                if (supportRadius > 0) supportRadius = -1 * supportRadius;
                SupportRadius = supportRadius;

                Point1 = TangentPointByTangencyAndLength(pointB, 0, pointA, transferLengthA);
                Point2 = TangentPointByTangencyAndTangentArc(pointB, 0, pointC, 0, supportRadius);
                Point3 = TangentPointByTangencyAndTangentArc(pointD, 0, pointC, 0, supportRadius);
                Point4 = TangentPointByTangencyAndLength(pointD, 0, pointE, transferLengthE);

                SegmentA1 = new TendonSegment(PointA, Point1);
                Segment1B = new TendonSegment(Point1, PointB, RadiusByTangencyAndLength(PointB, 0, PointA, transferLengthA), CenterByTangencyAndLength(PointB, 0, PointA, transferLengthA));
                SegmentB2 = new TendonSegment(PointB, Point2, RadiusByTangencyAndTangentArc(PointB, 0, PointC, 0, supportRadius), CenterByTangencyAndTangentArc(PointB, 0, PointC, 0, supportRadius));
                Segment2C = new TendonSegment(Point2, PointC, supportRadius, CenterByTangencyAndRadius(PointC, 0, supportRadius));
                SegmentC3 = new TendonSegment(PointC, Point3, supportRadius, CenterByTangencyAndRadius(PointC, 0, supportRadius));
                Segment3D = new TendonSegment(Point3, PointD, RadiusByTangencyAndTangentArc(PointD, 0, PointC, 0, supportRadius), CenterByTangencyAndTangentArc(PointD, 0, PointC, 0, supportRadius));
                SegmentD4 = new TendonSegment(PointD, Point4, RadiusByTangencyAndLength(PointD, 0, PointE, transferLengthE), CenterByTangencyAndLength(PointD, 0, PointE, transferLengthE));
                Segment4E = new TendonSegment(Point4, PointE);

                TendonPoints.Add(PointA);
                TendonPoints.Add(Point1); TendonSegments.Add(SegmentA1);
                TendonPoints.Add(PointB); TendonSegments.Add(Segment1B);
                TendonPoints.Add(Point2); TendonSegments.Add(SegmentB2);
                TendonPoints.Add(PointC); TendonSegments.Add(Segment2C);
                TendonPoints.Add(Point3); TendonSegments.Add(SegmentC3);
                TendonPoints.Add(PointD); TendonSegments.Add(Segment3D);
                TendonPoints.Add(Point4); TendonSegments.Add(SegmentD4);
                TendonPoints.Add(PointE); TendonSegments.Add(Segment4E);
            }

            public FullTendon(double xA, double zA, double xB, double zB, double xC, double zC, double xD, double zD, double xE, double zE, double transferLengthA, double transferLengthE, double supportRadius)
                : this(new TendonPoint(xA, zA),
                       new TendonPoint(xB, zB),
                       new TendonPoint(xC, zC),
                       new TendonPoint(xD, zD),
                       new TendonPoint(xE, zE),
                       transferLengthA, transferLengthE, supportRadius) { }

            public string ToScr(double multiplier = 1000)
            {
                string scr;

                scr = "_LINE " + (multiplier * SegmentA1.StartPoint.X).ToString() + "," + (multiplier * SegmentA1.StartPoint.Z).ToString() + " " + (multiplier * SegmentA1.EndPoint.X).ToString() + "," + (multiplier * SegmentA1.EndPoint.Z).ToString() + "  ";
                if (Segment1B.ArcCenter.Z > Segment1B.StartPoint.Z) scr = scr + "_ARC _C " + (multiplier * Segment1B.ArcCenter.X).ToString() + "," + (multiplier * Segment1B.ArcCenter.Z).ToString() + " " + (multiplier * Segment1B.StartPoint.X).ToString() + "," + (multiplier * Segment1B.StartPoint.Z).ToString() + " " + (multiplier * Segment1B.EndPoint.X).ToString() + "," + (multiplier * Segment1B.EndPoint.Z).ToString() + " ";
                else scr = scr + "_ARC _C " + (multiplier * Segment1B.ArcCenter.X).ToString() + "," + (multiplier * Segment1B.ArcCenter.Z).ToString() + " " + (multiplier * Segment1B.EndPoint.X).ToString() + "," + (multiplier * Segment1B.EndPoint.Z).ToString() + " " + (multiplier * Segment1B.StartPoint.X).ToString() + "," + (multiplier * Segment1B.StartPoint.Z).ToString() + " ";
                if (SegmentB2.ArcCenter.Z > SegmentB2.StartPoint.Z) scr = scr + "_ARC _C " + (multiplier * SegmentB2.ArcCenter.X).ToString() + "," + (multiplier * SegmentB2.ArcCenter.Z).ToString() + " " + (multiplier * SegmentB2.StartPoint.X).ToString() + "," + (multiplier * SegmentB2.StartPoint.Z).ToString() + " " + (multiplier * SegmentB2.EndPoint.X).ToString() + "," + (multiplier * SegmentB2.EndPoint.Z).ToString() + " ";
                else scr = scr + "_ARC _C " + (multiplier * SegmentB2.ArcCenter.X).ToString() + "," + (multiplier * SegmentB2.ArcCenter.Z).ToString() + " " + (multiplier * SegmentB2.EndPoint.X).ToString() + "," + (multiplier * SegmentB2.EndPoint.Z).ToString() + " " + (multiplier * SegmentB2.StartPoint.X).ToString() + "," + (multiplier * SegmentB2.StartPoint.Z).ToString() + " ";
                if (Segment2C.ArcCenter.Z > Segment2C.StartPoint.Z) scr = scr + "_ARC _C " + (multiplier * Segment2C.ArcCenter.X).ToString() + "," + (multiplier * Segment2C.ArcCenter.Z).ToString() + " " + (multiplier * Segment2C.StartPoint.X).ToString() + "," + (multiplier * Segment2C.StartPoint.Z).ToString() + " " + (multiplier * Segment2C.EndPoint.X).ToString() + "," + (multiplier * Segment2C.EndPoint.Z).ToString() + " ";
                else scr = scr + "_ARC _C " + (multiplier * Segment2C.ArcCenter.X).ToString() + "," + (multiplier * Segment2C.ArcCenter.Z).ToString() + " " + (multiplier * Segment2C.EndPoint.X).ToString() + "," + (multiplier * Segment2C.EndPoint.Z).ToString() + " " + (multiplier * Segment2C.StartPoint.X).ToString() + "," + (multiplier * Segment2C.StartPoint.Z).ToString() + " ";
                if (SegmentC3.ArcCenter.Z > SegmentC3.StartPoint.Z) scr = scr + "_ARC _C " + (multiplier * SegmentC3.ArcCenter.X).ToString() + "," + (multiplier * SegmentC3.ArcCenter.Z).ToString() + " " + (multiplier * SegmentC3.StartPoint.X).ToString() + "," + (multiplier * SegmentC3.StartPoint.Z).ToString() + " " + (multiplier * SegmentC3.EndPoint.X).ToString() + "," + (multiplier * SegmentC3.EndPoint.Z).ToString() + " ";
                else scr = scr + "_ARC _C " + (multiplier * SegmentC3.ArcCenter.X).ToString() + "," + (multiplier * SegmentC3.ArcCenter.Z).ToString() + " " + (multiplier * SegmentC3.EndPoint.X).ToString() + "," + (multiplier * SegmentC3.EndPoint.Z).ToString() + " " + (multiplier * SegmentC3.StartPoint.X).ToString() + "," + (multiplier * SegmentC3.StartPoint.Z).ToString() + " ";
                if (Segment3D.ArcCenter.Z > Segment3D.StartPoint.Z) scr = scr + "_ARC _C " + (multiplier * Segment3D.ArcCenter.X).ToString() + "," + (multiplier * Segment3D.ArcCenter.Z).ToString() + " " + (multiplier * Segment3D.StartPoint.X).ToString() + "," + (multiplier * Segment3D.StartPoint.Z).ToString() + " " + (multiplier * Segment3D.EndPoint.X).ToString() + "," + (multiplier * Segment3D.EndPoint.Z).ToString() + " ";
                else scr = scr + "_ARC _C " + (multiplier * Segment3D.ArcCenter.X).ToString() + "," + (multiplier * Segment3D.ArcCenter.Z).ToString() + " " + (multiplier * Segment3D.EndPoint.X).ToString() + "," + (multiplier * Segment3D.EndPoint.Z).ToString() + " " + (multiplier * Segment3D.StartPoint.X).ToString() + "," + (multiplier * Segment3D.StartPoint.Z).ToString() + " ";
                if (SegmentD4.ArcCenter.Z > SegmentD4.StartPoint.Z) scr = scr + "_ARC _C " + (multiplier * SegmentD4.ArcCenter.X).ToString() + "," + (multiplier * SegmentD4.ArcCenter.Z).ToString() + " " + (multiplier * SegmentD4.StartPoint.X).ToString() + "," + (multiplier * SegmentD4.StartPoint.Z).ToString() + " " + (multiplier * SegmentD4.EndPoint.X).ToString() + "," + (multiplier * SegmentD4.EndPoint.Z).ToString() + " ";
                else scr = scr + "_ARC _C " + (multiplier * SegmentD4.ArcCenter.X).ToString() + "," + (multiplier * SegmentD4.ArcCenter.Z).ToString() + " " + (multiplier * SegmentD4.EndPoint.X).ToString() + "," + (multiplier * SegmentD4.EndPoint.Z).ToString() + " " + (multiplier * SegmentD4.StartPoint.X).ToString() + "," + (multiplier * SegmentD4.StartPoint.Z).ToString() + " ";
                scr = scr + "_LINE " + (multiplier * Segment4E.StartPoint.X).ToString() + "," + (multiplier * Segment4E.StartPoint.Z).ToString() + " " + (multiplier * Segment4E.EndPoint.X).ToString() + "," + (multiplier * Segment4E.EndPoint.Z).ToString() + "  ";

                return scr;
            }

            public bool IsValid()
            {
                if (double.IsNaN(Point1.Z)) return false;
                if (double.IsNaN(Point2.Z)) return false;
                if (double.IsNaN(Point3.Z)) return false;
                if (double.IsNaN(Point4.Z)) return false;
                return true;
            }
        }

        public class PartialTendon
        {
            public TendonPoint PointA;
            public TendonPoint PointB;
            public TendonPoint PointC;
            public TendonPoint PointD;
            public double TransferLengthA;
            public double TransferLengthC;

            public double SupportRadius;

            public readonly TendonPoint Point1;
            public readonly TendonPoint Point2;
            public readonly List<TendonPoint> TendonPoints = new List<TendonPoint>();

            public readonly TendonSegment SegmentA1;
            public readonly TendonSegment Segment1B;
            public readonly TendonSegment SegmentB2;
            public readonly TendonSegment Segment2C;
            public readonly TendonSegment SegmentCD;
            public readonly List<TendonSegment> TendonSegments = new List<TendonSegment>();

            public double PrestressForce;
            public double TendonArea;
            public double PrestressSteelElasticityModulus = 195; //GPa

            public PartialTendon(TendonPoint pointA, TendonPoint pointB, TendonPoint pointC, double transferLengthA, double transferLengthC, double supportRadius)
            {
                PointA = pointA;
                PointB = pointB;
                PointC = pointC;
                PointD = new TendonPoint(pointC.X + transferLengthC, pointC.Z);

                TransferLengthA = transferLengthA;
                TransferLengthC = transferLengthC;

                if (supportRadius > 0) supportRadius = -1 * supportRadius;
                SupportRadius = supportRadius;

                Point1 = TangentPointByTangencyAndLength(pointB, 0, pointA, transferLengthA);
                Point2 = TangentPointByTangencyAndTangentArc(pointB, 0, pointC, 0, supportRadius);

                SegmentA1 = new TendonSegment(PointA, Point1);
                Segment1B = new TendonSegment(Point1, PointB, RadiusByTangencyAndLength(PointB, 0, PointA, transferLengthA), CenterByTangencyAndLength(PointB, 0, PointA, transferLengthA));
                SegmentB2 = new TendonSegment(PointB, Point2, RadiusByTangencyAndTangentArc(PointB, 0, PointC, 0, supportRadius), CenterByTangencyAndTangentArc(PointB, 0, PointC, 0, supportRadius));
                Segment2C = new TendonSegment(Point2, PointC, supportRadius, CenterByTangencyAndRadius(PointC, 0, supportRadius));
                SegmentCD = new TendonSegment(PointC, PointD);

                TendonPoints.Add(PointA); TendonSegments.Add(SegmentA1);
                TendonPoints.Add(Point1); TendonSegments.Add(Segment1B);
                TendonPoints.Add(PointB); TendonSegments.Add(SegmentB2);
                TendonPoints.Add(Point2); TendonSegments.Add(Segment2C);
                TendonPoints.Add(PointC); TendonSegments.Add(SegmentCD);
                TendonPoints.Add(PointD);
            }

            public PartialTendon(double xA, double zA, double xB, double zB, double xC, double zC, double transferLengthA, double transferLengthC, double supportRadius)
                : this(new TendonPoint(xA, zA),
                       new TendonPoint(xB, zB),
                       new TendonPoint(xC, zC),
                       transferLengthA, transferLengthC, supportRadius)
            { }

            public string ToScr(double multiplier = 1000)
            {
                string scr;
                
                scr = "_LINE " + (multiplier * SegmentA1.StartPoint.X).ToString() + "," + (multiplier * SegmentA1.StartPoint.Z).ToString() + " " + (multiplier * SegmentA1.EndPoint.X).ToString() + "," + (multiplier * SegmentA1.EndPoint.Z).ToString() + "  ";
                if (Segment1B.ArcCenter.Z > Segment1B.StartPoint.Z) scr = scr + "_ARC _C " + (multiplier * Segment1B.ArcCenter.X).ToString() + "," + (multiplier * Segment1B.ArcCenter.Z).ToString() + " " + (multiplier * Segment1B.StartPoint.X).ToString() + "," + (multiplier * Segment1B.StartPoint.Z).ToString() + " " + (multiplier * Segment1B.EndPoint.X).ToString() + "," + (multiplier * Segment1B.EndPoint.Z).ToString() + " ";
                else scr = scr + "_ARC _C " + (multiplier * Segment1B.ArcCenter.X).ToString() + "," + (multiplier * Segment1B.ArcCenter.Z).ToString() + " " + (multiplier * Segment1B.EndPoint.X).ToString() + "," + (multiplier * Segment1B.EndPoint.Z).ToString() + " " + (multiplier * Segment1B.StartPoint.X).ToString() + "," + (multiplier * Segment1B.StartPoint.Z).ToString() + " ";
                if (SegmentB2.ArcCenter.Z > SegmentB2.StartPoint.Z) scr = scr + "_ARC _C " + (multiplier * SegmentB2.ArcCenter.X).ToString() + "," + (multiplier * SegmentB2.ArcCenter.Z).ToString() + " " + (multiplier * SegmentB2.StartPoint.X).ToString() + "," + (multiplier * SegmentB2.StartPoint.Z).ToString() + " " + (multiplier * SegmentB2.EndPoint.X).ToString() + "," + (multiplier * SegmentB2.EndPoint.Z).ToString() + " ";
                else scr = scr + "_ARC _C " + (multiplier * SegmentB2.ArcCenter.X).ToString() + "," + (multiplier * SegmentB2.ArcCenter.Z).ToString() + " " + (multiplier * SegmentB2.EndPoint.X).ToString() + "," + (multiplier * SegmentB2.EndPoint.Z).ToString() + " " + (multiplier * SegmentB2.StartPoint.X).ToString() + "," + (multiplier * SegmentB2.StartPoint.Z).ToString() + " ";
                if (Segment2C.ArcCenter.Z > Segment2C.StartPoint.Z) scr = scr + "_ARC _C " + (multiplier * Segment2C.ArcCenter.X).ToString() + "," + (multiplier * Segment2C.ArcCenter.Z).ToString() + " " + (multiplier * Segment2C.StartPoint.X).ToString() + "," + (multiplier * Segment2C.StartPoint.Z).ToString() + " " + (multiplier * Segment2C.EndPoint.X).ToString() + "," + (multiplier * Segment2C.EndPoint.Z).ToString() + " ";
                else scr = scr + "_ARC _C " + (multiplier * Segment2C.ArcCenter.X).ToString() + "," + (multiplier * Segment2C.ArcCenter.Z).ToString() + " " + (multiplier * Segment2C.EndPoint.X).ToString() + "," + (multiplier * Segment2C.EndPoint.Z).ToString() + " " + (multiplier * Segment2C.StartPoint.X).ToString() + "," + (multiplier * Segment2C.StartPoint.Z).ToString() + " ";
                scr = scr + "_LINE " + (multiplier * SegmentCD.StartPoint.X).ToString() + "," + (multiplier * SegmentCD.StartPoint.Z).ToString() + " " + (multiplier * SegmentCD.EndPoint.X).ToString() + "," + (multiplier * SegmentCD.EndPoint.Z).ToString() + "  ";

                return scr;
            }

            public bool IsValid()
            {
                if (double.IsNaN(Point1.Z)) return false;
                if (double.IsNaN(Point2.Z)) return false;
                return true;
            }
        }

        public class DiscreteTendon
        {
            public object Tendon;
            public double InitialForce;

            public List<TendonPoint> Points = new List<TendonPoint>();
            public List<double> Lengths = new List<double>();
            public List<double> Tangents = new List<double>();
            public List<double> AngularSums = new List<double>();

            public List<double> FrictionLosses = new List<double>();
            public List<double> SlipLosses = new List<double>();
            public List<double> PrestressForces = new List<double>();

            public List<ForceSet> Forces = new List<ForceSet>();

            public DiscreteTendon(object tendon, double z, List<double> sections)
            {
                Tendon = tendon;
                List<double> calcSections = new List<double>();
                foreach (double x in sections) calcSections.Add(x);

                double totalLength = 0;
                if (Tendon is FullTendon)
                {
                    totalLength = ((FullTendon) Tendon).TendonSegments.Last().EndPoint.X;
                    InitialForce = ((FullTendon) Tendon).PrestressForce;
                }
                else if (Tendon is PartialTendon)
                {
                    totalLength = ((PartialTendon) Tendon).TendonSegments.Last().EndPoint.X;
                    InitialForce = ((PartialTendon) Tendon).PrestressForce;
                }

                if (calcSections.First() != 0.0) calcSections.Insert(0, 0.0);
                if (calcSections.Last() != totalLength) calcSections.Add(totalLength);

                foreach (double x in calcSections)
                {
                    Points.Add(new TendonPoint(x, GetTendonZ(x)));
                    Lengths.Add(GetTendonLength(x));
                    Tangents.Add(GetTendonTan(x));
                    AngularSums.Add(GetTendonAngularSum(x));

                    FrictionLosses.Add(GetFrictionLosses(x));
                    SlipLosses.Add(GetSlipLosses(x));
                    PrestressForces.Add(GetTendonForce(x));
                }

                foreach (TendonPoint point in Points)
                {
                    int i = Points.IndexOf(point);
                    if (point.X == 0)
                    {
                        Forces.Add(new ForceSet(PrestressForces[i] * Math.Cos(Math.Atan(Tangents[i])), PrestressForces[i] * Math.Sin(Math.Atan(Tangents[i])), PrestressForces[i] * Math.Cos(Math.Atan(Tangents[i])) * (Points[i].Z - z)));

                        Forces[i].FX = Forces[i].FX - 0.5 * (PrestressForces[i] * Math.Cos(Math.Atan(Tangents[i])) - PrestressForces[i + 1] * Math.Cos(Math.Atan(Tangents[i + 1])));
                        Forces[i].FZ = Forces[i].FZ - 0.5 * (PrestressForces[i] * Math.Sin(Math.Atan(Tangents[i])) - PrestressForces[i + 1] * Math.Sin(Math.Atan(Tangents[i + 1])));
                        Forces[i].MY = Forces[i].MY - 0.5 * (PrestressForces[i] * Math.Cos(Math.Atan(Tangents[i])) - PrestressForces[i + 1] * Math.Cos(Math.Atan(Tangents[i + 1]))) * (Points[i].Z + 0.5 * (Points[i + 1].Z - Points[i].Z) - z);
                    }
                    else if (point.X == totalLength)
                    {
                        Forces.Add(new ForceSet(PrestressForces[i] * Math.Cos(Math.Atan(Tangents[i])), PrestressForces[i] * Math.Sin(Math.Atan(Tangents[i])), PrestressForces[i] * Math.Cos(Math.Atan(Tangents[i])) * (Points[i].Z - z)));

                        Forces[i].FX = -1 * Forces[i].FX + 0.5 * (PrestressForces[i] * Math.Cos(Math.Atan(Tangents[i])) - PrestressForces[i - 1] * Math.Cos(Math.Atan(Tangents[i - 1])));
                        Forces[i].FZ = -1 * Forces[i].FZ + 0.5 * (PrestressForces[i] * Math.Sin(Math.Atan(Tangents[i])) - PrestressForces[i - 1] * Math.Sin(Math.Atan(Tangents[i - 1])));
                        Forces[i].MY = -1 * Forces[i].MY + 0.5 * (PrestressForces[i] * Math.Cos(Math.Atan(Tangents[i])) - PrestressForces[i - 1] * Math.Cos(Math.Atan(Tangents[i - 1]))) * (Points[i].Z + 0.5 * (Points[i - 1].Z - Points[i].Z) - z);
                    }
                    else
                    {
                        //Forces.Add(new ForceSet(0.0, PrestressForces[i] * Math.Sin(Math.Atan(Tangents[i])), PrestressForces[i] * Math.Cos(Math.Atan(Tangents[i])) * (Points[i].Z - zsc)));
                        Forces.Add(new ForceSet(0.0, 0.0, 0.0));

                        Forces[i].FX = Forces[i].FX - 0.5 * (PrestressForces[i] * Math.Cos(Math.Atan(Tangents[i])) - PrestressForces[i + 1] * Math.Cos(Math.Atan(Tangents[i + 1])));
                        Forces[i].FX = Forces[i].FX + 0.5 * (PrestressForces[i] * Math.Cos(Math.Atan(Tangents[i])) - PrestressForces[i - 1] * Math.Cos(Math.Atan(Tangents[i - 1])));

                        Forces[i].FZ = Forces[i].FZ - 0.5 * (PrestressForces[i] * Math.Sin(Math.Atan(Tangents[i])) - PrestressForces[i + 1] * Math.Sin(Math.Atan(Tangents[i + 1])));
                        Forces[i].FZ = Forces[i].FZ + 0.5 * (PrestressForces[i] * Math.Sin(Math.Atan(Tangents[i])) - PrestressForces[i - 1] * Math.Sin(Math.Atan(Tangents[i - 1])));

                        Forces[i].MY = Forces[i].MY - 0.5 * (PrestressForces[i] * Math.Cos(Math.Atan(Tangents[i])) - PrestressForces[i + 1] * Math.Cos(Math.Atan(Tangents[i + 1]))) * (Points[i].Z + 0.5 * (Points[i + 1].Z - Points[i].Z) - z);
                        Forces[i].MY = Forces[i].MY + 0.5 * (PrestressForces[i] * Math.Cos(Math.Atan(Tangents[i])) - PrestressForces[i - 1] * Math.Cos(Math.Atan(Tangents[i - 1]))) * (Points[i].Z + 0.5 * (Points[i - 1].Z - Points[i].Z) - z);
                    }
                }
            }

            public DiscreteTendon(object tendon, double z, double dx = 0.5) : this(tendon, z, CalculateSections(tendon, dx)) { }

            public void PrintToFile(string filePath)
            {
                if (Points.Count() == 0) return;
                using (StreamWriter outputFile = new StreamWriter(filePath))
                {
                    for (int i = 0; i < Points.Count(); i++)
                    {
                        outputFile.WriteLine((i + 1).ToString() + "\t" + Points[i].X + "\t" + Points[i].Z + "\t" + Lengths[i] + "\t" + Tangents[i] + "\t" + AngularSums[i] + "\t" + FrictionLosses[i] + "\t" + SlipLosses[i] + "\t" + PrestressForces[i] + "\t" + Forces[i].FX + "\t" + Forces[i].FZ + "\t" + Forces[i].MY);
                    }
                }
            }

            public bool IsFullTendon()
            {
                return Tendon is FullTendon;
            }
            public FullTendon GetFullTendon()
            {
                if (IsFullTendon()) return (FullTendon) Tendon;
                else return null;
            }

            public bool IsPartialTendon()
            {
                return Tendon is PartialTendon;
            }
            public PartialTendon GetPartialTendon()
            {
                if (IsPartialTendon()) return (PartialTendon) Tendon;
                else return null;
            }

            public double GetTendonZ(double x)
            {
                List<TendonSegment> tendonSegments = new List<TendonSegment>();
                if (Tendon is FullTendon) tendonSegments = ((FullTendon)Tendon).TendonSegments;
                else if (Tendon is PartialTendon) tendonSegments = ((PartialTendon)Tendon).TendonSegments;

                foreach (TendonSegment tendonSegment in tendonSegments)
                {
                    if ((x >= tendonSegment.StartPoint.X) && (x <= tendonSegment.EndPoint.X))
                    {
                        if (tendonSegment.IsArc())
                        {
                            if (tendonSegment.ArcRadius > 0) return -1 * Math.Sqrt(Math.Pow(tendonSegment.ArcRadius, 2) - Math.Pow(x - tendonSegment.ArcCenter.X, 2)) + tendonSegment.ArcCenter.Z;
                            return Math.Sqrt(Math.Pow(tendonSegment.ArcRadius, 2) - Math.Pow(x - tendonSegment.ArcCenter.X, 2)) + tendonSegment.ArcCenter.Z;
                        }
                        return tendonSegment.StartPoint.Z + (tendonSegment.EndPoint.Z - tendonSegment.StartPoint.Z) * (x - tendonSegment.StartPoint.X) / (tendonSegment.EndPoint.X - tendonSegment.StartPoint.X);
                    }
                }
                return double.NaN;
            }

            public double GetTendonTan(double x)
            {
                List<TendonSegment> tendonSegments = new List<TendonSegment>();
                if (Tendon is FullTendon) tendonSegments = ((FullTendon) Tendon).TendonSegments;
                else if (Tendon is PartialTendon) tendonSegments = ((PartialTendon) Tendon).TendonSegments;

                foreach (TendonSegment tendonSegment in tendonSegments)
                {
                    if ((x >= tendonSegment.StartPoint.X) && (x <= tendonSegment.EndPoint.X))
                    {
                        if (tendonSegment.IsArc())
                        {
                            return -1 * (x - tendonSegment.ArcCenter.X) / (GetTendonZ(x) - tendonSegment.ArcCenter.Z);
                        }
                        return (tendonSegment.EndPoint.Z - tendonSegment.StartPoint.Z) / (tendonSegment.EndPoint.X - tendonSegment.StartPoint.X);
                    }
                }
                return double.NaN;
            }

            public double GetTendonAngularSum(double x)
            {
                double angularSum = 0;

                List<TendonSegment> tendonSegments = new List<TendonSegment>();
                if (Tendon is FullTendon) tendonSegments = ((FullTendon) Tendon).TendonSegments;
                else if (Tendon is PartialTendon) tendonSegments = ((PartialTendon) Tendon).TendonSegments;

                foreach (TendonSegment tendonSegment in tendonSegments)
                {
                    if (x > tendonSegment.EndPoint.X)
                    {
                        if (tendonSegment.IsArc()) angularSum += GetArcLength(tendonSegment.StartPoint, tendonSegment.EndPoint, tendonSegment.ArcRadius) / Math.Abs(tendonSegment.ArcRadius);
                    }
                    else
                    {
                        TendonPoint middlePoint = new TendonPoint(x, GetTendonZ(x));
                        if (tendonSegment.IsArc()) angularSum += GetArcLength(tendonSegment.StartPoint, middlePoint, tendonSegment.ArcRadius) / Math.Abs(tendonSegment.ArcRadius);
                        break;
                    }
                }
                return angularSum;
            }

            public double GetTendonLength(double x)
            {
                double length = 0;

                List<TendonSegment> tendonSegments = new List<TendonSegment>();
                if (Tendon is FullTendon) tendonSegments = ((FullTendon) Tendon).TendonSegments;
                else if (Tendon is PartialTendon) tendonSegments = ((PartialTendon) Tendon).TendonSegments;

                foreach (TendonSegment tendonSegment in tendonSegments)
                {
                    if (x > tendonSegment.EndPoint.X)
                    {
                        if (tendonSegment.IsArc()) length += GetArcLength(tendonSegment.StartPoint, tendonSegment.EndPoint, tendonSegment.ArcRadius);
                        else length += Math.Sqrt(Math.Pow(tendonSegment.EndPoint.X - tendonSegment.StartPoint.X, 2) + Math.Pow(tendonSegment.EndPoint.Z - tendonSegment.StartPoint.Z, 2));
                    }
                    else
                    {
                        TendonPoint middlePoint = new TendonPoint(x, GetTendonZ(x));
                        if (tendonSegment.IsArc()) length += GetArcLength(tendonSegment.StartPoint, middlePoint, tendonSegment.ArcRadius);
                        else length += Math.Sqrt(Math.Pow(middlePoint.X - tendonSegment.StartPoint.X, 2) + Math.Pow(middlePoint.Z - tendonSegment.StartPoint.Z, 2));

                        break;
                    }
                }
                return length;
            }

            private double GetArcLength(TendonPoint startPoint, TendonPoint endPoint, double r)
            {
                //Funkcja zwraca długość łuku o promieniu r, ograniczonego dwoma punktami.

                //Długość cięciwy łuku:
                r = Math.Abs(r);
                double c = Math.Sqrt(Math.Pow(endPoint.X - startPoint.X, 2) + Math.Pow(endPoint.Z - startPoint.Z, 2));
                //Wysokość łuku:
                double h = r - 0.5 * Math.Sqrt(4 * Math.Pow(r, 2) - Math.Pow(c, 2));
                //Długość łuku:
                return 2 * Math.Atan(0.5 * c / (r - h)) * r;
            }

            public double GetFrictionLosses(double x, double frictionCoefficient = 0.200, double unintentionalDisplacements = 0.006)
            {
                //frictionCoefficient - mi [1/rad], according to EC2
                //unintentionalDisplacements - k [rad/m], according to EC2

                if (Tendon is FullTendon)
                {
                    FullTendon tendon = Tendon as FullTendon;

                    double lengthAtA = GetTendonLength(x);
                    double angularSumAtA = GetTendonAngularSum(x);

                    double lengthAtE = GetTendonLength(GetTendonLength(tendon.TendonSegments.Last().EndPoint.X)) - GetTendonLength(x);
                    double angularSumAtE = GetTendonAngularSum(GetTendonLength(tendon.TendonSegments.Last().EndPoint.X)) - GetTendonAngularSum(x);

                    if (tendon.PrestressType == PrestressTypes.DoubleSided) //Naciąg dwustronny:
                    {
                        return Math.Min(tendon.PrestressForce * (1 - Math.Exp(-1 * frictionCoefficient * (angularSumAtA + unintentionalDisplacements * lengthAtA))), tendon.PrestressForce * (1 - Math.Exp(-1 * frictionCoefficient * (angularSumAtE + unintentionalDisplacements * lengthAtE))));
                    }
                    if (tendon.PrestressType == PrestressTypes.LeftSided) //Naciąg jednostronny z lewej (punkt A):
                    {
                        return tendon.PrestressForce * (1 - Math.Exp(-1 * frictionCoefficient * (angularSumAtA + unintentionalDisplacements * lengthAtA)));
                    }
                    if (tendon.PrestressType == PrestressTypes.RightSided) //Naciąg jednostronny z prawej (punkt E):
                    {
                        return tendon.PrestressForce * (1 - Math.Exp(-1 * frictionCoefficient * (angularSumAtE + unintentionalDisplacements * lengthAtE)));
                    }
                    return 0;
                }
                else if (Tendon is PartialTendon)
                {
                    PartialTendon tendon = Tendon as PartialTendon;

                    double lengthAtA = GetTendonLength(x);
                    double angularSumAtA = GetTendonAngularSum(x);

                    //Naciąg jednostronny z lewej (punkt A):
                    return tendon.PrestressForce * (1 - Math.Exp(-1 * frictionCoefficient * (angularSumAtA + unintentionalDisplacements * lengthAtA)));
                }
                return 0;
            }

            public double GetSlipLosses(double x, double slip = 6, double frictionCoefficient = 0.200, double unintentionalDisplacements = 0.006)
            {
                //frictionCoefficient - mi [1/rad], according to EC2
                //unintentionalDisplacements - k [rad/m], according to EC2

                //slip - wślizg szczęk, mm

                if (Tendon is FullTendon)
                {
                    FullTendon tendon = Tendon as FullTendon;

                    double averageRA = 0.5 * (tendon.TendonSegments[1].ArcRadius + tendon.TendonSegments[2].ArcRadius);
                    double averageRE = 0.5 * (tendon.TendonSegments[5].ArcRadius + tendon.TendonSegments[6].ArcRadius);

                    double x0A = 0;
                    if (tendon.PrestressType != PrestressTypes.RightSided) //Naciąg obustronny lub jednostronny z lewej:
                    {
                        x0A = averageRA / (frictionCoefficient * (1 + unintentionalDisplacements * averageRA)) * Math.Log(1 / (1 - Math.Sqrt(0.001 * slip * tendon.PrestressSteelElasticityModulus * tendon.TendonArea * frictionCoefficient * (1 + unintentionalDisplacements * averageRA) / (averageRA * tendon.PrestressForce))));
                        if (x0A < tendon.TendonSegments[2].StartPoint.X)
                        {
                            averageRA = tendon.TendonSegments[1].ArcRadius;
                            x0A = averageRA / (frictionCoefficient * (1 + unintentionalDisplacements * averageRA)) * Math.Log(1 / (1 - Math.Sqrt(0.001 * slip * tendon.PrestressSteelElasticityModulus * tendon.TendonArea * frictionCoefficient * (1 + unintentionalDisplacements * averageRA) / (averageRA * tendon.PrestressForce))));
                        }
                    }
                    double x0E = 0;
                    if (tendon.PrestressType != PrestressTypes.LeftSided) //Naciąg obustronny lub jednostronny z prawej:
                    {
                        x0E = averageRE / (frictionCoefficient * (1 + unintentionalDisplacements * averageRE)) * Math.Log(1 / (1 - Math.Sqrt(0.001 * slip * tendon.PrestressSteelElasticityModulus * tendon.TendonArea * frictionCoefficient * (1 + unintentionalDisplacements * averageRE) / (averageRE * tendon.PrestressForce))));
                        if (x0E < tendon.TendonSegments.Last().EndPoint.X - tendon.TendonSegments[6].StartPoint.X)
                        {
                            averageRE = tendon.TendonSegments[6].ArcRadius;
                            x0E = averageRE / (frictionCoefficient * (1 + unintentionalDisplacements * averageRE)) * Math.Log(1 / (1 - Math.Sqrt(0.001 * slip * tendon.PrestressSteelElasticityModulus * tendon.TendonArea * frictionCoefficient * (1 + unintentionalDisplacements * averageRE) / (averageRE * tendon.PrestressForce))));
                        }
                    }

                    double lossA = 0;
                    if ((x <= x0A) && (x0A > 0))
                    {
                        lossA = 2 * 0.001 * slip * (x0A - x) / Math.Pow(x0A, 2) * tendon.PrestressSteelElasticityModulus * tendon.TendonArea;
                    }
                    double lossE = 0;
                    if ((x >= tendon.TendonSegments.Last().EndPoint.X - x0E) && (x0E > 0))
                    {
                        lossE = 2 * 0.001 * slip * (x0E - (tendon.TendonSegments.Last().EndPoint.X - x)) / Math.Pow(x0E, 2) * tendon.PrestressSteelElasticityModulus * tendon.TendonArea;
                    }

                    return Math.Max(lossA, lossE);
                }
                else if (Tendon is PartialTendon)
                {
                    PartialTendon tendon = Tendon as PartialTendon;

                    double averageRA = 0.5 * (tendon.TendonSegments[1].ArcRadius + tendon.TendonSegments[2].ArcRadius);

                    double x0A = averageRA / (frictionCoefficient * (1 + unintentionalDisplacements * averageRA)) * Math.Log(1 / (1 - Math.Sqrt(0.001 * slip * tendon.PrestressSteelElasticityModulus * tendon.TendonArea * frictionCoefficient * (1 + unintentionalDisplacements * averageRA) / (averageRA * tendon.PrestressForce))));
                    if (x0A < tendon.TendonSegments[2].StartPoint.X)
                    {
                        averageRA = tendon.TendonSegments[1].ArcRadius;
                        x0A = averageRA / (frictionCoefficient * (1 + unintentionalDisplacements * averageRA)) * Math.Log(1 / (1 - Math.Sqrt(0.001 * slip * tendon.PrestressSteelElasticityModulus * tendon.TendonArea * frictionCoefficient * (1 + unintentionalDisplacements * averageRA) / (averageRA * tendon.PrestressForce))));
                    }

                    double lossA = 0;
                    if ((x <= x0A) && (x0A > 0))
                    {
                        lossA = 2 * 0.001 * slip * (x0A - x) / Math.Pow(x0A, 2) * tendon.PrestressSteelElasticityModulus * tendon.TendonArea;
                    }
                    return lossA;
                }
                return 0;
            }

            public double GetTendonForce(double x, double slip = 6, double frictionCoefficient = 0.200, double unintentionalDisplacements = 0.006)
            {
                if (IsFullTendon())
                {
                     return ((FullTendon) Tendon).PrestressForce - GetFrictionLosses(x, frictionCoefficient, unintentionalDisplacements) - GetSlipLosses(x, slip, frictionCoefficient, unintentionalDisplacements);
                }
                else if (IsPartialTendon())
                {
                     return ((PartialTendon) Tendon).PrestressForce - GetFrictionLosses(x, frictionCoefficient, unintentionalDisplacements) - GetSlipLosses(x, slip, frictionCoefficient, unintentionalDisplacements);
                }
                return 0;
            }
        }

        private static List<double> CalculateSections(object tendon, double dx = 0.5)
        {
            List<TendonSegment> tendonSegments = new List<TendonSegment>();
            if (tendon is FullTendon) tendonSegments = ((FullTendon) tendon).TendonSegments;
            else if (tendon is PartialTendon) tendonSegments = ((PartialTendon) tendon).TendonSegments;

            List<double> sections = new List<double>();
            foreach (TendonSegment tendonSegment in tendonSegments)
            {
                double segmentLength = tendonSegment.EndPoint.X - tendonSegment.StartPoint.X;
                int div = (int) Math.Ceiling(segmentLength / dx);
                if (tendon is FullTendon)
                {
                    if (tendonSegment == tendonSegments.First()) div = 1; //Tranfer length A
                    else if (tendonSegment == tendonSegments.Last()) div = 1; //Transfer length E
                    else if (Math.Abs(tendonSegment.ArcRadius) < 15.0 && tendonSegment.IsArc()) //Middle support radius
                    {
                        div = Math.Max((int) Math.Round((tendonSegment.EndPoint.X - tendonSegment.StartPoint.X) / 0.3), 2);
                    }
                }
                else if (tendon is PartialTendon)
                {
                    if (tendonSegment == tendonSegments.First()) div = 1; //Tranfer length A
                    else if (tendonSegment == tendonSegments.Last()) div = 3; //Transfer length D
                    else if (Math.Abs(tendonSegment.ArcRadius) < 15.0 && tendonSegment.IsArc()) //Middle support radius
                    {
                        div = Math.Max((int) Math.Round((tendonSegment.EndPoint.X - tendonSegment.StartPoint.X) / 0.3), 2);
                    }
                }

                for (int i = 0; i <= div; i++)
                {
                    double x = tendonSegment.StartPoint.X + i * segmentLength / div;
                    sections.Add(x);

                    if (tendonSegment.StartPoint.X + i * segmentLength / div >= tendonSegment.EndPoint.X)
                    {
                        if (tendonSegment != tendonSegments.Last()) break;
                    }
                }
            }
            return sections;
        }

        public static List<TendonPoint> IntersectArcByLine(TendonPoint pointR, double r, TendonPoint pointK, double k)
        {
            //Funkcja zwraca listę punktów przecięcia łuku o promieniu r i punkcie środkowym w pointR i z prostą.
            //Prosta definiowana jest współczynnikiem kierunkowym k oraz punktem pointK, przez który przechodzi.

            TendonPoint point1 = new TendonPoint();
            TendonPoint point2 = new TendonPoint();

            if ((k < double.MaxValue) && (k > double.MinValue))
            {
                //Równanie prostej: y = ak * x + bk:
                double ak = k;
                double bk = pointK.Z - ak * pointK.X;

                //Równanie okręgu: (X - pointR.X) ^ 2 + (Z - pointR.Z) ^ 2 = r ^ 2
                //Uzależnienie wartości Z od X: Z = ak * X + bk
                //Równanie kwadratowe wyprowadzone z równania okręgu: a * X ^ 2 + b * X + c = 0

                double a = 1 + Math.Pow(ak, 2);
                double b = 2 * ak * bk - 2 * ak * pointR.Z - 2 * pointR.X;
                double c = Math.Pow(pointR.X, 2) + Math.Pow(pointR.Z, 2) - 2 * bk * pointR.Z + Math.Pow(bk, 2) - Math.Pow(r, 2);

                double delta = Math.Pow(b, 2) - 4 * a * c;
                if (delta < 0) //Brak punktów przecięć
                {
                    throw new Exception("No intersection points returned by tangent function (negative delta).");
                }
                point1.X = (-1 * b - Math.Sqrt(delta)) / (2 * a);
                point2.X = (-1 * b + Math.Sqrt(delta)) / (2 * a);

                point1.Z = ak * point1.X + bk;
                point2.Z = ak * point2.X + bk;
            }
            else
            {
                point1.X = pointK.X;
                point2.X = pointK.X;

                point1.Z = -1 * Math.Sqrt(Math.Pow(r, 2) - Math.Pow(point1.X - pointR.X, 2)) + pointR.Z;
                point2.Z = Math.Sqrt(Math.Pow(r, 2) - Math.Pow(point2.X - pointR.X, 2)) + pointR.Z;
            }

            if (point1.Z < point2.Z)
            {
                return new List<TendonPoint>() { point1, point2 };
            }
            return new List<TendonPoint>() { point2, point1 };
        }

        public static List<TendonPoint> IntersectArcByArc(TendonPoint pointR1, double r1, TendonPoint pointR2, double r2)
        {
            //Funkcja zwraca listę punktów przecięcia łuków o punktach środkowych w pointR1 i pointR2.
            //Promienie łuków to odpowiednio: r1 i r2.

            if ((pointR1.X == pointR2.X) && (pointR1.Z == pointR2.Z)) //Okręgi współśrodkowe
            {
                throw new Exception("No intersection points returned by tangent function (cocentric arcs).");
            }

            //Równanie prostej prostopadłej do odcinka łączącego punkty środkowe okręgów: y = ak * x + bk
            double ak; double bk;

            //Znalezienie równania prostej prostopadłej do odcinka łączącego punkty środkowe okręgów; znalezienie punktu zaczepienia prostej prostopadłej między punktami środkowymi:
            TendonPoint pointK = new TendonPoint();
            if (pointR1.X == pointR2.X) //Pionowa prosta łącząca środki okręgów - pozioma prosta prostopadła
            {
                ak = 0;

                pointK.X = pointR1.X;
                pointK.Z = 0.5 * (Math.Pow(pointR1.Z, 2) - Math.Pow(r1, 2) - (Math.Pow(pointR2.Z, 2) - Math.Pow(r2, 2))) / (pointR1.Z - pointR2.Z);
            }
            else if (pointR1.Z == pointR2.Z) //Pozioma prosta łącząca środki okręgów - pionowa prosta prostopadła
            {
                ak = double.MinValue;

                pointK.X = 0.5 * (Math.Pow(pointR1.X, 2) - Math.Pow(r1, 2) - (Math.Pow(pointR2.X, 2) - Math.Pow(r2, 2))) / (pointR1.X - pointR2.X);
                pointK.Z = pointR1.Z;
            }
            else //Ukośna prosta łącząca środki okręgów - ukośna prosta prostopadła
            {
                double av = (pointR1.Z - pointR2.Z) / (pointR1.X - pointR2.X);
                double bv = pointR2.Z - av * pointR2.X;

                ak = -1 / av;
                bk = 0.5 * (Math.Pow(pointR1.X, 2) + Math.Pow(pointR1.Z, 2) - Math.Pow(r1, 2) - (Math.Pow(pointR2.X, 2) + Math.Pow(pointR2.Z, 2) - Math.Pow(r2, 2))) / (pointR1.Z - pointR2.Z);

                pointK.X = (bk - bv) / (av - ak);
                pointK.Z = av * pointK.X + bv;
            }
            return IntersectArcByLine(pointR1, r1, pointK, ak);
        }

        public static TendonPoint TangentPointByTangencyAndTangentArc(TendonPoint pointK, double k, TendonPoint pointT, double t, double r)
        {
            //Funkcja zwraca punkt styczności łuku przechodzącego przez punkt pointK, stycznego w tym punkcie do prostej o współczynniku kierunkowym k.
            //Łuk jest styczny w szukanym punkcie do łuku o promieniu r, przechodzącego przez punkt pointT, stycznego w tym punkcie do prostej o współczynniku kierunkowym t.

            //Punkt środkowy okręgu o promieniu r, przechodzącego przez punkt pointT i stycznego w tym punkcie do prostej o współczynniku kierunkowym t:
            TendonPoint pointR1 = CenterByTangencyAndRadius(pointT, t, r);
            //Punkt środkowy okręgu przechodzącego przez punkt pointK i stycznego w tym punkcie do prostej o współczynniku kierunkowym k oraz stycznego z łukiem o wyznaczonym wyżej punkcie środkowym pointC1:
            TendonPoint pointR2 = CenterByTangencyAndTangentArc(pointK, k, pointT, t, r);

            //Równanie prostej przechodzącej przez pointC1 i pointC2: y = av * x + bv
            double av;
            if (pointR1.X - pointR2.X != 0)
            {
                av = (pointR1.Z - pointR2.Z) / (pointR1.X - pointR2.X);
            }
            else
            {
                if (pointR1.Z - pointR2.Z > 0) av = double.MaxValue;
                else av = double.MinValue;
            }

            //Znalezienie punktów przecięcia jednego z łuków z prostą y = av * x + bv:
            List<TendonPoint> intersections = IntersectArcByLine(pointR1, r, pointR1, av);
            //Zwracany jest punkt leżący bliżej punktu środkowego drugiego z łuków - pointC2:
            if (Math.Sqrt(Math.Pow(intersections.First().X - pointR2.X, 2) + Math.Pow(intersections.First().Z - pointR2.Z, 2)) < Math.Sqrt(Math.Pow(intersections.Last().X - pointR2.X, 2) + Math.Pow(intersections.Last().Z - pointR2.Z, 2)))
            {
                return intersections.First();
            }
            return intersections.Last();
        }

        public static TendonPoint TangentPointByTangencyAndTangentLine(TendonPoint pointK, double k, double r, TendonPoint pointT)
        {
            //Funkcja zwraca punkt styczności łuku o promieniu r, przechodzącego przez punkt pointK, stycznego w tym punkcie do prostej o współczynniku kierunkowym k.
            //Łuk jest styczny w szukanym punkcie do prostej przechodzącej przez punkt pointT.

            //Punkt środkowy okręgu o promieniu r, przechodzącego przez punkt pointK i stycznego w tym punkcie do prostej o współczynniku kierunkowym k:
            TendonPoint pointR = CenterByTangencyAndRadius(pointK, k, r);
            //Punkt środkowy odcinka łączącego pointR z punktem pointT:
            TendonPoint pointM = new TendonPoint((pointR.X + pointT.X) / 2, (pointR.Z + pointT.Z) / 2);

            //Promień okręgu zaczepionego w punkcie middlePoint, sięgającego do pointT:
            double r2 = Math.Sqrt(Math.Pow(pointT.X - pointM.X, 2) + Math.Pow(pointT.Z - pointM.Z, 2));

            List<TendonPoint> interPoints = IntersectArcByArc(pointM, r2, pointR, r);
            if (Math.Sqrt(Math.Pow(interPoints.First().X - pointK.X, 2) + Math.Pow(interPoints.First().Z - pointK.Z, 2)) < Math.Sqrt(Math.Pow(interPoints.Last().X - pointK.X, 2) + Math.Pow(interPoints.Last().Z - pointK.Z, 2)))
            {
                return interPoints.First();
            }
            return interPoints.Last();
        }

        public static TendonPoint TangentPointByTangencyAndLength(TendonPoint pointK, double k, TendonPoint pointL, double l)
        {
            //Funkcja zwraca punkt styczności łuku przechodzącego przez punkt pointK, stycznego w tym punkcie do prostej o współczynniku kierunkowym k.
            //Łuk jest ponadto styczny do odcinka o długości l, przechodzącego przez punkt o współrzędnych pointL.

            return TangentPointByTangencyAndTangentLine(pointK, k, RadiusByTangencyAndLength(pointK, k, pointL, l), pointL);
        }

        public static TendonPoint CenterByTangencyAndRadius(TendonPoint pointK, double k, double r)
        {
            //Funkcja zwraca punkt środkowy łuku o promieniu r, przechodzącego przez punkt pointK, stycznego w tym punkcie do prostej o współczynniku kierunkowym k.

            TendonPoint pointR1 = new TendonPoint();
            TendonPoint pointR2 = new TendonPoint();

            if (k != 0)
            {
                //Równanie prostej o współczynniku kierunkowym k: z = k * x + b
                //Równanie prostej prostopadłej: z = av * x + bv = (-1 / k) * x + bv
                double av = -1 / k;
                double bv = pointK.Z - av * pointK.X;

                //Równanie okręgu: (pointK.X - pointR.X) ^ 2 + (pointK.Z - pointR.Z) ^ 2 = r ^ 2
                //Uzależnienie wartości pointR.Z od pointR.X: pointR.Z = av * pointR.X + bv
                //Równanie kwadratowe wyprowadzone z równania okręgu: a * pointR.X ^ 2 + b * pointR.X + c = 0

                double a = 1 + Math.Pow(av, 2);
                double b = 2 * av * bv - 2 * av * pointK.Z - 2 * pointK.X;
                double c = Math.Pow(pointK.X, 2) + Math.Pow(pointK.Z, 2) - 2 * bv * pointK.Z + Math.Pow(bv, 2) - Math.Pow(r, 2);

                double delta = Math.Pow(b, 2) - 4 * a * c;

                pointR1.X = (-1 * b - Math.Sqrt(delta)) / (2 * a);
                pointR2.X = (-1 * b + Math.Sqrt(delta)) / (2 * a);

                pointR1.Z = av * pointR1.X + bv;
                pointR2.Z = av * pointR2.X + bv;
            }
            else
            {
                pointR1.X = pointK.X;
                pointR2.X = pointK.X;

                pointR1.Z = pointK.Z - r;
                pointR2.Z = pointK.Z + r;
            }

            if (r < 0)
            {
                if (pointR1.Z < pointR2.Z) return pointR1;
                return pointR2;
            }
            else
            {
                if (pointR1.Z < pointR2.Z) return pointR2;
                return pointR1;
            }
        }

        public static TendonPoint CenterByTangencyAndPoint(TendonPoint pointK, double k, TendonPoint pointC)
        {
            //Funkcja zwraca punkt środkowy łuku przechodzącego przez punkty pointK i pointC.
            //Łuk jest ponadto styczny do prostej o współczynniku kierunkowym k, przechodzącej przez punkt pointK.

            TendonPoint pointR = new TendonPoint();

            //Punkt środkowy odcinka łączącego punkty pointK i pointC - środek łuku leży na prostej prostopadłej do odcinka, przechodzącej przez jego punkt środkowy:
            TendonPoint pointM = new TendonPoint((pointK.X + pointC.X) / 2, (pointK.Z + pointC.Z) / 2);

            //Równanie prostej przechodzącej przez punkt middlePoint, prostopadłej do odcinka między punktami pointK i pointC: z = ac * x + bc
            double ac = -1 * (pointK.X - pointC.X) / (pointK.Z - pointC.Z);
            double bc = pointM.Z - ac * pointM.X;

            if (k != 0)
            {
                //Równanie prostej o współczynniku kierunkowym k: z = k * x + b
                //Równanie prostej prostopadłej: z = av * x + bv = (-1 / k) * x + bv
                double av = -1 / k;
                double bv = pointK.Z - av * pointK.X;

                //Punkt wspólny prostych z = ac * x + bc oraz z = av * x + bv:
                pointR.X = (bv - bc) / (ac - av);
                pointR.Z = av * pointR.X + bv;
            }
            else
            {
                pointR.X = pointK.X;
                pointR.Z = ac * pointR.X + bc;
            }

            return pointR;
        }

        public static TendonPoint CenterByTangencyAndTangentArc(TendonPoint pointK, double k, TendonPoint pointT, double t, double r)
        {
            //Funkcja zwraca punkt środkowy łuku przechodzącego przez punkt pointK, stycznego w tym punkcie do prostej o współczynniku kierunkowym k.
            //Łuk jest ponadto styczny do łuku o promieniu r, przechodzącego przez punkt pointT, stycznego w tym punkcie do prostej o współczynniku kierunkowym t.

            double av;
            if (k != 0) av = -1 / k;
            else av = double.MinValue;

            //Punkt środkowy okręgu o promieniu r, przechodzącego przez punkt pointT i stycznego w tym punkcie do prostej o współczynniku kierunkowym t:
            TendonPoint pointC = CenterByTangencyAndRadius(pointT, t, r);
            //Znalezienie punktu na prostej prostopadłej do prostej o współczynniku kierunkowym k, przechodzącej przez punkt pointK, odległym od szukanego pointR o odległość R + r, gdzie R - promień szukanego łuku w centrum w punkcie pointR:   
            TendonPoint movedPointK;

            if (r > 0) movedPointK = IntersectArcByLine(pointK, r, pointK, av).Last();
            else movedPointK = IntersectArcByLine(pointK, r, pointK, av).First();

            return CenterByTangencyAndPoint(movedPointK, k, pointC);
        }

        public static TendonPoint CenterByTangencyAndLength(TendonPoint pointK, double k, TendonPoint pointL, double l)
        {
            //Funkcja zwraca punkt środkowy łuku przechodzącego przez punkt pointK, stycznego w tym punkcie do prostej o współczynniku kierunkowym k.
            //Łuk jest ponadto styczny do odcinka o długości l, przechodzącego przez punkt o współrzędnych pointL.

            return CenterByTangencyAndRadius(pointK, k, RadiusByTangencyAndLength(pointK, k, pointL, l));
        }

        public static double RadiusByTangencyAndTangentArc(TendonPoint pointK, double k, TendonPoint pointT, double t, double r)
        {
            //Funkcja zwraca promień łuku przechodzącego przez punkt pointK, stycznego w tym punkcie do prostej o współczynniku kierunkowym k.
            //Łuk jest ponadto styczny do łuku o promieniu r, przechodzącego przez punkt pointT, stycznego w tym punkcie do prostej o współczynniku kierunkowym t.

            //Punkt środkowy łuku stycznego w punkcie pointK do prostej o współczynniku kierunkowym k; stycznego ponadto do łuku o promieniu r, przechodzącego przez punkt pointT, stycznego w tym punkcie do prostej o współczynniku kierunkowym t:
            TendonPoint pointR = CenterByTangencyAndTangentArc(pointK, k, pointT, t, r);
            double radius = Math.Sqrt(Math.Pow(pointR.X - pointK.X, 2) + Math.Pow(pointR.Z - pointK.Z, 2));

            if (r < 0)
            {
                return radius;
            }
            return -1 * radius;
        }

        public static double RadiusByTangencyAndLength(TendonPoint pointK, double k, TendonPoint pointL, double l)
        {
            //Funkcja zwraca promień łuku przechodzącego przez punkt pointK, stycznego w tym punkcie do prostej o współczynniku kierunkowym k.
            //Łuk jest ponadto styczny do odcinka o długości l, przechodzącego przez punkt o współrzędnych pointL.

            double alpha = Math.Atan(k); //Kąt między prostą o współczynniku kierunkowym k a osią poziomą
            return (Math.Pow(l, 2) - Math.Pow(pointK.X - pointL.X, 2) - Math.Pow(pointK.Z - pointL.Z, 2)) / (2 * Math.Cos(alpha) * (pointK.Z - pointL.Z) - 2 * Math.Sin(alpha) * (pointK.X - pointL.X));
        }
    }
}