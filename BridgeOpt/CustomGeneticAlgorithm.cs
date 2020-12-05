using System;
using System.Collections.Generic;
using System.Linq;

using GeneticSharp.Domain.Chromosomes;
using GeneticSharp.Domain.Crossovers;
using GeneticSharp.Domain.Mutations;
using GeneticSharp.Domain.Randomizations;

using GeneticSharp.Infrastructure.Framework.Texts;
using GeneticSharp.Infrastructure.Framework.Commons;

namespace BridgeOpt
{
    public class DoubleChromosome : IChromosome
    {
        public double[] MinValues;
        public double[] MaxValues;

        public double[] InitialMinValues;
        public double[] InitialMaxValues;

        public Gene[] Genes;

        public DoubleChromosome(double[] initMinValues, double[] initMaxValues, double[] minValues = null, double[] maxValues = null)
        {
            if (!minValues.Any()) minValues = initMinValues;
            if (!maxValues.Any()) maxValues = initMaxValues;

            MinValues = minValues;
            MaxValues = maxValues;

            InitialMinValues = initMinValues;
            InitialMaxValues = initMaxValues;

            Length = minValues.Count();
            Genes = new Gene[Length];

            var rnd = RandomizationProvider.Current;
            for (int i = 0; i < Length; i++)
            {
                ReplaceGene(i, new Gene(rnd.GetDouble(initMinValues[i], initMaxValues[i])));
            }
        }

        public double? Fitness { get; set; }
        public int Length { get; set; }

        public IChromosome CreateNew()
        {
            return new DoubleChromosome(InitialMinValues, InitialMaxValues, MinValues, MaxValues);
        }

        public IChromosome Clone()
        {
            var clone = CreateNew();
            clone.ReplaceGenes(0, GetGenes());
            clone.Fitness = Fitness;

            return clone;
        }

        public int CompareTo(IChromosome other)
        {
            if (other == null)
            {
                return -1;
            }

            var otherFitness = other.Fitness;
            if (Fitness == otherFitness)
            {
                return 0;
            }
            return Fitness > otherFitness ? 1 : -1;
        }

        public void ReplaceGene(int index, Gene gene)
        {
            if ((index < 0) || (index >= Length))
            {
                throw new ArgumentOutOfRangeException(nameof(index), "There is no gene on index {0} to be replaced.".With(index));
            }
            Genes[index] = gene;
            Fitness = null;
        }

        public void ReplaceGenes(int startIndex, Gene[] genes)
        {
            ExceptionHelper.ThrowIfNull("genes", genes);

            if (genes.Length > 0)
            {
                if ((startIndex < 0) || (startIndex >= Length))
                {
                    throw new ArgumentOutOfRangeException(nameof(startIndex), "There is no gene on index {0} to be replaced.".With(startIndex));
                }

                var genesToBeReplacedLength = genes.Length;
                var availableSpaceLength = Length - startIndex;

                if (genesToBeReplacedLength > availableSpaceLength)
                {
                    throw new ArgumentException(nameof(Gene), "The number of genes to be replaced is greater than available space, there is {0} genes between the index {1} and the end of chromosome, but there is {2} genes to be replaced.".With(availableSpaceLength, startIndex, genesToBeReplacedLength));
                }
                Array.Copy(genes, 0, Genes, startIndex, genes.Length);
                Fitness = null;
            }
        }

        public void Resize(int newLength)
        {
            Array.Resize(ref Genes, newLength);
            Length = newLength;
        }

        public Gene GenerateGene(int geneIndex)
        {
            return new Gene(RandomizationProvider.Current.GetDouble(InitialMinValues[geneIndex], InitialMaxValues[geneIndex]));
        }

        public Gene GetGene(int index)
        {
            return Genes[index];
        }

        public Gene[] GetGenes()
        {
            return Genes;
        }

        public override string ToString()
        {
            return string.Join("", GetGenes().Select(g => "[" + ((double)g.Value).ToString("F3") + "]").ToArray());
        }
    }

    public class PrestressingChromosome : IChromosome
    {
        public double[] MinValues;
        public double[] MaxValues;

        public double[] InitialMinValues;
        public double[] InitialMaxValues;

        public Gene[] Genes;

        public double Weight;
        public double[] Exceedance;
        public int Exceeds;
        public int Collisions;

        public PrestressingChromosome(double[] initMinValues, double[] initMaxValues, double[] minValues = null, double[] maxValues = null)
        {
            if (!minValues.Any()) minValues = initMinValues;
            if (!maxValues.Any()) maxValues = initMaxValues;

            MinValues = minValues;
            MaxValues = maxValues;

            InitialMinValues = initMinValues;
            InitialMaxValues = initMaxValues;

            Length = minValues.Count();
            Genes = new Gene[Length];

            var rnd = RandomizationProvider.Current;
            for (int i = 0; i < Length; i++)
            {
                ReplaceGene(i, new Gene(rnd.GetDouble(initMinValues[i], initMaxValues[i])));
            }

            Weight = 0.0;
            Exceedance = new double[Enum.GetNames(typeof(Combinations)).Length - 1];
            Collisions = 0;
        }

