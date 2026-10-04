using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ScenarioSystem.Tests.Editor
{
    public class ScenarioFlowCatalogTests
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private string _folder;
        private string _scenePath;
        private string _catalogPath;
        private Scene _previousActive;
        private Object[] _previousSelection;
        private Object _firstSequence;
        private Object _secondSequence;
        private bool _restoreUntitledScene;
        private bool _restoreUntitledDirty;

        private static Type FlowType(string name) => Type.GetType("System_Script.Flow." + name + ", Assembly-CSharp", true);
        private static Type BridgeType => Type.GetType("ScenarioSystem.Editor.Csv.ScenarioCsvFlowBridge, Assembly-CSharp-Editor", true);
        private static object Get(object target, string field) => target.GetType().GetField(field, Fields).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
        private static object Property(object target, string name) => target.GetType().GetProperty(name, Fields).GetValue(target);

        [SetUp]
        public void SetUp()
        {
            _restoreUntitledScene = false;
            _restoreUntitledDirty = false;
            _previousActive = SceneManager.GetActiveScene();
            _previousSelection = Selection.objects;
            _folder = "Assets/__ScenarioFlowTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", _folder.Substring("Assets/".Length));
            _scenePath = _folder + "/Flow.unity";
            _catalogPath = _folder + "/Catalog.asset";
            // EditMode TestRunner starts with an empty untitled scene. Unity refuses
            // additive creation until that scene has a path. Never save user content.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var open = SceneManager.GetSceneAt(i);
                if (!open.isLoaded || !string.IsNullOrEmpty(open.path)) continue;
                if (SceneManager.sceneCount != 1 || open.rootCount != 0)
                    Assert.Ignore("Scene migration tests require saved scenes or the TestRunner empty scene.");
                _restoreUntitledDirty = open.isDirty;
                Assert.That(EditorSceneManager.SaveScene(open, _folder + "/TestRunnerScene.unity"), Is.True);
                _restoreUntitledScene = true;
            }
            _firstSequence = CreateAsset("StorySequence", "First");
            _secondSequence = CreateAsset("StorySequence", "Second");
        }

        [TearDown]
        public void TearDown()
        {
            var scene = SceneManager.GetSceneByPath(_scenePath);
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            if (_restoreUntitledScene)
            {
                var restored = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (_restoreUntitledDirty) EditorSceneManager.MarkSceneDirty(restored);
            }
            else if (_previousActive.IsValid() && _previousActive.isLoaded) SceneManager.SetActiveScene(_previousActive);
            Selection.objects = _previousSelection;
            AssetDatabase.DeleteAsset(_folder);
        }

        private Object CreateAsset(string type, string name)
        {
            var asset = ScriptableObject.CreateInstance(FlowType(type));
            AssetDatabase.CreateAsset(asset, _folder + "/" + name + ".asset");
            return asset;
        }

        private void Populate(Object target, Object defaultSequence, Object matchedSequence)
        {
            using var serialized = new SerializedObject(target);
            serialized.FindProperty("startingSequence").objectReferenceValue = defaultSequence;
            var entries = serialized.FindProperty("overrideSequences");
            entries.arraySize = 1;
            var entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("targetChapter").intValue = 2;
            entry.FindPropertyRelative("targetPhase").intValue = 2;
            entry.FindPropertyRelative("sequence").objectReferenceValue = matchedSequence;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private Scene CreateSourceScene(bool leaveOpen = false)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var go = new GameObject("Flow");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.SetActive(false);
            var director = go.AddComponent(FlowType("GameFlowDirector"));
            Populate(director, _firstSequence, _secondSequence);
            Assert.That(EditorSceneManager.SaveScene(scene, _scenePath), Is.True);
            if (!leaveOpen) EditorSceneManager.CloseScene(scene, true);
            if (_previousActive.IsValid() && _previousActive.isLoaded) SceneManager.SetActiveScene(_previousActive);
            return scene;
        }

        private void Migrate() => BridgeType.GetMethod("MigrateScene").Invoke(null, new object[] { _scenePath, _catalogPath });

        private Object LoadCatalog() => AssetDatabase.LoadAssetAtPath(_catalogPath, FlowType("ScenarioFlowCatalog"));

        private static Object Lookup(Object catalog, int chapter, int phase)
        {
            var method = catalog.GetType().GetMethod("TryGetOverride");
            var args = new object[] { chapter, Enum.ToObject(method.GetParameters()[1].ParameterType, phase), null };
            bool found = (bool)method.Invoke(catalog, args);
            Assert.That(found, Is.EqualTo(args[2] != null));
            return (Object)args[2];
        }

        [Test]
        public void MigrationCopiesMappingsAndPreservesSceneSetup()
        {
            CreateSourceScene();
            int sceneCount = SceneManager.sceneCount;
            Migrate();
            var catalog = LoadCatalog();
            Assert.That(catalog, Is.Not.Null);
            Assert.That(Property(catalog, "StartingSequence"), Is.EqualTo(_firstSequence));
            Assert.That(Lookup(catalog, 2, 2), Is.EqualTo(_secondSequence));
            Assert.That(Lookup(catalog, 1, 2), Is.Null);
            Assert.That(Lookup(catalog, 2, 1), Is.Null);
            Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCount));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(_previousActive));
            var scene = EditorSceneManager.OpenScene(_scenePath, OpenSceneMode.Additive);
            var director = scene.GetRootGameObjects()[0].GetComponent(FlowType("GameFlowDirector"));
            Assert.That(Get(director, "flowCatalog"), Is.EqualTo(catalog));
            Assert.That(Get(director, "startingSequence"), Is.EqualTo(_firstSequence), "Legacy fallback must remain serialized.");
        }

        [Test]
        public void ExistingCatalogIsNotOverwrittenAndRepeatedMigrationKeepsItsGuid()
        {
            CreateSourceScene();
            var existing = ScriptableObject.CreateInstance(FlowType("ScenarioFlowCatalog"));
            Populate(existing, _secondSequence, _firstSequence);
            AssetDatabase.CreateAsset(existing, _catalogPath);
            string guid = AssetDatabase.AssetPathToGUID(_catalogPath);
            string snapshot = EditorJsonUtility.ToJson(existing);
            Migrate();
            Migrate();
            Assert.That(AssetDatabase.AssetPathToGUID(_catalogPath), Is.EqualTo(guid));
            Assert.That(EditorJsonUtility.ToJson(LoadCatalog()), Is.EqualTo(snapshot));
        }

        [Test]
        public void UnsavedSourceSceneIsRejectedBeforeCreatingCatalog()
        {
            var scene = CreateSourceScene(true);
            var go = scene.GetRootGameObjects()[0];
            go.name = "Unsaved author edit";
            EditorSceneManager.MarkSceneDirty(scene);
            var exception = Assert.Throws<TargetInvocationException>(Migrate);
            Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(go.name, Is.EqualTo("Unsaved author edit"));
            Assert.That(scene.isDirty, Is.True);
            Assert.That(LoadCatalog(), Is.Null);
            Assert.That(Get(go.GetComponent(FlowType("GameFlowDirector")), "flowCatalog"), Is.Null);
        }

        [Test]
        public void InvalidMappingDoesNotCreateCatalogOrLeaveOpenedScene()
        {
            var scene = CreateSourceScene(true);
            var director = scene.GetRootGameObjects()[0].GetComponent(FlowType("GameFlowDirector"));
            Populate(director, _firstSequence, null);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
            int sceneCount = SceneManager.sceneCount;
            var exception = Assert.Throws<TargetInvocationException>(Migrate);
            Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(LoadCatalog(), Is.Null);
            Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCount));
        }

        [Test]
        public void DirectorUsesCatalogDefaultOnlyWhenAssigned()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var catalog = ScriptableObject.CreateInstance(FlowType("ScenarioFlowCatalog"));
            try
            {
                var go = new GameObject("Flow");
                SceneManager.MoveGameObjectToScene(go, scene);
                go.SetActive(false);
                var director = go.AddComponent(FlowType("GameFlowDirector"));
                Populate(director, _firstSequence, _firstSequence);
                Populate(catalog, _secondSequence, _secondSequence);
                Assert.That(Property(director, "StartingSequence"), Is.EqualTo(_firstSequence));
                Set(director, "flowCatalog", catalog);
                Assert.That(Property(director, "StartingSequence"), Is.EqualTo(_secondSequence));
                Populate(catalog, null, _secondSequence);
                Assert.That(Property(director, "StartingSequence"), Is.Null, "An empty catalog default must not fall back to stale scene settings.");
                Set(director, "flowCatalog", null);
                Assert.That(Property(director, "StartingSequence"), Is.EqualTo(_firstSequence));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                Object.DestroyImmediate(catalog);
            }
        }
    }
}
