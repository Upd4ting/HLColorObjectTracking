﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using HoloLensCameraStream;

using UnityEngine;
using UnityEngine.XR.WSA;

using Resolution = HoloLensCameraStream.Resolution;
using Debug = UnityEngine.Debug;
using System.Collections;
using System.Threading.Tasks;

#if !UNITY_EDITOR
using Windows.Networking.Sockets;
using Windows.Networking;
using Windows.Storage.Streams;
#endif

public class TrackerManager : MonoBehaviour {
    private readonly List<ObjectTracker> _trackers = new List<ObjectTracker>();

    // Variable for Camera
    private Matrix4x4 _cameraToWorldMatrix = Matrix4x4.zero;
    private Matrix4x4 _projectionMatrix    = Matrix4x4.zero;
    private byte[]    _latestImageBytes;
    private byte[] _png;
    private Texture2D _texture2D;

    private Resolution _resolution;

    // Variable for Server
    private bool         _running;
    private MemoryStream _ms;
    private BinaryWriter _bw;
    private BinaryReader _br;
    private long         _lastTimestamp = -1;
#if !UNITY_EDITOR
    private StreamSocket _socket;
    private DataWriter _writer;
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

            _br = new BinaryReader(_socket.InputStream.AsStreamForRead());
            _writer = new DataWriter(_socket.OutputStream);
            _writer.ByteOrder = ByteOrder.BigEndian;
#endif
            _ms = new MemoryStream();
            _bw = new BinaryWriter(_ms);

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

        //Task.Run(() => ReceiveData());

        _spatialCoordinateSystemPtr = WorldManager.GetNativeISpatialCoordinateSystemPtr();

        CameraStreamHelper.Instance.GetVideoCaptureAsync(OnVideoCaptureCreated);
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
        if (_videoCapture != null) {
            _videoCapture.FrameSampleAcquired -= OnFrameSampleAcquired;
            _videoCapture.Dispose();
        }

        if (_running) {

        #if !UNITY_EDITOR
            SendBytes(System.Text.Encoding.ASCII.GetBytes("END_OF_CONNECTION"), false);

            _writer.DetachStream();
            _writer.Dispose();

            await _socket.CancelIOAsync();
            _socket.Dispose();
            _socket = null;
         #endif

            _running = false;
            Debug.Log("Closed");
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

        _texture2D = new Texture2D(_resolution.width, _resolution.height, TextureFormat.BGRA32, false);

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

        UnityEngine.WSA.Application.InvokeOnAppThread(() => {
            _texture2D.LoadRawTextureData(_latestImageBytes);
            Stopwatch encode = Stopwatch.StartNew();
            _png = _texture2D.EncodeToPNG();
            Debug.Log("Size: " + _png.Length);
            Debug.Log("Encode time " + encode.ElapsedMilliseconds);

            Task.Run(() => ConstructPacketAndSend());
        }, false);
    }

    private void ConstructPacketAndSend() {
        try
        {
            Stopwatch writingWatch = Stopwatch.StartNew();

            _ms.SetLength(0);
            _bw.Write(convertInt(_trackers.Count));

            foreach (ObjectTracker ot in _trackers)
            {
                _bw.Write(convertInt(ot.MinH));
                _bw.Write(convertInt(ot.MaxH));
            }

            TimeSpan timeDiff  = DateTime.UtcNow - new DateTime(1970, 1, 1);
            long     timestamp = (long)timeDiff.TotalMilliseconds;

            _bw.Write(convertLong(timestamp));
            _bw.Write(convertInt(_resolution.width));
            _bw.Write(convertInt(_resolution.height));
            _bw.Write(convertInt(_png.Length));
            _bw.Write(_png);

            byte[] toSend = _ms.ToArray();

            Debug.Log("Writing: " + writingWatch.ElapsedMilliseconds);

            Stopwatch sending = Stopwatch.StartNew();

        #if !UNITY_EDITOR
            SendBytes(toSend, true);
        #endif

            Debug.Log("Sending: " + sending.ElapsedMilliseconds);

            Stopwatch receiving = Stopwatch.StartNew();
            ReceiveData();
            Debug.Log("Receiving: " + receiving.ElapsedMilliseconds);
        }
        catch (Exception e)
        {
            Debug.Log("Error: " + e.Message);
        }
    }

#if !UNITY_EDITOR
    private async void SendBytes(byte[] bytes, bool writeSize) {
        if (writeSize)
            _writer.WriteInt32(bytes.Length);
        _writer.WriteBytes(bytes);

        await _writer.StoreAsync();
    }
#endif

    private void ReceiveData() {
        try {
            Stopwatch wait = Stopwatch.StartNew();
            int  number    = _br.ReadInt32();
            Debug.Log("Wait time: " + wait.ElapsedMilliseconds);
            long timestamp = _br.ReadInt64();

            bool valid = _lastTimestamp == -1 || _lastTimestamp < timestamp;

            for (int i = 0; i < number; i++)
            {
                int posx = _br.ReadInt32();
                int posy = _br.ReadInt32();

                if (valid) ReceivePosResult(i, posx, posy);
            }

            if (valid) _lastTimestamp = timestamp;
        } catch (Exception e) {
            Debug.Log("Exception when reading: " + e.Message);
        }
    }

    private void ReceivePosResult(int index, int posx, int posy) {
        Debug.Log("Index " + index + " POSX " + posx + " POSY " + posy);
    }

    private static byte[] convertInt(int number) {
        byte[] bytes = BitConverter.GetBytes(number);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return bytes;
    }

    private static byte[] convertLong(long number) {
        byte[] bytes = BitConverter.GetBytes(number);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return bytes;
    }
}