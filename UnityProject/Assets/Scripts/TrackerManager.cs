using System;
using System.Collections.Generic;

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

    private Resolution _resolution;

    private IntPtr       _spatialCoordinateSystemPtr;
    private VideoCapture _videoCapture;

    public void registerTracker(ObjectTracker tracker) {
        _trackers.Add(tracker);
    }

    public void unregisterTracker(ObjectTracker tracker) {
        _trackers.Remove(tracker);
    }

    private void Start() {
        _spatialCoordinateSystemPtr = WorldManager.GetNativeISpatialCoordinateSystemPtr();

        CameraStreamHelper.Instance.GetVideoCaptureAsync(OnVideoCaptureCreated);
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

        Debug.Log("Frame rate: " + frameRate);

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

        if (hierarchy.rows() > 0)
            for (int index = 0; index >= 0; index = (int) hierarchy.get(0, index)[0]) {
                Moments moment = Imgproc.moments(contours[index]);
                double  area   = moment.m00;

                if (area > 10 * 10) {
                    int x = (int) (moment.get_m10() / area);
                    int y = (int) (moment.get_m01() / area);

                    Vector2 point  = new Vector2(x, y);
                    Vector3 dirRay = LocatableCameraUtils.PixelCoordToWorldCoord(_cameraToWorldMatrix, _projectionMatrix, _resolution, point);

                    Application.InvokeOnAppThread(() => {
                        ot.Sphere.transform.position = Camera.main.transform.position + new Vector3(0, ot.Offset, 0);
                        SphereCollider collider = ot.Sphere.GetComponent<SphereCollider>();

                        // We inverse the ray source and dir to make the sphere collider work
                        Vector3 newPosRay = Camera.main.transform.position + dirRay * (collider.radius * 2);

                        Ray        ray = new Ray(newPosRay, -dirRay);
                        RaycastHit hit;

                        if (Physics.Raycast(ray, out hit, collider.radius * 3))
                        {
                            Vector3 pos = hit.point;
                            ot.gameObject.transform.position = pos;
                        }
                    }, false);
                }
            }
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

        _cameraToWorldMatrix = LocatableCameraUtils.ConvertFloatArrayToMatrix4x4(cameraToWorldMatrixAsFloat);
        _projectionMatrix    = LocatableCameraUtils.ConvertFloatArrayToMatrix4x4(projectionMatrixAsFloat);

        sample.Dispose();

        Mat frameBGRA = new Mat(_resolution.height, _resolution.width, CvType.CV_8UC4);
        frameBGRA.put(0, 0, _latestImageBytes);
        Mat frameBGR = new Mat(_resolution.height, _resolution.width, CvType.CV_8UC3);
        Imgproc.cvtColor(frameBGRA, frameBGR, Imgproc.COLOR_BGRA2BGR);

        Mat HSV       = new Mat();
        Mat threshold = new Mat();

        // Track objects
        foreach (ObjectTracker ot in _trackers) {
            Imgproc.cvtColor(frameBGR, HSV, Imgproc.COLOR_BGR2HSV);
            Core.inRange(HSV, new Scalar(ot.MinH, ot.MinSaturation, ot.MinLight), new Scalar(ot.MaxH, 255, 255), threshold);
            morphOps(threshold);
            trackFilteredObject(ot, threshold);
        }
    }
}