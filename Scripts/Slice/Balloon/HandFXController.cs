using System.Collections.Generic;
using Mediapipe.Tasks.Vision.HandLandmarker;
using UnityEngine;
using UnityEngine.UI;

namespace Zerah.Slicer
{

    public class HandFXController : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("손을 그릴 기준 UI 영역(배경 패널 RectTransform)")]
        public RectTransform playArea;
        [Tooltip("HandLandmarkerRunner 같은 러너(이벤트에 OnHandResult 있음)")]
        public MonoBehaviour handRunner;

        [Header("Cursor")]
        public RectTransform handCursorTemplate;
        public Vector2 cursorSize = new(250, 250);
        [Range(0f, 1f)] public float cursorSmooth = 0.18f;

        [Header("Sparkles")]
        public RectTransform sparkleContainer;
        public Sprite sparkleSprite;
        [Range(0, 200)] public int sparklePool = 60;
        [Range(0f, 0.2f)] public float sparkleSpawnInterval = 0.03f;
        public Vector2 sparkleSize = new(50, 50);
        public Vector2 sparkleSizeJitter = new(20, 20);
        public float sparkleRadiusPx = 40f;
        public float sparkleLife = 0.6f;
        public float sparkleSpeed = 30f;
        [Range(0f, 1f)] public float sparkleAlpha = 0.87f;

        [Header("Hands")]
        [Range(1, 10)] public int maxHands = 2;

        [Header("Hit (Hand ↔ Balloon)")]
        [Tooltip("손 커서 반지름(px). 빈값이면 커서 width의 절반을 사용")]
        public float handRadiusPx = 0f;
        [Tooltip("풍선 반지름 보정(px). 히트 판정을 약간 느슨/빡빡하게")]
        public float balloonRadiusExtraPx = 0f;

        private readonly List<Vector2> _handsUV = new();
        private readonly List<RectTransform> _handCursors = new();

        private class Spark
        {
            public RectTransform rt;
            public Image img;
            public float t;
            public float life;
            public Vector2 vel;
            public float startScale;
        }
        private readonly List<Spark> _sparks = new();
        private readonly Queue<Spark> _free = new();
        private float _sparkTimer;
        private readonly List<Vector2> _centers = new();

        private void Awake()
        {
            if (handCursorTemplate != null)
            {
                handCursorTemplate.sizeDelta = cursorSize;
                handCursorTemplate.gameObject.SetActive(false);
            }

            if (handRunner != null)
            {
                var evt = handRunner.GetType().GetEvent("OnHandResult");
                if (evt != null)
                {
                    System.Action<HandLandmarkerResult, long> cb = OnHandResult;
                    evt.AddEventHandler(handRunner, cb);
                }
                else
                {
                    Debug.LogError("handRunner에 OnHandResult 이벤트가 없습니다. 러너 오브젝트 확인하세요.");
                }
            }

            BuildSparkles();
        }

        private void OnHandResult(HandLandmarkerResult result, long ts)
        {
            _handsUV.Clear();

            if (!TryGetMemberIgnoreCase(result, "handLandmarks", out object listObj))
                return;
            if (listObj == null) return;

            var hands = new List<object>();
            if (listObj is System.Array arr) foreach (var it in arr) hands.Add(it);
            else if (listObj is System.Collections.IList il) foreach (var it in il) hands.Add(it);
            else
            {
                var en = listObj as System.Collections.IEnumerable;
                if (en != null) { foreach (var it in en) hands.Add(it); }
            }
            if (hands.Count == 0) return;

            int take = Mathf.Min(maxHands, hands.Count);
            for (int h = 0; h < take; h++)
            {
                var hand = hands[h];

                object lms = null;
                string[] cand = { "landmarks", "Landmarks", "value", "Value", "items", "Items" };
                var ht = hand.GetType();
                foreach (var n in cand)
                {
                    var p = ht.GetProperty(n);
                    if (p != null) { lms = p.GetValue(hand); if (lms != null) break; }
                    var f = ht.GetField(n);
                    if (f != null) { lms = f.GetValue(hand); if (lms != null) break; }
                }
                if (lms == null) lms = hand;

                System.Func<int, object> getAt = (idx) => null;
                int lmCount = 0;
                if (lms is System.Array lmArr)
                {
                    lmCount = lmArr.Length;
                    getAt = (i) => (i >= 0 && i < lmCount) ? lmArr.GetValue(i) : null;
                }
                else if (lms is System.Collections.IList lmList)
                {
                    lmCount = lmList.Count;
                    getAt = (i) => (i >= 0 && i < lmCount) ? lmList[i] : null;
                }
                else
                {
                    var en = lms as System.Collections.IEnumerable;
                    if (en != null)
                    {
                        var tmp = new List<object>(); foreach (var it in en) tmp.Add(it);
                        lmCount = tmp.Count; getAt = (i) => (i >= 0 && i < lmCount) ? tmp[i] : null;
                    }
                }
                if (lmCount == 0) continue;

                float GetCoord(object lm, bool isX)
                {
                    if (lm == null) return 0f;
                    string name = isX ? "x" : "y";
                    var lt = lm.GetType();
                    var p = lt.GetProperty(name) ?? lt.GetProperty(name.ToUpper()) ?? lt.GetProperty(char.ToUpper(name[0]) + name.Substring(1));
                    if (p != null)
                    {
                        var v = p.GetValue(lm);
                        if (v is float xf) return xf;
                        if (v is double xd) return (float)xd;
                    }
                    var f = lt.GetField(name) ?? lt.GetField(name.ToUpper()) ?? lt.GetField(char.ToUpper(name[0]) + name.Substring(1));
                    if (f != null)
                    {
                        var v = f.GetValue(lm);
                        if (v is float xf) return xf;
                        if (v is double xd) return (float)xd;
                    }
                    return 0f;
                }

                int[] idxs = { 0, 1, 5, 9, 13, 17 };
                float sx = 0f, sy = 0f; int c = 0;
                foreach (var i in idxs)
                {
                    if (i >= 0 && i < lmCount)
                    {
                        var one = getAt(i);
                        sx += GetCoord(one, true);
                        sy += GetCoord(one, false);
                        c++;
                    }
                }
                if (c == 0) continue;
                sx /= c; sy /= c;

                sy = 1f - sy;

                _handsUV.Add(new Vector2(sx, sy));
            }
        }

        private bool TryGetMemberIgnoreCase(object obj, string name, out object value)
        {
            value = null;
            if (obj == null) return false;
            var t = obj.GetType();

            var p = t.GetProperty(name) ?? t.GetProperty(char.ToUpper(name[0]) + name.Substring(1))
                    ?? t.GetProperty(name.ToUpper());
            if (p != null) { value = p.GetValue(obj); return true; }

            var f = t.GetField(name) ?? t.GetField(char.ToUpper(name[0]) + name.Substring(1))
                    ?? t.GetField(name.ToUpper());
            if (f != null) { value = f.GetValue(obj); return true; }

            return false;
        }

        private void LateUpdate()
        {

            if (playArea == null || handCursorTemplate == null)
            {
                EnsureCursorCount(0);
                return;
            }

            EnsureCursorCount(_handsUV.Count);

            for (int i = 0; i < _handsUV.Count; i++)
            {
                var uv = _handsUV[i];
                var target = UvToAnchoredPos(playArea, uv);

                var rt = _handCursors[i];
                Vector2 cur = rt.anchoredPosition;
                Vector2 next = (cursorSmooth > 0f)
                    ? Vector2.Lerp(cur, target, 1f - Mathf.Pow(1f - cursorSmooth, Time.unscaledDeltaTime * 60f))
                    : target;

                rt.anchoredPosition = next;
            }

            _centers.Clear();
            for (int i = 0; i < _handCursors.Count; i++)
                if (_handCursors[i].gameObject.activeSelf) _centers.Add(_handCursors[i].anchoredPosition);

            UpdateSparkles(_centers, Time.unscaledDeltaTime);

            CheckBalloonHits();

        }

        private Vector2 UvToAnchoredPos(RectTransform target, Vector2 uv)
        {
            var r = target.rect;
            float x = (uv.x - 0.5f) * r.width;
            float y = (uv.y - 0.5f) * r.height;
            return new Vector2(x, y);
        }

        private void EnsureCursorCount(int count)
        {

            while (_handCursors.Count < count)
            {
                var go = Instantiate(handCursorTemplate.gameObject, handCursorTemplate.parent ?? playArea);
                var rt = go.GetComponent<RectTransform>();
                rt.sizeDelta = cursorSize;
                go.SetActive(true);
                rt.SetAsLastSibling();
                if (sparkleContainer) sparkleContainer.SetAsLastSibling();
                _handCursors.Add(rt);
            }

            for (int i = 0; i < _handCursors.Count; i++)
            {
                bool on = i < count;
                if (_handCursors[i].gameObject.activeSelf != on)
                    _handCursors[i].gameObject.SetActive(on);
            }
        }

        private Vector2 CenterInPlayArea(RectTransform rt)
        {

            Vector3 world = rt.TransformPoint(rt.rect.center);

            Vector3 local = playArea.InverseTransformPoint(world);
            return new Vector2(local.x, local.y);
        }

        private void CheckBalloonHits()
        {
            if (playArea == null) return;
            if (_handCursors.Count == 0) return;
            if (Balloon.Alive.Count == 0) return;

            float rHandDefault = (_handCursors[0].rect.width) * 0.5f;
            float rHand = (handRadiusPx > 0f) ? handRadiusPx : rHandDefault;

            _centers.Clear();
            for (int i = 0; i < _handCursors.Count; i++)
            {
                if (!_handCursors[i].gameObject.activeSelf) continue;
                _centers.Add(CenterInPlayArea(_handCursors[i]));
            }
            if (_centers.Count == 0) return;

            for (int b = Balloon.Alive.Count - 1; b >= 0; b--)
            {
                var bal = Balloon.Alive[b];
                if (bal == null || bal.RectT == null) continue;

                Vector2 cBalloon = CenterInPlayArea(bal.RectT);
                float rBalloon = bal.HitRadiusPx + balloonRadiusExtraPx;

                float rSum = rHand + rBalloon;
                float rSumSqr = rSum * rSum;

                for (int i = 0; i < _centers.Count; i++)
                {
                    Vector2 d = _centers[i] - cBalloon;
                    if (d.sqrMagnitude <= rSumSqr)
                    {
                        bal.Pop();
                        break;
                    }
                }
            }
        }

        private void BuildSparkles()
        {
            if (sparkleContainer == null || sparkleSprite == null) return;

            foreach (var s in _sparks) if (s?.rt) Destroy(s.rt.gameObject);
            _sparks.Clear(); _free.Clear();

            for (int i = 0; i < sparklePool; i++)
            {
                var go = new GameObject("spark", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(sparkleContainer, false);
                var rt = go.GetComponent<RectTransform>();
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                img.sprite = sparkleSprite;
                img.color = new Color(1, 1, 1, 0);
                rt.sizeDelta = sparkleSize;
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.one;

                _free.Enqueue(new Spark { rt = rt, img = img, t = sparkleLife, life = sparkleLife });
            }
        }

        private Spark Rent()
        {
            if (_free.Count > 0) return _free.Dequeue();
            var s = _sparks.Count > 0 ? _sparks[0] : null;
            if (s != null) _sparks.RemoveAt(0);
            return s;
        }

        private void Return(Spark s)
        {
            if (s == null) return;
            s.img.color = new Color(1, 1, 1, 0);
            s.t = s.life = sparkleLife;
            _free.Enqueue(s);
        }

        private void SpawnSparkle(Vector2 centerAnchoredPos)
        {
            if (sparkleContainer == null || sparkleSprite == null) return;
            var s = Rent(); if (s == null) return;

            float ang = Random.value * Mathf.PI * 2f;
            float rad = sparkleRadiusPx * Mathf.Sqrt(Random.value);
            var pos = centerAnchoredPos + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;

            var size = sparkleSize + new Vector2(
                Random.Range(-sparkleSizeJitter.x, sparkleSizeJitter.x),
                Random.Range(-sparkleSizeJitter.y, sparkleSizeJitter.y)
            );

            var dir = (pos - centerAnchoredPos).normalized;
            var vel = dir * (sparkleSpeed * (0.5f + Random.value * 0.8f));

            s.rt.sizeDelta = size;
            s.rt.anchoredPosition = pos;
            s.rt.localScale = Vector3.one * Random.Range(0.8f, 1.2f);
            s.startScale = s.rt.localScale.x;
            s.vel = vel;
            s.t = 0f;
            s.life = sparkleLife;

            var c = s.img.color; c.a = sparkleAlpha; s.img.color = c;

            _sparks.Add(s);
            s.rt.SetAsLastSibling();
        }

        private void UpdateSparkles(IList<Vector2> centers, float dt)
        {
            _sparkTimer += dt;

            bool canSpawn = centers != null && centers.Count > 0;
            while (canSpawn && _sparkTimer >= sparkleSpawnInterval)
            {
                _sparkTimer -= sparkleSpawnInterval;
                for (int i = 0; i < centers.Count; i++)
                    SpawnSparkle(centers[i]);
            }

            for (int i = _sparks.Count - 1; i >= 0; i--)
            {
                var s = _sparks[i];
                s.t += dt;

                if (s.t >= s.life)
                {
                    _sparks.RemoveAt(i);
                    Return(s);
                    continue;
                }

                var p = s.rt.anchoredPosition + s.vel * dt;
                s.rt.anchoredPosition = p;

                float u = s.t / s.life;
                float alpha = sparkleAlpha * (1f - u);
                float scale = Mathf.Lerp(s.startScale, 0.6f * s.startScale, u);

                var col = s.img.color; col.a = alpha; s.img.color = col;
                s.rt.localScale = Vector3.one * scale;
            }
        }
    }
}
