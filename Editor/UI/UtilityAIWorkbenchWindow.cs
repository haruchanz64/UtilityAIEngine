using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UtilityAIWorkbench.Editor.Export;
using UtilityAIWorkbench.Runtime.Components;
using UtilityAIWorkbench.Runtime.Considerations;
using UtilityAIWorkbench.Runtime.Core;
using UtilityAIWorkbench.Runtime.Data;
using UtilityAIWorkbench.Runtime.Engine;

namespace UtilityAIWorkbench.Editor.UI
{
    /// <summary>
    /// Three-tab UI Toolkit EditorWindow providing brain authoring with live curve plotting,
    /// an EditMode What-If Simulator, and a Play Mode Live Runtime Debugger with trace export.
    /// </summary>
    public sealed class UtilityAIWorkbenchWindow : EditorWindow
    {
        private enum WorkbenchTab
        {
            Authoring = 0,
            Simulator = 1,
            LiveDebugger = 2
        }

        private const string PackageStyleSheetPath = "Packages/com.haruchanz64.utility-ai-workbench/Editor/UI/UtilityAIStyles.uss";
        private const string StyleSheetRelativePath = "Editor/UI/UtilityAIStyles.uss";
        private const float NarrowWindowBreakpoint = 760f;
        private const float StackedSplitBreakpoint = 640f;

        private WorkbenchTab _activeTab = WorkbenchTab.Authoring;
        private UtilityBrainAsset _brain;
        private SerializedObject _brainSerializedObject;
        private int _selectedBehaviorIndex = -1;
        private int _selectedConsiderationIndex = -1;
        private float _curveProbeX = 0.5f;

        // Resizable split state (persisted across tab rebuilds)
        private float _horizontalSplitSize = 270f;
        private float _verticalSplitSize = 220f;
        private bool _isStackedSplit;

        // Simulator state
        private readonly UtilityBlackboard _simBlackboard = new();
        private readonly UtilityScorer _simScorer = new(seed: 1337);
        private readonly Dictionary<string, (float min, float max)> _simFloatRanges = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _simBoolKeys = new(StringComparer.OrdinalIgnoreCase);
        private string _newSimFactKey = "CustomStat";
        private float _newSimFactValue = 50f;

        // Live Debugger state
        private UtilityAIAgent _inspectedAgent;
        private int _selectedHistoryIndex = -1;
        private double _lastLiveRefreshTime;

        // UI Elements
        private VisualElement _root;
        private Button _authoringNavBtn;
        private Button _simulatorNavBtn;
        private Button _debuggerNavBtn;
        private VisualElement _contentHost;
        private ObjectField _brainField;

        /// <summary>
        /// Opens or focuses the Utility AI Workbench window from the Unity Tools menu.
        /// </summary>
        [MenuItem("Tools/HaruChanZ64/AI/UtilityAIWorkbench/Utility AI Workbench", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<UtilityAIWorkbenchWindow>("Utility AI Workbench");
            window.minSize = new Vector2(380, 340);
        }

        /// <summary>
        /// Opens the Workbench window bound to <paramref name="brain"/> on the Authoring &amp; Curves tab.
        /// </summary>
        /// <param name="brain">Brain asset to inspect and edit.</param>
        public static void OpenWithBrain(UtilityBrainAsset brain)
        {
            var window = GetWindow<UtilityAIWorkbenchWindow>("Utility AI Workbench");
            window.minSize = new Vector2(380, 340);
            window.SetBrain(brain);
            window.SwitchTab(WorkbenchTab.Authoring);
        }

        /// <summary>
        /// Opens the Workbench window on the Live Runtime Debugger tab focused on <paramref name="agent"/>.
        /// </summary>
        /// <param name="agent">Scene agent to inspect in Play Mode.</param>
        public static void OpenDebuggerForAgent(UtilityAIAgent agent)
        {
            var window = GetWindow<UtilityAIWorkbenchWindow>("Utility AI Workbench");
            window.minSize = new Vector2(380, 340);
            window._inspectedAgent = agent;
            if (agent != null && agent.Brain != null)
            {
                window.SetBrain(agent.Brain);
            }

            window.SwitchTab(WorkbenchTab.LiveDebugger);
        }

        private void OnEnable()
        {
            UtilityAIAgent.OnActiveAgentsChanged += OnActiveAgentsChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            UtilityAIAgent.OnActiveAgentsChanged -= OnActiveAgentsChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
            _brainSerializedObject?.Dispose();
            _brainSerializedObject = null;
        }

        private void OnActiveAgentsChanged()
        {
            if (_activeTab == WorkbenchTab.LiveDebugger)
            {
                RebuildActiveTab();
            }
        }

        /// <summary>
        /// Resets history selection and auto-selects the first active agent when transitioning into or out of Play Mode.
        /// </summary>
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                _selectedHistoryIndex = -1;
                if (state == PlayModeStateChange.EnteredPlayMode && UtilityAIAgent.ActiveAgents.Count > 0)
                {
                    _inspectedAgent ??= UtilityAIAgent.ActiveAgents[0];
                }

                RebuildActiveTab();
            }
        }

        /// <summary>
        /// Polls live debugger updates at roughly 6.6 Hz while unpaused and viewing the latest snapshot in Play Mode.
        /// </summary>
        private void OnEditorUpdate()
        {
            if (_activeTab != WorkbenchTab.LiveDebugger || !Application.isPlaying)
                return;

            if (EditorApplication.timeSinceStartup - _lastLiveRefreshTime < 0.15)
                return;

            _lastLiveRefreshTime = EditorApplication.timeSinceStartup;
            if (_inspectedAgent != null && !_inspectedAgent.IsPaused && _selectedHistoryIndex < 0)
            {
                RebuildActiveTab();
            }
        }

