using System.Linq;
using Mediapipe.Tasks.Vision.HandLandmarker;
using UnityEngine;
using UnityEngine.UI;

public class HandEraserController : MonoBehaviour
{
    [Header("Refs")]
    public RawImage dimImage;
    public RenderTexture maskRT;
    public Material dimRevealMat;
    public Shader circleStampShader;
    public MonoBehaviour handRunner;

    [Header("Cursor")]
    public RectTransform handCursorTemplate;
    public Vector2 cursorSize = new(120, 120);
    [Range(0f, 1f)] public float cursorSmooth = 0.18f;

    readonly System.Collections.Generic.List<RectTransform> _handCursors = new();

    [Header("Sparkles")]
    public RectTransform sparkleContainer;
    public Sprite sparkleSprite;
    [Range(0, 200)] public int sparklePool = 60;
    [Range(0f, 0.2f)] public float sparkleSpawnInterval = 0.03f;
    public Vector2 sparkleSize = new(24, 24);
    public Vector2 sparkleSizeJitter = new(10, 10);
    public float sparkleRadiusPx = 40f;
    public float sparkleLife = 0.6f;
    public float sparkleSpeed = 30f;
    [Range(0f, 1f)] public float sparkleAlpha = 0.85f;

    [Header("Brush")]
    [Range(0.005f, 0.25f)] public float radiusUV = 0.08f;
    [Range(0.001f, 0.20f)] public float featherUV = 0.03f;
    [Range(0.01f, 1.00f)] public float strength = 0.25f;
    public bool continuous = false;

    [Header("Auto Reset")]
    public float inactivityResetSec = 3f;
    [Range(0f, 0.01f)] public float moveThresholdUV = 0.002f;

    float _lastEraseTime;
    bool _armedForReset;
    Vector2 _prevUV = new Vector2(-999f, -999f);

    [Header("Multi Hand")]
    [Range(1, 4)] public int maxHands = 3;

    readonly System.Collections.Generic.List<Vector2> _handsUV = new();

    readonly System.Collections.Generic.List<Vector2> _prevUVs = new();

    Material _stampMat;
    Vector2 _lastUV = new Vector2(-1, -1);
    bool _hasHand = false;

    class Spark
    {
        public RectTransform rt;
        public UnityEngine.UI.Image img;
        public float t;
        public float life;
        public Vector2 vel;
        public float startScale;
    }
    readonly System.Collections.Generic.List<Spark> _sparks = new();
    readonly System.Collections.Generic.Queue<Spark> _free = new();
    float _sparkTimer;

    readonly System.Collections.Generic.List<Vector2> _sparkCenters = new();

    void Start()
    {

        if (dimRevealMat != null && maskRT != null)
        {
            dimRevealMat.SetTexture("_MaskTex", maskRT);
            if (dimImage != null) dimImage.material = dimRevealMat;
        }

        _stampMat = new Material(circleStampShader);

        var runner = handRunner;
        if (runner != null)
        {
            var evt = runner.GetType().GetEvent("OnHandResult");
            if (evt != null)
            {
                System.Action<HandLandmarkerResult, long> handler = OnHandResult;
                evt.AddEventHandler(runner, handler);
            }
            else
            {
                Debug.LogError("HandLandmarkerRunner에 OnHandResult 이벤트가 없습니다.");
            }
        }

        ClearMask();

        if (handCursorTemplate != null)
        {
            handCursorTemplate.sizeDelta = cursorSize;
            handCursorTemplate.gameObject.SetActive(false);
        }

        BuildSparkles();
        if (sparkleContainer != null) sparkleContainer.SetAsLastSibling();

        _lastEraseTime = Time.unscaledTime;
        _armedForReset = false;

    }

    void OnDestroy()
    {
        if (_stampMat) Destroy(_stampMat);
    }

    public void ClearMask()
    {
        if (maskRT == null) return;
        var active = RenderTexture.active;
        RenderTexture.active = maskRT;
        GL.Clear(false, true, Color.black);
        RenderTexture.active = active;
    }

    Vector2 UvToAnchoredPos(RectTransform target, Vector2 uv)
    {

        var r = target.rect;
        float x = (uv.x - 0.5f) * r.width;
        float y = (uv.y - 0.5f) * r.height;
        return new Vector2(x, y);
    }

