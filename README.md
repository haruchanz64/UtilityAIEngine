# UtilityAIWorkbench

Modular Utility AI runtime and UI Toolkit workbench for Unity 6. It pairs a pure-C# scoring engine (`UtilityScorer` and `UtilityBrainRunner`) with a `UtilityAIAgent` component, parametric response curves, a typed Blackboard, an EditMode What-If Simulator, and a Play Mode Live Runtime Debugger with JSON/CSV trace export.

## Features

- **Parametric response curves (`ResponseCurve`):** Maps normalized `[0..1]` inputs to `[0..1]` utility scores using `Linear`, `Polynomial`, `Logistic`, `Logit`, `Step`, or `CustomAnimationCurve`, with optional `Invert` (`1 - y`) and NaN/Infinity clamping.
- **Compensated scoring and reducers (`UtilityScorer`):** Aggregates multiple considerations using Dave Mark's `CompensatedProduct` (preventing score decay on behaviors with more considerations), plus `RawProduct`, `Average`, `Minimum`, and `Maximum`.
- **Decision policies and anti-thrashing (`UtilityBrainAsset`):** Supports `HighestScore`, `WeightedTopK`, and `SoftmaxTemperature` candidate selection alongside per-behavior `CooldownSeconds`, `MinCommitmentSeconds`, `InertiaBonus`, `MinSwitchDelta`, and per-consideration `CanVeto` disqualification.
- **Case-insensitive typed Blackboard (`UtilityBlackboard`):** Stores `float`, `int`, `bool`, `string`, `Vector3`, and `UnityEngine.Object` facts with `OnKeyChanged` notifications, automatic `int` to `float` fallback in `TryGetFloat`, and sorted `CaptureDebugEntries()` snapshots.
- **TypeCache auto-discovery:** Any non-abstract, non-generic `[Serializable]` subclass of `UtilityConsiderationBase` or `UtilityActionBase` appears automatically in the Workbench consideration and action menus via `[SerializeReference]`.
- **Three-tab UI Toolkit Workbench (`Tools > HaruChanZ64 > AI > UtilityAIWorkbench > Utility AI Workbench`):**
  - **Authoring & Curves:** Edit brain policy, behaviors, actions, considerations, and `ResponseCurve` parameters with a `Painter2D` curve plot (`ResponseCurveGraphElement`) and live X-input probe.
  - **What-If Simulator:** Auto-generates sliders and toggles for referenced `BlackboardFloatConsideration` and `BlackboardBoolConsideration` keys so you can test rankings in Edit Mode without entering Play Mode.
  - **Live Runtime Debugger:** Inspect enabled Play Mode `UtilityAIAgent` instances with ranked score bars, per-consideration `raw -> norm -> curve` readouts, live Blackboard view, Pause/Step controls, bounded ring-buffer tick scrubbing, and JSON/CSV export (`UtilityDecisionExporter`).
- **Custom Inspectors and Scene gizmos:** `UtilityBrainAssetInspector` adds an **Open in Utility AI Workbench** button, `UtilityAIAgentInspector` adds **Open Live Runtime Debugger** plus a Play Mode active-behavior banner, and `UtilityAIAgent` draws a target line and wire sphere when selected.

## Requirements

- Unity `6000.0` or newer
- Zero required external dependencies

## Installation

### Unity Package Manager (Git URL)

1. Open **Window > Package Manager** in Unity 6 (`6000.0+`).
2. Click **+** (top-left) and choose **Install package from git URL...**.
3. Enter:

```text
https://github.com/haruchanz64/UtilityAIWorkbench.git
```

### Local package

