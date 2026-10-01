using Godot;
[Tool]
public partial class ChassisUI : Node
{

        [Export] ChassisMain main;
        [Export] Label gearText;
        [Export] Label linearVelocityText;
        [Export] Label dragText;
        [Export] public CheckButton starterButton;

        [Export] string nextGearActionName = "nextGear";
        [Export] string previousGearActionName = "previousGear";
        [Export] string starterActionName = "starter";

        [Export] string brakeActionName = "breake";
        private void HandleInput()
        {

                if (Engine.IsEditorHint())
                        return;

                if (nextGearActionName != "" && Input.IsActionJustPressed(nextGearActionName) && main.gear != main.gearRatios.Length - 1)
                        main.gear++;
                if (previousGearActionName != "" && Input.IsActionJustPressed(previousGearActionName) && main.gear != 0)
                        main.gear--;
                if (starterActionName != "" && Input.IsActionJustPressed(starterActionName) && main.linearVelocity < main.starterSpeed + 10)
                {
                        starterButton.ButtonPressed = !starterButton.ButtonPressed;
                        main.engine.holdIdle = true;
                }
                if (brakeActionName != "")
                        main.brakePosition = Input.GetActionStrength(brakeActionName);

        }

        private void HandleGraphicalUI()
        {
                if (starterButton != null)
                        main.starterButtonPressed = starterButton.ButtonPressed;

                if (gearText != null)
                        gearText.Text = $"gear: {main.gear}";

                if (linearVelocityText != null)
                        linearVelocityText.Text = $"velocity: {Mathf.RoundToInt(main.linearVelocity)}";

                if (dragText != null)
                        dragText.Text = $"drag force: {Mathf.RoundToInt(main.currentDragForce)}";
        }
        public override void _Process(double delta)
        {
                HandleInput();
                HandleGraphicalUI();

                powerCalculationTickSystem.Update((float)delta);

                base._Process(delta);
        }

        public override void _Ready()
        {
                powerCalculationTickSystem.toCall = new(1);
                powerCalculationTickSystem.toCall.Add(CalculatePower);
                powerCalculationTickSystem.updatesPerSecond = powerSamplesPerSecond;

                base._Ready();
        }


        [Export] float powerSamplesPerSecond;
        private TickSystem powerCalculationTickSystem = new();
        private float lastVelocity;


        private void CalculatePower(float deltaTime)
        {
                float deltaVelocity = main.linearVelocity - lastVelocity;
                float meanVelocity = (main.linearVelocity + lastVelocity) / 2f;
                float acceleration = deltaVelocity / deltaTime;

                float power = acceleration * main.mass * meanVelocity;
                main.engine.currentPower = power;

                lastVelocity = main.linearVelocity;

        }
}
