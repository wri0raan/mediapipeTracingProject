using UnityEngine;

[ExecuteAlways]
public class AutoFrameCamera : MonoBehaviour
{
    public Camera cam;
    public Transform targetRoot;
    [Range(0f, 0.5f)] public float margin = 0.12f;
    public Vector3 offset = new Vector3(0, 0.1f, 0);

    [Header("Behavior")]
    public bool runOnce = true;

    void Start()
    {
        FrameNow();
        if (runOnce) enabled = false;
    }

    void LateUpdate()
    {
        if (!runOnce) FrameNow();
    }

    void FrameNow()
    {
        if (!cam || !targetRoot) return;

        var b = ComputeBounds(targetRoot);
        if (b.size == Vector3.zero) return;

        var center = b.center + offset;

        if (cam.orthographic)
        {
            float halfH = (b.size.y * 0.5f) * (1f + margin);
            float halfW = (b.size.x * 0.5f) * (1f + margin);
            cam.orthographicSize = Mathf.Max(halfH, halfW / cam.aspect);
            var dir = (center - cam.transform.position).normalized;
            cam.transform.position = center - dir * 10f;
            cam.transform.LookAt(center, Vector3.up);
        }
        else
        {
            float fovY = cam.fieldOfView * Mathf.Deg2Rad;
            float fovX = 2f * Mathf.Atan(Mathf.Tan(fovY * 0.5f) * cam.aspect);

            float reqHalfH = (b.size.y * 0.5f) * (1f + margin);
            float reqHalfW = (b.size.x * 0.5f) * (1f + margin);

            float distY = reqHalfH / Mathf.Tan(fovY * 0.5f);
            float distX = reqHalfW / Mathf.Tan(fovX * 0.5f);
            float dist = Mathf.Max(distX, distY);

            var fwd = cam.transform.forward;
            cam.transform.position = center - fwd.normalized * dist;
            cam.transform.LookAt(center, Vector3.up);
        }
    }

    Bounds ComputeBounds(Transform root)
    {
        var rends = root.GetComponentsInChildren<Renderer>();
        var b = new Bounds(root.position, Vector3.zero);
        bool init = false;
        foreach (var r in rends)
        {
            if (!r.enabled) continue;
            if (!init) { b = r.bounds; init = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }
}
