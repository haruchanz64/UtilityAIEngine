using System;
using System.Collections.Generic;
using UnityEngine;
using UtilityAIWorkbench.Runtime.Core;
using UtilityAIWorkbench.Runtime.Data;

namespace UtilityAIWorkbench.Runtime.Engine
{
    /// <summary>
    /// Pure-C# Utility AI scorer implementing Dave Mark's compensation factor, cooldown gating,
    /// hysteresis inertia, and deterministic/stochastic behavior selection.
    /// </summary>
    public sealed class UtilityScorer
    {
        private readonly System.Random _rng;

        public UtilityScorer(int? seed = null)
        {
            _rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        }

        public DecisionSnapshot Evaluate(
            UtilityBrainAsset brain,
            UtilityContext context,
            int tickNumber = 0,
            string currentBehaviorId = null,
            bool lockedByCommitment = false,
            IUtilityTargetProvider targetProvider = null)
        {
            if (brain == null)
                throw new ArgumentNullException(nameof(brain));

            context ??= new UtilityContext();

            var behaviors = brain.Behaviors;
            var records = new List<BehaviorEvaluationRecord>(behaviors != null ? behaviors.Count : 0);
            var candidateIndices = new List<int>();
            var originalTarget = context.Target;

            if (behaviors != null)
            {
                for (var i = 0; i < behaviors.Count; i++)
                {
                    var behavior = behaviors[i];
                    if (behavior == null) continue;

                    if (targetProvider != null && !string.IsNullOrEmpty(behavior.TargetKey))
                    {
                        context.Target = targetProvider.ResolveTarget(context, behavior.TargetKey);
                    }
                    else
                    {
                        context.Target = originalTarget;
                    }

                    var isCurrent = !string.IsNullOrEmpty(currentBehaviorId) &&
                                    string.Equals(behavior.Id, currentBehaviorId, StringComparison.OrdinalIgnoreCase);

                    var record = ScoreBehavior(behavior, context, isCurrent ? brain.InertiaBonus : 0f);
                    records.Add(record);

                    if (record.FinalScore > 0f && string.IsNullOrEmpty(record.DisqualificationReason))
                    {
                        candidateIndices.Add(records.Count - 1);
                    }
                }
            }

            context.Target = originalTarget;

            var winnerIndex = -1;
            var switched = false;

            if (lockedByCommitment && !string.IsNullOrEmpty(currentBehaviorId))
            {
                winnerIndex = FindRecordIndex(records, currentBehaviorId);
            }
            else if (candidateIndices.Count > 0)
            {
                winnerIndex = SelectCandidate(brain, records, candidateIndices);

                if (winnerIndex >= 0 && !string.IsNullOrEmpty(currentBehaviorId) && brain.MinSwitchDelta > 0f)
                {
                    var currentIndex = FindRecordIndex(records, currentBehaviorId);
                    if (currentIndex >= 0 && currentIndex != winnerIndex &&
                        string.IsNullOrEmpty(records[currentIndex].DisqualificationReason) &&
                        records[currentIndex].FinalScore > 0f)
                    {
                        var advantage = records[winnerIndex].FinalScore - records[currentIndex].FinalScore;
                        if (advantage < brain.MinSwitchDelta)
                        {
                            winnerIndex = currentIndex;
                        }
                    }
                }
            }

            var selectedId = string.Empty;
            var selectedName = string.Empty;
            var selectedScore = 0f;

            if (winnerIndex >= 0 && winnerIndex < records.Count)
            {
                records[winnerIndex].IsWinner = true;
                selectedId = records[winnerIndex].BehaviorId;
                selectedName = records[winnerIndex].DisplayName;
                selectedScore = records[winnerIndex].FinalScore;
                switched = !string.Equals(selectedId, currentBehaviorId, StringComparison.OrdinalIgnoreCase);
            }

            var agentName = context.AgentObject ? context.AgentObject.name : "Simulator";
            return new DecisionSnapshot(
                tickNumber,
                context.ElapsedTime,
                agentName,
                brain.name,
                selectedId,
                selectedName,
                selectedScore,
                switched,
                lockedByCommitment,
                records);
        }

        public static BehaviorEvaluationRecord ScoreBehavior(
            UtilityBehaviorDefinition behavior,
            UtilityContext context,
            float inertiaBonus = 0f)
        {
            if (!behavior.Enabled)
            {
                return new BehaviorEvaluationRecord(
                    behavior.Id, behavior.EffectiveName, false, false, 0f,
                    0f, behavior.Weight, 0f, 0f, "Disabled", Array.Empty<ConsiderationScoreRecord>());
            }

            var cooldownRemaining = context.GetCooldownRemaining(behavior.Id);
            if (cooldownRemaining > 0.0001f)
            {
                return new BehaviorEvaluationRecord(
                    behavior.Id, behavior.EffectiveName, true, true, cooldownRemaining,
                    0f, behavior.Weight, 0f, 0f, $"Cooldown ({cooldownRemaining:F1}s)", Array.Empty<ConsiderationScoreRecord>());
            }

            var considerations = behavior.Considerations;
            if (considerations == null || considerations.Count == 0)
            {
                var fallbackBase = 1f;
                var fallbackFinal = Mathf.Max(0f, fallbackBase * behavior.Weight + inertiaBonus);
                return new BehaviorEvaluationRecord(
                    behavior.Id, behavior.EffectiveName, true, false, 0f,
                    fallbackBase, behavior.Weight, inertiaBonus, fallbackFinal, string.Empty,
                    Array.Empty<ConsiderationScoreRecord>());
            }

            var details = new List<ConsiderationScoreRecord>(considerations.Count);
            var vetoed = false;
            string vetoSource = null;

            for (var i = 0; i < considerations.Count; i++)
            {
                var c = considerations[i];
                if (c == null) continue;

                var eval = c.EvaluateDetailed(context);
                details.Add(eval);

                if (eval.WasVetoed && !vetoed)
                {
                    vetoed = true;
                    vetoSource = eval.ConsiderationName;
                }
            }

            if (details.Count == 0)
            {
                var emptyFinal = Mathf.Max(0f, behavior.Weight + inertiaBonus);
                return new BehaviorEvaluationRecord(
                    behavior.Id, behavior.EffectiveName, true, false, 0f,
                    1f, behavior.Weight, inertiaBonus, emptyFinal, string.Empty, details);
            }

            if (vetoed)
            {
                return new BehaviorEvaluationRecord(
                    behavior.Id, behavior.EffectiveName, true, false, 0f,
                    0f, behavior.Weight, 0f, 0f, $"Vetoed by {vetoSource}", details);
            }

            var aggregated = AggregateScores(details, behavior.Aggregation);
            var finalScore = Mathf.Max(0f, aggregated * Mathf.Max(0f, behavior.Weight) + inertiaBonus);

            return new BehaviorEvaluationRecord(
                behavior.Id, behavior.EffectiveName, true, false, 0f,
                aggregated, behavior.Weight, inertiaBonus, finalScore, string.Empty, details);
        }