Add the package path to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.haruchanz64.utility-ai-workbench": "file:../Plugins/UtilityAIWorkbench"
  }
}
```

## Quick start

1. Open **Tools > HaruChanZ64 > AI > UtilityAIWorkbench > Utility AI Workbench** and click **New Brain** (or create an asset via **Assets > Create > Utility AI Workbench > Utility Brain**).
   - To inspect a complete four-behavior example right away, import the **Survival Guard AI Demo** sample from Package Manager and run **Tools > HaruChanZ64 > AI > UtilityAIWorkbench > Create Survival Guard Demo Brain**.
2. In **Authoring & Curves**, add behaviors, assign a `UtilityActionBase`, add considerations, and tune each `ResponseCurve` using the live `Painter2D` preview and `Preview Input X` slider.
3. Switch to **What-If Simulator** to drag Blackboard sliders and toggles in Edit Mode and verify which behavior wins across edge cases.
4. Attach `UtilityAIAgent` (from `UtilityAIWorkbench.Runtime.Components`) to a scene GameObject, assign your `UtilityBrainAsset`, and optionally seed **Initial Blackboard Facts** (`initialFloats` and `initialBools`) or assign `Default Target`.
5. Enter Play Mode and open **Live Runtime Debugger** (or click **Open Live Runtime Debugger** in the `UtilityAIAgent` Inspector) to inspect live ticks, pause/step execution, scrub history, or export JSON/CSV traces.

## Core concepts

### Evaluation and scoring pipeline

Each evaluation in `UtilityScorer.Evaluate(...)` inspects every `UtilityBehaviorDefinition` in the `UtilityBrainAsset` in four stages:

1. **Eligibility and target resolution:** Disabled behaviors and behaviors on cooldown (`context.GetCooldownRemaining(behavior.Id) > 0.0001f`) are immediately disqualified with a reason string. If an `IUtilityTargetProvider` is present and `behavior.TargetKey` is non-empty, `context.Target` is temporarily resolved for that behavior and restored after scoring.
2. **Considerations and veto:** Each consideration runs `SampleNormalizedInput(context, out rawValue)` and passes the clamped `[0..1]` result through `ResponseCurve.Evaluate(...)`. If `CanVeto` is true and the curve output is `<= 0.0001`, the entire behavior is disqualified (`Vetoed by <Name>`). Behaviors with no considerations receive a baseline utility of `1.0`.
3. **Aggregation, weight, and inertia:** Consideration outputs are reduced via `ScoreAggregationMode` (`CompensatedProduct`, `RawProduct`, `Average`, `Minimum`, or `Maximum`). The final score is `max(0, aggregated * max(0, Weight) + inertiaBonus)`, where `InertiaBonus` is added only to the currently active behavior.
4. **Selection and anti-thrashing:** If the active behavior has not finished its `MinCommitmentSeconds`, it stays locked. Otherwise, eligible candidates (`FinalScore > 0`) are selected via `HighestScore`, `WeightedTopK`, or `SoftmaxTemperature`. When switching away from an active, still-eligible behavior, the challenger must beat the active score by at least `MinSwitchDelta`.

### Action lifecycle and cooldowns

`UtilityBrainRunner.Tick(deltaTime, forceEvaluate)` advances `context.ElapsedTime`, re-evaluates whenever `EvaluationInterval` elapses (or when no behavior is active or `forceEvaluate` is true), and ticks the active behavior's `UtilityActionBase`:

- `OnEnter(context)` runs once when a behavior becomes active.
- `OnTick(context)` runs every tick while active and returns `UtilityActionStatus.Running`, `Completed`, or `Failed`.
- Returning `Completed` or `Failed` immediately calls `OnExit(context)`, applies `CooldownSeconds` on `UtilityContext`, clears the active behavior, and resets the evaluation timer so the next `Tick` picks a new behavior immediately.
- Calling `UtilityBrainRunner.Stop()` (also called by `UtilityAIAgent.OnDisable`) invokes `OnExit(context)` without starting a cooldown.

### Blackboard and target resolution

`UtilityBlackboard` uses case-insensitive keys (`StringComparer.OrdinalIgnoreCase`) separated by storage type (`float`, `int`, `bool`, `string`, `Vector3`, and `UnityEngine.Object`). `TryGetFloat` and `GetFloat` fall back to integer entries with the same key so numeric considerations can read integer counters without duplicating facts. `TryGetObject<T>` checks Unity's native object lifetime so destroyed scene objects are never returned.

## Code examples

### 1. Write a custom consideration

Subclass `UtilityConsiderationBase` with `[Serializable]`. The Workbench discovers it automatically via `TypeCache`:

```csharp
using System;
using UnityEngine;
using UtilityAIWorkbench.Runtime.Core;

