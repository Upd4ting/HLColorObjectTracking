using UnityEngine;

public class ObjectTracker : MonoBehaviour {
    [Tooltip("The maximum H required (HSV color format)")] [SerializeField]
    private int maxH;

    [Header("Tracker settings")] [Tooltip("The minimum H required (HSV color format)")] [SerializeField]
    private int minH;

    [Tooltip("The Tracker manager to use")] [SerializeField]
    private TrackerManager tManager;

    [Tooltip("Tracking enabled or not")] [SerializeField]
    private bool trackingEnabled;

    public int MaxH { get { return maxH; } set { maxH = value; } }

    public int MinH { get { return minH; } set { minH = value; } }

    private void Start() {
        if (trackingEnabled) tManager.registerTracker(this);
    }
}