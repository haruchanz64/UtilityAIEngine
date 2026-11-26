using UnityEditor;
using UnityEngine;
using UtilityAIEngine.Editor.UI;
using UtilityAIEngine.Runtime.Components;
using UtilityAIEngine.Runtime.Data;

namespace UtilityAIEngine.Editor.Inspectors
{
    /// <summary>
    /// Custom Inspector for <see cref="UtilityBrainAsset"/> adding a shortcut button to open the asset in <see cref="UtilityAIWorkbenchWindow"/>.
    /// </summary>
    [CustomEditor(typeof(UtilityBrainAsset))]
    public sealed class UtilityBrainAssetInspector : UnityEditor.Editor
    {
        /// <summary>
        /// Draws the Workbench launch button above the default serialized Inspector fields.
        /// </summary>
        public override void OnInspectorGUI()
        {
            var brain = (UtilityBrainAsset)target;

            if (GUILayout.Button("Open in Utility AI Workbench", GUILayout.Height(30)))
            {
                UtilityAIWorkbenchWindow.OpenWithBrain(brain);
            }

            EditorGUILayout.Space(6);
            DrawDefaultInspector();
        }
    }

    /// <summary>
    /// Custom Inspector for <see cref="UtilityAIAgent"/> adding a Live Runtime Debugger button and an inline Play Mode status summary.
    /// </summary>
    [CustomEditor(typeof(UtilityAIAgent))]
    public sealed class UtilityAIAgentInspector : UnityEditor.Editor
    {
        /// <summary>
        /// Draws the Live Runtime Debugger shortcut, live Play Mode active-behavior summary, and default Inspector fields.
        /// </summary>
        public override void OnInspectorGUI()
        {
            var agent = (UtilityAIAgent)target;

            if (GUILayout.Button("Open Live Runtime Debugger", GUILayout.Height(30)))
            {
                UtilityAIWorkbenchWindow.OpenDebuggerForAgent(agent);
            }

            if (Application.isPlaying && agent.Runner != null)
            {
                var snap = agent.Runner.LatestSnapshot;
                var status = snap != null
                    ? $"Active: {(string.IsNullOrEmpty(snap.SelectedBehaviorName) ? "(None)" : snap.SelectedBehaviorName)}  |  Score: {snap.SelectedScore:F3}  |  Tick #{snap.TickNumber}"
                    : "Waiting for first evaluation...";
                EditorGUILayout.HelpBox(status, MessageType.Info);
            }

            EditorGUILayout.Space(6);
            DrawDefaultInspector();
        }
    }
}
