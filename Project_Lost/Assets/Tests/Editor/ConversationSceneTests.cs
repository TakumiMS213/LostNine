using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ConversationSceneTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type GameType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static object Get(object target, string field) => target.GetType().GetField(field, Fields).GetValue(target);

    private static Component[] Find(Scene scene, string type)
    {
        Type componentType = GameType(type);
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren(componentType, true)).ToArray();
    }

    private static T[] Find<T>(Scene scene) where T : Component
        => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

    private static Component Single(Scene scene, string type)
    {
        var matches = Find(scene, type);
        Assert.That(matches.Length, Is.EqualTo(1), scene.name + " must contain exactly one " + type);
        return matches[0];
    }

    private static void Inspect(string sceneName, Action<Scene> inspect)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        bool activeSceneDirty = activeScene.isDirty;
        Scene preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/" + sceneName + ".unity");
        bool previewDirty = preview.isDirty;
        try
        {
            inspect(preview);
            Assert.That(preview.isDirty, Is.EqualTo(previewDirty), "Read-only scene validation must not edit serialized scene content.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(activeScene));
            Assert.That(activeScene.isDirty, Is.EqualTo(activeSceneDirty));
        }
    }

    [TestCase("Main")]
    [TestCase("Story")]
    public void ConversationScenesHaveNoMissingScriptsAndOneExplicitKeywordSource(string sceneName)
    {
        Inspect(sceneName, scene =>
        {
            foreach (var transform in Find<Transform>(scene))
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero,
                    sceneName + "/" + transform.name + " contains a missing script.");

            var keyword = Single(scene, "MessageWindowSystem.Core.KeywordHandler");
            var provider = Single(scene, "ScenarioSystem.Adapter.DialogueProviderAdapter");
            var dialogue = Single(scene, "ScenarioSystem.View.DialogueView");
            var text = (TMP_Text)Get(provider, "dialogueText");
            Assert.That(text, Is.Not.Null);
            Assert.That(Get(keyword, "dialogueProviderSource"), Is.SameAs(provider));
            Assert.That(keyword.GetComponent<TMP_Text>(), Is.SameAs(text));
            Assert.That(Get(dialogue, "dialogueText"), Is.SameAs(text));
            Assert.That(Get(dialogue, "keywordHandler"), Is.SameAs(keyword));
            Assert.That(((Behaviour)keyword).enabled, Is.True);
            Assert.That(((Behaviour)provider).enabled, Is.True);
            Assert.That(text.raycastTarget, Is.True, "Keyword text must receive pointer events.");
            var clickArea = (Graphic)Get(keyword, "clickAreaGraphic");
            Assert.That(clickArea, Is.Not.Null);
            Assert.That(clickArea.enabled && clickArea.raycastTarget, Is.True);
            Assert.That(clickArea.GetComponent<Button>(), Is.Not.Null);
        });
    }

    [TestCase("Main")]
    [TestCase("Story")]
    public void DialogueAndOverlayHaveValidIndependentUIAndClickListeners(string sceneName)
    {
        Inspect(sceneName, scene =>
        {
            var dialogue = Single(scene, "ScenarioSystem.View.DialogueView");
            var overlay = Single(scene, "ScenarioSystem.View.OverlayView");
            var dialogueRoot = (GameObject)Get(dialogue, "windowRoot");
            var overlayRoot = (GameObject)Get(overlay, "overlayRoot");
            Assert.That(dialogueRoot, Is.Not.Null);
            Assert.That(overlayRoot, Is.Not.Null);
            Assert.That(overlayRoot, Is.Not.SameAs(dialogueRoot));
            Assert.That(((TMP_Text)Get(dialogue, "dialogueText")).transform.IsChildOf(dialogueRoot.transform), Is.True);
            foreach (var field in new[] { "speakerNameText", "overlayText", "portraitImage", "portraitLeftAnchor", "portraitCenterAnchor", "portraitRightAnchor" })
            {
                var reference = Get(overlay, field) as Component;
                Assert.That(reference, Is.Not.Null, sceneName + " overlay " + field);
                Assert.That(reference.transform.IsChildOf(overlayRoot.transform), Is.True, field);
            }
            Assert.That(((Behaviour)dialogue).isActiveAndEnabled, Is.True, "Dialogue controller must remain enabled when the window is hidden.");
            Assert.That(((Behaviour)overlay).isActiveAndEnabled, Is.True, "Overlay controller must remain enabled when the window is hidden.");
            AssertClickReceiver(scene, dialogue);
            AssertClickReceiver(scene, overlay);
        });
    }

    private static void AssertClickReceiver(Scene scene, Component view)
    {
        var receivers = Find<Button>(scene).Where(button => Enumerable.Range(0, button.onClick.GetPersistentEventCount())
            .Any(i => button.onClick.GetPersistentTarget(i) == view && button.onClick.GetPersistentMethodName(i) == "OnUserInput"
                && button.onClick.GetPersistentListenerState(i) != UnityEventCallState.Off)).ToArray();
        Assert.That(receivers, Is.Not.Empty, view.GetType().Name + " must have a serialized click receiver.");
        foreach (var button in receivers)
        {
            Assert.That(button.enabled && button.interactable, Is.True, button.name);
            Assert.That(button.targetGraphic, Is.Not.Null, button.name);
            Assert.That(button.targetGraphic.enabled && button.targetGraphic.raycastTarget, Is.True, button.name);
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentListenerState(i) == UnityEventCallState.Off) continue;
                var target = button.onClick.GetPersistentTarget(i);
                string method = button.onClick.GetPersistentMethodName(i);
                Assert.That(target, Is.Not.Null, button.name + " has a missing listener target.");
                Assert.That(target.GetType().GetMethods(Fields).Any(candidate => candidate.Name == method), Is.True,
                    button.name + " has a missing listener method: " + method);
            }
        }
    }

    [Test]
    public void MainDialogueButtonHasForegroundFrameTextAndTheOnlyPortraitConversationListener()
    {
        Inspect("Main", scene =>
        {
            var manager = Single(scene, "ComuStartandEndManager");
            var portrait = (GameObject)Get(manager, "Portrait");
            var button = (Button)Get(manager, "dialogueStartButton");
            var label = (TMP_Text)Get(manager, "dialogueStartButtonLabel");
            Assert.That(button, Is.Not.Null);
            Assert.That(label, Is.Not.Null);
            Assert.That(button.transform.IsChildOf(portrait.transform), Is.True, "The button must follow the scenario's portrait placement.");
            Assert.That(label.transform.IsChildOf(button.transform), Is.True);
            Assert.That(label.text, Is.EqualTo("対話開始"));
            Assert.That(label.font, Is.SameAs(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/DotGothic16-Regular SDF.asset")));
            Assert.That(label.raycastTarget, Is.False, "The decorative label must not intercept the button's pointer input.");
            Assert.That(button.enabled, Is.True);
            Assert.That(button.gameObject.activeSelf, Is.False, "The scenario, rather than scene loading, requests the button.");
            var background = button.targetGraphic as Image;
            Assert.That(background, Is.Not.Null);
            Assert.That(background.enabled && background.raycastTarget, Is.True);
            Assert.That(background.sprite, Is.SameAs(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Tuning/flame_Sq.png")));
            var rect = (RectTransform)button.transform;
            Assert.That(rect.rect.width, Is.GreaterThan(rect.rect.height * 2f), "The square frame must be stretched into a horizontal button.");

            var buttonCanvas = button.GetComponentInParent<Canvas>(true);
            var portraitCanvas = portrait.GetComponentInParent<Canvas>(true);
            Assert.That(buttonCanvas, Is.Not.Null);
            Assert.That(portraitCanvas, Is.Not.Null);
            Assert.That(buttonCanvas, Is.Not.SameAs(portraitCanvas));
            Assert.That(buttonCanvas.overrideSorting, Is.True);
            Assert.That(buttonCanvas.sortingOrder, Is.GreaterThan(portraitCanvas.sortingOrder), "The button must render and receive clicks in front of Portrait.");
            Assert.That(buttonCanvas.GetComponent<GraphicRaycaster>(), Is.Not.Null);

            Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1));
            Assert.That(button.onClick.GetPersistentTarget(0), Is.SameAs(manager));
            Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo("ToggleComuFromButton"));
            Assert.That(button.onClick.GetPersistentListenerState(0), Is.Not.EqualTo(UnityEventCallState.Off));
            foreach (var oldButton in Find<Button>(scene).Where(candidate => candidate.name == "Portrait" || candidate.name == "DummyPortrait"))
            {
                Assert.That(oldButton.enabled, Is.False, oldButton.name + " must no longer start or animate conversations.");
                Assert.That(oldButton.onClick.GetPersistentEventCount(), Is.Zero, oldButton.name + " still has a legacy portrait click listener.");
            }
        });
    }

    [TestCase("Main")]
    [TestCase("Story")]
    public void ConversationCanvasHasPointerRaycastingAndInputActions(string sceneName)
    {
        Inspect(sceneName, scene =>
        {
            var systems = Find<EventSystem>(scene).Where(system => system.isActiveAndEnabled).ToArray();
            Assert.That(systems.Length, Is.EqualTo(1));
            var module = systems[0].GetComponent<InputSystemUIInputModule>();
            Assert.That(module, Is.Not.Null);
            Assert.That(module.isActiveAndEnabled, Is.True);
            Assert.That(module.point != null && module.point.action != null, Is.True, "UI pointer action is missing.");
            Assert.That(module.leftClick != null && module.leftClick.action != null, Is.True, "UI click action is missing.");

            var provider = Single(scene, "ScenarioSystem.Adapter.DialogueProviderAdapter");
            var text = (TMP_Text)Get(provider, "dialogueText");
            var canvas = text.GetComponentInParent<Canvas>(true);
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.enabled, Is.True);
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            Assert.That(raycaster, Is.Not.Null);
            Assert.That(raycaster.enabled, Is.True);
        });
    }

    [TestCase("Main")]
    [TestCase("Story")]
    public void BootstrapRegistersEveryActionUsedByTheProject(string sceneName)
    {
        Inspect(sceneName, scene =>
        {
            var bootstrap = Single(scene, "ScenarioSystem.Runtime.ScenarioBootstrap");
            var facade = Single(scene, "ScenarioSystem.Adapter.MessageWindowFacade");
            var presenter = Get(bootstrap, "presenter") as Component;
            Assert.That(presenter, Is.Not.Null);
            Assert.That(Get(facade, "presenter"), Is.SameAs(presenter));
            var database = Get(bootstrap, "scenarioDatabase") as UnityEngine.Object;
            Assert.That(database, Is.Not.Null);
            Assert.That(Get(facade, "scenarioDataDatabase"), Is.SameAs(database));

            // 登録は非シリアライズ辞書のみ。UI状態やシーンアセットには触れない。
            bootstrap.GetType().GetMethod("RegisterAllExecutors", Fields).Invoke(bootstrap, null);
            var executors = (IDictionary)Get(presenter, "_executors");
            foreach (var name in new[] { "Dialogue", "Effect", "Choice", "Wait", "ProgressUpdate", "ComuToggle", "ComuToggleInstant", "DialogueStartButton", "KeywordEnable", "Overlay", "TitleLogo", "ProgressScenario", "CenterPortrait", "SceneTransition", "PortraitInteractable", "PortraitGuidance", "LostNoteCharacter" })
                Assert.That(executors.Contains(name), Is.True, "Executor is missing: " + name);
            foreach (var scenario in AllScenarios())
                foreach (var action in (IList)Get(scenario, "actions"))
                {
                    Assert.That(action, Is.Not.Null, scenario.name);
                    string actionType = (string)action.GetType().GetProperty("ActionType").GetValue(action);
                    Assert.That(executors.Contains(actionType), Is.True, scenario.name + " requires " + actionType);
                }
        });
    }

    [Test]
    public void DatabaseIdsAreUniqueAndAllScenarioActionReferencesExist()
    {
        var database = AssetDatabase.LoadMainAssetAtPath("Assets/ScriptableObjects/ProgressSystemDatabase/ScenarioDataDatabase.asset");
        Assert.That(database, Is.Not.Null);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in (IList)Get(database, "allScenarios"))
        {
            Assert.That(entry as UnityEngine.Object, Is.Not.Null, "Database contains a missing scenario reference.");
            string id = (string)Get(entry, "scenarioId");
            if (string.IsNullOrEmpty(id)) continue; // 直接参照でチェーンするシナリオはID不要。
            Assert.That(ids.Add(id), Is.True, "Duplicate scenario ID: " + id);
        }
        var scenarios = AllScenarios();
        Assert.That(scenarios, Is.Not.Empty);
        foreach (var scenario in scenarios)
        {
            var actions = Get(scenario, "actions") as IList;
            Assert.That(actions, Is.Not.Null, scenario.name);
            for (int i = 0; i < actions.Count; i++)
                Assert.That(actions[i] as UnityEngine.Object, Is.Not.Null, scenario.name + " has a missing action at index " + i);
        }
    }

    private static UnityEngine.Object[] AllScenarios()
        => AssetDatabase.FindAssets("t:ScenarioData", new[] { "Assets" })
            .Select(guid => AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(asset => asset != null && GameType("ScenarioSystem.Model.ScenarioData").IsInstanceOfType(asset))
            .ToArray();
}