    void OnHandResult(HandLandmarkerResult result, long ts)
    {
        try
        {
            _handsUV.Clear();
            _hasHand = false;

            var t = result.GetType();
            var fld = t.GetField("handLandmarks");
            object handLmsObj = fld != null ? fld.GetValue(result) : null;
            if (handLmsObj == null) return;

            var handList = new System.Collections.Generic.List<object>();
            if (handLmsObj is System.Array arr)
            {
                foreach (var it in arr) handList.Add(it);
            }
            else if (handLmsObj is System.Collections.IList ilist)
            {
                foreach (var it in ilist) handList.Add(it);
            }

            int take = Mathf.Min(maxHands, handList.Count);
            for (int h = 0; h < take; h++)
            {
                var hand = handList[h];
                object lms = null;
                var ht = hand.GetType();
                var candProps = new[] { "landmarks", "Landmarks", "value", "Value", "items", "Items" };
                foreach (var name in candProps)
                {
                    var p = ht.GetProperty(name);
                    if (p != null) { lms = p.GetValue(hand); if (lms != null) break; }
                    var f = ht.GetField(name);
                    if (f != null) { lms = f.GetValue(hand); if (lms != null) break; }
                }
                if (lms == null) lms = hand;

                System.Func<int, object> getAt = (idx) => null;
                int lmCount = 0;

                if (lms is System.Array lmArr)
                {
                    lmCount = lmArr.Length;
                    getAt = (idx) => (idx >= 0 && idx < lmCount) ? lmArr.GetValue(idx) : null;
                }
                else if (lms is System.Collections.IList lmList)
                {
                    lmCount = lmList.Count;
                    getAt = (idx) => (idx >= 0 && idx < lmCount) ? lmList[idx] : null;
                }
                else
                {
                    var enumObj = (lms as System.Collections.IEnumerable);
                    if (enumObj != null)
                    {
                        var tmp = new System.Collections.Generic.List<object>();
                        foreach (var it in enumObj) tmp.Add(it);
                        lmCount = tmp.Count;
                        getAt = (idx) => (idx >= 0 && idx < lmCount) ? tmp[idx] : null;
                    }
                }
                if (lmCount <= 0) continue;

                System.Func<object, bool, float> getCoord = (lmOne, getX) =>
                {
                    if (lmOne == null) return 0f;
                    var lt = lmOne.GetType();
                    var name = getX ? "x" : "y";
                    var p = lt.GetProperty(name) ?? lt.GetProperty(name.ToUpper()) ?? lt.GetProperty(char.ToUpper(name[0]) + name.Substring(1));
                    if (p != null)
                    {
                        var v = p.GetValue(lmOne);
                        if (v is float xf) return xf;
                        if (v is double xd) return (float)xd;
                    }
                    var f = lt.GetField(name) ?? lt.GetField(name.ToUpper()) ?? lt.GetField(char.ToUpper(name[0]) + name.Substring(1));
                    if (f != null)
                    {
                        var v = f.GetValue(lmOne);
                        if (v is float xf) return xf;
                        if (v is double xd) return (float)xd;
                    }
                    return 0f;
                };

                int[] useIdx = { 0, 1, 5, 9, 13, 17 };
                float ux = 0f, uy = 0f; int cnt = 0;
                foreach (var i in useIdx)
                {
                    if (i >= 0 && i < lmCount)
                    {
                        var one = getAt(i);
                        ux += getCoord(one, true);
                        uy += getCoord(one, false);
                        cnt++;
                    }
                }
                if (cnt == 0) continue;

                ux /= cnt; uy /= cnt;
                uy = 1f - uy;

                _handsUV.Add(new Vector2(ux, uy));
            }

            while (_prevUVs.Count < _handsUV.Count) _prevUVs.Add(new Vector2(-999f, -999f));
            if (_prevUVs.Count > _handsUV.Count) _prevUVs.RemoveRange(_handsUV.Count, _prevUVs.Count - _handsUV.Count);

            _hasHand = _handsUV.Count > 0;
            if (_hasHand)
                _lastUV = _handsUV[0];
        }
        catch
        {
            _hasHand = false;
        }
    }

