using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;

using static BridgeOpt.LayoutDefinition;

namespace BridgeOpt
{
    public class TendonNeuralNetwork
    {
        public Matrix<double> TrainingInputs;
        public Matrix<double> TrainingOutputs;
        public Matrix<double> TestInputs = null;
        public Matrix<double> TestOutputs = null;

        public int NumberOfHiddenUnits;
        public int MaxNumberOfEpochs;       
        public double LearnRate;

        public string Logs;

        //Inputs standarization:
        public Vector<double> NormInputAvg;
        public Vector<double> NormInputStd;
        public int NumberOfInputs;

        //Outputs standarization:
        public Vector<double> NormOutputAvg;
        public Vector<double> NormOutputStd;
        public int NumberOfOutputs;

        private readonly Stopwatch TimeElapsed;

        public TendonNeuralNetwork(AnalyticalBridge analyticalBridge)
        {
            foreach (PrestressingCaseResult result in analyticalBridge.PrestressingCaseResults)
            {
                if (result.DiscreteTendon.IsFullTendon())
                {
                    FullTendon fullTendon = result.DiscreteTendon.GetFullTendon();
                    double[] tendonOutputs = new double[result.MY.Count()];
                    foreach (double my in result.MY) tendonOutputs[result.MY.IndexOf(my)] = my;
                    AddMember(new double[5] { fullTendon.PointA.Z, fullTendon.PointB.X, fullTendon.PointB.Z, fullTendon.PointC.Z, fullTendon.SupportRadius }, tendonOutputs);
                }
                else if (result.DiscreteTendon.IsPartialTendon())
                {
                    PartialTendon partialTendon = result.DiscreteTendon.GetPartialTendon();
                    double[] tendonOutputs = new double[result.MY.Count()];
                    foreach (double my in result.MY) tendonOutputs[result.MY.IndexOf(my)] = my;
                    AddMember(new double[5] { partialTendon.PointA.Z, partialTendon.PointB.X, partialTendon.PointB.Z, partialTendon.PointC.Z, partialTendon.SupportRadius }, tendonOutputs);
                }
            }
            NumberOfHiddenUnits = int.Parse(analyticalBridge.PhysicalBridge.DataForm.DataFormHiddenUnits.Text);
            MaxNumberOfEpochs = int.Parse(analyticalBridge.PhysicalBridge.DataForm.DataFormMaxEpochs.Text);
            LearnRate = double.Parse(analyticalBridge.PhysicalBridge.DataForm.DataFormLearnRate.Text);

            double errorTreshold = double.Parse(analyticalBridge.PhysicalBridge.DataForm.DataFormErrorTreshold.Text);
            double error = errorTreshold + 1.000000;

            TimeElapsed = new Stopwatch();
            TimeElapsed.Start();
            Logs = analyticalBridge.PhysicalBridge.Directory + Globals.TextFiles.NeuralNetworkLogs;

            Initialize(int.Parse(analyticalBridge.PhysicalBridge.DataForm.DataFormTestSetSize.Text));
            int epochs = 100;
            for (int epoch = 0; epoch < MaxNumberOfEpochs; epoch += epochs)
            {
                if (error <= errorTreshold) break;
                if (TotalEpochs >= MaxNumberOfEpochs) break;
                error = Train(epochs, errorTreshold);
            }
            PrintSummary();
        }

        public TendonNeuralNetwork(Matrix<double> inputToHiddenWeights, Vector<double> inputToHiddenBiases, Matrix<double> hiddenToOutputWeights, Vector<double> hiddenToOutputBiases, Vector<double> normInputAvg, Vector<double> normInputStd, Vector<double> normOutputAvg, Vector<double> normOutputStd)
        {
            InputToHiddenWeights = inputToHiddenWeights;
            InputToHiddenBiases = inputToHiddenBiases;

            HiddenToOutputWeights = hiddenToOutputWeights;
            HiddenToOutputBiases = hiddenToOutputBiases;

            NormInputAvg = normInputAvg; NormOutputAvg = normOutputAvg;
            NormInputStd = normInputStd; NormOutputStd = normOutputStd;

            NumberOfHiddenUnits = inputToHiddenBiases.Count();
            NumberOfInputs = NormInputAvg.Count();
            NumberOfOutputs = NormOutputAvg.Count();
        }

