using System;
using System.Collections.Generic;
using UnityEngine;
using ScenarioSystem.Model;
using ScenarioSystem.Model.Actions;

namespace ScenarioSystem.Events
{
    /// <summary>
    /// シナリオシステムの全イベントを定義する静的イベントバス。
    /// Presenter が発火し、View / Adapter が購読する。
    /// Presenter と View/Adapter の間を完全に疎結合にする中間層。
    /// </summary>
    public static class ScenarioEventBus
    {
        // ──────────────────────────────
        //  Dialogue
        // ──────────────────────────────

        /// <summary>テキスト表示が要求された。</summary>
        public static event Action<DialogueEventData> OnDialogueRequested;

        /// <summary>テキストのタイピングが完了した（View → Presenter への通知）。</summary>
        public static event Action OnTypingCompleted;

        /// <summary>ユーザーが次へ進む操作を行った（View → Presenter への通知）。</summary>
        public static event Action OnAdvanceRequested;

        // ──────────────────────────────
        //  Choice
        // ──────────────────────────────

        /// <summary>選択肢の表示が要求された。</summary>
        public static event Action<List<ChoiceEntry>> OnChoicesRequested;

        /// <summary>選択肢が選択された（View → Presenter への通知）。</summary>
        public static event Action<int> OnChoiceSelected;

        // ──────────────────────────────
        //  Effect
        // ──────────────────────────────

        /// <summary>演出の実行が要求された。</summary>
        public static event Action<EffectAction> OnEffectRequested;

        // ──────────────────────────────
        //  Progress (Adapter 向け)
        // ──────────────────────────────

        /// <summary>進行状態の更新が要求された。</summary>
        public static event Action<ProgressUpdateAction> OnProgressUpdateRequested;

        /// <summary>コミュニケーション切り替えが要求された。</summary>
        public static event Action OnComuToggleRequested;

        /// <summary>アニメーションなしのコミュニケーション切り替えが要求された。</summary>
        public static event Action OnComuToggleInstantRequested;

        // ──────────────────────────────
        //  Keyword
        // ──────────────────────────────

        /// <summary>キーワード有効/無効の状態変更が要求された。</summary>
        public static event Action<bool> OnKeywordStateChanged;

        /// <summary>キーワードがクリックされた（View → Presenter/Adapter への通知）。</summary>
        public static event Action<string> OnKeywordClicked;

        // ──────────────────────────────
        //  Scenario Lifecycle
        // ──────────────────────────────

        /// <summary>シナリオの再生が開始された。</summary>
        public static event Action<ScenarioData> OnScenarioStarted;

        /// <summary>シナリオの再生が終了した。</summary>
        public static event Action<ScenarioData> OnScenarioEnded;

        // ──────────────────────────────
        //  Window Control
        // ──────────────────────────────

        /// <summary>メッセージウィンドウの表示/非表示が要求された。</summary>
        public static event Action<bool> OnWindowVisibilityChanged;

        // ──────────────────────────────
        //  Overlay Control
        // ──────────────────────────────

        /// <summary>オーバーレイテキストの表示が要求された。</summary>
        public static event Action<OverlayEventData> OnOverlayRequested;

        /// <summary>オーバーレイテキストの非表示が要求された。</summary>
        public static event Action OnOverlayDismissed;

        public static event Action<LostNoteEventData> OnLostNoteRequested;

        // ──────────────────────────────
        //  Center Portrait
        // ──────────────────────────────

        /// <summary>画面中央ポートレートのスプライト変更が要求された（null で非表示）。</summary>
        public static event Action<Sprite> OnCenterPortraitChanged;

        public static event Action<TitleLogoEventData> OnTitleLogoRequested;

        // ══════════════════════════════
        //  Raise Methods
        // ══════════════════════════════

        public static void RaiseDialogueRequested(DialogueEventData data)
            => OnDialogueRequested?.Invoke(data);

        public static void RaiseTypingCompleted()
            => OnTypingCompleted?.Invoke();

