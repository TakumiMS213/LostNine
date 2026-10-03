using System;
using System.Collections.Generic;
using MessageWindowSystem.Core;
using ScenarioSystem.Model.Actions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ScenarioSystem.Model.Validation
{
    public enum ScenarioDataIssueSeverity { Warning, Error }

    /// <summary>アセットを変更せず、修正箇所と理由を呼び出し側へ返す。</summary>
    public sealed class ScenarioDataIssue
    {
        public ScenarioDataIssueSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }
        public Object Context { get; }
        public string PropertyPath { get; }

        public ScenarioDataIssue(ScenarioDataIssueSeverity severity, string code, string message,
            Object context, string propertyPath)
        {
            Severity = severity;
            Code = code;
            Message = message;
            Context = context;
            PropertyPath = propertyPath;
        }
    }

    /// <summary>
    /// 再生結果を変えずにシナリオの参照グラフを検証する。
    /// 空IDの直参照シナリオ、同じアクションの共有、入力を挟むループは有効。
    /// </summary>
    public static class ScenarioDataValidator
    {
        public static List<ScenarioDataIssue> ValidateScenario(ScenarioData scenario)
            => Validate(new[] { scenario });

        public static List<ScenarioDataIssue> Validate(IEnumerable<ScenarioData> roots)
        {
            var issues = new List<ScenarioDataIssue>();
            var rootList = roots == null ? new List<ScenarioData>() : new List<ScenarioData>(roots);
            if (roots == null)
                Error(issues, "NULL_ROOTS", "検証対象のシナリオ一覧がありません。", null, "");
            for (int i = 0; i < rootList.Count; i++)
                if (rootList[i] == null)
                    Error(issues, "NULL_SCENARIO", $"シナリオ一覧の {i + 1} 件目に参照がありません。", null, $"allScenarios.Array.data[{i}]");

            var scenarios = ScenarioGraph.Collect(rootList);
            var ids = new Dictionary<string, ScenarioData>(StringComparer.Ordinal);
            foreach (var scenario in scenarios)
            {
                string id = ScenarioKey.Normalize(scenario.scenarioId);
                if (id.Length == 0) continue;
                if (ids.TryGetValue(id, out var existing))
                    Error(issues, "DUPLICATE_ID", $"ID「{id}」が「{existing.name}」と重複しています。", scenario, "scenarioId");
                else
                    ids.Add(id, scenario);
                if (!string.Equals(id, scenario.scenarioId, StringComparison.Ordinal))
                    Warning(issues, "ID_WHITESPACE", "シナリオIDの前後に空白があります。検索時には除去されます。", scenario, "scenarioId");
            }

            var checkedActions = new HashSet<ScenarioAction>();
            foreach (var scenario in scenarios)
            {
                if (scenario.loop && scenario.nextScenario != null)
                    Warning(issues, "LOOP_NEXT_CONFLICT", "ループが優先されるため、終了後のシナリオには進みません。", scenario, "nextScenario");
                if (scenario.actions == null)
                {
                    Error(issues, "NULL_ACTIONS", "アクション一覧がありません。", scenario, "actions");
                    continue;
                }
                for (int i = 0; i < scenario.actions.Count; i++)
                {
                    var action = scenario.actions[i];
                    string path = $"actions.Array.data[{i}]";
                    if (action == null)
                    {
                        Error(issues, "NULL_ACTION", $"{i + 1} 番目のアクション参照がなく、この位置でシナリオが終了します。", scenario, path);
                        continue;
                    }
                    if (i > 0 && action == scenario.actions[i - 1])
                        Warning(issues, "REPEATED_ACTION", $"「{action.name}」が連続登録されています。意図した繰り返しか確認してください。", scenario, path);
                    if (i + 1 < scenario.actions.Count && IsTerminalAction(action))
                        Warning(issues, "UNREACHABLE_ACTIONS", $"「{action.name}」以降のアクションには通常進みません。末尾への配置を確認してください。", scenario, path);
                    if (!scenario.showMainWindow && (action is DialogueAction || action is ChoiceAction))
                        Warning(issues, "HIDDEN_MAIN_WINDOW", "メインウィンドウを非表示にするシナリオに、本文または選択肢があります。表示先を確認してください。", scenario, path);
                    if (checkedActions.Add(action)) ValidateAction(action, ids, issues);
                }
            }
            ValidateImmediateCycles(scenarios, issues);
            return issues;
        }

        private static void ValidateAction(ScenarioAction action, Dictionary<string, ScenarioData> ids,
            List<ScenarioDataIssue> issues)
        {
            switch (action)
            {
                case DialogueAction dialogue:
                    if (dialogue.entries == null || dialogue.entries.Count == 0)
                    {
                        Error(issues, "EMPTY_DIALOGUE", "会話の本文が一行も登録されていません。", action, "entries");
                        break;
                    }
                    for (int i = 0; i < dialogue.entries.Count; i++)
                    {
                        var entry = dialogue.entries[i];
                        string path = $"entries.Array.data[{i}]";
                        if (string.IsNullOrWhiteSpace(entry.text))
                            Warning(issues, "EMPTY_TEXT", "本文が空です。空行による入力待ちが必要か確認してください。", action, path + ".text");
                        ValidateEnum(entry.portraitPosition, action, path + ".portraitPosition", issues);
                        ValidateEnum(entry.nameSlideDirection, action, path + ".nameSlideDirection", issues);
                        ValidateNumber(entry.typingSpeed, true, action, path + ".typingSpeed", issues);
                        ValidateKeywords(entry.text, ids, action, path + ".text", issues);
                    }
                    break;
                case ChoiceAction choice:
                    ValidateChoices(choice, issues);
                    break;
                case OverlayAction overlay:
                    if (string.IsNullOrWhiteSpace(overlay.text))
                        Warning(issues, "EMPTY_TEXT", "オーバーレイの本文が空です。", action, "text");
                    ValidateEnum(overlay.portraitPosition, action, "portraitPosition", issues);
                    // 0以下はクリック待ちという既存仕様。負数を不正扱いしない。
                    ValidateNumber(overlay.displayDuration, false, action, "displayDuration", issues);
                    break;
                case WaitAction wait:
                    ValidateNumber(wait.duration, true, action, "duration", issues);
                    break;
                case EffectAction effect:
                    ValidateEnum(effect.effectType, action, "effectType", issues);
                    ValidateNumber(effect.floatParam, false, action, "floatParam", issues);
                    break;
                case ProgressUpdateAction progress:
                    ValidateEnum(progress.actionType, action, "actionType", issues);
                    if (progress.actionType == ScenarioProgressActionType.SetDirectly)
                    {
                        ValidateEnum(progress.targetPhase, action, "targetPhase", issues);
                        if (progress.targetChapter < 1)
                            Error(issues, "INVALID_CHAPTER", "直接指定する章番号は1以上にしてください。", action, "targetChapter");
                    }
                    break;
                case SceneTransitionAction transition:
                    if (!transition.useChapterSelect && string.IsNullOrWhiteSpace(transition.targetSceneName))
                        Error(issues, "MISSING_SCENE", "チャプター選択を使用しない場合は、遷移先シーン名が必要です。", action, "targetSceneName");
                    break;
                case TitleLogoAction logo:
                    ValidateNumber(logo.backgroundFadeInDuration, true, action, "backgroundFadeInDuration", issues);
                    ValidateNumber(logo.logoFadeInDuration, true, action, "logoFadeInDuration", issues);
                    ValidateNumber(logo.logoDisplayDuration, true, action, "logoDisplayDuration", issues);
                    ValidateNumber(logo.logoFadeOutDuration, true, action, "logoFadeOutDuration", issues);
                    ValidateNumber(logo.backgroundHoldDuration, true, action, "backgroundHoldDuration", issues);
                    ValidateNumber(logo.backgroundFadeOutDuration, true, action, "backgroundFadeOutDuration", issues);
                    break;
            }
        }

        private static void ValidateChoices(ChoiceAction action, List<ScenarioDataIssue> issues)
        {
            if (action.choices == null || action.choices.Count == 0)
            {
                Error(issues, "EMPTY_CHOICES", "選択肢が一つも登録されていません。", action, "choices");
                return;
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < action.choices.Count; i++)
            {
                var choice = action.choices[i];
                string path = $"choices.Array.data[{i}]";
                if (choice == null)
                {
                    Error(issues, "NULL_CHOICE", "選択肢の内容がありません。このボタンでは先へ進めません。", action, path);
                    continue;
                }
                if (string.IsNullOrWhiteSpace(choice.choiceText))
                    Error(issues, "EMPTY_CHOICE_TEXT", "ボタンに表示する選択肢の文言がありません。", action, path + ".choiceText");
                string id = ScenarioKey.Normalize(choice.choiceId);
                if (id.Length > 0 && !ids.Add(id))
                    Warning(issues, "DUPLICATE_CHOICE_ID", $"選択肢ID「{id}」が同じアクション内で重複しています。", action, path + ".choiceId");
            }
        }

        private static void ValidateKeywords(string text, Dictionary<string, ScenarioData> ids,
            Object context, string path, List<ScenarioDataIssue> issues)
        {
            var checkedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in KeywordTextFormatter.GetKeywordIds(text))
            {
                if (!checkedIds.Add(id)) continue;
                if (id.Length == 0)
                    Error(issues, "EMPTY_KEYWORD_ID", "キーワードリンクのIDが空です。", context, path);
                else if (!KeywordHandler.IsDummyKeyword(id) && !ids.ContainsKey(id))
                    Error(issues, "MISSING_KEYWORD_SCENARIO", $"キーワード「{id}」のシナリオが検証対象にありません。DBへの登録も確認してください。", context, path);
            }
        }

        private static bool IsTerminalAction(ScenarioAction action)
            => action is ChoiceAction choice && choice.choices != null && choice.choices.Count > 0
                || action is SceneTransitionAction scene && (scene.useChapterSelect || !string.IsNullOrWhiteSpace(scene.targetSceneName));

        /// <summary>
        /// 入力・待機を挟まない next/loop の循環だけを検出する。
        /// 選択分岐やProgressScenarioの動的な遷移は決めつけず、入力後の通常ループも許容する。
        /// </summary>
        private static void ValidateImmediateCycles(List<ScenarioData> scenarios, List<ScenarioDataIssue> issues)
        {
            var successors = new Dictionary<ScenarioData, ScenarioData>();
            foreach (var scenario in scenarios)
            {
                if (CanFinishImmediately(scenario))
                {
                    var next = scenario.loop ? scenario : scenario.nextScenario;
                    if (next != null) successors[scenario] = next;
                }
            }
            var visited = new HashSet<ScenarioData>();
            foreach (var start in scenarios)
            {
                if (visited.Contains(start)) continue;
                var path = new List<ScenarioData>();
                var positions = new Dictionary<ScenarioData, int>();
                var current = start;
                while (current != null && !visited.Contains(current))
                {
                    if (positions.TryGetValue(current, out int cycleStart))
                    {
                        var names = new List<string>();
                        for (int i = cycleStart; i < path.Count; i++) names.Add(path[i].name);
                        names.Add(current.name);
                        Error(issues, "IMMEDIATE_CYCLE", "入力・時間待ちのない循環があります: " + string.Join(" → ", names)
                            + "。再生すると処理が終了せず、スタックオーバーフローにつながります。", current, current.loop ? "loop" : "nextScenario");
                        break;
                    }
                    positions.Add(current, path.Count);
                    path.Add(current);
                    if (!successors.TryGetValue(current, out current)) break;
                }
                foreach (var item in path) visited.Add(item);
            }
        }

        private static bool CanFinishImmediately(ScenarioData scenario)
        {
            if (scenario.actions == null) return true;
            foreach (var action in scenario.actions)
            {
                // Presenterはnullアクションで終了し、後続を実行しない。
                if (action == null) return true;
                if (action is DialogueAction dialogue && dialogue.entries != null && dialogue.entries.Count > 0) return false;
                if (action is ChoiceAction choice && choice.choices != null && choice.choices.Count > 0) return false;
                if (action is OverlayAction || action is ProgressScenarioAction || IsTerminalAction(action)) return false;
                if (action is WaitAction wait && wait.duration > 0f) return false;
                if (action is TitleLogoAction logo && logo.TotalDuration > 0f) return false;
            }
            return true;
        }

        private static void ValidateEnum<T>(T value, Object context, string path, List<ScenarioDataIssue> issues) where T : struct, Enum
        {
            if (!Enum.IsDefined(typeof(T), value))
                Error(issues, "INVALID_ENUM", $"未定義の {typeof(T).Name} 値「{value}」が設定されています。", context, path);
        }

        private static void ValidateNumber(float value, bool nonNegative, Object context, string path, List<ScenarioDataIssue> issues)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || nonNegative && value < 0f)
                Error(issues, "INVALID_NUMBER", $"{path} に有効な{(nonNegative ? "0以上の" : "")}有限数を設定してください。", context, path);
        }

        private static void Error(List<ScenarioDataIssue> issues, string code, string message, Object context, string path)
            => issues.Add(new ScenarioDataIssue(ScenarioDataIssueSeverity.Error, code, message, context, path));

        private static void Warning(List<ScenarioDataIssue> issues, string code, string message, Object context, string path)
            => issues.Add(new ScenarioDataIssue(ScenarioDataIssueSeverity.Warning, code, message, context, path));
    }
}
