#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UtilityAIEngine.Editor.UI;
using UtilityAIEngine.Runtime.Actions;
using UtilityAIEngine.Runtime.Considerations;
using UtilityAIEngine.Runtime.Core;
using UtilityAIEngine.Runtime.Data;

namespace UtilityAIEngine.Samples.SurvivalGuard
{
    /// <summary>
    /// Editor utility generating a ready-to-inspect Survival Guard <see cref="UtilityBrainAsset"/> with four configured behaviors.
    /// </summary>
    public static class GuardBrainDemoGenerator
    {
        /// <summary>
        /// Prompts for an asset path, creates the Survival Guard demo brain asset, and opens it in <see cref="UtilityAIWorkbenchWindow"/>.
        /// </summary>
        [MenuItem("Tools/HaruChanZ64/AI/UtilityAIEngine/Create Survival Guard Demo Brain", false, 101)]
        public static void CreateDemoBrain()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Survival Guard Demo Brain",
                "SurvivalGuardBrain",
                "asset",
                "Save the Survival Guard UtilityBrainAsset.");

            if (string.IsNullOrEmpty(path)) return;

            var brain = ScriptableObject.CreateInstance<UtilityBrainAsset>();
            brain.Description = "4-behavior survival guard AI demonstrating compensated scoring, response curves, cooldowns, and hysteresis.";
            brain.InertiaBonus = 0.08f;
            brain.MinSwitchDelta = 0.03f;
            brain.EvaluationInterval = 0.2f;

            // 1. Attack Target: wants target visible, good ammo, decent health
            brain.Behaviors.Add(new UtilityBehaviorDefinition
            {
                Id = "attack",
                DisplayName = "Attack Target",
                Weight = 1.0f,
                Considerations = new List<UtilityConsiderationBase>
                {
                    new BlackboardBoolConsideration { Label = "HasTarget", Key = "HasTarget", ExpectedValue = true, CanVeto = true },
                    new BlackboardFloatConsideration { Label = "Ammo", Key = "Ammo", MinValue = 0f, MaxValue = 30f, Curve = ResponseCurve.Logistic(10f, 0.2f) },
                    new BlackboardFloatConsideration { Label = "Health", Key = "Health", MinValue = 0f, MaxValue = 100f, Curve = ResponseCurve.Logistic(8f, 0.35f) }
                },
                Action = new DeltaBlackboardFloatAction { DisplayNameOverride = "FireBurst", Key = "Ammo", RatePerSecond = -4f, MinValue = 0f, MaxValue = 30f }
            });

            // 2. Heal Self: urgent when health drops low and medkits > 0
            brain.Behaviors.Add(new UtilityBehaviorDefinition
            {
                Id = "heal",
                DisplayName = "Use Medkit",
                Weight = 1.15f,
                CooldownSeconds = 6f,
                MinCommitmentSeconds = 1.0f,
                Considerations = new List<UtilityConsiderationBase>
                {
                    new BlackboardFloatConsideration { Label = "LowHealth", Key = "Health", MinValue = 0f, MaxValue = 100f, Curve = ResponseCurve.Polynomial(2.2f, invert: true) },
                    new BlackboardFloatConsideration { Label = "HasMedkit", Key = "Medkits", MinValue = 0f, MaxValue = 3f, Curve = ResponseCurve.Step(0.3f), CanVeto = true }
                },
                Action = new ConsumeMedkitAction { DisplayNameOverride = "ConsumeMedkit" }
            });

            // 3. Flee Threat: spikes when health is critical and threat is present
            brain.Behaviors.Add(new UtilityBehaviorDefinition
            {
                Id = "flee",
                DisplayName = "Flee Threat",
                Weight = 1.25f,
                MinCommitmentSeconds = 1.5f,
                Considerations = new List<UtilityConsiderationBase>
                {
                    new BlackboardBoolConsideration { Label = "HasTarget", Key = "HasTarget", ExpectedValue = true, CanVeto = true },
                    new BlackboardFloatConsideration { Label = "CriticalHealth", Key = "Health", MinValue = 0f, MaxValue = 100f, Curve = ResponseCurve.Logistic(-14f, 0.25f) }
                },
                Action = new LogDebugAction { DisplayNameOverride = "RetreatToCover", Message = "Retreating to cover!" }
            });

            // 4. Patrol: baseline fallback when no urgent threats exist
            brain.Behaviors.Add(new UtilityBehaviorDefinition
            {
                Id = "patrol",
                DisplayName = "Patrol Area",
                Weight = 0.35f,
                Considerations = new List<UtilityConsiderationBase>
                {
                    new ConstantConsideration { Label = "Baseline", Value = 0.6f, CanVeto = false }
                },
                Action = new WaitAction { DisplayNameOverride = "PatrolWaypoint", Duration = 2f }
            });

            AssetDatabase.CreateAsset(brain, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UtilityAIWorkbenchWindow.OpenWithBrain(brain);
        }
    }
}
#endif
