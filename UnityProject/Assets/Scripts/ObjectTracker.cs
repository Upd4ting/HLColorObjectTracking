using UnityEngine;

public class ObjectTracker : MonoBehaviour {
    [Header("Tracker settings")] [Range(0, 180)] [Tooltip("The maximum H required (HSV color format)")]
    public int maxH;

    [Range(0, 180)] [Tooltip("The minimum H required (HSV color format)")]
    public int minH;

    [Range(0, 255)] [Tooltip("The minimum H required (HSV color format)")]
    public int minLight;

    [Range(0, 255)] [Tooltip("The minimum saturation required (HSV color format)")]
    public int minSaturation;

    [Tooltip("Once this limit reached, the object will be disabled")]
    public int maxNotFound;

    [Tooltip("The Tracker manager to use")]
    public TrackerManager tManager;

    [Tooltip("Tracking enabled or not")]
    public bool trackingEnabled;

    public int CountNotFound { get; set; }

    private void Start() {
        if (trackingEnabled) {
            tManager.registerTracker(this);
        }
    }

    private void OnDestroy() {
        if (trackingEnabled) {
            tManager.unregisterTracker(this);
        }
    }
}