        public void AddMember(double[] inputs, double[] outputs)
        {
            if (NumberOfInputs == 0)
            {
                NumberOfInputs = inputs.Count();
                NumberOfOutputs = outputs.Count();

                TrainingInputs = Matrix<double>.Build.Dense(1, NumberOfInputs, inputs);
                TrainingOutputs = Matrix<double>.Build.Dense(1, NumberOfOutputs, outputs);
            }
            else
            {
                if (NumberOfInputs != inputs.Count()) throw new Exception("Inconsistent number of inputs in the training set.");
                if (NumberOfOutputs != outputs.Count()) throw new Exception("Inconsistent number of outputs in the training set.");

                TrainingInputs = TrainingInputs.InsertRow(TrainingInputs.RowCount, Vector<double>.Build.DenseOfArray(inputs));
                TrainingOutputs = TrainingOutputs.InsertRow(TrainingOutputs.RowCount, Vector<double>.Build.DenseOfArray(outputs));
            }
        }

        public Matrix<double> Normalize(Matrix<double> values, Vector<double> avg, Vector<double> std)
        {
            for (int i = 0; i < values.RowCount; i++) values.SetRow(i, values.Row(i).Subtract(avg).PointwiseDivide(std));
            return values;
        }
        public Matrix<double> Denormalize(Matrix<double> values, Vector<double> avg, Vector<double> std)
        {
            for (int i = 0; i < values.RowCount; i++) values.SetRow(i, std.PointwiseMultiply(values.Row(i)).Add(avg));
            return values;
        }

        private Matrix<double> InputToHiddenWeights;  //Size: NumberOfInputs x NumberOfHiddenUnits
        private Vector<double> InputToHiddenBiases;   //Size: NumberOfHiddenUnits

        private Matrix<double> HiddenToOutputWeights; //Size: NumberOfHiddenUnits x NumberOfOutputs;
        private Vector<double> HiddenToOutputBiases;  //Size: NumberOfOutputs;
        public void Initialize(int testSetSize, double lowerBound = -0.001, double upperBound = 0.001)
        {
            Random rand = new Random();
            ContinuousUniform randomDistribution = new ContinuousUniform(lowerBound, upperBound, rand);

            InputToHiddenWeights = Matrix<double>.Build.Random(NumberOfInputs, NumberOfHiddenUnits, randomDistribution);
            HiddenToOutputWeights = Matrix<double>.Build.Random(NumberOfHiddenUnits, NumberOfOutputs, randomDistribution);

            InputToHiddenBiases = Vector<double>.Build.Random(NumberOfHiddenUnits, randomDistribution);
            HiddenToOutputBiases = Vector<double>.Build.Random(NumberOfOutputs, randomDistribution);

            ArrangeTestSet(testSetSize);
            NormalizeData();
            using (StreamWriter outputFile = new StreamWriter(Logs, false))
            {
                outputFile.WriteLine("Begin neural network regression. Tendon Layout");
                outputFile.WriteLine("Goal is to predict the function of bending moment M as f(TendonLayout).\n");
                if (TestInputs == null) outputFile.WriteLine("Data loaded: " + TrainingInputs.RowCount.ToString() + " examples in training set, 0 examples in test set.\n");
                else outputFile.WriteLine("Data loaded: " + TrainingInputs.RowCount.ToString() + " examples in training set, " + TestInputs.RowCount.ToString() + " examples in test set.\n");

                outputFile.WriteLine("Creating a " + NumberOfInputs.ToString() + "-" + NumberOfHiddenUnits.ToString() + "-" + NumberOfOutputs.ToString() + " regression neural network.");
                outputFile.WriteLine("Using tanh hidden layer activation. Function parameters normalized.\n");

                outputFile.WriteLine("Maximum number of epochs: " + MaxNumberOfEpochs.ToString());
                outputFile.WriteLine(string.Format("Learning rate: {0:0.000000}\n\n", LearnRate));

                outputFile.WriteLine("Outputs normalization:");
                for (int i = 0; i < TrainingOutputs.ColumnCount; i++) outputFile.WriteLine(string.Format("     Output {0}: avg = {1:0.000000}, std = {2:0.000000}", i + 1, NormOutputAvg.At(i), NormOutputStd.At(i)));
                outputFile.WriteLine("\nStarting training (using stochastic back-propagation):");
            }
        }

