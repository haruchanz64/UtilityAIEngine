using System;
using UnityEngine;

namespace UtilityAIWorkbench.Runtime.Core
{
    /// <summary>
    /// Supported curve shapes evaluated by <see cref="ResponseCurve"/>.
    /// </summary>
    public enum ResponseCurveType
    {
        /// <summary>Linear ramp scaled by <see cref="ResponseCurve.Slope"/> and shifted by <see cref="ResponseCurve.XShift"/> and <see cref="ResponseCurve.YShift"/>.</summary>
        Linear = 0,
        /// <summary>Power curve <c>Slope * clamp01(x - XShift)^Exponent + YShift</c>.</summary>
        Polynomial = 1,
        /// <summary>S-shaped logistic sigmoid centered at <see cref="ResponseCurve.XShift"/>.</summary>
        Logistic = 2,
        /// <summary>Inverse-sigmoid logit curve compressing mid-range inputs and amplifying extremes.</summary>
        Logit = 3,
        /// <summary>Binary threshold step returning 1 when input is at or above <see cref="ResponseCurve.XShift"/>, otherwise 0.</summary>
        Step = 4,
        /// <summary>Evaluates an author-defined Unity <see cref="AnimationCurve"/> over [0..1].</summary>
        CustomAnimationCurve = 5
    }

    /// <summary>
    /// Parametric utility response curve mapping a normalized input [0..1] to a utility score [0..1].
    /// Supports standard Mark/Merrill curve shapes plus optional Unity <see cref="AnimationCurve"/> overrides.
    /// </summary>
    [Serializable]
    public sealed class ResponseCurve
    {
        /// <summary>Selected mathematical shape used by <see cref="Evaluate"/>.</summary>
        public ResponseCurveType CurveType = ResponseCurveType.Linear;

        /// <summary>Slope or steepness factor (m). Negative values invert Linear and Logistic curves.</summary>
        [Tooltip("Slope / steepness (m). Negative values invert Linear and Logistic curves.")]
        public float Slope = 1f;

        /// <summary>Exponent (k) applied by <see cref="ResponseCurveType.Polynomial"/>; clamped to at least 0.05 at evaluation time.</summary>
        [Tooltip("Exponent (k) for Polynomial and Logistic steepness.")]
        public float Exponent = 2f;

        /// <summary>Horizontal shift, midpoint, or step threshold (c) along the normalized X axis.</summary>
        [Tooltip("Horizontal shift / threshold (c) along the X axis.")]
        public float XShift = 0f;

        /// <summary>Vertical offset (b) added along the Y axis before [0..1] clamping.</summary>
        [Tooltip("Vertical shift (b) along the Y axis.")]
        public float YShift = 0f;

        /// <summary>When true, flips the clamped output via <c>1 - y</c>.</summary>
        [Tooltip("Flips the final output (1 - y).")]
        public bool Invert;

        /// <summary>Unity <see cref="AnimationCurve"/> sampled when <see cref="CurveType"/> is <see cref="ResponseCurveType.CustomAnimationCurve"/>.</summary>
        [Tooltip("Used when CurveType is CustomAnimationCurve.")]
        public AnimationCurve CustomCurve;

        /// <summary>
        /// Creates a linear response curve with the given slope, vertical shift, and inversion flag.
        /// </summary>
        /// <param name="slope">Slope multiplier applied to the input.</param>
        /// <param name="yShift">Vertical offset added before clamping.</param>
        /// <param name="invert">If true, returns <c>1 - y</c>.</param>
        /// <returns>A configured <see cref="ResponseCurve"/> instance.</returns>
        public static ResponseCurve Linear(float slope = 1f, float yShift = 0f, bool invert = false) =>
            new ResponseCurve { CurveType = ResponseCurveType.Linear, Slope = slope, YShift = yShift, Invert = invert };

