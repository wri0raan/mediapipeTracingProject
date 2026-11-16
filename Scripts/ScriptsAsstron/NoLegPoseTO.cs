using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[DefaultExecutionOrder(9000)]

public class NoLegPoseTO : MonoBehaviour
{
    public enum InputSpace { Viewport01, WorldMeters }

    [Header("Refs")]
    public Camera cam;
    public Animator animator;
    public Transform modelRoot;
    public MonoBehaviour poseProvider;

    [Header("Input")]
    public InputSpace inputSpace = InputSpace.Viewport01;
    public bool invertY_VP = true;
    public bool mirrorX = false;
    [Min(0.01f)] public float depthMeters = 1.0f;
    public float worldScale = 1.0f;
    public bool flipZ_World = true;
    public Vector3 worldOffset = Vector3.zero;

    [Header("Head Tuning")]
    [Range(0f, 1f)] public float headFollowNeck = 1f;
    public Vector3 neckLocalEulerOffset = Vector3.zero;
    public Vector3 headLocalEulerOffset = Vector3.zero;
    [Range(-45f, 45f)] public float headNodBias = 0f;

    [Header("Head Limit (deg)")]
    [Range(0f, 90f)] public float headYawLimit = 45f;
    [Range(0f, 60f)] public float headPitchLimit = 30f;

    [Header("Hand Orientation")]
    public bool alignHands = true;
    [Range(0, 1)] public float handAlignWeight = 0.7f;
    [Range(0, 30)] public float handRollClamp = 12f;

    [Header("Smoothing")]
    [Range(1f, 30f)] public float jointSmooth = 12f;
    [Range(1f, 30f)] public float rootPosSmooth = 12f;
    [Range(1f, 30f)] public float rootRotSmooth = 12f;

    [Header("Joint Limits (deg)")]
    [Range(1, 90)] public float maxDegPerFrame = 30f;

    public Vector3 shoulderMin = new Vector3(-80f, -70f, -40f);
    public Vector3 shoulderMax = new Vector3(100f, 70f, 40f);

    [Header("Debug")]
    public bool drawDebug = false;

    Transform _lUpperArm, _lLowerArm, _lHand;
    Transform _rUpperArm, _rLowerArm, _rHand;
    Transform _spine, _hips, _chest, _neck, _head;
    Transform _lUpperLeg, _lLowerLeg, _lFoot;
    Transform _rUpperLeg, _rLowerLeg, _rFoot;

    readonly Dictionary<Transform, Quaternion> _aimOffset = new();
    readonly Dictionary<int, Vector3> _ptSmooth = new();

    Quaternion _idle_lUpperLeg, _idle_lLowerLeg, _idle_lFoot;
    Quaternion _idle_rUpperLeg, _idle_rLowerLeg, _idle_rFoot;

    bool _leftLegActive = false;
    bool _rightLegActive = false;

