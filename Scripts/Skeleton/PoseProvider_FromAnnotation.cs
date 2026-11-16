using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class PoseProvider_FromAnnotation : MonoBehaviour, IPoseProvider
{
    [Header("Annotation Layer에 붙은 *PoseLandmarkerResultAnnotationController*")]
    [SerializeField] private Component controller;

    [Tooltip("true면 poseWorldLandmarks(미터), false면 poseLandmarks(0~1) 우선 사용")]
    [SerializeField] private bool preferWorldLandmarks = true;

    private readonly List<Vector3> _out = new List<Vector3>(33);

    void Awake()
    {
        if (!controller) controller = AutoFindController();
        if (controller)
            Debug.Log("[PoseProvider] bind => PoseLandmarkerResultAnnotationController");
    }

    private Component AutoFindController()
    {
        foreach (var mb in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
        {
            var t = mb.GetType();
            var n = t.FullName ?? t.Name;

            if (n.EndsWith("PoseLandmarkerResultAnnotationController"))
                return mb;
        }
        return null;
    }

    public bool TryGetPose(out List<Vector3> uvz)
    {
        uvz = _out; _out.Clear();

        if (!controller) { controller = AutoFindController(); if (!controller) return false; }

        var latest = GetProp(controller, "LatestResult");
        if (latest == null) return false;

        object listOfLists = null;
        if (preferWorldLandmarks) listOfLists = GetField(latest, "poseWorldLandmarks");
        if (listOfLists == null) listOfLists = GetField(latest, "poseLandmarks");
        if (listOfLists == null) return false;

        var people = listOfLists as IEnumerable; if (people == null) return false;
        object first = null;
        var it = people.GetEnumerator();
        if (it.MoveNext()) first = it.Current;
        if (first == null) return false;

        object points = GetFieldOrProp(first, "Landmark")
                     ?? GetFieldOrProp(first, "landmark")
                     ?? GetFieldOrProp(first, "Landmarks")
                     ?? GetFieldOrProp(first, "landmarks");
        var enumPts = points as IEnumerable; if (enumPts == null) return false;

        foreach (var p in enumPts)
        {
            if (p == null) continue;
            var x = GetFieldOrProp(p, "X") ?? GetFieldOrProp(p, "x");
            var y = GetFieldOrProp(p, "Y") ?? GetFieldOrProp(p, "y");
            var z = GetFieldOrProp(p, "Z") ?? GetFieldOrProp(p, "z");
            if (x == null || y == null || z == null) continue;

            _out.Add(new Vector3(ToF(x), ToF(y), ToF(z)));
            if (_out.Count == 33) break;
        }

        return _out.Count == 33;
    }

    static object GetProp(object o, string name)
    {
        if (o == null) return null;
        var p = o.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.CanRead && p.GetIndexParameters().Length == 0)
        { try { return p.GetValue(o, null); } catch { } }
        return null;
    }
    static object GetField(object o, string name)
    {
        if (o == null) return null;
        var f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null) { try { return f.GetValue(o); } catch { } }
        return null;
    }
    static object GetFieldOrProp(object o, string name) => GetField(o, name) ?? GetProp(o, name);

    static float ToF(object v)
    {
        if (v is float f) return f;
        if (v is double d) return (float)d;
        if (v is int i) return i;
        if (v is long l) return l;
        float.TryParse(v.ToString(), out var r); return r;
    }
}
