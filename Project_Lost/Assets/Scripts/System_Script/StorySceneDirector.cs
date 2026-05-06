using UnityEngine;
using System.Collections;
using ScenarioSystem.Adapter;
using ScenarioSystem.Events;

namespace System_Script
{
    /// <summary>
    /// Storyシーンで自動的にシナリオを再生し、終了後にMainシーンへ遷移する管理クラス。
    /// </summary>
    public class StorySceneDirector : MonoBehaviour
    {
        [Tooltip("MessageWindowFacadeの初期化を待機する最大フレーム数")]
        [SerializeField] private int maxWaitFrames = 10;

        private void Start()
        {
            StartCoroutine(WaitAndPlay());
        }

        private IEnumerator WaitAndPlay()
        {
            int waited = 0;
            while ((MessageWindowFacade.Instance == null || ProgressManager.Instance == null) && waited < maxWaitFrames)
            {
                yield return null;
                waited++;
            }

            yield return null; // 確実な初期化のためにもう1フレーム待機

            if (ProgressManager.Instance == null)
            {
                Debug.LogWarning("[StorySceneDirector] ProgressManagerが見つかりません。");
                yield break;
            }

            if (MessageWindowFacade.Instance == null)
            {
                Debug.LogWarning("[StorySceneDirector] MessageWindowFacadeが見つかりません。");
                yield break;
            }

            // イベントの購読
            ScenarioEventBus.OnScenarioEnded += HandleScenarioEnded;

            string scenarioId = ProgressManager.Instance.TryConsumeStoryScenarioId(out var requestedScenarioId)
                ? requestedScenarioId
                : $"Ch{ProgressManager.Instance.CurrentChapter}_Story";
            Debug.Log($"[StorySceneDirector] 自動再生開始: {scenarioId}");
            
            MessageWindowFacade.Instance.StartScenarioById(scenarioId);
        }

        private void HandleScenarioEnded(ScenarioSystem.Model.ScenarioData data)
        {
            Debug.Log("[StorySceneDirector] シナリオ再生終了を検知しました。Mainシーンへ遷移します。");
            
            // イベントの購読解除
            ScenarioEventBus.OnScenarioEnded -= HandleScenarioEnded;

            if (SceneTransition.Instance != null)
            {
                SceneTransition.Instance.TransitionTo(ProgressManager.Instance.MainSceneName);
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(ProgressManager.Instance.MainSceneName);
            }
        }

        private void OnDestroy()
        {
            ScenarioEventBus.OnScenarioEnded -= HandleScenarioEnded;
        }
    }
}
