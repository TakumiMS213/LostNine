using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public class KeywordProgressContinuityTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private Scene _scene;
    private Component _progress;
    private Component _clues;
    private readonly Dictionary<string, object> _singletons = new();
    private readonly Dictionary<FieldInfo, object> _events = new();
    private readonly List<Component> _subscribers = new();
    private readonly List<Object> _assets = new();

    private static Type GameType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static object Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, Flags).Invoke(target, args);
    private static object Field(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    private static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static object Phase(string name) => Enum.Parse(GameType("GamePhase"), name);
    private static void SetSingleton(string name, object value) => GameType(name).GetProperty("Instance").SetValue(null, value);

    private Component Create(string type)
    {
        var go = new GameObject(type, typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(go, _scene);
        go.SetActive(false);
        return go.AddComponent(GameType(type));
    }

    private ScriptableObject Asset(string type)
    {
        var asset = ScriptableObject.CreateInstance(GameType(type));
        asset.name = type;
        _assets.Add(asset);
        return asset;
    }

    private void Subscribe(Component component)
    {
        Call(component, "OnEnable");
        _subscribers.Add(component);
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
        foreach (string type in new[] { "ProgressManager", "ClueManager", "SceneTransition" })
            _singletons.Add(type, GameType(type).GetProperty("Instance").GetValue(null));
        _scene = EditorSceneManager.NewPreviewScene();
        _progress = Create("ProgressManager");
        SetSingleton("ProgressManager", _progress);
        Set(_progress, "_currentPhase", Phase("Dialogue"));
        _clues = Create("ClueManager");
        SetSingleton("ClueManager", _clues);
        Subscribe(_clues);
        // LOADの実初期化を検証し、シーンロードだけを既存の多重遷移ガードで抑止する。
        var transition = Create("SceneTransition");
        Set(transition, "_isTransitioning", true);
        SetSingleton("SceneTransition", transition);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var subscriber in _subscribers)
            if (subscriber != null) Call(subscriber, "OnDisable");
        _subscribers.Clear();
        EditorSceneManager.ClosePreviewScene(_scene);
        foreach (var asset in _assets) Object.DestroyImmediate(asset);
        _assets.Clear();
        foreach (var pair in _singletons) SetSingleton(pair.Key, pair.Value);
        _singletons.Clear();
        foreach (var pair in _events) pair.Key.SetValue(null, pair.Value);
        _events.Clear();
    }

    private void ExtractAll()
    {
        foreach (string id in new[] { "first", "second", "third" })
        {
            Call(_clues, "ProcessKeywordClick", id);
            Assert.That(Call(_progress, "AddKeyword", id), Is.True);
        }
    }

    [TestCase("advance")]
    [TestCase("direct")]
    [TestCase("flow")]
    public void EveryProgressUpdatePathPreservesKeywordsExtractedBeforeExtraction(string path)
    {
        ExtractAll();
        if (path == "flow")
        {
            var step = Asset("System_Script.Flow.ProgressStep");
            Set(step, "chapter", 1);
            Set(step, "phase", Phase("Extraction"));
            Call(step, "Execute", Create("System_Script.Flow.GameFlowDirector"));
        }
        else
        {
            string file = path == "advance" ? "AdvancePhase" : "Set_Ch1_Extraction";
            var action = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/ScriptableObjects/ProgressSystemDatabase/Actions/Phase/" + file + ".asset");
            Assert.That(action, Is.Not.Null);
            Call(Create("ScenarioSystem.Adapter.ProgressAdapter"), "HandleProgressUpdate", action);
        }
        Assert.That(Property(_progress, "CurrentPhase").ToString(), Is.EqualTo("Extraction"));
        Assert.That(Property(_progress, "CurrentKeywordProgress"), Is.EqualTo(3));
        Assert.That(Property(_progress, "AllKeywordsCollected"), Is.True);
        Assert.That(Call(_clues, "IsDiscovered", "first"), Is.True);
        Assert.That(Call(_clues, "ApplyKeywordColors", "<link=first>word</link>"), Does.Contain("#FFFF00"));
        Assert.That(Call(_progress, "AddKeyword", "first"), Is.False);
    }

    [Test]
    public void TuningFixationAndPresentationKeepCompletionButCannotStartTuningAgain()
    {
        ExtractAll();
        var drag = Create("DragToSceneItem");
        Assert.That(Call(drag, "IsKeywordsCollected"), Is.False, "A completed count must not skip the remaining Dialogue phase.");
        Call(_progress, "SetProgress", 1, Phase("Extraction"));
        Assert.That(Call(drag, "IsKeywordsCollected"), Is.True);
        foreach (string phase in new[] { "Tuning", "Fixation", "Presentation", "Epilogue" })
        {
            Call(_progress, "SetProgress", 1, Phase(phase));
            Assert.That(Property(_progress, "AllKeywordsCollected"), Is.True);
            Assert.That(Property(_progress, "CurrentKeywordProgress"), Is.EqualTo(3));
            Assert.That(Call(drag, "IsKeywordsCollected"), Is.False, phase + " must not permit another tuning drop.");
        }
    }

    [Test]
    public void AChapterChangeResetsCountsAndDiscoveryTogether()
    {
        ExtractAll();
        Call(_progress, "SetProgress", 2, Phase("Dialogue"));
        Assert.That(Property(_progress, "CurrentKeywordProgress"), Is.Zero);
        Assert.That(Call(_clues, "IsDiscovered", "first"), Is.False);
        Assert.That(Call(_clues, "ApplyKeywordColors", "<link=first>word</link>"), Is.EqualTo("<link=first>word</link>"));
        Assert.That(Call(_progress, "AddKeyword", "first"), Is.True);
    }

    private Component CreateCompletionDirector(int chapter, string completionPhase = "Tuning")
    {
        var director = Create("System_Script.Flow.GameFlowDirector");
        var sequence = Asset("System_Script.Flow.StorySequence");
        var step = Asset("System_Script.Flow.ProgressStep");
        Set(step, "chapter", chapter);
        Set(step, "phase", Phase(completionPhase));
        ((IList)Field(sequence, "steps")).Add(step);
        var mapping = Activator.CreateInstance(GameType("System_Script.Flow.SequenceOverride"));
        Set(mapping, "targetChapter", chapter);
        Set(mapping, "targetPhase", Phase("Extraction"));
        Set(mapping, "sequence", sequence);
        ((IList)Field(director, "overrideSequences")).Add(mapping);
        Subscribe(director);
        return director;
    }

    [Test]
    public void EarlyThresholdWaitsForExtractionAndDoesNotReenterTheProgressAction()
    {
        var director = CreateCompletionDirector(1);
        ExtractAll();
        Call(director, "LateUpdate");
        Assert.That(Property(_progress, "CurrentPhase").ToString(), Is.EqualTo("Dialogue"));
        Assert.That(Field(director, "_keywordCompletionChapter"), Is.EqualTo(-1));

        var adapter = Create("ScenarioSystem.Adapter.ProgressAdapter");
        Subscribe(adapter);
        var action = AssetDatabase.LoadAssetAtPath<Object>(
            "Assets/ScriptableObjects/ProgressSystemDatabase/Actions/Phase/Set_Ch1_Extraction.asset");
        var executor = Activator.CreateInstance(GameType("ScenarioSystem.Presenter.Executors.ProgressUpdateActionExecutor"));
        var state = Activator.CreateInstance(GameType("ScenarioSystem.Runtime.ScenarioRuntimeState"));
        var order = new List<string>();
        _progress.GetType().GetEvent("OnProgressChanged").AddEventHandler(_progress,
            new Action(() => order.Add(Property(_progress, "CurrentPhase").ToString())));
        Call(executor, "Execute", action, state, new Action(() => order.Add("action-completed")));
        Assert.That(order, Is.EqualTo(new[] { "Extraction", "action-completed" }),
            "The completion flow must wait until all progress listeners and the action completion have returned.");

        Call(director, "LateUpdate");
        Assert.That(order, Is.EqualTo(new[] { "Extraction", "action-completed", "Tuning" }));
        Assert.That(Property(_progress, "AllKeywordsCollected"), Is.True);
        Call(_progress, "SetProgress", 1, Phase("Extraction"));
        Call(director, "LateUpdate");
        Assert.That(Property(_progress, "CurrentPhase").ToString(), Is.EqualTo("Extraction"),
            "The chapter completion flow must not replay after already being handled.");
    }

    [TestCase(1)]
    [TestCase(2)]
    public void RestartingTheSameChapterAllowsItsCompletionFlowToRunAgain(int chapter)
    {
        Call(_progress, "SetProgress", chapter, Phase("Extraction"));
        var director = CreateCompletionDirector(chapter);
        ExtractAll();
        Call(director, "LateUpdate");
        Assert.That(Property(_progress, "CurrentPhase").ToString(), Is.EqualTo("Tuning"));
        Call(_progress, "StartFromChapter", chapter);
        Assert.That(Field(director, "_keywordCompletionChapter"), Is.EqualTo(-1));
        Assert.That(Property(_progress, "CurrentKeywordProgress"), Is.Zero);
        ExtractAll();
        Call(_progress, "SetProgress", chapter, Phase("Extraction"));
        Call(director, "LateUpdate");
        Assert.That(Property(_progress, "CurrentPhase").ToString(), Is.EqualTo("Tuning"));
    }

    [Test]
    public void ThresholdReachedDuringAnotherFlowIsRetainedUntilThatFlowFinishes()
    {
        Call(_progress, "SetProgress", 1, Phase("Extraction"));
        var director = CreateCompletionDirector(1);
        Set(director, "_isPlaying", true);
        ExtractAll();
        Call(director, "LateUpdate");
        Assert.That(Property(_progress, "CurrentPhase").ToString(), Is.EqualTo("Extraction"));
        Assert.That(Field(director, "_keywordCompletionPending"), Is.True);
        Call(director, "EndSequence");
        Call(director, "LateUpdate");
        Assert.That(Property(_progress, "CurrentPhase").ToString(), Is.EqualTo("Tuning"));
    }
}
