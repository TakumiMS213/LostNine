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
            OnKeywordStateChanged = null;
            OnKeywordClicked = null;
            OnScenarioStarted = null;
            OnScenarioEnded = null;
            OnWindowVisibilityChanged = null;
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

        /// <summary>DialogueAction から DialogueEventData を生成するファクトリ。</summary>
        public static DialogueEventData FromAction(DialogueAction action)
        {
            return new DialogueEventData(
                action.speakerName,
                action.text,
                action.portrait,
                action.portraitPosition,
                action.typingSpeed,
                action.nameSlideDirection,
                action.voiceClip,
                action.backgroundImage
            );
        }
    }
}
