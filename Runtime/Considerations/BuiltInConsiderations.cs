using System;
using UnityEngine;
using UtilityAIEngine.Runtime.Core;

namespace UtilityAIEngine.Runtime.Considerations
{
    /// <summary>
    /// Reads a float or int key from the Blackboard and normalizes it from [<see cref="MinValue"/>..<see cref="MaxValue"/>] to [0..1].
    /// </summary>
    [Serializable]
    public sealed class BlackboardFloatConsideration : UtilityConsiderationBase
    {
        /// <summary>Case-insensitive blackboard key storing a float or int value.</summary>
        public string Key = "Health";
        /// <summary>Lower bound mapped to normalized input 0.</summary>
        public float MinValue = 0f;
        /// <summary>Upper bound mapped to normalized input 1.</summary>
        public float MaxValue = 100f;
        /// <summary>Value used when <see cref="Key"/> is absent or no blackboard is provided.</summary>
        public float FallbackValue = 0f;

        /// <inheritdoc/>
        public override string DisplayName => string.IsNullOrWhiteSpace(Label) ? $"Float({Key})" : Label;

        /// <summary>
        /// Reads <see cref="Key"/> and normalizes it over [<see cref="MinValue"/>..<see cref="MaxValue"/>] (falling back to a step check when bounds match).
        /// </summary>
        public override float SampleNormalizedInput(UtilityContext context, out float rawValue)
        {
            rawValue = context != null && context.Blackboard != null
                ? context.Blackboard.GetFloat(Key, FallbackValue)
                : FallbackValue;

            if (Mathf.Approximately(MaxValue, MinValue))
            {
                return rawValue >= MaxValue ? 1f : 0f;
            }

            return Mathf.InverseLerp(MinValue, MaxValue, rawValue);
        }
    }

    /// <summary>
    /// Reads a boolean key from the Blackboard and maps a match against <see cref="ExpectedValue"/> to 1 and a mismatch to 0.
    /// </summary>
    [Serializable]
    public sealed class BlackboardBoolConsideration : UtilityConsiderationBase
    {
        /// <summary>Case-insensitive blackboard boolean key.</summary>
        public string Key = "HasTarget";
        /// <summary>Boolean state that produces a normalized score of 1.</summary>
        public bool ExpectedValue = true;
        /// <summary>Default boolean value used when <see cref="Key"/> is absent.</summary>
        public bool FallbackValue = false;

        /// <inheritdoc/>
        public override string DisplayName => string.IsNullOrWhiteSpace(Label) ? $"Bool({Key}=={ExpectedValue})" : Label;

        /// <summary>
        /// Compares the blackboard boolean against <see cref="ExpectedValue"/> and returns 1 on match or 0 otherwise.
        /// </summary>
        public override float SampleNormalizedInput(UtilityContext context, out float rawValue)
        {
            var actual = context != null && context.Blackboard != null
                ? context.Blackboard.GetBool(Key, FallbackValue)
                : FallbackValue;

            var matched = actual == ExpectedValue;
            rawValue = matched ? 1f : 0f;
            return rawValue;
        }
    }

    /// <summary>
    /// Computes world-space distance between the Agent transform and its current target (or a Blackboard <see cref="Vector3"/> key),
    /// normalized over [<see cref="MinDistance"/>..<see cref="MaxDistance"/>].
    /// </summary>
    [Serializable]
    public sealed class DistanceToTargetConsideration : UtilityConsiderationBase
    {
        /// <summary>Optional blackboard <see cref="Vector3"/> key sampled when <see cref="UtilityContext.Target"/> is null.</summary>
        public string FallbackBlackboardVectorKey = string.Empty;
        /// <summary>Distance in world units mapped to normalized input 0.</summary>
        public float MinDistance = 0f;
        /// <summary>Distance in world units mapped to normalized input 1.</summary>
        public float MaxDistance = 25f;
        /// <summary>Fallback distance reported when either the agent transform or target is unavailable.</summary>
        public float DistanceWhenMissingTarget = 25f;

        /// <inheritdoc/>
        public override string DisplayName => string.IsNullOrWhiteSpace(Label) ? "DistanceToTarget" : Label;

