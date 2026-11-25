using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UtilityAIWorkbench.Runtime.Core;
using UtilityAIWorkbench.Runtime.Data;
using UtilityAIWorkbench.Runtime.Engine;

namespace UtilityAIWorkbench.Runtime.Components
{
    /// <summary>
    /// Inspector-serialized initial float entry seeded into an agent's <see cref="UtilityBlackboard"/> on initialization.
    /// </summary>
    [Serializable]
    public struct InitialFloatFact
    {
        /// <summary>Case-insensitive blackboard key.</summary>
        public string Key;
        /// <summary>Initial float value written to the blackboard.</summary>
        public float Value;
    }

    /// <summary>
    /// Inspector-serialized initial boolean entry seeded into an agent's <see cref="UtilityBlackboard"/> on initialization.
    /// </summary>
    [Serializable]
    public struct InitialBoolFact
    {
        /// <summary>Case-insensitive blackboard key.</summary>
        public string Key;
        /// <summary>Initial boolean value written to the blackboard.</summary>
        public bool Value;
    }

    /// <summary>
    /// MonoBehaviour driving a <see cref="UtilityBrainAsset"/> at runtime and publishing live diagnostics
    /// to the Utility AI Workbench Live Runtime Debugger.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UtilityAIAgent : MonoBehaviour
    {
        private static readonly List<UtilityAIAgent> s_ActiveAgents = new();
        /// <summary>Read-only list of all currently enabled <see cref="UtilityAIAgent"/> instances in Play Mode.</summary>
        public static IReadOnlyList<UtilityAIAgent> ActiveAgents => s_ActiveAgents;
        /// <summary>Raised whenever an agent registers in <see cref="OnEnable"/> or unregisters in <see cref="OnDisable"/>.</summary>
        public static event Action OnActiveAgentsChanged;

        /// <summary>
        /// Clears static agent tracking before Play Mode starts so Enter Play Mode Options without Domain Reload stay clean.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticRegistry()
        {
            s_ActiveAgents.Clear();
            OnActiveAgentsChanged = null;
        }

        [Header("Brain Configuration")]
        [SerializeField] private UtilityBrainAsset brain;
        [SerializeField] private Transform defaultTarget;
        [SerializeField] private int decisionHistoryCapacity = 128;
        [SerializeField] private bool tickAutomatically = true;

        [Header("Initial Blackboard Facts")]
        [SerializeField] private List<InitialFloatFact> initialFloats = new();
        [SerializeField] private List<InitialBoolFact> initialBools = new();

        /// <summary>UnityEvent invoked with the newly selected behavior ID whenever the runner switches behaviors.</summary>
        [Header("Events")]
        public UnityEvent<string> onBehaviorChanged = new();

        /// <summary>Currently assigned <see cref="UtilityBrainAsset"/>.</summary>
        public UtilityBrainAsset Brain => brain;
        /// <summary>Runtime blackboard shared by this agent's considerations and actions.</summary>
        public UtilityBlackboard Blackboard { get; private set; }
        /// <summary>Active <see cref="UtilityBrainRunner"/> executing the assigned brain, or null if no brain is assigned.</summary>
        public UtilityBrainRunner Runner { get; private set; }
        /// <summary>When true, automatic per-frame ticking in <c>Update</c> is suspended until resumed or stepped via <see cref="StepOnce"/>.</summary>
        public bool IsPaused { get; set; }

        private void Awake()
        {
            InitializeRunner();
        }

        private void OnEnable()
        {
            if (Runner == null)
            {
                InitializeRunner();
            }

            if (!s_ActiveAgents.Contains(this))
            {
                s_ActiveAgents.Add(this);
                OnActiveAgentsChanged?.Invoke();
            }
        }

        private void OnDisable()
        {
            if (s_ActiveAgents.Remove(this))
            {
                OnActiveAgentsChanged?.Invoke();
            }

            Runner?.Stop();
        }

        private void Update()
        {
            if (!tickAutomatically || IsPaused || Runner == null)
                return;

            Runner.Tick(Time.deltaTime);
        }

        /// <summary>
        /// Seeds initial blackboard facts, resolves any sibling <see cref="IUtilityTargetProvider"/>, and constructs a fresh <see cref="UtilityBrainRunner"/>.
        /// </summary>
        /// <param name="overrideBrain">Optional brain asset replacing the Inspector-assigned <see cref="Brain"/> before runner creation.</param>
        public void InitializeRunner(UtilityBrainAsset overrideBrain = null)
        {
            if (overrideBrain != null)
            {
                brain = overrideBrain;
            }

            Blackboard ??= new UtilityBlackboard();
            for (var i = 0; i < initialFloats.Count; i++)
            {
                Blackboard.SetFloat(initialFloats[i].Key, initialFloats[i].Value);
            }

            for (var i = 0; i < initialBools.Count; i++)
            {
                Blackboard.SetBool(initialBools[i].Key, initialBools[i].Value);
            }

            if (brain == null)
            {
                Runner = null;
                return;
            }

            var context = new UtilityContext(Blackboard, gameObject)
            {
                Target = defaultTarget
            };

            var targetProvider = GetComponent<IUtilityTargetProvider>();
            Runner = new UtilityBrainRunner(brain, context, targetProvider, Mathf.Max(8, decisionHistoryCapacity));
            Runner.OnBehaviorChanged += (_, nextId) => onBehaviorChanged?.Invoke(nextId);
        }

        /// <summary>
        /// Advances the runner by one forced evaluation tick, typically called while paused from the Live Runtime Debugger.
        /// </summary>
        /// <param name="deltaTime">Simulated step duration in seconds.</param>
        public void StepOnce(float deltaTime = 0.2f)
        {
            if (Runner == null) return;
            Runner.Tick(deltaTime, forceEvaluate: true);
        }

        /// <summary>
        /// Updates the default target transform and immediately propagates it to the active runner context if initialized.
        /// </summary>
        /// <param name="target">New target transform, or null to clear the default target.</param>
        public void SetTarget(Transform target)
        {
            defaultTarget = target;
            if (Runner != null)
            {
                Runner.Context.Target = target;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (Runner?.Context?.Target)
            {
                Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.8f);
                Gizmos.DrawLine(transform.position, Runner.Context.Target.position);
                Gizmos.DrawWireSphere(Runner.Context.Target.position, 0.35f);
            }
        }
    }
}
