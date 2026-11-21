using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UtilityAIWorkbench.Editor.Export;
using UtilityAIWorkbench.Runtime.Considerations;
using UtilityAIWorkbench.Runtime.Core;
using UtilityAIWorkbench.Runtime.Data;
using UtilityAIWorkbench.Runtime.Engine;

namespace UtilityAIWorkbench.Tests
{
    public sealed class UtilityScorerAndRunnerTests
    {
        private sealed class SpyAction : UtilityActionBase
        {
            public int EnterCount;
            public int TickCount;
            public int ExitCount;
            public int CompleteAfterTicks = int.MaxValue;

            public override void OnEnter(UtilityContext context) => EnterCount++;

            public override UtilityActionStatus OnTick(UtilityContext context)
            {
                TickCount++;
                return TickCount >= CompleteAfterTicks ? UtilityActionStatus.Completed : UtilityActionStatus.Running;
            }

            public override void OnExit(UtilityContext context) => ExitCount++;
        }

        private UtilityBrainAsset _brain;

        [TearDown]
        public void TearDown()
        {
            if (_brain != null)
            {
                Object.DestroyImmediate(_brain);
                _brain = null;
            }
        }

        [Test]
        public void AggregateScores_CompensatedProductOutperformsRawProductForMultipleConsiderations()
        {
            var records = new List<ConsiderationScoreRecord>
            {
                new("C1", 0.8f, 0.8f, 0.8f, false, ResponseCurveType.Linear),
                new("C2", 0.8f, 0.8f, 0.8f, false, ResponseCurveType.Linear),
                new("C3", 0.8f, 0.8f, 0.8f, false, ResponseCurveType.Linear)
            };

            var raw = UtilityScorer.AggregateScores(records, ScoreAggregationMode.RawProduct);
            var compensated = UtilityScorer.AggregateScores(records, ScoreAggregationMode.CompensatedProduct);
            var avg = UtilityScorer.AggregateScores(records, ScoreAggregationMode.Average);

            Assert.AreEqual(0.512f, raw, 0.001f);
            Assert.Greater(compensated, raw);
            Assert.LessOrEqual(compensated, 1f);
            Assert.AreEqual(0.8f, avg, 0.001f);
        }

        [Test]
        public void Scorer_RespectsVetoCooldownAndInertiaHysteresis()
        {
            _brain = ScriptableObject.CreateInstance<UtilityBrainAsset>();
            _brain.InertiaBonus = 0.15f;
            _brain.MinSwitchDelta = 0.05f;

            _brain.Behaviors.Add(new UtilityBehaviorDefinition
            {
                Id = "attack",
                DisplayName = "Attack",
                Weight = 1f,
                Considerations = new List<UtilityConsiderationBase>
                {
                    new BlackboardBoolConsideration { Key = "HasTarget", ExpectedValue = true, CanVeto = true },
                    new ConstantConsideration { Value = 0.70f }
                }
            });

            _brain.Behaviors.Add(new UtilityBehaviorDefinition
            {
                Id = "heal",
                DisplayName = "Heal",
                Weight = 1f,
                Considerations = new List<UtilityConsiderationBase>
                {
                    new ConstantConsideration { Value = 0.78f }
                }
            });

            var ctx = new UtilityContext();
            ctx.Blackboard.SetBool("HasTarget", false);
            var scorer = new UtilityScorer(seed: 42);

            // Attack is vetoed when HasTarget is false -> Heal wins.
            var snap1 = scorer.Evaluate(_brain, ctx, tickNumber: 1);
            Assert.AreEqual("heal", snap1.SelectedBehaviorId);
            Assert.That(snap1.Evaluations[0].DisqualificationReason, Does.Contain("Vetoed"));

            // With HasTarget = true and attack active, inertia (+0.15 -> 0.85) keeps attack ahead of heal (0.78).
            ctx.Blackboard.SetBool("HasTarget", true);
            var snap2 = scorer.Evaluate(_brain, ctx, tickNumber: 2, currentBehaviorId: "attack");
            Assert.AreEqual("attack", snap2.SelectedBehaviorId);
            Assert.IsFalse(snap2.SwitchedBehavior);

            // Put attack on cooldown -> Heal must win.
            ctx.SetCooldown("attack", 5f);
            var snap3 = scorer.Evaluate(_brain, ctx, tickNumber: 3, currentBehaviorId: "attack");
            Assert.AreEqual("heal", snap3.SelectedBehaviorId);
            Assert.IsTrue(snap3.Evaluations[0].OnCooldown);
        }

        [Test]
        public void BrainRunner_ManagesLifecycleCommitmentCooldownAndExportsTrace()
        {
            _brain = ScriptableObject.CreateInstance<UtilityBrainAsset>();
            _brain.EvaluationInterval = 0.1f;
            _brain.InertiaBonus = 0f;
            _brain.MinSwitchDelta = 0f;

            var chargeAction = new SpyAction { CompleteAfterTicks = 3 };
            var patrolAction = new SpyAction();

            _brain.Behaviors.Add(new UtilityBehaviorDefinition
            {
                Id = "charge",
                DisplayName = "Charge",
                Weight = 1f,
                CooldownSeconds = 2f,
                MinCommitmentSeconds = 0.5f,
                Considerations = new List<UtilityConsiderationBase>
                {
                    new BlackboardFloatConsideration { Key = "Aggro", MinValue = 0f, MaxValue = 1f }
                },
                Action = chargeAction
            });

            _brain.Behaviors.Add(new UtilityBehaviorDefinition
            {
                Id = "patrol",
                DisplayName = "Patrol",
                Weight = 1f,
                Considerations = new List<UtilityConsiderationBase>
                {
                    new ConstantConsideration { Value = 0.5f }
                },
                Action = patrolAction
            });

            var ctx = new UtilityContext();
            ctx.Blackboard.SetFloat("Aggro", 0.9f);

            var runner = new UtilityBrainRunner(_brain, ctx, randomSeed: 7);

            // Tick 1: enters Charge
            runner.Tick(0.2f);
            Assert.AreEqual("charge", runner.ActiveBehaviorId);
            Assert.AreEqual(1, chargeAction.EnterCount);
            Assert.AreEqual(1, chargeAction.TickCount);

            // Drop Aggro below Patrol, but MinCommitmentSeconds (0.5s) locks Charge during tick 2 (0.2s < 0.5s).
            ctx.Blackboard.SetFloat("Aggro", 0.1f);
            runner.Tick(0.2f);
            Assert.AreEqual("charge", runner.ActiveBehaviorId);
            Assert.IsTrue(runner.LatestSnapshot.LockedByCommitment);

            // Tick 3: Charge completes on its 3rd tick, exits, enters cooldown, and next tick switches to Patrol.
            runner.Tick(0.2f);
            Assert.AreEqual(1, chargeAction.ExitCount);
            Assert.IsTrue(ctx.IsOnCooldown("charge"));

            runner.Tick(0.2f);
            Assert.AreEqual("patrol", runner.ActiveBehaviorId);
            Assert.AreEqual(1, patrolAction.EnterCount);

            // Export recorded decision history to JSON & CSV
            var history = runner.History.ToChronologicalList();
            Assert.GreaterOrEqual(history.Count, 3);

            var json = UtilityDecisionExporter.ToJson(history, prettyPrint: false);
            var csv = UtilityDecisionExporter.ToCsv(history);
            Assert.That(json, Does.Contain("\"selectedBehaviorId\":\"charge\""));
            Assert.That(csv, Does.Contain("Charge"));
            Assert.That(csv, Does.Contain("Patrol"));
        }
    }
}
