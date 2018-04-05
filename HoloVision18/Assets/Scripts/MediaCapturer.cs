using System;
using System.Linq;
using System.Threading.Tasks;

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
            _frameReader = await _captureManager.CreateFrameReaderAsync(frameSource.Value, subtype, new BitmapSize { Width = width, Height = height });
            _frameReader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;

            await _frameReader.StartAsync();
        }
    }

    public VideoFrame GetLatestFrame()
    {
        // The overloads of CreateFrameReaderAsync with the format arguments will actually return a copy so we dont'have to worry about creating another copy here
        var frame = _frameReader.TryAcquireLatestFrame();
        var videoFrame = frame?.VideoMediaFrame?.GetVideoFrame();
        return videoFrame;
    }
#endif

    public async Task StopCapturing()
    {
#if ENABLE_WINMD_SUPPORT
        if (_captureManager != null && _captureManager.CameraStreamState != CameraStreamState.Shutdown)
        {
            await _frameReader.StopAsync();
            _frameReader.Dispose();
            _captureManager.Dispose();
            _captureManager = null;
        }
#endif
    }
}