        public static void RaiseAdvanceRequested()
            => OnAdvanceRequested?.Invoke();

        public static void RaiseChoicesRequested(List<ChoiceEntry> choices)
            => OnChoicesRequested?.Invoke(choices);

        public static void RaiseChoiceSelected(int index)
            => OnChoiceSelected?.Invoke(index);

        public static void RaiseEffectRequested(EffectAction action)
            => OnEffectRequested?.Invoke(action);

        public static void RaiseProgressUpdateRequested(ProgressUpdateAction action)
            => OnProgressUpdateRequested?.Invoke(action);

        public static void RaiseComuToggleRequested()
            => OnComuToggleRequested?.Invoke();

        public static void RaiseComuToggleInstantRequested()
            => OnComuToggleInstantRequested?.Invoke();

        public static void RaiseKeywordStateChanged(bool enabled)
            => OnKeywordStateChanged?.Invoke(enabled);

        public static void RaiseKeywordClicked(string keywordId)
            => OnKeywordClicked?.Invoke(keywordId);

        public static void RaiseScenarioStarted(ScenarioData scenario)
            => OnScenarioStarted?.Invoke(scenario);

        public static void RaiseScenarioEnded(ScenarioData scenario)
            => OnScenarioEnded?.Invoke(scenario);

        public static void RaiseWindowVisibilityChanged(bool visible)
            => OnWindowVisibilityChanged?.Invoke(visible);

        public static void RaiseOverlayRequested(OverlayEventData data)
            => OnOverlayRequested?.Invoke(data);

        public static void RaiseOverlayDismissed()
            => OnOverlayDismissed?.Invoke();

        public static void RaiseLostNoteRequested(LostNoteEventData data)
            => OnLostNoteRequested?.Invoke(data);

        public static void RaiseLostNoteRequested(string keywordId, string title, string description)
            => OnLostNoteRequested?.Invoke(new LostNoteEventData(keywordId, title, description));

        public static void RaiseCenterPortraitChanged(Sprite sprite)
            => OnCenterPortraitChanged?.Invoke(sprite);

        public static void RaiseTitleLogoRequested(TitleLogoEventData data)
            => OnTitleLogoRequested?.Invoke(data);

        // ══════════════════════════════
        //  Cleanup（テスト・シーン遷移時用）
        // ══════════════════════════════

        /// <summary>
        /// 全イベント購読を解除する。
        /// シーン遷移時やテストのクリーンアップで使用。
        /// </summary>
        public static void ClearAll()
        {
            OnDialogueRequested = null;
            OnTypingCompleted = null;
            OnAdvanceRequested = null;
            OnChoicesRequested = null;
            OnChoiceSelected = null;
            OnEffectRequested = null;
            OnProgressUpdateRequested = null;
            OnComuToggleRequested = null;
            OnComuToggleInstantRequested = null;
            OnKeywordStateChanged = null;
            OnKeywordClicked = null;
            OnScenarioStarted = null;
            OnScenarioEnded = null;
            OnWindowVisibilityChanged = null;
            OnOverlayRequested = null;
            OnOverlayDismissed = null;
            OnLostNoteRequested = null;
            OnCenterPortraitChanged = null;
            OnTitleLogoRequested = null;
        }
    }

    // ══════════════════════════════
    //  Event Data Structs
    // ══════════════════════════════

