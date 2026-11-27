using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RP0.UI
{
    /// <summary>
    /// uGUI line chart with axis titles, tick labels, gridlines, vertical markers and a legend.
    /// Add it to a GameObject with a RectTransform; the chart fills that rect.
    /// </summary>
    public class LineChart : MonoBehaviour
    {
        private const float TitleSize = 20f;
        private const float YTickWidth = 48f;
        private const float XTickHeight = 18f;
        private const float LegendHeight = 20f;
        private const float PadRight = 14f;
        private const float PadTop = 8f;
        private const float TickFontSize = 12f;
        private const float TitleFontSize = 14f;

        public int XTickTarget = 8;
        public int YTickTarget = 5;
        public double MinXTickStep = 0;
        public double MinYTickStep = 0;
        public bool YIncludeZero = true;
        public Func<double, double, string> XTickFormatter = FormatTick;
        public Func<double, double, string> YTickFormatter = FormatTick;
        public Color TickLabelColor = new Color(0.8f, 0.8f, 0.8f);

        public LineChartGraphic Plot => _plot;

        public string XAxisTitle
        {
            get => _xTitle.text;
            set => _xTitle.text = value;
        }

        public string YAxisTitle
        {
            get => _yTitle.text;
            set => _yTitle.text = value;
        }

        private RectTransform _rt;
        private LineChartGraphic _plot;
        private TextMeshProUGUI _xTitle;
        private TextMeshProUGUI _yTitle;
        private RectTransform _legend;

        private bool _hasXRange;
        private double _xRangeMin, _xRangeMax;

        private readonly List<TextMeshProUGUI> _xTickLabels = new List<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> _yTickLabels = new List<TextMeshProUGUI>();
        private readonly List<LegendEntry> _legendEntries = new List<LegendEntry>();

        private class LegendEntry
        {
            public GameObject GO;
            public Image Swatch;
            public TextMeshProUGUI Text;
        }

        internal void Awake()
        {
            _rt = (RectTransform)transform;

            var plotGO = new GameObject("Plot", typeof(RectTransform));
            plotGO.transform.SetParent(transform, false);
            _plot = plotGO.AddComponent<LineChartGraphic>();
            _plot.raycastTarget = false;

            _xTitle = CreateText("XAxisTitle", transform, TitleFontSize, TextAlignmentOptions.Midline);
            _yTitle = CreateText("YAxisTitle", transform, TitleFontSize, TextAlignmentOptions.Midline);
            _yTitle.rectTransform.localEulerAngles = new Vector3(0, 0, 90);

            var legendGO = new GameObject("Legend", typeof(RectTransform));
            legendGO.transform.SetParent(transform, false);
            _legend = (RectTransform)legendGO.transform;
            var hlg = legendGO.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.spacing = 12;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            UpdateLayout();
        }

        internal void OnRectTransformDimensionsChange()
        {
            // Can be called before Awake has created all the children
            if (_legend != null)
                UpdateLayout();
        }

        public void Clear()
        {
            _plot.SeriesList.Clear();
            _plot.Markers.Clear();
            _hasXRange = false;
        }

        /// <summary>
        /// Fixes the X axis range. Without this the range is taken from the data.
        /// </summary>
        public void SetXRange(double min, double max)
        {
            _hasXRange = true;
            _xRangeMin = min;
            _xRangeMax = max;
        }

        /// <param name="name">Shown in the legend. Null or empty hides the entry.</param>
        public void AddSeries(string name, double[] x, double[] y, Color color, float width = 2f)
        {
            _plot.SeriesList.Add(new LineChartGraphic.Series { Name = name, X = x, Y = y, Color = color, Width = width });
        }

        /// <summary>
        /// Adds a vertical line spanning the full Y range at the given X value.
        /// </summary>
        /// <param name="name">Shown in the legend. Null or empty hides the entry.</param>
        public void AddMarker(string name, double x, Color color, float width = 1.5f)
        {
            _plot.Markers.Add(new LineChartGraphic.Marker { Name = name, X = x, Color = color, Width = width });
        }

        /// <summary>
        /// Recalculates bounds, ticks and legend from the current series and markers.
        /// </summary>
        public void Rebuild()
        {
            CalculateBounds(out double xMin, out double xMax, out double yMin, out double yMax);

            double yStep = Math.Max(NiceStep(yMax - yMin, YTickTarget), MinYTickStep);
            yMin = Math.Floor(yMin / yStep) * yStep;
            yMax = Math.Ceiling(yMax / yStep) * yStep;
            if (yMax <= yMin)
                yMax = yMin + yStep;

            double xStep = Math.Max(NiceStep(xMax - xMin, XTickTarget), MinXTickStep);

            _plot.XMin = xMin;
            _plot.XMax = xMax;
            _plot.YMin = yMin;
            _plot.YMax = yMax;

            FillTicks(_plot.XGrid, xMin, xMax, xStep);
            FillTicks(_plot.YGrid, yMin, yMax, yStep);

            for (int i = 0; i < _plot.XGrid.Count; i++)
            {
                double v = _plot.XGrid[i];
                TextMeshProUGUI label = GetTickLabel(_xTickLabels, i, TextAlignmentOptions.Top);
                label.text = XTickFormatter(v, xStep);
                float nx = (float)((v - xMin) / (xMax - xMin));
                var lrt = label.rectTransform;
                lrt.anchorMin = lrt.anchorMax = new Vector2(nx, 0f);
                lrt.pivot = new Vector2(0.5f, 1f);
                lrt.sizeDelta = new Vector2(60f, XTickHeight);
                lrt.anchoredPosition = new Vector2(0f, -2f);
            }
            HideUnused(_xTickLabels, _plot.XGrid.Count);

            for (int i = 0; i < _plot.YGrid.Count; i++)
            {
                double v = _plot.YGrid[i];
                TextMeshProUGUI label = GetTickLabel(_yTickLabels, i, TextAlignmentOptions.Right);
                label.text = YTickFormatter(v, yStep);
                float ny = (float)((v - yMin) / (yMax - yMin));
                var lrt = label.rectTransform;
                lrt.anchorMin = lrt.anchorMax = new Vector2(0f, ny);
                lrt.pivot = new Vector2(1f, 0.5f);
                lrt.sizeDelta = new Vector2(YTickWidth - 4f, XTickHeight);
                lrt.anchoredPosition = new Vector2(-4f, 0f);
            }
            HideUnused(_yTickLabels, _plot.YGrid.Count);

            RebuildLegend();
            UpdateLayout();
            _plot.SetVerticesDirty();
        }

        /// <summary>
        /// Formats a tick value with a k/M suffix chosen from the tick step so that all labels on an axis use the same unit.
        /// </summary>
        public static string FormatTick(double value, double step)
        {
            double absStep = Math.Abs(step);
            if (Math.Abs(value) < absStep * 1e-6)
                return "0";
            if (absStep >= 1e6)
                return (value / 1e6).ToString("0.##") + "M";
            if (absStep >= 1e3)
                return (value / 1e3).ToString("0.##") + "k";
            return value.ToString("0.##");
        }

        private void CalculateBounds(out double xMin, out double xMax, out double yMin, out double yMax)
        {
            double dxMin = double.PositiveInfinity, dxMax = double.NegativeInfinity;
            double dyMin = double.PositiveInfinity, dyMax = double.NegativeInfinity;
            foreach (var s in _plot.SeriesList)
            {
                int count = Math.Min(s.X.Length, s.Y.Length);
                for (int i = 0; i < count; i++)
                {
                    dxMin = Math.Min(dxMin, s.X[i]);
                    dxMax = Math.Max(dxMax, s.X[i]);
                    dyMin = Math.Min(dyMin, s.Y[i]);
                    dyMax = Math.Max(dyMax, s.Y[i]);
                }
            }

            foreach (LineChartGraphic.Marker m in _plot.Markers)
            {
                dxMin = Math.Min(dxMin, m.X);
                dxMax = Math.Max(dxMax, m.X);
            }

            if (_hasXRange)
            {
                xMin = _xRangeMin;
                xMax = _xRangeMax;
            }
            else
            {
                xMin = double.IsInfinity(dxMin) ? 0 : dxMin;
                xMax = double.IsInfinity(dxMax) ? 1 : dxMax;
            }
            if (xMax <= xMin)
                xMax = xMin + 1;

            yMin = double.IsInfinity(dyMin) ? 0 : dyMin;
            yMax = double.IsInfinity(dyMax) ? 1 : dyMax;
            if (YIncludeZero)
            {
                yMin = Math.Min(yMin, 0);
                yMax = Math.Max(yMax, 0);
            }
            if (yMax <= yMin)
                yMax = yMin + 1;
        }

        private static double NiceStep(double range, int target)
        {
            double raw = range / Math.Max(target, 1);
            if (raw <= 0 || double.IsNaN(raw) || double.IsInfinity(raw))
                return 1;

            double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double norm = raw / mag;
            double nice = norm <= 1 ? 1 : norm <= 2 ? 2 : norm <= 5 ? 5 : 10;
            return nice * mag;
        }

        private static void FillTicks(List<double> ticks, double min, double max, double step)
        {
            ticks.Clear();
            double epsilon = step * 1e-6;
            for (double v = Math.Ceiling((min - epsilon) / step) * step; v <= max + epsilon; v += step)
                ticks.Add(v);
        }

        private void RebuildLegend()
        {
            int idx = 0;
            foreach (LineChartGraphic.Series s in _plot.SeriesList)
            {
                if (!string.IsNullOrEmpty(s.Name))
                    SetLegendEntry(idx++, s.Name, s.Color);
            }

            foreach (LineChartGraphic.Marker m in _plot.Markers)
            {
                // Markers outside the X range are not drawn, so they get no legend entry either
                if (!string.IsNullOrEmpty(m.Name) && m.X >= _plot.XMin && m.X <= _plot.XMax)
                    SetLegendEntry(idx++, m.Name, m.Color);
            }

            for (int i = idx; i < _legendEntries.Count; i++)
                _legendEntries[i].GO.SetActive(false);
            _legend.gameObject.SetActive(idx > 0);
        }

        private void SetLegendEntry(int idx, string name, Color color)
        {
            if (idx >= _legendEntries.Count)
            {
                var go = new GameObject("LegendEntry", typeof(RectTransform));
                go.transform.SetParent(_legend, false);
                var hlg = go.AddComponent<HorizontalLayoutGroup>();
                hlg.childAlignment = TextAnchor.MiddleLeft;
                hlg.spacing = 4;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;

                var swatchGO = new GameObject("Swatch", typeof(RectTransform));
                swatchGO.transform.SetParent(go.transform, false);
                var swatch = swatchGO.AddComponent<Image>();
                swatch.raycastTarget = false;
                var swatchLE = swatchGO.AddComponent<LayoutElement>();
                swatchLE.preferredWidth = 14;
                swatchLE.preferredHeight = 3;

                TextMeshProUGUI text = CreateText("Label", go.transform, TickFontSize, TextAlignmentOptions.MidlineLeft);
                text.color = TickLabelColor;

                _legendEntries.Add(new LegendEntry { GO = go, Swatch = swatch, Text = text });
            }

            LegendEntry entry = _legendEntries[idx];
            entry.GO.SetActive(true);
            entry.Swatch.color = color;
            entry.Text.text = name;
        }

        private void UpdateLayout()
        {
            float legendH = _legend.gameObject.activeSelf ? LegendHeight : 0f;
            float plotLeft = TitleSize + YTickWidth;
            float plotBottom = TitleSize + XTickHeight;
            float plotTop = PadTop + legendH;

            var prt = _plot.rectTransform;
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = new Vector2(plotLeft, plotBottom);
            prt.offsetMax = new Vector2(-PadRight, -plotTop);

            var xrt = _xTitle.rectTransform;
            xrt.anchorMin = Vector2.zero;
            xrt.anchorMax = new Vector2(1f, 0f);
            xrt.pivot = new Vector2(0.5f, 0f);
            xrt.offsetMin = new Vector2(plotLeft, 0f);
            xrt.offsetMax = new Vector2(-PadRight, TitleSize);

            _legend.anchorMin = new Vector2(0f, 1f);
            _legend.anchorMax = Vector2.one;
            _legend.pivot = new Vector2(0.5f, 1f);
            _legend.offsetMin = new Vector2(plotLeft, -legendH);
            _legend.offsetMax = new Vector2(-PadRight, 0f);

            // The Y title is rotated, so its width runs along the plot height
            float plotHeight = Mathf.Max(_rt.rect.height - plotBottom - plotTop, 0f);
            var yrt = _yTitle.rectTransform;
            yrt.anchorMin = yrt.anchorMax = Vector2.zero;
            yrt.pivot = new Vector2(0.5f, 0.5f);
            yrt.sizeDelta = new Vector2(plotHeight, TitleSize);
            yrt.anchoredPosition = new Vector2(TitleSize * 0.5f, plotBottom + plotHeight * 0.5f);
        }

        private TextMeshProUGUI GetTickLabel(List<TextMeshProUGUI> pool, int idx, TextAlignmentOptions alignment)
        {
            if (idx >= pool.Count)
            {
                TextMeshProUGUI text = CreateText("Tick", _plot.transform, TickFontSize, alignment);
                text.color = TickLabelColor;
                pool.Add(text);
            }

            TextMeshProUGUI label = pool[idx];
            label.gameObject.SetActive(true);
            return label;
        }

        private static void HideUnused(List<TextMeshProUGUI> pool, int usedCount)
        {
            for (int i = usedCount; i < pool.Count; i++)
                pool[i].gameObject.SetActive(false);
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, float fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
