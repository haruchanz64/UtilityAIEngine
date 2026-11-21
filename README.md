# UtilityAIWorkbench

Zero-dependency Unity 6 Utility AI runtime and UI Toolkit workbench with parametric response curves, Dave Mark compensated scoring, an EditMode What-If Simulator, and a Live Runtime Debugger.

## Features

- Parametric response curves (`Linear`, `Polynomial`, `Logistic`, `Logit`, `Step`, `Invert`, and `CustomAnimationCurve`) with GPU-accelerated `Painter2D` preview and live input probing.
- Compensated utility scoring using Dave Mark's compensation factor, plus `RawProduct`, `Average`, `Minimum`, and `Maximum` aggregation modes.
- Decision policy and anti-thrashing controls supporting `HighestScore`, `WeightedTopK`, and `SoftmaxTemperature` selection, per-behavior cooldowns, minimum commitment locks, inertia bonus, and minimum switch delta.
- Case-insensitive `UtilityBlackboard` supporting `float`, `int`, `bool`, `string`, `Vector3`, and `UnityEngine.Object` facts with change events and debug snapshots.
- TypeCache auto-discovery: any subclass of `UtilityConsiderationBase` or `UtilityActionBase` in your project appears automatically in the Workbench menus.
- Three-tab UI Toolkit Workbench (`Tools > Utility AI Workbench`):
  - Authoring & Curves: edit brain policies, behaviors, considerations, actions, and response curves with live `Painter2D` curve plotting.
  - What-If Simulator: auto-generates sliders and toggles for referenced Blackboard facts so you can test scoring and winners in Edit Mode without entering Play Mode.
  - Live Runtime Debugger: inspect active `UtilityAIAgent` instances in Play Mode with live ranked score bars, per-consideration `raw -> norm -> curve` breakdowns, live Blackboard view, Pause/Step controls, historical tick scrubbing, and JSON/CSV trace export.

## Installation

### Unity Package Manager (Git URL)
1. Open `Window > Package Manager` in Unity 6.
2. Click `+` > `Install package from git URL...`
3. Enter:
   ```text
   https://github.com/haruchanz64/UtilityAIWorkbench.git
   ```

## Quick Start

1. Open `Tools > Utility AI Workbench` and click `New Brain` (or import the `Survival Guard AI Demo` sample and run `Tools > Utility AI Workbench > Create Survival Guard Demo Brain`).
2. Add behaviors, considerations, and actions in Authoring & Curves, then tune response curves with the live `Painter2D` preview.
3. Switch to What-If Simulator to drag Blackboard sliders and verify behavior rankings in Edit Mode.
4. Attach `UtilityAIAgent` to a GameObject, assign your `UtilityBrainAsset`, enter Play Mode, and inspect live ticks in Live Runtime Debugger.

## Custom Consideration Example

```csharp
using System;
using UnityEngine;
using UtilityAIWorkbench.Runtime.Core;

[Serializable]
public sealed class StaminaRatioConsideration : UtilityConsiderationBase
{
    public float MaxStamina = 100f;

    public override float SampleNormalizedInput(UtilityContext context, out float rawValue)
    {
        rawValue = context.Blackboard.GetFloat("Stamina", MaxStamina);
        return Mathf.Clamp01(rawValue / Mathf.Max(0.01f, MaxStamina));
    }
}
```

## Requirements
- Unity 6 (`6000.0+`)
- Zero third-party dependencies

## License
[MIT License](LICENSE.md)