        /// <summary>
        /// Builds the persistent UI Toolkit header, tab navigation sidebar, and active content host.
        /// </summary>
        public void CreateGUI()
        {
            rootVisualElement.Clear();

            _root = new VisualElement();
            _root.AddToClassList("uai-root");
            ApplyStyleSheet(_root);
            rootVisualElement.Add(_root);

            _root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);

            // Header
            var header = new VisualElement();
            header.AddToClassList("uai-header");

            var titleBlock = new VisualElement();
            titleBlock.AddToClassList("uai-header-title-block");
            var title = new Label("Utility AI Workbench");
            title.AddToClassList("uai-header-title");
            var subtitle = new Label("Response Curves · Compensated Scoring · What-If Simulator · Live Debugger");
            subtitle.AddToClassList("uai-header-subtitle");
            titleBlock.Add(title);
            titleBlock.Add(subtitle);
            header.Add(titleBlock);

            var brainRow = new VisualElement();
            brainRow.AddToClassList("uai-header-controls");

            _brainField = new ObjectField("Brain")
            {
                objectType = typeof(UtilityBrainAsset),
                allowSceneObjects = false,
                value = _brain
            };
            _brainField.AddToClassList("uai-brain-field");
            _brainField.RegisterValueChangedCallback(evt => SetBrain(evt.newValue as UtilityBrainAsset));
            brainRow.Add(_brainField);

            var createBrainBtn = new Button(CreateNewBrainAsset) { text = "New Brain" };
            createBrainBtn.style.flexShrink = 0;
            brainRow.Add(createBrainBtn);

            header.Add(brainRow);
            _root.Add(header);

            // Body
            var body = new VisualElement();
            body.AddToClassList("uai-body");

            var sidebar = new VisualElement();
            sidebar.AddToClassList("uai-sidebar");

            _authoringNavBtn = CreateNavButton("Authoring & Curves", WorkbenchTab.Authoring);
            _simulatorNavBtn = CreateNavButton("What-If Simulator", WorkbenchTab.Simulator);
            _debuggerNavBtn = CreateNavButton("Live Runtime Debugger", WorkbenchTab.LiveDebugger);

            sidebar.Add(_authoringNavBtn);
            sidebar.Add(_simulatorNavBtn);
            sidebar.Add(_debuggerNavBtn);
            body.Add(sidebar);

            _contentHost = new VisualElement();
            _contentHost.AddToClassList("uai-content");
            body.Add(_contentHost);

