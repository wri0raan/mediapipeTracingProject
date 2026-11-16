using System.Linq;
using System.Reflection;
using UnityEngine;

public class ForceWebCamSource : MonoBehaviour
{
    [Header("Target filter")]
    public string typeNameContains = "WebCam";
    public string preferredName = "";

    [Header("Request")]
    public int width = 640;
    public int height = 480;
    public int fps = 60;

    [Header("Behavior")]
    public float waitTimeout = 2.0f;
    public bool dontRestartIfRunning = true;
    public bool disableSelfAfterApply = true;

    float _deadline;
    bool _applied;

    void OnEnable()
    {
        _deadline = Time.unscaledTime + waitTimeout;
        InvokeRepeating(nameof(TryApplyOnce), 0.1f, 0.2f);
    }
    void OnDisable() => CancelInvoke(nameof(TryApplyOnce));

    void TryApplyOnce()
    {
        if (_applied) { StopSelf(); return; }
        if (Time.unscaledTime > _deadline) { StopSelf(); return; }

        var targets = FindObjectsOfType<MonoBehaviour>(true)
            .Where(mb => mb && (string.IsNullOrEmpty(typeNameContains) ||
                    mb.GetType().Name.ToLower().Contains(typeNameContains.ToLower())))
            .ToArray();
        if (targets.Length == 0) return;

        int applied = 0;
        foreach (var t in targets)
        {
            var ty = t.GetType();
            bool touched = false;

            bool running = GetBool(ty, t, "IsRunning", "isRunning", "IsPlaying", "isPlaying");
            if (dontRestartIfRunning && running)
            {
                SetIf(ty, t, "RequestedDeviceName", "DeviceName", "deviceName", preferredName, ref touched);
                SetIf(ty, t, "RequestedWidth", "Width", "requestedWidth", width, ref touched);
                SetIf(ty, t, "RequestedHeight", "Height", "requestedHeight", height, ref touched);
                SetIf(ty, t, "RequestedFps", "Fps", "requestedFps", fps, ref touched);
                if (touched) applied++;
                continue;
            }

            SetIf(ty, t, "RequestedDeviceName", "DeviceName", "deviceName", preferredName, ref touched);
            SetIf(ty, t, "RequestedWidth", "Width", "requestedWidth", width, ref touched);
            SetIf(ty, t, "RequestedHeight", "Height", "requestedHeight", height, ref touched);
            SetIf(ty, t, "RequestedFps", "Fps", "requestedFps", fps, ref touched);
            SetIf(ty, t, "AutoStart", "PlayOnAwake", "autoStart", true, ref touched);

            foreach (var m in new[] { "StartRunning", "Play", "Start" })
            {
                var mi = ty.GetMethod(m, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (mi != null) { try { mi.Invoke(t, null); touched = true; } catch { } break; }
            }
            if (touched) applied++;
        }
        if (applied > 0) { _applied = true; StopSelf(); }
    }

    void StopSelf()
    {
        CancelInvoke(nameof(TryApplyOnce));
        if (disableSelfAfterApply) enabled = false;
    }

    static void SetIf<T>(System.Type ty, object obj, string n1, string n2, string n3, T val, ref bool touched)
    {
        foreach (var n in new[] { n1, n2, n3 })
        {
            var p = ty.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanWrite) { try { p.SetValue(obj, val); touched = true; return; } catch { } }
            var f = ty.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null) { try { f.SetValue(obj, val); touched = true; return; } catch { } }
        }
    }
    static bool GetBool(System.Type ty, object obj, params string[] names)
    {
        foreach (var n in names)
        {
            var p = ty.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanRead && p.PropertyType == typeof(bool)) { try { return (bool)p.GetValue(obj); } catch { } }
            var f = ty.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(bool)) { try { return (bool)f.GetValue(obj); } catch { } }
        }
        return false;
    }
}
