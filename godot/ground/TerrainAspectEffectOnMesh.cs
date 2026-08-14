using Godot;
[GlobalClass, Tool]
public partial class TerrainAspectEffectOnMesh : Resource
{
        [Export] Curve base_height;

        [Export] Curve low_freq_noise_amplitude;
        [Export] Curve medium_freq_noise_amplitude;
        [Export] Curve high_freq_noise_amplitude;
        public void AddEffectToOutput(float value, ref OutputData output)
        {
                output.high_freq_noise_amplitude += high_freq_noise_amplitude.Sample(value);
                output.medium_freq_noise_amplitude += medium_freq_noise_amplitude.Sample(value);
                output.low_freq_noise_amplitude += low_freq_noise_amplitude.Sample(value);
                output.base_height += base_height.Sample(value);
        }
        public class OutputData
        {
                public float low_freq_noise_amplitude;
                public float medium_freq_noise_amplitude;
                public float high_freq_noise_amplitude;
                public float base_height;
        }
}

