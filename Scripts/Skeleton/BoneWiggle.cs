using UnityEngine;

public class BoneWiggleDance : MonoBehaviour
{
    public Animator anim;
    Transform head, spine, leftArm, rightArm, leftLeg, rightLeg;
    public float speed = 2.5f;
    public float amplitude = 25f;

    void Start()
    {
        if (!anim) anim = GetComponent<Animator>();

        head = anim.GetBoneTransform(HumanBodyBones.Head);
        spine = anim.GetBoneTransform(HumanBodyBones.Spine);
        leftArm = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        rightArm = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
        leftLeg = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        rightLeg = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);

        Debug.Log("[BoneWiggleDance] Init complete!");
    }

    void Update()
    {
        if (!anim) return;

        float t = Time.time * speed;

        if (head)
            head.localRotation = Quaternion.Euler(Mathf.Sin(t) * 10f, Mathf.Cos(t) * amplitude, 0);

        if (spine)
            spine.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.5f) * 10f, 0, Mathf.Cos(t) * 15f);

        if (leftArm)
            leftArm.localRotation = Quaternion.Euler(Mathf.Sin(t * 2f) * amplitude, 0, Mathf.Cos(t * 2f) * 10f);

        if (rightArm)
            rightArm.localRotation = Quaternion.Euler(Mathf.Sin(t * 2f + Mathf.PI) * amplitude, 0, Mathf.Cos(t * 2f) * 10f);

        if (leftLeg)
            leftLeg.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.5f + Mathf.PI) * amplitude * 0.5f, 0, 0);

        if (rightLeg)
            rightLeg.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.5f) * amplitude * 0.5f, 0, 0);
    }
}
