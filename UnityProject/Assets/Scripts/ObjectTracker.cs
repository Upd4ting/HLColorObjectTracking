using UnityEngine;

public class ObjectTracker : MonoBehaviour {
    [Header("Tracker settings")] [Tooltip("Tracking enabled or not")] [SerializeField]
    private bool trackingEnabled;

    [Header("Tracker settings")] [Tooltip("The maximum H required (HSV color format)")] [SerializeField]
    private int maxH;

    [Header("Tracker settings")] [Tooltip("The minimum H required (HSV color format)")] [SerializeField]
    private int minH;

    [Header("Tracker settings")] [Tooltip("The Tracker manager to use")] [SerializeField]
    private TrackerManager tManager;

    private void Start() {
        if (trackingEnabled) tManager.registerTracker(this);
    }
}