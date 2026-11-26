using System;
using System.Collections.Generic;
using UnityEngine;
using UtilityAIEngine.Runtime.Core;

namespace UtilityAIEngine.Runtime.Data
{
    /// <summary>
    /// Strategy used to reduce multiple consideration curve outputs into a single [0..1] base utility score.
    /// </summary>
    public enum ScoreAggregationMode
    {
        /// <summary>Product of curve outputs boosted by Dave Mark's compensation factor so behaviors with more considerations are not unfairly penalized.</summary>
        CompensatedProduct = 0,
        /// <summary>Unadjusted multiplicative product of all consideration curve outputs.</summary>
        RawProduct = 1,
        /// <summary>Arithmetic mean of all consideration curve outputs.</summary>
        Average = 2,
        /// <summary>Lowest curve output among all considerations (weakest-link bottleneck).</summary>
        Minimum = 3,
        /// <summary>Highest curve output among all considerations.</summary>
        Maximum = 4
    }

    /// <summary>
    /// Defines a single selectable AI behavior: its weight, cooldown, commitment lock, considerations, and executable action.
    /// </summary>
    [Serializable]
    public sealed class UtilityBehaviorDefinition
    {
        /// <summary>Case-insensitive unique identifier used for cooldown tracking, lookups, and decision history.</summary>
        public string Id = "behavior_1";
        /// <summary>Human-readable label shown in the Workbench and debugger.</summary>
        public string DisplayName = "New Behavior";
        /// <summary>When false, the scorer skips evaluation and marks the behavior as disabled.</summary>
        public bool Enabled = true;

        /// <summary>Multiplier applied to the aggregated [0..1] base utility score.</summary>
        [Min(0f)]
        [Tooltip("Multiplier applied after consideration aggregation.")]
        public float Weight = 1f;

        /// <summary>Cooldown duration in seconds applied whenever this behavior exits or finishes.</summary>
        [Min(0f)]
        [Tooltip("Cooldown seconds applied when this behavior exits or completes.")]
        public float CooldownSeconds = 0f;

        /// <summary>Minimum duration in seconds this behavior remains locked against preemption unless its action completes or fails.</summary>
        [Min(0f)]
        [Tooltip("Minimum seconds this behavior stays active before another behavior can preempt it (unless it completes/fails).")]
        public float MinCommitmentSeconds = 0f;

        /// <summary>Optional key forwarded to <see cref="IUtilityTargetProvider.ResolveTarget"/> when scoring and executing this behavior.</summary>
        [Tooltip("Optional key passed to IUtilityTargetProvider when evaluating/running this behavior.")]
        public string TargetKey = string.Empty;

        /// <summary>Reduction strategy combining all <see cref="Considerations"/> into a base utility value.</summary>
        public ScoreAggregationMode Aggregation = ScoreAggregationMode.CompensatedProduct;

        /// <summary>Polymorphic list of considerations evaluated to score this behavior.</summary>
        [SerializeReference]
        public List<UtilityConsiderationBase> Considerations = new();

        /// <summary>Polymorphic executable action run while this behavior is active; may be null for score-only behaviors.</summary>
        [SerializeReference]
        public UtilityActionBase Action;

        /// <summary>Returns <see cref="DisplayName"/> when non-blank, otherwise falls back to <see cref="Id"/>.</summary>
        public string EffectiveName => string.IsNullOrWhiteSpace(DisplayName) ? Id : DisplayName;
    }
}
