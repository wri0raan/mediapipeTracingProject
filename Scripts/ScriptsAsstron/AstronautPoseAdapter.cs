using UnityEngine;

[ExecuteAlways]
public class AstronautPoseAdapter : MonoBehaviour
{
    [Header("Refs")]
    public Animator animator;
    public Transform modelRoot;

    [Header("Options")]
    public bool flipFacing180 = false;
    public bool mirrorLeftRight = false;
    public float posLerp = 12f;
    public float rotLerp = 12f;
    public Vector3 worldOffset = new Vector3(0, -0.9f, 0);

    Transform hips, spine, chest, neck, head;
    Transform lUpperArm, lLowerArm, lHand, rUpperArm, rLowerArm, rHand;
    Transform lThigh, lShin, lFoot, rThigh, rShin, rFoot;

    Quaternion hipsBindR, spineBindR, chestBindR, neckBindR, headBindR;
    Quaternion lUA_BindR, lLA_BindR, lH_BindR, rUA_BindR, rLA_BindR, rH_BindR;
    Quaternion lThighBindR, lShinBindR, lFootBindR, rThighBindR, rShinBindR, rFootBindR;
    Vector3 hipsBindLocalPos;

    void Reset() { animator = GetComponentInChildren<Animator>(true); modelRoot = transform; }

    void OnEnable()
    {
        if (!animator) animator = GetComponentInChildren<Animator>(true);
        if (!modelRoot) modelRoot = transform;
        CacheBones();
        CacheBindPose();
    }

    void CacheBones()
    {
        hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        neck = animator.GetBoneTransform(HumanBodyBones.Neck);
        head = animator.GetBoneTransform(HumanBodyBones.Head);

        lUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        lLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        lHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        rUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        rLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        rHand = animator.GetBoneTransform(HumanBodyBones.RightHand);

        lThigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        lShin = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        lFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        rThigh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        rShin = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        rFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
    }

    void CacheBindPose()
    {
        if (hips) { hipsBindR = hips.localRotation; hipsBindLocalPos = hips.localPosition; }
        if (spine) spineBindR = spine.localRotation;
        if (chest) chestBindR = chest.localRotation;
        if (neck) neckBindR = neck.localRotation;
        if (head) headBindR = head.localRotation;

        if (lUpperArm) lUA_BindR = lUpperArm.localRotation;
        if (lLowerArm) lLA_BindR = lLowerArm.localRotation;
        if (lHand) lH_BindR = lHand.localRotation;
        if (rUpperArm) rUA_BindR = rUpperArm.localRotation;
        if (rLowerArm) rLA_BindR = rLowerArm.localRotation;
        if (rHand) rH_BindR = rHand.localRotation;

        if (lThigh) lThighBindR = lThigh.localRotation;
        if (lShin) lShinBindR = lShin.localRotation;
        if (lFoot) lFootBindR = lFoot.localRotation;
        if (rThigh) rThighBindR = rThigh.localRotation;
        if (rShin) rShinBindR = rShin.localRotation;
        if (rFoot) rFootBindR = rFoot.localRotation;
    }

    public void ApplyPose(TrackedPose pose, float dt)
    {
        if (pose == null || animator == null) return;

        var facingYaw = flipFacing180 ? 180f : 0f;
        var mirror = mirrorLeftRight ? -1f : 1f;

        var targetRootPos = pose.rootPosition + worldOffset;
        modelRoot.position = Vector3.Lerp(modelRoot.position, targetRootPos, 1f - Mathf.Exp(-posLerp * dt));
        var targetRootRot = Quaternion.Euler(0f, pose.rootYaw + facingYaw, 0f);
        modelRoot.rotation = Quaternion.Slerp(modelRoot.rotation, targetRootRot, 1f - Mathf.Exp(-rotLerp * dt));

        if (spine) spine.localRotation = spineBindR * pose.spineLocalRot;
        if (chest) chest.localRotation = chestBindR * pose.chestLocalRot;
        if (neck) neck.localRotation = neckBindR * pose.neckLocalRot;
        if (head) head.localRotation = headBindR * pose.headLocalRot;

        if (lUpperArm) lUpperArm.localRotation = lUA_BindR * pose.leftUpperArmLocalRot;
        if (lLowerArm) lLowerArm.localRotation = lLA_BindR * pose.leftLowerArmLocalRot;
        if (lHand) lHand.localRotation = lH_BindR * pose.leftHandLocalRot;

        if (rUpperArm) rUpperArm.localRotation = rUA_BindR * pose.rightUpperArmLocalRot;
        if (rLowerArm) rLowerArm.localRotation = rLA_BindR * pose.rightLowerArmLocalRot;
        if (rHand) rHand.localRotation = rH_BindR * pose.rightHandLocalRot;

        if (lThigh) lThigh.localRotation = lThighBindR * pose.leftUpperLegLocalRot;
        if (lShin) lShin.localRotation = lShinBindR * pose.leftLowerLegLocalRot;
        if (lFoot) lFoot.localRotation = lFootBindR * pose.leftFootLocalRot;

        if (rThigh) rThigh.localRotation = rThighBindR * pose.rightUpperLegLocalRot;
        if (rShin) rShin.localRotation = rShinBindR * pose.rightLowerLegLocalRot;
        if (rFoot) rFoot.localRotation = rFootBindR * pose.rightFootLocalRot;

    }

    [ContextMenu("Rebind (capture current pose as offsets)")]
    public void Rebind()
    {
        CacheBindPose();
    }
}

public class TrackedPose
{
    public Vector3 rootPosition;
    public float rootYaw;

    public Quaternion spineLocalRot = Quaternion.identity;
    public Quaternion chestLocalRot = Quaternion.identity;
    public Quaternion neckLocalRot = Quaternion.identity;
    public Quaternion headLocalRot = Quaternion.identity;

    public Quaternion leftUpperArmLocalRot = Quaternion.identity;
    public Quaternion leftLowerArmLocalRot = Quaternion.identity;
    public Quaternion leftHandLocalRot = Quaternion.identity;
    public Quaternion rightUpperArmLocalRot = Quaternion.identity;
    public Quaternion rightLowerArmLocalRot = Quaternion.identity;
    public Quaternion rightHandLocalRot = Quaternion.identity;

    public Quaternion leftUpperLegLocalRot = Quaternion.identity;
    public Quaternion leftLowerLegLocalRot = Quaternion.identity;
    public Quaternion leftFootLocalRot = Quaternion.identity;
    public Quaternion rightUpperLegLocalRot = Quaternion.identity;
    public Quaternion rightLowerLegLocalRot = Quaternion.identity;
    public Quaternion rightFootLocalRot = Quaternion.identity;
}
