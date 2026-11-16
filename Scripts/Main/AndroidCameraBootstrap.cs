using UnityEngine;
using System.Collections;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

public class AndroidCameraBootstrap : MonoBehaviour
{
    [Header("Auto start webcam after permission")]
    public bool autoStartWebcam = true;

    private WebCamTexture _tex;

    private IEnumerator Start()
    {
        if (!autoStartWebcam)
            yield break;

#if UNITY_ANDROID && !UNITY_EDITOR

        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Permission.RequestUserPermission(Permission.Camera);
            while (!Permission.HasUserAuthorizedPermission(Permission.Camera))
                yield return null;
        }
#endif

        yield return StartCoroutine(StartFrontCamera());
    }

    private IEnumerator StartFrontCamera()
    {

        while (WebCamTexture.devices == null || WebCamTexture.devices.Length == 0)
            yield return null;

        var devices = WebCamTexture.devices;

        int frontIndex = -1;
        for (int i = 0; i < devices.Length; i++)
        {
            Debug.Log($"[Cam] Device {i}: {devices[i].name}, front:{devices[i].isFrontFacing}");
            if (devices[i].isFrontFacing)
            {
                frontIndex = i;
                break;
            }
        }

        if (frontIndex < 0)
            frontIndex = devices.Length - 1;

        var selected = devices[frontIndex];
        Debug.Log($"[Cam] Using FRONT camera: {selected.name}");

        _tex = new WebCamTexture(selected.name, 1280, 720, 30);
        _tex.Play();

        var renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.mainTexture = _tex;

            renderer.material.mainTextureScale = new Vector2(-1f, 1f);
        }

        var rawImage = GetComponent<UnityEngine.UI.RawImage>();
        if (rawImage != null)
        {
            rawImage.texture = _tex;
            rawImage.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
        }

        yield break;
    }

    private void OnDestroy()
    {
        if (_tex != null)
        {
            if (_tex.isPlaying) _tex.Stop();
            Destroy(_tex);
        }
    }
}
