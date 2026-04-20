using TMPro;

namespace ScenarioSystem.Adapter
{
    /// <summary>
    /// KeywordHandler が必要とする対話ウィンドウの機能を抽象化するインターフェース。
    /// 旧 MessageWindowManager と新 ScenarioPresenter の両方がこのインターフェースを実装可能。
    /// 
    /// KeywordHandler は MessageWindowManager.Instance の代わりに
    /// IDialogueProvider を参照するように更新される。
    /// </summary>
    public interface IDialogueProvider
    {
        /// <summary>セリフ表示用 TMP_Text。</summary>
        TMP_Text DialogueText { get; }

        /// <summary>現在表示中のテキスト。</summary>
        string CurrentText { get; }

        /// <summary>ウィンドウが表示中か。</summary>
        bool IsWindowActive { get; }

        /// <summary>タイピング中か。</summary>
        bool IsTyping { get; }

        /// <summary>現在のテキストを更新する。</summary>
        void UpdateCurrentText(string newText);
    }
}
