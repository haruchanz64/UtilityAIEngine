using System;
using System.Collections.Generic;
using UnityEngine;

namespace UtilityAIWorkbench.Runtime.Core
{
    /// <summary>
    /// Shared evaluation and execution context passed to every consideration and action.
    /// </summary>
    public sealed class UtilityContext
    {
        public GameObject AgentObject { get; set; }
        public Transform AgentTransform => AgentObject ? AgentObject.transform : null;
        public UtilityBlackboard Blackboard { get; }
        public Transform Target { get; set; }
        public float DeltaTime { get; set; }
        public float ElapsedTime { get; set; }
        public string ActiveBehaviorId { get; set; }

        private readonly Dictionary<string, float> _cooldownReadyAt = new(StringComparer.OrdinalIgnoreCase);

        public UtilityContext(UtilityBlackboard blackboard = null, GameObject agentObject = null)
        {
            Blackboard = blackboard ?? new UtilityBlackboard();
            AgentObject = agentObject;
        }

        public void SetCooldown(string behaviorId, float durationSeconds)
        {
            if (string.IsNullOrWhiteSpace(behaviorId) || durationSeconds <= 0f)
                return;

            _cooldownReadyAt[behaviorId] = ElapsedTime + durationSeconds;
        }

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

        public bool IsOnCooldown(string behaviorId) => GetCooldownRemaining(behaviorId) > 0.0001f;

        public void ClearCooldowns() => _cooldownReadyAt.Clear();
    }
}
