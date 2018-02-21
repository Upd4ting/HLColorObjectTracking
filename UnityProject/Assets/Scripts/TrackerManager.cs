using System;
using System.Collections.Generic;
using System.IO;

using HoloLensCameraStream;

using UnityEngine;
using UnityEngine.XR.WSA;

using Resolution = HoloLensCameraStream.Resolution;
#if !UNITY_EDITOR
using Windows.Networking.Sockets;
using Windows.Networking;
using Windows.Storage.Streams;
#endif

public class TrackerManager : MonoBehaviour {
    // Variable for Server
#if !UNITY_EDITOR
        DatagramSocket _socket;
    #endif
    private bool _succeed;

    // Variable for Camera
    private byte[]     _latestImageBytes;
    private Resolution _resolution;

    private IntPtr    _spatialCoordinateSystemPtr;
    private Matrix4x4 _cameraToWorldMatrix;
    private Matrix4x4 _projectionMatrix;

    private readonly List<ObjectTracker> _trackers = new List<ObjectTracker>();
    private          VideoCapture        _videoCapture;

    [Header("Server settings")] [Tooltip("The server IP")] [SerializeField]
    private string serverIp;

    [Tooltip("The server port")] [SerializeField]
    private string serverPort;

    public void registerTracker(ObjectTracker tracker) {
        _trackers.Add(tracker);
    }

    private void OnDestroy() {
        if (_videoCapture != null) {
            _videoCapture.FrameSampleAcquired -= OnFrameSampleAcquired;
            _videoCapture.Dispose();
        }
    }

#if !UNITY_EDITOR
    private async void Awake() {
        try {
            _socket = new DatagramSocket();
            _socket.Control.InboundBufferSizeInBytes = 1048576;
            _socket.MessageReceived += Socket_MessageReceived;
            await _socket.ConnectAsync(new HostName(serverIp), serverPort);
            _succeed = true;
        } catch (Exception e) {
            Debug.LogError("Couldn't connect to the server! Exception: " + e.Message);
            _succeed = false;
        }
    }
#endif

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

#if !UNITY_EDITOR
        SendMessage(toSend);
#endif
    }

#if !UNITY_EDITOR
    private async void SendMessage(byte[] toSend)
    {
        DataWriter writer = new DataWriter(_socket.OutputStream);
        writer.WriteBytes(toSend);
        await writer.StoreAsync();
        Debug.Log("Sending");
    }

    private async void Socket_MessageReceived(Windows.Networking.Sockets.DatagramSocket sender, Windows.Networking.Sockets.DatagramSocketMessageReceivedEventArgs args)
    {
        Stream streamIn = args.GetDataStream().AsStreamForRead();
        BinaryReader reader = new BinaryReader(streamIn);

        int posx = reader.ReadInt32();
        int posy = reader.ReadInt32();

        Debug.Log("POSX " + posx + " POSY " + posy);
    }
#endif
}