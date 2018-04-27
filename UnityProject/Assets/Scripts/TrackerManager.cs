using System;
using System.Collections;
using System.Collections.Generic;

using HoloLensCameraStream;

using OpenCVForUnity;

using UnityEngine;
using UnityEngine.XR.WSA;

using Application = UnityEngine.WSA.Application;
using Resolution = HoloLensCameraStream.Resolution;
using VideoCapture = HoloLensCameraStream.VideoCapture;

#if !UNITY_EDITOR
using System.Linq;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Media.Effects;
using Windows.Perception.Spatial;
#endif

public class TrackerManager : MonoBehaviour {
    private readonly List<ObjectTracker> _trackers = new List<ObjectTracker>();

    // Variable for Camera
    private Matrix4x4 _cameraToWorldMatrix = Matrix4x4.zero;
    private byte[]    _latestImageBytes;
    private Matrix4x4 _projectionMatrix = Matrix4x4.zero;

    // Variable for depth
    private byte[] _latestDepthImageBytes;
    private double scaleDepth;

    private Resolution _resolution;

    private IntPtr       _spatialCoordinateSystemPtr;
    private VideoCapture _videoCapture;
    private VideoCapture _videoCaptureDepth;
    private CameraParameters _cameraParams;

    public void registerTracker(ObjectTracker tracker) {
        _trackers.Add(tracker);
    }

    public void unregisterTracker(ObjectTracker tracker) {
        _trackers.Remove(tracker);
    }

    private void Start() {
        _spatialCoordinateSystemPtr = WorldManager.GetNativeISpatialCoordinateSystemPtr();

#if !UNITY_EDITOR
        SpatialCoordinateSystem test;

        unsafe 
        {
            test = (SpatialCoordinateSystem) Marshal.GetObjectForIUnknown(_spatialCoordinateSystemPtr);
        }
#endif

        CameraStreamHelper.Instance.GetVideoCaptureAsync(OnVideoCaptureCreated);
        CameraStreamHelper.Instance.GetVideoCaptureDepthAsync(OnVideoCaptureDepthCreated);
    }

    private void OnDestroy() {
        Destroy();
    }

    private void OnApplicationQuit() {
        Destroy();
    }

    private async void Destroy() {
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

        _resolution = CameraStreamHelper.Instance.GetLowestResolution(videoCapture);
        float frameRate = CameraStreamHelper.Instance.GetHighestFrameRate(videoCapture, _resolution);

        videoCapture.FrameSampleAcquired += OnFrameSampleAcquired;

        CameraParameters cameraParams = new CameraParameters();
        cameraParams.cameraResolutionHeight = _resolution.height;
        cameraParams.cameraResolutionWidth  = _resolution.width;
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

        _resolution = CameraStreamHelper.Instance.GetLowestResolution(videoCapture);
        float frameRate = CameraStreamHelper.Instance.GetHighestFrameRate(videoCapture, _resolution);

        videoCapture.FrameSampleAcquired += OnFrameSampleAcquired;

        CameraParameters cameraParams = new CameraParameters();
        cameraParams.cameraResolutionHeight = _resolution.height;
        cameraParams.cameraResolutionWidth  = _resolution.width;
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
                    Vector3 dirRay = LocatableCameraUtils.PixelCoordToWorldCoord(_cameraToWorldMatrix, _projectionMatrix, _resolution, point);

                    ot.gameObject.GetComponent<Renderer>().enabled = true;

                    // Sensor test



                    // Sensor test

                    ot.Sphere.transform.position = Camera.main.transform.position + new Vector3(0, ot.offset, 0);
                    SphereCollider collider = ot.Sphere.GetComponent<SphereCollider>();

                    // We inverse the ray source and dir to make the sphere collider work
                    Vector3 newPosRay = Camera.main.transform.position + dirRay * (collider.radius * 2);

                    Ray        ray = new Ray(newPosRay, -dirRay);
                    RaycastHit hit;

                    if (Physics.Raycast(ray, out hit, collider.radius * 3))
                    {
                        Vector3 pos = hit.point;

                        Vector3 sub  = pos - ot.gameObject.transform.position;
                        float   dist = sub.magnitude;

                        if (dist >= 0.01f)
                        {
                            StartCoroutine(MoveOverSeconds(ot.gameObject, pos, 0.05f));
                        }
                    }

                    ot.CountNotFound = 0;
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

    private void OnFrameSampleAcquired(VideoCaptureSample sample) {
        // First check if that's a depth buffer that has been retrieved
        if (sample.isDepth()) {
            Debug.Log("Received depth");
            HandleDepthFrame(sample);
            sample.Dispose();
            return;
        }

        Debug.Log("Not depth");

        if (_latestImageBytes == null || _latestImageBytes.Length < sample.dataLength) _latestImageBytes = new byte[sample.dataLength];
        sample.CopyRawImageDataIntoBuffer(_latestImageBytes);

        if (_cameraToWorldMatrix == Matrix4x4.zero || _projectionMatrix == Matrix4x4.zero) {
            float[] cameraToWorldMatrixAsFloat;
            float[] projectionMatrixAsFloat;
            if (sample.TryGetCameraToWorldMatrix(out cameraToWorldMatrixAsFloat) == false || sample.TryGetProjectionMatrix(out projectionMatrixAsFloat) == false)
            {
                Debug.Log("Failed to get camera to world or projection matrix");
                sample.Dispose();
                return;
            }

            Debug.Log("Success");

            _cameraToWorldMatrix = LocatableCameraUtils.ConvertFloatArrayToMatrix4x4(cameraToWorldMatrixAsFloat);
            _projectionMatrix    = LocatableCameraUtils.ConvertFloatArrayToMatrix4x4(projectionMatrixAsFloat);
        }

        sample.Dispose();

        Application.InvokeOnAppThread(() => {
            Mat frameBGRA = new Mat(_resolution.height, _resolution.width, CvType.CV_8UC4);
            frameBGRA.put(0, 0, _latestImageBytes);
            Mat frameBGR = new Mat(_resolution.height, _resolution.width, CvType.CV_8UC3);
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

    private void HandleDepthFrame(VideoCaptureSample sample) {
        if (_latestDepthImageBytes == null || _latestDepthImageBytes.Length < sample.dataLength) _latestDepthImageBytes = new byte[sample.dataLength];
            sample.CopyRawImageDataIntoBuffer(_latestDepthImageBytes);

        scaleDepth = sample.getDepthScale();

        Debug.Log("Scale depth: " + scaleDepth);
    }
}