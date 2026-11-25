using System.Collections.Generic;
using UnityEngine;

namespace UtilityAIWorkbench.Runtime.Data
{
    /// <summary>
    /// Candidate selection policy applied after all eligible behaviors have been scored.
    /// </summary>
    public enum BehaviorSelectionMode
    {
        /// <summary>Deterministically picks the single highest-scoring eligible behavior.</summary>
        HighestScore = 0,
        /// <summary>Samples randomly among the top <see cref="UtilityBrainAsset.TopKCount"/> candidates weighted proportionally by final score.</summary>
        WeightedTopK = 1,
        /// <summary>Samples across all eligible candidates using a temperature-scaled softmax distribution.</summary>
        SoftmaxTemperature = 2
    }

    /// <summary>
    /// Reusable ScriptableObject defining a Utility AI brain's behaviors, selection policy, and hysteresis settings.
    /// </summary>
    [CreateAssetMenu(fileName = "NewUtilityBrain", menuName = "Utility AI Workbench/Utility Brain", order = 0)]
    public sealed class UtilityBrainAsset : ScriptableObject
    {
        /// <summary>Optional designer notes describing the brain's role and expected blackboard keys.</summary>
        [TextArea(2, 4)]
        public string Description = string.Empty;

        /// <summary>Policy controlling how the winning behavior is chosen from scored candidates.</summary>
        [Header("Decision Policy")]
        public BehaviorSelectionMode SelectionMode = BehaviorSelectionMode.HighestScore;

        /// <summary>Maximum number of top-ranked candidates included in <see cref="BehaviorSelectionMode.WeightedTopK"/> sampling.</summary>
        [Min(1)]
        [Tooltip("Number of highest-scoring candidates considered when SelectionMode is WeightedTopK.")]
        public int TopKCount = 3;

        /// <summary>Softmax temperature controlling exploration vs. exploitation (clamped to at least 0.05 during scoring).</summary>
        [Range(0.05f, 5f)]
        [Tooltip("Temperature parameter for Softmax selection (lower = greedier, higher = more exploratory).")]
        public float SoftmaxTemperature = 0.35f;

        /// <summary>Time in seconds between periodic utility re-evaluations (clamped to at least 0.02s at runtime).</summary>
        [Min(0.02f)]
        [Tooltip("Seconds between utility re-evaluations while running.")]
        public float EvaluationInterval = 0.2f;

        /// <summary>Score bonus added to the currently active behavior to resist rapid oscillation.</summary>
        [Header("Anti-Thrashing / Hysteresis")]
        [Range(0f, 1f)]
        [Tooltip("Bonus score added to the currently active behavior to prevent rapid oscillation.")]
        public float InertiaBonus = 0.08f;

        /// <summary>Minimum score lead a challenger must hold over the active behavior before switching occurs.</summary>
        [Range(0f, 1f)]
        [Tooltip("Minimum score advantage a rival behavior must have over the active behavior to switch.")]
        public float MinSwitchDelta = 0.02f;

        /// <summary>Ordered list of behaviors evaluated by this brain.</summary>
        [Header("Behaviors")]
        public List<UtilityBehaviorDefinition> Behaviors = new();

        /// <summary>
        /// Looks up a behavior definition by its case-insensitive <see cref="UtilityBehaviorDefinition.Id"/>.
        /// </summary>
        /// <param name="behaviorId">Behavior identifier to search for.</param>
        /// <returns>The matching <see cref="UtilityBehaviorDefinition"/>, or null if not found.</returns>
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
