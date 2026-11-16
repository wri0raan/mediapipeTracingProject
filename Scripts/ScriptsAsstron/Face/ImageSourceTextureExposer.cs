using UnityEngine;
using System.Reflection;

public class ImageSourceTextureExposer : MonoBehaviour
{
    public Texture manualTexture;
    public Texture SourceTexture { get; private set; }

    Component _src;
    PropertyInfo _propTex;

    void Awake()
    {
        foreach (var c in GetComponents<Component>())
        {
            if (c == this) continue;
            var t = c.GetType();
            _propTex = t.GetProperty("Texture") ?? t.GetProperty("texture");
            if (_propTex != null && _propTex.PropertyType == typeof(Texture))
            {
                _src = c;
                break;
            }
        }
        if (_src == null)
            Debug.LogWarning("[ImageSourceTextureExposer] Texture 프로퍼티를 가진 컴포넌트를 찾지 못함. manualTexture로 지정하세요.");
    }

    void LateUpdate()
    {
        if (manualTexture != null) { SourceTexture = manualTexture; return; }
        if (_src != null && _propTex != null)
            SourceTexture = _propTex.GetValue(_src, null) as Texture;

        Texture latest = (_src != null && _propTex != null) ? _propTex.GetValue(_src, null) as Texture : null;
        if (latest != null) SourceTexture = latest;

    }
}
