#define BENDINGCALC
#define SHEARCALC

#define GIRDERSREBARS //Output: final girders rebars arrangement

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

using MathNet.Numerics.LinearAlgebra;

using static BridgeOpt.LayoutDefinition;
using static BridgeOpt.CentralTriangle;

namespace BridgeOpt
{
    public enum Girder
    {
        LeftGirder = 0,
        RightGirder = 1
    }
    public enum LimitState
    {
        Exceeded = 0,
        Assured = 1
    }

    public class Strip
    {
        public double Area;
        public double Width;
        public double Height;
        public double Ordinate;
        public double Eccentricity;

        public Strip(double width, double height, double ordinate, double eccentricity)
        {
            Width = width;
            Height = height;
            Ordinate = ordinate;
            Eccentricity = eccentricity;

            Area = Width * Height;
        }
    }

    public class CrossSectionBoundaryFunction
    {
        //Function of z = A * x + B:
        public double A;
        public double B;

        public double MinX;
        public double MaxX;

        public double MinZ;
        public double MaxZ;

        public CrossSectionBoundaryFunction(Point point1, Point point2)
        {
            A = double.PositiveInfinity; B = point1.X;
            if (point1.X != point2.X)
            {
                A = (point2.Y - point1.Y) / (point2.X - point1.X);
                B = point1.Y - A * point1.X;
            }
            MinX = Math.Min(point1.X, point2.X);
            MaxX = Math.Max(point1.X, point2.X);

            MinZ = Math.Min(point1.Y, point2.Y);
            MaxZ = Math.Max(point1.Y, point2.Y);
        }

        public void Move(double dx, double dz)
        {
            MinX += dx; MinZ += dz;
            MaxX += dx; MaxZ += dz;
            if (A != double.PositiveInfinity) B += dz;
        }
    }

    public class StrainState
    {
        public double A;
        public double B;

        public StrainState(double a, double b)
        {
            A = a;
            B = b;
        }

        public void Update(double a, double b)
        {
            A = a;
            B = b;
        }

        public double StrainByCoordinates(double z)
        {
            if (double.IsNaN(A) || double.IsNaN(B)) return double.NaN;
            return A * z + B;
        }

        public double X(double crossSectionHeight)
        {
            if (A == 0) return crossSectionHeight;
            return crossSectionHeight + B / A;
        }
    }

    public class Concrete
    {
        public double CompressiveStrength;

        public double MeanTensileStrength;
        public double YieldStrain;
        public double UltimateStrain;
        public double ModulusOfElasticity;

        public double N;
        public double Gamma;

        public Concrete(double compressiveStrength, double meanTensileStrength, double yieldStrain, double ultimateStrain, double modulusOfElasticity, double n, double gammaC)
        {
            CompressiveStrength = compressiveStrength;

            MeanTensileStrength = meanTensileStrength;
            YieldStrain = yieldStrain;
            UltimateStrain = ultimateStrain;
            ModulusOfElasticity = modulusOfElasticity;

            N = n;
            Gamma = gammaC;
        }
        public Concrete(double compressiveStrength)
        {
            Gamma = 1.40;

            if (compressiveStrength == 12) { CompressiveStrength = compressiveStrength; MeanTensileStrength = 1.60; YieldStrain = 2.00; UltimateStrain = 3.50; ModulusOfElasticity = 27.0; N = 2.00; }
            else if (compressiveStrength == 16) { CompressiveStrength = compressiveStrength; MeanTensileStrength = 1.90; YieldStrain = 2.00; UltimateStrain = 3.50; ModulusOfElasticity = 29.0; N = 2.00; }
            else if (compressiveStrength == 20) { CompressiveStrength = compressiveStrength; MeanTensileStrength = 2.20; YieldStrain = 2.00; UltimateStrain = 3.50; ModulusOfElasticity = 30.0; N = 2.00; }
            else if (compressiveStrength == 25) { CompressiveStrength = compressiveStrength; MeanTensileStrength = 2.60; YieldStrain = 2.00; UltimateStrain = 3.50; ModulusOfElasticity = 31.0; N = 2.00; }
            else if (compressiveStrength == 30) { CompressiveStrength = compressiveStrength; MeanTensileStrength = 2.90; YieldStrain = 2.00; UltimateStrain = 3.50; ModulusOfElasticity = 32.0; N = 2.00; }
            else if (compressiveStrength == 35) { CompressiveStrength = compressiveStrength; MeanTensileStrength = 3.20; YieldStrain = 2.00; UltimateStrain = 3.50; ModulusOfElasticity = 34.0; N = 2.00; }
            else if (compressiveStrength == 40) { CompressiveStrength = compressiveStrength; MeanTensileStrength = 3.50; YieldStrain = 2.00; UltimateStrain = 3.50; ModulusOfElasticity = 35.0; N = 2.00; }
            else if (compressiveStrength == 45) { CompressiveStrength = compressiveStrength; MeanTensileStrength = 3.80; YieldStrain = 2.00; UltimateStrain = 3.50; ModulusOfElasticity = 36.0; N = 2.00; }
            else if (compressiveStrength == 50) { CompressiveStrength = compressiveStrength; MeanTensileStrength = 4.10; YieldStrain = 2.00; UltimateStrain = 3.50; ModulusOfElasticity = 37.0; N = 2.00; }
            else if (compressiveStrength == 55) { CompressiveStrength = compressiveStrength; MeanTensileStrength = 4.20; YieldStrain = 2.20; UltimateStrain = 3.10; ModulusOfElasticity = 38.0; N = 1.75; }
            else if (compressiveStrength == 60) { CompressiveStrength = compressiveStrength; MeanTensileStrength = 4.40; YieldStrain = 2.30; UltimateStrain = 2.90; ModulusOfElasticity = 39.0; N = 1.60; }
            else if (compressiveStrength == 70) { CompressiveStrength = compressiveStrength; MeanTensileStrength = 4.60; YieldStrain = 2.40; UltimateStrain = 2.70; ModulusOfElasticity = 41.0; N = 1.45; }
            else if (compressiveStrength == 80) { CompressiveStrength = compressiveStrength; MeanTensileStrength = 4.80; YieldStrain = 2.50; UltimateStrain = 2.60; ModulusOfElasticity = 42.0; N = 1.40; }
            else if (compressiveStrength == 70) { CompressiveStrength = compressiveStrength; MeanTensileStrength = 5.00; YieldStrain = 2.60; UltimateStrain = 2.60; ModulusOfElasticity = 44.0; N = 1.40; }
            else
            {
                CompressiveStrength = compressiveStrength;
                if (compressiveStrength <= 50) MeanTensileStrength = 0.3 * Math.Pow(compressiveStrength, 2 / 3);
                else MeanTensileStrength = 2.12 * Math.Log(1 + 0.1 * (compressiveStrength + 8));

                if (compressiveStrength <= 50) YieldStrain = 2.00;
                else YieldStrain = 2.0 + 0.085 * Math.Pow(compressiveStrength - 50, 0.53);

                if (compressiveStrength <= 50) UltimateStrain = 3.50;
                else UltimateStrain = 2.6 + 35 * Math.Pow(0.01 * (90 - compressiveStrength), 4);

                ModulusOfElasticity = 22 * Math.Pow(0.1 * (compressiveStrength + 8), 0.3);

                if (compressiveStrength <= 50) N = 2.00;
                else N = 1.4 + 23.4 * Math.Pow(0.01 * (90 - compressiveStrength), 4);
            }
        }

        public double StressByStrain(double e, double delta = 0.0)
        {
            if (double.IsNaN(e)) return double.NaN;

            double designCompressiveStrength = CompressiveStrength / Gamma;
            if (e < 0) return 0.0;
            if (e < YieldStrain) return (designCompressiveStrength - delta) * (1 - Math.Pow(1 - e / YieldStrain, N));
            if (e < UltimateStrain) return (designCompressiveStrength - delta) + delta * e / (UltimateStrain - YieldStrain);
            return designCompressiveStrength * (1 + delta * e / UltimateStrain);
        }
    }

    public class ReinforcingSteel
    {
        public double ModulusOfElasticity;
        public double YieldStrength;
        public double UltimateStrain;

        public double Gamma;

        public ReinforcingSteel(double yieldStrength, double ultimateStrain, double modulusOfElasticity, double gammaS)
        {
            YieldStrength = yieldStrength;
            UltimateStrain = ultimateStrain;
            ModulusOfElasticity = modulusOfElasticity;

            Gamma = gammaS;
        }
        public ReinforcingSteel() : this(500.0, 10.0, 200.0, 1.15) //Default characteristics of the reinforcing steel
        { }

        public double StressByStrain(double e, double delta = 0.0)
        {
            if (double.IsNaN(e)) return double.NaN;

            double designYieldStrength = YieldStrength / Gamma;
            double designYieldStrain = designYieldStrength / ModulusOfElasticity;

            if (e < -1 * UltimateStrain) return -1 * designYieldStrength * (1 - delta * e / UltimateStrain);
            if (e < -1 * designYieldStrain) return -1 * (designYieldStrength - delta) + delta * e / (UltimateStrain - designYieldStrain);
            if (e <= designYieldStrain) return designYieldStrength * (e / designYieldStrain);
            if (e <= UltimateStrain) return (designYieldStrength - delta) + delta * e / (UltimateStrain - designYieldStrain);
            return designYieldStrength * (1 + delta * e / UltimateStrain);
        }
    }
    public class Reinforcement
    {
        public double NumberOfRebars = 0;
        public double RebarsDiameter = 0;

        public double Area;
        public double Ordinate;
        public ReinforcingSteel ReinforcingSteel;

        public Reinforcement(double numberOfRebars, double rebarsDiameter, double z, ReinforcingSteel reinforcingSteel)
        {
            NumberOfRebars = numberOfRebars;
            RebarsDiameter = rebarsDiameter;

            Area = numberOfRebars * (0.25 * Math.PI * Math.Pow(rebarsDiameter, 2)) * Math.Pow(0.001, 2);
            Ordinate = z;
            ReinforcingSteel = reinforcingSteel;
        }
        public Reinforcement(double area, double z, ReinforcingSteel reinforcingSteel)
        {
            Area = area;
            Ordinate = z;
            ReinforcingSteel = reinforcingSteel;
        }
        public Reinforcement(double z, ReinforcingSteel reinforcingSteel) : this(0.0, z, reinforcingSteel) { }

        public void Rearrange(double numberOfRebars, double rebarsDiameter, bool isBottom = true)
        {
            if (isBottom) Ordinate = Ordinate - 0.5 * RebarsDiameter;
            else Ordinate = Ordinate + 0.5 * RebarsDiameter;

            NumberOfRebars = numberOfRebars;
            RebarsDiameter = rebarsDiameter;
            if (isBottom) Ordinate = Ordinate + 0.5 * rebarsDiameter;
            else Ordinate = Ordinate - 0.5 * rebarsDiameter;
            Area = numberOfRebars * (0.25 * Math.PI * Math.Pow(rebarsDiameter, 2)) * Math.Pow(0.001, 2);
        }
    }

    public class PrestressingSteel
    {
        public double ModulusOfElasticity;
        public double YieldStrength;
        public double UltimateStrain;

        public double Gamma;

        public PrestressingSteel(double yieldStrength, double ultimateStrain, double modulusOfElasticity, double gammaP)
        {
            YieldStrength = yieldStrength;
            UltimateStrain = ultimateStrain;
            ModulusOfElasticity = modulusOfElasticity;

            Gamma = gammaP;
        }
        public PrestressingSteel() : this(1680.0, 15.0, 195.0, 1.15) //Default characteristics of the prestressing steel
        { }

        public double StressByStrain(double e, double initialStress, double delta = 0.0)
        {
            if (double.IsNaN(e)) return double.NaN;

            double designYieldStrength = YieldStrength / Gamma;
            double designYieldStrain = designYieldStrength / ModulusOfElasticity;

            double initialStrain = -1 * initialStress / ModulusOfElasticity;
            e += initialStrain;

            if (e < -1 * UltimateStrain) return -1 * designYieldStrength * (1 - delta * e / UltimateStrain);
            if (e < -1 * designYieldStrain) return -1 * (designYieldStrength - delta) + delta * e / (UltimateStrain - designYieldStrain);
            if (e <= designYieldStrain) return designYieldStrength * (e / designYieldStrain);
            if (e <= UltimateStrain) return (designYieldStrength - delta) + delta * e / (UltimateStrain - designYieldStrain);
            return designYieldStrength * (1 + delta * e / UltimateStrain);
        }
        public double StressByStrain(double e, double initialForce, double area, double delta = 0.0)
        {
            return StressByStrain(e, 0.001 * initialForce / area, delta);
        }
    }
    public class Prestressing
    {
        public double Area;
        public double Ordinate;
        public double InitialStress;
        public PrestressingSteel PrestressingSteel;

