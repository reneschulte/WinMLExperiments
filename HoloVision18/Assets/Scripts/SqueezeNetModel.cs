

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
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.System.Threading;
using System.Threading;
using Windows.Media.Devices;
using Windows.Devices.Enumeration;
using System.Diagnostics;
using Windows.Media.SpeechSynthesis;
#endif // ENABLE_WINMD_SUPPORT

public class SqueezeNetModel
{
    public const string ModelFileName = "SqueezeNet.onnx";
    public const string LabelsFileName = "Labels.json";

    private List<string> _labels = new List<string>();
    private List<float> _outputVariableList = new List<float>();

#if ENABLE_WINMD_SUPPORT

    private ImageVariableDescriptorPreview _inputImageDescription;
    private TensorVariableDescriptorPreview _outputTensorDescription;
    private LearningModelPreview _model = null;

    private MediaCapture _captureManager;
    private VideoEncodingProperties _videoProperties;
    private ThreadPoolTimer _frameProcessingTimer;
    private SemaphoreSlim _frameProcessingSemaphore = new SemaphoreSlim(1);
    private SpeechSynthesizer _speechSynth;
#endif // ENABLE_WINMD_SUPPORT

    public async Task LoadModelAsync(bool isGpu = false)
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

            // Load Model via Unity
            // The WinML LearningModelPreview is 'not implemented' so we are using the trick to load via Unity Resource as txt file...
            var modelResource = Resources.Load(ModelFileName) as TextAsset;
            var modelBits = modelResource.bytes;

#if ENABLE_WINMD_SUPPORT
            IStorageFile modelFile = null;
            var fileName = "model.bin";

            try
            {
                modelFile = await ApplicationData.Current.TemporaryFolder.GetFileAsync(fileName);
            }
            catch (FileNotFoundException)
            {
            }
            if (modelFile == null)
            {
                modelFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName);
                await FileIO.WriteBytesAsync(modelFile, modelBits);
            }            

            // Init model
            _model = await LearningModelPreview.LoadModelFromStorageFileAsync(modelFile);
            _model.InferencingOptions.ReclaimMemoryAfterEvaluation = true;
            _model.InferencingOptions.PreferredDeviceKind = isGpu == true ? LearningModelDeviceKindPreview.LearningDeviceGpu : LearningModelDeviceKindPreview.LearningDeviceCpu;

            // Retrieve model input and output variable descriptions (we already know the model takes an image in and outputs a tensor)
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
}
