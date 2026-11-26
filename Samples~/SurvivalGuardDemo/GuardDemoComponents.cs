using System;
using UnityEngine;
using UtilityAIEngine.Runtime.Core;

namespace UtilityAIEngine.Samples.SurvivalGuard
{
    /// <summary>
    /// Example custom consideration auto-discovered by TypeCache in UtilityAIEngine.
    /// Computes urgency ratio = MissingHealth / MaxHealth so designers can shape heal and flee curves.
    /// </summary>
    [Serializable]
    public sealed class MissingHealthRatioConsideration : UtilityConsiderationBase
    {
        /// <summary>Blackboard float key storing current health.</summary>
        public string HealthKey = "Health";
        /// <summary>Maximum health denominator used to compute missing-health ratio.</summary>
        public float MaxHealth = 100f;

        /// <inheritdoc/>
        public override string DisplayName => string.IsNullOrWhiteSpace(Label) ? "MissingHealthRatio" : Label;

        /// <summary>
        /// Computes missing health (<c>MaxHealth - currentHealth</c>) and normalizes it by <see cref="MaxHealth"/> into [0..1].
        /// </summary>
        public override float SampleNormalizedInput(UtilityContext context, out float rawValue)
        {
            var hp = context?.Blackboard != null ? context.Blackboard.GetFloat(HealthKey, MaxHealth) : MaxHealth;
            rawValue = Mathf.Clamp(MaxHealth - hp, 0f, MaxHealth);
            return MaxHealth > 0.001f ? Mathf.Clamp01(rawValue / MaxHealth) : 0f;
        }
    }

    /// <summary>
    /// Example custom action auto-discovered by TypeCache that casts for <see cref="CastTime"/> seconds, consumes one medkit, and restores health on the Blackboard.
    /// </summary>
    [Serializable]
    public sealed class ConsumeMedkitAction : UtilityActionBase
    {
        /// <summary>Blackboard float key updated with restored health.</summary>
        public string HealthKey = "Health";
        /// <summary>Blackboard float key tracking remaining medkit inventory.</summary>
        public string MedkitCountKey = "Medkits";
        /// <summary>Health points restored when the cast finishes.</summary>
        public float HealAmount = 45f;
        /// <summary>Maximum health clamp applied after healing.</summary>
        public float MaxHealth = 100f;
        /// <summary>Duration in seconds required to finish using a medkit.</summary>
        public float CastTime = 1.0f;

        private float _elapsed;

        /// <summary>
        /// Resets the medkit cast timer when the action starts.
        /// </summary>
        public override void OnEnter(UtilityContext context)
        {
            _elapsed = 0f;
        }

        /// <summary>
        /// Fails if fewer than one medkit remains, ticks until <see cref="CastTime"/> elapses, then restores health and decrements medkits.
        /// </summary>
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
