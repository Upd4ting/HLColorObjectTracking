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

    [Tooltip("The offset from the camera for the origin point")]
    public float offset;

    [Tooltip("Size of the object")]
    public float size;

    [Tooltip("Once this limit reached, the object will be disabled")]
    public int maxNotFound;

    [Tooltip("The Tracker manager to use")]
    public TrackerManager tManager;

    [Tooltip("Tracking enabled or not")]
    public bool trackingEnabled;

    public GameObject Sphere { get; private set; }

    public int CountNotFound { get; set; }

    private void Start() {
        if (trackingEnabled) {
            tManager.registerTracker(this);

            Sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            MeshRenderer ren = Sphere.GetComponent<MeshRenderer>();
            ren.enabled = false;

            SphereCollider collider = Sphere.GetComponent<SphereCollider>();
            collider.radius    = size;
            collider.isTrigger = true;
            collider.enabled   = true;
        }
    }

    private void OnDestroy() {
        if (trackingEnabled) {
            tManager.unregisterTracker(this);
            Destroy(Sphere);
        }
    }
}