using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class PoseProvider_FromRunner : MonoBehaviour, IPoseProvider
{
    [Header("PoseLandmarkerRunner가 붙은 오브젝트 (예: Solution)")]
    [SerializeField] Component runner;

    readonly List<Vector3> _uvz = new(33);
    object _owner; MemberInfo _member; int _miss;

    void Awake()
    {
        if (!runner) runner = AutoFindRunner();
        Debug.Log(runner ? $"[PoseProvider:R] Runner={runner.GetType().Name}" : "[PoseProvider:R] Runner NOT FOUND");
    }

    Component AutoFindRunner()
    {
        foreach (var mb in FindObjectsOfType<MonoBehaviour>(true))
        {
            var n = mb.GetType().Name;
            if (n.Contains("PoseLandmarkerRunner")) return mb;
        }
        return null;
    }

    public bool TryGetPose(out List<Vector3> uvz)
    {
        uvz = _uvz;

        if (!runner)
        {
            runner = AutoFindRunner();
            if (!runner) { Throttle(); return false; }
        }

        if (_member != null && _owner != null)
        {
            if (TryRead(_owner, _member, _uvz)) { _miss = 0; return _uvz.Count >= 33; }
            _member = null; _owner = null;
        }

        var t = runner.GetType();
        foreach (var m in t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            var n = m.Name.ToLower();
            if (!(n.Contains("result") || n.Contains("pose") || n.Contains("landmark"))) continue;

            object v = null;
            try
            {
                if (m is FieldInfo f) v = f.GetValue(runner);
                else if (m is PropertyInfo p && p.CanRead && p.GetIndexParameters().Length == 0) v = p.GetValue(runner, null);
            }
            catch { }

            if (v == null) continue;

            if (TrySearchDeep(v, out _owner, out _member))
            {
                Debug.Log($"[PoseProvider:R] Using owner={_owner.GetType().Name}, member={_member.DeclaringType.Name}.{_member.Name}");
                if (TryRead(_owner, _member, _uvz)) { _miss = 0; return _uvz.Count >= 33; }
                _member = null; _owner = null;
            }
        }

        Throttle();
        return false;
    }

    void Throttle() { _miss++; if (_miss % 30 == 0) Debug.Log("[PoseProvider] Pose MISSING"); }

    static bool TrySearchDeep(object root, out object owner, out MemberInfo member)
    {
        owner = null; member = null;
        if (root == null) return false;
        var queue = new Queue<object>(); queue.Enqueue(root);
        int steps = 0;

        while (queue.Count > 0 && steps < 200)
        {
            steps++;
            var obj = queue.Dequeue();
            var t = obj.GetType();

            foreach (var m in t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                object v = null;
                if (m is FieldInfo f) v = SafeGetField(f, obj);
                else if (m is PropertyInfo p && p.CanRead && p.GetIndexParameters().Length == 0) v = SafeGetProp(p, obj);
                else continue;

                if (v == null) continue;

                if (LooksLikeLandmarkEnumerable(v)) { owner = obj; member = m; return true; }

                var name = m.Name.ToLower();
                if (name.Contains("landmark") || name.Contains("pose") || name.Contains("result") || name.Contains("output") || name.Contains("list"))
                    queue.Enqueue(v);
            }
        }
        return false;
    }

    static bool TryRead(object owner, MemberInfo m, List<Vector3> outList)
    {
        object v = null;
        if (m is FieldInfo f) v = SafeGetField(f, owner);
        else if (m is PropertyInfo p && p.CanRead && p.GetIndexParameters().Length == 0) v = SafeGetProp(p, owner);
        if (v == null) return false;

        outList.Clear();
        return ExtractUVZRecursive(v, outList, 0);
    }

    static bool ExtractUVZRecursive(object obj, List<Vector3> outList, int depth)
    {
        if (obj == null || depth > 6) return false;

        if (obj is IEnumerable en && !(obj is string))
        {
            var snapshot = new List<object>();
            try { var it = en.GetEnumerator(); while (it.MoveNext()) snapshot.Add(it.Current); } catch { }
            bool any = false;
            foreach (var e in snapshot) any |= ExtractUVZRecursive(e, outList, depth + 1);
            return any;
        }

        var t = obj.GetType();
        var px = t.GetProperty("X"); var py = t.GetProperty("Y"); var pz = t.GetProperty("Z");
        if (px != null && py != null && pz != null &&
            px.GetIndexParameters().Length == 0 && py.GetIndexParameters().Length == 0 && pz.GetIndexParameters().Length == 0)
        {
            try
            {
                float x = (float)(px.GetValue(obj, null) ?? 0f);
                float y = (float)(py.GetValue(obj, null) ?? 0f);
                float z = (float)(pz.GetValue(obj, null) ?? 0f);
                outList.Add(new Vector3(x, y, z));
                return true;
            }
            catch { }
        }

        foreach (var pname in new[] { "Landmarks", "landmarks", "poseLandmarks", "PoseLandmarks", "Landmark" })
        {
            var p = t.GetProperty(pname, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null && p.CanRead && p.GetIndexParameters().Length == 0)
            {
                var list = SafeGetProp(p, obj);
                if (ExtractUVZRecursive(list, outList, depth + 1)) return true;
            }
        }
        return false;
    }

    static bool LooksLikeLandmarkEnumerable(object v)
    {
        if (v is IEnumerable en && !(v is string))
        {
            object first = null;
            try { var it = en.GetEnumerator(); if (it.MoveNext()) first = it.Current; } catch { }
            if (first == null) return false;
            var t = first.GetType();
            return t.GetProperty("X") != null && t.GetProperty("Y") != null && t.GetProperty("Z") != null;
        }
        return false;
    }

    static object SafeGetProp(PropertyInfo p, object o) { try { return p.GetValue(o, null); } catch { return null; } }
    static object SafeGetField(FieldInfo f, object o) { try { return f.GetValue(o); } catch { return null; } }
}
