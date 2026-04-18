using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ScenarioSystem.Events;
using ScenarioSystem.Model;

namespace ScenarioSystem.View
{
    /// <summary>
    /// 背景スチル画像（CG）の表示を担当する View。
    /// EventBus の OnDialogueRequested を購読し、backgroundImage があれば表示する。
    /// </summary>
    public class BackgroundStillView : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Background Still")]
        [SerializeField] private Image backgroundStillImage;

        [Tooltip("背景スチル表示時に非表示にするオブジェクト群。")]
        [SerializeField] private GameObject[] objectsToHideOnStill;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            ScenarioEventBus.OnDialogueRequested += HandleDialogue;
            ScenarioEventBus.OnScenarioEnded += HandleScenarioEnded;
        }

        private void OnDisable()
        {
            ScenarioEventBus.OnDialogueRequested -= HandleDialogue;
            ScenarioEventBus.OnScenarioEnded -= HandleScenarioEnded;
        }

        #endregion

        #region Event Handlers

        private void HandleDialogue(DialogueEventData data)
        {
            if (backgroundStillImage == null) return;

            bool hasStill = data.BackgroundImage != null;

            if (hasStill)
            {
                backgroundStillImage.sprite = data.BackgroundImage;
                backgroundStillImage.gameObject.SetActive(true);
            }
            else
            {
                backgroundStillImage.gameObject.SetActive(false);
            }

            ToggleHiddenObjects(!hasStill);
        }

        private void HandleScenarioEnded(ScenarioData _)
        {
            if (backgroundStillImage != null)
                backgroundStillImage.gameObject.SetActive(false);

            ToggleHiddenObjects(true);
        }

        #endregion

        #region Utility

        private void ToggleHiddenObjects(bool visible)
        {
            if (objectsToHideOnStill == null) return;
            foreach (var obj in objectsToHideOnStill)
            {
                if (obj != null) obj.SetActive(visible);
            }
        }

        #endregion
    }
}
