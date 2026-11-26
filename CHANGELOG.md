# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - 2026-11-26
### Changed
- **Breaking:** Renamed package from `UtilityAIWorkbench` (`com.haruchanz64.utility-ai-workbench`) to `UtilityAIEngine` (`com.haruchanz64.utility-ai-engine`).
- Renamed assembly definitions (`UtilityAIEngine.Runtime`, `UtilityAIEngine.Editor`, `UtilityAIEngine.Editor.Tests`) and root C# namespaces (`UtilityAIEngine.*`).
- Updated Unity menu paths to `Tools > HaruChanZ64 > AI > UtilityAIEngine` and `Assets > Create > UtilityAIEngine > Utility Brain`.

## [1.0.1] - 2026-11-25
### Documentation
- Upgraded `README.md` into a full developer guide covering scoring architecture, action lifecycles, custom consideration/action/target-provider examples, core API reference tables, samples, tests, and troubleshooting.
- Added XML documentation comments across all `Runtime/`, `Editor/`, and `Samples~/` C# types, members, and helper methods without altering runtime logic.

## [1.0.0] - 2026-11-21
### Added
- Parametric `ResponseCurve` (`Linear`, `Polynomial`, `Logistic`, `Logit`, `Step`, `CustomAnimationCurve`) with inversion and NaN-safe clamping.
- Typed `UtilityBlackboard`, `UtilityContext`, `UtilityDiagnostics` (`DecisionSnapshot`, `BehaviorEvaluationRecord`, `ConsiderationScoreRecord`, `DecisionRingBuffer`), and `[SerializeReference]`-ready `UtilityConsiderationBase` / `UtilityActionBase` contracts.
- Built-in considerations (`BlackboardFloatConsideration`, `BlackboardBoolConsideration`, `DistanceToTargetConsideration`, `CooldownReadinessConsideration`, `RandomNoiseConsideration`, `ConstantConsideration`) and built-in actions (`WaitAction`, `SetBlackboardFloatAction`, `DeltaBlackboardFloatAction`, `SetBlackboardBoolAction`, `MoveTowardsTargetAction`, `LogDebugAction`).
- `UtilityScorer` implementing Dave Mark compensated scoring, cooldown gating, inertia hysteresis, minimum switch delta, and `HighestScore` / `WeightedTopK` / `SoftmaxTemperature` selection.
- `UtilityBrainRunner` and `UtilityAIAgent` MonoBehaviour with Domain Reload-safe active-agent registry, `IUtilityTargetProvider` support, and Scene gizmos.
- 3-tab UI Toolkit `UtilityAIWorkbenchWindow` (**Authoring & Curves** with `Painter2D` `ResponseCurveGraphElement`, **EditMode What-If Simulator**, and **Play Mode Live Runtime Debugger** with Pause/Step/Timeline scrubbing).
- `UtilityDecisionExporter` for JSON and CSV decision trace export.
- `Samples~/SurvivalGuardDemo` custom consideration/action sample and brain generator menu item.
- EditMode NUnit test suite covering response curves, blackboard, ring buffer, compensated scoring, hysteresis, runner lifecycle, and trace export.
