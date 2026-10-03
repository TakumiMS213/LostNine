using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ProgressRestartTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly Dictionary<FieldInfo, object> _events = new();
    private Scene _scene;
    private Component _progress;
    private object _previousProgress;
    private object _previousTransition;

    private static Type GameType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static object Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, Flags).Invoke(target, args);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
    private static object Field(object target, string field) => target.GetType().GetField(field, Flags).GetValue(target);
    private static object Get(object target, string property) => target.GetType().GetProperty(property).GetValue(target);
    private static object Singleton(string name) => GameType(name).GetProperty("Instance").GetValue(null);
    private static void SetSingleton(string name, object instance) => GameType(name).GetProperty("Instance").SetValue(null, instance);

    private Component CreateComponent(string name)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, _scene);
        go.SetActive(false);
        return go.AddComponent(GameType(name));
    }

    [SetUp]
    public void SetUp()
    {
        foreach (var field in GameType("ScenarioSystem.Events.ScenarioEventBus").GetFields(BindingFlags.Static | BindingFlags.NonPublic))
        {
            if (!typeof(Delegate).IsAssignableFrom(field.FieldType)) continue;
            _events.Add(field, field.GetValue(null));
            field.SetValue(null, null);
        }
        _previousProgress = Singleton("ProgressManager");
        _previousTransition = Singleton("SceneTransition");
        _scene = EditorSceneManager.NewPreviewScene();
        _progress = CreateComponent("ProgressManager");
        SetSingleton("ProgressManager", _progress);
        Set(_progress, "_maxChapter", 2);
        Set(_progress, "_keywordThreshold", 2);

        // LOADの初期化処理をそのまま呼び、シーンロードだけを既存の多重遷移ガードで止める。
        var transition = CreateComponent("SceneTransition");
        Set(transition, "_isTransitioning", true);
        SetSingleton("SceneTransition", transition);
    }

    [TearDown]
    public void TearDown()
    {
        EditorSceneManager.ClosePreviewScene(_scene);
        SetSingleton("ProgressManager", _previousProgress);
        SetSingleton("SceneTransition", _previousTransition);
        foreach (var pair in _events) pair.Key.SetValue(null, pair.Value);
        _events.Clear();
    }

    [TestCase(1)]
    [TestCase(2)]
    public void RestartingTheSameChapterResetsKeywordsPendingScenariosAndMemorizer(int chapter)
    {
        Set(_progress, "_currentChapter", chapter);
        Set(_progress, "_currentPhase", Enum.Parse(GameType("GamePhase"), "Presentation"));
        Set(_progress, "_isMemorizerUnlocked", true);
        Set(_progress, "_isMemorizerActive", true);
        Set(_progress, "_pendingStoryScenarioId", "old_story");
        Set(_progress, "_pendingMainScenarioId", "old_main");
        int thresholdReached = 0;
        _progress.GetType().GetEvent("OnKeywordThresholdReached").AddEventHandler(_progress, new Action(() => thresholdReached++));
        Assert.That(Call(_progress, "AddKeyword", "first"), Is.True);
        Assert.That(Call(_progress, "AddKeyword", "second"), Is.True);
        Assert.That(Get(_progress, "AllKeywordsCollected"), Is.True);
        Assert.That(thresholdReached, Is.EqualTo(1));

        int progressNotifications = 0;
        _progress.GetType().GetEvent("OnProgressChanged").AddEventHandler(_progress, new Action(() =>
        {
            progressNotifications++;
            Assert.That(Get(_progress, "CurrentKeywordProgress"), Is.Zero, "Subscribers must observe the reset state.");
            Assert.That(Get(_progress, "IsMemorizerActive"), Is.False);
        }));
        int activeScene = SceneManager.GetActiveScene().handle;
        Call(_progress, "StartFromChapter", chapter);

        Assert.That(Get(_progress, "CurrentChapter"), Is.EqualTo(chapter));
        Assert.That(Get(_progress, "CurrentPhase").ToString(), Is.EqualTo("Prologue"));
        Assert.That(Get(_progress, "CurrentKeywordProgress"), Is.Zero);
        Assert.That(Get(_progress, "AllKeywordsCollected"), Is.False);
        Assert.That(Get(_progress, "IsMemorizerUnlocked"), Is.EqualTo(chapter > 1));
        Assert.That(Get(_progress, "IsMemorizerActive"), Is.False);
        Assert.That(Field(_progress, "_pendingStoryScenarioId"), Is.Null);
        Assert.That(Field(_progress, "_pendingMainScenarioId"), Is.Null);
        Assert.That(progressNotifications, Is.EqualTo(1));
        Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(activeScene));

        Assert.That(Call(_progress, "AddKeyword", "first"), Is.True, "Previously extracted IDs must be collectible after LOAD.");
        Assert.That(Call(_progress, "AddKeyword", "first"), Is.False, "Duplicates within the restarted playthrough still count once.");
        Assert.That(Get(_progress, "CurrentKeywordProgress"), Is.EqualTo(1));
        Assert.That(thresholdReached, Is.EqualTo(1));
        Assert.That(Call(_progress, "AddKeyword", "second"), Is.True);
        Assert.That(Get(_progress, "AllKeywordsCollected"), Is.True);
        Assert.That(thresholdReached, Is.EqualTo(2), "Restart must allow the extraction completion event to fire again.");
    }
}
