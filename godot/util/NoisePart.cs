using Godot;
[GlobalClass, Tool]
public partial class NoisePart : Resource
{
        [Export] public FastNoiseLite noise;
        [Export] public float amplitude;
        [Export] public float frequency = 1f;

        public float Sample(Vector2 pos)
        {
                return noise.GetNoise2Dv(pos * frequency) * amplitude;
        }
}

