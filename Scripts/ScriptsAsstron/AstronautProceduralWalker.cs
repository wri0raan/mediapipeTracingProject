using UnityEngine;

[ExecuteAlways]
public class AstronautProceduralWalker : MonoBehaviour
{
    [Header("General")]
    public Animator anim;
    [Tooltip("초당 보행 주기(Hz). 2 = 1초에 좌우 한 번씩")]
    [Range(0.2f, 4f)] public float cadence = 2.0f;
    [Tooltip("재생(에디터 모드에서도)")]
    public bool animateInEditMode = false;

    [Header("Lower Body")]
    [Range(0f, 50f)] public float kneeBend = 22f;
    [Range(0f, 25f)] public float anklePitch = 12f;
    [Range(0f, 10f)] public float ankleRoll = 4f;
    [Range(0f, 0.06f)] public float hipBounce = 0.02f;

    [Header("Upper Body")]
    [Range(0f, 35f)] public float armSwing = 22f;
    [Range(0f, 12f)] public float armYaw = 5f;
    [Range(0f, 10f)] public float spineCounterYaw = 4f;

    [Header("Head Motion")]
    [Range(0f, 10f)] public float headYaw = 5f;
    [Range(0f, 8f)] public float headPitch = 4f;
    [Range(0f, 6f)] public float headRoll = 3f;

    [Header("Phase")]
    [Range(-3.14f, 3.14f)] public float phaseOffset = 0f;

    Transform hips, spine, head;
    Transform lThigh, rThigh, lShin, rShin, lFoot, rFoot, lUpperArm, rUpperArm;

    Quaternion hipsBindR, spineBindR, headBindR;
    Quaternion lThighBindR, rThighBindR, lShinBindR, rShinBindR, lFootBindR, rFootBindR, lUpperBindR, rUpperBindR;
    Vector3 hipsBindPos;

    void Reset() { anim = GetComponent<Animator>(); }

    void OnEnable()
    {
        if (!anim) anim = GetComponent<Animator>();
        CacheBones();
        CacheBindPose();
    }

    void CacheBones()
    {
        if (!anim) return;
        hips = anim.GetBoneTransform(HumanBodyBones.Hips);
        spine = anim.GetBoneTransform(HumanBodyBones.Spine);
        head = anim.GetBoneTransform(HumanBodyBones.Head);

        lThigh = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        rThigh = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        lShin = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        rShin = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        lFoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
        rFoot = anim.GetBoneTransform(HumanBodyBones.RightFoot);

        lUpperArm = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        rUpperArm = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
    }

    void CacheBindPose()
    {
        if (hips) { hipsBindR = hips.localRotation; hipsBindPos = hips.localPosition; }
        if (spine) spineBindR = spine.localRotation;
        if (head) headBindR = head.localRotation;

        if (lThigh) lThighBindR = lThigh.localRotation;
        if (rThigh) rThighBindR = rThigh.localRotation;
        if (lShin) lShinBindR = lShin.localRotation;
        if (rShin) rShinBindR = rShin.localRotation;
        if (lFoot) lFootBindR = lFoot.localRotation;
        if (rFoot) rFootBindR = rFoot.localRotation;

        if (lUpperArm) lUpperBindR = lUpperArm.localRotation;
        if (rUpperArm) rUpperBindR = rUpperArm.localRotation;
    }

    void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && !animateInEditMode) return;
#endif
        if (!anim) return;

        float t = Time.time;
#if UNITY_EDITOR
        if (!Application.isPlaying) t = (float)UnityEditor.EditorApplication.timeSinceStartup;
#endif
        float w = Mathf.PI * cadence;

        float pL = w * t + phaseOffset;
        float pR = pL + Mathf.PI;

        float sL = Mathf.Sin(pL);
        float sR = Mathf.Sin(pR);
        float liftL = Mathf.Max(0f, sL);
        float liftR = Mathf.Max(0f, sR);

        if (hips)
        {

            float bounce = (liftL + liftR) * 0.5f * hipBounce;
            hips.localPosition = hipsBindPos + new Vector3(0f, bounce, 0f);

            hips.localRotation = hipsBindR;
        }

        if (spine)
        {
            float yaw = -Mathf.Sin(w * t) * spineCounterYaw;
            spine.localRotation = spineBindR * Quaternion.Euler(0f, yaw, 0f);
        }

        if (lThigh) lThigh.localRotation = lThighBindR * Quaternion.Euler(sL * (kneeBend * 0.25f), 0f, 0f);
        if (rThigh) rThigh.localRotation = rThighBindR * Quaternion.Euler(sR * (kneeBend * 0.25f), 0f, 0f);

        if (lShin) lShin.localRotation = lShinBindR * Quaternion.Euler(liftL * kneeBend, 0f, 0f);
        if (rShin) rShin.localRotation = rShinBindR * Quaternion.Euler(liftR * kneeBend, 0f, 0f);

        if (lFoot)
        {
            float pitch = -Mathf.Clamp(sL, -1f, 1f) * anklePitch;
            float roll = -Mathf.Sin(pL * 2f) * 0.5f * ankleRoll;
            lFoot.localRotation = lFootBindR * Quaternion.Euler(pitch, 0f, roll);
            lFoot.localScale = Vector3.one;
        }
        if (rFoot)
        {
            float pitch = -Mathf.Clamp(sR, -1f, 1f) * anklePitch;
            float roll = -Mathf.Sin(pR * 2f) * 0.5f * ankleRoll;
            rFoot.localRotation = rFootBindR * Quaternion.Euler(pitch, 0f, roll);
            rFoot.localScale = Vector3.one;
        }

        if (lUpperArm)
            lUpperArm.localRotation = lUpperBindR *
                (Quaternion.Euler(-sR * armSwing, 0f, 0f) * Quaternion.Euler(0f, sR * armYaw, 0f));
        if (rUpperArm)
            rUpperArm.localRotation = rUpperBindR *
                (Quaternion.Euler(-sL * armSwing, 0f, 0f) * Quaternion.Euler(0f, sL * armYaw, 0f));

        if (head)
        {
            float hy = Mathf.Sin(w * t) * headYaw;
            float hp = Mathf.Sin(w * t * 0.5f + 0.8f) * headPitch;
            float hr = Mathf.Sin(w * t * 0.7f + 1.3f) * headRoll;
            head.localRotation = headBindR * Quaternion.Euler(hp, hy, hr);
        }
    }
}
