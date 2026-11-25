using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UtilityAIWorkbench.Runtime.Core;

namespace UtilityAIWorkbench.Editor.Export
{
    /// <summary>
    /// Exports decision history snapshots to JSON or CSV for QA traces and design review.
    /// </summary>
    public static class UtilityDecisionExporter
    {
        [Serializable]
        private sealed class TraceExportDto
        {
            public int snapshotCount;
            public List<SnapshotDto> snapshots = new();
        }

        [Serializable]
        private sealed class SnapshotDto
        {
            public int tick;
            public float timestamp;
            public string agentName;
            public string brainName;
            public string selectedBehaviorId;
            public string selectedBehaviorName;
            public float selectedScore;
            public bool switched;
            public bool lockedByCommitment;
            public List<BehaviorScoreDto> behaviors = new();
        }

        [Serializable]
        private sealed class BehaviorScoreDto
        {
            public string id;
            public string name;
            public float baseUtility;
            public float weight;
            public float inertiaBonus;
            public float finalScore;
            public bool isWinner;
            public string disqualificationReason;
        }

        /// <summary>
        /// Serializes a sequence of decision snapshots into a JSON trace document via <see cref="JsonUtility"/>.
        /// </summary>
        /// <param name="snapshots">Chronological snapshots to serialize; null or null elements are skipped safely.</param>
        /// <param name="prettyPrint">When true, formats the JSON with indentation for readability.</param>
        /// <returns>JSON string containing the snapshot count and per-tick behavior evaluation breakdowns.</returns>
        public static string ToJson(IReadOnlyList<DecisionSnapshot> snapshots, bool prettyPrint = true)
        {
            var export = new TraceExportDto
            {
                snapshotCount = snapshots != null ? snapshots.Count : 0
            };

            if (snapshots != null)
            {
                for (var i = 0; i < snapshots.Count; i++)
                {
                    var s = snapshots[i];
                    if (s == null) continue;

                    var dto = new SnapshotDto
                    {
                        tick = s.TickNumber,
                        timestamp = s.Timestamp,
                        agentName = s.AgentName,
                        brainName = s.BrainName,
                        selectedBehaviorId = s.SelectedBehaviorId,
                        selectedBehaviorName = s.SelectedBehaviorName,
                        selectedScore = s.SelectedScore,
                        switched = s.SwitchedBehavior,
                        lockedByCommitment = s.LockedByCommitment
                    };

                    for (var j = 0; j < s.Evaluations.Count; j++)
                    {
                        var b = s.Evaluations[j];
                        dto.behaviors.Add(new BehaviorScoreDto
                        {
                            id = b.BehaviorId,
                            name = b.DisplayName,
                            baseUtility = b.BaseUtility,
                            weight = b.Weight,
                            inertiaBonus = b.InertiaBonusApplied,
                            finalScore = b.FinalScore,
                            isWinner = b.IsWinner,
                            disqualificationReason = b.DisqualificationReason
                        });
                    }

                    export.snapshots.Add(dto);
                }
            }

            return JsonUtility.ToJson(export, prettyPrint);
        }

        /// <summary>
        /// Serializes decision snapshots into a flat RFC 4180 CSV table with one row per evaluated behavior per tick using invariant numeric formatting.
        /// </summary>
        /// <param name="snapshots">Chronological snapshots to export; returns only the header row if null or empty.</param>
        /// <returns>CSV text starting with the column header line.</returns>
        public static string ToCsv(IReadOnlyList<DecisionSnapshot> snapshots)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Tick,Timestamp,Agent,Brain,BehaviorId,BehaviorName,BaseUtility,Weight,InertiaBonus,FinalScore,IsWinner,Switched,Locked,Disqualification");

            if (snapshots == null)
                return sb.ToString();

            var ci = CultureInfo.InvariantCulture;
            for (var i = 0; i < snapshots.Count; i++)
            {
                var s = snapshots[i];
                if (s == null) continue;

                for (var j = 0; j < s.Evaluations.Count; j++)
                {
                    var b = s.Evaluations[j];
                    sb.Append(s.TickNumber.ToString(ci)).Append(',')
                      .Append(s.Timestamp.ToString("F3", ci)).Append(',')
                      .Append(EscapeCsv(s.AgentName)).Append(',')
                      .Append(EscapeCsv(s.BrainName)).Append(',')
                      .Append(EscapeCsv(b.BehaviorId)).Append(',')
                      .Append(EscapeCsv(b.DisplayName)).Append(',')
                      .Append(b.BaseUtility.ToString("F4", ci)).Append(',')
                      .Append(b.Weight.ToString("F4", ci)).Append(',')
                      .Append(b.InertiaBonusApplied.ToString("F4", ci)).Append(',')
                      .Append(b.FinalScore.ToString("F4", ci)).Append(',')
                      .Append(b.IsWinner ? "true" : "false").Append(',')
                      .Append(s.SwitchedBehavior ? "true" : "false").Append(',')
                      .Append(s.LockedByCommitment ? "true" : "false").Append(',')
                      .AppendLine(EscapeCsv(b.DisqualificationReason));
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Wraps fields containing commas, quotes, or line breaks in double quotes with escaped inner quotes.
        /// </summary>
        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }
    }
}
