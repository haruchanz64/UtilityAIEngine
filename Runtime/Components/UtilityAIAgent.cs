using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UtilityAIWorkbench.Runtime.Core;
using UtilityAIWorkbench.Runtime.Data;
using UtilityAIWorkbench.Runtime.Engine;

namespace UtilityAIWorkbench.Runtime.Components
{
    [Serializable]
    public struct InitialFloatFact
    {
        public string Key;
        public float Value;
    }

    [Serializable]
    public struct InitialBoolFact
    {
        public string Key;
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
        public static IReadOnlyList<UtilityAIAgent> ActiveAgents => s_ActiveAgents;
        public static event Action OnActiveAgentsChanged;

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

        [Header("Events")]
        public UnityEvent<string> onBehaviorChanged = new();

        public UtilityBrainAsset Brain => brain;
        public UtilityBlackboard Blackboard { get; private set; }
        public UtilityBrainRunner Runner { get; private set; }
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

        public void StepOnce(float deltaTime = 0.2f)
        {
            if (Runner == null) return;
            Runner.Tick(deltaTime, forceEvaluate: true);
        }

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