        public Prestressing(double area, double z, double initialStress, PrestressingSteel prestressingSteel)
        {
            Area = area;
            Ordinate = z;
            InitialStress = initialStress;
            PrestressingSteel = prestressingSteel;
        }
    }

    public class DiscreteCrossSection
    {
        public CrossSection CrossSection;
        public List<Strip> Strips = new List<Strip>();
        public Concrete Concrete;

        public List<Reinforcement> ReinforcementLayers = new List<Reinforcement>();
        public List<Prestressing> PrestressingLayers = new List<Prestressing>();

        public DiscreteCrossSection(CrossSection crossSection, Concrete concrete, double maxStripHeight = 0.010)
        {
            List<CrossSectionBoundaryFunction> edges = new List<CrossSectionBoundaryFunction>();
            double minZ = crossSection.Vertices[0].Y;
            for (int i = 0; i < crossSection.Vertices.Count() - 1; i++)
            {
                if (Math.Round(crossSection.Vertices[i].Y - crossSection.Vertices[i + 1].Y, 8) != 0)
                    edges.Add(new CrossSectionBoundaryFunction(crossSection.Vertices[i], crossSection.Vertices[i + 1]));

                if (crossSection.Vertices[i].Y < minZ) minZ = crossSection.Vertices[i].Y;
            }
            foreach (CrossSectionBoundaryFunction edge in edges) edge.Move(0, -1 * minZ);

            int numberOfStrips = (int) Math.Ceiling((crossSection.Boundaries.Top - crossSection.Boundaries.Bottom) / maxStripHeight);
            double stripHeight = (crossSection.Boundaries.Top - crossSection.Boundaries.Bottom) / numberOfStrips;
            for (int i = 0; i < numberOfStrips; i++)
            {
                double ordinate = i * stripHeight + 0.5 * stripHeight;
                Strips.Add(new Strip(GetStripWidth(ordinate, edges), stripHeight, ordinate, ordinate + crossSection.Boundaries.Bottom));
            }
            CrossSection = crossSection;
            Concrete = concrete;
        }
        public DiscreteCrossSection(double height, double width, Concrete concrete, double maxStripHeight = 0.010) : this //Rectangular cross-section
            (new CrossSection(new List<Point> { new Point(-0.5 * width, 0.5 * height), new Point(-0.5 * width, -0.5 * height), new Point(0.5 * width, -0.5 * height), new Point(0.5 * width, 0.5 * height) }), concrete, maxStripHeight)
        {
            CrossSection.Height = height;
            CrossSection.Width = width;
            CrossSection.LeftCantileverOverhang = 0.0;
            CrossSection.RightCantileverOverhang = 0.0;
        }

        private double GetStripWidth(double z, List<CrossSectionBoundaryFunction> edges)
        {
            List<double> intersections = new List<double>();
            foreach (CrossSectionBoundaryFunction edge in edges)
            {
                if ((z >= edge.MinZ) && (z <= edge.MaxZ))
                {
                    double? x = GetIntersectionWithEdge(z, edge);
                    if (x != null) intersections.Add((double) x);
                }
            }

            double width = 0.0;
            if (intersections.Count() > 0)
            {
                intersections = intersections.OrderBy(x => x).ToList();
                for (int i = 0; i < intersections.Count(); i += 2) width += intersections[i + 1] - intersections[i];
            }
            return width;
        }

        private double? GetIntersectionWithEdge(double z, CrossSectionBoundaryFunction edge)
        {
            if (edge.A == 0) return null;

            if (edge.A == double.PositiveInfinity) return edge.B;
            return (z - edge.B) / edge.A;
        }

        public double GetBearingCapacity(double nEd, double dx = 0.100, bool possitive = true)
        {
            double compressionZ, tensionZ;
            double compressionE, tensionE;

            tensionE = 0.0;
            if (possitive)
            {
                compressionZ = CrossSection.Boundaries.Top - CrossSection.Boundaries.Bottom;
                compressionE = Concrete.UltimateStrain;
            }
            else
            {
                compressionZ = 0.0;
                compressionE = Concrete.UltimateStrain;
            }
            tensionZ = GetRearmostReinforcementLayer(possitive).Ordinate;
            tensionE = -1 * GetRearmostReinforcementLayer(possitive).ReinforcingSteel.UltimateStrain;
            double x = compressionE / (compressionE - tensionE) * (compressionZ - tensionZ);

            double nRd;
            double mRd;

            int multiplier = 0; double prec = 1.0;
            do //Iterations:
            {
                double a = (tensionE - compressionE) / (tensionZ - compressionZ);
                double b = compressionE - a * compressionZ;
                StrainState strainState = new StrainState(a, b);

                nRd = 0.0;
                mRd = 0.0;
                foreach (Strip strip in Strips)
                {
                    nRd += 1000 * Concrete.StressByStrain(strainState.StrainByCoordinates(strip.Ordinate)) * strip.Area;
                    mRd += 1000 * Concrete.StressByStrain(strainState.StrainByCoordinates(strip.Ordinate)) * strip.Area * strip.Eccentricity;
                }
                foreach (Reinforcement reinforcementLayer in ReinforcementLayers)
                {
                    nRd += 1000 * reinforcementLayer.ReinforcingSteel.StressByStrain(strainState.StrainByCoordinates(reinforcementLayer.Ordinate)) * reinforcementLayer.Area;
                    mRd += 1000 * reinforcementLayer.ReinforcingSteel.StressByStrain(strainState.StrainByCoordinates(reinforcementLayer.Ordinate)) * reinforcementLayer.Area * (CrossSection.Boundaries.Bottom + reinforcementLayer.Ordinate);
                }
                foreach (Prestressing prestressingLayer in PrestressingLayers)
                {
                    nRd += 1000 * prestressingLayer.PrestressingSteel.StressByStrain(strainState.StrainByCoordinates(prestressingLayer.Ordinate), prestressingLayer.InitialStress) * prestressingLayer.Area;
                    mRd += 1000 * prestressingLayer.PrestressingSteel.StressByStrain(strainState.StrainByCoordinates(prestressingLayer.Ordinate), prestressingLayer.InitialStress) * prestressingLayer.Area * (CrossSection.Boundaries.Bottom + prestressingLayer.Ordinate);
                }

                if (Math.Abs(nEd - nRd) > prec)
                {
                    if (multiplier == 0)
                    {
                        if (nEd > nRd) multiplier = 1;
                        else multiplier = -1;
                    }

                    if (multiplier == 1) //Limit state determined by compression zone (compression zone to be increased):
                    {
                        if (nEd < nRd)
                        {
                            x = x - (possitive ? multiplier : -1 * multiplier) * dx;
                            dx = 0.5 * dx;
                        }
                        x = x + (possitive ? multiplier : -1 * multiplier) * dx;
                        compressionE = Concrete.UltimateStrain;
                        tensionE = -1 * compressionE * (compressionZ - tensionZ) / x + compressionE;
                    }
                    else //Limit state determined by reinforcement in tension zone (compression zone to be decreased):
                    {
                        if (nEd > nRd)
                        {
                            x = x - (possitive ? multiplier : -1 * multiplier) * dx;
                            dx = 0.5 * dx;
                        }
                        x = x + (possitive ? multiplier : -1 * multiplier) * dx;
                        tensionE = -1 * GetRearmostReinforcementLayer(possitive).ReinforcingSteel.UltimateStrain;
                        compressionE = -1 * tensionE * x / (compressionZ - tensionZ - x);
                    }
                }
            }
            while ((Math.Abs(nEd - nRd) > prec) && (dx > 0.00001));
            return mRd;
        }

        public StrainState GetStrainState(double nEd, double mEd, double dev = 0.01, double delta = 0.50)
        {
            double dN = 0.0;
            double dM = 0.0;
            StrainState strainState = new StrainState(0.0, 0.0);

            foreach (Strip strip in Strips)
            {
                dN += 1000 * Concrete.StressByStrain(strainState.StrainByCoordinates(strip.Ordinate), 0.1 * delta) * strip.Area;
                dM += 1000 * Concrete.StressByStrain(strainState.StrainByCoordinates(strip.Ordinate), 0.1 * delta) * strip.Area * strip.Eccentricity;
            }
            foreach (Reinforcement reinforcementLayer in ReinforcementLayers)
            {
                dN += 1000 * reinforcementLayer.ReinforcingSteel.StressByStrain(strainState.StrainByCoordinates(reinforcementLayer.Ordinate), delta) * reinforcementLayer.Area;
                dM += 1000 * reinforcementLayer.ReinforcingSteel.StressByStrain(strainState.StrainByCoordinates(reinforcementLayer.Ordinate), delta) * reinforcementLayer.Area * (CrossSection.Boundaries.Bottom + reinforcementLayer.Ordinate);
            }
            foreach (Prestressing prestressingLayer in PrestressingLayers)
            {
                dN += 1000 * prestressingLayer.PrestressingSteel.StressByStrain(strainState.StrainByCoordinates(prestressingLayer.Ordinate), prestressingLayer.InitialStress, delta) * prestressingLayer.Area;
                dM += 1000 * prestressingLayer.PrestressingSteel.StressByStrain(strainState.StrainByCoordinates(prestressingLayer.Ordinate), prestressingLayer.InitialStress, delta) * prestressingLayer.Area * (CrossSection.Boundaries.Bottom + prestressingLayer.Ordinate);
            }

            double prec = 1.0; int count = 0;
            while ((Math.Abs(nEd - dN) > prec) || (Math.Abs(mEd - dM) > prec))
            {
                double dNA = 0.0, dMA = 0.0;
                double dNB = 0.0, dMB = 0.0;

                foreach (Strip strip in Strips)
                {
                    //Partial derivative for A:
                    dNA += 1000 * Concrete.StressByStrain((strainState.A + dev) * strip.Ordinate + strainState.B, 0.1 * delta) * strip.Area;
                    dMA += 1000 * Concrete.StressByStrain((strainState.A + dev) * strip.Ordinate + strainState.B, 0.1 * delta) * strip.Area * strip.Eccentricity;
                    //Partial derivative for B:
                    dNB += 1000 * Concrete.StressByStrain(strainState.A * strip.Ordinate + (strainState.B + dev), 0.1 * delta) * strip.Area;
                    dMB += 1000 * Concrete.StressByStrain(strainState.A * strip.Ordinate + (strainState.B + dev), 0.1 * delta) * strip.Area * strip.Eccentricity;
                }
                foreach (Reinforcement reinforcementLayer in ReinforcementLayers)
                {
                    //Partial derivative for A:
                    dNA += 1000 * reinforcementLayer.ReinforcingSteel.StressByStrain((strainState.A + dev) * reinforcementLayer.Ordinate + strainState.B, delta) * reinforcementLayer.Area;
                    dMA += 1000 * reinforcementLayer.ReinforcingSteel.StressByStrain((strainState.A + dev) * reinforcementLayer.Ordinate + strainState.B, delta) * reinforcementLayer.Area * (CrossSection.Boundaries.Bottom + reinforcementLayer.Ordinate);
                    //Partial derivative for B:
                    dNB += 1000 * reinforcementLayer.ReinforcingSteel.StressByStrain(strainState.A * reinforcementLayer.Ordinate + (strainState.B + dev), delta) * reinforcementLayer.Area;
                    dMB += 1000 * reinforcementLayer.ReinforcingSteel.StressByStrain(strainState.A * reinforcementLayer.Ordinate + (strainState.B + dev), delta) * reinforcementLayer.Area * (CrossSection.Boundaries.Bottom + reinforcementLayer.Ordinate);
                }
                foreach (Prestressing prestressingLayer in PrestressingLayers)
                {
                    //Partial derivative for A:
                    dNA += 1000 * prestressingLayer.PrestressingSteel.StressByStrain((strainState.A + dev) * prestressingLayer.Ordinate + strainState.B, prestressingLayer.InitialStress, delta) * prestressingLayer.Area;
                    dMA += 1000 * prestressingLayer.PrestressingSteel.StressByStrain((strainState.A + dev) * prestressingLayer.Ordinate + strainState.B, prestressingLayer.InitialStress, delta) * prestressingLayer.Area * (CrossSection.Boundaries.Bottom + prestressingLayer.Ordinate);
                    //Partial derivative for B:
                    dNB += 1000 * prestressingLayer.PrestressingSteel.StressByStrain(strainState.A * prestressingLayer.Ordinate + (strainState.B + dev), prestressingLayer.InitialStress, delta) * prestressingLayer.Area;
                    dMB += 1000 * prestressingLayer.PrestressingSteel.StressByStrain(strainState.A * prestressingLayer.Ordinate + (strainState.B + dev), prestressingLayer.InitialStress, delta) * prestressingLayer.Area * (CrossSection.Boundaries.Bottom + prestressingLayer.Ordinate);
                }

                //Matrix<double> partialDifferences = Matrix<double>.Build.DenseOfColumnArrays(new double[] { dMA - dM, dMB - dM }, new double[] { dNA - dN, dNB - dN });
                Matrix<double> partialDifferences = Matrix<double>.Build.DenseOfColumnArrays(new double[] { dMA - dM, dNA - dN }, new double[] { dMB - dM, dNB - dN });
                Vector<double> mainDifferences = Vector<double>.Build.DenseOfArray(new double[] { mEd - dM, nEd - dN });

                Vector<double> results = partialDifferences.Inverse().Multiply(mainDifferences);
                if (double.IsNaN(results[0]) || double.IsNaN(results[1])) { results[0] = 0; results[1] = 0; }
                strainState.Update(strainState.A + dev * results[0], strainState.B + dev * results[1]);

                dN = 0.0;
                dM = 0.0;
                foreach (Strip strip in Strips)
                {
                    dN += 1000 * Concrete.StressByStrain(strainState.StrainByCoordinates(strip.Ordinate), 0.1 * delta) * strip.Area;
                    dM += 1000 * Concrete.StressByStrain(strainState.StrainByCoordinates(strip.Ordinate), 0.1 * delta) * strip.Area * strip.Eccentricity;
                }
                foreach (Reinforcement reinforcementLayer in ReinforcementLayers)
                {
                    dN += 1000 * reinforcementLayer.ReinforcingSteel.StressByStrain(strainState.StrainByCoordinates(reinforcementLayer.Ordinate), delta) * reinforcementLayer.Area;
                    dM += 1000 * reinforcementLayer.ReinforcingSteel.StressByStrain(strainState.StrainByCoordinates(reinforcementLayer.Ordinate), delta) * reinforcementLayer.Area * (CrossSection.Boundaries.Bottom + reinforcementLayer.Ordinate);
                }
                foreach (Prestressing prestressingLayer in PrestressingLayers)
                {
                    dN += 1000 * prestressingLayer.PrestressingSteel.StressByStrain(strainState.StrainByCoordinates(prestressingLayer.Ordinate), prestressingLayer.InitialStress, delta) * prestressingLayer.Area;
                    dM += 1000 * prestressingLayer.PrestressingSteel.StressByStrain(strainState.StrainByCoordinates(prestressingLayer.Ordinate), prestressingLayer.InitialStress, delta) * prestressingLayer.Area * (CrossSection.Boundaries.Bottom + prestressingLayer.Ordinate);
                }

                count++;
                if (count > 100)
                {
                    strainState.Update(double.NaN, double.NaN);
                    break;
                }
            }
            return strainState;
        }