        /// <summary>
        /// Creates a polynomial response curve raised to <paramref name="exponent"/>.
        /// </summary>
        /// <param name="exponent">Power exponent (values below 0.05 are clamped during evaluation).</param>
        /// <param name="slope">Multiplier applied to the powered term.</param>
        /// <param name="xShift">Horizontal shift subtracted from input before exponentiation.</param>
        /// <param name="yShift">Vertical offset added before clamping.</param>
        /// <param name="invert">If true, returns <c>1 - y</c>.</param>
        /// <returns>A configured <see cref="ResponseCurve"/> instance.</returns>
        public static ResponseCurve Polynomial(float exponent = 2f, float slope = 1f, float xShift = 0f, float yShift = 0f, bool invert = false) =>
            new ResponseCurve { CurveType = ResponseCurveType.Polynomial, Exponent = exponent, Slope = slope, XShift = xShift, YShift = yShift, Invert = invert };

        /// <summary>
        /// Creates a logistic S-curve centered at <paramref name="midpoint"/>.
        /// </summary>
        /// <param name="slope">Steepness of the transition; negative values produce a descending sigmoid.</param>
        /// <param name="midpoint">Normalized X coordinate where output equals 0.5 (when <see cref="YShift"/> is 0).</param>
        /// <param name="invert">If true, returns <c>1 - y</c>.</param>
        /// <returns>A configured <see cref="ResponseCurve"/> instance.</returns>
        public static ResponseCurve Logistic(float slope = 10f, float midpoint = 0.5f, bool invert = false) =>
            new ResponseCurve { CurveType = ResponseCurveType.Logistic, Slope = slope, XShift = midpoint, Invert = invert };

        /// <summary>
        /// Creates a logit (inverse-sigmoid) response curve.
        /// </summary>
        /// <param name="slope">Log scale factor (falls back to 0.2 if near zero).</param>
        /// <param name="xShift">Baseline offset added to the scaled log-odds output.</param>
        /// <param name="invert">If true, returns <c>1 - y</c>.</param>
        /// <returns>A configured <see cref="ResponseCurve"/> instance.</returns>
        public static ResponseCurve Logit(float slope = 0.2f, float xShift = 0.5f, bool invert = false) =>
            new ResponseCurve { CurveType = ResponseCurveType.Logit, Slope = slope, XShift = xShift, Invert = invert };

        /// <summary>
        /// Creates a step curve that outputs 1 at or above <paramref name="threshold"/> and 0 below it.
        /// </summary>
        /// <param name="threshold">Normalized X threshold in [0..1].</param>
        /// <param name="invert">If true, returns <c>1 - y</c>.</param>
        /// <returns>A configured <see cref="ResponseCurve"/> instance.</returns>
        public static ResponseCurve Step(float threshold = 0.5f, bool invert = false) =>
            new ResponseCurve { CurveType = ResponseCurveType.Step, XShift = threshold, Invert = invert };

        /// <summary>
        /// Evaluates the curve for a normalized input in [0..1], clamping NaN/Infinity inputs and outputs to [0..1].
        /// </summary>
        /// <param name="normalizedX">Input value in [0..1]; NaN is treated as 0 and out-of-range values are clamped.</param>
        /// <returns>Utility score in [0..1], inverted if <see cref="Invert"/> is true.</returns>
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
                    // Clamp exponent input to prevent Mathf.Exp overflow to Infinity.
                    exponentArg = Mathf.Clamp(exponentArg, -40f, 40f);
                    y = (1f / (1f + Mathf.Exp(exponentArg))) + YShift;
                    break;
                }

                case ResponseCurveType.Logit:
                {
                    // Keep input strictly inside (0, 1) to avoid log(0) and division by zero.
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

        /// <summary>
        /// Creates a deep copy of this curve, including duplicating <see cref="CustomCurve"/> keyframes if present.
        /// </summary>
        /// <returns>An independent copy of this <see cref="ResponseCurve"/>.</returns>
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
