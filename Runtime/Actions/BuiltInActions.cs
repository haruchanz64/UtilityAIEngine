using System;
using UnityEngine;
using UtilityAIWorkbench.Runtime.Core;

namespace UtilityAIWorkbench.Runtime.Actions
{
    /// <summary>
    /// Timed idle action that stays <see cref="UtilityActionStatus.Running"/> until <see cref="Duration"/> seconds elapse, then completes.
    /// </summary>
    [Serializable]
    public sealed class WaitAction : UtilityActionBase
    {
        /// <summary>Wait duration in seconds before returning <see cref="UtilityActionStatus.Completed"/>.</summary>
        public float Duration = 1.5f;
        private float _elapsed;

        /// <summary>
        /// Resets the elapsed timer when the action begins.
        /// </summary>
        public override void OnEnter(UtilityContext context)
        {
            _elapsed = 0f;
        }

        /// <summary>
        /// Advances the wait timer by <see cref="UtilityContext.DeltaTime"/> and completes once <see cref="Duration"/> is reached.
        /// </summary>
        public override UtilityActionStatus OnTick(UtilityContext context)
        {
            _elapsed += context != null ? context.DeltaTime : 0f;
            return _elapsed >= Duration ? UtilityActionStatus.Completed : UtilityActionStatus.Running;
        }
    }

    /// <summary>
    /// One-shot action that writes a float value to the blackboard and immediately completes.
    /// </summary>
    [Serializable]
    public sealed class SetBlackboardFloatAction : UtilityActionBase
    {
        /// <summary>Blackboard key to write.</summary>
        public string Key = "StateValue";
        /// <summary>Float value assigned to <see cref="Key"/>.</summary>
        public float Value = 1f;

        /// <summary>
        /// Writes <see cref="Value"/> to <see cref="Key"/> on the blackboard and returns <see cref="UtilityActionStatus.Completed"/>.
        /// </summary>
        public override UtilityActionStatus OnTick(UtilityContext context)
        {
            context?.Blackboard?.SetFloat(Key, Value);
            return UtilityActionStatus.Completed;
        }
    }

    /// <summary>
    /// Continuous action that integrates a blackboard float at <see cref="RatePerSecond"/> clamped to [<see cref="MinValue"/>..<see cref="MaxValue"/>].
    /// </summary>
    [Serializable]
    public sealed class DeltaBlackboardFloatAction : UtilityActionBase
    {
        /// <summary>Blackboard float key to modify each tick.</summary>
        public string Key = "Health";
        /// <summary>Change applied per second; positive values increase toward <see cref="MaxValue"/> and negative values decrease toward <see cref="MinValue"/>.</summary>
        public float RatePerSecond = 15f;
        /// <summary>Lower clamp bound and default starting value if <see cref="Key"/> is absent.</summary>
        public float MinValue = 0f;
        /// <summary>Upper clamp bound.</summary>
        public float MaxValue = 100f;
        /// <summary>When true, returns <see cref="UtilityActionStatus.Completed"/> upon reaching the bound in the direction of <see cref="RatePerSecond"/>.</summary>
        public bool CompleteAtLimit = true;

        /// <summary>
        /// Advances the blackboard float value by <c>RatePerSecond * DeltaTime</c> and returns <see cref="UtilityActionStatus.Failed"/> if no blackboard is available.
        /// </summary>
        public override UtilityActionStatus OnTick(UtilityContext context)
        {
            if (context?.Blackboard == null)
                return UtilityActionStatus.Failed;

            var current = context.Blackboard.GetFloat(Key, MinValue);
            var next = Mathf.Clamp(current + RatePerSecond * context.DeltaTime, MinValue, MaxValue);
            context.Blackboard.SetFloat(Key, next);

            if (CompleteAtLimit)
            {
                if (RatePerSecond > 0f && next >= MaxValue - 0.001f) return UtilityActionStatus.Completed;
                if (RatePerSecond < 0f && next <= MinValue + 0.001f) return UtilityActionStatus.Completed;
            }

            return UtilityActionStatus.Running;
        }
    }