        private Reinforcement GetRearmostReinforcementLayer(bool possitive = true)
        {
            if (ReinforcementLayers.Count == 0) return null;
            if (possitive)
            {
                double z = ReinforcementLayers.First().Ordinate;
                Reinforcement rearmostReinforcementLayer = ReinforcementLayers.First();

                foreach (Reinforcement reinforcementLayer in ReinforcementLayers)
                {
                    if (reinforcementLayer.Ordinate < z)
                    {
                        rearmostReinforcementLayer = reinforcementLayer;
                        z = rearmostReinforcementLayer.Ordinate;
                    }
                }
                return rearmostReinforcementLayer;
            }
            else
            {
                double z = ReinforcementLayers.First().Ordinate;
                Reinforcement rearmostReinforcementLayer = ReinforcementLayers.First();

                foreach (Reinforcement reinforcementLayer in ReinforcementLayers)
                {
                    if (reinforcementLayer.Ordinate > z)
                    {
                        rearmostReinforcementLayer = reinforcementLayer;
                        z = rearmostReinforcementLayer.Ordinate;
                    }
                }
                return rearmostReinforcementLayer;
            }
        }

        public double GetMinimumReinforcementArea(double b, double d, double reinforcementYieldStrength = 500.0)
        {
            double minimumReinforcementArea = 0.26 * Concrete.MeanTensileStrength / reinforcementYieldStrength * b * d;
            return Math.Max(minimumReinforcementArea, 0.0013 * b * d);
        }
    }

    public class UltimateLimitState
    {
        public DiscreteCrossSection DiscreteCrossSection;
        public StrainState StrainState;
        public LimitState State;

        public double N;
        public double M;

        public double TopConcreteStress;
        public double BottomConcreteStress;
        public double TopReinforcementStress;
        public double BottomReinforcementStress;

        public UltimateLimitState(double nEd, double mEd, DiscreteCrossSection discreteCrossSection)
        {
            N = nEd; M = mEd;
            DiscreteCrossSection = discreteCrossSection;

            StrainState = DiscreteCrossSection.GetStrainState(nEd, mEd);
            State = CheckLimitState();
        }

        private LimitState CheckLimitState()
        {
            if (double.IsNaN(StrainState.A) || double.IsNaN(StrainState.B)) return LimitState.Exceeded;
            bool exceeded = false;

            //Concrete, top fibers:
            TopConcreteStress = DiscreteCrossSection.Concrete.StressByStrain(StrainState.StrainByCoordinates(DiscreteCrossSection.CrossSection.Height), 0.5);
            if (TopConcreteStress > DiscreteCrossSection.Concrete.CompressiveStrength / DiscreteCrossSection.Concrete.Gamma) exceeded = true;

            //Concrete, bottom fibers:
            BottomConcreteStress = DiscreteCrossSection.Concrete.StressByStrain(StrainState.StrainByCoordinates(0.0), 0.5);
            if (BottomConcreteStress > DiscreteCrossSection.Concrete.CompressiveStrength / DiscreteCrossSection.Concrete.Gamma) exceeded = true;

            //Stress in reinforcement:
            TopReinforcementStress = double.PositiveInfinity; BottomReinforcementStress = double.PositiveInfinity;
            foreach (Reinforcement reinforcementLayer in DiscreteCrossSection.ReinforcementLayers)
            {
                double stress = reinforcementLayer.ReinforcingSteel.StressByStrain(StrainState.StrainByCoordinates(reinforcementLayer.Ordinate), 0.5);
                if (double.IsNaN(stress))
                {
                    TopReinforcementStress = double.NaN;
                    BottomReinforcementStress = double.NaN;
                    return LimitState.Exceeded;
                }

                if (reinforcementLayer.Ordinate < DiscreteCrossSection.CrossSection.Height - reinforcementLayer.Ordinate)
                {
                    if (stress < BottomReinforcementStress) BottomReinforcementStress = stress;
                }
                else
                {
                    if (stress < TopReinforcementStress) TopReinforcementStress = stress;
                }
                if (Math.Min(BottomConcreteStress, TopReinforcementStress) < -1 * reinforcementLayer.ReinforcingSteel.YieldStrength / reinforcementLayer.ReinforcingSteel.Gamma) exceeded = true;
            }

            if (exceeded) return LimitState.Exceeded;
            return LimitState.Assured;
        }
    }

    public class ServiceabilityLimitState
    {
        public DiscreteCrossSection DiscreteCrossSection;
        public StrainState StrainState;
        public LimitState State;

        public double N;
        public double M;

        public double ReinforcementStress;
        public double CrackWidth;

        public ServiceabilityLimitState(double nEk, double mEk, DiscreteCrossSection discreteCrossSection, double cover)
        {
            N = nEk; M = mEk;
            DiscreteCrossSection = discreteCrossSection;
            StrainState = DiscreteCrossSection.GetStrainState(nEk, mEk);

            double crossSectionWidth = discreteCrossSection.CrossSection.Width, crossSectionHeight = discreteCrossSection.CrossSection.Height;
            double kt = 0.4, k1 = 0.8, k2 = 0.5, k3 = 3.4, k4 = 0.425; //EN 1992-1-1, p. 7.3.4.(3)

            List<Reinforcement> tensionReinforcement = new List<Reinforcement>();
            ReinforcementStress = 0.0;

            double reinforcementModulusOfElasticity = 200.0; int index = 0;
            foreach (Reinforcement reinforcementLayer in discreteCrossSection.ReinforcementLayers)
            {
                if (discreteCrossSection.ReinforcementLayers.IndexOf(reinforcementLayer) == 0) continue;
                double stress = reinforcementLayer.ReinforcingSteel.StressByStrain(StrainState.StrainByCoordinates(reinforcementLayer.Ordinate), 0.5);
                if (stress < 0.0)
                {
                    tensionReinforcement.Add(reinforcementLayer);
                    if (stress < ReinforcementStress)
                    {
                        ReinforcementStress = stress;
                        reinforcementModulusOfElasticity = reinforcementLayer.ReinforcingSteel.ModulusOfElasticity;
                    }
                }
            }

            if (ReinforcementStress == 0.0) CrackWidth = 0.000;
            else
            {
                ReinforcementStress = Math.Abs(ReinforcementStress);

                double d = 0.0, area = 0.0, fieq = 0.0;
                foreach (Reinforcement reinforcementLayer in tensionReinforcement)
                {
                    d += Math.Max(reinforcementLayer.Ordinate, crossSectionHeight - reinforcementLayer.Ordinate) * reinforcementLayer.Area;

                    area += reinforcementLayer.Area;
                    fieq += 0.25 * Math.PI * reinforcementLayer.NumberOfRebars * (0.001 * reinforcementLayer.RebarsDiameter);
                }
                d = d / area;
                fieq = 1000 * area / fieq;

                double x = StrainState.X(crossSectionHeight);
                double aceff = Math.Min(2.5 * (crossSectionHeight - d), (crossSectionHeight - x) / 3) * crossSectionWidth;
                double ppeff = area / aceff;

                double ae = reinforcementModulusOfElasticity / discreteCrossSection.Concrete.ModulusOfElasticity;
                //Difference between mean strains: in the reinforcement and concrete:
                double dEpsilon = Math.Max((ReinforcementStress - kt * discreteCrossSection.Concrete.MeanTensileStrength / ppeff * (1 - ae * ppeff)) / (1000 * reinforcementModulusOfElasticity), 0.6 * ReinforcementStress / (1000 * reinforcementModulusOfElasticity));
                //Maximum crack spacing:
                double srmax = k3 * cover + k1 * k2 * k4 * fieq / ppeff;
                CrackWidth = srmax * dEpsilon;
            }
            State = CheckLimitState();
        }

        private LimitState CheckLimitState(double limitCrackWidth = 0.200)
        {
            if (double.IsNaN(StrainState.A) || double.IsNaN(StrainState.B)) return LimitState.Exceeded;
            if (CrackWidth <= limitCrackWidth) return LimitState.Assured;
            return LimitState.Exceeded;
        }
    }

    public class AnalyticalGirders
    {
        public PhysicalBridge PhysicalBridge;
        public AnalyticalBridge AnalyticalBridge;

        public CrossSection[] CrossSections;
        public DiscreteCrossSection[] DiscreteCrossSections;
        public ReinforcingSteel ReinforcingSteel;
        private readonly double[] Z;

        public GirdersRebarsArrangement GirdersRebars;
        private readonly double CombinedSlabWidth;

        private class PrestressingImpact
        {
            public double PrestressingForce;
            public double PrestressingShear;

            public PrestressingImpact(double x, List<DiscreteTendon> tendons, double middleSupport)
            {
                PrestressingForce = 0.0;
                PrestressingShear = 0.0;

                foreach (DiscreteTendon tendon in tendons)
                {
                    if (tendon.IsPartialTendon() && Math.Abs(middleSupport - x) <= tendon.GetPartialTendon().PointD.X - tendon.GetPartialTendon().PointC.X)
                    {
                        double n = tendon.GetTendonForce(x);
                        double atan = Math.Atan(tendon.GetTendonTan(x));
                        if (double.IsNaN(atan)) atan = Math.Atan(tendon.GetTendonTan(x - 0.001));

                        PrestressingForce += n;
                        PrestressingShear += n * Math.Sin(atan);

                        n = tendon.GetTendonForce(2 * middleSupport - x);
                        atan = Math.Atan(tendon.GetTendonTan(2 * middleSupport - x));
                        if (double.IsNaN(atan)) atan = Math.Atan(tendon.GetTendonTan(2 * middleSupport - x - 0.001));

                        PrestressingForce += n;
                        PrestressingShear += n * Math.Sin(atan);
                    }
                    else
                    {
                        double n = tendon.GetTendonForce(x);
                        double atan = Math.Atan(tendon.GetTendonTan(x));
                        if (double.IsNaN(atan)) atan = Math.Atan(tendon.GetTendonTan(x - 0.001));

                        PrestressingForce += n;
                        PrestressingShear += n * Math.Sin(atan);
                    }
                }
                PrestressingForce = Math.Abs(PrestressingForce);
                PrestressingShear = Math.Abs(PrestressingShear);
            }
        }

