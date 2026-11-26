using System;
using System.Collections.Generic;

namespace UtilityAIEngine.Runtime.Core
{
    /// <summary>
    /// Immutable evaluation trace for a single consideration, capturing raw input, normalized input, curved output, and veto state.
    /// </summary>
    public readonly struct ConsiderationScoreRecord
    {
        /// <summary>Display name of the evaluated consideration.</summary>
        public string ConsiderationName { get; }
        /// <summary>Unnormalized value sampled before [0..1] normalization.</summary>
        public float RawInput { get; }
        /// <summary>Normalized input in [0..1] fed into the response curve.</summary>
        public float NormalizedInput { get; }
        /// <summary>Utility score in [0..1] returned by the response curve.</summary>
        public float CurveOutput { get; }
        /// <summary>True when <see cref="UtilityConsiderationBase.CanVeto"/> disqualified the owning behavior.</summary>
        public bool WasVetoed { get; }
        /// <summary>Shape of the response curve used during evaluation.</summary>
        public ResponseCurveType CurveType { get; }

        /// <summary>
        /// Initializes a consideration evaluation record.
        /// </summary>
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

    /// <summary>
    /// Diagnostic breakdown of one behavior's evaluation during a single decision tick.
    /// </summary>
    public sealed class BehaviorEvaluationRecord
    {
        /// <summary>Unique identifier of the evaluated behavior.</summary>
        public string BehaviorId { get; }
        /// <summary>Effective display name of the behavior.</summary>
        public string DisplayName { get; }
        /// <summary>Whether the behavior was enabled at evaluation time.</summary>
        public bool Enabled { get; }
        /// <summary>True if the behavior was skipped because its cooldown had not expired.</summary>
        public bool OnCooldown { get; }
        /// <summary>Remaining cooldown time in seconds when <see cref="OnCooldown"/> is true.</summary>
        public float CooldownRemaining { get; }
        /// <summary>Aggregated consideration score in [0..1] before multiplying by <see cref="Weight"/>.</summary>
        public float BaseUtility { get; }
        /// <summary>Configured behavior weight multiplier.</summary>
        public float Weight { get; }
        /// <summary>Hysteresis inertia bonus added when this behavior was already active.</summary>
        public float InertiaBonusApplied { get; }
        /// <summary>Final score used for behavior selection (<c>max(0, BaseUtility * Weight + InertiaBonusApplied)</c>).</summary>
        public float FinalScore { get; }
        /// <summary>True if this behavior was chosen as the winner for the enclosing <see cref="DecisionSnapshot"/>.</summary>
        public bool IsWinner { get; internal set; }
        /// <summary>Human-readable reason the behavior was disqualified (for example disabled, cooldown, or veto), or empty when eligible.</summary>
        public string DisqualificationReason { get; }
        /// <summary>Per-consideration evaluation records captured for this behavior.</summary>
        public IReadOnlyList<ConsiderationScoreRecord> Considerations { get; }

        /// <summary>
        /// Initializes a behavior evaluation record with all scoring components and consideration details.
        /// </summary>
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

    /// <summary>
    /// Complete immutable snapshot of a single Utility AI decision tick, including the chosen behavior and all candidate scores.
    /// </summary>
    public sealed class DecisionSnapshot
    {
        /// <summary>Monotonically increasing evaluation counter for the runner or simulator.</summary>
        public int TickNumber { get; }
        /// <summary>Simulation timestamp in seconds from <see cref="UtilityContext.ElapsedTime"/>.</summary>
        public float Timestamp { get; }
        /// <summary>Name of the agent GameObject, or <c>Simulator</c> when evaluated without a GameObject.</summary>
        public string AgentName { get; }
        /// <summary>Asset name of the evaluated brain.</summary>
        public string BrainName { get; }
        /// <summary>Identifier of the winning behavior, or empty when no behavior was eligible.</summary>
        public string SelectedBehaviorId { get; }
        /// <summary>Display name of the winning behavior, or empty when no behavior was eligible.</summary>
        public string SelectedBehaviorName { get; }
        /// <summary>Final score of the winning behavior, or 0 when none won.</summary>
        public float SelectedScore { get; }
        /// <summary>True when the selected behavior differs from the previously active behavior.</summary>
        public bool SwitchedBehavior { get; }
        /// <summary>True when selection was forced to retain the active behavior because its minimum commitment duration had not elapsed.</summary>
        public bool LockedByCommitment { get; }
        /// <summary>Evaluation records for every behavior inspected during this tick.</summary>
        public IReadOnlyList<BehaviorEvaluationRecord> Evaluations { get; }

        /// <summary>
        /// Initializes a decision snapshot capturing the winner and full candidate evaluation breakdown.
        /// </summary>
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

        /// <summary>Maximum number of snapshots retained before older entries are overwritten.</summary>
        public int Capacity => _buffer.Length;
        /// <summary>Number of snapshots currently stored in the buffer.</summary>
        public int Count => _count;

        /// <summary>
        /// Initializes a ring buffer with the specified fixed capacity.
        /// </summary>
        /// <param name="capacity">Maximum number of snapshots to retain; must be greater than zero.</param>
        public DecisionRingBuffer(int capacity = 128)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _buffer = new DecisionSnapshot[capacity];
        }

        /// <summary>
        /// Appends a snapshot, overwriting the oldest entry once <see cref="Capacity"/> is reached. Null snapshots are ignored.
        /// </summary>
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

        /// <summary>Most recently pushed snapshot, or null when the buffer is empty.</summary>
        public DecisionSnapshot Latest => _count == 0 ? null : _buffer[(_head - 1 + _buffer.Length) % _buffer.Length];

        /// <summary>
        /// Copies all stored snapshots into a new list ordered from oldest to newest.
        /// </summary>
        /// <returns>A chronological list containing <see cref="Count"/> snapshots.</returns>
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

        /// <summary>
        /// Releases all buffered snapshot references and resets <see cref="Count"/> to zero.
        /// </summary>
        public void Clear()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _head = 0;
            _count = 0;
        }
    }
}
