using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// タイトル画面のチャプター選択ボタンにアタッチするヘルパー。
/// ボタンクリック時に ProgressManager.StartFromChapter() を呼び出す。
/// </summary>
[RequireComponent(typeof(Button))]
public class ChapterStartButton : MonoBehaviour
{
    [Tooltip("開始するチャプター番号")]
    [SerializeField] private int chapter = 1;

    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    private void OnDestroy()
    {
        var btn = GetComponent<Button>();
        if (btn != null) btn.onClick.RemoveListener(OnClick);
    }

    private void OnClick()
    {
        if (ProgressManager.Instance != null)
        {
            ProgressManager.Instance.StartFromChapter(chapter);
        }
        else
        {
            Debug.LogError("[ChapterStartButton] ProgressManager not found. Ensure it exists in the scene or is DontDestroyOnLoad.");
        }
    }
}
