using System;
using UnityEngine;
using UtilityAIWorkbench.Runtime.Core;
using UtilityAIWorkbench.Runtime.Data;

namespace UtilityAIWorkbench.Runtime.Engine
{
    /// <summary>
    /// Stateful runtime runner that evaluates a <see cref="UtilityBrainAsset"/> on cadence,
    /// manages active action lifecycle (OnEnter / OnTick / OnExit), commitment timers, cooldowns, and diagnostic history.
    /// </summary>
    public sealed class UtilityBrainRunner
    {
        private readonly UtilityBrainAsset _brain;
        private readonly UtilityScorer _scorer;
        private readonly IUtilityTargetProvider _targetProvider;

        private UtilityBehaviorDefinition _activeBehavior;
        private float _activeDuration;
        private float _timeUntilNextEvaluation;
        private int _tickCounter;

        public UtilityContext Context { get; }
        public DecisionRingBuffer History { get; }
        public UtilityBehaviorDefinition ActiveBehavior => _activeBehavior;
        public string ActiveBehaviorId => _activeBehavior != null ? _activeBehavior.Id : string.Empty;
        public UtilityActionStatus LastActionStatus { get; private set; } = UtilityActionStatus.Running;
        public DecisionSnapshot LatestSnapshot => History.Latest;

        public event Action<DecisionSnapshot> OnDecisionEvaluated;
        public event Action<string, string> OnBehaviorChanged;

        public UtilityBrainRunner(
            UtilityBrainAsset brain,
            UtilityContext context = null,
            IUtilityTargetProvider targetProvider = null,
            int historyCapacity = 128,
            int? randomSeed = null)
        {
            _brain = brain ?? throw new ArgumentNullException(nameof(brain));
            Context = context ?? new UtilityContext();
            _targetProvider = targetProvider;
            _scorer = new UtilityScorer(randomSeed);
            History = new DecisionRingBuffer(historyCapacity);
        }

        public void Tick(float deltaTime, bool forceEvaluate = false)
        {
            deltaTime = Mathf.Max(0f, deltaTime);
            Context.DeltaTime = deltaTime;
            Context.ElapsedTime += deltaTime;

            _timeUntilNextEvaluation -= deltaTime;

            var shouldEvaluate = forceEvaluate || _activeBehavior == null || _timeUntilNextEvaluation <= 0f;
            if (shouldEvaluate)
            {
                EvaluateAndTransition();
                _timeUntilNextEvaluation = Mathf.Max(0.02f, _brain.EvaluationInterval);
            }

            if (_activeBehavior != null)
            {
                _activeDuration += deltaTime;
                if (_targetProvider != null && !string.IsNullOrEmpty(_activeBehavior.TargetKey))
                {
                    Context.Target = _targetProvider.ResolveTarget(Context, _activeBehavior.TargetKey);
                }

                if (_activeBehavior.Action != null)
                {
                    LastActionStatus = _activeBehavior.Action.OnTick(Context);
                    if (LastActionStatus == UtilityActionStatus.Completed || LastActionStatus == UtilityActionStatus.Failed)
                    {
                        ExitActiveBehavior(applyCooldown: true);
                        _timeUntilNextEvaluation = 0f;
                    }
                }
                else
                {
                    LastActionStatus = UtilityActionStatus.Running;
                }
            }
        }

        public DecisionSnapshot ForceEvaluateNow()
        {
            EvaluateAndTransition();
            _timeUntilNextEvaluation = Mathf.Max(0.02f, _brain.EvaluationInterval);
            return LatestSnapshot;
        }

        public void Stop()
        {
            ExitActiveBehavior(applyCooldown: false);
            _timeUntilNextEvaluation = 0f;
        }

        private void EvaluateAndTransition()
        {
            _tickCounter++;

            var locked = _activeBehavior != null &&
                         _activeBehavior.MinCommitmentSeconds > 0f &&
                         _activeDuration < _activeBehavior.MinCommitmentSeconds;

            var currentId = _activeBehavior != null ? _activeBehavior.Id : null;
            var snapshot = _scorer.Evaluate(_brain, Context, _tickCounter, currentId, locked, _targetProvider);
            History.Push(snapshot);

            if (!locked && !string.Equals(snapshot.SelectedBehaviorId, currentId, StringComparison.OrdinalIgnoreCase))
            {
                var previousId = currentId ?? string.Empty;
                ExitActiveBehavior(applyCooldown: true);

                var nextBehavior = _brain.FindBehavior(snapshot.SelectedBehaviorId);
                if (nextBehavior != null)
                {
                    _activeBehavior = nextBehavior;
                    _activeDuration = 0f;
                    Context.ActiveBehaviorId = _activeBehavior.Id;

                    if (_targetProvider != null && !string.IsNullOrEmpty(_activeBehavior.TargetKey))
                    {
                        Context.Target = _targetProvider.ResolveTarget(Context, _activeBehavior.TargetKey);
                    }

                    _activeBehavior.Action?.OnEnter(Context);
                    LastActionStatus = UtilityActionStatus.Running;
                }

                OnBehaviorChanged?.Invoke(previousId, ActiveBehaviorId);
            }

            OnDecisionEvaluated?.Invoke(snapshot);
        }

        private void ExitActiveBehavior(bool applyCooldown)
        {
            if (_activeBehavior == null) return;

            _activeBehavior.Action?.OnExit(Context);
            if (applyCooldown && _activeBehavior.CooldownSeconds > 0f)
            {
                Context.SetCooldown(_activeBehavior.Id, _activeBehavior.CooldownSeconds);
            }

            _activeBehavior = null;
            _activeDuration = 0f;
            Context.ActiveBehaviorId = string.Empty;
        }
    }
}
