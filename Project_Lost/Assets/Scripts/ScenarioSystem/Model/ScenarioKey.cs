using System;

namespace ScenarioSystem.Model
{
    /// <summary>
    /// フェーズ名と一致しない、章ごとのシナリオ用途。
    /// 保存済みIDの大文字小文字（特に loop）は変更しない。
    /// </summary>
    public enum ScenarioPurpose
    {
        Story,
        Loop,
        DialogueStart
    }

    /// <summary>
    /// 既存のシナリオID規約を一元管理する。アセットのIDは書き換えない。
    /// </summary>
    public static class ScenarioKey
    {
        public static string Normalize(string scenarioId) =>
            string.IsNullOrWhiteSpace(scenarioId) ? string.Empty : scenarioId.Trim();

        public static string ForPhase(int chapter, GamePhase phase) => $"Ch{chapter}_{phase}";

        public static string ForPurpose(int chapter, ScenarioPurpose purpose)
        {
            string suffix = purpose switch
            {
                ScenarioPurpose.Story => "Story",
                ScenarioPurpose.Loop => "loop",
                ScenarioPurpose.DialogueStart => "DialogueStart",
                _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null)
            };
            return $"Ch{chapter}_{suffix}";
        }

        /// <summary>
        /// Ch{正の章番号}_{用途} 形式だけから章を取得する。
        /// キーワードIDなど別形式のIDは、有効なシナリオIDでも false を返す。
        /// </summary>
        public static bool TryGetChapter(string scenarioId, out int chapter)
        {
            chapter = 0;
            string id = Normalize(scenarioId);
            if (id.Length < 5 || id[0] != 'C' || id[1] != 'h')
                return false;

            int index = 2;
            int parsedChapter = 0;
            while (index < id.Length && id[index] >= '0' && id[index] <= '9')
            {
                int digit = id[index] - '0';
                if (parsedChapter > (int.MaxValue - digit) / 10)
                    return false;

                parsedChapter = parsedChapter * 10 + digit;
                index++;
            }

            if (parsedChapter <= 0 || index >= id.Length - 1 || id[index] != '_')
                return false;

            chapter = parsedChapter;
            return true;
        }
    }
}
