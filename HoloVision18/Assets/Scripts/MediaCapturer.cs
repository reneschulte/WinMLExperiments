using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

#if ENABLE_WINMD_SUPPORT
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Media.Devices;
using Windows.Graphics.Imaging;
using Windows.Devices.Enumeration;
#endif // ENABLE_WINMD_SUPPORT

public class MediaCapturer
{
#if ENABLE_WINMD_SUPPORT
    private MediaCapture _captureManager;
    private MediaFrameReader _frameReader;

    public async Task StartCapturing(BitmapPixelFormat pixelFormat = BitmapPixelFormat.Bgra8, uint width = 320, uint height = 240)
    {
        if (_captureManager == null ||
             _captureManager.CameraStreamState == CameraStreamState.Shutdown ||
             _captureManager.CameraStreamState == CameraStreamState.NotStreaming)
        {
            if (_captureManager != null)
            {
                _captureManager.Dispose();
            }

            // Find right camera settings and prefer back camera
            MediaCaptureInitializationSettings settings = new MediaCaptureInitializationSettings();
            var allCameras = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            var selectedCamera = allCameras.FirstOrDefault(c => c.EnclosureLocation?.Panel == Panel.Back) ?? allCameras.FirstOrDefault();
            if (selectedCamera != null)
            {
                settings.VideoDeviceId = selectedCamera.Id;
            }

            // Init capturer and Frame reader
            _captureManager = new MediaCapture();
            await _captureManager.InitializeAsync(settings);
            var frameSource = _captureManager.FrameSources.Where(source => source.Value.Info.SourceKind == MediaFrameSourceKind.Color).First();

            // Convert the pixel formats
            var subtype = MediaEncodingSubtypes.Bgra8;
            if (pixelFormat != BitmapPixelFormat.Bgra8)
            {
                throw new Exception($"Pixelformat {pixelFormat} not supported yet. Add conversion here");
            }

            // The overloads of CreateFrameReaderAsync with the format arguments will actually make a copy in FrameArrived
            _frameReader = await _captureManager.CreateFrameReaderAsync(frameSource.Value);
            _frameReader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;
            _frameReader.FrameArrived += OnFrameArrived;
            Interlocked.Exchange(ref _isProcessing, 0);

            await _frameReader.StartAsync();
        }
    }

    public event Func<VideoFrame, Task> ProcessFrame;
    private int _isProcessing;

    private async void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        if (ProcessFrame == null)
        {
            return;
        }
        if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0)
        {
            try
            {
                using (var frame = sender.TryAcquireLatestFrame())
                {
                    using (var videoFrame = frame?.VideoMediaFrame?.GetVideoFrame())
                    {
                        if (videoFrame != null)
                        {
                            await ProcessFrame(videoFrame);
                        }
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isProcessing, 0);
            }
        }
    }
#endif

    public async Task StopCapturing()
    {
#if ENABLE_WINMD_SUPPORT
        if (_captureManager != null && _captureManager.CameraStreamState != CameraStreamState.Shutdown)
        {
            _frameReader.FrameArrived -= OnFrameArrived;
            await _frameReader.StopAsync();
            _frameReader.Dispose();
            _captureManager.Dispose();
            _captureManager = null;
        }
#endif
    }
}