        /// <summary>
        /// Computes distance to the active target or fallback vector and normalizes it across [<see cref="MinDistance"/>..<see cref="MaxDistance"/>].
        /// </summary>
        public override float SampleNormalizedInput(UtilityContext context, out float rawValue)
        {
            if (context == null || !context.AgentTransform)
            {
                rawValue = DistanceWhenMissingTarget;
            }
            else if (context.Target)
            {
                rawValue = Vector3.Distance(context.AgentTransform.position, context.Target.position);
            }
            else if (!string.IsNullOrWhiteSpace(FallbackBlackboardVectorKey) &&
                     context.Blackboard.TryGetVector3(FallbackBlackboardVectorKey, out var targetPos))
            {
                rawValue = Vector3.Distance(context.AgentTransform.position, targetPos);
            }
            else
            {
                rawValue = DistanceWhenMissingTarget;
            }

            if (Mathf.Approximately(MaxDistance, MinDistance))
            {
                return rawValue >= MaxDistance ? 1f : 0f;
            }

            return Mathf.InverseLerp(MinDistance, MaxDistance, rawValue);
        }
    }

    /// <summary>
    /// Evaluates how ready a behavior cooldown is: 0 when remaining cooldown reaches <see cref="ReferenceDuration"/>, and 1 when ready.
    /// </summary>
    [Serializable]
    public sealed class CooldownReadinessConsideration : UtilityConsiderationBase
    {
        /// <summary>Identifier of the behavior whose cooldown is queried on <see cref="UtilityContext"/>.</summary>
        public string BehaviorId = string.Empty;
        /// <summary>Reference cooldown duration in seconds used to normalize remaining time.</summary>
        public float ReferenceDuration = 5f;

        /// <inheritdoc/>
        public override string DisplayName => string.IsNullOrWhiteSpace(Label) ? $"Cooldown({BehaviorId})" : Label;

        /// <summary>
        /// Samples remaining cooldown seconds for <see cref="BehaviorId"/> and returns readiness in [0..1].
        /// </summary>
        public override float SampleNormalizedInput(UtilityContext context, out float rawValue)
        {
            rawValue = context != null ? context.GetCooldownRemaining(BehaviorId) : 0f;
            if (ReferenceDuration <= 0.0001f)
            {
                return rawValue <= 0.0001f ? 1f : 0f;
            }

            return 1f - Mathf.Clamp01(rawValue / ReferenceDuration);
        }
    }

    /// <summary>
    /// Adds uniform random variation in [<see cref="MinOutput"/>..<see cref="MaxOutput"/>] to break ties or introduce variety.
    /// </summary>
    [Serializable]
    public sealed class RandomNoiseConsideration : UtilityConsiderationBase
    {
        /// <summary>Lower bound of the random sample in [0..1].</summary>
        [Range(0f, 1f)] public float MinOutput = 0.7f;
        /// <summary>Upper bound of the random sample in [0..1].</summary>
        [Range(0f, 1f)] public float MaxOutput = 1f;

        /// <inheritdoc/>
        public override string DisplayName => string.IsNullOrWhiteSpace(Label) ? "RandomNoise" : Label;

        /// <summary>
        /// Draws a random float via <see cref="UnityEngine.Random.Range(float, float)"/> between <see cref="MinOutput"/> and <see cref="MaxOutput"/>.
        /// </summary>
        public override float SampleNormalizedInput(UtilityContext context, out float rawValue)
        {
            var lo = Mathf.Min(MinOutput, MaxOutput);
            var hi = Mathf.Max(MinOutput, MaxOutput);
            rawValue = UnityEngine.Random.Range(lo, hi);
            return Mathf.Clamp01(rawValue);
        }
    }

    /// <summary>
    /// Constant baseline score useful for fallback or idle behaviors.
    /// </summary>
    [Serializable]
    public sealed class ConstantConsideration : UtilityConsiderationBase
    {
        /// <summary>Constant normalized value in [0..1] returned on every evaluation.</summary>
        [Range(0f, 1f)] public float Value = 0.5f;

        /// <inheritdoc/>
        public override string DisplayName => string.IsNullOrWhiteSpace(Label) ? $"Constant({Value:F2})" : Label;

        /// <summary>
        /// Returns <see cref="Value"/> clamped to [0..1].
        /// </summary>
        public override float SampleNormalizedInput(UtilityContext context, out float rawValue)
        {
            rawValue = Mathf.Clamp01(Value);
            return rawValue;
        }
    }
}