        public AnalyticalGirders(PhysicalBridge bridge, List<DiscreteTendon> tendons)
        {
            PhysicalBridge = bridge;

            CrossSections = new CrossSection[2];
            CrossSections[0] = PhysicalBridge.LeftGirderCrossSection;
            CrossSections[1] = PhysicalBridge.RightGirderCrossSection;
            CombinedSlabWidth = PhysicalBridge.AnalyticalSlab.CantileverOverhang[(int) Cantilever.Left] + PhysicalBridge.AnalyticalSlab.SpanLength + PhysicalBridge.AnalyticalSlab.CantileverOverhang[(int) Cantilever.Right];

            DiscreteCrossSections = new DiscreteCrossSection[CrossSections.Length];
            ReinforcingSteel = new ReinforcingSteel();

            Z = new double[CrossSections.Length];
            for (int i = 0; i < CrossSections.Length; i++)
            {
                DiscreteCrossSections[i] = new DiscreteCrossSection(CrossSections[i], PhysicalBridge.Concrete);
                CrossSection UpperPartition = GetPartition(CrossSections[i], CrossSections[i].GravityCenter.Y, true);
                Z[i] = CrossSections[i].MomentsOfInertia.IX / Math.Abs(UpperPartition.Area * UpperPartition.Boundaries.Bottom);
            }
            GirdersRebars = CalculateGirderReinforcement(PhysicalBridge.AnalyticalBridge.Envelopes, tendons);
            PhysicalBridge.AnalyticalGirders = this;
        }

