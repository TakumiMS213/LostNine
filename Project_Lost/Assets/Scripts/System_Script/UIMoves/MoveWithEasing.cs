using System;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using TMPro;

namespace Main.UIMoves
{
    /// <summary>
    /// 汎用的な移動（イージング）ユーティリティ。
    /// - 引数で座標を設定
    /// - 移動終了時に左右に揺れる（on/off切替可能）
    /// - 移動後に透明度を引数で設定
    /// 使用例:
    /// MoveWithEasing.MoveTo(gameObject, new Vector3(0,0,0), new MoveWithEasing.MoveOptions{ duration = 0.6f, shakeOnComplete = true, endAlpha = 0.8f });
    /// </summary>
    public static class MoveWithEasing // これ単体では MonoBehaviour を継承しない
    {
        public class MoveOptions
        {
            public float duration = 0.5f;
            public Ease ease = Ease.OutCubic;
            public bool shakeOnComplete = true;
            public float shakeStrength = 10f; // world units (x方向)
            public float shakeDuration = 0.35f;
            public float endAlpha = 1f; // 移動後の透明度
            public float fadeDuration = 0.25f; // 透明度変化の所要時間
        }

        /// <summary>
        /// 指定オブジェクトをワールド座標に移動させる（DOTween使用）。
        /// - RectTransform の UI でもワールド座標で渡してください。
        /// - 戻り値は作成した Sequence（チェーン）です。
        /// </summary>
        public static Sequence MoveTo(GameObject obj, Vector3 to, MoveOptions options = null, Action onComplete = null)
        {
            if (obj == null) return null;
            options ??= new MoveOptions();

            var trans = obj.transform;

            var seq = DOTween.Sequence().SetTarget(trans).SetLink(obj, LinkBehaviour.KillOnDisable);
            seq.Append(trans.DOMove(to, options.duration).SetEase(options.ease));
            AppendFade(seq, obj, options);
            float completionTime = seq.Duration();

            if (options.shakeOnComplete)
            {
                float strength = options.shakeStrength;
                float shakeDuration = Mathf.Max(0.01f, options.shakeDuration);
                var shake = DOTween.Sequence();
                shake.Append(trans.DOMoveX(to.x + strength, shakeDuration * 0.25f).SetEase(Ease.InOutSine));
                shake.Append(trans.DOMoveX(to.x - strength, shakeDuration * 0.5f).SetEase(Ease.InOutSine));
                shake.Append(trans.DOMoveX(to.x, shakeDuration * 0.25f).SetEase(Ease.InOutSine));
                seq.Insert(Mathf.Max(0f, options.duration), shake);
            }

            // 従来通り、移動後の揺れとフェードは同時に始まる。
            // 完了通知の時刻も従来の移動＋フェード終了時点を維持する。
            if (onComplete != null)
                seq.InsertCallback(completionTime, () => onComplete());

            return seq;
        }

        /// <summary>
        /// RectTransform の `anchoredPosition` を移動させるメソッド。
        /// - UI 用（RectTransform）向けの便利メソッドです。
        /// - RectTransform が見つからない場合は通常のワールド座標移動にフォールバックします。
        /// </summary>
        public static Sequence MoveToAnchored(GameObject obj, Vector2 toAnchored, MoveOptions options = null, Action onComplete = null)
        {
            if (obj == null) return null;
            options ??= new MoveOptions();

            var rt = obj.GetComponent<RectTransform>();
            if (rt == null)
            {
                // RectTransform がない場合はワールド座標にフォールバック
                var worldTo = new Vector3(toAnchored.x, toAnchored.y, obj.transform.position.z);
                return MoveTo(obj, worldTo, options, onComplete);
            }

            var seq = DOTween.Sequence().SetTarget(rt).SetLink(obj, LinkBehaviour.KillOnDisable);
            seq.Append(rt.DOAnchorPos(toAnchored, options.duration).SetEase(options.ease));
            AppendFade(seq, obj, options);
            float completionTime = seq.Duration();

            if (options.shakeOnComplete)
            {
                float strength = options.shakeStrength;
                float shakeDuration = Mathf.Max(0.01f, options.shakeDuration);
                var shake = DOTween.Sequence();
                shake.Append(rt.DOAnchorPosX(toAnchored.x + strength, shakeDuration * 0.25f).SetEase(Ease.InOutSine));
                shake.Append(rt.DOAnchorPosX(toAnchored.x - strength, shakeDuration * 0.5f).SetEase(Ease.InOutSine));
                shake.Append(rt.DOAnchorPosX(toAnchored.x, shakeDuration * 0.25f).SetEase(Ease.InOutSine));
                seq.Insert(Mathf.Max(0f, options.duration), shake);
            }

            if (onComplete != null)
                seq.InsertCallback(completionTime, () => onComplete());
            return seq;
        }

