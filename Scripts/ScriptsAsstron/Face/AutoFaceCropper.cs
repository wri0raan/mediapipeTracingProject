using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Mediapipe.Unity;
using Mediapipe.Tasks.Vision.PoseLandmarker;

[RequireComponent(typeof(VisorFaceScreen))]
public class AutoFaceCropper : MonoBehaviour
{
    [Header("Mediapipe Annotation Controllers (하나만 연결해도 동작)")]
    public PoseLandmarkerResultAnnotationController poseController;
    public Component faceController;

    [Header("선택 옵션")]
    [Min(0)] public int personIndex = 0;
    public bool mirrorX = true;

    [Header("프레이밍 설정 (코 중심, 원형 마스크용 정사각 크롭)")]
    [Range(0f, 0.8f)] public float eyeOuterPadding = 0.15f;
    [Range(0.6f, 2.0f)] public float userScale = 1.0f;
    [Range(0.08f, 1.0f)] public float minSize = 0.18f;
    [Range(0.10f, 1.0f)] public float maxSize = 0.85f;

    [Header("스무딩")]
    [Range(0.01f, 1.0f)] public float smoothPos = 0.35f;
    [Range(0.01f, 1.0f)] public float smoothSize = 0.35f;

    [Header("Display alignment")]
    public bool flipXForDisplay = true;
    public bool flipYForDisplay = true;

    [Header("디버그")]
    public bool logWhenStale = false;
    public float staleWarnSec = 0.6f;
    public int debugLandmarkCount;
    public Vector2 debugCenter;
    public float debugSize;
    public Vector4 lastCrop;

    const int NoseIdx = 0;
    static readonly int[] LEO = { 3 };
    static readonly int[] REO = { 6 };
    static readonly int[] LEar = { 7 };
    static readonly int[] REar = { 8 };

    VisorFaceScreen visor;
    Vector4 cropTarget, cropCurrent;
    float lastLandmarkTime = -999f;
    bool anyEventHooked = false;

    MethodInfo faceLatestGetter;

