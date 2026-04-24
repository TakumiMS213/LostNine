using UnityEngine;
using UnityEngine.UI;
using ScenarioSystem.Model;

/// <summary>
/// Starts a scenario based on current game progress when button is clicked.
/// Attach to a UI Button.
/// </summary>
[RequireComponent(typeof(Button))]
public class ProgressBasedScenarioStarter : MonoBehaviour
{
    [Header("Scenario Lookup")]
    // Legacy scenarioDatabase removed

    [Header("Optional Override")]
    [Tooltip("If set, uses this scenario instead of progress-based lookup.")]
    [SerializeField] private ScenarioData overrideScenario;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnClick);
    }

    private void OnDestroy()
    {
        _button?.onClick.RemoveListener(OnClick);
    }

    private void OnClick()
    {
        var facade = ScenarioSystem.Adapter.MessageWindowFacade.Instance;
        if (facade == null)
        {
            Debug.LogError("[ProgressBasedScenarioStarter] MessageWindowFacade not found!");
            return;
        }

        // 1. Override が指定されている場合はそのまま再生（レガシー互換）
        if (overrideScenario != null)
        {
            facade.StartScenario(overrideScenario);
            return;
        }

        // 2. ProgressManager から現在の状態に応じた ID を取得して再生
        if (ProgressManager.Instance != null)
        {
            string key = ProgressManager.Instance.GetScenarioKey();
            facade.StartScenarioById(key);
        }
        else
        {
            Debug.LogWarning("[ProgressBasedScenarioStarter] ProgressManager not found.");
        }
    }
}
