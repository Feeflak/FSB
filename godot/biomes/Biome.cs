using Godot;
[Tool, GlobalClass]
public partial class Biome : Resource
{
        public byte index_in_biomes_array;
        [Export] public BiomeObjectsGenData objects_data;

        [ExportGroup("Preferred terrain aspects")]
        [Export] public FloatRange preferred_moisture;
        [Export] public FloatRange preferred_elevation;
        [Export] public FloatRange preferred_temperature;

        [ExportGroup("Ground texture")]
        [Export] public Texture albedo;
        [Export] public Texture normal;
        [Export] public Texture roughness;
        [Export(PropertyHint.ColorNoAlpha)] public Color tint;
        [Export] public float color_offset;
        [Export] public float scale;


}
