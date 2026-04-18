using UnityEngine;
using ScenarioSystem.Events;
using ScenarioSystem.Model;

namespace ScenarioSystem.Adapter
{
    /// <summary>
    /// 新シナリオシステム ↔ 既存 ComuStartandEndManager の橋渡し（拡張版）。
    /// シナリオ開始/終了時のポートレートインタラクション制御も担当する。
    /// Phase 4 用に ComuAdapter を強化。
    /// </summary>
    public class ComuAdapterExtended : MonoBehaviour
    {
        #region Serialized Fields

        [Header("References")]
        [SerializeField] private ComuStartandEndManager comuManager;

        [Header("Portrait Control")]
        [Tooltip("シナリオ再生中はポートレートクリックを無効にするか。")]
        [SerializeField] private bool disablePortraitDuringScenario = true;

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
            ResolveManager();

            if (comuManager != null)
            {
                Debug.Log("[ComuAdapterExtended] ToggleComu");
                comuManager.ToggleComu();
            }
            else
            {
                Debug.LogWarning("[ComuAdapterExtended] ComuStartandEndManager not found.");
            }
        }

        private void HandleScenarioStarted(ScenarioData scenario)
        {
            ResolveManager();

            if (comuManager != null && disablePortraitDuringScenario)
            {
                comuManager.SetPortraitInteractable(false, true);
            }
        }

        private void HandleScenarioEnded(ScenarioData scenario)
        {
            ResolveManager();

            if (comuManager != null)
            {
                comuManager.SetPortraitInteractable(true, true);
            }
        }

        #endregion

        #region Utility

        private void ResolveManager()
        {
            if (comuManager == null)
                comuManager = FindObjectOfType<ComuStartandEndManager>();
        }

        #endregion
    }
}
