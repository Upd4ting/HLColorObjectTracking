using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HoloLensCameraStream;

using OpenCVForUnity;

using UnityEngine;
using UnityEngine.XR.WSA;

using Application = UnityEngine.WSA.Application;
using Resolution = HoloLensCameraStream.Resolution;
using VideoCapture = HoloLensCameraStream.VideoCapture;

public class TrackerManager : MonoBehaviour {
    private readonly List<ObjectTracker> _trackers = new List<ObjectTracker>();

    // Variable for Camera
    private Matrix4x4 _cameraToWorldMatrix = Matrix4x4.zero;
    private byte[]    _latestImageBytes;
    private Matrix4x4 _projectionMatrix = Matrix4x4.zero;

    // Variable for Depth
    private ushort[] _latestDepthBytes;
    private double depthScale;
    private uint minScale;
    private uint maxScale;

    private Resolution _resolutionColor;
    private Resolution _resolutionDepth;

    private IntPtr       _spatialCoordinateSystemPtr;
    private VideoCapture _videoCapture;
    private VideoCapture _videoCaptureDepth;
    private CameraParameters _cameraParams;

    private int lastPosX = -1;
    private int lastPosY;
    private int lastP;

    public void registerTracker(ObjectTracker tracker) {
        _trackers.Add(tracker);
    }

    public void unregisterTracker(ObjectTracker tracker) {
        _trackers.Remove(tracker);
    }

    private void Start() {
        _spatialCoordinateSystemPtr = WorldManager.GetNativeISpatialCoordinateSystemPtr();

        CameraStreamHelper.Instance.GetVideoCaptureAsync(OnVideoCaptureCreated);
        CameraStreamHelper.Instance.GetVideoCaptureDepthAsync(OnVideoCaptureDepthCreated);
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
            _videoCaptureDepth.FrameSampleAcquired -= OnFrameSampleDepthAcquired;
            _videoCapture.Dispose();
            _videoCaptureDepth.Dispose();
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

        _resolutionColor = CameraStreamHelper.Instance.GetLowestResolution(videoCapture);
        float frameRate = CameraStreamHelper.Instance.GetHighestFrameRate(videoCapture, _resolutionColor);

        videoCapture.FrameSampleAcquired += OnFrameSampleAcquired;

        CameraParameters cameraParams = new CameraParameters();
        cameraParams.cameraResolutionHeight = _resolutionColor.height;
        cameraParams.cameraResolutionWidth  = _resolutionColor.width;
        cameraParams.frameRate              = Mathf.RoundToInt(frameRate);
        cameraParams.pixelFormat            = CapturePixelFormat.BGRA32;
        cameraParams.rotateImage180Degrees  = false;
        cameraParams.enableHolograms        = false;

        _cameraParams = cameraParams;

        // Start color frame
        videoCapture.StartVideoModeAsync(false, cameraParams, OnVideoModeStarted);
    }

    private void OnVideoCaptureDepthCreated(VideoCapture videoCapture) {
        if (videoCapture == null)
        {
            Debug.LogError("Did not find a video capture object. You may not be using the HoloLens.");
            return;
        }

        _videoCaptureDepth = videoCapture;

        _resolutionDepth = CameraStreamHelper.Instance.GetLowestResolution(videoCapture);
        float frameRate = CameraStreamHelper.Instance.GetHighestFrameRate(videoCapture, _resolutionDepth);

        videoCapture.FrameSampleAcquired += OnFrameSampleDepthAcquired;

        CameraParameters cameraParams = new CameraParameters();
        cameraParams.cameraResolutionHeight = _resolutionDepth.height;
        cameraParams.cameraResolutionWidth  = _resolutionDepth.width;
        cameraParams.frameRate              = Mathf.RoundToInt(frameRate);
        cameraParams.pixelFormat            = CapturePixelFormat.BGRA32;
        cameraParams.rotateImage180Degrees  = false;
        cameraParams.enableHolograms        = false;

        _cameraParams = cameraParams;

        // Start depth frame
        videoCapture.StartVideoModeAsync(true, cameraParams, OnVideoModeStarted);
    }

    private void OnVideoModeStarted(VideoCaptureResult result) {
        if (result.success == false) {
            Debug.LogWarning("Could not start video mode.");
            return;
        }

        Debug.Log("Video capture started.");
    }

    private void morphOps(Mat thresh) {
        //create structuring element that will be used to "dilate" and "erode" image.
        //the element chosen here is a 3px by 3px rectangle
        Mat erodeElement = Imgproc.getStructuringElement(Imgproc.MORPH_RECT, new Size(3, 3));
        //dilate with larger element so make sure object is nicely visible
        Mat dilateElement = Imgproc.getStructuringElement(Imgproc.MORPH_RECT, new Size(8, 8));

        Imgproc.erode(thresh, thresh, erodeElement);
        Imgproc.erode(thresh, thresh, erodeElement);

        Imgproc.dilate(thresh, thresh, dilateElement);
        Imgproc.dilate(thresh, thresh, dilateElement);
    }

