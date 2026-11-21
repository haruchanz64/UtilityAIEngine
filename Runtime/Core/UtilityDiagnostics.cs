using System;
using System.Collections.Generic;

namespace UtilityAIWorkbench.Runtime.Core
{
    public readonly struct ConsiderationScoreRecord
    {
        public string ConsiderationName { get; }
        public float RawInput { get; }
        public float NormalizedInput { get; }
        public float CurveOutput { get; }
        public bool WasVetoed { get; }
        public ResponseCurveType CurveType { get; }

        public ConsiderationScoreRecord(
            string considerationName,
            float rawInput,
            float normalizedInput,
            float curveOutput,
            bool wasVetoed,
            ResponseCurveType curveType)
        {
            ConsiderationName = considerationName ?? string.Empty;
            RawInput = rawInput;
            NormalizedInput = normalizedInput;
            CurveOutput = curveOutput;
            WasVetoed = wasVetoed;
            CurveType = curveType;
        }
    }

    public sealed class BehaviorEvaluationRecord
    {
        public string BehaviorId { get; }
        public string DisplayName { get; }
        public bool Enabled { get; }
        public bool OnCooldown { get; }
        public float CooldownRemaining { get; }
        public float BaseUtility { get; }
        public float Weight { get; }
        public float InertiaBonusApplied { get; }
        public float FinalScore { get; }
        public bool IsWinner { get; internal set; }
        public string DisqualificationReason { get; }
        public IReadOnlyList<ConsiderationScoreRecord> Considerations { get; }

        public BehaviorEvaluationRecord(
            string behaviorId,
            string displayName,
            bool enabled,
            bool onCooldown,
            float cooldownRemaining,
            float baseUtility,
            float weight,
            float inertiaBonusApplied,
            float finalScore,
            string disqualificationReason,
            IReadOnlyList<ConsiderationScoreRecord> considerations)
        {
            BehaviorId = behaviorId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Enabled = enabled;
            OnCooldown = onCooldown;
            CooldownRemaining = cooldownRemaining;
            BaseUtility = baseUtility;
            Weight = weight;
            InertiaBonusApplied = inertiaBonusApplied;
            FinalScore = finalScore;
            DisqualificationReason = disqualificationReason ?? string.Empty;
            Considerations = considerations ?? Array.Empty<ConsiderationScoreRecord>();
        }
    }

    public sealed class DecisionSnapshot
    {
        public int TickNumber { get; }
        public float Timestamp { get; }
        public string AgentName { get; }
        public string BrainName { get; }
        public string SelectedBehaviorId { get; }
        public string SelectedBehaviorName { get; }
        public float SelectedScore { get; }
        public bool SwitchedBehavior { get; }
        public bool LockedByCommitment { get; }
        public IReadOnlyList<BehaviorEvaluationRecord> Evaluations { get; }

        public DecisionSnapshot(
            int tickNumber,
            float timestamp,
            string agentName,
            string brainName,
            string selectedBehaviorId,
            string selectedBehaviorName,
            float selectedScore,
            bool switchedBehavior,
            bool lockedByCommitment,
            IReadOnlyList<BehaviorEvaluationRecord> evaluations)
        {
            TickNumber = tickNumber;
            Timestamp = timestamp;
            AgentName = agentName ?? string.Empty;
            BrainName = brainName ?? string.Empty;
            SelectedBehaviorId = selectedBehaviorId ?? string.Empty;
            SelectedBehaviorName = selectedBehaviorName ?? string.Empty;
            SelectedScore = selectedScore;
            SwitchedBehavior = switchedBehavior;
            LockedByCommitment = lockedByCommitment;
            Evaluations = evaluations ?? Array.Empty<BehaviorEvaluationRecord>();
        }
    }

    /// <summary>
    /// Bounded circular history of decision snapshots for live debugging without unbounded memory growth.
    /// </summary>
    public sealed class DecisionRingBuffer
    {
        private readonly DecisionSnapshot[] _buffer;
        private int _head;
        private int _count;

        public int Capacity => _buffer.Length;
        public int Count => _count;

        public DecisionRingBuffer(int capacity = 128)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _buffer = new DecisionSnapshot[capacity];
        }

        public void Push(DecisionSnapshot snapshot)
        {
            if (snapshot == null) return;

            _buffer[_head] = snapshot;
            _head = (_head + 1) % _buffer.Length;
            if (_count < _buffer.Length)
            {
                _count++;
            }
        }

        public DecisionSnapshot Latest => _count == 0 ? null : _buffer[(_head - 1 + _buffer.Length) % _buffer.Length];

        public List<DecisionSnapshot> ToChronologicalList()
        {
            var result = new List<DecisionSnapshot>(_count);
            if (_count == 0) return result;

            var start = (_head - _count + _buffer.Length) % _buffer.Length;
            for (var i = 0; i < _count; i++)
            {
                result.Add(_buffer[(start + i) % _buffer.Length]);
            }

            return result;
        }

        public void Clear()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _head = 0;
            _count = 0;
        }
    }
}
