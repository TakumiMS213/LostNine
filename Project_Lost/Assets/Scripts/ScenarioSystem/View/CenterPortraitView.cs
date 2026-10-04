using UnityEngine;
using UnityEngine.UI;
using ScenarioSystem.Adapter;
using ScenarioSystem.Events;
using ScenarioSystem.Model.Actions;

namespace ScenarioSystem.View
{
    /// <summary>
    /// 画面中央に常時配置するポートレートの表示を担当する View。
    /// メッセージウィンドウ側の PortraitView とは独立して動作するが、
    /// 既存 Portrait が Center 位置にいる場合は自動的に非表示になる。
    /// </summary>
    public class CenterPortraitView : MonoBehaviour
    {
        [Header("UI Reference")]
        [Tooltip("画面中央に配置する Image コンポーネント")]
        [SerializeField] private Image portraitImage;

        [Header("章別Portrait（設定時はシナリオの表示指示から独立）")]
        [SerializeField] private LostNoteCharacterDatabase chapterPortraitDatabase;
        [Tooltip("章の画像が未登録の場合に表示する画像")]
        [SerializeField] private Sprite fallbackChapterPortrait;

        private ProgressManager _progressManager;
        private int _displayedChapter = -1;
        private Sprite _currentSprite;
        private bool _isMainPortraitAtCenter;
        private bool _hasMainPortrait;

        private void Awake() => MemorizerTintMask.MarkPortrait(portraitImage);

        private void Start()
        {
            if (chapterPortraitDatabase == null) return;
            // 他のオブジェクトのAwakeで生成された進行管理もここで取得する。
            BindProgressManager();
            RefreshChapterPortrait();
        }

        private void OnEnable()
        {
            if (chapterPortraitDatabase != null)
            {
                _displayedChapter = -1;
                _isMainPortraitAtCenter = false;
                _hasMainPortrait = false;
                BindProgressManager();
                RefreshChapterPortrait();
                return;
            }
            ScenarioEventBus.OnCenterPortraitChanged += HandleCenterPortraitChanged;
            ScenarioEventBus.OnDialogueRequested += HandleDialogueRequested;
            ScenarioEventBus.OnWindowVisibilityChanged += HandleWindowVisibilityChanged;
        }

        private void OnDisable()
        {
            ScenarioEventBus.OnCenterPortraitChanged -= HandleCenterPortraitChanged;
            ScenarioEventBus.OnDialogueRequested -= HandleDialogueRequested;
            ScenarioEventBus.OnWindowVisibilityChanged -= HandleWindowVisibilityChanged;
            if (_progressManager != null) _progressManager.OnProgressChanged -= RefreshChapterPortrait;
            _progressManager = null;
        }

        private void BindProgressManager()
        {
            var progress = ProgressManager.Instance;
            if (_progressManager == progress) return;
            if (_progressManager != null) _progressManager.OnProgressChanged -= RefreshChapterPortrait;
            _progressManager = progress;
            if (_progressManager != null) _progressManager.OnProgressChanged += RefreshChapterPortrait;
        }

        private void RefreshChapterPortrait()
        {
            if (chapterPortraitDatabase == null) return;
            int chapter = _progressManager != null ? _progressManager.CurrentChapter : 1;
            if (_displayedChapter == chapter) return;
            _displayedChapter = chapter;
            _currentSprite = fallbackChapterPortrait;
            if (chapterPortraitDatabase.TryGetByChapter(chapter, out var data)
                && data != null && data.CharacterSprite != null)
                _currentSprite = data.CharacterSprite;
            RefreshVisibility();
        }

        private void HandleCenterPortraitChanged(Sprite sprite)
        {
            _currentSprite = sprite;
            RefreshVisibility();
        }

        private void HandleDialogueRequested(DialogueEventData data)
        {
            // 既存 Portrait が Center にいるかどうかを追跡する
            _hasMainPortrait = data.Portrait != null;
            _isMainPortraitAtCenter = _hasMainPortrait && data.PortraitPosition == PortraitPosition.Center;
            RefreshVisibility();
        }

        private void HandleWindowVisibilityChanged(bool visible)
        {
            if (!visible)
            {
                // ウィンドウが閉じた = 既存 Portrait は Center に戻る
                // → CenterPortrait は競合するので非表示を維持
                _isMainPortraitAtCenter = _hasMainPortrait;
                RefreshVisibility();
            }
        }

        /// <summary>
        /// 表示条件: スプライトが設定されていて、かつ既存 Portrait が Center にいない時のみ表示
        /// </summary>
        private void RefreshVisibility()
        {
            if (portraitImage == null) return;

            bool shouldShow = _currentSprite != null && !_isMainPortraitAtCenter;

            if (shouldShow)
            {
                portraitImage.sprite = _currentSprite;
            }
            // このViewはImageと同じオブジェクトに付いているため、GameObjectを
            // 無効にすると購読も解除され、次の表示要求を受け取れなくなる。
            portraitImage.enabled = shouldShow;
        }
    }
}
