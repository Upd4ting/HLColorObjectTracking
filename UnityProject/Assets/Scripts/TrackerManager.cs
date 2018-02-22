using System;
using System.Collections.Generic;

using HoloLensCameraStream;

using LiteNetLib;

using UnityEngine;
using UnityEngine.XR.WSA;

using Resolution = HoloLensCameraStream.Resolution;

public class TrackerManager : MonoBehaviour {
    private readonly List<ObjectTracker> _trackers = new List<ObjectTracker>();
    private          Matrix4x4           _cameraToWorldMatrix;

    // Variable for Camera
    private byte[]    _latestImageBytes;
    private Matrix4x4 _projectionMatrix;

    private Resolution _resolution;

    // Variable for Server
    private NetPeer _serverPeer;

    private IntPtr       _spatialCoordinateSystemPtr;
    private bool         _succeed;
    private VideoCapture _videoCapture;

    [Tooltip("The connection key")] [SerializeField]
    private readonly string key = "ConnectionKey";

    [Header("Server settings")] [Tooltip("The server IP")] [SerializeField]
    private string serverIp;

    [Tooltip("The server port")] [SerializeField]
    private int serverPort;

    public void registerTracker(ObjectTracker tracker) {
        _trackers.Add(tracker);
    }

    private void OnDestroy() {
        if (_videoCapture != null) {
            _videoCapture.FrameSampleAcquired -= OnFrameSampleAcquired;
            _videoCapture.Dispose();
        }
    }

    private void Awake() {
        try {
            EventBasedNetListener listener = new EventBasedNetListener();
            NetManager            client   = new NetManager(listener, key);
            client.Start();
            _serverPeer = client.Connect(serverIp, serverPort);
            listener.NetworkReceiveEvent += (fromPeer, dataReader) => {
                int posx = dataReader.GetInt();
                int posy = dataReader.GetInt();

                Debug.Log("POSX " + posx + " POSY " + posy);
            };

            _succeed = true;
        } catch (Exception e) {
            Debug.LogError("Couldn't connect to the server! Exception: " + e.Message);
            _succeed = false;
        }
    }

    private void Start() {
        if (!_succeed)
            return;

        _spatialCoordinateSystemPtr = WorldManager.GetNativeISpatialCoordinateSystemPtr();

        CameraStreamHelper.Instance.GetVideoCaptureAsync(OnVideoCaptureCreated);
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


        if (_projectionMatrix == null || _cameraToWorldMatrix == null) {
            float[] cameraToWorldMatrixAsFloat;
            float[] projectionMatrixAsFloat;
            if (sample.TryGetCameraToWorldMatrix(out cameraToWorldMatrixAsFloat) == false || sample.TryGetProjectionMatrix(out projectionMatrixAsFloat) == false) {
                Debug.Log("Failed to get camera to world or projection matrix");
                return;
            }

            _cameraToWorldMatrix = LocatableCameraUtils.ConvertFloatArrayToMatrix4x4(cameraToWorldMatrixAsFloat);
            _projectionMatrix    = LocatableCameraUtils.ConvertFloatArrayToMatrix4x4(projectionMatrixAsFloat);
        }

        Request request = new Request(_latestImageBytes);

        foreach (ObjectTracker ot in _trackers) {
            Request.ObjectRequest or = new Request.ObjectRequest();
            or.minH = ot.MinH;
            or.maxH = ot.MaxH;
            request.ORequests.Add(or);
        }

        byte[] toSend = request.GetByteArray();

        _serverPeer.Send(toSend, SendOptions.ReliableOrdered);
    }
}