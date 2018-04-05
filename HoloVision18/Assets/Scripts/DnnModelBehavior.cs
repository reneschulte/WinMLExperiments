using HoloToolkit.Unity;
using System;
using System.Threading.Tasks;
using UnityEngine;

public class DnnModelBehavior : MonoBehaviour
{
    private SqueezeNetModel _dnnModel;
    private MediaCapturer _mediaCapturer;

    public TextMesh StatusBlock;
    public float ProbabilityThreshold = 0.6f;
    public bool IsRunning = false;

    async void Start()
    {
        StatusBlock.text = $"Loading {SqueezeNetModel.ModelFileName} ...";

        try
        {
            // Get components
            var tts = GetComponent<TextToSpeech>();

            // Load model
            _dnnModel = new SqueezeNetModel();
            await _dnnModel.LoadModelAsync(false);
            StatusBlock.text = $"Loaded model. Starting camera...";

#if ENABLE_WINMD_SUPPORT
            // Configure camera to return frames fitting the model input size
            _mediaCapturer = new MediaCapturer();
            await _mediaCapturer.StartCapturing(_dnnModel.InputDescription.BitmapPixelFormat, _dnnModel.InputDescription.Width, _dnnModel.InputDescription.Height);
            StatusBlock.text = $"Camera started. Running!";

            // Run processing loop in separate parallel Task
            IsRunning = true;
            await Task.Run(async () =>
            {
                while (IsRunning)
                {
                    using (var videoFrame = _mediaCapturer.GetLatestFrame())
                    {
                        try
                        {
                            var result = await _dnnModel.EvaluateVideoFrameAsync(videoFrame);
                            if (result.DominantResultProbability > 0)
                            {
                                // Process results
                                var labelText = $"Predominant objects detected at {1000f / result.ElapsedMilliseconds,4:f1} fps\n {result.TopResultsFormatted}";
                                var speechText = string.Format("This {0} a {1}", result.DominantResultProbability > ProbabilityThreshold ? "is likely" : "might be", result.DominantResultLabel);

                                // Surface results to UI
                                UnityEngine.WSA.Application.InvokeOnAppThread(() =>
                                {
                                    StatusBlock.text = labelText;
                                    if (!tts.IsSpeaking())
                                    {
                                        tts.StartSpeaking(speechText);
                                    }
                                }, false);
                            }
                        }
                        catch (Exception ex)
                        {
                            UnityEngine.WSA.Application.InvokeOnAppThread(() =>
                            {
                                //StatusBlock.text = $"Error loop: {ex.Message}";
                                //Debug.LogError(ex);
                                //Debug.LogError(videoFrame.Direct3DSurface == null ? "D3D null" : "D3D set");
                                //if (videoFrame.Direct3DSurface != null)
                                //{
                                //    Debug.LogError(videoFrame.Direct3DSurface.Description.Format);
                                //    Debug.LogError(videoFrame.Direct3DSurface.Description.Width);
                                //    Debug.LogError(videoFrame.Direct3DSurface.Description.Height);
                                //}
                            }, false);
                        }
                    }
                }
            });
#endif
        }
        catch (Exception ex)
        {
            StatusBlock.text = $"Error init: {ex.Message}";
            Debug.LogError(ex);
        }
    }

    private async void OnDestroy()
    {
        IsRunning = false;
        await _mediaCapturer.StopCapturing();
    }
}