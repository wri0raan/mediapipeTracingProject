using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Mediapipe.Tasks.Vision.HandLandmarker;

namespace Mediapipe.Unity
{
    [RequireComponent(typeof(UnityEngine.RectTransform))]
    public class HandSpriteFollower : MonoBehaviour
    {
        [Header("Targets (필수)")]
        public UnityEngine.RectTransform screenRect;
        public UnityEngine.RectTransform leftHandImg;
        public UnityEngine.RectTransform rightHandImg;

        [Header("Options")]
        public bool mirrorX = false;
        [Range(0f, 1f)] public float smooth = 0.25f;
        public float scaleByDepth = 0f;

        [Header("Trail (잔상)")]
        public int trailCount = 10;
        [Range(0f, 1f)] public float trailAlphaStart = 0.6f;
        public Vector2 trailScale = Vector2.one;

        private readonly Queue<UnityEngine.RectTransform> _trailL = new Queue<UnityEngine.RectTransform>();
        private readonly Queue<UnityEngine.RectTransform> _trailR = new Queue<UnityEngine.RectTransform>();
        private UnityEngine.UI.Image _leftImg, _rightImg;
        private Vector2 _curL, _curR;

        public void DrawLater(HandLandmarkerResult result)
        {
            bool hasL = TryGetHandCenter(result, "Left", out var lc, out var lz);
            bool hasR = TryGetHandCenter(result, "Right", out var rc, out var rz);

            if (hasL)
            {
                var p = FromNormalizedToAnchored(lc);
                _curL = Vector2.Lerp(_curL, p, 1f - smooth);
                MoveAndTrail(leftHandImg, _curL, lz, _trailL);
            }
            else Hide(leftHandImg, _trailL);

            if (hasR)
            {
                var p = FromNormalizedToAnchored(rc);
                _curR = Vector2.Lerp(_curR, p, 1f - smooth);
                MoveAndTrail(rightHandImg, _curR, rz, _trailR);
            }
            else Hide(rightHandImg, _trailR);
        }

        void Awake()
        {
            if (!screenRect) screenRect = GetComponent<UnityEngine.RectTransform>();
            _leftImg = leftHandImg ? leftHandImg.GetComponent<UnityEngine.UI.Image>() : null;
            _rightImg = rightHandImg ? rightHandImg.GetComponent<UnityEngine.UI.Image>() : null;
        }

        Vector2 FromNormalizedToAnchored(Vector2 norm)
        {
            var rect = screenRect.rect;
            float nx = mirrorX ? (1f - norm.x) : norm.x;
            float x = (nx - 0.5f) * rect.width;
            float y = (0.5f - norm.y) * rect.height;
            return new Vector2(x, y);
        }