    void LateUpdate()
    {

        if (maskRT == null || _stampMat == null) return;

        bool didEraseThisFrame = false;

        if (_hasHand)
        {

            _stampMat.SetVector("_CenterUV", _lastUV);
            _stampMat.SetFloat("_Radius", radiusUV);
            _stampMat.SetFloat("_Feather", featherUV);
            float applied = continuous ? strength * Time.deltaTime * 60f : strength;
            _stampMat.SetFloat("_Strength", applied);

            bool movedAny = false;
            float thr2 = moveThresholdUV * moveThresholdUV;

            for (int i = 0; i < _handsUV.Count; i++)
            {
                var cur = _handsUV[i];
                var prev = (i < _prevUVs.Count) ? _prevUVs[i] : new Vector2(-999f, -999f);
                bool hasPrev = prev.x > -0.5f;

                if (hasPrev && (cur - prev).sqrMagnitude > thr2)
                    movedAny = true;

                if (i < _prevUVs.Count) _prevUVs[i] = cur;
            }

            if (movedAny || continuous)
            {
                var tmp = RenderTexture.GetTemporary(maskRT.descriptor);
                Graphics.Blit(maskRT, tmp);

                bool wroteAny = false;
                for (int h = 0; h < _handsUV.Count; h++)
                {
                    var uv = _handsUV[h];
                    _stampMat.SetVector("_CenterUV", uv);
                    _stampMat.SetTexture("_PrevTex", tmp);

                    Graphics.Blit(tmp, maskRT, _stampMat);
                    Graphics.Blit(maskRT, tmp);
                    wroteAny = true;
                }

                RenderTexture.ReleaseTemporary(tmp);
                if (wroteAny) didEraseThisFrame = true;
            }

        }

        var dimRT = dimImage != null ? dimImage.rectTransform : null;
        Vector2 cursorPosForSparkles = Vector2.zero;

        if (_hasHand && dimRT != null)
        {

            EnsureCursorCount(_handsUV.Count);

            for (int i = 0; i < _handsUV.Count; i++)
            {
                var rt = _handCursors[i];
                Vector2 targetPos = UvToAnchoredPos(dimRT, _handsUV[i]);

                Vector2 cur = rt.anchoredPosition;
                Vector2 next = (cursorSmooth > 0f)
                    ? Vector2.Lerp(cur, targetPos, 1f - Mathf.Pow(1f - cursorSmooth, Time.unscaledDeltaTime * 60f))
                    : targetPos;

                rt.anchoredPosition = next;

                if (i == 0) cursorPosForSparkles = next;
            }

        }
        else
        {

            EnsureCursorCount(0);
            cursorPosForSparkles = Vector2.zero;
        }

        _sparkCenters.Clear();
        if (_hasHand && _handCursors != null && _handCursors.Count > 0)
        {
            for (int i = 0; i < _handCursors.Count; i++)
            {
                var rt = _handCursors[i];
                if (rt != null && rt.gameObject.activeSelf)
                    _sparkCenters.Add(rt.anchoredPosition);
            }
        }
        UpdateSparkles(_sparkCenters, Time.unscaledDeltaTime);

        if (didEraseThisFrame)
        {
            _lastEraseTime = Time.unscaledTime;
            _armedForReset = true;
        }

        if (_armedForReset)
        {
            float idle = Time.unscaledTime - _lastEraseTime;
            if (idle >= inactivityResetSec)
            {
                ClearMask();
                _armedForReset = false;
                _lastEraseTime = Time.unscaledTime;
                _prevUV = new Vector2(-999f, -999f);

                for (int i = 0; i < _prevUVs.Count; i++) _prevUVs[i] = new Vector2(-999f, -999f);

            }
        }

    }

    void EnsureCursorCount(int count)
    {
        if (handCursorTemplate == null) return;

        while (_handCursors.Count < count)
        {

            var parent = (dimImage != null) ? dimImage.rectTransform : handCursorTemplate.parent;
            var go = Instantiate(handCursorTemplate.gameObject, parent);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = cursorSize;
            go.SetActive(true);

            rt.SetAsLastSibling();

            if (sparkleContainer != null) sparkleContainer.SetAsLastSibling();

            _handCursors.Add(rt);
        }

        for (int i = 0; i < _handCursors.Count; i++)
        {
            bool on = i < count;
            if (_handCursors[i].gameObject.activeSelf != on)
                _handCursors[i].gameObject.SetActive(on);
        }
    }

    void BuildSparkles()
    {
        if (sparkleContainer == null || sparkleSprite == null) return;

        foreach (var s in _sparks) if (s?.rt) Destroy(s.rt.gameObject);
        _sparks.Clear(); _free.Clear();

        for (int i = 0; i < sparklePool; i++)
        {
            var go = new GameObject("spark", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(sparkleContainer, false);
            var rt = go.GetComponent<RectTransform>();
            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            img.sprite = sparkleSprite;
            img.color = new Color(1, 1, 1, 0);
            rt.sizeDelta = sparkleSize;
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;

            _free.Enqueue(new Spark { rt = rt, img = img, t = sparkleLife, life = sparkleLife });
        }
    }

    Spark Rent()
    {
        if (_free.Count > 0) return _free.Dequeue();

        var s = _sparks.Count > 0 ? _sparks[0] : null;
        if (s != null) { _sparks.RemoveAt(0); }
        return s;
    }

    void Return(Spark s)
    {
        if (s == null) return;
        s.img.color = new Color(1, 1, 1, 0);
        s.t = s.life = sparkleLife;
        _free.Enqueue(s);
    }

    void SpawnSparkle(Vector2 centerAnchoredPos)
    {

        if (sparkleContainer == null || sparkleSprite == null) return;
        var s = Rent();
        if (s == null) return;

        var ang = Random.value * Mathf.PI * 2f;
        var rad = sparkleRadiusPx * Mathf.Sqrt(Random.value);
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

    void UpdateSparkles(System.Collections.Generic.IList<Vector2> centers, float dt)
    {

        _sparkTimer += dt;

        bool canSpawn = centers != null && centers.Count > 0;
        while (canSpawn && _sparkTimer >= sparkleSpawnInterval)
        {
            _sparkTimer -= sparkleSpawnInterval;
            for (int i = 0; i < centers.Count; i++)
            {
                SpawnSparkle(centers[i]);
            }
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
