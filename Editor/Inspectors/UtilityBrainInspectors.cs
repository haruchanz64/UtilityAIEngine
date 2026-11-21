using UnityEditor;
using UnityEngine;
using UtilityAIWorkbench.Editor.UI;
using UtilityAIWorkbench.Runtime.Components;
using UtilityAIWorkbench.Runtime.Data;

namespace UtilityAIWorkbench.Editor.Inspectors
{
    [CustomEditor(typeof(UtilityBrainAsset))]
    public sealed class UtilityBrainAssetInspector : UnityEditor.Editor
    {
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

    [CustomEditor(typeof(UtilityAIAgent))]
    public sealed class UtilityAIAgentInspector : UnityEditor.Editor
    {
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
