//  
// Copyright (c) 2017 Vulcan, Inc. All rights reserved.  
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.
//
#if !UNITY_EDITOR && UNITY_WSA
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Media.Effects;
using Windows.Perception.Spatial;
using Windows.Foundation.Collections;
using Windows.Foundation;
using System.Diagnostics;


namespace HoloLensCameraStream
{
    /// <summary>
    /// Called when a VideoCapture resource has been created.
    /// If the instance failed to be created, the instance returned will be null.
    /// </summary>
    /// <param name="captureObject">The VideoCapture instance.</param>
    public delegate void OnVideoCaptureResourceCreatedCallback(VideoCapture captureObject);

    /// <summary>
    /// Called when the web camera begins streaming video.
    /// </summary>
    /// <param name="result">Indicates whether or not video recording started successfully.</param>
    /// <param name="reader">The reader that has been created</param>
    public delegate void OnVideoModeStartedCallback(VideoCaptureResult result);

    /// <summary>
    /// This is called every time there is a new frame sample available.
    /// See VideoCapture.FrameSampleAcquired and the VideoCaptureSample class for more information.
    /// </summary>
    /// <param name="videoCaptureSample">The recently captured frame sample.
    /// It contains methods for accessing the bitmap, as well as supporting information
    /// such as transform and projection matrices.</param>
    public delegate void FrameSampleAcquiredCallback(VideoCaptureSample videoCaptureSample);

    /// <summary>
    /// Called when video mode has been stopped.
    /// </summary>
    /// <param name="result">Indicates whether or not video mode was successfully deactivated.</param>
    public delegate void OnVideoModeStoppedCallback(VideoCaptureResult result);

    /// <summary>
    /// Streams video from the camera and makes the buffer available for reading.
    /// </summary>
    public sealed class VideoCapture
    {
        /// <summary>
        /// Note: This function is not yet implemented. Help us out on GitHub!
        /// There is an instance method on VideoCapture called GetSupportedResolutions().
        /// Please use that until we can get this method working.
        /// </summary>
        public static IEnumerable<Resolution> SupportedResolutions
        {
            get
            {
                throw new NotImplementedException("Please use the instance method VideoCapture.GetSupportedResolutions() for now.");
            }
        }

        /// <summary>
        /// Returns the supported frame rates at which a video can be recorded given a resolution.
        /// Use VideoCapture.SupportedResolutions to get the supported web camera recording resolutions.
        /// </summary>
        /// <param name="resolution">A recording resolution.</param>
        /// <returns>The frame rates at which the video can be recorded.</returns>
        public static IEnumerable<float> SupportedFrameRatesForResolution(Resolution resolution)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// This is called every time there is a new frame sample available.
        /// You must properly initialize the VideoCapture object, including calling StartVideoModeAsync()
        /// before this event will begin firing.
        /// 
        /// You should not subscribe to FrameSampleAcquired if you do not need access to most
        /// of the video frame samples for your application (for instance, if you are doing image detection once per second),
        /// because there is significant memory management overhead to processing every frame.
        /// Instead, you can call RequestNextFrameSample() which will respond with the next available sample only.
        /// 
        /// See the VideoFrameSample class for more information about dealing with the memory
        /// complications of the BitmapBuffer.
        /// </summary>
        public event FrameSampleAcquiredCallback FrameSampleAcquired;

        /// <summary>
        /// Indicates whether or not the VideoCapture instance is currently streaming video.
        /// This becomes true when the OnVideoModeStartedCallback is called, and ends 
        /// when the OnVideoModeStoppedCallback is called.
        /// 
        /// "VideoMode", as I have interpreted means that the frame reader begins delivering
        /// the bitmap buffer, making it available to be consumed.
        /// </summary>
        public bool IsStreaming { get { return Reader != null; } }

