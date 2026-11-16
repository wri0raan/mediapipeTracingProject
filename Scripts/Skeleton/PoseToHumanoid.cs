using System.Collections.Generic;
using UnityEngine;

public class PoseToHumanoid : MonoBehaviour
{
    public enum BindScope { HandsOnly, UpperBody, FullBody }
    public enum InputSpace { Viewport01, WorldMeters }

    [Header("Refs")]
    public Camera cam;
    public Animator animator;
    public Transform modelRoot;
    public MonoBehaviour poseProvider;

    [Header("Binding")]
    public BindScope scope = BindScope.FullBody;

    [Header("Input Space")]
    public InputSpace inputSpace = InputSpace.Viewport01;

    [Header("Viewport (0~1) 입력용")]
    public bool invertY = true;
    public bool mirrorX_VP = false;
    [Min(0.01f)] public float depthMeters = 1.0f;
    public Vector3 worldOffsetAfterVP = new Vector3(0.3f, -0.5f, 0f);

    [Header("World(미터) 입력용")]
    public bool mirrorX_World = false;
    public bool flipZ_World = true;
    public float worldScale = 1.0f;
    public Vector3 worldOffset = Vector3.zero;

    [Header("Root Follow & Smoothing (Hz)")]
    [Tooltip("루트 위치 스무딩 컷오프(Hz)")]
    [Range(1f, 30f)] public float posLerp = 12f;
    [Tooltip("루트 회전 스무딩 컷오프(Hz)")]
    [Range(1f, 30f)] public float rotLerp = 12f;
    public bool yawFromCamera = true;

    [Header("Auto Matching / Stability")]
    public bool autoScale = true;
    [Range(1f, 30f)] public float scaleLerp = 10f;
    [Tooltip("각 본 회전 스무딩 컷오프(Hz)")]
    [Range(1f, 30f)] public float jointSmooth = 15f;

    [Header("Head Debug")]
    public bool invertHeadYaw = false;
    public bool invertHeadPitch = false;

    [Header("Neck/Head Limits (deg)")]
    [Range(0f, 120f)] public float headYawMax = 70f;
    [Range(0f, 60f)] public float headPitchMax = 30f;
    [Range(0f, 45f)] public float headRollMax = 0f;

    [Header("Knee/Foot Limits (deg)")]
    [Range(0f, 179f)] public float kneeMaxBack = 5f;
    [Range(0f, 179f)] public float footYawLimit = 95f;

    [Header("Leg Stabilizer")]
    [Range(1f, 30f)] public float legSmooth = 9f;
    [Range(90f, 1440f)] public float legMaxDegPerSec = 420f;

    [Header("Foot Lock")]
    public float footLockVelThresh = 0.03f;
    public float footLockBlendTime = 0.12f;

    [Header("Facing")]
    public bool flipFacing180 = false;
    public float facingYawOffsetDeg = 0f;

    [Header("Leg Static Flip (when leg tracking is OFF)")]
    public bool flipLegsIdle = false;
    public enum FlipAxis { X, Y, Z }
    public FlipAxis flipAxis = FlipAxis.X;

    [Header("Leg Freeze (no tracking)")]
    [Tooltip("다리 트레킹을 완전히 끄고, 모델 기본 포즈(T-pose)로 고정합니다.")]
    public bool freezeLegsToIdle = true;

    [Tooltip("freeze 상태에서 골반만 180° 뒤집어 방향만 바꿉니다(무릎 접힘 방지).")]
    public bool flipHipOnly = false;

    public FlipAxis hipFlipAxis = FlipAxis.X;

    Quaternion _idle_hips;

    [Header("Root Lock")]
    public bool lockRootTransform = false;

    [Header("Input Mapping")]
    [Tooltip("어깨 방향으로 L/R 뒤집힘 자동 감지")]
    public bool autoDetectMirror = true;
    [Tooltip("거울 모드 강제 (손/발만 미러)")]
    public bool forceSwapLR = false;