        private void ArrangeTestSet(int size)
        {
            if (size > TrainingInputs.RowCount - 1)
            {
                throw new Exception("Size of the training set is greater than total amount of members introduced to the model.");
            }

            if (size > 0)
            {
                TestInputs = Matrix<double>.Build.Dense(size, NumberOfInputs);
                TestOutputs = Matrix<double>.Build.Dense(size, NumberOfOutputs);

                Random rnd = new Random();
                for (int i = 1; i <= size; i++)
                {
                    int index = rnd.Next(TrainingInputs.RowCount);

                    TestInputs.SetRow(i - 1, TrainingInputs.Row(index).ToArray());
                    TestOutputs.SetRow(i - 1, TrainingOutputs.Row(index).ToArray());

                    TrainingInputs = TrainingInputs.RemoveRow(index);
                    TrainingOutputs = TrainingOutputs.RemoveRow(index);
                }
            }
        }

        private void NormalizeData()
        {
            NormInputAvg = Vector<double>.Build.Dense(NumberOfInputs);
            NormInputStd = Vector<double>.Build.Dense(NumberOfInputs);

            NormInputAvg = TrainingInputs.ColumnSums().Divide(TrainingInputs.RowCount);
            //NormInputStd = TrainingInputs.PointwisePower(2).ColumnSums().Divide(TrainingInputs.RowCount).Subtract(NormInputAvg.PointwisePower(2)).PointwiseSqrt();
            NormInputStd = TrainingInputs.PointwisePower(2).ColumnSums().Divide(TrainingInputs.RowCount).Subtract(NormInputAvg.PointwisePower(2)).Multiply((double) TrainingInputs.RowCount / (TrainingInputs.RowCount - 1)).PointwiseSqrt();
            TrainingInputs = Normalize(TrainingInputs, NormInputAvg, NormInputStd);

            NormOutputAvg = Vector<double>.Build.Dense(NumberOfOutputs);
            NormOutputStd = Vector<double>.Build.Dense(NumberOfOutputs);

            NormOutputAvg = TrainingOutputs.ColumnSums().Divide(TrainingOutputs.RowCount);
            //NormOutputStd = TrainingOutputs.PointwisePower(2).ColumnSums().Divide(TrainingOutputs.RowCount).Subtract(NormOutputAvg.PointwisePower(2)).PointwiseSqrt();
            NormOutputStd = TrainingOutputs.PointwisePower(2).ColumnSums().Divide(TrainingOutputs.RowCount).Subtract(NormOutputAvg.PointwisePower(2)).Multiply((double) TrainingOutputs.RowCount / (TrainingOutputs.RowCount - 1)).PointwiseSqrt();
            TrainingOutputs = Normalize(TrainingOutputs, NormOutputAvg, NormOutputStd);
        }

