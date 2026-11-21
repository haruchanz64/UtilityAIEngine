using System;
using UnityEngine;

namespace UtilityAIWorkbench.Runtime.Core
{
    public enum ResponseCurveType
    {
        Linear = 0,
        Polynomial = 1,
        Logistic = 2,
        Logit = 3,
        Step = 4,
        CustomAnimationCurve = 5
    }

    /// <summary>
    /// Parametric utility response curve mapping a normalized input [0..1] to a utility score [0..1].
    /// Supports standard Mark/Merrill curve shapes plus optional Unity AnimationCurve overrides.
    /// </summary>
    [Serializable]
    public sealed class ResponseCurve
    {
        public ResponseCurveType CurveType = ResponseCurveType.Linear;

        [Tooltip("Slope / steepness (m). Negative values invert Linear and Logistic curves.")]
        public float Slope = 1f;

        [Tooltip("Exponent (k) for Polynomial and Logistic steepness.")]
        public float Exponent = 2f;

        [Tooltip("Horizontal shift / threshold (c) along the X axis.")]
        public float XShift = 0f;

        [Tooltip("Vertical shift (b) along the Y axis.")]
        public float YShift = 0f;

        [Tooltip("Flips the final output (1 - y).")]
        public bool Invert;

        [Tooltip("Used when CurveType is CustomAnimationCurve.")]
        public AnimationCurve CustomCurve;

        public static ResponseCurve Linear(float slope = 1f, float yShift = 0f, bool invert = false) =>
            new ResponseCurve { CurveType = ResponseCurveType.Linear, Slope = slope, YShift = yShift, Invert = invert };

        public static ResponseCurve Polynomial(float exponent = 2f, float slope = 1f, float xShift = 0f, float yShift = 0f, bool invert = false) =>
            new ResponseCurve { CurveType = ResponseCurveType.Polynomial, Exponent = exponent, Slope = slope, XShift = xShift, YShift = yShift, Invert = invert };

        public static ResponseCurve Logistic(float slope = 10f, float midpoint = 0.5f, bool invert = false) =>
            new ResponseCurve { CurveType = ResponseCurveType.Logistic, Slope = slope, XShift = midpoint, Invert = invert };

        public static ResponseCurve Logit(float slope = 0.2f, float xShift = 0.5f, bool invert = false) =>
            new ResponseCurve { CurveType = ResponseCurveType.Logit, Slope = slope, XShift = xShift, Invert = invert };

        public static ResponseCurve Step(float threshold = 0.5f, bool invert = false) =>
            new ResponseCurve { CurveType = ResponseCurveType.Step, XShift = threshold, Invert = invert };

        public float Evaluate(float normalizedX)
        {
            var x = Mathf.Clamp01(float.IsNaN(normalizedX) ? 0f : normalizedX);
            float y;

            switch (CurveType)
            {
                case ResponseCurveType.Linear:
                    y = Slope * (x - XShift) + YShift;
                    break;

                case ResponseCurveType.Polynomial:
                    var shifted = Mathf.Clamp01(x - XShift);
                    var exp = Mathf.Max(0.05f, Exponent);
                    y = Slope * Mathf.Pow(shifted, exp) + YShift;
                    break;

                case ResponseCurveType.Logistic:
                {
                    var steepness = Mathf.Abs(Slope) < 0.001f ? 1f : Slope;
                    var exponentArg = -steepness * (x - XShift);
                    exponentArg = Mathf.Clamp(exponentArg, -40f, 40f);
                    y = (1f / (1f + Mathf.Exp(exponentArg))) + YShift;
                    break;
                }

                case ResponseCurveType.Logit:
                {
                    var clamped = Mathf.Clamp(x, 0.001f, 0.999f);
                    var scale = Mathf.Abs(Slope) < 0.001f ? 0.2f : Slope;
                    y = scale * Mathf.Log(clamped / (1f - clamped)) + XShift + YShift;
                    break;
                }

                case ResponseCurveType.Step:
                    y = x >= XShift ? 1f : 0f;
                    break;

                case ResponseCurveType.CustomAnimationCurve:
                    y = CustomCurve != null ? CustomCurve.Evaluate(x) : x;
                    break;

                default:
                    y = x;
                    break;
            }

            if (float.IsNaN(y) || float.IsInfinity(y))
            {
                y = 0f;
            }

            y = Mathf.Clamp01(y);
            return Invert ? 1f - y : y;
        }

        public ResponseCurve Clone()
        {
            return new ResponseCurve
            {
                CurveType = CurveType,
                Slope = Slope,
                Exponent = Exponent,
                XShift = XShift,
                YShift = YShift,
                Invert = Invert,
                CustomCurve = CustomCurve != null ? new AnimationCurve(CustomCurve.keys) : null
            };
        }
    }
}
