using Godot;
[GlobalClass, Tool]
public partial class TerrainObject : Resource
{
        [Export] public PackedScene collision_shape;
        [Export] public Mesh mesh;
        [Export] public float mesh_y_offset;
        [Export] public float base_sale;
        [Export] public float scale_change_amplitude;
        [Export(PropertyHint.Range, "0,180,1")] public int rotation_amplitude;
        [Export(PropertyHint.Range, "0,1,0.01")] public float base_chance_to_spawn;
        [Export(PropertyHint.Range, "0,1,0.01")] public float max_slope;
}
