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

    [Tooltip("Size of the object")] [SerializeField]
    private float size;

    [Tooltip("The Tracker manager to use")] [SerializeField]
    private TrackerManager tManager;

    [Tooltip("Tracking enabled or not")] [SerializeField]
    private bool trackingEnabled;

    public int MaxH => maxH;

    public int MinH => minH;

    public int MinLight => minLight;

    public int MinSaturation => minSaturation;

    public float Offset => offset;

    public GameObject Sphere { get; private set; }

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