using Godot;
using System;
[Tool]
public partial class EngineSoundController : Node
{

        [Export] AudioStreamPlayer player;
        [Export] NoiseAndSin waveGen;
        [Export] float engine_sound_initial_timeout;
        [Export] float engine_volume;

        [Export] bool test;
        [Export] float test_rpm;
        [Export] float test_throttle;
        float timeout_timer = 0;
        public float throttle;

        public float rpm;
        public bool timeout_ended;
        [Export] public Vector2 volumeMinMax;
        public override void _Process(double delta)
        {


                if (test)
                {
                        throttle = test_throttle;
                        rpm = test_rpm;
                }
                float hz = rpm / 60f;
                float combustionHz = hz / 4f;

                player.VolumeDb = Mathf.Lerp(volumeMinMax.X, volumeMinMax.Y, throttle);

                player.PitchScale = Mathf.Max(.001f, combustionHz / 100f);

                int bus = AudioServer.GetBusIndex("Master");
                timeout_timer += (float)delta;
                if (!Engine.IsEditorHint() && timeout_timer > engine_sound_initial_timeout && !timeout_ended)
                {
                        timeout_ended = true;

                        // player.Playing = true;
                        waveGen.disable = false;
                        AudioServer.SetBusVolumeDb(bus, engine_volume);
                        AudioServer.SetBusMute(bus, false);
                        waveGen.Play();

                }

                base._Process(delta);
        }
        public override void _Ready()
        {
                timeout_timer = 0;
                base._Ready();
        }

}
