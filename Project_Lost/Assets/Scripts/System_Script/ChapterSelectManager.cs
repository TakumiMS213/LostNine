using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ChapterSelectシーンのUI管理。
/// Epilogue終了後にこのシーンに遷移する。
/// 「次のチャプターへ」または「タイトルへ戻る」を選択可能。
/// 最終チャプター（9章）後は「次のチャプターへ」ボタンを無効化する。
/// </summary>
public class ChapterSelectManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button nextChapterButton;
    [SerializeField] private Button returnToTitleButton;

    [Header("Optional UI")]
    [Tooltip("完了したチャプター番号を表示するテキスト")]
    [SerializeField] private TMP_Text chapterCompleteText;

    [Tooltip("次のチャプターボタンが無効の時に表示するテキスト（例：最終チャプター）")]
    [SerializeField] private TMP_Text lastChapterNotice;

    private void Start()
    {
        var pm = ProgressManager.Instance;
        if (pm == null)
        {
            Debug.LogError("[ChapterSelectManager] ProgressManager not found.");
            return;
        }

        int completedChapter = pm.CurrentChapter;
        bool isLast = pm.IsLastChapter;

        // チャプター完了表示
        if (chapterCompleteText != null)
            chapterCompleteText.text = $"Chapter {completedChapter} Complete";

        // 最終チャプター通知
        if (lastChapterNotice != null)
            lastChapterNotice.gameObject.SetActive(isLast);

        // ボタン設定
        if (nextChapterButton != null)
        {
            nextChapterButton.interactable = !isLast;
            nextChapterButton.onClick.AddListener(OnNextChapter);
        }

        if (returnToTitleButton != null)
        {
            returnToTitleButton.onClick.AddListener(OnReturnToTitle);
        }
    }

    private void OnDestroy()
    {
        if (nextChapterButton != null) nextChapterButton.onClick.RemoveListener(OnNextChapter);
        if (returnToTitleButton != null) returnToTitleButton.onClick.RemoveListener(OnReturnToTitle);
    }

    private void OnNextChapter()
    {
        var pm = ProgressManager.Instance;
        if (pm == null || pm.IsLastChapter) return;

        int nextChapter = pm.CurrentChapter + 1;
        Debug.Log($"[ChapterSelectManager] Starting Chapter {nextChapter}");
        pm.StartFromChapter(nextChapter);
    }

    private void OnReturnToTitle()
    {
        Debug.Log("[ChapterSelectManager] Returning to Title");
        if (ProgressManager.Instance != null)
            ProgressManager.Instance.GoToTitle();
    }
}
