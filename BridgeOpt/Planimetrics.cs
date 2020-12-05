using System;
using System.Collections.Generic;
using System.Linq;

using MathNet.Numerics.LinearAlgebra;
using Point = System.Windows.Point;

namespace BridgeOpt
{
    public enum Sign
    {
        Negative = -1,
        Zero = 0,
        Positive = 1,
    }
    public enum Axle
    {
        X = 0,
        Y = 1,
    }

    public class Boundaries
    {
        public double Left;
        public double Right;
        public double Bottom;
        public double Top;

        public Boundaries(double leftX = 0.0, double rightX = 0.0, double bottomY = 0.0, double topY = 0.0)
        {
            Left = leftX; Right = rightX;
            Bottom = bottomY; Top = topY;
        }
    }
    public class StaticMoments
    {
        public double SX;
        public double SY;

        public StaticMoments(double sX = 0.0, double sY = 0.0)
        {
            SX = sX;
            SY = sY;
        }
    }
    public class MomentsOfInertia
    {
        public double IX;
        public double IY;
        public double IS; //Torsion constant

        public MomentsOfInertia(double iX = 0.0, double iY = 0.0, double iS = 0.0)
        {
            IX = iX;
            IY = iY;
            IS = iS;
        }
    }

    public class CentralTriangle
    {
        public double Area = 0.0;
        public Sign Sign;

        public List<Vector<double>> Vectors = new List<Vector<double>>();

        public Point GravityCenter = new Point();
        public StaticMoments StaticMoments;
        public MomentsOfInertia MomentsOfInertia;

        public CentralTriangle(Vector<double> a, Vector<double> b)
        {
            Vectors.Add(a);
            Vectors.Add(b);

            double normalLength = a.First() * b.Last() - a.Last() * b.First(); //Length of a vector product
            if (Math.Abs(normalLength) < double.Epsilon) Sign = Sign.Zero;
            else if (normalLength > 0) Sign = Sign.Positive;
            else Sign = Sign.Negative;

            if (Sign != Sign.Zero)
            {
                Area = 0.5 * Math.Abs(normalLength);
                Vector<double> gravityCenterVector = a.Add(b) / 3;

                GravityCenter.X = gravityCenterVector[0];
                GravityCenter.Y = gravityCenterVector[1];

                StaticMoments = new StaticMoments(GetStaticMoment(Axle.X), GetStaticMoment(Axle.Y));
                MomentsOfInertia = new MomentsOfInertia(GetMomentOfInertia(Axle.X), GetMomentOfInertia(Axle.Y));
            }
            else
            {
                Area = 0.0;
                GravityCenter.X = 0.0;
                GravityCenter.Y = 0.0;

                StaticMoments = new StaticMoments(0.0, 0.0);
                MomentsOfInertia = new MomentsOfInertia(0.0, 0.0);
            }
        }

        public CentralTriangle(double aX, double aY, double bX, double bY) : this(
            Vector<double>.Build.DenseOfArray(new double[] { aX, aY }),
            Vector<double>.Build.DenseOfArray(new double[] { bX, bY })
        ) { }

        public CentralTriangle(Point aPoint, Point bPoint) : this(
            Vector<double>.Build.DenseOfArray(new double[] { aPoint.X, aPoint.Y }),
            Vector<double>.Build.DenseOfArray(new double[] { bPoint.X, bPoint.Y })
)
        { }

        public double GetStaticMoment(Axle axle)
        {
            if (Sign == Sign.Zero) return 0.0;

            if (axle == Axle.X) return Area * GravityCenter.Y;
            return Area * GravityCenter.X;
        }

