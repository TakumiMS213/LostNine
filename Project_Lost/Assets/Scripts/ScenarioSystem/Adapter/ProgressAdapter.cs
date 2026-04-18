using UnityEngine;
using ScenarioSystem.Events;
using ScenarioSystem.Model.Actions;

namespace ScenarioSystem.Adapter
{
    /// <summary>
    /// 新シナリオシステム ↔ 既存 ProgressManager の橋渡し。
    /// EventBus の OnProgressUpdateRequested を購読し、既存の ProgressManager API を呼び出す。
    /// 新システムは ProgressManager の存在を直接知らない。
    /// </summary>
    public class ProgressAdapter : MonoBehaviour
    {
        #region Unity Lifecycle

        private void OnEnable()
        {
            ScenarioEventBus.OnProgressUpdateRequested += HandleProgressUpdate;
        }

        private void OnDisable()
        {
            ScenarioEventBus.OnProgressUpdateRequested -= HandleProgressUpdate;
        }

        #endregion

        #region Event Handler

        private void HandleProgressUpdate(ProgressUpdateAction action)
        {
            if (ProgressManager.Instance == null)
            {
                Debug.LogWarning("[ProgressAdapter] ProgressManager.Instance is null.");
                return;
            }

            switch (action.actionType)
            {
                case ScenarioProgressActionType.AdvancePhase:
                    Debug.Log("[ProgressAdapter] AdvancePhase");
                    ProgressManager.Instance.AdvancePhase();
                    break;

                case ScenarioProgressActionType.AdvanceChapter:
                    Debug.Log("[ProgressAdapter] AdvanceChapter");
                    ProgressManager.Instance.AdvanceChapter();
                    break;

                case ScenarioProgressActionType.SetDirectly:
                    Debug.Log($"[ProgressAdapter] SetProgress: Ch{action.targetChapter} / {action.targetPhase}");
                    ProgressManager.Instance.SetProgress(action.targetChapter, action.targetPhase);
                    break;
            }
        }

        #endregion
    }
}
