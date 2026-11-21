using System.Collections.Generic;
using UnityEngine;

namespace UtilityAIWorkbench.Runtime.Data
{
    public enum BehaviorSelectionMode
    {
        HighestScore = 0,
        WeightedTopK = 1,
        SoftmaxTemperature = 2
    }

    /// <summary>
    /// Reusable ScriptableObject defining a Utility AI brain's behaviors, selection policy, and hysteresis settings.
    /// </summary>
    [CreateAssetMenu(fileName = "NewUtilityBrain", menuName = "Utility AI Workbench/Utility Brain", order = 0)]
    public sealed class UtilityBrainAsset : ScriptableObject
    {
        [TextArea(2, 4)]
        public string Description = string.Empty;

        [Header("Decision Policy")]
        public BehaviorSelectionMode SelectionMode = BehaviorSelectionMode.HighestScore;

        [Min(1)]
        [Tooltip("Number of highest-scoring candidates considered when SelectionMode is WeightedTopK.")]
        public int TopKCount = 3;

        [Range(0.05f, 5f)]
        [Tooltip("Temperature parameter for Softmax selection (lower = greedier, higher = more exploratory).")]
        public float SoftmaxTemperature = 0.35f;

        [Min(0.02f)]
        [Tooltip("Seconds between utility re-evaluations while running.")]
        public float EvaluationInterval = 0.2f;

        [Header("Anti-Thrashing / Hysteresis")]
        [Range(0f, 1f)]
        [Tooltip("Bonus score added to the currently active behavior to prevent rapid oscillation.")]
        public float InertiaBonus = 0.08f;

        [Range(0f, 1f)]
        [Tooltip("Minimum score advantage a rival behavior must have over the active behavior to switch.")]
        public float MinSwitchDelta = 0.02f;

        [Header("Behaviors")]
        public List<UtilityBehaviorDefinition> Behaviors = new();

        public UtilityBehaviorDefinition FindBehavior(string behaviorId)
        {
            if (string.IsNullOrEmpty(behaviorId) || Behaviors == null)
                return null;

            for (var i = 0; i < Behaviors.Count; i++)
            {
                if (Behaviors[i] != null && string.Equals(Behaviors[i].Id, behaviorId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return Behaviors[i];
                }
            }

            return null;
        }
    }
}
