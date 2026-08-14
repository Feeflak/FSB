using Godot;
[GlobalClass, Tool]
public partial class TerrainAspectsSolver : Node
{

        [Export] public NoiseComponent elevation_noise;
        [Export] public NoiseComponent temperature_noise;
        [Export] public NoiseComponent moisture_noise;

        [Export] public float slope_calculation_step_size;

        [Export] public float moisture_roughness_effect;
        [Export] public float elevation_roughness_effect;
        [Export] public float slope_roughness_effect;



        public class TerrainAspects(float moisture, float temperature, float elevation, float slope, float roughness)
        {
                // all values should be within a 0..1 range
                public float moisture = moisture;
                public float temperature = temperature;
                public float elevation = elevation;
                public float roughness = roughness;
                public float slope = slope;
        }
        public TerrainAspects SolveForPos(Vector2 pos)
        {
                var elevation = elevation_noise.SampleNormalized(pos);
                var temperature = temperature_noise.SampleNormalized(pos);
                var moisture = moisture_noise.SampleNormalized(pos);
                var slope = CalculateSlope(pos);
                var roughness = Mathf.Clamp(elevation_roughness_effect * elevation + moisture_roughness_effect * moisture + slope_roughness_effect * slope, 0, 1);

                return new(moisture, temperature, elevation, slope, roughness);
        }
        private float CalculateSlope(Vector2 pos)
        {
                var current_elevation = elevation_noise.SampleNormalized(pos);

                var elevation_right = elevation_noise.SampleNormalized(pos + Vector2.Right * slope_calculation_step_size);
                var delta_right = (elevation_right - current_elevation) / slope_calculation_step_size;

                var elevation_up = elevation_noise.SampleNormalized(pos + Vector2.Up * slope_calculation_step_size);
                var delta_up = (elevation_up - current_elevation) / slope_calculation_step_size;

                return Mathf.Sqrt(delta_right * delta_right + delta_up * delta_up);
        }
}

