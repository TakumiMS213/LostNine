using UnityEngine;
using System.Collections;
using ScenarioSystem.Adapter;

namespace System_Script
{
    /// <summary>
    /// シーン開始時に、ProgressManagerの現在の状態（Ch1_Prologueなど）に
    /// 対応するシナリオを Database から検索して自動再生します。
    /// 
    /// Title→Main や Tuning→Main 等、シーン遷移でMainに戻った際に
    /// 現在のProgressに応じたシナリオを確実に再生するための仕組みです。
    /// 
    /// ※ フェーズ変更後の連鎖再生は ProgressScenarioAction（シナリオ内 Action）が担当します。
    /// </summary>
    public class AutoPlayProgressScenario : MonoBehaviour
    {
        [Tooltip("シーン開始時に自動再生を行うか")]
        public bool playOnStart = true;

        [Tooltip("再生開始を少し遅らせる場合（秒）")]
        public float delaySeconds = 0f;

        [Tooltip("MessageWindowFacade の初期化を待機する最大フレーム数")]
        [SerializeField] private int maxWaitFrames = 10;

        private void Start()
        {
            if (playOnStart)
            {
                StartCoroutine(WaitAndPlay());
            }
        }

        /// <summary>
        /// MessageWindowFacade と ProgressManager の初期化を待ってからシナリオを再生する。
        /// ScenarioBootstrap.Start() 等の初期化順序に依存しないよう、
        /// 数フレーム待機して確実に全コンポーネントが揃った状態で再生する。
        /// </summary>
        private IEnumerator WaitAndPlay()
        {
            // 遅延指定がある場合は先に待つ
            if (delaySeconds > 0)
                yield return new WaitForSeconds(delaySeconds);

            // MessageWindowFacade と ProgressManager が揃うまで待機
            int waited = 0;
            while ((MessageWindowFacade.Instance == null || ProgressManager.Instance == null) && waited < maxWaitFrames)
            {
                yield return null;
                waited++;
            }

            // さらに1フレーム待って ScenarioBootstrap の Executor 登録を確実に待つ
            yield return null;

            PlayScenario();
        }

        private void PlayScenario()
        {
            if (ProgressManager.Instance == null)
            {
                Debug.LogWarning("[AutoPlayProgressScenario] ProgressManager が見つかりません。");
                return;
            }

            if (MessageWindowFacade.Instance == null)
            {
                Debug.LogWarning("[AutoPlayProgressScenario] MessageWindowFacade が見つかりません。");
                return;
            }

            // 例: "Ch1_Presentation" のようなキーを取得
            string key = ProgressManager.Instance.GetScenarioKey();
            Debug.Log($"[AutoPlayProgressScenario] シーン開始: Progressに応じたシナリオ({key})を自動再生します。");
            
            // Facade経由でデータベースから検索して再生
            MessageWindowFacade.Instance.StartScenarioById(key);
        }
    }
}