        /// <summary>
        /// Combines consideration outputs using Dave Mark's compensation factor (default) or standard reducers.
        /// </summary>
        public static float AggregateScores(IReadOnlyList<ConsiderationScoreRecord> details, ScoreAggregationMode mode)
        {
            if (details == null || details.Count == 0)
                return 0f;

            switch (mode)
            {
                case ScoreAggregationMode.CompensatedProduct:
                {
                    var product = 1f;
                    for (var i = 0; i < details.Count; i++)
                    {
                        product *= Mathf.Clamp01(details[i].CurveOutput);
                    }

                    // Dave Mark compensation factor prevents penalizing behaviors simply for having more considerations.
                    var modFactor = 1f - (1f / details.Count);
                    var makeUpValue = (1f - product) * modFactor;
                    return Mathf.Clamp01(product + (makeUpValue * product));
                }

                case ScoreAggregationMode.RawProduct:
                {
                    var product = 1f;
                    for (var i = 0; i < details.Count; i++)
                    {
                        product *= Mathf.Clamp01(details[i].CurveOutput);
                    }
                    return Mathf.Clamp01(product);
                }

                case ScoreAggregationMode.Average:
                {
                    var sum = 0f;
                    for (var i = 0; i < details.Count; i++)
                    {
                        sum += Mathf.Clamp01(details[i].CurveOutput);
                    }
                    return Mathf.Clamp01(sum / details.Count);
                }

                case ScoreAggregationMode.Minimum:
                {
                    var min = 1f;
                    for (var i = 0; i < details.Count; i++)
                    {
                        min = Mathf.Min(min, Mathf.Clamp01(details[i].CurveOutput));
                    }
                    return min;
                }

                case ScoreAggregationMode.Maximum:
                {
                    var max = 0f;
                    for (var i = 0; i < details.Count; i++)
                    {
                        max = Mathf.Max(max, Mathf.Clamp01(details[i].CurveOutput));
                    }
                    return max;
                }

                default:
                    return 0f;
            }
        }

        private int SelectCandidate(UtilityBrainAsset brain, List<BehaviorEvaluationRecord> records, List<int> candidateIndices)
        {
            candidateIndices.Sort((a, b) => records[b].FinalScore.CompareTo(records[a].FinalScore));

            if (brain.SelectionMode == BehaviorSelectionMode.HighestScore || candidateIndices.Count == 1)
            {
                return candidateIndices[0];
            }

            if (brain.SelectionMode == BehaviorSelectionMode.WeightedTopK)
            {
                var k = Mathf.Clamp(brain.TopKCount, 1, candidateIndices.Count);
                var total = 0.0;
                for (var i = 0; i < k; i++)
                {
                    total += records[candidateIndices[i]].FinalScore;
                }

                if (total <= 0.00001) return candidateIndices[0];

                var roll = _rng.NextDouble() * total;
                var cumulative = 0.0;
                for (var i = 0; i < k; i++)
                {
                    cumulative += records[candidateIndices[i]].FinalScore;
                    if (roll <= cumulative) return candidateIndices[i];
                }

                return candidateIndices[k - 1];
            }

            // SoftmaxTemperature
            var temp = Mathf.Max(0.05f, brain.SoftmaxTemperature);
            var maxScore = records[candidateIndices[0]].FinalScore;
            var weights = new double[candidateIndices.Count];
            var sumExp = 0.0;

            for (var i = 0; i < candidateIndices.Count; i++)
            {
                var shifted = (records[candidateIndices[i]].FinalScore - maxScore) / temp;
                var w = Math.Exp(Math.Max(-40.0, shifted));
                weights[i] = w;
                sumExp += w;
            }

            var sample = _rng.NextDouble() * sumExp;
            var acc = 0.0;
            for (var i = 0; i < candidateIndices.Count; i++)
            {
                acc += weights[i];
                if (sample <= acc) return candidateIndices[i];
            }

            return candidateIndices[0];
        }

        private static int FindRecordIndex(List<BehaviorEvaluationRecord> records, string behaviorId)
        {
            for (var i = 0; i < records.Count; i++)
            {
                if (string.Equals(records[i].BehaviorId, behaviorId, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
