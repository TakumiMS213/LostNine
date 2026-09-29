using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Tuning.Visuals
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class WaveformVisualizer : MaskableGraphic
    {
        [Header("Wave Settings")]
        [Tooltip("波の周波数")]
        [SerializeField] private float frequency = 10f;

        [Tooltip("波の振幅")]
        [SerializeField] private float amplitude = 50f;

        [Tooltip("波の移動速度")]
        [SerializeField] private float scrollSpeed = 5f;

        [Tooltip("線の太さ")]
        [SerializeField] private float thickness = 2f;

        [Tooltip("位相オフセット")]
        [SerializeField] private float phaseOffset = 0f;

        [SerializeField] private int resolution = 100;

        private float _offset;
        private WaveformVisualizer _scrollReference;

        public float Frequency
        {
            get => frequency;
            set => frequency = value;
        }

        public float Amplitude
        {
            get => amplitude;
            set => amplitude = value;
        }

        public float Thickness
        {
            get => thickness;
            set => thickness = value;
        }

        public float ScrollSpeed
        {
            get => scrollSpeed;
            set => scrollSpeed = value;
        }

        public float PhaseOffset
        {
            get => phaseOffset;
            set => phaseOffset = value;
        }

        public void ResetWave()
        {
            _offset = 0f;
            SetVerticesDirty();
        }

        /// <summary>
        /// 2本の波形を完全に重ねるため、スクロール位置を基準波形へ合わせる。
        /// </summary>
        public void MatchScrollOffset(WaveformVisualizer reference)
        {
            if (reference == null) return;
            _offset = reference._offset;
            SetVerticesDirty();
        }

        public void FollowScrollOffset(WaveformVisualizer reference)
        {
            _scrollReference = reference;
            MatchScrollOffset(reference);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
        }

        private void Update()
        {
            if (Application.isPlaying && !canvasRenderer.cull)
            {
                _offset += Time.deltaTime * scrollSpeed;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            float width = rectTransform.rect.width;
            float height = rectTransform.rect.height;
            float startX = -width / 2;

            int segments = Mathf.Max(2, resolution);
            float step = width / (segments - 1);

            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;

            // 隣り合う線分の共有点は一度だけ計算する。
            Vector2 previousPoint = GetWavePoint(0, segments, startX, step);
            for (int i = 0; i < segments - 1; i++)
            {
                Vector2 nextPoint = GetWavePoint(i + 1, segments, startX, step);
                AddSegment(vh, previousPoint, nextPoint, thickness, vertex);
                previousPoint = nextPoint;
            }
        }

        private Vector2 GetWavePoint(int index, int segments, float startX, float step)
        {
            float x = startX + index * step;
            float normalizedX = (float)index / (segments - 1);
            float scrollOffset = _scrollReference != null ? _scrollReference._offset : _offset;
            float y = Mathf.Sin((normalizedX * frequency) + scrollOffset + phaseOffset) * amplitude;
            return new Vector2(x, y);
        }

        private void AddSegment(VertexHelper vh, Vector2 p1, Vector2 p2, float width, UIVertex v)
        {
            Vector2 dir = (p2 - p1).normalized;
            Vector2 normal = new Vector2(-dir.y, dir.x) * width * 0.5f;

            v.position = p1 - normal;
            int idx1 = vh.currentVertCount;
            vh.AddVert(v);

            v.position = p1 + normal;
            vh.AddVert(v);

            v.position = p2 + normal;
            vh.AddVert(v);

            v.position = p2 - normal;
            vh.AddVert(v);

            vh.AddTriangle(idx1, idx1 + 1, idx1 + 2);
            vh.AddTriangle(idx1 + 2, idx1 + 3, idx1);
        }
    }
}
