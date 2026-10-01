using Godot;
using System;
[Tool]
public partial class EngineSoundController : Node
{

        [Export] AudioStreamPlayer player;
        [Export] NoiseAndSin waveGen;
        public float throttle;

        public float rpm;
        [Export] public Vector2 volumeMinMax;
        public override void _Process(double delta)
        {
                float hz = rpm / 60f;
                float combustionHz = hz / 4f;

                if (!player.Playing && rpm != 0)
                        player.StreamPaused = false;

                if (player.Playing && rpm == 0)
                        player.StreamPaused = true;

                player.VolumeDb = Mathf.Lerp(volumeMinMax.X, volumeMinMax.Y, throttle);

                player.PitchScale = Mathf.Max(.001f, combustionHz / 100f);

                // int bus = AudioServer.GetBusIndex("Engine");
                // AudioServer.SetBusMute(bus, rpm < 100);

                base._Process(delta);
        }

}
