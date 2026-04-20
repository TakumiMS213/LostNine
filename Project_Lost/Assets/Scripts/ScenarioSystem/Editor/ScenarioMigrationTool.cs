#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using MessageWindowSystem.Data;
using ScenarioSystem.Model;
using ScenarioSystem.Model.Actions;

/// <summary>
/// 旧 DialogueScenario SO を新 ScenarioData SO に変換するマイグレーションツール。
/// 
/// 使い方:
///   1. Project で変換したい DialogueScenario を選択
///   2. Scenario System → Migrate Selected Scenarios
///   3. 出力先フォルダに新 SO が生成される
/// </summary>
public static class ScenarioMigrationTool
{
    private const string DefaultOutputFolder = "Assets/ScenarioData_Migrated";

    // ═══════════════════════════════
    //  Menu Commands
    // ═══════════════════════════════

    [MenuItem("Scenario System/Migrate Selected Scenarios", false, 200)]
    public static void MigrateSelected()
    {
        var selected = Selection.objects;
        var scenarios = new List<DialogueScenario>();

        foreach (var obj in selected)
        {
            if (obj is DialogueScenario ds)
                scenarios.Add(ds);
        }

        if (scenarios.Count == 0)
        {
            EditorUtility.DisplayDialog("エラー",
                "Project ウィンドウで DialogueScenario を1つ以上選択してから実行してください。",
                "OK");
            return;
        }

        // 出力先
        string outputFolder = EditorUtility.SaveFolderPanel(
            "変換先フォルダを選択", "Assets", "ScenarioData_Migrated");

        if (string.IsNullOrEmpty(outputFolder)) return;

        // Assets/ 相対パスに変換
        if (outputFolder.StartsWith(Application.dataPath))
            outputFolder = "Assets" + outputFolder.Substring(Application.dataPath.Length);

        if (!AssetDatabase.IsValidFolder(outputFolder))
        {
            string parent = Path.GetDirectoryName(outputFolder).Replace("\\", "/");
            string folderName = Path.GetFileName(outputFolder);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        int successCount = 0;
        int errorCount = 0;
        var report = new System.Text.StringBuilder();
        report.AppendLine("── マイグレーションレポート ──");

        foreach (var oldScenario in scenarios)
        {
            try
            {
                MigrateSingle(oldScenario, outputFolder, report);
                successCount++;
            }
            catch (System.Exception e)
            {
                report.AppendLine($"  ❌ {oldScenario.name}: {e.Message}");
                errorCount++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        report.AppendLine($"\n✅ 成功: {successCount} / ❌ 失敗: {errorCount}");
        Debug.Log($"[Migration] {report}");

        EditorUtility.DisplayDialog("マイグレーション完了",
            $"成功: {successCount} / 失敗: {errorCount}\n\n" +
            $"出力先: {outputFolder}\n\n" +
            "詳細は Console を確認してください。",
            "OK");
    }

    [MenuItem("Scenario System/Migrate All in Database", false, 201)]
    public static void MigrateAllFromDatabase()
    {
        var database = FindScenarioDatabase();
        if (database == null)
        {
            EditorUtility.DisplayDialog("エラー",
                "ScenarioDatabase が見つかりません。", "OK");
            return;
        }

        Selection.objects = database.allScenarios.ToArray();
        MigrateSelected();
    }

    // ═══════════════════════════════
    //  Core Migration
    // ═══════════════════════════════

    private static void MigrateSingle(DialogueScenario old, string outputFolder,
                                       System.Text.StringBuilder report)
    {
        string scenarioFolder = $"{outputFolder}/{old.name}";
        if (!AssetDatabase.IsValidFolder(scenarioFolder))
        {
            string parent = outputFolder;
            string folderName = old.name;
            AssetDatabase.CreateFolder(parent, folderName);
        }

        // ── 1) 各 DialogueLine を ScenarioAction SO に変換 ──
        var actions = new List<ScenarioAction>();
        int lineIndex = 0;

        if (old.lines != null)
        {
            foreach (var line in old.lines)
            {
                // エフェクトを先に追加（旧システムでは行ごとにエフェクトリストがある）
                if (line.effects != null && line.effects.Count > 0)
                {
                    foreach (var effect in line.effects)
                    {
                        var effectAction = ScriptableObject.CreateInstance<EffectAction>();
                        effectAction.name = $"{old.name}_Effect_{lineIndex}_{effect.effectType}";
                        effectAction.effectType = ConvertEffectType(effect.effectType);
                        effectAction.floatParam = effect.floatParam;
                        effectAction.stringParam = effect.stringParam;
                        effectAction.spriteParam = effect.spriteParam;
                        effectAction.colorParam = effect.colorParam;

                        string effectPath = $"{scenarioFolder}/{effectAction.name}.asset";
                        AssetDatabase.CreateAsset(effectAction, effectPath);
                        actions.Add(effectAction);
                    }
                }

                // DialogueAction
                var dialogueAction = ScriptableObject.CreateInstance<DialogueAction>();
                dialogueAction.name = $"{old.name}_Dialogue_{lineIndex:D3}";
                dialogueAction.speakerName = line.speakerName ?? "";
                dialogueAction.text = line.text ?? "";
                dialogueAction.portrait = line.portrait;
                dialogueAction.portraitPosition = ConvertPortraitPosition(line.portraitPosition);
                dialogueAction.typingSpeed = line.typingSpeed;
                dialogueAction.nameSlideDirection = ConvertNameSlideDirection(line.nameSlideDirection);
                dialogueAction.voiceClip = line.voiceClip;
                dialogueAction.backgroundImage = line.backgroundImage;

                string dialoguePath = $"{scenarioFolder}/{dialogueAction.name}.asset";
                AssetDatabase.CreateAsset(dialogueAction, dialoguePath);
                actions.Add(dialogueAction);

                // 選択肢がある場合
                if (line.choices != null && line.choices.Count > 0)
                {
                    var choiceAction = ScriptableObject.CreateInstance<ChoiceAction>();
                    choiceAction.name = $"{old.name}_Choice_{lineIndex:D3}";

                    var entries = new List<ScenarioSystem.Model.Actions.ChoiceEntry>();
                    foreach (var choice in line.choices)
                    {
                        entries.Add(new ScenarioSystem.Model.Actions.ChoiceEntry
                        {
                            choiceText = choice.choiceText,
                            choiceId = choice.choiceId
                            // nextScenario は ScenarioData 内のリンクとして手動設定が必要
                        });
                    }
                    choiceAction.choices = entries;

                    string choicePath = $"{scenarioFolder}/{choiceAction.name}.asset";
                    AssetDatabase.CreateAsset(choiceAction, choicePath);
                    actions.Add(choiceAction);
                }

                // interruptScenario は注釈として記録（新システムでは別アプローチ）
                if (line.interruptScenario != null)
                {
                    report.AppendLine($"  ⚠️ {old.name} Line[{lineIndex}]: interruptScenario '{line.interruptScenario.name}' は手動移行が必要");
                }

                lineIndex++;
            }
        }

        // ── 2) シナリオ完了時のアクションを追加 ──

        // ProgressUpdate
        if (old.updateProgressOnEnd)
        {
            var progressAction = ScriptableObject.CreateInstance<ProgressUpdateAction>();
            progressAction.name = $"{old.name}_ProgressUpdate";
            progressAction.actionType = ConvertProgressActionType(old.progressAction);
            progressAction.targetChapter = old.targetChapter;
            progressAction.targetPhase = old.targetPhase;

            string progressPath = $"{scenarioFolder}/{progressAction.name}.asset";
            AssetDatabase.CreateAsset(progressAction, progressPath);
            actions.Add(progressAction);
        }

        // ComuToggle
        if (old.toggleComuOnEnd)
        {
            var comuAction = ScriptableObject.CreateInstance<ComuToggleAction>();
            comuAction.name = $"{old.name}_ComuToggle";

            string comuPath = $"{scenarioFolder}/{comuAction.name}.asset";
            AssetDatabase.CreateAsset(comuAction, comuPath);
            actions.Add(comuAction);
        }

        // KeywordEnable
        if (!old.enableKeywords)
        {
            var kwAction = ScriptableObject.CreateInstance<KeywordEnableAction>();
            kwAction.name = $"{old.name}_KeywordDisable";
            kwAction.enable = false;

            string kwPath = $"{scenarioFolder}/{kwAction.name}.asset";
            AssetDatabase.CreateAsset(kwAction, kwPath);
            // キーワード無効化は先頭に挿入
            actions.Insert(0, kwAction);
        }

        // ── 3) ScenarioData を作成 ──
        var scenarioData = ScriptableObject.CreateInstance<ScenarioData>();
        scenarioData.name = old.name;
        scenarioData.scenarioId = old.scenarioId ?? old.name;
        scenarioData.actions = actions;

        string scenarioPath = $"{scenarioFolder}/{old.name}.asset";
        AssetDatabase.CreateAsset(scenarioData, scenarioPath);

        // ── 4) 結果レポート ──
        report.AppendLine($"  ✅ {old.name}: {lineIndex} lines → {actions.Count} actions ({scenarioPath})");

        if (old.nextScenario != null)
            report.AppendLine($"     ⚠️ nextScenario '{old.nextScenario.name}' のリンクは手動設定が必要");

        if (old.loopScenario)
            report.AppendLine($"     ⚠️ loopScenario=true は新システムで手動設定が必要");
    }

    // ═══════════════════════════════
    //  Type Converters
    // ═══════════════════════════════

    private static ScenarioEffectType ConvertEffectType(EffectType old)
    {
        return old switch
        {
            EffectType.Shake => ScenarioEffectType.Shake,
            EffectType.Flash => ScenarioEffectType.Flash,
            EffectType.FadeIn => ScenarioEffectType.FadeIn,
            EffectType.FadeOut => ScenarioEffectType.FadeOut,
            EffectType.PlaySE => ScenarioEffectType.PlaySE,
            EffectType.PlayBGM => ScenarioEffectType.PlayBGM,
            EffectType.StopBGM => ScenarioEffectType.StopBGM,
            EffectType.ShowImage => ScenarioEffectType.ShowImage,
            EffectType.HideImage => ScenarioEffectType.HideImage,
            _ => ScenarioEffectType.None
        };
    }

    private static ScenarioSystem.Model.Actions.PortraitPosition ConvertPortraitPosition(
        MessageWindowSystem.Data.PortraitPosition old)
    {
        return old switch
        {
            MessageWindowSystem.Data.PortraitPosition.Left => ScenarioSystem.Model.Actions.PortraitPosition.Left,
            MessageWindowSystem.Data.PortraitPosition.Right => ScenarioSystem.Model.Actions.PortraitPosition.Right,
            _ => ScenarioSystem.Model.Actions.PortraitPosition.Center
        };
    }

    private static ScenarioSystem.Model.Actions.NameSlideDirection ConvertNameSlideDirection(
        MessageWindowSystem.Data.NameSlideDirection old)
    {
        return old switch
        {
            MessageWindowSystem.Data.NameSlideDirection.Left => ScenarioSystem.Model.Actions.NameSlideDirection.Left,
            MessageWindowSystem.Data.NameSlideDirection.Right => ScenarioSystem.Model.Actions.NameSlideDirection.Right,
            _ => ScenarioSystem.Model.Actions.NameSlideDirection.Default
        };
    }

    private static ScenarioProgressActionType ConvertProgressActionType(
        MessageWindowSystem.Data.ProgressActionType old)
    {
        return old switch
        {
            MessageWindowSystem.Data.ProgressActionType.AdvanceChapter =>
                ScenarioProgressActionType.AdvanceChapter,
            MessageWindowSystem.Data.ProgressActionType.SetDirectly =>
                ScenarioProgressActionType.SetDirectly,
            _ => ScenarioProgressActionType.AdvancePhase
        };
    }

    // ═══════════════════════════════
    //  Utility
    // ═══════════════════════════════

    private static ScenarioDatabase FindScenarioDatabase()
    {
        var guids = AssetDatabase.FindAssets("t:ScenarioDatabase");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var db = AssetDatabase.LoadAssetAtPath<ScenarioDatabase>(path);
            if (db != null) return db;
        }
        return null;
    }
}
#endif
