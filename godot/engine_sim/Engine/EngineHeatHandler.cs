using Godot;
[Tool]
public partial class EngineHeatHandler : Node {

    public EngineController engine;

    public float cylinderWallTemperature = 30 + 273;
    // I could calculate this but it doesn't really matter
    [Export(PropertyHint.Range, "0,1,")] private float emissivityOfTheGas;
    private const float StefanBoltzmannConstant = .0000000567f;
    [Export] private float cylinderWallThicknessCm;
    private float cylinderWallThickness => cylinderWallThicknessCm / 100f;

    // I add this because material has different textures that change area + outside of cylinder could have special fins to also do it
    [Export] private float coolingAreaModifier;
    [Export] private float heatingAreaModifier;

    private const float castIronThermalConductivity = 55f;//W/m·K
    private const float castIronSpecificHeatCapacity = 500f;//
    private const float castIronDensity = 7200f; //kg/m3
    private float coolantTempK => engine.ambientAirTemperature;

    public override void _Ready() {
        cylinderWallTemperature = 30 + 273;
        base._Ready();
    }
    public void HeatPhysics(float delta) {
        if (cylinderWallThickness == 0)
            return;
        // I ignore cooling form piston and top end cap
        float cylinderWallAreaOut = Mathf.Pi * (engine.bore + cylinderWallThickness) * (engine.strokeLength + engine.additionalUpwardHeight);
        // Piston isn't directly connected so i shouldn't calculate this like that but it should be ok
        float cylinderEndsArea = 2f * Mathf.Pi * Mathf.Pow(engine.bore / 2f, 2f);
        //In reality the area that is heated by combustion gases changes
        float cylinderWallAreaIn = Mathf.Pi * engine.bore * (engine.strokeLength + engine.additionalUpwardHeight);
        // I should add mass of piston + end cap  but they should be thin 
        float massOfCylinderWalls = castIronDensity * cylinderWallAreaIn * cylinderWallThickness;

        foreach (Cylinder cylinder in engine.cylinders) {
            float convectiveHeatTransferCoefficient = CalculateConvectiveHeatTransferCoefficient(cylinder);
            float convectionHeatFlux = convectiveHeatTransferCoefficient * (cylinder.gasTemperatureInsideCylinder - cylinderWallTemperature);

            // this shouldn't really change anything
            // float radiationHeatFlux = emissivityOfTheGas * StefanBoltzmannConstant * (Mathf.Pow(cylinder.gasTemperatureInsideCylinder, 4) - Mathf.Pow(cylinderWallTemperature, 4));
            float heatFluxIn = convectionHeatFlux /* + radiationHeatFlux */;

            float deltaTemp = (cylinderWallTemperature - coolantTempK);
            float heatFluxOut = deltaTemp == 0 ? 0 : castIronThermalConductivity / cylinderWallThickness * deltaTemp;

            // heating area changes with position of piston
            float currentHeatingArea = cylinderEndsArea + Mathf.Pi * engine.bore * (engine.strokeLength + engine.additionalUpwardHeight) * cylinder.PistonPosition;
            float transferredHeat = heatFluxIn * currentHeatingArea * heatingAreaModifier - heatFluxOut * cylinderWallAreaOut * coolingAreaModifier;

            cylinderWallTemperature += (delta * transferredHeat) / (massOfCylinderWalls * castIronSpecificHeatCapacity);
            // could be working if I add a better fuel burning model
            // if (cylinder.gasMasInsideCylinder != 0)
            // {
            //     float specificHeat = SpecificHeat.GetCurrentSpecificHeat(cylinder);
            //     float heat = heatFluxIn * currentHeatingArea * heatingAreaModifier;
            //     float temperatureChange = (delta * heat) / (cylinder.gasMasInsideCylinder * specificHeat);
            //     if (!float.IsNaN(temperatureChange))
            //     {
            //         GD.Print($"temperatureChange {temperatureChange} {cylinder.gasMasInsideCylinder} {specificHeat} {cylinder.gasTemperatureInsideCylinder}");
            //         cylinder.gasTemperatureInsideCylinder -= temperatureChange;
            //     }
            // }
        }
    }

    [Export] float gasVelocityModifier;
    private float CalculateCylinderGasVelocity(Cylinder cylinder) {
        // there is that whole thing for Woschni correlation to calculate gas velocity but i think there is no sense in doing that
        // I'll just use  one from airflow
        return cylinder.airFlow.AveragePistoneVelocity * gasVelocityModifier;
    }
    private float CalculateConvectiveHeatTransferCoefficient(Cylinder cylinder) {
        //Woschni correlation
        float bore = 130 * Mathf.Pow(engine.bore, -.2f);
        float velocity = Mathf.Pow(CalculateCylinderGasVelocity(cylinder), .8f);

        float temperature = Mathf.Pow(cylinder.gasTemperatureInsideCylinder, -.53f) /* rylinder.gasTemperatureInsideCylinder */;
        float pressure = Mathf.Pow(cylinder.currentPressure / 1000f /* from Pa to kPa */ , 0.8f);
        return bore * pressure * temperature * velocity; //W/m^2K
    }
}
