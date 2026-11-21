using System;
using UnityEngine;

namespace UtilityAIWorkbench.Runtime.Core
{
    public enum UtilityActionStatus
    {
        Running = 0,
        Completed = 1,
        Failed = 2
    }

    /// <summary>
    /// Base class for all Utility AI considerations. Subclasses are auto-discovered in the Workbench via TypeCache.
    /// </summary>
    [Serializable]
    public abstract class UtilityConsiderationBase
    {
        public string Label = string.Empty;
        public ResponseCurve Curve = ResponseCurve.Linear();

        [Tooltip("If true, a curve output <= 0 immediately disqualifies the entire behavior.")]
        public bool CanVeto = true;

        public virtual string DisplayName => string.IsNullOrWhiteSpace(Label) ? GetType().Name : Label;

        /// <summary>
        /// Samples a raw input value and normalizes it into [0..1] before passing through <see cref="Curve"/>.
        /// </summary>
        public abstract float SampleNormalizedInput(UtilityContext context, out float rawValue);

        public ConsiderationScoreRecord EvaluateDetailed(UtilityContext context)
        {
            var normalized = Mathf.Clamp01(SampleNormalizedInput(context, out var rawValue));
            var curve = Curve ?? ResponseCurve.Linear();
            var score = curve.Evaluate(normalized);
            var vetoed = CanVeto && score <= 0.0001f;
            return new ConsiderationScoreRecord(DisplayName, rawValue, normalized, score, vetoed, curve.CurveType);
        }
    }

    /// <summary>
    /// Base class for executable Utility AI actions. Subclasses are auto-discovered in the Workbench via TypeCache.
    /// </summary>
    [Serializable]
    public abstract class UtilityActionBase
    {
        public string DisplayNameOverride = string.Empty;
        public virtual string DisplayName => string.IsNullOrWhiteSpace(DisplayNameOverride) ? GetType().Name : DisplayNameOverride;

        public virtual void OnEnter(UtilityContext context) { }

        public abstract UtilityActionStatus OnTick(UtilityContext context);

        public virtual void OnExit(UtilityContext context) { }
    }

    /// <summary>
    /// Optional component interface allowing custom target selection for a UtilityAIAgent.
    /// </summary>
    public interface IUtilityTargetProvider
    {
        Transform ResolveTarget(UtilityContext context, string targetKey);
    }
}