    const int NOSE = 0;
    const int L_SH = 11, R_SH = 12, L_EL = 13, R_EL = 14, L_WR = 15, R_WR = 16;
    const int L_HIP = 23, R_HIP = 24, L_KN = 25, R_KN = 26, L_AN = 27, R_AN = 28;
    const int L_IDX = 19, R_IDX = 20, L_TH = 21, R_TH = 22;

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!animator || !animator.isHuman) { enabled = false; return; }

        animator.enabled = false;

        _lUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        _lLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        _lHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);

        _rUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        _rLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        _rHand = animator.GetBoneTransform(HumanBodyBones.RightHand);

        _spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        _hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        _chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        _neck = animator.GetBoneTransform(HumanBodyBones.Neck);
        _head = animator.GetBoneTransform(HumanBodyBones.Head);

        if (_lUpperLeg) _idle_lUpperLeg = _lUpperLeg.localRotation;
        if (_lLowerLeg) _idle_lLowerLeg = _lLowerLeg.localRotation;
        if (_lFoot) _idle_lFoot = _lFoot.localRotation;

        if (_rUpperLeg) _idle_rUpperLeg = _rUpperLeg.localRotation;
        if (_rLowerLeg) _idle_rLowerLeg = _rLowerLeg.localRotation;
        if (_rFoot) _idle_rFoot = _rFoot.localRotation;
    }

    void LateUpdate()
    {
        if (!cam || !modelRoot) return;
        if (!TryGetPose(out var pts) || pts == null || pts.Count < 33) return;

        float dt = Time.deltaTime;
        float kJ = 1f - Mathf.Exp(-jointSmooth * dt);
        float kP = 1f - Mathf.Exp(-rootPosSmooth * dt);
        float kR = 1f - Mathf.Exp(-rootRotSmooth * dt);

        Vector3 PW(int i)
        {
            var v = pts[i];
            Vector3 w;
            if (inputSpace == InputSpace.Viewport01)
            {
                float x = mirrorX ? (1f - v.x) : v.x;
                float y = invertY_VP ? (1f - v.y) : v.y;
                w = cam.ViewportToWorldPoint(new Vector3(x, y, depthMeters)) + worldOffset;
            }
            else
            {
                float x = mirrorX ? -v.x : v.x;
                float y = v.y;
                float z = flipZ_World ? -v.z : v.z;
                w = cam.transform.TransformPoint(new Vector3(x, y, z) * worldScale) + worldOffset;
            }
            if (_ptSmooth.TryGetValue(i, out var prev)) w = Vector3.Lerp(prev, w, kJ);
            _ptSmooth[i] = w;
            return w;
        }

        var LS = PW(L_SH); var RS = PW(R_SH);
        var LE = PW(L_EL); var RE = PW(R_EL);
        var LW = PW(L_WR); var RW = PW(R_WR);

        var LH = PW(L_HIP); var RH = PW(R_HIP);
        var LK = PW(L_KN); var RK = PW(R_KN);
        var LA = PW(L_AN); var RA = PW(R_AN);

        var NO = PW(NOSE);

        var midHip = (LH + RH) * 0.5f;
        modelRoot.position = Vector3.Lerp(modelRoot.position, midHip, kP);

        var right = (RS - LS); if (right.sqrMagnitude < 1e-8f) right = modelRoot.right;
        right.Normalize();
        var up = Vector3.up;
        var fwd = Vector3.Cross(up, right).normalized;
        if (fwd.sqrMagnitude < 1e-8f) fwd = modelRoot.forward;

        var targetRootRot = Quaternion.LookRotation(fwd, up);
        modelRoot.rotation = Quaternion.Slerp(modelRoot.rotation, targetRootRot, kR);

        CacheOffsetOnce(_lUpperArm, _lLowerArm);
        CacheOffsetOnce(_lLowerArm, _lHand);
        CacheOffsetOnce(_rUpperArm, _rLowerArm);
        CacheOffsetOnce(_rLowerArm, _rHand);
        CacheOffsetOnce(_lUpperLeg, _lLowerLeg);
        CacheOffsetOnce(_lLowerLeg, _lFoot);
        CacheOffsetOnce(_rUpperLeg, _rLowerLeg);
        CacheOffsetOnce(_rLowerLeg, _rFoot);
        CacheOffsetOnce(_neck, _head);

        AimEMA(_lUpperArm, _lLowerArm, LS, LE, kJ);
        AimEMA(_lLowerArm, _lHand, LE, LW, kJ);
        AimEMA(_rUpperArm, _rLowerArm, RS, RE, kJ);
        AimEMA(_rLowerArm, _rHand, RE, RW, kJ);

        if (_neck)
        {
            Vector3 chestCenterN = 0.5f * (LS + RS);
            Vector3 neckFwd = (NO - chestCenterN);
            if (neckFwd.sqrMagnitude < 1e-6f)
                neckFwd = (animator ? animator.transform.forward : Vector3.forward);
            neckFwd.Normalize();

            Vector3 neckRight = (RS - LS);
            if (neckRight.sqrMagnitude < 1e-6f) neckRight = _neck.right;
            neckRight.Normalize();

            if (Mathf.Abs(headNodBias) > 0.001f)
                neckFwd = Quaternion.AngleAxis(headNodBias, neckRight) * neckFwd;

            Vector3 neckUp = Vector3.Cross(neckRight, neckFwd);
            if (neckUp.sqrMagnitude < 1e-6f) neckUp = Vector3.up;
            neckUp.Normalize();
            neckFwd = Vector3.Cross(neckUp, neckRight).normalized;

            var qNeckLook = Quaternion.LookRotation(neckFwd, neckUp);
            var qNeck = ApplyOffset(_neck, qNeckLook) * ExtraLocal(_neck, neckLocalEulerOffset);
            _neck.rotation = Quaternion.Slerp(_neck.rotation, qNeck, kJ);
        }

        if (_head)
        {
            var baseHead = _neck ? _neck.rotation : _head.rotation;
            var qHead = baseHead * ExtraLocal(_head, headLocalEulerOffset);
            var tgt = Quaternion.Slerp(_head.rotation, qHead, Mathf.Clamp01(headFollowNeck));
            _head.rotation = Quaternion.Slerp(_head.rotation, tgt, kJ);

            Quaternion localRot = Quaternion.Inverse(_neck.rotation) * _head.rotation;
            Vector3 euler = localRot.eulerAngles;
            if (euler.x > 180) euler.x -= 360;
            if (euler.y > 180) euler.y -= 360;
            if (euler.z > 180) euler.z -= 360;
            euler.x = Mathf.Clamp(euler.x, -headPitchLimit, headPitchLimit);
            euler.y = Mathf.Clamp(euler.y, -headYawLimit, headYawLimit);
            _head.rotation = _neck.rotation * Quaternion.Euler(euler);
        }

        if (drawDebug)
        {
            Debug.DrawLine(LS, RS, Color.cyan);
            Debug.DrawLine(LH, RH, Color.yellow);
            Debug.DrawLine(LE, LW, Color.green);
            Debug.DrawLine(RE, RW, Color.magenta);
            Debug.DrawRay(modelRoot.position, modelRoot.forward * 0.4f, Color.blue);

            Color lc = _leftLegActive ? Color.green : Color.red;
            Color rc = _rightLegActive ? Color.green : Color.red;
            Debug.DrawLine(LH, LK, lc);
            Debug.DrawLine(RH, RK, rc);
        }
    }

    static Vector3 Normalize180(Vector3 e)
    {
        e.x = Mathf.DeltaAngle(0f, e.x);
        e.y = Mathf.DeltaAngle(0f, e.y);
        e.z = Mathf.DeltaAngle(0f, e.z);
        return e;
    }

    Quaternion HandLookFrom(Vector3 wrist, Vector3 indexKnuckle, Vector3 pinkyKnuckle, Vector3 elbow, bool isLeft)
    {
        var vI = (indexKnuckle - wrist);
        var vP = (pinkyKnuckle - wrist);
        if (vI.sqrMagnitude < 1e-6f || vP.sqrMagnitude < 1e-6f) return Quaternion.identity;

        var palmRight = (vI + vP);
        palmRight.Normalize();

        var forearm = (wrist - elbow);
        if (forearm.sqrMagnitude < 1e-6f) forearm = Vector3.up;
        forearm.Normalize();

        var palmNormal = Vector3.Cross(palmRight, forearm);
        palmNormal.Normalize();

        var fwd = palmRight;
        var up = Vector3.Cross(fwd, palmNormal);
        if (isLeft) fwd = -fwd;

        return Quaternion.LookRotation(fwd, up);
    }

    void ApplyHandAlign(Transform hand, Transform lowerArm,
                        Vector3 wrist, Vector3 idx, Vector3 pky, Vector3 elbow, bool isLeft)
    {
        var q = HandLookFrom(wrist, idx, pky, elbow, isLeft);
        if (q == Quaternion.identity) return;

        var step = maxDegPerFrame * (Application.isPlaying ? Time.deltaTime * 60f : 1f);
        var tgt = Quaternion.Slerp(hand.rotation, q, handAlignWeight);

        var e = Normalize180((Quaternion.Inverse(lowerArm.rotation) * tgt).eulerAngles);
        e.z = Mathf.Clamp(e.z, -handRollClamp, handRollClamp);
        tgt = lowerArm.rotation * Quaternion.Euler(e);

        hand.rotation = Quaternion.RotateTowards(hand.rotation, tgt, step);
    }

    Quaternion ClampLocalEuler(Transform t, Vector3 minDeg, Vector3 maxDeg)
    {
        var e = Normalize180(t.localEulerAngles);
        e.x = Mathf.Clamp(e.x, minDeg.x, maxDeg.x);
        e.y = Mathf.Clamp(e.y, minDeg.y, maxDeg.y);
        e.z = Mathf.Clamp(e.z, minDeg.z, maxDeg.z);
        return Quaternion.Euler(e);
    }

    void EnforceJointLimits()
    {
        float step = maxDegPerFrame * (Application.isPlaying ? Time.deltaTime * 60f : 1f);

        if (_lUpperArm)
        {
            var tgt = ClampLocalEuler(_lUpperArm, shoulderMin, shoulderMax);
            _lUpperArm.localRotation = Quaternion.RotateTowards(_lUpperArm.localRotation, tgt, step);
        }
        if (_rUpperArm)
        {
            var tgt = ClampLocalEuler(_rUpperArm, shoulderMin, shoulderMax);
            _rUpperArm.localRotation = Quaternion.RotateTowards(_rUpperArm.localRotation, tgt, step);
        }
    }

    void CacheOffsetOnce(Transform bone, Transform child)
    {
        if (!bone || _aimOffset.ContainsKey(bone)) return;
        Quaternion qOff = Quaternion.identity;
        if (child)
        {
            var dir = child.position - bone.position;
            if (dir.sqrMagnitude > 1e-8f)
            {
                var aim = Quaternion.LookRotation(dir.normalized, Vector3.up);
                qOff = Quaternion.Inverse(aim) * bone.rotation;
            }
        }
        else
        {
            var aim = Quaternion.LookRotation(bone.forward, Vector3.up);
            qOff = Quaternion.Inverse(aim) * bone.rotation;
        }
        _aimOffset[bone] = qOff;
    }

    Quaternion ApplyOffset(Transform bone, Quaternion desired)
    {
        if (bone && _aimOffset.TryGetValue(bone, out var off)) return desired * off;
        return desired;
    }

    Quaternion ExtraLocal(Transform bone, Vector3 eulerLocal)
    {
        var qLocal = Quaternion.Euler(eulerLocal);
        if (!bone || !bone.parent) return qLocal;
        return bone.parent.rotation * qLocal * Quaternion.Inverse(bone.parent.rotation);
    }

    void AimEMA(Transform bone, Transform child, Vector3 from, Vector3 to, float k)
    {
        if (!bone) return;
        var dir = to - from; if (dir.sqrMagnitude < 1e-8f) return;
        var q = ApplyOffset(bone, Quaternion.LookRotation(dir.normalized, Vector3.up));
        bone.rotation = Quaternion.Slerp(bone.rotation, q, k);
    }

    static float Wrap180(float a)
    {
        a %= 360f;
        if (a > 180f) a -= 360f;
        if (a < -180f) a += 360f;
        return a;
    }

    static Vector3 ClampEuler(Vector3 e, Vector3 min, Vector3 max)
    {
        e = new Vector3(Wrap180(e.x), Wrap180(e.y), Wrap180(e.z));
        return new Vector3(
            Mathf.Clamp(e.x, min.x, max.x),
            Mathf.Clamp(e.y, min.y, max.y),
            Mathf.Clamp(e.z, min.z, max.z)
        );
    }

    bool TryGetPose(out List<Vector3> pts)
    {
        pts = null;
        if (!poseProvider) return false;
        var t = poseProvider.GetType();

        var mi = t.GetMethod("TryGetPose", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                             null, new[] { typeof(List<Vector3>).MakeByRefType() }, null);
        if (mi != null)
        {
            object[] args = new object[] { null };
            bool ok = (bool)mi.Invoke(poseProvider, args);
            if (ok && args[0] is List<Vector3> list) { pts = list; return true; }
        }

        mi = t.GetMethod("TryGetPose", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                         null, new[] { typeof(Vector3[]).MakeByRefType() }, null);
        if (mi != null)
        {
            object[] args = new object[] { null };
            bool ok = (bool)mi.Invoke(poseProvider, args);
            if (ok && args[0] is Vector3[] arr) { pts = new List<Vector3>(arr); return true; }
        }

        var prop = t.GetProperty("points", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? t.GetProperty("posePoints", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop != null)
        {
            var v = prop.GetValue(poseProvider);
            if (v is List<Vector3> l && l.Count >= 33) { pts = l; return true; }
            if (v is Vector3[] a && a.Length >= 33) { pts = new List<Vector3>(a); return true; }
        }

        return false;
    }
}
