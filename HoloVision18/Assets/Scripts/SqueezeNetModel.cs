// Based on WinML SqueezeNet sample from
// https://github.com/Microsoft/Windows-Machine-Learning

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

#if ENABLE_WINMD_SUPPORT
using Windows.AI.MachineLearning.Preview;
using Windows.Storage;
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
    private ImageVariableDescriptorPreview _inputImageDescription;
    private TensorVariableDescriptorPreview _outputTensorDescription;
    private LearningModelPreview _model = null;

    public ImageVariableDescriptorPreview InputDescription => _inputImageDescription;
    public TensorVariableDescriptorPreview OutputDescription => _outputTensorDescription;
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

            // Load Model via Unity
            // The WinML LearningModelPreview.LoadModelFromStreamAsync is 'not implemented' so we are using the trick to load via Unity Resource as txt file...
            // Thanks Mike for the hint! https://mtaulty.com/2018/03/29/third-experiment-with-image-classification-on-windows-ml-from-uwp-on-hololens-in-unity/
            IStorageFile modelFile = null;
            var fileName = "model.bytes";
            try
            {
                modelFile = await ApplicationData.Current.TemporaryFolder.GetFileAsync(fileName);
            }
            catch (FileNotFoundException)
            {
            }
            if (modelFile == null)
            {
                var modelResource = Resources.Load(ModelFileName) as TextAsset;
                modelFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName);
                await FileIO.WriteBytesAsync(modelFile, modelResource.bytes);
            }

            // Initialize model          
            _model = await LearningModelPreview.LoadModelFromStorageFileAsync(modelFile);
            _model.InferencingOptions.ReclaimMemoryAfterEvaluation = true;
            _model.InferencingOptions.PreferredDeviceKind =
                shouldUseGpu ? LearningModelDeviceKindPreview.LearningDeviceGpu : LearningModelDeviceKindPreview.LearningDeviceCpu;

            // Get model input and output descriptions
            List<ILearningModelVariableDescriptorPreview> inputFeatures = _model.Description.InputFeatures.ToList();
            List<ILearningModelVariableDescriptorPreview> outputFeatures = _model.Description.OutputFeatures.ToList();

            _inputImageDescription =
                inputFeatures.FirstOrDefault(feature => feature.ModelFeatureKind == LearningModelFeatureKindPreview.Image)
                as ImageVariableDescriptorPreview;

            _outputTensorDescription =
                outputFeatures.FirstOrDefault(feature => feature.ModelFeatureKind == LearningModelFeatureKindPreview.Tensor)
                as TensorVariableDescriptorPreview;
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

        // Bind the input and output buffer
        LearningModelBindingPreview binding = new LearningModelBindingPreview(_model as LearningModelPreview);
        binding.Bind(_inputImageDescription.Name, inputFrame);
        binding.Bind(_outputTensorDescription.Name, _outputVariableList);

        // Process the frame and get the results
        var stopwatch = Stopwatch.StartNew();
        LearningModelEvaluationResultPreview results = await _model.EvaluateAsync(binding, "Squeeze");
        stopwatch.Stop();
        List<float> resultProbabilities = results.Outputs[_outputTensorDescription.Name] as List<float>;

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