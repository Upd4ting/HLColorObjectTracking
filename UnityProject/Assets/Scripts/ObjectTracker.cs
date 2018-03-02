using UnityEngine;

public class ObjectTracker : MonoBehaviour {
    [Header("Tracker settings")] [Tooltip("The maximum H required (HSV color format)")] [SerializeField]
    private int maxH;

    [Tooltip("The minimum H required (HSV color format)")] [SerializeField]
    private int minH;

    [Tooltip("The minimum H required (HSV color format)")] [SerializeField]
    private int minLight;

    [Tooltip("The minimum saturation required (HSV color format)")] [SerializeField]
    private int minSaturation;

    [Tooltip("The Tracker manager to use")] [SerializeField]
    private TrackerManager tManager;

    [Tooltip("Tracking enabled or not")] [SerializeField]
    private bool trackingEnabled;

    public int MaxH { get { return maxH; } set { maxH = value; } }

    public int MinH { get { return minH; } set { minH = value; } }

    public int MinLight { get { return minLight; } set { minLight = value; } }

    public int MinSaturation { get { return minSaturation; } set { minSaturation = value; } }

    private void Start() {
        if (trackingEnabled) tManager.registerTracker(this);
    }
}