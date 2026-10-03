using System.Collections.Generic;
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
        private readonly Dictionary<GameObject, bool> _hiddenObjectStates = new();

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            ScenarioEventBus.OnDialogueRequested += HandleDialogue;
            ScenarioEventBus.OnScenarioEnded += HandleScenarioEnded;
        }

        private void OnDisable()
        {
            RestoreHiddenObjects();
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

            if (hasStill)
                HideObjectsForStill();
            else
                RestoreHiddenObjects();
        }

        private void HandleScenarioEnded(ScenarioData _)
        {
            if (backgroundStillImage != null)
                backgroundStillImage.gameObject.SetActive(false);

            RestoreHiddenObjects();
        }

        #endregion

        #region Utility

        private void HideObjectsForStill()
        {
            if (objectsToHideOnStill == null) return;
            foreach (var obj in objectsToHideOnStill)
            {
                if (obj == null) continue;
                if (!_hiddenObjectStates.ContainsKey(obj))
                    _hiddenObjectStates.Add(obj, obj.activeSelf);
                obj.SetActive(false);
            }
        }

        private void RestoreHiddenObjects()
        {
            foreach (var entry in _hiddenObjectStates)
                if (entry.Key != null) entry.Key.SetActive(entry.Value);
            _hiddenObjectStates.Clear();
        }

        #endregion
    }
}
