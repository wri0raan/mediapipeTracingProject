using UnityEngine;
using Mediapipe.Tasks.Vision.HandLandmarker;

namespace Mediapipe.Unity
{

    [RequireComponent(typeof(HandLandmarkerResultAnnotationController))]
    public class HandResultBridge : MonoBehaviour
    {
        public HandSpriteFollower follower;

        private HandLandmarkerResultAnnotationController _ann;

        void Awake()
        {
            _ann = GetComponent<HandLandmarkerResultAnnotationController>();
        }

        public void DrawLater(HandLandmarkerResult result)
        {
            if (follower != null)
                follower.DrawLater(result);
        }
    }
}