        internal SpatialCoordinateSystem worldOrigin { get; private set; }
        public IntPtr WorldOriginPtr
        {
            set
            {
                worldOrigin = (SpatialCoordinateSystem)Marshal.GetObjectForIUnknown(value);
            }
        }

        public MediaFrameSourceGroup FrameSourceGroup { get; set; }
        public MediaFrameSource FrameSource { get; set; }
        public MediaFrameReader Reader { get; set; }

        static readonly MediaStreamType STREAM_TYPE = MediaStreamType.VideoPreview;
        static readonly Guid ROTATION_KEY = new Guid("C380465D-2271-428C-9B83-ECEA3B4A85C1");

        DeviceInformation _deviceInfo;
        MediaCapture _mediaCapture;
        MediaFrameSourceInfo _mediaSourceInfo;
        
        VideoCapture(MediaFrameSourceGroup frameSourceGroup, MediaFrameSourceInfo mediaSourceInfo, DeviceInformation deviceInfo)
        {
            FrameSourceGroup   = frameSourceGroup;
            _deviceInfo         = deviceInfo;
            _mediaSourceInfo = mediaSourceInfo;
        }

        public static async void CreateAsync(OnVideoCaptureResourceCreatedCallback onCreatedCallback, SourceKind[] sourceKinds) {
            CreateAsync(onCreatedCallback, sourceKinds, SourceKind.NOTDEFINED);
        }

        /// <summary>
        /// Asynchronously creates an instance of a VideoCapture object that can be used to stream video frames from the camera to memory.
        /// If the instance failed to be created, the instance returned will be null. Also, holograms will not appear in the video.
        /// </summary>
        /// <param name="onCreatedCallback">This callback will be invoked when the VideoCapture instance is created and ready to be used.</param>
        /// <param name="sourceKinds">Frame source kinds that we want in the MediaFrameSourceGroup</param>
        /// <param name="readerSourceKind">The source kind we want into our reader</param>
        public static async void CreateAsync(OnVideoCaptureResourceCreatedCallback onCreatedCallback, SourceKind[] sourceKinds, SourceKind readerSourceKind)
        {
            // Convert
            List<MediaFrameSourceKind> list = new List<MediaFrameSourceKind>();
            foreach (SourceKind sk in sourceKinds) {
                list.Add(ConvertSourceKind(sk));
            }

            var frameSourceKinds = list.ToArray();

            var allFrameSourceGroups = await MediaFrameSourceGroup.FindAllAsync();                                              //Returns IReadOnlyList<MediaFrameSourceGroup>

            var selectedFrameSourceGroup = allFrameSourceGroups.FirstOrDefault(g => {
                foreach (MediaFrameSourceInfo si in g.SourceInfos) {
                    if (!frameSourceKinds.Contains(si.SourceKind) && (si.SourceKind == MediaFrameSourceKind.Color || 
                                                                      si.SourceKind == MediaFrameSourceKind.Depth ||
                                                                      si.SourceKind == MediaFrameSourceKind.Infrared)) {
                        Debug.WriteLine("Group refused: " + g.DisplayName);
                        Debug.WriteLine("Containing this that we don't want: " + si.SourceKind);
                        return false;
                    }
                }

                return true;
            });
            
            if (selectedFrameSourceGroup == null)
            {
                Debug.WriteLine("Group not found");
                onCreatedCallback?.Invoke(null);
                return;
            }

            MediaFrameSourceInfo selectedFrameSourceInfo = null;

            if (readerSourceKind == SourceKind.NOTDEFINED)
                selectedFrameSourceInfo = selectedFrameSourceGroup.SourceInfos.FirstOrDefault();
            else {
                foreach (MediaFrameSourceInfo si in selectedFrameSourceGroup.SourceInfos) {
                    if (si.SourceKind == ConvertSourceKind(readerSourceKind)) {
                        selectedFrameSourceInfo = si;
                        break;
                    }
                }
            }
            
            var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);   //Returns DeviceCollection
            var deviceInformation = devices.FirstOrDefault();                               //Returns a single DeviceInformation
            
