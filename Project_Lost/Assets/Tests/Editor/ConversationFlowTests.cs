using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ConversationFlowTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private Scene _scene;
    private readonly Dictionary<FieldInfo, object> _events = new();
    private object _previousFacade;
    private object _previousProgress;
    private readonly List<Component> _subscribed = new();

    private static Type GameType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static object Get(object target, string field) => target.GetType().GetField(field, Flags).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
    private static object Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, Flags | BindingFlags.DeclaredOnly).Invoke(target, args);
    private static object Property(object target, string property) => target.GetType().GetProperty(property).GetValue(target);
    private static void SetSingleton(string type, object instance) => GameType(type).GetProperty("Instance").SetValue(null, instance);
    private static void Raise(string method, params object[] args) =>
        GameType("ScenarioSystem.Events.ScenarioEventBus").GetMethod(method).Invoke(null, args);

    [SetUp]
    public void SetUp()
    {
        _scene = EditorSceneManager.NewPreviewScene();
        foreach (var field in GameType("ScenarioSystem.Events.ScenarioEventBus").GetFields(BindingFlags.NonPublic | BindingFlags.Static))
        {
            if (!typeof(Delegate).IsAssignableFrom(field.FieldType)) continue;
            _events[field] = field.GetValue(null);
            field.SetValue(null, null);
        }
        _previousFacade = GameType("ScenarioSystem.Adapter.MessageWindowFacade").GetProperty("Instance").GetValue(null);
        _previousProgress = GameType("ProgressManager").GetProperty("Instance").GetValue(null);
        SetSingleton("ScenarioSystem.Adapter.MessageWindowFacade", null);
        SetSingleton("ProgressManager", null);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var component in _subscribed)
            if (component != null) Call(component, "OnDisable");
        _subscribed.Clear();
        EditorSceneManager.ClosePreviewScene(_scene);
        SetSingleton("ScenarioSystem.Adapter.MessageWindowFacade", _previousFacade);
        SetSingleton("ProgressManager", _previousProgress);
        foreach (var pair in _events) pair.Key.SetValue(null, pair.Value);
        _events.Clear();
    }

    private GameObject NewObject(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(go, _scene);
        go.SetActive(false);
        return go;
    }

    private Component Create(string type) => NewObject(type).AddComponent(GameType(type));

    private (Component manager, Component animator, TMP_Text text) CreateManager()
    {
        var manager = Create("ComuStartandEndManager");
        foreach (var name in new[] { "comuStartUI", "comuEndUI", "desk", "messageWindowBackGround", "NamePlate", "NamePlateBackGround", "Memorizer", "LostNote", "ToggleEffect", "ObjectiveDisplay", "Portrait", "unclickableOverlay" })
            Set(manager, name, NewObject(name));
        var text = NewObject("DialogueText").AddComponent<TextMeshProUGUI>();
        text.fontSize = 47f;
        Set(manager, "messageWindow", text.gameObject);
        Set(manager, "fontSizeTarget", text);
        Set(manager, "fadeFrame", NewObject("FadeFrame").AddComponent<Image>());
        var animator = NewObject("Shape").AddComponent(GameType("MoveOnClickandReturn"));
        ((RectTransform)animator.transform).anchoredPosition = new Vector2(17f, -23f);
        Set(animator, "targetAnchoredPosition", new Vector2(350f, 0f));
        Call(animator, "Awake");
        Array shapes = Array.CreateInstance(animator.GetType(), 1);
        shapes.SetValue(animator, 0);
        Set(manager, "shapeAnimators", shapes);
        Call(manager, "Awake");
        return (manager, animator, text);
    }

    private static void CompleteTask(Component manager, string method)
    {
        object task = Call(manager, method, null, false);
        object awaiter = task.GetType().GetMethod("GetAwaiter").Invoke(task, null);
        awaiter.GetType().GetMethod("GetResult").Invoke(awaiter, null);
    }

    private (Component manager, Button button, TMP_Text label) CreateDialogueButtonManager()
    {
        var (manager, _, _) = CreateManager();
        var buttonObject = NewObject("DialogueStartButton");
        buttonObject.transform.SetParent(manager.transform, false);
        buttonObject.AddComponent<Image>();
        var button = buttonObject.AddComponent<Button>();
        var labelObject = NewObject("Label");
        labelObject.transform.SetParent(button.transform, false);
        var label = labelObject.AddComponent<TextMeshProUGUI>();
        Set(manager, "dialogueStartButton", button);
        Set(manager, "dialogueStartButtonLabel", label);
        button.onClick.AddListener(() => Call(manager, "ToggleComuFromButton"));
        _subscribed.Add(manager);
        return (manager, button, label);
    }

    [Test]
    public void ScenarioButtonVisibilitySurvivesLocksAndManagerReactivation()
    {
        var (manager, button, _) = CreateDialogueButtonManager();
        Call(manager, "SetDialogueStartButtonVisible", true);
        Assert.That(button.gameObject.activeSelf, Is.False, "An inactive controller cannot expose a clickable button.");
        manager.gameObject.SetActive(true);
        Call(manager, "OnEnable");
        Assert.That(button.isActiveAndEnabled && button.IsInteractable(), Is.True);

        Call(manager, "SetPortraitInteractable", false, true);
        Assert.That(button.gameObject.activeSelf, Is.False);
        Call(manager, "SetPortraitInteractable", true, true);
        Assert.That(button.isActiveAndEnabled && button.IsInteractable(), Is.True, "An interaction lock must not discard the scenario's display request.");

        manager.gameObject.SetActive(false);
        Call(manager, "OnDisable");
        Assert.That(button.gameObject.activeSelf, Is.False);
        manager.gameObject.SetActive(true);
        Call(manager, "OnEnable");
        Assert.That(button.isActiveAndEnabled && button.IsInteractable(), Is.True);

        Call(manager, "SetDialogueStartButtonVisible", false);
        Call(manager, "SetPortraitInteractable", false, true);
        Call(manager, "SetPortraitInteractable", true, true);
        CompleteTask(manager, "ComuStartTask");
        CompleteTask(manager, "ComuEndTask");
        Assert.That(button.gameObject.activeSelf, Is.False, "Finishing a lock or conversation must not override a scenario's hide request.");
    }

    [Test]
    public void DialogueButtonTracksConversationStateAndDisappearsDuringTransitions()
    {
        var (manager, button, label) = CreateDialogueButtonManager();
        manager.gameObject.SetActive(true);
        Call(manager, "SetDialogueStartButtonVisible", true);
        Assert.That(label.text, Is.EqualTo("対話開始"));
        CompleteTask(manager, "ComuStartTask");
        Assert.That(button.isActiveAndEnabled && button.IsInteractable(), Is.True);
        Assert.That(label.text, Is.EqualTo("対話終了"));

        var logic = Get(manager, "_logic");
        logic.GetType().GetProperty("IsAnimating").SetValue(logic, true);
        Call(manager, "SetDialogueStartButtonVisible", true);
        Assert.That(button.gameObject.activeSelf, Is.False);
        button.onClick.Invoke();
        Assert.That(Property(manager, "IsInCommunication"), Is.True, "A second click must not reverse an in-flight transition.");
        logic.GetType().GetProperty("IsAnimating").SetValue(logic, false);

        CompleteTask(manager, "ComuEndTask");
        Assert.That(button.isActiveAndEnabled && button.IsInteractable(), Is.True);
        Assert.That(label.text, Is.EqualTo("対話開始"));
    }

    [Test]
    public void HiddenDisabledAndLockedDialogueButtonsCannotStartConversation()
    {
        var (manager, button, _) = CreateDialogueButtonManager();
        manager.gameObject.SetActive(true);
        button.onClick.Invoke();
        Assert.That(Property(manager, "IsInCommunication"), Is.False);

        Call(manager, "SetDialogueStartButtonVisible", true);
        button.interactable = false;
        button.onClick.Invoke();
        Assert.That(Property(manager, "IsInCommunication"), Is.False);
        button.interactable = true;
        button.enabled = false;
        button.onClick.Invoke();
        Assert.That(Property(manager, "IsInCommunication"), Is.False);
        button.enabled = true;

        Call(manager, "SetPortraitInteractable", false, true);
        // A stale event or externally reactivated button still cannot bypass the scenario lock.
        button.gameObject.SetActive(true);
        button.interactable = true;
        button.onClick.Invoke();
        Assert.That(Property(manager, "IsInCommunication"), Is.False);

        Call(manager, "SetPortraitInteractable", true, true);
        ((Behaviour)manager).enabled = false;
        button.onClick.Invoke();
        Assert.That(Property(manager, "IsInCommunication"), Is.False);
    }

    [Test]
    public void DirectStartAndEndKeepStateShapeAndFontInSyncAcrossRepeatedConversations()
    {
        var (manager, animator, text) = CreateManager();
        Assert.That(Property(manager, "IsInCommunication"), Is.False);
        for (int i = 0; i < 3; i++)
        {
            CompleteTask(manager, "ComuStartTask");
            Assert.That(Property(manager, "IsInCommunication"), Is.True);
            Assert.That(Get(animator, "isMoved"), Is.True);
            Assert.That(text.fontSize, Is.EqualTo(63f));
            CompleteTask(manager, "ComuStartTask");
            Assert.That(Get(animator, "isMoved"), Is.True, "Reapplying start must not toggle back.");
            CompleteTask(manager, "ComuEndTask");
            Assert.That(Property(manager, "IsInCommunication"), Is.False);
            Assert.That(Get(animator, "isMoved"), Is.False);
            Assert.That(((RectTransform)animator.transform).anchoredPosition, Is.EqualTo(new Vector2(17f, -23f)));
            Assert.That(text.fontSize, Is.EqualTo(47f), "Returning restores the scene's configured font size.");
            Assert.That(text.gameObject.activeSelf, Is.True, "Instant end must have the same final UI as animated end.");
            Assert.That(((GameObject)Get(manager, "NamePlate")).activeSelf, Is.True);
            Assert.That(((Image)Get(manager, "fadeFrame")).gameObject.activeSelf, Is.False);
        }
    }

    [Test]
    public void TransitionDoesNotOverwriteTheScenarioPortraitLock()
    {
        var (manager, _, _) = CreateManager();
        Call(manager, "SetPortraitInteractable", false, true);
        CompleteTask(manager, "ComuStartTask");
        Assert.That(Property(Get(manager, "_logic"), "IsPortraitInteractable"), Is.False);
        Call(manager, "ToggleComuforPortrait", false);
        Assert.That(Property(manager, "IsInCommunication"), Is.True, "A locked portrait must not exit conversation.");
        Call(manager, "ToggleComuFromScenario", false);
        Assert.That(Property(manager, "IsInCommunication"), Is.False, "Scenario commands may still change state.");
    }

    [Test]
    public void InFlightTransitionRejectsCompetingDirectRequest()
    {
        var (manager, _, _) = CreateManager();
        CompleteTask(manager, "ComuStartTask");
        var logic = Get(manager, "_logic");
        logic.GetType().GetProperty("IsAnimating").SetValue(logic, true);
        CompleteTask(manager, "ComuEndTask");
        Assert.That(Property(manager, "IsInCommunication"), Is.True);
        Assert.That(Property(logic, "IsAnimating"), Is.True);
    }

    [Test]
    public void ManualScenarioIdIsHonoredEvenWhenProgressManagerExists()
    {
        var (manager, _, _) = CreateManager();
        var progress = Create("ProgressManager");
        SetSingleton("ProgressManager", progress);
        Set(manager, "useProgressBasedId", false);
        Set(manager, "startScenarioId", "custom_conversation");
        Assert.That(Call(manager, "GetStartScenarioId"), Is.EqualTo("custom_conversation"));
        Set(manager, "useProgressBasedId", true);
        Assert.That(Call(manager, "GetEndScenarioId"), Is.Null, "Unregistered chapter loop IDs must not be launched.");
    }

    [Test]
    public void FacadeDisablingUnsubscribesTypingAndReenableDoesNotDuplicateLogs()
    {
        var facade = Create("ScenarioSystem.Adapter.MessageWindowFacade");
        object line = Activator.CreateInstance(GameType("ScenarioSystem.Events.DialogueEventData"));
        Call(facade, "OnEnable");
        _subscribed.Add(facade);
        Raise("RaiseDialogueRequested", line);
        Assert.That(Property(facade, "IsTyping"), Is.True);
        Call(facade, "OnDisable");
        Raise("RaiseDialogueRequested", line);
        Assert.That(Property(facade, "IsTyping"), Is.False, "Disabled components must not receive static events.");
        Assert.That(((IList)Call(facade, "GetLog")).Count, Is.EqualTo(1));
        Call(facade, "OnEnable");
        Raise("RaiseDialogueRequested", line);
        Assert.That(((IList)Call(facade, "GetLog")).Count, Is.EqualTo(2));
        Raise("RaiseDialogueRequested", Call(line, "WithInstantDisplay"));
        Assert.That(Property(facade, "IsTyping"), Is.False);
        Assert.That(((IList)Call(facade, "GetLog")).Count, Is.EqualTo(2), "Returning from a keyword detail must not duplicate the original line in the log.");
        Raise("RaiseWindowVisibilityChanged", false);
        Assert.That(Property(facade, "IsTyping"), Is.False);
        Raise("RaiseTypingCompleted");
        Assert.That(Property(facade, "IsTyping"), Is.False);
    }

    [Test]
    public void BootstrapRegistersExecutorsBeforeAnySceneStartCallback()
    {
        var go = NewObject("ScenarioSystem");
        var presenter = go.AddComponent(GameType("ScenarioSystem.Presenter.ScenarioPresenter"));
        var bootstrap = go.AddComponent(GameType("ScenarioSystem.Runtime.ScenarioBootstrap"));
        Call(bootstrap, "Awake");
        var executors = (IDictionary)Get(presenter, "_executors");
        foreach (var name in new[] { "Dialogue", "Choice", "Overlay", "ComuToggle", "ComuToggleInstant", "DialogueStartButton", "ProgressScenario" })
            Assert.That(executors.Contains(name), Is.True, name + " must be registered in Awake.");
    }

    [TestCase("Ch1_Tuning", false)]
    [TestCase("Ch1_Tuning", true)]
    [TestCase("Ch1_Fixation", false)]
    [TestCase("Ch1_Fixation", true)]
    public void TutorialOverlaysAdvanceWithTheirOwnButtonAndReleaseTheFullscreenBlocker(string scenarioName, bool showMainWindow)
    {
        var window = Create("System_Script.MemorizeOverlayMessageWindow");
        Set(window, "playOnStart", false);
        Call(window, "Awake");
        window.gameObject.SetActive(true);
        var presenter = (Component)Get(window, "_presenter");
        var dialogueView = (Component)Get(window, "_dialogueView");
        foreach (var component in new[] { dialogueView, presenter, window })
        {
            Call(component, "OnEnable");
            _subscribed.Add(component);
        }
        var original = AssetDatabase.LoadMainAssetAtPath(
            "Assets/ScriptableObjects/ProgressSystemDatabase/ScenarioData/Ch1/" + scenarioName + ".asset");
        var scenario = UnityEngine.Object.Instantiate(original);
        try
        {
            Set(scenario, "showMainWindow", showMainWindow);
            var root = (GameObject)Get(window, "_windowRoot");
            var text = (TMP_Text)Get(window, "_messageText");
            var button = root.GetComponentInChildren<Button>(true);
            int completed = 0;
            text.maxVisibleCharacters = 2;
            Call(presenter, "StartScenario", scenario, new Action(() => completed++));
            int count = ((IList)Get(scenario, "actions")).Count;
            for (int i = 0; i < count; i++)
            {
                Assert.That(root.activeInHierarchy, Is.True);
                Assert.That(text.text, Is.Not.Empty);
                Assert.That(text.maxVisibleCharacters, Is.EqualTo(int.MaxValue));
                Assert.That(Property(Property(presenter, "State"), "IsWaitingForInput"), Is.True);
                button.onClick.Invoke();
            }
            Assert.That(Property(presenter, "IsPlaying"), Is.False);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(root.activeSelf, Is.False, "The transparent full-screen click blocker must disappear with the tutorial.");
            Assert.That(text.text, Is.Empty);
            button.onClick.Invoke();
            Assert.That(completed, Is.EqualTo(1), "Hidden tutorial buttons must not advance the game.");
        }
        finally { UnityEngine.Object.DestroyImmediate(scenario); }
    }

}