        public int TotalEpochs;
        public double Train(int epochs = 100, double errorTreshold = 0.0005)
        {
            double error = 0.0;
            for (int epoch = 0; epoch < epochs; epoch++)
            {
                if (TotalEpochs == MaxNumberOfEpochs) break;
                Matrix<double> hiddens = ComputeHiddenUnits(TrainingInputs, false);
                Matrix<double> outputs = PredictNormalized(TrainingInputs, false);

                for (int m = 0; m < TrainingInputs.RowCount; m++)
                {
                    Matrix<double> oSignals = TrainingOutputs - outputs;   //TrainingInputs size (m) x  NumberOfOutputs (3)
                    Matrix<double> oGrads = oSignals.Row(m).ToRowMatrix(); //1 x NumberOfOutputs (3)

                    //NumberOfHiddenUnits (15) x 1:
                    Matrix<double> hSignals = HiddenToOutputWeights.TransposeAndMultiply(oGrads).PointwiseMultiply(ComputeGradients(hiddens.Row(m).ToColumnMatrix()));
                    Matrix<double> hGrads = hSignals; //NumberOfHiddenUnits (15) x 1

                    //NumberOfHiddenUnits (15) x NumberOfOutputs (3):
                    Matrix<double> hoGrads = oGrads.TransposeThisAndMultiply(hiddens.Row(m).ToRowMatrix()).Transpose();
                    Matrix<double> ihGrads = (hGrads * TrainingInputs.Row(m).ToRowMatrix()).Transpose(); //NumberOfInputs (5) x NumberOfHiddenUnits (15)

                    InputToHiddenWeights += LearnRate * ihGrads;
                    HiddenToOutputWeights += LearnRate * hoGrads;

                    InputToHiddenBiases += LearnRate * hGrads.Column(0);
                    HiddenToOutputBiases += LearnRate * oGrads.Row(0);

                    hiddens = ComputeHiddenUnits(TrainingInputs, false);
                    outputs = PredictNormalized(TrainingInputs, false);
                }
                TotalEpochs += 1;

                error = ComputeError();
                if (error <= errorTreshold)
                {
                    using (StreamWriter outputFile = new StreamWriter(Logs, true))
                    {
                        outputFile.WriteLine("Threshold reached at epoch no. {0}, error: {1:0.000000} < {2:0.000000}\n", TotalEpochs, error, errorTreshold);
                    }
                    break;
                }
                else if (TotalEpochs == MaxNumberOfEpochs) break;
            }

            if (error > errorTreshold)
            {
                using (StreamWriter outputFile = new StreamWriter(Logs, true))
                {
                    outputFile.WriteLine("Epoch: {0}  Training error: {1:0.000000}", TotalEpochs, error);
                    if (TotalEpochs == MaxNumberOfEpochs) outputFile.WriteLine("");
                }
            }
            return error;
        }

        public Matrix<double> PredictNormalized(Matrix<double> inputs, bool normalizeInputs = false)
        {
            if (inputs.ColumnCount != NumberOfInputs) throw new Exception("Invalid size of input vector.");

            if (normalizeInputs) inputs = Normalize(inputs, NormInputAvg, NormInputStd);
            Matrix<double> outputs = ComputeHiddenUnits(inputs, false) * HiddenToOutputWeights;
            for (int row = 0; row < outputs.RowCount; row++) outputs.SetRow(row, outputs.Row(row).Add(HiddenToOutputBiases));
            
            return outputs;
        }
        public Matrix<double> PredictDenormalized(Matrix<double> inputs, bool normalizeInputs = false)
        {
            return Denormalize(PredictNormalized(inputs, normalizeInputs), NormOutputAvg, NormOutputStd);
        }
        public List<double> Predict(double[] inputs, bool normalizeInputs = true)
        {
            Matrix<double> inputsMatrix = Matrix<double>.Build.Dense(1, NumberOfInputs, inputs);
            Matrix<double> outputMatrix = PredictDenormalized(inputsMatrix, normalizeInputs);
            return outputMatrix.Row(0).ToList();
        }
        public List<double> Predict(DiscreteTendon tendon, bool normalizeInputs = true)
        {
            double[] inputs = new double[5];
            if (tendon.IsFullTendon())
            {
                FullTendon fullTendon = tendon.GetFullTendon();
                inputs[0] = fullTendon.PointA.Z;
                inputs[1] = fullTendon.PointB.X;
                inputs[2] = fullTendon.PointB.Z;
                inputs[3] = fullTendon.PointC.Z;
                inputs[4] = fullTendon.SupportRadius;
            }
            else if (tendon.IsPartialTendon())
            {
                PartialTendon partialTendon = tendon.GetPartialTendon();
                inputs[0] = partialTendon.PointA.Z;
                inputs[1] = partialTendon.PointB.X;
                inputs[2] = partialTendon.PointB.Z;
                inputs[3] = partialTendon.PointC.Z;
                inputs[4] = partialTendon.SupportRadius;
            }
            else return new List<double>();

            return Predict(inputs, normalizeInputs);
        }

