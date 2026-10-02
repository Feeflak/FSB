using Godot;
using System;

public partial class WindController : Node
{
        [Export] AudioStreamPlayer player;
        [Export] public float MaxSpeed;
        [Export] public float Speed;
        [Export] public Vector2 volumeMinMax;
        [Export] public float currentGForce;
        [Export] public float consciousness;

        [Export] public Curve GSoundVolume;
        [Export] public Curve HighGEffectSoundVolume;
        public override void _Process(double delta)
        {
                var speed_effect = MathF.Min(Speed / MaxSpeed, 1);
                var volume = speed_effect * GSoundVolume.SampleBaked(currentGForce) * HighGEffectSoundVolume.SampleBaked(consciousness);
                player.VolumeDb = Mathf.Lerp(volumeMinMax.X, volumeMinMax.Y, volume);
                player.PitchScale = MathF.Max(.001f, HighGEffectSoundVolume.SampleBaked(consciousness));
                base._Process(delta);
        }



}
