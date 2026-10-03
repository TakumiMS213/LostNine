using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Assembly-CSharp の構成を変えず、公開APIと実Executorのイベント連携を検証する。
public class ScenarioPresenterTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly List<Object> _assets = new();
    private readonly Dictionary<FieldInfo, object> _events = new();
    private Scene _scene;
    private Component _presenter;
    private object State => _presenter.GetType().GetProperty("State").GetValue(_presenter);
    private static Type GameType(string name) => Type.GetType("ScenarioSystem." + name + ", Assembly-CSharp", true);
    private static object Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, Flags).Invoke(target, args);
    private static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static void SetField(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    private static void Raise(string name, params object[] args) =>
        GameType("Events.ScenarioEventBus").GetMethod("Raise" + name).Invoke(null, args);

    [SetUp]
    public void SetUp()
    {
        foreach (var field in GameType("Events.ScenarioEventBus").GetFields(BindingFlags.Static | BindingFlags.NonPublic))
        {
            if (!typeof(Delegate).IsAssignableFrom(field.FieldType)) continue;
            _events.Add(field, field.GetValue(null));
            field.SetValue(null, null);
        }
        _scene = EditorSceneManager.NewPreviewScene();
        var go = new GameObject("ScenarioPresenterTest");
        SceneManager.MoveGameObjectToScene(go, _scene);
        go.SetActive(false);
        _presenter = go.AddComponent(GameType("Presenter.ScenarioPresenter"));
        Call(_presenter, "OnEnable");
        Register("DialogueActionExecutor");
        Register("ChoiceActionExecutor");
    }

    [TearDown]
    public void TearDown()
    {
        Call(_presenter, "OnDisable");
        EditorSceneManager.ClosePreviewScene(_scene);
        foreach (var asset in _assets) Object.DestroyImmediate(asset);
        _assets.Clear();
        foreach (var pair in _events) pair.Key.SetValue(null, pair.Value);
        _events.Clear();
    }

    private ScriptableObject Asset(string type)
    {
        var asset = ScriptableObject.CreateInstance(GameType("Model." + type));
        asset.name = type;
        _assets.Add(asset);
        return asset;
    }

    private ScriptableObject Scenario(params ScriptableObject[] actions)
    {
        var scenario = Asset("ScenarioData");
        var list = (IList)scenario.GetType().GetField("actions").GetValue(scenario);
        foreach (var action in actions) list.Add(action);
        return scenario;
    }

    private ScriptableObject Dialogue(params string[] lines)
    {
        var dialogue = Asset("Actions.DialogueAction");
        var entries = (IList)dialogue.GetType().GetField("entries").GetValue(dialogue);
        foreach (string line in lines)
        {
            var entry = Activator.CreateInstance(GameType("Model.Actions.DialogueEntry"));
            SetField(entry, "text", line);
            entries.Add(entry);
        }
        return dialogue;
    }

    private ScriptableObject Choice(params ScriptableObject[] destinations)
    {
        var choice = Asset("Actions.ChoiceAction");
        var entries = (IList)choice.GetType().GetField("choices").GetValue(choice);
        foreach (var destination in destinations)
        {
            var entry = Activator.CreateInstance(GameType("Model.Actions.ChoiceEntry"));
            SetField(entry, "choiceText", "Choice");
            SetField(entry, "nextScenario", destination);
            entries.Add(entry);
        }
        return choice;
    }

    private void Register(string name, params object[] args) =>
        Call(_presenter, "RegisterExecutor", Activator.CreateInstance(GameType("Presenter.Executors." + name), args));

    private void RegisterDeferred(string actionType, Action<object, object, Action> execute)
    {
        var factory = typeof(DispatchProxy).GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
        var proxy = (ScenarioExecutorProxy)factory.MakeGenericMethod(
            GameType("Presenter.IActionExecutor"), typeof(ScenarioExecutorProxy)).Invoke(null, null);
        proxy.ActionType = actionType;
        proxy.ExecuteAction = execute;
        Call(_presenter, "RegisterExecutor", proxy);
    }

    private void Listen(string name, Action<object> listener)
    {
        var eventInfo = GameType("Events.ScenarioEventBus").GetEvent("On" + name);
        eventInfo.AddEventHandler(null, Delegate.CreateDelegate(eventInfo.EventHandlerType, listener.Target, listener.Method));
    }

    private void Start(ScriptableObject scenario, Action complete = null) => Call(_presenter, "StartScenario", scenario, complete);
    private void NextLine()
    {
        Raise("TypingCompleted");
        Raise("AdvanceRequested");
    }

    [Test]
    public void ReplacingScenarioRejectsOldAndDuplicateAsyncCompletions()
    {
        var completions = new List<Action>();
        RegisterDeferred("Wait", (_, _, complete) => completions.Add(complete));
        var first = Scenario(Asset("Actions.WaitAction"), Dialogue("old"));
        var second = Scenario(Asset("Actions.WaitAction"), Dialogue("new"));
        int oldFinished = 0, newFinished = 0;
        Start(first, () => oldFinished++);
        Start(second, () => newFinished++);

        completions[0]();
        Assert.That(Get(State, "CurrentScenario"), Is.SameAs(second));
        Assert.That(Get(State, "CurrentActionIndex"), Is.EqualTo(0));
        completions[1]();
        completions[1]();
        Assert.That(Get(State, "CurrentActionIndex"), Is.EqualTo(1));
        Assert.That(Get(State, "IsTyping"), Is.True);
        NextLine();
        completions[0]();
        completions[1]();
        Assert.That(oldFinished, Is.Zero);
        Assert.That(newFinished, Is.EqualTo(1));
        Assert.That(Get(State, "IsPlaying"), Is.False);
    }

    [Test]
    public void LateTypingCompletionCannotMakeAnAsyncActionSkippable()
    {
        Action complete = null;
        RegisterDeferred("Wait", (_, _, callback) => complete = callback);
        Start(Scenario(Asset("Actions.WaitAction"), Dialogue("after wait")));
        Raise("TypingCompleted");
        Raise("AdvanceRequested");
        Assert.That(Get(State, "CurrentActionIndex"), Is.Zero);
        Assert.That(Get(State, "IsWaitingForInput"), Is.False);
        complete();
        Assert.That(Get(State, "CurrentActionIndex"), Is.EqualTo(1));
    }

    [Test]
    public void DialogueSubstepsAndChoiceBranchesPreserveCompletionExactlyOnce()
    {
        var branch = Scenario(Dialogue("branch first", "branch last"));
        var original = Scenario(Dialogue("first", "second"), Choice(branch));
        int finished = 0;
        Start(original, () => finished++);
        Raise("AdvanceRequested");
        Assert.That(Get(State, "CurrentSubActionIndex"), Is.Zero, "Typing must not be advanced.");
        NextLine();
        Assert.That(Get(State, "CurrentSubActionIndex"), Is.EqualTo(1));
        NextLine();
        Assert.That(Get(State, "IsWaitingForChoice"), Is.True);
        Raise("TypingCompleted");
        Raise("ChoiceSelected", -1);
        Raise("ChoiceSelected", 9);
        Raise("AdvanceRequested");
        Assert.That(Get(State, "IsWaitingForChoice"), Is.True);
        Assert.That(Get(State, "IsWaitingForInput"), Is.False);
        Raise("ChoiceSelected", 0);
        Assert.That(Get(State, "CurrentScenario"), Is.SameAs(branch));
        NextLine();
        NextLine();
        Raise("ChoiceSelected", 0);
        Assert.That(finished, Is.EqualTo(1));
    }

    [Test]
    public void AChoiceWithoutDestinationEndsTheCurrentScenario()
    {
        int finished = 0;
        Start(Scenario(Choice(new ScriptableObject[] { null }), Dialogue("must not run")), () => finished++);
        Raise("ChoiceSelected", 0);
        Assert.That(finished, Is.EqualTo(1));
        Assert.That(Get(State, "IsPlaying"), Is.False);
    }

    [Test]
    public void StartingFromAnEndedEventDoesNotEraseTheNewScenario()
    {
        var next = Scenario(Dialogue("new scenario"));
        int finished = 0;
        Listen("ScenarioEnded", _ => Start(next));
        Start(Scenario(Dialogue("old scenario")), () => finished++);
        NextLine();
        Assert.That(Get(State, "CurrentScenario"), Is.SameAs(next));
        Assert.That(Get(State, "IsPlaying"), Is.True);
        Assert.That(Get(State, "IsTyping"), Is.True);
        Assert.That(finished, Is.EqualTo(1));
    }

    [Test]
    public void ReentrantStartExecutesTheReplacementOnlyOnce()
    {
        int executions = 0;
        RegisterDeferred("Wait", (_, _, _) => executions++);
        var replacement = Scenario(Asset("Actions.WaitAction"));
        var original = Scenario(Dialogue("must not be displayed"));
        Listen("ScenarioStarted", started => { if (ReferenceEquals(started, original)) Start(replacement); });
        Start(original);
        Assert.That(Get(State, "CurrentScenario"), Is.SameAs(replacement));
        Assert.That(executions, Is.EqualTo(1));
    }

    [Test]
    public void DisablingPresenterInvalidatesOutstandingCompletions()
    {
        Action complete = null;
        int finished = 0;
        RegisterDeferred("Wait", (_, _, callback) => complete = callback);
        Start(Scenario(Asset("Actions.WaitAction")), () => finished++);
        Call(_presenter, "OnDisable");
        complete();
        Assert.That(Get(State, "IsPlaying"), Is.False);
        Assert.That(finished, Is.Zero);
    }

    [Test]
    public void StoppingScenarioClosesItWithoutCompletingTheStoryStep()
    {
        int ended = 0, finished = 0;
        Listen("ScenarioEnded", _ => ended++);
        Start(Scenario(Dialogue("current")), () => finished++);
        Call(_presenter, "StopScenario");
        NextLine();
        Call(_presenter, "StopScenario");
        Assert.That(Get(State, "IsPlaying"), Is.False);
        Assert.That(ended, Is.EqualTo(1));
        Assert.That(finished, Is.Zero);
    }

    [Test]
    public void TemporaryScenarioRestoresTheSameLineAndOriginalCompletionWithoutReplayingProgress()
    {
        int progressUpdates = 0, originalFinished = 0, temporaryFinished = 0;
        RegisterDeferred("ProgressUpdate", (_, _, complete) => { progressUpdates++; complete(); });
        var original = Scenario(Asset("Actions.ProgressUpdateAction"), Dialogue("first", "second", "third"));
        var temporary = Scenario(Dialogue("keyword detail"));
        Start(original, () => originalFinished++);
        NextLine();
        Call(_presenter, "PlayTemporaryScenario", temporary, new Action(() => temporaryFinished++));
        NextLine();
        Assert.That(temporaryFinished, Is.EqualTo(1));
        Assert.That(Get(State, "CurrentScenario"), Is.SameAs(original));
        Assert.That(Get(State, "CurrentActionIndex"), Is.EqualTo(1));
        Assert.That(Get(State, "CurrentSubActionIndex"), Is.EqualTo(1));
        Assert.That(progressUpdates, Is.EqualTo(1));
        Assert.That(originalFinished, Is.Zero);
        NextLine();
        NextLine();
        Assert.That(originalFinished, Is.EqualTo(1));
    }

    [Test]
    public void TemporaryCompletionThatStartsAnotherScenarioDoesNotRestoreOldDialogue()
    {
        var original = Scenario(Dialogue("original"));
        var replacement = Scenario(Dialogue("next phase"));
        Start(original);
        Call(_presenter, "PlayTemporaryScenario", Scenario(Dialogue("detail")), new Action(() => Start(replacement)));
        NextLine();
        Assert.That(Get(State, "CurrentScenario"), Is.SameAs(replacement));
    }

    [Test]
    public void TemporaryDialogueRestoresTheFullColoredLineWithoutRetypingOrDuplicatingTheLog()
    {
        const string source = "original <link=key>word</link>";
        var clueType = Type.GetType("ClueManager, Assembly-CSharp", true);
        var clueSingleton = clueType.GetProperty("Instance");
        var previousClues = clueSingleton.GetValue(null);
        var clueObject = new GameObject("ClueManagerTest");
        SceneManager.MoveGameObjectToScene(clueObject, _scene);
        clueObject.SetActive(false);
        var clues = clueObject.AddComponent(clueType);
        clueSingleton.SetValue(null, clues);
        var provider = Provider();
        var logObject = new GameObject("DialogueLogTest");
        SceneManager.MoveGameObjectToScene(logObject, _scene);
        logObject.SetActive(false);
        var log = logObject.AddComponent(GameType("View.DialogueLogView"));
        Call(log, "OnEnable");
        Component view = null;
        try
        {
            var original = Scenario(Dialogue(source, "following line"));
            Start(original);
            Raise("TypingCompleted");
            Call(_presenter, "PlayTemporaryScenario", Scenario(Dialogue("keyword explanation")), null);
            Call(clues, "SetKeywordColor", "key", "#FFFF00");

            // 復帰表示だけ実Viewに接続する。通常のタイプ演出の実時間待機は不要。
            var canvas = new GameObject("RestoreCanvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(canvas, _scene);
            var textObject = new GameObject("RestoreText", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(textObject, _scene);
            textObject.transform.SetParent(canvas.transform, false);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = "keyword explanation";
            var viewObject = new GameObject("RestoreDialogueView");
            SceneManager.MoveGameObjectToScene(viewObject, _scene);
            viewObject.SetActive(false);
            view = viewObject.AddComponent(GameType("View.DialogueView"));
            SetField(view, "dialogueText", text);
            Call(view, "OnEnable");
            NextLine();

            Assert.That(Get(State, "CurrentScenario"), Is.SameAs(original));
            Assert.That(Get(State, "CurrentSubActionIndex"), Is.Zero);
            Assert.That(Get(State, "IsTyping"), Is.False);
            Assert.That(Get(State, "IsWaitingForInput"), Is.True);
            Assert.That(Get(provider, "IsTyping"), Is.False);
            Assert.That(text.text, Is.EqualTo(Call(clues, "ApplyKeywordColors", source)));
            Assert.That(text.text, Does.Contain("#FFFF00"));
            Assert.That(text.maxVisibleCharacters, Is.EqualTo(int.MaxValue));
            Assert.That(((IList)Get(log, "Log")).Count, Is.EqualTo(2), "Only the original line and keyword detail belong in the log.");

            Call(view, "OnDisable");
            Call(view, "OnUserInput");
            Assert.That(Get(State, "CurrentSubActionIndex"), Is.EqualTo(1), "The first click after restoration must advance, not skip typing.");
            Assert.That(((IList)Get(log, "Log")).Count, Is.EqualTo(3));
        }
        finally
        {
            if (view != null) Call(view, "OnDisable");
            Call(log, "OnDisable");
            Call(provider, "OnDisable");
            clueSingleton.SetValue(null, previousClues);
        }
    }

    private Component Provider()
    {
        var go = new GameObject("DialogueProviderTest");
        SceneManager.MoveGameObjectToScene(go, _scene);
        go.SetActive(false);
        var provider = go.AddComponent(GameType("Adapter.DialogueProviderAdapter"));
        Call(provider, "OnEnable");
        return provider;
    }

    [Test]
    public void TemporaryScenarioAtAChoiceRestoresTheQuestionAndKeepsTheChoicePending()
    {
        var provider = Provider();
        try
        {
            var branch = Scenario(Dialogue("chosen branch"));
            var original = Scenario(Dialogue("original question"), Choice(branch));
            Start(original);
            NextLine();
            Call(_presenter, "PlayTemporaryScenario", Scenario(Dialogue("keyword explanation")), null);
            NextLine();

            Assert.That(Get(provider, "CurrentText"), Is.EqualTo("original question"));
            Assert.That(Get(provider, "IsTyping"), Is.False);
            Assert.That(Get(State, "CurrentScenario"), Is.SameAs(original));
            Assert.That(Get(State, "CurrentActionIndex"), Is.EqualTo(1));
            Assert.That(Get(State, "IsWaitingForChoice"), Is.True);
            var restored = _presenter.GetType().GetField("_lastDialogueData", Flags).GetValue(_presenter);
            Assert.That(restored.GetType().GetField("Instant").GetValue(restored), Is.True);
            Raise("AdvanceRequested");
            Assert.That(Get(State, "IsWaitingForChoice"), Is.True);
            Raise("ChoiceSelected", 0);
            Assert.That(Get(State, "CurrentScenario"), Is.SameAs(branch));
        }
        finally { Call(provider, "OnDisable"); }
    }

    [Test]
    public void TemporaryScenarioDuringAWaitRestoresTheTextAndStillRequiresTheCurrentTimer()
    {
        var provider = Provider();
        try
        {
            var completions = new List<Action>();
            RegisterDeferred("Wait", (_, _, complete) => completions.Add(complete));
            var original = Scenario(Dialogue("before wait"), Asset("Actions.WaitAction"), Dialogue("after wait"));
            Start(original);
            NextLine();
            Call(_presenter, "PlayTemporaryScenario", Scenario(Dialogue("keyword explanation")), null);
            NextLine();

            Assert.That(Get(provider, "CurrentText"), Is.EqualTo("before wait"));
            Assert.That(Get(provider, "IsTyping"), Is.False);
            Assert.That(completions.Count, Is.EqualTo(2));
            Raise("AdvanceRequested");
            completions[0]();
            Assert.That(Get(State, "CurrentActionIndex"), Is.EqualTo(1));
            completions[1]();
            Assert.That(Get(State, "CurrentActionIndex"), Is.EqualTo(2));
            Assert.That(Get(provider, "CurrentText"), Is.EqualTo("after wait"));
        }
        finally { Call(provider, "OnDisable"); }
    }

    [Test]
    public void AnOverlayOnlyScenarioAcceptsItsOwnClicksWhileTheMainWindowIsHidden()
    {
        var go = new GameObject("OverlayViewTest");
        SceneManager.MoveGameObjectToScene(go, _scene);
        go.SetActive(false);
        var view = go.AddComponent(GameType("View.OverlayView"));
        var root = new GameObject("OverlayRoot");
        SceneManager.MoveGameObjectToScene(root, _scene);
        root.SetActive(false);
        SetField(view, "overlayRoot", root);
        Call(view, "OnEnable");
        try
        {
            Register("OverlayActionExecutor", _presenter);
            var original = Scenario(Asset("Actions.OverlayAction"), Asset("Actions.OverlayAction"));
            SetField(original, "showMainWindow", false);
            Start(original);
            Assert.That(root.activeSelf, Is.True);
            Call(view, "OnUserInput");
            Assert.That(root.activeSelf, Is.True);
            Assert.That(Get(State, "CurrentActionIndex"), Is.EqualTo(1));
            Call(view, "OnUserInput");
            Assert.That(root.activeSelf, Is.False);
            Assert.That(Get(State, "IsPlaying"), Is.False);
        }
        finally { Call(view, "OnDisable"); }
    }

    [Test]
    public void TimedOverlayDismissesOnlyOnceAndCannotDismissAReplacement()
    {
        var callbacks = new List<Action>();
        RegisterDeferred("Overlay", (_, _, complete) => callbacks.Add(complete));
        var eventInfo = GameType("Events.ScenarioEventBus").GetEvent("OnOverlayDismissed");
        int dismissed = 0;
        eventInfo.AddEventHandler(null, new Action(() => dismissed++));
        var first = Scenario(Asset("Actions.OverlayAction"), Dialogue("first end"));
        var next = Scenario(Asset("Actions.OverlayAction"), Dialogue("next end"));
        Start(first);
        Start(next);
        Assert.That(dismissed, Is.EqualTo(1), "Replacing an overlay must close the old one.");

        var executor = GameType("Presenter.Executors.OverlayActionExecutor");
        var wait = executor.GetMethod("WaitAutoDismiss", BindingFlags.Static | BindingFlags.NonPublic);
        var oldRoutine = (IEnumerator)wait.Invoke(null, new object[] { 1f, callbacks[0] });
        Assert.That(oldRoutine.MoveNext(), Is.True);
        Assert.That(oldRoutine.MoveNext(), Is.False);
        Assert.That(dismissed, Is.EqualTo(1), "An interrupted timer must not close the new overlay.");
        var currentRoutine = (IEnumerator)wait.Invoke(null, new object[] { 1f, callbacks[1] });
        currentRoutine.MoveNext();
        currentRoutine.MoveNext();
        Assert.That(dismissed, Is.EqualTo(2));
        Assert.That(Get(State, "CurrentActionIndex"), Is.EqualTo(1));
    }

    [Test]
    public void SwitchingToAnOverlayOnlyScenarioHidesThePreviousMainWindow()
    {
        var visibility = new List<bool>();
        GameType("Events.ScenarioEventBus").GetEvent("OnWindowVisibilityChanged")
            .AddEventHandler(null, new Action<bool>(visibility.Add));
        Register("OverlayActionExecutor", _presenter);
        Start(Scenario(Dialogue("main")));
        var overlay = Scenario(Asset("Actions.OverlayAction"));
        SetField(overlay, "showMainWindow", false);
        Start(overlay);
        Assert.That(visibility, Is.EqualTo(new[] { true, false }));
        Assert.That(Get(State, "IsWaitingForInput"), Is.True);
    }

    private (Component view, Button button) ChoiceView()
    {
        var viewObject = new GameObject("ChoiceViewTest");
        SceneManager.MoveGameObjectToScene(viewObject, _scene);
        viewObject.SetActive(false);
        var view = viewObject.AddComponent(GameType("View.ChoiceView"));
        var buttonObject = new GameObject("ChoiceButtonTest", typeof(RectTransform), typeof(Button));
        SceneManager.MoveGameObjectToScene(buttonObject, _scene);
        var button = buttonObject.GetComponent<Button>();
        SetField(view, "choiceButtons", new[] { button });
        Call(view, "OnEnable");
        return (view, button);
    }

    [TestCase("ScenarioStarted")]
    [TestCase("ScenarioEnded")]
    public void LifecycleReplacementKeepsNewChoicesVisibleAfterAllOldSubscribersFinish(string eventName)
    {
        var original = Scenario(Dialogue("old scenario"));
        var replacement = Scenario(Choice(new ScriptableObject[] { null }));
        // 進行制御の購読者がViewより先に登録されている順序も保証する。
        Listen(eventName, scenario => { if (ReferenceEquals(scenario, original)) Start(replacement); });
        var (view, button) = ChoiceView();
        try
        {
            Start(original);
            if (eventName == "ScenarioEnded") NextLine();
            Assert.That(Get(State, "CurrentScenario"), Is.SameAs(replacement));
            Assert.That(Get(State, "IsWaitingForChoice"), Is.True);
            Assert.That(button.gameObject.activeSelf, Is.True, "Old lifecycle notifications must not hide the replacement UI.");
            button.onClick.Invoke();
            Assert.That(Get(State, "IsPlaying"), Is.False);
        }
        finally { Call(view, "OnDisable"); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void StartingWhileAWindowClosesWaitsForTheOldEndedNotification(bool stop)
    {
        var replacement = Scenario(Choice(new ScriptableObject[] { null }));
        bool replaceOnClose = true;
        GameType("Events.ScenarioEventBus").GetEvent("OnWindowVisibilityChanged").AddEventHandler(null,
            new Action<bool>(visible =>
            {
                if (visible || !replaceOnClose) return;
                replaceOnClose = false;
                Start(replacement);
            }));
        var (view, button) = ChoiceView();
        try
        {
            Start(Scenario(Dialogue("old scenario")));
            if (stop) Call(_presenter, "StopScenario");
            else NextLine();
            Assert.That(Get(State, "CurrentScenario"), Is.SameAs(replacement));
            Assert.That(button.gameObject.activeSelf, Is.True);
        }
        finally { Call(view, "OnDisable"); }
    }

    [Test]
    public void AnEndedSubscriberReplacingAnImmediateTemporaryScenarioPreventsItsResume()
    {
        RegisterDeferred("Wait", (_, _, complete) => complete());
        var original = Scenario(Dialogue("original"));
        var temporary = Scenario(Asset("Actions.WaitAction"));
        var replacement = Scenario(Dialogue("replacement"));
        int originalStarts = 0;
        Listen("ScenarioStarted", scenario => { if (ReferenceEquals(scenario, original)) originalStarts++; });
        Listen("ScenarioEnded", scenario => { if (ReferenceEquals(scenario, temporary)) Start(replacement); });
        Start(original);
        Call(_presenter, "PlayTemporaryScenario", temporary, null);
        Assert.That(Get(State, "CurrentScenario"), Is.SameAs(replacement));
        Assert.That(originalStarts, Is.EqualTo(1), "A replacement queued by an Ended listener must prevent temporary restoration.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ChainedScenariosRetainTheOriginalCompletionUntilTheFinalScenario(bool synchronous)
    {
        RegisterDeferred("Wait", (_, _, complete) => complete());
        var final = Scenario(Dialogue("final"));
        var first = Scenario(synchronous ? Asset("Actions.WaitAction") : Dialogue("first"));
        SetField(first, "nextScenario", final);
        int finished = 0;
        Start(first, () => finished++);
        if (!synchronous) NextLine();
        Assert.That(Get(State, "CurrentScenario"), Is.SameAs(final));
        Assert.That(finished, Is.Zero);
        NextLine();
        Assert.That(finished, Is.EqualTo(1));
    }

    [Test]
    public void ALoopReplaysWithoutCompletingAndCanBeExplicitlyStopped()
    {
        var loop = Scenario(Dialogue("loop"));
        SetField(loop, "loop", true);
        int finished = 0;
        Start(loop, () => finished++);
        NextLine();
        NextLine();
        Assert.That(Get(State, "CurrentScenario"), Is.SameAs(loop));
        Assert.That(Get(State, "IsTyping"), Is.True);
        Assert.That(finished, Is.Zero);
        Call(_presenter, "StopScenario");
        Assert.That(Get(State, "IsPlaying"), Is.False);
        Assert.That(finished, Is.Zero);
    }
}

// 遅延完了を決定的に再現するExecutor。ゲームの実行アセンブリには追加しない。
public class ScenarioExecutorProxy : DispatchProxy
{
    public string ActionType;
    public Action<object, object, Action> ExecuteAction;

    protected override object Invoke(MethodInfo targetMethod, object[] args)
    {
        if (targetMethod.Name == "get_HandledActionType") return ActionType;
        if (targetMethod.Name == "Execute") ExecuteAction(args[0], args[1], (Action)args[2]);
        return null;
    }
}