        public GirdersRebarsArrangement CalculateGirderReinforcement(Envelopes envelopes, List<DiscreteTendon> tendons)
        {
            //Ultimate limit state check due to bending in prestressed girders:
            double a = 0.001 * (PhysicalBridge.Cover + Globals.NominalStirrupDiameter + 0.5 * Globals.ReferenceRebarDiameter);
            double bottomReinforcementArea = 0.0, topReinforcementArea = 0.0;
            foreach (DiscreteCrossSection cs in DiscreteCrossSections)
            {
                double area;
                area = cs.GetMinimumReinforcementArea(cs.CrossSection.Width, cs.CrossSection.Height - a);
                if (bottomReinforcementArea < area) bottomReinforcementArea = area;
                if (topReinforcementArea < area) topReinforcementArea = area;
            }

            Reinforcement[][] mainReinforcement = new Reinforcement[Enum.GetNames(typeof(Girders)).Length][];
            for (int girder = 0; girder < Enum.GetNames(typeof(Girders)).Length; girder++)
            {
                Reinforcement bottomReinforcement = new Reinforcement((int) Math.Ceiling(bottomReinforcementArea / (0.25 * Math.PI * Math.Pow(0.001 * Globals.ReferenceRebarDiameter, 2))), Globals.ReferenceRebarDiameter, a, new ReinforcingSteel());
                Reinforcement topReinforcement = new Reinforcement((int) Math.Ceiling(topReinforcementArea / (0.25 * Math.PI * Math.Pow(0.001 * Globals.ReferenceRebarDiameter, 2))), Globals.ReferenceRebarDiameter, DiscreteCrossSections[girder].CrossSection.Height - a, new ReinforcingSteel());

                mainReinforcement[girder] = new Reinforcement[Enum.GetNames(typeof(SectionData)).Length];
                for (int section = 0; section < Enum.GetNames(typeof(SectionData)).Length; section++)
                {
                    double mEd;
                    if (section == (int) SectionData.SupportSection) mEd = envelopes.GetGirder(girder).GetCombination(Combinations.DesignCombination).GetPhase(Phases.OperationalPhase).MY.GetMinForces(PhysicalBridge.CriticalCrossSections[section]).MY;
                    else mEd = envelopes.GetGirder(girder).GetCombination(Combinations.DesignCombination).GetPhase(Phases.OperationalPhase).MY.GetMaxForces(PhysicalBridge.CriticalCrossSections[section]).MY;

                    DiscreteCrossSections[girder].ReinforcementLayers.Clear();
                    DiscreteCrossSections[girder].ReinforcementLayers.Add(bottomReinforcement);
                    DiscreteCrossSections[girder].ReinforcementLayers.Add(topReinforcement);

                    if (section == (int) SectionData.SupportSection) mainReinforcement[girder][section] = topReinforcement;
                    else mainReinforcement[girder][section] = bottomReinforcement;

                    DiscreteCrossSections[girder].PrestressingLayers.Clear();
                    double nEd = 0.0;
                    foreach (DiscreteTendon tendon in tendons)
                    {
                        double tendonArea = 0.0, n = tendon.GetTendonForce(PhysicalBridge.CriticalCrossSections[section]);
                        if (tendon.IsFullTendon()) tendonArea = tendon.GetFullTendon().TendonArea;
                        else
                        {
                            if (section == (int) SectionData.SupportSection) { tendonArea = 2 * tendon.GetPartialTendon().TendonArea; n = 2 * n; }
                            else tendonArea = tendon.GetFullTendon().TendonArea;
                        }
                        nEd += n;
                        DiscreteCrossSections[girder].PrestressingLayers.Add(new Prestressing(tendonArea, tendon.GetTendonZ(PhysicalBridge.CriticalCrossSections[section]), 0.001 * n / tendonArea, new PrestressingSteel()));
                    }
#if BENDINGCALC
                    using (StreamWriter bendingCalcFile = new StreamWriter(PhysicalBridge.Directory + Globals.TextFiles.BendingCalcFile(section, (Girders) Enum.GetValues(typeof(Girders)).GetValue(girder)), false))
                    {
                        bendingCalcFile.WriteLine("Iteration\tH\tB\tB.W\tR.B.n\tR.B.f\tR.B.z\tA.RB\tR.T.n\tR.T.f\tR.T.z\tA.RT\tT1.A\tT1.z\tT1.S\tT2.A\tT2.z\tT2.S\tT3.A\tT3.z\tT3.S\tM.Ed\tM.Rd\tP");
                    }
#endif
                    double mRd; int iteration = 0;
                    do
                    {
                        iteration++;
                        mRd = DiscreteCrossSections[girder].GetBearingCapacity(nEd, 0.100, section != (int) SectionData.SupportSection);
#if BENDINGCALC
                        using (StreamWriter bendingCalcFile = new StreamWriter(PhysicalBridge.Directory + Globals.TextFiles.BendingCalcFile(section, (Girders) Enum.GetValues(typeof(Girders)).GetValue(girder)), true))
                        {
                            string str = string.Format("{0:0}\t{1:0.000}\t{2:0.000}\t{3:0.000}", iteration, DiscreteCrossSections[girder].CrossSection.Height, DiscreteCrossSections[girder].CrossSection.Boundaries.Right - DiscreteCrossSections[girder].CrossSection.Boundaries.Left, DiscreteCrossSections[girder].CrossSection.Width);
                            str += string.Format("\t{0:0}\t{1:0}\t{2:0.000}\t{3:0.0}", DiscreteCrossSections[girder].ReinforcementLayers.First().NumberOfRebars, DiscreteCrossSections[girder].ReinforcementLayers.First().RebarsDiameter, DiscreteCrossSections[girder].ReinforcementLayers.First().Ordinate, DiscreteCrossSections[girder].ReinforcementLayers.First().Area * Math.Pow(1000, 2));
                            str += string.Format("\t{0:0}\t{1:0}\t{2:0.000}\t{3:0.0}", DiscreteCrossSections[girder].ReinforcementLayers.Last().NumberOfRebars, DiscreteCrossSections[girder].ReinforcementLayers.Last().RebarsDiameter, DiscreteCrossSections[girder].ReinforcementLayers.Last().Ordinate, DiscreteCrossSections[girder].ReinforcementLayers.Last().Area * Math.Pow(1000, 2));
                            for (int i = 0; i < 3; i++)
                            {
                                if (i < DiscreteCrossSections[girder].PrestressingLayers.Count()) str += string.Format("\t{0:0.00000}\t{1:0.000}\t{2:0.0}", DiscreteCrossSections[girder].PrestressingLayers[i].Area, DiscreteCrossSections[girder].PrestressingLayers[i].Ordinate, DiscreteCrossSections[girder].PrestressingLayers[i].InitialStress);
                                else str += string.Format("\t{0:0.00000}\t{1:0.000}\t{2:0.0}", 0, 0, 0);
                            }
                            str += string.Format("\t{0:0.0}\t{1:0.0}\t{2:0.000}", mEd, mRd, mEd/mRd);
                            bendingCalcFile.WriteLine(str);
                        }
#endif
                        if (mEd / mRd > 1.000)
                        {
                            StrainState capacityState = DiscreteCrossSections[girder].GetStrainState(nEd, mRd);

                            double bottomReinforcementCap = Math.Abs(capacityState.StrainByCoordinates(DiscreteCrossSections[girder].ReinforcementLayers.First().Ordinate) / DiscreteCrossSections[girder].ReinforcementLayers.First().ReinforcingSteel.UltimateStrain);
                            double bottomConcreteCap = capacityState.StrainByCoordinates(0) / DiscreteCrossSections[girder].Concrete.UltimateStrain;
                            if (double.IsNaN(bottomReinforcementCap) || double.IsNaN(bottomConcreteCap))
                            {
                                bottomReinforcementCap = 1.000;
                                bottomConcreteCap = 1.000;
                            }
                            
                            if ((bottomReinforcementCap > 0.900) || (bottomConcreteCap > 0.900))
                            {
                                DiscreteCrossSections[girder].ReinforcementLayers.First().Rearrange(DiscreteCrossSections[girder].ReinforcementLayers.First().NumberOfRebars + 1, Globals.ReferenceRebarDiameter, true);
                                mainReinforcement[girder][section].Rearrange(DiscreteCrossSections[girder].ReinforcementLayers.First().NumberOfRebars, DiscreteCrossSections[girder].ReinforcementLayers.First().RebarsDiameter, true);
                            }

                            double topReinforcementCap = Math.Abs(capacityState.StrainByCoordinates(DiscreteCrossSections[girder].ReinforcementLayers.Last().Ordinate) / DiscreteCrossSections[girder].ReinforcementLayers.Last().ReinforcingSteel.UltimateStrain);
                            double topConcreteCap = capacityState.StrainByCoordinates(DiscreteCrossSections[girder].CrossSection.Height) / DiscreteCrossSections[girder].Concrete.UltimateStrain;
                            if (double.IsNaN(topReinforcementCap) || double.IsNaN(topConcreteCap))
                            {
                                topReinforcementCap = 1.000;
                                topConcreteCap = 1.000;
                            }

                            if ((topReinforcementCap > 0.900) || (topConcreteCap > 0.900))
                            {
                                DiscreteCrossSections[girder].ReinforcementLayers.Last().Rearrange(DiscreteCrossSections[girder].ReinforcementLayers.Last().NumberOfRebars + 1, Globals.ReferenceRebarDiameter, false);
                                mainReinforcement[girder][section].Rearrange(DiscreteCrossSections[girder].ReinforcementLayers.Last().NumberOfRebars, DiscreteCrossSections[girder].ReinforcementLayers.Last().RebarsDiameter, false);
                            }
                        }
                    }
                    while (mEd / mRd > 1.000);
                }
            }

            //Ultimate limit state check due to shear and torsion in girders (stirrups calculations):
            double[][][] stirrups = new double[Enum.GetNames(typeof(Girders)).Length][][];
            double[][][] longitudinalReinforcement = new double[Enum.GetNames(typeof(Girders)).Length][][];
            double[][][] capacityChecks = new double[Enum.GetNames(typeof(Girders)).Length][][];

            double cotInc = 0.1;
            int cots = (int) Math.Round((2.0 - 1.0) / cotInc) + 1;

            double minimumTransversalReinforcement = Math.Max(GetMinimumTransversalReinforcement(DiscreteCrossSections[(int) Girders.LeftGirder]), GetMinimumTransversalReinforcement(DiscreteCrossSections[(int) Girders.RightGirder]));
            List<double> lengths = new List<double>(); List<double> x = new List<double>();
            for (int girder = 0; girder < Enum.GetNames(typeof(Girders)).Length; girder++)
            {
#if SHEARCALC
                using (StreamWriter shearCalcFile = new StreamWriter(PhysicalBridge.Directory + Globals.TextFiles.ShearCalcFile((Girders) Enum.GetValues(typeof(Girders)).GetValue(girder)), false))
                {
                    string strTitle = "Section\tX\tH\tB.W\tZ\tShear\t\tTorsion\t\tT\t\tL\tS.min\tCot:";
                    string strSubtitle = "\t\t\t\t\tFZ\tMX.acc\tFZ.acc\tMX\tdFX\tdFz\t\t\t";
                    for (int cot = 0; cot < cots; cot++)
                    {
                        strTitle += string.Format("\t{0:0.000}\t{1:0.000}\t\t\t", 1.0 + cot * cotInc, 90 - 180 * Math.Atan(1.0 + cot * cotInc) / Math.PI);
                        strSubtitle += "\tS.S\tC.S\tS.T\tL.T\tC.T";
                    }
                    shearCalcFile.WriteLine(strTitle);
                    shearCalcFile.WriteLine(strSubtitle);
                }
#endif
                List<double> shearFZ = new List<double>(); List<double> torsionFZ = new List<double>();
                List<double> shearMX = new List<double>(); List<double> torsionMX = new List<double>();

                List<Force> fzMax = envelopes.GetGirder(Girders.LeftGirder).GetCombination(Combinations.DesignCombination).GetPhase(Phases.OperationalPhase).GetExtremes(Extremes.FZ).Max;
                List<Force> fzMin = envelopes.GetGirder(Girders.LeftGirder).GetCombination(Combinations.DesignCombination).GetPhase(Phases.OperationalPhase).GetExtremes(Extremes.FZ).Min;
                for (int i = 0; i < fzMax.Count(); i++)
                {
                    if ((i == 0) || (i == fzMax.Count() - 1))
                    {
                        if (Math.Abs(fzMax[i].FZ) > Math.Abs(fzMin[i].FZ))
                        {
                            shearFZ.Add(Math.Abs(fzMax[i].FZ));
                            shearMX.Add(Math.Abs(fzMax[i].MX));
                        }
                        else
                        {
                            shearFZ.Add(Math.Abs(fzMin[i].FZ));
                            shearMX.Add(Math.Abs(fzMin[i].MX));
                        }

                        if (girder == 0)
                        {
                            if (i < fzMax.Count() - 1) lengths.Add(0.5 * (fzMax[i + 1].X - fzMax[i].X));
                            else lengths.Add(0.5 * (fzMax[i].X - fzMax[i - 1].X));

                            x.Add(fzMax[i].X);
                        }
                    }
                    else
                    {
                        if (i % 2 != 0)
                        {
                            if (Math.Abs(fzMax[i].FZ) > Math.Abs(fzMin[i].FZ))
                            {
                                shearFZ.Add(Math.Abs(fzMax[i].FZ));
                                shearMX.Add(Math.Abs(fzMax[i].MX));
                            }
                            else
                            {
                                shearFZ.Add(Math.Abs(fzMin[i].FZ));
                                shearMX.Add(Math.Abs(fzMin[i].MX));
                            }

                            if (girder == 0)
                            {
                                lengths.Add(0.5 * (fzMax[i].X - fzMax[i - 1].X) + 0.5 * (fzMax[i + 2].X - fzMax[i].X));
                                x.Add(fzMax[i].X);
                            }
                        }
                        else if (Math.Max(Math.Abs(fzMax[i].FZ), Math.Abs(fzMin[i].FZ)) > shearFZ.Last())
                        {
                            if (Math.Abs(fzMax[i].FZ) > Math.Abs(fzMin[i].FZ))
                            {
                                shearFZ[shearFZ.Count() - 1] = Math.Abs(fzMax[i].FZ);
                                shearMX[shearFZ.Count() - 1] = Math.Abs(fzMax[i].MX);
                            }
                            else
                            {
                                shearFZ[shearFZ.Count() - 1] = Math.Abs(fzMin[i].FZ);
                                shearMX[shearFZ.Count() - 1] = Math.Abs(fzMin[i].MX);
                            }
                        }
                    }
                }

                List<Force> mxMax = envelopes.GetGirder(Girders.LeftGirder).GetCombination(Combinations.DesignCombination).GetPhase(Phases.OperationalPhase).GetExtremes(Extremes.MX).Max;
                List<Force> mxMin = envelopes.GetGirder(Girders.LeftGirder).GetCombination(Combinations.DesignCombination).GetPhase(Phases.OperationalPhase).GetExtremes(Extremes.MX).Min;
                for (int i = 0; i < mxMax.Count(); i++)
                {
                    if ((i == 0) || (i == mxMax.Count() - 1))
                    {
                        if (Math.Abs(mxMax[i].MX) > Math.Abs(mxMin[i].MX))
                        {
                            torsionFZ.Add(Math.Abs(mxMax[i].FZ));
                            torsionMX.Add(Math.Abs(mxMax[i].MX));
                        }
                        else
                        {
                            torsionFZ.Add(Math.Abs(mxMin[i].FZ));
                            torsionMX.Add(Math.Abs(mxMin[i].MX));
                        }
                    }
                    else
                    {
                        if (i % 2 != 0)
                        {
                            if (Math.Abs(mxMax[i].MX) > Math.Abs(mxMin[i].MX))
                            {
                                torsionFZ.Add(Math.Abs(mxMax[i].FZ));
                                torsionMX.Add(Math.Abs(mxMax[i].MX));
                            }
                            else
                            {
                                torsionFZ.Add(Math.Abs(mxMin[i].FZ));
                                torsionMX.Add(Math.Abs(mxMin[i].MX));
                            }
                        }
                        else if (Math.Max(Math.Abs(mxMax[i].MX), Math.Abs(mxMin[i].MX)) > torsionMX.Last())
                        {
                            if (Math.Abs(mxMax[i].MX) > Math.Abs(mxMin[i].MX))
                            {
                                torsionFZ[torsionFZ.Count() - 1] = Math.Abs(mxMax[i].FZ);
                                torsionMX[torsionFZ.Count() - 1] = Math.Abs(mxMax[i].MX);
                            }
                            else
                            {
                                torsionFZ[torsionFZ.Count() - 1] = Math.Abs(mxMin[i].FZ);
                                torsionMX[torsionFZ.Count() - 1] = Math.Abs(mxMin[i].MX);
                            }
                        }
                    }
                }

                List<PrestressingImpact> prestressing = new List<PrestressingImpact>();
                for (int i = 0; i < x.Count(); i++)
                {
                    prestressing.Add(new PrestressingImpact(x[i], tendons, PhysicalBridge.SpanLength.First()));
                }
                stirrups[girder] = new double[cots][];
                longitudinalReinforcement[girder] = new double[cots][];
                capacityChecks[girder] = new double[cots][];

                DiscreteCrossSections[girder].ReinforcementLayers.Add(new Reinforcement(Math.Abs(DiscreteCrossSections[girder].CrossSection.Boundaries.Bottom), new ReinforcingSteel()));
                for (int cot = 0; cot < cots; cot++)
                {
                    stirrups[girder][cot] = new double[x.Count()];
                    longitudinalReinforcement[girder][cot] = new double[x.Count()];
                    capacityChecks[girder][cot] = new double[x.Count()];
                }

                for (int cs = 0; cs < x.Count(); cs++)
                {
#if SHEARCALC
                    string str = string.Format("{0:0}\t{1:0.000}\t{2:0.000}\t{3:0.000}\t{4:0.000}", cs + 1, x[cs], DiscreteCrossSections[girder].CrossSection.Height, DiscreteCrossSections[girder].CrossSection.Width, Z[girder]);
                    str += string.Format("\t{0:0.0}\t{1:0.0}\t{2:0.0}\t{3:0.0}\t{4:0.0}\t{5:0.0}\t{6:0.0}\t{7:0.000}\t", shearFZ[cs], shearMX[cs], torsionFZ[cs], torsionMX[cs], prestressing[cs].PrestressingForce, prestressing[cs].PrestressingShear, minimumTransversalReinforcement * Math.Pow(1000, 2), lengths[cs]);
#endif
                    for (int cot = 0; cot < cots; cot++)
                    {
                        //Stirrups due to extreme torque and accompanying shear:
                        longitudinalReinforcement[girder][cot][cs] = GetLongitudinalReinforcement(DiscreteCrossSections[girder], torsionMX[cs], 90 - 180 * Math.Atan(1.0 + cot * cotInc) / Math.PI);
                        DiscreteCrossSections[girder].ReinforcementLayers.Last().Area = longitudinalReinforcement[girder][cot][cs];

                        double torsionStirrups = GetTransversalReinforcement(DiscreteCrossSections[girder], torsionFZ[cs], torsionMX[cs], 90 - 180 * Math.Atan(1.0 + cot * cotInc) / Math.PI, Z[girder], prestressing[cs].PrestressingShear);
                        double torsionCapacity = CheckTransversalCapacity(DiscreteCrossSections[girder], torsionFZ[cs], torsionMX[cs], prestressing[cs].PrestressingForce, 90 - 180 * Math.Atan(1.0 + cot * cotInc) / Math.PI, Z[girder], prestressing[cs].PrestressingShear);

                        //Stirrups due to extreme shear and accompanying torque:
                        double shearStirrups = GetTransversalReinforcement(DiscreteCrossSections[girder], shearFZ[cs], shearMX[cs], 90 - 180 * Math.Atan(1.0 + cot * cotInc) / Math.PI, Z[girder], prestressing[cs].PrestressingShear);
                        double shearCapacity = CheckTransversalCapacity(DiscreteCrossSections[girder], shearFZ[cs], shearMX[cs], prestressing[cs].PrestressingForce, 90 - 180 * Math.Atan(1.0 + cot * cotInc) / Math.PI, Z[girder], prestressing[cs].PrestressingShear);

                        //Max:
                        stirrups[girder][cot][cs] = Math.Max(shearStirrups, torsionStirrups);
                        capacityChecks[girder][cot][cs] = Math.Max(shearCapacity, torsionCapacity);
#if SHEARCALC
                        str += string.Format("\t{0:0.0}\t{1:0.000}\t{2:0.0}\t{3:0.0}\t{4:0.000}", shearStirrups * Math.Pow(1000, 2), shearCapacity, torsionStirrups * Math.Pow(1000, 2), longitudinalReinforcement[girder][cot][cs] * Math.Pow(1000, 2), torsionCapacity);
#endif
                    }
#if SHEARCALC
                    using (StreamWriter shearCalcFile = new StreamWriter(PhysicalBridge.Directory + Globals.TextFiles.ShearCalcFile((Girders) Enum.GetValues(typeof(Girders)).GetValue(girder)), true))
                    {
                        shearCalcFile.WriteLine(str);
                    }
#endif
                }
            }

            Reinforcement bottomSpanReinforcement = mainReinforcement[(int) Girders.LeftGirder][(int) SectionData.SpanSection];
            Reinforcement topSupportReinforcement = mainReinforcement[(int) Girders.LeftGirder][(int) SectionData.SupportSection];
            double bottomMaxArea = mainReinforcement[(int) Girders.LeftGirder][(int) SectionData.SpanSection].Area;
            double topMaxArea = mainReinforcement[(int) Girders.LeftGirder][(int) SectionData.SupportSection].Area;

            for (int girder = 0; girder < Enum.GetNames(typeof(Girders)).Length; girder++)
            {
                for (int section = 0; section < Enum.GetNames(typeof(SectionData)).Length; section++)
                {
                    if (section == (int) SectionData.SupportSection)
                    {
                        if (mainReinforcement[girder][section].Area > topMaxArea)
                        {
                            topSupportReinforcement = mainReinforcement[girder][section];
                            topMaxArea = mainReinforcement[girder][section].Area;
                        }
                    }
                    else
                    {
                        if (mainReinforcement[girder][section].Area > bottomMaxArea)
                        {
                            bottomSpanReinforcement = mainReinforcement[girder][section];
                            bottomMaxArea = mainReinforcement[girder][section].Area;
                        }
                    }
                }
            }

            List<GirdersRebarsArrangement> arrangements = new List<GirdersRebarsArrangement>();
            for (int cot = 0; cot < cots; cot++)
            {
                arrangements.Add(new GirdersRebarsArrangement(PhysicalBridge, bottomSpanReinforcement, topSupportReinforcement, Math.Max(longitudinalReinforcement[(int) Girders.LeftGirder][cot].Max(), longitudinalReinforcement[(int) Girders.RightGirder][cot].Max()), CombinedSlabWidth, Math.Max(capacityChecks[(int) Girders.LeftGirder][cot].Max(), capacityChecks[(int) Girders.RightGirder][cot].Max()), 90 - 180 * Math.Atan(1.0 + cot * cotInc) / Math.PI));

                List<Stirrups> leftGirderStirrups = new List<Stirrups>();
                List<Stirrups> rightGirderStirrups = new List<Stirrups>();
                for (int i = 0; i < stirrups[(int) Girders.LeftGirder][cot].Count(); i++)
                {
                    double loc = 0.0;
                    if (i == 0) loc = x[i];
                    else if (i <= stirrups[(int) Girders.LeftGirder][cot].Count() - 2) loc = x[i] - 0.5 * lengths[i];
                    else if (i == stirrups[(int) Girders.LeftGirder][cot].Count() - 1) loc = x[i] - lengths[i];

                    leftGirderStirrups.Add(new Stirrups(PhysicalBridge.LeftGirderCrossSection, Math.Max(minimumTransversalReinforcement, Math.Max(stirrups[(int) Girders.LeftGirder][cot][i], stirrups[(int) Girders.RightGirder][cot][i])), loc, lengths[i], PhysicalBridge.Cover));
                    rightGirderStirrups.Add(new Stirrups(PhysicalBridge.RightGirderCrossSection, Math.Max(minimumTransversalReinforcement, Math.Max(stirrups[(int) Girders.LeftGirder][cot][i], stirrups[(int) Girders.RightGirder][cot][i])), loc, lengths[i], PhysicalBridge.Cover));
                }
                arrangements.Last().AddStirrups(leftGirderStirrups, Girders.LeftGirder);
                arrangements.Last().AddStirrups(rightGirderStirrups, Girders.RightGirder);
            }

            List<GirdersRebarsArrangement> orderedArrangements = arrangements.Where(c => c.Capacity <= 1.000).OrderBy(arr => arr.Mass).ToList();
            if (arrangements.Where(c => c.Capacity > 1.000).Count() > 0) orderedArrangements.AddRange(arrangements.Where(c => c.Capacity > 1.000).OrderBy(arr => arr.Mass));

#if GIRDERSREBARS
            using (StreamWriter girdersArrFile = new StreamWriter(PhysicalBridge.Directory + Globals.TextFiles.GirdersRebarsArrangement, false))
            {
                string strTitle = "R.n\tR.f\tR.L\tA\tM";

                girdersArrFile.WriteLine("Bottom, span:");
                girdersArrFile.WriteLine(strTitle);
                girdersArrFile.WriteLine(string.Format("{0:0}\t{1:0}\t{2:0.000}\t{3:0.0}\t{4:0.0}\n", orderedArrangements.First().BottomSpanRebars.NumberOfRebars, orderedArrangements.First().BottomSpanRebars.RebarsDiameter, orderedArrangements.First().BottomSpanRebarsLength, orderedArrangements.First().BottomSpanRebars.Area * Math.Pow(1000, 2), orderedArrangements.First().BottomSpanRebars.Area * orderedArrangements.First().BottomSpanRebarsLength * 7850));

                girdersArrFile.WriteLine("Bottom, end:");
                girdersArrFile.WriteLine(strTitle);
                girdersArrFile.WriteLine(string.Format("{0:0}\t{1:0}\t{2:0.000}\t{3:0.0}\t{4:0.0}\n", orderedArrangements.First().BottomEndSupportRebars.NumberOfRebars, orderedArrangements.First().BottomEndSupportRebars.RebarsDiameter, orderedArrangements.First().BottomEndSupportRebarsLength, orderedArrangements.First().BottomEndSupportRebars.Area * Math.Pow(1000, 2), orderedArrangements.First().BottomEndSupportRebars.Area * orderedArrangements.First().BottomEndSupportRebarsLength * 7850));

                girdersArrFile.WriteLine("Bottom, support:");
                girdersArrFile.WriteLine(strTitle);
                girdersArrFile.WriteLine(string.Format("{0:0}\t{1:0}\t{2:0.000}\t{3:0.0}\t{4:0.0}\n", orderedArrangements.First().BottomMiddleSupportRebars.NumberOfRebars, orderedArrangements.First().BottomMiddleSupportRebars.RebarsDiameter, orderedArrangements.First().BottomMiddleSupportRebarsLength, orderedArrangements.First().BottomMiddleSupportRebars.Area * Math.Pow(1000, 2), orderedArrangements.First().BottomMiddleSupportRebars.Area * orderedArrangements.First().BottomMiddleSupportRebarsLength * 7850));

                girdersArrFile.WriteLine("Top, support:");
                girdersArrFile.WriteLine(strTitle);
                girdersArrFile.WriteLine(string.Format("{0:0}\t{1:0}\t{2:0.000}\t{3:0.0}\t{4:0.0}\n", orderedArrangements.First().TopSupportRebars.NumberOfRebars, orderedArrangements.First().TopSupportRebars.RebarsDiameter, orderedArrangements.First().TopSupportRebarsLength, orderedArrangements.First().TopSupportRebars.Area * Math.Pow(1000, 2), orderedArrangements.First().TopSupportRebars.Area * orderedArrangements.First().TopSupportRebarsLength * 7850));

                girdersArrFile.WriteLine("Top, span:");
                girdersArrFile.WriteLine(strTitle);
                girdersArrFile.WriteLine(string.Format("{0:0}\t{1:0}\t{2:0.000}\t{3:0.0}\t{4:0.0}\n", orderedArrangements.First().TopSpanRebars.NumberOfRebars, orderedArrangements.First().TopSpanRebars.RebarsDiameter, orderedArrangements.First().TopSpanRebarsLength, orderedArrangements.First().TopSpanRebars.Area * Math.Pow(1000, 2), orderedArrangements.First().TopSpanRebars.Area * orderedArrangements.First().TopSpanRebarsLength * 7850));

                girdersArrFile.WriteLine("Stirrups:");
                girdersArrFile.WriteLine("\t\t\t\t\tLeft girder:\t\t\tRight girder:");
                girdersArrFile.WriteLine("Section\tCot\tT\tX\tR\tA\tA.R\tL\tM\tA\tA.R\tL\tM");

                List<Stirrups> leftGirderStirrups = orderedArrangements.First().GetStirrups(Girders.LeftGirder);
                List<Stirrups> rightGirderStirrups = orderedArrangements.First().GetStirrups(Girders.RightGirder);
                for (int i = 0; i < orderedArrangements.First().GetStirrups(Girders.LeftGirder).Count(); i++)
                {
                    girdersArrFile.WriteLine(string.Format("{0:0}\t{1:0.000}\t{2:0.000}\t{3:0.000}\t{4:0.000}\t{5:0.0}\t{6:0.0}\t{7:0.000}\t{8:0.0}\t{9:0.0}\t{10:0.0}\t{11:0.000}\t{12:0.0}",
                        i + 1, 1 / Math.Tan(orderedArrangements.First().Theta * Math.PI / 180), orderedArrangements.First().Theta, leftGirderStirrups[i].X, leftGirderStirrups[i].Range, leftGirderStirrups[i].AreaPerUnit * Math.Pow(1000, 2), leftGirderStirrups[i].Area * Math.Pow(1000, 2), leftGirderStirrups[i].Length, leftGirderStirrups[i].Mass, rightGirderStirrups[i].AreaPerUnit * Math.Pow(1000, 2), rightGirderStirrups[i].Area * Math.Pow(1000, 2), rightGirderStirrups[i].Length, rightGirderStirrups[i].Mass));
                }
                girdersArrFile.WriteLine("\nArrangement summary per theta (both girders):");
                girdersArrFile.WriteLine("\nCot\tT\tM.S\tM.Lg\tM.Lt\tM.tot\tC");
                for (int i = 0; i < arrangements.Count(); i++)
                {
                    girdersArrFile.WriteLine(string.Format("{0:0.000}\t{1:0.000}\t{2:0.0}\t{3:0.0}\t{4:0.0}\t{5:0.0}\t{6:0.000}",
                        1 / Math.Tan(arrangements[i].Theta * Math.PI / 180), arrangements[i].Theta, arrangements[i].CalculateTransversalMass(), arrangements[i].CalculateLongitudinalMass(false, true), arrangements[i].CalculateLongitudinalMass() - arrangements[i].CalculateLongitudinalMass(false), arrangements[i].CalculateTotalMass(), arrangements[i].Capacity));
                }
                girdersArrFile.WriteLine(string.Format("\nLongitudinal reinforcement for torsion (per girder):\t{0:0.0}\tTotal mass:\t{1:0.0}", orderedArrangements.First().TorsionRebars * Math.Pow(1000, 2), orderedArrangements.First().CalculateLongitudinalMass() - orderedArrangements.First().CalculateLongitudinalMass(false, true)));
                girdersArrFile.WriteLine(string.Format("Longitudinal slab reinforcement:\t\t\t{0:0.0}\tTotal mass:\t{1:0.0}\n", orderedArrangements.First().LongitudinalSlabRebars * Math.Pow(1000, 2), orderedArrangements.First().CalculateLongitudinalMass() - orderedArrangements.First().CalculateLongitudinalMass(true, false)));
                girdersArrFile.WriteLine(string.Format("Capacity:\t{0:0.000}\nTotal mass:\t{1:0.0}", orderedArrangements.First().Capacity, orderedArrangements.First().CalculateTotalMass()));
            }
#endif
            if (orderedArrangements.First().Capacity <= 1.000) return orderedArrangements.First();
            return null;
        }

