namespace Communication
{
    /// <summary>
    /// ComuStartandEndManager から分離した純粋ロジック。
    /// Unity API に一切依存しない。
    /// 状態管理とトグル判定、シナリオID生成を担当する。
    /// </summary>
    public class ComuLogic
    {
        #region State

        public bool IsAnimating { get; set; }
        public bool IsPortraitInteractable { get; set; } = true;
        public bool IsInCommunication { get; set; }

        #endregion

        #region Toggle Judgment

        /// <summary>
        /// ポートレートクリック時の判定結果。
        /// UI操作やSE再生は呼び出し側（MonoBehaviour）が行う。
        /// </summary>
        public enum ToggleResult
        {
            /// <summary>アニメーション中のためブロック。</summary>
            Blocked,
            /// <summary>クリック不可状態 → SE再生のみ。</summary>
            PlayUnclickableSE,
            /// <summary>対話終了フローを実行する。</summary>
            EndCommunication,
            /// <summary>対話開始フローを実行する。</summary>
            StartCommunication
        }

        /// <summary>
        /// ポートレートクリック時のロジック判定。
        /// </summary>
        public ToggleResult JudgeToggle()
        {
            if (IsAnimating)
                return ToggleResult.Blocked;

            if (!IsPortraitInteractable)
                return ToggleResult.PlayUnclickableSE;

            if (IsInCommunication)
                return ToggleResult.EndCommunication;

            return ToggleResult.StartCommunication;
        }

        #endregion

        #region Scenario ID Resolution

        /// <summary>
        /// 対話開始時のシナリオID生成結果。
        /// </summary>
        public struct StartInfo
        {
            public string ScenarioId;
            public bool EnableKeywords;
        }

        /// <summary>
        /// フェーズとチャプターからシナリオIDとキーワード有効フラグを生成する。
        /// </summary>
        public static StartInfo ResolveScenarioId(int chapter, GamePhase phase)
        {
            var info = new StartInfo { EnableKeywords = false };

            switch (phase)
            {
                case GamePhase.Dialogue:
                    info.ScenarioId = $"Ch{chapter}_Dialogue";
                    break;

                case GamePhase.Extraction:
                    info.ScenarioId = $"Ch{chapter}_Extraction";
                    info.EnableKeywords = true;
                    break;

                case GamePhase.Presentation:
                    info.ScenarioId = $"Ch{chapter}_Presentation";
                    break;

                default:
                    info.ScenarioId = $"Ch{chapter}_{phase}";
                    break;
            }

            return info;
        }

        /// <summary>
        /// 対話終了時のシナリオIDを生成する。
        /// </summary>
        public static string ResolveEndScenarioId(int chapter)
        {
            return $"Ch{chapter}_loop";
        }

        #endregion
    }
}
