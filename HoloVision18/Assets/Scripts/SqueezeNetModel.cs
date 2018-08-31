// Based on WinML SqueezeNet sample from
// https://github.com/Microsoft/Windows-Machine-Learning

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

#if ENABLE_WINMD_SUPPORT
using Windows.AI.MachineLearning;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Media;
using System.Diagnostics;
#endif // ENABLE_WINMD_SUPPORT

public class SqueezeNetModel
{
    // Simple class to encapsulate the returned results
    public class SqueezeNetResult
    {
        public string TopResultsFormatted = "No results just yet";
        public string DominantResultLabel;
        public float DominantResultProbability;
        public long ElapsedMilliseconds;
    }

    public const string ModelFileName = "SqueezeNet.onnx";
    public const string LabelsFileName = "Labels.json";

    private List<string> _labels = new List<string>();
    private List<float> _outputVariableList = new List<float>();

#if ENABLE_WINMD_SUPPORT
    private LearningModel _model = null;
    private LearningModelSession _session;
    private TensorFeatureDescriptor _outputDescription;

    public uint InputWidth { get; private set; }
    public uint InputHeight { get; private set; }
#endif // ENABLE_WINMD_SUPPORT

    public async Task LoadModelAsync(bool shouldUseGpu = false)
    {
        try
        {
            // Parse labels from label file
            var labelsTextAsset = Resources.Load(LabelsFileName) as TextAsset;
            using (var streamReader = new StringReader(labelsTextAsset.text))
            {
                string line = "";
                char[] charToTrim = { '\"', ' ' };
                while (streamReader.Peek() >= 0)
                {
                    line = streamReader.ReadLine();
                    line.Trim(charToTrim);
                    var indexAndLabel = line.Split(':');
                    if (indexAndLabel.Count() == 2)
                    {
                        _labels.Add(indexAndLabel[1]);
                    }
                }
            }

#if ENABLE_WINMD_SUPPORT

            // Load from Unity Resources via awkward UWP streams and initialize model   
            using (var modelStream = new InMemoryRandomAccessStream())
            {
                var dataWriter = new DataWriter(modelStream);
                var modelResource = Resources.Load(ModelFileName) as TextAsset;
                dataWriter.WriteBytes(modelResource.bytes);
                await dataWriter.StoreAsync();
                var randomAccessStream = RandomAccessStreamReference.CreateFromStream(modelStream);

                _model = await LearningModel.LoadFromStreamAsync(randomAccessStream);
                var deviceKind = shouldUseGpu ? LearningModelDeviceKind.DirectX : LearningModelDeviceKind.Cpu;
                _session = new LearningModelSession(_model, new LearningModelDevice(deviceKind));
            }

            // Get model input and output descriptions
            var inputImageDescription =
                _model.InputFeatures.FirstOrDefault(feature => feature.Kind == LearningModelFeatureKind.Image)
                as ImageFeatureDescriptor;
            // Check if input is passed as image if not try to interpret it as generic tensor
            if (inputImageDescription != null)
            {
                InputWidth = inputImageDescription.Width;
                InputHeight = inputImageDescription.Height;
            }
            else
            {
                var inputTensorDescription =
                    _model.InputFeatures.FirstOrDefault(feature => feature.Kind == LearningModelFeatureKind.Tensor)
                    as TensorFeatureDescriptor;
                InputWidth = (uint)inputTensorDescription.Shape[3];
                InputHeight = (uint)inputTensorDescription.Shape[2];
            }

            _outputDescription =
                _model.OutputFeatures.FirstOrDefault(feature => feature.Kind == LearningModelFeatureKind.Tensor)
                as TensorFeatureDescriptor;
#endif
        }
        catch
        {
#if ENABLE_WINMD_SUPPORT
            _model = null;
#endif
            throw;
        }
    }

#if ENABLE_WINMD_SUPPORT
    public async Task<SqueezeNetResult> EvaluateVideoFrameAsync(VideoFrame inputFrame, int topResultsCount = 3)
    {
        // Sometimes on HL RS4 the D3D surface returned is null, so simply skip those frames
        if (inputFrame == null || (inputFrame.Direct3DSurface == null && inputFrame.SoftwareBitmap == null))
        {
            return new SqueezeNetResult
            {
                TopResultsFormatted = "No input frame",
                DominantResultLabel = "No input frame",
                DominantResultProbability = 0,
                ElapsedMilliseconds = 0
            };
        }

        // Bind the input
        var binding = new LearningModelBinding(_session);
        var imageTensor = ImageFeatureValue.CreateFromVideoFrame(inputFrame);
        binding.Bind("data_0", imageTensor);

        // Process the frame and get the results
        var stopwatch = Stopwatch.StartNew();
        var results = await _session.EvaluateAsync(binding, stopwatch.ElapsedMilliseconds.ToString());
        stopwatch.Stop();
        var resultTensor = results.Outputs[_outputDescription.Name] as TensorFloat;
        var resultProbabilities = resultTensor.GetAsVectorView();

        // Find and sort the result of the evaluation in the bound output (the top classes detected with the max confidence)
        var topProbabilities = new float[topResultsCount];
        var topProbabilityLabelIndexes = new int[topResultsCount];
        for (int i = 0; i < resultProbabilities.Count; i++)
        {
            for (int j = 0; j < topResultsCount; j++)
            {
                if (resultProbabilities[i] > topProbabilities[j])
                {
                    topProbabilityLabelIndexes[j] = i;
                    topProbabilities[j] = resultProbabilities[i];
                    break;
                }
            }
        }

        // Format the result strings and return results
        string message = string.Empty;
        for (int i = 0; i < topResultsCount; i++)
        {
            message += $"\n{topProbabilities[i] * 100,4:f0}% : { _labels[topProbabilityLabelIndexes[i]]} ";
        }
        var mainLabel = _labels[topProbabilityLabelIndexes[0]].Split(',')[0];
        return new SqueezeNetResult
        {
            TopResultsFormatted = message,
            DominantResultLabel = mainLabel,
            DominantResultProbability = topProbabilities[0],
            ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
        };
    }
#endif
}