            if (deviceInformation == null)
            {
                onCreatedCallback(null);
                return;
            }

            var videoCapture = new VideoCapture(selectedFrameSourceGroup, selectedFrameSourceInfo, deviceInformation);
            await videoCapture.CreateMediaCaptureAsync();
            onCreatedCallback?.Invoke(videoCapture);
        }

        public IEnumerable<Resolution> GetSupportedResolutions()
        {
            List<Resolution> resolutions = new List<Resolution>();

            var allPropertySets = _mediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(STREAM_TYPE).Select(x => x as VideoEncodingProperties); //Returns IEnumerable<VideoEncodingProperties>
            foreach (var propertySet in allPropertySets)
            {
                resolutions.Add(new Resolution((int)propertySet.Width, (int)propertySet.Height));
            }

            return resolutions.AsReadOnly();
        }

        public IEnumerable<float> GetSupportedFrameRatesForResolution(Resolution resolution)
        {
            //Get all property sets that match the supported resolution
            var allPropertySets = _mediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(STREAM_TYPE).Select((x) => x as VideoEncodingProperties)
                .Where((x) =>
            {
                return x != null &&
                x.Width == (uint)resolution.width &&
                x.Height == (uint)resolution.height;
            }); //Returns IEnumerable<VideoEncodingProperties>

            //Get all resolutions without duplicates.
            var frameRatesDict = new Dictionary<float, bool>();
            foreach (var propertySet in allPropertySets)
            {
                if (propertySet.FrameRate.Denominator != 0)
                {
                    float frameRate = (float)propertySet.FrameRate.Numerator / (float)propertySet.FrameRate.Denominator;
                    frameRatesDict.Add(frameRate, true);
                }
            }

            //Format resolutions as a list.
            var frameRates = new List<float>();
            foreach (KeyValuePair<float, bool> kvp in frameRatesDict)
            {
                frameRates.Add(kvp.Key);
            }

            return frameRates.AsReadOnly();
        }

        /// <summary>
        /// Asynchronously starts video mode.
        /// 
        /// Activates the web camera with the various settings specified in CameraParameters.
        /// Only one VideoCapture instance can start the video mode at any given time.
        /// After starting the video mode, you listen for new video frame samples via the VideoCapture.FrameSampleAcquired event, 
        /// or by calling VideoCapture.RequestNextFrameSample() when will return the next available sample.
        /// While in video mode, more power will be consumed so make sure that you call VideoCapture.StopVideoModeAsync qhen you can afford the start/stop video mode overhead.
        /// </summary>
        /// <param name="frameSourceInfo">Which frame we want to get the stream of</param>
        /// <param name="setupParams">Parameters that change how video mode is used.</param>
        /// <param name="onVideoModeStartedCallback">This callback will be invoked once video mode has been activated.</param>
        public async void StartVideoModeAsync(bool useRawFormat, CameraParameters setupParams, OnVideoModeStartedCallback onVideoModeStartedCallback)
        {
            var mediaFrameSource = _mediaCapture.FrameSources[_mediaSourceInfo.Id]; //Returns a MediaFrameSource

            FrameSource = mediaFrameSource;

            if (mediaFrameSource == null)
            {
                onVideoModeStartedCallback?.Invoke(new VideoCaptureResult(1, ResultType.UnknownError, false));
                return;
            }

            if (!useRawFormat) {
                var pixelFormat = ConvertCapturePixelFormatToMediaEncodingSubtype(setupParams.pixelFormat);
                Reader = await _mediaCapture.CreateFrameReaderAsync(mediaFrameSource, pixelFormat);
            } else {
                Reader = await _mediaCapture.CreateFrameReaderAsync(mediaFrameSource);
            }

            Reader.FrameArrived += HandleFrameArrived;
            await Reader.StartAsync();

            if (!useRawFormat) {
                VideoEncodingProperties properties = GetVideoEncodingPropertiesForCameraParams(setupParams);

                // Historical context: https://github.com/VulcanTechnologies/HoloLensCameraStream/issues/6
                if (setupParams.rotateImage180Degrees)
                {
                    properties.Properties.Add(ROTATION_KEY, 180);
                }

                //	gr: taken from here https://forums.hololens.com/discussion/2009/mixedrealitycapture
                IVideoEffectDefinition ved = new VideoMRCSettings(setupParams.enableHolograms, setupParams.enableVideoStabilization, setupParams.videoStabilizationBufferSize, setupParams.hologramOpacity);
                await _mediaCapture.AddVideoEffectAsync(ved, MediaStreamType.VideoPreview);

                await _mediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(STREAM_TYPE, properties);
            }

            onVideoModeStartedCallback?.Invoke(new VideoCaptureResult(0, ResultType.Success, true));
        }

