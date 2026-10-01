using Godot;
using System;
using System.Collections.Generic;
[Tool]
public partial class EngineUI : Node {
    public EngineController engine;
    public Crankshaft crankshaft;

    [Export] private EngineSoundController soundController;
    [Export] Slider throttleSlider;
    [Export] Label angularVelocityText;
    [Export] Label totalTorque;
    [Export] Label averageGasTemperature;

    [Export] Label exhaustFumesRatioBeforeCombustion;
    [Export] Label rpm;
    [Export] Label horsePower;
    [Export] Label temperature;
    [Export] Label gameFps;
    [Export] CheckBox holdIdle;

    public override void _Process(double delta) {
        HandleInput();
        UpdateTextUI();
        base._Process(delta);
    }

    [Export] string throttleActionName = "throttle";
    [Export] string holdIdleActionName = "hold idle";
    private void HandleInput() {
        if (Engine.IsEditorHint())
            return;

        if (throttleActionName != "")
            engine.throttle = Input.GetActionStrength(throttleActionName);
        if (holdIdleActionName != "" && Input.IsActionJustPressed(holdIdleActionName))
            engine.holdIdle = !engine.holdIdle;

    }
    private void UpdateTextUI() {
        // i just use one of cylinders so i don't heave to do any weird averaging, ratios should be similar in all cylinders
        SetLabel(exhaustFumesRatioBeforeCombustion, $"Exhaust fumes ratio in gas mixture before combustion {Math.Round(engine.cylinders[0].CurrentCombustionFumesAirRatio, 2)}");
        SetLabel(gameFps, $"FPS {Engine.GetFramesPerSecond()}");
        SetLabel(temperature, $"Temperature of 1'st cylinder's wall: {Mathf.RoundToInt(engine.heatHandler.cylinderWallTemperature - 273)}C");
        SetLabel(angularVelocityText, $"Angular velocity: {Mathf.RoundToInt(crankshaft.angularVelocityDeg)}");
        SetLabel(rpm, $"RPM: {Mathf.RoundToInt(crankshaft.RevolutionsPerSecond * 60f)}");
        SetLabel(horsePower, $"Horse power: {(int)crankshaft.engine.currentHorsePower}");
        SetLabel(totalTorque, $"Torque: {(int)engine.averageTorque}");
        SetLabel(averageGasTemperature, $"Average gas temperature: {(int)engine.cylinders[0].gasTemperatureInsideCylinder}");

        if (holdIdle != null) {
            holdIdle.ButtonPressed = engine.holdIdle;
        }

        throttleSlider.Value = engine.throttle;
        soundController.throttle = engine.overRPM ? 0 : engine.throttle;

        soundController.rpm = crankshaft.RevolutionsPerSecond * 60f;
    }
    public void SetLabel(Label label, string text) {
        if (label != null)
            label.Text = text;
    }

}