        public double? Fitness { get; set; }
        public int Length { get; set; }

        public IChromosome CreateNew()
        {
            return new PrestressingChromosome(InitialMinValues, InitialMaxValues, MinValues, MaxValues);
        }

        public IChromosome Clone()
        {
            var clone = CreateNew();
            clone.ReplaceGenes(0, GetGenes());
            clone.Fitness = Fitness;

            return clone;
        }

        public int CompareTo(IChromosome other)
        {
            if (other == null)
            {
                return -1;
            }

            var otherFitness = other.Fitness;
            if (Fitness == otherFitness)
            {
                return 0;
            }
            return Fitness > otherFitness ? 1 : -1;
        }

        public void ReplaceGene(int index, Gene gene)
        {
            if ((index < 0) || (index >= Length))
            {
                throw new ArgumentOutOfRangeException(nameof(index), "There is no gene on index {0} to be replaced.".With(index));
            }
            Genes[index] = gene;
            Fitness = null;

            Weight = 0.0;
            Exceedance = new double[Enum.GetNames(typeof(Combinations)).Length - 1];
            Exceedance[(int) Combinations.RareCombination - 1] = 0.0;
            Exceedance[(int) Combinations.FrequentCombination - 1] = 0.0;
            Exceedance[(int) Combinations.QuasiPermanentCombination - 1] = 0.0;
            Collisions = 0;
        }

        public void ReplaceGenes(int startIndex, Gene[] genes)
        {
            ExceptionHelper.ThrowIfNull("genes", genes);

            if (genes.Length > 0)
            {
                if ((startIndex < 0) || (startIndex >= Length))
                {
                    throw new ArgumentOutOfRangeException(nameof(startIndex), "There is no gene on index {0} to be replaced.".With(startIndex));
                }

                var genesToBeReplacedLength = genes.Length;
                var availableSpaceLength = Length - startIndex;

                if (genesToBeReplacedLength > availableSpaceLength)
                {
                    throw new ArgumentException(nameof(Gene), "The number of genes to be replaced is greater than available space, there is {0} genes between the index {1} and the end of chromosome, but there is {2} genes to be replaced.".With(availableSpaceLength, startIndex, genesToBeReplacedLength));
                }
                Array.Copy(genes, 0, Genes, startIndex, genes.Length);
                Fitness = null;

                Weight = 0.0;
                Exceedance = new double[Enum.GetNames(typeof(Combinations)).Length - 1];
                Exceedance[(int) Combinations.RareCombination - 1] = 0.0;
                Exceedance[(int) Combinations.FrequentCombination - 1] = 0.0;
                Exceedance[(int) Combinations.QuasiPermanentCombination - 1] = 0.0;
                Collisions = 0;
            }
        }

        public void Resize(int newLength)
        {
            Array.Resize(ref Genes, newLength);
            Length = newLength;
        }

        public Gene GenerateGene(int geneIndex)
        {
            return new Gene(RandomizationProvider.Current.GetDouble(InitialMinValues[geneIndex], InitialMaxValues[geneIndex]));
        }

        public Gene GetGene(int index)
        {
            return Genes[index];
        }

        public Gene[] GetGenes()
        {
            return Genes;
        }

        public override string ToString()
        {
            string str = string.Join("", GetGenes().Select(g => "[" + ((double) g.Value).ToString("F3") + "]").ToArray());
            str += string.Format(" W: {0:0.000}, E: {1:0.000}", Weight, Exceedance);
            return str;
        }
    }

    public sealed class SimulatedBinaryCrossover : CrossoverBase
    {
        public double DistributionIndex { get; set; }
        public double[] LowerBound;
        public double[] UpperBound;

        public SimulatedBinaryCrossover(double distributionIndex, double[] lowerBound = null, double[] upperBound = null) : base(2, 2)
        {
            IsOrdered = false;
            DistributionIndex = distributionIndex;

            LowerBound = lowerBound;
            UpperBound = upperBound;
        }