        public double GetMomentOfInertia(Axle axle)
        {
            if (Sign == Sign.Zero) return 0.0;

            int index;
            if (axle == Axle.X) index = 1;
            else index = 0;

            Vector<double> a = Vectors.First(); Vector<double> b = Vectors.Last();
            Vector<double> gravityCenterVector = Vector<double>.Build.DenseOfArray(new double[] { GravityCenter.X, GravityCenter.Y });

            if ((a[index] <= 0.0) && (b[index] <= 0.0))
            {
                a = -1 * a;
                b = -1 * b;
            }
            if (a[index] < b[index]) //Sorting, a[index] should be greater or equal to b[index]
            {
                Vector<double> replacing = a; a = b;
                b = replacing;
            }

            //Special cases: for axle = X - triangles with horizontal sides
            //               for axle = Y - triangles with vertical sides

            if (Math.Abs(b[index]) < double.Epsilon) //The horizontal (vertical) side going through the (0, 0) point:
            {
                //Equation A:

                //   (b * h ^ 3) / 36 + (1 / 2) * b * h * (h / 3) ^ 2 = 
                // = (b * h ^ 3) / 36 + (b * h ^ 3) / 18 =
                // = (b * h ^ 3) / 36 + 2 * (b * h ^ 3) / 36 = (b * h ^ 3) / 12:

                return b.L2Norm() * Math.Pow(a[index], 3) / 12;
            }
            if (Math.Abs(a[index] - b[index]) < double.Epsilon) //Otherwise - the horizontal (vertical) side above or below (on the left or on the right to) the (0, 0) point:
            {
                return a.Subtract(b).L2Norm() * Math.Pow(a[index], 3) / 36 + Area * Math.Pow(gravityCenterVector[index], 2);
            }

            double refAxle = 0;
            if (a[index] * b[index] > 0)
            {
                refAxle = b[index];
                a = a.Subtract(b); b = -1 * b;
            }
            Vector<double> v = a.Subtract(b);
            Vector<double> p = -b[index] / v[index] * v;
            double tBase = p.Add(b).L2Norm();

            double h1 = p[index];
            double h2 = a[index];

            double J = tBase * Math.Pow(h1, 3) / 12 + tBase * Math.Pow(h2, 3) / 12; //See: Equation A above
            return J + Area * Math.Pow(gravityCenterVector[index], 2) - Area * Math.Pow(Math.Abs(gravityCenterVector[index]) - refAxle, 2); //Mirror reflection of the gravity point due to inverstion for (a[index] <= 0.0) && (b[index] <= 0.0)
        }

        public class CrossSection
        {
            public double Area;
            public Point GravityCenter;
            public List<Point> Vertices;

            public Boundaries Boundaries;
            public StaticMoments StaticMoments;
            public MomentsOfInertia MomentsOfInertia;

            public List<CentralTriangle> Triangles = new List<CentralTriangle>();
            public double Height;
            public double Width;
            public double LeftCantileverOverhang;
            public double RightCantileverOverhang;

            public CrossSection(List<Point> points)
            {
                Vertices = points;

                Boundaries = new Boundaries();
                StaticMoments = new StaticMoments();
                MomentsOfInertia = new MomentsOfInertia();

                Boundaries.Left = points[0].X;
                Boundaries.Right = points[0].X;
                Boundaries.Bottom = points[0].Y;
                Boundaries.Top = points[0].Y;

                if (points.First() != points.Last()) points.Add(points[0]);

                for (int i = 0; i < points.Count() - 1; i++)
                {
                    Triangles.Add(new CentralTriangle(points[i], points[i + 1]));

                    Area += ((int) Triangles.Last().Sign) * Triangles.Last().Area;
                    StaticMoments.SX += ((int) Triangles.Last().Sign) * Triangles.Last().StaticMoments.SX;
                    StaticMoments.SY += ((int) Triangles.Last().Sign) * Triangles.Last().StaticMoments.SY;

                    if (points[i].X < Boundaries.Left) Boundaries.Left = points[i].X;
                    if (points[i].X > Boundaries.Right) Boundaries.Right = points[i].X;
                    if (points[i].Y < Boundaries.Bottom) Boundaries.Bottom = points[i].Y;
                    if (points[i].Y > Boundaries.Top) Boundaries.Top = points[i].Y;
                }
                if (Area < 0.0) throw new Exception("Invalid shape definition.");

                GravityCenter = new Point(StaticMoments.SY / Area, StaticMoments.SX / Area);

                Boundaries.Left = Boundaries.Left - GravityCenter.X;
                Boundaries.Right = Boundaries.Right - GravityCenter.X;
                Boundaries.Bottom = Boundaries.Bottom - GravityCenter.Y;
                Boundaries.Top = Boundaries.Top - GravityCenter.Y;

                foreach (CentralTriangle triangle in Triangles)
                {
                    MomentsOfInertia.IX += ((int) triangle.Sign) * (triangle.MomentsOfInertia.IX + triangle.Area * Math.Pow(GravityCenter.Y - triangle.GravityCenter.Y, 2) - triangle.Area * Math.Pow(triangle.GravityCenter.Y, 2));
                    MomentsOfInertia.IY += ((int) triangle.Sign) * (triangle.MomentsOfInertia.IY + triangle.Area * Math.Pow(GravityCenter.X - triangle.GravityCenter.X, 2) - triangle.Area * Math.Pow(triangle.GravityCenter.X, 2));
                }
            }

            public string ToScr(double multiplier = 1000)
            {
                string scr; scr = "_PLINE ";
                foreach (Point vertex in Vertices)
                {
                    scr = scr + (multiplier * vertex.X) + "," + (multiplier * vertex.Y) + " ";
                }
                return scr + " ";
            }
        }
    }
}