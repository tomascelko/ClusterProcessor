﻿using Accord.Math;
using Accord.Neuro;
using Accord.Neuro.Learning;
using Accord.Neuro.Networks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
namespace ClassifierForClusters
{
    [Serializable]
    struct Interval
    {
        public double Min { get; set; }
        public double Max { get; set; }
        public double Size => Max - Min;

        public Interval(double min, double max)
        {
            Min = min;
            Max = max;
        }

    }
    /*class SoftMaxFunction : IActivationFunction
    {
        public double IActivationFunction(double value)
        {
            return  Special.Softmax(value);
        }
    }
    */
    class NNInputProcessor
    {
        public double[] NormalizeElements(double[] originVector, Interval[] intervalVector)
        {
            double[] newVector = new double[originVector.Length];
            for (int i = 0; i < originVector.Length; i++)
            {
                newVector[i] =  (originVector[i] - intervalVector[i].Min) / (intervalVector[i].Size);
            }
            return newVector;
        }
        public Interval[] CalculateNormIntervals(string jsonPath, string[] usableKeys, string[] classes)
        {
            StreamReader sReader = new StreamReader(jsonPath);
            JsonTextReader jReader = new JsonTextReader(sReader);
            var squeezeIntervals = new Interval[usableKeys.Length];
            for (int i = 0; i < squeezeIntervals.Length; i++)
            {
                squeezeIntervals[i] = new Interval(double.MaxValue, double.MinValue);
            }
            while (sReader.Peek() != -1)
            {
                var numVector = ReadTrainJsonToVector(jReader, usableKeys, classes, out int classIndex);
                if (classIndex == -1)
                    continue;
                for(int j = 0; j < numVector.Length;j++)
                {
                    if (numVector[j] < squeezeIntervals[j].Min)
                        squeezeIntervals[j].Min = numVector[j];
                    if (numVector[j] > squeezeIntervals[j].Max)
                        squeezeIntervals[j].Max = numVector[j];

                }
            }
            return squeezeIntervals;
        }
        public double[] ReadTrainJsonToVector(JsonTextReader reader, string[] usableKeys, string[] classes, out int classIndex)
        {

            if (reader.Read())
            {
                if (reader.TokenType == JsonToken.StartObject)
                {

                    List<double> resultVector = new List<double>();
                    JObject jsonRecord = JObject.Load(reader);
                    foreach (var usableKey in usableKeys)
                    {
                        double attributeVal;
                        if (!jsonRecord.ContainsKey(usableKey))
                            throw new ArgumentException($"Error, Required property \"{usableKey}\" for training set is not included in given input file");

                        if (double.TryParse(jsonRecord[usableKey].ToString(), out attributeVal))
                        {
                            resultVector.Add(attributeVal);
                        }

                    }
                    const string ClassKey = "Class";
                    if (!jsonRecord.ContainsKey(ClassKey))
                        throw new ArgumentException($"Error, Required \"{ClassKey}\" property is not included in given input file");

                    classIndex = Array.IndexOf(classes, jsonRecord[ClassKey].ToString());
                    if (classIndex < 0)
                        throw new ArgumentException($"Error, Class value: \"{jsonRecord[ClassKey]}\" is not valid class value");
                    return resultVector.ToArray();
                }
            }
            classIndex = -1;
            return null;
        }
        public double[] ReadJsonToVector(JsonTextReader reader, string[] usableKeys)
        {

            if (reader.Read())
            {
                if (reader.TokenType == JsonToken.StartObject)
                {

                    List<double> resultVector = new List<double>();
                    JObject jsonRecord = JObject.Load(reader);
                    foreach (var usableKey in usableKeys)
                    {
                        double attributeVal;
                        if (!jsonRecord.ContainsKey(usableKey))
                            throw new ArgumentException($"Error, Required property \"{usableKey}\" for training set is not included in given input file");

                        if (double.TryParse(jsonRecord[usableKey].ToString(), out attributeVal))
                        {
                            resultVector.Add(attributeVal);
                        }

                    }

                    return resultVector.ToArray();
                }
            }
            return null;
        }
    }
    class NNClassifier
    {
        DeepBeliefNetwork Network { get; set; }
        public Interval[] SqueezeIntervals { get; set; }
        private NNClassifier() { }
        public NNClassifier(int inputLen, int outputLen, int[] layerSizes, IActivationFunction activationFunction, Interval[] squeezeIntervals)
        {
            var layerNewSizes = layerSizes.Append(outputLen).ToArray();
            Network = new DeepBeliefNetwork(
                 inputLen,
                 layerNewSizes
                 );
            Network.SetActivationFunction(activationFunction);
            SqueezeIntervals = squeezeIntervals;
            
                    
        }
        public double Learn(int epochSize, string learnJsonPath, string[] validProperties, string []outputClasses, double successThreshold)
        {
            var inputStream = new StreamReader(learnJsonPath);
            var jsonStream = new JsonTextReader(inputStream);
            
            NNInputProcessor preprocessor = new NNInputProcessor();
            //empty read
            var _inputVector = preprocessor.ReadTrainJsonToVector(jsonStream, validProperties, outputClasses, out int _classIndex);
            DeepNeuralNetworkLearning teacher = new DeepNeuralNetworkLearning(Network);
            NguyenWidrow weightInit = new NguyenWidrow(Network);
            //weightInit.Randomize();
            teacher.Algorithm = (activationNetwork, index) =>
            {

                var backProp = new BackPropagationLearning(activationNetwork);
                backProp.Momentum = 0.5;
                backProp.LearningRate = 0.6;
                return backProp;
            };
            teacher.LayerCount = Network.Layers.Length;
            teacher.LayerIndex = 0;

            var iteration = 0;
            double errorSum = 0;
            while (inputStream.BaseStream.Position < inputStream.BaseStream.Length * 0.5)
            {
                double[][] input = new double[epochSize][];
                double[][] output = new double[epochSize][];

                for (int j = 0; j < epochSize; j++)
                {
                    var inputVector = preprocessor.NormalizeElements(preprocessor.ReadTrainJsonToVector(jsonStream, validProperties, outputClasses, out int classIndex), SqueezeIntervals);
                    var outputVector = new double[outputClasses.Length];
                    outputVector[classIndex] = 1;
                    input[j] = inputVector;
                    output[j] = outputVector;
                }
                // run epoch of learning procedure
                double error = teacher.RunEpoch(input, output);
                errorSum += error;
                if (iteration % 50 == 49)
                {
                    Console.WriteLine(errorSum);
                    errorSum = 0;
                }
                iteration++;
            }
            int[][] confusionMatrix = new int[outputClasses.Length][];
            for (int j = 0; j < outputClasses.Length; j++)
            {
                confusionMatrix[j] = new int[outputClasses.Length];
            }
            inputStream = new StreamReader(learnJsonPath);
            jsonStream = new JsonTextReader(inputStream);
            _inputVector = preprocessor.ReadTrainJsonToVector(jsonStream, validProperties, outputClasses, out int ___classIndex);
            while (inputStream.BaseStream.Position < inputStream.BaseStream.Length * 0.5)
            {
                var unNormalizedVect = preprocessor.ReadTrainJsonToVector(jsonStream, validProperties, outputClasses, out int classIndex);
                var inputVector = preprocessor.NormalizeElements(unNormalizedVect, SqueezeIntervals);
                var outputVector = new double[outputClasses.Length];
                outputVector[classIndex] = 1;

                var result = Network.Compute(inputVector);
                var predictedClass = result.ArgMax();
                confusionMatrix[predictedClass][classIndex]++;

            }
            var totalSum = 0;
            var diagSum = 0;
            for (int j = 0; j < outputClasses.Length; j++)
            {

                for (int k = 0; k < outputClasses.Length; k++)
                {
                    Console.Write(confusionMatrix[j][k]);
                    Console.Write("\t");
                    totalSum += confusionMatrix[j][k];
                    if (j == k)
                        diagSum += confusionMatrix[j][k];


                }
                Console.WriteLine();
            }
            var successRate = diagSum / (double)totalSum;
            Console.WriteLine("Success rate is : " + successRate);
            if (successRate > successThreshold)
                StoreToFile(learnJsonPath + "trained "+ successRate +".txt");
            return successRate;
        }
        public void StoreToFile(string outJsonPath)
        {
            Network.Save(outJsonPath);

            StreamWriter writer = new StreamWriter(outJsonPath + "_intervals");
            writer.Write(System.Text.Json.JsonSerializer.Serialize(SqueezeIntervals));
            writer.Close();
        }
        public static NNClassifier LoadFromFile(string inJsonPath)
        {
            var classifier = new NNClassifier();
            classifier.Network = (DeepBeliefNetwork) DeepBeliefNetwork.Load(inJsonPath);
            classifier.SqueezeIntervals = new Interval[classifier.Network.InputsCount];
            StreamReader sReader = new StreamReader(inJsonPath + "_intervals");
            JsonTextReader jReader = new JsonTextReader(sReader);
            jReader.Read();
            
            for (int i = 0; i < classifier.SqueezeIntervals.Length; i++)
            {
                jReader.Read();
                JObject intervalRecord = JObject.Load(jReader);
                classifier.SqueezeIntervals[i] = new Interval((double)intervalRecord["Min"], (double)intervalRecord["Max"]);
            }
            jReader.Close();
            sReader.Close();
            return classifier;
        }
        public int ClassifySingle(double[] inputVector)
        {
            NNInputProcessor preprocessor = new NNInputProcessor();
            var resultVector = Network.Compute(preprocessor.NormalizeElements(inputVector,SqueezeIntervals));
            return resultVector.ArgMax();
        }
        
    }
    public class MultiLayeredClassifier
    {
        NNClassifier NNFragFeHe = NNClassifier.LoadFromFile("../../../../ClusterDescriptionGen/bin/Debug/BESTtrainFragHeFe.jsontrained 0.940.txt");
        NNClassifier NNPrLe_ElMuPi = NNClassifier.LoadFromFile("../../../../ClusterDescriptionGen/bin/Debug/BESTtrainPrLe_ElMuPi.jsontrained 0.966.txt");
        NNClassifier NNLead= NNClassifier.LoadFromFile("../../../../ClusterDescriptionGen/bin/Debug/BESTtrainLeadMulti.jsontrained 0.894.txt");
        NNClassifier NNElMuPi = NNClassifier.LoadFromFile("../../../../ClusterDescriptionGen/bin/Debug/BESTtrainElMuPi.jsontrained 0.802.txt");
        NNInputProcessor preprocessor = new NNInputProcessor();
        public MultiLayeredClassifier(int classesCount)
        {

        }
        public int Classify(double[] inputVector)
        {
            var resultIndex = NNLead.ClassifySingle(inputVector.Take(inputVector.Length).ToArray());
            if (resultIndex == 1)
                return 0;
            resultIndex = NNFragFeHe.ClassifySingle(inputVector);
            if (resultIndex == 0)
                    return 1;
            if (resultIndex == 1)
                return 2;
            if (resultIndex == 2)
                return 4;
            resultIndex = NNPrLe_ElMuPi.ClassifySingle(inputVector);
            switch (resultIndex)
            {
                case 0:
                    return 3;
                case 2:
                    return 5;
                default:
                    break;
                
            }
            resultIndex = NNElMuPi.ClassifySingle(inputVector);
            if (resultIndex == 0)
                return 6;
            if (resultIndex == 1)
                return 7;
            if (resultIndex == 2)
                return 8;

             return 9;
        }
    }
    class Program
    {
        static Random rand = new Random();
        static string[] validFields = new string[]{
                 "TotalEnergy",
                 "AverageEnergy",
                 "MaxEnergy",
                 "PixelCount",
                 "Convexity",
                 "Width",
                 "CrosspointCount",
                 "VertexCount",
                 "RelativeHaloSize",
                 "BranchCount",
                 "StdOfEnergy",
                "StdOfArrival",
                "RelLowEnergyPixels"
                 };
        static string[] outputClasses = new string[] {
            "lead",
            "frag",       
            "he",
            "proton",
            "fe",
            "low_electr",
            "muon",
            "electron",        
            "pion",
            "elPi0"
        };
        static double LearnFrag(double acceptableSuccessRate)
        {
            var jsonFilePath = "../../../../ClusterDescriptionGen/bin/Debug/trainFragHeFe.json";
            var outputClasses = new string[] {
                 "frag",
                 "he",
                 "fe",
                 "other"
             };
            var inputStream = new StreamReader(jsonFilePath);
            NNInputProcessor preprocessor = new NNInputProcessor();
            var commonRareIntervals = preprocessor.CalculateNormIntervals(jsonFilePath, validFields, outputClasses);

            var epochSize = 4;
            NNClassifier fragClassifier = new NNClassifier(validFields.Length, outputClasses.Length, new int[] { 10, 10}, new RectifiedLinearFunction(), commonRareIntervals);
            double success = 0;
            success = fragClassifier.Learn(epochSize, jsonFilePath, validFields, outputClasses, acceptableSuccessRate);
            if (success > acceptableSuccessRate)
                return success;
            success = fragClassifier.Learn(epochSize, jsonFilePath, validFields, outputClasses, acceptableSuccessRate);
            if (success > acceptableSuccessRate)
                return success;
            success = fragClassifier.Learn(epochSize, jsonFilePath, validFields, outputClasses, acceptableSuccessRate);
            if (success > acceptableSuccessRate)
                return success;
            success = fragClassifier.Learn(epochSize, jsonFilePath, validFields, outputClasses, acceptableSuccessRate);
            if (success > acceptableSuccessRate)
                return success;
            return fragClassifier.Learn(epochSize, jsonFilePath, validFields, outputClasses, acceptableSuccessRate);
        }
        static double LearnLead(double acceptableSuccessRate)
        {
            
            string jsonFilePath = "../../../../ClusterDescriptionGen/bin/Debug/trainLeadMulti.json";
            string[] outputClasses = new string[] {
                 "he",
                 "lead",
                 "fe",
                 "frag",
                 "other"
             };

            StreamReader inputStream = new StreamReader(jsonFilePath);           
            NNInputProcessor preprocessor = new NNInputProcessor();
            Interval[] commonRareIntervals = preprocessor.CalculateNormIntervals(jsonFilePath, validFields, outputClasses);                
            int epochSize = 32;
            NNClassifier commonRareClassifier = new NNClassifier(validFields.Length, outputClasses.Length, new int[] { 13, 13}, new SigmoidFunction(1), commonRareIntervals);
            return commonRareClassifier.Learn( epochSize, jsonFilePath, validFields, outputClasses, acceptableSuccessRate);
        }
        static double LearnPrLe_ElMuPi(double acceptableSuccessRate)
        {

            string jsonFilePath = "../../../../ClusterDescriptionGen/bin/Debug/trainPrLe_ElMuPi.json";
            string[] outputClasses = new string[] {
                 "proton",
                 "elMuPi",
                 "low_electr",
            };

            StreamReader inputStream = new StreamReader(jsonFilePath);
            

            NNInputProcessor preprocessor = new NNInputProcessor();
            Interval[] commonRareIntervals = preprocessor.CalculateNormIntervals(jsonFilePath, validFields, outputClasses);
                                                                                                                       
                                                                                                                       // loop
            int epochSize = 6;
            NNClassifier multiClassifier = new NNClassifier(validFields.Length, outputClasses.Length, new int[] { 13, 13 }, new SigmoidFunction(1), commonRareIntervals);
            return multiClassifier.Learn(epochSize, jsonFilePath, validFields, outputClasses, acceptableSuccessRate);
        }
        static double LearnElMuPi(double acceptableSuccessRate)
        {
            
            string jsonFilePath = "../../../../ClusterDescriptionGen/bin/Debug/trainElMuPi.json";
            string[] outputClasses = new string[] {
                 "muon",
                 "electron",
                 "pion",
                 "elPi0"
            };
            int epochSize = 8;
            NNInputProcessor preprocessor = new NNInputProcessor();
            Interval[] elMuPiIntervals = preprocessor.CalculateNormIntervals(jsonFilePath, validFields, outputClasses);//{ new Interval(0, 500), new Interval(0, 70), new Interval(0, 150), new Interval(0, 120), new Interval(0, 1),
                                                                                                                       //new Interval(0, 70), new Interval(0, 10), new Interval(0, 10), new Interval(0,1), new Interval(0,5) };
            NNClassifier multiClassifier = new NNClassifier(validFields.Length, outputClasses.Length, new int[] { 10, 10 }, new SigmoidFunction(1), elMuPiIntervals);
            multiClassifier.Learn(epochSize, jsonFilePath, validFields, outputClasses, acceptableSuccessRate);
            return multiClassifier.Learn(epochSize, jsonFilePath, validFields, outputClasses, acceptableSuccessRate);
        }
        static double LearnAll(double acceptableSuccessRate)
        {

            string jsonFilePath = "../../../../ClusterDescriptionGen/bin/Debug/allTestData2.json";

            StreamReader inputStream = new StreamReader(jsonFilePath);
            NNInputProcessor preprocessor = new NNInputProcessor();


            var commonRareIntervals = preprocessor.CalculateNormIntervals(jsonFilePath, validFields, outputClasses);
                // { new Interval(0, 20000), new Interval(0, 100), new Interval(0, 600), new Interval(0, 1000), new Interval(0, 1),
                                         // new Interval(0, 200), new Interval(0, 50), new Interval(0, 30), new Interval(0,1), new Interval(0,30) };

            // loop
            int epochSize = 256;
            NNClassifier commonRareClassifier = new NNClassifier(validFields.Length, outputClasses.Length, new int[] { 16, 16 }, new SigmoidFunction(1), commonRareIntervals);
            return commonRareClassifier.Learn(epochSize, jsonFilePath, validFields, outputClasses, acceptableSuccessRate);
        }
        static void TestModel()
        {
            var testJsonPath = "../../../../ClusterDescriptionGen/bin/Debug/testAllProportional.json";
            int[][] ConfusionMatrix = ConfusionMatrix = new int[outputClasses.Length][];
            for (int i = 0; i < outputClasses.Length; i++)
            {
                ConfusionMatrix[i] = new int[outputClasses.Length];
            }
            StreamReader inputStream = new StreamReader(testJsonPath);
            var jsonStream = new JsonTextReader(inputStream);
            NNInputProcessor preprocessor = new NNInputProcessor();
            var _inputVector = preprocessor.ReadTrainJsonToVector(jsonStream, validFields, outputClasses, out int _classIndex);
            MultiLayeredClassifier classifier = new MultiLayeredClassifier(outputClasses.Length);
            int[] classesCounts = new int[outputClasses.Length];
            int c = 0;
            while (inputStream.Peek() != -1 && inputStream.BaseStream.Position < 1 * inputStream.BaseStream.Length/* && c < 3000*/)
            {
                var inputVector = preprocessor.ReadTrainJsonToVector(jsonStream, validFields, outputClasses, out int classIndex);
                var resultIndex = classifier.Classify(inputVector);
                if (/*inputStream.BaseStream.Position > 0.5 * inputStream.BaseStream.Length && classesCounts[classIndex] < 300*/true)
                {
                    ConfusionMatrix[classIndex][resultIndex]++;
                    classesCounts[classIndex]++;
                    c++;
                }

            }
            var rowSums = ConfusionMatrix.Select(x => x.Sum()).ToArray();
            var totalSum = 0;
            var diagSum = 0;
            for (int j = 0; j < outputClasses.Length; j++)
            {

                for (int k = 0; k < outputClasses.Length; k++)
                {
                    Console.Write(Math.Truncate(1000d * ConfusionMatrix[j][k] / (double)rowSums[j]) / 10d);
                    Console.Write("\t");
                    totalSum += ConfusionMatrix[j][k];
                    if (j == k)
                        diagSum += ConfusionMatrix[j][k];


                }
                Console.WriteLine();
            }
            var successRate = diagSum / (double)totalSum;
            Console.WriteLine("Success rate is : " + successRate);
        }

        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
            double maxSuccess = 0.80;
           /* for (int i = 0; i < 30; i++)
            {
                var success = LearnLead(maxSuccess);
                if (success > maxSuccess)
                    maxSuccess = success;
            }*/
            TestModel();

        }
    }
}


