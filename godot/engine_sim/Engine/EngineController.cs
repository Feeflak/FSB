using System;
using Godot;
[Tool]
public partial class EngineController : Node
{


        [Export] public EngineUI ui;
        [Export] public EngineStatsSmoothing statsSmoothing;
        [Export] public AirFlow airFlow;
        [Export] public Cylinder[] cylinders;
        [Export] public Crankshaft crankshaft;
        [Export] public EngineHeatHandler heatHandler;

        public float ambientAirDensity = 1.225f; // kg/m3
        public float ambientAirTemperature = 288.15f; // Kelvins


        public override void _Ready()
        {
                ui.engine = this;
                ui.crankshaft = crankshaft;

                statsSmoothing.engine = this;
                heatHandler.engine = this;
                airFlow.engine = this;
                airFlow.crankshaft = crankshaft;
                foreach (Cylinder cylinder in cylinders)
                {
                        cylinder.engine = this;
                        cylinder.airFlow = airFlow;
                        cylinder.crankshaft = crankshaft;
                        cylinder.gasMasInsideCylinder = 0;
                        cylinder.Start();
                }
                base._Ready();
        }
        public override void _Process(double delta)
        {
                if (Engine.IsEditorHint() || strokeLength == 0)
                {
                        strokeLength = crankshaft.GetStroke();
                        strokeLengthDm = strokeLength * 10f;
                        CalculateDisplacement();
                }
                base._Process(delta);
        }



        [Export(PropertyHint.Range, "0,1,")] public float throttle;
        [Export] public float rpmLimit;

        [Export] private uint idleRPMTarget;
        [Export] private float idleThrottlePercentage;
        public bool holdIdle;
        public bool overRPM = false;


        [ExportGroup("Drag and torque")]
        [Export] private float mechanicalDragModifier;
        // DO NOT USE THIS!
        [Export] public float pressureChangeFrictionModifier = 3;


        [ExportGroup("Cylinders settings")]

        [Export] private float boreDm;
        [Export] private float displacementDm3;
        [Export] private float strokeLengthDm;
        [Export] private float additionalUpwardHeightDm;
        [Export] private float pistonHeightCm;
        [Export] public float visualsScale = 10f;

        public float bore => boreDm / 10f; // m
        public float strokeLength;// m 
        public float additionalUpwardHeight => additionalUpwardHeightDm / 10f; // m
        public float displacement; //m^3
        public float pistonHeight => pistonHeightCm / 100f;

        public float currentPower; // Watts
        public float currentHorsePower => currentPower / 745.7f;
        public float currentTorque;

        private void CalculateDisplacement()
        {
                var radius = bore / 2f;
                displacement = Mathf.Pi * radius * radius * (strokeLength + additionalUpwardHeight);
                displacementDm3 = displacement * 1000f;
        }
        /// m^3

        public float averageTemperature;
        public float averageTorque;

        float lastAngleDeg = 0;
        public void HandlePhysics(float delta)
        {
                overRPM = crankshaft.RevolutionsPerSecond * 60f > rpmLimit;
                if (holdIdle && idleRPMTarget > crankshaft.RevolutionsPerSecond * 60f && throttle < idleThrottlePercentage)
                        throttle = idleThrottlePercentage;

                //abs so engine doesn't run backwards
                float deltaAngle = crankshaft.shaftAngleDeg - lastAngleDeg/* Mathf.Abs(crankshaft.shaftAngleDeg - lastAngleDeg) */;
                lastAngleDeg = crankshaft.shaftAngleDeg;
                float temperatureSum = 0;
                float torque = 0;
                foreach (Cylinder cylinder in cylinders)
                {
                        cylinder.UpdateCurrentConditionsInsideCylinder(delta, deltaAngle, out float addTorque);
                        torque += addTorque;
                        temperatureSum += cylinder.gasTemperatureInsideCylinder;
                }
                torque -= mechanicalDragModifier * delta * crankshaft.angularVelocityDeg;

                heatHandler.HeatPhysics(delta);
                currentTorque = torque;
        }
        public void PhysicsProcessDataForLaterUI()
        {
                statsSmoothing.AddDataToSmoothing(currentTorque);
        }
}
