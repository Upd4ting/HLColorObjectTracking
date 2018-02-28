using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

using HoloLensCameraStream;

using UnityEngine;
using UnityEngine.XR.WSA;

using Resolution = HoloLensCameraStream.Resolution;
using Debug = UnityEngine.Debug;

public class TrackerManager : MonoBehaviour {
    private readonly List<ObjectTracker> _trackers = new List<ObjectTracker>();

    // Variable for Camera
    private Matrix4x4 _cameraToWorldMatrix = Matrix4x4.zero;
    private Matrix4x4 _projectionMatrix    = Matrix4x4.zero;
    private byte[]    _latestImageBytes;

    private Resolution _resolution;

    private IntPtr       _spatialCoordinateSystemPtr;
    private VideoCapture _videoCapture;
    private bool _finished;

    [Header("Server settings")] [Tooltip("The server IP")] [SerializeField]
    private string serverIp;

    [Tooltip("The server port")] [SerializeField]
    private string serverPort;

    public void registerTracker(ObjectTracker tracker) {
        _trackers.Add(tracker);
    }

    private void Start() {
        _spatialCoordinateSystemPtr = WorldManager.GetNativeISpatialCoordinateSystemPtr();

        CameraStreamHelper.Instance.GetVideoCaptureAsync(OnVideoCaptureCreated);

        _finished = false;
    }

    private void OnDestroy() {
        Destroy();
    }

    private void OnApplicationQuit() {
        Destroy();
    }

    private void Destroy() {
        if (_videoCapture != null) {
            _videoCapture.FrameSampleAcquired -= OnFrameSampleAcquired;
            _videoCapture.Dispose();
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

        if (_projectionMatrix == Matrix4x4.zero || _cameraToWorldMatrix == Matrix4x4.zero) {
            float[] cameraToWorldMatrixAsFloat;
            float[] projectionMatrixAsFloat;
            if (sample.TryGetCameraToWorldMatrix(out cameraToWorldMatrixAsFloat) == false || sample.TryGetProjectionMatrix(out projectionMatrixAsFloat) == false) {
                Debug.Log("Failed to get camera to world or projection matrix");
                return;
            }

            _cameraToWorldMatrix = LocatableCameraUtils.ConvertFloatArrayToMatrix4x4(cameraToWorldMatrixAsFloat);
            _projectionMatrix    = LocatableCameraUtils.ConvertFloatArrayToMatrix4x4(projectionMatrixAsFloat);
        }

        if (_finished)
            _finished = false;
        else {
            return;
        }

        UnityEngine.WSA.Application.InvokeOnAppThread(() => {
            int slice = _latestImageBytes.Length / 8;
            List<Thread> workers = new List<Thread>();

            for (int i = 0; i < 8; i++) {
                Thread t = new Thread(new ParameterizedThreadStart());
            }
        }, false);
    }

    private void processImageThread() {

    }
}