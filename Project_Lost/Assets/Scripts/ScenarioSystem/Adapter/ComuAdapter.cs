using UnityEngine;
using ScenarioSystem.Events;
using ScenarioSystem.Model;

namespace ScenarioSystem.Adapter
{
    /// <summary>
    /// 新シナリオシステム ↔ 既存 ComuStartandEndManager の橋渡し。
    /// EventBus の OnComuToggleRequested / OnComuToggleInstantRequested を購読し、
    /// 既存の ToggleComu() / ToggleComuInstant() を呼び出す。
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
            ScenarioEventBus.OnComuToggleInstantRequested += HandleComuToggleInstant;
            ScenarioEventBus.OnScenarioStarted += HandleScenarioStarted;
            ScenarioEventBus.OnScenarioEnded += HandleScenarioEnded;
        }

        private void OnDisable()
        {
            ScenarioEventBus.OnComuToggleRequested -= HandleComuToggle;
            ScenarioEventBus.OnComuToggleInstantRequested -= HandleComuToggleInstant;
            ScenarioEventBus.OnScenarioStarted -= HandleScenarioStarted;
            ScenarioEventBus.OnScenarioEnded -= HandleScenarioEnded;
        }

        #endregion

        #region Event Handlers

        private void HandleComuToggle()
        {
            ResolveManager();

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

        /// <summary>
        /// アニメーションなしでコミュニケーション状態を即座に切り替える。
        /// ToggleComuforPortrait と同じロジック判定を行い、形状変更も即座に適用する。
        /// </summary>
        private void HandleComuToggleInstant()
        {
            ResolveManager();

            if (comuManager != null)
            {
                Debug.Log("[ComuAdapter] ToggleComuInstant");
                comuManager.ToggleComuInstant();
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

        #region Utility

        private void ResolveManager()
        {
            if (comuManager == null)
                comuManager = FindFirstObjectByType<ComuStartandEndManager>();
        }

        #endregion
    }
}
