using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ScenarioSystem.Events;
using ScenarioSystem.Model.Actions;

namespace ScenarioSystem.View
{
    /// <summary>
    /// 選択肢表示を担当する View。
    /// EventBus の OnChoicesRequested を購読し、ボタンを動的に表示する。
    /// ユーザーの選択を OnChoiceSelected で Presenter に通知する。
    /// </summary>
    public class ChoiceView : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Choice Buttons")]
        [SerializeField] private Button[] choiceButtons;
        [SerializeField] private TMP_Text[] choiceButtonTexts;
        private int _visibleChoiceCount;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            HideAllChoices();
        }

        private void OnEnable()
        {
            ScenarioEventBus.OnChoicesRequested += HandleChoicesRequested;
            ScenarioEventBus.OnScenarioStarted += HandleScenarioEnded;
            ScenarioEventBus.OnScenarioEnded += HandleScenarioEnded;
        }

        private void OnDisable()
        {
            ScenarioEventBus.OnChoicesRequested -= HandleChoicesRequested;
            ScenarioEventBus.OnScenarioStarted -= HandleScenarioEnded;
            ScenarioEventBus.OnScenarioEnded -= HandleScenarioEnded;
            HideAllChoices();
        }

        #endregion

        #region Event Handlers

        private void HandleChoicesRequested(List<ChoiceEntry> choices)
        {
            HideAllChoices();
            if (choiceButtons == null) return;
            _visibleChoiceCount = choices != null ? Mathf.Min(choices.Count, choiceButtons.Length) : 0;

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] == null) continue;
                if (i < _visibleChoiceCount)
                {
                    // 全画面の会話送り用ClickAreaより前面でクリックを受け取る。
                    choiceButtons[i].transform.SetAsLastSibling();
                    choiceButtons[i].gameObject.SetActive(true);

                    if (choiceButtonTexts != null && i < choiceButtonTexts.Length && choiceButtonTexts[i] != null)
                        choiceButtonTexts[i].text = choices[i].choiceText;

                    int index = i; // ラムダキャプチャ用
                    choiceButtons[i].onClick.RemoveAllListeners();
                    choiceButtons[i].onClick.AddListener(() => OnChoiceButtonClicked(index));
                }
                else
                {
                    choiceButtons[i].gameObject.SetActive(false);
                }
            }
        }

        private void HandleScenarioEnded(Model.ScenarioData _)
        {
            HideAllChoices();
        }

        #endregion

        #region Choice Logic

        private void OnChoiceButtonClicked(int index)
        {
            if (index < 0 || index >= _visibleChoiceCount || choiceButtons[index] == null
                || !choiceButtons[index].isActiveAndEnabled || !choiceButtons[index].IsInteractable())
                return;

            HideAllChoices();
            ScenarioEventBus.RaiseChoiceSelected(index);
        }

        private void HideAllChoices()
        {
            _visibleChoiceCount = 0;
            if (choiceButtons == null) return;
            foreach (var btn in choiceButtons)
                if (btn != null) btn.gameObject.SetActive(false);
        }

        #endregion
    }
}
