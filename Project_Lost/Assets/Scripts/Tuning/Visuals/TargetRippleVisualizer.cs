using Tuning.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Tuning.Visuals
{
    /// <summary>
    /// ターゲットを中心に、0.5秒で消える四角い波紋を3段で繰り返す。
    /// UIメッシュを再利用し、波紋ごとのGameObject生成や破棄を行わない。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class TargetRippleVisualizer : MaskableGraphic
    {
        public const int RippleCount = 3;
        public const float RippleLifetime = 0.5f;

        private const float SpreadDistance = 80f;
        private const float LineWidth = 3f;
        private const float StartAlpha = 0.85f;

        private TuningManager _owner;
        private RectTransform _target;
        private float _startSize;
        private float _elapsed;
        private bool _isPlaying;

        public void Configure(TuningManager owner, RectTransform target, float targetDiameter, Color tint)
        {
            _owner = owner;
            _target = target;
            _startSize = Mathf.Max(LineWidth * 2f, targetDiameter);
            color = new Color(tint.r, tint.g, tint.b, StartAlpha);
            raycastTarget = false;
            _elapsed = 0f;
            _isPlaying = false;

            // 元のターゲットの透明度や点滅に巻き込まれないよう、同じ親の下へ配置。
            rectTransform.SetParent(target.parent, false);
            rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            float outerSize = _startSize + SpreadDistance * 2f;
            rectTransform.sizeDelta = new Vector2(outerSize, outerSize);
            rectTransform.SetSiblingIndex(target.GetSiblingIndex());
            FollowTarget();
            SetVerticesDirty();
        }

        private void LateUpdate()
        {
            bool shouldPlay = _owner != null && _owner.isActiveAndEnabled && _owner.IsActive
                && _target != null && _target.gameObject.activeInHierarchy;
            if (!shouldPlay)
            {
                if (_isPlaying)
                {
                    _isPlaying = false;
                    _elapsed = 0f;
                    SetVerticesDirty();
                }
                return;
            }

            _elapsed = _isPlaying ? _elapsed + Time.deltaTime : 0f;
            _isPlaying = true;
            FollowTarget();
            SetVerticesDirty();
        }

        private void FollowTarget()
        {
            // 判定に使われるターゲット原点に追従。中央配置・移動ターゲットにも対応する。
            // 同じ親のローカル座標を使い、Canvasのレイアウト更新前でも位置を保つ。
            rectTransform.localPosition = _target.localPosition;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;
        }

        protected override void OnDisable()
        {
            _isPlaying = false;
            _elapsed = 0f;
            base.OnDisable();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!_isPlaying) return;

            float interval = RippleLifetime / RippleCount;
            for (int i = 0; i < RippleCount; i++)
            {
                float age = _elapsed - i * interval;
                if (age < 0f) continue;

                // 3つの波紋を時間差で発生させ、各波紋を0.5秒ごとに再利用する。
                float progress = Mathf.Repeat(age, RippleLifetime) / RippleLifetime;
                float halfSize = _startSize * 0.5f + SpreadDistance * progress;
                Color tint = color;
                tint.a *= 1f - progress;
                AddSquare(vh, halfSize, tint);
            }
        }

        private static void AddSquare(VertexHelper vh, float outer, Color tint)
        {
            float inner = Mathf.Max(0f, outer - LineWidth);
            // 角が重なって濃くならないよう、上下の辺だけを角まで伸ばす。
            AddQuad(vh, -outer, inner, outer, outer, tint);
            AddQuad(vh, -outer, -outer, outer, -inner, tint);
            AddQuad(vh, -outer, -inner, -inner, inner, tint);
            AddQuad(vh, inner, -inner, outer, inner, tint);
        }

        private static void AddQuad(VertexHelper vh, float minX, float minY, float maxX, float maxY, Color tint)
        {
            int start = vh.currentVertCount;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = tint;
            vertex.position = new Vector3(minX, minY);
            vh.AddVert(vertex);
            vertex.position = new Vector3(minX, maxY);
            vh.AddVert(vertex);
            vertex.position = new Vector3(maxX, maxY);
            vh.AddVert(vertex);
            vertex.position = new Vector3(maxX, minY);
            vh.AddVert(vertex);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
