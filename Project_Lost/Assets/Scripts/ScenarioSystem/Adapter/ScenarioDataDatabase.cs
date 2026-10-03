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
        [Tooltip("ID検索の入口・独立シナリオを登録。Next Scenarioと選択肢の遷移先も自動で検索対象になる。既存の全件登録も使用可能。")]
        public List<ScenarioData> allScenarios = new();

        private Dictionary<string, ScenarioData> _map;
        private int _indexedRevision;

        private void OnEnable()
        {
            InvalidateCache();
        }

        private void OnValidate() => InvalidateCache();

        /// <summary>コードから登録リストを編集した後に呼ぶ。次の検索で一度だけ再構築する。</summary>
        public void InvalidateCache() => _map = null;

        /// <summary>辞書を再構築する。</summary>
        public void BuildMap()
        {
            var map = new Dictionary<string, ScenarioData>(StringComparer.Ordinal);
            foreach (var scenario in ScenarioGraph.Collect(allScenarios))
            {
                string id = ScenarioKey.Normalize(scenario.scenarioId);
                if (id.Length == 0)
                    continue;

                if (!map.TryAdd(id, scenario))
                    Debug.LogWarning($"[ScenarioDataDatabase] Duplicate ID: {id} in {scenario.name}", scenario);
            }
            _map = map;
            _indexedRevision = ScenarioDataRevision.Version;
        }

        /// <summary>ID でシナリオを検索する。</summary>
        public ScenarioData GetById(string id)
        {
            if (TryGetById(id, out var scenario))
                return scenario;

            Debug.LogWarning($"[ScenarioDataDatabase] Scenario '{id}' not found.");
            return null;
        }

        public bool TryGetById(string id, out ScenarioData scenario)
        {
            scenario = null;
            id = ScenarioKey.Normalize(id);
            if (id.Length == 0) return false;
            if (_map == null || _indexedRevision != ScenarioDataRevision.Version) BuildMap();
            return _map.TryGetValue(id, out scenario) && scenario != null;
        }
    }
}
