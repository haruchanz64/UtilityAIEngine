using System;
using UnityEngine;

namespace UtilityAIWorkbench.Runtime.Core
{
    /// <summary>
    /// Execution status returned by <see cref="UtilityActionBase.OnTick"/> each runner step.
    /// </summary>
    public enum UtilityActionStatus
    {
        /// <summary>Action is still in progress and should continue receiving ticks.</summary>
        Running = 0,
        /// <summary>Action finished normally, prompting the runner to exit the behavior, apply cooldown, and re-evaluate.</summary>
        Completed = 1,
        /// <summary>Action aborted or could not proceed, prompting the runner to exit the behavior, apply cooldown, and re-evaluate.</summary>
        Failed = 2
    }

    /// <summary>
    /// Base class for all Utility AI considerations. Non-abstract, non-generic subclasses are auto-discovered in the Workbench via TypeCache.
    /// </summary>
    [Serializable]
    public abstract class UtilityConsiderationBase
    {
        /// <summary>Optional designer-facing label shown in the Workbench and debugger telemetry.</summary>
        public string Label = string.Empty;
        /// <summary>Response curve transforming the normalized [0..1] sample into a [0..1] utility contribution.</summary>
        public ResponseCurve Curve = ResponseCurve.Linear();

        /// <summary>When true, a curve output at or below 0.0001 vetoes the entire owning behavior.</summary>
        [Tooltip("If true, a curve output <= 0 immediately disqualifies the entire behavior.")]
        public bool CanVeto = true;

        /// <summary>Resolved display label, falling back to the concrete type name when <see cref="Label"/> is blank.</summary>
        public virtual string DisplayName => string.IsNullOrWhiteSpace(Label) ? GetType().Name : Label;

        /// <summary>
        /// Samples a raw input value from <paramref name="context"/> and normalizes it into [0..1] before passing through <see cref="Curve"/>.
        /// </summary>
        /// <param name="context">Current agent evaluation context.</param>
        /// <param name="rawValue">Unnormalized raw input value recorded for debugger telemetry.</param>
        /// <returns>Normalized input value in [0..1].</returns>
        public abstract float SampleNormalizedInput(UtilityContext context, out float rawValue);

        /// <summary>
        /// Samples the consideration, evaluates <see cref="Curve"/>, checks <see cref="CanVeto"/>, and returns a full diagnostic record.
        /// </summary>
        /// <param name="context">Current agent evaluation context.</param>
        /// <returns>A <see cref="ConsiderationScoreRecord"/> containing raw, normalized, and curved outputs plus veto state.</returns>
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
    /// Base class for executable Utility AI actions. Non-abstract, non-generic subclasses are auto-discovered in the Workbench via TypeCache.
    /// </summary>
    [Serializable]
    public abstract class UtilityActionBase
    {
        /// <summary>Optional custom display name shown in the Workbench instead of the concrete class name.</summary>
        public string DisplayNameOverride = string.Empty;
        /// <summary>Resolved display name, falling back to the concrete type name when <see cref="DisplayNameOverride"/> is blank.</summary>
        public virtual string DisplayName => string.IsNullOrWhiteSpace(DisplayNameOverride) ? GetType().Name : DisplayNameOverride;

        /// <summary>
        /// Invoked once when the owning behavior becomes active.
        /// </summary>
        /// <param name="context">Execution context for the active agent.</param>
        public virtual void OnEnter(UtilityContext context) { }

        /// <summary>
        /// Invoked every runner tick while the owning behavior remains active.
        /// </summary>
        /// <param name="context">Execution context with <see cref="UtilityContext.DeltaTime"/> updated for this step.</param>
        /// <returns>Execution status indicating whether to keep running or exit the behavior.</returns>
        public abstract UtilityActionStatus OnTick(UtilityContext context);

        /// <summary>
        /// Invoked once when the owning behavior completes, fails, is preempted, or is stopped.
        /// </summary>
        /// <param name="context">Execution context for the active agent.</param>
        public virtual void OnExit(UtilityContext context) { }
    }

    /// <summary>
    /// Optional component interface allowing custom per-behavior target resolution for a Utility AI runner or agent.
    /// </summary>
    public interface IUtilityTargetProvider
    {
        /// <summary>
        /// Resolves a target <see cref="Transform"/> for the behavior's configured <paramref name="targetKey"/>.
        /// </summary>
        /// <param name="context">Current evaluation or execution context.</param>
        /// <param name="targetKey">Non-empty target lookup key configured on the behavior definition.</param>
        /// <returns>The resolved target transform, or null if no matching target is available.</returns>
        Transform ResolveTarget(UtilityContext context, string targetKey);
    }
}
