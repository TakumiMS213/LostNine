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

    [Header("Chapter Gate")]
    [SerializeField] private bool hideNextChapterButtonOnSpecificChapter = true;
    [SerializeField] private int chapterToHideNextChapterButton = 2;
    [SerializeField] private TMP_Text chapterGateNotice;
    [SerializeField] private TMP_FontAsset chapterGateNoticeFont;
    [SerializeField] private string chapterGateNoticeMessage = "今日遊べるのはここまでです。\nプレイしていただきありがとうございます！";
    [SerializeField] private float chapterGateNoticeFontSize = 48f;
    [SerializeField] private Vector2 chapterGateNoticeSize = new Vector2(1500f, 180f);
    [SerializeField] private Vector2 chapterGateNoticePosition = new Vector2(520f, 0f);

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
        bool shouldHideNextChapterButton = ShouldHideNextChapterButton(completedChapter);

        // チャプター完了表示
        if (chapterCompleteText != null)
            chapterCompleteText.text = $"Chapter {completedChapter} Complete";

        // 最終チャプター通知
        if (lastChapterNotice != null)
            lastChapterNotice.gameObject.SetActive(isLast);

        SetupChapterGateNotice(shouldHideNextChapterButton);

        // ボタン設定
        if (nextChapterButton != null)
        {
            nextChapterButton.gameObject.SetActive(!shouldHideNextChapterButton);
            nextChapterButton.interactable = !isLast && !shouldHideNextChapterButton;
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
        if (pm == null || pm.IsLastChapter || ShouldHideNextChapterButton(pm.CurrentChapter)) return;

        int nextChapter = pm.CurrentChapter + 1;
        Debug.Log($"[ChapterSelectManager] Starting Chapter {nextChapter}");
        pm.StartFromChapter(nextChapter);
    }

    private bool ShouldHideNextChapterButton(int chapter)
    {
        return hideNextChapterButtonOnSpecificChapter && chapter == chapterToHideNextChapterButton;
    }

    private void SetupChapterGateNotice(bool isVisible)
    {
        EnsureChapterGateNotice();

        if (chapterGateNotice == null)
            return;

        chapterGateNotice.gameObject.SetActive(isVisible);
        if (!isVisible)
            return;

        chapterGateNotice.text = chapterGateNoticeMessage;
        chapterGateNotice.fontSize = chapterGateNoticeFontSize;
        chapterGateNotice.alignment = TextAlignmentOptions.Center;
        chapterGateNotice.raycastTarget = false;
        if (chapterGateNoticeFont != null)
        {
            chapterGateNotice.font = chapterGateNoticeFont;
        }
    }

    private void EnsureChapterGateNotice()
    {
        if (chapterGateNotice != null)
            return;

        var parent = nextChapterButton != null && nextChapterButton.transform.parent != null
            ? nextChapterButton.transform.parent
            : transform;

        var noticeObject = new GameObject("ChapterGateNotice", typeof(RectTransform), typeof(TextMeshProUGUI));
        noticeObject.transform.SetParent(parent, false);

        var rect = noticeObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = chapterGateNoticePosition;
        rect.sizeDelta = chapterGateNoticeSize;

        chapterGateNotice = noticeObject.GetComponent<TextMeshProUGUI>();
    }

    private void OnReturnToTitle()
    {
        Debug.Log("[ChapterSelectManager] Returning to Title");
        if (ProgressManager.Instance != null)
            ProgressManager.Instance.GoToTitle();
    }
}
