using Godot;
[Tool, GlobalClass]
public partial class Cylinder : Node
{

        public enum StrokeType
        {
                Exhaust,
                Intake,
                Compression,
                Combustion
        }

        public Combustion combustion = new();
        public AirFlow airFlow;
        public EngineController engine;
        public Crankshaft crankshaft;


        [Export] CylinderVisuals visuals;

        [Export(PropertyHint.Range, "0,100,")] public uint cylinderIndex = 0;
        [Export] public float angleOffset;
        public float PistonPosition => Mathf.Clamp((crankshaft.GetPistonPositionAtAngle(CurrentAngleDegrees) - crankshaft.GetRelativeCylinderPlacement(cylinderIndex).Y) / engine.strokeLength, 0, 1);
        [Export] private float currentTorque;
        [Export] private StrokeType currentStrokeType;


        public float CurrentAngleDegrees => angleOffset + crankshaft.shaftAngleDeg;
        public StrokeType CurrentStrokeType => (StrokeType)(Mathf.FloorToInt((CurrentAngleDegrees + 180) / 180f) % 4);
        public float CurrentGasVolume => engine.bore * (engine.strokeLength * (1 - PistonPosition) + engine.additionalUpwardHeight);



        public override void _Process(double delta)
        {
                if (crankshaft == null)
                {
                        GD.PushWarning($"crankshaft == null, cylinderIndex:{cylinderIndex}");
                        return;
                }

                if (Engine.IsEditorHint())
                {
                        if (visuals != null)
                                visuals.Position = crankshaft.GetRelativeCylinderPlacement(cylinderIndex);

                        currentStrokeType = CurrentStrokeType;
                }

                if (visuals != null)
                        visuals.UpdateMeshes();

                base._Process(delta);
        }
        private bool fuelIsBurned;
        private bool exhaustedGas;
        private bool firstIntake = true;
        ///For debugging purposes
        public float combustionFumesRatioBeforeCombustion;
        public void UpdateCurrentConditionsInsideCylinder(float deltaTime, float deltaAngleDegrees, out float torque)
        {
                switch (CurrentStrokeType)
                {
                        case StrokeType.Combustion:
                                if (!fuelIsBurned)
                                {

                                        firstIntake = true;
                                        fuelIsBurned = true;
                                        exhaustedGas = false;
                                        combustionFumesRatioBeforeCombustion = CurrentCombustionFumesAirRatio;
                                        combustion.BurnCurrentAir();
                                }
                                break;
                        case StrokeType.Exhaust:
                                if (!exhaustedGas)
                                {
                                        fuelIsBurned = false;

                                        //Add exhaust fumes mas inside current gas mixture
                                        float massChange = airFlow.CalculateMasOfExhaustGass(deltaTime, this);
                                        combustionFumesMass -= CurrentCombustionFumesAirRatio * massChange;
                                        gasMasInsideCylinder = Mathf.Max(gasMasInsideCylinder - massChange, 0);
                                }
                                break;
                        case StrokeType.Intake:
                                if (firstIntake == true)
                                        gasTemperatureInsideCylinder = ambientAirTemperature;
                                gasMasInsideCylinder += airFlow.CalculateMasOfAirIntake(deltaTime);

                                break;
                        case StrokeType.Compression:
                                break;
                }
                gasMasInsideCylinder = Mathf.Max(.1f, gasMasInsideCylinder);
                //https://en.wikipedia.org/wiki/First_law_of_thermodynamics
                ApplyFirstLawOfThremodynamics(out float work);
                // I have just realized that, this is not realistic at all!
                // if i come back to this project I'll have to go back to previous way of calculating torque
                // this would be true if pistone was moving freely not connected to a spinning crankshaft
                if (deltaAngleDegrees == 0)
                        torque = 0;
                else
                        torque = work / Mathf.DegToRad(deltaAngleDegrees);
        }



        private float lastVolume;
        private float lastPressure;
        private void ApplyFirstLawOfThremodynamics(out float work)
        {

                float currentVolume = CurrentGasVolume;
                currentPressure = gasMasInsideCylinder * Combustion.GasConstant * gasTemperatureInsideCylinder / currentVolume;

                float pressureChange = currentPressure - lastPressure;
                float averagePressure = (currentPressure + lastPressure) / 2f;
                float volumeChange = currentVolume - lastVolume;
                work = averagePressure * volumeChange - (pressureChange * engine.pressureChangeFrictionModifier);

                //TODO: make combustion happen in couple frames
                float heatFormCombustion = 0;

                float internalEnergyChange = heatFormCombustion - work;
                float specificHeat = SpecificHeat.GetCurrentSpecificHeat(this);
                float temperatureChange = internalEnergyChange / (gasMasInsideCylinder * specificHeat);

                lastVolume = currentVolume;
                lastPressure = currentPressure;
        }

        public const float ambientAirTemperature = 20 + 273;
        public float CurrentCombustionFumesAirRatio => gasMasInsideCylinder == 0 ? 1f : Mathf.Clamp(combustionFumesMass / gasMasInsideCylinder, 0f, 1f);
        public float combustionFumesMass = 0;
        /// mass of every gas that is inside cylinder including air, exhaust fumes
        public float gasMasInsideCylinder = 0;  // Kg
        public float gasTemperatureInsideCylinder = 10000; //Kelvins

        const float AtmospherePressure = 101325f; //Pa
        public float currentPressure;


        public void Start()
        {

                combustion.main = this;

                if (visuals == null)
                        return;
                visuals.engine = engine;
                visuals.main = this;
                visuals.UpdateMeshes();
        }
}