        /// <summary>
        /// Returns a new VideoFrameSample as soon as the next one is available.
        /// This method is preferable to listening to the FrameSampleAcquired event
        /// in circumstances where most or all frames are not needed. For instance, if
        /// you were planning on sending frames to a remote image recognition service twice per second,
        /// you may consider using this method rather than ignoring most of the event dispatches from FrameSampleAcquired.
        /// This will avoid the overhead of acquiring and disposing of unused frames.
        /// 
        /// If, for whatever reason, a frame reference cannot be obtained, it is possible that the callback will return a null sample.
        /// </summary>
        /// <param name="onFrameSampleAcquired"></param>
        public void RequestNextFrameSample(FrameSampleAcquiredCallback onFrameSampleAcquired)
        {
            if (onFrameSampleAcquired == null)
            {
                throw new ArgumentNullException("onFrameSampleAcquired");
            }

            if (IsStreaming == false)
            {
                throw new Exception("You cannot request a frame sample until the video mode is started.");
            }

            TypedEventHandler<MediaFrameReader, MediaFrameArrivedEventArgs> handler = null;
            handler = (MediaFrameReader sender, MediaFrameArrivedEventArgs args) => {
                using (MediaFrameReference frameReference = sender.TryAcquireLatestFrame()) {
                    if (frameReference != null)
                    {
                        onFrameSampleAcquired.Invoke(new VideoCaptureSample(frameReference, worldOrigin));
                    }
                    else
                    {
                        onFrameSampleAcquired.Invoke(null);
                    }
                }
                Reader.FrameArrived -= handler;
            };
            Reader.FrameArrived += handler;
        }

        /// <summary>
        /// Asynchronously stops video mode.
        /// </summary>
        /// <param name="onVideoModeStoppedCallback">This callback will be invoked once video mode has been deactivated.</param>
        public async void StopVideoModeAsync(OnVideoModeStoppedCallback onVideoModeStoppedCallback)
        {
            if (IsStreaming == false)
            {
                onVideoModeStoppedCallback?.Invoke(new VideoCaptureResult(1, ResultType.InappropriateState, false));
                return;
            }

            Reader.FrameArrived -= HandleFrameArrived;
            await Reader.StopAsync();
            Reader.Dispose();

            onVideoModeStoppedCallback?.Invoke(new VideoCaptureResult(0, ResultType.Success, true));
        }

        /// <summary>
        /// Dispose must be called to shutdown the PhotoCapture instance.
        /// 
        /// If your VideoCapture instance successfully called VideoCapture.StartVideoModeAsync,
        /// you must make sure that you call VideoCapture.StopVideoModeAsync before disposing your VideoCapture instance.
        /// </summary>
        public void Dispose()
        {
            if (IsStreaming)
            {
                throw new Exception("Please make sure StopVideoModeAsync() is called before displosing the VideoCapture object.");
            }
            
            _mediaCapture?.Dispose();
        }

