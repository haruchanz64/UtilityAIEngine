using System;
using System.Collections.Generic;
using UnityEngine;
using UtilityAIWorkbench.Runtime.Core;

namespace UtilityAIWorkbench.Runtime.Data
{
    public enum ScoreAggregationMode
    {
        CompensatedProduct = 0,
        RawProduct = 1,
        Average = 2,
        Minimum = 3,
        Maximum = 4
    }

    /// <summary>
    /// Defines a single selectable AI behavior: its weight, cooldown, commitment lock, considerations, and executable action.
    /// </summary>
    [Serializable]
    public sealed class UtilityBehaviorDefinition
    {
        public string Id = "behavior_1";
        public string DisplayName = "New Behavior";
        public bool Enabled = true;

        [Min(0f)]
        [Tooltip("Multiplier applied after consideration aggregation.")]
        public float Weight = 1f;

        [Min(0f)]
        [Tooltip("Cooldown seconds applied when this behavior exits or completes.")]
        public float CooldownSeconds = 0f;

        [Min(0f)]
        [Tooltip("Minimum seconds this behavior stays active before another behavior can preempt it (unless it completes/fails).")]
        public float MinCommitmentSeconds = 0f;

        [Tooltip("Optional key passed to IUtilityTargetProvider when evaluating/running this behavior.")]
        public string TargetKey = string.Empty;

        public ScoreAggregationMode Aggregation = ScoreAggregationMode.CompensatedProduct;

        [SerializeReference]
        public List<UtilityConsiderationBase> Considerations = new();

        [SerializeReference]
        public UtilityActionBase Action;

        public string EffectiveName => string.IsNullOrWhiteSpace(DisplayName) ? Id : DisplayName;
    }
}
