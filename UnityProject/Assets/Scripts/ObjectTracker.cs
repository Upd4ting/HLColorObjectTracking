using UnityEngine;

public class ObjectTracker : MonoBehaviour {
    [Header("Tracker settings")] [Range(0, 180)] [Tooltip("The maximum H required (HSV color format)")] [SerializeField]
    private int maxH;

    [Range(0, 180)] [Tooltip("The minimum H required (HSV color format)")] [SerializeField]
    private int minH;

    [Range(0, 255)] [Tooltip("The minimum H required (HSV color format)")] [SerializeField]
    private int minLight;

    [Range(0, 255)] [Tooltip("The minimum saturation required (HSV color format)")] [SerializeField]
    private int minSaturation;

    [Tooltip("The offset from the camera for the origin point")] [SerializeField]
    private float offset;

    [Tooltip("Size of the stick")] [SerializeField]
    private float size;

    [Tooltip("The Tracker manager to use")] [SerializeField]
    private TrackerManager tManager;

    [Tooltip("Tracking enabled or not")] [SerializeField]
    private bool trackingEnabled;

    public GameObject Sphere { get; private set; }

    public int MaxH { get { return maxH; } set { maxH = value; } }

    public int MinH { get { return minH; } set { minH = value; } }

    public int MinLight { get { return minLight; } set { minLight = value; } }

    public int MinSaturation { get { return minSaturation; } set { minSaturation = value; } }

    private void Start() {
        if (trackingEnabled) {
            tManager.registerTracker(this);

            Sphere = new GameObject();

            SphereCollider collider = Sphere.AddComponent<SphereCollider>();
            Sphere.transform.position = Camera.main.transform.position + new Vector3(0, offset, 0);
            collider.radius             = size;
            collider.isTrigger          = true;
            collider.enabled            = false;
            Sphere.layer = LayerMask.NameToLayer("Tracker");
        }
    }

    private void OnDestroy() {
        if (trackingEnabled) {
            tManager.unregisterTracker(this);
            Destroy(Sphere);
        }
    }
}