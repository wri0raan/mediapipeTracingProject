using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class LoadSceneOnClick : MonoBehaviour
{
    [Header("빌드 세팅에 등록된 씬 이름")]
    [SerializeField] private string sceneName;

    [Header("옵션")]
    [Tooltip("true면 로딩 중 중복 클릭 방지")]
    [SerializeField] private bool disableButtonWhileLoading = true;

    private Button _btn;
    private bool _isLoading;

    private void Awake()
    {
        _btn = GetComponent<Button>();
        _btn.onClick.AddListener(OnClick);
    }

    private void OnDestroy()
    {
        if (_btn != null) _btn.onClick.RemoveListener(OnClick);
    }

    public void OnClick()
    {
        if (_isLoading) return;

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError($"[LoadSceneOnClick] 씬 이름이 비어있습니다. {gameObject.name}");
            return;
        }

        if (disableButtonWhileLoading) _btn.interactable = false;
        _isLoading = true;

        StartCoroutine(LoadAsync(sceneName));
    }

    private System.Collections.IEnumerator LoadAsync(string target)
    {
        var op = SceneManager.LoadSceneAsync(target, LoadSceneMode.Single);

        while (!op.isDone) yield return null;
    }

    public void SetSceneName(string name) => sceneName = name;
}