    [Header("Debug")]
    public bool drawDebug = false;
    public Color debugColor = new(0.1f, 1f, 0.7f, 0.8f);

    float _headYaw, _headPitch;
    float _headYawVel, _headPitchVel;
    [Range(0.01f, 0.5f)] public float headSmoothTime = 0.12f;
    [Range(0f, 15f)] public float headBackHysteresis = 4f;

    float _lFootYaw, _rFootYaw;
    float _lFootYawVel, _rFootYawVel;
    [Range(0.01f, 0.5f)] public float footYawSmoothTime = 0.10f;

    IPoseProvider _provider;

    Transform _lUpperArm, _lLowerArm, _lHand;
    Transform _rUpperArm, _rLowerArm, _rHand;
    Transform _spine, _hips, _chest, _neck, _head;
    Transform _lUpperLeg, _lLowerLeg, _lFoot, _lToes;
    Transform _rUpperLeg, _rLowerLeg, _rFoot, _rToes;

    Transform _lHandEnd, _rHandEnd;

    readonly Dictionary<Transform, Quaternion> _aimOffset = new();
    readonly Dictionary<int, Vector3> _ptSmooth = new();

    bool _offsetCached = false;
    float _modelInitialShoulder = 0.4f;
    Vector3 _rootScale0 = Vector3.one;

    const int NOSE = 0;
    const int L_SHOULDER = 11, R_SHOULDER = 12;
    const int L_ELBOW = 13, R_ELBOW = 14;
    const int L_WRIST = 15, R_WRIST = 16;

    const int L_PINKY = 17, R_PINKY = 18;
    const int L_INDEX = 19, R_INDEX = 20;
    const int L_THUMB = 21, R_THUMB = 22;

    const int L_HIP = 23, R_HIP = 24;
    const int L_KNEE = 25, R_KNEE = 26;
    const int L_ANKLE = 27, R_ANKLE = 28;
    const int L_HEEL = 29, R_HEEL = 30;
    const int L_FOOTIDX = 31, R_FOOTIDX = 32;

    const int L_EYE_INNER = 1, L_EYE = 2, L_EYE_OUTER = 3;
    const int R_EYE_INNER = 4, R_EYE = 5, R_EYE_OUTER = 6;

    bool _lLocked, _rLocked;
    float _lLockBlend;
    float _rLockBlend;
    Vector3 _lLockPos, _rLockPos;
    Quaternion _lLockRot, _rLockRot;
    Vector3 _prevLA, _prevRA;

    Quaternion _idle_lUpperLeg, _idle_lLowerLeg, _idle_lFoot, _idle_lToes;
    Quaternion _idle_rUpperLeg, _idle_rLowerLeg, _idle_rFoot, _idle_rToes;
    bool _legIdleCached = false;

