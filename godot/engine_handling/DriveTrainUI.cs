using System;
using Godot;
public partial class DriveTrainUI : Node
{
        [Export] private DriveTrain main;
        [Export] EngineSoundController engineSoundController;

        [Export] CheckBox starterCheckbox;
        [Export] public Label angleOfAttackOfTip;

        [Export] TextureProgressBar rpmPercentageBarHUD;
        [Export] VSlider throttle;
        [Export] Label temperatureLabelHUD;
        [Export] Label thrust;

        [Export] Node3D propeller;
        [Export] Node3D propeller_sprite;
        [Export] uint min_propeller_sprite_rpm;


        [Export] private string starterInputAction;


        public override void _Process(double delta)
        {
                if (Input.IsActionJustPressed(starterInputAction))
                {
                        main.starterButtonPressed = !main.starterButtonPressed;
                        main.engine.holdIdle = true;
                }
                starterCheckbox.ButtonPressed = main.starterButtonPressed;

                thrust.Text = $"{Mathf.RoundToInt(main.currentThrust * 10f) / 10f}";

                float engineRPM = main.engine.crankshaft.RevolutionsPerSecond * 60;
                float propellerRPM = engineRPM * main.gearRatio;
                throttle.Value = main.engine.throttle;
                engineSoundController.throttle = main.engine.throttle;
                engineSoundController.rpm = propellerRPM;

                rpmPercentageBarHUD.Value = engineRPM;
                temperatureLabelHUD.Text = $"{(uint)(main.engine.heatHandler.cylinderWallTemperature - 273)}";
                float prop_sprite_visibility_modifier = propeller_sprite.Visible ? propellerRPM * 0.2f : 0;
                if (propellerRPM + prop_sprite_visibility_modifier < min_propeller_sprite_rpm)
                {
                        propeller.Rotate(Vector3.Forward, 2f * Mathf.Pi * (float)delta * 60f * propellerRPM);
                        propeller.Visible = true;
                        propeller_sprite.Visible = false;
                }
                else
                {
                        propeller.Visible = false;
                        propeller_sprite.Visible = true;

                }
                base._Process(delta);
        }
        public override void _Ready()
        {
                rpmPercentageBarHUD.MinValue = 0;
                rpmPercentageBarHUD.MaxValue = main.engine.rpmLimit;
                base._Ready();
        }
}
