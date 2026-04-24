using UnityEngine;
using ScenarioSystem.Events;
using ScenarioSystem.Model;

namespace ScenarioSystem.Adapter
{
    /// <summary>
    /// 新シナリオシステム ↔ 既存 ComuStartandEndManager の橋渡し。
    /// EventBus の OnComuToggleRequested を購読し、既存の ToggleComu() を呼び出す。
    /// シナリオ開始/終了時の UI 切り替えも必要に応じて橋渡しする。
    /// </summary>
    public class ComuAdapter : MonoBehaviour
    {
        #region Serialized Fields

        [Header("References")]
        [Tooltip("既存の ComuStartandEndManager への参照。")]
        [SerializeField] private ComuStartandEndManager comuManager;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            ScenarioEventBus.OnComuToggleRequested += HandleComuToggle;
            ScenarioEventBus.OnScenarioStarted += HandleScenarioStarted;
            ScenarioEventBus.OnScenarioEnded += HandleScenarioEnded;
        }

        private void OnDisable()
        {
            ScenarioEventBus.OnComuToggleRequested -= HandleComuToggle;
            ScenarioEventBus.OnScenarioStarted -= HandleScenarioStarted;
            ScenarioEventBus.OnScenarioEnded -= HandleScenarioEnded;
        }

        #endregion

        #region Event Handlers

        private void HandleComuToggle()
        {
            if (comuManager == null)
            {
                comuManager = FindFirstObjectByType<ComuStartandEndManager>();
            }

            if (comuManager != null)
            {
                Debug.Log("[ComuAdapter] ToggleComu");
                comuManager.ToggleComu();
            }
            else
            {
                Debug.LogWarning("[ComuAdapter] ComuStartandEndManager not found.");
            }
        }

        private void HandleScenarioStarted(ScenarioData scenario)
        {
            Debug.Log($"[ComuAdapter] Scenario started: {scenario?.name}");
            // 必要に応じて Portrait のインタラクション制御などを行う
        }

        private void HandleScenarioEnded(ScenarioData scenario)
        {
            Debug.Log($"[ComuAdapter] Scenario ended: {scenario?.name}");
            // 必要に応じて探索モードへの復帰処理を行う
        }

        #endregion
    }
}
