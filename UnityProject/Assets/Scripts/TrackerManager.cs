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
using System.Threading.Tasks;
using Windows.Storage.Streams;
#endif

public class TrackerManager : MonoBehaviour {
    private readonly List<ObjectTracker> _trackers = new List<ObjectTracker>();

    // Variable for Camera
    private Matrix4x4 _cameraToWorldMatrix = Matrix4x4.zero;
    private Matrix4x4 _projectionMatrix = Matrix4x4.zero;
    private byte[]    _latestImageBytes;

    private Resolution _resolution;

    // Variable for Server
    private bool _running;
#if !UNITY_EDITOR
    private StreamSocket _socket;
#endif

    private IntPtr       _spatialCoordinateSystemPtr;
    private VideoCapture _videoCapture;

    [Header("Server settings")] [Tooltip("The server IP")] [SerializeField]
    private string serverIp;

    [Tooltip("The server port")] [SerializeField]
    private string serverPort;

    public void registerTracker(ObjectTracker tracker) {
        _trackers.Add(tracker);
    }

#if !UNITY_EDITOR
    async
#endif
    private void Awake() {
        try {
#if !UNITY_EDITOR
            _socket = new StreamSocket();
            _socket.Control.NoDelay = true;
            _socket.Control.QualityOfService = SocketQualityOfService.LowLatency;

            await _socket.ConnectAsync(new HostName(serverIp), serverPort);
#endif
            Debug.Log("Connected");
            _running = true;
            OnStart(); // Because our async Awake will be finished after the real Start method of Unity
        } catch (Exception e) {
            Debug.LogError("Couldn't connect to the server! Exception: " + e.Message);
            _running = false;
        }
    }

    private void OnStart() {
        if (!_running)
            return;

        _spatialCoordinateSystemPtr = WorldManager.GetNativeISpatialCoordinateSystemPtr();

        CameraStreamHelper.Instance.GetVideoCaptureAsync(OnVideoCaptureCreated);

    #if !UNITY_EDITOR
        Task.Run(() => ReceiveData());
    #endif
    }

    private void OnDestroy() {
        Destroy();
    }

    private void OnApplicationQuit() {
        Destroy();
    }

#if !UNITY_EDITOR
    async
#endif
    private void Destroy() {
        if (_videoCapture != null)
        {
            _videoCapture.FrameSampleAcquired -= OnFrameSampleAcquired;
            _videoCapture.Dispose();
        }

        if (_running)
        {
        #if !UNITY_EDITOR //TODO CHANGE SEND END_OF_CONNECTION HAHHA
            await _socket.CancelIOAsync();
            _socket.Dispose();
            _socket = null;
            _running = false;
            Debug.Log("Closed");
        #endif
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

#if !UNITY_EDITOR
    async
#endif
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

        try {
        //    MemoryStream ms = new MemoryStream();
        //    BinaryWriter bw = new BinaryWriter(ms);
        //    bw.Write(_trackers.Count);

        //    foreach (ObjectTracker ot in _trackers) {
        //        bw.Write(ot.MinH);
        //        bw.Write(ot.MaxH);
        //    }

        //    bw.Write(_latestImageBytes.Length);
        //    bw.Write(_latestImageBytes);

        //    byte[] toSend = ms.ToArray();

        //#if !UNITY_EDITOR
        //    using (DataWriter writer = new DataWriter(_socket.OutputStream)) {
        //        writer.WriteInt32(toSend.Length);
        //        writer.WriteBytes(toSend);

        //        await writer.StoreAsync();

        //        writer.DetachStream();
        //        writer.Dispose();
        //    }
        //#endif
        }
        catch (Exception e) {
            Debug.Log("Error: " + e.Message);
        }

        Debug.Log("Sending request");
    }

#if !UNITY_EDITOR
    private async void ReceiveData() {
        using (DataReader reader = new DataReader(_socket.InputStream)) {

            while (_running) {
                reader.InputStreamOptions = InputStreamOptions.Partial;

                await reader.LoadAsync(reader.UnconsumedBufferLength);

                int number = reader.ReadInt32();
                
                for (int i = 0; i < number; i++) {
                    int posx = reader.ReadInt32();
                    int posy = reader.ReadInt32();

                    ReceivePosResult(i, posx, posy);
                }
            }
            
            reader.DetachStream();
            reader.Dispose();
        }
    }
#endif

    private void ReceivePosResult(int index, int posx, int posy) {
        Debug.Log("Index " + index + " POSX " + posx + " POSY " + posy);
    }
}