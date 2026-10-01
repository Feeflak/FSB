using Godot;
using System;
[Tool]
public partial class NoiseAndSin : Node {


    [Export] public AudioStreamPlayer Player { get; set; }

    private AudioStreamGeneratorPlayback _playback; // Will hold the AudioStreamGeneratorPlayback.
    [Export] private int SampleRate = 44100;

    [Export] public int culindersCount;
    [Export] public float cylindersOffset;
    [Export] public float frequency = 1.0f; // Hertz
    [Export] public float amplitude = 1.0f;
    [Export] float dutyCycle = 0.5f; // Fraction of the period the wave is high (0.0 to 1.0)

    [Export] private float Duration = 10f; // Duration in seconds
    [Export] private float NoiseVolume = 0.1f; // Volume of the noise component
    [Export] private bool disable = false;
    [Export] bool distortion = true;

    public override void _Ready() {
        if (Player.Stream is AudioStreamGenerator generator) // Type as a generator to access MixRate.
        {
            Player.Play();
            _playback = (AudioStreamGeneratorPlayback)Player.GetStreamPlayback();
        }
    }
    public override void _Process(double delta) {
        if (disable)
            return;
        PlaySound();
        base._Process(delta);
    }
    // ? To change rpm just change Base Frequency
    public void PlaySound() {
        int totalSamples = (int)(SampleRate * Duration);
        Vector2[] samples = new Vector2[totalSamples];

        for (int i = 0; i < totalSamples; i++) {
            float value = 0;
            for (int cylinder = 0; cylinder < culindersCount; cylinder++) {
                float time = ((float)i + cylinder * cylindersOffset) / SampleRate;
                float wave = (time * frequency) % 1.0f < dutyCycle ? amplitude : 0;
                // Generate white noise
                float noise = (float)(GD.Randf() * 2.0 - 1.0) * NoiseVolume;
                value += wave /* * noise */;
            }
            samples[i] = new(value, value);
        }
        if (distortion)
            ApplyDistortion(totalSamples, ref samples);

        _playback.PushBuffer(samples);
    }

    [Export] private float DistortionAmount = 1.5f;
    [Export] private float FeedbackAmount = 0.3f;
    public void ApplyDistortion(int totalSamples, ref Vector2[] samples) {
        float feedback = 0f;
        for (int i = 0; i < totalSamples; i++) {
            Vector2 startSample = samples[i];
            startSample.Y = Distort(startSample.Y, ref feedback);
            startSample.X = startSample.Y;
            samples[i] = startSample;
        }
    }
    private float Distort(float input, ref float feedback) {
        float distortedSignal = input * DistortionAmount;

        // Clipping
        if (distortedSignal > 1.0f) distortedSignal = 1.0f;
        if (distortedSignal < -1.0f) distortedSignal = -1.0f;

        // Add feedback
        distortedSignal += feedback * FeedbackAmount;

        feedback = distortedSignal; // Update feedback for the next sample
        return distortedSignal;
    }
}