[Serializable]
public sealed class StaminaRatioConsideration : UtilityConsiderationBase
{
    public string StaminaKey = "Stamina";
    public float MaxStamina = 100f;

    public override string DisplayName => string.IsNullOrWhiteSpace(Label) ? "StaminaRatio" : Label;

    public override float SampleNormalizedInput(UtilityContext context, out float rawValue)
    {
        rawValue = context?.Blackboard != null
            ? context.Blackboard.GetFloat(StaminaKey, MaxStamina)
            : MaxStamina;

        return MaxStamina > 0.001f ? Mathf.Clamp01(rawValue / MaxStamina) : 0f;
    }
}
```

### 2. Write a custom action

Subclass `UtilityActionBase` with `[Serializable]` and store per-agent mutable state on `context.Blackboard` when multiple agents share one `UtilityBrainAsset`:

```csharp
using System;
using UnityEngine;
using UtilityAIWorkbench.Runtime.Core;

[Serializable]
public sealed class ReloadWeaponAction : UtilityActionBase
{
    public string AmmoKey = "Ammo";
    public float MaxAmmo = 30f;
    public float ReloadSeconds = 1.5f;

    private const string TimerKey = "_ReloadElapsed";

    public override void OnEnter(UtilityContext context)
    {
        context?.Blackboard?.SetFloat(TimerKey, 0f);
    }

    public override UtilityActionStatus OnTick(UtilityContext context)
    {
        if (context?.Blackboard == null)
            return UtilityActionStatus.Failed;

        var elapsed = context.Blackboard.GetFloat(TimerKey, 0f) + context.DeltaTime;
        context.Blackboard.SetFloat(TimerKey, elapsed);

        if (elapsed < ReloadSeconds)
            return UtilityActionStatus.Running;

        context.Blackboard.SetFloat(AmmoKey, MaxAmmo);
        return UtilityActionStatus.Completed;
    }

    public override void OnExit(UtilityContext context)
    {
        context?.Blackboard?.Remove(TimerKey);
    }
}
```

### 3. Feed sensor facts and resolve per-behavior targets

Attach a component implementing `IUtilityTargetProvider` to the same GameObject as `UtilityAIAgent`. `UtilityAIAgent.InitializeRunner` discovers it automatically via `GetComponent<IUtilityTargetProvider>()`:

```csharp
using UnityEngine;
using UtilityAIWorkbench.Runtime.Components;
using UtilityAIWorkbench.Runtime.Core;

[RequireComponent(typeof(UtilityAIAgent))]
public sealed class GuardSensors : MonoBehaviour, IUtilityTargetProvider
{
    [SerializeField] private Transform threatTarget;
    [SerializeField] private Transform coverPoint;

    private UtilityAIAgent _agent;

    private void Awake()
    {
        _agent = GetComponent<UtilityAIAgent>();
        _agent.onBehaviorChanged.AddListener(id => Debug.Log($"Guard switched to: {id}"));
    }

    private void Update()
    {
        if (_agent.Blackboard == null) return;

        _agent.Blackboard.SetBool("HasTarget", threatTarget != null);
        if (threatTarget != null)
        {
            _agent.SetTarget(threatTarget);
        }
    }

    public Transform ResolveTarget(UtilityContext context, string targetKey)
    {
        return targetKey switch
        {
            "Threat" => threatTarget,
            "Cover" => coverPoint,
            _ => threatTarget
        };
    }
}
```

### 4. Run a brain headlessly and export traces

Use `UtilityBrainRunner` directly without a `MonoBehaviour` for simulations, server ticks, or EditMode tests:

```csharp
using UtilityAIWorkbench.Runtime.Core;
using UtilityAIWorkbench.Runtime.Data;
using UtilityAIWorkbench.Runtime.Engine;

