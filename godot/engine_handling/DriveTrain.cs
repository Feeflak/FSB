using Godot;
using System;

public partial class DriveTrain : Node
{
        [Export] RigidBody3D rb;

        [Export] Propeller propeller;
        [Export] Crankshaft crankshaft;
        [Export] public EngineController engine;
        [Export] DriveTrainUI ui;

        [Export] public float momentOfInertia;

        //NEEDS TO BE SET BY THE RUST SIDE
        [Export] public float airDensity;
        [Export] public float airTemperature;
        [Export] public float frontalAirVelocity;

        [Export] public float gearRatio;
        [Export] public float starterSpeed;
        [Export] float currentAngularVelocity /* rad/s */;

        public bool starterButtonPressed;

        [Export] private float enginePhysicsUpdatesPerSecond;
        private TickSystem enginePhysicsTickSystem = new();

        public override void _Ready()
        {
                enginePhysicsTickSystem.updatesPerSecond = enginePhysicsUpdatesPerSecond;
                enginePhysicsTickSystem.toCall.Clear();
                enginePhysicsTickSystem.toCall.Add(HandlePhysics);
                base._Ready();
        }

        public float currentThrust;
        [Export] float dragModifier;
        [Export] bool enable;
        [Export] float engineShutdownDrag = 200;

        public float PropellerAngularVelocity => currentAngularVelocity * gearRatio;
        private void HandlePhysics(float delta)
        {
                if (!enable)
                        return;
                engine.HandlePhysics(delta);
                engine.PhysicsProcessDataForLaterUI();

                engine.ambientAirTemperature = airTemperature;
                engine.ambientAirDensity = airDensity;

                propeller.HandlePhysics(delta, frontalAirVelocity, airDensity, PropellerAngularVelocity, out float thrust, out float propellerDrag, out string aoaDebug);
                ui.angleOfAttackOfTip.Text = "angles of attack of propeller elements: \n" + aoaDebug;
                ApplyThrust(thrust);

                currentThrust = thrust;

                float shutdownDrag = engine.holdIdle ? 0 : engineShutdownDrag;
                float drivetrainTorque = engine.currentTorque - (propellerDrag + shutdownDrag) * dragModifier * gearRatio;
                float deltaAngularMomentum = drivetrainTorque;
                float deltaAngularVelocity = deltaAngularMomentum / momentOfInertia;

                currentAngularVelocity += deltaAngularVelocity;
                currentAngularVelocity = Mathf.Max(currentAngularVelocity, 0);


                if (starterButtonPressed)
                        currentAngularVelocity = starterSpeed;

                crankshaft.UpdateCrankshaftStatsBasedOnDrivetrain(currentAngularVelocity, delta);
        }
        private void ApplyThrust(float thrust)
        {
                if (float.IsNaN(thrust))
                        return;
                rb.ApplyForce(rb.Transform.Basis.Z * thrust);
        }
        public override void _PhysicsProcess(double delta)
        {
                if (Engine.IsEditorHint())
                        return;
                enginePhysicsTickSystem.Update((float)delta);


                base._PhysicsProcess(delta);
        }
}
