using System;
using System.Collections.Generic;
using System.Linq;
using ScenarioSystem.Adapter;
using ScenarioSystem.Model;
using UnityEditor;
using UnityEngine;

namespace ScenarioSystem.Editor
{
    /// <summary>シナリオの編集操作。参照を外しても既存アクションは削除しない。</summary>
    [InitializeOnLoad]
    public static class ScenarioAuthoringService
    {
        private static List<ScenarioData> _scenarios;
        private static List<ScenarioDataDatabase> _databases;
        private static Dictionary<ScenarioAction, int> _referenceCounts;

        static ScenarioAuthoringService()
        {
            EditorApplication.projectChanged += Invalidate;
            Undo.undoRedoPerformed += Invalidate;
        }

        public static void Invalidate()
        {
            _scenarios = null;
            _databases = null;
            _referenceCounts = null;
        }

        public static IReadOnlyList<ScenarioDataDatabase> Databases
        {
            get
            {
                if (_databases == null)
                    _databases = FindAssets<ScenarioDataDatabase>();
                return _databases;
            }
        }

        public static List<ScenarioData> ValidationRoots(ScenarioData extra = null)
        {
            var roots = Databases.Where(db => db.allScenarios != null)
                .SelectMany(db => db.allScenarios).Where(scenario => scenario != null).Distinct().ToList();
            if (extra != null && !roots.Contains(extra)) roots.Add(extra);
            return roots;
        }

        public static int ReferenceCount(ScenarioAction action)
        {
            if (action == null) return 0;
            if (_referenceCounts == null)
            {
                _scenarios ??= FindAssets<ScenarioData>();
                _referenceCounts = new Dictionary<ScenarioAction, int>();
                foreach (var scenario in ScenarioGraph.Collect(_scenarios))
                {
                    if (scenario.actions == null) continue;
                    foreach (var referenced in scenario.actions.Where(value => value != null).Distinct())
                    {
                        _referenceCounts.TryGetValue(referenced, out int count);
                        _referenceCounts[referenced] = count + 1;
                    }
                }
            }
            return _referenceCounts.TryGetValue(action, out int references) ? references : 0;
        }

        private static List<T> FindAssets<T>() where T : UnityEngine.Object
            => AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<T>().Distinct().ToList();

        public static ScenarioAction AddAction(ScenarioData scenario, Type actionType)
        {
            EnsurePersistent(scenario);
            if (actionType == null || actionType.IsAbstract || !typeof(ScenarioAction).IsAssignableFrom(actionType))
                throw new ArgumentException("ScenarioActionの具象型を指定してください。", nameof(actionType));
            var action = (ScenarioAction)ScriptableObject.CreateInstance(actionType);
            action.name = actionType.Name;
            AddOwnedAction(scenario, action, -1, "シナリオにアクションを追加");
            return action;
        }

        public static ScenarioAction DuplicateAction(ScenarioData scenario, int index)
        {
            EnsurePersistent(scenario);
            if (scenario.actions == null || index < 0 || index >= scenario.actions.Count || scenario.actions[index] == null)
                throw new ArgumentOutOfRangeException(nameof(index));
            var action = UnityEngine.Object.Instantiate(scenario.actions[index]);
            action.name = scenario.actions[index].name + " (複製)";
            AddOwnedAction(scenario, action, index, "シナリオ専用アクションを複製");
            return action;
        }

        private static void AddOwnedAction(ScenarioData scenario, ScenarioAction action, int replaceIndex, string label)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(label);
            Undo.RegisterCompleteObjectUndo(scenario, label);
            AssetDatabase.AddObjectToAsset(action, scenario);
            Undo.RegisterCreatedObjectUndo(action, label);
            scenario.actions ??= new List<ScenarioAction>();
            if (replaceIndex < 0) scenario.actions.Add(action);
            else scenario.actions[replaceIndex] = action;
            EditorUtility.SetDirty(scenario);
            EditorUtility.SetDirty(action);
            Undo.CollapseUndoOperations(group);
            Invalidate();
        }

        public static void RemoveActionReference(ScenarioData scenario, int index)
        {
            if (scenario == null || scenario.actions == null || index < 0 || index >= scenario.actions.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            Undo.RegisterCompleteObjectUndo(scenario, "シナリオからアクション参照を外す");
            scenario.actions.RemoveAt(index);
            EditorUtility.SetDirty(scenario);
            Invalidate();
        }

        private static void EnsurePersistent(ScenarioData scenario)
        {
            if (scenario == null || !AssetDatabase.Contains(scenario))
                throw new ArgumentException("保存済みのシナリオアセットを指定してください。", nameof(scenario));
        }
    }
}
