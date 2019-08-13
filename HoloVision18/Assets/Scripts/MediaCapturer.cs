using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

#if ENABLE_WINMD_SUPPORT
using Windows.Media;
using Windows.Media.Capture;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Storage.Pickers;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Media.Devices;
using Windows.Graphics.Imaging;
using Windows.Devices.Enumeration;
#endif // ENABLE_WINMD_SUPPORT

public class MediaCapturer
{
    public bool IsCapturing { get; set; }

#if ENABLE_WINMD_SUPPORT
    private MediaCapture _captureManager;
    private MediaFrameReader _frameReader;
    private VideoFrame _loadedVideoFrame;

    public async Task StartCapturing(uint width = 320, uint height = 240)
    {
        if (_captureManager == null || _captureManager.CameraStreamState == CameraStreamState.Shutdown || _captureManager.CameraStreamState == CameraStreamState.NotStreaming)
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
            //if (pixelFormat != BitmapPixelFormat.Bgra8)
            //{
            //    throw new Exception($"Pixelformat {pixelFormat} not supported yet. Add conversion here");
            //}

            // The overloads of CreateFrameReaderAsync with the format arguments will actually make a copy in FrameArrived
            BitmapSize outputSize = new BitmapSize { Width = width, Height = height };
            _frameReader = await _captureManager.CreateFrameReaderAsync(frameSource.Value, subtype, outputSize);
            _frameReader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;

            await _frameReader.StartAsync();
            IsCapturing = true;
        }
    }

    public VideoFrame GetLatestFrame()
    {
        // The overloads of CreateFrameReaderAsync with the format arguments will actually return a copy so we don't have to copy again
        var frame = _frameReader.TryAcquireLatestFrame();
        var videoFrame = frame?.VideoMediaFrame?.GetVideoFrame();
        return videoFrame;
    }

    public async Task<VideoFrame> GetTestFrame()
    {
        if (_loadedVideoFrame == null)
        {
            using (var resourceStream = new InMemoryRandomAccessStream())
            {
                var dataWriter = new DataWriter(resourceStream);
                var frameResource = Resources.Load("keyboard.jpg") as TextAsset;
                dataWriter.WriteBytes(frameResource.bytes);
                await dataWriter.StoreAsync();
                resourceStream.Seek(0);
               
                // Create the decoder from the stream 
                var decoder = await BitmapDecoder.CreateAsync(resourceStream);

                // Get the SoftwareBitmap representation of the file in BGRA8 format
                var softwareBitmap = await decoder.GetSoftwareBitmapAsync();
                softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

                // Encapsulate the image within a VideoFrame to be bound and evaluated
                _loadedVideoFrame = VideoFrame.CreateWithSoftwareBitmap(softwareBitmap);
            }
        }
        return _loadedVideoFrame;
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
        IsCapturing = false;
#endif
    }
}