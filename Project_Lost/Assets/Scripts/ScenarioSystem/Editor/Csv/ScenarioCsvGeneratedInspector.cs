using System;
using System.IO;
using ScenarioSystem.Model;
using System_Script.Flow;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ScenarioSystem.Editor.Csv
{
    internal static class ScenarioCsvGeneratedInspector
    {
        public static bool DrawManaged(UnityEngine.Object target)
        {
            if (!ScenarioCsvService.IsManaged(target)) return false;
            EditorGUILayout.HelpBox("このデータはCSVから生成されています。本文・演出・進行はシナリオCSV画面で編集してください。", MessageType.Info);
            if (GUILayout.Button("シナリオCSVを開く")) ScenarioCsvWindow.Open();
            using (new EditorGUI.DisabledScope(true))
            using (var serialized = new SerializedObject(target))
            {
                var property = serialized.GetIterator();
                while (property.NextVisible(false))
                    EditorGUILayout.PropertyField(property, true);
            }
            return true;
        }
    }

    [CustomEditor(typeof(ScenarioAction), true)]
    internal sealed class ScenarioCsvActionInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (!ScenarioCsvGeneratedInspector.DrawManaged(target)) DrawDefaultInspector();
        }
    }

    [CustomEditor(typeof(FlowStep), true)]
    internal sealed class ScenarioCsvStepInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (!ScenarioCsvGeneratedInspector.DrawManaged(target)) DrawDefaultInspector();
        }
    }

    [CustomEditor(typeof(StorySequence))]
    internal sealed class ScenarioCsvSequenceInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (!ScenarioCsvGeneratedInspector.DrawManaged(target)) DrawDefaultInspector();
        }
    }

    [CustomEditor(typeof(ScenarioFlowCatalog))]
    internal sealed class ScenarioCsvFlowInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (!ScenarioCsvGeneratedInspector.DrawManaged(target)) DrawDefaultInspector();
        }
    }

    /// <summary>Prevent a player build from silently using an older generated version of the CSV.</summary>
    internal sealed class ScenarioCsvBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;
        public void OnPreprocessBuild(BuildReport report)
        {
            string folder = ScenarioCsvService.DefaultFolder;
            if (!File.Exists(folder + "/scenarios.csv")) return;
            try
            {
                var document = ScenarioCsvService.Load(folder);
                var errors = ScenarioCsvService.Validate(document);
                if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
                if (ScenarioCsvService.ReadManifest(folder).fingerprint != ScenarioCsvService.Fingerprint(folder))
                    ScenarioCsvService.Apply(folder, document);
            }
            catch (Exception error) { throw new BuildFailedException("シナリオCSVを反映できません。\n" + error.Message); }
        }
    }
}
