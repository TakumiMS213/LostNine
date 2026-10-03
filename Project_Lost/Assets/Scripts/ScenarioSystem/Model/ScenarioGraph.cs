using System.Collections.Generic;
using ScenarioSystem.Model.Actions;

namespace ScenarioSystem.Model
{
    /// <summary>登録した入口から、直参照の連鎖・選択肢でつながるシナリオを一度ずつ列挙する。</summary>
    public static class ScenarioGraph
    {
        public static List<ScenarioData> Collect(IEnumerable<ScenarioData> roots)
        {
            var result = new List<ScenarioData>();
            if (roots == null) return result;

            var visited = new HashSet<ScenarioData>();
            var pending = new Queue<ScenarioData>();
            // 明示登録された順序を優先する。既存の重複IDの先勝ち動作も維持する。
            foreach (var root in roots)
                Add(root);

            while (pending.Count > 0)
            {
                var scenario = pending.Dequeue();
                Add(scenario.nextScenario);
                if (scenario.actions == null) continue;
                foreach (var action in scenario.actions)
                {
                    if (action is not ChoiceAction choice || choice.choices == null) continue;
                    foreach (var entry in choice.choices)
                        if (entry != null) Add(entry.nextScenario);
                }
            }
            return result;

            void Add(ScenarioData scenario)
            {
                if (scenario == null || !visited.Add(scenario)) return;
                result.Add(scenario);
                pending.Enqueue(scenario);
            }
        }
    }
}
