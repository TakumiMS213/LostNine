using UnityEngine;
using System_Script.Flow;

/// <summary>
/// メインシーンの起点コントローラー。
/// シーン起動時に ProgressManager.CurrentPhase を参照し、
/// GameFlowDirector.PlaySequenceForCurrentProgress() を呼ぶ。
///
/// 9章分のシーケンスはすべて GameFlowDirector の overrideSequences に登録する。
/// （Ch1_Prologue, Ch2_Prologue … Ch9_Prologue, Ch1_Presentation …）
///
/// フェーズ分岐:
///   Prologue     → GFD.PlaySequenceForCurrentProgress()
///   Presentation → Fragmentを非表示 + GFD.PlaySequenceForCurrentProgress()
///   Epilogue     → GFD.PlaySequenceForCurrentProgress()（通常はPresentation内で完結）
///   それ以外     → Portrait クリック待ち（ComuStartandEndManagerが管理）
/// </summary>
public class MainSceneFlowController : MonoBehaviour
{
    [Header("GameFlowDirector")]
    [SerializeField] private GameFlowDirector flowDirector;

    [Header("メモライザー連携")]
    [Tooltip("カメラD&Dアイテムの GameObject（メモライザーフラグONで有効化）")]
    [SerializeField] private GameObject cameraItem;

    [Tooltip("記憶の欠片システム（Presentationで HideAll を呼ぶ）")]
    [SerializeField] private MemoryFragmentSystem memoryFragmentSystem;

    // ── プライベート ──────────────────────────────────────────────
    private System.Action _onKeywordThresholdCached;

    // ── Unity Lifecycle ──────────────────────────────────────────

    private void Start()
    {
        var pm = ProgressManager.Instance;
        if (pm == null)
        {
            Debug.LogWarning("[MainSceneFlowController] ProgressManager not found.");
            return;
        }

        Debug.Log($"[MainSceneFlowController] Start: Ch{pm.CurrentChapter} / {pm.CurrentPhase}");

        switch (pm.CurrentPhase)
        {
            case GamePhase.Prologue:
                // GFD の overrideSequences から Ch{N}_Prologue を自動解決して再生
                flowDirector?.PlaySequenceForCurrentProgress();
                break;

            case GamePhase.Dialogue:
            case GamePhase.Extraction:
                // Portrait クリックは ComuStartandEndManager が管理するため何もしない
                break;

            case GamePhase.Presentation:
                InitPresentation();
                break;

            case GamePhase.Epilogue:
                // 通常は PresentationSequence 内の ProgressStep → TalkStep で流れる
                // Main シーンが Epilogue フェーズで直接ロードされた場合のフォールバック
                flowDirector?.PlaySequenceForCurrentProgress();
                break;

            default:
                Debug.Log($"[MainSceneFlowController] Phase {pm.CurrentPhase}: no auto-start action.");
                break;
        }

        // メモライザーフラグが既に ON であれば D&D アイテムをアクティブ化
        UpdateCameraItemState(pm.AllKeywordsCollected);

        // フラグ変化のリスナー登録（フィールドに保持して OnDestroy で解除）
        _onKeywordThresholdCached = () => UpdateCameraItemState(true);
        pm.OnKeywordThresholdReached += _onKeywordThresholdCached;
    }

    private void OnDestroy()
    {
        if (ProgressManager.Instance != null && _onKeywordThresholdCached != null)
            ProgressManager.Instance.OnKeywordThresholdReached -= _onKeywordThresholdCached;
    }

    // ── フェーズ別初期化 ─────────────────────────────────────────

    private void InitPresentation()
    {
        // 記憶の欠片を非表示（プレゼンテーション中は消す）
        if (memoryFragmentSystem != null)
            memoryFragmentSystem.HideAll();

        // カメラアイテムを非表示（もう不要）
        if (cameraItem != null)
            cameraItem.SetActive(false);

        // GFD の overrideSequences から Ch{N}_Presentation を自動解決して再生
        if (flowDirector != null && !flowDirector.PlaySequenceForCurrentProgress())
            Debug.LogWarning("[MainSceneFlowController] Presentation sequence not found in GFD overrides.");
    }

    // ── カメラアイテム状態更新 ────────────────────────────────────

    private void UpdateCameraItemState(bool active)
    {
        if (cameraItem == null) return;
        Debug.Log($"[MainSceneFlowController] AllKeywordsCollected={active}");
        // DragToSceneItem 内部で OnKeywordThresholdReached を購読済みのため特別な操作不要
        // 必要であれば cameraItem.SetActive(active) に変更可能
    }
}
