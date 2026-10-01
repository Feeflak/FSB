using System;
using Godot;
[Tool]
public partial class AirFlow : Node {

    public EngineController engine;
    public Crankshaft crankshaft;


    [Export] private float MinValveLiftCm;
    [Export] private float MaxValveLiftCm;
    [Export] private float throttleDiameterCm;

    private float MinValveLift => MinValveLiftCm / 100f;
    private float MaxValveLift => MaxValveLiftCm / 100f;
    private float throttleDiameter => throttleDiameterCm / 100f;


    private float ValveLift() {
        if (engine.overRPM)
            return MinValveLift;
        else
            return Mathf.Lerp(MinValveLift, MaxValveLift, engine.throttle);
    }
    // also i think it is the same as curtain area Ac
    private float CurrentEffectiveIntakeFlowArea => Mathf.Pi * throttleDiameter * ValveLift();

    // i don't think there is any way to calculate it
    [Export(PropertyHint.Range, "0.3,0.6,")] private float intakeVelocityModifier = .5f;
    public float AveragePistoneVelocity => 2 * engine.strokeLength * crankshaft.RevolutionsPerSecond;


    [Export(PropertyHint.Range, "0.4,0.8,")] private float flowEfficiency = .5f;
    private float VolumetricFlowRate(float flowArea) {
        return flowArea * AveragePistoneVelocity * flowEfficiency;
    }

    public float CalculateMasOfAirIntake(float deltaTime) {
        float gasDensity = engine.ambientAirDensity;
        return gasDensity * VolumetricFlowRate(CurrentEffectiveIntakeFlowArea) * intakeVelocityModifier * deltaTime;
    }
    [Export(PropertyHint.Range, "0.3,0.6,")] private float exhaustVelocityModifier = .5f;
    [Export] private float exhaustAreaCm = .5f;
    private float exhaustAreaM => exhaustAreaCm / 10000f;
    public float CalculateMasOfExhaustGass(float deltaTime, Cylinder cylinder) {
        float gasDensity = cylinder.gasMasInsideCylinder / cylinder.CurrentGasVolume;
        return gasDensity * VolumetricFlowRate(exhaustAreaM) * exhaustVelocityModifier * deltaTime;
    }


}