public static class HeadlessSimulationExample
{
    public static DecisionSnapshot RunStep(UtilityBrainAsset brain, float health, bool hasTarget)
    {
        var context = new UtilityContext();
        context.Blackboard.SetFloat("Health", health);
        context.Blackboard.SetBool("HasTarget", hasTarget);

        var runner = new UtilityBrainRunner(brain, context, historyCapacity: 64, randomSeed: 42);
        runner.Tick(deltaTime: 0.2f, forceEvaluate: true);
        return runner.LatestSnapshot;
    }
}
```

In Editor scripts, pass `runner.History.ToChronologicalList()` to `UtilityDecisionExporter.ToJson(...)` or `UtilityDecisionExporter.ToCsv(...)` (namespace `UtilityAIWorkbench.Editor.Export`).

## Extension contracts and core API

| Type | Namespace | Purpose | Built-in implementation |
| :--- | :--- | :--- | :--- |
| `UtilityConsiderationBase` | `UtilityAIWorkbench.Runtime.Core` | Base class for normalized `[0..1]` input sampling and curve evaluation | `BlackboardFloatConsideration`, `BlackboardBoolConsideration`, `DistanceToTargetConsideration`, `CooldownReadinessConsideration`, `RandomNoiseConsideration`, `ConstantConsideration` |
| `UtilityActionBase` | `UtilityAIWorkbench.Runtime.Core` | Executable behavior action lifecycle (`OnEnter`, `OnTick`, `OnExit`) | `WaitAction`, `SetBlackboardFloatAction`, `DeltaBlackboardFloatAction`, `SetBlackboardBoolAction`, `MoveTowardsTargetAction`, `LogDebugAction` |
| `IUtilityTargetProvider` | `UtilityAIWorkbench.Runtime.Core` | Resolves per-behavior `TargetKey` strings to a `Transform` during scoring and execution | Implement on a sibling `MonoBehaviour` or pass to `UtilityBrainRunner` |
| `ResponseCurve` | `UtilityAIWorkbench.Runtime.Core` | Parametric `[0..1] -> [0..1]` curve evaluator with inversion and clamping | `Linear`, `Polynomial`, `Logistic`, `Logit`, `Step`, `CustomAnimationCurve` |
| `UtilityBlackboard` | `UtilityAIWorkbench.Runtime.Core` | Case-insensitive typed fact storage with change events and debug snapshots | `UtilityBlackboard` |
| `UtilityContext` | `UtilityAIWorkbench.Runtime.Core` | Shared evaluation context holding agent, target, timing, blackboard, and cooldowns | `UtilityContext` |
| `UtilityBrainAsset` | `UtilityAIWorkbench.Runtime.Data` | ScriptableObject holding behaviors, selection mode, cadence, and hysteresis | `UtilityBrainAsset` |
| `UtilityBehaviorDefinition` | `UtilityAIWorkbench.Runtime.Data` | Serializable behavior record with weight, cooldown, commitment lock, considerations, and action | `UtilityBehaviorDefinition` |
| `UtilityScorer` | `UtilityAIWorkbench.Runtime.Engine` | Pure-C# scorer implementing compensated aggregation, vetoes, hysteresis, and selection | `UtilityScorer` |
| `UtilityBrainRunner` | `UtilityAIWorkbench.Runtime.Engine` | Stateful cadence runner managing active action lifecycle and `DecisionRingBuffer` history | `UtilityBrainRunner` |
| `UtilityAIAgent` | `UtilityAIWorkbench.Runtime.Components` | Scene `MonoBehaviour` driving a brain, registering for live debugging, and drawing target gizmos | `UtilityAIAgent` |
| `UtilityDecisionExporter` | `UtilityAIWorkbench.Editor.Export` | Serializes `DecisionSnapshot` histories to JSON or RFC 4180 CSV | `UtilityDecisionExporter` |

## Included samples

Import **Survival Guard AI Demo** (`Samples~/SurvivalGuardDemo`) from the Package Manager **Samples** tab:

- `MissingHealthRatioConsideration`: TypeCache-discovered custom consideration computing `(MaxHealth - Health) / MaxHealth` for heal and flee curves.
- `ConsumeMedkitAction`: TypeCache-discovered custom action with a cast timer (`CastTime`) that checks medkit count, restores health on the Blackboard, and decrements medkits on completion.
- `GuardBrainDemoGenerator`: Editor menu item (**Tools > HaruChanZ64 > AI > UtilityAIWorkbench > Create Survival Guard Demo Brain**) that generates a four-behavior `UtilityBrainAsset` (`Attack Target`, `Use Medkit`, `Flee Threat`, `Patrol Area`) and opens it in the Workbench.

## Testing

The package includes EditMode NUnit tests under `Tests/Editor/` runnable from **Window > General > Test Runner** (EditMode tab):

- `Tests/Editor/ResponseCurveAndBlackboardTests.cs`: verifies all parametric `ResponseCurve` shapes, inversion, NaN/range clamping, case-insensitive `UtilityBlackboard` storage with int-to-float reading, `OnKeyChanged` events, debug entry capture, key removal, and chronological wrapping in `DecisionRingBuffer`.
- `Tests/Editor/UtilityScorerAndRunnerTests.cs`: verifies `CompensatedProduct` vs. `RawProduct` and `Average` aggregation, veto/cooldown/inertia hysteresis in `UtilityScorer`, action `OnEnter`/`OnTick`/`OnExit` lifecycle with `MinCommitmentSeconds` locks and post-completion cooldowns in `UtilityBrainRunner`, and JSON/CSV trace output in `UtilityDecisionExporter`.

## Troubleshooting and limitations

- **Custom considerations or actions do not appear in Workbench menus:** Subclasses of `UtilityConsiderationBase` and `UtilityActionBase` must be non-abstract, non-generic, and marked `[Serializable]` so `TypeCache` discovers them and Unity can serialize them via `[SerializeReference]`.
- **Multiple agents sharing one `UtilityBrainAsset` with stateful actions:** `UtilityBehaviorDefinition.Action` instances live directly inside the `UtilityBrainAsset` ScriptableObject. Built-in timed actions such as `WaitAction` (and the sample `ConsumeMedkitAction`) store `_elapsed` on the action instance; if multiple `UtilityAIAgent` instances run the same asset concurrently, either instantiate a runtime clone (`Instantiate(brainAsset)`) per agent or store per-agent timers on `context.Blackboard`.
- **What-If Simulator does not show custom consideration inputs automatically:** The simulator auto-detects keys from `BlackboardFloatConsideration` and `BlackboardBoolConsideration` only. For custom float keys, use **Add Virtual Fact** in the simulator pane. Because the simulator runs without a scene `AgentTransform` or `IUtilityTargetProvider`, `DistanceToTargetConsideration` falls back to `DistanceWhenMissingTarget`.
- **Commitment locks end early when an action finishes:** `MinCommitmentSeconds` blocks preemption by competing behaviors during periodic re-evaluations, but if the active action returns `Completed` or `Failed`, `UtilityBrainRunner` exits it immediately, starts its cooldown, and re-evaluates on the next tick.
- **Live Runtime Debugger shows no active agents:** Agents register in `UtilityAIAgent.ActiveAgents` during `OnEnable` in Play Mode and unregister in `OnDisable`. Confirm the GameObject and component are enabled in Play Mode and have a `UtilityBrainAsset` assigned. Live updates refresh roughly every `0.15s` while unpaused and viewing the latest tick.
- **`UtilityBlackboard.Clear()` does not trigger `OnKeyChanged`:** Only `Set*` and `Remove` raise `OnKeyChanged`; `Clear()` silently empties all backing dictionaries.
- **Cooldowns are not applied when stopping an agent manually:** `UtilityBrainRunner.Stop()` (also invoked by `UtilityAIAgent.OnDisable`) calls `OnExit` without starting `CooldownSeconds`, whereas normal completion, failure, or preemption applies the configured cooldown.

## License

[MIT](LICENSE.md)
