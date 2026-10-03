using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ConversationAnimationTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private Scene _scene;
    private HashSet<object> _existingTweens;
    private readonly HashSet<object> _ownedTweens = new();
    private FieldInfo _dotweenInitialized;
    private object _previousDotweenInitialized;
    private static Type TweenType(string name) => Type.GetType("DG.Tweening." + name + ", DOTween", true);
    private static object LinearEase => Enum.Parse(TweenType("Ease"), "Linear");
    private static bool IsTweenActive(object tween) => (bool)TweenType("TweenExtensions")
        .GetMethod("IsActive").Invoke(null, new[] { tween });
    private static void KillTween(object tween) => TweenType("TweenExtensions")
        .GetMethod("Kill").Invoke(null, new[] { tween, (object)false });
    private static IEnumerable<object> PlayingTweens()
    {
        var result = (IEnumerable)TweenType("DOTween").GetMethod("PlayingTweens")
            .Invoke(null, new object[] { null });
        return result == null ? Enumerable.Empty<object>() : result.Cast<object>();
    }
    private static readonly Vector2 Original = new Vector2(-100f, -60f);
    private static readonly Vector2 Target = new Vector2(300f, 20f);
    private static Type GameType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Fields).Invoke(target, args);

    [SetUp]
    public void SetUp()
    {
        // EditModeではDOTween.Initが管理フラグを立てないため、既存テストと同様に
        // フラグだけを一時有効化し、自分が作ったTweenだけを手動更新する。
        _dotweenInitialized = TweenType("DOTween").GetField("initialized", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        _previousDotweenInitialized = _dotweenInitialized.GetValue(null);
        _dotweenInitialized.SetValue(null, true);
        _existingTweens = new HashSet<object>(PlayingTweens());
        _scene = EditorSceneManager.NewPreviewScene();
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            NewTweens();
            foreach (var tween in _ownedTweens)
                if (IsTweenActive(tween)) KillTween(tween);
        }
        finally
        {
            try
            {
                if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            }
            finally
            {
                _ownedTweens.Clear();
                _dotweenInitialized?.SetValue(null, _previousDotweenInitialized);
            }
        }
    }

    private object[] NewTweens()
    {
        var tweens = PlayingTweens().Where(tween => !_existingTweens.Contains(tween)).ToArray();
        foreach (var tween in tweens) _ownedTweens.Add(tween);
        return tweens;
    }

    private void Advance(float seconds)
    {
        // このテストで生成されたTweenだけを進め、開いているシーンの演出は変更しない。
        // 古い実装でCallbackから作られる独立した揺れSequenceも次の更新で検出する。
        var update = TweenType("TweenExtensions").GetMethod("ManualUpdate");
        foreach (var tween in NewTweens())
            update.Invoke(null, new[] { tween, (object)seconds, seconds });
    }

    private Component CreateMover(float duration = 0.6f, float shakeDuration = 0.8f)
    {
        var go = new GameObject("ConversationMotion", typeof(RectTransform), typeof(CanvasGroup));
        SceneManager.MoveGameObjectToScene(go, _scene);
        go.SetActive(false);
        var rect = (RectTransform)go.transform;
        rect.anchoredPosition = Original;
        rect.sizeDelta = new Vector2(120f, 90f);
        var mover = go.AddComponent(GameType("MoveOnClickandReturn"));
        Set(mover, "targetAnchoredPosition", Target);
        Set(mover, "duration", duration);
        Set(mover, "ease", LinearEase);
        Set(mover, "shakeOnComplete", true);
        Set(mover, "shakeDuration", shakeDuration);
        Set(mover, "shakeStrength", 12f);
        Set(mover, "fadeDuration", 0.2f);
        Set(mover, "endAlpha", 0.4f);
        Set(mover, "enableScale", true);
        Set(mover, "targetScale", new Vector3(1.5f, 1.5f, 1f));
        Set(mover, "enableSize", true);
        Set(mover, "targetSizeDelta", new Vector2(320f, 150f));
        Set(mover, "enableRotation", true);
        Set(mover, "targetRotation", new Vector3(0f, 0f, 45f));
        Call(mover, "Awake");
        go.SetActive(true);
        return mover;
    }

    private static void AssertShape(Component mover, bool target)
    {
        var rect = (RectTransform)mover.transform;
        Assert.That(Vector2.Distance(rect.anchoredPosition, target ? Target : Original), Is.LessThan(0.001f));
        Assert.That(Vector2.Distance(rect.sizeDelta, target ? new Vector2(320f, 150f) : new Vector2(120f, 90f)), Is.LessThan(0.001f));
        Assert.That(Vector3.Distance(rect.localScale, target ? new Vector3(1.5f, 1.5f, 1f) : Vector3.one), Is.LessThan(0.001f));
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(rect.localEulerAngles.z, target ? 45f : 0f)), Is.LessThan(0.001f));
    }

    [Test]
    public void ImmediateOriginalDuringMovementCannotBeOverwrittenByOldSequence()
    {
        var mover = CreateMover();
        Call(mover, "Play");
        Advance(0.2f);
        Assert.That(((RectTransform)mover.transform).anchoredPosition, Is.Not.EqualTo(Original));
        Call(mover, "SetToOriginal");
        Advance(1f);
        Advance(1f);
        AssertShape(mover, false);
        Assert.That(NewTweens(), Is.Empty);
    }

    [Test]
    public void ImmediateTargetDuringShakeCancelsMovementFadeAndAllDeformations()
    {
        var mover = CreateMover(0.2f);
        Call(mover, "Play");
        Advance(0.21f);
        Advance(0.05f);
        Call(mover, "SetToTarget");
        Advance(1f);
        Advance(1f);
        AssertShape(mover, true);
        Assert.That(mover.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0.4f).Within(0.001f));
        Assert.That(NewTweens(), Is.Empty);
    }

    [Test]
    public void RepeatedPlayReplacesPreviousAnimationInsteadOfLayeringSequences()
    {
        var mover = CreateMover();
        Call(mover, "Play");
        Advance(0.1f);
        var oldTweens = NewTweens();
        Call(mover, "Play");
        Assert.That(oldTweens.All(tween => !IsTweenActive(tween)), Is.True);
        Assert.That(NewTweens().Length, Is.EqualTo(1), "Position, shake, fade, scale, size and rotation share one owner.");
        Advance(2f);
        AssertShape(mover, false);
    }

    [Test]
    public void DisablingDuringConversationTransitionSettlesItsRequestedShape()
    {
        var mover = CreateMover();
        Call(mover, "Play");
        Advance(0.1f);
        mover.gameObject.SetActive(false);
        // 通常のMonoBehaviourはEditModeでは生命周期が走らないため明示呼び出し。
        Call(mover, "OnDisable");
        Advance(2f);
        mover.gameObject.SetActive(true);
        Advance(2f);
        AssertShape(mover, true);
        Assert.That(NewTweens(), Is.Empty);
    }

    [Test]
    public void DestroyingTheWindowStopsAllOwnedAnimationBeforeAnotherUpdate()
    {
        var mover = CreateMover();
        Call(mover, "Play");
        Advance(0.1f);
        UnityEngine.Object.DestroyImmediate(mover.gameObject);
        Advance(1f);
        Advance(1f);
        Assert.That(NewTweens(), Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void UtilityOwnsShakeWhilePreservingParallelFadeAndCompletionTiming(bool worldSpace)
    {
        var mover = CreateMover();
        var options = Activator.CreateInstance(GameType("Main.UIMoves.MoveWithEasing+MoveOptions"));
        Set(options, "duration", 0.2f);
        Set(options, "ease", LinearEase);
        Set(options, "shakeOnComplete", true);
        Set(options, "shakeDuration", 0.8f);
        Set(options, "shakeStrength", 10f);
        Set(options, "fadeDuration", 0.2f);
        Set(options, "endAlpha", 0.2f);
        int completed = 0;
        object destination = worldSpace ? (object)new Vector3(100f, 10f, 0f) : new Vector2(100f, 10f);
        var sequence = GameType("Main.UIMoves.MoveWithEasing")
            .GetMethod(worldSpace ? "MoveTo" : "MoveToAnchored")
            .Invoke(null, new object[] { mover.gameObject, destination, options, new Action(() => completed++) });
        Advance(0.3f);
        Assert.That(completed, Is.Zero);
        Assert.That(mover.GetComponent<CanvasGroup>().alpha, Is.InRange(0.21f, 0.99f), "Fade must run in parallel with shake.");
        Advance(0.11f);
        Assert.That(completed, Is.EqualTo(1), "Completion callback retains the original move + fade timing.");
        Assert.That(IsTweenActive(sequence), Is.True, "The returned sequence must still own the longer shake.");
        Assert.That(NewTweens().Length, Is.EqualTo(1), "Shake must not escape into an independent sequence.");
        TweenType("DOTween").GetMethod("Kill", new[] { typeof(object), typeof(bool) })
            .Invoke(null, new object[] { mover.transform, false });
        var rect = (RectTransform)mover.transform;
        rect.anchoredPosition = new Vector2(21f, 37f);
        Advance(2f);
        Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(21f, 37f)), "Killing the transform target must stop the parent sequence and shake.");
        Assert.That(NewTweens(), Is.Empty);
    }
}