        async Task CreateMediaCaptureAsync()
        {
            if (_mediaCapture != null)
            {
                throw new Exception("The MediaCapture object has already been created.");
            }

            _mediaCapture = new MediaCapture();
            await _mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings()
            {
                VideoDeviceId = _deviceInfo.Id,
                SourceGroup = FrameSourceGroup,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu, //TODO: Should this be the other option, Auto? GPU is not an option.
                StreamingCaptureMode = StreamingCaptureMode.Video
            });
            _mediaCapture.VideoDeviceController.Focus.TrySetAuto(true);
        }

        void HandleFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            if (FrameSampleAcquired == null)
            {
                return;
            }

            using (MediaFrameReference frameReference = sender.TryAcquireLatestFrame()) {
                if (frameReference != null)
                {
                    var sample = new VideoCaptureSample(frameReference, worldOrigin);
                    FrameSampleAcquired?.Invoke(sample);
                }
            }
        }

        VideoEncodingProperties GetVideoEncodingPropertiesForCameraParams(CameraParameters cameraParams)
        {
            var allPropertySets = _mediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(STREAM_TYPE).Select((x) => x as VideoEncodingProperties)
                .Where((x) =>
            {
                if (x == null) return false;
                if (x.FrameRate.Denominator == 0) return false;

                double calculatedFrameRate = (double)x.FrameRate.Numerator / (double)x.FrameRate.Denominator;
                
                return
                x.Width == (uint)cameraParams.cameraResolutionWidth &&
                x.Height == (uint)cameraParams.cameraResolutionHeight &&
                (int)Math.Round(calculatedFrameRate) == cameraParams.frameRate;
            }); //Returns IEnumerable<VideoEncodingProperties>

            if (allPropertySets.Count() == 0)
            {
                throw new Exception("Could not find an encoding property set that matches the given camera parameters.");
            }
            
            var chosenPropertySet = allPropertySets.FirstOrDefault();
            return chosenPropertySet;
        }

        static string ConvertCapturePixelFormatToMediaEncodingSubtype(CapturePixelFormat format)
        {
            switch (format)
            {
                case CapturePixelFormat.BGRA32:
                    return MediaEncodingSubtypes.Bgra8;
                case CapturePixelFormat.NV12:
                    return MediaEncodingSubtypes.Nv12;
                case CapturePixelFormat.JPEG:
                    return MediaEncodingSubtypes.Jpeg;
                case CapturePixelFormat.PNG:
                    return MediaEncodingSubtypes.Png;
                default:
                    return MediaEncodingSubtypes.Bgra8;
            }
        }

        static MediaFrameSourceKind ConvertSourceKind(SourceKind sk) {
            switch (sk) {
                case SourceKind.COLOR:
                    return MediaFrameSourceKind.Color;
                case SourceKind.DEPTH:
                    return MediaFrameSourceKind.Depth;
                case SourceKind.INFRARED:
                    return MediaFrameSourceKind.Infrared;
                default:
                    return MediaFrameSourceKind.Custom;
            }
        }
    }


	//	from https://forums.hololens.com/discussion/2009/mixedrealitycapture
	public class VideoMRCSettings : IVideoEffectDefinition
    {
        public string ActivatableClassId
        {
            get
            {
                return "Windows.Media.MixedRealityCapture.MixedRealityCaptureVideoEffect";
            }
        }

        public IPropertySet Properties
        {
            get; private set;
        }
        
        public VideoMRCSettings(bool HologramCompositionEnabled, bool VideoStabilizationEnabled, int VideoStabilizationBufferLength, float GlobalOpacityCoefficient)
        {
            Properties = (IPropertySet)new PropertySet();
            Properties.Add("HologramCompositionEnabled", HologramCompositionEnabled);
            Properties.Add("VideoStabilizationEnabled", VideoStabilizationEnabled);
            Properties.Add("VideoStabilizationBufferLength", VideoStabilizationBufferLength);
            Properties.Add("GlobalOpacityCoefficient", GlobalOpacityCoefficient);
        }
    }
}
#endif