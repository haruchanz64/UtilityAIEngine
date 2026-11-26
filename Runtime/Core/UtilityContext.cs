using System;
using System.Collections.Generic;
using UnityEngine;

namespace UtilityAIEngine.Runtime.Core
{
    /// <summary>
    /// Shared evaluation and execution context passed to every consideration and action,
    /// carrying agent references, target state, timing, blackboard facts, and per-behavior cooldowns.
    /// </summary>
    public sealed class UtilityContext
    {
        /// <summary>Optional owning <see cref="GameObject"/> for the AI agent (null in headless or simulator evaluations).</summary>
        public GameObject AgentObject { get; set; }
        /// <summary>Transform of <see cref="AgentObject"/>, or null when no live GameObject is assigned.</summary>
        public Transform AgentTransform => AgentObject ? AgentObject.transform : null;
        /// <summary>Typed fact store shared across considerations and actions.</summary>
        public UtilityBlackboard Blackboard { get; }
        /// <summary>Active target transform for the behavior currently being scored or executed.</summary>
        public Transform Target { get; set; }
        /// <summary>Elapsed seconds for the current runner tick.</summary>
        public float DeltaTime { get; set; }
        /// <summary>Accumulated simulation time in seconds used for cooldown timestamps and snapshot telemetry.</summary>
        public float ElapsedTime { get; set; }
        /// <summary>Identifier of the currently executing behavior, or empty when idle.</summary>
        public string ActiveBehaviorId { get; set; }

        private readonly Dictionary<string, float> _cooldownReadyAt = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a context with an optional existing blackboard and owning GameObject.
        /// </summary>
        /// <param name="blackboard">Shared blackboard instance; a new empty blackboard is allocated if null.</param>
        /// <param name="agentObject">Optional scene GameObject representing the AI agent.</param>
        public UtilityContext(UtilityBlackboard blackboard = null, GameObject agentObject = null)
        {
            Blackboard = blackboard ?? new UtilityBlackboard();
            AgentObject = agentObject;
        }

        /// <summary>
        /// Places <paramref name="behaviorId"/> on cooldown until <see cref="ElapsedTime"/> plus <paramref name="durationSeconds"/>.
        /// </summary>
        /// <param name="behaviorId">Case-insensitive behavior identifier; ignored when null or whitespace.</param>
        /// <param name="durationSeconds">Cooldown duration in seconds; non-positive durations are ignored.</param>
        public void SetCooldown(string behaviorId, float durationSeconds)
        {
            if (string.IsNullOrWhiteSpace(behaviorId) || durationSeconds <= 0f)
                return;

            _cooldownReadyAt[behaviorId] = ElapsedTime + durationSeconds;
        }

        /// <summary>
        /// Returns the remaining cooldown time in seconds for <paramref name="behaviorId"/>, clamped to at least 0.
        /// </summary>
        public float GetCooldownRemaining(string behaviorId)
        {
            if (string.IsNullOrWhiteSpace(behaviorId))
                return 0f;

            if (_cooldownReadyAt.TryGetValue(behaviorId, out var readyAt))
            {
                return Mathf.Max(0f, readyAt - ElapsedTime);
            }

            return 0f;
        }

        /// <summary>
        /// Returns true while <paramref name="behaviorId"/> has more than 0.0001 seconds of cooldown remaining.
        /// </summary>
        public bool IsOnCooldown(string behaviorId) => GetCooldownRemaining(behaviorId) > 0.0001f;

        /// <summary>
        /// Clears all active behavior cooldown timers in this context.
        /// </summary>
        public void ClearCooldowns() => _cooldownReadyAt.Clear();
    }
}