        protected override IList<IChromosome> PerformCross(IList<IChromosome> parents)
        {
            if (DistributionIndex < 0)
            {
                throw new CrossoverException(this, "Invalid distribution index value. Distribution index should be non-negative.");
            }
            var firstOffspring = parents[0].CreateNew();
            var secondOffspring = parents[1].CreateNew();

            Gene[] firstParentGenes = parents[0].GetGenes();
            Gene[] secondParentGenes = parents[1].GetGenes();
            double[] betas = new double[firstParentGenes.Count()];

            double u = RandomizationProvider.Current.GetDouble();
            for (int i = 0; i < firstParentGenes.Count(); i++)
            {
                double firstParentGene = (double) firstParentGenes[i].Value;
                double secondParentGene = (double) secondParentGenes[i].Value;

                double lowerBound; double upperBound;
                if (LowerBound == null) lowerBound = double.NegativeInfinity;
                else lowerBound = LowerBound[i];
                if (UpperBound == null) upperBound = double.PositiveInfinity;
                else upperBound = UpperBound[i];
                
                double beta = CalculateBeta(DistributionIndex, firstParentGene, secondParentGene, lowerBound, upperBound, u);
                betas[i] = beta;
                firstOffspring.ReplaceGene(i, new Gene(0.5 * (firstParentGene + secondParentGene - beta * Math.Abs(secondParentGene - firstParentGene))));
                secondOffspring.ReplaceGene(i, new Gene(0.5 * (firstParentGene + secondParentGene + beta * Math.Abs(secondParentGene - firstParentGene))));
            }
            return new List<IChromosome>() { firstOffspring, secondOffspring };
        }

        public double CalculateBeta(double distributionIndex, double x1, double x2, double lowerBound, double upperBound, double u)
        {
            double alpha; double beta;
            if (double.IsNegativeInfinity(lowerBound) && double.IsPositiveInfinity(upperBound)) alpha = 2;
            else if (x1 == x2) alpha = 2;
            else
            {
                double range;
                if (double.IsNegativeInfinity(lowerBound)) range = upperBound - Math.Max(x1, x2);
                else if (double.IsPositiveInfinity(upperBound)) range = Math.Min(x1, x2) - lowerBound;
                else range = Math.Min(Math.Min(x1, x2) - lowerBound, upperBound - Math.Max(x1, x2));

                if (Math.Round(Math.Abs(Math.Max(x1, x2) - Math.Min(x1, x2)), 8) == 0) alpha = 2;
                beta = 1 + 2 / (Math.Max(x1, x2) - Math.Min(x1, x2)) * range;
                alpha = 2 - Math.Pow(beta, -1 * (distributionIndex + 1));
            }

            if (u <= 1 / alpha)
            {
                return Math.Pow(alpha * u, 1 / (distributionIndex + 1));
            }
            else
            {
                return Math.Pow(1 / (2 - alpha * u), 1 / (distributionIndex + 1));
            }
        }
    }

    public sealed class PolynomialMutation : MutationBase
    {
        public double DistributionIndex { get; set; }
        public double[] LowerBound;
        public double[] UpperBound;

        public PolynomialMutation(double distributioIndex, double[] lowerBound = null, double[] upperBound = null)
        {
            DistributionIndex = distributioIndex;
            IsOrdered = false;

            LowerBound = lowerBound;
            UpperBound = upperBound;
        }

        protected override void PerformMutate(IChromosome chromosome, float probability)
        {
            if (RandomizationProvider.Current.GetDouble() <= probability)
            {
                double u = RandomizationProvider.Current.GetDouble();

                Gene[] genes = chromosome.GetGenes();
                for (int i = 0; i < genes.Count(); i++)
                {
                    double geneValue = (double) genes[i].Value;

                    double lowerBound; double upperBound;
                    if (LowerBound == null) lowerBound = double.NegativeInfinity;
                    else lowerBound = LowerBound[i];
                    if (UpperBound == null) upperBound = double.PositiveInfinity;
                    else upperBound = UpperBound[i];

                    double delta = CalculateDelta(DistributionIndex, geneValue, lowerBound, upperBound, u);
                    if (chromosome is PrestressingChromosome) chromosome.ReplaceGene(i, new Gene(geneValue + delta * (((PrestressingChromosome) chromosome).MaxValues[i] - ((PrestressingChromosome) chromosome).MinValues[i])));
                    else chromosome.ReplaceGene(i, new Gene(geneValue + delta * (((DoubleChromosome) chromosome).MaxValues[i] - ((DoubleChromosome) chromosome).MinValues[i]))); //chromosome.ReplaceGene(i, new Gene(geneValue + delta * (geneValue - ((DoubleChromosome) chromosome).MinValues[i])));
                }
            }
        }

        public double CalculateDelta(double distributionIndex, double x, double lowerBound, double upperBound, double u)
        {
            double delta;
            if (double.IsNegativeInfinity(lowerBound) && double.IsPositiveInfinity(upperBound)) delta = 1;
            else if (lowerBound == upperBound) delta = 0;
            else delta = Math.Min(x - lowerBound, upperBound - x) / (upperBound - lowerBound);

            if (u <= 0.5)
            {
                return Math.Pow(2 * u + (1 - 2 * u) * Math.Pow(1 - delta, distributionIndex + 1), 1 / (distributionIndex + 1)) - 1;
            }
            else
            {
                return 1 - Math.Pow(2 * (1 - u) + 2 * (u - 0.5) * Math.Pow(1 - delta, distributionIndex + 1), 1 / (distributionIndex + 1));
            }
        }
    }
}