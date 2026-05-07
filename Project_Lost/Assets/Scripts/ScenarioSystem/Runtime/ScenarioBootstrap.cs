using UnityEngine;
using ScenarioSystem.Model;
using ScenarioSystem.Adapter;
using ScenarioSystem.Presenter;
using ScenarioSystem.Presenter.Executors;

namespace ScenarioSystem.Runtime
{
    /// <summary>
    /// ScenarioPresenter に全 Executor を登録し、テストシナリオを自動再生するブートストラップ。
    /// テストシーンに配置して使用する。
    /// </summary>
    public class ScenarioBootstrap : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("同じ GameObject または子にある ScenarioPresenter。")]
        [SerializeField] private ScenarioPresenter presenter;

        [Tooltip("ProgressScenarioAction で使用するシナリオデータベース。")]
        [SerializeField] private ScenarioDataDatabase scenarioDatabase;

        [Header("Auto Play")]
        [Tooltip("Play モード開始時に自動再生するシナリオ（テスト用）。")]
        [SerializeField] private ScenarioData testScenario;

        [Tooltip("テストシナリオを自動再生するか。")]
        [SerializeField] private bool autoPlay = true;

        private void Start()
        {
            if (presenter == null)
                presenter = GetComponent<ScenarioPresenter>();

            if (presenter == null)
            {
                Debug.LogError("[ScenarioBootstrap] ScenarioPresenter not found.");
                return;
            }

            RegisterAllExecutors();

            if (autoPlay && testScenario != null)
            {
                Debug.Log($"[ScenarioBootstrap] Auto-playing test scenario: {testScenario.name}");
                presenter.StartScenario(testScenario, () =>
                {
                    Debug.Log("[ScenarioBootstrap] Test scenario completed.");
                });
            }
        }

        private void RegisterAllExecutors()
        {
            presenter.RegisterExecutor(new DialogueActionExecutor());
            presenter.RegisterExecutor(new EffectActionExecutor());
            presenter.RegisterExecutor(new ChoiceActionExecutor());
            presenter.RegisterExecutor(new WaitActionExecutor(presenter));
            presenter.RegisterExecutor(new ProgressUpdateActionExecutor());
            presenter.RegisterExecutor(new ComuToggleActionExecutor());
            presenter.RegisterExecutor(new ComuToggleInstantActionExecutor());
            presenter.RegisterExecutor(new KeywordEnableActionExecutor());
            presenter.RegisterExecutor(new OverlayActionExecutor(presenter));
            presenter.RegisterExecutor(new TitleLogoActionExecutor(presenter));
            presenter.RegisterExecutor(new ProgressScenarioActionExecutor(presenter, scenarioDatabase));
            presenter.RegisterExecutor(new CenterPortraitActionExecutor());
            presenter.RegisterExecutor(new SceneTransitionActionExecutor());
            presenter.RegisterExecutor(new PortraitInteractableActionExecutor());
            presenter.RegisterExecutor(new PortraitGuidanceActionExecutor());
            presenter.RegisterExecutor(new LostNoteCharacterActionExecutor());

            Debug.Log("[ScenarioBootstrap] All executors registered.");
        }
    }
}
