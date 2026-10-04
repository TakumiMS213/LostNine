using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class ScenarioDataValidationTests
{
    private const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private readonly List<ScriptableObject> _assets = new();
    private static Type ModelType(string name) => Type.GetType("ScenarioSystem.Model." + name + ", Assembly-CSharp", true);
    private static object Field(object target, string name) => target.GetType().GetField(name, Fields).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
    private static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static string Code(object issue) => (string)Property(issue, "Code");
    private static string Severity(object issue) => Property(issue, "Severity").ToString();

    [TearDown]
    public void TearDown()
    {
        foreach (var asset in _assets) Object.DestroyImmediate(asset);
        _assets.Clear();
    }

    private ScriptableObject Asset(string type)
    {
        var asset = ScriptableObject.CreateInstance(ModelType(type));
        asset.name = type + "_" + _assets.Count;
        _assets.Add(asset);
        return asset;
    }

    private ScriptableObject Scenario(params ScriptableObject[] actions)
    {
        var scenario = Asset("ScenarioData");
        foreach (var action in actions) ((IList)Field(scenario, "actions")).Add(action);
        return scenario;
    }

    private ScriptableObject Dialogue(string text = "会話本文")
    {
        var action = Asset("Actions.DialogueAction");
        object entry = Activator.CreateInstance(ModelType("Actions.DialogueEntry"));
        Set(entry, "text", text);
        ((IList)Field(action, "entries")).Add(entry);
        return action;
    }

    private ScriptableObject Choice(params ScriptableObject[] destinations)
    {
        var action = Asset("Actions.ChoiceAction");
        foreach (var destination in destinations)
        {
            object entry = Activator.CreateInstance(ModelType("Actions.ChoiceEntry"));
            Set(entry, "choiceText", "選ぶ");
            Set(entry, "nextScenario", destination);
            ((IList)Field(action, "choices")).Add(entry);
        }
        return action;
    }

    private static object[] Validate(params ScriptableObject[] scenarios)
    {
        Array roots = null;
        if (scenarios != null)
        {
            roots = Array.CreateInstance(ModelType("ScenarioData"), scenarios.Length);
            for (int i = 0; i < scenarios.Length; i++) roots.SetValue(scenarios[i], i);
        }
        return ((IEnumerable)ModelType("Validation.ScenarioDataValidator").GetMethod("Validate")
            .Invoke(null, new object[] { roots })).Cast<object>().ToArray();
    }

    private static void HasIssue(IEnumerable<object> issues, string code, Object context = null, string path = null)
    {
        var matches = issues.Where(issue => Code(issue) == code).ToArray();
        Assert.That(matches, Is.Not.Empty, code);
        if (context != null) Assert.That(matches.Any(issue => (Object)Property(issue, "Context") == context), Is.True, code + " context");
        if (path != null) Assert.That(matches.Any(issue => (string)Property(issue, "PropertyPath") == path), Is.True, code + " path");
        Assert.That(matches.All(issue => !string.IsNullOrWhiteSpace((string)Property(issue, "Message"))), Is.True);
    }

    [Test]
    public void EmptyIdsSharedActionsAndInputLoopsAreValid()
    {
        var dialogue = Dialogue();
        var root = Scenario(dialogue);
        var continuation = Scenario(dialogue);
        Set(root, "nextScenario", continuation);
        Set(continuation, "loop", true);
        Assert.That(Validate(root), Is.Empty);
    }

    [Test]
    public void DuplicateNormalizedIdsAreDetectedAcrossChoiceAndNextReferences()
    {
        var first = Scenario(Dialogue());
        var second = Scenario(Dialogue());
        Set(first, "scenarioId", "Ch2_Branch");
        Set(second, "scenarioId", " Ch2_Branch ");
        var root = Scenario(Choice(first));
        Set(root, "nextScenario", second);
        var issues = Validate(root);
        HasIssue(issues, "DUPLICATE_ID", path: "scenarioId");
        HasIssue(issues, "ID_WHITESPACE", second, "scenarioId");
    }

    [Test]
    public void RegisteringSameScenarioTwiceWarnsWithoutReportingDuplicateId()
    {
        var scenario = Scenario(Dialogue());
        Set(scenario, "scenarioId", "Unique");
        var issues = Validate(scenario, scenario);
        HasIssue(issues, "DUPLICATE_REGISTRATION", path: "allScenarios.Array.data[1]");
        Assert.That(issues.Select(Code), Does.Not.Contain("DUPLICATE_ID"));
        Assert.That(issues.All(issue => Severity(issue) == "Warning"), Is.True);
    }

    [Test]
    public void MissingRootsAndActionReferencesIdentifyTheSerializedField()
    {
        HasIssue(Validate(null), "NULL_ROOTS");
        HasIssue(Validate(new ScriptableObject[] { null }), "NULL_SCENARIO", path: "allScenarios.Array.data[0]");
        var scenario = Scenario(Dialogue(), null);
        HasIssue(Validate(scenario), "NULL_ACTION", scenario, "actions.Array.data[1]");
        Set(scenario, "actions", null);
        HasIssue(Validate(scenario), "NULL_ACTIONS", scenario, "actions");
    }

    [Test]
    public void EmptyDialogueAndChoiceListsAreErrors()
    {
        var dialogue = Asset("Actions.DialogueAction");
        var choice = Asset("Actions.ChoiceAction");
        var issues = Validate(Scenario(dialogue, choice));
        HasIssue(issues, "EMPTY_DIALOGUE", dialogue, "entries");
        HasIssue(issues, "EMPTY_CHOICES", choice, "choices");
        Assert.That(issues.All(issue => Severity(issue) == "Error"), Is.True);
    }

    [Test]
    public void NullAndUnlabelledChoicesAreDetectedButEndingBranchesAreAllowed()
    {
        var choice = Choice(new ScriptableObject[] { null });
        Assert.That(Validate(Scenario(choice)), Is.Empty, "A null destination intentionally ends the scenario.");
        var entries = (IList)Field(choice, "choices");
        Set(entries[0], "choiceText", " ");
        entries.Add(null);
        var issues = Validate(Scenario(choice));
        HasIssue(issues, "EMPTY_CHOICE_TEXT", choice, "choices.Array.data[0].choiceText");
        HasIssue(issues, "NULL_CHOICE", choice, "choices.Array.data[1]");
    }

    [Test]
    public void RepeatedSharedActionIsReportedAtItsSequencePositionOnly()
    {
        var dialogue = Dialogue();
        var root = Scenario(dialogue, dialogue);
        var next = Scenario(dialogue);
        Set(root, "nextScenario", next);
        var issues = Validate(root);
        Assert.That(issues.Length, Is.EqualTo(1));
        HasIssue(issues, "REPEATED_ACTION", root, "actions.Array.data[1]");
    }

    [Test]
    public void SharedMalformedActionIsValidatedOnlyOnce()
    {
        var empty = Asset("Actions.DialogueAction");
        var first = Scenario(empty);
        var second = Scenario(empty);
        Assert.That(Validate(first, second).Count(issue => Code(issue) == "EMPTY_DIALOGUE"), Is.EqualTo(1));
    }

    [Test]
    public void HiddenMainWindowAndLoopDestinationConflictWarnWithoutChangingData()
    {
        var root = Scenario(Dialogue());
        Set(root, "showMainWindow", false);
        Set(root, "loop", true);
        Set(root, "nextScenario", Scenario(Dialogue()));
        string before = EditorJsonUtility.ToJson(root);
        var issues = Validate(root);
        HasIssue(issues, "HIDDEN_MAIN_WINDOW", root, "actions.Array.data[0]");
        HasIssue(issues, "LOOP_NEXT_CONFLICT", root, "nextScenario");
        Assert.That(EditorJsonUtility.ToJson(root), Is.EqualTo(before));
        Assert.That(issues.All(issue => Severity(issue) == "Warning"), Is.True);
    }

    [TestCase("choice")]
    [TestCase("scene")]
    public void ActionsFollowingTerminalControlFlowAreFlagged(string kind)
    {
        var terminal = kind == "choice" ? Choice(new ScriptableObject[] { null }) : Asset("Actions.SceneTransitionAction");
        var root = Scenario(terminal, Dialogue());
        HasIssue(Validate(root), "UNREACHABLE_ACTIONS", root, "actions.Array.data[0]");
    }

    [Test]
    public void InvalidSceneDestinationIsAnErrorInsteadOfAFalseTerminalWarning()
    {
        var scene = Asset("Actions.SceneTransitionAction");
        Set(scene, "useChapterSelect", false);
        var issues = Validate(Scenario(scene, Dialogue()));
        HasIssue(issues, "MISSING_SCENE", scene, "targetSceneName");
        Assert.That(issues.Select(Code), Does.Not.Contain("UNREACHABLE_ACTIONS"));
    }

    [TestCase("portraitPosition", "PortraitPosition")]
    [TestCase("nameSlideDirection", "NameSlideDirection")]
    public void UndefinedDialogueEnumsIdentifyTheLine(string field, string enumName)
    {
        var dialogue = Dialogue();
        var entries = (IList)Field(dialogue, "entries");
        object entry = entries[0];
        Set(entry, field, Enum.ToObject(ModelType("Actions." + enumName), 99));
        entries[0] = entry;
        HasIssue(Validate(Scenario(dialogue)), "INVALID_ENUM", dialogue, "entries.Array.data[0]." + field);
    }

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(-1f)]
    public void InvalidWaitDurationIsReported(float duration)
    {
        var wait = Asset("Actions.WaitAction");
        Set(wait, "duration", duration);
        HasIssue(Validate(Scenario(wait)), "INVALID_NUMBER", wait, "duration");
    }

    [Test]
    public void NegativeOverlayDurationRetainsClickToDismissMeaning()
    {
        var overlay = Asset("Actions.OverlayAction");
        Set(overlay, "text", "表示");
        Set(overlay, "displayDuration", -1f);
        var scenario = Scenario(overlay);
        Set(scenario, "loop", true);
        Assert.That(Validate(scenario), Is.Empty);
    }

    [Test]
    public void KeywordResolutionUsesTheSameTagsAndNormalizationAsRendering()
    {
        var destination = Scenario(Dialogue());
        Set(destination, "scenarioId", "Known");
        var root = Scenario(Dialogue("<link=\" Known \">一</link><a href=Known>二</a><link=dummy_no_story>三</link>"));
        Assert.That(Validate(root, destination), Is.Empty);
        var unknown = Dialogue("<link=\"Missing\">一</link><link=Missing>二</link><link=\"\">三</link>");
        var issues = Validate(Scenario(unknown));
        HasIssue(issues, "MISSING_KEYWORD_SCENARIO", unknown, "entries.Array.data[0].text");
        HasIssue(issues, "EMPTY_KEYWORD_ID", unknown, "entries.Array.data[0].text");
        Assert.That(issues.Count(issue => Code(issue) == "MISSING_KEYWORD_SCENARIO"), Is.EqualTo(1));
    }

    [Test]
    public void ImmediateNextScenarioCycleIsReportedOnceWithoutRecursingForever()
    {
        var first = Scenario(Asset("Actions.KeywordEnableAction"));
        var second = Scenario(Asset("Actions.EffectAction"));
        Set(first, "nextScenario", second);
        Set(second, "nextScenario", first);
        var issues = Validate(first, second);
        Assert.That(issues.Count(issue => Code(issue) == "IMMEDIATE_CYCLE"), Is.EqualTo(1));
    }

    [Test]
    public void EmptyAndZeroDurationLoopsAreDetected()
    {
        var empty = Scenario();
        Set(empty, "loop", true);
        HasIssue(Validate(empty), "IMMEDIATE_CYCLE", empty, "loop");
        var wait = Asset("Actions.WaitAction");
        Set(wait, "duration", 0f);
        var root = Scenario(wait);
        Set(root, "loop", true);
        HasIssue(Validate(root), "IMMEDIATE_CYCLE", root, "loop");
    }

    [TestCase("wait")]
    [TestCase("dialogue")]
    [TestCase("choice")]
    [TestCase("logo")]
    public void ActualInputOrTimeWaitBreaksAnAutomaticCycle(string kind)
    {
        ScriptableObject action = kind switch
        {
            "wait" => Asset("Actions.WaitAction"),
            "dialogue" => Dialogue(),
            "choice" => Choice(new ScriptableObject[] { null }),
            _ => Asset("Actions.TitleLogoAction")
        };
        var first = Scenario(action);
        var second = Scenario();
        Set(first, "nextScenario", second);
        Set(second, "nextScenario", first);
        Assert.That(Validate(first), Is.Empty);
    }

    [Test]
    public void MissingActionBeforeWaitDoesNotMaskAnImmediateLoop()
    {
        var root = Scenario(null, Asset("Actions.WaitAction"));
        Set(root, "loop", true);
        var issues = Validate(root);
        HasIssue(issues, "NULL_ACTION", root);
        HasIssue(issues, "IMMEDIATE_CYCLE", root);
    }

    [Test]
    public void ShippedDatabaseHasNoStructuralErrorsAndValidationDoesNotEditAssets()
    {
        var database = AssetDatabase.LoadMainAssetAtPath("Assets/ScriptableObjects/ProgressSystemDatabase/ScenarioDataDatabase.asset");
        Assert.That(database, Is.Not.Null);
        var scenarios = ((IEnumerable)Field(database, "allScenarios")).Cast<ScriptableObject>().ToArray();
        var tracked = new HashSet<Object> { database };
        foreach (var scenario in scenarios)
        {
            if (scenario == null) continue;
            tracked.Add(scenario);
            foreach (Object action in (IEnumerable)Field(scenario, "actions"))
                if (action != null) tracked.Add(action);
        }
        var snapshots = tracked.ToDictionary(asset => asset, EditorJsonUtility.ToJson);
        var dirtiness = tracked.ToDictionary(asset => asset, EditorUtility.IsDirty);
        var errors = Validate(scenarios).Where(issue => Severity(issue) == "Error").ToArray();
        Assert.That(errors, Is.Empty, string.Join("\n", errors.Select(issue => Property(issue, "Message"))));
        foreach (var asset in tracked)
        {
            Assert.That(EditorJsonUtility.ToJson(asset), Is.EqualTo(snapshots[asset]), asset.name);
            Assert.That(EditorUtility.IsDirty(asset), Is.EqualTo(dirtiness[asset]), asset.name);
        }
    }
}