        bool TryGetHandCenter(HandLandmarkerResult res, string label, out Vector2 center, out float depthZ)
        {
            center = Vector2.zero; depthZ = 0f;

            if (!TryGetPropertyAsIList(res, new[] { "Handedness", "Handednesses" }, out var handednessList)) return false;

            if (!TryGetPropertyAsIList(res, new[] { "Landmarks", "HandLandmarks", "MultiHandLandmarks" }, out var landmarksList)) return false;

            if (handednessList.Count == 0 || landmarksList.Count == 0) return false;

            int best = -1;
            float bestScore = -1f;

            for (int i = 0; i < handednessList.Count; i++)
            {

                if (!(handednessList[i] is IList catList) || catList.Count == 0) continue;

                var cat0 = catList[0];
                if (cat0 == null) continue;

                string name = GetPropString(cat0, "CategoryName") ?? GetPropString(cat0, "DisplayName");
                float score = GetPropFloat(cat0, "Score", 0f);

                if (name == label && score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            if (best < 0 || best >= landmarksList.Count) return false;

            if (!(landmarksList[best] is IList lmList) || lmList.Count == 0) return false;

            int[] idx = { 0, 5, 9, 13, 17 };
            float x = 0, y = 0, z = 0; int n = 0;
            foreach (var id in idx)
            {
                if (id < 0 || id >= lmList.Count) continue;
                var lm = lmList[id];
                if (lm == null) continue;

                x += GetPropFloat(lm, "X", 0f);
                y += GetPropFloat(lm, "Y", 0f);
                z += GetPropFloat(lm, "Z", 0f);
                n++;
            }
            if (n == 0) return false;

            center = new Vector2(x / n, y / n);
            depthZ = z / n;
            return true;
        }

        void MoveAndTrail(UnityEngine.RectTransform target, Vector2 anchoredPos, float zNorm, Queue<UnityEngine.RectTransform> trailQ)
        {
            if (!target) return;
            target.gameObject.SetActive(true);
            target.anchoredPosition = anchoredPos;

            if (scaleByDepth != 0f)
            {
                float s = 1f + (-zNorm) * scaleByDepth;
                target.localScale = new Vector3(s, s, 1);
            }

            var ghost = SpawnGhostLike(target);
            ghost.anchoredPosition = anchoredPos;
            ghost.localScale = target.localScale;

            trailQ.Enqueue(ghost);
            while (trailQ.Count > Mathf.Max(0, trailCount))
            {
                var old = trailQ.Dequeue();
                if (old) Destroy(old.gameObject);
            }

            int count = trailQ.Count;
            int i = 0;
            foreach (var g in trailQ.Reverse())
            {
                float t = (count <= 1) ? 1f : (1f - (i / (float)(count - 1)));
                var img = g.GetComponent<UnityEngine.UI.Image>();
                if (img)
                {
                    var c = img.color; c.a = trailAlphaStart * t;
                    img.color = c;
                }
                g.sizeDelta = target.sizeDelta * Vector2.Lerp(trailScale, Vector2.one, t);
                i++;
            }
        }

        void Hide(UnityEngine.RectTransform main, Queue<UnityEngine.RectTransform> trailQ)
        {
            if (main) main.gameObject.SetActive(false);
            while (trailQ.Count > 0)
            {
                var old = trailQ.Dequeue();
                if (old) Destroy(old.gameObject);
            }
        }

        UnityEngine.RectTransform SpawnGhostLike(UnityEngine.RectTransform src)
        {
            var go = new GameObject(src.name + "_ghost", typeof(UnityEngine.RectTransform), typeof(UnityEngine.UI.Image));
            var rt = go.GetComponent<UnityEngine.RectTransform>();
            rt.SetParent(src.parent, false);
            rt.anchorMin = src.anchorMin;
            rt.anchorMax = src.anchorMax;
            rt.pivot = src.pivot;
            rt.sizeDelta = src.sizeDelta;

            var img = go.GetComponent<UnityEngine.UI.Image>();
            var srcImg = src.GetComponent<UnityEngine.UI.Image>();
            img.sprite = srcImg ? srcImg.sprite : null;
            img.raycastTarget = false;

            UnityEngine.Color c = srcImg ? srcImg.color : UnityEngine.Color.white;
            c.a = 0f;
            img.color = c;

            return rt;
        }

        static bool TryGetPropertyAsIList(object obj, string[] names, out IList value)
        {
            value = null;
            var t = obj.GetType();
            foreach (var nm in names)
            {
                var p = t.GetProperty(nm, BindingFlags.Instance | BindingFlags.Public);
                if (p != null)
                {
                    var v = p.GetValue(obj);
                    if (v is IList list) { value = list; return true; }
                }
            }
            return false;
        }

        static string GetPropString(object obj, string propName)
        {
            var p = obj.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public);
            return p?.GetValue(obj)?.ToString();
        }

        static float GetPropFloat(object obj, string propName, float defaultValue)
        {
            var p = obj.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public);
            if (p == null) return defaultValue;
            var v = p.GetValue(obj);
            if (v == null) return defaultValue;
            try { return System.Convert.ToSingle(v); }
            catch { return defaultValue; }
        }
    }
}