    /// <summary>
    /// DialogueAction の実行時に View へ渡すデータパケット。
    /// SOの参照ではなく値のコピーとして渡し、View がModel に依存しないようにする。
    /// </summary>
    public readonly struct DialogueEventData
    {
        public readonly string SpeakerName;
        public readonly string Text;
        public readonly Sprite Portrait;
        public readonly PortraitPosition PortraitPosition;
        public readonly float TypingSpeed;
        public readonly NameSlideDirection NameSlideDirection;
        public readonly AudioClip VoiceClip;
        public readonly Sprite BackgroundImage;

        public DialogueEventData(
            string speakerName,
            string text,
            Sprite portrait,
            PortraitPosition portraitPosition,
            float typingSpeed,
            NameSlideDirection nameSlideDirection,
            AudioClip voiceClip,
            Sprite backgroundImage)
        {
            SpeakerName = speakerName;
            Text = text;
            Portrait = portrait;
            PortraitPosition = portraitPosition;
            TypingSpeed = typingSpeed;
            NameSlideDirection = nameSlideDirection;
            VoiceClip = voiceClip;
            BackgroundImage = backgroundImage;
        }

        /// <summary>DialogueEntry から DialogueEventData を生成するファクトリ。</summary>
        public static DialogueEventData FromEntry(DialogueEntry entry)
        {
            return new DialogueEventData(
                entry.speakerName,
                entry.text,
                entry.portrait,
                entry.portraitPosition,
                entry.typingSpeed,
                entry.nameSlideDirection,
                entry.voiceClip,
                entry.backgroundImage
            );
        }
    }

    /// <summary>
    /// OverlayAction の実行時に View へ渡すデータパケット。
    /// </summary>
    public readonly struct OverlayEventData
    {
        public readonly string Text;
        public readonly Sprite Portrait;
        public readonly PortraitPosition PortraitPosition;

        public OverlayEventData(string text, Sprite portrait, PortraitPosition portraitPosition)
        {
            Text = text;
            Portrait = portrait;
            PortraitPosition = portraitPosition;
        }
    }

    public readonly struct LostNoteEventData
    {
        public readonly string KeywordId;
        public readonly string Title;
        public readonly string Description;

        public LostNoteEventData(string keywordId, string title, string description)
        {
            KeywordId = keywordId;
            Title = title;
            Description = description;
        }
    }

    public readonly struct TitleLogoEventData
    {
        public readonly Sprite LogoSprite;
        public readonly bool UseNativeLogoSize;
        public readonly Vector2 MaxLogoSize;
        public readonly Color BackgroundColor;
        public readonly float BackgroundFadeInDuration;
        public readonly float LogoFadeInDuration;
        public readonly float LogoDisplayDuration;
        public readonly float LogoFadeOutDuration;
        public readonly float BackgroundHoldDuration;
        public readonly float BackgroundFadeOutDuration;
        public readonly bool FadeOutBackgroundAfterLogo;

        public TitleLogoEventData(
            Sprite logoSprite,
            bool useNativeLogoSize,
            Vector2 maxLogoSize,
            Color backgroundColor,
            float backgroundFadeInDuration,
            float logoFadeInDuration,
            float logoDisplayDuration,
            float logoFadeOutDuration,
            float backgroundHoldDuration,
            float backgroundFadeOutDuration,
            bool fadeOutBackgroundAfterLogo)
        {
            LogoSprite = logoSprite;
            UseNativeLogoSize = useNativeLogoSize;
            MaxLogoSize = maxLogoSize;
            BackgroundColor = backgroundColor;
            BackgroundFadeInDuration = backgroundFadeInDuration;
            LogoFadeInDuration = logoFadeInDuration;
            LogoDisplayDuration = logoDisplayDuration;
            LogoFadeOutDuration = logoFadeOutDuration;
            BackgroundHoldDuration = backgroundHoldDuration;
            BackgroundFadeOutDuration = backgroundFadeOutDuration;
            FadeOutBackgroundAfterLogo = fadeOutBackgroundAfterLogo;
        }

        public static TitleLogoEventData FromAction(TitleLogoAction action)
        {
            return new TitleLogoEventData(
                action.logoSprite,
                action.useNativeLogoSize,
                action.maxLogoSize,
                action.backgroundColor,
                action.backgroundFadeInDuration,
                action.logoFadeInDuration,
                action.logoDisplayDuration,
                action.logoFadeOutDuration,
                action.backgroundHoldDuration,
                action.backgroundFadeOutDuration,
                action.fadeOutBackgroundAfterLogo
            );
        }
    }
}