        public Matrix<double> ComputeHiddenUnits(Matrix<double> inputs, bool normalizeInputs = false)
        {
            if (inputs.ColumnCount != NumberOfInputs) throw new Exception("Invalid size of input vector.");
            if (normalizeInputs) inputs = Normalize(inputs, NormInputAvg, NormInputStd);

            Matrix<double> hiddenUnits = inputs * InputToHiddenWeights;
            for (int row = 0; row < hiddenUnits.RowCount; row++) hiddenUnits.SetRow(row, hiddenUnits.Row(row).Add(InputToHiddenBiases));
            return hiddenUnits.PointwiseTanh();
        }

        public double ComputeError()
        {
            Matrix<double> outputs = PredictNormalized(TrainingInputs, false);
            return outputs.Subtract(TrainingOutputs).PointwisePower(2).ColumnSums().Sum() / TrainingInputs.RowCount;
        }

        public Matrix<double> ComputeGradients(Matrix<double> values)
        {
            //Tanh gradient:
            return 1 - values.PointwiseTanh().PointwisePower(2);
        }

        public void PrintSummary()
        {
            Matrix<double> predicted; int trainingItems = 10;
            using (StreamWriter outputFile = new StreamWriter(Logs, true))
            {
                if (TestInputs == null) outputFile.WriteLine("No test set definied.");
                else {
                    predicted = PredictDenormalized(TestInputs, true);
                    for (int i = 0; i < TestInputs.RowCount; i++)
                    {
                        for (int output = 0; output < NumberOfOutputs; output++)
                        {
                            double ratio;
                            if (Math.Max(Math.Abs(TestOutputs.At(i, output)), Math.Abs(predicted.At(i, output))) == 0) ratio = 1.000000;
                            else ratio = Math.Min(Math.Abs(TestOutputs.At(i, output)), Math.Abs(predicted.At(i, output))) / Math.Max(Math.Abs(TestOutputs.At(i, output)), Math.Abs(predicted.At(i, output)));

                            outputFile.WriteLine("Test set, item: {0}, output {1} (should be around {2:0.000000}): {3:0.000000}   ({4:0.000000})", i + 1, output + 1, TestOutputs.At(i, output), predicted.At(i, output), ratio);
                        }
                        outputFile.WriteLine("");
                    }
                    trainingItems = TestInputs.RowCount;
                }
                outputFile.WriteLine("");

                predicted = PredictDenormalized(TrainingInputs, false);
                TrainingOutputs = Denormalize(TrainingOutputs, NormOutputAvg, NormOutputStd);

                Random rnd = new Random();
                for (int i = 0; i < trainingItems; i++)
                {
                    int index = rnd.Next(TrainingInputs.RowCount);
                    for (int output = 0; output < NumberOfOutputs; output++)
                    {
                        double ratio;
                        if (Math.Max(Math.Abs(TrainingOutputs.At(index, output)), Math.Abs(predicted.At(index, output))) == 0) ratio = 1.000000;
                        else ratio = Math.Min(Math.Abs(TrainingOutputs.At(index, output)), Math.Abs(predicted.At(index, output))) / Math.Max(Math.Abs(TrainingOutputs.At(index, output)), Math.Abs(predicted.At(index, output)));

                        outputFile.WriteLine("Training set, item: {0}, output {1} (should be around {2:0.000000}): {3:0.000000}   ({4:0.000000})", i + 1, output + 1, TrainingOutputs.At(index, output), predicted.At(index, output), ratio);
                    }
                    outputFile.WriteLine("");
                }

                TimeElapsed.Stop();
                outputFile.WriteLine("Elapsed time: " + TimeElapsed.Elapsed.ToString());
                outputFile.WriteLine("End");
            }
        }
    }
}