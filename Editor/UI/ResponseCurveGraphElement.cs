using UnityEngine;
using UnityEngine.UIElements;
using UtilityAIWorkbench.Runtime.Core;

namespace UtilityAIWorkbench.Editor.UI
{
    /// <summary>
    /// GPU-accelerated UI Toolkit Painter2D graph displaying a [0..1] ResponseCurve with an optional live probe crosshair.
    /// </summary>
    public sealed class ResponseCurveGraphElement : VisualElement
    {
        private const int CurveSamples = 64;

        private readonly VisualElement _canvas;
        private readonly Label _caption;

        private ResponseCurve _curve;
        private float? _probeX;

        public ResponseCurveGraphElement(float height = 165f)
        {
            AddToClassList("uai-curve-container");

            _canvas = new VisualElement();
            _canvas.AddToClassList("uai-curve-canvas");
            _canvas.style.height = height;
            _canvas.generateVisualContent += OnGenerateVisualContent;
            Add(_canvas);

            _caption = new Label("Select a consideration to preview its response curve.");
            _caption.AddToClassList("uai-curve-caption");
            Add(_caption);
        }

        public void SetCurve(ResponseCurve curve, float? probeX = null)
        {
            _curve = curve;
            _probeX = probeX.HasValue ? Mathf.Clamp01(probeX.Value) : (float?)null;

            if (_curve == null)
            {
                _caption.text = "No response curve selected.";
            }
            else if (_probeX.HasValue)
            {
                var y = _curve.Evaluate(_probeX.Value);
                _caption.text = $"{_curve.CurveType}{(_curve.Invert ? " (Inverted)" : string.Empty)}  |  Input X = {_probeX.Value:F2}  ->  Utility Y = {y:F3}";
            }
            else
            {
                _caption.text = $"{_curve.CurveType}{(_curve.Invert ? " (Inverted)" : string.Empty)}  |  Domain [0..1] -> Utility [0..1]";
            }

            _canvas.MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var rect = _canvas.contentRect;
            if (rect.width < 10f || rect.height < 10f) return;

            const float padLeft = 14f;
            const float padRight = 10f;
            const float padTop = 10f;
            const float padBottom = 14f;

            var plotX = padLeft;
            var plotY = padTop;
            var plotW = Mathf.Max(1f, rect.width - padLeft - padRight);
            var plotH = Mathf.Max(1f, rect.height - padTop - padBottom);

            var painter = ctx.painter2D;

            // Gridlines (4x4)
            painter.lineWidth = 1f;
            painter.strokeColor = new Color(0.55f, 0.58f, 0.65f, 0.24f);
            for (var i = 0; i <= 4; i++)
            {
                var t = i / 4f;
                var gx = plotX + plotW * t;
                var gy = plotY + plotH * t;

                painter.BeginPath();
                painter.MoveTo(new Vector2(gx, plotY));
                painter.LineTo(new Vector2(gx, plotY + plotH));
                painter.Stroke();

                painter.BeginPath();
                painter.MoveTo(new Vector2(plotX, gy));
                painter.LineTo(new Vector2(plotX + plotW, gy));
                painter.Stroke();
            }

            if (_curve == null) return;

            // Translucent fill under curve
            painter.fillColor = new Color(0.29f, 0.62f, 1f, 0.16f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(plotX, plotY + plotH));
            for (var i = 0; i <= CurveSamples; i++)
            {
                var nx = i / (float)CurveSamples;
                var ny = _curve.Evaluate(nx);
                painter.LineTo(new Vector2(plotX + nx * plotW, plotY + (1f - ny) * plotH));
            }
            painter.LineTo(new Vector2(plotX + plotW, plotY + plotH));
            painter.ClosePath();
            painter.Fill();

            // Main curve stroke
            painter.lineWidth = 2.4f;
            painter.strokeColor = new Color(0.32f, 0.72f, 1f, 1f);
            painter.BeginPath();
            for (var i = 0; i <= CurveSamples; i++)
            {
                var nx = i / (float)CurveSamples;
                var ny = _curve.Evaluate(nx);
                var pt = new Vector2(plotX + nx * plotW, plotY + (1f - ny) * plotH);
                if (i == 0) painter.MoveTo(pt);
                else painter.LineTo(pt);
            }
            painter.Stroke();

            // Optional probe crosshair and point
            if (_probeX.HasValue)
            {
                var px = _probeX.Value;
                var py = _curve.Evaluate(px);
                var cx = plotX + px * plotW;
                var cy = plotY + (1f - py) * plotH;

                painter.lineWidth = 1.2f;
                painter.strokeColor = new Color(0.26f, 0.85f, 0.54f, 0.7f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(cx, plotY));
                painter.LineTo(new Vector2(cx, plotY + plotH));
                painter.MoveTo(new Vector2(plotX, cy));
                painter.LineTo(new Vector2(plotX + plotW, cy));
                painter.Stroke();

                painter.fillColor = new Color(0.26f, 0.85f, 0.54f, 1f);
                painter.BeginPath();
                painter.Arc(new Vector2(cx, cy), 4.5f, 0f, 360f);
                painter.ClosePath();
                painter.Fill();
            }
        }
    }
}
