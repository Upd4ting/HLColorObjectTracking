using System;
using System.Collections;
using System.Collections.Generic;
using HoloLensCameraStream;
using HoloToolkit.Unity;

using UnityEngine;
using UnityEngine.XR.WSA;

using Application = UnityEngine.WSA.Application;

public class TrackerManager : Singleton<TrackerManager> {

    [Header("Server settings")]
    [Tooltip("The server IP")]
    [SerializeField] private string serverIp;
    [Tooltip("The server port")]
    [SerializeField] private int serverPort;

    private List<ObjectTracker> trackers = new List<ObjectTracker>();

    // Variable for Camera
    private byte[]     _latestImageBytes;
    private HoloLensCameraStream.Resolution _resolution;

    private IntPtr       _spatialCoordinateSystemPtr;
    private VideoCapture _videoCapture;

    public void registerTracker(ObjectTracker tracker) {
        trackers.Add(tracker);
    }

    private void Start()
    {
        //Fetch a pointer to Unity's spatial coordinate system if you need pixel mapping
        _spatialCoordinateSystemPtr = WorldManager.GetNativeISpatialCoordinateSystemPtr();

        //Call this in Start() to ensure that the CameraStreamHelper is already "Awake".
        CameraStreamHelper.Instance.GetVideoCaptureAsync(OnVideoCaptureCreated);

        //TODO Start UDP server
    }

    protected override void OnDestroy()
    {
        if (_videoCapture != null)
        {
            _videoCapture.FrameSampleAcquired -= OnFrameSampleAcquired;
            _videoCapture.Dispose();
        }

        base.OnDestroy();
    }

    private void OnVideoCaptureCreated(VideoCapture videoCapture)
    {
        if (videoCapture == null)
        {
            Debug.LogError("Did not find a video capture object. You may not be using the HoloLens.");
            return;
        }

        _videoCapture = videoCapture;

        //Request the spatial coordinate ptr if you want fetch the camera and set it if you need to 
        CameraStreamHelper.Instance.SetNativeISpatialCoordinateSystemPtr(_spatialCoordinateSystemPtr);

        _resolution = CameraStreamHelper.Instance.GetLowestResolution();
        float frameRate = CameraStreamHelper.Instance.GetHighestFrameRate(_resolution);
        videoCapture.FrameSampleAcquired += OnFrameSampleAcquired;

        CameraParameters cameraParams = new CameraParameters();
        cameraParams.cameraResolutionHeight = _resolution.height;
        cameraParams.cameraResolutionWidth = _resolution.width;
        cameraParams.frameRate = Mathf.RoundToInt(frameRate);
        cameraParams.pixelFormat = CapturePixelFormat.BGRA32;
        cameraParams.rotateImage180Degrees = false;
        cameraParams.enableHolograms = false;

        videoCapture.StartVideoModeAsync(cameraParams, OnVideoModeStarted);
    }

    private void OnVideoModeStarted(VideoCaptureResult result)
    {
        if (result.success == false)
        {
            Debug.LogWarning("Could not start video mode.");
            return;
        }

        Debug.Log("Video capture started.");
    }

    private void OnFrameSampleAcquired(VideoCaptureSample sample)
    {
        if (_latestImageBytes == null || _latestImageBytes.Length < sample.dataLength) _latestImageBytes = new byte[sample.dataLength];
        sample.CopyRawImageDataIntoBuffer(_latestImageBytes);

        float[] cameraToWorldMatrixAsFloat;
        float[] projectionMatrixAsFloat;
        if (sample.TryGetCameraToWorldMatrix(out cameraToWorldMatrixAsFloat) == false || sample.TryGetProjectionMatrix(out projectionMatrixAsFloat) == false)
        {
            Debug.Log("Failed to get camera to world or projection matrix");
            return;
        }

        Matrix4x4 cameraToWorldMatrix = LocatableCameraUtils.ConvertFloatArrayToMatrix4x4(cameraToWorldMatrixAsFloat);
        Matrix4x4 projectionMatrix = LocatableCameraUtils.ConvertFloatArrayToMatrix4x4(projectionMatrixAsFloat);

        
        Application.InvokeOnAppThread(() =>
        {
            //TODO REQUEST UDP
        }, false);
    }
}