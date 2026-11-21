using System;
using UnityEngine;
using UtilityAIWorkbench.Runtime.Core;

namespace UtilityAIWorkbench.Samples.SurvivalGuard
{
    /// <summary>
    /// Example custom consideration auto-discovered by TypeCache in UtilityAIWorkbench.
    /// Computes urgency ratio = MissingHealth / MaxHealth so designers can shape heal/flee curves.
    /// </summary>
    [Serializable]
    public sealed class MissingHealthRatioConsideration : UtilityConsiderationBase
    {
        public string HealthKey = "Health";
        public float MaxHealth = 100f;

        public override string DisplayName => string.IsNullOrWhiteSpace(Label) ? "MissingHealthRatio" : Label;

        public override float SampleNormalizedInput(UtilityContext context, out float rawValue)
        {
            var hp = context?.Blackboard != null ? context.Blackboard.GetFloat(HealthKey, MaxHealth) : MaxHealth;
            rawValue = Mathf.Clamp(MaxHealth - hp, 0f, MaxHealth);
            return MaxHealth > 0.001f ? Mathf.Clamp01(rawValue / MaxHealth) : 0f;
        }
    }

    /// <summary>
    /// Example custom action auto-discovered by TypeCache that consumes a medkit and restores health on the Blackboard.
    /// </summary>
    [Serializable]
    public sealed class ConsumeMedkitAction : UtilityActionBase
    {
        public string HealthKey = "Health";
        public string MedkitCountKey = "Medkits";
        public float HealAmount = 45f;
        public float MaxHealth = 100f;
        public float CastTime = 1.0f;

        private float _elapsed;

        public override void OnEnter(UtilityContext context)
        {
            _elapsed = 0f;
        }

        public override UtilityActionStatus OnTick(UtilityContext context)
        {
            if (context?.Blackboard == null)
                return UtilityActionStatus.Failed;

            var kits = context.Blackboard.GetFloat(MedkitCountKey, 0f);
            if (kits < 1f)
                return UtilityActionStatus.Failed;

            _elapsed += context.DeltaTime;
            if (_elapsed < CastTime)
                return UtilityActionStatus.Running;

            var currentHp = context.Blackboard.GetFloat(HealthKey, 0f);
            context.Blackboard.SetFloat(HealthKey, Mathf.Min(MaxHealth, currentHp + HealAmount));
            context.Blackboard.SetFloat(MedkitCountKey, Mathf.Max(0f, kits - 1f));
            return UtilityActionStatus.Completed;
        }
    }
}
