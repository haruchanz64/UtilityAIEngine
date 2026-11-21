using System;
using UnityEngine;
using UtilityAIWorkbench.Runtime.Core;

namespace UtilityAIWorkbench.Runtime.Actions
{
    [Serializable]
    public sealed class WaitAction : UtilityActionBase
    {
        public float Duration = 1.5f;
        private float _elapsed;

        public override void OnEnter(UtilityContext context)
        {
            _elapsed = 0f;
        }

        public override UtilityActionStatus OnTick(UtilityContext context)
        {
            _elapsed += context != null ? context.DeltaTime : 0f;
            return _elapsed >= Duration ? UtilityActionStatus.Completed : UtilityActionStatus.Running;
        }
    }

    [Serializable]
    public sealed class SetBlackboardFloatAction : UtilityActionBase
    {
        public string Key = "StateValue";
        public float Value = 1f;

        public override UtilityActionStatus OnTick(UtilityContext context)
        {
            context?.Blackboard?.SetFloat(Key, Value);
            return UtilityActionStatus.Completed;
        }
    }

    [Serializable]
    public sealed class DeltaBlackboardFloatAction : UtilityActionBase
    {
        public string Key = "Health";
        public float RatePerSecond = 15f;
        public float MinValue = 0f;
        public float MaxValue = 100f;
        public bool CompleteAtLimit = true;

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

    [Serializable]
    public sealed class SetBlackboardBoolAction : UtilityActionBase
    {
        public string Key = "Alerted";
        public bool Value = true;

        public override UtilityActionStatus OnTick(UtilityContext context)
        {
            context?.Blackboard?.SetBool(Key, Value);
            return UtilityActionStatus.Completed;
        }
    }

    [Serializable]
    public sealed class MoveTowardsTargetAction : UtilityActionBase
    {
        public float MoveSpeed = 4f;
        public float StoppingDistance = 1.25f;
        public bool FaceTarget = true;
        public string FallbackBlackboardVectorKey = string.Empty;

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

    [Serializable]
    public sealed class LogDebugAction : UtilityActionBase
    {
        public string Message = "Utility AI executed LogDebugAction.";

        public override UtilityActionStatus OnTick(UtilityContext context)
        {
            var agent = context != null && context.AgentObject ? context.AgentObject.name : "Agent";
            Debug.Log($"[UtilityAI:{agent}] {Message}");
            return UtilityActionStatus.Completed;
        }
    }
}