    static Transform FindDescendantBySuffix(Transform root, string suffix)
    {
        if (!root) return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t != root && t.name.EndsWith(suffix)) return t;
        return null;
    }

    void Awake()
    {
        _provider = poseProvider as IPoseProvider;

        if (animator != null)
        {

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

            _lUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            _lLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            _lFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _lToes = animator.GetBoneTransform(HumanBodyBones.LeftToes);

            _rUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            _rLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            _rFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            _rToes = animator.GetBoneTransform(HumanBodyBones.RightToes);

            if (_lUpperLeg) _idle_lUpperLeg = _lUpperLeg.localRotation;
            if (_lLowerLeg) _idle_lLowerLeg = _lLowerLeg.localRotation;
            if (_lFoot) _idle_lFoot = _lFoot.localRotation;
            if (_lToes) _idle_lToes = _lToes.localRotation;

            if (_rUpperLeg) _idle_rUpperLeg = _rUpperLeg.localRotation;
            if (_rLowerLeg) _idle_rLowerLeg = _rLowerLeg.localRotation;
            if (_rFoot) _idle_rFoot = _rFoot.localRotation;
            if (_rToes) _idle_rToes = _rToes.localRotation;

            _legIdleCached = true;

            if (_hips) _idle_hips = _hips.localRotation;

            _lHandEnd = FindDescendantBySuffix(_lHand, "_end");
            _rHandEnd = FindDescendantBySuffix(_rHand, "_end");

            if (modelRoot) _rootScale0 = modelRoot.localScale;

            if (_lUpperArm && _rUpperArm)
            {
                _modelInitialShoulder = Vector3.Distance(_lUpperArm.position, _rUpperArm.position);
                if (_modelInitialShoulder < 1e-4f) _modelInitialShoulder = 0.4f;
            }
        }
    }

    void LateUpdate()
    {
        if (_provider == null || cam == null || animator == null || modelRoot == null) return;
        if (!_provider.TryGetPose(out var pts) || pts == null || pts.Count < 33) return;

        float dt = Time.deltaTime;

        Vector3 ToWorldSmooth(int idx)
        {
            var v = pts[idx];
            Vector3 w;
            if (inputSpace == InputSpace.Viewport01)
            {
                float x = mirrorX_VP ? (1f - v.x) : v.x;
                float y = invertY ? (1f - v.y) : v.y;
                w = cam.ViewportToWorldPoint(new Vector3(x, y, depthMeters)) + worldOffsetAfterVP;
            }
            else
            {
                float x = mirrorX_World ? -v.x : v.x;
                float y = v.y;
                float z = flipZ_World ? -v.z : v.z;
                w = cam.transform.TransformPoint(new Vector3(x, y, z) * worldScale) + worldOffset;
            }

            float a = 1f - Mathf.Exp(-jointSmooth * dt);
            if (_ptSmooth.TryGetValue(idx, out var prev)) w = Vector3.Lerp(prev, w, a);
            _ptSmooth[idx] = w;
            return w;
        }

        var pNO = ToWorldSmooth(NOSE);

        var pLS = ToWorldSmooth(L_SHOULDER);
        var pRS = ToWorldSmooth(R_SHOULDER);
        var pLE = ToWorldSmooth(L_ELBOW);
        var pRE = ToWorldSmooth(R_ELBOW);
        var pLW = ToWorldSmooth(L_WRIST);
        var pRW = ToWorldSmooth(R_WRIST);

        var pLIdx = ToWorldSmooth(L_INDEX);
        var pRIdx = ToWorldSmooth(R_INDEX);
        var pLTh = ToWorldSmooth(L_THUMB);
        var pRTh = ToWorldSmooth(R_THUMB);

        var pLH = ToWorldSmooth(L_HIP);
        var pRH = ToWorldSmooth(R_HIP);
        var pLK = ToWorldSmooth(L_KNEE);
        var pRK = ToWorldSmooth(R_KNEE);
        var pLA = ToWorldSmooth(L_ANKLE);
        var pRA = ToWorldSmooth(R_ANKLE);
        var pLHeel = ToWorldSmooth(L_HEEL);
        var pRHeel = ToWorldSmooth(R_HEEL);
        var pLToe = ToWorldSmooth(L_FOOTIDX);
        var pRToe = ToWorldSmooth(R_FOOTIDX);

        bool needSwap = forceSwapLR;
        if (autoDetectMirror)
        {
            var camRight = cam.transform.right;
            var shoulderVec = pRS - pLS;
            if (Vector3.Dot(shoulderVec.normalized, camRight.normalized) < 0f)
                needSwap = !needSwap;
        }

        bool swapHeadTorso = needSwap;
        bool swapArms = !needSwap;
        bool swapLegs = needSwap;

        Vector3 L_head(Vector3 Lp, Vector3 Rp) => swapHeadTorso ? Rp : Lp;
        Vector3 R_head(Vector3 Lp, Vector3 Rp) => swapHeadTorso ? Lp : Rp;

        Vector3 L_arm(Vector3 Lp, Vector3 Rp) => swapArms ? Rp : Lp;
        Vector3 R_arm(Vector3 Lp, Vector3 Rp) => swapArms ? Lp : Rp;

        Vector3 L_leg(Vector3 Lp, Vector3 Rp) => swapLegs ? Rp : Lp;
        Vector3 R_leg(Vector3 Lp, Vector3 Rp) => swapLegs ? Lp : Rp;

        var LS_head = L_head(pLS, pRS);
        var RS_head = R_head(pLS, pRS);

        var LS = L_arm(pLS, pRS);
        var RS = R_arm(pLS, pRS);
        var LE = L_arm(pLE, pRE);
        var RE = R_arm(pLE, pRE);
        var LW = L_arm(pLW, pRW);
        var RW = R_arm(pLW, pRW);
        var LIdx = L_arm(pLIdx, pRIdx);
        var RIdx = R_arm(pLIdx, pRIdx);
        var LTh = L_arm(pLTh, pRTh);
        var RTh = R_arm(pLTh, pRTh);

        var LH = L_leg(pLH, pRH);
        var RH = R_leg(pLH, pRH);
        var LK = L_leg(pLK, pRK);
        var RK = R_leg(pLK, pRK);
        var LA = L_leg(pLA, pRA);
        var RA = R_leg(pLA, pRA);
        var LHeel = L_leg(pLHeel, pRHeel);
        var RHeel = R_leg(pLHeel, pRHeel);
        var LToe = L_leg(pLToe, pRToe);
        var RToe = R_leg(pLToe, pRToe);

        float vLA = (LA - _prevLA).magnitude / Mathf.Max(dt, 1e-4f);
        float vRA = (RA - _prevRA).magnitude / Mathf.Max(dt, 1e-4f);
        _prevLA = LA; _prevRA = RA;

        var midHip = (LH + RH) * 0.5f;

        if (!lockRootTransform)
        {
            modelRoot.position = PoseSmooth.Exp(modelRoot.position, midHip, posLerp, dt);
        }

        Quaternion targetRot;
        if (yawFromCamera)
        {
            var fwd = cam.transform.forward;
            var right = (RS - LS).sqrMagnitude > 1e-8f ? (RS - LS).normalized : modelRoot.right;
            var up = Vector3.Cross(fwd, right).normalized;
            targetRot = Quaternion.LookRotation(fwd, up);
        }
        else
        {
            var fwd = (RH - LH);
            fwd.y = 0;
            fwd = fwd.sqrMagnitude > 1e-8f ? fwd.normalized : modelRoot.forward;
            var up = Vector3.up;
            targetRot = Quaternion.LookRotation(fwd, up);
        }

        if (flipFacing180)
            targetRot = Quaternion.Euler(0f, 180f, 0f) * targetRot;
        if (Mathf.Abs(facingYawOffsetDeg) > 0.001f)
            targetRot = Quaternion.Euler(0f, facingYawOffsetDeg, 0f) * targetRot;

        if (!lockRootTransform)
        {
            modelRoot.rotation = PoseSmooth.Exp(modelRoot.rotation, targetRot, rotLerp, dt);
        }

        if (Mathf.Abs(facingYawOffsetDeg) > 0.001f)
            targetRot = Quaternion.Euler(0f, facingYawOffsetDeg, 0f) * targetRot;

        modelRoot.rotation = PoseSmooth.Exp(modelRoot.rotation, targetRot, rotLerp, dt);

        if (!_offsetCached)
        {

            CacheAimOffset(_lUpperArm, _lLowerArm);
            CacheAimOffset(_lLowerArm, _lHand);
            CacheAimOffset(_rUpperArm, _rLowerArm);
            CacheAimOffset(_rLowerArm, _rHand);

            if (_lHand && _lHandEnd) CacheAimOffset(_lHand, _lHandEnd);
            if (_rHand && _rHandEnd) CacheAimOffset(_rHand, _rHandEnd);

            CacheAimOffset(_lUpperLeg, _lLowerLeg);
            CacheAimOffset(_rUpperLeg, _rLowerLeg);
            CacheAimOffset(_lLowerLeg, _lFoot);
            CacheAimOffset(_rLowerLeg, _rFoot);
            if (_lFoot && _lToes) CacheAimOffset(_lFoot, _lToes);
            if (_rFoot && _rToes) CacheAimOffset(_rFoot, _rToes);

            _offsetCached = true;
        }

        if (_lUpperArm && _lLowerArm && _lHand)
        {
            AimBoneWithOffset(_lUpperArm, _lLowerArm, LS, LE);
            AimBoneWithOffset(_lLowerArm, _lHand, LE, LW);
            var qL = HandLook(LW, LIdx, LTh, modelRoot);
            _lHand.rotation = PoseSmooth.Exp(_lHand.rotation, ApplyOffset(_lHand, qL), jointSmooth, dt);
        }
        if (_rUpperArm && _rLowerArm && _rHand)
        {
            AimBoneWithOffset(_rUpperArm, _rLowerArm, RS, RE);
            AimBoneWithOffset(_rLowerArm, _rHand, RE, RW);
            var qR = HandLook(RW, RIdx, RTh, modelRoot);
            _rHand.rotation = PoseSmooth.Exp(_rHand.rotation, ApplyOffset(_rHand, qR), jointSmooth, dt);
        }

        if (scope == BindScope.HandsOnly) { DrawDbg(LS, RS); return; }

        if (_spine && _hips)
        {
            var shouldersMid = (LS + RS) * 0.5f;
            var spineDir = (shouldersMid - midHip);
            if (spineDir.sqrMagnitude > 1e-6f)
            {
                var fwd = yawFromCamera ? cam.transform.forward : modelRoot.forward;
                var spineRot = Quaternion.LookRotation(fwd, spineDir.normalized);
                _spine.rotation = PoseSmooth.Exp(_spine.rotation, spineRot, jointSmooth, dt);

                if (!freezeLegsToIdle)
                    _hips.rotation = PoseSmooth.Exp(_hips.rotation, targetRot, jointSmooth, dt);
            }
        }

        if (_neck)
        {

            var rightAxis = (pRS - pLS);
            if (rightAxis.sqrMagnitude > 1e-8f)
            {
                rightAxis.Normalize();

                Vector3 fwdRef = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
                if (fwdRef.sqrMagnitude < 1e-8f) fwdRef = modelRoot.forward;

                var upAxis = Vector3.Cross(fwdRef, rightAxis).normalized;
                if (Vector3.Dot(upAxis, Vector3.up) < 0f) upAxis = -upAxis;

                var shouldersMid = (pLS + pRS) * 0.5f;
                var dirW = pNO - shouldersMid;
                if (dirW.sqrMagnitude > 1e-6f)
                {

                    var horiz = Vector3.ProjectOnPlane(dirW, upAxis);
                    if (horiz.sqrMagnitude < 1e-8f) horiz = fwdRef;
                    float yawRaw = Vector3.SignedAngle(fwdRef, horiz.normalized, upAxis);

                    float pitchRaw = Vector3.SignedAngle(horiz.normalized, dirW.normalized, rightAxis);

                    const float yawDeadband = 4f;
                    if (Mathf.Abs(yawRaw) < yawDeadband) yawRaw = 0f;

                    if (invertHeadYaw) yawRaw = -yawRaw;
                    if (invertHeadPitch) pitchRaw = -pitchRaw;

                    yawRaw = Mathf.Clamp(yawRaw, -headYawMax, headYawMax);
                    pitchRaw = Mathf.Clamp(pitchRaw, -headPitchMax, headPitchMax);

                    _headYaw = Mathf.SmoothDampAngle(_headYaw, yawRaw, ref _headYawVel, headSmoothTime);
                    _headPitch = Mathf.SmoothDampAngle(_headPitch, pitchRaw, ref _headPitchVel, headSmoothTime);

                    var shoulderFrameRot = Quaternion.LookRotation(fwdRef, upAxis);
                    var qYaw = Quaternion.AngleAxis(_headYaw, Vector3.up);
                    var qPitch = Quaternion.AngleAxis(_headPitch, shoulderFrameRot * Vector3.right);
                    var qRoll = Quaternion.AngleAxis(
                                    Mathf.Clamp(Vector3.SignedAngle(Vector3.up, upAxis, fwdRef), -headRollMax, headRollMax),
                                    shoulderFrameRot * Vector3.forward);

                    var targetWorld = shoulderFrameRot * qYaw * qPitch * qRoll;
                    _neck.rotation = PoseSmooth.Exp(_neck.rotation, ApplyOffset(_neck, targetWorld), jointSmooth, dt);
                }
            }
        }
        if (_head && _neck)
        {
            _head.rotation = PoseSmooth.Exp(_head.rotation, _neck.rotation, jointSmooth, dt);
        }

        if (scope == BindScope.UpperBody)
        {

            if (freezeLegsToIdle && _legIdleCached)
            {
                RestoreLegIdleToCached();
                if (flipHipOnly) ApplyHipFlipOnly();
            }

            DrawDbg(LS, RS);
            return;
        }

        Quaternion Axis180(FlipAxis ax)
        {
            switch (ax)
            {
                case FlipAxis.X: return Quaternion.AngleAxis(180f, Vector3.right);
                case FlipAxis.Y: return Quaternion.AngleAxis(180f, Vector3.up);
                default: return Quaternion.AngleAxis(180f, Vector3.forward);
            }
        }

        void RestoreLegIdleToCached()
        {
            if (!_legIdleCached) return;

            if (_lUpperLeg) _lUpperLeg.localRotation = _idle_lUpperLeg;
            if (_lLowerLeg) _lLowerLeg.localRotation = _idle_lLowerLeg;
            if (_lFoot) _lFoot.localRotation = _idle_lFoot;
            if (_lToes) _lToes.localRotation = _idle_lToes;

            if (_rUpperLeg) _rUpperLeg.localRotation = _idle_rUpperLeg;
            if (_rLowerLeg) _rLowerLeg.localRotation = _idle_rLowerLeg;
            if (_rFoot) _rFoot.localRotation = _idle_rFoot;
            if (_rToes) _rToes.localRotation = _idle_rToes;

            if (_hips) _hips.localRotation = _idle_hips;
        }

        void ApplyHipFlipOnly()
        {
            if (_hips == null) return;
            var q = Axis180(hipFlipAxis);

            _hips.localRotation = q * _idle_hips;
        }

        void CacheAimOffset(Transform bone, Transform child)
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

        void AimBoneWithOffset(Transform bone, Transform child, Vector3 from, Vector3 to)
        {
            if (!bone) return;
            var dir = (to - from);
            if (dir.sqrMagnitude < 1e-8f) return;
            var q = ApplyOffset(bone, Quaternion.LookRotation(dir.normalized, Vector3.up));
            bone.rotation = PoseSmooth.Exp(bone.rotation, q, jointSmooth, dt);
        }

        static Quaternion HandLook(Vector3 wrist, Vector3 index, Vector3 thumb, Transform root)
        {
            var fwd = ((index + thumb) * 0.5f - wrist);
            if (fwd.sqrMagnitude < 1e-8f) fwd = root.forward;

            var v1 = (index - wrist);
            var v2 = (thumb - wrist);
            var up = Vector3.Cross(v1, v2);
            if (up.sqrMagnitude < 1e-8f) up = Vector3.up;
            if (Vector3.Dot(up, Vector3.up) < 0f) up = -up;

            return Quaternion.LookRotation(fwd.normalized, up.normalized);
        }

        void DrawDbg(Vector3 pLS, Vector3 pRS)
        {
            if (!drawDebug) return;
            Debug.DrawLine(pLS, pRS, debugColor, 0, false);
        }
    }
}
