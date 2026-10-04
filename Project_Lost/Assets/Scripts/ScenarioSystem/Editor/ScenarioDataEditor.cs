using System;
using System.Collections.Generic;
using System.Linq;
using ScenarioSystem.Adapter;
using ScenarioSystem.Model;
using ScenarioSystem.Model.Validation;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace ScenarioSystem.Editor
{
    [CustomEditor(typeof(ScenarioData))]
    public class ScenarioDataEditor : UnityEditor.Editor
    {
        private const float Line = 22f;
        private ReorderableList _actions;
        private List<ScenarioDataIssue> _issues;

        private void OnEnable()
        {
            var property = serializedObject.FindProperty("actions");
            _actions = new ReorderableList(serializedObject, property, true, true, true, true);
            _actions.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "アクション（ドラッグで順序変更・三角で内容を編集）");
            _actions.elementHeightCallback = ElementHeight;
            _actions.drawElementCallback = DrawAction;
            _actions.onAddDropdownCallback = (_, _) => ShowAddMenu();
            _actions.onRemoveCallback = list =>
            {
                serializedObject.ApplyModifiedProperties();
                ScenarioAuthoringService.RemoveActionReference((ScenarioData)target, list.index);
                serializedObject.Update();
                Changed();
            };
            _actions.onReorderCallback = _ => Changed();
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;
        private void OnUndoRedo() { _issues = null; Repaint(); }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("scenarioId"), new GUIContent("シナリオID"));
            EditorGUILayout.HelpBox("IDは文字列で呼び出す場合に設定します。直接参照だけで再生するシナリオは空欄にできます。", MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("showMainWindow"), new GUIContent("メインウィンドウを表示"));
            _actions.DoLayoutList();
            EditorGUILayout.HelpBox("新規アクションはこのシナリオ内に保存します。－は参照だけを外し、アクションのアセット自体は削除しません。", MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("nextScenario"), new GUIContent("終了後のシナリオ"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("loop"), new GUIContent("ループ再生"));
            if (serializedObject.ApplyModifiedProperties()) Changed();

            if (GUILayout.Button("シナリオデータを検証"))
                _issues = ScenarioValidationGUI.ValidateSelection(new[] { (ScenarioData)target });
            ScenarioValidationGUI.Draw(_issues);
        }

        private void Changed()
        {
            _issues = null;
            ScenarioAuthoringService.Invalidate();
        }

        private float ElementHeight(int index)
        {
            var element = _actions.serializedProperty.GetArrayElementAtIndex(index);
            var action = element.objectReferenceValue as ScenarioAction;
            if (!element.isExpanded || action == null) return Line + 4;
            float height = Line * 3 + 8;
            using var fields = new SerializedObject(action);
            var iterator = fields.GetIterator();
            for (bool children = true; iterator.NextVisible(children); children = false)
                if (iterator.name != "m_Script") height += EditorGUI.GetPropertyHeight(iterator, true) + 4;
            return height;
        }

        private void DrawAction(Rect rect, int index, bool active, bool focused)
        {
            var element = _actions.serializedProperty.GetArrayElementAtIndex(index);
            var action = element.objectReferenceValue as ScenarioAction;
            rect.y += 2;
            element.isExpanded = EditorGUI.Foldout(new Rect(rect.x, rect.y, 32, Line), element.isExpanded, (index + 1).ToString(), true);
            EditorGUI.PropertyField(new Rect(rect.x + 34, rect.y, rect.width - 34, Line - 2), element, GUIContent.none);
            if (!element.isExpanded || action == null) return;
            rect.y += Line;
            int count = ScenarioAuthoringService.ReferenceCount(action);
            string usage = count > 1 ? $"共有：{count}本のシナリオが使用。編集は参照元すべてに反映されます。"
                : "参照元：" + count + "本のシナリオ";
            EditorGUI.LabelField(new Rect(rect.x + 14, rect.y, rect.width - 14, Line), usage, EditorStyles.miniLabel);
            rect.y += Line;
            if (GUI.Button(new Rect(rect.x + 14, rect.y, rect.width - 14, Line - 2), "このシナリオ用に複製して差し替える"))
            {
                serializedObject.ApplyModifiedProperties();
                ScenarioAuthoringService.DuplicateAction((ScenarioData)target, index);
                serializedObject.Update();
                Changed();
                GUIUtility.ExitGUI();
            }
            rect.y += Line + 2;
            using var fields = new SerializedObject(action);
            fields.Update();
            var iterator = fields.GetIterator();
            EditorGUI.indentLevel++;
            try
            {
                for (bool children = true; iterator.NextVisible(children); children = false)
                {
                    if (iterator.name == "m_Script") continue;
                    float height = EditorGUI.GetPropertyHeight(iterator, true);
                    EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, height), iterator, true);
                    rect.y += height + 4;
                }
            }
            finally { EditorGUI.indentLevel--; }
            if (fields.ApplyModifiedProperties()) Changed();
        }

        private void ShowAddMenu()
        {
            serializedObject.ApplyModifiedProperties();
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("既存アクションの参照欄を追加"), false, () =>
            {
                if (target == null) return;
                serializedObject.Update();
                var actions = serializedObject.FindProperty("actions");
                int index = actions.arraySize++;
                actions.GetArrayElementAtIndex(index).objectReferenceValue = null;
                serializedObject.ApplyModifiedProperties();
                Changed();
            });
            menu.AddSeparator("");
            foreach (var type in TypeCache.GetTypesDerivedFrom<ScenarioAction>().Where(type => !type.IsAbstract && !type.ContainsGenericParameters).OrderBy(type => type.Name))
            {
                var selectedType = type;
                menu.AddItem(new GUIContent("新規作成/" + ActionLabel(type.Name)), false, () =>
                {
                    if (target == null) return;
                    ScenarioAuthoringService.AddAction((ScenarioData)target, selectedType);
                    serializedObject.Update();
                    Changed();
                    Repaint();
                });
            }
            menu.ShowAsContext();
        }

        private static string ActionLabel(string name) => name switch
        {
            "DialogueAction" => "会話", "ChoiceAction" => "選択肢", "OverlayAction" => "オーバーレイ会話",
            "WaitAction" => "待機", "EffectAction" => "演出", "ProgressUpdateAction" => "進行状態の更新",
            "ProgressScenarioAction" => "現在の進行に対応するシナリオ", "SceneTransitionAction" => "シーン遷移",
            "ComuToggleAction" => "会話状態の切り替え", "ComuToggleInstantAction" => "会話状態の即時切り替え",
            "CenterPortraitAction" => "中央立ち絵", "PortraitInteractableAction" => "対話ボタンの操作許可",
            "PortraitGuidanceAction" => "立ち絵の誘導表示", "KeywordEnableAction" => "キーワードの操作許可",
            "DialogueStartButtonAction" => "対話開始ボタン",
            "LostNoteCharacterAction" => "ノートの人物登録", "TitleLogoAction" => "タイトルロゴ", _ => name
        };
    }

    [CustomEditor(typeof(ScenarioDataDatabase))]
    public class ScenarioDataDatabaseEditor : UnityEditor.Editor
    {
        private List<ScenarioDataIssue> _issues;

        private void OnEnable() => Undo.undoRedoPerformed += OnUndoRedo;
        private void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;
        private void OnUndoRedo() { _issues = null; Repaint(); }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("allScenarios"), new GUIContent("登録シナリオ"), true);
            if (serializedObject.ApplyModifiedProperties())
            {
                _issues = null;
                ScenarioAuthoringService.Invalidate();
            }
            EditorGUILayout.HelpBox("ID検索の入口を登録します。次のシナリオ・選択肢の遷移先も検証されます。", MessageType.Info);
            if (GUILayout.Button("登録データを検証"))
                _issues = ScenarioValidationGUI.ValidateDatabase((ScenarioDataDatabase)target);
            ScenarioValidationGUI.Draw(_issues);
        }
    }

    internal static class ScenarioValidationGUI
    {
        public static List<ScenarioDataIssue> ValidateDatabase(ScenarioDataDatabase database)
        {
            var roots = database.allScenarios?.Where(scenario => scenario != null).Distinct() ?? Enumerable.Empty<ScenarioData>();
            var issues = ScenarioDataValidator.Validate(roots);
            CheckRegistrations(database, issues);
            return issues;
        }

        private static void CheckRegistrations(ScenarioDataDatabase database, List<ScenarioDataIssue> issues)
        {
            if (database.allScenarios == null)
            {
                issues.Add(new ScenarioDataIssue(ScenarioDataIssueSeverity.Error, "NULL_REGISTRATIONS",
                    "登録シナリオのリストがありません。", database, "allScenarios"));
                return;
            }
            var seen = new HashSet<ScenarioData>();
            for (int i = 0; i < database.allScenarios.Count; i++)
            {
                var scenario = database.allScenarios[i];
                string property = $"allScenarios.Array.data[{i}]";
                if (scenario == null)
                    issues.Add(new ScenarioDataIssue(ScenarioDataIssueSeverity.Error, "NULL_REGISTRATION",
                        $"登録シナリオの {i + 1} 件目に参照がありません。", database, property));
                else if (!seen.Add(scenario))
                    issues.Add(new ScenarioDataIssue(ScenarioDataIssueSeverity.Warning, "REPEATED_REGISTRATION",
                        $"「{scenario.name}」が同じDBへ複数回登録されています。", database, property));
            }
        }

        public static List<ScenarioDataIssue> ValidateSelection(IEnumerable<ScenarioData> selection)
        {
            var selected = selection.Where(scenario => scenario != null).Distinct().ToList();
            var contexts = new HashSet<UnityEngine.Object>();
            foreach (var scenario in ScenarioGraph.Collect(selected))
            {
                contexts.Add(scenario);
                if (scenario.actions != null)
                    foreach (var action in scenario.actions) if (action != null) contexts.Add(action);
            }
            var covered = new HashSet<ScenarioData>();
            var issues = new List<ScenarioDataIssue>();
            foreach (var database in ScenarioAuthoringService.Databases)
            {
                var graph = ScenarioGraph.Collect(database.allScenarios);
                var matching = selected.Where(graph.Contains).ToList();
                if (matching.Count == 0) continue;
                covered.UnionWith(matching);
                foreach (var issue in ValidateDatabase(database).Where(issue => contexts.Contains(issue.Context)))
                    issues.Add(new ScenarioDataIssue(issue.Severity, issue.Code, $"[{database.name}] {issue.Message}", issue.Context, issue.PropertyPath));
            }
            foreach (var scenario in selected.Where(scenario => !covered.Contains(scenario)))
            {
                issues.Add(new ScenarioDataIssue(ScenarioDataIssueSeverity.Warning, "NO_DATABASE_CONTEXT",
                    "どのDBからも参照されていないため、このシナリオから辿れる範囲のみ検証しました。独立したキーワード詳細は、同じDBに登録して検証してください。",
                    scenario, "scenarioId"));
                issues.AddRange(ScenarioDataValidator.ValidateScenario(scenario));
            }
            return issues;
        }

        public static List<ScenarioDataIssue> ValidateProject()
        {
            ScenarioAuthoringService.Invalidate();
            var issues = new List<ScenarioDataIssue>();
            var registered = new HashSet<ScenarioData>();
            foreach (var database in ScenarioAuthoringService.Databases)
            {
                registered.UnionWith(ScenarioGraph.Collect(database.allScenarios));
                foreach (var issue in ValidateDatabase(database))
                    issues.Add(new ScenarioDataIssue(issue.Severity, issue.Code, $"[{database.name}] {issue.Message}", issue.Context, issue.PropertyPath));
            }
            foreach (var scenario in ScenarioAuthoringService.Scenarios)
                if (!registered.Contains(scenario) && !string.IsNullOrWhiteSpace(scenario.scenarioId))
                    issues.Add(new ScenarioDataIssue(ScenarioDataIssueSeverity.Warning, "UNREGISTERED_SCENARIO",
                        "IDが設定されていますが、どのDBからも参照されていません。ID検索で再生する場合は登録してください。直接参照専用なら登録は不要です。",
                        scenario, "scenarioId"));
            return issues;
        }

        public static void Draw(List<ScenarioDataIssue> issues)
        {
            if (issues == null) return;
            if (issues.Count == 0) EditorGUILayout.HelpBox("データの問題は検出されませんでした。", MessageType.Info);
            foreach (var issue in issues)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.HelpBox(issue.Message, issue.Severity == ScenarioDataIssueSeverity.Error ? MessageType.Error : MessageType.Warning);
                    string location = (issue.Context != null ? issue.Context.name : "データ") + " / " + issue.PropertyPath;
                    if (GUILayout.Button(location, EditorStyles.linkLabel) && issue.Context != null)
                    {
                        Selection.activeObject = issue.Context;
                        EditorGUIUtility.PingObject(issue.Context);
                    }
                }
            }
        }

        [MenuItem("Scenario System/Validate Scenario Data", false, 51)]
        private static void ValidateAll()
        {
            var issues = ValidateProject();
            foreach (var issue in issues)
            {
                string message = $"[シナリオ検証/{issue.Code}] {issue.Message} ({issue.PropertyPath})";
                if (issue.Severity == ScenarioDataIssueSeverity.Error) Debug.LogError(message, issue.Context);
                else Debug.LogWarning(message, issue.Context);
            }
            Debug.Log($"[シナリオ検証] {ScenarioAuthoringService.Databases.Count}件のDBを検証しました。指摘 {issues.Count}件。データは変更していません。");
        }
    }
}
