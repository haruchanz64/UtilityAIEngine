using System;
using UnityEngine;
using UtilityAIWorkbench.Runtime.Core;

namespace UtilityAIWorkbench.Runtime.Considerations
{
    /// <summary>
    /// Reads a float or int key from the Blackboard and normalizes it from [MinValue..MaxValue] to [0..1].
    /// </summary>
    [Serializable]
    public sealed class BlackboardFloatConsideration : UtilityConsiderationBase
    {
        public string Key = "Health";
        public float MinValue = 0f;
        public float MaxValue = 100f;
        public float FallbackValue = 0f;

        public override string DisplayName => string.IsNullOrWhiteSpace(Label) ? $"Float({Key})" : Label;

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
    /// Reads a boolean key from the Blackboard and maps true -> 1, false -> 0 (before curve evaluation).
    /// </summary>
    [Serializable]
    public sealed class BlackboardBoolConsideration : UtilityConsiderationBase
    {
        public string Key = "HasTarget";
        public bool ExpectedValue = true;
        public bool FallbackValue = false;

        public override string DisplayName => string.IsNullOrWhiteSpace(Label) ? $"Bool({Key}=={ExpectedValue})" : Label;

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
    /// Computes distance between the Agent transform and its current target (or a Blackboard Vector3 key),
    /// normalized over [MinDistance..MaxDistance].
    /// </summary>
    [Serializable]
    public sealed class DistanceToTargetConsideration : UtilityConsiderationBase
    {
        public string FallbackBlackboardVectorKey = string.Empty;
        public float MinDistance = 0f;
        public float MaxDistance = 25f;
        public float DistanceWhenMissingTarget = 25f;

        public override string DisplayName => string.IsNullOrWhiteSpace(Label) ? "DistanceToTarget" : Label;

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
    /// Evaluates how ready another (or current) behavior cooldown is: 0 when just triggered, 1 when ready.
    /// </summary>
    [Serializable]
    public sealed class CooldownReadinessConsideration : UtilityConsiderationBase
    {
        public string BehaviorId = string.Empty;
        public float ReferenceDuration = 5f;

        public override string DisplayName => string.IsNullOrWhiteSpace(Label) ? $"Cooldown({BehaviorId})" : Label;

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
    /// Adds controlled stochasticity in [MinScore..MaxScore] to break ties or create organic AI variety.
    /// </summary>
    [Serializable]
    public sealed class RandomNoiseConsideration : UtilityConsiderationBase
    {
        [Range(0f, 1f)] public float MinOutput = 0.7f;
        [Range(0f, 1f)] public float MaxOutput = 1f;

        public override string DisplayName => string.IsNullOrWhiteSpace(Label) ? "RandomNoise" : Label;

        public override float SampleNormalizedInput(UtilityContext context, out float rawValue)
        {
            var lo = Mathf.Min(MinOutput, MaxOutput);
            var hi = Mathf.Max(MinOutput, MaxOutput);
            rawValue = UnityEngine.Random.Range(lo, hi);
            return Mathf.Clamp01(rawValue);
        }
    }

    /// <summary>
    /// Constant baseline score useful for fallback/idle behaviors.
    /// </summary>
    [Serializable]
    public sealed class ConstantConsideration : UtilityConsiderationBase
    {
        [Range(0f, 1f)] public float Value = 0.5f;

        public override string DisplayName => string.IsNullOrWhiteSpace(Label) ? $"Constant({Value:F2})" : Label;

        public override float SampleNormalizedInput(UtilityContext context, out float rawValue)
        {
            rawValue = Mathf.Clamp01(Value);
            return rawValue;
        }
    }
}