    /// <summary>
    /// One-shot action that writes a boolean value to the blackboard and immediately completes.
    /// </summary>
    [Serializable]
    public sealed class SetBlackboardBoolAction : UtilityActionBase
    {
        /// <summary>Blackboard key to write.</summary>
        public string Key = "Alerted";
        /// <summary>Boolean value assigned to <see cref="Key"/>.</summary>
        public bool Value = true;

        /// <summary>
        /// Writes <see cref="Value"/> to <see cref="Key"/> on the blackboard and returns <see cref="UtilityActionStatus.Completed"/>.
        /// </summary>
        public override UtilityActionStatus OnTick(UtilityContext context)
        {
            context?.Blackboard?.SetBool(Key, Value);
            return UtilityActionStatus.Completed;
        }
    }

    /// <summary>
    /// Moves the agent transform directly toward <see cref="UtilityContext.Target"/> or a fallback blackboard <see cref="Vector3"/> position.
    /// </summary>
    [Serializable]
    public sealed class MoveTowardsTargetAction : UtilityActionBase
    {
        /// <summary>Translation speed in world units per second.</summary>
        public float MoveSpeed = 4f;
        /// <summary>World-space arrival threshold at which the action returns <see cref="UtilityActionStatus.Completed"/>.</summary>
        public float StoppingDistance = 1.25f;
        /// <summary>When true, aligns the agent's forward vector with the planar XZ direction to the destination.</summary>
        public bool FaceTarget = true;
        /// <summary>Optional blackboard <see cref="Vector3"/> key used when <see cref="UtilityContext.Target"/> is unassigned.</summary>
        public string FallbackBlackboardVectorKey = string.Empty;

        /// <summary>
        /// Advances the agent transform toward the destination, failing if no agent transform or target position is available.
        /// </summary>
        public override UtilityActionStatus OnTick(UtilityContext context)
        {
            if (context == null || !context.AgentTransform)
                return UtilityActionStatus.Failed;

            Vector3 destination;
            if (context.Target)
            {
                destination = context.Target.position;
            }
            else if (!string.IsNullOrWhiteSpace(FallbackBlackboardVectorKey) &&
                     context.Blackboard.TryGetVector3(FallbackBlackboardVectorKey, out var bbTarget))
            {
                destination = bbTarget;
            }
            else
            {
                return UtilityActionStatus.Failed;
            }

            var transform = context.AgentTransform;
            var toTarget = destination - transform.position;
            var dist = toTarget.magnitude;
            if (dist <= StoppingDistance)
            {
                return UtilityActionStatus.Completed;
            }

            var step = MoveSpeed * context.DeltaTime;
            transform.position = Vector3.MoveTowards(transform.position, destination, step);

            if (FaceTarget && toTarget.sqrMagnitude > 0.0001f)
            {
                var flatDir = new Vector3(toTarget.x, 0f, toTarget.z);
                if (flatDir.sqrMagnitude > 0.0001f)
                {
                    transform.forward = flatDir.normalized;
                }
            }

            return UtilityActionStatus.Running;
        }
    }

    /// <summary>
    /// Diagnostic one-shot action that logs a message to the Unity Console and completes immediately.
    /// </summary>
    [Serializable]
    public sealed class LogDebugAction : UtilityActionBase
    {
        /// <summary>Message text logged with the agent name prefix.</summary>
        public string Message = "Utility AI executed LogDebugAction.";

        /// <summary>
        /// Writes <see cref="Message"/> via <see cref="Debug.Log(object)"/> and returns <see cref="UtilityActionStatus.Completed"/>.
        /// </summary>
        public override UtilityActionStatus OnTick(UtilityContext context)
        {
            var agent = context != null && context.AgentObject ? context.AgentObject.name : "Agent";
            Debug.Log($"[UtilityAI:{agent}] {Message}");
            return UtilityActionStatus.Completed;
        }
    }
}
