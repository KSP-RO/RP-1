using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RP0.UI
{
    /// <summary>
    /// Draws the plot area of a LineChart (background, grid, axes, data lines and vertical markers) as UI mesh geometry.
    /// Geometry is in canvas units so it scales with the UI.
    /// </summary>
    public class LineChartGraphic : MaskableGraphic
    {
        public class Series
        {
            public string Name;
            public double[] X;
            public double[] Y;
            public Color Color;
            public float Width;
        }

        public class Marker
        {
            public string Name;
            public double X;
            public Color Color;
            public float Width;
        }

        public readonly List<Series> SeriesList = new List<Series>();
        public readonly List<Marker> Markers = new List<Marker>();
        public readonly List<double> XGrid = new List<double>();
        public readonly List<double> YGrid = new List<double>();

        public double XMin, XMax, YMin, YMax;

        public Color BackgroundColor = new Color(0f, 0f, 0f, 0.85f);
        public Color GridColor = new Color(1f, 1f, 1f, 0.18f);
        public Color AxisColor = new Color(1f, 1f, 1f, 0.6f);
        public float GridWidth = 1f;
        public float AxisWidth = 1.5f;

        // Limits how far a miter join can stick out on sharp corners, as a multiple of half the line width
        private const float MiterLimit = 2f;

        private readonly List<Vector2> _points = new List<Vector2>();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect r = GetPixelAdjustedRect();
            if (r.width <= 0f || r.height <= 0f || XMax <= XMin || YMax <= YMin)
                return;

            AddQuad(vh, r.min, new Vector2(r.xMin, r.yMax), r.max, new Vector2(r.xMax, r.yMin), BackgroundColor);

            foreach (double x in XGrid)
                AddSegment(vh, ToLocal(r, x, YMin), ToLocal(r, x, YMax), GridWidth, GridColor);
            foreach (double y in YGrid)
                AddSegment(vh, ToLocal(r, XMin, y), ToLocal(r, XMax, y), GridWidth, GridColor);

            foreach (Marker m in Markers)
            {
                if (m.X < XMin || m.X > XMax)
                    continue;
                AddSegment(vh, ToLocal(r, m.X, YMin), ToLocal(r, m.X, YMax), m.Width, m.Color);
            }

            foreach (Series s in SeriesList)
            {
                _points.Clear();
                int count = Mathf.Min(s.X.Length, s.Y.Length);
                for (int i = 0; i < count; i++)
                {
                    Vector2 p = ToLocal(r, s.X[i], s.Y[i]);
                    // Duplicate points would give a zero-length segment with no direction
                    if (_points.Count > 0 && (p - _points[_points.Count - 1]).sqrMagnitude < 0.0001f)
                        continue;
                    _points.Add(p);
                }
                AddPolyline(vh, _points, s.Width, s.Color);
            }

            AddSegment(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), AxisWidth, AxisColor);
            AddSegment(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), AxisWidth, AxisColor);
        }

        private Vector2 ToLocal(Rect r, double x, double y)
        {
            float nx = (float)((x - XMin) / (XMax - XMin));
            float ny = Mathf.Clamp01((float)((y - YMin) / (YMax - YMin)));
            return new Vector2(r.xMin + nx * r.width, r.yMin + ny * r.height);
        }

        private static Vector2 Perpendicular(Vector2 dir) => new Vector2(-dir.y, dir.x);

        private static void AddSegment(VertexHelper vh, Vector2 a, Vector2 b, float width, Color32 color)
        {
            Vector2 dir = b - a;
            if (dir.sqrMagnitude < 0.0001f)
                return;

            Vector2 n = Perpendicular(dir.normalized) * (width * 0.5f);
            AddQuad(vh, a - n, a + n, b + n, b - n, color);
        }

        /// <summary>
        /// Builds a triangle strip with miter joins so that consecutive segments have no gaps or overlaps.
        /// </summary>
        private static void AddPolyline(VertexHelper vh, List<Vector2> pts, float width, Color32 color)
        {
            if (pts.Count < 2)
                return;

            int start = vh.currentVertCount;
            float halfWidth = width * 0.5f;
            int last = pts.Count - 1;
            for (int i = 0; i <= last; i++)
            {
                Vector2 nIn = i > 0 ? Perpendicular((pts[i] - pts[i - 1]).normalized) : Vector2.zero;
                Vector2 nOut = i < last ? Perpendicular((pts[i + 1] - pts[i]).normalized) : Vector2.zero;

                Vector2 n;
                if (i == 0)
                {
                    n = nOut;
                }
                else if (i == last)
                {
                    n = nIn;
                }
                else
                {
                    n = nIn + nOut;
                    if (n.sqrMagnitude < 0.0001f)
                    {
                        n = nIn;
                    }
                    else
                    {
                        n.Normalize();
                        n /= Mathf.Max(Vector2.Dot(n, nIn), 1f / MiterLimit);
                    }
                }

                vh.AddVert(pts[i] + n * halfWidth, color, Vector2.zero);
                vh.AddVert(pts[i] - n * halfWidth, color, Vector2.zero);
            }

            for (int i = 0; i < last; i++)
            {
                int v = start + i * 2;
                vh.AddTriangle(v, v + 2, v + 3);
                vh.AddTriangle(v, v + 3, v + 1);
            }
        }

        private static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 color)
        {
            int start = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero);
            vh.AddVert(b, color, Vector2.zero);
            vh.AddVert(c, color, Vector2.zero);
            vh.AddVert(d, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
