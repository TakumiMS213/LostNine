using UnityEngine;
using UnityEngine.UI;

namespace Teichaku.Core
{
    /// <summary>なくしものの背後に描く、外周へ向けて消える光と放射状の光線。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class ResultHaloGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Vector2 radius = rectTransform.rect.size * 0.5f;
            const int segments = 64;
            Color transparent = color;
            transparent.a = 0f;

            AddVertex(vh, Vector2.zero, WithAlpha(0.55f));
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                AddVertex(vh, Vector2.Scale(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), radius), transparent);
                if (i > 0) vh.AddTriangle(0, i, i + 1);
            }

            const int rays = 12;
            for (int i = 0; i < rays; i++)
            {
                float angle = i * Mathf.PI * 2f / rays;
                int start = vh.currentVertCount;
                AddVertex(vh, Vector2.zero, WithAlpha(0.32f));
                AddVertex(vh, Point(angle - 0.11f, radius), transparent);
                AddVertex(vh, Point(angle + 0.11f, radius), transparent);
                vh.AddTriangle(start, start + 1, start + 2);
            }
        }

        private Color WithAlpha(float multiplier)
        {
            Color tint = color;
            tint.a *= multiplier;
            return tint;
        }

        private static Vector2 Point(float angle, Vector2 radius) =>
            Vector2.Scale(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), radius);

        private static void AddVertex(VertexHelper vh, Vector2 position, Color tint)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = tint;
            vh.AddVert(vertex);
        }
    }
}