            _root.Add(body);
            UpdateNavButtons();
            RebuildActiveTab();
        }

        /// <summary>
        /// Toggles responsive narrow and stacked-split USS classes when the window width crosses layout breakpoints.
        /// </summary>
        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            var width = evt.newRect.width;
            if (width <= 1f) return;

            var isNarrow = width < NarrowWindowBreakpoint;
            var shouldStack = width < StackedSplitBreakpoint;

            _root.EnableInClassList("uai-root--narrow", isNarrow);
            _root.EnableInClassList("uai-root--stacked", shouldStack);

            if (shouldStack != _isStackedSplit)
            {
                _isStackedSplit = shouldStack;
                RebuildActiveTab();
            }
        }

        /// <summary>
        /// Wraps two panes in a horizontal or stacked <see cref="TwoPaneSplitView"/> while persisting user drag sizes across tab rebuilds.
        /// </summary>
        private TwoPaneSplitView CreateResizableSplit(VisualElement firstPane, VisualElement secondPane)
        {
            var orientation = _isStackedSplit
                ? TwoPaneSplitViewOrientation.Vertical
                : TwoPaneSplitViewOrientation.Horizontal;

            var initialSize = _isStackedSplit
                ? Mathf.Clamp(_verticalSplitSize, 130f, 500f)
                : Mathf.Clamp(_horizontalSplitSize, 180f, 520f);

            var split = new TwoPaneSplitView(0, initialSize, orientation);
            split.AddToClassList("uai-split-view");

            firstPane.AddToClassList("uai-split-pane");
            secondPane.AddToClassList("uai-split-pane");
            secondPane.AddToClassList("uai-split-pane--right");

            // Track user drags on the splitter so tab rebuilds preserve their chosen pane size.
            firstPane.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (_isStackedSplit)
                {
                    if (evt.newRect.height >= 120f) _verticalSplitSize = evt.newRect.height;
                }
                else
                {
                    if (evt.newRect.width >= 160f) _horizontalSplitSize = evt.newRect.width;
                }
            });

            split.Add(firstPane);
            split.Add(secondPane);
            return split;
        }

        private Button CreateNavButton(string label, WorkbenchTab tab)
        {
            var btn = new Button(() => SwitchTab(tab)) { text = label };
            btn.AddToClassList("uai-nav-button");
            return btn;
        }

        private void SwitchTab(WorkbenchTab tab)
        {
            _activeTab = tab;
            UpdateNavButtons();
            RebuildActiveTab();
        }

        private void UpdateNavButtons()
        {
            _authoringNavBtn?.EnableInClassList("uai-nav-button--active", _activeTab == WorkbenchTab.Authoring);
            _simulatorNavBtn?.EnableInClassList("uai-nav-button--active", _activeTab == WorkbenchTab.Simulator);
            _debuggerNavBtn?.EnableInClassList("uai-nav-button--active", _activeTab == WorkbenchTab.LiveDebugger);
        }

        /// <summary>
        /// Binds a new <see cref="UtilityBrainAsset"/>, resets selection indices, syncs simulator facts, and rebuilds the active tab.
        /// </summary>
        private void SetBrain(UtilityBrainAsset brain)
        {
            _brain = brain;
            _brainSerializedObject?.Dispose();
            _brainSerializedObject = _brain != null ? new SerializedObject(_brain) : null;
            _selectedBehaviorIndex = _brain != null && _brain.Behaviors.Count > 0 ? 0 : -1;
            _selectedConsiderationIndex = 0;

            if (_brainField != null && _brainField.value != _brain)
            {
                _brainField.SetValueWithoutNotify(_brain);
            }

            SyncSimulatorFactsFromBrain();
            RebuildActiveTab();
        }

        /// <summary>
        /// Prompts the user for an asset location, creates a starter <see cref="UtilityBrainAsset"/> with a baseline idle behavior, and selects it.
        /// </summary>
        private void CreateNewBrainAsset()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Utility Brain Asset",
                "NewUtilityBrain",
                "asset",
                "Choose a location for the new UtilityBrainAsset.");

            if (string.IsNullOrEmpty(path)) return;

            var asset = CreateInstance<UtilityBrainAsset>();
            asset.Behaviors.Add(new UtilityBehaviorDefinition
            {
                Id = "idle",
                DisplayName = "Idle / Patrol",
                Weight = 0.35f,
                Considerations = new List<UtilityConsiderationBase>
                {
                    new ConstantConsideration { Label = "Baseline", Value = 0.5f, CanVeto = false }
                }
            });

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(asset);
            SetBrain(asset);
        }

        /// <summary>
        /// Clears the content host and constructs the visual tree for the currently selected workbench tab.
        /// </summary>
        private void RebuildActiveTab()
        {
            if (_contentHost == null) return;
            _contentHost.Clear();

            switch (_activeTab)
            {
                case WorkbenchTab.Authoring:
                    BuildAuthoringTab(_contentHost);
                    break;
                case WorkbenchTab.Simulator:
                    BuildSimulatorTab(_contentHost);
                    break;
                case WorkbenchTab.LiveDebugger:
                    BuildLiveDebuggerTab(_contentHost);
                    break;
            }
        }

        /// <summary>
        /// Builds the Authoring &amp; Curves tab with brain policy controls, behavior list, action selector, and live <see cref="ResponseCurveGraphElement"/> preview.
        /// </summary>
        private void BuildAuthoringTab(VisualElement host)
        {
            if (_brain == null)
            {
                host.Add(BuildEmptyBrainCard());
                return;
            }

            _brainSerializedObject ??= new SerializedObject(_brain);
            _brainSerializedObject.Update();

            // Left Pane: Policy + Behavior List
            var leftScroll = new ScrollView();

            var policyCard = new VisualElement();
            policyCard.AddToClassList("uai-card");
            policyCard.Add(CreateSectionTitle("Brain Policy"));

            var selectionProp = _brainSerializedObject.FindProperty("SelectionMode");
            var topKProp = _brainSerializedObject.FindProperty("TopKCount");
            var tempProp = _brainSerializedObject.FindProperty("SoftmaxTemperature");
            var intervalProp = _brainSerializedObject.FindProperty("EvaluationInterval");
            var inertiaProp = _brainSerializedObject.FindProperty("InertiaBonus");
            var deltaProp = _brainSerializedObject.FindProperty("MinSwitchDelta");

            var lastSelectionMode = _brain.SelectionMode;
            policyCard.Add(CreateBoundPropertyField(selectionProp, () =>
            {
                if (_brain != null && _brain.SelectionMode != lastSelectionMode)
                {
                    lastSelectionMode = _brain.SelectionMode;
                    EditorApplication.delayCall += RebuildActiveTab;
                }
            }));
            policyCard.Add(CreateBoundPropertyField(intervalProp));
            policyCard.Add(CreateBoundPropertyField(inertiaProp));
            policyCard.Add(CreateBoundPropertyField(deltaProp));
            if (_brain.SelectionMode == BehaviorSelectionMode.WeightedTopK)
                policyCard.Add(CreateBoundPropertyField(topKProp));
            if (_brain.SelectionMode == BehaviorSelectionMode.SoftmaxTemperature)
                policyCard.Add(CreateBoundPropertyField(tempProp));

            leftScroll.Add(policyCard);

            var behaviorsCard = new VisualElement();
            behaviorsCard.AddToClassList("uai-card");

            var headerRow = new VisualElement();
            headerRow.AddToClassList("uai-row");
            headerRow.AddToClassList("uai-row--between");
            headerRow.Add(CreateSectionTitle($"Behaviors ({_brain.Behaviors.Count})"));
            headerRow.Add(new Button(AddNewBehavior) { text = "+ Add" });
            behaviorsCard.Add(headerRow);

            for (var i = 0; i < _brain.Behaviors.Count; i++)
            {
                var idx = i;
                var b = _brain.Behaviors[i];
                var row = new VisualElement();
                row.AddToClassList("uai-list-row");
                if (idx == _selectedBehaviorIndex)
                {
                    row.AddToClassList("uai-list-row--selected");
                }

                var labelBtn = new Button(() =>
                {
                    _selectedBehaviorIndex = idx;
                    _selectedConsiderationIndex = b.Considerations.Count > 0 ? 0 : -1;
                    RebuildActiveTab();
                })
                {
                    text = $"{(b.Enabled ? "" : "[OFF] ")}{b.EffectiveName} (w:{b.Weight:F2})"
                };
                labelBtn.AddToClassList("uai-list-item-btn");
                row.Add(labelBtn);

                var delBtn = new Button(() => RemoveBehavior(idx)) { text = "X" };
                delBtn.AddToClassList("uai-list-delete-btn");
                row.Add(delBtn);

                behaviorsCard.Add(row);
            }

            leftScroll.Add(behaviorsCard);

            // Right Pane: Selected Behavior + Considerations + Painter2D Curve Preview
            var rightScroll = new ScrollView();

            if (_selectedBehaviorIndex >= 0 && _selectedBehaviorIndex < _brain.Behaviors.Count)
            {
                var behavior = _brain.Behaviors[_selectedBehaviorIndex];
                var behaviorsProp = _brainSerializedObject.FindProperty("Behaviors");
                var behaviorProp = behaviorsProp.GetArrayElementAtIndex(_selectedBehaviorIndex);

                // Behavior Settings Card
                var detailCard = new VisualElement();
                detailCard.AddToClassList("uai-card");
                detailCard.Add(CreateSectionTitle($"Behavior: {behavior.EffectiveName}"));
                detailCard.Add(CreateBoundPropertyField(behaviorProp.FindPropertyRelative("Id")));
                detailCard.Add(CreateBoundPropertyField(behaviorProp.FindPropertyRelative("DisplayName")));
                detailCard.Add(CreateBoundPropertyField(behaviorProp.FindPropertyRelative("Enabled")));
                detailCard.Add(CreateBoundPropertyField(behaviorProp.FindPropertyRelative("Weight")));
                detailCard.Add(CreateBoundPropertyField(behaviorProp.FindPropertyRelative("CooldownSeconds")));
                detailCard.Add(CreateBoundPropertyField(behaviorProp.FindPropertyRelative("MinCommitmentSeconds")));
                detailCard.Add(CreateBoundPropertyField(behaviorProp.FindPropertyRelative("TargetKey")));
                detailCard.Add(CreateBoundPropertyField(behaviorProp.FindPropertyRelative("Aggregation")));
                rightScroll.Add(detailCard);

                // Action Card
                var actionCard = new VisualElement();
                actionCard.AddToClassList("uai-card");
                var actionHeader = new VisualElement();
                actionHeader.AddToClassList("uai-row");
                actionHeader.AddToClassList("uai-row--between");
                actionHeader.Add(CreateSectionTitle($"Action: {(behavior.Action != null ? behavior.Action.DisplayName : "(None)")}"));
                actionHeader.Add(new Button(() => ShowActionTypeMenu(behavior)) { text = "Select Action Type..." });
                actionCard.Add(actionHeader);

                var actionProp = behaviorProp.FindPropertyRelative("Action");
                if (behavior.Action != null && actionProp != null)
                {
                    actionCard.Add(CreateBoundPropertyField(actionProp));
                }
                else
                {
                    actionCard.Add(CreateMutedLabel("Assign any UtilityActionBase subclass discovered via TypeCache."));
                }
                rightScroll.Add(actionCard);

                // Considerations + Curve Graph Card
                var consCard = new VisualElement();
                consCard.AddToClassList("uai-card");

                var consHeader = new VisualElement();
                consHeader.AddToClassList("uai-row");
                consHeader.AddToClassList("uai-row--between");
                consHeader.Add(CreateSectionTitle($"Considerations ({behavior.Considerations.Count})"));
                consHeader.Add(new Button(() => ShowConsiderationTypeMenu(behavior)) { text = "+ Add Consideration..." });
                consCard.Add(consHeader);

                var consListProp = behaviorProp.FindPropertyRelative("Considerations");
                var curveGraph = new ResponseCurveGraphElement(165f);

                if (behavior.Considerations.Count == 0)
                {
                    consCard.Add(CreateMutedLabel("No considerations added. Behavior will evaluate with baseline utility 1.0."));
                }
                else
                {
                    if (_selectedConsiderationIndex < 0 || _selectedConsiderationIndex >= behavior.Considerations.Count)
                        _selectedConsiderationIndex = 0;

                    var tabRow = new VisualElement();
                    tabRow.AddToClassList("uai-row");
                    for (var cIdx = 0; cIdx < behavior.Considerations.Count; cIdx++)
                    {
                        var capturedIdx = cIdx;
                        var cons = behavior.Considerations[cIdx];
                        var btn = new Button(() =>
                        {
                            _selectedConsiderationIndex = capturedIdx;
                            RebuildActiveTab();
                        })
                        {
                            text = cons != null ? cons.DisplayName : $"Null #{cIdx}"
                        };
                        btn.AddToClassList("uai-chip-btn");
                        if (capturedIdx == _selectedConsiderationIndex)
                            btn.AddToClassList("uai-nav-button--active");
                        tabRow.Add(btn);
                    }
                    consCard.Add(tabRow);

                    var selectedCons = behavior.Considerations[_selectedConsiderationIndex];
                    if (selectedCons != null)
                    {
                        selectedCons.Curve ??= ResponseCurve.Linear();
                        curveGraph.SetCurve(selectedCons.Curve, _curveProbeX);
                        consCard.Add(curveGraph);

                        var probeSlider = new Slider("Preview Input X", 0f, 1f) { value = _curveProbeX, showInputField = true };
                        probeSlider.RegisterValueChangedCallback(evt =>
                        {
                            _curveProbeX = evt.newValue;
                            curveGraph.SetCurve(selectedCons.Curve, _curveProbeX);
                        });
                        consCard.Add(probeSlider);

                        var consProp = consListProp.GetArrayElementAtIndex(_selectedConsiderationIndex);
                        var propField = CreateBoundPropertyField(consProp, () =>
                        {
                            curveGraph.SetCurve(selectedCons.Curve, _curveProbeX);
                        });
                        consCard.Add(propField);

                        var removeConsBtn = new Button(() =>
                        {
                            Undo.RecordObject(_brain, "Remove Consideration");
                            behavior.Considerations.RemoveAt(_selectedConsiderationIndex);
                            EditorUtility.SetDirty(_brain);
                            _selectedConsiderationIndex = Mathf.Clamp(_selectedConsiderationIndex - 1, -1, behavior.Considerations.Count - 1);
                            RebuildActiveTab();
                        })
                        {
                            text = "Remove Selected Consideration"
                        };
                        removeConsBtn.style.marginTop = 6;
                        consCard.Add(removeConsBtn);
                    }
                }

                rightScroll.Add(consCard);
            }
            else
            {
                var emptyCard = new VisualElement();
                emptyCard.AddToClassList("uai-card");
                emptyCard.Add(CreateMutedLabel("Select or add a behavior on the left to configure its action and response curves."));
                rightScroll.Add(emptyCard);
            }

            host.Add(CreateResizableSplit(leftScroll, rightScroll));
        }

        /// <summary>
        /// Builds the EditMode What-If Simulator tab with auto-detected Blackboard sliders/toggles, custom float inputs, and live ranking output.
        /// </summary>
        private void BuildSimulatorTab(VisualElement host)
        {
            if (_brain == null)
            {
                host.Add(BuildEmptyBrainCard());
                return;
            }

            SyncSimulatorFactsFromBrain();

            var leftScroll = new ScrollView();
            var rightScroll = new ScrollView();
            var resultsHost = new VisualElement();
            rightScroll.Add(resultsHost);

            var inputCard = new VisualElement();
            inputCard.AddToClassList("uai-card");
            inputCard.Add(CreateSectionTitle("Virtual Blackboard Inputs"));
            inputCard.Add(CreateMutedLabel("Drag sliders to simulate Utility AI decisions live in Edit Mode."));

            if (_simFloatRanges.Count == 0 && _simBoolKeys.Count == 0)
            {
                inputCard.Add(CreateMutedLabel("No BlackboardFloatConsideration or BlackboardBoolConsideration keys detected in this brain yet."));
            }

            foreach (var kv in _simFloatRanges.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                var key = kv.Key;
                var min = Mathf.Min(kv.Value.min, kv.Value.max);
                var max = Mathf.Max(kv.Value.min, kv.Value.max);
                if (Mathf.Approximately(min, max)) max = min + 100f;

                var currentVal = _simBlackboard.GetFloat(key, (min + max) * 0.5f);
                var slider = new Slider(key, min, max) { value = currentVal, showInputField = true };
                slider.RegisterValueChangedCallback(evt =>
                {
                    _simBlackboard.SetFloat(key, evt.newValue);
                    RefreshSimulationResults(resultsHost);
                });
                inputCard.Add(slider);
            }

            foreach (var key in _simBoolKeys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                var capturedKey = key;
                var toggle = new Toggle(capturedKey) { value = _simBlackboard.GetBool(capturedKey, false) };
                toggle.RegisterValueChangedCallback(evt =>
                {
                    _simBlackboard.SetBool(capturedKey, evt.newValue);
                    RefreshSimulationResults(resultsHost);
                });
                inputCard.Add(toggle);
            }

            var customBox = new VisualElement();
            customBox.style.marginTop = 8;
            var keyField = new TextField("Add Float Key") { value = _newSimFactKey };
            keyField.RegisterValueChangedCallback(evt => _newSimFactKey = evt.newValue);
            var valField = new FloatField("Value") { value = _newSimFactValue };
            valField.RegisterValueChangedCallback(evt => _newSimFactValue = evt.newValue);
            var addFactBtn = new Button(() =>
            {
                if (!string.IsNullOrWhiteSpace(_newSimFactKey))
                {
                    _simFloatRanges[_newSimFactKey] = (0f, Mathf.Max(100f, _newSimFactValue * 2f));
                    _simBlackboard.SetFloat(_newSimFactKey, _newSimFactValue);
                    RebuildActiveTab();
                }
            })
            {
                text = "Add Virtual Fact"
            };
            customBox.Add(keyField);
            customBox.Add(valField);
            customBox.Add(addFactBtn);
            inputCard.Add(customBox);

            leftScroll.Add(inputCard);
            RefreshSimulationResults(resultsHost);

            host.Add(CreateResizableSplit(leftScroll, rightScroll));
        }

        /// <summary>
        /// Evaluates the currently selected brain against the virtual simulator blackboard and renders ranked score bars.
        /// </summary>
        private void RefreshSimulationResults(VisualElement resultsHost)
        {
            resultsHost.Clear();
            if (_brain == null) return;

            var context = new UtilityContext(_simBlackboard);
            var snapshot = _simScorer.Evaluate(_brain, context, tickNumber: 1);

            var card = new VisualElement();
            card.AddToClassList("uai-card");
            var winnerTitle = string.IsNullOrEmpty(snapshot.SelectedBehaviorName)
                ? "Winner: (None — all behaviors scored 0 or were disqualified)"
                : $"Winner: {snapshot.SelectedBehaviorName}  (Score: {snapshot.SelectedScore:F3})";
            card.Add(CreateSectionTitle(winnerTitle));

            AppendEvaluationBars(card, snapshot);
            resultsHost.Add(card);
        }

        /// <summary>
        /// Builds the Play Mode Live Runtime Debugger tab with agent selector, Pause/Step/Export controls, Blackboard view, and history scrubber.
        /// </summary>
        private void BuildLiveDebuggerTab(VisualElement host)
        {
            var topCard = new VisualElement();
            topCard.AddToClassList("uai-card");

            var activeAgents = UtilityAIAgent.ActiveAgents;
            if (!Application.isPlaying)
            {
                topCard.Add(CreateSectionTitle("Live Runtime Debugger (Play Mode)"));
                topCard.Add(CreateMutedLabel("Enter Play Mode with one or more UtilityAIAgent components in your scene to inspect live scores, Blackboard facts, and decision history."));
                host.Add(topCard);
                return;
            }

            if (activeAgents.Count == 0)
            {
                topCard.Add(CreateSectionTitle("No Active UtilityAIAgent Found"));
                topCard.Add(CreateMutedLabel("Add a UtilityAIAgent component with a UtilityBrainAsset assigned to a GameObject in the active scene."));
                host.Add(topCard);
                return;
            }

            if (_inspectedAgent == null || !activeAgents.Contains(_inspectedAgent))
            {
                _inspectedAgent = activeAgents[0];
            }

            var agentNames = activeAgents.Select(a => a ? a.name : "(Destroyed)").ToList();
            var currentIdx = Mathf.Max(0, activeAgents.ToList().IndexOf(_inspectedAgent));

            var controlsRow = new VisualElement();
            controlsRow.AddToClassList("uai-row");

            var dropdown = new DropdownField("Active Agent", agentNames, currentIdx);
            dropdown.AddToClassList("uai-agent-dropdown");
            dropdown.RegisterValueChangedCallback(evt =>
            {
                var idx = agentNames.IndexOf(evt.newValue);
                if (idx >= 0 && idx < activeAgents.Count)
                {
                    _inspectedAgent = activeAgents[idx];
                    _selectedHistoryIndex = -1;
                    RebuildActiveTab();
                }
            });
            controlsRow.Add(dropdown);

            controlsRow.Add(new Button(() =>
            {
                _inspectedAgent.IsPaused = !_inspectedAgent.IsPaused;
                RebuildActiveTab();
            })
            {
                text = _inspectedAgent.IsPaused ? "Resume" : "Pause"
            });

            controlsRow.Add(new Button(() =>
            {
                _inspectedAgent.StepOnce();
                _selectedHistoryIndex = -1;
                RebuildActiveTab();
            })
            {
                text = "Step"
            });

            controlsRow.Add(new Button(() => ExportLiveHistory(asJson: true)) { text = "Export JSON" });
            controlsRow.Add(new Button(() => ExportLiveHistory(asJson: false)) { text = "Export CSV" });

            topCard.Add(controlsRow);
            host.Add(topCard);

            var runner = _inspectedAgent.Runner;
            if (runner == null)
            {
                var warnCard = new VisualElement();
                warnCard.AddToClassList("uai-card");
                warnCard.Add(CreateMutedLabel("Selected UtilityAIAgent has no UtilityBrainAsset assigned."));
                host.Add(warnCard);
                return;
            }

            var history = runner.History.ToChronologicalList();
            DecisionSnapshot displayedSnapshot = _selectedHistoryIndex >= 0 && _selectedHistoryIndex < history.Count
                ? history[_selectedHistoryIndex]
                : runner.LatestSnapshot;

            // Left: Blackboard + History Timeline
            var leftScroll = new ScrollView();

            var bbCard = new VisualElement();
            bbCard.AddToClassList("uai-card");
            bbCard.Add(CreateSectionTitle("Live Blackboard"));
            var entries = _inspectedAgent.Blackboard.CaptureDebugEntries();
            if (entries.Count == 0)
            {
                bbCard.Add(CreateMutedLabel("(Empty)"));
            }
            else
            {
                foreach (var entry in entries)
                {
                    var line = new VisualElement();
                    line.AddToClassList("uai-bb-row");
                    var kLbl = new Label($"{entry.Key} ({entry.ValueType})");
                    kLbl.AddToClassList("uai-bb-key");
                    var vLbl = new Label(entry.DisplayValue);
                    vLbl.AddToClassList("uai-bb-value");
                    line.Add(kLbl);
                    line.Add(vLbl);
                    bbCard.Add(line);
                }
            }
            leftScroll.Add(bbCard);

            var histCard = new VisualElement();
            histCard.AddToClassList("uai-card");
            var histHeader = new VisualElement();
            histHeader.AddToClassList("uai-row");
            histHeader.AddToClassList("uai-row--between");
            histHeader.Add(CreateSectionTitle($"Decision History ({history.Count})"));
            if (_selectedHistoryIndex >= 0)
            {
                histHeader.Add(new Button(() =>
                {
                    _selectedHistoryIndex = -1;
                    RebuildActiveTab();
                })
                {
                    text = "Live Latest"
                });
            }
            histCard.Add(histHeader);

            for (var i = history.Count - 1; i >= 0; i--)
            {
                var snapIdx = i;
                var snap = history[i];
                var item = new Button(() =>
                {
                    _selectedHistoryIndex = snapIdx;
                    RebuildActiveTab();
                })
                {
                    text = $"#{snap.TickNumber} [{snap.Timestamp:F1}s] {(string.IsNullOrEmpty(snap.SelectedBehaviorName) ? "(None)" : snap.SelectedBehaviorName)} ({snap.SelectedScore:F2}){(snap.SwitchedBehavior ? " *" : "")}"
                };
                item.AddToClassList("uai-history-btn");
                if (snapIdx == _selectedHistoryIndex)
                {
                    item.AddToClassList("uai-nav-button--active");
                }
                histCard.Add(item);
            }
            leftScroll.Add(histCard);

            // Right: Behavior Score Breakdown
            var rightScroll = new ScrollView();

            var scoreCard = new VisualElement();
            scoreCard.AddToClassList("uai-card");
            if (displayedSnapshot == null)
            {
                scoreCard.Add(CreateMutedLabel("No evaluations recorded yet. Click 'Step' or wait for next tick."));
            }
            else
            {
                var lockTag = displayedSnapshot.LockedByCommitment ? " [COMMITMENT LOCKED]" : string.Empty;
                scoreCard.Add(CreateSectionTitle(
                    $"Tick #{displayedSnapshot.TickNumber}  |  Active: {(string.IsNullOrEmpty(displayedSnapshot.SelectedBehaviorName) ? "(None)" : displayedSnapshot.SelectedBehaviorName)}{lockTag}"));
                AppendEvaluationBars(scoreCard, displayedSnapshot);
            }
            rightScroll.Add(scoreCard);

            host.Add(CreateResizableSplit(leftScroll, rightScroll));
        }

        /// <summary>
        /// Prompts for an output path and writes the inspected agent's chronological decision history as JSON or CSV.
        /// </summary>
        private void ExportLiveHistory(bool asJson)
        {
            if (_inspectedAgent?.Runner == null) return;
            var history = _inspectedAgent.Runner.History.ToChronologicalList();
            var ext = asJson ? "json" : "csv";
            var path = EditorUtility.SaveFilePanel("Export Utility AI Decision Trace", Application.dataPath, $"{_inspectedAgent.name}_trace.{ext}", ext);
            if (string.IsNullOrEmpty(path)) return;

            var content = asJson
                ? UtilityDecisionExporter.ToJson(history)
                : UtilityDecisionExporter.ToCsv(history);

            File.WriteAllText(path, content);
            Debug.Log($"[UtilityAIWorkbench] Exported {history.Count} snapshot(s) to '{path}'.");
        }

        /// <summary>
        /// Renders descending score bars, status badges, and per-consideration raw/normalized/curve breakdowns into <paramref name="parent"/>.
        /// </summary>
        private static void AppendEvaluationBars(VisualElement parent, DecisionSnapshot snapshot)
        {
            var sorted = snapshot.Evaluations.OrderByDescending(e => e.FinalScore).ToList();
            var maxScore = sorted.Count > 0 ? Mathf.Max(1f, sorted[0].FinalScore) : 1f;

            foreach (var eval in sorted)
            {
                var blocked = !string.IsNullOrEmpty(eval.DisqualificationReason);
                var row = new VisualElement();
                row.AddToClassList("uai-score-row");
                if (eval.IsWinner) row.AddToClassList("uai-score-row--winner");
                if (blocked) row.AddToClassList("uai-score-row--blocked");

                var header = new VisualElement();
                header.AddToClassList("uai-score-header");

                var leftHeader = new VisualElement();
                leftHeader.AddToClassList("uai-row");
                leftHeader.style.marginBottom = 0;

                var nameLbl = new Label(eval.DisplayName);
                nameLbl.AddToClassList("uai-score-title");
                leftHeader.Add(nameLbl);

                if (eval.IsWinner)
                {
                    var badge = new Label("ACTIVE");
                    badge.AddToClassList("uai-badge");
                    badge.AddToClassList("uai-badge--winner");
                    leftHeader.Add(badge);
                }
                else if (blocked)
                {
                    var badge = new Label(eval.DisqualificationReason);
                    badge.AddToClassList("uai-badge");
                    badge.AddToClassList("uai-badge--warn");
                    leftHeader.Add(badge);
                }

                header.Add(leftHeader);

                var scoreText = eval.InertiaBonusApplied > 0.0001f
                    ? $"{eval.FinalScore:F3} (base {eval.BaseUtility:F2} x w{eval.Weight:F2} + inertia {eval.InertiaBonusApplied:F2})"
                    : $"{eval.FinalScore:F3} (base {eval.BaseUtility:F2} x w{eval.Weight:F2})";
                var scoreLbl = new Label(scoreText);
                scoreLbl.AddToClassList("uai-score-value");
                header.Add(scoreLbl);
                row.Add(header);

                var track = new VisualElement();
                track.AddToClassList("uai-bar-track");
                var fill = new VisualElement();
                fill.AddToClassList("uai-bar-fill");
                if (eval.IsWinner) fill.AddToClassList("uai-bar-fill--winner");
                if (blocked) fill.AddToClassList("uai-bar-fill--blocked");
                fill.style.width = Length.Percent(Mathf.Clamp01(eval.FinalScore / maxScore) * 100f);
                track.Add(fill);
                row.Add(track);

                foreach (var c in eval.Considerations)
                {
                    var vetoText = c.WasVetoed ? " [VETO]" : string.Empty;
                    var cLbl = new Label($"• {c.ConsiderationName}: raw {c.RawInput:F2} -> norm {c.NormalizedInput:F2} -> {c.CurveType} = {c.CurveOutput:F3}{vetoText}");
                    cLbl.AddToClassList("uai-consideration-line");
                    row.Add(cLbl);
                }

                parent.Add(row);
            }
        }

        /// <summary>
        /// Appends a new behavior definition to the selected brain with Undo support and selects it.
        /// </summary>
        private void AddNewBehavior()
        {
            if (_brain == null) return;

            Undo.RecordObject(_brain, "Add Utility Behavior");
            var idx = _brain.Behaviors.Count + 1;
            _brain.Behaviors.Add(new UtilityBehaviorDefinition
            {
                Id = $"behavior_{idx}",
                DisplayName = $"Behavior {idx}",
                Weight = 1f
            });
            EditorUtility.SetDirty(_brain);
            _selectedBehaviorIndex = _brain.Behaviors.Count - 1;
            _selectedConsiderationIndex = -1;
            RebuildActiveTab();
        }

        /// <summary>
        /// Removes the behavior at <paramref name="index"/> with Undo support and clamps active selection indices.
        /// </summary>
        private void RemoveBehavior(int index)
        {
            if (_brain == null || index < 0 || index >= _brain.Behaviors.Count) return;

            Undo.RecordObject(_brain, "Remove Utility Behavior");
            _brain.Behaviors.RemoveAt(index);
            EditorUtility.SetDirty(_brain);
            _selectedBehaviorIndex = Mathf.Clamp(_selectedBehaviorIndex, -1, _brain.Behaviors.Count - 1);
            _selectedConsiderationIndex = -1;
            RebuildActiveTab();
        }

        /// <summary>
        /// Opens a context menu populated via <see cref="TypeCache"/> listing all concrete <see cref="UtilityConsiderationBase"/> subclasses.
        /// </summary>
        private void ShowConsiderationTypeMenu(UtilityBehaviorDefinition behavior)
        {
            var menu = new GenericMenu();
            var types = TypeCache.GetTypesDerivedFrom<UtilityConsiderationBase>()
                .Where(t => !t.IsAbstract && !t.IsGenericType)
                .OrderBy(t => t.Name);

            foreach (var type in types)
            {
                var captured = type;
                menu.AddItem(new GUIContent(captured.Name), false, () =>
                {
                    Undo.RecordObject(_brain, $"Add {captured.Name}");
                    if (Activator.CreateInstance(captured) is UtilityConsiderationBase instance)
                    {
                        behavior.Considerations.Add(instance);
                        EditorUtility.SetDirty(_brain);
                        _selectedConsiderationIndex = behavior.Considerations.Count - 1;
                        SyncSimulatorFactsFromBrain();
                        RebuildActiveTab();
                    }
                });
            }

            menu.ShowAsContext();
        }

        /// <summary>
        /// Opens a context menu populated via <see cref="TypeCache"/> listing all concrete <see cref="UtilityActionBase"/> subclasses plus a clear option.
        /// </summary>
        private void ShowActionTypeMenu(UtilityBehaviorDefinition behavior)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("(None)"), behavior.Action == null, () =>
            {
                Undo.RecordObject(_brain, "Clear Utility Action");
                behavior.Action = null;
                EditorUtility.SetDirty(_brain);
                RebuildActiveTab();
            });

            var types = TypeCache.GetTypesDerivedFrom<UtilityActionBase>()
                .Where(t => !t.IsAbstract && !t.IsGenericType)
                .OrderBy(t => t.Name);

            foreach (var type in types)
            {
                var captured = type;
                menu.AddItem(new GUIContent(captured.Name), behavior.Action?.GetType() == captured, () =>
                {
                    Undo.RecordObject(_brain, $"Assign {captured.Name}");
                    if (Activator.CreateInstance(captured) is UtilityActionBase instance)
                    {
                        behavior.Action = instance;
                        EditorUtility.SetDirty(_brain);
                        RebuildActiveTab();
                    }
                });
            }

            menu.ShowAsContext();
        }

        /// <summary>
        /// Scans the active brain's <see cref="BlackboardFloatConsideration"/> and <see cref="BlackboardBoolConsideration"/> entries
        /// to populate slider bounds and default values in the EditMode simulator.
        /// </summary>
        private void SyncSimulatorFactsFromBrain()
        {
            _simFloatRanges.Clear();
            _simBoolKeys.Clear();
            if (_brain == null || _brain.Behaviors == null) return;

            foreach (var behavior in _brain.Behaviors)
            {
                if (behavior?.Considerations == null) continue;
                foreach (var c in behavior.Considerations)
                {
                    if (c is BlackboardFloatConsideration f && !string.IsNullOrWhiteSpace(f.Key))
                    {
                        if (_simFloatRanges.TryGetValue(f.Key, out var existing))
                        {
                            _simFloatRanges[f.Key] = (Mathf.Min(existing.min, f.MinValue), Mathf.Max(existing.max, f.MaxValue));
                        }
                        else
                        {
                            _simFloatRanges[f.Key] = (f.MinValue, f.MaxValue);
                        }

                        if (!_simBlackboard.TryGetFloat(f.Key, out _))
                        {
                            _simBlackboard.SetFloat(f.Key, (f.MinValue + f.MaxValue) * 0.5f);
                        }
                    }
                    else if (c is BlackboardBoolConsideration b && !string.IsNullOrWhiteSpace(b.Key))
                    {
                        _simBoolKeys.Add(b.Key);
                        if (!_simBlackboard.TryGetBool(b.Key, out _))
                        {
                            _simBlackboard.SetBool(b.Key, b.ExpectedValue);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Creates a UI Toolkit <see cref="PropertyField"/> bound to <see cref="_brainSerializedObject"/> with an optional change callback.
        /// </summary>
        private PropertyField CreateBoundPropertyField(SerializedProperty prop, Action onChanged = null)
        {
            var field = new PropertyField(prop);
            field.Bind(_brainSerializedObject);
            if (onChanged != null)
            {
                field.RegisterValueChangeCallback(_ => onChanged());
            }
            return field;
        }

        private VisualElement BuildEmptyBrainCard()
        {
            var card = new VisualElement();
            card.AddToClassList("uai-card");
            card.Add(CreateSectionTitle("No UtilityBrainAsset Selected"));
            card.Add(CreateMutedLabel("Assign a UtilityBrainAsset in the top bar or click 'New Brain' to start authoring behaviors and response curves."));
            return card;
        }

        private static Label CreateSectionTitle(string text)
        {
            var lbl = new Label(text);
            lbl.AddToClassList("uai-section-title");
            return lbl;
        }

        private static Label CreateMutedLabel(string text)
        {
            var lbl = new Label(text);
            lbl.AddToClassList("uai-muted-label");
            return lbl;
        }

        /// <summary>
        /// Loads <c>UtilityAIStyles.uss</c> from the UPM package path, falling back to an <see cref="AssetDatabase"/> search for embedded copies.
        /// </summary>
        private static void ApplyStyleSheet(VisualElement root)
        {
            var direct = AssetDatabase.LoadAssetAtPath<StyleSheet>(PackageStyleSheetPath);
            if (direct != null)
            {
                root.styleSheets.Add(direct);
                return;
            }

            var guids = AssetDatabase.FindAssets("UtilityAIStyles t:StyleSheet");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.EndsWith(StyleSheetRelativePath, StringComparison.OrdinalIgnoreCase))
                {
                    var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                    if (sheet != null)
                    {
                        root.styleSheets.Add(sheet);
                        return;
                    }
                }
            }
        }
    }
}