        public double GetTransversalReinforcement(DiscreteCrossSection discreteCrossSection, double vEd, double tEd, double theta, double z, double prestressingShear = 0.0)
        {
            vEd = Math.Max(vEd - 0.9 * prestressingShear, 0);

            //Transversal reinforcement due to shear:
            double shearStirrups = 0.001 * vEd / (z * ReinforcingSteel.YieldStrength / ReinforcingSteel.Gamma / Math.Tan(theta * Math.PI / 180));

            //Transversal reinforcement due to torsion:
            double tef = discreteCrossSection.CrossSection.Height * discreteCrossSection.CrossSection.Width / (2 * discreteCrossSection.CrossSection.Height + 2 * discreteCrossSection.CrossSection.Width);
            double ak = (discreteCrossSection.CrossSection.Height - tef) * (discreteCrossSection.CrossSection.Width - tef);

            double torsionStirrups = 0.001 * tEd / (2 * ak) * (discreteCrossSection.CrossSection.Height - tef) / (z * ReinforcingSteel.YieldStrength / ReinforcingSteel.Gamma / Math.Tan(theta * Math.PI / 180));

            return shearStirrups + 2 * torsionStirrups;
        }

        public double GetMinimumTransversalReinforcement(DiscreteCrossSection discreteCrossSection)
        {
            //Minimal transversal reinforcement:
            return 0.08 * Math.Sqrt(discreteCrossSection.Concrete.CompressiveStrength) / discreteCrossSection.ReinforcementLayers.OrderBy(r => r.ReinforcingSteel.YieldStrength).First().ReinforcingSteel.YieldStrength * discreteCrossSection.CrossSection.Width;
        }

        public double CheckTransversalCapacity(DiscreteCrossSection discreteCrossSection, double vEd, double tEd, double nEd, double theta, double z, double prestressingShear = 0.0)
        {
            vEd = Math.Max(vEd - 0.9 * prestressingShear, 0);

            //Capacity due to shear:
            double area = discreteCrossSection.CrossSection.Area;
            foreach (Reinforcement reinforcementLayers in discreteCrossSection.ReinforcementLayers) area += (reinforcementLayers.ReinforcingSteel.ModulusOfElasticity / discreteCrossSection.Concrete.ModulusOfElasticity) * reinforcementLayers.Area;

            double compressiveStress = 0.001 * nEd / area;
            double alfacw = 1.000;
            if (compressiveStress <= 0.25 * discreteCrossSection.Concrete.CompressiveStrength / discreteCrossSection.Concrete.Gamma) alfacw = 1 + compressiveStress / (discreteCrossSection.Concrete.CompressiveStrength / discreteCrossSection.Concrete.Gamma);
            else if (compressiveStress <= 0.50 * discreteCrossSection.Concrete.CompressiveStrength / discreteCrossSection.Concrete.Gamma) alfacw = 1.250;
            else if (compressiveStress <= discreteCrossSection.Concrete.CompressiveStrength) alfacw = 2.5 * (1 - compressiveStress / (discreteCrossSection.Concrete.CompressiveStrength / discreteCrossSection.Concrete.Gamma));

            double v = 0.6 * (1 - discreteCrossSection.Concrete.CompressiveStrength / 250);

            double shearCapacity = 1000 * alfacw * discreteCrossSection.CrossSection.Width * z * v * discreteCrossSection.Concrete.CompressiveStrength / discreteCrossSection.Concrete.Gamma / (Math.Tan(theta * Math.PI / 180) + 1 / Math.Tan(theta * Math.PI / 180));

            //Capacity due to torsion:
            double tef = discreteCrossSection.CrossSection.Height * discreteCrossSection.CrossSection.Width / (2 * discreteCrossSection.CrossSection.Height + 2 * discreteCrossSection.CrossSection.Width);
            double ak = (discreteCrossSection.CrossSection.Height - tef) * (discreteCrossSection.CrossSection.Width - tef);

            double torsionCapacity = 1000 * alfacw * v * (discreteCrossSection.Concrete.CompressiveStrength / discreteCrossSection.Concrete.Gamma) * ak * tef * Math.Sin(theta * Math.PI / 180) * Math.Cos(theta * Math.PI / 180);

            return vEd / shearCapacity + tEd / torsionCapacity;
        }

