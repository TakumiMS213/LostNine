using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class DialogueVisualTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private Scene _scene;
    private Sprite _sprite;
    private Texture2D _texture;
    private readonly Dictionary<FieldInfo, object> _events = new();
    private readonly List<Component> _subscribers = new();
    private readonly List<object> _ownedTweens = new();
    private FieldInfo _dotweenInitialized;
    private object _previousDotweenInitialized;

    private static Type GameType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    private static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
    private static object Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, Flags | BindingFlags.DeclaredOnly).Invoke(target, args);
    private static void Raise(string name, params object[] args) =>
        GameType("ScenarioSystem.Events.ScenarioEventBus").GetMethod(name).Invoke(null, args);
    private void SeekTween(object tween, float time)
    {
        if (!_ownedTweens.Contains(tween)) _ownedTweens.Add(tween);
        Type.GetType("DG.Tweening.TweenExtensions, DOTween", true).GetMethod("Goto")
            .Invoke(null, new[] { tween, (object)time, false });
    }
    private static bool IsTweenActive(object tween) =>
        (bool)Type.GetType("DG.Tweening.TweenExtensions, DOTween", true).GetMethod("IsActive")
            .Invoke(null, new[] { tween });

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
        // DOTween.InitはEditModeでは初期化せず、Killもinitialized=falseなら何もしない。
        // Editorの全Tweenを更新するPreview.Startは使わず、このfixtureでのみ管理を有効化。
        // 手動Gotoで進め、作成したTweenだけを破棄して元の初期化状態へ戻す。
        _dotweenInitialized = Type.GetType("DG.Tweening.DOTween, DOTween", true)
            .GetField("initialized", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        _previousDotweenInitialized = _dotweenInitialized.GetValue(null);
        _dotweenInitialized.SetValue(null, true);
        _scene = EditorSceneManager.NewPreviewScene();
        _texture = new Texture2D(2, 2);
        _sprite = Sprite.Create(_texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
        foreach (var field in GameType("ScenarioSystem.Events.ScenarioEventBus").GetFields(BindingFlags.Static | BindingFlags.NonPublic))
        {
            if (!typeof(Delegate).IsAssignableFrom(field.FieldType)) continue;
            _events.Add(field, field.GetValue(null));
            field.SetValue(null, null);
        }
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            foreach (var component in _subscribers)
                if (component != null) Call(component, "OnDisable");
            var kill = Type.GetType("DG.Tweening.TweenExtensions, DOTween", true).GetMethod("Kill");
            foreach (var tween in _ownedTweens)
                if (IsTweenActive(tween)) kill.Invoke(null, new[] { tween, (object)false });
            EditorSceneManager.ClosePreviewScene(_scene);
            Object.DestroyImmediate(_sprite);
            Object.DestroyImmediate(_texture);
        }
        finally
        {
            _subscribers.Clear();
            _ownedTweens.Clear();
            foreach (var entry in _events) entry.Key.SetValue(null, entry.Value);
            _events.Clear();
            _dotweenInitialized?.SetValue(null, _previousDotweenInitialized);
        }
    }

    private object Dialogue(Sprite portrait = null, string position = "Center", Sprite background = null, string speaker = "Speaker")
    {
        return Activator.CreateInstance(GameType("ScenarioSystem.Events.DialogueEventData"), new object[]
        {
            speaker, "text", portrait, Enum.Parse(GameType("ScenarioSystem.Model.Actions.PortraitPosition"), position),
            .05f, Enum.Parse(GameType("ScenarioSystem.Model.Actions.NameSlideDirection"), "Default"), null, background
        });
    }

    private (Component view, Image image) CreatePortrait()
    {
        var view = Create("ScenarioSystem.View.PortraitView");
        var image = NewObject("Portrait").AddComponent<Image>();
        Set(view, "portraitImage", image);
        foreach (var (field, position) in new[]
        {
            ("portraitLeftAnchor", new Vector2(-400, 60)),
            ("portraitCenterAnchor", new Vector2(0, 60)),
            ("portraitRightAnchor", new Vector2(400, 60))
        })
        {
            var anchor = (RectTransform)NewObject(field).transform;
            anchor.anchoredPosition = position;
            Set(view, field, anchor);
        }
        Subscribe(view);
        return (view, image);
    }

    [Test]
    public void NextPortraitLineKillsTheOldJumpBeforeMovingToTheNewAnchor()
    {
        var (view, image) = CreatePortrait();
        Raise("RaiseDialogueRequested", Dialogue(_sprite, "Left"));
        var oldJump = Get(view, "_jumpTween");
        SeekTween(oldJump, .075f);
        Assert.That(image.rectTransform.anchoredPosition.y, Is.GreaterThan(60f));

        Raise("RaiseDialogueRequested", Dialogue(_sprite, "Right"));
        Assert.That(IsTweenActive(oldJump), Is.False);
        Assert.That(image.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(400, 60)));
        var newJump = Get(view, "_jumpTween");
        SeekTween(newJump, .3f);
        Assert.That(Vector2.Distance(image.rectTransform.anchoredPosition, new Vector2(400, 60)), Is.LessThan(.001f));
    }

    [TestCase("end")]
    [TestCase("disable")]
    [TestCase("close")]
    public void InterruptedPortraitJumpCannotMoveThePortraitAfterCleanup(string interruption)
    {
        var (view, image) = CreatePortrait();
        Raise("RaiseDialogueRequested", Dialogue(_sprite, "Left"));
        var jump = Get(view, "_jumpTween");
        SeekTween(jump, .075f);
        Assert.That(image.rectTransform.anchoredPosition.y, Is.GreaterThan(60f));

        if (interruption == "end") Raise("RaiseScenarioEnded", new object[] { null });
        else if (interruption == "close") Raise("RaiseWindowVisibilityChanged", false);
        else Call(view, "OnDisable");

        Assert.That(IsTweenActive(jump), Is.False);
        Assert.That(Get(view, "_jumpTween"), Is.Null);
        var expected = new Vector2(interruption == "close" ? 0 : -400, 60);
        Assert.That(image.rectTransform.anchoredPosition, Is.EqualTo(expected));
        Call(view, "OnDisable");
        Assert.That(image.rectTransform.anchoredPosition, Is.EqualTo(expected), "Cleanup must not restore an old anchor twice.");
    }

    [Test]
    public void SpeakerLayoutChangesCancelTheSlideToItsOldPosition()
    {
        var view = Create("ScenarioSystem.View.SpeakerNameView");
        var text = NewObject("SpeakerName").AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.rectTransform.anchoredPosition = new Vector2(10, 20);
        Call(view, "Configure", text, true);
        Subscribe(view);
        Raise("RaiseDialogueRequested", Dialogue(speaker: "First"));
        var slide = Get(view, "_slideTween");
        SeekTween(slide, .1f);
        Assert.That(text.rectTransform.anchoredPosition, Is.Not.EqualTo(new Vector2(10, 20)));
        var newPosition = new Vector2(80, 140);
        Call(view, "SetRestingPosition", newPosition);
        Assert.That(IsTweenActive(slide), Is.False);
        Assert.That(text.rectTransform.anchoredPosition, Is.EqualTo(newPosition));

        Raise("RaiseDialogueRequested", Dialogue(speaker: "Second"));
        var newSlide = Get(view, "_slideTween");
        SeekTween(newSlide, .1f);
        Raise("RaiseScenarioEnded", new object[] { null });
        Assert.That(IsTweenActive(newSlide), Is.False);
        Assert.That(text.rectTransform.anchoredPosition, Is.EqualTo(newPosition));
    }

    [Test]
    public void HiddenCenterPortraitKeepsItsEventSubscriptionsAndCanReappear()
    {
        var view = Create("ScenarioSystem.View.CenterPortraitView");
        var image = view.gameObject.AddComponent<Image>();
        Set(view, "portraitImage", image);
        view.gameObject.SetActive(true);
        Subscribe(view);
        Raise("RaiseCenterPortraitChanged", _sprite);
        Assert.That(image.enabled, Is.True);
        Raise("RaiseCenterPortraitChanged", new object[] { null });
        Assert.That(image.enabled, Is.False);
        Assert.That(view.gameObject.activeSelf, Is.True);
        Raise("RaiseCenterPortraitChanged", _sprite);
        Assert.That(image.enabled, Is.True);
        Assert.That(image.sprite, Is.SameAs(_sprite));
    }

    [Test]
    public void WindowCloseHidesCenterPortraitOnlyWhenAnExistingMainPortraitWillOverlap()
    {
        var view = Create("ScenarioSystem.View.CenterPortraitView");
        var image = view.gameObject.AddComponent<Image>();
        Set(view, "portraitImage", image);
        view.gameObject.SetActive(true);
        Subscribe(view);
        Raise("RaiseCenterPortraitChanged", _sprite);
        Raise("RaiseDialogueRequested", Dialogue());
        Raise("RaiseWindowVisibilityChanged", false);
        Assert.That(image.enabled, Is.True, "With no main portrait the center portrait must remain visible.");

        Raise("RaiseDialogueRequested", Dialogue(_sprite, "Left"));
        Assert.That(image.enabled, Is.True);
        Raise("RaiseWindowVisibilityChanged", false);
        Assert.That(image.enabled, Is.False);
        Raise("RaiseDialogueRequested", Dialogue());
        Assert.That(image.enabled, Is.True);
    }

    [TestCase("clear")]
    [TestCase("end")]
    [TestCase("disable")]
    public void RemovingAStillRestoresTheObjectsOriginalVisibility(string removal)
    {
        var view = Create("ScenarioSystem.View.BackgroundStillView");
        var image = NewObject("Still").AddComponent<Image>();
        var desk = NewObject("AlreadyHiddenDesk");
        var film = NewObject("VisibleFilm");
        film.SetActive(true);
        Set(view, "backgroundStillImage", image);
        Set(view, "objectsToHideOnStill", new[] { desk, film });
        Subscribe(view);

        Raise("RaiseDialogueRequested", Dialogue());
        Assert.That(desk.activeSelf, Is.False, "A dialogue with no still must not enable an unrelated hidden object.");
        Raise("RaiseDialogueRequested", Dialogue(background: _sprite));
        Raise("RaiseDialogueRequested", Dialogue(background: _sprite));
        Assert.That(image.gameObject.activeSelf, Is.True);
        Assert.That(desk.activeSelf, Is.False);
        Assert.That(film.activeSelf, Is.False);

        if (removal == "end") Raise("RaiseScenarioEnded", new object[] { null });
        else if (removal == "disable") Call(view, "OnDisable");
        else Raise("RaiseDialogueRequested", Dialogue());
        Assert.That(desk.activeSelf, Is.False);
        Assert.That(film.activeSelf, Is.True);
        Call(view, "OnDisable");
        Assert.That(desk.activeSelf, Is.False);
        Assert.That(film.activeSelf, Is.True);
    }

    [TestCase("ScenarioSystem.View.EffectView")]
    [TestCase("MessageWindowSystem.Core.EffectManager")]
    public void DisablingEffectsRemovesOnlyTheirOwnCameraShakeOffset(string type)
    {
        var view = Create(type);
        var camera = NewObject("CameraTransform").transform;
        var origin = new Vector3(4, 5, -10);
        var offset = new Vector3(.2f, -.4f, 0);
        var externalMovement = new Vector3(10, 20, 0);
        camera.localPosition = origin + offset + externalMovement;
        Set(view, "_shakingCamera", camera);
        Set(view, "_shakeOffset", offset);
        var flash = NewObject("Flash").AddComponent<Image>();
        flash.color = Color.white;
        flash.raycastTarget = true;
        Set(view, "flashOverlay", flash);
        Call(view, "OnDisable");
        Assert.That(Vector3.Distance(camera.localPosition, origin + externalMovement), Is.LessThan(.001f));
        Assert.That(flash.color.a, Is.Zero);
        Assert.That(flash.raycastTarget, Is.False);
        Call(view, "OnDisable");
        Assert.That(Vector3.Distance(camera.localPosition, origin + externalMovement), Is.LessThan(.001f));
    }

    [Test]
    public void RestoringDialogueDisplaysTheWholeLineWithoutTypingOrDuplicateLogEntries()
    {
        var canvas = NewObject("Canvas");
        canvas.AddComponent<Canvas>();
        canvas.SetActive(true);
        var textObject = NewObject("Dialogue");
        textObject.transform.SetParent(canvas.transform, false);
        var text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.rectTransform.sizeDelta = new Vector2(1000, 300);
        textObject.SetActive(true);
        var view = Create("ScenarioSystem.View.DialogueView");
        Set(view, "dialogueText", text);
        Set(view, "_isTyping", true);
        Subscribe(view);
        var log = Create("ScenarioSystem.View.DialogueLogView");
        Subscribe(log);
        var data = Dialogue(speaker: "Original speaker");
        Call(log, "HandleDialogue", data);
        var restored = Call(data, "WithInstantDisplay");
        int typingCompleted = 0;
        GameType("ScenarioSystem.Events.ScenarioEventBus").GetEvent("OnTypingCompleted")
            .AddEventHandler(null, new Action(() => typingCompleted++));

        Raise("RaiseDialogueRequested", restored);
        Assert.That(text.text, Is.EqualTo("text"));
        Assert.That(text.maxVisibleCharacters, Is.EqualTo(int.MaxValue));
        Assert.That(text.textInfo.characterCount, Is.EqualTo(4));
        Assert.That(Get(view, "_isTyping"), Is.False);
        Assert.That(Get(view, "_typingCoroutine"), Is.Null);
        Assert.That(typingCompleted, Is.Zero, "Presenter completes restoration after every subscriber has received the event.");
        Assert.That(((System.Collections.ICollection)Get(log, "_log")).Count, Is.EqualTo(1));
    }

    private (Component handler, TMP_Text text) CreateDiscoveryShake()
    {
        var text = NewObject("ShakingDialogue").AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.rectTransform.anchoredPosition = new Vector2(20, 30);
        text.gameObject.SetActive(true);
        var provider = Create("ScenarioSystem.Adapter.DialogueProviderAdapter");
        Set(provider, "dialogueText", text);
        var handler = Create("MessageWindowSystem.Core.KeywordHandler");
        Set(handler, "_provider", provider);
        Subscribe(handler);
        Call(handler, "ShakeLinkVisual", "key");
        return (handler, text);
    }

    [TestCase("dialogue")]
    [TestCase("end")]
    [TestCase("hide")]
    [TestCase("disable")]
    public void DiscoveryShakeIsStoppedAndItsOffsetRemovedWhenDialogueChanges(string interruption)
    {
        var (handler, text) = CreateDiscoveryShake();
        var shake = Get(handler, "_discoveryShakeTween");
        SeekTween(shake, .025f);
        var origin = new Vector2(20, 30);
        Assert.That(Vector2.Distance(text.rectTransform.anchoredPosition, origin), Is.GreaterThan(.001f));
        if (interruption == "dialogue") Raise("RaiseDialogueRequested", Dialogue());
        else if (interruption == "end") Raise("RaiseScenarioEnded", new object[] { null });
        else if (interruption == "hide") Raise("RaiseWindowVisibilityChanged", false);
        else Call(handler, "OnDisable");
        Assert.That(IsTweenActive(shake), Is.False);
        Assert.That(Get(handler, "_discoveryShakeTween"), Is.Null);
        Assert.That(text.rectTransform.anchoredPosition, Is.EqualTo(origin));
        Call(handler, "OnDisable");
        Assert.That(text.rectTransform.anchoredPosition, Is.EqualTo(origin));
    }

    [Test]
    public void RepeatedDiscoveryShakesDoNotCaptureAnAnimatedPositionAsTheirOrigin()
    {
        var (handler, text) = CreateDiscoveryShake();
        var oldShake = Get(handler, "_discoveryShakeTween");
        SeekTween(oldShake, .025f);
        Call(handler, "ShakeLinkVisual", "second");
        Assert.That(IsTweenActive(oldShake), Is.False);
        Assert.That(text.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(20, 30)));
        var newShake = Get(handler, "_discoveryShakeTween");
        SeekTween(newShake, .025f);
        Call(handler, "OnDisable");
        Assert.That(IsTweenActive(newShake), Is.False);
        Assert.That(text.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(20, 30)));
    }

    [Test]
    public void DiscoveryShakeCleanupToleratesItsTextBeingDestroyedFirst()
    {
        var (handler, text) = CreateDiscoveryShake();
        var shake = Get(handler, "_discoveryShakeTween");
        SeekTween(shake, .025f);
        Object.DestroyImmediate(text.gameObject);
        Assert.DoesNotThrow(() => Call(handler, "OnDisable"));
        Assert.That(IsTweenActive(shake), Is.False);
        Assert.That(Get(handler, "_discoveryShakeTarget"), Is.Null);
    }
}