    private void trackFilteredObject(ObjectTracker ot, Mat threshold) {
        Mat temp = new Mat();
        threshold.copyTo(temp);

        List<MatOfPoint> contours  = new List<MatOfPoint>();
        Mat              hierarchy = new Mat();

        Imgproc.findContours(temp, contours, hierarchy, Imgproc.RETR_CCOMP, Imgproc.CHAIN_APPROX_SIMPLE);

        bool find = false;

        if (hierarchy.rows() > 0) {
            for (int index = 0; index >= 0; index = (int) hierarchy.get(0, index)[0]) {
                Moments moment = Imgproc.moments(contours[index]);
                double  area   = moment.m00;

                if (area > 10 * 10) {
                    int x = (int) (moment.get_m10() / area);
                    int y = (int) (moment.get_m01() / area);

                    Vector2 point  = new Vector2(x, y);
                    Vector3 dirRay = LocatableCameraUtils.PixelCoordToWorldCoord(_cameraToWorldMatrix, _projectionMatrix, _resolutionColor, point);

                    // Getting depth
                    int x1 = x * _resolutionDepth.width / _resolutionColor.width;
                    int y1 = y * _resolutionDepth.height / _resolutionColor.height;
                    int p = x1 + y1 * _resolutionDepth.width;

                    if (lastPosX != -1) {
                        if (x < lastPosX + 10 && x > lastPosX - 10 && y < lastPosY + 10 && y > lastPosY - 10)
                            p = lastP;
                    }

                    lastPosX = x;
                    lastPosY = y;
                    lastP = p;

                    var depth = _latestDepthBytes[p] * depthScale;

                    Debug.Log("At " + x + " " + y);
                    Debug.Log("Depth: " + depth);

                    Vector3 pos = Camera.main.transform.position + dirRay.normalized * (float)depth;

                    Vector3 sub = pos - ot.gameObject.transform.position;
                    float dist = sub.magnitude;

                    if (dist < 0.01f)
                        continue;

                    Debug.Log("Found");

                    StartCoroutine(MoveOverSeconds(ot.gameObject, pos, 0.05f));
                    ot.CountNotFound = 0;
                    ot.gameObject.GetComponent<Renderer>().enabled = true;
                    find = true;
                    break;
                }
            }
        }

        if (!find && ot.gameObject.GetComponent<Renderer>().enabled)
        {
            ot.CountNotFound++;
            if (ot.CountNotFound > ot.maxNotFound)
            {
                ot.CountNotFound                               = 0;
                ot.gameObject.GetComponent<Renderer>().enabled = false;
                Debug.Log("Disabled");
            }
        }
    }

    private IEnumerator MoveOverSeconds(GameObject objectToMove, Vector3 end, float seconds) {
        float   elapsedTime = 0;
        Vector3 startingPos = objectToMove.transform.position;
        while (elapsedTime < seconds) {
            objectToMove.transform.position =  Vector3.Lerp(startingPos, end, elapsedTime / seconds);
            elapsedTime                          += Time.deltaTime;
            yield return new WaitForFixedUpdate();
        }

        objectToMove.transform.position = end;
    }

    private void OnFrameSampleDepthAcquired(VideoCaptureSample sample) {
        byte[] aBytes = new byte[sample.dataLength];
        sample.CopyRawImageDataIntoBuffer(aBytes);

        _latestDepthBytes = aBytes.Select(b => (ushort)b).ToArray();
#if !UNITY_EDITOR && UNITY_WSA
        depthScale = sample.FrameReference.VideoMediaFrame.DepthMediaFrame.DepthFormat.DepthScaleInMeters;
        minScale   = sample.FrameReference.VideoMediaFrame.DepthMediaFrame.MinReliableDepth;
        maxScale   = sample.FrameReference.VideoMediaFrame.DepthMediaFrame.MaxReliableDepth;
#endif
    }

    private void OnFrameSampleAcquired(VideoCaptureSample sample) {
        if (_latestImageBytes == null || _latestImageBytes.Length < sample.dataLength) _latestImageBytes = new byte[sample.dataLength];
        sample.CopyRawImageDataIntoBuffer(_latestImageBytes);

        if (_cameraToWorldMatrix == Matrix4x4.zero || _projectionMatrix == Matrix4x4.zero) {
            float[] cameraToWorldMatrixAsFloat;
            float[] projectionMatrixAsFloat;
            if (sample.TryGetCameraToWorldMatrix(out cameraToWorldMatrixAsFloat) == false || sample.TryGetProjectionMatrix(out projectionMatrixAsFloat) == false)
            {
                Debug.Log("Failed to get camera to world or projection matrix");
                return;
            }

            _cameraToWorldMatrix = LocatableCameraUtils.ConvertFloatArrayToMatrix4x4(cameraToWorldMatrixAsFloat);
            _projectionMatrix    = LocatableCameraUtils.ConvertFloatArrayToMatrix4x4(projectionMatrixAsFloat);

            Debug.Log("Success");
        }

        if (_latestDepthBytes == null)
            return;

        Application.InvokeOnAppThread(() => {
            Mat frameBGRA = new Mat(_resolutionColor.height, _resolutionColor.width, CvType.CV_8UC4);
            frameBGRA.put(0, 0, _latestImageBytes);
            Mat frameBGR = new Mat(_resolutionColor.height, _resolutionColor.width, CvType.CV_8UC3);
            Imgproc.cvtColor(frameBGRA, frameBGR, Imgproc.COLOR_BGRA2BGR);

            Mat HSV       = new Mat();
            Mat threshold = new Mat();

            // Track objects
            foreach (ObjectTracker ot in _trackers)
            {
                Imgproc.cvtColor(frameBGR, HSV, Imgproc.COLOR_BGR2HSV);
                Core.inRange(HSV, new Scalar(ot.minH, ot.minSaturation, ot.minLight), new Scalar(ot.maxH, 255, 255), threshold);
                morphOps(threshold);
                trackFilteredObject(ot, threshold);
            }
        }, false);
    }
}