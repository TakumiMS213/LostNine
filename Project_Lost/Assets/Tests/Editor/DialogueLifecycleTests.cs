using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class DialogueLifecycleTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private Scene _scene;
    private readonly Dictionary<FieldInfo, object> _events = new();
    private readonly List<Component> _subscribers = new();
    private object _previousProgress;
    private object _previousClues;

    private static Type GameType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    private static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
    private static object Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, Flags | BindingFlags.DeclaredOnly).Invoke(target, args);
    private static void Raise(string name, params object[] args) =>
        GameType("ScenarioSystem.Events.ScenarioEventBus").GetMethod(name).Invoke(null, args);
    private static void SetSingleton(string type, object value) => GameType(type).GetProperty("Instance").SetValue(null, value);
    private static object Singleton(string type) => GameType(type).GetProperty("Instance").GetValue(null);
    private static void Listen(string name, Delegate callback) =>
        GameType("ScenarioSystem.Events.ScenarioEventBus").GetEvent(name).AddEventHandler(null, callback);

    private GameObject NewObject(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(go, _scene);
        go.SetActive(false);
        return go;
    }

    private Component Create(string type) => NewObject(type).AddComponent(GameType(type));

    private void Subscribe(Component component)
    {
        Call(component, "OnEnable");
        _subscribers.Add(component);
    }

    [SetUp]
    public void SetUp()
    {
        _scene = EditorSceneManager.NewPreviewScene();
        foreach (var field in GameType("ScenarioSystem.Events.ScenarioEventBus").GetFields(BindingFlags.Static | BindingFlags.NonPublic))
        {
            if (!typeof(Delegate).IsAssignableFrom(field.FieldType)) continue;
            _events.Add(field, field.GetValue(null));
            field.SetValue(null, null);
        }
        _previousProgress = Singleton("ProgressManager");
        _previousClues = Singleton("ClueManager");
        SetSingleton("ProgressManager", null);
        SetSingleton("ClueManager", null);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var component in _subscribers)
            if (component != null) Call(component, "OnDisable");
        _subscribers.Clear();
        EditorSceneManager.ClosePreviewScene(_scene);
        SetSingleton("ProgressManager", _previousProgress);
        SetSingleton("ClueManager", _previousClues);
        foreach (var entry in _events) entry.Key.SetValue(null, entry.Value);
        _events.Clear();
    }

    private object Choices(int count)
    {
        var entryType = GameType("ScenarioSystem.Model.Actions.ChoiceEntry");
        var choices = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(entryType));
        for (int i = 0; i < count; i++)
        {
            var entry = Activator.CreateInstance(entryType);
            Set(entry, "choiceText", "Choice " + i);
            choices.Add(entry);
        }
        return choices;
    }

    private (Component view, Button[] buttons) CreateChoices()
    {
        var canvas = NewObject("ChoiceCanvas");
        canvas.AddComponent<Canvas>();
        canvas.SetActive(true);
        var buttons = new Button[2];
        for (int i = 0; i < buttons.Length; i++)
        {
            var go = NewObject("Choice" + i);
            go.transform.SetParent(canvas.transform, false);
            go.AddComponent<Image>();
            buttons[i] = go.AddComponent<Button>();
        }
        var clickArea = NewObject("ClickArea");
        clickArea.transform.SetParent(canvas.transform, false);
        clickArea.SetActive(true);
        var view = Create("ScenarioSystem.View.ChoiceView");
        Set(view, "choiceButtons", buttons);
        Subscribe(view);
        return (view, buttons);
    }

    [Test]
    public void ChoicesStayAboveClickAreaAndAcceptOnlyOneSelection()
    {
        var (_, buttons) = CreateChoices();
        var selected = new List<int>();
        Listen("OnChoiceSelected", new Action<int>(selected.Add));
        Raise("RaiseChoicesRequested", Choices(2));
        Assert.That(buttons[0].gameObject.activeSelf, Is.True);
        Assert.That(buttons[1].gameObject.activeSelf, Is.True);
        Assert.That(buttons[0].transform.GetSiblingIndex(), Is.EqualTo(1));
        Assert.That(buttons[1].transform.GetSiblingIndex(), Is.EqualTo(2));
        buttons[1].onClick.Invoke();
        buttons[0].onClick.Invoke();
        buttons[1].onClick.Invoke();
        Assert.That(selected, Is.EqualTo(new[] { 1 }));
        Assert.That(buttons[0].gameObject.activeSelf, Is.False);
        Assert.That(buttons[1].gameObject.activeSelf, Is.False);
    }

    [TestCase("RaiseScenarioStarted")]
    [TestCase("RaiseScenarioEnded")]
    public void ScenarioBoundaryRemovesOldChoicesAndRejectsStaleClicks(string eventName)
    {
        var (_, buttons) = CreateChoices();
        int selected = 0;
        Listen("OnChoiceSelected", new Action<int>(_ => selected++));
        Raise("RaiseChoicesRequested", Choices(2));
        Raise(eventName, new object[] { null });
        buttons[0].onClick.Invoke();
        Assert.That(selected, Is.Zero);
        Assert.That(buttons[0].gameObject.activeSelf, Is.False);
        Assert.That(buttons[1].gameObject.activeSelf, Is.False);
    }

    [Test]
    public void DisablingChoicesToleratesButtonsDestroyedEarlierInSceneUnload()
    {
        var (view, buttons) = CreateChoices();
        Raise("RaiseChoicesRequested", Choices(2));
        UnityEngine.Object.DestroyImmediate(buttons[0].gameObject);
        Assert.DoesNotThrow(() => Call(view, "OnDisable"));
        Assert.That(buttons[1].gameObject.activeSelf, Is.False);
    }

    [Test]
    public void HiddenDialogueCannotAdvanceOrEmitOldTypingCompletion()
    {
        var view = Create("ScenarioSystem.View.DialogueView");
        var provider = Create("ScenarioSystem.Adapter.DialogueProviderAdapter");
        Subscribe(view);
        Subscribe(provider);
        Set(view, "_isTyping", true);
        Set(provider, "_isTyping", true);
        Set(provider, "_isWindowActive", true);
        int advanced = 0, completed = 0;
        Listen("OnAdvanceRequested", new Action(() => advanced++));
        Listen("OnTypingCompleted", new Action(() => completed++));
        Raise("RaiseWindowVisibilityChanged", false);
        Call(view, "OnUserInput");
        Assert.That(Get(view, "_isTyping"), Is.False);
        Assert.That(Get(provider, "_isTyping"), Is.False);
        Assert.That(Get(provider, "_isWindowActive"), Is.False);
        Assert.That(advanced, Is.Zero);
        Assert.That(completed, Is.Zero);
    }

    [TestCase("RaiseScenarioStarted")]
    [TestCase("RaiseScenarioEnded")]
    public void ScenarioBoundaryCancelsTypingWithoutCompletingAnotherScenario(string eventName)
    {
        var view = Create("ScenarioSystem.View.DialogueView");
        var provider = Create("ScenarioSystem.Adapter.DialogueProviderAdapter");
        Subscribe(view);
        Subscribe(provider);
        Set(view, "_isTyping", true);
        Set(provider, "_isTyping", true);
        int completed = 0;
        Listen("OnTypingCompleted", new Action(() => completed++));
        Raise(eventName, new object[] { null });
        Assert.That(Get(view, "_isTyping"), Is.False);
        Assert.That(Get(provider, "_isTyping"), Is.False);
        Assert.That(completed, Is.Zero);
    }

    [Test]
    public void ChargingAndChoicesBlockDialogueAdvance()
    {
        var view = Create("ScenarioSystem.View.DialogueView");
        var handler = Create("MessageWindowSystem.Core.KeywordHandler");
        Set(view, "keywordHandler", handler);
        Subscribe(view);
        int advanced = 0;
        Listen("OnAdvanceRequested", new Action(() => advanced++));
        Set(handler, "_isCharging", true);
        Call(view, "OnUserInput");
        Call(view, "OnUserInput");
        Assert.That(advanced, Is.Zero);
        Set(handler, "_isCharging", false);
        Set(handler, "_shouldBlockNext", true);
        Call(view, "OnUserInput");
        Assert.That(advanced, Is.Zero);
        Call(view, "OnUserInput");
        Assert.That(advanced, Is.EqualTo(1));
        Raise("RaiseChoicesRequested", Choices(2));
        Call(view, "OnUserInput");
        Assert.That(advanced, Is.EqualTo(1));
    }

    private (Component handler, Component provider, TMP_Text text) CreateKeywordText()
    {
        var canvas = NewObject("Canvas");
        canvas.AddComponent<Canvas>();
        canvas.SetActive(true);
        var go = NewObject("Text");
        go.transform.SetParent(canvas.transform, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.rectTransform.sizeDelta = new Vector2(1000, 300);
        go.SetActive(true);
        text.text = "<link=key>word</link>";
        text.ForceMeshUpdate();
        var provider = Create("ScenarioSystem.Adapter.DialogueProviderAdapter");
        Set(provider, "dialogueText", text);
        Set(provider, "_isWindowActive", true);
        var handler = Create("MessageWindowSystem.Core.KeywordHandler");
        Set(handler, "_provider", provider);
        Set(handler, "_isKeywordEnabled", true);
        Subscribe(handler);
        var progress = Create("ProgressManager");
        Set(progress, "_isMemorizerActive", true);
        SetSingleton("ProgressManager", progress);
        return (handler, provider, text);
    }

    [TestCase("RaiseScenarioStarted")]
    [TestCase("RaiseScenarioEnded")]
    public void ScenarioBoundaryCancelsInProgressKeywordCharge(string eventName)
    {
        var (handler, _, text) = CreateKeywordText();
        Set(handler, "_isCharging", true);
        Set(handler, "_chargingLinkID", "key");
        Set(handler, "_chargingText", text);
        Set(handler, "_chargingTextSource", text.text);
        Raise(eventName, new object[] { null });
        Assert.That(Get(handler, "_isCharging"), Is.False);
        Assert.That(Get(handler, "_chargingLinkID"), Is.Null);
    }

    [Test]
    public void ReplacingTextDuringChargeCannotExtractTheOldKeyword()
    {
        var (handler, _, text) = CreateKeywordText();
        Set(handler, "_isCharging", true);
        Set(handler, "_chargingLinkID", "key");
        Set(handler, "_chargingText", text);
        Set(handler, "_chargingTextSource", text.text);
        Assert.That(Call(handler, "CanContinueCharge"), Is.True);
        text.text = "<link=another>replacement</link>";
        Call(handler, "Update");
        Assert.That(Get(handler, "_isCharging"), Is.False);
        Assert.That(Get(handler, "_chargingLinkID"), Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DisablingKeywordChargeToleratesDestroyedTextAndProvider(bool destroyProvider)
    {
        var (handler, provider, text) = CreateKeywordText();
        Set(handler, "_isCharging", true);
        Set(handler, "_chargingLinkID", "key");
        Set(handler, "_chargingText", text);
        UnityEngine.Object.DestroyImmediate(text.gameObject);
        if (destroyProvider) UnityEngine.Object.DestroyImmediate(provider.gameObject);
        Assert.DoesNotThrow(() => Call(handler, "OnDisable"));
        Assert.That(Get(handler, "_isCharging"), Is.False);
        Assert.That(Get(handler, "_chargingLinkID"), Is.Null);
    }

    [Test]
    public void KeywordHitTestingWorksWithoutCustomCursorAssets()
    {
        var (handler, _, text) = CreateKeywordText();
        var graphic = NewObject("ClickArea").AddComponent<Image>();
        Set(handler, "clickAreaGraphic", graphic);
        var character = text.textInfo.characterInfo[0];
        var position = RectTransformUtility.WorldToScreenPoint(null,
            text.transform.TransformPoint((character.bottomLeft + character.topRight) * .5f));
        var previousMouse = Mouse.current;
        var mouse = InputSystem.AddDevice<Mouse>();
        try
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            InputSystem.Update();
            Call(handler, "UpdateCursorHover");
            Assert.That(graphic.raycastTarget, Is.False, "The full-screen advance target must not cover the keyword.");
            Call(handler, "OnDisable");
            Assert.That(graphic.raycastTarget, Is.True);
        }
        finally
        {
            InputSystem.RemoveDevice(mouse);
            if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
        }
    }

    [Test]
    public void BackgroundOrPausedWindowCannotRestartHeldSkipInput()
    {
        var view = Create("ScenarioSystem.View.DialogueView");
        Set(view, "skipKey", Key.A);
        var previousKeyboard = Keyboard.current;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
            InputSystem.Update();
            Assert.That(Call(view, "IsSkipHeld"), Is.True);
            Call(view, "OnApplicationFocus", false);
            Call(view, "Update");
            Assert.That(Call(view, "IsSkipHeld"), Is.False);
            Assert.That(Get(view, "_wasSkipHeld"), Is.False);
            Call(view, "OnApplicationFocus", true);
            Call(view, "OnApplicationPause", true);
            Call(view, "Update");
            Assert.That(Call(view, "IsSkipHeld"), Is.False);
            Assert.That(Get(view, "_wasSkipHeld"), Is.False);
            Call(view, "OnApplicationPause", false);
            Assert.That(Call(view, "IsSkipHeld"), Is.True);
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
        }
    }

    [Test]
    public void RepeatedCaretVisibilityDoesNotAccumulateAnimatedOffsets()
    {
        var caret = Create("ScenarioSystem.View.MessageWindowCaretIndicator");
        var rect = (RectTransform)caret.transform;
        var origin = new Vector2(-44, 34);
        rect.anchoredPosition = origin;
        Call(caret, "Awake");
        for (int i = 0; i < 50; i++)
        {
            Call(caret, "OnEnable");
            rect.anchoredPosition += new Vector2(0, 7);
            Call(caret, "OnDisable");
        }
        Assert.That(rect.anchoredPosition, Is.EqualTo(origin));
    }

    [Test]
    public void LostNoteDoesNotWarnBeforeOtherObjectsAwakeHasInitializedTheManager()
    {
        const string managerType = "ScenarioSystem.Runtime.LostNoteManager";
        var previous = Singleton(managerType);
        try
        {
            SetSingleton(managerType, null);
            var view = Create("ScenarioSystem.View.LostNoteView");
            Set(view, "characterImage", NewObject("Character").AddComponent<Image>());
            Set(view, "characterNameText", NewObject("CharacterName").AddComponent<TextMeshProUGUI>());
            Set(view, "characterDescriptionText", NewObject("CharacterDescription").AddComponent<TextMeshProUGUI>());
            Subscribe(view);
            Assert.That(Get(view, "_managerWarningLogged"), Is.False);
            LogAssert.NoUnexpectedReceived();

            var manager = Create(managerType);
            SetSingleton(managerType, manager);
            Call(view, "Start");
            Assert.That(Get(view, "_subscribedManager"), Is.SameAs(manager));
            Assert.That(Get(view, "_managerWarningLogged"), Is.False);
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            SetSingleton(managerType, previous);
        }
    }

    [Test]
    public void LostNoteStillWarnsOnceWhenTheManagerIsMissingAfterInitialization()
    {
        const string managerType = "ScenarioSystem.Runtime.LostNoteManager";
        var previous = Singleton(managerType);
        try
        {
            SetSingleton(managerType, null);
            var view = Create("ScenarioSystem.View.LostNoteView");
            Subscribe(view);
            LogAssert.NoUnexpectedReceived();
            LogAssert.Expect(LogType.Warning, "[LostNoteView] LostNoteManager is not found.");
            Call(view, "Start");
            Call(view, "Start");
            Call(view, "Update");
            Assert.That(Get(view, "_managerWarningLogged"), Is.True);
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            SetSingleton(managerType, previous);
        }
    }
}