        public double GetLongitudinalReinforcement(DiscreteCrossSection discreteCrossSection, double tEd, double theta)
        {
            double tef = discreteCrossSection.CrossSection.Height * discreteCrossSection.CrossSection.Width / (2 * discreteCrossSection.CrossSection.Height + 2 * discreteCrossSection.CrossSection.Width);
            double ak = (discreteCrossSection.CrossSection.Height - tef) * (discreteCrossSection.CrossSection.Width - tef);
            double uk = 2 * (discreteCrossSection.CrossSection.Height - tef) + 2 * (discreteCrossSection.CrossSection.Width - tef);

            return 0.001 * tEd * uk / Math.Tan(theta * Math.PI / 180) / (2 * ak * ReinforcingSteel.YieldStrength / ReinforcingSteel.Gamma);
        }

        private CrossSection GetPartition(CrossSection crossSection, double horizontalDivision, bool upperPart = true)
        {
            List<Point> contourPoints = new List<Point>();
            foreach (Point point in crossSection.Vertices) contourPoints.Add(new Point(point.X, point.Y));

            for (int i = contourPoints.Count() - 2; i >= 0; i--)
            {
                if (contourPoints[i].Y == contourPoints[i + 1].Y) continue;
                else if (contourPoints[i].Y > contourPoints[i + 1].Y)
                {
                    if ((contourPoints[i].Y >= horizontalDivision) && (contourPoints[i + 1].Y <= horizontalDivision))
                    {
                        double x = contourPoints[i].X + ((double) horizontalDivision - contourPoints[i].Y) * (contourPoints[i + 1].X - contourPoints[i].X) / (contourPoints[i + 1].Y - contourPoints[i].Y);
                        contourPoints.Insert(i + 1, new Point(x, (double) horizontalDivision));
                    }
                }
                else
                {
                    if ((contourPoints[i].Y <= horizontalDivision) && (contourPoints[i + 1].Y >= horizontalDivision))
                    {
                        double x = contourPoints[i].X + ((double) horizontalDivision - contourPoints[i].Y) * (contourPoints[i + 1].X - contourPoints[i].X) / (contourPoints[i + 1].Y - contourPoints[i].Y);
                        contourPoints.Insert(i + 1, new Point(x, (double) horizontalDivision));
                    }
                }
            }
            contourPoints.RemoveAt(contourPoints.Count() - 1);
            contourPoints = contourPoints.Distinct().ToList();

            List<Point> PartitionVertices = new List<Point>();
            if (upperPart)
            {
                foreach (Point contourPoint in contourPoints)
                {
                    if (Math.Round(contourPoint.Y - horizontalDivision, 8) >= 0.0) PartitionVertices.Add(contourPoint);
                }
            }
            else
            {
                foreach (Point contourPoint in contourPoints)
                {
                    if (Math.Round(contourPoint.Y - horizontalDivision, 8) <= 0.0) PartitionVertices.Add(contourPoint);
                }
            }
            return new CrossSection(PartitionVertices);
        }
    }
    
    public class Stirrups
    {
        public double Area; //m^2
        public double AreaPerUnit; //m^2/m

        public double X;
        public double Range;

        public double Length;
        public double Mass;

        public Stirrups(CrossSection crossSection, double areaPerUnit, double x, double range, double cover)
        {
            AreaPerUnit = areaPerUnit;
            X = x; Range = range;

            Area = AreaPerUnit * Range;
            Length = 2 * (crossSection.Height - 2 * 0.001 * (cover + 0.5 * Globals.NominalStirrupDiameter)) + 2 * (crossSection.Width - 2 * 0.001 * (cover + 0.5 * Globals.NominalStirrupDiameter));
            Mass = Area * Length * 7850; //kg
        }
    }

    public class GirdersRebarsArrangement
    {
        //Stirrups:
        private readonly List<Stirrups> LeftGirderStirrups;
        private readonly List<Stirrups> RightGirderStirrups;

        //Rebars lengths:
        public double BottomSpanRebarsLength;
        public double BottomMiddleSupportRebarsLength;
        public double BottomEndSupportRebarsLength;

        public double TopSpanRebarsLength;
        public double TopSupportRebarsLength;
        public double TorsionRebarsLength;

        public double LongitudinalSlabRebarsLength;

        //Reinforcement class:
        public Reinforcement BottomSpanRebars;
        public Reinforcement BottomMiddleSupportRebars;
        public Reinforcement BottomEndSupportRebars;

        public Reinforcement TopSpanRebars;
        public Reinforcement TopSupportRebars;
        public double TorsionRebars;

        public double LongitudinalSlabRebars;

        public double Capacity;
        public double Theta;
        public double Mass;

        public GirdersRebarsArrangement(PhysicalBridge bridge, Reinforcement bottomSpanReinforcement, Reinforcement topSupportReinforcement, double torsionReinforcementArea, double combinedSlabWidth, double capacity, double theta)
        {
            Capacity = capacity;
            Theta = theta;

            LeftGirderStirrups = new List<Stirrups>();
            RightGirderStirrups = new List<Stirrups>();

            BottomSpanRebars = new Reinforcement(bottomSpanReinforcement.NumberOfRebars, bottomSpanReinforcement.RebarsDiameter, bottomSpanReinforcement.Ordinate, new ReinforcingSteel());
            TopSupportRebars = new Reinforcement(topSupportReinforcement.NumberOfRebars, topSupportReinforcement.RebarsDiameter, topSupportReinforcement.Ordinate, new ReinforcingSteel());

            double minimumReinforcement = 0.0;
            List<DiscreteCrossSection> crossSections = new List<DiscreteCrossSection>
            {
                new DiscreteCrossSection(bridge.LeftGirderCrossSection, bridge.Concrete),
                new DiscreteCrossSection(bridge.RightGirderCrossSection, bridge.Concrete)
            };
            foreach (DiscreteCrossSection cs in crossSections)
            {
                double area;
                area = cs.GetMinimumReinforcementArea(cs.CrossSection.Width, cs.CrossSection.Height - BottomSpanRebars.Ordinate);
                minimumReinforcement = Math.Max(minimumReinforcement, area);
            }

            BottomMiddleSupportRebars = new Reinforcement((int) Math.Ceiling(minimumReinforcement / (0.25 * Math.PI * Math.Pow(0.001 * Globals.ReferenceRebarDiameter, 2))), Globals.ReferenceRebarDiameter, BottomSpanRebars.Ordinate, new ReinforcingSteel());
            BottomEndSupportRebars = new Reinforcement((int) Math.Ceiling(Math.Max(minimumReinforcement, 0.25 * BottomSpanRebars.Area) / (0.25 * Math.PI * Math.Pow(0.001 * Globals.ReferenceRebarDiameter, 2))), Globals.ReferenceRebarDiameter, BottomSpanRebars.Ordinate, new ReinforcingSteel());

            TopSpanRebars = new Reinforcement((int) Math.Ceiling(minimumReinforcement / (0.25 * Math.PI * Math.Pow(0.001 * Globals.ReferenceRebarDiameter, 2))), Globals.ReferenceRebarDiameter, TopSupportRebars.Ordinate, new ReinforcingSteel());
            TorsionRebars = torsionReinforcementArea;

            BottomSpanRebarsLength = 0.50 * bridge.SpanLength.First() + 0.50 * bridge.SpanLength.Last() + 0.001 * (4 * 40 * BottomSpanRebars.RebarsDiameter);
            BottomMiddleSupportRebarsLength = 0.25 * bridge.SpanLength.First() + 0.25 * bridge.SpanLength.Last();
            BottomEndSupportRebarsLength = 0.25 * bridge.SpanLength.First() + 0.25 * bridge.SpanLength.Last();

            TopSpanRebarsLength = 0.75 * bridge.SpanLength.First() + 0.75 * bridge.SpanLength.Last();
            TopSupportRebarsLength = 0.25 * bridge.SpanLength.First() + 0.25 * bridge.SpanLength.Last() + 0.001 * (2 * 40 * TopSupportRebars.RebarsDiameter);
            TorsionRebarsLength = bridge.SpanLength.First() + bridge.SpanLength.Last();

            LongitudinalSlabRebars = 2 * Math.Ceiling(combinedSlabWidth / 0.150) * 0.25 * Math.PI * Math.Pow(0.001 * 12, 2);
            LongitudinalSlabRebarsLength = bridge.SpanLength.First() + bridge.SpanLength.Last();

            Mass = CalculateTotalMass();
        }
        public void AddStirrup(Stirrups stirrup, Girders girder) 
        {
            if (girder == Girders.LeftGirder) LeftGirderStirrups.Add(stirrup);
            if (girder == Girders.RightGirder) RightGirderStirrups.Add(stirrup);
            Mass = CalculateTotalMass();
        }
        public void AddStirrups(List<Stirrups> stirrups, Girders girder)
        {
            if (girder == Girders.LeftGirder) { foreach (Stirrups stirrup in stirrups) LeftGirderStirrups.Add(stirrup); }
            if (girder == Girders.RightGirder) { foreach (Stirrups stirrup in stirrups) RightGirderStirrups.Add(stirrup); }
            Mass = CalculateTotalMass();
        }
        public void RemoveStirrup(Stirrups stirrup, Girders girder) 
        {
            if (girder == Girders.LeftGirder) LeftGirderStirrups.Remove(stirrup);
            if (girder == Girders.RightGirder) RightGirderStirrups.Remove(stirrup);
            Mass = CalculateTotalMass();
        }
        public void RemoveStirrup(int index, Girders girder)
        {
            if (girder == Girders.LeftGirder) LeftGirderStirrups.RemoveAt(index);
            if (girder == Girders.RightGirder) RightGirderStirrups.RemoveAt(index);
            Mass = CalculateTotalMass();
        }
        public List<Stirrups> GetStirrups(Girders girder)
        {
            if (girder == Girders.LeftGirder) return LeftGirderStirrups;
            return RightGirderStirrups;
        }

        public double CalculateLongitudinalMass(bool includeTorsion = true, bool includeLongitudinalSlabReinforcement = true)
        {
            double mass = 0.0;
            mass += BottomSpanRebars.Area * BottomSpanRebarsLength;
            mass += BottomMiddleSupportRebars.Area * BottomMiddleSupportRebarsLength;
            mass += BottomEndSupportRebars.Area * BottomEndSupportRebarsLength;

            mass += TopSpanRebars.Area * TopSpanRebarsLength;
            mass += TopSupportRebars.Area * TopSupportRebarsLength;
            if (includeTorsion) mass += TorsionRebars * TorsionRebarsLength;
            mass = 2 * mass; //Two girders

            if (includeLongitudinalSlabReinforcement) mass += LongitudinalSlabRebars * LongitudinalSlabRebarsLength; //Two layers, top and bottom one included
            return mass * 7850; //kg, two girders;
        }
        public double CalculateTransversalMass()
        {
            double mass = 0.0;

            foreach (Stirrups stirrup in LeftGirderStirrups) mass += stirrup.Mass;
            foreach (Stirrups stirrup in RightGirderStirrups) mass += stirrup.Mass;
            return mass;
        }
        public double CalculateTotalMass()
        {
            return CalculateLongitudinalMass(true) + CalculateTransversalMass();
        }
    }

    public class SlabRebarsArrangement
    {
        //Rebars lengths:
        public double[] BottomCantileverRebarsLength;
        public double[] BottomSpanAdditionalRebarsLength;
        public double BottomSpanRebarsLength;

        public double[] TopCantileverRebarsLength;
        public double TopSpanRebarsLength;

        //Reinforcement class:
        public Reinforcement[] BottomCantileverRebars;
        public Reinforcement[] BottomSpanAdditionalRebars;
        public List<Reinforcement> BottomSpanRebars;

        public List<Reinforcement> TopCantileverRebars;
        public Reinforcement TopSpanRebars;

        public double Mass;

