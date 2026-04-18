using System;
using System.Collections.Generic;
using UnityEngine;
using ScenarioSystem.Model;

namespace ScenarioSystem.Adapter
{
    /// <summary>
    /// 新 ScenarioData 用のデータベース。
    /// 旧 ScenarioDatabase と同じ ID ベースの検索機能を提供する。
    /// </summary>
    [CreateAssetMenu(fileName = "ScenarioDataDatabase", menuName = "Scenario/Scenario Database")]
    public class ScenarioDataDatabase : ScriptableObject
    {
        [Tooltip("全シナリオデータのリスト。")]
        public List<ScenarioData> allScenarios = new();

        private Dictionary<string, ScenarioData> _map;

        private void OnEnable()
        {
            BuildMap();
        }

        /// <summary>辞書を再構築する。</summary>
        public void BuildMap()
        {
            _map = new Dictionary<string, ScenarioData>();
            foreach (var scenario in allScenarios)
            {
                if (scenario != null && !string.IsNullOrEmpty(scenario.scenarioId))
                {
                    if (!_map.ContainsKey(scenario.scenarioId))
                        _map.Add(scenario.scenarioId, scenario);
                    else
                        Debug.LogWarning($"[ScenarioDataDatabase] Duplicate ID: {scenario.scenarioId} in {scenario.name}");
                }
            }
        }

        /// <summary>ID でシナリオを検索する。</summary>
        public ScenarioData GetById(string id)
        {
            if (_map == null) BuildMap();

            if (_map.TryGetValue(id, out var scenario))
                return scenario;

            Debug.LogWarning($"[ScenarioDataDatabase] Scenario '{id}' not found.");
            return null;
        }
    }
}
