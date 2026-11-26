using System;
using UnityEngine;
using UtilityAIEngine.Runtime.Core;
using UtilityAIEngine.Runtime.Data;

namespace UtilityAIEngine.Runtime.Engine
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

        /// <summary>Evaluation and execution context shared across all considerations and actions.</summary>
        public UtilityContext Context { get; }
        /// <summary>Bounded circular history of recorded <see cref="DecisionSnapshot"/> entries.</summary>
        public DecisionRingBuffer History { get; }
        /// <summary>Currently executing behavior definition, or null if idle.</summary>
        public UtilityBehaviorDefinition ActiveBehavior => _activeBehavior;
        /// <summary>Identifier of <see cref="ActiveBehavior"/>, or <see cref="string.Empty"/> when idle.</summary>
        public string ActiveBehaviorId => _activeBehavior != null ? _activeBehavior.Id : string.Empty;
        /// <summary>Status returned by the most recent action tick (defaults to <see cref="UtilityActionStatus.Running"/>).</summary>
        public UtilityActionStatus LastActionStatus { get; private set; } = UtilityActionStatus.Running;
        /// <summary>Most recently recorded decision snapshot, or null before the first evaluation.</summary>
        public DecisionSnapshot LatestSnapshot => History.Latest;

        /// <summary>Raised after every decision evaluation with the newly recorded snapshot.</summary>
        public event Action<DecisionSnapshot> OnDecisionEvaluated;
        /// <summary>Raised with <c>(previousBehaviorId, nextBehaviorId)</c> whenever the active behavior transitions.</summary>
        public event Action<string, string> OnBehaviorChanged;

        /// <summary>
        /// Creates a stateful runner bound to <paramref name="brain"/>.
        /// </summary>
        /// <param name="brain">Brain asset defining behaviors, selection policy, and hysteresis; must not be null.</param>
        /// <param name="context">Optional shared context; a new <see cref="UtilityContext"/> is created when null.</param>
        /// <param name="targetProvider">Optional provider resolving per-behavior <see cref="UtilityBehaviorDefinition.TargetKey"/> lookups.</param>
        /// <param name="historyCapacity">Maximum snapshots retained in <see cref="History"/>.</param>
        /// <param name="randomSeed">Optional RNG seed forwarded to <see cref="UtilityScorer"/> for deterministic stochastic selection.</param>
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

        /// <summary>
        /// Advances elapsed time, re-evaluates utility when the cadence interval elapses (or when idle/forced),
        /// and ticks the active behavior's action.
        /// </summary>
        /// <param name="deltaTime">Step duration in seconds (clamped to non-negative).</param>
        /// <param name="forceEvaluate">If true, forces a utility re-evaluation on this tick regardless of <see cref="UtilityBrainAsset.EvaluationInterval"/>.</param>
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
                        // Trigger an immediate re-evaluation on the next Tick instead of waiting for EvaluationInterval.
                        _timeUntilNextEvaluation = 0f;
                    }
                }
                else
                {
                    LastActionStatus = UtilityActionStatus.Running;
                }
            }
        }

        /// <summary>
        /// Immediately runs a decision evaluation and behavior transition without ticking the active action, resetting the cadence timer.
        /// </summary>
        /// <returns>The newly recorded <see cref="DecisionSnapshot"/>.</returns>
        public DecisionSnapshot ForceEvaluateNow()
        {
            EvaluateAndTransition();
            _timeUntilNextEvaluation = Mathf.Max(0.02f, _brain.EvaluationInterval);
            return LatestSnapshot;
        }

        /// <summary>
        /// Exits the currently active behavior without applying its cooldown and resets the evaluation timer.
        /// </summary>
        public void Stop()
        {
            ExitActiveBehavior(applyCooldown: false);
            _timeUntilNextEvaluation = 0f;
        }

        /// <summary>
        /// Scores all behaviors, pushes the snapshot to <see cref="History"/>, transitions behaviors when unlocked, and raises runner events.
        /// </summary>
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

        /// <summary>
        /// Invokes <see cref="UtilityActionBase.OnExit"/> on the active behavior, optionally starts its cooldown, and clears active state.
        /// </summary>
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
