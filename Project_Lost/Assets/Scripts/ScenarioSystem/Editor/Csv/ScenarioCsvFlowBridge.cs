using System;
using System.Collections.Generic;
using System.IO;
using System_Script.Flow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ScenarioSystem.Editor.Csv
{
    /// <summary>既存フローをカタログ化する。別シーンの未保存編集は開いたまま保持する。</summary>
    public static class ScenarioCsvFlowBridge
    {
        public static void MigrateMainScene(string catalogPath)
            => MigrateScene("Assets/Scenes/Main.unity", catalogPath);

        public static void MigrateScene(string scenePath, string catalogPath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("編集モードで、コンパイル・インポート完了後に移行してください。");
            ValidateAssetPath(catalogPath);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                throw new ArgumentException("移行元シーンが見つかりません: " + scenePath, nameof(scenePath));

            var existingAsset = AssetDatabase.LoadMainAssetAtPath(catalogPath);
            var catalog = existingAsset as ScenarioFlowCatalog;
            if (existingAsset != null && catalog == null || existingAsset == null && File.Exists(catalogPath))
                throw new InvalidOperationException("指定先にフローカタログ以外のファイルがあります: " + catalogPath);

            var scene = SceneManager.GetSceneByPath(scenePath);
            bool wasPresent = scene.IsValid();
            bool wasLoaded = wasPresent && scene.isLoaded;
            if (wasLoaded && scene.isDirty)
                throw new InvalidOperationException("移行元シーンに未保存の変更があります。先に保存してください: " + scenePath);

            var previousActive = SceneManager.GetActiveScene();
            var previousSelection = Selection.objects;
            bool createdCatalog = false;
            bool attachedCatalog = false;
            bool savedScene = false;
            GameFlowDirector director = null;
            try
            {
                if (!wasLoaded) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                director = FindDirector(scene);
                using var source = new SerializedObject(director);
                var catalogProperty = source.FindProperty("flowCatalog");
                var previousCatalog = catalogProperty.objectReferenceValue;
                if (previousCatalog != null)
                {
                    if (previousCatalog != catalog)
                        throw new InvalidOperationException("シーンには別のフローカタログが設定されています。");
                    return;
                }

                using (var settings = catalog != null ? new SerializedObject(catalog) : new SerializedObject(director))
                    ValidateSettings(settings);

                if (catalog == null)
                {
                    EnsureFolder(Path.GetDirectoryName(catalogPath).Replace('\\', '/'));
                    catalog = ScriptableObject.CreateInstance<ScenarioFlowCatalog>();
                    catalog.name = Path.GetFileNameWithoutExtension(catalogPath);
                    using (var target = new SerializedObject(catalog))
                    {
                        CopySettings(source, target);
                        target.ApplyModifiedPropertiesWithoutUndo();
                    }
                    AssetDatabase.CreateAsset(catalog, catalogPath);
                    createdCatalog = true;
                    AssetDatabase.SaveAssetIfDirty(catalog);
                }

                // 既存カタログの内容やGUIDは変更しない。シーンには参照だけを追加する。
                catalogProperty.objectReferenceValue = catalog;
                source.ApplyModifiedPropertiesWithoutUndo();
                attachedCatalog = true;
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new IOException("移行したシーンを保存できませんでした: " + scenePath);
                savedScene = true;
            }
            catch
            {
                if (attachedCatalog && !savedScene && director != null)
                {
                    using var rollback = new SerializedObject(director);
                    rollback.FindProperty("flowCatalog").objectReferenceValue = null;
                    rollback.ApplyModifiedPropertiesWithoutUndo();
                }
                if (createdCatalog && !savedScene) AssetDatabase.DeleteAsset(catalogPath);
                else if (catalog != null && !AssetDatabase.Contains(catalog)) UnityEngine.Object.DestroyImmediate(catalog);
                throw;
            }
            finally
            {
                if (!wasLoaded && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, !wasPresent);
                if (previousActive.IsValid() && previousActive.isLoaded)
                    SceneManager.SetActiveScene(previousActive);
                Selection.objects = previousSelection;
            }
        }

        private static void ValidateAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", StringComparison.Ordinal)
                || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) || path.Contains('\\'))
                throw new ArgumentException("カタログの保存先はAssets以下の.assetパスで指定してください。", nameof(path));
            foreach (string segment in path.Split('/'))
                if (segment.Length == 0 || segment == "." || segment == ".." || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    throw new ArgumentException("カタログの保存先に不正なパス要素があります。", nameof(path));
        }

        private static GameFlowDirector FindDirector(Scene scene)
        {
            GameFlowDirector result = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null) continue;
                foreach (var candidate in root.GetComponentsInChildren<GameFlowDirector>(true))
                {
                    if (candidate == null) continue;
                    if (result != null)
                        throw new InvalidOperationException("移行対象のGameFlowDirectorが複数あります。");
                    result = candidate;
                }
            }
            return result != null ? result : throw new InvalidOperationException("移行元にGameFlowDirectorがありません。");
        }

        private static void ValidateSettings(SerializedObject settings)
        {
            var entries = settings.FindProperty("overrideSequences");
            var keys = new HashSet<(int, int)>();
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                int chapter = entry.FindPropertyRelative("targetChapter").intValue;
                int phase = entry.FindPropertyRelative("targetPhase").intValue;
                if (chapter <= 0 || !Enum.IsDefined(typeof(GamePhase), phase)
                    || entry.FindPropertyRelative("sequence").objectReferenceValue == null)
                    throw new InvalidOperationException($"フロー登録 {i + 1} の章・フェーズ・シーケンスを確認してください。");
                if (!keys.Add((chapter, phase)))
                    throw new InvalidOperationException($"章 {chapter} / {(GamePhase)phase} のフロー登録が重複しています。");
            }
        }

        private static void CopySettings(SerializedObject source, SerializedObject target)
        {
            target.FindProperty("startingSequence").objectReferenceValue = source.FindProperty("startingSequence").objectReferenceValue;
            var from = source.FindProperty("overrideSequences");
            var to = target.FindProperty("overrideSequences");
            to.arraySize = from.arraySize;
            for (int i = 0; i < from.arraySize; i++)
            {
                var sourceEntry = from.GetArrayElementAtIndex(i);
                var targetEntry = to.GetArrayElementAtIndex(i);
                targetEntry.FindPropertyRelative("targetChapter").intValue = sourceEntry.FindPropertyRelative("targetChapter").intValue;
                targetEntry.FindPropertyRelative("targetPhase").intValue = sourceEntry.FindPropertyRelative("targetPhase").intValue;
                targetEntry.FindPropertyRelative("sequence").objectReferenceValue = sourceEntry.FindPropertyRelative("sequence").objectReferenceValue;
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
                throw new IOException("フローカタログのフォルダーを作成できません: " + path);
        }
    }
}