        public SlabRebarsArrangement(AnalyticalSlab analyticalSlab, List<Reinforcement> bottomReinforcement, List<Reinforcement> topReinforcement)
        {
            BottomCantileverRebarsLength = new double[Enum.GetNames(typeof(Cantilever)).Count()];
            BottomCantileverRebarsLength[(int) Cantilever.Left] = analyticalSlab.CantileverOverhang[(int) Cantilever.Left];
            BottomCantileverRebarsLength[(int) Cantilever.Right] = analyticalSlab.CantileverOverhang[(int) Cantilever.Right];

            TopCantileverRebarsLength = new double[Enum.GetNames(typeof(Cantilever)).Count()];
            TopCantileverRebarsLength[(int) Cantilever.Left] = analyticalSlab.CantileverOverhang[(int) Cantilever.Left]
                    + Converters.ToMeters(analyticalSlab.PhysicalBridge.Superstructure.ParametersMap.get_Item("BL1").AsDouble())
                    + Converters.ToMeters(analyticalSlab.PhysicalBridge.Superstructure.ParametersMap.get_Item("BL2").AsDouble())
                    + analyticalSlab.PhysicalBridge.LeftGirderCrossSection.Width + Math.Max(analyticalSlab.CantileverOverhang[(int) Cantilever.Left], analyticalSlab.SpanLength / 3);
            TopCantileverRebarsLength[(int) Cantilever.Right] = analyticalSlab.CantileverOverhang[(int) Cantilever.Right]
                + Converters.ToMeters(analyticalSlab.PhysicalBridge.Superstructure.ParametersMap.get_Item("BP1").AsDouble())
                + Converters.ToMeters(analyticalSlab.PhysicalBridge.Superstructure.ParametersMap.get_Item("BP2").AsDouble())
                + analyticalSlab.PhysicalBridge.RightGirderCrossSection.Width + Math.Max(analyticalSlab.CantileverOverhang[(int) Cantilever.Right], analyticalSlab.SpanLength / 3);

            double slabWidth = Converters.ToMeters(analyticalSlab.PhysicalBridge.Superstructure.ParametersMap.get_Item("O5").AsDouble() - analyticalSlab.PhysicalBridge.Superstructure.ParametersMap.get_Item("O1").AsDouble());

            BottomSpanRebarsLength = analyticalSlab.SpanLength - analyticalSlab.PL - analyticalSlab.PP;
            TopSpanRebarsLength = slabWidth - TopCantileverRebarsLength.Sum();
            if (slabWidth - TopCantileverRebarsLength.Sum() < 1.000)
            {
                TopCantileverRebarsLength[(int) Cantilever.Left] = 0.5 * slabWidth;
                TopCantileverRebarsLength[(int) Cantilever.Right] = 0.5 * slabWidth;
                TopSpanRebarsLength = 0; //No additional top rebars in slab span cross-section
            }

            BottomSpanAdditionalRebarsLength = new double[Enum.GetNames(typeof(Cantilever)).Count()];
            BottomSpanAdditionalRebarsLength[(int) Cantilever.Left] = analyticalSlab.PL;
            BottomSpanAdditionalRebarsLength[(int) Cantilever.Right] = analyticalSlab.PP;

            BottomSpanRebars = new List<Reinforcement>();
            TopCantileverRebars = new List<Reinforcement>();
            foreach (Reinforcement reinforcementLayer in bottomReinforcement) BottomSpanRebars.Add(new Reinforcement(reinforcementLayer.NumberOfRebars, reinforcementLayer.RebarsDiameter, reinforcementLayer.Ordinate, new ReinforcingSteel()));
            foreach (Reinforcement reinforcementLayer in topReinforcement) TopCantileverRebars.Add(new Reinforcement(reinforcementLayer.NumberOfRebars, reinforcementLayer.RebarsDiameter, reinforcementLayer.Ordinate, new ReinforcingSteel()));
            BottomSpanRebars.RemoveAt(0);
            TopCantileverRebars.RemoveAt(0);

            List<double> allowableSpacing = new List<double>
            {
                1000 / BottomSpanRebars.First().NumberOfRebars,
                1000 / TopCantileverRebars.First().NumberOfRebars,
                2 * 1000 / BottomSpanRebars.First().NumberOfRebars,
                2 * 1000 / TopCantileverRebars.First().NumberOfRebars
            };
            allowableSpacing = allowableSpacing.Distinct().OrderByDescending(s => s).ToList();

            BottomCantileverRebars = new Reinforcement[Enum.GetNames(typeof(Cantilever)).Count()];
            BottomSpanAdditionalRebars = new Reinforcement[Enum.GetNames(typeof(Cantilever)).Count()];
            double minimumReinforcementArea = 0.0; DiscreteCrossSection cs;

            //Adjusting minimum bottom reinforcement for left cantilever:
            BottomCantileverRebars[(int) Cantilever.Left] = null;

            cs = new DiscreteCrossSection(analyticalSlab.G3[(int) Cantilever.Left], 1.000, analyticalSlab.PhysicalBridge.Concrete);
            foreach (int rebarDiameter in analyticalSlab.PhysicalBridge.RebarDiameters)
            {
                minimumReinforcementArea = cs.GetMinimumReinforcementArea(1.000, analyticalSlab.G3[(int) Cantilever.Left] - 0.001 * (analyticalSlab.PhysicalBridge.Cover + 0.5 * rebarDiameter));
                foreach (double spacing in allowableSpacing)
                {
                    double area = (1000 / spacing) * 0.25 * Math.PI * Math.Pow(0.001 * rebarDiameter, 2);
                    if (area >= minimumReinforcementArea)
                    {
                        BottomCantileverRebars[(int) Cantilever.Left] = new Reinforcement(1000 / spacing, rebarDiameter, 0.001 * (analyticalSlab.PhysicalBridge.Cover + 0.5 * rebarDiameter), new ReinforcingSteel());
                        break;
                    }
                }
                if (BottomCantileverRebars[(int) Cantilever.Left] != null) break;
            }
            //Adjusting minimum bottom reinforcement for right cantilever:
            BottomCantileverRebars[(int) Cantilever.Right] = null;

            cs = new DiscreteCrossSection(analyticalSlab.G3[(int) Cantilever.Right], 1.000, analyticalSlab.PhysicalBridge.Concrete);
            foreach (int rebarDiameter in analyticalSlab.PhysicalBridge.RebarDiameters)
            {
                minimumReinforcementArea = cs.GetMinimumReinforcementArea(1.000, analyticalSlab.G3[(int) Cantilever.Right] - 0.001 * (analyticalSlab.PhysicalBridge.Cover + 0.5 * rebarDiameter));
                foreach (double spacing in allowableSpacing)
                {
                    double area = (1000 / spacing) * 0.25 * Math.PI * Math.Pow(0.001 * rebarDiameter, 2);
                    if (area >= minimumReinforcementArea)
                    {
                        BottomCantileverRebars[(int) Cantilever.Right] = new Reinforcement(1000 / spacing, rebarDiameter, 0.001 * (analyticalSlab.PhysicalBridge.Cover + 0.5 * rebarDiameter), new ReinforcingSteel());
                        break;
                    }
                }
                if (BottomCantileverRebars[(int) Cantilever.Right] != null) break;
            }
            //Adjusting minimum bottom reinforcement for slab thickening, left:
            if (analyticalSlab.PL == 0) BottomSpanAdditionalRebars[(int) Cantilever.Left] = null;
            else
            {
                BottomSpanAdditionalRebars[(int) Cantilever.Left] = null;

                cs = new DiscreteCrossSection(analyticalSlab.G4, 1.000, analyticalSlab.PhysicalBridge.Concrete);
                foreach (int rebarDiameter in analyticalSlab.PhysicalBridge.RebarDiameters)
                {
                    minimumReinforcementArea = cs.GetMinimumReinforcementArea(1.000, analyticalSlab.G4 - 0.001 * (analyticalSlab.PhysicalBridge.Cover + 0.5 * rebarDiameter));
                    foreach (double spacing in allowableSpacing)
                    {
                        double area = (1000 / spacing) * 0.25 * Math.PI * Math.Pow(0.001 * rebarDiameter, 2);
                        if (area >= minimumReinforcementArea)
                        {
                            BottomSpanAdditionalRebars[(int) Cantilever.Left] = new Reinforcement(1000 / spacing, rebarDiameter, 0.001 * (analyticalSlab.PhysicalBridge.Cover + 0.5 * rebarDiameter), new ReinforcingSteel());
                            break;
                        }
                    }
                    if (BottomSpanAdditionalRebars[(int) Cantilever.Left] != null) break;
                }
            }
            //Adjusting minimum bottom reinforcement for slab thickening, right:
            if (analyticalSlab.PP == 0) BottomSpanAdditionalRebars[(int) Cantilever.Right] = null;
            else
            {
                BottomSpanAdditionalRebars[(int) Cantilever.Right] = null;

                cs = new DiscreteCrossSection(analyticalSlab.G8, 1.000, analyticalSlab.PhysicalBridge.Concrete);
                foreach (int rebarDiameter in analyticalSlab.PhysicalBridge.RebarDiameters)
                {
                    minimumReinforcementArea = cs.GetMinimumReinforcementArea(1.000, analyticalSlab.G8 - 0.001 * (analyticalSlab.PhysicalBridge.Cover + 0.5 * rebarDiameter));
                    foreach (double spacing in allowableSpacing)
                    {
                        double area = (1000 / spacing) * 0.25 * Math.PI * Math.Pow(0.001 * rebarDiameter, 2);
                        if (area >= minimumReinforcementArea)
                        {
                            BottomSpanAdditionalRebars[(int) Cantilever.Right] = new Reinforcement(1000 / spacing, rebarDiameter, 0.001 * (analyticalSlab.PhysicalBridge.Cover + 0.5 * rebarDiameter), new ReinforcingSteel());
                            break;
                        }
                    }
                    if (BottomSpanAdditionalRebars[(int) Cantilever.Right] != null) break;
                }
            }
            //Adjusting minimum bottom reinforcement for slab thickening, right:
            if (TopSpanRebarsLength == 0) TopSpanRebars = null;
            else
            {
                TopSpanRebars = null;
                double t = Math.Max(analyticalSlab.G5, Math.Max(analyticalSlab.G6, analyticalSlab.G7));

                cs = new DiscreteCrossSection(t, 1.000, analyticalSlab.PhysicalBridge.Concrete);
                foreach (int rebarDiameter in analyticalSlab.PhysicalBridge.RebarDiameters)
                {
                    minimumReinforcementArea = cs.GetMinimumReinforcementArea(1.000, t - 0.001 * (analyticalSlab.PhysicalBridge.Cover + 0.5 * rebarDiameter));
                    foreach (double spacing in allowableSpacing)
                    {
                        double area = (1000 / spacing) * 0.25 * Math.PI * Math.Pow(0.001 * rebarDiameter, 2);
                        if (area >= minimumReinforcementArea)
                        {
                            TopSpanRebars = new Reinforcement(1000 / spacing, rebarDiameter, t - 0.001 * (analyticalSlab.PhysicalBridge.Cover + 0.5 * rebarDiameter), new ReinforcingSteel());
                            break;
                        }
                    }
                    if (TopSpanRebars != null) break;
                }
            }

            Mass = 0.0;

            foreach (Reinforcement reinforcementLayer in BottomSpanRebars) Mass += reinforcementLayer.Area * (BottomSpanRebarsLength + 2 * 0.001 * 40 * reinforcementLayer.RebarsDiameter);
            foreach (Reinforcement reinforcementLayer in TopCantileverRebars) Mass += reinforcementLayer.Area * TopCantileverRebarsLength.Sum();
            Mass += BottomCantileverRebars[(int) Cantilever.Left].Area * (BottomCantileverRebarsLength[(int) Cantilever.Left] + 0.001 * 40 * BottomCantileverRebars[(int) Cantilever.Left].RebarsDiameter);
            Mass += BottomCantileverRebars[(int) Cantilever.Right].Area * (BottomCantileverRebarsLength[(int) Cantilever.Right] + 0.001 * 40 * BottomCantileverRebars[(int) Cantilever.Right].RebarsDiameter);
            if (analyticalSlab.PL > 0) Mass += BottomSpanAdditionalRebars[(int) Cantilever.Left].Area * (BottomSpanAdditionalRebarsLength[(int) Cantilever.Left] + 2 * 0.001 * 40 * BottomSpanAdditionalRebars[(int) Cantilever.Left].RebarsDiameter);
            if (analyticalSlab.PP > 0) Mass += BottomSpanAdditionalRebars[(int) Cantilever.Right].Area * (BottomSpanAdditionalRebarsLength[(int) Cantilever.Right] + 2 * 0.001 * 40 * BottomSpanAdditionalRebars[(int) Cantilever.Right].RebarsDiameter);
            if (TopSpanRebars != null) Mass += TopSpanRebars.Area * (TopSpanRebarsLength + 2 * 0.001 * 40 * TopSpanRebars.RebarsDiameter);

            Mass = Mass * 7850; //kg
        }
    }
}