using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Archipelago.Scanner
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CircleSearchDrawer : Graphic
    {
        [SerializeField] private float _lineWidth = 4f;

        private readonly List<Vector2> _points = new(256);

        // ── Public API ────────────────────────────────────────────

        public void AddPoint(Vector2 screenPoint)
        {
            _points.Add(ScreenToLocal(screenPoint));
            SetVerticesDirty();
        }

        public void Clear()
        {
            _points.Clear();
            SetVerticesDirty();
        }

        // ── Graphic override ──────────────────────────────────────

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_points.Count < 2) return;

            for (int i = 1; i < _points.Count; i++)
                AddSegment(vh, _points[i - 1], _points[i]);

            // Close the polygon if the path has enough points
            if (_points.Count >= 3)
                AddSegment(vh, _points[_points.Count - 1], _points[0]);
        }

        // ── Helpers ───────────────────────────────────────────────

        private void AddSegment(VertexHelper vh, Vector2 from, Vector2 to)
        {
            Vector2 dir  = (to - from);
            float   len  = dir.magnitude;
            if (len < 0.001f) return;

            Vector2 perp = new Vector2(-dir.y, dir.x) / len * (_lineWidth * 0.5f);

            int v = vh.currentVertCount;
            var c = this.color;

            vh.AddVert(new Vector3(from.x - perp.x, from.y - perp.y), c, Vector2.zero);
            vh.AddVert(new Vector3(from.x + perp.x, from.y + perp.y), c, Vector2.zero);
            vh.AddVert(new Vector3(to.x   + perp.x, to.y   + perp.y), c, Vector2.zero);
            vh.AddVert(new Vector3(to.x   - perp.x, to.y   - perp.y), c, Vector2.zero);

            vh.AddTriangle(v, v + 1, v + 2);
            vh.AddTriangle(v, v + 2, v + 3);
        }

        private Vector2 ScreenToLocal(Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                screenPoint,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                out Vector2 local);
            return local;
        }
    }
}