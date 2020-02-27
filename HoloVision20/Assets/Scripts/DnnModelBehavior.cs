using HoloToolkit.Unity;
using System;
using System.Threading.Tasks;
using UnityEngine;

public class DnnModelBehavior : MonoBehaviour
{
    public bool GPU = false;

    private SqueezeNetModel _dnnModel;
    private MediaCapturer _mediaCapturer;
    private TextToSpeech _tts;
    private UserInput _user;

    private string _previousDominantResult;
    private bool _isRunning = false;

    public TextMesh StatusBlock;
    public float ProbabilityThreshold = 0.6f;

    async void Start()
    {
        try
        {
            // Get components
            _tts = GetComponent<TextToSpeech>();
            _user = GetComponent<UserInput>();

            // Load model
            StatusBlock.text = $"Loading {SqueezeNetModel.ModelFileName} ...";
            _dnnModel = new SqueezeNetModel();
            await _dnnModel.LoadModelAsync(GPU);
            StatusBlock.text = $"Loaded model. Starting camera...";

#if ENABLE_WINMD_SUPPORT
            // Configure camera to return frames fitting the model input size
            try
            {
                _mediaCapturer = new MediaCapturer();
                await _mediaCapturer.StartCapturing(
                    _dnnModel.InputWidth,
                    _dnnModel.InputHeight);
                StatusBlock.text = $"Camera started. Running!";
            }
            catch (Exception ex)
            {
                StatusBlock.text = $"Failed to start camera: {ex.Message}. Using loaded/picked image.";

            }
            // Load fallback frame if there's no camera like when testing with the emulator
            if (!_mediaCapturer.IsCapturing)
            {
                var loadedFrame = await _mediaCapturer.GetTestFrame();
            }

            // Run processing loop in separate parallel Task
            _isRunning = true;
            await Task.Run(async () =>
            {
                while (_isRunning)
                {
                    if (_mediaCapturer.IsCapturing)
                    {
                        using (var videoFrame = await _mediaCapturer.GetLatestFrame())
                        {
                            await EvaluateFrame(videoFrame);
                        }
                    }
                    // Use fallback if there's no camera like when testing with the emulator
                    else
                    {
                        var loadedFrame = await _mediaCapturer.GetTestFrame();
                        await EvaluateFrame(loadedFrame);
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

#if ENABLE_WINMD_SUPPORT
    private async Task EvaluateFrame(Windows.Media.VideoFrame videoFrame)
    {
        try
        {
            var result = await _dnnModel.EvaluateVideoFrameAsync(videoFrame);

            if (result.DominantResultProbability > 0)
            {
                // Further process and surface results to UI on the UI thread
                UnityEngine.WSA.Application.InvokeOnAppThread(() =>
                {
                    // Measure distance between user's head and gaze ray hit point => distance to object
                    var distMessage = string.Empty;
                    if (_user.GazeHitDistance < 1)
                    {
                        distMessage = string.Format("{0:f0} {1}", _user.GazeHitDistance * 100, "centimeter");
                    }
                    else
                    {
                        distMessage = string.Format("{0:f1} {1}", _user.GazeHitDistance, "meter");
                    }

                    // Prepare strings for text and update labels
                    var deviceKind = GPU ? "GPU" : "CPU";
                    var labelText = $"Predominant objects detected in {result.ElapsedMilliseconds,3:f0}ms on {deviceKind}\n {result.TopResultsFormatted}";
                    var speechText = string.Format("This {0} a {1} {2} in front of you", 
                        result.DominantResultProbability > ProbabilityThreshold ? "is likely" : "might be", 
                        result.DominantResultLabel, 
                        distMessage);
                    StatusBlock.text = labelText;

                    // Check if the previous result was the same and only progress further if not to avoid a loop of same audio
                    if (!_tts.IsSpeaking() && result.DominantResultLabel != _previousDominantResult)
                    {
                        _tts.StartSpeaking(speechText);
                        _previousDominantResult = result.DominantResultLabel;
                    }
                }, false);
            }
        }
        catch (Exception ex)
        {
            //IsRunning = false;
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

#endif

    private async void OnDestroy()
    {
        _isRunning = false;
        if (_mediaCapturer != null)
        {
            await _mediaCapturer.StopCapturing();
        }
    }
}