        /// <summary>移動後のフェードを追加する。対象の優先順位と子 Sequence の構造を維持する。</summary>
        private static void AppendFade(Sequence seq, GameObject obj, MoveOptions options)
        {
            var cg = obj.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                seq.Append(cg.DOFade(options.endAlpha, options.fadeDuration));
            }
            else
            {
                var graphics = obj.GetComponentsInChildren<Graphic>(true);
                if (graphics != null && graphics.Length > 0)
                {
                    // 同時に複数の Graphic をフェードさせる
                    var fadeSeq = DOTween.Sequence();
                    foreach (var g in graphics)
                    {
                        fadeSeq.Join(g.DOFade(options.endAlpha, options.fadeDuration));
                    }
                    seq.Append(fadeSeq);
                }
                else
                {
                    var tmpros = obj.GetComponentsInChildren<TextMeshProUGUI>(true);
                    if (tmpros != null && tmpros.Length > 0)
                    {
                        var fadeSeq = DOTween.Sequence();
                        foreach (var t in tmpros)
                        {
                            fadeSeq.Join(t.DOFade(options.endAlpha, options.fadeDuration));
                        }
                        seq.Append(fadeSeq);
                    }
                    else
                    {
                        var srs = obj.GetComponentsInChildren<SpriteRenderer>(true);
                        if (srs != null && srs.Length > 0)
                        {
                            var fadeSeq = DOTween.Sequence();
                            foreach (var sr in srs)
                            {
                                fadeSeq.Join(sr.DOFade(options.endAlpha, options.fadeDuration));
                            }
                            seq.Append(fadeSeq);
                        }
                    }
                }
            }
        }

        // 3Dアンカーポジションは2Dプロジェクトの想定では不要のため削除しました。

        /// <summary>
        /// ターゲットの RectTransform へ移動するユーティリティ。
        /// - 同じ親であれば単純に `target.anchoredPosition` を使う。
        /// - 親が異なる場合はターゲットのワールド位置を基準に、移動元の親空間でのアンカーポジションを計算して移動する。
        /// </summary>
        public static Sequence MoveToRectTransform(GameObject obj, RectTransform target, MoveOptions options = null, Action onComplete = null)
        {
            if (obj == null || target == null) return null;
            options ??= new MoveOptions();

            var rt = obj.GetComponent<RectTransform>();
            if (rt == null)
            {
                // RectTransform を持たない場合はワールド座標で target の位置へ移動
                return MoveTo(obj, target.position, options, onComplete);
            }

            Vector3 finalAnchored;
            if (rt.parent == target.parent)
            {
                finalAnchored = target.anchoredPosition;
                return MoveToAnchored(obj, finalAnchored, options, onComplete);
            }
            else
            {
                // 異なる親の場合、target のワールド位置を rt.parent のローカル座標に変換して anchoredPosition を算出
                var parentTransform = rt.parent as RectTransform;
                if (parentTransform == null)
                {
                    // 親が RectTransform でない場合は world へフォールバック
                    return MoveTo(obj, target.position, options, onComplete);
                }

                Vector3 worldPos = target.position;
                Vector3 localPos = parentTransform.InverseTransformPoint(worldPos);
                finalAnchored = new Vector2(localPos.x, localPos.y);
                return MoveToAnchored(obj, finalAnchored, options, onComplete);
            }
        }
    }
}
