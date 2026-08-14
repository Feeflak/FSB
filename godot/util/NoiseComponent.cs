using Godot;
using System.Linq;
[Tool, GlobalClass]
public partial class NoiseComponent : Resource
{
        [Export] NoisePart[] noises;
        /// returns noise in a -Amplitude..Amplitude range
        public float Sample(Vector2 pos)
        {
                return noises.Sum(part => { return part.Sample(pos); });
        }
        public float Amplitude
        {
                get { return noises.Sum(part => { return part.amplitude; }); }
        }
        /// output is within a 0..1 range 
        public float SampleNormalized(Vector2 pos)
        {
                var amplitude = Amplitude;
                return (Sample(pos) + amplitude) / (amplitude * 2);
        }
}
