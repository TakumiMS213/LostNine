using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class ScenarioDatabaseTests
{
    private readonly List<Object> _created = new();
    private static Type GameType(string name) => Type.GetType("ScenarioSystem." + name + ", Assembly-CSharp", true);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name).SetValue(target, value);
    private static object Get(object target, string name) => target.GetType().GetField(name).GetValue(target);
    private static void Call(object target, string name) => target.GetType().GetMethod(name).Invoke(target, null);

    private ScriptableObject Create(string name)
    {
        var value = ScriptableObject.CreateInstance(GameType(name));
        _created.Add(value);
        return value;
    }

    private ScriptableObject Scenario(string id)
    {
        var scenario = Create("Model.ScenarioData");
        scenario.name = id ?? "Anonymous";
        Set(scenario, "scenarioId", id);
        return scenario;
    }

    private static object[] Lookup(object database, string id)
    {
        var arguments = new object[] { id, null };
        bool found = (bool)database.GetType().GetMethod("TryGetById").Invoke(database, arguments);
        return new[] { (object)found, arguments[1] };
    }

    private static IList List(object target, string name) => (IList)Get(target, name);

    private static IList Graph(params ScriptableObject[] roots)
    {
        var typedRoots = Array.CreateInstance(GameType("Model.ScenarioData"), roots.Length);
        for (int i = 0; i < roots.Length; i++) typedRoots.SetValue(roots[i], i);
        return (IList)GameType("Model.ScenarioGraph").GetMethod("Collect").Invoke(null, new object[] { typedRoots });
    }

    private ScriptableObject AddChoice(ScriptableObject scenario, params ScriptableObject[] destinations)
    {
        var choice = Create("Model.Actions.ChoiceAction");
        foreach (var destination in destinations)
        {
            var entry = Activator.CreateInstance(GameType("Model.Actions.ChoiceEntry"));
            Set(entry, "nextScenario", destination);
            List(choice, "choices").Add(entry);
        }
        List(scenario, "actions").Add(choice);
        return choice;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var value in _created)
        {
            if (value == null) continue;
            Undo.ClearUndo(value);
            Object.DestroyImmediate(value);
        }
        _created.Clear();
    }

    [Test]
    public void GraphVisitsSharedBranchesAndCyclesOnceAndKeepsRegisteredRootsFirst()
    {
        var first = Scenario("first");
        var second = Scenario("second");
        var anonymous = Scenario(null);
        var branch = Scenario("branch");
        Set(first, "nextScenario", anonymous);
        Set(anonymous, "nextScenario", first);
        AddChoice(first, branch, second, branch, null);
        List(first, "actions").Add(null);
        var actual = Graph(first, second, first, null).Cast<Object>().ToArray();
        Assert.That(actual, Is.EqualTo(new Object[] { first, second, anonymous, branch }));
    }

    [Test]
    public void RegisteringAnEntryAlsoResolvesNamedDescendantsThroughAnonymousScenarios()
    {
        var database = Create("Adapter.ScenarioDataDatabase");
        var root = Scenario("entry");
        var anonymous = Scenario(string.Empty);
        var detail = Scenario("keyword");
        Set(root, "nextScenario", anonymous);
        AddChoice(anonymous, detail);
        List(database, "allScenarios").Add(root);
        Assert.That(Lookup(database, "keyword"), Is.EqualTo(new object[] { true, detail }));
        Assert.That(Lookup(database, string.Empty)[0], Is.False);
    }

    [Test]
    public void LookupTrimsIdsWithoutChangingStoredValuesOrCaseSensitivity()
    {
        var database = Create("Adapter.ScenarioDataDatabase");
        var scenario = Scenario("  Ch2_Dialogue \t");
        List(database, "allScenarios").Add(scenario);
        Assert.That(Lookup(database, " Ch2_Dialogue "), Is.EqualTo(new object[] { true, scenario }));
        Assert.That(Lookup(database, "ch2_dialogue")[0], Is.False);
        Assert.That(Get(scenario, "scenarioId"), Is.EqualTo("  Ch2_Dialogue \t"));
    }

    [Test]
    public void InspectorRenameRemovesTheOldIdAndResolvesTheNewId()
    {
        var database = Create("Adapter.ScenarioDataDatabase");
        var scenario = Scenario("old");
        List(database, "allScenarios").Add(scenario);
        Assert.That(Lookup(database, "old")[0], Is.True);
        var serialized = new SerializedObject(scenario);
        serialized.FindProperty("scenarioId").stringValue = "new";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(Lookup(database, "old")[0], Is.False);
        Assert.That(Lookup(database, "new"), Is.EqualTo(new object[] { true, scenario }));
    }

    [Test]
    public void InspectorRegistrationChangesAreVisibleWithoutReloadingTheDatabase()
    {
        var database = Create("Adapter.ScenarioDataDatabase");
        var first = Scenario("first");
        var second = Scenario("second");
        List(database, "allScenarios").Add(first);
        Assert.That(Lookup(database, "first")[0], Is.True);
        var serialized = new SerializedObject(database);
        serialized.FindProperty("allScenarios").GetArrayElementAtIndex(0).objectReferenceValue = second;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(Lookup(database, "first")[0], Is.False);
        Assert.That(Lookup(database, "second"), Is.EqualTo(new object[] { true, second }));
    }

    [Test]
    public void UndoAndRedoOfAnIdEditRefreshLookupWithoutReopeningAssets()
    {
        var database = Create("Adapter.ScenarioDataDatabase");
        var scenario = Scenario("original");
        List(database, "allScenarios").Add(scenario);
        Assert.That(Lookup(database, "original")[0], Is.True);
        Undo.IncrementCurrentGroup();
        var serialized = new SerializedObject(scenario);
        serialized.FindProperty("scenarioId").stringValue = "edited";
        serialized.ApplyModifiedProperties();
        Undo.FlushUndoRecordObjects();
        Assert.That(Lookup(database, "edited")[0], Is.True);
        Undo.PerformUndo();
        Assert.That(Lookup(database, "original"), Is.EqualTo(new object[] { true, scenario }));
        Assert.That(Lookup(database, "edited")[0], Is.False);
        Undo.PerformRedo();
        Assert.That(Lookup(database, "edited"), Is.EqualTo(new object[] { true, scenario }));
        Assert.That(Lookup(database, "original")[0], Is.False);
    }

    [Test]
    public void InspectorChoiceDestinationChangesRefreshAllDatabasesUsingTheSharedAction()
    {
        var root = Scenario("entry");
        var oldDestination = Scenario("old");
        var newDestination = Scenario("new");
        var choice = AddChoice(root, oldDestination);
        var databases = new[] { Create("Adapter.ScenarioDataDatabase"), Create("Adapter.ScenarioDataDatabase") };
        foreach (var database in databases)
        {
            List(database, "allScenarios").Add(root);
            Assert.That(Lookup(database, "old")[0], Is.True);
        }
        var serialized = new SerializedObject(choice);
        serialized.FindProperty("choices").GetArrayElementAtIndex(0).FindPropertyRelative("nextScenario").objectReferenceValue = newDestination;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        foreach (var database in databases)
        {
            Assert.That(Lookup(database, "old")[0], Is.False);
            Assert.That(Lookup(database, "new"), Is.EqualTo(new object[] { true, newDestination }));
        }
    }

    [Test]
    public void RepeatedReferencesDoNotWarnButDistinctDuplicateIdsKeepExplicitRegistrationPrecedence()
    {
        var database = Create("Adapter.ScenarioDataDatabase");
        var first = Scenario("same");
        var second = Scenario(" same ");
        List(database, "allScenarios").Add(first);
        List(database, "allScenarios").Add(first);
        Assert.That(Lookup(database, "same"), Is.EqualTo(new object[] { true, first }));
        LogAssert.NoUnexpectedReceived();

        Set(first, "nextScenario", second);
        Call(first, "NotifyDataChanged");
        LogAssert.Expect(LogType.Warning, "[ScenarioDataDatabase] Duplicate ID: same in  same ");
        Assert.That(Lookup(database, "same"), Is.EqualTo(new object[] { true, first }));
    }

    [Test]
    public void MissingCollectionsAndDestroyedScenariosAreNotSuccessfulLookups()
    {
        var database = Create("Adapter.ScenarioDataDatabase");
        Set(database, "allScenarios", null);
        Assert.That(Lookup(database, "missing")[0], Is.False);
        Assert.That(Lookup(database, null)[0], Is.False);
        var scenario = Scenario("temporary");
        var roots = Activator.CreateInstance(typeof(List<>).MakeGenericType(GameType("Model.ScenarioData"))) as IList;
        roots.Add(null);
        roots.Add(scenario);
        Set(database, "allScenarios", roots);
        Call(database, "InvalidateCache");
        Assert.That(Lookup(database, "temporary")[0], Is.True);
        Object.DestroyImmediate(scenario);
        Assert.That(Lookup(database, "temporary")[0], Is.False);
    }

    [Test]
    public void ExistingProjectIdsResolveToTheSameAssetsWithoutMigration()
    {
        var database = AssetDatabase.LoadMainAssetAtPath("Assets/ScriptableObjects/ProgressSystemDatabase/ScenarioDataDatabase.asset");
        Assert.That(database, Is.Not.Null);
        Call(database, "InvalidateCache");
        foreach (Object scenario in List(database, "allScenarios"))
        {
            Assert.That(scenario, Is.Not.Null);
            var id = (string)Get(scenario, "scenarioId");
            if (string.IsNullOrWhiteSpace(id)) continue;
            Assert.That(Lookup(database, id), Is.EqualTo(new object[] { true, scenario }), id);
        }
    }

    [Test]
    public void KeywordValidationUsesTheSameTagSyntaxAsDisplayFormatting()
    {
        var formatter = Type.GetType("MessageWindowSystem.Core.KeywordTextFormatter, Assembly-CSharp", true);
        const string text = "<link=\" key \"><color=red>word</color></link> <a href=second>alias</a> <link=\"\">empty</link>";
        var ids = (IEnumerable)formatter.GetMethod("GetKeywordIds").Invoke(null, new object[] { text });
        Assert.That(ids.Cast<string>(), Is.EqualTo(new[] { "key", "second", string.Empty }));
        Assert.That((bool)formatter.GetMethod("ContainsKeywords").Invoke(null, new object[] { text }), Is.True);
    }
}