    void Awake()
    {
        visor = GetComponent<VisorFaceScreen>();
        cropCurrent = cropTarget = new Vector4(0.25f, 0.25f, 0.5f, 0.5f);

        if (faceController != null)
        {
            faceLatestGetter = faceController.GetType().GetMethod("get_LatestResult",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
    }

    void OnEnable()
    {
        visor.mirrorX = mirrorX;
        TryHookEvents();
    }

    void OnDisable()
    {
        TryUnhookEvents();
    }

    void Update()
    {
        visor.mirrorX = mirrorX;

        bool updated = TryPollPose() || TryPollFace();

        SmoothAndApply();

        if (logWhenStale && Time.time - lastLandmarkTime > staleWarnSec)
        {
            Debug.LogWarning($"[AutoFaceCropper] landmarks stale {Time.time - lastLandmarkTime:0.00}s. " +
                             $"Check controller wiring & Play Mode updates.");
        }
    }

    void TryHookEvents()
    {
        if (anyEventHooked) return;

        if (poseController != null)
        {
            var evt = poseController.GetType().GetEvent("OnUpdated",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (evt != null)
            {
                evt.AddEventHandler(poseController, (Action)(OnPoseUpdated));
                anyEventHooked = true;
            }
        }

        if (faceController != null)
        {
            var evt = faceController.GetType().GetEvent("OnUpdated",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (evt != null)
            {
                evt.AddEventHandler(faceController, (Action)(OnFaceUpdated));
                anyEventHooked = true;
            }
        }
    }

    void TryUnhookEvents()
    {
        if (!anyEventHooked) return;

        if (poseController != null)
        {
            var evt = poseController.GetType().GetEvent("OnUpdated",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (evt != null) evt.RemoveEventHandler(poseController, (Action)(OnPoseUpdated));
        }
        if (faceController != null)
        {
            var evt = faceController.GetType().GetEvent("OnUpdated",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (evt != null) evt.RemoveEventHandler(faceController, (Action)(OnFaceUpdated));
        }
        anyEventHooked = false;
    }

    void OnPoseUpdated() { TryPollPose(); }
    void OnFaceUpdated() { TryPollFace(); }

    bool TryPollPose()
    {
        if (poseController == null) return false;

        var result = poseController.LatestResult;
        var people = GetFieldOrProperty(result, "poseLandmarks", "PoseLandmarks") as IEnumerable;
        if (people == null) return false;

        var person = GetEnumerableElementAt(people, Mathf.Max(0, personIndex));
        if (person == null) return false;

        var lmList = GetLandmarkIList(person) ?? ToIList(person as IEnumerable);
        debugLandmarkCount = lmList != null ? lmList.Count : 0;
        if (lmList == null || lmList.Count == 0) return false;

        if (UpdateCropFromLandmarks(lmList))
        {
            lastLandmarkTime = Time.time;
            return true;
        }
        return false;
    }

    bool TryPollFace()
    {
        if (faceController == null || faceLatestGetter == null) return false;

        var latest = faceLatestGetter.Invoke(faceController, null);
        if (latest == null) return false;

        IList lmList = null;

        var faces = GetFieldOrProperty(latest, "faceLandmarks", "FaceLandmarks") as IEnumerable;
        if (faces != null)
        {
            var first = GetEnumerableElementAt(faces, 0);
            lmList = GetLandmarkIList(first) ?? ToIList(first as IEnumerable);
        }

        if (lmList == null)
        {
            lmList = GetLandmarkIList(latest) ?? ToIList(latest as IEnumerable);
        }

        debugLandmarkCount = lmList != null ? lmList.Count : 0;
        if (lmList == null || lmList.Count == 0) return false;

        if (UpdateCropFromLandmarks(lmList))
        {
            lastLandmarkTime = Time.time;
            return true;
        }
        return false;
    }

    bool UpdateCropFromLandmarks(IList lmList)
    {

        bool okNose = TryGetPoint(lmList, NoseIdx, out Vector2 nose);
        bool okLEO  = TryGetFirstAvailable(lmList, LEO, out Vector2 lEO);
        bool okREO  = TryGetFirstAvailable(lmList, REO, out Vector2 rEO);

        if ((!okLEO || !okREO))
        {
            bool okLE = TryGetFirstAvailable(lmList, LEar, out Vector2 lEar);
            bool okRE = TryGetFirstAvailable(lmList, REar, out Vector2 rEar);
            if (okLE && okRE) { lEO = lEar; rEO = rEar; okLEO = okREO = true; }
        }

        Vector2 center;
        if (!okNose)
        {
            if (!TryGetFaceBBoxCenter(lmList, out center)) return false;
        }
        else center = nose;

        float half;
        if (okLEO && okREO)
        {
            float eyeWidth = Vector2.Distance(lEO, rEO);
            float noseToL  = Vector2.Distance(center, lEO);
            float noseToR  = Vector2.Distance(center, rEO);
            float radius   = Mathf.Max(eyeWidth * 0.5f, Mathf.Max(noseToL, noseToR));
            radius *= (1f + eyeOuterPadding) * userScale;
            half = radius;
        }
        else
        {
            if (!TryGetFaceBBoxSquareHalf(lmList, out half)) return false;
            half *= (1f + eyeOuterPadding) * userScale;
        }

        float size = Mathf.Clamp(half * 2f, minSize, maxSize);

        if (size < minSize) size = minSize;
        if (logWhenStale) Debug.Log($"[AFC] size={size:0.000} (min={minSize:0.000}, max={maxSize:0.000})");

        Vector2 adjCenter = ClampSquareInside(center, size);

        SetSquareCropAround(adjCenter, size);

        debugCenter = adjCenter;
        debugSize   = size;
        return true;
    }

    void SetSquareCropAround(Vector2 center, float size)
    {
        float w = Mathf.Clamp01(size);
        float h = w;
        float x = Mathf.Clamp01(center.x - w * 0.5f);
        float y = Mathf.Clamp01(center.y - h * 0.5f);
        if (x + w > 1f) x = 1f - w;
        if (y + h > 1f) y = 1f - h;

        if (flipXForDisplay)
            x = 1f - (x + w);

        if (flipYForDisplay)
            y = 1f - (y + h);

        cropTarget = new Vector4(x, y, w, h);
    }

    Vector2 ClampSquareInside(Vector2 c, float size)
    {
        float half = size * 0.5f;
        return new Vector2(Mathf.Clamp(c.x, half, 1f - half),
                           Mathf.Clamp(c.y, half, 1f - half));
    }

    void SmoothAndApply()
    {
        float kp = 1f - Mathf.Exp(-smoothPos  * Time.deltaTime);
        float ks = 1f - Mathf.Exp(-smoothSize * Time.deltaTime);

        cropCurrent.x = Mathf.Lerp(cropCurrent.x, cropTarget.x, kp);
        cropCurrent.y = Mathf.Lerp(cropCurrent.y, cropTarget.y, kp);
        cropCurrent.z = Mathf.Lerp(cropCurrent.z, cropTarget.z, ks);
        cropCurrent.w = Mathf.Lerp(cropCurrent.w, cropTarget.w, ks);

        lastCrop = cropCurrent;

        visor.cropX = cropCurrent.x;
        visor.cropY = cropCurrent.y;
        visor.cropW = cropCurrent.z;
        visor.cropH = cropCurrent.w;
    }

    static bool TryGetFirstAvailable(IList lmList, int[] cand, out Vector2 p)
    {
        foreach (var idx in cand) if (TryGetPoint(lmList, idx, out p)) return true;
        p = default; return false;
    }

    static bool TryGetPoint(IList lmList, int idx, out Vector2 p)
    {
        p = default;
        if (lmList == null || idx < 0 || idx >= lmList.Count) return false;
        var lm = lmList[idx]; if (lm == null) return false;
        float x = GetFloat(lm, "X", "x"); float y = GetFloat(lm, "Y", "y");
        if (float.IsNaN(x) || float.IsNaN(y)) return false;
        p = new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
        return true;
    }

    static bool TryGetFaceBBoxCenter(IList lmList, out Vector2 center)
    {
        if (!TryGetFaceBBox(lmList, out float minX, out float minY, out float maxX, out float maxY))
        { center = default; return false; }
        center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        return true;
    }

    static bool TryGetFaceBBoxSquareHalf(IList lmList, out float half)
    {
        if (!TryGetFaceBBox(lmList, out float minX, out float minY, out float maxX, out float maxY))
        { half = 0; return false; }
        float w = maxX - minX, h = maxY - minY;
        half = Mathf.Max(w, h) * 0.5f; return true;
    }

    static bool TryGetFaceBBox(IList lmList, out float minX, out float minY, out float maxX, out float maxY)
    {
        int[] faceIdx = { 0,1,2,3,4,5,6,7,8,9,10 };
        minX = 1f; minY = 1f; maxX = 0f; maxY = 0f; bool any = false;
        foreach (int i in faceIdx)
        {
            if (i < 0 || i >= lmList.Count) continue;
            var lm = lmList[i]; if (lm == null) continue;
            float x = GetFloat(lm, "X", "x"), y = GetFloat(lm, "Y", "y");
            if (float.IsNaN(x) || float.IsNaN(y)) continue;
            any = true; if (x < minX) minX = x; if (y < minY) minY = y;
            if (x > maxX) maxX = x; if (y > maxY) maxY = y;
        }
        if (!any || maxX <= minX || maxY <= minY) return false;
        float w = maxX - minX, h = maxY - minY;
        minX = Mathf.Clamp01(minX - w * 0.05f);
        maxX = Mathf.Clamp01(maxX + w * 0.05f);
        minY = Mathf.Clamp01(minY - h * 0.05f);
        maxY = Mathf.Clamp01(maxY + h * 0.05f);
        return true;
    }

    static object GetFieldOrProperty(object target, params string[] names)
    {
        if (target == null) return null;
        var t = target.GetType();
        foreach (var n in names)
        {
            var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null) return p.GetValue(target, null);
            var f = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) return f.GetValue(target);
        }
        return null;
    }

    static object GetEnumerableElementAt(IEnumerable seq, int index)
    {
        if (seq == null) return null;
        int i = 0;
        foreach (var e in seq) { if (i == index) return e; i++; }
        return null;
    }

    static IList GetLandmarkIList(object obj)
    {
        if (obj == null) return null;
        var t = obj.GetType();
        var p = t.GetProperty("Landmark") ?? t.GetProperty("landmarks") ?? t.GetProperty("Landmarks");
        if (p != null) return p.GetValue(obj, null) as IList;
        var f = t.GetField("Landmark") ?? t.GetField("landmarks") ?? t.GetField("Landmarks");
        if (f != null) return f.GetValue(obj) as IList;
        return null;
    }

    static float GetFloat(object obj, params string[] names)
    {
        if (obj == null) return float.NaN;
        var t = obj.GetType();
        foreach (var n in names)
        {
            var p = t.GetProperty(n);
            if (p != null && p.PropertyType == typeof(float)) return (float)p.GetValue(obj, null);
            var f = t.GetField(n);
            if (f != null && f.FieldType == typeof(float)) return (float)f.GetValue(obj);
        }
        return float.NaN;
    }

    static IList ToIList(IEnumerable e)
    {
        if (e == null) return null;
        var list = new List<object>();
        foreach (var x in e) list.Add(x);
        return list;
    }
}
