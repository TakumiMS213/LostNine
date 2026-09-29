using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// ゲーム側はAssembly-CSharpのまま維持する。asmdefからの直接参照ができないため、
// テストだけreflectionでアクセスし、テストのためのランタイム構成変更を避ける。
public class KeywordColorTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private const string Source = "before <link=\"key\"><color=red><b>word</b></color></link> after";
    private const string Yellow = "before <link=\"key\"><color=#FFFF00><b>word</b></color></link> after";
    private Scene _scene;
    private Component _clues;
    private Component _progress;
    private object _previousClues;
    private object _previousProgress;
    private readonly Dictionary<FieldInfo, object> _events = new();
    private readonly List<Component> _subscribers = new();

    private static Type GameType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static object Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, InstanceFlags | BindingFlags.DeclaredOnly).Invoke(target, args);
    private static object StaticCall(string type, string name, params object[] args) =>
        GameType(type).GetMethod(name, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, InstanceFlags).SetValue(target, value);
    private static object Singleton(string name) => GameType(name).GetProperty("Instance").GetValue(null);
    private static void SetSingleton(string name, object value) => GameType(name).GetProperty("Instance").SetValue(null, value);

    private GameObject NewObject(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(go, _scene);
        go.SetActive(false);
        return go;
    }

    private Component Create(string type) => NewObject(type).AddComponent(GameType(type));
    private string Format(string text) => (string)Call(_clues, "ApplyKeywordColors", text);
    private void Subscribe(Component component)
    {
        Call(component, "OnEnable");
        _subscribers.Add(component);
    }

    [SetUp]
    public void SetUp()
    {
        _scene = EditorSceneManager.NewPreviewScene();
        foreach (var field in GameType("ScenarioSystem.Events.ScenarioEventBus").GetFields(BindingFlags.NonPublic | BindingFlags.Static))
        {
            if (!typeof(Delegate).IsAssignableFrom(field.FieldType)) continue;
            _events.Add(field, field.GetValue(null));
            field.SetValue(null, null);
        }
        _previousClues = Singleton("ClueManager");
        _previousProgress = Singleton("ProgressManager");
        _progress = Create("ProgressManager");
        SetSingleton("ProgressManager", _progress);
        Set(_progress, "_isMemorizerUnlocked", true);
        Set(_progress, "_isMemorizerActive", true);
        _clues = Create("ClueManager");
        SetSingleton("ClueManager", _clues);
        Subscribe(_clues);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var component in _subscribers)
            if (component != null) Call(component, "OnDisable");
        _subscribers.Clear();
        EditorSceneManager.ClosePreviewScene(_scene);
        SetSingleton("ClueManager", _previousClues);
        SetSingleton("ProgressManager", _previousProgress);
        foreach (var entry in _events) entry.Key.SetValue(null, entry.Value);
        _events.Clear();
    }

    [Test]
    public void DiscoveryAndClickRetainYellowForEveryRedisplay()
    {
        Assert.That(Format(Source), Is.EqualTo(Source));
        Call(_clues, "DiscoverKeyword", "key");
        Assert.That(Format(Source), Is.EqualTo(Yellow));
        Call(_clues, "ProcessKeywordClick", "key");
        Assert.That(Call(_clues, "IsClicked", "key"), Is.False, "Discovery must prevent a later click.");
        Assert.That(Format(Source), Is.EqualTo(Yellow));
        Assert.That(Format("other dialogue"), Is.EqualTo("other dialogue"));
        Assert.That(Format(Source), Is.EqualTo(Yellow));
    }

    [Test]
    public void ImmediatelyClickableKeywordsAlsoBecomeYellow()
    {
        Set(_clues, "clickableImmediately", true);
        Call(_clues, "ProcessKeywordClick", " key ");
        Assert.That(Format(Source), Is.EqualTo(Yellow));
        Assert.That(Call(_clues, "IsClicked", "key"), Is.True);
    }

    [TestCase(false, "key")]
    [TestCase(true, "key")]
    [TestCase(false, "dummy_key")]
    [TestCase(true, "dummy_key")]
    public void FirstClickDisablesInteractionUntilExplicitReset(bool immediate, string id)
    {
        Set(_clues, "clickableImmediately", immediate);
        var handler = Create("MessageWindowSystem.Core.KeywordHandler");
        Set(handler, "_isKeywordEnabled", true);
        Assert.That(Call(handler, "CanInteractWithKeyword", id), Is.True);
        Call(_clues, "ProcessKeywordClick", id);
        Assert.That(Call(handler, "CanInteractWithKeyword", " " + id + " "), Is.False);
        Call(_clues, "ProcessKeywordClick", id);
        Assert.That(Call(_clues, "IsClicked", id), Is.EqualTo(immediate));
        Assert.That(Call(handler, "CanInteractWithKeyword", "another"), Is.True);
        Call(_clues, "ResetKeywordStatus", id);
        Assert.That(Call(handler, "CanInteractWithKeyword", id), Is.True);
        Call(_clues, "ProcessKeywordClick", id);
        Call(_clues, "ResetForNewStage");
        Assert.That(Call(handler, "CanInteractWithKeyword", id), Is.True);
    }

    [Test]
    public void KeywordInteractionRequiresActiveMemorizerInEveryPhase()
    {
        var handler = Create("MessageWindowSystem.Core.KeywordHandler");
        Set(handler, "_isKeywordEnabled", true);

        Set(_progress, "_currentPhase", Enum.Parse(GameType("GamePhase"), "Dialogue"));
        Set(_progress, "_isMemorizerActive", false);
        Assert.That(Call(handler, "CanInteractWithKeyword", "key"), Is.False);

        Set(_progress, "_isMemorizerActive", true);
        Assert.That(Call(handler, "CanInteractWithKeyword", "key"), Is.True,
            "Memorizer must allow extraction outside the Extraction phase.");
    }

    [Test]
    public void TutorialCompletionUnlocksMemorizerAndLockingTurnsItOff()
    {
        Set(_progress, "_currentChapter", 1);
        Set(_progress, "_currentPhase", Enum.Parse(GameType("GamePhase"), "Prologue"));
        Set(_progress, "_isMemorizerUnlocked", false);
        Set(_progress, "_isMemorizerActive", false);

        Assert.That(Call(_progress, "ToggleMemorizer"), Is.False);
        Call(_progress, "AdvancePhase");
        Assert.That(_progress.GetType().GetProperty("IsMemorizerUnlocked").GetValue(_progress), Is.True);
        Assert.That(Call(_progress, "ToggleMemorizer"), Is.True);
        Assert.That(_progress.GetType().GetProperty("IsMemorizerActive").GetValue(_progress), Is.True);

        Call(_progress, "SetMemorizerUnlocked", false);
        Assert.That(_progress.GetType().GetProperty("IsMemorizerUnlocked").GetValue(_progress), Is.False);
        Assert.That(_progress.GetType().GetProperty("IsMemorizerActive").GetValue(_progress), Is.False);
    }

    [Test]
    public void RedisplayedDiscoveredLinkDoesNotStartChargeOrBlockNext()
    {
        var (view, text) = CreateView();
        var provider = Create("ScenarioSystem.Adapter.DialogueProviderAdapter");
        Set(provider, "dialogueText", text);
        Set(provider, "_isWindowActive", true);
        var handler = Create("MessageWindowSystem.Core.KeywordHandler");
        Set(handler, "_provider", provider);
        Set(handler, "_isKeywordEnabled", true);
        Call(_clues, "DiscoverKeyword", "key");
        text.text = Format(Source);
        text.maxVisibleCharacters = int.MaxValue;
        text.ForceMeshUpdate();
        Assert.That(text.textInfo.linkCount, Is.EqualTo(1));
        var character = text.textInfo.characterInfo[text.textInfo.linkInfo[0].linkTextfirstCharacterIndex];
        var point = RectTransformUtility.WorldToScreenPoint(null,
            text.transform.TransformPoint((character.bottomLeft + character.topRight) * .5f));
        Assert.That(TMP_TextUtilities.FindIntersectingLink(text, point, null), Is.Zero);
        Call(handler, "OnPointerDown", new PointerEventData(null) { position = point });
        Assert.That(handler.GetType().GetProperty("IsCharging").GetValue(handler), Is.False);
        Assert.That(Call(handler, "ConsumeBlockNext"), Is.False);
        Assert.That(text.text, Is.EqualTo(Yellow));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ChargeCompletionOnlyNotifiesForFirstDiscovery(bool alreadyDiscovered)
    {
        var (view, text) = CreateView();
        text.text = "<link=dummy_key>word</link>";
        text.ForceMeshUpdate();
        var handler = Create("MessageWindowSystem.Core.KeywordHandler");
        Set(handler, "_isKeywordEnabled", true);
        Set(handler, "chargeDuration", 0f);
        var clickArea = NewObject("ClickArea").AddComponent<Image>();
        clickArea.raycastTarget = false;
        Set(handler, "clickAreaGraphic", clickArea);
        int clicks = 0, scenarios = 0;
        handler.GetType().GetEvent("OnKeywordClicked").AddEventHandler(handler, new Action<string>(_ =>
        {
            clicks++;
            Assert.That(Call(handler, "CanInteractWithKeyword", "dummy_key"), Is.False);
        }));
        handler.GetType().GetEvent("OnKeywordScenarioRequested").AddEventHandler(handler,
            new Action<string, Action>((_, _) => scenarios++));
        if (alreadyDiscovered) Call(_clues, "DiscoverKeyword", "dummy_key");
        for (int i = 0; i < 2; i++)
        {
            var routine = (IEnumerator)Call(handler, "ChargeRoutine", text, 0, "dummy_key");
            Assert.That(routine.MoveNext(), Is.False);
        }
        Assert.That(clicks, Is.EqualTo(alreadyDiscovered ? 0 : 1));
        Assert.That(scenarios, Is.EqualTo(clicks));
        Assert.That(clickArea.raycastTarget, Is.True);
        Assert.That(Format("<link=dummy_key>word</link>"), Does.Contain("#FFFF00"));
    }

    [Test]
    public void DisablingKeywordInputDoesNotEraseDiscovery()
    {
        Subscribe(Create("ScenarioSystem.Adapter.ClueAdapter"));
        Call(_clues, "ProcessKeywordClick", "key");
        StaticCall("ScenarioSystem.Events.ScenarioEventBus", "RaiseKeywordStateChanged", false);
        StaticCall("ScenarioSystem.Events.ScenarioEventBus", "RaiseKeywordStateChanged", true);
        Assert.That(Format(Source), Is.EqualTo(Yellow));
    }

    [Test]
    public void ResetRestoresOriginalMarkupAndLeavesOtherKeywordsAlone()
    {
        Call(_clues, "SetKeywordColor", "key", "#FFFF00");
        Call(_clues, "SetKeywordColor", "other", "#FFFF00");
        var handler = Create("MessageWindowSystem.Core.KeywordHandler");
        Call(handler, "ResetKeywordState", "key");
        Assert.That(Format(Source), Is.EqualTo(Source));
        Assert.That(Format("<link=other>x</link>"), Does.Contain("#FFFF00"));
        Call(_clues, "ResetForNewStage");
        Assert.That(Format("<link=other>x</link>"), Is.EqualTo("<link=other>x</link>"));
    }

    [Test]
    public void ChapterChangeResetsColorsButPhaseChangeRetainsThem()
    {
        Call(_clues, "SetKeywordColor", "key", "#FFFF00");
        Call(_progress, "AdvancePhase");
        Assert.That(Format(Source), Is.EqualTo(Yellow));
        Call(_progress, "AdvanceChapter");
        Assert.That(Format(Source), Is.EqualTo(Source));
    }

    [TestCase("<link=\"a.b+\">a\nb</link>")]
    [TestCase("<link=a.b+>a\nb</link>")]
    [TestCase("<a href=\"a.b+\">a\nb</a>")]
    public void MultilineLinksAndLiteralIdsAreFormatted(string source)
    {
        Call(_clues, "SetKeywordColor", "a.b+", "#FFFF00");
        string expected = "<link=\"a.b+\"><color=#FFFF00>a\nb</color></link>";
        Assert.That(Format(source), Is.EqualTo(expected));
        Assert.That(Format(expected), Is.EqualTo(expected), "Color tags must not accumulate.");
        Assert.That(Format("<link=\"axb+\">other</link>"), Is.EqualTo("<link=\"axb+\">other</link>"));
    }

    [Test]
    public void ColorCanBeSetBeforeTheKeywordIsDisplayed()
    {
        var handler = Create("MessageWindowSystem.Core.KeywordHandler");
        Call(handler, "SetLinkColor", "key", "#FFFF00");
        Assert.That(Format(Source), Is.EqualTo(Yellow));
    }

    private (Component view, TMP_Text text) CreateView()
    {
        var canvas = NewObject("Canvas"); canvas.AddComponent<Canvas>(); canvas.SetActive(true);
        var textObject = NewObject("Text"); textObject.transform.SetParent(canvas.transform, false);
        var text = textObject.AddComponent<TextMeshProUGUI>(); text.font = TMP_Settings.defaultFontAsset;
        textObject.SetActive(true);
        var view = Create("ScenarioSystem.View.DialogueView");
        Set(view, "dialogueText", text); Set(view, "enableSkipMode", false);
        Subscribe(view);
        return (view, text);
    }

    private IEnumerator BeginTyping(Component view, string source)
    {
        Set(view, "_currentFullText", source);
        var routine = (IEnumerator)Call(view, "TypeText", source, .05f);
        Assert.That(routine.MoveNext(), Is.True);
        return routine;
    }

    [Test]
    public void ActiveViewRefreshesWithoutRevealingHiddenCharactersAndResetRestoresSource()
    {
        var (view, text) = CreateView();
        BeginTyping(view, Source);
        text.maxVisibleCharacters = 2;
        Call(_clues, "ProcessKeywordClick", "key");
        Assert.That(text.text, Is.EqualTo(Yellow));
        Assert.That(text.maxVisibleCharacters, Is.EqualTo(2));
        Call(_clues, "ResetKeywordStatus", "key");
        Assert.That(text.text, Is.EqualTo(Source));
        Assert.That(text.maxVisibleCharacters, Is.EqualTo(2));
    }

    [Test]
    public void RedisplayAndTypingSkipUseSavedColors()
    {
        var (view, text) = CreateView();
        Call(_clues, "SetKeywordColor", "key", "#FFFF00");
        BeginTyping(view, Source);
        Assert.That(text.text, Is.EqualTo(Yellow));
        Call(view, "OnUserInput");
        Assert.That(text.text, Is.EqualTo(Yellow));
        BeginTyping(view, "other dialogue");
        BeginTyping(view, Source);
        Assert.That(text.text, Is.EqualTo(Yellow));
        Assert.That(text.maxVisibleCharacters, Is.Zero);
    }

    [Test]
    public void ReenabledViewAndProviderUseSameColorsRegardlessOfSubscriptionOrder()
    {
        var (view, text) = CreateView();
        BeginTyping(view, Source);
        Call(view, "OnDisable");
        Call(_clues, "SetKeywordColor", "key", "#FFFF00");
        Assert.That(text.text, Is.EqualTo(Source));
        Call(view, "OnEnable");
        Assert.That(text.text, Is.EqualTo(Yellow));
        var provider = Create("ScenarioSystem.Adapter.DialogueProviderAdapter");
        Set(provider, "_currentText", Source);
        Assert.That(provider.GetType().GetProperty("CurrentText").GetValue(provider), Is.EqualTo(text.text));
    }
}
