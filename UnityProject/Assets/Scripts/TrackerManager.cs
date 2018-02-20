using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

using HoloLensCameraStream;

using HoloToolkit.Unity;

using UnityEngine;
using UnityEngine.XR.WSA;

using Application = UnityEngine.WSA.Application;
using Resolution = HoloLensCameraStream.Resolution;

public class TrackerManager : Singleton<TrackerManager> {
    // Variable for Server
    private UdpClient _client;

    // Variable for Camera
    private byte[]     _latestImageBytes;
    private Resolution _resolution;

    private IntPtr _spatialCoordinateSystemPtr;

    private readonly List<ObjectTracker> _trackers = new List<ObjectTracker>();
    private          VideoCapture        _videoCapture;

    [Header("Server settings")] [Tooltip("The server IP")] [SerializeField]
    private string serverIp;

    [Header("Server settings")] [Tooltip("The server port")] [SerializeField]
    private int serverPort;

    public void registerTracker(ObjectTracker tracker) {
        _trackers.Add(tracker);
    }

    protected override void OnDestroy() {
        if (_videoCapture != null) {
            _videoCapture.FrameSampleAcquired -= OnFrameSampleAcquired;
            _videoCapture.Dispose();
        }

        base.OnDestroy();
    }

    private void Start() {
        try {
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);

            _client.Connect(ep);

            _spatialCoordinateSystemPtr = WorldManager.GetNativeISpatialCoordinateSystemPtr();

            CameraStreamHelper.Instance.GetVideoCaptureAsync(OnVideoCaptureCreated);
        } catch (Exception e) {
            Debug.LogError("Couldn't connect to the server! Exception: " + e.Message);
        }
    }

    private void OnVideoCaptureCreated(VideoCapture videoCapture) {
        if (videoCapture == null) {
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
        cameraParams.cameraResolutionWidth  = _resolution.width;
        cameraParams.frameRate              = Mathf.RoundToInt(frameRate);
        cameraParams.pixelFormat            = CapturePixelFormat.BGRA32;
        cameraParams.rotateImage180Degrees  = false;
        cameraParams.enableHolograms        = false;

        videoCapture.StartVideoModeAsync(cameraParams, OnVideoModeStarted);
    }

    private void OnVideoModeStarted(VideoCaptureResult result) {
        if (result.success == false) {
            Debug.LogWarning("Could not start video mode.");
            return;
        }

        Debug.Log("Video capture started.");
    }

    private void OnFrameSampleAcquired(VideoCaptureSample sample) {
        if (_latestImageBytes == null || _latestImageBytes.Length < sample.dataLength) _latestImageBytes = new byte[sample.dataLength];
        sample.CopyRawImageDataIntoBuffer(_latestImageBytes);

        float[] cameraToWorldMatrixAsFloat;
        float[] projectionMatrixAsFloat;
        if (sample.TryGetCameraToWorldMatrix(out cameraToWorldMatrixAsFloat) == false || sample.TryGetProjectionMatrix(out projectionMatrixAsFloat) == false) {
            Debug.Log("Failed to get camera to world or projection matrix");
            return;
        }

        Matrix4x4 cameraToWorldMatrix = LocatableCameraUtils.ConvertFloatArrayToMatrix4x4(cameraToWorldMatrixAsFloat);
        Matrix4x4 projectionMatrix    = LocatableCameraUtils.ConvertFloatArrayToMatrix4x4(projectionMatrixAsFloat);


        Application.InvokeOnAppThread(() => {
            //TODO REQUEST UDP
        }, false);
    }
}